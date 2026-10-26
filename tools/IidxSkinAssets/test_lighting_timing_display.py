"""Synthetic timing author tests; fixture offsets are not native A/B/C measurements."""
from __future__ import annotations

import copy
import hashlib
import itertools
import json
from pathlib import Path
import re
import tempfile
import unittest

from PIL import Image

import lighting_timing_display as timing
from build_lighting_timing_display import upgrade


def positions():
    return {"contract": timing.POSITION_CONTRACT, "coordinateSpace": timing.POSITION_SPACE,
        "captures": [{"path": "synthetic-only.png", "sha256": "a" * 64}],
        "layoutBasis": {"p1": "synthetic-test-fixture-not-native"},
        "positions": {"p1": {
            "a": {"combined": [0, -40], "keys": [-24, -40], "scratch": [24, -40]},
            "b": {"combined": [240, 0], "keys": [240, -10], "scratch": [240, 14]},
            "c": {"combined": [240, 110], "keys": [240, 100], "scratch": [240, 124]}}}}


def judgement_scene():
    scene = timing.empty_scene("original.root")
    for index, layout in enumerate(timing.LAYOUTS):
        root, gate = "j.root" + str(index), "j.gate" + str(index)
        rows = [timing._node(root + ".row" + str(row), properties={"x": .49 if layout == "p1" else .48 if layout == "p2" else .5,
            "y": .2, "origin": "centre", "layout": "horizontal"}) for row in range(2)]
        node = timing._node(root, properties={"opacity": 0}, slot="hud.judgement",
            children=[timing._node(gate, properties={"width": 1, "height": 1}, children=rows)])
        scene["templates"].append({"id": root + ".t", "root": node})
        scene["instances"].append({"id": root + ".i", "template": root + ".t",
            "material": {"slot": "hud.judgement", "resource": timing.MATERIALS[layout]}})
        scene["bindings"].append({"id": root + ".brightness", "target": root, "property": "brightness", "source": "option.lane-objects-brightness"})
        for suffix, target in (("r", root), ("g", gate)):
            identifier = root + ".position." + suffix
            scene["tracks"].append({"id": identifier, "type": "tween", "target": target, "property": "y", "easing": "linear",
                "loop": False, "clock": "option.judgement-position", "keyframes": [
                    {"id": identifier + ".k" + str(i), "time": value, "value": delta}
                    for i, (value, delta) in enumerate(((-2, .5), (0, 0), (1, -.25), (10, -2.5)))]})
    return scene


def geometry():
    return timing.judgement_geometry(judgement_scene(), {layout: (440, 87.36) for layout in timing.LAYOUTS})


def assets():
    reference = {"contract": timing.SOURCE_CONTRACT, "assets": {
        name: {"sha256": str(index + 1) * 64} for index, name in enumerate(timing.ASSETS)}}
    return timing.resource_assets(reference)


def walk(node):
    yield node
    for child in node.get("children", []):
        yield from walk(child)


def visible(scene, layout, values):
    variants = {(item["target"], item["property"]): item for item in scene["variants"]}
    output = []
    def visit(node, parent=True):
        shown = node["properties"].get("visible", True)
        variant = variants.get((node["id"], "visible"))
        if variant:
            shown = next((case["value"] for case in variant["cases"] if case["key"] == values.get(variant["source"])), variant["default"])
        if not (parent and shown):
            return
        if node["type"] == "sprite":
            variant = variants[(node["id"], "resource")]
            output.append(next((case["resource"] for case in variant["cases"] if case["key"] == values.get(variant["source"])), variant["default"]))
        for child in node["children"]:
            visit(child)
    for instance in scene["instances"]:
        if instance["material"]["resource"] == timing.MATERIALS[layout]:
            root, = [item["root"] for item in scene["templates"] if item["id"] == instance["template"]]
            visit(root)
    return output


