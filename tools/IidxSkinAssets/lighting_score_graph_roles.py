"""Separate Current, Personal Best and Target bars on the existing C2 Graph.

Role textures and geometry are explicit reviewed inputs. This helper supplies
no original-machine colour or placement rule, new target calculation, option,
font, rival score, or history. All existing primary/SUB/NO DATA readouts keep
their original eligibility and material/layout owners.
"""
from __future__ import annotations

import copy
import hashlib
import math
import re
from pathlib import Path

from lighting_panels import CANVAS, GRAPH_FONT, GRAPH_PACKAGE, _contained, _independent, _node, walk

ROLES = ("current", "personal-best", "target")
TARGETS = ("maximum", "personal-best", "aaa", "aa", "a", "percentage", "next")
PRIMARY_SOURCES = {
    "maximum": "score.maximum", "personal-best": "score.personal-best", "aaa": "score.rank-aaa",
    "aa": "score.rank-aa", "a": "score.rank-a", "percentage": "option.pacemaker-percent", "next": "score.pacemaker-next",
}
VALUE_SOURCES = set(PRIMARY_SOURCES.values())
ALIASES = tuple((f"gr_sco_gauge_{index}.png", f"gr_sco_gauge_large_{colour}.png")
                for index, colour in enumerate(("blue", "green", "red")))
NEW_IDS = ("sgr.pb", "sgr.pb.frame", "sgr.pb.fill", "sgr.pb.available", "sgr.pb.progress")
_STABLE_ID = re.compile(r"[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*\Z")


def require(condition, message):
    if not condition:
        raise ValueError(message)


def verify_graph_aliases(library: Path, textures):
    """Read and prove the three numeric/default bar aliases, without writing.

    A matching filename or catalogue hash alone is insufficient. Every source
    PNG is checked against its declared bytes, dimensions and decoded RGBA,
    and each alias pair must contain exactly the same original PNG bytes.
    This proves resource equivalence only; it assigns no gameplay colour.
    """
    from PIL import Image

    library = _independent(library)
    names = {name for pair in ALIASES for name in pair}
    selected = [row for row in textures if row["package"] == GRAPH_PACKAGE and row["name"] in names]
    by_name = {row["name"]: row for row in selected}
    require(len(selected) == len(by_name) == len(names), "Use every unique original numeric/large Graph bar alias")
    measured = {}
    for name, row in by_name.items():
        path = _contained(library, row["sprite"])
        data = path.read_bytes()
        require(hashlib.sha256(data).hexdigest() == row["sha256"], "An original Graph PNG differs from its library hash")
        with Image.open(path) as image:
            rgba = image.convert("RGBA")
            size = rgba.size
            rgba_sha256 = hashlib.sha256(rgba.tobytes()).hexdigest()
        require(size == (row["width"], row["height"]) == (90, 690)
                and rgba_sha256 == row["rgbaSha256"], "An original Graph bar differs from its declared pixels")
        measured[name] = (data, rgba_sha256)
    pairs = []
    for alias, canonical in ALIASES:
        require(measured[alias][0] == measured[canonical][0], "The numeric/large Graph aliases are not byte-identical")
        pairs.append({"alias": alias, "canonical": canonical,
                      "sha256": by_name[alias]["sha256"], "rgbaSha256": measured[alias][1],
                      "originalPngBytesEqual": True})
    return {"package": GRAPH_PACKAGE, "aliases": pairs,
            "scope": "Exact source image aliases; no host colour/role/default inference", "filesWritten": 0}


def _nodes(document):
    return [node for root in (document["root"], *(item["root"] for item in document["templates"])) for node in walk(root)]


def _readouts(document):
    """Pair every terminal fill with its original Graph-font numeric readout.

    The current C2 author already shares target outlines in the base scene;
    they are not children of terminal readouts. Ordinary numeric labels may
    use the percentage source too, but are not the Graph-font score value.
    A genuine score readout missing its fill, or a fill missing that value,
    remains a broken source rather than an ignored label.
    """
    nodes = _nodes(document)
    parents = {child["id"]: node for node in nodes for child in node.get("children", [])}
    by_id = {node["id"]: node for node in nodes}
    result = []
    for binding in document["bindings"]:
        if binding["property"] != "text" or binding["source"] not in VALUE_SOURCES:
            continue
        value = by_id[binding["target"]]
        if value["properties"].get("bitmap-font") != GRAPH_FONT:
            continue
        require(value["type"] == "text" and value["properties"].get("format") == "integer",
                "The original Graph score value must retain its numeric font and integer format")
        owner = parents[value["id"]]
        sprites = [child for child in owner["children"] if child["type"] == "sprite"]
        fills = [child for child in sprites if child["properties"].get("origin") == "bottom-left"
                 and "scale-y" in child["properties"]]
        require(len(fills) == len(sprites) == 1, "Each existing target readout needs its one original terminal fill")
        require(not any(old["owner"] is owner for old in result), "An existing target readout has competing values")
        result.append({"owner": owner, "source": binding["source"], "fill": fills[0]})
    require(result, "The selected C2 target has no actual target readout")
    all_fills = {node["id"] for node in nodes if node["type"] == "sprite"
                 and node["properties"].get("origin") == "bottom-left" and "scale-y" in node["properties"]}
    require(all_fills == {entry["fill"]["id"] for entry in result}, "An existing target fill has no original Graph score value")
    return result


