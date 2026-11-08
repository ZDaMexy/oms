"""Original21 HUD source ownership and named layers; no private artwork."""
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_standard_skin import Frame, document, frame_label
from standard_source_catalog import centre_translation, package_key


def rectangle(package, texture, hook, width, height, x=400, y=600):
    vertices = [{'x': a, 'y': b, 'z': 0} for a, b in ((0, 0), (width, 0), (0, height), (width, height))]
    return {'texture': texture, 'asset': {'package': package, 'width': width, 'height': height},
            'game_bindings': [hook], 'bounds': {'left': x, 'right': x + width, 'top': y, 'bottom': y + height},
            'source_vertices': vertices,
            'projected_vertices': [{**v, 'x': v['x'] + x, 'y': v['y'] + y} for v in vertices],
            'matrix': {'a': 1, 'b': 0, 'c': 0, 'd': 1, 'tx': x, 'ty': y},
            'mask_present': False, 'blend': 0, 'mult_color': dict.fromkeys(('r', 'g', 'b', 'a'), 1),
            'add_color': dict.fromkeys(('r', 'g', 'b', 'a'), 0),
            'draw_params': [{'flags': 3, 'region': texture, 'vertexes': [0, 1, 2, 2, 1, 3], 'blend': None}]}


class Source21Frame(Frame):
    def __init__(self, layout='p1', wide=False):
        self.style, self.key, self.layout = 'source21-gmframe0', 'source21/gmframe0', layout
        self.dp, self.p2, self.wide = layout == 'dp', layout == 'p2', wide
        self.player = '2p' if self.p2 else '1p'
        self.side = 'dp_frame_1p' if self.dp else '2p_frame' if self.p2 else '1p_frame'
        self.centre = ('center_frame_dp_1p' if self.dp else
                       'center_frame_wide_' + self.player if wide else 'center_frame_sp')
        self.dx = centre_translation(self.style, layout, wide)
        self.prefix = 'fixture.' + layout + ('.wide' if wide else '')
        self.scene, self.info, self.evidence = document(self.prefix), [], []
        self.resources, self.fonts, self.calls = {}, {}, []
        self.empty = 'fixture.empty'
        self.owner = SimpleNamespace(solid_path='synthetic/solid.png', index={})
        self.map = {'layouts': {self.side: {'draws': []}, self.centre: {'draws': []}}}
        side, centre = self.map['layouts'][self.side]['draws'], self.map['layouts'][self.centre]['draws']
        for name, texture, width, height in (
                ('score_' + self.player + '_0', 'playm_sco00', 28, 19),
                ('maxcombo_' + self.player + '_0', 'playm_sco00', 28, 19),
                ('bpm_0', 'playm_bpm00', 28, 19),
                ('bpm_min_0', 'playm_bpm_m00', 19, 16),
                ('bpm_max_0', 'playm_bpm_m00', 19, 16)):
            centre.append(rectangle('s21/gameparts', texture, name, width, height))
        centre.append(rectangle(self.key, 'pm_bpm2', 'bpm_waku', 266, 54))
        if wide:
            centre.extend(rectangle(self.key, texture, name + '_' + self.player, width, 15)
                          for name, texture, width in (('score', 'playm_score', 52), ('maxcombo', 'playm_maxcombo', 90)))
        elif not self.dp:
            centre.append(rectangle(self.key, 'sco_waku_' + self.player, 'sco_waku_' + self.player, 291, 80))
        for i, x in enumerate((316, 302, 281, 267)):
            side.append(rectangle('s21/gameparts', 'playm_hs_00', f'hs_{self.player}_{i}', 15, 17, x, 629))
        side.append(rectangle('s21/gameparts', 'playm_gauge_normal_' + self.player, 'gg_bar_' + self.player, 300, 21))
        for i, x in enumerate((291, 262, 243)):
            side.append(rectangle('s21/gameparts', 'playm_gg_00', f'percent_{self.player}_{i}', 29, 22, x, 562))
        side.append(rectangle('s21/gameparts', 'playm_another' + ('_dp' if self.dp else ''),
                              'diff_' + self.player, 180 if self.dp else 310, 50))
        side.append(rectangle('s21/gameparts', 'lvlnum_12', 'lvlnum_' + self.player, 20, 12))
        side.append(rectangle('s21/gameparts', 'txt_lv_dot', 'lv_' + self.player, 28, 12))
        for name in ('beginner', 'normal', 'hyper', 'another'):
            self.owner.index['s21/gameparts', 'playm_' + name + ('_dp' if self.dp else '')] = True
        self.owner.index.update({('s21/gameparts', f'lvlnum_{i:02}'): True for i in range(1, 13)})

    def asset(self, package, name):
        self.calls.append((package, name))
        if package not in (self.key, 's21/gameparts'):
            raise AssertionError('Cross-family artwork requested')
        if name.startswith('playm_bpm_m'):
            size = (19, 16)
        elif name.startswith(('playm_sco0', 'playm_bpm0')):
            size = (28, 19)
        elif name.startswith('playm_hs_'):
            size = (15, 17)
        elif name.startswith('playm_gg_'):
            size = (29, 22)
        else:
            size = (1, 1)
        return package + '/' + name, {'width': size[0], 'height': size[1]}


