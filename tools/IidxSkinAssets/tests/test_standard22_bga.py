"""22 BGA source routing and complete cover timeline; synthetic pixels only."""
import copy
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard_bga as fixture
from standard_bga import (CENTRES, cover_frame_state, cover_sizes, cover_source_frames,
                          digest, validated_geometry, viewport_ini)
from standard_source_catalog import (COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES,
                                     SOURCE22_STYLES, SOURCE22_AVAILABLE_STYLES, package_key)


class Standard22BgaTests(unittest.TestCase):
    def setUp(self):
        self.source = fixture.StandardBgaTests()
        self.source.setUp()
        self.addCleanup(self.source.doCleanups)
        source = self.source
        source.metadata['sourceComposition'] = {'contract': COMPOSITION22,
            'aliases24': EARLIER_ALIASES, 'aliases22': SOURCE22_ALIASES}

        def archive(name):
            path = source.put('source-copies/' + name, ('Synthetic independent archive:' + name).encode())
            source.metadata['packages'].append({'source': name, 'sha256': digest(path)})
            source.put(f"packages/{name.removesuffix('.ifs')}/raw/tex/texturelist.xml", b'<texturelist />')

        for family in ('s23', 's24', 's22'):
            archive(f'contents/data/graphic/{family}/gameparts.ifs')
            for name, size in cover_sizes(family).items():
                hole = ([171, 54, 629, 534] if name == 'anime_cover' else [198, 56, 748, 632]) if family != 's24' else (
                    [171, 141, 629, 621] if name == 'anime_cover' else [190, 47, 740, 623])
                relative = f'sprites/f{family}/{name}.png'
                path = source.put(relative, fixture.png(size, hole, (int(family[1:]), 10, 30)))
                original = {'package': f'contents/data/graphic/{family}/gameparts.ifs', 'name': name + '.png',
                    'sprite': relative, 'sha256': digest(path), 'width': size[0], 'height': size[1],
                    'imageRect': [0, size[0], 0, size[1]], 'uvRect': [0, size[0], 0, size[1]]}
                source.metadata['textures'].append(original)
                source.evidence['textures'].append({'family': family, 'name': name, 'path': relative,
                    'sha256': original['sha256'], 'size': list(size), 'imageRect': original['imageRect'],
                    'uvRect': original['uvRect'], 'filter': 'nearest', 'clearHole': hole})
        base_contexts = [copy.deepcopy(c) for c in source.contexts if c['style'] == 26]
        for style in (23, 24, *SOURCE22_STYLES):
            archive(f'contents/data/graphic/{package_key(style)}.ifs')
            family = 's24' if style == 24 else 's23' if style == 23 else 's22'
            raw = f'packages/contents/data/graphic/{package_key(style)}/raw'
            for movie in CENTRES:
                source.put(raw + '/afp/' + movie, ('Synthetic independent AFP:' + movie).encode())
                source.put(raw + '/afp/bsi/' + movie, b'Synthetic independent BSI')
                row = {'style': style, 'movie': movie, 'frames': 300, 'fps': 60, 'covers': []}
                if '_dp_' not in movie:
                    name = 'anime_cover2' if '_wide_' in movie else 'anime_cover'
                    width, height = cover_sizes(family)[name]
                    if movie == 'center_frame_wide_2p':
                        quad = [[width, 0], [0, 0], [0, height], [width, height]]
                    else:
                        x = 342 if movie == 'center_frame_wide_1p' else 240
                        y = 87 if name == 'anime_cover' and family != 's24' else 0
                        quad = [[x, y], [x + width, y], [x + width, y + height], [x, y + height]]
                    geometry = movie + '_shape'
                    source.put(raw + '/geo/' + geometry, ('Synthetic independent GEO:' + name).encode())
                    row.update(covers=[name], texture=name, geometry=geometry, quad=quad, constantSourceFrames=300)
                source.evidence['rows'].append(row)
            for c in ([] if style == 'source22-gmframe0' else base_contexts):
                context = {**copy.deepcopy(c), 'style': style}
                if style in SOURCE22_STYLES:
                    layout, wide = c['layout'], c['wide']
                    if layout == 'dp':
                        points = ([11, 48, 243, 352], [10, 369, 244, 662]) if style == 'source22-gmframe8' else (
                            ([13, 50, 242, 350], [16, 367, 238, 662]) if style.endswith(('-00', '-01')) else
                            ([13, 47, 241, 353], [13, 367, 241, 662]))
                        context['apertures'] = [fixture.bounds(*p) for p in points]
                    elif wide:
                        context['apertures'] = [fixture.bounds(*(431, 56, 1199, 632) if layout == 'p1' else (81, 56, 849, 632))]
                    else:
                        left, right = (355, 995) if layout == 'p1' else (285, 925)
                        if style == 'source22-gmframe8':
                            left += int(layout == 'p1')
                            right -= int(layout == 'p2')
                        context['apertures'] = [fixture.bounds(left, 141, right, 621)]
                source.contexts.append(context)
        self.refresh()

    def refresh(self):
        source = self.source
        source.put('library.json', json.dumps(source.metadata).encode())
        source.evidence['librarySHA256'] = digest(source.library / 'library.json')
        source.evidence['sourceFiles'] = [{'path': p, 'sha256': h} for p, h in source.source_files.items()]

    def test_five_available_frames_keep_six_archive_qualification_and_own_geometry(self):
        result = self.source.export()
        self.assertEqual(108, len(self.source.evidence['rows']))
        self.assertNotIn('source22-gmframe0', {r['frame'] for r in result['receipts']})
        on = result['module']['choices'][1]
        expected = {('p1', False): fixture.bounds(446, 141, 904, 621),
                    ('p2', False): fixture.bounds(376, 141, 834, 621),
                    ('p1', True): fixture.bounds(540, 56, 1090, 632),
                    ('p2', True): fixture.bounds(190, 56, 740, 632)}
        for style in SOURCE22_AVAILABLE_STYLES:
            for (layout, wide), viewport in expected.items():
                receipt = next(r for r in result['receipts'] if
                    (r['frame'], r['layout'], r['wide'], r['choice']) == (style, layout, wide, 'on'))
                self.assertEqual('s22', receipt['commonFamily'])
                self.assertEqual(300, receipt['sourceConstantFrames'])
                self.assertEqual([viewport], receipt['viewports'])
                selected = fixture.select(on, {'frame': style, 'layout': layout, 'graph-visible': not wide})
                resource, = self.source.read(selected['manifest'])['resources']
                self.assertIn('/fs22/', resource['path'])
                self.assertEqual((self.source.library / receipt['sourceTexture']['path']).read_bytes(),
                                 (self.source.output / resource['path']).read_bytes())
                self.assertIn('does not claim original caller pixel parity', receipt['composition'])

    def test_dp_keeps_two_actual_apertures_without_inventing_cover(self):
        result = self.source.export()
        for style in SOURCE22_AVAILABLE_STYLES:
            for choice in result['module']['choices']:
                contribution = fixture.select(choice, {'frame': style})
                self.assertEqual([], self.source.read(contribution['manifest'])['resources'])
                self.assertEqual([], self.source.read(contribution['scene'])['templates'])
                text = (self.source.output / contribution['ini']).read_text()
                self.assertEqual(2, text.count(',fit'))
                self.assertIn('Keymode: 14K', text)

    def test_incomplete_or_later_timeline_and_wrong_source_namespace_are_rejected(self):
        for count in (299, 420):
            evidence = copy.deepcopy(self.source.evidence)
            row = next(r for r in evidence['rows'] if r['style'] in SOURCE22_STYLES and r['covers'])
            row['frames'] = row['constantSourceFrames'] = count
            with self.assertRaisesRegex(ValueError, 'completely constant'):
                validated_geometry(self.source.library, evidence)
        evidence = copy.deepcopy(self.source.evidence)
        raw = f'packages/contents/data/graphic/{package_key(SOURCE22_STYLES[0])}/raw/afp/center_frame_sp'
        evidence['sourceFiles'] = [r for r in evidence['sourceFiles'] if r['path'] != raw]
        with self.assertRaisesRegex(ValueError, 'movie.*closure'):
            validated_geometry(self.source.library, evidence)

    def test_matching_23_art_cannot_replace_missing_22_texture(self):
        self.source.metadata['textures'] = [r for r in self.source.metadata['textures'] if not
            (r['package'] == 'contents/data/graphic/s22/gameparts.ifs' and r['name'] == 'anime_cover.png')]
        self.refresh()
        with self.assertRaisesRegex(ValueError, 'family is absent'):
            validated_geometry(self.source.library, self.source.evidence)

    def test_static_export_checks_last_original_frame_and_short_quad(self):
        root, source = fixture.CoverTimelineTests().source()
        for vertex in source['geometry'][0]['vertices']:
            if vertex[1] == 720:
                vertex[1] = 586
        root['frames'] = [copy.deepcopy(root['frames'][0]) for _ in range(300)] + [{'nodes': []}]
        value = cover_frame_state(root, source, sizes=cover_sizes('s22'))
        self.assertEqual([[240, 0], [1040, 0], [1040, 586], [240, 586]], value['quad'])
        changed = copy.deepcopy(root['states'][0])
        changed['matrix'][-3] += 1
        root['states'].append(changed)
        root['frames'][299]['nodes'][0][1] = len(root['states']) - 1
        with self.assertRaisesRegex(ValueError, 'changes.*entry/loop'):
            cover_frame_state(root, source, sizes=cover_sizes('s22'))

    def test_legacy_frame_counts_and_sizes_stay_separate(self):
        self.assertEqual([420, 420, 300, 300, 300],
                         [cover_source_frames(s) for s in (25, 26, 23, 24, SOURCE22_STYLES[0])])
        self.assertEqual((800, 720), cover_sizes('0')['anime_cover'])
        self.assertEqual((932, 720), cover_sizes('s24')['anime_cover2'])

    def test_bounded_catalog_preserves_every_frame_viewport_clip_owner_and_png(self):
        result = self.source.export()
        self.assertEqual([102, 102], [r['verifiedContextsIncludingBothDpGraphValues'] for r in result['variantRouting']])
        for choice in result['module']['choices']:
            self.assertLessEqual(len(choice['variants']), 64)
            for receipt in [r for r in result['receipts'] if r['choice'] == choice['value']]:
                layout = receipt['layout']
                options = {'frame': receipt['frame']}
                if layout != 'dp':
                    options.update(layout=layout, **{'graph-visible': not receipt['wide']})
                selected = fixture.select(choice, options)
                self.assertEqual(viewport_ini(layout, receipt['viewports']),
                                 (self.source.output / selected['ini']).read_text())
                manifest, scene = (self.source.read(selected[k]) for k in ('manifest', 'scene'))
                if choice['value'] == 'off' or layout == 'dp':
                    self.assertEqual([], manifest['resources'])
                    self.assertEqual([], scene['templates'])
                    continue
                resource, = manifest['resources']
                self.assertEqual(receipt['sourceTexture']['sha256'], digest(self.source.output / resource['path']))
                self.assertEqual(receipt['sourceTexture']['filter'], resource['filter'])
                self.assertEqual({'slot': 'decoration', 'resource': f'markers/frame-{layout}-information'},
                                 scene['instances'][0]['material'])
                clip = scene['templates'][0]['root']['children'][0]
                cp, ap = clip['properties'], clip['children'][0]['properties']
                left, top, width, height = cp['x']*1280, cp['y']*720, cp['width']*1280, cp['height']*720
                expected = receipt['originalApertures'][0]
                for got, wanted in zip((left, top, left+width, top+height),
                                       (expected[k] for k in ('left', 'top', 'right', 'bottom'))):
                    self.assertAlmostEqual(wanted, got)
                for (u, v, q), (x, y) in zip(scene['tracks'][0]['keyframes'][0]['value']['vertices'], receipt['translatedQuad']):
                    self.assertEqual(1, q)
                    self.assertAlmostEqual(x, left + (ap['x'] + u*ap['width'])*width)
                    self.assertAlmostEqual(y, top + (ap['y'] + v*ap['height'])*height)

    def test_excess_distinct_apertures_fail_without_dropping_frame_conditions(self):
        contexts = copy.deepcopy(self.source.contexts)
        for i, context in enumerate(contexts):
            context['apertures'][0]['left'] += (i+1) / 1000
        with self.assertRaisesRegex(ValueError, '64-variant budget'):
            self.source.export(contexts=contexts)


if __name__ == '__main__':
    unittest.main()
