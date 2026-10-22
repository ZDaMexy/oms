"""Original Lightning main-screen art on truthful, read-only OMS statistics.

This is an author helper, not a gameplay adapter.  It consumes a verified,
independent texture library and never reads a game installation or player data.
The Graph Area artwork is deliberately a separate contribution between the
original graph backplate and these foreground statistics.
"""

from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import struct
import zlib

CANVAS = (1920, 1080)
LAYOUTS = ("p1", "p2", "dp")
GRAPH_FONT = "omni.graph.score"
JUDGE_FONT = "omni.lighting.counter"
GRAPH_PACKAGE = "contents/data/graphic/1/graph.ifs"
JUDGE_PACKAGE = "contents/data/graphic/1/gameparts.ifs"

# Source graph_1p/x_graph geometry; the 2P movie translates by -1502.
GRAPH_LEFT = {"p1": 1502, "p2": 0, "dp": 1502}
GRAPH_BOUNDS = {layout: (left, 0, 418, 1080) for layout, left in GRAPH_LEFT.items()}

# These are author placements in the main-screen bottom margin.  The original
# judge movie proves its internal coordinates, not every Frame's host anchor.
JUDGE_BOUNDS = {"p1": (596, 923, 280, 142), "p2": (1044, 923, 280, 142),
                "dp": (548, 923, 280, 142)}

JUDGE_SOURCES = {"fast": "judgement.count.fast", "slow": "judgement.count.slow",
                 "offset": "offset.value", "offset-available": "offset.available", "automatic": "offset.automatic"}
PREPARATION_CLOCK = "preparation.elapsed"
JUDGE_ENTRANCE_MS = 500
GRAPH_TARGETS = {
    "maximum": {"label": "MAXIMUM", "value": "score.maximum", "progress": None, "available": "score.maximum-available"},
    "personal-best": {"label": "MY BEST", "value": "score.personal-best", "progress": "score.personal-best-progress",
                      "available": "score.personal-best-available"},
    **{rank: {"label": rank.upper(), "value": "score.rank-" + rank, "progress": "score.rank-" + rank + "-progress",
              "available": "score.rank-available"} for rank in ("aaa", "aa", "a")},
}


def options():
    """The independent Graph Area art option and graph-visible gate already exist."""
    return [{"id": "graph-target", "type": "choice", "label": "TARGET SCORE GRAPH · 比较目标", "default": "maximum",
             "choices": [{"value": value, "label": label} for value, label in (
                 ("maximum", "理论满分"), ("personal-best", "本地个人最好"), ("aaa", "AAA（BMS）"),
                 ("aa", "AA（BMS）"), ("a", "A（BMS）"))]},
            {"id": "judge-panel-visible", "type": "toggle", "label": "JUDGE · 实时判定数", "default": True}]


def _independent(path: Path):
    path = Path(path)
    if path.drive.lower() == "d:":
        raise ValueError("Lighting panels only consume independent author libraries")
    result = path.resolve(strict=True)
    if result.drive.lower() == "d:":
        raise ValueError("Lighting panels only consume independent author libraries")
    return result


def _contained(root: Path, relative: str):
    if not isinstance(relative, str) or not relative or "\\" in relative or ":" in relative:
        raise ValueError("A panel asset must have a contained relative path")
    candidate = Path(relative)
    if candidate.is_absolute() or ".." in candidate.parts:
        raise ValueError("A panel asset path escapes its independent library")
    result = (root / candidate).resolve()
    if not result.is_relative_to(root):
        raise ValueError("A panel asset path escapes its independent library")
    return result


def _node(identifier, kind="container", properties=None, *, resource=None, children=(), slot=None):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
              "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        result["resource"] = resource
    if slot is not None:
        result["slot"] = slot
    return result


def _box(identifier, bounds, *, resource=None, **properties):
    x, y, width, height = bounds
    return _node(identifier, "sprite" if resource is not None else "container",
                 {"x": x / CANVAS[0], "y": y / CANVAS[1], "width": width / CANVAS[0],
                  "height": height / CANVAS[1], **properties}, resource=resource)


