"""Synthetic source provenance, selection and research-rebinding boundaries.

No game archives or artwork are read. Only the decoder is substituted in the
library-selection tests; composition and filesystem checks use real files.
"""
import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch
from urllib.parse import quote, unquote

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_library
import compose_standard_research as research_composition
import compose_standard_sources as source_composition
import standard_source_catalog as catalog


def checksum(content):
    return hashlib.sha256(content).hexdigest()


def write(path, content):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(content)
    return path


def save(path, value):
    return write(path, (json.dumps(value, ensure_ascii=False, indent=2) + "\n").encode("utf-8"))


def read(path):
    return json.loads(path.read_text(encoding="utf-8"))


def fingerprints(root):
    return {p.relative_to(root).as_posix(): checksum(p.read_bytes())
            for p in root.rglob("*") if p.is_file()}


def add_package(root, data, package):
    source = data["graphicRoot"] + "/" + package + ".ifs"
    archive = ("synthetic archive: " + source).encode()
    write(root / "source-copies" / source, archive)
    prefix = "packages/" + source.removesuffix(".ifs")
    # Encoded spaces/non-ASCII/# exercise the real library URL path convention.
    name = "shared 标记 #.png"
    sprite, raw = prefix + "/sprites/" + name, prefix + "/raw/tex/" + name
    pixels = ("synthetic undecoded image: " + source).encode()
    write(root / sprite, pixels)
    write(root / raw, b"raw atlas: " + pixels)
    write(root / prefix / "raw/afp/frame", b"synthetic AFP: " + archive)
    data["packages"].append({"source": source, "sha256": checksum(archive),
                             "sourceUnchanged": True, "spriteCount": 1})
    data["textures"].append({"package": source, "name": name,
                             "sprite": quote(sprite, safe="/"), "raw": quote(raw, safe="/"),
                             "sha256": checksum(pixels), "width": 2, "height": 3,
                             "imageRect": [10, 14, 20, 25], "uvRect": [11, 13, 21, 24]})
    data["sourceFileCount"] += 1


def make_library(root, graphic_root, packages):
    data = {"status": "complete", "sourceMetadataUnchanged": True,
            "graphicRoot": graphic_root, "sourceFileCount": 0, "packages": [], "textures": []}
    for package in packages:
        add_package(root, data, package)
    save(root / "library.json", data)
    save(root / "packages.json", data["packages"])
    save(root / "run.json", {"status": "complete"})
    write(root / "index.html", b"original complete library browser")
    return data


