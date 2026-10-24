"""Migrate a frozen private Omni author with explicitly reviewed brightness values.

The required reference contract is oms-iidx-shared-lane-objects-brightness-reference.v1.
Supply status reviewed, source {manifestSha256,generationSha256,archiveSha256},
canonical {archiveSha256,barSources:[{path,sha256}],provenance}, and one setting:
{id:"lane-objects-brightness",min,max,step,default,reference:
 {menuLabel,description,review,screenshots:[{path,sha256,supports:[min,max,step,default]}]}}.
barSources must pin bms/bar.png and mania/bar.png in the explicit --canonical-source
archive; both original PNGs must be byte-identical. Description records the actual
shared native control/help interpretation. Screenshot paths resolve relative to the
reference JSON. No numeric setting has an inferred/default range in this tool.
"""

from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
from pathlib import Path
import re
import uuid
import zipfile

from PIL import Image

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                             package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_start_options import (BAR_PATH, BAR_RESOURCE, BAR_TARGETS, BRIGHTNESS,
                                    OPTION_ID, OPTION_LABELS, add_brightness_options, attach_barline_brightness,
                                    attach_barline_materials, attach_brightness, brightness_definition, require)

REFERENCE = "oms-iidx-shared-lane-objects-brightness-reference.v1"
SOURCE_FIELDS = {"manifestSha256", "generationSha256", "archiveSha256"}
VALUE_FIELDS = {"min", "max", "step", "default"}
BAR_SOURCES = {"bms/bar.png", "mania/bar.png"}


def _sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def _development_path(path, *, exists=True):
    path = Path(path)
    require(path.drive.casefold() not in ("c:", "d:"), "Use independent development-drive author/reference paths")
    path = path.resolve(strict=exists)
    require(path.drive.casefold() not in ("c:", "d:"), "Use independent development-drive author/reference paths")
    return path


def _unique_fields(items):
    result = {}
    for key, value in items:
        require(key not in result, "Duplicate field in reviewed reference: " + key)
        result[key] = value
    return result


def load_reference(path):
    """Require reviewed bounds and actual hash-matching screenshot receipts."""
    path = _development_path(path)
    data = path.read_bytes()
    reference = json.loads(data.decode("utf-8-sig"), object_pairs_hook=_unique_fields)
    require(isinstance(reference, dict) and set(reference) == {"contract", "status", "source", "canonical", "setting"}
            and reference["contract"] == REFERENCE and reference["status"] == "reviewed",
            "Supply an explicitly reviewed original-menu reference")
    identity = reference["source"]
    require(isinstance(identity, dict) and set(identity) == SOURCE_FIELDS
            and all(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) for value in identity.values()),
            "Pin the frozen source manifest, generation receipt and original archive hashes")
    canonical = reference["canonical"]
    require(isinstance(canonical, dict) and set(canonical) == {"archiveSha256", "barSources", "provenance"}
            and isinstance(canonical["archiveSha256"], str) and re.fullmatch(r"[0-9a-f]{64}", canonical["archiveSha256"])
            and isinstance(canonical["provenance"], str) and canonical["provenance"].strip()
            and isinstance(canonical["barSources"], list) and len(canonical["barSources"]) == 2
            and all(isinstance(item, dict) and set(item) == {"path", "sha256"}
                    and isinstance(item["path"], str) and isinstance(item["sha256"], str)
                    and re.fullmatch(r"[0-9a-f]{64}", item["sha256"]) for item in canonical["barSources"])
            and {item["path"] for item in canonical["barSources"]} == BAR_SOURCES,
            "Pin the reviewed canonical archive and both original bar-line PNGs")
    item = reference["setting"]
    require(isinstance(item, dict) and set(item) == {"id", "reference", *VALUE_FIELDS} and item["id"] == OPTION_ID,
            "Supply the one shared Lane Objects Brightness Adjust setting")
    definition = brightness_definition(item["id"], minimum=item["min"], maximum=item["max"],
        step=item["step"], default=item["default"])
    evidence = item["reference"]
    require(isinstance(evidence, dict) and set(evidence) == {"menuLabel", "description", "review", "screenshots"}
            and all(isinstance(evidence[key], str) and evidence[key].strip() for key in ("menuLabel", "description", "review"))
            and isinstance(evidence["screenshots"], list) and evidence["screenshots"],
            "The shared setting needs its actual menu label, description, reviewed interpretation and screenshots")
    supported, screenshots = set(), []
    for screenshot in evidence["screenshots"]:
        require(isinstance(screenshot, dict) and set(screenshot) == {"path", "sha256", "supports"}
                and isinstance(screenshot["path"], str) and screenshot["path"]
                and isinstance(screenshot["sha256"], str) and re.fullmatch(r"[0-9a-f]{64}", screenshot["sha256"])
                and isinstance(screenshot["supports"], list) and screenshot["supports"]
                and all(isinstance(field, str) and field in VALUE_FIELDS for field in screenshot["supports"]),
                "Invalid shared-setting screenshot receipt")
        image_path = _development_path(path.parent / screenshot["path"])
        image_data = image_path.read_bytes()
        require(hashlib.sha256(image_data).hexdigest() == screenshot["sha256"], "A reviewed screenshot changed")
        with Image.open(io.BytesIO(image_data)) as image:
            require(image.format in ("PNG", "JPEG"), "Reference receipts must identify real PNG/JPEG screenshots")
            image.verify()
        supported.update(screenshot["supports"])
        screenshots.append({**copy.deepcopy(screenshot), "path": str(image_path)})
    require(supported == VALUE_FIELDS, "Screenshot receipts must support min, max, step and default for the shared setting")
    receipt = {"id": item["id"], **{key: item[key] for key in VALUE_FIELDS},
               "reference": {**evidence, "screenshots": screenshots}}
    return {"path": path, "sha256": hashlib.sha256(data).hexdigest(), "source": identity,
            "canonical": canonical, "definitions": [definition], "setting": receipt}


