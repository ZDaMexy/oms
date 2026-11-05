"""Gauge program boundaries and source hierarchy, without private artwork."""
from copy import deepcopy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_gauge_effect import geometric_bounds, repeating_program


class StandardGaugeEffectTests(unittest.TestCase):
    @staticmethod
    def source():
        identity = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]
        return {"loop": False, "trigger": "gauge.increase", "fps": 60,
            "nodes": [{"parent": -1, "geometry": -1}, {"parent": 0, "geometry": 0}],
            "states": [
                {"matrix": [*identity[:9], 10, 20, 0], "origin": [0, 0, 0], "projection": "affine"},
                {"matrix": [-1, 0, 0, 0, 1, 0, 0, 0, 1, 5, 0, 0], "origin": [2, 0, 0], "projection": "affine"}],
            "geometries": [{"resource": "synthetic.texture", "vertices": [[0, 0, 0, 0, 0], [4, 0, 0, 1, 0], [0, 2, 0, 0, 1]],
                            "triangles": [0, 1, 2]}],
            "frames": [{"nodes": [[0, 0], [1, 1]], "camera": None}, {"nodes": [], "camera": None}]}

    def test_loop_changes_only_playback_and_keeps_terminal_boundary(self):
        source = self.source()
        original = deepcopy(source)
        loop = repeating_program(source)
        self.assertEqual(original, source)
        self.assertEqual({**original, "loop": True}, loop)
        self.assertEqual([], loop["frames"][-1]["nodes"])
        loop["states"][0]["matrix"][9] = 99
        self.assertEqual(10, source["states"][0]["matrix"][9])

    def test_unqualified_trigger_held_loop_and_missing_end_are_rejected(self):
        for changed in ({"trigger": "input.key.down"}, {"loop": True}, {"heldLoop": [0, 1]},
                        {"releaseFrame": 1}, {"frames": [{"nodes": [[0, 0]], "camera": None}]}):
            with self.subTest(change=changed), self.assertRaises(ValueError):
                repeating_program({**self.source(), **changed})

    def test_bounds_preserve_parent_transform_source_origin_and_mirror(self):
        self.assertEqual([13, 20, 17, 22], geometric_bounds(self.source()))
        perspective = self.source()
        perspective["states"][1]["projection"] = "perspective"
        with self.assertRaises(ValueError):
            geometric_bounds(perspective)


if __name__ == "__main__":
    unittest.main()
