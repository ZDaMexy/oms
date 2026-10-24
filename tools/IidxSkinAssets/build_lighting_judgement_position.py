"""Create a fresh private Omni author from reviewed real-gameplay position/OFF evidence."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_judgement_position import OPTION, add_position, definition, host_height, require, reviewed_points
from lighting_persistent import _independent


CONTRACT = "oms-iidx-judgement-position-review.v1"


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def load_review(path):
    path = _independent(path)
    review = json.loads(path.read_text(encoding="utf-8-sig"))
    require(set(review) == {"contract", "status", "source", "geometry", "points", "offHidesLabelAndCombo", "evidence"}
            and review["contract"] == CONTRACT and review["status"] == "reviewed"
            and review["offHidesLabelAndCombo"] is True, "Review the actual gameplay anchors and OFF before authoring")
    reviewed_points(review["points"])
    require(set(review["source"]) == {"manifestSha256", "generationSha256", "archiveSha256"}, "Pin the preserved source author")
    supported = set()
    for receipt in [review["geometry"], *review["evidence"]]:
        require(isinstance(receipt, dict) and set(receipt) == {"path", "sha256", "supports"}
                and isinstance(receipt["supports"], list), "Pin each independently reviewed evidence file")
        evidence_path = _independent(path.parent / receipt["path"])
        require(sha(evidence_path) == receipt["sha256"], "A reviewed evidence file changed")
        supported.update(receipt["supports"])
    require({"geometry", "-2", "0", "1", "10", "off"} <= supported, "Review all four actual positions, geometry and OFF")
    geometry = read_json(_independent(path.parent / review["geometry"]["path"]))
    require(geometry["archive_sha256"] == review["source"]["archiveSha256"]
            and geometry["summary"]["profiles"] == 225
            and geometry["summary"]["existing_scene_manifest_tuples"] == 42
            and geometry["summary"]["all_profile_bms_cap_inactive"] is True,
            "Use the reviewed 225-profile/42-tuple source geometry")
    return review


def frame_heights(source, catalog):
    modules = [module for module in catalog["modules"] if module["id"] == "frame"]
    require(len(modules) == 1 and len(modules[0]["choices"]) == 15, "Keep all fifteen original Frames")
    heights, profiles = {}, 0
    seen_manifests = set()
    for choice in modules[0]["choices"]:
        values = []
        for part in (choice, *choice.get("variants", [])):
            manifest_path = part["manifest"]
            if manifest_path in seen_manifests:
                continue
            seen_manifests.add(manifest_path)
            manifest = read_json(exact_file(source, manifest_path))
            values.extend(host_height(profile) for profile in manifest["layouts"])
            profiles += len(manifest["layouts"])
        require(values and max(values) - min(values) < .0001, "Each reviewed Frame must have a single judgement host height")
        heights[choice["value"]] = values[0]
    require(profiles == 225, "Review every existing authored native layout")
    return heights


def upgrade(source, output, reference):
    source, output = _independent(source), _independent(output, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists()
            and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and OSK")
    review = load_review(reference)
    archive = source.with_suffix(".osk")
    identity = {"manifestSha256": sha(source / "gameplay-skin.json"),
                "generationSha256": sha(source / "generation.json"), "archiveSha256": sha(archive)}
    require(identity == review["source"], "Use the exact preserved author approved by the geometry/native review")
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    original_manifest = read_json(source / "gameplay-skin.json")
    manifest = copy.deepcopy(original_manifest)
    require(not any(option["id"] == OPTION for option in manifest["options"]), "Do not overwrite an existing position setting")
    original_catalog = read_json(source / "gameplay/content.json")
    catalog = copy.deepcopy(original_catalog)
    heights = frame_heights(source, catalog)
    judgement = [module for module in catalog["modules"] if module["id"] == "judgement"]
    require(len(judgement) == 1 and len(judgement[0]["choices"]) == 21
            and sum(len(module["choices"]) for module in catalog["modules"]) == 1272
            and len(set(preview_files(catalog))) == 540, "Preserve every original choice and preview")
    display = [option for option in manifest["options"] if option["id"] == "judgement-display"]
    require(len(display) == 1 and display[0]["default"] == "normal"
            and [choice["value"] for choice in display[0]["choices"]] == ["normal", "label-only"],
            "Extend the established judgement display without changing its default")
    display[0]["choices"].append({"value": "off", "label": "关闭判定与连击"})
    manifest["options"].append(definition())
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(source / relative, staging / relative)
    transformed, receipts = {}, []
    for choice in judgement[0]["choices"]:
        require(len(choice.get("variants", [])) == 15
                and {part["when"]["frame"] for part in choice["variants"]} == set(heights),
                "Keep the existing fifteen Frame branches rather than duplicating fonts or variants")
        for part in (choice, *choice["variants"]):
            key = (part["scene"], part["manifest"])
            logical_height = heights[part.get("when", {}).get("frame", "iidx33")]
            if key not in transformed:
                updated, roots = add_position(read_json(exact_file(source, key[0])),
                                              logical_host_height=logical_height, points=review["points"])
                scene_path = store_json(staging, "s", updated)
                # No module manifest, texture or font changes are necessary.
                transformed[key] = (scene_path, logical_height)
                receipts.append({"sourceScene": key[0], "sourceManifest": key[1], "newScene": scene_path,
                                 "style": choice["value"], "logicalHostHeight": logical_height, "roots": roots})
            scene_path, previous_height = transformed[key]
            require(abs(previous_height - logical_height) < .0001, "A scene cannot use two host-height conversions")
            part["scene"] = scene_path
    require(len(transformed) == 42, "Transform the existing twenty-one SD/HD tuples exactly once")
    restored_catalog = copy.deepcopy(catalog)
    for old_module, module in zip(original_catalog["modules"], restored_catalog["modules"], strict=True):
        if module["id"] == "judgement":
            for old_choice, choice in zip(old_module["choices"], module["choices"], strict=True):
                for old_part, part in zip((old_choice, *old_choice["variants"]), (choice, *choice["variants"]), strict=True):
                    part["scene"] = old_part["scene"]
    restored_manifest = copy.deepcopy(manifest)
    restored_manifest["options"].pop()
    next(option for option in restored_manifest["options"] if option["id"] == "judgement-display")["choices"].pop()
    require(restored_catalog == original_catalog and restored_manifest == original_manifest,
            "No unrelated catalog declaration, default, choice, layout or option may change")
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    changed_configs = {"gameplay-skin.json", "gameplay/content.json"}
    require(all(sha(exact_file(staging, relative)) == hashes[relative]
                for relative in set(source_files).intersection(files) - changed_configs), "An unrelated source file changed")
    require(set(source_files) - set(files) <= {key[0] for key in transformed}, "An unrelated source file was retired")
    require(preview_files(catalog) == preview_files(original_catalog), "Every original preview selection must be preserved")
    require(all(sha(exact_file(source, relative)) == digest for relative, digest in hashes.items())
            and sha(source / "generation.json") == identity["generationSha256"] and sha(archive) == identity["archiveSha256"],
            "The parent author or archive changed")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    require(not audit["aggregateViolations"], "The judgement adaptation exceeds an unchanged author budget")
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
               "sourceIdentity": identity, "judgementPositionReview": review, "judgementPositions": receipts,
               "adaptation": "Four actual-gameplay pixel anchors, linear interpolation between anchors, normalized by the"
                             " existing authored host height, split across lifecycle root and its same-sized OFF gate to"
                             " retain the per-node position4 budget; P1/P2/DP share displacement as an OMS adaptation. The original"
                             " grade rows, fonts, RGB, lifecycle, clip and hit line stay unchanged. Independent child OFF gate.",
               "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
                                      "changedExistingMedia": [], "catalogChoices": 1272, "previewFiles": 540},
               "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
               "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
               "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
                           "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.reference)


if __name__ == "__main__":
    main()
