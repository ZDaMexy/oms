"""Original long-note effects driven only by native long-note lane timers.

HOLD (70...), HCN ACTIVE (250...) and HCN DAMAGE (270...) are owned by
JudgeManager. Custom timers 10100--10117 observe their transitions; they never
write gameplay state. A render-observed lane state is not an object identity.
"""

from beatoraja_author import material_templates, walk_nodes
from beatoraja_native import Lua, colour, node_rect, scene_properties


TRANSITION_BASE = 10100


def transition_timer(deck, key):
    lane = deck * 10 + key
    if deck not in (0, 1) or key not in range(8):
        raise ValueError("Expected a native BMS scratch/key lane")
    return {"id": TRANSITION_BASE + lane, "timer": Lua("""(function()
  local off=main_state.timer_off_value
  local previous,previous_hold,previous_hcn,previous_time,play=nil,nil,nil,nil,nil
  local origin=off
  return function()
    local now=main_state.time()
    local active=main_state.timer(41)
    local hold=main_state.timer(""" + str(70 + lane) + """)
    local hcn=main_state.timer(""" + str(250 + lane) + """)
    local damage=main_state.timer(""" + str(270 + lane) + """)
    local held=damage==off and (hcn~=off or hold~=off)
    if active==off or play~=active or previous==nil or now<previous_time then
      origin=off
    elseif held~=previous or (held and
        ((hold~=off and previous_hold~=off and hold~=previous_hold) or
         (hcn~=off and previous_hcn~=off and hcn~=previous_hcn))) then origin=now end
    previous,previous_hold,previous_hcn,previous_time,play=held,hold,hcn,now,active
    return origin
  end
end)()""")}


def state_condition(deck, key, state):
    lane = deck * 10 + key
    hold, hcn, damage = (f"(main_state.timer({base + lane}) ~= main_state.timer_off_value)"
                         for base in (70, 250, 270))
    # DAMAGE may coexist with HOLD during native release grace. It wins.
    return {0: f"(not {damage} and not {hcn} and not {hold})",
            1: f"(not {damage} and not {hcn} and {hold})",
            2: f"(not {damage} and {hcn})", 3: damage}[state]


def _visible_states(operation):
    if (operation["kind"], operation["property"], operation["source"]) != (
            "variants", "visible", "object.long-note-appearance"):
        raise ValueError("Unmapped long-note effect binding: " + operation["source"])
    cases = {case["key"]: case["value"] for case in operation["cases"]}
    if operation.get("default") is not False or any(not isinstance(v, bool) for v in cases.values()):
        raise ValueError("Long-note appearance requires explicit boolean state visibility")
    known = {"ln-holding", "cn-holding", "hcn-holding", "hcn-released"}
    if any(value for key, value in cases.items() if key not in known):
        raise ValueError("Long-note appearance requires unavailable per-object state")
    if cases.get("ln-holding", False) != cases.get("cn-holding", False):
        raise ValueError("Native lane HOLD does not distinguish LN from CN")
    return {state for state, name in ((1, "cn-holding"), (2, "hcn-holding"), (3, "hcn-released"))
            if cases.get(name, False)}


def hold_plan(author, contribution, options, presentation, lane_materials):
    """Use Composer.lane_materials(..., 'effect.long-note') geometry unchanged.

    Each item is (binding, native deck, native key, lane rect, key rect, hit rect).
    Preserve every original template ancestor; the source 4x carrier is essential.
    Native offset 3 moves this judge-line effect with the player's Lift setting.
    """
    result = {"customTimers": [], "draws": [], "gaps": []}
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    animations = {item["id"]: item for item in contribution.manifest.get("animations", [])}
    used = set()
    for binding, deck, key, _, _, hit in lane_materials:
        if binding["mode"] != "Provide" or binding["slot"] != "effect.long-note":
            raise ValueError("Expected a provided long-note effect material")
        roots = material_templates(contribution, binding["slot"], binding["resource"])
        if not roots:
            raise ValueError("Provided long-note effect has no source template")
        targets = {node["id"] for root in roots for node in walk_nodes(root)}
        if any(track["target"] in targets for track in contribution.scene.get("tracks", [])):
            raise ValueError("Long-note effect has an unmapped source track")
        before = len(result["draws"])

        def visit(node, parent, rgba, states):
            prop, dynamic = scene_properties(node, contribution.scene, context)
            if prop.get("visible", True) is False and not any(d["property"] == "visible" for d in dynamic):
                return
            for operation in dynamic:
                states = states & _visible_states(operation)
                prop[operation["property"]] = True
            if not states:
                return
            if prop.get("rotation", 0) or prop.get("clip", False) or node.get("effects") or node.get("blend", "alpha") != "alpha":
                raise ValueError("Long-note effect transform needs a qualified adapter")
            rect, rgba = node_rect(prop, parent), colour(prop, rgba)
            if node["type"] == "recorded-animation":
                program = author.json(animations[node["animation"]]["path"])
                trigger = program["trigger"]
                if trigger == "object.long-note-transition" and not program["loop"]:
                    used.add((deck, key))
                    clocks = [(TRANSITION_BASE + deck * 10 + key, states)]
                elif trigger == "object.state" and program["loop"] and 0 not in states:
                    clocks = [(base + deck * 10 + key, {state}) for state, base in
                              ((1, 70), (2, 250), (3, 270)) if state in states]
                else:
                    raise ValueError("Unqualified original long-note trigger/loop combination")
                for timer, selected in clocks:
                    conditions = [] if selected == {0, 1, 2, 3} else ["(" + " or ".join(
                        state_condition(deck, key, state) for state in sorted(selected)) + ")"]
                    result["draws"].append({"manifest": contribution.manifest_path, "animation": node["animation"],
                        "rect": rect, "rgba": rgba, "timer": timer, "conditions": conditions, "offsets": [3]})
            elif node["type"] != "container":
                raise ValueError("Unmapped original long-note effect node: " + node["type"])
            for child in sorted(node.get("children", []), key=lambda n: -n.get("properties", {}).get("z", 0)):
                visit(child, rect, rgba, states)

        for root in roots:
            visit(root, hit, (1, 1, 1, 1), {0, 1, 2, 3})
        if len(result["draws"]) == before:
            raise ValueError("Provided long-note effect emitted no source animation")
    result["customTimers"] = [transition_timer(deck, key) for deck, key in sorted(used)]
    if result["draws"]:
        result["gaps"].append({"code": "native-hold-lane-state-adaptation", "informational": True,
            "detail": "Original sustained loops use public HOLD/HCN ACTIVE/DAMAGE lane timers, with DAMAGE taking priority. Native HOLD cannot identify per-object LN/CN or an early HCN before its ACTIVE timer; generic LN/CN art is used in that interval. Chart-level LN mode 308 cannot resolve mixed-note types and is not substituted for lane state."})
        result["gaps"].append({"code": "native-hold-render-edge-adaptation", "informational": True,
            "detail": "The original transition clip observes entering/leaving native held status, or a changed native timer origin during continued holding, once per render. It keeps its one-shot exit tail; an unheld HCN miss and subsequent DAMAGE expiry do not invent holding transitions. Attachment, retry and time rewind establish a baseline; multiple changes between renders collapse, and lane timers cannot retain overlapping object-specific tails."})
    return result
