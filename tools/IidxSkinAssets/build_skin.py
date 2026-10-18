"""Create a local IIDX 27-33 gameplay author package from independently extracted art.

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
STYLE_NAMES = {27: "IIDX 27 HEROIC VERSE", 28: "IIDX 28 BISTROVER", 29: "IIDX 29 CastHour",
               30: "IIDX 30 RESIDENT", 31: "IIDX 31 EPOLIS", 32: "IIDX 32 Pinky Crush", 33: "IIDX 33 Sparkle Shower"}


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
    def __init__(self, library, research, fullcombo_research, output, image_class, style=33, legacy_library=None,
                 customization_library=None, frame_package=None, name=None, identity=None):
        self.library, self.research, self.output, self.Image = library, research, output, image_class
        self.style = style
        self.prefix, self.frame_package = identity or f"iidx{style}", frame_package or f"gmframe{style}"
        self.name = name or STYLE_NAMES[style]
        self.map = read_json(research / "skin-reference-map.json")
        self.sd = self.map["stage"]["width"] == 1280
        self.source_scale = 1.5 if self.sd else 1
        stage = self.map["stage"]
        if (stage["width"], stage["height"], stage["fps"]) != ((1280, 720, FPS) if self.sd else (WIDTH, HEIGHT, FPS)):
            raise ValueError("The research stage does not match the selected source canvas")
        self.stage_width, self.stage_height = (438, 726) if self.sd else (440, 728)
        for movie in ("1p_frame", "2p_frame", "dp_frame_1p", "center_frame_sp", "center_frame_dp_1p"):
            if self.map["layouts"][movie]["package"] != self.frame_package:
                raise ValueError(f"The {movie} research is not for {self.frame_package}")
        self.gameplay = read_json(research / "gameplay-assets.json")
        self.fullcombo = read_json(within(fullcombo_research, "fcombo00-author-data.json"))
        if (self.fullcombo["schemaVersion"] != 1 or self.fullcombo["fps"] != FPS
                or self.fullcombo["durationFrames"] != 150 or self.fullcombo["durationMs"] != 2500
                or not self.fullcombo["last30FramesTransparent"]):
            raise ValueError("The full-combo source requires a different timing consumer")
        self.source_libraries = {"primary": library, "extra": research / "extra-note-library"}
        if customization_library:
            self.source_libraries["custom"] = customization_library
        self.customization = customization_library is not None
        self.bga_apertures = read_json(research / "effective-bga-viewports.json") if (research / "effective-bga-viewports.json").is_file() else None
        if self.sd:
            if legacy_library is None:
                raise ValueError("27-29 require the independently extracted 0/common and 0/gameparts library")
            self.source_libraries["legacy"] = legacy_library
            self.bga_apertures = read_json(research / "effective-bga-viewports.json")
            if style == 29:
                self.score_slots = read_json(research / "candidate-score-slots.json")
        self.resources, self.resource_paths, self.fonts = [], {}, []
        self.templates, self.instances, self.bindings, self.variants, self.tracks, self.machines = [], [], [], [], [], []
        self.ini = [f"// Local IIDX {style} study. Original art is not part of the OMS distribution.",
                    "[General]", f"Name: {self.name} · local study", "Author: local IIDX study", "Version: 2.7", ""]
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

    def options(self):
        result = [{"id": "layout", "type": "choice", "label": "游玩区域样式", "default": "p1",
                   "choices": [{"value": "p1", "label": "1P（左皿）"}, {"value": "p2", "label": "2P（右皿）"}],
                   "keymodes": ["5k", "6k", "7k", "8k"], "effect": "presentation"}]
        if self.customization:
            for identifier, label, keys in [("notes", "音符样式", [f"n{i:02}" for i in range(12)]),
                                            ("beam", "按键光柱", [f"n{i:02}" for i in range(4)]),
                                            ("cover", "挡板样式", [f"n{i:03}" for i in range(7)]),
                                            ("bomb", "击打爆炸", ["original", "compact"]),
                                            ("judgement", "判定文字", ["normal", "label-only"])]:
                result.append({"id": identifier, "type": "choice", "label": label, "default": keys[0],
                               "choices": [{"value": key, "label": "素材 " + str(int(key[1:])) if key.startswith("n") and key[1:].isdigit() else
                                            {"original": "原尺寸", "compact": "紧凑", "normal": "判定与连击", "label-only": "仅判定"}[key]}
                                           for key in keys]})
            result.extend([{"id": "beam-visible", "type": "toggle", "label": "显示按键光柱", "default": True},
                           {"id": "bomb-scale", "type": "number", "label": "爆炸缩放", "default": 1, "min": 0.5, "max": 1.5, "step": 0.05}])
        return result

    def layout_profiles(self):
        """Place only real logical lanes in source cabinet slots; unassigned slots remain inert art."""
        width, height = self.stage_width, self.stage_height
        pad, white, black, gap = (3, 51, 39, 3) if self.sd else (4, 52, 40, 2)
        result = []
        for ruleset, modes in [("bms", [5, 7, 14]), ("mania", [5, 6, 7, 8, 14, 16])]:
            for mode in modes:
                dual = mode > 8
                count = (8 if ruleset == "bms" else mode // 2) if dual else (mode + 1 if ruleset == "bms" else mode)
                keymode = f"{mode}k" if ruleset == "bms" or not dual else f"{count}k-{count}k"
                for presentation in (["any"] if dual else ["p1", "p2"]):
                    stages = []
                    for group in range(2 if dual else 1):
                        right = group == 1 if dual else presentation == "p2"
                        widths = ([white, black, white, black, white, black, white, 90] if right else
                                  [90, white, black, white, black, white, black, white])
                        starts, cursor = [], pad
                        for lane_width in widths:
                            starts.append(cursor)
                            cursor += lane_width + gap
                        if ruleset == "bms":
                            slots = ([*range(8)] if dual else ([7, *range(2 if mode == 5 else 0, 7)] if right else list(range(count))))
                        elif count in (5, 7):
                            slots = list(range(7 - count, 7)) if right else list(range(1, count + 1))
                        else:
                            slots = list(range(8 - count, 8)) if right else list(range(count))
                        x = ((456 if group == 0 else 1026) if self.sd else (455 if group == 0 else 1025)) if dual else (
                            (1410 if right else 72) if self.sd else (1409 if right else 71))
                        lanes = [[starts[slot] / width, widths[slot] / width] for slot in slots]
                        keys = [[(starts[slot] - (60 if slot == 0 and not right else 0)) / width,
                                 (150 if slot == (7 if right else 0) else widths[slot]) / width] for slot in slots]
                        stages.append({"rect": [x / WIDTH, 0, width / WIDTH, height / HEIGHT], "lanes": lanes, "keys": keys})
                    result.append({"id": f"{ruleset}.keys-{keymode}.{presentation}", "ruleset": ruleset, "keymode": keymode,
                                   "presentation": presentation, "stages": stages, "keyAreaHeight": 120 / HEIGHT,
                                   "gaugeHeight": 110 / HEIGHT, "hitPosition": 1})
        return result

    def register(self, relative):
        relative = Path(relative).as_posix()
        if relative in self.resource_paths:
            return self.resource_paths[relative]
        # The same effect frames are referenced by both native-host templates and every animation key.
        # Short IDs retain the bounded scene size without altering original paths or image bytes.
        identifier = (stable(Path(relative).stem.replace("exp00_", "e").replace("exp00-", "e")) if relative.startswith("art/primary/exp00/")
                      else self.prefix + "." + stable(relative.removesuffix(".png").replace("/", ".")))
        if len(identifier) > 128 or any(r["id"] == identifier for r in self.resources):
            raise ValueError(f"Non-unique or oversized resource identifier: {relative}")
        self.resources.append({"id": identifier, "type": "texture", "path": relative})
        self.resource_paths[relative] = identifier
        return identifier

    def generated(self, relative, image, filtering=None):
        target = self.output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        image.save(target)
        identifier = self.register(relative)
        if filtering is not None:
            next(resource for resource in self.resources if resource["id"] == identifier)["filter"] = filtering
        return identifier

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
        identifier = f"{self.prefix}.template.{name}"
        self.templates.append({"id": identifier, "root": root})
        self.instances.append({"id": f"{self.prefix}.instance.{name}", "template": identifier,
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

    def font(self, name, package, names, line_height, advance, group="primary"):
        glyphs = []
        for character, texture in names.items():
            resource = self.asset(package, texture, group)
            glyphs.append({"character": character, "resource": resource,
                           "advance": advance if isinstance(advance, (int, float)) else advance[character]})
        identifier = f"{self.prefix}.font.{name}"
        self.fonts.append({"id": identifier, "lineHeight": line_height, "glyphs": glyphs})
        return identifier

    def fonts_for_numbers(self):
        if self.sd:
            self.sd_fonts_for_numbers()
            return
        self.score_font = self.font("score", self.frame_package, {str(i): f"playm_sco{i:02}" for i in range(10)}, 24, 36)
        self.bpm_font = self.font("bpm", self.frame_package, {str(i): f"playm_bpm{i:02}" for i in range(10)}, 28, 36)
        self.small_bpm_font = self.font("bpm-small", "1/gameparts", {str(i): f"playm_bpm_m{i:02}" for i in range(10)}, 18, 25)
        self.hs_font = self.font("hs", self.frame_package, {str(i): f"playm_hs_{i:02}" for i in range(10)}, 19, 27)
        dot_source = within(self.library, self.source_index[("primary", self.frame_package, "playm_hs_dot")]["sprite"])
        with self.Image.open(dot_source) as dot:
            canvas = self.Image.new("RGBA", (8, 19))
            canvas.paste(dot, (0, 13))
        dot_resource = self.generated("font/hs-dot.png", canvas, filtering="nearest")
        self.fonts[-1]["glyphs"].append({"character": ".", "resource": dot_resource, "advance": 8})
        self.gauge_font = self.font("gauge", self.frame_package, {str(i): f"playm_gg_{i:02}" for i in range(10)}, 28, 38)
        # The original static cabinet already owns its fixed percent symbol.
        self.fonts[-1]["glyphs"].append({"character": "%", "resource": self.empty, "advance": 1})
        self.combo_font = self.font("combo", "judge00", {str(i): f"judge_combo_{i}y" for i in range(10)}, 100, 55)
        self.perfect_combo_font = self.font("combo-blue", "judge00", {str(i): f"judge_combo_{i}b" for i in range(10)}, 100, 55)

    def sd_fonts_for_numbers(self):
        # Only the glyph sizes and HS digit steps are established by the AFP.
        # Score/BPM multi-digit advances are explicitly labelled author candidates.
        self.score_font = self.font("score", self.frame_package, {str(i): f"playm_sco{i:02}" for i in range(10)}, 22, 26)
        self.bpm_font = self.font("bpm", self.frame_package, {str(i): f"playm_bpm{i:02}" for i in range(10)}, 22, 26)
        self.small_bpm_font = self.font("bpm-small", "0/gameparts", {str(i): f"playm_bpm_m{i:02}" for i in range(10)}, 18, 25, "legacy")
        height = 12 if self.style == 27 else 18
        self.hs_fonts = {}
        hs_metrics = {}
        for layout, movie, player in [("p1", "1p_frame", "1p"), ("p2", "2p_frame", "2p"), ("dp", "dp_frame_1p", "1p")]:
            digits = sorted((draw["bounds"]["left"] for draw in self.map["layouts"][movie]["draws"]
                             if any(name in {f"hs_{player}_{i}" for i in range(4)} for name in draw["game_bindings"])))
            if len(digits) != 4 or digits[1] - digits[0] != digits[3] - digits[2]:
                raise ValueError("The source HS row requires a different digit consumer")
            advance = digits[1] - digits[0]
            dot_advance = digits[2] - digits[1] - advance
            metrics = (height, advance, dot_advance)
            if metrics not in hs_metrics:
                hs_metrics[metrics] = self.font(f"hs-{layout}", self.frame_package,
                    {str(i): f"playm_hs_{i:02}" for i in range(10)}, height, advance)
                self.fonts[-1]["glyphs"].append({"character": ".", "resource": self.empty, "advance": dot_advance})
            self.hs_fonts[layout] = hs_metrics[metrics]
        self.gauge_font = self.font("gauge", self.frame_package, {str(i): f"playm_gg_{i:02}" for i in range(10)}, 24,
                                    {str(i): 18 if i == 1 else 26 for i in range(10)})
        self.fonts[-1]["glyphs"].append({"character": "%", "resource": self.empty, "advance": 1})
        self.combo_font = self.font("combo", "judge00", {str(i): f"judge_combo_{i}y" for i in range(10)}, 100, 55)
        self.perfect_combo_font = self.font("combo-blue", "judge00", {str(i): f"judge_combo_{i}b" for i in range(10)}, 100, 55)
        self.calibration_receipts.append({"change": "sd-source-font-metrics", "sourceStage": self.map["stage"],
            "projection": self.source_scale, "scoreBpmGlyph": [30, 22], "candidateScoreBpmAdvance": 26,
            "hsGlyph": [14, 12] if self.style == 27 else [22, 18], "hsDigitAdvance": "Derived independently from each layout's four source hooks.",
            "hsDot": "Original sprite placed independently at each layout's source transform; transparent text separator carries the measured step.",
            "smallBpm": "Original28x18 PNG, full UV on19x16 source GEO; anisotropic scene transform retains the image bytes.",
            "gaugeGlyph": [26, 24], "candidateGaugeAdvance": "26, except narrow1 uses18; host number logic remains unverified.",
            "scope": "Glyph dimensions/HS hooks are source evidence. Score/BPM clone counts/advances and gauge text layout are author candidates."})

    def projected(self, bounds):
        return {key: value * self.source_scale for key, value in bounds.items()}

    def numeric(self, identifier, source, font, font_size, bounds, surface=(0, 0, WIDTH, HEIGHT), fmt="integer"):
        properties = {**geometry(bounds, surface), "text": "", "font-size": font_size * REFERENCE_FONT_SCALE, "bitmap-font": font,
                      "format": fmt, "alignment": "right", "colour": "#ffffffff"}
        value = node(identifier, "text", properties)
        self.bind(identifier, "text", source)
        if source == "score.value":
            value["id"] = identifier + ".bms"
            self.bindings[-1]["target"] = value["id"]
            mania = json.loads(json.dumps(value))
            mania["id"] = identifier + ".mania"
            # The original four-EX-digit panel also accommodates mania's seven-digit native score.
            # Expand the local text canvas before horizontal compression so its right edge stays fixed.
            mania["properties"]["width"] *= 7 / 4
            mania["properties"]["scale-x"] = 4 / 7
            self.bind(mania["id"], "text", source)
            self.visible_variant(value["id"], "layout.ruleset", ["bms"])
            self.visible_variant(mania["id"], "layout.ruleset", ["mania"])
            return node(identifier + ".formats", children=[value, mania])
        return value

    def static_turntable_nodes(self, layout):
        movie = "dp_frame_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"
        result = []
        for group in range(2 if layout == "dp" else 1):
            binding = "turn_2p" if group == 1 or layout == "p2" else "turn_1p"
            draw = next(draw for draw in self.map["layouts"][movie]["draws"] if binding in draw["game_bindings"])
            identifier = f"{self.prefix}.{layout}.fixed-turn.n{group}"
            asset = draw["asset"]
            resource = self.asset(asset["package"], draw["texture"], asset.get("root", "primary"))
            mirrored = draw["matrix"]["a"] < 0
            result.append(node(identifier, "projective-sprite" if mirrored else "sprite", geometry(self.projected(draw["bounds"])), resource))
            if mirrored:
                self.tracks.append({"id": identifier + ".orientation", "type": "frame", "target": identifier,
                    "property": "projective-quad", "easing": "step", "loop": False,
                    "keyframes": [{"id": identifier + ".orientation.fixed", "time": 0,
                        "value": {"vertices": [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]], "opacity": 1}}]})
        return result

    def source_bounds(self, movies, binding_prefix, exclude=()):
        bounds = union(draw["bounds"] for movie in movies for draw in self.map["layouts"][movie]["draws"]
                     if draw["texture"] not in exclude and any(name.startswith(binding_prefix)
                        and (binding_prefix != "bpm_" or name.removeprefix("bpm_").isdigit()) for name in draw["game_bindings"]))
        return self.projected(bounds) if self.sd else bounds

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
        if self.sd or self.bga_apertures is not None:
            return self.sd_background(layout)
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
        resource = self.generated(relative, background, filtering="nearest")
        self.template(f"{layout}.background", node(f"{self.prefix}.{layout}.background", "sprite", resource=resource,
                                                  slot="stage.background"), "stage.background", relative.removesuffix(".png"))
        background.save(self.output / f"preview-{layout}-static.png")
        return relative.removesuffix(".png")

    def sd_bga_windows(self, movie):
        windows = []
        for viewport in self.bga_apertures["viewports"]:
            if viewport["movie"] == movie:
                left, top, right, bottom = viewport["fullyTransparentRectangularViewportCandidate"]["bounds"]
                windows.append(self.projected({"left": left, "top": top, "right": right, "bottom": bottom}))
        if not windows:
            raise ValueError(f"No effective BGA opening for {movie}")
        return windows

    def sd_background(self, layout):
        def original(movie):
            with self.Image.open(within(self.research, f"static-frames/{movie}-static181.png")) as image:
                if image.size != (self.map["stage"]["width"], self.map["stage"]["height"]):
                    raise ValueError("Source static frames must retain their original research canvas")
                return image.convert("RGBA").resize((WIDTH, HEIGHT), self.Image.Resampling.NEAREST)

        movie = "center_frame_dp_1p" if layout == "dp" else "center_frame_sp"
        centre = original(movie)
        background = self.Image.new("RGBA", (WIDTH, HEIGHT))
        dx = 0 if layout == "dp" else SP_CENTER_OFFSETS[layout]
        applied = [shifted_bounds(bounds, dx) for bounds in self.sd_bga_windows(movie)]
        for bounds in applied:
            background.paste((0, 0, 0, 255), (math.floor(bounds["left"]), math.floor(bounds["top"]),
                                                      math.ceil(bounds["right"]), math.ceil(bounds["bottom"])))
        # Underlay first: even a movie whose raw bounds cross the lane cannot erase cabinet art.
        background.alpha_composite(centre, (dx, 0))
        background.alpha_composite(original("dp_frame_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"))
        relative = f"backgrounds/{layout}.png"
        resource = self.generated(relative, background, filtering="nearest")
        self.template(f"{layout}.background", node(f"{self.prefix}.{layout}.background", "sprite", resource=resource,
                                                  slot="stage.background"), "stage.background", relative.removesuffix(".png"))
        background.save(self.output / f"preview-{layout}-static.png")
        self.calibration_receipts.append({"change": "sd-source-cabinet-and-safe-bga-aperture", "layout": layout,
            "sourceStage": self.map["stage"], "authorReference": [WIDTH, HEIGHT], "projectionCandidate": self.source_scale,
            "sampling": "nearest", "sourceMovie": movie, "effectiveBgaEvidence": "effective-bga-viewports.json",
            "appliedBgaBounds": applied, "centreDx": dx, "layering": "Black safe apertures, centre cabinet, then source side/fixed-symbol layer.",
            "scope": "Safe all-transparent frame opening; original host crop/fit and SD-to-HD presentation remain unverified."})
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
        for colour, width in [("white", 51 if self.sd else 52), ("black", 39 if self.sd else 40), ("scratch", 90)]:
            note_name = {"white": "notes_white", "black": "notes_black", "scratch": "notes_turn"}[colour]
            note_resource = self.asset("notes00", note_name)
            note_material = next(r["path"] for r in self.resources if r["id"] == note_resource).removesuffix(".png")
            root = node(f"{self.prefix}.note.{colour}", "sprite", {"height": 15 * REFERENCE_FONT_SCALE / 22.5,
                        "y": -5 * REFERENCE_FONT_SCALE / 22.5,
                        "anchor": "bottom-left", "origin": "bottom-left"}, note_resource, "object.note")
            self.template(f"note.{colour}", root, "object.note", note_material)
            if self.customization:
                self.variant(root["id"], "option.notes", note_resource, {"n00": note_resource, **{
                    f"n{i:02}": self.asset(f"notes{i:02}", note_name, "custom") for i in range(1, 12)}})
            values = {"object.note": note_material}
            for part, suffix in [("head", "start"), ("body", "beam0"), ("tail", "end")]:
                texture = f"notes_bss_{suffix}" if colour == "scratch" else f"notes_cn_{colour}_{suffix}"
                resource = self.asset("notes_cn00", texture, "extra")
                material = next(r["path"] for r in self.resources if r["id"] == resource).removesuffix(".png")
                identifier = f"{self.prefix}.long.{colour}.{part}"
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
                "explosionGeometry": "BMS retains its native hit-target host; mania normalizes against its full-column host."})

            if colour == "scratch":
                # Constant centres are owned once by the full cabinet, including vacant mania scratch slots.
                values["playfield.key"] = "common/empty"
            else:
                key = self.asset("1/gameparts", f"light_{colour}_key")
                key_properties = {"x": -4 / width, "y": 0 if colour == "white" else -5 / 120,
                                  "width": (60 if colour == "white" else 48) / width,
                                  "height": (90 if colour == "white" else 74) / 120}
                key_material = self.copy_marker(f"key-{colour}", key)
                key_id = f"{self.prefix}.key.{colour}"
                self.template(f"key.{colour}", node(key_id, "sprite", key_properties, key, "playfield.key",
                              "additive"),
                              "playfield.key", key_material)
                self.bind(key_id, "visible", "input.pressed")
                values["playfield.key"] = key_material

            beam = self.asset("beam00", f"playm_bar_{'turn' if colour == 'scratch' else colour}")
            beam_material = self.copy_marker(f"beam-{colour}", beam)
            beam_id = f"{self.prefix}.beam.{colour}"
            beam_root = node(beam_id, "sprite", {"height": (self.stage_height - 5) / self.stage_height}, beam, "decoration", "additive")
            if self.customization:
                art_id = f"{beam_id}.art"
                beam_root = node(beam_id, properties={"height": (self.stage_height - 5) / self.stage_height}, slot="decoration",
                                 children=[node(art_id, "sprite", resource=beam, blend="additive")])
                self.variant(art_id, "option.beam", beam, {"n00": beam, **{
                    f"n{i:02}": self.asset(f"beam{i:02}", f"playm_bar_{'turn' if colour == 'scratch' else colour}", "custom") for i in range(1, 4)}})
                self.bind(art_id, "visible", "option.beam-visible")
            self.template(f"beam.{colour}", beam_root,
                          "decoration", beam_material)
            self.bind(beam_id, "visible", "input.pressed")
            values["decoration"] = beam_material

            for ruleset in ("bms", "mania"):
                effect_material = self.copy_marker(f"explosion-{ruleset}-{colour}", hit_resources[0])
                effect_id = f"x.{ruleset[0]}.{colour[0]}.f"
                frame = node(effect_id, "sprite", {"x": (width / 2 + hit_bounds["left"] - hit_centre_x) / (width * 4),
                         "y": (22.5 - 5 + hit_bounds["top"] - hit_centre_y) / 90,
                         "width": (hit_bounds["right"] - hit_bounds["left"]) / (width * 4),
                         "height": (hit_bounds["bottom"] - hit_bounds["top"]) / 90}, hit_resources[0], blend="additive")
                properties = {"width": 4, "height": 4}
                if ruleset == "mania":
                    # The existing mania pool fills the column down to target.Top (one pixel above the bottom).
                    # Give this package a source-pixel effect band; changing the engine host would alter old skins.
                    host_height = self.stage_height - 1
                    properties = {"width": 4, "height": 90 / host_height, "y": (host_height - 22.5) / host_height}
                root = node(f"x.{ruleset[0]}.{colour[0]}", properties=properties, slot="effect.hit-explosion", children=[frame])
                if self.customization:
                    group_id = f"{root['id']}.s"
                    compact = json.loads(json.dumps(frame))
                    compact["id"] = effect_id + ".c"
                    root["children"] = [node(group_id + ".o", children=[frame]),
                                        node(group_id + ".c", properties={"scale-x": 0.7, "scale-y": 0.7}, children=[compact])]
                    self.visible_variant(group_id + ".o", "option.bomb", ["original"])
                    self.visible_variant(group_id + ".c", "option.bomb", ["compact"])
                    for prop in ("scale-x", "scale-y"):
                        self.bind(root["id"], prop, "option.bomb-scale")
                self.template(f"x.{ruleset[0]}.{colour[0]}", root, "effect.hit-explosion", effect_material)
                self.tracks.append({"id": f"{root['id']}.t", "type": "frame", "target": effect_id,
                                "property": "resource", "easing": "step", "loop": False, "trigger": "judgement.hit",
                                "keyframes": [{"id": f"{effect_id}.n{i}", "time": timing["time_ms"], "value": resource}
                                              for i, (timing, resource) in enumerate(zip(hit["frames"], hit_resources))]})
                if self.customization:
                    compact_track = json.loads(json.dumps(self.tracks[-1]))
                    compact_track["id"] += ".c"
                    compact_track["target"] += ".c"
                    for keyframe in compact_track["keyframes"]:
                        keyframe["id"] += ".c"
                    self.tracks.append(compact_track)
                values[f"effect.hit-explosion.{ruleset}"] = effect_material
            self.lane_materials[colour] = values
        if self.customization:
            cover = self.asset("lane000", "lane000")
            material = self.copy_marker("lane-cover", cover)
            identifier = f"{self.prefix}.lane-cover"
            self.template("lane-cover", node(identifier, "sprite", resource=cover, slot="playfield.lane-cover.fill"),
                          "playfield.lane-cover.fill", material)
            self.variant(identifier, "option.cover", cover, {"n000": cover, **{
                f"n{i:03}": self.asset(f"lane{i:03}", f"lane{i:03}", "custom") for i in range(1, 7)}})

    def layout_consumers(self, layout):
        dp, p2 = layout == "dp", layout == "p2"
        centre_dx = 0 if dp else SP_CENTER_OFFSETS[layout]
        player = "2p" if p2 else "1p"
        movies = ["center_frame_dp_1p", "dp_frame_1p"] if dp else ["2p_frame" if p2 else "1p_frame", "center_frame_sp"]
        background = self.background(layout)
        information_material = self.copy_marker(f"{layout}-information")
        information_children = self.static_turntable_nodes(layout)
        if self.sd:
            information_children.extend(self.sd_information_numbers(layout, movies, player, centre_dx))
        for suffix, source, font, size, prefix, fmt in ([] if self.sd else [
            ("score", "score.value", self.score_font, 24, f"score_{player}_", "integer"),
            ("hs", "scroll.speed", self.hs_font, 19, f"hs_{player}_", "fixed-2"),
            ("bpm", "timing.bpm", self.bpm_font, 28, "bpm_", "integer"),
            ("bpm-min", "timing.bpm-min", self.small_bpm_font, 18, "bpm_min_", "integer"),
            ("bpm-max", "timing.bpm-max", self.small_bpm_font, 18, "bpm_max_", "integer")]):
            bounds = self.source_bounds(movies, prefix)
            if suffix == "score":
                score_bindings = {name for movie in movies for draw in self.map["layouts"][movie]["draws"]
                                  for name in draw["game_bindings"] if name.startswith(prefix)}
                digit_count = len(score_bindings)
                if digit_count != 4:
                    raise ValueError(f"The recorded {layout} score needs a different digit-background consumer")
                information_children.append(node(f"{self.prefix}.{layout}.score.zeros", "text", {
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
                    "node": f"{self.prefix}.{layout}.{suffix}", "sourceMovie": "center_frame_sp", "sourceBounds": original_bounds,
                    "dx": centre_dx, "appliedBounds": bounds})
            information_children.append(self.numeric(f"{self.prefix}.{layout}.{suffix}", source, font, size, bounds, fmt=fmt))
        for suffix, source, y, size in [("title", "song.title", 43, 24), ("artist", "song.artist", 72, 18)]:
            if self.style == 27:
                # The frame181 centre panel is SCORE/MAXCOMBO; it has no song-text hook.
                continue
            identifier = f"{self.prefix}.{layout}.{suffix}"
            properties = {"x": (610 + centre_dx) / WIDTH, "y": y / HEIGHT, "width": 700 / WIDTH,
                "height": 28 / HEIGHT, "font-size": size * REFERENCE_FONT_SCALE, "text": "", "alignment": "centre", "text-overflow": "ellipsis", "colour": "#ffffffff"}
            if self.style != 33:
                movie = "center_frame_dp_1p" if dp else "center_frame_sp"
                source_bounds = self.source_bounds([movie], suffix)
                bounds = shifted_bounds(source_bounds, centre_dx)
                if self.sd and dp:
                    left_openings = [window for window in self.sd_bga_windows(movie) if window["left"] < WIDTH / 2]
                    bounds["right"] = min(bounds["right"], min(window["right"] for window in left_openings))
                    self.calibration_receipts.append({"change": "sd-dp-song-text-safe-area", "node": identifier,
                        "projectedSourcePlaceholder": source_bounds, "appliedBounds": bounds,
                        "scope": "Author candidate limits long metadata to the visible left cabinet column with ellipsis; the raw placeholder extends beneath the lane and is not native text-clipping evidence."})
                properties.update({**geometry(bounds), "alignment": "left" if dp else "centre"})
                self.calibration_receipts.append({"change": f"{self.style}-source-song-information-placement", "layout": layout,
                    "node": identifier, "sourceMovie": movie, "sourceBinding": suffix, "sourceBounds": source_bounds,
                    "dx": centre_dx, "appliedBounds": bounds,
                    "limit": "Source placeholder bounds; text uses the existing OMS metadata font, not an extracted game font. DP left alignment remains a candidate."})
            information_children.append(node(identifier, "text", properties))
            self.bind(identifier, "text", source)
            if not dp and self.style == 33:
                self.calibration_receipts.append({"change": "sp-centre-song-information-placement", "layout": layout,
                    "node": identifier, "sourceMovie": "center_frame_sp", "sourceBinding": suffix,
                    "sourceBounds": self.source_bounds(["center_frame_sp"], suffix), "dx": centre_dx,
                    "appliedCentreX": WIDTH / 2 + centre_dx,
                    "limit": "Existing metadata font, text width and vertical placement are retained."})
        information_children.append(self.fullcombo_overlay(layout))
        self.template(f"{layout}.information", node(f"{self.prefix}.{layout}.information", slot="decoration", children=information_children),
                      "decoration", information_material)

        # Stage HUD slots use the outer frame; the four-pixel padding applies only to lane content.
        first_stage_left = (456 if dp else 1410 if p2 else 72) if self.sd else (455 if dp else 1409 if p2 else 71)
        stage_width = self.stage_width
        slot_surface = (first_stage_left, self.stage_height + 120 + 0.006 * HEIGHT, stage_width, 110)
        gauge_draw = next(d for movie in movies for d in self.map["layouts"][movie]["draws"]
                          if any(n == f"gg_bar_{player}" for n in d["game_bindings"]))
        gauge_resource = self.asset(gauge_draw["asset"]["package"], gauge_draw["texture"], gauge_draw["asset"].get("root", "primary"))
        if self.style != 33:
            package, group = ("0/gameparts", "legacy") if self.sd else ("1/gameparts", "primary")
            gauge_modes = {"hard": self.asset(package, "playm_gauge_hard", group),
                           "ex-hard": self.asset(package, "playm_gauge_exhard", group)}
        else:
            gauge_modes = {"hard": self.asset(self.frame_package, "unit_groove_gauge_exc_r"),
                           "ex-hard": self.asset(self.frame_package, "unit_groove_gauge_exc_y"),
                           "hazard": self.asset(self.frame_package, "unit_groove_gauge_exc_b")}
        gauge_material = self.copy_marker(f"{layout}-gauge", gauge_resource)
        gauge_geometry = geometry(self.projected(gauge_draw["bounds"]), slot_surface)
        gauge_id = f"{self.prefix}.{layout}.gauge.reveal"
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
        children = [node(f"{self.prefix}.{layout}.gauge.dark", "sprite", {**gauge_geometry, "colour": "#404040ff"}, gauge_resource), gauge,
                    self.numeric(f"{self.prefix}.{layout}.gauge.percent", "gauge.value", self.gauge_font, 36 if self.sd else 28, percent_bounds, slot_surface, "percent")]
        self.variant(f"{self.prefix}.{layout}.gauge.dark", "gauge.type", gauge_resource, gauge_modes)
        self.template(f"{layout}.gauge", node(f"{self.prefix}.{layout}.gauge", slot="hud.gauge", children=children), "hud.gauge", gauge_material)

        judgement_resource = self.asset("judge00", "judge_p_great_blue")
        judgement_material = self.copy_marker(f"{layout}-judgement", judgement_resource)
        judgement_root = f"{self.prefix}.{layout}.judgement"
        judgement_native_centre_y = self.stage_height * 0.81
        judgement_slot_height = self.stage_height * 0.12
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
            if self.customization:
                self.visible_variant(combo_id, "option.judgement", ["normal"])
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
        if self.style != 33:
            self.calibration_receipts[-1].update({
                "sourceEvidenceScope": f"Current-HDD judge00 measurements; not a historical{self.style} executable capture.",
                "positionEvidence": f"Retained33 measured P1/P2 anchors or native DP anchor; historical{self.style} placement still needs comparison."})
        self.write_materials(layout, background, information_material, gauge_material, judgement_material)

    def sd_information_numbers(self, layout, movies, player, centre_dx):
        children = []
        entries = [("score", "score.value", self.score_font, 33, f"score_{player}_", "integer"),
                   ("hs", "scroll.speed", self.hs_fonts[layout], 18 if self.style == 27 else 27, f"hs_{player}_", "fixed-2"),
                   ("bpm", "timing.bpm", self.bpm_font, 33, "bpm_", "integer"),
                   ("bpm-min", "timing.bpm-min", self.small_bpm_font, 24, "bpm_min_", "integer"),
                   ("bpm-max", "timing.bpm-max", self.small_bpm_font, 24, "bpm_max_", "integer")]
        if self.style == 27:
            entries.append(("maxcombo", "combo.maximum", self.score_font, 33, f"maxcombo_{player}_", "integer"))
        for suffix, source, font, size, prefix, fmt in entries:
            bounds = self.source_bounds(movies, prefix, exclude=("playm_hs_dot",))
            hook = dict(bounds)
            if suffix in {"score", "maxcombo"}:
                bounds["right"] = bounds["left"] + 108 * self.source_scale
                if self.style == 29 and suffix == "score":
                    movie = "center_frame_dp_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"
                    slot = next(item for item in self.score_slots["slots"] if item["movie"] == movie)
                    left, top, right, bottom = slot["fullyOpaqueBlackCandidateRectangle"]["bounds"]
                    bounds["left"] = min(max(bounds["left"], left * self.source_scale), (right - 108) * self.source_scale)
                    bounds["right"] = bounds["left"] + 108 * self.source_scale
                    if bounds["top"] < top * self.source_scale or bounds["bottom"] > bottom * self.source_scale:
                        raise ValueError("The candidate score digits do not fit the measured source black slot")
            elif suffix == "bpm":
                bounds["right"] = bounds["left"] + 82 * self.source_scale
            elif suffix in {"bpm-min", "bpm-max"}:
                bounds["right"] = bounds["left"] + (19 + 2 * 25 / 28 * 19) * self.source_scale
            if layout != "dp" and (suffix.startswith("bpm") or self.style == 27 and suffix in {"score", "maxcombo"}):
                bounds = shifted_bounds(bounds, centre_dx)
            identifier = f"{self.prefix}.{layout}.{suffix}"
            if suffix in {"score", "maxcombo"}:
                children.append(node(identifier + ".zeros", "text", {**geometry(bounds), "text": "0000",
                    "font-size": size * REFERENCE_FONT_SCALE, "bitmap-font": font, "alignment": "right", "colour": "#404040ff"}))
            text = self.numeric(identifier, source, font, size, bounds, fmt=fmt)
            if suffix in {"bpm-min", "bpm-max"}:
                # Preserve full UV while reproducing19x16 GEO from the28x18 common image.
                sx = (19 / 28) / (16 / 18)
                text["properties"]["width"] /= sx
                text["properties"]["scale-x"] = sx
            children.append(text)
            self.calibration_receipts.append({"change": "sd-number-consumer", "layout": layout, "number": suffix,
                "sourceProjectedHook": hook, "appliedBounds": bounds, "fontSizeReferencePixels": size,
                "scope": "Original glyph/hook. Four score/maxcombo digits, three BPM digits and their advances are author candidates, not source host-code evidence."})
        for movie in movies:
            for index, draw in enumerate(self.map["layouts"][movie]["draws"]):
                if draw["texture"] not in {"playm_hs_dot", "playm_red_line"}:
                    continue
                asset = draw["asset"]
                resource = self.asset(asset["package"], draw["texture"], asset["root"])
                children.append(node(f"{self.prefix}.{layout}.fixed.{stable(movie)}.n{index}", "sprite",
                    geometry(self.projected(draw["bounds"])), resource))
        return children

    def write_materials(self, layout, background, information, gauge, judgement):
        dp = layout == "dp"
        for ruleset, mode in [("bms", 5), ("bms", 7), ("bms", 14), ("mania", 5), ("mania", 6),
                              ("mania", 7), ("mania", 8), ("mania", 14), ("mania", 16)]:
            if dp == (mode > 8):
                self.write_material_context(layout, background, information, gauge, judgement, ruleset, mode)

    def write_material_context(self, layout, background, information, gauge, judgement, ruleset, mode):
        dp = mode > 8
        count = (8 if ruleset == "bms" else mode // 2) if dp else (mode + 1 if ruleset == "bms" else mode)
        keymode = f"{mode}k" if ruleset == "bms" or not dp else f"{count}k-{count}k"
        stage_mode = "dual" if dp else "single"
        suffix = f"ruleset={ruleset} keymode={keymode} stage-mode={stage_mode}" + (f" presentation={layout}" if not dp else "")
        self.material_ini += [COMMON, f"Target: Global {suffix}", f'stage.background: resource Provide "{background}"',
                              f'decoration: resource Provide "{information}"', "hud.text: resource Suppress"]
        if ruleset == "bms":
            self.material_ini.append("bga.frame: resource Suppress")
            self.calibration_receipts.append({"change": "native-bga-frame-explicit-suppression", "layout": layout,
                "target": f"Global {suffix}", "slot": "bga.frame", "state": "Suppress",
                "source": "BgaFrame catalog scopeGlobal and BmsBgaPanel single global key replicated per viewport",
                "reason": "The authored original cabinet already includes the source BGA frame; avoid the canonical silver border."})
        # This package keeps each critical HUD slot independently owned and recoverable.
        for group in range(2 if dp else 1):
            group_id = f"bms.group.deck-{group + 1}" if ruleset == "bms" else f"mania.group.stage-{group + 1}"
            self.material_ini.append(f"Target: Stage {suffix} group={group_id} group-logical={group} group-visual={group}")
            self.material_ini += ['stage.background: resource Provide "common/empty"',
                                  f'decoration: resource Provide "{self.fullcombo_core_material}"']
            for slot, material in [("hud.gauge", gauge), ("hud.judgement", judgement)]:
                self.material_ini.append(f'{slot}: resource Provide "{material}"' if group == 0 or slot == "hud.judgement" else f"{slot}: resource Suppress")
            self.material_ini += ["hud.combo: resource Suppress", "hud.text: resource Suppress",
                                  'playfield.judgement-line: resource Provide "common/empty"' if self.sd else 'playfield.judgement-line: resource Provide "art/primary/1/gameparts/playm_red_line"',
                                  'stage.foreground: resource Provide "common/empty"', 'playfield.backdrop: resource Provide "common/empty"',
                                  'playfield.baseplate: resource Provide "common/empty"']
            if self.customization:
                self.material_ini.append('playfield.lane-cover.fill: resource Provide "markers/lane-cover"')
        lane_receipts = []
        profile = next(profile for profile in self.layout_profiles() if profile["ruleset"] == ruleset
                       and profile["keymode"] == keymode and profile["presentation"] == ("any" if dp else layout))
        for group in range(2 if dp else 1):
            group_id = f"bms.group.deck-{group + 1}" if ruleset == "bms" else f"mania.group.stage-{group + 1}"
            for local_logical in range(count):
                right_deck = dp and group == 1
                global_logical = group * count + local_logical
                if ruleset == "bms":
                    scratch = local_logical == (count - 1 if right_deck else 0)
                    local_visual = (count - 1 if scratch else local_logical - 1) if layout == "p2" else local_logical
                    lane = f"bms.lane.scratch-{group + 1}" if scratch else f"bms.lane.key-{global_logical}"
                else:
                    scratch = count in (6, 8) and local_logical == (count - 1 if right_deck or layout == "p2" else 0)
                    local_visual = local_logical
                    lane = f"mania.lane.column-{global_logical + 1}"
                global_visual = group * count + local_visual
                lane_width = profile["stages"][group]["lanes"][local_logical][1] * self.stage_width
                colour = "scratch" if scratch else "black" if abs(lane_width - (39 if self.sd else 40)) < 0.01 else "white"
                self.material_ini.append(f"Target: Lane {suffix} group={group_id} group-logical={group} group-visual={group} "
                    f"lane={lane} global-logical={global_logical} global-visual={global_visual} "
                    f"group-local-logical={local_logical} group-local-visual={local_visual}")
                materials = self.lane_materials[colour]
                materials = {slot: value for slot, value in materials.items() if not slot.startswith("effect.hit-explosion.")}
                materials["effect.hit-explosion"] = self.lane_materials[colour][f"effect.hit-explosion.{ruleset}"]
                if scratch:
                    # The source centre is a constant decoration, including when mania leaves the scratch slot empty.
                    materials = {**materials, "playfield.key": "common/empty"}
                self.material_ini += [f'{slot}: resource Provide "{value}"' for slot, value in materials.items()]
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
        if self.sd:
            self.sd_configuration()
            return
        for keymode in ["5K", "7K", "14K"]:
            dp = keymode == "14K"
            windows = "23,253,345.5859375,259.189453125;23,547,345.5859375,259.189453125;1551,253,345.5859375,259.189453125;1551,547,345.5859375,259.189453125" if dp else "610,100,700,821.25"
            viewports = []
            for window in windows.split(";"):
                x, y, width, height = map(float, window.split(","))
                viewports.append(f"{x / WIDTH:.12g},{y / HEIGHT:.12g},{width / WIDTH:.12g},{height / HEIGHT:.12g},fit")
            if self.bga_apertures is not None:
                viewports = [viewport_configuration(bounds) for bounds in self.sd_bga_windows("center_frame_dp_1p" if dp else "center_frame_sp")]
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
                    value = "; ".join(viewport_configuration(shifted_bounds(window, SP_CENTER_OFFSETS[layout]))
                                      for window in self.sd_bga_windows("center_frame_sp")) if self.bga_apertures is not None else viewport_configuration(bounds)
                    self.ini.append(f"{field}: {value}")
                    self.calibration_receipts.append({"change": "presentation-native-bga-viewport", "layout": layout,
                        "sourceMovie": source["movie"], "sourceInstance": source["instance"], "sourceBounds": source["bounds"],
                        "dx": SP_CENTER_OFFSETS[layout], "appliedBounds": bounds, "field": field, "value": value,
                        "genericViewport": "Existing BgaViewports safe window is retained."})
                self.ini.append("")
        self.asset("1/gameparts", "playm_red_line")

    def sd_configuration(self):
        for keymode in ["5K", "7K", "14K"]:
            dp = keymode == "14K"
            values = {"PlayfieldWidth": (1008 if dp else 438) / WIDTH, "PlayfieldHeight": 726 / HEIGHT,
                      "PlayfieldSideInset": 72 / WIDTH, "LaneSidePadding": 3 / 438,
                      "NormalLaneWidth": 1, "BlackLaneWidth": 39 / 51, "ScratchLaneWidth": 90 / 51,
                      "NormalLaneSpacing": 3 / 51, "ScratchLaneSpacing": 3 / 51, "LongNoteBodyWidth": 1,
                      "KeyAreaHeight": 120 / HEIGHT, "ScratchKeyWidth": 150 / 90, "GaugeHeight": 110 / HEIGHT,
                      "BarLineHeight": 1, "HitTargetLineHeight": 7 * REFERENCE_FONT_SCALE, "BgaInformationHeight": 0}
            if dp:
                values["DualStageGap"] = 132 / 1008
            windows = self.sd_bga_windows("center_frame_dp_1p" if dp else "center_frame_sp")
            self.ini += ["[Bms]", f"Keymode: {keymode}", *[f"{key}: {value:.12g}" for key, value in values.items()],
                         "BgaViewports: " + "; ".join(viewport_configuration(bounds) for bounds in windows), ""]
            if not dp:
                for layout, field in [("p1", "BgaViewportsP1"), ("p2", "BgaViewportsP2")]:
                    self.ini.append(field + ": " + "; ".join(viewport_configuration(shifted_bounds(bounds, SP_CENTER_OFFSETS[layout])) for bounds in windows))
                self.ini.append("")
        self.calibration_receipts.append({"change": "sd-native-lane-geometry", "sourceLaneSize": [292, 484],
            "authorReferenceScale": 1.5, "stageSize": [438, 726], "laneWidths": {"white": 51, "black": 39, "scratch": 90},
            "sidePadding": 3, "spacing": 3, "dpGap": 132,
            "scope": "True lane GEO, not294x494 decorative lane_base. Small source affine differences, native key host and original host projection remain unverified.",
            "commonArtScope": "Current shared HD notes/effects keep their own source-pixel metrics; they are not multiplied by the SD projection."})

    def finish(self):
        self.fonts_for_numbers()
        self.legacy_configuration()
        self.common_lane_consumers()
        self.fullcombo_core()
        for layout in ["p1", "p2", "dp"]:
            self.layout_consumers(layout)
        (self.output / "skin.ini").write_text("\n".join([*self.ini, *self.material_ini, ""]), encoding="utf-8")
        manifest = {"contract": MANIFEST_CONTRACT, "scene": "gameplay-skin.scene.json", "sceneContract": SCENE_CONTRACT,
                    "eventContract": "oms-gameplay-skin-event.v1", "resources": self.resources, "bitmapFonts": self.fonts,
                    "options": self.options(), "layouts": self.layout_profiles()}
        scene = {"contract": SCENE_CONTRACT, "root": node(f"{self.prefix}.root", blend="inherit"), "tracks": self.tracks,
                 "stateMachines": self.machines, "bindings": self.bindings, "variants": self.variants,
                 "templates": self.templates, "instances": self.instances, "completionOutro": 2500}
        manifest["layouts"] = rendered_float_value(manifest["layouts"])
        write_json(self.output / "gameplay-skin.json", manifest, compact=True)
        write_json(self.output / "gameplay-skin.scene.json", scene, compact=True)
        if (self.output / "gameplay-skin.json").stat().st_size > 64 * 1024:
            raise ValueError("Generated manifest exceeds the64KiB contract")
        if (self.output / "gameplay-skin.scene.json").stat().st_size > 512 * 1024:
            raise ValueError("Generated scene exceeds the512KiB contract")
        if len(self.tracks) > 512 or sum(len(track["keyframes"]) for track in self.tracks) > 8192:
            raise ValueError("Generated tracks exceed the bounded C5 contract")
        if len(self.resources) > 256:
            raise ValueError("Generated scene exceeds the 256 resource contract")
        if len(self.fonts) > 8:
            raise ValueError("Generated manifest exceeds the eight bitmap-font contract")
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
            "BMS5/7/14K and native mania5/6/7/8/14/16K are authored. Scratch/outer key vacancies are package geometry, not extra gameplay lanes; unsupported modes retain canonical materials."]
        if self.style != 33:
            limitations[2] = ("Judgement rows use current-HDD judge00 art and metrics, with the33 measured P1/P2 anchors "
                              f"and native DP anchor. Historical{self.style} default colours, placement and animation need comparison.")
            limitations[3] = (f"7K P1/P2 shift the{self.style} AFP centre frame and BGA viewport by +270/-270 reference pixels. "
                              f"Both are candidates awaiting a{self.style} executable capture; the33 P1 capture is not{self.style} evidence.")
            limitations[6] = ("Hard/EX-Hard use named common playm_gauge_hard/exhard art from the current HDD. "
                              f"Hazard retains the normal gauge artwork because no historical{self.style} Hazard mapping is established. "
                              f"Ruleset gauge behaviour is unchanged; historical{self.style} palette selection remains unverified.")
            limitations[8] = (f"The{self.style} usr_turn00 blue-heart centre remains visible without a press. "
                              "Its historical default selection, feedback and rotation need comparison.")
            limitations.append(f"The cabinet and its fonts are actual {self.frame_package} data retained in the current HDD. "
                               "Common notes/CN/HCN, judgement, key lights, beams and fcombo00 come from that same current HDD; "
                               f"this does not prove a complete historical{self.style} default asset set. Song select and results remain OMS.")
        if self.sd:
            limitations[4] = "The original1280x720 SD layout uses an explicit1.5 author projection:438x726 lane stages,51/39/90 lanes,3px gaps/padding and132px DP gap. Source affine differences and the original host presentation still need comparison."
            limitations[8] = f"The{self.style} original turn00 remains visible at its own source geometry; historical default selection, feedback and rotation remain unverified."
            limitations.append("SD small BPM preserves the original full-UV19x16 GEO of the28x18 common PNG through an anisotropic transform. HS uses per-layout measured digit steps and an independently placed original dot. Score/maxcombo four-digit and BPM three-digit layouts are labelled author candidates because the AFP has only one placeholder; gauge numeral layout also awaits host evidence.")
            limitations.append("SD BGA windows use safe transparent cabinet apertures, with black beneath the original frame. Original host crop/fit, multiple-window content mapping, key-light positions and historical default common-art selection remain unverified.")
            if self.style == 27:
                limitations.append("The27 centre score/maxcombo panel uses actual score and highest attained combo. Frame181 has no title/artist hook, so those texts are not drawn over its score panel.")
        usage = ["请使用本工作区更新后的 OMS 客户端运行此包。",
            "BMS 支持5K／7K／14K；mania 支持5／6／7／8／14／16K。",
            "皮肤设置 → 皮肤配置项：默认1P，可选2P；选择按皮肤及模式独立保存，下次游玩生效。",
            "5K保留靠皿的五个普通键位，mania5／7／14K的皿位空置；6／8／16K的皿位承载实际按键。",
            "双侧布局依据谱面的真实双stage结构；其他键数使用原有后备表现。"]
        write_json(self.output / "local-study.json", {"status": "generated-awaiting-OMS-validation", "reference": {"width": WIDTH, "height": HEIGHT, "fps": FPS},
            "resources": len(self.resources), "decodedTexturePixels": total_pixels, "sourceAssets": self.used_source_assets,
            "sceneBytes": (self.output / "gameplay-skin.scene.json").stat().st_size,
            "tracks": len(self.tracks), "keyframes": sum(len(track["keyframes"]) for track in self.tracks),
            "calibrationReceipts": "calibration-receipts.json",
            "usage": usage, "limitations": limitations})
        placement = {
            "p1": {"status": "capture-confirmed", "dx": SP_CENTER_OFFSETS["p1"],
                "capture": "artifacts/iidx33-reference-20261018/captures/reference05-p1-gameplay-001-main.jpg"},
            "p2": {"status": "symmetric-candidate-awaiting-real-game-capture", "dx": SP_CENTER_OFFSETS["p2"]},
            "sourceGeometry": "gameplay-assets.json/native_bga_placeholders and skin-reference-map.json/layouts"}
        if self.style != 33:
            placement["p1"] = {"status": f"candidate-awaiting{self.style}-real-game-capture", "dx": SP_CENTER_OFFSETS["p1"]}
            placement["p2"]["status"] = f"candidate-awaiting{self.style}-real-game-capture"
        write_json(self.output / "calibration-receipts.json", {"status": "generated-awaiting-Player-comparison",
            "reference": {"width": WIDTH, "height": HEIGHT},
            "placementEvidence": placement,
            "changes": self.calibration_receipts})
        (self.output / "READ-ME.txt").write_text(self.name + " local study\n\n" + "\n".join(usage)
            + "\n\n" + "\n".join(limitations) + "\n", encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--style", type=int, choices=tuple(STYLE_NAMES), default=33, help="Frame style; default33 preserves the existing author package")
    parser.add_argument("--library", type=Path, required=True, help="Existing independent IIDX art library")
    parser.add_argument("--legacy-library", type=Path, help="Independent 0/common and 0/gameparts library, required for27-29")
    parser.add_argument("--customization-library", type=Path, help="Verified independent notes, beams and lane-cover variants")
    parser.add_argument("--frame-package", help="Verified alternative cabinet archive identity, including BPL frames")
    parser.add_argument("--name", help="Local display name for a researched alternative cabinet")
    parser.add_argument("--identity", help="Stable node prefix for an alternative cabinet")
    parser.add_argument("--research", type=Path, required=True, help="AFP research output with maps and filtered static frames")
    parser.add_argument("--fullcombo-research", type=Path, required=True, help="Exact original fcombo00 author-data export")
    parser.add_argument("--output", type=Path, required=True, help="New local author directory; an adjacent OSK is also created")
    parser.add_argument("--image-tools-path", type=Path, help="Non-system directory containing Pillow")
    parser.add_argument("--game-root", type=Path, action="append", default=[Path("D:/LDJ-010-2026081900")], help="Protected install root; never used as input")
    args = parser.parse_args()
    library, research = args.library.resolve(strict=True), args.research.resolve(strict=True)
    fullcombo_research = args.fullcombo_research.resolve(strict=True)
    legacy_library = args.legacy_library.resolve(strict=True) if args.legacy_library else None
    customization_library = args.customization_library.resolve(strict=True) if args.customization_library else None
    if args.style < 30 and legacy_library is None:
        parser.error("27-29 require --legacy-library with verified independent source copies.")
    output = args.output.resolve()
    archive = output.with_name(output.name + ".osk")
    if not output.parent.is_dir() or output.exists() or archive.exists():
        parser.error("Use a new output directory and OSK with an existing parent.")
    if output.drive.lower() == os.environ.get("SystemDrive", "C:").lower():
        parser.error("Generated art and packages must stay on a non-system drive.")
    for protected in [library, research, fullcombo_research, *([legacy_library] if legacy_library else []),
                      *([customization_library] if customization_library else []), *[p.resolve() for p in args.game_root]]:
        if output.is_relative_to(protected) or protected.is_relative_to(output):
            parser.error("Output must not overlap an art library, evidence directory, or protected game installation.")
    if args.image_tools_path:
        sys.path.insert(0, str(args.image_tools_path.resolve(strict=True)))
    from PIL import Image
    required = ["skin-reference-map.json", "gameplay-assets.json", "extra-note-library/library.json",
                *[f"static-frames/{name}-static181.png" for name in ["1p_frame", "2p_frame", "center_frame_sp", "center_frame_dp_1p"]]]
    for relative in required:
        within(research, relative)
    if args.style < 30:
        for relative in ["effective-bga-viewports.json", "static-frames/dp_frame_1p-static181.png"]:
            within(research, relative)
    output.mkdir()
    write_json(output / "generation.json", {"status": "running"})
    builder = Builder(library, research, fullcombo_research, output, Image, args.style, legacy_library,
                      customization_library, args.frame_package, args.name, args.identity)
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
