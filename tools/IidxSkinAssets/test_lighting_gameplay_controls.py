"""Behavioral author tests with synthetic geometry and scores, no HDD or player data."""
from __future__ import annotations

import copy
import io
import struct
import unittest

from lighting_percentage_target import inject as add_percentage
from test_lighting_percentage_target import fixture
import lighting_gameplay_controls as controls
from build_lighting_gameplay_controls import audit_readout_scopes, readout_ini


def nodes(scene):
    return [node for root in (scene["root"], *(t["root"] for t in scene["templates"])) for node in controls.walk(root)]


def graph_fixture():
    scene, manifest = fixture()
    scene, _ = add_percentage(scene, manifest)
    return scene


def visible_text_sources(scene, values):
    variants = {item["target"]: item for item in scene["variants"] if item["property"] == "visible"}
    bindings = {(item["target"], item["property"]): item["source"] for item in scene["bindings"]}
    visible = []
    def visit(node, parent_visible=True):
        shown = node["properties"].get("visible", True)
        if node["id"] in variants:
            variant = variants[node["id"]]
            key = str(values.get(variant["source"], "")).lower()
            shown = next((case["value"] for case in variant["cases"] if case["key"] == key), variant["default"])
        if (node["id"], "visible") in bindings:
            shown = values[bindings[node["id"], "visible"]]
        if shown and parent_visible:
            if node["type"] == "text":
                visible.append((node["properties"].get("text"), bindings.get((node["id"], "text"))))
            for child in node.get("children", []):
                visit(child)
    for root in (scene["root"], *(t["root"] for t in scene["templates"])):
        visit(root)
    return visible


