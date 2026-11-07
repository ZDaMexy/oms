"""Author glyph overhang, native negative spacing and actual SkinNumber layout."""

from copy import deepcopy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import Contribution
from beatoraja_hud import _bitmap_font, _scaled_integer, export_hud
from beatoraja_native import AssetSink, lua


class HudSpacingTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-hud-spacing-tests"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        source, self.output = self.folder / "source", self.folder / "output"
        source.mkdir(); self.output.mkdir()
        self.art = Image.new("RGBA", (320, 18))
        for i in range(10):
            self.art.paste((20+i*10, 100, 200-i*10, 100+i*10), (i*32, 0, i*32+28, 18))
        self.art.save(source / "glyphs.png")
        self.sink = AssetSink(source, self.output)
        self.font = {"id": "source.bpm-small", "lineHeight": 18, "glyphs": [
            {"character": str(i), "resource": "source.glyphs", "advance": 25, "rect": [i*32, 0, 28, 18]}
            for i in range(10)]}
        self.resources = {"source.glyphs": {"id": "source.glyphs", "path": "glyphs.png"}}

    def scaled(self, *, alignment="right", scale=(1, 1), size=12.8, font=None, identifier="number"):
        gaps = []
        result = _scaled_integer(font or self.font, self.resources, self.sink,
            {"font-size": size, "alignment": alignment}, (200.25, 300.125, 104*scale[0], 24),
            scale, 1080, identifier, "main_state.number(91)", gaps=gaps)
        return result, gaps

    def test_original_glyph_pixels_remain_full_width_with_negative_space(self):
        (value, destination), gaps = self.scaled()
        self.assertEqual(-3, value["space"])
        self.assertEqual(28, destination["w"])
        self.assertEqual([], gaps)
        with Image.open(self.output / self.sink.source[value["src"]]["path"]) as atlas:
            for i in range(10):
                self.assertEqual(self.art.crop((i*32, 0, i*32+28, 18)).tobytes(),
                                 atlas.crop((i*28, 0, i*28+28, 18)).tobytes())

    def test_last_glyph_overhang_is_reserved_for_all_alignments(self):
        for alignment in ("left", "centre", "right"):
            (value, destination), _ = self.scaled(alignment=alignment)
            span = 9*25+28
            x = {"left": 200.25, "centre": 200.25+(104-span)/2, "right": 200.25+104-span}[alignment]
            self.assertEqual(round(x), destination["x"])
            self.assertEqual({"left": 1, "centre": 2, "right": 0}[alignment], value["align"])

    def test_iidx27_anisotropic_rounding_retains_nearest_cell_and_advance(self):
        (value, destination), gaps = self.scaled(scale=(.76339286, 1), size=17.066667)
        self.assertEqual((29, -4), (destination["w"], value["space"]))
        self.assertEqual(25, destination["w"]+value["space"])
        self.assertEqual("native-numeric-advance-quantization", gaps[0]["code"])
        self.assertTrue(gaps[0]["informational"])
        self.assertLess(abs((2*25+29) - (2*gaps[0]["sourceAdvance"]+gaps[0]["sourceCellWidth"])), .4)
        self.assertLess(gaps[0]["maxTenDigitSpanErrorPx"], 5)

    def test_non_overhanging_fonts_keep_zero_space_and_unsupported_variable_advance(self):
        for advance in (28, 30):
            font = deepcopy(self.font)
            for glyph in font["glyphs"]:
                glyph["advance"] = advance
            (value, destination), gaps = self.scaled(font=font)
            self.assertEqual(0, value["space"])
            self.assertEqual(advance, destination["w"])
            self.assertEqual([], gaps)
        font["glyphs"][0]["advance"] = 31
        result, gaps = self.scaled(font=font)
        self.assertIsNone(result)

    def test_near_integral_advance_keeps_exact_integer_font_bytes(self):
        font = deepcopy(self.font)
        for glyph in font["glyphs"]:
            glyph["advance"] = 29
        expected = _bitmap_font(font, self.resources, self.sink)
        files = {p.name: p.read_bytes() for p in (self.output / "assets").iterdir()}
        for advance in (28.99999999999997, 29 - 5e-10, 29 + 5e-10):
            with self.subTest(advance=advance):
                for glyph in font["glyphs"]:
                    glyph["advance"] = advance
                self.assertEqual(expected, _bitmap_font(font, self.resources, self.sink))
                self.assertEqual(files, {p.name: p.read_bytes() for p in (self.output / "assets").iterdir()})
                self.assertEqual(advance, font["glyphs"][0]["advance"])

    def test_fractional_or_negative_advance_is_still_rejected(self):
        for advance in (29.25, 29 - 2e-9, 29 + 2e-9, -5e-10):
            with self.subTest(advance=advance):
                font = deepcopy(self.font)
                font["glyphs"][0]["advance"] = advance
                with self.assertRaisesRegex(ValueError, "integral nonnegative advance"):
                    _bitmap_font(font, self.resources, self.sink)

    def test_minimum_bpm_binding_is_emitted_and_reading_ownership_skip_survives(self):
        text = {"id": "min-bpm", "type": "text", "properties": {
            "x": .56015625, "y": .87916667, "width": .054166667, "height": .022222222,
            "font-size": 17.066667, "bitmap-font": self.font["id"], "format": "integer",
            "alignment": "right", "scale-x": .76339286}}
        root = {"id": "information", "type": "container", "children": [text]}
        manifest = {"resources": list(self.resources.values()), "bitmapFonts": [self.font]}
        scene = {"root": root, "bindings": [{"id": "bpm-binding", "target": text["id"],
                 "property": "text", "source": "timing.bpm-min"}]}
        contribution = Contribution("frame", "iidx27", "manifest.json", None, None, manifest, scene, "")
        result = export_hud(contribution, root, (0, 0, 1920, 1080), self.sink)
        self.assertEqual(1, len(result["value"]))
        self.assertEqual("main_state.number(91)", result["mappings"][0]["native"])
        self.assertNotIn("unsupported-anisotropic-text-scale", [g.get("reason") for g in result["gaps"]])
        self.assertEqual(["native-numeric-advance-quantization"], [g.get("code") for g in result["gaps"]])
        skipped = export_hud(contribution, root, (0, 0, 1920, 1080), self.sink, skip_node_ids=["min-bpm"])
        self.assertEqual([], skipped["value"])
        self.assertEqual([], skipped["text"])

    def test_official_skin_number_aligns_actual_digits_and_scales_negative_space(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        cases = []
        for scale, size in (((1, 1), 12.8), ((.76339286, 1), 17.066667), ((.5, 1), 12.8)):
            for alignment in ("left", "centre", "right"):
                (value, destination), gaps = self.scaled(alignment=alignment, scale=scale, size=size)
                source_scale = size*1080/768/18*scale[0]
                cases.append({"name": str(scale)+" "+alignment,
                    "skin": {"value": [value], "destination": [{"id": value["id"], "dst": [{"time": 0, **destination}]}]},
                    "rect": [200.25, 300.125, 104*scale[0], 24], "alignment": alignment,
                    "sourceCell": 28*source_scale, "sourceAdvance": 25*source_scale,
                    "spanBound": gaps[0]["maxTenDigitSpanErrorPx"] if gaps else 0})
        recipe = self.output / "number-cases.lua"
        recipe.write_text("return "+lua(cases), encoding="utf-8")
        source = Path(__file__).with_name("HudNumberProbe.java")
        compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.folder), str(source)],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout+compiled.stderr)
        run = subprocess.run([java, "-cp", str(self.folder)+os.pathsep+str(jar),
                              "bms.player.beatoraja.skin.lua.HudNumberProbe", str(recipe)],
                             capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, run.returncode, run.stdout+run.stderr)
        receipt = json.loads(run.stdout)
        self.assertEqual(9, receipt["cases"])
        self.assertEqual(36, receipt["sentinelChecks"])
        self.assertGreater(receipt["glyphChecks"], 500)
        print("Native HUD number probe: "+json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
