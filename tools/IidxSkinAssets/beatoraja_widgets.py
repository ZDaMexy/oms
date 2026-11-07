"""Compose same-family Judge and whole-bar Gauge recipes into native widgets.

All coordinates supplied by the caller are top-left canvas pixels. Output uses
beatoraja's bottom-left destinations. Rasters only concatenate or mirror original
pixels; no default-skin component, resampling, or inferred 50-cell gauge is used.
"""

from __future__ import annotations

import math

from PIL import Image

from beatoraja_author import source_path
from beatoraja_native import (Lua, anchor, colour, native_colour, native_rect,
                             node_rect, scene_properties)

# Index is the real GrooveGauge type. Course CLASS/EXCLASS/EXHARDCLASS are
# survival gauges (100 maximum, zero border), with their own host rules. Their
# last three entries select only a same-family appearance, never those rules.
GAUGE_PALETTES = ("assist-easy", "easy", "normal", "hard", "ex-hard", "hazard", "hard", "hard", "ex-hard")
IMAGE_FIELDS = ("id", "src", "x", "y", "w", "h", "divx", "divy", "cycle", "timer")


def _rect(rect):
    values = tuple(rect[k] for k in ("x", "y", "w", "h")) if isinstance(rect, dict) else tuple(rect)
    if len(values) != 4 or not all(math.isfinite(v) for v in values) or min(values[2:]) <= 0:
        raise ValueError("Expected a finite, positive stage rectangle in canvas pixels")
    return values


def _pixels(image, sink):
    with Image.open(source_path(sink.author_root, image["source"])) as source:
        return source.convert("RGBA").crop((image["x"], image["y"], image["x"] + image["w"], image["y"] + image["h"]))


def _stack(images, sink):
    if not images or any(i.size != images[0].size for i in images):
        raise ValueError("Palette pages must retain equal actual source dimensions")
    width, height = images[0].size
    atlas = Image.new("RGBA", (width, height * len(images)))
    for index, image in enumerate(images):
        atlas.paste(image, (0, index * height))
    return sink.add_raster(atlas), width, height * len(images)


def _known_track_properties(properties, node, recipe, context):
    properties = dict(properties)
    for track in recipe.get("tracks", []):
        if track["target"] != node["id"] or track.get("clock") not in context:
            continue
        now = context[track["clock"]]
        if not isinstance(now, (int, float)) or isinstance(now, bool):
            continue
        keys = track["keyframes"]
        if not keys or not all(isinstance(k["value"], (int, float)) for k in keys):
            continue
        previous = keys[0]
        value = previous["value"]
        for next_key in keys[1:]:
            if now < next_key["time"]:
                if track.get("easing") == "linear" and next_key["time"] != previous["time"]:
                    ratio = max(0, min(1, (now - previous["time"]) / (next_key["time"] - previous["time"])))
                    value = previous["value"] + (next_key["value"] - previous["value"]) * ratio
                break
            previous, value = next_key, next_key["value"]
        properties[track["property"]] = value
    return properties


def _route(route, parent, recipe, context, *, ignore_opacity=False):
    rect, rgba, visible = parent, (1, 1, 1, 1), True
    for node in route:
        properties, _ = scene_properties(node, recipe, context)
        properties = _known_track_properties(properties, node, recipe, context)
        visible = visible and properties.get("visible", True)
        if ignore_opacity:
            properties.pop("opacity", None)  # Native Judge owns the actual hit lifetime/palette timer.
        rect = node_rect(properties, rect)
        rotation = properties.get("rotation", 0) % 360
        if rotation == 180:
            ox, oy = anchor(properties.get("origin", "top-left"))
            x, y, w, h = rect
            rect = (x + 2 * ox * w, y + 2 * oy * h, -w, -h)
        elif rotation != 0:
            raise ValueError("Widget route needs an explicit non-axis-aligned bake")
        rgba = colour(properties, rgba)
    return rect, rgba, visible


