"""Prepare an independent results-page sibling; never pack an OSK or alter gameplay bytes."""
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
from lighting_results_page import (ASSETS, CONTRACT, NAME, PAGE_FILE, RESULT_PACKAGE, adaptation_receipt,
                                   page_member_bytes, replace_results_page, require, results_page)


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def write_json(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":")) + "\n", encoding="utf-8")


def inventory(root, files):
    return [{"path": relative, "bytes": exact_file(root, relative).stat().st_size, "sha256": sha(exact_file(root, relative))}
            for relative in sorted(files)]


def identity(rows):
    return hashlib.sha256(json.dumps(rows, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()


def physical_files(root):
    return sorted(path.relative_to(root).as_posix() for path in root.rglob("*") if path.is_file())


def difference(before, after):
    old, new = ({row["path"]: row for row in rows} for rows in (before, after))
    return {"added": [new[path] for path in sorted(new.keys() - old.keys())],
            "removed": [old[path] for path in sorted(old.keys() - new.keys())],
            "changed": [{"path": path, "before": old[path], "after": new[path]}
                        for path in sorted(old.keys() & new.keys()) if old[path] != new[path]],
            "unchangedFiles": sum(old[path] == new[path] for path in old.keys() & new.keys())}


def raw_crop_evidence(library, item, sprite_rgba, export_hashes):
    raw_path = exact_file(library, item["raw"])
    raw_sha = sha(raw_path)
    require(export_hashes[item["raw"]]["sha256"] == raw_sha, "The decoded original texture differs from its extraction receipt")
    image_rect, uv_rect = item["imageRect"], item["uvRect"]
    crop = (uv_rect[0] - image_rect[0], uv_rect[2] - image_rect[2],
            uv_rect[1] - image_rect[0], uv_rect[3] - image_rect[2])
    with Image.open(raw_path) as raw:
        require(raw.format == "PNG" and raw.size == (image_rect[1] - image_rect[0], image_rect[3] - image_rect[2]),
                "The original texture and image rectangle disagree")
        require(0 <= crop[0] < crop[2] <= raw.width and 0 <= crop[1] < crop[3] <= raw.height,
                "The original UV rectangle escapes its decoded texture")
        raw_rgba = raw.convert("RGBA")
        cropped = raw_rgba.crop(crop)
        require(cropped.size == (item["width"], item["height"]) and cropped.tobytes() == sprite_rgba,
                "Original texture pixels, including transparent RGB, differ from the cropped sprite")
        return {"source": item["raw"], "sha256": raw_sha, "bytes": raw_path.stat().st_size,
                "width": raw.width, "height": raw.height, "crop": list(crop),
                "borders": [crop[0], crop[1], raw.width - crop[2], raw.height - crop[3]],
                "rawRgbaSha256": hashlib.sha256(raw_rgba.tobytes()).hexdigest(),
                "croppedRgbaSha256": hashlib.sha256(sprite_rgba).hexdigest(), "allRgbaBytesEqual": True}


def verify_sources(library, index):
    library_index = read_json(library / "library.json")
    textures = {(item["package"], item["name"]): item for item in library_index["textures"]}
    packages = {item["source"]: item for item in library_index["packages"]}
    require(RESULT_PACKAGE in packages and packages[RESULT_PACKAGE]["sourceUnchanged"], "Use the verified main result package")
    evidence = read_json(index / "summary.json")
    require(evidence["status"] == "complete" and evidence["allThreeSourceHashesMatch"]
            and evidence["originalSourceMetadataUnchanged"] and evidence["stagingSourceMetadataUnchanged"],
            "Use the independently verified main-screen extraction evidence")
    verified = read_json(index / "source-post-verification.json")
    result = next(item for item in verified if item["name"] == "result.ifs")
    require(result["allMatch"] and result["originalSha256"] == packages[RESULT_PACKAGE]["sha256"],
            "The original result package receipt and library must identify the same source")
    candidates = {(item["package"], item["name"]): item for item in read_json(index / "results-texture-candidates.json")["textures"]}
    export_hashes = {row["path"]: row for row in read_json(index / "output-sha256.json")}
    rows = []
    for asset in ASSETS:
        item = textures[(RESULT_PACKAGE, asset.name)]
        require(item["sprite"] == asset.source and (item["width"], item["height"]) == (asset.width, asset.height),
                "A main-result sprite changed dimensions or package")
        source = exact_file(library, asset.source)
        require(sha(source) == item["sha256"], "The main-result sprite differs from its extracted library receipt")
        if (RESULT_PACKAGE, asset.name) in candidates:
            candidate = candidates[(RESULT_PACKAGE, asset.name)]
            require(candidate["sprite"] == asset.source and candidate["sha256"] == item["sha256"], "The curated candidate and library disagree")
        with Image.open(source) as image:
            require(image.format == "PNG" and image.size == (asset.width, asset.height), "Use the exact original cropped PNG")
            image.load()
            rgba = image.convert("RGBA").tobytes()
            if asset.identifier == "neutral":
                require(image.convert("RGBA").getchannel("A").getextrema() == (255, 255), "The neutral black multiplier needs the verified opaque source")
        raw_pixels = raw_crop_evidence(library, item, rgba, export_hashes)
        rows.append({"id": asset.identifier, "path": asset.destination, "source": asset.source,
                     "package": RESULT_PACKAGE, "sourcePackageSha256": packages[RESULT_PACKAGE]["sha256"],
                     "sha256": item["sha256"], "bytes": source.stat().st_size, "width": asset.width, "height": asset.height,
                     "imageRect": item["imageRect"], "uvRect": item["uvRect"], "role": asset.role,
                     "rgbaSha256": hashlib.sha256(rgba).hexdigest(), "rawPixels": raw_pixels,
                     "curatedCandidate": (RESULT_PACKAGE, asset.name) in candidates,
                     "nativePlacementProven": False})
    require(sum(row["bytes"] for row in rows) <= 32 * 1024 * 1024, "Page encoded resources exceed the public32MiB budget")
    require(sum(row["width"] * row["height"] for row in rows) <= 16 * 1024 * 1024, "Page pixels exceed the public16M budget")
    return rows


def upgrade(source, output, library, source_index, evidence):
    source, library, source_index = (_independent(path) for path in (source, library, source_index))
    output = _independent(output, output=True, exists=False)
    evidence = _independent(evidence, output=True, exists=False)
    require(output.parent == source.parent and output != source and not output.exists() and not output.with_suffix(".osk").exists(),
            "Create a fresh sibling author, never overwrite an author or archive")
    require(not evidence.exists() and evidence.parent.is_dir(), "Use a fresh evidence directory")
    for owner in (source, library, source_index):
        require(not evidence.is_relative_to(owner) and not owner.is_relative_to(evidence)
                and not output.is_relative_to(owner) and not owner.is_relative_to(output), "Source and output ownership must remain disjoint")
    generation = read_json(source / "generation.json")
    require(generation["name"] == NAME, "Preserve the existing Until33 work name")
    general = (source / "skin.ini").read_text(encoding="utf-8").split("[GameplaySkin.", 1)[0]
    require("Name: " + NAME + "\n" in general, "The source skin name must remain unchanged")
    files = package_closure(source)
    physical = physical_files(source)
    print(f"Capturing immutable parent payload: {len(files)} files", flush=True)
    before = inventory(source, files)
    physical_before = inventory(source, physical)
    meta_files = (library / "library.json", source_index / "summary.json", source_index / "source-post-verification.json",
                  source_index / "results-texture-candidates.json", source_index / "output-sha256.json", source / "generation.json")
    metadata_before = {str(path): sha(path) for path in meta_files}
    resources = verify_sources(library, source_index)
    previous = (source / PAGE_FILE).read_bytes() if (source / PAGE_FILE).exists() else None
    old_document = json.loads(previous) if previous is not None else {"contract": CONTRACT}
    declaration = replace_results_page(previous, results_page()["results"])
    document = json.loads(declaration)
    original_members = page_member_bytes(previous) if previous is not None else {}
    new_members = page_member_bytes(declaration)
    preserved_members = {key: value for key, value in original_members.items() if key != "results"}
    require(all(new_members[key] == value for key, value in preserved_members.items()), "An unrelated page JSON value changed")

    def page_paths(page):
        return {resource["path"] for resource in page.get("resources", [])}

    old_result_paths = page_paths(old_document.get("results", {}))
    other_page_paths = {path for key, page in old_document.items() if key not in ("contract", "results") for path in page_paths(page)}
    new_result_paths = {asset.destination for asset in ASSETS}
    for asset in ASSETS:
        require(asset.destination not in physical or asset.destination in old_result_paths,
                "A new Results destination collides with an unrelated parent file: " + asset.destination)
    staging = output.with_name("." + output.name + ".preparing-" + uuid.uuid4().hex)
    require(not staging.exists(), "Staging already exists")
    evidence.mkdir()
    staging.mkdir()
    write_json(evidence / "parent-payload-sha256.json", before)
    write_json(evidence / "parent-physical-sha256.json", physical_before)
    print("Copying every parent file; only Results owns the planned replacements", flush=True)
    for relative in physical:
        if relative == PAGE_FILE:
            continue
        destination = staging / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(source, relative), destination)

    # This new staging tree has no pages yet. The existing generic enumerator therefore gives the
    # genuine gameplay closure without duplicating its manifest/INI/C2 traversal or touching the parent.
    gameplay_files = package_closure(staging)
    gameplay_before = inventory(source, gameplay_files)
    require(not {"generation.json", "package-audit.json"}.intersection(gameplay_files), "Audit metadata cannot be a gameplay resource")
    protected = set(gameplay_files) | other_page_paths
    for asset in ASSETS:
        if asset.destination in protected:
            require(sha(exact_file(staging, asset.destination)) == sha(exact_file(library, asset.source)),
                    "Results must not overwrite another page or gameplay resource")
    removed = old_result_paths - new_result_paths - protected
    for relative in sorted(removed):
        exact_file(staging, relative).unlink()
    for asset in ASSETS:
        destination = staging / asset.destination
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(exact_file(library, asset.source), destination)
    (staging / PAGE_FILE).write_bytes(declaration)
    copied = inventory(staging, gameplay_files)
    require(copied == gameplay_before, "A copied gameplay payload changed")
    untouched = set(physical) - {PAGE_FILE, "generation.json", "package-audit.json"} - old_result_paths
    require(inventory(staging, untouched) == inventory(source, untouched), "An unrelated parent file changed")
    require(inventory(staging, other_page_paths) == inventory(source, other_page_paths), "An unrelated page image changed")
    for row in resources:
        require(sha(staging / row["path"]) == row["sha256"], "A copied result bitmap changed")
    complete_files = package_closure(staging)
    budget = author_directory_budget(complete_files)
    all_rows = inventory(staging, complete_files)
    all_page_paths = {path for key, page in document.items() if key != "contract" for path in page_paths(page)}
    page_rows = [row for row in all_rows if row["path"] == PAGE_FILE or row["path"] in all_page_paths]
    require(len(all_rows) == len({row["path"] for row in all_rows}), "The package closure has duplicate paths")
    require(set(gameplay_files).issubset(complete_files), "The optional page lost an existing gameplay dependency")
    after = inventory(source, files)
    require(before == after and physical_before == inventory(source, physical_files(source))
            and metadata_before == {str(path): sha(path) for path in meta_files}, "A read-only input changed during generation")
    require(all(sha(exact_file(library, row["source"])) == row["sha256"] for row in resources), "A consumed source bitmap changed during generation")
    require(all(sha(exact_file(library, row["rawPixels"]["source"])) == row["rawPixels"]["sha256"] for row in resources),
            "A consumed decoded texture changed during generation")
    preservation = {"parentGameplayFiles": len(gameplay_files), "parentGameplayBytes": sum(row["bytes"] for row in gameplay_before),
        "parentGameplayClosureSha256": identity(gameplay_before), "copiedGameplayClosureSha256": identity(copied),
        "parentGameplayAfterSha256": identity(inventory(source, gameplay_files)),
        "parentClosureSha256": identity(before), "parentAfterSha256": identity(after),
        "allParentGameplayBytesUnchanged": True, "sourceAuthorUnchanged": True, "sourceMetadataUnchanged": True,
        "skinIniUnchanged": True, "allGameplayOptionsAndDefaultsUnchanged": True, "allGameplayPreviewsAndArtUnchanged": True,
        "allOtherParentFilesUnchanged": True, "preservedPageJsonValuesByteIdentical": True, "preservedPagePNGBytesUnchanged": True,
        "preservedPages": [{"page": key, "jsonValueBytes": len(value), "jsonValueSha256": hashlib.sha256(value).hexdigest(),
                            "resourcePaths": sorted(page_paths(old_document[key]))}
                           for key, value in preserved_members.items() if key != "contract"],
        "removedUnreferencedResultsPNGs": sorted(removed),
        "retainedOldResultsResourcesUsedElsewhere": sorted((old_result_paths - new_result_paths) & protected),
        "newPageIsOptionalAndOriginalClientPageRemainsDefault": True, "archiveReadOrWritten": False}
    merged_resources = [resource for key, page in document.items() if key != "contract" for resource in page["resources"]]
    require(len(merged_resources) <= 32 and len({row["id"] for row in merged_resources}) == len(merged_resources)
            and len({row["path"].casefold() for row in merged_resources}) == len(merged_resources),
            "The merged pages exceed their shared resource identity domain")
    encoded_bytes = texture_pixels = 0
    for resource in merged_resources:
        path = exact_file(staging, resource["path"])
        encoded_bytes += path.stat().st_size
        with Image.open(path) as image:
            image.load()
            texture_pixels += image.width * image.height
    require(encoded_bytes <= 32 * 1024 * 1024 and texture_pixels <= 16 * 1024 * 1024 and len(declaration) <= 64 * 1024,
            "The merged pages exceed the unchanged public budgets")
    page_budget = {"encodedBytes": encoded_bytes, "texturePixels": texture_pixels, "resources": len(merged_resources),
        "resultsResources": len(resources),
        "elements": len(document["results"]["elements"]),
        "textElements": sum(element["type"] == "text" for element in document["results"]["elements"]),
        "jsonBytes": (staging / PAGE_FILE).stat().st_size, "sharedCodecValidation": "pending", "runtimeValidation": "pending"}
    receipt = {"name": NAME, "status": "prepared-results-page-awaiting-shared-codec-and-native",
        "createdUtc": datetime.now(timezone.utc).isoformat(), "repackagedFrom": str(source),
        "sourceMetadataSha256": metadata_before, "resultsPage": adaptation_receipt(), "pageBudget": page_budget,
        "sourcePreservation": preservation, "closure": {"files": len(all_rows), "uncompressedBytes": sum(row["bytes"] for row in all_rows),
        "sha256": identity(all_rows)}, "authorDirectory": budget,
        "archive": {"status": "not-created", "deliveryNamePolicy": NAME + ".vyymmddxx.osk; Asia/Shanghai date, next unused daily sequence"}}
    write_json(staging / "generation.json", receipt)
    write_json(staging / "package-audit.json", {"status": "pending-current-shared-audit", "resultsPage": page_budget,
        "sourcePreservation": preservation, "closure": receipt["closure"], "authorDirectory": budget})
    physical_after = inventory(staging, physical_files(staging))
    physical_diff = difference(physical_before, physical_after)
    require({row["path"] for row in physical_diff["removed"]} == removed, "The author lost an unrelated parent file")
    require({row["path"] for row in physical_diff["added"]}.issubset(new_result_paths | {PAGE_FILE, "generation.json", "package-audit.json"})
            and {row["path"] for row in physical_diff["changed"]}.issubset(new_result_paths | {PAGE_FILE, "generation.json", "package-audit.json"}),
            "The Results update changed an unrelated author file")
    write_json(evidence / "parent-gameplay-sha256.json", gameplay_before)
    write_json(evidence / "results-page.json", document)
    (evidence / PAGE_FILE).write_bytes(declaration)
    write_json(evidence / "page-resource-sources.json", resources)
    write_json(evidence / "raw-crop-pixels.json", [{"sprite": row["source"], "spriteSha256": row["sha256"],
        "spriteRgbaSha256": row["rgbaSha256"], **row["rawPixels"]} for row in resources])
    write_json(evidence / "page-closure.json", {"contract": CONTRACT, "files": page_rows, "sha256": identity(page_rows), "budget": page_budget})
    write_json(evidence / "preservation.json", preservation)
    write_json(evidence / "output-closure.json", {"files": all_rows, "sha256": identity(all_rows)})
    write_json(evidence / "payload-diff.json", difference(before, all_rows))
    write_json(evidence / "physical-diff.json", {**physical_diff, "parentPhysicalSha256": identity(physical_before),
        "outputPhysicalSha256": identity(physical_after), "files": physical_after})
    write_json(evidence / "generation.json", receipt)
    staging.rename(output)
    print(f"Prepared {output}; {len(resources)} original result images, no OSK or runtime validation", flush=True)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for argument in ("source", "output", "library", "source-index", "evidence"):
        parser.add_argument("--" + argument, required=True, type=Path)
    args = parser.parse_args()
    upgrade(args.source, args.output, args.library, args.source_index, args.evidence)


if __name__ == "__main__":
    main()
