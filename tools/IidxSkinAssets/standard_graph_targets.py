"""Export the six OMS PACEMAKER branches with explicitly qualified source art."""
from __future__ import annotations

import copy
import json
from pathlib import Path
import shutil

from standard_graph import GraphOPTIONS, TARGETS, build_graph_target, digest, node
from standard_source_catalog import metadata, families as source_families, styles as source_styles, presentation_family, family_suffix


def export_graph_target_module(library, output, prepared_font, *, notifications=None):
    """Write only this module's exact files and return its catalog contribution.

    Existing identical files are shared with the frame/feedback source closure.
    Conflicting author bytes are rejected, never silently replaced.
    """
    library, output = Path(library).resolve(strict=True), Path(output).resolve()
    if output.drive.casefold() == "c:" or output.is_relative_to(library) or library.is_relative_to(output):
        raise ValueError("Graph target output must be an independent non-system author directory")
    files, receipts, choices = {}, [], []
    source_data = metadata(library)

    def destination(relative):
        target = (output / relative).resolve()
        if not target.is_relative_to(output) or target == output:
            raise ValueError("Graph target output escapes the author directory")
        target.parent.mkdir(parents=True, exist_ok=True)
        return target

    def record(relative, target):
        row = {"path": relative, "source": str(target), "sha256": digest(target)}
        if relative in files and files[relative] != row:
            raise ValueError("Conflicting Graph target file identity")
        files[relative] = row

    def write(relative, value):
        data = value.encode("utf-8") if isinstance(value, str) else (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8")
        target = destination(relative)
        if target.exists():
            if target.read_bytes() != data:
                raise ValueError("Graph target output already contains different bytes: " + relative)
        else:
            with target.open("xb") as stream:
                stream.write(data)
        record(relative, target)

    for target, label in TARGETS.items():
        layouts = {}
        for common_family, layout in ((family, layout) for family in source_families(source_data) for layout in ("p1", "p2", "dp")):
            contribution = build_graph_target(library, layout, target, prepared_font, common_family=common_family,
                notifications=notifications[common_family]["clips"] if notifications is not None else None)
            suffix = family_suffix(common_family)
            family_path = "/" + suffix if suffix else ""
            family_id = "." + suffix if suffix else ""
            base = f"standard/graph{family_path}/targets/{target}/{layout}"
            for row in contribution["files"]:
                source = Path(row["source"]).resolve(strict=True)
                if digest(source) != row["sha256"]:
                    raise ValueError("Graph target source bytes changed")
                copied = destination(row["path"])
                if copied.exists():
                    if digest(copied) != row["sha256"]:
                        raise ValueError("Graph target source collides with existing author bytes")
                else:
                    shutil.copyfile(source, copied)
                record(row["path"], copied)
            manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1",
                "resources": contribution["resources"], "bitmapFonts": contribution["bitmapFonts"], "animations": contribution["animations"]}
            scene = {"contract": "oms-gameplay-skin-scene.v1", "root": node(f"standard.graph{family_id}.target.{target}.{layout}.root")}
            for field in ("tracks", "stateMachines", "bindings", "variants", "templates", "instances", "numericSources"):
                scene[field] = contribution[field]
            write(base + "/manifest.json", manifest)
            write(base + "/scene.json", scene)
            write(base + "/materials.ini", "\n")
            layouts[common_family, layout] = {"manifest": base + "/manifest.json", "scene": base + "/scene.json", "ini": base + "/materials.ini"}
            receipts.append(contribution["receipt"])
        # Public ordered variants select the first matching contribution.
        # A DP publication has no layout option, so each qualified family must
        # follow the two more specific single-player branches.
        choices.append({"value": target, "label": label, **layouts["0", "dp"], "orderedVariants": True,
            "variants": [
                *[variant for style in source_styles(source_data) if presentation_family(style) != '0'
                  for variant in ([{"when": {"frame": f"iidx{style:02}", "layout": layout},
                                     **layouts[presentation_family(style), layout]} for layout in ("p1", "p2")]
                                  + [{"when": {"frame": f"iidx{style:02}"}, **layouts[presentation_family(style), "dp"]}])],
                *[{"when": {"layout": layout}, **layouts["0", layout]} for layout in ("p1", "p2")],
            ]})
    return {"module": {"id": "graph-target", "label": "PACEMAKER", "rulesets": ["bms"], "default": "aaa", "choices": choices},
            "files": list(files.values()), "receipts": receipts, "options": copy.deepcopy(GraphOPTIONS)}
