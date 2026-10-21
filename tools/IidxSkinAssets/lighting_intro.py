"""Bounded author-only component assembly from audited original IIDX frames.

Original PNGs are copied without re-encoding.  Original 33 component geometry is
sampled once offline; gameplay loads each texture once and applies bounded quad
tracks.  It never decodes full-screen images per frame or changes chart timing.
"""

from __future__ import annotations

import copy
import hashlib
import json
import math
from pathlib import Path
import struct
import xml.etree.ElementTree as ET

CANVAS = (1920, 1080)
CLOCK = "preparation.elapsed"
LAYOUTS = ("p1", "p2", "dp")
PACKAGE = "contents/data/graphic/gmframe33.ifs"
DURATION_MS = 1000
SOURCE_LAST_FRAME = 180
SOURCE_FPS = 60
MAX_SCENE_BYTES = 512 * 1024
MAX_MANIFEST_BYTES = 64 * 1024
MAX_KEYFRAMES = 8192
MOVIES = {"p1": (("center_frame_sp", 270), ("1p_frame", 0)),
          "p2": (("center_frame_sp", -270), ("2p_frame", 0)),
          "dp": (("center_frame_dp_1p", 0),)}
EXPECTED_STATIC_DRAWS = {"center_frame_sp": 1, "1p_frame": 28, "2p_frame": 28, "center_frame_dp_1p": 61}
BGA_APERTURES = {"p1": ((682, 100, 1095, 822),), "p2": ((142, 100, 1095, 822),),
                 "dp": ((23, 253, 346, 260), (23, 547, 346, 260),
                        (1551, 253, 346, 260), (1551, 547, 346, 260))}


def option():
    return {"id": "entrance-animation", "type": "toggle", "label": "Gameplay · 组件入场动画", "default": True}


def _json(path):
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _independent(path, *, exists=True):
    path = Path(path)
    if path.drive.lower() == "d:":
        raise ValueError("Intro authoring only consumes independent copies")
    result = path.resolve(strict=exists)
    if result.drive.lower() == "d:":
        raise ValueError("Intro authoring only consumes independent copies")
    return result


def _contained(root, relative):
    if not isinstance(relative, str) or not relative or ":" in relative or "\\" in relative:
        raise ValueError("An intro source path must be contained and relative")
    path = Path(relative)
    if path.is_absolute() or ".." in path.parts:
        raise ValueError("An intro source path escapes its library")
    result = (root / path).resolve()
    if not result.is_relative_to(root):
        raise ValueError("An intro source path escapes its library")
    return result


def _identity(draw):
    return tuple((part["tag_id"], part["depth"], part["instance_name"]) for part in draw["layer_path"])


def _static_draw(draw):
    names = [part["instance_name"] for part in draw["layer_path"] if part["instance_name"]]
    return all(name in ("lane_1p", "lane_2p") for name in names) and "playm_hs_dot" not in draw["textures"]


def _number(value):
    if isinstance(value, bool) or not isinstance(value, (int, float)) or not math.isfinite(value):
        raise ValueError("Intro geometry must contain finite source numbers")
    return float(value)


def _source_corners(geometry, texture, atlas_size):
    """Only the actual full-UV rectangular source meshes use sprite quads."""
    vertices, uv = geometry["vertex_points"], geometry["tex_points"]
    draws = geometry["draw_params"]
    if (len(vertices) != 4 or len(uv) != 4 or geometry["tex_colors"] or len(draws) != 1
            or draws[0]["region"] != texture["name"].removesuffix(".png")
            or len(draws[0]["vertexes"]) != 6 or set(draws[0]["vertexes"]) != {0, 1, 2, 3}):
        raise ValueError("An actual original component needs a mesh capability beyond the bounded quad helper")
    rect = texture["uvRect"]
    normalized = [(point["x"], point["y"]) if draws[0]["flags"] & 0x40 else (
        (point["x"] * atlas_size[0] - rect[0]) / texture["width"],
        (point["y"] * atlas_size[1] - rect[2]) / texture["height"]) for point in uv]
    result = []
    for expected in ((0, 0), (1, 0), (1, 1), (0, 1)):
        indices = [index for index, point in enumerate(normalized)
                   if max(abs(point[axis] - expected[axis]) for axis in (0, 1)) < 0.00001]
        if len(indices) != 1:
            raise ValueError("The original GEO does not address the complete unmodified UV sprite")
        point = vertices[indices[0]]
        if _number(point["z"]) != 0:
            raise ValueError("The intro source unexpectedly requires perspective geometry")
        result.append((_number(point["x"]), _number(point["y"])))
    return tuple(result)


