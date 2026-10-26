"""Dynamic lane345 author tests; native original-game parity is not a fixture."""
from __future__ import annotations

import copy
from pathlib import Path
import re
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

import lighting_dynamic_cover as cover


def reference():
    return {"contract": cover.CONTRACT,
        "resources": [{"id": "dc.r" + str(i), "type": "texture", "path": f"dc/r{i}.png", "filter": "nearest"} for i in range(3)],
        "animations": [{"id": "dc.a", "path": "dc/a.json.br", "resources": ["dc.r0", "dc.r1"]},
                       {"id": "dc.b", "path": "dc/b.json.br", "resources": ["dc.r2"]}],
        "branches": {"cat": {"animation": "dc.a", "periodFrames": 256, "fps": 60},
                     "border": {"animation": "dc.b", "periodFrames": 128, "fps": 60}}}


def original():
    manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
        "resources": [{"id": "old.atlas", "type": "texture", "path": "old.png", "filter": "nearest"},
                      {"id": "lift.black", "type": "texture", "path": "black.png", "filter": "nearest"}],
        "animations": [{"id": cover.OLD_ANIMATION, "path": "old.json.br", "resources": ["old.atlas"]}], "bitmapFonts": []}
    animation = {"id": "cover.animation", "type": "recorded-animation", "target": {"kind": "global"},
        "slot": "playfield.lift.fill", "animation": cover.OLD_ANIMATION, "blend": "alpha",
        "properties": {"x": -.02, "y": -.024, "width": 1.04, "height": 1.05}, "effects": [], "children": []}
    scene = {"contract": "oms-gameplay-skin-scene.v1",
        "root": {"id": "cover.root", "type": "container", "target": {"kind": "global"}, "blend": "inherit",
                 "properties": {}, "effects": [], "children": []},
        "templates": [{"id": "cover.template", "root": {"id": "cover.clip", "type": "clip", "target": {"kind": "global"},
            "blend": "alpha", "properties": {"clip-mode": "bounds", "x": .1, "width": .5}, "effects": [], "children": [animation]}}],
        "instances": [{"id": "cover.instance", "template": "cover.template", "material": {"slot": "playfield.lift.fill", "resource": "native.lift"}}],
        "tracks": [], "stateMachines": [], "bindings": [{"id": "cover.brightness", "target": animation["id"],
            "property": "brightness", "source": "option.lane-objects-brightness"}],
        "variants": [{"id": "cover.frame", "target": "cover.clip", "property": "visible", "source": "option.frame",
            "default": True, "cases": [{"id": "cover.frame.c0", "key": "iidx27", "value": False}]}]}
    return manifest, scene


class ClockSourceTest(unittest.TestCase):
    def movie(self):
        frames = [SimpleNamespace(num_tags=10 if index == 0 else 0, start_tag_offset=0) for index in range(128)]
        return SimpleNamespace(fps=60, frames=frames, child=SimpleNamespace(frames=list(range(256))))

    def test_only_copied_transparent_root_gets_empty_scheduler_ticks(self):
        movie = self.movie()
        selected = cover.clock_movie(movie, "cat")
        self.assertIsNot(movie, selected)
        self.assertEqual(128, len(movie.frames))
        self.assertEqual(512, len(selected.frames))
        self.assertEqual(60, selected.fps)
        self.assertIs(movie.child, selected.child)
        self.assertEqual(256, len(selected.child.frames))
        self.assertTrue(all(frame.num_tags == 0 for frame in selected.frames[128:]))
        self.assertIsNot(selected.frames[128], selected.frames[129])

    def test_border_retains_its_128_source_ticks_and_root_fps(self):
        movie = self.movie()
        selected = cover.clock_movie(movie, "border")
        self.assertEqual(movie.frames, selected.frames)
        self.assertEqual(128, len(selected.frames))
        self.assertEqual(60, selected.fps)

    def test_scheduler_rejects_changed_fps_root_or_unbounded_duration(self):
        movie = self.movie()
        for count in (127, 513, 1024, 256.0):
            with self.assertRaises(ValueError):
                cover.clock_movie(movie, "cat", ticks=count)
        movie.fps = 30
        with self.assertRaisesRegex(ValueError, "128@60"):
            cover.clock_movie(movie, "cat")

    def test_branch_filter_does_not_filter_the_real_childs_own_placements(self):
        class Place:
            def __init__(self, source):
                self.source_tag_id = source
        class Base:
            def __init__(self):
                self._AFPRenderer__root = object()
            def _AFPRenderer__place(self, tag, clip, prefix=""):
                return "evaluated", True
        for branch in ("cat", "border"):
            renderer = cover.branch_renderer(SimpleNamespace(AP2PlaceObjectTag=Place), Base, branch)()
            root = renderer._AFPRenderer__root
            self.assertEqual(("evaluated", True), renderer._AFPRenderer__place(Place(121), object()))
            self.assertEqual((None, False) if branch == "cat" else ("evaluated", True),
                             renderer._AFPRenderer__place(Place(16), root))
            self.assertEqual(("evaluated", True) if branch == "cat" else (None, False),
                             renderer._AFPRenderer__place(Place(279), root))

    def test_cat_branch_ignores_only_root_border_removes(self):
        class AP2RemoveObjectTag:
            pass
        class Place:
            pass
        class Base:
            def __init__(self):
                self._AFPRenderer__root = object()
            def _AFPRenderer__place(self, tag, clip, prefix=""):
                return "removed", True
        renderer = cover.branch_renderer(SimpleNamespace(AP2PlaceObjectTag=Place), Base, "cat")()
        self.assertEqual((None, False), renderer._AFPRenderer__place(AP2RemoveObjectTag(), renderer._AFPRenderer__root))
        self.assertEqual(("removed", True), renderer._AFPRenderer__place(AP2RemoveObjectTag(), object()))


