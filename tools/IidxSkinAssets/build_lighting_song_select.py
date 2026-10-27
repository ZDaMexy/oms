"""Prepare a fresh native SongSelect sibling; do not pack an OSK or modify its parent."""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import sys
import uuid

sys.dont_write_bytecode = True

from PIL import Image

from build_omni_skin import author_directory_budget, exact_file, package_closure, read_json
from lighting_persistent import _independent
from lighting_song_select import ASSETS, NAME, PACKAGE, PACKAGE_SHA256, PAGE_FILE, adaptation_receipt, append_song_select, require


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def inventory(root, files):
    return [{"path": path, "bytes": exact_file(root, path).stat().st_size, "sha256": sha(exact_file(root, path))} for path in sorted(files)]


def identity(rows):
    return hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def verify_sources(library):
    index = read_json(library / "library.json")
    package = next(item for item in index["packages"] if item["source"] == PACKAGE)
    require(package["sha256"] == PACKAGE_SHA256 and package["sourceUnchanged"], "Use the verified original main-screen mselect package")
    textures = {(row["package"], row["name"]): row for row in index["textures"]}
    rows = []
    for asset in ASSETS:
        item = textures[(PACKAGE, asset.name)]
        source = exact_file(library, asset.source)
        require(item["sprite"] == asset.source and (item["width"], item["height"]) == (asset.width, asset.height)
                and item["sha256"] == asset.sha256 == sha(source), "A visually audited native SongSelect source changed")
        with Image.open(source) as image:
            require(image.format == "PNG" and image.size == (asset.width, asset.height), "Copy the exact original PNG canvas")
            rgba = image.convert("RGBA").tobytes()
        rows.append({"id": asset.identifier, "path": asset.destination, "source": asset.source,
                     "package": PACKAGE, "sourcePackageSha256": PACKAGE_SHA256, "sha256": asset.sha256,
                     "rgbaSha256": hashlib.sha256(rgba).hexdigest(), "bytes": source.stat().st_size,
                     "width": asset.width, "height": asset.height, "imageRect": item["imageRect"], "uvRect": item["uvRect"],
                     "role": asset.role, "originalPlacementOrTimingProven": False})
    return rows


