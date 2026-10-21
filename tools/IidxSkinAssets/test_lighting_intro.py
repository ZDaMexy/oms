"""Assembly contract checks with synthetic geometry and no original game art."""

import copy
import hashlib
from pathlib import Path
import tempfile
import unittest

import lighting_intro as intro


def scene(layout="p1"):
    background = intro._node("frame." + layout + ".background", "sprite", resource="test.baked")
    background["slot"] = "stage.background"
    notes = intro._node("native.notes", "sprite", {"opacity": 1}, resource="test.note")
    notes["slot"] = "note.head"
    return {"root": intro._node("root"),
            "templates": [{"id": "frame.template." + layout + ".background", "root": background},
                          {"id": "frame.template.notes", "root": notes}],
            "instances": [], "bindings": [{"id": "native.bind", "target": notes["id"],
                                             "property": "opacity", "source": "note.opacity"}],
            "variants": [], "tracks": [], "stateMachines": []}


def reference():
    first = {"vertices": [[0, 0, 1], [0.1, 0, 1], [0.1, 0.1, 1], [0, 0.1, 1]], "opacity": 1}
    later = copy.deepcopy(first)
    later["vertices"] = [[point[0] + 0.2, *point[1:]] for point in first["vertices"]]
    second = {**copy.deepcopy(first), "opacity": 0}

    def layer(resource, channel):
        return {"resource": resource, "blend": "alpha", "channel": channel,
                "sourceTexture": resource + ".png", "sourceGeometry": "test_rect", "layerPath": []}

    return {"movies": {"center_frame_sp": [layer("original.a", [(0, first)])],
                       "1p_frame": [layer("original.b", [(0, second), (20, first), (40, later)])],
                       "2p_frame": [layer("original.b", [(0, later), (40, first)])],
                       "center_frame_dp_1p": [layer("original.b", [(0, second), (80, later)])]},
            "textures": [{"id": "original.a", "width": 16, "height": 16},
                         {"id": "original.b", "width": 24, "height": 32}]}


def assets():
    return {"resources": [{"id": "original." + suffix, "type": "texture", "path": "art/" + suffix + ".png",
                           "filter": "nearest"} for suffix in ("a", "b")]}


def manifest():
    return {"resources": [{"id": "test.baked", "type": "texture", "path": "art/baked.png", "filter": "nearest"},
                          {"id": "test.note", "type": "texture", "path": "art/note.png", "filter": "nearest"}]}


