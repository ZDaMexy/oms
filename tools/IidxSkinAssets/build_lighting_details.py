#!/usr/bin/env python3
"""Rebuild the private Omni study from a verified author, never from an installation.

Existing notes, animations and artwork are preserved byte-for-byte. New visual
programs consume public read-only gameplay data; this script changes no player
data, input binding, chart or game installation.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import re
import uuid

from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact,
                             exact_file, package_closure, publish_prepared_package,
                             read_json, write_json)
import lighting_motion
import lighting_panels


def select_turntable_frame(scene, frame):
    """Capture only the selected Frame's four possible knobs at load time.

    Keeping all 60 knobs would run 180 motor tracks every gameplay frame even
    though 56 could never be shown. Other frames remain independent catalog
    contributions and are validated before import.
    """
    result = copy.deepcopy(scene)
    for template in result["templates"]:
        children = template["root"]["children"]
        selected = [node for node in children if node["id"].endswith("." + frame)]
        if len(children) != 15 or len(selected) != 1:
            raise ValueError("The full source turntable does not contain every Frame exactly once")
        template["root"]["children"] = selected
        selected[0]["properties"]["visible"] = True
    from build_omni_skin import walk
    ids = {node["id"] for root in [result["root"], *(item["root"] for item in result["templates"])] for node in walk(root)}
    for field in ("tracks", "bindings", "variants"):
        result[field] = [program for program in result[field] if program["target"] in ids
                         and program.get("source") != "option.frame"]
    return result


def share_turntable_geometry(scene, manifest, style):
    """Reuse identical geometry with the texture declared by this selected choice.

    C2 captures each scene against its own manifest before selection. A common
    resource identity can therefore resolve to each choice's exact original PNG;
    no extra inactive texture has to be loaded alongside the selected style.
    """
    if len(manifest["resources"]) != 1:
        raise ValueError("A turntable style must declare exactly its own original texture")
    original = manifest["resources"][0]["id"]
    active = "tt.texture"
    prefix = "t." + style + "."
    def rewrite(value):
        if isinstance(value, str):
            if value == original:
                return active
            return "tt." + value[len(prefix):] if value.startswith(prefix) else value
        if isinstance(value, list):
            return [rewrite(item) for item in value]
        if isinstance(value, dict):
            return {key: rewrite(item) for key, item in value.items()}
        return value
    return rewrite(scene), rewrite(manifest)


def deduplicate_inis(output, catalog):
    """One immutable file per exact INI payload, including graph variants."""
    replacements = {}
    for module in catalog["modules"]:
        for choice in module["choices"]:
            for contribution in (choice, *choice.get("variants", [])):
                original = contribution.get("ini")
                if original is None:
                    continue
                if original not in replacements:
                    data = exact_file(output, original).read_bytes()
                    digest = hashlib.sha256(data).hexdigest()
                    relative = "config/i" + digest + ".ini"
                    destination = output / relative
                    if destination.exists() and destination.read_bytes() != data:
                        raise ValueError("An INI content identity conflicts")
                    copy_exact(output / original, destination)
                    replacements[original] = relative
                contribution["ini"] = replacements[original]
    return replacements


def compact_intro_keyframe_ids(scene):
    """Keep the exact source samples while bounding their serialized overhead.

    Keyframe IDs are document-local stable identities, never references from
    another program. Only this selected Frame's new intro channels are renamed.
    """
    identities = {}
    def collect(value):
        if isinstance(value, list):
            for item in value:
                collect(item)
        elif isinstance(value, dict):
            identifier = value.get("id")
            if isinstance(identifier, str) and identifier.startswith("li."):
                identities.setdefault(identifier, f"li{len(identities):x}")
            for key, child in value.items():
                if key != "keyframes":
                    collect(child)
    collect(scene)
    def rewrite(value):
        if isinstance(value, str):
            return identities.get(value, value)
        if isinstance(value, list):
            return [rewrite(item) for item in value]
        if isinstance(value, dict):
            return {key: rewrite(item) for key, item in value.items()}
        return value
    scene.update(rewrite(scene))
    index = 0
    def key_id(number):
        digits = "0123456789abcdefghijklmnopqrstuvwxyz"
        encoded = ""
        while number:
            number, digit = divmod(number, len(digits))
            encoded = digits[digit] + encoded
        return "k" + (encoded or "0")
    used = {keyframe["id"] for track in scene["tracks"] for keyframe in track["keyframes"]}
    for track in scene["tracks"]:
        if not (track.get("clock") == "timing.elapsed" or ".motion-" in track["id"] or ".arrival." in track["id"]):
            continue
        for keyframe in track["keyframes"]:
            # A single short segment is sufficient; identities are local to
            # this document and are never referenced by another program.
            identifier = key_id(index)
            while identifier in used:
                index += 1
                identifier = key_id(index)
            keyframe["id"] = identifier
            used.add(identifier)
            index += 1
    return identities


def select_frame_layouts(manifest, layout):
    """Keep the original declarations for the actually selected presentation."""
    original = manifest["layouts"]
    manifest["layouts"] = [declaration for declaration in original
                           if len(declaration["stages"]) == (2 if layout == "dp" else 1)
                           and declaration.get("presentation", "any") in ("any", layout)]
    if not manifest["layouts"]:
        raise ValueError("The selected Frame has no original declarations for this presentation")
    return {"originalDeclarations": len(original), "selectedDeclarations": len(manifest["layouts"]), "originalGeometryPreserved": True}


def compact_added_textures(output, manifest, scene):
    """Short content identities keep the merged manifest inside its 64KiB cap.

    Only newly added media is addressed here. PNG bytes, sampling, geometry and
    all pre-existing resource identities remain unchanged.
    """
    replacements, declarations, receipts = {}, {}, []
    for resource in manifest["resources"]:
        if resource["id"].startswith(("omni.lighting.", "omni.motion.", "omni.arrival.", "omni.intro.")):
            data = exact_file(output, resource["path"]).read_bytes()
            digest = hashlib.sha256(data).hexdigest()
            identifier = "ld.t" + hashlib.sha256((digest + ":" + resource["filter"]).encode()).hexdigest()[:16]
            path = "art/ld/" + digest[:16] + ".png"
            copy_exact(exact_file(output, resource["path"]), output / path)
            replacements[resource["id"]] = identifier
            receipts.append({"sourceId": resource["id"], "id": identifier, "sourcePath": resource["path"],
                             "path": path, "sha256": digest, "originalPngBytesPreserved": True})
            resource = {**resource, "id": identifier, "path": path}
        previous = declarations.get(resource["id"])
        if previous is not None and previous != resource:
            raise ValueError("A compact texture identity conflicts with existing content")
        declarations[resource["id"]] = resource
    manifest["resources"] = list(declarations.values())

    def rewrite(value):
        if isinstance(value, str):
            return replacements.get(value, value)
        if isinstance(value, list):
            return [rewrite(item) for item in value]
        if isinstance(value, dict):
            return {key: rewrite(item) for key, item in value.items()}
        return value

    for font in manifest.get("bitmapFonts", []):
        for glyph in font["glyphs"]:
            glyph["resource"] = replacements.get(glyph["resource"], glyph["resource"])
    for field in ("root", "templates", "tracks", "variants"):
        scene[field] = rewrite(scene[field])
    return receipts


def independent(path, *, exists=True):
    path = Path(path)
    if path.drive.lower() == "d:":
        raise ValueError("Only independent author libraries are accepted")
    result = path.resolve(strict=exists)
    if result.drive.lower() == "d:":
        raise ValueError("Only independent author libraries are accepted")
    return result


def remove_old_graph(scene, manifest, layout):
    owner = next(item for item in scene["templates"] if item["id"].endswith(f".template.{layout}.information"))
    prefix = owner["root"]["id"] + ".graph"
    removed = {item["id"] for item in scene["templates"] if item["root"]["id"] == prefix}
    if len(removed) != 1:
        raise ValueError("The baseline must contain exactly the proven old graph")
    scene["templates"] = [item for item in scene["templates"] if item["id"] not in removed]
    scene["instances"] = [item for item in scene["instances"] if item["template"] not in removed]
    for field in ("bindings", "tracks", "variants"):
        scene[field] = [item for item in scene[field]
                        if item["target"] != prefix and not item["target"].startswith(prefix + ".")]
    manifest["resources"] = [item for item in manifest["resources"] if item["id"] != "omni.graph.author-comparison"]


def bga_ini(content, *, graph, judge=False):
    """Reserve the graph column, and keep DP videos above the actual HUD band.

    This is an author layout choice. It does not relax the native BGA admission
    check. Graph-off SP geometry remains byte-identical to the source.
    """
    result, keymode, section = [], None, None
    for line in content.splitlines(keepends=True):
        stripped = line.strip()
        if stripped.startswith("["):
            section, keymode = stripped, None
        if section == "[Bms]" and stripped.startswith("Keymode:"):
            keymode = stripped.partition(":")[2].strip()
        match = re.fullmatch(r"(BgaViewports(?:P[12])?):\s*(.*?)\r?\n?", line)
        if section == "[Bms]" and match:
            field, value = match.groups()
            windows = [part.strip().split(",") for part in value.split(";")]
            if any(len(part) != 5 for part in windows):
                raise ValueError("Unexpected BGA author window")
            changed = False
            projected = []
            for part in windows:
                x, y, width, height = map(float, part[:4])
                if keymode == "14K":
                    if graph and x >= 0.5:
                        changed = True
                        continue
                    # Native BGA admission reserves the bottom 8% for HUD.
                    # Keep a subpixel gap; old 27 windows crossed that band.
                    if y + height > 993 / 1080:
                        height = 993 / 1080 - y
                        if height <= 0:
                            raise ValueError("DP BGA starts inside the reserved HUD")
                        changed = True
                    if judge and x >= 0.5 and y + height > 930 / 1080:
                        height = 930 / 1080 - y
                        changed = True
                elif graph and field == "BgaViewportsP1":
                    width = min(x + width, 1498 / 1920) - x
                    changed = True
                elif graph and field == "BgaViewportsP2":
                    right = x + width
                    x = max(x, 422 / 1920)
                    width = right - x
                    changed = True
                if width <= 0 or height <= 0:
                    raise ValueError("The graph and BGA have no non-overlapping author area")
                projected.append(",".join([*(f"{number:.12g}" for number in (x, y, width, height)), part[4]]))
            result.append(field + ": " + "; ".join(projected) + "\n" if changed else line)
        else:
            result.append(line)
    return "".join(result)


def store_json(output, kind, value):
    import json
    data = json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    relative = f"config/{kind}{hashlib.sha256(data).hexdigest()}.json"
    destination = output / relative
    destination.parent.mkdir(parents=True, exist_ok=True)
    if destination.exists() and destination.read_bytes() != data:
        raise ValueError("A configuration content identity conflicts")
    destination.write_bytes(data)
    return relative


def copy_motion_art(library, output, requirements):
    by_key = {(row["package"], row["name"]): row for row in read_json(library / "library.json")["textures"]}
    sources = []
    for role, item in requirements.items():
        source = item["source"]
        row = by_key[("contents/data/graphic/" + source["package"] + ".ifs", source["name"] + ".png")]
        path = exact_file(library, row["sprite"])
        if hashlib.sha256(path.read_bytes()).hexdigest() != row["sha256"]:
            raise ValueError("A motion texture changed from its source receipt")
        copy_exact(path, output / item["resource"]["path"])
        item["resource"]["filter"] = row["sampler"]["mag"]
        sources.append({"role": role, "package": row["package"], "source": row["sprite"],
                        "destination": item["resource"]["path"], "sha256": row["sha256"],
                        "size": [row["width"], row["height"]]})
    return sources


def build(source, library, localized, maps, intro_reference, output):
    source, library, localized, maps = (independent(path) for path in (source, library, localized, maps))
    output = independent(output, exists=False)
    input_roots = (source, library, localized, maps, independent(intro_reference))
    if (output.drive.lower() == "c:" or output.exists() or output.with_suffix(".osk").exists()
            or any(output.is_relative_to(root) or root.is_relative_to(output) for root in input_roots)):
        raise ValueError("Choose a fresh, independent author output")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir(parents=True)
    source_files = package_closure(source)
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    catalog = read_json(staging / "gameplay/content.json")
    root = read_json(staging / "gameplay-skin.json")
    options = lighting_panels.options()
    root["options"] += options
    root["options"][next(i for i, item in enumerate(root["options"]) if item["id"] == "graph-visible")]["label"] = "TARGET SCORE GRAPH · 显示目标图表"
    frames = next(item for item in catalog["modules"] if item["id"] == "frame")
    panels = lighting_panels.prepare_assets(library, staging)
    map_paths = read_json(maps / "frame-maps.json")
    receipts = {"frames": [], "turntables": [], "originalArtwork": []}

    import lighting_arrival
    import lighting_intro
    lighting_arrival.install_option(root)
    root["options"].append(lighting_intro.option())
    arrivals = lighting_arrival.load_reference(localized / "source-reference.json")
    arrival_receipt = lighting_arrival.copy_art(arrivals, staging)
    intro_reference_data = lighting_intro.load_reference(library, intro_reference)
    intro = lighting_intro.prepare_assets(intro_reference_data, staging)

    for choice in frames["choices"]:
        frame = choice["value"]
        source_map = read_json(exact_file(maps, map_paths[frame]))
        art = lighting_motion.art_requirements(frame, source_map)
        art_sources = copy_motion_art(library, staging, art)
        receipts["originalArtwork"] += art_sources
        original_ini = exact_file(source, choice["ini"]).read_text(encoding="utf-8")
        ini_paths = {}
        for graph in (False, True):
            relative = f"gameplay/frames/{frame}/materials{'-graph' if graph else ''}.ini"
            (staging / relative).write_text(bga_ini(original_ini, graph=graph), encoding="utf-8")
            ini_paths[graph] = relative
        judge_ini = f"gameplay/frames/{frame}/materials-judge.ini"
        (staging / judge_ini).write_text(bga_ini(original_ini, graph=False, judge=True), encoding="utf-8")
        original_branches = {"dp": copy.deepcopy(choice), **{variant["when"]["layout"]: copy.deepcopy(variant) for variant in choice["variants"]}}
        branches = {}
        for layout, branch in original_branches.items():
            manifest, scene = (read_json(exact_file(source, branch[field])) for field in ("manifest", "scene"))
            layout_receipt = select_frame_layouts(manifest, layout)
            remove_old_graph(scene, manifest, layout)
            panel_receipt = lighting_panels.inject(scene, manifest, panels, layout=layout,
                                                     judge_sources=lighting_panels.JUDGE_SOURCES, targets=lighting_panels.GRAPH_TARGETS,
                                                     judge_bounds=(1600, 938, 280, 142) if layout == "dp" else None)
            dot_width = next(item["size"][0] for item in art_sources if item["role"] == "normal")
            # Original sprite width is projected by the source movie, not its PNG dimensions alone.
            gauge_hooks = [item for item in source_map["layouts"]["1p_frame"]["draws"] if "gauge" in item.get("texture", "") and item.get("texture", "").endswith("_dot")]
            if gauge_hooks:
                normal = next((item for item in gauge_hooks if "normal_dot" in item["texture"]), gauge_hooks[0])
                dot_width = (normal["bounds"]["right"] - normal["bounds"]["left"]) * 1920 / source_map["stage"]["width"]
            motion = lighting_motion.inject_frame(scene, manifest, frame_id=frame, source_map=source_map, gauge_dot_width_px=dot_width)
            for declaration in manifest["resources"]:
                found = next((item["resource"] for item in art.values() if item["resource"]["id"] == declaration["id"]), None)
                if found:
                    declaration.update(found)
            cue = lighting_arrival.inject_frame(scene, manifest, frame_id=frame, reference=arrivals)
            assembly = lighting_intro.inject(scene, manifest, frame=frame, layout=layout,
                                               reference=intro_reference_data, assets=intro)
            compact_scene_ids = compact_intro_keyframe_ids(scene)
            compact_textures = compact_added_textures(staging, manifest, scene)
            branches[layout] = {"manifest": store_json(staging, "m", manifest), "scene": store_json(staging, "s", scene)}
            receipts["frames"].append({"frame": frame, "layout": layout, "panels": panel_receipt,
                                        "motion": motion, "arrival": cue, "assembly": assembly,
                                        "authorLayout": layout_receipt, "compactTextureNames": compact_textures,
                                        "compactIntroNames": compact_scene_ids})
        choice.update({**branches["dp"], "ini": ini_paths[False], "orderedVariants": True})
        choice["variants"] = [{"when": {"layout": layout, "graph-visible": graph}, **branches[layout], "ini": ini_paths[graph]}
                              for layout in ("p1", "p2") for graph in (True, False)]
        choice["variants"].append({"when": {"graph-visible": True}, **branches["dp"], "ini": ini_paths[True]})
        choice["variants"].append({"when": {"judge-panel-visible": True}, **branches["dp"], "ini": judge_ini})

    for choice in next(item for item in catalog["modules"] if item["id"] == "turntable")["choices"]:
        scene = read_json(exact_file(source, choice["scene"]))
        motion_receipt = lighting_motion.inject_turntables(scene)
        scene, manifest = share_turntable_geometry(scene, read_json(exact_file(source, choice["manifest"])), choice["value"])
        choice["manifest"] = store_json(staging, "m", manifest)
        scenes = {frame: store_json(staging, "s", select_turntable_frame(scene, frame)) for frame in map_paths}
        choice["scene"] = scenes["iidx33"]
        choice["variants"] = [{"when": {"frame": frame}, "manifest": choice["manifest"], "scene": scenes[frame], "ini": choice["ini"]}
                              for frame in map_paths if frame != "iidx33"]
        receipts["turntables"].append({**motion_receipt, "activeKnobsAcrossThreeLayouts": 4,
                                       "activeMotorTracksAcrossThreeLayouts": 12, "selectedAtLoadTime": True})
    receipts["sharedInis"] = deduplicate_inis(staging, catalog)
    write_json(staging / "gameplay/content.json", catalog)
    write_json(staging / "gameplay-skin.json", root)
    files = package_closure(staging)
    changed_original_media = [relative for relative in source_files if relative in files
                              and not relative.endswith((".json", ".ini"))
                              and (source / relative).read_bytes() != (staging / relative).read_bytes()]
    if changed_original_media:
        raise ValueError("Original media changed: " + ", ".join(changed_original_media))
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "defaultFrame": "iidx33",
               "originalHdd": "not accessed; independent libraries only", "repackagedFrom": str(source),
               "lightingDetails": receipts, "sourceArtwork": {"panels": panels["receipt"], "arrival": arrival_receipt,
               "intro": {key: value for key, value in intro.items() if key != "resources"}}, "sourcePreservation": {"changedOriginalMedia": changed_original_media,
               "originalMediaByteExact": True}, "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
               "budgets": audit["selectedUpperBound"], "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
               "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"],
               "scope": "Independent private skin. No bundled assets, source HDD writes, chart/judgement timing changes or fabricated signed replay input."}
    write_json(staging / "generation.json", receipt)
    if audit["aggregateViolations"]:
        raise ValueError(f"Real selected-content budget rejected the candidate: {audit['aggregateViolations']}")
    publish_prepared_package(staging, output, receipt, files, audit)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "library", "localized", "maps", "intro-reference", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    build(args.source, args.library, args.localized, args.maps, args.intro_reference, args.output)


if __name__ == "__main__":
    main()
