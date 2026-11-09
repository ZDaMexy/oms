"""Original20 producer fields through native Graph gates, without private art."""
from copy import deepcopy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import Contribution
from beatoraja_graph import export_graph
from beatoraja_native import AssetSink, lua
from standard_graph import _Graph, _target_value, node, TARGETS, LOGICAL


class GraphAuthor:
    family = "standard"

    def __init__(self, root, keymode="7k", side="p1", choice="aaa"):
        self.root, self.parts = root, {}
        self.side, self.choice = side, choice
        self.left = 0 if side == "p2" else 1000
        resources, fonts = [], []
        for digit in range(10):
            path = root / f"digit{digit}.png"
            Image.new("RGBA", (23, 22), (digit * 20, 70, 90, 255)).save(path)
            resources.append({"id": f"own20.digit{digit}", "type": "texture", "path": path.name})
        Image.new("RGBA", (253, 22), (70, 80, 90, 255)).save(root / "signed.png")
        resources.append({"id": "signed", "type": "texture", "path": "signed.png"})
        for rank in ("aaa", "aa", "a"):
            Image.new("RGBA", (270, 15), (240, 240, 240, 255)).save(root / (rank + ".png"))
            resources.append({"id": "standard.graph.fs20.texture.gr-line-" + rank,
                              "type": "texture", "path": rank + ".png"})
        fonts.append({"id": "standard.graph.fs20.font.score", "lineHeight": 22,
            "glyphs": [{"character": str(i), "resource": f"own20.digit{i}", "advance": 22} for i in range(10)]})
        fonts.append({"id": "synthetic.signed", "lineHeight": 22,
            "glyphs": [{"character": c, "resource": "signed", "rect": [i * 23, 0, 23, 22], "advance": 22}
                       for i, c in enumerate("0123456789-")]})
        def graph(target=None):
            value = _Graph.__new__(_Graph)
            value.common_family, value.layout, value.left = "s20", side, self.left
            value.prefix = "standard.graph.fs20." + (side if target is None else f"target.{target}.{side}")
            value.score_font, value.score_advance = fonts[0]["id"], 22
            value.small_font = fonts[1]["id"]
            value.value = {key: [] for key in ("bindings", "tracks", "numericSources")}
            value.depths = {}
            value.asset = lambda name, size: ("standard.graph.fs20.texture.gr-line-" + name[8:]
                                              if name.startswith("gr_line_") else "own20.digit0")
            return value
        def part(module, graph, children, target=None, ini=""):
            inner = node(graph.prefix + ".available", children=children)
            graph.bind(inner["id"], "visible", "score.maximum-available")
            outer = node(graph.prefix + ".data", properties={"z": -30}, children=[inner])
            graph.bind(outer["id"], "visible", "option.graph-visible")
            return Contribution(module, target or module, "synthetic/manifest.json", "synthetic/scene.json", "synthetic/frame.ini",
                {"resources": resources, "bitmapFonts": fonts},
                {"root": node("unused.root"), "templates": [{"id": outer["id"] + ".template", "root": outer}],
                 "instances": [{"template": outer["id"] + ".template", "material": {"slot": "decoration", "resource": "own20-marker"}}],
                 **graph.value}, ini)
        ini = f'[BmsMaterial]\nTarget: global ruleset=bms keymode={keymode} presentation={side}\ndecoration: resource Provide "own20-marker"\n'
        current = graph()
        current_children = [current.score_total("current.value", "score.value", 178, 30, 89)]
        for rank in ("aaa", "aa", "a"):
            line = current.sprite("line." + rank, "gr_line_" + rank, (270, 15), 5, 529)
            line["properties"].update(colour="#8787ffff", opacity=1)
            current.track(line["id"], "y", "score.rank-" + rank + "-progress", [(0, 529 / 720), (1, 79 / 720)])
            source = current.numeric("rank-colour-" + rank, "subtract", "score.rank-" + rank, "score.value")
            current.track(line["id"], "opacity", source, [(0, 0), (1, 1)], "step")
            reached = deepcopy(line)
            reached["id"] += ".reached"
            reached["properties"].update(colour="#ffff00ff", opacity=0)
            current.track(reached["id"], "y", "score.rank-" + rank + "-progress", [(0, 529 / 720), (1, 79 / 720)])
            current.track(reached["id"], "opacity", source, [(0, 1), (1, 0)], "step")
            current_children.extend((line, reached))
        self.parts["frame"] = part("frame", current, current_children, ini=ini)
        target = graph(choice)
        source = _target_value(target, choice)
        total = target.score_total("total", source, 178, 57, 89)
        difference = target.caption("difference", "", 9, 656, 127)
        difference["properties"].update({"bitmap-font": fonts[1]["id"], "font-size": 22 * LOGICAL})
        target.bind(difference["id"], "text", "value." + target.prefix + ".value.difference")
        children = [total, difference, target.caption("label", TARGETS[choice], 9, 604, 127)]
        if choice == "custom":
            percent = target.caption("custom-percent", "", 7, 680, 45)
            target.bind(percent["id"], "text", "option.graph-custom-percent")
            children.append(percent)
        self.parts["graph-target"] = part("graph-target", target, children, choice)
        self.parts["base"] = Contribution("base", "base", None, None, None, {}, {"templates": [], "instances": []}, "")

    def base(self):
        return self.parts["base"]

    def contribution(self, name, options):
        return self.parts[name]


