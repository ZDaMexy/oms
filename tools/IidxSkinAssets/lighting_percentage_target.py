"""A displayed percentage target on the existing, truthful OMS score graph.

The bar is a percentage of the normalized maximum. Its readout is deliberately
not an integer EX-score calculation, a ghost, or an original rotate algorithm.
"""
from __future__ import annotations

import copy

from lighting_panels import GRAPH_FONT, walk
from lighting_start_options import _declarations

OPTION_ID = "pacemaker-percent"
TARGET_VALUE = "percentage"
OLD_TARGETS = ("maximum", "personal-best", "aaa", "aa", "a")
OFFICIAL_URL = "https://p.eagate.573.jp/game/2dx/33/howto/play/score_graph.html"
NEW_IDS = tuple([f"pp.n{i}" for i in range(7)]
                + ["pp.b0", "pp.b1", "pp.v0", "pp.c0", "pp.t0", "pp.k0", "pp.k1"])


def require(condition, message):
    if not condition:
        raise ValueError(message)


def add_options(manifest):
    """Append one mode and one setting, retaining unrelated later author options."""
    result = copy.deepcopy(manifest)
    options = result["options"]
    identifiers = [option["id"] for option in options]
    require(len(set(identifiers)) == len(identifiers) and OPTION_ID not in identifiers,
            "A percentage option conflicts with an existing declaration")
    targets = [option for option in options if option["id"] == "graph-target"]
    gates = [option for option in options if option["id"] == "graph-visible"]
    require(len(targets) == len(gates) == 1 and targets[0]["type"] == "choice" and gates[0]["type"] == "toggle",
            "Use the existing graph choice and display gate")
    target = targets[0]
    require(tuple(choice["value"] for choice in target["choices"]) == OLD_TARGETS and target["default"] in OLD_TARGETS,
            "The source must retain the five established graph targets")
    target["choices"].append({"value": TARGET_VALUE, "label": "PACEMAKER 百分比"})
    options.append({"id": OPTION_ID, "type": "number", "label": "PACEMAKER · 理论满分百分比 (%)",
                    "default": 50, "min": 0, "max": 100, "step": 1})
    return result


