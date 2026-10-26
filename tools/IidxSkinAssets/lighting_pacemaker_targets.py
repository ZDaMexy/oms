"""PACEMAKER NEXT on truthful OMS score data and the existing Graph artwork.

The official33 definition supplies seven targets. OMS takes the ceiling of
each exact fraction of the maximum, then selects the first integer target
strictly above the matched personal best (saturating at DJ AUTO). Native
integer rounding and equality boundaries remain unverified; NEXT+ and rotate
are deliberately absent. This module never reads an HDD or player database.
"""
from __future__ import annotations

import copy

from lighting_panels import GRAPH_FONT, _node, walk
from lighting_start_options import _declarations

OFFICIAL_URL = "https://p.eagate.573.jp/game/2dx/33/howto/play/score_graph.html"
TARGET_VALUE = "next"
OLD_TARGETS = ("maximum", "personal-best", "aaa", "aa", "a", "percentage")
LEVELS = (("LV A", 12), ("LV A+", 13), ("LV AA", 14), ("LV AA+", 15),
          ("LV AAA", 16), ("LV AAA+", 17), ("DJ AUTO", 18))
VALUE_SOURCE = "score.pacemaker-next"
PROGRESS_SOURCE = "score.pacemaker-next-progress"
LABEL_SOURCE = "score.pacemaker-next-label"
AVAILABLE_SOURCE = "score.pacemaker-next-available"
ROUNDING = "Ceiling of maximum*n/18 for n=12..18, using exact integer arithmetic"
SELECTION = "First rounded target strictly above matched personal best; DJ AUTO at maximum"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def add_options(manifest):
    """Reuse both existing choices; reserve the last unified option for FAST/SLOW."""
    result = copy.deepcopy(manifest)
    for identifier, expected in (("graph-target", OLD_TARGETS), ("graph-sub-target", ("none", *OLD_TARGETS))):
        options = [item for item in result["options"] if item["id"] == identifier]
        require(len(options) == 1 and options[0]["type"] == "choice", "Use the existing " + identifier + " choice")
        option, = options
        require(tuple(item["value"] for item in option["choices"]) == expected and option["default"] in expected,
                "Preserve every existing " + identifier + " choice and default")
        option["choices"].append({"value": TARGET_VALUE, "label": "PACEMAKER NEXT（BMS）"})
    return result


