import unittest
from pathlib import Path
import sys
from types import SimpleNamespace
from unittest.mock import patch
from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_gauge import compose_gauge_markers, compose_gauge_shade, _pulse_palette


class GaugeBorderTests(unittest.TestCase):
    def recipe(self):
        grey = {"id": "z-grey", "type": "sprite", "resource": "line",
                "properties": {"width": .01, "colour": "#999999ff"}}
        gold = {"id": "a-gold", "type": "sprite", "resource": "line",
                "properties": {"width": .01, "colour": "#ffcc33ff"}}
        root = {"id": "root", "type": "container", "properties": {"visible": False}, "children": [grey, gold]}
        bindings = [{"id": n["id"] + ".x", "target": n["id"], "property": "x", "source": "gauge.clear-threshold"}
                    for n in (grey, gold)]
        variants = [{"id": "ruleset", "target": "root", "property": "visible", "source": "layout.ruleset",
                     "default": False, "cases": [{"key": "bms", "value": True}]},
                    {"id": "clear", "target": "a-gold", "property": "visible", "source": "gauge.clear-state",
                     "default": False, "cases": [{"key": "clear", "value": True}]}]
        # Palette visibility belongs to each leaf for this small fixture.
        variants.extend({"id": n["id"] + ".gauge", "target": n["id"], "property": "visible", "source": "gauge.type",
                         "default": False, "cases": [{"key": k, "value": True} for k in ("normal", "easy", "assist-easy")]}
                        for n in (grey,))
        scene = {"root": root, "bindings": bindings, "variants": variants}
        recipe = {"family": "standard", "context": {"layout.ruleset": "bms"},
                  "bindings": bindings, "variants": variants[1:],
                  "images": [{"id": "line-image", "src": "line-source", "x": 0, "y": 0, "w": 1, "h": 1}],
                  "resource_images": {"line": "line-image"},
                  "bars": [{"ancestors": [root], "hosts": [{"slot": "hud.gauge", "target": {"kind": "stage", "group-logical": 0}}]}],
                  "destinations": [{"node": n, "ancestors": [root, n]} for n in (gold, grey)]}
        return recipe, SimpleNamespace(scene=scene, templates={})

    def test_native_border_positions_and_ruleset_ancestor(self):
        recipe, source = self.recipe()
        result = compose_gauge_markers(recipe, source, [(10, 20, 100, 10)], 720)
        grey = [d for d in result["destination"] if "z-grey" in d["id"]]
        self.assertEqual(len(grey), 6)
        for d in grey:
            gauge_type = int(d["id"].split(".")[-2])
            self.assertEqual(d["dst"][0]["x"], 70 if gauge_type == 0 else 90)
            self.assertIn("main_state.option(1240)", str(d["draw"]))

    def test_source_order_wins_over_sorted_recipe_targets(self):
        recipe, source = self.recipe()
        result = compose_gauge_markers(recipe, source, [(10, 20, 100, 10)], 720)
        ids = [d["id"] for d in result["destination"]]
        self.assertLess(max(i for i, v in enumerate(ids) if "z-grey" in v),
                        min(i for i, v in enumerate(ids) if "a-gold" in v))

    def test_boundary_clip_preserves_fixed_art_and_original_tint(self):
        leaf = {"id": "shade", "type": "sprite", "resource": "bar", "properties": {"x": .1, "colour": "#40404080"}}
        inner = {"id": "inner", "type": "clip", "properties": {"x": -.1, "reveal-x": .1}, "children": [leaf]}
        moving = {"id": "moving", "type": "container", "children": [inner]}
        outer = {"id": "outer", "type": "clip", "children": [moving]}
        root = {"id": "root", "type": "container", "children": [outer]}
        scene = {"root": root, "bindings": [
            {"target": "outer", "property": "reveal-x", "source": "gauge.value"},
            {"target": "moving", "property": "x", "source": "gauge.value"}],
            "tracks": [{"target": "shade", "property": "x", "clock": "gauge.value", "keyframes": [
                {"time": 0, "value": .1}, {"time": 1, "value": -.9}]}]}
        recipe = {"family": "lightning", "context": {}, "images": [{"id": "art", "src": "source", "x": 0, "y": 0, "w": 100, "h": 20}],
                  "resource_images": {"bar": "art"}, "bars": [{"ancestors": [root, outer],
                      "hosts": [{"slot": "hud.gauge", "target": {"kind": "stage", "group-logical": 0}}]}]}
        result = compose_gauge_shade(recipe, SimpleNamespace(scene=scene), [(10, 20, 100, 20)], 720)
        points = result["destination"][0]["dst"]
        self.assertEqual([(p['x'], p['y'], p['w'], p['h']) for p in points], [(10, 680, 100, 20)] * 3)
        self.assertEqual([(p['clip_x'], p['clip_w']) for p in points], [(10, 0), (10, 10), (100, 10)])
        self.assertEqual([(p['r'], p['a']) for p in points], [(64, 128)] * 3)
        self.assertIn('main_state.gauge()/100', str(result['destination'][0]['timer']))

    def test_pulse_palette_preserves_rgb_and_reuses_identical_pixels(self):
        original = Image.new('RGBA', (2, 1))
        original.putdata([(20, 30, 40, 128), (50, 60, 70, 255)])
        saved = []
        def save(image):
            saved.append(image.copy())
            return 'atlas'
        sink = SimpleNamespace(_gauge_pulse_palettes={}, add_raster=save)
        image = {'src': 'original', 'x': 0, 'y': 0, 'w': 2, 'h': 1}
        track = {'clock': 'timing.time', 'property': 'opacity', 'loop': True, 'keyframes': [
            {'time': 0, 'value': .2}, {'time': 200, 'value': 1}, {'time': 400, 'value': .2}]}
        with patch('beatoraja_gauge._pixels', return_value=original) as pixels:
            a, first = _pulse_palette(image, track, sink, 'a')
            b, second = _pulse_palette(image, track, sink, 'b')
        self.assertEqual(pixels.call_count, 1)
        self.assertEqual(len(saved), 1)
        self.assertEqual(saved[0].getpixel((0, 0)), (20, 30, 40, 26))
        self.assertEqual(saved[0].getpixel((0, 204)), (20, 30, 40, 128))
        self.assertEqual(saved[0].getpixel((1, 0)), (50, 60, 70, 51))
        self.assertEqual(len(a), 205)
        self.assertEqual([i['src'] for i in a], [i['src'] for i in b])
        self.assertNotEqual(first['images'], second['images'])


if __name__ == "__main__":
    unittest.main()