def _matches_outline(frame, fill):
    """Recognise the reviewed v31 outline geometry, allowing its float writes."""
    if frame["type"] != "sprite" or frame["properties"].get("origin", "top-left") != "top-left":
        return False
    f, b = frame["properties"], fill["properties"]
    expected = {"x": b["x"] * CANVAS[0] - 2, "y": (b["y"] - b["height"]) * CANVAS[1] - 1,
                "width": b["width"] * CANVAS[0] + 4, "height": b["height"] * CANVAS[1] + 2}
    return all(key in f and math.isclose(f[key] * CANVAS[index % 2], expected[key], abs_tol=.001)
               for index, key in enumerate(("x", "y", "width", "height")))


def _review_inputs(role_resources, geometry):
    require(set(role_resources) == set(geometry) == set(ROLES), "Supply all three reviewed Graph roles")
    for role in ROLES:
        require(set(role_resources[role]) == set(geometry[role]) == {"fill", "frame"},
                "Each reviewed Graph role needs explicit fill/frame resources and bounds")
        for kind in ("fill", "frame"):
            resource = role_resources[role][kind]
            require(isinstance(resource, str) and _STABLE_ID.fullmatch(resource), "Use a declared stable Graph resource ID")
            bounds = geometry[role][kind]
            require(isinstance(bounds, (list, tuple)) and len(bounds) == 4
                    and all(isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
                            for value in bounds)
                    and bounds[2] > 0 and bounds[3] > 0, "Use finite reviewed Graph bounds with positive size")


def _place(node, role, kind, role_resources, geometry):
    node["resource"] = role_resources[role][kind]
    node["properties"].update({key: value / CANVAS[index % 2]
                               for index, (key, value) in enumerate(zip(("x", "y", "width", "height"), geometry[role][kind]))})


