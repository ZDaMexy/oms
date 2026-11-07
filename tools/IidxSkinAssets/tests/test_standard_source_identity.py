"""Source-qualified frame identities and immutable 22/24/26 composition inputs.

Only small synthetic archives, textures and recorded exports are used.
"""
import copy
from pathlib import Path
import unittest
from urllib.parse import unquote

from test_standard_source_composition import (SyntheticSources, add_package,
                                               catalog, checksum, fingerprints,
                                               make_library, read,
                                               research_composition, save,
                                               source_composition, write)


class SourceIdentityTest(unittest.TestCase):
    def test_existing_ids_are_unchanged_and_source22_does_not_replace_arc21(self):
        old = (*catalog.BASE_STYLES, *catalog.EARLIER_STYLES)
        for style in old:
            self.assertEqual(catalog.source_style(str(style)), style)
            self.assertEqual(catalog.frame_token(style), f"{style:02}")
            self.assertEqual(catalog.frame_id(style), f"iidx{style:02}")
            self.assertEqual(catalog.package_key(style), f"gmframe{style:02}")
        expected = {
            "source22-gmframe0": "source22/gmframe0",
            "source22-gmframe0-00": "source22/gmframe0_00",
            "source22-gmframe0-01": "source22/gmframe0_01",
            "source22-gmframe8": "source22/gmframe8",
            "source22-gmframe9": "source22/gmframe9",
            "source22-gmframe10": "source22/gmframe10",
        }
        for style, package in expected.items():
            self.assertEqual(catalog.source_style(style), style)
            self.assertEqual(catalog.frame_id(style), style)
            self.assertEqual(catalog.frame_token(style), style.removeprefix("source"))
            self.assertEqual(catalog.package_key(style), package)
        all_styles = (*old, *expected)
        for helper in (catalog.frame_id, catalog.frame_token, catalog.package_key):
            self.assertEqual(len({helper(s) for s in all_styles}), len(all_styles))
        self.assertEqual(catalog.source_style("03"), 3)

    def test_unknown_or_inferred_generation_keys_are_rejected(self):
        for value in (22, 20, 0, 27, True, 21.0, "iidx21", "22", "source22-gmframe0_00",
                      "source22-gmframe1", "source22-gmframe21", "../gmframe0", "２１"):
            with self.subTest(value=value), self.assertRaises(ValueError):
                catalog.source_style(value)

    def test_new_source_family_requires_the_complete_alias_contract(self):
        data = {"sourceComposition": {"contract": catalog.COMPOSITION22,
                                      "aliases24": copy.deepcopy(catalog.EARLIER_ALIASES),
                                      "aliases22": copy.deepcopy(catalog.SOURCE22_ALIASES)}}
        self.assertEqual(catalog.styles(data),
                         (3, 8, 12, 14, 16, 17, 19, 21, 23, 24, 25, 26, *catalog.SOURCE22_AVAILABLE_STYLES))
        self.assertEqual(catalog.families(data), ("0", "1", "s23", "s24", "s22"))
        self.assertEqual(catalog.art_identity(data), "IIDX22+IIDX24+IIDX26")
        self.assertEqual(catalog.family_suffix("s22"), "fs22")
        data["sourceComposition"]["aliases22"]["gmframe10"] = "gmframe21"
        with self.assertRaisesRegex(ValueError, "Unrecognized"):
            catalog.styles(data)

    def test_bare_source22_is_retained_for_research_but_not_a_playable_theme(self):
        data = {"sourceComposition": {"contract": catalog.COMPOSITION22,
                                      "aliases24": copy.deepcopy(catalog.EARLIER_ALIASES),
                                      "aliases22": copy.deepcopy(catalog.SOURCE22_ALIASES)}}
        bare = "source22-gmframe0"
        self.assertEqual(len(catalog.SOURCE22_STYLES), 6)
        self.assertEqual(catalog.SOURCE22_AVAILABLE_STYLES,
                         ("source22-gmframe0-00", "source22-gmframe0-01", "source22-gmframe8",
                          "source22-gmframe9", "source22-gmframe10"))
        self.assertEqual(len(catalog.retained_styles(data)), 18)
        self.assertEqual(len(catalog.styles(data)), 17)
        self.assertEqual(set(catalog.retained_styles(data)) - set(catalog.styles(data)), {bare})
        self.assertEqual(catalog.frame_id(catalog.source_style(bare)), bare)
        self.assertEqual(catalog.package_key(bare), "source22/gmframe0")
        self.assertEqual(catalog.layout_family(bare), "s22")
        self.assertEqual(catalog.presentation_family(bare), "s22")
        for old in ({}, {"sourceComposition": {"contract": catalog.COMPOSITION,
                                               "aliases24": copy.deepcopy(catalog.EARLIER_ALIASES)}}):
            self.assertEqual(catalog.styles(old), catalog.retained_styles(old))

    def test_source22_steady_centres_use_their_supplied_hdd_geometry(self):
        for style in catalog.SOURCE22_STYLES:
            self.assertEqual(catalog.layout_family(style), "s22")
            self.assertEqual(catalog.presentation_family(style), "s22")
            self.assertEqual(catalog.centre_translation(style, "p1"), 35)
            self.assertEqual(catalog.centre_translation(style, "p2"), -35)
            self.assertEqual(catalog.centre_translation(style, "dp"), 0)
            self.assertEqual(catalog.centre_translation(style, "p1", wide=True), 0)
        self.assertEqual(catalog.centre_translation(26, "p1"), 36)
        self.assertEqual(catalog.centre_translation(23, "p2"), -35)


