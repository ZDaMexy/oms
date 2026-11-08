"""Qualified Standard Start/READY art driven by native OMS reading state.

The source window artwork hosts OMS-authored ten-row pages for Normal preset
indices 1..20. This does not reproduce the eleven fixed multipliers observed
in the supplied 24 executable. Classic uses an explicitly labelled multiplier
window. Neither adaptation changes native OMS speed maths.
"""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path

from build_standard_skin import LOGICAL, box, node, rect, sha
from standard_source_catalog import layout_family, styles as source_styles


OPTIONS = [
    {"id": "start-panel", "type": "toggle", "label": "Start 调速面板", "default": True},
    {"id": "ready-animation", "type": "toggle", "label": "GET READY", "default": True},
    {"id": "arrival-cue", "type": "toggle", "label": "首排音符提示（25–26 / ARC SCORE）", "default": True},
]

# Stable frame16 geometry of the six original 0/s21/s22/s23/s24 gameparts movies.
# Each family keeps its own PNGs and UVs; only the observed bounds/hooks agree.
# The numeric
# prototype is hidden by the executable and replaced with font2 fixed cells.
GEOMETRY = {
    "p1": {"window": (350, 180, 101, 304), "row": (360, 195), "select": (353, 443),
           "green": (356, 157), "normal": (429, 248, 14, 168),
           "floating": (429, 214, 14, 184), "current": (356, 430, 89, 24),
           "value": (364, 456), "top": (362, 192, 46, 19), "bottom": (366, 401, 39, 19),
           "slider": (363, 388), "slider_top": 214},
    "p2": {"window": (829, 180, 101, 304), "row": (866, 195), "select": (861, 443),
           "green": (867, 157), "normal": (837, 248, 14, 168),
           "floating": (837, 214, 14, 184), "current": (836, 430, 89, 24),
           "value": (845, 456), "top": (871, 192, 46, 19), "bottom": (875, 401, 39, 19),
           "slider": (872, 388), "slider_top": 214},
    "dp": {"window": (596, 30, 88, 376), "row": (614, 52), "select": (607, 300),
           "green": (614, 6), "value": (605, 310), "slider": (618, 244), "slider_top": 70},
}


def export_ready(library, output, bemani_root, image_tools):
    """Record all three source movies once; art stays in this private export."""
    from standard_components import StandardComponents
    exporter = StandardComponents(library, output, bemani_root, image_tools)
    families = {}
    for family in sorted({layout_family(style) for style in source_styles(exporter.source_data)}):
        clips = families[family] = {}
        for layout, movie in (("p1", "1p_ready"), ("p2", "2p_ready"), ("dp", "dp_ready")):
            prefix = "sr.ready." if family == "0" else "sr.ready." + family + "."
            clip = exporter.clip(f"{family}/gameparts", movie, prefix + layout,
                                 loop=False, trigger="gameplay.attach")
            if clip["source"]["fps"] != 60 or clip["source"]["sourceFrames"] != 90:
                raise ValueError("The qualified READY requires its original 90 frames at 60Hz")
            if family in ("s21", "s22") and (clip["source"]["stage"] != [0, 0, 1280, 720]
                    or clip["source"]["labels"] or clip["source"]["translatedRootControl"]):
                raise ValueError("The qualified22 READY requires its complete canvas and original nonlooping timeline")
            clips[layout] = clip
    result = {"contract": "oms-standard-reading-assets.v1", "librarySHA256": sha(library / "library.json"),
              "clips": families['0'], "families": families, "files": list(exporter.files.values()), "source": exporter.receipts}
    (output / "reading.json").write_text(json.dumps(result, separators=(",", ":")) + "\n", encoding="utf-8")
    return result


