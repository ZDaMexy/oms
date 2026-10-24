"""Adapt reviewed native judgement displacement to the existing author host.

The input values are option values, not elapsed milliseconds.  The native
gameplay displacement must be reviewed separately from its smaller menu preview.
No grade row, font, palette, timing, hit line or lifecycle controller is changed.
"""
from __future__ import annotations

import copy
import math

from lighting_panels import _node, _track


OPTION = "judgement-position"
PREFIX = "jp."


def require(condition, message):
    if not condition:
        raise ValueError(message)


def definition():
    return {"id": OPTION, "type": "number", "label": "判定文字高度（正数上移）",
            "min": -2, "max": 10, "step": 1, "default": 0}


def host_height(profile):
    """Use the current authored BMS/mania judgement-height contract at 1080."""
    stages = profile["stages"]
    require(stages and profile["ruleset"] in ("bms", "mania"), "Use an authored native BMS/mania profile")
    top = min(stage["rect"][1] for stage in stages)
    bottom = max(stage["rect"][1] + stage["rect"][3] for stage in stages)
    height = (bottom - top) * .12
    require(0 < height < .11, "This migration requires the reviewed inactive BMS safe-height cap")
    return height * 1080


def reviewed_points(points):
    require(isinstance(points, list) and len(points) == 4, "Supply the four reviewed real-gameplay anchors")
    values = []
    for point in points:
        require(isinstance(point, dict) and set(point) == {"value", "deltaY"}, "Invalid reviewed displacement anchor")
        value, delta = point["value"], point["deltaY"]
        require(type(value) in (int, float) and type(delta) in (int, float)
                and math.isfinite(value) and math.isfinite(delta), "Native anchors must be finite numbers")
        values.append((value, delta))
    require([value for value, _ in values] == [-2, 0, 1, 10] and values[1][1] == 0,
            "Review -2, baseline0, +1 and +10 in real gameplay")
    require(values[0][1] > 0 > values[2][1] > values[3][1], "Review the native displacement direction")
    return values


def add_position(scene, *, logical_host_height, points):
    """Move the whole existing judgement and add an independent inner OFF gate."""
    require(type(logical_host_height) in (int, float) and math.isfinite(logical_host_height)
            and logical_host_height > 0, "A verified logical host height is required")
    anchors = reviewed_points(points)
    require(all(abs(delta / logical_host_height / 2) <= 4 for _, delta in anchors),
            "The two existing parent transforms must each fit the public absolute-position budget")
    result = copy.deepcopy(scene)

    def dictionaries(value):
        if isinstance(value, dict):
            yield value
            for item in value.values():
                yield from dictionaries(item)
        elif isinstance(value, list):
            for item in value:
                yield from dictionaries(item)

    require(not any(str(item.get("id", "")).startswith(PREFIX) for item in dictionaries(scene)),
            "Do not overwrite an existing judgement-position adaptation")
    roots = [template["root"] for template in result.get("templates", [])
             if template["root"].get("slot") == "hud.judgement"]
    require(len(roots) == 3 and len({root["id"] for root in roots}) == 3,
            "The reviewed author has three existing P1/P2/DP judgement roots")
    receipts = []
    for index, root in enumerate(roots):
        require(root["type"] == "container" and root.get("children")
                and "y" not in root["properties"]
                and not any(item.get("target") == root["id"] and item.get("property") == "y"
                            for item in dictionaries(scene)), "Preserve the original row geometry and Y ownership")
        gate_id = PREFIX + "gate.g" + str(index)
        original_children = [child["id"] for child in root["children"]]
        root["children"] = [_node(gate_id, properties={"width": 1, "height": 1}, children=root["children"])]
        # Opacity remains on the existing lifecycle root.  This separate child
        # gate cannot reopen a fading/hidden native judgement or change its RGB.
        result["variants"].append({"id": PREFIX + "off.v" + str(index), "target": gate_id,
                                   "property": "visible", "source": "option.judgement-display", "default": True,
                                   "cases": [{"id": PREFIX + "off.case.c" + str(index), "key": "off", "value": False}]})
        # The public per-node relative-position bound is4. The real +10 native
        # displacement exceeds four judgement-host heights. Share it equally
        # between the existing lifecycle root and this already-required OFF
        # gate: same-sized unmasked parents add their translations without
        # another node, a larger global budget, or a changed grade-row origin.
        tracks = [_track(PREFIX + "position." + suffix + str(index), target, "y", "option." + OPTION,
                         [(value, delta / logical_host_height / 2) for value, delta in anchors])
                  for suffix, target in (("t", root["id"]), ("g", gate_id))]
        result["tracks"].extend(tracks)
        receipts.append({"root": root["id"], "gate": gate_id, "logicalHostHeight": logical_host_height,
                         "tracks": copy.deepcopy(tracks),
                         "originalChildren": original_children})

    restored = copy.deepcopy(result)
    restored["tracks"] = [track for track in restored["tracks"] if not track["id"].startswith(PREFIX)]
    restored["variants"] = [variant for variant in restored["variants"] if not variant["id"].startswith(PREFIX)]
    for template in restored["templates"]:
        root = template["root"]
        if root.get("slot") == "hud.judgement":
            root["children"] = root["children"][0]["children"]
    require(restored == scene, "Only whole-root Y and an independent inner OFF gate may change")
    return result, receipts
