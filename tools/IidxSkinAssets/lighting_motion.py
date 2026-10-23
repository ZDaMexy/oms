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
_FROZEN_TURN_IDLE = re.compile(
    r"(?P<knob>tt\.base\.(?P<layout>p1|p2|dp)\.(?P<frame>[a-z0-9-]+)\.n(?P<group>[01]))\.motion\.idle\Z")


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


def retime_turntable_idle(document, *, idle_period_ms=1000):
    """Adjust the four motors of one frozen frame's turntable contribution.

    One second per turn is an author approximation chosen from the independent
    reference recordings, not an exact native period or a BPM calibration.
    Historical generation keeps inject_turntables' original four-second default.
    """
    _require(document.get("contract") == SCENE_CONTRACT, "Expected an author scene")
    _require(not isinstance(idle_period_ms, bool) and idle_period_ms == 1000,
             "This author revision chooses the 1000ms idle approximation")
    tracks = document["tracks"]
    candidates = [(track, _FROZEN_TURN_IDLE.fullmatch(track.get("id", ""))) for track in tracks
                  if track.get("id", "").endswith(".motion.idle")]
    _require(len(candidates) == 4 and all(match for _, match in candidates),
             "Expected four frozen turntable idle tracks")
    frame_ids = {match["frame"] for _, match in candidates}
    _require(len(frame_ids) == 1 and frame_ids <= set(FRAMES)
             and {(match["layout"], int(match["group"])) for _, match in candidates}
             == {("p1", 0), ("p2", 0), ("dp", 0), ("dp", 1)},
             "Expected the complete 1P, 2P and DP motors of one frame")
    source_nodes = list(_nodes(document))
    nodes = {item["id"]: item for item in source_nodes}
    _require(len(nodes) == len(source_nodes), "Frozen turntable node IDs must be unique")
    idle_targets = {match["knob"] + ".motor" for _, match in candidates}
    _require(len([track for track in tracks if track.get("property") == "rotation"
                  and track.get("clock") == "timing.time"]) == 4,
             "The frozen contribution has an unexpected timed rotation")
    terminals = []
    for track, match in candidates:
        knob_id = match["knob"]
        motor, counts, knob = (nodes.get(knob_id + suffix) for suffix in (".motor", ".counts", ""))
        _require(motor is not None and counts is not None and knob is not None
                 and motor.get("type") == counts.get("type") == "container"
                 and motor.get("children") == [counts] and counts.get("children") == [knob]
                 and knob.get("type") in ("sprite", "projective-sprite") and knob.get("resource") is not None,
                 "A frozen turntable motor must retain its original input wrappers and sprite")
        keys = track.get("keyframes", [])
        _require(isinstance(keys, list) and len(keys) == 2 and all(isinstance(key, dict)
                 and all(type(key.get(field)) in (int, float) for field in ("time", "value")) for key in keys),
                 "A frozen idle track must contain two numeric keyframes")
        period = keys[-1]["time"]
        _require(period in (4000, 1000) and track.get("loop") is True
                 and track == _track(knob_id + ".motion.idle", knob_id + ".motor", "rotation", "timing.time",
                                    [(0, 0), (period, 360)]),
                 "A frozen idle track differs from the supported clockwise linear loop")
        _require(len([owner for owner in tracks if owner.get("target") == knob_id + ".motor"
                      and owner.get("property") == "rotation"]) == 1,
                 "Another track owns this turntable motor rotation")
        terminals.append(keys[-1])
    # Validate every motor first: a conflict in the last layout must not leave
    # the earlier layouts running at the new speed.
    previous = [key["time"] for key in terminals]
    for key in terminals:
        key["time"] = idle_period_ms
    return {"frame": next(iter(frame_ids)), "knobs": len(idle_targets),
            "retimedTracks": sum(value != idle_period_ms for value in previous),
            "previousPeriodsMs": sorted(set(previous)), "idlePeriodMs": idle_period_ms,
            "clock": "timing.time", "calibration": "author approximation; exact native idle period is unverified",
            "directionAndInputTracksPreserved": True}


