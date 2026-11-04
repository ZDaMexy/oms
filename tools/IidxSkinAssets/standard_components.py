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
import re
import sys
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True

from build_skin import node, read_json, within
from customization_modules import Branch, ModuleBuilder, material_contexts
from build_fullcombo_catalog import (InternTable, LocalStateRecorder, all_tags, exporter_classes,
                                    load_tools, point_values, rgba_values, to_recorded_animation)
from export_customization_animations import node_for_reference, root_loop_control

FRAME_IDS = tuple(f"iidx{style:02}" for style in (3, 8, 12, 14, 16, 17, 19, 21, 25, 26))
COLOURS = ("white", "black", "scratch")
SOURCE_WIDTHS = {"white": 34, "black": 26, "scratch": 60}
SOURCE_TO_LOGICAL = 768 / 720
SOURCE_TO_REFERENCE = 1.5
SOURCE_STAGE_HEIGHT = 484
SOURCE_STAGE_WIDTH = 292
REFERENCE_STAGE_HEIGHT = SOURCE_STAGE_HEIGHT * SOURCE_TO_REFERENCE
SOURCE_HIT_OFFSET = 10 / 3  # Explicit OMS alignment; original-game hit-point calibration is separate.
SUPPORTED_MODULES = ("notes", "charge-notes", "judgement", "explosion", "key-beam", "lane-cover",
                     "lift-art", "turntable", "graph-area", "full-combo")


