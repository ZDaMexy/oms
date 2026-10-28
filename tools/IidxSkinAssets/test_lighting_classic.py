"""Classic original-art upgrade contracts using only synthetic private inputs."""
from __future__ import annotations

import copy
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from PIL import Image, PngImagePlugin

import lighting_gameplay_controls as controls
import test_lighting_gameplay_controls as presentation_tests
from build_lighting_classic import REFERENCE_FILES, append_classic_resources, classic_source_inputs, copy_classic_resources


def legacy_fixture(*, compact=False):
    captured = {}
    def retain_legacy(scene, reference, mapping):
        captured.update(scene=copy.deepcopy(scene), reference=copy.deepcopy(reference), mapping=copy.deepcopy(mapping))
        return scene, None
    with mock.patch.object(controls, "upgrade_classic_presentation", side_effect=retain_legacy):
        presentation_tests.GameplayControlPresentationTest().fixture()
    # Capture the factory before its one upgrade call, rather than feeding an already upgraded scene back into it.
    assertions = unittest.TestCase()
    by_id = index(captured["scene"])
    for layout in ("p1", "p2", "dp"):
        prefix = "gc." + layout
        classic = by_id[prefix + ".classic"]
        assertions.assertEqual([item["properties"]["text"] for item in classic["children"] if item["type"] == "text"],
                               ["10", "9", "8", "7", "6", "5", "4", "3", "2", "1", "0.5"])
        cursor = by_id[prefix + ".classic.slider"]
        assertions.assertEqual(cursor["resource"], captured["mapping"]["playm_spd_sel"])
        assertions.assertAlmostEqual(cursor["properties"]["width"] * 1920, 130, delta=1e-4)
        assertions.assertEqual([item["id"] for item in by_id[prefix + ".nohid"]["children"]],
                               [prefix + ".gnl", prefix + ".gn"])
    assertions.assertFalse(any(identifier.startswith("cl.") for identifier in by_id))
    if compact:
        captured["scene"] = controls.compact(captured["scene"], "z")
    return captured["scene"], captured["reference"], captured["mapping"]


def index(scene):
    return {item["id"]: item for item in controls._scene_nodes(scene)}


def sample(track, value):
    keys = track["keyframes"]
    if value <= keys[0]["time"]: return keys[0]["value"]
    if value >= keys[-1]["time"]: return keys[-1]["value"]
    for left, right in zip(keys, keys[1:]):
        if left["time"] <= value <= right["time"]:
            return left["value"] + (right["value"] - left["value"]) * (value - left["time"]) / (right["time"] - left["time"])
    raise AssertionError("Unbounded sample")


