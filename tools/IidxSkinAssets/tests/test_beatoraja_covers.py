"""Whole native cover movement with exact baked-frame membership and layering."""

import copy
import hashlib
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution
from beatoraja_covers import export_animated_graph_area, export_covers, group_baked_runs
from beatoraja_native import AssetSink


def node(identifier, kind="container", properties=None, children=(), **extra):
    return {"id": identifier, "type": kind, "properties": properties or {}, "children": list(children), **extra}


class CoverTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-covers"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name)
        self.author_root, output, self.bake_dir = root / "author", root / "output", root / "bake"
        for path in (self.author_root, output, self.bake_dir):
            path.mkdir()
        Image.new("RGBA", (1, 1), (0, 0, 0, 255)).save(self.author_root / "black.png")
        Image.new("RGBA", (4, 4), (255, 0, 0, 255)).save(self.bake_dir / "page.png")
        self.sink = AssetSink(self.author_root, output)
        layouts = [{"ruleset": "bms", "keymode": mode, "presentation": side,
                    "stages": [{"rect": [.1, .05, .2, .5]}] + ([{"rect": [.7, .05, .2, .5]}] if side == "dp" else [])}
                   for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp"))]
        self.parts = {"frame": Contribution("frame", "test", None, None, None, {"layouts": layouts}, {}, "")}
        for module, slot in (("lane-cover", "playfield.lane-cover.fill"), ("lift-art", "playfield.lift.fill")):
            animated = node(module + ".art", "recorded-animation", animation="loop", properties={"x": .1, "width": .8} if module == "lift-art" else {})
            root_node = animated if module == "lane-cover" else node("lift.root", children=[node("black", "sprite", resource="black"), animated])
            ini = []
            for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                for stage in range(2 if side == "dp" else 1):
                    ini.append(f'[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode={mode} presentation={side} group=bms.group.deck-{stage + 1} group-logical={stage}\n{slot}: resource Provide "marker"')
            self.parts[module] = Contribution(module, "test", module + ".json", None, None,
                {"resources": [{"id": "black", "type": "texture", "path": "black.png"}]},
                {"templates": [{"id": "template", "root": root_node}],
                 "instances": [{"template": "template", "material": {"slot": slot, "resource": "marker"}}]}, "\n".join(ini))
        self.author = SimpleNamespace(root=self.author_root, family="lightning", modules=self.parts, contribution=lambda name, options: self.parts[name])
        self.index = {"contract": "oms-iidx-baked-animation.v1", "verification": {"passed": True}, "selectionComplete": True,
                      "sourceFrameCount": 4, "framesPerSecond": 10, "loop": True, "loopStartFrame": 0,
                      "trigger": "gameplay.attach", "canvas": [4, 4],
                      "source": {"authorRoot": str(self.author_root), "manifest": "lane-cover.json", "animation": "loop"},
                      "pages": [{"id": 0, "file": "page.png", "width": 4, "height": 4,
                                 "sha256": hashlib.sha256((self.bake_dir / "page.png").read_bytes()).hexdigest()}]}
        normal = {"order": 0, "page": 0, "rect": [1, 1, 2, 2], "offset": [1, 1], "blend": "normal"}
        additive = {**normal, "order": 1, "blend": "additive"}
        self.index["frames"] = [
            {"sourceFrame": 0, "terminal": False, "runs": [normal, additive]},
            {"sourceFrame": 1, "terminal": False, "runs": [normal, {**additive, "blend": "normal"}]},
            {"sourceFrame": 2, "terminal": False, "runs": [normal, additive, {**normal, "order": 2}]},
            {"sourceFrame": 3, "terminal": True, "runs": []}]
        self.baked = {}
        for module in ("lane-cover", "lift-art"):
            index = copy.deepcopy(self.index); index["source"]["manifest"] = module + ".json"
            self.baked[(module + ".json", "loop")] = self.bake_dir / "index.json", index

    def export(self, kinds=("sudden", "hidden", "lift"), mode="7k", side="p1"):
        return export_covers(self.author, {}, mode, side, self.sink, (0, 0, 200, 200), self.baked, kinds)

    def test_same_pixels_keep_blend_order_and_exact_frame_membership(self):
        before = copy.deepcopy(self.index)
        groups = group_baked_runs(self.index)
        self.assertEqual(self.index, before)
        self.assertEqual(len(groups), 4)
        self.assertEqual([(g["order"], g["blend"], g["frames"]) for g in groups],
                         [(0, "normal", [0, 1, 2]), (1, "additive", [0, 2]), (1, "normal", [1]), (2, "normal", [2])])
        for frame in range(4):
            actual = [{k: g[k] for k in ("order", "page", "rect", "offset", "blend")} for g in groups if frame in g["frames"]]
            self.assertEqual(actual, self.index["frames"][frame]["runs"])
        self.assertIn("(frame >= 0 and frame <= 2)", groups[0]["draw"])
        self.assertIn("frame == 0 or frame == 2", groups[1]["draw"])

    def test_each_animated_crop_keeps_full_slider_range_and_host_hidden_clip(self):
        result = self.export()
        self.assertEqual([len(result[k]) for k in ("slider", "hiddenCover", "liftCover")], [4, 4, 5])
        self.assertTrue(all(s["range"] == 100 and s["type"] == 4 and s["angle"] == 2 and not s["changeable"] for s in result["slider"]))
        self.assertTrue(all(h["disapearLine"] == 90 and h["isDisapearLineLinkLift"] for h in result["hiddenCover"]))
        self.assertTrue(all(h["disapearLine"] == 90 and not h["isDisapearLineLinkLift"] for h in result["liftCover"]))
        first = result["destination"][0]
        self.assertEqual({k: first["dst"][0][k] for k in ("x", "y", "w", "h")}, {"x": 30, "y": 215, "w": 20, "h": 50})
        self.assertEqual([d["blend"] for d in result["destination"][:4]], [1, 2, 1, 1])
        self.assertEqual({k: result["destination"][8]["dst"][0][k] for k in ("x", "y", "w", "h")}, {"x": 20, "y": -10, "w": 40, "h": 100})
        self.assertEqual({k: result["destination"][9]["dst"][0][k] for k in ("x", "y", "w", "h")}, {"x": 32, "y": 15, "w": 16, "h": 50})
        self.assertNotIn("draw", result["destination"][8])
        self.assertEqual([g["objects"] for g in result["gaps"]], [4, 4, 4])
        self.assertEqual(len(self.sink.source), 2)  # One exact black image and one verified atlas.

    def test_dp_does_not_share_destination_identity_or_geometry_between_stages(self):
        result = self.export(("sudden",), "14k", "dp")
        self.assertEqual(len(result["slider"]), 8)
        self.assertEqual(len({d["id"] for d in result["destination"]}), 8)
        self.assertEqual([result["destination"][i]["dst"][0]["x"] for i in (0, 4)], [30, 150])
        self.assertEqual([g["stage"] for g in result["gaps"]], [0, 1])

    def test_missing_or_partial_bake_is_not_substituted_with_a_snapshot(self):
        self.baked[("lane-cover.json", "loop")][1]["selectionComplete"] = False
        del self.baked[("lift-art.json", "loop")]
        result = self.export()
        self.assertEqual((result["slider"], result["hiddenCover"]), ([], []))
        self.assertEqual(len(result["liftCover"]), 1)  # The actual original black margin remains.
        self.assertTrue(all(g["code"] == "cover-animation-needs-complete-bake" for g in result["gaps"]))
        self.assertEqual(len(self.sink.source), 1)

    def test_unverified_wrong_identity_and_incomplete_frame_sequence_are_rejected(self):
        for mutation in (lambda i: i["verification"].update(passed=False), lambda i: i["frames"].pop(1),
                         lambda i: i.update(trigger="judgement.hit"), lambda i: i["frames"][-1]["runs"].append(i["frames"][0]["runs"][0])):
            broken = copy.deepcopy(self.index); mutation(broken)
            with self.assertRaises(ValueError):
                group_baked_runs(broken)
        self.baked[("lane-cover.json", "loop")][1]["source"]["animation"] = "another"
        with self.assertRaisesRegex(ValueError, "identity"):
            self.export(("sudden",))

    def test_atlas_bytes_are_checked_by_the_common_asset_sink(self):
        Image.new("RGBA", (4, 4), (0, 0, 255, 255)).save(self.bake_dir / "page.png")
        with self.assertRaisesRegex(ValueError, "hash"):
            self.export(("sudden",))

    def test_loop_start_and_nonloop_terminal_use_existing_runtime_contract(self):
        self.index["loopStartFrame"] = 1
        self.assertIn("animation_frame(0,10,3,1,0)", group_baked_runs(self.index)[0]["draw"])
        self.index["loop"] = False
        self.assertIn("animation_frame(0,10,3,-1,0)", group_baked_runs(self.index)[0]["draw"])

    def add_graph_area(self):
        old = self.parts["frame"]
        self.parts["frame"] = Contribution(old.module, old.choice, None, None, None, old.manifest, {},
            '[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=7k presentation=p1\ndecoration: resource Provide "graph.marker"')
        art = node("graph.art", "recorded-animation", {"x": -.1, "y": -.1, "width": 1.2, "height": 1.2}, animation="loop")
        clip = node("graph.clip", "clip", {"x": .5, "y": .1, "width": .4, "height": .5, "clip-mode": "bounds"}, [art])
        root = node("graph.root", children=[clip])
        scene = {"templates": [{"id": "graph.template", "root": root}],
                 "instances": [{"template": "graph.template", "material": {"slot": "decoration", "resource": "graph.marker"}}],
                 "bindings": [{"target": "graph.root", "property": "visible", "source": "option.graph-visible"}]}
        self.parts["graph-area"] = Contribution("graph-area", "test", "lane-cover.json", None, None, {}, scene, "")

    def test_animated_graph_area_uses_actual_global_clip_and_preserves_overscan(self):
        self.add_graph_area()
        result = export_animated_graph_area(self.author, {"graph-visible": True}, "7k", "p1", self.sink, (0, 0, 200, 200), self.baked)
        self.assertEqual(len(result["image"]), 4)
        self.assertEqual(result["handled_template_ids"], ["graph.template"])
        self.assertEqual(result["handled_root_ids"], ["graph.root"])
        self.assertEqual({k: result["destination"][0]["dst"][0][k] for k in ("x", "y", "w", "h", "clip_x", "clip_y", "clip_w", "clip_h")},
                         {"x": 116, "y": 100, "w": 48, "h": 60, "clip_x": 100, "clip_y": 80, "clip_w": 80, "clip_h": 100})
        self.assertEqual([d["blend"] for d in result["destination"]], [1, 2, 1, 1])
        self.assertTrue(all("offsets" not in d and "op" not in d for d in result["destination"]))

    def test_graph_area_off_and_unmatched_marker_copy_no_atlas(self):
        self.add_graph_area()
        for options, side in (({"graph-visible": False}, "p1"), ({"graph-visible": True}, "p2")):
            result = export_animated_graph_area(self.author, options, "7k", side, self.sink, (0, 0, 200, 200), self.baked)
            self.assertEqual(result["image"], [])
            self.assertEqual(result["destination"], [])
        self.assertEqual(self.sink.source, {})

    def test_missing_graph_area_bake_returns_handled_identity_and_explicit_gap(self):
        self.add_graph_area()
        result = export_animated_graph_area(self.author, {"graph-visible": True}, "7k", "p1", self.sink, (0, 0, 200, 200), {})
        self.assertEqual(result["image"], [])
        self.assertEqual(result["handled_root_ids"], ["graph.root"])
        self.assertEqual(result["gaps"][0]["code"], "graph-area-animation-needs-complete-bake")


if __name__ == "__main__":
    unittest.main()
