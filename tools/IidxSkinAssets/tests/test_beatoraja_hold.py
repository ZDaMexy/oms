"""Original hold geometry and public-timer lifecycle; no gameplay or GPU claims."""

import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import Contribution
from beatoraja_export import ANIMATION_RUNTIME
from beatoraja_hold import hold_plan, transition_timer
from beatoraja_native import Lua, lua


OFF = -(2 ** 63)


def fixture(deck=0, key=1):
    # Original Standard scratch carrier, including both otherwise-easy-to-lose
    # 4x and positioned ancestors. No private artwork is copied into the test.
    def node(identifier, kind="container", properties=None, children=None, **extra):
        return {"id": identifier, "type": kind, "properties": properties or {},
                "children": children or [], "blend": "alpha", **extra}
    children, variants = [], []
    appearance = {"cn": ["ln-holding", "cn-holding"], "hcn": ["hcn-holding"], "break": ["hcn-released"]}
    for kind, width, height, scale_y in (("base", 5 / 12, 1.4, 1), ("cn", 5 / 12, 1.4, 1),
                                         ("hcn", 1.25, 4, 1.25), ("break", 5 / 6, 10 / 3, 1)):
        art = node(kind + ".art", "recorded-animation", {"x": .5, "y": .5, "origin": "centre",
                   "width": width, "height": height, "scale-y": scale_y}, animation=kind)
        children.append(node(kind, properties={"x": .125, "y": 17.5 / 90, "origin": "centre"}, children=[art]))
        if kind in appearance:
            variants.append({"target": kind, "property": "visible", "source": "object.long-note-appearance",
                             "default": False, "cases": [{"key": a, "value": True} for a in appearance[kind]]})
    root = node("effect", properties={"width": 4, "height": 4}, children=children)
    scene = {"templates": [{"id": "t", "root": root}], "instances": [{"template": "t",
             "material": {"slot": "effect.long-note", "resource": "marker"}}], "variants": variants,
             "bindings": [{"target": "base", "property": p, "source": "option.explosion-scale"} for p in ("scale-x", "scale-y")]}
    programs = {name: {"trigger": "object.long-note-transition" if name == "base" else "object.state",
                      "loop": name != "base", "fps": 60, "frames": [{}] * (27 if name in ("base", "break") else 25)}
                for name in ("base", "cn", "hcn", "break")}
    contribution = Contribution("charge-notes", "c00", "manifest.json", "scene.json", None,
                   {"animations": [{"id": name, "path": name} for name in programs]}, scene, "")
    lane = ({"mode": "Provide", "slot": "effect.long-note", "resource": "marker"}, deck, key,
            (100, 100, 60, 400), (100, 500, 60, 20), (100, 485, 60, 15))
    return SimpleNamespace(json=lambda path: programs[path]), contribution, [lane], programs


def plan_for(deck=0, key=1, scale=1):
    author, part, lanes, programs = fixture(deck, key)
    return hold_plan(author, part, {"explosion-scale": scale}, "p1", lanes), programs


def destinations(plan, programs):
    result = []
    for draw in plan["draws"]:
        p = programs[draw["animation"]]
        count = len(p["frames"]) - 1
        clock = f"animation_frame({draw['timer']},60,{count},{0 if p['loop'] else -1},0)"
        condition = " and ".join("(" + item + ")" for item in draw["conditions"]) or "true"
        result.append({"id": draw["animation"], "draw": Lua(
            "function() local frame=" + clock + f"; return frame>=0 and frame<{count} and ({condition}) end")})
    return result


