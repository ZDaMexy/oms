"""Synthetic source22 panel projections, isolation and actual lane-input owners."""
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from standard_controls import build_controls, cabinet_keys, project, quad_for_host
from standard_lane_geometry import source_lane_regions
from standard_source_catalog import COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES, SOURCE22_STYLES, SOURCE22_AVAILABLE_STYLES, package_key


def composition():
    return {"contract": COMPOSITION22, "aliases24": EARLIER_ALIASES, "aliases22": SOURCE22_ALIASES}


def pendual_reference(style, layout):
    """No source pixels/hashes: only the independently established GEO contract."""
    package = package_key(style)
    movie = "center_frame_dp_1p" if layout == "dp" else "2p_frame" if layout == "p2" else "1p_frame"
    domains = [(304, 596), (684, 976)] if layout == "dp" else [(940, 1232)] if layout == "p2" else [(48, 340)]
    draws = []

    def add(name, size, matrix):
        width, height = size
        vertices = [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (width, 0), (0, height), (width, height))]
        points = [project(matrix, v["x"], v["y"]) for v in vertices]
        draw = {"texture": name, "geometry": movie + ", synthetic-" + name, "game_bindings": [],
                "asset": {"package": package, "name": name, "width": width, "height": height,
                          "sprite": f"packages/contents/data/graphic/{package}/sprites/{name}.png"},
                "matrix": matrix, "source_vertices": vertices,
                "bounds": {"left": min(p[0] for p in points), "right": max(p[0] for p in points),
                           "top": min(p[1] for p in points), "bottom": max(p[1] for p in points)}}
        draws.append(draw)
        return draw

    def translate(x, y=0):
        return {"a": 1, "b": 0, "c": 0, "d": 1, "tx": x, "ty": y}

    for group, (left, _) in enumerate(domains):
        right = group == 1 if layout == "dp" else layout == "p2"
        if style != "source22-gmframe9" and not (style == "source22-gmframe8" and layout != "dp"):
            matrix = translate(left)
            matrix.update(d=.95, ty=484 * .05)
            add("sp_lane_2p" if right else "sp_lane_1p", (292, 484), matrix)
        if style in ("source22-gmframe0-00", "source22-gmframe0-01"):
            # The right side is the same original 1P bitmap reflected by its
            # AFP matrix. Declaring a separate 2P resource is the regression.
            matrix = {"a": -1 if right else 1, "b": -.015, "c": -.015 if right else .015,
                      "d": 1, "tx": left + 316 if right else left - 25, "ty": 482}
            add("sp_key_1p", (322, 86), matrix)
        elif style in ("source22-gmframe0", "source22-gmframe10"):
            matrix = {"a": 1, "b": .009 if right else -.009, "c": -.009 if right else .009,
                      "d": 1, "tx": left - 5 if right else left - 25, "ty": 480 if right else 482}
            add("sp_key_2p" if right else "sp_key_1p", (322, 86), matrix)
        elif style == "source22-gmframe8":
            if layout != "dp":
                add("sp_frame_2p" if right else "sp_frame_1p", (356, 720), translate(924 if right else 0))
            elif group == 0:
                add("dp_frame", (1280, 720), translate(0))
        elif style == "source22-gmframe9":
            add("frame_lane_2p" if right else "frame_lane_1p", (354, 609), translate(left - (5 if right else 57)))
    return {"package": package, "common_family": "s22", "stage": {"width": 1280, "height": 720, "fps": 60.0},
            "layouts": {movie: {"draws": draws}}}


