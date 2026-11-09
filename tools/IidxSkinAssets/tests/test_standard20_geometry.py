"""Synthetic Original20 panels, single-DP lane partitions and BGA-cover contracts."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_bga import (CENTRES, centre_movies, cover_frame_state, cover_sizes,
                          cover_source_frames, project_hole)
from standard_controls import cabinet_keys
from standard_lane_geometry import lane_movie, source_lane_regions
from standard_presentation_options import (lane_layer_movies, lane_roots, pendual_baked_surfaces,
                                           partition_baked_lane_clip)
import test_standard_bga as bga_fixture
from test_standard_pendual_presentation import fixture as baked_fixture

STYLE = 'source20-gmframe0'


def reference(layout):
    movie = lane_movie(STYLE, layout)
    domains = ((304, '1p'), (684, '2p')) if layout == 'dp' else (((940, '2p'),) if layout == 'p2' else ((48, '1p'),))
    draws = []
    for left, player in domains:
        tx = left - (5 if player == '2p' else 57)
        texture = 'frame_lane_' + player
        draws.append({'texture': texture, 'geometry': movie + '_synthetic, ' + texture,
                      'game_bindings': [], 'asset': {'package': 'source20/gmframe0', 'name': texture,
                                                    'width': 354, 'height': 609},
                      'source_vertices': [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (354, 0), (0, 609), (354, 609))],
                      'matrix': {'a': 1, 'b': 0, 'c': 0, 'd': 1, 'tx': tx, 'ty': 0},
                      'bounds': {'left': tx, 'top': 0, 'right': tx + 354, 'bottom': 609}})
    return {'package': 'source20/gmframe0', 'common_family': 's20', 'layouts': {movie: {'draws': draws}}}


class Standard20ControlGeometryTests(unittest.TestCase):
    def test_each_deck_uses_its_own_baked_panel_and_measured_cap(self):
        for layout in ('p1', 'p2', 'dp'):
            for group in range(2 if layout == 'dp' else 1):
                with self.subTest(layout=layout, group=group):
                    movie, field, keys = cabinet_keys(reference(layout), STYLE, layout, group)
                    self.assertEqual(lane_movie(STYLE, layout), movie)
                    self.assertEqual(['white', 'black', 'white', 'black', 'white', 'black', 'white'],
                                     [key['colour'] for key in keys])
                    right = group == 1 if layout == 'dp' else layout == 'p2'
                    self.assertEqual([10 if right else 124, 494, 28, 47], keys[0]['cap'])
                    self.assertEqual('source20/gmframe0', keys[0]['draw']['asset']['package'])
                    self.assertEqual(484, field['bottom'])

    def test_later_family_equal_size_panel_is_not_a_source20_fallback(self):
        for mutation in ('family', 'source', 'matrix', 'vertices'):
            with self.subTest(mutation=mutation):
                value = reference('p1')
                panel = value['layouts']['1p_frame']['draws'][0]
                if mutation == 'family':
                    value['common_family'] = 's22'
                elif mutation == 'source':
                    panel['asset']['package'] = 'source22/gmframe9'
                elif mutation == 'matrix':
                    panel['matrix']['tx'] += 1
                else:
                    panel['source_vertices'][1]['x'] -= 1
                with self.assertRaises(ValueError):
                    cabinet_keys(value, STYLE, 'p1', 0)

    def test_a_split_dp_movie_cannot_replace_the_one_source20_lane_centre(self):
        value = reference('dp')
        value['layouts']['center_frame_dp_1p'] = value['layouts'].pop('center_frame_dp')
        with self.assertRaises(KeyError):
            source_lane_regions(value, STYLE, 'dp')

    def test_single_source20_dp_partition_preserves_both_decks(self):
        source, layout, clip, geometry = baked_fixture('source22-gmframe9', 'center_frame_dp_1p')
        source.update(package='source20/gmframe0', sourceMovie='center_frame_dp')
        layout['package'] = source['package']
        for draw in layout['draws']:
            draw['asset']['package'] = source['package']
        surfaces = pendual_baked_surfaces(source, layout, STYLE)
        self.assertEqual({'lane_1p', 'lane_2p'}, set(surfaces))
        self.assertEqual([57, 0, 349, 484], surfaces['lane_1p']['rectangle'])
        self.assertEqual([5, 0, 297, 484], surfaces['lane_2p']['rectangle'])
        with self.assertRaisesRegex(ValueError, 'not whole-panel'):
            lane_roots(source, layout, style=STYLE)
        layers, _ = partition_baked_lane_clip(source, clip, surfaces, geometry, range(5))
        self.assertEqual(2, sum(layer['kind'] == 'lane' for layer in layers))
        self.assertEqual(('1p_frame', '2p_frame', 'center_frame_dp'), lane_layer_movies(STYLE))
        self.assertEqual(('1p_frame', '2p_frame', 'center_frame_dp_1p'), lane_layer_movies('source21-gmframe0'))


class Standard20BgaGeometryTests(unittest.TestCase):
    def test_original_cover_sizes_are_not_normalized_to_later_art(self):
        root, source = bga_fixture.CoverTimelineTests().source()
        source['geometry'][0]['vertices'] = [[0, 0, 0], [640, 0, 0], [0, 480, 0], [640, 480, 0]]
        result = cover_frame_state(root, source, sizes=cover_sizes('s20'))
        self.assertEqual([[240, 0], [880, 0], [880, 480], [240, 480]], result['quad'])
        self.assertEqual({'left': 250, 'top': 20, 'right': 870, 'bottom': 460},
                         project_hole(result['quad'], [640, 480], [10, 20, 630, 460]))
        for family in ('0', 's21', 's22'):
            with self.subTest(family=family), self.assertRaises(ValueError):
                cover_frame_state(root, source, sizes=cover_sizes(family))

    def test_source20_cover_domain_contains_only_its_actual_dp_movie(self):
        self.assertEqual((*CENTRES[:4], 'center_frame_dp'), centre_movies(STYLE))
        self.assertEqual(180, cover_source_frames(STYLE))
        self.assertEqual((768, 576), cover_sizes('s20')['anime_cover2'])
        self.assertEqual(CENTRES, centre_movies('source21-gmframe0'))
        self.assertEqual(300, cover_source_frames('source21-gmframe0'))


if __name__ == '__main__':
    unittest.main()
