"""Original cover-edge reading displays on public beatoraja state.

Lightning keeps precise native offsets. Standard's fixed source clip cannot use
those offsets because SkinObject also translates the clip; a native TimerProperty
instead interpolates only Y from public cover fractions. Its Lift/Hidden position
has the explicitly reported thousandth-unit quantisation of properties 314/315.
"""

from __future__ import annotations

import math
from pathlib import Path
import re

from PIL import Image

from beatoraja_author import layout_for, material_bindings, material_templates, walk_nodes
from beatoraja_decoration import _sample_static
from beatoraja_hud import _bitmap_font
from beatoraja_native import Lua, all_of, colour, native_colour, native_rect, node_rect, scene_properties


_EDGE = {"cover.sudden-units": "sudden", "cover.hidden-units": "hidden", "lift.offset": "lift"}
_WHITE = {"sudden": ("cover.sudden-units", 14, 271), "hidden": ("cover.hidden-units", 315, 273), "lift": ("lift.value", 314, 272)}
_SAMPLES = {"scroll.adjusting": True, "scroll.available": True, "scroll.hi-speed-available": True,
            "cover.sudden-active": True, "cover.hidden-active": True, "lift.available": True,
            "lift.reversed": False, "lift.value": 100, "lift.offset": 0,
            "cover.sudden-units": 100, "cover.hidden-units": 100, "scroll.green-number": 100,
            "scroll.green-number-min": 100, "scroll.green-number-max": 100,
            "scroll.green-number-range-available": True}
_RANGE_HIGH = "main_state.number(main_state.option(271) and 1321 or 1323)"
_RANGE_LOW = "main_state.number(main_state.option(271) and 1325 or 1327)"


def _valid_number(expression):
    return f"({expression}) >= 0 and ({expression}) < 2147483647"


def _reading_targets(author, options, keymode, presentation):
    frame = author.contribution("frame", options)
    edge_nodes = {track["target"] for track in frame.scene.get("tracks", [])
                  if track.get("clock") in _EDGE and track["property"] == "y"}
    targets = []

    def locate(node, ancestors=()):
        if node["id"].endswith(".cover-readout.version"):
            targets.append((node, ancestors, None))
            return
        for child in node.get("children", []):
            locate(child, (*ancestors, node))

    for binding in material_bindings(frame, keymode, presentation):
        if binding["slot"] != "decoration" or binding["mode"] != "Provide":
            continue
        roots = material_templates(frame, binding["slot"], binding["resource"])
        if author.family == "standard" and binding["target"]["kind"] == "global":
            for template in roots:
                locate(template)
        elif author.family == "lightning" and binding["target"]["kind"] == "stage":
            stage = int(binding["target"]["group-logical"])
            targets.extend((root, (), stage) for root in roots
                           if any(node["id"] in edge_nodes for node in walk_nodes(root)))
    return frame, targets


def _ownership(targets):
    return {"handled_root_ids": list(dict.fromkeys(root["id"] for root, _, stage in targets if stage is not None)),
            "handled_node_ids": list(dict.fromkeys(root["id"] for root, _, stage in targets if stage is None))}


def reading_ownership(author, options, keymode, presentation):
    """Select exact reading ownership without constructing or copying assets.

    Independent HUD/decorations package stages can skip these source nodes
    before the reading stage has run. Standard owns only its reading subtree.
    """
    _, targets = _reading_targets(author, options, keymode, presentation)
    return _ownership(targets)


