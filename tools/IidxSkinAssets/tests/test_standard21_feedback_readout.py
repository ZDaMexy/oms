"""Actual-state21 reading and explicit host feedback boundaries, no private art."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_cover_readout import add_cover_readouts
from standard_feedback import build_feedback
from standard_source_catalog import COMPOSITION21, EARLIER_ALIASES, SOURCE22_ALIASES, SOURCE21_ALIASES
from customization_modules import material_contexts
import test_standard_feedback as feedback_fixture
import test_standard_cover_readout as cover_fixture
from test_standard21_author_hud import rectangle


class Standard21ReadoutTests(unittest.TestCase):
    def test_current_only_green_in_all_layouts_has_real_state_and_two_left_anchors(self):
        for layout, expected in (('p1', [129]), ('p2', [1021]), ('dp', [397, 767])):
            f = cover_fixture.SyntheticFrame(layout)
            add_cover_readouts(f, {'fonts': {'normal': 'fixture.font2', 'small': 'fixture.font5'}}, source_family='s21')
            nodes = list(cover_fixture.walk(f.info))
            sources = {b['source'] for b in f.scene['bindings']}
            self.assertIn('scroll.green-number', sources)
            self.assertFalse(any('green-number-min' in s or 'green-number-max' in s or 'range-available' in s for s in sources))
            self.assertFalse(any('.range' in n['id'] for n in nodes))
            self.assertNotIn('option.start-panel', sources)
            for side, x in enumerate(expected, 1):
                row = next(n for n in nodes if n['id'].endswith(f'.p{side}.sudden.normal'))
                self.assertEqual(x / 1280, row['properties']['x'])
                track = next(t for t in f.scene['tracks'] if t['target'].endswith(f'.p{side}.sudden.normal.green') and t['property'] == 'x')
                self.assertEqual([(0, 90 / 1280), (1000, 88 / 1280)], [(k['time'], k['value']) for k in track['keyframes']])
            receipt = f.evidence[-1]['coverReadouts']
            self.assertNotIn('sourceCallerRVA', receipt)
            self.assertIn('Supplied21', receipt['sourceCaller'])
            self.assertEqual(482, receipt['sourceGeometry']['bottomClip'])

    def test_source21_feedback_owns_its_panel_and_does_not_claim26_addresses_as21(self):
        b = feedback_fixture.StandardFeedbackTests()
        b.setUp()
        self.addCleanup(b.doCleanups)
        b.inventory['sourceComposition'] = {'contract': COMPOSITION21, 'aliases24': EARLIER_ALIASES,
                                            'aliases22': SOURCE22_ALIASES, 'aliases21': SOURCE21_ALIASES}
        b.texture['package'] = 'contents/data/graphic/s21/gameparts.ifs'
        b.reference['package'], b.reference['common_family'] = 'source21/gmframe0', 's21'
        for value in b.reference['layouts'].values():
            for d in value['draws']:
                if d['texture'] == 'judge_waku':
                    d['asset']['package'] = 's21/gameparts'
        b.reference['layouts']['center_frame_sp']['draws'].append(b.panel(807, 544, '2p'))
        b.reference['layouts']['center_frame_sp']['draws'][-1]['asset']['package'] = 's21/gameparts'
        for movie, sides in (('1p_frame', [('1p', 48)]), ('2p_frame', [('2p', 940)]),
                             ('center_frame_dp_1p', [('1p', 304), ('2p', 684)])):
            draws = b.reference['layouts'][movie]['draws']
            draws[:] = [d for d in draws if d['texture'] != 'lane']
            draws.extend(rectangle('source21/gmframe0', 'sp_lane_' + player, '', 292, 484, left, 0) for player, left in sides)
        b.save()
        for layout, movie, dx in (('p1', 'center_frame_sp', 35), ('p2', 'center_frame_sp', -35),
                                  ('p1', 'center_frame_wide_1p', 0), ('p2', 'center_frame_wide_2p', 0),
                                  ('dp', 'center_frame_dp_1p', 0)):
            with self.subTest(layout=layout, movie=movie):
                result = build_feedback(b.library, b.map, style='source21-gmframe0', layout=layout, centre_movie=movie,
                    centre_dx=dx, contexts=[c for c in material_contexts() if c['layout'] == layout],
                    font_path=b.font_file, output=b.output, prepared_font=b.font)
                receipt = result['receipt']
                self.assertEqual('s21/gameparts', receipt['judgeHook']['asset']['package'])
                self.assertEqual(dx, receipt['callerCentreOffset'])
                self.assertNotIn('nativeEvidence', receipt)
                self.assertEqual(26, receipt['hostFeedbackModelEvidence']['softwareVersion'])
                self.assertIn('not claimed as21', receipt['fastSlow'])
                self.assertTrue(all('source21' not in r['path'] or 's21' in r['path'] for r in result['resources']))


if __name__ == '__main__':
    unittest.main()
