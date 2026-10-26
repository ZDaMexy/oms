"""Synthetic PACEMAKER NEXT mathematics and author behavior; no private data."""
from __future__ import annotations

import copy
import unittest

import lighting_gameplay_controls as controls
import lighting_pacemaker_targets as targets
import test_lighting_gameplay_controls as graph_tests
from test_lighting_gameplay_controls import graph_fixture, visible_text_sources
from test_lighting_percentage_target import fixture


class PacemakerNextOptionTest(unittest.TestCase):
    @staticmethod
    def fixture():
        return {"options": [
            {"id": "graph-target", "type": "choice", "default": "maximum",
             "choices": [{"value": item, "label": item} for item in targets.OLD_TARGETS]},
            {"id": "graph-sub-target", "type": "choice", "default": "none",
             "choices": [{"value": item, "label": item} for item in ("none", *targets.OLD_TARGETS)]},
            {"id": "unrelated", "type": "toggle", "default": True}]}

    def test_both_choices_gain_next_without_a_new_option_or_changed_default(self):
        original = self.fixture()
        saved = copy.deepcopy(original)
        result = targets.add_options(original)
        self.assertEqual(original, saved)
        self.assertEqual(len(result["options"]), len(original["options"]))
        self.assertEqual(result["options"][-1], original["options"][-1])
        for option in result["options"][:2]:
            self.assertEqual(option["choices"].pop(), {"value": "next", "label": "PACEMAKER NEXT（BMS）"})
        self.assertEqual(result, original)

    def test_duplicate_or_missing_original_target_is_rejected_without_input_mutation(self):
        for location in ("primary", "sub", "second-application"):
            original = self.fixture()
            if location == "primary":
                original["options"][0]["choices"].pop()
            elif location == "sub":
                original["options"].pop(1)
            else:
                original = targets.add_options(original)
            saved = copy.deepcopy(original)
            with self.subTest(location=location), self.assertRaises(ValueError):
                targets.add_options(original)
            self.assertEqual(original, saved)


