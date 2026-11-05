"""IIDX26 Graph art and read-only score presentation; original art remains external."""
from __future__ import annotations
import copy
import hashlib
import json
from pathlib import Path
import re
import struct
from urllib.parse import unquote
import xml.etree.ElementTree as ET

PACKAGE = "contents/data/graphic/0/graph.ifs"
FRAME_IDS = {3, 8, 12, 14, 16, 17, 19, 21, 25, 26}
LOGICAL = 768 / 720
BAR_TOP, BAR_BOTTOM, BAR_WIDTH = 94, 542, 66
BAR_COLUMNS = {"current": 54, "best": 128, "target": 202}
TARGETS = {"aaa": "AAA", "aa": "AA", "a": "A", "custom": "CUSTOM %", "next": "NEXT", "next-plus": "NEXT+"}
GraphOPTIONS = [
    {"id": "graph-difference", "type": "toggle", "label": "Graph score difference", "default": True},
    {"id": "graph-custom-percent", "type": "number", "label": "PACEMAKER CUSTOM %", "default": 80,
     "min": 0, "max": 100, "step": 1, "rulesets": ["bms"]},
]
_ABSENT = object()


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def node(identifier, kind="container", properties=None, resource=None, *, slot=None, children=()):
    value = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
             "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        value["resource"] = resource
    if slot is not None:
        value["slot"] = slot
    return value


def bar_columns(layout):
    return {role: 256 - x if layout == "p2" else x for role, x in BAR_COLUMNS.items()}


