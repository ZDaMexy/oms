"""Build the independent pre-Lightning component catalogue from an IIDX26 library.

The public source contains no game art. Every pixel and movie is read from the
caller's verified library. Old Lightning author packages are never inputs.
"""
from __future__ import annotations

import argparse
import contextlib
import copy
import hashlib
import io
import json
import math
from pathlib import Path
from standard_source_catalog import (frame_id, package_key, includes20, includes22,
                                     SOURCE20_STYLES, SOURCE22_STYLES, SOURCE21_STYLES, layout_family)
import re
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True

from build_skin import node, read_json, within
from customization_modules import Branch, ModuleBuilder, material_contexts
from build_fullcombo_catalog import (InternTable, LocalStateRecorder, all_tags, exporter_classes,
                                    load_tools, point_values, rgba_values, to_recorded_animation)
from export_customization_animations import node_for_reference, root_loop_control
from standard_source_catalog import styles as source_styles, retained_styles, families as source_families, art_identity

FRAME_IDS = tuple(f"iidx{style:02}" for style in (3, 8, 12, 14, 16, 17, 19, 21, 25, 26))
COLOURS = ("white", "black", "scratch")
SOURCE_WIDTHS = {"white": 34, "black": 26, "scratch": 60}
SOURCE_TO_LOGICAL = 768 / 720
SOURCE_TO_REFERENCE = 1.5
SOURCE_STAGE_HEIGHT = 484
SOURCE_STAGE_WIDTH = 292
JUDGEMENT_DIGIT_ADVANCE = 37
JUDGEMENT_LABEL_WIDTHS = {"judge_p_great_blue": 170, "judge_p_great_white": 170,
                          "judge_p_great_pink": 170, "judge_great_yellow": 170,
                          "judge_good": 141, "judge_bad": 112, "judge_poor": 141}
REFERENCE_STAGE_HEIGHT = SOURCE_STAGE_HEIGHT * SOURCE_TO_REFERENCE
SOURCE_HIT_OFFSET = 10 / 3  # Explicit OMS alignment; original-game hit-point calibration is separate.
SUPPORTED_MODULES = ("notes", "charge-notes", "judgement", "explosion", "key-beam", "lane-cover",
                     "lift-art", "turntable", "graph-area", "full-combo")


def judgement_fit_steps(label_width, digit_width, *, gap=-11):
    """Fit the complete source row, retaining full size while it fits.

    C4 clock keyframes stop at 86,400,000, so the final legal decimal boundary
    reserves ten digits for all 8--10 digit int values. Realistic 1--7 digit
    combos use their exact integer digit count, with no cropping or ellipsis.
    """
    def scale(digits):
        # MeasureText includes the final image overhang beyond the37px advance.
        width = max(label_width, label_width + gap + digit_width + JUDGEMENT_DIGIT_ADVANCE * (digits - 1))
        return min(1, SOURCE_STAGE_WIDTH / width)

    return [(0, scale(1))] + [(10 ** (digits - 1), scale(digits)) for digits in range(2, 8)] + [(10_000_000, scale(10))]


def turntable_motion(identifier, draw, layout, group):
    """Rotate the preserved projected quad only when its source is a similarity.

    For a uniform affine source M, M R(theta) equals
    R(sign(det(M)) theta) M. Its bounds centre is its original quad centre.
    """
    if layout not in ("p1", "p2", "dp") or group not in (range(2) if layout == "dp" else range(1)):
        raise ValueError("Turntable motion requires the actual logical stage group")
    a, b, c, d = (draw["matrix"][key] for key in ("a", "b", "c", "d"))
    determinant = a * d - b * c
    orthogonality = a * c + b * d
    scale_difference = a * a + b * b - c * c - d * d
    if (math.isclose(determinant, 0, abs_tol=1e-12)
            or not math.isclose(orthogonality, 0, abs_tol=1e-10)
            or not math.isclose(scale_difference, 0, abs_tol=1e-10)):
        raise ValueError("Turntable source is not a uniform similarity; retain its static projective art")
    bounds = draw["bounds"]
    centre = [(bounds["left"] + bounds["right"]) / 2, (bounds["top"] + bounds["bottom"]) / 2]
    properties = {"x": centre[0] / 1280, "y": centre[1] / 720,
                  "width": (bounds["right"] - bounds["left"]) / 1280,
                  "height": (bounds["bottom"] - bounds["top"]) / 720, "origin": "centre"}
    direction = -1 if determinant < 0 else 1
    track = {"id": identifier + ".motion", "type": "tween", "target": identifier, "property": "rotation",
             "easing": "linear", "loop": True, "clock": f"input.motion-revolutions-{group + 1}",
             "keyframes": [{"id": identifier + ".motion.zero", "time": 0, "value": 0},
                           {"id": identifier + ".motion.turn", "time": 1, "value": direction * 360}]}
    return properties, track, {"determinant": determinant, "orthogonalityError": orthogonality,
                               "axisScaleError": scale_difference, "rotationSign": direction,
                               "sourceCentre": centre, "clock": track["clock"]}


def source22_turntable_size(frame, source, draw):
    """Read the dynamic 21/22 turntable carrier from its own normalized GEO.

    The original named turn00 hook addresses the whole externally loaded image
    with normalized UVs. Its package-local four vertices therefore establish
    the carrier size even when the separately loaded image is unresolved in a
    cabinet/common reference export.
    """
    if (frame not in (*SOURCE22_STYLES, *SOURCE21_STYLES) or source["package"] != package_key(frame)
            or source["common_family"] != layout_family(frame) or draw["texture"] != "turn00"):
        raise ValueError("22 Turntable hook belongs to a different source family")
    vertices, uv, draws = draw["source_vertices"], draw["uv"], draw["draw_params"]
    if len(vertices) != 4:
        raise ValueError("22 Turntable carrier is not its complete original quad")
    width, height = vertices[1]["x"], vertices[2]["y"]
    if (not 0 < width <= 1280 or not 0 < height <= 720
            or [(p["x"], p["y"], p["z"]) for p in vertices] != [(0, 0, 0), (width, 0, 0), (0, height, 0), (width, height, 0)]
            or [(p["x"], p["y"], p["z"]) for p in uv] != [(0, 0, 0), (1, 0, 0), (0, 1, 0), (1, 1, 0)]
            or len(draws) != 1 or draws[0]["region"] != "turn00" or draws[0]["flags"] != 67
            or draws[0]["vertexes"] != [0, 1, 2, 2, 1, 3] or draws[0]["blend"] is not None):
        raise ValueError("22 Turntable carrier XYZ/UV/draw differs from the qualified source")
    return width, height


def source20_turntable_size(frame, source, draw):
    """Qualify the original20 default25px carrier, without a dynamic-size claim."""
    if (frame not in SOURCE20_STYLES or source["package"] != package_key(frame)
            or source["common_family"] != "s20" or draw["texture"] != "turn_00"
            or draw.get("asset", {}).get("package") != "s20/gameparts"
            or (draw["asset"]["width"], draw["asset"]["height"]) != (25, 25)):
        raise ValueError("Original20 Turntable hook differs from its own default source")
    vertices, uv, draws = draw["source_vertices"], draw["uv"], draw["draw_params"]
    if ([(p["x"], p["y"], p["z"]) for p in vertices] != [(0, 0, 0), (25, 0, 0), (0, 25, 0), (25, 25, 0)]
            or [(p["x"], p["y"], p["z"]) for p in uv] != [(0, 0, 0), (1, 0, 0), (0, 1, 0), (1, 1, 0)]
            or len(draws) != 1 or draws[0]["region"] != "turn_00" or draws[0]["flags"] != 67
            or draws[0]["vertexes"] != [0, 1, 2, 2, 1, 3] or draws[0]["blend"] is not None):
        raise ValueError("Original20 Turntable carrier XYZ/UV/draw differs from the qualified source")
    return 25, 25


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def exact_styles(packages, pattern, expected):
    values = sorted(int(match[1]) for key in packages if (match := re.fullmatch(pattern, key)))
    if values != list(expected):
        raise ValueError(f"IIDX26 source family differs from its qualified selection: {pattern}: {values}")
    return values


def terminal_repeat_control(movie):
    """Translate the qualified lnote root's final deepGotoAndPlay(loop).

    Its last frame only removes the final draw and jumps: the next rendered
    image is the first frame of the next cycle, not an extra empty hold.
    """
    if movie.labels != {"loop": 0}:
        raise ValueError("Repeating long-note source must expose its sole loop at frame zero")
    actions = [(fi, ti, movie.tags[ti]) for fi, frame in enumerate(movie.frames)
               for ti in range(frame.start_tag_offset, frame.start_tag_offset + frame.num_tags)
               if type(movie.tags[ti]).__name__ == "AP2DoActionTag"]
    if len(actions) != 1:
        raise ValueError("Repeating long-note source must have exactly one root action")
    frame_index, tag_index, tag = actions[0]
    useful = [a for a in tag.bytecode.as_dict()["actions"] if a["action"] != "END"]
    expected = ["PUSH", "GET_VARIABLE", "PUSH", "CALL_METHOD"]
    boundary = movie.tags[movie.frames[-1].start_tag_offset:
                          movie.frames[-1].start_tag_offset + movie.frames[-1].num_tags]
    if (frame_index != len(movie.frames) - 1 or frame_index <= 0 or boundary[-1] is not tag
            or any(type(t).__name__ != "AP2RemoveObjectTag" for t in boundary[:-1])
            or [a["action"] for a in useful] != expected):
        raise ValueError("Repeating long-note source has an unqualified jump boundary")
    first, _, method, _ = useful
    if (len(first["objects"]) != 4 or first["objects"][:3] != ["'loop'", "THIS", "2"]
            or not first["objects"][3].endswith(": aeplib)") or len(method["objects"]) != 1
            or not method["objects"][0].endswith(": deepGotoAndPlay)")):
        raise ValueError("Repeating long-note source does not call the literal original loop")
    return {"tagIndex": tag_index, "sourceFrame": frame_index, "playbackFrames": frame_index,
            "sourceBytecode": tag.bytecode.as_dict(), "sourceBoundary": [t.as_dict() for t in boundary]}


