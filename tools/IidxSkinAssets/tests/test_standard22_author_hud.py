"""Qualified22 HUD omissions and unequal original glyph heights, without private art."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_standard_skin import Frame, select_gauge_assets
from standard_source_catalog import (BASE_STYLES, EARLIER_STYLES, COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES,
                                     SOURCE22_AVAILABLE_STYLES, retained_styles, frame_token, package_key)


class Standard22AuthorHudTests(unittest.TestCase):
    def frame(self, style='source22-gmframe9', dp=False):
        frame = Frame.__new__(Frame)
        frame.style, frame.dp, frame.player, frame.side = style, dp, '1p', 'side'
        frame.prefix, frame.info, frame.evidence, frame.fonts = 'fixture', [], [], {}
        frame.bind = lambda *args: None
        frame.asset = lambda package, name: ('original-' + name, {'height': 28 if name == 'digit2' else 30, 'width': 35})
        return frame

    def test_retained_tricoro_sp_without_original_hook_has_no_invented_cursor(self):
        frame = self.frame()
        frame.draws = lambda *args: []
        frame.progress()
        self.assertEqual([], frame.info)
        self.assertEqual(['musicProgressUnavailable'], list(frame.evidence[0]))
        self.assertEqual(1, len(frame.evidence))

    def test_absent_other_theme_or_dp_hook_still_fails(self):
        for style, dp in (('source22-gmframe10', False), (26, False), ('source22-gmframe9', True)):
            with self.subTest(style=style, dp=dp):
                frame = self.frame(style, dp)
                frame.draws = lambda *args: []
                with self.assertRaises(IndexError):
                    frame.progress()

    def test_original_dp_hooks_keep_two_separate_bound_cursors(self):
        frame = self.frame(dp=True)
        called = []
        def draws(name, movie):
            called.append(name)
            return [{'asset': {'package': 'source22/gmframe9'}, 'texture': name,
                     'bounds': {'left': 10, 'top': 20, 'right': 20, 'bottom': 50}}]
        frame.draws = draws
        frame.progress()
        self.assertEqual(['music_point_1p', 'music_point_2p'], called)
        self.assertEqual(2, len(frame.info))
        self.assertEqual(['musicProgress'], list(frame.evidence[0]))

    def test_source_font_keeps_small_glyph_inside_largest_line_without_rescaling(self):
        frame = self.frame('source22-gmframe8')
        font, height = frame.font('gauge', 'source22/gmframe8', {'0': 'digit0', '2': 'digit2'}, 35,
                                  uniform_height=False)
        self.assertEqual(30, height)
        self.assertEqual('original-digit2', frame.fonts[font]['glyphs'][1]['resource'])
        self.assertEqual(35, frame.fonts[font]['glyphs'][1]['advance'])

    def test_existing_uniform_font_contract_remains_strict(self):
        frame = self.frame(26)
        with self.assertRaisesRegex(ValueError, 'height differs'):
            frame.font('gauge', 'gmframe0', {'0': 'digit0', '2': 'digit2'}, 35)

    def panel_fixture(self, player='1p'):
        frame = self.frame('source22-gmframe0-00')
        frame.key, frame.layout = package_key(frame.style), 'p1' if player == '1p' else 'p2'
        frame.player, frame.centre, frame.wide = player, 'center_frame_sp', False
        frame.dx = 35 if player == '1p' else -35
        left, hook = (300 if player == '1p' else 640), 'sco_waku_' + player
        vertices = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (340, 0), (0, 125), (340, 125))]
        draw = {'asset': {'package': frame.key, 'width': 340, 'height': 125}, 'texture': hook,
                'game_bindings': [hook], 'bounds': {'left': left, 'top': 0, 'right': left + 340, 'bottom': 125},
                'source_vertices': vertices, 'projected_vertices': [{**v, 'x': v['x'] + left} for v in vertices],
                'mask_present': False, 'blend': 0, 'mult_color': dict.fromkeys(('r', 'g', 'b', 'a'), 1),
                'add_color': dict.fromkeys(('r', 'g', 'b', 'a'), 0),
                'draw_params': [{'flags': 3, 'region': hook, 'vertexes': [0, 1, 2, 2, 1, 3], 'blend': None}]}
        frame.draws = lambda name, movie: [draw] if (name, movie) == (hook, 'center_frame_sp') else []
        return frame, draw

    def test_source22_score_panel_restores_only_selected_side_before_live_digits(self):
        for player, left in (('1p', 335), ('2p', 605)):
            with self.subTest(player=player):
                frame, _ = self.panel_fixture(player)
                frame.score_panel()
                frame.info.append({'id': 'later-live-score'})
                self.assertEqual('original-sco_waku_' + player, frame.info[0]['resource'])
                self.assertEqual({'x': left / 1280, 'y': 0, 'width': 340 / 1280, 'height': 125 / 720},
                                 frame.info[0]['properties'])
                self.assertEqual('later-live-score', frame.info[1]['id'])
                self.assertEqual('sco_waku_' + player, frame.evidence[0]['scorePanel']['sourceHook'])

    def test_source22_score_panel_does_not_add_to_wide_dp_bare_or_legacy_art(self):
        for style, dp, wide in ((26, False, False), (23, False, False), ('source22-gmframe0', False, False),
                                ('source22-gmframe8', True, False), ('source22-gmframe9', False, True)):
            with self.subTest(style=style, dp=dp, wide=wide):
                frame = self.frame(style, dp)
                frame.wide = wide
                frame.draws = lambda *args: self.fail('Unrelated source must not be inspected')
                frame.score_panel()
                self.assertEqual([], frame.info)
                self.assertEqual([], frame.evidence)

    def test_source22_score_panel_requires_exact_own_hook(self):
        frame, draw = self.panel_fixture()
        draw['asset']['package'] = 's22/basic_frame_00'
        with self.assertRaisesRegex(ValueError, 'qualified original rectangle'):
            frame.score_panel()
        frame.draws = lambda *args: []
        with self.assertRaisesRegex(ValueError, 'requires one original'):
            frame.score_panel()

    def test_source22_score_panel_does_not_flatten_unqualified_geometry_or_colour(self):
        for mutation in ('mesh', 'mask', 'colour', 'indices'):
            with self.subTest(mutation=mutation):
                frame, draw = self.panel_fixture()
                if mutation == 'mesh':
                    draw['projected_vertices'][3]['x'] += 1
                elif mutation == 'mask':
                    draw['mask_present'] = True
                elif mutation == 'colour':
                    draw['mult_color']['a'] = 0.5
                else:
                    draw['draw_params'][0]['vertexes'] = [0, 1, 2]
                with self.assertRaisesRegex(ValueError, 'qualified original rectangle'):
                    frame.score_panel()
                self.assertEqual([], frame.info)

    def range_fixture(self, style='source22-gmframe0-00', layout='p1', wide=False):
        frame = self.frame(style, layout == 'dp')
        frame.key, frame.layout, frame.wide = package_key(style), layout, wide
        frame.player = '2p' if layout == 'p2' else '1p'
        frame.centre = ('center_frame_dp_1p' if frame.dp else
                        'center_frame_wide_' + frame.player if wide else 'center_frame_sp')
        frame.dx = 0 if frame.dp or wide else -35 if layout == 'p2' else 35
        left = 325 if wide and layout == 'p2' else 675 if wide else 500
        top = 628 if wide else 621
        if frame.dp and style == 'source22-gmframe9':
            left, top = 501, 605
        elif frame.dp and style == 'source22-gmframe10':
            top = 628
        vertices = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (280, 0), (0, 64), (280, 64))]
        draw = {'asset': {'package': frame.key, 'width': 280, 'height': 64}, 'texture': 'pm_bpm2',
                'game_bindings': ['bpm_waku'], 'bounds': {'left': left, 'top': top, 'right': left + 280, 'bottom': top + 64},
                'source_vertices': vertices,
                'projected_vertices': [{**v, 'x': v['x'] + left, 'y': v['y'] + top} for v in vertices],
                'mask_present': False, 'blend': 0, 'mult_color': dict.fromkeys(('r', 'g', 'b', 'a'), 1),
                'add_color': dict.fromkeys(('r', 'g', 'b', 'a'), 0),
                'draw_params': [{'flags': 3, 'region': 'pm_bpm2', 'vertexes': [0, 1, 2, 2, 1, 3], 'blend': None}]}
        frame.draws = lambda name, movie: [draw] if (name, movie) == ('bpm_waku', frame.centre) else []
        frame.asset = lambda package, name: (package + '/' + name, draw['asset'])
        return frame, draw

    def test_source22_range_labels_preserve_each_source_and_all_five_layouts(self):
        for style in SOURCE22_AVAILABLE_STYLES:
            for layout, wide in (('p1', False), ('p2', False), ('p1', True), ('p2', True), ('dp', False)):
                with self.subTest(style=style, layout=layout, wide=wide):
                    frame, draw = self.range_fixture(style, layout, wide)
                    frame.bpm_range_panel()
                    self.assertEqual(frame.key + '/pm_bpm2', frame.info[0]['resource'])
                    self.assertEqual({'x': (draw['bounds']['left'] + frame.dx) / 1280,
                                      'y': draw['bounds']['top'] / 720, 'width': 280 / 1280, 'height': 64 / 720},
                                     frame.info[0]['properties'])
                    evidence = frame.evidence[0]['bpmRangePanel']
                    self.assertEqual('bpm_waku', evidence['sourceHook'])
                    self.assertIn('continuously displays', evidence['scope'])
                    self.assertIn('native22 hides', evidence['scope'])

    def test_range_labels_do_not_change_legacy_or_unavailable_archival_theme(self):
        for style in (*BASE_STYLES, *EARLIER_STYLES, 'source22-gmframe0'):
            with self.subTest(style=style):
                frame = self.frame(style)
                frame.draws = lambda *args: self.fail('Unrelated source must not be inspected')
                frame.bpm_range_panel()
                self.assertEqual([], frame.info)
                self.assertEqual([], frame.evidence)

    def test_range_labels_require_exactly_one_own_original_hook(self):
        for mutation in ('missing', 'duplicate', 'family', 'texture', 'hook'):
            with self.subTest(mutation=mutation):
                frame, draw = self.range_fixture()
                if mutation in ('missing', 'duplicate'):
                    frame.draws = lambda *args: [] if mutation == 'missing' else [draw, draw]
                elif mutation == 'family':
                    draw['asset']['package'] = 'gmframe0'
                elif mutation == 'texture':
                    draw['texture'] = 'pm_bpm'
                else:
                    draw['game_bindings'] = ['bpm_waku', 'another-owner']
                with self.assertRaisesRegex(ValueError, 'one original|qualified original rectangle'):
                    frame.bpm_range_panel()
                self.assertEqual([], frame.info)

    def test_range_labels_do_not_flatten_unqualified_mesh_size_or_colour(self):
        for mutation in ('mesh', 'size', 'mask', 'colour', 'add', 'blend', 'indices'):
            with self.subTest(mutation=mutation):
                frame, draw = self.range_fixture()
                if mutation == 'mesh':
                    draw['projected_vertices'][3]['y'] += 1
                elif mutation == 'size':
                    draw['asset']['width'] -= 1
                elif mutation == 'mask':
                    draw['mask_present'] = True
                elif mutation == 'colour':
                    draw['mult_color']['a'] = 0.5
                elif mutation == 'add':
                    draw['add_color']['r'] = 0.1
                elif mutation == 'blend':
                    draw['blend'] = 1
                else:
                    draw['draw_params'][0]['vertexes'] = [0, 1, 2]
                with self.assertRaisesRegex(ValueError, 'qualified original rectangle'):
                    frame.bpm_range_panel()
                self.assertEqual([], frame.info)

    def gauge_fixture(self):
        data = {'sourceComposition': {'contract': COMPOSITION22,
                'aliases24': EARLIER_ALIASES, 'aliases22': SOURCE22_ALIASES}}
        frames = {f'f{frame_token(style)}': {'p1': {'resources': [{'path': 'shared.png'}],
                  'animations': [{'path': f'f{frame_token(style)}.json.br'}]}}
                  for style in retained_styles(data)}
        return data, {'frames': frames, 'files': [{'path': 'shared.png'}] +
                     [{'path': key + '.json.br'} for key in frames]}

    def test_bare_archive_gauge_clip_is_not_an_unreferenced_runtime_file(self):
        data, gauge = self.gauge_fixture()
        frames, files = select_gauge_assets(gauge, data)
        self.assertEqual(17, len(frames))
        self.assertNotIn('f22-gmframe0', frames)
        self.assertNotIn({'path': 'f22-gmframe0.json.br'}, files)
        self.assertIn({'path': 'shared.png'}, files)
        self.assertEqual(18, len(gauge['frames']))

    def test_missing_usable_gauge_or_unqualified_frame_is_not_waived(self):
        for mutation in ('missing', 'unknown', 'file'):
            with self.subTest(mutation=mutation):
                data, gauge = self.gauge_fixture()
                if mutation == 'missing':
                    del gauge['frames']['f22-gmframe8']
                elif mutation == 'unknown':
                    gauge['frames']['unexpected'] = gauge['frames']['f26']
                else:
                    gauge['files'].pop()
                with self.assertRaises(ValueError):
                    select_gauge_assets(gauge, data)


if __name__ == '__main__':
    unittest.main()
