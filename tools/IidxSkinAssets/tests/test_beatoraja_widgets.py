"""Native widget semantics exercised with original synthetic pixel patterns."""
import copy
from pathlib import Path
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_native import AssetSink
from beatoraja_widgets import compose_gauges, compose_judges


class WidgetTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-materials"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.author = self.root / "author"
        self.output = self.root / "output"
        self.author.mkdir()
        self.output.mkdir()
        self.sink = AssetSink(self.author, self.output)

    def image(self, name, width, height, rgba):
        image = Image.new("RGBA", (width, height), rgba)
        image.save(self.author / (name + ".png"))
        return {"id": name, "source": name + ".png", "src": self.sink.add_source(name + ".png"),
                "x": 0, "y": 0, "w": width, "h": height, "divx": 1, "divy": 1}

    def judge_recipe(self):
        images, alternatives, tracks = [], [], []
        for index, rgba in enumerate(((255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255))):
            label = self.image("label" + str(index), 2, 3, rgba)
            digits = self.image("digits" + str(index), 20, 3, rgba)
            images.extend([label, digits])
            ancestors = [{"id": "owner", "type": "container", "properties": {"opacity": 0}},
                         {"id": "palette" + str(index), "type": "container", "properties": {}},
                         {"id": "row" + str(index), "type": "container", "properties": {"x": .5, "y": .5, "origin": "centre", "layout": "horizontal"}}]
            font = {"font": "font" + str(index), "line_height": 3, "glyph_size": [2, 3], "advance": 1,
                    "digit_count": 10, "grid_image": digits["id"], "value_grid": {**digits, "divx": 10}}
            alternatives.append({"image": label["id"], "font": font, "ancestors": ancestors,
                                 "label_node": {"properties": {"height-px": 3.2}},
                                 "number_node": {"properties": {"font-size": 3.2}}})
            tracks.append({"target": "palette" + str(index), "property": "opacity", "trigger": "judgement.hit", "loop": True, "easing": "step",
                           "keyframes": [{"time": t * 100, "value": int(t % 3 == index)} for t in range(4)]})
        grades = ["perfect", "great", "good", "meh", "miss", "ok"]
        return {"family": "standard", "judge": {"id": "judge"}, "context": {}, "images": images,
                "tracks": tracks, "bindings": [], "variants": [],
                "results": [{"native_index": i, "grade": grade, "alternatives": alternatives if i == 0 else alternatives[:1]} for i, grade in enumerate(grades)]}

    def test_native_timers_and_palette_pages_preserve_source_pixels(self):
        recipe = self.judge_recipe()
        result = compose_judges(recipe, self.sink, [(10, 0, 80, 100), (110, 0, 80, 100)], 720)
        self.assertEqual([j["index"] for j in result["judge"]], [0, 1])
        self.assertEqual([j["images"][0]["timer"] for j in result["judge"]], [46, 47])
        for index, timer in enumerate((46, 47)):
            image = next(i for i in result["image"] if i["id"] == result["judge"][index]["images"][0]["id"])
            value = next(v for v in result["value"] if v["id"] == result["judge"][index]["numbers"][0]["id"])
            self.assertEqual((image["cycle"], image["divy"], value["cycle"], value["divy"]), (300, 3, 300, 3))
            self.assertEqual((image["timer"], value["timer"]), (timer, timer))
            with Image.open(self.output / self.sink.source[image["src"]]["path"]) as atlas:
                self.assertEqual([atlas.getpixel((0, y)) for y in (0, 3, 6)], [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255)])
        self.assertTrue(all(d["dst"][-1]["time"] == 500 for j in result["judge"] for d in j["images"]))

    def test_unqualified_palette_timing_is_not_silently_replaced(self):
        recipe = self.judge_recipe()
        recipe["tracks"][1]["keyframes"][1]["time"] = 125
        with self.assertRaisesRegex(ValueError, "Uneven palette"):
            compose_judges(recipe, self.sink, [(10, 0, 80, 100)], 720)

    def test_source_lift_track_moves_label_once_and_combo_stays_relative(self):
        recipe = self.judge_recipe()
        recipe["tracks"].append({"id": "source-lift", "target": "owner", "property": "y",
                                  "clock": "lift.offset", "easing": "linear", "loop": False,
                                  "keyframes": [{"time": 0, "value": 0}, {"time": 1, "value": -1 / .12}]})
        before = copy.deepcopy(recipe)
        stages = [(10, 0, 80, 100), (110, 0, 80, 100)]
        result = compose_judges(recipe, self.sink, stages, 720)
        self.assertEqual(recipe, before)
        for judge in result["judge"]:
            self.assertTrue(all(label["offsets"] == [3] for label in judge["images"]))
            self.assertTrue(all("offsets" not in number for number in judge["numbers"]))
        self.assertTrue(all("offsets" not in destination for destination in result["destination"]))
        self.assertNotIn("judge-dynamic-position-tracks", [gap["code"] for gap in result["gaps"]])

    def test_family_name_does_not_create_lift_tracking_without_source_track(self):
        recipe = self.judge_recipe()
        for family in ("standard", "lightning"):
            recipe["family"] = family
            result = compose_judges(recipe, self.sink, [(10, 0, 80, 100)], 720)
            self.assertTrue(all("offsets" not in label for label in result["judge"][0]["images"]))

    def test_non_equivalent_lift_curve_is_not_replaced_by_native_offset(self):
        recipe = self.judge_recipe()
        recipe["tracks"].append({"id": "source-lift", "target": "owner", "property": "y",
                                  "clock": "lift.offset", "easing": "linear", "loop": False,
                                  "keyframes": [{"time": 0, "value": 0}, {"time": .5, "value": -1}, {"time": 1, "value": -1 / .12}]})
        with self.assertRaisesRegex(ValueError, "does not match the native lane offset"):
            compose_judges(recipe, self.sink, [(10, 0, 80, 100)], 720)

    def test_label_only_cycles_actual_resource_palettes_without_combo(self):
        recipe = self.judge_recipe()
        recipe["tracks"] = []
        recipe["resource_images"] = {"blue": "label0", "green": "label1", "red": "label2"}
        recipe["context"]["option.judgement-display"] = "label-only"
        for grade in recipe["results"]:
            row = copy.deepcopy(grade["alternatives"][0])
            row["label_node"]["id"] = grade["grade"] + ".label"
            row["font"] = None
            row["number_node"] = None
            grade["alternatives"] = [row]
        recipe["tracks"].append({"id": "palette", "target": "perfect.label", "property": "resource",
                                  "easing": "step", "loop": True, "trigger": "judgement.hit",
                                  "keyframes": [{"time": i * 100, "value": resource} for i, resource in enumerate(("blue", "green", "red", "blue"))]})
        result = compose_judges(recipe, self.sink, [(10, 0, 80, 100)], 720)
        label = result["image"][0]
        self.assertEqual((label["cycle"], label["divy"], label["timer"]), (300, 3, 46))
        self.assertEqual(result["value"], [])
        self.assertTrue(all(number == {"id": ""} for number in result["judge"][0]["numbers"]))
        with Image.open(self.output / self.sink.source[label["src"]]["path"]) as atlas:
            self.assertEqual(atlas.size, (2, 9))
            self.assertEqual([atlas.getpixel((0, y)) for y in (0, 3, 6)], [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255)])
        self.assertNotIn("label-only-resource-palette-track", [gap["code"] for gap in result["gaps"]])
        recipe["tracks"][0]["keyframes"][1]["time"] = 125
        with self.assertRaisesRegex(ValueError, "Uneven label palette"):
            compose_judges(recipe, self.sink, [(10, 0, 80, 100)], 720)

    def test_combo_fit_records_missing_per_judge_read_instead_of_using_max_combo(self):
        recipe = self.judge_recipe()
        recipe["tracks"].append({"id": "fit", "target": "row0", "property": "scale-x", "clock": "combo.value",
                                  "easing": "step", "loop": False, "keyframes": [{"time": 0, "value": 1}, {"time": 1000, "value": .9}]})
        result = compose_judges(recipe, self.sink, [(10, 0, 80, 100)], 720)
        gap = next(gap for gap in result["gaps"] if gap["code"] == "judge-combo-fit-track")
        self.assertEqual(gap["tracks"], ["fit"])
        self.assertIn("per-stage hit-time course combo", gap["detail"])
        self.assertFalse(any("draw" in destination for destination in result["destination"]))

    def test_right_filling_gauge_mirrors_pixels_without_resizing(self):
        source = self.image("gauge", 4, 1, (0, 0, 0, 0))
        pattern = [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 255, 255)]
        image = Image.new("RGBA", (4, 1));image.putdata(pattern);image.save(self.author / "gauge.png")
        source["src"] = self.sink.add_source("gauge.png")
        root = {"id": "root", "type": "container", "properties": {}}
        clip = {"id": "clip", "type": "clip", "properties": {"x": 1, "y": 1, "rotation": 180}}
        sprite = {"id": "fill", "type": "sprite", "properties": {"x": 1, "y": 1, "rotation": 180}, "resource": "gauge"}
        recipe = {"family": "standard", "images": [source], "bindings": [], "variants": [], "context": {}, "destinations": [], "resource_images": {},
                  "bars": [{"ancestors": [root, clip], "sprite_node": sprite,
                            "states": {k: "gauge" for k in ("normal", "easy", "assist-easy", "hard", "ex-hard", "hazard")},
                            "hosts": [{"slot": "hud.gauge", "target": {"kind": "stage", "group-logical": "0"}}]}]}
        before = copy.deepcopy(recipe)
        result = compose_gauges(recipe, self.sink, [(10, 20, 40, 10)], 720)
        self.assertEqual(recipe, before)
        self.assertEqual(result["destination"][0]["dst"][0]["x"], 50)
        self.assertEqual(result["destination"][0]["dst"][0]["w"], -40)
        self.assertIn("main_state.gauge() / 100", result["graph"][0]["value"])
        with Image.open(self.output / self.sink.source[result["graph"][0]["src"]]["path"]) as mirrored:
            self.assertEqual(mirrored.size, (4, 1))
            self.assertEqual([mirrored.getpixel((x, 0)) for x in range(4)], list(reversed(pattern)))


if __name__ == "__main__":
    unittest.main()