def _quad(corners, draw, shift=0):
    matrix = draw["matrix"]
    if set(matrix) != {"a", "b", "c", "d", "tx", "ty"}:
        raise ValueError("The intro source unexpectedly requires a non-affine matrix")
    a, b, c, d, tx, ty = (_number(matrix[key]) for key in ("a", "b", "c", "d", "tx", "ty"))
    if any(_number(draw["mult_color"][channel]) != 1 for channel in "rgb") or any(
        _number(draw["add_color"][channel]) != 0 for channel in "rgba"):
        raise ValueError("The intro source unexpectedly requires colour processing beyond alpha")
    alpha = _number(draw["mult_color"]["a"])
    if not 0 <= alpha <= 1 or draw["mask_present"]:
        raise ValueError("The original component has an unsupported mask or alpha")
    # Nine decimal canvas units are below 0.000002 source pixels, and below the
    # final renderer float precision.  The PNG bytes and source matrices remain
    # unchanged in the reference receipt.
    points = [[round((a * x + c * y + tx + shift) / 1920, 9),
               round((b * x + d * y + ty) / 1080, 9), 1] for x, y in corners]
    if any(abs(point[axis]) > 4 for point in points for axis in (0, 1)):
        raise ValueError("Original intro geometry exceeds the public quad budget")
    return {"vertices": points, "opacity": alpha}


def _collapse(values):
    result = []
    for frame, value in values:
        if not result or result[-1][1] != value:
            result.append((frame, value))
    return result


