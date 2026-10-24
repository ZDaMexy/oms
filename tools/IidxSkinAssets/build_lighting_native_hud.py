"""Place OMS's real Mod display outside Omni's score readouts in a fresh private author.

This is an OMS layout adaptation, not a reconstruction of an IIDX Mod display.
Only author-layout modsRect fields change; no scene, input, media or option does.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_persistent import _independent


# Existing native ModDisplay is fitted into these safe-screen rectangles. SP uses
# the middle cabinet area; DP uses the left cabinet header above its BGA windows.
RECTANGLES = {
    "p1": [0.31, 0.105, 0.39, 0.065],
    "p2": [0.31, 0.105, 0.39, 0.065],
    "dp": [0.015, 0.15, 0.18, 0.065],
}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def place_native_mods(manifest):
    """Keep every established native profile and add only its declared overlay rectangle."""
    result = copy.deepcopy(manifest)
    profiles = result.get("layouts", [])
    require(profiles, "The selected Frame manifest must own its exact native layouts")
    receipts = []
    for profile in profiles:
        require("modsRect" not in profile and profile["ruleset"] in ("bms", "mania"),
                "Do not overwrite an existing native Mod display placement")
        if len(profile["stages"]) == 2:
            layout = "dp"
        else:
            require(len(profile["stages"]) == 1 and profile["presentation"] in ("p1", "p2"),
                    "Select the known single/dual Omni profile without inventing a native topology")
            layout = profile["presentation"]
        profile["modsRect"] = list(RECTANGLES[layout])
        receipts.append({"id": profile["id"], "ruleset": profile["ruleset"], "keymode": profile["keymode"],
                         "presentation": profile["presentation"], "layout": layout, "modsRect": profile["modsRect"]})
    restored = copy.deepcopy(result)
    for profile in restored["layouts"]:
        del profile["modsRect"]
    require(restored == manifest, "Native Mod placement must preserve all original layout geometry and resources")
    return result, receipts


def upgrade(source, output):
    source, output = _independent(source), _independent(output, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists()
            and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and OSK")
    archive = source.with_suffix(".osk")
    identity = {"manifestSha256": sha(source / "gameplay-skin.json"),
                "generationSha256": sha(source / "generation.json"), "archiveSha256": sha(archive)}
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    manifest = read_json(source / "gameplay-skin.json")
    original_catalog = read_json(source / "gameplay/content.json")
    catalog = copy.deepcopy(original_catalog)
    frame_modules = [module for module in catalog["modules"] if module["id"] == "frame"]
    require(len(frame_modules) == 1 and len(frame_modules[0]["choices"]) == 15
            and (sum(len(module["choices"]) for module in catalog["modules"]),
                 len(preview_files(catalog))) in {(1272, 540), (1271, 539)}
            and any(option["id"] == "lane-objects-brightness" for option in manifest["options"]),
            "Use a complete shared-brightness Omni author with every original choice and preview")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(source / relative, staging / relative)
    transformed, receipts = {}, []
    for choice in frame_modules[0]["choices"]:
        for part in (choice, *choice.get("variants", [])):
            previous = part["manifest"]
            if previous not in transformed:
                updated, profiles = place_native_mods(read_json(exact_file(source, previous)))
                transformed[previous] = store_json(staging, "m", updated)
                receipts.append({"sourceManifest": previous, "sourceSha256": hashes[previous],
                                 "newManifest": transformed[previous], "profiles": profiles})
            part["manifest"] = transformed[previous]
    write_json(staging / "gameplay/content.json", catalog)
    restored = copy.deepcopy(catalog)
    for original_module, module in zip(original_catalog["modules"], restored["modules"]):
        if module["id"] == "frame":
            for original_choice, choice in zip(original_module["choices"], module["choices"]):
                for original_part, part in zip((original_choice, *original_choice.get("variants", [])),
                                               (choice, *choice.get("variants", []))):
                    part["manifest"] = original_part["manifest"]
    require(restored == original_catalog and read_json(staging / "gameplay-skin.json") == manifest,
            "A native overlay adaptation cannot change an option, default, choice, scene or INI")
    files = package_closure(staging)
    require(all(sha(exact_file(staging, relative)) == hashes[relative]
                for relative in set(source_files).intersection(files) if relative != "gameplay/content.json"),
            "An existing source media or configuration changed")
    require(set(source_files) - set(files) <= set(transformed), "An unrelated source file was retired")
    require(preview_files(catalog) == preview_files(original_catalog)
            and all(sha(exact_file(staging, relative)) == hashes[relative] for relative in set(preview_files(catalog))),
            "Every original preview must stay byte-identical")
    require(all(sha(exact_file(source, relative)) == digest for relative, digest in hashes.items())
            and sha(source / "generation.json") == identity["generationSha256"]
            and sha(archive) == identity["archiveSha256"], "The preserved parent or old archive changed")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    require(not audit["aggregateViolations"], "Native overlay placement exceeds an existing package budget")
    write_json(staging / "package-audit.json", audit)
    receipt = {
        "name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "nativeModsPlacement": receipts,
        "adaptation": "OMS author layout moves/fits the existing real ModDisplay. Not an IIDX art or gameplay reconstruction;"
                      " current Mods, replay lifecycle, hover and HUD visibility remain native. No new visual, texture or framebuffer.",
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
                               "changedExistingMedia": [],
                               "catalogChoices": sum(len(module["choices"]) for module in catalog["modules"]),
                               "previewFiles": len(preview_files(catalog))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
                    "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"],
    }
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
