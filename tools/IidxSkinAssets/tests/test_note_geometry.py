"""Independent synthetic fixtures for the known Omni Mania note author correction."""

import copy
import hashlib
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock
import zlib

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from note_geometry import ALIASES, apply_to_author, correct_ini, correct_scene, marker_dimensions


COLOURS = ("white", "black", "scratch")
DIMENSIONS = {
    "custom/markers/n/white": (52, 15), "custom/markers/n/black": (40, 15), "custom/markers/n/scratch": (90, 15),
    "custom/markers/c/white-head": (52, 24), "custom/markers/c/white-tail": (52, 24),
    "custom/markers/c/black-head": (40, 24), "custom/markers/c/black-tail": (40, 24),
    "custom/markers/c/scratch-head": (90, 24), "custom/markers/c/scratch-tail": (90, 24),
}


def encoded(document):
    return json.dumps(document, separators=(",", ":")).encode("utf-8")


def fixture_scene(category="n", style=0, size="normal", palette="source"):
    prefix = f"{category}.{category}{style:02}.{size}" + ("." + palette if category == "c" else "")
    document = {"contract": "oms-gameplay-skin-scene.v1", "root": {
        "id": prefix + ".root", "type": "container", "target": {"kind": "global"}, "blend": "inherit",
        "properties": {}, "effects": [], "children": []},
        "tracks": [], "stateMachines": [], "bindings": [], "variants": [], "templates": [], "instances": []}
    for colour in COLOURS:
        for part in ((None,) if category == "n" else ("head", "body", "tail")):
            name = colour + ("." + part if part else "")
            node_id, template_id = prefix + "." + name, prefix + ".t." + name
            slot = "object.note" if part is None else "object.long-note." + part
            marker = f"custom/markers/{category}/{colour}" + ("-" + part if part else "")
            resource = f"{category}.art.{name}"
            if category == "c":
                source_height = {"normal": 24, "long": 27, "short": 21, "veryshort": 18}[size]
            elif style == 11:
                source_height = {"normal": 12, "long": 15, "short": 9, "veryshort": 6}[size]
            else:
                source_height = 16 if (style, size, colour) == (5, "long", "white") else 15
            props = {} if part == "body" else {"height": source_height * (768 / 1080) / 22.5,
                "y": -5 * (768 / 1080) / 22.5, "anchor": "bottom-left", "origin": "bottom-left"}
            root = {"id": node_id, "type": "sprite", "target": {"kind": "global"}, "blend": "alpha",
                    "properties": props, "effects": [], "children": [], "resource": resource, "slot": slot}
            document["templates"].append({"id": template_id, "root": root})
            document["instances"].append({"id": template_id + ".i", "template": template_id,
                                          "material": {"slot": slot, "resource": marker}})
            if category == "c":
                source = "object.long-note-appearance" if part == "body" else "object.long-note-mode"
                keys = ("ln", "ln-holding", "ln-released", "cn", "cn-holding", "cn-released", "hcn", "hcn-holding", "hcn-released") if part == "body" else ("ln", "cn", "hcn")
                cases = []
                for key in keys:
                    chosen = resource
                    if key.endswith("-holding"):
                        chosen += ".active"
                    elif key.endswith("-released"):
                        chosen += ".released"
                    if key.startswith("hcn") and palette == "source":
                        chosen += ".hcn"
                    cases.append({"id": node_id + ".c." + source + "." + key, "key": key, "resource": chosen})
                document["variants"].append({"id": node_id + ".v." + source, "target": node_id,
                    "property": "resource", "source": source, "default": resource, "cases": cases})
    return document


def png(width, height):
    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))
    pixels = (b"\0" + b"\x20\x80\xff\xff" * width) * height
    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(pixels)) + chunk(b"IEND", b""))


