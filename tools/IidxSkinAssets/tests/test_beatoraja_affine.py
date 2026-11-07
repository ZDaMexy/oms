"""Exact pixel tests for the actual fixed, overscanning key-light conversion."""

import copy
from pathlib import Path
import sys
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_affine import fixed_affine_quad, rasterize_canvas_affine


class CanvasAffineTests(unittest.TestCase):
    def setUp(self):
        self.colours = [(255, 0, 0, 255), (0, 255, 0, 160), (0, 0, 255, 80), (255, 255, 0, 40)]
        self.source = Image.new("RGBA", (2, 2))
        self.source.putdata(self.colours)
        self.identity = {"vertices": [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]], "opacity": .5}

    def test_identity_preserves_every_original_pixel_and_separate_opacity(self):
        raster, evidence = rasterize_canvas_affine(self.source, self.identity, (10, 20, 2, 2))
        self.assertEqual(raster.tobytes(), self.source.tobytes())
        self.assertEqual(evidence["rect"], [10, 20, 2, 2])
        self.assertEqual(evidence["opacity"], .5)
        self.assertEqual(evidence["sampled_pixels"], 4)

    def test_sheared_overhang_retains_full_canvas_bounds_and_exact_nearest_pixels(self):
        quad = {"vertices": [[-.5, 0, 1], [1, 0, 1], [1.5, 1, 1], [0, 1, 1]]}
        raster, evidence = rasterize_canvas_affine(self.source, quad, (10, 20, 2, 2))
        a, b, c, d = self.colours
        clear = (0, 0, 0, 0)
        self.assertEqual(evidence["rect"], [9, 20, 4, 2])
        self.assertEqual([raster.getpixel((x, y)) for y in range(2) for x in range(4)],
                         [a, a, b, clear, clear, c, d, d])
        self.assertEqual(evidence["sampled_pixels"], 6)
        self.assertEqual(evidence["closure_residual"], [0, 0])

    def test_reflection_keeps_orientation_and_alpha_without_generated_colours(self):
        quad = {"vertices": [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]]}
        raster, _ = rasterize_canvas_affine(self.source, quad, (0, 0, 2, 2))
        a, b, c, d = self.colours
        self.assertEqual([raster.getpixel((x, y)) for y in range(2) for x in range(2)], [b, a, d, c])

    def test_perspective_non_affine_and_degenerate_inputs_are_rejected(self):
        invalid = []
        perspective = copy.deepcopy(self.identity)
        perspective["vertices"][1][2] = .9
        invalid.append(perspective)
        trapezoid = copy.deepcopy(self.identity)
        trapezoid["vertices"][2][0] = .9
        invalid.append(trapezoid)
        invalid.append({"vertices": [[0, 0, 1]] * 4})
        for quad in invalid:
            with self.subTest(quad=quad), self.assertRaises(ValueError):
                rasterize_canvas_affine(self.source, quad, (0, 0, 2, 2))

    def test_only_fixed_zero_time_track_is_accepted(self):
        node = {"id": "key", "properties": {"projective-quad": self.identity}}
        track = {"type": "frame", "target": "key", "property": "projective-quad",
                 "keyframes": [{"time": 0, "value": self.identity}]}
        self.assertEqual(fixed_affine_quad(node, {"tracks": [track]}), self.identity)
        for addition in ({"clock": "timing.time"}, {"trigger": "input.pressed"}, {"loop": True},
                         {"keyframes": [{"time": 10, "value": self.identity}]}):
            with self.subTest(addition=addition), self.assertRaises(ValueError):
                fixed_affine_quad(node, {"tracks": [{**track, **addition}]})
        with self.assertRaises(ValueError):
            fixed_affine_quad(node, {"variants": [{"target": "key", "property": "projective-quad"}]})


if __name__ == "__main__":
    unittest.main()