class PacemakerNextGraphTest(unittest.TestCase):
    @staticmethod
    def fixture():
        _, manifest = fixture()
        return controls.add_sub_target(graph_fixture()), manifest

    def test_all_main_and_sub_choices_preserve_primary_priority_and_truthful_no_data(self):
        original, manifest = self.fixture()
        saved = copy.deepcopy(original)
        scene, _ = targets.inject(original, manifest)
        sources = {"maximum": "score.maximum", "personal-best": "score.personal-best", "aaa": "score.rank-aaa",
                   "aa": "score.rank-aa", "a": "score.rank-a", "percentage": "option.pacemaker-percent", "next": targets.VALUE_SOURCE}
        for primary in (*targets.OLD_TARGETS, "next"):
            for fallback in ("none", *targets.OLD_TARGETS, "next"):
                for pb in (False, True):
                    for rank in (False, True):
                        for maximum in (False, True):
                            values = {"option.graph-target": primary, "option.graph-sub-target": fallback,
                                "option.graph-visible": True, "score.maximum-available": maximum,
                                "score.personal-best-available": pb, "score.rank-available": rank,
                                targets.AVAILABLE_SOURCE: maximum and pb and rank, "layout.ruleset": "bms"}
                            def available(target):
                                return maximum and target != "none" and (pb if target == "personal-best" else rank
                                    if target in ("aaa", "aa", "a") else pb and rank if target == "next" else True)
                            expected = primary if available(primary) else fallback if available(fallback) else None
                            shown = visible_text_sources(scene, values)
                            with self.subTest(primary=primary, fallback=fallback, pb=pb, rank=rank, maximum=maximum):
                                self.assertEqual([source for _, source in shown if source in sources.values()],
                                                 [] if expected is None else [sources[expected]])
                                self.assertEqual(sum(text == "NO DATA" for text, _ in shown), int(expected is None))
        self.assertEqual(original, saved)

    def test_next_uses_one_dynamic_readout_and_original_media_instead_of_seven_level_copies(self):
        original, manifest = self.fixture()
        scene, receipt = targets.inject(original, manifest)
        nodes = [node for root in (scene["root"], *(item["root"] for item in scene["templates"])) for node in controls.walk(root)]
        label_bindings = [item for item in scene["bindings"] if item["source"] == targets.LABEL_SOURCE]
        self.assertEqual(len(label_bindings), 1)
        self.assertEqual(label_bindings[0]["target"], receipt["newLabel"])
        self.assertEqual(sum(item["source"] == targets.VALUE_SOURCE for item in scene["bindings"]), 1)
        self.assertEqual(sum(item["source"] == targets.PROGRESS_SOURCE for item in scene["bindings"]), 1)
        number_binding, = [item for item in scene["bindings"] if item["source"] == targets.VALUE_SOURCE]
        value, = [node for node in nodes if node["id"] == number_binding["target"]]
        self.assertEqual(value["properties"]["bitmap-font"], controls.GRAPH_FONT)
        self.assertEqual(value["properties"]["format"], "integer")
        original_resources = {node["resource"] for root in (original["root"], *(item["root"] for item in original["templates"]))
                              for node in controls.walk(root) if node.get("resource")}
        self.assertTrue({node["resource"] for node in nodes if node.get("resource")} <= original_resources)
        self.assertFalse(any(item.get("clock", "").startswith("score.pacemaker-next") for item in scene["tracks"]))
        self.assertEqual((receipt["newFonts"], receipt["newTextures"]), (0, 0))

    def test_graph_off_also_hides_next_fallback_and_no_data(self):
        original, manifest = self.fixture()
        scene, _ = targets.inject(original, manifest)
        values = {"option.graph-target": "next", "option.graph-sub-target": "maximum", "option.graph-visible": False,
                  "score.maximum-available": True, "score.personal-best-available": False, "score.rank-available": True,
                  targets.AVAILABLE_SOURCE: False, "layout.ruleset": "bms"}
        self.assertEqual(visible_text_sources(scene, values), [])

    def test_missing_actual_availability_or_duplicate_injection_is_rejected_without_mutation(self):
        for altered in ("maximum-gate", "sub-route", "digits", "duplicate"):
            scene, manifest = self.fixture()
            if altered == "maximum-gate":
                scene["bindings"] = [item for item in scene["bindings"] if item["source"] != "score.maximum-available"]
            elif altered == "sub-route":
                scene["variants"] = [item for item in scene["variants"] if item["source"] != "option.graph-sub-target"]
            elif altered == "digits":
                manifest["bitmapFonts"][0]["glyphs"].pop()
            else:
                scene, _ = targets.inject(scene, manifest)
            saved = copy.deepcopy(scene)
            with self.subTest(altered=altered), self.assertRaises(ValueError):
                targets.inject(scene, manifest)
            self.assertEqual(scene, saved)


