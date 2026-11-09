"""Original20 provenance, distinct CUSTOM retention and predecessor research.

All payloads and source fingerprints are synthetic; no private assets are read.
"""
import copy
from pathlib import Path
import unittest
from urllib.parse import unquote

from test_standard21_source_composition import Original21Fixture
from test_standard_source_composition import (add_package, catalog, checksum, fingerprints,
                                              make_library, read, research_composition,
                                              save, source_composition, write)


class Original20Fixture(Original21Fixture):
    def make_source20(self):
        self.source20 = self.root / 'library20'
        self.data20 = make_library(self.source20, 'data/graphic',
                                   (*catalog.SOURCE20_CORE_ALIASES, *catalog.SOURCE20_CUSTOM_PACKAGES))
        for package in self.data20['packages']:
            name = Path(package['source']).stem
            if name in catalog.SOURCE20_CUSTOM_PACKAGES and name not in catalog.SOURCE20_DISTINCT_CUSTOM_ALIASES:
                original = self.base / 'source-copies/contents/data/graphic' / (name + '.ifs')
                content = original.read_bytes()
                write(self.source20 / 'source-copies' / package['source'], content)
                package['sha256'] = checksum(content)
            elif name in catalog.SOURCE20_DISTINCT_CUSTOM_ALIASES:
                for folder in ('afp', 'afp/bsi', 'geo'):
                    write(self.source20 / 'packages/data/graphic' / name / 'raw' / folder / 'own20',
                          ('original20 ' + name + ' ' + folder).encode())
        save(self.source20 / 'library.json', self.data20)
        self.extended20 = self.root / 'extended-library20'

    def prepare20(self):
        self.prepare21()
        self.extend21()
        self.make_source20()

    def extend20(self):
        return source_composition.extend20(self.extended21, self.source20, self.extended20)

    def make_research20(self):
        self.make_research21()
        self.make_source20()
        self.extend20()
        source = self.root / 'research20'
        data = read(self.extended20 / 'library.json')
        key = 'source20/gmframe0'
        folder = source / key
        save(source / 'source.json', {'librarySha256': checksum((self.extended20 / 'library.json').read_bytes()),
                                      'layoutMovies': ['dp_frame', 'center_frame_dp']})
        texture = next(t for t in data['textures'] if t['package'] == f'contents/data/graphic/{key}.ifs')
        sprite = self.extended20 / unquote(texture['sprite'])
        closure = {'files': [{'path': texture['sprite'], 'bytes': sprite.stat().st_size, 'sha256': texture['sha256']}]}
        save(source / 'sources-20-gmframe0.json', closure)
        save(folder / 'recorded/sources.json', closure)
        save(folder / 'skin-reference-map.json', {'package': key, 'common_family': 's20',
                                                'layouts': {'dp_frame': {'own20': True}, 'center_frame_dp': {'own20': True}}})
        for name in ('run.json', 'recorded/run.json'):
            save(folder / name, {'status': 'complete'})
        for movie in ('dp_frame', 'center_frame_dp'):
            export = {}
            for mode in ('motion', 'steady'):
                clip = write(folder / f'recorded/{movie}/{mode}.clip.json.br',
                             f'original20 single DP {movie}/{mode}'.encode())
                export[mode] = {'node': {'clock': 'presentation.elapsed'}, 'files': [
                    {'source': str(clip), 'path': f'{key}/{movie}/{mode}.clip.json.br', 'sha256': checksum(clip.read_bytes())},
                    {'source': str(sprite), 'path': f'{key}/sprite.png', 'sha256': texture['sha256']}]}
            save(folder / f'recorded/{movie}/export.json', export)
        self.research20 = source

    def join20(self, **changes):
        args = {'research22': self.research22, 'research21': self.research21, 'research20': self.research20}
        args.update(changes)
        return research_composition.compose(self.extended20, *self.old_research,
                                            self.root / 'joined-research20', **args)


