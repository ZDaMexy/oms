"""Upgrade existing hx text consumers to original33 art in a fresh gameplay sibling.

Never rebuild pictures, update an old author, pack, or claim codec/native acceptance.
"""
from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import shutil
import sys
import uuid

sys.dont_write_bytecode = True

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, exact_file,
                            package_closure, preview_files, read_json)
from lighting_header import ART_NAMES, identifiers, original_art, require, reverse_header_art, upgrade_header
from lighting_persistent import _independent


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes((json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n").encode("utf-8"))


def inventory(root, files):
    return [{"path": path, "bytes": exact_file(root, path).stat().st_size, "sha256": sha(exact_file(root, path))}
            for path in sorted(files)]


def all_files(root):
    result = []
    for path in root.rglob("*"):
        require(not path.is_symlink() and path.resolve().is_relative_to(root), "Do not follow an author link")
        if path.is_file(): result.append(path.relative_to(root).as_posix())
    return sorted(result)


def identity(rows):
    return hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":"), ensure_ascii=False).encode()).hexdigest()


def read_art_inventory(path):
    """Validate the explicit private name-to-SHA256 input once at the boundary."""
    def unique_names(pairs):
        value = dict(pairs)
        require(len(value) == len(pairs), "A private art inventory contains duplicate names")
        return value
    value = json.loads(Path(path).read_text(encoding="utf-8"), object_pairs_hook=unique_names)
    require(isinstance(value, dict) and set(value) == set(ART_NAMES), "Declare exactly the eighteen expected original art names")
    require(all(isinstance(digest, str) and re.fullmatch(r"[0-9a-fA-F]{64}", digest) for digest in value.values()),
            "Each private art inventory value must be a 64-hex SHA256 digest")
    return {name: digest.lower() for name, digest in value.items()}


def copy_header_art(art, output):
    """Copy complete identified source bytes once; no image encoder is called."""
    for row in art["sources"]:
        source, destination = Path(row["source"]), output / row["path"]
        require(not destination.exists() and destination.resolve().is_relative_to(output.resolve()), "Never overwrite an art path")
        require(sha(source) == row["sha256"], "Original header PNG changed before copy")
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, destination)
        require(sha(destination) == row["sha256"] and destination.read_bytes() == source.read_bytes(), "Original PNG bytes were not preserved")


def upgrade_manifest(manifest, art):
    require(len(art["resources"]) == 18, "Declare exactly the complete original eighteen textures")
    result = copy.deepcopy(manifest)
    ids = {resource["id"] for resource in result["resources"]}
    paths = {resource["path"] for resource in result["resources"]}
    require(not ids.intersection(resource["id"] for resource in art["resources"])
            and not paths.intersection(resource["path"] for resource in art["resources"]), "This manifest already declares header art")
    result["resources"].extend(copy.deepcopy(art["resources"]))
    restored = copy.deepcopy(result)
    del restored["resources"][-len(art["resources"]):]
    require(restored == manifest, "An existing manifest field changed")
    return result


def rewrite_33_catalog(catalog, replacements):
    result = copy.deepcopy(catalog)
    choices = [choice for module in result["modules"] if module["id"] == "frame"
               for choice in module["choices"] if choice["value"] == "iidx33"]
    require(len(choices) == 1, "Use exactly one original33 choice")
    for part in (choices[0], *choices[0].get("variants", [])):
        key = (part["scene"], part["manifest"])
        require(key in replacements, "A33 branch has no exact replacement")
        part["scene"], part["manifest"] = replacements[key]
    reverse = {value: key for key, value in replacements.items()}
    require(len(reverse) == len(replacements), "Distinct source33 branches unexpectedly collapsed")
    restored = copy.deepcopy(result)
    for choice in next(module for module in restored["modules"] if module["id"] == "frame")["choices"]:
        if choice["value"] != "iidx33": continue
        for part in (choice, *choice.get("variants", [])):
            part["scene"], part["manifest"] = reverse[(part["scene"], part["manifest"])]
    require(restored == catalog, "A non33 field, option, preview or branch changed")
    return result


