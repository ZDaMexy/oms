"""Publish three truthful score Graph roles in a fresh private Omni author."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import uuid
import zipfile

from build_lighting_details import store_json
from build_lighting_gameplay_controls import audit_shared_root
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, resource_references, write_json)
from lighting_persistent import _independent
import lighting_score_graph_roles as roles

RED_RESOURCE = {"id": "sgr.r", "type": "texture", "path": "art/gr.png", "filter": "nearest"}
ASSETS = {
    "blue": ("gr_sco_gauge_large_blue.png", (90, 690)),
    "green": ("gr_sco_gauge_large_green.png", (90, 690)),
    "red": ("gr_sco_gauge_large_red.png", (90, 690)),
    "frame": ("gr_sco_gauge_waku.png", (94, 692)),
}
# Explicit OMS placements, not a reconstruction of the original host's bar table.
# A source 90x690 PNG is drawn 90x689 so the destination spans exactly y178..867.
GEOMETRY = {role: {"fill": [x + 2, 867, 90, 689], "frame": [x, 177, 94, 692]}
            for role, x in zip(roles.ROLES, (1525, 1656, 1787))}
CHANGED_CONFIGS = {"gameplay-skin.json", "gameplay-skin.scene.json", "gameplay/content.json"}


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def verify_parent_archive(archive, expected_sha256, source_files, hashes):
    roles.require(isinstance(expected_sha256, str) and re.fullmatch(r"[0-9a-f]{64}", expected_sha256),
                  "Supply the frozen parent archive SHA256")
    archive_sha256 = sha(archive)
    roles.require(archive_sha256 == expected_sha256, "The frozen parent archive SHA256 changed")
    with zipfile.ZipFile(archive) as package:
        roles.require(sorted(package.namelist()) == source_files, "The parent archive differs from its source closure")
        for relative in source_files:
            with package.open(relative) as stream:
                roles.require(hashlib.file_digest(stream, "sha256").hexdigest() == hashes[relative],
                              "A parent author file differs from the frozen archive: " + relative)
    return archive_sha256


def source_reference(library):
    """Measure only the exact Graph source images required by this upgrade."""
    from PIL import Image

    library = _independent(library)
    index_file = exact_file(library, "library.json")
    index = read_json(index_file)
    roles.require(index["status"] == "complete" and index["sourceMetadataUnchanged"] is True,
                  "Use the frozen independent source library")
    aliases = roles.verify_graph_aliases(library, index["textures"])
    by_name = {row["name"]: row for row in index["textures"] if row["package"] == roles.GRAPH_PACKAGE}
    read_inputs = {str(index_file): {"path": str(index_file), "sha256": sha(index_file)}}
    assets = {}
    for role, (name, size) in ASSETS.items():
        row = by_name[name]
        path = roles._contained(library, row["sprite"])
        digest = sha(path)
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            measured = rgba.size, hashlib.sha256(rgba.tobytes()).hexdigest()
        roles.require(digest == row["sha256"] and measured == (size, row["rgbaSha256"])
                      and (row["width"], row["height"]) == size
                      and row["sampler"]["mag"] == row["sampler"]["min"] == "nearest"
                      and row["sampler"]["wrapS"] == row["sampler"]["wrapT"] == "clamp",
                      "A reviewed original Graph texture or sampler changed")
        assets[role] = {"name": name, "source": row["sprite"], "sha256": digest,
                        "rgbaSha256": measured[1], "width": size[0], "height": size[1]}
        read_inputs[str(path)] = {"path": str(path), "sha256": digest}
    for alias, canonical in roles.ALIASES:
        for name in (alias, canonical):
            path = roles._contained(library, by_name[name]["sprite"])
            read_inputs[str(path)] = {"path": str(path), "sha256": by_name[name]["sha256"]}
    return {"contract": "oms-iidx-score-graph-roles-art.v1", "library": str(library),
            "assets": assets, "aliasProof": aliases, "readInputs": list(read_inputs.values())}


def add_graph_resources(source, manifest, reference):
    """Reuse original root blue/green/frame images and declare only one red PNG."""
    roles.require(not any(resource["id"] == RED_RESOURCE["id"] or resource["path"] == RED_RESOURCE["path"]
                          for resource in manifest["resources"]), "The new red Graph resource conflicts with the parent")
    updated = copy.deepcopy(manifest)
    selected = {}
    for name in ("blue", "green", "frame"):
        matches = [resource for resource in manifest["resources"] if resource["type"] == "texture"
                   and sha(exact_file(source, resource["path"])) == reference["assets"][name]["sha256"]]
        roles.require(len(matches) == 1 and matches[0].get("filter") == "nearest",
                      "Use the one byte-identical original root Graph " + name + " image")
        selected[name] = matches[0]["id"]
    updated["resources"].append(copy.deepcopy(RED_RESOURCE))
    resource_map = {role: {"fill": selected[colour] if colour != "red" else RED_RESOURCE["id"], "frame": selected["frame"]}
                    for role, colour in zip(roles.ROLES, ("blue", "green", "red"))}
    return updated, resource_map


def close_target_resources(contributions, root_manifest):
    """Declare exact C2 scene/font dependencies, reusing equal root definitions."""
    updated = copy.deepcopy(contributions)
    shared = {item["id"]: item for item in root_manifest["resources"]}
    for contribution in updated.values():
        manifest = contribution["manifest"]
        original = {item["id"]: item for item in manifest["resources"]}
        roles.require(all(original[key] == shared[key] for key in set(original) & set(shared)),
                      "A Graph contribution disagrees with a shared root texture")
        required = resource_references(contribution["scene"], manifest.get("bitmapFonts", []))
        available = {**shared, **original}
        roles.require(required <= set(available), "A new Graph contribution has an undeclared source texture")
        order = [*original, *(key for key in shared if key not in original)]
        manifest["resources"] = [copy.deepcopy(available[key]) for key in order if key in required]
    return updated


def upgrade(source, output, library, parent_sha256):
    source = _independent(source)
    output = _independent(output, output=True, exists=False)
    library = _independent(library)
    roles.require(output.parent == source.parent and output != source and not output.exists()
                  and not output.with_suffix(".osk").exists(), "Create a fresh sibling Graph author and archive")
    archive = source.with_suffix(".osk")
    generation = read_json(exact_file(source, "generation.json"))
    original_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in original_files}
    identity = {"generationSha256": sha(source / "generation.json"),
                "packageAuditSha256": sha(source / "package-audit.json"),
                "archiveSha256": verify_parent_archive(archive, parent_sha256, original_files, hashes)}
    roles.require(generation["archive"]["sha256"] == identity["archiveSha256"] and "autoScratch" in generation,
                  "Use the frozen AUTO SCRATCH parent and its matching archive receipt")
    original_catalog = read_json(exact_file(source, "gameplay/content.json"))
    roles.require(len(original_catalog["modules"]) == 13
                  and sum(len(module["choices"]) for module in original_catalog["modules"]) == 1285,
                  "Keep all existing art and functional choices")
    module, = [entry for entry in original_catalog["modules"] if entry["id"] == "graph-target"]
    roles.require([choice["value"] for choice in module["choices"]] == list(roles.TARGETS)
                  and module["default"] == "maximum" and all(not choice.get("variants") for choice in module["choices"]),
                  "Use the original seven-choice C2 Graph target module")
    contributions = {choice["value"]: {"scene": read_json(exact_file(source, choice["scene"])),
                                       "manifest": read_json(exact_file(source, choice["manifest"]))} for choice in module["choices"]}
    reference = source_reference(library)
    manifest, resource_map = add_graph_resources(source, read_json(source / "gameplay-skin.json"), reference)
    scene, contributions, role_receipt = roles.inject_roles(read_json(source / "gameplay-skin.scene.json"), contributions,
                                                          role_resources=resource_map, geometry=GEOMETRY)
    contributions = close_target_resources(contributions, manifest)
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in original_files:
        copy_exact(exact_file(source, relative), staging / relative)
    roles.require(RED_RESOURCE["path"] not in original_files, "The new red asset path is already used by the parent closure")
    copy_exact(roles._contained(library, reference["assets"]["red"]["source"]), staging / RED_RESOURCE["path"])
    roles.require(sha(staging / RED_RESOURCE["path"]) == reference["assets"]["red"]["sha256"],
                  "The new red Graph PNG bytes changed during copying")
    catalog = copy.deepcopy(original_catalog)
    updated_module, = [entry for entry in catalog["modules"] if entry["id"] == "graph-target"]
    new_configs = set()
    for choice in updated_module["choices"]:
        contribution = contributions[choice["value"]]
        choice["scene"] = store_json(staging, "s", contribution["scene"])
        choice["manifest"] = store_json(staging, "m", contribution["manifest"])
        new_configs.update((choice["manifest"], choice["scene"]))
    write_json(staging / "gameplay-skin.scene.json", scene)
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    roles.require(set(files) <= set(original_files) | new_configs | {RED_RESOURCE["path"]},
                  "Only reviewed Graph scenes and one original red PNG may extend the closure")
    roles.require(preview_files(catalog) == preview_files(original_catalog) and len(preview_files(catalog)) == 539,
                  "Preserve all 539 previews and their selectors")
    roles.require(all(before == after for before, after in zip(original_catalog["modules"], catalog["modules"])
                      if before["id"] != "graph-target"), "An unrelated module changed")
    roles.require(all(sha(exact_file(staging, relative)) == hashes[relative]
                      for relative in set(original_files) & set(files) if relative not in CHANGED_CONFIGS),
                  "A retained parent image, font, animation, preview, or unrelated configuration changed")
    audit = audit_package(staging, files)
    audit_shared_root(staging, catalog, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    previous = read_json(exact_file(source, "package-audit.json"))
    roles.require(audit["options"] == 32 and audit["selectedUpperBound"]["fonts"] == previous["selectedUpperBound"]["fonts"] == 10
                  and not audit["aggregateViolations"], "Preserve all existing option/font/import budgets")
    roles.require(all(sha(item["path"]) == item["sha256"] for item in reference["readInputs"]),
                  "The independent source library changed during Graph authoring")
    roles.require(all(sha(exact_file(source, relative)) == expected for relative, expected in hashes.items())
                  and sha(source / "generation.json") == identity["generationSha256"]
                  and sha(source / "package-audit.json") == identity["packageAuditSha256"] and sha(archive) == identity["archiveSha256"],
                  "The immutable parent author, audit, receipt or archive changed")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "graphRoles": {"art": reference, "routing": role_receipt,
            "roleAssignment": "Explicit OMS author mapping: Current blue, genuine matched PB green, eligible effective non-PB Target red",
            "geometryPolicy": "OMS adaptation: 90x690 original fill drawn 90x689 at y178..867; original94x692 frame at y177; fixed three-role columns",
            "layouts": "Existing P2 -1502 translation and DP right-side Graph owners retained",
            "notClaimed": ["Original host bar placement table", "Yellow/team/ROG behaviour", "600-frame original texture scroll"],
            "originalSourcePngBytesPreserved": True, "newTextureFiles": [RED_RESOURCE["path"]], "fontsAdded": 0, "optionsAdded": 0},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "parentArchiveMatchesSourceClosure": True, "changedExistingMedia": [], "previewFiles": 539, "unrelatedModulesUnchanged": 12},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    print(json.dumps({"author": str(output), "graphTargetScenes": len(contributions), "newTextures": 1,
                      "budgets": receipt["budgets"], "closure": receipt["closure"]}, ensure_ascii=True))
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
