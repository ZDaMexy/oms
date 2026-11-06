"""Earlier cabinet input mapping, source isolation and original key transforms."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from standard_controls import build_controls, cabinet_keys, project, quad_for_host
from standard_source_catalog import COMPOSITION, EARLIER_ALIASES


def key_reference(style, layout):
    movie = "center_frame_dp_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"
    fields = [(304, 596), (684, 976)] if layout == "dp" else [(940, 1232)] if layout == "p2" else [(48, 340)]
    draws = []

    def asset(name, width, height):
        return {"package": f"gmframe{style}", "name": name, "width": width, "height": height,
                "sprite": f"packages/contents/data/graphic/gmframe{style}/sprites/{name}.png"}

    for group, (left, right) in enumerate(fields):
        is_right = group == 1 if layout == "dp" else layout == "p2"
        draws.append({"texture": "sp_lane_2p" if is_right else "sp_lane_1p", "asset": asset("lane", 292, 484),
                      "source_vertices": [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (292, 0), (0, 484), (292, 484))],
                      "bounds": {"left": left, "right": right, "top": 30.72265625 if style == 24 else 0, "bottom": 484},
                      "game_bindings": []})
        if style == 23:
            x = (682 if is_right else 364) if layout == "dp" else (938 if is_right else 108)
            y = 483 if layout == "dp" else 485
            draws.append({"texture": "keyboard", "asset": asset("keyboard", 234, 68),
                          "geometry": movie + "_synthetic_keyboard", "game_bindings": [],
                          "bounds": {"left": x, "right": x + 234, "top": y, "bottom": y + 68},
                          "matrix": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": x, "ty": y}})
        else:
            # Reverse declaration order, as a mirrored source does not promise
            # that source order equals physical left-to-right input order.
            for i in reversed(range(7)):
                white = i % 2 == 0
                width, height = (34, 54) if white else (26, 46)
                x = left + (2 if is_right else 64) + 32 * i + (0 if white else 4)
                bottom = 545 if white else 533
                scale = .666015625
                y = bottom - height * scale
                mirror = layout == "p2"
                name = "key_w" if white else "key_b"
                draws.append({"texture": name, "asset": asset(name, width, height),
                              "geometry": movie + "_synthetic_" + str(i), "game_bindings": [],
                              "bounds": {"left": x, "right": x + width, "top": y, "bottom": bottom},
                              "matrix": {"a": -1 if mirror else 1, "b": 0, "c": 0, "d": scale,
                                         "tx": x + width if mirror else x, "ty": y}})
    return {"package": f"gmframe{style}", "common_family": f"s{style}",
            "stage": {"width": 1280, "height": 720, "fps": 60.0}, "layouts": {movie: {"draws": draws}}}


class EarlierControlsTests(unittest.TestCase):
    def test_copula_caps_follow_each_original_panel_and_not_the_mirrored_lane(self):
        for layout, expected in (("p1", [(129, 518.5)]), ("p2", [(959, 518.5)]),
                                 ("dp", [(385, 516.5), (703, 516.5)])):
            reference = key_reference(23, layout)
            for group, centre in enumerate(expected):
                with self.subTest(layout=layout, group=group):
                    _, field, keys = cabinet_keys(reference, 23, layout, group)
                    self.assertEqual(7, len(keys))
                    self.assertEqual(["white", "black", "white", "black", "white", "black", "white"], [k["colour"] for k in keys])
                    self.assertEqual([7, 10, 28, 47], keys[0]["cap"])
                    self.assertEqual([44, 6, 19, 40], keys[1]["cap"])
                    self.assertEqual([44, 107, 171], [k["cap"][0] for k in keys if k["colour"] == "black"])
                    self.assertEqual(list(centre), project(keys[0]["draw"]["matrix"], 21, 33.5))
                    self.assertEqual(484, field["bottom"])
                    self.assertTrue(all(k["draw"]["asset"]["package"] == "gmframe23" for k in keys))

    def test_sinobuz_retains_independent_key_mesh_scaling_and_p2_mirror(self):
        for layout in ("p1", "p2", "dp"):
            for group in range(2 if layout == "dp" else 1):
                with self.subTest(layout=layout, group=group):
                    _, field, keys = cabinet_keys(key_reference(24, layout), 24, layout, group)
                    self.assertEqual(0, field["top"])
                    self.assertEqual(7, len(keys))
                    x = [k["draw"]["bounds"]["left"] for k in keys]
                    self.assertEqual(sorted(x), x)
                    for key in keys:
                        width, height = (34, 54) if key["colour"] == "white" else (26, 46)
                        self.assertEqual([0, 0, width, height], key["cap"])
                        self.assertEqual(.666015625, key["draw"]["matrix"]["d"])
                        self.assertEqual(-1 if layout == "p2" else 1, key["draw"]["matrix"]["a"])
                    host = [field["left"], field["bottom"], 34, 80]
                    # Use the nearest left cap's own host for the finite quad.
                    host[0] = keys[0]["draw"]["bounds"]["left"]
                    quad = quad_for_host(keys[0], {"width": 50, "height": 69}, host)["vertices"]
                    self.assertAlmostEqual(69 * .666015625, (quad[2][1] - quad[1][1]) * 80)
                    self.assertEqual(layout == "p2", quad[0][0] > quad[1][0])

    def test_missing_or_wrong_family_caps_are_not_filled_from_another_style(self):
        for style in (23, 24):
            for fault in ("missing", "wrong-family"):
                with self.subTest(style=style, fault=fault):
                    reference = key_reference(style, "p1")
                    draws = reference["layouts"]["1p_frame"]["draws"]
                    target = next(d for d in draws if d["texture"] == ("keyboard" if style == 23 else "key_w"))
                    if fault == "missing":
                        draws.remove(target)
                    else:
                        target["asset"]["package"] = "gmframe26"
                    with self.assertRaises(ValueError):
                        cabinet_keys(reference, style, "p1", 0)

    def test_actual_input_uses_only_its_own_gameparts_and_leaves_scratch_empty(self):
        for style in (23, 24):
            with self.subTest(style=style), tempfile.TemporaryDirectory(prefix="earlier-controls-") as directory:
                root = Path(directory).resolve()
                library = root / "library"
                reference = key_reference(style, "dp")
                family = f"s{style}"
                textures = []

                def add(package, name, width, height):
                    relative = f"packages/contents/data/graphic/{package}/sprites/{name}.png"
                    path = library / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(("synthetic source bytes " + package + "/" + name).encode())
                    row = {"package": f"contents/data/graphic/{package}.ifs", "name": name + ".png",
                           "width": width, "height": height, "sprite": relative,
                           "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                    textures.append(row)
                    return row

                for draw in reference["layouts"]["center_frame_dp_1p"]["draws"]:
                    if draw["texture"] not in ("keyboard", "key_w", "key_b"):
                        continue
                    asset = draw["asset"]
                    row = next((r for r in textures if Path(r["name"]).stem == asset["name"]), None)
                    row = row or add(asset["package"], asset["name"], asset["width"], asset["height"])
                    asset["sha256"] = row["sha256"]
                for colour, size in (("white", (50, 69)), ("black", (42, 62))):
                    add(f"{family}/gameparts", "light_" + colour + "_key", *size)
                    add("0/gameparts", "light_" + colour + "_key", *size)  # A different-source decoy must never win.
                sampler = library / f"packages/contents/data/graphic/{family}/gameparts/raw/tex/texturelist.xml"
                sampler.parent.mkdir(parents=True)
                sampler.write_text('<texturelist><texture min_filter="linear" mag_filter="linear" wrap_s="clamp" wrap_t="clamp">'
                                   '<image name="light_white_key"/><image name="light_black_key"/></texture></texturelist>', encoding="utf-8")
                (library / "library.json").write_text(json.dumps({"status": "complete", "sourceMetadataUnchanged": True,
                    "sourceComposition": {"contract": COMPOSITION, "aliases24": EARLIER_ALIASES}, "textures": textures}), encoding="utf-8")
                frame_map = root / "research/skin-reference-map.json"
                frame_map.parent.mkdir()
                frame_map.write_text(json.dumps(reference), encoding="utf-8")
                contexts = [c for c in material_contexts() if c["layout"] == "dp"]
                result = build_controls(library, frame_map, style=style, layout="dp", contexts=contexts)
                self.assertEqual(family, result["receipt"]["commonSourceFamily"])
                self.assertEqual(16, len(result["templates"]))
                self.assertEqual(14, len(result["bindings"]))
                self.assertEqual([], result["animations"])
                self.assertEqual({"input.pressed"}, {b["source"] for b in result["bindings"]})
                self.assertEqual({"preparation.active"}, {v["source"] for v in result["variants"]})
                self.assertTrue(all(f"/{family}/gameparts/" in f["source"].replace("\\", "/") for f in result["files"][:2]))
                scratch = [t for t in result["templates"] if t["id"].endswith((".g0.k0.template", ".g1.k7.template"))]
                self.assertEqual(2, len(scratch))
                self.assertTrue(all(not t["root"]["children"] for t in scratch))
                self.assertEqual(sum(len(c["lanes"]) for c in contexts), len(result["targets"]))
                for file in result["files"]:
                    self.assertEqual(file["sha256"], hashlib.sha256(Path(file["source"]).read_bytes()).hexdigest())


if __name__ == "__main__":
    unittest.main()
