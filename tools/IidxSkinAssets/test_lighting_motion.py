"""Synthetic author-contract checks; original IIDX artwork is not needed."""

import copy
import unittest

import lighting_motion as motion


def node(identifier, kind="container", properties=None, children=None, resource=None, slot=None):
    result = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "alpha",
              "properties": properties or {}, "effects": [], "children": children or []}
    if resource:
        result["resource"] = resource
    if slot:
        result["slot"] = slot
    return result


def turntables():
    scene = {"contract": motion.SCENE_CONTRACT, "root": node("t.t00.base.root"), "tracks": [], "templates": []}
    for layout in motion.LAYOUTS:
        frames = []
        for frame in motion.FRAMES:
            knobs = []
            for group in range(2 if layout == "dp" else 1):
                identifier = f"t.t00.base.{layout}.{frame}.n{group}"
                mirrored = layout == "p2" or group == 1
                knob = node(identifier, "projective-sprite" if mirrored else "sprite",
                            {"x": 0.05 + group * 0.6, "y": 0.7, "width": 0.025, "height": 0.045}, resource="original.knob")
                knobs.append(knob)
                if mirrored:
                    scene["tracks"].append({"id": identifier + ".orientation", "type": "frame", "target": identifier,
                        "property": "projective-quad", "easing": "step", "loop": False, "keyframes": [
                            {"id": identifier + ".fixed", "time": 0, "value": {
                                "vertices": [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]], "opacity": 1}}]})
            frames.append(node(f"t.t00.base.{layout}.{frame}", properties={"visible": False}, children=knobs))
        scene["templates"].append({"id": f"t.t00.base.t.{layout}", "root": node(f"t.t00.base.{layout}", children=frames)})
    return scene


def frozen_turntables(frame="iidx33"):
    scene = turntables()
    motion.inject_turntables(scene)
    for template in scene["templates"]:
        template["root"]["children"] = [child for child in template["root"]["children"]
                                         if child["id"].endswith("." + frame)]
    retained = {item["id"] for item in motion._nodes(scene)}
    scene["tracks"] = [track for track in scene["tracks"] if track["target"] in retained]

    def freeze_names(value):
        if isinstance(value, dict):
            return {key: freeze_names(item) for key, item in value.items()}
        if isinstance(value, list):
            return [freeze_names(item) for item in value]
        if isinstance(value, str):
            return value.replace("t.t00.base", "tt.base")
        return value

    return freeze_names(scene)


def source_map(frame="iidx33"):
    sd = frame in ("iidx27", "iidx28", "iidx29")
    scale = 1.5 if sd else 1
    width, height = (438, 726) if sd else (440, 728)
    result = {"stage": {"width": 1920 / scale, "height": 1080 / scale}, "layouts": {}}
    for layout, movie in (("p1", "1p_frame"), ("p2", "2p_frame"), ("dp", "dp_frame_1p")):
        draws = []
        for group in range(2 if layout == "dp" else 1):
            left = ((456 if group == 0 else 1026) if sd else (455 if group == 0 else 1025)) if layout == "dp" else (
                (1410 if layout == "p2" else 72) if sd else (1409 if layout == "p2" else 71))
            player = "2p" if layout == "p2" or group == 1 else "1p"
            draws.append({"texture": "playm_bpm_gra_bar", "game_bindings": [f"bpm_gra_{player}"],
                "asset": {"package": "0/gameparts" if sd else "1/gameparts", "name": "playm_bpm_gra_bar",
                          "root": "legacy" if sd else "primary"}, "blend": 0,
                "mult_color": {"r": 1, "g": 1, "b": 1, "a": 1}, "add_color": {"r": 0, "g": 0, "b": 0, "a": 0},
                "bounds": {"left": (left + 4) / scale, "top": (height - 44) / scale,
                           "right": (left + width - 4) / scale, "bottom": (height - 5) / scale}})
        result["layouts"][movie] = {"draws": draws}
    return result


