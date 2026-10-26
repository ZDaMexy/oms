"""Add reviewed FAST/SLOW C2 choices to a new private Omni sibling author.

No final author can be produced before --positions supplies hashed, reviewed
Position A/B/C observations/adaptations. Parent/media are read-only, all old
modules stay exact, no new fonts/INI/root resources are introduced. This tool
does not launch an HDD, read player data or validate original-game parity.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
from pathlib import Path
import uuid

from build_lighting_details import store_json
from build_lighting_gameplay_controls import audit_shared_root
from build_omni_skin import (NAME, audit_package, author_directory_budget, copy_exact, exact_file,
                            package_closure, preview_files, publish_prepared_package, read_json, write_json)
from lighting_persistent import _independent
import lighting_timing_display as timing


def sha(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def frame_geometry(source, catalog):
    frames, = [module for module in catalog["modules"] if module["id"] == "frame"]
    judgement, = [module for module in catalog["modules"] if module["id"] == "judgement"]
    default, = [choice for choice in judgement["choices"] if choice["value"] == judgement["default"]]
    result, scenes = {}, {}
    def geometry_signature(geometry):
        return {layout: {"material": host["material"], "x": host["x"], "y": host["y"],
            "hostWidth": host["hostWidth"], "hostHeight": host["hostHeight"], "brightnessSource": host["brightnessSource"],
            "position": [[(point["time"], point["value"]) for point in track["keyframes"]] for track in host["positionTracks"]]}
                for layout, host in geometry.items()}
    def selected_geometry(choice, frame, sizes):
        variants = [part for part in choice.get("variants", []) if part.get("when") == {"frame": frame}]
        timing.require(len(variants) == 1, "Use the existing exact Frame judgement selection")
        selected = variants[0]
        if selected["scene"] not in scenes:
            scenes[selected["scene"]] = read_json(exact_file(source, selected["scene"]))
        return selected, timing.judgement_geometry(scenes[selected["scene"]], sizes)
    for frame in frames["choices"]:
        profiles = []
        for part in (frame, *frame.get("variants", [])):
            profiles.extend(read_json(exact_file(source, part["manifest"]))["layouts"])
        sizes = timing.host_sizes(profiles)
        selected, geometry = selected_geometry(default, frame["value"], sizes)
        for style in judgement["choices"]:
            _, other = selected_geometry(style, frame["value"], sizes)
            timing.require(geometry_signature(other) == geometry_signature(geometry),
                           "A judgement style has distinct geometry; preserve it through an explicit style branch")
        result[frame["value"]] = {"geometry": geometry, "sourceScene": selected["scene"],
            "sourceSceneSha256": sha(exact_file(source, selected["scene"])), "allExistingJudgementStyleHostsEqual": True}
    timing.require(len(result) == 15 and frames["default"] in result, "Use all fifteen existing Omni Frames")
    return frames["default"], result


def upgrade(source, output, library, positions_path):
    source = _independent(source)
    output = _independent(output, output=True, exists=False)
    timing.require(output.parent == source.parent and output != source and not output.exists()
                   and not output.with_suffix(".osk").exists(), "Create a fresh sibling author and archive")
    archive = source.with_suffix(".osk")
    generation = read_json(exact_file(source, "generation.json"))
    identity = {"generationSha256": sha(source / "generation.json"), "archiveSha256": sha(archive)}
    timing.require(generation["archive"]["sha256"] == identity["archiveSha256"], "The immutable parent archive changed")
    positions, position_identity = timing.load_positions(positions_path)
    reference = timing.source_reference(_independent(library))
    assets = timing.resource_assets(reference)
    source_files = package_closure(source)
    hashes = {relative: sha(exact_file(source, relative)) for relative in source_files}
    original_catalog = read_json(exact_file(source, "gameplay/content.json"))
    root_manifest = read_json(exact_file(source, "gameplay-skin.json"))
    timing.require(len(original_catalog["modules"]) == 12 and sum(len(module["choices"]) for module in original_catalog["modules"]) == 1278
                   and all(module["id"] != timing.MODULE for module in original_catalog["modules"]) and len(root_manifest["options"]) == 19,
                   "Use the complete v30 PACEMAKER NEXT parent with its remaining unified option slot")
    default_frame, contexts = frame_geometry(source, original_catalog)
    module = timing.module_definition()
    contributions = {choice: {frame: timing.build_choice(choice, context["geometry"], positions, assets)
                             for frame, context in contexts.items()} for choice in timing.CHOICES}
    identifier_audits = [timing.audit_identifiers(contribution) for branches in contributions.values() for contribution in branches.values()]
    staging = output.parent / (output.name + ".staging-" + uuid.uuid4().hex[:12])
    staging.mkdir()
    for relative in source_files:
        copy_exact(exact_file(source, relative), staging / relative)
    media_paths = set()
    for name, resource in assets.items():
        item = reference["assets"][name]
        timing.require(resource["path"] not in source_files, "A new timing asset path collides with existing media")
        copy_exact(timing.inside(Path(reference["library"]), item["source"]), staging / resource["path"])
        timing.require(sha(staging / resource["path"]) == item["sha256"], "Original timing PNG bytes changed while copying")
        media_paths.add(resource["path"])
    paths = set()
    choices = []
    for choice in module["choices"]:
        value = choice["value"]
        def store(frame):
            contribution = contributions[value][frame]
            result = {"manifest": store_json(staging, "m", contribution["manifest"]),
                      "scene": store_json(staging, "s", contribution["scene"])}
            paths.update(result.values())
            return result
        choice.update(store(default_frame))
        if value != "off":
            choice["variants"] = [{"when": {"frame": frame}, **store(frame)} for frame in contexts if frame != default_frame]
        choices.append({"value": value, "scene": choice["scene"], "manifest": choice["manifest"],
                        "frameVariants": len(choice.get("variants", []))})
    catalog = copy.deepcopy(original_catalog)
    catalog["modules"].append(module)
    write_json(staging / "gameplay/content.json", catalog)
    files = package_closure(staging)
    timing.require(set(files) == set(source_files) | media_paths | paths, "Only timing choice configurations and four original PNGs may extend the closure")
    timing.require(all(sha(exact_file(staging, path)) == hashes[path] for path in source_files if path != "gameplay/content.json"),
                   "An existing root document, image, font, material declaration, preview or module changed")
    timing.require(catalog["modules"][:-1] == original_catalog["modules"] and len(catalog["modules"]) == 13
                   and sum(len(item["choices"]) for item in catalog["modules"]) == 1285
                   and preview_files(catalog) == preview_files(original_catalog), "Keep all old choices/previews and add exactly the seven timing choices")
    audit = audit_package(staging, files)
    audit_shared_root(staging, catalog, audit)
    audit["authorDirectory"] = author_directory_budget(files)
    previous = read_json(exact_file(source, "package-audit.json"))
    timing.require(audit["options"] == 32 and audit["selectedUpperBound"]["fonts"] == previous["selectedUpperBound"]["fonts"]
                   and not audit["aggregateViolations"], "FAST/SLOW must keep every public selection budget and add no fonts")
    timing.verify_reference_unchanged(reference)
    timing.require(all(sha(exact_file(source, path)) == expected for path, expected in hashes.items())
                   and sha(source / "generation.json") == identity["generationSha256"] and sha(archive) == identity["archiveSha256"],
                   "The immutable parent author/archive changed")
    timing.require(sha(positions_path) == position_identity["sha256"], "The reviewed position receipt changed during authoring")
    receipt = {"name": NAME, "status": "prepared-awaiting-current-runtime-validation", "repackagedFrom": str(source),
        "sourceIdentity": identity, "timingDisplay": {"art": reference, "positionEvidence": position_identity,
            "reviewedPositions": positions, "choices": choices, "default": module["default"], "rulesets": ["bms"],
            "defaultPolicy": "Position A merged hint enabled by default under the existing Omni visible-options-on policy; OFF remains selectable",
            "directionSources": [timing.DIRECTION_SOURCE, timing.KEYS_SOURCE, timing.SCRATCH_SOURCE],
            "sourceValues": list(timing.DIRECTIONS), "geometrySources": contexts,
            "identifierAudit": {"rule": identifier_audits[0]["rule"], "choiceFrameBranchesCheckedBeforeStaging": len(identifier_audits),
                "maximumDeclaredIds": max(item["declaredIds"] for item in identifier_audits),
                "maximumReferenceIds": max(item["referenceIds"] for item in identifier_audits)},
            "projectionBoundary": "Exact material group directions only; no offset inference or cumulative-count proxy",
            "materialPolicy": "Existing hud.judgement marker, Frame surface and two position carriers; no new INI",
            "selectionPolicy": "C2 publishes only the selected timing-display scene; OFF has no timing templates/resources",
            "fontsAdded": 0, "rootResourcesAdded": 0, "rootSceneChanged": False},
        "sourcePreservation": {"originalSourceClosureUnchanged": True, "originalArchiveUnchanged": True,
            "changedExistingMedia": [], "originalModulesUnchanged": 12, "originalCatalogChoices": 1278,
            "deliveredModules": 13, "deliveredCatalogChoices": 1285, "previewFiles": len(preview_files(catalog))},
        "codecValidation": {"status": "pending"}, "runtimeValidation": {"status": "pending"},
        "budgets": audit["selectedUpperBound"], "unifiedOptionCount": audit["options"],
        "closure": {"files": len(files), "uncompressedBytes": audit["uncompressedBytes"], "configurationBytes": audit["configurationBytes"]},
        "authorDirectory": audit["authorDirectory"]}
    write_json(staging / "package-audit.json", audit)
    write_json(staging / "generation.json", receipt)
    publish_prepared_package(staging, output, receipt, files, audit)
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--positions", type=Path, required=True, help="Reviewed independent capture receipt; no guessed defaults")
    args = parser.parse_args()
    upgrade(args.source, args.output, args.library, args.positions)


if __name__ == "__main__":
    main()
