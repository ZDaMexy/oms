"""Synthetic 22 lane partitions: no original image, archive or private hash."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_presentation_options import (export_lane_layers, lane_roots, partition_baked_lane_clip,
                                           partition_clip, pendual_baked_surfaces, split_geometry_rectangle)
from standard_source_catalog import COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES, SOURCE22_STYLES, package_key


def fixture(style="source22-gmframe9", movie="center_frame_dp_1p"):
    package = package_key(style)
    domains = [(304, 596), (684, 976)] if movie == "center_frame_dp_1p" else [(940, 1232)] if movie == "2p_frame" else [(48, 340)]
    baked = style == "source22-gmframe9" or style == "source22-gmframe8" and movie != "center_frame_dp_1p"
    state = {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], "origin": [0, 0, 0],
             "projection": 1, "mult": [.8, .7, .6, .9], "add": [.01, .02, .03, .04],
             "hsl": [.02, -.03, .04], "mask": [0, 0, 1280, 720], "visible": True}
    source = {"package": package, "sourceMovie": movie, "nodes": [], "states": [state], "frames": []}
    source_geometry = {"geometries": [], "filters": []}
    layout = {"package": package, "draws": [], "dynamic_instances": []}
    nodes, geometries = [], []
    panels, parents, opening_states, steady_states = [], [], {}, {}

    def add_node(parent, geometry=None, shape="container"):
        identifier = len(nodes)
        source["nodes"].append({"id": identifier, "parent": parent, "geometry": geometry, "source": shape,
                                "sourceTag": identifier + 100, "sourceObject": identifier + 200, "depth": identifier,
                                "kind": "container" if geometry is None else "mesh"})
        nodes.append({"parent": parent, "geometry": -1 if geometry is None else geometry, "blend": "normal"})
        return identifier

    def add_geometry(name, width, height):
        vertices = [[0, 0, 0], [width, 0, 0], [0, height, 0], [width, height, 0]]
        # The fourth UV deliberately makes this piecewise affine, not one
        # affine quad. Replacing it by rectangular UV crops would change art.
        uv = [[.02, .03], [.98, .05], [.04, .97], [.91, .95]]
        shape = movie + "_shape" + str(len(geometries))
        draw = {"texture": len(geometries), "uv": uv, "indices": [0, 1, 2, 2, 1, 3], "flags": 65, "sourceColour": None}
        source_geometry["geometries"].append({"source": shape, "vertices": vertices, "colours": [], "draws": [draw]})
        geometries.append({"resource": name, "vertices": [[*p, *t] for p, t in zip(vertices, uv)], "triangles": draw["indices"]})
        return len(geometries) - 1, shape

    root = add_node(-1)
    ornament, ornament_shape = add_geometry("ornament", 5, 5)
    add_node(root, ornament, ornament_shape)
    for group, (left, _) in enumerate(domains):
        right = group == 1 if movie == "center_frame_dp_1p" else movie == "2p_frame"
        player = "2p" if right else "1p"
        if style == "source22-gmframe9":
            name, width, height, local_left = "frame_lane_" + player, 354, 609, 5 if right else 57
        elif baked:
            name, width, height, local_left = "sp_frame_" + player, 356, 720, 16 if right else 48
        else:
            name, width, height, local_left = "sp_lane_" + player, 292, 484, 0
        geometry, shape = add_geometry(name, width, height)
        parent = add_node(root)
        panel = add_node(parent, geometry, shape)
        parents.append(parent)
        panels.append(panel)
        matrix = {"a": 1, "b": 0, "c": 0, "d": 1 if baked else .95,
                  "tx": left - local_left, "ty": 0 if baked else 484 * .05}
        layout["draws"].append({"texture": name, "geometry": shape + ", " + name, "game_bindings": [],
            "asset": {"package": package, "name": name, "width": width, "height": height},
            "source_vertices": [{"x": p[0], "y": p[1], "z": p[2]} for p in source_geometry["geometries"][geometry]["vertices"]],
            "matrix": matrix, "bounds": {"left": matrix["tx"], "top": matrix["ty"],
                                          "right": matrix["tx"] + width, "bottom": matrix["ty"] + height * matrix["d"]},
            "draw_params": [{"region": name, "vertexes": [0, 1, 2, 2, 1, 3]}],
            "layer_path": [{"depth": n, "tag_id": n + 100} for n in (root, parent, panel)]})
        for field, d, y in ((opening_states, .5, 242), (steady_states, matrix["d"], matrix["ty"])):
            field[panel] = len(source["states"])
            source["states"].append({**deepcopy(state), "matrix": [1, 0, 0, 0, d, 0, 0, 0, 1, matrix["tx"], y, 0],
                                     "origin": [1, 2, 0], "mask": [0, 0, width, height], "projection": 2})
        add_node(root, ornament, ornament_shape)
    hidden_state = len(source["states"])
    source["states"].append({**deepcopy(state), "visible": False})
    entries = [[n, steady_states.get(n, 0)] for n in range(len(nodes))]
    source["frames"] = [
        {"nodes": [p for p in entries if p[0] not in panels + parents], "camera": 0},
        {"nodes": [[n, opening_states.get(n, 0)] for n in range(len(nodes))], "camera": 0},
        {"nodes": entries, "camera": 0},
        {"nodes": [[n, hidden_state if n == parents[0] else s] for n, s in entries], "camera": 0},
        {"nodes": [], "camera": None}]
    colour_matrix = [1, 0, 0, 0, .05, 0, .9, 0, 0, .02, 0, 0, .8, 0, 0, 0, 0, 0, 1, 0]
    clip = {"contract": "oms-gameplay-recorded-animation.v1", "width": 1280, "height": 720, "fps": 60,
            "loop": True, "loopStartFrame": 2, "trigger": "gameplay.attach", "nodes": nodes, "geometries": geometries,
            "states": [{"matrix": s["matrix"], "origin": s["origin"], "projection": "perspective" if s["projection"] == 2 else "affine",
                        "multiply": s["mult"], "add": s["add"], "hsl": s["hsl"], "mask": s["mask"],
                        "colourMatrix": deepcopy(colour_matrix)} for s in source["states"]],
            "frames": [{"nodes": deepcopy(f["nodes"]), "camera": [640, 360, 500] if f["nodes"] else None} for f in source["frames"]]}
    clip["frames"][3]["nodes"] = [p for p in clip["frames"][3]["nodes"] if p[0] not in (parents[0], panels[0])]
    return source, layout, clip, source_geometry


def chains(clip, frame_index):
    result = {}
    for n, s in clip["frames"][frame_index]["nodes"]:
        node = clip["nodes"][n]
        result[n] = (result[node["parent"]] if node["parent"] >= 0 else []) + [(node["blend"], clip["states"][s])]
    return result


def samples_at(geometry, x, y):
    """Independent barycentric interpolation for the test's original surface."""
    values = []
    for offset in range(0, len(geometry["triangles"]), 3):
        a, b, c = [geometry["vertices"][n] for n in geometry["triangles"][offset:offset + 3]]
        denominator = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
        if abs(denominator) < 1e-12:
            continue
        u = ((b[1] - c[1]) * (x - c[0]) + (c[0] - b[0]) * (y - c[1])) / denominator
        v = ((c[1] - a[1]) * (x - c[0]) + (a[0] - c[0]) * (y - c[1])) / denominator
        w = 1 - u - v
        if min(u, v, w) >= -1e-10:
            values.append([u * a[i] + v * b[i] + w * c[i] for i in range(2, 5)])
    return values


