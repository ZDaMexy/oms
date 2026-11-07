"""Original RGBA and TL--BR triangle UV checks for entrance-only rasters.

The independent oracle uses rational barycentric coordinates, not the helper's
NumPy edge equations. Its non-affine fixture has no pixel-centre outer-edge
ties. These CPU checks do not claim equivalence with GPU edge/filter coverage.
"""

from copy import deepcopy
from fractions import Fraction
import json
import math
import os
from pathlib import Path
import sys
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import AuthorPackage, walk_nodes
from beatoraja_entrance_art import entrance_quad_track, rasterize_canvas_quad


def pattern(width=7, height=5):
    image = Image.new("RGBA", (width, height))
    image.putdata([(17+x*23, 11+y*29, (x*47+y*31) % 256, (x*67+y*89) % 256)
                   for y in range(height) for x in range(width)])
    return image


def identity():
    return {"vertices": [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]], "opacity": 1}


def rational(value):
    return Fraction(str(value))


def triangle_oracle(source, quad, rect, diagonal="tl-br"):
    x, y, width, height = map(rational, rect)
    points = [(x+rational(u)*width, y+rational(v)*height) for u, v, _ in quad["vertices"]]
    uv = [(0, 0), (1, 0), (1, 1), (0, 1)]
    left, top = math.floor(min(p[0] for p in points)), math.floor(min(p[1] for p in points))
    right, bottom = math.ceil(max(p[0] for p in points)), math.ceil(max(p[1] for p in points))
    image = Image.new("RGBA", (right-left, bottom-top))
    triangles = [(0, 1, 2), (0, 2, 3)] if diagonal == "tl-br" else [(0, 1, 3), (1, 2, 3)]
    sampled = 0
    for iy in range(image.height):
        for ix in range(image.width):
            px, py = Fraction(2*(left+ix)+1, 2), Fraction(2*(top+iy)+1, 2)
            # This test fixture avoids outer-edge pixel-centre ties, so a
            # different GPU top-left coverage convention is not its oracle.
            for first, last in zip(points, [*points[1:], points[0]]):
                if ((px-first[0])*(last[1]-first[1]) == (py-first[1])*(last[0]-first[0])
                        and min(first[0], last[0]) <= px <= max(first[0], last[0])
                        and min(first[1], last[1]) <= py <= max(first[1], last[1])):
                    raise AssertionError("Choose a fixture without outer-edge pixel-centre ties")
            for indices in triangles:
                a, b, c = [points[index] for index in indices]
                determinant = (b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
                if determinant == 0:
                    continue
                wa = ((b[1]-c[1])*(px-c[0])+(c[0]-b[0])*(py-c[1]))/determinant
                wb = ((c[1]-a[1])*(px-c[0])+(a[0]-c[0])*(py-c[1]))/determinant
                wc = 1-wa-wb
                if min(wa, wb, wc) < 0:
                    continue
                u, v = [sum(weight*uv[index][axis] for weight, index in zip((wa, wb, wc), indices))
                        for axis in (0, 1)]
                sx = min(source.width-1, max(0, math.floor(u*source.width)))
                sy = min(source.height-1, max(0, math.floor(v*source.height)))
                image.putpixel((ix, iy), source.getpixel((sx, sy)))
                sampled += 1
                break
    return image, [left, top, right-left, bottom-top], sampled


def step_track(**changes):
    result = {"id": "entry", "target": "art", "type": "frame", "property": "projective-quad",
              "clock": "preparation.elapsed", "easing": "step", "loop": False,
              "keyframes": [{"time": 0, "value": identity()}, {"time": 1000/60, "value": identity()}]}
    result.update(changes)
    return result


class EntranceArtTests(unittest.TestCase):
    def test_identity_preserves_every_rgba_byte_and_opacity_is_not_premultiplied(self):
        source = pattern()
        original = source.tobytes()
        for opacity in (1, .375):
            quad = {**identity(), "opacity": opacity}
            raster, evidence = rasterize_canvas_quad(source, quad, (10, -20, 7, 5))
            self.assertEqual(original, raster.tobytes())
            self.assertEqual(original, source.tobytes())
            self.assertEqual([10, -20, 7, 5], evidence["rect"])
            self.assertEqual(35, evidence["sampledPixels"], "Transparent texels still retain their original RGB")
            self.assertEqual(opacity, evidence["opacity"])
            self.assertEqual((17, 11, 0, 0), raster.getpixel((0, 0)))

    def test_reflections_preserve_orientation_registration_and_alpha(self):
        source = pattern()
        for flip_x, flip_y in ((True, False), (False, True), (True, True)):
            with self.subTest(x=flip_x, y=flip_y):
                quad = identity()
                quad["vertices"] = [[1-x if flip_x else x, 1-y if flip_y else y, q]
                                    for x, y, q in quad["vertices"]]
                expected = source
                if flip_x:
                    expected = expected.transpose(Image.Transpose.FLIP_LEFT_RIGHT)
                if flip_y:
                    expected = expected.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
                raster, evidence = rasterize_canvas_quad(source, quad, (-30, 12, 7, 5))
                self.assertEqual(expected.tobytes(), raster.tobytes())
                self.assertEqual([-30, 12, 7, 5], evidence["rect"])
                self.assertEqual(35, evidence["sampledPixels"])

    def test_nonparallelogram_uses_tl_br_triangle_uv_not_affine_or_other_diagonal(self):
        source = pattern(8, 7)
        vertices = [[.07, .11, 1], [.93, .05, 1], [1.13, .84, 1], [-.16, 1.09, 1]]
        for reflected in (False, True):
            with self.subTest(reflected=reflected):
                quad = {"vertices": [[1-x if reflected else x, y, q] for x, y, q in vertices], "opacity": .6}
                unchanged = deepcopy(quad)
                rect = (2.2, -1.1, 9.3, 8.7)
                expected, bounds, count = triangle_oracle(source, quad, rect)
                wrong, _, _ = triangle_oracle(source, quad, rect, "tr-bl")
                self.assertGreater(sum(expected.getpixel((x, y)) != wrong.getpixel((x, y))
                                       for y in range(expected.height) for x in range(expected.width)), 5,
                                   "The fixture must distinguish the two production diagonals")
                raster, evidence = rasterize_canvas_quad(source, quad, rect)
                self.assertEqual(expected.tobytes(), raster.tobytes())
                self.assertEqual((bounds, count), (evidence["rect"], evidence["sampledPixels"]))
                self.assertEqual(unchanged, quad)

    def test_shared_diagonal_has_no_hole_and_one_collapsed_triangle_remains_valid(self):
        source = Image.new("RGBA", (4, 4), (20, 40, 60, 180))
        raster, evidence = rasterize_canvas_quad(source, identity(), (0, 0, 4, 4))
        self.assertEqual(16, evidence["sampledPixels"])
        self.assertTrue(all(raster.getpixel((i, i)) == (20, 40, 60, 180) for i in range(4)))
        quad = identity()
        quad["vertices"][3] = [0, 0, 1]
        raster, evidence = rasterize_canvas_quad(source, quad, (0, 0, 4, 4))
        self.assertEqual(10, evidence["sampledPixels"])
        self.assertEqual((0, 0, 0, 0), raster.getpixel((0, 3)))
        self.assertEqual((20, 40, 60, 180), raster.getpixel((3, 0)))

    def test_fully_degenerate_or_transparent_quad_produces_no_visible_carrier(self):
        source = pattern()
        for vertices in (
                [[0, 0, 1]]*4,
                [[0, 0, 1], [1, 0, 1], [1, 0, 1], [0, 0, 1]],
                [[0, 0, 1], [.25, .25, 1], [1, 1, 1], [.5, .5, 1]]):
            with self.subTest(vertices=vertices):
                raster, evidence = rasterize_canvas_quad(source, {"vertices": vertices}, (10, 20, 8, 8))
                self.assertIsNone(raster)
                self.assertEqual(0, evidence["sampledPixels"])
        raster, evidence = rasterize_canvas_quad(source, {**identity(), "opacity": 0}, (10, 20, 7, 5))
        self.assertIsNone(raster)
        self.assertEqual([10, 20, 7, 5], evidence["rect"])
        self.assertEqual(0, evidence["sampledPixels"])

    def test_nonunit_q_nonfinite_values_and_crossed_edges_are_rejected(self):
        source = pattern()
        invalid = []
        for coordinate, value in ((2, 0), (2, 2), (2, float("nan")), (0, float("inf")), (0, True)):
            quad = identity()
            quad["vertices"][1][coordinate] = value
            invalid.append(quad)
        invalid.extend((
            {"vertices": [[0, 0, 1], [1, 1, 1], [1, 0, 1], [0, 1, 1]]},
            {"vertices": [[1, 0, 1], [0, 1, 1], [0, 0, 1], [1, 1, 1]]},
        ))
        for quad in invalid:
            with self.subTest(quad=quad), self.assertRaises(ValueError):
                rasterize_canvas_quad(source, quad, (0, 0, 7, 5))
        for opacity in (-.01, 1.01, float("nan")):
            with self.subTest(opacity=opacity), self.assertRaises(ValueError):
                rasterize_canvas_quad(source, {**identity(), "opacity": opacity}, (0, 0, 7, 5))

    def test_track_selection_preserves_actual_frame_records_and_fractional_times(self):
        node = {"id": "art", "type": "projective-sprite"}
        original = step_track()
        before = deepcopy(original)
        self.assertIs(original, entrance_quad_track(node, {"tracks": [original]}))
        self.assertEqual(before, original)
        one = step_track(keyframes=[{"time": 0, "value": identity()}])
        self.assertIs(one, entrance_quad_track(node, {"tracks": [one]}))
        self.assertIsNone(entrance_quad_track(node, {}))
        self.assertIsNone(entrance_quad_track(node, {"tracks": [step_track(clock="timing.time")]}))
        self.assertIsNone(entrance_quad_track(node, {"tracks": [step_track(target="unrelated")]}))

    def test_track_qualification_rejects_wrong_type_clock_conflicts_and_bad_order(self):
        node = {"id": "art"}
        invalid = [step_track(**patch) for patch in (
            {"type": "tween"}, {"easing": "linear"}, {"loop": True}, {"keyframes": []},
            {"keyframes": [{"time": 1, "value": identity()}]},
            {"keyframes": [{"time": 0, "value": identity()}, {"time": 0, "value": identity()}]},
            {"keyframes": [{"time": 0, "value": identity()}, {"time": -1, "value": identity()}]},
            {"keyframes": [{"time": 0, "value": identity()}, {"time": float("nan"), "value": identity()}]},
        )]
        for candidate in invalid:
            with self.subTest(track=candidate), self.assertRaises(ValueError):
                entrance_quad_track(node, {"tracks": [candidate]})
        with self.assertRaisesRegex(ValueError, "conflicting"):
            entrance_quad_track(node, {"tracks": [step_track(), step_track(clock="timing.time")]})

    def test_actual_116_nonaffine_source_states_remain_qualified_without_rewriting(self):
        root = Path(os.environ.get("OMS_LIGHTNING_AUTHOR",
            "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1"))
        if not root.is_dir():
            self.skipTest("The frozen private Lightning author is required")
        author = AuthorPackage(root, "lightning")
        tracks, unique, nonaffine = set(), set(), {}
        for choice in author.modules["frame"]["choices"]:
            for keymode, mode in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                frame = author.contribution("frame", author.options(keymode, mode, {"frame": choice["value"]}))
                nodes = {node["id"]: node for tree in [frame.scene["root"], *frame.templates.values()] for node in walk_nodes(tree)}
                for definition in frame.scene.get("tracks", []):
                    if definition["property"] != "projective-quad" or definition.get("clock") != "preparation.elapsed":
                        continue
                    self.assertIs(definition, entrance_quad_track(nodes[definition["target"]], frame.scene))
                    tracks.add((frame.scene_path, definition["id"]))
                    for key in definition["keyframes"]:
                        quad = key["value"]
                        stamp = json.dumps(quad, sort_keys=True, separators=(",", ":"))
                        unique.add(stamp)
                        corners = quad["vertices"]
                        defects = [abs(corners[0][axis]+corners[2][axis]-corners[1][axis]-corners[3][axis]) for axis in (0, 1)]
                        if max(defects) > 1e-12:
                            nonaffine[stamp] = quad
        self.assertEqual((115, 2786, 116), (len(tracks), len(unique), len(nonaffine)))
        source = pattern()
        visible = 0
        for quad in nonaffine.values():
            unchanged = deepcopy(quad)
            raster, evidence = rasterize_canvas_quad(source, quad, (0, 0, 1920, 1080))
            self.assertEqual(unchanged, quad)
            if quad.get("opacity", 1) > 0:
                self.assertIsNotNone(raster)
                self.assertGreater(evidence["sampledPixels"], 0)
                self.assertEqual(tuple(evidence["rect"][2:]), raster.size)
                visible += 1
            else:
                self.assertIsNone(raster)
        self.assertEqual(114, visible)
        print("Original entrance quads: 115 frame tracks, 2786 states, 116 non-affine states (114 visible)")


if __name__ == "__main__":
    unittest.main()
