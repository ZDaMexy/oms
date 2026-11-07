"""Qualified original preparation translations and binary opacity tracks.

The current Lightning routes have at most one translating ancestor, with all
clips inside that ancestor. Their source Out easing equals native acc=2.
Native JSON coordinates/time are integers: endpoint coordinates are rounded
once after adding the original parent-space delta, never inferred by comparing
two already-rounded scene rectangles. Existing baseline rounding is retained.
"""

from copy import deepcopy
import math

from beatoraja_native import Lua, lua
from beatoraja_preparation import PREPARATION_TIMER


def _keys(track, property_names, easing):
    keys = track.get("keyframes", [])
    if (track.get("type") != "tween" or track.get("clock") != "preparation.elapsed"
            or track.get("loop", False) or track.get("easing") != easing
            or track.get("property") not in property_names or len(keys) < 2):
        raise ValueError("Unqualified preparation track")
    for key in keys:
        if (any(isinstance(key.get(field), bool) or not isinstance(key.get(field), (int, float))
                or not math.isfinite(key[field]) for field in ("time", "value"))
                or key["time"] != int(key["time"])):
            raise ValueError("Preparation keyframes require finite values and whole milliseconds")
    if keys[0]["time"] != 0 or any(a["time"] >= b["time"] for a, b in zip(keys, keys[1:])):
        raise ValueError("Preparation keyframes must begin at zero and strictly increase")
    return keys


def _with_gate(existing, expression):
    if existing is None:
        return Lua("function() return " + expression + " end")
    if not isinstance(existing, Lua):
        raise ValueError("Preparation translation requires a qualified native draw callback")
    return Lua("(function() local original=(" + str(existing) + "); return function() return original() and ("
               + expression + ") end end)()")


def apply_preparation_translation(destinations, track, parent_rect, baseline_value):
    """Add one original parent translation to static native destinations.

    Returns mutually exclusive original-static and prepared branches. Existing
    draw gates, native options/offsets, colour, dimensions and blend are retained.
    Clip fields, when present, must belong inside the translating source subtree;
    fixed clips outside it are not part of the qualified source routes.
    """
    keys = _keys(track, ("x", "y"), "out")
    if len(keys) != 2:
        raise ValueError("Only the verified two-point preparation translation is supported")
    if (isinstance(baseline_value, bool) or not isinstance(baseline_value, (int, float))
            or not math.isfinite(baseline_value) or len(parent_rect) != 4
            or not all(math.isfinite(value) for value in parent_rect)):
        raise ValueError("Preparation translation needs its real finite parent rectangle and baseline")
    axis = track["property"]
    multiplier = parent_rect[2] if axis == "x" else -parent_rect[3]
    available = f"main_state.timer({PREPARATION_TIMER}) ~= main_state.timer_off_value"
    output = []
    for original in destinations:
        if (original.get("timer", 0) not in (None, 0) or original.get("loop", 0) != 0
                or len(original.get("dst", [])) != 1 or original["dst"][0].get("time", 0) != 0):
            raise ValueError("Preparation translation cannot replace an existing timer or multi-point track")
        point = original["dst"][0]
        if axis not in point or point.get("acc", 0) != 0:
            raise ValueError("Preparation translation needs a static resolved native baseline")
        baseline = deepcopy(original)
        baseline.pop("timer", None)
        baseline["draw"] = _with_gate(original.get("draw"), "not (" + available + ")")
        translated = deepcopy(original)
        translated.update(timer=PREPARATION_TIMER, loop=int(keys[-1]["time"]),
                          draw=_with_gate(original.get("draw"), available), dst=[])
        for key in keys:
            delta = (key["value"] - baseline_value) * multiplier
            frame = {**point, "time": int(key["time"]), "acc": 2, axis: round(point[axis] + delta)}
            clip_axis = "clip_" + axis
            if clip_axis in point:
                frame[clip_axis] = round(point[clip_axis] + delta)
            translated["dst"].append(frame)
        output.extend((baseline, translated))
    return output


def preparation_opacity_condition(track, baseline_value):
    """Return an expression for a source 0/1 opacity step, including OFF pose.

    The caller sets this node's carrier opacity to one and retains its other tint
    and ancestor opacity. The expression then supplies only this binary factor.
    """
    keys = _keys(track, ("opacity",), "step")
    if baseline_value not in (0, 1) or any(key["value"] not in (0, 1) for key in keys):
        raise ValueError("Preparation opacity requires original binary steps and baseline")
    branches = " ".join(f"if elapsed < {lua(following['time'])} then return {lua(bool(current['value']))} end;"
                        for current, following in zip(keys, keys[1:]))
    return (f"(function() local origin=main_state.timer({PREPARATION_TIMER}); "
            f"if origin==main_state.timer_off_value then return {lua(bool(baseline_value))} end; "
            f"local elapsed=(main_state.time()-origin)/1000; {branches} "
            f"return {lua(bool(keys[-1]['value']))} end)()")
