"""Synthetic reference/provenance audit tests. No HDD, player data, or private artwork."""
from __future__ import annotations

import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image

from audit_lighting_asset_usage import (AuthorReferences, ReadLedger, atlas_derivations, classify_sources, member,
    output_directory, scene_dependencies, tint_derivations, validated_derivations)


def source(identifier, **changes):
    row = {"id": identifier, "path": identifier + ".png", "sha256": "encoded", "width": 2,
           "height": 2, "rgbaSha256": "pixels", "scope": "gameplay-or-unresolved",
           "family": "shared-controls", "scopeReason": None}
    row.update(changes)
    return row


def reference(path, *kinds, **changes):
    row = {"path": path, "sha256": "encoded", "width": 2, "height": 2,
           "rgbaSha256": "pixels", "kinds": list(kinds)}
    row.update(changes)
    return row


class ClassificationTest(unittest.TestCase):
    def test_equal_content_is_alias_evidence_without_selecting_source_provenance(self):
        rows = [source("one"), source("two", sha256="different-encoding")]
        classify_sources(rows, [reference("art/short.png", "runtime-scene-font-or-clip")], {})
        self.assertEqual([row["classification"] for row in rows], ["runtime_reference_alias"] * 2)
        self.assertEqual(rows[0]["equivalentSourceCount"], 2)

    def test_shape_is_part_of_pixel_identity(self):
        row = source("one", width=1, height=4)
        classify_sources([row], [reference("art/short.png", "runtime-scene-font-or-clip")], {})
        self.assertEqual(row["classification"], "candidate_unreferenced")

    def test_previews_and_unused_declarations_do_not_become_runtime_references(self):
        preview, declared, candidate = source("p"), source("d", rgbaSha256="declared"), source("c", rgbaSha256="none")
        classify_sources([preview, declared, candidate], [reference("preview.png", "preview"),
            reference("declared.png", "declared", rgbaSha256="declared")], {})
        self.assertEqual([row["classification"] for row in (preview, declared, candidate)],
                         ["preview_only", "evidence_insufficient", "candidate_unreferenced"])

    def test_scope_exclusion_retains_reuse_evidence_and_frame_composites_stay_unresolved(self):
        excluded = source("premium", scope="excluded-premium-or-secondary", scopeReason="explicit scope")
        frame = source("frame", family="game-frame", rgbaSha256="baked")
        classify_sources([excluded, frame], [reference("same.png", "runtime-ini-selector")], {})
        self.assertEqual(excluded["classification"], "explicit_out_of_scope")
        self.assertEqual(excluded["runtimeReferences"], ["same.png"])
        self.assertEqual(frame["classification"], "evidence_insufficient")

    def test_verified_derivation_is_not_mislabelled_unreferenced(self):
        rows = [source("original"), source("same-content-other-package", sha256="other-encoding")]
        classify_sources(rows, [], {"original": [{"kind": "explicit-transformation-receipt"}]})
        self.assertEqual([row["classification"] for row in rows], ["derived_reference"] * 2)
        self.assertEqual(rows[1]["derivations"][0]["inputSourceId"], "original")

    def test_fully_transparent_source_does_not_suggest_a_missing_visible_feature(self):
        row = source("helper", alphaBounds=None)
        classify_sources([row], [], {})
        self.assertEqual(row["classification"], "evidence_insufficient")


