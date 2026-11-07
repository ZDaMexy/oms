"""Inventory authored long-note and key-flash movies without inferring input state."""

import argparse
from collections import Counter
from itertools import product
import json
from pathlib import Path

from batch_frames import DEV, Sources, canonical, digest
from beatoraja_author import AuthorPackage, material_bindings, material_templates, source_path, walk_nodes


def variants(author, module, choice, base):
    values = {}
    for variant in choice.get("variants", []):
        for name, value in variant["when"].items():
            if name in ("layout", "frame") or name not in base:
                continue
            values.setdefault(name, {canonical(base[name]): base[name]})[canonical(value)] = value
    names = sorted(values)
    for combination in product(*(values[name].values() for name in names)):
        yield {module: choice["value"], **dict(zip(names, combination))}


def collect(authors):
    sources = Sources()
    jobs = {}
    declaration_states = Counter()
    for family, root in authors.items():
        author = AuthorPackage(Path(root), family)
        for mode in ("p1", "p2", "dp"):
            keymode = "14k" if mode == "dp" else "7k"
            base = author.options(keymode, mode)
            for module, slot in (("charge-notes", "effect.long-note"), ("frame", "effect.key-flash")):
                seen = set()
                for choice in author.modules[module]["choices"]:
                    for overrides in variants(author, module, choice, base):
                        options = author.options(keymode, mode, overrides)
                        contribution = author.contribution(module, options)
                        # Several option combinations may select the same branch.
                        branch = contribution.manifest_path, contribution.scene_path, contribution.ini_path
                        if branch in seen:
                            continue
                        seen.add(branch)
                        bindings = [b for b in material_bindings(contribution, keymode, mode) if b["slot"] == slot]
                        if not bindings:
                            declaration_states[family + ":" + slot + ":absent"] += 1
                        for binding in bindings:
                            declaration_states[family + ":" + slot + ":" + binding["mode"]] += 1
                            if binding["mode"] != "Provide":
                                continue
                            for root_node in material_templates(contribution, slot, binding["resource"]):
                                for node in (n for n in walk_nodes(root_node) if n["type"] == "recorded-animation"):
                                    animation = node["animation"]
                                    key = family, contribution.manifest_path, animation
                                    consumer = {"module": module, "choice": choice["value"], "mode": mode,
                                                "options": overrides, "slot": slot, "target": binding["target"],
                                                "templateRoot": root_node["id"], "sourceNode": node["id"]}
                                    if key in jobs:
                                        if consumer not in jobs[key]["consumers"]:
                                            jobs[key]["consumers"].append(consumer)
                                        continue
                                    desc = sources.describe(family, author.root, contribution.manifest_path, animation)
                                    program = desc["program"]
                                    token = digest(canonical(key))[:12]
                                    kind = "long-note-effect" if module == "charge-notes" else "key-flash"
                                    jobs[key] = {"name": f"{family}-{kind}-{choice['value']}-{token}",
                                                 "family": family, "author": str(author.root), "theme": choice["value"],
                                                 "kind": kind, "mode": mode, "consumers": [consumer],
                                                 "defaultTheme": choice["value"] == author.modules[module]["default"],
                                                 "manifest": contribution.manifest_path, "scene": contribution.scene_path,
                                                 "sceneSha256": sources.sha(source_path(author.root, contribution.scene_path)),
                                                 "animation": animation, "sourceNode": node["id"], "frames": "all",
                                                 "selection": {"rule": "complete-authored-object-feedback", "slot": slot,
                                                               "trigger": program["trigger"], "clock": node.get("clock"),
                                                               "owner": root_node.get("longNoteEffect")},
                                                 "sourceSignature": desc["signature"], "sourceFiles": desc["files"],
                                                 "sourceDecodedBytes": desc["sourceDecodedBytes"],
                                                 "canvas": [program["width"], program["height"]], "fps": program["fps"],
                                                 "sourceFrameCount": len(program["frames"]),
                                                 "loop": program.get("loop"), "loopStartFrame": program.get("loopStartFrame")}
    return sorted(jobs.values(), key=lambda j: (j["family"], j["kind"], j["sourceSignature"], j["name"])), dict(declaration_states)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--standard", type=Path, required=True)
    parser.add_argument("--lightning", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    if not output.is_relative_to(DEV.resolve()):
        parser.error("Output must remain in the dedicated bake development directory")
    authors = {"standard": str(args.standard.resolve(strict=True)), "lightning": str(args.lightning.resolve(strict=True))}
    jobs, declarations = collect(authors)
    summary = {"jobs": len(jobs), "strictSourcePrograms": len({j["sourceSignature"] for j in jobs}),
               "byKind": dict(Counter(j["family"] + ":" + j["kind"] for j in jobs)),
               "materialDeclarations": declarations,
               "scope": "All catalog choices and contribution-selecting option variants, all BMS presentations. Absence means no authored movie declaration, not absence of original HDD art."}
    output.mkdir()
    (output / "worklist.json").write_text(json.dumps({"contract": "oms-iidx-bake-worklist.v1", "authors": authors,
        "jobs": jobs, "summary": summary, "minimumFreeBytes": 5 * 1024 ** 3,
        "losslessTilesAboveBytes": 512 * 1024 ** 2, "unavailable": []}, indent=2), encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    main()
