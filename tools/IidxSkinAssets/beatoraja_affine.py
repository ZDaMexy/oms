"""Rasterize the source's fixed affine quad at native canvas pixel centres.

This is a qualified transformation of original RGBA pixels, including overhang;
it does not redraw key lights or crop them to the unprojected material slot.
The result is an integer-pixel carrier for filter=0. It differs from OMS's
floating-point, single-pass GPU rasterization at subpixel edges and is not an
assertion of bitwise GPU equivalence. Opacity is returned for the caller to
apply once; it is deliberately not multiplied into the original pixels.
"""

from __future__ import annotations

import math

from PIL import Image


def fixed_affine_quad(node: dict, scene: dict) -> dict:
    """Read a literal quad or its sole fixed time-zero frame/tween track.

    Unresolved bindings, variants, clocks, triggers and animation are rejected.
    The caller still owns ancestor visibility/colour, the material rectangle,
    the actual texture and any unrelated node transforms.
    """
    if any(item["target"] == node["id"] and item["property"] == "projective-quad"
           for category in ("bindings", "variants") for item in scene.get(category, [])):
        raise ValueError("Dynamic projective quad requires an explicit bake")
    tracks = [item for item in scene.get("tracks", [])
              if item["target"] == node["id"] and item["property"] == "projective-quad"]
    if len(tracks) > 1:
        raise ValueError("Conflicting projective quad tracks")
    quad = node.get("properties", {}).get("projective-quad")
    if tracks:
        track = tracks[0]
        keys = track.get("keyframes", [])
        if (track.get("type") not in ("frame", "tween") or track.get("clock")
                or track.get("trigger") or track.get("loop") or len(keys) != 1
                or keys[0]["time"] != 0):
            raise ValueError("Animated projective quad requires an explicit bake")
        quad = keys[0]["value"]
    if not isinstance(quad, dict):
        raise ValueError("Projective sprite has no declared fixed quad")
    return quad


def _finite(value):
    return isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)


def rasterize_canvas_affine(source: Image.Image, quad: dict, rect) -> tuple[Image.Image, dict]:
    """Inverse-nearest sample TL/TR/BR/BL into the full integer canvas bounds.

    ``rect`` is the resolved top-left material rectangle in canvas pixels.
    Unit Q and an affine parallelogram are required. A <=1e-12 normalized
    closure residual accommodates the source JSON's floating-point arithmetic;
    the actual residual is retained in the result. No perspective approximation
    or clipping to the original slot is performed.
    """
    vertices = quad.get("vertices", [])
    if (len(vertices) != 4 or any(len(point) != 3 or not all(_finite(v) for v in point)
                                or point[2] != 1 for point in vertices)):
        raise ValueError("Fixed affine raster requires four finite vertices with unit Q")
    residual = [vertices[0][i] + vertices[2][i] - vertices[1][i] - vertices[3][i] for i in (0, 1)]
    if any(abs(value) > 1e-12 for value in residual):
        raise ValueError("Non-affine projective quad requires an explicit bake")
    opacity = quad.get("opacity", 1)
    if not _finite(opacity) or not 0 <= opacity <= 1:
        raise ValueError("Quad opacity must be finite and in [0, 1]")
    if len(rect) != 4 or not all(_finite(value) for value in rect) or min(rect[2:]) <= 0:
        raise ValueError("Affine raster requires a finite positive material rectangle")
    x, y, width, height = rect
    world = [[x + px * width, y + py * height] for px, py, _ in vertices]
    p0, p1, _, p3 = world
    ax, ay = p1[0] - p0[0], p1[1] - p0[1]
    bx, by = p3[0] - p0[0], p3[1] - p0[1]
    determinant = ax * by - ay * bx
    if not math.isfinite(determinant) or determinant == 0:
        raise ValueError("Degenerate affine quad has no inverse")
    left, top = math.floor(min(p[0] for p in world)), math.floor(min(p[1] for p in world))
    right, bottom = math.ceil(max(p[0] for p in world)), math.ceil(max(p[1] for p in world))
    inverse = (by / determinant, -bx / determinant, -ay / determinant, ax / determinant)
    pixels = source.convert("RGBA")
    raster = Image.new("RGBA", (right - left, bottom - top))
    original, output = pixels.load(), raster.load()
    sampled = 0
    for row in range(raster.height):
        dy = top + row + .5 - p0[1]
        for column in range(raster.width):
            dx = left + column + .5 - p0[0]
            u = inverse[0] * dx + inverse[1] * dy
            v = inverse[2] * dx + inverse[3] * dy
            if 0 <= u < 1 and 0 <= v < 1:
                output[column, row] = original[math.floor(u * pixels.width), math.floor(v * pixels.height)]
                sampled += 1
    return raster, {
        "code": "fixed-affine-quad-rasterized", "rect": [left, top, raster.width, raster.height],
        "opacity": opacity, "source_size": list(pixels.size), "material_rect": list(rect),
        "vertices": vertices, "closure_residual": residual, "world_vertices": world,
        "inverse_linear": list(inverse), "inverse_origin": p0, "sampled_pixels": sampled,
        "sampling": "integer canvas pixel centres; inverse affine; floor UV source pixel; filter=0",
        "detail": "Original RGBA pixels and full quad overhang retained. Native integer carrier may differ from OMS floating-point GPU rasterization at subpixel edges; opacity is not baked into pixels.",
    }