def _palette_rows(rows, tracks):
    if len(rows) <= 1:
        return rows, 0
    phases, periods = [], set()
    for row in rows:
        ids = {n["id"] for n in row["ancestors"]}
        matching = [t for t in tracks if t["target"] in ids and t["property"] == "opacity"
                    and t.get("loop") and t.get("trigger") == "judgement.hit" and t.get("easing") == "step"]
        if len(matching) != 1:
            raise ValueError("Judge palette timing is not a qualified source step sequence")
        keys = matching[0]["keyframes"]
        active = [k["time"] for k in keys[:-1] if k["value"] == 1]
        if len(active) != 1 or any(k["value"] not in (0, 1) for k in keys):
            raise ValueError("Judge palettes need an explicit blended bake")
        phases.append((active[0], row))
        periods.add(keys[-1]["time"])
    if len(periods) != 1:
        raise ValueError("Judge palette periods do not agree")
    period = periods.pop()
    phases.sort(key=lambda p: p[0])
    if [p[0] for p in phases] != [i * period / len(phases) for i in range(len(phases))]:
        raise ValueError("Uneven palette periods require an explicit atlas schedule")
    return [row for _, row in phases], period


def _label_palette(row, recipe, images):
    tracks = [t for t in recipe.get("tracks", [])
              if t["target"] == row["label_node"].get("id") and t["property"] == "resource"]
    if not tracks:
        return [images[row["image"]]], 0
    if len(tracks) != 1:
        raise ValueError("Judge label has conflicting resource palette tracks")
    track = tracks[0]
    keys = track["keyframes"]
    if (not track.get("loop") or track.get("trigger") != "judgement.hit"
            or track.get("easing") != "step" or track.get("clock")
            or len(keys) < 2 or keys[0]["time"] != 0
            or keys[-1]["value"] != keys[0]["value"]):
        raise ValueError("Judge label palette is not a qualified looping hit sequence")
    period = keys[-1]["time"]
    if period <= 0 or [k["time"] for k in keys] != [i * period / (len(keys) - 1) for i in range(len(keys))]:
        raise ValueError("Uneven label palette periods need an explicit atlas schedule")
    resources = recipe["resource_images"]
    return [images[resources[k["value"]]] for k in keys[:-1]], period


def _judge_lift_offsets(row, recipe, context, host, stage_height):
    """Use native Lift only when the source route moves by exactly one lane.

    SkinJudge prepares its label independently, then places the combo relative
    to that label. Applying offset 3 to both would lift the number twice.
    """
    route = row["ancestors"]
    ids = {node["id"] for node in route}
    tracks = [t for t in recipe.get("tracks", []) if t["target"] in ids and t.get("clock") == "lift.offset"]
    if not tracks:
        return []
    if any(t["property"] != "y" or t.get("loop") or t.get("easing") != "linear" for t in tracks):
        raise ValueError("Judge Lift track does not match the native lane offset")
    samples = {0, 1, *(k["time"] for t in tracks for k in t["keyframes"])}
    base, _, _ = _route(route, host, recipe, {**context, "lift.offset": 0}, ignore_opacity=True)
    for amount in samples:
        rect, _, _ = _route(route, host, recipe, {**context, "lift.offset": amount}, ignore_opacity=True)
        expected = (base[0], base[1] - amount * stage_height, base[2], base[3])
        if not 0 <= amount <= 1 or any(not math.isclose(a, b, abs_tol=1e-6) for a, b in zip(rect, expected)):
            raise ValueError("Judge Lift track does not match the native lane offset")
    return [3]