def _text(identifier, caption, bounds, size, *, font=None, **properties):
    result = _box(identifier, bounds, text=caption, **{"font-size": size * 768 / 1080,
                       "colour": "#ffffffff", **properties})
    result["type"] = "text"
    if font is not None:
        result["properties"].update({"bitmap-font": font, "format": "integer"})
    return result


def _bind(target, property_name, source):
    return {"id": target + ".bind." + property_name, "target": target,
            "property": property_name, "source": source}


def _show(target, source, key):
    return {"id": target + ".show", "target": target, "property": "visible", "source": source,
            "default": False, "cases": [{"id": target + ".show.case", "key": key, "value": True}]}


def prepare_assets(library: Path, output: Path):
    """Copy original PNG bytes after their library hash/size/filter validation.

    The existing graph font keeps its ID and metrics.  No generated pixel,
    repainted panel, cropped PNG or replacement source artwork is introduced.
    """
    library, output = _independent(library), _independent(output)
    if output.is_relative_to(library) or library.is_relative_to(output):
        raise ValueError("The panel author output and read-only source library must be independent")
    rows = json.loads((library / "library.json").read_text(encoding="utf-8"))["textures"]
    by_key = {}
    for row in rows:
        key = (row["package"], row["name"])
        if key in by_key:
            raise ValueError("The panel library has a duplicate exact asset identity")
        by_key[key] = row
    resources, sources, graph_glyphs, judge_glyphs = [], [], [], []

    def asset(package, name, size, identifier, relative):
        row = by_key[(package, name + ".png")]
        source = _contained(library, row["sprite"])
        data = source.read_bytes()
        sha256 = hashlib.sha256(data).hexdigest()
        if sha256 != row["sha256"] or (row["width"], row["height"]) != size:
            raise ValueError("A verified original panel texture changed")
        if (len(data) < 33 or data[:16] != b"\x89PNG\r\n\x1a\n\x00\x00\x00\x0dIHDR"
                or struct.unpack(">II", data[16:24]) != size
                or zlib.crc32(data[12:29]) != struct.unpack(">I", data[29:33])[0]):
            raise ValueError("The original panel PNG dimensions differ from the library")
        filtering = row["sampler"]["mag"]
        if filtering not in ("nearest", "linear") or row["sampler"]["min"] != filtering:
            raise ValueError("The original panel texture has an unsupported source filter")
        destination = _contained(output, relative)
        if destination.exists():
            if destination.read_bytes() != data:
                raise ValueError("The panel output conflicts with the verified original PNG")
        else:
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
        resources.append({"id": identifier, "type": "texture", "path": relative, "filter": filtering})
        sources.append({"package": package, "sprite": row["sprite"], "sha256": sha256,
                        "size": list(size), "destination": relative, "originalPngBytesPreserved": True})
        return identifier

    for digit in range(10):
        identifier = asset(GRAPH_PACKAGE, f"gr_sco_{digit:02}", (36, 24), f"omni.graph.d{digit}",
                           f"art/graph/gr_sco_{digit:02}.png")
        graph_glyphs.append({"character": str(digit), "resource": identifier, "advance": 40})
        identifier = asset(JUDGE_PACKAGE, f"judge_num{digit}", (14, 14), f"omni.lighting.counter.d{digit}",
                           f"art/lighting-panels/judge_num{digit}.png")
        judge_glyphs.append({"character": str(digit), "resource": identifier, "advance": 14})
    for character, name in (("-", "minus"), ("+", "plus")):
        identifier = asset(JUDGE_PACKAGE, "judge_num_" + name, (14, 14), "omni.lighting.counter." + name,
                           "art/lighting-panels/judge_num_" + name + ".png")
        judge_glyphs.append({"character": character, "resource": identifier, "advance": 14})
    for name, size in (("gr_score_graph_panel", (418, 1080)), ("gr_sco_gauge_large_blue", (90, 690)),
                       ("gr_sco_gauge_large_green", (90, 690)), ("gr_sco_gauge_waku", (94, 692)),
                       ("gr_line_max", (418, 18)), ("gr_line_aaa", (410, 18)), ("gr_line_aa", (410, 18)),
                       ("gr_line_a", (410, 18)), ("gr_line_under", (404, 4))):
        asset(GRAPH_PACKAGE, name, size, "omni.lighting." + name.replace("_", "-"),
              "art/lighting-panels/" + name + ".png")
    for name, size in (("judge_waku", (180, 142)), ("judge_auto", (280, 142)),
                       ("judge_auto_flip", (280, 142)), ("judge_auto_on", (58, 22)),
                       ("judge_bar", (76, 16)), ("judge_bar_auto", (76, 16))):
        asset(JUDGE_PACKAGE, name, size, "omni.lighting." + name.replace("_", "-"),
              "art/lighting-panels/" + name + ".png")
    return {"resources": resources,
            "fonts": [{"id": GRAPH_FONT, "lineHeight": 24, "glyphs": graph_glyphs},
                      {"id": JUDGE_FONT, "lineHeight": 14, "glyphs": judge_glyphs}],
            "receipt": {"contract": "oms-iidx-lighting-panel-assets.v1", "sourceTextures": sources,
                        "geometrySource": "independent author-data/contents/data/graphic/1/{graph,gameparts}/{afp,geo}",
                        "artwork": "Only byte-equal original PNGs; no generated replacement panel artwork"}}


