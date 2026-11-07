"""Qualified22 HUD omissions and unequal original glyph heights, without private art."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_standard_skin import Frame, select_gauge_assets
from standard_source_catalog import (COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES,
                                     retained_styles, frame_token)


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
