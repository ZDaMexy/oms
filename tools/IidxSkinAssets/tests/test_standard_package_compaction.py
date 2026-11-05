"""Reference compaction preserves source pixels, animation numbers and material owners."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest

import brotli

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_package_compaction import all_declared_ids, compact_package, encoded, rewrite_scene, scene_declarations


class PackageCompactionTest(unittest.TestCase):
    def fixture(self, parent):
        source = parent / "source"
        source.mkdir()
        texture = "original.texture.long-name"
        font = "original.bitmap-font.long-name"
        animation = "original.long-note.white.base.clip"
        image = "original/very-long-texture-file.png"
        movie = "original/very-long-recorded-animation.json.br"
        marker = "markers/frame-p1-information"
        manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                    "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1",
                    "resources": [{"id": texture, "type": "texture", "path": image},
                                  {"id": "original.marker", "type": "texture", "path": marker + ".png"}],
                    "bitmapFonts": [{"id": font, "lineHeight": 1, "glyphs": [
                        {"character": "0", "resource": texture, "advance": 1, "rect": [0, 0, 1, 1]},
                        {"character": " ", "resource": "original.marker", "advance": 1}]}],
                    "animations": [{"id": animation, "path": movie, "resources": [texture]}],
                    "options": [{"id": "example", "type": "choice", "default": "a", "choices": [{"value": "a", "preview": image}]}],
                    "contentCatalog": "content.json"}
        scene = {"contract": "oms-gameplay-skin-scene.v1",
                 "root": {"id": "root", "type": "container", "children": [
                     {"id": "source.sprite", "type": "sprite", "resource": texture},
                     {"id": "source.text", "type": "text", "properties": {"bitmap-font": font, "text": texture}},
                     {"id": "source.movie", "type": "recorded-animation", "resource": animation}]},
                 "templates": [{"id": "owner.template", "root": {"id": "owner", "type": "sprite", "resource": "original.marker"}}],
                 "instances": [{"id": "owner.instance", "template": "owner.template", "material": {"slot": "decoration", "resource": marker}}],
                 "tracks": [], "bindings": [],
                 "variants": [{"id": "variant", "property": "resource", "default": texture, "cases": [{"id": "case", "key": "a", "resource": texture}]}],
                 "stateMachines": [{"id": "machine", "states": [{"id": "state", "assignments": [{"target": "source.sprite", "property": "resource", "value": texture}]}]}]}
        branch = {"contract": "oms-gameplay-skin-scene.v1", "root": {"id": "branch", "type": "container", "children": []},
                  "templates": [], "instances": [], "tracks": [], "bindings": [], "variants": [], "stateMachines": []}
        catalogue = {"contract": "oms-gameplay-skin-content-catalog.v1", "modules": [{"id": "style", "default": "a", "choices": [
            {"value": "a", "scene": "branch.json", "preview": image},
            {"value": "b", "scene": "other.json", "variants": [{"when": {"example": "a"}, "scene": "variant.json"}]}]}]}
        recording = {"contract": "oms-gameplay-recorded-animation.v1", "width": 2, "height": 2, "fps": 60,
                     "loop": False, "trigger": "object.long-note-transition",
                     "geometries": [{"resource": texture, "vertices": [[0.125, 1, 0, 0.5, 1], [1, 0, 0, 0, 0], [0, 0, 0, 1, 1]], "triangles": [0, 1, 2]},
                                    {"resource": None, "colour": [0, 0, 0, 1], "vertices": [], "triangles": []}],
                     "nodes": [], "states": [], "frames": [[], []]}
        raw_recording = encoded(recording).replace(b"0.125", b"0.12500000000000000")
        files = {"gameplay-skin.json": encoded(manifest), "gameplay-skin.scene.json": encoded(scene),
                 "branch.json": encoded(branch), "other.json": encoded({**branch, "root": {"id": "other", "type": "container"}}),
                 "variant.json": encoded({**branch, "root": {"id": "variant-root", "type": "container"}}), "content.json": encoded(catalogue),
                 "skin.ini": ("[General]\nName: Generated test fixture\n[Bms]\nProvide decoration: " + marker + "\n").encode(),
                 image: b"synthetic opaque image bytes", marker + ".png": b"synthetic marker bytes",
                 movie: brotli.compress(raw_recording)}
        for path, data in files.items():
            destination = source / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(data)
        return source, manifest, scene, recording, files

    def test_all_references_and_compressed_movie_are_rewritten_without_changing_visual_data(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, manifest, scene, recording, files = self.fixture(root)
            output, receipt = root / "output", root / "receipt.json"
            result = compact_package(source, output, receipt)
            image = manifest["resources"][0]["path"]
            self.assertEqual((output / result["paths"][image]).read_bytes(), files[image])
            self.assertEqual((output / "skin.ini").read_bytes(), files["skin.ini"])
            self.assertEqual(result["paths"]["markers/frame-p1-information.png"], "markers/frame-p1-information.png")
            changed = json.loads((output / "gameplay-skin.scene.json").read_text("utf8"))
            self.assertEqual(changed["root"]["children"][0]["resource"], result["resources"][manifest["resources"][0]["id"]])
            self.assertEqual(changed["root"]["children"][1]["properties"]["text"], scene["root"]["children"][1]["properties"]["text"])
            self.assertEqual(changed["root"]["children"][2], scene["root"]["children"][2])
            self.assertEqual(changed["instances"], scene["instances"])
            movie = manifest["animations"][0]["path"]
            self.assertTrue(result["paths"][movie].endswith(".json.br"))
            raw = brotli.decompress((output / result["paths"][movie]).read_bytes())
            self.assertIn(b"0.12500000000000000", raw)
            expected = copy.deepcopy(recording)
            expected["geometries"][0]["resource"] = result["resources"][expected["geometries"][0]["resource"]]
            self.assertEqual(json.loads(raw), expected)
            compact_manifest = json.loads((output / "gameplay-skin.json").read_text("utf8"))
            self.assertEqual(compact_manifest["bitmapFonts"][0]["glyphs"][0]["rect"], [0, 0, 1, 1])
            self.assertEqual(compact_manifest["options"][0]["choices"][0]["preview"], result["paths"][image])
            for path, data in files.items():
                self.assertEqual((source / path).read_bytes(), data)
            self.assertTrue(json.loads(receipt.read_text("utf8"))["recordedGeometryTimingAndNumericLexemesUnchanged"])

    def test_repeated_runs_are_byte_identical_and_do_not_merge_equal_image_files(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, manifest, _, _, _ = self.fixture(root)
            duplicate = "original/equal-bytes-different-owner.png"
            (source / duplicate).write_bytes((source / manifest["resources"][0]["path"]).read_bytes())
            manifest["resources"].append({"id": "separate.texture", "type": "texture", "path": duplicate})
            (source / "gameplay-skin.json").write_bytes(encoded(manifest))
            first = compact_package(source, root / "one", root / "one.json")
            second = compact_package(source, root / "two", root / "two.json")
            self.assertEqual(first["resources"], second["resources"])
            self.assertEqual(first["sceneDeclarationIds"], second["sceneDeclarationIds"])
            self.assertNotEqual(first["paths"][duplicate], first["paths"][manifest["resources"][0]["path"]])
            self.assertEqual(first["files"], second["files"])
            for file in first["files"]:
                self.assertEqual((root / "one" / file["output"]).read_bytes(), (root / "two" / file["output"]).read_bytes())

    def test_declaration_shortening_preserves_targets_text_keys_and_all_other_scene_fields(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, manifest, scene, _, files = self.fixture(root)
            # These short IDs deliberately occupy the first generated candidates across
            # preserved node, template, instance, machine and state namespaces.
            scene["root"]["id"] = "cd0"
            scene["templates"][0]["id"] = scene["instances"][0]["template"] = "cd1"
            scene["templates"][0]["root"]["id"] = "cd2"
            scene["instances"][0]["id"] = "cd3"
            scene["stateMachines"][0]["id"] = "cd4"
            scene["stateMachines"][0]["states"][0]["id"] = "cd5"
            track_id = "long.track.declaration.identity"
            binding_id = "long.binding.declaration.identity"
            scene["tracks"] = [{"id": track_id, "target": "source.sprite", "property": "opacity", "clock": "gameplay.time",
                                "keyframes": [{"id": "long.keyframe.first", "time": 0, "value": 0},
                                              {"id": "long.keyframe.last", "time": 400, "value": 1}]}]
            scene["bindings"] = [{"id": binding_id, "target": "source.text", "property": "text", "source": "combo.value"}]
            scene["root"]["children"][1]["properties"]["text"] = binding_id
            scene["variants"][0]["cases"][0]["key"] = track_id
            (source / "gameplay-skin.scene.json").write_bytes(encoded(scene))
            result = compact_package(source, root / "output", root / "receipt.json")
            actual = json.loads((root / "output/gameplay-skin.scene.json").read_text("utf8"))
            identities = result["sceneDeclarationIds"]
            self.assertEqual({item["id"] for item in scene_declarations(scene)}, set(identities))
            self.assertTrue(set(identities.values()).isdisjoint(all_declared_ids([scene, manifest])))
            self.assertTrue(set(identities.values()).isdisjoint(result["resources"].values()))
            self.assertTrue(set(identities.values()).isdisjoint(result["bitmapFonts"].values()))
            self.assertEqual(len(identities), len(set(identities.values())))
            self.assertEqual("source.sprite", actual["tracks"][0]["target"])
            self.assertEqual(binding_id, actual["root"]["children"][1]["properties"]["text"])
            self.assertEqual(track_id, actual["variants"][0]["cases"][0]["key"])
            self.assertEqual(scene["instances"], actual["instances"])
            inverse = lambda mapping: {value: key for key, value in mapping.items()}
            self.assertEqual(scene, rewrite_scene(actual, inverse(result["resources"]), inverse(result["bitmapFonts"]),
                                                 result["animationIdsPreserved"], inverse(identities)))
            self.assertEqual(files["skin.ini"], (root / "output/skin.ini").read_bytes())
            configuration = [p for p in (root / "output").rglob("*") if p.is_file() and p.suffix.lower() in (".ini", ".json")]
            self.assertEqual(sum(p.stat().st_size for p in configuration), result["configurationBytes"]["output"])
            self.assertEqual(16 * 1024 * 1024, result["configurationBytes"]["limit"])
            self.assertGreater(result["configurationBytes"]["remaining"], 0)
            self.assertTrue(result["allOtherSceneFieldsVerifiedByInverseRewrite"])

    def test_unselected_branch_declarations_share_one_deterministic_package_mapping(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, _, _, _, _ = self.fixture(root)
            identity = "same.long.binding.identity.in.exclusive.branches"
            for name in ("branch.json", "other.json"):
                branch = json.loads((source / name).read_text("utf8"))
                branch["bindings"] = [{"id": identity, "target": branch["root"]["id"], "property": "opacity", "source": "gauge.value"}]
                (source / name).write_bytes(encoded(branch))
            result = compact_package(source, root / "output", root / "receipt.json")
            expected = result["sceneDeclarationIds"][identity]
            for name in ("branch.json", "other.json"):
                actual = json.loads((root / "output" / name).read_text("utf8"))
                self.assertEqual(expected, actual["bindings"][0]["id"])
                self.assertEqual(actual["root"]["id"], actual["bindings"][0]["target"])

    def test_dangling_reference_in_an_unselected_variant_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, _, _, _, _ = self.fixture(root)
            variant = json.loads((source / "variant.json").read_text("utf8"))
            variant["root"] = {"id": "variant-root", "type": "sprite", "resource": "missing.texture"}
            (source / "variant.json").write_bytes(encoded(variant))
            with self.assertRaisesRegex(ValueError, "Dangling texture"):
                compact_package(source, root / "output", root / "receipt.json")
            self.assertFalse((root / "receipt.json").exists())

    def test_legacy_material_asset_path_is_protected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, manifest, _, _, _ = self.fixture(root)
            image = manifest["resources"][0]["path"]
            (source / "skin.ini").write_text("[Bms]\nNoteImage0: " + image[:-4] + "\n", "utf8")
            result = compact_package(source, root / "output", root / "receipt.json")
            self.assertEqual(result["paths"][image], image)

    def test_public_provide_quoted_resource_identity_is_protected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, manifest, _, _, _ = self.fixture(root)
            image = manifest["resources"][0]["path"]
            (source / "skin.ini").write_text('[GameplaySkin.Common:1]\nplayfield.backdrop: resource Provide "' + image[:-4] + '"\n', "utf8")
            result = compact_package(source, root / "output", root / "receipt.json")
            self.assertEqual(result["paths"][image], image)

    def test_destination_collision_and_unsafe_receipt_are_rejected_before_writing(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, _, _, _, _ = self.fixture(root)
            (source / "c").mkdir()
            (source / "c/a0.json.br").write_bytes(b"existing unrelated file")
            with self.assertRaisesRegex(ValueError, "file paths collide"):
                compact_package(source, root / "output", root / "receipt.json")
            self.assertFalse((root / "output").exists())
            with self.assertRaisesRegex(ValueError, "separate ownership"):
                compact_package(source, root / "output", root / "output/receipt.json")


if __name__ == "__main__":
    unittest.main()
