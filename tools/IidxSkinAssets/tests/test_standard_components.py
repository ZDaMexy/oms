"""Regression checks for independent Standard author contribution contracts."""
import copy
import math
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from standard_components import FRAME_IDS, StandardBranch, StandardComponents, terminal_repeat_control, turntable_motion


class AP2DoActionTag:
    def __init__(self):
        actions = [
            {"action": "PUSH", "objects": ["'loop'", "THIS", "2", "StringConstant(0x390: aeplib)"]},
            {"action": "GET_VARIABLE"},
            {"action": "PUSH", "objects": ["StringConstant(0x815: deepGotoAndPlay)"]},
            {"action": "CALL_METHOD"}, {"action": "END"}]
        self.bytecode = SimpleNamespace(as_dict=lambda: {"actions": actions})

    def as_dict(self):
        return {"type": type(self).__name__, "bytecode": self.bytecode.as_dict()}


class AP2RemoveObjectTag:
    def as_dict(self):
        return {"type": type(self).__name__}


def source_loop():
    return SimpleNamespace(labels={"loop": 0},
        tags=[SimpleNamespace(), SimpleNamespace(), AP2RemoveObjectTag(), AP2DoActionTag()],
        frames=[SimpleNamespace(start_tag_offset=0, num_tags=1),
                SimpleNamespace(start_tag_offset=1, num_tags=1),
                SimpleNamespace(start_tag_offset=2, num_tags=2)])


def memory_owner():
    owner = object.__new__(StandardComponents)
    owner.modules, owner.branch_counts, owner.documents = [], [], {}
    owner.json = lambda path, value: owner.documents.__setitem__(path, copy.deepcopy(value))
    owner.text = lambda path, value: owner.documents.__setitem__(path, value)
    return owner


def turntable_draw(binding, mirrored=False):
    angle = .013
    cosine, sine = math.cos(angle), math.sin(angle)
    a, b = (-cosine, -sine) if mirrored else (cosine, sine)
    c, d = -sine, cosine
    matrix = {"a": a, "b": b, "c": c, "d": d, "tx": 300 - a * 50 - c * 50, "ty": 400 - b * 50 - d * 50}
    source = [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (100, 0), (0, 100), (100, 100))]
    vertices = [{"x": a * p["x"] + c * p["y"] + matrix["tx"],
                 "y": b * p["x"] + d * p["y"] + matrix["ty"], "z": 0} for p in source]
    return {"game_bindings": [binding], "matrix": matrix, "source_vertices": source, "projected_vertices": vertices,
            "asset": {"width": 100, "height": 100},
            "bounds": {"left": min(p["x"] for p in vertices), "right": max(p["x"] for p in vertices),
                       "top": min(p["y"] for p in vertices), "bottom": max(p["y"] for p in vertices)}}


