"""Plan only mounted source animations for native gauge and score feedback.

Selection follows the frozen content catalog and actual material instances.
This does not infer feedback from an unmounted animation declaration or colour.
"""

import argparse
from collections import Counter
import json
from pathlib import Path

from batch_frames import DEV, Sources, canonical, digest
from beatoraja_author import AuthorPackage, material_bindings, source_path, walk_nodes


def collect(authors, sources):
    jobs = {}
    inspected = set()
    triggers = Counter()
    for family, root in authors.items():
        author = AuthorPackage(Path(root), family)
        graph_choices = author.modules.get("graph-target", {}).get("choices", [None])
        for frame_choice in author.modules["frame"]["choices"]:
            for mode in ("p1", "p2", "dp"):
                keymode = "14k" if mode == "dp" else "7k"
                defaults = author.options(keymode, mode)
                graph_visibilities = (True, False) if "graph-visible" in defaults else (None,)
                for graph_visible in graph_visibilities:
                    for graph_choice in graph_choices:
                        overrides = {"frame": frame_choice["value"]}
                        if graph_visible is not None:
                            overrides["graph-visible"] = graph_visible
                        if graph_choice is not None:
                            overrides["graph-target"] = graph_choice["value"]
                        options = author.options(keymode, mode, overrides)
                        contributions = [author.base(), author.contribution("frame", options)]
                        if graph_choice is not None and graph_visible is not False:
                            contributions.append(author.contribution("graph-target", options))
                        materials = {(b["slot"], b["resource"]) for c in contributions
                                     for b in material_bindings(c, keymode, mode) if b["mode"] == "Provide"}
                        for contribution in contributions:
                            selected = {i["template"] for i in contribution.scene.get("instances", [])
                                        if (i["material"]["slot"], i["material"]["resource"]) in materials}
                            roots = [contribution.scene["root"], *(contribution.templates[t] for t in selected)]
                            for node in (n for r in roots for n in walk_nodes(r) if n["type"] == "recorded-animation"):
                                animation = node["animation"]
                                key = family, contribution.manifest_path, animation
                                desc = sources.describe(family, author.root, contribution.manifest_path, animation)
                                program = desc["program"]
                                if key not in inspected:
                                    inspected.add(key)
                                    triggers[family + ":" + program["trigger"]] += 1
                                score = node.get("scoreCrossing")
                                gauge = program["trigger"] == "gauge.increase"
                                initial_gauge = (family == "standard" and animation.startswith("sg.f")
                                                 and animation.endswith(".loop.clip.initial")
                                                 and program["trigger"] == "gameplay.attach")
                                if not (score or gauge or initial_gauge):
                                    continue
                                if score and program["trigger"] != "score.crossing":
                                    raise ValueError("Mounted score crossing has a different source trigger")
                                consumer = {"frame": frame_choice["value"], "mode": mode,
                                            "graphVisible": graph_visible,
                                            "graphTarget": graph_choice["value"] if graph_choice else None,
                                            "sourceNode": node["id"], "scoreCrossing": score}
                                if key in jobs:
                                    jobs[key]["defaultTheme"] |= frame_choice["value"] == author.modules["frame"]["default"]
                                    if consumer not in jobs[key]["consumers"]:
                                        jobs[key]["consumers"].append(consumer)
                                    continue
                                kind = "score-feedback" if score else "gauge-feedback"
                                token = digest(canonical(key))[:12]
                                jobs[key] = {"name": f"{family}-{kind}-{frame_choice['value']}-{mode}-{token}",
                                             "family": family, "author": str(author.root),
                                             "theme": frame_choice["value"], "mode": mode, "kind": kind,
                                             "defaultTheme": frame_choice["value"] == author.modules["frame"]["default"],
                                             "manifest": contribution.manifest_path, "scene": contribution.scene_path,
                                             "sceneSha256": sources.sha(source_path(author.root, contribution.scene_path)),
                                             "animation": animation, "sourceNode": node["id"], "frames": "all",
                                             "selection": {"rule": "complete-mounted-feedback-program",
                                                           "trigger": program["trigger"], "initialGauge": initial_gauge},
                                             "consumers": [consumer], "sourceSignature": desc["signature"],
                                             "sourceFiles": desc["files"], "sourceDecodedBytes": desc["sourceDecodedBytes"],
                                             "canvas": [program["width"], program["height"]], "fps": program["fps"],
                                             "sourceFrameCount": len(program["frames"])}
    return sorted(jobs.values(), key=lambda j: (j["kind"], j["sourceSignature"], j["name"])), dict(triggers)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--standard", type=Path, required=True)
    parser.add_argument("--lightning", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(DEV.resolve()):
        parser.error("Output must be under the dedicated bake development directory")
    authors = {"standard": str(args.standard.resolve(strict=True)), "lightning": str(args.lightning.resolve(strict=True))}
    jobs, triggers = collect(authors, Sources())
    summary = {"jobs": len(jobs), "strictSourcePrograms": len({j["sourceSignature"] for j in jobs}),
               "byKind": dict(Counter(j["family"] + ":" + j["kind"] for j in jobs)),
               "mountedProgramTriggersInspected": triggers,
               "maximumSourceTextureDecodedMiB": max((j["sourceDecodedBytes"] for j in jobs), default=0) / 1048576}
    output.mkdir()
    (output / "worklist.json").write_text(json.dumps({"contract": "oms-iidx-bake-worklist.v1", "authors": authors,
        "jobs": jobs, "summary": summary, "minimumFreeBytes": 5 * 1024 ** 3,
        "losslessTilesAboveBytes": 512 * 1024 ** 2, "unavailable": []}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
