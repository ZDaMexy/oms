"""Evaluate the rectangular script mask used by the earlier Standard FC movie.

This is an offline adapter for an inspected source shape, not a runtime AVM or
a general setMask implementation. Unqualified shapes fail instead of vanishing.
"""
import math
from types import MethodType

from build_fullcombo_catalog import matrix_values, point_values

IDENTITY = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]


def instance_at_depth(clip, depth):
    if type(depth) is not int:
        raise ValueError("Script mask depth must be an integer")
    found = [child for child in clip.placed_objects if child.depth == depth + 0x4000]
    if len(found) != 1:
        raise ValueError("Script mask lookup must identify exactly one direct child")
    return found[0]


def set_mask(clip, mask):
    if not any(child is mask for child in clip.placed_objects):
        raise ValueError("Script mask must be a direct child of its target")
    clip.export_script_mask = mask


def rectangle_bounds(clip, tools, geometry, mapping):
    mask = clip.export_script_mask
    if not any(child is mask for child in clip.placed_objects) or not isinstance(mask, tools.PlacedClip):
        raise ValueError("Script mask no longer belongs to its target")
    if mask.visible or len(mask.placed_objects) != 1:
        raise ValueError("Script mask must contain one hidden rectangle")
    shape = mask.placed_objects[0]
    if not isinstance(shape, tools.PlacedShape) or not shape.visible:
        raise ValueError("Script mask child must be a visible solid shape")
    for obj in (mask, shape):
        if matrix_values(obj.transform) != IDENTITY or point_values(obj.rotation_origin) != [0, 0, 0]:
            raise ValueError("Script mask has an unqualified transform")
        if obj.mask is not None or getattr(obj, "export_filters", []):
            raise ValueError("Script mask has nested masks or filters")
    if any(getattr(mask, key, None) is not None for key in ("_x", "_y", "_rotation", "_xscale", "_yscale")):
        raise ValueError("Script mask has unqualified script transforms")
    source = shape.source.reference.split(",", 1)[0]
    row = geometry[mapping[source]]
    points = row["vertices"]
    if (len(points) != 4 or row["colours"] or len(row["draws"]) != 1
            or points[0] != [0, 0, 0] or points[1][0] <= 0 or points[2][1] <= 0
            or points != [[0, 0, 0], [points[1][0], 0, 0], [0, points[2][1], 0], [points[1][0], points[2][1], 0]]):
        raise ValueError("Script mask geometry is not an origin-aligned rectangle")
    draw = row["draws"][0]
    if (draw["flags"] != 9 or draw["texture"] is not None or draw["uv"] or draw["indices"]
            or draw["sourceColour"] is None or draw["sourceColour"][3] != 1):
        raise ValueError("Script mask must use the inspected opaque solid primitive")
    width, height = mask._width, mask._height
    if any(type(value) not in (int, float) or not math.isfinite(value) or not 0 < value <= 8192
           for value in (width, height)):
        raise ValueError("Script mask dimensions are invalid")
    return [0, 0, width, height]


def masked_renderer(base, tools, geometry, mapping, receipt):
    """Bind source methods and resolve their resized rectangle after each frame."""
    class MaskedRenderer(base):
        def _AFPRenderer__place(self, tag, clip, prefix=""):
            result = super()._AFPRenderer__place(tag, clip, prefix)
            for obj in (clip, *clip.placed_objects):
                if isinstance(obj, tools.PlacedClip):
                    obj.getInstanceAtDepth = MethodType(instance_at_depth, obj)
                    obj.setMask = MethodType(set_mask, obj)
            return result

        def render_path(self, *args, **kwargs):
            def refresh(obj):
                if not isinstance(obj, tools.PlacedClip):
                    return
                if hasattr(obj, "export_script_mask"):
                    bounds = rectangle_bounds(obj, tools, geometry, mapping)
                    obj.mask = tools.Mask(tools.Rectangle(left=bounds[0], top=bounds[1], right=bounds[2], bottom=bounds[3]))
                    if bounds not in receipt["rectangles"]:
                        receipt["rectangles"].append(bounds)
                    receipt["evaluatedMaskFrames"] += 1
                for child in obj.placed_objects:
                    refresh(child)
            for image in super().render_path(*args, **kwargs):
                refresh(self._AFPRenderer__root)
                yield image
    return MaskedRenderer
