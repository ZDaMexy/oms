"""Regression checks for independent Standard author contribution contracts."""
import copy
import hashlib
import math
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from standard_components import (FRAME_IDS, StandardBranch, StandardComponents, judgement_fit_steps,
                                 terminal_repeat_control, turntable_motion)


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


def judgement_owner():
    owner = memory_owner()
    owner.packages = {f"judge{i:02}": {} for i in range(14)}
    owner.options, owner.receipts, owner.contexts = [], [], []
    owner.preview = lambda *args: "preview.png"
    owner.fallback_marker = lambda *args: "marker"
    owner.asset = lambda category, package, name: ({"id": category + "." + name, "path": "source.png"}, {})
    owner.index = {(f"judge{i:02}", name): {"width": 164 if i == 11 else width, "height": 74}
                   for i in range(14) for name, width in (("judge_p_great_blue", 170), ("judge_p_great_white", 170),
                       ("judge_p_great_pink", 170), ("judge_great_yellow", 170), ("judge_good", 141),
                       ("judge_bad", 112), ("judge_poor", 141))}
    owner.judgement_atlas = lambda package: (
        {"id": "atlas." + package, "type": "texture", "path": "atlas.png"},
        {(palette, digit): [digit * 55, 0, 55, 74] for palette in "bgry" for digit in range(10)}, 74)
    return owner