class Source22Fixture(SyntheticSources):
    def make_source22(self):
        self.source22 = self.root / "library22"
        self.source22_data = make_library(self.source22, "data/graphic", catalog.SOURCE22_ALIASES)
        self.extended = self.root / "extended-library"

    def extend(self):
        return source_composition.extend(self.output, self.source22, self.extended)

    def make_research22(self):
        self.old_research = self.make_research()
        self.make_source22()
        self.extend()
        source = self.root / "research22"
        data = read(self.extended / "library.json")
        save(source / "source.json", {"librarySha256": checksum((self.extended / "library.json").read_bytes())})
        for style in catalog.SOURCE22_STYLES:
            key = catalog.package_key(style)
            folder = source / key
            texture = next(t for t in data["textures"] if t["package"] == f"contents/data/graphic/{key}.ifs")
            sprite = self.extended / unquote(texture["sprite"])
            closure = {"files": [{"path": texture["sprite"], "bytes": sprite.stat().st_size, "sha256": texture["sha256"]}]}
            save(source / f"sources-{catalog.frame_token(style)}.json", closure)
            save(folder / "recorded/sources.json", closure)
            save(folder / "skin-reference-map.json", {"package": key, "common_family": "s22"})
            for run in ("run.json", "recorded/run.json"):
                save(folder / run, {"status": "complete"})
            export = {}
            for mode in ("motion", "steady"):
                clip = write(folder / f"recorded/movie/{mode}.clip.json.br", f"synthetic {style}/{mode}".encode())
                export[mode] = {"node": {"clock": "presentation.elapsed", "x": 0.125, "w": 0.25},
                                "files": [{"source": str(clip), "path": f"{style}/{mode}.clip.json.br",
                                           "sha256": checksum(clip.read_bytes())},
                                          {"source": str(sprite), "path": f"{style}/sprite.png", "sha256": texture["sha256"]}]}
            save(folder / "recorded/movie/export.json", export)
        self.research22 = source

    def join22(self):
        return research_composition.compose(self.extended, *self.old_research,
                                            self.root / "joined-research22", research22=self.research22)


