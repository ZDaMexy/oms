"""Author-only beam routing checks; no game media or engine policy changes."""

import copy
from pathlib import Path
import sys
import unittest

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from build_omni_skin import (beam_ini_paths, correct_beam_material, correct_frame_lane_surface,
                             correct_scene_author_contracts, scene_document)


class BeamLayerTests(unittest.TestCase):
    def scene(self, animated):
        scene = scene_document("b.b00.hd.normal.root")
        for colour in ("white", "black", "scratch"):
            identifier = "b.b00.hd.normal." + colour
            root = {"id": identifier, "type": "recorded-animation" if animated else "sprite",
                    "slot": "decoration", "properties": {"height": 0.9945}, "children": [],
                    "animation" if animated else "resource": "original." + colour}
            scene["templates"].append({"id": identifier + ".t", "root": root})
            scene["instances"].append({"id": identifier + ".i", "template": identifier + ".t",
                                       "material": {"slot": "decoration", "resource": "custom/markers/b/" + colour}})
        scene["bindings"] = [{"id": "held", "target": "b.b00.hd.normal.white", "property": "visible", "source": "input.pressed"}]
        return scene

    def test_static_and_recorded_beams_move_only_their_slot_ownership(self):
        for animated in (False, True):
            with self.subTest(animated=animated):
                scene = self.scene(animated)
                expected = copy.deepcopy(scene)
                for template, instance in zip(expected["templates"], expected["instances"]):
                    template["root"]["slot"] = instance["material"]["slot"] = "playfield.lane-surface"
                changes = correct_scene_author_contracts(scene)
                self.assertEqual(3, len(changes["beamLaneSurfaceRoots"]))
                self.assertEqual(expected, scene)
                self.assertFalse(any(correct_scene_author_contracts(scene).values()))

    def test_unrelated_decoration_is_preserved(self):
        scene = self.scene(True)
        scene["root"]["id"] = "another-skin.root"
        before = copy.deepcopy(scene)
        self.assertFalse(any(correct_scene_author_contracts(scene).values()))
        self.assertEqual(before, scene)

    def test_conflicting_beam_ownership_is_rejected(self):
        scene = self.scene(True)
        scene["templates"][0]["root"]["slot"] = "effect.hit-explosion"
        with self.assertRaisesRegex(ValueError, "ownership disagree"):
            correct_scene_author_contracts(scene)

    def test_beam_material_and_frame_placeholder_do_not_duplicate_lane_surface(self):
        for newline in (b"\n", b"\r\n"):
            with self.subTest(newline=newline):
                prefix = b"[GameplaySkin.Common:1]" + newline + b"Target: Lane ruleset=bms" + newline
                decoration = b'decoration: resource Provide "custom/markers/b/white"' + newline
                frame = prefix + b'playfield.lane-surface: resource Provide "gameplay/frames/iidx33/common/empty"' + newline
                other = b'playfield.lane-divider: resource Provide "common/empty"' + newline
                fixed_frame, removed = correct_frame_lane_surface(frame + other)
                fixed_beam, moved = correct_beam_material(prefix + decoration)
                self.assertEqual(1, removed)
                self.assertEqual(1, moved)
                self.assertEqual(prefix + other, fixed_frame)
                self.assertEqual(prefix + decoration.replace(b"decoration:", b"playfield.lane-surface:"), fixed_beam)
                self.assertEqual((fixed_beam, 0), correct_beam_material(fixed_beam))
                self.assertEqual((fixed_frame, 0), correct_frame_lane_surface(fixed_frame))

    def test_non_empty_frame_surface_is_not_silently_replaced(self):
        with self.assertRaisesRegex(ValueError, "non-empty lane surface"):
            correct_frame_lane_surface(b'playfield.lane-surface: resource Provide "another-background"\n')

    def test_frame_fullcombo_only_probe_has_no_beam_materials_to_move(self):
        self.assertEqual([], beam_ini_paths({"modules": [{"id": "frame", "choices": []}]}))


if __name__ == "__main__":
    unittest.main()
