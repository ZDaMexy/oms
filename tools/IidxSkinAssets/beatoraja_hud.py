"""Translate author HUD text to native beatoraja 0.8.9 text objects.

This is a text-only pass over one already-selected scene root/template. Callers
own material placement, scene animation and the shared native.fallback font.
All remaining bindings are either mapped to real host properties or reported;
an unknown ancestor visibility/transform never becomes an unconditional HUD.

Bitmap text uses native SkinTextBitmap, not stretched numeric cells. Each author
glyph rectangle is copied unchanged into a BMFont atlas with its own advance.
Transparent right/bottom padding preserves OMS's max(advance, ink) measurement
and full line height without changing any visible source pixels.
The host's bitmap renderer uses bilinear filtering. Author numeric step tracks
for font size, opacity and x become mutually exclusive native draw branches;
unmapped continuous tracks remain explicit gaps. Qualified preparation Out
translations retain an original-static branch when the live clock is OFF.
A caller-selected unavailable preparation preview also restores source properties.
"""

from __future__ import annotations

import math
from pathlib import Path
import re
from typing import Any, Iterable

from PIL import Image

from beatoraja_author import Contribution, source_path, walk_nodes
from beatoraja_entrance import apply_preparation_translation, preparation_opacity_condition
from beatoraja_native import (
    AssetSink, Lua, all_of, colour, lua, native_colour, native_rect,
    node_rect, scene_properties, native_song_expression, native_offset_expression,
)
from beatoraja_preparation import preparation_expression


# Official tag 0.8.9: SkinProperty, IntegerPropertyFactory, SkinLuaAccessor.
# BMS POOR (missed note) is judge index 4; empty POOR is index 5. The host's
# NUMBER_MISS name means the latter and must not be assigned to OMS Miss.
_NUMBERS = {
    "score.value": 101, "combo.maximum": 105,
    "timing.bpm": 160, "timing.bpm-min": 91, "timing.bpm-max": 90,
    "judgement.count.perfect": 110, "judgement.count.great": 111,
    "judgement.count.good": 112, "judgement.count.meh": 113,
    "judgement.count.miss": 114, "judgement.count.ok": 420,
    "combo.breaks": 425, "judgement.count.fast": 423, "judgement.count.slow": 424,
    "scroll.green-number": 313, "cover.sudden-units": 14,
    "cover.hidden-units": 315,
}
_EXPRESSIONS = {
    "score.personal-best": ("main_state.exscore_best()", "number"),
    "score.maximum": ("(main_state.number(106) * 2)", "number"),
    "scroll.hi-speed": ("(main_state.number(10) / 100)", "number"),
    "gauge.value": ("(main_state.gauge() / 100)", "number"),
    "song.title": ("main_state.text(10)", "string"),
    "song.artist": ("main_state.text(14)", "string"),
    "song.level": (native_song_expression("song.level"), "string"),
    "song.difficulty": (native_song_expression("song.difficulty"), "string"),
    "scroll.adjusting": ("main_state.option(270)", "boolean"),
    "cover.sudden-active": ("main_state.option(271)", "boolean"),
    "cover.hidden-active": ("main_state.option(273)", "boolean"),
    "scroll.hi-speed-available": (
        "(main_state.number(10) ~= -2147483648 and main_state.number(10) ~= 2147483647)", "boolean"),
    "scroll.available": (
        "(main_state.number(10) ~= -2147483648 and main_state.number(10) ~= 2147483647)", "boolean"),
    "gauge.clear-state": ('(main_state.option(1240) and "clear" or "uncleared")', "string"),
    "gauge.type": (
        '(({[0]="assist-easy",[1]="easy",[2]="normal",[3]="hard",'
        '[4]="ex-hard",[5]="hazard",[6]="hard",[7]="hard",[8]="ex-hard"})[main_state.gauge_type()] or "")', "string"),
    **{source: native_offset_expression(source) for source in (
        "offset.value", "offset.available", "offset.automatic", "offset.automatic-available")},
    **{source: preparation_expression(source) for source in (
        "preparation.available", "preparation.active", "preparation.ready", "preparation.elapsed",
        "preparation.remaining", "preparation.ready-elapsed", "presentation.elapsed")},
}


