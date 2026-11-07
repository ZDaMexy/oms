"""RGB brightness reaches native carriers without changing note alpha or size."""
from pathlib import Path
import hashlib
import sys
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import Contribution
from beatoraja_export import Composer
from beatoraja_materials import collect_note_materials
from beatoraja_native import AssetSink, colour, tint_pixels
from beatoraja_widgets import compose_judges
import test_beatoraja_materials as material_fixtures
import test_beatoraja_widgets as widget_fixtures


class BrightnessTests(unittest.TestCase):
    def test_rgb_brightness_is_not_alpha_and_identity_keeps_every_byte(self):
        source = Image.new("RGBA", (256, 1))
        source.putdata([(i, 255-i, i//2, i) for i in range(256)])
        self.assertEqual(tint_pixels(source, (1, 1, 1, 1)).tobytes(), source.tobytes())
        rgba = colour({"brightness": .9})
        result = tint_pixels(source, rgba)
        self.assertEqual(result.size, source.size)
        self.assertEqual(result.getchannel("A").tobytes(), source.getchannel("A").tobytes())
        self.assertEqual([result.getpixel((i, 0)) for i in range(256)], [(round(i*.9), round((255-i)*.9), round((i//2)*.9), i) for i in range(256)])
        self.assertEqual(colour({"brightness": 0, "opacity": .5}), (0, 0, 0, .5))

    def test_bound_note_brightness_reaches_caps_and_all_body_states(self):
        fixture = material_fixtures.MaterialTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        fixture.author.name = "Synthetic carrier test"
        fixture.author.base = lambda: Contribution("base", "base", None, None, None, {}, {}, "")
        for path in fixture.root.glob("*.png"):
            with Image.open(path) as source:
                Image.new("RGBA", source.size, (123, 234, 56, 137)).save(path)
        hashes = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture.root.glob("*.png")}
        for part in fixture.parts.values():
            part.scene["bindings"] = [{"target": t["root"]["id"], "property": "brightness", "source": "option.lane-objects-brightness"}
                                      for t in part.scene["templates"]]
        options = {"lane-objects-brightness": .9}
        recipe = collect_note_materials(fixture.author, options, "7k", "p1", fixture.sink)
        self.assertTrue(all(image["rgba"] == (.9, .9, .9, 1) for image in recipe["images"]))
        output = fixture.root / "output"
        output.mkdir()
        sink = AssetSink(fixture.root, output)
        composer = Composer(fixture.author, sink, options, "p1", {})
        stage = {"rect": [0, 0, .5, .7]}
        layout = {"ruleset": "bms", "keymode": "7k", "presentation": "p1", "stages": [stage]}
        frame = SimpleNamespace(manifest={"layouts": [layout]})
        composer.lanes = lambda _: (layout, [{"x": i*40, "y": 0, "w": 40, "h": 500} for i in range(8)])
        composer.notes(frame)
        images = {item["id"]: item for item in composer.skin["image"]}
        for field in ("note", "lnstart", "lnend", "hcnstart", "hcnend", "lnbody", "lnbodyActive", "hcnbody", "hcnbodyActive", "hcnbodyReactive", "hcnbodyMiss"):
            for identifier in composer.skin["note"][field]:
                image = images[identifier]
                with Image.open(output / sink.source[image["src"]]["path"]) as pixels:
                    self.assertEqual(pixels.getpixel((0, pixels.height-1)), (111, 211, 50, 137))
                    if field == "note":
                        self.assertEqual(pixels.size[1], 16)
                        self.assertEqual(pixels.getpixel((0, 0)), (0, 0, 0, 0))
        self.assertEqual({p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in fixture.root.glob("*.png")}, hashes)
        mine = images[composer.skin["note"]["mine"][0]]
        with Image.open(output / sink.source[mine["src"]]["path"]) as pixels:
            self.assertIn(pixels.getpixel((0, pixels.height-1)), ((255, 240, 240, 255), (220, 20, 35, 255)))

    def test_judge_label_and_number_each_receive_carrier_colour(self):
        fixture = widget_fixtures.WidgetTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        recipe = fixture.judge_recipe()
        recipe["context"]["option.lane-objects-brightness"] = .9
        recipe["bindings"] = [{"target": "owner", "property": "brightness", "source": "option.lane-objects-brightness"}]
        result = compose_judges(recipe, fixture.sink, [(10, 0, 80, 100), (110, 0, 80, 100)], 720)
        for judge in result["judge"]:
            for item in [*judge["images"], *[n for n in judge["numbers"] if n["id"]]]:
                self.assertEqual({key: item["dst"][0][key] for key in ("r", "g", "b", "a")},
                                 {"r": 230, "g": 230, "b": 230, "a": 255})
        self.assertTrue(all("dst" not in item for item in result["destination"]))


if __name__ == "__main__":
    unittest.main()