class Standard21AuthorHudTests(unittest.TestCase):
    def test_identity_does_not_alias_later_spada_sources(self):
        self.assertEqual('21 SPADA (Original)', frame_label('source21-gmframe0'))
        self.assertEqual('21 SPADA', frame_label(21))
        self.assertEqual('SPADA (22 CUSTOM)', frame_label('source22-gmframe10'))
        self.assertEqual(3, len({frame_label(style) for style in
                                 (21, 'source21-gmframe0', 'source22-gmframe10')}))
        self.assertNotEqual(package_key(21), package_key('source21-gmframe0'))
        self.assertNotEqual(package_key('source22-gmframe10'), package_key('source21-gmframe0'))

    def test_all_five_layouts_use_own_common_fonts_with_qualified_advances(self):
        for layout, wide in (('p1', False), ('p2', False), ('dp', False), ('p1', True), ('p2', True)):
            with self.subTest(layout=layout, wide=wide):
                f = Source21Frame(layout, wide)
                f.numbers()
                f.header()
                f.gauge()
                for suffix, height, advance in (('numbers', 19, 27), ('bpm', 19, 27), ('small-bpm', 16, 18)):
                    font = next(v for k, v in f.fonts.items() if k.endswith('.' + suffix))
                    self.assertEqual(height, font['lineHeight'])
                    self.assertEqual({advance}, {g['advance'] for g in font['glyphs']})
                    self.assertTrue(all(g['resource'].startswith('s21/gameparts/') for g in font['glyphs']))
                fields = {r['readout']: r for r in f.evidence if 'readout' in r}
                for name, width in (('score', 163), ('combo', 109), ('bpm', 82), ('min-bpm', 55), ('max-bpm', 55)):
                    bounds = fields[name]['bounds']
                    self.assertEqual(width, bounds['right'] - bounds['left'])
                self.assertFalse(any('aeasy' in name for _, name in f.calls))
                gauge = next(row for row in f.evidence if 'gaugePercent' in row)
                self.assertEqual('s21', gauge['sourceFamily'])
                self.assertIn('60%', gauge['assistEasyArtwork'])

    def test_wide_named_labels_narrow_bezel_and_bpm_panel_have_distinct_owners(self):
        for layout, wide in (('p1', False), ('p2', False), ('dp', False), ('p1', True), ('p2', True)):
            f = Source21Frame(layout, wide)
            f.numbers()
            self.assertEqual(2 if wide else 0, sum('wideScoreLabel' in r for r in f.evidence))
            self.assertEqual(0 if wide or layout == 'dp' else 1, sum('scorePanel' in r for r in f.evidence))
            panel = next(r['bpmRangePanel'] for r in f.evidence if 'bpmRangePanel' in r)
            self.assertEqual('source21/gmframe0', panel['sourcePackage'])
            self.assertIn('native21 hides', panel['scope'])
            self.assertEqual(266 / 1280, next(n for n in f.info if n['id'].endswith('.bpm-range-panel'))['properties']['width'])

    def test_dp_uses_own_smaller_difficulty_art_and_unknown_label_is_host_text(self):
        f = Source21Frame('dp')
        f.header()
        chosen = next(v for v in f.scene['variants'] if v['target'].endswith('.header.difficulty') and v['property'] == 'resource')
        self.assertEqual({'BEGINNER', 'NORMAL', 'HYPER', 'ANOTHER'}, {c['key'] for c in chosen['cases']})
        self.assertTrue(all(c['resource'].endswith('_dp') for c in chosen['cases']))
        fallback = next(v for v in f.scene['variants'] if v['target'].endswith('.header.difficulty.text'))
        self.assertTrue(fallback['default'])

    def test_wide_label_rejects_cross_source_or_changed_mesh(self):
        for mutation in ('package', 'mesh', 'missing'):
            f = Source21Frame(wide=True)
            draw = next(d for d in f.map['layouts'][f.centre]['draws'] if d['texture'] == 'playm_score')
            if mutation == 'package':
                draw['asset']['package'] = 'source22/gmframe10'
            elif mutation == 'mesh':
                draw['projected_vertices'][1]['x'] += 1
            else:
                f.map['layouts'][f.centre]['draws'].remove(draw)
            with self.assertRaisesRegex(ValueError, 'qualified original|requires one original'):
                f.wide_score_labels()

    def test_bpm_panel_rejects_later_size_even_with_matching_source_name(self):
        f = Source21Frame()
        draw = next(d for d in f.map['layouts'][f.centre]['draws'] if d['texture'] == 'pm_bpm2')
        draw['asset']['width'] = 280
        with self.assertRaisesRegex(ValueError, 'qualified original rectangle'):
            f.bpm_range_panel()


if __name__ == '__main__':
    unittest.main()
