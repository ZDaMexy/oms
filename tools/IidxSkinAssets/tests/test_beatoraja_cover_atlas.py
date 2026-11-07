"""Pixel-exact grid carriers and the official read-only timer selection path."""

import copy
from dataclasses import FrozenInstanceError
import gc
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch
import weakref

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import beatoraja_cover_atlas as atlas
from beatoraja_native import AssetSink, lua
from beatoraja_export import ANIMATION_RUNTIME


EVIDENCE = Path(__file__).resolve().parents[3] / "artifacts/beatoraja-capture/cover-grid-r1"


def fixture(root):
    source, output = root / "source", root / "output"
    source.mkdir(); output.mkdir()
    pages = []
    for number in range(2):
        with Image.new("RGBA", (4, 4)) as image:
            # Include transparent nonzero RGB: exact RGBA, not visual-only equality.
            image.putdata([((number * 17 + x * 31) % 256, y * 43, (x + y + number) * 19,
                            0 if (x + y) % 3 == 0 else 255 - x * 7) for y in range(4) for x in range(4)])
            path = source / f"page-{number}.png"
            image.save(path)
        pages.append({"id": number, "file": path.name, "width": 4, "height": 4,
                      "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
    frames = []
    for frame, crop in enumerate((0, 1, 2, 3, 4, 5, 6, 0)):
        runs = [{"order": 0, "page": crop // 4, "rect": [crop % 2 * 2, crop % 4 // 2 * 2, 2, 2],
                 "offset": [-1, 3], "blend": "normal"}]
        if frame in (0, 3, 4):
            runs.append({"order": 1, "page": frame % 2, "rect": [1, 0, 1, 2], "offset": [2, -1], "blend": "additive"})
        if frame == 6:
            runs.append({"order": 1, "page": 0, "rect": [0, 1, 1, 2], "offset": [2, -1], "blend": "normal"})
        frames.append({"sourceFrame": frame, "terminal": False, "runs": runs})
    frames.append({"sourceFrame": 8, "terminal": True, "runs": []})
    index = {"contract": "oms-iidx-baked-animation.v1", "verification": {"passed": True}, "selectionComplete": True,
             "sourceFrameCount": 9, "framesPerSecond": 10, "loop": True, "loopStartFrame": 2,
             "trigger": "gameplay.attach", "canvas": [4, 4], "pages": pages, "frames": frames,
             "source": {"authorRoot": str(source), "manifest": "lane.json", "animation": "loop"}}
    return source, AssetSink(source, output), index


class CoverAtlasTests(unittest.TestCase):
    def setUp(self):
        parent = EVIDENCE / "python"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source, self.sink, self.index = fixture(self.root)

    def test_reconstruction_preserves_each_rgba_run_order_geometry_and_blank_frame(self):
        before = copy.deepcopy(self.index)
        with patch.object(atlas, "_page_bytes", wraps=atlas._page_bytes) as reads:
            grids = atlas.cover_atlas(self.sink, self.source / "index.json", self.index, page_limit=4)
        self.assertEqual(2, reads.call_count)  # Source-page-major, not once per group.
        self.assertEqual(before, self.index)
        self.assertEqual(4, len(grids))  # Two pages for order 0, two distinct blend branches.
        self.assertTrue(any(g.columns * g.rows > len(g.crops) for g in grids))
        for frame in self.index["frames"]:
            actual = []
            for grid in grids:
                cell = dict(grid.frames).get(frame["sourceFrame"])
                if cell is None:
                    continue
                w, h = grid.size
                left, top = cell % grid.columns * w, cell // grid.columns * h
                with Image.open(self.sink.output / self.sink.source[grid.source]["path"]) as page:
                    with page.crop((left, top, left + w, top + h)) as pixels:
                        actual.append((grid.order, grid.blend, grid.offset, grid.size, pixels.tobytes()))
            expected = []
            for run in frame["runs"]:
                x, y, w, h = run["rect"]
                with Image.open(self.source / self.index["pages"][run["page"]]["file"]) as page:
                    with page.crop((x, y, x + w, y + h)) as pixels:
                        expected.append((run["order"], run["blend"], tuple(run["offset"]), (w, h), pixels.tobytes()))
            self.assertEqual(expected, actual, f"source frame {frame['sourceFrame']}")
        self.assertFalse(list(self.sink.output.glob(".cover-grid-*")))

    def test_exact_alias_cache_is_sink_local_and_image_specifiers_are_not_mutable_cache(self):
        original = atlas.cover_atlas(self.sink, self.source / "lane.json", self.index, page_limit=4)
        alias = copy.deepcopy(self.index)
        alias["source"].update(manifest="lift.json", animation="strict-alias")
        with patch.object(atlas, "_page_bytes", side_effect=AssertionError("Frozen shared page was reread")):
            again = atlas.cover_atlas(self.sink, self.source / "lane.json", self.index, page_limit=4)
            lifted = atlas.cover_atlas(self.sink, self.source / "lift.json", alias, page_limit=4)
        self.assertIs(original, again)
        self.assertIs(original, lifted)
        with self.assertRaises(FrozenInstanceError):
            original[0].columns = 9
        image = original[0].image(); image["src"] = "corruption"
        self.assertNotEqual("corruption", original[0].image()["src"])
        alias["frames"][0]["runs"][0]["rect"][0] = 2
        changed = atlas.cover_atlas(self.sink, self.source / "changed.json", alias, page_limit=4)
        self.assertIsNot(original, changed)
        second_output = self.root / "second-output"; second_output.mkdir()
        second = AssetSink(self.source, second_output)
        with patch.object(atlas, "_page_bytes", wraps=atlas._page_bytes) as reads:
            atlas.cover_atlas(second, self.source / "lane.json", self.index, page_limit=4)
        self.assertEqual(2, reads.call_count)

    def test_hash_dimensions_bounds_and_animation_qualification_fail_closed(self):
        mutations = [lambda i: i["verification"].update(passed=False),
                     lambda i: i.update(selectionComplete=False),
                     lambda i: i.update(trigger="judgement.hit"),
                     lambda i: i.update(loopStartFrame=8),
                     lambda i: i["frames"].pop(2),
                     lambda i: i["frames"][-1]["runs"].append(i["frames"][0]["runs"][0]),
                     lambda i: i["frames"][0]["runs"][0]["rect"].__setitem__(0, 4)]
        for mutation in mutations:
            index = copy.deepcopy(self.index); mutation(index)
            with self.assertRaises((ValueError, KeyError)):
                atlas.plan_cover_atlas(index, 4)
        bad = copy.deepcopy(self.index); bad["pages"][0]["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "hash"):
            atlas.cover_atlas(self.sink, self.source / "bad-hash.json", bad, 4)
        bad = copy.deepcopy(self.index); bad["pages"][0]["width"] = 5
        with self.assertRaisesRegex(ValueError, "dimensions"):
            atlas.cover_atlas(self.sink, self.source / "bad-size.json", bad, 4)
        failures = list(self.sink.output.glob(".cover-grid-*/failure.json.txt"))
        self.assertEqual(2, len(failures))
        self.assertTrue(all(not json.loads(path.read_text())["complete"] for path in failures))

    def test_pixel_cache_does_not_retain_the_input_tree_and_accepts_identical_reload(self):
        class TrackedIndex(dict):
            pass

        first = TrackedIndex(copy.deepcopy(self.index))
        reference = weakref.ref(first)
        grids = atlas.cover_atlas(self.sink, self.source / "index.json", first, 4)
        assets = copy.deepcopy(self.sink.source)
        del first
        gc.collect()
        self.assertIsNone(reference(), "Grid cache retained the complete source index")
        reloaded = copy.deepcopy(self.index)
        with patch.object(atlas, "_page_bytes", side_effect=AssertionError("Qualified pixels were reread")), \
             patch.object(self.sink, "add_raster", side_effect=AssertionError("Cached grids were encoded again")):
            again = atlas.cover_atlas(self.sink, self.source / "index.json", reloaded, 4)
        self.assertIs(grids, again)
        self.assertEqual(assets, self.sink.source)

    def test_page_limit_prefix_loop_and_padding_count_are_explicit(self):
        grids = atlas.plan_cover_atlas(self.index, 4)
        self.assertEqual(2, grids[0].columns)
        self.assertEqual(2, grids[0].rows)
        self.assertTrue(all(max(g.dimensions) <= 4 for g in grids))
        self.assertTrue(all("animation_frame(0,10,8,2,0)" in g.timer for g in grids))
        self.index["loop"] = False
        self.assertTrue(all("animation_frame(0,10,8,-1,0)" in g.timer for g in atlas.plan_cover_atlas(self.index, 4)))
        for grid in grids:
            self.assertEqual(grid.columns * grid.rows * 100, grid.image()["cycle"])
            self.assertIn("cell == nil then return main_state.timer_off_value", grid.timer)

    def test_cached_alias_must_qualify_its_new_paths_and_cannot_escape_source_root(self):
        atlas.cover_atlas(self.sink, self.source / "lane.json", self.index, 4)
        alias = copy.deepcopy(self.index)
        alias_root = self.root / "alias"; alias_root.mkdir()
        for page in alias["pages"]:
            (alias_root / page["file"]).write_bytes((self.source / page["file"]).read_bytes())
        with Image.new("RGBA", (4, 4), (1, 2, 3, 4)) as changed:
            changed.save(alias_root / alias["pages"][0]["file"])
        with self.assertRaisesRegex(ValueError, "hash"):
            atlas.cover_atlas(self.sink, alias_root / "index.json", alias, 4)
        escaped = copy.deepcopy(self.index); escaped["pages"][0]["file"] = "../escape.png"
        with self.assertRaisesRegex(ValueError, "Unsafe author path"):
            atlas.cover_atlas(self.sink, self.source / "escaped.json", escaped, 4)
        with self.assertRaisesRegex(ValueError, "escaped"):
            atlas._owned(self.source, self.sink.output)

    def test_mid_encoding_failure_retains_its_own_files_and_does_not_change_sources(self):
        before = {path.name: path.read_bytes() for path in self.source.iterdir()}
        with patch.object(self.sink, "add_raster", side_effect=OSError("isolated encoding failure")):
            with self.assertRaisesRegex(OSError, "isolated encoding"):
                atlas.cover_atlas(self.sink, self.source / "index.json", self.index, 4)
        work = list(self.sink.output.glob(".cover-grid-*"))
        self.assertEqual(1, len(work))
        self.assertTrue(list(work[0].glob("*.rgba")))
        self.assertIn("isolated encoding failure", (work[0] / "failure.json.txt").read_text())
        self.assertEqual(before, {path.name: path.read_bytes() for path in self.source.iterdir()})

    def test_official_luaj_and_native_slider_hidden_lift_image_selection(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac = Path(os.environ.get("BEATORAJA_JAVAC", "C:/Program Files/Eclipse Adoptium/jdk-17.0.20.8-hotspot/bin/javac.exe"))
        java = Path(os.environ.get("BEATORAJA_JAVA", "D:/beatoraja/jre/bin/java.exe"))
        if not all(path.is_file() for path in (jar, javac, java)):
            self.skipTest("Official beatoraja JAR, JDK17 and runtime21 are required")
        folder = Path(tempfile.mkdtemp(prefix="native-", dir=EVIDENCE))
        cases = []
        for loop in (True, False):
            index = copy.deepcopy(self.index); index["loop"] = loop
            grids = atlas.plan_cover_atlas(index, 4)
            skin = {key: [] for key in ("slider", "hiddenCover", "liftCover", "image", "destination")}
            membership = {}
            for number, grid in enumerate(grids):
                for kind in ("slider", "hiddenCover", "liftCover", "image"):
                    identifier = f"{kind}.{number}"
                    item = {"id": identifier, **grid.image()}
                    if kind == "slider":
                        item.update(angle=2, range=471, type=4, changeable=False)
                    elif kind in ("hiddenCover", "liftCover"):
                        item.update(disapearLine=137, isDisapearLineLinkLift=kind == "hiddenCover")
                    skin[kind].append(item)
                    skin["destination"].append({"id": identifier, "draw": grid.draw})
                    membership[identifier] = dict(grid.frames)
            times = sorted({0, 1, 999, 1000, 12345, 123456789, 2000000999,
                            *(boundary + delta for boundary in range(100000, 1600001, 100000) for delta in (-1, 0, 1))})
            samples = []
            for now in times:
                frame = now * 10 // 1000000
                if frame >= 8:
                    frame = 2 + (frame - 2) % 6 if loop else 8
                samples.append({"now": now, "cells": {key: frames.get(frame, -1) for key, frames in membership.items()}})
            cases.append({"name": "prefix-loop" if loop else "nonloop-terminal", "skin": skin, "samples": samples})
        recipe = folder / "cases.lua"
        recipe.write_text(ANIMATION_RUNTIME + "\nreturn " + lua(cases), encoding="utf8")
        probe = Path(__file__).with_name("CoverGridTimerProbe.java")
        compiled = subprocess.run([str(javac), "-encoding", "UTF-8", "-cp", str(jar), "-d", str(folder), str(probe)],
                                  capture_output=True, text=True, encoding="utf8", errors="replace", timeout=60)
        (folder / "javac.log").write_text(compiled.stdout + compiled.stderr, encoding="utf8")
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        result = subprocess.run([str(java), "-Xmx512m", "-cp", str(folder) + os.pathsep + str(jar),
                                 "bms.player.beatoraja.skin.lua.CoverGridTimerProbe", str(recipe)],
                                capture_output=True, text=True, encoding="utf8", errors="replace", timeout=60)
        (folder / "native.log").write_text(result.stdout + result.stderr, encoding="utf8")
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        receipt = json.loads(result.stdout)
        self.assertTrue(receipt["complete"])
        self.assertEqual(2, receipt["reloads"])
        self.assertGreater(receipt["selectionChecks"], 0)
        self.assertGreater(receipt["offChecks"], 0)
        receipt.update(recipeSha256=hashlib.sha256(recipe.read_bytes()).hexdigest(),
                       probeSha256=hashlib.sha256(probe.read_bytes()).hexdigest(),
                       jarSha256=hashlib.sha256(jar.read_bytes()).hexdigest(), maxHeap="512m")
        (folder / "native-receipt.json").write_text(json.dumps(receipt, indent=2) + "\n", encoding="utf8")
        print("Official cover grid probe: " + str(folder / "native-receipt.json"))


if __name__ == "__main__":
    unittest.main()