class Original20NativeGraphTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-standard20-graph"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temp.cleanup)
        self.root, self.case_number = Path(self.temp.name), 0

    def case(self, keymode="7k", side="p1", choice="aaa", change=None, frame="source20-gmframe0"):
        self.case_number += 1
        root = self.root / str(self.case_number)
        author_root, output = root / "author", root / "output"
        author_root.mkdir(parents=True)
        output.mkdir()
        author = GraphAuthor(author_root, keymode, side, choice)
        if change:
            change(author)
        result = export_graph(author, {"frame": frame, "graph-visible": True}, keymode, side,
            AssetSink(author_root, output), (0, 0, 1280, 720), layer="foreground")
        return result, author

    def test_all_targets_and_five_layouts_keep_same_host_sources_and_same_fit(self):
        for keymode, side in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
            for choice in TARGETS:
                result, _ = self.case(keymode, side, choice)
                self.assertFalse([g for g in result["gaps"] if g.get("reason") not in ("native-pb-existence-unavailable", "native-target-selection")])
                zeros = [d for d in result["destination"] if ".leading-zero.n" in d["id"]]
                self.assertEqual(30, len(zeros))
                for destination in zeros:
                    expression = destination["draw"]
                    expected = "main_state.exscore_rival()" if ".target." in destination["id"] else "main_state.exscore()"
                    self.assertIn(expected, expression)
                    self.assertIn("main_state.number(106) * 2", expression)
                    self.assertEqual(255, destination["dst"][0]["a"])
                numbers = result["arrays"]["value"]
                self.assertTrue(any(v["id"].endswith(".current.value.0") and "main_state.exscore()" in v["value"] for v in numbers))
                self.assertTrue(any(v["id"].endswith(".total.0") and "main_state.exscore_rival()" in v["value"] for v in numbers))

    def test_wrong_clock_threshold_glyph_and_fit_shape_are_explicitly_rejected(self):
        def tracks(author):
            return author.parts["frame"].scene["tracks"]
        def wrong_clock(author):
            next(t for t in tracks(author) if t["property"] == "opacity")["clock"] = "score.personal-best"
        def wrong_threshold(author):
            next(t for t in tracks(author) if t["property"] == "opacity")["keyframes"][1]["time"] = 11
        def wrong_fit(author):
            next(t for t in tracks(author) if t["property"] == "scale-x")["keyframes"][1]["value"] *= .9
        def wrong_glyph(author):
            root = author.parts["frame"].scene["templates"][0]["root"]
            root["children"][0]["children"][0]["children"][1]["children"][0]["resource"] = "own20.digit1"
        for change in (wrong_clock, wrong_threshold, wrong_fit, wrong_glyph):
            result, _ = self.case(change=change)
            with self.subTest(change=change):
                self.assertTrue(any(g.get("reason") == "unqualified-original20-leading-zero-field" for g in result["gaps"]))
                self.assertFalse(any(".current.value.leading-zero.n" in d["id"] for d in result["destination"]))

    def test_other_frame_cannot_claim_original20_dynamic_zero_field(self):
        for frame in ("source21-gmframe0", "source22-gmframe9", "iidx20"):
            result, _ = self.case(frame=frame)
            self.assertTrue(any(g.get("reason") == "unmapped-track" for g in result["gaps"]))
            self.assertFalse(any(".leading-zero.n" in d["id"] for d in result["destination"]))

    def test_zero_grid_requires_the_same_original_native_number_cells(self):
        def nonuniform(author):
            font = author.parts["frame"].manifest["bitmapFonts"][0]
            font["glyphs"][1]["advance"] = 23
        result, _ = self.case(change=nonuniform)
        self.assertTrue(any(g.get("reason") == "unqualified-original20-native-number-cells" for g in result["gaps"]))
        self.assertFalse(any(".leading-zero.n" in d["id"] for d in result["destination"]))

    def test_rank_line_has_two_exact_colours_same_y_and_no_timer_gate(self):
        for side in ("p1", "p2", "dp"):
            result, _ = self.case("14k" if side == "dp" else "7k", side)
            for rank in ("a", "aa", "aaa"):
                prefix = f"graph.frame.standard.graph.fs20.{side}.line.{rank}"
                entries = {"reached" if d["id"].endswith(".reached") else "pending": d for d in result["destination"]
                           if d["id"] in (prefix, prefix + ".reached")}
                self.assertEqual({"reached", "pending"}, set(entries))
                for state, channels in (("reached", (255, 255, 0, 255)), ("pending", (135, 135, 255, 255))):
                    entry = entries[state]
                    self.assertEqual(channels, tuple(entry["dst"][0][k] for k in ("r", "g", "b", "a")))
                    self.assertNotIn("timer", entry)
                    self.assertIn("main_state.exscore()", entry["draw"])
                self.assertEqual(entries["reached"]["dst"][0]["y"], entries["pending"]["dst"][0]["y"])

    def test_rank_colour_rejects_unknown_operand_frame_track_or_key(self):
        def operand(author):
            next(n for n in author.parts["frame"].scene["numericSources"] if "rank-colour-aaa" in n["id"])["right"] = "score.personal-best"
        def track_type(author):
            next(t for t in author.parts["frame"].scene["tracks"] if t["property"] == "opacity" and ".line.aaa." in t["id"])["type"] = "frame"
        def key(author):
            next(t for t in author.parts["frame"].scene["tracks"] if t["property"] == "opacity" and ".line.aaa." in t["id"])["keyframes"][1]["time"] = 2
        for change in (operand, track_type, key):
            result, _ = self.case(change=change)
            self.assertTrue(any(g.get("reason") == "unqualified-original20-rank-state" for g in result["gaps"]))
            self.assertFalse(any(d["id"].endswith((".line.aaa", ".line.aaa.reached")) for d in result["destination"]))

    def test_official_cpu_gates_and_destination_geometry(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        java, javac = shutil.which("java"), shutil.which("javac")
        if not jar.is_file() or not java or not javac:
            self.skipTest("Official0.8.9 JAR/JDK required")
        cases = []
        for keymode, side in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
            for choice in TARGETS:
                result, author = self.case(keymode, side, choice)
                cases.append({"name": keymode + side + choice, "left": author.left,
                    "skin": {"w": 1280, "h": 720, **result["arrays"], "destination": result["destination"]}})
        recipe = self.root / "cases.lua"
        recipe.write_text("return " + lua(cases), encoding="utf-8")
        compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.root),
            str(Path(__file__).with_name("Standard20GraphZeroProbe.java"))], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        run = subprocess.run([java, "-Xmx256M", "-cp", str(self.root) + os.pathsep + str(jar),
            "bms.player.beatoraja.skin.lua.Standard20GraphZeroProbe", str(recipe)], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, run.returncode, run.stdout + run.stderr)
        result = json.loads(run.stdout)
        self.assertEqual(30, result["cases"])
        self.assertGreater(result["gateChecks"], 10_000)
        self.assertGreater(result["geometryChecks"], 1_000)
        self.assertGreater(result["rankChecks"], 1_000)
        self.assertGreater(result["jointNativeCellChecks"], 1_000)
        self.assertLess(result["maxJointNativeCellErrorPx"], .0003)
        print("Original20 Graph official CPU: " + json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
