"""Behaviour checks for source-only lane openings; no proprietary fixture pixels."""

import copy
from pathlib import Path
from types import SimpleNamespace
import unittest

import lighting_lane_intro as intro


def state(alpha=1, *, x=0, y=0, visible=True, blend=0, mask=None):
    return {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, x, y, 0], "origin": [0, 0, 0],
            "projection": 1, "mult": [1, 1, 1, alpha], "add": [0, 0, 0, 0],
            "hsl": [0, 0, 0], "blend": blend, "mask": mask, "filters": [], "visible": visible}


def geometry(name, resource):
    return {"source": name, "vertices": [[0, 0, 0], [20, 0, 0], [0, 30, 0], [20, 30, 0]], "colours": [],
            "draws": [{"flags": 1, "sourceColour": None, "texture": resource,
                       "uv": [[0, 0], [1, 0], [0, 1], [1, 1]], "indices": [0, 1, 2, 2, 1, 3]}]}


def source_tree():
    return {"canvas": [1920, 1080], "fps": 60, "frames": [
        {"nodes": [[0, 0], [1, 1], [2, 2], [3, 3]], "camera": None},
        {"nodes": [[0, 0], [1, 4], [2, 2]], "camera": None},
        {"nodes": [], "camera": None}],
        "nodes": [
            {"id": 0, "parent": -1, "geometry": None, "source": "original root"},
            {"id": 1, "parent": 0, "geometry": 0, "source": "fading flourish"},
            {"id": 2, "parent": 0, "geometry": 1, "source": "persistent frame"},
            {"id": 3, "parent": 0, "geometry": 2, "source": "removed particle"}],
        "states": [state(mask=[0, 0, 440, 728]), state(blend=8, x=80), state(x=900), state(x=120), state(0, x=80)],
        "cameras": []}


