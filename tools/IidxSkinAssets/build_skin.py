"""Create a local IIDX 33 gameplay author package from independently extracted art.

The source HDD is never an input to this command. Only an existing art library and
the recorded AFP layout evidence are consumed. Generated art and OSKs stay local.
"""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import re
import shutil
import struct
import sys
import xml.etree.ElementTree as ET
import zipfile

sys.dont_write_bytecode = True

WIDTH, HEIGHT = 1920, 1080
# Source-pixel sizes/offsets use the game's 768-unit height; glyph metrics remain source pixels.
REFERENCE_FONT_SCALE = 768 / HEIGHT
SP_CENTER_OFFSETS = {"p1": 270, "p2": -270}
FPS = 60
COMMON = "[GameplaySkin.Common:1]"
MANIFEST_CONTRACT = "oms-gameplay-skin-manifest.v1"
SCENE_CONTRACT = "oms-gameplay-skin-scene.v1"


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value, compact=False):
    path.write_text(json.dumps(value, ensure_ascii=False, indent=None if compact else 2,
                               separators=(",", ":") if compact else None) + "\n", encoding="utf-8")


def within(root, relative):
    path = (root / relative).resolve(strict=True)
    if not path.is_relative_to(root.resolve(strict=True)) or not path.is_file():
        raise ValueError(f"Resource escapes its library: {relative}")
    return path


def stable(value):
    segments = re.sub(r"[^a-z0-9.-]", "-", value.lower()).split(".")
    segments = [segment.strip("-") for segment in segments]
    if any(not segment for segment in segments):
        raise ValueError(f"Empty identity segment: {value}")
    return ".".join(segment if segment[0].isalpha() else "n" + segment for segment in segments)


def node(identifier, kind="container", properties=None, resource=None, slot=None, blend="alpha", children=None):
    value = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": blend,
             "properties": properties or {}, "effects": [], "children": children or []}
    if resource is not None:
        value["resource"] = resource
    if slot is not None:
        value["slot"] = slot
    return value


def union(rectangles):
    rectangles = list(rectangles)
    if not rectangles:
        raise ValueError("The source layout has no requested component")
    return {"left": min(r["left"] for r in rectangles), "top": min(r["top"] for r in rectangles),
            "right": max(r["right"] for r in rectangles), "bottom": max(r["bottom"] for r in rectangles)}


def geometry(bounds, surface=(0, 0, WIDTH, HEIGHT)):
    x, y, width, height = surface
    return {"x": (bounds["left"] - x) / width, "y": (bounds["top"] - y) / height,
            "width": (bounds["right"] - bounds["left"]) / width,
            "height": (bounds["bottom"] - bounds["top"]) / height}


def shifted_bounds(bounds, dx):
    return {**bounds, "left": bounds["left"] + dx, "right": bounds["right"] + dx}


def viewport_configuration(bounds):
    return ",".join(f"{value:.12g}" for value in (
        bounds["left"] / WIDTH, bounds["top"] / HEIGHT,
        (bounds["right"] - bounds["left"]) / WIDTH, (bounds["bottom"] - bounds["top"]) / HEIGHT)) + ",fit"


def rendered_float_value(value):
    """Shortest JSON numbers which preserve every final float32 renderer bit.

    Track times deliberately bypass this function. The C5 ordinary properties and
    projective vertex/shader inputs cast these values to float32 before drawing.
    """
    if isinstance(value, dict):
        return {key: rendered_float_value(item) for key, item in value.items()}
    if isinstance(value, list):
        return [rendered_float_value(item) for item in value]
    if not isinstance(value, (int, float)) or isinstance(value, bool):
        return value
    bits = struct.pack("<f", value)
    rounded = struct.unpack("<f", bits)[0]
    for digits in range(1, 10):
        short = float(format(rounded, f".{digits}g"))
        if struct.pack("<f", short) == bits:
            return int(short) if short.is_integer() and bits != b"\0\0\0\x80" else short
    raise ValueError("No exact float32 JSON representation")


