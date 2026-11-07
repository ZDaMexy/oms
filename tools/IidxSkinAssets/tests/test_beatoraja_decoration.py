"""Decoration geometry and native scratch ownership, using synthetic pixels."""

import copy
import math
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution
from beatoraja_decoration import (add_oriented_source, export_bga_frame,
                                  export_turntable, fixed_projective_quad,
                                  rasterize_fixed_affine_quad)
from beatoraja_native import AssetSink


def node(identifier, kind="container", properties=None, children=(), **extra):
    return {"id": identifier, "type": kind, "properties": properties or {}, "children": list(children), **extra}


def motion(identifier, clock):
    end = 1000 if clock == "timing.time" else 360 if "counts" in clock else 1
    return {"id": identifier + ".motion", "type": "tween", "target": identifier, "property": "rotation",
            "clock": clock, "easing": "linear", "loop": True,
            "keyframes": [{"time": 0, "value": 0}, {"time": end, "value": 360}]}


def quad(identifier, vertices):
    return {"id": identifier + ".quad", "type": "frame", "target": identifier, "property": "projective-quad",
            "easing": "step", "loop": False, "keyframes": [{"time": 0, "value": {"vertices": vertices, "opacity": .75}}]}


def contribution(module, templates=(), tracks=(), ini="", variants=(), bindings=()):
    return Contribution(module, "test", None, None, None,
                        {"resources": [{"id": "art", "type": "texture", "path": "art.png"}]},
                        {"templates": [{"id": root["id"] + ".template", "root": root} for _, root in templates],
                         "instances": [{"template": root["id"] + ".template", "material": {"slot": "decoration", "resource": marker}}
                                       for marker, root in templates],
                         "tracks": list(tracks), "variants": list(variants), "bindings": list(bindings)}, ini)


class DecorationTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-decoration"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.author_root, self.output = self.root / "author", self.root / "output"
        self.author_root.mkdir()
        self.output.mkdir()
        self.pattern = [(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 255, 255)]
        art = Image.new("RGBA", (2, 2)); art.putdata(self.pattern); art.save(self.author_root / "art.png")
        self.sink = AssetSink(self.author_root, self.output)
        ini = "\n".join('[BmsMaterial]\nTarget: global ruleset=bms keymode=' + mode + ' presentation=' + side
                        + '\ndecoration: resource Provide "marker-' + side + '"'
                        for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")))
        self.parts = {"frame": contribution("frame", ini=ini)}
        self.author = SimpleNamespace(root=self.author_root, family="standard", modules=self.parts,
                                      contribution=lambda module, options: self.parts[module])

    def add_turntables(self):
        templates, tracks = [], []
        for mode in ("p1", "p2", "dp"):
            children = []
            for group in range(1, 3 if mode == "dp" else 2):
                prefix = mode + str(group)
                sprite = node(prefix + ".sprite", "projective-sprite" if mode == "p2" else "sprite",
                              {"x": .5, "y": .5, "origin": "centre"}, resource="art")
                counts = node(prefix + ".counts", properties={"x": .5, "y": .5, "origin": "centre"}, children=[sprite])
                spin = node(prefix + ".spin", properties={"x": .25 if group == 1 else .75, "y": .5,
                            "width": .1, "height": .2, "origin": "centre"}, children=[counts])
                children.append(spin)
                tracks += [motion(spin["id"], "timing.time"), motion(counts["id"], "input.motion-counts-" + str(group)),
                           motion(sprite["id"], "input.motion-revolutions-" + str(group))]
                if mode == "p2":
                    tracks[-1]["keyframes"][-1]["value"] = -360
                    tracks.append(quad(sprite["id"], [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]]))
            templates.append(("marker-" + mode, node(mode + ".root", children=children)))
        self.parts["turntable"] = contribution("turntable", templates, tracks)

    def test_sp2_uses_player_one_offset_and_dp_uses_each_source_group_once(self):
        self.add_turntables()
        for keymode, mode, expected in (("7k", "p1", [[1]]), ("7k", "p2", [[1]]), ("14k", "dp", [[1], [2]])):
            result = export_turntable(self.author, {}, keymode, mode, self.sink, (0, 0, 200, 100))
            self.assertEqual([d["offsets"] for d in result["destination"]], expected)
            self.assertEqual(result["handled_root_ids"], [mode + ".root"])
            self.assertEqual(len(result["image"]), len(expected))
            differences = [g for g in result["gaps"] if g["code"] == "native-scratch-motion"]
            self.assertEqual(len(differences), len(expected))
            self.assertTrue(all(len(g["source_clocks"]) == 3 for g in differences))
            if mode == "p2":
                with Image.open(self.output / self.sink.source[result["image"][0]["src"]]["path"]) as flipped:
                    self.assertEqual([flipped.getpixel((x, y)) for y in range(2) for x in range(2)], [self.pattern[i] for i in (1, 0, 3, 2)])
                self.assertEqual(result["destination"][0]["dst"][0]["a"], 191)

    def test_fixed_quad_keeps_real_keycap_overhang_and_rejects_shear(self):
        sprite = node("key", "projective-sprite")
        vertices = [[-8 / 34, -1 / 80, 1], [42 / 34, -1 / 80, 1], [42 / 34, 68 / 80, 1], [-8 / 34, 68 / 80, 1]]
        scene = {"tracks": [quad("key", vertices)]}
        mapping = fixed_projective_quad(sprite, scene, (102, 484, 34, 80))
        for actual, expected in zip(mapping["rect"], (94, 483, 50, 69)):
            self.assertAlmostEqual(actual, expected)
        self.assertFalse(mapping["flip_x"] or mapping["flip_y"])
        scene["tracks"][0]["keyframes"][0]["value"]["vertices"][1][1] += 1 / 8192
        with self.assertRaisesRegex(ValueError, "Non-axis-aligned"):
            fixed_projective_quad(sprite, scene, (102, 484, 34, 80))

    def test_source_flips_do_not_resize_or_change_original_file(self):
        before = (self.author_root / "art.png").read_bytes()
        src = add_oriented_source(self.sink, "art.png", flip_x=True, flip_y=True)
        self.assertEqual((self.author_root / "art.png").read_bytes(), before)
        with Image.open(self.output / self.sink.source[src]["path"]) as flipped:
            self.assertEqual(flipped.size, (2, 2))
            self.assertEqual([flipped.getpixel((x, y)) for y in range(2) for x in range(2)], list(reversed(self.pattern)))

    def test_fixed_affine_uses_all_four_vertices_and_matches_triangle_uv_sampling(self):
        art = Image.new("RGBA", (25, 25))
        for y in range(25):
            for x in range(25):
                art.putpixel((x, y), (x * 10, y * 10, 0, 255))
        for delta in (1 / 8192, .17):
            for mirrored in (False, True):
                vertices = [[0, delta, 1], [1 - delta, 0, 1], [1, 1 - delta, 1], [delta, 1, 1]]
                if mirrored:
                    vertices = [[1 - x, y, q] for x, y, q in vertices]
                raster, evidence = rasterize_fixed_affine_quad(art, {"vertices": vertices})
                self.assertEqual(raster.size, art.size)
                self.assertEqual(evidence["vertices"], vertices)
                self.assertNotEqual(evidence["inverse_affine"][1], 0)
                # Independent two-triangle barycentric interpolation, as used by
                # GameplaySkinProjectiveSprite for four unit reciprocal-Ws.
                source_uvs = [(0, 0), (1, 0), (1, 1), (0, 1)]
                sampled = 0
                for y in range(25):
                    for x in range(25):
                        px, py = (x + .5) / 25, (y + .5) / 25
                        expected = (0, 0, 0, 0)
                        for indices in ((0, 1, 2), (0, 2, 3)):
                            a, b, c = [vertices[i] for i in indices]
                            denominator = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
                            la = ((b[1] - c[1]) * (px - c[0]) + (c[0] - b[0]) * (py - c[1])) / denominator
                            lb = ((c[1] - a[1]) * (px - c[0]) + (a[0] - c[0]) * (py - c[1])) / denominator
                            lc = 1 - la - lb
                            if min(la, lb, lc) < -1e-12:
                                continue
                            u, v = [sum(weight * source_uvs[i][axis] for weight, i in zip((la, lb, lc), indices)) for axis in (0, 1)]
                            if 0 <= u < 1 and 0 <= v < 1:
                                expected = art.getpixel((math.floor(u * 25), math.floor(v * 25)))
                                sampled += 1
                            break
                        self.assertEqual(raster.getpixel((x, y)), expected)
                self.assertEqual(evidence["sampled_pixels"], sampled)
                self.assertEqual(evidence["nontransparent_pixels"], sampled)
                if delta == 1 / 8192:
                    expected = art.transpose(Image.Transpose.FLIP_LEFT_RIGHT) if mirrored else art
                    self.assertEqual(raster.tobytes(), expected.tobytes())
                else:
                    self.assertLess(sampled, 25 * 25)

    def test_affine_turntable_keeps_registration_opacity_and_native_owner(self):
        self.add_turntables()
        delta = 1 / 8192
        vertices = [[1, delta, 1], [delta, 0, 1], [0, 1 - delta, 1], [1 - delta, 1, 1]]
        track = next(t for t in self.parts["turntable"].scene["tracks"] if t["property"] == "projective-quad")
        track["keyframes"][0]["value"]["vertices"] = vertices
        result = export_turntable(self.author, {}, "7k", "p2", self.sink, (0, 0, 200, 100))
        self.assertEqual(len(result["image"]), 1)
        destination = result["destination"][0]
        self.assertEqual(destination["offsets"], [1])
        self.assertEqual({k: destination["dst"][0][k] for k in ("x", "y", "w", "h", "a")},
                         {"x": 40, "y": 40, "w": 20, "h": 20, "a": 191})
        evidence = next(g for g in result["gaps"] if g["code"] == "fixed-decoration-quad-rasterized")
        self.assertEqual(evidence["vertices"], vertices)
        self.assertEqual(evidence["nontransparent_pixels"], 4)
        self.assertNotIn("non-axis-aligned-decoration-quad-needs-bake", [g["code"] for g in result["gaps"]])

    def test_perspective_quad_is_not_misrepresented_as_affine_raster(self):
        with self.assertRaisesRegex(ValueError, "unit reciprocal-W"):
            rasterize_fixed_affine_quad(Image.new("RGBA", (25, 25)),
                                       {"vertices": [[0, 0, 1], [1, 0, 2], [1, 1, 2], [0, 1, 1]]})

    def test_bga_border_keeps_full_source_with_native_clip_and_no_movie(self):
        art = node("border.art", "projective-sprite", {"x": -.25, "y": -1 / 3, "width": 1.5, "height": 5 / 3}, resource="art")
        clip = node("aperture", "clip", {"x": .25, "y": .2, "width": .5, "height": .6, "clip-mode": "bounds"}, [art])
        root = node("border", children=[clip])
        self.parts["bga-frame"] = contribution("bga-frame", [("marker-p1", root)], [quad(art["id"], [[0, 0, 1], [1, 0, 1], [1, 1, 1], [0, 1, 1]])])
        original = copy.deepcopy(self.parts["bga-frame"].scene)
        result = export_bga_frame(self.author, {"bga-frame": "on"}, "7k", "p1", self.sink, (0, 0, 200, 100))
        self.assertEqual(self.parts["bga-frame"].scene, original)
        self.assertEqual((result["image"][0]["w"], result["image"][0]["h"]), (2, 2))
        dst = result["destination"][0]["dst"][0]
        self.assertEqual({k: dst[k] for k in ("x", "y", "w", "h", "clip_x", "clip_y", "clip_w", "clip_h")},
                         {"x": 25, "y": 0, "w": 150, "h": 100, "clip_x": 50, "clip_y": 20, "clip_w": 100, "clip_h": 60})
        self.assertNotIn("bga", result)
        self.assertNotIn("offsets", result["destination"][0])
        dp = export_bga_frame(self.author, {"bga-frame": "on"}, "14k", "dp", self.sink, (0, 0, 200, 100))
        self.assertEqual(dp["image"], [])
        self.assertEqual(dp["gaps"], [])

    def test_absent_or_source_off_module_adds_no_replacement(self):
        for present in (False, True):
            if present:
                self.parts["bga-frame"] = contribution("bga-frame")
            result = export_bga_frame(self.author, {"bga-frame": "off"}, "7k", "p1", self.sink, (0, 0, 200, 100))
            self.assertEqual(result, {"image": [], "destination": [], "gaps": [], "handled_root_ids": []})
        self.assertEqual(self.sink.source, {})

    def test_hidden_source_theme_and_unknown_runtime_visibility_do_not_leak_art(self):
        sprite = node("choice.art", "sprite", {"width": .1, "height": .2}, resource="art")
        root = node("choice", children=[sprite])
        self.parts["turntable"] = contribution("turntable", [("marker-p1", root)], variants=[
            {"target": "choice", "property": "visible", "source": "option.frame", "default": False, "cases": [{"key": "selected", "value": True}]}])
        result = export_turntable(self.author, {"frame": "other"}, "7k", "p1", self.sink, (0, 0, 200, 100))
        self.assertEqual(result["image"], [])
        self.parts["turntable"].scene["bindings"] = [{"target": "choice.art", "property": "visible", "source": "camera.active"}]
        result = export_turntable(self.author, {"frame": "selected"}, "7k", "p1", self.sink, (0, 0, 200, 100))
        self.assertEqual(result["image"], [])
        self.assertEqual(result["gaps"][0]["code"], "unmapped-decoration-state")


if __name__ == "__main__":
    unittest.main()
