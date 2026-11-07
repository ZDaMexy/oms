"""Preparation lifecycle through the official 0.8.9 Lua/native timer cache.

Only read-only public-state fixtures are supplied; no game, GPU or player data
is opened. Private author checks inspect declarations, never copy artwork.
"""

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

from beatoraja_author import AuthorPackage, walk_nodes
from beatoraja_native import Lua, lua
from beatoraja_preparation import (ACTIVE_TIMER, PREPARATION_TIMER, PRESENTATION_TIMER,
                                   READY_TIMER, preparation_expression, preparation_plan)


OFF = -(2 ** 63)
EXPRESSION_TIMERS = {
    "preparation.available": PREPARATION_TIMER,
    "preparation.active": ACTIVE_TIMER,
    "preparation.ready": READY_TIMER,
    "preparation.elapsed": PREPARATION_TIMER,
    "presentation.elapsed": PRESENTATION_TIMER,
    "preparation.ready-elapsed": READY_TIMER,
    "preparation.remaining": ACTIVE_TIMER,
}


def micro(milliseconds):
    return round(milliseconds * 1000)


def sample(now, *, preparation=None, presentation=None, ready_elapsed=None, active_until=None, **state):
    current = micro(now)
    patch = {"now": current, **{key: (value if key in ("practice", "loading") or value == OFF else micro(value))
                               for key, value in state.items()}}
    elapsed = {PREPARATION_TIMER: preparation, PRESENTATION_TIMER: presentation, READY_TIMER: ready_elapsed}
    expected = {identifier: OFF if value is None else current - micro(value)
                for identifier, value in elapsed.items()}
    expected[ACTIVE_TIMER] = OFF if active_until is None else micro(active_until)
    return {"state": patch, "expected": expected,
            "draws": {"preparation.available": preparation is not None,
                      "preparation.active": active_until is not None,
                      "preparation.ready": ready_elapsed is not None},
            "numbers": {"preparation.elapsed": micro(preparation or 0),
                        "presentation.elapsed": micro(presentation or 0),
                        "preparation.ready-elapsed": micro(ready_elapsed or 0),
                        "preparation.remaining": 0 if active_until is None else max(0, micro(active_until) - current)}}


def case(name, duration, steps):
    plan = preparation_plan(SimpleNamespace(scene={"preparationDuration": duration}))
    skin = {"customTimers": plan["customTimers"], "destination": [], "value": []}
    for source in EXPRESSION_TIMERS:
        expression, kind = preparation_expression(source)
        if kind == "boolean":
            skin["destination"].append({"id": source, "draw": Lua("function() return " + expression + " end")})
        else:
            # IntegerProperty preserves microsecond comparisons here; rounding
            # belongs only to the probe, not the exported source expression.
            skin["value"].append({"id": source, "value": Lua("function() return math.floor((" + expression + ")*1000+0.5) end")})
    return {"name": name, "skin": skin, "expressionTimers": EXPRESSION_TIMERS,
            "initial": {"now": 0, "ready": OFF, "play": OFF, "fade": OFF, "failure": OFF,
                        "practice": False, "loading": False}, "steps": steps}


