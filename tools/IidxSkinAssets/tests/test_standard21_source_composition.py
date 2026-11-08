"""Original21 identity, immutable predecessors and exact CUSTOM reuse."""
import copy
from pathlib import Path
import unittest
from urllib.parse import unquote

from test_standard_source_identity import Source22Fixture
from test_standard_source_composition import (add_package, catalog, checksum, fingerprints,
                                              make_library, read, research_composition,
                                              save, source_composition, write)


class Original21Fixture(Source22Fixture):
    def setUp(self):
        super().setUp()
        existing = {Path(p['source']).stem for p in self.base_data['packages']}
        for name in catalog.SOURCE21_CUSTOM_PACKAGES:
            if name not in existing:
                add_package(self.base, self.base_data, name)
        save(self.base / 'library.json', self.base_data)

    def make_source21(self):
        self.source21 = self.root / 'library21'
        self.data21 = make_library(self.source21, 'data/graphic',
                                   (*catalog.SOURCE21_ALIASES, *catalog.SOURCE21_CUSTOM_PACKAGES))
        for package in self.data21['packages']:
            name = Path(package['source']).stem
            if name in catalog.SOURCE21_CUSTOM_PACKAGES:
                original = self.base / 'source-copies/contents/data/graphic' / (name + '.ifs')
                content = original.read_bytes()
                write(self.source21 / 'source-copies' / package['source'], content)
                package['sha256'] = checksum(content)
        save(self.source21 / 'library.json', self.data21)
        self.extended21 = self.root / 'extended-library21'

    def extend21(self):
        return source_composition.extend21(self.extended, self.source21, self.extended21)

    def prepare21(self):
        self.compose()
        self.make_source22()
        self.extend()
        self.make_source21()

    def make_research21(self):
        self.make_research22()
        self.make_source21()
        self.extend21()
        source = self.root / 'research21'
        style = 'source21-gmframe0'
        key = catalog.package_key(style)
        data = read(self.extended21 / 'library.json')
        save(source / 'source.json', {'librarySha256': checksum((self.extended21 / 'library.json').read_bytes())})
        texture = next(t for t in data['textures'] if t['package'] == f'contents/data/graphic/{key}.ifs')
        sprite = self.extended21 / unquote(texture['sprite'])
        closure = {'files': [{'path': texture['sprite'], 'bytes': sprite.stat().st_size, 'sha256': texture['sha256']}]}
        save(source / f'sources-{catalog.frame_token(style)}.json', closure)
        folder = source / key
        save(folder / 'recorded/sources.json', closure)
        save(folder / 'skin-reference-map.json', {'package': key, 'common_family': 's21'})
        for run in ('run.json', 'recorded/run.json'):
            save(folder / run, {'status': 'complete'})
        exports = {}
        for mode in ('motion', 'steady'):
            clip = write(folder / f'recorded/movie/{mode}.clip.json.br', f'original21 {mode}'.encode())
            exports[mode] = {'node': {'clock': 'presentation.elapsed'}, 'files': [
                {'source': str(clip), 'path': f'{style}/{mode}.clip.json.br', 'sha256': checksum(clip.read_bytes())},
                {'source': str(sprite), 'path': f'{style}/sprite.png', 'sha256': texture['sha256']}]}
        save(folder / 'recorded/movie/export.json', exports)
        self.research21 = source


class Original21IdentityTest(unittest.TestCase):
    def test_original21_does_not_relabel_either_spada_revival_or_the_bare22_archive(self):
        old = {'sourceComposition': {'contract': catalog.COMPOSITION22,
                                     'aliases24': copy.deepcopy(catalog.EARLIER_ALIASES),
                                     'aliases22': copy.deepcopy(catalog.SOURCE22_ALIASES)}}
        extended = copy.deepcopy(old)
        extended['sourceComposition'].update(contract=catalog.COMPOSITION21, aliases21=copy.deepcopy(catalog.SOURCE21_ALIASES))
        self.assertEqual(catalog.styles(extended), (*catalog.styles(old), 'source21-gmframe0'))
        self.assertEqual(len(catalog.styles(extended)), 18)
        self.assertEqual(len(catalog.retained_styles(extended)), 19)
        self.assertNotIn('source22-gmframe0', catalog.styles(extended))
        self.assertEqual(catalog.frame_token('source21-gmframe0'), '21-gmframe0')
        self.assertEqual(catalog.frame_id('source21-gmframe0'), 'source21-gmframe0')
        self.assertEqual(catalog.package_key('source21-gmframe0'), 'source21/gmframe0')
        self.assertEqual(catalog.layout_family('source21-gmframe0'), 's21')
        self.assertEqual(catalog.families(extended), (*catalog.families(old), 's21'))
        self.assertEqual(catalog.art_identity(extended), 'IIDX21+IIDX22+IIDX24+IIDX26')
        self.assertEqual(catalog.frame_id(21), 'iidx21')
        self.assertEqual(catalog.package_key('source22-gmframe10'), 'source22/gmframe10')
        self.assertEqual(catalog.centre_translation('source21-gmframe0', 'p2'), -35)
        extended['sourceComposition']['aliases21']['gmframe0'] = 'gmframe21'
        with self.assertRaisesRegex(ValueError, 'Unrecognized'):
            catalog.styles(extended)


