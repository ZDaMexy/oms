"""Reading-state geometry regressions using only synthetic textures and fonts."""
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_standard_skin import Frame, document, node
from standard_reading import add_arrival, add_preview_background, add_reading


def walk(nodes):
    for value in nodes:
        yield value
        yield from walk(value.get("children", []))


class SyntheticFrame(Frame):
    def __init__(self, output, layout):
        self.layout = layout
        self.dp, self.p2 = layout == "dp", layout == "p2"
        self.prefix = "synthetic." + layout
        self.owner = SimpleNamespace(output=output, reading_labels={}, files={})
        self.resources, self.animations = {}, {}
        self.scene = document(self.prefix + ".root")
        self.info, self.evidence = [], []
        self.requested = []

    def asset(self, package, name):
        dimensions = {
            "playm_spd_window": (101, 304), "playm_spd2_window": (101, 304),
            "playm_spd_window_dp": (88, 376), "playm_spd2_window_dp": (88, 376),
            "playm_txt_spd": (14, 168), "playm_txt_spd2": (14, 184),
            "playm_spd_sel": (66, 27), "txt_current_spd": (89, 24),
            "txt_spd2_10": (46, 19), "txt_spd2_05": (39, 19), "slider_bar": (45, 10),
            "playm_spd_pv_waku": (68, 520), "playm_spd_pv_lane": (38, 482),
            "playm_spd_pv_red_line": (38, 5),
            "playm_spd_pv_bpm_gra_bar": (38, 27),
            "start_notes_w": (34, 180), "start_notes_b": (26, 180), "start_notes_s": (60, 180),
        }
        self.requested.append(name)
        width, height = dimensions[name]
        identifier = "synthetic.asset." + name
        return self.resource({"id": identifier, "type": "texture", "path": identifier + ".png"}), {"width": width, "height": height}


class StandardReadingTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.output = Path(self.temporary.name)

    def build(self, layout="p1"):
        frame = SyntheticFrame(self.output, layout)
        ready = {layout: {"resources": [], "animations": [], "node": node("synthetic.ready"), "source": {"synthetic": True}}}
        font = {"fonts": {"normal": "synthetic.font2", "small": "synthetic.font5"}}
        with patch("standard_feedback.rasterize_source_text", return_value=Image.new("RGBA", (84, 20))):
            add_reading(frame, ready, font, self.output / "unused-font")
        return frame

    @staticmethod
    def suffix(nodes, suffix):
        return next(n for n in walk(nodes) if n["id"].endswith(suffix))

    def test_normal_pages_show_actual_twenty_levels_in_original_ten_rows(self):
        frame = self.build()
        for page, expected in ((0, list(range(10, 0, -1))), (1, list(range(20, 10, -1)))):
            actual = [int(self.suffix(frame.info, f".normal.page{page}.row{i}")["properties"]["text"]) for i in range(10)]
            self.assertEqual(expected, actual)
            track = next(t for t in frame.scene["tracks"] if t["target"].endswith(f".normal.page{page}.selected"))
            self.assertEqual("scroll.speed", track["clock"])
            self.assertEqual(list(range(1 + page * 10, 11 + page * 10)), [f["time"] for f in track["keyframes"]])
        self.assertTrue(all(t["type"] == "tween" and "keyframes" in t and "frames" not in t for t in frame.scene["tracks"]))

    def test_original_green_range_uses_max_then_min_and_native_side_anchors(self):
        for layout, anchor in (("p1", (356, 141)), ("p2", (927, 141)), ("dp", (668, 10))):
            with self.subTest(layout=layout):
                frame = self.build(layout)
                root = self.suffix(frame.info, ".green.range")
                self.assertEqual({"x": anchor[0] / 1280, "y": anchor[1] / 720}, root["properties"])
                bindings = {b["target"]: b["source"] for b in frame.scene["bindings"]}
                self.assertEqual("scroll.green-number-range-available", bindings[root["id"]])
                self.assertEqual("scroll.green-number-max", bindings[self.suffix(frame.info, ".green.max")["id"]])
                self.assertEqual("scroll.green-number-min", bindings[self.suffix(frame.info, ".green.min")["id"]])
                self.assertEqual("synthetic.font5", self.suffix(frame.info, ".green.hyphen")["properties"]["bitmap-font"])
                if layout == "p2":
                    track = next(t for t in frame.scene["tracks"] if t["target"].endswith(".green.tail"))
                    self.assertEqual("scroll.green-number-min", track["clock"])
                    self.assertEqual(-24 / 1280, next(f["value"] for f in track["keyframes"] if f["time"] == 100))

    def test_dp_floating_keeps_baked_labels_and_exact_slider_travel(self):
        frame = self.build("dp")
        for unused in ("txt_current_spd", "txt_spd2_10", "txt_spd2_05"):
            self.assertNotIn(unused, frame.requested)
        value = self.suffix(frame.info, ".floating.value")
        self.assertEqual(615 / 1280, value["properties"]["x"])
        slider = next(t for t in frame.scene["tracks"] if t["target"].endswith(".floating.slider"))
        self.assertEqual([(0.5, 244 / 720), (10, 70 / 720)], [(f["time"], f["value"]) for f in slider["keyframes"]])
        upper = self.suffix(frame.info, ".classic.upper")
        self.assertEqual("clip", upper["type"])
        self.assertEqual(300 / 720, upper["properties"]["height"])
        self.assertEqual(376 / 300, upper["children"][0]["properties"]["height"])
        self.assertEqual("synthetic.asset.playm_spd2_window_dp", upper["children"][0]["resource"])
        self.assertTrue(all(set(r) <= {"id", "type", "path", "filter"} for r in frame.resources.values()))

    def test_normal_preview_has_independent_start_gate_and_no_frozen_dynamic_bar(self):
        for layout, note_x in (("p1", 304), ("p2", 942), ("dp", 686)):
            frame = SyntheticFrame(self.output, layout)
            children = []
            add_preview_background(frame, children)
            self.assertEqual({"scroll.adjusting", "scroll.available", "scroll.hi-speed-available", "scroll.preview-available"},
                             {b["source"] for b in frame.scene["bindings"]})
            self.assertEqual("scroll.mode", frame.scene["variants"][0]["source"])
            self.assertEqual("normal", frame.scene["variants"][0]["cases"][0]["key"])
            self.assertIn("playm_spd_pv_bpm_gra_bar", frame.requested)
            self.assertEqual([note_x, 0, 34, 477], frame.evidence[0]["normalSpeedPreview"]["sourceNoteArea"])
            self.assertEqual("lift.offset", frame.scene["tracks"][0]["clock"])

    def test_preview_bar_keeps_source_thirty_pixel_pivot_and_actual_driver_phase(self):
        frame = SyntheticFrame(self.output, "p1")
        children = []
        add_preview_background(frame, children)
        back, front = self.suffix(children, ".bpm.back"), self.suffix(children, ".bpm.front")
        self.assertEqual(back["resource"], front["resource"])
        self.assertEqual(.5, back["properties"]["opacity"])
        self.assertEqual(27 / 720, front["properties"]["height"])
        tracks = {t["property"]: t for t in frame.scene["tracks"] if t["target"] == front["id"]}
        self.assertEqual({"y", "height", "opacity"}, set(tracks))
        self.assertTrue(all(t["clock"] == "scroll.preview-phase" and not t["loop"] for t in tracks.values()))
        self.assertEqual([455 / 720, 485 / 720], [k["value"] for k in tracks["y"]["keyframes"]])
        self.assertEqual([27 / 720, 0], [k["value"] for k in tracks["height"]["keyframes"]])
        intensity = tracks["opacity"]["keyframes"]
        self.assertEqual(76, len(intensity))
        self.assertEqual(.75, intensity[0]["value"])
        self.assertEqual(0, intensity[-1]["value"])
        self.assertAlmostEqual(1 / 75, intensity[1]["time"])
        self.assertEqual(.74, intensity[1]["value"])

    def test_arrival_uses_active_sudden_and_fixed_source_clip_without_preparation_cut(self):
        frame = SyntheticFrame(self.output, "dp")
        add_arrival(frame)
        roots = [t["root"] for t in frame.scene["templates"]]
        self.assertEqual(3, len(roots))
        self.assertNotIn("preparation.active", {b["source"] for b in frame.scene["bindings"]})
        self.assertIn("arrival.visible", {b["source"] for b in frame.scene["bindings"]})
        for colour in ("white", "black", "scratch"):
            clip = self.suffix(roots, f".{colour}.source-clip")
            self.assertEqual("clip", clip["type"])
            self.assertEqual(482 / 484, clip["properties"]["height"])
            for branch in ("sudden", "uncovered"):
                art = self.suffix(roots, f".{colour}.{branch}.art")
                self.assertEqual(180 / 482, art["properties"]["height"])
                self.assertAlmostEqual(180 / 484, art["properties"]["height"] * clip["properties"]["height"])
            variants = [v for v in frame.scene["variants"] if f".{colour}." in v["target"]]
            self.assertEqual({"false", "true"}, {v["cases"][0]["key"] for v in variants})
            self.assertTrue(all(v["source"] == "cover.sudden-active" for v in variants))
        self.assertEqual({"cover.sudden-units"}, {t["clock"] for t in frame.scene["tracks"]})
        self.assertTrue(all(t["keyframes"][-1]["time"] == 1000 and t["keyframes"][-1]["value"] == 484 / 482 for t in frame.scene["tracks"]))


if __name__ == "__main__":
    unittest.main()
