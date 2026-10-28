"""Portable author-contract fixtures; actual original art, C5 and native acceptance are separate gates."""
import copy
import hashlib
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from PIL import Image, PngImagePlugin

import lighting_header as header
from build_lighting_header import copy_header_art, read_art_inventory, rewrite_33_catalog, upgrade_manifest

MODS_RECT = [0.015, 0.15, 0.18, 0.065]


def reference():
    layouts = {}
    for movie, player, difficulty, label, digits in [
            ("center_frame_sp", "1p", [379, 38, 565, 64], [429, 69, 475, 89], [477, 69, 513, 89]),
            ("dp_frame_1p", "1p", [21, 129, 207, 155], [72, 158, 118, 178], [118, 158, 154, 178])]:
        draws = []
        for binding, texture, bounds in [("diff_", "playm_hyper", difficulty), ("lv_", "txt_lv_dot", label), ("lvlnum_", "lvlnum_12", digits)]:
            draws.append({"game_bindings": [binding + player], "texture": texture,
                          "asset": {"package": "gmframe33", "name": texture},
                          "bounds": dict(zip(("left", "top", "right", "bottom"), bounds))})
        layouts[movie] = {"package": "gmframe33", "draws": draws}
    layouts["center_frame_sp"]["draws"].extend([
        {**copy.deepcopy(draw), "game_bindings": [draw["game_bindings"][0].replace("1p", "2p")],
         "bounds": {**draw["bounds"], "left": draw["bounds"]["left"] + 977, "right": draw["bounds"]["right"] + 977}}
        for draw in layouts["center_frame_sp"]["draws"][:]])
    return {"stage": {"width": 1920, "height": 1080, "fps": 60}, "layouts": layouts}


def art():
    names = header.ART_NAMES
    return {"mapping": {name: f"h33.r{index}" for index, name in enumerate(names)},
            "resources": [{"id": f"h33.r{index}", "type": "texture", "path": f"header33/{name}.png", "filter": "nearest"}
                          for index, name in enumerate(names)]}


def scene(layout="p1", mods_rect=MODS_RECT):
    # This is the real white factory still used by build_skin.py, not a second production compatibility path.
    program = header.header_program(reference(), layout, "hx." + layout, mods_rect=mods_rect)
    fields = [{"id": "title", "type": "text", "properties": {"text": ""}, "children": []},
              {"id": "artist", "type": "text", "properties": {"text": ""}, "children": []}, program["root"]]
    return {"root": {"id": "old-root", "properties": {}, "children": []},
            "templates": [{"id": "info", "root": {"id": "info-root", "slot": "decoration", "target": {"kind": "global"},
                            "properties": {}, "children": fields}}],
            "instances": [{"id": "info-instance", "template": "info", "material": {"slot": "decoration", "resource": "old-marker"}}],
            "bindings": [{"id": "title-binding", "target": "title", "property": "text", "source": "song.title"},
                         {"id": "artist-binding", "target": "artist", "property": "text", "source": "song.artist"}, *program["bindings"]],
            "variants": program["variants"], "tracks": [], "stateMachines": []}


def upgraded(layout="p1", mods_rect=MODS_RECT):
    parent = scene(layout, mods_rect)
    return parent, *header.upgrade_header(parent, reference(), layout, "hx." + layout, "hr." + layout,
                                          header.identifiers(parent), art(), mods_rect=mods_rect)


def nodes(document):
    return {node["id"]: node for root in [document["root"], *(item["root"] for item in document["templates"])] for node in header.walk(root)}


def variant(document, target, prop, value):
    declared = [item for item in document["variants"] if item["target"] == target and item["property"] == prop]
    assert len(declared) == 1
    item = declared[0]
    match = next((case for case in item["cases"] if case["key"] == value), None)
    return match["resource" if prop == "resource" else "value"] if match else item["default"]


