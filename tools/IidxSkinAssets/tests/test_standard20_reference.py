"""Original20 keeps its actual single-DP movies and independent texture family."""

import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_reference import (LAYOUTS, MOVIE_IDS, RECORDED_LAYOUTS, Research,
                                layout_movies, movie_identifier, recorded_movies)


STYLE = "source20-gmframe0"
MOVIES = ("1p_frame", "2p_frame", "dp_frame", "center_frame_sp", "center_frame_dp",
          "center_frame_wide_1p", "center_frame_wide_2p", "center_frame_2pp")


class ReachedSourceParser(Exception):
    pass


def stop_at_source_parser(*args):
    raise ReachedSourceParser


class Standard20ReferenceTest(unittest.TestCase):
    def research(self, root, family="s20", textures=()):
        library, output = root / "library", root / "research"
        library.mkdir()
        output.mkdir()
        (library / "library.json").write_text(json.dumps({"status": "complete", "textures": list(textures)}),
                                               encoding="utf-8")
        return Research(library, SimpleNamespace(SWF=stop_at_source_parser), family), output

    def write_movies(self, library, movies, missing=None):
        raw = library / "packages/contents/data/graphic/source20/gmframe0/raw"
        (raw / "geo").mkdir(parents=True)
        for movie in movies:
            for relative in (f"afp/{movie}", f"afp/bsi/{movie}"):
                if relative != missing:
                    path = raw / relative
                    path.parent.mkdir(parents=True, exist_ok=True)
                    path.write_bytes(b"synthetic-presence-only")

    def test_original20_single_dp_sources_reach_parser_without_invented_side_movies(self):
        with tempfile.TemporaryDirectory() as directory:
            research, output = self.research(Path(directory))
            self.write_movies(research.root, MOVIES)
            with self.assertRaises(ReachedSourceParser):
                research.run_style(STYLE, output)
            run = json.loads((output / "source20/gmframe0/run.json").read_text(encoding="utf-8"))
            self.assertEqual(run["expectedLayouts"], list(MOVIES))
            self.assertNotIn("dp_frame_1p", run["expectedLayouts"])
            self.assertNotIn("center_frame_dp_1p", run["expectedLayouts"])

    def test_split_movies_cannot_stand_in_for_missing_original20_dp_sources(self):
        for missing in ("afp/dp_frame", "afp/bsi/center_frame_dp"):
            with self.subTest(missing=missing), tempfile.TemporaryDirectory() as directory:
                research, output = self.research(Path(directory))
                self.write_movies(research.root, (*MOVIES, "dp_frame_1p", "dp_frame_2p",
                                                  "center_frame_dp_1p", "center_frame_dp_2p"), missing)
                with self.assertRaises(FileNotFoundError):
                    research.run_style(STYLE, output)
                self.assertFalse((output / "source20/gmframe0").exists())

    def test_original20_cannot_borrow_a_later_common_family(self):
        for family in ("0", "s21", "s22"):
            with self.subTest(family=family), tempfile.TemporaryDirectory() as directory:
                research, output = self.research(Path(directory), family)
                with self.assertRaisesRegex(ValueError, "Original20"):
                    research.run_style(STYLE, output)
                self.assertFalse((output / "source20").exists())

    def test_original20_recorded_identity_has_one_real_dp_frame(self):
        self.assertEqual(layout_movies(STYLE), MOVIES)
        self.assertEqual(recorded_movies(STYLE), MOVIES)
        identifiers = [movie_identifier(STYLE, movie) for movie in MOVIES]
        self.assertEqual(len(set(identifiers)), len(MOVIES))
        self.assertEqual(movie_identifier(STYLE, "dp_frame"), "dp-frame")
        for movie in ("dp_frame_1p", "dp_frame_2p", "center_frame_dp_1p", "center_frame_dp_2p"):
            with self.subTest(movie=movie), self.assertRaises(KeyError):
                movie_identifier(STYLE, movie)

    def test_existing_source_movie_contracts_do_not_change(self):
        for style in (26, 23, "source22-gmframe9", "source21-gmframe0"):
            with self.subTest(style=style):
                self.assertEqual(layout_movies(style), LAYOUTS)
                self.assertEqual(recorded_movies(style), RECORDED_LAYOUTS)
                self.assertEqual(movie_identifier(style, "dp_frame_1p"), MOVIE_IDS["dp_frame_1p"])

    def test_original20_conflicting_effect_owner_is_not_the_source21_exception(self):
        textures = [{"package": f"contents/data/graphic/s20/{package}.ifs", "name": "playm_ef_rev3.png",
                     "sha256": fingerprint, "sprite": f"{package}/effect.png", "width": 1, "height": 1,
                     "imageRect": [0, 1, 0, 1], "uvRect": [0, 1, 0, 1]}
                    for package, fingerprint in (("common", "a" * 64), ("gameparts", "b" * 64))]
        with tempfile.TemporaryDirectory() as directory:
            research, _ = self.research(Path(directory), textures=textures)
            with self.assertRaisesRegex(ValueError, "Conflicting shared texture"):
                research.resolve("playm_ef_rev3", "source20/gmframe0")


if __name__ == "__main__":
    unittest.main()