class GameplayGraphTest(unittest.TestCase):
    def shared_fixture(self):
        graphs = {}
        for layout in controls.LAYOUTS:
            scene, manifest = fixture(layout)
            scene, _ = add_percentage(scene, manifest)
            roots = scene["root"]["children"]
            scene["root"]["children"] = []
            for index, root in enumerate(roots):
                scene["templates"].append({"id": "template" + str(index), "root": root})
                scene["instances"].append({"id": "instance" + str(index), "template": "template" + str(index),
                    "material": {"slot": "decoration", "resource": "markers/frame-" + layout + "-information"}})
            graphs[layout] = scene
        return graphs

    def test_primary_wins_fallback_only_for_missing_primary_and_no_data_remains_visible(self):
        original = graph_fixture()
        saved = copy.deepcopy(original)
        result = controls.add_sub_target(original)
        sources = {"maximum": "score.maximum", "personal-best": "score.personal-best", "aaa": "score.rank-aaa",
                   "aa": "score.rank-aa", "a": "score.rank-a", "percentage": "option.pacemaker-percent"}
        for primary in controls.TARGETS:
            for fallback in ("none", *controls.TARGETS):
                for personal_best in (False, True):
                    for rank in (False, True):
                        for maximum in (False, True):
                            values = {"option.graph-target": primary, "option.graph-sub-target": fallback,
                                      "option.graph-visible": True, "score.maximum-available": maximum,
                                      "score.personal-best-available": personal_best, "score.rank-available": rank,
                                      "layout.ruleset": "bms"}
                            def available(target):
                                return maximum and (personal_best if target == "personal-best" else rank
                                    if target in ("aaa", "aa", "a") else target != "none")
                            expected = primary if available(primary) else fallback if available(fallback) else None
                            actual = visible_text_sources(result, values)
                            with self.subTest(primary=primary, fallback=fallback, pb=personal_best, rank=rank, maximum=maximum):
                                self.assertEqual([source for _, source in actual if source in sources.values()],
                                                 [] if expected is None else [sources[expected]])
                                self.assertEqual(sum(text == "NO DATA" for text, _ in actual), int(expected is None))
        self.assertEqual(original, saved)

    def test_graph_off_hides_primary_fallback_and_no_data(self):
        result = controls.add_sub_target(graph_fixture())
        values = {"option.graph-target": "personal-best", "option.graph-sub-target": "aaa", "option.graph-visible": False,
                  "score.maximum-available": True, "score.personal-best-available": False, "score.rank-available": False,
                  "layout.ruleset": "bms"}
        self.assertEqual(visible_text_sources(result, values), [])

    def test_sub_percentage_prefixes_only_its_label_and_keeps_the_percent_suffix(self):
        result = controls.add_sub_target(graph_fixture())
        values = {"option.graph-target": "personal-best", "option.graph-sub-target": "percentage", "option.graph-visible": True,
                  "score.maximum-available": True, "score.personal-best-available": False, "score.rank-available": False,
                  "layout.ruleset": "bms"}
        visible = visible_text_sources(result, values)
        text = [item for item, source in visible if source is None]
        self.assertIn("SUB PACEMAKER", text)
        self.assertIn("%", text)
        self.assertNotIn("SUB %", text)

    def test_options_keep_existing_values_and_default_and_reject_duplicate(self):
        original = {"options": [{"id": "graph-target", "type": "choice", "default": "maximum",
                                "choices": [{"value": item, "label": item} for item in controls.TARGETS]}]}
        saved = copy.deepcopy(original)
        result = controls.add_sub_target_option(original)
        self.assertEqual(original, saved)
        self.assertEqual(result["options"][0], original["options"][0])
        self.assertEqual(result["options"][1]["default"], "none")
        self.assertEqual([item["value"] for item in result["options"][1]["choices"]], ["none", *controls.TARGETS])
        with self.assertRaisesRegex(ValueError, "already exists"):
            controls.add_sub_target_option(result)

    def test_missing_availability_contract_rejects(self):
        original = graph_fixture()
        original["bindings"] = [item for item in original["bindings"] if item["source"] != "score.maximum-available"]
        with self.assertRaisesRegex(ValueError, "maximum availability"):
            controls.add_sub_target(original)

    def test_fallback_has_no_third_bar_or_fabricated_score_source(self):
        result = controls.add_sub_target(graph_fixture())
        self.assertFalse(any(any(term in item["source"] for term in ("rival", "national", "history", "online"))
                             for item in result["bindings"]))
        self.assertFalse(any(item.get("source") == "option.graph-sub-target" and item.get("property") != "visible"
                             for item in result["variants"]))
        ids = [item["id"] for item in controls._declarations(result, nodes(result))]
        self.assertEqual(len(ids), len(set(ids)))

    def test_three_layouts_share_only_after_exact_source_geometry_checks(self):
        graphs = self.shared_fixture()
        saved = copy.deepcopy(graphs)
        result, receipt = controls.share_graph_layouts(graphs)
        self.assertEqual(graphs, saved)
        self.assertEqual(len(result["templates"]), 2)
        self.assertEqual(len(result["instances"]), 6)
        self.assertEqual(receipt["p2SourceTranslationPixels"], -1502)
        self.assertTrue(receipt["sourceP1AndDpExact"])
        self.assertFalse(any(item["source"] == "option.layout" for item in result["variants"]))
        modified = copy.deepcopy(graphs)
        modified["p2"]["templates"][0]["root"]["children"][0]["properties"]["y"] += 0.001
        with self.assertRaisesRegex(ValueError, "differs beyond"):
            controls.share_graph_layouts(modified)

    def test_fallback_clones_keep_real_presentation_geometry_variants(self):
        shared, _ = controls.share_graph_layouts(self.shared_fixture())
        result = controls.add_sub_target(shared)
        source_variants = [item for item in result["variants"] if item["source"] == "layout.presentation"]
        self.assertGreater(len(source_variants), len([item for item in shared["variants"] if item["source"] == "layout.presentation"]))
        self.assertTrue(all(item["property"] in ("x", "width") for item in source_variants))
        self.assertTrue(all(item["property"] == "visible" for item in result["variants"] if item["source"] == "option.graph-sub-target"))

    def test_shorter_static_geometry_keeps_renderer_float_bits_and_tween_precision(self):
        shared, _ = controls.share_graph_layouts(self.shared_fixture())
        original = controls.add_sub_target(shared)
        result, receipt = controls.compact_static_geometry(original)
        self.assertGreater(receipt["changedLiterals"], 0)
        self.assertEqual(result["tracks"], original["tracks"])
        for old, new in zip(nodes(original), nodes(result), strict=True):
            for prop, value in old["properties"].items():
                if type(value) is float:
                    self.assertEqual(struct.pack("<f", value), struct.pack("<f", new["properties"][prop]))
        for old, new in zip(original["variants"], result["variants"], strict=True):
            for old_case, new_case in zip(old["cases"], new["cases"], strict=True):
                if type(old_case.get("value")) is float:
                    self.assertEqual(struct.pack("<f", old_case["value"]), struct.pack("<f", new_case["value"]))

    def test_projective_step_frames_keep_the_exact_submitted_floats_and_all_timing(self):
        scene = controls.empty_scene("root")
        scene["tracks"] = [{"id": "frame", "target": "quad", "type": "frame", "property": "projective-quad",
            "keyframes": [{"id": "k", "time": 16.666666666666668, "value": {"vertices": [
                [0.1234567890123456, 0.8765432109876543, 0.0098765432109876]] * 4, "opacity": 0.9876543210987654}}]},
            {"id": "tween", "target": "other", "type": "tween", "property": "x",
             "keyframes": [{"id": "t0", "time": 0, "value": 0.1234567890123456}]}]
        saved = copy.deepcopy(scene)
        result, receipt = controls.compact_projective_frames(scene)
        self.assertEqual(scene, saved)
        self.assertGreater(receipt["changedLiterals"], 0)
        self.assertEqual(result["tracks"][1], scene["tracks"][1])
        old, new = scene["tracks"][0]["keyframes"][0], result["tracks"][0]["keyframes"][0]
        self.assertEqual(old["time"], new["time"])
        for first, second in zip(old["value"]["vertices"], new["value"]["vertices"], strict=True):
            self.assertEqual([struct.pack("<f", value) for value in first], [struct.pack("<f", value) for value in second])
        self.assertEqual(struct.pack("<f", old["value"]["opacity"]), struct.pack("<f", new["value"]["opacity"]))