def native_cases():
    result = [case("5000ms normal, exact Ready boundaries and normal finish", 5000, [
        sample(0, loading=True),
        sample(100, loading=False, ready=100, preparation=0, presentation=0, active_until=5100),
        sample(1000, preparation=900, presentation=900, active_until=5100),
        sample(3599.999, preparation=3499.999, presentation=3499.999, active_until=5100),
        sample(3600, preparation=3500, presentation=3500, ready_elapsed=0, active_until=5100),
        sample(5099.999, preparation=4999.999, presentation=4999.999, ready_elapsed=1499.999, active_until=5100),
        sample(5100, preparation=5000, presentation=5000, ready_elapsed=1500, active_until=5100),
        sample(5100.999, preparation=5000.999, presentation=5000.999, ready_elapsed=1500, active_until=5100),
        sample(5101, play=5101, preparation=5001, presentation=5001),
        sample(6101, preparation=5001, presentation=6001),
        sample(10000, fade=10000, preparation=5001, presentation=9900),
        sample(11000, preparation=5001, presentation=10900),
    ])]
    for duration in (1000, 1500, 2500, 3000, 10000):
        end = duration + 20
        result.append(case("declared duration " + str(duration), duration, [
            sample(20, ready=20, preparation=0, presentation=0,
                   ready_elapsed=1500-duration if duration <= 1500 else None, active_until=end),
            sample(max(20, end-1500), preparation=max(0, duration-1500), presentation=max(0, duration-1500),
                   ready_elapsed=max(0, 1500-duration), active_until=end),
            sample(end, preparation=duration, presentation=duration, ready_elapsed=1500, active_until=end),
            sample(end+1, play=end+1, preparation=duration+1, presentation=duration+1),
            sample(end+101, preparation=duration+1, presentation=duration+101),
        ]))
    result.append(case("practice offset, changing speed, failure and new practice epoch", 5000, [
        sample(10, practice=True, ready=5),
        sample(200, practice=False, ready=200, preparation=0, presentation=0, active_until=5200),
        sample(5201, play=-33799, preparation=5001, presentation=5001),  # chart begins at 39000 ms
        sample(5301, play=-33749, preparation=5001, presentation=5051),  # 50% elapsed
        sample(5401, play=-33849, preparation=5001, presentation=5251),  # 200% elapsed
        sample(5501, play=-33749, preparation=5001, presentation=5251),  # zero elapsed
        sample(5601, failure=5551, preparation=5001, presentation=5301),  # actual failure timestamp
        sample(5901, preparation=5001, presentation=5301),
        sample(6000, practice=True),
        sample(6080, play=OFF, failure=OFF),
        sample(7000, practice=False, ready=7000, preparation=0, presentation=0, active_until=12000),
        sample(12001, play=1001, preparation=5001, presentation=5001),
        sample(12301, preparation=5001, presentation=5301),
    ]))
    result.append(case("early exit does not revive old READY when fade clears", 3000, [
        sample(100, ready=100, preparation=0, presentation=0, active_until=3100),
        sample(1700, preparation=1600, presentation=1600, ready_elapsed=100, active_until=3100),
        sample(2000, fade=2000),
        sample(3000, fade=OFF),
        sample(4000, ready=4000, preparation=0, presentation=0, active_until=7000),
    ]))
    result.append(case("late PLAY snapshot must not invent preparation or practice offset", 3000, [
        sample(6000, ready=100, play=-20000),
        sample(7000),
        sample(8000, play=OFF),
        sample(9000, ready=9000, preparation=0, presentation=0, active_until=12000),
        sample(12001, play=-1999, preparation=3001, presentation=3001),
    ]))
    result.append(case("new READY timestamp missed before PLAY is not observed history", 3000, [
        sample(100, ready=100, preparation=0, presentation=0, active_until=3100),
        sample(3101, play=3101, preparation=3001, presentation=3001),
        sample(10000, ready=6000, play=9001),
        sample(11000, play=OFF),
        sample(12000, ready=12000, preparation=0, presentation=0, active_until=15000),
    ]))
    for gate in ("practice", "loading"):
        result.append(case("old READY surviving " + gate, 1500, [
            sample(100, ready=50, **{gate: True}),
            sample(200, **{gate: False}),
            sample(300, ready=300, preparation=0, presentation=0, ready_elapsed=0, active_until=1800),
            sample(1801, play=1801, preparation=1501, presentation=1501),
            sample(2000, **{gate: True}),
            sample(2100, play=OFF, **{gate: False}),
            sample(2200, ready=2200, preparation=0, presentation=0, ready_elapsed=0, active_until=3700),
        ]))
    result.append(case("time rewind invalidates old epoch", 3000, [
        sample(100, ready=100, preparation=0, presentation=0, active_until=3100),
        sample(2000, preparation=1900, presentation=1900, ready_elapsed=400, active_until=3100),
        sample(500), sample(550),
        sample(600, ready=600, preparation=0, presentation=0, active_until=3600),
    ]))
    result.append(case("PLAY disappears without a new READY", 1500, [
        sample(100, ready=100, preparation=0, presentation=0, ready_elapsed=0, active_until=1600),
        sample(1601, play=1601, preparation=1501, presentation=1501),
        sample(2000, play=OFF), sample(2100),
        sample(2200, ready=2200, preparation=0, presentation=0, ready_elapsed=0, active_until=3700),
    ]))
    result.append(case("failure before PLAY is cancellation, not retained preparation", 3000, [
        sample(100, ready=100, preparation=0, presentation=0, active_until=3100),
        sample(200, failure=200), sample(300, failure=OFF),
        sample(400, ready=400, preparation=0, presentation=0, active_until=3400),
    ]))
    result.append(case("host reset with OFF clocks permits a new epoch", 1500, [
        sample(100, ready=100, preparation=0, presentation=0, ready_elapsed=0, active_until=1600),
        sample(1601, play=1601, preparation=1501, presentation=1501),
        sample(0, ready=OFF, play=OFF), sample(20),
        sample(100, ready=100, preparation=0, presentation=0, ready_elapsed=0, active_until=1600),
    ]))
    return result


