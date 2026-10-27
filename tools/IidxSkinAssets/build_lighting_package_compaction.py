"""Prepare a fresh, reversible compact author; never validate or create an OSK.

The parent author/archive and every media byte remain read-only. Current shared
package/codec/native gates must run separately after this tool has frozen output.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
import uuid
import zipfile

from build_omni_skin import NAME, author_directory_budget, exact_file, package_closure
import lighting_package_compaction as compact

ROOT_MANIFEST = "gameplay-skin.json"
ROOT_SCENE = "gameplay-skin.scene.json"


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def independent(path, *, exists):
    result = Path(path).resolve(strict=exists)
    compact.require(not any(part.casefold() == "private-data" for part in result.parts)
                    and result.drive.casefold() not in {"c:", "d:"},
                    "Use independent non-system source/evidence paths; protected data and the original HDD are excluded")
    return result


def destinations(source, output, evidence):
    source, output, evidence = independent(source, exists=True), independent(output, exists=False), independent(evidence, exists=False)
    compact.require(source.is_dir() and output.parent == source.parent and output != source
                    and not output.exists() and not output.with_suffix(".osk").exists(),
                    "Create a wholly new sibling author; an existing author or OSK cannot be overwritten")
    compact.require(not evidence.exists() and not evidence.is_relative_to(source.parent)
                    and not source.parent.is_relative_to(evidence),
                    "Create wholly new evidence outside the immutable author tree")
    compact.require(output.parent.is_dir() and evidence.parent.is_dir(), "The designated authors and evidence parent directories must already exist")
    return source, output, evidence


def verify_parent(source, expected_sha256, closure, hashes):
    compact.require(isinstance(expected_sha256, str) and re.fullmatch(r"[0-9a-f]{64}", expected_sha256),
                    "Supply the explicitly frozen parent archive SHA256")
    archive = source.with_suffix(".osk")
    digest = sha(archive)
    compact.require(digest == expected_sha256, "The parent OSK differs from its explicit frozen identity")
    generation = read_json(source / "generation.json")
    compact.require(generation["name"] == NAME and generation["archive"]["sha256"] == digest,
                    "The parent generation receipt and frozen archive disagree")
    with zipfile.ZipFile(archive) as package:
        compact.require(sorted(package.namelist()) == closure, "The frozen parent archive differs from the complete author reference closure")
        for relative in closure:
            with package.open(relative) as stream:
                compact.require(hashlib.file_digest(stream, "sha256").hexdigest() == hashes[relative],
                                "A parent author file differs from its frozen archive: " + relative)
    return {"author": str(source), "archive": str(archive), "archiveBytes": archive.stat().st_size,
            "archiveSha256": digest, "generationSha256": sha(source / "generation.json"),
            "packageAuditSha256": sha(source / "package-audit.json"), "allArchiveEntriesMatchSourceClosure": True}


def json_plan(documents, mapping, *, scene):
    transformed, paths, data, receipts = {}, {}, {}, []
    inverse = {new: old for old, new in mapping.items()}
    for old_path, original in sorted(documents.items()):
        if scene:
            declarations, _ = compact.identity_slots(original)
            local = {item["id"]: mapping[item["id"]] for item in declarations if item["id"] in mapping}
            changed = compact.rewrite_scene(original, local)
            restored = compact.rewrite_scene(changed, {new: old for old, new in local.items()})
        else:
            local = {item["path"]: mapping[item["path"]] for item in original.get("resources", []) if item["path"] in mapping}
            changed = compact.rewrite_manifest(original, local)
            restored = compact.rewrite_manifest(changed, inverse)
        compact.require(compact.json_bytes(restored) == compact.json_bytes(original),
                        "Inverse typed-ID/resource-path mapping did not restore the entire JSON: " + old_path)
        encoded = compact.json_bytes(changed)
        modified = compact.json_bytes(changed) != compact.json_bytes(original)
        if old_path in {ROOT_MANIFEST, ROOT_SCENE} or not modified:
            new_path = old_path
        else:
            new_path = "config/" + ("s" if scene else "m") + hashlib.sha256(encoded).hexdigest() + ".json"
        compact.require(new_path not in transformed or compact.json_bytes(transformed[new_path]) == encoded,
                        "A content-addressed configuration path has conflicting JSON")
        transformed[new_path], paths[old_path] = changed, new_path
        if modified:
            data[old_path] = encoded
        receipts.append({"source": old_path, "destination": new_path, "changed": modified,
                         "typedAliases" if scene else "textureAliases": len(local),
                         "originalCanonicalSha256": compact.canonical_sha(original),
                         "restoredCanonicalSha256": compact.canonical_sha(restored),
                         "restoredNormalizedJsonBytesEqual": True})
    return transformed, paths, data, receipts


def file_plan(closure, hashes, sizes, scene_paths, manifest_paths, texture_paths, encoded, catalog_path, catalog):
    paths, entries, occupied = {}, {}, {}
    for old in closure:
        new = scene_paths.get(old, manifest_paths.get(old, texture_paths.get(old, old)))
        content = compact.json_bytes(catalog) if old == catalog_path else encoded.get(old)
        digest = hashlib.sha256(content).hexdigest() if content is not None else hashes[old]
        size = len(content) if content is not None else sizes[old]
        folded = compact.normal_path(new)
        compact.require(folded not in occupied or occupied[folded] == new, "Output paths collide after case folding")
        occupied[folded] = new
        compact.require(new not in entries or entries[new]["destinationSha256"] == digest, "Two source files conflict at one output path")
        role = ("typed-scene-json" if old in scene_paths else "resource-path-manifest-json" if old in manifest_paths else
                "catalog-configuration-pointers" if old == catalog_path else "texture-alias" if old in texture_paths else "exact-copy")
        row = {"source": old, "destination": new, "role": role,
               "sourceBytes": sizes[old], "sourceSha256": hashes[old],
               "destinationBytes": size, "destinationSha256": digest,
               "bytesUnchanged": digest == hashes[old] and size == sizes[old]}
        paths[old] = row
        if new not in entries:
            entries[new] = {**row, "content": content}
    return paths, entries


def write_new_json(path, value):
    with Path(path).open("xb") as stream:
        stream.write(compact.json_bytes(value))


def prepare(source, output, evidence, parent_sha256):
    source, output, evidence = destinations(source, output, evidence)
    closure = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in closure}
    sizes = {relative: exact_file(source, relative).stat().st_size for relative in closure}
    parent = verify_parent(source, parent_sha256, closure, hashes)
    # Preserve every physical source file, including any historical local metadata.
    source_files = sorted(path.relative_to(source).as_posix() for path in source.rglob("*") if path.is_file())
    source_snapshot = {relative: {"sha256": hashes.get(relative) or sha(exact_file(source, relative)),
                                  "bytes": exact_file(source, relative).stat().st_size} for relative in source_files}
    root = read_json(source / ROOT_MANIFEST)
    compact.require(root["scene"] == ROOT_SCENE and root["contentCatalog"] == "gameplay/content.json",
                    "Use the frozen shared-root/C2 layout without changing its public paths")
    catalog_path, catalog = root["contentCatalog"], read_json(source / root["contentCatalog"])
    contributions = list(compact.parts(catalog))
    scene_files = {ROOT_SCENE} | {part["scene"] for _, _, _, part in contributions if part.get("scene")}
    manifest_files = {ROOT_MANIFEST} | {part["manifest"] for _, _, _, part in contributions if part.get("manifest")}
    compact.require(not scene_files & manifest_files and scene_files | manifest_files <= set(closure),
                    "Typed configuration paths overlap or escape the reference closure")
    scenes = {path: read_json(source / path) for path in scene_files}
    manifests = {path: read_json(source / path) for path in manifest_files}
    ini_files = [relative for relative in closure if relative.endswith(".ini")]
    ini_contents = [(source / relative).read_text(encoding="utf-8-sig") for relative in ini_files]
    identities, identity_receipt = compact.plan_identity_aliases(scenes, manifests, catalog, ini_contents)
    textures, texture_receipt = compact.plan_texture_aliases(manifests, scenes, catalog, ini_contents, closure)
    new_scenes, scene_paths, scene_data, scene_proofs = json_plan(scenes, identities, scene=True)
    new_manifests, manifest_paths, manifest_data, manifest_proofs = json_plan(manifests, textures, scene=False)
    new_catalog = compact.rewrite_catalog(catalog, scene_paths, manifest_paths)
    restored_catalog = compact.restore_catalog(new_catalog, catalog, scene_paths, manifest_paths)
    encoded = {**scene_data, **manifest_data}
    paths, entries = file_plan(closure, hashes, sizes, scene_paths, manifest_paths, textures, encoded, catalog_path, new_catalog)
    projected_sizes = {relative: entry["destinationBytes"] for relative, entry in entries.items()}
    before = compact.selected_projection(catalog, manifests, scenes, sizes)
    after = compact.selected_projection(new_catalog, new_manifests, new_scenes, projected_sizes)
    parent_audit = read_json(source / "package-audit.json")
    for key, limit in before["limits"].items():
        compact.require(before["selectedUpperBound"][key] == parent_audit["selectedUpperBound"][key],
                        "The metadata-only projection does not reproduce the frozen parent budget: " + key)
        compact.require(after["selectedUpperBound"][key] <= limit, "Compaction exceeds an unchanged public byte budget: " + key)
    compact.require(len(root.get("options", [])) + len(catalog["modules"]) <= 32, "The parent already exceeds the unified option budget")
    directory_budget = author_directory_budget(sorted(entries))
    previews = set(texture_receipt["previews"])
    selectors = {value.replace("\\", "/").casefold() for value in
                 texture_receipt["materialSelectors"] + texture_receipt["iniProvides"]}
    markers = {relative for relative in closure if relative.casefold().startswith(("markers/", "custom/markers/"))
               or relative.casefold() in selectors or str(Path(relative).with_suffix("")).replace("\\", "/").casefold() in selectors}
    immutable = set(ini_files) | previews | markers
    for relative in immutable:
        compact.require(relative in paths and paths[relative]["destination"] == relative and paths[relative]["bytesUnchanged"],
                        "An INI, preview or public material-marker file was changed: " + relative)
    texture_files = {resource["path"] for manifest in manifests.values() for resource in manifest.get("resources", [])}
    animation_files = {item["path"] for manifest in manifests.values() for item in manifest.get("animations", [])}
    compact.require(all(paths[relative]["bytesUnchanged"] for relative in texture_files | animation_files),
                    "Compaction must not rebuild any image or recorded animation")
    projection = {"contract": "oms-iidx-author-compaction-budget-projection.v1", "before": before, "after": after,
                  "savedBytes": {key: before["selectedUpperBound"][key] - after["selectedUpperBound"][key] for key in before["limits"]},
                  "remainingBytes": {key: limit - after["selectedUpperBound"][key] for key, limit in before["limits"].items()},
                  "unchangedParentMetrics": {key: value for key, value in parent_audit["selectedUpperBound"].items()
                                             if key not in {"sceneBytes", "rawSceneBytes", "manifestBytes"}},
                  "unchangedMetricsBasis": "Full JSON inverse mappings and byte-identical media, not a new image/codec/runtime audit",
                  "authorDirectoryIncludingFuturePackageAudit": directory_budget}
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    compact.require(staging.parent == source.parent and not staging.exists(), "A new isolated preparation stage is required")
    staging.mkdir()
    evidence.mkdir()
    print(json.dumps({"status": "copying-exact-reference-closure", "staging": str(staging), "evidence": str(evidence)}, ensure_ascii=False), flush=True)
    for relative, entry in sorted(entries.items()):
        destination = staging / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        if entry["content"] is not None:
            with destination.open("xb") as stream:
                stream.write(entry["content"])
        else:
            shutil.copyfile(exact_file(source, entry["source"]), destination)
        compact.require(sha(destination) == entry["destinationSha256"] and destination.stat().st_size == entry["destinationBytes"],
                        "A prepared output file differs from its complete SHA256 plan: " + relative)
    expected_closure = sorted(entries)
    compact.require(package_closure(staging) == expected_closure, "The compact author has stale files or missing reference dependencies")
    actual_files = sorted(path.relative_to(staging).as_posix() for path in staging.rglob("*") if path.is_file())
    compact.require(actual_files == expected_closure, "The preparation tree is not exactly its reference closure")
    # Re-read the actual written JSON instead of accepting only in-memory proofs.
    inverse_ids, inverse_textures = {new: old for old, new in identities.items()}, {new: old for old, new in textures.items()}
    for old_path, original in scenes.items():
        actual = read_json(staging / scene_paths[old_path])
        declarations, _ = compact.identity_slots(actual)
        local = {item["id"]: inverse_ids[item["id"]] for item in declarations if item["id"] in inverse_ids}
        compact.require(compact.json_bytes(compact.rewrite_scene(actual, local)) == compact.json_bytes(original),
                        "Written scene JSON fails the complete inverse mapping: " + old_path)
    for old_path, original in manifests.items():
        compact.require(compact.json_bytes(compact.rewrite_manifest(read_json(staging / manifest_paths[old_path]), inverse_textures)) == compact.json_bytes(original),
                        "Written manifest JSON fails the complete inverse mapping: " + old_path)
    compact.restore_catalog(read_json(staging / catalog_path), catalog, scene_paths, manifest_paths)
    for relative, row in source_snapshot.items():
        compact.require(sha(exact_file(source, relative)) == row["sha256"], "An immutable parent file changed during preparation: " + relative)
    compact.require(sorted(path.relative_to(source).as_posix() for path in source.rglob("*") if path.is_file()) == source_files,
                    "The immutable parent file inventory changed during preparation")
    compact.require(sha(source.with_suffix(".osk")) == parent["archiveSha256"], "The immutable parent archive changed during preparation")
    proof = {"contract": "oms-iidx-author-compaction-proof.v1", "status": "prepared-current-package-codec-native-gates-pending",
             "parent": parent, "output": str(output), "sourceFilesUnchanged": len(source_snapshot),
             "sceneDocumentsReversed": len(scene_proofs), "manifestDocumentsReversed": len(manifest_proofs),
             "writtenJsonReReadAndReversed": True, "sceneTypedIds": identity_receipt,
             "texturePaths": {key: value for key, value in texture_receipt.items() if key not in {"excludedPaths", "previews", "materialSelectors", "iniProvides"}},
             "preserved": {"textureFiles": len(texture_files), "recordedAnimationFiles": len(animation_files),
                           "iniFiles": sorted(ini_files), "previewFiles": sorted(previews), "c4MarkerFiles": sorted(markers),
                           "allMediaFullSha256Equal": True, "allIniPreviewMarkerBytesAndPathsEqual": True,
                           "optionsBeforeAndAfter": len(root.get("options", [])) + len(catalog["modules"]),
                           "modulesBeforeAndAfter": len(catalog["modules"]),
                           "choicesBeforeAndAfter": sum(len(module["choices"]) for module in catalog["modules"]),
                           "orderedContributionsBeforeAndAfter": len(contributions),
                           "catalogOriginalCanonicalSha256": compact.canonical_sha(catalog),
                           "catalogRestoredCanonicalSha256": compact.canonical_sha(restored_catalog),
                           "resourceFontAnimationIdsAndDefinitionsExceptPathsUnchanged": True,
                           "nodeTreeProgramsTimingSamplingC3TargetsAndC4SelectorsRestoredExactly": True},
             "projection": projection,
             "validationBoundary": "No image decode/re-encode, runtime, codec, heavy package audit, tests or OSK creation performed"}
    write_new_json(evidence / "identity-map.json", {"forward": identities, "inverse": inverse_ids, "receipt": identity_receipt})
    write_new_json(evidence / "texture-path-map.json", {"forward": textures, "inverse": inverse_textures, "protection": texture_receipt,
                                                        "files": [paths[path] for path in sorted(texture_files)]})
    write_new_json(evidence / "configuration-path-map.json", {"scenes": scene_paths, "manifests": manifest_paths,
                                                             "orderedContributions": [{"module": module, "choice": choice, "variantIndex": index,
                                                                  "sourceScene": part.get("scene"), "sourceManifest": part.get("manifest")}
                                                                 for module, choice, index, part in contributions]})
    write_new_json(evidence / "scene-reversal.json", scene_proofs)
    write_new_json(evidence / "manifest-reversal.json", manifest_proofs)
    write_new_json(evidence / "source-inventory.json", source_snapshot)
    write_new_json(evidence / "file-path-sha256-map.json", list(paths.values()))
    write_new_json(evidence / "budget-projection.json", projection)
    write_new_json(evidence / "proof.json", proof)
    generation = {"name": NAME, "status": proof["status"], "repackagedFrom": str(source), "sourceIdentity": parent,
                  "compaction": {"proof": str(evidence / "proof.json"), "proofSha256": sha(evidence / "proof.json"),
                                 "identityAliases": len(identities), "texturePathAliases": len(textures),
                                 "budgetProjection": str(evidence / "budget-projection.json")},
                  "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
                  "packageValidation": {"status": "pending", "reason": "Metadata projection and preservation proofs do not replace current shared audits"},
                  "unifiedOptionCount": len(root.get("options", [])) + len(catalog["modules"]),
                  "closure": {"files": len(entries), "uncompressedBytes": sum(row["destinationBytes"] for row in entries.values())},
                  "authorDirectory": directory_budget, "archive": {"status": "not-created"}}
    write_new_json(staging / "generation.json", generation)
    compact.require(not output.exists() and not output.with_suffix(".osk").exists() and staging.parent == output.parent,
                    "The fresh destination became occupied; preserve the staging evidence instead")
    staging.rename(output)
    return {"author": str(output), "evidence": str(evidence), "status": proof["status"],
            "sceneDocuments": len(scene_proofs), "manifestDocuments": len(manifest_proofs),
            "identityAliases": len(identities), "texturePathAliases": len(textures),
            "before": before["selectedUpperBound"], "after": after["selectedUpperBound"],
            "remainingBytes": projection["remainingBytes"], "archiveCreated": False}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--evidence", type=Path, required=True)
    parser.add_argument("--parent-sha256", required=True)
    arguments = parser.parse_args()
    print(json.dumps(prepare(arguments.source, arguments.output, arguments.evidence, arguments.parent_sha256),
                     ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
