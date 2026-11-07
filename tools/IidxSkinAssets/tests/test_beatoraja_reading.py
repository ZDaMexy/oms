"""Original reading layouts, host gates, fixed clips and faithful font cells."""

from pathlib import Path
import re
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution
from beatoraja_native import AssetSink
from beatoraja_reading import export_reading, reading_ownership
from lighting_gameplay_controls import _readout_row
from standard_cover_readout import add_cover_readouts


def node(identifier, children=(), **properties):
    return {"id": identifier, "type": "container", "properties": properties, "children": list(children)}


class ReadingTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-reading"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        root = Path(self.temporary.name)
        self.source, out = root / "author", root / "output"
        self.source.mkdir(); out.mkdir()
        self.sink = AssetSink(self.source, out)
        self.options = {"frame": "test", "lift-readout": True}
        self.resources, self.fonts = [], []
        for size, advance, height in (("normal", 12, 20), ("small", 8, 18), ("gc.font", 21, 20)):
            glyphs = []
            for char in "0123456789-":
                identifier = size + "." + char
                path = identifier + ".png"
                raster = Image.new("RGBA", (advance, height))
                raster.paste((255, 255, 255, 255), (1, 2, advance - 1, height - 2))
                raster.save(self.source / path)
                self.resources.append({"id": identifier, "type": "texture", "path": path})
                glyphs.append({"character": char, "resource": identifier, "advance": advance})
            self.fonts.append({"id": size, "lineHeight": height, "glyphs": glyphs})
        self.parts = {}
        self.author = SimpleNamespace(family="standard", contribution=lambda name, options: self.parts[name])
        self.standard()

    def standard(self, side="p1"):
        mode = "14k" if side == "dp" else "7k"
        frame = SimpleNamespace(prefix="std.test." + side, layout=side, info=[], evidence=[],
                                scene={"tracks": [], "bindings": [], "variants": []})
        def bind(target, prop, source):
            frame.scene["bindings"].append({"id": target + "." + prop, "target": target, "property": prop, "source": source})
        def steps(target, prop, source, keys):
            frame.scene["tracks"].append({"id": target + "." + prop, "target": target, "property": prop, "clock": source,
                "type": "tween", "easing": "step", "loop": False, "keyframes": [{"time": t, "value": v} for t, v in keys]})
        def variant(target, prop, source, default, cases):
            frame.scene["variants"].append({"id": target + "." + prop, "target": target, "property": prop, "source": source,
                                           "default": default, "cases": [{"key": k, "value": v} for k, v in cases.items()]})
        frame.bind, frame.steps, frame.variant = bind, steps, variant
        add_cover_readouts(frame, {"fonts": {"normal": "normal", "small": "small"}})
        roots = [node("unrelated.information"), *frame.info]
        frame.scene.update({"templates": [{"id": "information", "root": node("information.root", roots)}],
                            "instances": [{"template": "information", "material": {"slot": "decoration", "resource": "marker"}}]})
        stages = [{"rect": [x / 1280, 0, 292 / 1280, 484 / 720]} for x in ((304, 684) if side == "dp" else (48 if side == "p1" else 940,))]
        manifest = {"resources": self.resources, "bitmapFonts": self.fonts,
                    "layouts": [{"ruleset": "bms", "keymode": mode, "presentation": side, "stages": stages}]}
        ini = f'[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode={mode} presentation={side}\ndecoration: resource Provide "marker"'
        self.parts["frame"] = Contribution("frame", "test", None, None, None, manifest, frame.scene, ini)

    def lightning(self, side="p1"):
        self.author.family = "lightning"
        mode = "14k" if side == "dp" else "7k"
        scene = {"tracks": [], "bindings": [], "variants": []}
        root = node("gc.stage", visible=False)
        scene["bindings"].append({"id": "root.visible", "target": root["id"], "property": "visible", "source": "scroll.adjusting"})
        mapping = {"tx_sub_length": "white.label", "tx_notes_time": "green.label"}
        for name, width in (("white.label", 92), ("green.label", 84)):
            Image.new("RGBA", (width, 12), (255, 255, 255, 255)).save(self.source / (name + ".png"))
            self.resources.append({"id": name, "type": "texture", "path": name + ".png"})
        for kind, source, enabled in (("sudden", "cover.sudden-units", "cover.sudden-active"),
                                      ("hidden", "cover.hidden-units", "cover.hidden-active"),
                                      ("lift", "lift.value", "option.lift-readout")):
            row = _readout_row(scene, "row." + kind, source, mapping, stage_width=440, stage_height=728, above=kind == "sudden")
            root["children"].append(row)
            scene["bindings"].append({"id": row["id"] + ".visible", "target": row["id"], "property": "visible", "source": enabled})
            scene["tracks"].append({"id": row["id"] + ".edge", "target": row["id"], "property": "y", "type": "tween", "loop": False,
                "clock": "lift.offset" if kind == "lift" else source, "easing": "linear",
                "keyframes": [{"time": 0, "value": 0 if kind == "sudden" else 1},
                              {"time": 1 if kind == "lift" else 1000, "value": 1 if kind == "sudden" else 0}]})
        scene.update({"templates": [{"id": "readout", "root": root}],
                      "instances": [{"template": "readout", "material": {"slot": "decoration", "resource": "readout.marker"}}]})
        stages = [{"rect": [x / 1920, 0, 440 / 1920, 728 / 1080]} for x in ((384, 1000) if side == "dp" else (72,))]
        manifest = {"resources": self.resources, "bitmapFonts": self.fonts,
                    "layouts": [{"ruleset": "bms", "keymode": mode, "presentation": side, "stages": stages}]}
        ini = "\n".join(f'[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode={mode} presentation={side} group=bms.group.deck-{i + 1} group-logical={i}\ndecoration: resource Provide "readout.marker"' for i in range(len(stages)))
        self.parts["frame"] = Contribution("frame", "test", None, None, None, manifest, scene, ini)

    def export(self, side="p1", canvas=None):
        canvas = canvas or ((0, 0, 1280, 720) if self.author.family == "standard" else (0, 0, 1920, 1080))
        return export_reading(self.author, self.options, "14k" if side == "dp" else "7k", side, self.sink, canvas)

    def test_standard_uses_fixed_clip_value_clock_and_leaves_other_information_owned_by_hud(self):
        result = self.export()
        self.assertEqual(len(result["text"]), 9)
        self.assertEqual(result["handled_root_ids"], [])
        self.assertEqual(result["handled_node_ids"], ["std.test.p1.cover-readout.version"])
        self.assertTrue(all("offsets" not in d for d in result["destination"]))
        for destination in result["destination"]:
            self.assertEqual([{k: key[k] for k in ("clip_x", "clip_y", "clip_w", "clip_h")} for key in destination["dst"]],
                             [{"clip_x": 0, "clip_y": 238, "clip_w": 1280, "clip_h": 482}] * 2)
            self.assertIn("main_state.option(270)", destination["draw"])
        self.assertIn("main_state.float_number(4)", result["destination"][0]["timer"])
        self.assertIn("main_state.number(315)", result["destination"][3]["timer"])
        self.assertIn("main_state.option(272)", result["destination"][3]["timer"])
        self.assertEqual(result["gaps"][1]["hiddenSourcePixelsExclusiveBound"], .968)

    def test_ownership_is_available_to_independent_stages_without_asset_writes(self):
        standard = reading_ownership(self.author, self.options, "7k", "p1")
        self.assertEqual(standard, {"handled_root_ids": [], "handled_node_ids": ["std.test.p1.cover-readout.version"]})
        self.assertEqual(list(self.sink.assets.iterdir()), [])
        self.lightning("dp")
        lightning = reading_ownership(self.author, self.options, "14k", "dp")
        self.assertEqual(lightning, {"handled_root_ids": ["gc.stage"], "handled_node_ids": []})
        self.assertEqual(self.sink.source, {})

    def test_reading_subtree_retains_its_real_information_ancestors(self):
        self.parts["frame"].scene["templates"][0]["root"]["properties"]["x"] = 1 / 1280
        result = self.export()
        self.assertEqual(result["destination"][0]["dst"][0]["x"], 130)
        self.assertEqual(result["destination"][0]["dst"][0]["clip_x"], 1)

    def test_source_padding_layout_is_compacted_without_changing_font_pixels(self):
        result = self.export()
        self.assertIn('%03d', result["text"][0]["value"])
        self.assertIn('string.rep(" ", 10 - #s)', result["text"][1]["value"])
        self.assertIn("1321 or 1323", result["text"][2]["value"])
        self.assertIn("1325 or 1327", result["text"][2]["value"])
        native_font = next(f for f in result["font"] if f["id"] == result["text"][1]["font"])
        descriptor = (self.sink.output / native_font["path"]).read_text()
        path = re.search(r'page id=0 file="([^"]+)"', descriptor)[1]
        glyphs = {}
        for line in descriptor.splitlines():
            if line.startswith("char id="):
                fields = dict(item.split("=", 1) for item in line.split()[1:])
                glyphs[int(fields["id"])] = {key: int(fields[key]) for key in ("x", "y", "width", "height", "xadvance")}
        with Image.open(self.sink.assets / path) as atlas:
            space = glyphs[32]
            self.assertEqual((space["width"], space["height"], space["xadvance"]), (12, 20, 12))
            self.assertIsNone(atlas.crop((space["x"], space["y"], space["x"] + space["width"], space["y"] + space["height"])).getbbox())
            zero = glyphs[48]
            with Image.open(self.source / "normal.0.png") as original:
                self.assertEqual(atlas.crop((zero["x"], zero["y"], zero["x"] + 12, zero["y"] + 20)).tobytes(), original.tobytes())

    def test_standard_dp_keeps_software_table_anchors_and_shared_native_cover_state(self):
        self.standard("dp")
        result = self.export("dp")
        self.assertEqual(len(result["text"]), 18)
        self.assertEqual([result["destination"][i]["dst"][0]["x"] for i in (0, 9)], [397, 767])
        self.assertEqual(result["destination"][0]["timer"], result["destination"][9]["timer"])

    def test_source_canvas_scaling_does_not_read_window_height_or_guess_config_resolution(self):
        result = self.export(canvas=(0, 0, 1920, 1080))
        self.assertEqual(result["text"][0]["size"], 30)
        self.assertEqual(result["destination"][0]["dst"][0]["clip_h"], 723)
        self.assertIn("* 726", result["destination"][0]["timer"])
        self.assertNotIn("screen_height", str(result))
        self.assertEqual(result["gaps"][1]["liftSourcePixelsExclusiveBound"], .726)

    def test_inset_cabinet_lane_uses_actual_native_edge_but_retains_software_clip(self):
        self.parts["frame"].manifest["layouts"][0]["stages"][0]["rect"] = [48 / 1280, 11 / 720, 292 / 1280, 473 / 720]
        result = self.export()
        sudden, hidden = result["destination"][0], result["destination"][3]
        self.assertEqual(sum(d["y"] for d in sudden["dst"]) / 2, 715)
        self.assertEqual(sum(d["y"] for d in hidden["dst"]) / 2, 216)
        self.assertIn("* 473", sudden["timer"])
        self.assertEqual(sudden["dst"][0]["clip_h"], 482)
        self.assertEqual(result["gaps"][0]["code"], "native-reading-cabinet-edge-adaptation")

    def test_lightning_preserves_original_labels_and_exact_native_edge_offsets(self):
        self.lightning()
        result = self.export()
        self.assertEqual((len(result["image"]), len(result["text"])), (6, 6))
        self.assertEqual([d["offsets"] for d in result["destination"]], [[4]] * 4 + [[3, 5]] * 4 + [[3]] * 4)
        self.assertEqual((result["image"][0]["w"], result["image"][0]["h"]), (92, 12))
        self.assertEqual((result["destination"][0]["dst"][0]["w"], result["destination"][0]["dst"][0]["h"]), (92, 12))
        self.assertTrue(all(t["size"] == 20 for t in result["text"]))
        self.assertEqual(result["handled_root_ids"], ["gc.stage"])

    def test_lightning_dp_has_independent_object_ids_and_lift_preference_is_respected(self):
        self.lightning("dp")
        self.options["lift-readout"] = False
        result = self.export("dp")
        self.assertEqual(len(result["destination"]), 16)
        self.assertEqual(len({d["id"] for d in result["destination"]}), 16)
        self.assertFalse(any(d["offsets"] == [3] for d in result["destination"]))

    def test_unqualified_source_edge_and_numeric_layout_fail_explicitly(self):
        scene = self.parts["frame"].scene
        track = next(t for t in scene["tracks"] if t["property"] == "x" and ".white.value" in t["target"])
        track["keyframes"][-1]["value"] = .1
        with self.assertRaisesRegex(ValueError, "glyph positions"):
            self.export()
        self.standard()
        track = next(t for t in self.parts["frame"].scene["tracks"] if t["property"] == "y")
        track["keyframes"][-1]["value"] = 0
        with self.assertRaisesRegex(ValueError, "lane height"):
            self.export()

    def test_unmapped_software_mode_is_not_assumed_to_be_normal(self):
        root = self.parts["frame"].scene["templates"][0]["root"]["children"][-1]
        self.parts["frame"].scene["bindings"].append({"id": "unknown", "target": root["id"], "property": "visible", "source": "scroll.mode"})
        with self.assertRaisesRegex(ValueError, "scroll.mode"):
            self.export()

    def test_suppressed_material_does_not_claim_or_copy_reading_art(self):
        frame = self.parts["frame"]
        self.parts["frame"] = Contribution(frame.module, frame.choice, None, None, None, frame.manifest, frame.scene, frame.ini.replace("resource Provide", "resource Suppress"))
        self.assertTrue(all(not value for value in self.export().values()))
        self.assertEqual(self.sink.source, {})


if __name__ == "__main__":
    unittest.main()
