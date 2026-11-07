"""Source bar-line art on beatoraja's real measure-line carrier.

``group`` belongs inside NoteSet; it is not an ordinary destination array.
The host positions and clips each occurrence while preserving the source PNG.
"""

from __future__ import annotations

import math
import re

from PIL import Image

from beatoraja_author import bms_sections, layout_for, material_templates
from beatoraja_native import colour, native_colour, scene_properties


def _group_markers(contribution, keymode, presentation):
    """Read the base C4 group declarations, including their actual `any` scope."""
    groups = 2 if presentation == "dp" else 1
    result, target = {}, None
    for line in contribution.ini.splitlines():
        line = line.strip()
        if line.startswith("["):
            target = None
        elif line.startswith("Target:"):
            parts = line[7:].split()
            target = {"kind": parts[0].lower(), **dict(p.split("=", 1) for p in parts[1:])}
        elif line.startswith("playfield.bar-line:") and target is not None:
            if (target.get("ruleset") != "bms" or target.get("keymode") not in ("any", keymode)
                    or target.get("presentation", "any") not in ("any", presentation)
                    or target.get("stage-mode") not in ("any", "dual" if groups == 2 else "single")):
                continue
            if target["kind"] != "group":
                raise ValueError("Bar-line material requires its real group owner")
            index = int(target["group-logical"])
            if index not in range(groups):
                continue
            if target.get("group") != f"bms.group.deck-{index + 1}":
                raise ValueError("Bar-line group identity does not match its native deck")
            match = re.fullmatch(r'playfield\.bar-line: resource Provide "([^"]+)"', line)
            if not match:
                raise ValueError("Unqualified source bar-line material mode")
            if index in result and result[index] != match[1]:
                raise ValueError("Conflicting source bar-line group declarations")
            result[index] = match[1]
    return result


def export_barline(author, options, keymode, presentation, frame, sink, canvas):
    """Return ``image``, ``group`` and ``gaps`` for the caller's native NoteSet.

    The supplied frame is the already-selected real contribution. Lightning's
    BarLineHeight is an OMS profile metric at the 768-high, 96-DPI reference;
    native JsonSkin geometry uses integer source coordinates. Its nearest
    positive integer is retained through the host's own HD/FULLHD scaling.
    This does not reproduce OMS's per-object 150-ms arrival fade.
    """
    if (keymode, presentation) not in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
        raise ValueError("Bar-line export requires a matching SP or DP presentation")
    x, y, width, height = canvas
    if x != 0 or y != 0 or min(width, height) <= 0:
        raise ValueError("Bar-line export requires the full positive zero-origin canvas")
    layout = layout_for(frame, keymode, presentation)
    stages = layout["stages"]
    if len(stages) != (2 if presentation == "dp" else 1):
        raise ValueError("Bar-line stages do not match the native play mode")
    base = author.base()
    markers = _group_markers(base, keymode, presentation)
    result = {"image": [], "group": [], "gaps": []}

    if not markers:
        if author.family != "standard":
            raise ValueError("Lightning's original bar-line material is missing")
        # Standard has no authored bar-line material. Retain the existing
        # declared host addition; never borrow Lightning's texture or setting.
        identifier = "standard.native.barline"
        source = sink.add_raster(Image.new("RGBA", (1, 1), (255, 255, 255, 80)))
        result["image"].append({"id": identifier, "src": source, "x": 0, "y": 0,
                                "w": 1, "h": 1, "divx": 1, "divy": 1})
        images = {index: (identifier, (1, 1, 1, 1)) for index in range(len(stages))}
        native_height = 1
        result["gaps"].append({"code": "native BMS barline addition", "informational": True,
            "detail": "Standard declares no bar-line material. The existing native 1-pixel white/alpha-80 measure-line addition is retained; no Lightning source or brightness setting is used."})
    else:
        if set(markers) != set(range(len(stages))):
            raise ValueError("Source bar-line material does not cover every active group")
        values = {}
        for contribution in (base, frame):
            values.update(bms_sections(contribution.ini).get(keymode, {}))
        if "BarLineHeight" not in values:
            raise ValueError("Source bar-line height requires its explicit profile setting")
        logical_height = float(values["BarLineHeight"])
        if not math.isfinite(logical_height) or not .5 <= logical_height <= 32:
            raise ValueError("Bar-line height is outside the original profile metric range")
        desired_height = logical_height * height / 768
        native_height = max(1, round(desired_height))
        context = {"layout.ruleset": "bms", "layout.presentation": presentation,
                   **{"option." + key: value for key, value in options.items()}}
        images, emitted = {}, {}
        for index, marker in sorted(markers.items()):
            roots = material_templates(base, "playfield.bar-line", marker)
            if len(roots) != 1:
                raise ValueError("Bar-line material must resolve to one original template")
            node = roots[0]
            properties, dynamic = scene_properties(node, base.scene, context)
            if (node["type"] != "sprite" or node.get("children") or node.get("effects")
                    or node.get("blend", "alpha") != "alpha" or dynamic
                    or properties.get("visible", True) is not True
                    or set(properties) - {"resource", "colour", "opacity", "brightness", "visible"}
                    or any(track["target"] == node["id"] for track in base.scene.get("tracks", []))
                    or any(change["target"] == node["id"] for machine in base.scene.get("stateMachines", [])
                           for state in machine["states"] for change in state.get("set", []))):
                raise ValueError("Bar-line template is not the qualified static source sprite")
            resource = base.resources[properties["resource"]]
            if resource["type"] != "texture":
                raise ValueError("Bar-line material requires its original texture")
            path = resource["path"]
            if path not in emitted:
                source = sink.add_source(path)
                source_width, source_height = sink.dimensions[source]
                identifier = author.family + ".barline." + str(len(emitted))
                emitted[path] = identifier
                result["image"].append({"id": identifier, "src": source, "x": 0, "y": 0,
                                        "w": source_width, "h": source_height, "divx": 1, "divy": 1})
            images[index] = (emitted[path], colour(properties))
        result["gaps"].append({"code": "native-barline-adaptation", "informational": True,
            "sourceHeightLogical": logical_height, "sourceHeightPx": desired_height,
            "nativeHeightPx": native_height, "heightErrorPx": abs(native_height - desired_height),
            "detail": "Original bar texture and RGB brightness are retained with unchanged texture alpha. BarLineHeight is projected at the source 768-high/96-DPI reference and rounded to a positive native source-pixel height; the host scales it to the chosen resolution. Native measure position/visibility remains LaneRenderer-owned, without OMS's independent 150-ms fade after each bar's arrival."})

    for index, stage in enumerate(stages):
        sx, sy, sw, sh = stage["rect"]
        identifier, rgba = images[index]
        # Native measure y is the lower edge, matching source BottomLeft origin.
        # LaneRenderer supplies its real per-occurrence time/scroll translation.
        result["group"].append({"id": identifier, "dst": [{"time": 0,
            "x": round(sx * width), "y": round(height * (1 - sy - sh)),
            "w": round(sw * width), "h": native_height, **native_colour(rgba)}]})
    return result
