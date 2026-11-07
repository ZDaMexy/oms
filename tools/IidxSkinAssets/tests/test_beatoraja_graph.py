"""Native Graph contract tests using small, local, non-author fixture images."""

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
from beatoraja_native import AssetSink, Lua, lua


def node(identifier, kind="container", properties=None, children=(), **extra):
    return {"id": identifier, "type": kind, "properties": properties or {},
            "children": list(children), **extra}


def binding(identifier, prop, source):
    return {"id": identifier + "." + prop, "target": identifier, "property": prop, "source": source}


def contribution(module, roots, bindings=(), variants=(), tracks=(), resources=(), fonts=(), ini="", choice=None):
    return Contribution(module, choice or ("maximum" if module == "graph-target" else module),
                        None, None, None, {"resources": list(resources), "bitmapFonts": list(fonts)},
                        {"root": node(module + ".root"),
                         "templates": [{"id": r["id"] + ".template", "root": r} for r in roots],
                         "instances": [{"id": r["id"] + ".instance", "template": r["id"] + ".template",
                                        "material": {"slot": "decoration", "resource": "frame-marker"}}
                                       for r in roots],
                         "bindings": list(bindings), "variants": list(variants), "tracks": list(tracks)}, ini)


class FixtureAuthor:
    family = "standard"

    def __init__(self, root, *, rotated=True, offscreen=False):
        self.root = root
        resources = []
        for index, role in enumerate(("panel", "ex", "pb", "target")):
            image = Image.new("RGBA", (12, 60))
            for y in range(60):
                for x in range(12):
                    image.putpixel((x, y), (index * 50, y * 4, x * 20, 255))
            image.save(root / (role + ".png"))
            resources.append({"id": role, "type": "texture", "path": role + ".png"})
        bases = node("panel.root", children=[node("panel.image", "sprite",
                     {"x": 1.5 if offscreen else .7, "width": .3, "height": 1}, resource="panel")])
        tracks = [{"id": "panel.position", "type": "tween", "target": "panel.image", "property": "x",
                   "clock": "option.graph-visible", "easing": "step", "loop": False,
                   "keyframes": [{"time": 0, "value": 1.5}, {"time": 1, "value": .7}]}] if offscreen else []
        base = contribution("base", [bases], [binding("panel.root", "visible", "option.graph-visible")],
                            tracks=tracks, resources=resources)
        ini = '[BmsMaterial]\nTarget: global ruleset=bms keymode=7k presentation=p1\ndecoration: resource Provide "frame-marker"\n'
        ex, ex_binding = self.bar("ex", .7, rotated)
        pb, pb_binding = self.bar("pb", .8, rotated)
        missing, missing_binding = self.bar("missing", .8, rotated, resource="pb")
        values = node("data.root", children=[ex, node("pb.available", children=[pb]),
                                               node("pb.missing", children=[missing])])
        frame = contribution("frame", [values], [binding("data.root", "visible", "option.graph-visible"),
                              binding(ex_binding, "reveal-x" if rotated else "scale-y", "score.progress"),
                              binding(pb_binding, "reveal-x" if rotated else "scale-y", "score.personal-best-progress"),
                              binding(missing_binding, "reveal-x" if rotated else "scale-y", "score.progress"),
                              binding("pb.available", "visible", "score.personal-best-available")],
                              variants=[{"id": "pb.missing.visibility", "target": "pb.missing", "property": "visible",
                                         "source": "score.personal-best-available", "default": False,
                                         "cases": [{"key": "false", "value": True}]}], ini=ini)
        target_bar, target_binding = self.bar("target", .9, rotated)
        label = node("target.name", "text", {"x": .7, "width": .3, "height": .1,
                                               "text": "MAXIMUM", "font-size": 16})
        target = contribution("graph-target", [node("target.root", children=[target_bar, label])],
                              [binding("target.root", "visible", "option.graph-visible"),
                               binding(target_binding, "reveal-x" if rotated else "scale-y", "score.maximum")])
        self.contributions = {"base": base, "frame": frame, "graph-target": target}

    @staticmethod
    def bar(role, x, rotated, resource=None):
        if rotated:
            leaf = node(role + ".fill", "sprite", {"width": .2, "height": 1, "scale-y": 5,
                        "rotation": 90, "origin": "bottom-left"}, resource=resource or role)
            clip = node(role + ".clip", "clip", {"clip-mode": "bounds", "reveal-x": 0}, [leaf])
            return node(role + ".bar", properties={"x": x, "y": .8, "width": .6, "height": .12,
                                                   "rotation": -90}, children=[clip]), clip["id"]
        return node(role + ".fill", "sprite", {"x": x, "y": .8, "width": .12, "height": .6,
                    "scale-y": 0, "origin": "bottom-left"}, resource=resource or role), role + ".fill"

    def base(self):
        return self.contributions["base"]

    def contribution(self, module, options):
        return self.contributions[module]


