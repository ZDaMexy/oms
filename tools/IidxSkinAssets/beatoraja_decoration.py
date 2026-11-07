"""Native turntable art and optional BGA borders from the selected author.

Turntable movement deliberately follows beatoraja's scratch-angle offsets. Its
automatic rotation and key acceleration are not the OMS motion/timing clocks.
BGA output contains only the author's border; the caller owns the movie and
must use the selected module's BgaViewports from its INI.
"""

from __future__ import annotations

import math

from PIL import Image

from beatoraja_author import material_bindings, material_templates, source_path
from beatoraja_native import anchor, colour, native_colour, native_rect, node_rect, scene_properties


_MOTION_CLOCKS = {"timing.time", "input.motion-counts-1", "input.motion-counts-2",
                  "input.motion-revolutions-1", "input.motion-revolutions-2"}


def _quad_mapping(quad, rect):
    if not quad or len(quad.get("vertices", [])) != 4:
        raise ValueError("Projective sprite needs its four declared fixed vertices")
    vertices = quad["vertices"]
    if any(len(p) != 3 or not all(math.isfinite(v) for v in p) or p[2] != 1 for p in vertices):
        raise ValueError("Projective weights require a bake")
    x0, y0, _ = vertices[0]
    x1, y1, _ = vertices[2]
    if (vertices[1] != [x1, y0, 1] or vertices[3] != [x0, y1, 1] or x0 == x1 or y0 == y1):
        raise ValueError("Non-axis-aligned projective quad requires a bake")
    x, y, w, h = rect
    return {"rect": (x + min(x0, x1) * w, y + min(y0, y1) * h, abs(x1 - x0) * w, abs(y1 - y0) * h),
            "flip_x": x1 < x0, "flip_y": y1 < y0, "opacity": quad.get("opacity", 1)}


def fixed_projective_quad(node, scene, rect):
    """Resolve one fixed axial quad; retain overhang and return lossless flips.

    ``rect`` is the node's resolved top-left rectangle before projection. The
    result has rect/flip_x/flip_y/opacity; unsupported perspective or animation
    raises ValueError so the caller can record a gap instead of guessing.
    """
    quad = node.get("properties", {}).get("projective-quad")
    tracks = [t for t in scene.get("tracks", []) if t["target"] == node["id"] and t["property"] == "projective-quad"]
    if len(tracks) > 1:
        raise ValueError("Conflicting projective quad tracks")
    if tracks:
        known, quad = _sample_static(tracks[0], {})
        if not known:
            raise ValueError("Animated projective quad requires a bake")
    return _quad_mapping(quad, rect)


def add_oriented_source(sink, relative, *, flip_x=False, flip_y=False):
    """Copy the exact original and, only when needed, flip its pixels losslessly."""
    source_id = sink.add_source(relative)
    if not flip_x and not flip_y:
        return source_id
    with Image.open(source_path(sink.author_root, relative)) as source:
        raster = source.convert("RGBA")
    if flip_x:
        raster = raster.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
    if flip_y:
        raster = raster.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
    return sink.add_raster(raster)


