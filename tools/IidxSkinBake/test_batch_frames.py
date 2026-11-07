"""Focused regressions for provenance reuse and exact steady selection."""

import json
import os
from pathlib import Path
import stat
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

from batch_frames import (DEV, REPOSITORY, Sources, checked_output, digest,
                          matching, steady_frame, supplied_indices, transfer_page)


class BatchSelectionTest(unittest.TestCase):
    def test_explicit_output_root_retains_containment_and_read_only_sources(self):
        with tempfile.TemporaryDirectory(prefix="bake-external-root-", dir=REPOSITORY / ".dev-cache/temp") as directory:
            root = Path(directory)
            output = root / "new-job"
            with self.assertRaisesRegex(ValueError, "permitted output root"):
                checked_output(output)
            self.assertEqual(output, checked_output(output, root))
            for escaped in (root / ".." / "outside", root.with_name(root.name + "-prefix") / "job"):
                with self.assertRaisesRegex(ValueError, "permitted output root"):
                    checked_output(escaped, root)
            source = root / "original-author"
            source.mkdir()
            with self.assertRaisesRegex(ValueError, "may not overlap"):
                checked_output(source / "job", root, [source])
            output.mkdir()
            with self.assertRaisesRegex(ValueError, "new directory"):
                checked_output(output, root)
        with self.assertRaisesRegex(ValueError, "system drive"):
            checked_output(Path("C:/oms-bake-output-test-not-created/job"), Path("C:/"))

    def test_reparse_ancestor_is_rejected_without_following_it(self):
        with tempfile.TemporaryDirectory(prefix="bake-reparse-test-", dir=DEV) as directory:
            root = Path(directory)
            original = Path.lstat

            def attributes(path):
                if path == root:
                    class Reparse:
                        st_mode = stat.S_IFDIR
                        st_file_attributes = stat.FILE_ATTRIBUTE_REPARSE_POINT
                    return Reparse()
                return original(path)

            # No real junction is made in the workspace.
            with patch.object(Path, "lstat", attributes):
                with self.assertRaisesRegex(ValueError, "reparse points"):
                    checked_output(root / "new-job")

    def test_same_volume_page_transfer_rejects_stale_and_existing_pages(self):
        with tempfile.TemporaryDirectory(prefix="bake-page-test-", dir=DEV) as directory:
            root = Path(directory)
            source = root / "verified.png"
            Image.new("RGBA", (2, 2), (13, 29, 43, 127)).save(source)
            expected = digest(source.read_bytes())
            destination = root / "alias.png"
            self.assertEqual("hardlink", transfer_page(source, destination, expected))
            self.assertEqual(source.stat().st_ino, destination.stat().st_ino)
            self.assertEqual(expected, digest(destination.read_bytes()))
            with self.assertRaisesRegex(ValueError, "overwrite"):
                transfer_page(source, destination, expected)
            stale_target = root / "stale.png"
            with self.assertRaisesRegex(ValueError, "differs from its verified bake"):
                transfer_page(source, stale_target, "0" * 64)
            self.assertFalse(stale_target.exists())

    @unittest.skipUnless(os.environ.get("IIDX_BAKE_TEST_SECOND_VOLUME"), "Set an explicit second-volume synthetic test directory")
    def test_cross_volume_transfer_copies_only_verified_baked_page(self):
        second = Path(os.environ["IIDX_BAKE_TEST_SECOND_VOLUME"])
        with tempfile.TemporaryDirectory(prefix="bake-copy-source-", dir=DEV) as first_dir, \
                tempfile.TemporaryDirectory(prefix="bake-copy-target-", dir=second) as second_dir:
            source = Path(first_dir) / "baked.png"
            Image.new("RGBA", (3, 2), (41, 73, 109, 151)).save(source)
            expected = digest(source.read_bytes())
            destination = Path(second_dir) / "baked.png"
            self.assertNotEqual(source.stat().st_dev, destination.parent.stat().st_dev)
            self.assertEqual("verified-copy", transfer_page(source, destination, expected))
            self.assertEqual(expected, digest(destination.read_bytes()))
            self.assertEqual(expected, digest(source.read_bytes()))
            self.assertEqual(["baked.png"], sorted(p.name for p in destination.parent.iterdir()))

    def test_existing_index_lists_are_explicit_and_deduplicated(self):
        with tempfile.TemporaryDirectory(prefix="bake-index-list-", dir=DEV) as directory:
            root = Path(directory)
            index = root / "index.json"
            index.write_text("{}", encoding="utf8")
            listing = root / "existing.json"
            listing.write_text(json.dumps([str(index), str(index)]), encoding="utf8")
            self.assertEqual([index.resolve()], supplied_indices([listing], [index]))
            listing.write_text(json.dumps({"index": str(index)}), encoding="utf8")
            with self.assertRaisesRegex(ValueError, "contain index paths"):
                supplied_indices([listing])

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
