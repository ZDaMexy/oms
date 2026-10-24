"""Append an author-side percentage graph target to a fresh Omni sibling.

Only the official numeric range and default are claimed. The supplied native
receipt establishes that 50 percent was selected; it does not verify other
values, the integer EX threshold, rotate behavior, or ghosts.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import uuid

from PIL import Image

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_percentage_target import OFFICIAL_URL, OPTION_ID, add_options, inject, require
from lighting_persistent import _independent

REFERENCE_CONTRACT = "oms-iidx-pacemaker-percentage-reference.v1"


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def _unique_fields(items):
    result = {}
    for key, value in items:
        require(key not in result, "A reviewed reference contains a duplicate JSON field")
        result[key] = value
    return result


def load_reference(path):
    path = _independent(path)
    reference = json.loads(path.read_text(encoding="utf-8"), object_pairs_hook=_unique_fields)
    require(isinstance(reference, dict) and reference.get("contract") == REFERENCE_CONTRACT and reference.get("status") == "reviewed",
            "Use a reviewed percentage reference")
    source = reference.get("source", {})
    require(isinstance(source, dict) and set(source) == {"manifestSha256", "generationSha256", "archiveSha256"}
            and all(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) for value in source.values()),
            "Pin the immutable parent manifest, generation and archive SHA-256")
    official = reference.get("official", {})
    require(official == {"url": OFFICIAL_URL, "min": 0, "max": 100, "step": 1, "default": 50}
            and all(type(official[key]) is int for key in ("min", "max", "step", "default")),
            "The official percentage range is integer 0..100, step1, default50")
    observed = reference.get("observed", {})
    require(isinstance(observed, dict) and type(observed.get("value")) is int and observed["value"] == 50,
            "This receipt establishes only the native selected 50 percent")
    image = observed.get("screenshot", {})
    require(isinstance(image, dict) and set(image) == {"path", "sha256"} and isinstance(image["path"], str)
            and isinstance(image["sha256"], str) and re.fullmatch(r"[0-9a-f]{64}", image["sha256"]),
            "Supply the actual native50 screenshot path and SHA-256")
    screenshot = Path(image["path"])
    screenshot = _independent(screenshot if screenshot.is_absolute() else path.parent / screenshot)
    require(screenshot.suffix.lower() in (".png", ".jpg", ".jpeg") and sha(screenshot) == image["sha256"],
            "The reviewed native50 screenshot changed")
    with Image.open(screenshot) as pixels:
        require(pixels.format in ("PNG", "JPEG"), "The native receipt must be a real PNG or JPEG")
        pixels.verify()
    require(isinstance(reference.get("review"), str) and reference["review"].strip(), "Describe the reviewed evidence boundary")
    return {"path": path, "sha256": sha(path), "source": source, "official": official,
            "observed": {"value": 50, "screenshot": {"path": str(screenshot), "sha256": image["sha256"]}},
            "review": reference["review"]}


def upgrade(source, reference_path, output):
    source, output = _independent(source), _independent(output, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists()
            and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and OSK")
    reference = load_reference(reference_path)
    archive = source.with_suffix(".osk")
    identity = {"manifestSha256": sha(source / "gameplay-skin.json"),
                "generationSha256": sha(source / "generation.json"), "archiveSha256": sha(archive)}
    require(identity == reference["source"], "The reviewed immutable parent changed")
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    original_manifest = read_json(source / "gameplay-skin.json")
    manifest = add_options(original_manifest)
    original_catalog = read_json(source / "gameplay/content.json")
    catalog = copy.deepcopy(original_catalog)
    frames = [module for module in catalog["modules"] if module["id"] == "frame"]
    require(len(frames) == 1 and len(frames[0]["choices"]) == 15
            and sum(len(module["choices"]) for module in catalog["modules"]) == 1272
            and len(preview_files(catalog)) == 540, "Use the complete current Omni author closure")
    all_ids = [option["id"] for option in manifest["options"]] + [module["id"] for module in catalog["modules"]]
    require(len(all_ids) == len(set(all_ids)) and len(all_ids) <= 32, "The new option must retain the shared32-option budget")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    transformed, receipts = {}, []
    for choice in frames[0]["choices"]:
        for part in (choice, *choice.get("variants", [])):
            key = (part["scene"], part["manifest"])
            if key not in transformed:
                scene, receipt = inject(read_json(exact_file(source, key[0])), read_json(exact_file(source, key[1])))
                transformed[key] = store_json(staging, "s", scene)
                receipts.append({"frame": choice["value"], "sourceScene": key[0], "sourceSceneSha256": hashes[key[0]],
                                 "sourceManifest": key[1], "newScene": transformed[key], **receipt})
            part["scene"] = transformed[key]
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay/content.json", catalog)
    restored = copy.deepcopy(catalog)
    for old_module, module in zip(original_catalog["modules"], restored["modules"], strict=True):
        if module["id"] == "frame":
            for old_choice, choice in zip(old_module["choices"], module["choices"], strict=True):
                for old_part, part in zip((old_choice, *old_choice.get("variants", [])),
                                          (choice, *choice.get("variants", [])), strict=True):
                    part["scene"] = old_part["scene"]
    require(restored == original_catalog, "An unrelated choice, scene, INI, manifest or preview changed")
    restored_manifest = copy.deepcopy(manifest)
    restored_manifest["options"].pop()
    next(option for option in restored_manifest["options"] if option["id"] == "graph-target")["choices"].pop()
    require(restored_manifest == original_manifest, "An existing option, default or root declaration changed")
    files = package_closure(staging)
    changed = {"gameplay-skin.json", "gameplay/content.json"}
    require(all(sha(exact_file(staging, relative)) == hashes[relative]
                for relative in set(source_files).intersection(files) - changed),
            "Existing source media, programs, font metrics or unrelated configuration changed")
    require(set(source_files) - set(files) <= {key[0] for key in transformed}, "An unrelated source dependency was retired")
    require(preview_files(catalog) == preview_files(original_catalog)
            and all(sha(exact_file(staging, relative)) == hashes[relative] for relative in preview_files(catalog)),
            "All540 original previews must remain byte-identical")
    require(all(sha(exact_file(source, relative)) == digest for relative, digest in hashes.items())
            and sha(source / "generation.json") == identity["generationSha256"]
            and sha(archive) == identity["archiveSha256"], "The immutable parent or its old archive changed")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    original_audit = read_json(source / "package-audit.json")
    require(audit["options"] == original_audit["options"] + 1 and not audit["aggregateViolations"],
            "The percentage setting must fit every unchanged selected and closure budget")
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "percentageReference": {"path": str(reference["path"]), "sha256": reference["sha256"],
            "official": reference["official"], "observed": reference["observed"], "review": reference["review"]},
        "percentageTarget": receipts, "option": OPTION_ID,
        "adaptation": "Author-side percentage of normalized maximum, original graph digits plus ordinary OMS percent suffix."
                      " No calculated integer EX threshold, original keypad workflow, rotate algorithm, ghost or rival data.",
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedExistingMedia": [], "catalogChoices": 1272, "previewFiles": 540,
            "retiredSceneFiles": sorted(set(source_files) - set(files))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
                    "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "reference", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.reference, args.output)


if __name__ == "__main__":
    main()