def load_canonical(path, reference):
    """Read the pinned ordinary package without importing a skin or touching player storage."""
    path = _development_path(path)
    require(_sha(path) == reference["archiveSha256"], "The canonical archive differs from the reviewed source")
    originals = {}
    with zipfile.ZipFile(path) as package:
        names = package.namelist()
        for entry in (*sorted(BAR_SOURCES), "skin.ini"):
            require(names.count(entry) == 1 and package.getinfo(entry).file_size <= 2 * 1024 * 1024,
                    "The canonical package must contain each bounded original asset once")
            originals[entry] = package.read(entry)
    for original in reference["barSources"]:
        require(hashlib.sha256(originals[original["path"]]).hexdigest() == original["sha256"],
                "A canonical bar-line source changed")
    data = originals["bms/bar.png"]
    require(data == originals["mania/bar.png"], "Both canonical bar-line PNGs must have exactly the same original bytes")
    with Image.open(io.BytesIO(data)) as image:
        require(image.format == "PNG" and image.mode == "RGBA", "Preserve the canonical RGBA PNG without conversion")
        dimensions = list(image.size)
        image.verify()
    targets, section, target = [], "", ""
    for line in originals["skin.ini"].decode("utf-8-sig").splitlines():
        line = line.strip()
        if line.startswith("["):
            section = line
        elif line.startswith("Target:"):
            target = line
        elif line.startswith("playfield.bar-line:"):
            require(section == "[GameplaySkin.Common:1]" and target in BAR_TARGETS,
                    "A canonical bar-line scope differs from the reviewed public group contract")
            expected = 'playfield.bar-line: resource Provide "' + ("bms/bar" if "ruleset=bms " in target else "mania/bar") + '"'
            require(line == expected, "The canonical scope must provide its pinned original bar-line texture")
            targets.append(target)
    require(tuple(targets) == BAR_TARGETS, "The canonical archive must retain all four public BMS/mania group scopes")
    return data, {"archive": str(path), "archiveSha256": reference["archiveSha256"],
        "provenance": reference["provenance"], "sources": copy.deepcopy(reference["barSources"]),
        "addedAsset": BAR_PATH, "sha256": hashlib.sha256(data).hexdigest(), "dimensions": dimensions,
        "originalBytesCopied": True, "sameBmsManiaPng": True, "publicGroupTargets": list(BAR_TARGETS)}