class Original20CompositionTest(Original20Fixture):
    def test_imports_six_core_and_three_distinct_archives_without_adding_custom_choices(self):
        self.prepare20()
        before = [fingerprints(p) for p in (self.extended21, self.source20)]
        result = self.extend20()
        self.assertEqual(result['importedPackages'], 9)
        self.assertEqual(result['reusedCustomPackages'], 101)
        self.assertEqual(result['distinctCustomPackages'], 3)
        old, new = (read(root / 'library.json') for root in (self.extended21, self.extended20))
        self.assertEqual(new['packages'][:len(old['packages'])], old['packages'])
        self.assertEqual(new['textures'][:len(old['textures'])], old['textures'])
        self.assertEqual(source_composition.retained_base21(self.extended20, new), old)
        self.assertEqual(catalog.styles(new), (*catalog.styles(old), 'source20-gmframe0'))
        self.assertEqual((self.extended20 / new['sourceComposition']['previousComposition21']['path']).read_bytes(),
                         (self.extended21 / 'library.json').read_bytes())
        for relative, digest in fingerprints(self.extended21).items():
            if relative not in {'library.json', 'packages.json', 'run.json', 'index.html'}:
                self.assertEqual(checksum((self.extended20 / relative).read_bytes()), digest)
        reused, distinct = (new['sourceComposition'][field] for field in ('source20ReusedPackages', 'source20DistinctPackages'))
        self.assertEqual({Path(row['source']).stem for row in reused},
                         set(catalog.SOURCE20_CUSTOM_PACKAGES) - set(catalog.SOURCE20_DISTINCT_CUSTOM_ALIASES))
        for row in reused:
            self.assertFalse(row['newChoice'])
            self.assertFalse(row['payloadCopied'])
            self.assertTrue(row['retainedPackages'])
            for match in row['retainedPackages']:
                self.assertEqual(match['archiveSHA256'], row['archiveSHA256'])
                self.assertEqual(checksum((self.extended20 / 'source-copies' / match['source']).read_bytes()), row['archiveSHA256'])
        self.assertEqual({Path(row['source']).stem for row in distinct}, {'beam00', 'notes00', 'turn00'})
        for row in distinct:
            self.assertFalse(row['newChoice'])
            self.assertTrue(row['payloadCopied'])
            self.assertEqual(checksum((self.extended20 / 'source-copies' / row['logicalSource']).read_bytes()), row['archiveSHA256'])
        for name, alias in catalog.SOURCE20_ALIASES.items():
            logical = 'contents/data/graphic/' + alias + '.ifs'
            package = next(p for p in new['packages'] if p['source'] == logical)
            self.assertEqual(package['origin']['library'], 'iidx20')
            self.assertEqual(package['origin']['path'], 'data/graphic/' + name + '.ifs')
            for relative, digest in fingerprints(self.source20 / 'packages/data/graphic' / name).items():
                self.assertEqual(checksum((self.extended20 / 'packages/contents/data/graphic' / alias / relative).read_bytes()), digest)
        self.assertEqual(before, [fingerprints(p) for p in (self.extended21, self.source20)])

    def test_changed_reusable_archive_is_not_accepted_by_name_or_sprite_similarity(self):
        self.prepare20()
        row = next(p for p in self.data20['packages'] if p['source'].endswith('/beam01.ifs'))
        write(self.source20 / 'source-copies' / row['source'], b'different program, unchanged synthetic sprite')
        row['sha256'] = checksum(b'different program, unchanged synthetic sprite')
        save(self.source20 / 'library.json', self.data20)
        with self.assertRaisesRegex(ValueError, 'no exact retained source'):
            self.extend20()
        self.assertFalse(self.extended20.exists())

    def test_distinct_custom_cannot_be_silently_reclassified_as_an_exact_duplicate(self):
        self.prepare20()
        row = next(p for p in self.data20['packages'] if p['source'].endswith('/beam00.ifs'))
        content = (self.base / 'source-copies/contents/data/graphic/beam00.ifs').read_bytes()
        write(self.source20 / 'source-copies' / row['source'], content)
        row['sha256'] = checksum(content)
        save(self.source20 / 'library.json', self.data20)
        with self.assertRaisesRegex(ValueError, 'unexpectedly matches'):
            self.extend20()
        self.assertFalse(self.extended20.exists())

    def test_missing_extra_duplicate_or_foreign_texture_selection_is_rejected(self):
        self.prepare20()
        for change in ('missing', 'extra', 'duplicate', 'foreign-texture'):
            data = copy.deepcopy(self.data20)
            if change == 'missing':
                data['packages'].pop()
            elif change == 'extra':
                add_package(self.source20, data, 'gmframe9')
            elif change == 'duplicate':
                data['packages'].append(copy.deepcopy(data['packages'][0]))
            else:
                data['textures'][0]['package'] = 'data/graphic/unknown.ifs'
            save(self.source20 / 'library.json', data)
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, '110 original20'):
                self.extend20()
            self.assertFalse(self.extended20.exists())

    def test_wrong_or_already_extended_predecessor_is_rejected(self):
        self.prepare20()
        with self.assertRaisesRegex(ValueError, 'existing 21/22/24/26'):
            source_composition.extend20(self.extended, self.source20, self.root / 'wrong-predecessor')
        self.extend20()
        with self.assertRaisesRegex(ValueError, 'existing 21/22/24/26'):
            source_composition.extend20(self.extended20, self.source20, self.root / 'second-extension')

    def test_retained21_manifest_requires_content_and_inherited_provenance(self):
        self.prepare20()
        self.extend20()
        data = read(self.extended20 / 'library.json')
        path = self.extended20 / data['sourceComposition']['previousComposition21']['path']
        saved = path.read_bytes()
        path.write_bytes(saved + b' ')
        with self.assertRaisesRegex(ValueError, 'manifest changed'):
            source_composition.retained_base21(self.extended20, data)
        path.write_bytes(saved)
        data['sourceComposition']['source21ReusedPackages'][0]['newChoice'] = True
        with self.assertRaisesRegex(ValueError, 'unchanged base'):
            source_composition.retained_base21(self.extended20, data)


