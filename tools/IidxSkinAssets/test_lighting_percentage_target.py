"""Synthetic percentage-target guards; no native artwork or player data required."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from build_lighting_percentage_target import REFERENCE_CONTRACT, load_reference
import lighting_panels as panels
from lighting_percentage_target import NEW_IDS, OFFICIAL_URL, OPTION_ID, add_options, inject


def fixture(layout="p1"):
    graph = panels.build_graph("synthetic.graph", layout, targets=panels.GRAPH_TARGETS)
    scene = {"contract": "oms-gameplay-skin-scene.v1", "root": panels._node("root", children=graph["roots"]),
             "templates": [], "instances": [], "tracks": [], "stateMachines": [],
             "bindings": graph["bindings"], "variants": graph["variants"]}
    resources = [{"id": "omni.lighting." + name.replace("_", "-"), "type": "texture",
                  "path": "art/lighting-panels/" + name + ".png", "filter": "nearest"}
                 for name in ("gr_sco_gauge_large_green", "gr_sco_gauge_waku")]
    manifest = {"resources": resources, "bitmapFonts": [{"id": panels.GRAPH_FONT, "lineHeight": 24,
        "glyphs": [{"character": str(value), "resource": "digit" + str(value), "advance": 40} for value in range(10)]}]}
    return scene, manifest


def nodes(scene):
    return {node["id"]: node for tree in (scene["root"], *(item["root"] for item in scene["templates"])) for node in panels.walk(tree)}


class PercentageTargetTest(unittest.TestCase):
    def test_all_layouts_keep_every_old_branch_coordinate_colour_and_program_exact(self):
        for layout in panels.LAYOUTS:
            with self.subTest(layout=layout):
                original, manifest = fixture(layout)
                saved, saved_manifest = copy.deepcopy(original), copy.deepcopy(manifest)
                result, receipt = inject(original, manifest)
                self.assertEqual(original, saved)
                self.assertEqual(manifest, saved_manifest)
                self.assertEqual(len(receipt["newNodeIds"]), 7)
                restored = copy.deepcopy(result)
                by_id = nodes(restored)
                parent = by_id[receipt["parent"]]
                parent["children"] = [child for child in parent["children"] if child["id"] != receipt["newBranch"]]
                for field in ("tracks", "bindings", "variants"):
                    restored[field] = [item for item in restored[field] if item["id"] not in NEW_IDS]
                self.assertEqual(restored, original)
                new_nodes = nodes(result)
                percentage = new_nodes["pp.n6"]["properties"]
                self.assertEqual(percentage["text"], "%")
                self.assertNotIn("bitmap-font", percentage)
                self.assertNotIn("format", percentage)
                self.assertEqual(new_nodes[receipt["newValueNode"]]["properties"]["bitmap-font"], panels.GRAPH_FONT)
                self.assertEqual(new_nodes[receipt["newValueNode"]]["properties"]["format"], "integer")

    def test_frozen_clock_has_zero_half_full_endpoints_without_loop_trigger_or_score_arithmetic(self):
        original, manifest = fixture()
        result, receipt = inject(original, manifest)
        track, = result["tracks"]
        self.assertEqual(track["clock"], "option.pacemaker-percent")
        self.assertEqual(track["property"], "scale-y")
        self.assertEqual(track["easing"], "linear")
        self.assertIs(track["loop"], False)
        self.assertNotIn("trigger", track)
        self.assertEqual([(key["time"], key["value"]) for key in track["keyframes"]], [(0, 0), (100, 1)])
        self.assertEqual(track["target"], receipt["newFillNode"])
        new_bindings = [item for item in result["bindings"] if item["id"] in NEW_IDS]
        self.assertEqual({item["source"] for item in new_bindings}, {"score.maximum-available", "option.pacemaker-percent"})
        self.assertFalse(any(item["source"] == "option.pacemaker-percent" and item["property"] == "visible" for item in new_bindings))
        fill = nodes(result)[receipt["newFillNode"]]
        self.assertEqual(fill["properties"]["origin"], "bottom-left")
        self.assertEqual(fill["properties"]["scale-y"], 0)

    def test_later_unrelated_options_and_all_five_existing_defaults_are_preserved(self):
        source = {"options": [{"id": "graph-visible", "type": "toggle", "default": True},
                             *panels.options()[:1],
                             {"id": "judgement-position", "type": "number", "min": -2, "max": 10, "step": 1, "default": 0}],
                  "unrelated": {"heightSource": "preserved"}}
        saved = copy.deepcopy(source)
        result = add_options(source)
        self.assertEqual(source, saved)
        setting = result["options"][-1]
        self.assertEqual({key: setting[key] for key in ("id", "type", "default", "min", "max", "step")},
                         {"id": OPTION_ID, "type": "number", "default": 50, "min": 0, "max": 100, "step": 1})
        result["options"].pop()
        result["options"][1]["choices"].pop()
        self.assertEqual(result, source)

    def test_missing_real_maximum_or_graph_display_gate_rejects_without_mutation(self):
        for source in ("option.graph-visible", "score.maximum-available"):
            scene, manifest = fixture()
            scene["bindings"] = [item for item in scene["bindings"] if item["source"] != source]
            saved = copy.deepcopy(scene)
            with self.subTest(source=source), self.assertRaisesRegex(ValueError, "gates"):
                inject(scene, manifest)
            self.assertEqual(scene, saved)

    def test_unexpected_old_maximum_animation_is_not_silently_cloned(self):
        scene, manifest = fixture()
        selector = next(item for item in scene["variants"] if item["cases"][0]["key"] == "maximum")
        scene["tracks"].append({"id": "old.track", "target": selector["target"], "keyframes": []})
        saved = copy.deepcopy(scene)
        with self.assertRaisesRegex(ValueError, "competing program"):
            inject(scene, manifest)
        self.assertEqual(scene, saved)

    def test_nested_state_and_template_id_conflicts_are_atomic(self):
        for location in ("state-assignment", "template", "keyframe", "variant-case"):
            scene, manifest = fixture()
            if location == "state-assignment":
                scene["stateMachines"] = [{"id": "m", "states": [{"id": "s", "set": [{"id": "pp.b1"}]}], "transitions": []}]
            elif location == "template":
                scene["templates"] = [{"id": "pp.n6", "root": panels._node("other.root")}]
            elif location == "keyframe":
                scene["tracks"] = [{"id": "other.track", "target": "root", "keyframes": [{"id": "pp.k0"}]}]
            else:
                scene["variants"][0]["cases"][0]["id"] = "pp.c0"
            saved = copy.deepcopy(scene)
            with self.subTest(location=location), self.assertRaisesRegex(ValueError, "conflicts"):
                inject(scene, manifest)
            self.assertEqual(scene, saved)

    def test_content_addressed_texture_paths_keep_original_resource_identity(self):
        scene, manifest = fixture()
        for index, resource in enumerate(manifest["resources"]):
            resource["path"] = f"art/ld/{index:016x}.png"
        maximum = next(item["target"] for item in scene["variants"] if item["cases"][0]["key"] == "maximum")
        original_sprites = {node["resource"] for node in panels.walk(nodes(scene)[maximum]) if node["type"] == "sprite"}
        result, receipt = inject(scene, manifest)
        actual = nodes(result)
        self.assertEqual({actual[node_id]["resource"] for node_id in receipt["newNodeIds"]
                          if actual[node_id]["type"] == "sprite"}, original_sprites)

    def test_missing_digits_or_bar_texture_rejects(self):
        for changed in ("font", "bar"):
            scene, manifest = fixture()
            if changed == "font":
                manifest["bitmapFonts"][0]["glyphs"].pop()
            else:
                manifest["resources"][0]["type"] = "unsupported"
            with self.subTest(changed=changed), self.assertRaisesRegex(ValueError, "digit font"):
                inject(scene, manifest)

    def test_second_injection_and_existing_numeric_option_are_rejected(self):
        scene, manifest = fixture()
        result, _ = inject(scene, manifest)
        with self.assertRaisesRegex(ValueError, "conflicts"):
            inject(result, manifest)
        source = {"options": [{"id": OPTION_ID}]}
        with self.assertRaisesRegex(ValueError, "conflicts"):
            add_options(source)


class PercentageReferenceTest(unittest.TestCase):
    def setUp(self):
        directory = Path(__file__).resolve().parents[2] / "artifacts/omni-start-options-20261024"
        directory.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(prefix="percentage-synthetic-", dir=directory)
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name)
        self.image = self.directory / "synthetic-native50.png"
        Image.new("RGB", (2, 2), (80, 80, 80)).save(self.image)
        self.path = self.directory / "reviewed.json"
        self.value = {"contract": REFERENCE_CONTRACT, "status": "reviewed",
            "source": {key: "a" * 64 for key in ("manifestSha256", "generationSha256", "archiveSha256")},
            "official": {"url": OFFICIAL_URL, "min": 0, "max": 100, "default": 50}, "authorStep": 1,
            "observed": {"value": 50, "screenshot": {"path": self.image.name,
                "sha256": hashlib.sha256(self.image.read_bytes()).hexdigest()}},
            "review": "Synthetic loader test only; no actual native screenshot or author is produced."}

    def write(self):
        self.path.write_text(json.dumps(self.value), encoding="utf-8")

    def test_review_keeps_official_range_separate_from_only_observed_50(self):
        self.write()
        result = load_reference(self.path)
        self.assertEqual(result["official"]["max"], 100)
        self.assertNotIn("step", result["official"])
        self.assertEqual(result["authorStep"], 1)
        self.assertEqual(result["observed"]["value"], 50)
        self.assertEqual(result["observed"]["screenshot"]["path"], str(self.image))
        self.assertEqual(result["sha256"], hashlib.sha256(self.path.read_bytes()).hexdigest())

    def test_altered_receipt_image_rejects(self):
        self.write()
        Image.new("RGB", (2, 2), (0, 0, 0)).save(self.image)
        with self.assertRaisesRegex(ValueError, "screenshot changed"):
            load_reference(self.path)

    def test_unobserved_value_or_fabricated_official_bounds_reject(self):
        for field in ("observed", "range", "url", "bool"):
            saved = copy.deepcopy(self.value)
            if field == "observed":
                self.value["observed"]["value"] = 100
            elif field == "range":
                self.value["official"]["max"] = 99
            elif field == "url":
                self.value["official"]["url"] = "https://example.invalid"
            else:
                self.value["authorStep"] = True
            self.write()
            with self.subTest(field=field), self.assertRaises(ValueError):
                load_reference(self.path)
            self.value = saved

    def test_duplicate_review_fields_reject(self):
        self.write()
        self.path.write_text(self.path.read_text(encoding="utf-8").replace('"status": "reviewed"',
                             '"status": "reviewed", "status": "reviewed"'), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "duplicate JSON field"):
            load_reference(self.path)


if __name__ == "__main__":
    unittest.main()