def compose_judges(recipe, sink, stages, canvas_height):
    """Return native image/value/judge/destination arrays for actual stage timers.

    SP2 is the first player's presentation and therefore still uses timer 46.
    DP stage indices 0/1 use 46/47. The source OMS judgement owner expires after
    500ms (GameplaySkinEventBudgets), matching this native destination lifetime.
    """
    stages = [_rect(stage) for stage in stages]
    if len(stages) not in (1, 2):
        raise ValueError("Judge composition supports one or two actual stage groups")
    images = {image["id"]: image for image in recipe["images"]}
    result = {"image": [], "value": [], "judge": [], "destination": [], "gaps": []}
    if not any(r["alternatives"] for r in recipe["results"]):
        # The official loader establishes JudgeManager's regions from SkinJudge
        # objects, not from lanes. Empty image arrays keep the source's OFF
        # appearance while allowing real stage timing/fast state to update.
        for index in range(len(stages)):
            identifier = recipe["judge"]["id"] + ".stage" + str(index)
            result["judge"].append({"id": identifier, "index": index, "shift": False, "images": [], "numbers": []})
            result["destination"].append({"id": identifier})
        result["gaps"].append({"code": "native-judge-state-ownership", "stages": len(stages),
                               "detail": "Invisible native Judge owners preserve real per-stage timing while source judgement art is OFF. No image, number or synthetic judgement is emitted."})
        return result
    logical_scale = canvas_height / 768
    active_nodes = set()
    for index, (sx, sy, sw, sh) in enumerate(stages):
        slot_height = min(sh * .12, canvas_height * .11)
        host = (sx, sy + sh * .81 - slot_height / 2, sw, slot_height)
        timer = (46, 47)[index]
        prefix = recipe["judge"]["id"] + ".stage" + str(index)
        judge = {"id": prefix, "index": index, "shift": True, "images": [], "numbers": []}
        for grade in recipe["results"]:
            rows, period = _palette_rows(grade["alternatives"], recipe.get("tracks", []))
            if not rows:
                raise ValueError("An active Judge is missing a required grade label")
            row = rows[0]
            for palette_row in rows:
                active_nodes.update(n["id"] for n in palette_row["ancestors"])
                active_nodes.add(palette_row["label_node"].get("id"))
            context = {**recipe.get("context", {}), "judgement.result": grade["grade"], "combo.active": True, "lift.offset": 0}
            row_rect, _, visible = _route(row["ancestors"], host, recipe, context, ignore_opacity=True)
            if not visible:
                raise ValueError("Selected Judge row disagrees with source visibility")
            offsets = _judge_lift_offsets(row, recipe, context, host, sh)
            if any(_judge_lift_offsets(other, recipe, context, host, sh) != offsets for other in rows[1:]):
                raise ValueError("Judge palette rows disagree on Lift tracking")
            label = images[row["image"]]
            label_id = prefix + "." + grade["grade"]
            palette, resource_period = _label_palette(row, recipe, images)
            if resource_period:
                if len(rows) != 1 or row["font"]:
                    raise ValueError("Combined Judge row and resource palettes need an explicit schedule")
                period = resource_period
            elif period:
                palette = [images[r["image"]] for r in rows]
            if period:
                src, width, height = _stack([_pixels(image, sink) for image in palette], sink)
                native_image = {"id": label_id, "src": src, "x": 0, "y": 0, "w": width, "h": height,
                                "divy": len(palette), "cycle": period, "timer": timer}
            else:
                native_image = {k: v for k, v in label.items() if k in IMAGE_FIELDS}
                native_image["id"] = label_id
            result["image"].append(native_image)
            label_height = row["label_node"].get("properties", {}).get("height-px", label["h"] / logical_scale) * logical_scale
            label_width = label["w"] * label_height / label["h"]
            centre_x = row_rect[0] + row_rect[2] / 2
            label_y = row_rect[1] + (row_rect[3] - label_height) / 2
            font = row["font"] if grade["native_index"] < 3 else None
            number_destination = {"id": ""}
            gap = row["ancestors"][-1]["properties"].get("gap-px", 0) * logical_scale
            if font:
                number_height = row["number_node"]["properties"]["font-size"] * logical_scale
                glyph_width = font["glyph_size"][0] * number_height / font["line_height"]
                advance = font["advance"] * number_height / font["line_height"]
                wanted_space = advance - glyph_width
                space = round(wanted_space)
                if not math.isclose(space, wanted_space, abs_tol=1e-7):
                    result["gaps"].append({"code": "native-font-spacing-rounded", "font": font["font"],
                                           "wanted": wanted_space, "used": space, "error_per_advance": space - wanted_space})
                if any(r["font"] is None or r["font"]["glyph_size"] != font["glyph_size"] for r in rows):
                    raise ValueError("Palette numbers do not have matching real glyph cells")
                number_id = label_id + ".combo"
                if period:
                    src, width, height = _stack([_pixels(images[r["font"]["grid_image"]], sink) for r in rows], sink)
                    value = {"src": src, "x": 0, "y": 0, "w": width, "h": height, "divx": 10, "divy": len(rows), "cycle": period, "timer": timer}
                else:
                    value = {k: v for k, v in font["value_grid"].items() if k != "source"}
                digits = font["digit_count"]
                value.update({"id": number_id, "space": space, "digit": digits, "ref": 0})
                result["value"].append(value)
                # JsonPlaySkinObjectLoader subtracts glyph_width*digits/2;
                # SkinNumber then centres only its non-padding digits. Account
                # for integer spacing here, while SkinJudge shifts the label.
                number_destination = {"id": number_id, "timer": timer, "loop": -1,
                                      "dst": [{"time": 0, "x": label_width + gap - space * digits / 2,
                                               "y": (label_height - number_height) / 2,
                                               "w": glyph_width, "h": number_height}, {"time": 500}]}
                label_x = centre_x - (label_width + gap + glyph_width - (glyph_width + space)) / 2
            else:
                label_x = centre_x - label_width / 2
            judge["images"].append({"id": label_id, "timer": timer, "loop": -1, "filter": 0,
                                    **({"offsets": offsets} if offsets else {}),
                                    "dst": [{"time": 0, **native_rect((label_x, label_y, label_width, label_height), canvas_height)}, {"time": 500}]})
            judge["numbers"].append(number_destination)
        result["judge"].append(judge)
        result["destination"].append({"id": prefix})
    active_tracks = [t for t in recipe.get("tracks", []) if t["target"] in active_nodes]
    combo_fit = [t["id"] for t in active_tracks if t.get("clock") == "combo.value" and t["property"].startswith("scale-")]
    if combo_fit:
        result["gaps"].append({"code": "judge-combo-fit-track", "tracks": combo_fit,
                               "detail": "The native Judge owns per-stage hit-time course combo, but beatoraja 0.8.9 does not expose it to Lua. NUMBER_COMBO 104 has no property implementation; 75/105 are maximum combo, so the source fit tracks cannot use them. Source-size rows are retained."})
    unmapped_clocks = sorted({t["clock"] for t in active_tracks
                              if t.get("clock") and t["clock"] not in ("combo.value", "lift.offset")
                              and t["clock"] not in recipe.get("context", {})})
    if unmapped_clocks:
        result["gaps"].append({"code": "judge-dynamic-position-tracks", "clocks": unmapped_clocks})
    return result


