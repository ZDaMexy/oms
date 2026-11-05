"""Lossless lane-brightness partition regressions using synthetic source data."""
from copy import deepcopy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_presentation_options import lane_roots, partition_clip


def fixture():
    state = {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 13, 7, 0], "origin": [0, 0, 0],
             "projection": 1, "mult": [.8, .7, .6, .5], "add": [.1, 0, 0, 0],
             "hsl": [0, 0, 0], "mask": None, "filters": [], "visible": True}
    source = {"nodes": [], "states": [state, {**state, "mask": [2, 3, 80, 90]},
                                         {**state, "mask": [5, 6, 50, 60]}], "frames": []}
    for identifier, parent, geometry in ((0, -1, None), (1, 0, 0), (2, 0, None), (3, 2, 1),
                                         (4, 0, 0), (5, 0, None), (6, 5, 1), (7, 0, 0)):
        source["nodes"].append({"id": identifier, "parent": parent, "geometry": geometry,
                                 "sourceTag": identifier + 100, "sourceObject": identifier + 200,
                                 "depth": identifier})
    source["frames"] = [{"nodes": [[n, 1 if n == 2 else 2 if n == 5 else 0] for n in ns], "camera": None}
                        for ns in (range(8), [0, 1, 2, 3, 5, 6, 7], [])]
    clip = {"contract": "oms-gameplay-recorded-animation.v1", "width": 1280, "height": 720, "fps": 60,
            "loop": True, "loopStartFrame": 1, "trigger": "gameplay.attach",
            "nodes": [{"parent": n["parent"], "geometry": -1 if n["geometry"] is None else n["geometry"],
                        "blend": "normal"} for n in source["nodes"]],
            "geometries": [{"resource": name, "vertices": [[0, 0, 0, 0, 0], [1, 0, 0, 1, 0], [0, 1, 0, 0, 1]],
                            "triangles": [0, 1, 2]} for name in ("frame", "lane", "unused")],
            "states": [{"matrix": s["matrix"], "origin": s["origin"], "projection": "affine",
                        "multiply": s["mult"], "add": s["add"], "hsl": s["hsl"], "mask": s["mask"],
                        "colourMatrix": None} for s in source["states"]],
            "frames": deepcopy(source["frames"])}
    clip["states"].append({"unused": True})
    return source, clip


class StandardPresentationOptionsTests(unittest.TestCase):
    def test_interleaved_dp_keeps_draw_order_masks_and_original_loop(self):
        source, clip = fixture()
        layers = partition_clip(source, clip, {"lane_1p": 2, "lane_2p": 5}, [0, 1, 2])
        self.assertEqual(["frame", "lane", "frame", "lane", "frame"], [layer["kind"] for layer in layers])
        for frame_index, expected in ((0, [1, 3, 4, 6, 7]), (1, [1, 3, 6, 7]), (2, [])):
            actual = [layer["originalNodeIds"][n] for layer in layers for n, _ in layer["clip"]["frames"][frame_index]["nodes"]
                      if layer["clip"]["nodes"][n]["geometry"] >= 0]
            self.assertEqual(expected, actual)
        for layer, mask in ((layers[1], [2, 3, 80, 90]), (layers[3], [5, 6, 50, 60])):
            self.assertTrue(any(s["mask"] == mask for s in layer["clip"]["states"]))
            self.assertTrue(all(s["matrix"] == source["states"][0]["matrix"] for s in layer["clip"]["states"]))
        for layer in layers:
            self.assertEqual(1, layer["clip"]["loopStartFrame"])
            self.assertEqual(3, len(layer["clip"]["frames"]))
            self.assertEqual(1, len(layer["clip"]["geometries"]))
            self.assertFalse(any("unused" in s for s in layer["clip"]["states"]))

    def test_adjacent_lane_layers_merge_only_when_no_frame_mesh_ever_intervenes(self):
        source, clip = fixture()
        source["frames"][0]["nodes"] = [p for p in source["frames"][0]["nodes"] if p[0] != 4]
        clip["frames"][0]["nodes"] = deepcopy(source["frames"][0]["nodes"])
        layers = partition_clip(source, clip, {"lane_1p": 2, "lane_2p": 5}, [0, 1, 2])
        self.assertEqual(["frame", "lane", "frame"], [layer["kind"] for layer in layers])
        self.assertEqual([1, 3], layers[1]["sourceLayers"])

    def test_invisible_lane_ancestor_does_not_resurrect_its_mesh(self):
        source, clip = fixture()
        source["states"].append({**source["states"][1], "visible": False})
        source["frames"][1]["nodes"] = [[n, 3 if n == 2 else s] for n, s in source["frames"][1]["nodes"]]
        clip["frames"][1]["nodes"] = [p for p in clip["frames"][1]["nodes"] if p[0] not in (2, 3)]
        layers = partition_clip(source, clip, {"lane_1p": 2, "lane_2p": 5}, [0, 1, 2])
        self.assertEqual([], layers[1]["clip"]["frames"][1]["nodes"])

    def test_transform_mismatch_or_exchange_of_lane_order_is_rejected(self):
        source, clip = fixture()
        clip["states"][0]["matrix"] = [0] * 12
        with self.assertRaisesRegex(ValueError, "transform"):
            partition_clip(source, clip, {"lane_1p": 2, "lane_2p": 5}, [0, 1, 2])
        source, clip = fixture()
        source["frames"][1]["nodes"] = [[n, 0] for n in (0, 1, 5, 6, 2, 3, 7)]
        with self.assertRaisesRegex(ValueError, "exchange"):
            partition_clip(source, clip, {"lane_1p": 2, "lane_2p": 5}, [0, 1, 2])

    def test_lane_identity_uses_named_source_placement_not_texture_bounds(self):
        source, _ = fixture()
        hook = {"name": "lane_1p", "source_tag": 102, "object_id": 202, "depth": 2}
        self.assertEqual({"lane_1p": 2}, lane_roots(source, {"dynamic_instances": [hook]}))
        source["nodes"].append({**source["nodes"][2], "id": 8})
        with self.assertRaisesRegex(ValueError, "one original"):
            lane_roots(source, {"dynamic_instances": [hook]})


if __name__ == "__main__":
    unittest.main()
