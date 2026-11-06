"""The decorative lane animation must not resize the host's playfield."""
import copy
from pathlib import Path
import sys
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_lane_geometry import source_lane_regions


def reference(style, layout, top=30.72265625):
    spans = [(304, 596), (684, 976)] if layout == 'dp' else ([(940, 1232)] if layout == 'p2' else [(48, 340)])
    movie = 'center_frame_dp_1p' if layout == 'dp' else ('2p_frame' if layout == 'p2' else '1p_frame')
    draws = [{'texture': 'sp_lane_1p' if i == 0 else 'sp_lane_2p',
              'asset': {'package': f'gmframe{style:02}', 'width': 292, 'height': 484},
              'bounds': {'left': left, 'right': right, 'top': top, 'bottom': 484},
              'source_vertices': [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (292, 0), (0, 484), (292, 484))],
              'game_bindings': []} for i, (left, right) in enumerate(spans)]
    return {'layouts': {movie: {'draws': draws}}}, draws


class LaneGeometryTests(unittest.TestCase):
    def test_all_earlier_contexts_retain_full_source_domain_and_bottom(self):
        for style in (23, 24):
            for layout in ('p1', 'p2', 'dp'):
                with self.subTest(style=style, layout=layout):
                    source, draws = reference(style, layout, top=0 if style == 23 else 30.72265625)
                    saved = copy.deepcopy(source)
                    regions = source_lane_regions(source, style, layout)
                    self.assertEqual(2 if layout == 'dp' else 1, len(regions))
                    self.assertTrue(all(r['top'] == 0 and r['bottom'] == 484 for r in regions))
                    self.assertEqual([d['bounds']['left'] for d in draws], [r['left'] for r in regions])
                    self.assertEqual(saved, source)

    def test_unqualified_source_plane_cannot_move_or_stretch_gameplay(self):
        for kind in ('width', 'bottom', 'vertices', 'package'):
            with self.subTest(kind=kind):
                source, draws = reference(24, 'p1')
                if kind == 'width': draws[0]['asset']['width'] = 294
                elif kind == 'bottom': draws[0]['bounds']['bottom'] = 480
                elif kind == 'vertices': draws[0]['source_vertices'][2]['y'] = 480
                else: draws[0]['asset']['package'] = 'gmframe26'
                with self.assertRaisesRegex(ValueError, 'dimensions or bottom anchor'):
                    source_lane_regions(source, 24, 'p1')

    def test_missing_or_duplicate_plane_is_rejected(self):
        for duplicate in (False, True):
            source, draws = reference(23, 'p2')
            if duplicate: draws.append(copy.deepcopy(draws[0]))
            else: draws.clear()
            with self.assertRaisesRegex(ValueError, 'exact source lane planes'):
                source_lane_regions(source, 23, 'p2')

    def test_existing_named_lane_geometry_is_unchanged(self):
        source, draws = reference(26, 'p1', top=0)
        draws[0]['texture'] = 'lane_1p'
        draws[0]['game_bindings'] = ['lane_1p']
        self.assertEqual([draws[0]['bounds']], source_lane_regions(source, 26, 'p1'))


if __name__ == '__main__':
    unittest.main()
