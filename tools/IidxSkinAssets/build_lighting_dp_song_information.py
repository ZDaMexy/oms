"""Correct only the existing33 DP song text in a fresh v46-r2.

Original anchors are AFP/GEO-backed; the354px bounded-ellipsis width is an
explicit OMS author adaptation. No original font/clip/fit parity is asserted.
"""
from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import json
from pathlib import Path
import re
import shutil
import sys
import uuid

sys.dont_write_bytecode = True

SOURCE_NAME = "omni-full-v46-classic-original-r1"
OUTPUT_NAME = "omni-full-v46-classic-original-r2"
MOVIE = "center_frame_dp_1p"
SOURCES = {"title": "song.title", "artist": "song.artist"}
CHANGED_PROPERTIES = ("x", "y", "width", "height", "alignment")


def require(condition, message):
    if not condition: raise ValueError(message)


def walk(node):
    yield node
    for child in node["children"]: yield from walk(child)


def dp_song_geometry(reference):
    """One geometry path used by both the ordinary Frame builder and final upgrade."""
    require(reference["stage"] == {"width": 1920, "height": 1080, "fps": 60}
            and reference["layouts"][MOVIE]["package"] == "gmframe33", "Use actual33 DP1P source geometry")
    draws = reference["layouts"][MOVIE]["draws"]
    fields, originals = {}, {}
    for name, texture, canvas, matrix in (
            ("title", "usr_l_music", (540, 26), {"a": 1, "b": 0, "c": 0, "d": 1, "tx": 15, "ty": 56}),
            ("artist", "usr_artist_name", (540, 24), {"a": 0.79998779296875, "b": 0, "c": 0, "d": 0.79998779296875, "tx": 15, "ty": 83})):
        matches = [draw for draw in draws if name in draw["game_bindings"]]
        require(len(matches) == 1, "The actual DP song hook must be unique")
        draw = matches[0]
        expected = {"left": matrix["tx"], "top": matrix["ty"], "right": matrix["tx"] + canvas[0] * matrix["a"],
                    "bottom": matrix["ty"] + canvas[1] * matrix["d"]}
        require(draw["texture"] == texture and draw["asset"]["package"] == "gmframe33"
                and (draw["asset"]["width"], draw["asset"]["height"]) == canvas and draw["matrix"] == matrix and draw["bounds"] == expected
                and draw["mult_color"] == dict.fromkeys(("r", "g", "b", "a"), 1)
                and draw["add_color"] == dict.fromkeys(("r", "g", "b", "a"), 0) and draw["blend"] == 0,
                "The original DP text origin, canvas, transform or alpha changed")
        originals[name] = draw
    # The same PNG also appears in the right unit at926. Match the actual left
    # export's place in the graph, never the first filename/texture occurrence.
    seam = [draw for draw in draws if draw["texture"] == "unit_main_dp_1p"
            and [layer["tag_id"] for layer in draw["layer_path"]] == [None, 459, 336, 314]]
    cabinet = [draw for draw in draws if draw["texture"] == "unit_vj_area_dp"]
    require(len(seam) == len(cabinet) == 1 and seam[0]["bounds"] == {"left": 384, "top": 0, "right": 994, "bottom": 1080}
            and cabinet[0]["bounds"] == {"left": 0, "top": 0, "right": 401, "bottom": 960},
            "Use the actual left cabinet source layer, not the unqualified911 boundary")
    left_inset = originals["title"]["bounds"]["left"] - cabinet[0]["bounds"]["left"]
    right = seam[0]["bounds"]["left"] - left_inset
    require(left_inset == 15 and right == 369, "The source-backed left cabinet/inset differs")
    for name, draw in originals.items(): fields[name] = {**draw["bounds"], "right": right}
    return fields, {"movie": MOVIE, "sourceDraws": originals, "leftExportTag": 336, "leftUnitDraw": seam[0],
        "sourceCabinetBoundary": 384, "appliedWidth": 354,
        "authorAdaptation": "Original(15,56)/(15,83) anchors and original placeholder heights; left alignment and bounded ellipsis in the384px cabinet column with symmetric15px inset. Width354 is OMS author adaptation, not original host clip/fit.",
        "font": "Existing OMS font/font-size and white alpha are preserved; original font/long-text/Start behaviour remains unverified."}


