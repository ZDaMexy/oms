"""AUTO SCRATCH author tests. Synthetic art/geometry do not assert native parity."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import re
import tempfile
import unittest

from PIL import Image

import lighting_auto_assist as assist


def spec(module="notes", choice=None, size="normal", palette="source"):
    return {"module": module, "choice": choice or ("n00" if module == "notes" else "c00"),
            "size": size, "palette": palette, "manifest": "config/m.json", "scene": "config/s.json"}


def reference(selection):
    packages = {}
    for package in set(assist.source_packages(selection).values()):
        note = selection["module"] == "notes"
        names = ("notes_turn", "notes_auto_turn") if note else assist.CHARGE_TEXTURES
        head_height = {"normal": 24, "long": 27, "short": 21, "veryshort": 18}[selection["size"]]
        assets = {}
        for name in names:
            value = hashlib.sha256((package + name).encode()).hexdigest()
            height = (12 if selection["choice"] == "n11" else 15) if note else (
                head_height + 12 if name.endswith("joint") else 24 if "beam" in name else head_height)
            assets[name] = {"name": name, "package": package, "source": "sprites/" + value + ".png", "sha256": value,
                "rgbaSha256": value, "width": 90, "height": height, "imageRect": [0, 92, 0, height + 2],
                "uvRect": [1, 91, 1, height + 1], "sourceArchiveSha256": "e" * 64,
                "sampler": {"min": "nearest", "mag": "nearest", "wrapS": "clamp", "wrapT": "clamp"}}
        packages[package] = {"assets": assets}
    return {"contract": assist.CONTRACT, "library": "X:/synthetic-auto-scratch-library", "packages": packages}


def fixture(selection):
    original = reference(selection)
    packages = assist.source_packages(selection)
    scene = {"contract": "oms-gameplay-skin-scene.v1",
        "root": {"id": "original.root", "type": "container", "target": {"kind": "global"},
                 "blend": "inherit", "properties": {}, "effects": [], "children": []},
        "templates": [], "instances": [], "variants": [], "bindings": [], "tracks": [], "stateMachines": []}
    manifest = {"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
        "sceneContract": scene["contract"], "eventContract": "oms-gameplay-skin-event.v1",
        "resources": [], "animations": [], "bitmapFonts": []}
    measured = {"resources": {}, "readInputs": []}

    def resource(item):
        identifier = "original.art.t" + item["sha256"][:24]
        if identifier not in measured["resources"]:
            manifest["resources"].append({"id": identifier, "type": "texture", "filter": "nearest",
                                          "path": "art/" + item["sha256"] + ".png"})
            measured["resources"][identifier] = {key: item[key] for key in ("sha256", "rgbaSha256", "width", "height")}
        return identifier

    parts = {"note": assist.NOTE_MARKER} if selection["module"] == "notes" else assist.CHARGE_MARKERS
    for part, marker in parts.items():
        slot = "object.note" if part == "note" else "object.long-note." + part
        package = packages["notes"] if part == "note" else packages["cn"]
        name = "notes_turn" if part == "note" else "notes_bss_" + assist.PART_TEXTURES[part]
        original_art = original["packages"][package]["assets"][name]
        first = resource(original_art)
        identifier = "original." + part
        props = {} if part == "body" else {"height": original_art["height"] * (768 / 1080) / 22.5, "y": -.15802469135802472,
            "anchor": "bottom-left", "origin": "bottom-left"}
        root = {"id": identifier, "type": "sprite", "target": {"kind": "global"}, "slot": slot,
                "resource": first, "blend": "alpha", "properties": props, "effects": [], "children": []}
        scene["templates"].append({"id": identifier + ".t", "root": root})
        scene["instances"].append({"id": identifier + ".i", "template": identifier + ".t",
            "material": {"slot": slot, "resource": marker}})
        scene["bindings"].append({"id": identifier + ".brightness", "target": identifier,
                                 "property": "brightness", "source": "option.lane-objects-brightness"})
        if part != "note":
            cases = {}
            for mode in ("ln", "cn", "hcn"):
                package = packages["hcn"] if mode == "hcn" else packages["cn"]
                assets = original["packages"][package]["assets"]
                cases[mode] = resource(assets[name])
                if part == "body":
                    cases[mode + "-holding"] = resource(assets["notes_bss_beam1"])
                    cases[mode + "-released"] = resource(assets["notes_bss2_beam4"])
            scene["variants"].append(assist._variant(identifier + ".mode", identifier,
                "object.long-note-appearance" if part == "body" else "object.long-note-mode", first, cases))
    # A normal key and mania consumer deliberately share a source resource; they
    # must never become AUTO SCRATCH consumers merely because the pixels match.
    for name in ("key", "mania-scratch"):
        node = copy.deepcopy(scene["templates"][0]["root"])
        node["id"] = "untouched." + name
        scene["templates"].append({"id": node["id"] + ".t", "root": node})
        marker = "custom/markers/n/" + name if selection["module"] == "notes" else "custom/markers/c/" + name + "-head"
        scene["instances"].append({"id": node["id"] + ".i", "template": node["id"] + ".t",
            "material": {"slot": node["slot"], "resource": marker}})
    return manifest, scene, original, measured


def transform(selection):
    manifest, scene, original, measured = fixture(selection)
    return assist.transform_contribution(manifest, scene, selection, original, measured)


def variant_value(variant, value):
    field = "resource" if variant["property"] == "resource" else "value"
    return next((case[field] for case in variant["cases"] if case["key"] == value), variant["default"])


def prepare_material_tree(scene):
    """Independent data-flow model of prepareNode's exact owner/target rules.

    This visits hidden branches as C# preparation does. The synthetic native
    target is supplied by each material instance and overrides every raw global
    source target through the whole tree, rather than getting re-resolved by a
    child's texture or its visibility variant.
    """
    templates = {template["id"]: template["root"] for template in scene["templates"]}
    prepared = []
    for instance in scene["instances"]:
        slot = instance["material"]["slot"]
        native_target = ("synthetic-native-material-target", instance["id"])
        entry = object()

        def prepare(node, inherited_owner, inherited_target, parent=None):
            target = native_target
            if node.get("slot") is not None:
                if inherited_owner is not None:
                    raise ValueError("prepareNode: nested SlotId is not an inherited owner")
                if node["slot"] != slot:
                    raise ValueError("prepareNode: unrelated selected Provide owner")
                owner = entry
            else:
                if inherited_owner is None or inherited_target != target:
                    raise ValueError("prepareNode: slotless programmed/visible node has no exact owner")
                owner = inherited_owner
            if owner is not entry:
                raise ValueError("prepareNode: package/material ownership crossed")
            prepared.append({"node": node["id"], "parent": parent, "owner": owner, "target": target,
                             "entry": entry, "nativeTarget": native_target})
            for child in node["children"]:
                prepare(child, owner, target, node["id"])

        prepare(templates[instance["template"]], None, None)
    return prepared


def hcn01_long_fixture():
    selection = spec("charge-notes", choice="c01", size="long")
    manifest, scene, source, measured = fixture(selection)
    hcn = assist.source_packages(selection)["hcn"]
    for name, height in (("notes_bss_auto_start", 24), ("notes_bss_auto_end", 24), ("notes_bss_auto_joint", 36)):
        item = source["packages"][hcn]["assets"][name]
        item.update({"height": height, "uvRect": [1, 91, 1, height + 1], "imageRect": [0, 92, 0, height + 2]})
    return selection, manifest, scene, source, measured


def catalogue():
    modules = []
    for module, prefix, count in (("notes", "n", 12), ("charge-notes", "c", 11)):
        choices = []
        for index in range(count):
            choice = {"value": prefix + f"{index:02}", "manifest": "config/m.json", "scene": "config/s.json", "variants": []}
            for size in assist.SIZES:
                for palette in ("source",) if module == "notes" else assist.PALETTES:
                    if (size, palette) == ("normal", "source"):
                        continue
                    when = {"note-size": size} if module == "notes" else {"charge-size": size, "hcn-color": palette}
                    choice["variants"].append({"when": when, "manifest": "config/m.json", "scene": "config/s.json"})
            choices.append(choice)
        modules.append({"id": module, "choices": choices})
    return {"modules": modules}


class AutoScratchTransformTest(unittest.TestCase):
    def test_enumerates_all_136_actual_size_and_palette_consumers(self):
        specs = assist.contribution_specs(catalogue())
        self.assertEqual(48, sum(row["module"] == "notes" for row in specs))
        self.assertEqual(88, sum(row["module"] == "charge-notes" for row in specs))
        self.assertEqual(136, len(specs))

    def test_missing_size_is_not_a_silent_fallback(self):
        value = catalogue()
        value["modules"][0]["choices"][0]["variants"].pop()
        with self.assertRaisesRegex(ValueError, "missing"):
            assist.contribution_specs(value)

    def test_note_resource_variant_only_replaces_real_scratch_assist(self):
        selection = spec()
        manifest, scene, source, measured = fixture(selection)
        _, changed, _ = assist.transform_contribution(manifest, scene, selection, source, measured)
        self.assertEqual(scene["templates"], changed["templates"])
        self.assertEqual(scene["bindings"], changed["bindings"])
        self.assertEqual(scene["instances"], changed["instances"])
        variant, = changed["variants"]
        self.assertEqual(assist.SOURCE, variant["source"])
        self.assertEqual(scene["templates"][0]["root"]["resource"], variant_value(variant, "none"))
        self.assertEqual(variant_value(variant, "none"), variant_value(variant, "note"))
        self.assertNotEqual(variant_value(variant, "none"), variant_value(variant, "scratch"))

    def test_conversion_does_not_mutate_inputs_or_manifest_animation_declarations(self):
        selection = spec("charge-notes")
        manifest, scene, source, measured = fixture(selection)
        manifest["animations"].append({"id": "unrelated.animation", "path": "unrelated.json.br", "resources": []})
        scene["tracks"].append({"id": "unrelated.track", "type": "tween", "target": "untouched.key", "property": "y",
            "easing": "linear", "loop": True, "keyframes": [{"id": "unrelated.frame", "time": 0, "value": 0},
                {"id": "unrelated.last", "time": 100, "value": .1}]})
        before = copy.deepcopy((manifest, scene, source, measured))
        new_manifest, changed, _ = assist.transform_contribution(manifest, scene, selection, source, measured)
        self.assertEqual(before, (manifest, scene, source, measured))
        self.assertEqual(manifest["animations"], new_manifest["animations"])
        self.assertEqual(scene["tracks"], changed["tracks"])
        self.assertEqual(scene["stateMachines"], changed["stateMachines"])

    def test_notes11_keeps_explicit_existing_geometry_adaptation_for_every_size(self):
        for size in assist.SIZES:
            selection = spec(choice="n11", size=size)
            self.assertTrue(assist.source_packages(selection)["notes"].endswith("notes11.ifs"))
            manifest, scene, source, measured = fixture(selection)
            scene["templates"][0]["root"]["properties"]["height"] = 1.2345
            _, changed, receipt = assist.transform_contribution(manifest, scene, selection, source, measured)
            self.assertEqual(scene["templates"], changed["templates"])
            self.assertEqual(1.2345, receipt["consumers"][0]["geometry"]["height"])
            self.assertEqual(12, receipt["originalArt"][0]["height"])

    def test_charge_normal_nodes_modes_geometry_and_c4_materials_remain_exact(self):
        selection = spec("charge-notes")
        manifest, scene, source, measured = fixture(selection)
        _, changed, _ = assist.transform_contribution(manifest, scene, selection, source, measured)
        self.assertEqual(scene["instances"], changed["instances"])
        self.assertEqual(scene["variants"], changed["variants"][:len(scene["variants"])])
        self.assertEqual(scene["bindings"], changed["bindings"][:len(scene["bindings"])])
        for index in range(3):
            root = changed["templates"][index]["root"]
            self.assertEqual({"width": 1, "height": 1}, root["properties"])
            self.assertEqual({key: value for key, value in scene["templates"][index]["root"].items() if key != "slot"}, root["children"][0])
            self.assertEqual(root["children"][0]["properties"], root["children"][1]["properties"])
        self.assertEqual(scene["templates"][3:], changed["templates"][3:])

    def test_charge_none_and_note_show_original_but_scratch_shows_auto(self):
        _, changed, _ = transform(spec("charge-notes"))
        for index in range(3):
            normal = next(v for v in changed["variants"] if v["id"] == f"as.h{index}.normal")
            automatic = next(v for v in changed["variants"] if v["id"] == f"as.h{index}.enabled")
            for value in ("none", "note"):
                self.assertTrue(variant_value(normal, value))
                self.assertFalse(variant_value(automatic, value))
            self.assertFalse(variant_value(normal, "scratch"))
            self.assertTrue(variant_value(automatic, "scratch"))

    def test_original_hcn_auto_source_is_preserved_when_its_png_differs(self):
        _, scene, receipt = transform(spec("charge-notes"))
        mode = next(v for v in scene["variants"] if v["id"] == "as.h0.mode")
        self.assertNotEqual(variant_value(mode, "cn"), variant_value(mode, "hcn"))
        self.assertEqual(variant_value(mode, "cn"), variant_value(mode, "ln"))
        self.assertIn("notes_hcn00.ifs", receipt["sourcePackages"]["hcn"])

    def test_cn_palette_uses_its_explicit_cn_source_for_all_actual_hcn_states(self):
        _, scene, receipt = transform(spec("charge-notes", palette="cn-palette"))
        self.assertEqual(receipt["sourcePackages"]["cn"], receipt["sourcePackages"]["hcn"])
        self.assertFalse(any(v["id"].endswith(".mode") and v["id"].startswith("as.") for v in scene["variants"]))
        for consumer in receipt["consumers"]:
            self.assertEqual(1, len(set(consumer["autoResources"].values())))

    def test_body_keeps_stretching_auto_beam0_and_joint_has_no_invented_consumer(self):
        _, scene, receipt = transform(spec("charge-notes", size="long"))
        body = scene["templates"][1]["root"]["children"][1]
        self.assertEqual({}, body["properties"])
        self.assertTrue(all(item["name"] != "notes_bss_auto_joint" for item in receipt["originalArt"]))
        body_sources = [item for item in receipt["originalArt"] if item["name"] == "notes_bss_auto_beam0"]
        self.assertTrue(body_sources)
        self.assertTrue(all(item["height"] == 24 for item in body_sources))
        self.assertTrue(all(item["height"] == 39 for item in receipt["joint"].values()))

    def test_actual_hcn01_long_auto_caps_keep_24px_while_cn_and_ordinary_hcn_keep_27px(self):
        selection, manifest, scene, source, measured = hcn01_long_fixture()
        hcn = assist.source_packages(selection)["hcn"]
        _, changed, receipt = assist.transform_contribution(manifest, scene, selection, source, measured)
        scale = (768 / 1080) / 22.5
        for index in (0, 2):
            root = changed["templates"][index]["root"]
            ordinary, auto_gate = root["children"]
            cn_cap, hcn_cap = auto_gate["children"]
            self.assertEqual({key: value for key, value in scene["templates"][index]["root"].items() if key != "slot"}, ordinary)
            self.assertAlmostEqual(27 * scale, ordinary["properties"]["height"])
            self.assertAlmostEqual(27 * scale, cn_cap["properties"]["height"])
            self.assertAlmostEqual(24 * scale, hcn_cap["properties"]["height"])
            for key in ("y", "anchor", "origin"):
                self.assertEqual(ordinary["properties"][key], hcn_cap["properties"][key])
            self.assertEqual(24 / 27, receipt["consumers"][index]["heightScales"]["hcn"])
            cn_visibility = next(v for v in changed["variants"] if v["target"] == cn_cap["id"])
            hcn_visibility = next(v for v in changed["variants"] if v["target"] == hcn_cap["id"])
            for mode in ("ln", "cn"):
                self.assertTrue(variant_value(cn_visibility, mode))
                self.assertFalse(variant_value(hcn_visibility, mode))
            self.assertFalse(variant_value(cn_visibility, "hcn"))
            self.assertTrue(variant_value(hcn_visibility, "hcn"))
        self.assertEqual("sprite", changed["templates"][1]["root"]["children"][1]["type"])
        self.assertEqual({}, receipt["consumers"][1]["autoGeometry"]["hcn"])
        self.assertEqual(36, receipt["joint"][hcn]["height"])
        # The explicit same-style CN palette still uses its real 27px CN PNG.
        _, palette_scene, palette_receipt = transform(spec("charge-notes", choice="c01", size="long", palette="cn-palette"))
        self.assertEqual("sprite", palette_scene["templates"][0]["root"]["children"][1]["type"])
        self.assertEqual(1, palette_receipt["consumers"][0]["heightScales"]["hcn"])

    def test_every_owner_edge_including_hidden_auto_and_hcn_size_gates_inherits_exact_target(self):
        normal = spec("charge-notes")
        cases = [(normal, *fixture(normal)), hcn01_long_fixture()]
        for selection, manifest, scene, source, measured in cases:
            _, changed, receipt = assist.transform_contribution(manifest, scene, selection, source, measured)
            expected_edges = 10 if selection["choice"] == "c01" else 6
            self.assertEqual(expected_edges, len(receipt["materialOwnership"]["edges"]))
            for assist_value in ("none", "note", "scratch"):
                for mode in ("ln", "cn", "hcn"):
                    frame = copy.deepcopy(changed)
                    nodes = {node["id"]: node for node in assist._walk(frame) if node.get("type") in ("sprite", "container")}
                    for variant in frame["variants"]:
                        if variant["property"] == "visible":
                            value = assist_value if variant["source"] == assist.SOURCE else mode
                            nodes[variant["target"]]["properties"]["visible"] = variant_value(variant, value)
                    prepared = prepare_material_tree(frame)
                    by_id = {entry["node"]: entry for entry in prepared}
                    edges = [entry for entry in prepared if entry["parent"] is not None]
                    self.assertEqual(expected_edges, len(edges))
                    for entry in edges:
                        parent = by_id[entry["parent"]]
                        self.assertIs(parent["owner"], entry["owner"])
                        self.assertIs(entry["entry"], entry["owner"])
                        self.assertEqual(entry["nativeTarget"], entry["target"])
                        self.assertEqual(parent["target"], entry["target"])
                    if selection["choice"] == "c01":
                        self.assertIn("as.h0.auto.hcn", by_id)
                        self.assertIn("as.h2.auto.cn", by_id)

    def test_previous_nested_slot_failure_is_rejected_even_in_a_hidden_hcn_branch(self):
        selection, manifest, scene, source, measured = hcn01_long_fixture()
        _, changed, _ = assist.transform_contribution(manifest, scene, selection, source, measured)
        bad = copy.deepcopy(changed)
        host = bad["templates"][0]["root"]
        cap = host["children"][1]["children"][1]
        cap["properties"]["visible"] = False
        cap["slot"] = host["slot"]
        with self.assertRaisesRegex(ValueError, "nested SlotId"):
            prepare_material_tree(bad)
        with self.assertRaisesRegex(ValueError, "descendant SlotId"):
            assist.material_ownership(bad)

    def test_other_package_canvas_mismatch_cannot_use_the_hcn01_long_exception(self):
        selection = spec("charge-notes", size="long")
        manifest, scene, source, measured = fixture(selection)
        hcn = assist.source_packages(selection)["hcn"]
        source["packages"][hcn]["assets"]["notes_bss_auto_start"]["height"] = 24
        with self.assertRaisesRegex(ValueError, "no explicit original source exception"):
            assist.transform_contribution(manifest, scene, selection, source, measured)

    def test_identical_original_auto_pngs_share_resources_without_merging_mode_meaning(self):
        selection = spec("charge-notes")
        manifest, scene, source, measured = fixture(selection)
        packages = assist.source_packages(selection)
        for ending in assist.PART_TEXTURES.values():
            name = "notes_bss_auto_" + ending
            cn = source["packages"][packages["cn"]]["assets"][name]
            hcn = source["packages"][packages["hcn"]]["assets"][name]
            hcn.update({key: cn[key] for key in ("sha256", "rgbaSha256")})
        _, _, receipt = assist.transform_contribution(manifest, scene, selection, source, measured)
        self.assertEqual(3, receipt["addedResources"])
        self.assertEqual(3, len(receipt["newFiles"]))
        self.assertEqual(9, len(receipt["originalArt"]))

    def test_real_source_canvas_mismatch_fails_before_replacing_any_art(self):
        selection = spec()
        manifest, scene, source, measured = fixture(selection)
        original = scene["templates"][0]["root"]["resource"]
        measured["resources"][original]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "exact source package"):
            assist.transform_contribution(manifest, scene, selection, source, measured)

    def test_absent_auto_source_is_a_visible_failure_not_normal_art_fallback(self):
        selection = spec()
        manifest, scene, source, measured = fixture(selection)
        package = assist.source_packages(selection)["notes"]
        del source["packages"][package]["assets"]["notes_auto_turn"]
        with self.assertRaisesRegex(ValueError, "absent"):
            assist.transform_contribution(manifest, scene, selection, source, measured)

    def test_incomplete_normal_hcn_state_cases_are_not_silently_flattened(self):
        selection = spec("charge-notes")
        manifest, scene, source, measured = fixture(selection)
        scene["variants"][1]["cases"].pop()
        with self.assertRaisesRegex(ValueError, "cases changed"):
            assist.transform_contribution(manifest, scene, selection, source, measured)

    def test_declared_id_collision_and_double_application_fail(self):
        selection = spec("charge-notes")
        manifest, scene, source, measured = fixture(selection)
        scene["root"]["id"] = "as.h0.host"
        with self.assertRaisesRegex(ValueError, "Duplicate scene declaration"):
            assist.transform_contribution(manifest, scene, selection, source, measured)
        selection = spec()
        manifest, scene, source, measured = fixture(selection)
        new_manifest, changed, _ = assist.transform_contribution(manifest, scene, selection, source, measured)
        with self.assertRaisesRegex(ValueError, "already attached"):
            assist.transform_contribution(new_manifest, changed, selection, source, measured)

    def test_every_new_declaration_uses_the_actual_public_stable_id_rule(self):
        segment = r"[a-z](?:[a-z0-9-]*[a-z0-9])?"
        valid = re.compile(segment + r"(?:\." + segment + r")*")
        for selection in (spec(), spec("charge-notes")):
            manifest, scene, receipt = transform(selection)
            for item in assist._walk([manifest, scene]):
                if "id" in item:
                    self.assertLessEqual(len(item["id"]), 128)
                    self.assertIsNotNone(valid.fullmatch(item["id"]), item["id"])
            self.assertEqual(0, receipt["fontsAdded"])
            self.assertEqual(0, receipt["optionsAdded"])
            self.assertFalse(receipt["nativeHiddenAndTintContainerChanged"])
            self.assertFalse(receipt["autoNoteArtInvented"])


class AutoScratchSourceBoundaryTest(unittest.TestCase):
    def make_library(self, root):
        package = "contents/data/graphic/notes11.ifs"
        rows = []
        for index, name in enumerate(("notes_turn", "notes_auto_turn")):
            path = root / (name + ".png")
            Image.new("RGBA", (90, 12), (30 + index * 20, 80, 100, 255)).save(path)
            with Image.open(path) as image:
                rgba = hashlib.sha256(image.convert("RGBA").tobytes()).hexdigest()
            rows.append({"package": package, "name": name + ".png", "sprite": path.name,
                "sha256": assist.digest(path.read_bytes()), "rgbaSha256": rgba, "width": 90, "height": 12,
                "imageRect": [0, 92, 0, 14], "uvRect": [1, 91, 1, 13],
                "sampler": {"min": "nearest", "mag": "nearest", "wrapS": "clamp", "wrapT": "clamp"}})
        (root / "package.json").write_text(json.dumps({"movies": [], "geometry": [], "unknownPlacementCount": 0}), encoding="utf-8")
        metadata = {"status": "complete", "sourceMetadataUnchanged": True,
            "packages": [{"source": package, "sha256": "a" * 64, "authorData": "package.json"}], "textures": rows}
        (root / "library.json").write_text(json.dumps(metadata), encoding="utf-8")

    def test_exact_original_bytes_pixels_uv_and_notes11_reuse_are_read_only(self):
        with tempfile.TemporaryDirectory(prefix="auto-scratch-source-") as folder:
            root = Path(folder)
            self.make_library(root)
            before = {path.name: path.read_bytes() for path in root.iterdir()}
            result = assist.source_reference(root, [spec(choice="n11", size="long")])
            self.assertEqual(before, {path.name: path.read_bytes() for path in root.iterdir()})
            self.assertEqual(1, len(result["packages"]))
            self.assertFalse(result["originalPixelsChanged"])
            self.assertFalse(result["originalHddAccessed"])
            for package in result["packages"].values():
                self.assertEqual({"notes_turn", "notes_auto_turn"}, set(package["assets"]))
                self.assertEqual([1, 91, 1, 13], package["assets"]["notes_auto_turn"]["uvRect"])

    def test_missing_original_discrete_size_does_not_use_the_normal_package(self):
        with tempfile.TemporaryDirectory(prefix="auto-scratch-source-") as folder:
            root = Path(folder)
            self.make_library(root)
            with self.assertRaisesRegex(ValueError, "Missing original source package"):
                assist.source_reference(root, [spec(choice="n00", size="long")])

    def test_tampered_png_fails_the_hash_receipt(self):
        with tempfile.TemporaryDirectory(prefix="auto-scratch-source-") as folder:
            root = Path(folder)
            self.make_library(root)
            Image.new("RGBA", (90, 12), (200, 0, 0, 255)).save(root / "notes_auto_turn.png")
            with self.assertRaisesRegex(ValueError, "PNG changed"):
                assist.source_reference(root, [spec(choice="n11")])

    def test_bad_uv_receipt_is_not_accepted_as_original_geometry(self):
        with tempfile.TemporaryDirectory(prefix="auto-scratch-source-") as folder:
            root = Path(folder)
            self.make_library(root)
            path = root / "library.json"
            metadata = json.loads(path.read_text(encoding="utf-8"))
            metadata["textures"][1]["uvRect"] = [1, 90, 1, 13]
            path.write_text(json.dumps(metadata), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "UV dimensions"):
                assist.source_reference(root, [spec(choice="n11")])

    def test_hcn01_long_exception_receipt_retains_measured_hash_uv_and_discrete_canvas(self):
        selection = spec("charge-notes", choice="c01", size="long")
        with tempfile.TemporaryDirectory(prefix="auto-scratch-hcn01-long-") as folder:
            root = Path(folder)
            rows, packages = [], []
            for channel, package in assist.source_packages(selection).items():
                metadata_path = root / (channel + ".json")
                metadata_path.write_text(json.dumps({"movies": [], "geometry": [], "unknownPlacementCount": 0}), encoding="utf-8")
                packages.append({"source": package, "sha256": "b" * 64, "authorData": metadata_path.name})
                for index, name in enumerate(assist.CHARGE_TEXTURES):
                    height = 39 if name.endswith("joint") else 24 if "beam" in name else 27
                    if channel == "hcn" and name in ("notes_bss_auto_start", "notes_bss_auto_end", "notes_bss_auto_joint"):
                        height = 36 if name.endswith("joint") else 24
                    path = root / (channel + "-" + name + ".png")
                    Image.new("RGBA", (90, height), (20 + index * 15, 80, 100, 255)).save(path)
                    with Image.open(path) as image:
                        pixels = assist.digest(image.convert("RGBA").tobytes())
                    rows.append({"package": package, "name": name + ".png", "sprite": path.name,
                        "sha256": assist.digest(path.read_bytes()), "rgbaSha256": pixels, "width": 90, "height": height,
                        "imageRect": [92, 184, 100, 102 + height], "uvRect": [93, 183, 101, 101 + height],
                        "sampler": {"min": "nearest", "mag": "nearest", "wrapS": "clamp", "wrapT": "clamp"}})
            (root / "library.json").write_text(json.dumps({"status": "complete", "sourceMetadataUnchanged": True,
                "packages": packages, "textures": rows}), encoding="utf-8")
            result = assist.source_reference(root, [selection])
            hcn_assets = result["packages"][assist.source_packages(selection)["hcn"]]["assets"]
            self.assertEqual(27, hcn_assets["notes_bss_start"]["height"])
            self.assertNotIn("canvasException", hcn_assets["notes_bss_start"])
            for name, actual_height, standard_height in (("notes_bss_auto_start", 24, 27),
                    ("notes_bss_auto_end", 24, 27), ("notes_bss_auto_joint", 36, 39)):
                item = hcn_assets[name]
                self.assertEqual([90, actual_height], item["canvasException"]["actualSourceCanvas"])
                self.assertEqual([90, standard_height], item["canvasException"]["standardSizeCanvas"])
                self.assertFalse(item["canvasException"]["pixelsResampled"])
                self.assertEqual([93, 183, 101, 101 + actual_height], item["uvRect"])
                self.assertEqual(assist.digest((root / item["source"]).read_bytes()), item["sha256"])


if __name__ == "__main__":
    unittest.main()