def load_reference(library, research):
    """Resolve audited 33 source layers and their exact complete-UV PNGs.

    research contains the read-only *-intro-geometry.json exports.  The original
    raw AFP hash is checked against the independent library before use.
    """
    library, research = _independent(library), _independent(research)
    index = _json(library / "library.json")
    if index.get("status") != "complete" or index.get("sourceMetadataUnchanged") is not True:
        raise ValueError("The intro requires a completed verified source library")
    by_texture = {(row["package"], row["name"]): row for row in index["textures"]}
    if len(by_texture) != len(index["textures"]):
        raise ValueError("The source library has duplicate exact texture identities")
    atlas_sizes, textures, source_receipts, movies = {}, {}, [], {}

    def texture(name):
        matches = [by_texture[(package, name + ".png")] for package in (
            PACKAGE, "contents/data/graphic/1/gameparts.ifs", "contents/data/graphic/1/common.ifs")
            if (package, name + ".png") in by_texture]
        if not matches:
            raise ValueError("An actual intro texture is missing from the verified source library: " + name)
        row = matches[0]
        key = (row["package"], row["name"])
        if key not in textures:
            data = _contained(library, row["sprite"]).read_bytes()
            if hashlib.sha256(data).hexdigest() != row["sha256"] or len(data) < 33 or data[:8] != b"\x89PNG\r\n\x1a\n" \
                    or struct.unpack(">II", data[16:24]) != (row["width"], row["height"]):
                raise ValueError("A verified original intro PNG changed")
            sampler = row["sampler"]
            if sampler["mag"] != sampler["min"] or sampler["mag"] not in ("nearest", "linear") \
                    or sampler["wrapS"] != "clamp" or sampler["wrapT"] != "clamp":
                raise ValueError("The intro requires unsupported source sampling")
            identifier = "omni.intro.t" + row["sha256"][:24]
            textures[key] = {"id": identifier, "type": "texture", "path": "art/lighting-intro/" + row["sha256"] + ".png",
                             "filter": sampler["mag"], "source": _contained(library, row["sprite"]),
                             "sha256": row["sha256"], "width": row["width"], "height": row["height"]}
        if key not in atlas_sizes:
            raw = library / "packages" / row["package"].removesuffix(".ifs") / "raw/tex/texturelist.xml"
            for atlas in ET.parse(raw).getroot().findall("texture"):
                size = tuple(int(value) for value in atlas.findtext("size").split())
                for image in atlas.findall("image"):
                    atlas_sizes[row["package"], image.attrib["name"] + ".png"] = size
        return row, textures[key], atlas_sizes[key]

    for name in EXPECTED_STATIC_DRAWS:
        source_path = research / (name + "-intro-geometry.json")
        source = _json(source_path)
        if source["sourceMovie"] != name or source["sourceFps"] != SOURCE_FPS or len(source["frames"]) != 181 \
                or [frame["sourceFrameZeroBased"] for frame in source["frames"]] != list(range(181)):
            raise ValueError("An intro reference must contain all 181 original 60Hz frames")
        raw = library / "packages/contents/data/graphic/gmframe33/raw/afp" / name
        if hashlib.sha256(raw.read_bytes()).hexdigest() != source["sourceRawAfpSha256"]:
            raise ValueError("The sampled intro no longer matches its original raw AFP")
        final = [draw for draw in source["frames"][-1]["draws"] if _static_draw(draw)]
        if len(final) != EXPECTED_STATIC_DRAWS[name] or len({_identity(draw) for draw in final}) != len(final):
            raise ValueError("The original final static layer closure differs from the baked Frame's source")
        frame_maps = [{_identity(draw): draw for draw in frame["draws"]} for frame in source["frames"]]
        layers = []
        for final_draw in final:
            if len(final_draw["textures"]) != 1:
                raise ValueError("A source component unexpectedly has multiple textured meshes")
            row, declaration, atlas = texture(final_draw["textures"][0])
            geometry_name = final_draw["source"].split(",", 1)[0]
            geometry = _json(library / "author-data/contents/data/graphic/gmframe33/geo" / (geometry_name + ".json"))
            corners = _source_corners(geometry, row, atlas)
            channel = []
            final_quad = _quad(corners, final_draw)
            for frame, mapping in enumerate(frame_maps):
                draw = mapping.get(_identity(final_draw))
                if draw is None:
                    value = {**final_quad, "opacity": 0}
                else:
                    if draw["textures"] != final_draw["textures"] or draw["blend"] != final_draw["blend"]:
                        raise ValueError("A source component unexpectedly changes material")
                    value = _quad(corners, draw)
                channel.append((frame, value))
            if final_draw["blend"] not in (0, 1, 2, 8):
                raise ValueError("An actual intro layer needs an unsupported blend")
            layers.append({"resource": declaration["id"], "blend": "additive" if final_draw["blend"] == 8 else "alpha",
                           "channel": _collapse(channel), "sourceTexture": row["name"], "sourceGeometry": geometry_name,
                           "layerPath": final_draw["layer_path"]})
        movies[name] = layers
        source_receipts.append({"movie": name, "rawAfpSha256": source["sourceRawAfpSha256"],
                                "referenceSha256": hashlib.sha256(source_path.read_bytes()).hexdigest(),
                                "staticLayers": len(layers), "sourceFps": SOURCE_FPS, "sampledFrames": 181})
    if len({row["id"] for row in textures.values()}) != len({(row["sha256"], row["filter"]) for row in textures.values()}):
        raise ValueError("An original PNG alias conflicts with its texture sampler")
    return {"library": library, "research": research, "movies": movies, "textures": list(textures.values()),
            "receipt": {"sources": source_receipts, "sourceDurationMs": 3000, "originalPngBytesPreserved": True,
                        "scope": "Final static component closure only; transient flourish-only layers and source filters are not claimed"}}


def prepare_assets(reference, output):
    """Copy exact source PNGs once; no per-frame atlas or raster is generated."""
    output = _independent(output)
    if output.is_relative_to(reference["library"]) or reference["library"].is_relative_to(output):
        raise ValueError("The intro author output must not overlap its read-only source library")
    resources, seen, pixels, encoded = [], set(), 0, 0
    for texture in reference["textures"]:
        if texture["id"] in seen:
            continue
        seen.add(texture["id"])
        data = texture["source"].read_bytes()
        if hashlib.sha256(data).hexdigest() != texture["sha256"]:
            raise ValueError("An original intro PNG changed after reference preparation")
        target = _contained(output, texture["path"])
        if target.exists() and target.read_bytes() != data:
            raise ValueError("An intro output conflicts with the original PNG")
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            target.write_bytes(data)
        resources.append({key: texture[key] for key in ("id", "type", "path", "filter")})
        pixels += texture["width"] * texture["height"]
        encoded += len(data)
    return {"resources": resources, "texturePixels": pixels, "decodedTextureBytes": pixels * 4,
            "encodedTextureBytes": encoded, "frameRasters": 0, "originalPngBytesPreserved": True}