def frame_scene(layout, frame="iidx33"):
    namespace = frame.replace("bpl-", "bpl.", 1)
    prefix = f"{namespace}.{layout}.gauge"
    geometry = {"x": -0.04, "y": 0.38, "width": 1.02, "height": 30 / 110}
    reveal_geometry = {**geometry, "clip-mode": "bounds", "reveal-x": 0}
    fill_geometry = {}
    if layout == "p2":
        reveal_geometry.update({"x": 0.98, "y": 0.38 + 30 / 110, "rotation": 180})
        fill_geometry = {"x": 1, "y": 1, "rotation": 180}
    reveal = node(prefix + ".reveal", "clip", reveal_geometry,
                  children=[node(prefix + ".reveal.fill", "sprite", fill_geometry, resource="original.gauge")])
    owner = node(prefix, children=[node(prefix + ".dark", "sprite", geometry, resource="original.gauge"), reveal], slot="hud.gauge")
    modes = {"hard": "original.hard", "ex-hard": "original.ex-hard", "hazard": "original.hazard"}
    scene = {"contract": motion.SCENE_CONTRACT, "root": node(f"omni.frame.{frame}.root"), "tracks": [], "variants": [
                 {"id": prefix + ".modes", "target": reveal["children"][0]["id"], "property": "resource", "source": "gauge.type",
                  "default": "original.gauge", "cases": [{"id": prefix + f".mode.c{i}", "key": key, "resource": resource}
                                                         for i, (key, resource) in enumerate(modes.items())]}],
             "bindings": [{"id": prefix + ".value", "target": reveal["id"], "property": "reveal-x", "source": "gauge.value"}],
             "templates": [{"id": f"{namespace}.template.{layout}.gauge", "root": owner}], "instances": []}
    manifest = {"contract": motion.MANIFEST_CONTRACT, "resources": [
        {"id": identifier, "type": "texture", "path": f"art/{identifier}.png", "filter": "nearest"}
        for identifier in ["original.gauge", *modes.values()]]}
    return scene, manifest


def legacy_front(scene, manifest, layout, frame="iidx33", width_px=6):
    reveal = scene["templates"][0]["root"]["children"][1]
    front_id = reveal["id"] + ".motion-front"
    width = reveal["properties"]["width"] * (438 if frame in ("iidx27", "iidx28", "iidx29") else 440)
    reveal["children"].append(node(front_id, "sprite", {"x": 0, "y": 0.5, "width": width_px / width, "height": 1,
                                  "origin": "centre-left" if layout == "p2" else "centre-right",
                                  "rotation": 180 if layout == "p2" else 0}, resource="old.dot.normal"))
    scene["bindings"].append({"id": front_id + ".position", "target": front_id, "property": "x", "source": "gauge.value"})
    scene["tracks"].append(motion._track(front_id + ".blink", front_id, "opacity", "timing.time", [(0, 0.2), (200, 1), (400, 0.2)]))
    scene["variants"].append({"id": front_id + ".mode", "target": front_id, "property": "resource", "source": "gauge.type",
                             "default": "old.dot.normal", "cases": [
                                 {"id": front_id + ".mode.hard", "key": "hard", "resource": "old.dot.hard"},
                                 {"id": front_id + ".mode.ex-hard", "key": "ex-hard", "resource": "old.dot.ex-hard"}]})
    manifest["resources"].extend({"id": f"old.dot.{name}", "type": "texture", "path": f"art/motion/{frame}/{name}.png", "filter": "linear"}
                                 for name in ("normal", "hard", "ex-hard"))