class PendualLaneAndControlsTests(unittest.TestCase):
    def test_all_six_sources_keep_fixed_host_planes_separate_from_compressed_or_baked_art(self):
        for style in SOURCE22_STYLES:
            for layout, xs in (("p1", [48]), ("p2", [940]), ("dp", [304, 684])):
                with self.subTest(style=style, layout=layout):
                    reference = pendual_reference(style, layout)
                    self.assertEqual([{"left": x, "right": x + 292, "top": 0, "bottom": 484} for x in xs],
                                     source_lane_regions(reference, style, layout))
                    for group in range(len(xs)):
                        _, _, keys = cabinet_keys(reference, style, layout, group)
                        self.assertEqual(7, len(keys))
                        self.assertTrue(all(k["draw"]["asset"]["package"] == package_key(style) for k in keys))

    def test_present_future_right_input_uses_the_reflected_1p_cap_geometry(self):
        for style in ("source22-gmframe0-00", "source22-gmframe0-01"):
            for layout, group in (("p2", 0), ("dp", 1)):
                with self.subTest(style=style, layout=layout):
                    _, field, keys = cabinet_keys(pendual_reference(style, layout), style, layout, group)
                    self.assertEqual([283, 12, 28, 47], keys[0]["cap"])
                    self.assertEqual([255, 8, 20, 40], keys[1]["cap"])
                    self.assertTrue(all(k["draw"]["texture"] == "sp_key_1p" for k in keys))
                    centres = [project(k["draw"]["matrix"], k["cap"][0] + k["cap"][2] / 2, k["cap"][1])[0] for k in keys]
                    self.assertEqual(sorted(centres), centres)
                    quad = quad_for_host(keys[0], {"width": 50, "height": 69}, [field["left"] + 2, 484, 34, 80])
                    self.assertGreater(quad["vertices"][0][0], quad["vertices"][1][0])
                    self.assertNotEqual(quad["vertices"][0][1], quad["vertices"][1][1], "Preserve source rotation as well as mirror.")

    def test_spada_source_panels_and_new_present_caps_are_not_collapsed_by_dimensions(self):
        for style in ("source22-gmframe0", "source22-gmframe10"):
            left = cabinet_keys(pendual_reference(style, "p1"), style, "p1", 0)[2]
            right = cabinet_keys(pendual_reference(style, "p2"), style, "p2", 0)[2]
            self.assertEqual([128, 7, 18, 40], left[1]["cap"])
            self.assertEqual([46, 7, 20, 40], right[1]["cap"])
            self.assertEqual("sp_key_2p", right[0]["draw"]["texture"])
        present = cabinet_keys(pendual_reference("source22-gmframe0-00", "p1"), "source22-gmframe0-00", "p1", 0)[2]
        self.assertEqual([127, 8, 20, 40], present[1]["cap"])

    def test_lincle_and_tricoro_caps_use_each_own_local_raster_coordinates(self):
        for style in ("source22-gmframe8", "source22-gmframe9"):
            reference = pendual_reference(style, "dp")
            for group, expected in ((0, [385, 517.5]), (1, [703, 517.5])):
                with self.subTest(style=style, group=group):
                    _, _, keys = cabinet_keys(reference, style, "dp", group)
                    cap = keys[0]["cap"]
                    self.assertEqual(expected, project(keys[0]["draw"]["matrix"], cap[0] + cap[2] / 2, cap[1] + cap[3] / 2))
                    self.assertEqual("dp_frame" if style.endswith("8") else "frame_lane_" + ("1p" if group == 0 else "2p"),
                                     keys[0]["draw"]["texture"])

    def test_missing_other_family_and_changed_baked_projection_fail_closed(self):
        for style, layout in (("source22-gmframe0-00", "p1"), ("source22-gmframe8", "p2"), ("source22-gmframe9", "dp")):
            for mutation in ("missing", "wrong-package", "wrong-vertices", "moved"):
                with self.subTest(style=style, mutation=mutation):
                    reference = pendual_reference(style, layout)
                    draws = next(iter(reference["layouts"].values()))["draws"]
                    lane = next(d for d in draws if d["texture"].startswith(("sp_lane", "sp_frame", "frame_lane")))
                    if mutation == "missing":
                        draws.remove(lane)
                    elif mutation == "wrong-package":
                        lane["asset"]["package"] = "gmframe26"
                    elif mutation == "wrong-vertices":
                        lane["source_vertices"][0]["x"] += 1
                    else:
                        lane["matrix"]["tx"] += 1
                    with self.assertRaises(ValueError):
                        source_lane_regions(reference, style, layout)
        with self.assertRaises(ValueError):
            source_lane_regions(pendual_reference("source22-gmframe8", "p1"), "source22-gmframe1", "p1")

    def test_materials_bind_actual_input_and_only_own_s22_lights(self):
        for style in SOURCE22_AVAILABLE_STYLES:
            with self.subTest(style=style), tempfile.TemporaryDirectory(prefix="pendual-controls-") as directory:
                root = Path(directory).resolve()
                library = root / "library"
                reference = pendual_reference(style, "dp")
                textures, by_key = [], {}

                def add(package, name, size):
                    if (package, name) in by_key:
                        return by_key[package, name]
                    relative = f"packages/contents/data/graphic/{package}/sprites/{name}.png"
                    path = library / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(("synthetic original source " + package + "/" + name).encode())
                    row = {"package": "contents/data/graphic/" + package + ".ifs", "name": name + ".png",
                           "width": size[0], "height": size[1], "sprite": relative,
                           "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
                    textures.append(row)
                    by_key[package, name] = row
                    return row

                for draw in reference["layouts"]["center_frame_dp_1p"]["draws"]:
                    asset = draw["asset"]
                    row = add(asset["package"], asset["name"], (asset["width"], asset["height"]))
                    asset["sha256"] = row["sha256"]
                for colour, size in (("white", (50, 69)), ("black", (42, 62))):
                    add("s22/gameparts", "light_" + colour + "_key", size)
                    add("s24/gameparts", "light_" + colour + "_key", size)  # Later-source decoy.
                sampler = library / "packages/contents/data/graphic/s22/gameparts/raw/tex/texturelist.xml"
                sampler.parent.mkdir(parents=True)
                sampler.write_text('<texturelist><texture min_filter="linear" mag_filter="linear" wrap_s="clamp" wrap_t="clamp">'
                                   '<image name="light_white_key"/><image name="light_black_key"/></texture></texturelist>', encoding="utf-8")
                (library / "library.json").write_text(json.dumps({"status": "complete", "sourceMetadataUnchanged": True,
                    "sourceComposition": composition(), "textures": textures}), encoding="utf-8")
                frame_map = root / "map.json"
                frame_map.write_text(json.dumps(reference), encoding="utf-8")
                contexts = [c for c in material_contexts() if c["layout"] == "dp"]
                result = build_controls(library, frame_map, style=style, layout="dp", contexts=contexts)
                self.assertEqual("s22", result["receipt"]["commonSourceFamily"])
                self.assertEqual(16, len(result["templates"]))
                self.assertEqual(14, len(result["bindings"]))
                self.assertEqual({"input.pressed"}, {b["source"] for b in result["bindings"]})
                self.assertEqual([], result["animations"])
                self.assertTrue(all("/s22/gameparts/" in f["source"].replace("\\", "/") for f in result["files"][:2]))
                scratch = [t for t in result["templates"] if t["id"].endswith((".g0.k0.template", ".g1.k7.template"))]
                self.assertEqual(2, len(scratch))
                self.assertTrue(all(not t["root"]["children"] for t in scratch))
                self.assertTrue(all("f22-gmframe" in t["id"] for t in result["templates"]))
                for file in result["files"]:
                    self.assertEqual(file["sha256"], hashlib.sha256(Path(file["source"]).read_bytes()).hexdigest())


if __name__ == "__main__":
    unittest.main()
