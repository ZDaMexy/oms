"""Source feedback selection plus real official-JAR timer execution, without GPU.

Set BEATORAJA_JAR to an installed 0.8.9 beatoraja.jar when it is not at the
local D:/beatoraja location. The Java check skips only if that JAR or JDK is
absent; it compiles/runs solely under this checkout's development cache.
"""

from copy import deepcopy
import json
import math
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution, walk_nodes
from beatoraja_feedback import GAUGE_TIMER, SCORE_TIMERS, feedback_plan, gauge_timer, score_timer
from beatoraja_native import Lua, lua
from standard_graph import _Graph, _add_notifications, node


OFF = -(2 ** 63)
CANVAS = (0, 0, 1280, 720)


def contribution(module, manifest, scene, ini=""):
    return Contribution(module, "fixture", module + "/manifest.json", None, None, manifest, scene, ini)


def graph_fixture(side="p1"):
    # Use the original author's actual notification-tree builder, without
    # extracting or retaining any private source artwork in this test.
    graph = _Graph.__new__(_Graph)
    graph.prefix, graph.layout = "fixture.graph." + side, side
    graph.left, graph.depths = (0 if side == "p2" else 1000), {}
    graph.value = {key: [] for key in ("resources", "animations", "templates", "instances",
                                     "bindings", "variants", "tracks", "numericSources")}
    clips = {name: {"resources": [], "animations": [{"id": name, "path": name + ".json"}],
                    "node": {"id": name, "type": "recorded-animation", "animation": name}}
             for name in SCORE_TIMERS}
    _add_notifications(graph, "score.rank-aaa", clips)
    target = contribution("graph-target", {"animations": graph.value["animations"]}, graph.value)
    mode = "14k" if side == "dp" else "7k"
    ini = (f'[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode={mode} presentation={side}\n'
           f'decoration: resource Provide "markers/frame-{side}-information"')
    frame = contribution("frame", {}, {}, ini)
    empty = contribution("base", {}, {})
    programs = {name + ".json": {"trigger": "score.crossing"} for name in SCORE_TIMERS}
    author = SimpleNamespace(family="standard", base=lambda: empty,
                             contribution=lambda name, options: frame if name == "frame" else target,
                             json=lambda path: programs[path])
    options = {"graph-visible": True, "graph-cutin": True}
    return author, options, target, programs


def gauge_fixture():
    def movie(identifier, animation):
        return {"id": identifier, "type": "recorded-animation", "animation": animation, "properties": {}}
    initial = node("feedback.initial", children=[movie("initial.movie", "sg.f26.loop.clip.initial")])
    increased = node("feedback.increased", children=[movie("loop.movie", "sg.f26.loop.clip")])
    clear = node("feedback.clear", children=[initial, increased])
    uncleared = node("feedback.uncleared", children=[movie("burst.movie", "sg.f26.clip")])
    root = node("information", children=[
        node("unrelated.readout"), uncleared, clear,
        movie("unrelated.attach", "sr.ready.clip")])
    scene = {"templates": [{"id": "information.template", "root": root}],
             "instances": [{"template": "information.template", "material": {"slot": "decoration", "resource": "marker"}}],
             "bindings": [{"target": "feedback.increased", "property": "visible", "source": "gauge.increase-available"}],
             "variants": [
                 {"target": "feedback.uncleared", "property": "visible", "source": "gauge.clear-state", "default": False,
                  "cases": [{"key": "uncleared", "value": True}]},
                 {"target": "feedback.clear", "property": "visible", "source": "gauge.clear-state", "default": False,
                  "cases": [{"key": "clear", "value": True}]},
                 {"target": "feedback.initial", "property": "visible", "source": "gauge.increase-available", "default": True,
                  "cases": [{"key": "true", "value": False}]}]}
    triggers = {"sg.f26.clip": "gauge.increase", "sg.f26.loop.clip": "gauge.increase",
                "sg.f26.loop.clip.initial": "gameplay.attach", "sr.ready.clip": "gameplay.attach"}
    manifest = {"animations": [{"id": key, "path": key + ".json"} for key in triggers]}
    frame = contribution("frame", manifest, scene,
        '[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=7k presentation=p1\ndecoration: resource Provide "marker"')
    programs = {key + ".json": {"trigger": value} for key, value in triggers.items()}
    return SimpleNamespace(family="standard", contribution=lambda name, options: frame,
                           json=lambda path: programs[path]), frame


def step(now, expected, **state):
    return {"state": {"now": now, **state}, "expected": expected}


