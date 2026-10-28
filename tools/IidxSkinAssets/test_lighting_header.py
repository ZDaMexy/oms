"""Header author regressions; actual shared codec and native pixels are separate gates."""
import copy
import unittest

from lighting_header import append_header, header_program, identifiers, overlaps, walk


def reference():
    layouts = {}
    for movie, player, difficulty, label, digits in [
            ("center_frame_sp", "1p", [379, 38, 565, 64], [429, 69, 475, 89], [477, 69, 513, 89]),
            ("dp_frame_1p", "1p", [21, 129, 207, 155], [72, 158, 118, 178], [118, 158, 154, 178])]:
        draws = []
        for binding, texture, bounds in [("diff_", "playm_hyper", difficulty),
                                         ("lv_", "txt_lv_dot", label),
                                         ("lvlnum_", "lvlnum_12", digits)]:
            draws.append({"game_bindings": [binding + player], "texture": texture,
                          "asset": {"package": "gmframe33", "name": texture},
                          "bounds": dict(zip(("left", "top", "right", "bottom"), bounds))})
        layouts[movie] = {"package": "gmframe33", "draws": draws}
    # The original two SP fields have separate source hooks, not a text-mirroring rule.
    layouts["center_frame_sp"]["draws"].extend([
        {**copy.deepcopy(draw), "game_bindings": [draw["game_bindings"][0].replace("1p", "2p")],
         "bounds": {**draw["bounds"], "left": draw["bounds"]["left"] + 977,
                    "right": draw["bounds"]["right"] + 977}}
        for draw in layouts["center_frame_sp"]["draws"][:]])
    return {"stage": {"width": 1920, "height": 1080, "fps": 60}, "layouts": layouts}


def scene():
    fields = [{"id": "title", "type": "text", "properties": {"text": ""}, "children": []},
              {"id": "artist", "type": "text", "properties": {"text": ""}, "children": []}]
    return {"root": {"id": "old-root", "properties": {}, "children": []},
            "templates": [{"id": "info", "root": {"id": "info-root", "slot": "decoration",
                            "target": {"kind": "global"}, "properties": {}, "children": fields}}],
            "instances": [{"id": "info-instance", "template": "info", "material": {"slot": "decoration", "resource": "old-marker"}}],
            "bindings": [{"id": "title-binding", "target": "title", "property": "text", "source": "song.title"},
                         {"id": "artist-binding", "target": "artist", "property": "text", "source": "song.artist"}],
            "variants": [], "tracks": [], "stateMachines": []}