class OriginalLaneOpeningTests(unittest.TestCase):
    def test_all_current_frames_resolve_their_actual_sources(self):
        self.assertEqual(set(intro.FRAMES), set(intro.SOURCE_PACKAGES))
        for version in range(27, 34):
            self.assertEqual(intro.SOURCE_PACKAGES[f"iidx{version}"], f"contents/data/graphic/gmframe{version}.ifs")
        self.assertIn("/0/", intro.SOURCE_PACKAGES["bpl-supernova-hd"])
        for frame in intro.FRAMES:
            if frame.startswith("bpl-") and frame != "bpl-supernova-hd":
                self.assertIn("/1/gmframe_bpls5_", intro.SOURCE_PACKAGES[frame])

    def test_source_viewports_keep_all_cabinet_slots_at_every_key_count(self):
        for frame in intro.FRAMES:
            for layout in intro.SOURCE_MOVIES:
                views = intro.source_viewports(frame, layout)
                self.assertEqual(len(views), 2 if layout == "dp" else 1)
                for left, top, right, bottom in views:
                    self.assertEqual(top, 0)
                    self.assertEqual(right - left, 292 if frame in ("iidx27", "iidx28", "iidx29") else 440)
                    self.assertEqual(bottom, 484 if frame in ("iidx27", "iidx28", "iidx29") else 728)

    def test_only_genuine_transient_meshes_survive_with_original_ancestors(self):
        original = source_tree()
        package = {"geometries": [geometry("flourish", 0), geometry("static", 1), geometry("particle", 2)]}
        snapshot = copy.deepcopy(original)
        selected, receipt = intro.transient_source(original, package)
        self.assertEqual(receipt["transientMeshes"], 2)
        self.assertEqual(receipt["excludedSettledMeshes"], 1)
        self.assertEqual(selected["frames"][0]["nodes"], [[0, 0], [1, 1], [3, 3]])
        self.assertEqual(selected["states"], original["states"])
        self.assertEqual(original, snapshot)
        self.assertEqual(selected["states"][0]["mask"], [0, 0, 440, 728])

    def test_transparent_parent_does_not_keep_a_settled_child_as_static(self):
        root = source_tree()
        root["states"].append(state(0))
        root["frames"][-2]["nodes"][0][1] = 5
        selected, receipt = intro.transient_source(root, {"geometries": [geometry("a", 0), geometry("b", 1), geometry("c", 2)]})
        self.assertEqual(receipt["transientMeshes"], 3)
        self.assertIn([2, 2], selected["frames"][0]["nodes"])

    def test_off_lane_flourishes_are_omitted_before_blend_validation(self):
        root = source_tree()
        root["frames"][-2]["nodes"] = [[0, 0]]
        root["states"][2]["blend"] = 75
        package = {"geometries": [geometry("flourish", 0), geometry("central unsupported", 1), geometry("particle", 2)]}
        transient, _ = intro.transient_source(root, package)
        regional, receipt = intro.lane_region_source(transient, package, [[71, 0, 511, 728]])
        self.assertEqual(receipt["omittedOutsideLaneMeshes"], 1)
        self.assertNotIn([2, 2], regional["frames"][0]["nodes"])
        self.assertIn([1, 1], regional["frames"][0]["nodes"])

    def test_rotated_and_nested_bounds_use_original_affine_order(self):
        root = source_tree()
        root["states"][0] = state(x=100, y=50)
        root["states"][1] = state(x=-30, y=0)
        root["frames"] = [{"nodes": [[0, 0], [1, 1]], "camera": None}, {"nodes": [], "camera": None}]
        root["nodes"] = root["nodes"][:2]
        package = {"geometries": [geometry("nested", 0)]}
        selected, receipt = intro.lane_region_source(root, package, [[71, 0, 511, 728]])
        self.assertEqual(receipt["meshesIntersectingLaneRegion"], 1)
        self.assertEqual(selected["frames"][0]["nodes"], root["frames"][0]["nodes"])

    def test_crop_preserves_uv_states_masks_camera_and_terminal_empty_frame(self):
        root = source_tree()
        package = {"geometries": [geometry("flourish", 0), geometry("static", 1), geometry("particle", 2)], "filters": []}
        transient, _ = intro.transient_source(root, package)
        clip = intro.to_recorded_animation(package, transient, {0: "one", 1: "two", 2: "three"})
        clip["frames"][0]["camera"] = [960, 540, 0, 1000]
        snapshot = copy.deepcopy(clip)
        cropped = intro.crop_clip(clip, [71, 0, 511, 728])
        self.assertEqual((cropped["width"], cropped["height"]), (440, 728))
        self.assertEqual(cropped["geometries"], clip["geometries"])
        self.assertEqual(cropped["states"][:-1], clip["states"])
        self.assertEqual(cropped["frames"][0]["camera"], [889, 540, 0, 1000])
        self.assertEqual(cropped["frames"][-1], {"nodes": [], "camera": None})
        self.assertEqual(clip, snapshot)

    def test_crop_rejects_a_viewport_outside_its_original_canvas(self):
        for rectangle in ([0, 0, 1921, 1080], [-1, 0, 10, 10], [0, 0, 0, 2]):
            with self.assertRaises(ValueError):
                intro.crop_clip({"width": 1920, "height": 1080}, rectangle)

    def test_hidden_geometry_cannot_create_a_successful_visible_opening(self):
        root = source_tree()
        root["states"][0]["visible"] = False
        with self.assertRaisesRegex(ValueError, "No genuine transient"):
            intro.lane_region_source(root, {"geometries": [geometry("a", 0), geometry("b", 1), geometry("c", 2)]},
                                     [[71, 0, 511, 728]])

    def test_asset_copy_paths_cannot_escape_the_author_output(self):
        root = Path.cwd().resolve()
        for relative in ("../HDD/file.png", "D:/HDD/file.png", "art\\file.png", "/file.png"):
            with self.assertRaises(ValueError):
                intro._output_file(root, relative)
        self.assertEqual(intro._output_file(root, "art/opening/file.png"), root / "art/opening/file.png")

    def test_only_exact_terminal_source_loop_is_removed(self):
        actions = [{"action": "PUSH", "objects": ["'loop'", "THIS", "2", "StringConstant(0x390: aeplib)"]},
                   {"action": "GET_VARIABLE"}, {"action": "PUSH", "objects": ["StringConstant(0x815: deepGotoAndPlay)"]},
                   {"action": "CALL_METHOD"}, {"action": "END"}]
        tag = type("AP2DoActionTag", (), {"bytecode": SimpleNamespace(as_dict=lambda: {"actions": actions})})()
        movie = SimpleNamespace(labels={"loop": 1}, tags=[tag], frames=[
            SimpleNamespace(start_tag_offset=0, num_tags=0), SimpleNamespace(start_tag_offset=0, num_tags=1)])
        tags, receipts = intro._root_return_tags(movie)
        self.assertEqual(tags, {id(tag)})
        self.assertEqual(receipts[0]["sourceFrame"], 1)
        actions[2]["objects"] = ["StringConstant(0x815: unexpectedMethod)"]
        with self.assertRaises(ValueError):
            intro._root_return_tags(movie)

    def test_injection_uses_preparation_clock_and_real_group_surfaces(self):
        for layout in intro.SOURCE_MOVIES:
            scene = {"contract": "oms-gameplay-skin-scene.v1", "root": {"id": "omni.frame.iidx33.root"},
                     "templates": [], "instances": [], "bindings": [], "variants": []}
            manifest = {"contract": "oms-gameplay-skin-manifest.v1", "resources": []}
            assets = {"frame": "iidx33", "layout": layout, "resources": [], "animations": [],
                      "durationMs": 3000, "texturePixels": 0, "clips": [
                          {"group": index} for index in range(2 if layout == "dp" else 1)]}
            intro.inject(scene, manifest, frame="iidx33", layout=layout, assets=assets)
            snapshot = copy.deepcopy((scene, manifest))
            intro.inject(scene, manifest, frame="iidx33", layout=layout, assets=assets)
            self.assertEqual((scene, manifest), snapshot)
            self.assertEqual(scene["templates"][0]["root"]["slot"], "playfield.baseplate")
            self.assertEqual(scene["instances"][0]["material"]["resource"], "gameplay/frames/iidx33/common/empty")
            self.assertEqual(len(scene["variants"]), 2 if layout == "dp" else 0)

            def visit(node):
                yield node
                for child in node["children"]:
                    yield from visit(child)

            clips = [node for node in visit(scene["templates"][0]["root"]) if node["type"] == "recorded-animation"]
            self.assertTrue(all(node["clock"] == "preparation.elapsed" for node in clips))
            self.assertIn("preparation.available", {binding["source"] for binding in scene["bindings"]})


if __name__ == "__main__":
    unittest.main()
