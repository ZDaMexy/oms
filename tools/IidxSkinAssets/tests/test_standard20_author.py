"""Original20 author routing and own-source HUD; synthetic inputs only."""
import json
import math
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from build_standard_skin import Frame, box, document, frame_label, sha
from test_standard21_author_hud import rectangle


STYLE = 'source20-gmframe0'


class Source20Frame(Frame):
    def __init__(self, layout='p1'):
        self.style, self.key, self.layout = STYLE, 'source20/gmframe0', layout
        self.dp, self.p2, self.wide = layout == 'dp', layout == 'p2', False
        self.player = '2p' if self.p2 else '1p'
        self.side = 'dp_frame' if self.dp else self.player + '_frame'
        self.centre = 'center_frame_dp' if self.dp else 'center_frame_sp'
        self.dx, self.prefix = 0, 'fixture.' + layout
        self.scene, self.info, self.evidence = document(self.prefix), [], []
        self.resources, self.fonts, self.calls = {}, {}, []
        self.empty = 'fixture.empty'
        self.owner = SimpleNamespace(solid_path='synthetic/solid.png', index={})
        self.map = {'layouts': {self.side: {'draws': []}, self.centre: {'draws': []}}}
        side = self.map['layouts'][self.side]['draws']
        side.append(rectangle('s20/gameparts', 'playm_gauge_normal_1p', 'gg_bar_' + self.player, 300, 21))
        side.extend(rectangle('s20/gameparts', 'playm_gg_00', f'percent_{self.player}_{i}', 9, 11, x, 500)
                    for i, x in enumerate((210, 192, 178)))
        side.append(rectangle('s20/gameparts', 'playm_another', 'diff_' + self.player, 37, 9))
        for draw in side:
            draw['uv'] = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (1, 0), (0, 1), (1, 1))]

    def asset(self, package, name):
        self.calls.append((package, name))
        if package not in ('s20/gameparts', self.key):
            raise AssertionError('Cross-family artwork requested')
        difficulty = {'playm_beginner': (41, 9), 'playm_normal': (29, 9),
                      'playm_hyper': (15, 9), 'playm_another': (37, 9)}
        labels = {'playm_score': (19, 5), 'playm_maxcombo': (25, 5),
                  'playm_bpm_min': (13, 4), 'playm_bpm_max': (17, 4)}
        size = ((9, 11) if name.startswith('playm_gg_dp_') else
                (14, 13) if name.startswith('playm_gg_') else
                (66, 72) if name == 'playm_music_point' else
                (21, 17) if name.startswith('playm_bpm_m') else
                (34, 20) if name.startswith(('playm_sco0', 'playm_bpm0')) else
                difficulty.get(name, labels.get(name, (300, 21))))
        return package + '/' + name, {'width': size[0], 'height': size[1]}