class TargetCatalogPublicationTest(unittest.TestCase):
    @staticmethod
    def fixture():
        shared, _ = controls.share_graph_layouts(graph_tests.GameplayGraphTest().shared_fixture())
        scene = controls.compact(controls.add_sub_target(shared), "s")
        _, manifest = fixture()
        manifest.update({"contract": "oms-gameplay-skin-manifest.v1", "scene": "gameplay-skin.scene.json",
                         "sceneContract": "oms-gameplay-skin-scene.v1", "eventContract": "oms-gameplay-skin-event.v1"})
        manifest["options"] = [{"id": "graph-visible", "type": "toggle", "default": True},
                               *PacemakerNextOptionTest.fixture()["options"]]
        manifest["resources"].extend({"id": "digit" + str(i), "type": "texture", "path": "synthetic/font/d" + str(i) + ".png",
                                      "filter": "nearest"} for i in range(10))
        return scene, manifest

    @staticmethod
    def combine(base, selected):
        result = copy.deepcopy(base)
        result["root"]["children"].append(copy.deepcopy(selected["root"]))
        for field in ("templates", "instances", "bindings", "variants", "tracks", "stateMachines"):
            result[field].extend(copy.deepcopy(selected[field]))
        return result

    def test_selected_c2_choice_matches_every_existing_primary_sub_and_no_data_result(self):
        source, manifest = self.fixture()
        full, _ = targets.inject(source, manifest)
        base, _, _, contributions, _ = targets.split_target_module(source, manifest)
        for primary, contribution in contributions.items():
            selected = self.combine(base, contribution["scene"])
            for fallback in ("none", *targets.OLD_TARGETS, "next"):
                for pb in (False, True):
                    for rank in (False, True):
                        for maximum in (False, True):
                            values = {"option.graph-target": primary, "option.graph-sub-target": fallback,
                                "option.graph-visible": True, "score.maximum-available": maximum,
                                "score.personal-best-available": pb, "score.rank-available": rank,
                                targets.AVAILABLE_SOURCE: maximum and pb and rank, "layout.ruleset": "bms"}
                            with self.subTest(primary=primary, fallback=fallback, pb=pb, rank=rank, maximum=maximum):
                                self.assertCountEqual(visible_text_sources(selected, values), visible_text_sources(full, values))

    def test_module_preserves_old_primary_values_default_and_unified_option_count(self):
        scene, manifest = self.fixture()
        saved_scene, saved_manifest = copy.deepcopy(scene), copy.deepcopy(manifest)
        _, updated, module, contributions, review = targets.split_target_module(scene, manifest)
        self.assertEqual(scene, saved_scene)
        self.assertEqual(manifest, saved_manifest)
        self.assertEqual(module["id"], "graph-target")
        self.assertEqual(module["label"], "TARGET SCORE GRAPH")
        self.assertEqual(module["default"], "maximum")
        self.assertEqual([choice["value"] for choice in module["choices"]], [*targets.OLD_TARGETS, "next"])
        self.assertEqual(set(contributions), {*targets.OLD_TARGETS, "next"})
        self.assertEqual(len(updated["options"]) + 1, len(manifest["options"]))
        self.assertFalse(any(option["id"] == "graph-target" for option in updated["options"]))
        self.assertEqual(review["unifiedOptionCountChange"], 0)

    def test_cross_module_options_materials_and_shared_resources_use_existing_contracts(self):
        scene, manifest = self.fixture()
        base, _, _, contributions, _ = targets.split_target_module(scene, manifest)
        base_nodes = [node for tree in (base["root"], *(item["root"] for item in base["templates"])) for node in controls.walk(tree)]
        base_ids = {item["id"] for item in controls._declarations(base, base_nodes)}
        resources = {item["id"]: item for item in manifest["resources"]}
        fonts = {item["id"]: item for item in manifest["bitmapFonts"]}
        for primary, contribution in contributions.items():
            with self.subTest(primary=primary):
                chosen = contribution["scene"]
                nodes = [node for tree in (chosen["root"], *(item["root"] for item in chosen["templates"])) for node in controls.walk(tree)]
                all_ids = [item["id"] for item in controls._declarations(chosen, nodes)]
                self.assertEqual(len(all_ids), len(set(all_ids)))
                for identifier in all_ids:
                    self.assertRegex(identifier, r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")
                self.assertFalse(base_ids.intersection(all_ids))
                node_ids = {item["id"] for item in nodes}
                self.assertTrue(all(item["target"] in node_ids for field in ("bindings", "variants", "tracks") for item in chosen[field]))
                self.assertFalse(any(item["source"] == "option.graph-target" for item in chosen["variants"]))
                self.assertTrue(any(item["source"] == "option.graph-visible" for item in chosen["bindings"]))
                self.assertEqual({item["material"]["resource"] for item in chosen["instances"]},
                    {"markers/frame-" + layout + "-information" for layout in controls.LAYOUTS})
                self.assertTrue(all(item["material"]["slot"] == "decoration" for item in chosen["instances"]))
                self.assertEqual(chosen["templates"][0]["root"]["slot"], "decoration")
                self.assertTrue(all(resources[item["id"]] == item for item in contribution["manifest"]["resources"]))
                self.assertTrue(all(fonts[item["id"]] == item for item in contribution["manifest"]["bitmapFonts"]))
                self.assertFalse(any(key in contribution["manifest"] for key in ("options", "contentCatalog", "script")))

    def test_graph_off_hides_every_selected_target_and_its_sub_route(self):
        scene, manifest = self.fixture()
        base, _, _, contributions, _ = targets.split_target_module(scene, manifest)
        for primary, contribution in contributions.items():
            values = {"option.graph-target": primary, "option.graph-sub-target": "maximum", "option.graph-visible": False,
                "score.maximum-available": True, "score.personal-best-available": False, "score.rank-available": False,
                targets.AVAILABLE_SOURCE: False, "layout.ruleset": "mania"}
            with self.subTest(primary=primary):
                self.assertEqual(visible_text_sources(self.combine(base, contribution["scene"]), values), [])


if __name__ == "__main__":
    unittest.main()
