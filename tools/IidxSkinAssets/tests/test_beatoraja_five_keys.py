"""Six-lane source ownership, independent native mode and unchanged SP/DP routes."""

import itertools
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import AuthorPackage, layout_for, material_bindings
from beatoraja_export import Composer, DEFAULT_MODES, MODE, source_presentation
from beatoraja_materials import NOTE_FIELDS, collect_note_materials, collect_explosion_materials
from beatoraja_native import native_rect
from beatoraja_package import CompositionCache, _options, entry_script, local_controls


AUTHORS = {
    "standard": Path("E:/OMS-Development/sources/OMS-Standard-Model-24-20261107/author-r8"),
    "lightning": Path("E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1"),
}


class FiveKeyTests(unittest.TestCase):
    def author(self, family):
        if not AUTHORS[family].is_dir():
            self.skipTest("The private frozen author is required for the source comparison")
        return AuthorPackage(AUTHORS[family], family)

    def test_composer_separates_chart_mode_from_source_side_and_keeps_legacy_defaults(self):
        author = SimpleNamespace(family="standard", name="Fixture")
        for side, legacy in (("p1", ("7k", 0)), ("p2", ("7k", 0)), ("dp", ("14k", 2))):
            old = Composer(author, None, {}, side, {})
            self.assertEqual(legacy, (old.keymode, old.skin["type"]))
            self.assertEqual(side, old.context["layout.presentation"])
        for side in ("p1", "p2"):
            five = Composer(author, None, {}, side, {}, keymode="5k")
            self.assertEqual(("5k", side, 1), (five.keymode, five.presentation, five.skin["type"]))
        for keymode, side in (("5k", "dp"), ("7k", "dp"), ("14k", "p1"), ("10k", "dp"), ("5k", "5p1")):
            with self.assertRaises(ValueError):
                Composer(author, None, {}, side, {}, keymode=keymode)
        self.assertEqual(("p1", "p2", "dp"), DEFAULT_MODES)
        self.assertEqual(("5k", 1), MODE["5p1"])
        self.assertEqual(("p1", "p2"), (source_presentation("5p1"), source_presentation("5p2")))

    def test_independent_entry_uses_native_side_ids_without_leaking_package_mode_into_labels(self):
        script = entry_script({"name": "Fixture", "type": 1}, ["5p1", "5p2"], ["f"],
                              {}, {}, {}, {"name": "Fixture", "type": 1})
        header = script.split("\nif not skin_config", 1)[0]
        self.assertIn('["name"]="P1"', header)
        self.assertIn('["name"]="P2"', header)
        self.assertNotIn("5P1", header)
        self.assertIn('[920]="5p1"', script)
        self.assertIn('[921]="5p2"', script)

    def test_source_five_key_options_preserve_sp_variant_and_reject_ten_key_inference(self):
        for family in AUTHORS:
            author = self.author(family)
            for side in ("p1", "p2"):
                five = author.options("5k", side)
                self.assertEqual(side, five["layout"])
                selected = _options(author, "5" + side, five["frame"])
                self.assertEqual(five, selected)
                self.assertEqual(author.contribution("frame", author.options("7k", side)).manifest_path,
                                 author.contribution("frame", five).manifest_path)
            for mode, side in (("5k", "dp"), ("10k", "dp"), ("14k", "p2")):
                with self.assertRaises(ValueError):
                    author.options(mode, side)

    def test_all_real_frames_use_their_full_mode_layout_and_correct_scratch_timer(self):
        routes = 0
        for family in AUTHORS:
            author = self.author(family)
            width, height = (1280, 720) if family == "standard" else (1920, 1080)
            for choice in author.modules["frame"]["choices"]:
                for keymode, side in (("5k", "p1"), ("5k", "p2"), ("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                    options = author.options(keymode, side, {"frame": choice["value"]})
                    frame = author.contribution("frame", options)
                    composer = Composer(author, None, options, side, {}, keymode=keymode)
                    layout, lanes = composer.lanes(frame)
                    original = layout_for(frame, keymode, side)
                    self.assertIs(layout, original)
                    bindings = [b for b in material_bindings(frame, keymode, side)
                                if b["slot"] == "playfield.key" and b["mode"] == "Provide"]
                    expected, native_keys = [], []
                    for group, stage in enumerate(original["stages"]):
                        members = [b for b in bindings if int(b["target"]["group-logical"]) == group]
                        scratch = [b for b in members if b["target"]["lane"].startswith("bms.lane.scratch-")]
                        keys = sorted((b for b in members if b not in scratch), key=lambda b: int(b["target"]["group-local-logical"]))
                        self.assertEqual(1, len(scratch))
                        sx, sy, sw, sh = stage["rect"]
                        for offset, b in enumerate([*keys, *scratch]):
                            logical = int(b["target"]["group-local-logical"])
                            lx, lw = stage["lanes"][logical]
                            expected.append(native_rect(((sx + lx * sw) * width, sy * height, lw * sw * width, sh * height), height))
                            native_keys.append((group, 0 if b in scratch else offset + 1, b["target"]["lane"]))
                    self.assertEqual(expected, lanes)
                    effects = list(composer.lane_materials(frame, frame, "playfield.key"))
                    self.assertEqual(native_keys, [(group, key, b["target"]["lane"]) for b, group, key, *_ in effects])
                    self.assertEqual({"5k": 6, "7k": 8, "14k": 16}[keymode], len(lanes))
                    if keymode == "5k" and side == "p2":
                        seven = layout_for(frame, "7k", side)
                        self.assertNotEqual(original["stages"][0]["lanes"][1], seven["stages"][0]["lanes"][1])
                        self.assertEqual(original["stages"][0]["lanes"][1], seven["stages"][0]["lanes"][3])
                    routes += 1
        self.assertEqual(135, routes)

    def test_source_notes_and_charge_controls_keep_exact_art_on_six_real_lanes(self):
        comparisons = 0
        for family in AUTHORS:
            author = self.author(family)
            for side in ("p1", "p2"):
                for module in ("notes", "charge-notes"):
                    # Brightness has no chart-mode branch; source-changing
                    # size/colour/HCN choices need their actual six-lane paths.
                    controls = {k: v for k, v in local_controls(author, module, "5k").items()
                                if k != "lane-objects-brightness"}
                    for choice in author.modules[module]["choices"]:
                        for values in itertools.product(*(v for v in controls.values())):
                            overrides = {module: choice["value"], **dict(zip(controls, (v["value"] for v in values)))}
                            five = collect_note_materials(author, author.options("5k", side, overrides), "5k", side, lambda p: p)
                            seven = collect_note_materials(author, author.options("7k", side, overrides), "7k", side, lambda p: p)
                            self.assertEqual([1, 2, 3, 4, 5, 0], [int(g["source_target"]["global-logical"]) for g in five["geometry"]])
                            self.assertEqual({"5k"}, {g["source_target"]["keymode"] for g in five["geometry"]})
                            for field in NOTE_FIELDS:
                                self.assertEqual(6, len(five["note"][field]))
                                for native, other in enumerate((0, 1, 2, 3, 4, 7)):
                                    self.assertEqual(five["geometry"][native]["slots"][field], seven["geometry"][other]["slots"][field])
                            self.assertEqual(five["images"], seven["images"])
                            comparisons += 1
        self.assertGreaterEqual(comparisons, 300)
        print("5K source note/charge control comparisons: " + str(comparisons))

    def test_cache_separates_six_and_eight_lanes_even_with_identical_art_and_stage(self):
        for family in AUTHORS:
            author = self.author(family)
            options = author.options()
            frame = author.contribution("frame", options)
            cache = CompositionCache(author)
            for stage in ("notes", "charge-notes", "judgement", "key-beam", "explosion", "long-note-effects"):
                self.assertNotEqual(cache.key(stage, "p1", frame, options, 32), cache.key(stage, "5p1", frame, options, 32))
            explosion = collect_explosion_materials(author, options, "5k", "p1")
            self.assertEqual(list(range(6)), sorted(i for b in explosion["bakes"] for i in b["lane_indices"]))


if __name__ == "__main__":
    unittest.main()
