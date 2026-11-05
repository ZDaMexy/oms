"""Public contribution and source-boundary regressions, without private art."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from build_standard_skin import Frame, document
from standard_feedback import build_feedback, source_hook, prepare_feedback_font


def walk(node):
    yield node
    for child in node["children"]:
        yield from walk(child)


class StandardFeedbackTests(unittest.TestCase):
    def test_export_uses_public_glyph_rects_and_unique_full_texture_paths(self):
        from PIL import Image
        def raster(_font, text, *, cache_height, cell):
            return Image.new('RGBA', (len(text) * cell[0], cell[1]), (240, 240, 240, 255))
        with patch('standard_feedback.rasterize_source_text', side_effect=raster):
            exported = prepare_feedback_font(self.font_file, self.output)
        resources = {row['id']: row for row in exported['resources']}
        self.assertEqual(len(resources), len({row['path'].casefold() for row in resources.values()}))
        self.assertTrue(all(set(row) == {'id','type','path','filter'} for row in resources.values()))
        for font in exported['bitmapFonts']:
            self.assertEqual(len({glyph['resource'] for glyph in font['glyphs']}), 1)
            for glyph in font['glyphs']:
                self.assertEqual(len(glyph['rect']), 4)
                with Image.open(self.output / resources[glyph['resource']]['path']) as image:
                    x, y, width, height = glyph['rect']
                    self.assertLessEqual(x + width, image.width)
                    self.assertLessEqual(y + height, image.height)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.library = self.root / "library"
        self.library.mkdir()
        self.output = self.root / "output"
        self.font_file = self.root / "source-font.bin"
        self.font_file.write_bytes(b"synthetic source font identity; never private font data")
        self.font_sha = hashlib.sha256(self.font_file.read_bytes()).hexdigest()
        path = self.library / "sprites/judge_waku.png"
        path.parent.mkdir()
        path.write_bytes(b"independent synthetic source panel")
        self.panel_sha = hashlib.sha256(path.read_bytes()).hexdigest()
        self.texture = {"package": "contents/data/graphic/0/gameparts.ifs", "name": "judge_waku.png",
                        "width": 111, "height": 163, "sprite": "sprites/judge_waku.png", "sha256": self.panel_sha}
        self.inventory = {"status": "complete", "sourceMetadataUnchanged": True, "textures": [self.texture]}
        self.reference = {"package": "gmframe26", "common_family": "0", "stage": {"width": 1280, "height": 720, "fps": 60.0}, "layouts": {}}
        for movie, x, y, side in (("center_frame_sp", 362, 544, "1p"),
                                  ("center_frame_wide_1p", 251, 401, "1p"),
                                  ("center_frame_wide_2p", 807, 474, "2p"),
                                  ("center_frame_dp_1p", 119, 529, "1p")):
            self.reference["layouts"][movie] = {"draws": [self.panel(x, y, side)]}
        for movie, side, x in (("1p_frame", "1p", 48), ("2p_frame", "2p", 940),
                               ("center_frame_dp_1p", "1p", 304), ("center_frame_dp_1p", "2p", 684)):
            self.reference["layouts"].setdefault(movie, {"draws": []})["draws"].append(
                {"game_bindings": ["lane_" + side], "texture": "lane", "bounds": {"left": x, "top": 0, "right": x + 292, "bottom": 484}})
        self.map = self.root / "map.json"
        self.font = {"resources": [], "bitmapFonts": [], "files": [], "fonts": {"normal": "test.normal", "small": "test.small"},
                     "labels": {}, "receipt": {"sourceFontSHA256": self.font_sha}}
        for size, width, height in (("normal", 12, 20), ("small", 8, 18)):
            for label in ("PG:", "GR:", "GD:", "BD:", "PR:", "CB:", "MAX", "300", "200", "100", "50", "MISS", "FA:", "/SL:", "FAST", "SLOW", "FA", "SL"):
                self.font["labels"][size, label] = {"resource": "test.label." + size + str(len(self.font["labels"])),
                                                  "width": len(label) * width, "height": height}
        self.save()

    def panel(self, x, y, side):
        return {"game_bindings": ["judge_" + side], "texture": "judge_waku", "bounds": {"left": x, "top": y, "right": x + 111, "bottom": y + 163},
                "matrix": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": x, "ty": y},
                "asset": {"package": "0/gameparts", "width": 111, "height": 163, "sha256": self.panel_sha}}

    def save(self):
        (self.library / "library.json").write_text(json.dumps(self.inventory), "utf-8")
        self.map.write_text(json.dumps(self.reference), "utf-8")

    def build(self, layout="p1", movie="center_frame_sp", dx=36):
        return build_feedback(self.library, self.map, style=26, layout=layout, centre_movie=movie,
                              centre_dx=dx, contexts=[c for c in material_contexts() if c["layout"] == layout],
                              font_path=self.font_file, output=self.output, prepared_font=self.font)

    @staticmethod
    def nodes(value):
        return [n for template in value["templates"] for n in walk(template["root"])]

    def test_each_centre_uses_its_own_source_hook_before_caller_offset(self):
        narrow, wide = self.build(), self.build(movie="center_frame_wide_1p", dx=0)
        panels = [next(n for n in self.nodes(value) if n["id"].endswith(".judge.bms.panel")) for value in (narrow, wide)]
        self.assertAlmostEqual((362 + 36) / 1280, panels[0]["properties"]["x"])
        self.assertAlmostEqual(251 / 1280, panels[1]["properties"]["x"])
        self.assertAlmostEqual(401 / 720, panels[1]["properties"]["y"])

    def test_judge_rows_use_real_ruleset_counts_and_native_bms_structure(self):
        value = self.build()
        by_ruleset = {ruleset: [b["source"] for b in value["bindings"] if ".judge." + ruleset + "." in b["target"] and b["property"] == "text"]
                      for ruleset in ("bms", "mania")}
        self.assertEqual(["judgement.count." + r for r in ("perfect", "great", "good", "meh")] + ["value.standard-poor"] +
                         ["combo.breaks", "judgement.count.fast", "judgement.count.slow"], by_ruleset["bms"])
        self.assertEqual([{"id": "standard-poor", "operation": "add", "left": "judgement.count.miss",
                           "right": "judgement.count.ok"}], value["numericSources"])
        self.assertEqual(["judgement.count." + r for r in ("perfect", "great", "good", "ok", "meh", "miss")] +
                         ["combo.breaks", "judgement.count.fast", "judgement.count.slow"], by_ruleset["mania"])

    def test_judge_uses_full_canvas_owner_with_separate_ruleset_preference_and_preparation_gates(self):
        for layout, movie, dx in (("p1", "center_frame_sp", 36),
                                  ("p1", "center_frame_wide_1p", 0),
                                  ("p2", "center_frame_wide_2p", 0),
                                  ("dp", "center_frame_dp_1p", 0)):
            with self.subTest(layout=layout, movie=movie):
                value = self.build(layout, movie, dx)
                instances = [i for i in value["instances"] if ".judge." in i["id"]]
                self.assertEqual(2, len(instances))
                self.assertTrue(all(i["material"] == {"slot": "decoration",
                    "resource": f"markers/frame-{layout}-information"} for i in instances))
                roots = [t["root"] for t in value["templates"] if ".judge." in t["id"]]
                for ruleset in ("bms", "mania"):
                    root = next(n for n in roots if n["id"].endswith(f".judge.{ruleset}.root"))
                    self.assertEqual("decoration", root["slot"])
                    self.assertEqual({"kind": "global"}, root["target"])
                    self.assertFalse({"x", "y", "width", "height"} & root["properties"].keys())
                    preference, = root["children"]
                    self.assertEqual(root["id"] + ".preference", preference["id"])
                    canvas, = preference["children"]
                    self.assertTrue(canvas["id"].endswith(f".judge.{ruleset}.canvas"))
                    rule_gate, = [v for v in value["variants"] if v["target"] == root["id"]]
                    self.assertEqual("layout.ruleset", rule_gate["source"])
                    self.assertFalse(rule_gate["default"])
                    self.assertEqual([(ruleset, True)], [(c["key"], c["value"]) for c in rule_gate["cases"]])
                    binding, = [b for b in value["bindings"] if b["target"] == preference["id"]]
                    self.assertEqual(("visible", "option.realtime-judge"), (binding["property"], binding["source"]))
                    opening_gate, = [v for v in value["variants"] if v["target"] == canvas["id"]]
                    self.assertEqual("preparation.active", opening_gate["source"])
                    self.assertTrue(opening_gate["default"])
                    self.assertEqual([("true", False)], [(c["key"], c["value"]) for c in opening_gate["cases"]])
                global_targets = [t for t in value["targets"] if t["scope"] == "Global"]
                self.assertTrue(global_targets)
                self.assertTrue(all(t["slot"] == "decoration" and t["material"] == f"markers/frame-{layout}-information"
                                    for t in global_targets))

    def test_failure_is_one_direct_foreground_owner_independent_of_judge_templates(self):
        value = self.build()
        frame = object.__new__(Frame)
        frame.prefix = "test.frame"
        frame.common_family = '0'
        frame.owner = SimpleNamespace(failure_clip={"resources": [], "animations": [
            {"id": "test.failure.clip", "path": "synthetic/failure.json.br", "resources": []}],
            "node": {"id": "test.failure.node", "type": "recorded-animation", "target": {"kind": "global"},
                     "animation": "test.failure.clip", "blend": "alpha", "effects": [], "children": [],
                     "properties": {"x": 0, "y": 0, "width": 1, "height": 1}}},
            failure_receipt={"source": "synthetic full-canvas failure"})
        frame.owner.failure_families = {'0': {'clip':frame.owner.failure_clip, 'receipt':frame.owner.failure_receipt}}
        frame.resources, frame.animations, frame.evidence = {}, {}, []
        frame.scene = document("test.root")
        frame.scene.update({field: value[field] for field in ("bindings", "variants", "templates", "instances")})
        original_templates = json.loads(json.dumps(value["templates"]))
        original_instances = json.loads(json.dumps(value["instances"]))
        frame.failure_feedback()
        self.assertEqual(3000, frame.scene["failureOutro"])
        self.assertEqual(["test.failure.clip"], list(frame.animations))
        self.assertEqual(original_templates, frame.scene["templates"], "Failure must not alter the JUDGE owners or gates.")
        self.assertEqual(original_instances, frame.scene["instances"], "A material selector would squeeze the child into the footer.")
        root, = frame.scene["root"]["children"]
        self.assertEqual(("hud.text", {"kind": "global"}, {"z": -100}),
                         (root["slot"], root["target"], root["properties"]))
        failure, = root["children"]
        self.assertEqual(frame.owner.failure_clip["node"], failure, "Retain the original clip, target, animation and full-canvas properties.")
        self.assertIsNot(frame.owner.failure_clip["node"], failure)
        self.assertNotIn("slot", failure)
        self.assertEqual(1, sum(n["type"] == "recorded-animation" for n in walk(frame.scene["root"])))
        failure_ids = {root["id"], failure["id"]}
        for field in ("bindings", "variants", "tracks"):
            self.assertFalse(any(row["target"] in failure_ids for row in frame.scene[field]),
                             "JUDGE, preparation and ruleset preferences cannot gate the universal failure clip.")

    def test_feedback_material_ini_provides_global_failure_without_repeating_it_per_stage(self):
        value = self.build("dp", "center_frame_dp_1p", 0)
        frame = object.__new__(Frame)
        frame.layout, frame.ini = "dp", []
        frame.owner = SimpleNamespace(empty_path="synthetic/empty.png")
        frame.profiles = lambda: []
        frame.material_targets = {(t["scope"], t["target"], t["slot"]): t["material"] for t in value["targets"]}
        controls = {lane["target"]: "synthetic/key" for c in material_contexts() if c["layout"] == "dp" for lane in c["lanes"]}
        frame.material_ini("synthetic/background", "markers/frame-dp-information", controls)
        sections = [block.splitlines() for block in "\n".join(frame.ini).split("[GameplaySkin.Common:1]") if block.strip()]
        globals_ = [block for block in sections if any(line.startswith("Target: Global ") for line in block)]
        self.assertTrue(globals_)
        for block in globals_:
            self.assertEqual(['decoration: resource Provide "markers/frame-dp-information"'],
                             [line for line in block if line.startswith("decoration:")])
            self.assertEqual(['hud.text: resource Provide "synthetic/empty"'], [line for line in block if line.startswith("hud.text:")])
        self.assertTrue(any("ruleset=bms" in line for block in globals_ for line in block))
        self.assertTrue(any("ruleset=mania" in line for block in globals_ for line in block))
        stages = [block for block in sections if any(line.startswith("Target: Stage ") for line in block)]
        self.assertTrue(all("decoration: resource Suppress" in block for block in stages))
        self.assertTrue(all("hud.text: resource Suppress" in block for block in stages))

    def test_dp_timing_mounts_exact_stages_without_global_timing_instances(self):
        value = self.build("dp", "center_frame_dp_1p", 0)
        instances = [i for i in value["instances"] if ".timing." in i["id"]]
        self.assertEqual(2, len(instances))
        self.assertTrue(all(i["material"]["slot"] == "stage.foreground" for i in instances))
        targets = [t for t in value["targets"] if t["scope"] == "Stage"]
        self.assertTrue(any("bms.group.deck-1" in t["target"] for t in targets))
        self.assertTrue(any("bms.group.deck-2" in t["target"] for t in targets))
        nodes = self.nodes(value)
        for group, x in ((0, 608), (1, 648)):
            art = next(n for n in nodes if n["id"].endswith(f".timing.g{group}.height.normal.position.c.fast"))
            self.assertAlmostEqual((x - (304 if group == 0 else 684)) / 292, art["properties"]["x"])
            self.assertEqual(24 / 292, art["properties"]["width"])
            self.assertEqual("#0000ffff", art["properties"]["colour"])
        self.assertTrue(all(n["properties"]["colour"] == "#ff0000ff" for n in nodes if n["id"].endswith(".slow") and ".timing." in n["id"]))

    def test_position_off_matches_no_height_or_direction_branch(self):
        value = self.build()
        height_variants = [v for v in value["variants"] if v["source"] == "option.judgement-position"]
        self.assertEqual({"normal", "up100", "up200", "up300"}, {c["key"] for v in height_variants for c in v["cases"]})
        self.assertTrue(all(v["default"] is False for v in height_variants))
        position_variants = [v for v in value["variants"] if v["source"] == "option.timing-position"]
        self.assertTrue(all(v["default"] is False for v in position_variants))
        self.assertFalse(any(c["key"] == "off" for v in position_variants for c in v["cases"]))

    def test_real_lift_and_shared_position_are_separate_from_counts_and_preparation(self):
        value = self.build()
        lift = next(t for t in value["tracks"] if t["clock"] == "lift.offset")
        self.assertEqual([0, -1], [k["value"] for k in lift["keyframes"]])
        height = next(n for n in self.nodes(value) if n["id"].endswith(".height.up300"))
        self.assertAlmostEqual(-300 / 484, height["properties"]["y"])
        self.assertTrue(any(v["source"] == "preparation.active" and v["default"] is True for v in value["variants"]))
        self.assertEqual([], value["animations"])
        self.assertFalse(any(t["clock"] == "option.judgement-position" for t in value["tracks"]))

    def test_large_count_fit_respects_public_numeric_track_budget(self):
        for track in self.build()["tracks"]:
            times = [k["time"] for k in track["keyframes"]]
            self.assertEqual(sorted(set(times)), times)
            self.assertLessEqual(max(times), 86_400_000)
            self.assertTrue(all(set(k) == {"id", "time", "value"} for k in track["keyframes"]))
            self.assertNotIn("frames", track)

    def test_unverified_font_and_changed_panel_are_rejected(self):
        self.font_file.write_bytes(b"changed font")
        with self.assertRaisesRegex(ValueError, "different source"):
            self.build()
        self.font["receipt"]["sourceFontSHA256"] = hashlib.sha256(self.font_file.read_bytes()).hexdigest()
        (self.library / self.texture["sprite"]).write_bytes(b"changed panel")
        with self.assertRaisesRegex(ValueError, "disagree"):
            self.build()

    def test_cross_library_panel_path_is_rejected(self):
        outside = self.root / "outside.png"
        outside.write_bytes(b"independent synthetic source panel")
        self.texture["sprite"] = "../outside.png"
        self.save()
        with self.assertRaisesRegex(ValueError, "leaves"):
            self.build()

    def test_original_hook_cannot_silently_lose_rotation(self):
        self.reference["layouts"]["center_frame_sp"]["draws"][0]["matrix"]["b"] = .001
        with self.assertRaisesRegex(ValueError, "non-translational"):
            source_hook(self.reference, "center_frame_sp", "p1")


if __name__ == "__main__":
    unittest.main()