def _node(identifier, kind="container", properties=None, *, resource=None, children=()):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
              "properties": properties or {}, "effects": [], "children": list(children)}
    if resource is not None:
        result["resource"] = resource
    return result


def _track(identifier, target, property_name, values, *, kind="tween"):
    return {"id": identifier, "type": kind, "target": target, "property": property_name, "easing": "step",
            "loop": False, "clock": CLOCK,
            "keyframes": [{"id": identifier + ".k" + str(index), "time": time, "value": value}
                          for index, (time, value) in enumerate(values)]}


def _clipped_baked(identifier, rectangle, baked_resource):
    """Sample the existing exact baked background through a bounded viewport.

    The intermediate transform keeps each public relative size/position within
    four, including narrow right-side apertures; it allocates no render target.
    """
    x, y, width, height = rectangle
    if min(width, height) <= 0 or x < 0 or y < 0 or x + width > 1920 or y + height > 1080:
        raise ValueError("An intro clipping rectangle must fit the author canvas")
    canvas = _node(identifier + ".canvas", "sprite", {"x": -x / (width * 32), "y": -y / (height * 32),
                   "width": 1920 / (width * 32), "height": 1080 / (height * 32)}, resource=baked_resource)
    magnifier = _node(identifier + ".space", properties={"width": 4, "height": 4, "scale-x": 8, "scale-y": 8},
                      children=[canvas])
    return _node(identifier, "clip", {"x": x / 1920, "y": y / 1080, "width": width / 1920,
                 "height": height / 1080, "clip-mode": "bounds"}, children=[magnifier])


def original_overlay(reference, layout, prefix, baked_resource, duration_ms):
    children, tracks, used, sources = [], [], set(), []
    for movie, shift in MOVIES[layout]:
        for layer in reference["movies"][movie]:
            index = len(children)
            identifier = prefix + ".n" + str(index)
            children.append(_node(identifier, "projective-sprite", resource=layer["resource"]))
            children[-1]["blend"] = layer["blend"]
            values = []
            for frame, original in layer["channel"]:
                value = copy.deepcopy(original)
                for point in value["vertices"]:
                    point[0] = round(point[0] + shift / 1920, 9)
                values.append((round(frame * duration_ms / SOURCE_LAST_FRAME, 9), value))
            tracks.append(_track(prefix + ".q" + str(index), identifier, "projective-quad", values, kind="frame"))
            used.add(layer["resource"])
            sources.append({"node": identifier, "movie": movie, "texture": layer["sourceTexture"],
                            "geometry": layer["sourceGeometry"], "sourceHorizontalShift": shift})
    # The original centre art contains white host-video backing.  Reuse only
    # the baked author's exact black aperture pixels above source art, while
    # actual BGA and all gameplay objects remain in their native higher slots.
    for index, rectangle in enumerate(BGA_APERTURES[layout]):
        children.append(_clipped_baked(prefix + ".black" + str(index), rectangle, baked_resource))
    return children, tracks, used, {"sourceComponentCount": len(sources), "components": sources,
        "source": "Original 33 final static component closure and source 0..180 world quad/alpha channels",
        "bgaBacking": "Existing baked black pixels in fixed native aperture rectangles; original source PNGs unchanged"}


