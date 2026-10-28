"""Prepare a fresh Omni sibling with true chart header text; never pack or run the game."""
from __future__ import annotations

import argparse
import copy
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import sys
import uuid

sys.dont_write_bytecode = True

from build_lighting_details import store_json
from build_omni_skin import (NAME, audit_package, author_directory_budget, exact_file,
                            package_closure, preview_files, read_json)
from lighting_header import append_header, identifiers, require
from lighting_persistent import _independent


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8")


def inventory(root, files):
    return [{"path": path, "bytes": exact_file(root, path).stat().st_size, "sha256": sha(exact_file(root, path))}
            for path in sorted(files)]


def identity(rows):
    return hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def upgrade(source, output, reference, evidence):
    source, reference = (_independent(path) for path in (source, reference))
    output = _independent(output, output=True, exists=False)
    evidence = _independent(evidence, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists()
            and not output.with_suffix(".osk").exists(), "Create a strictly new sibling author")
    require(not evidence.exists() and evidence.parent.is_dir() and not evidence.is_relative_to(source)
            and not evidence.is_relative_to(output), "Use a separate fresh evidence directory")
    require(read_json(source / "generation.json")["name"] == NAME, "Preserve the original work name")
    files = package_closure(source)
    before = inventory(source, files)
    source_metadata = {str(path): sha(path) for path in (source / "generation.json", source / "package-audit.json", reference)}
    if source.with_suffix(".osk").is_file():
        source_metadata[str(source.with_suffix(".osk"))] = sha(source.with_suffix(".osk"))
    mapping = read_json(reference)
    original_catalog = read_json(source / "gameplay/content.json")
    catalog = copy.deepcopy(original_catalog)
    frame = next(module for module in catalog["modules"] if module["id"] == "frame")
    choices = [choice for choice in frame["choices"] if choice["value"] == "iidx33"]
    require(len(choices) == 1, "Use the original Sparkle Shower Frame choice")
    occupied = set()
    for relative in files:
        if relative.endswith(".json"):
            occupied.update(identifiers(read_json(exact_file(source, relative))))
    choice = choices[0]
    transformed, changes = {}, []
    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    evidence.mkdir()
    staging.mkdir()
    write_json(evidence / "parent-payload-sha256.json", before)
    print(f"Copying {len(files)} frozen parent payload files", flush=True)
    for relative in files:
        destination = staging / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, relative), destination)
    prefixes = {"p1": "hx.p1", "p2": "hx.p2", "dp": "hx.dp"}
    for part in (choice, *choice.get("variants", [])):
        previous = part["scene"]
        layout = part.get("when", {}).get("layout", "dp")
        if previous not in transformed:
            native_layouts = read_json(exact_file(source, part["manifest"]))["layouts"]
            mod_rectangles = {tuple(profile["modsRect"]) for profile in native_layouts if profile["ruleset"] == "bms"}
            require(len(mod_rectangles) == 1, "Use the exact selected BMS profiles' shared native Mod display rectangle")
            updated, receipt = append_header(read_json(exact_file(source, previous)), mapping, layout, prefixes[layout], occupied,
                                             mods_rect=next(iter(mod_rectangles)))
            replacement = store_json(staging, "s", updated)
            transformed[previous] = (replacement, layout)
            occupied.update(receipt["addedIds"])
            changes.append({**receipt, "previousScene": previous, "scene": replacement,
                            "previousSha256": sha(source / previous), "sha256": sha(staging / replacement),
                            "sceneByteIncrement": (staging / replacement).stat().st_size - (source / previous).stat().st_size})
        require(transformed[previous][1] == layout, "An existing scene is shared by different header layouts")
        part["scene"] = transformed[previous][0]
    require({row["layout"] for row in changes} == {"p1", "p2", "dp"}, "Preserve all three exact layout branches")
    restored = copy.deepcopy(catalog)
    reverse = {value[0]: key for key, value in transformed.items()}
    for module in restored["modules"]:
        for item in module["choices"]:
            for part in (item, *item.get("variants", [])):
                if part.get("scene") in reverse:
                    part["scene"] = reverse[part["scene"]]
    require(restored == original_catalog, "Only the three existing Frame scene paths may change")
    write_json(staging / "gameplay/content.json", catalog)
    complete = package_closure(staging)
    retired = sorted(set(files) - set(complete))
    require(set(retired) == set(transformed), "No unrelated payload may be retired")
    for relative in retired:
        (staging / relative).unlink()
    unchanged = sorted(set(files).intersection(complete) - {"gameplay/content.json"})
    require(inventory(source, unchanged) == inventory(staging, unchanged), "An unrelated public payload changed")
    require(preview_files(original_catalog) == preview_files(catalog), "All original preview references must be retained")
    require(before == inventory(source, files) and all(sha(Path(path)) == digest for path, digest in source_metadata.items()),
            "The frozen source author, archive or reference changed")
    print("Computing closure and conservative selected-author budgets; codec and native checks remain pending", flush=True)
    audit = audit_package(staging, complete)
    require(not audit["aggregateViolations"], "Do not relax an existing author budget")
    audit["authorDirectory"] = author_directory_budget(complete)
    after = inventory(staging, complete)
    pages = [row for row in before if row["path"] == "skin-pages.json" or row["path"].startswith("pages/")]
    preservation = {"parentClosureSha256": identity(before), "parentAfterClosureSha256": identity(inventory(source, files)),
        "untouchedPayloadFiles": len(unchanged), "untouchedPayloadSha256": identity(inventory(staging, unchanged)),
        "allOriginalPNGsByteIdentical": True, "originalPNGCount": sum(path.endswith(".png") for path in files),
        "allOriginalINIBytesUnchanged": True, "allOriginalPreviewsAndPublicC4IdsUnchanged": True,
        "bothPagesJsonAndPNGBytesUnchanged": True, "pagePayloadSha256": identity(pages),
        "sourceAuthorAndArchiveUnchanged": True, "allNon33FrameAndOtherModulePayloadUnchanged": True,
        "newBitmapImages": 0, "newBitmapFonts": 0, "newOptions": 0, "newDataSources": 0,
        "newActionsOrScripts": 0, "parentGenerationSha256": source_metadata[str(source / "generation.json")]}
    receipt = {"name": NAME, "status": "prepared-real-chart-header-awaiting-shared-codec-and-native",
        "createdUtc": datetime.now(timezone.utc).isoformat(), "repackagedFrom": str(source),
        "sourceMetadataSha256": source_metadata, "headerConsumers": changes,
        "scope": "Only iidx33 Frame has verified header consumers here. Other fourteen Frame choices are preserved without claiming this fix.",
        "sourceArt": "No images copied, generated or edited. Recorded gmframe33 diff/lv/lvlnum bounds guide native OMS text only; placeholder HYPER/12 pixels are never baked as chart data.",
        "nativeReview": ["Real BMS HYPER/1, empty declarations, long level, P1/P2/DP and mania isolation",
                         "Use real nonempty Mods to verify the DP level adaptation remains separate from the unchanged native ModDisplay"],
        "sourcePreservation": preservation, "closure": {"files": len(after), "uncompressedBytes": sum(row["bytes"] for row in after), "sha256": identity(after)},
        "selectedUpperBound": audit["selectedUpperBound"], "authorDirectory": audit["authorDirectory"],
        "runtimeBudget": {"additionalReservedTextGlyphsPerSelected33Frame": 512, "additionalInformationInstances": 0,
                          "informationMarkerCountPerSelectedLayout": 1, "sharedRuntimeValidation": "pending"},
        "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk; Asia/Shanghai date, next unused daily sequence"}}
    diff = {"changed": ["gameplay/content.json"], "removed": [row for row in before if row["path"] in retired],
            "added": [row for row in after if row["path"] not in files], "unchangedFiles": len(unchanged),
            "beforeCatalogSha256": sha(source / "gameplay/content.json"), "catalogSha256": sha(staging / "gameplay/content.json"),
            "allOriginalTypedSceneIdsPreserved": True}
    write_json(staging / "generation.json", receipt)
    write_json(staging / "package-audit.json", audit)
    write_json(evidence / "source-preservation.json", preservation)
    write_json(evidence / "source-hooks.json", {"source": str(reference), "sha256": sha(reference), "consumers": changes})
    write_json(evidence / "payload-diff.json", diff)
    write_json(evidence / "output-payload-sha256.json", after)
    write_json(evidence / "generation.json", receipt)
    write_json(evidence / "package-audit.json", audit)
    staging.rename(output)
    print(f"Prepared {output}; no OSK, tests, shared codec or runtime validation performed", flush=True)
    print(json.dumps({"closure": receipt["closure"], "selectedUpperBound": receipt["selectedUpperBound"],
                      "authorDirectory": receipt["authorDirectory"]}, separators=(",", ":")), flush=True)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "output", "reference", "evidence"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.reference, args.evidence)


if __name__ == "__main__":
    main()
