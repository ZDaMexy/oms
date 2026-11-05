"""Source cover layout and live-state boundaries, using no private assets."""
from pathlib import Path
import re
import sys
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_standard_skin import Frame, document
from standard_cover_readout import add_cover_readouts


def walk(items):
    for item in items:
        yield item
        yield from walk(item.get("children", []))


class SyntheticFrame(Frame):
    def __init__(self, layout):
        self.layout, self.prefix = layout, "synthetic." + layout
        self.owner = SimpleNamespace()
        self.scene = document(self.prefix + ".root")
        self.info, self.evidence = [], []


class StandardCoverReadoutTests(unittest.TestCase):
    def make(self, layout="p1"):
        frame = SyntheticFrame(layout)
        add_cover_readouts(frame, {"fonts": {"normal": "synthetic.font2", "small": "synthetic.font5"}})
        return frame

    @staticmethod
    def ending(frame, suffix):
        return next(item for item in walk(frame.info) if item["id"].endswith(suffix))

    @staticmethod
    def track(frame, suffix, prop):
        return next(item for item in frame.scene["tracks"] if item["target"].endswith(suffix) and item["property"] == prop)

    def test_source_software_anchors_do_not_follow_centre_translation(self):
        for layout, expected in (("p1", [129]), ("p2", [1021]), ("dp", [397, 767])):
            with self.subTest(layout=layout):
                frame = self.make(layout)
                for index, x in enumerate(expected, 1):
                    item = self.ending(frame, f".p{index}.sudden.normal")
                    self.assertEqual(x / 1280, item["properties"]["x"])
                    self.assertEqual(104 / 1280, self.ending(frame, f".p{index}.sudden.normal.green")["properties"]["x"])
                clip = self.ending(frame, ".source-clip")
                self.assertEqual("clip", clip["type"])
                self.assertEqual({"height": 482 / 720, "clip-mode": "bounds"}, clip["properties"])

    def test_inactive_cover_values_and_panel_preference_cannot_fake_visibility(self):
        frame = self.make()
        sources = {binding["target"]: binding["source"] for binding in frame.scene["bindings"]}
        for suffix, source in ((".sudden.normal.active", "cover.sudden-active"),
                               (".hidden.normal.active", "cover.hidden-active"),
                               (".lift.normal.active", "lift.available"),
                               (".adjusting", "scroll.adjusting"), (".available", "scroll.available")):
            self.assertEqual(source, sources[self.ending(frame, suffix)["id"]])
        self.assertNotIn("option.start-panel", sources.values())
        self.assertNotIn("preparation.active", sources.values())

    def test_positions_follow_units_and_applied_lift_not_configuration(self):
        frame = self.make()
        for suffix, clock, times, values in (
                (".sudden.normal", "cover.sudden-units", [0, 1000], [-26 / 482, 458 / 482]),
                (".hidden.normal", "cover.hidden-units", [0, 1000], [484 / 482, 0]),
                (".lift.normal", "lift.offset", [0, 1], [484 / 482, 0]),
                (".lift.reversed", "lift.offset", [0, 1], [-26 / 482, 458 / 482])):
            with self.subTest(suffix=suffix):
                track = self.track(frame, suffix, "y")
                self.assertEqual(clock, track["clock"])
                self.assertEqual(times, [key["time"] for key in track["keyframes"]])
                self.assertEqual(values, [key["value"] for key in track["keyframes"]])
        variants = [item for item in frame.scene["variants"] if item["source"] == "lift.reversed"]
        self.assertEqual(["false", "true"], [item["cases"][0]["key"] for item in variants])

    def test_native_padding_and_centred_range_keep_actual_endpoint_sources(self):
        frame = self.make()
        value = self.ending(frame, ".sudden.normal.green.value")
        track = self.track(frame, ".sudden.normal.green.value", "x")
        self.assertEqual([6 / 1280, -6 / 1280, -18 / 1280, -24 / 1280],
                         [key["value"] for key in track["keyframes"][:4]])
        self.assertEqual("synthetic.font2", value["properties"]["bitmap-font"])
        range_root = self.ending(frame, ".sudden.normal.range")
        self.assertEqual(-16 / 482, range_root["properties"]["y"])
        self.assertEqual(22 / 482, self.ending(frame, ".hidden.normal.range")["properties"]["y"])
        bound = {item["target"]: item["source"] for item in frame.scene["bindings"]}
        self.assertEqual("scroll.green-number-range-available", bound[range_root["id"]])
        self.assertEqual("scroll.green-number-max", bound[self.ending(frame, ".sudden.normal.range.max")["id"]])
        self.assertEqual("scroll.green-number-min", bound[self.ending(frame, ".sudden.normal.range.min")["id"]])
        self.assertEqual("synthetic.font5", self.ending(frame, ".sudden.normal.range.hyphen")["properties"]["bitmap-font"])

    def test_public_shape_and_identifiers_require_no_new_artwork(self):
        frame = self.make("dp")
        identifier = re.compile(r"^[a-zA-Z][a-zA-Z0-9-]*(?:\.[a-zA-Z][a-zA-Z0-9-]*)*$")
        for item in walk(frame.info):
            self.assertRegex(item["id"], identifier)
            self.assertNotIn("resource", item)
            self.assertNotIn("y-px", item["properties"])
        for track in frame.scene["tracks"]:
            self.assertIn("keyframes", track)
            self.assertNotIn("frames", track)
            for key in track["keyframes"]:
                self.assertEqual({"id", "time", "value"}, set(key))
                self.assertRegex(key["id"], identifier)
        self.assertEqual([], frame.scene["templates"])
        self.assertEqual([], frame.scene["instances"])


if __name__ == "__main__":
    unittest.main()