def node_properties(bounds):
    values = {"x": bounds["left"] / 1920, "y": bounds["top"] / 1080, "width": (bounds["right"] - bounds["left"]) / 1920,
              "height": (bounds["bottom"] - bounds["top"]) / 1080}
    return {**{key: float(format(value, ".9g")) for key, value in values.items()}, "alignment": "left"}


def reverse_scene(scene, receipt):
    result = copy.deepcopy(scene)
    nodes = {node["id"]: node for root in (result["root"], *(template["root"] for template in result["templates"])) for node in walk(root)}
    for entry in receipt["nodes"]:
        for key in CHANGED_PROPERTIES:
            nodes[entry["id"]]["properties"][key] = copy.deepcopy(entry["oldProperties"][key])
    return result


def adjust_scene(scene, qualified_consumers, fields):
    """Keep every existing typed field except XY/WH/alignment on exactly two nodes."""
    require(len(qualified_consumers) == 2 and {item["source"] for item in qualified_consumers} == set(SOURCES.values()), "Use exactly the two qualified consumers")
    flattened = [node for root in (scene["root"], *(template["root"] for template in scene["templates"])) for node in walk(root)]
    nodes = {node["id"]: node for node in flattened}
    require(len(nodes) == len(flattened), "The parent has duplicate node identities")
    result = copy.deepcopy(scene)
    updated_nodes = {node["id"]: node for root in (result["root"], *(template["root"] for template in result["templates"])) for node in walk(root)}
    entries = []
    for name, source in SOURCES.items():
        expected = next(item for item in qualified_consumers if item["source"] == source)
        bindings = [item for item in scene["bindings"] if item["source"] == source]
        require(bindings == [expected["binding"]] and bindings[0]["property"] == "text", "Preserve the actual title/artist binding identities")
        node = nodes[bindings[0]["target"]]
        require(node == expected["node"] and node["type"] == "text" and node["target"] == {"kind": "global"}, "Use the exact existing Global metadata nodes")
        owner = expected["owner"]
        hosts = [item for item in scene["templates"] if node["id"] in {child["id"] for child in walk(item["root"])}]
        require(len(hosts) == 1 and hosts[0]["id"] == owner["template"] and hosts[0]["root"]["id"] == owner["root"]
                and hosts[0]["root"]["properties"] == owner["rootProperties"] == {} and hosts[0]["root"].get("slot") == "decoration"
                and [item for item in scene["instances"] if item["template"] == owner["template"]] == owner["instances"]
                and len(owner["instances"]) == 1 and owner["instances"][0]["material"] == {"slot": "decoration", "resource": "markers/frame-dp-information"},
                "Do not change the actual owner or adapt a pooled/transformed/different slot")
        require(not any(item["target"] == node["id"] for family in ("tracks", "variants") for item in scene[family])
                and [item for item in scene["bindings"] if item["target"] == node["id"]] == bindings,
                "An additional writer would invalidate the two-node geometry correction")
        p = node["properties"]
        old_rect = (p["x"] * 1920, p["y"] * 1080, p["width"] * 1920, p["height"] * 1080)
        require(all(abs(actual - wanted) < 1e-4 for actual, wanted in zip(old_rect, (610, 43 if name == "title" else 72, 700, 28)))
                and p["alignment"] == "centre" and p["text-overflow"] == "ellipsis" and "bitmap-font" not in p,
                "Use the actual uncorrected33 DP parent, not an already-upgraded/SP consumer")
        updated_nodes[node["id"]]["properties"].update(node_properties(fields[name]))
        entries.append({"id": node["id"], "source": source, "binding": bindings[0]["id"], "ownerTemplate": owner["template"],
                        "oldProperties": copy.deepcopy(p), "newProperties": copy.deepcopy(updated_nodes[node["id"]]["properties"]), "appliedBounds": fields[name]})
    require(len({entry["ownerTemplate"] for entry in entries}) == 1, "The two fields must retain their shared real information owner")
    receipt = {"nodes": entries, "changedProperties": list(CHANGED_PROPERTIES), "changedNodes": 2,
        "newNodes": 0, "newBindings": 0, "newVariants": 0, "newResources": 0, "newFonts": 0,
        "preservedExistingSongTextReservation": 512, "additionalReservedTextGlyphs": 0}
    require(reverse_scene(result, receipt) == scene, "The typed scene differs outside the planned two property dictionaries")
    return result, receipt