def _fixed_clip_motion(kind, base, clip, stage_height):
    """Public normalized reads avoid guessing Config resolution from window size.

    A 1024-tick/source-pixel clock keeps the TimerProperty's millisecond
    quantisation below 1/1024 source pixel. Integral microsecond timestamps also
    avoid Java's negative-timestamp division rounding a synthetic timer early.
    """
    if kind == "sudden":
        fraction = "(-main_state.float_number(4))"
    else:
        lift = "(main_state.option(272) and main_state.number(314) / 1000 or 0)"
        fraction = lift if kind == "lift" else f"({lift} + (1 - {lift}) * main_state.number(315) / 1000)"
    span = math.ceil(stage_height)
    ticks = 1024
    timer = Lua(f"function() local delta = {fraction} * {stage_height:.12g}; "
                f"return (math.floor(main_state.time() / 1000) - math.floor((delta + {span}) * {ticks} + 0.5)) * 1000 end")
    clip_fields = {"clip_" + key: value for key, value in clip.items()}
    return {"timer": timer, "loop": span * ticks * 2,
            "dst": [{**base, **clip_fields, "time": 0, "y": base["y"] - span},
                    {**base, **clip_fields, "time": span * ticks * 2, "y": base["y"] + span}]}


def _font_with_layout_space(font, resources, sink):
    """Add one transparent cell for source geometry gaps; retain every glyph.

    The source range reserves ten cells for 8/9 digit endpoints. Whitespace
    preserves those authored blank positions without hundreds of draw branches.
    The space has a full transparent width so the native final-glyph measure also
    retains a trailing reserved cell.
    """
    native = _bitmap_font(font, resources, sink)
    advances = {g["advance"] for g in font["glyphs"] if g["character"] in "0123456789-"}
    if len(advances) != 1 or any(g["character"] == " " for g in font["glyphs"]):
        raise ValueError("Reading padding requires the original uniform fixed-cell font")
    advance = advances.pop()
    if advance != int(advance) or advance <= 0:
        raise ValueError("Reading font has a nonintegral cell width")
    descriptor = (sink.output / native["path"]).read_text(encoding="utf-8")
    filename = re.search(r'^page id=0 file="([^"]+)"$', descriptor, re.MULTILINE)[1]
    with Image.open(sink.assets / filename) as original:
        raster = Image.new("RGBA", (max(original.width, int(advance) + 2), original.height + int(font["lineHeight"]) + 2))
        raster.paste(original, (0, 0))
        space_y = original.height + 1
    src = sink.add_raster(raster)
    descriptor = descriptor.replace(f'file="{filename}"', f'file="{Path(sink.source[src]["path"]).name}"')
    descriptor = re.sub(r"scaleW=\d+ scaleH=\d+", f"scaleW={raster.width} scaleH={raster.height}", descriptor)
    descriptor = re.sub(r"chars count=(\d+)", lambda match: f"chars count={int(match[1]) + 1}", descriptor)
    space = (f"char id=32 x=1 y={space_y} width={int(advance)} height={int(font['lineHeight'])} "
             f"xoffset=0 yoffset=0 xadvance={int(advance)} page=0 chnl=15\n")
    descriptor = descriptor.replace("kernings count=0", space + "kernings count=0")
    path = sink.add_font_descriptor(descriptor)
    return {"id": "reading.font." + Path(path).stem[:24], "path": path, "type": 0}