class HeaderAuthorTests(unittest.TestCase):
    def test_p1_p2_and_dp_use_their_real_hooks_without_mirroring_text(self):
        expected = {"p1": (649, 38), "p2": (1086, 38), "dp": (21, 129)}
        for layout, (left, top) in expected.items():
            with self.subTest(layout=layout):
                program = header_program(reference(), layout, "new")
                bounds = program["receipt"]["fields"][0]["appliedBounds"]
                self.assertEqual((bounds["left"], bounds["top"]), (left, top))
                self.assertEqual(bounds["right"] - bounds["left"], 186)
                self.assertNotIn("scale-x", program["root"]["children"][0]["properties"])

    def test_real_fields_are_raw_and_empty_without_placeholder_or_numeric_conversion(self):
        program = header_program(reference(), "p1", "new")
        self.assertEqual([item["source"] for item in program["bindings"]], ["song.difficulty", "song.level"])
        for child in program["root"]["children"]:
            self.assertEqual(child["properties"]["text"], "")
            self.assertNotIn("format", child["properties"])
            self.assertNotIn("bitmap-font", child["properties"])
        self.assertFalse(any(item.get("resource") for item in walk(program["root"])))

    def test_dp_level_avoids_the_complete_real_nonempty_mod_display_bounds(self):
        program = header_program(reference(), "dp", "new", mods_rect=[0.015, 0.15, 0.18, 0.065])
        fields = program["receipt"]["fields"]
        protected = program["receipt"]["protectedNativeModsBounds"]
        self.assertTrue(overlaps(fields[1]["sourceBounds"], protected))
        for field in fields:
            self.assertFalse(overlaps(field["appliedBounds"], protected))
        self.assertEqual(fields[0]["appliedBounds"], fields[0]["sourceBounds"])
        self.assertEqual({key: value for key, value in fields[1]["appliedBounds"].items() if key != "right"},
                         {"left": 211, "top": 129, "bottom": 155})
        self.assertAlmostEqual(fields[1]["appliedBounds"]["right"], 374.4)
        self.assertIsNotNone(fields[1]["safeAreaAdaptation"])

    def test_long_level_and_difficulty_are_bounded_and_do_not_resize_metadata(self):
        original = scene()
        result, receipt = append_header(original, reference(), "p2", "new", identifiers(original))
        old_fields = result["templates"][0]["root"]["children"][:2]
        self.assertEqual(old_fields, original["templates"][0]["root"]["children"])
        for child in result["templates"][0]["root"]["children"][-1]["children"]:
            self.assertEqual(child["properties"]["text-overflow"], "ellipsis")
            self.assertLess(child["properties"]["width"], 0.1)
        self.assertTrue(receipt["allOriginalSceneFieldsAndIdsPreserved"])

    def test_mania_has_no_iidx_difficulty_mapping_or_alpha_writer_conflict(self):
        program = header_program(reference(), "dp", "new")
        self.assertEqual(program["variants"], [{"id": "newv", "target": "new", "property": "visible", "source": "layout.ruleset",
                                               "default": False, "cases": [{"id": "newc", "key": "bms", "value": True}]}])
        self.assertFalse(any(item["property"] in ("visible", "opacity") for item in program["bindings"]))

    def test_source_ambiguity_and_other_cabinets_are_rejected(self):
        for change in ("canvas", "cabinet", "ambiguous", "missing"):
            data = reference()
            if change == "canvas":
                data["stage"]["width"] = 1280
            elif change == "cabinet":
                data["layouts"]["center_frame_sp"]["package"] = "gmframe32"
            elif change == "missing":
                data["layouts"]["center_frame_sp"]["draws"].pop(0)
            else:
                duplicate = copy.deepcopy(data["layouts"]["center_frame_sp"]["draws"][0])
                duplicate["bounds"]["left"] += 1
                data["layouts"]["center_frame_sp"]["draws"].append(duplicate)
            with self.subTest(change=change), self.assertRaises(ValueError):
                header_program(data, "p1", "new")

    def test_collision_with_another_selected_module_is_not_renamed_silently(self):
        with self.assertRaises(ValueError):
            append_header(scene(), reference(), "p1", "new", {"newbl"})

    def test_scope_and_single_existing_information_instance_are_required(self):
        for change in ("stage", "pool", "translated", "existing"):
            parent = scene()
            if change == "stage":
                parent["templates"][0]["root"]["target"] = {"kind": "stage"}
            elif change == "pool":
                parent["instances"].append({**parent["instances"][0], "id": "duplicate"})
            elif change == "translated":
                parent["templates"][0]["root"]["properties"] = {"x": 0.1}
            else:
                parent["bindings"].append({"target": "title", "source": "song.difficulty"})
            with self.subTest(change=change), self.assertRaises(ValueError):
                append_header(parent, reference(), "p1", "new", identifiers(parent))

    def test_original_ids_and_every_unrelated_program_survive(self):
        parent = scene()
        parent["tracks"] = [{"id": "unrelated-track", "target": "title", "property": "opacity", "keyframes": [{"id": "old-frame", "time": 0, "value": 1}]}]
        frozen = copy.deepcopy(parent)
        result, receipt = append_header(parent, reference(), "dp", "new", identifiers(parent))
        self.assertEqual(parent, frozen)
        self.assertTrue(identifiers(parent).issubset(identifiers(result)))
        self.assertEqual(result["tracks"], parent["tracks"])
        self.assertEqual(result["instances"], parent["instances"])
        self.assertEqual(receipt["reservedTextGlyphIncrement"], 512)


if __name__ == "__main__":
    unittest.main()