class _Graph:
    def __init__(self, library, layout, prepared_font=None, *, target=None):
        if layout not in ("p1", "p2", "dp"):
            raise ValueError("Graph layout must be p1, p2 or dp")
        self.library = Path(library).resolve(strict=True)
        self.layout, self.left = layout, 0 if layout == "p2" else 1000
        self.prefix = "standard.graph." + (layout if target is None else f"target.{target}.{layout}")
        metadata = json.loads((self.library / "library.json").read_text(encoding="utf-8"))
        if metadata["status"] != "complete" or metadata["sourceMetadataUnchanged"] is not True:
            raise ValueError("Graph requires the complete verified IIDX26 library")
        frames = {int(match[1]) for package in metadata["packages"]
                  if (match := re.fullmatch(r"contents/data/graphic/gmframe(\d+)\.ifs", package["source"]))}
        if frames != FRAME_IDS:
            raise ValueError("Graph requires the independent IIDX26 cabinet selection")
        packages = [package for package in metadata["packages"] if package["source"] == PACKAGE]
        if len(packages) != 1:
            raise ValueError("Graph requires exactly one source-qualified 0/graph package")
        archive = (self.library / "source-copies" / PACKAGE).resolve(strict=True)
        if not archive.is_relative_to(self.library) or not archive.is_file() or digest(archive) != packages[0]["sha256"]:
            raise ValueError("Graph archive differs from the private source inventory")
        self.source_sha = packages[0]["sha256"]
        self.index = {Path(row["name"]).stem: row for row in metadata["textures"] if row["package"] == PACKAGE}
        texture_list = self.library / "packages/contents/data/graphic/0/graph/raw/tex/texturelist.xml"
        self.samplers = {image.attrib["name"]: atlas.attrib["mag_filter"]
                         for atlas in ET.parse(texture_list).getroot().findall("texture") for image in atlas.findall("image")}
        self.value = {key: [] for key in ("resources", "bitmapFonts", "animations", "templates", "instances", "bindings",
                                          "variants", "tracks", "stateMachines", "numericSources", "files")}
        self.used, self.depths = {}, {}
        self.small_font = None
        if prepared_font is not None:
            self.small_font = prepared_font["fonts"]["small"]
            fonts = [font for font in prepared_font["bitmapFonts"] if font["id"] == self.small_font]
            if len(fonts) != 1 or not set("0123456789-").issubset({glyph["character"] for glyph in fonts[0]["glyphs"]}):
                raise ValueError("Graph differences need the prepared source small numeric font")
            self.value["bitmapFonts"].extend(copy.deepcopy(fonts))
            resources = {glyph["resource"] for glyph in fonts[0]["glyphs"]}
            rows = [row for row in prepared_font["resources"] if row["id"] in resources]
            if len(rows) != len(resources):
                raise ValueError("Prepared Graph font resource closure is incomplete")
            self.value["resources"].extend(copy.deepcopy(rows))
            paths = {row["path"] for row in rows}
            files = [row for row in prepared_font["files"] if row["path"] in paths]
            if {row["path"] for row in files} != paths:
                raise ValueError("Prepared Graph font file closure is incomplete")
            for row in files:
                if digest(row["source"]) != row["sha256"]:
                    raise ValueError("Prepared Graph font bytes changed")
            self.value["files"].extend(copy.deepcopy(files))
        self.score_font = "standard.graph.font.score"
        self.value["bitmapFonts"].append({"id": self.score_font, "lineHeight": 22,
            "glyphs": [{"character": str(i), "resource": self.asset(f"gr_sco_{i:02}", (23, 22)), "advance": 23} for i in range(10)]})

    def asset(self, name, size):
        if name in self.used:
            return self.used[name]
        row = self.index[name]
        if (row["width"], row["height"]) != size:
            raise ValueError(f"Original Graph dimensions changed: {name}")
        source = (self.library / unquote(row["sprite"])).resolve(strict=True)
        if not source.is_relative_to(self.library) or not source.is_file() or digest(source) != row["sha256"]:
            raise ValueError(f"Original Graph bytes changed: {name}")
        identifier = "standard.graph.texture." + name.replace("_", "-")
        relative = "standard/graph/source/" + name + ".png"
        self.value["resources"].append({"id": identifier, "type": "texture", "path": relative, "filter": self.samplers[name]})
        self.value["files"].append({"source": str(source), "path": relative, "sha256": row["sha256"]})
        self.used[name] = identifier
        return identifier

    def rect(self, x, y, width, height):
        return {"x": (self.left + x) / 1280, "y": y / 720, "width": width / 1280, "height": height / 720}

    def bind(self, target, prop, source):
        self.value["bindings"].append({"id": target + ".bind." + prop, "target": target, "property": prop, "source": source})

    def track(self, target, prop, source, points, easing="linear"):
        identifier = target + ".track." + prop
        self.value["tracks"].append({"id": identifier, "type": "tween", "target": target, "property": prop,
            "clock": source, "loop": False, "easing": easing,
            "keyframes": [{"id": identifier + f".n{i}", "time": time, "value": value} for i, (time, value) in enumerate(points)]})

    def gate(self, name, source, children, *, when=True):
        root = node(self.prefix + "." + name, children=children)
        if when:
            self.bind(root["id"], "visible", source)
        else:
            self.variant(root["id"], source, {"false": True})
        return root

    def variant(self, target, source, cases):
        self.value["variants"].append({"id": target + ".visible", "target": target, "property": "visible", "source": source,
            "default": False, "cases": [{"id": target + f".case.n{i}", "key": key, "value": value} for i, (key, value) in enumerate(cases.items())]})

    def sprite(self, name, source, size, x, y):
        return node(self.prefix + "." + name, "sprite", self.rect(x, y, *size), self.asset(source, size))

    def number(self, name, source, x, y, width, *, small=False):
        height, advance = (18, 8) if small else (22, 23)
        if small and self.small_font is None:
            raise ValueError("Graph differences require the prepared source font")
        text = node(self.prefix + "." + name, "text", {**self.rect(x, y, width, height), "text": "",
            "bitmap-font": self.small_font if small else self.score_font, "font-size": height * LOGICAL,
            "format": "integer", "alignment": "right", "colour": "#ffffffff"})
        self.bind(text["id"], "text", source)
        # The real maximum bounds signed differences too; never fit by a fake clock.
        self.track(text["id"], "font-size", "score.maximum", [(0, height * LOGICAL)] +
            [(10 ** (digits - 1), height * LOGICAL * min(1, width / (advance * (digits + int(small))))) for digits in (5, 6, 7)] +
            [(10_000_000, height * LOGICAL * min(1, width / (advance * (10 + int(small)))))], "step")
        return text

    def caption(self, name, text, x, y, width, height=20, size=13, alignment="left"):
        return node(self.prefix + "." + name, "text", {**self.rect(x, y, width, height), "text": text,
            "font-size": size * LOGICAL, "alignment": alignment, "colour": "#ffffffff"})

    def bar(self, name, role, source):
        height = BAR_BOTTOM - BAR_TOP
        fill = node(self.prefix + ".bar." + name + ".fill", "sprite",
            {"width": BAR_WIDTH / height, "height": 1, "scale-y": height / BAR_WIDTH,
             "rotation": 90, "origin": "bottom-left"}, self.asset("gr_sco_gauge_" + str({"current": 0, "best": 1, "target": 2}[role]), (66, 448)))
        clip = node(self.prefix + ".bar." + name + ".clip", "clip", {"clip-mode": "bounds", "reveal-x": 0}, children=[fill])
        self.bind(clip["id"], "reveal-x", source)
        # reveal-x fixes child coordinates while clipping. Opposite rotations
        # preserve source scale/UVs and turn it into bottom-up vertical crop.
        return node(self.prefix + ".bar." + name,
            properties={**self.rect(bar_columns(self.layout)[role], BAR_BOTTOM, height, BAR_WIDTH), "rotation": -90}, children=[clip])

    def numeric(self, name, operation, left, right=_ABSENT):
        identifier = self.prefix + ".value." + name
        row = {"id": identifier, "operation": operation, "left": left}
        operands = [left]
        if right is not _ABSENT:
            row["right"] = right
            operands.append(right)
        depth = 1 + max((self.depths[value[6:]] if isinstance(value, str) and value.startswith("value.") else 0 for value in operands), default=0)
        if identifier in self.depths or depth > 16 or len(self.value["numericSources"]) >= 64:
            raise ValueError("Graph arithmetic exceeds the existing declaration/depth contract")
        self.depths[identifier] = depth
        self.value["numericSources"].append(row)
        return "value." + identifier

    def sum(self, name, values):
        values = list(values)
        level = 0
        while len(values) > 1:
            values = [self.numeric(f"{name}.l{level}.n{i // 2}", "add", values[i], values[i + 1])
                      if i + 1 < len(values) else values[i] for i in range(0, len(values), 2)]
            level += 1
        return values[0]

    def mount(self, root):
        self.bind(root["id"], "visible", "option.graph-visible")
        template = root["id"] + ".template"
        self.value["templates"].append({"id": template, "root": root})
        self.value["instances"].append({"id": template + ".instance", "template": template,
            "material": {"slot": "decoration", "resource": f"markers/frame-{self.layout}-information"}})

    def receipt(self):
        return {"contract": "oms-standard-graph.v2", "sourcePackage": PACKAGE, "sourcePackageSHA256": self.source_sha,
            "layout": self.layout, "sourceMovie": "graph_2p" if self.layout == "p2" else "graph_1p", "originalCanvas": [1280, 720],
            "panel": [self.left, 0, 280, 720], "barPlacement": {"localX": bar_columns(self.layout), "top": BAR_TOP, "bottom": BAR_BOTTOM, "width": BAR_WIDTH},
            "barGeometry": "Original gauge_0/1/2 PNGs; opposite rotations plus reveal-x retain source scale/UVs while cropping upward from the baseline.",
            "sourceBytes": "Complete extracted source PNG bytes; no raster edits.",
            "layers": {"base": -10, "existingGraphAreaArt": -20, "data": -30},
            "dp": "Explicit reuse of source P1 right-side panel; no separate DP graph movie is asserted.",
            "numericDeclarations": len(self.value["numericSources"]), "numericDepth": max(self.depths.values(), default=0),
            "mount": "Existing Global frame information marker only; Stage decoration remains suppressed."}


