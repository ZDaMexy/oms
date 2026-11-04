#!/usr/bin/env python3
"""Export independent Standard Model cabinet layout evidence from one art library.

This command reads extracted AFP/GEO/PNG files, never a running game or another
skin. The explicit common family records an observed source choice; canvas size
does not select it. Offline snapshots do not prove executable timing or filters.
"""

from __future__ import annotations

import argparse
from collections import Counter
import contextlib
import copy
import hashlib
import io
import json
import math
import os
from pathlib import Path
import struct
import sys
import types
from urllib.parse import unquote
import xml.etree.ElementTree as ET


CONTRACT = "oms-iidx-standard-reference.v1"
LAYOUTS = ("1p_frame", "2p_frame", "dp_frame_1p", "dp_frame_2p",
           "center_frame_sp", "center_frame_dp_1p", "center_frame_dp_2p",
           "center_frame_wide_1p", "center_frame_wide_2p", "center_frame_2pp")
COMMON_PACKAGES = ("basic_frame", "common", "gameparts", "graph")
UNKNOWN_BITS = (0x40, 0x10000, 0x100000, 0x800000000, 0x1000000000, 0x2000000000)
KEEP_NAMED = frozenset(("lane_1p", "lane_2p"))
DROP_TEXTURES = frozenset(("playm_hs_dot",))
RECORDED_LAYOUTS = ("1p_frame", "2p_frame", "dp_frame_1p", "center_frame_sp", "center_frame_dp_1p",
                    "center_frame_wide_1p", "center_frame_wide_2p", "center_frame_2pp")
MOVIE_IDS = {"1p_frame": "p1", "2p_frame": "p2", "dp_frame_1p": "dp-gauge",
             "center_frame_sp": "sp-center", "center_frame_dp_1p": "dp-center",
             "center_frame_wide_1p": "wide-p1-center", "center_frame_wide_2p": "wide-p2-center",
             "center_frame_2pp": "two-player-center"}
LIMITS = [
    "Original affine matrices and GEO mesh/UV are retained. Recorded geometric bounds do not apply masks or perspective.",
    "The external AFP renderer omits some original filters and script behavior; unknown flags and parser messages are recorded.",
    "Named game-controlled placeholders are removed from static art, except the source lane backgrounds. Their bindings remain in the map.",
    "The snapshot is sampled from each movie's own loop, not a claim about runtime defaults, native timing, or a complete historical version.",
    "Common texture family is an explicit caller selection. No cross-family or cross-library fallback is used.",
]