def export_reading(author, options, keymode, presentation, sink, canvas):
    """Return native arrays plus exact template/subtree ownership.

    Call after native covers so these original labels remain on their edges.
    The caller must skip handled_node_ids inside HUD traversal; Standard's other
    information fields retain their existing ownership. No scroll.mode is faked.
    """
    output = {"font": [], "image": [], "value": [], "text": [], "destination": [], "gaps": [],
              "handled_root_ids": [], "handled_node_ids": []}
    if (keymode, presentation) not in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
        raise ValueError("Reading export requires a matching SP or DP presentation")
    if canvas[:2] != (0, 0) or min(canvas[2:]) <= 0:
        raise ValueError("Reading export requires the full positive zero-origin canvas")
    width, height = canvas[2:]
    frame, targets = _reading_targets(author, options, keymode, presentation)
    output.update(_ownership(targets))
    layout = layout_for(frame, keymode, presentation)
    resources = frame.resources
    fonts = {font["id"]: font for font in frame.manifest.get("bitmapFonts", [])}
    context = {**_SAMPLES, "layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    tracks = {}
    for track in frame.scene.get("tracks", []):
        tracks.setdefault(track["target"], []).append(track)
    edge_ids = {t["id"] for ts in tracks.values() for t in ts if t.get("clock") in _EDGE and t["property"] == "y"}
    native_fonts = {}
    reading_roles = set()

    def resolved(node, values):
        properties, dynamic = scene_properties(node, frame.scene, values)
        if dynamic:
            raise ValueError("Unmapped reading sources: " + ", ".join(sorted({v["source"] for v in dynamic})))
        for track in tracks.get(node["id"], []):
            if track["id"] in edge_ids:
                keys = track["keyframes"]
                if (track.get("loop") or track.get("trigger") or track.get("easing") != "linear" or len(keys) != 2
                        or keys[0]["time"] != 0 or keys[1]["time"] != (1 if track["clock"] == "lift.offset" else 1000)):
                    raise ValueError("Unqualified reading edge track")
                properties[track["property"]] = keys[0]["value"]
            else:
                known, value = _sample_static(track, values)
                if not known:
                    raise ValueError("Unmapped reading track: " + track["id"])
                properties[track["property"]] = value
        if (node.get("effects") or properties.get("rotation", 0) % 360
                or properties.get("scale-x", 1) != 1 or properties.get("scale-y", 1) != 1
                or node.get("blend", "inherit") not in ("inherit", "alpha")):
            raise ValueError("Reading transform needs a qualified source bake")
        return properties

    def route_state(route, host, values):
        rect, rgba, clip = host, (1, 1, 1, 1), None
        for node in route:
            props = resolved(node, values)
            rgba = colour(props, rgba)
            if props.get("visible", True) is False or rgba[3] <= 0:
                return None
            rect = node_rect(props, rect)
            if node["type"] == "clip":
                if props.get("clip-mode") != "bounds" or clip is not None:
                    raise ValueError("Reading supports the original single rectangular clip")
                clip = rect
            elif node["type"] not in ("container", "text", "sprite"):
                raise ValueError("Unqualified reading node: " + node["type"])
        return rect, rgba, clip

    def font_for(font_id, spacing=False):
        key = (font_id, spacing)
        if key not in native_fonts:
            native_fonts[key] = (_font_with_layout_space(fonts[font_id], resources, sink) if spacing
                                 else _bitmap_font(fonts[font_id], resources, sink))
            output["font"].append(native_fonts[key])
        return native_fonts[key]["id"]

    def destination(identifier, rect, rgba, kind, stage_height, conditions, clip=None):
        base = {"time": 0, **native_rect(rect, height), **native_colour(rgba)}
        result = {"id": identifier, "blend": 1, "filter": 0, "draw": all_of(conditions)}
        if clip is not None:
            result.update(_fixed_clip_motion(kind, base, native_rect(clip, height), stage_height))
        else:
            result.update(offsets={"sudden": [4], "hidden": [3, 5], "lift": [3]}[kind], dst=[base])
        output["destination"].append(result)

    def glyph_layout(root, ancestors, host, values):
        glyphs = []
        def walk(node, route):
            route = (*route, node)
            state = route_state(route, host, values)
            if state is None:
                return
            rect, rgba, _ = state
            props = resolved(node, values)
            if node["type"] == "text":
                if props.get("alignment", "left") != "left" or props.get("format", "") not in ("", "integer"):
                    raise ValueError("Unqualified source reading text layout")
                text = props.get("text", "")
                if isinstance(text, (float, int)):
                    text = str(int(text))
                font = fonts[props["bitmap-font"]]
                scale = props["font-size"] * height / 768 / font["lineHeight"]
                x, y = rect[0], rect[1] + (rect[3] - font["lineHeight"] * scale) / 2
                lookup = {g["character"]: g for g in font["glyphs"]}
                for char in text:
                    advance = lookup[char]["advance"] * scale
                    glyphs.append({"char": char, "x": x, "y": y, "advance": advance,
                                   "font": font["id"], "size": font["lineHeight"] * scale, "rgba": rgba})
                    x += advance
            for child in node.get("children", []):
                walk(child, route)
        walk(root, ancestors)
        return sorted(glyphs, key=lambda g: g["x"])

    def standard_group(group, route, host, kind, stage_height, conditions, origin_shift):
        ids = {n["id"] for n in walk_nodes(group)}
        sources = {v["source"] for v in frame.scene.get("bindings", []) if v["target"] in ids and v["property"] == "text"}
        white_source, white_ref, _ = _WHITE[kind]
        left_green = False
        if sources == {white_source}:
            role, expression, alignment, anchor_count = "white", f"main_state.number({white_ref})", 0, 0
            display = lambda values: f"{int(values[white_source]):03d}"
            code = f'return string.format("%03d", {expression})'
        elif sources == {"scroll.green-number"}:
            role, expression, alignment, anchor_count = "green", "main_state.number(313)", 1, 3
            display = lambda values: f'{int(values["scroll.green-number"]):03d}'.ljust(10 if values["scroll.green-number"] >= 10000000 else 3)
            code = f'local n = {expression}; local s = string.format("%03d", n); if n >= 10000000 then s = s .. string.rep(" ", 10 - #s) end; return s'
            anchors = [t for t in tracks.get(group["id"], []) if t["property"] == "x"]
            if anchors:
                # The earlier source keeps a left anchor, changing it at the
                # fourth digit. Read both positions from the actual scene;
                # neither cabinet IDs nor later centred geometry identify it.
                if (len(anchors) != 1 or anchors[0].get("clock") != "scroll.green-number"
                        or anchors[0].get("type") != "tween" or anchors[0].get("easing") != "step"
                        or anchors[0].get("loop") or anchors[0].get("trigger")
                        or [k["time"] for k in anchors[0]["keyframes"]] != [0, 1000]):
                    raise ValueError("Unqualified current-green left-anchor track")
                left_green, alignment, anchor_count = True, 0, 0
                display = lambda values: f'{int(values["scroll.green-number"]):03d}'
                code = f'return string.format("%03d", {expression})'
        elif sources == {"scroll.green-number-min", "scroll.green-number-max"}:
            role, expression, alignment, anchor_count = "range", None, 1, 7
            def display(values):
                parts = [str(int(values[key])) for key in ("scroll.green-number-max", "scroll.green-number-min")]
                return "-".join(part.ljust(10 if len(part) >= 8 else len(part)) for part in parts)
            code = (f'local hi = tostring({_RANGE_HIGH}); local lo = tostring({_RANGE_LOW}); '
                    'if #hi >= 8 then hi = hi .. string.rep(" ", 10 - #hi) end; '
                    'if #lo >= 8 then lo = lo .. string.rep(" ", 10 - #lo) end; return hi .. "-" .. lo')
            conditions = [*conditions, "main_state.number(90) > 0 and main_state.number(91) > 0 and main_state.number(90) ~= main_state.number(91)",
                          _valid_number(_RANGE_HIGH), _valid_number(_RANGE_LOW)]
        else:
            raise ValueError("Unqualified Standard cover-reading group")
        reading_roles.add(role)
        sample = glyph_layout(group, route, host, context)
        if not sample:
            raise ValueError("Standard reading has no original glyph layout")
        first = sample[0]
        anchor = first["x"] + anchor_count * first["advance"] / 2
        upper_anchor = (glyph_layout(group, route, host, {**context, "scroll.green-number": 1000})[0]["x"]
                        if left_green else anchor)
        # Independently evaluate the retained source tracks at every digit-width
        # transition, including the author's 8/9-digit reserved ten-cell case.
        probes = [0, 9, 10, 99, 100, 999, 1000, 9999, 10000, 9999999, 10000000, 999999999, 1000000000]
        pairs = [(v, w) for v in probes for w in probes] if role == "range" else [(v, v) for v in probes]
        for high, low in pairs:
            values = {**context, white_source: high, "scroll.green-number": high,
                      "scroll.green-number-max": high, "scroll.green-number-min": low}
            actual = glyph_layout(group, route, host, values)
            string = display(values)
            current_anchor = upper_anchor if left_green and high >= 1000 else anchor
            start = current_anchor - len(string) * first["advance"] / 2 if alignment else current_anchor
            expected = [(char, start + i * first["advance"]) for i, char in enumerate(string) if char != " "]
            if (len(actual) != len(expected) or any(g["char"] != char or not math.isclose(g["x"], x, abs_tol=1e-4)
                    or not math.isclose(g["y"], first["y"], abs_tol=1e-4) or g["font"] != first["font"]
                    or not math.isclose(g["advance"], first["advance"], abs_tol=1e-4) or g["rgba"] != first["rgba"]
                    for g, (char, x) in zip(actual, expected))):
                raise ValueError("Combined reading text would change the original glyph positions")
        identifier = author.family + ".reading." + group["id"]
        state = route_state((*route, group), host, context)
        if state[2] is None:
            raise ValueError("Standard reading lost its original fixed clip")
        placements = [(identifier, anchor, [])]
        if left_green:
            placements = [(identifier + ".below1000", anchor, [expression + " < 1000"]),
                          (identifier + ".atleast1000", upper_anchor, [expression + " >= 1000"])]
        for text_id, text_anchor, digit_conditions in placements:
            output["text"].append({"id": text_id, "font": font_for(first["font"], spacing=role != "white" and not left_green),
                                   "size": round(first["size"]), "align": alignment, "overflow": 0,
                                   "value": Lua("function() " + code + " end")})
            destination(text_id, (text_anchor, first["y"] + origin_shift, 0, first["size"]), first["rgba"], kind, stage_height,
                        [*conditions, *([_valid_number(expression)] if expression else []), *digit_conditions], state[2])

    def lightning_row(root, route, host, kind, stage_height, conditions, stage):
        state = route_state((*route, root), host, context)
        if state is None:
            return
        rect, rgba, clip = state
        props = resolved(root, context)
        if root["type"] != "container" or props.get("layout") != "horizontal" or len(root.get("children", [])) != 1 or clip:
            raise ValueError("Lightning reading needs its one original horizontal leaf")
        leaf = root["children"][0]
        leaf_props = resolved(leaf, context)
        rgba = colour(leaf_props, rgba)
        identifier = f"{author.family}.reading.{stage}.{leaf['id']}"
        if leaf["type"] == "sprite":
            src = sink.add_source(resources[leaf_props["resource"]]["path"])
            sw, sh = sink.dimensions[src]
            h = leaf_props["height-px"] * height / 768
            w = h * sw / sh
            output["image"].append({"id": identifier, "src": src, "x": 0, "y": 0, "w": sw, "h": sh})
            box = (rect[0] + (rect[2] - w) / 2, rect[1] + (rect[3] - h) / 2, w, h)
        elif leaf["type"] == "text":
            binding = [b for b in frame.scene.get("bindings", []) if b["target"] == leaf["id"] and b["property"] == "text"]
            if len(binding) != 1 or binding[0]["source"] not in (_WHITE[kind][0], "scroll.green-number") or leaf_props.get("format") != "integer":
                raise ValueError("Unmapped Lightning reading value")
            ref = 313 if binding[0]["source"] == "scroll.green-number" else _WHITE[kind][1]
            expression = f"main_state.number({ref})"
            h = leaf_props["font-size"] * height / 768
            output["text"].append({"id": identifier, "font": font_for(leaf_props["bitmap-font"]), "size": round(h),
                                   "align": 1, "overflow": 0, "value": Lua(f'function() return string.format("%d", {expression}) end')})
            box = (rect[0] + rect[2] / 2, rect[1] + (rect[3] - h) / 2, 0, h)
            conditions = [*conditions, _valid_number(expression)]
        else:
            raise ValueError("Unqualified Lightning reading leaf")
        destination(identifier, box, rgba, kind, stage_height, conditions)

    rows = 0
    for root, ancestors, stage in targets:
        host = canvas
        if stage is not None:
            sx, sy, sw, sh = layout["stages"][stage]["rect"]
            host = (sx * width, sy * height, sw * width, sh * height)
        def visit(node, route):
            nonlocal rows
            state = route_state((*route, node), host, context)
            if state is None:
                return
            edges = [t for t in tracks.get(node["id"], []) if t["id"] in edge_ids]
            if not edges:
                for child in node.get("children", []):
                    visit(child, (*route, node))
                return
            if len(edges) != 1:
                raise ValueError("Reading carrier has competing edge tracks")
            track = edges[0]
            kind = _EDGE[track["clock"]]
            parent = route_state(route, host, context)[0] if route else host
            delta = (track["keyframes"][-1]["value"] - track["keyframes"][0]["value"]) * parent[3]
            stage_rect = layout["stages"][stage or 0]["rect"]
            stage_height = round(stage_rect[3] * height)
            # Standard intentionally retains the 26 software's fixed 484px
            # reading table across older cabinets. 21 has an inset lane top;
            # retain the table's glyph offsets while following the real edge.
            source_height = 484 * height / 720 if author.family == "standard" else stage_height
            if not math.isclose(delta, source_height * (1 if kind == "sudden" else -1), abs_tol=1e-3):
                raise ValueError("Source reading edge differs from the actual native lane height")
            actual_bottom = height - round(height - (stage_rect[1] + stage_rect[3]) * height)
            origin_shift = (actual_bottom - stage_height) if kind == "sudden" else actual_bottom - source_height
            if author.family == "standard" and (origin_shift or source_height != stage_height):
                adaptation = {"code": "native-reading-cabinet-edge-adaptation", "informational": True,
                              "sourceSoftwareLaneHeight": source_height, "nativeLaneHeight": stage_height,
                              "nativeLaneTop": actual_bottom - stage_height,
                              "detail": "The retained 26 software reading table has a fixed lane span. Original glyph offsets, horizontal anchors and clip are retained; movement follows this cabinet's actual native lane edges."}
                if adaptation not in output["gaps"]:
                    output["gaps"].append(adaptation)
            conditions = ["main_state.option(270)", f"main_state.option({_WHITE[kind][2]})", _valid_number("main_state.number(10)")]
            if author.family == "lightning" and kind == "lift":
                conditions.append("main_state.number(314) >= 1")
            for child in node.get("children", []):
                if author.family == "standard":
                    standard_group(child, (*route, node), host, kind, stage_height, conditions, origin_shift)
                else:
                    lightning_row(child, (*route, node), host, kind, stage_height, conditions, stage)
            rows += 1
        visit(root, ancestors)
    if rows:
        output["gaps"].append({"code": "native-reading-value-adaptation", "informational": True, "rows": rows,
            "detail": "14/314/315 are native Sudden/Lift/Hidden thousandths; 313 is actual currentDuration*3/5. Start OR Select (270) and actual enabled flags gate these source readouts. Native Hidden follows Lift and remaining lane height; no OMS Normal/Floating/Classic mode is fabricated."})
        if author.family == "standard":
            source_height = round(layout["stages"][0]["rect"][3] * height)
            output["gaps"].append({"code": "native-reading-fixed-clip-quantisation", "informational": True,
                "liftSourcePixelsExclusiveBound": source_height / 1000,
                "hiddenSourcePixelsExclusiveBound": source_height / 500,
                "timerSourcePixelsBound": 1 / 1024,
                "detail": "Fixed source clip is retained. Sudden uses exact effective float property 4; Lift/Hidden geometry uses public integer thousandths, not window size or unexposed Config resolution."
                    + (" Range uses native min/max BPM duration refs 1321/1325 (1323/1327 with Sudden disabled); these BPM-only endpoints do not include current SCROLL/CONSTANT effects as 313 does."
                       if "range" in reading_roles else " The selected source has current green only; no endpoint range is added.")})
    return output
