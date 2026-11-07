"""Real source qualification and official CPU rhythm/opacity lifecycle."""

from copy import deepcopy
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import AuthorPackage, material_bindings, material_templates, walk_nodes
from beatoraja_beat import BEAT_ADAPTATION, apply_beat_opacity, beat_opacity_track
from beatoraja_native import Lua, lua


OFF = -(2**63)
KEYS = [(0, .35), (.12, .28), (.7, .02), (1, .35)]


def fixture():
    node = {"id": "baseplate", "type": "sprite", "resource": "original",
            "properties": {"opacity": .35}, "children": []}
    track = {"type": "tween", "target": "baseplate", "property": "opacity", "clock": "timing.beat",
             "loop": True, "easing": "linear", "keyframes": [{"time": t, "value": v} for t, v in KEYS]}
    return node, {"tracks": [track]}, track


def destination(alpha=1):
    return {"id": "original", "dst": [{"time": 0, "x": 20, "y": 30, "w": 200, "h": 10,
             "r": 96, "g": 128, "b": 200, "a": round(alpha*255)}]}


def ideal(phase):
    fraction = (phase / 1_000_000) % 1
    for (ta, va), (tb, vb) in zip(KEYS, KEYS[1:]):
        if fraction < tb:
            return va + (vb-va)*(fraction-ta)/(tb-ta)
    raise AssertionError("Unexpected beat phase")


