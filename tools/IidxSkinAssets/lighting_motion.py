"""Omni-only motion authored over original, unchanged texture resources.

This module only changes JSON dictionaries supplied by the author tool. It does
not open an installation, copy artwork, write an author directory, or start a
host. The caller owns source verification, byte-exact asset copying and package
validation. Idle speed, input visual scale, pulse envelopes and blink period are
author adaptations; the original single-frame AFP hooks do not establish them.
"""

from __future__ import annotations

import copy
import math
import re

SCENE_CONTRACT = "oms-gameplay-skin-scene.v1"
MANIFEST_CONTRACT = "oms-gameplay-skin-manifest.v1"
FRAMES = (*[f"iidx{i}" for i in range(27, 34)], "bpl-apina", "bpl-gamepanic", "bpl-gigo",
          "bpl-leisureland", "bpl-round1", "bpl-silkhat", "bpl-supernova-hd", "bpl-tradz")
LAYOUTS = ("p1", "p2", "dp")
IDLE_PERIOD_MS = 4000
COUNTS_PER_TURN = 360
REVOLUTIONS_PER_TURN = 1
GAUGE_BLINK_PERIOD_MS = 400
_TURN = re.compile(r"t\.t(?P<style>\d{2})\.base\.(?P<layout>p1|p2|dp)\.(?P<frame>[a-z0-9-]+)\.n(?P<group>[01])\Z")


def _require(condition, message):
    if not condition:
        raise ValueError(message)


def frame_namespace(frame_id):
    """Original node IDs use bpl.team; catalog values and file paths keep bpl-team."""
    _require(frame_id in FRAMES, "Unknown Omni frame")
    return frame_id.replace("bpl-", "bpl.", 1)


def _walk(node):
    yield node
    for child in node.get("children", []):
        yield from _walk(child)


def _nodes(document):
    yield from _walk(document["root"])
    for template in document.get("templates", []):
        yield from _walk(template["root"])


def _node(identifier, kind="container", properties=None, children=None, resource=None, slot=None, blend="alpha"):
    value = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": blend,
             "properties": properties or {}, "effects": [], "children": children or []}
    if resource is not None:
        value["resource"] = resource
    if slot is not None:
        value["slot"] = slot
    return value


def _track(identifier, target, prop, clock, values):
    return {"id": identifier, "type": "tween", "target": target, "property": prop,
            "clock": clock, "easing": "linear", "loop": True,
            "keyframes": [{"id": identifier + f".k{index}", "time": time, "value": value}
                          for index, (time, value) in enumerate(values)]}


def _append_exact(items, value, kind):
    previous = [item for item in items if item.get("id") == value["id"]]
    _require(not previous or previous == [value], f"Existing {kind} differs from the Omni motion declaration")
    if not previous:
        items.append(value)


def _positive(value, description):
    _require(isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value)
             and 0 < value <= 86400000, f"{description} must be positive and inside the public track budget")


