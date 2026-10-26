"""Add PACEMAKER NEXT to a fresh Omni author without changing original media.

The seven target definitions come from the official33 score-graph manual.
Integer rounding and personal-best equality use an explicit OMS policy; the
receipt keeps those separate from native observations. NEXT+ and rotate are
not inferred. The old author, archive and catalog remain read-only.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_lighting_gameplay_controls import audit_shared_root
from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, write_json)
import lighting_pacemaker_targets as targets
from lighting_persistent import _independent


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def upgrade(source, output):
    source = _independent(source)
    output = _independent(output, output=True, exists=False)
    targets.require(output.parent == source.parent and output != source and not output.exists()
                    and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and archive")
    archive = source.with_suffix(".osk")
    generation = read_json(source / "generation.json")
    identity = {"manifestSha256": sha(source / "gameplay-skin.json"),
                "sceneSha256": sha(source / "gameplay-skin.scene.json"),
                "generationSha256": sha(source / "generation.json"), "archiveSha256": sha(archive)}
    targets.require(generation["archive"]["sha256"] == identity["archiveSha256"],
                    "The immutable parent archive differs from its receipt")
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    original_catalog = read_json(source / "gameplay/content.json")
    catalog = copy.deepcopy(original_catalog)
    targets.require(len(catalog["modules"]) == 11 and sum(len(module["choices"]) for module in catalog["modules"]) == 1271
                    and len(preview_files(catalog)) == 539, "Use the complete current eleven-module Omni author")
    original_manifest = read_json(source / "gameplay-skin.json")
    scene, manifest, module, contributions, transformation = targets.split_target_module(
        read_json(source / "gameplay-skin.scene.json"), original_manifest)
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    choice_paths = []
    for choice in module["choices"]:
        contribution = contributions[choice["value"]]
        choice.update({"scene": store_json(staging, "s", contribution["scene"]),
                       "manifest": store_json(staging, "m", contribution["manifest"])})
        choice_paths.append({"value": choice["value"], "scene": choice["scene"], "manifest": choice["manifest"]})
    catalog["modules"].append(module)
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay-skin.scene.json", scene)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    new_paths = {part[field] for part in choice_paths for field in ("scene", "manifest")}
    targets.require(set(files) == set(source_files) | new_paths, "Only the new target-choice configurations may extend the closure")
    changed = {"gameplay-skin.json", "gameplay-skin.scene.json", "gameplay/content.json"}
    targets.require(all(sha(exact_file(staging, path)) == hashes[path] for path in set(source_files) - changed),
                    "An existing image, font, catalog, preview or unrelated configuration changed")
    targets.require(catalog["modules"][:-1] == original_catalog["modules"] and len(catalog["modules"]) == 12
                    and sum(len(item["choices"]) for item in catalog["modules"]) == 1278
                    and preview_files(catalog) == preview_files(original_catalog),
                    "All original eleven modules, 1271 choices and539 previews must remain exact")
    restored_manifest = copy.deepcopy(manifest)
    sub, = [item for item in restored_manifest["options"] if item["id"] == "graph-sub-target"]
    targets.require(sub["choices"].pop() == {"value": targets.TARGET_VALUE, "label": "PACEMAKER NEXT（BMS）"},
                    "Only NEXT may extend the existing SUB TARGET choice")
    old_primary, = [item for item in original_manifest["options"] if item["id"] == "graph-target"]
    restored_manifest["options"].insert(original_manifest["options"].index(old_primary), copy.deepcopy(old_primary))
    targets.require(restored_manifest == original_manifest,
                    "Only the primary option-to-module migration and NEXT SUB choice may change the root manifest")
    audit = audit_package(staging, files)
    audit_shared_root(staging, catalog, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    previous_audit = read_json(source / "package-audit.json")
    targets.require(audit["options"] == previous_audit["options"] and audit["options"] <= 32
                    and not audit["aggregateViolations"], "NEXT must retain all unchanged selected and closure budgets")
    targets.require(all(sha(exact_file(source, path)) == digest for path, digest in hashes.items())
                    and sha(source / "generation.json") == identity["generationSha256"]
                    and sha(archive) == identity["archiveSha256"], "The immutable parent or its archive changed")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "pacemakerNext": {"official": {"url": targets.OFFICIAL_URL,
            "levels": [{"label": label, "numerator": numerator, "denominator": 18} for label, numerator in targets.LEVELS]},
            "omsRounding": targets.ROUNDING, "omsSelection": targets.SELECTION,
            "nativeVerification": {"integerRounding": "pending", "personalBestEquality": "pending",
                                   "missingPersonalBest": "pending"},
            "projectionSources": [targets.VALUE_SOURCE, targets.PROGRESS_SOURCE, targets.LABEL_SOURCE, targets.AVAILABLE_SOURCE],
            "authorTransformation": transformation,
            "choiceConfigurations": choice_paths,
            "unimplemented": "NEXT+ subdivisions and rotate switching rules remain unverified; no ghost or rival data"},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedExistingMedia": [], "originalModulesUnchanged": 11, "originalCatalogChoices": 1271,
            "deliveredModules": 12, "deliveredCatalogChoices": 1278, "previewFiles": 539},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
                    "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.output)


if __name__ == "__main__":
    main()