def _affine_quad_mapping(quad):
    """Qualify the fixed centred parallelogram used by the older turntable.

    Equal unit reciprocal-W makes the OMS two-triangle UV interpolation affine.
    Retaining its unit bounding box also retains the node's rotation registration;
    other perspective/overhanging quads need a separately qualified bake.
    """
    vertices = quad.get("vertices", []) if quad else []
    if (len(vertices) != 4 or any(len(p) != 3 or not all(math.isfinite(v) for v in p) or p[2] != 1 for p in vertices)):
        raise ValueError("Affine raster needs four fixed vertices with unit reciprocal-W")
    p0, p1, p2, p3 = vertices
    ax, ay = p1[0] - p0[0], p1[1] - p0[1]
    bx, by = p3[0] - p0[0], p3[1] - p0[1]
    if any(not math.isclose(p0[i] + p2[i], p1[i] + p3[i], rel_tol=0, abs_tol=1e-12) for i in (0, 1)):
        raise ValueError("Non-affine projective quad requires a recorded bake")
    if any(min(p[i] for p in vertices) != 0 or max(p[i] for p in vertices) != 1 for i in (0, 1)):
        raise ValueError("Affine raster requires the original centred unit bounding box")
    determinant = ax * by - ay * bx
    if determinant == 0:
        raise ValueError("Degenerate projective quad has no affine inverse")
    inverse = (by / determinant, -bx / determinant, (bx * p0[1] - by * p0[0]) / determinant,
               -ay / determinant, ax / determinant, (ay * p0[0] - ax * p0[1]) / determinant)
    return {"inverse": inverse, "vertices": vertices, "opacity": quad.get("opacity", 1)}


def rasterize_fixed_affine_quad(source, quad):
    """Sample a qualified fixed quad at the original texture's pixel centres.

    This applies the declared inverse affine, including small nonzero deltas and
    mirrored orientation. It neither rounds the vertices into a rectangle nor
    resizes the carrier. Later native rotation resamples that fixed raster; it is
    not an assertion of single-pass OMS GPU equivalence at every rotation/scale.
    """
    mapping = _affine_quad_mapping(quad)
    source = source.convert("RGBA")
    width, height = source.size
    raster = Image.new("RGBA", source.size)
    a, b, c, d, e, f = mapping["inverse"]
    sampled = 0
    for y in range(height):
        vy = (y + .5) / height
        for x in range(width):
            vx = (x + .5) / width
            u, v = a * vx + b * vy + c, d * vx + e * vy + f
            if 0 <= u < 1 and 0 <= v < 1:
                raster.putpixel((x, y), source.getpixel((math.floor(u * width), math.floor(v * height))))
                sampled += 1
    return raster, {"vertices": mapping["vertices"], "inverse_affine": list(mapping["inverse"]),
                    "raster_size": [width, height], "sampled_pixels": sampled,
                    "nontransparent_pixels": sum(raster.getpixel((x, y))[3] > 0 for y in range(height) for x in range(width))}


def _intersection(a, b):
    if a is None:
        return b
    left, top = max(a[0], b[0]), max(a[1], b[1])
    right, bottom = min(a[0] + a[2], b[0] + b[2]), min(a[1] + a[3], b[1] + b[3])
    return left, top, max(0, right - left), max(0, bottom - top)


def _sample_static(track, context):
    keys = track["keyframes"]
    if not keys or track.get("loop") or track.get("trigger"):
        return False, None
    clock = track.get("clock")
    if clock is None and len(keys) == 1 and keys[0]["time"] == 0:
        return True, keys[0]["value"]
    if clock not in context or not isinstance(context[clock], (bool, int, float)):
        return False, None
    now = context[clock]
    if now <= keys[0]["time"]:
        return True, keys[0]["value"]
    for left, right in zip(keys, keys[1:]):
        if now < right["time"]:
            if track.get("easing") == "step":
                return True, left["value"]
            if track.get("easing") != "linear" or not all(isinstance(k["value"], (int, float)) for k in (left, right)):
                return False, None
            ratio = (now - left["time"]) / (right["time"] - left["time"])
            return True, left["value"] + (right["value"] - left["value"]) * ratio
    return True, keys[-1]["value"]