class PendualPresentationTests(unittest.TestCase):
    def test_all_six_sources_resolve_their_own_independent_or_baked_geometry(self):
        for style in SOURCE22_STYLES:
            for movie in ("1p_frame", "2p_frame", "center_frame_dp_1p"):
                with self.subTest(style=style, movie=movie):
                    source, layout, clip, geometry = fixture(style, movie)
                    baked = pendual_baked_surfaces(source, layout, style)
                    expected_baked = style == "source22-gmframe9" or style == "source22-gmframe8" and movie != "center_frame_dp_1p"
                    self.assertEqual(expected_baked, bool(baked))
                    if baked:
                        with self.assertRaisesRegex(ValueError, "not whole-panel"):
                            lane_roots(source, layout, style=style)
                        layers, _ = partition_baked_lane_clip(source, clip, baked, geometry, range(5))
                    else:
                        roots = lane_roots(source, layout, style=style)
                        self.assertEqual(2 if movie == "center_frame_dp_1p" else 1, len(roots))
                        layers = partition_clip(source, clip, roots, range(5))
                    self.assertEqual(2 if movie == "center_frame_dp_1p" else 1, sum(l["kind"] == "lane" for l in layers))

    def test_embedded_triangle_partition_has_no_hole_overlap_or_uv_reprojection(self):
        source, layout, clip, _ = fixture()
        surfaces = pendual_baked_surfaces(source, layout, "source22-gmframe9")
        for surface in surfaces.values():
            original = clip["geometries"][surface["geometry"]]
            left, top, right, bottom = surface["rectangle"]
            pieces, proof = split_geometry_rectangle(original, surface["rectangle"])
            self.assertAlmostEqual(292 * 484, proof["insideArea"])
            self.assertAlmostEqual(354 * 609, proof["insideArea"] + proof["outsideArea"])
            for x in (.25, left - .25, left + .25, (left + right) / 2 + .125, right - .25, right + .25, 353.75):
                for y in (.25, 147.125, 483.75, 484.25, 608.75):
                    expected_kind = "lane" if left < x < right and top < y < bottom else "frame"
                    original_samples = samples_at(original, x, y)
                    self.assertTrue(original_samples)
                    for kind, value in pieces.items():
                        actual = samples_at(value, x, y)
                        self.assertEqual(kind == expected_kind, bool(actual))
                        for sample in actual:
                            for got, expected in zip(sample, original_samples[0]):
                                self.assertAlmostEqual(expected, got, places=11)

    def test_every_frame_retains_leaf_order_hierarchy_colours_masks_camera_and_loop(self):
        source, layout, clip, geometry = fixture()
        original_source, original_clip = deepcopy(source), deepcopy(clip)
        surfaces = pendual_baked_surfaces(source, layout, "source22-gmframe9")
        layers, proof = partition_baked_lane_clip(source, clip, surfaces, geometry, range(5))
        targets = {s["node"] for s in surfaces.values()}
        for frame_index, frame in enumerate(clip["frames"]):
            expected = [n for n, _ in frame["nodes"] if clip["nodes"][n]["geometry"] >= 0 for _ in range(2 if n in targets else 1)]
            actual, original_chains = [], chains(clip, frame_index)
            for layer in layers:
                result, result_chains = layer["clip"], chains(layer["clip"], frame_index)
                self.assertEqual(frame["camera"], result["frames"][frame_index]["camera"])
                for n, _ in result["frames"][frame_index]["nodes"]:
                    original = layer["originalNodeIds"][n]
                    self.assertEqual(original_chains[original], result_chains[n])
                    if result["nodes"][n]["geometry"] >= 0:
                        actual.append(original)
                        if original not in targets:
                            self.assertEqual(clip["geometries"][clip["nodes"][original]["geometry"]],
                                             result["geometries"][result["nodes"][n]["geometry"]])
            self.assertEqual(expected, actual)
        for layer in layers:
            self.assertEqual(5, len(layer["clip"]["frames"]))
            for field in ("fps", "width", "height", "loop", "loopStartFrame", "trigger"):
                self.assertEqual(clip[field], layer["clip"][field])
        self.assertEqual([], layers[1]["clip"]["frames"][3]["nodes"])
        self.assertTrue(proof["noPngDecodeOrPixelModification"])
        self.assertEqual(original_source, source)
        self.assertEqual(original_clip, clip)

    def test_wrong_source_or_surface_cannot_brighten_a_neighbouring_cabinet(self):
        for mutation in ("family", "texture", "path", "vertices", "triangles", "child", "named", "duplicate"):
            with self.subTest(mutation=mutation):
                source, layout, _, _ = fixture()
                if mutation == "family":
                    layout["draws"][0]["asset"]["package"] = "gmframe26"
                elif mutation == "texture":
                    layout["draws"][0]["texture"] = "frame_sp_panel_1p"
                elif mutation == "path":
                    layout["draws"][0]["layer_path"][-1]["depth"] += 10
                elif mutation == "vertices":
                    layout["draws"][0]["source_vertices"][3]["x"] += 1
                elif mutation == "triangles":
                    layout["draws"][0]["draw_params"][0]["vertexes"] = [0, 1, 2]
                elif mutation == "child":
                    source["nodes"][4]["parent"] = 3
                elif mutation == "named":
                    layout["dynamic_instances"] = [{"name": "lane_1p"}]
                else:
                    layout["draws"].append(deepcopy(layout["draws"][0]))
                with self.assertRaises(ValueError):
                    pendual_baked_surfaces(source, layout, "source22-gmframe9")

    def test_recorded_uv_or_transform_drift_fails_before_emitting_layers(self):
        for mutation in ("uv", "triangles", "matrix", "parent", "source-geometry"):
            with self.subTest(mutation=mutation):
                source, layout, clip, geometry = fixture()
                surfaces = pendual_baked_surfaces(source, layout, "source22-gmframe9")
                if mutation == "uv":
                    clip["geometries"][1]["vertices"][0][3] += .1
                elif mutation == "triangles":
                    clip["geometries"][1]["triangles"] = [0, 1, 2]
                elif mutation == "matrix":
                    clip["states"][1]["matrix"] = [0] * 12
                elif mutation == "parent":
                    clip["nodes"][3]["parent"] = 0
                else:
                    geometry["geometries"][1]["source"] = "another_shape"
                with self.assertRaises(ValueError):
                    partition_baked_lane_clip(source, clip, surfaces, geometry, range(5))

    def test_export_binds_only_inner_geometry_and_steady_reuses_the_same_source_frame(self):
        import brotli
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            library, research, output = root / "library", root / "research", root / "output"
            library.mkdir()

            def save(path, value):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps(value), encoding="utf-8")

            save(library / "library.json", {"status": "complete", "sourceMetadataUnchanged": True,
                 "sourceComposition": {"contract": COMPOSITION22, "aliases24": EARLIER_ALIASES, "aliases22": SOURCE22_ALIASES}})
            style, movie = "source22-gmframe9", "center_frame_dp_1p"
            source, layout, clip, geometry = fixture(style, movie)
            cabinet = research / package_key(style)
            recorded = cabinet / "recorded" / movie
            save(cabinet / "skin-reference-map.json", {"package": package_key(style), "common_family": "s22", "layouts": {movie: layout}})
            save(recorded / "source-root.json", source)
            save(recorded / "source-geometry.json", geometry)
            files, resources = [], []
            for value in clip["geometries"]:
                name = value["resource"]
                path = library / (name + ".png")
                path.write_bytes(("synthetic pixel identity " + name).encode())
                resources.append({"id": name, "type": "texture", "path": "test/" + path.name})
                files.append({"source": str(path), "path": "test/" + path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
            exported = {"source": {"package": package_key(style), "movie": movie, "steadyFrameZeroBased": 2, "fps": 60}}
            for mode in ("motion", "steady"):
                value = deepcopy(clip)
                if mode == "steady":
                    value["frames"] = [value["frames"][2], value["frames"][-1]]
                    value["loopStartFrame"] = 0
                path = recorded / (mode + ".clip.json.br")
                path.write_bytes(brotli.compress(json.dumps(value).encode()))
                animation = "test." + mode
                exported[mode] = {"node": {"id": animation + ".node", "animation": animation, "blend": "alpha"},
                    "animations": [{"id": animation, "path": "test/" + path.name}], "resources": resources,
                    "files": [*files, {"source": str(path), "path": "test/" + path.name, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}]}
            save(recorded / "export.json", exported)
            result = export_lane_layers(library, research, style, movie, output)
            self.assertEqual(["lane-brightness"], [o["id"] for o in result["options"]])
            self.assertNotIn("nativeColourCallerRVA", result["receipt"])
            self.assertNotIn("nativeMultiplierRVA", result["receipt"])
            self.assertIn("not evidence", result["receipt"]["brightnessEvidence"])
            self.assertEqual(hashlib.sha256((recorded / "source-geometry.json").read_bytes()).hexdigest(), result["receipt"]["sourceGeometrySHA256"])
            for mode in ("motion", "steady"):
                for layer in result[mode]["layers"]:
                    self.assertEqual(layer["kind"] == "lane", bool(layer["bindings"]))
                    for file in layer["files"]:
                        self.assertEqual(file["sha256"], hashlib.sha256(Path(file["source"]).read_bytes()).hexdigest())

            def read_layer(layer):
                animation, = layer["animations"]
                path, = (f["source"] for f in layer["files"] if f["path"] == animation["path"])
                return json.loads(brotli.decompress(Path(path).read_bytes()))

            def frame_draws(value, frame):
                tree = chains(value, frame)
                return [(tree[n], value["geometries"][value["nodes"][n]["geometry"]])
                        for n, _ in value["frames"][frame]["nodes"] if value["nodes"][n]["geometry"] >= 0]

            self.assertEqual([l["kind"] for l in result["motion"]["layers"]], [l["kind"] for l in result["steady"]["layers"]])
            for dynamic, held in zip(result["motion"]["layers"], result["steady"]["layers"]):
                self.assertEqual(frame_draws(read_layer(dynamic), 2), frame_draws(read_layer(held), 0))


if __name__ == "__main__":
    unittest.main()
