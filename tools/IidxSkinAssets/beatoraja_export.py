"""Build private native beatoraja packages from one frozen IIDX author family.

The exporter never includes player data and never opens the real installation.
Native gameplay owns notes, judgement, BGA and score; retained art supplies their
appearance. The local receipt distinguishes implemented features from gaps.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
from dataclasses import replace
from pathlib import Path
from typing import Any

from PIL import Image

from beatoraja_author import (AuthorPackage, Contribution, bms_sections,
                             layout_for, material_bindings, material_templates,
                             read_json, source_path, walk_nodes)
from beatoraja_native import (AssetSink, Lua, all_of, colour, lua, native_colour,
                             native_rect, node_rect, scene_properties, native_song_expression,
                             native_offset_expression, tint_pixels)
from beatoraja_preparation import (PREPARATION_TIMER, PRESENTATION_TIMER, READY_TIMER,
                                  preparation_expression, preparation_plan)


MODE = {"p1": ("7k", 0), "p2": ("7k", 0), "dp": ("14k", 2)}


def native_art_expression(source):
    song = native_song_expression(source)
    return ((song, "string") if song is not None else
            native_offset_expression(source) or preparation_expression(source))


def constant_clock(scene, source, seen=()):
    """Resolve only author-declared literal arithmetic, never gameplay state."""
    if isinstance(source, (int, float)) and not isinstance(source, bool):
        return source
    if not isinstance(source, str) or not source.startswith("value."):
        return None
    name = source[6:]
    if name in seen:
        raise ValueError("Cyclic author constant clock")
    definition = next((v for v in scene.get("numericSources", []) if v["id"] == name), None)
    if definition is None:
        return None
    left = constant_clock(scene, definition["left"], (*seen, name))
    right = constant_clock(scene, definition["right"], (*seen, name))
    if left is None or right is None:
        return None
    operation = definition["operation"]
    return {"add": lambda: left + right, "subtract": lambda: left - right,
            "multiply": lambda: left * right}.get(operation, lambda: None)()


ANIMATION_RUNTIME = """local main_state = require('main_state')
local function animation_frame(timer, fps, count, loop_start, start_frame)
  local origin = timer == 0 and 0 or main_state.timer(timer)
  if origin == main_state.timer_off_value then return -1 end
  local frame = math.floor(math.max(0, main_state.time() - origin) * fps / 1000000) + start_frame
  if frame >= count and loop_start >= 0 then
    return loop_start + (frame - loop_start) % (count - loop_start)
  end
  return math.min(frame, count)
end
local function input_frame(keyon, keyoff, fps, count, first, last, release)
  local on = main_state.timer(keyon)
  if on ~= main_state.timer_off_value then
    local frame = math.floor(math.max(0, main_state.time() - on) * fps / 1000000)
    if frame < last then return frame end
    return first + (frame - first) % (last - first)
  end
  local off = main_state.timer(keyoff)
  if off == main_state.timer_off_value then return -1 end
  local frame = release + math.floor(math.max(0, main_state.time() - off) * fps / 1000000)
  return frame < count and frame or -1
