"""Native Graph contract tests using small, local, non-author fixture images."""

from pathlib import Path
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution
from beatoraja_graph import export_graph
from beatoraja_native import AssetSink, Lua


def node(identifier, kind="container", properties=None, children=(), **extra):
    return {"id": identifier, "type": kind, "properties": properties or {},
            "children": list(children), **extra}


def binding(identifier, prop, source):
    return {"id": identifier + "." + prop, "target": identifier, "property": prop, "source": source}


def contribution(module, roots, bindings=(), variants=(), tracks=(), resources=(), fonts=(), ini=""):
    return Contribution(module, "maximum" if module == "graph-target" else module,
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


if __name__ == "__main__":
    unittest.main()
