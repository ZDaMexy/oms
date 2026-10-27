"""Add BMS clear semantics to a fresh sibling of the sealed Omni author."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import struct
import uuid
import zlib

from build_lighting_details import store_json
from build_lighting_gameplay_controls import audit_shared_root
from build_lighting_score_graph_roles import verify_parent_archive
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
    package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_persistent import _independent
import lighting_gauge_state as gauge


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def white_pixel():
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0)) + chunk(
        b"IDAT", zlib.compress(b"\x00\xff\xff\xff\xff", 9)) + chunk(b"IEND", b"")


def private_path_plan(manifests, root_manifest, hashes, ini_contents):
    """Only private media paths change; all bytes and C4 identities remain."""
    markers = {value + ".png" for content in ini_contents for value in re.findall(r'Provide "([^"]+)"', content)}
    plan, identities, occupied = {}, {}, dict(hashes)
    for resource in root_manifest["resources"]:
        old = resource["path"]
        if old in markers or any(old.removesuffix(".png") in content for content in ini_contents):
            continue
        digest = hashes[old]
        new = "u/" + digest[:8] + ".png"
        if len(new) >= len(old):
            continue
        gauge.require(new not in occupied or occupied[new] == digest, "A short path collides with different source bytes")
        occupied[new] = digest
        plan[old], identities[new] = new, digest
    changed = copy.deepcopy(manifests)
    for manifest in changed.values():
        for resource in manifest["resources"]:
            resource["path"] = plan.get(resource["path"], resource["path"])
    return changed, {"paths": plan, "fullSha256": identities, "allIniFilesChecked": len(ini_contents),
                     "c4MarkerPathsExcluded": len(markers), "resourceFontMaterialIdsUnchanged": True}


def layout_of(scene):
    layouts = {match.group(1) for instance in scene["instances"]
               if (match := re.fullmatch(r"markers/frame-(p1|p2|dp)-information", instance.get("material", {}).get("resource", "")))}
    gauge.require(len(layouts) == 1, "Use one actual selected Frame layout")
    return layouts.pop()


def host_size(manifest, ini):
    widths = {round(stage["rect"][2] * 1920, 6) for profile in manifest["layouts"] if profile["ruleset"] == "bms"
              for stage in profile["stages"]}
    heights = {float(value) * 1080 for value in re.findall(r"^GaugeHeight:\s*([0-9.]+)", ini, re.MULTILINE)}
    gauge.require(len(widths) == len(heights) == 1, "The Frame has ambiguous native gauge host dimensions")
    return (widths.pop(), heights.pop())


def upgrade(source, output, library, parent_sha256):
    source, library = _independent(source), _independent(library)
    output = _independent(output, output=True, exists=False)
    gauge.require(output.parent == source.parent and not output.exists() and not output.with_suffix(".osk").exists(),
                  "Create a fresh sibling gauge-state author and archive")
    original_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in original_files}
    archive = source.with_suffix(".osk")
    identity = {"generationSha256": sha(source / "generation.json"), "packageAuditSha256": sha(source / "package-audit.json"),
                "archiveSha256": verify_parent_archive(archive, parent_sha256, original_files, hashes)}
    generation = read_json(source / "generation.json")
    gauge.require(generation["archive"]["sha256"] == identity["archiveSha256"], "The sealed parent receipt differs")
    index_identity = sha(library / "library.json")
    catalog = read_json(source / "gameplay/content.json")
    original_catalog = copy.deepcopy(catalog)
    frame, = [module for module in catalog["modules"] if module["id"] == "frame"]
    gauge.require(len(catalog["modules"]) == 13 and len(frame["choices"]) == 15, "Use the sealed complete Omni parent")
    manifests = {"gameplay-skin.json": read_json(source / "gameplay-skin.json")}
    scenes, nonframe_ids, external_references = {}, set(), set()
    frame_scenes = {part["scene"] for choice in frame["choices"] for part in (choice, *choice.get("variants", []))}
    for module in catalog["modules"]:
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                if part["manifest"] not in manifests:
                    manifests[part["manifest"]] = read_json(source / part["manifest"])
                if part["scene"] not in scenes:
                    scenes[part["scene"]] = read_json(source / part["scene"])
    scenes["gameplay-skin.scene.json"] = read_json(source / "gameplay-skin.scene.json")
    for path, scene in scenes.items():
        if path not in frame_scenes:
            nonframe_ids.update(item["id"] for item in gauge.declarations(scene))
            external_references.update(gauge._strings(scene, omit_identity=True))
    for manifest in manifests.values():
        nonframe_ids.update(item["id"] for field in ("resources", "bitmapFonts", "animations") for item in manifest.get(field, []))
        external_references.update(gauge._strings(manifest, omit_identity=True))
    ini_contents = [(source / path).read_text("utf-8-sig") for path in sorted(original_files) if path.endswith(".ini")]
    changed_manifests, aliases = private_path_plan(manifests, manifests["gameplay-skin.json"], hashes, ini_contents)
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in original_files:
        copy_exact(source / relative, staging / relative)
    for old, new in aliases["paths"].items():
        copy_exact(source / old, staging / new)
        gauge.require(sha(staging / new) == aliases["fullSha256"][new], "A media alias changed source bytes")
    line_data = white_pixel()
    line_path = "u/clear-line.png"
    target = staging / line_path
    gauge.require(not target.exists(), "The generated marker path is already occupied")
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_bytes(line_data)
    line = {"id": "gx.line", "type": "texture", "path": line_path, "filter": "nearest"}
    changed_manifests["gameplay-skin.json"]["resources"].append(line)
    frame_changes = {}
    for choice in frame["choices"]:
        for part in (choice, *choice.get("variants", [])):
            key = (part["scene"], part["manifest"])
            if key not in frame_changes:
                scene, manifest = scenes[key[0]], copy.deepcopy(changed_manifests[key[1]])
                layout = layout_of(scene)
                expanded, semantics = gauge.add_semantics(scene, layout=layout,
                    host_size=host_size(manifest, (source / part["ini"]).read_text("utf-8-sig")), line_resource=line["id"])
                compacted, compact_receipt = gauge.compact_unreferenced_identities(expanded,
                    forbidden_ids=nonframe_ids, external_references=external_references)
                gauge.require(not {item["id"] for item in gauge.declarations(compacted)}.intersection(nonframe_ids),
                              "A Frame declaration collides with another selectable C2 module")
                manifest["resources"].append(copy.deepcopy(line))
                frame_changes[key] = {"scene": store_json(staging, "s", compacted), "manifest": store_json(staging, "m", manifest),
                                      "semantics": semantics, "identities": compact_receipt}
            changed = frame_changes[key]
            part.update({field: changed[field] for field in ("scene", "manifest")})
    # Every other module keeps its exact scene and INI. Shared media aliases
    # update every declaration, avoiding inconsistent C2 resource identities.
    for module in catalog["modules"]:
        if module["id"] == "frame":
            continue
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                original = part["manifest"]
                if changed_manifests[original] != manifests[original]:
                    part["manifest"] = store_json(staging, "m", changed_manifests[original])
    write_json(staging / "gameplay-skin.json", changed_manifests["gameplay-skin.json"])
    write_json(staging / "gameplay/content.json", catalog)
    gauge.require(preview_files(catalog) == preview_files(original_catalog), "A preview choice changed")
    files = package_closure(staging)
    allowed = {"gameplay-skin.json", "gameplay/content.json"}
    gauge.require(all(sha(staging / path) == hashes[path] for path in set(files).intersection(original_files) - allowed),
                  "A retained payload changed")
    audit = audit_package(staging, files)
    audit_shared_root(staging, catalog, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    gauge.require(all(sha(source / path) == digest for path, digest in hashes.items()) and sha(archive) == identity["archiveSha256"]
                  and sha(source / "generation.json") == identity["generationSha256"] and sha(library / "library.json") == index_identity,
                  "The sealed author/archive/library metadata changed")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "sourceLibraryIndexSha256": index_identity, "gaugeState": {
            "hazardPolicy": "User-selected A: original per-frame pixels retained; live HAZARD / SURVIVAL label, no inferred legacy image or borrowed33 palette",
            "frames": [{"sourceScene": key[0], "sourceManifest": key[1], **value} for key, value in frame_changes.items()],
            "pathAliases": aliases, "lineTextureSha256": hashlib.sha256(line_data).hexdigest()},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "allExistingPngAndRecordedPayloadBytesUnchanged": True, "allOriginalIniAndNonFrameScenesUnchanged": True},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "generation.json", receipt)
    write_json(staging / "package-audit.json", audit)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ("source", "output", "library", "parent-sha256"):
        parser.add_argument("--" + field, required=True, type=str if field == "parent-sha256" else Path)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.library, args.parent_sha256)


if __name__ == "__main__":
    main()