def build_graph(identifier, layout, *, targets=None):
    """Return two independent roots, leaving Graph Area artwork between them.

    Optional targets maps option values to label/value/progress/available source
    declarations from actual engine targets.  Without it the second row is theoretical
    maximum.  No rival, personal best or score history is invented here.
    """
    left = GRAPH_LEFT[layout]
    background = _node(identifier + ".background", properties={"z": -10, "visible": False}, slot="decoration",
                       children=[_box(identifier + ".panel", (left, 0, 418, 1080),
                                      resource="omni.lighting.gr-score-graph-panel")])
    foreground = _node(identifier + ".readout", properties={"z": -30, "visible": False}, slot="decoration")
    bindings = [_bind(root["id"], "visible", "option.graph-visible") for root in (background, foreground)]
    variants = []
    # The ordinary OMS pause/settings controls occupy the upper-right corner.
    # Keep both real numbers clear of them, including seven-digit mania scores.
    number_width = 285 if left else 389
    number_properties = {"scale-x": 0.7, "origin": "top-right"}
    def number(suffix, y):
        return _text(identifier + suffix, "", (left + 12 + number_width, y, number_width / 0.7, 24), 24,
                     font=GRAPH_FONT, alignment="right", **number_properties)
    current = number(".current", 50)
    bindings.append(_bind(current["id"], "text", "score.value"))
    available = _node(identifier + ".available", properties={"visible": False})
    bindings.append(_bind(available["id"], "visible", "score.maximum-available"))

    def column(suffix, x, source, colour):
        frame = _box(identifier + "." + suffix + ".frame", (left + x - 2, 178, 94, 692),
                     resource="omni.lighting.gr-sco-gauge-waku")
        fill = _box(identifier + "." + suffix, (left + x, 869, 90, 690),
                    resource="omni.lighting.gr-sco-gauge-large-" + colour,
                    **{"origin": "bottom-left", "scale-y": 1 if source is None else 0})
        if source is not None:
            bindings.append(_bind(fill["id"], "scale-y", source))
        return [frame, fill]

    maximum_label = _text(identifier + ".maximum-label", "MAXIMUM", (left + 12, 88, 180, 18), 14)
    maximum = number(".maximum", 111)
    bindings.append(_bind(maximum["id"], "text", "score.maximum"))
    available["children"] = [maximum_label, maximum,
        _box(identifier + ".maximum-line", (left, 161, 418, 18), resource="omni.lighting.gr-line-max"),
        _box(identifier + ".baseline", (left + 7, 867, 404, 4), resource="omni.lighting.gr-line-under"),
        *column("current-bar", 78, "score.progress", "blue")]
    rank = _node(identifier + ".rank-lines", properties={"visible": False})
    variants.append(_show(rank["id"], "layout.ruleset", "bms"))
    for name, ratio in (("aaa", 8 / 9), ("aa", 7 / 9), ("a", 6 / 9)):
        rank["children"].append(_box(identifier + ".rank-" + name,
            (left + 4, 870 - 690 * ratio - 18, 410, 18), resource="omni.lighting.gr-line-" + name))
    available["children"].append(rank)
    if targets is not None:
        if set(targets) != {"maximum", "personal-best", "aaa", "aa", "a"}:
            raise ValueError("Graph target choices must cover the five declared real target modes")
        available["children"] = [child for child in available["children"] if child not in (maximum_label, maximum)]
        bindings.remove(_bind(maximum["id"], "text", "score.maximum"))
        for choice, sources in targets.items():
            if set(sources) != {"value", "progress", "available", "label"}:
                raise ValueError("Graph targets require an actual value/progress/available contract and a static label")
            selected = _node(identifier + ".target-" + choice, properties={"visible": False})
            variants.append(_show(selected["id"], "option.graph-target", choice))
            target = _node(selected["id"] + ".available", properties={"visible": False})
            bindings.append(_bind(target["id"], "visible", sources["available"]))
            target_label = _text(selected["id"] + ".label", sources["label"], (left + 12, 88, 190, 18), 14)
            target_value = number(".target-" + choice + ".value", 111)
            bar = column("target-" + choice + "-bar", 276, sources["progress"], "green")
            target["children"] = [target_label, target_value, *bar]
            bindings.append(_bind(target_value["id"], "text", sources["value"]))
            selected["children"].append(target)
            available["children"].append(selected)
    foreground["children"] = [current, available]
    return {"roots": [background, foreground], "bindings": bindings, "variants": variants,
            "receipt": {"canvas": list(CANVAS), "bounds": list(GRAPH_BOUNDS[layout]),
                        "source": "1/graph graph_1p/graph_2p + x_graph; panel/digits/grade-line original geometry",
                        "layers": {"panel": -10, "independentGraphAreaArt": -20, "statistics": -30},
                        "authorAdaptation": "Two column positions and normalized OMS score scale; no original host bar algorithm claimed",
                        "rankLines": "BMS EX SCORE only: A=6/9, AA=7/9, AAA=8/9 of actual maximum",
                        "target": "actual caller-provided engine target; unavailable targets are hidden" if targets else "theoretical maximum only",
                        "dp": "author application of right-hand graph; not an original DP capture" if layout == "dp" else None}}


