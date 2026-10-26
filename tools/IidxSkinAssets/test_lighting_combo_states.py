"""Behaviour and pixel-preservation checks for the COMBO author upgrade."""
from __future__ import annotations

import copy
import hashlib
import io
from pathlib import Path
import tempfile
import unittest

from PIL import Image

import lighting_combo_states as combo


def fixture():
    resources = [{"id": name, "type": "texture", "path": name + ".png", "filter": "nearest"}
                 for name in ("atlas", "blue", "yellow", "good", "bad", "poor")]
    glyphs = [{"character": str(n), "resource": "atlas", "rect": [74 * n, 0, 74, 100], "advance": 55.0} for n in range(10)]
    manifest = {"resources": resources, "bitmapFonts": [{"id": "font." + palette, "lineHeight": 100,
        "glyphs": copy.deepcopy(glyphs)} for palette in ("b", "y")]}
    scene = {"contract": "oms-gameplay-skin-scene.v1", "root": {"id": "root", "type": "container", "properties": {}, "children": []},
             "templates": [], "instances": [], "tracks": [], "bindings": [], "variants": [], "stateMachines": []}
    def node(identifier, kind="container", props=None, resource=None, children=()):
        value = {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "alpha",
                 "effects": [], "properties": props or {}, "children": list(children)}
        if resource:
            value["resource"] = resource
        return value
    for index, layout in enumerate(("p1", "p2", "dp")):
        rows = []
        for grade, palette, keys in (("perfect", "b", ["perfect"]), ("other", "y", ["great", "good", "meh", "miss", "ok"])):
            identifier = layout + "." + grade
            label = node(identifier + ".label", "sprite", {"height-px": 100}, "blue" if grade == "perfect" else "yellow")
            text = node(identifier + ".combo", "text", {"height-px": 100, "font-size": 100, "bitmap-font": "font." + palette,
                                                       "text": "", "format": "integer"})
            row = node(identifier, props={"x": (.49, .48, .5)[index], "y": .2 if layout != "dp" else .5,
                "origin": "centre", "layout": "horizontal", "gap-px": -16, "visible": False}, children=[label, text])
            rows.append(row)
            scene["bindings"].append({"id": identifier + ".bind", "target": text["id"], "property": "text", "source": "combo.value"})
            scene["variants"].append({"id": identifier + ".numbers", "target": text["id"], "property": "visible",
                "source": "option.judgement-display", "default": False, "cases": [{"id": identifier + ".normal", "key": "normal", "value": True}]})
            scene["variants"].append({"id": identifier + ".grade", "target": identifier, "property": "visible", "source": "judgement.result",
                "default": False, "cases": [{"id": identifier + "." + key, "key": key, "value": True} for key in keys]})
            if grade == "other":
                scene["variants"].append({"id": identifier + ".labelv", "target": label["id"], "property": "resource", "source": "judgement.result",
                    "default": "yellow", "cases": [{"id": identifier + ".label." + key, "key": key, "resource": resource}
                        for key, resource in (("great", "yellow"), ("good", "good"), ("meh", "bad"), ("miss", "poor"), ("ok", "poor"))]})
        gate = node(layout + ".gate", props={"width": 1, "height": 1}, children=rows)
        root = node(layout + ".root", children=[gate])
        scene["templates"].append({"id": layout + ".template", "root": root})
        scene["instances"].append({"id": layout + ".instance", "template": layout + ".template",
            "material": {"slot": "hud.judgement", "resource": "marker/" + layout}})
        scene["variants"].append({"id": layout + ".off", "target": gate["id"], "property": "visible", "source": "option.judgement-display",
                                  "default": True, "cases": [{"id": layout + ".offcase", "key": "off", "value": False}]})
        scene["bindings"].append({"id": layout + ".brightness", "target": root["id"], "property": "brightness", "source": "option.lane-objects-brightness"})
    art = {"resource": "c0", "resources": [{"id": name, "type": "texture", "path": name + ".png", "filter": "nearest"}
        for name in ("c0", "green", "pink")], "labels": {"g": "green", "r": "pink"},
        "glyphs": {(palette, str(n)): [74 * n, 0 if palette == "g" else 100, 74, 100] for palette in ("g", "r") for n in range(10)}}
    return manifest, scene, art