def upgrade(source, output, reference, evidence, sprites, art_inventory, *, expected_source_sha256=None):
    source, reference, sprites, art_inventory = (_independent(path) for path in (source, reference, sprites, art_inventory))
    output = _independent(output, output=True, exists=False)
    evidence = _independent(evidence, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists()
            and not output.with_suffix(".osk").exists(), "Create a strictly new sibling author")
    require(not evidence.exists() and evidence.parent.is_dir() and not evidence.is_relative_to(source)
            and not evidence.is_relative_to(output), "Use a separate fresh evidence directory")
    require(read_json(source / "generation.json")["name"] == NAME, "Preserve the original work name")
    require(not (source / "skin-pages.json").exists(), "Use the gameplay-only lineage; do not restore excluded pages")
    runtime_before, files = package_closure(source), all_files(source)
    reports = {"generation.json", "package-audit.json"}
    require(set(files) == set(runtime_before) | reports, "Do not silently omit unknown parent payload files")
    before = inventory(source, files)
    source_identity = identity(before)
    require(expected_source_sha256 is None or source_identity == expected_source_sha256, "Frozen parent identity differs")
    source_metadata = {str(path): sha(path) for path in (source / "generation.json", source / "package-audit.json", reference, art_inventory)}
    if source.with_suffix(".osk").is_file(): source_metadata[str(source.with_suffix(".osk"))] = sha(source.with_suffix(".osk"))
    art = original_art(sprites, read_art_inventory(art_inventory))
    require(not {row["path"] for row in art["sources"]}.intersection(files), "Never overwrite a parent art path")
    mapping, original_catalog = read_json(reference), read_json(source / "gameplay/content.json")
    frame = next(module for module in original_catalog["modules"] if module["id"] == "frame")
    require(len(frame["choices"]) == 15, "Preserve all fifteen Frames")
    choice = next(choice for choice in frame["choices"] if choice["value"] == "iidx33")
    occupied = set()
    for relative in runtime_before:
        if relative.endswith(".json"): occupied.update(identifiers(read_json(exact_file(source, relative))))
    require(not occupied.intersection(resource["id"] for resource in art["resources"]), "Art resource IDs collide with the parent domain")
    plans = {}
    for part in (choice, *choice.get("variants", [])):
        key = (part["scene"], part["manifest"])
        layout = part.get("when", {}).get("layout", "dp")
        if key in plans:
            require(plans[key]["layout"] == layout, "A source scene is shared by incompatible layouts")
            continue
        manifest = read_json(exact_file(source, part["manifest"]))
        rectangles = {tuple(profile["modsRect"]) for profile in manifest["layouts"] if profile["ruleset"] == "bms"}
        require(len(rectangles) == 1, "Use the actual BMS profiles' native Mod rectangle")
        scene = read_json(exact_file(source, part["scene"]))
        updated, receipt = upgrade_header(scene, mapping, layout, "hx." + layout, "hr." + layout, occupied, art,
                                          mods_rect=next(iter(rectangles)))
        require(reverse_header_art(updated, receipt) == scene, "The existing typed scene is not reversible")
        occupied.update(receipt["addedIds"])
        plans[key] = {"layout": layout, "scene": updated, "manifest": upgrade_manifest(manifest, art), "receipt": receipt}
    require(len(plans) == 3 and {plan["layout"] for plan in plans.values()} == {"p1", "p2", "dp"}, "Preserve exactly three33 branches")
    other_refs = {part.get(field) for module in original_catalog["modules"] for item in module["choices"]
        if module["id"] != "frame" or item["value"] != "iidx33" for part in (item, *item.get("variants", [])) for field in ("manifest", "scene")}
    require(not {path for key in plans for path in key}.intersection(other_refs), "A replaced33 file also serves another consumer")

    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    require(not staging.exists(), "Never reuse a preparation directory")
    evidence.mkdir()
    (evidence / "parent-reports").mkdir()
    for report in sorted(reports):
        shutil.copyfile(source / report, evidence / "parent-reports" / report)
        require(sha(evidence / "parent-reports" / report) == sha(source / report), "A preserved parent report changed")
    staging.mkdir()
    write_json(evidence / "parent-payload-sha256.json", before)
    for relative in files:
        destination = staging / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, relative), destination)
    copy_header_art(art, staging)
    replacements, changes = {}, []
    for (old_scene, old_manifest), plan in plans.items():
        new_scene, new_manifest = store_json(staging, "s", plan["scene"]), store_json(staging, "m", plan["manifest"])
        replacements[(old_scene, old_manifest)] = (new_scene, new_manifest)
        changes.append({**plan["receipt"], "previousScene": old_scene, "scene": new_scene,
            "previousManifest": old_manifest, "manifest": new_manifest,
            "previousSceneSha256": sha(source / old_scene), "sceneSha256": sha(staging / new_scene),
            "previousManifestSha256": sha(source / old_manifest), "manifestSha256": sha(staging / new_manifest),
            "sceneByteIncrement": (staging / new_scene).stat().st_size - (source / old_scene).stat().st_size,
            "manifestByteIncrement": (staging / new_manifest).stat().st_size - (source / old_manifest).stat().st_size})
    catalog = rewrite_33_catalog(original_catalog, replacements)
    require(preview_files(original_catalog) == preview_files(catalog) and len(preview_files(catalog)) == 539, "Preserve every original preview")
    write_json(staging / "gameplay/content.json", catalog)
    runtime = package_closure(staging)
    retired = sorted(set(runtime_before) - set(runtime))
    require(set(retired) == {path for key in plans for path in key}, "Only three replaced scenes and manifests may retire")
    for relative in retired: exact_file(staging, relative).unlink()
    unchanged = sorted(set(runtime_before).intersection(runtime) - {"gameplay/content.json"})
    require(inventory(source, unchanged) == inventory(staging, unchanged), "An old PNG, INI, preview, other Frame or module changed")
    require(before == inventory(source, files) and all(sha(Path(path)) == digest for path, digest in source_metadata.items())
            and all(sha(Path(row["source"])) == row["sha256"] for row in art["sources"]), "A frozen source or original PNG changed")
    print("Computing conservative bounds; shared codec/full CLI/native validation remain pending", flush=True)
    audit = audit_package(staging, runtime)
    require(not audit["aggregateViolations"], "Do not relax any budget")
    audit["authorDirectory"] = author_directory_budget(runtime)
    runtime_rows = inventory(staging, runtime)
    preservation = {"parentPayloadSha256": source_identity, "parentPostPayloadSha256": identity(inventory(source, files)),
        "unchangedFiles": len(unchanged), "unchangedSha256": identity(inventory(staging, unchanged)),
        "allParentPngIniAnd539PreviewsByteIdentical": True, "allFourteenOtherFramesAndEveryOtherModuleUnchanged": True,
        "originalTypedSceneAndManifestReversalProved": True, "allOriginalHxIdsTextBindingsAndGeometryUnchanged": True,
        "v42OpaqueShortcutMasksPreserved": 135, "v43AutoUnknownGatePreserved": True,
        "originalPngsDirectCopied": len(art["sources"]), "originalEncodedBytes": sum(row["bytes"] for row in art["sources"]),
        "additionalSelectedTexturePixels": sum(row["width"] * row["height"] for row in art["sources"]),
        "newResourcesPer33Manifest": len(art["resources"]), "newFonts": 0, "newOptions": 0, "newDataSources": 0,
        "sourceAuthorArchiveReferenceAndArtUnchanged": True}
    receipt = {"name": NAME, "status": "prepared-original33-header-awaiting-shared-codec-full-cli-and-native",
        "createdUtc": datetime.now(timezone.utc).isoformat(), "repackagedFrom": str(source),
        "sourceMetadataSha256": source_metadata, "artInventory": {"path": str(art_inventory), "sha256": source_metadata[str(art_inventory)]},
        "headerConsumers": changes, "sourceArt": art["sources"],
        "scope": "Only existing iidx33 P1/P2/DP hx consumers gain exact original-image branches; unknown raw text and BMS scope remain.",
        "sourcePreservation": preservation,
        "closure": {"definition": "Referenced runtime files; the two new nonruntime reports are in the external full output inventory",
                    "files": len(runtime_rows), "uncompressedBytes": sum(row["bytes"] for row in runtime_rows), "sha256": identity(runtime_rows)},
        "selectedUpperBound": audit["selectedUpperBound"], "authorDirectory": audit["authorDirectory"],
        "runtimeBudget": {"additionalReservedTextGlyphs": 0, "additionalNodesPerSelected33Frame": 4, "additionalVariants": 6,
                          "additionalCases": 51, "sharedCodecFullCliNative": "pending; Python bounds are conservative only"},
        "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk; next unused Asia/Shanghai sequence"}}
    write_json(staging / "generation.json", receipt)
    write_json(staging / "package-audit.json", audit)
    after = inventory(staging, all_files(staging))
    write_json(evidence / "source-preservation.json", preservation)
    write_json(evidence / "source-hooks-and-art.json", {"reference": str(reference), "referenceSha256": sha(reference),
        "artInventory": receipt["artInventory"], "art": art["sources"], "consumers": changes})
    write_json(evidence / "payload-diff.json", {"changed": ["gameplay/content.json", *sorted(reports)], "retired": retired,
        "added": sorted(set(row["path"] for row in after) - set(files)), "runtimeReplacements": changes,
        "oldReportsPreservedWithSha256": {name: sha(evidence / "parent-reports" / name) for name in sorted(reports)}})
    write_json(evidence / "runtime-payload-sha256.json", runtime_rows)
    write_json(evidence / "output-payload-sha256.json", after)
    write_json(evidence / "generation.json", {**receipt, "payloadSha256": identity(after), "fullFiles": len(after)})
    require(before == inventory(source, files) and all(sha(Path(path)) == digest for path, digest in source_metadata.items())
            and all(sha(Path(row["source"])) == row["sha256"] for row in art["sources"]), "SourcePost mismatch")
    staging.rename(output)
    print(json.dumps({"author": str(output), "payloadSha256": identity(after), "files": len(after),
        "selectedUpperBound": audit["selectedUpperBound"], "validation": "pending shared codec/full CLI/native; no OSK created"}, separators=(",", ":")), flush=True)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "output", "reference", "evidence", "sprites", "art-inventory"):
        parser.add_argument("--" + name, type=Path, required=True)
    parser.add_argument("--expected-source-sha256")
    args = parser.parse_args()
    upgrade(args.source, args.output, args.reference, args.evidence, args.sprites, args.art_inventory,
            expected_source_sha256=args.expected_source_sha256)


if __name__ == "__main__":
    main()
