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
        self.assertFalse(any(node.get("resource", "").endswith("judge-auto") for node in panels.walk(root)))

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


if __name__ == "__main__":
    unittest.main()
