"""Synthetic clear semantics, identity and byte-preservation contracts."""
from __future__ import annotations

import copy
import io
import unittest

from lighting_gameplay_controls import _node, empty_scene, walk
from lighting_gauge_state import add_semantics, compact_unreferenced_identities, declarations
from build_lighting_gauge_state import private_path_plan, white_pixel


def fixture(layout="p1"):
    scene = empty_scene("fixture.root")
    dark = _node("fixture.dark", "sprite", {"x": .1, "y": .3, "width": .8, "height": .25, "colour": "#404040ff"}, resource="original")
    reveal = _node("fixture.reveal", "clip", {"clip-mode": "bounds", "reveal-x": 0,
                    **({"rotation": 180} if layout == "p2" else {})}, children=[_node("fixture.fill", "sprite", resource="original")])
    root = _node("fixture.gauge", slot="hud.gauge", children=[dark, reveal])
    scene["templates"] = [{"id": "fixture.template", "root": root}]
    scene["instances"] = [{"id": "fixture.instance", "template": "fixture.template", "material": {"slot": "hud.gauge", "resource": "unchanged/material"}}]
    scene["bindings"] = [{"id": "fixture.value", "target": reveal["id"], "source": "gauge.value", "property": "reveal-x"}]
    return scene


def visible_ids(scene, values):
    variants = {value["target"]: value for value in scene["variants"] if value["property"] == "visible"}
    result = set()
    def visit(node, parent=True):
        shown = node["properties"].get("visible", True)
        if node["id"] in variants:
            value = variants[node["id"]]
            shown = next((case["value"] for case in value["cases"] if case["key"] == values.get(value["source"])), value["default"])
        if shown and parent:
            result.add(node["id"])
            for child in node["children"]:
                visit(child)
    for template in scene["templates"]:
        visit(template["root"])
    return result


class GaugeSemanticsTest(unittest.TestCase):
    def test_each_layout_uses_true_threshold_axis_and_existing_direction(self):
        for layout in ("p1", "p2", "dp"):
            original = fixture(layout)
            saved = copy.deepcopy(original)
            scene, receipt = add_semantics(original, layout=layout, host_size=(500, 100), line_resource="gx.line")
            self.assertEqual(original, saved)
            axis = next(n for t in scene["templates"] for n in walk(t["root"]) if n["id"] == "gx." + layout + ".threshold")
            binding, = [x for x in scene["bindings"] if x["source"] == "gauge.clear-threshold"]
            self.assertEqual((binding["target"], binding["property"]), (axis["id"], "x"))
            self.assertEqual(axis["properties"]["width"], 1 / 400)
            self.assertEqual(axis["properties"]["origin"], "top-centre")
            self.assertEqual(receipt["actualBarPixelsAt1080"], [400, 25])
            reverse = [n for t in scene["templates"] for n in walk(t["root"]) if n["id"].endswith(".reverse")]
            self.assertEqual(len(reverse), int(layout == "p2"))
            if reverse:
                self.assertEqual(reverse[0]["properties"], {"x": 1, "y": 1, "rotation": 180})
            self.assertTrue(all(x["id"].startswith("gx." + layout + ".") for x in declarations(scene) if x["id"].startswith("gx.")))

    def test_non_eighty_thresholds_survival_clear_state_and_mania_are_independent(self):
        scene, _ = add_semantics(fixture(), layout="p1", host_size=(500, 100), line_resource="gx.line")
        for threshold in (.5, .6, .75, .8):
            values = {"layout.ruleset": "bms", "gauge.type": "normal", "gauge.clear-state": "clear", "gauge.clear-threshold": threshold}
            shown = visible_ids(scene, values)
            self.assertIn("gx.p1.line", shown)
            self.assertIn("gx.p1.clear", shown)
            self.assertNotIn("gx.p1.unclear", shown)
        for mode in ("hard", "ex-hard", "hazard"):
            values = {"layout.ruleset": "bms", "gauge.type": mode, "gauge.clear-state": "uncleared", "gauge.clear-threshold": 0}
            shown = visible_ids(scene, values)
            self.assertNotIn("gx.p1.line", shown)
            self.assertIn("gx.p1.unclear", shown)
            self.assertEqual("gx.p1.hazard" in shown, mode == "hazard")
        for state in ("clear", "uncleared", "unavailable"):
            shown = visible_ids(scene, {"layout.ruleset": "mania", "gauge.type": "normal", "gauge.clear-state": state})
            self.assertFalse(any(x.startswith("gx.") for x in shown))
        shown = visible_ids(scene, {"layout.ruleset": "bms", "gauge.type": "none", "gauge.clear-state": "unavailable"})
        self.assertNotIn("gx.p1.clear", shown)
        self.assertNotIn("gx.p1.unclear", shown)

    def test_old_art_properties_bindings_and_materials_are_unchanged(self):
        source = fixture()
        result, _ = add_semantics(source, layout="p1", host_size=(500, 100), line_resource="gx.line")
        restored = copy.deepcopy(result)
        restored["templates"][0]["root"]["children"].pop()
        for field in ("bindings", "variants"):
            restored[field] = [x for x in restored[field] if not x["target"].startswith("gx.")]
        self.assertEqual(restored, source)
        self.assertFalse(any(n["properties"].get("bitmap-font") for t in result["templates"] for n in walk(t["root"])))