class ContributionTest(unittest.TestCase):
    def test_normal_layout_clip_material_gate_and_bindings_stay_exact(self):
        manifest, scene = original()
        before = copy.deepcopy((manifest, scene))
        new_manifest, changed, receipt = cover.transform_contribution(manifest, scene, reference())
        self.assertEqual(before, (manifest, scene))
        for key in ("instances", "bindings", "variants", "tracks", "stateMachines"):
            self.assertEqual(scene[key], changed[key])
        old_host = scene["templates"][0]["root"]
        new_host = changed["templates"][0]["root"]
        self.assertEqual({key: value for key, value in old_host.items() if key != "children"},
                         {key: value for key, value in new_host.items() if key != "children"})
        wrapped = new_host["children"][0]
        self.assertEqual(old_host["children"][0]["properties"], wrapped["properties"])
        self.assertEqual(old_host["children"][0]["id"], wrapped["id"])
        self.assertEqual(old_host["children"][0]["slot"], wrapped["slot"])
        self.assertEqual("container", wrapped["type"])
        self.assertEqual(["dc.a", "dc.b"], [node["animation"] for node in wrapped["children"]])
        self.assertTrue(receipt["ordinaryMaterialAndLayoutHostsPreserved"])
        self.assertEqual(manifest["bitmapFonts"], new_manifest["bitmapFonts"])

    def test_incomplete_old_animation_and_atlas_are_removed_but_lift_black_remains(self):
        manifest, scene = original()
        updated, _, _ = cover.transform_contribution(manifest, scene, reference())
        self.assertEqual({"dc.a", "dc.b"}, {row["id"] for row in updated["animations"]})
        self.assertEqual({"dc.r0", "dc.r1", "dc.r2", "lift.black"}, {row["id"] for row in updated["resources"]})

    def test_new_consumer_ids_are_unique_across_lane_lift_and_graph_contributions(self):
        ids = []
        for prefix in ("lane", "lift-hd", "lift-sd", "graph-p1", "graph-p2", "graph-dp"):
            manifest, scene = original()
            scene["templates"][0]["root"]["children"][0]["id"] = prefix + ".art"
            _, changed, _ = cover.transform_contribution(manifest, scene, reference())
            ids.extend(node["id"] for node in changed["templates"][0]["root"]["children"][0]["children"])
        self.assertEqual(12, len(set(ids)))

    def test_every_id_obeys_public_stable_id_rule_without_numeric_dot_segments(self):
        manifest, scene = original()
        updated, changed, _ = cover.transform_contribution(manifest, scene, reference())
        segment = r"[a-z](?:[a-z0-9-]*[a-z0-9])?"
        valid = re.compile(segment + r"(?:\." + segment + r")*")
        for item in cover.walk([updated, changed]):
            if "id" in item:
                self.assertLessEqual(len(item["id"]), 128)
                self.assertIsNotNone(valid.fullmatch(item["id"]), item["id"])

    def test_shared_old_atlas_with_another_animation_fails_instead_of_deleting_it(self):
        manifest, scene = original()
        manifest["animations"].append({"id": "unrelated.clip", "path": "unrelated.json.br", "resources": ["old.atlas"]})
        with self.assertRaisesRegex(ValueError, "another consumer"):
            cover.transform_contribution(manifest, scene, reference())

    def test_missing_real_source_consumer_and_double_application_fail(self):
        manifest, scene = original()
        updated, changed, _ = cover.transform_contribution(manifest, scene, reference())
        with self.assertRaisesRegex(ValueError, "existing lane345"):
            cover.transform_contribution(updated, changed, reference())
        scene["templates"][0]["root"]["children"][0]["animation"] = "unrelated.clip"
        with self.assertRaisesRegex(ValueError, "no actual lane345"):
            cover.transform_contribution(manifest, scene, reference())

    def test_no_options_fonts_or_original_game_verification_are_invented(self):
        manifest, scene = original()
        _, _, receipt = cover.transform_contribution(manifest, scene, reference())
        self.assertEqual(0, receipt["optionsAdded"])
        self.assertEqual(0, receipt["fontsAdded"])
        self.assertFalse(receipt["nativeOriginalCustomVerified"])