def _gauge_host(bar, stages, canvas):
    hosts = bar["hosts"]
    if len(hosts) != 1:
        raise ValueError("Expected one qualified gauge material owner")
    owner = hosts[0]
    if owner["target"]["kind"] == "global":
        return canvas
    if owner["slot"] == "hud.gauge" and owner["target"]["kind"] == "stage":
        return stages[int(owner["target"]["group-logical"])]
    raise ValueError("Gauge material host cannot be treated as a canvas or gauge stage")


def compose_gauges(recipe, sink, gauge_stages, canvas_height):
    """Use native Graph.value and gauge_type callbacks on the exact source bar.

    Caller supplies the actual gauge-band stages, including the OMS .006-height
    surface gap. Global Standard decorations instead resolve against the canvas.
    """
    stages = [_rect(stage) for stage in gauge_stages]
    canvas = (0, 0, canvas_height * 16 / 9, canvas_height)
    images = {image["id"]: image for image in recipe["images"]}
    result = {"image": [], "graph": [], "destination": [], "gaps": []}
    emitted_dark = set()
    palette_targets = {v["target"] for v in recipe["variants"] if v["property"] == "resource" and v["source"] == "gauge.type"}
    for bar_index, bar in enumerate(recipe["bars"]):
        host = _gauge_host(bar, stages, canvas)
        root_id = bar["ancestors"][0]["id"]
        for gauge_type, kind in enumerate(GAUGE_PALETTES):
            context = {**recipe.get("context", {}), "gauge.type": kind}
            draw = Lua("function() return main_state.gauge_type() == " + str(gauge_type) + " end")
            # Draw declared full dark bars, excluding the dynamic boundary stripe
            # inside reveal clips. Never synthesize a substitute dark palette.
            for destination in recipe["destinations"]:
                node, route = destination["node"], destination["ancestors"]
                key = (node["id"], gauge_type)
                if (key in emitted_dark or node["id"] not in palette_targets or route[0]["id"] != root_id or node["type"] != "sprite"
                        or any(n["type"] == "clip" for n in route)):
                    continue
                props, _ = scene_properties(node, recipe, context)
                resource = props.get("resource")
                if resource not in recipe["resource_images"]:
                    continue
                rect, rgba, visible = _route(route, host, recipe, context)
                if not visible:
                    continue
                identifier = f"{recipe['family']}.gauge.dark.{bar_index}.{gauge_type}.{len(emitted_dark)}"
                image = images[recipe["resource_images"][resource]]
                result["image"].append({**{k: v for k, v in image.items() if k in IMAGE_FIELDS}, "id": identifier})
                result["destination"].append({"id": identifier, "draw": draw, "filter": 0, "blend": 1,
                                               "dst": [{**native_rect(rect, canvas_height), **native_colour(rgba)}]})
                emitted_dark.add(key)
            clip, inherited_rgba, visible = _route(bar["ancestors"], host, recipe, context)
            rect, rgba, child_visible = _route([bar["sprite_node"]], clip, recipe, context)
            rgba = tuple(a * b for a, b in zip(rgba, inherited_rgba))
            if not visible or not child_visible:
                continue
            image = images[bar["states"][kind]]
            graph = {k: v for k, v in image.items() if k in IMAGE_FIELDS}
            identifier = f"{recipe['family']}.gauge.fill.{bar_index}.{gauge_type}"
            if clip[2] < 0:
                # A flipped source plus negative destination width preserves all
                # original pixels while native Graph crops from the right edge.
                source = _pixels(image, sink).transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                graph.update({"src": sink.add_raster(source), "x": 0, "y": 0})
                x, y, w, h = rect
                rect = (x + w, y, -w, h)
            graph.update({"id": identifier, "angle": 0,
                          "value": Lua("function() return math.max(0, math.min(1, main_state.gauge() / 100)) end")})
            result["graph"].append(graph)
            result["destination"].append({"id": identifier, "draw": draw, "filter": 0, "blend": 1,
                                           "dst": [{**native_rect(rect, canvas_height), **native_colour(rgba)}]})
    result["gaps"].extend([
        {"code": "gauge-extra-widgets", "detail": "Percent, clear threshold, dynamic boundary shade and recorded feedback remain separate HUD/bake work; original dependencies are retained in the materials recipe."},
        {"code": "native-course-gauge-palette-adaptation", "palettes": {"6": "hard", "7": "hard", "8": "ex-hard"},
         "detail": "Native CLASS/EXCLASS/EXHARDCLASS use same-family survival-bar art. The fill reads the actual host gauge value; these are visual selections, not claims of equivalent source gauge rules."},
    ])
    return result