def add_reading(frame, ready, prepared_font, font_path):
    """Attach to the frame's one Global information owner; return lane markers."""
    from standard_feedback import rasterize_source_text
    prefix = frame.prefix + ".reading"
    geo = GEOMETRY[frame.layout]

    def image(name, texture, bounds):
        resource, row = frame.asset(layout_family(frame.style) + "/gameparts", texture)
        if (row["width"], row["height"]) != (bounds[2], bounds[3]):
            raise ValueError(f"Source Start geometry changed: {texture}")
        return node(prefix + "." + name, "sprite", rect(box(*bounds)), resource)

    def text(name, value, x, y, width=60, colour="#ffffffff", source=None, fmt="integer", size="normal"):
        height = 20 if size == "normal" else 18
        result = node(prefix + "." + name, "text", {**rect(box(x, y, width, height)), "text": value,
            "bitmap-font": prepared_font["fonts"][size], "font-size": height * LOGICAL, "format": fmt,
            "alignment": "left", "colour": colour})
        if source:
            frame.bind(result["id"], "text", source)
        return result

    def label(name, value, x, y, *, size=12):
        cache = frame.owner.reading_labels
        if value not in cache:
            raster = rasterize_source_text(font_path, value)
            relative = "standard/reading/labels/" + name + ".png"
            path = frame.owner.output / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            raster.save(path)
            frame.owner.files[relative] = sha(path)
            cache[value] = (relative, raster.width, raster.height)
        relative, width, height = cache[value]
        resource = frame.resource({"id": "std.reading.label.h" + sha(frame.owner.output / relative)[:24],
            "type": "texture", "path": relative, "filter": "nearest"})
        return node(prefix + "." + name, "sprite", rect(box(x, y, width * size / 20, height * size / 20)), resource)

    def gate(name, source, children, *, choice=None):
        result = node(prefix + "." + name, children=children)
        if choice is None:
            frame.bind(result["id"], "visible", source)
        else:
            frame.variant(result["id"], "visible", source, False, {choice: True})
        return result

    normal = [image("normal.window", "playm_spd_window" + ("_dp" if frame.dp else ""), geo["window"])]
    if not frame.dp:
        normal.append(image("normal.title", "playm_txt_spd", geo["normal"]))
    x, y = geo["row"]
    sx, sy = geo["select"]
    for page in (0, 1):
        rows = []
        for index in range(10):
            level = 10 + 10 * page - index
            rows.append(text(f"normal.page{page}.row{index}", str(level), x + (14 if level >= 10 else 20), y + 28 * index, 28))
        selector = image(f"normal.page{page}.selected", "playm_spd_sel", (sx, sy, 66, 27))
        frame.steps(selector["id"], "y", "scroll.speed",
                    [(level, (sy - (level - 1 - page * 10) * 28) / 720)
                     for level in range(1 + page * 10, 11 + page * 10)])
        # Selector before the digits, matching the source highlight underneath.
        parent = node(prefix + f".normal.page{page}", children=[selector, *rows])
        frame.steps(parent["id"], "opacity", "scroll.speed", [(0, 1 - page), (11, page)])
        normal.append(parent)
    modes = [gate("normal.mode-gate", "scroll.mode", normal, choice="normal")]

    # The source Floating interval and native OMS Floating interval are both
    # 0.5..10. The displayed multiplier always comes from the native snapshot.
    for mode in ("floating", "classic"):
        window = "playm_spd_window_dp" if frame.dp and mode == "classic" else "playm_spd2_window" + ("_dp" if frame.dp else "")
        children = [image(mode + ".window", window, geo["window"])]
        if not frame.dp:
            children.append(image(mode + ".current", "txt_current_spd", geo["current"]))
            if mode == "floating":
                children.append(image(mode + ".title", "playm_txt_spd2", geo["floating"]))
            children.extend([image(mode + ".top", "txt_spd2_10", geo["top"]),
                             image(mode + ".bottom", "txt_spd2_05", geo["bottom"])])
        elif mode == "classic":
            # DP's Floating labels, ticks and CURRENT label are baked into the
            # window. Keep its upper300 source pixels; the lower generic
            # HI-SPEED CHANGE panel avoids falsely labelling Classic FLOATING.
            original, row = frame.asset(layout_family(frame.style) + "/gameparts", "playm_spd2_window_dp")
            if (row["width"], row["height"]) != (88, 376):
                raise ValueError("Source DP Floating window dimensions changed")
            # The public texture declaration keeps the complete original PNG;
            # only the ordinary scene clip bounds select the upper source area.
            upper = node(prefix + ".classic.upper.art", "sprite", {"height": 376 / 300}, original)
            children.append(node(prefix + ".classic.upper", "clip",
                                 {**rect(box(596, 30, 88, 300)), "clip-mode": "bounds"}, children=[upper]))
        slider = image(mode + ".slider", "slider_bar", (*geo["slider"], 45, 10))
        identifier = slider["id"] + ".position"
        frame.scene["tracks"].append({"id": identifier, "type": "tween", "target": slider["id"], "property": "y",
            "clock": "scroll.hi-speed", "loop": False, "easing": "linear", "keyframes": [
                {"id": identifier + ".min", "time": .5, "value": geo["slider"][1] / 720},
                {"id": identifier + ".max", "time": 10, "value": geo["slider_top"] / 720}]})
        children.extend([slider, text(mode + ".value", "", geo["value"][0] + 10, geo["value"][1], 72,
                                      source="scroll.hi-speed", fmt="fixed-2")])
        if mode == "classic":
            # Explicit OMS mode label, outside native green-number/range cells.
            children.append(label(mode + ".mode", "CLASSIC", geo["window"][0],
                                  geo["window"][1] + 378 if frame.dp else geo["window"][1] - 58))
        modes.append(gate(mode + ".mode-gate", "scroll.mode", children, choice=mode))
    gx, gy = geo["green"]
    green = text("green", "", gx, gy, 120, colour="#00ff00ff", source="scroll.green-number")
    # Native font2's %4d minimum field, without inventing a space bitmap glyph.
    frame.steps(green["id"], "x", "scroll.green-number",
                [(0, (gx + 36) / 1280), (10, (gx + 24) / 1280), (100, (gx + 12) / 1280), (1000, gx / 1280)])

    readouts = [green]
    # 22's audited Start renderer draws only the current %4d value. The later
    # font5 max-min branch is not a source22 capability.
    if layout_family(frame.style) not in ("s21", "s22"):
        # 26 prints "%d-%d" in font5 at the notes_time-relative anchor.
        # Providers supply the extrema; never reconstruct them from current.
        rx, ry = ((gx + 54, gy + 4) if frame.dp else (gx + 60, gy - 16) if frame.p2 else (gx, gy - 16))
        ranges = node(prefix + ".green.range", properties={"x": rx / 1280, "y": ry / 720})
        large = text("green.max", "", 0, 0, 80, colour="#00ff00ff", source="scroll.green-number-max", size="small")
        small = text("green.min", "", 0, 0, 80, colour="#00ff00ff", source="scroll.green-number-min", size="small")
        hyphen = text("green.hyphen", "-", 0, 0, 8, colour="#00ff00ff", size="small")
        widths = [(0, 8), (10, 16), (100, 24), (1000, 32), (10000, 40), (100000, 48),
                  (1000000, 56), (10000000, 80)]
        if frame.p2:
            tail = node(prefix + ".green.tail", children=[small, hyphen])
            hyphen["properties"]["x"] = -8 / 1280
            head = node(prefix + ".green.head", children=[large])
            tail["children"].append(head)
            ranges["children"].append(tail)
            frame.steps(tail["id"], "x", "scroll.green-number-min", [(value, -width / 1280) for value, width in widths])
            frame.steps(head["id"], "x", "scroll.green-number-max", [(value, -(width + 8) / 1280) for value, width in widths])
        else:
            tail = node(prefix + ".green.tail", children=[hyphen, small])
            small["properties"]["x"] = 8 / 1280
            ranges["children"].extend([large, tail])
            frame.steps(tail["id"], "x", "scroll.green-number-max", [(value, width / 1280) for value, width in widths])
        frame.bind(ranges["id"], "visible", "scroll.green-number-range-available")
        readouts.append(ranges)
    body = gate("adjusting", "scroll.adjusting", [*modes, *readouts])
    body = gate("available", "scroll.available", [body])
    body = gate("version", "scroll.hi-speed-available", [body])
    frame.info.append(gate("enabled", "option.start-panel", [body]))

    clip = copy.deepcopy(ready[frame.layout])
    for resource in clip["resources"]:
        frame.resource(resource)
    for declaration in clip["animations"]:
        frame.animations[declaration["id"]] = declaration
    art = clip["node"]
    art["id"] = prefix + ".ready.art"
    art["clock"] = "preparation.ready-elapsed"
    frame.info.append(gate("ready.enabled", "option.ready-animation", [art]))
    frame.scene["preparationDuration"] = max(1500, frame.scene.get("preparationDuration", 0))
    frame.evidence.append({"reading": {"source": layout_family(frame.style) + "/gameparts spd/spd2 p1/p2/dp, stable frame16",
        "geometryBasis": "The six source movies in each of0/s23/s24 have identical frame16 texture bounds and named hooks; numeric rendering uses the separately identified source font and OMS author metrics.",
        "nativeReferenceBoundary": "The caller addresses below refer only to the audited26 executable; matching earlier source geometry does not prove identical historical software behaviour.",
        "normal": "Source window artwork with OMS-authored ten-row pages for native OMS preset indices1..20; not the supplied24 executable's eleven fixed multipliers. No change to OMS maths.",
        "classic": "Explicit CLASSIC label and actual multiplier using the source0.5..10 window.",
        "green": "Actual current value at notes_time with native %4d spacing; native max-min endpoints use font5, exact source offset and right alignment for2P. No inferred endpoint maths.",
        "greenExtremeWidth": "Eight-to-ten-digit endpoints reserve ten original cells to avoid overlap within the public numeric-track time bound; ordinary one-to-seven-digit values retain native spacing.",
        "floatingSlider": "26 caller3508d3: (speed-.5)*174/9.5; retained source anchors and174px travel. Actual OMS multiplier drives it.",
        "floatingCurrent": "26 caller350957 draws font2 at spd2_0+(10,0); DP original limit/CURRENT labels are baked and not redrawn.",
        "classicWindow": "Explicit OMS CLASSIC label; DP keeps Floating upper300 source pixels over the original generic lower HI-SPEED CHANGE panel.",
        "ready": clip["source"], "clock": "preparation.ready-elapsed"}})
    if layout_family(frame.style) == "s22":
        frame.evidence[-1]["reading"].update({
            "geometryBasis": "The original22 six speed movies independently retain the frame16 bounds and named hooks used here. Own22 PNGs and complete90-frame READY programs are used; matching geometry does not replace source pixels.",
            "nativeReferenceBoundary": "The supplied22 Start renderer independently qualifies current green, Floating slider travel and current multiplier placement. Numeric glyphs and OMS reading state remain explicit host adapters.",
            "normal": "OMS-authored ten-row pages for preset indices1..20 in the original22 window. The22 caller instead draws eleven fixed multipliers with25px row spacing; those source preset semantics are not reproduced.",
            "green": "Actual current green at the original notes_time anchor, retaining minimum four-cell spacing. No later max-min range is added to22.",
            "greenExtremeWidth": "Not applicable: the22 Start renderer has no qualified endpoint-range branch.",
            "floatingSlider": "The22 caller independently uses (speed-.5)*174/9.5. The original source anchors and174px travel are driven by the actual OMS multiplier.",
            "floatingCurrent": "The22 caller uses font2 at spd2_0+(10,0); source DP labels stay baked. The source font in this author remains the separately qualified shared OMS numeric adapter.",
            "sourceCapabilities": {"currentGreen": True, "greenRange": False,
                "dedicatedNormalSpeedPreviewArt": False, "firstRowCueArt": False}})
    elif layout_family(frame.style) == "s21":
        frame.evidence[-1]["reading"].update({
            "geometryBasis": "The six original21 speed movies independently preserve the frame16 bounds, vertices and affine hooks used here. Own21 PNGs/UVs and complete90-frame READY programs are retained.",
            "nativeReferenceBoundary": "Original21 font2 is independently registered from its supplied DF-HeiseiGothic-W7. The numeric cell metrics, preset pages, state providers and slider/number placement in this panel remain explicit OMS author adapters; complete21 Start caller parity is not asserted.",
            "normal": "OMS-authored ten-row pages for preset indices1..20 inside the original21 window; no historical source multiplier table is invented.",
            "green": "Actual current green at the original notes_time hook with the existing OMS four-cell numeric adapter. No later endpoint range is added to21.",
            "greenExtremeWidth": "Not applicable: no qualified21 endpoint-range branch is supplied.",
            "floatingSlider": "Original21 frame16 slider endpoints with actual OMS multiplier. Existing174px travel is an OMS author adaptation, not an assertion of an unverified21 caller formula.",
            "floatingCurrent": "The original21 spd2_0 hook is retained with the established OMS +(10,0) adapter; source DP labels remain baked. Native21 full-panel offset/caching semantics are not asserted.",
            "sourceCapabilities": {"currentGreen": True, "greenRange": False,
                "dedicatedNormalSpeedPreviewArt": False, "firstRowCueArt": False}})


