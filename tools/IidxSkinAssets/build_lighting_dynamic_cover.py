"""Publish the complete lane345 dual-clock art in a new private Omni author."""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_lighting_gameplay_controls import audit_shared_root
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
    package_closure, preview_files, publish_prepared_package, read_json, write_json)
import lighting_dynamic_cover as cover


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def upgrade(source, output, reference_path):
    source = cover._independent(source)
    output = cover._independent(output, output=True, exists=False)
    cover.require(output.parent == source.parent and output != source and not output.exists()
                  and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and archive")
    reference = cover.load_reference(reference_path)
    archive = source.with_suffix(".osk")
    generation = read_json(exact_file(source, "generation.json"))
    identity = {"generationSha256": sha(source / "generation.json"), "archiveSha256": sha(archive)}
    cover.require(generation["archive"]["sha256"] == identity["archiveSha256"], "The immutable parent archive changed")
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    original = read_json(exact_file(source, "gameplay/content.json"))
    cover.require(len(original["modules"]) == 13 and sum(len(module["choices"]) for module in original["modules"]) == 1285,
                  "Use the complete timing/AUTO SCRATCH/Graph parent")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    catalogue = copy.deepcopy(original)
    media, new_configs, receipts, replaced_files = {}, set(), [], set()
    for item in reference["files"]:
        relative = item["path"]
        cover.require(relative not in source_files or hashes[relative] == item["sha256"], "A compact new art path conflicts with parent data")
        copy_exact(Path(item["source"]), staging / relative)
        cover.require(sha(staging / relative) == item["sha256"], "Lossless dynamic source media changed while copying")
        media[relative] = item["sha256"]
    for module_id in ("lane-cover", "lift-art", "graph-area"):
        module, = [value for value in catalogue["modules"] if value["id"] == module_id]
        choice, = [value for value in module["choices"] if value["value"] == "l345"]
        for part in (choice, *choice.get("variants", [])):
            manifest = read_json(exact_file(source, part["manifest"]))
            scene = read_json(exact_file(source, part["scene"]))
            old_definition, = [value for value in manifest["animations"] if value["id"] == cover.OLD_ANIMATION]
            replaced_files.add(old_definition["path"])
            replaced_files.update(value["path"] for value in manifest["resources"] if value["id"] in old_definition["resources"])
            updated_manifest, updated_scene, receipt = cover.transform_contribution(manifest, scene, reference)
            part.update({"manifest": store_json(staging, "m", updated_manifest), "scene": store_json(staging, "s", updated_scene)})
            new_configs.update((part["manifest"], part["scene"]))
            receipts.append({"module": module_id, **receipt, "manifest": part["manifest"], "scene": part["scene"]})
    write_json(staging / "gameplay/content.json", catalogue)
    files = package_closure(staging)
    cover.require(not replaced_files.intersection(files), "The replaced incomplete lane345 animation/atlas still enters the archive")
    cover.require(set(files) <= set(source_files) | set(media) | new_configs, "Only the reviewed dynamic art/configuration may enter the closure")
    cover.require(preview_files(original) == preview_files(catalogue), "Preserve all existing choice previews")
    for old_module, new_module in zip(original["modules"], catalogue["modules"]):
        if old_module["id"] not in ("lane-cover", "lift-art", "graph-area"):
            cover.require(old_module == new_module, "An unrelated option/module changed")
        else:
            for old_choice, new_choice in zip(old_module["choices"], new_module["choices"]):
                if old_choice["value"] != "l345":
                    cover.require(old_choice == new_choice, "Another art design changed")
    cover.require(all(sha(exact_file(staging, relative)) == hashes[relative]
                      for relative in set(source_files).intersection(files) if relative != "gameplay/content.json"),
                  "A retained parent document, pixel, font or animation changed")
    audit = audit_package(staging, files)
    write_json(staging / "dynamic-cover-budget-check.json", {"selectedUpperBound": audit["selectedUpperBound"],
        "violations": audit["aggregateViolations"], "configurationBytes": audit["configurationBytes"]})
    print({"staging": str(staging), "selectedUpperBound": audit["selectedUpperBound"], "violations": audit["aggregateViolations"]})
    audit_shared_root(staging, catalogue, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    parent_audit = read_json(exact_file(source, "package-audit.json"))
    cover.require(audit["options"] == 32 and audit["selectedUpperBound"]["fonts"] == parent_audit["selectedUpperBound"]["fonts"]
                  and not audit["aggregateViolations"], "Retain the unchanged public option/font/resource budgets")
    cover.load_reference(reference_path)
    cover.require(all(sha(exact_file(source, relative)) == expected for relative, expected in hashes.items())
                  and sha(source / "generation.json") == identity["generationSha256"] and sha(archive) == identity["archiveSha256"],
                  "The immutable parent author/archive changed")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "dynamicCover": {"reference": str(Path(reference_path).resolve()),
            "referenceSha256": sha(reference_path), "clockPolicy": reference["qualification"], "branches": reference["branches"],
            "proof": reference["proof"], "contributions": receipts, "fontsAdded": 0, "optionsAdded": 0},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedExistingMedia": [], "previewFiles": len(preview_files(catalogue)), "replacedIncompleteDependencies": sorted(replaced_files)},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    print({"author": str(output), "contributions": len(receipts), "newMedia": len(media), "budgets": receipt["budgets"], "closure": receipt["closure"]})
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--reference", required=True, type=Path)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.reference)


if __name__ == "__main__":
    main()