def timer_case(name, timers, steps, **initial):
    return {"name": name, "skin": {"customTimers": timers},
            "initial": {"now": 0, "play": 1, "gauge": 20, "kind": 2, "score": 0,
                        "best": 100, "rival": 200, "notes": 900, "qualified": False, **initial},
            "steps": steps}


def condition_destinations(plan):
    return [{"id": row["animation"], "draw": Lua("function() return " +
             (" and ".join("(" + value + ")" for value in row["conditions"]) or "true") + " end")}
            for row in plan["draws"]]


def native_probe_cases():
    cases = []
    for name, field in (("mybest", "best"), ("target", "rival")):
        identifier = SCORE_TIMERS[name]
        cases.append(timer_case(name + " strict equality", [score_timer(name)], [
            step(1000, {identifier: OFF}, score=399),
            step(2000, {identifier: OFF}, score=400),
            step(3000, {identifier: 3000}, score=401),
            step(4000, {identifier: 3000}, score=450),
        ], **{field: 400}))
        cases.append(timer_case(name + " zero is unavailable", [score_timer(name)], [
            step(1000, {identifier: OFF}), step(2000, {identifier: OFF}, score=500),
            step(3000, {identifier: OFF}, **{field: 400}),
            step(4000, {identifier: OFF}, score=501),
        ], **{field: 0}))
    for rank, weight in (("a", 6), ("aa", 7), ("aaa", 8)):
        for notes in (9, 10):
            identifier, threshold = SCORE_TIMERS[rank], math.ceil(notes * 2 * weight / 9)
            cases.append(timer_case(rank + " inclusive " + str(notes), [score_timer(rank)], [
                step(1000, {identifier: OFF}, score=threshold - 1),
                step(2000, {identifier: 2000}, score=threshold),
                step(3000, {identifier: 2000}, score=threshold + 1),
            ], notes=notes))
    identifier = SCORE_TIMERS["mybest"]
    cases.append(timer_case("snapshot and score rollback", [score_timer("mybest")], [
        step(1000, {identifier: OFF}, score=120), step(2000, {identifier: OFF}, score=130),
        step(3000, {identifier: OFF}, score=110), step(4000, {identifier: OFF}, score=111),
        step(5000, {identifier: OFF}, score=99), step(6000, {identifier: 6000}, score=101),
    ]))
    cases.append(timer_case("play stop and retry", [score_timer("mybest")], [
        step(1000, {identifier: OFF}, score=99), step(2000, {identifier: 2000}, score=101),
        step(3000, {identifier: OFF}, play=OFF), step(4000, {identifier: OFF}, play=4),
        step(5000, {identifier: OFF}, score=102),
        step(6000, {identifier: OFF}, play=6, score=99), step(7000, {identifier: 7000}, score=101),
    ]))
    cases.append(timer_case("time rewind", [score_timer("mybest")], [
        step(10000, {identifier: OFF}, score=99), step(20000, {identifier: 20000}, score=101),
        step(10000, {identifier: OFF}, score=102), step(30000, {identifier: OFF}, score=103),
    ]))
    cases.append(timer_case("threshold changes are metadata", [score_timer("mybest")], [
        step(1000, {identifier: OFF}, score=99), step(2000, {identifier: 2000}, score=101),
        step(3000, {identifier: OFF}, best=80), step(4000, {identifier: OFF}, score=102),
        step(5000, {identifier: OFF}, best=150), step(6000, {identifier: OFF}, score=150),
        step(7000, {identifier: 7000}, score=151),
    ]))
    cases.append(timer_case("maximum changes invalidate old origins", [score_timer("mybest")], [
        step(1000, {identifier: OFF}, score=99), step(2000, {identifier: 2000}, score=101),
        step(3000, {identifier: OFF}, notes=1000), step(4000, {identifier: OFF}, score=102),
        step(5000, {identifier: OFF}, score=99), step(6000, {identifier: 6000}, score=101),
    ]))
    for notes in (0, 2147483647):
        cases.append(timer_case("missing maximum " + str(notes), [score_timer("mybest")], [
            step(1000, {identifier: OFF}, score=99), step(2000, {identifier: OFF}, score=101),
            step(3000, {identifier: OFF}, notes=900), step(4000, {identifier: OFF}, score=102),
        ], notes=notes))
    all_timers = [score_timer(name) for name in SCORE_TIMERS]
    all_off = {identifier: OFF for identifier in SCORE_TIMERS.values()}
    cases.append(timer_case("simultaneous thresholds and shared update", all_timers, [
        step(1000, all_off, score=1199),
        step(2000, {identifier: 2000 for identifier in SCORE_TIMERS.values()}, score=1601),
        step(3000, {identifier: 2000 for identifier in SCORE_TIMERS.values()}, score=1700),
    ], best=1200, rival=1400))
    aggregate = SCORE_TIMERS["update"]
    cases.append(timer_case("aggregate restarts at the next distinct boundary", [score_timer("update")], [
        step(1000, {aggregate: OFF}, score=99), step(2000, {aggregate: 2000}, score=101),
        step(3000, {aggregate: 3000}, score=201), step(4000, {aggregate: 3000}, score=1199),
        step(5000, {aggregate: 5000}, score=1200), step(6000, {aggregate: 6000}, score=1400),
        step(7000, {aggregate: 7000}, score=1600), step(8000, {aggregate: 7000}, score=1601),
    ]))
    cases.append(timer_case("aggregate threshold change resets baseline", [score_timer("update")], [
        step(1000, {aggregate: OFF}, score=99), step(2000, {aggregate: 2000}, score=101),
        step(3000, {aggregate: OFF}, rival=50), step(4000, {aggregate: OFF}, score=102),
        step(5000, {aggregate: 5000}, score=1200),
    ]))
    cases.append(timer_case("gauge actual observed gains", [gauge_timer()], [
        step(1000, {GAUGE_TIMER: OFF}), step(2000, {GAUGE_TIMER: 2000}, gauge=20.01),
        step(3000, {GAUGE_TIMER: 2000}, gauge=20.01),
        step(4000, {GAUGE_TIMER: 2000}, gauge=10),
        step(5000, {GAUGE_TIMER: 5000}, gauge=100), step(6000, {GAUGE_TIMER: 5000}, gauge=100),
    ]))
    cases.append(timer_case("gauge snapshot type retry rewind", [gauge_timer()], [
        step(1000, {GAUGE_TIMER: OFF}, gauge=80), step(2000, {GAUGE_TIMER: 2000}, gauge=81),
        step(3000, {GAUGE_TIMER: OFF}, kind=3, gauge=100),
        step(4000, {GAUGE_TIMER: OFF}, gauge=90), step(5000, {GAUGE_TIMER: 5000}, gauge=91),
        step(6000, {GAUGE_TIMER: OFF}, play=OFF), step(7000, {GAUGE_TIMER: OFF}, play=7, gauge=92),
        step(8000, {GAUGE_TIMER: 8000}, gauge=93), step(4000, {GAUGE_TIMER: OFF}, gauge=94),
        step(9000, {GAUGE_TIMER: 9000}, gauge=95),
    ]))
    cases.append(timer_case("gauge unsampled intermediate gain is not fabricated", [gauge_timer()], [
        step(1000, {GAUGE_TIMER: OFF}, gauge=50),
        step(2000, {GAUGE_TIMER: OFF}, gauge=49),
        step(3000, {GAUGE_TIMER: OFF}, gauge=49),
        step(4000, {GAUGE_TIMER: 4000}, gauge=49.5),
    ]))

    author, options, _, _ = graph_fixture()
    plan = feedback_plan(author, options, "7k", "p1", CANVAS, "graph")
    graph_case = timer_case("source rank gates and target independence", plan["customTimers"], [], best=100, rival=200)
    graph_case["skin"]["destination"] = condition_destinations(plan)
    for now, score, notes in ((1000, 1199, 900), (2000, 1200, 900), (3000, 1400, 900),
                              (4000, 1600, 900), (5000, 1700, 900), (6000, 1700, 0),
                              (7000, 1700, 2147483647)):
        valid = 0 < notes < 2147483647
        graph_case["steps"].append({"state": {"now": now, "score": score, "notes": notes},
            "draws": {name: valid and (name != "aa" or score < 1600) and (name != "a" or score < 1400)
                      for name in SCORE_TIMERS}})
    cases.append(graph_case)

    author, _ = gauge_fixture()
    plan = feedback_plan(author, {}, "7k", "p1", CANVAS, "gauge")
    gauge_case = timer_case("native qualification and initial loop handoff", plan["customTimers"], [])
    gauge_case["skin"]["destination"] = condition_destinations(plan)
    for now, gauge, qualified, origin in ((1000, 20, False, OFF), (2000, 21, False, 2000),
                                          (3000, 22, True, 3000), (4000, 20, False, 3000)):
        gauge_case["steps"].append({"state": {"now": now, "gauge": gauge, "qualified": qualified},
            "expected": {GAUGE_TIMER: origin}, "draws": {"sg.f26.clip": not qualified,
                "sg.f26.loop.clip.initial": qualified and origin == OFF,
                "sg.f26.loop.clip": qualified and origin != OFF}})
    gauge_case["steps"].append({"state": {"now": 5000, "play": 2, "qualified": True},
        "expected": {GAUGE_TIMER: OFF}, "draws": {"sg.f26.clip": False,
            "sg.f26.loop.clip.initial": True, "sg.f26.loop.clip": False}})
    cases.append(gauge_case)
    return cases


