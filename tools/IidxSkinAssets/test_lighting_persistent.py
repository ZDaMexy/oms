"""Persistent component ownership, source fidelity and failed-baseline checks."""

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

import lighting_persistent as persistent


def source_state(x=0, mask=None):
    return {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, x, 0, 0], "origin": [3, 4, 0],
            "projection": 1, "mult": [1, 1, 1, 1], "add": [0, 0, 0, 0], "hsl": [0, 0, 0],
            "blend": 0, "mask": mask, "filters": [], "visible": True}


def source_fixture(frame="iidx33"):
    textures = [{"name": "unit_asterisk.png"}, {"name": "other.png"}] if frame in persistent.ASTERISK_FRAMES else [
        {"name": "lane_base.png"}, {"name": "lane_1p.png"}, {"name": "other.png"}]
    nodes = [{"id": 0, "parent": -1, "depth": -1, "sourceTag": None, "geometry": None}]
    geometries = []
    for index in range(len(textures)):
        nodes.append({"id": index + 1, "parent": 0, "depth": index, "sourceTag": 50 + index, "geometry": index})
        geometries.append({"source": "shape" + str(index), "vertices": [[0, 0, 0], [10, 0, 0], [0, 20, 0], [10, 20, 0]],
                           "colours": [], "draws": [{"texture": index, "uv": [[0, 0], [1, 0], [0, 1], [1, 1]],
                             "indices": [0, 1, 2, 2, 1, 3], "flags": 3, "sourceColour": None}]})
    states = [source_state(mask=[0, 0, 440, 728]), source_state(20)]
    frames = [{"nodes": [[node["id"], 0 if node["id"] == 0 else 1] for node in nodes], "camera": None}
              for _ in range(300)] + [{"nodes": [], "camera": None}]
    root = {"fps": 60, "frameCount": 300, "canvas": [1920, 1080], "frames": frames,
            "nodes": nodes, "states": states, "cameras": []}
    return root, {"geometries": geometries, "filters": []}, textures


def sprite(identifier, resource):
    return {"id": identifier, "type": "sprite", "resource": resource, "properties": {}, "children": []}


def scene_fixture(frame="iidx33"):
    overlay = {"id": "q3", "type": "container", "properties": {"opacity": 0}, "children": [sprite("q4", "old")]}
    tracks = [{"id": "t0", "target": "q3", "property": "opacity", "clock": "preparation.elapsed",
               "keyframes": [{"time": 0, "value": 1}, {"time": 1000, "value": 0}]}]
    if frame == "iidx33":
        star = sprite("q5", "star")
        star["type"] = "projective-sprite"
        overlay["children"].append(star)
        tracks.append({"id": "t1", "target": "q5", "property": "projective-quad", "clock": "preparation.elapsed",
                       "keyframes": [{"time": 0, "value": {"opacity": 0}}, {"time": 1000, "value": {"opacity": 1}}]})
    active = {"id": "q2", "type": "container", "children": [overlay, sprite("q6", "old")]}
    enabled = {"id": "q1", "type": "container", "children": [active, sprite("q7", "old")]}
    root = {"id": "q0", "type": "container", "slot": "stage.background",
            "children": [enabled, sprite("q8", "old")]}
    scene = {"root": {"id": "already.compacted", "children": []},
             "templates": [{"id": "p0", "root": root}, {"id": "notes", "root": sprite("note", "notes")}],
             "instances": [{"id": "i0", "template": "p0", "material": {
                 "slot": "stage.background", "resource": f"gameplay/frames/{frame}/backgrounds/p1"}}],
             "bindings": [{"id": "b0", "target": "q1", "property": "visible", "source": "option.entrance-animation"},
                          {"id": "b1", "target": "q2", "property": "visible", "source": "preparation.available"}],
             "variants": [], "tracks": tracks}
    manifest = {"resources": [{"id": "old", "path": f"gameplay/frames/{frame}/backgrounds/p1.png", "type": "texture"},
                              {"id": "star", "path": "art/star.png", "type": "texture"}], "animations": []}
    assets = {"frame": frame, "layout": "p1", "resources": [{"id": "split", "type": "texture", "path": "art/split.png"}],
              "background": "split", "sourceTextureIds": ["star"], "animation": {"id": "anim", "path": "clip.br", "resources": ["star"]},
              "clock": "presentation.elapsed" if frame in persistent.ASTERISK_FRAMES else "preparation.elapsed"}
    return scene, manifest, assets