def build_graph(library: Path, layout: str, prepared_font=None):
    """Panel/current score/terminal local PB. PACEMAKER is a separate module."""
    graph = _Graph(library, layout, prepared_font)
    base = node(graph.prefix + ".base", properties={"z": -10}, slot="decoration", children=[
        graph.sprite("panel", "gr_score_graph_panel", (280, 720), 0, 0)])
    bars = [graph.bar("current", "current", "score.progress"),
        graph.gate("bar.best.available", "score.personal-best-available", [graph.bar("best", "best", "score.personal-best-progress")]),
        graph.gate("bar.best.missing", "score.personal-best-available", [graph.bar("best-fallback", "best", "score.progress")], when=False),
        graph.sprite("line.maximum", "gr_line_max", (276, 20), 4, 90),
        graph.sprite("line.baseline", "gr_line_under", (272, 6), 4, 540)]
    ranks = []
    for rank in ("aaa", "aa", "a"):
        line = graph.sprite("line." + rank, "gr_line_" + rank, (272, 18), 4, BAR_BOTTOM - 14)
        graph.track(line["id"], "y", "score.rank-" + rank + "-progress", [(0, (BAR_BOTTOM - 14) / 720), (1, (BAR_TOP - 14) / 720)])
        ranks.append(line)
    bars.append(graph.gate("rank.available", "score.rank-available", ranks))
    best = [graph.caption("best.label", "LOCAL PB TOTAL", 143, 572, 128, size=12),
            graph.number("best.value", "score.personal-best", 143, 603, 128)]
    if prepared_font is not None:
        difference = graph.numeric("pb-difference", "subtract", "score.value", "score.personal-best")
        best.append(graph.gate("best.difference.visible", "option.graph-difference", [
            graph.caption("best.difference.label", "PB TOTAL DIFF", 143, 637, 128, size=11),
            graph.number("best.difference", difference, 143, 656, 128, small=True)]))
    mania = graph.caption("mania.pacemaker", "EX PACEMAKER: BMS ONLY", 5, 57, 270, size=13)
    graph.variant(mania["id"], "layout.ruleset", {"mania": True})
    data = node(graph.prefix + ".data", properties={"z": -30}, slot="decoration", children=[
        graph.number("current.value", "score.value", 183, 28, 92),
        graph.sprite("tag.current", "gr_p_nowsc", (135, 31), 5, 567),
        graph.gate("maximum.available", "score.maximum-available", bars),
        graph.gate("best.available", "score.personal-best-available", best), mania])
    graph.mount(base)
    graph.mount(data)
    graph.value["receipt"] = {**graph.receipt(),
        "localBest": "Matching local PB final total, explicitly labelled LOCAL PB TOTAL. No per-note ghost is invented. Without PB, second bar follows current and PB labels/difference disappear.",
        "target": "Separate BMS-only six-choice PACEMAKER module; no MAXIMUM placeholder.",
        "differences": prepared_font is not None, "differenceFont": graph.small_font,
        "rankLines": "Original rank sprites use real EX thresholds and their availability gate."}
    return graph.value


