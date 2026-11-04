"""IIDX26 Graph panel and real OMS statistics, without bundled game artwork.

The original graph_1p/graph_2p movies supply the panel and named text/tag
anchors. Their executable attaches the bars separately, so the three-column
placement and Maximum substitution below are explicit OMS adaptations.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import shutil
from urllib.parse import unquote
import xml.etree.ElementTree as ET


PACKAGE = "contents/data/graphic/0/graph.ifs"
FRAME_IDS = {3, 8, 12, 14, 16, 17, 19, 21, 25, 26}
LOGICAL = 768 / 720
BAR_TOP, BAR_BOTTOM, BAR_WIDTH = 94, 542, 66
BAR_COLUMNS = {"current": 54, "best": 128, "maximum": 202}


def digest(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def node(identifier, kind="container", properties=None, resource=None, *, slot=None, children=()):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"},
              "blend": "inherit", "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        result["resource"] = resource
    if slot is not None:
        result["slot"] = slot
    return result


def build_graph(library: Path, layout: str):
    """Return a read-only contribution for the caller's existing Global marker.

    The caller copies ``files`` byte-for-byte, merges the declarations, and
    continues to suppress decoration at Stage so the panel mounts only once.
    """
    if layout not in ("p1", "p2", "dp"):
        raise ValueError("Graph layout must be p1, p2 or dp")
    library = Path(library).resolve(strict=True)
    metadata = json.loads((library / "library.json").read_text(encoding="utf-8"))
    if metadata["status"] != "complete" or metadata["sourceMetadataUnchanged"] is not True:
        raise ValueError("Graph requires the complete verified IIDX26 library")
    frames = {int(match[1]) for p in metadata["packages"]
              if (match := re.fullmatch(r"contents/data/graphic/gmframe(\d+)\.ifs", p["source"]))}
    if frames != FRAME_IDS:
        raise ValueError("Graph requires the independent IIDX26 cabinet selection")
    packages = [p for p in metadata["packages"] if p["source"] == PACKAGE]
    if len(packages) != 1:
        raise ValueError("Graph requires exactly one source-qualified 0/graph package")
    source_archive = (library / "source-copies" / PACKAGE).resolve(strict=True)
    if not source_archive.is_relative_to(library) or not source_archive.is_file() or digest(source_archive) != packages[0]["sha256"]:
        raise ValueError("Graph archive differs from the private source inventory")
    index = {Path(t["name"]).stem: t for t in metadata["textures"] if t["package"] == PACKAGE}
    texture_list = library / "packages/contents/data/graphic/0/graph/raw/tex/texturelist.xml"
    samplers = {image.attrib["name"]: atlas.attrib["mag_filter"]
                for atlas in ET.parse(texture_list).getroot().findall("texture") for image in atlas.findall("image")}
    result = {key: [] for key in ("resources", "bitmapFonts", "templates", "instances", "bindings",
                                  "variants", "tracks", "files")}
    used = {}
    prefix = "standard.graph." + layout
    left = 0 if layout == "p2" else 1000

    def asset(name, size):
        if name in used:
            return used[name]
        row = index[name]
        if (row["width"], row["height"]) != size:
            raise ValueError(f"Original Graph dimensions changed: {name}")
        source = (library / unquote(row["sprite"])).resolve(strict=True)
        if not source.is_relative_to(library) or not source.is_file() or digest(source) != row["sha256"]:
            raise ValueError(f"Original Graph bytes changed: {name}")
        identifier = "standard.graph.texture." + name.replace("_", "-")
        relative = "standard/graph/source/" + name + ".png"
        result["resources"].append({"id": identifier, "type": "texture", "path": relative, "filter": samplers[name]})
        result["files"].append({"source": str(source), "path": relative, "sha256": row["sha256"]})
        used[name] = identifier
        return identifier

    def rect(x, y, width, height):
        return {"x": (left + x) / 1280, "y": y / 720, "width": width / 1280, "height": height / 720}

    def bind(target, prop, source):
        result["bindings"].append({"id": target + ".bind." + prop, "target": target, "property": prop, "source": source})

    def track(target, prop, source, values, easing="linear"):
        identifier = target + ".track." + prop
        result["tracks"].append({"id": identifier, "type": "tween", "target": target, "property": prop,
                                 "clock": source, "loop": False, "easing": easing,
                                 "keyframes": [{"id": identifier + f".n{i}", "time": time, "value": value}
                                               for i, (time, value) in enumerate(values)]})

    def gated(name, source, children):
        root = node(prefix + "." + name, children=children)
        bind(root["id"], "visible", source)
        return root

    def sprite(name, source, size, x, y):
        return node(prefix + "." + name, "sprite", rect(x, y, *size), asset(source, size))

    font = "standard.graph.font.score"
    result["bitmapFonts"].append({"id": font, "lineHeight": 22,
        "glyphs": [{"character": str(i), "resource": asset(f"gr_sco_{i:02}", (23, 22)), "advance": 23} for i in range(10)]})

    def number(name, source, x, y, width):
        text = node(prefix + "." + name, "text", {**rect(x, y, width, 22), "text": "", "bitmap-font": font,
                    "font-size": 22 * LOGICAL, "format": "integer", "alignment": "right", "colour": "#ffffffff"})
        bind(text["id"], "text", source)
        # Preserve the original four-digit size. Larger OMS numbers fit the
        # same field by changing only its public font-size, never the PNG.
        track(text["id"], "font-size", source,
              [(0, 22 * LOGICAL)] + [(10 ** (digits - 1), 22 * LOGICAL * min(1, width / (23 * digits)))
                                    for digits in (5, 6, 7)]
              + [(10_000_000, 22 * LOGICAL * min(1, width / (23 * 10)))], "step")
        return text

    def caption(name, text, x, y, width, height, size=14, alignment="left"):
        return node(prefix + "." + name, "text", {**rect(x, y, width, height), "text": text,
                    "font-size": size * LOGICAL, "alignment": alignment, "colour": "#ffffffff"})

    base = node(prefix + ".base", properties={"z": -10}, slot="decoration", children=[
        sprite("panel", "gr_score_graph_panel", (280, 720), 0, 0)])
    data = [number("current.value", "score.value", 183, 28, 92)]
    maximum = [caption("maximum.header", "MAXIMUM", 5, 55, 155, 24, 18),
               number("maximum.value", "score.maximum", 183, 57, 92),
               sprite("line.maximum", "gr_line_max", (276, 20), 4, 90)]

    bars = []
    for name, colour, source in (("current", "blue", "score.progress"),
                                  ("best", "green", "score.personal-best-progress"),
                                  ("maximum", "red", None)):
        # The source gauge is 66x448. Public bottom-origin height reproduces
        # the bar's variable geometry; no reveal-y extension or fake clock.
        fill = node(prefix + ".bar." + name + ".fill", "sprite",
                    {"y": 1, "origin": "bottom-left", "height": 0 if source else 1},
                    asset("gr_sco_gauge_large_" + colour, (66, 448)))
        if source:
            bind(fill["id"], "height", source)
        outline = node(prefix + ".bar." + name + ".outline", "sprite", {"opacity": .4, "z": -1},
                       asset("gr_sco_gauge_large_ref", (66, 448)))
        bar = node(prefix + ".bar." + name, properties=rect(BAR_COLUMNS[name], BAR_TOP, BAR_WIDTH, BAR_BOTTOM - BAR_TOP),
                   children=[fill, outline])
        bars.append(gated("bar.best.available", "score.personal-best-available", [bar]) if name == "best" else bar)
    maximum.append(node(prefix + ".bars", children=bars))
    maximum.append(sprite("line.baseline", "gr_line_under", (272, 6), 4, 540))

    ranks = []
    for rank in ("aaa", "aa", "a"):
        rank_line = sprite("line." + rank, "gr_line_" + rank, (272, 18), 4, BAR_BOTTOM - 14)
        # The first horizontal stroke is source row14. Align that stroke to
        # the real score threshold, independently of its decorative letters.
        track(rank_line["id"], "y", "score.rank-" + rank + "-progress",
              [(0, (BAR_BOTTOM - 14) / 720), (1, (BAR_TOP - 14) / 720)])
        ranks.append(rank_line)
    maximum.append(gated("rank.available", "score.rank-available", ranks))

    data.extend([sprite("tag.current", "gr_p_nowsc", (135, 31), 5, 567),
                 sprite("tag.best", "gr_p_bstsc", (135, 31), 140, 567)])
    maximum.extend([sprite("tag.maximum", "gr_score_tag_red", (135, 31), 5, 598),
                    caption("tag.maximum.label", "MAXIMUM", 9, 604, 127, 19, 15, "centre")])
    data.append(gated("maximum.available", "score.maximum-available", maximum))
    data.append(gated("best.available", "score.personal-best-available", [
        number("best.value", "score.personal-best", 143, 603, 128)]))
    no_best = caption("best.missing", "NO DATA", 143, 606, 128, 18, 14, "centre")
    result["variants"].append({"id": no_best["id"] + ".variant", "target": no_best["id"], "property": "visible",
        "source": "score.personal-best-available", "default": True,
        "cases": [{"id": no_best["id"] + ".known", "key": "true", "value": False}]})
    data.append(no_best)
    foreground = node(prefix + ".data", properties={"z": -30}, slot="decoration", children=data)
    for root in (base, foreground):
        bind(root["id"], "visible", "option.graph-visible")
        template = root["id"] + ".template"
        result["templates"].append({"id": template, "root": root})
        result["instances"].append({"id": template + ".instance", "template": template,
                                    "material": {"slot": "decoration", "resource": f"markers/frame-{layout}-information"}})
    result["receipt"] = {
        "contract": "oms-standard-graph.v1", "sourcePackage": PACKAGE, "sourcePackageSHA256": packages[0]["sha256"],
        "layout": layout, "sourceMovie": "graph_2p" if layout == "p2" else "graph_1p", "originalCanvas": [1280, 720],
        "originalAnchors": {"panel": [left, 0, 280, 720], "myscore": [left + 183, 28, 23, 22],
                            "target_score": [left + 183, 57, 23, 22], "score_tag_0": [left + 5, 567, 135, 31],
                            "score_tag_1": [left + 140, 567, 135, 31], "score_tag_2": [left + 5, 598, 135, 31],
                            "gr_line_max": [left + 4, 90, 276, 20], "gr_line_under": [left + 4, 540, 272, 6]},
        "sourceBytes": "All PNG files are complete original extracted sprites, copied without pixel edits.",
        "layers": {"base": -10, "existingGraphAreaArt": -20, "data": -30, "lowerDepthIsInFront": True},
        "barAdaptation": {"sourceSize": [66, 448], "localX": BAR_COLUMNS, "top": BAR_TOP, "bottom": BAR_BOTTOM,
                          "placement": "OMS three-column placement; original executable-attached bar positions are not asserted.",
                          "height": "Current/maximum and matching local-best/maximum; Maximum is1 only while the real maximum is available."},
        "semanticAdaptation": "The red third bar and original target_score anchor show theoretical MAXIMUM, labelled explicitly; no PACEMAKER, rival, arena or fabricated score.",
        "rankLines": "Original AAA/AA/A sprites use native score.rank-*-progress and disappear when score.rank-available is false.",
        "scoreText": "Original23x22 digits; wider OMS values shrink inside the same original field. Best missing is NO DATA.",
        "dp": "Explicit reuse of the source P1 right-side Graph panel; no claim of a separate DP graph movie.",
        "mount": "Caller supplies option.graph-visible and Global marker; Stage decoration must be Suppress to avoid inherited copies."}
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    output = args.output.resolve()
    library = args.library.resolve(strict=True)
    if output.exists() or output.drive.casefold() == "c:" or output.is_relative_to(library) or library.is_relative_to(output):
        parser.error("Use a new non-system output directory outside the source library")
    contributions = {layout: build_graph(library, layout) for layout in ("p1", "p2", "dp")}
    output.mkdir(parents=True)
    files = {row["path"]: row for value in contributions.values() for row in value["files"]}
    for relative, row in files.items():
        path = output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(row["source"], path)
        if digest(path) != row["sha256"]:
            raise ValueError("Graph source copy changed")
    for layout, value in contributions.items():
        (output / (layout + ".json")).write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"output": str(output), "layouts": list(contributions), "originalPNGFiles": len(files)}))


if __name__ == "__main__":
    main()
