"""Synthetic regressions for the earlier Full Combo's script rectangle."""
import copy
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_fullcombo_catalog import MATRIX_FIELDS
from standard_script_masks import IDENTITY, instance_at_depth, rectangle_bounds, set_mask


class Clip(SimpleNamespace):
    pass


class Shape(SimpleNamespace):
    pass


def source():
    attrs = dict(transform=SimpleNamespace(**dict(zip(MATRIX_FIELDS, IDENTITY))),
                 rotation_origin=SimpleNamespace(x=0, y=0, z=0), mask=None, visible=True)
    shape = Shape(**copy.deepcopy(attrs), source=SimpleNamespace(reference="synthetic,shape"))
    mask = Clip(**copy.deepcopy(attrs), depth=1, placed_objects=[shape], _width=120, _height=240)
    mask.visible = False
    target = Clip(placed_objects=[mask])
    set_mask(target, instance_at_depth(target, -16383))
    geometry = [{"vertices": [[0, 0, 0], [16, 0, 0], [0, 16, 0], [16, 16, 0]], "colours": [],
                 "draws": [{"flags": 9, "texture": None, "uv": [], "indices": [], "sourceColour": [1, 0, 1, 1]}]}]
    return target, mask, shape, SimpleNamespace(PlacedClip=Clip, PlacedShape=Shape), geometry, {"synthetic": 0}


class ScriptMaskTests(unittest.TestCase):
    def bounds(self, value):
        target, _, _, tools, geometry, mapping = value
        return rectangle_bounds(target, tools, geometry, mapping)

    def test_script_resize_uses_current_dimensions_each_frame(self):
        value = source()
        self.assertEqual([0, 0, 120, 240], self.bounds(value))
        value[1]._width, value[1]._height = 300, 400
        self.assertEqual([0, 0, 300, 400], self.bounds(value))

    def test_depth_lookup_does_not_select_another_child(self):
        target, mask, *_ = source()
        with self.assertRaisesRegex(ValueError, "exactly one"):
            instance_at_depth(target, -16382)
        target.placed_objects.append(Clip(depth=1))
        with self.assertRaisesRegex(ValueError, "exactly one"):
            instance_at_depth(target, -16383)
        with self.assertRaisesRegex(ValueError, "integer"):
            instance_at_depth(target, -16383.0)

    def test_reparented_mask_is_rejected_at_the_next_frame(self):
        value = source()
        value[0].placed_objects.clear()
        with self.assertRaisesRegex(ValueError, "belongs"):
            self.bounds(value)
        with self.assertRaisesRegex(ValueError, "direct child"):
            set_mask(value[0], value[1])

    def test_transformed_geometry_cannot_be_silently_clipped_as_a_rectangle(self):
        for which in (1, 2):
            with self.subTest(which=which):
                value = source()
                value[which].transform.a41 = 10
                with self.assertRaisesRegex(ValueError, "transform"):
                    self.bounds(value)
        value = source()
        value[1]._rotation = 90
        with self.assertRaisesRegex(ValueError, "script transforms"):
            self.bounds(value)

    def test_visible_or_multiple_mask_children_are_not_dropped(self):
        value = source()
        value[1].visible = True
        with self.assertRaisesRegex(ValueError, "hidden rectangle"):
            self.bounds(value)
        value = source()
        value[1].placed_objects.append(value[2])
        with self.assertRaisesRegex(ValueError, "hidden rectangle"):
            self.bounds(value)

    def test_unknown_solid_and_textured_shapes_fail(self):
        for change in ({"flags": 1}, {"texture": 0}, {"sourceColour": [1, 1, 1, .5]}):
            with self.subTest(change=change):
                value = source()
                value[4][0]["draws"][0].update(change)
                with self.assertRaisesRegex(ValueError, "opaque solid"):
                    self.bounds(value)
        value = source()
        value[4][0]["vertices"][3][0] = 15
        with self.assertRaisesRegex(ValueError, "origin-aligned rectangle"):
            self.bounds(value)

    def test_invalid_dimensions_fail_instead_of_masking_everything(self):
        for width in (0, -1, float("nan"), float("inf"), 10000, True):
            with self.subTest(width=width):
                value = source()
                value[1]._width = width
                with self.assertRaisesRegex(ValueError, "dimensions"):
                    self.bounds(value)


if __name__ == "__main__":
    unittest.main()
