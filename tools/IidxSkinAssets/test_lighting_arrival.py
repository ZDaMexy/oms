"""Synthetic arrival author-package checks; no original artwork or game host."""

import copy
import hashlib
import json
from pathlib import Path
import struct
import tempfile
import unittest
import zlib

import lighting_arrival as arrival


def png(width, height, colour):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    pixels = bytes(colour) * width * height
    scanlines = b"".join(b"\0" + pixels[y * width * 4:(y + 1) * width * 4] for y in range(height))
    encoded = (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
               + chunk(b"IDAT", zlib.compress(scanlines)) + chunk(b"IEND", b""))
    return encoded, pixels


def reference():
    textures = []
    for index, (role, (name, width)) in enumerate(arrival._ROLES.items()):
        data, pixels = png(width, 270, (64 + index * 32, 128, 200, 255))
        textures.append(arrival.ArrivalTexture(role, name, "1/gameparts_j.ifs", f"tex/{name}.png", data,
                         hashlib.sha256(data).hexdigest(), hashlib.sha256(pixels).hexdigest(), width, 270))
    return arrival.ArrivalReference("a" * 64, tuple(textures))


def write_reference(directory):
    directory.mkdir()
    (directory / "sprites").mkdir()
    captured = reference()
    textures = []
    for texture in captured.textures:
        relative = f"sprites/{texture.source_id}.png"
        (directory / relative).write_bytes(texture.data)
        textures.append({"id": texture.source_id, "sourcePackage": texture.source_package, "sourceEntry": texture.source_entry,
                         "sprite": relative, "sha256": texture.sha256, "rgbaSha256": texture.rgba_sha256,
                         "width": texture.width, "height": texture.height, "sourceUvPixelsPreserved": True,
                         "sampler": {"mag": "nearest", "min": "nearest", "wrapS": "clamp", "wrapT": "clamp"}})
    document = {"contract": arrival.REFERENCE_CONTRACT, "status": "complete", "originalHddAccessed": False, "textures": textures}
    path = directory / "source-reference.json"
    path.write_text(json.dumps(document), encoding="utf-8")
    return path


def fixture(frame="iidx33", layout="p1"):
    namespace = frame.replace("bpl-", "bpl.", 1)
    root = {"id": f"omni.frame.{frame}.root", "type": "container", "target": {"kind": "global"}, "blend": "inherit",
            "properties": {}, "effects": [], "children": []}
    information = {"id": f"{namespace}.template.{layout}.information", "root": {**copy.deepcopy(root), "id": f"{namespace}.{layout}.information"}}
    existing_beam = {"id": "beam.template", "root": {**copy.deepcopy(root), "id": "beam.root", "slot": "playfield.lane-surface"}}
    scene = {"contract": arrival.SCENE_CONTRACT, "root": root, "templates": [information, existing_beam], "instances": [
                 {"id": "beam.instance", "template": "beam.template", "material": {"slot": "playfield.lane-surface", "resource": "custom/markers/b/white"}}],
             "tracks": [], "bindings": []}
    manifest = {"contract": arrival.MANIFEST_CONTRACT, "resources": []}
    return scene, manifest


def walk(node):
    yield node
    for child in node["children"]:
        yield from walk(child)


