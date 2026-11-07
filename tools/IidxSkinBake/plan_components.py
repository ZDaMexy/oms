"""Inventory complete optional animations before scheduling their GPU bakes.

The full-combo plan is deliberately planning-only. Its source canvas-frame
figures are conservative comparison units, not a promise about compressed PNG
size or target GPU allocation. Execute small modules separately first.
"""

import argparse
from collections import Counter
import json
from pathlib import Path

from batch_frames import (DEV, Sources, available_indices, canonical, digest,
                          statistics)
from beatoraja_author import AuthorPackage, material_bindings, source_path, walk_nodes


def collect(authors, modules, sources, beam_length="normal"):
    jobs = {}
    for family, root in authors.items():
        author = AuthorPackage(Path(root), family)
        for module in modules:
            if module == "key-beam" and family == "standard" and beam_length != "normal":
                continue
            for choice in author.modules[module]["choices"]:
                for mode in ("p1", "p2", "dp"):
                    keymode = "14k" if mode == "dp" else "7k"
                    overrides = {module: choice["value"]}
                    if module == "key-beam" and family == "lightning":
                        overrides["beam-length"] = beam_length
                    options = author.options(keymode, mode, overrides)
                    contribution = author.contribution(module, options)
                    frame = author.contribution("frame", options)
                    bindings = [b for c in (author.base(), frame, contribution)
                                for b in material_bindings(c, keymode, mode) if b["mode"] == "Provide"]
                    materials = {(b["slot"], b["resource"]) for b in bindings}
                    selected_templates = {i["template"] for i in contribution.scene.get("instances", [])
                                          if (i["material"]["slot"], i["material"]["resource"]) in materials}
                    roots = [contribution.scene["root"],
                             *(contribution.templates[t] for t in selected_templates)]
                    nodes = {n["animation"]: n for r in roots for n in walk_nodes(r)
                             if n["type"] == "recorded-animation"}
                    if not nodes:
                        raise ValueError(f"No mounted recorded animation for {family}/{module}/{choice['value']}/{mode}")
                    for animation, node in nodes.items():
                        key = (family, contribution.manifest_path, animation)
                        if key in jobs:
                            jobs[key]["modeConsumers"].append(mode)
                            continue
                        desc = sources.describe(family, author.root, contribution.manifest_path, animation)
                        program = desc["program"]
                        mesh_counts = [sum(program["nodes"][entry[0]].get("geometry") is not None
                                           for entry in f["nodes"]) for f in program["frames"]]
                        normal = additive = 0
                        for definition in program["nodes"]:
                            if definition.get("geometry") is not None:
                                if definition.get("blend") == "additive":
                                    additive += 1
                                else:
                                    normal += 1
                        identity = digest(canonical([family, contribution.manifest_path, animation]))[:12]
                        jobs[key] = {"name": f"{family}-{module}-{choice['value']}-{mode}-{identity}",
                                     "family": family, "author": str(author.root), "theme": choice["value"],
                                     "module": module, "choice": choice["value"], "mode": mode,
                                     "beamLength": beam_length if module == "key-beam" and family == "lightning" else None,
                                     "modeConsumers": [mode], "kind": module,
                                     "defaultTheme": choice["value"] == author.modules[module]["default"],
                                     "manifest": contribution.manifest_path, "scene": contribution.scene_path,
                                     "sceneSha256": sources.sha(source_path(author.root, contribution.scene_path)),
                                     "animation": animation, "sourceNode": node["id"], "frames": "all",
                                     "selection": {"rule": "complete-mounted-module-program"},
                                     "sourceSignature": desc["signature"], "sourceFiles": desc["files"],
                                     "sourceDecodedBytes": desc["sourceDecodedBytes"],
                                     "canvas": [program["width"], program["height"]], "fps": program["fps"],
                                     "sourceFrameCount": len(program["frames"]),
                                     "durationMs": (len(program["frames"]) - 1) * 1000 / program["fps"],
                                     "sourceMeshPeak": max(mesh_counts),
                                     "sourceBlendDefinitions": {"normal": normal, "additive": additive},
                                     "oneCanvasPerFrameRgbaBytes": program["width"] * program["height"] *
                                         4 * (len(program["frames"]) - 1)}
    return sorted(jobs.values(), key=lambda j: (modules.index(j["kind"]), j["family"], j["choice"], j["mode"], j["animation"]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--standard", type=Path, required=True)
    parser.add_argument("--lightning", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--modules", default="key-beam,explosion,full-combo")
    parser.add_argument("--beam-lengths", default="normal")
    args = parser.parse_args()
    modules = args.modules.split(",")
    if not modules or any(m not in ("key-beam", "explosion", "full-combo") for m in modules):
        parser.error("Modules must be key-beam,explosion,full-combo")
    beam_lengths = args.beam_lengths.split(",")
    if not beam_lengths or any(v not in ("normal", "short", "veryshort", "long") for v in beam_lengths):
        parser.error("Unknown frozen-author beam length")
    output = args.output.resolve()
    if not output.is_relative_to(DEV.resolve()):
        parser.error("Output must be under the dedicated bake development directory")
    authors = {"standard": str(args.standard.resolve(strict=True)), "lightning": str(args.lightning.resolve(strict=True))}
    sources = Sources()
    by_identity = {}
    for length in beam_lengths:
        for job in collect(authors, modules, sources, length):
            by_identity.setdefault((job["family"], job["manifest"], job["animation"]), job)
    jobs = list(by_identity.values())
    indices = available_indices(DEV, authors, sources)
    summary = statistics(jobs, indices)
    summary["scope"] = "Mounted animations of every requested module choice, all three modes at the default frame"
    summary["byModule"] = {}
    for module in modules:
        rows = [j for j in jobs if j["module"] == module]
        summary["byModule"][module] = {"consumptionIdentities": len(rows),
            "strictSourceIdentities": len({j["sourceSignature"] for j in rows}),
            "contentFrameRange": [min(j["sourceFrameCount"] - 1 for j in rows), max(j["sourceFrameCount"] - 1 for j in rows)],
            "maximumSourceTextureDecodedMiB": max(j["sourceDecodedBytes"] for j in rows) / 1048576,
            "maximumOneCanvasPerFrameMiB": max(j["oneCanvasPerFrameRgbaBytes"] for j in rows) / 1048576}
    output.mkdir()
    (output / "worklist.json").write_text(json.dumps({"contract": "oms-iidx-bake-worklist.v1", "authors": authors,
        "summary": summary, "jobs": jobs, "unavailable": [],
        "executionPolicy": "Inventory only; full-combo requires per-program size and segmentation review before GPU execution"}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))
    print("Worklist:", output / "worklist.json")


if __name__ == "__main__":
    main()