class NoteGeometrySceneTests(unittest.TestCase):
    def test_each_colour_preserves_original_pixel_height_offset_and_bms_body_bytes(self):
        for category in ("n", "c"):
            with self.subTest(category=category):
                document = fixture_scene(category)
                original = copy.deepcopy(document)
                result = correct_scene(document, DIMENSIONS)
                count = len(original["templates"])
                self.assertTrue(result["changed"])
                self.assertEqual(encoded(original["templates"]), encoded(document["templates"][:count]))
                self.assertEqual(encoded(original["instances"]), encoded(document["instances"][:count]))
                self.assertEqual(encoded(original["variants"]), encoded(document["variants"][:len(original["variants"])]))
                self.assertEqual(original["root"], document["root"])
                for source, instance in zip(original["templates"], original["instances"]):
                    slot = source["root"]["slot"]
                    matching = [item for item in document["templates"] if item["id"] == source["id"] + ".mania"]
                    if slot == "object.long-note.body":
                        self.assertEqual([], matching)
                        continue
                    self.assertEqual(1, len(matching))
                    clone = matching[0]["root"]
                    width, height = DIMENSIONS[instance["material"]["resource"]]
                    # Independent native material calculation; no correction-factor helper is used.
                    native_height = height * 48 / width
                    self.assertAlmostEqual(15 if category == "n" else 24,
                        clone["properties"]["height"] * native_height * 1080 / 768, places=11)
                    self.assertAlmostEqual(-5, clone["properties"]["y"] * native_height * 1080 / 768, places=11)
                    self.assertEqual(source["root"]["resource"], clone["resource"])
                    self.assertEqual(source["root"]["blend"], clone["blend"])

    def test_material_aliases_select_only_one_template_per_ruleset_and_native_slot(self):
        for category in ("n", "c"):
            document = fixture_scene(category)
            correct_scene(document, DIMENSIONS)
            for marker, (width, height) in DIMENSIONS.items():
                if marker.split("/")[2] != category:
                    continue
                for selected in (marker, ALIASES[marker]):
                    matches = [item for item in document["instances"] if item["material"]["resource"] == selected]
                    self.assertEqual(1, len(matches), (selected, width, height))
                    self.assertEqual(selected.endswith("mania-" + marker.rsplit("/", 1)[1]), matches[0]["template"].endswith(".mania"))

    def test_notes11_all_sizes_retain_the_authored_scale_of_the_same_original_image(self):
        for size, expected_pixels in (("normal", 12), ("long", 15), ("short", 9), ("veryshort", 6)):
            document = fixture_scene("n", 11, size)
            correct_scene(document, DIMENSIONS)
            for clone in document["templates"][3:]:
                colour = clone["root"]["id"].split(".")[-2]
                marker_width = {"white": 52, "black": 40, "scratch": 90}[colour]
                native_height = 15 * 48 / marker_width
                pixels = clone["root"]["properties"]["height"] * native_height * 1080 / 768
                self.assertAlmostEqual(expected_pixels, pixels, places=11)
                self.assertEqual("n.art." + colour, clone["root"]["resource"])

    def test_actual_n05_long_white16_canvas_is_not_replaced_with_other_colours15(self):
        document = fixture_scene("n", 5, "long")
        correct_scene(document, DIMENSIONS)
        for clone, expected_pixels, width in zip(document["templates"][3:], (16, 15, 15), (52, 40, 90)):
            self.assertAlmostEqual(expected_pixels, clone["root"]["properties"]["height"] * (15 * 48 / width) * 1080 / 768)

    def test_cn_all_sizes_and_palettes_clone_every_cap_variant_but_keep_body_exact(self):
        for palette in ("source", "cn-palette"):
            for size, expected_pixels in (("normal", 24), ("long", 27), ("short", 21), ("veryshort", 18)):
                document = fixture_scene("c", 0, size, palette)
                original = copy.deepcopy(document)
                result = correct_scene(document, DIMENSIONS)
                self.assertEqual(6, result["addedTemplates"])
                self.assertEqual(15, len(document["templates"]))
                self.assertEqual(15, len(document["variants"]))
                for old in original["variants"]:
                    clones = [item for item in document["variants"] if item["id"] == old["id"] + ".mania"]
                    if old["source"] == "object.long-note-appearance":
                        self.assertEqual([], clones)
                    else:
                        self.assertEqual(1, len(clones))
                        clone = clones[0]
                        self.assertEqual(old["target"] + ".mania", clone["target"])
                        self.assertEqual(old["default"], clone["default"])
                        self.assertEqual([case["resource"] for case in old["cases"]], [case["resource"] for case in clone["cases"]])
                        self.assertEqual([case["id"] + ".mania" for case in old["cases"]], [case["id"] for case in clone["cases"]])
                for clone in document["templates"][9:]:
                    colour = clone["root"]["id"].split(".")[-3]
                    width = {"white": 52, "black": 40, "scratch": 90}[colour]
                    self.assertAlmostEqual(expected_pixels, clone["root"]["properties"]["height"] * (24 * 48 / width) * 1080 / 768)

    def test_scene_correction_is_byte_idempotent_and_rejects_partial_or_changed_clones(self):
        document = fixture_scene("c")
        correct_scene(document, DIMENSIONS)
        once = encoded(document)
        second = correct_scene(document, DIMENSIONS)
        self.assertFalse(second["changed"])
        self.assertEqual(0, second["addedTemplates"])
        self.assertEqual(once, encoded(document))
        for altered in ("instance", "height", "case"):
            value = copy.deepcopy(document)
            if altered == "instance":
                value["instances"].pop()
            elif altered == "height":
                value["templates"][-1]["root"]["properties"]["height"] += 0.1
            else:
                value["variants"][-1]["cases"][-1]["resource"] = "changed.resource"
            before = encoded(value)
            with self.assertRaisesRegex(ValueError, "conflicting or incomplete"):
                correct_scene(value, DIMENSIONS)
            self.assertEqual(before, encoded(value))

    def test_unknown_scene_is_untouched_but_altered_known_shapes_fail_before_mutation(self):
        unrelated = {"root": {"id": "omni.frame.iidx33.root"}}
        self.assertFalse(correct_scene(unrelated, {})["recognized"])
        self.assertEqual({"root": {"id": "omni.frame.iidx33.root"}}, unrelated)
        for change in ("height", "nan", "offset", "anchor", "binding", "missing", "variant", "body", "root"):
            document = fixture_scene("c")
            if change == "height":
                document["templates"][0]["root"]["properties"]["height"] = 1
            elif change == "nan":
                document["templates"][0]["root"]["properties"]["height"] = float("nan")
            elif change == "offset":
                document["templates"][0]["root"]["properties"]["y"] = 0
            elif change == "anchor":
                document["templates"][0]["root"]["properties"]["anchor"] = "top-left"
            elif change == "binding":
                document["bindings"].append({"id": "new.binding"})
            elif change == "missing":
                document["templates"].pop()
            elif change == "variant":
                document["variants"][0]["cases"][0]["resource"] = "altered.resource"
            elif change == "body":
                document["templates"][1]["root"]["properties"]["height"] = 0.5
            else:
                document["root"]["id"] = "c.c99.normal.source.root"
            before = encoded(document)
            with self.assertRaises(ValueError, msg=change):
                correct_scene(document, DIMENSIONS)
            self.assertEqual(before, encoded(document))
        dimensions = dict(DIMENSIONS)
        dimensions["custom/markers/n/white"] = (52, 16)
        with self.assertRaisesRegex(ValueError, "marker dimensions"):
            correct_scene(fixture_scene(), dimensions)


