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
from pathlib import Path
from typing import Any

from PIL import Image

from beatoraja_author import (AuthorPackage, Contribution, bms_sections,
                             layout_for, material_bindings, material_templates,
                             read_json, source_path, walk_nodes)
from beatoraja_native import (AssetSink, Lua, all_of, colour, lua, native_colour,
                             native_rect, node_rect, scene_properties)


MODE = {"p1": ("7k", 0), "p2": ("7k", 0), "dp": ("14k", 2)}


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
                        "preparation.available": False, "preparation.active": False,
                        "preparation.ready": False, "scroll.available": True,
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

    def destination(self, identifier: str, rect, *, rgba=(1, 1, 1, 1), blend="alpha", draw=None, **extra):
        destination = {"id": identifier, "filter": 0, "blend": 2 if blend == "additive" else 1,
                       "dst": [{**native_rect(rect, self.h), **native_colour(rgba)}], **extra}
        if draw:
            destination["draw"] = draw
        self.skin["destination"].append(destination)
        return destination

    def merge(self, recipe: dict[str, Any]):
        for key in ("font", "image", "imageset", "value", "floatvalue", "text", "graph", "judge", "destination"):
            self.skin.setdefault(key, []).extend(recipe.get(key, []))
        self.gaps.extend(recipe.get("gaps", []))

    def hud(self, frame: Contribution, native_font_path: str | None):
        from beatoraja_hud import export_hud
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
                                      fallback_contributions=(self.author.base(),)))
        self.skin["font"] = list({item["id"]: item for item in self.skin["font"]}.values())

    def graph(self):
        from beatoraja_graph import export_graph
        result = export_graph(self.author, self.options, self.keymode, self.presentation, self.sink, self.canvas)
        self.handled_roots.update(result["handled_root_ids"])
        self.merge({**result["arrays"], "destination": result["destination"], "gaps": result["gaps"]})

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
            gauge = collect_gauge_materials(self.author, self.options, self.keymode, self.presentation, self.sink.add_source)
            self.merge(compose_gauges(gauge, self.sink, gauges, self.h))

    def draw_tree(self, contribution: Contribution, root: dict[str, Any], parent=None,
                  rgba=(1, 1, 1, 1), blend="alpha", timer=0, offsets=None):
        properties, dynamic = scene_properties(root, contribution.scene, self.context)
        if properties.get("visible", True) is False:
            return
        if any(d["property"] == "visible" for d in dynamic):
            self.gap("unmapped visibility", node=root["id"], sources=[d["source"] for d in dynamic if d["property"] == "visible"])
            return
        if root["type"] != "text" and any(d["property"] == "resource" for d in dynamic):
            self.gap("unmapped resource state", node=root["id"], sources=[d["source"] for d in dynamic if d["property"] == "resource"])
            return
        rect = node_rect(properties, parent or self.canvas)
        rgba = colour(properties, rgba)
        if rgba[3] <= 0:
            return
        blend = root.get("blend", "inherit") if root.get("blend", "inherit") != "inherit" else blend
        if properties.get("rotation", 0) or root.get("effects"):
            self.gap("node transform/effects", node=root["id"])
            return
        kind = root["type"]
        if kind == "sprite":
            resource = contribution.resources[properties["resource"]]
            identifier = self.image(self.sink.add_source(resource["path"]))
            self.destination(identifier, rect, rgba=rgba, blend=blend,
                             **({"timer": timer} if timer else {}), **({"offsets": offsets} if offsets else {}))
        elif kind == "recorded-animation":
            fixed_time = constant_clock(contribution.scene, root.get("clock"))
            self.draw_baked(contribution.manifest_path, root["animation"], rect, rgba,
                            timer=timer, offsets=offsets, static_time_ms=fixed_time)
        elif kind == "container":
            for child in sorted(root.get("children", []), key=lambda n: -n.get("properties", {}).get("z", 0)):
                self.draw_tree(contribution, child, rect, rgba, blend, timer, offsets)
        elif kind == "text":
            # Text is emitted separately by the native HUD adapter.
            return
        else:
            self.gap("unmapped scene primitive", node=root["id"], kind=kind)

    def draw_baked(self, manifest: str, animation: str, rect, rgba=(1, 1, 1, 1), *, timer=0, start_frame=None, offsets=None, static_time_ms=None):
        key = (manifest, animation)
        if key not in self.baked:
            self.gap("recorded animation needs bake", manifest=manifest, animation=animation)
            return
        path, index = self.baked[key]
        if index.get("selectionComplete") and static_time_ms is None:
            self.draw_baked_sequence(path, index, rect, rgba, timer=timer, start_frame=start_frame, offsets=offsets)
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
                             rgba=rgba, blend=run["blend"], **({"offsets": offsets} if offsets else {}))
        if static_time_ms is None:
            self.gap("steady animation snapshot", animation=animation, sourceFrame=frame["sourceFrame"])

    def draw_baked_sequence(self, path, index, rect, rgba, *, timer, start_frame, offsets=None):
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
                             rgba=rgba, blend=blend, **({"offsets": offsets} if offsets else {}))

    def frame(self, contribution: Contribution):
        for binding in material_bindings(contribution, self.keymode, self.presentation):
            if binding["target"]["kind"] == "global" and binding["slot"] == "stage.background" and binding["mode"] == "Provide":
                for root in material_templates(contribution, binding["slot"], binding["resource"]):
                    self.draw_tree(contribution, root)

    def decorations(self, frame: Contribution):
        for binding in material_bindings(frame, self.keymode, self.presentation):
            if binding["target"]["kind"] != "global" or binding["slot"] != "decoration" or binding["mode"] != "Provide":
                continue
            for root in material_templates(frame, "decoration", binding["resource"]):
                if root["id"] in self.handled_roots or ".graph." in root["id"]:
                    continue
                self.draw_tree(frame, root)

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
        self.gaps.extend(recipe.get("gaps", []))
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
        for key in cap_fields:
            for i, old in enumerate(note[key]):
                if old not in padded:
                    image = images[old]
                    with Image.open(source_path(self.author.root, image["source"])) as png:
                        crop = png.convert("RGBA").crop((image["x"], image["y"], image["x"] + image["w"], image["y"] + image["h"]))
                    carrier = Image.new("RGBA", (crop.width, height))
                    carrier.paste(crop, (0, height - crop.height))
                    padded[old] = self.image(self.sink.add_raster(carrier))
                note[key][i] = padded[old]
        used_body = {i for key, values in note.items() if key not in cap_fields and isinstance(values, list) for i in values if isinstance(i, str)}
        self.skin["image"].extend({k: v for k, v in images[i].items() if k != "source"} for i in used_body)
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
        line_id = self.image(self.sink.add_raster(Image.new("RGBA", (1, 1), (255, 255, 255, 80))))
        note["group"] = []
        for stage in layout["stages"]:
            sx, sy, sw, sh = stage["rect"]
            note["group"].append({"id": line_id, "dst": [{"x": round(sx * self.w), "y": round(self.h * (1 - sy - sh)), "w": round(sw * self.w), "h": 1}]})
        self.skin["note"] = note
        self.skin["destination"].append({"id": "native.notes"})
        return layout

    def covers(self, frame: Contribution, kinds=("sudden", "hidden", "lift")):
        layout = layout_for(frame, self.keymode, self.presentation)
        for kind in kinds:
            contribution = self.author.contribution("lift-art" if kind == "lift" else "lane-cover", self.options)
            slot = "playfield.lift.fill" if kind == "lift" else "playfield.lane-cover.fill"
            for binding in material_bindings(contribution, self.keymode, self.presentation):
                if binding["slot"] != slot or binding["mode"] != "Provide":
                    continue
                stage = layout["stages"][int(binding["target"]["group-logical"])]["rect"]
                x, y, w, h = (stage[i] * (self.w if i % 2 == 0 else self.h) for i in range(4))
                baseline = self.h - y - h

                def visit(node, parent, inherited):
                    properties, dynamic = scene_properties(node, contribution.scene, self.context)
                    if properties.get("visible", True) is False:
                        return
                    if dynamic or node.get("effects"):
                        raise ValueError("Cover requires an explicit dynamic/effect adapter")
                    rect, rgba = node_rect(properties, parent), colour(properties, inherited)
                    if node["type"] == "sprite":
                        src = self.sink.add_source(contribution.resources[properties["resource"]]["path"])
                        width, height = self.sink.dimensions[src]
                        identifier = self.identifier(kind)
                        image = {"id": identifier, "src": src, "x": 0, "y": 0, "w": width, "h": height}
                        if kind == "sudden":
                            self.skin.setdefault("slider", []).append({**image, "angle": 2, "range": round(h), "type": 4, "changeable": False})
                        else:
                            self.skin.setdefault(kind + "Cover", []).append({**image, "disapearLine": round(baseline), "isDisapearLineLinkLift": kind == "hidden"})
                        self.destination(identifier, rect, rgba=rgba, op=[{"sudden": 271, "hidden": 273, "lift": 272}[kind]])
                    elif node["type"] != "container":
                        raise ValueError("Cover needs a declared sprite/container source")
                    for child in node.get("children", []):
                        visit(child, rect, rgba)

                for root in material_templates(contribution, slot, binding["resource"]):
                    visit(root, (x, y - h if kind == "sudden" else y + h, w, h), (1, 1, 1, 1))

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
                    field = "playstart" if is_ready else "close"
                    self.skin[field] = max(self.skin[field], round(duration + 200))
                    self.draw_baked(frame.manifest_path, animation, rect, rgba, timer=40 if is_ready else 3, start_frame=0)
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
            for name in ("frame-opening", "entrance-animation"):
                if name in options:
                    options[name] = False
            composer = Composer(author, sink, options, mode, baked)
            frame = author.contribution("frame", options)
            composer.frame(frame)
            composer.bga(frame)
            composer.graph()
            composer.decorations(frame)
            composer.key_beams(frame)
            composer.hit_line(frame)
            composer.notes(frame)
            composer.explosions(frame)
            composer.key_lights(frame)
            composer.covers(frame)
            composer.widgets(frame)
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
