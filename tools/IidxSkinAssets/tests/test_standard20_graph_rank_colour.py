"""Own20 rank colours use existing numeric opacity, with no new Core contract."""
import unittest

import test_standard20_graph_reading as source20_fixture
from test_standard20_graph_leading_zero import step
from test_standard_graph import evaluate, state
from standard_graph import TARGETS


class Standard20GraphRankColourTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.fixture = source20_fixture.Standard20GraphTests()
        cls.fixture.setUp()
        cls.addClassCleanup(cls.fixture.doCleanups)

    def test_only_the_three_original20_lines_use_the_qualified_colour_program(self):
        for layout in ("p1", "p2", "dp"):
            value = self.fixture.build("s20", layout)
            nodes = source20_fixture.graph_nodes(value)
            self.assertFalse(any(t["property"] == "colour" for t in value["tracks"]))
            for rank in ("aaa", "aa", "a"):
                identity = f"standard.graph.fs20.{layout}.line.{rank}"
                before, reached = nodes[identity], nodes[identity + ".reached"]
                self.assertEqual(("#8787ffff", 1),
                                 (before["properties"]["colour"], before["properties"]["opacity"]))
                self.assertEqual(("#ffff00ff", 0),
                                 (reached["properties"]["colour"], reached["properties"]["opacity"]))
                self.assertEqual(before["resource"], reached["resource"])
                self.assertEqual("standard.graph.fs20.texture.gr-line-" + rank, before["resource"])
                clocks = []
                for line, expected in ((before, [(0, 0), (1, 1)]), (reached, [(0, 1), (1, 0)])):
                    track, = (t for t in value["tracks"] if t["target"] == line["id"] and t["property"] == "opacity")
                    self.assertEqual(("tween", "step", False),
                                     (track["type"], track["easing"], track["loop"]))
                    self.assertEqual(expected, [(p["time"], p["value"]) for p in track["keyframes"]])
                    clocks.append(track["clock"])
                self.assertEqual(clocks[0], clocks[1])
                source, = (n for n in value["numericSources"] if "value." + n["id"] == clocks[0])
                self.assertEqual(("subtract", "score.rank-" + rank, "score.value"),
                                 (source["operation"], source["left"], source["right"]))

    def test_native_before_and_reached_colours_switch_at_the_actual_integer_threshold(self):
        for layout in ("p1", "p2", "dp"):
            value = self.fixture.build("s20", layout)
            for rank, multiplier in (("aaa", 8), ("aa", 7), ("a", 6)):
                threshold = (400 * multiplier + 8) // 9
                identity = f"standard.graph.fs20.{layout}.line.{rank}"
                before, = (t for t in value["tracks"] if t["target"] == identity and t["property"] == "opacity")
                reached, = (t for t in value["tracks"] if t["target"] == identity + ".reached" and t["property"] == "opacity")
                for score, expected in ((0, (1, 0)), (threshold - 1, (1, 0)),
                                        (threshold, (0, 1)), (threshold + 1, (0, 1)),
                                        (400, (0, 1)), (1000000, (0, 1))):
                    with self.subTest(layout=layout, rank=rank, score=score):
                        values = evaluate(value, state(maximum=400, score=score))
                        self.assertEqual(expected, (step(before, values[before["clock"]]),
                                                    step(reached, values[reached["clock"]])))

    def test_colour_does_not_replace_the_existing_host_visibility_or_rank_position(self):
        value = self.fixture.build("s20", "p1")
        nodes = source20_fixture.graph_nodes(value)
        gate = nodes["standard.graph.fs20.p1.rank.available"]
        self.assertEqual({"standard.graph.fs20.p1.line." + rank + suffix
                          for rank in ("aaa", "aa", "a") for suffix in ("", ".reached")},
                         {n["id"] for n in gate["children"]})
        self.assertTrue(any(b["target"] == gate["id"] and b["property"] == "visible"
                            and b["source"] == "score.rank-available" for b in value["bindings"]))
        for line in gate["children"]:
            self.assertFalse(any(t["target"] == line["id"] and t["property"] == "visible"
                                 for t in value["tracks"]))
            y, = (t for t in value["tracks"] if t["target"] == line["id"] and t["property"] == "y")
            self.assertEqual([529 / 720, 79 / 720], [p["value"] for p in y["keyframes"]])
        for track in value["tracks"]:
            if track.get("clock", "").startswith("value."):
                self.assertEqual("opacity", track["property"])
        receipt = value["receipt"]
        self.assertEqual([.53, .53, 1, 1], receipt["rankColour"]["sourceRGBA"]["before"])
        self.assertEqual([1, 1, 0, 1], receipt["rankColour"]["sourceRGBA"]["reached"])
        self.assertIn("host", receipt["rankColour"]["hostMapping"])
        self.assertIn("no numeric-clock colour", receipt["rankColour"]["hostDraws"])
        self.assertNotIn("alpha switch", receipt["adaptations"][2])

    def test_pacemaker_and_all_old_families_keep_their_own_existing_programs(self):
        for family in ("0", "1", "s23", "s24", "s22", "s21"):
            for layout in ("p1", "p2", "dp"):
                value = self.fixture.build(family, layout)
                self.assertEqual(self.fixture.before[family, layout, None], value)
                self.assertNotIn("rankColour", value["receipt"])
        for layout in ("p1", "p2", "dp"):
            for target in TARGETS:
                value = self.fixture.build("s20", layout, target)
                self.assertFalse(any(n["id"].endswith(("rank-colour-aaa", "rank-colour-aa", "rank-colour-a"))
                                     for n in value["numericSources"]))
                self.assertFalse(any(t["property"] == "colour" for t in value["tracks"]))


if __name__ == "__main__":
    unittest.main()
