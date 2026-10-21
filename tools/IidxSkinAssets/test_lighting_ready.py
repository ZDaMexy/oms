"""Shared source-clock and wrapper equivalence checks without private artwork."""

import copy
import unittest

import lighting_ready as ready


def wrapper(*anchors):
    tags = []
    for depth, x in enumerate(anchors):
        tags.append({"type": "AP2PlaceObjectTag", "depth": depth, "update": False, "source_tag_id": 177,
                     "transform": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": x, "ty": 514},
                     "rotation_origin": None, "projection": "affine", "mult_color": None,
                     "add_color": None, "hsl_shift": None, "blend": None})
    frames = [{"start_tag_offset": 0, "num_tags": len(tags)}]
    frames.extend({"start_tag_offset": len(tags), "num_tags": 0} for _ in range(88))
    start = len(tags)
    for depth, x in enumerate(anchors):
        tags.append({"type": "AP2PlaceObjectTag", "depth": depth, "update": True, "source_tag_id": None,
                     "transform": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": x, "ty": 528},
                     "rotation_origin": None, "projection": "affine", "mult_color": {"r": 1, "g": 1, "b": 1, "a": 0},
                     "add_color": None, "hsl_shift": None, "blend": None})
    frames.append({"start_tag_offset": start, "num_tags": len(anchors)})
    return {"fps": 60, "frames": frames, "tags": tags, "exported_tags": {"x_get_ready": 177},
            "location": {"left": 0, "top": 0, "bottom": 1080, "right": 1920}}


class LightingReadyTest(unittest.TestCase):
    def test_full_wrappers_share_timing_but_keep_their_original_player_anchors(self):
        base = ready._wrapper_channels(wrapper(75))[0]["events"]
        for anchors in ((1413,), (458, 1028)):
            channels = ready._wrapper_channels(wrapper(*anchors))
            self.assertEqual(list(anchors), [channel["x"] for channel in channels])
            self.assertTrue(all(channel["events"] == base for channel in channels))
        self.assertEqual(0, base[-1][1]["mult_color"]["a"])
        self.assertEqual(528, base[-1][1]["transform"]["ty"])

    def test_wrapper_rejects_unknown_actions_and_a_truncated_exit(self):
        sample = wrapper(75)
        sample["frames"].pop()
        with self.assertRaisesRegex(ValueError, "90-frame"):
            ready._wrapper_channels(sample)
        sample = wrapper(75)
        sample["tags"][0]["type"] = "AP2DoActionTag"
        with self.assertRaisesRegex(ValueError, "unrecorded root operation"):
            ready._wrapper_channels(sample)

    def test_every_layout_shares_one_clip_and_the_actual_ready_stage(self):
        assets = {"animations": [{"id": ready.ANIMATION}], "resources": []}
        for layout, anchors in ready.LAYOUT_X.items():
            result = ready.build("test.ready", layout, assets)
            root = result["roots"][0]
            self.assertFalse(root["properties"]["visible"])
            self.assertEqual("preparation.ready", result["bindings"][0]["source"])
            self.assertEqual(len(anchors), len(root["children"]))
            for node, left in zip(root["children"], anchors):
                self.assertEqual("recorded-animation", node["type"])
                self.assertEqual("lr.ready", node["animation"])
                self.assertEqual("preparation.ready-elapsed", node["clock"])
                self.assertEqual(left, round(node["properties"]["x"] * 1920 + 75))
                self.assertEqual(1, node["properties"]["width"])
                self.assertEqual(1, node["properties"]["height"])

    def test_injection_targets_the_exact_frame_and_rejects_duplicates(self):
        assets = {"animations": [{"id": ready.ANIMATION, "path": "art/lr/ready.json.br", "resources": ["lr.tx"]}],
                  "resources": [{"id": "lr.tx", "type": "texture", "path": "art/lr/x.png", "filter": "nearest"}]}
        scene = {"templates": [{"id": "test.template.dp.information", "root": {"id": "test.dp.information"}}],
                 "instances": [], "bindings": []}
        manifest = {"resources": [], "animations": []}
        ready.inject(scene, manifest, assets, layout="dp")
        self.assertEqual(1, len(manifest["animations"]))
        self.assertEqual(1, len(manifest["resources"]))
        self.assertEqual("markers/frame-dp-information", scene["instances"][0]["material"]["resource"])
        with self.assertRaisesRegex(ValueError, "already contains"):
            ready.inject(scene, manifest, assets, layout="dp")
        other = copy.deepcopy(scene)
        other["templates"] = []
        with self.assertRaisesRegex(ValueError, "one exact selected Frame"):
            ready.inject(other, manifest, assets, layout="p1")


if __name__ == "__main__":
    unittest.main()
