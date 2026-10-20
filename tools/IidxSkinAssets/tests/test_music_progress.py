"""Progress authoring checks against independent 1080p endpoint fixtures; no game media needed."""

import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from music_progress import RESOURCE_ID, RESOURCE_PATH, apply_to_author, inject, load_reference, prepare_resource


FRAMES = ("iidx27", "iidx28", "iidx29", "iidx30", "iidx31", "iidx32", "iidx33", "bpl-apina", "bpl-gamepanic",
          "bpl-gigo", "bpl-leisureland", "bpl-round1", "bpl-silkhat", "bpl-supernova-hd", "bpl-tradz")


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)


class MusicProgressTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="oms-music-progress-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.reference_path = self.root / "reference/source-geometry.json"
        self.reference_path.parent.mkdir()
        self.png = self.reference_path.parent / "original.png"
        image = Image.new("RGBA", (22, 30))
        for y in range(30):
            for x in range(22):
                symmetric_x = min(x, 21 - x)
                image.putpixel((x, y), (symmetric_x * 20, y * 8, 123, 0 if y == 0 else 255))
        image.save(self.png)
        self.reference_data = {"contract": "oms-iidx-music-progress-reference.v1", "texture": {
            "path": "original.png", "width": 22, "height": 30, "filter": "nearest",
            "sha256": hashlib.sha256(self.png.read_bytes()).hexdigest()}, "frames": {}}
        for frame_index, frame in enumerate(FRAMES):
            layouts = {}
            for layout in ("p1", "p2", "dp"):
                width, height = (33, 45) if frame_index < 3 else (22, 30)
                if frame in ("iidx27", "iidx31", "iidx32") and layout == "dp":
                    width = 21.763671875
                positions = (456.25, 1438.5) if layout == "dp" else (37.5,) if layout == "p1" else (1840.25,)
                layouts[layout] = {"canvas": [1920, 1080], "cursors": [{
                    # Original instance names are deliberately identical on the two DP sides.
                    "id": "same-source-cursor", "start": [x, 45.25, width, height], "end": [x, 700.75, width, height],
                    "flipX": layout == "p2" or layout == "dp" and occurrence == 1,
                    "evidence": {"occurrence": occurrence, "method": "independent-endpoint-test-fixture", "sourceBlend": 8,
                                 "sourceMultiply": {"r": 1, "g": 1, "b": 1, "a": 1}, "sourceAdd": {"r": 0, "g": 0, "b": 0, "a": 0}},
                } for occurrence, x in enumerate(positions)]}
            self.reference_data["frames"][frame] = layouts
        self.save_reference()

    def save_reference(self):
        self.reference_path.write_text(json.dumps(self.reference_data), encoding="utf-8")

    def reference(self):
        return load_reference(self.reference_path, expected_frames=FRAMES)

    def test_changed_source_blend_or_colour_cannot_silently_use_the_original_rendering(self):
        original = copy.deepcopy(self.reference_data)
        for field, value in (("sourceBlend", 0), ("sourceMultiply", {"r": 0, "g": 1, "b": 1, "a": 1}),
                             ("sourceAdd", {"r": 1, "g": 0, "b": 0, "a": 0})):
            with self.subTest(field=field):
                self.reference_data = copy.deepcopy(original)
                self.reference_data["frames"]["iidx33"]["dp"]["cursors"][1]["evidence"][field] = value
                self.save_reference()
                with self.assertRaisesRegex(ValueError, "untinted additive"):
                    self.reference()

    def scene(self, frame, layout):
        owner = {"id": frame + "." + layout + ".information", "type": "container", "slot": "decoration",
                 "target": {"kind": "global"}, "blend": "inherit", "properties": {}, "effects": [],
                 "children": [{"id": frame + "." + layout + ".existing", "type": "text", "target": {"kind": "global"},
                               "blend": "inherit", "properties": {"text": "Existing source HUD"}, "effects": [], "children": []}]}
        document = {"contract": "oms-gameplay-skin-scene.v1", "root": {
            "id": "omni.frame." + frame + ".root", "type": "container", "target": {"kind": "global"},
            "blend": "inherit", "properties": {}, "effects": [], "children": []},
            "templates": [{"id": frame + ".template." + layout + ".information", "root": owner}],
            "instances": [{"id": "existing-instance", "template": frame + ".template." + layout + ".information",
                           "material": {"slot": "decoration", "resource": "markers/frame-" + layout + "-information"}}],
            "bindings": [{"id": "existing-binding", "target": owner["children"][0]["id"], "property": "text", "source": "score.value"}],
            "tracks": [], "variants": [], "stateMachines": []}
        manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                    "resources": [{"id": "original.texture", "type": "texture", "path": "original-art.png"}]}
        return document, manifest

    def test_all_45_layouts_reach_original_start_middle_end_without_resizing_or_changing_old_hud(self):
        reference = self.reference()
        cursors_checked = 0
        for frame in FRAMES:
            for layout in ("p1", "p2", "dp"):
                with self.subTest(frame=frame, layout=layout):
                    document, manifest = self.scene(frame, layout)
                    original_document, original_manifest = copy.deepcopy(document), copy.deepcopy(manifest)
                    receipt = inject(document, manifest, reference.frames[frame])
                    expected = self.reference_data["frames"][frame][layout]["cursors"]
                    owner = document["templates"][0]["root"]
                    self.assertEqual(len(expected), len(receipt["cursors"]))
                    self.assertEqual(original_document["bindings"], document["bindings"][:1])
                    self.assertEqual(original_document["instances"], document["instances"])
                    self.assertEqual(original_document["templates"][0]["root"]["children"], owner["children"][:1])
                    self.assertEqual(original_manifest["resources"], manifest["resources"][:1])
                    for index, source in enumerate(expected):
                        track = owner["children"][index + 1]
                        sprite = track["children"][0]
                        self.assertEqual(owner["id"] + f".music-progress.o{index}.track", track["id"])
                        self.assertEqual(owner["id"] + f".music-progress.o{index}.cursor", sprite["id"])
                        self.assertEqual(RESOURCE_ID, sprite["resource"])
                        self.assertEqual("additive", sprite["blend"])
                        self.assertEqual({"id": owner["id"] + f".music-progress.o{index}.progress", "target": sprite["id"],
                                          "property": "y", "source": "timing.progress"}, document["bindings"][index + 1])
                        generated_ids = [track["id"], sprite["id"], sprite["resource"],
                                         document["bindings"][index + 1]["id"], document["bindings"][index + 1]["target"],
                                         manifest["resources"][-1]["id"]]
                        for identifier in generated_ids:
                            self.assertLessEqual(len(identifier), 128)
                            for segment in identifier.split("."):
                                self.assertRegex(segment, r"^[a-z][a-z0-9-]*$")
                                self.assertIn(segment[-1], "abcdefghijklmnopqrstuvwxyz0123456789")
                        for progress in (0, 0.5, 1):
                            # Roots use the shared full-global rect. A child Transform's relative Y is
                            # measured against its Root filling the parent's travel-height Transform.
                            position = [track["properties"]["x"] * 1920,
                                        (track["properties"]["y"] + progress * track["properties"]["height"]) * 1080,
                                        track["properties"]["width"] * sprite["properties"]["width"] * 1920,
                                        track["properties"]["height"] * sprite["properties"]["height"] * 1080]
                            for actual, start, end in zip(position, source["start"], source["end"]):
                                self.assertAlmostEqual(start + progress * (end - start), actual, delta=1e-8)
                        self.assertFalse(any(key.startswith("scale-") and value < 0 for node in walk(track)
                                             for key, value in node["properties"].items()))
                        cursors_checked += 1
                    before = copy.deepcopy((document, manifest))
                    second = inject(document, manifest, reference.frames[frame])
                    self.assertEqual(before, (document, manifest))
                    self.assertEqual(0, second["addedCursors"])
                    self.assertFalse(second["addedResource"])
        self.assertEqual(60, cursors_checked)

    def test_dp_retains_two_same_named_occurrences_with_distinct_runtime_ids_and_source_flip(self):
        reference = self.reference()
        document, manifest = self.scene("iidx33", "dp")
        receipt = inject(document, manifest, reference.frames["iidx33"])
        self.assertEqual(["same-source-cursor", "same-source-cursor"], [row["sourceId"] for row in receipt["cursors"]])
        self.assertEqual(2, len({row["cursor"] for row in receipt["cursors"]}))
        self.assertEqual([False, True], [row["flipX"] for row in receipt["cursors"]])
        self.assertEqual([False, True], [row["flipPreservedByPixelSymmetry"] for row in receipt["cursors"]])

    def test_invalid_generated_id_prefixes_are_rejected_before_any_mutation(self):
        reference = self.reference()
        for identifier in ("iidx33.0.information", "iidx33.P1.information", "iidx33..information", "iidx33.p1.information-",
                           "iidx33.p1.informati\u00f6n", "iidx33." + "a" * 128):
            with self.subTest(identifier=identifier):
                document, manifest = self.scene("iidx33", "p1")
                document["templates"][0]["root"]["id"] = identifier
                before = copy.deepcopy((document, manifest))
                with self.assertRaisesRegex(ValueError, "public stable ID"):
                    inject(document, manifest, reference.frames["iidx33"])
                self.assertEqual(before, (document, manifest))

    def test_wrong_frame_id_or_non_unique_or_transformed_information_owner_is_rejected_without_mutation(self):
        reference = self.reference()
        for change in ("frame", "missing", "multiple", "transformed"):
            with self.subTest(change=change):
                document, manifest = self.scene("iidx33", "p1")
                if change == "frame":
                    document["root"]["id"] = "omni.frame.iidx32.root"
                elif change == "missing":
                    document["templates"] = []
                elif change == "multiple":
                    document["templates"].append(self.scene("iidx33", "p2")[0]["templates"][0])
                else:
                    document["templates"][0]["root"]["properties"]["scale-x"] = 0.5
                before = copy.deepcopy((document, manifest))
                with self.assertRaises(ValueError):
                    inject(document, manifest, reference.frames["iidx33"])
                self.assertEqual(before, (document, manifest))

    def test_changed_or_incomplete_owned_nodes_and_conflicting_resource_are_rejected(self):
        reference = self.reference()
        for change in ("geometry", "binding", "resource"):
            with self.subTest(change=change):
                document, manifest = self.scene("iidx33", "p1")
                inject(document, manifest, reference.frames["iidx33"])
                if change == "geometry":
                    document["templates"][0]["root"]["children"][1]["properties"]["height"] = 0.3
                elif change == "binding":
                    document["bindings"].pop()
                else:
                    manifest["resources"][-1]["path"] = "other.png"
                before = copy.deepcopy((document, manifest))
                with self.assertRaises(ValueError):
                    inject(document, manifest, reference.frames["iidx33"])
                self.assertEqual(before, (document, manifest))

    def test_incomplete_frames_layouts_and_unrepresentable_geometry_are_rejected(self):
        original = copy.deepcopy(self.reference_data)
        for change in ("frame", "layout", "missing-dp", "horizontal", "size", "reverse", "infinite", "outside", "evidence", "flip"):
            with self.subTest(change=change):
                self.reference_data = copy.deepcopy(original)
                cursor = self.reference_data["frames"]["iidx33"]["p1"]["cursors"][0]
                if change == "frame":
                    self.reference_data["frames"]["iidx34"] = self.reference_data["frames"].pop("iidx33")
                elif change == "layout":
                    del self.reference_data["frames"]["iidx33"]["p2"]
                elif change == "missing-dp":
                    self.reference_data["frames"]["iidx33"]["dp"]["cursors"].pop()
                elif change == "horizontal":
                    cursor["end"][0] += 1
                elif change == "size":
                    cursor["end"][2] += 1
                elif change == "reverse":
                    cursor["end"][1] = cursor["start"][1]
                elif change == "infinite":
                    cursor["end"][1] = float("inf")
                elif change == "outside":
                    cursor["end"][1] = 1080
                elif change == "evidence":
                    del cursor["evidence"]
                else:
                    cursor["flipX"] = -1
                self.save_reference()
                with self.assertRaises(ValueError):
                    self.reference()

    def test_png_hash_header_source_change_and_unsupported_asymmetric_flip_are_rejected(self):
        original_png, original_reference = self.png.read_bytes(), copy.deepcopy(self.reference_data)
        for change in ("hash", "header", "asymmetric"):
            with self.subTest(change=change):
                self.reference_data = copy.deepcopy(original_reference)
                self.png.write_bytes(original_png)
                image = Image.new("RGBA", (23, 30) if change == "header" else (22, 30), "black")
                image.putpixel((0, 0), (255, 255, 255, 255))
                image.save(self.png)
                if change != "hash":
                    self.reference_data["texture"]["sha256"] = hashlib.sha256(self.png.read_bytes()).hexdigest()
                self.save_reference()
                with self.assertRaises(ValueError):
                    self.reference()
        self.png.write_bytes(original_png)
        self.reference_data = original_reference
        self.save_reference()
        reference = self.reference()
        self.png.write_bytes(original_png + b"changed-after-validation")
        output = self.root / "new-author"
        output.mkdir()
        with self.assertRaisesRegex(ValueError, "changed after"):
            prepare_resource(output, reference)
        self.assertFalse((output / RESOURCE_PATH).exists())

    def test_reference_texture_path_cannot_escape_reference_directory(self):
        self.reference_data["texture"]["path"] = "../elsewhere.png"
        self.save_reference()
        with self.assertRaisesRegex(ValueError, "inside"):
            self.reference()

    def prepare_author(self):
        output = self.root / "author"
        (output / "config").mkdir(parents=True)
        (output / "gameplay").mkdir()
        choices = []
        for frame in FRAMES:
            parts = {}
            for layout in ("dp", "p1", "p2"):
                scene, manifest = self.scene(frame, layout)
                scene_path, manifest_path = f"config/{frame}-{layout}-scene.json", f"config/{frame}-{layout}-manifest.json"
                (output / scene_path).write_text(json.dumps(scene), encoding="utf-8")
                (output / manifest_path).write_text(json.dumps(manifest), encoding="utf-8")
                parts[layout] = {"manifest": manifest_path, "scene": scene_path}
            choices.append({"value": frame, **parts["dp"], "variants": [
                {"when": {"layout": layout}, **parts[layout]} for layout in ("p1", "p2")]})
        (output / "gameplay/content.json").write_text(json.dumps({"contract": "oms-gameplay-skin-content.v1", "modules": [
            {"id": "frame", "choices": choices}, {"id": "untouched-custom", "choices": [{"value": "original"}]}]}), encoding="utf-8")
        (output / "gameplay-skin.json").write_text(json.dumps({"contract": "oms-gameplay-skin-manifest.v1",
            "scene": "gameplay-skin.scene.json", "contentCatalog": "gameplay/content.json"}), encoding="utf-8")
        (output / "untouched-custom.bin").write_bytes(b"original custom source bytes")
        return output

    def snapshot(self, output):
        return {path.relative_to(output).as_posix(): path.read_bytes() for path in output.rglob("*") if path.is_file()}

    def test_apply_updates_all_45_pairs_only_and_copies_one_exact_original_png_idempotently(self):
        output, reference = self.prepare_author(), self.reference()
        before = self.snapshot(output)
        receipt = apply_to_author(output, reference)
        after = self.snapshot(output)
        self.assertEqual(15, receipt["frameCount"])
        self.assertEqual(45, len(receipt["contributions"]))
        self.assertEqual(60, sum(len(item["cursors"]) for item in receipt["contributions"]))
        self.assertEqual(90, len(receipt["changedJson"]))
        self.assertEqual(45, receipt["changedScenes"])
        self.assertEqual(45, receipt["changedManifests"])
        self.assertEqual({RESOURCE_PATH}, set(after).difference(before))
        self.assertEqual(self.png.read_bytes(), after[RESOURCE_PATH])
        for path, data in before.items():
            if path not in receipt["changedJson"]:
                self.assertEqual(data, after[path])
        second = apply_to_author(output, reference)
        self.assertEqual(after, self.snapshot(output))
        self.assertEqual([], second["changedJson"])
        self.assertFalse(second["texture"]["created"])

    def test_one_bad_last_frame_is_rejected_before_any_author_file_or_asset_is_written(self):
        output, reference = self.prepare_author(), self.reference()
        path = output / f"config/{FRAMES[-1]}-p2-scene.json"
        invalid = json.loads(path.read_text(encoding="utf-8"))
        invalid["templates"][0]["root"]["slot"] = "hud.text"
        path.write_text(json.dumps(invalid), encoding="utf-8")
        before = self.snapshot(output)
        with self.assertRaises(ValueError):
            apply_to_author(output, reference)
        self.assertEqual(before, self.snapshot(output))

    def test_existing_destination_png_is_never_overwritten(self):
        output, reference = self.prepare_author(), self.reference()
        destination = output / RESOURCE_PATH
        destination.parent.mkdir(parents=True)
        destination.write_bytes(b"foreign PNG")
        before = self.snapshot(output)
        with self.assertRaisesRegex(ValueError, "destination conflicts"):
            apply_to_author(output, reference)
        self.assertEqual(before, self.snapshot(output))


if __name__ == "__main__":
    unittest.main()