def _panel_sample(identifier, bounds, resource, source_rectangle):
    """Sample original plate pixels through a bounded viewport, without a new PNG.

    The intermediate scale follows the existing source-frame clip helper. It
    keeps tiny shortcut-key patches inside the public relative-size budget.
    """
    x, y, width, height = bounds
    sx, sy, sw, sh = source_rectangle
    if width <= 0 or height <= 0 or sw <= 0 or sh <= 0 or sx < 0 or sy < 0 or sx + sw > 280 or sy + sh > 142:
        raise ValueError("Judge source sampling must stay inside the original plate")
    image = _node(identifier + ".image", "sprite", {
        "x": -sx / (sw * 32), "y": -sy / (sh * 32),
        "width": 280 / (sw * 32), "height": 142 / (sh * 32)}, resource=resource)
    space = _node(identifier + ".space", properties={"width": 4, "height": 4, "scale-x": 8, "scale-y": 8},
                  children=[image])
    return _node(identifier, "clip", {"x": x / 1920, "y": y / 1080, "width": width / 1920,
                 "height": height / 1080, "clip-mode": "bounds"}, children=[space])


def _track(identifier, target, prop, clock, values, *, easing="linear"):
    return {"id": identifier, "type": "tween", "target": target, "property": prop,
            "easing": easing, "loop": False, "clock": clock,
            "keyframes": [{"id": identifier + ".k" + str(index), "time": time, "value": value}
                          for index, (time, value) in enumerate(values)]}


