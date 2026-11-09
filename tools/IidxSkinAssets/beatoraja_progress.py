"""Retain the selected author's song-position cursor on native playback progress.

Only actual global decoration routes with a timing.progress -> y binding are
owned here. The original image, parent-space travel and inherited tint remain;
beatoraja's public music-progress property owns the value and duration policy.
"""

import math

from beatoraja_author import material_bindings, material_templates
from beatoraja_beat import (STEP_BEAT_ADAPTATION, BEAT_ADAPTATION, apply_beat_opacity,
                           beat_opacity_track)
from beatoraja_native import Lua, colour, native_colour, native_rect, node_rect, scene_properties
from beatoraja_preparation import PRESENTATION_TIMER


def _source20_intro(node, frame, parent, canvas):
    """Only the real20 120-tick sampled sine on the original travel container."""
    tracks = [track for track in frame.scene.get("tracks", []) if track["target"] == node["id"]]
    if not tracks:
        return None
    if (frame.choice != "source20-gmframe0" or node["type"] != "container" or len(tracks) != 1
            or parent != canvas or canvas != (0, 0, 1280, 720)
            or frame.scene.get("preparationDuration", 0) < 2000):
        raise ValueError("Progress intro requires its original20 travel container and2000ms preparation")
    track = tracks[0]
    keys = track.get("keyframes", [])
    if (track.get("type") != "tween" or track.get("property") != "y"
            or track.get("clock") != "presentation.elapsed" or track.get("loop", False)
            or track.get("easing") != "step" or len(keys) != 121):
        raise ValueError("Progress intro requires the original20 sampled sine track")
    for tick, key in enumerate(keys):
        expected = 10 if tick == 120 else 415 - math.trunc(405 * math.sin(tick * math.pi / 240))
        if (not math.isclose(key.get("time", -1), tick * 1000 / 60, abs_tol=1e-8)
                or not math.isclose(key.get("value", -1), expected / 720, abs_tol=1e-10)):
            raise ValueError("Progress intro differs from the qualified120-tick original20 sine")
    return track


def _source20_intro_value():
    """FloatProperty keeps position separate from the Slider's beat-alpha timer.

    This reads the already-installed presentation CustomTimer cache. An absent
    READY history uses actual native progress, without inventing an intro.
    """
    return Lua("""function()
  local origin=main_state.timer(__TIMER__)
  if origin==main_state.timer_off_value then return main_state.float_number(6) end
  local elapsed=math.max(0,(main_state.time()-origin)/1000)
  if elapsed>=2000 then return main_state.float_number(6) end
  local tick=math.min(119,math.floor(elapsed*60/1000))
  local y=415-math.floor(405*math.sin(tick*math.pi/240))
  return (y-10)/405
end""".replace("__TIMER__", str(PRESENTATION_TIMER)))


def _routes(author, options, keymode, presentation):
    frame = author.contribution("frame", options)
    targets = {operation["target"] for operation in frame.scene.get("bindings", [])
               if operation["source"] == "timing.progress" and operation["property"] == "y"}
    selected, seen = [], set()

    def visit(node, ancestors):
        if node["id"] in targets and node["id"] not in seen:
            seen.add(node["id"])
            selected.append((*ancestors, node))
        for child in node.get("children", []):
            visit(child, (*ancestors, node))

    for binding in material_bindings(frame, keymode, presentation):
        if (binding["target"]["kind"] == "global" and binding["slot"] == "decoration"
                and binding["mode"] == "Provide"):
            for root in material_templates(frame, "decoration", binding["resource"]):
                visit(root, ())
    return frame, selected


def progress_ownership(author, options, keymode, presentation):
    """Exact leaf ownership for independently composed decoration fragments."""
    _, routes = _routes(author, options, keymode, presentation)
    return {"handled_node_ids": [route[-1]["id"] for route in routes]}


