"""Upgrade verified Omni components in a fresh private author closure."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import uuid

from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                             package_closure, publish_prepared_package, read_json, write_json)
from build_lighting_details import store_json
from lighting_local_ids import compact_local_ids
from lighting_motion import retime_turntable_idle
import lighting_persistent


def upgrade(source, reference_path, output):
    source = Path(source).resolve(strict=True)
    reference = lighting_persistent.load_reference(reference_path)
    output = Path(output).resolve()
    for path in (source, output):
        if path.drive.lower() in ("c:", "d:"):
            raise ValueError("Use independent author directories on a development drive")
    if output.exists() or output.with_suffix(".osk").exists() or not output.parent.is_dir():
        raise ValueError("Choose a fresh author output with an existing parent")
    for root in (source, reference["directory"], reference["library"]):
        if output.is_relative_to(root) or root.is_relative_to(output):
            raise ValueError("The output overlaps a preserved input")
    if source != reference["sourceAuthor"]:
        raise ValueError("The reference must have been compared with this exact source author")

    source_files = package_closure(source)
    source_hashes = {relative: hashlib.sha256(exact_file(source, relative).read_bytes()).hexdigest()
                     for relative in source_files}
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(source / relative, staging / relative)
    catalog = read_json(staging / "gameplay/content.json")
    transformed, prepared, receipts = {}, {}, []
    for module in catalog["modules"]:
        if module["id"] not in ("frame", "turntable", "charge-notes", "judgement"):
            continue
        for choice in module["choices"]:
            if module["id"] == "frame" and choice["value"] not in reference["frames"]:
                continue
            for part in (choice, *choice.get("variants", [])):
                identity = (module["id"], choice["value"], part["scene"], part["manifest"])
                if identity not in transformed:
                    scene = read_json(exact_file(staging, part["scene"]))
                    manifest = read_json(exact_file(staging, part["manifest"]))
                    if module["id"] == "frame":
                        frame = choice["value"]
                        owners = [(layout, lighting_persistent.background_owner(scene, manifest, frame=frame, layout=layout))
                                  for layout in lighting_persistent.MOVIES
                                  if any(instance["material"]["slot"] == "stage.background"
                                         and instance["material"].get("resource") == f"gameplay/frames/{frame}/backgrounds/{layout}"
                                         for instance in scene["instances"])]
                        if len(owners) != 1:
                            raise ValueError("Each selected Frame contribution must own exactly one layout")
                        layout = owners[0][0]
                        key = (frame, layout, part["manifest"])
                        if key not in prepared:
                            prepared[key] = lighting_persistent.prepare_assets(reference, staging, frame=frame,
                                layout=layout, manifest=manifest)
                        receipt = lighting_persistent.inject(scene, manifest, frame=frame, layout=layout, assets=prepared[key])
                    elif module["id"] == "turntable":
                        receipt = retime_turntable_idle(scene)
                    else:
                        receipt = compact_local_ids(scene, manifest, module=module["id"])
                    transformed[identity] = {"manifest": store_json(staging, "m", manifest),
                                             "scene": store_json(staging, "s", scene)}
                    receipts.append({"module": module["id"], "choice": choice["value"], **receipt})
                part.update(transformed[identity])
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    changed_media = [relative for relative in files if relative in source_hashes
                     and not relative.endswith((".json", ".ini"))
                     and hashlib.sha256((staging / relative).read_bytes()).hexdigest() != source_hashes[relative]]
    if changed_media:
        raise ValueError("Existing source media changed: " + ", ".join(changed_media))
    if any(hashlib.sha256((source / relative).read_bytes()).hexdigest() != sha for relative, sha in source_hashes.items()):
        raise ValueError("The preserved source author changed during the upgrade")
    audit = audit_package(staging, files)
    audit["authorDirectory"] = author_directory_budget(files)
    write_json(staging / "package-audit.json", audit)
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceGenerationSha256": hashlib.sha256((source / "generation.json").read_bytes()).hexdigest(),
        "componentReference": str(Path(reference_path).resolve()), "componentUpgrade": receipts,
        "sourcePreservation": {"changedExistingMedia": changed_media, "originalSourceClosureUnchanged": True,
            "retiredFiles": sorted(set(source_files) - set(files))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "closure": {"files": len(files),
            "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"],
        "scope": "Independent private Omni artwork; no player data, game installation or gameplay contracts changed."}
    write_json(staging / "generation.json", receipt)
    if audit["aggregateViolations"]:
        raise ValueError(f"The upgraded closure exceeds existing budgets: {audit['aggregateViolations']}")
    publish_prepared_package(staging, output, receipt, files, audit)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("source", "reference", "output"):
        parser.add_argument("--" + name, type=Path, required=True)
    args = parser.parse_args()
    upgrade(args.source, args.reference, args.output)


if __name__ == "__main__":
    main()