class LightingIntroTest(unittest.TestCase):
    def test_source_assets_are_copied_once_as_exact_bytes_and_never_reencoded(self):
        with tempfile.TemporaryDirectory() as temporary:
            library, output = Path(temporary) / "source", Path(temporary) / "author"
            library.mkdir()
            output.mkdir()
            # The reference is already validated by load_reference.  A binary
            # payload here tests the copier's byte contract without game art.
            data = b"\x89PNG\r\n\x1a\noriginal-source-payload\x00\xff"
            source = library / "source.png"
            source.write_bytes(data)
            row = {"id": "original", "type": "texture", "path": "art/source.png", "filter": "nearest",
                   "source": source, "sha256": hashlib.sha256(data).hexdigest(), "width": 10, "height": 20}
            prepared = intro.prepare_assets({"library": library, "textures": [row, copy.deepcopy(row)]}, output)
            self.assertEqual(data, (output / row["path"]).read_bytes())
            self.assertEqual(1, len(prepared["resources"]))
            self.assertEqual(10 * 20 * 4, prepared["decodedTextureBytes"])
            self.assertEqual(0, prepared["frameRasters"])
            source.write_bytes(data + b"changed")
            with self.assertRaisesRegex(ValueError, "changed after reference"):
                intro.prepare_assets({"library": library, "textures": [row]}, output)

    def test_uv_identity_preserves_corner_order_under_source_reflection(self):
        geometry = {"vertex_points": [{"x": x, "y": y, "z": 0} for x, y in ((10, 20), (50, 20), (10, 80), (50, 80))],
                    "tex_points": [{"x": x, "y": y} for x, y in ((0, 0), (1, 0), (0, 1), (1, 1))],
                    "tex_colors": [], "draw_params": [{"region": "rect", "flags": 0x40, "vertexes": [0, 1, 2, 2, 1, 3]}]}
        corners = intro._source_corners(geometry, {"name": "rect.png", "uvRect": [0, 40, 0, 60],
                                                   "width": 40, "height": 60}, (40, 60))
        draw = {"matrix": {"a": 0, "b": -1, "c": -1, "d": 0, "tx": 200, "ty": 100},
                "mult_color": {"r": 1, "g": 1, "b": 1, "a": 0.5},
                "add_color": {"r": 0, "g": 0, "b": 0, "a": 0}, "mask_present": False}
        quad = intro._quad(corners, draw)
        self.assertEqual(0.5, quad["opacity"])
        for actual, expected in zip(quad["vertices"], ((180, 90), (180, 50), (120, 50), (120, 90))):
            self.assertAlmostEqual(expected[0], actual[0] * 1920, places=5)
            self.assertAlmostEqual(expected[1], actual[1] * 1080, places=5)
            self.assertEqual(1, actual[2])
        geometry["tex_points"][3]["x"] = 0.75
        with self.assertRaisesRegex(ValueError, "complete unmodified UV"):
            intro._source_corners(geometry, {"name": "rect.png", "uvRect": [0, 40, 0, 60],
                                           "width": 40, "height": 60}, (40, 60))

    def test_independent_source_channels_leave_gameplay_and_static_defaults_intact(self):
        document, declaration = scene(), manifest()
        original_notes = copy.deepcopy(document["templates"][1])
        original_binding = copy.deepcopy(document["bindings"][0])
        receipt = intro.inject(document, declaration, frame="iidx33", layout="p1", reference=reference(), assets=assets())
        self.assertEqual(original_notes, document["templates"][1])
        self.assertEqual(original_binding, document["bindings"][0])
        tracks = [track for track in document["tracks"] if track["property"] == "projective-quad"]
        self.assertEqual([1, 3], [len(track["keyframes"]) for track in tracks])
        self.assertEqual({intro.CLOCK}, {track["clock"] for track in document["tracks"]})
        self.assertTrue(all(not track["loop"] and "trigger" not in track for track in document["tracks"]))
        all_nodes = list(intro._walk(document["templates"][0]["root"]))
        self.assertEqual(1, next(node for node in all_nodes if node["id"].endswith(".baked"))["properties"]["opacity"])
        self.assertEqual(0, next(node for node in all_nodes if node["id"].endswith(".overlay"))["properties"]["opacity"])
        self.assertEqual(0, receipt["frameRasters"])
        self.assertEqual((16 * 16 + 24 * 32) * 4, receipt["addedDecodedTextureBytes"])
        self.assertEqual({"original.a", "original.b", "test.baked", "test.note"}, {item["id"] for item in declaration["resources"]})
        self.assertTrue(all(node.get("slot") in (None, "stage.background") for node in all_nodes))

    def test_layout_sources_are_selected_independently_and_do_not_keep_unused_art(self):
        for layout, expected in (("p2", {"original.a", "original.b"}), ("dp", {"original.b"})):
            with self.subTest(layout=layout):
                document, declaration = scene(layout), manifest()
                receipt = intro.inject(document, declaration, frame="iidx33", layout=layout,
                                       reference=reference(), assets=assets(), duration_ms=800)
                self.assertEqual(expected, set(receipt["sourceTextureIds"]))
                self.assertEqual(expected, {item["id"] for item in declaration["resources"] if item["id"].startswith("original.")})
                self.assertEqual(800, max(track["keyframes"][-1]["time"] for track in document["tracks"]))
        p1, p2 = scene(), scene("p2")
        intro.inject(p1, manifest(), frame="iidx33", layout="p1", reference=reference(), assets=assets())
        intro.inject(p2, manifest(), frame="iidx33", layout="p2", reference=reference(), assets=assets())
        first = lambda doc: next(track for track in doc["tracks"] if track["property"] == "projective-quad")["keyframes"][0]["value"]["vertices"][0][0]
        self.assertAlmostEqual(540 / 1920, first(p1) - first(p2))

    def test_option_off_is_exact_baked_art_and_other_frames_are_labelled_adaptations(self):
        for layout in intro.LAYOUTS:
            with self.subTest(layout=layout):
                document, declaration = scene(layout), manifest()
                before_resources = copy.deepcopy(declaration["resources"])
                receipt = intro.inject(document, declaration, frame="iidx32", layout=layout)
                root = document["templates"][0]["root"]
                disabled = next(node for node in root["children"] if node["id"].endswith(".disabled"))
                self.assertEqual("test.baked", disabled["children"][0]["resource"])
                case = next(item for item in document["variants"] if item["target"] == disabled["id"])
                self.assertEqual("false", case["cases"][0]["key"])
                self.assertTrue(case["cases"][0]["value"])
                self.assertEqual(before_resources, declaration["resources"])
                self.assertIn("Author adaptation", receipt["source"])
                self.assertGreater(receipt["regions"], 1)
                self.assertTrue(all(track["property"] in ("x", "y", "opacity") for track in document["tracks"]))

    def test_intro_cannot_extend_timing_or_overwrite_an_existing_owner(self):
        document, declaration = scene(), manifest()
        with self.assertRaisesRegex(ValueError, "one-second"):
            intro.inject(document, declaration, frame="iidx32", layout="p1", duration_ms=3000)
        intro.inject(document, declaration, frame="iidx32", layout="p1")
        with self.assertRaisesRegex(ValueError, "already has an intro"):
            intro.inject(document, declaration, frame="iidx32", layout="p1")


if __name__ == "__main__":
    unittest.main()
