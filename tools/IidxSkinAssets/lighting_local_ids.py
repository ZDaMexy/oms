"""Shorten document-local CN/Judge identities without rewriting external names."""

from __future__ import annotations

import copy

MANIFEST = "oms-gameplay-skin-manifest.v1"
SCENE = "oms-gameplay-skin-scene.v1"


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def _walk(node):
    yield node
    for child in node.get("children", []):
        yield from _walk(child)


def _nodes(document):
    return [node for root in [document["root"], *(template["root"] for template in document.get("templates", []))] for node in _walk(root)]


def _local_declarations(document):
    nodes = _nodes(document)
    declarations = [*nodes, *(effect for node in nodes for effect in node["effects"]), *document["templates"], *document["instances"]]
    for field in ("tracks", "bindings", "variants", "stateMachines"):
        declarations.extend(document[field])
    for track in document["tracks"]:
        declarations.extend(track["keyframes"])
    for variant in document["variants"]:
        declarations.extend(variant["cases"])
    for machine in document["stateMachines"]:
        declarations.extend(machine["states"])
        declarations.extend(machine["transitions"])
        for state in machine["states"]:
            declarations.extend(state["set"])
    return declarations


def compact_local_ids(document, manifest, *, module):
    """Shorten typed document declarations/references; never rewrite arbitrary strings."""
    prefixes = {"charge-notes": "oc", "judgement": "oj"}
    _require(module in prefixes and "script" not in manifest and manifest.get("contract") == MANIFEST
             and document.get("contract") == SCENE and set(document) == {
                 "contract", "root", "templates", "instances", "tracks", "bindings", "variants", "stateMachines"},
             "Only script-free public CN/Judge documents have this local rewrite contract")
    result = copy.deepcopy(document)
    declarations = _local_declarations(result)
    ids = [item["id"] for item in declarations]
    _require(all(isinstance(identifier, str) and identifier for identifier in ids) and len(ids) == len(set(ids)),
             "A local declaration identity is missing or duplicated")

    def base36(value):
        digits, encoded = "0123456789abcdefghijklmnopqrstuvwxyz", ""
        while value:
            encoded = digits[value % 36] + encoded
            value //= 36
        return encoded or "0"

    mapping = {identifier: prefixes[module] + base36(index) for index, identifier in enumerate(ids)}
    for item in declarations:
        item["id"] = mapping[item["id"]]
    for instance in result["instances"]:
        instance["template"] = mapping[instance["template"]]
    for field in ("tracks", "bindings", "variants"):
        for item in result[field]:
            item["target"] = mapping[item["target"]]
    for machine in result["stateMachines"]:
        machine["initial"] = mapping[machine["initial"]]
        for transition in machine["transitions"]:
            transition["from"], transition["to"] = mapping[transition["from"]], mapping[transition["to"]]
        for state in machine["states"]:
            for assignment in state["set"]:
                assignment["target"] = mapping[assignment["target"]]
    document.clear()
    document.update(result)
    return {"changed": sum(old != new for old, new in mapping.items()), "mapping": mapping,
            "externalResourcesFontsAnimationsTargetsPathsAndKeysUnchanged": True}