class SyntheticSources(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="standard-source-composition-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name).resolve()
        self.base, self.earlier = self.root / "library26", self.root / "library24"
        self.output = self.root / "composed"
        self.base_data = make_library(self.base, "contents/data/graphic",
                                      [*(f"gmframe{s:02}" for s in catalog.BASE_STYLES), "0/graph", "1/graph", "fcombo00"])
        self.earlier_data = make_library(self.earlier, "data/graphic", catalog.EARLIER_ALIASES)

    def compose(self):
        return source_composition.compose(self.base, self.earlier, self.output)

    def make_research(self):
        self.compose()
        data = read(self.output / "library.json")
        research = [self.root / name for name in ("old-research", "research23", "research24")]
        base_identity = data["sourceComposition"]["inputManifestSHA256"]["iidx26"]
        new_identity = checksum((self.output / "library.json").read_bytes())
        self.research_sprites = {}
        for source, styles, identity in zip(research, (catalog.BASE_STYLES, (23,), (24,)),
                                            (base_identity, new_identity, new_identity), strict=True):
            save(source / "source.json", {"librarySha256": identity})
            for style in styles:
                folder = source / f"gmframe{style:02}"
                package = f"contents/data/graphic/gmframe{style:02}.ifs"
                texture = next(t for t in data["textures"] if t["package"] == package)
                relative = unquote(texture["sprite"])
                original_library = self.base if style in catalog.BASE_STYLES else self.output
                old_sprite = original_library / relative
                self.research_sprites[style] = old_sprite
                closure = {"files": [{"path": texture["sprite"], "bytes": old_sprite.stat().st_size,
                                       "sha256": texture["sha256"]}]}
                save(source / f"sources-{style:02}.json", closure)
                save(folder / "recorded/sources.json", closure)
                save(folder / "skin-reference-map.json",
                     {"package": folder.name, "common_family": catalog.layout_family(style),
                      "syntheticGeometry": {"matrix": [1, 0, 0, 1, -2, 7], "uv": [0.1, 0.3, 0.4, 0.8]}})
                for relative_run in ("run.json", "recorded/run.json"):
                    save(folder / relative_run, {"status": "complete"})
                movie = folder / "recorded/movie"
                export = {}
                for mode in ("motion", "steady"):
                    clip = write(movie / (mode + ".clip.json.br"), f"synthetic {style} {mode} clip".encode())
                    export[mode] = {"files": [
                        {"source": str(clip), "path": "author/" + clip.name,
                         "sha256": checksum(clip.read_bytes())},
                        {"source": str(old_sprite), "path": "author/shared.png",
                         "sha256": texture["sha256"]}],
                        "node": {"clock": "presentation.elapsed", "syntheticMatrix": [1, 0, 0, 1, 5, -8]}}
                save(movie / "export.json", export)
        return research

    def join_research(self, inputs):
        return research_composition.compose(self.output, *inputs, self.root / "joined-research")


class SourceCompositionTest(SyntheticSources):
    def test_aliases_preserve_bytes_origin_and_inputs_without_importing_unselected_packages(self):
        for extra in ("gmframe33", "2/gameparts", "1/graph", "notes99"):
            add_package(self.earlier, self.earlier_data, extra)
        save(self.earlier / "library.json", self.earlier_data)
        payload = save(self.base / "packages/contents/data/graphic/gmframe26/raw/run.json", {"sourcePayload": True})
        before = fingerprints(self.base), fingerprints(self.earlier)
        result = self.compose()
        data = read(self.output / "library.json")
        self.assertEqual((self.output / payload.relative_to(self.base)).read_bytes(), payload.read_bytes())
        self.assertEqual(read(self.output / "packages.json"), data["packages"])
        self.assertEqual(read(self.output / "run.json")["contract"], catalog.COMPOSITION)
        self.assertEqual(result["importedPackages"], 13)
        self.assertFalse(result["payloadBytesChanged"])
        expected = {p["source"] for p in self.base_data["packages"]} | {
            "contents/data/graphic/" + alias + ".ifs" for alias in catalog.EARLIER_ALIASES.values()}
        self.assertEqual({p["source"] for p in data["packages"]}, expected)
        self.assertEqual(set(p.relative_to(self.output / "source-copies").as_posix()
                             for p in (self.output / "source-copies").rglob("*.ifs")), expected)
        for label, source in (("iidx26", self.base), ("iidx24", self.earlier)):
            manifest = (source / "library.json").read_bytes()
            self.assertEqual((self.output / "source-imports" / (label + "-library.json")).read_bytes(), manifest)
            self.assertEqual(data["sourceComposition"]["inputManifestSHA256"][label], checksum(manifest))
        for package in data["packages"]:
            origin = package["origin"]
            original = (self.base if origin["library"] == "iidx26" else self.earlier) / "source-copies" / origin["path"]
            self.assertEqual((self.output / "source-copies" / package["source"]).read_bytes(), original.read_bytes())
            self.assertEqual(origin["archiveSHA256"], checksum(original.read_bytes()))
        imported = next(t for t in data["textures"] if t["package"] == "contents/data/graphic/s23/graph.ifs")
        original = next(t for t in self.earlier_data["textures"] if t["package"] == "data/graphic/graph.ifs")
        self.assertEqual(imported["originPackage"], original["package"])
        for field in ("sprite", "raw"):
            self.assertEqual((self.output / unquote(imported[field])).read_bytes(),
                             (self.earlier / unquote(original[field])).read_bytes())
        for field in ("width", "height", "imageRect", "uvRect", "sha256"):
            self.assertEqual(imported[field], original[field])
        combo_sources = {p["source"]: p["origin"]["library"] for p in data["packages"]
                         if Path(p["source"]).name == "fcombo00.ifs"}
        self.assertEqual(combo_sources, {"contents/data/graphic/fcombo00.ifs": "iidx26",
                                        "contents/data/graphic/source24/fcombo00.ifs": "iidx24"})
        for row in data["sourceComposition"]["importedFiles"]:
            self.assertEqual(checksum((self.output / row["path"]).read_bytes()), row["sha256"])
            self.assertEqual((self.output / row["path"]).read_bytes(), (self.earlier / row["origin"]).read_bytes())
        self.assertEqual(before, (fingerprints(self.base), fingerprints(self.earlier)))

    def test_rejects_swapped_source_layouts(self):
        with self.assertRaisesRegex(ValueError, "source layouts"):
            source_composition.compose(self.earlier, self.base, self.output)
        self.assertFalse(self.output.exists())

    def test_rejects_unapproved_base_frame(self):
        add_package(self.base, self.base_data, "gmframe33")
        save(self.base / "library.json", self.base_data)
        with self.assertRaisesRegex(ValueError, "ten-theme"):
            self.compose()

    def test_rejects_unapproved_base_common_family(self):
        add_package(self.base, self.base_data, "2/graph")
        save(self.base / "library.json", self.base_data)
        with self.assertRaises(ValueError):
            self.compose()

    def test_rejects_changed_source_archives_from_either_hdd(self):
        for label, source in (("26", self.base), ("24", self.earlier)):
            with self.subTest(source=label):
                data = read(source / "library.json")
                archive = source / "source-copies" / data["packages"][0]["source"]
                saved = archive.read_bytes()
                archive.write_bytes(b"!" + saved[1:])
                try:
                    output = self.root / ("bad-archive-" + label)
                    with self.assertRaisesRegex(ValueError, "archive differs"):
                        source_composition.compose(self.base, self.earlier, output)
                    self.assertFalse(output.exists(), "Corrupt archives must fail before copying the base library")
                finally:
                    archive.write_bytes(saved)

    def test_late_failure_does_not_publish_original_complete_metadata(self):
        texture = self.earlier_data["textures"][0]
        sprite = self.earlier / unquote(texture["sprite"])
        sprite.write_bytes(b"!" + sprite.read_bytes()[1:])
        with self.assertRaisesRegex(ValueError, "Imported sprite changed"):
            self.compose()
        for name in ("library.json", "packages.json", "index.html", "run.json"):
            self.assertFalse((self.output / name).exists(), name + " must only describe a completed composition")

    def test_rejects_changed_extracted_sprite_from_either_hdd(self):
        for label, source in (("26", self.base), ("24", self.earlier)):
            with self.subTest(source=label):
                texture = read(source / "library.json")["textures"][0]
                sprite = source / unquote(texture["sprite"])
                saved = sprite.read_bytes()
                sprite.write_bytes(b"!" + saved[1:])
                try:
                    with self.assertRaises(ValueError):
                        source_composition.compose(self.base, self.earlier, self.root / ("bad-sprite-" + label))
                finally:
                    sprite.write_bytes(saved)

    def test_texture_cannot_claim_another_approved_package(self):
        self.earlier_data["textures"][0]["sprite"] = self.earlier_data["textures"][1]["sprite"]
        save(self.earlier / "library.json", self.earlier_data)
        with self.assertRaisesRegex(ValueError, "declared source package"):
            self.compose()

    def test_encoded_source_path_cannot_leave_library(self):
        outside = write(self.root / "outside.bin", b"outside")
        for relative in ("..%2Foutside.bin", str(outside)):
            with self.subTest(relative=relative), self.assertRaisesRegex(ValueError, "leaves"):
                source_composition.inside(self.base, relative)

    def test_source_and_output_cannot_overlap(self):
        with self.assertRaisesRegex(ValueError, "overlap"):
            source_composition.compose(self.base, self.earlier, self.base / "new-output")
        self.assertFalse((self.base / "new-output").exists())

    def test_composed_or_incomplete_library_cannot_be_relabelled_as_original(self):
        for field, value in (("status", "running"), ("sourceMetadataUnchanged", False),
                             ("sourceComposition", {"contract": catalog.COMPOSITION})):
            with self.subTest(field=field):
                data = copy.deepcopy(self.base_data)
                data[field] = value
                save(self.base / "library.json", data)
                with self.assertRaisesRegex(ValueError, "original independently"):
                    self.compose()


class SourceCatalogTest(unittest.TestCase):
    def test_only_explicit_composition_enables_earlier_styles_and_families(self):
        self.assertEqual(catalog.styles({}), (3, 8, 12, 14, 16, 17, 19, 21, 25, 26))
        self.assertEqual(catalog.families({}), ("0", "1"))
        data = {"sourceComposition": {"contract": catalog.COMPOSITION, "aliases24": copy.deepcopy(catalog.EARLIER_ALIASES)}}
        self.assertEqual(catalog.styles(data), (3, 8, 12, 14, 16, 17, 19, 21, 23, 24, 25, 26))
        self.assertEqual(catalog.families(data), ("0", "1", "s23", "s24"))
        self.assertEqual([catalog.presentation_family(s) for s in (23, 24, 25, 26)], ["s23", "s24", "1", "0"])
        self.assertEqual(catalog.layout_family(25), "0")
        data["sourceComposition"]["aliases24"]["0/graph"] = "2/graph"
        with self.assertRaisesRegex(ValueError, "Unrecognized"):
            catalog.styles(data)
        with self.assertRaises(ValueError):
            catalog.family_suffix("2")


class ResearchCompositionTest(SyntheticSources):
    def test_rebases_recorded_paths_but_preserves_clips_geometry_and_original_receipts(self):
        inputs = self.make_research()
        original = [fingerprints(p) for p in inputs]
        result = self.join_research(inputs)
        joined = self.root / "joined-research"
        self.assertEqual(result["frames"], 12)
        receipt = read(joined / "source-composition.json")
        self.assertEqual(receipt["librarySha256"], checksum((self.output / "library.json").read_bytes()))
        self.assertEqual(set(read(joined / "frame-maps.json")), {f"iidx{s:02}" for s in (*catalog.BASE_STYLES, 23, 24)})
        for frame in receipt["frames"]:
            style = frame["style"]
            source = inputs[0 if style in catalog.BASE_STYLES else 1 if style == 23 else 2] / f"gmframe{style:02}"
            destination = joined / source.name
            self.assertEqual((source / "skin-reference-map.json").read_bytes(), (destination / "skin-reference-map.json").read_bytes())
            old = read(source / "recorded/movie/export.json")
            new = read(destination / "recorded/movie/export.json")
            for mode in ("motion", "steady"):
                self.assertEqual(old[mode]["node"], new[mode]["node"])
                for before, after in zip(old[mode]["files"], new[mode]["files"], strict=True):
                    self.assertEqual({k: v for k, v in before.items() if k != "source"},
                                     {k: v for k, v in after.items() if k != "source"})
                    self.assertEqual(Path(before["source"]).read_bytes(), Path(after["source"]).read_bytes())
                    self.assertTrue(Path(after["source"]).is_relative_to(joined if before["path"].endswith(".br") else self.output))
            self.assertEqual({p["path"]: p["sha256"] for p in frame["researchFiles"]}, fingerprints(source))
            self.assertEqual({p["path"]: p["sha256"] for p in frame["composedResearchFiles"]}, fingerprints(destination))
        self.assertEqual(original, [fingerprints(p) for p in inputs])

    def test_rejects_wrong_old_or_new_library_identity(self):
        inputs = self.make_research()
        for index in (0, 1):
            with self.subTest(index=index):
                path = inputs[index] / "source.json"
                saved = path.read_bytes()
                save(path, {"librarySha256": read(inputs[1 if index == 0 else 0] / "source.json")["librarySha256"]})
                with self.assertRaisesRegex(ValueError, "source manifest"):
                    self.join_research(inputs)
                path.write_bytes(saved)
                self.assertFalse((self.root / "joined-research").exists())

    def test_rejects_changed_composed_source_before_publishing_research(self):
        inputs = self.make_research()
        texture = next(t for t in read(self.output / "library.json")["textures"] if "gmframe26.ifs" in t["package"])
        sprite = self.output / unquote(texture["sprite"])
        sprite.write_bytes(b"!" + sprite.read_bytes()[1:])
        with self.assertRaisesRegex(ValueError, "Source bytes changed"):
            self.join_research(inputs)
        self.assertFalse((self.root / "joined-research").exists())

    def test_rejects_changed_old_recorded_input_even_when_new_library_is_intact(self):
        inputs = self.make_research()
        sprite = self.research_sprites[26]
        sprite.write_bytes(b"!" + sprite.read_bytes()[1:])
        with self.assertRaisesRegex(ValueError, "Rebound source differs"):
            self.join_research(inputs)
        self.assertFalse((self.root / "joined-research/source-composition.json").exists())

    def test_rejects_wrong_research_family_and_encoded_source_escape(self):
        inputs = self.make_research()
        reference = inputs[1] / "gmframe23/skin-reference-map.json"
        saved = reference.read_bytes()
        value = read(reference)
        value["common_family"] = "0"
        save(reference, value)
        with self.assertRaisesRegex(ValueError, "common texture family"):
            self.join_research(inputs)
        reference.write_bytes(saved)
        outside = write(self.root / "outside.bin", b"unapproved source")
        save(inputs[0] / "sources-03.json", {"files": [{"path": "..%2Foutside.bin", "bytes": outside.stat().st_size,
                                                       "sha256": checksum(outside.read_bytes())}]})
        with self.assertRaisesRegex(ValueError, "leaves"):
            self.join_research(inputs)
        self.assertFalse((self.root / "joined-research").exists())


class LibraryLayoutSelectionTest(unittest.TestCase):
    @staticmethod
    def parser_error(message):
        raise ValueError(message)

    @staticmethod
    def extract_stub(copied, output, image_class, ifs_class, cropped, **kwargs):
        output.mkdir(parents=True)
        if not cropped:
            write(output / "synthetic.afp", copied.read_bytes())
        return {}

    def test_library_keeps_selected_hdd_layout_and_excludes_the_other_root(self):
        for graphic_root, package in (("data/graphic", "gmframe24.ifs"),
                                      ("contents/data/graphic", "gmframe26.ifs")):
            with self.subTest(graphic_root=graphic_root), tempfile.TemporaryDirectory(prefix="standard-layout-") as directory:
                root = Path(directory).resolve()
                game = root / "game"
                for path in ("data/graphic/gmframe24.ifs", "contents/data/graphic/gmframe26.ifs"):
                    write(game / path, ("synthetic selected archive: " + path).encode())
                selected = graphic_root + "/" + package
                selection = save(root / "selection.json", [selected])
                args = SimpleNamespace(game_root=game, output=root / "library", selection=selection,
                                       graphic_root=graphic_root, ifstools_path=None)
                parser = SimpleNamespace(error=self.parser_error)
                with patch.object(build_library, "extract_copy", side_effect=self.extract_stub), contextlib.redirect_stdout(io.StringIO()):
                    build_library.build(args, parser)
                data = read(args.output / "library.json")
                self.assertEqual(data["graphicRoot"], graphic_root)
                self.assertEqual([p["source"] for p in data["packages"]], [selected])
                self.assertEqual([p["path"] for p in read(args.output / "source-inventory.json")], [selected])
                self.assertEqual((args.output / "source-copies" / selected).read_bytes(), (game / selected).read_bytes())
                wrong = "contents/data/graphic/gmframe26.ifs" if graphic_root == "data/graphic" else "data/graphic/gmframe24.ifs"
                save(selection, [wrong])
                args.output = root / "invalid-library"
                with patch.object(build_library, "extract_copy") as decoder, self.assertRaisesRegex(ValueError, "IFS inside"):
                    build_library.build(args, parser)
                decoder.assert_not_called()
                self.assertFalse(args.output.exists())


if __name__ == "__main__":
    unittest.main()
