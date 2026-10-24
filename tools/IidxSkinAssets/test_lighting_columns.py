"""Original column ownership and exact source recomposition guards."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from lighting_columns import (attach_column_binding, bitmap_consumers, inject_split, partition_recorded_runs,
                              select_source, static_column_clip, verify_settled_columns, compose_source)
from lighting_columns import verify_28_handover
import lighting_persistent as persistent
from test_lighting_persistent import source_fixture


def scene_and_assets(frame="iidx27"):
    from test_lighting_persistent import scene_fixture
    scene, manifest, _ = scene_fixture(frame)
    scene["contract"] = "oms-gameplay-skin-scene.v1"
    scene["stateMachines"] = []
    for root in (scene["root"], *(item["root"] for item in scene["templates"])):
        for node in persistent._walk(root):
            node.setdefault("target", {"kind": "global"})
            node.setdefault("blend", "inherit")
            node.setdefault("properties", {})
            node.setdefault("effects", [])
    for track in scene["tracks"]:
        track["easing"] = "step"
        for index, key in enumerate(track["keyframes"]):
            key["id"] = track["id"] + ".k" + str(index)
    consumers = bitmap_consumers(scene, manifest, frame=frame, layout="p1", resources={"old"})
    assets = {"frame": frame, "layout": "p1", "resources": [{"id": "floorless", "type": "texture", "path": "floorless.png"}],
              "animations": [{"id": "static", "path": "columns.br", "resources": ["columns"]}],
              "static": {"id": "static"}, "backgrounds": [{"oldResource": "old", "oldPath": "old.png", "oldSha256": "source",
                                                          "resource": "floorless", "path": "floorless.png"}],
              "columnTextureIds": ["columns"], "prefix": None, "bitmapConsumers": consumers}
    return scene, manifest, assets


class LightingColumnsTest(unittest.TestCase):
    def test_all_numbered_hd_compositors_keep_native_placeholder_and_side_order(self):
        class Raster:
            def __init__(self):
                self.calls = []

            def render(self, name, omitted=()):
                self.calls.append((name, omitted))
                if name in ("1p_frame", "2p_frame"):
                    image = Image.new("RGBA", (1920, 1080))
                    image.putpixel((300, 25), (255, 255, 255, 255))
                    return image
                return Image.new("RGBA", (1920, 1080), (10, 20, 30, 255))

        parent = Path("artifacts/omni-start-options-20261024").resolve()
        parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="column-composition-test-", dir=parent) as temporary:
            directory = Path(temporary)
            (directory / "gameplay-assets.json").write_text(json.dumps({"native_bga_placeholders": [
                {"movie": movie, "bounds": {"left": 10, "top": 20, "right": 40, "bottom": 40}}
                for movie in ("center_frame_sp", "center_frame_dp_1p")]}), encoding="utf-8")
            for frame in ("iidx30", "iidx31", "iidx32", "iidx33"):
                with self.subTest(frame=frame):
                    raster = Raster()
                    sp = compose_source(raster, directory, frame=frame, layout="p1", source_canvas=[1920, 1080])
                    self.assertEqual(sp.getpixel((290, 25)), (0, 0, 0, 255))
                    self.assertEqual(sp.getpixel((300, 25)), (255, 255, 255, 255))
                    self.assertEqual([name for name, _ in raster.calls], ["1p_frame", "center_frame_sp"])
                    raster = Raster()
                    dp = compose_source(raster, directory, frame=frame, layout="dp", source_canvas=[1920, 1080])
                    self.assertEqual(dp.getpixel((20, 25)), (0, 0, 0, 255))
                    self.assertEqual(dp.getpixel((50, 50)), (10, 20, 30, 255))
                    self.assertEqual([name for name, _ in raster.calls], ["center_frame_dp_1p"])

    def test_exact_column_mesh_selection_preserves_every_ancestor_mask_state_and_uv(self):
        root, geometry, textures = source_fixture("iidx28")
        before = copy.deepcopy(root)
        selected, receipt = select_source(root, geometry, textures)
        self.assertEqual(receipt["meshes"], [2])
        self.assertEqual(root, before)
        self.assertEqual(selected["states"], before["states"])
        self.assertEqual(receipt["layerPaths"], [[[None, -1], [51, 1]]])
        clip = static_column_clip(selected, geometry, {1: "original.column"})
        self.assertEqual(len(clip["frames"]), 2)
        self.assertTrue(clip["loop"])
        self.assertEqual(clip["frames"][-1], {"nodes": [], "camera": None})
        self.assertEqual(clip["geometries"][0]["resource"], "original.column")
        self.assertEqual(clip["geometries"][0]["triangles"], [0, 1, 2, 2, 1, 3])
        self.assertEqual(clip["states"][0]["mask"], [0, 0, 440, 728])
        self.assertEqual(clip["states"][0]["origin"], [3, 4, 0])

    def test_decorative_lane_name_and_mixed_mesh_cannot_be_misclassified_as_a_column(self):
        root, geometry, textures = source_fixture("iidx28")
        textures[1]["name"] = "lane_01.png"
        with self.assertRaisesRegex(ValueError, "No exact"):
            select_source(root, geometry, textures)
        textures[1]["name"] = "lane_1p.png"
        geometry["geometries"][1]["draws"].append(copy.deepcopy(geometry["geometries"][0]["draws"][0]))
        with self.assertRaisesRegex(ValueError, "unrelated"):
            select_source(root, geometry, textures)

    def test_one_frame_column_requires_the_complete_original_tail_to_stay_constant(self):
        root, geometry, textures = source_fixture("iidx28")
        selected, _ = select_source(root, geometry, textures)
        receipt = verify_settled_columns(selected)
        self.assertEqual(receipt["checkedFrames"], 120)
        selected["frames"][230]["nodes"] = []
        with self.assertRaisesRegex(ValueError, "tail still moves"):
            verify_settled_columns(selected)

    def test_28_static_handover_matches_frame30_through_the_entire_remaining_source(self):
        root, geometry, textures = source_fixture("iidx28")
        receipt = verify_28_handover(root, geometry, textures)
        self.assertTrue(receipt["allRemainingSourceStatesIdentical"])
        root["frames"][110]["nodes"][2][1] = 0
        selected, _ = select_source(root, geometry, textures)
        self.assertTrue(verify_settled_columns(selected)["exactSourceStateIdentity"])
        with self.assertRaisesRegex(ValueError, "frame30 hold"):
            verify_28_handover(root, geometry, textures)

    def test_all_bitmap_branches_keep_their_controls_and_fractional_fades_are_rejected(self):
        from test_lighting_persistent import scene_fixture
        scene, manifest, _ = scene_fixture("iidx28")
        for template in scene["templates"]:
            for node in persistent._walk(template["root"]):
                node.setdefault("blend", "alpha")
                node.setdefault("properties", {})
        scene["tracks"][0]["easing"] = "step"
        scene["templates"][0]["root"]["children"][0]["children"][0]["children"][0]["children"][0]["blend"] = "inherit"
        _, resource = persistent.background_owner(scene, manifest, frame="iidx28", layout="p1")
        before = copy.deepcopy(scene)
        rows = bitmap_consumers(scene, manifest, frame="iidx28", layout="p1", resources={resource["id"]})
        self.assertEqual(scene, before)
        self.assertEqual(len(rows), 4)
        self.assertTrue(any(row["controlIds"] for row in rows))
        persistent.background_owner(scene, manifest, frame="iidx28", layout="p1")[0]["properties"]["opacity"] = .5
        with self.assertRaisesRegex(ValueError, "translucent bitmap ancestry"):
            bitmap_consumers(scene, manifest, frame="iidx28", layout="p1", resources={resource["id"]})

    def test_28_partition_retains_every_prefix_hold_mask_origin_uv_and_terminal(self):
        root, geometry, textures = source_fixture("iidx28")
        source, _ = persistent.select_components(root, geometry, textures, frame="iidx28", layout="p1")
        clip = persistent.component_clip(source, geometry, {0: "base", 1: "column"}, frame="iidx28", source_loop=180)
        before = copy.deepcopy(clip)
        parts, receipt = partition_recorded_runs(clip, {"column"}, {"base"})
        base, columns = (part["clip"] for part in parts)
        self.assertEqual(clip, before)
        self.assertEqual(len(base["frames"]), 61)
        self.assertEqual(len(columns["frames"]), 61)
        self.assertFalse(columns["loop"])
        self.assertEqual(base["frames"][30], base["frames"][59])
        self.assertEqual(columns["frames"][30], columns["frames"][59])
        self.assertEqual(base["geometries"][0]["resource"], "base")
        self.assertEqual(columns["geometries"][0]["resource"], "column")
        self.assertTrue(receipt["exactDrawStreamPreserved"])
        self.assertEqual(receipt["checkedFramesIncludingSentinel"], 61)
        self.assertEqual(columns["states"][0]["mask"], clip["states"][0]["mask"])
        self.assertEqual(columns["states"][0]["origin"], clip["states"][0]["origin"])

    def test_unrelated_foreground_in_the_column_source_program_is_rejected_atomically(self):
        root, geometry, textures = source_fixture("iidx28")
        from build_fullcombo_catalog import to_recorded_animation
        clip = to_recorded_animation(geometry, root, {0: "base", 1: "column", 2: "foreground"})
        before = copy.deepcopy(clip)
        with self.assertRaisesRegex(ValueError, "unrelated source mesh"):
            partition_recorded_runs(clip, {"column"}, {"base"})
        self.assertEqual(clip, before)

    def test_dp_source_runs_preserve_interleaved_deck_order_with_a_shared_base_geometry(self):
        root, geometry, textures = source_fixture("iidx28")
        source, _ = persistent.select_components(root, geometry, textures, frame="iidx28", layout="p1")
        clip = persistent.component_clip(source, geometry, {0: "base", 1: "column"}, frame="iidx28", source_loop=180)
        clip["nodes"].extend([copy.deepcopy(clip["nodes"][1]), copy.deepcopy(clip["nodes"][2])])
        for frame in clip["frames"][:-1]:
            frame["nodes"].extend([[3, frame["nodes"][1][1]], [4, frame["nodes"][2][1]]])
        before = copy.deepcopy(clip)
        parts, receipt = partition_recorded_runs(clip, {"column"}, {"base"})
        self.assertEqual(clip, before)
        self.assertEqual(receipt["sourceRunOrder"], ["base", "columns", "base", "columns"])
        self.assertFalse(receipt["aabbIndependenceAssumed"])
        self.assertEqual([len(part["clip"]["geometries"]) for part in parts], [1] * 4)
        self.assertTrue(all(len(part["clip"]["frames"]) == 61 for part in parts))
        self.assertTrue(all(part["clip"]["states"][0]["mask"] == [0, 0, 440, 728] for part in parts))

    def test_split_keeps_each_original_rect_id_and_all_preparation_controls(self):
        scene, manifest, assets = scene_and_assets()
        original_scene, original_manifest = copy.deepcopy(scene), copy.deepcopy(manifest)
        result, output_manifest, receipt = inject_split(scene, manifest, frame="iidx27", layout="p1", assets=assets)
        self.assertEqual(scene, original_scene)
        self.assertEqual(manifest, original_manifest)
        self.assertEqual(result["tracks"], scene["tracks"])
        self.assertEqual(result["bindings"], scene["bindings"])
        self.assertEqual(result["variants"], scene["variants"])
        actual_nodes = {node["id"]: node for item in result["templates"] for node in persistent._walk(item["root"])}
        for consumer in assets["bitmapConsumers"]:
            wrapper = actual_nodes[consumer["node"]]
            self.assertEqual(wrapper["type"], "container")
            self.assertEqual(wrapper["properties"], consumer["sourceLeaf"]["properties"])
            backing, columns = wrapper["children"]
            self.assertEqual(backing["resource"], "floorless")
            self.assertEqual(columns["animation"], "static")
            self.assertEqual(columns["properties"]["z"], -1)
            self.assertIn(columns["id"], receipt["columnTargets"])
        self.assertEqual(len(result["templates"]), len(scene["templates"]))
        self.assertEqual(len(receipt["columnTargets"]), 4)
        for value in receipt["createdNodes"]:
            self.assertRegex(value, r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")
        self.assertTrue(any(item["id"] == "static" for item in output_manifest["animations"]))
        bound = attach_column_binding(result, receipt["columnTargets"], option_id="column-brightness", property_name="brightness")
        self.assertEqual(bound["tracks"], result["tracks"])
        self.assertEqual(bound["bindings"][:len(scene["bindings"])], scene["bindings"])
        self.assertTrue(all(item["property"] == "brightness" for item in bound["bindings"][len(scene["bindings"]):]))

    def test_28_base_and_column_retain_the_same_prefix_clock_and_1000ms_parent_gate(self):
        scene, manifest, assets = scene_and_assets("iidx28")
        owner, _ = persistent.background_owner(scene, manifest, frame="iidx28", layout="p1")
        _, overlay = persistent._assembly(scene, owner)
        original = {"id": "original", "path": "old-prefix.br", "resources": ["base", "columns"]}
        component = {"id": "lp.component", "type": "recorded-animation", "animation": "original", "clock": "preparation.elapsed",
                     "target": {"kind": "global"}, "blend": "alpha", "properties": {"z": -1}, "effects": [], "children": []}
        overlay["children"].append(component)
        manifest["animations"].append(original)
        base = {"id": "prefix.base", "path": "base.br", "resources": ["base"]}
        columns = {"id": "prefix.columns", "path": "moving-columns.br", "resources": ["columns"]}
        assets["prefix"] = {"original": original, "runs": [{"role": "base", "animation": base}, {"role": "columns", "animation": columns}],
                            "receipt": {"exactDrawStreamPreserved": True}}
        assets["animations"].extend([base, columns])
        result, output_manifest, receipt = inject_split(scene, manifest, frame="iidx28", layout="p1", assets=assets)
        overlay_result = next(node for item in result["templates"] for node in persistent._walk(item["root"]) if node["id"] == overlay["id"])
        source_owner = next(node for node in overlay_result["children"] if node["id"] == component["id"])
        self.assertEqual(source_owner["type"], "container")
        self.assertEqual(source_owner["properties"], {"z": -1})
        base_result, column_result = source_owner["children"]
        self.assertEqual(base_result["properties"], {"z": 0})
        self.assertEqual(column_result["properties"], {"z": -1})
        self.assertEqual(base_result["clock"], column_result["clock"])
        self.assertEqual(base_result["clock"], "preparation.elapsed")
        self.assertEqual(result["tracks"], scene["tracks"])
        self.assertNotIn(base_result["id"], receipt["columnTargets"])
        self.assertIn(column_result["id"], receipt["columnTargets"])
        self.assertNotIn("original", {item["id"] for item in output_manifest["animations"]})

    def test_typed_binding_identity_conflict_and_a_competing_program_reject_without_mutating(self):
        scene, manifest, assets = scene_and_assets()
        scene["tracks"][0]["keyframes"][0]["id"] = "lc.n0"
        before = copy.deepcopy(scene)
        with self.assertRaisesRegex(ValueError, "reserved identities"):
            inject_split(scene, manifest, frame="iidx27", layout="p1", assets=assets)
        self.assertEqual(scene, before)
        scene["tracks"][0]["keyframes"][0]["id"] = "original.key"
        result, _, receipt = inject_split(scene, manifest, frame="iidx27", layout="p1", assets=assets)
        result["variants"].append({"id": "other.program", "target": receipt["columnTargets"][0], "property": "brightness", "cases": []})
        before = copy.deepcopy(result)
        with self.assertRaisesRegex(ValueError, "competing scalar program"):
            attach_column_binding(result, receipt["columnTargets"], option_id="column-brightness", property_name="brightness")
        self.assertEqual(result, before)


class ReviewedColumnMigrationTest(unittest.TestCase):
    def reference_fixture(self, directory):
        from build_lighting_columns import REVIEW_CONTRACT, SUPPORTED_RECEIPTS
        image_path = directory / "synthetic-test-only.png"
        Image.new("RGBA", (3, 2), (20, 40, 60, 255)).save(image_path)
        value = {"contract": REVIEW_CONTRACT, "status": "reviewed",
                 "source": {field: "0" * 64 for field in ("manifestSha256", "generationSha256", "archiveSha256")},
                 "columnSourceReferenceSha256": "1" * 64,
                 "setting": {"id": "column-brightness", "min": .2, "max": 1, "step": .2, "default": 1,
                    "mapping": {"property": "brightness", "formula": "value"},
                    "reference": {"menuLabel": "Synthetic unit-test fixture", "description": "Not an original-game receipt",
                        "review": "Synthetic unit-test data only", "screenshots": [{"path": image_path.name,
                            "sha256": hashlib.sha256(image_path.read_bytes()).hexdigest(), "supports": sorted(SUPPORTED_RECEIPTS)}]}}}
        path = directory / "synthetic-review.json"
        path.write_text(json.dumps(value), encoding="utf-8")
        return path, value

    def test_review_requires_layer_boundary_rgb_adaptation_and_unchanged_image_bytes(self):
        from build_lighting_columns import load_reviewed_column
        temporary_parent = Path("artifacts/omni-start-options-20261024").resolve()
        temporary_parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(prefix="column-test-", dir=temporary_parent) as temporary:
            directory = Path(temporary)
            path, value = self.reference_fixture(directory)
            loaded = load_reviewed_column(path)
            self.assertEqual(loaded["definition"]["min"], .2)
            self.assertEqual(loaded["definition"]["step"], .2)
            self.assertEqual(loaded["setting"]["mapping"], {"property": "brightness", "formula": "value"})
            value["setting"]["mapping"]["formula"] = "1-value"
            path.write_text(json.dumps(value), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "OMS RGB visual adaptation"):
                load_reviewed_column(path)
            value["setting"]["mapping"]["formula"] = "value"
            value["setting"]["mapping"]["property"] = "opacity"
            path.write_text(json.dumps(value), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "OMS RGB visual adaptation"):
                load_reviewed_column(path)
            value["setting"]["mapping"]["property"] = "brightness"
            value["setting"]["reference"]["screenshots"][0]["supports"].remove("layer")
            path.write_text(json.dumps(value), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "affected-layer boundary"):
                load_reviewed_column(path)
            path, _ = self.reference_fixture(directory)
            (directory / "synthetic-test-only.png").write_bytes(b"changed")
            with self.assertRaisesRegex(ValueError, "screenshot changed"):
                load_reviewed_column(path)

    def test_review_rejects_nonfinite_bool_and_non_default_one_values(self):
        from build_lighting_columns import column_definition
        valid = {"id": "column-brightness", "min": .2, "max": 1, "step": .2, "default": 1}
        for field, value in (("min", float("nan")), ("max", float("inf")), ("step", True), ("default", .8), ("step", .3)):
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                column_definition({**valid, field: value})

    def test_shared_ini_retargets_all_layout_scopes_without_changing_empty_or_other_materials(self):
        from build_lighting_columns import retarget_background
        scene, _, assets = scene_and_assets()
        assets["backgrounds"][0]["oldPath"] = "gameplay/frames/iidx27/backgrounds/p1.png"
        assets["backgrounds"][0]["path"] = "art/lc/p1.png"
        materials = {layout: "art/lc/" + layout for layout in ("p1", "p2", "dp")}
        lines = [b'[GameplaySkin.Common:1]\r\n']
        for layout in ("p1", "p1", "p2", "dp"):
            lines.append(f'stage.background: resource Provide "gameplay/frames/iidx27/backgrounds/{layout}"\r\n'.encode())
        lines.append(b'stage.background: resource Provide "gameplay/frames/iidx27/common/empty"\r\n')
        lines.append(b'playfield.baseplate: resource Provide "original/base"\r\n')
        data, before = b"".join(lines), copy.deepcopy(scene)
        result, rewritten, receipt = retarget_background(scene, data, frame="iidx27", layout="p1", assets=assets, layout_materials=materials)
        self.assertEqual(scene, before)
        self.assertEqual(receipt["allLayoutProvideCounts"], {"p1": 2, "p2": 1, "dp": 1})
        self.assertNotIn(b'/backgrounds/', rewritten)
        self.assertIn(lines[-1], rewritten)
        self.assertIn(lines[-2], rewritten)
        self.assertEqual(result["instances"][0]["material"], {"slot": "stage.background", "resource": "art/lc/p1"})


if __name__ == "__main__":
    unittest.main()