def inject_turntables(document, *, idle_period_ms=IDLE_PERIOD_MS, counts_per_turn=COUNTS_PER_TURN,
                      revolutions_per_turn=REVOLUTIONS_PER_TURN):
    """Rotate every original knob around its existing centre, retaining its fixed source quad.

    One selected turntable contribution contains all 15 frames and all three
    information layouts. The outer motor uses gameplay milliseconds; two inner
    rotations separately consume relative counts and accepted revolutions.
    Single-player P2 still consumes logical group zero. Signed motion in replay
    is unavailable and is not inferred from the lane attack bit.
    """
    _require(document.get("contract") == SCENE_CONTRACT, "Expected an author scene")
    for value, label in ((idle_period_ms, "Idle period"), (counts_per_turn, "Counts per visual turn"),
                         (revolutions_per_turn, "Revolutions per visual turn")):
        _positive(value, label)
    candidates = [(node, _TURN.fullmatch(node.get("id", ""))) for node in _nodes(document)]
    candidates = [(node, match) for node, match in candidates if match]
    _require(len(candidates) == 60 and len({match["style"] for _, match in candidates}) == 1,
             "Expected the complete 60 original knobs of one turntable choice")
    _require({(match["layout"], match["frame"], int(match["group"])) for _, match in candidates}
             == {(layout, frame, group) for layout in LAYOUTS for frame in FRAMES
                 for group in range(2 if layout == "dp" else 1)}, "Turntable layouts are incomplete")
    all_nodes = {node["id"]: node for node in _nodes(document)}
    tracks = document["tracks"]
    mutations = []
    track_declarations = []
    for knob, match in candidates:
        identifier = knob["id"]
        motor_id, counts_id = identifier + ".motor", identifier + ".counts"
        logical_group = 2 if match["layout"] == "dp" and match["group"] == "1" else 1
        declarations = [
            _track(identifier + ".motion.idle", motor_id, "rotation", "timing.time", [(0, 0), (idle_period_ms, 360)]),
            _track(identifier + ".motion.counts", counts_id, "rotation", f"input.motion-counts-{logical_group}",
                   [(0, 0), (counts_per_turn, 360)]),
            _track(identifier + ".motion.revolutions", identifier, "rotation", f"input.motion-revolutions-{logical_group}",
                   [(0, 0), (revolutions_per_turn, 360)]),
        ]
        _require(knob.get("type") in ("sprite", "projective-sprite") and knob.get("resource") is not None,
                 "The original turntable must remain a textured sprite")
        inner = {"x": 0.5, "y": 0.5, "width": 1, "height": 1, "origin": "centre"}
        if motor_id in all_nodes:
            motor, counts = all_nodes[motor_id], all_nodes.get(counts_id)
            _require(counts is not None and motor["children"] == [counts] and counts["children"] == [knob]
                     and counts["properties"] == inner and knob["properties"] == inner,
                     "An existing turntable motion wrapper is incomplete")
        else:
            properties = knob["properties"]
            _require(set(properties) == {"x", "y", "width", "height"},
                     "A static source knob has unexpected programmed geometry")
            _require(not any(track.get("target") == identifier and track.get("property") == "rotation" for track in tracks),
                     "Another track already owns this knob rotation")
            original_parent = next(node for node in all_nodes.values() if knob in node.get("children", []))
            motor_properties = {**properties, "x": properties["x"] + properties["width"] / 2,
                                "y": properties["y"] + properties["height"] / 2, "origin": "centre"}
            counts = _node(counts_id, properties=copy.deepcopy(inner), children=[knob])
            motor = _node(motor_id, properties=motor_properties, children=[counts])
            mutations.append((original_parent, knob, motor, inner))
        for declaration in declarations:
            previous = [track for track in tracks if track.get("id") == declaration["id"]]
            _require(not previous or previous == [declaration], "An existing turntable clock declaration differs")
        track_declarations.extend(declarations)
    # Validate the whole contribution before making any changes.
    for parent, knob, motor, inner in mutations:
        parent["children"][parent["children"].index(knob)] = motor
        knob["properties"] = copy.deepcopy(inner)
    for declaration in track_declarations:
        _append_exact(tracks, declaration, "turntable track")
    return {"style": int(candidates[0][1]["style"]), "knobs": len(candidates), "newWrappers": len(mutations) * 2,
            "originalResourcesAndQuadsPreserved": True, "idlePeriodMs": idle_period_ms,
            "countsPerVisualTurn": counts_per_turn, "revolutionsPerVisualTurn": revolutions_per_turn,
            "directionEvidence": "1P clockwise from user observation; 2P clockwise is an explicit author choice, not a native-host claim",
            "unknown": "Original idle speed, host motor/inertia model, physical contact latency and signed replay motion"}


def art_requirements(frame_id, source_map):
    """Describe originals the caller must verify and copy byte-for-byte, without doing I/O."""
    _require(frame_id in FRAMES, "Unknown Omni frame")
    glows = [draw for draw in source_map["layouts"]["1p_frame"]["draws"] if "bpm_gra_1p" in draw.get("game_bindings", [])]
    _require(len(glows) == 1 and glows[0]["texture"] == "playm_bpm_gra_bar", "No exact original beat-glow hook")
    asset = glows[0]["asset"]
    roles = {"beat": "playm_bpm_gra_bar", "normal": "playm_gauge_normal_dot",
             "hard": "playm_gauge_hard_dot", "ex-hard": "playm_gauge_exhard_dot"}
    return {role: {"source": {"root": asset.get("root", "primary"), "package": asset["package"], "name": name},
                   "resource": {"id": f"omni.motion.{frame_id}.{role}", "type": "texture",
                                "path": f"art/motion/{frame_id}/{role}.png", "filter": "linear"}}
            for role, name in roles.items()}