def inject(document, manifest):
    """Keep primary priority and the existing SUB/NO DATA rules for NEXT.

    The new readout copies the original MY BEST geometry and digit font. Its
    seven labels and thresholds are projected by the engine, never emulated
    by seven hidden text trees or numeric animation clocks. A missing NEXT
    retains the same selected SUB fallback behavior as the other primaries.
    """
    result = copy.deepcopy(document)
    node_list = [node for root in (result["root"], *(item["root"] for item in result["templates"])) for node in walk(root)]
    declarations = _declarations(result, node_list)
    identifiers = [item["id"] for item in declarations]
    require(len(identifiers) == len(set(identifiers)) and not any(item.startswith("pn.") for item in identifiers),
            "A PACEMAKER NEXT declaration conflicts with the source")
    nodes = {node["id"]: node for node in node_list}
    parents = {child["id"]: node for node in node_list for child in node.get("children", [])}
    selectors = [item for item in result["variants"] if item["source"] == "option.graph-target"]
    value_sources = {"maximum": "score.maximum", "personal-best": "score.personal-best", "aaa": "score.rank-aaa",
                     "aa": "score.rank-aa", "a": "score.rank-a", "percentage": "option.pacemaker-percent"}
    primary = {}
    for choice, source in value_sources.items():
        candidates = []
        for selector in selectors:
            if [item["key"] for item in selector["cases"]] != [choice]:
                continue
            branch_ids = {node["id"] for node in walk(nodes[selector["target"]])}
            if any(item["target"] in branch_ids and item["property"] == "text" and item["source"] == source
                   for item in result["bindings"]):
                candidates.append(selector)
        require(len(candidates) == 1, "Use the six original primary graph branches")
        primary[choice] = candidates[0]
    owner = parents[primary["maximum"]["target"]]
    require(all(parents[item["target"]] is owner for item in primary.values())
            and any(item["target"] == owner["id"] and item["property"] == "visible"
                    and item["source"] == "score.maximum-available" for item in result["bindings"]),
            "The real maximum gate and common target-column owner must remain")
    ancestors, ancestor = [], owner
    while ancestor is not None:
        ancestors.append(ancestor["id"])
        ancestor = parents.get(ancestor["id"])
    require(any(item["target"] in ancestors and item["property"] == "visible" and item["source"] == "option.graph-visible"
                for item in result["bindings"]), "The original Graph display gate must remain")
    missing = {}
    for key, values, availability in (("pb", ["personal-best"], "score.personal-best-available"),
                                       ("rank", ["aaa", "aa", "a"], "score.rank-available")):
        candidates = [item for item in selectors if [case["key"] for case in item["cases"]] == values
                      and item not in primary.values() and parents.get(item["target"]) is owner]
        require(len(candidates) == 1, "The original PB/rank SUB fallback routes must remain")
        selector, = candidates
        branch = nodes[selector["target"]]
        require(len(branch["children"]) == 1, "A SUB fallback must have one original missing-data gate")
        gate, = branch["children"]
        require(any(item["target"] == gate["id"] and item["source"] == availability and item["property"] == "visible"
                    and item["default"] is False and item["cases"][0]["key"] == "false"
                    for item in result["variants"]), "The real missing-data availability gate changed")
        missing[key] = (selector, branch, gate)

    original_programs = {field: copy.deepcopy(result[field]) for field in ("bindings", "variants", "tracks")}
    serial = 0
    def fresh():
        nonlocal serial
        serial += 1
        return "pn.n" + str(serial)
    def clone(branch):
        old_nodes = list(walk(branch))
        old_ids = {node["id"] for node in old_nodes}
        require(not any(assignment["target"] in old_ids for machine in result["stateMachines"]
                        for state in machine["states"] for assignment in state["set"]),
                "A stateful target graph cannot be copied as a stateless readout")
        remap = {node["id"]: fresh() for node in old_nodes}
        def rewrite(value):
            if isinstance(value, str):
                return remap.get(value, value)
            if isinstance(value, list):
                return [rewrite(item) for item in value]
            if isinstance(value, dict):
                return {key: rewrite(item) for key, item in value.items()}
            return value
        copied = rewrite(copy.deepcopy(branch))
        programs = {}
        for field, source_programs in original_programs.items():
            programs[field] = []
            for item in source_programs:
                if item["target"] not in old_ids:
                    continue
                program = rewrite(copy.deepcopy(item))
                program["id"] = fresh()
                for child in program.get("cases", []) + program.get("keyframes", []):
                    child["id"] = fresh()
                programs[field].append(program)
        return copied, programs
    def append_programs(programs):
        for field, items in programs.items():
            result[field].extend(items)

    old_pb = nodes[primary["personal-best"]["target"]]
    pb_nodes = list(walk(old_pb))
    label, = [node for node in pb_nodes if node["type"] == "text" and node["properties"].get("text") == "MY BEST"]
    value_binding, = [item for item in original_programs["bindings"] if item["target"] in {node["id"] for node in pb_nodes}
                     and item["property"] == "text" and item["source"] == "score.personal-best"]
    value = nodes[value_binding["target"]]
    fonts = [font for font in manifest.get("bitmapFonts", []) if font["id"] == GRAPH_FONT]
    require(len(pb_nodes) == 5 and len(fonts) == 1 and value["properties"].get("bitmap-font") == GRAPH_FONT
            and value["properties"].get("format") == "integer"
            and set("0123456789") <= {glyph["character"] for glyph in fonts[0]["glyphs"]},
            "Keep the original five-node MY BEST readout and graph digit font")
    next_branch, programs = clone(old_pb)
    next_nodes = list(walk(next_branch))
    next_label = next(node for node in next_nodes if node["type"] == "text" and node["properties"].get("text") == "MY BEST")
    next_label["properties"]["text"] = ""
    programs["bindings"].append({"id": fresh(), "target": next_label["id"], "property": "text", "source": LABEL_SOURCE})
    source_map = {"score.personal-best": VALUE_SOURCE, "score.personal-best-progress": PROGRESS_SOURCE,
                  "score.personal-best-available": AVAILABLE_SOURCE}
    for item in programs["bindings"]:
        item["source"] = source_map.get(item["source"], item["source"])
    selector, = [item for item in programs["variants"] if item["source"] == "option.graph-target"]
    selector["cases"][0]["key"] = TARGET_VALUE
    owner["children"].append(next_branch)
    append_programs(programs)

    # Reuse the established full SUB behavior for the new primary. NEXT needs
    # both a matched PB and rank data, so either may be the unavailable input;
    # the fallback must still support an available PB on a non-BMS layout.
    fallback, programs = clone(missing["pb"][1])
    selector, = [item for item in programs["variants"] if item["target"] == fallback["id"]]
    selector["cases"][0]["key"] = TARGET_VALUE
    fallback_gate, = fallback["children"]
    unavailable, = [item for item in programs["variants"] if item["target"] == fallback_gate["id"]]
    unavailable["source"] = AVAILABLE_SOURCE
    owner["children"].append(fallback)
    append_programs(programs)
    pb_fallback, programs = clone(old_pb)
    selector, = [item for item in programs["variants"] if item["source"] == "option.graph-target"]
    selector["source"] = "option.graph-sub-target"
    for node in walk(pb_fallback):
        if node["type"] == "text" and node["properties"].get("text") == "MY BEST":
            node["properties"]["text"] = "SUB MY BEST"
    fallback_gate["children"].append(pb_fallback)
    append_programs(programs)
    # When either original primary is missing, NEXT is missing as well. The
    # selected unavailable SUB must report NO DATA, never a fabricated target.
    no_data_routes = []
    for gate in (missing["pb"][2], missing["rank"][2], fallback_gate):
        child_ids = {node["id"] for node in gate["children"]}
        none, = [item for item in result["variants"] if item["target"] in child_ids
                 and item["source"] == "option.graph-sub-target" and [case["key"] for case in item["cases"]] == ["none"]]
        none["cases"].append({"id": fresh(), "key": TARGET_VALUE, "value": True})
        no_data_routes.append(none["target"])
    return result, {"newPrimary": next_branch["id"], "newLabel": next_label["id"],
                    "newMissingPrimaryRoute": fallback["id"], "newUnavailableSubRoutes": no_data_routes,
                    "newNodes": sum(node["id"].startswith("pn.") for root in
                        (result["root"], *(item["root"] for item in result["templates"])) for node in walk(root)),
                    "font": GRAPH_FONT, "newFonts": 0, "newTextures": 0,
                    "primaryPriority": "Only a missing primary permits the selected SUB; neither valid yields NO DATA"}