def inject(document, manifest):
    """Clone the exact maximum branch beneath its existing availability gates.

    No old node, program, resource, font, material, or default is rewritten. The
    new suffix is ordinary OMS text because the original graph font has digits
    only; the cabinet's transparent percentage placeholder is not a real glyph.
    """
    result = copy.deepcopy(document)
    nodes = [node for root in (result["root"], *(item["root"] for item in result["templates"])) for node in walk(root)]
    declarations = _declarations(result, nodes)
    identifiers = [item["id"] for item in declarations]
    require(len(identifiers) == len(set(identifiers)), "The source has duplicate scene declaration IDs")
    require(not set(NEW_IDS).intersection(identifiers), "A percentage declaration ID conflicts with the source")
    by_id = {node["id"]: node for node in nodes}
    parents = {child["id"]: node for node in nodes for child in node.get("children", [])}
    selectors = [item for item in result["variants"] if item["source"] == "option.graph-target"]
    require(len(selectors) == 5, "The scene must own exactly five established graph selectors")
    by_choice = {}
    for selector in selectors:
        require(selector["property"] == "visible" and selector["default"] is False and len(selector["cases"]) == 1
                and selector["cases"][0].get("value") is True, "An existing graph selector has different semantics")
        choice = selector["cases"][0]["key"]
        require(choice not in by_choice and choice in OLD_TARGETS, "Graph selector modes conflict")
        by_choice[choice] = selector
    require(set(by_choice) == set(OLD_TARGETS), "An established graph mode is missing")
    selected = by_id[by_choice["maximum"]["target"]]
    parent = parents.get(selected["id"])
    require(parent is not None and all(parents.get(item["target"]) is parent for item in selectors),
            "Graph target branches must share their original parent")
    ancestors, ancestor = [], parent
    while ancestor is not None:
        ancestors.append(ancestor["id"])
        ancestor = parents.get(ancestor["id"])
    gates = {(binding["property"], binding["source"]) for binding in result["bindings"] if binding["target"] in ancestors}
    require({("visible", "option.graph-visible"), ("visible", "score.maximum-available")} <= gates,
            "The percentage branch must retain the real maximum-available and graph display gates")

    old_nodes = list(walk(selected))
    old_ids = {node["id"] for node in old_nodes}
    bindings = [item for item in result["bindings"] if item["target"] in old_ids]
    available = [item for item in bindings if item["property"] == "visible" and item["source"] == "score.maximum-available"]
    values = [item for item in bindings if item["property"] == "text" and item["source"] == "score.maximum"]
    require(len(old_nodes) == 6 and len(bindings) == 2 and len(available) == len(values) == 1,
            "Use the established six-node maximum graph branch")
    require(not any(item["target"] in old_ids for item in result["tracks"])
            and not any(item["target"] in old_ids for machine in result["stateMachines"]
                        for state in machine["states"] for item in state["set"])
            and [item for item in result["variants"] if item["target"] in old_ids] == [by_choice["maximum"]],
            "The original maximum branch has an unexpected competing program")
    resources = {item["id"]: item for item in manifest["resources"]}
    # Published author media use content-addressed paths. Locate the existing
    # full maximum bar by its original bottom-anchored geometry, not a retired
    # source filename. The other sprite in this exact six-node branch is its
    # frame; both keep their original declared texture resource IDs.
    sprites = [node for node in old_nodes if node["type"] == "sprite"]
    fills = [node for node in sprites if node["properties"].get("origin") == "bottom-left"
             and node["properties"].get("scale-y") == 1]
    frames = [node for node in sprites if node not in fills]
    labels = [node for node in old_nodes if node["type"] == "text" and node["properties"].get("text") == "MAXIMUM"]
    value = by_id[values[0]["target"]]
    fonts = [font for font in manifest.get("bitmapFonts", []) if font["id"] == GRAPH_FONT]
    require(len(fills) == len(frames) == len(labels) == len(fonts) == 1
            and all(resources.get(node.get("resource"), {}).get("type") == "texture" for node in sprites)
            and value["type"] == "text" and value["properties"].get("bitmap-font") == GRAPH_FONT
            and value["properties"].get("format") == "integer"
            and set("0123456789") <= {glyph["character"] for glyph in fonts[0]["glyphs"]},
            "The original graph digit font and green bar resources must be present")
    fill = fills[0]
    require(fill["properties"].get("origin") == "bottom-left" and fill["properties"].get("scale-y") == 1,
            "The original maximum bar must retain its bottom origin and full height")
    require(all(not node.get("effects") for node in old_nodes), "The maximum branch has an unsupported effect")
    require(selected["type"] == "container" and len(selected["children"]) == 1
            and selected["children"][0]["id"] == available[0]["target"]
            and {node["id"] for node in selected["children"][0]["children"]}
                == {labels[0]["id"], value["id"], frames[0]["id"], fill["id"]},
            "The maximum branch ownership changed")

    branch = copy.deepcopy(selected)
    copied = list(walk(branch))
    remap = {node["id"]: NEW_IDS[index] for index, node in enumerate(copied)}
    for node in copied:
        node["id"] = remap[node["id"]]
    clone_by_id = {node["id"]: node for node in copied}
    clone_by_id[remap[labels[0]["id"]]]["properties"]["text"] = "PACEMAKER"
    new_value = clone_by_id[remap[value["id"]]]
    original_properties = value["properties"]
    require(original_properties.get("origin") == "top-right" and original_properties.get("alignment") == "right"
            and original_properties.get("scale-x") == 0.7 and original_properties["width"] * 0.7 * 1920 > 24,
            "The existing right-aligned graph readout geometry changed")
    # Reserve a fixed 24 source pixels for the ordinary percent suffix. Only the
    # new branch moves its numeric right edge; all five old readouts stay exact.
    new_value["properties"]["x"] -= 24 / 1920
    new_value["properties"]["width"] -= 24 / (1920 * 0.7)
    new_value["properties"]["text"] = ""
    suffix = copy.deepcopy(value)
    suffix["id"] = "pp.n6"
    suffix["properties"] = {key: copy.deepcopy(original_properties[key]) for key in
                            ("x", "y", "height", "font-size", "colour", "alignment", "origin")}
    suffix["properties"].update({"text": "%", "width": 24 / 1920})
    clone_by_id[remap[available[0]["target"]]]["children"].append(suffix)
    new_fill = clone_by_id[remap[fill["id"]]]
    new_fill["properties"]["scale-y"] = 0
    parent["children"].append(branch)
    result["bindings"].extend([
        {"id": "pp.b0", "target": remap[available[0]["target"]], "property": "visible", "source": "score.maximum-available"},
        {"id": "pp.b1", "target": new_value["id"], "property": "text", "source": "option." + OPTION_ID},
    ])
    result["variants"].append({"id": "pp.v0", "target": branch["id"], "property": "visible",
                               "source": "option.graph-target", "default": False,
                               "cases": [{"id": "pp.c0", "key": TARGET_VALUE, "value": True}]})
    # A numeric clock samples a frozen option directly. It must not also carry
    # an event trigger; nonloop keeps the 100 endpoint at full height.
    result["tracks"].append({"id": "pp.t0", "type": "tween", "target": new_fill["id"], "property": "scale-y",
                            "clock": "option." + OPTION_ID, "easing": "linear", "loop": False,
                            "keyframes": [{"id": "pp.k0", "time": 0, "value": 0},
                                          {"id": "pp.k1", "time": 100, "value": 1}]})
    return result, {"sourceMaximumBranch": selected["id"], "parent": parent["id"], "newBranch": branch["id"],
                    "newNodeIds": [node["id"] for node in walk(branch)], "newDeclarationIds": list(NEW_IDS),
                    "newValueNode": new_value["id"], "newFillNode": new_fill["id"],
                    "readout": "Original graph digits, integer percent plus ordinary OMS percent suffix; no calculated EX integer",
                    "clock": "option." + OPTION_ID, "suffixReservedSourcePixels": 24,
                    "existingGates": ["option.graph-visible", "score.maximum-available"]}