class Source22CompositionTest(Source22Fixture):
    def test_appends_selected_sources_without_relabelling_or_changing_old_payload(self):
        self.compose()
        self.make_source22()
        before = [fingerprints(p) for p in (self.base, self.earlier, self.output, self.source22)]
        result = self.extend()
        self.assertEqual(result["importedPackages"], 24)
        data = read(self.extended / "library.json")
        old = read(self.output / "library.json")
        self.assertEqual(data["packages"][:len(old["packages"])], old["packages"])
        self.assertEqual(data["textures"][:len(old["textures"])], old["textures"])
        receipt = data["sourceComposition"]
        self.assertEqual(read(self.extended / "run.json")["contract"], catalog.COMPOSITION22)
        self.assertEqual((self.extended / receipt["previousComposition"]["path"]).read_bytes(),
                         (self.output / "library.json").read_bytes())
        self.assertEqual(receipt["previousComposition"]["sha256"], checksum((self.output / "library.json").read_bytes()))
        self.assertEqual(source_composition.retained_base(self.extended, data), old)
        for relative, digest in fingerprints(self.output).items():
            if relative not in ("library.json", "packages.json", "run.json", "index.html"):
                self.assertEqual(checksum((self.extended / relative).read_bytes()), digest)
        for original, alias in catalog.SOURCE22_ALIASES.items():
            logical = "contents/data/graphic/" + alias + ".ifs"
            package = next(p for p in data["packages"] if p["source"] == logical)
            self.assertEqual(package["origin"], {"library": "iidx22", "path": "data/graphic/" + original + ".ifs",
                                                 "archiveSHA256": package["sha256"]})
            self.assertEqual((self.extended / "source-copies" / logical).read_bytes(),
                             (self.source22 / "source-copies" / package["origin"]["path"]).read_bytes())
        self.assertIn("not six native menu choices", receipt["scope"])
        self.assertIn("without a proved native menu route", receipt["scope"])
        self.assertEqual(before, [fingerprints(p) for p in (self.base, self.earlier, self.output, self.source22)])

    def test_rejects_missing_and_unsupported_source22_packages_before_copying(self):
        self.compose()
        self.make_source22()
        saved = copy.deepcopy(self.source22_data)
        for mutation in ("missing", "extra", "duplicate"):
            data = copy.deepcopy(saved)
            if mutation == "missing":
                data["packages"].pop()
            elif mutation == "extra":
                add_package(self.source22, data, "gmframe11")
            else:
                data["packages"].append(copy.deepcopy(data["packages"][0]))
            save(self.source22 / "library.json", data)
            with self.subTest(mutation=mutation), self.assertRaisesRegex(ValueError, "qualified 22 package selection"):
                self.extend()
            self.assertFalse(self.extended.exists())

    def test_rejects_changed_archives_and_retained_original_manifest_before_copying(self):
        self.compose()
        self.make_source22()
        for path in (self.source22 / "source-copies/data/graphic/gmframe0.ifs",
                     self.output / "source-imports/iidx24-library.json"):
            saved = path.read_bytes()
            path.write_bytes(saved + b" ")
            with self.subTest(path=path.name), self.assertRaises(ValueError):
                self.extend()
            path.write_bytes(saved)
            self.assertFalse(self.extended.exists())

    def test_failure_does_not_publish_success_metadata(self):
        self.compose()
        self.make_source22()
        texture = self.source22_data["textures"][0]
        (self.source22 / unquote(texture["sprite"])).write_bytes(b"changed sprite")
        with self.assertRaisesRegex(ValueError, "Imported sprite changed"):
            self.extend()
        for name in ("library.json", "packages.json", "run.json", "index.html"):
            self.assertFalse((self.extended / name).exists())

    def test_extension_cannot_accept_itself_as_the_previous_24_26_library(self):
        self.compose()
        self.make_source22()
        self.extend()
        with self.assertRaisesRegex(ValueError, "existing 24/26"):
            source_composition.extend(self.extended, self.source22, self.root / "second-extension")

    def test_retained_manifest_is_not_accepted_by_hash_alone(self):
        self.compose()
        self.make_source22()
        self.extend()
        data = read(self.extended / "library.json")
        prior = self.extended / data["sourceComposition"]["previousComposition"]["path"]
        saved = prior.read_bytes()
        prior.write_bytes(saved + b" ")
        with self.assertRaisesRegex(ValueError, "manifest changed"):
            source_composition.retained_base(self.extended, data)
        old = read(prior)
        old["textures"][0]["width"] += 1
        save(prior, old)
        data["sourceComposition"]["previousComposition"]["sha256"] = checksum(prior.read_bytes())
        with self.assertRaisesRegex(ValueError, "unchanged base"):
            source_composition.retained_base(self.extended, data)