def add_preview_background(frame, children):
    """Add original Normal-only preview art behind the native preview notes.

    The caller projects its existing native two-note preview into the declared
    Normal preview rectangle. This author does not create another note clock.
    """
    prefix = frame.prefix + ".reading.preview"
    x = {"p1": 287, "p2": 925, "dp": 669}[frame.layout]
    art = []
    for name, bounds in (("waku", (x, 0, 68, 520)), ("lane", (x + 15, 0, 38, 482)),
                         ("red_line", (x + 15, 477, 38, 5))):
        resource, row = frame.asset(layout_family(frame.style) + "/gameparts", "playm_spd_pv_" + name)
        if (row["width"], row["height"]) != bounds[2:]:
            raise ValueError("Qualified source speed preview geometry changed")
        sprite = node(prefix + "." + name.replace('_', '-'), "sprite", rect(box(*bounds)), resource)
        if name == "red_line":
            frame.scene["tracks"].append({"id": sprite["id"] + ".lift", "type": "tween", "target": sprite["id"],
                "property": "y", "clock": "lift.offset", "loop": False, "easing": "linear", "keyframes": [
                    {"id": sprite["id"] + ".lift.normal", "time": 0, "value": 477 / 720},
                    {"id": sprite["id"] + ".lift.full", "time": 1, "value": (477 - 484) / 720}]})
        art.append(sprite)

    # Native3504a7 removes the prototype and redraws two original bars: a
    # 50-percent base at455-Lift and a shrinking foreground around its30px
    # pivot. The texture is27px tall, so using a bottom27px origin would move
    # the original art incorrectly. Public phase is the existing OMS preview
    # driver's live phase, intentionally not the original60Hz/BPM counter.
    resource, row = frame.asset(layout_family(frame.style) + "/gameparts", "playm_spd_pv_bpm_gra_bar")
    if (row["width"], row["height"]) != (38, 27):
        raise ValueError("Qualified source preview BPM bar dimensions changed")
    bar_back = node(prefix + ".bpm.back", "sprite",
                    {**rect(box(x + 15, 455, 38, 27)), "opacity": .5}, resource)
    bar_front = node(prefix + ".bpm.front", "sprite",
                     {**rect(box(x + 15, 455, 38, 27)), "opacity": .75}, resource)
    for prop, values in (("y", ((0, 455 / 720), (1, 485 / 720))),
                         ("height", ((0, 27 / 720), (1, 0)))):
        identifier = bar_front["id"] + "." + prop
        frame.scene["tracks"].append({"id": identifier, "type": "tween", "target": bar_front["id"],
            "property": prop, "clock": "scroll.preview-phase", "loop": False, "easing": "linear",
            "keyframes": [{"id": identifier + f".n{i}", "time": time, "value": value}
                          for i, (time, value) in enumerate(values)]})
    frame.steps(bar_front["id"], "opacity", "scroll.preview-phase",
                [(i / 75, (75 - i) / 100) for i in range(76)])
    bar = node(prefix + ".bpm", children=[bar_back, bar_front])
    frame.scene["tracks"].append({"id": bar["id"] + ".lift", "type": "tween", "target": bar["id"],
        "property": "y", "clock": "lift.offset", "loop": False, "easing": "linear", "keyframes": [
            {"id": bar["id"] + ".lift.normal", "time": 0, "value": 0},
            {"id": bar["id"] + ".lift.full", "time": 1, "value": -484 / 720}]})
    frame.bind(bar["id"], "visible", "scroll.preview-available")
    art.append(bar)
    root = node(prefix + ".normal", children=art)
    frame.variant(root["id"], "visible", "scroll.mode", False, {"normal": True})
    for name, source in (("adjusting", "scroll.adjusting"), ("available", "scroll.available"),
                         ("version", "scroll.hi-speed-available")):
        root = node(prefix + "." + name, children=[root])
        frame.bind(root["id"], "visible", source)
    children.append(root)
    frame.evidence.append({"normalSpeedPreview": {"source": "0/gameparts speed_preview_1p/2p/dp, steady frame16",
        "nativeSourceCallerRVA": "0x34fef0, normal branch3503bc..350794",
        "sourceWindow": [x, 0, 68, 520], "sourceNoteArea": [x + 17, 0, 34, 477],
        "adaptation": "Original steady art toggles with actual Normal Start state; original16-frame window motion is not reproduced. Native OMS two-note preview remains the single clock/pattern, not the original maximum16-note/line pattern.",
        "bpmBar": "Original38x27 texture at y455-Lift, 50-percent base; foreground pivots at source30px: top455+30*phase, height27*(1-phase), opacity(75-floor(75*phase))/100. Actual scroll.preview-phase/available comes from the same native OMS preview driver as its two notes, with its pause/activation clock. This intentionally adapts the original private60Hz counter and floor(3600/BPM) period rather than pretending to reproduce that source rhythm.",
        "limits": "Original16-frame window opening and sixteen-note/every-fourth-note line pattern are replaced by the existing native OMS preview experience; no static prototype impersonates live values. Red line follows actual Lift, matching source35045a."}})