def judgement_fit_steps(label_width, digit_advance):
    """Fit the complete source row, retaining full size while it fits.

    C4 clock keyframes stop at 86,400,000, so the final legal decimal boundary
    reserves ten digits for all 8--10 digit int values. Realistic 1--7 digit
    combos use their exact integer digit count, with no cropping or ellipsis.
    """
    def scale(digits):
        width = max(label_width, label_width - 8 + digit_advance * digits)
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
        self.packages = {row["source"].removeprefix("contents/data/graphic/").removesuffix(".ifs"): row
                         for row in data["packages"]}
        exact_styles(self.packages, r"gmframe(\d+)", (3, 8, 12, 14, 16, 17, 19, 21, 25, 26))
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
        repeat = terminal_repeat_control(selected) if package == "lnote00" and loop else None
        if repeat:
            flow_tags.add(id(selected.tags[repeat["tagIndex"]]))
        if controls and (trigger != "input.key.down" or loop):
            raise ValueError("Original held loop requires the native input trigger")

        class TimelineRenderer(self.StateRenderer):
            def _AFPRenderer__place(self, tag, clip, prefix=""):
                if id(tag) in flow_tags and clip is self._AFPRenderer__root:
                    return None, False
                return super()._AFPRenderer__place(tag, clip, prefix)

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
        for i, row in enumerate(source["rows"]):
            if resources[i] in used:
                declaration, _ = self.asset(category, package, Path(row["name"]).stem)
                declared[declaration["id"]] = declaration
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
                    **({"translatedTerminalRepeat": repeat} if repeat else {})},
                  "receipt": {"sourceArchiveSHA256": self.packages[package]["sha256"], "rawSHA256": source["hashes"],
                    "sourcePNGBytesPreserved": True, "runtimeInterpreter": False,
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
            marker_package = "notes00" if slot == "object.note" else "notes_cn00"
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
            choices.append({"value": f"n{style:02}", "label": f"Notes {style:02}", **branch.finish(),
                "preview": self.preview("notes", f"n{style:02}", [(package, "notes_" + c) for c in ("white", "black", "turn")])})
        self.add_module("notes", "Notes", choices, "n00")
        self.receipts.append({"category": "notes", "sourcePackages": 12, "sourcePixelToLogical": SOURCE_TO_LOGICAL,
            "sourceHitOffset": SOURCE_HIT_OFFSET, "hitOffsetStatus": "OMS alignment requiring original-game/Player comparison",
            "sizeVariants": "Only the twelve actual normal archives exist; no imported Lightning sizes or synthetic design choices",
            "maniaCarrier": "48 * original marker height / width", "bmsCarrier": 22.5})

    def charge_notes(self):
        branch = StandardBranch(self, "sc", "c00")
        materials = {r: {} for r in ("bms", "mania")}
        for colour in COLOURS:
            stem = "notes_bss" if colour == "scratch" else "notes_cn_" + colour
            second = "notes_bss2" if colour == "scratch" else "notes_cn2_" + colour
            for ruleset in materials:
                materials[ruleset][colour] = {}
            for part, suffix in (("head", "start"), ("body", "beam0"), ("tail", "end")):
                texture = stem + "_" + suffix
                if part == "body":
                    appearance = ("object.long-note-appearance", {"ln": texture, "cn": texture,
                        "ln-holding": stem + "_beam1", "cn-holding": stem + "_beam1",
                        "ln-released": stem + "_beam2", "cn-released": stem + "_beam2",
                        "hcn": second + "_beam0", "hcn-holding": second + "_beam1", "hcn-released": second + "_beam4"})
                else:
                    appearance = ("object.long-note-mode", {"ln": texture, "cn": texture, "hcn": second + "_" + suffix})
                slot = "object.long-note." + part
                for ruleset, marker in self._note(branch, colour, slot, texture, body=part == "body",
                                                appearance=appearance, package="notes_cn00"):
                    materials[ruleset][colour][slot] = marker
        for ruleset, material in materials.items():
            branch.lane_ini(material, ruleset)
        self.add_module("charge-notes", "Charge Notes / HCN", [{"value": "c00", "label": "Original 00", **branch.finish(),
            "preview": self.preview("charge-notes", "c00", [("notes_cn00", n) for n in ("notes_cn_white_start", "notes_cn2_white_start", "notes_bss_start")])}], "c00")
        self.receipts.append({"category": "charge-notes", "sourcePackage": "notes_cn00", "choices": 1,
            "mapping": "cn/bss = LN/CN; cn2/bss2 = HCN; body states use actual native appearance and original beam0/1/2/4",
            "qualification": "Original source naming/state family; original-game HCN held/released palette requires runtime comparison",
            "bodyLength": "Native time/scroll geometry is unchanged; only original head/tail sprite geometry is normalized"})

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
                             "min": .5, "max": 1.5, "step": .05})
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

    def static_lanes(self):
        choices = []
        for style in exact_styles(self.packages, r"lane(\d+)", range(167)):
            branch = StandardBranch(self, "sl", f"l{style:03}")
            package = f"lane{style:03}"
            resource = branch.asset(package, package, "sl")[0]
            marker = self.fallback_marker("sl", "cover", "lane000", "lane000")
            branch.template("art", node(branch.prefix + ".art", "sprite", resource=resource, slot="playfield.lane-cover.fill"),
                            "playfield.lane-cover.fill", marker)
            branch.stage_ini("playfield.lane-cover.fill", marker)
            choices.append({"value": f"l{style:03}", "label": f"Lane Cover {style:03}", **branch.finish(),
                            "preview": self.preview("lane-cover", f"l{style:03}", [(package, package)])})
        self.add_module("lane-cover", "Lane Cover", choices, "l000")

    def lift_art(self):
        choices = []
        for style in exact_styles(self.packages, r"lane(\d+)", range(167)):
            branch = StandardBranch(self, "sv", f"l{style:03}")
            black, marker = self.control_black(branch)
            package = f"lane{style:03}"
            resource = branch.asset(package, package, "sl")[0]
            root = node(branch.prefix + ".fill", slot="playfield.lift.fill", children=[
                node(branch.prefix + ".black", "sprite", resource=black),
                node(branch.prefix + ".art", "sprite", {"x": 2 / 292, "width": 288 / 292}, resource)])
            branch.template("fill", root, "playfield.lift.fill", marker)
            for context in self.contexts:
                if context["ruleset"] == "bms":
                    branch.ini.extend(("[GameplaySkin.Bms:1]", "Target: Stage " + context["stage"],
                                       f'playfield.lift.fill: resource Provide "{marker}"'))
            choices.append({"value": f"l{style:03}", "label": f"LIFT art {style:03}", **branch.finish(),
                            "preview": self.preview("lift-art", f"l{style:03}", [(package, package)])})
        self.add_module("lift-art", "LIFT", choices, "l000")
        self.modules[-1].update(rulesets=["bms"], keymodes=["5k", "7k", "14k"])
        self.receipts.append({"category": "lift-art", "choices": 167,
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
                                "advance": glyphs[palette, d][2]} for d in range(10)]})
                fonts[palette] = identifier
            # One source-size row, centred in the exact native group judgement slot.
            # Source bitmap dimensions are not multiplied by an inherited 33 normalization.
            height_logical = height * SOURCE_TO_LOGICAL
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
                label = node(pair_id + ".label", "sprite", {"height-px": height_logical}, label_resource)
                combo = node(pair_id + ".combo", "text", {"text": "", "font-size": height_logical,
                             "bitmap-font": fonts[palette], "format": "integer"})
                branch.bind(combo["id"], "text", "combo.value")
                pair = node(pair_id, properties={"x": .5, "y": .5, "origin": "centre", "layout": "horizontal", "gap-px": -8 * SOURCE_TO_LOGICAL},
                            children=[label, combo])
                branch.bind(pair_id, "visible", "combo.active")
                label_source = self.index[package, label_name]
                label_width = label_source["width"] * height / label_source["height"]
                digit_advance = max(glyphs[palette, digit][2] for digit in range(10))
                steps = judgement_fit_steps(label_width, digit_advance)
                for prop in ("scale-x", "scale-y"):
                    identifier = pair_id + ".fit." + prop
                    branch.tracks.append({"id": identifier, "type": "tween", "target": pair_id, "property": prop,
                        "easing": "step", "loop": False, "clock": "combo.value",
                        "keyframes": [{"id": identifier + ".n" + str(i), "time": time, "value": value}
                                      for i, (time, value) in enumerate(steps)]})
                # A hidden horizontal cell still occupies its natural width.
                # The inactive state therefore owns a label-only centred row.
                no_combo = node(pair_id + ".no-combo", properties={"x": .5, "y": .5, "origin": "centre", "layout": "horizontal"},
                    children=[node(pair_id + ".no-combo.label", "sprite", {"height-px": height_logical}, label_resource)])
                branch.show(no_combo["id"], "combo.active", ["false"])
                phase_layer = node(pair_id + ".palette", children=[pair, no_combo])
                grade_gate = node(pair_id + ".grade", children=[phase_layer])
                branch.show(grade_gate["id"], "judgement.result", grades)
                if palette != "y":
                    phase = "bgr".index(palette)
                    branch.tracks.append({"id": pair_id + ".phase", "type": "tween", "target": phase_layer["id"], "property": "opacity",
                        "easing": "step", "loop": True, "trigger": "judgement.hit",
                        "keyframes": [{"id": pair_id + ".phase.n" + str(i), "time": i * 100, "value": int(i % 3 == phase)} for i in range(4)]})
                children.append(grade_gate)
                self.receipts.append({"category": "judgement", "style": style, "grade": grades, "palette": palette,
                    "sourceLabelWidth": label_width, "sourceDigitAdvance": digit_advance, "sourceStageWidth": SOURCE_STAGE_WIDTH,
                    "fitSteps": steps, "noCombo": "Separate centred label-only row at original size"})
            gate = node(root_id + ".display", children=children)
            branch.bind(gate["id"], "visible", "option.judgement-display")
            root = node(root_id, properties={"opacity": 0}, slot="hud.judgement", children=[gate])
            marker = self.fallback_marker("sj", "judgement", "judge00", "judge_p_great_blue")
            branch.template("group", root, "hud.judgement", marker)
            branch.stage_ini("hud.judgement", marker)
            branch.machines.append({"id": root_id + ".m", "initial": root_id + ".empty",
                "states": [{"id": root_id + ".empty", "set": [{"id": root_id + ".hide", "target": root_id, "property": "opacity", "value": 0}]},
                           {"id": root_id + ".active", "set": [{"id": root_id + ".show", "target": root_id, "property": "opacity", "value": 1}]}],
                "transitions": [{"id": root_id + ".arrive", "from": root_id + ".empty", "to": root_id + ".active", "event": "judgement.hit"}]})
            choices.append({"value": f"j{style:02}", "label": f"Judgment Characters {style:02}", **branch.finish(),
                            "preview": self.preview("judgement", f"j{style:02}", [(package, "judge_p_great_blue")])})
        self.add_module("judgement", "Judgment Characters", choices, "j00")
        self.receipts.append({"category": "judgement", "choices": 14, "sourceSize": "Per-source complete74px label and digit canvas",
            "position": "Native exact group judgement slot centre; original26 executable anchor is not yet asserted",
            "widthFit": "Whole label-plus-digits uses equal X/Y scaling only when its source width exceeds292px; exact digit thresholds1-7, conservative10-digit fit from10,000,000 because C4 keyframe times are limited to86,400,000. Original PNG/atlas/number value unchanged; no crop or ellipsis.",
            "PGCycle": "Original blue/green/pink label-and-number pairs;100ms per phase is an explicit OMS author rhythm, not measured source timing",
            "noCombo": "Real combo.active hides digits without replacing gameplay scores"})

    def turntables(self):
        if self.frame_maps is None:
            raise ValueError("Turntables require all ten independently qualified source frame maps")
        mappings = read_json(self.frame_maps)
        if set(mappings) != set(FRAME_IDS):
            raise ValueError("Frame maps must contain exactly the ten Standard frame identities")
        maps = {key: read_json(within(self.frame_maps.parent, value)) for key, value in mappings.items()}
        choices, motion_sources = [], []
        for style in exact_styles(self.packages, r"turn(\d+)", range(21)):
            branch = StandardBranch(self, "st", f"t{style:02}")
            resource = branch.asset(f"turn{style:02}", f"turn{style:02}")[0]
            for layout, movie in (("p1", "1p_frame"), ("p2", "2p_frame"), ("dp", "dp_frame_1p")):
                frames = []
                for frame in FRAME_IDS:
                    source = maps[frame]
                    if (source["stage"]["width"], source["stage"]["height"]) != (1280, 720):
                        raise ValueError("Standard frame source canvas changed")
                    children = []
                    for group in range(2 if layout == "dp" else 1):
                        binding = "turn_2p" if group == 1 or layout == "p2" else "turn_1p"
                        candidates = [draw for draw in source["layouts"][movie]["draws"] if binding in draw["game_bindings"]]
                        if len(candidates) != 1:
                            raise ValueError(f"Expected one real turntable source hook: {frame}/{layout}/{binding}")
                        draw = candidates[0]
                        bounds = draw["bounds"]
                        source_vertices = draw["source_vertices"]
                        width, height = draw["asset"]["width"], draw["asset"]["height"]
                        expected = [(0, 0), (width, 0), (0, height), (width, height)]
                        if [(p["x"], p["y"]) for p in source_vertices] != expected or any(p["z"] for p in source_vertices):
                            raise ValueError("Turntable source needs a non-rectangular mesh consumer")
                        ident = branch.prefix + "." + layout + "." + frame + ".n" + str(group)
                        props, motion, motion_source = turntable_motion(ident, draw, layout, group)
                        art = node(ident, "projective-sprite", props, resource)
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
                                                   "sourceHook": binding, **motion_source})
                        children.append(art)
                    ident = branch.prefix + "." + layout + "." + frame
                    frames.append(node(ident, children=children))
                    branch.show(ident, "option.frame", [frame])
                branch.template(layout, node(branch.prefix + "." + layout, slot="decoration", children=frames),
                                "decoration", f"markers/frame-{layout}-information")
            choices.append({"value": f"t{style:02}", "label": f"Turntable {style:02}", **branch.finish(),
                            "preview": self.preview("turntable", f"t{style:02}", [(f"turn{style:02}", f"turn{style:02}")])})
        self.add_module("turntable", "Turntable", choices, "t00")
        self.receipts.append({"category": "turntable", "choices": 21, "sourceMaps": str(self.frame_maps),
            "placement": "Original per-frame bounds and projected vertices, including the25 tiny rotation and negative-X orientation; all texture-only source archives",
            "motion": "Preserved source projective quad rotates about its original centre from real signed input.motion-revolutions-1/2. P2 single-player is logical group0; only DP right uses group1. Negative source determinant reverses the angle.",
            "motionSources": motion_sources,
            "unavailableMotion": "The public unavailable source is zero; digital keys and old replays retain static art. No invented timer, key trigger or scratch press-light.",
            "motionLimits": "Rigid post-projection rotation is emitted only for the verified source uniform similarities; it is not a claim about original executable turntable response timing."})

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
        choices = [{"value": "blank", "label": "Original blank", **StandardBranch(self, "sg", "blank").finish(),
                    "preview": self.preview("graph-area", "blank", [])}]
        for style in exact_styles(self.packages, r"lane(\d+)", range(167)):
            branch = StandardBranch(self, "sg", f"l{style:03}")
            package = f"lane{style:03}"
            resource = branch.asset(package, package, "sl")[0]
            for layout in ("p1", "p2", "dp"):
                reference = references["p1" if layout == "dp" else layout]
                l, t, r, b = reference["clip"]
                x, y = reference["artOrigin"]
                row = self.index[package, package]
                identifier = branch.prefix + "." + layout
                art = node(identifier + ".art", "sprite", {"x": (x - l) / (r - l), "y": (y - t) / (b - t),
                    "width": row["width"] / (r - l), "height": row["height"] / (b - t)}, resource)
                clipped = node(identifier + ".clip", "clip", {"x": l / 1280, "y": t / 720,
                    "width": (r - l) / 1280, "height": (b - t) / 720, "clip-mode": "bounds"}, children=[art])
                root = node(identifier, properties={"z": -20}, slot="decoration", children=[clipped])
                branch.bind(root["id"], "visible", "option.graph-visible")
                branch.template(layout, root, "decoration", f"markers/frame-{layout}-information")
            choices.append({"value": f"l{style:03}", "label": f"Graph Area art {style:03}", **branch.finish(),
                            "preview": self.preview("graph-area", f"l{style:03}", [(package, package)])})
        self.add_module("graph-area", "Graph Area", choices, "blank")
        self.receipts.append({"category": "graph-area", "choices": 168, "sourcePackage": "0/graph",
            "references": references, "DP": "Explicit author reuse of the original right-side P1 graph hook",
            "sourceComposition": "Original lane art at actual customize hook with intersected ancestor masks",
            "dataConsumer": "Caller must supply real graph scores and option.graph-visible; this module provides only art"})

    def fullcombo(self):
        choices = []
        for style in exact_styles(self.packages, r"fcombo(\d+)", range(18)):
            package = f"fcombo{style:02}"
            contributions = {}
            for layout, movie in (("p1", "1p_fullcombo"), ("p2", "2p_fullcombo"), ("dp", "dp_fullcombo")):
                branch = StandardBranch(self, "sf", f"fc{style:02}", layout)
                exported = self.clip(package, movie, f"sf.clip.n{style:02}.{layout}", loop=False, trigger="gameplay.complete")
                art = branch.include_clip(exported)
                art["id"] = branch.prefix + ".art"
                duration = exported["source"]["durationMs"]
                if not 0 < duration <= 3500:
                    raise ValueError("Original Full Combo movie exceeds the bounded completion outro contract")
                branch.completion_outro = duration
                retention = node(branch.prefix + ".retention", children=[art])
                root = node(branch.prefix + ".eligibility", slot="decoration", children=[retention])
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
            choices.append({"value": f"fc{style:02}", "label": f"Full Combo {style:02}", **contributions["dp"],
                "variants": [{"when": {"layout": layout}, **contributions[layout]} for layout in ("p1", "p2")],
                "preview": self.preview("full-combo", f"fc{style:02}", [(package, n) for n in largest])})
        self.add_module("full-combo", "Full Combo", choices, "fc00")
        self.receipts.append({"category": "full-combo", "choices": 18, "layouts": ["p1", "p2", "dp"],
            "sourceMovieScope": "All declared frames, hierarchy, geometry, UV, masks, filters, camera and sentinel preserved",
            "completionOutro": "Each original duration is retained by an explicit non-loop completion track on a separate opacity wrapper",
            "eligibility": "Actual gameplay.full-combo; no score/combo inference in the author package"})

    def long_note_research(self):
        """Export source movies without declaring a nonexistent runtime owner."""
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
                "Current exact-object head/body/tail instances expose object.state and long-note-appearance but no active/holding elapsed clock.",
                "The BMS head expires120ms after judgement. The scrolling body is not a fixed judgement-line effect owner.",
                "Mounting requires a real fixed-line long-note effect owner, exact CN/BSS/HCN identity and native state time; ordinary key/judgement events are insufficient.",
                "Source cn2/bss2 and lnote2 associations are evidence from source naming; exact original-game state switching remains unmeasured."]}
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
                    "librarySHA256": self.source_library_sha, "artIdentity": "IIDX26-only", "frameIds": list(FRAME_IDS)},
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
