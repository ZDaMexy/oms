"""Own20 four-cell totals follow native zero alpha and the existing host fit."""
import unittest

import test_standard20_graph_reading as source20_fixture
from test_standard_graph import evaluate, state
from standard_graph import LOGICAL, TARGETS
from standard_source_catalog import presentation_family


CASES = ((0, "0000"), (9, "0009"), (10, "0010"), (99, "0099"),
         (100, "0100"), (999, "0999"), (1000, "1000"))


def step(track, value):
    result = track["keyframes"][0]["value"]
    for point in track["keyframes"]:
        if value < point["time"]:
            break
        result = point["value"]
    return result


class Standard20GraphLeadingZeroTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = source20_fixture.Standard20GraphTests()
        cls.fixture.setUp()
        cls.addClassCleanup(cls.fixture.doCleanups)

    def field(self, layout, target=None):
        value = self.fixture.build("s20", layout, target)
        nodes = source20_fixture.graph_nodes(value)
        identity = (f"standard.graph.fs20.{layout}.current.value" if target is None else
                    f"standard.graph.fs20.target.{target}.{layout}.total")
        number = nodes[identity]
        grid = nodes[identity + ".leading-zeros"]
        zeros = [nodes[identity + f".leading-zero.n{i}"] for i in range(3)]
        source, = (b["source"] for b in value["bindings"]
                   if b["target"] == identity and b["property"] == "text")
        tracks = {(t["target"], t["property"]): t for t in value["tracks"]}
        return value, nodes, number, grid, zeros, source, tracks

    def displayed(self, number, zeros, tracks, actual):
        prefix = "".join("0" for zero in zeros
                         if step(tracks[zero["id"], "opacity"], actual) == 1)
        self.assertEqual("integer", number["properties"]["format"])
        return prefix + str(actual)

    def test_you_uses_three_source_zero_glyphs_with_bright_integer_boundaries(self):
        for layout in ("p1", "p2", "dp"):
            value, nodes, number, grid, zeros, source, tracks = self.field(layout)
            self.assertEqual("score.value", source)
            self.assertEqual([number, grid], nodes[number["id"] + ".fixed-cells"]["children"])
            glyph_zero = next(g for font in value["bitmapFonts"] if font["id"].endswith(".font.score")
                              for g in font["glyphs"] if g["character"] == "0")
            self.assertEqual(3, len([n for n in nodes if ".leading-zero.n" in n]))
            for index, zero in enumerate(zeros):
                self.assertEqual("sprite", zero["type"])
                self.assertEqual(glyph_zero["resource"], zero["resource"])
                track = tracks[zero["id"], "opacity"]
                self.assertEqual(("score.value", "step", False),
                                 (track["clock"], track["easing"], track["loop"]))
                self.assertEqual([(0, 1), (10 ** (3 - index), 0)],
                                 [(p["time"], p["value"]) for p in track["keyframes"]])
            for actual, expected in CASES:
                with self.subTest(layout=layout, score=actual):
                    self.assertEqual(expected, self.displayed(number, zeros, tracks, actual))

    def test_all_pacemaker_choices_use_their_own_actual_total_clock(self):
        for layout in ("p1", "p2", "dp"):
            for target in TARGETS:
                value, nodes, number, grid, zeros, source, tracks = self.field(layout, target)
                with self.subTest(layout=layout, target=target):
                    self.assertEqual(value["receipt"]["targetTotalSource"], source)
                    self.assertTrue(all(tracks[z["id"], "opacity"]["clock"] == source for z in zeros))
                    self.assertEqual(3, len([n for n in nodes if ".leading-zero.n" in n]))
                    actual = evaluate(value, state())[source]
                    self.assertEqual(str(actual).zfill(4), self.displayed(number, zeros, tracks, actual))
            value, _, number, _, zeros, source, tracks = self.field(layout, "custom")
            for actual, expected in CASES:
                with self.subTest(layout=layout, customTotal=actual):
                    sources = evaluate(value, state(maximum=actual, percent=100))
                    self.assertEqual(actual, sources[source])
                    self.assertEqual(expected, self.displayed(number, zeros, tracks, sources[source]))

    def test_zeros_keep_the_same_right_edge_and_fit_as_the_significant_digits(self):
        for layout in ("p1", "p2", "dp"):
            for target in (None, "aaa", "custom", "next", "next-plus"):
                _, _, number, grid, zeros, _, tracks = self.field(layout, target)
                font = tracks[number["id"], "font-size"]
                right = (0 if layout == "p2" else 1000) + 267
                top = 30 if target is None else 57
                self.assertEqual("centre-right", grid["properties"]["origin"])
                for maximum in (9999, 10000, 100000, 1000000):
                    factor = step(font, maximum) / (22 * LOGICAL)
                    for axis in ("x", "y"):
                        fit = tracks[grid["id"], "scale-" + axis]
                        self.assertEqual(("score.maximum", "step", False),
                                         (fit["clock"], fit["easing"], fit["loop"]))
                        self.assertAlmostEqual(factor, step(fit, maximum))
                    props = grid["properties"]
                    scaled_width, scaled_height = props["width"] * 1280 * factor, props["height"] * 720 * factor
                    left = props["x"] * 1280 - scaled_width
                    self.assertAlmostEqual(right, left + scaled_width)
                    # Core's right text alignment is CentreRight, including
                    # when the original font-size fit shrinks its natural box.
                    self.assertAlmostEqual(top + (22 - 22 * factor) / 2,
                                           props["y"] * 720 - scaled_height / 2)
                    self.assertAlmostEqual(89 * factor, scaled_width)
                    self.assertAlmostEqual(22 * factor, scaled_height)
                    for index, zero in enumerate(zeros):
                        self.assertAlmostEqual(right - (89 - index * 22) * factor,
                                               left + zero["properties"]["x"] * scaled_width)
                        self.assertAlmostEqual(23 * factor, zero["properties"]["width"] * scaled_width)
                        self.assertAlmostEqual(22 * factor, zero["properties"]["height"] * scaled_height)
                    for actual, expected in CASES:
                        with self.subTest(layout=layout, target=target, maximum=maximum, score=actual):
                            self.assertEqual(expected, self.displayed(number, zeros, tracks, actual))
                            active = [zero for zero in zeros
                                      if step(tracks[zero["id"], "opacity"], actual) == 1]
                            if active:
                                # Core measures the existing main text by
                                # advance, retaining its right-aligned box.
                                last = active[-1]["properties"]
                                zero_right = left + (last["x"] + last["width"]) * scaled_width
                                self.assertAlmostEqual(right - len(str(actual)) * 22 * factor, zero_right)
                for actual in (10000, 100000):
                    self.assertEqual(str(actual), self.displayed(number, zeros, tracks, actual))

    def test_host_pb_differences_and_all_18_retained_themes_do_not_acquire_padding(self):
        value, nodes, _, _, _, _, tracks = self.field("p1")
        for suffix in (".best.value", ".best.difference"):
            readout, = (n for n in nodes.values() if n["id"].endswith(suffix))
            self.assertEqual("integer", readout["properties"]["format"])
            self.assertNotIn(readout["id"] + ".fixed-cells", nodes)
            self.assertEqual("score.maximum", tracks[readout["id"], "font-size"]["clock"])
        self.assertEqual(1, value["receipt"]["scoreFont"]["unusedHighDigitAlphaFactor"])
        self.assertIn("clamps to9999", value["receipt"]["scoreFont"]["largeScoreAdaptation"])
        self.assertEqual(18, len(self.fixture.old_styles))
        for style in self.fixture.old_styles:
            family = presentation_family(style)
            for layout in ("p1", "p2", "dp"):
                for target in (None, *TARGETS):
                    value = self.fixture.build(family, layout, target)
                    with self.subTest(style=style, layout=layout, target=target):
                        self.assertEqual(self.fixture.before[family, layout, target], value)
                        self.assertFalse(any(".leading-zero" in n for n in source20_fixture.graph_nodes(value)))


if __name__ == "__main__":
    unittest.main()