class StandardComponentsTests(unittest.TestCase):
    def test_catalog_scene_path_does_not_replace_fixed_manifest_contract(self):
        owner = memory_owner()
        branch = StandardBranch(owner, "sc", "c00")
        contribution = branch.finish()
        self.assertEqual("gameplay-skin.scene.json", owner.documents[contribution["manifest"]]["scene"])
        self.assertIn(contribution["scene"], owner.documents)

    def test_full_combo_default_is_dp_without_requiring_a_dp_layout_option(self):
        owner = memory_owner()
        owner.packages = {f"fcombo{i:02}": {} for i in range(18)}
        owner.index = {(package, "art"): {"width": 1, "height": 1} for package in owner.packages}
        owner.preview = lambda *args: "preview.png"
        owner.receipts = []
        owner.clip = lambda *args, **kwargs: {
            "node": {"id": "source", "type": "container", "children": []},
            "files": [], "resources": [], "animations": [], "source": {"durationMs": 2500}}
        owner.fullcombo()
        for choice in owner.modules[0]["choices"]:
            self.assertTrue(choice["scene"].endswith("/dp/scene.json"))
            self.assertEqual([{"layout": "p1"}, {"layout": "p2"}], [v["when"] for v in choice["variants"]])
            for branch in [choice, *choice["variants"]]:
                scene = owner.documents[branch["scene"]]
                self.assertEqual(2500, scene["completionOutro"])
                eligibility = next(b for b in scene["bindings"] if b["source"] == "gameplay.full-combo")
                retention = next(t for t in scene["tracks"] if t.get("trigger") == "gameplay.complete")
                self.assertNotEqual(eligibility["target"], retention["target"])
                self.assertFalse(retention["loop"])
                self.assertEqual({"time": 2500, "value": 0}, {k: retention["keyframes"][-1][k] for k in ("time", "value")})

    def test_judgement_grade_gate_does_not_overwrite_palette_phase_opacity(self):
        owner = memory_owner()
        owner.packages = {f"judge{i:02}": {} for i in range(14)}
        owner.options, owner.receipts, owner.contexts = [], [], []
        owner.preview = lambda *args: "preview.png"
        owner.fallback_marker = lambda *args: "marker"
        owner.asset = lambda category, package, name: ({"id": category + "." + name, "path": "source.png"}, {})
        owner.judgement_atlas = lambda package: (
            {"id": "atlas." + package, "type": "texture", "path": "atlas.png"},
            {(palette, digit): [digit * 55, 0, 55, 74] for palette in "bgry" for digit in range(10)}, 74)
        owner.judgements()
        for choice in owner.modules[0]["choices"]:
            scene = owner.documents[choice["scene"]]
            opacity_targets = {t["target"] for t in scene["tracks"] if t["property"] == "opacity"}
            visible_targets = {v["target"] for v in scene["variants"] if v["property"] == "visible"}
            self.assertEqual(3, len(opacity_targets))
            self.assertFalse(opacity_targets & visible_targets)

    def test_turntable_export_keeps_the_source_quad_and_uses_logical_motion_groups(self):
        owner = memory_owner()
        owner.frame_maps = Path("mapping.json")
        owner.packages = {f"turn{i:02}": {} for i in range(21)}
        owner.receipts = []
        owner.preview = lambda *args: "preview.png"
        owner.asset = lambda category, package, name: ({"id": "source." + name, "path": "source.png"}, {})
        maps = {frame: {"stage": {"width": 1280, "height": 720}, "layouts": {
            "1p_frame": {"draws": [turntable_draw("turn_1p")]},
            "2p_frame": {"draws": [turntable_draw("turn_2p", frame == "iidx25")]},
            "dp_frame_1p": {"draws": [turntable_draw("turn_1p"), turntable_draw("turn_2p")]}}} for frame in FRAME_IDS}
        inputs = {"mapping.json": {frame: frame + ".json" for frame in FRAME_IDS},
                  **{frame + ".json": value for frame, value in maps.items()}}
        with patch("standard_components.read_json", side_effect=lambda path: inputs[str(path)]), \
                patch("standard_components.within", side_effect=lambda _, relative: Path(relative)):
            owner.turntables()
        for choice in owner.modules[0]["choices"]:
            scene = owner.documents[choice["scene"]]
            manifest = owner.documents[choice["manifest"]]
            motions = [t for t in scene["tracks"] if t["property"] == "rotation"]
            self.assertEqual(40, len(motions))
            self.assertEqual([], manifest["animations"])
            nodes = {n["id"]: n for t in scene["templates"] for n in self.walk(t["root"])}
            for motion in motions:
                target = motion["target"]
                group = 2 if ".dp." in target and target.endswith(".n1") else 1
                self.assertEqual(f"input.motion-revolutions-{group}", motion["clock"])
                self.assertNotIn("trigger", motion)
                self.assertTrue(motion["loop"])
                self.assertEqual("linear", motion["easing"])
                expected_direction = -1 if ".p2.iidx25." in target else 1
                self.assertEqual([0, expected_direction * 360], [k["value"] for k in motion["keyframes"]])
                orientation = next(t for t in scene["tracks"] if t["target"] == target and t["property"] == "projective-quad")
                art = nodes[target]
                self.assertEqual("centre", art["properties"]["origin"])
                draw = turntable_draw("unused", expected_direction < 0)
                props = art["properties"]
                for vertex, source_index in zip(orientation["keyframes"][0]["value"]["vertices"], (0, 1, 3, 2)):
                    self.assertAlmostEqual(draw["projected_vertices"][source_index]["x"],
                                           (props["x"] + (vertex[0] - .5) * props["width"]) * 1280)
                    self.assertAlmostEqual(draw["projected_vertices"][source_index]["y"],
                                           (props["y"] + (vertex[1] - .5) * props["height"]) * 720)

    @staticmethod
    def walk(root):
        yield root
        for child in root.get("children", []):
            yield from StandardComponentsTests.walk(child)

    def test_projected_rotation_matches_original_local_rotation_including_negative_motion(self):
        for mirrored in (False, True):
            draw = turntable_draw("turn_2p", mirrored)
            props, track, _ = turntable_motion("test.turn", draw, "p2", 0)
            m = draw["matrix"]
            self.assertEqual("input.motion-revolutions-1", track["clock"])
            for revolutions in (-1.25, -.25, 0, .25, 1.25):
                phase = revolutions % 1
                theta = phase * math.tau
                displayed = math.radians(phase * track["keyframes"][-1]["value"])
                for point, projected in zip(draw["source_vertices"], draw["projected_vertices"]):
                    x, y = point["x"] - 50, point["y"] - 50
                    rx, ry = x * math.cos(theta) - y * math.sin(theta), x * math.sin(theta) + y * math.cos(theta)
                    expected = (300 + m["a"] * rx + m["c"] * ry, 400 + m["b"] * rx + m["d"] * ry)
                    px, py = projected["x"] - 300, projected["y"] - 400
                    actual = (props["x"] * 1280 + px * math.cos(displayed) - py * math.sin(displayed),
                              props["y"] * 720 + px * math.sin(displayed) + py * math.cos(displayed))
                    self.assertAlmostEqual(expected[0], actual[0])
                    self.assertAlmostEqual(expected[1], actual[1])
            self.assertEqual(0, track["keyframes"][0]["value"], "Unavailable motion is zero, preserving the source quad")

    def test_turntable_shear_or_unequal_axis_scale_cannot_acquire_rigid_rotation(self):
        for matrix in ({"a": 2, "b": 0, "c": 0, "d": 1}, {"a": 1, "b": .1, "c": 0, "d": 1},
                       {"a": 0, "b": 0, "c": 0, "d": 0}):
            draw = turntable_draw("turn_1p")
            draw["matrix"].update(matrix)
            with self.assertRaisesRegex(ValueError, "not a uniform similarity"):
                turntable_motion("test.turn", draw, "p1", 0)

    def test_root_loop_boundary_does_not_insert_an_empty_hold_frame(self):
        movie = source_loop()
        control = terminal_repeat_control(movie)
        self.assertEqual(2, control["playbackFrames"])
        self.assertEqual(3, len(movie.frames))
        self.assertEqual(2, len(control["sourceBoundary"]))

    def test_a_drawing_frame_cannot_be_silently_dropped_as_loop_control(self):
        movie = source_loop()
        movie.tags[2] = SimpleNamespace()
        with self.assertRaisesRegex(ValueError, "unqualified jump boundary"):
            terminal_repeat_control(movie)

    def test_an_unknown_action_is_not_relabelled_as_a_known_source_loop(self):
        movie = source_loop()
        movie.tags[-1].bytecode.as_dict()["actions"][2]["objects"] = ["StringConstant(0x814: otherMethod)"]
        with self.assertRaisesRegex(ValueError, "literal original loop"):
            terminal_repeat_control(movie)


if __name__ == "__main__":
    unittest.main()
