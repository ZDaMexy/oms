"""Read-only BMS clear semantics beside unchanged Omni gauge artwork."""
from __future__ import annotations

import copy
import hashlib
import itertools
import json
import string

from lighting_gameplay_controls import SCALE, _node, _gate, _bind, walk, require
from lighting_start_options import _declarations

PREFIX = "gx."
PROPORTIONAL = ("normal", "easy", "assist-easy")


def declarations(scene):
    nodes = [node for root in (scene["root"], *(t["root"] for t in scene["templates"])) for node in walk(root)]
    return _declarations(scene, nodes)


def _strings(value, *, omit_identity=False):
    if isinstance(value, str):
        yield value
    elif isinstance(value, dict):
        for key, child in value.items():
            if not (omit_identity and key == "id"):
                yield from _strings(child, omit_identity=omit_identity)
    elif isinstance(value, list):
        for child in value:
            yield from _strings(child, omit_identity=omit_identity)


def compact_unreferenced_identities(scene, *, forbidden_ids, external_references):
    """Only declaration identities that have no references may be shortened.

    Nodes, effects, bindings, templates and instances retain their identities.
    A selected C2 combination has exactly one Frame contribution; the caller
    supplies every non-Frame declaration from every alternative as forbidden.
    """
    result = copy.deepcopy(scene)
    records = [record for field, children in (("tracks", "keyframes"), ("variants", "cases"))
               for parent in result[field] for record in (parent, *parent[children])]
    original_ids = {item["id"] for item in declarations(scene)}
    references = set(_strings(scene, omit_identity=True)) | set(external_references)
    eligible = [record for record in records if record["id"] not in references]
    used = original_ids | set(forbidden_ids)
    def names():
        for length in itertools.count(1):
            for first in string.ascii_lowercase:
                for tail in itertools.product(string.ascii_lowercase + string.digits, repeat=length - 1):
                    yield first + "".join(tail)
    available = (name for name in names() if name not in used)
    mapping = {}
    for record in eligible:
        candidate = next(available)
        used.add(candidate)
        if len(candidate) < len(record["id"]):
            mapping[record["id"]] = candidate
            record["id"] = candidate
    restored = copy.deepcopy(result)
    reverse = {value: key for key, value in mapping.items()}
    for field, children in (("tracks", "keyframes"), ("variants", "cases")):
        for parent in restored[field]:
            for record in (parent, *parent[children]):
                record["id"] = reverse.get(record["id"], record["id"])
    require(restored == scene, "Identity compaction changed a referenced value or another field")
    resulting_ids = [item["id"] for item in declarations(result)]
    require(len(resulting_ids) == len(set(resulting_ids)), "Compaction created a local declaration collision")
    require(not set(mapping.values()).intersection(forbidden_ids), "Compaction collides with another C2 module")
    canonical = lambda value: json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    source_bytes, restored_bytes = canonical(scene), canonical(restored)
    require(source_bytes == restored_bytes, "Reversal changed canonical source bytes")
    return result, {"mapping": mapping, "referencedCandidatesRetained": sorted(record["id"] for record in records
        if record["id"] in references), "savedBytes": len(source_bytes) - len(canonical(result)),
        "sourceCanonicalSha256": hashlib.sha256(source_bytes).hexdigest(),
        "restoredCanonicalSha256": hashlib.sha256(restored_bytes).hexdigest(),
        "reversibleExact": True, "nodeResourceFontMaterialAndIniIdentitiesUnchanged": True,
        "allNonFrameAlternativesExcluded": True}


def add_semantics(scene, *, layout, host_size, line_resource):
    require(layout in ("p1", "p2", "dp"), "Use an actual selected Frame layout")
    require(all(type(x) in (int, float) and x > 0 for x in host_size), "A measured native gauge host is required")
    result = copy.deepcopy(scene)
    require(not any(item["id"].startswith(PREFIX) for item in declarations(scene)), "The Frame already has gauge semantics")
    owner, = [template["root"] for template in result["templates"] if template["root"].get("slot") == "hud.gauge"]
    dark, = [child for child in owner["children"] if child["type"] == "sprite"
             and child["properties"].get("colour") == "#404040ff"
             and all(key in child["properties"] for key in ("x", "y", "width", "height"))]
    reveal_ids = {binding["target"] for binding in result["bindings"]
                  if binding["source"] == "gauge.value" and binding["property"] == "reveal-x"}
    reveal, = [child for child in owner["children"] if child["id"] in reveal_ids]
    reversed_fill = reveal["properties"].get("rotation", 0) == 180
    require(reversed_fill == (layout == "p2"), "Use the actual existing P2 fill direction")
    prefix = PREFIX + layout
    rect = {key: dark["properties"][key] for key in ("x", "y", "width", "height")}
    bar_width, bar_height = rect["width"] * host_size[0], rect["height"] * host_size[1]
    axis = _node(prefix + ".threshold", properties={"x": 0, "width": 1 / bar_width, "height": 1,
                                                  "origin": "top-centre"})
    axis["children"] = [_node(prefix + ".line", "sprite", {"y": -2 / bar_height, "height": 1 + 4 / bar_height},
                              resource=line_resource)]
    _bind(result, axis["id"], "x", "gauge.clear-threshold")
    line_parent = axis
    if reversed_fill:
        line_parent = _node(prefix + ".reverse", properties={"x": 1, "y": 1, "rotation": 180}, children=[axis])
    proportional = _gate(result, prefix + ".proportional", "gauge.type", PROPORTIONAL, children=[line_parent])
    canvas = _node(prefix + ".fill", properties=rect, children=[proportional])
    status_y = rect["y"] + rect["height"] + 3 / host_size[1]
    status = {"x": rect["x"] + rect["width"] * .55, "y": status_y, "width": rect["width"] * .45,
              "height": 12 / host_size[1], "font-size": 11 * SCALE, "alignment": "right"}
    clear = _node(prefix + ".clear", "text", {**status, "text": "CLEAR", "colour": "#64ff00ff", "visible": False})
    unclear = _node(prefix + ".unclear", "text", {**status, "text": "NOT CLEAR", "colour": "#ffffffff", "visible": False})
    for node, key in ((clear, "clear"), (unclear, "uncleared")):
        _gate(result, node["id"], "gauge.clear-state", (key,))
    hazard = _node(prefix + ".hazard", "text", {"x": rect["x"], "y": status_y, "width": rect["width"] * .55,
        "height": 12 / host_size[1], "font-size": 11 * SCALE, "alignment": "left", "text": "HAZARD / SURVIVAL"})
    hazard["properties"]["visible"] = False
    _gate(result, hazard["id"], "gauge.type", ("hazard",))
    bms = _gate(result, prefix + ".bms", "layout.ruleset", ("bms",), children=[canvas, clear, unclear, hazard])
    owner["children"].append(bms)
    return result, {"layout": layout, "hostSizeAt1080": list(host_size), "originalFillRect": rect,
        "actualBarPixelsAt1080": [bar_width, bar_height], "thresholdNode": axis["id"], "lineNode": prefix + ".line",
        "hazardNode": hazard["id"], "threshold": "x binds true normalized gauge.clear-threshold; original P2 direction compensated on line parent only",
        "clearState": "Static CLEAR/NOT CLEAR labels select real gauge.clear-state; unavailable selects neither",
        "survival": "Hard/ExHard/Hazard have zero threshold and no percentage line; HAZARD / SURVIVAL follows live gauge.type",
        "mania": "Entire new subtree gated to layout.ruleset=bms", "fontsAdded": 0,
        "originalGaugePixelsAndProgramsUnchanged": True}