def adapted_overlay(layout, prefix, baked_resource, duration_ms):
    """Clearly labelled regional author assembly for Frames without this audit."""
    if layout == "p1":
        pieces = [((0, 0, 540, 1080), -540, 0, 0.35), ((540, 0, 1380, 100), 0, -100, 0.5),
                  ((540, 100, 1380, 840), 0, 0, 0), ((540, 940, 1380, 140), 0, 140, 0.7)]
    elif layout == "p2":
        pieces = [((1380, 0, 540, 1080), 540, 0, 0.35), ((0, 0, 1380, 100), 0, -100, 0.5),
                  ((0, 100, 1380, 840), 0, 0, 0), ((0, 940, 1380, 140), 0, 140, 0.7)]
    elif layout == "dp":
        pieces = [((0, 0, 960, 940), -960, 0, 0.4), ((960, 0, 960, 940), 960, 0, 0.4),
                  ((0, 940, 1920, 140), 0, 140, 0.7)]
    else:
        raise ValueError("Unsupported intro layout")
    children, tracks = [], []
    for index, (bounds, dx, dy, end_ratio) in enumerate(pieces):
        identifier = prefix + ".r" + str(index)
        root = _node(identifier, children=[_clipped_baked(identifier + ".clip", bounds, baked_resource)])
        children.append(root)
        for prop, offset, dimension in (("x", dx, 1920), ("y", dy, 1080)):
            if offset:
                track = _track(identifier + "." + prop, identifier, prop,
                               [(0, offset / dimension), (duration_ms * end_ratio, 0)])
                track["easing"] = "out"
                tracks.append(track)
    return children, tracks, set(), {"sourceComponentCount": 0, "regions": len(pieces),
        "source": "Author adaptation: independently clipped existing Frame regions; no original per-Frame AFP motion claimed"}


def _walk(root):
    yield root
    for child in root.get("children", []):
        yield from _walk(child)