def charge_owner():
    owner = memory_owner()
    owner.options, owner.receipts, owner.contexts, owner.calls = [], [], [], []
    owner.preview = lambda *args: "preview.png"
    owner.fallback_marker = lambda category, name, *args: "markers/" + category + "/" + name
    owner.index = {}
    for colour, width in (("white", 34), ("black", 26), ("scratch", 60)):
        for stem in (("notes_bss", "notes_bss2") if colour == "scratch" else ("notes_cn_" + colour, "notes_cn2_" + colour)):
            for suffix in ("start", "end", "beam0", "beam1", "beam2", "beam4"):
                owner.index["notes_cn00", stem + "_" + suffix] = {"width": width, "height": 8}
    owner.asset = lambda category, package, name: (
        {"id": category + "." + name.replace("_", "-"), "path": "source/" + name + ".png"}, owner.index[package, name])

    def clip(package, movie, prefix, *, loop, trigger):
        owner.calls.append((movie, loop, trigger))
        width, height = (300, 300) if "2_push" in movie else (200, 200) if movie == "notes_break" else (125, 105) if "push" in movie else (96, 100)
        return {"node": {"id": prefix + ".art", "type": "recorded-animation", "animation": prefix + ".clip", "children": []},
                "resources": [], "animations": [{"id": prefix + ".clip", "path": "clips/" + movie + ".json.br", "resources": []}],
                "files": [], "source": {"stage": [0, 0, width, height], "durationMs": 400}}
    owner.clip = clip
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
    def atlas_fixture(self, directory):
        from PIL import Image

        resources, files, pixels = {}, {}, {}
        for index, size in enumerate(((3, 2), (2, 4))):
            identifier = f"original.texture.{index}"
            path = directory / f"source-{index}.png"
            image = Image.new("RGBA", size)
            image.putdata([(index * 101 + x * 17, y * 41, x + y, 0 if x == y else 127 + x * 31)
                           for y in range(size[1]) for x in range(size[0])])
            image.save(path)
            resources[identifier] = {"id": identifier, "type": "texture", "path": path.name, "filter": "nearest"}
            files[path.name] = {"source": str(path), "path": path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
            pixels[identifier] = image.copy()
        clip = {"contract": "synthetic-geometry-test", "fps": 60, "loop": False, "trigger": "gameplay.complete",
                "geometries": [{"resource": identifier, "colour": [1, .5, .25, .75],
                                "vertices": [[-3.5, 11, 2, 0, 0], [12, 8, 4, 1, 1], [-2, 7, -1, -.25, 1.125]],
                                "triangles": [0, 1, 2]} for identifier in resources]
                              + [{"resource": None, "vertices": [], "triangles": [], "colour": [0, 1, 0, .5]}],
                "nodes": [{"parent": -1, "geometry": 0, "blend": "add"}],
                "states": [{"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 25, 50, 0], "mask": [0, 0, 3, 2]}],
                "frames": [{"nodes": [[0, 0]], "camera": [8, 9, 2]}, {"nodes": [], "camera": None}]}
        return resources, files, pixels, clip

    def test_original_movie_atlas_preserves_rgba_edges_xyz_and_all_frame_state(self):
        from PIL import Image

        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            resources, files, pixels, clip = self.atlas_fixture(directory)
            original = copy.deepcopy(clip)
            packed, images, receipt = memory_owner().original_pixel_atlas(clip, resources, files, "test.movie", directory)
            self.assertEqual(1, len(packed))
            self.assertEqual((2, 1), (receipt["sourceResources"], receipt["atlasResources"]))
            self.assertTrue(receipt["allXYZTopologyStatesFramesAndMetadataUnchanged"])
            self.assertLessEqual(receipt["maximumInverseUVError"], 1e-12)
            for field in ("fps", "loop", "trigger", "nodes", "states", "frames"):
                self.assertEqual(original[field], clip[field])
            for old, new in zip(original["geometries"], clip["geometries"]):
                self.assertEqual(old["triangles"], new["triangles"])
                self.assertEqual([v[:3] for v in old["vertices"]], [v[:3] for v in new["vertices"]])
            for page in receipt["pages"]:
                with Image.open(images[page["path"]]["source"]) as atlas:
                    for source in page["sources"]:
                        left, top, width, height = source["rect"]
                        image = pixels[source["originalResource"]]
                        self.assertEqual(image.tobytes(), atlas.crop((left, top, left + width, top + height)).tobytes())
                        for y in range(-2, height + 2):
                            for x in range(-2, width + 2):
                                self.assertEqual(image.getpixel((max(0, min(width - 1, x)), max(0, min(height - 1, y)))),
                                                 atlas.getpixel((left + x, top + y)))

    def test_original_movie_atlas_refuses_outside_uv_and_non_nearest_sources(self):
        for bad_sampler in (False, True):
            with tempfile.TemporaryDirectory() as temporary:
                directory = Path(temporary)
                resources, files, _, clip = self.atlas_fixture(directory)
                if bad_sampler:
                    next(iter(resources.values()))["filter"] = "linear"
                else:
                    clip["geometries"][0]["vertices"][0][3] = -1
                with self.assertRaises(ValueError):
                    memory_owner().original_pixel_atlas(clip, resources, files, "test.movie", directory)

    def test_original_movie_atlas_rejects_an_accidental_non_uv_edit(self):
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            resources, files, _, clip = self.atlas_fixture(directory)
            owner = memory_owner()
            packing = owner.pack_large_lane_clip

            def damage(*args):
                result = packing(*args)
                args[0]["states"][0]["matrix"][9] += 1
                return result

            owner.pack_large_lane_clip = damage
            with self.assertRaisesRegex(ValueError, "XYZ, topology, state or frame"):
                owner.original_pixel_atlas(clip, resources, files, "test.movie", directory)

    def test_long_note_effects_follow_the_ten_native_callers_and_real_owner_clocks(self):
        owner = charge_owner()
        owner.charge_notes()
        self.assertEqual(10, len({movie for movie, _, _ in owner.calls}))
        for movie, loop, trigger in owner.calls:
            if movie == "notes_break" or movie.endswith("_push"):
                self.assertTrue(loop)
                self.assertEqual("object.state", trigger)
            else:
                self.assertFalse(loop)
                self.assertFalse(movie.endswith("2"), "The source base2 movies are not dispatched by26")
                self.assertEqual("object.long-note-transition", trigger)
        choice = owner.modules[0]["choices"][0]
        self.assertEqual([{"hcn-display": "cn", "auto-scratch-display": True},
                          {"hcn-display": "hcn", "auto-scratch-display": False},
                          {"hcn-display": "cn", "auto-scratch-display": False}], [v["when"] for v in choice["variants"]])
        scenes = [owner.documents[item["scene"]] for item in [choice, *choice["variants"]]]
        for scene in scenes:
            effects = [t for t in scene["templates"] if t["root"].get("slot") == "effect.long-note"]
            self.assertEqual(6, len(effects))
            for template in effects:
                nodes = {n["id"]: n for n in self.walk(template["root"])}
                wrappers = [n for n in template["root"]["children"]]
                self.assertEqual(4, len(wrappers))
                base = next(n for n in wrappers if n["id"].endswith(".base"))
                scales = [b for b in scene["bindings"] if b["target"] in nodes and b["source"] == "option.explosion-scale"]
                self.assertEqual({base["id"]}, {b["target"] for b in scales})
                release = next(n for n in wrappers if n["id"].endswith(".break"))
                gate = next(v for v in scene["variants"] if v["target"] == release["id"])
                self.assertEqual(["hcn-released"], [c["key"] for c in gate["cases"]])
        animations = [owner.documents[item["manifest"]]["animations"] for item in [choice, *choice["variants"]]]
        self.assertTrue(all(item == animations[0] for item in animations), "Display options never change the actual HCN effects")

    def test_auto_scratch_display_changes_only_exact_assisted_note_visibility(self):
        charge = charge_owner()
        charge.charge_notes()
        notes = charge_owner()
        notes.packages = {f"notes{i:02}": {} for i in range(12)}
        for package in notes.packages:
            for colour, width in (("white", 34), ("black", 26), ("turn", 60)):
                notes.index[package, "notes_" + colour] = {"width": width, "height": 16}
        notes.notes()
        for owner, count in ((notes, 6), (charge, 18)):
            option = next(o for o in owner.options if o["id"] == "auto-scratch-display")
            self.assertEqual(("toggle", True), (option["type"], option["default"]))
            for choice in owner.modules[0]["choices"]:
                for contribution in (choice, *choice["variants"]):
                    scene = owner.documents[contribution["scene"]]
                    gates = [v for v in scene["variants"] if v["source"] == "object.auto-assist"]
                    hidden = contribution.get("when", {}).get("auto-scratch-display") is False
                    self.assertEqual(count if hidden else 0, len(gates))
                    targets = {t["root"]["id"]: t["root"]["slot"] for t in scene["templates"]}
                    for gate in gates:
                        self.assertTrue(targets[gate["target"]].startswith("object."))
                        self.assertTrue(gate["default"], "Ordinary notes and full Autoplay retain their original art")
                        self.assertEqual([("scratch", False)], [(c["key"], c["value"]) for c in gate["cases"]])

    def test_hcn_display_type_uses_original_cn_body_states_without_recolouring(self):
        owner = charge_owner()
        owner.charge_notes()
        choice = owner.modules[0]["choices"][0]
        for contribution, expected in ((choice, "notes-cn2-white-beam4"), (choice["variants"][0], "notes-cn-white-beam2")):
            scene = owner.documents[contribution["scene"]]
            variants = [v for v in scene["variants"] if ".white.body." in v["target"]]
            self.assertEqual(2, len(variants))
            for variant in variants:
                case = next(c for c in variant["cases"] if c["key"] == "hcn-released")
                self.assertTrue(case["resource"].endswith(expected))

    def test_original_judgement_positions_select_one_text_scene_and_share_lift(self):
        owner = judgement_owner()
        owner.judgements()
        option = next(o for o in owner.options if o["id"] == "judgement-position")
        self.assertEqual(["normal", "up100", "up200", "up300", "off"], [c["value"] for c in option["choices"]])
        for choice in owner.modules[0]["choices"]:
            normal = owner.documents[choice["scene"]]
            normal_root = normal["templates"][0]["root"]
            self.assertAlmostEqual((342 - 484 * .81) / (484 * .12), normal_root["properties"]["y"])
            for variant, offset in zip(choice["variants"], (100, 200, 300, 0)):
                scene = owner.documents[variant["scene"]]
                root = scene["templates"][0]["root"]
                self.assertEqual(normal_root["properties"]["y"], root["properties"]["y"])
                self.assertAlmostEqual(-offset / (484 * .12 * 3), root["children"][0]["children"][0]["properties"]["y"])
                self.assertEqual(len(list(self.walk(normal_root))), len(list(self.walk(root))))
                self.assertEqual(normal["bindings"], scene["bindings"])
                self.assertEqual(normal["tracks"], scene["tracks"])
                if variant["when"]["judgement-position"] == "off":
                    hidden = root["children"][0]
                    self.assertFalse(hidden["properties"]["visible"])
                    # A static OFF ancestor must survive every live opacity/visibility writer.
                    live_targets = {entry["target"] for field in ("tracks", "bindings", "variants")
                                    for entry in scene[field] if entry["property"] in ("opacity", "visible")}
                    live_targets.update(assignment["target"] for machine in scene["stateMachines"]
                                        for state in machine["states"] for assignment in state.get("set", [])
                                        if assignment["property"] in ("opacity", "visible"))
                    self.assertNotIn(hidden["id"], live_targets)
                    self.assertIn(root["id"], live_targets, "The real judgement lifecycle still runs on its own parent")
            lift = next(t for t in normal["tracks"] if t.get("clock") == "lift.offset")
            self.assertEqual("y", lift["property"])
            self.assertAlmostEqual(-1 / (.12 * 3), lift["keyframes"][-1]["value"])

    def test_explosion_size_has_only_the_seven_native_scales(self):
        owner = memory_owner()
        owner.options, owner.receipts, owner.contexts = [], [], []
        owner.packages = {f"exp{i:02}": {} for i in range(20)}
        owner.index = {(package, "art"): {"width": 96, "height": 100} for package in owner.packages}
        owner.preview = lambda *args: "preview.png"
        owner.fallback_marker = lambda *args: "marker"
        owner.clip = lambda *args, **kwargs: {
            "node": {"id": "source", "type": "container", "children": []},
            "files": [], "resources": [], "animations": [], "source": {"stage": [0, 0, 96, 100]}}
        owner.explosions()
        option = next(o for o in owner.options if o["id"] == "explosion-scale")
        self.assertEqual(1, option["default"])
        scales = [round(option["min"] + step * option["step"], 1) for step in range(7)]
        self.assertEqual([.7, .8, .9, 1, 1.1, 1.2, 1.3], scales)
        self.assertEqual(scales[-1], option["max"])

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
                roots = {n["id"]: n for t in scene["templates"] for n in self.walk(t["root"])}
                self.assertEqual(-40, roots[eligibility["target"]]["properties"]["z"])
                self.assertEqual({"time": 2500, "value": 0}, {k: retention["keyframes"][-1][k] for k in ("time", "value")})

    def test_judgement_grade_gate_does_not_overwrite_palette_phase_opacity(self):
        owner = judgement_owner()
        owner.judgements()
        for choice in owner.modules[0]["choices"]:
            scene = owner.documents[choice["scene"]]
            opacity_targets = {t["target"] for t in scene["tracks"] if t["property"] == "opacity"}
            visible_targets = {v["target"] for v in scene["variants"] if v["property"] == "visible"}
            self.assertEqual(3, len(opacity_targets))
            self.assertFalse(opacity_targets & visible_targets)

    def test_combo_rows_fit_three_four_five_and_ten_digits_without_shrinking_short_rows(self):
        def evaluate(steps, value):
            return next(scale for threshold, scale in reversed(steps) if value >= threshold)

        steps = judgement_fit_steps(170, 55)
        for value in (0, 1, 9, 10, 99, 100, 999):
            self.assertEqual(1, evaluate(steps, value))
        for value, width in ((1000, 325), (9999, 325), (10000, 362), (99999, 362),
                             (1_000_000_000, 547), (2_147_483_647, 547)):
            self.assertAlmostEqual(292 / width, evaluate(steps, value))
        for label_width in (112, 141, 164, 170):
            steps = judgement_fit_steps(label_width, 55)
            self.assertTrue(all(time <= 86_400_000 for time, _ in steps))
            for digits in range(1, 11):
                width = label_width - 11 + 55 + 37 * (digits - 1)
                scale = evaluate(steps, 10 ** (digits - 1))
                self.assertLessEqual(width * scale, 292 + 1e-10)
                if width <= 292:
                    self.assertEqual(1, scale)
                elif digits <= 7:
                    self.assertAlmostEqual(292, width * scale)
        self.assertEqual(1, evaluate(judgement_fit_steps(112, 55), 999), "BAD with three digits already fits")
        self.assertAlmostEqual(292 / 296, evaluate(judgement_fit_steps(141, 55), 9999))

    def test_judgement_fit_is_on_the_top_centred_whole_row_and_inactive_combo_has_no_digit_cell(self):
        owner = judgement_owner()
        owner.judgements()
        for choice in owner.modules[0]["choices"]:
            scene = owner.documents[choice["scene"]]
            nodes = {n["id"]: n for t in scene["templates"] for n in self.walk(t["root"])}
            combo_bindings = [b for b in scene["bindings"] if b["source"] == "combo.value"]
            self.assertEqual(5, len(combo_bindings))
            for binding in combo_bindings:
                pair_id = binding["target"].removesuffix(".combo")
                pair = nodes[pair_id]
                self.assertEqual(["sprite", "text"], [c["type"] for c in pair["children"]])
                self.assertEqual((.5, .5, "top-centre"), tuple(pair["properties"][p] for p in ("x", "y", "origin")))
                self.assertAlmostEqual(74 / (3 * 484 * .12), pair["properties"]["height"])
                self.assertNotIn("height-px", pair["properties"])
                self.assertTrue(any(b["target"] == pair_id and b["property"] == "visible" and b["source"] == "combo.active"
                                    for b in scene["bindings"]))
                fit = [t for t in scene["tracks"] if t["target"] == pair_id]
                self.assertEqual({"scale-x", "scale-y"}, {t["property"] for t in fit})
                self.assertEqual([[k["value"] for k in t["keyframes"]] for t in fit][0],
                                 [[k["value"] for k in t["keyframes"]] for t in fit][1])
                self.assertTrue(all(t["clock"] == "combo.value" and t["easing"] == "step" and not t["loop"] for t in fit))
                inactive = nodes[pair_id + ".no-combo"]
                self.assertEqual(["sprite"], [c["type"] for c in inactive["children"]])
                self.assertEqual("top-centre", inactive["properties"]["origin"])
                self.assertEqual(pair["properties"]["height"], inactive["properties"]["height"])
                self.assertFalse(any(t["target"] == inactive["id"] for t in scene["tracks"]))
                gate = next(v for v in scene["variants"] if v["target"] == inactive["id"])
                self.assertEqual("combo.active", gate["source"])
                self.assertFalse(gate["default"])
                self.assertEqual([("false", True)], [(c["key"], c["value"]) for c in gate["cases"]])

    def test_judgement_canvas_top_stays_at_native_342_through_fit_position_and_lift(self):
        owner = judgement_owner()
        owner.judgements()
        field_height, band_height, row_height = 484, 484 * .12, 74
        for choice in owner.modules[0]["choices"]:
            contributions = [(choice, 0), *zip(choice["variants"][:3], (100, 200, 300))]
            for contribution, offset in contributions:
                scene = owner.documents[contribution["scene"]]
                root = scene["templates"][0]["root"]
                canvas = root["children"][0]
                position = canvas["children"][0]
                canvas_height = canvas["properties"]["height"] * band_height
                nodes = {n["id"]: n for n in self.walk(root)}
                rows = [n for n in nodes.values() if n.get("properties", {}).get("layout") == "horizontal"]
                lift_track = next(t for t in scene["tracks"] if t.get("clock") == "lift.offset")
                for lift in (0, .25, .75):
                    parent_top = field_height * .75 + root["properties"]["y"] * band_height
                    parent_top += canvas["properties"]["y"] * band_height
                    parent_top += position["properties"].get("y", 0) * canvas_height
                    parent_top += lift * lift_track["keyframes"][-1]["value"] * canvas_height
                    for row in rows:
                        scale_track = next((t for t in scene["tracks"] if t["target"] == row["id"] and t["property"] == "scale-y"), None)
                        scales = [k["value"] for k in scale_track["keyframes"]] if scale_track else [1]
                        for scale in scales:
                            origin_y = {"top-centre": 0, "centre": .5}[row["properties"]["origin"]]
                            carrier_height = row["properties"].get("height", 1) * canvas_height
                            # GameplaySkinHorizontalContainer.RegisterCell anchors each leaf
                            # at the carrier centre, then LayoutCells subtracts half leaf height.
                            # A carrier height-px would be ignored by applyNodeProperties.
                            top = parent_top + row["properties"]["y"] * canvas_height
                            top += ((.5 - origin_y) * carrier_height - row_height / 2) * scale
                            self.assertAlmostEqual(342 - offset - lift * field_height, top)

        # Synthetic source-padded ink, using the verified00 vertical span, reproduces
        # the visible3/4/5-digit regression without embedding any source image.
        # The original FAST-A20px canvas starts at327. Centring the74px judgement
        # at342 incorrectly puts its painted core through that caption.
        steps = judgement_fit_steps(170, 55)
        for count in (108, 1024, 10000):
            scale = next(value for threshold, value in reversed(steps) if count >= threshold)
            painted_top = 342 + 9 * scale
            self.assertGreater(painted_top, 327 + 20)
            wrong_centred_top = 342 + (9 - row_height / 2) * scale
            wrong_centred_bottom = 342 + (65 - row_height / 2) * scale
            self.assertLess(wrong_centred_top, 327 + 20)
            self.assertGreater(wrong_centred_bottom, 327)

    def test_native_digit_advance_and_label_gap_keep_full_glyph_overhang(self):
        owner = judgement_owner()
        owner.judgements()
        for choice in owner.modules[0]["choices"]:
            scene = owner.documents[choice["scene"]]
            manifest = owner.documents[choice["manifest"]]
            for font in manifest["bitmapFonts"]:
                self.assertEqual({37}, {g["advance"] for g in font["glyphs"]})
                self.assertEqual({55}, {g["rect"][2] for g in font["glyphs"]})
            nodes = {n["id"]: n for t in scene["templates"] for n in self.walk(t["root"])}
            for suffix, native_width in (("b", 170), ("y-great", 170), ("y-good", 141)):
                row = nodes[f"sj.{choice['value']}.base.group.{suffix}"]
                image_width = 164 if choice["value"] == "j11" else native_width
                gap = row["properties"]["gap-px"] / (768 / 720)
                self.assertAlmostEqual(native_width - 11, image_width + gap)
                for count in (1, 3, 4, 5, 10):
                    # Match the public bitmap-font MeasureText and horizontal LayoutCells:
                    # full55px images at37px advance, with the last18px overhang retained.
                    text_width = 37 * (count - 1) + 55
                    measured = max(image_width, image_width + gap + text_width)
                    steps = judgement_fit_steps(image_width, 55, gap=gap)
                    value = 10 ** (count - 1)
                    fit = next(scale for threshold, scale in reversed(steps) if value >= threshold)
                    self.assertLessEqual(measured * fit, 292 + 1e-10)
                    if count <= 7 and measured > 292:
                        self.assertAlmostEqual(292, measured * fit)

    def test_bad_and_poor_never_draw_combo_even_when_a_ruleset_retains_it(self):
        owner = judgement_owner()
        owner.judgements()
        for choice in owner.modules[0]["choices"]:
            for contribution in [choice, *choice["variants"]]:
                scene = owner.documents[contribution["scene"]]
                nodes = {n["id"]: n for t in scene["templates"] for n in self.walk(t["root"])}
                for suffix, grades in (("y-bad", ["meh"]), ("y-poor", ["miss", "ok"])):
                    prefix = f"sj.{choice['value']}.base.group.{suffix}"
                    grade = next(v for v in scene["variants"] if v["target"] == prefix + ".grade")
                    self.assertEqual("judgement.result", grade["source"])
                    self.assertEqual(grades, [c["key"] for c in grade["cases"]])
                    children = nodes[prefix + ".palette"]["children"]
                    self.assertEqual([prefix + ".no-combo"], [n["id"] for n in children])
                    label_only = children[0]
                    self.assertEqual(["sprite"], [n["type"] for n in label_only["children"]])
                    self.assertEqual("top-centre", label_only["properties"]["origin"])
                    self.assertAlmostEqual(74 / (3 * 484 * .12), label_only["properties"]["height"])
                    self.assertNotIn(prefix + ".combo", nodes)
                    for field in ("bindings", "variants", "tracks"):
                        # No retained/nonzero combo can hide this label or add a number.
                        references = [entry for entry in scene[field] if entry["target"].startswith(prefix)]
                        self.assertFalse(any(entry.get("source", entry.get("clock", "")).startswith("combo.")
                                             for entry in references))

    def test_turntable_export_keeps_the_source_quad_and_uses_logical_motion_groups(self):
        owner = memory_owner()
        owner.frame_maps = Path("mapping.json")
        owner.frame_ids = FRAME_IDS
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