def qualified_geometry(source, reference_path, library, audit_path, audit_sha256):
    from build_lighting_header import sha
    from build_omni_skin import exact_file, read_json
    require(re.fullmatch(r"[0-9a-f]{64}", audit_sha256) and sha(audit_path) == audit_sha256, "Use the exact corrected-r2 private audit identity")
    audit = read_json(audit_path)
    boundary = audit.get("backgroundBoundary", {})
    require(boundary.get("requiredLeftExportTag") == 336 and boundary.get("adjacentMainUnitDraw", {}).get("bounds", {}).get("left") == 384
            and audit.get("minimumCandidate", {}).get("title", {}).get("right") == 369
            and audit.get("minimumCandidate", {}).get("artist", {}).get("right") == 369,
            "Reject the original911-boundary report; only the corrected left-layer qualification is usable")
    require(source == Path(audit["author"]).resolve(strict=True), "Use the exact qualified v46-r1 source")
    # Mutable public builder code and saved screenshots are root-cause provenance,
    # not geometry authority. The reviewed audit digest preserves those historical
    # hashes; a legitimate public builder fix must not fail against its old SHA.
    all_inputs = {Path(path).resolve(): digest for path, digest in audit["inputs"].items()}
    metadata = {str(path): digest for path, digest in all_inputs.items() if path.is_relative_to(library) or path == reference_path or path.is_relative_to(source)}
    metadata[str(audit_path)] = audit_sha256
    require(reference_path in all_inputs and all(sha(Path(path)) == digest for path, digest in metadata.items()), "The static library/map/GEO or qualified parent inputs changed")
    reference = read_json(reference_path)
    fields, geometry = dp_song_geometry(reference)
    require(all(fields[name] == audit["minimumCandidate"][name] for name in SOURCES), "The candidate differs from the corrected qualified geometry")
    package_path = library / "author-data/contents/data/graphic/gmframe33/package.json"
    require(package_path in all_inputs, "The static source package must be in the private qualification")
    package = read_json(package_path)
    movie_row = next(item for item in package["movies"] if item["name"] == MOVIE)
    require(sha(exact_file(library, movie_row["raw"])) == movie_row["sha256"], "The original AFP raw identity differs")
    movie = read_json(exact_file(library, movie_row["data"]))
    tags = {item["id"]: item for item in movie["tags"] if item["id"] is not None}
    require(movie["exported_tags"]["x_sp_frame_dp_left"] == geometry["leftExportTag"]
            and movie["exported_tags"][MOVIE] == 517, "The actual AFP left export/root identity changed")
    for name, top_tag, middle_tag, shape_tag in (("title", 28, 27, 26), ("artist", 23, 22, 21)):
        draw = geometry["sourceDraws"][name]
        saved = next(item for item in next(row for row in audit["originalSource"] if row["movie"] == MOVIE)["drawsAndGeo"] if item["binding"] == name)
        require(draw == saved["draw"] and tags[shape_tag]["reference"] == draw["geometry"].split(",")[0]
                and tags[top_tag] == saved["tagChain"][0] and tags[middle_tag] == saved["tagChain"][1]
                and tags[shape_tag] == saved["tagChain"][2], "The original dynamic-text AFP chain changed")
        row = next(item for item in package["geometry"] if item["name"] == tags[shape_tag]["reference"])
        require(read_json(exact_file(library, row["data"])) == saved["parsedGeo"] and sha(exact_file(library, row["raw"])) == row["sha256"], "The complete original text GEO differs")
    # Independently prove that336 is really the exported left unit containing314,
    # rather than a renamed/reordered draw sharing the same texture name.
    require(any(item["source_tag_id"] == 336 and item["depth"] == 5 for item in tags[459]["tags"] if item["type"] == "AP2PlaceObjectTag")
            and any(item["source_tag_id"] == 314 and item["depth"] == 2 for item in tags[336]["tags"] if item["type"] == "AP2PlaceObjectTag")
            and tags[314]["reference"] == geometry["leftUnitDraw"]["geometry"].split(",")[0], "The AFP left cabinet ownership chain differs")
    row = next(item for item in package["geometry"] if item["name"] == tags[314]["reference"])
    raw_path, parsed_path = exact_file(library, row["raw"]), exact_file(library, row["data"])
    require(sha(raw_path) == row["sha256"] and read_json(parsed_path)["bounds"] == {"left": 0, "top": 0, "right": 610, "bottom": 1080}, "The original left-unit GEO differs")
    metadata[str(raw_path)], metadata[str(parsed_path)] = row["sha256"], sha(parsed_path)
    return fields, geometry, audit, metadata