class LightingArrivalTest(unittest.TestCase):
    def test_original_png_and_rgba_hashes_are_verified_and_copy_is_byte_exact(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            path = write_reference(base / "source")
            captured = arrival.load_reference(path)
            with self.assertRaises(ValueError):
                arrival.copy_art(captured, path.parent)
            self.assertFalse((path.parent / "art").exists())
            output = base / "author"
            output.mkdir()
            receipt = arrival.copy_art(captured, output)
            for texture in captured.textures:
                self.assertEqual(texture.data, (output / texture.declaration()["path"]).read_bytes())
                self.assertEqual("nearest", texture.declaration()["filter"])
            self.assertTrue(receipt["originalImageAndUvPixelsPreserved"])
            self.assertFalse(receipt["originalHddAccessed"])
            self.assertEqual(receipt, arrival.copy_art(captured, output))

    def test_source_corruption_is_rejected_and_existing_different_art_is_not_overwritten(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            path = write_reference(base / "source")
            captured = arrival.load_reference(path)
            original = base / "source/sprites/start_notes_s.png"
            original.write_bytes(b"changed")
            with self.assertRaises(ValueError):
                arrival.load_reference(path)
            output = base / "author"
            target = output / captured.textures[0].declaration()["path"]
            target.parent.mkdir(parents=True)
            target.write_bytes(b"existing author data")
            with self.assertRaises(ValueError):
                arrival.copy_art(captured, output)
            self.assertEqual(b"existing author data", target.read_bytes())
            self.assertEqual([target], list(output.rglob("*.png")))

    def test_reference_cannot_escape_to_another_source_tree(self):
        with tempfile.TemporaryDirectory() as temp:
            base = Path(temp)
            path = write_reference(base / "source")
            document = json.loads(path.read_text(encoding="utf-8"))
            document["textures"][0]["sprite"] = "../elsewhere.png"
            path.write_text(json.dumps(document), encoding="utf-8")
            with self.assertRaises(ValueError):
                arrival.load_reference(path)

    def test_each_real_lane_material_gets_option_availability_and_before_note_gates(self):
        for frame in arrival.FRAMES:
            for layout in ("p1", "p2", "dp"):
                with self.subTest(frame=frame, layout=layout):
                    scene, manifest = fixture(frame, layout)
                    before = copy.deepcopy(scene)
                    receipt = arrival.inject_frame(scene, manifest, frame_id=frame, reference=reference())
                    self.assertEqual(before["templates"], scene["templates"][:2])
                    self.assertEqual(before["instances"], scene["instances"][:1])
                    self.assertEqual(3, len(manifest["resources"]))
                    for template, instance in zip(scene["templates"][2:], scene["instances"][1:]):
                        root = template["root"]
                        nodes = list(walk(root))
                        self.assertEqual("playfield.lane-surface", root["slot"])
                        self.assertEqual("playfield.lane-surface", instance["material"]["slot"])
                        self.assertTrue(instance["material"]["resource"].startswith("custom/markers/b/"))
                        for gate, source in zip(nodes[:3], ("option.arrival-cue", "arrival.available", "arrival.visible")):
                            self.assertIs(False, gate["properties"]["visible"])
                            self.assertEqual(source, next(binding["source"] for binding in scene["bindings"] if binding["target"] == gate["id"]))
                        art = nodes[-1]
                        self.assertEqual(0, art["properties"]["y"])
                        self.assertEqual(1, art["properties"]["width"])
                        self.assertAlmostEqual(270, art["properties"]["height"] * receipt["placement"]["stageHeightPx"])
                    self.assertEqual(726 if frame in ("iidx27", "iidx28", "iidx29") else 728, receipt["placement"]["stageHeightPx"])
                    namespace = frame.replace("bpl-", "bpl.", 1)
                    self.assertTrue(all(template["root"]["id"].startswith(namespace + ".") for template in scene["templates"][2:]))
                    self.assertEqual(f"omni.frame.{frame}.root", scene["root"]["id"])

    def test_pre_and_post_window_clamp_to_invisible_without_a_loop(self):
        scene, manifest = fixture()
        arrival.inject_frame(scene, manifest, frame_id="iidx33", reference=reference())
        for track in scene["tracks"]:
            self.assertIs(False, track["loop"])
            self.assertEqual("arrival.remaining", track["clock"])
            self.assertEqual((0, 0), (track["keyframes"][0]["time"], track["keyframes"][0]["value"]))
            self.assertEqual((3000, 0), (track["keyframes"][-1]["time"], track["keyframes"][-1]["value"]))
            self.assertTrue(all(frame["value"] == 1 for frame in track["keyframes"][1:-1]))
            self.assertNotIn("trigger", track)

    def test_injection_and_root_option_are_idempotent(self):
        scene, manifest = fixture()
        root_manifest = {"contract": arrival.MANIFEST_CONTRACT}
        arrival.install_option(root_manifest)
        arrival.inject_frame(scene, manifest, frame_id="iidx33", reference=reference())
        before = copy.deepcopy((scene, manifest, root_manifest))
        arrival.install_option(root_manifest)
        arrival.inject_frame(scene, manifest, frame_id="iidx33", reference=reference(), layout="p1")
        self.assertEqual(before, (scene, manifest, root_manifest))
        self.assertEqual([arrival.OPTION], root_manifest["options"])
        self.assertNotIn("options", manifest)

    def test_conflicting_art_or_layout_fails_before_scene_mutation(self):
        scene, manifest = fixture()
        manifest["resources"].append({**reference().textures[0].declaration(), "path": "different.png"})
        before = copy.deepcopy((scene, manifest))
        with self.assertRaises(ValueError):
            arrival.inject_frame(scene, manifest, frame_id="iidx33", reference=reference())
        self.assertEqual(before, (scene, manifest))
        with self.assertRaises(ValueError):
            arrival.inject_frame(scene, manifest, frame_id="iidx33", reference=reference(), layout="p2")
        self.assertEqual(before, (scene, manifest))


if __name__ == "__main__":
    unittest.main()