def _export(author, options, keymode, presentation, sink, canvas, module):
    x, y, width, height = canvas
    if x != 0 or y != 0 or min(width, height) <= 0:
        raise ValueError("Decoration export requires the full positive zero-origin canvas")
    if (keymode, presentation) not in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
        raise ValueError("Decoration export requires a matching SP or DP presentation")
    result = {"image": [], "destination": [], "gaps": [], "handled_root_ids": []}
    if module not in author.modules:
        return result
    contribution = author.contribution(module, options)
    frame = author.contribution("frame", options)
    markers = {binding["resource"] for binding in material_bindings(frame, keymode, presentation)
               if binding["target"]["kind"] == "global" and binding["slot"] == "decoration" and binding["mode"] == "Provide"}
    roots = {root["id"]: root for marker in sorted(markers)
             for root in material_templates(contribution, "decoration", marker)}
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    tracks = {}
    for track in contribution.scene.get("tracks", []):
        tracks.setdefault(track["target"], []).append(track)

    def gap(node, code, **details):
        result["gaps"].append({"feature": module, "node": node["id"], "code": code, **details})

    def walk(node, parent, inherited_colour=(1, 1, 1, 1), blend="alpha", clip=None, motions=()):
        properties, dynamic = scene_properties(node, contribution.scene, context)
        if properties.get("visible", True) is False:
            return
        if dynamic:
            gap(node, "unmapped-decoration-state", sources=sorted({item["source"] for item in dynamic}))
            return
        current_motions = []
        for track in tracks.get(node["id"], []):
            if module == "turntable" and track["property"] == "rotation" and track.get("clock") in _MOTION_CLOCKS:
                keys = track["keyframes"]
                if (track.get("type") != "tween" or track.get("easing") != "linear" or not track.get("loop")
                        or len(keys) != 2 or keys[0]["time"] != 0 or keys[0]["value"] != 0
                        or keys[1]["time"] <= 0 or abs(keys[1]["value"]) != 360):
                    gap(node, "unqualified-turntable-motion-track", track=track["id"])
                    return
                current_motions.append(track["clock"])
                continue
            known, value = _sample_static(track, context)
            if not known:
                gap(node, "unmapped-decoration-track", track=track["id"], clock=track.get("clock"))
                return
            properties[track["property"]] = value
        if properties.get("visible", True) is False:
            return
        if node.get("effects") or properties.get("rotation", 0) % 360:
            gap(node, "decoration-transform-needs-bake")
            return
        rect = node_rect(properties, parent)
        if min(rect[2:]) <= 0:
            gap(node, "non-positive-decoration-rectangle")
            return
        rgba = colour(properties, inherited_colour)
        if rgba[3] <= 0:
            return
        own_blend = node.get("blend", "inherit")
        blend = blend if own_blend == "inherit" else own_blend
        if blend not in ("alpha", "additive"):
            gap(node, "unmapped-decoration-blend", blend=blend)
            return
        ox, oy = anchor(properties.get("origin", "top-left"))
        motions = (*motions, *((clock, (rect[0] + ox * rect[2], rect[1] + oy * rect[3])) for clock in current_motions))
        kind = node["type"]
        if kind == "clip":
            if properties.get("clip-mode", "bounds") != "bounds":
                gap(node, "unmapped-decoration-clip")
                return
            clip = _intersection(clip, rect)
        if clip is not None and min(clip[2:]) <= 0:
            return
        if kind in ("container", "clip"):
            for child in sorted(node.get("children", []), key=lambda item: -item.get("properties", {}).get("z", 0)):
                walk(child, rect, rgba, blend, clip, motions)
            return
        if kind not in ("sprite", "projective-sprite"):
            gap(node, "decoration-primitive-needs-bake", kind=kind)
            return
        flip_x = flip_y = False
        affine_quad = None
        if kind == "projective-sprite":
            try:
                mapping = _quad_mapping(properties.get("projective-quad"), rect)
            except ValueError as error:
                if module != "turntable":
                    gap(node, "non-axis-aligned-decoration-quad-needs-bake", detail=str(error))
                    return
                affine_quad = properties.get("projective-quad")
                try:
                    mapping = _affine_quad_mapping(affine_quad)
                except ValueError as affine_error:
                    gap(node, "non-axis-aligned-decoration-quad-needs-bake", detail=str(affine_error))
                    return
            else:
                rect, flip_x, flip_y = mapping["rect"], mapping["flip_x"], mapping["flip_y"]
            rgba = (*rgba[:3], rgba[3] * mapping["opacity"])
        offset = None
        if motions:
            center = (rect[0] + rect[2] / 2, rect[1] + rect[3] / 2)
            if any(any(not math.isclose(a, b, abs_tol=1e-6) for a, b in zip(pivot, center)) for _, pivot in motions):
                gap(node, "turntable-rotation-pivots-need-bake")
                return
            groups = {int(clock.rsplit("-", 1)[1]) for clock, _ in motions if clock.startswith("input.motion-")}
            if len(groups) != 1 or presentation != "dp" and groups != {1}:
                gap(node, "turntable-native-stage-unresolved")
                return
            offset = groups.pop()
            gap(node, "native-scratch-motion", native_offset=offset,
                source_clocks=sorted({clock for clock, _ in motions}),
                detail="beatoraja scratch angle includes host automatic rotation and key acceleration; it is not the OMS timing/motion-counts/motion-revolutions sum.")
        resource = contribution.resources[properties["resource"]]
        if resource["type"] != "texture":
            raise ValueError("Decoration sprite requires its declared texture")
        if affine_quad is None:
            source_id = add_oriented_source(sink, resource["path"], flip_x=flip_x, flip_y=flip_y)
        else:
            sink.add_source(resource["path"])  # Retain exact-source provenance as for lossless flips.
            with Image.open(source_path(sink.author_root, resource["path"])) as source:
                raster, evidence = rasterize_fixed_affine_quad(source, affine_quad)
            source_id = sink.add_raster(raster)
            gap(node, "fixed-decoration-quad-rasterized", **evidence,
                detail="Declared fixed affine sampled at original texture pixel centres, retaining centre registration. Later native rotation uses this raster; subpixel appearance at other scales/angles is not single-pass OMS GPU equivalence.")
        source_width, source_height = sink.dimensions[source_id]
        identifier = f"{author.family}.{module}.{presentation}.{node['id']}"
        result["image"].append({"id": identifier, "src": source_id, "x": 0, "y": 0,
                                "w": source_width, "h": source_height})
        destination_rect = native_rect(rect, height)
        error = max(abs(a - b) for a, b in zip((rect[0], height - rect[1] - rect[3], *rect[2:]), destination_rect.values()))
        if error > 1e-6:
            gap(node, "native-decoration-integer-geometry", max_error_pixels=error)
        destination = {"id": identifier, "filter": 0, "blend": 2 if blend == "additive" else 1,
                       "center": 0, "dst": [{**destination_rect, **native_colour(rgba)}]}
        if offset is not None:
            destination["offsets"] = [offset]
        if clip is not None:
            destination["dst"][0].update({"clip_" + key: value for key, value in native_rect(clip, height).items()})
        result["destination"].append(destination)

    for root in sorted(roots.values(), key=lambda item: -item.get("properties", {}).get("z", 0)):
        result["handled_root_ids"].append(root["id"])
        walk(root, canvas)
    return result


def export_turntable(author, options, keymode, presentation, sink, canvas):
    """Emit original art with one real native scratch offset per source stage.

    SP2 uses offset 1, because it remains player one's presentation. DP reads
    the source's explicit motion group and uses offsets 1 and 2. Fixed axial
    projective orientation is a lossless pixel flip. Fixed centred affine quads
    are rasterized at original texture size; unsupported perspective remains a gap.
    """
    return _export(author, options, keymode, presentation, sink, canvas, "turntable")


def export_bga_frame(author, options, keymode, presentation, sink, canvas):
    """Emit the selected optional border, preserving native clip rectangles.

    Standard's off and DP choices provide no border templates. Authors without
    this module keep their frame-owned artwork in the caller's frame pass; no
    independent replacement or movie destination is synthesized here.
    """
    return _export(author, options, keymode, presentation, sink, canvas, "bga-frame")
