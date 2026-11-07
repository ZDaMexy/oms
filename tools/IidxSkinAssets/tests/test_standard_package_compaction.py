"""Reference compaction preserves source pixels, animation numbers and material owners."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import brotli

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_package_compaction import all_declared_ids, compact_package, encoded, rewrite_scene, scene_declarations


class PackageCompactionTest(unittest.TestCase):
    @staticmethod
    def node(identity, kind="container", **fields):
        return {"id": identity, "type": kind, "target": {"kind": "global"}, "blend": "inherit",
                "properties": {}, "effects": [], "children": [], **fields}

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
                        *[{"character": str(digit), "resource": texture, "advance": 1} for digit in range(1, 10)],
                        {"character": "-", "resource": "original.marker", "advance": 1}]}],
                    "animations": [{"id": animation, "path": movie, "resources": [texture]}],
                    "options": [{"id": "example", "label": "Example", "type": "choice", "default": "a", "choices": [{"value": "a", "label": "A", "preview": image}]}],
                    "contentCatalog": "content.json"}
        scene = {"contract": "oms-gameplay-skin-scene.v1",
                 "root": self.node("root", children=[
                     self.node("source.sprite", "sprite", resource=texture),
                     self.node("source.text", "text", properties={"bitmap-font": font, "text": "0", "format": "integer"}),
                     self.node("source.movie", "recorded-animation", animation=animation),
                     self.node("source.caption", "text", properties={"text": texture})]),
                 "templates": [{"id": "owner.template", "root": self.node("owner", "sprite", resource="original.marker")}],
                 "instances": [{"id": "owner.instance", "template": "owner.template", "material": {"slot": "decoration", "resource": marker}}],
                 "tracks": [], "bindings": [],
                 "variants": [{"id": "variant", "target": "source.sprite", "property": "resource", "source": "option.example",
                               "default": texture, "cases": [{"id": "case", "key": "a", "resource": texture}]}],
                 "stateMachines": [{"id": "machine", "initial": "state", "states": [{"id": "state", "set": [
                     {"id": "state.assignment", "target": "source.sprite", "property": "resource", "value": texture}]}],
                                    "transitions": [{"id": "transition", "from": "state", "to": "state", "event": "judgement.hit"}]}]}
        branch = {"contract": "oms-gameplay-skin-scene.v1", "root": self.node("branch"),
                  "templates": [], "instances": [], "tracks": [], "bindings": [], "variants": [], "stateMachines": []}
        branch_manifest = {key: manifest[key] for key in ("contract", "scene", "sceneContract", "eventContract")}
        branch_manifest["resources"] = []
        catalogue = {"contract": "oms-gameplay-skin-content.v1", "modules": [{"id": "style", "label": "Style", "default": "a", "choices": [
            {"value": "a", "label": "A", "manifest": "branch-manifest.json", "scene": "branch.json", "preview": image},
            {"value": "b", "label": "B", "manifest": "branch-manifest.json", "scene": "other.json", "variants": [
                {"when": {"example": "a"}, "manifest": "branch-manifest.json", "scene": "variant.json"}]}]}]}
        recording = {"contract": "oms-gameplay-recorded-animation.v1", "width": 2, "height": 2, "fps": 60,
                     "loop": False, "trigger": "object.long-note-transition",
                     "geometries": [{"resource": texture, "vertices": [[0.125, 1, 0, 0.5, 1], [1, 0, 0, 0, 0], [0, 0, 0, 1, 1]], "triangles": [0, 1, 2]},
                                    {"resource": None, "colour": [0, 0, 0, 1], "vertices": [], "triangles": []}],
                     "nodes": [], "states": [], "frames": [[], []]}
        raw_recording = encoded(recording).replace(b"0.125", b"0.12500000000000000")
        files = {"gameplay-skin.json": encoded(manifest), "gameplay-skin.scene.json": encoded(scene),
                 "branch-manifest.json": encoded(branch_manifest),
                 "branch.json": encoded(branch), "other.json": encoded({**branch, "root": self.node("other")}),
                 "variant.json": encoded({**branch, "root": self.node("variant-root")}), "content.json": encoded(catalogue),
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
            self.assertEqual(changed["root"]["children"][3]["properties"]["text"], scene["root"]["children"][3]["properties"]["text"])
            self.assertEqual(changed["root"]["children"][2]["animation"], scene["root"]["children"][2]["animation"])
            self.assertEqual(changed["instances"][0]["material"], scene["instances"][0]["material"])
            self.assertEqual(changed["templates"][0]["id"], changed["instances"][0]["template"])
            assignment = changed["stateMachines"][0]["states"][0]["set"][0]
            self.assertEqual(assignment["target"], changed["root"]["children"][0]["id"])
            self.assertEqual(assignment["value"], changed["root"]["children"][0]["resource"])
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
            self.assertEqual(first["sceneIdentityIds"], second["sceneIdentityIds"])
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
            scene["stateMachines"][0]["states"][0]["id"] = scene["stateMachines"][0]["initial"] = "cd5"
            scene["stateMachines"][0]["transitions"][0].update({"from": "cd5", "to": "cd5"})
            track_id = "long.track.declaration.identity"
            binding_id = "long.binding.declaration.identity"
            scene["tracks"] = [{"id": track_id, "type": "tween", "target": "source.sprite", "property": "opacity", "easing": "linear", "loop": False,
                                "keyframes": [{"id": "long.keyframe.first", "time": 0, "value": 0},
                                              {"id": "long.keyframe.last", "time": 400, "value": 1}]}]
            scene["bindings"] = [{"id": binding_id, "target": "source.text", "property": "text", "source": "combo.value"}]
            scene["root"]["children"][3]["properties"]["text"] = binding_id
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
            self.assertEqual(result["sceneIdentityIds"]["source.sprite"], actual["tracks"][0]["target"])
            self.assertEqual(binding_id, actual["root"]["children"][3]["properties"]["text"])
            self.assertEqual(track_id, actual["variants"][0]["cases"][0]["key"])
            self.assertEqual(scene["instances"][0]["material"], actual["instances"][0]["material"])
            inverse = lambda mapping: {value: key for key, value in mapping.items()}
            self.assertEqual(scene, rewrite_scene(actual, inverse(result["resources"]), inverse(result["bitmapFonts"]),
                                                 result["animationIdsPreserved"], inverse(identities), inverse(result["sceneIdentityIds"])))
            self.assertEqual(files["skin.ini"], (root / "output/skin.ini").read_bytes())
            configuration = [p for p in (root / "output").rglob("*") if p.is_file() and p.suffix.lower() in (".ini", ".json")]
            self.assertEqual(sum(p.stat().st_size for p in configuration), result["configurationBytes"]["output"])
            self.assertEqual(32 * 1024 * 1024, result["configurationBytes"]["limit"])
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

    def test_scene_identity_references_preserve_capture_semantics_and_other_namespaces(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, _, scene, _, _ = self.fixture(root)
            observed = ["frame.reading.ready.art", "frame.reading.green", "frame.reading.normal.page0.row00",
                        "frame.arrival.first", "graph.best.difference", "graph.current.value", "graph.cutin.aaa"]
            scene["root"]["children"].extend(self.node(identity) for identity in observed)
            # A physical C3 target and a text literal are not source-node references,
            # even if their strings happen to equal a renamed node identity.
            scene["root"]["children"][0]["target"] = {"kind": "lane", "id": "source.sprite", "index": 0}
            scene["root"]["children"][3]["properties"]["text"] = "source.sprite"
            scene["root"]["children"].append(self.node("cz0"))
            scene["stateMachines"][0]["id"] = "cz1"
            scene["numericSources"] = [{"id": "native.number", "operation": "add", "left": 1, "right": 0}]
            scene["root"]["children"][2]["scoreCrossing"] = {"thresholds": [
                {"source": "value.native.number", "comparison": "greater"}]}
            scene["bindings"] = [{"id": "binding.source", "target": "source.text", "property": "text", "source": "combo.value"}]
            scene["tracks"] = [{"id": "track.source", "type": "tween", "target": "source.sprite", "property": "opacity",
                                "clock": "value.native.number", "loop": False, "easing": "step",
                                "keyframes": [{"id": "frame.source", "time": 0, "value": 1}]}]
            (source / "gameplay-skin.scene.json").write_bytes(encoded(scene))
            result = compact_package(source, root / "output", root / "receipt.json")
            actual = json.loads((root / "output/gameplay-skin.scene.json").read_bytes())
            identities = result["sceneIdentityIds"]
            self.assertEqual(observed, [identities[identity] for identity in observed])
            self.assertEqual(set(observed), set(result["preservedObservedNodeIds"]))
            self.assertNotEqual("source.sprite", identities["source.sprite"])
            self.assertNotIn("cz0", {value for key, value in identities.items() if key != value})
            self.assertNotIn("cz1", identities.values())
            self.assertEqual(scene["root"]["children"][0]["target"], actual["root"]["children"][0]["target"])
            self.assertEqual("source.sprite", actual["root"]["children"][3]["properties"]["text"])
            for field in ("animation", "scoreCrossing"):
                self.assertEqual(scene["root"]["children"][2][field], actual["root"]["children"][2][field])
            self.assertEqual(scene["numericSources"], actual["numericSources"])
            self.assertEqual(scene["tracks"][0]["clock"], actual["tracks"][0]["clock"])
            for field in ("id", "initial", "transitions"):
                self.assertEqual(scene["stateMachines"][0][field], actual["stateMachines"][0][field])
            targets = [actual["variants"][0]["target"], actual["tracks"][0]["target"],
                       actual["stateMachines"][0]["states"][0]["set"][0]["target"]]
            self.assertEqual([identities["source.sprite"]] * 3, targets)
            self.assertEqual(identities["source.text"], actual["bindings"][0]["target"])
            self.assertEqual(identities["owner.template"], actual["templates"][0]["id"])
            self.assertEqual(identities["owner.template"], actual["instances"][0]["template"])
            self.assertEqual(identities["owner.instance"], actual["instances"][0]["id"])

    def test_exact_catalog_configurations_share_files_and_budget_counts_unique_outputs(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            source, _, _, _, files = self.fixture(root)
            copies = {"duplicate-manifest.json": files["branch-manifest.json"],
                      "copy.scene.json": files["branch.json"],
                      "choice-a.ini": files["skin.ini"], "choice-b.ini": files["skin.ini"],
                      "choice-near.ini": files["skin.ini"] + b"\n",
                      "unreferenced.ini": files["skin.ini"]}
            for name, content in copies.items():
                (source / name).write_bytes(content)
            catalogue = json.loads(files["content.json"])
            choices = catalogue["modules"][0]["choices"]
            choices[0]["ini"] = "choice-a.ini"
            choices[1].update(manifest="duplicate-manifest.json", scene="copy.scene.json", ini="choice-b.ini")
            choices[1]["variants"][0]["ini"] = "choice-near.ini"
            (source / "content.json").write_bytes(encoded(catalogue))
            result = compact_package(source, root / "output", root / "receipt.json")
            self.assertEqual({"duplicate-manifest.json": "branch-manifest.json", "copy.scene.json": "branch.json",
                              "choice-b.ini": "choice-a.ini"}, result["configurationAliases"])
            for name in result["configurationAliases"]:
                self.assertFalse((root / "output" / name).exists())
            for name in ("skin.ini", "choice-a.ini", "choice-near.ini", "unreferenced.ini"):
                self.assertEqual((source / name).read_bytes(), (root / "output" / name).read_bytes())
            compact = json.loads((root / "output/content.json").read_bytes())
            self.assertEqual(compact["modules"][0]["choices"][0]["scene"], compact["modules"][0]["choices"][1]["scene"])
            self.assertEqual("gameplay-skin.scene.json", json.loads((root / "output/branch-manifest.json").read_bytes())["scene"])
            for edit in reversed(result["catalogReferenceEdits"]["content.json"]):
                parent = compact
                for segment in edit["path"][:-1]:
                    parent = parent[segment]
                self.assertEqual(edit["output"], parent[edit["path"][-1]])
                parent[edit["path"][-1]] = edit["source"]
            self.assertEqual(catalogue, compact)
            count = result["configurationBytes"]["output"]
            self.assertEqual(count, sum(path.stat().st_size for path in (root / "output").rglob("*")
                                        if path.is_file() and path.suffix in (".json", ".ini")))
            self.assertGreater(sum(item["outputBytes"] for item in result["files"]
                                   if Path(item["output"]).suffix in (".json", ".ini")), count)
            with patch("standard_package_compaction.MAX_CONFIGURATION_BYTES", count):
                exact = compact_package(source, root / "exact", root / "exact.json")
                self.assertEqual(0, exact["configurationBytes"]["remaining"])
            with patch("standard_package_compaction.MAX_CONFIGURATION_BYTES", count - 1):
                with self.assertRaisesRegex(ValueError, "configuration exceeds"):
                    compact_package(source, root / "oversize", root / "oversize.json")
            self.assertFalse((root / "oversize.json").exists())

    def test_scene_references_cannot_borrow_an_exclusive_branch_or_missing_template(self):
        for reference in ("node", "template"):
            with self.subTest(reference=reference), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                source, _, _, _, _ = self.fixture(root)
                scene = json.loads((source / "variant.json").read_bytes())
                if reference == "node":
                    scene["bindings"] = [{"id": "cross.branch", "target": "other", "property": "opacity", "source": "gauge.value"}]
                else:
                    scene["instances"] = [{"id": "missing.instance", "template": "missing.template", "target": {"kind": "global"}}]
                (source / "variant.json").write_bytes(encoded(scene))
                with self.assertRaisesRegex(ValueError, "Dangling scene " + reference):
                    compact_package(source, root / "output", root / "receipt.json")
                self.assertFalse((root / "output").exists())

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