def _target_value(graph, target):
    maximum, best = "score.maximum", "score.personal-best"
    if target in ("aaa", "aa", "a"):
        return "score.rank-" + target
    if target == "custom":
        product = graph.numeric("custom-product", "multiply", maximum, "option.graph-custom-percent")
        ratio = graph.numeric("custom-ratio", "divide", product, 100)
        return graph.numeric("target", "floor", ratio)
    # Native snapshot PB is nonnegative by contract; original upper clamp stays.
    best = graph.numeric("bounded-best", "min", best, maximum)
    if target == "next":
        boundaries = ["score.rank-a", "score.rank-aa", "score.rank-aaa", maximum]
        thresholds = []
        for i, (lower, upper) in enumerate(zip(boundaries, boundaries[1:])):
            total = graph.numeric(f"midpoint.n{i}.sum", "add", lower, upper)
            half = graph.numeric(f"midpoint.n{i}.half", "divide", total, 2)
            midpoint = graph.numeric(f"midpoint.n{i}", "floor", half)
            thresholds.extend((lower, midpoint))
        thresholds.append(maximum)
        deductions = []
        for i, (lower, upper) in enumerate(zip(thresholds, thresholds[1:])):
            step = graph.numeric(f"band.n{i}.step", "subtract", upper, lower)
            remaining = graph.numeric(f"band.n{i}.remaining", "subtract", lower, best)
            nonnegative = graph.numeric(f"band.n{i}.positive", "max", remaining, 0)
            selected = graph.numeric(f"band.n{i}.selected", "min", nonnegative, 1)
            deductions.append(graph.numeric(f"band.n{i}.deduction", "multiply", step, selected))
        result = graph.numeric("target-unclamped", "subtract", maximum, graph.sum("deductions", deductions))
        # This also proves the digit-only target font's nonnegative domain to
        # the public codec; it does not change any valid native target.
        return graph.numeric("target", "max", result, 0)
    rate = struct.unpack("<f", struct.pack("<f", .005))[0]
    span = graph.numeric("span", "subtract", maximum, "score.rank-a")
    safe_span = graph.numeric("safe-span", "max", span, 1)
    gap = graph.numeric("gap", "subtract", maximum, best)
    numerator = graph.numeric("rate-numerator", "multiply32", gap, rate)
    ratio = graph.numeric("rate-ratio", "divide32", numerator, safe_span)
    capped = graph.numeric("rate-capped", "min", ratio, rate)
    nonnegative = graph.numeric("rate-positive", "max", capped, 0)
    raw_increment = graph.numeric("increment-raw", "multiply32", nonnegative, maximum)
    integer_increment = graph.numeric("increment-integer", "floor", raw_increment)
    increment = graph.numeric("increment", "max", integer_increment, 1)
    candidate = graph.numeric("candidate", "add", best, increment)
    capped_target = graph.numeric("target-cap", "min", candidate, maximum)
    return graph.numeric("target", "max", capped_target, "score.rank-a")