def upgrade(source, reference_path, library, audit_path, output, evidence, *, expected_source_sha256, audit_sha256):
    from build_lighting_header import all_files, identity, inventory, sha, write_json
    from build_lighting_gameplay_controls import audit_shared_root
    from build_omni_skin import NAME, audit_package, author_directory_budget, exact_file, package_closure, preview_files, read_json
    from lighting_persistent import _independent
    source, reference_path, library, audit_path = (_independent(path) for path in (source, reference_path, library, audit_path))
    output, evidence = (_independent(path, output=True, exists=False) for path in (output, evidence))
    require(source.name == SOURCE_NAME and output.name == OUTPUT_NAME and output.parent == source.parent and not output.with_suffix(".osk").exists()
            and evidence.parent.is_dir() and not any(evidence.is_relative_to(path) or output.is_relative_to(path) for path in (source, library)),
            "Use the strictly fresh v46-r1 to sibling v46-r2 correction and separate evidence")
    files = all_files(source); before = inventory(source, files)
    require(re.fullmatch(r"[0-9a-f]{64}", expected_source_sha256) and identity(before) == expected_source_sha256, "The complete explicitly pinned parent payload differs")
    runtime = package_closure(source); reports = {"generation.json", "package-audit.json"}
    require(set(files) == set(runtime) | reports and read_json(source / "generation.json")["name"] == NAME and not (source / "skin-pages.json").exists(), "Use the complete gameplay-only parent")
    fields, geometry, audit_input, metadata = qualified_geometry(source, reference_path, library, audit_path, audit_sha256)
    catalog = read_json(source / "gameplay/content.json")
    frame = next(module for module in catalog["modules"] if module["id"] == "frame")
    choice = next(item for item in frame["choices"] if item["value"] == "iidx33")
    scene_path = choice["scene"]
    require(len(catalog["modules"]) == 13 and len(frame["choices"]) == 15 and len(preview_files(catalog)) == 539
            and scene_path == audit_input["frame33DpScene"] and choice["manifest"] == audit_input["frame33DpManifest"], "The current catalog scope or defaultDP identity differs")
    for module in catalog["modules"]:
        for item in module["choices"]:
            for part in (item, *item.get("variants", [])):
                if part.get("scene") == scene_path:
                    require(module["id"] == "frame" and item["value"] == "iidx33" and part.get("when", {}).get("layout", "dp") == "dp",
                            "The DP scene is also consumed by another Frame/layout/module")
    scene = read_json(exact_file(source, scene_path))
    updated, change = adjust_scene(scene, audit_input["currentConsumers"], fields)
    archive = source.with_suffix(".osk")
    if archive.is_file(): metadata[str(archive)] = sha(archive)
    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    require(not staging.exists(), "Never reuse a preparation directory")
    evidence.mkdir(); (evidence / "parent-files").mkdir(); staging.mkdir()
    allowed = {scene_path, *reports}
    for relative in files:
        destination = staging / relative; destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, relative), destination)
        if relative in allowed: shutil.copyfile(source / relative, evidence / "parent-files" / Path(relative).name)
    # Flat C2 paths are hashes of original relative paths, not content addresses.
    # Keep the existing opaque path and every catalog/INI reference byte-identical.
    write_json(staging / scene_path, updated)
    require(package_closure(staging) == runtime, "No runtime path/resource may be added or retired")
    unchanged = sorted(set(files) - allowed)
    require(inventory(source, unchanged) == inventory(staging, unchanged) and reverse_scene(read_json(staging / scene_path), change) == scene,
            "An unplanned byte or typed scene field changed")
    audit = audit_package(staging, runtime); audit_shared_root(staging, catalog, audit)
    require(not audit["aggregateViolations"] and audit["options"] == 32, "Keep all existing selected budgets and32 options")
    audit["authorDirectory"] = author_directory_budget(runtime)
    runtime_rows = inventory(staging, runtime)
    preservation = {"parentPayloadSha256": expected_source_sha256, "parentPostPayloadSha256": identity(inventory(source, files)),
        "unchangedFiles": len(unchanged), "unchangedSha256": identity(inventory(staging, unchanged)), "changedRuntimeFiles": [scene_path],
        "allOtherFramesRootClassicResourcesFontsOptions539PreviewsByteEqual": True, "catalogManifestIniAndAllReferencePathsByteEqual": True,
        "allTypedIdsBindingsOwnersAnd512SongGlyphReservationPreserved": True, "typedSceneReverseEqual": True}
    report = {"name": NAME, "status": "dp-song-information-source-corrected-awaiting-fresh-cli-and-native",
        "createdUtc": datetime.now(timezone.utc).isoformat(), "repackagedFrom": str(source), "auditInput": {"path": str(audit_path), "sha256": audit_sha256},
        "sourceMetadataSha256": metadata, "rootCauseProvenance": audit_input["rootCause"], "songInformationChange": change, "geometry": geometry,
        "sourcePreservation": preservation, "sceneBytes": {"before": (source / scene_path).stat().st_size, "after": (staging / scene_path).stat().st_size},
        "closure": {"files": len(runtime_rows), "sha256": identity(runtime_rows)}, "selectedUpperBound": audit["selectedUpperBound"],
        "runtimeBudget": {"newNodes": 0, "newResources": 0, "newFonts": 0, "additionalReservedTextGlyphs": 0, "preservedSongTextReservation": 512,
            "qualification": "Same text bindings, font sizes, owners and fanout; no reservation increase. Actual decoder/native counters remain to be verified."},
        "qualification": "Original DP1P anchors/heights; width354/left alignment/bounded ellipsis is an explicit OMS adaptation. Original dynamic font/clip/fit and external Start behaviour are not signed.",
        "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk"}}
    write_json(staging / "generation.json", report); write_json(staging / "package-audit.json", audit)
    after = inventory(staging, all_files(staging))
    write_json(evidence / "parent-payload-sha256.json", before); write_json(evidence / "output-payload-sha256.json", after)
    write_json(evidence / "source-preservation.json", preservation)
    write_json(evidence / "payload-diff.json", {"changed": sorted(allowed), "added": [], "removed": [], "typedChanges": change,
        "oldFilesPreservedSha256": {path: sha(evidence / "parent-files" / Path(path).name) for path in allowed}})
    write_json(evidence / "generation.json", {**report, "fullPayloadSha256": identity(after), "fullFiles": len(after)})
    require(before == inventory(source, files) and all(sha(Path(path)) == digest for path, digest in metadata.items()), "SourcePost mismatch")
    staging.rename(output)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "reference", "library", "audit", "output", "evidence"):
        parser.add_argument("--" + name, type=Path, required=True)
    for name in ("expected-source-sha256", "audit-sha256"):
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    result = upgrade(args.source, args.reference, args.library, args.audit, args.output, args.evidence,
                     expected_source_sha256=args.expected_source_sha256, audit_sha256=args.audit_sha256)
    print(json.dumps({"status": result["status"], "selectedUpperBound": result["selectedUpperBound"]}, separators=(",", ":")))


if __name__ == "__main__":
    main()