class FeedbackTests(unittest.TestCase):
    def test_original_graph_panel_and_notification_layer_order_all_layouts(self):
        for side in ("p1", "p2", "dp"):
            with self.subTest(side=side):
                author, options, target, _ = graph_fixture(side)
                plan = feedback_plan(author, options, "14k" if side == "dp" else "7k", side, CANVAS, "graph")
                self.assertEqual(["update", "mybest", "target", "aaa", "aa", "a"], [d["animation"] for d in plan["draws"]])
                self.assertEqual({(0 if side == "p2" else 1000, 0, 280, 720)}, {d["rect"] for d in plan["draws"]})
                self.assertEqual([t["id"] for t in target.scene["templates"]], plan["handled_template_ids"])
                self.assertEqual(set(SCORE_TIMERS.values()), {t["id"] for t in plan["customTimers"]})

    def test_static_graph_controls_hide_source_without_leaking_generic_art(self):
        author, options, _, _ = graph_fixture()
        for control in options:
            plan = feedback_plan(author, {**options, control: False}, "7k", "p1", CANVAS, "graph")
            self.assertEqual([], plan["draws"])
            self.assertEqual([], plan["customTimers"])
            self.assertEqual(1, len(plan["handled_root_ids"]))

    def test_feedback_does_not_mutate_original_scene_or_program(self):
        author, options, target, programs = graph_fixture()
        before = deepcopy((target.scene, programs, options))
        feedback_plan(author, options, "7k", "p1", CANVAS, "graph")
        self.assertEqual(before, (target.scene, programs, options))

    def test_changed_original_comparison_and_unrelated_trigger_are_rejected(self):
        author, options, target, programs = graph_fixture()
        aaa = next(n for n in walk_nodes(target.scene["templates"][0]["root"]) if n["id"].endswith(".aaa"))
        aaa["scoreCrossing"]["thresholds"][0]["comparison"] = "greater"
        with self.assertRaisesRegex(ValueError, "comparison"):
            feedback_plan(author, options, "7k", "p1", CANVAS, "graph")
        aaa["scoreCrossing"]["thresholds"][0]["comparison"] = "at-least"
        programs["aaa.json"]["trigger"] = "gameplay.attach"
        with self.assertRaisesRegex(ValueError, "unrelated trigger"):
            feedback_plan(author, options, "7k", "p1", CANVAS, "graph")

    def test_gauge_claims_only_feedback_and_preserves_initial_and_gain_clocks(self):
        author, _ = gauge_fixture()
        plan = feedback_plan(author, {}, "7k", "p1", CANVAS, "gauge")
        self.assertEqual({"feedback.uncleared", "feedback.clear"}, set(plan["handled_node_ids"]))
        self.assertEqual([], plan["handled_root_ids"])
        self.assertEqual({"sg.f26.clip": GAUGE_TIMER, "sg.f26.loop.clip": GAUGE_TIMER,
                          "sg.f26.loop.clip.initial": 0}, {d["animation"]: d["timer"] for d in plan["draws"]})
        self.assertEqual([GAUGE_TIMER], [timer["id"] for timer in plan["customTimers"]])

    def test_lightning_does_not_adopt_standard_gauge_feedback(self):
        author, _ = gauge_fixture()
        author.family = "lightning"
        plan = feedback_plan(author, {}, "7k", "p1", CANVAS, "gauge")
        self.assertEqual([], plan["draws"])
        self.assertEqual([], plan["handled_node_ids"])

    def test_native_jar_luaj_custom_timer_lifecycle_and_source_gates(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required for native execution")
        root = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-feedback-tests"
        root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=root) as temporary:
            folder = Path(temporary)
            recipe = folder / "feedback-cases.lua"
            cases = native_probe_cases()
            recipe.write_text("return " + lua(cases), encoding="utf-8")
            source = Path(__file__).with_name("FeedbackTimerProbe.java")
            compile_result = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(folder), str(source)],
                                            capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(0, compile_result.returncode, compile_result.stdout + compile_result.stderr)
            result = subprocess.run([java, "-cp", str(folder) + os.pathsep + str(jar),
                                     "bms.player.beatoraja.skin.lua.FeedbackTimerProbe", str(recipe)],
                                    capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            receipt = json.loads(result.stdout)
            self.assertEqual(len(cases), receipt["scenarios"])
            self.assertGreater(receipt["timerAssertions"], 100)
            self.assertGreater(receipt["gateAssertions"], 50)
            print("Native feedback probe: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
