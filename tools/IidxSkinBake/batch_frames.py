"""Plan and serially bake the selected frozen authors' steady frame art.

This is an orchestration tool, not another animation renderer. GPU work always
uses IidxSkinBake's production evaluator and verification. Exact recorded bytes
and all referenced texture bytes/definitions qualify reuse within one family.
"""

from __future__ import annotations

import argparse
from collections import Counter
from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import shutil
import stat
import subprocess
import sys
import time


REPOSITORY = Path(__file__).resolve().parents[2]
DEV = REPOSITORY / ".dev-cache/temp/iidx-bake-dev"
sys.dont_write_bytecode = True
sys.path[:0] = [str(REPOSITORY / "tools/IidxSkinAssets"),
               str(REPOSITORY / ".dev-cache/tools/iidx-assets")]
from beatoraja_author import (AuthorPackage, material_bindings, material_templates,
                             read_json, source_path, walk_nodes)
from beatoraja_native import scene_properties
from PIL import Image


def digest(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest()


def canonical(value) -> bytes:
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()


def checked_output(output, explicit_root=None, source_roots=()):
    """Apply the CLI's output policy to Python-only alias writes as well."""
    output = Path(os.path.abspath(output))
    if output.exists():
        raise ValueError("Output must be a new directory")
    system_drive = Path(os.environ.get("SystemRoot", "C:/Windows")).drive.lower()
    if output.drive.lower() in {"c:", system_drive}:
        raise ValueError("Bake output may not use the system drive")
    if explicit_root is not None:
        root = Path(explicit_root)
        if not root.is_absolute():
            raise ValueError("An explicit output root must be absolute")
        root = Path(os.path.abspath(root))
        if not root.is_dir() or root == Path(root.anchor):
            raise ValueError("An explicit output root must be an existing dedicated directory")
        allowed = [root]
    else:
        allowed = [DEV, REPOSITORY / "artifacts"]
    if not any(output != root and output.is_relative_to(root) for root in allowed):
        raise ValueError("Output escapes its permitted output root")
    for ancestor in (output, *output.parents):
        try:
            attributes = ancestor.lstat()
        except FileNotFoundError:
            continue
        if stat.S_ISLNK(attributes.st_mode) or getattr(attributes, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT:
            raise ValueError("Output ancestors may not contain reparse points")
    for source_root in source_roots:
        source = Path(os.path.abspath(source_root))
        if output.is_relative_to(source) or source.is_relative_to(output):
            raise ValueError("Output and source may not overlap")
    return output


def supplied_indices(lists=(), paths=()):
    result = set(Path(p).resolve(strict=True) for p in paths)
    for path in lists:
        entries = json.loads(Path(path).read_text(encoding="utf-8"))
        if not isinstance(entries, list) or not all(isinstance(p, str) for p in entries):
            raise ValueError("An existing bake list must contain index paths")
        result.update(Path(p).resolve(strict=True) for p in entries)
    return sorted(result)


def transfer_page(source, destination, expected_sha):
    """Only baked pages cross volumes; original author resources never do."""
    if digest(source.read_bytes()) != expected_sha:
        raise ValueError("Reused page differs from its verified bake")
    if destination.exists():
        raise ValueError("A reused baked page may not overwrite an existing file")
    if source.stat().st_dev == destination.parent.stat().st_dev:
        os.link(source, destination)
        return "hardlink"
    with source.open("rb") as reader, destination.open("xb") as writer:
        shutil.copyfileobj(reader, writer)
    if digest(destination.read_bytes()) != expected_sha:
        raise ValueError("Copied baked page differs from its verified source")
    return "verified-copy"


class Sources:
    def __init__(self):
        self.hashes = {}
        self.signatures = {}

    def sha(self, path: Path) -> str:
        if path not in self.hashes:
            self.hashes[path] = digest(path.read_bytes())
        return self.hashes[path]

    def describe(self, family, author, manifest_path, animation):
        key = (family, str(author), manifest_path, animation)
        if key in self.signatures:
            return self.signatures[key]
        manifest = read_json(source_path(author, manifest_path))
        definition = next(a for a in manifest["animations"] if a["id"] == animation)
        resources = {r["id"]: r for r in manifest["resources"]}
        program_path = source_path(author, definition["path"])
        program = read_json(program_path)
        files = {manifest_path: self.sha(source_path(author, manifest_path)),
                 definition["path"]: self.sha(program_path)}
        textures = []
        decoded = 0
        for resource_id in definition["resources"]:
            resource = resources[resource_id]
            path = source_path(author, resource["path"])
            sha = self.sha(path)
            files[resource["path"]] = sha
            # Paths are storage identities; every other declared property,
            # including resource ID and sampling, must match exactly.
            textures.append({"definition": {k: v for k, v in resource.items() if k != "path"},
                             "sha256": sha})
            # Frozen dynamic-cover authors also contain a WebP atlas. This
            # metadata estimate must not impose a PNG-only input contract;
            # actual decode still belongs to the production C# texture loader.
            with Image.open(path) as image:
                width, height = image.size
            decoded += width * height * 4
        signature = digest(canonical({"family": family, "program": files[definition["path"]],
                                      "resources": textures}))
        result = {"signature": signature, "files": files, "program": program,
                  "animationFile": definition["path"], "sourceDecodedBytes": decoded}
        self.signatures[key] = result
        return result


def selected_background_nodes(contribution, keymode, mode, options):
    context = {"layout.ruleset": "bms", "layout.presentation": mode,
               **{"option." + k: v for k, v in options.items()},
               "preparation.available": False, "preparation.active": False,
               "preparation.ready": False, "scroll.available": True,
               "scroll.hi-speed-available": False, "scroll.adjusting": False,
               "lift.reversed": False}

    def visit(node):
        properties, dynamic = scene_properties(node, contribution.scene, context)
        if properties.get("visible", True) is False or properties.get("opacity", 1) <= 0:
            return
        if any(d["property"] == "visible" for d in dynamic):
            raise ValueError("Steady background visibility needs an explicit source state")
        if node["type"] == "recorded-animation":
            yield node
        for child in node.get("children", []):
            yield from visit(child)

    for binding in material_bindings(contribution, keymode, mode):
        if (binding["target"]["kind"] == "global" and binding["slot"] == "stage.background"
                and binding["mode"] == "Provide"):
            for root in material_templates(contribution, binding["slot"], binding["resource"]):
                yield from visit(root)


def steady_frame(node, scene, program):
    clock = node.get("clock")
    if clock and clock.startswith("value."):
        source = next(s for s in scene["numericSources"] if s["id"] == clock[6:])
        if source["operation"] != "add" or source["right"] != 0 or not isinstance(source["left"], (int, float)):
            raise ValueError("Steady clock is not an explicit constant")
        raw = source["left"] * program["fps"] / 1000
        if abs(raw - round(raw)) > 1e-7:
            raise ValueError("Steady clock does not identify an exact source frame")
        frame = round(raw)
        if not 0 <= frame < len(program["frames"]) - 1:
            raise ValueError("Steady clock is outside the non-terminal source frames")
        return frame, {"clock": clock, "milliseconds": source["left"], "rule": "source-constant"}
    if clock is None and len(program["frames"]) == 2:
        return 0, {"clock": None, "rule": "single-content-frame"}
    if program.get("loop") and clock == "presentation.elapsed":
        return program.get("loopStartFrame", 0), {"clock": clock, "rule": "source-loop-start-snapshot"}
    raise ValueError("No proven steady frame for " + node["id"])


def collect_jobs(authors, sources):
    jobs = []
    unavailable = []
    for family, root in authors.items():
        author = AuthorPackage(Path(root), family)
        default = author.modules["frame"]["default"]
        for choice in author.modules["frame"]["choices"]:
            for mode in ("p1", "p2", "dp"):
                keymode = "14k" if mode == "dp" else "7k"
                options = author.options(keymode, mode, {"frame": choice["value"]})
                for name in ("frame-opening", "entrance-animation"):
                    if name in options:
                        options[name] = False
                contribution = author.contribution("frame", options)
                seen = set()

                def add(node, kind, frames, selection):
                    identity = (node["animation"], kind)
                    if identity in seen:
                        return
                    seen.add(identity)
                    src = sources.describe(family, author.root, contribution.manifest_path, node["animation"])
                    p = src["program"]
                    job = {"family": family, "author": str(author.root), "theme": choice["value"],
                           "mode": mode, "kind": kind, "defaultTheme": choice["value"] == default,
                           "manifest": contribution.manifest_path, "scene": contribution.scene_path,
                           "sceneSha256": sources.sha(source_path(author.root, contribution.scene_path)),
                           "animation": node["animation"], "sourceNode": node["id"],
                           "frames": frames, "selection": selection, "sourceSignature": src["signature"],
                           "sourceFiles": src["files"], "sourceDecodedBytes": src["sourceDecodedBytes"],
                           "canvas": [p["width"], p["height"]], "fps": p["fps"],
                           "sourceFrameCount": len(p["frames"])}
                    key = digest(canonical([family, contribution.manifest_path, node["animation"], frames]))[:12]
                    job["name"] = f"{family}-{choice['value']}-{mode}-{kind}-{key}"
                    jobs.append(job)

                for node in selected_background_nodes(contribution, keymode, mode, options):
                    src = sources.describe(family, author.root, contribution.manifest_path, node["animation"])
                    frame, selection = steady_frame(node, contribution.scene, src["program"])
                    add(node, "steady", [frame], selection)
                events = set()
                for root_node in [contribution.scene["root"], *contribution.templates.values()]:
                    for node in walk_nodes(root_node):
                        if node["type"] != "recorded-animation":
                            continue
                        src = sources.describe(family, author.root, contribution.manifest_path, node["animation"])
                        kind = "ready" if "ready" in node["animation"] else (
                            "fail" if src["program"].get("trigger") == "gameplay.fail" else None)
                        if kind:
                            events.add(kind)
                            add(node, kind, "all", {"rule": "complete-event-program", "clock": node.get("clock")})
                if "fail" not in events:
                    unavailable.append({"family": family, "theme": choice["value"], "mode": mode,
                                        "kind": "fail", "reason": "No failure recorded program in frozen frame contribution"})
    # Keep GPU single-instance; all steady art precedes the default event gaps.
    return sorted(jobs, key=lambda j: (0 if j["kind"] == "steady" else 1 if j["defaultTheme"] else 2,
                                      j["family"], not j["defaultTheme"], j["theme"], j["mode"], j["name"])), unavailable


def available_indices(root, authors, sources, runtime=None, paths=None):
    result = []
    roots = {str(Path(p).resolve()): family for family, p in authors.items()}
    renderer = ({"productionAssembly": digest((runtime / "osu.Game.dll").read_bytes()),
                 "frameworkAssembly": digest((runtime / "osu.Framework.dll").read_bytes())}
                if runtime is not None else None)
    for path in sorted(root.rglob("index.json") if paths is None else paths):
        index = read_json(path)
        if index.get("contract") != "oms-iidx-baked-animation.v1" or not index.get("verification", {}).get("passed"):
            continue
        source = index["source"]
        if renderer is not None and any(source[k]["sha256"] != expected for k, expected in renderer.items()):
            continue
        family = roots.get(str(Path(source["authorRoot"]).resolve()))
        if family is None:
            continue
        author = Path(source["authorRoot"])
        for relative, expected in source["files"].items():
            if sources.sha(source_path(author, relative)) != expected:
                raise ValueError("Stale bake source: " + str(path))
        desc = sources.describe(family, author, source["manifest"], source["animation"])
        result.append((path, index, desc["signature"]))
    return result


def matching(job, indices):
    for path, index, signature in indices:
        if signature != job["sourceSignature"]:
            continue
        if job["frames"] == "all":
            if not index["selectionComplete"]:
                continue
        elif not set(job["frames"]).issubset(index["selectedFrames"]):
            continue
        return path, index
    return None


def statistics(jobs, indices):
    pending = set()
    counts = Counter()
    source_peak = 0
    for j in jobs:
        counts[j["family"] + ":" + j["kind"]] += 1
        source_peak = max(source_peak, j["sourceDecodedBytes"])
        if matching(j, indices):
            counts["existing-source-qualified"] += 1
        else:
            identity = (j["sourceSignature"], json.dumps(j["frames"]))
            if identity not in pending:
                counts["gpu:" + j["family"] + ":" + j["kind"]] += 1
                pending.add(identity)
            else:
                counts["reuse-after-queued"] += 1
    return {"counts": dict(counts), "gpuJobs": len(pending),
            "maximumSourceTextureDecodedBytes": source_peak,
            "scope": "All frame choices at default graph visibility, P1/P2/DP; steady partials and complete READY/fail events"}


def reuse(job, existing, output, sources, output_root=None):
    original_path, original = existing
    output = checked_output(output, output_root, [job["author"], original_path.parent])
    output.mkdir()
    index = deepcopy(original)
    if job["frames"] != "all":
        wanted = set(job["frames"])
        index["frames"] = [f for f in index["frames"] if f["sourceFrame"] in wanted]
        index["selectedFrames"] = job["frames"]
        index["selectionComplete"] = len(wanted) == index["sourceFrameCount"]
    pages = {r["page"] for f in index["frames"] for r in f["runs"]}
    index["pages"] = [p for p in index["pages"] if p["id"] in pages]
    page_transfers = []
    for page in index["pages"]:
        source_page = source_path(original_path.parent, page["file"])
        transfer = transfer_page(source_page, output / page["file"], page["sha256"])
        page_transfers.append({"file": page["file"], "sha256": page["sha256"], "method": transfer})
    desc = sources.describe(job["family"], Path(job["author"]), job["manifest"], job["animation"])
    index["source"].update({"authorRoot": job["author"], "manifest": job["manifest"],
                            "animation": job["animation"], "animationFile": desc["animationFile"],
                            "files": desc["files"]})
    proof = {"kind": "exact-program-and-resources-reuse", "originalIndex": str(original_path.resolve()),
             "originalIndexSha256": digest(original_path.read_bytes()),
             "sourceSignature": desc["signature"], "sameFamily": True,
             "selection": job["selection"], "sceneSha256": job["sceneSha256"],
             "renderingPerformed": False,
             "verifiedPixelPagesHardLinked": all(p["method"] == "hardlink" for p in page_transfers),
             "pageTransfers": page_transfers}
    index["reuse"] = proof
    index["optimization"] = {"kind": "source-qualified-reuse", "decodedPixels": sum(p["width"] * p["height"] for p in index["pages"])}
    original_verification = read_json(source_path(original_path.parent, original["verification"]["file"]))
    (output / "verification.json").write_text(json.dumps({"passed": True, "reuse": proof,
        "inheritedVerification": original_verification}, indent=2), encoding="utf-8")
    index["verification"]["file"] = "verification.json"
    (output / "index.json").write_text(json.dumps(index, indent=2), encoding="utf-8")
    return index


def execute(plan_path, runtime, dll, output_root=None, existing_lists=(), existing_paths=()):
    plan = read_json(plan_path)
    root = plan_path.parent
    planned_root = plan.get("outputRoot")
    if output_root is not None and planned_root is not None and Path(os.path.abspath(output_root)) != Path(os.path.abspath(planned_root)):
        raise ValueError("The supplied output root differs from the reviewed worklist")
    output_root = output_root if output_root is not None else planned_root
    supplied = supplied_indices(existing_lists, [*plan.get("existingIndices", []), *existing_paths])
    sources = Sources()
    indices = available_indices(DEV, plan["authors"], sources, runtime, supplied or None)
    results = []
    for ordinal, job in enumerate(plan["jobs"], 1):
        pause = root / "pause-between-jobs"
        if pause.exists():
            print("Paused at completed job boundary; waiting for " + str(pause) + " to be removed.", flush=True)
            while pause.exists():
                time.sleep(1)
            print("Resuming serial GPU queue.", flush=True)
        minimum_free = plan.get("minimumFreeBytes", 0)
        if shutil.disk_usage(root).free < minimum_free:
            raise OSError("The reviewed worklist's minimum free storage reserve has been reached")
        author = Path(job["author"])
        output = checked_output(root / job["name"], output_root, plan["authors"].values())
        if digest(source_path(author, job["scene"]).read_bytes()) != job["sceneSha256"]:
            raise ValueError("Scene changed after worklist selection")
        for relative, expected in job["sourceFiles"].items():
            if digest(source_path(author, relative).read_bytes()) != expected:
                raise ValueError("Source changed after worklist selection: " + relative)
        actual = sources.describe(job["family"], author, job["manifest"], job["animation"])
        if actual["files"] != job["sourceFiles"] or actual["signature"] != job["sourceSignature"]:
            raise ValueError("Recorded sources changed after planning")
        existing = matching(job, indices)
        if existing:
            index = reuse(job, existing, output, sources, output_root)
            action = "source-qualified-reuse"
        else:
            if job["kind"] == "full-combo" and not job.get("programSizeReviewed", False):
                raise ValueError("Full-combo programs require per-program size review before GPU execution")
            command = ["dotnet", str(dll), "--runtime", str(runtime), "--author", job["author"],
                       "--manifest", job["manifest"], "--animation", job["animation"],
                       "--frames", "all" if job["frames"] == "all" else ",".join(map(str, job["frames"])),
                       "--output", str(output), "--verification-images", "false"]
            if output_root is not None:
                command.extend(("--output-root", str(output_root)))
            for key, value in job.get("bakerOptions", {}).items():
                if key not in ("--normal-run-pieces", "--page-size"):
                    raise ValueError("Unsupported per-program baker option")
                command.extend((key, str(value)))
            log = root / (job["name"] + ".log")
            print(f"[{ordinal}/{len(plan['jobs'])}] GPU {job['name']}", flush=True)
            with log.open("w", encoding="utf-8") as stream:
                result = subprocess.run(command, cwd=REPOSITORY, stdout=stream, stderr=subprocess.STDOUT)
            if result.returncode:
                raise RuntimeError(f"Bake exited {result.returncode}; retained log: {log}")
            index = read_json(output / "index.json")
            if not index["verification"]["passed"]:
                raise RuntimeError("Baker did not verify its output")
            action = "gpu-bake"
        index_path = output / "index.json"
        raw_index_path = index_path
        raw_decoded = sum(p["width"] * p["height"] * 4 for p in index["pages"])
        tile_threshold = plan.get("losslessTilesAboveBytes")
        tile_candidate = None
        tile_candidate_metrics = None
        if tile_threshold is not None and raw_decoded > tile_threshold and not index.get("tiling"):
            tile_output = checked_output(root / (job["name"] + "-tiles128"), output_root, [*plan["authors"].values(), output])
            tile_log = root / (job["name"] + "-tiles128.log")
            print(f"[{ordinal}/{len(plan['jobs'])}] Lossless 128px movie-union; {raw_decoded/1048576:.2f} MiB raw", flush=True)
            command = ["dotnet", str(dll), "--runtime", str(runtime),
                       "--tile-index", str(index_path), "--tile-size", "128", "--output", str(tile_output)]
            if output_root is not None:
                command.extend(("--output-root", str(output_root)))
            with tile_log.open("w", encoding="utf-8") as stream:
                result = subprocess.run(command, cwd=REPOSITORY, stdout=stream, stderr=subprocess.STDOUT)
            if result.returncode:
                raise RuntimeError(f"Lossless tiling exited {result.returncode}; retained log: {tile_log}")
            tiled = read_json(tile_output / "index.json")
            if not tiled["verification"].get("exactOriginalRunReconstruction"):
                raise RuntimeError("Tiling did not prove exact reconstruction of every original run")
            tile_candidate = str((tile_output / "index.json").resolve())
            tile_candidate_metrics = {
                "decodedBytes": sum(p["width"] * p["height"] * 4 for p in tiled["pages"]),
                "staticCropGroups": len({(r["order"], r["page"], *r["rect"], *r["offset"], r["blend"])
                                         for f in tiled["frames"] for r in f["runs"]}),
                "nativeImageSetGroups": tiled["optimization"]["destinationImageSetGroups"],
                "maximumRunsPerFrame": max(len(f["runs"]) for f in tiled["frames"]),
            }
            if plan.get("selectLosslessTiles", True) and tile_candidate_metrics["decodedBytes"] < raw_decoded:
                index_path, index = tile_output / "index.json", tiled
                action += "+lossless-tiles128"
        indices.append((index_path, index, job["sourceSignature"]))
        groups = {(r["order"], r["blend"], *r["offset"], *r["rect"][2:])
                  for f in index["frames"] for r in f["runs"]}
        results.append({**{k: job[k] for k in ("family", "theme", "mode", "kind", "manifest", "animation")},
                        "index": str(index_path.resolve()), "action": action,
                        "rawIndex": str(raw_index_path.resolve()), "rawDecodedBytes": raw_decoded,
                        "losslessTileCandidate": tile_candidate,
                        "losslessTileCandidateMetrics": tile_candidate_metrics,
                        "nativeImageSetGroups": len(groups),
                        "staticCropGroups": len({(r["order"], r["page"], *r["rect"], *r["offset"], r["blend"])
                                                 for f in index["frames"] for r in f["runs"]}),
                        "maximumRunsPerFrame": max(len(f["runs"]) for f in index["frames"]),
                        "freeBytesAfterJob": shutil.disk_usage(root).free,
                        "selectionComplete": index["selectionComplete"], "selectedFrames": index["selectedFrames"],
                        "decodedBytes": sum(p["width"] * p["height"] * 4 for p in index["pages"]),
                        "pngBytes": sum(p["bytes"] for p in index["pages"])})
        (root / "results.json").write_text(json.dumps({"completed": len(results), "expected": len(plan["jobs"]),
            "outputRoot": str(Path(output_root).absolute()) if output_root is not None else None,
            "allPassed": len(results) == len(plan["jobs"]), "jobs": results}, indent=2), encoding="utf-8")
        print(f"[{ordinal}/{len(plan['jobs'])}] {action}; {job['name']}; {results[-1]['decodedBytes']/1048576:.2f} MiB decoded", flush=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--standard", type=Path)
    parser.add_argument("--lightning", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--output-root", type=Path)
    parser.add_argument("--existing-list", type=Path, action="append", default=[])
    parser.add_argument("--existing-index", type=Path, action="append", default=[])
    parser.add_argument("--execute", type=Path)
    parser.add_argument("--runtime", type=Path, default=REPOSITORY / "osu.Desktop/bin/Release/net8.0")
    parser.add_argument("--baker", type=Path, default=DEV / "bin/net8.0/IidxSkinBake.dll")
    args = parser.parse_args()
    if args.execute:
        execute(args.execute.resolve(strict=True), args.runtime.resolve(strict=True), args.baker.resolve(strict=True),
                args.output_root, args.existing_list, args.existing_index)
        return
    if not args.output or not args.standard or not args.lightning:
        parser.error("Planning requires both --standard and --lightning and a fresh --output")
    authors = {"standard": str(args.standard.resolve(strict=True)), "lightning": str(args.lightning.resolve(strict=True))}
    output = checked_output(args.output, args.output_root, authors.values())
    sources = Sources()
    jobs, unavailable = collect_jobs(authors, sources)
    supplied = supplied_indices(args.existing_list, args.existing_index)
    indices = available_indices(DEV, authors, sources, args.runtime.resolve(strict=True), supplied or None)
    summary = statistics(jobs, indices)
    output.mkdir()
    (output / "worklist.json").write_text(json.dumps({"contract": "oms-iidx-bake-worklist.v1", "authors": authors,
        "summary": summary, "jobs": jobs, "unavailable": unavailable,
        "outputRoot": str(Path(args.output_root).absolute()) if args.output_root is not None else None,
        "existingIndices": [str(p) for p in supplied]}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    print("Worklist:", output / "worklist.json")


if __name__ == "__main__":
    main()