def upgrade(source, reference_path, output, *, canonical_source):
    source = _development_path(source)
    output = _development_path(output, exists=False)
    reference = load_reference(reference_path)
    require(output.parent == source.parent and output != source, "Choose a new sibling author directory")
    require(not output.exists() and not output.with_suffix(".osk").exists() and output.parent.is_dir(),
            "The new author and archive must not already exist")
    require(not reference["path"].is_relative_to(output), "The reference cannot live in the new output")
    canonical_data, canonical_receipt = load_canonical(canonical_source, reference["canonical"])
    require(not Path(canonical_receipt["archive"]).is_relative_to(output), "The canonical source cannot live in the new output")
    archive = source.with_suffix(".osk")
    require(archive.is_file(), "The frozen source must retain its original sibling archive")
    identity = {"manifestSha256": _sha(exact_file(source, "gameplay-skin.json")),
                "generationSha256": _sha(exact_file(source, "generation.json")), "archiveSha256": _sha(archive)}
    require(identity == reference["source"], "The author/archive differs from the reviewed frozen source")
    source_files = package_closure(source)
    preserved = {relative: _sha(exact_file(source, relative))
                 for relative in (*source_files, "generation.json", "package-audit.json")}
    source_catalog = read_json(exact_file(source, "gameplay/content.json"))
    source_manifest = read_json(exact_file(source, "gameplay-skin.json"))
    require(not any(option["id"] in {OPTION_ID, "note-brightness", "beam-brightness", "explosion-brightness", "judgement-brightness"}
                    for option in source_manifest["options"]), "Start from the frozen pre-brightness author")
    require(BAR_PATH not in source_files and not any(resource["id"] == BAR_RESOURCE for resource in source_manifest["resources"]),
            "The frozen source already owns the reserved bar-line asset")
    require(all("playfield.bar-line" not in exact_file(source, relative).read_text(encoding="utf-8")
                for relative in source_files if relative.endswith(".ini")),
            "An existing authored bar-line declaration would compete with the new shared material")
    expected_modules = set(BRIGHTNESS)
    require({module["id"] for module in source_catalog["modules"]} >= expected_modules,
            "The frozen Omni source is missing a required material module")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(source / relative, staging / relative)
    manifest = add_brightness_options(source_manifest, reference["definitions"])
    manifest["resources"].append({"id": BAR_RESOURCE, "type": "texture", "path": BAR_PATH})
    base_scene, barline_receipt = attach_barline_brightness(read_json(exact_file(source, "gameplay-skin.scene.json")))
    write_json(staging / "gameplay-skin.scene.json", base_scene)
    (staging / "skin.ini").write_bytes(attach_barline_materials(exact_file(source, "skin.ini").read_bytes()))
    bar_asset = staging / BAR_PATH
    bar_asset.parent.mkdir(parents=True, exist_ok=True)
    bar_asset.write_bytes(canonical_data)
    catalog = copy.deepcopy(source_catalog)
    transformed, receipts = {}, []
    for module in catalog["modules"]:
        if module["id"] not in BRIGHTNESS:
            continue
        for choice in module["choices"]:
            for part in (choice, *choice.get("variants", [])):
                key = (module["id"], part["scene"])
                if key not in transformed:
                    scene, receipt = attach_brightness(read_json(exact_file(staging, part["scene"])), module=module["id"])
                    transformed[key] = store_json(staging, "s", scene)
                    receipts.append({"module": module["id"], "sourceScene": part["scene"],
                                     "scene": transformed[key], **receipt})
                part["scene"] = transformed[key]
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    media = {relative for relative in source_files if not relative.endswith((".json", ".ini"))}
    require(media | {BAR_PATH} == {relative for relative in files if not relative.endswith((".json", ".ini"))}
            and all(_sha(exact_file(staging, relative)) == preserved[relative] for relative in media)
            and _sha(exact_file(staging, BAR_PATH)) == canonical_receipt["sha256"],
            "The migration changed existing media, previews or recorded programs")
    require(all(_sha(exact_file(staging, relative)) == preserved[relative]
                for relative in set(source_files).intersection(files)
                - {"skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json", "gameplay/content.json"}),
            "The migration changed an existing referenced configuration or uncompressed program")
    restored = copy.deepcopy(catalog)
    for original_module, module in zip(source_catalog["modules"], restored["modules"]):
        if module["id"] in BRIGHTNESS:
            for original_choice, choice in zip(original_module["choices"], module["choices"]):
                for original_part, part in zip((original_choice, *original_choice.get("variants", [])),
                                                (choice, *choice.get("variants", []))):
                    part["scene"] = original_part["scene"]
    require(restored == source_catalog and manifest["options"][:len(source_manifest["options"])] == source_manifest["options"],
            "The migration changed existing choices, previews, defaults or settings")
    require(all(_sha(exact_file(source, relative)) == digest for relative, digest in preserved.items())
            and _sha(archive) == identity["archiveSha256"]
            and _sha(Path(canonical_receipt["archive"])) == canonical_receipt["archiveSha256"],
            "The immutable source, old archive or canonical source changed during migration")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    require(audit["options"] == len(source_manifest["options"]) + len(source_catalog["modules"]) + 1,
            "The migration must add exactly one shared global setting")
    require(not audit["aggregateViolations"], f"The new closure exceeds existing budgets: {audit['aggregateViolations']}")
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity,
        "reviewedStartOptions": {"path": str(reference["path"]), "sha256": reference["sha256"], "setting": reference["setting"]},
        "brightnessUpgrade": receipts, "barlineUpgrade": {**barline_receipt, **canonical_receipt},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedExistingMedia": [], "addedCanonicalAssets": [BAR_PATH], "mediaAndPreviewFiles": len(media),
            "catalogChoices": sum(len(module["choices"]) for module in source_catalog["modules"]),
            "previewFiles": len(set(preview_files(source_catalog))),
            "retiredConfigurationFiles": sorted(set(source_files) - set(files))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"],
        "scope": "One reviewed Lane Objects Brightness Adjust RGB multiplier across notes/holds, beam, explosion, judgement and canonical bar lines;"
                 " one original canonical PNG and one sprite template replace the old bar sprite, preserving alpha/geometry/native fades."
                 " Column Brightness Adjust is not implemented."}
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--reference", type=Path, required=True)
    parser.add_argument("--canonical-source", type=Path, required=True, help="Reviewed original oms-simple.osk containing the unchanged public bar-line PNGs")
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.reference, args.output, canonical_source=args.canonical_source)


if __name__ == "__main__":
    main()
