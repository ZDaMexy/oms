"""Behaviour checks for independent option illustrations; no game art needed."""

import hashlib
import json
from pathlib import Path
import shutil
import sys
import tempfile
import unittest

from PIL import Image

sys.dont_write_bytecode = True
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from choice_previews import Illustration, audit_previews, build_choice_previews, exact_file
from build_omni_skin import load_modules, package_closure


class ChoicePreviewTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="oms-choice-previews-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()

    def json(self, name, value):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding="utf-8")

    def notes(self, value, colour):
        resources, templates = [], []
        for lane in ("white", "black", "scratch"):
            identifier, relative = value + "." + lane, f"art/{value}-{lane}.png"
            path = self.root / relative
            path.parent.mkdir(parents=True, exist_ok=True)
            Image.new("RGBA", (52, 15), colour).save(path)
            resources.append({"id": identifier, "path": relative})
            templates.append({"root": {"id": identifier, "resource": identifier, "children": []}})
        self.json(f"config/{value}-manifest.json", {"contract": "oms-gameplay-skin-manifest.v1", "resources": resources})
        self.json(f"config/{value}-scene.json", {"templates": templates})
        return {"value": value, "label": value, "manifest": f"config/{value}-manifest.json", "scene": f"config/{value}-scene.json"}

    def prepare(self):
        frame = self.root / "art/frame/backgrounds/dp.png"
        frame.parent.mkdir(parents=True)
        Image.new("RGBA", (1920, 1080), "green").save(frame)
        self.json("config/frame-manifest.json", {"resources": [{"id": "frame", "path": "art/frame/backgrounds/dp.png"}]})
        self.json("config/frame-scene.json", {})
        self.original_library = self.root / "originals"
        original = self.original_library / "fcombo00.jpg"
        original.parent.mkdir()
        Image.new("RGB", (140, 220), "yellow").save(original)
        self.json("originals/original-previews.json", [{"source": "contents/data/graphic/customize/fcombo00.jpg", "copy": "fcombo00.jpg",
                                                      "sha256": hashlib.sha256(original.read_bytes()).hexdigest(), "size": [140, 220]}])
        self.json("gameplay/content.json", {"contract": "oms-gameplay-skin-content.v1", "modules": [
            {"id": "notes", "choices": [self.notes("n00", "red"), self.notes("n01", "blue")]},
            {"id": "frame", "choices": [{"value": "iidx33", "label": "33", "manifest": "config/frame-manifest.json", "scene": "config/frame-scene.json"}]},
            {"id": "full-combo", "choices": [{"value": "fc00", "label": "FC"}]}]})
        self.json("gameplay-skin.json", {"contract": "oms-gameplay-skin-manifest.v1", "contentCatalog": "gameplay/content.json"})
        self.json("gameplay-skin.scene.json", {})
        (self.root / "skin.ini").write_text("[General]\nName: Preview fixture\n", encoding="utf-8")

    def test_each_design_has_its_own_visible_preview_and_originals_stay_exact(self):
        self.prepare()
        before = {str(path.relative_to(self.root)): hashlib.sha256(path.read_bytes()).digest()
                  for path in self.root.rglob("*") if path.is_file() and path.name != "content.json"}
        report = build_choice_previews(self.root, self.original_library)
        catalog = json.loads((self.root / "gameplay/content.json").read_text(encoding="utf-8"))
        choices = catalog["modules"][0]["choices"]
        first, second = (self.root / choice["preview"] for choice in choices)
        self.assertNotEqual(first.read_bytes(), second.read_bytes())
        self.assertEqual({"notes": 2, "frame": 1, "full-combo": 1}, report["perModule"])
        self.assertTrue(all("preview" in item for module in catalog["modules"] for item in module["choices"]))
        self.assertEqual(4, audit_previews(self.root, catalog)["files"])
        for relative, digest in before.items():
            self.assertEqual(digest, hashlib.sha256((self.root / relative).read_bytes()).digest(), relative)
        self.assertEqual(report, build_choice_previews(self.root, self.original_library))

    def test_existing_fc_thumbnail_repackages_without_reopening_original_custom_library(self):
        self.prepare()
        build_choice_previews(self.root, self.original_library)
        path = self.root / "previews/full-combo/fc00.png"
        before = path.read_bytes()
        shutil.rmtree(self.original_library)
        build_choice_previews(self.root)
        self.assertEqual(before, path.read_bytes())

    def test_fc_original_preview_requires_its_exact_source_receipt(self):
        self.prepare()
        with self.assertRaisesRegex(ValueError, "choice-preview-library"):
            build_choice_previews(self.root)
        (self.original_library / "fcombo00.jpg").write_bytes(b"changed original")
        with self.assertRaisesRegex(ValueError, "source receipt"):
            build_choice_previews(self.root, self.original_library)

    def test_cover_graph_and_lift_share_art_bytes_while_graph_blank_stays_distinct(self):
        image = self.root / "cover.png"
        Image.new("RGBA", (80, 120), "purple").save(image)
        self.json("art.json", {"resources": [{"id": "cover", "path": "cover.png"}]})
        self.json("scene.json", {"templates": [{"root": {"id": "cover.art", "resource": "cover"}}]})
        self.json("empty.json", {"resources": []})
        self.json("empty-scene.json", {})
        modules = [{"id": module, "choices": [{"value": "l000", "manifest": "art.json", "scene": "scene.json"}]}
                   for module in ("lane-cover", "graph-area", "lift-art")]
        modules[1]["choices"].append({"value": "blank", "manifest": "empty.json", "scene": "empty-scene.json"})
        self.json("gameplay/content.json", {"modules": modules})
        report = build_choice_previews(self.root)
        catalog = json.loads((self.root / "gameplay/content.json").read_text(encoding="utf-8"))
        paths = [module["choices"][0]["preview"] for module in catalog["modules"]]
        self.assertEqual(1, len(set(paths)))
        self.assertEqual(4, report["choices"])
        self.assertEqual(2, report["uniqueFiles"])
        self.assertEqual(2, audit_previews(self.root, catalog)["files"])
        empty = self.root / catalog["modules"][1]["choices"][1]["preview"]
        self.assertNotEqual(empty.read_bytes(), (self.root / paths[0]).read_bytes())

    def test_animated_cover_uses_exact_original_custom_card_and_reuses_it_after_repackaging(self):
        self.prepare()
        original = self.original_library / "lane306_anim.jpg"
        Image.new("RGB", (140, 220), "cyan").save(original)
        self.json("originals/original-previews.json", [{"source": "contents/data/graphic/customize/lane306_anim.jpg",
                                                      "copy": original.name, "sha256": hashlib.sha256(original.read_bytes()).hexdigest(), "size": [140, 220]}])
        self.json("animated.json", {"resources": [{"id": "unused", "path": "not-needed.png"}], "animations": [{"id": "clip"}]})
        self.json("animated-scene.json", {"templates": [{"root": {"id": "cover.art", "animation": "clip"}}]})
        self.json("gameplay/content.json", {"modules": [{"id": module, "choices": [
            {"value": "l306", "manifest": "animated.json", "scene": "animated-scene.json"}]}
            for module in ("lane-cover", "graph-area", "lift-art")]})
        report = build_choice_previews(self.root, self.original_library)
        self.assertEqual(1, report["uniqueFiles"])
        preview = self.root / "previews/lane-cover/l306.png"
        before = preview.read_bytes()
        shutil.rmtree(self.original_library)
        self.assertEqual(1, build_choice_previews(self.root)["uniqueFiles"])
        self.assertEqual(before, preview.read_bytes())

    def test_repackaging_reference_closure_keeps_the_preview(self):
        self.prepare()
        build_choice_previews(self.root, self.original_library)
        closure = package_closure(self.root)
        self.assertIn("previews/notes/n00.png", closure)
        self.assertIn("previews/notes/n01.png", closure)
        self.assertIn("previews/frame/iidx33.png", closure)
        self.assertIn("previews/full-combo/fc00.png", closure)

    def test_opaque_black_canvas_does_not_beat_an_actual_visible_frame(self):
        self.json("manifest.json", {"resources": [{"id": "black", "path": "black.png"},
                                                  {"id": "visible", "path": "visible.png"}]})
        self.json("scene.json", {})
        Image.new("RGBA", (80, 80), "black").save(self.root / "black.png")
        Image.new("RGBA", (10, 10), "red").save(self.root / "visible.png")
        illustration = Illustration(self.root, {"manifest": "manifest.json", "scene": "scene.json"})
        image = illustration.strongest(["black", "visible"])
        self.assertEqual((10, 10), image.size)
        self.assertEqual("visible.png", illustration.sources[0]["path"])

    def test_frozen_module_export_needs_no_original_library_or_animation_exporter(self):
        self.prepare()
        catalog = json.loads((self.root / "gameplay/content.json").read_text(encoding="utf-8"))
        frozen, output = (self.root / name for name in ("frozen", "fresh"))
        frozen.mkdir()
        output.mkdir()
        rows = []
        for directory in ("art", "config"):
            for source in (self.root / directory).rglob("*"):
                if not source.is_file():
                    continue
                relative = source.relative_to(self.root).as_posix()
                target = frozen / relative
                target.parent.mkdir(parents=True, exist_ok=True)
                shutil.copyfile(source, target)
                rows.append({"path": relative, "bytes": source.stat().st_size,
                             "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
        self.json("frozen-module-author-data/files.json", rows)
        self.json("frozen-module-author-data/modules.json", {"contract": catalog["contract"], "modules": catalog["modules"][:1]})
        self.json("frozen-module-author-data/options.json", [])
        self.json("frozen-module-author-data/receipts.json", [])
        loaded = load_modules(frozen, output)
        (output / "gameplay").mkdir()
        (output / "gameplay/content.json").write_text(json.dumps({"modules": loaded["modules"]}), encoding="utf-8")
        report = build_choice_previews(output)
        self.assertEqual(2, report["choices"])
        self.assertTrue((output / "previews/notes/n01.png").is_file())

    def test_escape_and_oversized_art_cannot_enter_preview_library(self):
        self.prepare()
        with self.assertRaises(ValueError):
            exact_file(self.root, str(Path(__file__).resolve()))
        too_large = self.root / "oversized.png"
        Image.new("RGBA", (513, 1), "red").save(too_large)
        catalog = {"modules": [{"choices": [{"preview": "oversized.png"}]}]}
        with self.assertRaisesRegex(ValueError, "dimensions"):
            audit_previews(self.root, catalog)


if __name__ == "__main__":
    unittest.main()
