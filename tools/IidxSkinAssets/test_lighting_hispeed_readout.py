"""The new BMS multiplier and the existing mania/legacy parameter share original art."""
import unittest
import copy

from build_skin import Builder
from lighting_gameplay_controls import upgrade_hi_speed_readouts


class HiSpeedReadoutTest(unittest.TestCase):
    def test_existing_author_readout_retains_parameter_cursor_and_mania_fallback(self):
        scene = {"root": {"id": "root", "type": "container", "children": [
            {"id": "hs", "type": "text", "properties": {"bitmap-font": "original", "x": 0.2}}]},
            "templates": [], "bindings": [{"id": "hs.b", "target": "hs", "property": "text", "source": "scroll.speed"}],
            "tracks": [{"id": "selector", "target": "cursor", "clock": "scroll.speed"}], "variants": []}
        before = copy.deepcopy(scene)
        upgraded, receipt = upgrade_hi_speed_readouts(scene)
        self.assertEqual(scene, before)
        self.assertEqual(upgraded["tracks"], before["tracks"])
        self.assertEqual(upgraded["root"]["children"][0], before["root"]["children"][0])
        self.assertEqual(upgraded["root"]["children"][1]["properties"], before["root"]["children"][0]["properties"])
        self.assertEqual(receipt["readouts"], [{"legacy": "hs", "native": "hs.actual"}])
        for available, expected in ((True, "hs.actual"), (False, "hs")):
            active = [item["target"] for item in upgraded["variants"]
                      if (item["cases"][0]["value"] if available else item["default"])]
            self.assertEqual(active, [expected])
        again, second = upgrade_hi_speed_readouts(upgraded)
        self.assertEqual(again, upgraded)
        self.assertEqual(second["readouts"], [])

    def test_actual_multiplier_and_legacy_parameter_have_one_visible_original_slot(self):
        builder = Builder.__new__(Builder)
        builder.bindings, builder.variants = [], []
        result = builder.numeric("frame.hs", "scroll.hi-speed", "original.digits", 19,
                                 {"left": 100, "top": 200, "right": 180, "bottom": 220}, fmt="fixed-2")
        native, legacy = result["children"]
        self.assertEqual(native["properties"], legacy["properties"])
        self.assertEqual(native["properties"]["bitmap-font"], "original.digits")
        self.assertEqual({item["target"]: item["source"] for item in builder.bindings},
                         {native["id"]: "scroll.hi-speed", legacy["id"]: "scroll.speed"})
        for available, expected in (("true", native["id"]), ("false", legacy["id"])):
            active = [item["target"] for item in builder.variants
                      if any(case["key"] == available and case["value"] for case in item["cases"])]
            self.assertEqual(active, [expected])
            self.assertTrue(all(item["default"] is False and item["source"] == "scroll.hi-speed-available"
                                for item in builder.variants))


if __name__ == "__main__":
    unittest.main()