def rendered(scene, *, ruleset="bms", combo_value=64, grade="perfect", elapsed=0, display="normal", layout="p1"):
    """Evaluate the finite author gates/tracks, retaining the actual digit string."""
    instance = next(instance for instance in scene["instances"] if instance["material"]["resource"] == "marker/" + layout)
    root = copy.deepcopy(next(template["root"] for template in scene["templates"] if template["id"] == instance["template"]))
    nodes = {node["id"]: node for node in combo.walk(root) if "type" in node}
    sources = {"combo.active": str(combo_value > 0).lower(), "combo.value": combo_value, "layout.ruleset": ruleset,
               "layout.presentation": layout, "judgement.result": grade, "option.judgement-display": display}
    for binding in scene["bindings"]:
        if binding["target"] not in nodes or binding["source"] not in sources:
            continue
        value = sources[binding["source"]]
        if binding["source"] == "combo.active":
            value = value == "true"
        elif binding["property"] == "text":
            value = str(value)
        nodes[binding["target"]]["properties"][binding["property"]] = value
    for variant in scene["variants"]:
        if variant["target"] not in nodes:
            continue
        field = "resource" if variant["property"] == "resource" else "value"
        value = next((case[field] for case in variant["cases"] if case["key"] == sources.get(variant["source"])), variant["default"])
        node = nodes[variant["target"]]
        if field == "resource":
            node["resource"] = value
        else:
            node["properties"][variant["property"]] = value
    for track in scene["tracks"]:
        if track.get("trigger") != "judgement.hit" or track["target"] not in nodes:
            continue
        if track["type"] == "tween":
            assert track["easing"] == "step" and track["property"] == "opacity"
        else:
            assert track["type"] == "frame" and track["property"] == "resource"
        at = elapsed % track["keyframes"][-1]["time"] if track.get("loop") else elapsed
        value = next(frame["value"] for frame in reversed(track["keyframes"]) if frame["time"] <= at)
        node = nodes[track["target"]]
        if track["property"] == "resource":
            node["resource"] = value
        else:
            node["properties"][track["property"]] = value
    leaves = []
    def visit(node, visible=True):
        props = node["properties"]
        visible = visible and props.get("visible", True) and props.get("opacity", 1) > 0
        if not visible:
            return
        if node["type"] != "container":
            leaves.append(node)
        for child in node.get("children", []):
            visit(child, visible)
    visit(root)
    return leaves