def _glow_geometry(frame_id, layout, source_map):
    """Project source hooks onto the same fixed author stage rectangles as build_skin.py."""
    sd = frame_id in ("iidx27", "iidx28", "iidx29")
    width, height = (438, 726) if sd else (440, 728)
    scale = 1920 / source_map["stage"]["width"]
    movie = {"p1": "1p_frame", "p2": "2p_frame", "dp": "dp_frame_1p"}[layout]
    geometries, evidence = [], []
    for group in range(2 if layout == "dp" else 1):
        player = "2p" if layout == "p2" or group == 1 else "1p"
        draws = [draw for draw in source_map["layouts"][movie]["draws"] if f"bpm_gra_{player}" in draw.get("game_bindings", [])]
        _require(len(draws) == 1, "Each original stage must have one exact beat-glow hook")
        draw = draws[0]
        _require(draw["texture"] == "playm_bpm_gra_bar" and draw["blend"] in (0, 8)
                 and draw["mult_color"] == {"r": 1, "g": 1, "b": 1, "a": 1}
                 and draw["add_color"] == {"r": 0, "g": 0, "b": 0, "a": 0},
                 "The source beat-glow material needs a different author projection")
        stage_left = ((456 if group == 0 else 1026) if sd else (455 if group == 0 else 1025)) if layout == "dp" else (
            (1410 if layout == "p2" else 72) if sd else (1409 if layout == "p2" else 71))
        bounds = draw["bounds"]
        geometry = {"x": (bounds["left"] * scale - stage_left) / width, "y": bounds["top"] * scale / height,
                    "width": (bounds["right"] - bounds["left"]) * scale / width,
                    "height": (bounds["bottom"] - bounds["top"]) * scale / height}
        geometries.append(geometry)
        evidence.append({"binding": f"bpm_gra_{player}", "sourceBounds": copy.deepcopy(bounds),
                         "sourceBlend": draw["blend"], "sourceScale": scale})
    _require(all(all(math.isclose(geometries[0][key], item[key], rel_tol=0, abs_tol=1e-5) for key in geometries[0])
                 for item in geometries[1:]), "The two original beat glows require independent stage templates")
    return geometries[0], "additive" if draws[0]["blend"] == 8 else "alpha", evidence


