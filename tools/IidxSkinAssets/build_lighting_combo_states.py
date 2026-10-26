"""Publish original COMBO palettes and BMS zero-combo labels in a fresh Omni author."""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_lighting_gameplay_controls import audit_shared_root
from build_lighting_score_graph_roles import verify_parent_archive
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
    package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_persistent import _independent
import lighting_combo_states as combo


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def upgrade(source, output, library, parent_sha256):
    source, library = _independent(source), _independent(library)
    output = _independent(output, output=True, exists=False)
    combo.require(output.parent == source.parent and not output.exists() and not output.with_suffix(".osk").exists(),
                  "Create a fresh sibling COMBO author and archive")
    archive = source.with_suffix(".osk")
    original_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in original_files}
    identity = {"generationSha256": sha(source / "generation.json"), "packageAuditSha256": sha(source / "package-audit.json"),
                "archiveSha256": verify_parent_archive(archive, parent_sha256, original_files, hashes)}
    generation = read_json(source / "generation.json")
    combo.require(generation["archive"]["sha256"] == identity["archiveSha256"] and "dynamicCover" in generation,
                  "Use the frozen complete dynamic-cover parent")
    original_catalog = read_json(source / "gameplay/content.json")
    combo.require(len(original_catalog["modules"]) == 13
                  and sum(len(module["choices"]) for module in original_catalog["modules"]) == 1285,
                  "Keep every existing art and functional choice")
    manifests = {"gameplay-skin.json": read_json(source / "gameplay-skin.json")}
    frame_manifests = set()
    judgement_parts = []
    for module in original_catalog["modules"]:
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                relative = part["manifest"]
                if relative not in manifests:
                    manifests[relative] = read_json(source / relative)
                if module["id"] == "frame":
                    frame_manifests.add(relative)
                elif module["id"] == "judgement":
                    judgement_parts.append((int(choice["value"][1:]), part))
    ini_contents = [exact_file(source, relative).read_text(encoding="utf-8-sig")
                    for relative in original_files if relative.endswith(".ini")]
    retained_ini_paths = {resource["path"] for manifest in manifests.values() for resource in manifest["resources"]
                          if any(resource["path"].removesuffix(".png") in content for content in ini_contents)}
    path_plan = combo.frame_path_plan(manifests, frame_manifests, hashes, preserved_paths=retained_ini_paths)
    library_index = exact_file(library, "library.json")
    index_identity = sha(library_index)
    index = read_json(library_index)
    combo.require(index["status"] == "complete" and index["sourceMetadataUnchanged"] is True,
                  "Use the complete immutable independent source library")
    arts = {style: combo.palette_art(library, index["textures"], style) for style in range(21)}
    transformations = {}
    branch_receipts = []
    for style, part in judgement_parts:
        key = (part["manifest"], part["scene"])
        if key in transformations:
            continue
        updated_manifest, updated_scene, receipt = combo.transform_contribution(manifests[part["manifest"]],
            read_json(source / part["scene"]), arts[style])
        transformations[key] = updated_manifest, updated_scene
        branch_receipts.append({"style": style, "sourceManifest": key[0], "sourceScene": key[1], **receipt})
    combo.require(len(transformations) == 42 and {row["style"] for row in branch_receipts} == set(range(21)),
                  "Upgrade exactly both frame geometries for all twenty-one Judgement styles")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in original_files:
        copy_exact(exact_file(source, relative), staging / relative)
    for old, relocation in path_plan.items():
        target = staging / relocation["path"]
        if target.exists():
            combo.require(sha(target) == relocation["sha256"], "A Frame short path conflicts with existing publication data")
        else:
            copy_exact(source / old, target)
        combo.require(sha(target) == hashes[old], "A relocated Frame PNG changed its complete original bytes")
    new_media = {}
    for art in arts.values():
        for relative, data in art["images"].items():
            expected = hashlib.sha256(data).hexdigest()
            combo.require(relative not in hashes or hashes[relative] == expected, "A new COMBO texture conflicts with parent media")
            target = staging / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists():
                combo.require(sha(target) == expected, "A COMBO media name conflicts with another source")
            else:
                target.write_bytes(data)
            new_media[relative] = expected
    catalog = copy.deepcopy(original_catalog)
    frame_paths, combo_paths = {}, {}
    for path in frame_manifests:
        updated = combo.apply_frame_paths(manifests[path], path_plan)
        frame_paths[path] = store_json(staging, "m", updated) if updated != manifests[path] else path
    for key, (manifest, scene) in transformations.items():
        combo_paths[key] = (store_json(staging, "m", manifest), store_json(staging, "s", scene))
    for module in catalog["modules"]:
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                if module["id"] == "frame":
                    part["manifest"] = frame_paths[part["manifest"]]
                elif module["id"] == "judgement":
                    part["manifest"], part["scene"] = combo_paths[(part["manifest"], part["scene"])]
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    new_configs = {*frame_paths.values(), *(path for pair in combo_paths.values() for path in pair)}
    combo.require(set(files) <= set(original_files) | new_configs | set(new_media) | {value["path"] for value in path_plan.values()},
                  "Only reviewed COMBO configuration/art and relocated original Frame PNGs may extend the closure")
    combo.require(preview_files(catalog) == preview_files(original_catalog) and len(preview_files(catalog)) == 539,
                  "Preserve all 539 preview bytes and selectors")
    for before, after in zip(original_catalog["modules"], catalog["modules"]):
        if before["id"] not in ("frame", "judgement"):
            combo.require(before == after, "An unrelated module changed")
    combo.require(all(sha(exact_file(staging, relative)) == hashes[relative]
                      for relative in set(original_files).intersection(files) if relative != "gameplay/content.json"),
                  "A retained parent document, original atlas, PNG, scene, INI or preview changed")
    for old, relocation in path_plan.items():
        combo.require(relocation["path"] in files and sha(staging / relocation["path"]) == hashes[old],
                      "A relocated original Frame texture was lost from the closure")
    audit = audit_package(staging, files)
    write_json(staging / "combo-budget-check.json", {"selectedUpperBound": audit["selectedUpperBound"],
        "configurationBytes": audit["configurationBytes"], "violations": audit["aggregateViolations"]})
    audit_shared_root(staging, catalog, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    combo.require(audit["options"] == 32 and audit["selectedUpperBound"]["fonts"] == 12 and not audit["aggregateViolations"],
                  "Keep the unchanged option/byte/texture/glyph budgets and the twelve actually consumed fonts")
    combo.require(sha(library_index) == index_identity and all(sha(item["source"]) == item["pngSha256"]
                  for art in arts.values() for item in art["proof"]["readInputs"]), "The original COMBO source library changed")
    combo.require(all(sha(source / relative) == expected for relative, expected in hashes.items())
                  and sha(source / "generation.json") == identity["generationSha256"]
                  and sha(source / "package-audit.json") == identity["packageAuditSha256"] and sha(archive) == identity["archiveSha256"],
                  "The immutable parent author, audit, receipt or archive changed")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "comboStates": {"contract": combo.CONTRACT, "sourceLibrary": str(library),
            "sourceLibraryIndexSha256": index_identity, "styles": [art["proof"] for art in arts.values()],
            "contributions": branch_receipts, "fontsAddedPerSelection": 2, "optionsAdded": 0,
            "phasePolicy": "Explicit OMS author rhythm: blue+b, white+g, pink+r; 100ms each, 300ms loop using existing judgement.hit origin",
            "notClaimed": ["Original game's exact PGREAT period or initial phase", "Yellow GREAT as a PGREAT phase"],
            "newTextureFiles": new_media, "framePathRelocations": path_plan,
            "iniReferencedTexturePathsRetained": len(retained_ini_paths),
            "framePathPolicy": "Only Frame-exclusive PNG paths; resource IDs and all source PNG bytes preserved; six-hex prefix checked against complete SHA256 of every occupied package path"},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "parentArchiveMatchesSourceClosure": True, "changedExistingMedia": [], "previewFiles": 539,
            "unrelatedModulesUnchanged": 11, "frameScenesAndAllIniByteExact": True},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    print({"author": str(output), "judgementScenes": len(transformations), "newTextures": len(new_media),
           "relocatedFramePaths": len(path_plan), "budgets": receipt["budgets"], "closure": receipt["closure"]})
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--parent-sha256", required=True)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.library, args.parent_sha256)


if __name__ == "__main__":
    main()
