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
    scene = {"contract": motion.SCENE_CONTRACT, "root": node(f"omni.frame.{frame}.root"), "tracks": [], "variants": [],
             "bindings": [{"id": prefix + ".value", "target": reveal["id"], "property": "reveal-x", "source": "gauge.value"}],
             "templates": [{"id": f"{namespace}.template.{layout}.gauge", "root": owner}], "instances": []}
    manifest = {"contract": motion.MANIFEST_CONTRACT, "resources": []}
    return scene, manifest


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
                self.assertEqual(180 if layout == "p2" else 0, front["properties"]["rotation"])
                self.assertAlmostEqual(6, receipt["gaugeFront"]["fullBarWidthPx"] * front["properties"]["width"])
                glow = scene["templates"][1]["root"]
                self.assertEqual("playfield.baseplate", glow["slot"])
                self.assertEqual("alpha", glow["blend"])
                self.assertEqual("gameplay/frames/iidx33/common/empty", scene["instances"][0]["material"]["resource"])
                self.assertEqual("timing.beat", scene["tracks"][-1]["clock"])
                self.assertEqual(4, len(manifest["resources"]))

    def test_gauge_front_stays_inside_the_actual_fill_for_zero_partial_and_full(self):
        for layout in ("p1", "p2"):
            scene, manifest = frame_scene(layout)
            receipt = motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
            width = receipt["gaugeFront"]["fullBarWidthPx"]
            owner = scene["templates"][0]["root"]
            dark, reveal = owner["children"]
            clip = reveal["properties"]
            marker = reveal["children"][1]["properties"]
            dark_left = dark["properties"]["x"] * 440
            clip_sign = -1 if clip.get("rotation", 0) == 180 else 1
            marker_sign = -1 if marker["rotation"] == 180 else 1
            marker_origin = 0 if marker["origin"] == "centre-left" else marker["width"] * width
            for gauge in (0, 0.28, 1):
                corners = [clip["x"] * 440 + clip_sign * (gauge * width + marker_sign * (x - marker_origin))
                           for x in (0, marker["width"] * width)]
                mask_corners = [clip["x"] * 440, clip["x"] * 440 + clip_sign * width * gauge]
                visible_left = max(min(corners), min(mask_corners))
                visible_right = min(max(corners), max(mask_corners))
                self.assertAlmostEqual(0 if gauge == 0 else 6, max(0, visible_right - visible_left))
                if gauge:
                    expected_left = dark_left + width * (1 - gauge) if layout == "p2" else dark_left + width * gauge - 6
                    self.assertAlmostEqual(expected_left, visible_left)

    def test_sd_source_is_projected_once_and_uses_its_own_originals(self):
        scene, manifest = frame_scene("p1", "iidx27")
        receipt = motion.inject_frame(scene, manifest, frame_id="iidx27", source_map=source_map("iidx27"))
        self.assertAlmostEqual(4 / 438, receipt["beatGeometry"]["x"])
        self.assertAlmostEqual(39 / 726, receipt["beatGeometry"]["height"])
        self.assertEqual("legacy", receipt["artwork"]["beat"]["source"]["root"])
        self.assertEqual("0/gameparts", receipt["artwork"]["normal"]["source"]["package"])

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
                    self.assertTrue(all(resource["path"].startswith(f"art/motion/{frame}/") for resource in manifest["resources"]))
                    before = copy.deepcopy((scene, manifest))
                    motion.inject_frame(scene, manifest, frame_id=frame, source_map=source_map(frame))
                    self.assertEqual(before, (scene, manifest))

    def test_frame_injection_is_idempotent_and_conflicts_do_not_partially_change_it(self):
        scene, manifest = frame_scene("p1")
        motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
        before = copy.deepcopy((scene, manifest))
        motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
        self.assertEqual(before, (scene, manifest))
        manifest["resources"][0]["path"] = "different.png"
        before = copy.deepcopy((scene, manifest))
        with self.assertRaises(ValueError):
            motion.inject_frame(scene, manifest, frame_id="iidx33", source_map=source_map())
        self.assertEqual(before, (scene, manifest))

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
