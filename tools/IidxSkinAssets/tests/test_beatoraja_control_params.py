"""Qualified source controls keep exact native geometry, colour and ownership."""

import copy
import itertools
import json
from pathlib import Path
import sys
from types import SimpleNamespace
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_control_params import (UnqualifiedParameters, apply_parameters,
                                     factor_fragment, qualify_cap_rgb, qualify_source)
from beatoraja_native import Lua


def source_author():
    part = SimpleNamespace(manifest_path="real-manifest", scene_path="real-scene", ini_path="real-ini",
        scene={"bindings": [{"target": "carrier", "property": "brightness", "source": "option.lane-objects-brightness"}],
               "tracks": [{"target": "carrier", "property": "y", "clock": "option.judgement-position",
                           "easing": "linear", "loop": False,
                           "keyframes": [{"time": -2, "value": .4}, {"time": 10, "value": -2.8}]}]})
    return SimpleNamespace(family="lightning", modules={"judgement": {"choices": [{"value": "j"}]}},
                           contribution=lambda module, options: part), part


class ControlParameterTests(unittest.TestCase):
    def test_only_the_actual_independent_source_properties_qualify(self):
        author, part = source_author()
        self.assertEqual(qualify_source(author, "judgement", {"judgement": "j"})["operations"],
                         {"judgement-position": 1, "lane-objects-brightness": 1})
        part.scene["tracks"][0]["property"] = "opacity"
        with self.assertRaisesRegex(UnqualifiedParameters, "another source operation"):
            qualify_source(author, "judgement", {"judgement": "j"})
        part.scene["tracks"][0]["property"] = "y"
        part.scene["unexpected"] = "option.judgement-position"
        with self.assertRaisesRegex(UnqualifiedParameters, "outside"):
            qualify_source(author, "judgement", {"judgement": "j"})

    def test_source_catalog_branches_and_standard_positions_are_not_collapsed(self):
        author, _ = source_author()
        author.modules["judgement"]["choices"][0]["variants"] = [{"when": {"judgement-position": 1}}]
        with self.assertRaisesRegex(UnqualifiedParameters, "catalog"):
            qualify_source(author, "judgement", {"judgement": "j"})
        author.family = "standard"
        with self.assertRaisesRegex(UnqualifiedParameters, "Only Lightning"):
            qualify_source(author, "judgement", {"judgement": "j"})

    def test_judge_all_axis_intersections_keep_relative_combo_and_lift_owner(self):
        defaults = {"judgement-position": 0, "lane-objects-brightness": 1}
        definitions = {"judgement-position": [{"value": v} for v in (-2, 0, 1, 10)],
                       "lane-objects-brightness": [{"value": v} for v in (.9, .94, 1)]}
        def direct(options):
            rgb = round(255 * options["lane-objects-brightness"])
            # Deliberately fractional source coordinates: adding a rounded
            # displacement to a rounded default would not always be equal.
            label = {"id": "real.label", "timer": 46, "offsets": [3],
                     "dst": [{"x": 110, "y": round(399.5 + options["judgement-position"] * 18.25),
                              "w": 100, "h": 40, "r": rgb, "g": rgb, "b": rgb, "a": 200}, {"time": 500}]}
            combo = {"id": "real.combo", "dst": [{"x": 103, "y": 2, "r": rgb, "g": rgb, "b": rgb, "a": 255}]}
            return {"judge": [{"id": "real.judge", "index": 1, "shift": True,
                                "images": [label], "numbers": [combo]}], "destination": [{"id": "real.judge"}]}, []
        base, gaps, axes, calls = factor_fragment("judgement", defaults, definitions, direct)
        self.assertEqual(calls, 6)
        for position, brightness in itertools.product((-2, 0, 1, 10), (.9, .94, 1)):
            selected = {"judgement-position": position, "lane-objects-brightness": brightness}
            self.assertEqual(apply_parameters(base, axes, selected), direct(selected)[0])
        self.assertEqual(base, direct(defaults)[0])
        self.assertEqual(gaps, [])

    def test_explosion_rounding_is_taken_from_source_and_callbacks_remain_exact(self):
        defaults = {"explosion-scale": 1, "lane-objects-brightness": 1}
        definitions = {"explosion-scale": [{"value": v} for v in (.5, .95, 1, 1.5)],
                       "lane-objects-brightness": [{"value": v} for v in (.9, 1)]}
        callback = Lua("function() return main_state.timer(51) end")
        def direct(options):
            scale, bright = options["explosion-scale"], options["lane-objects-brightness"]
            return {"imageset": [{"id": "original", "images": ["crop"], "value": callback}],
                    "destination": [{"id": "original", "offsets": [3],
                                     "dst": [{"x": round(3.25 - 8.5 * scale), "y": round(10.5 + 9.5 * scale),
                                              "w": round(17 * scale), "h": round(19 * scale),
                                              "r": round(204 * bright), "g": round(255 * bright), "b": 0, "a": 127}]}]}, []
        base, _, axes, _ = factor_fragment("explosion", defaults, definitions, direct)
        for scale, bright in itertools.product((.5, .95, 1, 1.5), (.9, 1)):
            options = {"explosion-scale": scale, "lane-objects-brightness": bright}
            actual = apply_parameters(base, axes, options)
            self.assertEqual(actual, direct(options)[0])
            self.assertIsInstance(actual["imageset"][0]["value"], Lua)

    def test_source_resource_timer_alpha_or_diagnostics_changes_keep_exact_enumeration(self):
        definitions = {"explosion-scale": [{"value": 1}, {"value": 1.5}]}
        base = {"image": [{"id": "image", "src": "original"}],
                "destination": [{"id": "image", "timer": 51, "dst": [{"x": 1, "a": 255}]}]}
        def changed(options, target):
            fragment = copy.deepcopy(base)
            if options["explosion-scale"] != 1:
                if target == "source": fragment["image"][0]["src"] = "other"
                elif target == "timer": fragment["destination"][0]["timer"] = 52
                else: fragment["destination"][0]["dst"][0]["a"] = 128
            return fragment, []
        for target in ("source", "timer", "alpha"):
            with self.subTest(target=target), self.assertRaises(UnqualifiedParameters):
                factor_fragment("explosion", {"explosion-scale": 1}, definitions,
                                lambda options: changed(options, target))
        with self.assertRaisesRegex(UnqualifiedParameters, "diagnostics"):
            factor_fragment("explosion", {"explosion-scale": 1}, definitions,
                            lambda options: (base, [] if options["explosion-scale"] == 1 else [{"reason": "missing"}]))

    def test_beam_rgb_keeps_real_pressed_release_clock_and_timing_keeps_region(self):
        beam_draw = Lua("function() return main_state.timer(101) ~= main_state.timer_off_value end")
        timing_draw = Lua("function() return main_state.number(525) > 0 end")
        definitions = {"lane-objects-brightness": [{"value": .9}, {"value": 1}]}
        for stage, draw in (("key-beam", beam_draw), ("timing-display", timing_draw)):
            def direct(options):
                return {"destination": [{"id": "source", "draw": draw, "timer": 101 if stage == "key-beam" else 46,
                                         "dst": [{"x": 12, "y": 345, "r": round(255 * options['lane-objects-brightness']),
                                                  "g": 100, "b": 0, "a": 255}]}]}, []
            base, _, axes, _ = factor_fragment(stage, {"lane-objects-brightness": 1}, definitions, direct)
            self.assertEqual(apply_parameters(base, axes, {"lane-objects-brightness": .9}), direct({"lane-objects-brightness": .9})[0])

    def test_cap_scan_drops_only_proven_rgb_and_keeps_resource_visibility_branches(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-control-tests"
        parent.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=parent) as directory:
            root = Path(directory)
            scene = {"bindings": [{"target": "sprite", "property": "brightness", "source": "option.lane-objects-brightness"}]}
            (root / "scene.json").write_text(json.dumps(scene), encoding="utf-8")
            author = SimpleNamespace(family="lightning", root=root, json=lambda relative: scene,
                modules={name: {"choices": [{"value": "choice", "scene": "scene.json"}]} for name in ("notes", "charge-notes")})
            self.assertTrue(qualify_cap_rgb(author)["qualified"])
            for prop in ("resource", "visible", "height"):
                scene["bindings"][0]["property"] = prop
                self.assertFalse(qualify_cap_rgb(author)["qualified"])
            scene["bindings"][0]["property"] = "brightness"
            author.modules['notes']['choices'][0]['variants'] = [{"when": {"lane-objects-brightness": .9}, "scene": "scene.json"}]
            self.assertFalse(qualify_cap_rgb(author)["qualified"])


if __name__ == "__main__":
    unittest.main()
