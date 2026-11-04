"""Source isolation and per-movie snapshot boundaries for Standard research."""

import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_reference import (LAYOUTS, MOVIE_IDS, RECORDED_LAYOUTS, Research,
                                cabinet_loop_control, inside, reference_frame)


def texture(package, name, fingerprint="a" * 64):
    return {"package": f"contents/data/graphic/{package}.ifs", "name": f"{name}.png",
            "width": 1, "height": 1, "imageRect": [0, 1, 0, 1], "uvRect": [0, 1, 0, 1],
            "sprite": f"{package}/{name}.png", "sha256": fingerprint}


class StandardReferenceTest(unittest.TestCase):
    def research(self, root, textures, status="complete", family="0"):
        (root / "library.json").write_text(json.dumps({"status": status, "textures": textures}), encoding="utf-8")
        return Research(root, None, family)

    def test_each_movie_selects_its_own_loop(self):
        self.assertEqual(reference_frame(300, 60, {"loop": 119})[0], 180)
        self.assertEqual(reference_frame(300, 60, {"loop": 120})[0], 181)
        self.assertEqual(reference_frame(420, 60, {"loop": 180})[0], 241)
        self.assertEqual(reference_frame(420, 60, {"loop": 150})[0], 211)
        self.assertEqual(reference_frame(30, 60, {"loop": 20})[0], 30)
        self.assertEqual(reference_frame(30, 60, {})[0], 30)

    def test_malformed_timing_does_not_become_a_reference_frame(self):
        for count, fps, labels in ((0, 60, {}), (10, float("nan"), {}), (10, 0, {}),
                                   (10, 60, {"loop": -1}), (10, 60, {"loop": 10}),
                                   (10, 60, {"loop": True}), (10, 60, {"loop": 1.5})):
            with self.subTest(count=count, fps=fps, labels=labels), self.assertRaises(ValueError):
                reference_frame(count, fps, labels)

    def test_incomplete_extraction_cannot_feed_research(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(ValueError):
                self.research(Path(directory), [], status="running")

    def test_local_art_wins_and_common_family_never_crosses(self):
        with tempfile.TemporaryDirectory() as directory:
            research = self.research(Path(directory), [texture("gmframe26", "local"),
                                                       texture("0/common", "local", "b" * 64),
                                                       texture("0/common", "shared"),
                                                       texture("1/common", "only_hd")])
            self.assertEqual(research.resolve("local", "gmframe26")["asset"]["package"], "gmframe26")
            self.assertEqual(research.resolve("shared", "gmframe26")["asset"]["package"], "0/common")
            self.assertTrue(research.resolve("only_hd", "gmframe26")["missing"])

    def test_conflicting_shared_images_are_rejected_not_order_selected(self):
        with tempfile.TemporaryDirectory() as directory:
            research = self.research(Path(directory), [texture("0/common", "shared"),
                                                       texture("0/gameparts", "shared", "b" * 64)])
            with self.assertRaises(ValueError):
                research.resolve("shared", "gmframe26")

    def test_encoded_library_traversal_is_rejected(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            library = root / "library"
            library.mkdir()
            (root / "outside.png").write_bytes(b"outside")
            with self.assertRaises(ValueError):
                inside(library, "..%2Foutside.png")

    def test_missing_wide_movie_cannot_be_reported_as_complete_research(self):
        for missing in ("afp/center_frame_wide_1p", "afp/bsi/center_frame_wide_2p"):
            with self.subTest(missing=missing), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                library, output = root / "library", root / "research"
                library.mkdir()
                output.mkdir()
                research = self.research(library, [])
                raw = library / "packages/contents/data/graphic/gmframe26/raw"
                for movie in LAYOUTS:
                    for relative in (f"afp/{movie}", f"afp/bsi/{movie}"):
                        if relative != missing:
                            path = raw / relative
                            path.parent.mkdir(parents=True, exist_ok=True)
                            path.write_bytes(b"synthetic-presence-only")
                with self.assertRaises(FileNotFoundError):
                    research.run_style(26, output)
                self.assertFalse((output / "gmframe26").exists())

    def test_wide_and_two_player_movies_have_distinct_recorded_identities(self):
        required = {"center_frame_wide_1p", "center_frame_wide_2p", "center_frame_2pp"}
        self.assertTrue(required.issubset(RECORDED_LAYOUTS))
        self.assertTrue(set(RECORDED_LAYOUTS).issubset(LAYOUTS))
        self.assertEqual(set(MOVIE_IDS), set(RECORDED_LAYOUTS))
        self.assertEqual(len(set(MOVIE_IDS.values())), len(RECORDED_LAYOUTS))

    def loop_movie(self, method="deepGotoAndPlay", frame_index=2, label="loop"):
        actions = [{"action": "PUSH", "objects": [repr(label), "THIS", "2", "StringConstant(0x390: aeplib)"]},
                   {"action": "GET_VARIABLE"},
                   {"action": "PUSH", "objects": [f"StringConstant(0x815: {method})"]},
                   {"action": "CALL_METHOD"}, {"action": "END"}]
        tag_type = type("AP2DoActionTag", (), {})
        tag = tag_type()
        tag.bytecode = SimpleNamespace(as_dict=lambda: {"actions": actions})
        return SimpleNamespace(tags=[tag], labels={"loop": 1}, frames=[
            SimpleNamespace(start_tag_offset=0, num_tags=1 if i == frame_index else 0) for i in range(3)])

    def test_literal_final_deep_return_carries_its_source_evidence(self):
        movie = self.loop_movie()
        control, tags = cabinet_loop_control(movie)
        self.assertEqual(control["sourceFrame"], 2)
        self.assertEqual(control["loopStartFrame"], 1)
        self.assertIn("sourceBytecode", control)
        self.assertEqual(tags, {id(movie.tags[0])})

    def test_arbitrary_or_early_control_is_not_promoted_to_a_loop(self):
        for movie in (self.loop_movie(method="gotoAndPlay"), self.loop_movie(frame_index=0),
                      self.loop_movie(label="unobserved")):
            with self.assertRaises(ValueError):
                cabinet_loop_control(movie)


if __name__ == "__main__":
    unittest.main()
