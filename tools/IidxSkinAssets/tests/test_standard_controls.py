"""Real topology and source-projection boundaries; no private artwork fixtures."""
import sys
import json
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from standard_controls import cabinet_keys, quad_for_host, quad_track, source_slot


class StandardControlsTests(unittest.TestCase):
    def test_every_supported_layout_has_unique_physical_targets(self):
        contexts = list(material_contexts())
        self.assertEqual({c["suffix"].split()[1] for c in contexts}, {
            "keymode=5k", "keymode=6k", "keymode=7k", "keymode=8k", "keymode=14k", "keymode=7k-7k", "keymode=8k-8k"})
        for context in contexts:
            slots = [source_slot(context, lane)[0] for lane in context["lanes"]]
            self.assertEqual(len(slots), len(set(slots)))
            self.assertTrue(all(0 <= slot < 8 for slot in slots))
            colours = [lane["colour"] for lane in context["lanes"]]
            self.assertEqual(colours.count("scratch"), int(context["ruleset"] == "bms" or len(colours) in (6, 8)))

    def test_bms_5p2_retains_logical_scratch_and_right_five_keys(self):
        context = next(c for c in material_contexts() if c["ruleset"] == "bms" and c["layout"] == "p2" and "keymode=5k " in c["suffix"])
        self.assertEqual([source_slot(context, lane)[0] for lane in context["lanes"]], [7, 2, 3, 4, 5, 6])

    def test_dp_scratch_is_group_local_last_on_right(self):
        contexts = [c for c in material_contexts() if c["ruleset"] == "bms" and c["layout"] == "dp"]
        self.assertEqual([lane["colour"] for lane in contexts[0]["lanes"]][0], "scratch")
        self.assertEqual([lane["colour"] for lane in contexts[1]["lanes"]][-1], "scratch")
        self.assertEqual([source_slot(contexts[1], lane)[0] for lane in contexts[1]["lanes"]], list(range(8)))

    def test_mismatched_source_colour_is_rejected(self):
        context = next(material_contexts())
        lane = {**context["lanes"][0], "colour": "white"}
        with self.assertRaisesRegex(ValueError, "colour"):
            source_slot(context, lane)

    def test_mirror_and_tiny_rotation_retain_corner_identity(self):
        key = {"cap": [0, 0, 28, 47], "draw": {"matrix": {"a": -1, "b": 0.0001220703125, "c": 0, "d": 1, "tx": 100, "ty": 10}}}
        quad = quad_for_host(key, {"width": 50, "height": 69}, [55, 0, 34, 80])["vertices"]
        self.assertGreater(quad[0][0], quad[1][0])
        self.assertNotEqual(quad[0][1], quad[1][1])

    def test_missing_source_lane_is_not_guessed(self):
        with self.assertRaisesRegex(ValueError, "source lane"):
            cabinet_keys({"layouts": {"1p_frame": {"draws": []}}}, 26, "p1", 0)

    def test_projective_track_serializes_the_published_scene_contract(self):
        # Compare the serialization shape with the published author example,
        # not with a second copy of this exporter's field-name assumptions.
        reference = (Path(__file__).resolve().parents[3] / "skin-authoring/docs/REFERENCE.md").read_text(encoding="utf-8")
        section = reference.split("### 已投影纹理的四角动画", 1)[1]
        example = json.loads(section.split("```json\n", 1)[1].split("```", 1)[0])
        quad = {"vertices": [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]], "opacity": 1}
        actual = json.loads(json.dumps(quad_track("source.fixed", "source.sprite", quad)))
        self.assertEqual(set(actual), set(example) - {"trigger"})
        self.assertEqual(actual["type"], example["type"])
        self.assertEqual(actual["property"], example["property"])
        self.assertEqual(set(actual["keyframes"][0]), set(example["keyframes"][0]))
        self.assertTrue(actual["keyframes"][0]["id"])
        self.assertEqual(set(actual["keyframes"][0]["value"]), set(example["keyframes"][0]["value"]))
        self.assertEqual(actual["keyframes"][0]["value"], quad)

    def test_full_light_halo_extends_beyond_key_host_without_cropping(self):
        key = {"cap": [0, 0, 28, 47], "draw": {"matrix": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": 115, "ty": 494}}}
        quad = quad_for_host(key, {"width": 50, "height": 69}, [112, 484, 34, 80])["vertices"]
        self.assertLess(quad[0][0], 0)
        self.assertGreater(quad[1][0], 1)
        self.assertLess(quad[0][1], 0)
        self.assertAlmostEqual((quad[1][0] - quad[0][0]) * 34, 50)
        self.assertAlmostEqual((quad[2][1] - quad[1][1]) * 80, 69)


if __name__ == "__main__":
    unittest.main()
