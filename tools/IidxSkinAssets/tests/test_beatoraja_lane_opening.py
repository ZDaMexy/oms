"""Stage-owned source routing without loading any large animation bake."""

import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import AuthorPackage, Contribution, layout_for, walk_nodes
from beatoraja_export import Composer
from beatoraja_native import AssetSink


class LaneOpeningRoutingTests(unittest.TestCase):
    def synthetic(self):
        stages = [{"rect": [.1, .2, .3, .4]}, {"rect": [.6, .2, .3, .4]}]
        manifest = {"layouts": [{"ruleset": "bms", "keymode": "14k", "presentation": "dp", "stages": stages}]}
        children, variants = [], []
        for side in (1, 2):
            children.append({"id": f"group-{side}", "type": "container", "properties": {"visible": False},
                             "children": [{"id": f"movie-{side}", "type": "recorded-animation", "animation": f"lane-{side}",
                                           "clock": "preparation.elapsed"}]})
            variants.append({"target": f"group-{side}", "property": "visible", "source": "layout.group", "default": False,
                             "cases": [{"key": f"bms.group.deck-{side}", "value": True}]})
        scene = {"templates": [{"id": "both", "root": {"id": "opening", "type": "container", "children": children}}],
                 "instances": [{"template": "both", "material": {"slot": "playfield.baseplate", "resource": "marker"}}],
                 "variants": variants}
        ini = "\n".join(f'[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode=14k presentation=dp group=bms.group.deck-{side} group-logical={side-1}\nplayfield.baseplate: resource Provide "marker"'
                        for side in (1, 2))
        frame = Contribution("frame", "fixture", "fixture.json", None, None, manifest, scene, ini)
        composer = Composer(SimpleNamespace(family="lightning", name="fixture"), SimpleNamespace(), {}, "dp", {})
        return composer, frame

    def test_each_dp_template_instance_sees_only_its_exact_binding_group(self):
        composer, frame = self.synthetic()
        previous = composer.context
        calls = []
        composer.draw_baked = lambda manifest, animation, rect, rgba, **kw: calls.append(
            (animation, composer.context["layout.group"], rect, kw["timer"], kw["start_frame"]))
        composer.lane_opening(frame)
        self.assertEqual([("lane-1", "bms.group.deck-1", (192, 216, 576, 432), 10020, 0),
                          ("lane-2", "bms.group.deck-2", (1152, 216, 576, 432), 10020, 0)], calls)
        self.assertFalse(composer.gaps)
        self.assertIs(previous, composer.context)
        self.assertNotIn("layout.group", composer.context)

    def test_stage_context_is_restored_when_a_source_branch_fails(self):
        composer, frame = self.synthetic()
        composer.context["layout.group"] = "existing-caller-context"
        previous = composer.context
        def fail(*args, **kwargs):
            self.assertEqual("bms.group.deck-1", composer.context["layout.group"])
            raise ValueError("source branch failure")
        composer.draw_baked = fail
        with self.assertRaisesRegex(ValueError, "source branch failure"):
            composer.lane_opening(frame)
        self.assertIs(previous, composer.context)
        self.assertEqual("existing-caller-context", composer.context["layout.group"])

    def test_real_bpl_tradz_dp_groups_and_both_sp_sides_route_once(self):
        root = Path(os.environ.get("OMS_LIGHTNING_AUTHOR",
            "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1"))
        if not root.is_dir():
            self.skipTest("The frozen Lightning author is required")
        evidence = Path(__file__).resolve().parents[3] / "artifacts/beatoraja-capture/cover-grid-r1"
        evidence.mkdir(parents=True, exist_ok=True)
        folder = Path(tempfile.mkdtemp(prefix="lane-opening-routing-", dir=evidence))
        author = AuthorPackage(root, "lightning")
        sink = AssetSink(root, folder)
        records = []
        for keymode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
            for enabled in (True, False):
                options = author.options(keymode, side, {"frame": "bpl-tradz", "lane-opening": enabled})
                frame = author.contribution("frame", options)
                if side == "dp":
                    node_ids = {node["id"] for target in frame.templates.values() for node in walk_nodes(target)}
                    self.assertTrue({"f7b", "f7e"}.issubset(node_ids))
                    gates = [v for v in frame.scene["variants"] if v["target"] in ("f7b", "f7e")]
                    self.assertEqual(["layout.group", "layout.group"], [v["source"] for v in gates])
                composer = Composer(author, sink, options, side, {}, keymode=keymode)
                before = composer.context
                calls = []
                def recorded(manifest, animation, rect, rgba, **parameters):
                    calls.append({"manifest": manifest, "animation": animation, "group": composer.context["layout.group"],
                                  "rect": list(rect), "timer": parameters["timer"], "startFrame": parameters["start_frame"],
                                  "conditions": list(parameters["conditions"])})
                composer.draw_baked = recorded  # Intercept only final bake lookup; the actual source tree is traversed.
                composer.lane_opening(frame)
                self.assertIs(before, composer.context)
                self.assertNotIn("layout.group", composer.context)
                expected = (["lo.dp.g1", "lo.dp.g2"] if side == "dp" else [f"lo.{side}.g1"]) if enabled else []
                self.assertEqual(expected, [call["animation"] for call in calls])
                self.assertTrue(all(gap.get("code") == "native-beat-opacity-adaptation" for gap in composer.gaps))
                stages = layout_for(frame, keymode, side)["stages"]
                for number, call in enumerate(calls):
                    self.assertEqual(f"bms.group.deck-{number+1}", call["group"])
                    x, y, w, h = stages[number]["rect"]
                    self.assertEqual([x*1920, y*1080, w*1920, h*1080], call["rect"])
                    self.assertEqual((10020, 0), (call["timer"], call["startFrame"]))
                    self.assertEqual(1, len(call["conditions"]))
                    self.assertIn("10020", call["conditions"][0])
                records.append({"keymode": keymode, "side": side, "enabled": enabled, "calls": calls, "gaps": composer.gaps,
                                "sourceFilesSha256": {name: hashlib.sha256((root/name).read_bytes()).hexdigest()
                                    for name in (frame.manifest_path, frame.scene_path, frame.ini_path)}})
        receipt = {"complete": True, "scope": "Actual three-mode source routing, on/off; bake lookup intercepted; no GPU or large bake loaded",
                   "frame": "bpl-tradz", "routes": records,
                   "exporterSha256": hashlib.sha256(Path(__file__).resolve().parents[1].joinpath("beatoraja_export.py").read_bytes()).hexdigest()}
        path = folder / "source-routing.json"
        path.write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf8")
        print("Actual lane opening routing: " + str(path))


if __name__ == "__main__":
    unittest.main()