class Standard20AuthorTests(unittest.TestCase):
    def numeric_frame(self):
        frame = Source20Frame()
        frame.fonts['fixture.digits'] = {'lineHeight': 5, 'glyphs': [
            {'character': str(i), 'resource': f'fixture.digit{i}', 'advance': 7} for i in range(10)]}
        draw = rectangle('s20/gameparts', 'playm_sco00', 'score_1p_0', 7, 5, 50, 100)
        frame.map['layouts'][frame.centre]['draws'].append(draw)
        return frame

    def test_theme_label_keeps_original20_distinct_from_later_custom(self):
        self.assertEqual('20 tricoro (Original)', frame_label(STYLE))
        self.assertNotEqual(frame_label(STYLE), frame_label('source22-gmframe9'))

    def test_single_dp_movies_are_selected_without_later_aliases(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'skin-reference-map.json').write_text(json.dumps({
                'stage': {'width': 1280, 'height': 720, 'fps': 60.0},
                'package': 'source20/gmframe0', 'common_family': 's20'}), encoding='utf-8')
            owner = SimpleNamespace(empty_path='empty.png')
            with patch('build_standard_skin.centre_translation', return_value=0):
                frame = Frame(owner, root, STYLE, 'dp')
            self.assertEqual(('dp_frame', 'center_frame_dp'), (frame.side, frame.centre))

    def test_later_source_research_cannot_be_presented_as_original20(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'skin-reference-map.json').write_text(json.dumps({
                'stage': {'width': 1280, 'height': 720, 'fps': 60.0},
                'package': 'source22/gmframe9', 'common_family': 's22'}), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'own source20/s20 identity'):
                Frame(SimpleNamespace(empty_path='empty.png'), root, STYLE, 'dp')

    def test_dp_graph_off_retains_all_four_own_source_windows(self):
        from PIL import Image
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            frame = Source20Frame('dp')
            frame.research, frame.owner.Image = root, Image
            frame.map['layouts'][frame.centre]['static_frame'] = 'own20-centre.png'
            frame.map['layouts'][frame.side]['static_frame'] = 'own20-side.png'
            frame.map['layouts'][frame.centre]['draws'].append(
                rectangle(frame.key, 'own20-lower-monitor', 'qpro_bg', 232, 340, 12, 360))
            frame.field_rects = lambda: [box(304, 0, 292, 484), box(684, 0, 292, 484)]
            image = Image.new('RGBA', (1280, 720), (0, 0, 0, 255))
            for bounds in ((12, 48, 244, 356), (12, 364, 244, 682),
                           (1036, 48, 1268, 356), (1036, 364, 1268, 682)):
                image.paste((0, 0, 0, 0), bounds)
            image.save(root / 'own20-centre.png')
            Image.new('RGBA', image.size).save(root / 'own20-side.png')
            two = frame.apertures()
            four = frame.apertures(graph_visible=False)
            self.assertEqual([box(12, 48, 232, 308), box(12, 364, 232, 298)], two)
            self.assertEqual([*two, box(1036, 48, 232, 308), box(1036, 364, 232, 298)], four)
            geometry = next(row['source20DpMonitorGeometry'] for row in reversed(frame.evidence)
                            if 'source20DpMonitorGeometry' in row)
            self.assertEqual(['center_frame_dp', 'dp_frame'], geometry['sourceMovies'])
            self.assertEqual('source20/gmframe0', geometry['sourcePackage'])
            self.assertFalse(geometry['graphVisible'])
            self.assertIn('Original20 DP Graph-OFF', frame.evidence[-1]['scope'])

    def test_original20_single_player_does_not_gain_unqualified_four_window_mode(self):
        frame = Source20Frame()
        with self.assertRaisesRegex(ValueError, 'independently qualified original20/21 DP'):
            frame.apertures(graph_visible=False)

    def test_dp_frame_build_forwards_its_own_graph_off_windows_to_bga(self):
        with tempfile.TemporaryDirectory() as directory:
            frame = Source20Frame('dp')
            frame.research = Path(directory)
            frame.owner.output, frame.owner.receipts, frame.owner.bga_contexts = Path(directory), [], []
            frame.map['stage'] = {'width': 1280, 'height': 720, 'fps': 60.0}
            frame.ini, frame.animations = [], {}
            frame.background = lambda: 'synthetic/background'
            for method in ('numbers', 'header', 'gauge', 'progress', 'graph', 'gauge_feedback', 'feedback', 'reading'):
                setattr(frame, method, lambda: None)
            frame.template = lambda *args: 'synthetic/information'
            frame.controls = lambda: {}
            frame.material_ini = lambda *args: []
            two = [box(12, 48, 232, 308), box(12, 364, 232, 298)]
            four = [*two, box(1036, 48, 232, 308), box(1036, 364, 232, 298)]
            frame.apertures = lambda graph_visible=True: two if graph_visible else four
            frame.build()
            context, = frame.owner.bga_contexts
            self.assertEqual(STYLE, context['style'])
            self.assertEqual('dp', context['layout'])
            self.assertEqual(two, context['apertures'])
            self.assertEqual(four, context['dpGraphOffApertures'])

    def background_frame(self, fixed_times=(1000, 1000), style=STYLE):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            frame = Source20Frame('dp')
            frame.style = style
            frame.research = root / 'research'
            frame.owner.presentation_assets = root / 'presentation'
            frame.map['stage'] = {'fps': 60}
            frame.owner.copy = lambda *args: None
            roots = []
            frame.template = lambda name, root, slot: roots.append(root) or name
            inputs = []
            for movie in (frame.centre, frame.side):
                source = frame.research / 'recorded' / movie / 'export.json'
                source.parent.mkdir(parents=True)
                original = {'source': {'movie': movie}, 'motion': {'receipt': {'loopStartFrame': 60}}}
                source.write_text(json.dumps(original), encoding='utf-8')
                if movie == frame.centre:
                    graph = source.with_name('source-root.json')
                    graph.write_text('{}', encoding='utf-8')
                    split = frame.owner.presentation_assets / frame.key / movie / 'export.json'
                    split.parent.mkdir(parents=True)
                    split.write_text(json.dumps({'contract': 'oms-standard-lane-layers.v1',
                        'source': original['source'], 'receipt': {'sourceExportSHA256': sha(source),
                                                                  'sourceRootSHA256': sha(graph)}}), encoding='utf-8')

            def reuse(original, exported, split):
                inputs.append((original['source']['movie'], split))
                branch = {'files': [], 'resources': [], 'animations': [], 'bindings': [],
                          'node': {'id': 'fixture.original', 'blend': 'normal'}}
                return [branch], {'fixedClockMs': fixed_times[len(inputs) - 1]}

            with patch('build_standard_skin.steady_program_reuse', side_effect=reuse):
                self.assertEqual('background', frame.background())
            self.assertEqual([('center_frame_dp', True), ('dp_frame', False)], inputs)
            return frame, roots[0]

    def test_dp_background_consumes_each_movie_once_and_shares_equal_steady_time(self):
        frame, root = self.background_frame()
        self.assertEqual(1, len(frame.scene['numericSources']))
        self.assertFalse(any('_1p' in item['id'] or '_2p' in item['id'] for item in frame.scene['numericSources']))
        steady = [item['children'][0]['clock'] for item in root['children'] if item['id'].endswith('.steady.gate')]
        self.assertEqual(['value.' + frame.scene['numericSources'][0]['id']] * 2, steady)

    def test_unequal_movie_steady_times_keep_independent_sampling(self):
        frame, root = self.background_frame((1000, 1500))
        self.assertEqual([1000, 1500], [item['left'] for item in frame.scene['numericSources']])
        steady = [item['children'][0]['clock'] for item in root['children'] if item['id'].endswith('.steady.gate')]
        self.assertEqual(['value.' + item['id'] for item in frame.scene['numericSources']], steady)

    def test_clock_sharing_does_not_rewrite_retained_earlier_themes(self):
        frame, _ = self.background_frame(style='source21-gmframe0')
        self.assertEqual(2, len(frame.scene['numericSources']))

    def test_difficulty_preserves_each_source_png_size_without_later_level_art(self):
        frame = Source20Frame()
        frame.header()
        sprites = [item for item in frame.info if item['type'] == 'sprite']
        self.assertEqual([41 / 1280, 29 / 1280, 15 / 1280, 37 / 1280],
                         [item['properties']['width'] for item in sprites])
        self.assertTrue(all(package == 's20/gameparts' for package, _ in frame.calls))
        self.assertFalse(any('lvlnum' in name for _, name in frame.calls))
        self.assertIn('No level/lvlnum hook', frame.evidence[-1]['source20Header']['level'])

    def test_difficulty_does_not_flatten_changed_source_geometry(self):
        frame = Source20Frame()
        source = next(draw for draw in frame.map['layouts'][frame.side]['draws']
                      if 'diff_1p' in draw['game_bindings'])
        source['source_vertices'][1]['y'] = 3
        with self.assertRaisesRegex(ValueError, 'unscaled source rectangle'):
            frame.header()

    def test_normalized_texture_geo_is_accepted_only_for_the_whole_source_image(self):
        frame = Source20Frame()
        source = next(draw for draw in frame.map['layouts'][frame.side]['draws']
                      if 'diff_1p' in draw['game_bindings'])
        source['draw_params'][0]['flags'] = 0x43
        frame.header()
        source['uv'][1]['x'] = .75
        with self.assertRaisesRegex(ValueError, 'unscaled source rectangle'):
            frame.header()

    def test_plain_atlas_uv_must_match_the_declared_source_region(self):
        frame = Source20Frame()
        source = next(draw for draw in frame.map['layouts'][frame.side]['draws']
                      if 'diff_1p' in draw['game_bindings'])
        source['asset']['uvRect'] = [40, 77, 16, 25]
        source['uv'] = [{'x': x / 128, 'y': y / 64, 'z': 0}
                        for x, y in ((40, 16), (77, 16), (40, 25), (77, 25))]
        self.assertEqual(source['bounds'], Frame.source20_rectangle(source, (128, 64)))
        with self.assertRaisesRegex(ValueError, 'unscaled source rectangle'):
            Frame.source20_rectangle(source)
        source['uv'][0]['y'] = 15 / 64
        with self.assertRaisesRegex(ValueError, 'unscaled source rectangle'):
            Frame.source20_rectangle(source, (128, 64))

    def test_dp_gauge_uses_separate_own_digit_set(self):
        frame = Source20Frame('dp')
        frame.gauge()
        gauge_digits = [name for _, name in frame.calls if name.startswith('playm_gg')]
        self.assertTrue(gauge_digits)
        self.assertTrue(all(name.startswith('playm_gg_dp_') for name in gauge_digits))
        self.assertEqual(11, next(iter(frame.fonts.values()))['lineHeight'])
        self.assertFalse(any('aeasy' in name for _, name in frame.calls))
        self.assertEqual('s20', frame.evidence[-1]['sourceFamily'])

    def test_sp_gauge_retains_own_sp_set_and_original_anchor_advance(self):
        frame = Source20Frame('p2')
        frame.gauge()
        font = next(iter(frame.fonts.values()))
        self.assertEqual(18, font['glyphs'][0]['advance'])
        self.assertEqual(13, font['lineHeight'])
        self.assertFalse(any(name.startswith('playm_gg_dp_') for _, name in frame.calls))

    def test_gauge_rejects_foreign_art_owner(self):
        frame = Source20Frame()
        frame.map['layouts'][frame.side]['draws'][0]['asset']['package'] = 's21/gameparts'
        with self.assertRaisesRegex(ValueError, 'own gameparts owner'):
            frame.gauge()

    def test_independent_progress_draw_uses_own_art_and_native_positions_without_afp_hook(self):
        for layout, expected_x in (('p1', [2]), ('p2', [1212]), ('dp', [258, 956])):
            with self.subTest(layout=layout):
                frame = Source20Frame(layout)
                frame.dx = 35  # This standalone caller is outside centre-movie translation.
                frame.progress()
                self.assertEqual([('s20/gameparts', 'playm_music_point')], frame.calls)
                self.assertEqual(expected_x, [round(item['properties']['x'] * 1280) for item in frame.info])
                self.assertEqual(2000, frame.scene['preparationDuration'])
                for item in frame.info:
                    cursor, = item['children']
                    intro, = [track for track in frame.scene['tracks'] if track['target'] == item['id']]
                    self.assertEqual('s20/gameparts/playm_music_point', cursor['resource'])
                    self.assertEqual(66, round(item['properties']['width'] * 1280))
                    self.assertEqual(72, round(item['properties']['height'] * cursor['properties']['height'] * 720))
                    self.assertEqual('additive', cursor['blend'])
                    for progress in (0, .5, 1):
                        self.assertAlmostEqual(10 + 405 * progress,
                            (intro['keyframes'][-1]['value'] + item['properties']['height'] * progress) * 720)
                self.assertEqual(len(expected_x), len(frame.scene['bindings']))
                self.assertTrue(all(binding['source'] == 'timing.progress' for binding in frame.scene['bindings']))
                self.assertIn('not reconstructed', frame.evidence[-1]['source20MusicProgress']['beatAlpha'])

    def test_progress_intro_samples_native_sine_before_host_play_and_keeps_beat_phase_explicit(self):
        frame = Source20Frame()
        frame.scene['preparationDuration'] = 2500
        frame.progress()
        self.assertEqual(2500, frame.scene['preparationDuration'])
        travel, = frame.info
        cursor, = travel['children']
        intro, = [track for track in frame.scene['tracks'] if track['target'] == travel['id']]
        self.assertEqual(('presentation.elapsed', 'y', 'step', False),
            (intro['clock'], intro['property'], intro['easing'], intro['loop']))
        self.assertEqual(121, len(intro['keyframes']))
        self.assertEqual((0, 415), (intro['keyframes'][0]['time'], round(intro['keyframes'][0]['value'] * 720)))
        self.assertEqual((2000, 10), (intro['keyframes'][-1]['time'], round(intro['keyframes'][-1]['value'] * 720)))
        self.assertEqual(129, round(intro['keyframes'][60]['value'] * 720))
        self.assertNotEqual(128, round(intro['keyframes'][60]['value'] * 720))
        for tick, key in enumerate(intro['keyframes']):
            self.assertAlmostEqual(tick * 1000 / 60, key['time'])
            self.assertAlmostEqual(415 - math.trunc(405 * math.sin(tick * math.pi / 240)), key['value'] * 720)
        beat, = [track for track in frame.scene['tracks'] if track['target'] == cursor['id']]
        self.assertEqual(('timing.beat', 'opacity', 'step', True),
            (beat['clock'], beat['property'], beat['easing'], beat['loop']))
        for phase in (0, .001, .019, .02, .28, .999):
            value = [key['value'] for key in beat['keyframes'] if key['time'] <= phase][-1]
            self.assertAlmostEqual(1 - math.floor(phase * 50) / 100, value)
        # Sampling the source phase function at its original integer beat
        # counters preserves alpha values even though host phase cadence differs.
        for bpm in (72, 128, 150, 250):
            period = 3600 // bpm
            for tick in range(period):
                phase = tick / period
                value = [key['value'] for key in beat['keyframes'] if key['time'] <= phase][-1]
                self.assertAlmostEqual(1 - (tick * 50 // period) / 100, value)
        self.assertEqual(1, beat['keyframes'][-1]['value'])

    def test_progress_rejects_a_cropped_source_sprite(self):
        frame = Source20Frame()
        frame.asset = lambda *args: ('synthetic.point', {'width': 65, 'height': 72})
        with self.assertRaisesRegex(ValueError, 'complete own-source sprite'):
            frame.progress()

    def test_dim_leading_zeros_follow_formatted_value_without_covering_significant_digits(self):
        frame = self.numeric_frame()
        field = frame.source20_integer('score', 'score.value', 'score_1p_0', 'fixture.digits', 5, 4, 7)
        zero_nodes = [item for item in frame.info if '.leading-zero.' in item['id']]
        self.assertEqual(3, len(zero_nodes))
        self.assertTrue(all(item['type'] == 'text' and item['properties']['text'] == '0' and
                            item['properties']['bitmap-font'] == 'fixture.digits' for item in zero_nodes))
        tracks = {track['target']: track for track in frame.scene['tracks'] if track['property'] == 'opacity'}
        self.assertEqual([50 / 1280, 57 / 1280, 64 / 1280],
                         [item['properties']['x'] for item in zero_nodes])
        self.assertEqual('right', field['properties']['alignment'])
        self.assertEqual(28 / 1280, field['properties']['width'])
        self.assertIs(field, frame.info[-1])
        for value in (0, 9, 10, 99, 100, 999, 1000, 9999, 10000):
            with self.subTest(value=value):
                dim = 0
                for item in zero_nodes:
                    track = tracks[item['id']]
                    self.assertEqual('score.value', track['clock'])
                    opacity = [key['value'] for key in track['keyframes'] if key['time'] <= value][-1]
                    self.assertIn(opacity, (0, .3))
                    dim += opacity == .3
                self.assertEqual(max(0, 4 - len(str(round(value)))), dim)

    def test_bpm_fractional_rounding_changes_dim_zero_at_the_same_formatted_boundary(self):
        frame = self.numeric_frame()
        draw = frame.map['layouts'][frame.centre]['draws'][0]
        draw['game_bindings'] = ['bpm_0']
        frame.source20_integer('bpm', 'timing.bpm', 'bpm_0', 'fixture.digits', 5, 3, 7)
        tracks = [track for track in frame.scene['tracks'] if track['property'] == 'opacity']
        for bpm in (0, 9.4, 9.5, 99.4, 99.5, 120.125):
            with self.subTest(bpm=bpm):
                dim = 0
                for track in tracks:
                    self.assertEqual('timing.bpm', track['clock'])
                    dim += [key['value'] for key in track['keyframes'] if key['time'] <= bpm][-1] == .3
                self.assertEqual(max(0, 3 - len(str(round(bpm)))), dim)

    def test_extra_host_score_digit_fits_the_original_fixed_field(self):
        frame = self.numeric_frame()
        field = frame.source20_integer('score', 'score.value', 'score_1p_0', 'fixture.digits', 5, 4, 7)
        size = next(track for track in frame.scene['tracks'] if track['target'] == field['id'] and
                    track['property'] == 'font-size')
        self.assertEqual([0, 10000], [key['time'] for key in size['keyframes']])
        self.assertAlmostEqual(.8, size['keyframes'][1]['value'] / size['keyframes'][0]['value'])

    def test_numeric_anchor_cannot_fall_back_to_another_source(self):
        frame = self.numeric_frame()
        frame.map['layouts'][frame.centre]['draws'][0]['asset']['package'] = 's21/gameparts'
        with self.assertRaisesRegex(ValueError, 'own single gameparts anchor'):
            frame.source20_integer('score', 'score.value', 'score_1p_0', 'fixture.digits', 5, 4, 7)

    def complete_numeric_frame(self):
        frame = Source20Frame()
        centre = frame.map['layouts'][frame.centre]['draws']
        for hook, texture, width, height in (
                ('score_1p_0', 'playm_sco02', 34, 20), ('maxcombo_1p_0', 'playm_sco02', 34, 20),
                ('bpm_0', 'playm_bpm02', 34, 20), ('bpm_min_0', 'playm_bpm_m02', 21, 17),
                ('bpm_max_0', 'playm_bpm_m02', 21, 17),
                ('bpm_min_t', 'playm_bpm_min', 13, 4), ('bpm_max_t', 'playm_bpm_max', 17, 4)):
            centre.append(rectangle('s20/gameparts', texture, hook, width, height))
        for hook, texture, width in (('score_1p', 'playm_score', 19), ('maxcombo_1p', 'playm_maxcombo', 25)):
            centre.append(rectangle(frame.key, texture, hook, width, 5))
        frame.map['layouts'][frame.side]['draws'].append(
            rectangle('s20/gameparts', 'playm_hs_00', 'hs_1p_1', 21, 28, 90, 200))
        for layout in frame.map['layouts'].values():
            for draw in layout['draws']:
                draw['uv'] = [{'x': x, 'y': y, 'z': 0} for x, y in ((0, 0), (1, 0), (0, 1), (1, 1))]
        font = {'id': 'fixture.source-font.normal', 'lineHeight': 20, 'glyphs': [
            {'character': char, 'resource': 'fixture.font2.atlas', 'advance': 12,
             'rect': [i * 12, 0, 12, 20]} for i, char in enumerate('0123456789.') ]}
        frame.owner.feedback_font = {'fonts': {'normal': font['id']}, 'bitmapFonts': [font],
            'resources': [{'id': 'fixture.font2.atlas', 'type': 'texture', 'path': 'font2.png'}], 'files': []}
        frame.owner.source_font = Path('synthetic/own20.ttc')
        frame.owner.copy = lambda *args: None
        return frame

    def test_number_fields_follow_original20_table_and_hide_constant_bpm_extrema(self):
        frame = self.complete_numeric_frame()
        frame.numbers()
        fields = {item['source20Readout']['field']: item['source20Readout'] for item in frame.evidence
                  if 'source20Readout' in item}
        self.assertEqual({'score': (6, 34), 'combo': (4, 34), 'bpm': (3, 34),
                          'min-bpm': (3, 21), 'max-bpm': (3, 21)},
                         {key: (value['count'], value['advance']) for key, value in fields.items()})
        group = next(item for item in frame.info if item['id'].endswith('.bpm-range'))
        self.assertGreater(len(group['children']), 4)
        self.assertEqual(['subtract', 'ceil', 'min'], [item['operation'] for item in frame.scene['numericSources']])
        for minimum, maximum in ((128, 128), (90, 150), (128, 128.01)):
            values = {'timing.bpm-min': minimum, 'timing.bpm-max': maximum}
            for item in frame.scene['numericSources']:
                left = values[item['left']]
                right = values.get(item.get('right'), item.get('right'))
                if item['operation'] == 'subtract':
                    value = left - right
                elif item['operation'] == 'ceil':
                    value = math.ceil(left)
                else:
                    value = min(left, right)
                values['value.' + item['id']] = value
            track = next(track for track in frame.scene['tracks'] if track['target'] == group['id'])
            opacity = [key['value'] for key in track['keyframes'] if key['time'] <= values[track['clock']]][-1]
            self.assertEqual(int(maximum > minimum), opacity)

    def test_hispeed_uses_own_ttf_cells_and_not_the_movie_bitmap_placeholder(self):
        frame = self.complete_numeric_frame()
        frame.source20_hi_speed()
        text, = frame.info
        self.assertEqual('std.font.f20-gmframe0.hispeed', text['properties']['bitmap-font'])
        self.assertEqual(frame.owner.feedback_font['bitmapFonts'][0]['glyphs'],
                         frame.fonts[text['properties']['bitmap-font']]['glyphs'])
        self.assertEqual('fixed-2', text['properties']['format'])
        self.assertEqual('left', text['properties']['alignment'])
        self.assertEqual('#00ffffff', text['properties']['colour'])
        self.assertEqual(90 / 1280, text['properties']['x'])
        self.assertEqual(200 / 720, text['properties']['y'])
        self.assertEqual([], frame.calls)
        frame.owner.feedback_font['bitmapFonts'][0]['glyphs'][0]['advance'] = 11
        with self.assertRaisesRegex(ValueError, 'qualified font2 cells'):
            frame.source20_hi_speed()

    def test_hispeed_and_graph_keep_independent_font_declarations_with_the_same_source_cells(self):
        frame = self.complete_numeric_frame()
        frame.common_family, frame.owner.library = 's20', Path('synthetic/own20-library')
        frame.source20_hi_speed()
        source_font = frame.owner.feedback_font['bitmapFonts'][0]
        exported = {'files': [], 'resources': [], 'bitmapFonts': [source_font], 'receipt': {}}
        with patch('standard_graph.build_graph', return_value=exported):
            frame.graph()
        self.assertEqual({'std.font.f20-gmframe0.hispeed', 'fixture.source-font.normal'}, set(frame.fonts))
        self.assertEqual(frame.fonts['std.font.f20-gmframe0.hispeed']['glyphs'],
                         frame.fonts['fixture.source-font.normal']['glyphs'])


if __name__ == '__main__':
    unittest.main()