class Original20ResearchTest(Original20Fixture):
    def test_old_research_identities_are_preserved_and_real_single_dp_names_survive(self):
        self.make_research20()
        sources = (*self.old_research, self.research22, self.research21, self.research20)
        before = [fingerprints(p) for p in sources]
        result = self.join20()
        self.assertEqual(result['frames'], 20)
        joined = self.root / 'joined-research20'
        data = read(self.extended20 / 'library.json')
        mappings = read(joined / 'frame-maps.json')
        self.assertEqual(set(mappings), {catalog.frame_id(style) for style in catalog.retained_styles(data)})
        self.assertEqual(len(catalog.styles(data)), 19)
        identity21 = checksum((self.extended21 / 'library.json').read_bytes())
        identity20 = checksum((self.extended20 / 'library.json').read_bytes())
        receipt = read(joined / 'source-composition.json')
        original21 = next(r for r in receipt['frames'] if r['style'] == 'source21-gmframe0')
        original20 = next(r for r in receipt['frames'] if r['style'] == 'source20-gmframe0')
        self.assertEqual(original21['originalSourceIdentity']['librarySha256'], identity21)
        self.assertEqual(original20['originalSourceIdentity']['librarySha256'], identity20)
        reference = joined / 'source20/gmframe0/skin-reference-map.json'
        self.assertEqual(reference.read_bytes(), (self.research20 / 'source20/gmframe0/skin-reference-map.json').read_bytes())
        self.assertEqual(set(read(reference)['layouts']), {'dp_frame', 'center_frame_dp'})
        self.assertFalse((reference.parent / 'recorded/dp_frame_1p').exists())
        for movie in ('dp_frame', 'center_frame_dp'):
            export = read(reference.parent / 'recorded' / movie / 'export.json')
            original = read(self.research20 / 'source20/gmframe0/recorded' / movie / 'export.json')
            for mode in ('motion', 'steady'):
                self.assertEqual(export[mode]['node'], original[mode]['node'])
                for row in export[mode]['files']:
                    path = Path(row['source'])
                    self.assertTrue(path.is_relative_to(joined) or path.is_relative_to(self.extended20))
                    self.assertEqual(checksum(path.read_bytes()), row['sha256'])
        self.assertEqual(before, [fingerprints(p) for p in sources])

    def test_original20_research_is_required_and_cannot_borrow_the21_identity(self):
        self.make_research20()
        with self.assertRaisesRegex(ValueError, 'separate research20'):
            self.join20(research20=None)
        save(self.research20 / 'source.json', read(self.research21 / 'source.json'))
        with self.assertRaisesRegex(ValueError, 'source manifest'):
            self.join20()
        self.assertFalse((self.root / 'joined-research20').exists())

    def test_changed_old_source_or_wrong20_family_is_rejected_before_publication(self):
        self.make_research20()
        path = self.research20 / 'source20/gmframe0/skin-reference-map.json'
        saved = path.read_bytes()
        data = read(path)
        data['common_family'] = 's21'
        save(path, data)
        with self.assertRaisesRegex(ValueError, 'common texture family'):
            self.join20()
        path.write_bytes(saved)
        closure = read(self.research21 / 'sources-21-gmframe0.json')
        (self.extended20 / unquote(closure['files'][0]['path'])).write_bytes(b'changed old source')
        with self.assertRaisesRegex(ValueError, 'Source bytes changed'):
            self.join20()
        self.assertFalse((self.root / 'joined-research20').exists())


if __name__ == '__main__':
    unittest.main()
