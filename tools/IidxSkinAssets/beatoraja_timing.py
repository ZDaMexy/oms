"""Selected author FAST/SLOW art on actual beatoraja Judge-region state.

Standard mounts its original Position A/B/C sprites in each frame's exact stage
foreground; Lightning uses the selected Judge material's horizontal sprite rows.
Combined feedback uses official early/late qualification and signed whole-
millisecond reads; independent key/scratch history cannot be inferred from one
region's latest hit. Neither current source has millisecond-number artwork.
"""

from __future__ import annotations

import math

from beatoraja_author import layout_for, material_bindings, material_templates, walk_nodes
from beatoraja_decoration import _sample_static
from beatoraja_native import Lua, colour, native_colour, native_rect, node_rect, scene_properties


_COMBINED = "judgement.timing-direction"
_SEPARATE = {"judgement.keys-timing-direction", "judgement.scratch-timing-direction"}


def export_timing(author, options, keymode, presentation, sink, canvas):
    """Return image/value/text/destination/gaps and handled material root IDs.

    The caller must retain one native Judge owner per actual stage even when its
    artwork is OFF. SP2 still uses region 0, timer 46 and integer property 525;
    DP uses regions 0/1, timers 46/47 and properties 525/526. Property 527 is the
    optional third Judge region, never the second deck's scratch-only state.
    """
    result = {"image": [], "value": [], "text": [], "destination": [], "gaps": [], "handled_root_ids": []}
    standard = author.family == "standard" and "timing-display" not in author.modules
    if not standard and "timing-display" not in author.modules:
        return result
    if (keymode, presentation) not in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
        raise ValueError("Timing export requires a matching SP or DP presentation")
    x, y, width, height = canvas
    if x != 0 or y != 0 or min(width, height) <= 0:
        raise ValueError("Timing export requires the full positive zero-origin canvas")
    frame = author.contribution("frame", options)
    contribution = frame if standard else author.contribution("timing-display", options)
    if not contribution.scene.get("instances"):
        return result
    owner = frame if standard else author.contribution("judgement", options)
    slot = "stage.foreground" if standard else "hud.judgement"
    layout = layout_for(frame, keymode, presentation)
    bindings = [item for item in material_bindings(owner, keymode, presentation)
                if item["slot"] == slot and item["mode"] == "Provide"]
    tracks = {}
    for track in contribution.scene.get("tracks", []):
        tracks.setdefault(track["target"], []).append(track)
    emitted = set()

    def resolved(node, context):
        properties, dynamic = scene_properties(node, contribution.scene, context)
        if dynamic:
            raise ValueError("Unmapped timing sources: " + ", ".join(sorted({v["source"] for v in dynamic})))
        for track in tracks.get(node["id"], []):
            known, value = _sample_static(track, context)
            if not known:
                raise ValueError("Unmapped timing track: " + track["id"])
            properties[track["property"]] = value
        if (node.get("effects") or properties.get("rotation", 0) % 360
                or properties.get("scale-x", 1) != 1 or properties.get("scale-y", 1) != 1
                or node.get("blend", "inherit") not in ("inherit", "alpha")):
            raise ValueError("Timing transform requires a separately qualified bake")
        return properties

    def emit(child, props, cell, stage, direction, rgba, offsets=(), source=None):
        resource = contribution.resources[props["resource"]]
        if resource["type"] != "texture":
            raise ValueError("Timing sprite must use its declared texture")
        src = source or sink.add_source(resource["path"])
        source_width, source_height = sink.dimensions[src]
        identifier = f"{author.family}.timing.{stage}.{child['id']}.{direction}"
        result["image"].append({"id": identifier, "src": src, "x": 0, "y": 0, "w": source_width, "h": source_height})
        # JudgeManager stores (note microtime - input microtime) / 1000,
        # truncated toward zero. Positive is FAST, negative is SLOW. The
        # native boolean also excludes PGREAT and the initial/no-hit state.
        option = 1242 + stage * 20 + (direction == "slow")
        comparator = ">" if direction == "fast" else "<"
        draw = Lua(f"function() return main_state.option({option}) and main_state.number({525 + stage}) {comparator} 0 end")
        result["destination"].append({"id": identifier, "timer": 46 + stage, "loop": -1,
                                       "filter": 0, "blend": 1, "draw": draw,
                                       **({"offsets": list(offsets)} if offsets else {}),
                                       "dst": [{"time": 0, **native_rect(cell, height), **native_colour(rgba)},
                                               {"time": 500}]})
        emitted.add(stage)

    def lift_offsets(route, context, host):
        route_tracks = [track for node in route for track in tracks.get(node["id"], [])
                        if track.get("clock") == "lift.offset"]
        if not route_tracks:
            return []
        if any(t["property"] != "y" or t.get("loop") or t.get("easing") != "linear" for t in route_tracks):
            raise ValueError("Timing Lift track does not match the native lane offset")
        samples = {0, .5, 1, *(key["time"] for t in route_tracks for key in t["keyframes"])}
        rectangles = {}
        for amount in samples:
            rect = host
            for node in route:
                rect = node_rect(resolved(node, {**context, "lift.offset": amount}), rect)
            rectangles[amount] = rect
        base = rectangles[0]
        for amount, rect in rectangles.items():
            expected = (base[0], base[1] - amount * host[3], base[2], base[3])
            if not 0 <= amount <= 1 or any(not math.isclose(a, b, rel_tol=0, abs_tol=1e-6) for a, b in zip(rect, expected)):
                raise ValueError("Timing Lift track does not match the native lane offset")
        return [3]

    def walk(node, parent, context, stage, direction, inherited=(1, 1, 1, 1), route=(), host=None):
        properties = resolved(node, context)
        if properties.get("visible", True) is False:
            return
        rgba = colour(properties, inherited)
        if rgba[3] <= 0:
            return
        rect = node_rect(properties, parent)
        route = (*route, node)
        if node["type"] == "sprite" and standard:
            emit(node, properties, rect, stage, direction, rgba, lift_offsets(route, context, host))
            return
        if node["type"] != "container":
            raise ValueError("Timing source requires a qualified sprite or horizontal row")
        if properties.get("layout") != "horizontal":
            for child in node.get("children", []):
                walk(child, rect, context, stage, direction, rgba, route, host)
            return
        # GameplaySkinHorizontalContainer uses logical pixels for its direct
        # leaves and centres the entire row inside its carrier. It does not
        # stretch each sprite to the default full-size relative rectangle.
        cells, cursor, left, right = [], 0, 0, 0
        for child in node.get("children", []):
            props = resolved(child, context)
            if child["type"] != "sprite" or "height-px" not in props:
                raise ValueError("Timing row has no qualified original sprite dimensions")
            resource = contribution.resources[props["resource"]]
            if resource["type"] != "texture":
                raise ValueError("Timing sprite must use its declared texture")
            src = sink.add_source(resource["path"])
            source_width, source_height = sink.dimensions[src]
            cell_height = props["height-px"] * height / 768
            cell_width = props.get("width-px", props["height-px"] * source_width / source_height) * height / 768
            cells.append((child, props, src, source_width, source_height, cursor, cell_width, cell_height))
            left, right = min(left, cursor), max(right, cursor + cell_width)
            cursor += cell_width + properties.get("gap-px", 0) * height / 768
        for child, props, src, source_width, source_height, cursor, cell_width, cell_height in cells:
            if props.get("visible", True) is False:
                continue
            cell = (rect[0] + rect[2] / 2 + cursor - (left + right) / 2,
                    rect[1] + (rect[3] - cell_height) / 2, cell_width, cell_height)
            emit(child, props, cell, stage, direction, colour(props, rgba), source=src)

    for binding in bindings:
        target = binding["target"]
        if target["kind"] != "stage":
            raise ValueError("Timing feedback requires its exact stage material owner")
        stage = int(target["group-logical"])
        if stage >= len(layout["stages"]) or stage not in (0, 1):
            raise ValueError("Timing stage does not match the actual play layout")
        sx, sy, sw, sh = layout["stages"][stage]["rect"]
        sx, sy, sw, sh = sx * width, sy * height, sw * width, sh * height
        slot_height = min(sh * .12, height * .11)
        host = (sx, sy, sw, sh) if standard else (sx, sy + sh * .81 - slot_height / 2, sw, slot_height)
        context = {"layout.ruleset": "bms", "layout.presentation": presentation,
                   "layout.group": target["group"], **{"option." + key: value for key, value in options.items()},
                   "lift.offset": 0, "preparation.active": False}
        roots = material_templates(contribution, slot, binding["resource"])
        for root in roots:
            ids = {node["id"] for node in walk_nodes(root)}
            if standard and not any(v["target"] in ids and v["source"] == _COMBINED
                                    for kind in ("bindings", "variants") for v in contribution.scene.get(kind, [])):
                continue
            if root["id"] not in result["handled_root_ids"]:
                result["handled_root_ids"].append(root["id"])
            separate = {v["source"] for category in ("bindings", "variants") for v in contribution.scene.get(category, [])
                        if v["target"] in ids and v["source"] in _SEPARATE}
            if separate:
                result["gaps"].append({"code": "native-timing-separate-channels-unavailable", "stage": stage,
                                       "sources": sorted(separate),
                                       "detail": "525/526/527 describe Judge regions, not independent key/scratch timing histories. Separate Display is not replaced with duplicated combined feedback."})
                continue
            for direction in ("fast", "slow"):
                # Native Judge timers start on real gameplay judgements, so the
                # source preparation-hidden branch has no visible native owner.
                walk(root, host, {**context, _COMBINED: direction}, stage, direction, host=host)
    if emitted:
        result["gaps"].append({"code": "native-timing-feedback-adaptation", "stages": sorted(emitted),
                               "detail": "Official non-PG early/late qualification includes native POOR cases; 525/526 are signed whole milliseconds (positive FAST). Feedback expires 500ms after the latest region Judge, unlike OMS's retained latest eligible hint. Current author choices contain no millisecond-number artwork."})
    return result