class GameplayControlPresentationTest(unittest.TestCase):
    def fixture(self, *, geometry=None):
        reference = {"panels": {mode: {layout: {"window": {"bounds": [541, 295, 152, 442],
                        "region": mode, "mirroredX": layout == "p2"}, "shapes": []}
                    for layout in controls.LAYOUTS} for mode in ("normal", "floating", "classic")}}
        source_positions = {"p1": ([566, 316, 74, 22], [572, 625, 62, 22], (560, 587, 622, 649), 707),
                            "p2": ([1286, 316, 74, 22], [1292, 625, 62, 22], (1250, 1277, 1312, 1339), 707),
                            "dp": ([923, 96, 74, 22], [929, 405, 62, 22], (903, 930, 963, 990), 492)}
        for layout, (maximum, minimum, digit_x, digit_y) in source_positions.items():
            for mode in ("normal", "floating", "classic"):
                reference["panels"][mode][layout]["window"]["bounds"] = {"p1": [541, 295, 152, 442],
                    "p2": [1233, 295, 152, 442], "dp": [896, 68, 128, 498]}[layout]
            reference["panels"]["floating"][layout]["shapes"] = [
                {"region": "tx_fspd_num10", "bounds": maximum}, {"region": "tx_fspd_num05", "bounds": minimum},
                *({"region": "playm_spd_00", "bounds": [x, digit_y, 28, 20]} for x in digit_x)]
            classic_geometry = controls.CLASSIC_GEOMETRY[layout]
            cx, cy = classic_geometry["center"]
            lx, ly, _, _ = classic_geometry["notes"]
            reference["panels"]["classic"][layout]["shapes"] = [
                {"region": "tx_num1_4", "bounds": list(classic_geometry["strip"]), "mirroredX": False},
                {"region": "playm_classic_spd_sel", "bounds": [cx - 75, cy + 370 - 35, 150, 70], "mirroredX": False},
                {"region": "tx_notes_time", "bounds": list(classic_geometry["notes"]), "mirroredX": False},
                *({"region": "notes_time_num9g", "bounds": [lx + 10 + 21 * index, ly + 12, 22, 20]} for index in range(3))]
            if layout != "dp":
                reference["panels"]["classic"][layout]["shapes"].append({"region": "tx_classic_hispeed",
                    "bounds": [665 if layout == "p1" else 1243, 364, 18, 304], "mirroredX": False})
        names = ("normal", "floating", "classic", "tx_sub_length", "tx_notes_time", "slider_bar", "playm_spd_sel",
                 "tx_fspd_num10", "tx_fspd_num05", "tx_classic_hispeed", *controls.CLASSIC_ASSET_NAMES)
        assets = {"mapping": {name: "synthetic." + name for name in names}}
        return controls.build_controls(reference, assets, stage_geometry=geometry or [{"width": 440, "height": 728, "frames": ["synthetic"]}])

    def test_selected_frame_captures_only_its_actual_stage_geometry(self):
        scene = self.fixture(geometry=[{"width": 438, "height": 726, "frames": ["sd"]},
                                       {"width": 440, "height": 728, "frames": ["hd"]}])
        stage, global_controls = controls._selected_document(scene, {"gc.stage.t"})
        for frame, height in (("sd", 726), ("hd", 728)):
            selected = controls.select_stage_frame(stage, frame)
            self.assertFalse(any(item["source"] == "option.frame" for item in selected["variants"]))
            label_rows = [node for node in nodes(selected) if node["id"].endswith("sudden.wlr")]
            self.assertEqual(len(label_rows), 1)
            self.assertEqual(label_rows[0]["properties"]["y"], -30 / height)
        self.assertFalse(any(item["root"]["id"] == "gc.stage" for item in global_controls["templates"]))

    def test_native_hold_mode_and_each_native_cover_own_the_presentation(self):
        scene = self.fixture()
        self.assertEqual(sum(item["source"] == "scroll.adjusting" for item in scene["bindings"]), 4)
        self.assertEqual({item["source"] for item in scene["bindings"] if item["property"] == "text"},
                         {"scroll.hi-speed", "scroll.green-number", "cover.sudden-units", "cover.hidden-units", "lift.value"})
        by_id = {node["id"]: node for node in nodes(scene)}
        self.assertEqual(by_id["gc.stage.g0.sudden.wlr"]["properties"]["y"], -30 / 728)
        self.assertEqual(by_id["gc.stage.g0.hidden.wlr"]["properties"]["y"], 10 / 728)
        self.assertEqual(by_id["gc.stage.g0.lift.false.wlr"]["properties"]["y"], 10 / 728)
        self.assertEqual(by_id["gc.stage.g0.lift.true.wlr"]["properties"]["y"], -30 / 728)

    def test_all_sliders_sample_actual_speed_with_clamped_nonloop_endpoints(self):
        scene = self.fixture()
        sliders = [track for track in scene["tracks"] if track.get("clock") == "scroll.speed"]
        self.assertEqual(len(sliders), 9)
        for slider in sliders:
            self.assertFalse(slider["loop"])
            self.assertNotIn("trigger", slider)
            self.assertEqual(slider["property"], "y")
            self.assertEqual(slider["keyframes"][0]["time"], 1 if ".normal." in slider["id"] or ".classic." in slider["id"] else 0.5)
            self.assertEqual(slider["keyframes"][-1]["time"], 20 if ".normal." in slider["id"] else 4 if ".classic." in slider["id"] else 10)
            self.assertGreater(slider["keyframes"][0]["value"], slider["keyframes"][1]["value"])

    def test_normal_ticks_are_unchanged_and_classic_uses_one_complete_original_strip(self):
        scene = self.fixture()
        for layout in controls.LAYOUTS:
            for mode in ("normal", "classic"):
                ticks = [node for node in nodes(scene) if node["id"].startswith(f"gc.{layout}.{mode}.tick")]
                self.assertEqual(len(ticks), 11 if mode == "normal" else 0)
                if mode == "normal":
                    values = [float(node["properties"]["text"]) for node in ticks]
                    self.assertEqual((values[0], values[-1]), (20, 1))
                else:
                    strip, = [node for node in nodes(scene) if node["id"] == "cl." + layout + ".s"]
                    self.assertEqual(strip["resource"], "synthetic.tx_num1_4")
                    self.assertEqual(strip["type"], "sprite")
                selected, = [node for node in nodes(scene) if node["id"] == f"gc.{layout}.{mode}.slider"]
                self.assertEqual(selected["resource"], "synthetic.playm_spd_sel" if mode == "normal" else "synthetic.playm_classic_spd_sel")

    def test_only_floating_keeps_the_extra_current_speed_readout(self):
        scene = self.fixture()
        speed_readouts = [item for item in scene["bindings"] if item["property"] == "text" and item["source"] == "scroll.hi-speed"]
        self.assertEqual([item["target"] for item in speed_readouts], ["gc." + layout + ".floating.value" for layout in controls.LAYOUTS])

    def test_floating_limits_use_original_baked_sprites_and_current_uses_source_digit_union(self):
        scene = self.fixture()
        by_id = {node["id"]: node for node in nodes(scene)}
        for layout, center, maximum, minimum in (
                ("p1", (618.5, 717), (566, 316, 74, 22), (572, 625, 62, 22)),
                ("p2", (1308.5, 717), (1286, 316, 74, 22), (1292, 625, 62, 22)),
                ("dp", (960.5, 502), (923, 96, 74, 22), (929, 405, 62, 22))):
            current = by_id["gc." + layout + ".floating.value"]["properties"]
            self.assertEqual(current["format"], "fixed-2")
            self.assertAlmostEqual(current["x"], center[0] / 1920 - 0.5)
            self.assertAlmostEqual(current["y"], center[1] / 1080 - 0.5)
            for suffix, name, bounds in (("max", "tx_fspd_num10", maximum), ("min", "tx_fspd_num05", minimum)):
                node = by_id["gc." + layout + ".floating." + suffix]
                self.assertEqual(node["type"], "sprite")
                self.assertEqual(node["resource"], "synthetic." + name)
                for prop, value, dimension in zip(("x", "y", "width", "height"), bounds, (1920, 1080, 1920, 1080), strict=True):
                    self.assertEqual(node["properties"][prop], value / dimension)

    def test_fixed_pixels_only_appear_on_the_codecs_horizontal_sprite_leaves(self):
        scene = self.fixture()
        def visit(node, horizontal=False):
            pixels = set(node["properties"]) & {"width-px", "height-px"}
            if pixels:
                self.assertTrue(horizontal)
                self.assertEqual(node["type"], "sprite")
                self.assertEqual(node["children"], [])
                self.assertEqual(node["properties"]["height-px"], 12 * controls.SCALE)
            if horizontal:
                self.assertIn(node["type"], ("sprite", "text"))
                self.assertEqual(node["children"], [])
                self.assertFalse(set(node["properties"]) & {"x", "y", "width", "height", "scale-x", "scale-y", "rotation", "anchor", "origin", "alignment"})
            for child in node["children"]:
                visit(child, node["properties"].get("layout") == "horizontal")
        for root in (scene["root"], *(t["root"] for t in scene["templates"])):
            visit(root)

    def test_visibility_and_opacity_never_compete_for_one_transform_alpha(self):
        scene = self.fixture()
        writers = {}
        for field in ("bindings", "variants", "tracks"):
            for program in scene[field]:
                property_name = "alpha" if program["property"] in ("visible", "opacity") else program["property"]
                key = program["target"], property_name
                self.assertNotIn(key, writers, f"{writers.get(key)} and {program['id']} write the same runtime field")
                writers[key] = program["id"]
        by_id = {node["id"]: node for node in nodes(scene)}
        self.assertEqual(by_id["gc.p1.nosud"]["children"][0]["id"], "gc.p1.nolift")
        self.assertEqual(by_id["gc.stage.g0.lift"]["children"][0]["id"], "gc.stage.g0.lift.nonzero")

    def test_cover_and_lift_edges_use_original_full_stage_and_actual_lift_offset(self):
        scene = self.fixture()
        edges = {track["clock"] + ":" + track["target"]: track for track in scene["tracks"] if track["id"].endswith(".edge")}
        for position in ("sudden", "hidden"):
            edge = edges[f"cover.{position}-units:gc.stage.g0.{position}"]
            for units in (10, 125, 500, 1000):
                fraction = units / 1000 if position == "sudden" else 1 - units / 1000
                first, last = edge["keyframes"]
                self.assertAlmostEqual(first["value"] + (last["value"] - first["value"]) * units / 1000, fraction)
        self.assertEqual(len([key for key in edges if key.startswith("lift.offset:")]), 2)
        self.assertFalse(any(key.startswith("lift.value:") for key in edges))

    def test_p2_preserves_original_mirrored_geometry_without_negative_scale(self):
        scene = self.fixture()
        mirrors = [track for track in scene["tracks"] if track["property"] == "projective-quad"]
        self.assertEqual(len(mirrors), 3)
        for mirror in mirrors:
            self.assertEqual(mirror["keyframes"][0]["value"]["vertices"], [[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]])
        self.assertFalse(any(node["properties"].get("scale-x", 1) < 0 for node in nodes(scene)))

    def test_visible_green_numbers_use_the_proven_shared_original_white_font(self):
        scene = self.fixture()
        green_targets = {item["target"] for item in scene["bindings"] if item["source"] == "scroll.green-number"}
        by_id = {node["id"]: node for node in nodes(scene)}
        self.assertEqual(len(green_targets), 10)
        for target in green_targets:
            self.assertEqual(by_id[target]["properties"]["bitmap-font"], controls.FONT)
            self.assertEqual(by_id[target]["properties"]["colour"], "#64ff00ff")