class ReferenceGraphTest(unittest.TestCase):
    def test_resource_font_animation_branches_are_in_union(self):
        scene = {"root": {"resource": "direct", "properties": {"bitmap-font": "digits", "colour": "#64ff00ff"}},
                 "variants": [{"property": "resource", "default": "zero", "cases": [{"value": "one"}]}],
                 "tracks": [{"property": "resource", "keyframes": [{"value": "two"}]}],
                 "animation": "clip"}
        resources, fonts, animations, tints = scene_dependencies(scene)
        self.assertEqual(resources, {"direct", "zero", "one", "two"})
        self.assertEqual(fonts, {"digits"})
        self.assertEqual(animations, {"clip"})
        self.assertEqual(tints["digits"], {"#64ff00ff"})

    def test_catalogue_contribution_scene_and_clip_font_dependencies(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            def write(name, value):
                (root / name).write_text(json.dumps(value), encoding="utf-8")
            for name in ("glyph.png", "clip.png", "branch.png", "unused.png", "preview.png", "marker.png"):
                Image.new("RGBA", (2, 2), "white").save(root / name)
            write("gameplay-skin.json", {"scene": "root.json", "contentCatalog": "catalog.json", "resources": []})
            write("root.json", {})
            (root / "skin.ini").write_text('decoration: resource Provide "marker"', encoding="utf-8")
            write("catalog.json", {"modules": [{"choices": [{"manifest": "module.json", "scene": "actual.json",
                "preview": "preview.png", "variants": [{"manifest": "module.json", "scene": "branch.json"}]}]}]})
            write("module.json", {"scene": "unused-scene-header.json", "resources": [
                {"id": "glyph", "path": "glyph.png"}, {"id": "texture", "path": "clip.png"},
                {"id": "branch", "path": "branch.png"}, {"id": "unused", "path": "unused.png"}],
                "bitmapFonts": [{"id": "font", "glyphs": [{"character": "0", "resource": "glyph"}]}],
                "animations": [{"id": "clip", "path": "clip.json", "resources": ["texture"]}]})
            write("actual.json", {"properties": {"bitmap-font": "font"}, "animation": "clip"})
            write("branch.json", {"resource": "branch"})
            write("clip.json", {"geometries": [{"resource": "texture"}]})
            collector = AuthorReferences(root, ReadLedger())
            rows = {row["path"]: row for row in collector.collect()}
            for path in ("glyph.png", "clip.png", "branch.png"):
                self.assertIn("runtime-scene-font-or-clip", rows[path]["kinds"])
            self.assertEqual(rows["unused.png"]["kinds"], ["declared"])
            self.assertEqual(rows["preview.png"]["kinds"], ["preview"])
            self.assertEqual(rows["marker.png"]["kinds"], ["runtime-ini-selector"])
            self.assertEqual(collector.catalogue_choices, 1)


class InputAndReceiptTest(unittest.TestCase):
    def test_atlas_original_pixels_require_current_glyph_or_uv_region_and_unchanged_crop(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            Image.new("RGBA", (2, 2), "red").save(root / "first.png")
            Image.new("RGBA", (2, 2), "blue").save(root / "second.png")
            atlas = Image.new("RGBA", (4, 2), "red")
            atlas.paste(Image.new("RGBA", (2, 2), "blue"), (2, 0))
            atlas.save(root / "atlas.png")
            ledger = ReadLedger()
            first = source("first", library=str(root), **ledger.image(root / "first.png"))
            second = source("second", library=str(root), **ledger.image(root / "second.png"))
            entries = [{"source": row["path"], "sourcePNG_SHA256": row["sha256"],
                        "rect": [index * 2, 0, 2, 2], "rgbaSHA256": row["rgbaSha256"]}
                       for index, row in enumerate((first, second))]
            receipt = root / "module-receipts.json"
            receipt.write_text(json.dumps({"sourceLibrary": str(root), "details": [{"kind": "lossless-glyph-atlas",
                "path": "atlas.png", "sourceGlyphs": entries}]}), encoding="utf-8")
            refs = [reference("atlas.png", "runtime-scene-font-or-clip", **ledger.image(root / "atlas.png"))]
            regions = {"atlas.png": [{"kind": "font-glyph", "rect": [0, 0, 2, 2]}]}
            edges, uncertain = atlas_derivations([first, second], refs, root, regions, [receipt], ledger)
            self.assertEqual(set(edges), {"first"})
            self.assertEqual(set(uncertain), {"second"})
            classify_sources([first, second], refs, edges, uncertain)
            self.assertEqual(first["classification"], "derived_reference")
            self.assertEqual(second["classification"], "evidence_insufficient")
            regions["atlas.png"].append({"kind": "recorded-clip-geometry-uv", "rect": [2, 0, 2, 2]})
            edges, uncertain = atlas_derivations([first, second], refs, root, regions, [receipt], ledger)
            self.assertEqual(set(edges), {"first", "second"})
            self.assertFalse(uncertain)
            atlas.putpixel((0, 0), (0, 255, 0, 255))
            atlas.save(root / "atlas.png")
            with self.assertRaises(ValueError):
                atlas_derivations([first, second], refs, root, regions, [receipt], ledger)

    def test_tint_requires_current_font_character_colour_and_matching_visible_pixels(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            white = Image.new("RGBA", (2, 1))
            white.putdata([(255, 255, 255, 255), (11, 22, 33, 0)])
            white.save(root / "white.png")
            green = Image.new("RGBA", (2, 1))
            green.putdata([(100, 255, 0, 255), (90, 80, 70, 0)])
            green.save(root / "green.png")
            ledger = ReadLedger()
            white_row = source("white", library=str(root), **ledger.image(root / "white.png"))
            green_row = source("green", library=str(root), **ledger.image(root / "green.png"))
            receipt = root / "tint.json"
            receipt.write_text(json.dumps({"contract": "oms-iidx-gameplay-control-source.v1", "sharedWhiteGreenFont": [
                {"digit": 0, "whiteSha256": white_row["sha256"], "greenSha256": green_row["sha256"]}]}), encoding="utf-8")
            refs = [reference("white.png", "runtime-scene-font-or-clip", sha256=white_row["sha256"])]
            glyphs = [{"manifest": "manifest.json", "scene": "scene.json", "font": "digits", "character": "0",
                       "colour": "#ffffffff", "path": "white.png"}]
            self.assertFalse(tint_derivations([white_row, green_row], refs, root, glyphs, [receipt], ledger))
            glyphs[0]["colour"] = "#64ff00ff"
            edges = tint_derivations([white_row, green_row], refs, root, glyphs, [receipt], ledger)
            self.assertTrue(edges["green"][0]["alphaEqual"])
            self.assertEqual(edges["green"][0]["kind"], "verified-visible-pixel-tint")
            glyphs[0]["character"] = "1"
            self.assertFalse(tint_derivations([white_row, green_row], refs, root, glyphs, [receipt], ledger))

    def test_traversal_absolute_and_overlapping_or_existing_output_are_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            (root / "input").mkdir()
            for name in ("../escape", "C:/file.png", "/file.png", "..\\escape"):
                with self.assertRaises(ValueError):
                    member(root, name)
            with self.assertRaises(ValueError):
                output_directory(root / "input" / "output", [root / "input"])
            with self.assertRaises(ValueError):
                output_directory(root, [root / "input"])
            output_directory(root / "new", [root / "input"])
            with self.assertRaises(ValueError):
                output_directory(root / "new", [root / "input"])

    def test_derivation_receipt_must_match_current_runtime_output_not_retired_or_preview(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            receipt = root / "receipt.json"
            receipt.write_text(json.dumps({"contract": "oms-omni-gameplay-control-assets.v1", "assets": [
                {"name": "edited", "source": "input.png", "sourceSha256": "input-hash", "path": "output.png",
                 "sha256": "output-hash", "derivation": {"operation": "crop"}}]}), encoding="utf-8")
            rows = [source("input", sha256="input-hash")]
            refs = [reference("output.png", "preview", sha256="output-hash")]
            edges, rejected = validated_derivations(rows, refs, root, [receipt], ReadLedger())
            self.assertFalse(edges)
            self.assertEqual(len(rejected), 1)
            refs[0]["kinds"] = ["runtime-scene-font-or-clip"]
            edges, rejected = validated_derivations(rows, refs, root, [receipt], ReadLedger())
            self.assertEqual(edges["input"][0]["operation"], "crop")
            self.assertFalse(rejected)
            refs[0]["sha256"] = "changed-current-output"
            edges, rejected = validated_derivations(rows, refs, root, [receipt], ReadLedger())
            self.assertFalse(edges)
            self.assertEqual(len(rejected), 1)


if __name__ == "__main__":
    unittest.main()
