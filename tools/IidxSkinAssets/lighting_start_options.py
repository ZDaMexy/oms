"""Attach source-reviewed presentation settings to existing private Omni nodes."""
from __future__ import annotations

import copy
import math

OPTION_ID = "lane-objects-brightness"
OPTION_LABELS = {OPTION_ID: "轨道物件亮度"}
BRIGHTNESS = {
    "notes": (OPTION_ID, OPTION_LABELS[OPTION_ID], "n", {"object.note"}),
    "charge-notes": (OPTION_ID, OPTION_LABELS[OPTION_ID], "c", {"object.long-note.head", "object.long-note.body", "object.long-note.tail"}),
    "key-beam": (OPTION_ID, OPTION_LABELS[OPTION_ID], "b", {"playfield.lane-surface"}),
    "explosion": (OPTION_ID, OPTION_LABELS[OPTION_ID], "e", {"effect.hit-explosion"}),
    "judgement": (OPTION_ID, OPTION_LABELS[OPTION_ID], "j", {"hud.judgement"}),
}
BAR_PATH = "gameplay/start-options/bar.png"
BAR_MATERIAL = BAR_PATH[:-4]
BAR_RESOURCE = "ob.bar.texture"
# These are the existing canonical public C4 group scopes, not new layout authorities.
BAR_TARGETS = (
    "Target: Group ruleset=bms keymode=any stage-mode=any group=bms.group.deck-1 group-logical=0 group-visual=0",
    "Target: Group ruleset=bms keymode=any stage-mode=dual group=bms.group.deck-2 group-logical=1 group-visual=1",
    "Target: Group ruleset=mania keymode=any stage-mode=any group=mania.group.stage-1 group-logical=0 group-visual=0",
    "Target: Group ruleset=mania keymode=any stage-mode=dual group=mania.group.stage-2 group-logical=1 group-visual=1",
)
SCENE_FIELDS = {"contract", "root", "templates", "instances", "tracks", "bindings", "variants", "stateMachines"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def brightness_definition(identifier, *, minimum, maximum, step, default):
    """Accept observed bounds, not a guessed original-game range."""
    require(identifier in OPTION_LABELS, "Unknown brightness setting")
    require(all(type(value) in (int, float) and math.isfinite(value) for value in (minimum, maximum, step, default)),
            "Brightness bounds must be finite numbers")
    require(0 <= minimum <= default <= maximum <= 1 and step > 0 and minimum < maximum,
            "Brightness must fit the public RGB multiplier range")
    for value in (maximum, default):
        intervals = (value - minimum) / step
        require(math.isfinite(intervals) and intervals <= 10_000 and abs(intervals - round(intervals)) < 1e-8,
                "Brightness bounds/default must land on the observed step within the public step budget")
    return {"id": identifier, "type": "number", "label": OPTION_LABELS[identifier],
            "default": default, "min": minimum, "max": maximum, "step": step}


def _walk(node):
    yield node
    for child in node.get("children", []):
        yield from _walk(child)


def _declarations(document, nodes):
    declarations = [*nodes, *(effect for node in nodes for effect in node.get("effects", [])),
                    *document["templates"], *document["instances"]]
    for field in ("tracks", "bindings", "variants", "stateMachines"):
        declarations.extend(document[field])
    for track in document["tracks"]:
        declarations.extend(track.get("keyframes", []))
    for variant in document["variants"]:
        declarations.extend(variant.get("cases", []))
    for machine in document["stateMachines"]:
        declarations.extend(machine["states"])
        declarations.extend(machine["transitions"])
        for state in machine["states"]:
            declarations.extend(state["set"])
    return declarations


def attach_brightness(document, *, module):
    """One multiplier per existing material root; no extra wrapper or media."""
    require(module in BRIGHTNESS, "Only the five existing material modules own these settings")
    require(document.get("contract") == "oms-gameplay-skin-scene.v1" and set(document) == SCENE_FIELDS,
            "Expected the frozen script-free public scene shape")
    require(document["root"]["type"] == "container" and not document["root"].get("children")
            and not document["root"].get("properties") and not document["root"].get("effects")
            and document["templates"] and document["instances"], "Expected template-owned material art")
    option, _, prefix, slots = BRIGHTNESS[module]
    roots = [template["root"] for template in document["templates"]]
    require(all(node.get("slot") in slots for node in roots), "Unexpected material owner in this module")
    nodes = [node for root in (document["root"], *roots) for node in _walk(root)]
    declarations = _declarations(document, nodes)
    identifiers = [item.get("id") for item in declarations]
    require(all(isinstance(identifier, str) and identifier for identifier in identifiers)
            and len(identifiers) == len(set(identifiers)), "A document-local declaration identity is missing or duplicated")
    templates = {template["id"]: template["root"]["slot"] for template in document["templates"]}
    require({instance.get("template") for instance in document["instances"]} == set(templates)
            and all(instance.get("material", {}).get("slot") == templates.get(instance.get("template"))
                    for instance in document["instances"]), "Every brightness root must retain its exact material instances")
    expected = [{"id": f"ob.{prefix}.v{index}", "target": node["id"], "property": "brightness", "source": "option." + option}
                for index, node in enumerate(roots)]
    require(all("brightness" not in node.get("properties", {}) for node in nodes), "Do not multiply authored brightness again")
    require(not any(track.get("property") == "brightness" for track in document["tracks"]),
            "Do not compete with an authored brightness track")
    require(not any(assignment.get("property") == "brightness"
                    for machine in document["stateMachines"] for state in machine["states"] for assignment in state["set"]),
            "Do not compete with an authored brightness state")
    existing = [binding for binding in document["bindings"] if binding.get("property") == "brightness"]
    if existing:
        require(existing == expected,
                "An existing brightness author declaration conflicts with this upgrade")
        return copy.deepcopy(document), {"changed": False, "option": option, "materialRoots": len(roots), "addedNodes": 0}
    declared_ids = set(identifiers)
    require(not any(binding["id"] in declared_ids for binding in expected), "Brightness binding identity conflict")
    result = copy.deepcopy(document)
    result["bindings"].extend(expected)
    return result, {"changed": True, "option": option, "materialRoots": len(roots), "addedNodes": 0}


def add_brightness_options(manifest, definitions):
    require(manifest.get("contract") == "oms-gameplay-skin-manifest.v1" and "script" not in manifest,
            "Only the script-free public author manifest can be upgraded")
    require(len(definitions) == 1 and all(isinstance(definition, dict) and set(definition) == {"id", "type", "label", "default", "min", "max", "step"}
                               for definition in definitions)
            and {definition["id"] for definition in definitions} <= OPTION_LABELS.keys(),
            "Expected reviewed brightness definitions")
    require(len({definition["id"] for definition in definitions}) == len(definitions), "Duplicate setting definition")
    for definition in definitions:
        require(definition == brightness_definition(definition["id"], minimum=definition["min"], maximum=definition["max"],
            step=definition["step"], default=definition["default"]), "Unexpected brightness definition")
    result = copy.deepcopy(manifest)
    existing = {definition["id"]: definition for definition in result.get("options", [])}
    require(len(existing) == len(result.get("options", [])), "Duplicate existing setting definition")
    for definition in definitions:
        if definition["id"] in existing:
            require(existing[definition["id"]] == definition, "An existing setting conflicts with the reviewed definition")
        else:
            result.setdefault("options", []).append(copy.deepcopy(definition))
    return result


def attach_barline_brightness(document):
    """Replace the existing canonical sprite through one ordinary public material template."""
    require(document.get("contract") == "oms-gameplay-skin-scene.v1" and set(document) == SCENE_FIELDS,
            "Expected the frozen root scene shape")
    require(document["root"]["type"] == "container" and document["root"].get("target") == {"kind": "global"}
            and not document["root"].get("children") and not document["root"].get("properties")
            and not document["root"].get("effects")
            and all(not document[field] for field in SCENE_FIELDS - {"contract", "root"}),
            "The frozen root must remain a neutral material dispatcher")
    require(document["root"]["id"] not in {"ob.bar.t", "ob.bar.n", "ob.bar.i", "ob.bar.b"},
            "Bar-line declaration identity conflict")
    result = copy.deepcopy(document)
    result["templates"].append({"id": "ob.bar.t", "root": {
        "id": "ob.bar.n", "type": "sprite", "slot": "playfield.bar-line", "target": {"kind": "global"},
        "resource": BAR_RESOURCE, "blend": "alpha", "properties": {}, "effects": [], "children": [],
    }})
    result["instances"].append({"id": "ob.bar.i", "template": "ob.bar.t",
        "material": {"slot": "playfield.bar-line", "resource": BAR_MATERIAL}})
    result["bindings"].append({"id": "ob.bar.b", "target": "ob.bar.n", "property": "brightness",
        "source": "option." + OPTION_ID})
    return result, {"materialRoots": 1, "addedNodes": 1, "replacesCanonicalSprite": True,
        "nativeFadeAndGeometry": "unchanged", "authoredOpacityOverrides": 0}


def attach_barline_materials(data):
    """Append the four canonical scopes while preserving the complete original INI prefix."""
    text = data.decode("utf-8")
    require("playfield.bar-line" not in text, "The frozen author already declares a bar-line material")
    sections = [line.strip() for line in text.splitlines() if line.strip().startswith("[")]
    require(sections and sections[-1] == "[GameplaySkin.Common:1]",
            "Expected the frozen root INI's final public common section")
    additions = "\n" + "\n".join(target + '\nplayfield.bar-line: resource Provide "' + BAR_MATERIAL + '"'
                                  for target in BAR_TARGETS) + "\n"
    return data + additions.encode("utf-8")