def source22_root_repeat_control(movie):
    """Bound only the six original22 global self-jumps, without shortening them.

    Unlike26's aeplib method call, these movies invoke the current clip's global
    deepGotoAndPlay('loop'). The native22 caller counts all27/25 source frames.
    Keep the final removal frame; the public loop/one-shot declaration owns the
    next iteration. Initial sizing/visibility scripts still run in the evaluator.
    """
    match = re.fullmatch(r"lnote00_lo_(?:siro|ao|scr)(_push)?", movie.name)
    expected_frames = 25 if match and match[1] else 27
    if (not match or len(movie.frames) != expected_frames or movie.fps != 60
            or movie.labels != {"loop": 0}):
        raise ValueError("22 long-note root differs from its qualified six-movie timeline")
    actions = [(fi, ti, movie.tags[ti]) for fi, frame in enumerate(movie.frames)
               for ti in range(frame.start_tag_offset, frame.start_tag_offset + frame.num_tags)
               if type(movie.tags[ti]).__name__ == "AP2DoActionTag"]
    if [fi for fi, _, _ in actions] != [0, 0, expected_frames - 1]:
        raise ValueError("22 long-note root must retain its two initial actions and one terminal self-jump")
    frame_index, tag_index, tag = actions[-1]
    boundary = movie.tags[movie.frames[-1].start_tag_offset:
                          movie.frames[-1].start_tag_offset + movie.frames[-1].num_tags]
    bytecode = tag.bytecode.as_dict()
    operations = bytecode["actions"]
    if (len(boundary) != 2 or type(boundary[0]).__name__ != "AP2RemoveObjectTag" or boundary[-1] is not tag
            or boundary[0].object_id != 0 or boundary[0].depth != (2 if match[1] else 14)
            or [a["action"] for a in operations] != ["PUSH", "CALL_FUNCTION", "END", "END"]
            or operations[0]["objects"] != ["'loop'", "1", "StringConstant(0x815: deepGotoAndPlay)"]):
        raise ValueError("22 long-note root has an unqualified terminal global self-jump")
    return {"tagIndex": tag_index, "sourceFrame": frame_index, "loopStartFrame": 0,
            "playbackFrames": expected_frames, "method": "global.deepGotoAndPlay",
            "sourceBytecode": bytecode, "sourceBoundary": [t.as_dict() for t in boundary],
            "timing": "Retain every declared source frame, including the final removal; no26 terminal-frame shrink"}


class StandardBranch(Branch):
    """Same public contribution format; four original judgement palettes."""

    def __init__(self, owner, category, choice, suffix="base"):
        super().__init__(owner, category, choice, suffix)
        self.completion_outro = 0

    def finish(self):
        manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                    "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1",
                    "resources": list(self.resources.values()), "bitmapFonts": self.fonts,
                    "animations": list(self.animations.values())}
        scene = {"contract": "oms-gameplay-skin-scene.v1", "root": node(self.prefix + ".root", blend="inherit", children=self.roots),
                 "tracks": self.tracks, "stateMachines": self.machines, "bindings": self.bindings,
                 "variants": self.variants, "templates": self.templates, "instances": self.instances}
        if self.completion_outro:
            scene["completionOutro"] = self.completion_outro
        self.scene = scene
        if len(self.resources) > 256 or len(self.fonts) > 4:
            raise ValueError(f"Standard contribution exceeds its local resource/font budget: {self.prefix}")
        self.owner.json(self.base + "/manifest.json", manifest)
        self.owner.json(self.base + "/scene.json", scene)
        ini = "\n".join(self.ini) + "\n"
        ini_path = "gameplay/custom/materials/m" + hashlib.sha256(ini.encode()).hexdigest()[:24] + ".ini"
        self.owner.text(ini_path, ini)
        self.owner.branch_counts.append({"branch": self.base, "resources": len(self.resources),
                                         "fonts": len(self.fonts), "animations": len(self.animations)})
        return {"manifest": self.base + "/manifest.json", "scene": self.base + "/scene.json", "ini": ini_path}


