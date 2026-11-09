"""Original20 components and source-specific default routing, synthetic art only."""
from copy import deepcopy
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard21_components as source21
import test_standard_pendual_components as fixture
from standard_components import source20_turntable_size, source22_turntable_size
from standard_source_catalog import (COMPOSITION20, SOURCE20_ALIASES, SOURCE20_STYLES,
                                     SOURCE21_STYLES, SOURCE22_STYLES, frame_id,
                                     package_key, layout_family, retained_styles, styles)

STYLE = SOURCE20_STYLES[0]


def owner(with20=True):
    value = source21.extended_owner()
    if with20:
        value.source_data["sourceComposition"].update(contract=COMPOSITION20, aliases20=SOURCE20_ALIASES)
        value.frame_ids = tuple(frame_id(s) for s in styles(value.source_data))
        value.packages.update({key: {} for key in SOURCE20_ALIASES.values()})
        value.index["source20/turn00", "turn00"] = {"width": 25, "height": 25, "sha256": "own20-default"}
        value.index["s20/gameparts", "turn_00"] = dict(value.index["source20/turn00", "turn00"])
    for kind, count in (("beam", 11), ("notes", 12)):
        value.packages.update({f"{kind}{i:02}": {} for i in range(count)})
    for i in range(12):
        for name, width in (("white", 34), ("black", 26), ("turn", 60)):
            value.index[f"notes{i:02}", "notes_" + name] = {"width": width, "height": 8}
    return value


def own_draw(binding, x=55):
    draw = fixture.turn_draw(binding, True)
    draw.update(texture="turn_00", asset={"package": "s20/gameparts", "width": 25, "height": 25})
    draw["draw_params"][0]["region"] = "turn_00"
    draw["matrix"].update(tx=x, ty=512)
    draw["bounds"] = {"left": x, "right": x + 25, "top": 512, "bottom": 537}
    draw["projected_vertices"] = [{"x": x + p["x"], "y": 512 + p["y"], "z": 0} for p in draw["source_vertices"]]
    return draw


def build_turntables(value):
    value.frame_maps = Path("synthetic-frame-maps.json")
    inputs = {str(value.frame_maps): {frame_id(s): frame_id(s) + ".json" for s in retained_styles(value.source_data)}}
    for style in retained_styles(value.source_data):
        identity = frame_id(style)
        if style in SOURCE20_STYLES:
            layouts = {"1p_frame": {"draws": [own_draw("turn_1p", 55)]},
                       "2p_frame": {"draws": [own_draw("turn_2p", 1200)]},
                       "dp_frame": {"draws": [own_draw("turn_1p", 311), own_draw("turn_2p", 945)]}}
        else:
            normalized = style in (*SOURCE21_STYLES, *SOURCE22_STYLES)
            layouts = {movie: {"draws": [fixture.turn_draw(binding, normalized) for binding in bindings]}
                       for movie, bindings in (("1p_frame", ("turn_1p",)), ("2p_frame", ("turn_2p",)),
                                               ("dp_frame_1p", ("turn_1p", "turn_2p")))}
        inputs[identity + ".json"] = {"package": package_key(style), "common_family": layout_family(style),
                                      "stage": {"width": 1280, "height": 720}, "layouts": layouts}
    with patch("standard_components.read_json", side_effect=lambda p: inputs[str(p)]), \
            patch("standard_components.within", side_effect=lambda root, relative: Path(relative)):
        value.turntables()