def upgrade(source, output, library, evidence):
    source, library = (_independent(path) for path in (source, library))
    output = _independent(output, output=True, exists=False)
    evidence = _independent(evidence, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists() and not output.with_suffix(".osk").exists(),
            "Use a new sibling author and preserve every old author/archive")
    require(not evidence.exists() and evidence.parent.is_dir(), "Use a fresh evidence directory")
    for owner in (source, library):
        require(not evidence.is_relative_to(owner) and not owner.is_relative_to(evidence)
                and not output.is_relative_to(owner) and not owner.is_relative_to(output), "Inputs and outputs must be disjoint")
    parent_generation = read_json(source / "generation.json")
    require(parent_generation["name"] == NAME, "Preserve the existing work name")
    general = (source / "skin.ini").read_text("utf-8").split("[GameplaySkin.", 1)[0]
    require("Name: " + NAME + "\n" in general, "Do not rename the work in skin.ini")
    files = package_closure(source)
    before = inventory(source, files)
    untouched = [path for path in files if path != PAGE_FILE]
    original_pages = exact_file(source, PAGE_FILE).read_bytes()
    pages = append_song_select(original_pages)
    resources = verify_sources(library)
    require(not set(files).intersection(asset.destination for asset in ASSETS), "New SongSelect paths collide with parent payload")
    source_meta = {str(path): sha(path) for path in (source / "generation.json", library / "library.json")}
    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    require(not staging.exists(), "Preserve existing staging")
    evidence.mkdir()
    staging.mkdir()
    write_json(evidence / "parent-payload-sha256.json", before)
    print(f"Copying {len(files)} parent payload files without reencoding", flush=True)
    for relative in files:
        target = staging / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, relative), target)
    for asset in ASSETS:
        target = staging / asset.destination
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(library, asset.source), target)
    (staging / PAGE_FILE).write_bytes(pages)
    require(inventory(source, untouched) == inventory(staging, untouched), "An existing gameplay or Results resource changed")
    require(json.loads(original_pages)["results"] == read_json(staging / PAGE_FILE)["results"], "Existing Results semantics changed")
    require((staging / PAGE_FILE).read_bytes().startswith(original_pages.decode("utf-8").rstrip()[:-1].encode("utf-8")), "Existing Results JSON bytes changed")
    for row in resources:
        require(sha(staging / row["path"]) == row["sha256"], "A copied original PNG changed")
    complete = package_closure(staging)
    require(set(files).issubset(complete), "A parent dependency disappeared")
    rows = inventory(staging, complete)
    page_document = read_json(staging / PAGE_FILE)
    page_resources = [resource for page in (page_document["results"], page_document["songSelect"]) for resource in page["resources"]]
    pixels = encoded = 0
    for resource in page_resources:
        path = exact_file(staging, resource["path"])
        encoded += path.stat().st_size
        with Image.open(path) as image:
            pixels += image.width * image.height
    require(len(page_resources) <= 32 and pixels <= 16 * 1024 * 1024 and encoded <= 32 * 1024 * 1024,
            "Do not enlarge the public aggregate page budgets")
    require(before == inventory(source, files) and source_meta == {path: sha(Path(path)) for path in source_meta}, "A read-only parent/source metadata changed")
    require(all(sha(exact_file(library, row["source"])) == row["sha256"] for row in resources), "A source PNG changed")
    preservation = {"allExistingFilesExceptAppendedPageBytesUnchanged": True, "allParentGameplayBytesUnchanged": True,
        "existingResultsJsonPrefixAndSemanticsUnchanged": True, "existingResultsPNGBytesUnchanged": True,
        "skinIniAndPreviewAndC4MarkerBytesUnchanged": True, "sourceAuthorUnchanged": True, "originalPNGsUnchanged": True,
        "parentClosureSha256": identity(before), "parentAfterClosureSha256": identity(inventory(source, files)),
        "untouchedPayloadSha256": identity(inventory(staging, untouched)), "newFonts": 0, "gameplayOptionChanges": 0}
    page_budget = {"resources": len(page_resources), "encodedBytes": encoded, "texturePixels": pixels,
                   "declarationBytes": len(pages), "songSelectOriginalPNGs": len(resources),
                   "sharedCodecValidation": "pending", "nativeValidation": "pending"}
    receipt = {"name": NAME, "status": "prepared-native-songselect-awaiting-shared-codec-and-native",
        "createdUtc": datetime.now(timezone.utc).isoformat(), "repackagedFrom": str(source),
        "songSelect": adaptation_receipt(), "pageBudget": page_budget, "sourceMetadataSha256": source_meta,
        "sourcePreservation": preservation, "closure": {"files": len(rows), "uncompressedBytes": sum(row["bytes"] for row in rows), "sha256": identity(rows)},
        "authorDirectory": author_directory_budget(complete),
        "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk; Asia/Shanghai date, next unused daily sequence"}}
    write_json(staging / "generation.json", receipt)
    write_json(staging / "package-audit.json", {"status": "pending-current-shared-audit", "pageBudget": page_budget,
               "sourcePreservation": preservation, "closure": receipt["closure"]})
    write_json(evidence / "page-resource-sources.json", resources)
    write_json(evidence / "source-preservation.json", preservation)
    write_json(evidence / "output-payload-sha256.json", rows)
    write_json(evidence / "generation.json", receipt)
    (evidence / PAGE_FILE).write_bytes(pages)
    staging.rename(output)
    print(f"Prepared {output}; no OSK, shared codec or runtime validation performed", flush=True)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "output", "library", "evidence"):
        parser.add_argument("--" + name, required=True, type=Path)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.library, args.evidence)


if __name__ == "__main__":
    main()