def build_judge(identifier, layout, *, sources, bounds=None, counter_font=JUDGE_FONT):
    """Build BMS original-statistics art and a correctly labelled mania view.

    sources is the actual engine fast/slow/offset/offset-available/automatic
    contract.  This read-only panel never adjusts timing or implements keys.
    """
    if set(sources) != {"fast", "slow", "offset", "offset-available", "automatic"}:
        raise ValueError("Judge panels require the complete actual timing/counter source contract")
    bounds = tuple(bounds if bounds is not None else JUDGE_BOUNDS[layout])
    if len(bounds) != 4 or bounds[2:] != (280, 142):
        raise ValueError("The judge panel uses its original 280 by 142 source canvas")
    x, y, width, height = bounds
    if x < 0 or y < 0 or x + width > 1920 or y + height > 1080:
        raise ValueError("The authored judge panel must fit the main screen")
    root = _node(identifier, properties={"z": -30, "visible": False}, slot="decoration")
    bindings = [_bind(identifier, "visible", "option.judge-panel-visible")]
    variants = []
    # The source plate is static. Its host slides it into the lower margin;
    # this bounded author timing follows that direction without claiming the
    # unavailable original host duration. A preview without preparation keeps
    # the authored static y=0 rather than hiding the panel below the screen.
    tracks = [_track(identifier + ".entrance", identifier, "y", PREPARATION_CLOCK,
                     [(0, (1080 - y) / 1080), (JUDGE_ENTRANCE_MS, 0)], easing="out")]

    def counter(suffix, rectangle, source):
        px, py, pw, ph = rectangle
        properties = {"font": counter_font, "alignment": "right"}
        if counter_font == GRAPH_FONT:
            # The combined 27DP Frame+judgement content already consumes eight
            # font metrics.  Reusing the real graph font retains that budget;
            # source pixels stay unchanged and spacing becomes 14px per digit.
            rectangle = (px + pw, py, pw / 0.6, ph)
            properties.update({"origin": "top-right", "scale-x": 0.6})
        elif counter_font != JUDGE_FONT:
            raise ValueError("Judge counters must use an explicitly supported original font")
        item = text(suffix, "", rectangle, **properties)
        bindings.append(_bind(item["id"], "text", source))
        return item

    def text(suffix, caption, rectangle, size=14, **properties):
        px, py, pw, ph = rectangle
        return _text(identifier + "." + suffix, caption, (x + px, y + py, pw, ph), size, **properties)

    bms = _node(identifier + ".bms", properties={"visible": False})
    mania = _node(identifier + ".mania", properties={"visible": False})
    variants.extend([_show(bms["id"], "layout.ruleset", "bms"), _show(mania["id"], "layout.ruleset", "mania")])
    flipped = layout == "p2"
    plate = "omni.lighting.judge-auto-flip" if flipped else "omni.lighting.judge-auto"
    bms["children"].append(_box(identifier + ".original-frame", (x, y, 280, 142), resource=plate))
    # Sample the plate's own empty black field over the HDD-only 0/9/3 keys.
    # Nothing in this read-only panel advertises a keyboard operation that OMS
    # does not implement. The complete source PNG remains byte-for-byte intact.
    shortcuts = ((91, 10), (7, 10), (7, 113)) if flipped else ((103, 10), (190, 10), (190, 113))
    for index, (px, py) in enumerate(shortcuts):
        bms["children"].append(_panel_sample(identifier + ".shortcut" + str(index),
            (x + px, y + py, 20, 20), plate, (70, 31, 20, 20)))
    # Complete judge_auto has row centres 44/60/.../124. Original 14px
    # glyphs have a 7/7 origin; the display reserves more than four digits.
    count_x = 207 if flipped else 38
    for row, (suffix, source) in enumerate((("pg", "judgement.count.perfect"), ("gr", "judgement.count.great"),
        ("gd", "judgement.count.good"), ("bd", "judgement.count.meh"), ("pr", "judgement.count.miss"),
        ("cb", "combo.breaks"))):
        bms["children"].append(counter("bms-" + suffix, (count_x, 37 + row * 16, 63, 14), source))
    fast_x = 107 if flipped else 110
    for suffix, source, py in (("fast", sources["fast"], 69), ("slow", sources["slow"], 117)):
        bms["children"].append(counter("bms-" + suffix, (fast_x, py, 63, 14), source))
    mania["children"].append(text("mania-title", "JUDGE", (8, 7, 168, 20), 16))
    for row, (caption, suffix) in enumerate((("MAX", "perfect"), ("300", "great"), ("200", "good"),
                                          ("100", "ok"), ("50", "meh"), ("MISS", "miss"))):
        label = text("mania-label-" + suffix, caption, (8, 35 + row * 16, 48, 14), 12)
        value = counter("mania-" + suffix, (56, 35 + row * 16, 58, 14), "judgement.count." + suffix)
        mania["children"].extend([label, value])
    for suffix, source, py, colour in (("fast", sources["fast"], 51, "#33cfffff"),
                                      ("slow", sources["slow"], 99, "#ff4444ff")):
        label = text("mania-label-" + suffix, suffix.upper(), (118, py, 60, 14), 12, colour=colour)
        value = counter("mania-" + suffix, (118, py + 16, 60, 14), source)
        mania["children"].extend([label, value])
    offset = _node(identifier + ".offset", properties={"visible": False})
    bindings.append(_bind(offset["id"], "visible", sources["offset-available"]))
    center = 53 if flipped else 236
    pointer = _node(identifier + ".offset-pointer", children=[_box(identifier + ".offset-bar",
        (x + center - 38, y + 63, 76, 16), resource="omni.lighting.judge-bar")])
    # The original horizontal bar marks the zero reference. The actual signed
    # value is displayed in OMS milliseconds below, rather than relabelling it
    # as the HDD's adjustment units or implying original shortcut controls.
    variants.append({"id": identifier + ".offset-bar.mode", "target": identifier + ".offset-bar",
                     "property": "resource", "source": sources["automatic"], "default": "omni.lighting.judge-bar",
                     "cases": [{"id": identifier + ".offset-bar.auto", "key": "true",
                                "resource": "omni.lighting.judge-bar-auto"}]})
    offset["children"].append(pointer)
    # Keep the source horizontal pointer visible at either edge of the actual
    # readout. The numeric black field is sampled from the original plate.
    offset["children"].append(_panel_sample(identifier + ".offset-field",
        (x + center - 33, y + 63, 66, 16), plate, (60, 31, 66, 16)))
    value = text("offset-value", "", (center - 35, 64, 70, 16), 13,
                 format="signed-fixed-2", alignment="centre")
    bindings.append(_bind(value["id"], "text", sources["offset"]))
    offset["children"].append(text("offset-label", "ms", (center - 10, 82, 20, 12), 9,
                                 alignment="centre", colour="#ddddddff"))
    automatic = _box(identifier + ".automatic", (x + (112 if flipped else 124), y + 9, 58, 22),
                     resource="omni.lighting.judge-auto-on", visible=False)
    bindings.append(_bind(automatic["id"], "visible", sources["automatic"]))
    offset["children"].extend([value, automatic])
    bms["children"].append(offset)
    root["children"] = [bms, mania]
    return {"roots": [root], "bindings": bindings, "variants": variants, "tracks": tracks,
            "receipt": {"canvas": list(CANVAS), "bounds": list(bounds),
                        "source": "1/gameparts judge_auto/flip full original plate, judge_num*, judge_bar/auto and judge_auto_on PNGs",
                        "bmsRows": "PG perfect, GR great, GD good, BD meh, PR miss, CB actual combo breaks; empty POOR ok is not PR",
                        "maniaRows": "MAX/300/200/100/50/MISS are mania's own six judgement results",
                        "authorAdaptation": "Main-screen anchor; actual signed OMS millisecond readout, not IIDX adjustment units; mania retains its own six-tier labels",
                        "controls": "Read-only; plate's own black pixels cover only the original 0/9/3 shortcuts, which are not OMS controls",
                        "automatic": "Original unlit AUTO ADJUST text for OFF; original orange lamp and pointer for actual ON",
                        "entry": {"clock": PREPARATION_CLOCK, "durationMs": JUDGE_ENTRANCE_MS,
                                  "source": "Author timing for the observed host-controlled slide from below; original plate itself is static"},
                        "counterFont": "original judge_num 14px" if counter_font == JUDGE_FONT else
                            "author adaptation: original graph digits at 14px height and 0.6 horizontal scale; existing eight-font budget retained",
                        "unavailableOffset": "Hidden; no default or simulated automatic state"}}