class _Sources:
    def __init__(self, contributions: Iterable[Contribution], context: dict[str, Any]):
        self.context = context
        self.numeric: dict[str, dict[str, Any]] = {}
        for contribution in contributions:
            self.numeric.update({item["id"]: item for item in contribution.scene.get("numericSources", [])})

    def expression(self, source: Any, visited: tuple[str, ...] = ()) -> tuple[str, str] | None:
        if isinstance(source, (int, float)) and not isinstance(source, bool):
            return lua(source), "number"
        if source in self.context:
            value = self.context[source]
            kind = "boolean" if isinstance(value, bool) else "number" if isinstance(value, (int, float)) else "string"
            return lua(value), kind
        if source in _NUMBERS:
            return f"main_state.number({_NUMBERS[source]})", "number"
        if source in _EXPRESSIONS:
            return _EXPRESSIONS[source]
        if not isinstance(source, str) or not source.startswith("value."):
            return None
        key = source[6:]
        if key in visited:
            raise ValueError("Cyclic author numeric source")
        definition = self.numeric.get(key)
        if definition is None or definition["operation"] not in ("add", "subtract", "multiply"):
            return None
        left = self.expression(definition["left"], (*visited, key))
        right = self.expression(definition["right"], (*visited, key))
        if left is None or right is None or left[1] != "number" or right[1] != "number":
            return None
        operator = {"add": "+", "subtract": "-", "multiply": "*"}[definition["operation"]]
        return f"({left[0]} {operator} {right[0]})", "number"


def _visible(operation: dict[str, Any], sources: _Sources) -> str | None:
    expression = sources.expression(operation["source"])
    if expression is None:
        return None
    value, kind = expression
    if operation["kind"] == "bindings":
        return value if kind == "boolean" else None
    if not isinstance(operation.get("default"), bool) or any(
            not isinstance(case.get("value"), bool) for case in operation["cases"]):
        return None
    key = value if kind == "string" else f"tostring({value})"
    # An explicit branch is essential: Lua's `condition and false or default`
    # cannot represent a false case when the default is true.
    branches = " ".join(f"if v == {lua(case['key'])} then return {lua(case['value'])} end"
                        for case in operation["cases"])
    return f"(function() local v = {key}; {branches} return {lua(operation['default'])} end)()"


def _formatted(expression: tuple[str, str], formatting: str) -> str | None:
    value, kind = expression
    if kind == "string":
        if formatting == "uppercase":
            return f"string.upper({value})"
        return value if formatting == "" else None
    if kind != "number":
        return None
    if formatting in ("integer", "percent"):
        if formatting == "percent":
            return f'string.format("%.0f%%", ({value}) * 100)'
        return f'string.format("%.0f", {value})'
    if formatting in ("fixed-2", "fixed-2-pad2", "signed-fixed-2"):
        specifier = {"fixed-2": "%.2f", "fixed-2-pad2": "%05.2f", "signed-fixed-2": "%+.2f"}[formatting]
        return f"string.format({lua(specifier)}, {value})"
    if formatting == "":
        return f'(string.format("%.3f", {value}):gsub("0+$", ""):gsub("%.$", ""))'
    return None


def _glyph_images(font: dict[str, Any], resources: dict[str, Any], sink: AssetSink):
    line_height = font["lineHeight"]
    if line_height != int(line_height) or line_height <= 0:
        raise ValueError("BMFont export requires a positive integral author line height")
    line_height = int(line_height)
    glyphs = []
    for glyph in font["glyphs"]:
        character, advance = glyph["character"], glyph["advance"]
        # Original coordinate transforms can leave an integral advance a few
        # ULPs below/above its pixel value. BMFont itself requires an integer.
        integer_advance = round(advance)
        if len(character) != 1 or abs(advance - integer_advance) > 1e-9 or advance < 0:
            raise ValueError("BMFont export requires one character and an integral nonnegative advance")
        advance = integer_advance
        path = resources[glyph["resource"]]["path"]
        sink.add_source(path)  # Retain exact author provenance through the common sink.
        with Image.open(source_path(sink.author_root, path)) as source:
            image = source.convert("RGBA")
        x, y, w, h = glyph.get("rect", [0, 0, *image.size])
        if min(x, y) < 0 or min(w, h) <= 0 or x + w > image.width or y + h > image.height or h > line_height:
            raise ValueError("Author glyph rectangle escaped its image or line height")
        # Keep visible pixels unchanged. Padding affects the final glyph's
        # measured extent, matching GameplaySkinPreparedBitmapFont.MeasureText.
        cell = Image.new("RGBA", (max(w, int(advance)), line_height))
        cell.paste(image.crop((x, y, x + w, y + h)), (0, 0))
        glyphs.append((ord(character), int(advance), cell))
    return line_height, glyphs


