"""Correct a frozen Omni LIFT catalogue without transforming its contributions.

Remove the author-added black-only choice, default to artwork000 and put LIFT
immediately after Lane Cover. Existing author directories and archives are read-only.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_omni_skin import (NAME, audit_package, author_directory_budget, calibrate_lift_art,
                            copy_exact, exact_file, package_closure, preview_files,
                            publish_prepared_package, read_json, write_json)
from lighting_persistent import _independent


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def upgrade(source: Path, output: Path):
    source = _independent(source)
    output = _independent(output, output=True, exists=False)
    archive = source.with_suffix(".osk")
    if (output.parent != source.parent or output == source or output.exists()
            or output.with_suffix(".osk").exists()):
        raise ValueError("Use a fresh sibling author directory and archive")
    generation = read_json(exact_file(source, "generation.json"))
    source_archive_sha = sha(archive)
    if generation["archive"]["sha256"] != source_archive_sha:
        raise ValueError("The parent archive differs from its generation receipt")
    source_files = package_closure(source)
    source_hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    source_generation_sha = sha(source / "generation.json")
    original_catalog = read_json(exact_file(source, "gameplay/content.json"))
    catalog = copy.deepcopy(original_catalog)
    correction = calibrate_lift_art(catalog)
    if correction.get("status") == "not-included":
        raise ValueError("The frozen author must include the Omni LIFT artworks")
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    if set(files) - set(source_files):
        raise ValueError("A catalogue correction cannot add payload files")
    changed = [relative for relative in files if sha(exact_file(staging, relative)) != source_hashes[relative]]
    if set(changed) - {"gameplay/content.json"}:
        raise ValueError("The LIFT correction changed a contribution or media file")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    if audit["aggregateViolations"]:
        raise ValueError(f"The corrected author exceeds its package budgets: {audit['aggregateViolations']}")
    if (sha(archive) != source_archive_sha or sha(source / "generation.json") != source_generation_sha
            or any(sha(exact_file(source, relative)) != source_hashes[relative] for relative in source_files)):
        raise ValueError("The read-only parent changed during the catalogue correction")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation",
        "repackagedFrom": str(source), "sourceIdentity": {"archiveSha256": source_archive_sha,
            "generationSha256": source_generation_sha, "catalogSha256": source_hashes["gameplay/content.json"]},
        "liftArtDefault": correction,
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedFiles": changed, "retiredFiles": sorted(set(source_files) - set(files)),
            "retainedPayloadByteEqual": len(files) - len(changed),
            "catalogChoices": sum(len(module["choices"]) for module in catalog["modules"]),
            "previewFiles": len(preview_files(catalog))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"],
            "configurationBytes": audit["configurationBytes"]}, "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.output)


if __name__ == "__main__":
    main()