def inject_frame(document, manifest, *, frame_id, source_map, gauge_dot_width_px=6):
    """Add a real-gauge front marker and an original BPM glow under the pooled notes.

    Frame layout scenes already provide the transparent baseplate marker. Its
    material-selected template is resolved onto each actual stage by the public
    renderer, for both rulesets; no ruleset-specific IDs or extra input lanes are
    declared. The marker goes inside the existing reveal clip, retaining all
    gauge modes, exact fill geometry and P2's right-to-left mask.
    """
    _require(document.get("contract") == SCENE_CONTRACT and manifest.get("contract") == MANIFEST_CONTRACT
             and document.get("root", {}).get("id") == f"omni.frame.{frame_id}.root", "Expected one exact Omni Frame contribution")
    namespace = frame_namespace(frame_id)
    owners = [(template["root"], re.fullmatch(re.escape(namespace) + r"\.template\.(p1|p2|dp)\.gauge", template["id"]))
              for template in document["templates"]]
    owners = [(owner, match[1]) for owner, match in owners if match]
    _require(len(owners) == 1, "Expected one selected Frame gauge layout")
    owner, layout = owners[0]
    prefix = f"{namespace}.{layout}"
    reveal = next((node for node in owner["children"] if node["id"] == prefix + ".gauge.reveal"), None)
    _require(reveal is not None and reveal.get("type") == "clip" and reveal["properties"].get("clip-mode") == "bounds",
             "The Frame gauge must retain its original reveal clip")
    fill = next((node for node in reveal["children"] if node["id"] == reveal["id"] + ".fill"), None)
    _require(fill is not None and fill.get("type") == "sprite", "The original full-width gauge fill is missing")
    _require(any(binding.get("target") == reveal["id"] and binding.get("property") == "reveal-x"
                 and binding.get("source") == "gauge.value" for binding in document["bindings"]), "Gauge front must share the real reveal value")
    _positive(gauge_dot_width_px, "Projected original gauge-dot width")
    glow_geometry, blend, source_evidence = _glow_geometry(frame_id, layout, source_map)
    originals = art_requirements(frame_id, source_map)
    resources = {role: item["resource"]["id"] for role, item in originals.items()}
    # Existing gauge geometry is in a 110-pixel stage HUD surface.
    dark = next(node for node in owner["children"] if node["id"] == prefix + ".gauge.dark")
    sd = frame_id in ("iidx27", "iidx28", "iidx29")
    full_bar_width = dark["properties"]["width"] * (438 if sd else 440)
    right_to_left = layout == "p2"
    front_id = reveal["id"] + ".motion-front"
    front = _node(front_id, "sprite", {"x": 0, "y": 0.5, "width": gauge_dot_width_px / full_bar_width,
                  "height": 1, "origin": "centre-left" if right_to_left else "centre-right",
                  "rotation": 180 if right_to_left else 0}, resource=resources["normal"])
    binding = {"id": front_id + ".position", "target": front_id, "property": "x", "source": "gauge.value"}
    blink = _track(front_id + ".blink", front_id, "opacity", "timing.time",
                   [(0, 0.2), (GAUGE_BLINK_PERIOD_MS / 2, 1), (GAUGE_BLINK_PERIOD_MS, 0.2)])
    variant = {"id": front_id + ".mode", "target": front_id, "property": "resource", "source": "gauge.type",
               "default": resources["normal"], "cases": [
                   {"id": front_id + ".mode.hard", "key": "hard", "resource": resources["hard"]},
                   {"id": front_id + ".mode.ex-hard", "key": "ex-hard", "resource": resources["ex-hard"]}]}
    glow_id = prefix + ".motion-beat"
    glow = _node(glow_id, "sprite", {**glow_geometry, "opacity": 0.35}, resource=resources["beat"],
                 slot="playfield.baseplate", blend=blend)
    template = {"id": glow_id + ".template", "root": glow}
    instance = {"id": glow_id + ".instance", "template": template["id"],
                "material": {"slot": "playfield.baseplate", "resource": f"gameplay/frames/{frame_id}/common/empty"}}
    pulse = _track(glow_id + ".pulse", glow_id, "opacity", "timing.beat", [(0, 0.35), (0.12, 0.28), (0.7, 0.02), (1, 0.35)])
    additions = [(reveal["children"], front, "gauge front"), (document["bindings"], binding, "gauge binding"),
                 (document["tracks"], blink, "gauge blink"), (document["variants"], variant, "gauge mode"),
                 (document["templates"], template, "underlay template"), (document["instances"], instance, "underlay instance"),
                 (document["tracks"], pulse, "beat track")]
    additions.extend((manifest["resources"], item["resource"], "original texture") for item in originals.values())
    # Check all identity conflicts before appending any declaration.
    for items, value, kind in additions:
        previous = [item for item in items if item.get("id") == value["id"]]
        _require(not previous or previous == [value], f"Existing {kind} differs from the Omni motion declaration")
    for items, value, kind in additions:
        _append_exact(items, value, kind)
    return {"frame": frame_id, "layout": layout, "beatSource": source_evidence,
            "beatGeometry": glow_geometry, "beatLayer": "playfield.baseplate / Underlay; native notes remain above",
            "pulse": "Author beat envelope from real timing.beat; original static AFP hook does not prove the native envelope",
            "gaugeFront": {"projectedWidthPx": gauge_dot_width_px, "fullBarWidthPx": full_bar_width,
                           "rightToLeft": right_to_left, "source": "gauge.value", "zeroHiddenByOriginalClip": True,
                           "blinkPeriodMs": GAUGE_BLINK_PERIOD_MS, "periodEvidence": "author adaptation"},
            "artwork": originals}
