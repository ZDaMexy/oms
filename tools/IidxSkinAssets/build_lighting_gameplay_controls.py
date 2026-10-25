"""Upgrade a frozen Omni author with gameplay controls and real SUB TARGET.

The parent, its archive, and the independent original-art library remain
read-only. Shared Graph and LIFT readout declarations avoid multiplying the
new presentation across all fifteen Frames and 366 artwork choices.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json,
                            resource_references, write_json)
import lighting_gameplay_controls as controls
from lighting_persistent import _independent


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def _nodes(scene):
    return [node for root in (scene["root"], *(item["root"] for item in scene["templates"])) for node in controls.walk(root)]


def trim_manifest(manifest, scene):
    result = copy.deepcopy(manifest)
    fonts = {node["properties"]["bitmap-font"] for node in _nodes(scene) if node["properties"].get("bitmap-font")}
    result["bitmapFonts"] = [font for font in result.get("bitmapFonts", []) if font["id"] in fonts]
    animations = {node["animation"] for node in _nodes(scene) if node.get("animation")}
    result["animations"] = [item for item in result.get("animations", []) if item["id"] in animations]
    used = resource_references(scene, result["bitmapFonts"])
    for animation in result.get("animations", []):
        used.update(animation["resources"])
    result["resources"] = [item for item in result["resources"] if item["id"] in used]
    return result


def append_component(scene, component):
    require = controls.require
    require(not component["root"]["children"] and not component["stateMachines"], "Only inert shared declaration roots may be merged")
    for field in ("templates", "instances", "bindings", "variants", "tracks"):
        scene[field].extend(copy.deepcopy(component[field]))


def merge_named(destination, field, values):
    by_id = {item["id"]: item for item in destination.setdefault(field, [])}
    for value in values:
        if value["id"] in by_id:
            controls.require(value == by_id[value["id"]], "A shared " + field + " identity changes its definition")
        else:
            destination[field].append(copy.deepcopy(value))
            by_id[value["id"]] = value


def _layout(scene):
    roots = {item["target"] for item in scene["bindings"] if item["source"] == "option.graph-visible"}
    template, = [item for item in scene["templates"] if item["root"]["id"] in roots and any(
        node["type"] == "text" for node in controls.walk(item["root"]))]
    instance, = [item for item in scene["instances"] if item["template"] == template["id"]]
    resource = instance["material"]["resource"]
    layout, = [value for value in controls.LAYOUTS if resource == "markers/frame-" + value + "-information"]
    return layout


def stage_geometry(source, frame_module):
    groups = {}
    for choice in frame_module["choices"]:
        dimensions = set()
        for part in (choice, *choice.get("variants", [])):
            manifest = read_json(source / part["manifest"])
            for profile in manifest["layouts"]:
                if profile["ruleset"] == "bms":
                    dimensions.update((stage["rect"][2] * 1920, stage["rect"][3] * 1080) for stage in profile["stages"])
        controls.require(len(dimensions) == 1, "A Frame needs separate readout geometry for its actual stage profiles")
        dimensions, = dimensions
        groups.setdefault(dimensions, []).append(choice["value"])
    return [{"width": width, "height": height, "frames": frames} for (width, height), frames in groups.items()]


def readout_ini(data, *, lift=False):
    lines, result, bms_stage, changed = data.splitlines(keepends=True), [], False, 0
    group_suppression = None
    for line in lines:
        text = line.rstrip(b"\r\n")
        if (text.startswith(b"Target:") or text.startswith(b"[")) and group_suppression is not None:
            controls.require(result[-1].endswith((b"\r\n", b"\n")), "A Stage block lacks its terminating line ending")
            result.extend(group_suppression)
            group_suppression = None
        if text.startswith(b"Target:"):
            bms_stage = text.startswith(b"Target: Stage ") and b"ruleset=bms " in text
            if bms_stage and not lift:
                ending = line[len(text):]
                controls.require(bool(ending), "A Stage target lacks its line ending")
                # C4 Stage declarations also apply to Group and Lane targets.
                # Retain the full Stage owner, then stop this readout marker at
                # the existing Group boundary instead of cloning it per lane.
                group_suppression = (text.replace(b"Target: Stage ", b"Target: Group ", 1) + ending,
                                     b"decoration: resource Suppress" + ending)
        elif text.startswith(b"["):
            bms_stage = False
        if bms_stage and not lift and text == b"decoration: resource Suppress":
            ending = line[len(text):]
            controls.require(bool(ending), "A Stage declaration lacks its line ending")
            line = b'decoration: resource Provide "' + controls.COVER_MARKER.encode("ascii") + b'"' + ending
            changed += 1
        elif bms_stage and lift and text == b'playfield.lift.decoration: resource Provide "custom/markers/v/readout"':
            changed += 1
            continue
        result.append(line)
    if group_suppression is not None:
        controls.require(result[-1].endswith((b"\r\n", b"\n")), "A Stage block lacks its terminating line ending")
        result.extend(group_suppression)
    controls.require(changed > 0, "The old exact readout/material scopes were not found")
    return b"".join(result), changed


def audit_readout_scopes(data):
    """Mirror the existing C4 selector order for every explicit BMS stage.

    A Stage scope inherits to its Group and Lane descendants. This audit is an
    author assertion over that contract; native prepared-slot counts are still
    required to confirm the actual selected publication at runtime.
    """
    declarations, targets, target = [], [], None
    for index, line in enumerate(data.decode("utf-8").splitlines()):
        if line.startswith("Target: "):
            parts = line[len("Target: "):].split()
            target = {"kind": parts[0], **dict(item.split("=", 1) for item in parts[1:])}
            targets.append(target)
        elif line.startswith("["):
            target = None
        elif target is not None and line.startswith("decoration: resource "):
            declarations.append({"target": target, "value": line[len("decoration: resource "):], "line": index})
    stages = [item["target"] for item in declarations if item["target"]["kind"] == "Stage"
              and item["target"].get("ruleset") == "bms" and item["value"] == 'Provide "' + controls.COVER_MARKER + '"']
    controls.require(stages, "There is no explicit BMS Stage readout declaration")
    def winner(probe):
        def matches(item):
            candidate = item["target"]
            if any(candidate.get(key, "any") not in ("any", probe.get(key))
                   for key in ("ruleset", "keymode", "stage-mode", "presentation")):
                return False
            if candidate["kind"] == "Global":
                return True
            if any(candidate.get(key) != probe.get(key) for key in ("group", "group-logical", "group-visual")):
                return False
            if candidate["kind"] == "Stage":
                return probe["kind"] in ("Stage", "Group", "Lane")
            if candidate["kind"] == "Group":
                return probe["kind"] in ("Group", "Lane")
            return probe["kind"] == "Lane" and all(candidate.get(key) == probe.get(key) for key in (
                "lane", "global-logical", "global-visual", "group-local-logical", "group-local-visual"))
        def order(item):
            candidate = item["target"]
            return (*(candidate.get(key, "any") != "any" for key in ("ruleset", "keymode", "stage-mode", "presentation")),
                    {"Global": 0, "Stage": 1, "Group": 2, "Lane": 3}[candidate["kind"]], item["line"])
        candidates = [item for item in declarations if matches(item)]
        return max(candidates, key=order)["value"] if candidates else None
    lane_probes = 0
    for stage in stages:
        controls.require(winner(stage) == 'Provide "' + controls.COVER_MARKER + '"', "A native Stage loses its readout owner")
        controls.require(winner({**stage, "kind": "Group"}) == "Suppress", "A readout marker inherits to Group")
        lanes = [item for item in targets if item["kind"] == "Lane" and all(item.get(key) == stage.get(key) for key in (
            "ruleset", "keymode", "stage-mode", "presentation", "group", "group-logical", "group-visual"))]
        if not lanes:
            lanes = [{**stage, "kind": "Lane", "lane": "author.probe", "global-logical": "0", "global-visual": "0",
                      "group-local-logical": "0", "group-local-visual": "0"}]
        for lane in lanes:
            controls.require(winner(lane) == "Suppress", "A readout marker inherits to Lane")
        lane_probes += len(lanes)
    return {"explicitStageSelectors": len(stages), "readoutTreesPerActualStage": 1,
            "readoutTreesPerGroup": 0, "readoutTreesPerLane": 0,
            "groupProbes": len(stages), "laneProbes": lane_probes,
            "matchingContract": "GameplaySkinDocumentTarget.Matches(snapshot,target), ordered specificity then last line",
            "nativePublicationValidation": "pending"}


def audit_shared_root(output, catalog, audit):
    """Include the now-substantial root in the old module-only offline bound."""
    from PIL import Image

    manifest = read_json(output / "gameplay-skin.json")
    scene = read_json(output / "gameplay-skin.scene.json")
    nodes = _nodes(scene)
    root_resources = {item["id"]: item for item in manifest["resources"]}
    root_fonts = {item["id"]: item for item in manifest.get("bitmapFonts", [])}
    root_ids = set(root_resources)
    measurements, manifest_bounds = [], []
    def numeric_reserve(value):
        if type(value) is int:
            return 2
        if isinstance(value, list):
            return sum(numeric_reserve(item) for item in value)
        if isinstance(value, dict):
            return sum(numeric_reserve(item) for item in value.values())
        return 0
    for module in catalog["modules"]:
        alternatives, manifest_alternatives = [], []
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                path = part.get("manifest")
                if path is None:
                    continue
                selected = read_json(output / path)
                resources = selected.get("resources", [])
                for item in resources:
                    if item["id"] in root_resources:
                        controls.require(item == root_resources[item["id"]], "Root/shared resources disagree")
                fonts = selected.get("bitmapFonts", [])
                for item in fonts:
                    if item["id"] in root_fonts:
                        controls.require(item == root_fonts[item["id"]], "Root/shared fonts disagree")
                alternatives.append(len({item["id"] for item in fonts} - set(root_fonts)))
                exclusive = copy.deepcopy(selected)
                exclusive["resources"] = [item for item in resources if item["id"] not in root_resources]
                exclusive["bitmapFonts"] = [item for item in fonts if item["id"] not in root_fonts]
                manifest_alternatives.append(len(json.dumps(exclusive, ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
                                             + numeric_reserve(exclusive))
        measurements.append(max(alternatives))
        manifest_bounds.append(max(manifest_alternatives))
    total = dict(audit["selectedUpperBound"])
    total["fonts"] = len(root_fonts) + sum(measurements)
    total["manifestBytes"] = sum(manifest_bounds)
    for key, value in (("nodes", len(nodes)), ("tracks", len(scene["tracks"])),
                       ("keyframes", sum(len(item["keyframes"]) for item in scene["tracks"])),
                       ("bindings", len(scene["bindings"])), ("variants", len(scene["variants"])),
                       ("templates", len(scene["templates"])), ("instances", len(scene["instances"])),
                       ("sceneBytes", (output / "gameplay-skin.scene.json").stat().st_size + numeric_reserve(scene)),
                       ("manifestBytes", (output / "gameplay-skin.json").stat().st_size + numeric_reserve(manifest))):
        total[key] += value
    # Counting every root texture in addition to the module upper bound is
    # conservative even where the exact C2 union will deduplicate Graph art.
    total["resources"] += len(root_resources)
    for resource in root_resources.values():
        path = output / resource["path"]
        with Image.open(path) as image:
            pixels = image.width * image.height
        total["pixels"] += pixels
        total["decodedTextureBytes"] += pixels * 4
        total["encodedTextureBytes"] += path.stat().st_size
    limits = {"resources": 384, "pixels": 96 * 1024 * 1024, "decodedTextureBytes": 384 * 1024 * 1024,
              "encodedTextureBytes": 64 * 1024 * 1024, "fonts": 10, "nodes": 2048, "tracks": 512,
              "keyframes": 8192, "bindings": 512, "variants": 512, "templates": 128, "instances": 1024,
              "sceneBytes": 512 * 1024, "manifestBytes": 64 * 1024}
    violations = [{"budget": key, "upperBound": total[key], "limit": limit} for key, limit in limits.items() if total[key] > limit]
    controls.require(not violations, f"The shared-root presentation exceeds unchanged selected budgets: {violations}")
    audit["sharedRoot"] = {"declaredNodes": len(nodes), "resources": len(root_resources), "fonts": len(root_fonts),
                           "method": "Module maxima plus complete root; exact root-font identities and matching root manifest declarations deduplicated, all numeric re-encoding reserved"}
    audit["selectedUpperBound"] = total
    audit["aggregateViolations"] = violations


def upgrade(source, library, output, evidence):
    source = _independent(source)
    library = _independent(library)
    output = _independent(output, output=True, exists=False)
    evidence = _independent(evidence, output=True, exists=False)
    controls.require(output.parent == source.parent and output != source and not output.exists()
                     and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and archive")
    controls.require(not evidence.is_relative_to(source) and not evidence.is_relative_to(library), "Keep review evidence outside immutable sources")
    archive = source.with_suffix(".osk")
    generation = read_json(source / "generation.json")
    archive_sha = sha(archive)
    controls.require(generation["archive"]["sha256"] == archive_sha, "The parent archive differs from its receipt")
    original_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in original_files}
    generation_sha = sha(source / "generation.json")
    catalog = read_json(source / "gameplay/content.json")
    original_catalog = copy.deepcopy(catalog)
    manifest = controls.add_sub_target_option(read_json(source / "gameplay-skin.json"))
    scene = read_json(source / "gameplay-skin.scene.json")
    frames, = [module for module in catalog["modules"] if module["id"] == "frame"]
    lift, = [module for module in catalog["modules"] if module["id"] == "lift-art"]
    controls.require(len(frames["choices"]) == 15 and len(lift["choices"]) == 366,
                     "Use the complete current fifteen-Frame/366-LIFT author")
    stage_dimensions = stage_geometry(source, frames)
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in original_files:
        copy_exact(exact_file(source, relative), staging / relative)
    reference = controls.source_reference(library)
    assets = controls.prepare_assets(library, staging, reference)
    all_controls = controls.build_controls(reference, assets, stage_geometry=stage_dimensions)
    stage_templates = {item["id"] for item in all_controls["templates"] if item["root"]["id"] == "gc.stage"}
    stage_component, control_scene = controls._selected_document(all_controls, stage_templates)
    shared, transformed, receipts, inis = {}, {}, [], {}
    for module in (frames, lift):
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                key = (part["scene"], part["manifest"])
                if key not in transformed:
                    original_scene = read_json(source / key[0])
                    original_manifest = read_json(source / key[1])
                    working_manifest = original_manifest
                    if module["id"] == "frame":
                        layout = _layout(original_scene)
                        graph, changed_scene = controls.extract_graph(original_scene, layout=layout)
                        graph_manifest = trim_manifest(original_manifest, graph)
                        signature = json.dumps({"scene": graph, "resources": graph_manifest["resources"],
                            "bitmapFonts": graph_manifest["bitmapFonts"]}, sort_keys=True, separators=(",", ":"))
                        if layout in shared:
                            controls.require(signature == shared[layout]["signature"], "Frame Graph geometry/media differ; sharing would change artwork")
                            shared[layout]["verifiedFrames"].append(choice["value"])
                        else:
                            shared[layout] = {"signature": signature, "scene": graph, "manifest": graph_manifest,
                                              "verifiedFrames": [choice["value"]]}
                        receipt = {"kind": "graph", "frame": choice["value"], "layout": layout,
                                   "originalNodes": len(_nodes(original_scene)), "remainingNodes": len(_nodes(changed_scene))}
                        append_component(changed_scene, controls.select_stage_frame(stage_component, choice["value"]))
                        working_manifest = copy.deepcopy(original_manifest)
                        merge_named(working_manifest, "resources", assets["resources"])
                        merge_named(working_manifest, "bitmapFonts", assets["bitmapFonts"])
                        changed_scene, precision = controls.compact_static_geometry(changed_scene)
                        receipt["staticGeometryEncoding"] = precision
                        changed_scene, projective_precision = controls.compact_projective_frames(changed_scene)
                        receipt["projectiveFrameEncoding"] = projective_precision
                        changed_scene = controls.compact(changed_scene, "f")
                    else:
                        old_readout, changed_scene = controls.extract_lift_readout(original_scene)
                        receipt = {"kind": "lift-readout", "choice": choice["value"], "removedNodes": len(_nodes(old_readout)) - 1}
                    changed_manifest = trim_manifest(working_manifest, changed_scene)
                    paths = {"scene": store_json(staging, "s", changed_scene), "manifest": store_json(staging, "m", changed_manifest)}
                    transformed[key] = paths
                    receipts.append({"sourceScene": key[0], "sourceManifest": key[1], **paths, **receipt})
                part.update(transformed[key])
                old_ini = part["ini"]
                if old_ini not in inis:
                    ini_data, count = readout_ini((source / old_ini).read_bytes(), lift=module["id"] == "lift-art")
                    scope_audit = audit_readout_scopes(ini_data) if module["id"] == "frame" else None
                    ini_path = "config/i" + hashlib.sha256(ini_data).hexdigest() + ".ini"
                    (staging / ini_path).write_bytes(ini_data)
                    inis[old_ini] = {"path": ini_path, "declarations": count, "module": module["id"], "readoutScopes": scope_audit}
                part["ini"] = inis[old_ini]["path"]
    controls.require(set(shared) == set(controls.LAYOUTS) and all(len(item["verifiedFrames"]) == 15 for item in shared.values()),
                     "Every actual Frame/layout must prove identical shared Graph declarations")
    shared_graph, graph_geometry = controls.share_graph_layouts({layout: item["scene"] for layout, item in shared.items()})
    append_component(scene, controls.compact(controls.add_sub_target(shared_graph), "gg.shared."))
    for item in shared.values():
        merge_named(manifest, "resources", item["manifest"]["resources"])
        merge_named(manifest, "bitmapFonts", item["manifest"]["bitmapFonts"])
    append_component(scene, control_scene)
    merge_named(manifest, "resources", assets["resources"])
    merge_named(manifest, "bitmapFonts", assets["bitmapFonts"])
    # Original full-combo resource IDs and recorded clips are already shared.
    # Only their long outer PNG paths are shortened, preserving every byte.
    full_combo, = [module for module in catalog["modules"] if module["id"] == "full-combo"]
    full_combo_paths, full_combo_receipts = {}, []
    for choice in full_combo["choices"]:
        for part in (choice, *choice.get("variants", [])):
            old_path = part["manifest"]
            if old_path not in full_combo_paths:
                original = read_json(source / old_path)
                compacted = copy.deepcopy(original)
                aliases = []
                for resource in compacted["resources"]:
                    old_png = resource["path"]
                    data = exact_file(source, old_png).read_bytes()
                    digest = hashlib.sha256(data).hexdigest()
                    short_png = "art/gf/" + digest[:16] + ".png"
                    destination = staging / short_png
                    if destination.exists():
                        controls.require(destination.read_bytes() == data, "A full-combo short-path hash conflicts")
                    else:
                        destination.parent.mkdir(parents=True, exist_ok=True)
                        destination.write_bytes(data)
                    resource["path"] = short_png
                    aliases.append({"source": old_png, "path": short_png, "sha256": digest})
                full_combo_paths[old_path] = store_json(staging, "m", compacted)
                full_combo_receipts.append({"sourceManifest": old_path, "manifest": full_combo_paths[old_path],
                    "pngAliases": aliases, "resourceIdsAndRecordedClipsUnchanged": True})
            part["manifest"] = full_combo_paths[old_path]
    marker_resource = assets["mapping"]["tx_sub_length"]
    marker_source = next(item["path"] for item in assets["resources"] if item["id"] == marker_resource)
    copy_exact(staging / marker_source, staging / (controls.COVER_MARKER + ".png"))
    controls.require(controls.COVER_MARKER.encode("ascii") not in (source / "skin.ini").read_bytes(),
                     "The parent already owns a gameplay stage readout")
    scene = controls.compact(scene, "g")
    scene, geometry_precision = controls.compact_static_geometry(scene)
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay-skin.scene.json", scene)
    write_json(staging / "gameplay/content.json", catalog)
    restored = copy.deepcopy(catalog)
    for old_module, module in zip(original_catalog["modules"], restored["modules"], strict=True):
        for old_choice, choice in zip(old_module["choices"], module["choices"], strict=True):
            for old_part, part in zip((old_choice, *old_choice.get("variants", [])), (choice, *choice.get("variants", [])), strict=True):
                if module["id"] in ("frame", "lift-art"):
                    part["scene"], part["manifest"] = old_part["scene"], old_part["manifest"]
                    part["ini"] = old_part["ini"]
                elif module["id"] == "full-combo":
                    part["manifest"] = old_part["manifest"]
    controls.require(restored == original_catalog, "An unrelated choice, default, preview, INI or contribution changed")
    files = package_closure(staging)
    allowed = {"skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json", "gameplay/content.json"}
    controls.require(all(sha(staging / path) == hashes[path] for path in set(original_files) & set(files) - allowed),
                     "A retained source payload changed")
    controls.require(preview_files(catalog) == preview_files(original_catalog), "Source previews changed")
    audit = audit_package(staging, files)
    audit_shared_root(staging, catalog, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    controls.require(all(sha(source / path) == digest for path, digest in hashes.items())
                     and sha(source / "generation.json") == generation_sha and sha(archive) == archive_sha,
                     "An immutable parent changed during generation")
    evidence.mkdir(parents=True, exist_ok=True)
    source_path = evidence / "source-reference.json"
    source_data = (json.dumps(reference, ensure_ascii=False, separators=(",", ":")) + "\n").encode("utf-8")
    if source_path.exists():
        controls.require(source_path.read_bytes().replace(b"\r\n", b"\n") == source_data, "Existing source evidence differs")
    else:
        source_path.write_bytes(source_data)
    asset_proof_path = evidence / "control-assets.json"
    write_json(asset_proof_path, {"contract": "oms-omni-gameplay-control-assets.v1", "assets": assets["receipt"],
        "sourceLibraryUnchanged": True,
        "fontMetricsUnchanged": {"lineHeight": 20, "digitAdvance": 21, "pointAdvance": 8},
        "currentValueFormat": "Existing fixed-2 text remains an author adaptation; no new numeric clock or engine format"})
    review = {"contract": "oms-omni-gameplay-controls-author-review.v1", "sourceReferenceSha256": sha(source_path),
        "reviewedSourceGeometry": {mode: {layout: item["window"]["bounds"] for layout, item in layouts.items()}
                                   for mode, layouts in reference["panels"].items()},
        "whiteGreenFont": "All ten original pairs match visible white pixels multiplied by #64ff00, including original alpha. One font replaces old duplicated LIFT font; no font-budget increase.",
        "speedMapping": "OMS existing Normal1..20 and Floating/Classic0.5..10. Normal/Classic have eleven true OMS scale ticks plus the original selected strip; the existing native Frame HS retains the exact value. Only Floating retains the source CURRENT HI-SPEED readout. Tick sampling and floating slider linearity are author adaptations, not the original tier-to-multiplier table.",
        "floatingPointAndPlacement": "The original 8x8 point is copied unchanged at (0,12) in an 8x20 transparent line canvas. Only the measured 9x9 baked point in each FHS plate is filled with its measured #ff8400 background; every outside RGBA pixel is unchanged. P2 keeps the original plate mirror. Bounds for the two baked limit labels and the CURRENT four-digit union come directly from the AFP/GEO evidence.",
        "controlAssetProof": {"path": str(asset_proof_path), "sha256": sha(asset_proof_path)},
        "subTarget": "Primary valid always wins. PB/rank primary unavailable permits chosen fallback; neither available yields NO DATA. No rival/national/history data.",
        "nativeKeypadEvidence": "Original guest keypad2 attempts are not confirmed state changes. Official SUB TARGET behavior is the semantic source; actual OMS settings and runtime validation are separate.",
        "sharedGraph": {layout: item["verifiedFrames"] for layout, item in shared.items()},
        "sharedGraphGeometry": graph_geometry,
        "staticGeometryEncoding": geometry_precision,
        "stageReadoutGeometry": stage_dimensions,
        "stageReadoutOwnership": "Existing full native Stage decoration. Global actual cover units/active and native LIFT offset move the readout; horizontal leaf envelopes keep original label/font pixels fixed regardless of cover height or opacity.",
        "stageReadoutScopes": "Every BMS Stage Provide is followed by the same-context Group Suppress. C4 Stage inheritance is therefore stopped before Group/Lane, preserving exactly one tree per actual Stage. Author selector checks are recorded with INI receipts; native prepared publication counts remain required.",
        "maniaLiftReadout": "The old LIFT readout was already hidden by lift.available: mania has no IGameplaySkinLiftStateSource and the event producer publishes default unavailable. Mania Stage decoration remains the original Suppress; original LIFT fill declarations/art are retained.",
        "sourceAndParentUnchanged": True, "codecAndActualPlayerValidation": "pending"}
    write_json(evidence / "author-review.json", review)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": {"archiveSha256": archive_sha, "generationSha256": generation_sha},
        "gameplayControlSource": {"path": str(source_path), "sha256": sha(source_path)}, "authorReview": review,
        "transformedContributions": receipts,
        "stageReadoutIni": inis,
        "fullComboPngAliases": full_combo_receipts,
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "catalogChoices": sum(len(module["choices"]) for module in catalog["modules"]), "previewFiles": len(preview_files(catalog)),
            "retiredConfiguration": sorted(set(original_files) - set(files))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "library", "output", "evidence"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.library, args.output, args.evidence)


if __name__ == "__main__":
    main()