class ProofRasterTest(unittest.TestCase):
    def test_lossless_derived_atlas_encoding_preserves_all_rgba_including_transparent_rgb(self):
        with tempfile.TemporaryDirectory(prefix="lane345-lossless-") as folder:
            root = Path(folder)
            source, target = root / "source.png", root / "derived.webp"
            image = Image.new("RGBA", (3, 2))
            image.putdata([(10, 20, 30, 0), (40, 50, 60, 255), (90, 80, 70, 33),
                           (22, 33, 44, 128), (55, 66, 77, 1), (255, 254, 253, 255)])
            image.save(source)
            before = source.read_bytes()
            receipt = cover.encode_lossless_atlas(source, target)
            self.assertEqual(before, source.read_bytes())
            self.assertTrue(receipt["wholeCanvasPixelsUnchanged"])
            self.assertFalse(receipt["sourceOriginalPngsChanged"])
            self.assertFalse(receipt["uvOrSamplerChanged"])
            with Image.open(target) as decoded:
                self.assertEqual(image.tobytes(), decoded.convert("RGBA").tobytes())

    def test_proof_raster_preserves_nearest_source_pixels_and_vertical_flip(self):
        with tempfile.TemporaryDirectory(prefix="lane345-proof-") as folder:
            root = Path(folder)
            image = Image.new("RGBA", (2, 2))
            image.putdata([(255, 0, 0, 255), (0, 255, 0, 255), (0, 0, 255, 255), (255, 255, 0, 255)])
            image.save(root / "source.png")
            draw = {"sprite": "source.png", "size": [2, 2], "vertices": [[1, 4, 0], [3, 4, 0], [1, 2, 0], [3, 2, 0]],
                "uv": [[0, 0], [1, 0], [0, 1], [1, 1]], "indices": [0, 1, 2, 2, 1, 3]}
            result = cover.draw_frame([draw], root)
            self.assertEqual((0, 0, 255, 255), result.getpixel((1, 2)))
            self.assertEqual((255, 0, 0, 255), result.getpixel((1, 3)))
            self.assertEqual((0, 0, 0, 0), result.getpixel((0, 0)))

    def test_proof_raster_rejects_silent_rescaling_or_different_uv(self):
        draw = {"sprite": "unused.png", "size": [2, 2], "vertices": [[0, 0, 0], [3, 0, 0], [0, 2, 0], [3, 2, 0]],
            "uv": [[0, 0], [1, 0], [0, 1], [1, 1]], "indices": [0, 1, 2, 2, 1, 3]}
        with self.assertRaisesRegex(ValueError, "rescale"):
            cover.draw_frame([draw], Path("unused"))
        draw["uv"][0] = [.1, 0]
        with self.assertRaisesRegex(ValueError, "quads"):
            cover.draw_frame([draw], Path("unused"))


if __name__ == "__main__":
    unittest.main()