def inject(document, manifest, assets, *, layout, judge_sources, targets=None, judge_bounds=None, counter_font=JUDGE_FONT):
    """Append main-screen templates to an already selected Frame contribution.

    Caller removes the proven old graph template/bindings first.  This helper
    refuses a second lighting-panel injection rather than stacking displays.
    """
    information = [template for template in document["templates"]
                   if template["id"].endswith(f".template.{layout}.information")]
    if len(information) != 1:
        raise ValueError("Lighting panels require one exact full-canvas Frame information owner")
    owner = information[0]
    legacy_prefix = owner["root"]["id"] + ".graph"
    roots = [document["root"], *(item["root"] for item in document["templates"])]
    if any(item["id"] == legacy_prefix or item["id"].startswith(legacy_prefix + ".")
           for root in roots for item in walk(root)):
        raise ValueError("The proven old Graph readout must be removed before injecting its replacement")
    identifier = owner["root"]["id"] + ".lighting-panels"
    if any(item["root"]["id"].startswith(identifier) for item in document["templates"]):
        raise ValueError("The Frame already contains lighting panels")
    graph = build_graph(identifier + ".graph", layout, targets=targets)
    judge = build_judge(identifier + ".judge", layout, sources=judge_sources, bounds=judge_bounds, counter_font=counter_font)
    marker = f"markers/frame-{layout}-information"
    for feature in (graph, judge):
        for root in feature["roots"]:
            template_id = root["id"] + ".template"
            document["templates"].append({"id": template_id, "root": root})
            document["instances"].append({"id": root["id"] + ".instance", "template": template_id,
                                           "material": {"slot": "decoration", "resource": marker}})
        document["bindings"].extend(feature["bindings"])
        document["variants"].extend(feature["variants"])
        document.setdefault("tracks", []).extend(feature.get("tracks", []))
    used_fonts = {GRAPH_FONT, counter_font}
    fonts = [item for item in assets["fonts"] if item["id"] in used_fonts]
    used_resources = {item["resource"] for font in fonts for item in font["glyphs"]}
    used_resources.update(item["resource"] for feature in (graph, judge) for root in feature["roots"]
                          for item in walk(root) if item.get("resource"))
    for feature in (graph, judge):
        for variant in feature["variants"]:
            if variant["property"] == "resource":
                used_resources.add(variant["default"])
                used_resources.update(case["resource"] for case in variant["cases"])
    resources = [item for item in assets["resources"] if item["id"] in used_resources]
    for field, additions in (("resources", resources), ("bitmapFonts", fonts)):
        existing = {item["id"]: item for item in manifest.get(field, [])}
        for item in additions:
            if item["id"] in existing:
                if existing[item["id"]] != item:
                    raise ValueError("A panel resource or font conflicts with the selected Frame")
            else:
                manifest.setdefault(field, []).append(copy.deepcopy(item))
    return {"graph": graph["receipt"], "judge": judge["receipt"]}


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)