def _budget(document, manifest):
    roots = [document["root"], *(template["root"] for template in document["templates"])]
    nodes = [node for root in roots for node in _walk(root)]
    tracks = document["tracks"]
    result = {"sceneBytes": len(json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")),
              "manifestBytes": len(json.dumps(manifest, ensure_ascii=False, separators=(",", ":")).encode("utf-8")),
              "nodes": len(nodes), "tracks": len(tracks), "keyframes": sum(len(track["keyframes"]) for track in tracks),
              "resources": len(manifest["resources"]), "maximumChildren": max(len(node["children"]) for node in nodes)}
    for key, maximum in (("sceneBytes", MAX_SCENE_BYTES), ("manifestBytes", MAX_MANIFEST_BYTES), ("nodes", 2048),
                         ("tracks", 512), ("keyframes", MAX_KEYFRAMES), ("resources", 256), ("maximumChildren", 128)):
        if result[key] > maximum:
            raise ValueError("Intro contribution exceeds existing public budget: " + key)
    if any(len(track["keyframes"]) > 512 for track in tracks):
        raise ValueError("An intro source channel exceeds the per-track keyframe budget")
    if len({node["id"] for node in nodes}) != len(nodes):
        raise ValueError("Intro injection introduced duplicate node identities")
    return result


def inject(document, manifest, *, frame, layout, reference=None, assets=None, duration_ms=DURATION_MS):
    """Replace one baked background owner with a bounded temporary assembly.

    Ordinary source values remain untouched. preparation.elapsed is a read-only
    track clock; when the preparation is unavailable its programs must not be
    applied, leaving the static baked default intact (including after reset).
    """
    if layout not in LAYOUTS or not isinstance(frame, str) or not frame:
        raise ValueError("Intro injection requires an explicit Frame and layout")
    if type(duration_ms) not in (int, float) or not math.isfinite(duration_ms) or not 0 < duration_ms <= 1000:
        raise ValueError("Decorative source timing must fit the explicit one-second author adaptation")
    templates = [item for item in document["templates"] if item["id"].endswith(f".template.{layout}.background")]
    if len(templates) != 1:
        raise ValueError("The selected Frame must have one exact background owner")
    root = templates[0]["root"]
    if root["type"] != "sprite" or root.get("slot") != "stage.background" or root["children"] or not root.get("resource"):
        raise ValueError("The original baked background owner changed or already has an intro")
    prefix = "li." + frame + "." + layout
    baked_resource = root["resource"]
    if frame == "iidx33":
        if reference is None or assets is None:
            raise ValueError("33 original assembly requires its verified source reference and exact PNG assets")
        children, tracks, used, receipt = original_overlay(reference, layout, prefix, baked_resource, duration_ms)
        declarations = {item["id"]: item for item in assets["resources"]}
        current = {item["id"]: item for item in manifest["resources"]}
        for identifier in sorted(used):
            resource = declarations[identifier]
            if identifier in current:
                if current[identifier] != resource:
                    raise ValueError("An intro source texture conflicts with an existing resource")
            else:
                manifest["resources"].append(copy.deepcopy(resource))
    else:
        children, tracks, used, receipt = adapted_overlay(layout, prefix, baked_resource, duration_ms)
    overlay = _node(prefix + ".overlay", properties={"opacity": 0}, children=children)
    animated_baked = copy.deepcopy(root)
    animated_baked.pop("slot")
    animated_baked["id"] = prefix + ".baked"
    animated_baked["properties"]["opacity"] = 1
    disabled_baked = copy.deepcopy(animated_baked)
    disabled_baked["id"] = prefix + ".disabled-baked"
    enabled = _node(prefix + ".enabled", properties={"visible": False}, children=[overlay, animated_baked])
    disabled = _node(prefix + ".disabled", properties={"visible": True}, children=[disabled_baked])
    document["bindings"].append({"id": prefix + ".enabled.bind", "target": enabled["id"],
                                  "property": "visible", "source": "option.entrance-animation"})
    document["variants"].append({"id": prefix + ".disabled.select", "target": disabled["id"], "property": "visible",
        "source": "option.entrance-animation", "default": False,
        "cases": [{"id": prefix + ".disabled.case", "key": "false", "value": True}]})
    tracks.extend([_track(prefix + ".overlay-clock", overlay["id"], "opacity", [(0, 1), (duration_ms, 0)]),
                   _track(prefix + ".baked-clock", animated_baked["id"], "opacity", [(0, 0), (duration_ms, 1)])])
    document["tracks"].extend(tracks)
    root["type"] = "container"
    root.pop("resource")
    root["children"] = [enabled, disabled]
    budgets = _budget(document, manifest)
    texture_ids = {identifier for identifier in used}
    source_textures = [item for item in reference["textures"] if item["id"] in texture_ids] if reference else []
    return {"frame": frame, "layout": layout, **receipt, "clock": CLOCK, "durationMs": duration_ms,
            "timingAdaptation": "Original 33 source 0..180 (3000ms) compressed uniformly into the authored duration" if frame == "iidx33" else
                "Independent authored regional motion; no original timing claim",
            "handover": "At duration, restore the exact existing baked background; no change to native notes, beam, hit line or judgement",
            "inactiveClock": "Skip preparation.elapsed programs and retain complete static preview when preparation is unavailable",
            "sourceTextureIds": sorted(texture_ids), "addedDecodedTextureBytes": sum(item["width"] * item["height"] * 4 for item in source_textures),
            "frameRasters": 0, "budgets": budgets}


def upgrade_existing(document, *, layout):
    """Move v14's exact assembly samples onto preparation, with a static preview."""
    templates = [item for item in document["templates"] if item["id"].endswith(f".template.{layout}.background")]
    if len(templates) != 1 or templates[0]["root"]["type"] != "container":
        raise ValueError("The source must contain one assembled Frame background")
    root = templates[0]["root"]
    nodes = {node["id"]: node for node in _walk(root)}
    enabled_ids = [binding["target"] for binding in document["bindings"]
                   if binding.get("source") == "option.entrance-animation" and binding["target"] in nodes]
    if len(enabled_ids) != 1:
        raise ValueError("The original Frame assembly option owner changed")
    enabled = nodes[enabled_ids[0]]
    if len(enabled["children"]) != 2 or enabled["children"][1]["type"] != "sprite":
        raise ValueError("The original baked handover is incomplete")
    prefix = root["id"] + ".preparation"
    animated = _node(prefix + ".active", properties={"visible": False}, children=enabled["children"])
    static = copy.deepcopy(enabled["children"][1])
    static["id"] = prefix + ".preview"
    static["properties"].update({"visible": True, "opacity": 1})
    enabled["children"] = [animated, static]
    document["bindings"].append({"id": prefix + ".available", "target": animated["id"],
                                 "property": "visible", "source": "preparation.available"})
    document["variants"].append({"id": prefix + ".preview-visible", "target": static["id"],
        "property": "visible", "source": "preparation.available", "default": False,
        "cases": [{"id": prefix + ".preview-unavailable", "key": "false", "value": True}]})
    migrated = 0
    for track in document["tracks"]:
        if track.get("clock") == "timing.elapsed" and track["target"] in nodes:
            track["clock"] = CLOCK
            migrated += 1
    if not migrated:
        raise ValueError("The original Frame has no assembly clock to migrate")
    return {"clock": CLOCK, "migratedTracks": migrated, "originalKeyframesPreserved": True,
            "staticPreviewWhenUnavailable": True, "animationDurationMs": DURATION_MS}