class StandardComponents(ModuleBuilder):
    def __init__(self, library, output, bemani_root, image_tools, frame_maps=None):
        self.library = library.resolve(strict=True)
        self.output = output.resolve()
        self.bemani_root, self.image_tools = bemani_root.resolve(strict=True), image_tools.resolve(strict=True)
        self.frame_maps = frame_maps.resolve(strict=True) if frame_maps else None
        if self.output.exists() or self.output.drive.casefold() == "c:":
            raise ValueError("Use a new non-system output directory")
        if self.output.is_relative_to(self.library) or self.library.is_relative_to(self.output):
            raise ValueError("Component output and source library must not overlap")
        data = read_json(self.library / "library.json")
        if data["status"] != "complete" or data["sourceMetadataUnchanged"] is not True:
            raise ValueError("Use a complete independently verified source selection")
        self.source_library_sha = digest(self.library / "library.json")
        self.source_data = data
        self.frame_ids = tuple(frame_id(style) for style in source_styles(data))
        self.source_families = source_families(data)
        self.packages = {row["source"].removeprefix("contents/data/graphic/").removesuffix(".ifs"): row
                         for row in data["packages"]}
        actual_frames = {key for key in self.packages if Path(key).name.startswith("gmframe")}
        if actual_frames != {package_key(style) for style in retained_styles(data)}:
            raise ValueError("Component library differs from its qualified Standard frame selection")
        self.index = {}
        samplers = {}
        for row in data["textures"]:
            key = row["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs")
            if key not in samplers:
                xml = ET.parse(self.library / "packages/contents/data/graphic" / key / "raw/tex/texturelist.xml").getroot()
                samplers[key] = {img.attrib["name"]: {"mag": atlas.attrib["mag_filter"], "min": atlas.attrib["min_filter"],
                    "wrapS": atlas.attrib["wrap_s"], "wrapT": atlas.attrib["wrap_t"]}
                    for atlas in xml.findall("texture") for img in atlas.findall("image")}
            self.index[key, Path(row["name"]).stem] = {**row, "sampler": samplers[key][Path(row["name"]).stem]}
        self.output.mkdir(parents=True)
        self.files, self.used_assets, self.receipts, self.branch_counts = {}, {}, [], []
        self.modules, self.options, self.clip_cache, self.atlas_cache = [], [], {}, {}
        self.contexts = list(material_contexts())
        self.tools = load_tools(self.bemani_root, self.image_tools)
        self.SourceSWF, self.StateRenderer = exporter_classes(self.tools)
        self.package_cache = {}
        self.preview_sources = {}

    def _package(self, package):
        if package in self.package_cache:
            return self.package_cache[package]
        raw = self.library / "packages/contents/data/graphic" / package / "raw"
        archive = self.library / "source-copies" / self.packages[package]["source"]
        if digest(archive) != self.packages[package]["sha256"]:
            raise ValueError(f"Verified source archive changed: {package}")
        rows = [row for (key, _), row in self.index.items() if key == package]
        names = {Path(row["name"]).stem: i for i, row in enumerate(rows)}
        atlases = {}
        for atlas in ET.parse(raw / "tex/texturelist.xml").getroot().findall("texture"):
            for image in atlas.findall("image"):
                atlases[image.attrib["name"]] = [int(v) for v in atlas.findtext("size").split()]
        shapes, geometries, mapping, movies, hashes = {}, [], {}, {}, {}
        captured = io.StringIO()
        filters = InternTable()
        with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
            for path in sorted((raw / "geo").glob("*")):
                if not path.is_file():
                    continue
                shape = self.tools.Shape(path.name, path.read_bytes())
                shape.parse(text_obfuscated=False)
                shapes[path.name] = shape
                hashes[path.relative_to(self.library).as_posix()] = digest(path)
                draws = []
                for draw in shape.draw_params:
                    uv, texture_id = [], None
                    unresolved = None
                    if draw.region is not None:
                        if draw.region not in names:
                            unresolved = draw.region
                        else:
                            texture_id = names[draw.region]
                            row = rows[texture_id]
                            if draw.flags & 0x40:
                                uv = [[p.x, p.y] for p in shape.tex_points]
                            else:
                                w, h = atlases[draw.region]
                                uv = [[(p.x * w - row["uvRect"][0]) / row["width"],
                                       (p.y * h - row["uvRect"][2]) / row["height"]] for p in shape.tex_points]
                    value = {"texture": texture_id, "uv": uv, "indices": list(draw.vertexes), "flags": draw.flags,
                             "sourceColour": rgba_values(draw.blend, (1, 1, 1, 1)) if draw.blend is not None else None}
                    if unresolved:
                        value["unresolvedTexture"] = unresolved
                    draws.append(value)
                mapping[path.name] = len(geometries)
                geometries.append({"source": path.name, "vertices": [point_values(p) for p in shape.vertex_points],
                    "atlasUV": [point_values(p) for p in shape.tex_points],
                    "colours": [rgba_values(c, (1, 1, 1, 1)) for c in shape.tex_colors], "draws": draws})
            for path in sorted((raw / "afp").glob("*")):
                if not path.is_file() or path.suffix == ".xml":
                    continue
                bsi = raw / "afp/bsi" / path.name
                movie = self.SourceSWF(path.name, path.read_bytes(), bsi.read_bytes() if bsi.is_file() else b"")
                movie.parse()
                movies[path.name] = movie
                hashes[path.relative_to(self.library).as_posix()] = digest(path)
                if bsi.is_file():
                    hashes[bsi.relative_to(self.library).as_posix()] = digest(bsi)
                for block in movie.export_filter_blocks:
                    filters.add(block)
        result = {"rows": rows, "shapes": shapes, "geometry": geometries, "mapping": mapping, "movies": movies,
                  "filters": filters, "hashes": hashes, "parseWarnings": captured.getvalue().splitlines()}
        self.package_cache[package] = result
        return result

    def clip(self, package, movie, prefix, *, loop, trigger):
        key = (package, movie, prefix, loop, trigger)
        if key in self.clip_cache:
            return self.clip_cache[key]
        source = self._package(package)
        selected = source["movies"][movie]
        controls, flow_tags = root_loop_control(selected)
        repeat = terminal_repeat_control(selected) if package == "lnote00" and movie != "notes_break" else None
        if repeat:
            flow_tags.add(id(selected.tags[repeat["tagIndex"]]))
        source22_repeat = source22_root_repeat_control(selected) if package == "source22/lnote00" else None
        if source22_repeat:
            flow_tags.add(id(selected.tags[source22_repeat["tagIndex"]]))
        if controls and (trigger != "input.key.down" or loop):
            raise ValueError("Original held loop requires the native input trigger")

        class TimelineRenderer(self.StateRenderer):
            def _AFPRenderer__place(self, tag, clip, prefix=""):
                if id(tag) in flow_tags and clip is self._AFPRenderer__root:
                    return None, False
                return super()._AFPRenderer__place(tag, clip, prefix)

        script_mask = None
        if package == "source24/fcombo00":
            from standard_script_masks import masked_renderer
            script_mask = {"adapter": "direct-child-solid-rectangle.v1", "rectangles": [], "evaluatedMaskFrames": 0}
            TimelineRenderer = masked_renderer(TimelineRenderer, self.tools, source["geometry"], source["mapping"], script_mask)
        captured = io.StringIO()
        recorder = LocalStateRecorder(self.tools, TimelineRenderer)
        with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
            root = recorder.record(package, movie, selected, source["movies"], source["shapes"], source["mapping"], source["filters"],
                                   frame_count=repeat["playbackFrames"] if repeat else None)
        warnings = sorted(set(source["parseWarnings"] + captured.getvalue().splitlines()))
        unsupported = [line for line in warnings if line.startswith("WARNING:")
                       and "includes unparsed options and might not display properly!" not in line
                       and line != "WARNING: Root clip requested to play not in top-left corner!"]
        if unsupported:
            raise ValueError(f"Unsupported source movie {package}/{movie}: {'; '.join(unsupported)}")
        category = prefix.split(".")[0]
        resources = {i: f"{category}.a.t{row['sha256'][:24]}" for i, row in enumerate(source["rows"])}
        clip = to_recorded_animation({"geometries": source["geometry"], "filters": source["filters"].values}, root, resources)
        clip["loop"], clip["trigger"] = loop, trigger
        if controls:
            clip["heldLoop"], clip["releaseFrame"] = controls[0]["heldLoop"], controls[0]["releaseFrame"]
        location = selected.location
        stage = [location.left, location.top, location.right, location.bottom]
        if stage[0] or stage[1]:
            for frame in clip["frames"]:
                if frame["camera"]:
                    frame["camera"][0] -= stage[0]
                    frame["camera"][1] -= stage[1]
            index = len(clip["states"])
            clip["states"].append({"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, -stage[0], -stage[1], 0],
                "origin": [0, 0, 0], "projection": "affine", "multiply": [1, 1, 1, 1], "add": [0, 0, 0, 0],
                "hsl": [0, 0, 0], "mask": None, "colourMatrix": None})
            clip["nodes"] = [{"parent": -1, "geometry": -1, "blend": "normal"}] + [
                {**n, "parent": n["parent"] + 1} for n in clip["nodes"]]
            for frame in clip["frames"]:
                if frame["nodes"]:
                    frame["nodes"] = [[0, index]] + [[n + 1, s] for n, s in frame["nodes"]]
        used = {g["resource"] for g in clip["geometries"] if g["resource"] is not None}
        declared = {}
        atlas_sources = {}
        use_atlas = (package in ("lnote00", "source22/lnote00") and trigger != "gameplay.attach") or re.fullmatch(r"(?:source24/)?fcombo\d{2}", package) is not None
        for i, row in enumerate(source["rows"]):
            if resources[i] in used:
                if use_atlas:
                    sampler = row["sampler"]
                    if sampler != {"mag": "nearest", "min": "nearest", "wrapS": "clamp", "wrapT": "clamp"}:
                        raise ValueError("Original movie atlas requires its original nearest/clamp sampler")
                    original = within(self.library, row["sprite"])
                    if digest(original) != row["sha256"]:
                        raise ValueError("Original movie atlas source changed")
                    declaration = {"id": resources[i], "type": "texture", "path": row["sprite"], "filter": "nearest"}
                    atlas_sources[row["sprite"]] = {"source": str(original), "path": row["sprite"], "sha256": row["sha256"]}
                else:
                    declaration, _ = self.asset(category, package, Path(row["name"]).stem)
                declared[declaration["id"]] = declaration
        atlas_receipt = None
        if use_atlas:
            atlas_dir = self.output / "research" / prefix
            atlas_dir.mkdir(parents=True, exist_ok=True)
            declared, atlas_files, atlas_receipt = self.original_pixel_atlas(clip, declared, atlas_sources, prefix, atlas_dir)
            for item in atlas_files.values():
                self.copy_file(Path(item["source"]), item["path"], item["sha256"])
            used = {g["resource"] for g in clip["geometries"] if g["resource"] is not None}
        import brotli
        encoded = json.dumps(clip, separators=(",", ":"), allow_nan=False).encode()
        compressed = brotli.compress(encoded, quality=5)
        if len(compressed) > 16 * 1024**2 or len(encoded) > 128 * 1024**2:
            raise ValueError("Source animation exceeds the public recorded-clip byte limit")
        relative = f"custom/{prefix}/clip.json.br"
        target = self.output / relative
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(compressed)
        self.files[relative] = {"path": relative, "sha256": digest(target), "bytes": target.stat().st_size}
        animation_id = prefix + ".clip"
        result = {"node": node_for_reference(prefix, animation_id, stage, stage), "resources": list(declared.values()),
                  "animations": [{"id": animation_id, "path": relative, "resources": sorted(used)}],
                  "files": [], "source": {"package": self.packages[package]["source"], "movie": movie,
                    "stage": stage, "fps": selected.fps, "sourceFrames": len(selected.frames),
                    "durationMs": (len(clip["frames"]) - 1) * 1000 / selected.fps,
                    "labels": selected.labels, "translatedRootControl": controls,
                    **({"translatedTerminalRepeat": repeat} if repeat else {}),
                     **({"translatedSource22RootRepeat": source22_repeat} if source22_repeat else {}),
                    **({"translatedScriptMask": script_mask} if script_mask else {})},
                  "receipt": {"sourceArchiveSHA256": self.packages[package]["sha256"], "rawSHA256": source["hashes"],
                    "sourcePNGBytesPreserved": not use_atlas, "sourcePixelsPreserved": True,
                    "losslessSourcePixelAtlas": atlas_receipt, "runtimeInterpreter": False,
                    "hierarchyMathChecks": recorder.math_checks, "maximumHierarchyMathError": recorder.math_max_error,
                    "framesIncludingEnd": len(clip["frames"]), "sourceNodeCount": len(clip["nodes"]), "warnings": warnings}}
        research = self.output / "research" / prefix
        research.mkdir(parents=True, exist_ok=True)
        (research / "source-root.json").write_text(json.dumps(root, separators=(",", ":")), encoding="utf-8")
        (research / "receipt.json").write_text(json.dumps({"source": result["source"], "receipt": result["receipt"]},
                                                       separators=(",", ":")), encoding="utf-8")
        self.clip_cache[key] = result
        self.receipts.append({"category": category, "kind": "original-recorded-movie", "source": result["source"],
                              "receipt": result["receipt"]})
        return result

    def original_pixel_atlas(self, clip, resources, files, prefix, directory):
        """Pack exact source pixels while checking every non-reference movie field."""
        original = copy.deepcopy(clip)
        packed, images, receipt = self.pack_large_lane_clip(clip, resources, files, prefix, directory)
        restored = copy.deepcopy(clip)
        maximum_uv_error = 0
        mapping = {source["originalResource"]: (*source["rect"], *page["size"], page["resource"])
                   for page in receipt["pages"] for source in page["sources"]}
        for before, after, restore in zip(original["geometries"], clip["geometries"], restored["geometries"], strict=True):
            if before["resource"] is None:
                continue
            left, top, iw, ih, width, height, atlas = mapping[before["resource"]]
            if after["resource"] != atlas:
                raise ValueError("Atlas geometry no longer refers to its original pixel rectangle")
            restore["resource"] = before["resource"]
            for source_vertex, mapped_vertex, restored_vertex in zip(before["vertices"], after["vertices"], restore["vertices"], strict=True):
                error = max(abs((mapped_vertex[3] * width - left) / iw - source_vertex[3]),
                            abs((mapped_vertex[4] * height - top) / ih - source_vertex[4]))
                maximum_uv_error = max(maximum_uv_error, error)
                if error > 1e-12:
                    raise ValueError("Atlas UV rebasing changed the original texture coordinates")
                restored_vertex[3:5] = source_vertex[3:5]
        if restored != original:
            raise ValueError("Lossless atlas changed original XYZ, topology, state or frame data")
        receipt.update({"sourceResources": len(resources), "atlasResources": len(packed),
                        "maximumInverseUVError": maximum_uv_error,
                        "allXYZTopologyStatesFramesAndMetadataUnchanged": True})
        return packed, images, receipt

    def preview(self, module, value, originals):
        from PIL import Image, ImageDraw
        shared = "lane-cover" if module in ("lift-art", "graph-area") and value != "blank" else module
        relative = f"previews/{shared}/{value}.png"
        if relative in self.files:
            return relative
        canvas = Image.new("RGBA", (256, 160), (12, 16, 21, 255))
        draw = ImageDraw.Draw(canvas)
        draw.text((8, 6), shared + " " + value, fill=(190, 202, 215))
        originals = list(originals)
        if originals:
            cell = 240 / len(originals)
            for index, (package, name) in enumerate(originals):
                row = self.index[package, name]
                path = within(self.library, row["sprite"])
                if digest(path) != row["sha256"]:
                    raise ValueError("Preview source changed")
                with Image.open(path) as image:
                    image = image.convert("RGBA")
                scale = min((cell - 6) / image.width, 126 / image.height, 4)
                image = image.resize((max(1, round(image.width * scale)), max(1, round(image.height * scale))), Image.Resampling.NEAREST)
                canvas.alpha_composite(image, (round(8 + cell * index + (cell - image.width) / 2), 27 + (126 - image.height) // 2))
        path = self.output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        canvas.save(path)
        if path.stat().st_size > 256 * 1024:
            raise ValueError("Preview exceeds public image limit")
        self.files[relative] = {"path": relative, "sha256": digest(path), "bytes": path.stat().st_size}
        self.preview_sources[relative] = [{"package": p, "texture": n, "sha256": self.index[p, n]["sha256"]} for p, n in originals]
        return relative

    def _note(self, branch, colour, slot, texture, *, body=False, appearance=None, package="notes00"):
        resource, _, row = branch.asset(package, texture)
        part = slot.rsplit(".", 1)[1]
        for ruleset in ("bms", "mania"):
            suffix = colour + "." + part + "." + ruleset
            marker_package = package if package == "source22/notes_cn00" else "notes00" if slot == "object.note" else "notes_cn00"
            marker = self.fallback_marker(branch.prefix.split(".")[0], suffix, marker_package, texture)
            # Mania native notes use a 48-unit width-derived marker carrier;
            # BMS uses a 22.5-unit carrier. Bodies keep native time geometry.
            marker_row = self.index[marker_package, texture]
            carrier = 22.5 if ruleset == "bms" else 48 * marker_row["height"] / marker_row["width"]
            props = {} if body else {"height": row["height"] * SOURCE_TO_LOGICAL / carrier,
                "y": -SOURCE_HIT_OFFSET * SOURCE_TO_LOGICAL / carrier, "anchor": "bottom-left", "origin": "bottom-left"}
            identifier = branch.prefix + "." + suffix
            branch.template(suffix, node(identifier, "sprite", props, resource, slot), slot, marker)
            if appearance:
                source, cases = appearance
                branch.variant(identifier, source, resource, {key: branch.asset(package, name)[0] for key, name in cases.items()})
            yield ruleset, marker

    def notes(self):
        self.auto_scratch_option()
        choices = []
        for style in exact_styles(self.packages, r"notes(\d+)", range(12)):
            package = f"notes{style:02}"
            branch = StandardBranch(self, "sn", f"n{style:02}")
            materials = {r: {} for r in ("bms", "mania")}
            for colour in COLOURS:
                texture = "notes_" + ("turn" if colour == "scratch" else colour)
                for ruleset, marker in self._note(branch, colour, "object.note", texture, package=package):
                    materials[ruleset][colour] = {"object.note": marker}
            for ruleset, material in materials.items():
                branch.lane_ini(material, ruleset)
            contribution = branch.finish()
            hidden = self.auto_scratch_hidden(branch.scene, contribution)
            choices.append({"value": f"n{style:02}", "label": f"Notes {style:02}", **contribution,
                "variants": [{"when": {"auto-scratch-display": False}, **hidden}],
                "preview": self.preview("notes", f"n{style:02}", [(package, "notes_" + c) for c in ("white", "black", "turn")])})
        self.add_module("notes", "Notes", choices, "n00")
        self.receipts.append({"category": "notes", "sourcePackages": 12, "sourcePixelToLogical": SOURCE_TO_LOGICAL,
            "sourceHitOffset": SOURCE_HIT_OFFSET, "hitOffsetStatus": "OMS alignment requiring original-game/Player comparison",
            "sizeVariants": "Only the twelve actual normal archives exist; no imported Lightning sizes or synthetic design choices",
            "maniaCarrier": "48 * original marker height / width", "bmsCarrier": 22.5})

    def auto_scratch_option(self):
        if not any(option["id"] == "auto-scratch-display" for option in self.options):
            self.options.append({"id": "auto-scratch-display", "type": "toggle", "label": "Auto-Scratch notes", "default": True})

    def auto_scratch_hidden(self, scene, contribution):
        scene = copy.deepcopy(scene)
        slots = {"object.note", "object.long-note.head", "object.long-note.body", "object.long-note.tail"}
        for template in scene["templates"]:
            target = template["root"]
            if target.get("slot") not in slots:
                continue
            identifier = target["id"] + ".ascr"
            scene["variants"].append({"id": identifier, "target": target["id"], "property": "visible",
                "source": "object.auto-assist", "default": True,
                "cases": [{"id": identifier + ".scratch", "key": "scratch", "value": False}]})
        path = contribution["scene"].removesuffix("scene.json") + "auto-scratch-off.scene.json"
        self.json(path, scene)
        return {**contribution, "scene": path}

    def charge_notes(self):
        self.auto_scratch_option()
        self.options.append({"id": "hcn-display", "type": "choice", "label": "HCN display type", "default": "hcn",
                             "choices": [{"value": "hcn", "label": "HCN"}, {"value": "cn", "label": "Charge Note"}]})
        contributions = {}
        for display in ("hcn", "cn"):
            branch = StandardBranch(self, "sc", "c00", display)
            materials = {r: {} for r in ("bms", "mania")}
            for colour in COLOURS:
                stem = "notes_bss" if colour == "scratch" else "notes_cn_" + colour
                second = ("notes_bss2" if colour == "scratch" else "notes_cn2_" + colour) if display == "hcn" else stem
                for ruleset in materials:
                    materials[ruleset][colour] = {}
                for part, suffix in (("head", "start"), ("body", "beam0"), ("tail", "end")):
                    texture = stem + "_" + suffix
                    if part == "body":
                        appearance = ("object.long-note-appearance", {"ln": texture, "cn": texture,
                            "ln-holding": stem + "_beam1", "cn-holding": stem + "_beam1",
                            "ln-released": stem + "_beam2", "cn-released": stem + "_beam2",
                            "hcn": second + "_beam0", "hcn-holding": second + "_beam1",
                            "hcn-released": second + ("_beam4" if display == "hcn" else "_beam2")})
                    else:
                        appearance = ("object.long-note-mode", {"ln": texture, "cn": texture, "hcn": second + "_" + suffix})
                    slot = "object.long-note." + part
                    for ruleset, marker in self._note(branch, colour, slot, texture, body=part == "body",
                                                    appearance=appearance, package="notes_cn00"):
                        materials[ruleset][colour][slot] = marker
            self.long_note_effects(branch, materials)
            for ruleset, material in materials.items():
                branch.lane_ini(material, ruleset)
            contributions[display] = branch.finish()
            contributions[display + "-hidden"] = self.auto_scratch_hidden(branch.scene, contributions[display])
        self.add_module("charge-notes", "Charge Notes / HCN", [{"value": "c00", "label": "Original 00", **contributions["hcn"],
            "variants": [{"when": {"hcn-display": "cn", "auto-scratch-display": True}, **contributions["cn"]},
                         {"when": {"hcn-display": "hcn", "auto-scratch-display": False}, **contributions["hcn-hidden"]},
                         {"when": {"hcn-display": "cn", "auto-scratch-display": False}, **contributions["cn-hidden"]}],
            "preview": self.preview("charge-notes", "c00", [("notes_cn00", n) for n in ("notes_cn_white_start", "notes_cn2_white_start", "notes_bss_start")])}], "c00")
        self.receipts.append({"category": "charge-notes", "sourcePackage": "notes_cn00", "choices": 1,
            "mapping": "cn/bss = LN/CN; cn2/bss2 = HCN; body states use actual native appearance and original beam0/1/2/4",
            "displayType": "26 official customization permits HCN/BSS appearance to use CN art; the real chart mode and held/released effects remain HCN",
            "officialReference": "https://p.eagate.573.jp/game/2dx/26/howto/new/custom_new.html",
            "autoScratchDisplay": "26 ASCR note visibility selects only exact native object.auto-assist=scratch. Ordinary notes, full Autoplay, LN effects and scoring eligibility are unchanged.",
            "bodyLength": "Native time/scroll geometry is unchanged; only original head/tail sprite geometry is normalized"})
        if includes22(self.source_data):
            self.charge_notes22()

    def charge_notes22(self):
        """Original22 CN sprites and six movies, with host HCN using CN art."""
        branch = StandardBranch(self, "sc22", "c22")
        materials = {r: {} for r in ("bms", "mania")}
        for colour in COLOURS:
            stem = "notes_bss" if colour == "scratch" else "notes_cn_" + colour
            for ruleset in materials:
                materials[ruleset][colour] = {}
            for part, suffix in (("head", "start"), ("body", "beam0"), ("tail", "end")):
                texture = stem + "_" + suffix
                appearance = ("object.long-note-appearance", {
                    state: stem + "_" + value for states, value in (
                        (("ln", "cn", "hcn"), "beam0"),
                        (("ln-holding", "cn-holding", "hcn-holding"), "beam1"),
                        (("ln-released", "cn-released", "hcn-released"), "beam2"))
                    for state in states}) if part == "body" else (
                        "object.long-note-mode", {mode: texture for mode in ("ln", "cn", "hcn")})
                slot = "object.long-note." + part
                for ruleset, marker in self._note(branch, colour, slot, texture, body=part == "body",
                                                  appearance=appearance, package="source22/notes_cn00"):
                    materials[ruleset][colour][slot] = marker
        self.long_note_effects(branch, materials, source22=True)
        for ruleset, material in materials.items():
            branch.lane_ini(material, ruleset)
        contribution = branch.finish()
        hidden = self.auto_scratch_hidden(branch.scene, contribution)
        module = next(m for m in self.modules if m["id"] == "charge-notes")
        module["choices"].append({"value": "c22", "label": "Charge Notes (PENDUAL)", **contribution,
            "variants": [{"when": {"auto-scratch-display": False}, **hidden}],
            "preview": self.preview("charge-notes", "c22", [("source22/notes_cn00", n) for n in
                ("notes_cn_white_start", "notes_cn_black_start", "notes_bss_start")])})
        self.receipts.append({"category": "charge-notes22", "sourcePackage": "source22/notes_cn00",
            "effectPackage": "source22/lnote00", "sourceMovies": 6,
            "state": "Original base27 frames once on actual hold entry/exit; push25 frames loops only while held. The22 source has no HCN push/break movies.",
            "hostAdaptation": "Host LN, CN and HCN all use22 CN appearance and its held effect; real note mode/scoring is unchanged. The26 HCN display choice does not replace this source.",
            "size": "Original22 movie scale retained; the26 explosion-size control is not applied to22 CN effects."})

    def long_note_effects(self, branch, materials, *, source22=False):
        """Source-specific caller layers driven by the exact native hold owner."""
        effect_package = "source22/lnote00" if source22 else "lnote00"
        note_package = "source22/notes_cn00" if source22 else "notes_cn00"
        effect_prefix = "sc22" if source22 else "sc"
        for colour, family in (("white", "siro"), ("black", "ao"), ("scratch", "scr")):
            movies = (("base", f"lnote00_lo_{family}", False, "object.long-note-transition", None),
                      ("cn", f"lnote00_lo_{family}_push", True, "object.state", ["ln-holding", "cn-holding"]),
                      ("hcn", f"lnote00_lo_{family}2_push", True, "object.state", ["hcn-holding"]),
                      ("break", "notes_break", True, "object.state", ["hcn-released"]))
            if source22:
                movies = (("base", f"lnote00_lo_{family}", False, "object.long-note-transition", None),
                          ("cn", f"lnote00_lo_{family}_push", True, "object.state",
                           ["ln-holding", "cn-holding", "hcn-holding"]))
            for ruleset in ("bms", "mania"):
                identifier = branch.prefix + ".effect." + ruleset + "." + colour
                props = {"width": 4, "height": 4} if ruleset == "bms" else {
                    "width": 4, "height": 90 / (REFERENCE_STAGE_HEIGHT - 1),
                    "y": (REFERENCE_STAGE_HEIGHT - 1 - 22.5) / (REFERENCE_STAGE_HEIGHT - 1)}
                children = []
                for role, movie, loop, trigger, appearances in movies:
                    exported = self.clip(effect_package, movie, effect_prefix + ".clip.effect." + ("break" if role == "break" else colour + "." + role),
                                         loop=loop, trigger=trigger)
                    art = branch.include_clip(exported)
                    art["id"] = identifier + "." + role + ".art"
                    stage = exported["source"]["stage"]
                    sw, sh = (stage[2] - stage[0]) * SOURCE_TO_REFERENCE, (stage[3] - stage[1]) * SOURCE_TO_REFERENCE
                    art["properties"] = {"x": .5, "y": .5, "origin": "centre",
                        "width": sw / (SOURCE_WIDTHS[colour] * SOURCE_TO_REFERENCE * 4), "height": sh / 90}
                    for dimension, scale in (("width", "scale-x"), ("height", "scale-y")):
                        if art["properties"][dimension] > 4:
                            art["properties"][scale], art["properties"][dimension] = art["properties"][dimension] / 4, 4
                    positioned = node(identifier + "." + role, properties={"x": .125, "y": 17.5 / 90, "origin": "centre"}, children=[art])
                    if role == "base" and not source22:
                        branch.bind(positioned["id"], "scale-x", "option.explosion-scale")
                        branch.bind(positioned["id"], "scale-y", "option.explosion-scale")
                    elif role != "base":
                        branch.show(positioned["id"], "object.long-note-appearance", appearances)
                    children.append(positioned)
                marker = self.fallback_marker(effect_prefix, "effect-" + ruleset + "-" + colour, note_package,
                    "notes_bss_start" if colour == "scratch" else "notes_cn_" + colour + "_start")
                branch.template("effect." + ruleset + "." + colour,
                    node(identifier, properties=props, slot="effect.long-note", children=children), "effect.long-note", marker)
                materials[ruleset][colour]["effect.long-note"] = marker
        if source22:
            return
        self.receipts.append({"category": "long-note-effect", "sourcePackage": "lnote00", "nativeCallerMovies": 10,
            "sourceMovies": 13, "unusedTopLevelMovies": "The three base2 movies are source inventory, not dispatched by the26 executable",
            "state": "base once on actual Holding entry/exit; original _push loops while LN/CN held,2_push while HCN held; notes_break loops only for HCN released. Direct head miss has no invented base burst.",
            "origin": "All movies use their original canvas centre, proved by26 caller anchor(3,3). Source hit position480 maps to the existing native target at17.5/22.5; the one-reference-pixel field edge is retained.",
            "scale": "Original seven explosion sizes affect base only, never continuous push/break",
            "tail": "Real terminal object state stops continuous layers and retains the original bounded base outro; no ordinary key or judgement trigger",
            "adaptation": "OMS Standard LN and mania use the original CN held family; HCN effects are selected only from actual native HCN mode"})

    def beams(self):
        choices = []
        for style in exact_styles(self.packages, r"beam(\d+)", range(11)):
            package = f"beam{style:02}"
            branch = StandardBranch(self, "sb", f"b{style:02}")
            materials = {}
            for colour in COLOURS:
                name = "turn" if colour == "scratch" else colour
                exported = self.clip(package, "beam_" + name, f"sb.clip.n{style:02}.{colour}", loop=False, trigger="input.key.down")
                root = branch.include_clip(exported)
                root["id"], root["slot"] = branch.prefix + "." + colour, "playfield.lane-surface"
                root["properties"] = {"height": (exported["source"]["stage"][3] - exported["source"]["stage"][1]) / SOURCE_STAGE_HEIGHT}
                marker = self.fallback_marker("sb", colour, "beam00", "playm_bar_" + name)
                branch.template(colour, root, "playfield.lane-surface", marker)
                materials[colour] = {"playfield.lane-surface": marker}
            branch.lane_ini(materials)
            names = [n for key, n in self.index if key == package]
            preview = [(package, n) for n in names[:3]]
            choices.append({"value": f"b{style:02}", "label": f"Key beam {style:02}", **branch.finish(),
                            "preview": self.preview("key-beam", f"b{style:02}", preview)})
        self.add_module("key-beam", "Key beam", choices, "b00")
        self.receipts.append({"category": "key-beam", "choices": 11, "layer": "Native underlay playfield.lane-surface",
            "loop": "Only actual per-source in/loop/out branches become heldLoop/releaseFrame; unlabelled movies retain one complete input-triggered source sequence",
            "sourceCanvasHeight": 482, "sourceStageHeight": SOURCE_STAGE_HEIGHT, "pixelsChanged": False})

    def explosions(self):
        self.options.append({"id": "explosion-scale", "type": "number", "label": "Explosion size", "default": 1,
                             "min": .7, "max": 1.3, "step": .1})
        choices = []
        for style in exact_styles(self.packages, r"exp(\d+)", range(20)):
            package = f"exp{style:02}"
            branch = StandardBranch(self, "se", f"e{style:02}")
            exported = self.clip(package, package, f"se.clip.n{style:02}", loop=False, trigger="judgement.hit")
            prototype = branch.include_clip(exported)
            stage = exported["source"]["stage"]
            sw, sh = (stage[2] - stage[0]) * SOURCE_TO_REFERENCE, (stage[3] - stage[1]) * SOURCE_TO_REFERENCE
            for ruleset in ("bms", "mania"):
                materials = {}
                for colour in COLOURS:
                    width = SOURCE_WIDTHS[colour] * SOURCE_TO_REFERENCE
                    identifier = branch.prefix + "." + ruleset + "." + colour
                    props = {"width": 4, "height": 4} if ruleset == "bms" else {
                        "width": 4, "height": 90 / (REFERENCE_STAGE_HEIGHT - 1),
                        "y": (REFERENCE_STAGE_HEIGHT - 1 - 22.5) / (REFERENCE_STAGE_HEIGHT - 1)}
                    animation = copy.deepcopy(prototype)
                    animation["id"] = identifier + ".art"
                    ap = {"x": .5, "y": .5, "origin": "centre", "width": sw / (width * 4), "height": sh / 90}
                    for dim, scale in (("width", "scale-x"), ("height", "scale-y")):
                        if ap[dim] > 4:
                            ap[scale], ap[dim] = ap[dim] / 4, 4
                    animation["properties"] = ap
                    scale_id = identifier + ".scale"
                    wrapper = node(scale_id, properties={"x": .125, "y": 17.5 / 90, "origin": "centre"}, children=[animation])
                    branch.bind(scale_id, "scale-x", "option.explosion-scale")
                    branch.bind(scale_id, "scale-y", "option.explosion-scale")
                    marker = self.fallback_marker("se", ruleset + "-" + colour, "exp00", "exp00_00")
                    branch.template(ruleset + "." + colour, node(identifier, properties=props, slot="effect.hit-explosion", children=[wrapper]),
                                    "effect.hit-explosion", marker)
                    materials[colour] = {"effect.hit-explosion": marker}
                branch.lane_ini(materials, ruleset)
            names = [n for key, n in self.index if key == package]
            choices.append({"value": f"e{style:02}", "label": f"Explosion {style:02}", **branch.finish(),
                            "preview": self.preview("explosion", f"e{style:02}", [(package, names[len(names)//2])])})
        self.add_module("explosion", "Explosion", choices, "e00")

    def lane_sources(self):
        for index in exact_styles(self.packages, r"lane(\d+)", range(167)):
            yield f"lane{index:03}", f"lane{index:03}", f"l{index:03}", f"{index:03}"
        if includes22(self.source_data):
            for index in (27, 40, 58, 68, 86, 87, 88, 89):
                yield f"source22/lane{index:03}", f"lane{index:03}", f"l22-{index:03}", f"PENDUAL {index:03}"

    def static_lanes(self):
        choices = []
        for package, texture, value, label in self.lane_sources():
            branch = StandardBranch(self, "sl", value)
            resource = branch.asset(package, texture, "sl")[0]
            marker = self.fallback_marker("sl", "cover", "lane000", "lane000")
            branch.template("art", node(branch.prefix + ".art", "sprite", resource=resource, slot="playfield.lane-cover.fill"),
                            "playfield.lane-cover.fill", marker)
            branch.stage_ini("playfield.lane-cover.fill", marker)
            choices.append({"value": value, "label": "Lane Cover " + label, **branch.finish(),
                            "preview": self.preview("lane-cover", value, [(package, texture)])})
        self.add_module("lane-cover", "Lane Cover", choices, "l000")

    def lift_art(self):
        choices = []
        for package, texture, value, label in self.lane_sources():
            branch = StandardBranch(self, "sv", value)
            black, marker = self.control_black(branch)
            resource = branch.asset(package, texture, "sl")[0]
            root = node(branch.prefix + ".fill", slot="playfield.lift.fill", children=[
                node(branch.prefix + ".black", "sprite", resource=black),
                node(branch.prefix + ".art", "sprite", {"x": 2 / 292, "width": 288 / 292}, resource)])
            branch.template("fill", root, "playfield.lift.fill", marker)
            for context in self.contexts:
                if context["ruleset"] == "bms":
                    branch.ini.extend(("[GameplaySkin.Bms:1]", "Target: Stage " + context["stage"],
                                       f'playfield.lift.fill: resource Provide "{marker}"'))
            choices.append({"value": value, "label": "LIFT art " + label, **branch.finish(),
                            "preview": self.preview("lift-art", value, [(package, texture)])})
        self.add_module("lift-art", "LIFT", choices, "l000")
        self.modules[-1].update(rulesets=["bms"], keymodes=["5k", "7k", "14k"])
        self.receipts.append({"category": "lift-art", "choices": len(choices),
            "geometry": "Native BMS full stage fill clipped by actual LIFT; source288px art occupies292px stage with2px gutters",
            "adaptation": "Source cover design reused as LIFT art; no extra black choice and no fake mania Lift"})

    def judgement_atlas(self, package):
        from PIL import Image
        width = max(self.index[package, f"judge_combo_{d}{p}"]["width"] for p in "bgry" for d in range(10))
        height = max(self.index[package, f"judge_combo_{d}{p}"]["height"] for p in "bgry" for d in range(10))
        atlas = Image.new("RGBA", (width * 10, height * 4))
        glyphs, sources = {}, []
        for y, palette in enumerate("bgry"):
            for digit in range(10):
                row = self.index[package, f"judge_combo_{digit}{palette}"]
                path = within(self.library, row["sprite"])
                if digest(path) != row["sha256"]:
                    raise ValueError("Original COMBO glyph changed")
                with Image.open(path) as im:
                    im = im.convert("RGBA")
                position = (digit * width, y * height)
                atlas.paste(im, position)
                rect = [*position, im.width, im.height]
                if atlas.crop((rect[0], rect[1], rect[0] + rect[2], rect[1] + rect[3])).tobytes() != im.tobytes():
                    raise ValueError("Lossless atlas changed glyph RGBA")
                glyphs[palette, digit] = rect
                sources.append({"path": row["sprite"], "sha256": row["sha256"], "rect": rect})
        relative = f"custom/font/{package}-digits.png"
        path = self.output / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        atlas.save(path)
        self.files[relative] = {"path": relative, "sha256": digest(path), "bytes": path.stat().st_size}
        self.receipts.append({"category": "judgement", "kind": "lossless-original-four-palette-atlas", "sources": sources,
                              "path": relative, "resampled": False, "recoloured": False})
        return {"id": "sj.atlas." + package, "type": "texture", "path": relative, "filter": "nearest"}, glyphs, height

    def judgements(self):
        self.options.append({"id": "judgement-display", "type": "toggle", "label": "Judgement", "default": True})
        self.options.append({"id": "judgement-position", "type": "choice", "label": "Judgement position", "default": "normal",
            "choices": [{"value": value, "label": label} for value, label in
                        (("normal", "Normal"), ("up100", "+100"), ("up200", "+200"), ("up300", "+300"), ("off", "Off"))]})
        choices = []
        for style in exact_styles(self.packages, r"judge(\d+)", range(14)):
            package = f"judge{style:02}"
            branch = StandardBranch(self, "sj", f"j{style:02}")
            atlas, glyphs, height = self.judgement_atlas(package)
            branch.resources[atlas["id"]] = atlas
            fonts = {}
            for palette in "bgry":
                identifier = branch.prefix + ".font." + palette
                branch.fonts.append({"id": identifier, "lineHeight": height,
                    "glyphs": [{"character": str(d), "resource": atlas["id"], "rect": glyphs[palette, d],
                                "advance": JUDGEMENT_DIGIT_ADVANCE} for d in range(10)]})
                fonts[palette] = identifier
            # One source-size row, horizontally centred with its original canvas top at the native judgement position.
            # Source bitmap dimensions are not multiplied by an inherited 33 normalization.
            height_logical = height * SOURCE_TO_LOGICAL
            # The horizontal runtime centres its leaf cells inside this carrier.
            # Only relative height applies to the carrier; height-px belongs to leaves.
            row_height = height / (SOURCE_STAGE_HEIGHT * .12 * 3)
            root_id = branch.prefix + ".group"
            children = []
            for name, palette, label_name, grades in (("b", "b", "judge_p_great_blue", ["perfect"]),
                                                      ("g", "g", "judge_p_great_white", ["perfect"]),
                                                      ("r", "r", "judge_p_great_pink", ["perfect"]),
                                                      ("y-great", "y", "judge_great_yellow", ["great"]),
                                                      ("y-good", "y", "judge_good", ["good"]),
                                                      ("y-bad", "y", "judge_bad", ["meh"]),
                                                      ("y-poor", "y", "judge_poor", ["miss", "ok"])):
                pair_id = root_id + "." + name
                label_resource = branch.asset(package, label_name)[0]
                label_source = self.index[package, label_name]
                label_width = label_source["width"] * height / label_source["height"]
                native_label_width = JUDGEMENT_LABEL_WIDTHS[label_name]
                gap = native_label_width - label_width - 11
                digit_width = max(glyphs[palette, digit][2] for digit in range(10))
                # A hidden horizontal cell still occupies its natural width.
                # The inactive state therefore owns a label-only row at the same original top edge.
                no_combo = node(pair_id + ".no-combo", properties={"x": .5, "y": .5, "height": row_height,
                    "origin": "top-centre", "layout": "horizontal"},
                    children=[node(pair_id + ".no-combo.label", "sprite", {"height-px": height_logical}, label_resource)])
                rows, steps = [no_combo], []
                # Native351510 rejects BAD/POOR before drawing digits. A ruleset may
                # retain its actual combo on these mapped grades, but that does not
                # qualify the original label for a COMBO visual.
                combo_eligible = name not in ("y-bad", "y-poor")
                if combo_eligible:
                    label = node(pair_id + ".label", "sprite", {"height-px": height_logical}, label_resource)
                    combo = node(pair_id + ".combo", "text", {"text": "", "font-size": height_logical,
                                 "bitmap-font": fonts[palette], "format": "integer"})
                    branch.bind(combo["id"], "text", "combo.value")
                    pair = node(pair_id, properties={"x": .5, "y": .5, "height": row_height,
                                "origin": "top-centre", "layout": "horizontal", "gap-px": gap * SOURCE_TO_LOGICAL},
                                children=[label, combo])
                    branch.bind(pair_id, "visible", "combo.active")
                    steps = judgement_fit_steps(label_width, digit_width, gap=gap)
                    for prop in ("scale-x", "scale-y"):
                        identifier = pair_id + ".fit." + prop
                        branch.tracks.append({"id": identifier, "type": "tween", "target": pair_id, "property": prop,
                            "easing": "step", "loop": False, "clock": "combo.value",
                            "keyframes": [{"id": identifier + ".n" + str(i), "time": time, "value": value}
                                          for i, (time, value) in enumerate(steps)]})
                    branch.show(no_combo["id"], "combo.active", ["false"])
                    rows.insert(0, pair)
                phase_layer = node(pair_id + ".palette", children=rows)
                grade_gate = node(pair_id + ".grade", children=[phase_layer])
                branch.show(grade_gate["id"], "judgement.result", grades)
                if palette != "y":
                    phase = "bgr".index(palette)
                    branch.tracks.append({"id": pair_id + ".phase", "type": "tween", "target": phase_layer["id"], "property": "opacity",
                        "easing": "step", "loop": True, "trigger": "judgement.hit",
                        "keyframes": [{"id": pair_id + ".phase.n" + str(i), "time": i * 100, "value": int(i % 3 == phase)} for i in range(4)]})
                children.append(grade_gate)
                self.receipts.append({"category": "judgement", "style": style, "grade": grades, "palette": palette,
                    "sourceLabelWidth": label_width, "nativeLabelLayoutWidth": native_label_width, "labelDigitGap": gap,
                    "sourceDigitAdvance": JUDGEMENT_DIGIT_ADVANCE, "sourceDigitImageWidth": digit_width,
                    "sourceStageWidth": SOURCE_STAGE_WIDTH,
                    "fitSteps": steps, "comboEligible": combo_eligible,
                    "noCombo": "Separate horizontally centred label-only row at original size and canvas top; always used for BAD/POOR"})
            gate = node(root_id + ".display", children=children)
            branch.bind(gate["id"], "visible", "option.judgement-display")
            lift = node(root_id + ".lift", children=[gate])
            branch.tracks.append({"id": lift["id"] + ".offset", "type": "tween", "target": lift["id"],
                "property": "y", "clock": "lift.offset", "easing": "linear", "loop": False,
                "keyframes": [{"id": lift["id"] + ".zero", "time": 0, "value": 0},
                              {"id": lift["id"] + ".full", "time": 1, "value": -1 / (.12 * 3)}]})
            # Both existing authored rulesets place the C3 judgement band centre at81% of the484px field.
            # The26 native caller places the complete label/number canvas TOP at342-offset-lift.
            # Its original sprite has local bounds starting at(0,0), without a centre-origin override.
            # Public x/y use the parent slot, whose C3 judgement height is12% of the source field.
            judgement_height = SOURCE_STAGE_HEIGHT * .12
            normal_y = (342 - SOURCE_STAGE_HEIGHT * .81) / judgement_height
            # Centre a three-band canvas so full Lift and the300px setting each stay within
            # the public relative-position bound, without changing the original glyph size.
            position = node(root_id + ".position", children=[lift])
            canvas = node(root_id + ".canvas", properties={"y": -1, "height": 3}, children=[position])
            root = node(root_id, properties={"opacity": 0, "y": normal_y}, slot="hud.judgement", children=[canvas])
            marker = self.fallback_marker("sj", "judgement", "judge00", "judge_p_great_blue")
            branch.template("group", root, "hud.judgement", marker)
            branch.stage_ini("hud.judgement", marker)
            branch.machines.append({"id": root_id + ".m", "initial": root_id + ".empty",
                "states": [{"id": root_id + ".empty", "set": [{"id": root_id + ".hide", "target": root_id, "property": "opacity", "value": 0}]},
                           {"id": root_id + ".active", "set": [{"id": root_id + ".show", "target": root_id, "property": "opacity", "value": 1}]}],
                "transitions": [{"id": root_id + ".arrive", "from": root_id + ".empty", "to": root_id + ".active", "event": "judgement.hit"}]})
            contribution = branch.finish()
            positions = []
            for value, offset in (("up100", 100), ("up200", 200), ("up300", 300), ("off", 0)):
                variant_scene = copy.deepcopy(branch.scene)
                variant_root = variant_scene["templates"][0]["root"]
                variant_root["children"][0]["children"][0]["properties"]["y"] = -offset / (judgement_height * 3)
                if value == "off":
                    # The root's judgement.hit machine owns opacity. Keep OFF on a separate
                    # ancestor so a real hit cannot reveal this branch again.
                    variant_root["children"][0]["properties"]["visible"] = False
                path = branch.base + "/position-" + value + ".scene.json"
                self.json(path, variant_scene)
                positions.append({"when": {"judgement-position": value}, **contribution, "scene": path})
            choices.append({"value": f"j{style:02}", "label": f"Judgment Characters {style:02}", **contribution, "variants": positions,
                            "preview": self.preview("judgement", f"j{style:02}", [(package, "judge_p_great_blue")])})
        self.add_module("judgement", "Judgment Characters", choices, "j00")
        self.receipts.append({"category": "judgement", "choices": 14, "sourceSize": "Per-source complete74px label and digit canvas",
            "position": "26 native canvas top y=342-offset-lift, not its centre: caller3516f3 positions the original sprite; native texture geometry starts at(0,0) and the judgement initializer does not change its origin. Original table0/100/200/300/-1 becomes normal/up100/up200/up300/off. Only the selected catalogue scene is instantiated, so four positions do not duplicate dynamic text reservations.",
            "carrier": "Horizontal carriers have relative height74/(3*484*.12), so the runtime's vertically centred leaf is exactly at the original canvas top. height-px is used only by sprite leaves.",
            "horizontal": "Native113730 registers37px digit advance;1133f0 draws full55px glyphs at that step. The first digit starts at native label-layout width minus11, preserving the18px full-image overlap without trimming. Actual label PNG width is retained, including judge11's164px art.",
            "centringAdaptation": "OMS retains whole-row centring in its actual292px stage. The native caller uses labelX=C-floor((L+37*n-11)/2)-11 with C=195/1085 forSP and450/830 forDP; its complete canvas centre differs by1-3source pixels from this OMS centring. Native digits have five slots and clamp at99999; OMS keeps the actual longer integer value and fits it instead. No claim of exact native horizontal placement.",
            "widthFit": "Whole label-plus-digits uses equal X/Y scaling only when its source width exceeds292px; exact digit thresholds1-7, conservative10-digit fit from10,000,000 because C4 keyframe times are limited to86,400,000. Original PNG/atlas/number value unchanged; no crop or ellipsis.",
            "PGCycle": "Original blue/green/pink label-and-number pairs;100ms per phase is an explicit OMS author rhythm, not measured source timing",
            "noCombo": "BAD/POOR never contain digits, following native351510. PG/GREAT/GOOD show label plus actual digits only while combo.active; otherwise they show label only. Gameplay combo and scores are never changed."})

    def turntables(self):
        if self.frame_maps is None:
            raise ValueError("Turntables require all independently qualified source frame maps")
        mappings = read_json(self.frame_maps)
        if set(mappings) != {frame_id(style) for style in retained_styles(self.source_data)}:
            raise ValueError("Frame maps must contain exactly the retained Standard source identities")
        maps = {key: read_json(within(self.frame_maps.parent, value)) for key, value in mappings.items()}
        if includes20(self.source_data):
            original = self.index["s20/gameparts", "turn_00"]
            custom = self.index["source20/turn00", "turn00"]
            if any(original[key] != custom[key] for key in ("sha256", "width", "height")) or (custom["width"], custom["height"]) != (25, 25):
                raise ValueError("Original20 default Turntable differs from its supplied CUSTOM image")
        choices, motion_sources = [], []
        sources = [(style, f"turn{style:02}", f"turn{style:02}", f"t{style:02}", f"Turntable {style:02}")
                   for style in exact_styles(self.packages, r"turn(\d+)", range(21))]
        if includes20(self.source_data):
            sources.append((None, "source20/turn00", "turn00", "source20-turn00", "Turntable 00 (20 tricoro)"))
        for style, package, texture, choice, label in sources:
            branch = StandardBranch(self, "st", choice)
            resource = branch.asset(package, texture)[0]
            default20 = branch.asset("source20/turn00", "turn00")[0] if style == 0 and includes20(self.source_data) else None
            for layout, movie in (("p1", "1p_frame"), ("p2", "2p_frame"), ("dp", "dp_frame_1p")):
                frames = []
                for frame in self.frame_ids:
                    source = maps[frame]
                    if (source["stage"]["width"], source["stage"]["height"]) != (1280, 720):
                        raise ValueError("Standard frame source canvas changed")
                    children = []
                    for group in range(2 if layout == "dp" else 1):
                        binding = "turn_2p" if group == 1 or layout == "p2" else "turn_1p"
                        source_movie = "dp_frame" if frame in SOURCE20_STYLES and layout == "dp" else movie
                        candidates = [draw for draw in source["layouts"][source_movie]["draws"] if binding in draw["game_bindings"]]
                        if len(candidates) != 1:
                            raise ValueError(f"Expected one real turntable source hook: {frame}/{layout}/{binding}")
                        draw = candidates[0]
                        bounds = draw["bounds"]
                        source_vertices = draw["source_vertices"]
                        hook_asset = draw.get("asset")
                        if frame in SOURCE20_STYLES:
                            width, height = source20_turntable_size(frame, source, draw)
                            hook_asset = {"width": width, "height": height}
                        elif frame in SOURCE22_STYLES or frame in SOURCE21_STYLES:
                            width, height = source22_turntable_size(frame, source, draw)
                            hook_asset = {"width": width, "height": height}
                        elif hook_asset is None:
                            if frame != "iidx23" or draw["texture"] != "turn00":
                                raise ValueError("Unqualified unresolved turntable hook")
                            # copula's frame references its separately loaded default
                            # Turntable. Its 24/26 PNG is byte-identical; verify the
                            # exact 24 manifest before using this shared raster size.
                            earlier_path = self.library / "source-imports/iidx24-library.json"
                            expected = self.source_data['sourceComposition']['inputManifestSHA256']['iidx24']
                            if digest(earlier_path) != expected:
                                raise ValueError("Earlier source manifest changed")
                            earlier = read_json(earlier_path)
                            matches = [row for row in earlier['textures']
                                       if row['package'] == 'data/graphic/turn00.ifs' and row['name'] == 'turn00.png']
                            if len(matches) != 1:
                                raise ValueError("Missing independently sourced copula Turntable")
                            hook_asset = matches[0]
                            shared = self.index['turn00', 'turn00']
                            if any(hook_asset[key] != shared[key] for key in ('sha256', 'width', 'height', 'imageRect', 'uvRect')):
                                raise ValueError("Earlier default Turntable differs from the shared source")
                        width, height = hook_asset["width"], hook_asset["height"]
                        expected = [(0, 0), (width, 0), (0, height), (width, height)]
                        if [(p["x"], p["y"]) for p in source_vertices] != expected or any(p["z"] for p in source_vertices):
                            raise ValueError("Turntable source needs a non-rectangular mesh consumer")
                        ident = branch.prefix + "." + layout + "." + frame + ".n" + str(group)
                        props, motion, motion_source = turntable_motion(ident, draw, layout, group)
                        art = node(ident, "projective-sprite", props, default20 if frame in SOURCE20_STYLES and default20 else resource)
                        vertices = draw["projected_vertices"]
                        quad = [[(vertices[i]["x"] - bounds["left"]) / (bounds["right"] - bounds["left"]),
                                 (vertices[i]["y"] - bounds["top"]) / (bounds["bottom"] - bounds["top"]), 1]
                                for i in (0, 1, 3, 2)]
                        branch.tracks.append({"id": ident + ".orientation", "type": "frame", "target": ident,
                            "property": "projective-quad", "easing": "step", "loop": False,
                            "keyframes": [{"id": ident + ".fixed", "time": 0,
                                           "value": {"vertices": quad, "opacity": 1}}]})
                        branch.tracks.append(motion)
                        if style == 0:
                            motion_sources.append({"frame": frame, "layout": layout, "group": group,
                                                   "sourceHook": binding, **motion_source,
                                                   **({"carrierGeometry": "Original22 normalized-UV GEO; no fallback PNG dimensions"}
                                                      if frame in SOURCE22_STYLES else
                                                      {"carrierGeometry": "Original21 normalized-UV GEO; no fallback PNG dimensions"}
                                                      if frame in SOURCE21_STYLES else
                                                      {"carrierGeometry": "Original20 turn_00 default25x25 GEO; independent of21/22 dynamic carriers"}
                                                      if frame in SOURCE20_STYLES else {})})
                        children.append(art)
                    ident = branch.prefix + "." + layout + "." + frame
                    frames.append(node(ident, children=children))
                    branch.show(ident, "option.frame", [frame])
                branch.template(layout, node(branch.prefix + "." + layout, slot="decoration", children=frames),
                                "decoration", f"markers/frame-{layout}-information")
            choices.append({"value": choice, "label": label, **branch.finish(),
                            "preview": self.preview("turntable", choice, [(package, texture)])})
        self.add_module("turntable", "Turntable", choices, "t00")
        self.receipts.append({"category": "turntable", "choices": len(choices), "sourceMaps": str(self.frame_maps),
            "placement": "Original per-frame bounds and projected vertices, including the25 tiny rotation and negative-X orientation; all texture-only source archives",
            "motion": "Preserved source projective quad rotates about its original centre from real signed input.motion-revolutions-1/2. P2 single-player is logical group0; only DP right uses group1. Negative source determinant reverses the angle.",
            "motionSources": motion_sources,
            "unavailableMotion": "The public unavailable source is zero; digital keys and old replays retain static art. No invented timer, key trigger or scratch press-light.",
            "motionLimits": "Rigid post-projection rotation is emitted only for the verified source uniform similarities; it is not a claim about original executable turntable response timing."})
        if includes20(self.source_data):
            self.receipts[-1]["source20"] = {
                "default": "The unchanged module default t00 selects the supplied20 source20/turn00 image only for frame source20-gmframe0. It is byte-identical to that frame's own s20/gameparts turn_00, whose named placeholder is removed from recorded background art.",
                "additionalChoice": "source20-turn00 retains that distinct original25x25 design as an explicit choice; exact CUSTOM duplicates do not add choices.",
                "geometry": "Own20 original1p_frame/2p_frame and single dp_frame carriers. Each retains its25x25 normalized-UV quad and actual affine position; DP has two independently driven scratch groups.",
                "customProjection": "Other Turntable choices reuse this25x25 host carrier when frame20 is selected. Native20 dynamic CUSTOM carrier resizing is not qualified, and the21/22 carrier rule is not promoted to20. Selecting the new20 image on older frames uses their existing carriers as an explicit host adaptation."}

    def graph_art(self):
        source = self._package("0/graph")
        references = {}
        for layout, movie in (("p1", "graph_1p"), ("p2", "graph_2p")):
            selected = source["movies"][movie]
            hooks = [tag for tag in all_tags(selected.tags, self.tools.AP2DefineSpriteTag)
                     if isinstance(tag, self.tools.AP2PlaceObjectTag) and tag.movie_name == "customize"]
            if len(hooks) != 1:
                raise ValueError("Expected one original graph customization hook")
            recorder = LocalStateRecorder(self.tools, self.StateRenderer)
            captured = io.StringIO()
            with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
                root = recorder.record("0/graph", movie, selected, source["movies"], source["shapes"], source["mapping"], source["filters"])
            current = dict(root["frames"][-2]["nodes"])
            candidates = [n for n in root["nodes"] if n["sourceTag"] == hooks[0].source_tag_id and n["id"] in current]
            if len(candidates) != 1:
                raise ValueError("The actual graph hook is not unique in the rendered source hierarchy")
            hook = candidates[0]
            chain = []
            while hook["id"] >= 0:
                chain.append(hook)
                if hook["parent"] < 0:
                    break
                hook = root["nodes"][hook["parent"]]
            x = y = 0
            masks = []
            for item in reversed(chain):
                state = root["states"][current[item["id"]]]
                if state["matrix"][:9] != [1, 0, 0, 0, 1, 0, 0, 0, 1] or state["origin"][2] or state["matrix"][11]:
                    raise ValueError("Graph hook needs an explicit transformed-art consumer")
                x += state["matrix"][9] - state["origin"][0]
                y += state["matrix"][10] - state["origin"][1]
                if state["mask"]:
                    l, t, r, b = state["mask"]
                    masks.append([l + x, t + y, r + x, b + y])
            if not masks:
                raise ValueError("Original graph art must retain its actual ancestor clip")
            clip = [max(m[0] for m in masks), max(m[1] for m in masks), min(m[2] for m in masks), min(m[3] for m in masks)]
            if clip[2] <= clip[0] or clip[3] <= clip[1]:
                raise ValueError("Graph art ancestor masks have no visible intersection")
            references[layout] = {"movie": movie, "artOrigin": [x, y], "clip": clip, "sourceHook": hooks[0].as_dict(),
                                  "ancestorMasks": masks}
            research = self.output / "research/graph"
            research.mkdir(parents=True, exist_ok=True)
            (research / (movie + ".json")).write_text(json.dumps(root, separators=(",", ":")), encoding="utf-8")
        source20 = None
        if includes20(self.source_data):
            original20 = self._package("s20/graph")
            source20 = {"sourcePackage": "s20/graph", "movies": [], "customizeHookPresent": False,
                "selectedContribution": "Original blank; retain the complete original20 panel",
                "boundary": "No customize hook exists in the supplied20 Graph movies. MAX and baseline are baked into its panel. This host keeps Graph Area fixed for20 instead of overlaying the26 customization rectangle; native dynamically created objects are not inferred from absent AFP names."}
            for movie, selected in sorted(original20["movies"].items()):
                hooks = [tag for tag in all_tags(selected.tags, self.tools.AP2DefineSpriteTag)
                         if isinstance(tag, self.tools.AP2PlaceObjectTag) and tag.movie_name == "customize"]
                if hooks:
                    raise ValueError("Original20 Graph Area source gained an unqualified customization hook")
                source20["movies"].append({"name": movie, "frames": len(selected.frames), "customizeHooks": len(hooks)})
        choices = [{"value": "blank", "label": "Original blank", **StandardBranch(self, "sg", "blank").finish(),
                    "preview": self.preview("graph-area", "blank", [])}]
        for package, texture, value, label in self.lane_sources():
            branch = StandardBranch(self, "sg", value)
            resource = branch.asset(package, texture, "sl")[0]
            for layout in ("p1", "p2", "dp"):
                reference = references["p1" if layout == "dp" else layout]
                l, t, r, b = reference["clip"]
                x, y = reference["artOrigin"]
                row = self.index[package, texture]
                identifier = branch.prefix + "." + layout
                art = node(identifier + ".art", "sprite", {"x": (x - l) / (r - l), "y": (y - t) / (b - t),
                    "width": row["width"] / (r - l), "height": row["height"] / (b - t)}, resource)
                clipped = node(identifier + ".clip", "clip", {"x": l / 1280, "y": t / 720,
                    "width": (r - l) / 1280, "height": (b - t) / 720, "clip-mode": "bounds"}, children=[art])
                root = node(identifier, properties={"z": -20}, slot="decoration", children=[clipped])
                branch.bind(root["id"], "visible", "option.graph-visible")
                branch.template(layout, root, "decoration", f"markers/frame-{layout}-information")
            choices.append({"value": value, "label": "Graph Area art " + label, **branch.finish(),
                            "preview": self.preview("graph-area", value, [(package, texture)])})
            if source20 is not None:
                choices[-1]["variants"] = [{"when": {"frame": frame_id(SOURCE20_STYLES[0])},
                    **{key: choices[0][key] for key in ("manifest", "scene", "ini")}}]
        self.add_module("graph-area", "Graph Area", choices, "blank")
        self.receipts.append({"category": "graph-area", "choices": len(choices), "sourcePackage": "0/graph",
            "references": references, "DP": "Explicit author reuse of the original right-side P1 graph hook",
            "sourceComposition": "Original lane art at actual customize hook with intersected ancestor masks",
            "dataConsumer": "Caller must supply real graph scores and option.graph-visible; this module provides only art"})
        if source20 is not None:
            self.receipts[-1]["source20"] = source20

    def fullcombo(self):
        choices = []
        sources = [(f"fcombo{style:02}", f"fc{style:02}", f"n{style:02}", f"Full Combo {style:02}")
                   for style in exact_styles(self.packages, r"fcombo(\d+)", range(18))]
        if "source24/fcombo00" in self.packages:
            sources.append(("source24/fcombo00", "fc00-iidx24", "source24-n00", "Full Combo 00 · copula / SINOBUZ"))
        for package, value, clip_id, label in sources:
            contributions = {}
            for layout, movie in (("p1", "1p_fullcombo"), ("p2", "2p_fullcombo"), ("dp", "dp_fullcombo")):
                branch = StandardBranch(self, "sf", value, layout)
                exported = self.clip(package, movie, f"sf.clip.{clip_id}.{layout}", loop=False, trigger="gameplay.complete")
                art = branch.include_clip(exported)
                art["id"] = branch.prefix + ".art"
                duration = exported["source"]["durationMs"]
                if not 0 < duration <= 3500:
                    raise ValueError("Original Full Combo movie exceeds the bounded completion outro contract")
                branch.completion_outro = duration
                retention = node(branch.prefix + ".retention", children=[art])
                root = node(branch.prefix + ".eligibility", properties={"z": -40}, slot="decoration", children=[retention])
                branch.bind(root["id"], "visible", "gameplay.full-combo")
                branch.tracks.append({"id": branch.prefix + ".outro", "type": "tween", "target": retention["id"],
                    "property": "opacity", "easing": "step", "loop": False, "trigger": "gameplay.complete",
                    "keyframes": [{"id": branch.prefix + ".outro.begin", "time": 0, "value": 1},
                                  {"id": branch.prefix + ".outro.end", "time": duration, "value": 0}]})
                branch.template("art", root, "decoration", f"markers/frame-{layout}-information")
                contributions[layout] = branch.finish()
            # Double stages have no layout option: the base is DP. Only single
            # stages provide the p1/p2 option and select either override.
            names = [n for key, n in self.index if key == package]
            largest = sorted(names, key=lambda n: self.index[package, n]["width"] * self.index[package, n]["height"], reverse=True)[:2]
            choices.append({"value": value, "label": label, **contributions["dp"],
                "variants": [{"when": {"layout": layout}, **contributions[layout]} for layout in ("p1", "p2")],
                "preview": self.preview("full-combo", value, [(package, n) for n in largest])})
        self.add_module("full-combo", "Full Combo", choices, "fc00")
        self.receipts.append({"category": "full-combo", "choices": len(choices), "layouts": ["p1", "p2", "dp"],
            "sourceMovieScope": "All declared frames, hierarchy, XYZ, topology, masks, filters, camera and sentinel preserved; UV is losslessly rebased to original RGBA atlas rectangles with repeated clamp-edge padding",
            "sourcePixelPacking": "Every Full Combo movie uses bounded nearest/clamp atlases; original pixels are not resampled, recoloured, trimmed or rotated",
            "completionOutro": "Each original duration is retained by an explicit non-loop completion track on a separate opacity wrapper",
            "layerOrder": "Original26 common caller covers all18 styles: FC priority44 becomes156, Graph88/71 becomes112/129, and AFP draws ascending. Therefore FC is in front of Graph; C5 lower depth is front, so the full source movie root uses z=-40 before Graph -10/-20/-30. No stage crop or rescale.",
            "eligibility": "Actual gameplay.full-combo; no score/combo inference in the author package"})

    def long_note_research(self):
        """Export all source movies for independent offline inspection."""
        source = self._package("lnote00")
        expected = {"notes_break"} | {f"lnote00_lo_{colour}{suffix}"
            for colour in ("siro", "ao", "scr") for suffix in ("", "2", "_push", "2_push")}
        if set(source["movies"]) != expected:
            raise ValueError("The qualified long-note source movie selection changed")
        movies = [self.clip("lnote00", name, "sx." + name, loop=name != "notes_break", trigger="gameplay.attach")
                  for name in sorted(expected)]
        result = {"contract": "oms-standard-long-note-research.v1", "movies": movies,
            "files": list(self.files.values()), "sourceAssets": list(self.used_assets.values()),
            "runtimeAttachment": False,
            "limitations": [
                "No runtime choice references these research movies. gameplay.attach is an offline playback clock, not a proposed gameplay event.",
                "This inventory includes three base2 movies that the26 executable does not dispatch.",
                "The gameplay charge-notes component separately mounts the ten native-caller movies on the exact fixed-line long-note effect owner.",
                "Native state and transition times drive gameplay effects; ordinary key/judgement events are insufficient.",
                "This offline inventory alone does not prove native state switching or live rendering."]}
        (self.output / "export.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
        return result

    def build(self, selected):
        methods = {"notes": self.notes, "charge-notes": self.charge_notes, "judgement": self.judgements,
                   "explosion": self.explosions, "key-beam": self.beams, "lane-cover": self.static_lanes,
                   "lift-art": self.lift_art, "turntable": self.turntables, "graph-area": self.graph_art,
                   "full-combo": self.fullcombo}
        for module in selected:
            print("Building", module, flush=True)
            methods[module]()
            print("Complete", module, len(self.modules[-1]["choices"]), flush=True)
        if digest(self.library / "library.json") != self.source_library_sha:
            raise ValueError("Source library manifest changed during component export")
        result = {"contract": "oms-standard-components.v1", "source": {"library": str(self.library),
                    "librarySHA256": self.source_library_sha, "artIdentity": art_identity(self.source_data), "frameIds": list(self.frame_ids)},
                  "modules": self.modules, "options": self.options, "files": list(self.files.values()),
                  "receipts": {"sourceAssets": list(self.used_assets.values()), "branches": self.branch_counts,
                               "details": self.receipts, "previewSources": self.preview_sources,
                               "defaults": "00 are author starting choices; original user/custom/default menu selection is separately observed"}}
        (self.output / "modules.json").write_text(json.dumps(result, ensure_ascii=False, separators=(",", ":")), encoding="utf-8")
        return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--library", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--bemani-root", type=Path, required=True)
    parser.add_argument("--image-tools", type=Path, required=True)
    parser.add_argument("--frame-maps", type=Path)
    parser.add_argument("--research-long-notes", action="store_true", help="Export all13 original lnote movies as research only; no gameplay module")
    parser.add_argument("--modules", default="notes,charge-notes,judgement,explosion,key-beam,lane-cover,lift-art,full-combo")
    args = parser.parse_args()
    selected = args.modules.split(",")
    if not selected or len(selected) != len(set(selected)) or any(item not in SUPPORTED_MODULES for item in selected):
        parser.error("Choose distinct supported modules")
    builder = StandardComponents(args.library, args.output, args.bemani_root, args.image_tools, args.frame_maps)
    if args.research_long_notes:
        result = builder.long_note_research()
        print(json.dumps({"output": str(args.output), "researchMovies": len(result["movies"]), "runtimeAttachment": False}))
        return
    result = builder.build(selected)
    print(json.dumps({"output": str(args.output), "modules": [{"id": m["id"], "choices": len(m["choices"])} for m in result["modules"]],
                      "files": len(result["files"])}, ensure_ascii=False))


if __name__ == "__main__":
    main()
