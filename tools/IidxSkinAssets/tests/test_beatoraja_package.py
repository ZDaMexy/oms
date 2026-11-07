"""Portable checks for actual modular selection and object-reference hazards."""

import copy
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_native import Lua
from beatoraja_package import (Shards, blocking_gaps, entry_script, extract_fragment,
                               namespace_fragment, note_fragment)
from beatoraja_package import (stable_option_ids, control_choices, control_combinations,
                               insert_selection, available_selections, local_controls,
                               CompositionCache, tool_file_hashes, verify_tool_files)


class PackageTests(unittest.TestCase):
    def test_namespace_updates_note_judge_imageset_and_font_references_only(self):
        draw = Lua("function() return main_state.number(101)>0 end")
        original = {"source": [{"id": "source", "path": "assets/source.png"}],
                    "font": [{"id": "font", "path": "assets/a.fnt"}, {"id": "native.fallback", "path": "assets/f.ttf"}],
                    "image": [{"id": "image.1", "src": "source", "timer": 51, "ref": 101}],
                    "imageset": [{"id": "animation.2", "images": ["image.1"], "value": draw}],
                    "text": [{"id": "text", "font": "font", "constantText": "image.1"}],
                    "note": {"id": "native.notes", "note": ["image.1"], "size": [16],
                             "group": [{"id": "image.1", "op": [920]}]},
                    "judge": [{"id": "judge", "index": 0, "images": [{"id": "image.1"}]}],
                    "destination": [{"id": "native.notes", "draw": draw, "offsets": [3]}]}
        result = namespace_fragment(original, "shard")
        self.assertEqual(result["image"][0], {"id": "shard.image.1", "src": "source", "timer": 51, "ref": 101})
        self.assertEqual(result["imageset"][0]["images"], ["shard.image.1"])
        self.assertEqual(result["note"]["note"], ["shard.image.1"])
        self.assertEqual(result["note"]["group"][0]["id"], "shard.image.1")
        self.assertEqual(result["judge"][0]["images"][0]["id"], "shard.image.1")
        self.assertEqual(result["text"][0]["font"], "shard.font")
        self.assertEqual(result["text"][0]["constantText"], "image.1")
        self.assertEqual(result["font"][1]["id"], "native.fallback")
        self.assertEqual(result["destination"][0]["offsets"], [3])
        self.assertIsInstance(result["destination"][0]["draw"], Lua)
        self.assertEqual(result["source"], original["source"])
        self.assertEqual(original["image"][0]["id"], "image.1")

    def test_note_and_charge_patches_do_not_overlap_geometry_or_each_other(self):
        skin = {"note": {"id": "native.notes", "note": ["n"], "lnbody": ["body"],
                         "lnstart": ["head"], "mine": ["mine"], "group": [{"id": "line"}],
                         "size": [24], "dst": [{"x": 1}]},
                "image": [{"id": i, "src": i} for i in ("n", "body", "head", "mine", "line")],
                "source": [{"id": i, "path": i + ".png"} for i in ("n", "body", "head", "mine", "line")],
                "destination": [{"id": "native.notes"}]}
        patches = [note_fragment(skin, part) for part in ("note-geometry", "notes", "charge-notes")]
        fields = [set(p["note"]) for p in patches]
        self.assertFalse(fields[0] & fields[1] or fields[0] & fields[2] or fields[1] & fields[2])
        self.assertEqual(set.union(*fields), set(skin["note"]))
        self.assertEqual([len(p["destination"]) for p in patches], [1, 0, 0])
        self.assertEqual([{i["id"] for i in p["image"]} for p in patches], [{"mine", "line"}, {"n"}, {"body", "head"}])

    def test_shards_deduplicate_payload_order_and_preserve_lua_callback_type(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-package-tests"
        parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=parent) as folder:
            store = Shards(Path(folder))
            a = {"image": [{"id": "i", "src": "s"}], "destination": [{"id": "i", "draw": Lua("function() return true end")} ]}
            b = {"destination": copy.deepcopy(a["destination"]), "image": copy.deepcopy(a["image"])}
            self.assertEqual(store.add(a), store.add(b))
            self.assertEqual(len(store.records), 1)
            text = next((Path(folder) / "shards").glob("*.lua")).read_text(encoding="utf-8")
            self.assertIn("return function() return", text)
            self.assertIn('["draw"]=function() return true end', text)

    def test_header_only_branch_precedes_all_payload_loading_and_unknown_rejects(self):
        script = entry_script({"name": "Family", "type": 0}, ["p1", "p2"], ["f"],
                              {"p1": {"f": {"notes": {"selectors": ["notes"], "defaults": {}, "paths": {"n": "shards/n"}}}}},
                              {"frame": [{"label": "Frame F", "value": "f"}],
                               "notes": [{"label": "Notes N", "value": "n"}]},
                              {"frame": "f", "notes": "n"}, {"name": "Family", "type": 0})
        self.assertLess(script.index("skin_config.onlyload"), script.index("require(path)"))
        self.assertIn("Unknown native option", script)
        self.assertIn("Unknown Play Side", script)
        self.assertIn("if choices[module] == nil", script)  # False is a legitimate author toggle.
        self.assertIn("path=assert(path[key]", script)

    def test_partial_effects_are_not_advertised_as_available(self):
        missing = [{"feature": "event animation needs complete bake"}]
        self.assertEqual(blocking_gaps(missing, "full-combo"), missing)
        self.assertEqual(blocking_gaps([{"code": "judge-combo-fit-track"}], "judgement"), [])
        self.assertEqual(blocking_gaps([{"feature": "graph", "reason": "native-target-selection"}], "graph-target"), [])

    def test_charge_selection_requires_both_caps_and_original_hold_effects(self):
        author = SimpleNamespace(manifest={"options": [{"id": "hcn-display", "type": "choice", "default": "hcn",
                    "choices": [{"value": "hcn"}, {"value": "cn"}]}]}, modules={"charge-notes": {
                    "choices": [{"value": "c0"}, {"value": "c1"}]}})
        coverage = [{"mode": "p1", "frame": "f", "stage": stage, "choice": choice,
                     "options": {"hcn-display": display}, "available": True, "gaps": []}
                    for stage in ("charge-notes", "long-note-effects") for choice in ("c0", "c1")
                    for display in ("hcn", "cn")]
        for row in coverage:
            if row["stage"] == "long-note-effects" and (row["choice"] == "c1" or row["options"]["hcn-display"] == "cn"):
                row["available"] = False
                row["gaps"] = [{"feature": "event animation needs complete bake"}]
        self.assertEqual((["c0"], {"hcn-display": ["hcn"]}),
                         available_selections(author, "charge-notes", coverage, ["p1"], ["f"]))
        self.assertEqual(([], {"hcn-display": ["hcn"]}), available_selections(author, "charge-notes",
                         [r for r in coverage if r["stage"] == "charge-notes"], ["p1"], ["f"]))

    def test_source_catalog_reserves_ids_for_temporarily_unavailable_choices(self):
        author = SimpleNamespace(modules={"frame": {"choices": [{"value": "f1"}, {"value": "f2"}]},
                                          "notes": {"choices": [{"value": "n0"}, {"value": "n1"}]}})
        ids = stable_option_ids(author)
        self.assertEqual(ids, {"frame": {"f1": 1000, "f2": 1001}, "notes": {"n0": 1002, "n1": 1003}})
        script = entry_script({"name": "test"}, ["dp"], ["f2"], {},
                              {"frame": [{"label": "F2", "value": "f2"}],
                               "notes": [{"label": "N1", "value": "n1"}]},
                              {"frame": "f2", "notes": "n1"}, {}, ids)
        self.assertIn('["op"]=1001', script)
        self.assertIn('["op"]=1003', script)

    def test_local_controls_keep_exact_declared_values_and_false_selection(self):
        positions = control_choices({"type": "number", "min": -2, "max": 10, "step": 1})
        sizes = control_choices({"type": "number", "min": .5, "max": 1.5, "step": .05})
        self.assertEqual([c["value"] for c in positions], list(range(-2, 11)))
        self.assertEqual(len(sizes), 21)
        self.assertEqual(sizes[3], {"value": .65, "label": "0.65"})
        values = control_combinations({"display": [{"value": True}, {"value": False}], "position": positions})
        self.assertEqual(len(values), 26)
        self.assertEqual(values[-1], {"display": False, "position": 10})
        paths = {}
        insert_selection(paths, ["j00", False, -2], "shards/off")
        self.assertEqual(paths, {"j00": {False: {-2: "shards/off"}}})

    def test_unbaked_optional_length_does_not_remove_working_default_beam(self):
        author = SimpleNamespace(manifest={"options": [{"id": "beam-length", "type": "choice", "default": "normal",
                                                        "choices": [{"value": "normal"}, {"value": "long"}]}]},
                                 modules={"key-beam": {"choices": [{"value": "b0"}, {"value": "b1"}]}})
        coverage = [{"stage": "key-beam", "mode": "p1", "frame": "f", "choice": choice,
                     "options": {"beam-length": length}, "available": working, "gaps": []}
                    for choice, length, working in (("b0", "normal", True), ("b0", "long", False),
                                                    ("b1", "normal", False), ("b1", "long", True))]
        choices, controls = available_selections(author, "key-beam", coverage, ["p1"], ["f"])
        self.assertEqual(choices, ["b0"])
        self.assertEqual(controls, {"beam-length": ["normal"]})

    def test_standard_fixed_timing_controls_do_not_invent_a_module(self):
        definition = {"id": "timing-position", "type": "choice", "default": "a",
                      "choices": [{"value": value, "label": value.upper()} for value in ("a", "b", "c", "off")]}
        author = SimpleNamespace(manifest={"options": [definition]}, modules={})
        self.assertEqual(local_controls(author, "timing-display", "7k"), {"timing-position": definition["choices"]})
        coverage = [{"stage": "timing-display", "mode": "p1", "frame": "f", "choice": None,
                     "options": {"timing-position": value}, "available": True,
                     "gaps": [{"code": "native-timing-feedback-adaptation"}]}
                    for value in ("a", "b", "c", "off")]
        choices, controls = available_selections(author, "timing-display", coverage, ["p1"], ["f"])
        self.assertEqual(choices, [None])
        self.assertEqual(controls, {"timing-position": ["a", "b", "c", "off"]})

    def test_cache_uses_actual_contribution_frame_dependencies_and_judge_hosts(self):
        scene = {}
        def contribution(module, options):
            return SimpleNamespace(module=module, choice=options[module], manifest_path=module + ".json",
                                   scene_path=module + ".scene", ini_path=None, scene=scene)
        author = SimpleNamespace(contribution=contribution)
        cache = CompositionCache(author)
        options = {"frame": "f1", "notes": "n", "charge-notes": "c", "judgement": "j", "judgement-position": 0}
        frame = SimpleNamespace(manifest={"layouts": [{"ruleset": "bms", "keymode": "7k", "presentation": "p1",
                                                       "stages": [{"rect": [0, 0, .3, .8]}]}]})
        alternate = {**options, "frame": "f2"}
        self.assertEqual(cache.key("notes", "p1", frame, options, 27), cache.key("notes", "p1", frame, alternate, 27))
        self.assertNotEqual(cache.key("notes", "p1", frame, options, 27),
                            cache.key("notes", "p1", frame, {**options, "charge-notes": "other"}, 27))
        self.assertEqual(cache.key("judgement", "p1", frame, options, 27), cache.key("judgement", "p1", frame, alternate, 27))
        moved = copy.deepcopy(frame)
        moved.manifest["layouts"][0]["stages"][0]["rect"][0] = .1
        self.assertNotEqual(cache.key("judgement", "p1", frame, options, 27), cache.key("judgement", "p1", moved, alternate, 27))
        scene["bindings"] = [{"source": "option.frame"}]
        dependent = CompositionCache(author)
        self.assertNotEqual(dependent.key("notes", "p1", frame, options, 27), dependent.key("notes", "p1", frame, alternate, 27))
        self.assertIsNone(cache.key("graph-area", "p1", frame, options, 27))

    def test_tool_identity_rejects_modified_or_new_adapter_files(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-package-tests"
        parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=parent) as folder:
            root = Path(folder)
            path = root / "beatoraja_sample.py"
            path.write_text("original", encoding="utf-8")
            hashes = tool_file_hashes(root)
            verify_tool_files(hashes, root)
            path.write_text("changed", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "beatoraja_sample.py"):
                verify_tool_files(hashes, root)
            path.write_text("original", encoding="utf-8")
            (root / "beatoraja_new.py").write_text("new", encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "beatoraja_new.py"):
                verify_tool_files(hashes, root)

    def test_custom_timer_identity_keeps_numeric_ids_and_rejects_conflicts(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-package-tests"
        parent.mkdir(parents=True, exist_ok=True)
        a = {"id": 10000, "timer": Lua("function() return 10 end")}
        b = {"id": 10000, "timer": Lua("function() return 20 end")}
        self.assertEqual(namespace_fragment({"customTimers": [a]}, "ignored")["customTimers"], [a])
        with tempfile.TemporaryDirectory(dir=parent) as folder:
            store = Shards(Path(folder))
            path = store.add({"customTimers": [a, a]})
            text = (Path(folder) / (path + ".lua")).read_text(encoding="utf-8")
            self.assertIn('_customTimerSignatures', text)
            self.assertIn('["id"]=10000', text)
            with self.assertRaisesRegex(ValueError, "Conflicting custom timer"):
                store.add({"customTimers": [a, b]})


if __name__ == "__main__":
    unittest.main()
