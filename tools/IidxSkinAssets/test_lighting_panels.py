"""Author-scene regression checks; no original game art or player data needed."""

import copy
import unittest

import lighting_panels as panels


class LightingPanelsTest(unittest.TestCase):
    def test_graph_separates_art_layers_and_gates_unknown_personal_best(self):
        result = panels.build_graph("test.graph", "p1", targets=panels.GRAPH_TARGETS)
        background, foreground = result["roots"]
        self.assertGreater(background["properties"]["z"], -20)
        self.assertLess(foreground["properties"]["z"], -20)
        self.assertFalse(background["properties"]["visible"])
        self.assertFalse(foreground["properties"]["visible"])
        selected = next(node for node in panels.walk(foreground) if node["id"] == "test.graph.target-personal-best")
        availability = selected["children"][0]
        self.assertFalse(availability["properties"]["visible"])
        self.assertIn({"id": availability["id"] + ".bind.visible", "target": availability["id"],
                       "property": "visible", "source": "score.personal-best-available"}, result["bindings"])
        value = next(node for node in panels.walk(selected) if node["id"].endswith(".value"))
        self.assertEqual("", value["properties"]["text"])
        rank = next(item for item in result["variants"] if item["target"].endswith(".rank-lines"))
        self.assertEqual("layout.ruleset", rank["source"])
        self.assertEqual(["bms"], [case["key"] for case in rank["cases"]])

    def test_judge_keeps_empty_poor_distinct_and_hides_unavailable_offset(self):
        result = panels.build_judge("test.judge", "p1", sources=panels.JUDGE_SOURCES)
        by_target = {item["target"]: item["source"] for item in result["bindings"]}
        self.assertEqual("judgement.count.meh", by_target["test.judge.bms-bd"])
        self.assertEqual("judgement.count.miss", by_target["test.judge.bms-pr"])
        self.assertEqual("combo.breaks", by_target["test.judge.bms-cb"])
        self.assertEqual("judgement.count.ok", by_target["test.judge.mania-ok"])
        self.assertEqual("offset.available", by_target["test.judge.offset"])
        root = result["roots"][0]
        self.assertFalse(root["properties"]["visible"])
        self.assertFalse(next(node for node in panels.walk(root) if node["id"] == "test.judge.offset")["properties"]["visible"])
        self.assertTrue(any(node.get("resource", "").endswith("judge-auto") for node in panels.walk(root)))
        offset = next(node for node in panels.walk(root) if node["id"] == "test.judge.offset")
        bms = next(node for node in panels.walk(root) if node["id"] == "test.judge.bms")
        self.assertIn(offset, bms["children"])
        value = next(node for node in panels.walk(offset) if node["id"] == "test.judge.offset-value")
        self.assertEqual("signed-fixed-2", value["properties"]["format"])
        self.assertNotIn("bitmap-font", value["properties"])
        self.assertEqual("", value["properties"]["text"])

    def test_full_plate_mirrors_the_original_two_player_counters_without_fake_shortcuts(self):
        for layout, resource, count_right in (("p1", "judge-auto", 101), ("p2", "judge-auto-flip", 270),
                                                ("dp", "judge-auto", 101)):
            result = panels.build_judge("test.judge", layout, sources=panels.JUDGE_SOURCES)
            nodes = {node["id"]: node for node in panels.walk(result["roots"][0])}
            frame = nodes["test.judge.original-frame"]
            self.assertEqual("omni.lighting." + resource, frame["resource"])
            self.assertEqual(280 / 1920, frame["properties"]["width"])
            x = panels.JUDGE_BOUNDS[layout][0]
            value = nodes["test.judge.bms-pg"]["properties"]
            self.assertAlmostEqual(x + count_right, (value["x"] + value["width"]) * 1920)
            patches = [node for node in nodes.values() if node["id"].startswith("test.judge.shortcut") and node["type"] == "clip"]
            self.assertEqual(3, len(patches))
            for patch in patches:
                image = patch["children"][0]["children"][0]
                self.assertEqual(frame["resource"], image["resource"])
                self.assertLessEqual(max(image["properties"]["width"], image["properties"]["height"]), 4)
            self.assertFalse(any(node["properties"].get("text") in ("0", "9", "3") for node in nodes.values()))

    def test_panel_entry_uses_preparation_and_leaves_statistics_out_of_animation(self):
        result = panels.build_judge("test.judge", "p1", sources=panels.JUDGE_SOURCES)
        self.assertEqual(1, len(result["tracks"]))
        entry = result["tracks"][0]
        self.assertEqual("preparation.elapsed", entry["clock"])
        self.assertEqual("test.judge", entry["target"])
        self.assertEqual("y", entry["property"])
        self.assertFalse(entry["loop"])
        self.assertGreater(entry["keyframes"][0]["value"], 0)
        self.assertEqual((500, 0), (entry["keyframes"][-1]["time"], entry["keyframes"][-1]["value"]))
        self.assertTrue(all(item["target"] != "test.judge" for item in result["bindings"] if item["property"] == "text"))
        automatic = next(item for item in result["bindings"] if item["target"] == "test.judge.automatic")
        self.assertEqual("offset.automatic", automatic["source"])

    def test_no_extra_font_is_required_when_caller_selects_graph_metrics(self):
        result = panels.build_judge("test.judge", "p2", sources=panels.JUDGE_SOURCES, counter_font=panels.GRAPH_FONT)
        fonts = {node["properties"]["bitmap-font"] for node in panels.walk(result["roots"][0])
                 if "bitmap-font" in node["properties"]}
        self.assertEqual({panels.GRAPH_FONT}, fonts)
        values = [node for node in panels.walk(result["roots"][0]) if "bitmap-font" in node["properties"]]
        self.assertTrue(all(node["properties"]["scale-x"] == 0.6 and node["properties"]["origin"] == "top-right" for node in values))

    def test_injection_refuses_duplicate_or_legacy_readouts(self):
        def scene():
            return {"root": panels._node("omni.frame.test.root"), "templates": [{
                "id": "test.template.p1.information", "root": panels._node("test.p1.information")}],
                "instances": [], "bindings": [], "variants": []}

        assets = {"resources": [], "fonts": []}
        manifest = {"resources": [], "bitmapFonts": []}
        original = scene()
        legacy = panels._node("test.p1.information.graph")
        original["templates"][0]["root"]["children"].append(legacy)
        with self.assertRaisesRegex(ValueError, "old Graph"):
            panels.inject(original, manifest, assets, layout="p1", judge_sources=panels.JUDGE_SOURCES)
        clean = scene()
        panels.inject(clean, copy.deepcopy(manifest), assets, layout="p1", judge_sources=panels.JUDGE_SOURCES)
        self.assertEqual(4, len(clean["templates"]))
        self.assertEqual(3, len(clean["instances"]))
        with self.assertRaisesRegex(ValueError, "already contains"):
            panels.inject(clean, manifest, assets, layout="p1", judge_sources=panels.JUDGE_SOURCES)

    def test_injection_declares_the_actual_automatic_offset_pointer_variant(self):
        scene = {"root": panels._node("test.root"), "templates": [{
            "id": "test.template.p1.information", "root": panels._node("test.p1.information")}],
            "instances": [], "bindings": [], "variants": []}
        default = {"id": "omni.lighting.judge-bar", "type": "texture", "path": "bar.png"}
        automatic = {"id": "omni.lighting.judge-bar-auto", "type": "texture", "path": "bar-auto.png"}
        manifest = {"resources": [], "bitmapFonts": []}
        panels.inject(scene, manifest, {"resources": [default, automatic], "fonts": []},
                      layout="p1", judge_sources=panels.JUDGE_SOURCES)
        self.assertIn(default, manifest["resources"])
        self.assertIn(automatic, manifest["resources"])


if __name__ == "__main__":
    unittest.main()
