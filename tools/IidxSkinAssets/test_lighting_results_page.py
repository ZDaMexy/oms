"""Exact-source, true-data and non-overwrite contracts for the first results author."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from PIL import Image

from lighting_results_page import (ASSETS, CANVAS, CONTRACT, HIT_RESULTS, NAME, TEXT_SOURCES, page_member_bytes,
                                   replace_results_page, results_page, validate_generated_page)
from build_lighting_results_page import identity, raw_crop_evidence, sha, upgrade
from build_omni_skin import copy_exact, exact_file, main as repackage_main, package_closure


def visible_elements(document, outcome):
    return {element["id"] for element in document["results"]["elements"]
            if "visibleWhen" not in element or type(outcome) is bool and element["visibleWhen"]["value"] is outcome}


class ResultsPageTest(unittest.TestCase):
    def test_clear_failed_and_unknown_do_not_borrow_completion_or_each_others_art(self):
        document = results_page()
        clear, failed, unknown = (visible_elements(document, outcome) for outcome in (True, False, None))
        for field in ("background", "left-frame", "right-frame", "outcome-word"):
            self.assertIn("clear-" + field, clear)
            self.assertNotIn("failed-" + field, clear)
            self.assertIn("failed-" + field, failed)
            self.assertNotIn("clear-" + field, failed)
            self.assertNotIn("clear-" + field, unknown)
            self.assertNotIn("failed-" + field, unknown)
        self.assertIn("neutral-background", unknown)
        self.assertIn("native-ruleset-statistics", unknown)
        self.assertTrue(all(element["visibleWhen"]["source"] == "score.cleared"
                            for element in document["results"]["elements"] if "visibleWhen" in element))

    def test_all_dynamic_readouts_use_actual_sources_and_rank_gauge_keep_the_native_owner(self):
        document = results_page()
        elements = document["results"]["elements"]
        sources = {element["source"] for element in elements if element["type"] == "text"}
        self.assertEqual(sources, TEXT_SOURCES)
        for result in HIT_RESULTS:
            self.assertIn("hit." + result, sources)
            self.assertIn("hit." + result + ".label", sources)
        self.assertNotIn("score.rank", sources)
        self.assertFalse(any("text" in element for element in elements))
        self.assertEqual(len([element for element in elements if element["type"] == "native-statistics"]), 1)
        self.assertFalse(any("pb" in source or "opponent" in source or "target" in source for source in sources))

    def test_exact_original_bitmaps_only_and_no_digit_or_ordinal_substitute(self):
        self.assertEqual(len(ASSETS), len({asset.name for asset in ASSETS}))
        self.assertTrue(all(asset.source.startswith("packages/contents/data/graphic/1/result/sprites/") for asset in ASSETS))
        self.assertFalse(any("sub_premium" in asset.source for asset in ASSETS))
        self.assertFalse(any(asset.name.startswith(("score0", "result_judge_num", "djt_result_num", "level_s_")) for asset in ASSETS))
        for asset in ASSETS:
            if asset.identifier in ("clear-bg", "failed-bg", "outer-shadow", "liquid"):
                self.assertEqual((asset.width, asset.height), CANVAS)
        resources = results_page()["results"]["resources"]
        self.assertEqual({resource["path"] for resource in resources}, {asset.destination for asset in ASSETS})

    def test_accuracy_and_max_combo_are_labelled_without_borrowing_miss_count_art_or_statistics(self):
        elements = {element["id"]: element for element in results_page()["results"]["elements"]}
        for heading, value, source in (("accuracy-heading", "accuracy-value", "score.accuracy"),
                                       ("maximum-combo-heading", "maximum-combo-value", "score.max-combo")):
            self.assertEqual(elements[heading]["type"], "text")
            self.assertEqual(elements[heading]["source"], source + ".label")
            self.assertEqual(elements[value]["source"], source)
            self.assertLessEqual(elements[heading]["y"] + elements[heading]["height"], elements[value]["y"])
        self.assertNotIn("score_panel_mc.png", {asset.name for asset in ASSETS})
        self.assertFalse(any("miss-count" in source for source in TEXT_SOURCES))

    def test_clear_and_failed_words_use_real_main_english_art_without_aspect_distortion(self):
        assets = {asset.identifier: asset for asset in ASSETS}
        self.assertEqual(assets["clear-title"].name, "txt_clear.png")
        self.assertEqual(assets["failed-title"].name, "txt_failed.png")
        for element in results_page()["results"]["elements"]:
            if element["id"] in ("clear-outcome-word", "failed-outcome-word"):
                asset = assets[element["resource"]]
                self.assertAlmostEqual(element["width"] / element["height"], asset.width / asset.height)
                self.assertEqual(element["visibleWhen"]["source"], "score.cleared")
        self.assertFalse(any("stageclear_kana" in asset.name or "stagefailed_kana" in asset.name for asset in ASSETS))

    def test_results_replacement_preserves_other_page_value_bytes_and_semantics(self):
        parent = (' \n{"songSelect": { "resources" : [{"id":"ss","path":"pages/\\u7ed3.png"}],'
                  ' "background":"ss", "listEdge":"ss", "row":"ss", "selectedRow":"ss" },'
                  ' "results" : {"resources": []}, "contract" : "' + CONTRACT + '"}\r\n').encode("utf-8")
        updated = replace_results_page(parent, results_page()["results"])
        self.assertEqual(page_member_bytes(parent)["songSelect"], page_member_bytes(updated)["songSelect"])
        self.assertEqual(page_member_bytes(parent)["contract"], page_member_bytes(updated)["contract"])
        self.assertEqual(json.loads(parent)["songSelect"], json.loads(updated)["songSelect"])
        self.assertEqual(json.loads(updated)["results"], results_page()["results"])
        self.assertTrue(updated.startswith(parent[:parent.index(b'{"resources": []}')]))
        self.assertTrue(updated.endswith(parent[parent.index(b'{"resources": []}') + len(b'{"resources": []}'):]))

    def test_a_songselect_only_parent_and_an_author_without_pages_can_acquire_results(self):
        parent = json.dumps({"contract": CONTRACT, "songSelect": {"resources": []}}, indent=2).encode("utf-8")
        updated = replace_results_page(parent, results_page()["results"])
        self.assertEqual(page_member_bytes(parent)["songSelect"], page_member_bytes(updated)["songSelect"])
        self.assertEqual(json.loads(replace_results_page(None, results_page()["results"])), results_page())

    def test_structural_splice_rejects_duplicate_keys_non_json_numbers_and_incomplete_roots(self):
        for suffix in ('"results":{},"results":{}', '"songSelect":{"row":1,"row":2}', '"songSelect":NaN', '"songSelect":{},'):
            parent = ('{"contract":"' + CONTRACT + '",' + suffix + '}').encode("utf-8")
            with self.subTest(suffix=suffix), self.assertRaises(ValueError):
                replace_results_page(parent, results_page()["results"])

    def test_raw_crop_proof_keeps_transparent_rgb_and_rejects_changed_original_pixels(self):
        with tempfile.TemporaryDirectory() as directory:
            library = Path(directory)
            raw = Image.new("RGBA", (5, 4), (7, 9, 11, 0))
            raw.putpixel((2, 2), (201, 99, 3, 255))
            path = library / "raw.png"
            raw.save(path)
            item = {"raw": "raw.png", "width": 3, "height": 2, "imageRect": [20, 25, 30, 34], "uvRect": [21, 24, 31, 33]}
            rgba = raw.crop((1, 1, 4, 3)).tobytes()
            receipt = raw_crop_evidence(library, item, rgba, {"raw.png": {"sha256": sha(path)}})
            self.assertEqual(receipt["borders"], [1, 1, 1, 1])
            self.assertEqual(receipt["croppedRgbaSha256"], hashlib.sha256(rgba).hexdigest())
            changed = bytearray(rgba)
            changed[0] ^= 1  # Change only the RGB of an invisible pixel; visual equivalence is insufficient.
            with self.assertRaises(ValueError):
                raw_crop_evidence(library, item, bytes(changed), {"raw.png": {"sha256": sha(path)}})

    def test_generation_is_deterministic_and_within_the_existing_public_budgets(self):
        first = results_page()
        second = results_page()
        self.assertEqual(first, second)
        self.assertLess(len(json.dumps(first).encode("utf-8")), 64 * 1024)
        self.assertLessEqual(sum(asset.width * asset.height for asset in ASSETS), 16 * 1024 * 1024)
        validate_generated_page(first)

    def test_generator_refuses_invented_sources_completion_clear_substitution_and_out_of_canvas(self):
        for change in ("source", "clear", "rectangle"):
            value = copy.deepcopy(results_page())
            if change == "source":
                next(element for element in value["results"]["elements"] if element["type"] == "text")["source"] = "fake.pb"
            elif change == "clear":
                next(element for element in value["results"]["elements"] if "visibleWhen" in element)["visibleWhen"]["source"] = "score.passed"
            else:
                value["results"]["elements"][0]["x"] = 1
            with self.assertRaises(ValueError):
                validate_generated_page(value)

    def test_identity_includes_every_path_size_and_byte_digest(self):
        baseline = [{"path": "a.png", "bytes": 1, "sha256": "1" * 64}]
        for key, replacement in (("path", "b.png"), ("bytes", 2), ("sha256", "2" * 64)):
            other = copy.deepcopy(baseline)
            other[0][key] = replacement
            self.assertNotEqual(identity(baseline), identity(other))

    def test_existing_output_is_rejected_before_any_parent_or_library_content_is_read(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, output, library, index = (root / name for name in ("parent", "new", "library", "index"))
            for path in (source, output, library, index):
                path.mkdir()
            protected = output / "keep.txt"
            protected.write_text("existing author", encoding="utf-8")
            with self.assertRaises(ValueError):
                upgrade(source, output, library, index, root / "evidence")
            self.assertEqual(protected.read_text(encoding="utf-8"), "existing author")
            self.assertFalse((root / "evidence").exists())

    @staticmethod
    def _replacement_fixture(root, shared_gameplay=False):
        """Synthetic file-routing boundary only; real-source receipts are verified outside this mock."""
        source, output, library, index, evidence = (root / name for name in ("parent", "new", "library", "index", "evidence"))
        for path in (source, library, index):
            path.mkdir()
        (source / "skin.ini").write_text("[General]\nName: " + NAME + "\n", encoding="utf-8")
        old_paths = ["pages/results33/" + name for name in ("score_panel_mc.png", "33re_tx_stageclear_kana.png", "33re_tx_stagefailed_kana.png")]
        old_resources = [{"id": "old" + str(i), "path": path} for i, path in enumerate(old_paths)]
        manifest_resources = [{"id": "shared", "path": old_paths[0]}] if shared_gameplay else []
        (source / "gameplay-skin.json").write_text(json.dumps({"contract": "oms-gameplay-skin-manifest.v1",
            "scene": "gameplay-skin.scene.json", "resources": manifest_resources}), encoding="utf-8")
        (source / "gameplay-skin.scene.json").write_text('{"contract":"oms-gameplay-skin-scene.v1"}', encoding="utf-8")
        (source / "generation.json").write_text(json.dumps({"name": NAME}), encoding="utf-8")
        (source / "package-audit.json").write_text('{"status":"fixture"}', encoding="utf-8")
        (source / "retained-extra.txt").write_bytes(b"An unrelated physical file must not disappear")
        for path in old_paths + ["pages/song.png"]:
            destination = source / path
            destination.parent.mkdir(parents=True, exist_ok=True)
            Image.new("RGBA", (1, 1), (12, 34, 56, 255)).save(destination)
        song = b'{ "resources" : [{"id":"ss","path":"pages/song.png"}], "background":"ss", "listEdge":"ss", "row":"ss", "selectedRow":"ss" }'
        declaration = ('{"contract":"' + CONTRACT + '","results":' + json.dumps({"resources": old_resources}) + ',"songSelect":').encode("utf-8") + song + b'}\n'
        (source / "skin-pages.json").write_bytes(declaration)
        for path in (library / "library.json", index / "summary.json", index / "source-post-verification.json",
                     index / "results-texture-candidates.json", index / "output-sha256.json"):
            path.write_text("{}", encoding="utf-8")
        rows = []
        for i, asset in enumerate(ASSETS):
            sprite = library / asset.source
            sprite.parent.mkdir(parents=True, exist_ok=True)
            image = Image.new("RGBA", (1, 1), (i, 34, 56, 255))
            image.save(sprite)
            raw_relative = asset.source.replace("/sprites/", "/raw/tex/")
            raw = library / raw_relative
            raw.parent.mkdir(parents=True, exist_ok=True)
            raw_image = Image.new("RGBA", (3, 3), (3, 2, 1, 0))
            raw_image.paste(image, (1, 1))
            raw_image.save(raw)
            item = {"raw": raw_relative, "width": 1, "height": 1, "imageRect": [0, 3, 0, 3], "uvRect": [1, 2, 1, 2]}
            rows.append({"id": asset.identifier, "path": asset.destination, "source": asset.source, "sha256": sha(sprite),
                "bytes": sprite.stat().st_size, "width": 1, "height": 1, "role": "synthetic routing fixture",
                "rgbaSha256": hashlib.sha256(image.tobytes()).hexdigest(),
                "rawPixels": raw_crop_evidence(library, item, image.tobytes(), {raw_relative: {"sha256": sha(raw)}})})
        return source, output, library, index, evidence, rows, old_paths, song

    def test_actual_results_update_preserves_songselect_bytes_gameplay_and_unrelated_physical_files(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output, library, index, evidence, rows, old_paths, song = self._replacement_fixture(Path(directory))
            before = {path.relative_to(source).as_posix(): path.read_bytes() for path in source.rglob("*") if path.is_file()}
            with patch("build_lighting_results_page.verify_sources", return_value=rows):
                receipt = upgrade(source, output, library, index, evidence)
            self.assertEqual(page_member_bytes((output / "skin-pages.json").read_bytes())["songSelect"], song)
            self.assertEqual((evidence / "skin-pages.json").read_bytes(), (output / "skin-pages.json").read_bytes())
            self.assertEqual((output / "pages/song.png").read_bytes(), before["pages/song.png"])
            self.assertEqual((output / "retained-extra.txt").read_bytes(), before["retained-extra.txt"])
            for path in ("skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json"):
                self.assertEqual((output / path).read_bytes(), before[path])
            self.assertTrue(all(not (output / path).exists() for path in old_paths))
            self.assertEqual(receipt["sourcePreservation"]["removedUnreferencedResultsPNGs"], sorted(old_paths))
            self.assertEqual(receipt["sourcePreservation"]["parentGameplayFiles"], 3)
            self.assertEqual(receipt["pageBudget"]["resources"], len(ASSETS) + 1)
            self.assertEqual(before, {path.relative_to(source).as_posix(): path.read_bytes() for path in source.rglob("*") if path.is_file()})
            self.assertFalse(output.with_suffix(".osk").exists())
            self.assertEqual({row["path"] for row in json.loads((evidence / "physical-diff.json").read_text())["removed"]}, set(old_paths))

    def test_old_results_image_still_owned_by_gameplay_is_not_pruned(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output, library, index, evidence, rows, old_paths, _ = self._replacement_fixture(Path(directory), shared_gameplay=True)
            original = (source / old_paths[0]).read_bytes()
            with patch("build_lighting_results_page.verify_sources", return_value=rows):
                receipt = upgrade(source, output, library, index, evidence)
            self.assertEqual((output / old_paths[0]).read_bytes(), original)
            self.assertIn(old_paths[0], package_closure(output))
            self.assertEqual(receipt["sourcePreservation"]["retainedOldResultsResourcesUsedElsewhere"], [old_paths[0]])
            self.assertEqual(receipt["sourcePreservation"]["parentGameplayFiles"], 4)

    def test_results_update_cannot_overwrite_a_songselect_resource(self):
        with tempfile.TemporaryDirectory() as directory:
            source, output, library, index, evidence, rows, _, _ = self._replacement_fixture(Path(directory))
            page = json.loads((source / "skin-pages.json").read_text())
            collision = ASSETS[0].destination
            page["songSelect"]["resources"][0]["path"] = collision
            (source / "skin-pages.json").write_text(json.dumps(page), encoding="utf-8")
            (source / collision).parent.mkdir(parents=True, exist_ok=True)
            (source / collision).write_bytes(b"an unrelated retained SongSelect resource")
            with patch("build_lighting_results_page.verify_sources", return_value=rows), self.assertRaises(ValueError):
                upgrade(source, output, library, index, evidence)
            self.assertEqual((source / collision).read_bytes(), b"an unrelated retained SongSelect resource")
            self.assertFalse(output.exists())
            self.assertFalse(evidence.exists())

    @staticmethod
    def _package(root, page=True):
        root.mkdir()
        (root / "skin.ini").write_text("[General]\nName: Closure fixture\nAuthor: OMS tests\n", encoding="utf-8")
        (root / "gameplay-skin.json").write_text(json.dumps({"contract": "oms-gameplay-skin-manifest.v1", "resources": [],
            "scene": "gameplay-skin.scene.json"}), encoding="utf-8")
        (root / "gameplay-skin.scene.json").write_text('{"contract":"oms-gameplay-skin-scene.v1"}', encoding="utf-8")
        (root / "generation.json").write_text('{"name":"Closure fixture","status":"fixture"}', encoding="utf-8")
        if page:
            (root / "skin-pages.json").write_text(json.dumps({"contract": "oms-skin-pages.v1", "results": {
                "resources": [{"id": "frame", "path": "pages/frame.png"}]}}), encoding="utf-8")
            (root / "pages").mkdir()
            (root / "pages/frame.png").write_bytes(b"synthetic closure payload; codec is tested separately")

    def test_unified_optional_page_closure_copies_every_declared_file_and_does_not_collect_unrelated_files(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, destination = root / "source", root / "copy"
            self._package(source)
            (source / "unused.png").write_bytes(b"unreferenced")
            destination.mkdir()
            closure = package_closure(source)
            self.assertEqual(set(closure), {"skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json", "skin-pages.json", "pages/frame.png"})
            for relative in closure:
                copy_exact(exact_file(source, relative), destination / relative)
            self.assertEqual(package_closure(destination), closure)
            self.assertTrue(all((source / relative).read_bytes() == (destination / relative).read_bytes() for relative in closure))
            self.assertFalse((destination / "unused.png").exists())

    def test_actual_repackage_entry_copies_pages_before_shared_audit_and_publication(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            source, output = root / "source", root / "output"
            self._package(source)
            expected = {relative: (source / relative).read_bytes() for relative in package_closure(source)}
            observed = []

            def prepare(staging, receipt, parent, progress_reference, choice_preview_library):
                self.assertEqual(parent, source)
                closure = package_closure(staging)
                self.assertEqual(set(closure), set(expected))
                self.assertTrue(all((staging / relative).read_bytes() == content for relative, content in expected.items()))
                observed.append("shared-audit-boundary")
                return closure, {"fixture": "this test isolates copy routing, not the shared semantic audit"}

            def publish(staging, target, receipt, files, audit):
                self.assertEqual(target, output)
                self.assertIn("skin-pages.json", files)
                self.assertIn("pages/frame.png", files)
                observed.append("publication-boundary")

            with patch("sys.argv", ["build_omni_skin.py", "--repackage-directory", str(source), "--output", str(output)]), \
                    patch("build_omni_skin.prepare_package", side_effect=prepare), \
                    patch("build_omni_skin.publish_prepared_package", side_effect=publish):
                repackage_main()
            self.assertEqual(observed, ["shared-audit-boundary", "publication-boundary"])
            self.assertFalse(output.with_suffix(".osk").exists())

    def test_generic_enumerator_leaves_page_semantics_to_the_shared_codec(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source"
            self._package(source)
            page_path = source / "skin-pages.json"
            value = json.loads(page_path.read_text(encoding="utf-8"))
            value["contract"] = "future-invalid-to-current-client"
            value["results"]["not-a-real-semantic-field"] = 1
            page_path.write_text(json.dumps(value), encoding="utf-8")
            self.assertIn("pages/frame.png", package_closure(source))

    def test_declared_page_missing_files_and_noncanonical_or_escaping_paths_fail_explicitly(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source"
            self._package(source)
            page_path = source / "skin-pages.json"
            original = json.loads(page_path.read_text(encoding="utf-8"))
            for path in ("../outside.png", "pages/../frame.png", "/outside.png", "C:/outside.png", "C:outside.png", "pages\\frame.png", "", "pages/missing.png"):
                value = copy.deepcopy(original)
                value["results"]["resources"][0]["path"] = path
                page_path.write_text(json.dumps(value), encoding="utf-8")
                with self.subTest(path=path), self.assertRaises(ValueError):
                    package_closure(source)

    def test_ordinary_parent_without_pages_has_its_unchanged_gameplay_closure(self):
        with tempfile.TemporaryDirectory() as directory:
            source = Path(directory) / "source"
            self._package(source, page=False)
            self.assertEqual(package_closure(source), ["gameplay-skin.json", "gameplay-skin.scene.json", "skin.ini"])


if __name__ == "__main__":
    unittest.main()
