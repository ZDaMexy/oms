"""Factor qualified Lightning controls without changing native ownership.

This is limited to Judge/Timing Y/RGB, Explosion rectangle/RGB and Beam RGB. Each
number comes from the existing composer at the actual finite source value.
Geometry and colour have disjoint source properties and disjoint native paths;
no rounded-default translation, generic scene interpreter, or image tint is
invented at Lua load time. Unqualified sources remain ordinary enumerations.
"""

from __future__ import annotations

import copy
import hashlib
import math
from typing import Callable

from beatoraja_author import source_path
from beatoraja_native import lua


AXES = {"judgement": ("judgement-position", "lane-objects-brightness"),
        "explosion": ("explosion-scale", "lane-objects-brightness"),
        "key-beam": ("lane-objects-brightness",),
        "timing-display": ("judgement-position", "lane-objects-brightness")}


class UnqualifiedParameters(ValueError):
    """The caller must retain the existing exact enumeration for this route."""


def parameter_names(author, stage):
    return AXES.get(stage, ()) if author.family == "lightning" else ()


def _references(scene, names):
    sources = {"option." + name for name in names}
    result = set()
    def visit(value, path=()):
        if isinstance(value, dict):
            for key, child in value.items(): visit(child, (*path, key))
        elif isinstance(value, list):
            for index, child in enumerate(value): visit(child, (*path, index))
        elif isinstance(value, str) and value in sources:
            result.add(path)
    visit(scene)
    return result


def qualify_source(author, stage, options):
    """Prove independent geometry/colour inputs on the selected real source.

    Catalog branches remain selected normally. Every reference to either
    parameter must be one of the specifically supported scene operations.
    """
    names = parameter_names(author, stage)
    if not names:
        raise UnqualifiedParameters("Only Lightning Judge, Timing, Explosion and Beam are qualified")
    choice = next(c for c in author.modules[stage]["choices"] if c["value"] == options[stage])
    if any(set(v["when"]) & set(names) for v in choice.get("variants", [])):
        raise UnqualifiedParameters("A parameter selects a different catalog contribution")
    part = author.contribution(stage, options)
    expected = set()
    counts = {name: 0 for name in names}
    for kind in ("bindings", "variants", "tracks"):
        for index, operation in enumerate(part.scene.get(kind, [])):
            field = "clock" if kind == "tracks" else "source"
            source = operation.get(field, "")
            if source not in {"option." + name for name in names}:
                continue
            name = source[7:]
            prop = operation.get("property")
            valid = (name == "lane-objects-brightness" and kind == "bindings" and prop == "brightness")
            valid |= (stage == "explosion" and name == "explosion-scale"
                      and kind == "bindings" and prop in ("scale-x", "scale-y"))
            if stage in ("judgement", "timing-display") and name == "judgement-position":
                keys = operation.get("keyframes", [])
                valid = (kind == "tracks" and prop == "y" and operation.get("easing") == "linear"
                         and not operation.get("loop") and bool(keys)
                         and all(isinstance(k.get("time"), (int, float))
                                 and isinstance(k.get("value"), (int, float)) for k in keys))
            if not valid:
                raise UnqualifiedParameters("Parameter has another source operation: " + source + "/" + str(prop))
            expected.add((kind, index, field))
            counts[name] += 1

    if _references(part.scene, names) != expected:
        raise UnqualifiedParameters("Parameter is referenced outside its qualified source operations")
    return {"manifest": part.manifest_path, "scene": part.scene_path, "ini": part.ini_path,
            "operations": counts}


def qualify_cap_rgb(author):
    """Cap dimensions read PNG rectangles, not RGB. Check every source branch.

    A new brightness-dependent catalog/resource/visibility route keeps the old
    scan. This only removes duplicate height work; all six actual note raster
    derivatives and the original native menu remain produced normally.
    """
    name = "lane-objects-brightness"
    if author.family != "lightning":
        return {"qualified": False, "reason": "Not the Lightning RGB control"}
    scenes = set()
    for module in ("notes", "charge-notes"):
        for choice in author.modules[module]["choices"]:
            for branch in [choice, *choice.get("variants", [])]:
                if name in branch.get("when", {}):
                    return {"qualified": False, "reason": "RGB selects a different note contribution"}
                if branch.get("scene"): scenes.add(branch["scene"])
    hashes, operations = {}, 0
    for path in sorted(scenes):
        scene = author.json(path)
        expected = {("bindings", index, "source") for index, op in enumerate(scene.get("bindings", []))
                    if op.get("source") == "option." + name and op.get("property") == "brightness"}
        if _references(scene, (name,)) != expected:
            return {"qualified": False, "reason": "RGB affects another note source property", "scene": path}
        operations += len(expected)
        hashes[path] = hashlib.sha256(source_path(author.root, path).read_bytes()).hexdigest()
    return {"qualified": True, "control": name, "sceneFilesSha256": hashes, "brightnessBindings": operations}