def split_target_module(document, manifest):
    """Publish one chosen target through C2 instead of retaining seven trees.

    The original Graph plate/current-score/outline stay in the base scene.
    Each mutually exclusive choice receives its original target and necessary
    SUB route, behind exact copies of the original display/max/layout gates.
    No runtime source reads a special author option in engine code.
    """
    from build_omni_skin import resource_references

    augmented, next_review = inject(document, manifest)
    updated_manifest = add_options(manifest)
    target_option, = [item for item in updated_manifest["options"] if item["id"] == "graph-target"]
    updated_manifest["options"].remove(target_option)
    module = {"id": "graph-target", "label": "TARGET SCORE GRAPH", "default": target_option["default"],
              "choices": copy.deepcopy(target_option["choices"])}
    nodes = {node["id"]: node for root in (augmented["root"], *(item["root"] for item in augmented["templates"])) for node in walk(root)}
    declarations = _declarations(augmented, list(nodes.values()))
    require(not any(item["id"].startswith("gt.") for item in declarations), "A graph-target module identity already exists")
    parents = {child["id"]: node for node in nodes.values() for child in node.get("children", [])}
    selectors = [item for item in augmented["variants"] if item["source"] == "option.graph-target"]
    maximum, = [item for item in selectors if [case["key"] for case in item["cases"]] == ["maximum"]]
    owner = parents[maximum["target"]]
    require(all(parents.get(item["target"]) is owner and item["property"] == "visible" and item["default"] is False
                and all(case.get("value") is True for case in item["cases"]) for item in selectors),
            "All primary/fallback selectors must be sibling visible gates")
    outer = parents[owner["id"]]
    source_templates = [item for item in augmented["templates"] if item["root"] is outer]
    require(len(source_templates) == 1 and outer.get("slot") == "decoration", "Keep the original material-owned Graph root")
    source_template, = source_templates
    source_instances = [item for item in augmented["instances"] if item["template"] == source_template["id"]]
    layouts = ("p1", "p2", "dp")
    require(len(source_instances) == 3 and {item.get("material", {}).get("resource") for item in source_instances}
            == {"markers/frame-" + layout + "-information" for layout in layouts}
            and all(item["material"]["slot"] == "decoration" for item in source_instances),
            "The chosen target must retain all three exact Frame information material scopes")
    missing_maximum = [child for child in outer["children"] if any(
        item["target"] == child["id"] and item["source"] == "score.maximum-available"
        and item["property"] == "visible" and [case["key"] for case in item["cases"]] == ["false"]
        for item in augmented["variants"])]
    require(len(missing_maximum) == 1, "Keep the original missing-maximum NO DATA route")
    missing_maximum, = missing_maximum
    moving_roots = {item["target"] for item in selectors}
    moving_ids = {node["id"] for identifier in moving_roots for node in walk(nodes[identifier])}
    moving_ids.update(node["id"] for node in walk(missing_maximum))
    require(not any(assignment["target"] in moving_ids for machine in augmented["stateMachines"]
                    for state in machine["states"] for assignment in state["set"]),
            "A stateful Graph must be reviewed before splitting its target publication")
    base = copy.deepcopy(augmented)
    base_nodes = {node["id"]: node for root in (base["root"], *(item["root"] for item in base["templates"])) for node in walk(root)}
    base_nodes[owner["id"]]["children"] = [child for child in base_nodes[owner["id"]]["children"] if child["id"] not in moving_roots]
    base_nodes[outer["id"]]["children"] = [child for child in base_nodes[outer["id"]]["children"] if child["id"] != missing_maximum["id"]]
    for field in ("bindings", "variants", "tracks"):
        base[field] = [program for program in base[field] if program["target"] not in moving_ids]
    contributions = {}
    choice_reviews = []
    for choice in module["choices"]:
        value = choice["value"]
        selected = [item for item in selectors if any(case["key"] == value for case in item["cases"])]
        require(len(selected) == (2 if value in ("personal-best", "aaa", "aa", "a", "next") else 1),
                "A selected target must retain exactly its main branch and necessary SUB route")
        branches = [copy.deepcopy(nodes[item["target"]]) for item in selected]
        for branch in branches:
            branch["properties"]["visible"] = True  # C2 froze this primary choice before publication.
        selected_ids = {node["id"] for branch in (*branches, missing_maximum) for node in walk(branch)}
        target_owner = copy.deepcopy(owner)
        target_owner["id"] = "gt.maximum"
        target_owner["children"] = branches
        template_root = copy.deepcopy(outer)
        template_root["id"] = "gt.graph"
        template_root["children"] = [target_owner, copy.deepcopy(missing_maximum)]
        scene = {"contract": augmented["contract"], "root": _node("gt.root"),
            "templates": [{"id": "gt.template", "root": template_root}],
            "instances": [{"id": "gt." + layout, "template": "gt.template",
                           "material": {"slot": "decoration", "resource": "markers/frame-" + layout + "-information"}}
                          for layout in layouts], "bindings": [], "variants": [], "tracks": [], "stateMachines": []}
        ancestors = {owner["id"]: "gt.maximum", outer["id"]: "gt.graph"}
        for field in ("bindings", "variants", "tracks"):
            for program in augmented[field]:
                if program["target"] in selected_ids:
                    if program in selected:
                        continue
                    scene[field].append(copy.deepcopy(program))
                elif program["target"] in ancestors:
                    copied = copy.deepcopy(program)
                    copied["target"] = ancestors[copied["target"]]
                    copied["id"] = "gt.a." + copied["id"]
                    for child in copied.get("cases", []) + copied.get("keyframes", []):
                        child["id"] = "gt.a." + child["id"]
                    scene[field].append(copied)
        fonts = {node["properties"]["bitmap-font"] for node in walk(template_root) if node["properties"].get("bitmap-font")}
        branch_manifest = {field: copy.deepcopy(manifest[field]) for field in ("contract", "scene", "sceneContract", "eventContract")}
        branch_manifest["bitmapFonts"] = [copy.deepcopy(item) for item in manifest.get("bitmapFonts", []) if item["id"] in fonts]
        resources = resource_references(scene, branch_manifest["bitmapFonts"])
        branch_manifest["resources"] = [copy.deepcopy(item) for item in manifest["resources"] if item["id"] in resources]
        require(resources == {item["id"] for item in branch_manifest["resources"]}, "A selected target has an undeclared root resource")
        contributions[value] = {"scene": scene, "manifest": branch_manifest}
        choice_reviews.append({"value": value, "selectedRoots": [branch["id"] for branch in branches],
            "nodes": len(selected_ids) + 3, "resources": len(resources), "fonts": len(fonts),
            "primarySelection": "Frozen by C2; main selector removed, source subtree and SUB rules retained"})
    return base, updated_manifest, module, contributions, {
        "selection": "Existing C2 captures all choices and publishes exactly one graph-target contribution",
        "ordinaryOptionReplacedBySameIdModule": True, "primaryDefault": module["default"], "unifiedOptionCountChange": 0,
        "originalMediaResourcesFontsSlotsAndIniUnchanged": True, "originalTargetNodeIdsPreserved": True,
        "ancestorNodeIds": ["gt.graph", "gt.maximum"], "materialScopes": ["markers/frame-" + layout + "-information" for layout in layouts],
        "subTarget": "Existing root graph-sub-target choice, primary valid first, fallback only when missing, otherwise NO DATA",
        "next": next_review, "choices": choice_reviews}