class GaugeIdentityTest(unittest.TestCase):
    def test_only_unreferenced_program_identities_change_and_global_ids_are_excluded(self):
        scene = fixture()
        scene["tracks"] = [{"id": "fixture.track", "target": "fixture.dark", "property": "x", "clock": "gauge.value", "type": "tween",
                            "keyframes": [{"id": "fixture.key", "time": 0, "value": 0}]}]
        saved = copy.deepcopy(scene)
        compacted, receipt = compact_unreferenced_identities(scene, forbidden_ids={"a", "b", "c"}, external_references={"fixture.track"})
        self.assertEqual(scene, saved)
        self.assertEqual(compacted["tracks"][0]["id"], "fixture.track")
        self.assertNotIn(compacted["tracks"][0]["keyframes"][0]["id"], {"a", "b", "c"})
        self.assertEqual(compacted["tracks"][0]["target"], "fixture.dark")
        self.assertTrue(receipt["reversibleExact"])
        self.assertGreater(receipt["savedBytes"], 0)

    def test_private_path_aliases_preserve_ids_and_all_ini_markers(self):
        manifests = {"root": {"resources": [{"id": "private", "path": "art/private/0123456789abcdef.png"},
                       {"id": "marker", "path": "art/required-marker.png"}], "bitmapFonts": [{"id": "font", "glyphs": []}]}}
        manifests["branch"] = copy.deepcopy(manifests["root"])
        saved = copy.deepcopy(manifests)
        changed, receipt = private_path_plan(manifests, manifests["root"], {
            "art/private/0123456789abcdef.png": "1" * 64, "art/required-marker.png": "2" * 64},
            ['decoration: resource Provide "art/required-marker"'])
        self.assertEqual(manifests, saved)
        self.assertEqual(changed["root"]["resources"][0]["id"], "private")
        self.assertEqual(changed["root"]["resources"][1], saved["root"]["resources"][1])
        self.assertEqual(changed["root"], changed["branch"])
        self.assertEqual(changed["root"]["bitmapFonts"], saved["root"]["bitmapFonts"])
        self.assertTrue(receipt["resourceFontMaterialIdsUnchanged"])

    def test_generated_line_is_one_opaque_white_pixel(self):
        from PIL import Image
        image = Image.open(io.BytesIO(white_pixel())).convert("RGBA")
        self.assertEqual(image.size, (1, 1))
        self.assertEqual(image.getpixel((0, 0)), (255, 255, 255, 255))


if __name__ == "__main__":
    unittest.main()
