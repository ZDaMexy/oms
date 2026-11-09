"""Own20 DP Graph-OFF source routing; synthetic images and asymmetric holes."""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard_bga as fixture
import test_standard21_bga as source21
from standard_bga import centre_movies, cover_sizes, digest, viewport_ini
from standard_source_catalog import COMPOSITION20, SOURCE20_ALIASES, SOURCE20_STYLES, frame_id, styles

STYLE = SOURCE20_STYLES[0]


class Standard20BgaRoutesTests(unittest.TestCase):
    def setUp(self):
        previous = source21.Standard21BgaRoutingTests()
        previous.setUp()
        self.addCleanup(previous.doCleanups)
        self.source = source = previous.source
        self.before = source.export()
        self.before_bytes = {r['path']: (source.output / r['path']).read_bytes() for r in self.before['files']}
        source.output = source.output.with_name('author20')
        source.metadata['sourceComposition'].update(contract=COMPOSITION20, aliases20=SOURCE20_ALIASES)
        for package in ('s20/gameparts', 'source20/gmframe0'):
            name = 'contents/data/graphic/' + package + '.ifs'
            path = source.put('source-copies/' + name, ('Independent synthetic20:' + package).encode())
            source.metadata['packages'].append({'source': name, 'sha256': digest(path)})
            source.put('packages/' + name.removesuffix('.ifs') + '/raw/tex/texturelist.xml', b'<texturelist/>')
        for name, size in cover_sizes('s20').items():
            hole = [10, 20, size[0] - 10, size[1] - 20]
            path = source.put(f'sprites/fs20/{name}.png', fixture.png(size, hole, (13, 37, 71)))
            row = {'package': 'contents/data/graphic/s20/gameparts.ifs', 'name': name + '.png',
                   'sprite': path.relative_to(source.library).as_posix(), 'sha256': digest(path),
                   'width': size[0], 'height': size[1], 'imageRect': [0, size[0], 0, size[1]], 'uvRect': [0, size[0], 0, size[1]]}
            source.metadata['textures'].append(row)
            source.evidence['textures'].append({'family': 's20', 'name': name, 'path': row['sprite'], 'sha256': row['sha256'],
                'size': list(size), 'imageRect': row['imageRect'], 'uvRect': row['uvRect'], 'filter': 'nearest', 'clearHole': hole})
        raw = 'packages/contents/data/graphic/source20/gmframe0/raw'
        for movie in centre_movies(STYLE):
            source.put(raw + '/afp/' + movie, ('Independent20 AFP:' + movie).encode())
            source.put(raw + '/afp/bsi/' + movie, b'Independent20 BSI')
            row = {'style': STYLE, 'movie': movie, 'frames': 180, 'fps': 60, 'covers': []}
            if movie != 'center_frame_dp':
                name = 'anime_cover2' if '_wide_' in movie else 'anime_cover'
                width, height = cover_sizes('s20')[name]
                geometry = movie + '_shape20'
                source.put(raw + '/geo/' + geometry, ('Independent20 GEO:' + movie).encode())
                row.update(covers=[name], texture=name, geometry=geometry, constantSourceFrames=180,
                           quad=[[240, 0], [240 + width, 0], [240 + width, height], [240, height]])
            source.evidence['rows'].append(row)
        for context in [copy.deepcopy(c) for c in source.contexts if c['style'] == source21.STYLE]:
            context['style'] = STYLE
            if context['layout'] == 'dp':
                context['dpGraphOffApertures'] = context['apertures'] + [fixture.bounds(1037, 49, 1269, 355), fixture.bounds(1035, 369, 1271, 661)]
            source.contexts.append(context)
        previous.old.refresh()

    def test_20_graph_off_uses_own_four_openings_without_changing_18_retained_routes(self):
        result = self.source.export()
        for path, expected in self.before_bytes.items():
            self.assertEqual(expected, (self.source.output / path).read_bytes(), path)
        for choice in result['module']['choices']:
            self.assertLessEqual(len(choice['variants']), 64)
            old_choice = next(c for c in self.before['module']['choices'] if c['value'] == choice['value'])
            for style in styles(self.source.metadata):
                for layout in ('p1', 'p2', 'dp'):
                    for shown in (False, True):
                        options = {'frame': frame_id(style), 'graph-visible': shown}
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
                            self.assertEqual([], self.source.read(actual['scene'])['templates'])
            own = [r for r in result['receipts'] if r['frame'] == STYLE and r['layout'] == 'dp' and r.get('graphVisible') is False]
            self.assertTrue(all('Original20' in r['dp'] for r in own))

    def test_20_four_viewports_cannot_replace_left_pair_or_another_source(self):
        contexts = copy.deepcopy(self.source.contexts)
        context = next(c for c in contexts if c['style'] == STYLE and c['layout'] == 'dp')
        context['dpGraphOffApertures'][0] = {**context['dpGraphOffApertures'][0],
                                            'left': context['dpGraphOffApertures'][0]['left'] + 1}
        with self.assertRaisesRegex(ValueError, 'Original20.*two left and two right'):
            self.source.export(contexts=contexts)


if __name__ == '__main__':
    unittest.main()