def digest(path: Path) -> str:
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def read_json(path: Path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path: Path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8", newline="\n")


def inside(root: Path, relative: str) -> Path:
    path = (root / unquote(relative)).resolve(strict=True)
    if not path.is_relative_to(root) or not path.is_file():
        raise ValueError(f"Input is not a regular file inside its library: {relative}")
    return path


def reference_frame(frame_count: int, fps: float, labels: dict) -> tuple[int, str]:
    if frame_count < 1 or not math.isfinite(fps) or fps <= 0:
        raise ValueError("Movie requires a positive finite frame rate and at least one frame")
    loop = labels.get("loop")
    if loop is not None:
        if isinstance(loop, bool) or not isinstance(loop, int) or not 0 <= loop < frame_count:
            raise ValueError("Movie loop label is outside its frame range")
        return min(frame_count, loop + round(fps) + 1), "one source second after the movie's own zero-based loop label; clamped to its last frame"
    return frame_count, "last source frame; the movie has no loop label"


def cabinet_loop_control(movie):
    """Translate only the observed final aeplib.deepGotoAndPlay(this, 'loop')."""
    controls, retained, tag_ids = [], [], set()
    for frame_index, frame in enumerate(movie.frames):
        for tag in movie.tags[frame.start_tag_offset:frame.start_tag_offset + frame.num_tags]:
            if type(tag).__name__ != "AP2DoActionTag":
                continue
            actions = [item for item in tag.bytecode.as_dict()["actions"] if item["action"] != "END"]
            if [item["action"] for item in actions] != ["PUSH", "GET_VARIABLE", "PUSH", "CALL_METHOD"]:
                retained.append({"sourceFrame": frame_index, "sourceBytecode": tag.bytecode.as_dict()})
                continue
            first, _, method, _ = actions
            if (len(first["objects"]) != 4 or first["objects"][:3] != [repr("loop"), "THIS", "2"]
                    or not first["objects"][3].endswith(": aeplib)")
                    or len(method["objects"]) != 1 or not method["objects"][0].endswith(": deepGotoAndPlay)")
                    or frame_index != len(movie.frames) - 1):
                retained.append({"sourceFrame": frame_index, "sourceBytecode": tag.bytecode.as_dict()})
                continue
            loop = movie.labels.get("loop")
            if type(loop) is not int or not 0 <= loop < frame_index:
                raise ValueError("Cabinet loop target is absent or outside the preceding source frames")
            controls.append({"sourceFrame": frame_index, "loopStartFrame": loop,
                             "method": "aeplib.deepGotoAndPlay", "sourceBytecode": tag.bytecode.as_dict()})
            tag_ids.add(id(tag))
    if len(controls) != 1:
        raise ValueError("A complete cabinet motion export requires one directly evidenced final root loop")
    # Initialisation, such as the 12 SP source frameOffset assignment, remains
    # with the offline evaluator. Its warnings are checked after recording.
    controls[0]["retainedRootActions"] = retained
    return controls[0], tag_ids


def load_api(bemani_root: Path, image_tools: Path):
    sys.path.insert(0, str(image_tools))
    # The parser is independent of bemaniutils' unrelated web/server dependencies.
    for name, path in (("bemani", bemani_root / "bemani"),
                       ("bemani.format", bemani_root / "bemani/format"),
                       ("bemani.format.afp", bemani_root / "bemani/format/afp")):
        if not path.is_dir():
            raise ValueError(f"Missing external parser module: {path}")
        module = types.ModuleType(name)
        module.__path__ = [str(path)]
        sys.modules[name] = module
    from PIL import Image
    from bemani.format.afp.geo import Shape
    from bemani.format.afp.render import AFPRenderer, PlacedClip, PlacedShape
    from bemani.format.afp.swf import SWF, AP2DefineSpriteTag, AP2PlaceObjectTag
    from bemani.format.afp.types import Color, Point

    class AuditSWF(SWF):
        def _SWF__parse_tag(self, ap2_version, afp_version, ap2data, tagid, size,
                           dataoffset, tag_parent_sprite, tag_frame, prefix=""):
            tag = super()._SWF__parse_tag(ap2_version, afp_version, ap2data, tagid, size,
                                        dataoffset, tag_parent_sprite, tag_frame, prefix)
            if isinstance(tag, AP2PlaceObjectTag):
                flags = struct.unpack_from("<I", ap2data, dataoffset)[0]
                if flags & 0x80000000:
                    flags |= struct.unpack_from("<I", ap2data, dataoffset + 8)[0] << 32
                tag.audit_flags, tag.audit_frame, tag.audit_sprite = flags, tag_frame, tag_parent_sprite
            return tag

    class AuditRenderer(AFPRenderer):
        def __init__(self, *, names, draw, static, **kwargs):
            super().__init__(**kwargs)
            self.names, self.draw, self.static = names, draw, static
            self.stack, self.placements, self.excluded = [], [], []

        def _AFPRenderer__apply_mask(self, parent_mask, transform, projection, mask):
            if not self.draw:
                return parent_mask
            return super()._AFPRenderer__apply_mask(parent_mask, transform, projection, mask)

        def _AFPRenderer__render_object(self, img, obj, parent_transform, parent_projection,
                                       parent_mask, parent_mult, parent_add, parent_hsl,
                                       parent_blend, only_depths=None, prefix=""):
            if not obj.visible:
                return img
            parent_tag = self.stack[-1]["tag_id"] if self.stack else None
            name = self.names.get((parent_tag, obj.object_id, obj.depth))
            textures = [p.region for p in obj.source.draw_params if p.region] if isinstance(obj, PlacedShape) else []
            if self.static and ((name and name not in KEEP_NAMED) or DROP_TEXTURES.intersection(textures)):
                self.excluded.append({"name": name, "source": obj.source.reference, "textures": textures})
                return img
            transform = obj.transform.multiply(parent_transform).translate(Point.identity().subtract(obj.rotation_origin))
            mult = (obj.mult_color or Color(1, 1, 1, 1)).multiply(parent_mult)
            add = (obj.add_color or Color(0, 0, 0, 0)).multiply(parent_mult).add(parent_add)
            blend = obj.blend or 0
            if parent_blend not in {0, 1, 2} and blend in {0, 1, 2}:
                blend = parent_blend
            entry = {"tag_id": obj.source.tag_id, "object_id": obj.object_id, "depth": obj.depth,
                     "instance_name": name, "source": obj.source.reference,
                     "matrix": transform.as_dict(), "local_matrix": obj.transform.as_dict(),
                     "rotation_origin": obj.rotation_origin.as_dict(), "blend": blend,
                     "mult_color": mult.as_dict(), "add_color": add.as_dict(),
                     "kind": type(obj).__name__, "mask_present": obj.mask is not None,
                     "projection": obj.projection if hasattr(obj, "projection") else None}
            if isinstance(obj, PlacedClip):
                entry["current_frame_1based"] = obj.frame
            if isinstance(obj, PlacedShape):
                points = [transform.multiply_point(p) for p in obj.source.vertex_points]
                entry.update({"textures": textures, "projected_vertices": [p.as_dict() for p in points],
                              "source_vertices": [p.as_dict() for p in obj.source.vertex_points],
                              "uv": [p.as_dict() for p in obj.source.tex_points],
                              "draw_params": [p.as_dict() for p in obj.source.draw_params],
                              "scale_x": math.hypot(transform.a, transform.b),
                              "scale_y": math.hypot(transform.c, transform.d)})
                if points:
                    entry["bounds"] = {"left": min(p.x for p in points), "right": max(p.x for p in points),
                                       "top": min(p.y for p in points), "bottom": max(p.y for p in points)}
                entry["layer_path"] = [{"depth": p["depth"], "tag_id": p["tag_id"], "instance_name": p["instance_name"]}
                                       for p in [*self.stack, entry]]
            self.placements.append(entry)
            self.stack.append(entry)
            try:
                if not self.draw and not isinstance(obj, PlacedClip):
                    return img
                return super()._AFPRenderer__render_object(img, obj, parent_transform, parent_projection,
                                                          parent_mask, parent_mult, parent_add, parent_hsl,
                                                          parent_blend, only_depths, prefix)
            finally:
                self.stack.pop()

    return types.SimpleNamespace(Image=Image, Shape=Shape, SWF=AuditSWF, Renderer=AuditRenderer,
                                 SpriteTag=AP2DefineSpriteTag, PlaceTag=AP2PlaceObjectTag, Color=Color)


def walk(tags, sprite_type):
    for tag in tags:
        yield tag
        if isinstance(tag, sprite_type):
            yield from walk(tag.tags, sprite_type)


class Research:
    def __init__(self, library: Path, api, common_family: str):
        self.root, self.api, self.common_family = library, api, common_family
        data = read_json(library / "library.json")
        if data.get("status") != "complete":
            raise ValueError("Use a completed independently verified art library")
        self.exclusions = data.get("excludedSources", [])
        self.textures, self.used = {}, {}
        for item in data["textures"]:
            package = item["package"].removeprefix("contents/data/graphic/").removesuffix(".ifs")
            key = package, Path(item["name"]).stem
            if key in self.textures:
                raise ValueError(f"Ambiguous texture identity: {key}")
            self.textures[key] = item

    def source(self, relative: str):
        path = inside(self.root, relative)
        self.used[relative] = {"path": relative, "bytes": path.stat().st_size, "sha256": digest(path)}
        return path

    def resolve(self, name, package):
        if (package, name) in self.textures:
            selected, reason = self.textures[package, name], "package-local source texture"
        else:
            candidates = [(p, self.textures[p, name]) for p in (f"{self.common_family}/{s}" for s in COMMON_PACKAGES)
                          if (p, name) in self.textures]
            if not candidates:
                return {"missing": True, "candidates": [], "assetResolution": "unresolved in the selected library and explicit common family"}
            if len({item["sha256"] for _, item in candidates}) != 1:
                raise ValueError(f"Conflicting shared texture bytes for {package}/{name}")
            package, selected = candidates[0]
            reason = "explicit common-family source; historical default binding not inferred"
        return {"asset": {"root": "primary", "package": package, "name": name,
                          "width": selected["width"], "height": selected["height"],
                          "imageRect": selected["imageRect"], "uvRect": selected["uvRect"],
                          "sprite": selected["sprite"], "sha256": selected["sha256"]},
                "assetResolution": reason}

    def run_style(self, style: int, output: Path):
        self.used = {}
        package = f"gmframe{style:02d}"
        base = f"packages/contents/data/graphic/{package}/raw"
        # A complete layout export must include the separately authored single
        # player wide movies. The shared SP movie cannot stand in for them.
        for movie_name in LAYOUTS:
            self.source(f"{base}/afp/{movie_name}")
            self.source(f"{base}/afp/bsi/{movie_name}")
        folder = output / package
        folder.mkdir(exist_ok=False)
        write_json(folder / "run.json", {"status": "running", "contract": CONTRACT, "package": package,
                                         "expectedLayouts": list(LAYOUTS)})
        shapes, geometry = {}, []
        for path in sorted((self.root / base / "geo").iterdir()):
            if not path.is_file():
                continue
            relative = path.relative_to(self.root).as_posix()
            shape = self.api.Shape(path.name, self.source(relative).read_bytes())
            shape.parse(text_obfuscated=False)
            shapes[path.name] = shape
            write_json(folder / "geometry" / f"{path.name}.json", shape.as_dict())
            geometry.append({"name": path.name, "textures": [p.region for p in shape.draw_params if p.region],
                             "vertices": len(shape.vertex_points), "bounds": shape.bounds.as_dict()})
        write_json(folder / "geometry.json", geometry)
        used_names = {name for row in geometry for name in row["textures"]}
        # Read just the actual texture closure, never the other library's media.
        images, texture_sources = {}, {}
        for name in sorted(used_names):
            resolution = self.resolve(name, package)
            texture_sources[name] = resolution
            if "asset" in resolution:
                asset = resolution["asset"]
                path = self.source(asset["sprite"])
                if digest(path) != asset["sha256"]:
                    raise ValueError(f"Texture differs from the independently extracted inventory: {name}")
                with self.api.Image.open(path) as image:
                    images[name] = image.convert("RGBA")
        reference = {"schema_version": 1, "contract": CONTRACT, "package": package,
                     "common_family": self.common_family, "evidence_limits": LIMITS,
                     "layouts": {}, "effect_timelines": []}
        static_receipt = {"rules": {"keep_named_instances": sorted(KEEP_NAMED), "drop_other_named_instances": True,
                                    "drop_textures": sorted(DROP_TEXTURES)}, "limitations": LIMITS, "frames": []}
        (folder / "static-frames").mkdir()
        for movie_name in LAYOUTS:
            log = io.StringIO()
            with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                movie = self.api.SWF(movie_name, self.source(f"{base}/afp/{movie_name}").read_bytes(),
                                     self.source(f"{base}/afp/bsi/{movie_name}").read_bytes())
                movie.parse()
                tags = list(walk(movie.tags, self.api.SpriteTag))
                frame, selection = reference_frame(len(movie.frames), movie.fps, movie.labels)
                stage = {"width": movie.location.width, "height": movie.location.height, "fps": movie.fps}
                if "stage" in reference and reference["stage"] != stage:
                    raise ValueError("One cabinet's layout canvases differ; retain a separate consumer rather than normalising silently")
                reference["stage"] = stage
                if movie.location.left != 0 or movie.location.top != 0:
                    raise ValueError("The external renderer does not support a nonzero root viewport origin")
                names = {(tag.audit_sprite, tag.object_id, tag.depth): tag.movie_name for tag in tags
                         if isinstance(tag, self.api.PlaceTag) and tag.movie_name}

                def make_renderer(draw, static):
                    renderer = self.api.Renderer(names=names, draw=draw, static=static, shapes={}, textures={},
                                                 swfs={}, single_threaded=True, enable_aa=False)
                    for name, shape in shapes.items():
                        renderer.add_shape(name, shape)
                    if draw:
                        for name, image in images.items():
                            renderer.add_texture(name, image)
                    renderer.add_swf(movie_name, movie)
                    return renderer

                renderer = make_renderer(False, False)
                list(renderer.render_path(movie_name, background_color=self.api.Color(0, 0, 0, 0), only_frames=[frame]))
                placements = renderer.placements
                static = make_renderer(True, True)
                rendered = list(static.render_path(movie_name, background_color=self.api.Color(0, 0, 0, 0), only_frames=[frame]))
                if len(rendered) != 1:
                    raise ValueError(f"Movie {movie_name} did not reach selected frame {frame}")
                unresolved = {name for placed in static.placements for name in placed.get("textures", [])
                              if texture_sources[name].get("missing")}
                if unresolved:
                    raise ValueError(f"Static snapshot has unresolved source textures: {sorted(unresolved)}")
                image = rendered[0]
                if image.size != (stage["width"], stage["height"]):
                    raise ValueError("Rendered pixels do not match the original movie canvas")
                filename = f"{movie_name}-static{frame:03d}.png"
                image.save(folder / "static-frames" / filename)
                static_receipt["frames"].append({"movie": movie_name, "file": filename, "frame_1based": frame,
                                                "selection": selection, "canvas": stage, "alpha_bounds": image.getbbox(),
                                                "sha256": digest(folder / "static-frames" / filename),
                                                "excluded": static.excluded,
                                                "included": [p for p in static.placements if p.get("textures")]})
                unknown = [{"sprite_tag_id": tag.audit_sprite, "frame": tag.audit_frame,
                            "object_id": tag.object_id, "depth": tag.depth, "flags": hex(tag.audit_flags),
                            "unknown_flag_bits": [hex(bit) for bit in UNKNOWN_BITS if tag.audit_flags & bit]}
                           for tag in tags if isinstance(tag, self.api.PlaceTag) and tag.unrecognized_options]
                draws = []
                for index, placed in enumerate(placements):
                    for name in placed.get("textures", []):
                        draws.append({"draw_order": index, "geometry": placed["source"], "texture": name,
                                      **texture_sources[name],
                                      **{key: placed[key] for key in ("matrix", "bounds", "scale_x", "scale_y", "blend",
                                                                     "mult_color", "add_color", "layer_path", "source_vertices",
                                                                     "projected_vertices", "uv", "draw_params", "mask_present")},
                                      "game_bindings": [p["instance_name"] for p in placed["layer_path"] if p["instance_name"]]})
                reference["layouts"][movie_name] = {
                    "package": package, "stage": movie.location.as_dict(), "reference_frame_1based": frame,
                    "reference_frame_selection": selection, "total_frames": len(movie.frames), "labels_0based": movie.labels,
                    "imports": {key: value.as_dict() for key, value in movie.imported_tags.items()},
                    "unrecognized_flags": dict(Counter(bit for row in unknown for bit in row["unknown_flag_bits"])),
                    "draws": draws, "static_frame": f"static-frames/{filename}",
                    "dynamic_instances": [{"name": p["instance_name"], "source_tag": p["tag_id"], "object_id": p["object_id"],
                                           "depth": p["depth"], "matrix": p["matrix"], "local_matrix": p["local_matrix"],
                                           "frame_1based": p.get("current_frame_1based")}
                                          for p in placements if p.get("instance_name")]}
                write_json(folder / "mapping" / f"{movie_name}.json", {"placements": placements, "unknown_placements": unknown})
                write_json(folder / "afp" / f"{movie_name}.json", movie.as_dict())
            (folder / f"{movie_name}.log").write_text(log.getvalue(), encoding="utf-8", newline="\n")
            write_json(folder / "skin-reference-map.json", reference)
            write_json(folder / "static-frames/receipt.json", static_receipt)
            print(f"{package}/{movie_name}: {stage['width']}x{stage['height']} frame {frame}, {len(draws)} source draws", flush=True)
        for image in images.values():
            image.close()
        write_json(folder / "run.json", {"status": "complete", "contract": CONTRACT, "package": package,
                                        "layouts": len(reference["layouts"]), "sourceOnly": True,
                                        "exportedLayouts": list(reference["layouts"])})


def export_recorded(research, style, output, bemani, image_tools):
    """Record original source trees; PNGs remain byte-preserving copy instructions."""
    from build_fullcombo_catalog import (InternTable, LocalStateRecorder, all_tags, exporter_classes,
                                        load_tools, point_values, rgba_values, to_recorded_animation)
    import brotli

    tools = load_tools(bemani, image_tools)
    source_swf, state_renderer = exporter_classes(tools)
    package = f"gmframe{style:02d}"
    folder = output / package
    reference = read_json(folder / "skin-reference-map.json")
    if read_json(folder / "run.json")["status"] != "complete":
        raise ValueError("Finish this cabinet's source layout research before recording it")
    target = folder / "recorded"
    target.mkdir(exist_ok=False)
    write_json(target / "run.json", {"status": "running", "contract": CONTRACT, "package": package})
    research.used = {}
    base = f"packages/contents/data/graphic/{package}/raw"
    shapes = {}
    for path in sorted((research.root / base / "geo").iterdir()):
        if path.is_file():
            shape = tools.Shape(path.name, research.source(path.relative_to(research.root).as_posix()).read_bytes())
            shape.parse(text_obfuscated=False)
            shapes[path.name] = shape
    names = sorted({draw.region for shape in shapes.values() for draw in shape.draw_params if draw.region is not None})
    texture_rows, texture_ids, atlas_sizes, samplers = [], {}, {}, {}
    for name in names:
        selected = research.resolve(name, package)
        if "asset" not in selected:
            continue
        asset = selected["asset"]
        source_package = asset["package"]
        if source_package not in samplers:
            xml = research.source(f"packages/contents/data/graphic/{source_package}/raw/tex/texturelist.xml")
            samplers[source_package] = {}
            for atlas in ET.parse(xml).getroot().findall("texture"):
                size = [int(value) for value in atlas.findtext("size").split()]
                sampler = {field: atlas.attrib[field] for field in ("min_filter", "mag_filter", "wrap_s", "wrap_t")}
                for sprite in atlas.findall("image"):
                    atlas_sizes[source_package, sprite.attrib["name"]] = size
                    samplers[source_package][sprite.attrib["name"]] = sampler
        texture_ids[name] = len(texture_rows)
        texture_rows.append({**asset, "sampler": samplers[source_package][name]})
    geometries, geometry_map = [], {}
    for name, shape in shapes.items():
        draws = []
        for draw in shape.draw_params:
            texture_index = texture_ids.get(draw.region)
            uv = []
            if texture_index is not None:
                texture = texture_rows[texture_index]
                if draw.flags & 0x40:
                    uv = [[point.x, point.y] for point in shape.tex_points]
                else:
                    width, height = atlas_sizes[texture["package"], draw.region]
                    rect = texture["uvRect"]
                    uv = [[(point.x * width - rect[0]) / texture["width"],
                           (point.y * height - rect[2]) / texture["height"]] for point in shape.tex_points]
            entry = {"texture": texture_index, "uv": uv, "indices": list(draw.vertexes), "flags": draw.flags,
                     "sourceColour": rgba_values(draw.blend, (1, 1, 1, 1)) if draw.blend is not None else None}
            if draw.region is not None and texture_index is None:
                entry["unresolvedTexture"] = draw.region
            draws.append(entry)
        geometry_map[name] = len(geometries)
        geometries.append({"source": name, "vertices": [point_values(point) for point in shape.vertex_points],
                           "atlasUV": [point_values(point) for point in shape.tex_points],
                           "colours": [rgba_values(colour, (1, 1, 1, 1)) for colour in shape.tex_colors], "draws": draws})
    resource_ids = {index: f"standard.iidx{style:02d}.t.t" + row["sha256"][:24] for index, row in enumerate(texture_rows)}
    source_table = {}
    for index, row in enumerate(texture_rows):
        identity = resource_ids[index]
        if identity in source_table and (source_table[identity]["sha256"], source_table[identity]["sampler"]) != (row["sha256"], row["sampler"]):
            raise ValueError("Source texture identity collides or has conflicting sampler state")
        source_table[identity] = row
    exported = []
    for movie_name in RECORDED_LAYOUTS:
        movie_target = target / movie_name
        movie_target.mkdir()
        log = io.StringIO()
        with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
            raw = research.source(f"{base}/afp/{movie_name}")
            bsi = research.source(f"{base}/afp/bsi/{movie_name}")
            movie = source_swf(movie_name, raw.read_bytes(), bsi.read_bytes())
            movie.parse()
            control, flow_tags = cabinet_loop_control(movie)
            named = {(tag.export_source_sprite, tag.object_id, tag.depth): tag.movie_name
                     for tag in all_tags(movie.tags, tools.AP2DefineSpriteTag)
                     if isinstance(tag, tools.AP2PlaceObjectTag) and tag.movie_name}
            excluded = Counter()

            class FilteredRenderer(state_renderer):
                def _AFPRenderer__place(self, tag, clip, prefix=""):
                    if id(tag) in flow_tags and clip is self._AFPRenderer__root:
                        return None, False
                    return super()._AFPRenderer__place(tag, clip, prefix)

                def render_path(self, *args, **kwargs):
                    def filtered(obj, parent_tag):
                        name = named.get((parent_tag, obj.object_id, obj.depth))
                        texture_names = [draw.region for draw in obj.source.draw_params if draw.region] if isinstance(obj, tools.PlacedShape) else []
                        if (name and name not in KEEP_NAMED) or DROP_TEXTURES.intersection(texture_names):
                            excluded[name or ",".join(texture_names)] += 1
                            return None
                        clone = copy.copy(obj)
                        if isinstance(obj, tools.PlacedClip):
                            clone.placed_objects = [child for source in obj.placed_objects
                                                    if (child := filtered(source, obj.source.tag_id)) is not None]
                        return clone

                    for image in super().render_path(*args, **kwargs):
                        original = self._AFPRenderer__root
                        self._AFPRenderer__root = filtered(original, None)
                        try:
                            yield image
                        finally:
                            self._AFPRenderer__root = original

            filters = InternTable()
            recorder = LocalStateRecorder(tools, FilteredRenderer)
            root = recorder.record(package, movie_name, movie, {movie_name: movie}, shapes, geometry_map, filters)
        warnings = sorted(set(log.getvalue().splitlines()))
        (movie_target / "source.log").write_text(log.getvalue(), encoding="utf-8", newline="\n")
        unsupported = [line for line in warnings if line.startswith("WARNING:")
                       and "includes unparsed options and might not display properly!" not in line]
        if unsupported:
            raise ValueError("Source evaluation reported unsupported behavior: " + "; ".join(unsupported))
        source_geometry = {"geometries": geometries, "filters": filters.values}
        write_json(movie_target / "source-root.json", root)
        write_json(movie_target / "source-geometry.json", source_geometry)
        sample = reference["layouts"][movie_name]["reference_frame_1based"] - 1
        result = {"contract": CONTRACT, "source": {"package": package, "movie": movie_name,
                  "commonFamily": research.common_family, "canvas": root["canvas"], "fps": root["fps"],
                  "sourceFrames": len(movie.frames), "loop": control, "steadyFrameZeroBased": sample,
                  "filteredNamedSamples": dict(excluded), "rules": {"keepNamed": sorted(KEEP_NAMED), "dropTextures": sorted(DROP_TEXTURES)}},
                  "receipt": {"pngBytesPreserved": True, "runtimeFrameRasters": 0, "runtimeSourceInterpreter": False,
                  "hierarchyMathChecks": recorder.math_checks, "maximumHierarchyMathError": recorder.math_max_error,
                  "warnings": warnings, "boundary": "Offline original hierarchy/PNG export; public CLI budget, actual Player, source-loop timing and visual parity require their own checks."}}
        for kind in ("motion", "steady"):
            selected = root if kind == "motion" else {**root, "frames": [root["frames"][sample], {"nodes": [], "camera": None}]}
            clip = to_recorded_animation(source_geometry, selected, resource_ids)
            clip.update({"trigger": "gameplay.attach", "loop": True, "loopStartFrame": control["loopStartFrame"] if kind == "motion" else 0})
            encoded = json.dumps(clip, ensure_ascii=False, separators=(",", ":"), allow_nan=False).encode("utf-8")
            compressed = brotli.compress(encoded, quality=5)
            if len(compressed) > 16 * 1024 * 1024 or len(encoded) > 128 * 1024 * 1024:
                raise ValueError("Source frame graph exceeds the public encoded/decoded clip byte budget")
            clip_file = movie_target / f"{kind}.clip.json.br"
            clip_file.write_bytes(compressed)
            prefix = f"standard.iidx{style:02d}.frame.{MOVIE_IDS[movie_name]}.{kind}"
            archive_path = f"standard/frames/iidx{style:02d}/{movie_name}/{kind}.clip.json.br"
            used = {geometry["resource"] for geometry in clip["geometries"] if geometry["resource"] is not None}
            resources, files = [], []
            for identity in sorted(used):
                row = source_table[identity]
                sampler = row["sampler"]
                if sampler["min_filter"] != sampler["mag_filter"] or sampler["min_filter"] not in ("nearest", "linear") or sampler["wrap_s"] != "clamp" or sampler["wrap_t"] != "clamp":
                    raise ValueError(f"Source requires an unsupported sampler: {sampler}")
                source = research.source(row["sprite"])
                if digest(source) != row["sha256"]:
                    raise ValueError("Original PNG changed during source export")
                path = f"standard/art/{row['sha256']}.png"
                resources.append({"id": identity, "type": "texture", "path": path, "filter": sampler["min_filter"]})
                files.append({"source": str(source), "path": path, "sha256": row["sha256"]})
            files.append({"source": str(clip_file), "path": archive_path, "sha256": digest(clip_file)})
            animation_id = prefix + ".clip"
            result[kind] = {"node": {"id": prefix + ".node", "type": "recorded-animation", "target": {"kind": "global"},
                                     "animation": animation_id, "blend": "alpha", "properties": {"x": 0, "y": 0, "width": 1, "height": 1},
                                     "effects": [], "children": []},
                            "resources": resources, "animations": [{"id": animation_id, "path": archive_path, "resources": sorted(used)}],
                            "tracks": [], "files": files,
                            "receipt": {"nodes": len(clip["nodes"]), "states": len(clip["states"]), "geometries": len(clip["geometries"]),
                                        "framesIncludingEmptyEnd": len(clip["frames"]), "loopStartFrame": clip["loopStartFrame"],
                                        "encodedBytes": len(compressed), "decodedBytes": len(encoded)}}
        write_json(movie_target / "export.json", result)
        exported.append({"movie": movie_name, "path": f"{movie_name}/export.json", "motion": result["motion"]["receipt"], "steady": result["steady"]["receipt"]})
        print(f"{package}/{movie_name}: recorded {root['frameCount']} original frames, loop {control['loopStartFrame']}, no runtime rasters", flush=True)
    write_json(target / "sources.json", {"files": list(research.used.values()), "excludedSources": research.exclusions})
    write_json(target / "run.json", {"status": "complete", "contract": CONTRACT, "package": package, "exports": exported})


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ("library", "bemani-root", "image-tools", "output"):
        parser.add_argument(f"--{name}", type=Path, required=True)
    parser.add_argument("--styles", type=int, nargs="+", required=True)
    parser.add_argument("--recorded", action="store_true", help="Add original PNG/mesh/hierarchy clips to completed layout research")
    parser.add_argument("--common-family", choices=("0", "1"), required=True,
                        help="Explicit common family from the observed source configuration, never inferred from canvas size")
    args = parser.parse_args()
    library, bemani, image_tools = (path.resolve(strict=True) for path in (args.library, args.bemani_root, args.image_tools))
    output = args.output.resolve()
    if not args.styles or len(set(args.styles)) != len(args.styles) or any(not 1 <= value <= 26 for value in args.styles):
        parser.error("Choose unique Standard Model styles in 1..26; each must exist in the supplied library")
    if any(output.is_relative_to(path) or path.is_relative_to(output) for path in (library, bemani, image_tools)):
        parser.error("Output must not overlap any input")
    if output.drive.lower() == os.environ.get("SystemDrive", "C:").lower():
        parser.error("Research output must stay on a non-system drive")
    if not output.parent.is_dir():
        parser.error("Create the dedicated research parent first")
    identity = {"contract": CONTRACT, "librarySha256": digest(library / "library.json"),
                "commonFamily": args.common_family,
                "layoutMovies": list(LAYOUTS), "recordedMovies": list(RECORDED_LAYOUTS),
                "parser": [{"path": str(path.relative_to(bemani)).replace("\\", "/"), "sha256": digest(path)}
                           for path in sorted((bemani / "bemani/format/afp").rglob("*.py"))]}
    if output.exists():
        if not (output / "source.json").is_file() or read_json(output / "source.json") != identity:
            parser.error("Existing research belongs to different inputs; use a new output directory")
    else:
        output.mkdir()
        write_json(output / "source.json", identity)
    for style in args.styles:
        if (output / f"gmframe{style:02d}").exists() and not args.recorded:
            parser.error("Existing style evidence is immutable; use a new output directory")
    api = load_api(bemani, image_tools)
    research = Research(library, api, args.common_family)
    for style in args.styles:
        if not (output / f"gmframe{style:02d}").exists():
            research.run_style(style, output)
            write_json(output / f"sources-{style:02d}.json", {"contract": CONTRACT, "excludedSources": research.exclusions,
                                                            "files": list(research.used.values())})
        if args.recorded:
            export_recorded(research, style, output, bemani, image_tools)
    print(f"Complete: {len(args.styles)} independently sourced cabinets", flush=True)


if __name__ == "__main__":
    main()