class NoteGeometryIniTests(unittest.TestCase):
    def test_only_exact_common_mania_lane_note_and_caps_change_with_bytes_preserved_elsewhere(self):
        original = ("\ufeff[GameplaySkin.Common:1]\r\n"
            'Target: Lane ruleset=bms keymode=7k\r\nobject.note: resource Provide "custom/markers/n/white"\r\n'
            'Target: Stage ruleset=mania keymode=7k\r\nobject.note: resource Provide "custom/markers/n/white"\r\n'
            'Target: Lane ruleset=any keymode=7k\r\nobject.note: resource Provide "custom/markers/n/white"\r\n'
            'Target: Lane ruleset=mania keymode=7k\r\n  object.note: resource Provide "custom/markers/n/white" // original image\r\n'
            'object.long-note.head: resource Provide "custom/markers/c/black-head"\r\n'
            'object.long-note.tail: resource Provide "custom/markers/c/scratch-tail"\r\n'
            'object.long-note.body: resource Provide "custom/markers/c/white-body"\r\n'
            '[Other]\r\nTarget: Lane ruleset=mania keymode=7k\r\nobject.note: resource Provide "custom/markers/n/white"').encode("utf-8")
        expected = original.replace(b'  object.note: resource Provide "custom/markers/n/white" //', b'  object.note: resource Provide "custom/markers/n/mania-white" //')
        expected = expected.replace(b'Provide "custom/markers/c/black-head"', b'Provide "custom/markers/c/mania-black-head"')
        expected = expected.replace(b'Provide "custom/markers/c/scratch-tail"', b'Provide "custom/markers/c/mania-scratch-tail"')
        actual, receipt = correct_ini(original)
        self.assertEqual(expected, actual)
        self.assertEqual(3, len(receipt["rewrittenEntries"]))
        second, receipt = correct_ini(actual)
        self.assertEqual(actual, second)
        self.assertFalse(receipt["changed"])
        self.assertEqual(3, receipt["alreadyCorrectEntries"])

    def test_mania_owned_slots_reject_changed_operation_resource_and_conflicting_selector(self):
        for tail in ('object.note: resource Suppress', 'object.note: resource Provide "another/resource"',
                     'object.note: resource Provide "custom/markers/c/white-head"'):
            with self.assertRaises(ValueError):
                correct_ini(('[GameplaySkin.Common:1]\nTarget: Lane ruleset=mania keymode=7k\n' + tail).encode())
        with self.assertRaisesRegex(ValueError, "conflicting ruleset"):
            correct_ini(b'[GameplaySkin.Common:1]\nTarget: Lane ruleset=mania ruleset=bms\n')


class NoteGeometryAuthorTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="oms-note-geometry-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        (self.root / "gameplay").mkdir()
        (self.root / "gameplay-skin.json").write_bytes(encoded({"contract": "oms-gameplay-skin-manifest.v1", "contentCatalog": "gameplay/content.json"}))
        modules = []
        for category, identifier in (("n", "notes"), ("c", "charge-notes")):
            scene = fixture_scene(category)
            (self.root / f"{category}.json").write_bytes(encoded(scene))
            lines = ["// unchanged author comment"]
            for ruleset in ("bms", "mania"):
                lines.extend(("[GameplaySkin.Common:1]", f"Target: Lane ruleset={ruleset} keymode=7k"))
                for instance in scene["instances"]:
                    material = instance["material"]
                    lines.append(f'{material["slot"]}: resource Provide "{material["resource"]}"')
            (self.root / f"{category}.ini").write_bytes(("\n".join(lines) + "\n").encode())
            modules.append({"id": identifier, "default": category + "00", "choices": [
                {"value": category + "00", "scene": category + ".json", "ini": category + ".ini"}]})
        (self.root / "gameplay/content.json").write_bytes(encoded({"contract": "oms-gameplay-skin-content.v1", "modules": modules}))
        for relative, (width, height) in DIMENSIONS.items():
            path = self.root / (relative + ".png")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(png(width, height))

    def files(self):
        return {path.relative_to(self.root).as_posix(): path.read_bytes() for path in self.root.rglob("*") if path.is_file()}

    def test_apply_copies_exact_nine_aliases_and_second_application_does_not_write(self):
        before = self.files()
        self.assertEqual(DIMENSIONS, marker_dimensions(self.root))
        receipt = apply_to_author(self.root)
        after = self.files()
        expected_aliases = {alias + ".png" for alias in ALIASES.values()}
        self.assertEqual(expected_aliases, set(after) - set(before))
        self.assertEqual({"n.json", "c.json", "n.ini", "c.ini"}, set(receipt["changedFiles"]))
        self.assertEqual(2, receipt["changedScenes"])
        for source, alias in ALIASES.items():
            self.assertEqual(before[source + ".png"], after[alias + ".png"])
        for row in receipt["aliases"]:
            self.assertEqual(hashlib.sha256(before[row["source"]]).hexdigest(), row["sha256"])
        for path in set(before) - {"n.json", "c.json", "n.ini", "c.ini"}:
            self.assertEqual(before[path], after[path], path)
        with mock.patch.object(Path, "write_bytes", side_effect=AssertionError("Idempotent correction attempted a write")):
            second = apply_to_author(self.root)
        self.assertEqual([], second["changedFiles"])
        self.assertEqual(0, second["changedScenes"])
        self.assertTrue(all(not row["created"] for row in second["aliases"]))
        self.assertEqual(after, self.files())

    def test_frame_only_author_has_explicit_empty_noop_and_needs_no_markers(self):
        catalog_path = self.root / "gameplay/content.json"
        catalog_path.write_bytes(encoded({"contract": "oms-gameplay-skin-content.v1", "modules": []}))
        for source in DIMENSIONS:
            (self.root / (source + ".png")).unlink()
        before = self.files()
        with mock.patch.object(Path, "write_bytes", side_effect=AssertionError("Frame-only author attempted a write")):
            receipt = apply_to_author(self.root)
        self.assertEqual(0, receipt["changedScenes"])
        for key in ("changedFiles", "aliases", "scenes", "inis"):
            self.assertEqual([], receipt[key])
        self.assertEqual(before, self.files())

    def test_note_only_and_cap_only_fixtures_require_only_their_original_markers(self):
        catalog_path = self.root / "gameplay/content.json"
        original_catalog = json.loads(catalog_path.read_bytes())
        original_markers = {source: (self.root / (source + ".png")).read_bytes() for source in DIMENSIONS}
        for module_id, category, alias_count in (("notes", "n", 3), ("charge-notes", "c", 6)):
            with self.subTest(module=module_id):
                catalog = copy.deepcopy(original_catalog)
                catalog["modules"] = [module for module in catalog["modules"] if module["id"] == module_id]
                catalog_path.write_bytes(encoded(catalog))
                sources = [source for source in DIMENSIONS if source.split("/")[2] == category]
                for source, data in original_markers.items():
                    path = self.root / (source + ".png")
                    if source in sources:
                        path.write_bytes(data)
                    elif path.exists():
                        path.unlink()
                self.assertEqual({source: DIMENSIONS[source] for source in sources}, marker_dimensions(self.root, sources))
                receipt = apply_to_author(self.root)
                self.assertEqual(alias_count, len(receipt["aliases"]))
                self.assertEqual(1, receipt["changedScenes"])
                self.assertTrue(all(row["source"].startswith(f"custom/markers/{category}/") for row in receipt["aliases"]))

    def test_conflicting_alias_or_later_malformed_scene_causes_no_partial_author_write(self):
        alias = self.root / (ALIASES["custom/markers/n/white"] + ".png")
        alias.write_bytes(png(52, 15) + b"changed")
        before = self.files()
        with self.assertRaisesRegex(ValueError, "alias conflicts"):
            apply_to_author(self.root)
        self.assertEqual(before, self.files())
        alias.unlink()
        scene = fixture_scene("n")
        scene["templates"][0]["root"]["properties"]["height"] = 0
        (self.root / "n.json").write_bytes(encoded(scene))
        before = self.files()
        with self.assertRaisesRegex(ValueError, "normalization"):
            apply_to_author(self.root)
        self.assertEqual(before, self.files())

    def test_original_ihdr_crc_dimensions_and_contained_contribution_paths_are_checked(self):
        path = self.root / "custom/markers/n/white.png"
        original = path.read_bytes()
        path.write_bytes(original[:29] + b"\0\0\0\0" + original[33:])
        with self.assertRaisesRegex(ValueError, "IHDR"):
            marker_dimensions(self.root)
        path.write_bytes(png(52, 16))
        with self.assertRaisesRegex(ValueError, "dimensions"):
            marker_dimensions(self.root)
        path.write_bytes(original)
        catalog_path = self.root / "gameplay/content.json"
        catalog = json.loads(catalog_path.read_bytes())
        catalog["modules"][0]["choices"][0]["scene"] = "../outside.json"
        catalog_path.write_bytes(encoded(catalog))
        before = self.files()
        with self.assertRaisesRegex(ValueError, "escapes"):
            apply_to_author(self.root)
        self.assertEqual(before, self.files())


if __name__ == "__main__":
    unittest.main()