class StageReadoutIniTest(unittest.TestCase):
    def test_only_bms_stage_owner_and_its_group_inheritance_boundary_change(self):
        source = (b'[GameplaySkin.Common:1]\r\nTarget: Stage ruleset=bms keymode=7k group=bms.group.deck-1\r\n'
                  b'decoration: resource Suppress\r\nother: untouched\r\n'
                  b'Target: Stage ruleset=mania keymode=7k group=mania.group.stage-1\r\n'
                  b'decoration: resource Suppress\r\n')
        result, changed = readout_ini(source)
        self.assertEqual(changed, 1)
        group_boundary = (b'Target: Group ruleset=bms keymode=7k group=bms.group.deck-1\r\n'
                          b'decoration: resource Suppress\r\n')
        self.assertIn(group_boundary, result)
        self.assertEqual(result.replace(group_boundary, b'').replace(
            b'decoration: resource Provide "gameplay/controls/stage-readout"', b'decoration: resource Suppress', 1), source)
        self.assertEqual(audit_readout_scopes(result)["readoutTreesPerActualStage"], 1)
        self.assertEqual(audit_readout_scopes(result)["readoutTreesPerGroup"], 0)
        self.assertEqual(audit_readout_scopes(result)["readoutTreesPerLane"], 0)

    def test_scope_audit_rejects_inherited_readouts_and_any_explicit_lane_copy(self):
        stage = (b'[GameplaySkin.Common:1]\nTarget: Stage ruleset=bms keymode=7k group=bms.group.deck-1 group-logical=0 group-visual=0\n'
                 b'decoration: resource Suppress\n')
        lane = (b'Target: Lane ruleset=bms keymode=7k group=bms.group.deck-1 group-logical=0 group-visual=0 '
                b'lane=bms.lane.key-1 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1\n')
        result, _ = readout_ini(stage + lane + b'playfield.key: resource Suppress\n')
        audit = audit_readout_scopes(result)
        self.assertEqual(audit["laneProbes"], 1)
        inherited = stage.replace(b'decoration: resource Suppress', b'decoration: resource Provide "gameplay/controls/stage-readout"')
        with self.assertRaisesRegex(ValueError, "inherits to Group"):
            audit_readout_scopes(inherited)
        explicit_lane = result + lane + b'decoration: resource Provide "gameplay/controls/stage-readout"\n'
        with self.assertRaisesRegex(ValueError, "inherits to Lane"):
            audit_readout_scopes(explicit_lane)

    def test_retire_only_the_old_exact_lift_decoration_leaving_fill_and_other_rulesets(self):
        source = (b'[GameplaySkin.Bms:1]\nTarget: Stage ruleset=bms keymode=7k group=bms.group.deck-1\n'
                  b'playfield.lift.fill: resource Provide "kept"\n'
                  b'playfield.lift.decoration: resource Provide "custom/markers/v/readout"\n')
        result, changed = readout_ini(source, lift=True)
        self.assertEqual(changed, 1)
        self.assertIn(b'playfield.lift.fill: resource Provide "kept"', result)
        self.assertNotIn(b'lift.decoration', result)