def source_receipt_fixture(root):
    library, receipts = root / "library", root / "receipts"
    library.mkdir(parents=True); receipts.mkdir()
    _, reference, _ = legacy_fixture()
    reference.update(sourceMovies=[], movies={})
    textures, pictures = [], {}

    def picture(name, size):
        relative = "sprites/" + name + ".png"
        path = library / relative
        path.parent.mkdir(exist_ok=True)
        Image.new("RGBA", size, (12, 34, 56, 0)).save(path, format="PNG")
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        textures.append({"package": controls.PACKAGE, "name": name + ".png", "sprite": relative,
                         "sha256": digest, "width": size[0], "height": size[1], "sampler": {"min": "nearest", "mag": "nearest"}})
        pictures[name] = {"path": str(path), "sha256": digest, "size": list(size)}

    for name, size in (("tx_num1_4", (90, 394)), ("playm_classic_spd_sel", (150, 70)),
                       ("playm_spd_sel", (130, 70)), ("tx_notes_time", (84, 12)),
                       ("spd_classic_window", (152, 442)), ("spd_classic_window_dp", (128, 498)),
                       ("tx_classic_hispeed", (18, 304))):
        picture(name, size)
    for digit in range(10):
        for suffix in ("", "g"):
            picture(f"notes_time_num{digit}{suffix}", (22, 20))
    geometry = library / "geometry.json"
    geometry.write_text('{"fixture":"source-geometry"}', encoding="utf-8")
    geometry_digest = hashlib.sha256(geometry.read_bytes()).hexdigest()
    geometry_receipt = {"path": str(geometry), "sha256": geometry_digest}
    layouts, labels = [], []
    for layout in ("p1", "p2", "dp"):
        shapes = reference["panels"]["classic"][layout]["shapes"]
        source = {item["region"]: item for item in shapes}
        cx, first = {"p1": (600, 331), "p2": (1326, 331), "dp": (960, 109)}[layout]
        layouts.append({"layout": layout, "strip": copy.deepcopy(source["tx_num1_4"]),
                        "cursor": copy.deepcopy(source["playm_classic_spd_sel"]),
                        "wholeStripOriginalGeo": {"parsed": str(geometry), "parsedSha256": geometry_digest},
                        "cursorGeo": {"parsed": str(geometry), "parsedSha256": geometry_digest},
                        "rowAnchors": [{"value": value, "sourceAlignedCursorCenter": [cx, first + 37 * index]}
                                       for index, value in enumerate((4, 3.75, 3.5, 3.25, 3, 2.75, 2.5, 2.25, 2, 1.5, 1))]})
        labels.append({"layout": layout, "sourceLabel": copy.deepcopy(source["tx_notes_time"]),
                       "sourcePng": {key: pictures["tx_notes_time"][key] for key in ("path", "sha256")},
                       "sourceGeo": copy.deepcopy(geometry_receipt)})
    strip = pictures["tx_num1_4"]
    art = {"originalStrip": {"spritePng": strip["path"], "spriteSha256": strip["sha256"], "spriteSize": strip["size"]},
           "authorReuse": {"selectors": [pictures[name] for name in ("playm_classic_spd_sel", "playm_spd_sel")]},
           "originalLayoutGeometry": layouts}
    supplement = {"correctOriginalStaticComponents": [{"sourcePng": pictures[name], "sourceGeo": copy.deepcopy(geometry_receipt)}
                  for name in ("spd_classic_window", "spd_classic_window_dp", "tx_classic_hispeed")],
                  "notesTimeLabelAndCarrier": labels}
    documents = {library / "library.json": {"textures": textures},
                 library / "author-data/contents/data/graphic/1/gameparts/package.json": {"movies": [], "geometry": []},
                 receipts / REFERENCE_FILES[0]: art, receipts / REFERENCE_FILES[1]: supplement}
    for path, value in documents.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")
    return library, receipts, reference


