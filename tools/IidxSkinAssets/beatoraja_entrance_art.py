"""Original step-sampled entrance quads on integer native PNG carriers.

Only the actual unit-Q source is accepted. Slightly non-parallelogram corners
are retained as two triangles, never silently projected onto an affine quad.
The triangle diagonal follows the pinned production framework 2026.303.0.
Like the existing fixed-affine adapter, this is nearest sampling at integer
canvas pixel centres, not a claim of bitwise equality with GPU edge coverage.
"""

from __future__ import annotations

import math
import numpy as np
from PIL import Image


def rasterize_canvas_quad(source: Image.Image, quad: dict, rect):
    vertices = quad.get("vertices", [])
    if (len(vertices) != 4 or any(len(point) != 3
            or any(isinstance(v, bool) or not isinstance(v, (float, int)) or not math.isfinite(v) for v in point)
            or point[2] != 1 for point in vertices)):
        raise ValueError("Entrance raster requires four finite source corners with unit Q")
    opacity = quad.get("opacity", 1)
    if not isinstance(opacity, (float, int)) or not math.isfinite(opacity) or not 0 <= opacity <= 1:
        raise ValueError("Entrance quad opacity must be in [0, 1]")
    if len(rect) != 4 or not all(math.isfinite(v) for v in rect) or min(rect[2:]) <= 0:
        raise ValueError("Entrance quad requires a positive source material rectangle")
    x, y, w, h = rect
    points = np.asarray([(x + u * w, y + v * h) for u, v, _ in vertices], dtype=np.float64)
    uv = np.asarray(((0, 0), (1, 0), (1, 1), (0, 1)), dtype=np.float64)
    left, top = (math.floor(float(v)) for v in points.min(axis=0))
    right, bottom = (math.ceil(float(v)) for v in points.max(axis=0))
    evidence = {"rect": [left, top, right-left, bottom-top], "opacity": opacity,
                "triangles": [[3, 2, 0], [1, 0, 2]], "sampledPixels": 0}
    if right == left or bottom == top or opacity == 0:
        return None, evidence
    original = np.asarray(source.convert("RGBA"))
    output = np.zeros((bottom-top, right-left, 4), dtype=np.uint8)
    covered = np.zeros((bottom-top, right-left), dtype=bool)
    winding = None
    # Production appends BL,BR,TR,TL and indexes (0,1,3),(2,3,1): TL--BR
    # diagonal. See ppy/osu-framework/2026.303.0 VeldridIndexBuffer.cs.
    for indices in evidence["triangles"]:
        tri, tex = points[indices].copy(), uv[indices].copy()
        ab, ac = tri[1]-tri[0], tri[2]-tri[0]
        determinant = float(ab[0]*ac[1]-ab[1]*ac[0])
        if determinant == 0:
            continue
        sign = determinant > 0
        if winding is not None and winding != sign:
            raise ValueError("Entrance source quad is folded or self-intersecting")
        winding = sign
        if determinant < 0:
            tri[[1, 2]], tex[[1, 2]] = tri[[2, 1]], tex[[2, 1]]
            ab, ac = tri[1]-tri[0], tri[2]-tri[0]
            determinant = -determinant
        columns = np.arange(left, right, dtype=np.float64)[None, :] + .5
        for row in range(0, bottom-top, 128):
            rows = np.arange(top+row, min(bottom, top+row+128), dtype=np.float64)[:, None] + .5
            inside = np.ones((rows.shape[0], columns.shape[1]), dtype=bool)
            for first, last in ((0, 1), (1, 2), (2, 0)):
                edge = tri[last]-tri[first]
                side = edge[0]*(rows-tri[first, 1])-edge[1]*(columns-tri[first, 0])
                top_left = edge[1] < 0 or edge[1] == 0 and edge[0] > 0
                inside &= (side > 0) | ((side == 0) & top_left)
            if not np.any(inside):
                continue
            dx, dy = columns-tri[0, 0], rows-tri[0, 1]
            b = (dx*ac[1]-dy*ac[0])/determinant
            c = (ab[0]*dy-ab[1]*dx)/determinant
            a = 1-b-c
            u = a*tex[0, 0]+b*tex[1, 0]+c*tex[2, 0]
            v = a*tex[0, 1]+b*tex[1, 1]+c*tex[2, 1]
            # Source textures clamp at their edges. Interior RGBA is copied,
            # including alpha; quad opacity remains a separate destination value.
            sx = np.clip(np.floor(u[inside]*source.width), 0, source.width-1).astype(np.intp)
            sy = np.clip(np.floor(v[inside]*source.height), 0, source.height-1).astype(np.intp)
            target = output[row:row+rows.shape[0]]
            target[inside] = original[sy, sx]
            covered[row:row+rows.shape[0]] |= inside
    evidence["sampledPixels"] = int(np.count_nonzero(covered))
    if not evidence["sampledPixels"]:
        return None, evidence
    return Image.fromarray(output), evidence


def entrance_quad_track(node, scene):
    tracks = [t for t in scene.get("tracks", [])
              if t["target"] == node["id"] and t["property"] == "projective-quad"]
    if not tracks or all(t.get("clock") != "preparation.elapsed" for t in tracks):
        return None
    if len(tracks) != 1:
        raise ValueError("Entrance quad has conflicting tracks")
    track = tracks[0]
    keys = track.get("keyframes", [])
    if (track.get("type") != "frame" or track.get("easing") != "step" or track.get("loop") or not keys
            or keys[0]["time"] != 0
            or any(not math.isfinite(k["time"]) or k["time"] < 0 for k in keys)
            or any(a["time"] >= b["time"] for a, b in zip(keys, keys[1:]))):
        raise ValueError("Entrance quad requires the original ordered non-looping step frames")
    return track