def sample(now, phase=0, timers=None):
    return {"now": now, "timers": timers or {}, "elapsedMs": phase//1000, "idealAlpha": ideal(phase)}


def native_cases():
    _, _, track = fixture()
    def case(name, steps):
        return {"name": name, "destination": apply_beat_opacity(destination(), track, 1), "steps": steps}
    cases = [case("READY, real phase, moving PLAY origin, same-render failure and restart", [
        sample(0), sample(1_000_000, timers={40: 1_000_000}),
        sample(4_000_000, timers={41: 4_000_000, 140: 4_000_000}),
        sample(4_060_000, 60_000),
        sample(4_100_000, 100_000, {41: 4_020_000}),
        sample(5_000_000, 1_120_000, {140: 3_880_000}),
        sample(5_125_000, 700_000, {140: 4_425_000, 3: 5_125_000}),
        sample(5_125_999, 700_000), sample(5_500_001, 700_000),
        sample(5_900_000, timers={41: OFF, 140: OFF}),
        sample(6_000_000, timers={40: 6_000_000, 3: OFF}),
        sample(7_000_000, 10_000, {41: 7_000_000, 140: 6_990_000}),
    ])]
    for terminal in (3, 908, 2):
        cases.append(case("late terminal freezes observed phase " + str(terminal), [
            sample(1_000_000, 120_000, {40: 0, 41: 500_000, 140: 880_000}),
            sample(1_400_000, 120_000, {terminal: 1_100_000, 140: 700_000}),
            sample(1_600_999, 120_000),
        ]))
        cases.append(case("first terminal snapshot never reconstructs history " + str(terminal), [
            sample(1_400_000, timers={40: 0, 41: 500_000, 140: 700_000, terminal: 1_100_000}),
            sample(1_800_999),
        ]))
    cases.append(case("new READY and time rewind discard old frozen phase", [
        sample(1_000_000, 120_000, {40: 0, 41: 500_000, 140: 880_000}),
        sample(1_400_000, 120_000, {3: 1_100_000}),
        sample(2_100_000, 700_000, {40: 2_000_000, 41: 2_000_000, 140: 1_400_000, 3: OFF}),
        sample(2_200_000, 800_000, {3: 2_200_000}),
        sample(1_000_000, 200_000, {41: 900_000, 140: 800_000, 3: OFF}),
    ]))
    cases.append(case("late PLAY needs no invented history", [
        sample(100_000_000, 1_700_000, {41: 50_000_000, 140: 98_300_000}),
        sample(100_010_000, 20_000, {140: 99_990_000}),  # actual section reset
        sample(100_020_000, timers={140: OFF}),
    ]))
    # Includes sub-millisecond boundaries and a negative synthetic timer origin;
    # only public timer values are read, never a current-BPM approximation.
    for start in (0, 4_000_001):
        phases = sorted({0, 1, 999, 1000, 119999, 120000, 120001, 699999, 700000,
                         700001, 999999, 1000000, 1000001, *range(0, 2_000_001, 997)})
        cases.append(case("native fractional-alpha grid " + str(start), [
            sample(start+phase, phase, {40: start, 41: start, 140: start}) for phase in phases]))
    return cases


class BeatTests(unittest.TestCase):
    def test_original_keys_replace_node_opacity_once_and_retain_other_fields(self):
        node, scene, track = fixture()
        self.assertIs(track, beat_opacity_track(node, scene))
        original = destination(.73)
        original.update(offsets=[3], op=[920], draw=Lua("function() return true end"), blend=2)
        original["dst"][0].update(clip_x=20, clip_y=30, clip_w=200, clip_h=10)
        result = apply_beat_opacity(original, track, .73)
        self.assertEqual([0, 120, 700, 1000], [p["time"] for p in result["dst"]])
        self.assertEqual([round(.73*v*255) for _, v in KEYS], [p["a"] for p in result["dst"]])
        for point in result["dst"]:
            self.assertEqual({k:v for k,v in original["dst"][0].items() if k not in ("time", "a")},
                             {k:v for k,v in point.items() if k not in ("time", "a", "acc")})
        for key in ("id", "offsets", "op", "draw", "blend"):
            self.assertEqual(original[key], result[key])
        self.assertEqual(round(.73*255), original["dst"][0]["a"])
        self.assertEqual("native-beat-opacity-adaptation", BEAT_ADAPTATION["code"])

    def test_unsupported_tracks_and_competing_sources_are_rejected(self):
        for change in ({"clock":"timing.time"}, {"loop":False}, {"property":"x"}, {"easing":"out"},
                       {"type":"frame"}, {"keyframes":[{"time":0,"value":.35},{"time":1,"value":.5}]},
                       {"keyframes":[{"time":0,"value":.35},{"time":.0001,"value":.3},{"time":1,"value":.35}]}):
            _, _, track = fixture(); track.update(change)
            with self.assertRaises(ValueError): apply_beat_opacity(destination(), track, 1)
        for kind in ("bindings", "variants"):
            node, scene, _ = fixture()
            scene[kind] = [{"target":"baseplate", "property":"opacity", "source":"option.x"}]
            with self.assertRaises(ValueError): beat_opacity_track(node, scene)
        node, scene, _ = fixture(); node["type"]="container"
        with self.assertRaises(ValueError): beat_opacity_track(node, scene)
        self.assertIsNone(beat_opacity_track({"id":"unrelated", "type":"sprite"}, scene))

    def test_conflicting_native_clock_and_double_alpha_are_rejected(self):
        _, _, track = fixture()
        for patch in ({"timer":140}, {"loop":120}, {"dst":[{"time":10,"a":255}]}):
            original = destination(); original.update(patch)
            with self.assertRaises(ValueError): apply_beat_opacity(original, track, 1)
        with self.assertRaises(ValueError): apply_beat_opacity(destination(.35), track, 1)
        with self.assertRaises(ValueError): apply_beat_opacity(destination(), track, float("nan"))

    def test_all_real_lightning_frame_routes_have_qualified_original_root(self):
        path = Path(os.environ.get("BEATORAJA_LIGHTNING_AUTHOR",
            "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1"))
        if not path.is_dir(): self.skipTest("Frozen private Lightning author is required")
        author = AuthorPackage(path, "lightning")
        routes = sources = instances = 0
        for choice in author.modules["frame"]["choices"]:
            for keymode, mode in (("7k","p1"),("7k","p2"),("14k","dp")):
                part = author.contribution("frame", author.options(keymode, mode, {"frame":choice["value"]}))
                roots = [part.scene["root"], *part.templates.values()]
                matches = [(n, beat_opacity_track(n, part.scene)) for r in roots for n in walk_nodes(r)
                           if any(t.get("target")==n["id"] and t.get("clock")=="timing.beat" for t in part.scene.get("tracks",[]))]
                self.assertEqual(1, len(matches))
                node, track = matches[0]
                self.assertIn(node, roots)
                self.assertEqual(KEYS, [(k["time"],k["value"]) for k in track["keyframes"]])
                resource = part.resources[node["resource"]]
                self.assertTrue((path/resource["path"]).is_file())
                for binding in material_bindings(part, keymode, mode):
                    if (binding["mode"]=="Provide" and binding["target"]["kind"]=="stage"
                            and binding["slot"]=="playfield.baseplate"
                            and node in material_templates(part, binding["slot"], binding["resource"])):
                        instances += 1
                routes += 1; sources += len(matches)
        self.assertEqual((45,45,60), (routes,sources,instances))

    def test_official_processor_lua_timer_and_native_alpha(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac = Path(os.environ.get("BEATORAJA_JAVAC", "C:/Program Files/Eclipse Adoptium/jdk-17.0.20.8-hotspot/bin/javac.exe"))
        java = Path(os.environ.get("BEATORAJA_JAVA", "D:/beatoraja/jre/bin/java.exe"))
        if not all(p.is_file() for p in (jar,javac,java)):
            self.skipTest("Official beatoraja JAR, JDK17 and runtime21 are required")
        root = Path(__file__).resolve().parents[3]/".dev-cache/temp/beatoraja-beat-dev"
        root.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=root) as temporary:
            folder = Path(temporary)
            recipe = folder/"beat-cases.lua"; cases=native_cases()
            recipe.write_text("return "+lua(cases),encoding="utf8")
            compiled = subprocess.run([str(javac),"-encoding","UTF-8","-cp",str(jar),"-d",str(folder),
                str(Path(__file__).with_name("BeatTimerProbe.java"))], capture_output=True,text=True,encoding="utf8",errors="replace",timeout=60)
            self.assertEqual(0,compiled.returncode,compiled.stdout+compiled.stderr)
            result = subprocess.run([str(java),"-cp",str(folder)+os.pathsep+str(jar),
                "bms.player.beatoraja.skin.lua.BeatTimerProbe",str(recipe)],capture_output=True,text=True,
                encoding="utf8",errors="replace",timeout=60)
            self.assertEqual(0,result.returncode,result.stdout+result.stderr)
            receipt=json.loads(result.stdout)
            self.assertEqual(12,receipt["rhythmChecks"])
            self.assertEqual(len(cases),receipt["scenarios"])
            self.assertEqual(sum(len(c["steps"]) for c in cases),receipt["colourChecks"])
            self.assertEqual(receipt["colourChecks"]*3,receipt["clockChecks"])
            (root/"native-receipt.json").write_text(json.dumps(receipt,indent=2),encoding="utf8")
            print("Official beat probe: "+json.dumps(receipt,sort_keys=True))


if __name__ == "__main__": unittest.main()