class Standard20ComponentsTests(unittest.TestCase):
    def test_default_carrier_is_own25px_texture_and_is_not_a21_or22_dynamic_hook(self):
        source = {"package": package_key(STYLE), "common_family": "s20"}
        draw = own_draw("turn_1p")
        self.assertEqual((25, 25), source20_turntable_size(STYLE, source, draw))
        with self.assertRaises(ValueError):
            source22_turntable_size(STYLE, source, draw)
        for field, replacement in (("package", "source21/gmframe0"), ("common_family", "s21")):
            with self.subTest(field=field), self.assertRaises(ValueError):
                source20_turntable_size(STYLE, {**source, field: replacement}, draw)
        draw["draw_params"][0]["region"] = "turn00"
        with self.assertRaises(ValueError):
            source20_turntable_size(STYLE, source, draw)

    def test_default_changes_only20_route_and_retains_one_draw_per_scratch_group(self):
        value = owner()
        build_turntables(value)
        module = value.modules[0]
        self.assertEqual("t00", module["default"])
        self.assertEqual([f"t{i:02}" for i in range(21)] + ["source20-turn00"], [c["value"] for c in module["choices"]])
        for choice in module["choices"]:
            scene = value.documents[choice["scene"]]
            nodes = {n["id"]: n for t in scene["templates"] for n in fixture.walk(t["root"])}
            arts = [n for n in nodes.values() if STYLE in n["id"] and n["type"] == "projective-sprite"]
            self.assertEqual(4, len(arts))
            expected_package = "source20/turn00" if choice["value"] in ("t00", "source20-turn00") else "turn" + choice["value"][1:]
            expected = value.asset("st", expected_package, expected_package.rsplit("/", 1)[-1])[0]["id"]
            self.assertEqual({expected}, {n["resource"] for n in arts})
            for art in arts:
                self.assertEqual((25 / 1280, 25 / 720), (art["properties"]["width"], art["properties"]["height"]))
            tracks = [t for t in scene["tracks"] if STYLE in t["target"] and t["property"] == "rotation"]
            self.assertEqual(4, len(tracks))
            for track in tracks:
                self.assertEqual("input.motion-revolutions-2" if ".dp." in track["target"] and track["target"].endswith(".n1")
                                 else "input.motion-revolutions-1", track["clock"])
        self.assertIn("not qualified", value.receipts[-1]["source20"]["customProjection"])

    def test_old18_turntable_templates_tracks_and_resources_remain_exact(self):
        old, new = owner(False), owner()
        self.assertEqual(18, len(old.frame_ids))
        build_turntables(old)
        build_turntables(new)
        for first, extended in zip(old.modules[0]["choices"], new.modules[0]["choices"]):
            self.assertEqual(first, extended)
            before, after = old.documents[first["scene"]], new.documents[extended["scene"]]
            nodes = {n["id"]: n for t in after["templates"] for n in fixture.walk(t["root"])}
            for template in before["templates"]:
                for branch in template["root"]["children"]:
                    self.assertEqual(branch, nodes[branch["id"]])
            tracks = {t["id"]: t for t in after["tracks"]}
            for track in before["tracks"]:
                self.assertEqual(track, tracks[track["id"]])
            manifest_before, manifest_after = old.documents[first["manifest"]], new.documents[extended["manifest"]]
            resources = {r["id"]: r for r in manifest_after["resources"]}
            self.assertTrue(all(resources[r["id"]] == r for r in manifest_before["resources"]))

    def test_original_common_choices_and_defaults_do_not_duplicate_exact20_custom(self):
        for method in ("notes", "beams", "static_lanes", "lift_art", "charge_notes"):
            with self.subTest(method=method):
                old, new = owner(False), owner()
                getattr(old, method)()
                getattr(new, method)()
                self.assertEqual(old.options, new.options)
                self.assertEqual(old.modules, new.modules)
                self.assertEqual(old.documents, new.documents)
                self.assertEqual(old.calls, new.calls)

    def test_default20_must_match_its_original_frame_image_instead_of_falling_back(self):
        value = owner()
        value.index["source20/turn00", "turn00"]["sha256"] = "other-source"
        with self.assertRaisesRegex(ValueError, "differs from its supplied CUSTOM"):
            build_turntables(value)

    def test_graph_art_keeps20_original_panel_without_borrowing_the26_customize_hook(self):
        with tempfile.TemporaryDirectory() as directory, patch("standard_components.LocalStateRecorder", fixture.GraphRecorder):
            old, new = owner(False), owner()
            fixture.setup_graph(old, Path(directory) / "old")
            fixture.setup_graph(new, Path(directory) / "new")
            original_package = new._package
            own = {"movies": {m: SimpleNamespace(tags=[], frames=[None] * 60) for m in ("graph_1p", "graph_2p")}}
            new._package = lambda package: own if package == "s20/graph" else original_package(package)
            old.graph_art()
            new.graph_art()
            self.assertEqual("blank", new.modules[0]["default"])
            self.assertEqual(old.modules[0]["choices"][0], new.modules[0]["choices"][0])
            for first, extended in zip(old.modules[0]["choices"][1:], new.modules[0]["choices"][1:]):
                self.assertEqual(first, {k: v for k, v in extended.items() if k != "variants"})
                variant, = extended["variants"]
                self.assertEqual({"frame": STYLE}, variant["when"])
                self.assertEqual(new.modules[0]["choices"][0]["scene"], variant["scene"])
                self.assertEqual([], new.documents[variant["scene"]]["templates"])
                self.assertEqual(old.documents[first["scene"]], new.documents[first["scene"]])
            self.assertEqual("s20/graph", new.receipts[-1]["source20"]["sourcePackage"])
            self.assertFalse(new.receipts[-1]["source20"]["customizeHookPresent"])


if __name__ == "__main__":
    unittest.main()