def _bitmap_font(font: dict[str, Any], resources: dict[str, Any], sink: AssetSink) -> dict[str, Any]:
    line_height, glyphs = _glyph_images(font, resources, sink)
    x = y = 1
    placements = []
    max_x = 1
    for codepoint, advance, glyph in glyphs:
        if x + glyph.width + 1 > 1024:
            x, y = 1, y + line_height + 2
        placements.append((codepoint, advance, glyph, x, y))
        x += glyph.width + 2
        max_x = max(max_x, x)
    atlas = Image.new("RGBA", (max_x, y + line_height + 1))
    rows = []
    for codepoint, advance, glyph, x, y in placements:
        atlas.paste(glyph, (x, y))
        rows.append(f"char id={codepoint} x={x} y={y} width={glyph.width} height={line_height} "
                    f"xoffset=0 yoffset=0 xadvance={advance} page=0 chnl=15")
    atlas_id = sink.add_raster(atlas)
    filename = Path(sink.source[atlas_id]["path"]).name
    descriptor = "\n".join([
        f'info face="OMS author bitmap" size={line_height} bold=0 italic=0 charset="" unicode=1 '
        'stretchH=100 smooth=0 aa=1 padding=0,0,0,0 spacing=0,0',
        f"common lineHeight={line_height} base={line_height} scaleW={atlas.width} scaleH={atlas.height} "
        "pages=1 packed=0 alphaChnl=0 redChnl=0 greenChnl=0 blueChnl=0",
        f'page id=0 file="{filename}"', f"chars count={len(rows)}", *rows, "kernings count=0", "",
    ])
    path = sink.add_font_descriptor(descriptor)
    return {"id": "hud.font." + Path(path).stem[:24], "path": path, "type": 0}


def _scaled_integer(font: dict[str, Any], resources: dict[str, Any], sink: AssetSink,
                    properties: dict[str, Any], rect: tuple[float, float, float, float],
                    scale: tuple[float, float], canvas_height: int, object_id: str, expression: str,
                    *, gaps: list[dict[str, Any]] | None = None):
    """Native SkinNumber for an authored anisotropic, uniform-advance integer.

    The base graph intentionally scales its digits horizontally. SkinTextBitmap
    has uniform font scaling; SkinNumber's individual cell destination supports
    this author intent. Negative native space retains overlapping source glyphs;
    alignment includes the last glyph's overhang, as OMS MeasureText does.
    Integer cell/advance rounding is reported when it changes that source font.
    The digits are never stretched to the whole node width.
    """
    line_height, glyphs = _glyph_images(font, resources, sink)
    digits = {chr(codepoint): (advance, image) for codepoint, advance, image in glyphs if 48 <= codepoint <= 57}
    if len(digits) != 10 or len({(a, im.size) for a, im in digits.values()}) != 1:
        return None
    advance, cell = digits["0"]
    atlas = Image.new("RGBA", (cell.width * 10, line_height))
    for i in range(10):
        atlas.paste(digits[str(i)][1], (i * cell.width, 0))
    src = sink.add_raster(atlas)
    font_scale = properties.get("font-size", 16) * canvas_height / 768 / line_height
    source_cell_width = cell.width * font_scale * scale[0]
    source_advance = advance * font_scale * scale[0]
    cell_width = round(source_cell_width)
    native_advance = round(source_advance)
    spacing = native_advance - cell_width
    cell_height = round(line_height * font_scale * scale[1])
    if min(cell_width, cell_height) <= 0:
        raise ValueError("Author integer has nonpositive display size")
    alignment = properties.get("alignment", "left")
    x, y, width, height = rect
    digits_count = 10  # Full nonnegative native int range, without truncation.
    # SkinNumber shifts missing leading cells by (cell + space), but draws
    # the final glyph at its full cell width. Its getLength() omits this final
    # overhang, so reserve the actual image span rather than K advances.
    reserved_width = (digits_count - 1) * native_advance + cell_width
    start = {"left": x, "centre": x + (width - reserved_width) / 2,
             "right": x + width - reserved_width}[alignment]
    value = {"id": object_id, "src": src, "x": 0, "y": 0, "w": atlas.width, "h": atlas.height,
             "divx": 10, "divy": 1, "digit": digits_count, "padding": 0, "space": spacing,
             "align": {"left": 1, "centre": 2, "right": 0}[alignment],
             "value": Lua("function() return " + expression + " end")}
    destination = native_rect((start, y + (height - cell_height) / 2, cell_width, cell_height), canvas_height)
    if (advance != cell.width and gaps is not None
            and (not math.isclose(cell_width, source_cell_width, abs_tol=1e-6)
                 or not math.isclose(native_advance, source_advance, abs_tol=1e-6))):
        gaps.append({"code": "native-numeric-advance-quantization", "informational": True, "node": object_id,
            "sourceCellWidth": source_cell_width, "sourceAdvance": source_advance,
            "nativeCellWidth": cell_width, "nativeAdvance": native_advance, "nativeSpace": spacing,
            "maxTenDigitSpanErrorPx": abs(cell_width-source_cell_width) + 9*abs(native_advance-source_advance),
            "detail": "Original glyph pixels and overhang are retained. Native integer cell width and advance use their nearest source-canvas pixels; spacing follows the host's resolution scale. Alignment reserves the final full glyph. The span bound excludes at most 0.5 source-pixel destination-origin rounding."})
    return value, destination


