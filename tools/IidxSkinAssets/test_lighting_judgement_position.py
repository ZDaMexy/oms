"""Protect original judgement ownership while adding reviewed position/OFF control."""
import copy
import unittest

from lighting_judgement_position import add_position, definition, host_height, reviewed_points


POINTS = [{"value": -2, "deltaY": 80}, {"value": 0, "deltaY": 0},
          {"value": 1, "deltaY": -50}, {"value": 10, "deltaY": -500}]


def fixture():
    # Synthetic shapes only. Values are not a substitute for the builder's
    # separately pinned native gameplay review.
    templates = []
    for index in range(3):
        root_id = "grade.g" + str(index)
        templates.append({"id": "template.t" + str(index), "root": {
            "id": root_id, "type": "container", "slot": "hud.judgement",
            "target": {"kind": "global"}, "blend": "alpha", "effects": [],
            "properties": {"opacity": .4, "colour": "#aabbccff"},
            "children": [{"id": root_id + ".row", "type": "container", "properties": {
                "y": .5 if index == 2 else .21749084, "clip": True, "layout": "horizontal"},
                "children": [{"id": root_id + ".label", "type": "sprite", "resource": "original.grade"},
                             {"id": root_id + ".combo", "type": "text", "properties": {"bitmap-font": "original.digits"}}]}]}})
    return {"contract": "oms-gameplay-skin-scene.v1", "root": {"id": "root", "children": []},
            "templates": templates, "instances": [{"id": "instance.i" + str(index), "template": item["id"]}
                                                   for index, item in enumerate(templates)],
            "tracks": [{"id": "original.fade", "target": "grade.g0", "property": "opacity"}],
            "bindings": [{"id": "original.rgb", "target": "grade.g0", "property": "brightness"}],
            "variants": [{"id": "original.grade", "target": "grade.g0.label", "property": "resource"}],
            "stateMachines": [{"id": "original.lifecycle", "states": [{"id": "original.shown", "set": [
                {"id": "original.alpha", "target": "grade.g0", "property": "opacity", "value": .4}]}], "transitions": []}]}


class JudgementPositionTest(unittest.TestCase):
    def test_whole_group_moves_without_changing_rows_fonts_clips_or_lifecycle(self):
        original = fixture()
        before = copy.deepcopy(original)
        result, receipt = add_position(original, logical_host_height=87.36, points=POINTS)
        self.assertEqual(original, before)
        self.assertEqual(result["stateMachines"], before["stateMachines"])
        self.assertEqual(result["bindings"], before["bindings"])
        self.assertEqual(result["instances"], before["instances"])
        self.assertEqual(len(receipt), 3)
        for old_template, template, row in zip(before["templates"], result["templates"], receipt):
            root = template["root"]
            gate = root["children"][0]
            self.assertEqual(root["properties"], old_template["root"]["properties"])
            self.assertEqual(gate["children"], old_template["root"]["children"])
            self.assertNotIn("slot", gate)
            self.assertNotIn("clip", gate["properties"])
            self.assertEqual(gate["effects"], [])
            self.assertEqual(row["originalChildren"], [old_template["root"]["children"][0]["id"]])
        self.assertEqual(result["tracks"][0], original["tracks"][0])
        self.assertEqual(result["variants"][0], original["variants"][0])

    def test_zero_preserves_baseline_and_pixel_displacements_use_host_height(self):
        for height in (87.12, 87.36):
            result, _ = add_position(fixture(), logical_host_height=height, points=POINTS)
            tracks = result["tracks"][1:]
            self.assertEqual(len(tracks), 6)
            for track in tracks:
                self.assertFalse(track["loop"])
                self.assertEqual(track["clock"], "option.judgement-position")
                self.assertNotIn("trigger", track)
                self.assertEqual([key["time"] for key in track["keyframes"]], [-2, 0, 1, 10])
                self.assertEqual(track["keyframes"][1]["value"], 0)
                self.assertTrue(all(abs(key["value"]) <= 4 for key in track["keyframes"]))
            for index in range(0, len(tracks), 2):
                pair = tracks[index:index + 2]
                self.assertNotEqual(pair[0]["target"], pair[1]["target"])
                self.assertAlmostEqual(sum(track["keyframes"][0]["value"] for track in pair) * height, 80)
                self.assertAlmostEqual(sum(track["keyframes"][-1]["value"] for track in pair) * height, -500)
        option = definition()
        self.assertEqual((option["min"], option["max"], option["step"], option["default"]), (-2, 10, 1, 0))

    def test_off_owns_only_inner_gate_and_preserves_existing_combo_selection(self):
        result, _ = add_position(fixture(), logical_host_height=87.36, points=POINTS)
        for variant, template in zip(result["variants"][1:], result["templates"]):
            self.assertEqual(variant["target"], template["root"]["children"][0]["id"])
            self.assertTrue(variant["default"])
            self.assertEqual([(case["key"], case["value"]) for case in variant["cases"]], [("off", False)])
            self.assertEqual(template["root"]["properties"]["opacity"], .4)
        # Actual multiplication and re-opening are checked by the shared C5
        # runtime tests and the imported real Player captures, not simulated here.

    def test_existing_y_owner_or_repeat_migration_is_rejected(self):
        scene = fixture()
        scene["stateMachines"][0]["states"][0]["set"].append({"target": "grade.g0", "property": "y", "value": 1})
        with self.assertRaisesRegex(ValueError, "Y ownership"):
            add_position(scene, logical_host_height=87.36, points=POINTS)
        updated, _ = add_position(fixture(), logical_host_height=87.36, points=POINTS)
        with self.assertRaisesRegex(ValueError, "existing judgement-position"):
            add_position(updated, logical_host_height=87.36, points=POINTS)

    def test_new_typed_ids_obey_the_public_stable_id_grammar(self):
        from lighting_start_options import _declarations
        from lighting_persistent import _walk
        result, _ = add_position(fixture(), logical_host_height=87.36, points=POINTS)
        nodes = [node for root in (result["root"], *(template["root"] for template in result["templates"]))
                 for node in _walk(root)]
        for declaration in _declarations(result, nodes):
            if declaration["id"].startswith("jp."):
                self.assertRegex(declaration["id"], r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")

    def test_native_review_requires_real_anchors_instead_of_menu_preview_guess(self):
        for points in (POINTS[1:], list(reversed(POINTS)),
                       [{"value": -2, "deltaY": float("nan")}, *POINTS[1:]]):
            with self.assertRaises(ValueError):
                reviewed_points(points)

    def test_host_conversion_uses_authored_stage_union_for_both_rulesets(self):
        for ruleset in ("bms", "mania"):
            for stages in ([{"rect": [.1, 0, .2, 728 / 1080]}],
                           [{"rect": [.1, 0, .2, 728 / 1080]}, {"rect": [.4, 0, .2, 728 / 1080]}]):
                self.assertAlmostEqual(host_height({"ruleset": ruleset, "stages": stages}), 87.36)
        with self.assertRaisesRegex(ValueError, "safe-height cap"):
            host_height({"ruleset": "bms", "stages": [{"rect": [0, 0, .3, 1]}]})


if __name__ == "__main__":
    unittest.main()
