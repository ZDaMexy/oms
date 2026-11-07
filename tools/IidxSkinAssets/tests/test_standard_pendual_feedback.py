"""22 source identity and caller-offset integration without private font/art."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from standard_feedback import build_feedback
from standard_source_catalog import SOURCE22_AVAILABLE_STYLES, frame_token
from test_standard_pendual_controls import composition, pendual_reference


def nodes(result):
    def walk(node):
        yield node
        for child in node["children"]:
            yield from walk(child)
    return [n for t in result["templates"] for n in walk(t["root"])]


class PendualFeedbackTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="pendual-feedback-")
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name).resolve()
        self.library = self.root / "library"
        self.library.mkdir()
        self.font_path = self.root / "source-font.bin"
        self.font_path.write_bytes(b"synthetic font outlines identity")
        self.panel_path = self.library / "original22/judge.png"
        self.panel_path.parent.mkdir()
        self.panel_path.write_bytes(b"synthetic original22 backplate")
        self.panel_sha = hashlib.sha256(self.panel_path.read_bytes()).hexdigest()
        self.font = {"resources": [], "bitmapFonts": [], "files": [], "fonts": {"normal": "fixture.normal", "small": "fixture.small"},
                     "labels": {}, "receipt": {"sourceFontSHA256": hashlib.sha256(self.font_path.read_bytes()).hexdigest()}}
        for size, width, height in (("normal", 12, 20), ("small", 8, 18)):
            for label in ("PG:", "GR:", "GD:", "BD:", "PR:", "CB:", "MAX", "300", "200", "100", "50", "MISS", "FA:", "/SL:", "FAST", "SLOW", "FA", "SL"):
                self.font["labels"][size, label] = {"resource": "fixture.label.n" + str(len(self.font["labels"])),
                                                   "width": len(label) * width, "height": height}
        self.inventory = {"status": "complete", "sourceMetadataUnchanged": True, "sourceComposition": composition(), "textures": [
            {"package": "contents/data/graphic/s22/gameparts.ifs", "name": "judge_waku.png", "width": 111, "height": 163,
             "sprite": "original22/judge.png", "sha256": self.panel_sha}]}

    def build(self, style, layout="p1", *, wide=False, offset=None, wrong_panel=False):
        reference = pendual_reference(style, layout)
        movie = ("center_frame_dp_1p" if layout == "dp" else "center_frame_wide_" + ("2p" if layout == "p2" else "1p")
                 if wide else "center_frame_sp")
        x, y = (119, 529) if layout == "dp" else (807, 474) if layout == "p2" else (362, 544)
        draw = {"texture": "judge_waku", "game_bindings": ["judge_2p" if layout == "p2" else "judge_1p"],
                "matrix": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": x, "ty": y},
                "bounds": {"left": x, "right": x + 111, "top": y, "bottom": y + 163},
                "asset": {"package": "s24/gameparts" if wrong_panel else "s22/gameparts", "width": 111, "height": 163,
                          "sha256": self.panel_sha}}
        reference["layouts"].setdefault(movie, {"draws": []})["draws"].append(draw)
        (self.library / "library.json").write_text(json.dumps(self.inventory), encoding="utf-8")
        path = self.root / "map.json"
        path.write_text(json.dumps(reference), encoding="utf-8")
        if offset is None:
            offset = 0 if layout == "dp" or wide else -35 if layout == "p2" else 35
        return build_feedback(self.library, path, style=style, layout=layout, centre_movie=movie, centre_dx=offset,
                              contexts=[c for c in material_contexts() if c["layout"] == layout],
                              font_path=self.font_path, output=self.root / "output", prepared_font=self.font)

    def test_each_source_uses_its22_panel_and35px_caller_offset(self):
        identities = set()
        for style in SOURCE22_AVAILABLE_STYLES:
            for layout, wide, x in (("p1", False, 397), ("p2", False, 772), ("p1", True, 362), ("dp", False, 119)):
                with self.subTest(style=style, layout=layout, wide=wide):
                    result = self.build(style, layout, wide=wide)
                    panel = next(n for n in nodes(result) if n["id"].endswith(".judge.bms.panel"))
                    self.assertAlmostEqual(x / 1280, panel["properties"]["x"])
                    self.assertIn("f" + frame_token(style) + ".", panel["id"])
                    self.assertEqual("s22/gameparts", result["receipt"]["judgeHook"]["asset"]["package"])
                    if layout == "p1" and not wide:
                        identities.add(panel["id"])
        self.assertEqual(5, len(identities), "Same-sized source panels must not collapse source identities.")

    def test_incomplete_retained_frame_is_not_a_playable_feedback_source(self):
        with self.assertRaisesRegex(ValueError, "qualified Standard cabinet"):
            self.build("source22-gmframe0")

    def test_later_family_or36px_offset_cannot_silently_supply22_feedback(self):
        for style in SOURCE22_AVAILABLE_STYLES:
            with self.subTest(style=style, fault="source-family"), self.assertRaises(ValueError):
                self.build(style, wrong_panel=True)
            with self.subTest(style=style, fault="later-caller-offset"), self.assertRaises(ValueError):
                self.build(style, offset=36)

    def test_true_counts_and_exact_stage_timing_keep_independent_preference_gates(self):
        result = self.build("source22-gmframe9", "dp")
        bms = [b["source"] for b in result["bindings"] if ".judge.bms." in b["target"] and b["property"] == "text"]
        self.assertEqual(["judgement.count." + grade for grade in ("perfect", "great", "good", "meh")] +
                         ["value.standard-poor", "combo.breaks", "judgement.count.fast", "judgement.count.slow"], bms)
        self.assertEqual([{"id": "standard-poor", "operation": "add", "left": "judgement.count.miss", "right": "judgement.count.ok"}],
                         result["numericSources"])
        preferences = [b for b in result["bindings"] if b["source"] == "option.realtime-judge"]
        self.assertEqual(2, len(preferences))
        self.assertTrue(all(b["target"].endswith(".preference") for b in preferences))
        timing = [v for v in result["variants"] if v["source"] == "judgement.timing-direction"]
        self.assertTrue(timing)
        self.assertTrue(any(".g0." in v["target"] for v in timing))
        self.assertTrue(any(".g1." in v["target"] for v in timing))
        height = [v for v in result["variants"] if v["source"] == "option.judgement-position"]
        self.assertTrue(all(v["default"] is False and all(c["key"] != "off" for c in v["cases"]) for v in height))
        self.assertTrue(all(not any(t["target"] == v["target"] and t["property"] == "opacity" for t in result["tracks"]) for v in height))

    def test_receipt_does_not_promote26_font_or_timing_coordinates_to22_native_fact(self):
        result = self.build("source22-gmframe0-01")
        receipt = result["receipt"]
        self.assertEqual(26, receipt["nativeEvidence"]["softwareVersion"])
        self.assertIn("not asserted as22", receipt["fastSlow"])
        self.assertTrue(any("482px" in item and "484px" in item for item in receipt["limits"]))
        self.assertEqual(35, receipt["callerCentreOffset"])


if __name__ == "__main__":
    unittest.main()