def inject_roles(base_document, contributions, *, role_resources, geometry):
    """Apply explicit fixed role anchors to the current seven-choice C2 Graph.

    Geometry uses the 1920x1080 P1/right-Graph canvas: frame y is its top and
    fill y its bottom. Existing Frame instances and P2 translation/DP rules
    remain the owners of that geometry. This function does not choose between
    large/small bars or invent adaptive two-/three-column placement.

    The caller declares the reviewed texture resources in the final merged
    manifest. Only bar nodes change. Personal Best is now one independent
    bar, including while a selected target shows NO DATA. Existing PB target
    text remains; its duplicate old fill is retired, not hidden as a dead
    tree. Target outlines inherit the existing eligible non-PB readout gates,
    so PB only has Current/PB, and NO DATA creates no apparent Target bar.
    A PB target never renders a second copy of the PB fill.
    """
    _review_inputs(role_resources, geometry)
    require(set(contributions) == set(TARGETS), "Use all seven existing C2 target contributions")
    base, updated = copy.deepcopy(base_document), copy.deepcopy(contributions)
    all_documents = [base, *(item["scene"] for item in updated.values())]
    require(not any(identifier in NEW_IDS for document in all_documents
                    for identifier in _scene_ids(document)), "Graph roles have already been injected or conflict")
    require(not set(NEW_IDS).intersection(resource for pair in role_resources.values() for resource in pair.values()),
            "Graph role resources conflict with the new scene IDs")
    readouts = {choice: _readouts(item["scene"]) for choice, item in updated.items()}
    require(all(any(entry["source"] == PRIMARY_SOURCES[choice] for entry in entries) for choice, entries in readouts.items()),
            "Each selected C2 target must retain its original primary readout")

    nodes = _nodes(base)
    by_id = {node["id"]: node for node in nodes}
    parents = {child["id"]: node for node in nodes for child in node.get("children", [])}
    current, = _single([item for item in base["bindings"] if item["property"] == "scale-y" and item["source"] == "score.progress"],
                       "Use the one real current-score progress bar")
    fill = by_id[current["target"]]
    owner = parents[fill["id"]]
    frame, = _single([child for child in owner["children"] if _matches_outline(child, fill)],
                     "Use the original current-score bar frame")
    target_frame, = _single([child for child in owner["children"] if child is not frame
                            and child["type"] == "sprite" and child.get("resource") == frame["resource"]],
                            "Use the one shared original target outline")
    require(all(_matches_outline(target_frame, entry["fill"]) for entries in readouts.values() for entry in entries),
            "Every existing terminal target fill must use the one shared original outline")
    require(not any(item["target"] == target_frame["id"] for field in ("bindings", "variants", "tracks") for item in base[field])
            and not any(item["target"] == target_frame["id"] for machine in base["stateMachines"]
                        for state in machine["states"] for item in state["set"]),
            "The old shared target outline must retain its proven static ownership")
    require(fill["type"] == "sprite" and fill["properties"].get("origin") == "bottom-left"
            and any(item["target"] == owner["id"] and item["property"] == "visible"
                    and item["source"] == "score.maximum-available" for item in base["bindings"]),
            "Keep the current-score maximum-available owner")
    ancestors, ancestor = set(), owner
    while ancestor is not None:
        ancestors.add(ancestor["id"])
        ancestor = parents.get(ancestor["id"])
    require(any(item["target"] in ancestors and item["property"] == "visible" and item["source"] == "option.graph-visible"
                for item in base["bindings"]), "Keep the original global Graph visibility gate")

    pb_frame, pb_fill = copy.deepcopy(frame), copy.deepcopy(fill)
    pb_frame["id"], pb_fill["id"] = NEW_IDS[1:3]
    pb_fill["properties"]["scale-y"] = 0
    for node, kind in ((pb_frame, "frame"), (pb_fill, "fill")):
        _place(node, "personal-best", kind, role_resources, geometry)
    pb = _node(NEW_IDS[0], properties={"visible": False}, children=[pb_frame, pb_fill])
    owner["children"].remove(target_frame)
    owner["children"].append(pb)
    base["bindings"].extend([
        {"id": NEW_IDS[3], "target": pb["id"], "property": "visible", "source": "score.personal-best-available"},
        {"id": NEW_IDS[4], "target": pb_fill["id"], "property": "scale-y", "source": "score.personal-best-progress"},
    ])
    _place(fill, "current", "fill", role_resources, geometry)
    _place(frame, "current", "frame", role_resources, geometry)

    choices = []
    for choice, entries in readouts.items():
        scene = updated[choice]["scene"]
        retired, outlines = set(), []
        for index, entry in enumerate(entries):
            if entry["source"] == "score.personal-best":
                retired.add(entry["fill"]["id"])
                entry["owner"]["children"] = [child for child in entry["owner"]["children"] if child["id"] not in retired]
            else:
                _place(entry["fill"], "target", "fill", role_resources, geometry)
                outline = copy.deepcopy(target_frame)
                outline["id"] = "sgr.t" + str(index)
                require(outline["id"] not in set(_scene_ids(scene)), "A target outline ID conflicts with the original C2 scene")
                _place(outline, "target", "frame", role_resources, geometry)
                entry["owner"]["children"].insert(entry["owner"]["children"].index(entry["fill"]), outline)
                outlines.append(outline["id"])
        require(not any(item["target"] in retired for field in ("variants", "tracks") for item in scene[field])
                and not any(item["target"] in retired for machine in scene["stateMachines"]
                            for state in machine["states"] for item in state["set"]),
                "Review a stateful PB target bar before retiring it")
        old_bindings = [item for item in scene["bindings"] if item["target"] in retired]
        require(all(item["property"] == "scale-y" and item["source"] == "score.personal-best-progress" for item in old_bindings),
                "A retired PB target bar has an unexpected binding")
        scene["bindings"] = [item for item in scene["bindings"] if item["target"] not in retired]
        choices.append({"choice": choice, "targetBarReadouts": len(entries) - len(retired),
                        "retiredDuplicatePbBarNodes": sorted(retired), "targetOutlineIds": outlines})
    return base, updated, {
        "currentFill": fill["id"], "currentFrame": frame["id"], "retiredSharedTargetFrame": target_frame["id"], "maximumOwner": owner["id"],
        "independentPersonalBest": pb["id"], "newSceneIds": list(NEW_IDS), "choices": choices,
        "roleResources": copy.deepcopy(role_resources), "geometry": copy.deepcopy(geometry),
        "geometryPolicy": "Reviewed caller-supplied fixed role anchors; existing P2/DP material/layout owners retained",
        "readoutPolicy": "Existing primary priority, SUB fallback and NO DATA text/eligibility unchanged",
        "personalBestPolicy": "One genuine matched PB bar independent of target eligibility; no PB value is fabricated",
        "optionsAdded": 0, "fontsAdded": 0,
    }


def _single(items, message):
    require(len(items) == 1, message)
    return items


def _scene_ids(document):
    def identifiers(value):
        if isinstance(value, dict):
            if "id" in value:
                yield value["id"]
            for child in value.values():
                yield from identifiers(child)
        elif isinstance(value, list):
            for child in value:
                yield from identifiers(child)
    return identifiers(document)