def native_cases():
    plan, programs = plan_for()
    def case(name, steps, initial=None):
        return {"name": name, "skin": {"customTimers": plan["customTimers"], "destination": destinations(plan, programs)},
                "initial": {"now": 0, "play": 1, "timers": {}, **(initial or {})}, "steps": steps}
    def step(now, origin, timers=None, visible=(), **patch):
        return {"state": {"now": now, **({"timers": timers} if timers is not None else {}), **patch},
                "expected": {10101: origin}, "draws": {name: name in visible for name in programs}}
    cases = [case("LN/CN real HOLD, ordinary key ignored, one-shot exit", [
        step(0, OFF), step(1000, OFF, {101: 1000}),
        step(2000, 2000, {71: 2000}, ("base", "cn")),
        step(500000, 2000, visible=("cn",)),
        step(510000, 510000, {}, ("base",)), step(700000, 510000, visible=("base",)),
        step(1000000, 510000), step(1010000, 1010000, {71: 1010000}, ("base", "cn")),
        step(1020000, 1020000, {71: 1020000}, ("base", "cn")),
    ]), case("HCN priority, looping damage, actual recovery", [
        step(0, OFF), step(1000, 1000, {71: 1000, 251: 1000}, ("base", "hcn")),
        step(2000, 2000, {71: 1000, 271: 2000}, ("base", "break")),
        step(700000, 2000, {71: 1000, 251: 1000, 271: 2000}, ("break",)),
        step(710000, 710000, {71: 710000, 251: 710000}, ("base", "hcn")),
        step(720000, 720000, {}, ("base",)), step(1500000, 720000),
    ]), case("Snapshots, retry, rewind establish a baseline", [
        step(10000, OFF, {71: 5000}, ("cn",)), step(20000, OFF, visible=("cn",)),
        step(30000, 30000, {}, ("base",)),
        step(40000, OFF, {71: 40000}, ("cn",), play=2),
        step(1000, OFF, {71: 500}, ("cn",)), step(2000, 2000, {}, ("base",)),
        step(3000, OFF, {}, play=OFF), step(4000, OFF, {}, play=3),
    ]), case("Early HCN follows generic HOLD until actual ACTIVE", [
        step(0, OFF), step(1000, 1000, {71: 1000}, ("base", "cn")),
        step(3000, 1000, {71: 1000, 251: 3000}, ("base", "hcn")),
        step(500000, 1000, visible=("hcn",)),
    ]), case("Unheld HCN miss and DAMAGE expiry never invent a holding transition", [
        step(0, OFF), step(1000, OFF, {271: 1000}, ("break",)),
        step(700000, OFF, {271: 1000}, ("break",)), step(710000, OFF, {}),
        step(720000, 720000, {71: 720000, 251: 720000}, ("base", "hcn")),
        step(730000, 730000, {271: 730000}, ("base", "break")),
        step(740000, 730000, {}, ("base",)), step(1200000, 730000),
    ])]
    timers = [transition_timer(deck, key) for deck in range(2) for key in range(8)]
    expected = {row["id"]: OFF for row in timers}
    cases.append({"name": "All DP keys and scratches remain independent", "skin": {"customTimers": timers},
                  "initial": {"now": 0, "play": 1, "timers": {}}, "steps": [
                      {"state": {}, "expected": expected},
                      {"state": {"now": 5000, "timers": {70: 5000, 261: 5000}},
                       "expected": {**expected, 10100: 5000, 10111: 5000}},
                      {"state": {"now": 6000, "timers": {70: 5000, 281: 6000}},
                       "expected": {**expected, 10100: 5000, 10111: 6000}}]})
    return cases


class HoldTests(unittest.TestCase):
    def test_source_ancestors_keep_position_and_only_base_scales(self):
        first, _ = plan_for(scale=1)
        second, _ = plan_for(scale=1.25)
        a, b = ({d["animation"]: d for d in plan["draws"]} for plan in (first, second))
        self.assertEqual((100, 60 * 1.4), (a["base"]["rect"][2], a["base"]["rect"][3]))
        for draw in first["draws"]:
            x, y, w, h = draw["rect"]
            self.assertAlmostEqual(130, x + w / 2)
            self.assertAlmostEqual(485 + 60 * 17.5 / 90, y + h / 2)
            self.assertEqual([3], draw["offsets"])
        self.assertAlmostEqual(125, b["base"]["rect"][2])
        for name in ("cn", "hcn", "break"):
            self.assertEqual(a[name]["rect"], b[name]["rect"])

    def test_real_loop_metadata_is_required_and_tail_has_no_hold_gate(self):
        plan, _ = plan_for()
        self.assertEqual({"base": 10101, "cn": 71, "hcn": 251, "break": 271},
                         {d["animation"]: d["timer"] for d in plan["draws"]})
        self.assertEqual([], plan["draws"][0]["conditions"])
        author, part, lanes, programs = fixture()
        programs["break"]["loop"] = False
        with self.assertRaisesRegex(ValueError, "trigger/loop"):
            hold_plan(author, part, {"explosion-scale": 1}, "p1", lanes)

    def test_unavailable_object_type_or_empty_provide_is_not_silently_accepted(self):
        author, part, lanes, _ = fixture()
        part.scene["variants"][0]["cases"][0]["value"] = False
        with self.assertRaisesRegex(ValueError, "distinguish LN from CN"):
            hold_plan(author, part, {"explosion-scale": 1}, "p1", lanes)
        part.scene["instances"] = []
        with self.assertRaisesRegex(ValueError, "no source template"):
            hold_plan(author, part, {"explosion-scale": 1}, "p1", lanes)
        self.assertEqual({"customTimers": [], "draws": [], "gaps": []}, hold_plan(author, part, {}, "p1", []))

    def test_official_native_timer_cache_and_lua_animation_lifecycle(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac = shutil.which("javac")
        java = shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        root = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-hold-tests"
        root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=root) as temporary:
            folder = Path(temporary)
            recipe = folder / "cases.lua"
            cases = native_cases()
            recipe.write_text(ANIMATION_RUNTIME + "\nreturn " + lua(cases), encoding="utf-8")
            compile_result = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(folder),
                                            str(Path(__file__).with_name("HoldTimerProbe.java"))],
                                            capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(0, compile_result.returncode, compile_result.stdout + compile_result.stderr)
            result = subprocess.run([java, "-cp", str(folder) + os.pathsep + str(jar),
                                     "bms.player.beatoraja.skin.lua.HoldTimerProbe", str(recipe)],
                                    capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            receipt = json.loads(result.stdout)
            self.assertEqual(len(cases), receipt["scenarios"])
            self.assertGreater(receipt["gateAssertions"], 100)
            print("Native hold probe: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
