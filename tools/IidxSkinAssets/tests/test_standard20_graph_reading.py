"""Original20 Graph and reading contracts, using synthetic source identities."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard21_graph as source21_fixture
from test_standard_reading import SyntheticFrame, walk
from test_standard_cover_readout import SyntheticFrame as CoverFrame
from standard_cover_readout import add_cover_readouts
from standard_graph import TARGETS, build_graph, build_graph_target
from standard_reading import add_arrival, add_preview_background, add_reading
from standard_source_catalog import (COMPOSITION20, SOURCE20_ALIASES, SOURCE20_STYLES,
                                     package_key, presentation_family, styles)


def graph_nodes(value):
    return {n["id"]: n for t in value["templates"] for n in walk([t["root"]])}


class Standard20GraphTests(unittest.TestCase):
    def setUp(self):
        fixture = source21_fixture.Standard21GraphTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.library, self.font, self.metadata = fixture.library, fixture.font, fixture.metadata
        self.old_styles = styles(self.metadata)
        self.before = {(family, layout, target): self.build(family, layout, target)
                       for family in ("0", "1", "s23", "s24", "s22", "s21")
                       for layout in ("p1", "p2", "dp") for target in (None, *TARGETS)}
        self.metadata["sourceComposition"].update(contract=COMPOSITION20, aliases20=SOURCE20_ALIASES)
        self.metadata["packages"].extend({"source": f"contents/data/graphic/{package_key(style)}.ifs"}
                                        for style in SOURCE20_STYLES)
        own = "contents/data/graphic/s20/graph.ifs"
        rows = []
        for original in self.metadata["textures"]:
            if original["package"] != "contents/data/graphic/s21/graph.ifs":
                continue
            row = copy.deepcopy(original)
            row["package"] = own
            row["sprite"] = row["sprite"].replace("synthetic-s21", "synthetic-s20")
            path = self.library / row["sprite"]
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(("independent20 fixture:" + row["name"]).encode())
            row["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
            rows.append(row)
        self.metadata["textures"].extend(rows)
        archive = self.library / "source-copies" / own
        archive.parent.mkdir(parents=True)
        archive.write_bytes(b"independent20 synthetic graph archive")
        self.metadata["packages"].append({"source": own, "sha256": hashlib.sha256(archive.read_bytes()).hexdigest()})
        xml = self.library / "packages/contents/data/graphic/s20/graph/raw/tex/texturelist.xml"
        xml.parent.mkdir(parents=True)
        xml.write_bytes((self.library / "packages/contents/data/graphic/s21/graph/raw/tex/texturelist.xml").read_bytes())
        normal = copy.deepcopy(self.font["bitmapFonts"][0])
        normal.update(id="fixture.font.normal", lineHeight=20)
        for glyph in normal["glyphs"]:
            glyph["advance"] = 12
        self.font["fonts"]["normal"] = normal["id"]
        self.font["bitmapFonts"].append(normal)
        self.write_metadata()

    def write_metadata(self):
        (self.library / "library.json").write_text(json.dumps(self.metadata), "utf-8")

    def build(self, family, layout, target=None):
        if target is None:
            return build_graph(self.library, layout, self.font, common_family=family)
        return build_graph_target(self.library, layout, target, self.font, common_family=family)

    def test_20_uses_own_graph_pixels_and_independently_qualified_geometry(self):
        for layout in ("p1", "p2", "dp"):
            value = self.build("s20", layout)
            nodes = graph_nodes(value)
            prefix = f"standard.graph.fs20.{layout}"
            left = 0 if layout == "p2" else 1000
            current = nodes[prefix + ".current.value"]["properties"]
            self.assertEqual(((left + 178) / 1280, 30 / 720, 89 / 1280),
                             (current["x"], current["y"], current["width"]))
            font, = (f for f in value["bitmapFonts"] if f["id"].endswith(".font.score"))
            self.assertEqual({22}, {g["advance"] for g in font["glyphs"]})
            for rank in ("a", "aa", "aaa"):
                line = nodes[prefix + ".line." + rank]["properties"]
                self.assertEqual((left + 5) / 1280, line["x"])
                track = next(t for t in value["tracks"] if t["clock"] == f"score.rank-{rank}-progress")
                self.assertEqual([529 / 720, 79 / 720], [k["value"] for k in track["keyframes"]])
            self.assertFalse(any(n.endswith((".line.maximum", ".line.baseline")) for n in nodes))
            font_paths = {row["path"] for row in self.font["files"]}
            self.assertTrue(all("/fs20/" in row["path"] or row["path"] in font_paths for row in value["files"]))
            receipt = value["receipt"]
            self.assertEqual("supplied IIDX20 original2013 x86 DLL", receipt["callerBasis"]["software"])
            self.assertNotIn("supplied24", json.dumps(receipt))

    def test_20_score_width_uses_native_advance_and_auxiliary_differences_use_font2(self):
        for target in (None, *TARGETS):
            value = self.build("s20", "p1", target)
            nodes = graph_nodes(value)
            number = next(n for n in nodes.values() if n["id"].endswith(".current.value" if target is None else ".total"))
            track = next(t for t in value["tracks"] if t["target"] == number["id"] and t["property"] == "font-size")
            self.assertAlmostEqual(22 * (768 / 720) * 89 / (4 * 22 + 23),
                next(k["value"] for k in track["keyframes"] if k["time"] == 10_000))
            difference = next(n for n in nodes.values() if n["id"].endswith(".best.difference" if target is None else ".difference"))
            self.assertEqual("fixture.font.normal", difference["properties"]["bitmap-font"])
            self.assertEqual(20 / 720, difference["properties"]["height"])
            self.assertNotIn("fixture.font.small", {f["id"] for f in value["bitmapFonts"]})
            if target is not None:
                self.assertIn("not assert a reconstruction of20", value["receipt"]["policyBasis"])
                self.assertIn("priorities58/57", value["receipt"]["cutins"]["layers"])

    def test_20_requires_qualified_font2_cells_and_never_falls_back_on_missing_art(self):
        font = next(f for f in self.font["bitmapFonts"] if f["id"] == "fixture.font.normal")
        font["glyphs"][0]["advance"] = 8
        with self.assertRaisesRegex(ValueError, "12x20 font2"):
            self.build("s20", "p1")
        self.build("s21", "p1")
        font["glyphs"][0]["advance"] = 12
        row = next(r for r in self.metadata["textures"] if r["package"].endswith("/s20/graph.ifs") and r["name"] == "gr_line_aaa.png")
        self.metadata["textures"].remove(row)
        self.write_metadata()
        with self.assertRaises(KeyError):
            self.build("s20", "p1")
        self.build("s21", "p1")

    def test_all_18_retained_themes_keep_graph_and_target_declarations_exact(self):
        self.assertEqual(18, len(self.old_styles))
        for style in self.old_styles:
            family = presentation_family(style)
            for layout in ("p1", "p2", "dp"):
                for target in (None, *TARGETS):
                    self.assertEqual(self.before[family, layout, target], self.build(family, layout, target))


class Standard20ReadingTests(unittest.TestCase):
    def test_all_three_layouts_use_20_current_only_green_and_actual_start_hooks(self):
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            for layout, anchor in (("p1", (356, 157)), ("p2", (867, 157)), ("dp", (614, 6))):
                frame = SyntheticFrame(output, layout)
                frame.style = SOURCE20_STYLES[0]
                original_asset = frame.asset
                packages = []
                def asset(package, name):
                    packages.append(package)
                    return original_asset(package, name)
                frame.asset = asset
                ready = {layout: {"resources": [], "animations": [], "node": {"id": "source20.ready"}, "source": {"family": "s20"}}}
                with patch("standard_feedback.rasterize_source_text", return_value=Image.new("RGBA", (84, 20))):
                    add_reading(frame, ready, {"fonts": {"normal": "qualified20.font2"}}, output / "font")
                self.assertEqual({"s20/gameparts"}, set(packages))
                nodes = {n["id"]: n for n in walk(frame.info)}
                green = next(n for n in nodes.values() if n["id"].endswith(".green"))
                self.assertEqual((anchor[0] / 1280, anchor[1] / 720),
                                 (green["properties"]["x"], green["properties"]["y"]))
                self.assertEqual("#00ff00ff", green["properties"]["colour"])
                sources = {b["source"] for b in frame.scene["bindings"]}
                self.assertIn("scroll.green-number", sources)
                self.assertFalse(any(s.startswith("scroll.green-number-") for s in sources))
                self.assertFalse(any(n.endswith(".green.range") for n in nodes))
                self.assertEqual({"qualified20.font2"}, {n["properties"]["bitmap-font"] for n in nodes.values() if "bitmap-font" in n.get("properties", {})})
                receipt = frame.evidence[-1]["reading"]
                self.assertEqual("%4d", receipt["callerBasis"]["greenFormat"])
                self.assertEqual([12, 20], receipt["fontLayout"]["cell"])
                self.assertFalse(receipt["fontLayout"]["nativeNaturalPenQualified"])
                self.assertFalse(receipt["sourceCapabilities"]["greenRange"])
                self.assertEqual(1500, frame.scene["preparationDuration"])
                self.assertNotIn("audited26", receipt["nativeReferenceBoundary"])
                if layout == "dp":
                    self.assertNotIn("txt_current_spd", frame.requested)

    def test_20_cannot_request_later_preview_or_arrival_art(self):
        with tempfile.TemporaryDirectory() as directory:
            frame = SyntheticFrame(Path(directory), "p1")
            frame.style = SOURCE20_STYLES[0]
            with self.assertRaisesRegex(ValueError, "no qualified dedicated Normal"):
                add_preview_background(frame, [])
            with self.assertRaisesRegex(ValueError, "no qualified first-row"):
                add_arrival(frame)
            self.assertEqual([], frame.requested)


class Standard20CoverReadoutTests(unittest.TestCase):
    def test_20_cover_callers_keep_current_only_font2_and_own_fixed_anchors(self):
        for layout, anchors in (("p1", [129]), ("p2", [1021]), ("dp", [397, 767])):
            frame = CoverFrame(layout)
            add_cover_readouts(frame, {"fonts": {"normal": "qualified20.font2"}}, source_family="s20")
            nodes = list(walk(frame.info))
            for index, x in enumerate(anchors, 1):
                row = next(n for n in nodes if n["id"].endswith(f".p{index}.sudden.normal"))
                self.assertEqual(x / 1280, row["properties"]["x"])
                green = next(n for n in nodes if n["id"].endswith(f".p{index}.sudden.normal.green"))
                track = next(t for t in frame.scene["tracks"] if t["target"] == green["id"] and t["property"] == "x")
                self.assertEqual([(0, 90 / 1280), (1000, 88 / 1280)],
                                 [(k["time"], k["value"]) for k in track["keyframes"]])
            self.assertEqual({"qualified20.font2"}, {n["properties"]["bitmap-font"] for n in nodes if "bitmap-font" in n.get("properties", {})})
            self.assertFalse(any(".range" in n["id"] for n in nodes))
            self.assertFalse(any(b["source"].startswith("scroll.green-number-") for b in frame.scene["bindings"]))
            receipt = frame.evidence[-1]["coverReadouts"]
            self.assertEqual([12, 20], receipt["fontLayout"]["cell"])
            self.assertFalse(receipt["fontLayout"]["nativeNaturalPenQualified"])
            self.assertEqual(anchors, receipt["whiteAnchors"])
            self.assertIn("10066e60", receipt["sourceCaller"])
            self.assertEqual([0, 0, 1280, 482], receipt["callerBasis"]["clipRect"])
            self.assertIn("0..1000", receipt["actualUnitsAdaptation"])


if __name__ == "__main__":
    unittest.main()