class LightingMotionTest(unittest.TestCase):
    def test_all_original_knobs_keep_their_centres_materials_and_mirror_quads(self):
        scene = turntables()
        before = copy.deepcopy(scene)
        receipt = motion.inject_turntables(scene)
        self.assertEqual(60, receipt["knobs"])
        self.assertEqual(120, receipt["newWrappers"])
        nodes = {item["id"]: item for item in motion._nodes(scene)}
        for original in motion._nodes(before):
            if not motion._TURN.fullmatch(original["id"]):
                continue
            knob = nodes[original["id"]]
            motor = nodes[original["id"] + ".motor"]
            self.assertEqual(original["resource"], knob["resource"])
            self.assertEqual(original["type"], knob["type"])
            self.assertEqual(original["properties"]["x"] + original["properties"]["width"] / 2, motor["properties"]["x"])
            self.assertEqual(original["properties"]["y"] + original["properties"]["height"] / 2, motor["properties"]["y"])
            self.assertEqual("centre", knob["properties"]["origin"])
        self.assertEqual(before["tracks"], [track for track in scene["tracks"] if track["property"] == "projective-quad"])

    def test_single_player_p2_uses_first_logical_input_and_dp_keeps_separate_units(self):
        scene = turntables()
        motion.inject_turntables(scene)
        tracks = {track["id"]: track for track in scene["tracks"]}
        prefix = "t.t00.base."
        for layout, group, signal in (("p1", 0, 1), ("p2", 0, 1), ("dp", 0, 1), ("dp", 1, 2)):
            name = f"{prefix}{layout}.iidx33.n{group}"
            self.assertEqual(f"input.motion-counts-{signal}", tracks[name + ".motion.counts"]["clock"])
            self.assertEqual(f"input.motion-revolutions-{signal}", tracks[name + ".motion.revolutions"]["clock"])
            self.assertEqual(360, tracks[name + ".motion.counts"]["keyframes"][-1]["time"])
            self.assertEqual(1, tracks[name + ".motion.revolutions"]["keyframes"][-1]["time"])
            self.assertEqual("timing.time", tracks[name + ".motion.idle"]["clock"])

    def test_turntable_injection_is_idempotent_and_conflicts_fail_before_mutation(self):
        scene = turntables()
        motion.inject_turntables(scene)
        before = copy.deepcopy(scene)
        receipt = motion.inject_turntables(scene)
        self.assertEqual(0, receipt["newWrappers"])
        self.assertEqual(before, scene)
        with self.assertRaises(ValueError):
            motion.inject_turntables(scene, counts_per_turn=180)
        self.assertEqual(before, scene)

    def test_incomplete_turntable_contribution_fails_without_changes(self):
        scene = turntables()
        scene["templates"][2]["root"]["children"].pop()
        before = copy.deepcopy(scene)
        with self.assertRaises(ValueError):
            motion.inject_turntables(scene)
        self.assertEqual(before, scene)

    def test_frozen_turntable_retime_changes_only_idle_duration_for_every_frame(self):
        for frame in motion.FRAMES:
            with self.subTest(frame=frame):
                scene = frozen_turntables(frame)
                before = copy.deepcopy(scene)
                receipt = motion.retime_turntable_idle(scene)
                self.assertEqual(frame, receipt["frame"])
                self.assertEqual(4, receipt["knobs"])
                self.assertEqual(4, receipt["retimedTracks"])
                self.assertEqual([4000], receipt["previousPeriodsMs"])
                self.assertEqual(1000, receipt["idlePeriodMs"])
                self.assertEqual("timing.time", receipt["clock"])
                self.assertIn("author approximation", receipt["calibration"])
                expected = copy.deepcopy(before)
                for track in expected["tracks"]:
                    if track["id"].endswith(".motion.idle"):
                        track["keyframes"][-1]["time"] = 1000
                # This includes the original coordinates, resources, mirrored
                # quads and separate1P/2P input-count/revolution channels.
                self.assertEqual(expected, scene)
        self.assertEqual(4000, motion.IDLE_PERIOD_MS)
        historical = turntables()
        motion.inject_turntables(historical)
        self.assertEqual({4000}, {track["keyframes"][-1]["time"] for track in historical["tracks"]
                                 if track["id"].endswith(".motion.idle")})

    def test_frozen_turntable_retime_is_idempotent_and_accepts_already_retimed_motors(self):
        scene = frozen_turntables()
        next(track for track in scene["tracks"] if track["id"].endswith(".motion.idle"))["keyframes"][-1]["time"] = 1000
        receipt = motion.retime_turntable_idle(scene, idle_period_ms=1000)
        self.assertEqual(3, receipt["retimedTracks"])
        self.assertEqual([1000, 4000], receipt["previousPeriodsMs"])
        before = copy.deepcopy(scene)
        receipt = motion.retime_turntable_idle(scene)
        self.assertEqual(0, receipt["retimedTracks"])
        self.assertEqual([1000], receipt["previousPeriodsMs"])
        self.assertEqual(before, scene)

    def test_frozen_turntable_retime_conflicts_reject_the_whole_contribution(self):
        for conflict in ("period", "clock", "direction", "start", "start-value", "numeric-boolean", "loop", "easing",
                         "target", "third-keyframe", "duplicate", "missing", "frame", "rotation-owner", "motor"):
            with self.subTest(conflict=conflict):
                scene = frozen_turntables()
                idle = [track for track in scene["tracks"] if track["id"].endswith(".motion.idle")]
                last = idle[-1]
                if conflict == "period":
                    last["keyframes"][-1]["time"] = 2000
                elif conflict == "clock":
                    last["clock"] = "timing.beat"
                elif conflict == "direction":
                    last["keyframes"][-1]["value"] = -360
                elif conflict == "start":
                    last["keyframes"][0]["time"] = 1
                elif conflict == "start-value":
                    last["keyframes"][0]["value"] = 1
                elif conflict == "numeric-boolean":
                    last["keyframes"][0]["value"] = False
                elif conflict == "loop":
                    last["loop"] = False
                elif conflict == "easing":
                    last["easing"] = "ease-in"
                elif conflict == "target":
                    last["target"] = last["target"].removesuffix(".motor") + ".counts"
                elif conflict == "third-keyframe":
                    last["keyframes"].append({"id": "extra", "time": 5000, "value": 360})
                elif conflict == "duplicate":
                    scene["tracks"].append(copy.deepcopy(last))
                elif conflict == "missing":
                    scene["tracks"].remove(last)
                elif conflict == "frame":
                    last["id"] = last["id"].replace("iidx33", "iidx32")
                elif conflict == "rotation-owner":
                    scene["tracks"].append(motion._track("another-rotation", last["target"], "rotation",
                                                        "input.motion-counts-2", [(0, 0), (360, 360)]))
                elif conflict == "motor":
                    parent = next(item for item in motion._nodes(scene)
                                  if any(child["id"] == last["target"] for child in item["children"]))
                    parent["children"] = [child for child in parent["children"] if child["id"] != last["target"]]
                before = copy.deepcopy(scene)
                with self.assertRaises(ValueError):
                    motion.retime_turntable_idle(scene)
                self.assertEqual(before, scene)

    def test_frozen_turntable_retime_rejects_other_scopes_and_unselected_periods(self):
        complete = turntables()
        motion.inject_turntables(complete)
        before = copy.deepcopy(complete)
        with self.assertRaises(ValueError):
            motion.retime_turntable_idle(complete)
        self.assertEqual(before, complete)
        for period in (4000, 2000, 0, True, float("nan")):
            with self.subTest(period=period):
                scene = frozen_turntables()
                before = copy.deepcopy(scene)
                with self.assertRaises(ValueError):
                    motion.retime_turntable_idle(scene, idle_period_ms=period)
                self.assertEqual(before, scene)

    def test_gauge_front_follows_original_clip_and_beat_is_beneath_notes_for_every_layout(self):
        for layout in motion.LAYOUTS:
            with self.subTest(layout=layout):
                scene, manifest = frame_scene(layout)
                source = source_map()
                before = copy.deepcopy(scene)
                receipt = motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source)
                reveal = scene["templates"][0]["root"]["children"][1]
                self.assertEqual(before["templates"][0]["root"]["children"][1]["properties"], reveal["properties"])
                self.assertEqual(before["templates"][0]["root"]["children"][1]["children"][0], reveal["children"][0])
                front = reveal["children"][1]
                self.assertEqual("gauge.value", next(item["source"] for item in scene["bindings"] if item["target"] == front["id"]))
                sample = front["children"][0]
                self.assertAlmostEqual(6, receipt["gaugeFront"]["fullBarWidthPx"] * sample["properties"]["reveal-x"])
                self.assertEqual("original.gauge", sample["children"][0]["resource"])
                self.assertEqual(180 if layout == "p2" else 0, sample["children"][0]["properties"].get("rotation", 0))
                glow = scene["templates"][1]["root"]
                self.assertEqual("playfield.baseplate", glow["slot"])
                self.assertEqual("alpha", glow["blend"])
                self.assertEqual("gameplay/frames/iidx33/common/empty", scene["instances"][0]["material"]["resource"])
                self.assertEqual("timing.beat", scene["tracks"][-1]["clock"])
                self.assertEqual(len(before["variants"]) + 1, len(scene["variants"]))
                self.assertEqual({"beat"}, receipt["artwork"].keys())
                self.assertEqual(5, len(manifest["resources"]))

    def test_gauge_front_stays_inside_the_actual_fill_for_zero_partial_and_full(self):
        for layout in motion.LAYOUTS:
            scene, manifest = frame_scene(layout)
            receipt = motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
            width = receipt["gaugeFront"]["fullBarWidthPx"]
            owner = scene["templates"][0]["root"]
            dark, reveal = owner["children"]
            clip = reveal["properties"]
            marker = reveal["children"][1]
            sample = marker["children"][0]["properties"]
            sample_fill = marker["children"][0]["children"][0]
            position = next(track for track in scene["tracks"] if track["target"] == sample_fill["id"])
            dark_left = dark["properties"]["x"] * 440
            clip_sign = -1 if clip.get("rotation", 0) == 180 else 1
            for gauge in (0, 0.005, 0.28, 0.78, 0.8, 0.82, 1):
                corners = [clip["x"] * 440 + clip_sign * (gauge + sample["x"] + x) * width
                           for x in (0, sample["reveal-x"])]
                mask_corners = [clip["x"] * 440, clip["x"] * 440 + clip_sign * width * gauge]
                visible_left = max(min(corners), min(mask_corners))
                visible_right = min(max(corners), max(mask_corners))
                self.assertAlmostEqual(min(gauge * width, 6), max(0, visible_right - visible_left))
                if gauge * width >= 6:
                    expected_left = dark_left + width * (1 - gauge) if layout == "p2" else dark_left + width * gauge - 6
                    self.assertAlmostEqual(expected_left, visible_left)
                sample_x = position["keyframes"][0]["value"] * (1 - gauge) + position["keyframes"][1]["value"] * gauge
                # The duplicate source sprite has exactly the original world transform.
                self.assertAlmostEqual(1 if layout == "p2" else 0, gauge + sample["x"] + sample_x)
                self.assertFalse(position["loop"])

    def test_sd_source_is_projected_once_and_uses_its_own_originals(self):
        scene, manifest = frame_scene("p1", "iidx27")
        receipt = motion.inject_frame(scene, manifest, frame_id="iidx27", source_map=source_map("iidx27"))
        self.assertAlmostEqual(4 / 438, receipt["beatGeometry"]["x"])
        self.assertAlmostEqual(39 / 726, receipt["beatGeometry"]["height"])
        self.assertEqual("legacy", receipt["artwork"]["beat"]["source"]["root"])
        self.assertEqual("0/gameparts", receipt["artwork"]["beat"]["source"]["package"])

    def test_all_bpl_node_namespaces_keep_catalog_roots_and_asset_paths(self):
        for frame in (identifier for identifier in motion.FRAMES if identifier.startswith("bpl-")):
            for layout in motion.LAYOUTS:
                with self.subTest(frame=frame, layout=layout):
                    scene, manifest = frame_scene(layout, frame)
                    receipt = motion.inject_frame(scene, manifest, frame_id=frame, source_map=source_map(frame))
                    namespace = frame.replace("bpl-", "bpl.", 1)
                    self.assertEqual(f"omni.frame.{frame}.root", scene["root"]["id"])
                    self.assertEqual(f"{namespace}.{layout}.motion-beat", scene["templates"][-1]["root"]["id"])
                    self.assertEqual(f"gameplay/frames/{frame}/common/empty", scene["instances"][-1]["material"]["resource"])
                    self.assertEqual(frame, receipt["frame"])
                    self.assertTrue(manifest["resources"][-1]["path"].startswith(f"art/motion/{frame}/"))
                    before = copy.deepcopy((scene, manifest))
                    motion.inject_frame(scene, manifest, frame_id=frame, source_map=source_map(frame))
                    self.assertEqual(before, (scene, manifest))

    def test_frame_injection_is_idempotent_and_conflicts_do_not_partially_change_it(self):
        scene, manifest = frame_scene("p1")
        motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
        before = copy.deepcopy((scene, manifest))
        motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
        self.assertEqual(before, (scene, manifest))
        manifest["resources"][-1]["path"] = "different.png"
        before = copy.deepcopy((scene, manifest))
        with self.assertRaises(ValueError):
            motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
        self.assertEqual(before, (scene, manifest))

    def test_frontier_matches_every_gauge_mode_and_unknown_type_default(self):
        for frame in motion.FRAMES:
            for layout in motion.LAYOUTS:
                with self.subTest(frame=frame, layout=layout):
                    scene, manifest = frame_scene(layout, frame)
                    fill = copy.deepcopy(scene["templates"][0]["root"]["children"][1]["children"][0])
                    source_modes = copy.deepcopy(scene["variants"][0])
                    motion.inject_frame(scene, manifest, frame_id=frame, source_map=source_map(frame))
                    sample = scene["templates"][0]["root"]["children"][1]["children"][1]["children"][0]["children"][0]
                    sample_modes = next(item for item in scene["variants"] if item["target"] == sample["id"])
                    for mode in ("none", "assist-easy", "easy", "normal", "hard", "ex-hard", "hazard"):
                        selected = lambda variant: next((case["resource"] for case in variant["cases"] if case["key"] == mode), variant["default"])
                        self.assertEqual(selected(source_modes), selected(sample_modes))
                    self.assertEqual(fill, scene["templates"][0]["root"]["children"][1]["children"][0])
                    self.assertEqual(source_modes, scene["variants"][0])
                    self.assertEqual("#404040ff", sample["properties"]["colour"])

    def test_legacy_dot_upgrade_removes_unused_resources_and_preserves_compact_ids(self):
        for frame in motion.FRAMES:
            for layout in motion.LAYOUTS:
                with self.subTest(frame=frame, layout=layout):
                    scene, manifest = frame_scene(layout, frame)
                    original = copy.deepcopy((scene, manifest))
                    legacy_front(scene, manifest, layout, frame, width_px=8)
                    # v15 compaction changes all node/track/variant IDs, not semantics.
                    identifiers = []
                    def gather(value):
                        if isinstance(value, dict):
                            if "id" in value:
                                identifiers.append(value["id"])
                            for child in value.values():
                                gather(child)
                        elif isinstance(value, list):
                            for child in value:
                                gather(child)
                    gather(scene)
                    replacements = {value: f"c{index}" for index, value in enumerate(identifiers)}
                    def compact(value):
                        if isinstance(value, dict):
                            return {key: compact(child) for key, child in value.items()}
                        if isinstance(value, list):
                            return [compact(child) for child in value]
                        return replacements.get(value, value) if isinstance(value, str) else value
                    scene = compact(scene)
                    before_fill = copy.deepcopy(scene["templates"][0]["root"]["children"][1]["children"][0])
                    receipt = motion.replace_gauge_front(scene, manifest, frame_id=frame, layout=layout)
                    self.assertTrue(receipt["migratedLegacyDot"])
                    self.assertEqual({"old.dot.normal", "old.dot.hard", "old.dot.ex-hard"}, set(receipt["removedResources"]))
                    self.assertEqual(original[1], manifest)
                    self.assertEqual(before_fill, scene["templates"][0]["root"]["children"][1]["children"][0])
                    before = copy.deepcopy((scene, manifest))
                    receipt = motion.replace_gauge_front(scene, manifest, frame_id=frame, layout=layout)
                    self.assertFalse(receipt["migratedLegacyDot"])
                    self.assertEqual(before, (scene, manifest))

    def test_legacy_upgrade_retains_a_dot_resource_still_used_elsewhere(self):
        scene, manifest = frame_scene("p1")
        legacy_front(scene, manifest, "p1")
        scene["root"]["children"].append(node("independent", "sprite", resource="old.dot.normal"))
        receipt = motion.replace_gauge_front(scene, manifest, frame_id="iidx33", layout="p1")
        self.assertEqual({"old.dot.hard", "old.dot.ex-hard"}, set(receipt["removedResources"]))
        self.assertIn("old.dot.normal", [resource["id"] for resource in manifest["resources"]])

    def test_legacy_eight_pixel_marker_can_keep_its_existing_width(self):
        scene, manifest = frame_scene("p2")
        legacy_front(scene, manifest, "p2", width_px=8)
        receipt = motion.replace_gauge_front(scene, manifest, frame_id="iidx33", layout="p2", gauge_dot_width_px=8)
        self.assertEqual(8, receipt["projectedWidthPx"])
        sample = scene["templates"][0]["root"]["children"][1]["children"][1]["children"][0]
        self.assertAlmostEqual(8, sample["properties"]["reveal-x"] * receipt["fullBarWidthPx"])

    def test_legacy_upgrade_rejects_changed_front_or_missing_art_without_partial_mutation(self):
        for defect in ("geometry", "blink", "resource", "mode"):
            with self.subTest(defect=defect):
                scene, manifest = frame_scene("p1")
                legacy_front(scene, manifest, "p1")
                if defect == "geometry":
                    scene["templates"][0]["root"]["children"][1]["children"][1]["properties"]["rotation"] = 1
                elif defect == "blink":
                    scene["tracks"][0]["keyframes"][0]["value"] = 0.3
                elif defect == "resource":
                    manifest["resources"].pop()
                else:
                    scene["variants"][-1]["cases"].pop()
                before = copy.deepcopy((scene, manifest))
                with self.assertRaises(ValueError):
                    motion.replace_gauge_front(scene, manifest, frame_id="iidx33", layout="p1")
                self.assertEqual(before, (scene, manifest))

    def test_neutral_luminance_pulse_blinks_saturated_palettes_without_changing_hue(self):
        for colour in ((0, 255, 255), (255, 0, 0), (255, 255, 0), (0, 199, 255)):
            def composite(alpha):
                return tuple(value * ((1 - alpha) + alpha * 64 / 255) for value in colour)
            self.assertNotEqual(composite(0.2), composite(1))
            for pixel in (composite(0.2), composite(1)):
                self.assertEqual([index for index, value in enumerate(colour) if value == 0],
                                 [index for index, value in enumerate(pixel) if value == 0])

    def test_unproven_or_asymmetric_source_glow_is_not_silently_replaced(self):
        for defect in ("missing", "tinted", "different-second-stage"):
            with self.subTest(defect=defect):
                scene, manifest = frame_scene("dp")
                source = source_map()
                draws = source["layouts"]["dp_frame_1p"]["draws"]
                if defect == "missing":
                    draws.pop()
                elif defect == "tinted":
                    draws[0]["mult_color"]["r"] = 0.5
                else:
                    draws[1]["bounds"]["top"] += 3
                before = copy.deepcopy((scene, manifest))
                with self.assertRaises(ValueError):
                    motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source)
                self.assertEqual(before, (scene, manifest))

if __name__ == "__main__":
    unittest.main()