class ClassicOriginalPresentationTest(unittest.TestCase):
    def test_all_source_clock_anchors_keep_real_track_and_keyframe_identities_with_plain_and_compact_inputs(self):
        for compact in (False, True):
            parent, reference, mapping = legacy_fixture(compact=compact)
            scene, receipt = controls.upgrade_classic_presentation(parent, reference, mapping)
            self.assertEqual(controls.reverse_classic_presentation(scene, receipt), parent)
            for change in receipt["layouts"]:
                layout = change["layout"]
                cx, first = {"p1": (600, 331), "p2": (1326, 331), "dp": (960, 109)}[layout]
                track = next(item for item in scene["tracks"] if item["id"] == change["oldTrack"]["id"])
                self.assertEqual([item["id"] for item in track["keyframes"]], [item["id"] for item in change["oldTrack"]["keyframes"]])
                self.assertEqual([item["time"] for item in track["keyframes"]], [1, 1.5, 2, 2.25, 2.5, 2.75, 3, 3.25, 3.5, 3.75, 4])
                for keyframe, ordinal in zip(track["keyframes"], range(10, -1, -1)):
                    self.assertAlmostEqual(keyframe["value"] * 1080 + 35, first + 37 * ordinal, delta=1e-4)
                self.assertEqual((track["clock"], track["easing"], track["loop"], track["property"]), ("scroll.speed", "linear", False, "y"))
                cursor = index(scene)[change["cursorNode"]]
                self.assertEqual(cursor["resource"], mapping["playm_classic_spd_sel"])
                for key, expected, axis in (("x", cx - 75, 1920), ("width", 150, 1920), ("height", 70, 1080)):
                    self.assertAlmostEqual(cursor["properties"][key] * axis, expected, delta=1e-4)
                self.assertEqual(cursor["type"], "sprite")

    def test_complete_strip_png_replaces_only_the_old_static_text_ticks_and_remains_upright_on_p2(self):
        parent, reference, mapping = legacy_fixture()
        scene, receipt = controls.upgrade_classic_presentation(parent, reference, mapping)
        before, after = index(parent), index(scene)
        for change in receipt["layouts"]:
            expected = {"p1": (555, 320, 90, 394), "p2": (1281, 320, 90, 394), "dp": (915, 98, 90, 394)}[change["layout"]]
            strip = after[change["stripNode"]]
            self.assertEqual(strip["type"], "sprite")
            self.assertEqual(strip["resource"], mapping["tx_num1_4"])
            self.assertEqual(strip["effects"], [])
            self.assertEqual(strip["children"], [])
            for key, value, axis in zip(("x", "y", "width", "height"), expected, (1920, 1080, 1920, 1080)):
                self.assertAlmostEqual(strip["properties"][key] * axis, value, delta=1e-4)
            self.assertFalse(any(item["target"] == strip["id"] for item in scene["tracks"]))
            self.assertTrue(set(change["removedTickIds"]).isdisjoint(after))
            for old in change["oldClassic"]["children"]:
                if old["id"] not in change["removedTickIds"] and old["id"] != change["cursorNode"]:
                    self.assertEqual(before[old["id"]], after[old["id"]])
            children = after[change["oldClassic"]["id"]]["children"]
            self.assertLess(children.index(strip), next(i for i, node in enumerate(children) if node["id"] == change["cursorNode"]))

    def test_original_gn_label_and_numeric_row_move_together_only_in_classic_without_guessing_digit_count(self):
        parent, reference, mapping = legacy_fixture(compact=True)
        scene, receipt = controls.upgrade_classic_presentation(parent, reference, mapping)
        before, after = index(parent), index(scene)
        for change in receipt["layouts"]:
            layout = change["layout"]
            label = next(node for node in after[change["classicPairModeGate"]]["children"] if node["type"] == "sprite")
            bounds, center = {"p1": ((574, 264, 84, 12), (616, 286)), "p2": ((1269, 264, 84, 12), (1311, 286)),
                              "dp": ((918, 37, 84, 12), (960, 59))}[layout]
            for key, value, axis in zip(("x", "y", "width", "height"), bounds, (1920, 1080, 1920, 1080)):
                self.assertAlmostEqual(label["properties"][key] * axis, value, delta=1e-4)
            green = after[change["classicGreen"]]
            self.assertAlmostEqual((green["properties"]["x"] + 0.5) * 1920, center[0], delta=1e-4)
            self.assertAlmostEqual((green["properties"]["y"] + 0.5) * 1080, center[1], delta=1e-4)
            old_green = next(node for node in change["oldGreenGate"]["children"] if node["type"] == "text")
            self.assertEqual({k: v for k, v in green["properties"].items() if k not in ("x", "y")},
                             {k: v for k, v in old_green["properties"].items() if k not in ("x", "y")})
            self.assertEqual(green["properties"]["format"], "integer")
            self.assertEqual(after[change["oldPairModeGate"]]["children"], change["oldGreenGate"]["children"])
            for old in change["oldGreenGate"]["children"]: self.assertEqual(after[old["id"]], before[old["id"]])
            old_gate = next(item for item in scene["variants"] if item["target"] == change["oldPairModeGate"])
            new_gate = next(item for item in scene["variants"] if item["target"] == change["classicPairModeGate"])
            self.assertEqual((old_gate["default"], new_gate["default"]), (True, False))
            self.assertEqual([(item["key"], item["value"]) for item in old_gate["cases"]], [("classic", False)])
            self.assertEqual([(item["key"], item["value"]) for item in new_gate["cases"]], [("classic", True)])

    def test_off_grid_values_keep_continuous_cursor_interpolation_and_clamped_display_endpoints(self):
        parent, reference, mapping = legacy_fixture()
        scene, receipt = controls.upgrade_classic_presentation(parent, reference, mapping)
        p1 = next(row for row in receipt["layouts"] if row["layout"] == "p1")
        track = next(track for track in scene["tracks"] if track["id"] == p1["oldTrack"]["id"])
        for speed, top in ((0.5, 666), (1, 666), (1.25, 647.5), (2.125, 573.5), (4, 296), (8.3, 296)):
            self.assertAlmostEqual(sample(track, speed) * 1080, top, delta=1e-4)
        self.assertEqual([binding for binding in scene["bindings"] if binding["source"] == "scroll.speed"],
                         [binding for binding in parent["bindings"] if binding["source"] == "scroll.speed"])

    def test_all_unplanned_nodes_programs_and_original_material_owners_survive_exact_reversal(self):
        parent, reference, mapping = legacy_fixture(compact=True)
        scene, receipt = controls.upgrade_classic_presentation(parent, reference, mapping)
        self.assertEqual(controls.reverse_classic_presentation(scene, receipt), parent)
        self.assertEqual(scene["instances"], parent["instances"])
        self.assertEqual(scene["stateMachines"], parent["stateMachines"])
        for field in ("bindings", "variants"):
            self.assertEqual(scene[field][:len(parent[field])], parent[field])
        changed_tracks = {row["oldTrack"]["id"] for row in receipt["layouts"]}
        self.assertEqual([row for row in scene["tracks"] if row["id"] not in changed_tracks],
                         [row for row in parent["tracks"] if row["id"] not in changed_tracks])
        after = index(scene)
        for node in controls._scene_nodes(parent):
            if node["id"] in after and node["id"] not in {row["cursorNode"] for row in receipt["layouts"]}:
                self.assertEqual({k: v for k, v in node.items() if k != "children"}, {k: v for k, v in after[node["id"]].items() if k != "children"})
        declarations = controls._declarations(scene, controls._scene_nodes(scene))
        self.assertEqual(len(declarations), len({item["id"] for item in declarations}))
        for identifier in receipt["addedIds"]: self.assertRegex(identifier, r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")

    def test_wrong_geometry_writer_owner_clock_and_second_application_fail_closed(self):
        for change in ("source", "writer", "pool", "clock", "already"):
            parent, reference, mapping = legacy_fixture()
            if change == "source": reference["panels"]["classic"]["p2"]["shapes"][0]["mirroredX"] = True
            elif change == "writer": parent["bindings"].append({"id": "bad", "target": "gc.p1.classic.tick0", "property": "visible", "source": "scroll.adjusting"})
            elif change == "pool": parent["instances"].append({**next(item for item in parent["instances"] if item["material"]["resource"] == "markers/frame-p1-information"), "id": "pool"})
            elif change == "clock": next(item for item in parent["tracks"] if item["target"] == "gc.p1.classic.slider")["easing"] = "step"
            else: parent = controls.upgrade_classic_presentation(parent, reference, mapping)[0]
            with self.subTest(change=change), self.assertRaises(ValueError): controls.upgrade_classic_presentation(parent, reference, mapping)


class ClassicAssetPreservationTest(unittest.TestCase):
    def test_actual_receipt_shape_takes_label_canvas_from_verified_geo_and_inventory_without_a_png_size_field(self):
        with tempfile.TemporaryDirectory() as temporary:
            library, receipts, reference = source_receipt_fixture(Path(temporary))
            supplement_path = receipts / REFERENCE_FILES[1]
            before = supplement_path.read_bytes()
            supplement = json.loads(before)
            self.assertTrue(all(set(row["sourcePng"]) == {"path", "sha256"} for row in supplement["notesTimeLabelAndCarrier"]))
            with mock.patch.object(controls, "source_reference", return_value=reference):
                actual, expected, metadata = classic_source_inputs(library, receipts)
            self.assertEqual(actual, reference)
            self.assertEqual(len(expected), 7)
            self.assertEqual(expected["tx_notes_time"]["size"], [84, 12])
            self.assertEqual(expected["tx_num1_4"]["size"], [90, 394])
            self.assertEqual(metadata[str(library / "sprites/tx_notes_time.png")], expected["tx_notes_time"]["sha256"])
            self.assertEqual(supplement_path.read_bytes(), before)

    def test_receipt_without_label_png_size_still_rejects_wrong_hash_inventory_canvas_geo_and_sampling(self):
        with tempfile.TemporaryDirectory() as temporary:
            for change in ("hash", "inventory", "canvas", "geo", "sampling"):
                with self.subTest(change=change):
                    library, receipts, reference = source_receipt_fixture(Path(temporary) / change)
                    supplement_path, inventory_path = receipts / REFERENCE_FILES[1], library / "library.json"
                    supplement = json.loads(supplement_path.read_bytes())
                    inventory = json.loads(inventory_path.read_bytes())
                    row = next(item for item in inventory["textures"] if item["name"] == "tx_notes_time.png")
                    if change == "hash":
                        supplement["notesTimeLabelAndCarrier"][0]["sourcePng"]["sha256"] = "0" * 64
                    elif change == "inventory": row["width"] = 85
                    elif change == "canvas":
                        path = library / row["sprite"]
                        Image.new("RGBA", (85, 12), (12, 34, 56, 0)).save(path, format="PNG")
                        row["sha256"] = hashlib.sha256(path.read_bytes()).hexdigest()
                        for label in supplement["notesTimeLabelAndCarrier"]:
                            label["sourcePng"]["sha256"] = row["sha256"]
                    elif change == "geo": supplement["notesTimeLabelAndCarrier"][0]["sourceLabel"]["bounds"][2] = 85
                    else: row["sampler"]["mag"] = "linear"
                    supplement_path.write_text(json.dumps(supplement), encoding="utf-8")
                    inventory_path.write_text(json.dumps(inventory), encoding="utf-8")
                    with mock.patch.object(controls, "source_reference", return_value=reference), self.assertRaises(ValueError):
                        classic_source_inputs(library, receipts)

    def test_append_preserves_every_existing_resource_font_option_and_layout_without_renumbering(self):
        names = ("playm_spd_sel", "tx_notes_time", "spd_classic_window", "spd_classic_window_dp", "tx_classic_hispeed")
        expected = {name: {"sha256": hashlib.sha256(name.encode()).hexdigest(), "name": name} for name in (*names, *controls.CLASSIC_ASSET_NAMES)}
        resources = [{"id": "gc.a" + format(i, "x"), "path": f"old/{name}.png", "type": "texture", "filter": "nearest"} for i, name in enumerate(names)]
        resources.append({"id": "gc.a1a", "path": "old/retained.png", "type": "texture", "filter": "nearest"})
        inventory = [{"path": item["path"], "sha256": expected[name]["sha256"]} for item, name in zip(resources, names)]
        inventory.append({"path": "old/retained.png", "sha256": hashlib.sha256(b"retained").hexdigest()})
        manifest = {"resources": resources, "bitmapFonts": [{"id": "gc.font", "lineHeight": 20, "glyphs": [{"advance": 21}]}],
                    "options": [{"id": "real"}], "layouts": [{"id": "real-layout"}]}
        frozen = copy.deepcopy(manifest)
        result, mapping, additions = append_classic_resources(manifest, expected, inventory)
        self.assertEqual(manifest, frozen)
        self.assertEqual(result["resources"][:-2], frozen["resources"])
        self.assertEqual([row["resource"]["id"] for row in additions], ["gc.a1b", "gc.a1c"])
        self.assertEqual(mapping["tx_num1_4"], "gc.a1b")
        self.assertEqual(mapping["playm_classic_spd_sel"], "gc.a1c")
        del result["resources"][-2:]
        self.assertEqual(result, frozen)
        inventory[0]["sha256"] = hashlib.sha256(b"changed").hexdigest()
        with self.assertRaises(ValueError): append_classic_resources(manifest, expected, inventory)

    def test_whole_png_bytes_including_hidden_rgb_and_metadata_are_direct_copied_and_changed_source_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "source"; output = root / "output"
            source.mkdir(); output.mkdir(); rows = []
            for name, size in (("tx_num1_4", (90, 394)), ("playm_classic_spd_sel", (150, 70))):
                buffer = io.BytesIO(); info = PngImagePlugin.PngInfo(); info.add_text("fixture", name)
                Image.new("RGBA", size, (12, 34, 56, 0)).save(buffer, format="PNG", pnginfo=info)
                path = source / (name + ".png"); path.write_bytes(buffer.getvalue())
                rows.append({"source": str(path), "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "resource": {"path": "art/" + path.name}})
            with mock.patch.object(Image.Image, "save", side_effect=AssertionError("No encoding original art")):
                copy_classic_resources(rows, output)
            for row in rows: self.assertEqual((output / row["resource"]["path"]).read_bytes(), Path(row["source"]).read_bytes())
            Path(rows[0]["source"]).write_bytes(b"tampered")
            other = root / "other"; other.mkdir()
            with self.assertRaises(ValueError): copy_classic_resources(rows, other)
            with self.assertRaises(ValueError): copy_classic_resources(rows, output)


if __name__ == "__main__":
    unittest.main()
