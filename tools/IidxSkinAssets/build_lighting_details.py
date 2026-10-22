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

from build_omni_skin import (NAME, audit_package, author_directory_budget, calibrate_full_combo, copy_exact, enable_toggle_defaults,
                             exact_file, package_closure, publish_prepared_package,
                             read_json, write_json, resource_references, walk)
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
        if not (track.get("clock") in ("timing.elapsed", "preparation.elapsed") or ".motion-" in track["id"] or ".arrival." in track["id"]):
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
    import brotli
    import json
    for animation in manifest.get("animations", []):
        if not set(animation["resources"]).intersection(replacements):
            continue
        original = exact_file(output, animation["path"]).read_bytes()
        clip = json.loads(brotli.decompress(original) if animation["path"].endswith(".br") else original)
        for geometry in clip["geometries"]:
            if geometry["resource"] is not None:
                geometry["resource"] = replacements.get(geometry["resource"], geometry["resource"])
        encoded = brotli.compress(json.dumps(clip, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode(), quality=7)
        relative = "art/lc/" + hashlib.sha256(encoded).hexdigest()[:24] + ".json.br"
        destination = output / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists() and destination.read_bytes() != encoded:
            raise ValueError("A compact recorded clip content identity conflicts")
        destination.write_bytes(encoded)
        animation["path"] = relative
        animation["resources"] = sorted({replacements.get(identifier, identifier) for identifier in animation["resources"]})
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


def compact_scene_identifiers(scene):
    """Shorten only document-local identities; source samples and art stay exact."""
    identities = {}
    def collect(value):
        if isinstance(value, dict):
            if isinstance(value.get("id"), str):
                identities.setdefault(value["id"], "of" + format(len(identities), "x"))
            for child in value.values():
                collect(child)
        elif isinstance(value, list):
            for child in value:
                collect(child)
    collect(scene)
    def rewrite(value):
        if isinstance(value, dict):
            return {key: rewrite(child) for key, child in value.items()}
        if isinstance(value, list):
            return [rewrite(child) for child in value]
        if isinstance(value, str):
            return identities.get(value, value)
        return value
    scene.update(rewrite(scene))
    return {"identities": len(identities), "sourceSamplesChanged": 0}


def replace_lighting_panels(scene, manifest, panels, *, layout):
    """Replace the proven v14 panels, retaining all other Frame owners."""
    information = next(item for item in scene["templates"] if item["id"].endswith(f".template.{layout}.information"))
    prefix = information["root"]["id"] + ".lighting-panels"
    removed = [item for item in scene["templates"] if item["root"]["id"].startswith(prefix)]
    if len(removed) != 3:
        raise ValueError("The source is not the three-panel v14 Frame baseline")
    template_ids = {item["id"] for item in removed}
    node_ids = {node["id"] for item in removed for node in walk(item["root"])}
    scene["templates"] = [item for item in scene["templates"] if item["id"] not in template_ids]
    scene["instances"] = [item for item in scene["instances"] if item["template"] not in template_ids]
    for field in ("tracks", "bindings", "variants"):
        scene[field] = [item for item in scene[field] if item["target"] not in node_ids]
    manifest["bitmapFonts"] = [font for font in manifest.get("bitmapFonts", [])
                               if font["id"] not in (lighting_panels.GRAPH_FONT, lighting_panels.JUDGE_FONT)]
    return lighting_panels.inject(scene, manifest, panels, layout=layout,
        judge_sources=lighting_panels.JUDGE_SOURCES, targets=lighting_panels.GRAPH_TARGETS,
        judge_bounds=(1600, 938, 280, 142) if layout == "dp" else None)


def gate_ready_option(scene):
    ready = [item for item in scene["templates"] if item["root"]["id"].endswith(".ready")]
    if len(ready) != 1:
        raise ValueError("The selected Frame must have exactly one GET READY owner")
    root = ready[0]["root"]
    identifier = root["id"] + ".option"
    enabled = {"id": identifier, "type": "container", "target": {"kind": "global"}, "blend": "inherit",
               "properties": {"visible": False}, "effects": [], "children": root["children"]}
    root["children"] = [enabled]
    scene["bindings"].append({"id": identifier + ".visible", "target": identifier,
                              "property": "visible", "source": "option.get-ready"})


def prune_unused_resources(scene, manifest):
    used = resource_references(scene, manifest.get("bitmapFonts", []))
    for animation in manifest.get("animations", []):
        used.update(animation["resources"])
    before = len(manifest["resources"])
    manifest["resources"] = [resource for resource in manifest["resources"] if resource["id"] in used]
    return before - len(manifest["resources"])


def compact_frame_resource_ids(output, scene, manifest, identities, protected):
    """Intern Frame-only names without duplicating art shared with other modules."""
    import brotli
    import json
    mapping = {}
    for resource in manifest["resources"]:
        identifier = resource["id"]
        if identifier in protected:
            continue
        if identifier not in identities:
            identities[identifier] = "fr" + format(len(identities), "x")
        mapping[identifier] = identities[identifier]
        resource["id"] = mapping[identifier]
    def rewrite(value):
        if isinstance(value, dict):
            return {key: rewrite(child) for key, child in value.items()}
        if isinstance(value, list):
            return [rewrite(child) for child in value]
        if isinstance(value, str):
            return mapping.get(value, value)
        return value
    scene.update(rewrite(scene))
    manifest["bitmapFonts"] = rewrite(manifest.get("bitmapFonts", []))
    for animation in manifest.get("animations", []):
        if not set(animation["resources"]).intersection(mapping):
            continue
        data = exact_file(output, animation["path"]).read_bytes()
        clip = json.loads(brotli.decompress(data) if animation["path"].endswith(".br") else data)
        for geometry in clip["geometries"]:
            if geometry["resource"] is not None:
                geometry["resource"] = mapping.get(geometry["resource"], geometry["resource"])
        encoded = brotli.compress(json.dumps(clip, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode(), quality=7)
        relative = "art/lc/" + hashlib.sha256(encoded).hexdigest()[:24] + ".json.br"
        destination = output / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if destination.exists() and destination.read_bytes() != encoded:
            raise ValueError("A compact Frame clip identity conflicts")
        destination.write_bytes(encoded)
        animation["path"] = relative
        animation["resources"] = sorted({mapping.get(identifier, identifier) for identifier in animation["resources"]})
    return {"renamedFrameOnlyResources": len(mapping), "sharedResourcesKept": len(manifest["resources"]) - len(mapping),
            "originalPngPathsAndBytesPreserved": True, "originalRecordedGeometryAndFramesPreserved": True}


def build(source, library, lane_reference, ready_reference, output):
    """Upgrade the frozen v14 author, preserving its existing CUSTOM closure."""
    import lighting_arrival
    import lighting_intro
    import lighting_lane_intro
    import lighting_ready

    source, library, lane_reference, ready_reference = (independent(path) for path in
        (source, library, lane_reference, ready_reference))
    output = independent(output, exists=False)
    inputs = (source, library, lane_reference, ready_reference)
    if (output.drive.lower() == "c:" or output.exists() or output.with_suffix(".osk").exists()
            or any(output.is_relative_to(root) or root.is_relative_to(output) for root in inputs)):
        raise ValueError("Choose a fresh, independent author output")
    original_generation = read_json(source / "generation.json")
    if original_generation.get("status") != "validated-local-private-delivery" or "lightingDetails" not in original_generation:
        raise ValueError("The source must be the validated complete v14 lighting author")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir(parents=True)
    source_files = package_closure(source)
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    catalog = read_json(staging / "gameplay/content.json")
    full_combo_default = calibrate_full_combo(catalog)
    root = read_json(staging / "gameplay-skin.json")
    new_options = [lighting_lane_intro.OPTION,
                   {"id": "get-ready", "type": "toggle", "label": "Gameplay · GET READY", "default": True}]
    existing = {item["id"] for item in root["options"]}
    if existing.intersection(item["id"] for item in new_options):
        raise ValueError("The source already contains the new preparation options")
    root["options"].extend(copy.deepcopy(new_options))
    enable_toggle_defaults(root)
    panels = lighting_panels.prepare_assets(library, staging)
    ready = lighting_ready.prepare_assets(library, staging, ready_reference)
    lane = lighting_lane_intro.load_reference(lane_reference)
    if set(lane["frames"]) != set(lighting_motion.FRAMES):
        raise ValueError("The opening reference must contain all fifteen current Game Frames")
    frames = next(item for item in catalog["modules"] if item["id"] == "frame")
    receipts = {"frames": [], "preparationDurationMs": 5000, "originalFrameChoices": len(frames["choices"])}
    protected_ids = set()
    for module in catalog["modules"]:
        if module["id"] == "frame":
            continue
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                if part.get("manifest"):
                    protected_ids.update(resource["id"] for resource in read_json(exact_file(source, part["manifest"]))["resources"])
    prepared_lanes, transformed, resource_identities = {}, {}, {}
    for choice in frames["choices"]:
        frame = choice["value"]
        for contribution in (choice, *choice.get("variants", [])):
            identity = (frame, contribution["manifest"], contribution["scene"])
            if identity not in transformed:
                manifest = read_json(exact_file(source, contribution["manifest"]))
                scene = read_json(exact_file(source, contribution["scene"]))
                layouts = [layout for layout in ("p1", "p2", "dp") if any(
                    item["id"].endswith(f".template.{layout}.information") for item in scene["templates"])]
                if len(layouts) != 1:
                    raise ValueError("A v14 Frame must contain one selected information layout")
                layout = layouts[0]
                panel = replace_lighting_panels(scene, manifest, panels, layout=layout)
                arrival = lighting_arrival.upgrade_existing(scene)
                assembly = lighting_intro.upgrade_existing(scene, layout=layout)
                key = (frame, layout)
                if key not in prepared_lanes:
                    prepared_lanes[key] = lighting_lane_intro.prepare_assets(lane, staging, frame=frame, layout=layout)
                opening = lighting_lane_intro.inject(scene, manifest, frame=frame, layout=layout, assets=prepared_lanes[key])
                ready_receipt = lighting_ready.inject(scene, manifest, ready, layout=layout)
                gate_ready_option(scene)
                scene["preparationDuration"] = 5000
                textures = compact_added_textures(staging, manifest, scene)
                pruned = prune_unused_resources(scene, manifest)
                resource_names = compact_frame_resource_ids(staging, scene, manifest, resource_identities, protected_ids)
                names = compact_scene_identifiers(scene)
                transformed[identity] = {"manifest": store_json(staging, "m", manifest), "scene": store_json(staging, "s", scene)}
                receipts["frames"].append({"frame": frame, "layout": layout, "panels": panel,
                    "arrival": arrival, "assembly": assembly, "laneOpening": opening, "ready": ready_receipt,
                    "compactedTextures": textures, "prunedObsoleteDeclarations": pruned,
                    "frameResourceIdentities": resource_names, "sceneIdentities": names})
            contribution.update(transformed[identity])
    # Other modules retain their exact choices, previews, media and scene bytes;
    # only the proven 33 Full Combo default and its two identifying labels change.
    write_json(staging / "gameplay/content.json", catalog)
    write_json(staging / "gameplay-skin.json", root)
    files = package_closure(staging)
    changed_media = [relative for relative in source_files if relative in files
        and not relative.endswith((".json", ".ini"))
        and (source / relative).read_bytes() != (staging / relative).read_bytes()]
    if changed_media:
        raise ValueError("Existing source media changed: " + ", ".join(changed_media))
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "defaultFrame": "iidx33",
        "originalHdd": "not accessed; independent libraries only", "repackagedFrom": str(source),
        "preparationDetails": receipts, "fullComboDefault": full_combo_default,
        "sourceArtwork": {"panels": panels["receipt"], "ready": ready["receipt"],
            "laneReference": str(lane_reference), "laneFrames": len(lane["frames"])},
        "sourcePreservation": {"changedExistingMedia": changed_media, "existingReferencedMediaByteExact": True,
            "unreferencedRetiredMedia": [relative for relative in source_files if relative not in files
                and not relative.endswith((".json", ".ini"))]},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "closure": {"files": len(files),
            "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"],
        "scope": "Independent private skin. Preparation uses original per-Frame lane art and full READY; no gameplay timing or player-data writes."}
    write_json(staging / "generation.json", receipt)
    if audit["aggregateViolations"]:
        raise ValueError(f"Real selected-content budget rejected the candidate: {audit['aggregateViolations']}")
    publish_prepared_package(staging, output, receipt, files, audit)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "library", "lane-reference", "ready-reference", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    build(args.source, args.library, args.lane_reference, args.ready_reference, args.output)


if __name__ == "__main__":
    main()