def add_arrival(frame):
    """Keep the original cue above the active Sudden edge, clipped at source482.

    A lane-surface owner spans the source484px lane. Sudden units are the live
    OMS0..1000 scale, not source pixels; configured units remain populated when
    the cover is inactive, so the two visibility branches must stay separate.
    """
    for colour, suffix, width in (("white", "w", 34), ("black", "b", 26), ("scratch", "s", 60)):
        resource, row = frame.asset(layout_family(frame.style) + "/gameparts", "start_notes_" + suffix)
        if (row["width"], row["height"]) != (width, 180):
            raise ValueError("Unqualified 26 first-row source dimensions")
        prefix = frame.prefix + ".arrival." + colour
        branches = []
        for active in (False, True):
            name = "sudden" if active else "uncovered"
            sprite = node(prefix + "." + name + ".art", "sprite", {"height": 180 / 482}, resource)
            if active:
                identifier = sprite["id"] + ".position"
                frame.scene["tracks"].append({"id": identifier, "type": "tween", "target": sprite["id"],
                    "property": "y", "clock": "cover.sudden-units", "loop": False, "easing": "linear", "keyframes": [
                        {"id": identifier + ".none", "time": 0, "value": 0},
                        {"id": identifier + ".full", "time": 1000, "value": 484 / 482}]})
            branch = node(prefix + "." + name, children=[sprite])
            frame.variant(branch["id"], "visible", "cover.sudden-active", False,
                          {"true" if active else "false": True})
            branches.append(branch)
        root = node(prefix + ".source-clip", "clip", {"height": 482 / 484, "clip-mode": "bounds", "opacity": 0},
                    children=branches)
        frame.bind(root["id"], "opacity", "arrival.cue-opacity")
        for name, source in (("visible", "arrival.visible"), ("first", "arrival.first-row"),
                             ("available", "arrival.available"), ("enabled", "option.arrival-cue")):
            root = node(prefix + "." + name, children=[root])
            frame.bind(root["id"], "visible", source)
        root["slot"] = "playfield.lane-surface"
        template = prefix + ".template"
        frame.scene["templates"].append({"id": template, "root": root})
        frame.scene["instances"].append({"id": template + ".instance", "template": template,
            "material": {"slot": "playfield.lane-surface", "resource": f"custom/markers/sb/{colour}"}})
    frame.evidence.append({"arrival": {"source": "26 start_notes_w/b/s, complete34/26/60x180 PNGs; native3477b0 draws at active Sudden top and clips the bottom to482.",
        "geometry": "Source484px lane owner, fixed180px cue, source482px bottom clip. Actual OMS active Sudden0..1000 units map to the source lane; inactive configured units do not move the cue. Lift does not move this source top-edge cue.",
        "lifecycle": "Only the actual post-mod global first row; arrival.visible and native global first-note scroll-entry200ms fade drive visibility. Preparation completion does not prematurely remove the cue. This is the selected OMS cue experience: original348ee0 uses an abs(sin(2*t*pi/180)) pulse multiplied by352610's60-tick delay/10-tick fade-in and source difficulty/mode eligibility; those original course-level gates and pulse clock are not impersonated."}})


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for field in ("library", "output", "bemani-root", "image-tools"):
        parser.add_argument("--" + field, type=Path, required=True)
    args = parser.parse_args()
    value = export_ready(args.library, args.output, args.bemani_root, args.image_tools)
    print(json.dumps({"output": str(args.output), "clips": len(value["clips"]), "files": len(value["files"])}))
