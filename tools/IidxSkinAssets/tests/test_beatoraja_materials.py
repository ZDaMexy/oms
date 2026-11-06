"""Portable, synthetic tests; no private skin files or game installation needed."""
import copy
import hashlib
from pathlib import Path
import struct
import sys
import tempfile
from types import SimpleNamespace
import unittest
import zlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from beatoraja_author import Contribution
from beatoraja_materials import (_Images, _font_value,
                                collect_judge_materials, collect_note_materials)


def png(path, width, height):
    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", zlib.crc32(kind + payload))
    raw = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    raw += chunk(b"IDAT", zlib.compress((b"\0" + bytes(width * 4)) * height)) + chunk(b"IEND", b"")
    path.write_bytes(raw)


class MaterialTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-materials"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.parts = {name: self.contribution(name) for name in ("notes", "charge-notes")}
        self.author = SimpleNamespace(root=self.root, family="standard", contribution=lambda name, options: self.parts[name])
        self.copies = []

    def sink(self, relative):
        self.copies.append(relative)
        return "source-" + relative

    def contribution(self, module):
        resources, templates, instances, variants = [], [], [], []
        slots = ["object.note"] if module == "notes" else ["object.long-note." + s for s in ("head", "body", "tail")]
        for colour, width in (("white", 34), ("black", 26), ("scratch", 60)):
            for slot in slots:
                part = slot.rsplit(".", 1)[-1]
                prefix = module + "-" + colour + "-" + part
                states = ["normal"] if module == "notes" else ["cn", "cn-holding", "cn-released", "hcn", "hcn-holding", "hcn-released"]
                for state in states:
                    identifier = prefix + "-" + state
                    path = identifier + ".png"
                    png(self.root / path, width, 8 if module == "notes" else 16)
                    resources.append({"id": identifier, "type": "texture", "path": path})
                node = {"id": prefix, "type": "sprite", "resource": prefix + "-" + states[0],
                        "properties": {"anchor": "bottom-left", "origin": "bottom-left"}, "children": [], "effects": []}
                templates.append({"id": prefix + "-template", "root": node})
                instances.append({"template": prefix + "-template", "material": {"slot": slot, "resource": prefix + "-marker"}})
                if module != "notes":
                    source = "object.long-note-appearance" if part == "body" else "object.long-note-mode"
                    keys = states if part == "body" else ["cn", "hcn"]
                    cases = [{"key": key, "resource": prefix + "-" + key} for key in keys]
                    cases += [{"key": key.replace("cn", "ln", 1), "resource": prefix + "-" + key} for key in keys if key.startswith("cn")]
                    variants.append({"target": prefix, "property": "resource", "source": source, "default": prefix + "-cn", "cases": cases})
        ini = []
        for mode, presentation, groups in (("7k", "p1", 1), ("7k", "p2", 1), ("14k", "dp", 2)):
            for group in range(groups):
                for local in range(8):
                    scratch = local == (7 if group == 1 else 0)
                    key = local + 1 if group == 1 else local
                    colour = "scratch" if scratch else ("white" if key % 2 else "black")
                    logical = group * 8 + local
                    lane = "bms.lane.scratch-" + str(group + 1) if scratch else "bms.lane.key-" + str(logical)
                    visual = (7 if scratch else local - 1) if presentation == "p2" else local
                    target = f"Target: Lane ruleset=bms keymode={mode} group-logical={group} group-local-logical={local} global-logical={logical} global-visual={group * 8 + visual} lane={lane}"
                    if mode == "7k":
                        target += " presentation=" + presentation
                    ini += ["[GameplaySkin.Common:1]", target]
                    ini += [f'{slot}: resource Provide "{module}-{colour}-{slot.rsplit(".", 1)[-1]}-marker"' for slot in slots]
        return Contribution(module, "test", "manifest.json", "scene.json", "materials.ini",
                            {"resources": resources}, {"templates": templates, "instances": instances, "variants": variants}, "\n".join(ini))

    def collect(self, keymode="7k", presentation="p1"):
        return collect_note_materials(self.author, {}, keymode, presentation, self.sink)

    def test_sp2_and_dp_use_native_key_then_scratch_order(self):
        sp = self.collect("7k", "p2")
        dp = self.collect("14k", "dp")
        self.assertEqual([int(g["source_target"]["global-logical"]) for g in sp["geometry"]], [1, 2, 3, 4, 5, 6, 7, 0])
        self.assertEqual([int(g["source_target"]["global-logical"]) for g in dp["geometry"]], [1, 2, 3, 4, 5, 6, 7, 0, 8, 9, 10, 11, 12, 13, 14, 15])
        self.assertTrue(all(len(v) == 16 for k, v in dp["note"].items() if k != "id"))

    def test_hcn_reactive_is_held_and_miss_is_released_actual_art(self):
        recipe = self.collect()
        note = recipe["note"]
        self.assertEqual(note["hcnbodyReactive"], note["hcnbodyActive"])
        self.assertNotEqual(note["hcnbodyMiss"], note["hcnbodyActive"])
        self.assertTrue(recipe["geometry"][0]["slots"]["hcnbodyMiss"]["resource"].endswith("hcn-released"))
        self.assertNotEqual(recipe["extra_states"]["ln_cn_released"], note["lnbody"])

    def test_original_sizes_and_bytes_survive_recipe_collection(self):
        before = {p.name: hashlib.sha256(p.read_bytes()).digest() for p in self.root.glob("*.png")}
        recipe = self.collect()
        after = {p.name: hashlib.sha256(p.read_bytes()).digest() for p in self.root.glob("*.png")}
        self.assertEqual(before, after)
        slots = recipe["geometry"][0]["slots"]
        self.assertEqual(slots["note"]["source_size"], [34, 8])
        self.assertEqual(slots["lnstart"]["source_size"], [34, 16])
        self.assertIn("shared-note-height-needs-padding", [g["code"] for g in recipe["gaps"]])
        self.assertEqual(len(self.copies), len(set(self.copies)))

    def test_conflicting_ln_cn_art_is_rejected(self):
        c = self.parts["charge-notes"]
        variant = next(v for v in c.scene["variants"] if v["target"] == "charge-notes-white-head")
        next(case for case in variant["cases"] if case["key"] == "ln")["resource"] = "charge-notes-white-head-hcn"
        with self.assertRaisesRegex(ValueError, "cannot share"):
            self.collect()

    def test_real_assist_branch_container_is_resolved_without_using_hidden_art(self):
        c = self.parts["charge-notes"]
        template = next(t for t in c.scene["templates"] if t["root"]["id"] == "charge-notes-scratch-head")
        original = template["root"]
        other = {**copy.deepcopy(original), "id": "assist-only", "resource": "missing-hidden-art"}
        template["root"] = {"id": "assist-wrapper", "type": "container", "properties": {"width": 1, "height": 1}, "children": [original, other]}
        c.scene["variants"] += [
            {"target": original["id"], "property": "visible", "source": "object.auto-assist", "default": True, "cases": [{"key": "scratch", "value": False}]},
            {"target": other["id"], "property": "visible", "source": "object.auto-assist", "default": False, "cases": [{"key": "scratch", "value": True}]},
        ]
        recipe = self.collect()
        self.assertEqual(recipe["geometry"][7]["slots"]["lnstart"]["node"], original["id"])

    def test_fractional_font_spacing_is_retained_without_rounding_native_space(self):
        png(self.root / "digits.png", 20, 3)
        c = Contribution("judgement", "test", None, None, None, {"resources": [{"id": "digits", "type": "texture", "path": "digits.png"}]}, {}, "")
        font = {"id": "fractional", "lineHeight": 3, "glyphs": [{"character": str(i), "resource": "digits", "rect": [i * 2, 0, 2, 3], "advance": 1.25} for i in range(10)]}
        value, metadata = _font_value(c, font, _Images(self.author, self.sink))
        self.assertIsNone(value)
        self.assertEqual(metadata["advance"], 1.25)
        self.assertEqual(metadata["source_space"], -0.75)
        self.assertFalse(metadata["native_spacing_ready"])

    def test_label_resource_tracks_retain_all_same_source_palette_dependencies(self):
        resources = []
        for name in ("blue", "green", "red"):
            png(self.root / (name + ".png"), 20, 3)
            resources.append({"id": name, "type": "texture", "path": name + ".png"})
        label = {"id": "label", "type": "sprite", "resource": "blue", "properties": {"height-px": 3}, "children": []}
        row = {"id": "row", "type": "container", "properties": {"layout": "horizontal"}, "children": [label]}
        scene = {"templates": [{"id": "judge", "root": row}],
                 "instances": [{"template": "judge", "material": {"slot": "hud.judgement", "resource": "judge-marker"}}],
                 "tracks": [{"id": "palette", "target": "label", "property": "resource", "easing": "step", "loop": True,
                             "trigger": "judgement.hit", "keyframes": [{"time": i * 100, "value": name} for i, name in enumerate(("blue", "green", "red", "blue"))]},
                            {"id": "inactive-palette", "target": "inactive-label", "property": "resource", "keyframes": [{"time": 0, "value": "not-an-active-resource"}]}]}
        ini = '[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode=7k presentation=p1 group-logical=0\nhud.judgement: resource Provide "judge-marker"'
        self.parts["judgement"] = Contribution("judgement", "test", "manifest.json", "scene.json", "materials.ini", {"resources": resources}, scene, ini)
        recipe = collect_judge_materials(self.author, {"judgement-display": "label-only"}, "7k", "p1", self.sink)
        self.assertEqual(set(recipe["resource_images"]), {"blue", "green", "red"})
        self.assertEqual(len(recipe["images"]), 3)
        self.assertEqual(set(self.copies), {"blue.png", "green.png", "red.png"})
        self.assertTrue(all(len(grade["alternatives"]) == 1 for grade in recipe["results"]))
        self.assertTrue(all(grade["alternatives"][0]["font"] is None for grade in recipe["results"]))


if __name__ == "__main__":
    unittest.main()
