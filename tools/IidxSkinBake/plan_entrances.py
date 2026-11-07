"""Plan full, mounted entrance programs without baking caller HUD movement.

This planner is CPU-only. Existing representative steady frames are evidence of
partial coverage, never substitutes for complete source programs. Source clocks
are recorded as authored; mapping them to a host timer is a separate consumer.
"""

import argparse
from itertools import product
import json
from pathlib import Path

from batch_frames import DEV, REPOSITORY, Sources, canonical, digest, matching
from beatoraja_author import AuthorPackage, material_bindings, material_templates, read_json, source_path, walk_nodes
from beatoraja_native import scene_properties


CLOCKS = {"presentation.elapsed", "preparation.elapsed"}
OPENING_OPTIONS = ("frame-opening", "entrance-animation", "lane-opening")


def mounted_entrances(contribution, keymode, mode, options):
    """Keep option-enabled mounted trees, including initially transparent fades."""
    context = {"layout.ruleset": "bms", "layout.presentation": mode,
               "preparation.available": True, "preparation.active": True,
               "preparation.ready": False, "scroll.available": True,
               "scroll.adjusting": False, "scroll.hi-speed-available": False,
               "lift.reversed": False,
               **{"option." + key: value for key, value in options.items()}}
    mounted = {"<scene-root>": (contribution.scene["root"], None)}
    for binding in material_bindings(contribution, keymode, mode):
        if binding["mode"] == "Provide":
            for template in material_templates(contribution, binding["slot"], binding["resource"]):
                mounted.setdefault(template["id"], (template, binding))

    def visit(node, ancestry):
        if not any(n.get("type") == "recorded-animation" and n.get("clock") in CLOCKS
                   for n in walk_nodes(node)):
            return
        properties, dynamic = scene_properties(node, contribution.scene, context)
        tracks = [t for t in contribution.scene.get("tracks", []) if t["target"] == node["id"]]
        # A preparation-time fade can start at zero. Retain its whole program,
        # rather than treating one initial phase as a permanently hidden node.
        for prop, hidden in (("visible", properties.get("visible", True) is False),
                             ("opacity", properties.get("opacity", 1) <= 0)):
            can_change = any(d["property"] == prop for d in dynamic) or any(t["property"] == prop for t in tracks)
            if hidden and not can_change:
                return
        chain = [*ancestry, node["id"]]
        if node.get("type") == "recorded-animation" and node.get("clock") in CLOCKS:
            yield node, chain
        for child in node.get("children", []):
            yield from visit(child, chain)

    for root, binding in mounted.values():
        for node, ancestry in visit(root, []):
            yield node, {"slot": binding["slot"] if binding else None,
                         "target": binding["target"] if binding else None,
                         "templateRoot": root["id"], "ancestry": ancestry}


def collect(authors, sources):
    jobs = {}
    selections = []
    for family, path in authors.items():
        author = AuthorPackage(Path(path), family)
        for choice in author.modules["frame"]["choices"]:
            for mode in ("p1", "p2", "dp"):
                keymode = "14k" if mode == "dp" else "7k"
                defaults = author.options(keymode, mode)
                graphs = (True, False) if "graph-visible" in defaults else (None,)
                panels = (True, False) if "judge-panel-visible" in defaults else (None,)
                for graph, panel in product(graphs, panels):
                    overrides = {"frame": choice["value"],
                                 **{name: True for name in OPENING_OPTIONS if name in defaults}}
                    if graph is not None:
                        overrides["graph-visible"] = graph
                    if panel is not None:
                        overrides["judge-panel-visible"] = panel
                    options = author.options(keymode, mode, overrides)
                    contribution = author.contribution("frame", options)
                    scene_sha = sources.sha(source_path(author.root, contribution.scene_path))
                    ini_sha = sources.sha(source_path(author.root, contribution.ini_path))
                    selected = []
                    for node, mount in mounted_entrances(contribution, keymode, mode, options):
                        animation = node["animation"]
                        key = family, contribution.manifest_path, animation
                        selected.append(animation)
                        consumer = {"frame": choice["value"], "mode": mode, "options": overrides,
                                    "scene": contribution.scene_path, "sceneSha256": scene_sha,
                                    "ini": contribution.ini_path, "iniSha256": ini_sha, "sourceNode": node["id"],
                                    "clock": node["clock"], **mount}
                        if key in jobs:
                            jobs[key]["defaultTheme"] |= choice["value"] == author.modules["frame"]["default"]
                            if consumer not in jobs[key]["consumers"]:
                                jobs[key]["consumers"].append(consumer)
                            continue
                        source = sources.describe(family, author.root, contribution.manifest_path, animation)
                        program = source["program"]
                        mesh_counts = [sum(program["nodes"][entry[0]].get("geometry") is not None
                                           for entry in frame["nodes"]) for frame in program["frames"]]
                        token = digest(canonical(key))[:12]
                        jobs[key] = {"name": f"{family}-entrance-{choice['value']}-{mode}-{token}",
                                     "family": family, "author": str(author.root), "theme": choice["value"],
                                     "mode": mode, "kind": "entrance", "defaultTheme": choice["value"] == author.modules["frame"]["default"],
                                     "manifest": contribution.manifest_path, "scene": contribution.scene_path,
                                     "sceneSha256": scene_sha, "animation": animation, "sourceNode": node["id"],
                                     "frames": "all", "selection": {"rule": "complete-mounted-entrance-program", "clock": node["clock"]},
                                     "consumers": [consumer], "sourceSignature": source["signature"],
                                     "sourceFiles": source["files"], "sourceDecodedBytes": source["sourceDecodedBytes"],
                                     "canvas": [program["width"], program["height"]], "fps": program["fps"],
                                     "sourceFrameCount": len(program["frames"]),
                                     "durationMs": (len(program["frames"]) - 1) * 1000 / program["fps"],
                                     "trigger": program["trigger"], "loop": program.get("loop", False),
                                     "loopStartFrame": program.get("loopStartFrame"), "sourceMeshPeak": max(mesh_counts),
                                     "oneCanvasPerFrameRgbaBytes": program["width"] * program["height"] * 4 * (len(program["frames"]) - 1)}
                    selections.append({"family": family, "frame": choice["value"], "mode": mode,
                                       "graphVisible": graph, "judgePanelVisible": panel,
                                       "manifest": contribution.manifest_path, "scene": contribution.scene_path,
                                       "ini": contribution.ini_path, "animations": sorted(set(selected))})
    return sorted(jobs.values(), key=lambda j: (j["family"], not j["defaultTheme"], j["theme"], j["mode"], j["name"])), selections