def export_hud(
    contribution: Contribution,
    template_root: dict[str, Any],
    rect_top_left_px: tuple[float, float, float, float],
    sink: AssetSink,
    *,
    canvas_height: int = 1080,
    id_prefix: str = "hud",
    options: dict[str, Any] | None = None,
    context: dict[str, Any] | None = None,
    native_font_id: str | None = "native.fallback",
    fallback_contributions: Iterable[Contribution] = (),
    skip_node_ids: Iterable[str] = (),
) -> dict[str, Any]:
    """Export only text; append native arrays and keep gaps/mappings as evidence.

    `rect_top_left_px` is the caller-selected material target. The root's own
    properties are applied once within it. Context must include the selected
    ruleset and explicit settled animation/preparation values when appropriate.
    Options become `option.<id>`; explicit context wins. Unsupported visibility,
    masks, dynamic geometry or text sources omit the affected text subtree.

    Fonts supplied by the author remain exact bitmap assets. `native_font_id`
    is a caller-declared regular font for OMS's implicit default-font strings;
    passing None reports those texts as gaps. Font descriptors use the shared
    sink's add_font_descriptor(text) -> relative path method.
    """
    output: dict[str, Any] = {key: [] for key in ("font", "image", "value", "text", "destination", "gaps", "mappings")}
    resolved_context = {**{"option." + k: v for k, v in (options or {}).items()}, **(context or {})}
    contributions = [*fallback_contributions, contribution]
    skipped = set(skip_node_ids)
    sources = _Sources(contributions, resolved_context)
    resources: dict[str, Any] = {}
    fonts: dict[str, Any] = {}
    for item in contributions:
        resources.update(item.resources)
        fonts.update({font["id"]: font for font in item.manifest.get("bitmapFonts", [])})
    native_fonts: dict[str, dict[str, Any]] = {}
    tracks_by_node: dict[str, list[dict[str, Any]]] = {}
    for track in contribution.scene.get("tracks", []):
        # OMS canSampleElapsedTrack restores the author's initial properties
        # when that preparation clock is unavailable. The caller explicitly
        # selects this settled path; it is not a fabricated elapsed timestamp.
        clock = track.get("clock")
        availability = ("preparation.ready" if clock == "preparation.ready-elapsed" else
                        "preparation.available" if clock in ("preparation.elapsed", "preparation.remaining") else None)
        if availability and resolved_context.get(availability) is False:
            continue
        tracks_by_node.setdefault(track["target"], []).append(track)
    last_operation: dict[tuple[str, str], str] = {}
    for kind in ("bindings", "variants"):
        for operation in contribution.scene.get(kind, []):
            last_operation[operation["target"], operation["property"]] = operation["id"]

    def gap(node: dict[str, Any], reason: str, **details: Any):
        item = {"node": node["id"], "module": contribution.module,
                "choice": contribution.choice, "reason": reason, **details}
        if item not in output["gaps"]:
            output["gaps"].append(item)

    def visit(node: dict[str, Any], parent_rect: tuple[float, float, float, float],
              parent_colour: tuple[float, float, float, float], conditions: list[str],
              parent_scale: tuple[float, float], track_override: dict[str, Any] | None = None,
              instance_suffix: str = "", preparation_translation=None):
        if node["id"] in skipped:
            return
        if not any(n["type"] == "text" for n in walk_nodes(node)):
            return
        properties, operations = scene_properties(node, contribution.scene, resolved_context)
        operations = [o for o in operations if last_operation[o["target"], o["property"]] == o["id"]]
        if track_override is not None:
            properties.update(track_override)
            operations = [o for o in operations if o["property"] not in track_override]
        visible = next((o for o in operations if o["property"] == "visible"), None)
        if visible is None and properties.get("visible", True) is False:
            return
        conditions = list(conditions)
        if visible is not None:
            condition = _visible(visible, sources)
            if condition is None:
                gap(node, "unmapped-visibility", source=visible["source"])
                return
            conditions.append(condition)
        local_tracks = tracks_by_node.get(node["id"], []) if track_override is None else []
        step_tracks, preparation_override = [], {}
        for track in local_tracks:
            if track.get("clock") == "preparation.elapsed" and track["property"] in ("x", "y", "opacity"):
                try:
                    if track["property"] == "opacity":
                        conditions.append(preparation_opacity_condition(track, properties.get("opacity", 1)))
                        properties["opacity"] = preparation_override["opacity"] = 1
                    else:
                        if preparation_translation is not None:
                            raise ValueError("A text route has more than one preparation translation")
                        translation = (track, parent_rect, properties.get(track["property"], 0))
                        apply_preparation_translation([], *translation)  # Qualify before creating font assets.
                        preparation_translation = translation
                except ValueError as error:
                    gap(node, "unmapped-text-track", track=track["id"], property=track["property"],
                        clock=track.get("clock"), detail=str(error))
                    return
            else:
                step_tracks.append(track)
        if step_tracks:
            # A property callback cannot change native destination geometry or
            # bitmap text size. Author step tracks become disjoint native draw
            # branches, retaining their exact thresholds and pixel sizes.
            branches: list[tuple[dict[str, Any], list[str]]] = [(preparation_override, conditions)]
            for track in step_tracks:
                clock = sources.expression(track.get("clock"))
                keys = track.get("keyframes", [])
                if (track["type"] != "tween" or track.get("easing") != "step" or track.get("loop", False)
                        or track["property"] not in ("font-size", "opacity", "x")
                        or clock is None or clock[1] != "number" or not keys
                        or any(keys[i]["time"] >= keys[i + 1]["time"] for i in range(len(keys) - 1))):
                    gap(node, "unmapped-text-track", track=track["id"], property=track["property"], clock=track.get("clock"))
                    return
                expanded = []
                for properties_so_far, conditions_so_far in branches:
                    for i, key in enumerate(keys):
                        bounds = ([] if i == 0 else [f"({clock[0]}) >= {lua(key['time'])}"])
                        if i + 1 < len(keys):
                            bounds.append(f"({clock[0]}) < {lua(keys[i + 1]['time'])}")
                        expanded.append(({**properties_so_far, track["property"]: key["value"]}, [*conditions_so_far, *bounds]))
                branches = expanded
            for i, (override, branch_conditions) in enumerate(branches):
                visit(node, parent_rect, parent_colour, branch_conditions, parent_scale,
                      override, instance_suffix + ".step." + str(i), preparation_translation)
            return
        geometry = {"x", "y", "width", "height", "scale-x", "scale-y", "rotation", "anchor", "origin",
                    "opacity", "colour", "font-size", "alignment", "bitmap-font", "reveal-x"}
        unresolved = [o for o in operations if o["property"] in geometry]
        if unresolved:
            gap(node, "unmapped-dynamic-style", operations=[{"source": o["source"], "property": o["property"]} for o in unresolved])
            return
        if node["type"] in ("mask", "clip") or properties.get("rotation", 0) or node.get("effects"):
            gap(node, "unsupported-text-transform-or-mask")
            return
        rect = node_rect(properties, parent_rect)
        rgba = colour(properties, parent_colour)
        if rgba[3] == 0:
            return
        scale = (parent_scale[0] * properties.get("scale-x", 1),
                 parent_scale[1] * properties.get("scale-y", 1))
        if node["type"] == "text":
            binding = next((o for o in operations if o["property"] == "text"), None)
            text: dict[str, Any] = {"id": id_prefix + "." + node["id"] + instance_suffix, "overflow": 0}
            mapping = None
            if binding is None:
                text["constantText"] = properties.get("text", "")
                if not text["constantText"]:
                    return
            elif binding["kind"] != "bindings":
                gap(node, "unmapped-text-variant", source=binding["source"])
                return
            else:
                expression = sources.expression(binding["source"])
                formatted = _formatted(expression, properties.get("format", "")) if expression else None
                if formatted is None:
                    gap(node, "unmapped-text-binding", source=binding["source"], format=properties.get("format", ""))
                    return
                text["value"] = Lua("function() return " + formatted + " end")
                mapping = {"node": node["id"], "source": binding["source"], "native": expression[0]}
                if binding["source"] == "offset.value":
                    output["gaps"].append({"code": "native-offset-display-adaptation", "informational": True,
                        "detail": "The original signed field displays actual native whole-millisecond visual timing "
                                  "offset 12; AUTO follows public image index 75, not maximum-combo number 75. "
                                  "The host keeps its own automatic-adjustment and replay behavior; an OMS "
                                  "per-time replay offset history is not reconstructed."})
                # SkinNumber suppresses these host sentinels. Keep that native
                # availability behavior when rendering numbers through a font.
                for native_id in sorted(set(re.findall(r"main_state\.number\((\d+)\)", expression[0]))):
                    conditions.append(f"main_state.number({native_id}) ~= -2147483648 and main_state.number({native_id}) ~= 2147483647")
            font_id = properties.get("bitmap-font")
            if not math.isclose(scale[0], scale[1], abs_tol=1e-8):
                scaled = None
                if (font_id in fonts and mapping is not None and binding["source"] in _NUMBERS
                        and properties.get("format") == "integer"):
                    scaled = _scaled_integer(fonts[font_id], resources, sink, properties, rect,
                                             scale, canvas_height, text["id"], mapping["native"], gaps=output["gaps"])
                if scaled is None:
                    gap(node, "unsupported-anisotropic-text-scale", scale=list(scale))
                    return
                value, destination = scaled
                destination.update(time=0, **native_colour(rgba))
                entry = {"id": value["id"], "dst": [destination], "blend": 1}
                draw = all_of(conditions)
                if draw is not None:
                    entry["draw"] = draw
                output["value"].append(value)
                output["destination"].extend(apply_preparation_translation([entry], *preparation_translation)
                                             if preparation_translation is not None else [entry])
                if mapping not in output["mappings"]:
                    output["mappings"].append(mapping)
                return
            if font_id:
                if font_id not in fonts:
                    gap(node, "missing-bitmap-font", font=font_id)
                    return
                if font_id not in native_fonts:
                    native_fonts[font_id] = _bitmap_font(fonts[font_id], resources, sink)
                    if native_fonts[font_id] not in output["font"]:
                        output["font"].append(native_fonts[font_id])
                text["font"] = native_fonts[font_id]["id"]
            elif native_font_id:
                text["font"] = native_font_id
                if properties.get("text-overflow") == "ellipsis":
                    text["overflow"] = 2
                    gap(node, "native-truncate-overflow", detail="Native overflow=2 truncates; OMS adds ellipsis.")
            else:
                gap(node, "missing-native-font")
                return
            size = round(properties.get("font-size", 16) * canvas_height / 768 * scale[1])
            if size <= 0:
                raise ValueError("Author text has nonpositive display size")
            alignment = properties.get("alignment", "left")
            text.update(size=size, align={"left": 0, "centre": 1, "right": 2}[alignment])
            x, y, width, height = rect
            offset = {"left": 0, "centre": .5, "right": 1}[alignment]
            destination = native_rect((x + width * offset, y + (height - size) / 2, width, size), canvas_height)
            destination.update(time=0, **native_colour(rgba))
            entry = {"id": text["id"], "dst": [destination], "blend": 1}
            draw = all_of(conditions)
            if draw is not None:
                entry["draw"] = draw
            output["text"].append(text)
            output["destination"].extend(apply_preparation_translation([entry], *preparation_translation)
                                         if preparation_translation is not None else [entry])
            if mapping is not None:
                if mapping not in output["mappings"]:
                    output["mappings"].append(mapping)
                if binding["source"] in ("judgement.count.fast", "judgement.count.slow"):
                    gap(node, "native-timing-count-semantics", source=binding["source"],
                        detail="Native 423/424 count judges 1..5, excluding PGREAT; OMS uses successful timed hits. This port displays the native count.")
        children = node.get("children", [])
        for child in sorted(children, key=lambda n: -n.get("properties", {}).get("z", 0)):
            visit(child, rect, rgba, conditions, scale, instance_suffix=instance_suffix,
                  preparation_translation=preparation_translation)

    visit(template_root, rect_top_left_px, (1, 1, 1, 1), [], (1, 1))
    return output