class FloatingOriginalPixelTest(unittest.TestCase):
    @staticmethod
    def png(image):
        encoded = io.BytesIO()
        image.save(encoded, format="PNG")
        return encoded.getvalue()

    def test_point_keeps_every_original_rgba_pixel_below_transparent_twelve_rows(self):
        from PIL import Image

        original = Image.new("RGBA", (8, 8))
        original.putdata([(x * 31, y * 31, 255 - x * 31, (x + y) * 17) for y in range(8) for x in range(8)])
        source = self.png(original)
        encoded, proof = controls.derive_control_png("playm_spd_dot", source)
        result = Image.open(io.BytesIO(encoded)).convert("RGBA")
        self.assertEqual(result.size, (8, 20))
        self.assertEqual(result.crop((0, 12, 8, 20)).tobytes(), original.tobytes())
        self.assertEqual(result.crop((0, 0, 8, 12)).tobytes(), bytes(8 * 12 * 4))
        self.assertEqual(proof["lineHeightAndAdvanceUnchanged"], [20, 8])
        self.assertEqual(controls.derive_control_png("playm_spd_dot", source)[0], encoded)

    def test_only_measured_nine_pixel_point_square_changes_and_other_rgba_is_exact(self):
        from PIL import Image

        for name, dimensions, patch in (("floating_spd_window", (152, 442), (73, 425, 9, 9)),
                                        ("floating_spd_window_dp", (128, 498), (60, 437, 9, 9))):
            with self.subTest(name=name):
                original = Image.new("RGBA", dimensions, (255, 132, 0, 255))
                original.putpixel((10, 10), (1, 2, 3, 4))
                x, y, width, height = patch
                original.paste((255, 255, 255, 255), (x, y, x + width, y + height))
                encoded, proof = controls.derive_control_png(name, self.png(original))
                result = Image.open(io.BytesIO(encoded)).convert("RGBA")
                for py in range(dimensions[1]):
                    for px in range(dimensions[0]):
                        self.assertEqual(result.getpixel((px, py)), (255, 132, 0, 255)
                                         if x <= px < x + width and y <= py < y + height else original.getpixel((px, py)))
                self.assertEqual(proof["changedPixels"], 81)
                self.assertTrue(proof["outsidePatchRgbaUnchanged"])


if __name__ == "__main__":
    unittest.main()
