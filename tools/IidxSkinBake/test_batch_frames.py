"""Focused regressions for provenance reuse and exact steady selection."""

import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from batch_frames import DEV, Sources, matching, steady_frame


class BatchSelectionTest(unittest.TestCase):
    def test_source_constant_wins_over_loop_start(self):
        program = {"fps": 60, "frames": [{}] * 421, "loop": True, "loopStartFrame": 180}
        node = {"id": "steady", "clock": "value.constant"}
        scene = {"numericSources": [{"id": "constant", "operation": "add", "left": 4000, "right": 0}]}
        frame, why = steady_frame(node, scene, program)
        self.assertEqual(240, frame)
        self.assertEqual("source-constant", why["rule"])
        scene["numericSources"][0]["left"] = "score.value"
        with self.assertRaisesRegex(ValueError, "explicit constant"):
            steady_frame(node, scene, program)

    def test_partial_at_loop_start_cannot_cover_a_later_constant(self):
        job = {"sourceSignature": "same-program-and-pixels", "frames": [240]}
        partial = {"selectedFrames": [180], "selectionComplete": False}
        full = {"selectedFrames": list(range(421)), "selectionComplete": True}
        self.assertIsNone(matching(job, [(Path("old"), partial, job["sourceSignature"])]))
        self.assertEqual(Path("full"), matching(job, [(Path("full"), full, job["sourceSignature"])])[0])
        self.assertIsNone(matching(job, [(Path("same-id-other-pixels"), full, "different-program-or-pixels")]))
        job["frames"] = "all"
        self.assertIsNone(matching(job, [(Path("partial"), partial, job["sourceSignature"])]))

    def test_reuse_ignores_storage_path_but_keeps_pixels_sampler_and_family(self):
        with tempfile.TemporaryDirectory(prefix="batch-synthetic-", dir=DEV) as directory:
            root = Path(directory)
            Image.new("RGBA", (2, 2), (1, 2, 3, 255)).save(root / "first.png")
            (root / "second.png").write_bytes((root / "first.png").read_bytes())
            (root / "clip.json").write_text(json.dumps({"width": 2, "height": 2, "fps": 60,
                "frames": [{}, {}], "loop": True}), encoding="utf-8")

            def manifest(name, texture, sampling="nearest"):
                (root / name).write_text(json.dumps({"animations": [{"id": "clip", "path": "clip.json", "resources": ["texture"]}],
                    "resources": [{"id": "texture", "type": "texture", "path": texture, "filter": sampling}]}), encoding="utf-8")

            manifest("first.json", "first.png")
            manifest("second.json", "second.png")
            manifest("linear.json", "second.png", "linear")
            Image.new("RGBA", (2, 2), (1, 2, 3, 255)).save(root / "atlas.webp", lossless=True)
            manifest("webp.json", "atlas.webp")
            sources = Sources()
            identity = sources.describe("standard", root, "first.json", "clip")["signature"]
            self.assertEqual(identity, sources.describe("standard", root, "second.json", "clip")["signature"])
            self.assertNotEqual(identity, sources.describe("standard", root, "linear.json", "clip")["signature"])
            self.assertNotEqual(identity, sources.describe("lightning", root, "first.json", "clip")["signature"])
            webp = sources.describe("standard", root, "webp.json", "clip")
            self.assertEqual(16, webp["sourceDecodedBytes"])
            self.assertNotEqual(identity, webp["signature"])
            Image.new("RGBA", (2, 2), (1, 9, 3, 255)).save(root / "second.png")
            self.assertNotEqual(identity, Sources().describe("standard", root, "second.json", "clip")["signature"])


if __name__ == "__main__":
    unittest.main()