class Source22ResearchTest(Source22Fixture):
    def test_independent_22_research_preserves_older_source_identities_and_nested_paths(self):
        self.make_research22()
        sources = [*self.old_research, self.research22]
        before = [fingerprints(p) for p in sources]
        self.assertEqual(self.join22()["frames"], 18)
        joined = self.root / "joined-research22"
        receipt = read(joined / "source-composition.json")
        mappings = read(joined / "frame-maps.json")
        source_data = read(self.extended / "library.json")
        self.assertEqual(set(mappings), {catalog.frame_id(s) for s in catalog.retained_styles(source_data)})
        self.assertEqual(len(catalog.styles(source_data)), 17)
        self.assertIn("source22-gmframe0", mappings)
        self.assertNotEqual(mappings["iidx21"], mappings["source22-gmframe10"])
        old_identity = checksum((self.output / "library.json").read_bytes())
        new_identity = checksum((self.extended / "library.json").read_bytes())
        for frame in receipt["frames"]:
            style = frame["style"]
            if style in (23, 24):
                self.assertEqual(frame["originalSourceIdentity"]["librarySha256"], old_identity)
            if style in catalog.SOURCE22_STYLES:
                self.assertEqual(frame["originalSourceIdentity"]["librarySha256"], new_identity)
                folder = joined / catalog.package_key(style)
                self.assertEqual(mappings[catalog.frame_id(style)], str(folder / "skin-reference-map.json"))
                export = read(folder / "recorded/movie/export.json")
                original = read(self.research22 / catalog.package_key(style) / "recorded/movie/export.json")
                for mode in ("motion", "steady"):
                    self.assertEqual(export[mode]["node"], original[mode]["node"])
                    for row in export[mode]["files"]:
                        expected = joined if row["path"].endswith(".br") else self.extended
                        self.assertTrue(Path(row["source"]).is_relative_to(expected))
                        self.assertEqual(checksum(Path(row["source"]).read_bytes()), row["sha256"])
        self.assertEqual(before, [fingerprints(p) for p in sources])

    def test_source22_cannot_borrow_the_old_composed_library_identity(self):
        self.make_research22()
        save(self.research22 / "source.json", read(self.old_research[1] / "source.json"))
        with self.assertRaisesRegex(ValueError, "source manifest"):
            self.join22()
        self.assertFalse((self.root / "joined-research22").exists())

    def test_research22_is_required_only_for_the_explicit_extension(self):
        self.make_research22()
        with self.assertRaisesRegex(ValueError, "separate research22"):
            research_composition.compose(self.extended, *self.old_research, self.root / "missing22")
        with self.assertRaisesRegex(ValueError, "separate research22"):
            research_composition.compose(self.output, *self.old_research, self.root / "wrong22", self.research22)

    def test_retained_identity_does_not_excuse_changed_source_bytes(self):
        self.make_research22()
        closure = read(self.old_research[1] / "sources-23.json")
        (self.extended / unquote(closure["files"][0]["path"])).write_bytes(b"changed prior source")
        with self.assertRaisesRegex(ValueError, "Source bytes changed"):
            self.join22()
        self.assertFalse((self.root / "joined-research22").exists())

    def test_source22_package_must_keep_its_full_identity(self):
        self.make_research22()
        path = self.research22 / "source22/gmframe0/skin-reference-map.json"
        value = read(path)
        value["package"] = "gmframe0"
        save(path, value)
        with self.assertRaisesRegex(ValueError, "common texture family"):
            self.join22()
        self.assertFalse((self.root / "joined-research22").exists())


if __name__ == "__main__":
    unittest.main()