class Builder:
    def __init__(self, library, research, fullcombo_research, output, image_class):
        self.library, self.research, self.output, self.Image = library, research, output, image_class
        self.map = read_json(research / "skin-reference-map.json")
        self.gameplay = read_json(research / "gameplay-assets.json")
        self.fullcombo = read_json(within(fullcombo_research, "fcombo00-author-data.json"))
        if (self.fullcombo["schemaVersion"] != 1 or self.fullcombo["fps"] != FPS
                or self.fullcombo["durationFrames"] != 150 or self.fullcombo["durationMs"] != 2500
                or not self.fullcombo["last30FramesTransparent"]):
            raise ValueError("The full-combo source requires a different timing consumer")
        self.source_libraries = {"primary": library, "extra": research / "extra-note-library"}
        self.resources, self.resource_paths, self.fonts = [], {}, []
        self.templates, self.instances, self.bindings, self.variants, self.tracks, self.machines = [], [], [], [], [], []
        self.ini = ["// Local IIDX 33 study. Original art is not part of the OMS distribution.",
                    "[General]", "Name: IIDX 33 Sparkle Shower · local study", "Author: local IIDX study", "Version: 2.7", ""]
        self.material_ini = []
        self.used_source_assets = []
        self.calibration_receipts = []
        self.fullcombo_track_index = 0
        self.fullcombo_key_index = 0
        self.source_index = {}
        self.source_sampler_cache = {}
        for group, root in self.source_libraries.items():
            data = read_json(root / "library.json")
            for item in data["textures"]:
                package = item["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs")
                self.source_index[(group, package, Path(item["name"]).stem)] = item
        self.empty = self.generated("common/empty.png", self.Image.new("RGBA", (1, 1)))

    def register(self, relative):
        relative = Path(relative).as_posix()
        if relative in self.resource_paths:
            return self.resource_paths[relative]
        identifier = "iidx33." + stable(relative.removesuffix(".png").replace("/", "."))
        if len(identifier) > 128 or any(r["id"] == identifier for r in self.resources):
            raise ValueError(f"Non-unique or oversized resource identifier: {relative}")
        self.resources.append({"id": identifier, "type": "texture", "path": relative})
        self.resource_paths[relative] = identifier
        return identifier

    def generated(self, relative, image):
        target = self.output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        return self.register(relative)

    def asset(self, package, name, group="primary"):
        item = self.source_index[(group, package, name)]
        sampler = self.source_sampler(group, package, name, item)
        relative = f"art/{group}/{package}/{name}.png"
        target = self.output / relative
        if not target.exists():
            source = within(self.source_libraries[group], item["sprite"])
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, target)
            self.used_source_assets.append({"root": group, "package": package, "texture": name,
                                           "width": item["width"], "height": item["height"], "sampler": sampler})
        identifier = self.register(relative)
        next(resource for resource in self.resources if resource["id"] == identifier)["filter"] = sampler["mag"]
        return identifier

    def source_sampler(self, group, package, name, item):
        key = (group, package)
        if key not in self.source_sampler_cache:
            relative = Path(item["sprite"]).parent.parent / "raw/tex/texturelist.xml"
            texturelist = within(self.source_libraries[group], relative)
            original = texturelist.read_bytes()
            xml = ET.fromstring(original)
            self.source_sampler_cache[key] = {
                image.attrib["name"]: {"atlas": atlas.attrib["name"], "mag": atlas.attrib["mag_filter"],
                    "min": atlas.attrib["min_filter"], "wrapS": atlas.attrib["wrap_s"], "wrapT": atlas.attrib["wrap_t"]}
                for atlas in xml.findall("texture") for image in atlas.findall("image")}
            self.calibration_receipts.append({"change": "original-texture-sampler", "root": group, "package": package,
                "sourceTextureList": relative.as_posix(), "sourceTextureListSha256": hashlib.sha256(original).hexdigest(),
                "samplers": [{"atlas": atlas.attrib["name"], "mag": atlas.attrib["mag_filter"],
                    "min": atlas.attrib["min_filter"], "wrapS": atlas.attrib["wrap_s"], "wrapT": atlas.attrib["wrap_t"]}
                    for atlas in xml.findall("texture")],
                "scope": "Each referenced original image consumes its own declared filter; generated textures are unchanged."})
        sampler = self.source_sampler_cache[key][name]
        if (sampler["mag"], sampler["min"]) not in {("nearest", "nearest"), ("linear", "linear")}:
            raise ValueError(f"The source texture requires a different sampler consumer: {group}/{package}/{name}")
        return sampler

    def copy_marker(self, name, source_resource=None):
        source = next(r["path"] for r in self.resources if r["id"] == (source_resource or self.empty))
        relative = f"markers/{name}.png"
        target = self.output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(self.output / source, target)
        return relative.removesuffix(".png")

    def template(self, name, root, slot, material):
        identifier = f"iidx33.template.{name}"
        self.templates.append({"id": identifier, "root": root})
        self.instances.append({"id": f"iidx33.instance.{name}", "template": identifier,
                               "material": {"slot": slot, "resource": material}})

    def bind(self, target, prop, source):
        self.bindings.append({"id": f"{target}.bind.{prop}", "target": target, "property": prop, "source": source})

    def variant(self, target, source, default, cases):
        self.variants.append({"id": f"{target}.variant.{stable(source)}", "target": target, "property": "resource",
                              "source": source, "default": default,
                              "cases": [{"id": f"{target}.case.{stable(source)}.{key}", "key": key, "resource": value}
                                        for key, value in cases.items()]})

    def visible_variant(self, target, source, cases):
        self.variants.append({"id": f"{target}.variant.{stable(source)}", "target": target, "property": "visible",
                              "source": source, "default": False,
                              "cases": [{"id": f"{target}.case.{stable(source)}.{key}", "key": key, "value": True}
                                        for key in cases]})

    def current_judgement(self, target):
        self.machines.append({"id": f"{target}.machine", "initial": f"{target}.empty",
                              "states": [{"id": f"{target}.empty", "set": [{"id": f"{target}.hide", "target": target, "property": "opacity", "value": 0}]},
                                         {"id": f"{target}.active", "set": [{"id": f"{target}.show", "target": target, "property": "opacity", "value": 1}]}],
                              "transitions": [{"id": f"{target}.arrive", "from": f"{target}.empty", "to": f"{target}.active", "event": "judgement.hit"}]})

    def font(self, name, package, names, line_height, advance):
        glyphs = []
        for character, texture in names.items():
            resource = self.asset(package, texture)
            glyphs.append({"character": character, "resource": resource,
                           "advance": advance if isinstance(advance, (int, float)) else advance[character]})
        identifier = f"iidx33.font.{name}"
        self.fonts.append({"id": identifier, "lineHeight": line_height, "glyphs": glyphs})
        return identifier

    def fonts_for_numbers(self):
        self.score_font = self.font("score", "gmframe33", {str(i): f"playm_sco{i:02}" for i in range(10)}, 24, 36)
        self.bpm_font = self.font("bpm", "gmframe33", {str(i): f"playm_bpm{i:02}" for i in range(10)}, 28, 36)
        self.small_bpm_font = self.font("bpm-small", "1/gameparts", {str(i): f"playm_bpm_m{i:02}" for i in range(10)}, 18, 25)
        self.hs_font = self.font("hs", "gmframe33", {str(i): f"playm_hs_{i:02}" for i in range(10)}, 19, 27)
        dot_source = within(self.library, self.source_index[("primary", "gmframe33", "playm_hs_dot")]["sprite"])
        with self.Image.open(dot_source) as dot:
            canvas = self.Image.new("RGBA", (8, 19))
            canvas.paste(dot, (0, 13))
        dot_resource = self.generated("font/hs-dot.png", canvas)
        self.fonts[-1]["glyphs"].append({"character": ".", "resource": dot_resource, "advance": 8})
        self.gauge_font = self.font("gauge", "gmframe33", {str(i): f"playm_gg_{i:02}" for i in range(10)}, 28, 38)
        # The original static cabinet already owns its fixed percent symbol.
        self.fonts[-1]["glyphs"].append({"character": "%", "resource": self.empty, "advance": 1})
        self.combo_font = self.font("combo", "judge00", {str(i): f"judge_combo_{i}y" for i in range(10)}, 100, 55)
        self.perfect_combo_font = self.font("combo-blue", "judge00", {str(i): f"judge_combo_{i}b" for i in range(10)}, 100, 55)

    def numeric(self, identifier, source, font, font_size, bounds, surface=(0, 0, WIDTH, HEIGHT), fmt="integer"):
        properties = {**geometry(bounds, surface), "text": "", "font-size": font_size * REFERENCE_FONT_SCALE, "bitmap-font": font,
                      "format": fmt, "alignment": "right", "colour": "#ffffffff"}
        value = node(identifier, "text", properties)
        self.bind(identifier, "text", source)
        return value

    def source_bounds(self, movies, binding_prefix, exclude=()):
        return union(draw["bounds"] for movie in movies for draw in self.map["layouts"][movie]["draws"]
                     if draw["texture"] not in exclude and any(name.startswith(binding_prefix)
                        and (binding_prefix != "bpm_" or name.removeprefix("bpm_").isdigit()) for name in draw["game_bindings"]))

    def fullcombo_track(self, target, prop, frames, convert=lambda value: value):
        identifier = f"f.t{self.fullcombo_track_index}"
        self.fullcombo_track_index += 1
        # The source's last30 frames are transparent. Retain an explicit duration
        # boundary as well, so late attachment and the bounded2.5s outro stay empty.
        if prop in {"opacity", "projective-quad"}:
            terminal = frames[-1]["value"]
            if (terminal if prop == "opacity" else terminal["opacity"]) != 0:
                raise ValueError("A full-combo visual must end transparently")
            frames = [*frames, {"frame": self.fullcombo["durationFrames"], "value": terminal}]
        keyframes = []
        for frame in frames:
            keyframes.append({"id": f"f.k{self.fullcombo_key_index}",
                              "time": frame["frame"] * 1000 / FPS,
                              "value": rendered_float_value(convert(frame["value"]))})
            self.fullcombo_key_index += 1
        track_type = "frame" if prop in {"resource", "projective-quad"} else "tween"
        self.tracks.append({"id": identifier, "type": track_type, "target": target, "property": prop,
                            "easing": "step", "loop": False, "trigger": "gameplay.complete", "keyframes": keyframes})

    def fullcombo_affine_sprite(self, source, identifier, surface, depth):
        x, y, width, height = surface
        initial = {**source["constants"], **{prop: frames[0]["value"] for prop, frames in source["channels"].items()}}
        converters = {"x": lambda value: (value - x) / width, "y": lambda value: (value - y) / height,
                      "width": lambda value: value / width, "height": lambda value: value / height,
                      "rotation": lambda value: value, "opacity": lambda value: value,
                      "texture": lambda value: self.fullcombo_resources[value]}
        scales = {}
        for axis in ("width", "height"):
            convert = converters[axis]
            values = [initial[axis], *[frame["value"] for frame in source["channels"].get(axis, [])]]
            maximum = max(convert(value) for value in values)
            if maximum > 4:
                factor = 2 ** math.ceil(math.log2(maximum / 4))
                if factor > 8:
                    raise ValueError("The original affine sprite exceeds the bounded size/scale representation")
                # This is one leaf sprite. A power-of-two size/scale split keeps
                # the same top-left, rotation, source UV and final float32 extent.
                converters[axis] = lambda value, convert=convert, factor=factor: convert(value) / factor
                scales["scale-x" if axis == "width" else "scale-y"] = factor
        properties = {prop: rendered_float_value(converters[prop](initial[prop])) for prop in converters if prop != "texture"}
        properties.update({**scales, "z": depth, "fill-mode": "stretch", "colour": "#ffffffff"})
        for prop, frames in source["channels"].items():
            self.fullcombo_track(identifier, "resource" if prop == "texture" else prop, frames, converters[prop])
        return node(identifier, "sprite", properties, self.fullcombo_resources[initial["texture"]],
                    blend="additive" if source["blend"] == 8 else "alpha")

    def fullcombo_core(self):
        self.fullcombo_resources = {texture["name"]: self.asset("fcombo00", texture["name"])
                                   for texture in self.fullcombo["textures"]}
        first_texture = self.source_index[("primary", "fcombo00", self.fullcombo["textures"][0]["name"])]
        texturelist_relative = Path(first_texture["sprite"]).parent.parent / "raw/tex/texturelist.xml"
        texturelist = ET.parse(within(self.library, texturelist_relative)).getroot()
        source_filters = {image.attrib["name"]: (texture.attrib["mag_filter"], texture.attrib["min_filter"])
                          for texture in texturelist.findall("texture") for image in texture.findall("image")}
        for name, resource_id in self.fullcombo_resources.items():
            if source_filters[name] != ("nearest", "nearest"):
                raise ValueError("The source full-combo texture needs a different sampler consumer")
            next(resource for resource in self.resources if resource["id"] == resource_id)["filter"] = "nearest"
        source = self.fullcombo["core"]
        if len(source["nodes"]) != 36 or len(self.fullcombo_resources) != 19:
            raise ValueError("The recorded full-combo graph needs a different bounded consumer")
        core_surface = tuple(source["sourceMaskRectangles"]["core"])
        rise_surface = tuple(source["sourceMaskRectangles"]["rise"])
        stage = source["sourceStageSurface"]
        stage_surface = (stage["left"], stage["top"], stage["width"], stage["height"])

        def rectangle(bounds):
            left, top, width, height = bounds
            return {"left": left, "top": top, "right": left + width, "bottom": top + height}

        rise = node("f.rise", "clip", {**geometry(rectangle(rise_surface), core_surface),
                                                "clip-mode": "bounds"}, blend="inherit", children=[])
        core_children = [rise]
        for index, item in enumerate(source["nodes"]):
            if item["maskDepth"] not in (1, 2) or item["blend"] != 8:
                raise ValueError("The full-combo core needs another mask or blend consumer")
            parent, surface = (rise["children"], rise_surface) if item["maskDepth"] == 2 else (core_children, core_surface)
            parent.append(self.fullcombo_affine_sprite(item, f"f.c{index}", surface, -index - 1))
        clip = node("f.core.clip", "clip", {**geometry(rectangle(core_surface), stage_surface),
                                                     "clip-mode": "bounds"}, blend="inherit", children=core_children)
        # All source core layers precede the independent projective streams and text.
        # The native Stage rectangle locates this one shared graph for either deck.
        root = node("f.core", properties={"visible": False, "z": 100}, slot="decoration",
                    blend="inherit", children=[clip])
        self.bind(root["id"], "visible", "gameplay.full-combo")
        self.fullcombo_core_material = self.copy_marker("full-combo-core")
        self.template("fullcombo.core", root, "decoration", self.fullcombo_core_material)
        self.calibration_receipts.append({"change": "full-combo-original-affine-core", "sourceMovie": "1p_fullcombo",
            "sourceStageSurface": stage, "sourceMasks": source["sourceMaskRectangles"],
            "sourceTexturelist": texturelist_relative.as_posix(), "resourceFilter": "nearest",
            "nodes": len(source["nodes"]), "sharedTranslations": source["sharedSourceExactness"],
            "sourceFps": FPS, "playback": "Exact held source frames, no tween smoothing; real gameplay.complete origin",
            "eligibility": "Actual gameplay.full-combo state; no score or last-hit inference",
            "serialization": "Short stable identities and shortest exact final-float32 numbers; original double frame times retained",
            "layering": "Stage decoration at z100, below the global full-combo projective/text owner"})

    def fullcombo_overlay(self, layout):
        expected = {"p1": {"p1"}, "p2": {"p2"}, "dp": {"dp-left", "dp-right"}}[layout]
        movie = {"p1": "1p_fullcombo", "p2": "2p_fullcombo", "dp": "dp_fullcombo"}[layout]
        children = []
        for stream in self.fullcombo["projectiveStreams"]:
            if stream["sourceMovie"] != movie:
                continue
            identifier = f"f.{layout}.q{stream['sourceDepthPath'][-1]}"
            children.append(node(identifier, "projective-sprite", {"z": -100 * stream["sourceDepthPath"][1]},
                                 self.fullcombo_resources[stream["texture"]], blend="additive"))
            frames = [{"frame": stream["hiddenLeadFrame"], "value": {"vertices": [[v["x"], v["y"], v["q"]]
                       for v in stream["frames"][0]["vertices"]], "opacity": 0}}]
            for frame in stream["frames"]:
                frames.append({"frame": frame["frame"], "value": {"vertices": [[v["x"], v["y"], v["q"]]
                              for v in frame["vertices"]], "opacity": frame["opacity"]}})
            frames.append({"frame": stream["hiddenTailFrame"], "value": {**frames[-1]["value"], "opacity": 0}})
            self.fullcombo_track(identifier, "projective-quad", frames)
        for bank in self.fullcombo["texts"]:
            if bank["name"] not in expected:
                continue
            if len(bank["nodes"]) != 6:
                raise ValueError("The source full-combo text needs another bounded consumer")
            for index, item in enumerate(bank["nodes"]):
                if item["maskDepth"] != 0 or item["blend"] not in (0, 8):
                    raise ValueError("The source full-combo text needs another mask or blend consumer")
                source_depth = item["sourceDepthPath"]
                depth = -100 * source_depth[1] - source_depth[-1]
                children.append(self.fullcombo_affine_sprite(item, f"f.{bank['name']}.t{index}",
                                                             (0, 0, WIDTH, HEIGHT), depth))
        root = node(f"f.{layout}.overlay", properties={"visible": False, "z": -100},
                    blend="inherit", children=children)
        self.bind(root["id"], "visible", "gameplay.full-combo")
        self.calibration_receipts.append({"change": "full-combo-original-projective-and-text", "layout": layout,
            "sourceMovie": movie, "sourceFps": FPS, "sourceDurationFrames": 150, "outroMs": 2500,
            "projectiveStreams": sum(stream["sourceMovie"] == movie for stream in self.fullcombo["projectiveStreams"]),
            "sourceVertices": "Original four screen-normalized vertices and focal/distance q; fixed TL/TR/BR/BL UV",
            "projectivePlayback": "Independent additive source-depth streams, exact60FPS step frames and explicit hidden endpoints",
            "textPlayback": "Original pixel textures, original affine transforms and independent source rounding; no mirroring",
            "limitation": "Original executable full-combo appearance and depth still need real-game visual comparison."})
        return root

    def background(self, layout):
        filename = {"p1": "1p_frame", "p2": "2p_frame", "dp": "center_frame_dp_1p"}[layout]
        source = within(self.research, f"static-frames/{filename}-static181.png")
        with self.Image.open(source) as image:
            cabinet = image.convert("RGBA")
        if layout in {"p1", "p2"}:
            with self.Image.open(within(self.research, "static-frames/center_frame_sp-static181.png")) as image:
                centre = image.convert("RGBA")
            # Exact AFP movie geometry identifies the backing which the game replaces.
            for viewport in self.gameplay["native_bga_placeholders"]:
                if viewport["movie"] == "center_frame_sp":
                    b = viewport["bounds"]
                    centre.paste((0, 0, 0, 255), (math.floor(b["left"]), math.floor(b["top"]), math.ceil(b["right"]), math.ceil(b["bottom"])))
                    self.calibration_receipts.append({"change": "native-bga-opaque-black-underlay", "layout": layout,
                        "sourceMovie": viewport["movie"], "sourceInstance": viewport["instance"], "sourceBounds": b,
                        "dx": SP_CENTER_OFFSETS[layout], "appliedBounds": shifted_bounds(b, SP_CENTER_OFFSETS[layout]),
                        "rgba": [0, 0, 0, 255], "rasterBounds": "floor left/top; ceil right/bottom",
                        "layering": "Authored stage.background Underlay; native BGA video remains above it."})
            dx = SP_CENTER_OFFSETS[layout]
            background = self.Image.new("RGBA", (WIDTH, HEIGHT))
            background.alpha_composite(centre, (dx, 0))
            background.alpha_composite(cabinet)
            self.calibration_receipts.append({"change": "sp-centre-frame-placement", "layout": layout,
                "sourceMovie": "center_frame_sp", "source": "static-frames/center_frame_sp-static181.png",
                "dx": dx, "dy": 0, "sideFrame": filename, "composition": "translated centre, then unchanged side frame"})
        else:
            background = cabinet
            for viewport in self.gameplay["native_bga_placeholders"]:
                if viewport["movie"] == "center_frame_dp_1p":
                    b = viewport["bounds"]
                    background.paste((0, 0, 0, 255), (math.floor(b["left"]), math.floor(b["top"]), math.ceil(b["right"]), math.ceil(b["bottom"])))
                    self.calibration_receipts.append({"change": "native-bga-opaque-black-underlay", "layout": layout,
                        "sourceMovie": viewport["movie"], "sourceInstance": viewport["instance"], "sourceBounds": b,
                        "dx": 0, "appliedBounds": b, "rgba": [0, 0, 0, 255],
                        "rasterBounds": "floor left/top; ceil right/bottom",
                        "layering": "Authored stage.background Underlay; native BGA video remains above it."})
        relative = f"backgrounds/{layout}.png"
        resource = self.generated(relative, background)
        self.template(f"{layout}.background", node(f"iidx33.{layout}.background", "sprite", resource=resource,
                                                  slot="stage.background"), "stage.background", relative.removesuffix(".png"))
        background.save(self.output / f"preview-{layout}-static.png")
        return relative.removesuffix(".png")

    def common_lane_consumers(self):
        self.lane_materials = {}
        hit = next(movie for movie in self.map["effect_timelines"] if movie["movie"] == "exp00")
        hit_draw = hit["frames"][0]["draws"][0]
        hit_bounds, hit_stage = hit_draw["bounds"], hit["stage"]
        if any(len(frame["draws"]) > 1 or any(draw["bounds"] != hit_bounds or draw["blend"] != 8
               for draw in frame["draws"]) for frame in hit["frames"]):
            raise ValueError("The recorded exp00 timeline needs a different geometry or blend consumer")
        hit_resources = [self.asset(hit["package"], frame["draws"][0]["texture"]) if frame["draws"] else self.empty
                         for frame in hit["frames"]]
        hit_centre_x = (hit_stage["left"] + hit_stage["right"]) / 2
        hit_centre_y = (hit_stage["top"] + hit_stage["bottom"]) / 2
        for colour, width in [("white", 52), ("black", 40), ("scratch", 90)]:
            note_name = {"white": "notes_white", "black": "notes_black", "scratch": "notes_turn"}[colour]
            note_resource = self.asset("notes00", note_name)
            note_material = next(r["path"] for r in self.resources if r["id"] == note_resource).removesuffix(".png")
            root = node(f"iidx33.note.{colour}", "sprite", {"height": 15 * REFERENCE_FONT_SCALE / 22.5,
                        "y": -5 * REFERENCE_FONT_SCALE / 22.5,
                        "anchor": "bottom-left", "origin": "bottom-left"}, note_resource, "object.note")
            self.template(f"note.{colour}", root, "object.note", note_material)
            values = {"object.note": note_material}
            for part, suffix in [("head", "start"), ("body", "beam0"), ("tail", "end")]:
                texture = f"notes_bss_{suffix}" if colour == "scratch" else f"notes_cn_{colour}_{suffix}"
                resource = self.asset("notes_cn00", texture, "extra")
                material = next(r["path"] for r in self.resources if r["id"] == resource).removesuffix(".png")
                identifier = f"iidx33.long.{colour}.{part}"
                properties = {} if part == "body" else {"height": 24 * REFERENCE_FONT_SCALE / 22.5,
                    "anchor": "bottom-left", "origin": "bottom-left", "y": -5 * REFERENCE_FONT_SCALE / 22.5}
                self.template(f"long.{colour}.{part}", node(identifier, "sprite", properties, resource,
                                                          f"object.long-note.{part}"), f"object.long-note.{part}", material)
                hcn_resource = self.asset("notes_hcn00", texture, "extra")
                if part == "body":
                    active_texture = texture.removesuffix("0") + "1"
                    released_texture = "notes_bss2_beam4" if colour == "scratch" else f"notes_cn2_{colour}_beam4"
                    cn_active = self.asset("notes_cn00", active_texture, "extra")
                    cn_released = self.asset("notes_cn00", released_texture, "extra")
                    self.variant(identifier, "object.long-note-appearance", resource, {
                        "ln": resource, "ln-holding": cn_active, "ln-released": cn_released,
                        "cn": resource, "cn-holding": cn_active, "cn-released": cn_released,
                        "hcn": hcn_resource, "hcn-holding": self.asset("notes_hcn00", active_texture, "extra"),
                        "hcn-released": self.asset("notes_hcn00", released_texture, "extra")})
                else:
                    self.variant(identifier, "object.long-note-mode", resource, {"hcn": hcn_resource, "cn": resource, "ln": resource})
                values[f"object.long-note.{part}"] = material

            self.calibration_receipts.append({"change": "native-note-reference-pixel-height", "colour": colour,
                "sourceNote": f"notes00/{note_name}", "sourceNoteHeightPixels": 15,
                "sourceLongHeadTailHeightPixels": 24, "sourceYOffsetPixels": -5,
                "nativeNoteHeightLogical": 22.5, "referencePixelToLogicalScale": REFERENCE_FONT_SCALE,
                "appliedNoteRelativeHeight": 15 * REFERENCE_FONT_SCALE / 22.5,
                "appliedHeadTailRelativeHeight": 24 * REFERENCE_FONT_SCALE / 22.5,
                "appliedRelativeYOffset": -5 * REFERENCE_FONT_SCALE / 22.5,
                "longBodyGeometry": "Native duration geometry is unchanged; source body templates keep empty properties.",
                "explosionGeometry": "Unchanged; already normalized against its native authored SourceStage size."})

            if colour == "scratch":
                key = self.asset("gmframe33", "usr_turn00")
                key_properties = {"x": 50 / 150, "y": 44 / 120, "width": 47 / 150, "height": 47 / 120}
            else:
                key = self.asset("1/gameparts", f"light_{colour}_key")
                key_properties = {"x": -4 / width, "y": 0 if colour == "white" else -5 / 120,
                                  "width": (60 if colour == "white" else 48) / width,
                                  "height": (90 if colour == "white" else 74) / 120}
            key_material = self.copy_marker(f"key-{colour}", key)
            key_id = f"iidx33.key.{colour}"
            self.template(f"key.{colour}", node(key_id, "sprite", key_properties, key, "playfield.key",
                          "alpha" if colour == "scratch" else "additive"),
                          "playfield.key", key_material)
            if colour != "scratch":
                self.bind(key_id, "visible", "input.pressed")
            else:
                self.calibration_receipts.append({"change": "default-turntable-centre-always-visible",
                    "sourceMovie": "1p_frame", "sourceBinding": "turn_1p", "sourceTexture": "gmframe33/usr_turn00",
                    "sourceBounds": self.source_bounds(["1p_frame"], "turn_1p"), "sourceBlend": 0,
                    "appliedBlend": "alpha", "inputBinding": None,
                    "limit": "No unverified press/release or rotation animation is added."})
            values["playfield.key"] = key_material

            beam = self.asset("beam00", f"playm_bar_{'turn' if colour == 'scratch' else colour}")
            beam_material = self.copy_marker(f"beam-{colour}", beam)
            beam_id = f"iidx33.beam.{colour}"
            self.template(f"beam.{colour}", node(beam_id, "sprite", {"height": 723 / 728}, beam, "decoration", "additive"),
                          "decoration", beam_material)
            self.bind(beam_id, "visible", "input.pressed")
            values["decoration"] = beam_material

            effect_material = self.copy_marker(f"explosion-{colour}", hit_resources[0])
            effect_id = f"iidx33.explosion.{colour}.frame"
            frame = node(effect_id, "sprite", {"x": (width / 2 + hit_bounds["left"] - hit_centre_x) / (width * 4),
                         "y": (22.5 - 5 + hit_bounds["top"] - hit_centre_y) / 90,
                         "width": (hit_bounds["right"] - hit_bounds["left"]) / (width * 4),
                         "height": (hit_bounds["bottom"] - hit_bounds["top"]) / 90}, hit_resources[0], blend="additive")
            root = node(f"iidx33.explosion.{colour}", properties={"width": 4, "height": 4}, slot="effect.hit-explosion", children=[frame])
            self.template(f"explosion.{colour}", root, "effect.hit-explosion", effect_material)
            self.tracks.append({"id": f"iidx33.explosion.{colour}.track", "type": "frame", "target": effect_id,
                                "property": "resource", "easing": "step", "loop": False, "trigger": "judgement.hit",
                                "keyframes": [{"id": f"iidx33.explosion.{colour}.f{i}", "time": timing["time_ms"], "value": resource}
                                              for i, (timing, resource) in enumerate(zip(hit["frames"], hit_resources))]})
            values["effect.hit-explosion"] = effect_material
            self.lane_materials[colour] = values

    def layout_consumers(self, layout):
        dp, p2 = layout == "dp", layout == "p2"
        centre_dx = 0 if dp else SP_CENTER_OFFSETS[layout]
        player = "2p" if p2 else "1p"
        movies = ["center_frame_dp_1p", "dp_frame_1p"] if dp else ["2p_frame" if p2 else "1p_frame", "center_frame_sp"]
        background = self.background(layout)
        information_material = self.copy_marker(f"{layout}-information")
        information_children = []
        for suffix, source, font, size, prefix, fmt in [
            ("score", "score.value", self.score_font, 24, f"score_{player}_", "integer"),
            ("hs", "scroll.speed", self.hs_font, 19, f"hs_{player}_", "fixed-2"),
            ("bpm", "timing.bpm", self.bpm_font, 28, "bpm_", "integer"),
            ("bpm-min", "timing.bpm-min", self.small_bpm_font, 18, "bpm_min_", "integer"),
            ("bpm-max", "timing.bpm-max", self.small_bpm_font, 18, "bpm_max_", "integer")]:
            bounds = self.source_bounds(movies, prefix)
            if suffix == "score":
                score_bindings = {name for movie in movies for draw in self.map["layouts"][movie]["draws"]
                                  for name in draw["game_bindings"] if name.startswith(prefix)}
                digit_count = len(score_bindings)
                if digit_count != 4:
                    raise ValueError(f"The recorded {layout} score needs a different digit-background consumer")
                information_children.append(node(f"iidx33.{layout}.score.zeros", "text", {
                    **geometry(bounds), "text": "0" * digit_count, "font-size": size * REFERENCE_FONT_SCALE,
                    "bitmap-font": font, "alignment": "right", "colour": "#404040ff"}))
                self.calibration_receipts.append({"change": "score-dark-zero-background", "layout": layout,
                    "sourceBindings": sorted(score_bindings), "digitCount": digit_count, "sourceBounds": bounds,
                    "font": font, "fontSize": size * REFERENCE_FONT_SCALE, "colour": "#404040ff",
                    "dynamicLayer": "unchanged right-aligned actual score.value, integer format, white"})
            if not dp and suffix in {"bpm", "bpm-min", "bpm-max"}:
                original_bounds = bounds
                bounds = shifted_bounds(bounds, centre_dx)
                self.calibration_receipts.append({"change": "sp-centre-number-placement", "layout": layout,
                    "node": f"iidx33.{layout}.{suffix}", "sourceMovie": "center_frame_sp", "sourceBounds": original_bounds,
                    "dx": centre_dx, "appliedBounds": bounds})
            information_children.append(self.numeric(f"iidx33.{layout}.{suffix}", source, font, size, bounds, fmt=fmt))
        for suffix, source, y, size in [("title", "song.title", 43, 24), ("artist", "song.artist", 72, 18)]:
            identifier = f"iidx33.{layout}.{suffix}"
            information_children.append(node(identifier, "text", {"x": (610 + centre_dx) / WIDTH, "y": y / HEIGHT, "width": 700 / WIDTH,
                "height": 28 / HEIGHT, "font-size": size * REFERENCE_FONT_SCALE, "text": "", "alignment": "centre", "text-overflow": "ellipsis", "colour": "#ffffffff"}))
            self.bind(identifier, "text", source)
            if not dp:
                self.calibration_receipts.append({"change": "sp-centre-song-information-placement", "layout": layout,
                    "node": identifier, "sourceMovie": "center_frame_sp", "sourceBinding": suffix,
                    "sourceBounds": self.source_bounds(["center_frame_sp"], suffix), "dx": centre_dx,
                    "appliedCentreX": WIDTH / 2 + centre_dx,
                    "limit": "Existing metadata font, text width and vertical placement are retained."})
        information_children.append(self.fullcombo_overlay(layout))
        self.template(f"{layout}.information", node(f"iidx33.{layout}.information", slot="decoration", children=information_children),
                      "decoration", information_material)

        # Stage HUD slots use the outer frame; the four-pixel padding applies only to lane content.
        first_stage_left = 455 if dp else 1409 if p2 else 71
        stage_width = 440
        slot_surface = (first_stage_left, 728 + 120 + 0.006 * HEIGHT, stage_width, 110)
        gauge_draw = next(d for movie in movies for d in self.map["layouts"][movie]["draws"]
                          if any(n == f"gg_bar_{player}" for n in d["game_bindings"]))
        gauge_resource = self.asset(gauge_draw["asset"]["package"], gauge_draw["texture"])
        gauge_modes = {"hard": self.asset("gmframe33", "unit_groove_gauge_exc_r"),
                       "ex-hard": self.asset("gmframe33", "unit_groove_gauge_exc_y"),
                       "hazard": self.asset("gmframe33", "unit_groove_gauge_exc_b")}
        gauge_material = self.copy_marker(f"{layout}-gauge", gauge_resource)
        gauge_geometry = geometry(gauge_draw["bounds"], slot_surface)
        gauge_id = f"iidx33.{layout}.gauge.reveal"
        fill_id = f"{gauge_id}.fill"
        gauge_properties = {**gauge_geometry, "clip-mode": "bounds", "reveal-x": 0}
        fill_properties = {}
        if p2:
            # Existing reveal-x keeps the child canvas at full width. Rotate that mask from its
            # lower-right corner, then rotate the full-size art back so its pixels are not mirrored.
            gauge_properties.update({"x": gauge_geometry["x"] + gauge_geometry["width"],
                                     "y": gauge_geometry["y"] + gauge_geometry["height"], "rotation": 180})
            fill_properties = {"x": 1, "y": 1, "rotation": 180}
            self.calibration_receipts.append({"change": "p2-gauge-right-to-left-reveal", "layout": layout,
                "sourceMovie": "2p_frame", "sourceTexture": gauge_draw["texture"], "sourceBounds": gauge_draw["bounds"],
                "before": {"mask": {**gauge_geometry, "clip-mode": "bounds", "reveal-x": 0}, "fill": {}},
                "after": {"mask": gauge_properties, "fill": fill_properties},
                "contract": "Existing180-degree mask transform and compensating180-degree full-width sprite transform; reveal-x retains its existing full child canvas.",
                "expectedSourceXWindow": "[right - width * gauge.value, right]",
                "invariants": "0 hides the bright layer;100 exposes all original pixels; percentage/dark layer/material identities and P1/DP are unchanged."})
        gauge = node(gauge_id, "clip", gauge_properties,
                     children=[node(fill_id, "sprite", fill_properties, gauge_resource)])
        self.bind(gauge_id, "reveal-x", "gauge.value")
        self.variant(fill_id, "gauge.type", gauge_resource, gauge_modes)
        percent_bounds = self.source_bounds(movies, f"percent_{player}_", exclude=("playm_gg_hyphen",))
        children = [node(f"iidx33.{layout}.gauge.dark", "sprite", {**gauge_geometry, "colour": "#404040ff"}, gauge_resource), gauge,
                    self.numeric(f"iidx33.{layout}.gauge.percent", "gauge.value", self.gauge_font, 28, percent_bounds, slot_surface, "percent")]
        self.variant(f"iidx33.{layout}.gauge.dark", "gauge.type", gauge_resource, gauge_modes)
        self.template(f"{layout}.gauge", node(f"iidx33.{layout}.gauge", slot="hud.gauge", children=children), "hud.gauge", gauge_material)

        judgement_resource = self.asset("judge00", "judge_p_great_blue")
        judgement_material = self.copy_marker(f"{layout}-judgement", judgement_resource)
        judgement_root = f"iidx33.{layout}.judgement"
        judgement_native_centre_y = 728 * 0.81
        judgement_slot_height = 728 * 0.12
        centre_dx = {"p1": -4, "p2": -8, "dp": 0}[layout]
        centre_dy = 0 if dp else 565 - judgement_native_centre_y
        row_properties = {"x": 0.5 + centre_dx / stage_width, "y": 0.5 + centre_dy / judgement_slot_height,
                          "origin": "centre", "layout": "horizontal", "gap-px": -16 * REFERENCE_FONT_SCALE}
        rows = []
        for suffix, font, resource, grades in [
            ("perfect", self.perfect_combo_font, judgement_resource, ("perfect",)),
            ("other", self.combo_font, self.asset("judge00", "judge_great_yellow"), ("great", "good", "meh", "miss", "ok"))]:
            row_id = f"{judgement_root}.{suffix}"
            label_id = f"{row_id}.label"
            combo_id = f"{row_id}.combo"
            label = node(label_id, "sprite", {"height-px": 100 * REFERENCE_FONT_SCALE}, resource)
            combo = node(combo_id, "text", {"text": "", "font-size": 100 * REFERENCE_FONT_SCALE,
                         "bitmap-font": font, "format": "integer", "colour": "#ffffffff"})
            self.bind(combo_id, "text", "combo.value")
            row = node(row_id, properties={**row_properties, "visible": False}, children=[label, combo])
            self.visible_variant(row_id, "judgement.result", grades)
            if suffix == "other":
                self.variant(label_id, "judgement.result", resource, {
                    "great": resource, "good": self.asset("judge00", "judge_good"), "meh": self.asset("judge00", "judge_bad"),
                    "miss": self.asset("judge00", "judge_poor"), "ok": self.asset("judge00", "judge_poor")})
            rows.append(row)
        self.template(f"{layout}.judgement", node(judgement_root, properties={"opacity": 0}, slot="hud.judgement", children=rows),
                      "hud.judgement", judgement_material)
        self.current_judgement(judgement_root)
        self.calibration_receipts.append({"change": "judgement-combo-measured-horizontal-row", "layout": layout,
            "sourceEvidence": "artifacts/iidx-judgement-research-20261018/judge00-consumer-data-v2.json",
            "observedSourceDigitCounts": [1, 2], "labelSourceHeight": 100, "glyphSourceSize": [74, 100],
            "sourceDigitAdvance": 55, "sourceSignedGap": -16, "logicalPixelScale": REFERENCE_FONT_SCALE,
            "rowProperties": row_properties, "stageNativeSourceCentreX": first_stage_left + stage_width / 2,
            "rowSourceCentreX": first_stage_left + stage_width / 2 + centre_dx,
            "stageNativeSourceCentreY": judgement_native_centre_y, "rowSourceCentreY": judgement_native_centre_y + centre_dy,
            "positionEvidence": "DP retains the native anchor pending a real original reference." if dp else "Measured1/2-digit source rows: P1 centre287/P2 centre1621, shared source top515 and height100.",
            "perfectPalette": {"label": "judge_p_great_blue", "glyphSuffix": "b", "phase": "fixed observed blue; original cadence not yet established"},
            "otherGrades": "Previous yellow glyph choice retained; label natural aspect follows its original250/204/160px width. Actual grade pairing and zero-combo display still need reference.",
            "lifetime": "Both children share the JudgementDisplay current-judgement/expiry gate. No speculative original fade track is introduced.",
            "independentHudCombo": "Suppress; actual combo.value is displayed only in the judgement owner."})
        self.write_materials(layout, background, information_material, gauge_material, judgement_material)

    def write_materials(self, layout, background, information, gauge, judgement):
        dp = layout == "dp"
        keymode, stage_mode = ("14k", "dual") if dp else ("7k", "single")
        suffix = f"ruleset=bms keymode={keymode} stage-mode={stage_mode}" + (f" presentation={layout}" if not dp else "")
        self.material_ini += [COMMON, f"Target: Global {suffix}", f'stage.background: resource Provide "{background}"',
                              f'decoration: resource Provide "{information}"', "hud.text: resource Suppress",
                              "bga.frame: resource Suppress"]
        self.calibration_receipts.append({"change": "native-bga-frame-explicit-suppression", "layout": layout,
            "target": f"Global {suffix}", "slot": "bga.frame", "state": "Suppress",
            "source": "BgaFrame catalog scopeGlobal and BmsBgaPanel single global key replicated per viewport",
            "reason": "The authored original cabinet already includes the source BGA frame; avoid the canonical silver border."})
        # This package keeps each critical HUD slot independently owned and recoverable.
        for group in range(2 if dp else 1):
            self.material_ini.append(f"Target: Stage {suffix} group=bms.group.deck-{group + 1} group-logical={group} group-visual={group}")
            self.material_ini += ['stage.background: resource Provide "common/empty"',
                                  f'decoration: resource Provide "{self.fullcombo_core_material}"']
            for slot, material in [("hud.gauge", gauge), ("hud.judgement", judgement)]:
                self.material_ini.append(f'{slot}: resource Provide "{material}"' if group == 0 else f"{slot}: resource Suppress")
            self.material_ini += ["hud.combo: resource Suppress", "hud.text: resource Suppress", f'playfield.judgement-line: resource Provide "art/primary/1/gameparts/playm_red_line"',
                                  'stage.foreground: resource Provide "common/empty"', 'playfield.backdrop: resource Provide "common/empty"',
                                  'playfield.baseplate: resource Provide "common/empty"']
        lane_receipts = []
        for group in range(2 if dp else 1):
            for local_logical in range(8):
                right_deck = dp and group == 1
                scratch = local_logical == (7 if right_deck else 0)
                global_logical = group * 8 + local_logical
                global_visual = (7 if scratch else local_logical - 1) if layout == "p2" else global_logical
                local_visual = global_visual - group * 8
                lane = f"bms.lane.scratch-{group + 1}" if scratch else f"bms.lane.key-{global_logical}"
                key_ordinal = local_logical + 1 if right_deck else local_logical
                colour = "scratch" if scratch else "black" if key_ordinal % 2 == 0 else "white"
                self.material_ini.append(f"Target: Lane {suffix} group=bms.group.deck-{group + 1} group-logical={group} group-visual={group} "
                    f"lane={lane} global-logical={global_logical} global-visual={global_visual} "
                    f"group-local-logical={local_logical} group-local-visual={local_visual}")
                self.material_ini += [f'{slot}: resource Provide "{value}"' for slot, value in self.lane_materials[colour].items()]
                self.material_ini += ['playfield.lane-surface: resource Provide "common/empty"', 'playfield.lane-divider: resource Provide "common/empty"',
                                      'playfield.hit-target: resource Provide "common/empty"', "effect.key-flash: resource Suppress"]
                if dp:
                    lane_receipts.append({"lane": lane, "group": group, "globalLogical": global_logical,
                        "globalVisual": global_visual, "groupLocalLogical": local_logical,
                        "groupLocalVisual": local_visual, "materialColour": colour})
        if dp:
            self.calibration_receipts.append({"change": "14k-canonical-lane-targets", "layout": layout,
                "source": "osu.Game.Rulesets.Bms/Skinning/BmsGameplaySkinLaneTopologyFactory.cs",
                "canonicalActions": "Scratch1, Key1..Key14, Scratch2; logical and visual order identical for 14K",
                "nativeGeometryEvidence": "BmsAuthoredSideInsetProductTest.TestAuthoredDualStageGapReachesAllSixteenNativeNotes",
                "lanes": lane_receipts})

    def legacy_configuration(self):
        for keymode in ["7K", "14K"]:
            dp = keymode == "14K"
            windows = "23,253,345.5859375,259.189453125;23,547,345.5859375,259.189453125;1551,253,345.5859375,259.189453125;1551,547,345.5859375,259.189453125" if dp else "610,100,700,821.25"
            viewports = []
            for window in windows.split(";"):
                x, y, width, height = map(float, window.split(","))
                viewports.append(f"{x / WIDTH:.12g},{y / HEIGHT:.12g},{width / WIDTH:.12g},{height / HEIGHT:.12g},fit")
            values = {"PlayfieldWidth": (1010 if dp else 440) / WIDTH, "PlayfieldHeight": 728 / HEIGHT,
                      "PlayfieldSideInset": 71 / WIDTH, "LaneSidePadding": 4 / 440,
                      "NormalLaneWidth": 1, "BlackLaneWidth": 40 / 52, "ScratchLaneWidth": 90 / 52,
                      "NormalLaneSpacing": 2 / 52, "ScratchLaneSpacing": 2 / 52, "LongNoteBodyWidth": 1,
                      "KeyAreaHeight": 120 / HEIGHT, "ScratchKeyWidth": 150 / 90, "GaugeHeight": 110 / HEIGHT,
                      "BarLineHeight": 1, "HitTargetLineHeight": 7 / (HEIGHT / 768), "BgaInformationHeight": 0}
            if dp:
                values["DualStageGap"] = 130 / 1010
            self.ini += ["[Bms]", f"Keymode: {keymode}", *[f"{key}: {value:.12g}" for key, value in values.items()],
                         "BgaViewports: " + "; ".join(viewports), ""]
            if not dp:
                source = next(viewport for viewport in self.gameplay["native_bga_placeholders"]
                              if viewport["movie"] == "center_frame_sp")
                for layout, field in [("p1", "BgaViewportsP1"), ("p2", "BgaViewportsP2")]:
                    bounds = shifted_bounds(source["bounds"], SP_CENTER_OFFSETS[layout])
                    value = viewport_configuration(bounds)
                    self.ini.append(f"{field}: {value}")
                    self.calibration_receipts.append({"change": "presentation-native-bga-viewport", "layout": layout,
                        "sourceMovie": source["movie"], "sourceInstance": source["instance"], "sourceBounds": source["bounds"],
                        "dx": SP_CENTER_OFFSETS[layout], "appliedBounds": bounds, "field": field, "value": value,
                        "genericViewport": "Existing BgaViewports safe window is retained."})
                self.ini.append("")
        self.asset("1/gameparts", "playm_red_line")

    def finish(self):
        self.fonts_for_numbers()
        self.legacy_configuration()
        self.common_lane_consumers()
        self.fullcombo_core()
        for layout in ["p1", "p2", "dp"]:
            self.layout_consumers(layout)
        (self.output / "skin.ini").write_text("\n".join([*self.ini, *self.material_ini, ""]), encoding="utf-8")
        manifest = {"contract": MANIFEST_CONTRACT, "scene": "gameplay-skin.scene.json", "sceneContract": SCENE_CONTRACT,
                    "eventContract": "oms-gameplay-skin-event.v1", "resources": self.resources, "bitmapFonts": self.fonts}
        scene = {"contract": SCENE_CONTRACT, "root": node("iidx33.root", blend="inherit"), "tracks": self.tracks,
                 "stateMachines": self.machines, "bindings": self.bindings, "variants": self.variants,
                 "templates": self.templates, "instances": self.instances, "completionOutro": 2500}
        write_json(self.output / "gameplay-skin.json", manifest)
        write_json(self.output / "gameplay-skin.scene.json", scene, compact=True)
        if (self.output / "gameplay-skin.json").stat().st_size > 64 * 1024:
            raise ValueError("Generated manifest exceeds the64KiB contract")
        if (self.output / "gameplay-skin.scene.json").stat().st_size > 512 * 1024:
            raise ValueError("Generated scene exceeds the512KiB contract")
        if len(self.tracks) > 512 or sum(len(track["keyframes"]) for track in self.tracks) > 8192:
            raise ValueError("Generated tracks exceed the bounded C5 contract")
        if len(self.resources) > 256:
            raise ValueError("Generated scene exceeds the 256 resource contract")
        total_pixels = 0
        for resource in self.resources:
            with self.Image.open(self.output / resource["path"]) as image:
                total_pixels += image.width * image.height
                image.verify()
        if total_pixels > 64 * 1024 * 1024:
            raise ValueError("Generated scene exceeds its decoded texture budget")
        limitations = ["This is a local playable study, not a completed pixel-identical IIDX runtime.",
            "Reference decorative motion is frozen at AFP frame 181. Original 0x10000 filters are not implemented.",
            "One/two-digit source PGREAT rows prove100px shared height,55px advance,-16px gap and whole-row centering. P1/P2 preserve measured anchors; DP position, other-grade digit pairing, zero-combo display, three/four-digit behaviour and PGREAT colour cadence still need original-reference calibration and Player comparison. The PGREAT consumer holds the observed blue phase without inventing a cycle.",
            "7K P1/P2 shift the source centre frame and exact BGA viewport by +270/-270 reference pixels; P1 is capture-confirmed, while P2 is a symmetric candidate awaiting its real-game capture. Generic BgaViewports retains the prior safe common window.",
            "14K declares the original 130 px middle gap; the full cabinet and HUD still need a real-render comparison.",
            "LN/CN/HCN use actual mode and holding/released projections. Pulse cadence and released-colour matching still need real-game calibration.",
            "Hard/EX-Hard/Hazard select continuous source gauge art; their source colour choices still need real-game calibration.",
            "The fixed percent mark remains original static art. Its bitmap-font percent character is transparent to avoid duplicate drawing.",
            "Default blue turntable centres remain visible without a press. Additional turntable feedback and rotation are not yet calibrated.",
            "Full combo consumes original fcombo00 textures, held60FPS affine frames and UV-correct projective quads. It uses actual completion/full-combo state and a2.5s outro; original executable eligibility and appearance still need comparison.",
            "Only 7K P1/P2 and 14K materials are authored. Other keymodes retain the existing canonical fallback; centred 7K uses canonical materials with the shared 7K geometry."]
        usage = ["请使用本工作区更新后的 OMS 客户端运行此包。",
            "7K：在 BMS 设置 → 游玩区域样式 选择 1P（居左）或 2P（居右）。默认 Center 使用 canonical，不显示此 IIDX33 布局。",
            "14K 自动使用双打布局；皮肤不会覆盖用户的布局偏好。"]
        write_json(self.output / "local-study.json", {"status": "generated-awaiting-OMS-validation", "reference": {"width": WIDTH, "height": HEIGHT, "fps": FPS},
            "resources": len(self.resources), "decodedTexturePixels": total_pixels, "sourceAssets": self.used_source_assets,
            "sceneBytes": (self.output / "gameplay-skin.scene.json").stat().st_size,
            "tracks": len(self.tracks), "keyframes": sum(len(track["keyframes"]) for track in self.tracks),
            "calibrationReceipts": "calibration-receipts.json",
            "usage": usage, "limitations": limitations})
        write_json(self.output / "calibration-receipts.json", {"status": "generated-awaiting-Player-comparison",
            "reference": {"width": WIDTH, "height": HEIGHT},
            "placementEvidence": {
                "p1": {"status": "capture-confirmed", "dx": SP_CENTER_OFFSETS["p1"],
                    "capture": "artifacts/iidx33-reference-20261018/captures/reference05-p1-gameplay-001-main.jpg"},
                "p2": {"status": "symmetric-candidate-awaiting-real-game-capture", "dx": SP_CENTER_OFFSETS["p2"]},
                "sourceGeometry": "gameplay-assets.json/native_bga_placeholders and skin-reference-map.json/layouts"},
            "changes": self.calibration_receipts})
        (self.output / "READ-ME.txt").write_text("IIDX 33 Sparkle Shower local study\n\n" + "\n".join(usage)
            + "\n\n" + "\n".join(limitations) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True, help="Existing independent IIDX art library")
    parser.add_argument("--research", type=Path, required=True, help="AFP research output with maps and filtered static frames")
    parser.add_argument("--fullcombo-research", type=Path, required=True, help="Exact original fcombo00 author-data export")
    parser.add_argument("--output", type=Path, required=True, help="New local author directory; an adjacent OSK is also created")
    parser.add_argument("--image-tools-path", type=Path, help="Non-system directory containing Pillow")
    parser.add_argument("--game-root", type=Path, action="append", default=[Path("D:/LDJ-010-2026081900")], help="Protected install root; never used as input")
    args = parser.parse_args()
    library, research = args.library.resolve(strict=True), args.research.resolve(strict=True)
    fullcombo_research = args.fullcombo_research.resolve(strict=True)
    output = args.output.resolve()
    archive = output.with_name(output.name + ".osk")
    if not output.parent.is_dir() or output.exists() or archive.exists():
        parser.error("Use a new output directory and OSK with an existing parent.")
    if output.drive.lower() == os.environ.get("SystemDrive", "C:").lower():
        parser.error("Generated art and packages must stay on a non-system drive.")
    for protected in [library, research, fullcombo_research, *[p.resolve() for p in args.game_root]]:
        if output.is_relative_to(protected) or protected.is_relative_to(output):
            parser.error("Output must not overlap an art library, evidence directory, or protected game installation.")
    if args.image_tools_path:
        sys.path.insert(0, str(args.image_tools_path.resolve(strict=True)))
    from PIL import Image
    required = ["skin-reference-map.json", "gameplay-assets.json", "extra-note-library/library.json",
                *[f"static-frames/{name}-static181.png" for name in ["1p_frame", "2p_frame", "center_frame_sp", "center_frame_dp_1p"]]]
    for relative in required:
        within(research, relative)
    output.mkdir()
    write_json(output / "generation.json", {"status": "running"})
    builder = Builder(library, research, fullcombo_research, output, Image)
    builder.finish()
    completed = {"status": "complete", "archive": archive.name}
    with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as zipped:
        for file in sorted(output.rglob("*")):
            if file.is_file() and file.name != "generation.json":
                zipped.write(file, file.relative_to(output).as_posix())
        zipped.writestr("generation.json", json.dumps(completed, ensure_ascii=False, indent=2) + "\n")
    write_json(output / "generation.json", completed)
    print(f"Created local author package: {output}")
    print(f"Created local import package: {archive}")


if __name__ == "__main__":
    main()
