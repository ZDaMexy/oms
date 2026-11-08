"""Original21 DP Graph-OFF has four source apertures; synthetic fixtures only."""
import copy
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
sys.path.insert(0, str(Path(__file__).resolve().parents[3] / '.dev-cache/tools/iidx-assets'))
from PIL import Image
from build_standard_skin import Frame
from standard_bga import digest, viewport_ini
from standard_source_catalog import COMPOSITION21, SOURCE21_ALIASES, SOURCE21_STYLES, styles
import test_standard_bga as fixture
import test_standard22_bga as earlier

STYLE = SOURCE21_STYLES[0]


class Standard21ApertureTests(unittest.TestCase):
    def test_four_openings_are_measured_independently_and_hud_clipped(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            image = Image.new('RGBA', (1280, 720), 'white')
            # Deliberately asymmetric source holes: a mirrored assumption fails.
            holes = [(13, 47, 241, 353), (13, 367, 241, 677),
                     (1041, 49, 1265, 351), (1039, 369, 1267, 680)]
            for rectangle in holes:
                image.paste((0, 0, 0, 0), rectangle)
            image.save(root / 'centre.png')
            Image.new('RGBA', image.size).save(root / 'side.png')
            frame = Frame.__new__(Frame)
            frame.owner, frame.research = SimpleNamespace(Image=Image), root
            frame.style, frame.dp, frame.p2, frame.dx = STYLE, True, False, 0
            frame.centre, frame.side, frame.evidence = 'centre', 'side', []
            frame.map = {'layouts': {name: {'static_frame': name + '.png'} for name in ('centre', 'side')}}
            frame.field_rects = lambda: [{'left': 304, 'right': 596}]
            frame.draws = lambda name: [{'bounds': {'top': 365}}]
            expected = [fixture.bounds(a, b, c, min(d, 662)) for a, b, c, d in holes]
            self.assertEqual(expected[:2], frame.apertures())
            self.assertEqual(expected, frame.apertures(graph_visible=False))
            frame.style = 21
            with self.assertRaisesRegex(ValueError, 'only for original21 DP'):
                frame.apertures(graph_visible=False)


class Standard21BgaRoutingTests(unittest.TestCase):
    def setUp(self):
        self.old = earlier.Standard22BgaTests()
        self.old.setUp()
        self.addCleanup(self.old.doCleanups)
        self.source = source = self.old.source
        self.previous = source.export()
        self.previous_bytes = {r['path']: (source.output / r['path']).read_bytes() for r in self.previous['files']}
        source.output = source.output.with_name('author21')
        source.metadata['sourceComposition'].update(contract=COMPOSITION21, aliases21=SOURCE21_ALIASES)

        def archive(name):
            path = source.put('source-copies/' + name, ('Synthetic original21:' + name).encode())
            source.metadata['packages'].append({'source': name, 'sha256': digest(path)})
            source.put(f"packages/{name.removesuffix('.ifs')}/raw/tex/texturelist.xml", b'<texturelist />')

        archive('contents/data/graphic/s21/gameparts.ifs')
        archive('contents/data/graphic/source21/gmframe0.ifs')
        for row in list(source.metadata['textures']):
            if row['package'] != 'contents/data/graphic/s22/gameparts.ifs':
                continue
            original = copy.deepcopy(row)
            original.update(package='contents/data/graphic/s21/gameparts.ifs', sprite=row['sprite'].replace('fs22/', 'fs21/'))
            path = source.put(original['sprite'], (source.library / row['sprite']).read_bytes())
            original['sha256'] = digest(path)
            source.metadata['textures'].append(original)
        for row in list(source.evidence['textures']):
            if row['family'] == 's22':
                source.evidence['textures'].append({**row, 'family': 's21', 'path': row['path'].replace('fs22/', 'fs21/')})
        for row in list(source.evidence['rows']):
            if row['style'] != 'source22-gmframe0':
                continue
            value = copy.deepcopy(row)
            value['style'] = STYLE
            source.evidence['rows'].append(value)
            raw = 'packages/contents/data/graphic/source21/gmframe0/raw/'
            source.put(raw + 'afp/' + row['movie'], ('Synthetic21 AFP:' + row['movie']).encode())
            source.put(raw + 'afp/bsi/' + row['movie'], b'Synthetic21 BSI')
            if row['covers']:
                source.put(raw + 'geo/' + row['geometry'], b'Synthetic21 GEO')
        for context in list(source.contexts):
            if context['style'] != 'source22-gmframe0-00':
                continue
            value = {**copy.deepcopy(context), 'style': STYLE}
            if value['layout'] == 'dp':
                value['dpGraphOffApertures'] = value['apertures'] + [fixture.bounds(1039, 47, 1267, 353), fixture.bounds(1039, 367, 1267, 662)]
            source.contexts.append(value)
        self.old.refresh()

    def test_full_selection_domain_keeps_old17_and_only_adds_original21_dp_off(self):
        result = self.source.export()
        current_files = {r['path']: r for r in result['files']}
        for path, data in self.previous_bytes.items():
            self.assertEqual(data, (self.source.output / current_files[path]['path']).read_bytes(), path)
        for choice in result['module']['choices']:
            self.assertLessEqual(len(choice['variants']), 64)
            old_choice = next(c for c in self.previous['module']['choices'] if c['value'] == choice['value'])
            for style in styles(self.source.metadata):
                for layout in ('p1', 'p2', 'dp'):
                    for shown in (False, True):
                        options = {'frame': 'iidx' + str(style).zfill(2) if isinstance(style, int) else style, 'graph-visible': shown}
                        if layout != 'dp':
                            options['layout'] = layout
                        actual = fixture.select(choice, options)
                        if style != STYLE:
                            previous = fixture.select(old_choice, options)
                            self.assertEqual({k: previous[k] for k in ('manifest', 'scene', 'ini')},
                                             {k: actual[k] for k in ('manifest', 'scene', 'ini')})
                        elif layout == 'dp':
                            context = next(c for c in self.source.contexts if c['style'] == STYLE and c['layout'] == 'dp')
                            expected = context['apertures'] if shown else context['dpGraphOffApertures']
                            self.assertEqual(viewport_ini('dp', expected), (self.source.output / actual['ini']).read_text())
                            self.assertEqual([], self.source.read(actual['manifest'])['resources'])
                            self.assertEqual([], self.source.read(actual['scene'])['templates'])
                        else:
                            self.assertNotIn('viewports-graph-off.ini', actual['ini'])
            on = fixture.select(choice, {'frame': STYLE, 'graph-visible': True})
            off = fixture.select(choice, {'frame': STYLE, 'graph-visible': False})
            self.assertEqual({k: on[k] for k in ('manifest', 'scene')}, {k: off[k] for k in ('manifest', 'scene')})

    def test_extra_route_rejects_missing_left_or_foreign_source(self):
        contexts = copy.deepcopy(self.source.contexts)
        dp = next(c for c in contexts if c['style'] == STYLE and c['layout'] == 'dp')
        dp['dpGraphOffApertures'][0] = fixture.bounds(14, 50, 241, 350)
        with self.assertRaisesRegex(ValueError, 'two left and two right'):
            self.source.export(contexts=contexts)
        contexts = copy.deepcopy(self.source.contexts)
        contexts[0]['dpGraphOffApertures'] = dp['dpGraphOffApertures']
        with self.assertRaisesRegex(ValueError, 'only for original21 DP'):
            self.source.export(contexts=contexts)


if __name__ == '__main__':
    unittest.main()