class TimingChoiceTest(unittest.TestCase):
    def test_every_generated_declaration_obeys_the_actual_public_ascii_segment_and_length_rule(self):
        # Exact equivalent of GameplaySkinStableIdentityId.cs, including no trailing
        # hyphen, plus the SceneCodec's MAX_STABLE_ID_LENGTH=128 restriction.
        segment = r"[a-z](?:[a-z0-9-]*[a-z0-9])?"
        expression = re.compile(segment + r"(?:\." + segment + r")*")
        def visit(value):
            if isinstance(value, dict):
                if "id" in value:
                    identifier = value["id"]
                    self.assertLessEqual(len(identifier), 128)
                    self.assertIsNotNone(expression.fullmatch(identifier), identifier)
                for child in value.values():
                    visit(child)
            elif isinstance(value, list):
                for child in value:
                    visit(child)
        for choice in timing.CHOICES:
            contribution = timing.build_choice(choice, geometry(), positions(), assets())
            visit(contribution)
            self.assertGreater(timing.audit_identifiers(contribution)["declaredIds"], 0)

    def test_preflight_rejects_the_failed_numeric_segments_in_all_declaration_and_reference_roles(self):
        contribution = timing.build_choice("a-separated", geometry(), positions(), assets())
        mutations = (
            lambda doc: doc["scene"]["templates"][0]["root"]["children"][0]["children"][0].__setitem__("id", "td.0.keys"),
            lambda doc: doc["scene"]["tracks"][0].__setitem__("id", "td.p0.0"),
            lambda doc: doc["scene"]["variants"][0]["cases"][0].__setitem__("id", "td.0.case"),
            lambda doc: doc["scene"]["instances"][0].__setitem__("id", "0instance"),
            lambda doc: doc["manifest"]["resources"][0].__setitem__("id", "td.0.texture"),
            lambda doc: doc["scene"]["tracks"][0].__setitem__("target", "td.0.keys"),
            lambda doc: doc["scene"]["stateMachines"].append({"id": "td.machine", "initial": "td.0.state", "states": [], "transitions": []}),
        )
        for mutation in mutations:
            invalid = copy.deepcopy(contribution)
            mutation(invalid)
            with self.assertRaises(ValueError):
                timing.audit_identifiers(invalid)

    def test_public_validator_is_not_relaxed_for_uppercase_underscores_trailing_hyphens_or_length(self):
        for identifier in ("td.h0.Key", "td.h0.key_name", "td.h0.key-", "td..keys", "td.p0.0", "a" * 129):
            invalid = timing.build_choice("off", {}, {}, {})
            invalid["scene"]["root"]["id"] = identifier
            with self.subTest(identifier=identifier), self.assertRaises(ValueError):
                timing.audit_identifiers(invalid)

    def test_seven_existing_c2_choices_default_a_and_bms_scope_preserve_proper_names(self):
        module = timing.module_definition()
        self.assertEqual((module["id"], module["default"], module["rulesets"]), ("timing-display", "a", ["bms"]))
        self.assertEqual(tuple(item["value"] for item in module["choices"]), timing.CHOICES)
        self.assertIn("FAST/SLOW", module["label"])
        for choice in module["choices"][1:]:
            self.assertIn("Position " + choice["value"][0].upper(), choice["label"])
            self.assertEqual("Separate Display" in choice["label"], choice["value"].endswith("-separated"))

    def test_off_adds_no_timing_nodes_templates_bindings_tracks_resources_or_fonts(self):
        contribution = timing.build_choice("off", {}, {}, {})
        scene, manifest = contribution["scene"], contribution["manifest"]
        self.assertEqual(scene["root"]["children"], [])
        for key in ("templates", "instances", "bindings", "tracks", "variants", "stateMachines"):
            self.assertEqual(scene[key], [])
        self.assertEqual(manifest["resources"], [])
        self.assertNotIn("bitmapFonts", manifest)

    def test_combined_uses_only_current_exact_group_direction_and_two_original_images(self):
        resources = assets()
        for choice in timing.POSITIONS:
            contribution = timing.build_choice(choice, geometry(), positions(), resources)
            scene = contribution["scene"]
            self.assertEqual({item["id"] for item in contribution["manifest"]["resources"]}, {"td.f", "td.s"})
            for layout, direction, keys, scratch in itertools.product(timing.LAYOUTS, timing.DIRECTIONS, timing.DIRECTIONS, timing.DIRECTIONS):
                values = {"layout.ruleset": "bms", timing.DIRECTION_SOURCE: direction, timing.KEYS_SOURCE: keys, timing.SCRATCH_SOURCE: scratch,
                    "judgement.offset": -1000, "judgement.count.fast": 100, "judgement.count.slow": 200}
                self.assertEqual(visible(scene, layout, values), [] if direction == "none" else ["td.f" if direction == "fast" else "td.s"])

    def test_separate_keys_and_scratch_keep_independent_current_directions_and_s_art(self):
        for position in timing.POSITIONS:
            scene = timing.build_choice(position + "-separated", geometry(), positions(), assets())["scene"]
            for layout, keys, scratch in itertools.product(timing.LAYOUTS, timing.DIRECTIONS, timing.DIRECTIONS):
                values = {"layout.ruleset": "bms", timing.DIRECTION_SOURCE: "slow", timing.KEYS_SOURCE: keys, timing.SCRATCH_SOURCE: scratch}
                expected = ([] if keys == "none" else ["td.f" if keys == "fast" else "td.s"])
                expected += [] if scratch == "none" else ["td.sf" if scratch == "fast" else "td.ss"]
                self.assertEqual(visible(scene, layout, values), expected)

    def test_no_offset_counter_or_unscoped_first_input_proxy_and_no_mania_leak(self):
        for choice in timing.CHOICES[1:]:
            contribution = timing.build_choice(choice, geometry(), positions(), assets())
            scene = contribution["scene"]
            self.assertEqual({item["source"] for item in scene["bindings"]}, {"option.lane-objects-brightness"})
            self.assertTrue({item["source"] for item in scene["variants"]}.issubset(
                {"layout.ruleset", "layout.group", timing.DIRECTION_SOURCE, timing.KEYS_SOURCE, timing.SCRATCH_SOURCE}))
            self.assertEqual({item["clock"] for item in scene["tracks"]}, {"option.judgement-position"})
            values = {"layout.ruleset": "mania", timing.DIRECTION_SOURCE: "fast", timing.KEYS_SOURCE: "fast", timing.SCRATCH_SOURCE: "slow"}
            self.assertTrue(all(visible(scene, layout, values) == [] for layout in timing.LAYOUTS))
            self.assertNotIn("bitmapFonts", contribution["manifest"])

    def test_exact_material_group_owners_and_position_brightness_projection_stay_intact(self):
        original = judgement_scene()
        saved = copy.deepcopy(original)
        host = timing.judgement_geometry(original, {layout: (440, 87.36) for layout in timing.LAYOUTS})
        saved_host = copy.deepcopy(host)
        contribution = timing.build_choice("a-separated", host, positions(), assets())
        scene = contribution["scene"]
        self.assertEqual(original, saved)
        self.assertEqual(host, saved_host)
        self.assertEqual([item["material"] for item in scene["instances"]],
                         [{"slot": "hud.judgement", "resource": timing.MATERIALS[layout]} for layout in timing.LAYOUTS])
        for index, layout in enumerate(timing.LAYOUTS):
            for layer in range(2):
                old = host[layout]["positionTracks"][layer]
                new = scene["tracks"][index * 2 + layer]
                self.assertEqual([(item["time"], item["value"]) for item in new["keyframes"]],
                                 [(item["time"], item["value"]) for item in old["keyframes"]])
        self.assertEqual(len(scene["tracks"]), 6)
        self.assertEqual(scene["stateMachines"], [])

    def test_natural_png_pixels_use_one_horizontal_leaf_per_channel(self):
        scene = timing.build_choice("c-separated", geometry(), positions(), assets())["scene"]
        for template in scene["templates"]:
            for row in template["root"]["children"][0]["children"]:
                self.assertEqual(row["properties"]["layout"], "horizontal")
                self.assertEqual(len(row["children"]), 1)
                sprite = row["children"][0]
                self.assertEqual(sprite["type"], "sprite")
                self.assertEqual(sprite["properties"]["height-px"], 22 * timing.SCALE)

    def test_new_selected_contribution_respects_existing_small_manifest_and_scene_headroom(self):
        contribution = timing.build_choice("c-separated", geometry(), positions(), assets())
        manifest_size = len(json.dumps(contribution["manifest"], ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        scene_size = len(json.dumps(contribution["scene"], ensure_ascii=False, separators=(",", ":")).encode("utf-8"))
        self.assertLess(64323 + manifest_size, 65536)
        self.assertLess(503213 + scene_size + 2048, 524288)


class TimingGeometryEvidenceTest(unittest.TestCase):
    def test_sp_reference_is_enough_with_explicit_p2_and_exact_group_dp_mirrors(self):
        supplied = positions()
        saved = copy.deepcopy(supplied)
        expanded = timing.reviewed_positions(supplied)
        self.assertEqual(supplied, saved)
        for position in timing.POSITIONS:
            for channel in ("combined", "keys", "scratch"):
                x, y = supplied["positions"]["p1"][position][channel]
                self.assertEqual(expanded["positions"]["p2"][position][channel], [-x, y])
                self.assertEqual(expanded["positions"]["dp"][position][channel], [x, y])
        self.assertIn("oms-", expanded["layoutBasis"]["p2"])
        self.assertIn("oms-", expanded["layoutBasis"]["dp"])
        self.assertEqual(timing.reviewed_positions(expanded), expanded)

    def test_dp_second_group_mirrors_current_c3_host_centre_without_changing_direction_scope(self):
        scene = timing.build_choice("b-separated", geometry(), positions(), assets())["scene"]
        mappings = [item for item in scene["variants"] if item["source"] == "layout.group"]
        self.assertEqual(len(mappings), 2)
        for item in mappings:
            self.assertEqual(item["property"], "x")
            self.assertEqual(item["cases"][0]["key"], "bms.group.deck-2")
            self.assertAlmostEqual(item["default"] + item["cases"][0]["value"], 1)

    def test_menu_preview_is_not_promoted_to_native_gameplay_or_a_new_dp_requirement(self):
        supplied = positions()
        supplied["layoutBasis"]["p1"] = "native-menu-preview"
        expanded = timing.reviewed_positions(supplied)
        self.assertEqual(expanded["layoutBasis"]["p1"], "native-menu-preview")
        self.assertIn("Menu previews", expanded["adaptation"]["nativeParity"])
        self.assertNotIn("native-gameplay", expanded["layoutBasis"].values())

    def test_missing_sp_anchor_nonfinite_or_unbounded_offsets_are_not_guessed_or_clamped(self):
        for mutation in (lambda doc: doc["positions"]["p1"].pop("c"),
                         lambda doc: doc["positions"]["p1"]["a"]["scratch"].__setitem__(0, float("nan")),
                         lambda doc: doc["positions"]["p1"]["b"]["combined"].__setitem__(0, 1921)):
            supplied = positions()
            mutation(supplied)
            with self.assertRaises(ValueError):
                timing.reviewed_positions(supplied)

    def test_current_profile_sizes_retain_native_judgement_band_and_reject_group_mismatch(self):
        profiles = []
        for presentation, keymode, stages in (("p1", "7k", 1), ("p2", "7k", 1), ("any", "14k", 2)):
            profiles.append({"ruleset": "bms", "presentation": presentation, "keymode": keymode,
                "stages": [{"rect": [index * .5, 0, 440 / 1920, 728 / 1080]} for index in range(stages)]})
        sizes = timing.host_sizes(profiles)
        self.assertTrue(all(abs(width - 440) < .001 and abs(height - 87.36) < .001 for width, height in sizes.values()))
        profiles[-1]["stages"][-1]["rect"][2] = 438 / 1920
        with self.assertRaises(ValueError):
            timing.host_sizes(profiles)


def source_library(root):
    rows = []
    for package in (timing.ALIAS_PACKAGE, timing.PACKAGE):
        for index, name in enumerate(timing.ASSETS):
            relative = "packages/" + package.removesuffix(".ifs") + "/sprites/" + name + ".png"
            target = root / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            image = Image.new("RGBA", (64, 22), (20 + index * 30, 50, 90, 255))
            image.save(target)
            rows.append({"package": package, "name": name + ".png", "sprite": relative, "sha256": timing.digest(target.read_bytes()),
                "rgbaSha256": timing.digest(image.tobytes()), "width": 64, "height": 22,
                "sampler": {"min": "nearest", "mag": "nearest", "wrapS": "clamp", "wrapT": "clamp"}})
    metadata = {"status": "complete", "sourceMetadataUnchanged": True, "textures": rows,
                "packages": [{"source": package, "sha256": "b" * 64} for package in (timing.PACKAGE, timing.ALIAS_PACKAGE)]}
    (root / "library.json").write_text(json.dumps(metadata), encoding="utf-8")
    return metadata


class TimingSourceProofTest(unittest.TestCase):
    def test_only_one_original_alias_set_is_selected_with_measured_encoded_and_rgba_hashes(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            source_library(root)
            reference = timing.source_reference(root)
            self.assertEqual(set(reference["assets"]), set(timing.ASSETS))
            self.assertTrue(all(row["package"] == timing.PACKAGE for row in reference["assets"].values()))
            self.assertTrue(all(len(rows) == 2 and len({row["rgbaSha256"] for row in rows}) == 1 for rows in reference["equivalentAliases"].values()))
            self.assertEqual(reference["fontsAdded"], 0)
            self.assertFalse(reference["originalHddAccessed"])
            timing.verify_reference_unchanged(reference)

    def test_changed_png_or_non_equivalent_alias_cannot_be_an_original_pixel_receipt(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            metadata = source_library(root)
            reference = timing.source_reference(root)
            first = metadata["textures"][0]
            target = root / first["sprite"]
            image = Image.new("RGBA", (64, 22), "red")
            image.save(target)
            with self.assertRaises(ValueError):
                timing.verify_reference_unchanged(reference)
            first.update({"sha256": timing.digest(target.read_bytes()), "rgbaSha256": timing.digest(image.tobytes())})
            (root / "library.json").write_text(json.dumps(metadata), encoding="utf-8")
            with self.assertRaises(ValueError):
                timing.source_reference(root)

    def test_bad_position_evidence_fails_before_any_staging_author_or_archive_is_created(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            parent = root / "parent"
            parent.mkdir()
            archive = parent.with_suffix(".osk")
            archive.write_bytes(b"Synthetic immutable archive fixture, never a private game skin")
            generation = {"archive": {"sha256": timing.digest(archive.read_bytes())}}
            (parent / "generation.json").write_text(json.dumps(generation), encoding="utf-8")
            evidence = root / "positions.json"
            evidence.write_text("{}", encoding="utf-8")
            before = sorted(path.name for path in root.iterdir())
            with self.assertRaises(ValueError):
                upgrade(parent, root / "new-author", root, evidence)
            self.assertEqual(sorted(path.name for path in root.iterdir()), before)

    def test_capture_identity_is_verified_without_a_status_or_external_approval_field(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            capture = root / "capture.png"
            Image.new("RGBA", (1, 1), "white").save(capture)
            document = positions()
            document["captures"] = [{"path": str(capture), "sha256": hashlib.sha256(capture.read_bytes()).hexdigest()}]
            path = root / "positions.json"
            path.write_text(json.dumps(document), encoding="utf-8")
            observed, identity = timing.load_positions(path)
            self.assertEqual(observed["positions"]["p2"]["b"]["combined"], [-240, 0])
            self.assertEqual(identity["sha256"], timing.digest(path.read_bytes()))
            capture.write_bytes(b"changed fixture")
            with self.assertRaises(ValueError):
                timing.load_positions(path)


if __name__ == "__main__":
    unittest.main()