class HeaderAuthorTests(unittest.TestCase):
    def test_the_existing_white_builder_still_has_raw_empty_consumers_and_bms_scope(self):
        result = header.header_program(reference(), "p1", "factory")
        self.assertEqual([item["source"] for item in result["bindings"]], ["song.difficulty", "song.level"])
        self.assertEqual(result["variants"][0]["source"], "layout.ruleset")
        self.assertEqual(result["variants"][0]["cases"], [{"id": "factoryc", "key": "bms", "value": True}])
        for node in result["root"]["children"]:
            self.assertEqual(node["properties"]["text"], "")
            self.assertEqual(node["properties"]["text-overflow"], "ellipsis")
            self.assertNotIn("format", node["properties"])
            self.assertNotIn("bitmap-font", node["properties"])

    def test_known_difficulty_uses_its_exact_complete_sprite_and_hides_only_its_fallback(self):
        _, result, _ = upgraded()
        for declaration, texture in header.DIFFICULTY_TEXTURES.items():
            with self.subTest(declaration=declaration):
                self.assertTrue(variant(result, "hr.p1.d", "visible", declaration))
                self.assertFalse(variant(result, "hx.p1d", "visible", declaration))
                self.assertEqual(variant(result, "hr.p1.d", "resource", declaration), art()["mapping"][texture])
        self.assertEqual(len([item for item in result["templates"][0]["root"]["children"][-1]["children"] if item["type"] == "sprite"]), 1)

    def test_insane_unknown_case_whitespace_empty_and_absent_never_show_a_fake_default_label(self):
        _, result, _ = upgraded()
        for declaration in ("INSANE", "5", "hyper", "HYPER ", "UNKNOWN", "", None):
            with self.subTest(declaration=declaration):
                self.assertFalse(variant(result, "hr.p1.d", "visible", declaration))
                self.assertTrue(variant(result, "hx.p1d", "visible", declaration))
        self.assertNotIn("INSANE", header.DIFFICULTY_TEXTURES)
        self.assertFalse(nodes(result)["hr.p1.d"]["properties"]["visible"])
        self.assertFalse(any(case["key"] == "" for item in result["variants"] for case in item["cases"]))

    def test_each_canonical_level_one_through_twelve_selects_one_whole_number_and_lv_group(self):
        _, result, _ = upgraded()
        for declaration, texture in header.LEVEL_TEXTURES.items():
            with self.subTest(declaration=declaration):
                self.assertTrue(variant(result, "hr.p1.l", "visible", declaration))
                self.assertFalse(variant(result, "hx.p1l", "visible", declaration))
                self.assertEqual(variant(result, "hr.p1.n", "resource", declaration), art()["mapping"][texture])
        self.assertEqual(nodes(result)["hr.p1.l"]["children"], [nodes(result)["hr.p1.lv"], nodes(result)["hr.p1.n"]])
        self.assertEqual(nodes(result)["hr.p1.lv"]["resource"], art()["mapping"]["txt_lv_dot"])

    def test_noncanonical_out_of_range_noninteger_and_empty_levels_remain_raw_white_text(self):
        parent, result, _ = upgraded()
        for declaration in ("0", "13", "99", "-1", "01", "+1", "1.0", "1e0", " 1", "１２", "", None):
            with self.subTest(declaration=declaration):
                self.assertFalse(variant(result, "hr.p1.l", "visible", declaration))
                self.assertTrue(variant(result, "hx.p1l", "visible", declaration))
        self.assertEqual(nodes(result)["hx.p1l"], nodes(parent)["hx.p1l"])
        self.assertEqual(result["bindings"], parent["bindings"])
        self.assertFalse(nodes(result)["hr.p1.l"]["properties"]["visible"])

    def test_p1_and_p2_full_pngs_use_source_bounds_and_upright_text(self):
        for layout, difficulty, label, number in [
                ("p1", (649, 38, 186, 26), (699, 69, 46, 20), (747, 69, 36, 20)),
                ("p2", (1086, 38, 186, 26), (1136, 69, 46, 20), (1184, 69, 36, 20))]:
            _, result, _ = upgraded(layout)
            for suffix, expected in (("d", difficulty), ("lv", label), ("n", number)):
                item = nodes(result)["hr." + layout + "." + suffix]
                self.assertEqual(item["type"], "sprite")
                actual = tuple(item["properties"][key] * axis for key, axis in (("x", 1920), ("y", 1080), ("width", 1920), ("height", 1080)))
                for first, second in zip(actual[:2], expected[:2]): self.assertAlmostEqual(first, second, delta=1e-4)
                for first, second in zip(actual[2:], expected[2:]): self.assertAlmostEqual(first, second, places=4)
                self.assertNotIn("scale-x", item["properties"])
                self.assertNotIn("projective-quad", item["properties"])

    def test_dp_whole_lv_and_number_group_keeps_original_size_and_avoids_actual_mods(self):
        parent, result, receipt = upgraded("dp")
        geometry = receipt["levelArtGeometry"]
        safe = receipt["fields"][1]["appliedBounds"]
        protected = receipt["protectedNativeModsBounds"]
        self.assertEqual(geometry["scale"], 1)
        self.assertEqual(geometry["gap"], 2)
        self.assertEqual(safe["left"], 211)
        self.assertAlmostEqual(safe["right"], 374.4)
        for name, size in (("label", (46, 20)), ("number", (36, 20)), ("group", (84, 20))):
            rect = geometry[name]
            self.assertFalse(header.overlaps(rect, protected))
            self.assertGreaterEqual(rect["left"], safe["left"])
            self.assertLessEqual(rect["right"], safe["right"])
            self.assertGreaterEqual(rect["top"], safe["top"])
            self.assertLessEqual(rect["bottom"], safe["bottom"])
            self.assertGreaterEqual(rect["bottom"] - rect["top"], 20)
            self.assertAlmostEqual((rect["right"] - rect["left"]) / size[0], geometry["scale"])
            self.assertAlmostEqual((rect["bottom"] - rect["top"]) / size[1], geometry["scale"])
        self.assertAlmostEqual(geometry["label"]["right"] + 2, geometry["number"]["left"])
        self.assertAlmostEqual(geometry["label"]["left"], 250.7)
        self.assertAlmostEqual(geometry["number"]["left"], 298.7)
        self.assertEqual(geometry["label"]["top"], 132)
        for suffix, name in (("lv", "label"), ("n", "number")):
            sprite = nodes(result)["hr.dp." + suffix]
            properties, bounds = sprite["properties"], geometry[name]
            actual = (properties["x"] * 1920, properties["y"] * 1080,
                      properties["width"] * 1920, properties["height"] * 1080)
            expected = (bounds["left"], bounds["top"], bounds["right"] - bounds["left"], bounds["bottom"] - bounds["top"])
            for first, second in zip(actual[:2], expected[:2]): self.assertAlmostEqual(first, second, delta=1e-4)
            for first, second in zip(actual[2:], expected[2:]): self.assertAlmostEqual(first, second, places=4)
            self.assertEqual(sprite["blend"], "alpha")
            self.assertEqual(sprite["effects"], [])
        self.assertEqual(nodes(result)["hx.dpl"], nodes(parent)["hx.dpl"])
        diff = nodes(result)["hr.dp.d"]["properties"]
        self.assertAlmostEqual(diff["x"] * 1920, 21, delta=1e-4)
        self.assertAlmostEqual(diff["y"] * 1080, 129, delta=1e-4)

    def test_dp_rejects_area_that_only_fits_the_old_thirty_six_pixel_text_minimum(self):
        rect = [0.015, 0.15, 0.115, 0.065]
        # This fits the old text rule but cannot fit the original-size 84x20 Lv+number group.
        old = header.header_program(reference(), "dp", "hx.dp", mods_rect=rect)
        self.assertGreater(old["receipt"]["fields"][1]["appliedBounds"]["right"] - 211, 36)
        with self.assertRaises(ValueError): upgraded("dp", rect)

    def test_every_old_typed_field_resource_and_fallback_is_recoverable(self):
        for layout in ("p1", "p2", "dp"):
            parent, result, receipt = upgraded(layout)
            self.assertEqual(header.reverse_header_art(result, receipt), parent)
            self.assertTrue(header.identifiers(parent).issubset(header.identifiers(result)))
            self.assertEqual(result["bindings"], parent["bindings"])
            self.assertEqual(result["tracks"], parent["tracks"])
            self.assertEqual(result["instances"], parent["instances"])
            self.assertEqual(receipt["reservedTextGlyphIncrement"], 0)
            self.assertEqual(receipt["addedNodes"], 4)
            self.assertEqual(receipt["addedVariants"], 6)
            for identifier in [*receipt["addedIds"], *(resource["id"] for resource in art()["resources"])]:
                self.assertRegex(identifier, r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")

    def test_no_mania_alpha_writer_or_second_header_is_created(self):
        parent, result, _ = upgraded()
        self.assertEqual([item for item in result["variants"] if item["target"] == "hx.p1"], parent["variants"])
        self.assertEqual(len([node for node in header.walk(result["templates"][0]["root"]) if node["id"] == "hx.p1"]), 1)
        self.assertTrue(all(item["source"] in ("song.difficulty", "song.level") for item in result["variants"][1:]))

    def test_actual_owner_geometry_single_instance_and_fallback_writers_are_required(self):
        for change in ("stage", "pool", "geometry", "missing", "writer"):
            parent = scene()
            if change == "stage": parent["templates"][0]["root"]["target"] = {"kind": "stage"}
            elif change == "pool": parent["instances"].append({**parent["instances"][0], "id": "second"})
            elif change == "geometry": nodes(parent)["hx.p1d"]["properties"]["x"] += 0.01
            elif change == "missing": parent["bindings"].pop()
            else: parent["bindings"].append({"id": "old-writer", "target": "hx.p1d", "property": "visible", "source": "offset.available"})
            with self.subTest(change=change), self.assertRaises(ValueError):
                header.upgrade_header(parent, reference(), "p1", "hx.p1", "hr.p1", header.identifiers(parent), art(), mods_rect=MODS_RECT)

    def test_collision_and_second_application_are_rejected(self):
        parent, result, _ = upgraded()
        with self.assertRaises(ValueError):
            header.upgrade_header(parent, reference(), "p1", "hx.p1", "hr.p1", {"hr.p1.dr.k0"}, art(), mods_rect=MODS_RECT)
        with self.assertRaises(ValueError):
            header.upgrade_header(result, reference(), "p1", "hx.p1", "hr.p1", header.identifiers(result), art(), mods_rect=MODS_RECT)

    def test_original_png_bytes_including_transparent_rgb_and_text_chunks_are_only_copied(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, output = root / "source", root / "output"
            source.mkdir(); output.mkdir()
            digests = {}
            for index, name in enumerate(header.ART_NAMES):
                size = (186, 26) if name in header.DIFFICULTY_TEXTURES.values() else (46, 20) if name == "txt_lv_dot" else (36, 20)
                # Synthetic portable fixture, never an embedded copy of the private source art.
                info = PngImagePlugin.PngInfo(); info.add_text("fixture", name)
                stream = io.BytesIO()
                Image.new("RGBA", size, (index + 1, 10, 20, 0)).save(stream, format="PNG", pnginfo=info)
                data = stream.getvalue(); (source / (name + ".png")).write_bytes(data)
                digests[name] = hashlib.sha256(data).hexdigest()
            private_inventory = root / "art-inventory.json"
            private_inventory.write_text(json.dumps({name: digests[name].upper() for name in reversed(header.ART_NAMES)}), encoding="utf-8")
            expected = read_art_inventory(private_inventory)
            bundle = header.original_art(source, expected)
            with mock.patch.object(Image.Image, "save", side_effect=AssertionError("Do not encode source images")):
                copy_header_art(bundle, output)
            self.assertEqual(len(bundle["resources"]), 18)
            self.assertEqual([row["texture"] for row in bundle["sources"]], list(header.ART_NAMES))
            for row in bundle["sources"]:
                self.assertEqual((output / row["path"]).read_bytes(), Path(row["source"]).read_bytes())
            (source / "playm_hyper.png").write_bytes(b"changed original")
            with self.assertRaises(ValueError): header.original_art(source, expected)
            with self.assertRaises(ValueError): copy_header_art(bundle, output)

    def test_private_art_inventory_requires_exact_names_and_full_sha256_values(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "art-inventory.json"
            expected = {name: hashlib.sha256(name.encode()).hexdigest() for name in header.ART_NAMES}
            # Input order and hex casing never change the fixed art-resource order.
            path.write_text(json.dumps({name: expected[name].upper() for name in reversed(header.ART_NAMES)}), encoding="utf-8")
            self.assertEqual(read_art_inventory(path), expected)
            for change in ("missing", "extra", "short", "nonhex", "not-string", "not-map", "duplicate"):
                value = dict(expected)
                if change == "missing": value.pop("txt_lv_dot")
                elif change == "extra": value["other"] = "0" * 64
                elif change == "short": value["txt_lv_dot"] = "0" * 63
                elif change == "nonhex": value["txt_lv_dot"] = "z" * 64
                elif change == "not-string": value["txt_lv_dot"] = None
                elif change == "not-map": value = []
                text = json.dumps(value)
                if change == "duplicate": text = text[:-1] + ',"txt_lv_dot":"' + expected["txt_lv_dot"] + '"}'
                path.write_text(text, encoding="utf-8")
                with self.subTest(change=change), self.assertRaises(ValueError): read_art_inventory(path)

    def test_manifest_extension_preserves_all_old_metadata_and_has_no_font_or_option_delta(self):
        original = {"resources": [{"id": "old", "path": "old.png"}], "bitmapFonts": [{"id": "old-font"}],
                    "layouts": [{"modsRect": MODS_RECT}], "options": [], "animations": [{"id": "old-animation"}]}
        frozen = copy.deepcopy(original)
        result = upgrade_manifest(original, art())
        self.assertEqual(original, frozen)
        self.assertEqual(result["resources"][:1], original["resources"])
        self.assertEqual(result["resources"][1:], art()["resources"])
        del result["resources"][-18:]
        self.assertEqual(result, original)
        with self.assertRaises(ValueError): upgrade_manifest(upgrade_manifest(original, art()), art())

    def test_catalog_rewrites_only_the_existing_three33_resource_and_scene_pairs(self):
        old = {"modules": [{"id": "frame", "default": "iidx33", "choices": [
            {"value": "iidx33", "label": "33", "preview": "old33.png", "scene": "dp-s", "manifest": "dp-m", "ini": "dp-i",
             "variants": [{"when": {"layout": "p1", "graph-visible": True}, "scene": "p1-s", "manifest": "p1-m", "ini": "p1-i"},
                          {"when": {"layout": "p2"}, "scene": "p2-s", "manifest": "p2-m", "ini": "p2-i"}]},
            *[{"value": "other" + str(index), "scene": "keep-s" + str(index), "manifest": "keep-m" + str(index), "preview": "keep-p" + str(index)} for index in range(14)]]},
            {"id": "notes", "default": "n00", "choices": [{"value": "n00", "preview": "notes.png", "scene": "notes-s", "manifest": "notes-m"}]}]}
        frozen = copy.deepcopy(old)
        replacements = {(mode + "-s", mode + "-m"): (mode + "-new-s", mode + "-new-m") for mode in ("p1", "p2", "dp")}
        result = rewrite_33_catalog(old, replacements)
        self.assertEqual(old, frozen)
        self.assertEqual(result["modules"][0]["choices"][1:], old["modules"][0]["choices"][1:])
        self.assertEqual(result["modules"][1:], old["modules"][1:])
        self.assertEqual(result["modules"][0]["choices"][0]["preview"], "old33.png")
        self.assertEqual(result["modules"][0]["choices"][0]["ini"], "dp-i")

    def test_wrong_reference_canvas_cabinet_and_missing_or_ambiguous_hooks_are_rejected(self):
        for change in ("canvas", "cabinet", "missing", "ambiguous"):
            data = reference()
            if change == "canvas": data["stage"]["width"] = 1280
            elif change == "cabinet": data["layouts"]["center_frame_sp"]["package"] = "gmframe32"
            elif change == "missing": data["layouts"]["center_frame_sp"]["draws"].pop(0)
            else:
                duplicate = copy.deepcopy(data["layouts"]["center_frame_sp"]["draws"][0]); duplicate["bounds"]["left"] += 1
                data["layouts"]["center_frame_sp"]["draws"].append(duplicate)
            with self.subTest(change=change), self.assertRaises(ValueError):
                header.upgrade_header(scene(), data, "p1", "hx.p1", "hr.p1", header.identifiers(scene()), art(), mods_rect=MODS_RECT)


if __name__ == "__main__":
    unittest.main()
