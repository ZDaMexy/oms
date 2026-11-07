"""Native entrance geometry and original Lightning Judge Panel HUD routes."""

from copy import deepcopy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import AuthorPackage, Contribution, walk_nodes
from beatoraja_entrance import apply_preparation_translation, preparation_opacity_condition
from beatoraja_hud import _Sources, export_hud
from beatoraja_native import AssetSink, Lua, lua


def track(axis="y", first=.14537037037037037, last=0, duration=500, easing="out"):
    return {"id": "entry", "target": "panel", "type": "tween", "property": axis,
            "clock": "preparation.elapsed", "easing": easing, "loop": False,
            "keyframes": [{"time": 0, "value": first}, {"time": duration, "value": last}]}


def native_case(name, baseline, translated, motion, parent=(0, 0, 1920, 1080), baseline_value=0):
    return {"name": name, "skin": {"destination": translated}, "baseline": baseline,
            "track": motion, "parent": parent, "baselineValue": baseline_value}


class EntranceTests(unittest.TestCase):
    def setUp(self):
        root = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-entrance-dev"
        root.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=root)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)

    def probe(self, cases, opacity_cases=()):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        recipe = self.folder / "entrance.lua"
        recipe.write_text("return " + lua({"geometry": cases, "opacity": opacity_cases}), encoding="utf-8")
        source = Path(__file__).with_name("EntranceProbe.java")
        compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.folder), str(source)],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        result = subprocess.run([java, "-cp", str(self.folder) + os.pathsep + str(jar),
                                 "bms.player.beatoraja.skin.lua.EntranceProbe", str(recipe)],
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        return json.loads(result.stdout)

    def test_translation_retains_baseline_gates_and_relative_internal_clip(self):
        base = {"id": "image", "blend": 2, "offsets": [3], "op": [42],
                "draw": Lua("function() return main_state.option(9001) end"),
                "dst": [{"time": 0, "x": 100, "y": 200, "w": -50, "h": 30,
                         "r": 80, "g": 90, "b": 100, "a": 120,
                         "clip_x": 75, "clip_y": 202, "clip_w": 20, "clip_h": 24}]}
        untouched = deepcopy(base)
        result = apply_preparation_translation([base], track(), (0, 0, 1920, 1080), 0)
        self.assertEqual(untouched, base)
        self.assertEqual(base["dst"], result[0]["dst"])
        self.assertEqual((10020, 500), (result[1]["timer"], result[1]["loop"]))
        self.assertEqual([43, 200], [point["y"] for point in result[1]["dst"]])
        self.assertEqual([45, 202], [point["clip_y"] for point in result[1]["dst"]])
        self.assertEqual([2, 2], [point["acc"] for point in result[1]["dst"]])
        for row in result:
            self.assertEqual((2, [3], [42]), (row["blend"], row["offsets"], row["op"]))
            self.assertIn("main_state.option(9001)", row["draw"])
        with self.assertRaises(ValueError):
            apply_preparation_translation(result, track(), (0, 0, 1920, 1080), 0)

    def test_unqualified_time_geometry_and_nonbinary_opacity_are_rejected(self):
        original = {"id": "text", "dst": [{"time": 0, "x": 1, "y": 2, "w": 3, "h": 4}]}
        for changes in ({"timer": 46}, {"loop": -1}, {"dst": [*original["dst"], *original["dst"]]}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                apply_preparation_translation([{**original, **changes}], track(), (0, 0, 1920, 1080), 0)
        for changes in ({"loop": True}, {"easing": "linear"}, {"clock": "timing.time"}):
            with self.subTest(changes=changes), self.assertRaises(ValueError):
                apply_preparation_translation([original], {**track(), **changes}, (0, 0, 1920, 1080), 0)
        with self.assertRaises(ValueError):
            preparation_opacity_condition(track("opacity", 0, .5, easing="step"), 0)

    def test_hud_keeps_dynamic_preparation_gates_and_one_translation_through_steps(self):
        source = self.folder / "source"
        output = self.folder / "output"
        source.mkdir(); output.mkdir()
        sink = AssetSink(source, output)
        child = {"id": "caption", "type": "text", "properties": {"text": "READY", "font-size": 20}}
        root = {"id": "panel", "type": "container", "properties": {"opacity": 0}, "children": [child]}
        scene = {"root": root, "bindings": [{"id": "visible", "target": "caption", "property": "visible", "source": "preparation.active"}],
                 "tracks": [track(), track("opacity", 1, 0, 1000, "step"),
                            {**track("font-size", 20, 30, 10, "step"), "id": "size", "target": "caption", "clock": "combo.value"}]}
        contribution = Contribution("frame", "fixture", "manifest.json", None, None, {}, scene, "")
        result = export_hud(contribution, root, (0, 0, 1920, 1080), sink, context={"combo.value": 0})
        self.assertEqual([], result["gaps"])
        self.assertEqual(2, len(result["text"]))
        self.assertEqual(4, len(result["destination"]))
        self.assertEqual({20, 30}, {round(row["size"] * 768 / 1080) for row in result["text"]})
        self.assertTrue(all("10023" in row["draw"] for row in result["destination"]))
        self.assertTrue(all(row["dst"][0]["a"] == 255 for row in result["destination"]))
        self.assertIn("10020", _Sources([], {}).expression("preparation.available")[0])
        self.assertIn("10021", _Sources([], {}).expression("presentation.elapsed")[0])
        nested = deepcopy(scene)
        nested["tracks"].append({**track("x", .1), "id": "second", "target": "caption"})
        rejected = export_hud(Contribution("frame", "fixture", None, None, None, {}, nested, ""),
                              root, (0, 0, 1920, 1080), sink, context={"combo.value": 0})
        self.assertFalse(rejected["destination"])
        self.assertTrue(any("more than one" in gap.get("detail", "") for gap in rejected["gaps"]))

    def test_official_native_out_clip_and_exact_binary_steps(self):
        cases = []
        for axis, first, last, duration, parent, baseline_value in (
                ("x", -.28125, 0, 350, (0, 0, 1920, 1080), 0),
                ("y", .14537037037037037, 0, 500, (0, 0, 1920, 1080), 0),
                ("y", -.09259259259259259, 0, 500, (0, 0, 1920, 1080), 0),
                ("x", -.2, .1, 500, (30, 40, 401, 245), .05)):
            motion = track(axis, first, last, duration)
            baseline = [{"id": "source", "blend": 1, "draw": Lua("function() return main_state.option(9001) end"),
                         "dst": [{"time": 0, "x": 70, "y": 400, "w": -100, "h": 36,
                                  "r": 80, "g": 100, "b": 120, "a": 200,
                                  "clip_x": 15, "clip_y": 403, "clip_w": 40, "clip_h": 30}]}]
            cases.append(native_case(axis+str(first), baseline,
                apply_preparation_translation(baseline, motion, parent, baseline_value), motion, parent, baseline_value))
        opacity = []
        for initial, first, last in ((0, 1, 0), (1, 0, 1)):
            definition = track("opacity", first, last, 1000, "step")
            opacity.append({"baseline": initial, "first": first, "last": last,
                            "skin": {"destination": [{"id": "step", "draw": Lua("function() return " +
                                      preparation_opacity_condition(definition, initial) + " end")}]}})
        receipt = self.probe(cases, opacity)
        self.assertEqual(4, receipt["cases"])
        self.assertGreater(receipt["clipChecks"], 100)
        self.assertGreater(receipt["opacityChecks"], 20)
        print("Native entrance geometry: " + json.dumps(receipt, sort_keys=True))

    def test_all_45_source_judge_panels_keep_fonts_gates_and_actual_entry_geometry(self):
        root = Path(os.environ.get("OMS_LIGHTNING_AUTHOR",
            "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1"))
        if not root.is_dir():
            self.skipTest("The frozen private Lightning author is required")
        author = AuthorPackage(root, "lightning")
        sink = AssetSink(root, self.folder)
        cases, source_nodes = [], 0
        for choice in author.modules["frame"]["choices"]:
            for keymode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                options = author.options(keymode, side, {"frame": choice["value"], "judge-panel-visible": True})
                frame = author.contribution("frame", options)
                candidates = [(target, motion) for target in frame.templates.values() for motion in frame.scene.get("tracks", [])
                              if motion["target"] == target["id"] and motion.get("clock") == "preparation.elapsed"
                              and motion["property"] in ("x", "y") and any(n["type"] == "text" for n in walk_nodes(target))]
                self.assertEqual(1, len(candidates))
                target, motion = candidates[0]
                context = {"layout.ruleset": "bms", "layout.presentation": side}
                live = export_hud(frame, target, (0, 0, 1920, 1080), sink, options=options, context=context)
                baseline = export_hud(frame, target, (0, 0, 1920, 1080), sink, options=options,
                                      context={**context, "preparation.available": False, "preparation.active": False,
                                               "preparation.ready": False})
                self.assertEqual(10, len(live["text"]), (choice["value"], side))
                self.assertEqual(20, len(live["destination"]))
                for field in ("font", "text", "value", "mappings", "gaps"):
                    self.assertEqual(baseline[field], live[field], (choice["value"], side, field))
                self.assertFalse(any(g.get("reason") == "unmapped-text-track" for g in live["gaps"]))
                hidden = export_hud(frame, target, (0, 0, 1920, 1080), sink,
                                    options={**options, "judge-panel-visible": False}, context=context)
                self.assertEqual([], hidden["destination"])
                cases.append(native_case(choice["value"]+"/"+side, baseline["destination"], live["destination"],
                                         motion, baseline_value=target.get("properties", {}).get(motion["property"], 0)))
                source_nodes += len(live["text"])
        self.assertEqual((45, 450), (len(cases), source_nodes))
        receipt = self.probe(cases)
        self.assertEqual(45, receipt["cases"])
        self.assertGreater(receipt["geometryChecks"], 10000)
        self.assertLess(receipt["maxSettledEndpointErrorPx"], .001)
        print("Original Lightning HUD entrance: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