class LightingPersistentTest(unittest.TestCase):
    def test_source_mesh_selection_keeps_exact_hierarchy_masks_uv_and_loop_prefix(self):
        root, package, textures = source_fixture()
        snapshot = copy.deepcopy(root)
        selected, receipt = persistent.select_components(root, package, textures, frame="iidx33", layout="p1")
        clip = persistent.component_clip(selected, package, {0: "star"}, frame="iidx33", source_loop=180)
        self.assertEqual([1], receipt["meshes"])
        self.assertEqual([[[None, -1], [50, 0]]], receipt["omittedLayerPaths"])
        self.assertEqual(root, snapshot)
        self.assertEqual(301, len(clip["frames"]))
        self.assertTrue(clip["loop"])
        self.assertEqual(180, clip["loopStartFrame"])
        self.assertEqual([0, 0, 440, 728], clip["states"][0]["mask"])
        self.assertEqual([3, 4, 0], clip["states"][0]["origin"])
        self.assertEqual([0, 0, 0, 0, 0], clip["geometries"][0]["vertices"][0])
        self.assertEqual([0, 1, 2, 2, 1, 3], clip["geometries"][0]["triangles"])
        self.assertEqual("star", clip["geometries"][0]["resource"])
        self.assertTrue(clip["frames"][299]["nodes"])
        self.assertEqual({"nodes": [], "camera": None}, clip["frames"][300])

    def test_28_hold_uses_the_actual_settled_frame_and_has_no_loop(self):
        root, package, textures = source_fixture("iidx28")
        selected, _ = persistent.select_components(root, package, textures, frame="iidx28", layout="p1")
        clip = persistent.component_clip(selected, package, {0: "base", 1: "surface"}, frame="iidx28", source_loop=180)
        self.assertEqual(61, len(clip["frames"]))
        self.assertFalse(clip["loop"])
        self.assertNotIn("loopStartFrame", clip)
        self.assertEqual(clip["frames"][30], clip["frames"][59])
        selected["frames"][100]["nodes"][1][1] = 0
        with self.assertRaisesRegex(ValueError, "settle"):
            persistent.component_clip(selected, package, {0: "base", 1: "surface"}, frame="iidx28", source_loop=180)

    def test_each_proven_bpl_frame_uses_its_own_star_and_the_full_source_return(self):
        self.assertEqual(7, len(persistent.BPL_FRAMES))
        self.assertNotIn("bpl-supernova-hd", persistent.ASTERISK_FRAMES)
        for frame in persistent.BPL_FRAMES:
            with self.subTest(frame=frame):
                root, package, textures = source_fixture(frame)
                selected, _ = persistent.select_components(root, package, textures, frame=frame, layout="p1")
                resource = frame + ".original-star"
                clip = persistent.component_clip(selected, package, {0: resource}, frame=frame, source_loop=180)
                self.assertEqual(resource, clip["geometries"][0]["resource"])
                self.assertEqual(301, len(clip["frames"]))
                self.assertEqual(180, clip["loopStartFrame"])
                self.assertTrue(clip["frames"][299]["nodes"])
                self.assertEqual({"nodes": [], "camera": None}, clip["frames"][300])
                scene, manifest, assets = scene_fixture(frame)
                result = persistent.inject(scene, manifest, frame=frame, layout="p1", assets=assets)
                owner, _ = persistent.background_owner(scene, manifest, frame=frame, layout="p1")
                active, _ = persistent._assembly(scene, owner)
                animation = next(child for child in active["children"] if child["type"] == "recorded-animation")
                self.assertEqual("presentation.elapsed", animation["clock"])
                self.assertEqual(-1, animation["properties"]["z"])
                self.assertEqual(0, result["removedIntroNodes"])
                self.assertEqual(0, result["removedInlineTrackBytes"])

    def test_hd_bpl_retains_safe_bga_edges_and_its_original_fixed_dp_symbol_layer(self):
        from PIL import Image
        from unittest.mock import patch

        class SourceRaster:
            def __init__(self):
                self.calls = []

            def render(self, name, omitted=()):
                self.calls.append((name, omitted))
                image = Image.new("RGBA", (8, 6))
                if name == "dp_frame_1p":
                    image.putpixel((6, 5), (5, 6, 7, 255))
                else:
                    image.putpixel((2, 2), (1, 2, 3, 255))
                return image

        with tempfile.TemporaryDirectory() as temporary:
            research = Path(temporary)
            (research / "effective-bga-viewports.json").write_text(json.dumps({"viewports": [{
                "movie": "center_frame_dp_1p", "fullyTransparentRectangularViewportCandidate": {"bounds": [1, 1, 7, 4]}}]}))
            raster = SourceRaster()
            omitted = [[[None, -1], [99, 3]]]
            with patch.object(persistent, "CANVAS", (8, 6)):
                image = persistent._compose(raster, research, frame="bpl-apina", layout="dp", omitted=omitted)
            self.assertEqual((0, 0, 0, 255), image.getpixel((1, 3)))
            self.assertEqual((0, 0, 0, 0), image.getpixel((7, 3)))
            self.assertEqual((0, 0, 0, 0), image.getpixel((1, 4)))
            self.assertEqual((1, 2, 3, 255), image.getpixel((2, 2)))
            self.assertEqual((5, 6, 7, 255), image.getpixel((6, 5)))
            self.assertEqual([("center_frame_dp_1p", omitted), ("dp_frame_1p", ())], raster.calls)

    def test_missing_components_and_unproven_loop_are_rejected(self):
        root, package, textures = source_fixture()
        with self.assertRaisesRegex(ValueError, "closure changed"):
            persistent.select_components(root, package, textures, frame="iidx33", layout="dp")
        with self.assertRaisesRegex(ValueError, "literal original"):
            persistent.component_clip(root, package, {}, frame="iidx33", source_loop=179)

    def test_dp_retains_the_original_mirrored_parent_and_two_distinct_source_instances(self):
        root, package, textures = source_fixture()
        root["nodes"] = [
            {"id": 0, "parent": -1, "depth": -1, "sourceTag": None, "geometry": None},
            {"id": 1, "parent": 0, "depth": 4, "sourceTag": 416, "geometry": None},
            {"id": 2, "parent": 1, "depth": 14, "sourceTag": 267, "geometry": 0},
            {"id": 3, "parent": 0, "depth": 5, "sourceTag": 336, "geometry": None},
            {"id": 4, "parent": 3, "depth": 14, "sourceTag": 267, "geometry": 0}]
        reflected = source_state(1624, mask=[0, 0, 720, 1166])
        reflected["matrix"][0] = -1
        root["states"].append(reflected)
        root["frames"] = [{"nodes": [[0, 0], [1, 2], [2, 1], [3, 0], [4, 1]], "camera": None}
                          for _ in range(300)] + [{"nodes": [], "camera": None}]
        selected, receipt = persistent.select_components(root, package, textures, frame="iidx33", layout="dp")
        clip = persistent.component_clip(selected, package, {0: "shared.star"}, frame="iidx33", source_loop=180)
        self.assertEqual([2, 4], receipt["meshes"])
        self.assertEqual(1, len(clip["geometries"]))
        self.assertEqual([1, 3], [node["parent"] for node in clip["nodes"] if node["geometry"] == 0])
        reflected_state = clip["states"][dict(clip["frames"][180]["nodes"])[1]]
        self.assertEqual(-1, reflected_state["matrix"][0])
        self.assertEqual([0, 0, 720, 1166], reflected_state["mask"])
        self.assertEqual(root["states"][2]["origin"], reflected_state["origin"])

    def test_33_replaces_active_baked_and_removes_duplicate_intro_star_only(self):
        scene, manifest, assets = scene_fixture()
        notes = copy.deepcopy(scene["templates"][1])
        result = persistent.inject(scene, manifest, frame="iidx33", layout="p1", assets=assets)
        nodes = {node["id"]: node for node in persistent._walk(scene["templates"][0]["root"])}
        self.assertNotIn("q5", nodes)
        self.assertEqual(["t0"], [track["id"] for track in scene["tracks"]])
        self.assertEqual("split", nodes["q4"]["resource"])
        self.assertEqual("split", nodes["q6"]["resource"])
        self.assertEqual("old", nodes["q7"]["resource"])
        self.assertEqual("old", nodes["q8"]["resource"])
        self.assertEqual("presentation.elapsed", nodes["lp.iidx33.p1"]["clock"])
        self.assertEqual(notes, scene["templates"][1])
        self.assertEqual(1, result["removedIntroNodes"])
        self.assertGreater(result["removedInlineTrackBytes"], 0)
        with self.assertRaisesRegex(ValueError, "already been injected"):
            persistent.inject(scene, manifest, frame="iidx33", layout="p1", assets=assets)

    def test_28_changes_temporary_overlay_but_preserves_complete_handover_and_fallbacks(self):
        scene, manifest, assets = scene_fixture("iidx28")
        persistent.inject(scene, manifest, frame="iidx28", layout="p1", assets=assets)
        nodes = {node["id"]: node for node in persistent._walk(scene["templates"][0]["root"])}
        self.assertEqual("split", nodes["q4"]["resource"])
        for identifier in ("q6", "q7", "q8"):
            self.assertEqual("old", nodes[identifier]["resource"])
        self.assertIn(nodes["lp.iidx28.p1"], nodes["q3"]["children"])
        self.assertEqual("preparation.elapsed", nodes["lp.iidx28.p1"]["clock"])

    def test_persistent_components_draw_in_front_of_the_replaced_background_siblings(self):
        for frame in ("iidx33", "iidx28"):
            with self.subTest(frame=frame):
                scene, manifest, assets = scene_fixture(frame)
                persistent.inject(scene, manifest, frame=frame, layout="p1", assets=assets)
                root, _ = persistent.background_owner(scene, manifest, frame=frame, layout="p1")
                active, overlay = persistent._assembly(scene, root)
                parent = active if frame == "iidx33" else overlay
                animation = next(child for child in parent["children"] if child["type"] == "recorded-animation")
                background_siblings = [child for child in parent["children"]
                    if any(node.get("resource") == assets["background"] for node in persistent._walk(child))]
                self.assertTrue(background_siblings)
                # RuntimeHost assigns root.Depth = baseDepth + z. Lower depth is
                # in front, so a positive z would hide the source mesh behind
                # these opaque background owners, even with correct C5 timing.
                for background in background_siblings:
                    self.assertLess(animation["properties"]["z"], background.get("properties", {}).get("z", 0))

    def test_owner_is_identified_by_catalog_material_not_compacted_id_or_root_name(self):
        scene, manifest, _ = scene_fixture()
        owner, baked = persistent.background_owner(scene, manifest, frame="iidx33", layout="p1")
        self.assertEqual("q0", owner["id"])
        self.assertEqual("old", baked["id"])
        scene["instances"][0]["material"]["resource"] = "gameplay/frames/iidx28/backgrounds/p1"
        with self.assertRaisesRegex(ValueError, "one exact Frame"):
            persistent.background_owner(scene, manifest, frame="iidx33", layout="p1")

    def test_failed_rgba_baseline_keeps_evidence_and_never_changes_the_old_png(self):
        from PIL import Image

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            expected = root / "old.png"
            Image.new("RGBA", (3, 2), (10, 20, 30, 255)).save(expected)
            original = expected.read_bytes()
            rendered = Image.new("RGBA", (3, 2), (10, 20, 30, 255))
            rendered.putpixel((1, 0), (10, 20, 30, 254))
            check = persistent.verify_background(rendered, expected, root / "evidence")
            self.assertFalse(check["matches"])
            self.assertEqual(1, check["differentPixels"])
            self.assertEqual(original, expected.read_bytes())
            self.assertTrue((root / "evidence/baseline-render.png").is_file())
            self.assertTrue((root / "evidence/baseline-difference.png").is_file())
            self.assertTrue(persistent.verify_background(Image.open(expected), expected, root / "unused")["matches"])
            self.assertFalse((root / "unused").exists())
            with self.assertRaisesRegex(ValueError, "mismatched source background"):
                persistent.prepare_assets({"frames": {"iidx33": {"p1": {"status": "baseline-mismatch"}}}},
                                          root, frame="iidx33", layout="p1", manifest={"resources": []})

    def test_prepare_reuses_identical_selected_png_and_keeps_source_loop_and_uv(self):
        import brotli
        from PIL import Image

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            library, reference_dir, original_author, output = [root / name for name in ("library", "reference", "original", "author")]
            for path in (library, reference_dir, original_author, output):
                path.mkdir()
            source = library / "star.png"
            Image.new("RGBA", (2, 2), (0, 200, 100, 255)).save(source)
            data = source.read_bytes()
            (output / "existing.png").write_bytes(data)
            Image.new("RGBA", (4, 3), (1, 2, 3, 255)).save(reference_dir / "split.png")
            background = reference_dir / "split.png"
            clip = {"geometries": [{"resource": "lp.source", "vertices": [[0, 0, 0, 0, 0]], "triangles": []}],
                    "loop": True, "loopStartFrame": 180}
            encoded = brotli.compress(json.dumps(clip).encode())
            (reference_dir / "clip.br").write_bytes(encoded)
            row = {"status": "verified", "textures": [{"id": "lp.source", "source": "star.png", "sha256": hashlib.sha256(data).hexdigest(),
                    "bytes": len(data), "filter": "nearest", "width": 2, "height": 2}],
                   "background": {"path": "split.png", "sha256": hashlib.sha256(background.read_bytes()).hexdigest()},
                   "clip": {"path": "clip.br", "sha256": hashlib.sha256(encoded).hexdigest()},
                   "sourceOwner": {"baked": {"path": "existing.png"}, "bakedSha256": hashlib.sha256(data).hexdigest()},
                   "clock": "presentation.elapsed", "handoverMs": None}
            reference = {"directory": reference_dir, "library": library, "sourceAuthor": original_author,
                         "frames": {"iidx33": {"p1": row}}}
            manifest = {"resources": [{"id": "already.selected", "path": "existing.png", "type": "texture", "filter": "nearest"}]}
            with self.assertRaisesRegex(ValueError, "overlaps its reference"):
                persistent.prepare_assets(reference, original_author, frame="iidx33", layout="p1", manifest=manifest)
            prepared = persistent.prepare_assets(reference, output, frame="iidx33", layout="p1", manifest=manifest)
            self.assertEqual(["already.selected"], prepared["sourceTextureIds"])
            self.assertEqual(1, len(prepared["resources"]))
            actual = json.loads(brotli.decompress((output / prepared["animation"]["path"]).read_bytes()))
            self.assertEqual(180, actual["loopStartFrame"])
            self.assertEqual("already.selected", actual["geometries"][0]["resource"])
            self.assertEqual(clip["geometries"][0]["vertices"], actual["geometries"][0]["vertices"])
            self.assertEqual(data, (output / "existing.png").read_bytes())
            # The preserved BPL backgrounds never declared a separate raw star.
            # Their new source PNG must be copied exactly, rather than assuming
            # that the numbered33 byte-identical alias is already selected.
            Image.new("RGBA", (4, 3), (8, 9, 10, 255)).save(output / "baked-only.png")
            bpl_row = copy.deepcopy(row)
            bpl_row["sourceOwner"] = {"baked": {"path": "baked-only.png"},
                "bakedSha256": hashlib.sha256((output / "baked-only.png").read_bytes()).hexdigest()}
            reference["frames"]["bpl-apina"] = {"p1": bpl_row}
            bpl_manifest = {"resources": [{"id": "baked", "type": "texture", "path": "baked-only.png", "filter": "nearest"}]}
            bpl = persistent.prepare_assets(reference, output, frame="bpl-apina", layout="p1", manifest=bpl_manifest)
            self.assertEqual(2, len(bpl["resources"]))
            self.assertEqual(["lp.source"], bpl["sourceTextureIds"])
            raw_star = next(item for item in bpl["resources"] if item["id"] == "lp.source")
            self.assertEqual(data, (output / raw_star["path"]).read_bytes())
            source.write_bytes(data + b"changed")
            with self.assertRaisesRegex(ValueError, "PNG changed"):
                persistent.prepare_assets(reference, output, frame="iidx33", layout="p1", manifest=manifest)
            source.write_bytes(data)
            (output / "existing.png").write_bytes(data + b"changed")
            with self.assertRaisesRegex(ValueError, "verified original baked"):
                persistent.prepare_assets(reference, output, frame="iidx33", layout="p1", manifest=manifest)

    def test_path_boundaries_reject_hdd_and_reference_escape_before_access(self):
        for value in ("D:/HDD/raw", "d:/library"):
            with self.assertRaisesRegex(ValueError, "non-HDD"):
                persistent._independent(value, exists=False)
        root = Path.cwd().resolve()
        for relative in ("../private-data/secret", "D:/HDD/raw", "art\\source.png", "/absolute.png"):
            with self.assertRaises(ValueError):
                persistent._file(root, relative, exists=False)

    def test_reused_pixel_verification_requires_unchanged_inputs_and_output_bytes(self):
        import brotli
        from PIL import Image

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            library, previous, output = [root / name for name in ("library", "previous", "output")]
            for path in (library, previous, output):
                path.mkdir()
            raw = library / "frame.afp"
            raw.write_bytes(b"preserved original AFP")
            recording = root / "root.json.br"
            recording.write_bytes(b"preserved original recording")
            image = Image.new("RGBA", (4, 3), (1, 2, 3, 255))
            image.save(previous / "split.png")
            background = (previous / "split.png").read_bytes()
            source, package, textures = source_fixture()
            selected, _ = persistent.select_components(source, package, textures, frame="iidx33", layout="p1")
            clip = persistent.component_clip(selected, package, {0: "star"}, frame="iidx33", source_loop=180)
            compressed = brotli.compress(json.dumps(clip).encode())
            (previous / "clip.br").write_bytes(compressed)
            owner = {"bakedSha256": "preserved baked PNG"}
            source_row = {"raw": "frame.afp", "sha256": persistent._sha(raw.read_bytes())}
            row = {"status": "verified", "baseline": {"matches": True, "expectedPngSha256": owner["bakedSha256"]},
                   "sourceOwner": owner, "sourceRootSha256": persistent._sha(recording.read_bytes()),
                   "sourceRawAfpSha256": source_row["sha256"], "textures": [],
                   "background": {"path": "split.png", "sha256": persistent._sha(background),
                                  "rgbaSha256": persistent._sha(image.tobytes())},
                   "clip": {"path": "clip.br", "sha256": persistent._sha(compressed)}}
            reference = {"directory": previous, "library": library, "referencePath": previous / "reference.json",
                         "referenceSha256": "previous index", "frames": {"iidx33": {"p1": row}}}
            copied = persistent._reuse_layout(reference, output, frame="iidx33", layout="p1", owner=owner,
                                             source_row=source_row, root_path=recording)
            self.assertTrue(copied["reusedPixelVerification"]["allInputsAndOutputBytesUnchanged"])
            self.assertEqual(compressed, (output / "clip.br").read_bytes())
            self.assertEqual(background, (output / "split.png").read_bytes())
            (previous / "split.png").write_bytes(background + b"corrupted")
            with self.assertRaisesRegex(ValueError, "asset changed"):
                persistent._reuse_layout(reference, output, frame="iidx33", layout="p1", owner=owner,
                                         source_row=source_row, root_path=recording)
            (previous / "split.png").write_bytes(background)
            recording.write_bytes(b"changed recording")
            with self.assertRaisesRegex(ValueError, "exact source inputs"):
                persistent._reuse_layout(reference, output, frame="iidx33", layout="p1", owner=owner,
                                         source_row=source_row, root_path=recording)


if __name__ == "__main__":
    unittest.main()
