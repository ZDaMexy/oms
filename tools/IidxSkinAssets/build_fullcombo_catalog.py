"""Export original full-combo movies as immutable local-state frame graphs.

Only previously verified F-drive IFS copies are accepted. The exporter evaluates
the pinned AFP tooling offline; generated consumers never execute AFP bytecode.
"""

import argparse
import base64
from collections import Counter
import contextlib
import hashlib
import io
import json
import math
from pathlib import Path
import struct
import sys
import types
import xml.etree.ElementTree as ET

sys.dont_write_bytecode = True

ROOTS = (("p1", "1p_fullcombo"), ("p2", "2p_fullcombo"), ("dp", "dp_fullcombo"))
MATRIX_FIELDS = ("a11", "a12", "a13", "a21", "a22", "a23", "a31", "a32", "a33", "a41", "a42", "a43")
STATE_FIELDS = ("matrix", "origin", "projection", "mult", "add", "hsl", "blend", "mask", "filters", "visible")


def read_json(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_json(path, value):
    if path.exists():
        raise FileExistsError(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, separators=(",", ":"), allow_nan=False) + "\n", encoding="utf-8")


def digest(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def content_key(value):
    return json.dumps(value, sort_keys=True, separators=(",", ":"), allow_nan=False)


class InternTable:
    def __init__(self):
        self.values = []
        self.lookup = {}

    def add(self, value):
        key = content_key(value)
        if key not in self.lookup:
            self.lookup[key] = len(self.values)
            self.values.append(value)
        return self.lookup[key]


def load_tools(bemani_root, image_tools):
    sys.path.insert(0, str(image_tools))
    for name, path in (("bemani", bemani_root / "bemani"),
                       ("bemani.format", bemani_root / "bemani/format"),
                       ("bemani.format.afp", bemani_root / "bemani/format/afp")):
        module = types.ModuleType(name)
        module.__path__ = [str(path)]
        sys.modules[name] = module
    from ifstools import IFS
    from ifstools.handlers.image_file import ImageFile
    from PIL import Image
    from bemani.format.afp.swf import SWF, AP2PlaceObjectTag, AP2DefineSpriteTag
    from bemani.format.afp.geo import Shape
    from bemani.format.afp.render import AFPRenderer, PlacedClip, PlacedShape, PlacedImage, PlacedDummy
    from bemani.format.afp.types import Color, Matrix, Point
    return types.SimpleNamespace(**locals())


def matrix_values(matrix):
    return [getattr(matrix, field) for field in MATRIX_FIELDS]


def point_values(point):
    return [point.x, point.y, point.z]


def rgba_values(colour, default):
    return [getattr(colour, c) for c in "rgba"] if colour is not None else list(default)


def matrix4(values):
    return [values[:3] + [0], values[3:6] + [0], values[6:9] + [0], values[9:12] + [1]]


def multiply4(left, right):
    return [[sum(left[r][k] * right[k][c] for k in range(4)) for c in range(4)] for r in range(4)]


def world_matrix(local, origin, parent):
    translation = [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [-origin[0], -origin[1], -origin[2], 1]]
    return multiply4(multiply4(translation, matrix4(local)), parent)


def all_tags(tags, sprite_type):
    for tag in tags:
        yield tag
        if isinstance(tag, sprite_type):
            yield from all_tags(tag.tags, sprite_type)


def exporter_classes(tools):
    class SourceSWF(tools.SWF):
        def __init__(self, *args, **kwargs):
            self.export_contexts = []
            self.export_filter_blocks = []
            super().__init__(*args, **kwargs)

        def _SWF__parse_tag(self, *args, **kwargs):
            context = {"data": args[2], "last": None, "filters": []}
            self.export_contexts.append(context)
            try:
                tag = super()._SWF__parse_tag(*args, **kwargs)
                tag.export_filters = context["filters"]
                if isinstance(tag, tools.AP2PlaceObjectTag):
                    flags = struct.unpack_from("<I", args[2], args[5])[0]
                    if flags & 0x80000000:
                        flags |= struct.unpack_from("<I", args[2], args[5] + 8)[0] << 32
                    tag.export_flags = flags
                    tag.export_source_frame = args[7]
                    tag.export_source_sprite = args[6]
                    if tag.unrecognized_options and flags & ~0x10000:
                        unknown = flags & (0x40 | 0x100000 | 0x800000000 | 0x1000000000 | 0x2000000000)
                        if unknown:
                            raise ValueError(f"Unrecorded placement flags: {unknown:#x}")
                return tag
            finally:
                self.export_contexts.pop()

        def add_coverage(self, offset, length, unique=True):
            if self.export_contexts:
                self.export_contexts[-1]["last"] = offset
            return super().add_coverage(offset, length, unique)

        def vprint(self, *args, **kwargs):
            if args and "Unknown Filter data Count:" in str(args[0]):
                context = self.export_contexts[-1]
                offset = context["last"]
                count, length = struct.unpack_from("<HH", context["data"], offset)
                raw = context["data"][offset:offset + length]
                if len(raw) != length or length < 4 + count * 4:
                    raise ValueError("Invalid source filter offset table")
                offsets = struct.unpack_from("<" + "I" * count, raw, 4)
                entries = []
                for index, start in enumerate(offsets):
                    end = offsets[index + 1] if index + 1 < count else length
                    if not 4 + count * 4 <= start < end <= length:
                        raise ValueError("Source filter offset escapes block")
                    kind = struct.unpack_from("<I", raw, start)[0]
                    entry = {"type": kind, "rawBase64": base64.b64encode(raw[start:end]).decode("ascii")}
                    if kind == 6 and end - start == 84:
                        fixed = list(struct.unpack_from("<20i", raw, start + 4))
                        entry.update({"kind": "colour-matrix", "fixed16": fixed,
                                      "matrix4x5": [value / 65536 for value in fixed]})
                    else:
                        raise ValueError(f"Unsupported actual source filter type/size: {kind}/{end - start}")
                    entries.append(entry)
                block = {"rawBase64": base64.b64encode(raw).decode("ascii"), "entries": entries}
                context["filters"].append(block)
                self.export_filter_blocks.append(block)
            return super().vprint(*args, **kwargs)

    class StateRenderer(tools.AFPRenderer):
        def _AFPRenderer__render_object(self, image, *args, **kwargs):
            return image

        def _AFPRenderer__place(self, tag, clip, prefix=""):
            old = next((obj for obj in clip.placed_objects
                        if isinstance(tag, tools.AP2PlaceObjectTag) and obj.object_id == tag.object_id and obj.depth == tag.depth), None)
            previous = getattr(old, "export_filters", [])
            result = super()._AFPRenderer__place(tag, clip, prefix)
            if isinstance(tag, tools.AP2PlaceObjectTag):
                matches = [obj for obj in clip.placed_objects if obj.object_id == tag.object_id and obj.depth == tag.depth]
                if matches:
                    obj = matches[-1]
                    if tag.export_flags & 0x10000:
                        obj.export_filters = tag.export_filters
                    else:
                        obj.export_filters = previous if tag.update else []
                    obj.export_placement = [tag.export_source_sprite, tag.export_source_frame, tag.export_flags]
            return result

    return SourceSWF, StateRenderer


def to_recorded_animation(package, root, resource_ids):
    """Convert evaluated source data to the bounded, generic runtime clip schema.

    resource_ids maps catalog texture integer ids to the caller's manifest ids.
    This function never opens an IFS, loads an image or evaluates source code.
    """
    geometries, geometry_map = [], {}
    nodes, node_map, states = [], {}, InternTable()

    def geometry(source_index):
        if source_index in geometry_map:
            return geometry_map[source_index]
        source = package["geometries"][source_index]
        if source["colours"]:
            raise ValueError("Actual source vertex colours require an explicit runtime geometry field")
        if len(source["draws"]) != 1:
            raise ValueError(f"Actual source mesh has multiple draws: {source['source']}")
        draw = source["draws"][0]
        if draw.get("unresolvedTexture") is not None:
            raise ValueError(f"Selected source geometry needs an explicit texture dependency: {draw['unresolvedTexture']}")
        if not draw["flags"] & 1:
            geometry_map[source_index] = -1
            return -1
        colour = None
        if draw["texture"] is None:
            points = source["vertices"]
            if draw["flags"] != 9 or len(points) != 4 or draw["sourceColour"] is None:
                raise ValueError(f"Actual unsupported solid source primitive: {source['source']}")
            left, right = min(p[0] for p in points), max(p[0] for p in points)
            top, bottom = min(p[1] for p in points), max(p[1] for p in points)
            if [p[:2] for p in points] != [[left, top], [right, top], [left, bottom], [right, bottom]]:
                raise ValueError("Source solid rectangle corner order differs")
            uv = [[0, 0], [1, 0], [0, 1], [1, 1]]
            indices = [0, 1, 2, 2, 1, 3]
            resource = None
            colour = draw["sourceColour"]
        else:
            if draw["sourceColour"] is not None:
                raise ValueError("Actual textured source geometry also has a source fill colour")
            uv, indices, resource = draw["uv"], draw["indices"], resource_ids[draw["texture"]]
        vertices = [[*point, *point_uv] for point, point_uv in zip(source["vertices"], uv)]
        if len(vertices) != len(source["vertices"]):
            raise ValueError("UV/vertex cardinality mismatch")
        identifier = len(geometries)
        value = {"resource": resource, "vertices": vertices, "triangles": indices}
        if colour is not None:
            value["colour"] = colour
        geometries.append(value)
        geometry_map[source_index] = identifier
        return identifier

    output_frames = []
    for frame in root["frames"]:
        mapped, hidden, output_nodes, effective_blends = {}, set(), [], {}
        for source_id, state_id in frame["nodes"]:
            node = root["nodes"][source_id]
            state = root["states"][state_id]
            if not state["visible"] or node["parent"] in hidden:
                hidden.add(source_id)
                continue
            parent = mapped[node["parent"]] if node["parent"] >= 0 else -1
            if state["blend"] not in (0, 1, 2, 8):
                raise ValueError(f"Actual unsupported source blend: {state['blend']}")
            parent_blend = effective_blends.get(node["parent"], 0)
            source_blend = state["blend"]
            if parent_blend not in (0, 1, 2) and source_blend in (0, 1, 2):
                source_blend = parent_blend
            effective_blends[source_id] = source_blend
            # Runtime mesh blend is explicit; source container inheritance is
            # resolved here while matrices/colours remain local to the tree.
            blend = "additive" if source_blend == 8 else "normal"
            key = (parent, source_id, blend)
            if key not in node_map:
                node_map[key] = len(nodes)
                nodes.append({"parent": parent, "geometry": geometry(node["geometry"]) if node["geometry"] is not None else -1,
                              "blend": blend})
            mapped[source_id] = node_map[key]
            colour_matrices = [entry["matrix4x5"] for index in state["filters"] for entry in package["filters"][index]["entries"]]
            if len(colour_matrices) > 1:
                raise ValueError("Actual stacked source colour filters require explicit composition semantics")
            projection = {0: "affine", 1: "affine", 2: "perspective"}[state["projection"]]
            output_state = {"matrix": state["matrix"], "origin": state["origin"], "projection": projection,
                            "multiply": state["mult"], "add": state["add"], "hsl": state["hsl"], "mask": state["mask"],
                            "colourMatrix": colour_matrices[0] if colour_matrices else None}
            output_nodes.append([mapped[source_id], states.add(output_state)])
        camera = root["cameras"][frame["camera"]] if frame["camera"] is not None else None
        output_frames.append({"nodes": output_nodes, "camera": [*camera["centre"], camera["focalLength"]] if camera else None})
    return {"contract": "oms-gameplay-recorded-animation.v1", "width": root["canvas"][0], "height": root["canvas"][1],
            "fps": root["fps"], "geometries": geometries, "nodes": nodes, "states": states.values,
            "frames": output_frames, "loop": False, "trigger": "gameplay.complete"}


def default_resource_id(texture):
    # Every stable-id dot component starts with a letter, including a SHA prefix.
    return "fc.t.t" + texture["sha256"][:24]


def export_runtime_catalog(source_path, prefix):
    """Convert an existing source catalog without decoding or copying its PNGs.

    The fresh sibling catalog uses paths relative to the same media root. Old
    source data and earlier runtime examples remain untouched.
    """
    source_path = source_path.resolve(strict=True)
    source = read_json(source_path)
    if source["schema"] != "source-recorded-clip-catalog.v1":
        raise ValueError("Not a local-state source catalog")
    if not prefix or not all(c in "abcdefghijklmnopqrstuvwxyz0123456789-" for c in prefix):
        raise ValueError("Runtime prefix must be a lowercase directory name")
    media = source_path.parent
    destination = media / prefix
    catalog_path = media / (prefix + "-catalog.json")
    if destination.exists() or catalog_path.exists():
        raise FileExistsError("Runtime output must be new; earlier exports are retained")
    resources = {texture["id"]: default_resource_id(texture) for texture in source["textures"]}
    if len(set(resources.values())) != len(resources):
        raise ValueError("Source texture resource-id collision")
    destination.mkdir()
    packages = []
    for item in source["packages"]:
        package = read_json(media / item["path"])
        roots = []
        for row in item["roots"]:
            root = read_json(media / row["path"])
            clip = to_recorded_animation(package, root, resources)
            relative = f"{prefix}/{item['id']}/{row['layout']}.clip.json"
            write_json(media / relative, clip)
            roots.append({"layout": row["layout"], "path": relative, "sourceRoot": row["path"],
                          "sourceFrames": root["frameCount"], "durationMs": root["durationMs"],
                          "fps": root["fps"], "nodes": len(clip["nodes"]), "states": len(clip["states"]),
                          "geometries": len(clip["geometries"])})
        packages.append({"id": item["id"], "sourcePackage": item["path"],
                         "sourceArchiveSHA256": item["sourceArchiveSHA256"], "roots": roots})
    result = {"schema": "source-recorded-animation-runtime-catalog.v1", "sourceCatalog": source_path.name,
              "textures": [{**texture, "resource": resources[texture["id"]]} for texture in source["textures"]],
              "packages": packages, "defaultIdentity": source["defaultIdentity"],
              "scope": "Typed local-state runtime clips with complete source timing and terminal empty frames. Original-game colour/filter parity still requires visual comparison."}
    write_json(catalog_path, result)
    return catalog_path


class LocalStateRecorder:
    """Evaluate a bounded source movie into shared local-state frame graphs."""

    def __init__(self, tools, renderer_class):
        self.tools = tools
        self.StateRenderer = renderer_class
        self.math_max_error = 0.0
        self.math_checks = 0

    def record(self, package, layout, movie, movies, shapes, geometry_map, filters, *, frame_count=None):
        frame_count = len(movie.frames) if frame_count is None else frame_count
        if type(frame_count) is not int or not 0 < frame_count <= len(movie.frames):
            raise ValueError("Recorded sample count is outside the source timeline")
        renderer = self.StateRenderer(shapes={}, textures={}, swfs={}, single_threaded=True, enable_aa=False)
        for name, shape in shapes.items():
            renderer.add_shape(name, shape)
        for name, item in movies.items():
            renderer.add_swf(name, item)
        node_lookup, nodes = {}, []
        states, cameras = InternTable(), InternTable()
        frames = []
        maximum_live = 0
        local_checks = 0
        for frame_number, _ in enumerate(renderer.render_path(movie.exported_name, background_color=self.tools.Color(0, 0, 0, 0),
                                                             only_frames=list(range(1, frame_count + 1))), 1):
            graph = []

            def visit(obj, parent_id, parent_world, vendor_parent):
                kind = "container" if isinstance(obj, self.tools.PlacedClip) else "mesh" if isinstance(obj, self.tools.PlacedShape) else "image" if isinstance(obj, self.tools.PlacedImage) else "dummy"
                if kind in {"dummy", "image"}:
                    raise ValueError(f"Actual unimplemented recorded source node kind: {kind}")
                source = obj.source.reference.split(",", 1)[0]
                identity = (parent_id, obj.depth, obj.object_id, obj.source.tag_id, source, kind)
                if identity not in node_lookup:
                    node_lookup[identity] = len(nodes)
                    nodes.append({"id": len(nodes), "parent": parent_id, "depth": obj.depth, "sourceObject": obj.object_id,
                                  "sourceTag": obj.source.tag_id, "source": source, "kind": kind,
                                  "geometry": geometry_map[source] if kind == "mesh" else None})
                identifier = node_lookup[identity]
                origin = point_values(obj.rotation_origin)
                local = matrix_values(obj.transform)
                matrix = world_matrix(local, origin, parent_world)
                vendor_world = obj.transform.multiply(vendor_parent).translate(self.tools.Point(-origin[0], -origin[1], -origin[2]))
                expected = matrix_values(vendor_world)
                actual = [matrix[row][column] for row in range(4) for column in range(3)]
                error = max(abs(a - b) for a, b in zip(actual, expected))
                if error > 1e-8:
                    raise ValueError(f"Local hierarchy matrix mismatch: {error}")
                self.math_max_error = max(self.math_max_error, error)
                self.math_checks += 1
                mask = obj.mask.bounds if obj.mask is not None else None
                hsl = obj.hsl_shift
                state = {"matrix": local, "origin": origin, "projection": obj.projection,
                         "mult": rgba_values(obj.mult_color, (1, 1, 1, 1)), "add": rgba_values(obj.add_color, (0, 0, 0, 0)),
                         "hsl": [hsl.h, hsl.s, hsl.l] if hsl is not None else [0, 0, 0],
                         "blend": obj.blend or 0, "mask": [mask.left, mask.top, mask.right, mask.bottom] if mask is not None else None,
                         "filters": [filters.add(block) for block in getattr(obj, "export_filters", [])], "visible": obj.visible}
                graph.append([identifier, states.add(state)])
                if kind == "container":
                    for _, child in sorted(enumerate(obj.placed_objects), key=lambda pair: (pair[1].depth, pair[0])):
                        visit(child, identifier, matrix, vendor_world)

            root = renderer._AFPRenderer__root
            visit(root, -1, [[1, 0, 0, 0], [0, 1, 0, 0], [0, 0, 1, 0], [0, 0, 0, 1]], self.tools.Matrix.identity())
            if len({node for node, _ in graph}) != len(graph):
                raise ValueError("Source frame graph contains duplicate node identities")
            camera = renderer._AFPRenderer__camera
            camera_id = cameras.add({"centre": point_values(camera.center), "focalLength": camera.focal_length}) if camera is not None else None
            frames.append({"nodes": graph, "camera": camera_id})
            maximum_live = max(maximum_live, len(graph))
            local_checks += len(graph)
        if len(frames) != frame_count:
            raise ValueError("Source root terminated before all declared frames; do not invent missing frames")
        channels = self.node_channels(nodes, states.values, frames)
        # The movie's full frame count is retained. Explicit end clears every draw.
        frames.append({"nodes": [], "camera": None})
        return {"schema": "source-recorded-clip-root.v1", "package": package, "layout": layout,
                "sourceMovie": movie.name, "canvas": [movie.location.width, movie.location.height],
                "fps": movie.fps, "frameCount": frame_count, "sourceDeclaredFrames": len(movie.frames), "durationMs": frame_count * 1000 / movie.fps,
                "frameTime": {"numeratorMs": 1000, "denominator": movie.fps}, "endFrameIndex": frame_count,
                "nodes": nodes, "states": states.values, "cameras": cameras.values, "frames": frames, "nodeChannels": channels,
                "maximumLiveNodes": maximum_live, "hierarchyMathChecks": local_checks,
                "semantics": "Source local hierarchy, no interpolation; frameN occupies[N*1000/fps,(N+1)*1000/fps). Missing nodes are absent. SourceDeclaredFrames records the complete original duration; frameCount records the explicitly evaluated prefix. Last extra frame is empty."}

    @staticmethod
    def node_channels(nodes, states, frames):
        indexes = []
        frame_maps = [dict(frame["nodes"]) for frame in frames]
        for node in nodes:
            identifier = node["id"]
            samples = [(frame_index, mapping.get(identifier)) for frame_index, mapping in enumerate(frame_maps)]
            presence, channels, last_state = [], {field: [] for field in STATE_FIELDS}, None
            last_present = None
            for frame_index, state_id in samples + [(len(frames), None)]:
                present = state_id is not None
                if present != last_present:
                    presence.append([frame_index, present])
                    last_present = present
                if not present:
                    continue
                state = states[state_id]
                for field in STATE_FIELDS:
                    if last_state is None or state[field] != last_state[field]:
                        channels[field].append([frame_index, state_id])
                last_state = state
            indexes.append({"node": identifier, "presence": presence, "fields": {field: values for field, values in channels.items() if values}})
        return indexes


class CatalogBuilder:
    def __init__(self, args):
        self.args = args
        self.repo = args.repo.resolve(strict=True)
        self.output = args.output.resolve()
        self.audit = args.audit_root.resolve(strict=True)
        if self.output.exists():
            raise FileExistsError("Catalog output must be new; previous media is retained")
        if self.output.drive.casefold() == "c:":
            raise ValueError("Catalog media must use a non-system drive")
        if not self.output.parent.is_dir():
            raise ValueError("Catalog output parent must exist")
        self.tools = load_tools(args.bemani_root.resolve(strict=True), args.image_tools.resolve(strict=True))
        self.SourceSWF, self.StateRenderer = exporter_classes(self.tools)
        self.textures = []
        self.texture_keys = {}
        self.package_rows = []
        self.math_max_error = 0.0
        self.math_checks = 0

    def source_copy(self, number):
        record = read_json(self.audit / "packages-v2" / f"fcombo{number:02}.json")["sourceReceipt"]
        path = (self.repo / record["copy"]).resolve(strict=True)
        if not path.is_relative_to(self.repo / "artifacts") or path.drive.casefold() != self.repo.drive.casefold():
            raise ValueError("Only existing F repository artifact copies are accepted")
        if digest(path) != record["sha256"]:
            raise ValueError(f"Verified source copy changed: {path}")
        return path, record

    def add_texture(self, image, sampler, expected):
        data = image.load(crop_to_uvrect=True)
        checksum = hashlib.sha256(data).hexdigest()
        if checksum != expected["sha256"]:
            raise ValueError(f"Original PNG bytes changed: {image.name}")
        key = (checksum, content_key(sampler))
        if key not in self.texture_keys:
            identifier = len(self.textures)
            relative = f"textures/{checksum}.png"
            target = self.output / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.exists():
                if target.read_bytes() != data:
                    raise ValueError("Texture hash collision")
            else:
                target.write_bytes(data)
            self.texture_keys[key] = identifier
            self.textures.append({"id": identifier, "path": relative, "sha256": checksum,
                                  "size": list(image.uv_size), "sampler": sampler})
        return self.texture_keys[key]

    def package(self, number):
        key = f"fcombo{number:02}"
        source, receipt = self.source_copy(number)
        source_audit = read_json(self.audit / "packages-v2" / f"{key}.json")
        archive = self.tools.IFS(str(source), super_abort_if_bad=True)
        try:
            nodes = {node.full_path.replace("\\", "/"): node for node in archive.tree.all_files if getattr(node, "from_ifs", False)}
            images = {node.name.removesuffix(".png"): node for node in nodes.values() if isinstance(node, self.tools.ImageFile)}
            atlas_sizes, samplers = {}, {}
            for atlas in ET.fromstring(nodes["tex/texturelist.xml"].load()).findall("texture"):
                size = [int(v) for v in atlas.findtext("size").split()]
                sampler = {field: atlas.attrib[field] for field in ("min_filter", "mag_filter", "wrap_s", "wrap_t")}
                for image in atlas.findall("image"):
                    atlas_sizes[image.attrib["name"]] = size
                    samplers[image.attrib["name"]] = sampler
            texture_map = {name: self.add_texture(node, samplers[name], source_audit["textures"][name]) for name, node in sorted(images.items())}
            shapes, geometry, geometry_map = {}, [], {}
            for name, node in sorted(nodes.items()):
                if not name.startswith("geo/"):
                    continue
                shape = self.tools.Shape(node.name, node.load())
                shape.parse(text_obfuscated=False)
                shapes[node.name] = shape
                draws = []
                for draw in shape.draw_params:
                    uv = []
                    if draw.region:
                        image = images[draw.region]
                        width, height = atlas_sizes[draw.region]
                        uv = [[(point.x * width - image.uvrect[0]) / image.uv_size[0],
                               (point.y * height - image.uvrect[2]) / image.uv_size[1]] for point in shape.tex_points]
                    draws.append({"texture": texture_map.get(draw.region), "uv": uv,
                                  "indices": list(draw.vertexes), "flags": draw.flags,
                                  "sourceColour": rgba_values(draw.blend, (1, 1, 1, 1)) if draw.blend is not None else None})
                identifier = len(geometry)
                geometry_map[node.name] = identifier
                geometry.append({"id": identifier, "source": node.name,
                                 "vertices": [point_values(point) for point in shape.vertex_points],
                                 "atlasUV": [point_values(point) for point in shape.tex_points],
                                 "colours": [rgba_values(colour, (1, 1, 1, 1)) for colour in shape.tex_colors], "draws": draws})
            movies, filter_table = {}, InternTable()
            captured = io.StringIO()
            with contextlib.redirect_stdout(captured), contextlib.redirect_stderr(captured):
                for name, node in sorted(nodes.items()):
                    if not name.startswith("afp/") or "/" in name[4:] or node.name.endswith(".xml"):
                        continue
                    bsi = nodes.get("afp/bsi/" + node.name)
                    movie = self.SourceSWF(node.name, node.load(), bsi.load() if bsi else b"")
                    movie.parse()
                    movies[node.name] = movie
                    for block in movie.export_filter_blocks:
                        filter_table.add(block)
                roots = []
                for layout, name in ROOTS:
                    root = self.root(key, layout, movies[name], movies, shapes, geometry_map, filter_table)
                    relative = f"{key}/{layout}.json"
                    write_json(self.output / relative, root)
                    roots.append({"layout": layout, "path": relative, "frames": root["frameCount"], "durationMs": root["durationMs"],
                                  "nodes": len(root["nodes"]), "states": len(root["states"]), "cameraStates": len(root["cameras"]),
                                  "frameGraphEntries": sum(len(frame["nodes"]) for frame in root["frames"])})
            if digest(source) != receipt["sha256"]:
                raise ValueError("Source IFS changed during export")
            package = {"schema": "source-recorded-clip-package.v1", "id": key, "sourceArchiveSHA256": receipt["sha256"],
                       "textures": [{"source": name, "resource": index, "sourcePNG": source_audit["textures"][name]["sha256"]} for name, index in texture_map.items()],
                       "geometries": geometry, "filters": filter_table.values, "roots": roots,
                       "sourceRuntimeWarnings": sorted(set(captured.getvalue().splitlines())),
                       "filterNote": "Original filter blocks retained. Type6 is a20 coefficient signed16.16 RGBA colour matrix; no filter is omitted."}
            resources = {item["id"]: default_resource_id(item) for item in self.textures}
            for root_row in roots:
                root = read_json(self.output / root_row["path"])
                clip = to_recorded_animation(package, root, resources)
                relative = f"{key}/{root_row['layout']}.clip.json"
                write_json(self.output / relative, clip)
                root_row["runtimeClip"] = relative
                root_row["runtimeNodeCount"] = len(clip["nodes"])
                root_row["runtimeStateCount"] = len(clip["states"])
            relative = f"{key}/package.json"
            write_json(self.output / relative, package)
            self.package_rows.append({"id": key, "path": relative, "sourceArchiveSHA256": receipt["sha256"], "roots": roots,
                                      "geometryCount": len(geometry), "filterStates": len(filter_table.values)})
            print(f"{key}: {len(geometry)} geometries, {len(filter_table.values)} filter states; {len(roots)} complete roots", flush=True)
        finally:
            archive.close()

    def root(self, package, layout, movie, movies, shapes, geometry_map, filters):
        recorder = LocalStateRecorder(self.tools, self.StateRenderer)
        result = recorder.record(package, layout, movie, movies, shapes, geometry_map, filters)
        self.math_max_error = max(self.math_max_error, recorder.math_max_error)
        self.math_checks += recorder.math_checks
        return result

    def build(self, selected):
        self.output.mkdir()
        for number in selected:
            self.package(number)
        model = {"schema": "source-recorded-clip-catalog.v1", "packages": self.package_rows, "textures": self.textures,
                 "defaultIdentity": "needs-original-menu-confirmation",
                 "mathematics": {"matrixOrder": list(MATRIX_FIELDS), "vectors": "row vectors; xyz1*M",
                                 "world": "T(-rotationOrigin)*localMatrix*parentWorld",
                                 "projection": "If parent perspective, inherit perspective; otherwise local projection. q=focalLength/(worldZ-cameraZ), xy=(worldXY-cameraXY)*q+cameraXY. Clip camera/viewport before emitting triangles.",
                                 "colours": "effectiveMult=localMult*parentMult;effectiveAdd=localAdd*parentMult+parentAdd",
                                 "hsl": "Original local HSL values retained. Pinned evaluator adds hierarchy shifts; original-game colour ordering needs visual comparison.",
                                 "masks": "Source local rectangle transforms with this node world matrix/projection; intersect ancestor masks and root canvas.",
                                 "blends": "Source0 normal alpha;8 additive. Non-normal ancestor blend replaces normal descendant blend, preserving source depth/insertion order.",
                                 "filters": "Original per-node offset-table blocks retained and decoded type6 signed16.16 colourMatrix4x5. Apply as scoped source colour filters; do not remove or double-approximate with tint."},
                 "validation": {"hierarchyMatrixComparisons": self.math_checks, "maximumDoubleError": self.math_max_error,
                                "sourceIFSCopiesUnchanged": True, "sourcePNGBytesUnchanged": True,
                                "uniqueTextureCount": len(self.textures), "uniqueDecodedPixels": sum(t["size"][0] * t["size"][1] for t in self.textures)},
                 "scope": "Offline neutral author data, not proof of completed generic recorded-clip rendering or original game parity. No source AFP bytecode is emitted as a runtime program."}
        write_json(self.output / "catalog.json", model)
        return model


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--audit-root", type=Path)
    parser.add_argument("--bemani-root", type=Path)
    parser.add_argument("--image-tools", type=Path)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--packages", default=",".join(str(i) for i in range(25)))
    parser.add_argument("--convert-catalog", type=Path)
    parser.add_argument("--runtime-prefix", default="runtime-v1")
    args = parser.parse_args()
    if args.convert_catalog is not None:
        if any(value is not None for value in (args.audit_root, args.bemani_root, args.image_tools, args.output)):
            parser.error("Catalog conversion does not decode archives; omit archive export arguments")
        print(export_runtime_catalog(args.convert_catalog, args.runtime_prefix), flush=True)
        return
    if any(value is None for value in (args.audit_root, args.bemani_root, args.image_tools, args.output)):
        parser.error("Archive export requires --audit-root, --bemani-root, --image-tools and --output")
    selected = [int(value) for value in args.packages.split(",")]
    if not selected or len(selected) != len(set(selected)) or any(value < 0 or value > 24 for value in selected):
        parser.error("Packages must be distinct source numbers0..24")
    builder = CatalogBuilder(args)
    model = builder.build(selected)
    print(f"Complete: {len(model['packages'])} packages, {len(model['textures'])} exact source textures", flush=True)


if __name__ == "__main__":
    main()
