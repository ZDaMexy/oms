"""Upgrade only the existing Classic controls in a fresh frozen Omni sibling.

Original digests and source receipts are external private inputs. This command
does not pack an OSK or claim decoder, native, speed-math or original-host parity.
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

from PIL import Image

sys.dont_write_bytecode = True

from build_lighting_header import all_files, identity, inventory, sha, write_json
from build_lighting_gameplay_controls import audit_shared_root
from build_omni_skin import NAME, audit_package, author_directory_budget, exact_file, package_closure, preview_files, read_json
import lighting_gameplay_controls as controls
from lighting_persistent import _independent

require = controls.require
REFERENCE_FILES = ("classic-panel-art-r1.json", "classic-panel-static-supplement-r1.json")


def classic_source_inputs(library, reference_directory):
    """Check actual source geometry and private PNG/GEO receipts at this input boundary."""
    documents = [read_json(exact_file(reference_directory, name)) for name in REFERENCE_FILES]
    art, supplement = documents
    reference = controls.source_reference(library)
    rows = {(item["package"], Path(item["name"]).stem): item for item in read_json(library / "library.json")["textures"]}
    expected = {}
    source_metadata = {str(reference_directory / name): sha(reference_directory / name) for name in REFERENCE_FILES}
    source_metadata[str(library / "library.json")] = sha(library / "library.json")
    package_path = controls.inside(library, "author-data/contents/data/graphic/1/gameparts/package.json")
    package = read_json(package_path)
    source_metadata[str(package_path)] = sha(package_path)
    movie_rows = {row["name"]: row for row in package["movies"]}
    geometry_rows = {row["name"]: row for row in package["geometry"]}
    for movie in reference["sourceMovies"]:
        row = movie_rows[movie["name"]]
        source_metadata[str(controls.inside(library, row["raw"]))] = movie["rawSha256"]
        source_metadata[str(controls.inside(library, movie["parsed"]))] = movie["parsedSha256"]
    for movie in reference["movies"].values():
        for shape in movie["shapes"]:
            row = geometry_rows[shape["geometry"]]
            source_metadata[str(controls.inside(library, row["raw"]))] = row["sha256"]
            parsed = controls.inside(library, row["data"])
            source_metadata[str(parsed)] = sha(parsed)
    for digit in range(10):
        for suffix in ("", "g"):
            row = rows[controls.PACKAGE, f"notes_time_num{digit}{suffix}"]
            source_metadata[str(controls.inside(library, row["sprite"]))] = row["sha256"]

    def geometry_file(path, digest):
        path = Path(path).resolve(strict=True)
        require(path.is_relative_to(library) and re.fullmatch(r"[0-9a-f]{64}", digest) and sha(path) == digest,
                "A private Classic source receipt no longer matches the independent library")
        source_metadata[str(path)] = digest

    def picture(path, digest, size):
        path = Path(path).resolve(strict=True)
        require(path.is_relative_to(library) and path.suffix == ".png", "Use the contained original Classic PNG")
        name = path.stem
        row = rows[controls.PACKAGE, name]
        require(re.fullmatch(r"[0-9a-f]{64}", digest) and digest == row["sha256"] == sha(path)
                and path == controls.inside(library, row["sprite"]) and tuple(size) == (row["width"], row["height"]),
                "The private expected PNG bytes differ from the actual source inventory")
        require(row["sampler"]["min"] == row["sampler"]["mag"] == "nearest", "Keep original nearest Classic sampling")
        with Image.open(path) as image:
            require(image.format == "PNG" and image.mode == "RGBA" and image.size == tuple(size), "The original Classic PNG canvas differs")
        value = {"name": name, "source": str(path), "sha256": digest, "bytes": path.stat().st_size,
                 "size": list(size), "filter": "nearest", "sourceSprite": row["sprite"]}
        require(name not in expected or value == expected[name], "The Classic source receipts disagree")
        expected[name] = value
        source_metadata[str(path)] = digest

    strip = art["originalStrip"]
    picture(strip["spritePng"], strip["spriteSha256"], strip["spriteSize"])
    for selector in art["authorReuse"]["selectors"]:
        if Path(selector["path"]).stem in ("playm_classic_spd_sel", "playm_spd_sel"):
            picture(selector["path"], selector["sha256"], selector["size"])
    layouts = art["originalLayoutGeometry"]
    require(len(layouts) == 3 and {row["layout"] for row in layouts} == set(controls.LAYOUTS), "Use all three original Classic source layouts")
    for row in layouts:
        actual = controls.classic_geometry(reference, row["layout"])
        require(all(actual[key] == row[key] for key in ("strip", "cursor")), "The private Classic GEO evidence changed")
        for key in ("wholeStripOriginalGeo", "cursorGeo"):
            geometry_file(row[key]["parsed"], row[key]["parsedSha256"])
        require([float(item["value"]) for item in row["rowAnchors"]] == list(controls.CLASSIC_VALUES), "Keep the eleven original strip values")
        cx, top = actual["center"]
        require(all(item["sourceAlignedCursorCenter"] == [cx, top + 37 * index] for index, item in enumerate(row["rowAnchors"])),
                "Use the source cursor's exact 37px row alignment")
    for component in supplement["correctOriginalStaticComponents"]:
        if component.get("sourcePng"):
            png = component["sourcePng"]
            picture(png["path"], png["sha256"], png["size"])
            geometry_file(component["sourceGeo"]["path"], component["sourceGeo"]["sha256"])
    labels = supplement["notesTimeLabelAndCarrier"]
    require(len(labels) == 3 and {row["layout"] for row in labels} == set(controls.LAYOUTS), "Use the three actual NOTES TIME source anchors")
    for row in labels:
        actual = controls.classic_geometry(reference, row["layout"])
        require(actual["notes"] == row["sourceLabel"], "The Classic NOTES TIME GEO differs")
        png = row["sourcePng"]
        # This receipt stores the whole label dimensions in sourceLabel.bounds, not sourcePng.
        # picture still cross-checks these verified GEO dimensions with the inventory and decoded PNG canvas.
        picture(png["path"], png["sha256"], actual["notes"]["bounds"][2:])
        geometry_file(row["sourceGeo"]["path"], row["sourceGeo"]["sha256"])
    names = {"tx_num1_4", "playm_classic_spd_sel", "playm_spd_sel", "tx_notes_time", "spd_classic_window", "spd_classic_window_dp", "tx_classic_hispeed"}
    require(set(expected) == names, "Use exactly the verified Classic source roles; no synthesized glyphs or substitute artwork")
    for name, size in controls.CLASSIC_ASSET_SIZES.items():
        require(tuple(expected[name]["size"]) == size, "Keep the complete original Classic canvas")
    return reference, expected, source_metadata


def append_classic_resources(manifest, expected, payload_inventory):
    """Preserve every old resource/ID and append two direct-PNG definitions."""
    result = copy.deepcopy(manifest)
    by_path = {row["path"]: row for row in payload_inventory}
    old = [resource for resource in manifest["resources"] if re.fullmatch(r"gc\.a[0-9a-f]+", resource["id"])]
    require(old, "Use the actual existing gc asset namespace")
    mapping = {}
    for name, source in expected.items():
        if name in controls.CLASSIC_ASSET_NAMES:
            continue
        matches = [resource for resource in old if by_path[resource["path"]]["sha256"] == source["sha256"]]
        require(len(matches) == 1 and matches[0]["filter"] == "nearest", "An old Classic source role is absent or ambiguous: " + name)
        mapping[name] = matches[0]["id"]
    first = max(int(resource["id"].removeprefix("gc.a"), 16) for resource in old) + 1
    additions = []
    for index, name in enumerate(controls.CLASSIC_ASSET_NAMES):
        source = expected[name]
        relative = "art/gc/" + source["sha256"][:16] + ".png"
        resource = {"id": "gc.a" + format(first + index, "x"), "type": "texture", "path": relative, "filter": "nearest"}
        require(relative not in by_path and not any(item["id"] == resource["id"] or item["path"] == relative for item in manifest["resources"]),
                "Do not overwrite or disguise an already-added Classic resource")
        result["resources"].append(resource)
        mapping[name] = resource["id"]
        additions.append({**source, "resource": resource})
    restored = copy.deepcopy(result); del restored["resources"][-2:]
    require(restored == manifest, "An existing manifest resource, font, option or layout changed")
    return result, mapping, additions


def copy_classic_resources(additions, output):
    for row in additions:
        source = Path(row["source"])
        target = output / row["resource"]["path"]
        require(not target.exists() and target.resolve().is_relative_to(output.resolve()) and sha(source) == row["sha256"], "Never overwrite Classic art or copy changed source bytes")
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        require(target.read_bytes() == source.read_bytes() and sha(target) == row["sha256"], "Original Classic PNG bytes were not preserved")


def upgrade(source, reference_directory, library, output, evidence, *, expected_source_sha256):
    source, reference_directory, library = (_independent(path) for path in (source, reference_directory, library))
    output = _independent(output, output=True, exists=False)
    evidence = _independent(evidence, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists() and not output.with_suffix(".osk").exists(), "Create a fresh sibling author only")
    require(not evidence.exists() and evidence.parent.is_dir() and not any(evidence.is_relative_to(path) or output.is_relative_to(path) for path in (source, reference_directory, library)), "Use fresh independent output and evidence")
    require(re.fullmatch(r"[0-9a-f]{64}", expected_source_sha256), "Provide the complete frozen source payload SHA256")
    files = all_files(source); before = inventory(source, files)
    require(identity(before) == expected_source_sha256, "The complete frozen author payload differs")
    runtime_before = package_closure(source)
    reports = {"generation.json", "package-audit.json"}
    require(set(files) == set(runtime_before) | reports and read_json(source / "generation.json")["name"] == NAME
            and not (source / "skin-pages.json").exists(), "Use the current complete gameplay-only Omni source")
    reference, expected, metadata = classic_source_inputs(library, reference_directory)
    archive = source.with_suffix(".osk")
    if archive.is_file(): metadata[str(archive)] = sha(archive)
    manifest = read_json(source / "gameplay-skin.json")
    scene = read_json(source / manifest["scene"])
    require(manifest["scene"] == "gameplay-skin.scene.json", "Use the actual current shared control scene")
    updated_manifest, mapping, additions = append_classic_resources(manifest, expected, before)
    updated_scene, changes = controls.upgrade_classic_presentation(scene, reference, mapping)
    occupied = set()
    for path in runtime_before:
        if path.endswith(".json"):
            document = read_json(source / path)
            def ids(value):
                if isinstance(value, dict):
                    if isinstance(value.get("id"), str): occupied.add(value["id"])
                    for child in value.values(): ids(child)
                elif isinstance(value, list):
                    for child in value: ids(child)
            ids(document)
    require(not occupied.intersection(changes["addedIds"]) and not occupied.intersection(row["resource"]["id"] for row in additions), "New Classic identities collide with the current author")
    require(controls.reverse_classic_presentation(updated_scene, changes) == scene, "Classic scene reversal failed")
    catalog = read_json(source / "gameplay/content.json")
    require(len(preview_files(catalog)) == 539 and len(next(module for module in catalog["modules"] if module["id"] == "frame")["choices"]) == 15,
            "Keep all current Frames and 539 previews")
    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    require(not staging.exists(), "Never reuse a preparation directory")
    evidence.mkdir(); (evidence / "parent-files").mkdir(); staging.mkdir()
    allowed = {"gameplay-skin.json", manifest["scene"], *reports}
    for path in files:
        target = staging / path; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, path), target)
        if path in allowed: shutil.copyfile(source / path, evidence / "parent-files" / Path(path).name)
    copy_classic_resources(additions, staging)
    write_json(staging / "gameplay-skin.json", updated_manifest)
    write_json(staging / manifest["scene"], updated_scene)
    runtime = package_closure(staging)
    require(set(runtime) == set(runtime_before) | {row["resource"]["path"] for row in additions}, "The runtime closure changed outside the two new original PNGs")
    unchanged = sorted(set(files) - allowed)
    require(inventory(source, unchanged) == inventory(staging, unchanged), "An old PNG, INI, Frame, module, preview, typed configuration or marker changed")
    audit = audit_package(staging, runtime)
    audit_shared_root(staging, catalog, audit)
    require(not audit["aggregateViolations"] and audit["options"] == 32, "Do not relax the selected or unified option budgets")
    audit["authorDirectory"] = author_directory_budget(runtime)
    runtime_rows = inventory(staging, runtime)
    preservation = {"parentPayloadSha256": expected_source_sha256, "parentPostPayloadSha256": identity(inventory(source, files)),
                    "unchangedFileCount": len(unchanged), "unchangedFilesSha256": identity(inventory(staging, unchanged)),
                    "typedSceneReversalEqual": True, "typedManifestReversalEqual": True,
                    "allExistingResourceIdsAndFontDefinitionsPreserved": True, "allNonClassicControlsPreserved": True,
                    "originalClassicPngBytesDirectCopied": True, "allIniAnd539PreviewsAnd15FramesAndOtherModulesByteEqual": True,
                    "oldOpaqueMasksAutoAvailabilityAndHeaderPreserved": True}
    root_delta = {"oldSceneBytes": (source / manifest["scene"]).stat().st_size, "newSceneBytes": (staging / manifest["scene"]).stat().st_size,
                  "oldManifestBytes": (source / "gameplay-skin.json").stat().st_size, "newManifestBytes": (staging / "gameplay-skin.json").stat().st_size,
                  "oldNodes": len(controls._scene_nodes(scene)), "newNodes": len(controls._scene_nodes(updated_scene)),
                  "oldResources": len(manifest["resources"]), "newResources": len(updated_manifest["resources"]),
                  "addedOriginalEncodedBytes": sum(row["bytes"] for row in additions),
                  "addedOriginalPixels": sum(row["size"][0] * row["size"][1] for row in additions),
                  "newBitmapFonts": 0, "newOptions": 0, "newReadonlySources": 0}
    report = {"name": NAME, "status": "classic-original-source-prepared-awaiting-fresh-cli-and-native",
              "createdUtc": datetime.now(timezone.utc).isoformat(), "repackagedFrom": str(source), "sourceMetadataSha256": metadata,
              "classicChanges": changes, "newOriginalAssets": additions, "sourcePreservation": preservation, "rootDelta": root_delta,
              "closure": {"files": len(runtime_rows), "uncompressedBytes": sum(row["bytes"] for row in runtime_rows), "sha256": identity(runtime_rows)},
              "selectedUpperBound": audit["selectedUpperBound"], "qualification": "Art geometry only; old off-grid values, speed calculation, clocks and replay data are unchanged. No original-host or native acceptance asserted.",
              "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk"}}
    write_json(staging / "generation.json", report); write_json(staging / "package-audit.json", audit)
    after = inventory(staging, all_files(staging))
    write_json(evidence / "parent-payload-sha256.json", before)
    write_json(evidence / "output-payload-sha256.json", after)
    write_json(evidence / "runtime-payload-sha256.json", runtime_rows)
    write_json(evidence / "source-preservation.json", preservation)
    write_json(evidence / "generation.json", {**report, "fullPayloadSha256": identity(after), "fullFiles": len(after)})
    write_json(evidence / "payload-diff.json", {"changed": sorted(allowed), "added": [row["resource"]["path"] for row in additions], "removed": [],
        "parentFilesPreserved": {path: sha(evidence / "parent-files" / Path(path).name) for path in sorted(allowed)}})
    require(before == inventory(source, files) and all(sha(Path(path)) == digest for path, digest in metadata.items()), "A frozen source, archive, reference or original art changed")
    staging.rename(output)
    print(json.dumps({"author": str(output), "fullPayloadSha256": identity(after), "files": len(after), "runtimeFiles": len(runtime_rows),
                      "selectedUpperBound": audit["selectedUpperBound"], "validation": "pending authoritative CLI/native; no OSK created"}, separators=(",", ":")), flush=True)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "reference", "library", "output", "evidence"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--expected-source-sha256", required=True)
    args = parser.parse_args()
    upgrade(args.source, args.reference, args.library, args.output, args.evidence, expected_source_sha256=args.expected_source_sha256)


if __name__ == "__main__":
    main()