def build_graph_target(library: Path, layout: str, target: str, prepared_font):
    """One selected policy participates in the scene, not all six alternatives."""
    if target not in TARGETS:
        raise ValueError("Unknown IIDX26 PACEMAKER policy")
    if prepared_font is None:
        raise ValueError("PACEMAKER requires the prepared source numeric font")
    graph = _Graph(library, layout, prepared_font, target=target)
    total = _target_value(graph, target)
    count = graph.sum("judged", ["judgement.count." + name for name in ("perfect", "great", "good", "meh", "miss")])
    judged_maximum = graph.numeric("judged-maximum", "multiply", count, 2)
    pace_product = graph.numeric("pace-product", "multiply32", judged_maximum, total)
    pace_fraction = graph.numeric("pace-fraction", "divide32", pace_product, "score.maximum")
    pace = graph.numeric("pace", "ceil", pace_fraction)
    progress = graph.numeric("pace-progress", "divide", pace, "score.maximum")
    difference = graph.numeric("difference", "subtract", "score.value", pace)
    content = [graph.sprite("header", "gr_gt_pacemaker", (160, 28), 5, 53),
        graph.number("total", total, 183, 57, 92), graph.bar("target", "target", progress),
        graph.sprite("tag", "gr_score_tag_red", (135, 31), 5, 598),
        graph.caption("label", TARGETS[target], 9, 604, 127, 19, 15, "centre"),
        graph.gate("difference.visible", "option.graph-difference", [
            graph.caption("difference.label", "TARGET DIFF", 9, 637, 127, size=11),
            graph.number("difference", difference, 9, 656, 127, small=True)])]
    if target == "custom":
        content.append(graph.number("custom-percent", "option.graph-custom-percent", 7, 680, 45, small=True))
        content.append(graph.caption("custom-percent.label", "% OF MAX", 58, 681, 83, 18, 11))
    ready = graph.gate("available", "score.rank-available", content)
    root = node(graph.prefix + ".data", properties={"z": -30}, slot="decoration", children=[ready])
    graph.mount(root)
    graph.value["receipt"] = {**graph.receipt(), "target": target, "label": TARGETS[target],
        "targetTotalSource": total, "targetPaceSource": pace, "targetProgressSource": progress, "differenceSource": difference,
        "availability": "BMS EX score.rank-available; module is BMS-only. No mania EX pacemaker is inferred.",
        "pace": "ceilf(float32(float32(judgedMaximumEX*target)/maximumEX)); judgedMaximumEX=2*(PG+GR+GD+BD+Miss), excluding empty POOR/Ignore and HCN gauge-only ticks.",
        "policy": "Original AAA/AA/A integer thresholds; CUSTOM floor(maximum*percent/100); NEXT floor of rounded rank midpoints; NEXT+ original float32 arithmetic, truncation and A..MAX limits.",
        "missingPB": "Existing public missing-PB numeric source is zero; target calculations use zero without claiming a stored PB.",
        "difference": "Real current score minus current target pace, using the source small numeric font."}
    return graph.value


def main():
    import argparse
    import shutil
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--source-font", type=Path)
    args = parser.parse_args()
    library, output = args.library.resolve(strict=True), args.output.resolve()
    if output.exists() or output.drive.casefold() == "c:" or output.is_relative_to(library) or library.is_relative_to(output):
        parser.error("Use a new non-system output directory outside the source library")
    output.mkdir(parents=True)
    prepared_font = None
    if args.source_font:
        from standard_feedback import prepare_feedback_font
        prepared_font = prepare_feedback_font(args.source_font, output)
    contributions = {layout: build_graph(library, layout, prepared_font) for layout in ("p1", "p2", "dp")}
    files = {row["path"]: row for value in contributions.values() for row in value["files"]}
    for relative, row in files.items():
        path = output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.resolve() != Path(row["source"]).resolve():
            shutil.copyfile(row["source"], path)
        if digest(path) != row["sha256"]:
            raise ValueError("Graph source copy changed")
    for layout, value in contributions.items():
        (output / (layout + ".json")).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "layouts": list(contributions), "originalPNGFiles": len(files)}))


if __name__ == "__main__":
    main()