end
"""


class Composer:
    def __init__(self, author: AuthorPackage, sink: AssetSink, options: dict[str, Any],
                 presentation: str, baked: dict[str, tuple[Path, dict[str, Any]]]):
        self.author, self.sink, self.options = author, sink, options
        self.presentation = presentation
        self.keymode, skin_type = MODE[presentation]
        self.w, self.h = (1280, 720) if author.family == "standard" else (1920, 1080)
        self.canvas = (0, 0, self.w, self.h)
        self.baked = baked
        self.skin: dict[str, Any] = {"type": skin_type, "name": author.name,
                                   "author": "ZDaMexy", "w": self.w, "h": self.h,
                                   "loadend": 500, "playstart": 1200, "close": 2500,
                                   "fadeout": 1000, "finishmargin": 2500,
                                   "source": [], "image": [], "imageset": [], "graph": [],
                                   "font": [], "value": [], "text": [], "destination": []}
        self.gaps: list[dict[str, Any]] = []
        self.handled_roots: set[str] = set()
        self.context = {"layout.ruleset": "bms", "layout.presentation": presentation,
                        **{"option." + k: v for k, v in options.items()},
                        "scroll.available": True,
                        "scroll.hi-speed-available": False, "scroll.adjusting": False,
                        "lift.reversed": False}
        self._id = 0

    def identifier(self, label="image") -> str:
        self._id += 1
        return label + "." + str(self._id)

    def gap(self, feature: str, **detail):
        item = {"feature": feature, **detail}
        if item not in self.gaps:
            self.gaps.append(item)

    def image(self, src: str, crop=None, identifier=None) -> str:
        identifier = identifier or self.identifier()
        w, h = self.sink.dimensions[src]
        x, y, w, h = crop or (0, 0, w, h)
        self.skin["image"].append({"id": identifier, "src": src, "x": x, "y": y, "w": w, "h": h})
        return identifier

    def destination(self, identifier: str, rect, *, rgba=(1, 1, 1, 1), blend="alpha", draw=None, clip=None, **extra):
        point = {**native_rect(rect, self.h), **native_colour(rgba)}
        if clip is not None:
            point.update({"clip_" + key: value for key, value in native_rect(clip, self.h).items()})
        destination = {"id": identifier, "filter": 0, "blend": 2 if blend == "additive" else 1,
                       "dst": [point], **extra}
        if draw:
            destination["draw"] = draw
        self.skin["destination"].append(destination)
        return destination

    def merge(self, recipe: dict[str, Any]):
        for key in ("font", "image", "imageset", "value", "floatvalue", "text", "graph", "judge",
                    "slider", "hiddenCover", "liftCover", "customTimers", "destination"):
            self.skin.setdefault(key, []).extend(recipe.get(key, []))
        self.gaps.extend(recipe.get("gaps", []))

    def preparation(self, frame: Contribution):
        recipe = preparation_plan(frame)
        self.merge(recipe)
        self.skin["playstart"] = recipe["playstart"]

    def hud(self, frame: Contribution, native_font_path: str | None):
        from beatoraja_hud import export_hud
        from beatoraja_reading import reading_ownership
        reading = reading_ownership(self.author, self.options, self.keymode, self.presentation)
        self.handled_roots.update(reading["handled_root_ids"])
        if native_font_path:
            self.skin["font"].append({"id": "native.fallback", "path": native_font_path})
        for contribution in (self.author.base(), frame):
            roots = [(contribution.scene["root"], self.canvas)]
            layout = layout_for(frame, self.keymode, self.presentation)
            for binding in material_bindings(contribution, self.keymode, self.presentation):
                if binding["target"]["kind"] == "global" and binding["slot"] == "decoration" and binding["mode"] == "Provide":
                    roots.extend((root, self.canvas) for root in material_templates(contribution, binding["slot"], binding["resource"]))
                elif binding["target"]["kind"] == "stage" and binding["slot"] in ("hud.gauge", "decoration") and binding["mode"] == "Provide":
                    stage = layout["stages"][int(binding["target"]["group-logical"])]
                    sx, sy, sw, sh = stage["rect"]
                    if binding["slot"] == "hud.gauge":
                        rect = (sx * self.w, (sy + sh + layout["keyAreaHeight"] + .006) * self.h, sw * self.w, layout["gaugeHeight"] * self.h)
                    else:
                        rect = (sx * self.w, sy * self.h, sw * self.w, sh * self.h)
                    roots.extend((root, rect) for root in material_templates(contribution, binding["slot"], binding["resource"]))
            hud_context = {k: v for k, v in self.context.items() if not k.startswith("scroll.")}
            for root, rect in roots:
                if root["id"] in self.handled_roots:
                    continue
                self.merge(export_hud(contribution, root, rect, self.sink,
                                      canvas_height=self.h, options=self.options, context=hud_context,
                                      id_prefix=self.identifier("hud"), native_font_id="native.fallback" if native_font_path else None,
                                      fallback_contributions=(self.author.base(),),
                                      skip_node_ids=reading["handled_node_ids"]))
        self.skin["font"] = list({item["id"]: item for item in self.skin["font"]}.values())

    def graph(self, layer="all"):
        from beatoraja_graph import export_graph
        if layer == "all":
            for phase in ("background", "art", "foreground"):
                self.graph(phase)
            return
        result = export_graph(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas, layer=layer)
        animated = None
        feedback = None
        if layer == "art":
            from beatoraja_covers import export_animated_graph_area
            animated = export_animated_graph_area(self.author, self.options, self.keymode, self.presentation,
                                                  self.sink, self.canvas, self.baked)
            result["gaps"] = [g for g in result["gaps"] if g.get("template") not in animated["handled_template_ids"]]
            self.handled_roots.update(animated["handled_root_ids"])
        elif layer == "foreground":
            from beatoraja_feedback import feedback_plan
            feedback = feedback_plan(self.author, self.options, self.keymode, self.presentation, self.canvas, "graph")
            result["gaps"] = [g for g in result["gaps"] if g.get("template") not in feedback["handled_template_ids"]]
            self.handled_roots.update(feedback["handled_root_ids"])
        self.handled_roots.update(result["handled_root_ids"])
        self.merge({**result["arrays"], "destination": result["destination"], "gaps": result["gaps"]})
        if animated:
            self.merge(animated)
        if feedback:
            self.feedback(feedback)

    def feedback(self, plan):
        self.merge(plan)
        for draw in plan["draws"]:
            self.draw_baked(draw["manifest"], draw["animation"], draw["rect"], draw["rgba"],
                            timer=draw["timer"], start_frame=0, conditions=draw["conditions"], offsets=draw.get("offsets"))

    def reading(self, frame: Contribution):
        from beatoraja_reading import export_reading
        result = export_reading(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas)
        self.handled_roots.update(result["handled_root_ids"])
        self.handled_roots.update(result["handled_node_ids"])
        self.merge(result)

    def turntable(self, frame: Contribution):
        from beatoraja_decoration import export_turntable
        result = export_turntable(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas)
        self.handled_roots.update(result["handled_root_ids"])
        self.merge(result)

    def bga_frame(self, frame: Contribution):
        from beatoraja_decoration import export_bga_frame
        result = export_bga_frame(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas)
        self.handled_roots.update(result["handled_root_ids"])
        self.merge(result)

    def timing_display(self, frame: Contribution):
        from beatoraja_timing import export_timing
        result = export_timing(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas)
        self.handled_roots.update(result["handled_root_ids"])
        self.merge(result)

    def widgets(self, frame: Contribution, *, include_judge=True, include_gauge=True):
        from beatoraja_materials import collect_gauge_materials, collect_judge_materials
        from beatoraja_widgets import compose_gauges, compose_judges
        layout = layout_for(frame, self.keymode, self.presentation)
        stages, gauges = [], []
        for stage in layout["stages"]:
            sx, sy, sw, sh = stage["rect"]
            stages.append((sx * self.w, sy * self.h, sw * self.w, sh * self.h))
            gauges.append((sx * self.w, (sy + sh + layout["keyAreaHeight"] + .006) * self.h, sw * self.w, layout["gaugeHeight"] * self.h))
        if include_judge:
            judge = collect_judge_materials(self.author, self.options, self.keymode, self.presentation, self.sink.add_source)
            self.merge(compose_judges(judge, self.sink, stages, self.h))
        if include_gauge:
            from beatoraja_gauge import compose_gauge_markers, compose_gauge_shade
            gauge = collect_gauge_materials(self.author, self.options, self.keymode, self.presentation, self.sink.add_source)
            core = compose_gauges(gauge, self.sink, gauges, self.h)
            core["gaps"] = [g for g in core["gaps"] if g["code"] != "gauge-extra-widgets"]
            self.merge(core)
            self.merge(compose_gauge_shade(gauge, frame, gauges, self.h, self.sink))
            self.merge(compose_gauge_markers(gauge, frame, gauges, self.h))
            from beatoraja_feedback import feedback_plan
            self.feedback(feedback_plan(self.author, self.options, self.keymode, self.presentation, self.canvas, "gauge"))

    def draw_tree(self, contribution: Contribution, root: dict[str, Any], parent=None,
                  rgba=(1, 1, 1, 1), blend="alpha", timer=0, offsets=None, conditions=(), clip=None):
        if root["id"] in self.handled_roots:
            return
        if all(n["type"] in ("text", "container") for n in walk_nodes(root)):
            # The HUD pass owns these source text gates and their diagnostics.
            return
        # READY is owned by event_clips; its original ancestor gate is now live
        # in other passes and must not create a second copy of the same movie.
        if root.get("clock") == "preparation.ready-elapsed":
            return
        properties, dynamic = scene_properties(root, contribution.scene, self.context)
        translations = [track for track in contribution.scene.get("tracks", [])
                        if track["target"] == root["id"] and track.get("clock") == "preparation.elapsed"
                        and self.context.get("preparation.available") is not False
                        and track["property"] in ("x", "y")]
        if translations:
            from beatoraja_entrance import apply_preparation_translation
            if len(translations) != 1 or clip is not None:
                raise ValueError("Entrance translation requires one source track without an external fixed clip")
            track = translations[0]
            static = replace(contribution, scene={**contribution.scene,
                "tracks": [t for t in contribution.scene["tracks"] if t is not track]})
            first = len(self.skin["destination"])
            self.draw_tree(static, root, parent, rgba, blend, timer, offsets, conditions, clip)
            self.skin["destination"][first:] = apply_preparation_translation(
                self.skin["destination"][first:], track, parent or self.canvas,
                properties.get(track["property"], 0))
            return
        visibility = [d for d in dynamic if d["property"] == "visible"]
        if properties.get("visible", True) is False and not visibility:
            return
        conditions = list(conditions)
        for operation in visibility:
            if (operation["source"] == "input.pressed" and operation["kind"] == "bindings"
                    and 100 <= timer <= 117 and timer % 10 < 8):
                # This source gate belongs to the same native KEYON clock as
                # its beam. KeyInputProccessor switches it OFF on release.
                conditions.append(f"main_state.timer({timer}) ~= main_state.timer_off_value")
                continue
            expression = native_art_expression(operation["source"])
            if expression is not None and operation["kind"] == "bindings" and expression[1] == "boolean":
                conditions.append(expression[0])
                continue
            if expression is None or operation["kind"] != "variants":
                self.gap("unmapped visibility", node=root["id"], sources=[operation["source"]])
                return
            cases = {case["key"]: case["value"] for case in operation["cases"]}
            conditions.append("(function() local cases=" + lua(cases) + ";local value=cases[tostring(" + expression[0]
                              + ")];if value == nil then return " + lua(operation["default"]) + " end;return value end)()")
        resource_states = [d for d in dynamic if d["property"] == "resource"]
        if root["type"] != "text" and any(native_art_expression(d["source"]) is None or d["kind"] != "variants" for d in resource_states):
            self.gap("unmapped resource state", node=root["id"], sources=[d["source"] for d in dynamic if d["property"] == "resource"])
            return
        from beatoraja_beat import beat_opacity_track, apply_beat_opacity, BEAT_ADAPTATION
        beat = beat_opacity_track(root, contribution.scene)
        if beat:
            properties["opacity"] = 1
        for track in contribution.scene.get("tracks", []):
            if track["target"] != root["id"] or track["property"] == "projective-quad":
                continue
            if track is beat:
                continue
            if track.get("clock") == "preparation.elapsed" and self.context.get("preparation.available") is False:
                continue
            keys = track.get("keyframes", [])
            clock = self.context.get(track.get("clock"))
            if track.get("clock") == "preparation.elapsed" and track["property"] == "opacity":
                from beatoraja_entrance import preparation_opacity_condition
                conditions.append(preparation_opacity_condition(track, properties.get("opacity", 1)))
                properties["opacity"] = 1  # The source's binary track is the draw gate.
            elif isinstance(clock, (int, float)) and all(isinstance(k["value"], (int, float)) for k in keys) and keys:
                if track.get("loop") and keys[-1]["time"] > keys[0]["time"]:
                    clock = keys[0]["time"] + (clock - keys[0]["time"]) % (keys[-1]["time"] - keys[0]["time"])
                value = keys[0]["value"]
                for first, last in zip(keys, keys[1:]):
                    if clock < last["time"]:
                        value = first["value"]
                        if track.get("easing", "linear") == "linear":
                            ratio = max(0, min(1, (clock - first["time"]) / (last["time"] - first["time"])))
                            value += (last["value"] - first["value"]) * ratio
                        break
                    value = last["value"]
                properties[track["property"]] = value
            elif (track.get("clock") == "gauge.value" and track["property"] == "opacity"
                  and track.get("easing") == "step" and not track.get("loop")
                  and keys and all(k["value"] in (0, 1) for k in keys)):
                branches = " ".join("if value < " + lua(b["time"]) + " then return " + lua(bool(a["value"])) + " end"
                                    for a, b in zip(keys, keys[1:]))
                conditions.append("(function() local value=main_state.gauge()/100; " + branches
                                  + " return " + lua(bool(keys[-1]["value"])) + " end)()")
                properties["opacity"] = 1
        rect = node_rect(properties, parent or self.canvas)
        rgba = colour(properties, rgba)
        if rgba[3] <= 0:
            return
        blend = root.get("blend", "inherit") if root.get("blend", "inherit") != "inherit" else blend
        if properties.get("rotation", 0) or root.get("effects"):
            self.gap("node transform/effects", node=root["id"])
            return
        kind = root["type"]
        if kind in ("sprite", "projective-sprite"):
            if kind == "projective-sprite":
                from beatoraja_entrance_art import entrance_quad_track
                entrance = entrance_quad_track(root, contribution.scene)
                if entrance:
                    if resource_states:
                        raise ValueError("Entrance quad has an unqualified changing texture")
                    self.entrance_quad(contribution, root, properties, entrance, rect, rgba, blend,
                                       timer=timer, offsets=offsets, conditions=conditions, clip=clip)
                    return
            orientation = {"flip_x": False, "flip_y": False}
            raster_source = None
            if kind == "projective-sprite":
                from beatoraja_decoration import fixed_projective_quad
                try:
                    projected = fixed_projective_quad(root, contribution.scene, rect)
                except ValueError as error:
                    from beatoraja_affine import fixed_affine_quad, rasterize_canvas_affine
                    try:
                        quad = fixed_affine_quad(root, contribution.scene)
                    except ValueError:
                        self.gap("unsupported projective source", node=root["id"], detail=str(error))
                        return
                    with Image.open(source_path(self.author.root, contribution.resources[properties["resource"]]["path"])) as source:
                        raster, projected = rasterize_canvas_affine(source.convert("RGBA"), quad, rect)
                    raster_source = self.sink.add_raster(raster)
                    self.gap("fixed-affine-quad-rasterized", node=root["id"], detail=projected)
                else:
                    orientation = {k: projected[k] for k in ("flip_x", "flip_y")}
                rect = projected["rect"]
                rgba = (*rgba[:3], rgba[3] * projected["opacity"])
            def add(resource_id):
                if raster_source:
                    return self.image(raster_source)
                relative = contribution.resources[resource_id]["path"]
                if any(orientation.values()):
                    from beatoraja_decoration import add_oriented_source
                    return self.image(add_oriented_source(self.sink, relative, **orientation))
                return self.image(self.sink.add_source(relative))
            if resource_states:
                if len(resource_states) != 1:
                    raise ValueError("Sprite has multiple unresolved resource selectors")
                operation = resource_states[0]
                resource_ids = list(dict.fromkeys([operation["default"], *[c["resource"] for c in operation["cases"]]]))
                cases = {case["key"]: resource_ids.index(case["resource"]) for case in operation["cases"]}
                identifier = self.identifier("host.art")
                value = Lua("(function() local cases=" + lua(cases) + ";return function() local value=cases[tostring("
                            + native_art_expression(operation["source"])[0] + ")];return value == nil and 0 or value end end)()")
                self.skin["imageset"].append({"id": identifier, "images": [add(r) for r in resource_ids], "value": value})
            else:
                identifier = add(properties["resource"])
            destination = self.destination(identifier, rect, rgba=rgba, blend=blend,
                             draw=all_of(conditions), clip=clip, **({"timer": timer} if timer else {}), **({"offsets": offsets} if offsets else {}))
            if beat:
                self.skin["destination"][-1] = apply_beat_opacity(destination, beat, rgba[3])
                self.gaps.append(BEAT_ADAPTATION)
        elif kind == "recorded-animation":
            fixed_time = constant_clock(contribution.scene, root.get("clock"))
            clock_timer = {"preparation.elapsed": PREPARATION_TIMER,
                           "presentation.elapsed": PRESENTATION_TIMER}.get(root.get("clock"))
            self.draw_baked(contribution.manifest_path, root["animation"], rect, rgba,
                            timer=clock_timer or timer, start_frame=0 if clock_timer else None,
                            offsets=offsets, static_time_ms=fixed_time, clip=clip, conditions=conditions)
        elif kind in ("container", "clip"):
            if kind == "clip":
                if (properties.get("clip-mode") != "bounds"
                        or any(d["property"] != "visible" for d in dynamic) or offsets or min(rect[2:]) <= 0
                        or not 0 <= properties.get("reveal-x", 1) <= 1 or not 0 <= properties.get("reveal-y", 1) <= 1):
                    self.gap("unmapped dynamic/offset clip", node=root["id"])
                    return
                own = (rect[0], rect[1], rect[2] * properties.get("reveal-x", 1), rect[3] * properties.get("reveal-y", 1))
                if clip is not None:
                    left, top = max(clip[0], own[0]), max(clip[1], own[1])
                    own = (left, top, min(clip[0]+clip[2], own[0]+own[2])-left,
                           min(clip[1]+clip[3], own[1]+own[3])-top)
                if min(own[2:]) <= 0 or min(native_rect(own, self.h)[k] for k in ("w", "h")) <= 0:
                    return  # A native zero-size clip disables clipping, rather than hiding art.
                clip = own
            for child in sorted(root.get("children", []), key=lambda n: -n.get("properties", {}).get("z", 0)):
                self.draw_tree(contribution, child, rect, rgba, blend, timer, offsets, conditions, clip)
        elif kind == "text":
            # Text is emitted separately by the native HUD adapter.
            return
        else:
            self.gap("unmapped scene primitive", node=root["id"], kind=kind)

    def entrance_quad(self, contribution, root, properties, track, rect, rgba, blend, *, timer, offsets, conditions, clip):
        from beatoraja_entrance_art import rasterize_canvas_quad
        relative = contribution.resources[properties["resource"]]["path"]
        available = preparation_expression("preparation.available")[0]
        elapsed = preparation_expression("preparation.elapsed")[0]
        samples = []
        original = properties.get("projective-quad")
        if original is not None:
            samples.append((original, "not " + available))
        keys = track["keyframes"]
        for i, key in enumerate(keys):
            phase = available + " and " + elapsed + " >= " + lua(key["time"])
            if i + 1 < len(keys):
                phase += " and " + elapsed + " < " + lua(keys[i+1]["time"])
            samples.append((key["value"], phase))
        with Image.open(source_path(self.author.root, relative)) as source:
            pixels = source.convert("RGBA")
        for quad, phase in samples:
            key = (relative, rect, lua(quad))
            cached = self.sink._entrance_quads.get(key)
            if cached is None:
                raster, geometry = rasterize_canvas_quad(pixels, quad, rect)
                cached = (self.sink.add_raster(raster) if raster is not None else None, geometry)
                self.sink._entrance_quads[key] = cached
            src, geometry = cached
            if src is None:
                continue
            identifier = self.image(src)
            self.destination(identifier, geometry["rect"], rgba=(*rgba[:3], rgba[3]*geometry["opacity"]),
                             blend=blend, draw=all_of([*conditions, phase]), clip=clip,
                             **({"timer": timer} if timer else {}), **({"offsets": offsets} if offsets else {}))
        self.gap("native-entrance-quad-rasterization", informational=True, node=root["id"],
                 sourceFrames=len(keys), detail="Original unit-Q corners and original RGBA use the production TL--BR triangle diagonal, sampled at native canvas pixel centres. Original step timing and overhang remain; native integer sampling is not bitwise GPU edge-coverage equivalence.")

    def draw_baked(self, manifest: str, animation: str, rect, rgba=(1, 1, 1, 1), *, timer=0, start_frame=None, offsets=None, static_time_ms=None, conditions=(), clip=None):
        key = (manifest, animation)
        if key not in self.baked:
            self.gap("recorded animation needs bake", manifest=manifest, animation=animation)
            return
        path, index = self.baked[key]
        if index.get("selectionComplete") and static_time_ms is None:
            self.draw_baked_sequence(path, index, rect, rgba, timer=timer, start_frame=start_frame, offsets=offsets, conditions=conditions, clip=clip)
            return
        if timer:
            self.gap("event animation needs complete bake", manifest=manifest, animation=animation)
            return
        if static_time_ms is not None:
            wanted = math.floor(max(0, static_time_ms) * index["framesPerSecond"] / 1000)
            count = index["sourceFrameCount"] - 1
            if wanted >= count:
                first = index.get("loopStartFrame", 0)
                wanted = first + (wanted - first) % (count - first) if index["loop"] else count
            frame = next((f for f in index["frames"] if f["sourceFrame"] == wanted), None)
            if frame is None:
                self.gap("constant animation frame needs bake", manifest=manifest, animation=animation, sourceFrame=wanted)
                return
        else:
            frame = next(f for f in index["frames"] if not f.get("terminal"))
        used_pages = {run["page"] for run in frame["runs"]}
        pages = {p["id"]: self.sink.add_baked_page(path, p) for p in index["pages"] if p["id"] in used_pages}
        cw, ch = index["canvas"]
        x, y, w, h = rect
        for run in frame["runs"]:
            identifier = self.image(pages[run["page"]], run["rect"])
            ox, oy = run["offset"]
            rw, rh = run["rect"][2:]
            self.destination(identifier, (x + ox * w / cw, y + oy * h / ch, rw * w / cw, rh * h / ch),
                             rgba=rgba, blend=run["blend"], draw=all_of(conditions), clip=clip, **({"offsets": offsets} if offsets else {}))
        if static_time_ms is None:
            declaration = next(a for a in self.author.json(manifest)["animations"] if a["id"] == animation)
            program = self.author.json(declaration["path"])
            single_frame_loop = (program["loop"] and len(program["frames"]) == 2
                                 and not program["frames"][-1]["nodes"] and frame["sourceFrame"] == 0)
            if not single_frame_loop:
                self.gap("steady animation snapshot", animation=animation, sourceFrame=frame["sourceFrame"])

    def draw_baked_sequence(self, path, index, rect, rgba, *, timer, start_frame, offsets=None, conditions=(), clip=None):
        pages = {p["id"]: self.sink.add_baked_page(path, p) for p in index["pages"]}
        images, groups = {}, {}
        count = index["sourceFrameCount"]
        if index["frames"][-1].get("terminal"):
            count -= 1
        loop_start = index.get("loopStartFrame", 0) if index["loop"] else -1
        start_frame = (loop_start if index["loop"] else 0) if start_frame is None else start_frame
        clock = f"animation_frame({timer},{index['framesPerSecond']},{count},{loop_start},{start_frame})"
        if index.get("heldLoop"):
            if not 100 <= timer <= 117:
                raise ValueError("A held-loop animation requires its real native key timer")
            first, last = index["heldLoop"]
            clock = f"input_frame({timer},{timer + 20},{index['framesPerSecond']},{count},{first},{last},{index['releaseFrame']})"
        for frame in index["frames"]:
            for run in frame["runs"]:
                image_key = (run["page"], *run["rect"])
                if image_key not in images:
                    images[image_key] = self.image(pages[run["page"]], run["rect"])
                key = (run["order"], run["blend"], *run["offset"], *run["rect"][2:])
                group = groups.setdefault(key, {"images": [], "frames": {}})
                identifier = images[image_key]
                if identifier not in group["images"]:
                    group["images"].append(identifier)
                group["frames"][frame["sourceFrame"]] = group["images"].index(identifier)
        cw, ch = index["canvas"]
        x, y, w, h = rect
        for key, group in sorted(groups.items()):
            _, blend, ox, oy, rw, rh = key
            identifier = self.identifier("animation")
            # A negative native ImageSet value suppresses drawing. The atlas
            # crop and destination remain immutable; only the real frame changes.
            value = Lua("(function() local frames=" + lua(group["frames"]) + ";return function() local v=frames[" + clock + "];return v == nil and -1 or v end end)()")
            self.skin["imageset"].append({"id": identifier, "images": group["images"], "value": value})
            self.destination(identifier, (x + ox * w / cw, y + oy * h / ch, rw * w / cw, rh * h / ch),
                             rgba=rgba, blend=blend, draw=all_of(conditions), clip=clip, **({"offsets": offsets} if offsets else {}))

    def frame(self, contribution: Contribution):
        for binding in material_bindings(contribution, self.keymode, self.presentation):
            if binding["target"]["kind"] == "global" and binding["slot"] == "stage.background" and binding["mode"] == "Provide":
                for root in material_templates(contribution, binding["slot"], binding["resource"]):
                    self.draw_tree(contribution, root)

    def lane_opening(self, frame: Contribution):
        layout = layout_for(frame, self.keymode, self.presentation)
        for binding in material_bindings(frame, self.keymode, self.presentation):
            if (binding["target"]["kind"] != "stage" or binding["slot"] != "playfield.baseplate"
                    or binding["mode"] != "Provide"):
                continue
            sx, sy, sw, sh = layout["stages"][int(binding["target"]["group-logical"])]["rect"]
            for root in material_templates(frame, binding["slot"], binding["resource"]):
                self.draw_tree(frame, root, (sx*self.w, sy*self.h, sw*self.w, sh*self.h))

    def decorations(self, frame: Contribution):
        from beatoraja_reading import reading_ownership
        from beatoraja_feedback import feedback_plan
        from beatoraja_progress import export_progress
        reading = reading_ownership(self.author, self.options, self.keymode, self.presentation)
        feedback = feedback_plan(self.author, self.options, self.keymode, self.presentation, self.canvas, "gauge")
        progress = export_progress(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas)
        self.handled_roots.update(reading["handled_root_ids"])
        self.handled_roots.update(reading["handled_node_ids"])
        self.handled_roots.update(feedback["handled_node_ids"])
        self.handled_roots.update(progress["handled_node_ids"])
        # These subtrees have explicit native Gauge owners. Do not draw their
        # static source a second time or report their main bar as wholly absent.
        owned = {b["target"] for b in frame.scene.get("bindings", [])
                 if (b["source"], b["property"]) in (("gauge.value", "reveal-x"), ("gauge.clear-threshold", "x"))}
        owned.update(v["target"] for v in frame.scene.get("variants", [])
                     if v["source"] == "gauge.type" and v["property"] == "resource")
        def mark(node):
            for child in node.get("children", []):
                mark(child)
            if node["type"] == "container" and node.get("children") and all(c["id"] in owned for c in node["children"]):
                owned.add(node["id"])
        for root in frame.templates.values():
            mark(root)
        self.handled_roots.update(owned)
        for binding in material_bindings(frame, self.keymode, self.presentation):
            if binding["target"]["kind"] != "global" or binding["slot"] != "decoration" or binding["mode"] != "Provide":
                continue
            for root in material_templates(frame, "decoration", binding["resource"]):
                if root["id"] in self.handled_roots or ".graph." in root["id"]:
                    continue
                self.draw_tree(frame, root)
        self.merge(progress)

    def bga(self, frame: Contribution):
        values = {}
        for contribution in [self.author.base(), frame] + ([self.author.contribution("bga-frame", self.options)] if "bga-frame" in self.author.modules else []):
            values.update(bms_sections(contribution.ini).get(self.keymode, {}))
        key = {"p1": "BgaViewportsP1", "p2": "BgaViewportsP2", "dp": "BgaViewports"}[self.presentation]
        parts = values.get(key, values.get("BgaViewports", "")).split(";")
        if not parts[0]:
            raise ValueError("The selected source does not declare its BGA viewport")
        self.skin["bga"] = {"id": "native.bga"}
        for part in parts:
            x, y, w, h, fit = part.strip().split(",")
            if fit != "fit":
                raise ValueError("Only the source's contain-fit BGA is mapped")
            self.destination("native.bga", (float(x) * self.w, float(y) * self.h, float(w) * self.w, float(h) * self.h), stretch=1)

    def lanes(self, frame: Contribution):
        layout = layout_for(frame, self.keymode, self.presentation)
        lanes = []
        for group, stage in enumerate(layout["stages"]):
            sx, sy, sw, sh = stage["rect"]
            # Author lane arrays are logical, not sorted by on-screen X. Both
            # SP sides put scratch at index 0; only DP's second deck uses 7.
            scratch_first = self.presentation != "dp" or group == 0
            order = [*range(1, 8), 0] if scratch_first else list(range(8))
            for i in order:
                lx, lw = stage["lanes"][i]
                lanes.append({**native_rect(((sx + lx * sw) * self.w, sy * self.h, lw * sw * self.w, sh * self.h), self.h)})
        return layout, lanes

    def notes(self, frame: Contribution, cap_height=None):
        from beatoraja_materials import collect_note_materials
        recipe = collect_note_materials(self.author, self.options, self.keymode, self.presentation, self.sink.add_source)
        self.gaps.extend(g for g in recipe.get("gaps", []) if g.get("code") != "shared-note-height-needs-padding")
        images = {item["id"]: item for item in recipe["images"]}
        note = recipe["note"]
        # Transparent top padding preserves the family's original note pixels.
        # Native LaneRenderer uses a shared lane height for all five cap fields.
        cap_fields = ("note", "lnstart", "lnend", "hcnstart", "hcnend")
        height = max(images[i]["h"] for key in cap_fields for i in note[key])
        if cap_height is not None:
            if not isinstance(cap_height, int) or cap_height < height:
                raise ValueError("Shared cap height must contain every original cap")
            height = cap_height
        padded: dict[str, str] = {}
        def pixels(image):
            with Image.open(source_path(self.author.root, image["source"])) as png:
                crop = png.convert("RGBA").crop((image["x"], image["y"], image["x"] + image["w"], image["y"] + image["h"]))
            return tint_pixels(crop, image.get("rgba", (1, 1, 1, 1)))
        for key in cap_fields:
            for i, old in enumerate(note[key]):
                if old not in padded:
                    image = images[old]
                    crop = pixels(image)
                    carrier = Image.new("RGBA", (crop.width, height))
                    carrier.paste(crop, (0, height - crop.height))
                    padded[old] = self.image(self.sink.add_raster(carrier))
                note[key][i] = padded[old]
        used_body = {i for key, values in note.items() if key not in cap_fields and isinstance(values, list) for i in values if isinstance(i, str)}
        for old in used_body:
            image = images[old]
            if image.get("rgba", (1, 1, 1, 1)) != (1, 1, 1, 1):
                identifier = self.image(self.sink.add_raster(pixels(image)))
                for key in note:
                    if key not in cap_fields and isinstance(note[key], list):
                        note[key] = [identifier if value == old else value for value in note[key]]
            else:
                self.skin["image"].append({k: v for k, v in image.items() if k not in ("source", "rgba")})
        if any("rgba" in image for image in images.values()):
            self.gap("native-note-brightness-raster", detail="Original note pixels receive the selected source RGB brightness once in an 8-bit derivative because native NoteSet ignores destination colour; source alpha, dimensions and transparent cap padding remain unchanged. This is not a GPU pixel-equivalence claim.")
        layout, lanes = self.lanes(frame)
        note.update({"id": "native.notes", "dst": lanes, "size": [height] * len(lanes), "expansionrate": [100, 100]})
        # IIDX has no mines. Add an explicit, conspicuous BMS hazard carrier;
        # leaving mine[] empty crashes the official native SkinNote constructor.
        mine = Image.new("RGBA", (32, height))
        for yy in range(height - 8, height):
            for xx in range(32):
                mine.putpixel((xx, yy), (255, 240, 240, 255) if (xx + yy) % 8 < 2 else (220, 20, 35, 255))
        mine_id = self.image(self.sink.add_raster(mine))
        note["mine"] = [mine_id] * len(lanes)
        self.gap("native BMS mine addition", detail="Red/white hazard stripe, outside the retained IIDX source art")
        from beatoraja_barline import export_barline
        barline = export_barline(self.author, self.options, self.keymode, self.presentation, frame, self.sink, self.canvas)
        self.skin["image"].extend(barline["image"])
        self.gaps.extend(barline["gaps"])
        note["group"] = barline["group"]
        self.skin["note"] = note
        self.skin["destination"].append({"id": "native.notes"})
        return layout

    def covers(self, frame: Contribution, kinds=("sudden", "hidden", "lift")):
        from beatoraja_covers import export_covers
        self.merge(export_covers(self.author, self.options, self.keymode, self.presentation,
                                 self.sink, self.canvas, self.baked, kinds))

    def full_combo(self, frame: Contribution):
        contribution = self.author.contribution("full-combo", self.options)
        markers = [b["resource"] for b in material_bindings(frame, self.keymode, self.presentation)
                   if b["target"]["kind"] == "global" and b["slot"] == "decoration" and b["mode"] == "Provide"]
        self.context["gameplay.full-combo"] = True
        for marker in markers:
            for root in material_templates(contribution, "decoration", marker):
                self.draw_tree(contribution, root, timer=48)
        self.context.pop("gameplay.full-combo")

    def lane_materials(self, contribution: Contribution, frame: Contribution, slot: str):
        layout = layout_for(frame, self.keymode, self.presentation)
        bindings = [b for b in material_bindings(contribution, self.keymode, self.presentation)
                    if b["target"]["kind"] == "lane" and b["slot"] == slot and b["mode"] == "Provide"]
        for group, stage in enumerate(layout["stages"]):
            members = [b for b in bindings if int(b["target"]["group-logical"]) == group]
            scratch = [b for b in members if b["target"]["lane"].startswith("bms.lane.scratch-")]
            keys = sorted((b for b in members if b not in scratch), key=lambda b: int(b["target"]["group-local-logical"]))
            sx, sy, sw, sh = stage["rect"]
            for index, binding in enumerate([*keys, *scratch]):
                logical = int(binding["target"]["group-local-logical"])
                lx, lw = stage["lanes"][logical]
                kx, kw = stage["keys"][logical]
                lane = ((sx + lx * sw) * self.w, sy * self.h, lw * sw * self.w, sh * self.h)
                key = ((sx + kx * sw) * self.w, (sy + sh) * self.h, kw * sw * self.w, layout["keyAreaHeight"] * self.h)
                hit_height = 16 * self.h / 768
                hit = (lane[0], lane[1] + lane[3] - hit_height, lane[2], hit_height)
                yield binding, group, (0 if index == 7 else index + 1), lane, key, hit

    def key_beams(self, frame: Contribution):
        contribution = self.author.contribution("key-beam", self.options)
        for binding, group, lane_number, lane, _, _ in self.lane_materials(contribution, frame, "playfield.lane-surface"):
            for root in material_templates(contribution, binding["slot"], binding["resource"]):
                self.draw_tree(contribution, root, lane, timer=100 + group * 10 + lane_number)

    def key_lights(self, frame: Contribution):
        self.context["input.pressed"] = True
        for binding, group, lane_number, _, key, _ in self.lane_materials(frame, frame, "playfield.key"):
            for root in material_templates(frame, binding["slot"], binding["resource"]):
                self.draw_tree(frame, root, key, timer=100 + group * 10 + lane_number)
        self.context.pop("input.pressed")

    def explosions(self, frame: Contribution):
        contribution = self.author.contribution("explosion", self.options)
        for binding, group, lane_number, _, _, hit in self.lane_materials(contribution, frame, "effect.hit-explosion"):
            for root in material_templates(contribution, binding["slot"], binding["resource"]):
                self.draw_tree(contribution, root, hit, timer=50 + group * 10 + lane_number, offsets=[3])

    def long_note_effects(self, frame: Contribution):
        from beatoraja_hold import hold_plan
        contribution = self.author.contribution("charge-notes", self.options)
        self.feedback(hold_plan(self.author, contribution, self.options, self.presentation,
                                self.lane_materials(contribution, frame, "effect.long-note")))

    def hit_line(self, frame: Contribution):
        layout = layout_for(frame, self.keymode, self.presentation)
        values = bms_sections(frame.ini)[self.keymode]
        height = float(values.get("HitTargetLineHeight", 3)) * self.h / 768
        for binding in material_bindings(frame, self.keymode, self.presentation):
            if binding["slot"] != "playfield.judgement-line" or binding["mode"] != "Provide":
                continue
            sx, sy, sw, sh = layout["stages"][int(binding["target"]["group-logical"])]["rect"]
            identifier = self.image(self.sink.add_source(binding["resource"] + ".png"))
            self.destination(identifier, (sx * self.w, (sy + sh) * self.h - height, sw * self.w, height), offsets=[3])

    def event_clips(self, frame: Contribution):
        roots = [frame.scene["root"]]
        for binding in material_bindings(frame, self.keymode, self.presentation):
            if binding["target"]["kind"] == "global" and binding["slot"] == "decoration" and binding["mode"] == "Provide":
                roots.extend(material_templates(frame, binding["slot"], binding["resource"]))
        definitions = {a["id"]: a for a in frame.manifest.get("animations", [])}
        seen = set()

        def visit(node, parent, inherited):
            properties, _ = scene_properties(node, frame.scene, self.context)
            rect, rgba = node_rect(properties, parent), colour(properties, inherited)
            if node["type"] == "recorded-animation" and node["id"] not in seen:
                animation = node["animation"]
                source = self.author.json(definitions[animation]["path"])
                is_ready = "ready" in animation
                if is_ready or source.get("trigger") == "gameplay.fail":
                    if is_ready and not self.options.get("ready-animation", self.options.get("get-ready", True)):
                        return
                    seen.add(node["id"])
                    duration = (len(source["frames"]) - 1) * 1000 / source["fps"]
                    if not is_ready:
                        self.skin["close"] = max(self.skin["close"], round(duration + 200))
                    self.draw_baked(frame.manifest_path, animation, rect, rgba, timer=READY_TIMER if is_ready else 3, start_frame=0)
            for child in node.get("children", []):
                visit(child, rect, rgba)

        for root in roots:
            visit(root, self.canvas, (1, 1, 1, 1))

    def finish(self):
        wanted = set()
        def visit(value):
            if isinstance(value, dict):
                if "src" in value:
                    wanted.add(value["src"])
                for item in value.values():
                    visit(item)
            elif isinstance(value, list):
                for item in value:
                    visit(item)
        visit(self.skin)
        self.skin["source"] = [self.sink.source[key] for key in sorted(wanted)]
        return self.skin


def read_bakes(paths: list[Path], author: AuthorPackage, overrides: list[Path] = ()):
    result = {}
    for root in paths:
        for path in root.rglob("index.json"):
            index = read_json(path)
            if index.get("contract") != "oms-iidx-baked-animation.v1":
                continue
            if Path(index["source"]["authorRoot"]).resolve() != author.root:
                continue
            if not index.get("verification", {}).get("passed"):
                raise ValueError("Unverified bake index")
            key = (index["source"]["manifest"], index["source"]["animation"])
            # A completed sequence supersedes an exploratory single frame.
            def rank(item):
                return (bool(item["selectionComplete"]), -item.get("optimization", {}).get("decodedPixels", sum(p["width"] * p["height"] for p in item["pages"])))
            if key not in result or rank(index) > rank(result[key][1]):
                result[key] = path, index
    for path in overrides:
        index = read_json(path)
        if (index.get("contract") != "oms-iidx-baked-animation.v1"
                or Path(index["source"]["authorRoot"]).resolve() != author.root
                or not index.get("verification", {}).get("passed")):
            raise ValueError("Bake override must be verified and belong to the selected author")
        result[(index["source"]["manifest"], index["source"]["animation"])] = path, index
    digests = {}
    for _, index in result.values():
        for relative, expected in index["source"]["files"].items():
            if relative not in digests:
                digests[relative] = hashlib.sha256(source_path(author.root, relative).read_bytes()).hexdigest()
            if digests[relative].lower() != expected.lower():
                raise ValueError("Stale bake source: " + relative)
    return result


def export(author: AuthorPackage, output: Path, modes: list[str], frames: list[str], bake_roots: list[Path], native_font: Path | None = None, bake_overrides: list[Path] = ()):
    if output.exists():
        raise ValueError("Export requires a fresh output directory")
    output.mkdir(parents=True)
    sink = AssetSink(author.root, output)
    native_font_path = sink.add_native_font(native_font) if native_font else None
    baked = read_bakes(bake_roots, author, bake_overrides)
    variants = output / "variants"
    variants.mkdir()
    receipt = {"contract": "oms-beatoraja-export.v1", "name": author.name, "family": author.family,
               "sourceManifestSha256": hashlib.sha256((author.root / "gameplay-skin.json").read_bytes()).hexdigest(),
               "variants": []}
    for mode in modes:
        for theme in frames:
            keymode, _ = MODE[mode]
            options = author.options(keymode, mode, {"frame": theme})
            composer = Composer(author, sink, options, mode, baked)
            frame = author.contribution("frame", options)
            composer.preparation(frame)
            composer.frame(frame)
            composer.bga(frame)
            composer.bga_frame(frame)
            composer.graph()
            composer.decorations(frame)
            composer.turntable(frame)
            composer.lane_opening(frame)
            composer.key_beams(frame)
            composer.hit_line(frame)
            composer.notes(frame)
            composer.explosions(frame)
            composer.long_note_effects(frame)
            composer.key_lights(frame)
            composer.covers(frame)
            composer.reading(frame)
            composer.widgets(frame)
            composer.timing_display(frame)
            composer.hud(frame, native_font_path)
            composer.full_combo(frame)
            composer.event_clips(frame)
            skin = composer.finish()
            filename = theme + "_" + mode
            (variants / (filename + ".lua")).write_text(ANIMATION_RUNTIME + "\nreturn " + lua(skin) + "\n", encoding="utf-8")
            receipt["variants"].append({"frame": theme, "mode": mode, "gaps": composer.gaps,
                                        "images": len(skin["image"]), "sources": len(skin["source"])})
    for keymode, file, relevant in (("7k", "play7.luaskin", [m for m in modes if m != "dp"]), ("14k", "play14.luaskin", [m for m in modes if m == "dp"])):
        if not relevant:
            continue
        properties = []
        if keymode == "7k":
            properties.append({"name": "Play Side", "item": [{"name": m.upper(), "op": 920 if m == "p1" else 921} for m in relevant], "def": relevant[0].upper()})
        properties.append({"name": "Frame", "item": [{"name": next(c["label"] for c in author.modules["frame"]["choices"] if c["value"] == f), "op": 1000 + i} for i, f in enumerate(frames)]})
        header = {"type": 0 if keymode == "7k" else 2, "name": author.name, "author": "ZDaMexy", "w": 1280 if author.family == "standard" else 1920, "h": 720 if author.family == "standard" else 1080, "property": properties}
        script = "local header = " + lua(header) + "\nif not skin_config then return header end\n"
        script += "local frames = " + lua(frames) + "\nlocal frame = frames[(skin_config.option['Frame'] or 1000) - 999]\n"
        script += "local side = " + ("'dp'" if keymode == "14k" else "(skin_config.option['Play Side'] == 921 and 'p2' or 'p1')") + "\n"
        script += "local skin = require('variants.' .. frame .. '_' .. side)\nskin.property = header.property\nreturn skin\n"
        (output / file).write_text(script, encoding="utf-8")
    receipt["assets"] = sink.provenance
    (output / "export-receipt.json").write_text(json.dumps(receipt, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return receipt


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--author", type=Path, required=True)
    parser.add_argument("--family", choices=("standard", "lightning"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--modes", default="p1,p2,dp")
    parser.add_argument("--frames", help="Comma-separated frozen frame values; defaults to the source default")
    parser.add_argument("--bake-root", type=Path, action="append", default=[])
    parser.add_argument("--bake-override", type=Path, action="append", default=[], help="Verified index.json to use for an explicit resource/performance comparison")
    parser.add_argument("--native-font", type=Path, help="Explicit local text font; not IIDX artwork")
    args = parser.parse_args()
    author = AuthorPackage(args.author, args.family)
    receipt = export(author, args.output, args.modes.split(","), (args.frames or author.modules["frame"]["default"]).split(","), args.bake_root, args.native_font, args.bake_override)
    print(json.dumps({"name": receipt["name"], "variants": len(receipt["variants"]), "assets": len(receipt["assets"]),
                      "unresolved": sum(len(v["gaps"]) for v in receipt["variants"])}))


if __name__ == "__main__":
    main()