def art_requirements(frame_id, source_map):
    """Describe originals the caller must verify and copy byte-for-byte, without doing I/O."""
    _require(frame_id in FRAMES, "Unknown Omni frame")
    glows = [draw for draw in source_map["layouts"]["1p_frame"]["draws"] if "bpm_gra_1p" in draw.get("game_bindings", [])]
    _require(len(glows) == 1 and glows[0]["texture"] == "playm_bpm_gra_bar", "No exact original beat-glow hook")
    asset = glows[0]["asset"]
    roles = {"beat": "playm_bpm_gra_bar"}
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


def _contains_value(value, expected):
    if isinstance(value, dict):
        return any(_contains_value(item, expected) for item in value.values())
    if isinstance(value, list):
        return any(_contains_value(item, expected) for item in value)
    return value == expected


def _replace_gauge_front(document, manifest, *, frame_id, layout, gauge_dot_width_px):
    """Mutate a private transaction copy; both long and compact v15 IDs are accepted."""
    _require(frame_id in FRAMES and layout in LAYOUTS, "Expected one known Frame and layout")
    _positive(gauge_dot_width_px, "Projected gauge-front width")
    owners = [template["root"] for template in document["templates"] if template["root"].get("slot") == "hud.gauge"]
    _require(len(owners) == 1, "Expected one selected Frame gauge layout")
    owner = owners[0]
    reveals = [node for node in owner["children"] if node.get("type") == "clip"
               and any(binding.get("target") == node["id"] and binding.get("property") == "reveal-x"
                       and binding.get("source") == "gauge.value" for binding in document["bindings"])]
    _require(len(reveals) == 1, "The Frame gauge must retain its real-value reveal clip")
    reveal = reveals[0]
    _require(reveal["properties"].get("clip-mode") == "bounds" and len(reveal["children"]) in (1, 2),
             "The gauge reveal has an unexpected mask or overlay")
    fill = reveal["children"][0]
    right_to_left = layout == "p2"
    expected_fill_geometry = {"x": 1, "y": 1, "rotation": 180} if right_to_left else {}
    _require(fill.get("type") == "sprite" and fill.get("resource") is not None
             and fill["properties"] == expected_fill_geometry and not fill["children"]
             and reveal["properties"].get("rotation", 0) == (180 if right_to_left else 0),
             "The original full-width gauge or P2 compensation differs")
    dark = owner["children"][0]
    _require(dark.get("type") == "sprite" and dark.get("resource") == fill["resource"]
             and dark["properties"].get("width") == reveal["properties"].get("width"),
             "The original full gauge geometry is missing")
    full_bar_width = dark["properties"]["width"] * (438 if frame_id in ("iidx27", "iidx28", "iidx29") else 440)
    _require(math.isfinite(full_bar_width) and 0 < gauge_dot_width_px <= full_bar_width,
             "The gauge front must fit the original full bar")
    fraction = gauge_dot_width_px / full_bar_width
    previous = reveal["children"][1] if len(reveal["children"]) == 2 else None
    front_id = previous["id"] if previous else reveal["id"] + ".motion-front"
    sample_id = front_id + ".sample"
    sample_fill_id = sample_id + ".fill"

    # reveal-x preserves the full-width child canvas. Both nested masks therefore
    # retain the source UV mapping while exposing just the actual frontier strip.
    sample_fill = copy.deepcopy(fill)
    sample_fill["id"] = sample_fill_id
    sample_fill["properties"] = {**expected_fill_geometry, "x": (1 if right_to_left else 0) + fraction,
                                  "colour": "#404040ff"}
    sample = _node(sample_id, "clip", {"x": -fraction, "clip-mode": "bounds", "reveal-x": fraction}, children=[sample_fill])
    front = _node(front_id, children=[sample])
    # Identical opaque art alpha-blended over itself does not blink. A neutral
    # luminance pulse visibly modulates every palette, including saturated yellow.
    # The original fill, source pixels, mode variants and gameplay values stay intact.
    position = _track(sample_fill_id + ".position", sample_fill_id, "x", "gauge.value",
                      [(0, sample_fill["properties"]["x"]), (1, sample_fill["properties"]["x"] - 1)])
    position["loop"] = False
    binding = {"id": front_id + ".position", "target": front_id, "property": "x", "source": "gauge.value"}
    blink = _track(front_id + ".blink", front_id, "opacity", "timing.time",
                   [(0, 0.2), (GAUGE_BLINK_PERIOD_MS / 2, 1), (GAUGE_BLINK_PERIOD_MS, 0.2)])
    old_resources = set()
    old_variants = []
    if previous is not None:
        bindings = [item for item in document["bindings"] if item.get("target") == front_id]
        _require(len(bindings) == 1 and {key: value for key, value in bindings[0].items() if key != "id"}
                 == {key: value for key, value in binding.items() if key != "id"}, "The old frontier position binding differs")
        binding = bindings[0]
        blinks = [item for item in document["tracks"] if item.get("target") == front_id]
        _require(len(blinks) == 1 and {key: value for key, value in blinks[0].items() if key not in ("id", "keyframes")}
                 == {key: value for key, value in blink.items() if key not in ("id", "keyframes")}
                 and [(item["time"], item["value"]) for item in blinks[0]["keyframes"]]
                 == [(item["time"], item["value"]) for item in blink["keyframes"]], "The old frontier blink differs")
        blink = blinks[0]
        if previous.get("type") == "sprite":
            old_width = previous["properties"].get("width")
            _require(isinstance(old_width, (int, float)) and not isinstance(old_width, bool)
                     and any(math.isclose(old_width * full_bar_width, width, rel_tol=0, abs_tol=1e-6) for width in (6, 8))
                     and previous["properties"] == {"x": 0, "y": 0.5, "width": old_width, "height": 1,
                      "origin": "centre-left" if right_to_left else "centre-right", "rotation": 180 if right_to_left else 0},
                     "The legacy frontier geometry differs")
            old_variants = [item for item in document["variants"] if item.get("target") == front_id]
            _require(len(old_variants) == 1 and old_variants[0].get("property") == "resource"
                     and old_variants[0].get("source") == "gauge.type"
                     and old_variants[0].get("default") == previous.get("resource")
                     and [case["key"] for case in old_variants[0]["cases"]] == ["hard", "ex-hard"],
                     "The legacy frontier resources differ")
            old_resources = {previous["resource"], *(case["resource"] for case in old_variants[0]["cases"])}
        else:
            _require(previous == front, "An existing sampled frontier differs")

    fill_variants = [variant for variant in document["variants"] if variant.get("target") == fill["id"]]
    _require(len(fill_variants) <= 1 and all(item.get("property") == "resource" and item.get("source") == "gauge.type"
             for item in fill_variants), "The source fill has an unexpected dynamic property")
    variants = []
    for variant in fill_variants:
        value = copy.deepcopy(variant)
        value.update({"id": sample_fill_id + ".mode", "target": sample_fill_id})
        for index, case in enumerate(value["cases"]):
            case["id"] = value["id"] + f".c{index}"
        variants.append(value)
    reveal["children"] = [fill, front]
    document["variants"] = [item for item in document["variants"] if item not in old_variants]
    additions = [(document["bindings"], binding, "gauge position"), (document["tracks"], blink, "gauge blink"),
                 (document["tracks"], position, "source sample position")]
    additions.extend((document["variants"], value, "source fill mode") for value in variants)
    for items, value, kind in additions:
        _append_exact(items, value, kind)
    resources = manifest["resources"]
    candidates = [resource for resource in resources if resource["id"] in old_resources]
    _require(len(candidates) == len(old_resources), "A legacy frontier texture declaration is missing")
    other_declarations = {**manifest, "resources": [resource for resource in resources if resource["id"] not in old_resources]}
    removed = [resource["id"] for resource in candidates
               if not _contains_value(document, resource["id"]) and not _contains_value(other_declarations, resource["id"])]
    manifest["resources"] = [resource for resource in resources if resource["id"] not in removed]
    return {"projectedWidthPx": gauge_dot_width_px, "fullBarWidthPx": full_bar_width, "rightToLeft": right_to_left,
            "source": "gauge.value", "zeroHiddenByOriginalClip": True, "palette": "exact current full-fill resource and mode variants",
            "blinkPeriodMs": GAUGE_BLINK_PERIOD_MS, "periodEvidence": "author adaptation", "luminance": "neutral grey modulation",
            "migratedLegacyDot": bool(old_resources), "removedResources": removed}


