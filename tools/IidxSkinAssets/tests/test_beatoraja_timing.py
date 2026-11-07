"""Author timing rows, real stage ownership and unsupported channel separation."""

import copy
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution
from beatoraja_native import AssetSink
from beatoraja_timing import export_timing


def node(identifier, properties=None, children=(), kind="container", **extra):
    return {"id": identifier, "type": kind, "properties": properties or {}, "children": list(children), **extra}


class TimingTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-timing"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name)
        self.author_root, output = root / "author", root / "output"
        self.author_root.mkdir(); output.mkdir()
        for name, rgba in (("fast", (255, 0, 0, 255)), ("slow", (0, 0, 255, 255))):
            Image.new("RGBA", (64, 22), rgba).save(self.author_root / (name + ".png"))
        self.sink = AssetSink(self.author_root, output)
        layouts, ini, templates, instances, tracks, variants, bindings = [], [], [], [], [], [], []
        for keymode, presentation in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
            stages = [{"rect": [.1, 0, .25, .5]}]
            if presentation == "dp":
                stages.append({"rect": [.65, 0, .25, .5]})
            for i in range(len(stages)):
                ini.append(f'[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode={keymode} presentation={presentation} group=bms.group.deck-{i + 1} group-logical={i}\nhud.judgement: resource Provide "marker-{presentation}"')
            layouts.append({"ruleset": "bms", "keymode": keymode, "presentation": presentation, "stages": stages})
            sprite = node(presentation + ".art", {"visible": False, "height-px": 22 * 768 / 1080}, kind="sprite", resource="fast")
            row = node(presentation + ".row", {"x": .5, "y": -.25, "origin": "centre", "layout": "horizontal"}, [sprite])
            inner = node(presentation + ".inner", children=[row])
            carrier = node(presentation + ".root", children=[inner])
            templates.append({"id": presentation, "root": carrier})
            instances.append({"template": presentation, "material": {"slot": "hud.judgement", "resource": "marker-" + presentation}})
            for target in (carrier["id"], inner["id"]):
                tracks.append({"id": target + ".position", "target": target, "property": "y", "clock": "option.judgement-position",
                               "easing": "linear", "loop": False, "keyframes": [{"time": 0, "value": 0}, {"time": 1, "value": -.2}]})
            variants.extend([
                {"target": sprite["id"], "property": "visible", "source": "judgement.timing-direction", "default": False,
                 "cases": [{"key": "fast", "value": True}, {"key": "slow", "value": True}]},
                {"target": sprite["id"], "property": "resource", "source": "judgement.timing-direction", "default": "fast",
                 "cases": [{"key": "slow", "resource": "slow"}]},
                {"target": row["id"], "property": "x", "source": "layout.group", "default": .5,
                 "cases": [{"key": "bms.group.deck-2", "value": .6}]}])
            bindings.append({"target": carrier["id"], "property": "brightness", "source": "option.lane-objects-brightness"})
        resources = [{"id": name, "type": "texture", "path": name + ".png"} for name in ("fast", "slow")]
        self.parts = {
            "frame": Contribution("frame", "test", None, None, None, {"layouts": layouts}, {}, ""),
            "judgement": Contribution("judgement", "test", None, None, None, {}, {}, "\n".join(ini)),
            "timing-display": Contribution("timing-display", "test", None, None, None, {"resources": resources},
                {"templates": templates, "instances": instances, "tracks": tracks, "variants": variants, "bindings": bindings}, "")}
        self.author = SimpleNamespace(family="lightning", modules=self.parts, contribution=lambda name, options: self.parts[name])
        self.options = {"judgement-position": 0, "lane-objects-brightness": 1}

    def export(self, keymode="7k", presentation="p1"):
        return export_timing(self.author, self.options, keymode, presentation, self.sink, (0, 0, 1920, 1080))

    def test_original_sprite_size_and_both_source_position_tracks_are_preserved(self):
        self.options.update({"judgement-position": 1, "lane-objects-brightness": .4})
        before = copy.deepcopy(self.parts["timing-display"].scene)
        result = self.export()
        self.assertEqual(self.parts["timing-display"].scene, before)
        self.assertEqual(len(result["image"]), 2)
        for image, destination in zip(result["image"], result["destination"]):
            self.assertEqual((image["w"], image["h"]), (64, 22))
            self.assertEqual({k: destination["dst"][0][k] for k in ("x", "y", "w", "h", "r", "g", "b", "a")},
                             {"x": 400, "y": 706, "w": 64, "h": 22, "r": 102, "g": 102, "b": 102, "a": 255})
        self.assertNotEqual(result["image"][0]["src"], result["image"][1]["src"])
        self.assertEqual(result["value"], [])
        self.assertEqual(result["text"], [])

    def test_sp2_keeps_first_region_and_dp_uses_independent_second_region(self):
        for keymode, presentation, expected in (("7k", "p2", [46, 46]), ("14k", "dp", [46, 46, 47, 47])):
            result = self.export(keymode, presentation)
            self.assertEqual([d["timer"] for d in result["destination"]], expected)
            for i, destination in enumerate(result["destination"]):
                stage = i // 2
                self.assertIn(f"main_state.number({525 + stage})", destination["draw"])
                self.assertIn(f"main_state.option({1242 + stage * 20 + i % 2})", destination["draw"])
                self.assertIn(" > 0" if i % 2 == 0 else " < 0", destination["draw"])
                self.assertEqual((destination["loop"], destination["dst"][-1]), (-1, {"time": 500}))
            if presentation == "dp":
                self.assertEqual(result["destination"][2]["dst"][0]["x"], 1504)

    def test_separate_source_does_not_duplicate_one_stage_read_into_both_channels(self):
        scene = self.parts["timing-display"].scene
        for variant in scene["variants"]:
            if variant["source"] == "judgement.timing-direction":
                variant["source"] = "judgement.scratch-timing-direction"
        result = self.export("14k", "dp")
        self.assertEqual(result["image"], [])
        self.assertEqual(result["destination"], [])
        self.assertEqual([g["stage"] for g in result["gaps"]], [0, 1])
        self.assertTrue(all(g["code"] == "native-timing-separate-channels-unavailable" for g in result["gaps"]))
        self.assertEqual(self.sink.source, {})

    def test_absent_frame_feedback_and_module_off_add_nothing(self):
        self.parts["timing-display"].scene["instances"] = []
        self.assertEqual(self.export()["destination"], [])
        del self.parts["timing-display"]
        self.author.family = "standard"
        self.assertTrue(all(not value for value in self.export().values()))
        self.assertEqual(self.sink.source, {})

    def use_standard_frame_feedback(self):
        del self.parts["timing-display"]
        self.author.family = "standard"
        self.options = {"judgement-position": "normal", "timing-position": "a"}
        for name, size in (("fast", (48, 20)), ("slow", (48, 20)), ("fa", (24, 20)), ("sl", (24, 20))):
            Image.new("RGBA", size, (255, 255, 255, 255)).save(self.author_root / (name + ".png"))
        templates, instances, variants, tracks, ini = [], [], [], [], []
        for mode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
            for stage in range(2 if side == "dp" else 1):
                prefix = f"{side}.timing.g{stage}"
                lift = node(prefix + ".lift")
                root = node(prefix + ".root", children=[lift])
                tracks.append({"id": prefix + ".lift.y", "target": lift["id"], "property": "y", "clock": "lift.offset",
                               "easing": "linear", "loop": False, "keyframes": [{"time": 0, "value": 0}, {"time": 1, "value": -1}]})
                variants.append({"target": root["id"], "property": "visible", "source": "preparation.active", "default": True,
                                 "cases": [{"key": "true", "value": False}]})
                for setting, move in (("normal", 0), ("up100", 100)):
                    level = node(prefix + "." + setting, {"y": -move / 540})
                    lift["children"].append(level)
                    variants.append({"target": level["id"], "property": "visible", "source": "option.judgement-position", "default": False,
                                     "cases": [{"key": setting, "value": True}]})
                    for position, px, py in (("a", 120, 300), ("b", -60, 350), ("c", 500, 400)):
                        location = node(level["id"] + "." + position)
                        level["children"].append(location)
                        variants.append({"target": location["id"], "property": "visible", "source": "option.timing-position", "default": False,
                                         "cases": [{"key": position, "value": True}]})
                        small = position == "c" and side == "dp"
                        for direction, tint in (("fast", "#0000ffff"), ("slow", "#ff0000ff")):
                            resource = ("fa" if direction == "fast" else "sl") if small else direction
                            art = node(location["id"] + "." + direction, {"x": px / 480, "y": py / 540,
                                "width": (24 if small else 48) / 480, "height": 20 / 540, "colour": tint}, kind="sprite", resource=resource)
                            location["children"].append(art)
                            variants.append({"target": art["id"], "property": "visible", "source": "judgement.timing-direction", "default": False,
                                             "cases": [{"key": direction, "value": True}]})
                templates.append({"id": prefix, "root": root})
                instances.append({"template": prefix, "material": {"slot": "stage.foreground", "resource": prefix}})
                ini.append(f'[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode={mode} presentation={side} group=bms.group.deck-{stage + 1} group-logical={stage}\nstage.foreground: resource Provide "{prefix}"')
        # An unrelated frame foreground is owned by its actual caller, not by
        # this exporter just because it shares a material slot.
        templates.append({"id": "other", "root": node("other.root", kind="sprite", resource="fast")})
        instances.append({"template": "other", "material": {"slot": "stage.foreground", "resource": "p1.timing.g0"}})
        old = self.parts["frame"]
        resources = [{"id": name, "type": "texture", "path": name + ".png"} for name in ("fast", "slow", "fa", "sl")]
        self.parts["frame"] = Contribution("frame", "test", None, None, None, {**old.manifest, "resources": resources},
            {"templates": templates, "instances": instances, "tracks": tracks, "variants": variants}, "\n".join(ini))

    def test_standard_frame_feedback_keeps_source_position_colour_and_single_native_lift(self):
        self.use_standard_frame_feedback()
        self.options.update({"timing-position": "b", "judgement-position": "up100"})
        result = self.export()
        self.assertEqual(result["handled_root_ids"], ["p1.timing.g0.root"])
        self.assertEqual([(i["w"], i["h"]) for i in result["image"]], [(48, 20), (48, 20)])
        for destination, rgb in zip(result["destination"], ((0, 0, 255), (255, 0, 0))):
            self.assertEqual(destination["offsets"], [3])
            self.assertEqual({k: destination["dst"][0][k] for k in ("x", "y", "w", "h")}, {"x": 132, "y": 810, "w": 48, "h": 20})
            self.assertEqual(tuple(destination["dst"][0][k] for k in ("r", "g", "b")), rgb)
            self.assertEqual(destination["timer"], 46)
        self.assertEqual(len(self.sink.source), 1)  # The fixture glyphs share white pixels; tint remains per destination.

    def test_standard_sp2_owns_first_region_and_dp_c_uses_short_original_labels_per_stage(self):
        self.use_standard_frame_feedback()
        self.assertEqual([d["timer"] for d in self.export("7k", "p2")["destination"]], [46, 46])
        self.options["timing-position"] = "c"
        result = self.export("14k", "dp")
        self.assertEqual([(i["w"], i["h"]) for i in result["image"]], [(24, 20)] * 4)
        self.assertEqual([d["timer"] for d in result["destination"]], [46, 46, 47, 47])
        self.assertEqual([d["dst"][0]["x"] for d in result["destination"]], [692, 692, 1748, 1748])
        self.assertTrue(all(d["offsets"] == [3] for d in result["destination"]))
        self.assertIn("main_state.number(526)", result["destination"][2]["draw"])

    def test_standard_off_and_suppressed_stage_marker_add_no_art(self):
        self.use_standard_frame_feedback()
        for key in ("timing-position", "judgement-position"):
            with self.subTest(option=key):
                previous = self.options[key]
                self.options[key] = "off"
                result = self.export()
                self.assertEqual(result["image"], [])
                self.assertEqual(result["destination"], [])
                self.options[key] = previous
        frame = self.parts["frame"]
        self.parts["frame"] = Contribution(frame.module, frame.choice, None, None, None, frame.manifest, frame.scene, frame.ini.replace('resource Provide', 'resource Suppress'))
        self.assertEqual(self.export()["destination"], [])
        self.assertEqual(self.sink.source, {})

    def test_standard_different_lift_transform_is_not_silently_replaced(self):
        self.use_standard_frame_feedback()
        self.parts["frame"].scene["tracks"][0]["keyframes"][1]["value"] = -.5
        with self.assertRaisesRegex(ValueError, "Lift track"):
            self.export()

    def test_actual_judgement_marker_suppression_prevents_timing_art(self):
        self.parts["judgement"] = Contribution("judgement", "test", None, None, None, {}, {},
            '[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode=7k presentation=p1 group=bms.group.deck-1 group-logical=0\nhud.judgement: resource Suppress')
        self.assertEqual(self.export()["destination"], [])
        self.assertEqual(self.sink.source, {})


if __name__ == "__main__":
    unittest.main()