class PreparationTests(unittest.TestCase):
    def test_duration_declaration_and_absent_phase(self):
        for scene in ({}, {"preparationDuration": 0}):
            self.assertEqual({"customTimers": [], "playstart": 0, "gaps": []},
                             preparation_plan(SimpleNamespace(scene=scene)))
        for duration in (1500, 2500, 3000.0, 5000, 10000):
            plan = preparation_plan(SimpleNamespace(scene={"preparationDuration": duration}))
            self.assertEqual(duration, plan["playstart"])
            self.assertEqual([10020, 10021, 10022, 10023], [row["id"] for row in plan["customTimers"]])
            self.assertTrue(all(row["informational"] for row in plan["gaps"]))
        for duration in (True, "3000", -1, 10001, 1500.5, float("inf"), float("nan")):
            with self.subTest(duration=duration), self.assertRaises(ValueError):
                preparation_plan(SimpleNamespace(scene={"preparationDuration": duration}))
        self.assertIsNone(preparation_expression("timing.time"))

    def test_original_frame_declarations_and_ready_clips(self):
        roots = {
            "standard": Path(os.environ.get("OMS_STANDARD_AUTHOR", "E:/OMS-Development/sources/OMS-Standard-Model-24-20261107/author-r8")),
            "lightning": Path(os.environ.get("OMS_LIGHTNING_AUTHOR", "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1")),
        }
        if not all((root / "skin.ini").is_file() for root in roots.values()):
            self.skipTest("The two independent local author sources are required for source checks")
        frames, ready_nodes = 0, 0
        for family, path in roots.items():
            author = AuthorPackage(path, family)
            for choice in author.modules["frame"]["choices"]:
                for keymode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                    frame = author.contribution("frame", author.options(keymode, side, {"frame": choice["value"]}))
                    expected = 5000 if family == "lightning" else (1500 if choice["value"] in ("iidx23", "iidx24")
                        else 2500 if (choice["value"], side) == ("iidx03", "dp") else 3000)
                    self.assertEqual(expected, preparation_plan(frame)["playstart"], (family, choice["value"], side))
                    definitions = {item["id"]: item for item in frame.manifest["animations"]}
                    for root in [frame.scene["root"], *frame.templates.values()]:
                        for node in walk_nodes(root):
                            if node.get("clock") == "preparation.ready-elapsed":
                                program = author.json(definitions[node["animation"]]["path"])
                                self.assertEqual((60, 91, False), (program["fps"], len(program["frames"]), program["loop"]))
                                ready_nodes += 1
                    frames += 1
        self.assertEqual((81, 96), (frames, ready_nodes))

    def test_official_luaj_timer_cache_and_lifecycle(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required for native execution")
        root = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-preparation-dev"
        root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=root) as temporary:
            folder = Path(temporary)
            recipe = folder / "preparation-cases.lua"
            cases = native_cases()
            recipe.write_text("return " + lua(cases), encoding="utf-8")
            source = Path(__file__).with_name("PreparationTimerProbe.java")
            compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(folder), str(source)],
                                      capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
            result = subprocess.run([java, "-cp", str(folder) + os.pathsep + str(jar),
                                     "bms.player.beatoraja.skin.lua.PreparationTimerProbe", str(recipe)],
                                    capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
            self.assertEqual(0, result.returncode, result.stdout + result.stderr)
            receipt = json.loads(result.stdout)
            self.assertEqual(len(cases) * 5, receipt["scenarios"])
            self.assertEqual(sum(len(row["steps"]) for row in cases) * 8, receipt["timerChecks"])
            self.assertGreater(receipt["cacheChecks"], 1000)
            print("Native preparation probe: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
