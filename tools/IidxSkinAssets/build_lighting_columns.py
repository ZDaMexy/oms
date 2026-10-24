"""Private column-source probe and explicitly reviewed fresh-author migration.

Probe mode requires the original independent library, recording and research
directories. Migration mode requires --column-reference and --reviewed-column;
there is no inferred Column range or step. The reviewed JSON
contract is oms-iidx-column-brightness-reference.v1, status reviewed, source
{manifestSha256,generationSha256,archiveSha256}, columnSourceReferenceSha256, and
setting {id:"column-brightness",min,max,step,default,mapping:{property,formula},
reference:{menuLabel,description,review,screenshots:[{path,sha256,supports}]}}.
The mapping names brightness/value as the reviewed OMS RGB visual adaptation.
Screenshot receipts support min/max/step/default, the affected-layer boundary,
and this adaptation; they do not prove the native internal blending algorithm.
Only the column meshes receive it; original source alpha/masks stay untouched.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import io
import json
import math
from pathlib import Path
import re
import uuid

from PIL import Image

from build_lighting_details import prune_unused_resources, store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                             package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_columns import (LAYOUTS, SOURCE_PACKAGES, _independent, _sha, attach_column_binding,
                              export_reference, inject_split, load_reference, merge_references, prepare_assets, require)

REVIEW_CONTRACT = "oms-iidx-column-brightness-reference.v1"
SOURCE_FIELDS = {"manifestSha256", "generationSha256", "archiveSha256"}
VALUE_FIELDS = {"min", "max", "step", "default"}
SUPPORTED_RECEIPTS = {*VALUE_FIELDS, "layer", "formula"}


def file_sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def column_definition(setting):
    require(setting["id"] == "column-brightness", "Use the reviewed Column setting identity")
    lo, hi, step, default = (setting[field] for field in ("min", "max", "step", "default"))
    require(all(type(value) in (int, float) and math.isfinite(value) for value in (lo, hi, step, default))
            and 0 <= lo < hi <= 1 and lo <= default <= hi and default == 1 and step > 0,
            "Column values must be finite, fit the selected public multiplier and preserve original default1")
    for value in (hi, default):
        intervals = (value - lo) / step
        require(math.isfinite(intervals) and intervals <= 10_000 and abs(intervals - round(intervals)) < 1e-8,
                "Column bounds/default must land on the reviewed original step")
    return {"id": setting["id"], "type": "number", "label": "轨道底色亮度",
            **{field: setting[field] for field in ("min", "max", "step", "default")}}


def load_reviewed_column(path):
    """Review native range/layer evidence and the chosen OMS visual adaptation."""
    path = _independent(path)
    require(path.drive.casefold() not in ("c:", "d:"), "Keep reviewed native inputs on an independent development drive")

    def unique_fields(items):
        result = {}
        for key, value in items:
            require(key not in result, "Duplicate reviewed Column field: " + key)
            result[key] = value
        return result

    data = path.read_bytes()
    result = json.loads(data.decode("utf-8-sig"), object_pairs_hook=unique_fields)
    require(isinstance(result, dict) and set(result) == {"contract", "status", "source", "columnSourceReferenceSha256", "setting"}
            and result["contract"] == REVIEW_CONTRACT and result["status"] == "reviewed", "Supply an explicitly reviewed Column reference")
    require(isinstance(result["source"], dict) and set(result["source"]) == SOURCE_FIELDS
            and all(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) for value in result["source"].values())
            and isinstance(result["columnSourceReferenceSha256"], str) and re.fullmatch(r"[0-9a-f]{64}", result["columnSourceReferenceSha256"]),
            "Pin the actual source author/archive and complete column-source reference")
    setting = result["setting"]
    require(isinstance(setting, dict) and set(setting) == {"id", *VALUE_FIELDS, "mapping", "reference"}, "Invalid reviewed Column setting fields")
    definition = column_definition(setting)
    mapping = setting["mapping"]
    require(isinstance(mapping, dict) and set(mapping) == {"property", "formula"}
            and mapping["property"] == "brightness" and mapping["formula"] == "value",
            "Supply the reviewed OMS RGB visual adaptation brightness/value")
    evidence = setting["reference"]
    require(isinstance(evidence, dict) and set(evidence) == {"menuLabel", "description", "review", "screenshots"}
            and all(isinstance(evidence[key], str) and evidence[key].strip() for key in ("menuLabel", "description", "review"))
            and isinstance(evidence["screenshots"], list) and evidence["screenshots"], "Column range, affected-layer and OMS adaptation review requires screenshot receipts")
    supported = set()
    for screenshot in evidence["screenshots"]:
        require(isinstance(screenshot, dict) and set(screenshot) == {"path", "sha256", "supports"}
                and isinstance(screenshot["path"], str) and screenshot["path"]
                and isinstance(screenshot["sha256"], str) and re.fullmatch(r"[0-9a-f]{64}", screenshot["sha256"])
                and isinstance(screenshot["supports"], list) and screenshot["supports"]
                and all(isinstance(field, str) and field in SUPPORTED_RECEIPTS for field in screenshot["supports"]),
                "Invalid Column screenshot receipt")
        image_path = _independent(path.parent / screenshot["path"])
        require(image_path.drive.casefold() not in ("c:", "d:"), "Use independently preserved native screenshots")
        image_data = image_path.read_bytes()
        require(_sha(image_data) == screenshot["sha256"], "A reviewed Column screenshot changed")
        with Image.open(io.BytesIO(image_data)) as image:
            require(image.format in ("PNG", "JPEG"), "Column receipts must be actual PNG/JPEG screenshots")
            image.verify()
        supported.update(screenshot["supports"])
        screenshot["path"] = str(image_path)
    require(supported == SUPPORTED_RECEIPTS, "Column receipts must cover range, step, default, affected-layer boundary and OMS adaptation")
    return {"path": path, "sha256": _sha(data), "source": result["source"], "definition": definition,
            "columnSourceReferenceSha256": result["columnSourceReferenceSha256"], "setting": setting}


def retarget_background(scene, data, *, frame, layout, assets, layout_materials):
    """Retire the old full bitmap as C4 material as well as its C5 resource."""
    old = f"gameplay/frames/{frame}/backgrounds/{layout}"
    backgrounds = [item for item in assets["backgrounds"] if item["oldPath"] == old + ".png"]
    require(len(backgrounds) == 1 and backgrounds[0]["path"].endswith(".png"), "The original C4 background lacks its exact floorless replacement")
    new = backgrounds[0]["path"][:-4]
    instances = [instance for instance in scene["instances"]
                 if instance["material"] == {"slot": "stage.background", "resource": old}]
    require(len(instances) == 1, "The exact C4 stage-background owner changed")
    require(set(layout_materials) == set(LAYOUTS) and layout_materials[layout] == new,
            "Supply this Frame's exact three floorless C4 material identities")
    result_data, counts = data, {}
    for current, replacement in layout_materials.items():
        previous = f"gameplay/frames/{frame}/backgrounds/{current}"
        before = ('stage.background: resource Provide "' + previous + '"').encode()
        after = ('stage.background: resource Provide "' + replacement + '"').encode()
        counts[current] = sum(line.rstrip(b"\r\n") == before for line in data.splitlines(keepends=True))
        require(counts[current] > 0 and after not in data, "The shared Frame INI must retain every original layout scope")
        result_data = b"".join(after + line[len(before):] if line.rstrip(b"\r\n") == before else line
                                for line in result_data.splitlines(keepends=True))
    result = copy.deepcopy(scene)
    next(instance for instance in result["instances"] if instance["id"] == instances[0]["id"])["material"]["resource"] = new
    return result, result_data, {"slot": "stage.background", "oldMaterial": old, "newMaterial": new,
        "oldPath": old + ".png", "newPath": new + ".png", "oldGameTextureRetired": True,
        "allLayoutProvideCounts": counts, "allLayoutMaterials": copy.deepcopy(layout_materials)}


def upgrade(source, column_reference_path, reviewed_path, output):
    """Called only after real native input exists; always create a fresh sibling."""
    source = _independent(source)
    output = _independent(output, output=True, exists=False)
    reviewed = load_reviewed_column(reviewed_path)
    reference = load_reference(column_reference_path)
    require(output.parent == source.parent and output != source and output.parent.is_dir()
            and not output.exists() and not output.with_suffix(".osk").exists(), "Choose a new sibling author/OSK")
    for root in (source, reference["directory"], reference["library"], reviewed["path"].parent):
        require(not output.is_relative_to(root) and not root.is_relative_to(output), "The new author overlaps a preserved input")
    require(file_sha(column_reference_path) == reviewed["columnSourceReferenceSha256"], "The reviewed source-split reference changed")
    require(set(reference["frames"]) == SOURCE_PACKAGES.keys()
            and all(set(layouts) == set(LAYOUTS) for layouts in reference["frames"].values()),
            "A final Column migration requires all15 Frames and allthree layouts")
    archive = source.with_suffix(".osk")
    identity = {"manifestSha256": file_sha(exact_file(source, "gameplay-skin.json")),
                "generationSha256": file_sha(exact_file(source, "generation.json")), "archiveSha256": file_sha(archive)}
    require(identity == reviewed["source"], "The reviewed immutable parent author/archive changed")
    source_files = package_closure(source)
    source_hashes = {relative: file_sha(exact_file(source, relative)) for relative in (*source_files, "generation.json", "package-audit.json")}
    original_manifest = read_json(exact_file(source, "gameplay-skin.json"))
    original_catalog = read_json(exact_file(source, "gameplay/content.json"))
    modules = [module for module in original_catalog["modules"] if module["id"] == "frame"]
    require(len(modules) == 1 and {choice["value"] for choice in modules[0]["choices"]} == SOURCE_PACKAGES.keys(),
            "The frozen parent must retain its exact15 Frame choices")
    require((sum(len(module["choices"]) for module in original_catalog["modules"]),
             len(preview_files(original_catalog))) in {(1272, 540), (1271, 539)}
            and len(original_manifest["options"]) + len(original_catalog["modules"]) == 27
            and any(option["id"] == "lane-objects-brightness" for option in original_manifest["options"])
            and not any(option["id"] == "column-brightness" for option in original_manifest["options"]),
            "Start from the complete shared-brightness parent with preserved choices/previews and27 options")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(source / relative, staging / relative)
    manifest, catalog = copy.deepcopy(original_manifest), copy.deepcopy(original_catalog)
    manifest["options"].append(reviewed["definition"])
    transformed, receipts, retired_media = {}, [], set()
    for choice in next(module for module in catalog["modules"] if module["id"] == "frame")["choices"]:
        frame = choice["value"]
        for part in (choice, *choice.get("variants", [])):
            key = (frame, part["scene"], part["manifest"], part["ini"])
            if key not in transformed:
                scene, contribution = (read_json(exact_file(source, part[field])) for field in ("scene", "manifest"))
                layouts = [layout for layout in LAYOUTS if any(instance["material"] == {
                    "slot": "stage.background", "resource": f"gameplay/frames/{frame}/backgrounds/{layout}"} for instance in scene["instances"])]
                require(len(layouts) == 1, "Each original Frame contribution must own exactly one known layout")
                layout = layouts[0]
                row = reference["frames"][frame][layout]
                require(row["sourceOwner"]["sceneSha256"] == file_sha(exact_file(source, part["scene"])),
                        "The parent Frame scene differs from its exact source-split reference")
                assets = prepare_assets(reference, staging, frame=frame, layout=layout, manifest=contribution)
                scene, contribution, receipt = inject_split(scene, contribution, frame=frame, layout=layout, assets=assets)
                scene = attach_column_binding(scene, receipt["columnTargets"], option_id="column-brightness",
                                              property_name=reviewed["setting"]["mapping"]["property"])
                material_map = {}
                for current in LAYOUTS:
                    original_path = f"gameplay/frames/{frame}/backgrounds/{current}.png"
                    matches = [item for item in reference["frames"][frame][current]["backgrounds"] if item["oldPath"] == original_path]
                    require(len(matches) == 1 and matches[0]["status"] == "split", "Every shared INI layout needs its proven original floorless replacement")
                    material_map[current] = "art/lc/b" + matches[0]["floorless"]["sha256"][:24]
                scene, ini, material = retarget_background(scene, exact_file(source, part["ini"]).read_bytes(),
                    frame=frame, layout=layout, assets=assets, layout_materials=material_map)
                prune_unused_resources(scene, contribution)
                require(not {item["oldPath"] for item in assets["backgrounds"]}
                        & {resource["path"] for resource in contribution["resources"]},
                        "A retired full-screen background is still a selected game texture")
                ini_hash = _sha(ini)
                ini_path = "config/i" + ini_hash + ".ini"
                target = staging / ini_path
                target.parent.mkdir(parents=True, exist_ok=True)
                require(not target.exists() or target.read_bytes() == ini, "An INI content identity conflicts")
                target.write_bytes(ini)
                transformed[key] = {"scene": store_json(staging, "s", scene), "manifest": store_json(staging, "m", contribution), "ini": ini_path}
                retired_media.update(item["oldPath"] for item in assets["backgrounds"])
                if assets["prefix"] is not None:
                    retired_media.add(assets["prefix"]["original"]["path"])
                receipts.append({"frame": frame, "layout": layout, "sourceScene": part["scene"], "sourceManifest": part["manifest"],
                    "sourceManifestSha256": file_sha(exact_file(source, part["manifest"])),
                    "material": material, "columnTargets": receipt["columnTargets"], "createdNodes": receipt["createdNodes"],
                    "sourceBitmapConsumers": receipt["sourceBitmapConsumers"], "retiredBitmapResources": receipt["retiredBitmapResources"],
                    "prefix": None if assets["prefix"] is None else assets["prefix"]["receipt"],
                    "copiedFiles": assets["copiedFiles"], "columnSourceReference": {"frame": frame, "layout": layout}})
            part.update(transformed[key])
    write_json(staging / "gameplay-skin.json", manifest)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    media = {relative for relative in source_files if not relative.endswith((".json", ".ini"))}
    require(all(file_sha(exact_file(staging, relative)) == source_hashes[relative] for relative in media.intersection(files)),
            "The new author changed an existing original media or preview file")
    retired = set(source_files) - set(files)
    require(retired.intersection(media) <= retired_media, "An unrelated original media asset was retired")
    require(retired_media.intersection(files) <= set(preview_files(original_catalog)),
            "An obsolete full background/program is still referenced outside an original preview")
    require(set(preview_files(catalog)) == set(preview_files(original_catalog))
            and all(file_sha(exact_file(staging, relative)) == source_hashes[relative] for relative in set(preview_files(original_catalog))),
            "Every original preview must remain byte-identical")
    restored = copy.deepcopy(catalog)
    for original_module, module in zip(original_catalog["modules"], restored["modules"]):
        if module["id"] == "frame":
            for original_choice, choice in zip(original_module["choices"], module["choices"]):
                for original_part, part in zip((original_choice, *original_choice.get("variants", [])), (choice, *choice.get("variants", []))):
                    for field in ("scene", "manifest", "ini"):
                        part[field] = original_part[field]
    require(restored == original_catalog and manifest["options"][:-1] == original_manifest["options"],
            "The migration changed an original choice, preview, default or setting")
    require(all(file_sha(exact_file(source, relative)) == digest for relative, digest in source_hashes.items())
            and file_sha(archive) == identity["archiveSha256"], "The preserved parent or its old archive changed")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    require(audit["options"] == 28 and not audit["aggregateViolations"] and len({part["scene"] for part in transformed.values()}) == 45,
            "The complete column closure must retain existing limits,28 options and45 Frame contributions")
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "reviewedColumn": {"path": str(reviewed["path"]), "sha256": reviewed["sha256"], "setting": reviewed["setting"],
            "mappingSemantics": "Reviewed OMS RGB direct visual adaptation; the native internal blending algorithm is not established by screenshot residuals"},
        "columnSourceReference": {"path": str(Path(column_reference_path).resolve()), "sha256": file_sha(column_reference_path)},
        "columnUpgrade": receipts, "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedExistingMedia": [], "retiredFiles": sorted(retired), "oldBitmapPathsStillUsedOnlyForPreview": sorted(retired_media.intersection(files)),
            "catalogChoices": sum(len(module["choices"]) for module in catalog["modules"]),
            "previewFiles": len(preview_files(catalog))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"}, "budgets": audit["selectedUpperBound"],
        "unifiedOptionCount": audit["options"], "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
            "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"],
        "scope": "Only source lane_1p/lane_2p column meshes use the reviewed scalar; lane_base, frame, beam, all source alpha, masks, UVs,"
                 " entrance gates and lane-opening clips are preserved. Old full-screen C4 game textures retire from the new selected closure."}
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--mode", choices=("probe", "merge", "upgrade"), default="probe")
    for name in ("source", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    for name in ("library", "recording", "bemani-root", "image-tools", "column-reference", "reviewed-column"):
        parser.add_argument("--" + name, type=Path)
    parser.add_argument("--merge-reference", type=Path, action="append")
    parser.add_argument("--frame", choices=tuple(SOURCE_PACKAGES), action="append")
    parser.add_argument("--layout", choices=LAYOUTS, action="append")
    parser.add_argument("--frame-research", action="append", metavar="FRAME=DIRECTORY")
    parser.add_argument("--persistent-reference", type=Path,
                        help="Exact current starless/laneless source-reference JSON; required when the selected author owns those backgrounds")
    args = parser.parse_args()
    if args.mode == "upgrade":
        if not args.column_reference or not args.reviewed_column:
            parser.error("Upgrade requires the complete --column-reference and actual --reviewed-column JSON")
        upgrade(args.source, args.column_reference, args.reviewed_column, args.output)
        return
    if args.mode == "merge":
        if not args.merge_reference:
            parser.error("Merge requires completed --merge-reference JSON inputs")
        merge_references(args.merge_reference, args.source, args.output)
        return
    if not all((args.library, args.recording, args.bemani_root, args.image_tools, args.frame, args.frame_research)):
        parser.error("Probe requires explicit library, recording, source tools, selected Frame and per-Frame research directories")
    research = {}
    for value in args.frame_research:
        frame, separator, path = value.partition("=")
        if not separator or frame not in SOURCE_PACKAGES or frame in research:
            parser.error("Use one explicit research directory per selected source Frame")
        research[frame] = Path(path).resolve(strict=True)
    result = export_reference(args.library, args.source, args.recording, args.output, frame_research=research,
                              bemani_root=args.bemani_root, image_tools=args.image_tools, frames=tuple(args.frame),
                              layouts=tuple(args.layout or LAYOUTS), persistent_reference=args.persistent_reference)
    raise SystemExit(0 if result["status"] == "static-probe-complete" else 1)


if __name__ == "__main__":
    main()
