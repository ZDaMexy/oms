"""Synthetic 23/24 GEO identity and lossless brightness-layer regressions."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_presentation_options import export_lane_layers, lane_roots, partition_clip
from standard_source_catalog import COMPOSITION, EARLIER_ALIASES


def fixture(style=24, movie="1p_frame"):
    textures = (["sp_lane_1p", "sp_lane_2p"] if movie == "center_frame_dp_1p" else
                ["sp_lane_2p" if movie == "2p_frame" and style == 24 else "sp_lane_1p"])
    state = {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], "origin": [0, 0, 0],
             "projection": 1, "mult": [.8, .7, .6, 1], "add": [0, 0, 0, 0],
             "hsl": [0, 0, 0], "mask": [0, 0, 1280, 720], "visible": True}
    opening = deepcopy(state)
    opening.update(matrix=[1, 0, 0, 0, .5, 0, 0, 0, 1, 194, 484, 0], origin=[146, 484, 0], mask=None)
    steady = deepcopy(opening)
    steady["matrix"][4] = .9375
    source = {"package": f"gmframe{style}", "sourceMovie": movie,
              "nodes": [{"id": 0, "parent": -1, "source": "anonymous sprite", "sourceTag": None,
                         "sourceObject": -1, "depth": -1, "geometry": None, "kind": "container"}],
              "states": [state, opening, steady], "frames": []}
    layout = {"package": source["package"], "dynamic_instances": [], "draws": []}
    geometry = [{"resource": "frame", "vertices": [[0, 0, 0, 0, 0], [1, 0, 0, 1, 0], [0, 1, 0, 0, 1]],
                 "triangles": [0, 1, 2]}]
    public_nodes = [{"parent": -1, "geometry": -1, "blend": "normal"}]
    for identifier in range(1, len(textures) * 2 + 2):
        is_lane = identifier % 2 == 0
        shape = f"{movie}_shape{100 + identifier}"
        source["nodes"].append({"id": identifier, "parent": 0, "source": shape,
                                "sourceTag": 100 + identifier, "sourceObject": 300, "depth": identifier,
                                "geometry": identifier + 20, "kind": "mesh"})
        if is_lane:
            texture = textures[identifier // 2 - 1]
            vertices = [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (292, 0), (0, 484), (292, 484))]
            layout["draws"].append({"texture": texture, "geometry": shape + ", " + texture,
                                    "asset": {"package": source["package"], "name": texture, "width": 292, "height": 484},
                                    "source_vertices": vertices, "game_bindings": [],
                                    "draw_params": [{"region": texture, "vertexes": [0, 1, 2, 2, 1, 3]}],
                                    "layer_path": [{"depth": -1, "tag_id": None}, {"depth": identifier, "tag_id": 100 + identifier}]})
            geometry.append({"resource": texture, "vertices": [[v["x"], v["y"], 0, (i % 2), (i // 2)]
                                                                    for i, v in enumerate(vertices)],
                             "triangles": [0, 1, 2, 2, 1, 3]})
        public_nodes.append({"parent": 0, "geometry": identifier // 2 if is_lane else 0, "blend": "normal"})
    # A later frame ornament deliberately lies between the two DP surfaces.
    # The opening also has no lane meshes at all, as the real 24 movie starts.
    all_nodes = range(len(source["nodes"]))
    source["frames"] = [
        {"nodes": [[n, 0] for n in all_nodes if n == 0 or n % 2], "camera": None},
        {"nodes": [[n, 1 if n and n % 2 == 0 else 0] for n in all_nodes], "camera": None},
        {"nodes": [[n, 2 if n and n % 2 == 0 else 0] for n in all_nodes], "camera": None},
        {"nodes": [], "camera": None}]
    clip = {"contract": "oms-gameplay-recorded-animation.v1", "width": 1280, "height": 720, "fps": 60,
            "loop": True, "loopStartFrame": 1, "trigger": "gameplay.attach", "geometries": geometry,
            "nodes": public_nodes, "frames": deepcopy(source["frames"]),
            "states": [{"matrix": s["matrix"], "origin": s["origin"], "projection": "affine",
                        "multiply": s["mult"], "add": s["add"], "hsl": s["hsl"], "mask": s["mask"],
                        "colourMatrix": None} for s in source["states"]]}
    return source, layout, clip


class EarlierPresentationTests(unittest.TestCase):
    def test_six_source_placements_resolve_without_named_lane_hooks(self):
        for style in (23, 24):
            for movie, expected in (("1p_frame", {"lane_1p": 2}), ("2p_frame", {"lane_2p": 2}),
                                    ("center_frame_dp_1p", {"lane_1p": 2, "lane_2p": 4})):
                with self.subTest(style=style, movie=movie):
                    source, layout, _ = fixture(style, movie)
                    self.assertEqual(expected, lane_roots(source, layout, style=style))
                    self.assertEqual({}, lane_roots(source, layout, style=26))

    def test_copula_p2_uses_its_shared_original_lane_texture(self):
        source, layout, _ = fixture(23, "2p_frame")
        self.assertEqual("sp_lane_1p", layout["draws"][0]["texture"])
        self.assertEqual({"lane_2p": 2}, lane_roots(source, layout, style=23))
        with self.assertRaises(ValueError):
            lane_roots(source, layout, style=24)

    def test_missing_duplicate_or_other_package_lane_is_rejected(self):
        for mutation in ("missing", "duplicate", "other-package", "wrong-vertices", "named-hook"):
            with self.subTest(mutation=mutation):
                source, layout, _ = fixture()
                if mutation == "missing":
                    layout["draws"][0]["texture"] = "lane_bg_sp"
                elif mutation == "duplicate":
                    layout["draws"].append(deepcopy(layout["draws"][0]))
                elif mutation == "other-package":
                    layout["draws"][0]["asset"]["package"] = "gmframe26"
                elif mutation == "wrong-vertices":
                    layout["draws"][0]["source_vertices"][3]["y"] = 636
                else:
                    layout["dynamic_instances"].append({"name": "lane_1p"})
                with self.assertRaises(ValueError):
                    lane_roots(source, layout, style=24)

    def test_shape_name_alone_cannot_select_another_placement_or_a_parent_shell(self):
        for mutation in ("wrong-path", "ambiguous", "has-child", "container"):
            with self.subTest(mutation=mutation):
                source, layout, _ = fixture()
                if mutation == "wrong-path":
                    layout["draws"][0]["layer_path"][-1]["depth"] += 1
                elif mutation == "ambiguous":
                    source["nodes"].append({**source["nodes"][2], "id": 4})
                elif mutation == "has-child":
                    source["nodes"][3]["parent"] = 2
                else:
                    source["nodes"][2].update(kind="container", geometry=None)
                with self.assertRaisesRegex(ValueError, "one original leaf mesh"):
                    lane_roots(source, layout, style=24)

    def test_dp_partition_preserves_all_frames_uv_masks_and_interleaved_shells(self):
        source, layout, clip = fixture(24, "center_frame_dp_1p")
        original = deepcopy(clip)
        layers = partition_clip(source, clip, lane_roots(source, layout, style=24), range(4))
        self.assertEqual(["frame", "lane", "frame", "lane", "frame"], [r["kind"] for r in layers])
        for index, frame in enumerate(clip["frames"]):
            expected = [n for n, _ in frame["nodes"] if clip["nodes"][n]["geometry"] >= 0]
            actual = [layer["originalNodeIds"][n] for layer in layers
                      for n, _ in layer["clip"]["frames"][index]["nodes"] if layer["clip"]["nodes"][n]["geometry"] >= 0]
            self.assertEqual(expected, actual)
        for layer in layers:
            self.assertEqual(1, layer["clip"]["loopStartFrame"])
            self.assertEqual(4, len(layer["clip"]["frames"]))
            self.assertEqual([clip["geometries"][g] for g in layer["originalGeometryIds"]], layer["clip"]["geometries"])
            self.assertTrue(any(s["mask"] == [0, 0, 1280, 720] for s in layer["clip"]["states"]))
            resources = {g["resource"] for g in layer["clip"]["geometries"]}
            self.assertEqual(resources <= {"sp_lane_1p", "sp_lane_2p"}, layer["kind"] == "lane")
        self.assertEqual([], layers[1]["clip"]["frames"][0]["nodes"])
        self.assertEqual(original, clip)

    def test_export_binds_only_lane_layers_and_does_not_claim_26_native_evidence(self):
        import brotli

        with tempfile.TemporaryDirectory(prefix="earlier-presentation-") as directory:
            root = Path(directory).resolve()
            library, research, output = root / "library", root / "research", root / "output"

            def save(path, value):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(json.dumps(value), encoding="utf-8")

            save(library / "library.json", {"status": "complete", "sourceMetadataUnchanged": True,
                 "sourceComposition": {"contract": COMPOSITION, "aliases24": EARLIER_ALIASES}})
            source, layout, clip = fixture()
            folder = research / "gmframe24/recorded/1p_frame"
            save(research / "gmframe24/skin-reference-map.json",
                 {"package": "gmframe24", "common_family": "s24", "layouts": {"1p_frame": layout}})
            save(folder / "source-root.json", source)
            files, resources = [], []
            for name in ("frame", "sp_lane_1p"):
                path = library / (name + ".png")
                path.write_bytes(("synthetic unchanged texture " + name).encode())
                resources.append({"id": name, "type": "texture", "path": "test/" + path.name})
                files.append({"source": str(path), "path": "test/" + path.name,
                              "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
            exported = {"source": {"package": "gmframe24", "movie": "1p_frame", "steadyFrameZeroBased": 2}}
            for mode in ("motion", "steady"):
                value = deepcopy(clip)
                if mode == "steady":
                    value["frames"] = [value["frames"][2], value["frames"][3]]
                    value["loopStartFrame"] = 0
                path = folder / (mode + ".clip.json.br")
                path.write_bytes(brotli.compress(json.dumps(value).encode()))
                animation = "test." + mode
                exported[mode] = {"node": {"id": animation + ".node", "animation": animation, "blend": "alpha"},
                                  "animations": [{"id": animation, "path": "test/" + path.name}],
                                  "resources": resources, "files": [*files, {"source": str(path), "path": "test/" + path.name,
                                                                           "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}]}
            save(folder / "export.json", exported)
            result = export_lane_layers(library, research, 24, "1p_frame", output)
            self.assertEqual({"lane_1p": 2}, result["receipt"]["laneRoots"])
            self.assertNotIn("nativeColourCallerRVA", result["receipt"])
            self.assertNotIn("nativeMultiplierRVA", result["receipt"])
            self.assertIn("not evidence", result["receipt"]["brightnessEvidence"])
            for mode in ("motion", "steady"):
                self.assertEqual(["frame", "lane", "frame"], [layer["kind"] for layer in result[mode]["layers"]])
                for layer in result[mode]["layers"]:
                    self.assertEqual(layer["kind"] == "lane", bool(layer["bindings"]))
                    if layer["bindings"]:
                        self.assertEqual("brightness", layer["bindings"][0]["property"])
                        self.assertEqual("option.lane-brightness", layer["bindings"][0]["source"])
                    for file in layer["files"]:
                        self.assertEqual(file["sha256"], hashlib.sha256(Path(file["source"]).read_bytes()).hexdigest())


if __name__ == "__main__":
    unittest.main()