class ComboStateTest(unittest.TestCase):
    def setUp(self):
        self.manifest, self.scene, self.art = fixture()
        self.updated_manifest, self.updated_scene, self.receipt = combo.transform_contribution(self.manifest, self.scene, self.art)

    def test_pg_pairs_share_one_real_combo_and_restart_at_blue(self):
        for elapsed, label, font in ((0, "blue", "font.b"), (99, "blue", "font.b"), (100, "green", "c0gf"),
                                     (199, "green", "c0gf"), (200, "pink", "c0rf"), (299, "pink", "c0rf"), (300, "blue", "font.b")):
            with self.subTest(elapsed=elapsed):
                leaves = rendered(self.updated_scene, elapsed=elapsed)
                self.assertEqual(len(leaves), 2)
                self.assertEqual(leaves[0]["resource"], label)
                self.assertEqual(leaves[1]["properties"]["bitmap-font"], font)
                self.assertEqual(leaves[1]["properties"]["text"], "64")

    def test_phase_tracks_satisfy_actual_codec_required_fields_and_kinds(self):
        # SceneCodec.parseTracks requires easing even for frame tracks. Frame
        # animation is sprite-resource-only; a container opacity uses a numeric
        # tween with step easing. These checks reject the initial r1 document.
        nodes = {node["id"]: node for node in combo.walk(self.updated_scene) if "type" in node}
        tracks = [track for track in self.updated_scene["tracks"] if track.get("trigger") == "judgement.hit"]
        self.assertEqual(len(tracks), 5)
        for track in tracks:
            self.assertTrue({"id", "type", "target", "property", "easing", "loop", "keyframes"} <= set(track))
            self.assertEqual(track["easing"], "step")
            self.assertTrue(track["loop"])
            self.assertEqual([frame["time"] for frame in track["keyframes"]], [0, 100, 200, 300])
            if track["property"] == "resource":
                self.assertEqual(track["type"], "frame")
                self.assertEqual(nodes[track["target"]]["type"], "sprite")
                self.assertTrue(all(isinstance(frame["value"], str) for frame in track["keyframes"]))
            else:
                self.assertEqual(track["property"], "opacity")
                self.assertEqual(track["type"], "tween")
                self.assertEqual(nodes[track["target"]]["type"], "container")
                self.assertTrue(all(type(frame["value"]) is int for frame in track["keyframes"]))

    def test_label_only_is_one_centred_cell_for_both_rulesets_at_every_combo(self):
        nodes = {node["id"]: node for node in combo.walk(self.updated_scene) if "type" in node}
        layout_variants = {variant["property"]: variant for variant in self.updated_scene["variants"]
                           if variant["source"] == "layout.presentation"}
        for ruleset in ("bms", "mania"):
            for value in (0, 1, 12, 12345):
                for layout, expected_anchor in (("p1", (.49, .2)), ("p2", (.48, .2)), ("dp", (.5, .5))):
                    for elapsed, grade, resource in ((0, "perfect", "blue"), (100, "perfect", "green"),
                                                     (200, "perfect", "pink"), (100, "good", "good")):
                        with self.subTest(ruleset=ruleset, combo=value, layout=layout, elapsed=elapsed, grade=grade):
                            leaves = rendered(self.updated_scene, ruleset=ruleset, combo_value=value, layout=layout,
                                              elapsed=elapsed, grade=grade, display="label-only")
                            self.assertEqual(len(leaves), 1)
                            label = leaves[0]
                            self.assertEqual(label["resource"], resource)
                            row, = [node for node in nodes.values() if label["id"] in [child["id"] for child in node.get("children", [])]]
                            self.assertEqual(row["properties"]["layout"], "horizontal")
                            self.assertEqual(row["properties"]["origin"], "centre")
                            self.assertEqual(len(row["children"]), 1)
                            # A single natural-size cell centres on this exact
                            # original row anchor; no hidden number contributes
                            # width, regardless of digit count or phase font.
                            for axis, expected in zip(("x", "y"), expected_anchor):
                                variant = layout_variants[axis]
                                offset = next((case["value"] for case in variant["cases"] if case["key"] == layout), variant["default"])
                                self.assertAlmostEqual(row["properties"][axis] + offset, expected)

    def test_phase_alpha_has_its_own_carrier_and_cannot_override_grade_visibility(self):
        phase_targets = {track["target"] for track in self.updated_scene["tracks"]
                         if track.get("trigger") == "judgement.hit" and track["property"] == "opacity"}
        visibility_targets = {item["target"] for field in ("variants", "bindings") for item in self.updated_scene[field]
                              if item["property"] == "visible"}
        self.assertEqual(len(phase_targets), 3)
        self.assertTrue(phase_targets.isdisjoint(visibility_targets))
        nodes = {node["id"]: node for node in combo.walk(self.updated_scene) if "type" in node}
        for target in phase_targets:
            self.assertEqual(len(nodes[target]["children"]), 1)
            self.assertIn(nodes[target]["children"][0]["id"], visibility_targets)

    def test_bms_bad_and_poor_zero_have_one_centred_label_without_digit_cell(self):
        nodes = {node["id"]: node for node in combo.walk(self.updated_scene) if "type" in node}
        for grade, resource in (("meh", "bad"), ("miss", "poor"), ("ok", "poor")):
            for layout in ("p1", "p2", "dp"):
                leaves = rendered(self.updated_scene, combo_value=0, grade=grade, layout=layout)
                self.assertEqual([leaf["resource"] for leaf in leaves], [resource])
                parent, = [node for node in nodes.values() if leaves[0]["id"] in [child["id"] for child in node.get("children", [])]]
                self.assertEqual(parent["properties"]["layout"], "horizontal")
                self.assertEqual(len(parent["children"]), 1)

    def test_mania_zero_retains_previous_label_and_zero_digit(self):
        for grade in ("meh", "miss", "ok"):
            leaves = rendered(self.updated_scene, ruleset="mania", combo_value=0, grade=grade)
            self.assertEqual(len(leaves), 2)
            self.assertEqual(leaves[1]["properties"]["text"], "0")
            self.assertEqual(leaves[1]["properties"]["bitmap-font"], "font.y")

    def test_positive_good_and_empty_poor_keep_real_digits_and_yellow_font(self):
        for grade, resource in (("good", "good"), ("great", "yellow"), ("ok", "poor")):
            leaves = rendered(self.updated_scene, combo_value=137, grade=grade)
            self.assertEqual(leaves[0]["resource"], resource)
            self.assertEqual(leaves[1]["properties"]["text"], "137")
            self.assertEqual(leaves[1]["properties"]["bitmap-font"], "font.y")

    def test_off_and_label_only_choices_still_work(self):
        for value in (0, 64):
            self.assertEqual(rendered(self.updated_scene, combo_value=value, display="off"), [])
            self.assertEqual(len(rendered(self.updated_scene, combo_value=value, display="label-only")), 1)

    def test_template_reuse_preserves_original_sp_side_and_dp_anchors(self):
        self.assertEqual(len(self.updated_scene["templates"]), 1)
        self.assertEqual(self.updated_scene["instances"][0]["template"], self.updated_scene["instances"][1]["template"])
        self.assertEqual(self.updated_scene["instances"][2]["template"], self.updated_scene["instances"][0]["template"])
        self.assertEqual([row["material"] for row in self.updated_scene["instances"]], [row["material"] for row in self.scene["instances"]])
        variants = {row["property"]: row for row in self.updated_scene["variants"] if row["source"] == "layout.presentation"}
        for layout, expected_x, expected_y in (("p1", .49, .2), ("p2", .48, .2), ("dp", .5, .5)):
            offsets = {axis: next((case["value"] for case in variant["cases"] if case["key"] == layout), variant["default"])
                       for axis, variant in variants.items()}
            self.assertAlmostEqual(.49 + offsets["x"], expected_x)
            self.assertAlmostEqual(.2 + offsets["y"], expected_y)

    def test_only_two_ten_digit_fonts_added_and_old_fonts_byte_exact(self):
        self.assertEqual(self.updated_manifest["bitmapFonts"][:2], self.manifest["bitmapFonts"])
        self.assertEqual(len(self.updated_manifest["bitmapFonts"]), 4)
        for font in self.updated_manifest["bitmapFonts"][2:]:
            self.assertEqual([g["character"] for g in font["glyphs"]], list("0123456789"))
            self.assertEqual([g["advance"] for g in font["glyphs"]], [55] * 10)

    def test_frozen_inputs_are_not_mutated_and_new_ids_have_no_duplicates(self):
        before = fixture()
        self.assertEqual((self.manifest, self.scene, self.art), before)
        ids = [item["id"] for item in combo.walk(self.updated_scene) if "id" in item]
        self.assertEqual(len(ids), len(set(ids)))
        new_nodes = {item["id"] for item in combo.walk(self.updated_scene) if "type" in item}
        self.assertTrue(all(program["target"] in new_nodes for field in ("bindings", "variants", "tracks") for program in self.updated_scene[field]))

    def test_unproven_sp_vertical_change_is_rejected(self):
        self.scene["templates"][1]["root"]["children"][0]["children"][0]["properties"]["y"] = .3
        with self.assertRaises(ValueError):
            combo.transform_contribution(self.manifest, self.scene, self.art)

    def test_a_distinct_dp_program_is_not_silently_dropped(self):
        self.scene["bindings"][-1]["source"] = "option.another-brightness"
        with self.assertRaises(ValueError):
            combo.transform_contribution(self.manifest, self.scene, self.art)

    def test_frame_short_paths_only_rewrite_exclusive_media(self):
        old = "gameplay/frames/one/long-original.png"
        manifests = {"frame": {"resources": [{"id": "frame.exclusive", "path": old}, {"id": "shared", "path": "shared.png"}]},
                     "root": {"resources": [{"id": "shared", "path": "shared.png"}]}}
        plan = combo.frame_path_plan(manifests, {"frame"}, {old: "abc123" + "1" * 58, "shared.png": "0" * 64})
        self.assertEqual(plan, {old: {"path": "f/abc123.png", "sha256": "abc123" + "1" * 58}})
        updated = combo.apply_frame_paths(manifests["frame"], plan)
        self.assertEqual(updated["resources"][0]["id"], "frame.exclusive")
        self.assertEqual(updated["resources"][1], manifests["frame"]["resources"][1])

    def test_short_hash_collision_checks_complete_hash_and_existing_paths(self):
        old = "frame/some-long-original.png"
        manifest = {"frame": {"resources": [{"id": "r", "path": old}]}}
        with self.assertRaises(ValueError):
            combo.frame_path_plan(manifest, {"frame"}, {old: "abcdef" + "0" * 58, "f/abcdef.png": "abcdef" + "1" * 58})
        conflict = copy.deepcopy(manifest)
        conflict["frame"]["resources"].append({"id": "s", "path": "frame/other-long-original.png"})
        with self.assertRaises(ValueError):
            combo.frame_path_plan(conflict, {"frame"}, {old: "abcdef" + "0" * 58,
                "frame/other-long-original.png": "abcdef" + "1" * 58})

    def test_ini_consumed_frame_png_path_is_never_relocated(self):
        old = "frame/some-long-original.png"
        manifest = {"frame": {"resources": [{"id": "r", "path": old}]}}
        self.assertEqual(combo.frame_path_plan(manifest, {"frame"}, {old: "a" * 64}, preserved_paths={old}), {})

    def test_atlas_preserves_full_rgba_including_transparent_colour(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            rows, originals = [], {}
            names = [f"judge_combo_{n}{palette}" for palette in ("g", "r") for n in range(10)]
            names += ["judge_p_great_blue", "judge_p_great_white", "judge_p_great_pink"]
            for index, name in enumerate(names):
                image = Image.new("RGBA", (4, 5), (index * 7 % 256, 37, 91, 255))
                image.putpixel((0, 0), (123, index, 63, 0))
                path = root / (name + ".png")
                image.save(path)
                originals[name] = image
                rows.append({"package": "contents/data/graphic/judge00.ifs", "name": path.name, "sprite": path.name,
                    "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "rgbaSha256": hashlib.sha256(image.tobytes()).hexdigest(),
                    "width": 4, "height": 5, "sampler": {"mag": "nearest", "min": "nearest", "wrapS": "clamp", "wrapT": "clamp"}})
            result = combo.palette_art(root, rows, 0)
            atlas = Image.open(io.BytesIO(result["images"]["c/0a.png"])).convert("RGBA")
            for palette in ("g", "r"):
                for n in range(10):
                    x, y, w, h = result["glyphs"][(palette, str(n))]
                    self.assertEqual(atlas.crop((x, y, x + w, y + h)).tobytes(), originals[f"judge_combo_{n}{palette}"].tobytes())
            self.assertEqual(result["images"]["c/0g.png"], (root / "judge_p_great_white.png").read_bytes())
            (root / "judge_combo_0g.png").write_bytes(b"changed")
            with self.assertRaises((ValueError, OSError)):
                combo.palette_art(root, rows, 0)


if __name__ == "__main__":
    unittest.main()
