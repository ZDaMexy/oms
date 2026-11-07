"""Selection, immutable input bytes and bounded parsed bake ownership."""

import gc
import hashlib
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
import weakref

import brotli

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import beatoraja_export as exporter
from beatoraja_native import lua


EVIDENCE = Path(__file__).resolve().parents[3] / "artifacts/beatoraja-capture/bake-memory-r1/focused"


class TrackedIndex(dict):
    pass


class BakeIndexTests(unittest.TestCase):
    def setUp(self):
        EVIDENCE.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=EVIDENCE)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.author_root = self.root / "author"
        self.author_root.mkdir()
        (self.author_root / "source.json").write_bytes(b'{"original":true}')
        self.source_sha = hashlib.sha256((self.author_root / "source.json").read_bytes()).hexdigest()
        self.author = SimpleNamespace(root=self.author_root.resolve(), family="lightning", name="Test")

    def make_index(self, name, *, animation="loop", complete=True, pixels=16, files=None, compressed=False):
        folder = self.root / name
        folder.mkdir()
        index = {"contract": "oms-iidx-baked-animation.v1", "verification": {"passed": True},
                 "selectionComplete": complete, "optimization": {"decodedPixels": pixels},
                 "source": {"authorRoot": str(self.author.root), "manifest": "source.json", "animation": animation,
                            "files": {"source.json": self.source_sha} if files is None else files},
                 "canvas": [4, 4], "sourceFrameCount": 3, "framesPerSecond": 60, "loop": True, "loopStartFrame": 1,
                 "pages": [{"id": 0, "file": "page.png", "width": 4, "height": 4}],
                 "frames": [{"sourceFrame": 0, "runs": [{"order": 0, "page": 0, "rect": [0, 0, 2, 2],
                              "offset": [-1, 1], "blend": "normal"}]},
                            {"sourceFrame": 1, "runs": [{"order": 0, "page": 0, "rect": [2, 2, 2, 2],
                              "offset": [-1, 1], "blend": "normal"}]},
                            {"sourceFrame": 2, "runs": [], "terminal": True}], "marker": name}
        path = folder / ("index.json.br" if compressed else "index.json")
        data = json.dumps(index).encode("utf-8")
        path.write_bytes(brotli.compress(data) if compressed else data)
        return path, index

    def test_discovery_keeps_complete_lower_cost_and_first_equal_candidate(self):
        paths = [self.make_index("partial", complete=False, pixels=1)[0],
                 self.make_index("large", pixels=30)[0], self.make_index("first-equal", pixels=10)[0],
                 self.make_index("later-equal", pixels=10)[0]]
        ignored, data = self.make_index("other-author")
        data["source"]["authorRoot"] = str(self.root)
        data["verification"]["passed"] = False
        ignored.write_text(json.dumps(data), encoding="utf-8")
        wrong, _ = self.make_index("other-contract")
        wrong.write_text('{"contract":"unrelated"}', encoding="utf-8")
        baked = exporter.read_bakes([p.parent for p in [*paths, ignored, wrong]], self.author)
        self.assertEqual([("source.json", "loop")], list(baked))
        self.assertEqual(paths[2], baked[("source.json", "loop")][0])

    def test_last_override_wins_regardless_of_rank_and_only_selected_sources_are_checked(self):
        discarded, _ = self.make_index("discarded", files={"missing.json": "bad"})
        better, _ = self.make_index("complete", pixels=1)
        last, expected = self.make_index("last", complete=False, pixels=100)
        baked = exporter.read_bakes([discarded.parent], self.author, [better, last])
        self.assertEqual((last, expected), baked[("source.json", "loop")])

    def test_membership_iteration_and_count_do_not_load_and_switch_releases_before_read(self):
        a, expected_a = self.make_index("a", animation="a")
        b, expected_b = self.make_index("b", animation="b")
        original = exporter._read_bake_index
        previous = None
        reads = []

        def tracked(path, expected_sha256=None):
            if previous is not None:
                self.assertIsNone(previous(), "Previous cached tree remained alive during the next read")
            index, digest, size = original(path, expected_sha256)
            reads.append((path, expected_sha256))
            return TrackedIndex(index), digest, size

        with patch.object(exporter, "_read_bake_index", side_effect=tracked):
            baked = exporter.read_bakes([], self.author, [a, b])
            self.assertEqual(2, len(reads))
            for _ in range(3):
                self.assertIn(("source.json", "a"), baked)
                self.assertNotIn(("source.json", "absent"), baked)
                self.assertEqual(2, len(baked))
                self.assertEqual([("source.json", "a"), ("source.json", "b")], list(baked))
            self.assertEqual(2, len(reads))
            self.assertEqual(expected_a, baked[("source.json", "a")][1])
            previous = weakref.ref(baked[("source.json", "a")][1])
            self.assertEqual(3, len(reads))
            self.assertEqual(expected_b, baked[("source.json", "b")][1])
            previous = weakref.ref(baked[("source.json", "b")][1])
            self.assertEqual(expected_a, baked[("source.json", "a")][1])
            self.assertEqual(5, len(reads))
            self.assertTrue(all(sha is not None for _, sha in reads[2:]))
            previous = weakref.ref(baked[("source.json", "a")][1])
            del baked
            gc.collect()
            self.assertIsNone(previous())

    def test_reloaded_index_preserves_native_frame_callbacks_and_geometry(self):
        a, original = self.make_index("a", animation="a")
        b, _ = self.make_index("b", animation="b")
        baked = exporter.read_bakes([], self.author, [a, b])
        sink = SimpleNamespace(dimensions={"page": (4, 4)}, add_baked_page=lambda path, page: "page")

        def compose(mapping):
            composer = exporter.Composer(self.author, sink, {}, "p1", mapping)
            composer.draw_baked("source.json", "a", (10, 20, 80, 160), timer=48, start_frame=0)
            return lua(composer.skin), composer.gaps

        expected = compose({("source.json", "a"): (a, original)})
        self.assertEqual(expected, compose(baked))
        baked[("source.json", "b")]
        self.assertEqual(expected, compose(baked))

    def test_changed_index_bytes_are_rejected_on_first_read_and_after_eviction(self):
        a, index = self.make_index("a", animation="a")
        b, _ = self.make_index("b", animation="b")
        baked = exporter.read_bakes([], self.author, [a, b])
        baked[("source.json", "a")]
        baked[("source.json", "b")]
        index["marker"] = "z"  # Same encoded length; a size check cannot detect this.
        a.write_text(json.dumps(index), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "changed after selection"):
            baked[("source.json", "a")]
        self.assertIsNone(baked._cached)
        fresh = exporter.read_bakes([], self.author, [a])
        a.write_bytes(b'{}')
        with self.assertRaisesRegex(ValueError, "changed after selection"):
            fresh[("source.json", "a")]

    def test_source_validation_order_deduplication_and_stale_rejection_are_preserved(self):
        (self.author.root / "second.json").write_bytes(b"second")
        sha = hashlib.sha256(b"second").hexdigest()
        a, _ = self.make_index("a", animation="a", files={"second.json": sha.upper(), "source.json": self.source_sha})
        b, _ = self.make_index("b", animation="b")
        with patch.object(exporter, "source_path", wraps=exporter.source_path) as sources:
            exporter.read_bakes([], self.author, [a, b])
        self.assertEqual(["second.json", "source.json"], [call.args[1] for call in sources.call_args_list])
        (self.author.root / "source.json").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "Stale bake source: source.json"):
            exporter.read_bakes([], self.author, [a, b])

    def test_invalid_overrides_and_unverified_discovery_are_rejected(self):
        path, index = self.make_index("invalid")
        for mutation in (lambda i: i.update(contract="other"),
                         lambda i: i["source"].update(authorRoot=str(self.root)),
                         lambda i: i["verification"].update(passed=False)):
            candidate = json.loads(json.dumps(index)); mutation(candidate)
            path.write_text(json.dumps(candidate), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Bake override must be verified"):
                exporter.read_bakes([], self.author, [path])
        with self.assertRaisesRegex(ValueError, "Unverified bake index"):
            exporter.read_bakes([path.parent], self.author)

    def test_compressed_override_hashes_original_bytes_and_preserves_json(self):
        path, expected = self.make_index("compressed", compressed=True)
        baked = exporter.read_bakes([], self.author, [path])
        self.assertEqual((path, expected), baked[("source.json", "loop")])
        selected = baked._entries[("source.json", "loop")]
        self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), selected.sha256)
        self.assertEqual(path.stat().st_size, selected.bytes)


if __name__ == "__main__":
    unittest.main()
