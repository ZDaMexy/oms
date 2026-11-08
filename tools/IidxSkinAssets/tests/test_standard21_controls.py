"""Independent21 panel ownership and input topology, using synthetic rasters."""
import copy
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_controls import cabinet_keys, quad_for_host
from test_standard21_author_hud import rectangle


def reference(layout):
    movie = 'center_frame_dp_1p' if layout == 'dp' else '2p_frame' if layout == 'p2' else '1p_frame'
    draws = []
    for player, left in ((('1p', 304), ('2p', 684)) if layout == 'dp' else
                         (('2p', 940),) if layout == 'p2' else (('1p', 48),)):
        draws.append(rectangle('source21/gmframe0', 'sp_lane_' + player, '', 292, 484, left, 0))
        panel = rectangle('source21/gmframe0', 'sp_key_' + player, '', 322, 86, left - 24, 482)
        panel['matrix']['b'] = .009 if player == '2p' else -.009
        panel['matrix']['c'] = -.009 if player == '2p' else .009
        draws.append(panel)
    return {'package': 'source21/gmframe0', 'common_family': 's21', 'layouts': {movie: {'draws': draws}}}


class Standard21ControlsTests(unittest.TestCase):
    def test_three_layouts_keep_seven_caps_per_deck_and_original_affine(self):
        for layout in ('p1', 'p2', 'dp'):
            for group in range(2 if layout == 'dp' else 1):
                with self.subTest(layout=layout, group=group):
                    _, bounds, keys = cabinet_keys(reference(layout), 'source21-gmframe0', layout, group)
                    self.assertEqual(['white', 'black', 'white', 'black', 'white', 'black', 'white'], [k['colour'] for k in keys])
                    right = group == 1 if layout == 'dp' else layout == 'p2'
                    black = keys[1]
                    self.assertEqual(20 if right else 18, black['cap'][2])
                    self.assertEqual('source21/gmframe0', black['draw']['asset']['package'])
                    # A strict original panel remains rotated; do not turn it into a bounds-only sprite.
                    q = quad_for_host(black, {'width': 42, 'height': 62}, [bounds['left'] + 80, 484, 26, 80])
                    self.assertNotEqual(q['vertices'][0][1], q['vertices'][1][1])

    def test_equal_size_later_family_panel_cannot_replace_original21(self):
        for mutation in ('family', 'size', 'geometry'):
            r = reference('p1')
            draw = r['layouts']['1p_frame']['draws'][1]
            if mutation == 'family':
                draw['asset']['package'] = 'source22/gmframe10'
            elif mutation == 'size':
                draw['asset']['width'] = 323
            else:
                draw['source_vertices'][1]['x'] = 321
            with self.assertRaisesRegex(ValueError, '21 key panel'):
                cabinet_keys(r, 'source21-gmframe0', 'p1', 0)

    def test_duplicate_or_missing_panel_is_not_a_silent_fallback(self):
        for duplicate in (False, True):
            r = reference('p1')
            draws = r['layouts']['1p_frame']['draws']
            draws.append(copy.deepcopy(draws[-1])) if duplicate else draws.pop()
            with self.assertRaisesRegex(ValueError, 'one 21 original'):
                cabinet_keys(r, 'source21-gmframe0', 'p1', 0)


if __name__ == '__main__':
    unittest.main()
