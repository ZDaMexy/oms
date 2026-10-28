"""Prepare a fresh sibling for exactly29 externally qualified non33 headers.

The caller supplies the frozen parent identity and private source qualification.
This performs no OSK packing and never rewrites a source/qualification directory.
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

from build_lighting_details import store_json
from build_lighting_header import all_files, identity, inventory, sha, write_json
from build_lighting_gameplay_controls import audit_shared_root
from build_omni_skin import NAME, audit_package, author_directory_budget, exact_file, package_closure, preview_files, read_json
from lighting_header import ART_NAMES, LEVEL_TEXTURES, identifiers, require, walk
from lighting_other_frame_header import TARGETS, append_branch, close_bounds, known_difficulties, reverse_branch, rewrite_catalog
from lighting_persistent import _independent


def read_qualification(path, expected_sha256):
    require(re.fullmatch(r"[0-9a-f]{64}", expected_sha256) and sha(path) == expected_sha256, "Use the exact explicitly reviewed private qualification input")
    def unique(pairs):
        result = dict(pairs)
        require(len(result) == len(pairs), "Private qualification has duplicate keys")
        return result
    document = json.loads(path.read_text(encoding="utf-8-sig"), object_pairs_hook=unique)
    actual = [(frame["id"], branch["layout"]) for frame in document["frames"] for branch in frame["branches"]]
    excluded = {(branch["frame"], branch["layout"]) for branch in document["excludedBranches"]}
    all_frames = {"iidx27", "iidx28", "iidx29", "iidx30", "iidx31", "iidx32", "bpl-supernova-hd", *(frame for frame, _ in TARGETS if frame.startswith("bpl-"))}
    require(document["schemaVersion"] == 1 and len(actual) == len(set(actual)) == 29 and set(actual) == TARGETS
            and excluded == {(frame, layout) for frame in all_frames for layout in ("p1", "p2", "dp")} - TARGETS,
            "Keep the exact29 qualified and13 untouched branches")
    require(len(document["baselineFrameFiles"]) == 84, "Keep all42 actual prior Frame contributions")
    return document


def verify_draws(frame, branch, source_map):
    require(source_map["stage"] == frame["stage"], "Do not transplant33 geometry onto a different source stage")
    observed = set()
    for hook in branch["sourceHooks"]:
        scale = 1920 / frame["stage"]["width"]
        projected = {key: value * scale + (hook["existingCentreDx"] if key in ("left", "right") else 0)
                     for key, value in hook["sourceBounds"].items()}
        require(hook["sourceCanvasScale"] == scale and close_bounds(projected, hook["projectedBounds"]),
                "Keep this Frame's actual source projection and existing centre translation")
        expected = hook["sourceDraw"]
        inactive = hook["inactiveSourceDraws"]
        family = [draw for draw in source_map["layouts"][hook["movie"]]["draws"]
                  if draw["geometry"] == hook["geometry"] and draw["texture"] == hook["texture"]
                  and set(hook["bindings"]).issubset(draw["game_bindings"])]
        require(family == sorted((expected, *inactive), key=lambda draw: draw["draw_order"]),
                "The exact main/inactive source draw family changed")
        candidates = [draw for draw in family if draw["draw_order"] == expected["draw_order"]
                      and draw["layer_path"] == expected["layer_path"]]
        require(len(candidates) == 1, "The qualified source draw identity is missing or ambiguous")
        draw = candidates[0]
        require(draw == expected and draw["bounds"] == hook["sourceBounds"] and draw["matrix"] == hook["matrix"]
                and draw["asset"]["package"] == frame["package"]
                and draw["mult_color"] == dict.fromkeys(("r", "g", "b", "a"), 1)
                and draw["add_color"] == dict.fromkeys(("r", "g", "b", "a"), 0) and draw["blend"] == 0,
                "The original source hook geometry, canvas, tint or alpha changed")
        require(all(item["blend"] == 8 and item["mult_color"] == {"r": 1, "g": 1, "b": 1, "a": 0}
                    and item["add_color"] == dict.fromkeys(("r", "g", "b", "a"), 0) for item in inactive),
                "An unconsumed neighbour is not the exact alpha-zero source snapshot")
        for role, family in (("difficulty", "diff_"), ("label", "lv_"), ("number", "lvlnum_")):
            if any(binding.startswith(family) for binding in hook["bindings"]):
                component = branch["geometry"][role]
                require(component["sourceBounds"] == hook["sourceBounds"] and component["matrix"] == hook["matrix"]
                        and component["sourceTexture"] == hook["texture"] and component["projectedBounds"] == hook["projectedBounds"],
                        "The applied component is not its qualified source hook")
                require(role not in observed, "Qualify one exact source instance per header role")
                observed.add(role)
    require(observed == {"difficulty", "label", "number"}, "A complete original hook is missing")


def resolve_art(frame, library, library_rows, source, parent_rows, source33_resources):
    """Read original whole PNGs; reuse actual h33 identities only for exact bytes."""
    expected = {item["name"]: item for item in frame["art"]}
    require(set(expected) == set(ART_NAMES), "The external inventory must identify all eighteen original roles")
    consumed = (*known_difficulties(frame["id"]).values(), "txt_lv_dot", *LEVEL_TEXTURES.values())
    parent_by_path = {row["path"]: row for row in parent_rows}
    shared = {}
    for resource in source33_resources:
        require(resource["id"].startswith("h33.") and resource["type"] == "texture" and resource["filter"] == "nearest", "Reuse the actual unchanged original33 declarations")
        digest = parent_by_path[resource["path"]]["sha256"]
        require(digest not in shared, "Original33 resource identity is ambiguous")
        shared[digest] = resource
    mapping, sizes, resources, copies, proofs = {}, {}, {}, {}, []
    for name in consumed:
        proof = expected[name]["source"]
        path = Path(proof["path"]).resolve(strict=True)
        require(path.is_relative_to(library) and not path.is_symlink() and path.suffix == ".png", "Use a contained independent original PNG")
        relative = path.relative_to(library).as_posix()
        row = library_rows[relative]
        require(re.fullmatch(r"[0-9a-f]{64}", proof["sha256"]) and sha(path) == proof["sha256"] == row["sha256"]
                and path.stem == name and Path(row["package"]).stem == frame["package"]
                and (proof["width"], proof["height"]) == (row["width"], row["height"])
                and row["sampler"]["min"] == row["sampler"]["mag"] == "nearest", "Original header bytes, dimensions, package or sampler differ")
        with Image.open(path) as image:
            require(image.format == "PNG" and image.mode == "RGBA" and image.size == (proof["width"], proof["height"]), "Keep each complete original RGBA canvas")
        digest = proof["sha256"]
        if digest in shared:
            resource = copy.deepcopy(shared[digest])
            require(any(item == resource for item in expected[name]["existing33Resources"])
                    and exact_file(source, resource["path"]).read_bytes() == path.read_bytes(), "A claimed h33 alias is not the same original bytes/declaration")
            reused = True
        else:
            require(not expected[name]["existing33Resources"], "Do not replace a lost h33 alias with another declaration")
            resource = {"id": "oh.r" + digest[:16], "type": "texture", "path": "header-other/" + digest[:16] + ".png", "filter": "nearest"}
            require(resource["path"] not in parent_by_path, "Never overwrite or silently reuse a prior proposal path")
            reused = False
            candidate = {"source": str(path), "sha256": digest, "path": resource["path"], "bytes": path.stat().st_size}
            require(resource["path"] not in copies or copies[resource["path"]]["sha256"] == digest, "A truncated path hash collided")
            copies[resource["path"]] = candidate
        require(resource["id"] not in resources or resources[resource["id"]] == resource, "An original resource alias disagrees")
        resources[resource["id"]] = resource
        mapping[name], sizes[name] = resource["id"], [proof["width"], proof["height"]]
        proofs.append({"name": name, "source": str(path), "sha256": digest, "resource": resource, "reused33": reused})
    return {"mapping": mapping, "sizes": sizes, "resources": list(resources.values()), "copies": list(copies.values()), "proofs": proofs}


def copy_originals(rows, output):
    for row in rows:
        original = Path(row["source"])
        destination = output / row["path"]
        require(not destination.exists() and destination.resolve().is_relative_to(output.resolve()) and sha(original) == row["sha256"], "Do not overwrite or copy changed source art")
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(original, destination)
        require(destination.read_bytes() == original.read_bytes() and sha(destination) == row["sha256"], "Original PNG bytes were changed")


def append_resources(manifest, art):
    result = copy.deepcopy(manifest)
    resources = {item["id"]: item for item in result["resources"]}
    added = []
    for resource in art["resources"]:
        require(resource["id"] not in resources, "The non33 manifest already declares this header; do not apply twice")
        result["resources"].append(copy.deepcopy(resource)); added.append(resource["id"])
    restored = copy.deepcopy(result); del restored["resources"][-len(added):]
    require(restored == manifest, "An existing resource, font, layout, ModsRect or option changed")
    return result, added


def upgrade(source, output, qualification_path, evidence, *, expected_source_sha256, qualification_sha256):
    source, qualification_path = (_independent(path) for path in (source, qualification_path))
    output, evidence = (_independent(path, output=True, exists=False) for path in (output, evidence))
    require(output.parent == source.parent and not output.exists() and not evidence.exists()
            and not output.with_suffix(".osk").exists() and evidence.parent.is_dir()
            and not evidence.is_relative_to(source) and not evidence.is_relative_to(output), "Use a strictly fresh sibling and separate evidence")
    qualification = read_qualification(qualification_path, qualification_sha256)
    require(re.fullmatch(r"[0-9a-f]{64}", expected_source_sha256)
            and expected_source_sha256 == qualification["parent"]["fullPayloadSha256"], "The expected complete parent differs from the reviewed qualification parent")
    library = _independent(qualification["library"])
    require(source == Path(qualification["parent"]["path"]).resolve(strict=True) and not output.is_relative_to(library) and not evidence.is_relative_to(library), "Use the reviewed immutable parent and independent library")
    metadata = {**qualification["sourceMetadataSha256"], str(qualification_path): qualification_sha256}
    require(all(sha(Path(path)) == digest for path, digest in metadata.items()), "Qualification source receipts changed")
    files = all_files(source); before = inventory(source, files)
    require(identity(before) == expected_source_sha256, "The complete frozen parent differs")
    runtime_before = package_closure(source); reports = {"generation.json", "package-audit.json"}
    require(set(files) == set(runtime_before) | reports and not (source / "skin-pages.json").exists()
            and read_json(source / "generation.json")["name"] == NAME, "Preserve the complete gameplay-only work")
    for receipt in qualification["baselineFrameFiles"]:
        require(sha(exact_file(source, receipt["relative"])) == receipt["current"]["sha256"] == receipt["previous"]["sha256"], "An unqualified old Frame changed after the audit")
    catalog = read_json(source / "gameplay/content.json")
    require(len(catalog["modules"]) == 13 and len(preview_files(catalog)) == 539, "Keep all13 modules and539 previews")
    choice33 = next(item for module in catalog["modules"] if module["id"] == "frame" for item in module["choices"] if item["value"] == "iidx33")
    source33 = [item for item in read_json(source / choice33["manifest"])["resources"] if item["id"].startswith("h33.")]
    require(source33 == qualification["source33Resources"], "Original33 declarations changed")
    scene33 = read_json(source / choice33["scene"])
    nodes33 = {node["id"]: node for root in (scene33["root"], *(template["root"] for template in scene33["templates"])) for node in walk(root)}
    approved_dp = {}
    for role, identifier in (("difficulty", "hr.dp.d"), ("label", "hr.dp.lv"), ("number", "hr.dp.n")):
        props = nodes33[identifier]["properties"]
        approved_dp[role] = {"left": props["x"] * 1920, "top": props["y"] * 1080,
                             "right": (props["x"] + props["width"]) * 1920, "bottom": (props["y"] + props["height"]) * 1080}
    library_rows = {item["sprite"]: item for item in read_json(library / "library.json")["textures"] if item.get("sprite")}
    occupied = set().union(*(identifiers(read_json(source / path)) for path in runtime_before if path.endswith(".json")))
    plans, copies, declared_resources, art_proofs = {}, {}, {}, []
    for frame in qualification["frames"]:
        source_map = read_json(Path(frame["sourceMap"]["path"]))
        art = resolve_art(frame, library, library_rows, source, before, source33)
        art_proofs.extend({"frame": frame["id"], **row} for row in art["proofs"])
        for row in art["copies"]:
            require(row["path"] not in copies or copies[row["path"]]["sha256"] == row["sha256"], "A copied alias differs")
            copies[row["path"]] = row
        for resource in art["resources"]:
            if not resource["id"].startswith("h33."): require(resource["id"] not in occupied, "A new resource ID collides with parent content")
            require(resource["id"] not in declared_resources or declared_resources[resource["id"]] == resource, "Cross-Frame resource aliases disagree")
            declared_resources[resource["id"]] = resource
        for branch in frame["branches"]:
            verify_draws(frame, branch, source_map)
            if not branch["originalDirect"]:
                require(all(close_bounds(branch["geometry"][role]["appliedBounds"], bounds) for role, bounds in approved_dp.items()),
                        "The seven DP aliases may only reuse the actual current33 approved placement")
            key = (branch["scene"], branch["manifest"])
            require(key not in plans and sha(source / key[0]) == branch["sceneSha256"] and sha(source / key[1]) == branch["manifestSha256"], "A qualified contribution changed or is duplicated")
            scene, manifest = (read_json(exact_file(source, path)) for path in key)
            updated, receipt = append_branch(scene, manifest, frame, branch, art, occupied)
            occupied.update(receipt["addedIds"])
            updated_manifest, added_resources = append_resources(manifest, art)
            plans[key] = {"scene": updated, "manifest": updated_manifest, "receipt": {**receipt, "addedResourceIds": added_resources}}
            require(reverse_branch(updated, receipt) == scene, "Typed scene reverse proof failed")
    require(len(plans) == 29, "Plan exactly29 independent source contributions")

    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    require(not staging.exists(), "Never reuse a preparation directory")
    evidence.mkdir(); (evidence / "parent-reports").mkdir(); staging.mkdir()
    for report in reports: shutil.copyfile(source / report, evidence / "parent-reports" / report)
    for relative in files:
        target = staging / relative; target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, relative), target)
    copy_originals(list(copies.values()), staging)
    replacements, changes = {}, []
    for (old_scene, old_manifest), plan in plans.items():
        new_scene, new_manifest = store_json(staging, "s", plan["scene"]), store_json(staging, "m", plan["manifest"])
        replacements[(old_scene, old_manifest)] = (new_scene, new_manifest)
        changes.append({**plan["receipt"], "oldScene": old_scene, "oldManifest": old_manifest, "scene": new_scene, "manifest": new_manifest,
                        "sceneByteIncrement": (staging / new_scene).stat().st_size - (source / old_scene).stat().st_size,
                        "manifestByteIncrement": (staging / new_manifest).stat().st_size - (source / old_manifest).stat().st_size})
    updated_catalog = rewrite_catalog(catalog, replacements)
    require(preview_files(updated_catalog) == preview_files(catalog), "A preview changed")
    write_json(staging / "gameplay/content.json", updated_catalog)
    runtime = package_closure(staging)
    retired = set(runtime_before) - set(runtime)
    require(retired == {path for pair in plans for path in pair}, "An excluded layout,33, other module or original texture lost its consumer")
    for relative in retired: exact_file(staging, relative).unlink()
    unchanged = sorted(set(files) & set(runtime) - {"gameplay/content.json"})
    require(inventory(source, unchanged) == inventory(staging, unchanged), "An old resource, INI, preview, Classic control,33 header or unqualified typed field changed")
    # This is a real fresh budget computation, not acceptance copied from a parent report.
    audit = audit_package(staging, runtime); audit_shared_root(staging, updated_catalog, audit)
    require(not audit["aggregateViolations"] and audit["options"] == 32, "No budget or configuration-count relaxation is permitted")
    audit["authorDirectory"] = author_directory_budget(runtime)
    rows = inventory(staging, runtime)
    preservation = {"parentPayloadSha256": expected_source_sha256, "parentPostPayloadSha256": identity(inventory(source, files)),
        "unchangedFiles": len(unchanged), "unchangedSha256": identity(inventory(staging, unchanged)),
        "allExistingPngIniAnd539PreviewsByteEqual": True, "all13ModulesAnd32OptionsPreserved": True,
        "rootSceneClassicControlsAnd33HeaderByteEqual": True, "unqualified13BranchesByteEqual": True,
        "typedSceneAndManifestReversible": True, "newFonts": 0, "newDataSources": 0, "newOptions": 0}
    receipt = {"name": NAME, "status": "prepared-awaiting-fresh-decoder-full-cli-and-native-not-original-parity",
        "createdUtc": datetime.now(timezone.utc).isoformat(), "qualification": str(qualification_path), "qualificationSha256": qualification_sha256,
        "sourceMetadataSha256": metadata, "headers": changes, "sourceArt": art_proofs, "sourcePreservation": preservation,
        "scope": "29 layouts:26 five-difficulty originals;27 three layouts only four original difficulties plus original level.27 BEGINNER and all unknown values remain exact raw text.",
        "runtimeBudget": {"newNodesPerSelectedQualifiedLayout": 7, "additionalReservedTextGlyphsPerSelectedQualifiedLayout": 512,
            "additionalBindings": 2, "additionalVariants": 7, "additionalCases": "49 for27;52 for the other26 layouts", "newBitmapFonts": 0,
            "qualifiedLayoutCount": 29, "qualifiedLayoutCountsAreNotSimultaneouslySelected": True, "nativeReservationAndSharedCodec": "pending"},
        "closure": {"files": len(rows), "sha256": identity(rows)}, "selectedUpperBound": audit["selectedUpperBound"],
        "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk"}}
    write_json(staging / "generation.json", receipt); write_json(staging / "package-audit.json", audit)
    after = inventory(staging, all_files(staging))
    write_json(evidence / "parent-payload-sha256.json", before); write_json(evidence / "output-payload-sha256.json", after)
    write_json(evidence / "source-preservation.json", preservation)
    write_json(evidence / "payload-diff.json", {"changed": ["gameplay/content.json", *sorted(reports)], "retired": sorted(retired),
        "added": sorted(set(row["path"] for row in after) - set(files)), "headers": changes, "copiedOriginals": list(copies.values())})
    write_json(evidence / "generation.json", {**receipt, "payloadSha256": identity(after), "fullFiles": len(after)})
    require(before == inventory(source, files) and all(sha(Path(path)) == digest for path, digest in metadata.items())
            and all(sha(Path(row["source"])) == row["sha256"] for row in art_proofs), "SourcePost mismatch")
    staging.rename(output)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "output", "qualification", "evidence"):
        parser.add_argument("--" + name, type=Path, required=True)
    for name in ("expected-source-sha256", "qualification-sha256"):
        parser.add_argument("--" + name, required=True)
    args = parser.parse_args()
    result = upgrade(args.source, args.output, args.qualification, args.evidence,
                     expected_source_sha256=args.expected_source_sha256, qualification_sha256=args.qualification_sha256)
    print(json.dumps({"status": result["status"], "selectedUpperBound": result["selectedUpperBound"]}, separators=(",", ":")))


if __name__ == "__main__":
    main()