def replace_gauge_front(document, manifest, *, frame_id, layout, gauge_dot_width_px=6):
    """Upgrade one selected Frame contribution, including a compact v15 export.

    Original fill/dark/percentage, unrelated nodes and source images are retained.
    Only the previous fixed-dot frontier and now-unused texture declarations are
    retired. The caller rebuilds its normal reference closure to omit unused PNGs.
    This function is idempotent and commits neither dictionary on a conflict.
    """
    _require(document.get("contract") == SCENE_CONTRACT and manifest.get("contract") == MANIFEST_CONTRACT,
             "Expected an author scene and manifest")
    scene_copy, manifest_copy = copy.deepcopy((document, manifest))
    receipt = _replace_gauge_front(scene_copy, manifest_copy, frame_id=frame_id, layout=layout, gauge_dot_width_px=gauge_dot_width_px)
    document.clear()
    document.update(scene_copy)
    manifest.clear()
    manifest.update(manifest_copy)
    return receipt


def inject_frame(document, manifest, *, frame_id, source_map, gauge_dot_width_px=6):
    """Add a palette-matched blinking frontier and original BPM glow beneath notes."""
    _require(document.get("contract") == SCENE_CONTRACT and manifest.get("contract") == MANIFEST_CONTRACT
             and document.get("root", {}).get("id") == f"omni.frame.{frame_id}.root", "Expected one exact Omni Frame contribution")
    original_document, original_manifest = document, manifest
    document, manifest = copy.deepcopy((document, manifest))
    namespace = frame_namespace(frame_id)
    owners = [match[1] for template in document["templates"]
              if (match := re.fullmatch(re.escape(namespace) + r"\.template\.(p1|p2|dp)\.gauge", template["id"]))]
    _require(len(owners) == 1, "Expected one selected Frame gauge layout")
    layout = owners[0]
    gauge = _replace_gauge_front(document, manifest, frame_id=frame_id, layout=layout, gauge_dot_width_px=gauge_dot_width_px)
    prefix = f"{namespace}.{layout}"
    glow_geometry, blend, source_evidence = _glow_geometry(frame_id, layout, source_map)
    originals = art_requirements(frame_id, source_map)
    resources = {role: item["resource"]["id"] for role, item in originals.items()}
    glow_id = prefix + ".motion-beat"
    glow = _node(glow_id, "sprite", {**glow_geometry, "opacity": 0.35}, resource=resources["beat"],
                 slot="playfield.baseplate", blend=blend)
    template = {"id": glow_id + ".template", "root": glow}
    instance = {"id": glow_id + ".instance", "template": template["id"],
                "material": {"slot": "playfield.baseplate", "resource": f"gameplay/frames/{frame_id}/common/empty"}}
    pulse = _track(glow_id + ".pulse", glow_id, "opacity", "timing.beat", [(0, 0.35), (0.12, 0.28), (0.7, 0.02), (1, 0.35)])
    additions = [(document["templates"], template, "underlay template"), (document["instances"], instance, "underlay instance"),
                 (document["tracks"], pulse, "beat track")]
    additions.extend((manifest["resources"], item["resource"], "original texture") for item in originals.values())
    for items, value, kind in additions:
        _append_exact(items, value, kind)
    original_document.clear()
    original_document.update(document)
    original_manifest.clear()
    original_manifest.update(manifest)
    return {"frame": frame_id, "layout": layout, "beatSource": source_evidence,
            "beatGeometry": glow_geometry, "beatLayer": "playfield.baseplate / Underlay; native notes remain above",
            "pulse": "Author beat envelope from real timing.beat; original static AFP hook does not prove the native envelope",
            "gaugeFront": gauge,
            "artwork": originals}