class Original21CompositionTest(Original21Fixture):
    def test_imports_only_six_core_packages_preserves_base_and_records149_original_archives(self):
        self.prepare21()
        before = fingerprints(self.extended)
        source_before = fingerprints(self.source21)
        result = self.extend21()
        self.assertEqual(result['importedPackages'], 6)
        self.assertEqual(result['reusedCustomPackages'], 149)
        old = read(self.extended / 'library.json')
        new = read(self.extended21 / 'library.json')
        self.assertEqual(new['packages'][:len(old['packages'])], old['packages'])
        self.assertEqual(new['textures'][:len(old['textures'])], old['textures'])
        self.assertEqual(len(new['packages']), len(old['packages']) + 6)
        self.assertEqual(source_composition.retained_base22(self.extended21, new), old)
        reused = new['sourceComposition']['source21ReusedPackages']
        self.assertEqual({Path(row['source']).stem for row in reused}, set(catalog.SOURCE21_CUSTOM_PACKAGES))
        for row in reused:
            self.assertFalse(row['newChoice'])
            self.assertFalse(row['payloadCopied'])
            self.assertTrue(row['retainedPackages'])
            self.assertFalse((self.extended21 / 'source-copies/contents/data/graphic/source21' / Path(row['source']).name).exists())
            for retained in row['retainedPackages']:
                self.assertEqual(row['archiveSHA256'], retained['archiveSHA256'])
                self.assertEqual(checksum((self.extended21 / 'source-copies' / retained['source']).read_bytes()), row['archiveSHA256'])
        for relative, value in before.items():
            if relative not in {'library.json', 'packages.json', 'run.json', 'index.html'}:
                self.assertEqual(checksum((self.extended21 / relative).read_bytes()), value)
        self.assertEqual(fingerprints(self.extended), before)
        self.assertEqual(fingerprints(self.source21), source_before)

    def test_different_custom_archive_cannot_be_reused_by_name(self):
        self.prepare21()
        row = next(p for p in self.data21['packages'] if p['source'].endswith('/lane027.ifs'))
        write(self.source21 / 'source-copies' / row['source'], b'new original pixels/program')
        row['sha256'] = checksum(b'new original pixels/program')
        save(self.source21 / 'library.json', self.data21)
        with self.assertRaisesRegex(ValueError, 'no exact retained source'):
            self.extend21()
        self.assertFalse(self.extended21.exists())

    def test_missing_or_extra21_packages_are_rejected_before_copying(self):
        self.prepare21()
        for change in ('missing', 'duplicate', 'extra'):
            data = copy.deepcopy(self.data21)
            if change == 'missing':
                data['packages'].pop()
            elif change == 'duplicate':
                data['packages'].append(copy.deepcopy(data['packages'][0]))
            else:
                add_package(self.source21, data, 'gmframe9')
            save(self.source21 / 'library.json', data)
            with self.subTest(change=change), self.assertRaisesRegex(ValueError, '155 original21'):
                self.extend21()
            self.assertFalse(self.extended21.exists())

    def test_changed_retained_predecessor_manifest_is_rejected(self):
        self.prepare21()
        self.extend21()
        data = read(self.extended21 / 'library.json')
        path = self.extended21 / data['sourceComposition']['previousComposition22']['path']
        path.write_bytes(path.read_bytes() + b' ')
        with self.assertRaisesRegex(ValueError, 'manifest changed'):
            source_composition.retained_base22(self.extended21, data)

    def test_research_reuses_all18_prior_sources_with_verified_closure_and_adds_original21(self):
        self.make_research21()
        before = {str(p): fingerprints(p) for p in (*self.old_research, self.research22, self.research21)}
        output = self.root / 'joined21'
        result = research_composition.compose(self.extended21, *self.old_research, output,
                                              research22=self.research22, research21=self.research21)
        self.assertEqual(result['frames'], 19)
        maps = read(output / 'frame-maps.json')
        self.assertIn('iidx21', maps)
        self.assertIn('source22-gmframe0', maps)
        self.assertIn('source22-gmframe10', maps)
        self.assertIn('source21-gmframe0', maps)
        self.assertEqual(before, {str(p): fingerprints(p) for p in (*self.old_research, self.research22, self.research21)})

    def test_research21_cannot_borrow_the22_manifest_or_family(self):
        self.make_research21()
        path = self.research21 / 'source.json'
        data = read(path)
        data['librarySha256'] = checksum((self.extended / 'library.json').read_bytes())
        save(path, data)
        with self.assertRaisesRegex(ValueError, 'source manifest'):
            research_composition.compose(self.extended21, *self.old_research, self.root / 'wrong21',
                                          research22=self.research22, research21=self.research21)


if __name__ == '__main__':
    unittest.main()