def export_progress(author, options, keymode, presentation, sink, canvas):
    """Return native slider/destination arrays without changing source pixels."""
    frame, routes = _routes(author, options, keymode, presentation)
    result = {"slider": [], "destination": [], "gaps": [],
              "handled_node_ids": [route[-1]["id"] for route in routes]}
    context = {"layout.ruleset": "bms", "layout.presentation": presentation,
               **{"option." + key: value for key, value in options.items()}}
    for route in routes:
        rect, rgba, blend, visible = canvas, (1, 1, 1, 1), "alpha", True
        cursor = route[-1]
        beat = beat_opacity_track(cursor, frame.scene)
        if beat is not None:
            keys = beat["keyframes"]
            if (frame.choice != "source20-gmframe0" or beat.get("easing") != "step" or len(keys) != 51
                    or any(not math.isclose(key["time"], tick / 50, abs_tol=1e-10)
                           or not math.isclose(key["value"], 1 - tick / 100 if tick < 50 else 1, abs_tol=1e-10)
                           for tick, key in enumerate(keys))):
                raise ValueError("Progress beat requires the own20 closed50-step host-alpha adaptation")
        intro = None
        for node in route:
            properties, dynamic = scene_properties(node, frame.scene, context)
            is_cursor = node is cursor
            expected = [operation for operation in dynamic
                        if (is_cursor and operation["kind"] == "bindings"
                            and operation["source"] == "timing.progress" and operation["property"] == "y")]
            if dynamic != expected or (is_cursor and len(expected) != 1):
                raise ValueError("Progress cursor has an unqualified dynamic route")
            tracks = [track for track in frame.scene.get("tracks", []) if track["target"] == node["id"]]
            if not is_cursor and tracks:
                if intro is not None:
                    raise ValueError("Progress cursor has more than one intro container")
                intro = _source20_intro(node, frame, rect, canvas)
                properties["y"] = intro["keyframes"][-1]["value"]
            if (node["type"] != ("sprite" if is_cursor else "container")
                    or node.get("effects") or properties.get("rotation", 0)
                    or properties.get("clip", False)
                    or (is_cursor and any(track is not beat for track in tracks))):
                raise ValueError("Progress cursor requires its static axis-aligned source route")
            if is_cursor:
                span = rect[3]
                properties["y"] = 0  # The binding replaces, rather than adds to, source y.
                if beat:
                    properties["opacity"] = 1
            rect = node_rect(properties, rect)
            visible = visible and properties.get("visible", True)
            rgba = colour(properties, rgba)
            blend = node.get("blend", "inherit") if node.get("blend", "inherit") != "inherit" else blend
        if not visible or rgba[3] == 0:
            continue
        if span <= 0 or min(rect[2:]) <= 0 or blend not in ("alpha", "additive"):
            raise ValueError("Progress cursor requires positive source travel and a supported blend")
        resource = frame.resources[properties["resource"]]
        if resource["type"] != "texture":
            raise ValueError("Progress cursor must use its original texture")
        source = sink.add_source(resource["path"])
        width, height = sink.dimensions[source]
        identifier = author.family + ".progress." + cursor["id"]
        slider = {"id": identifier, "src": source, "x": 0, "y": 0,
                                 "w": width, "h": height, "angle": 2, "range": round(span),
                                 "type": 6, "changeable": False}
        if intro is not None:
            if not math.isclose(span, 405) or (width, height) != (66, 72):
                raise ValueError("Original20 intro requires its complete66x72 cursor and405px travel")
            slider["value"] = _source20_intro_value()
        result["slider"].append(slider)
        destination = {"id": identifier, "filter": 0,
                                      "blend": 2 if blend == "additive" else 1,
                                      "dst": [{**native_rect(rect, canvas[3]), **native_colour(rgba)}]}
        if beat is not None:
            destination = apply_beat_opacity(destination, beat, rgba[3])
            adaptation = STEP_BEAT_ADAPTATION if beat.get("easing") == "step" else BEAT_ADAPTATION
            if not any(gap.get("code") == adaptation["code"] for gap in result["gaps"]):
                result["gaps"].append({**adaptation, "informational": True})
        result["destination"].append(destination)
        if intro is not None and not any(gap.get("code") == "native-progress-intro-adaptation" for gap in result["gaps"]):
            result["gaps"].append({"code": "native-progress-intro-adaptation", "informational": True,
                "detail": "Original20's120-tick sine415-to10 uses121 source samples over2000ms on the observed "
                          "presentation clock. Native read-only Slider.value shows that sampled intro before2000ms, "
                          "then actual public music progress6 on the original405px travel. The source preparation "
                          "lasts at least2000ms, so PLAY progress cannot compete with the intro. A missing observed "
                          "READY epoch uses native progress immediately; no earlier intro is reconstructed. "
                          "Original tick cadence and exact chart-terminal duration remain distinct from this host clock."})
    if result["slider"]:
        source_motion = any("value" in slider for slider in result["slider"]) or any(
            "timer" in destination for destination in result["destination"])
        motion_boundary = ("Additional authored intro/beat routes are qualified separately." if source_motion else
                           "No source beat timing or artificial cursor animation is inferred.")
        result["gaps"].extend([
            {"code": "native-progress-duration-adaptation", "informational": True,
             "detail": "Original song-position art follows native music progress (rate 6): PLAY elapsed / host playtime, "
                       "zero before PLAY and capped at one. Native playtime includes its completion margin and practice/autoplay policy; "
                       "it is not OMS's first-to-last hit-object interval. " + motion_boundary},
            {"code": "native-progress-integer-travel", "informational": True,
             "detail": "Native destination and travel are integral source pixels; the official loader then truncates scaled Slider travel "
                       "to whole destination pixels. Cursor-origin error is below 1 source pixel plus 1/destinationScale, apart from float arithmetic. "
                       "Original PNG bytes, crop and source geometry remain the reference."},
        ])
    return result