class TargetRoleAuthor(FixtureAuthor):
    """Small artwork with the actual source role/track relationships, no author art."""

    def __init__(self, root, family, choice, keymode, side):
        super().__init__(root, rotated=family == "standard")
        self.family = family
        image = Image.new("RGBA", (44, 6))
        for x in range(44):
            for y in range(6):
                image.putpixel((x, y), (x * 5, y * 40, 80, 255))
        image.save(root / "digits.png")
        glyphs = [{"character": c, "resource": "digits", "rect": [i * 4, 0, 4, 6], "advance": 4}
                  for i, c in enumerate("0123456789-")]
        fonts = [{"id": "digits", "lineHeight": 6, "glyphs": glyphs[:10]},
                 {"id": "signed", "lineHeight": 6, "glyphs": glyphs}]
        resources = [{"id": "digits", "type": "texture", "path": "digits.png"}]
        self.roles = {}
        tracks, bindings = [], []
        if family == "standard":
            # 23/24/25 have a different prefix from the other original frames.
            prefix = ("standard.graph.fs24.target." if side == "p2" else "standard.graph.target.") + choice + "." + side
            total = node(prefix + ".total", "text", {"x": .7, "width": .3, "height": .1,
                "bitmap-font": "digits", "font-size": 12.8, "alignment": "right"})
            difference = node(prefix + ".difference", "text", {"x": .7, "y": .1, "width": .3, "height": .1,
                "bitmap-font": "signed", "font-size": 12.8, "alignment": "right"})
            label = node(prefix + ".label", "text", {"x": .7, "width": .3, "height": .1,
                "text": {"custom": "CUSTOM %", "next": "NEXT", "next-plus": "NEXT+"}[choice], "font-size": 12.8})
            bar, clip_id = self.bar("target", .9, True)
            roots = [total, difference, label, bar]
            suffix = "selected.l2.n0" if choice == "next" else "target"
            bindings.extend([binding(total["id"], "text", "value." + prefix + ".value." + suffix),
                             binding(difference["id"], "text", "value." + prefix + ".value.difference"),
                             binding(clip_id, "reveal-x", "value." + prefix + ".value.pace-progress")])
            numeric = [(total, "total"), (difference, "difference")]
            if choice == "custom":
                percent = node(prefix + ".custom-percent", "text", {"x": .7, "y": .2, "width": .3, "height": .1,
                    "bitmap-font": "signed", "font-size": 12.8})
                roots.extend([percent, node(prefix + ".custom-percent.label", "text", {"text": "% OF MAX"})])
                bindings.append(binding(percent["id"], "text", "option.graph-custom-percent"))
                numeric.append((percent, "percentage"))
            for text, role in numeric:
                tracks.append({"id": text["id"] + ".fit", "target": text["id"], "type": "tween",
                    "property": "font-size", "clock": "score.maximum", "easing": "step", "loop": False,
                    "keyframes": [{"time": 0, "value": 12.8}, {"time": 100, "value": 6.4}, {"time": 1000, "value": 3.2}]})
                for i, (low, high) in enumerate(((None, 100), (100, 1000), (1000, None))):
                    self.roles["graph.graph-target." + text["id"] + "." + str(i)] = {"kind": role, "low": low, "high": high}
            self.roles["graph.graph-target." + label["id"] + ".0"] = {"kind": "name"}
        else:
            label = node("g2b", "text", {"text": "PACEMAKER", "font-size": 12.8, "height": .1})
            percent = node("g2c", "text", {"x": .7, "width": .3, "height": .1, "scale-x": .7,
                "bitmap-font": "digits", "font-size": 12.8, "alignment": "right"})
            bar = node("g2d", "sprite", {"x": .9, "y": .8, "width": .12, "height": .6,
                "origin": "bottom-left", "scale-y": 0}, resource="target")
            roots = [label, percent, bar, node("g2e", "text", {"text": "%"})]
            bindings.append(binding("g2c", "text", "option.pacemaker-percent"))
            tracks.append({"id": "gfe", "target": "g2d", "type": "tween", "property": "scale-y",
                "clock": "option.pacemaker-percent", "easing": "linear", "loop": False,
                "keyframes": [{"time": 0, "value": 0}, {"time": 100, "value": 1}]})
            self.roles = {"graph.graph-target.g2b.0": {"kind": "name"},
                          "graph.graph-target.g2c.0": {"kind": "percentage"}}
        parent = node("target.available", children=roots)
        target_root = node("target.root", children=[parent])
        bindings.extend([binding("target.root", "visible", "option.graph-visible"),
                         binding("target.available", "visible", "score.maximum-available")])
        self.contributions["graph-target"] = contribution("graph-target", [target_root], bindings,
            tracks=tracks, resources=resources, fonts=fonts, choice=choice)
        # Frozen dataclass; replace only the fixture's mode marker.
        frame = self.contributions["frame"]
        self.contributions["frame"] = Contribution(frame.module, frame.choice, None, None, None,
            frame.manifest, frame.scene, frame.ini.replace("keymode=7k presentation=p1", f"keymode={keymode} presentation={side}"))


class GraphTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache" / "temp" / "beatoraja-graph"
        parent.mkdir(parents=True, exist_ok=True)
        self.temp = tempfile.TemporaryDirectory(dir=parent)
        self.root = Path(self.temp.name)
        self.author_root = self.root / "author"
        self.output = self.root / "output"
        self.author_root.mkdir()
        self.output.mkdir()

    def tearDown(self):
        self.temp.cleanup()

    def export(self, **kwargs):
        author = FixtureAuthor(self.author_root, **kwargs)
        sink = AssetSink(self.author_root, self.output)
        result = export_graph(author, {"graph-visible": True}, "7k", "p1", sink, (0, 0, 100, 100))
        return result, sink

    def test_c5_marker_activates_base_without_a_base_ini_binding(self):
        result, _ = self.export()
        self.assertEqual(set(result["handled_template_ids"]),
                         {"panel.root.template", "data.root.template", "target.root.template"})
        self.assertIn("graph.base.panel.image", {i["id"] for i in result["arrays"]["image"]})

    def test_standard_rotated_clip_retains_all_three_full_height_sources(self):
        result, sink = self.export(rotated=True)
        graphs = result["arrays"]["graph"]
        self.assertEqual([g["type"] for g in graphs], [110, 113, 115])
        destinations = {d["id"]: d["dst"][0] for d in result["destination"]}
        for graph in graphs:
            self.assertEqual((graph["w"], graph["h"], graph["angle"]), (12, 60, 1))
            rect = destinations[graph["id"]]
            self.assertEqual((rect["y"], rect["w"], rect["h"]), (20, 12, 60))
            role = {110: "ex", 113: "pb", 115: "target"}[graph["type"]]
            self.assertEqual((self.output / sink.source[graph["src"]]["path"]).read_bytes(),
                             (self.author_root / (role + ".png")).read_bytes())

    def test_vertical_source_scale_is_a_native_rate_not_a_zero_size_image(self):
        result, _ = self.export(rotated=False)
        self.assertEqual([g["type"] for g in result["arrays"]["graph"]], [110, 113, 115])
        for graph in result["arrays"]["graph"]:
            # A scale-y source must retain every gradient row at half-height;
            # native SkinGraph's default bottom-half crop would change its art.
            self.assertEqual(graph["value"], Lua("function() return 1 end"))
            dest = next(d for d in result["destination"] if d["id"] == graph["id"])
            self.assertEqual([d["h"] for d in dest["dst"]], [0, 60])
            self.assertEqual(dest["dst"][-1]["time"], dest["loop"])
            self.assertIn(f"main_state.float_number({graph['type']})", dest["timer"])
            self.assertEqual({d["y"] for d in dest["dst"]}, {20})

    def test_static_option_clock_moves_source_from_offscreen_to_its_final_position(self):
        result, _ = self.export(offscreen=True)
        panel = next(d for d in result["destination"] if d["id"] == "graph.base.panel.image")
        self.assertEqual(panel["dst"][0]["x"], 70)

    def test_zero_pb_is_not_misclassified_as_missing_and_target_name_is_real(self):
        result, _ = self.export()
        ids = {d["id"] for d in result["destination"]}
        self.assertNotIn("graph.frame.missing.fill", ids)
        pb = next(d for d in result["destination"] if d["id"] == "graph.frame.pb.fill")
        self.assertNotIn("exscore_best() >", str(pb["draw"]))
        name = next(t for t in result["arrays"]["text"] if t["id"].endswith("target.name.0"))
        self.assertEqual(name["ref"], 3)
        self.assertNotIn("constantText", name)
        self.assertTrue(any(g["reason"] == "native-pb-existence-unavailable" for g in result["gaps"]))

    def test_hidden_graph_emits_no_draw_objects_but_claims_its_templates(self):
        author = FixtureAuthor(self.author_root)
        result = export_graph(author, {"graph-visible": False}, "7k", "p1",
                              AssetSink(self.author_root, self.output), (0, 0, 100, 100))
        self.assertEqual(result["destination"], [])
        self.assertEqual(len(result["handled_template_ids"]), 3)

    def target_case(self, family, choice, keymode, side, percentage=80):
        source, output = self.root / (family + choice + side + str(percentage)), self.root / ("out-" + family + choice + side + str(percentage))
        source.mkdir()
        output.mkdir()
        author = TargetRoleAuthor(source, family, choice, keymode, side)
        before = deepcopy(author.contributions["graph-target"].scene)
        result = export_graph(author, {"graph-visible": True, "graph-custom-percent": percentage,
            "pacemaker-percent": percentage}, keymode, side, AssetSink(source, output), (0, 0, 1280, 720))
        self.assertEqual(before, author.contributions["graph-target"].scene)
        self.assertFalse(any(g["reason"].startswith(("unmapped", "unsupported")) for g in result["gaps"]), result["gaps"])
        return {"name": family + "/" + choice + "/" + side,
                "skin": {"w": 1280, "h": 720, **result["arrays"], "destination": result["destination"]},
                "roles": author.roles, "targetBarHeight": 432}

    def test_standard_new_artworks_bind_whole_target_role_including_next_plus_name(self):
        for choice in ("custom", "next", "next-plus"):
            for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                case = self.target_case("standard", choice, mode, side)
                objects = {obj["id"]: obj for key in ("value", "text") for obj in case["skin"][key]}
                for identifier, role in case["roles"].items():
                    obj = objects[identifier]
                    if role["kind"] == "name":
                        self.assertEqual(3, obj["ref"])
                        self.assertNotIn("constantText", obj)
                    else:
                        self.assertIn({"total": "main_state.exscore_rival()", "difference": "main_state.exscore() - main_state.exscore_rival()",
                            "percentage": "main_state.number(135)"}[role["kind"]], obj["value"])
                self.assertEqual(1, sum(g["type"] == 115 for g in case["skin"]["graph"]))

    def test_percentage_artwork_uses_host_integer_percentage_independently_of_source_control(self):
        for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
            low = self.target_case("lightning", "percentage", mode, side, 1)
            high = self.target_case("lightning", "percentage", mode, side, 99)
            self.assertEqual(low, high)
            self.assertTrue(any(t.get("constantText") == "%" for t in low["skin"]["text"]))

    def test_lightning_next_keeps_its_unavailable_pb_existence_gate(self):
        author = FixtureAuthor(self.author_root)
        author.family = "lightning"
        target = contribution("graph-target", [node("next.root", children=[node("l3w", children=[
            node("next.total", "text", {"text": "not visible"})])])],
            [binding("next.root", "visible", "option.graph-visible"),
             binding("l3w", "visible", "score.pacemaker-next-available")], choice="next")
        author.contributions["graph-target"] = target
        result = export_graph(author, {"graph-visible": True}, "7k", "p1", AssetSink(self.author_root, self.output), (0, 0, 100, 100))
        self.assertTrue(any(g.get("source") == "score.pacemaker-next-available" and g["reason"] == "unmapped-visibility" for g in result["gaps"]))
        self.assertFalse(any(d["id"].startswith("graph.graph-target.") for d in result["destination"]))

    def test_official_luaj_parser_values_font_gates_and_target_bar_clock(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        cases = [self.target_case(family, choice, mode, side)
                 for family, choices in (("standard", ("custom", "next", "next-plus")), ("lightning", ("percentage",)))
                 for choice in choices for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp"))]
        recipe = self.root / "graph-roles.lua"
        recipe.write_text("return " + lua(cases), encoding="utf-8")
        compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.root),
            str(Path(__file__).with_name("GraphLuaProbe.java"))], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        result = subprocess.run([java, "-cp", str(self.root) + os.pathsep + str(jar),
            "bms.player.beatoraja.skin.lua.GraphLuaProbe", str(recipe)], capture_output=True, text=True,
            encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        receipt = json.loads(result.stdout)
        self.assertEqual(12, receipt["cases"])
        self.assertGreater(receipt["numericChecks"], 1000)
        self.assertGreater(receipt["gateChecks"], 1000)
        self.assertGreater(receipt["clockChecks"], 0)
        print("Graph artwork official CPU probe: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