def _allowed(stage, name, path):
    if stage == "judgement":
        # Judge image Y is absolute; number XY is relative to its native label.
        # Native shift, index, timer, Lift offsets and ownership never change.
        if (len(path) != 7 or path[0] != "judge" or path[2] not in ("images", "numbers")
                or path[4] != "dst" or path[5] != 0):
            return False
        if name == "judgement-position":
            return path[2] == "images" and path[6] == "y"
        return name == "lane-objects-brightness" and path[6] in ("r", "g", "b")
    if len(path) != 5 or path[0] != "destination" or path[2] != "dst" or path[3] != 0:
        return False
    if name == "lane-objects-brightness":
        return stage in ("explosion", "key-beam", "timing-display") and path[4] in ("r", "g", "b")
    if name == "judgement-position":
        return stage == "timing-display" and path[4] == "y"
    return stage == "explosion" and name == "explosion-scale" and path[4] in ("x", "y", "w", "h")


def _changes(base, other, stage, name, path=()):
    if isinstance(base, dict) and isinstance(other, dict):
        if base.keys() != other.keys():
            raise UnqualifiedParameters("Parameter changed native fields at " + repr(path))
        result = {}
        for key in base: result.update(_changes(base[key], other[key], stage, name, (*path, key)))
        return result
    if isinstance(base, list) and isinstance(other, list):
        if len(base) != len(other):
            raise UnqualifiedParameters("Parameter changed native array length at " + repr(path))
        result = {}
        for index, (first, second) in enumerate(zip(base, other)):
            result.update(_changes(first, second, stage, name, (*path, index)))
        return result
    if type(base) is type(other) and base == other:
        return {}
    numeric = all(isinstance(value, (int, float)) and not isinstance(value, bool)
                  and math.isfinite(value) for value in (base, other))
    if not numeric or not _allowed(stage, name, path):
        raise UnqualifiedParameters("Parameter changed an unqualified native value at " + repr(path))
    return {path: other}


def _read(value, path):
    for key in path: value = value[key]
    return value


def factor_fragment(stage, options, definitions, compose: Callable):
    """Compose each axis, prove unchanged structure, retain only exact numbers.

    Returns (baseline fragment, baseline gaps, axes, original compose count).
    The caller performs qualify_source first. No cross-axis output is inferred
    until both its source independence and disjoint native paths are proven.
    """
    base, gaps = compose(options)
    axes, used, calls = [], set(), 1
    for name, choices in definitions.items():
        changes = {options[name]: {}}
        for choice in choices:
            value = choice["value"]
            if value == options[name]: continue
            fragment, variant_gaps = compose({**options, name: value})
            calls += 1
            if gaps != variant_gaps:
                raise UnqualifiedParameters("Parameter changed source diagnostics")
            changes[value] = _changes(base, fragment, stage, name)
        paths = sorted(set().union(*(set(row) for row in changes.values())))
        if used.intersection(paths):
            raise UnqualifiedParameters("Two parameters affect the same native field")
        used.update(paths)
        axes.append({"option": name, "default": options[name],
                     "paths": [[key + 1 if isinstance(key, int) else key for key in path] for path in paths],
                     "values": {value: [row.get(path, _read(base, path)) for path in paths]
                                for value, row in changes.items()}})
    return base, gaps, axes, calls


def apply_parameters(fragment, axes, choices):
    """CPU reference used to compare exact native values with direct exports."""
    result = copy.deepcopy(fragment)
    for axis in axes:
        value = choices.get(axis["option"], axis["default"])
        row = axis["values"][value]
        for path, number in zip(axis["paths"], row):
            target = result
            keys = [key - 1 if isinstance(key, int) else key for key in path]
            for key in keys[:-1]: target = target[key]
            target[keys[-1]] = number
    return result


def factory_script(payload, axes):
    """Fresh table per load; every patch path was qualified before serialization."""
    return ("local parameter_axes=" + lua(axes) + "\nreturn function(choices)\n"
            " choices=choices or {}\n local fragment=" + lua(payload) + "\n"
            " for _,axis in ipairs(parameter_axes) do\n"
            "  local selected=choices[axis.option]; if selected == nil then selected=axis.default end\n"
            "  local row=assert(axis.values[selected], 'Unknown qualified parameter '..axis.option)\n"
            "  for i,path in ipairs(axis.paths) do\n"
            "   local target=fragment; for j=1,#path-1 do target=target[path[j]] end\n"
            "   target[path[#path]]=row[i]\n"
            "  end\n end\n return fragment\nend\n")