def existing_indices(paths, authors, sources, runtime):
    roots = {str(Path(root).resolve()): family for family, root in authors.items()}
    assemblies = {"productionAssembly": sources.sha(runtime / "osu.Game.dll"),
                  "frameworkAssembly": sources.sha(runtime / "osu.Framework.dll")}
    result = []
    for path in dict.fromkeys(Path(p).resolve(strict=True) for p in paths):
        index = read_json(path)
        if index.get("contract") != "oms-iidx-baked-animation.v1" or not index.get("verification", {}).get("passed"):
            raise ValueError("Supplied existing index has not passed the baker gate")
        source = index["source"]
        family = roots[str(Path(source["authorRoot"]).resolve())]
        if any(source[k]["sha256"] != value for k, value in assemblies.items()):
            raise ValueError("Existing index belongs to a different production renderer")
        for relative, sha in source["files"].items():
            if sources.sha(source_path(Path(source["authorRoot"]), relative)) != sha:
                raise ValueError("Existing index source changed")
        description = sources.describe(family, Path(source["authorRoot"]), source["manifest"], source["animation"])
        result.append((path, index, description["signature"]))
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--standard", type=Path, required=True)
    parser.add_argument("--lightning", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--existing-list", type=Path, action="append", default=[])
    parser.add_argument("--existing-index", type=Path, action="append", default=[])
    parser.add_argument("--runtime", type=Path, default=REPOSITORY / "osu.Desktop/bin/Release/net8.0")
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(DEV.resolve()):
        parser.error("Output must remain in the dedicated bake development directory")
    authors = {"standard": str(args.standard.resolve(strict=True)), "lightning": str(args.lightning.resolve(strict=True))}
    sources = Sources()
    jobs, selections = collect(authors, sources)
    paths = list(args.existing_index)
    for path in args.existing_list:
        paths.extend(json.loads(path.read_text(encoding="utf-8")))
    indices = existing_indices(paths, authors, sources, args.runtime.resolve(strict=True))
    unique = {}
    for job in jobs:
        full = matching(job, indices)
        partials = [str(p) for p, index, sig in indices if sig == job["sourceSignature"] and not index["selectionComplete"]]
        job["existingCoverage"] = {"completeIndex": str(full[0]) if full else None, "partialIndices": partials}
        unique.setdefault(job["sourceSignature"], job)
    pending = [j for j in unique.values() if not j["existingCoverage"]["completeIndex"]]
    summary = {"planningOnly": True, "selectionBranches": len(selections), "consumptionIdentities": len(jobs),
               "strictSourcePrograms": len(unique), "newUniqueGpuPrograms": len(pending),
               "existingCompleteSourcePrograms": len(unique) - len(pending),
               "partialSourceProgramsNeedingCompleteUpgrade": sum(bool(j["existingCoverage"]["partialIndices"]) for j in pending),
               "byFamily": {family: {"consumptionIdentities": sum(j["family"] == family for j in jobs),
                   "newUniqueGpuPrograms": sum(j["family"] == family for j in pending),
                   "newContentFrames": sum(j["sourceFrameCount"] - 1 for j in pending if j["family"] == family),
                   "oneCanvasPerFrameRgbaBytes": sum(j["oneCanvasPerFrameRgbaBytes"] for j in pending if j["family"] == family)} for family in authors},
               "newContentFrames": sum(j["sourceFrameCount"] - 1 for j in pending),
               "maximumSourceTextureDecodedBytes": max((j["sourceDecodedBytes"] for j in pending), default=0),
               "scope": "All actual mounted presentation.elapsed/preparation.elapsed recorded programs with entrance options enabled, every frame/P1/P2/DP/Graph/JudgePanel branch. Full original loop and terminal retained; READY/fail and static HUD-coordinate tracks are not baked again.",
               "sizeCaveat": "Canvas-frame RGBA is one full canvas per source frame, not an atlas/PNG estimate or upper bound; blend segmentation, sparse pixels, exact frame reuse and tiling change output."}
    output.mkdir()
    (output / "worklist.json").write_text(json.dumps({"contract": "oms-iidx-bake-worklist.v1", "authors": authors,
        "jobs": jobs, "summary": summary, "selections": selections, "unavailable": [],
        "executionPolicy": "CPU plan only. Review unique programs and schedule separately; do not append blindly to the running FC/cover queue."}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    print(output / "worklist.json")


if __name__ == "__main__":
    main()
