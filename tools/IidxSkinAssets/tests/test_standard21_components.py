"""Original21 routing and old-theme preservation, using only synthetic art."""
from copy import deepcopy
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, call, patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard_reference as reference_fixture
import test_standard_pendual_components as component_fixture
import test_standard_pendual_presentation as lane_fixture
from test_standard_reading import SyntheticFrame, walk
from standard_bga import cover_sizes, cover_source_frames
from standard_components import source22_turntable_size
from standard_failure import export_failure
from standard_lane_geometry import source_lane_regions
from standard_presentation_options import lane_roots, partition_clip, pendual_baked_surfaces
from standard_reading import add_reading, export_ready
from standard_source_catalog import (COMPOSITION21, SOURCE21_ALIASES, SOURCE21_STYLES,
                                     SOURCE22_STYLES, frame_id, layout_family, package_key,
                                     retained_styles, styles)

STYLE = SOURCE21_STYLES[0]


def extended_owner():
    value = component_fixture.owner(True)
    value.source_data['sourceComposition'].update(contract=COMPOSITION21, aliases21=SOURCE21_ALIASES)
    value.frame_ids = tuple(frame_id(s) for s in styles(value.source_data))
    return value


def build_turntables(value):
    value.frame_maps = Path('synthetic-frame-maps.json')
    inputs = {str(value.frame_maps): {frame_id(s): frame_id(s) + '.json' for s in retained_styles(value.source_data)}}
    for style in retained_styles(value.source_data):
        identity = frame_id(style)
        normalized = style in (*SOURCE21_STYLES, *SOURCE22_STYLES)
        inputs[identity + '.json'] = {'package': package_key(style), 'common_family': layout_family(style),
            'stage': {'width': 1280, 'height': 720}, 'layouts': {
                movie: {'draws': [component_fixture.turn_draw(binding, normalized) for binding in bindings]}
                for movie, bindings in (('1p_frame', ('turn_1p',)), ('2p_frame', ('turn_2p',)),
                                        ('dp_frame_1p', ('turn_1p', 'turn_2p'))) }}
    with patch('standard_components.read_json', side_effect=lambda p: inputs[str(p)]), \
            patch('standard_components.within', side_effect=lambda root, relative: Path(relative)):
        value.turntables()


class Standard21ReferenceTests(unittest.TestCase):
    def test_unresolved_dynamic_effect_keeps_both_own_candidates_without_borrowing(self):
        with tempfile.TemporaryDirectory() as directory:
            rows = [reference_fixture.texture('s21/common', 'playm_ef_rev3'),
                    reference_fixture.texture('s21/gameparts', 'playm_ef_rev3', 'b' * 64),
                    reference_fixture.texture('s22/common', 'playm_ef_rev3', 'c' * 64),
                    reference_fixture.texture('s22/gameparts', 'later_only')]
            research = reference_fixture.StandardReferenceTest().research(Path(directory), rows, family='s21')
            result = research.resolve('playm_ef_rev3', package_key(STYLE))
            self.assertTrue(result['missing'])
            self.assertTrue(result['ambiguous'])
            self.assertNotIn('asset', result)
            self.assertEqual({'s21/common', 's21/gameparts'}, {c['package'] for c in result['candidates']})
            self.assertTrue(research.resolve('later_only', package_key(STYLE))['missing'])
            with self.assertRaises(ValueError):
                research.resolve('playm_ef_rev3', 'source22/gmframe0')

    def test_no_other_conflict_gains_the_effect_exception(self):
        with tempfile.TemporaryDirectory() as directory:
            rows = [reference_fixture.texture('s21/common', 'other'),
                    reference_fixture.texture('s21/gameparts', 'other', 'b' * 64)]
            research = reference_fixture.StandardReferenceTest().research(Path(directory), rows, family='s21')
            with self.assertRaises(ValueError):
                research.resolve('other', package_key(STYLE))


class Standard21GeometryTests(unittest.TestCase):
    def test_each_own_lane_is_partitioned_without_changing_draw_order_state_or_uv(self):
        for layout, movie, lefts in (('p1', '1p_frame', [48]), ('p2', '2p_frame', [940]),
                                     ('dp', 'center_frame_dp_1p', [304, 684])):
            with self.subTest(layout=layout):
                source, source_layout, clip, _ = lane_fixture.fixture(STYLE, movie)
                original_source, original_clip = deepcopy(source), deepcopy(clip)
                reference = {'package': package_key(STYLE), 'common_family': 's21', 'layouts': {movie: source_layout}}
                self.assertEqual([{'left': x, 'right': x + 292, 'top': 0, 'bottom': 484} for x in lefts],
                                 source_lane_regions(reference, STYLE, layout))
                self.assertFalse(pendual_baked_surfaces(source, source_layout, STYLE))
                roots = lane_roots(source, source_layout, style=STYLE)
                layers = partition_clip(source, clip, roots, range(len(clip['frames'])))
                self.assertEqual(len(lefts), sum(layer['kind'] == 'lane' for layer in layers))
                for fi, frame in enumerate(clip['frames']):
                    expected = [n for n, _ in frame['nodes'] if clip['nodes'][n]['geometry'] >= 0]
                    actual, original_chains = [], lane_fixture.chains(clip, fi)
                    for layer in layers:
                        value = layer['clip']
                        self.assertEqual(frame['camera'], value['frames'][fi]['camera'])
                        chains = lane_fixture.chains(value, fi)
                        for n, _ in value['frames'][fi]['nodes']:
                            old = layer['originalNodeIds'][n]
                            self.assertEqual(original_chains[old], chains[n])
                            if value['nodes'][n]['geometry'] >= 0:
                                actual.append(old)
                                self.assertEqual(clip['geometries'][clip['nodes'][old]['geometry']],
                                                 value['geometries'][value['nodes'][n]['geometry']])
                        for field in ('fps', 'width', 'height', 'loop', 'loopStartFrame', 'trigger'):
                            self.assertEqual(clip[field], value[field])
                    self.assertEqual(expected, actual)
                self.assertEqual(original_source, source)
                self.assertEqual(original_clip, clip)
                reference['common_family'] = 's22'
                with self.assertRaises(ValueError):
                    source_lane_regions(reference, STYLE, layout)

    def test_turntable_uses_own_normalized_geometry_and_rejects_other_source(self):
        source = {'package': package_key(STYLE), 'common_family': 's21'}
        draw = component_fixture.turn_draw('turn_1p', True)
        self.assertEqual((25, 25), source22_turntable_size(STYLE, source, draw))
        for field, replacement in (('common_family', 's22'), ('package', 'source22/gmframe0')):
            with self.subTest(field=field), self.assertRaises(ValueError):
                source22_turntable_size(STYLE, {**source, field: replacement}, draw)
        draw['uv'][3]['x'] = .9
        with self.assertRaises(ValueError):
            source22_turntable_size(STYLE, source, draw)

    def test_turntable_adds_only_four_own_routes_and_preserves_all_old17_nodes(self):
        old, new = component_fixture.owner(True), extended_owner()
        build_turntables(old)
        build_turntables(new)
        for before, after in zip(old.modules[0]['choices'], new.modules[0]['choices']):
            first, extended = old.documents[before['scene']], new.documents[after['scene']]
            nodes = {n['id']: n for t in extended['templates'] for n in component_fixture.walk(t['root'])}
            for template in first['templates']:
                for branch in template['root']['children']:
                    self.assertEqual(branch, nodes[branch['id']])
            tracks = {t['id']: t for t in extended['tracks']}
            for track in first['tracks']:
                self.assertEqual(track, tracks[track['id']])
            added = [t for t in extended['tracks'] if STYLE in t['target'] and t['property'] == 'rotation']
            self.assertEqual(4, len(added))
            for track in added:
                art = nodes[track['target']]
                self.assertEqual((25 / 1280, 25 / 720), (art['properties']['width'], art['properties']['height']))
                self.assertEqual('input.motion-revolutions-2' if '.dp.' in track['target'] and track['target'].endswith('.n1')
                                 else 'input.motion-revolutions-1', track['clock'])

    def test_existing_custom_choices_are_not_duplicated_for_identical21_archives(self):
        for method in ('static_lanes', 'lift_art', 'charge_notes'):
            with self.subTest(method=method):
                old, new = component_fixture.owner(True), extended_owner()
                getattr(old, method)()
                getattr(new, method)()
                self.assertEqual(old.options, new.options)
                self.assertEqual(old.modules, new.modules)
                self.assertEqual(old.documents, new.documents)
                self.assertEqual(old.calls, new.calls)


class Standard21ReadingEventTests(unittest.TestCase):
    def test_start_panel_uses_only21_art_and_current_green_for_each_layout(self):
        with tempfile.TemporaryDirectory() as directory:
            for layout in ('p1', 'p2', 'dp'):
                frame = SyntheticFrame(Path(directory), layout)
                frame.style = STYLE
                source_asset, packages = frame.asset, []
                def asset(package, name):
                    packages.append(package)
                    return source_asset(package, name)
                frame.asset = asset
                ready = {layout: {'resources': [], 'animations': [], 'node': {'id': 'own21.ready'}, 'source': {'family': 's21'}}}
                with patch('standard_feedback.rasterize_source_text', return_value=Image.new('RGBA', (84, 20))):
                    add_reading(frame, ready, {'fonts': {'normal': 'host.font2', 'small': 'host.font5'}}, Path(directory) / 'font')
                self.assertEqual({'s21/gameparts'}, set(packages))
                bindings = {b['source'] for b in frame.scene['bindings']}
                self.assertIn('scroll.green-number', bindings)
                self.assertFalse(any(s.startswith('scroll.green-number-') for s in bindings))
                self.assertFalse(any(n['id'].endswith('.green.range') for n in walk(frame.info)))
                self.assertFalse(any('start_notes_' in s or 'playm_spd_pv_' in s for s in frame.requested))
                reading = frame.evidence[-1]['reading']
                self.assertEqual('s21/gameparts spd/spd2 p1/p2/dp, stable frame16', reading['source'])
                self.assertFalse(reading['sourceCapabilities']['greenRange'])
                self.assertIn('OMS author adapters', reading['nativeReferenceBoundary'])
                self.assertEqual('preparation.ready-elapsed', next(n for n in walk(frame.info) if n['id'].endswith('.ready.art'))['clock'])

    def test_ready_has_three_own_full90frame_nonlooping_programs(self):
        source = {'stage': [0, 0, 1280, 720], 'fps': 60, 'sourceFrames': 90, 'labels': {}, 'translatedRootControl': []}
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / 'library.json').write_text('{}', 'utf-8')
            exporter = SimpleNamespace(source_data={}, files={}, receipts=[], clip=Mock(return_value={'source': source}))
            with patch('standard_components.StandardComponents', return_value=exporter), patch('standard_reading.source_styles', return_value=(26, STYLE)):
                result = export_ready(root, root, root, root)
                self.assertEqual({'0', 's21'}, set(result['families']))
                own = [c for c in exporter.clip.call_args_list if c.args[0] == 's21/gameparts']
                self.assertEqual(3, len(own))
                self.assertTrue(all(c.kwargs['loop'] is False for c in own))
                for change in ({'sourceFrames': 89}, {'fps': 59}, {'stage': [0, 0, 280, 720]}, {'labels': {'loop': 0}}):
                    exporter.clip.return_value = {'source': {**source, **change}}
                    with self.assertRaises(ValueError):
                        export_ready(root, root, root, root)

    def test_failure_is_own_art_with_actual_failure_gate_not_inferred_native_exit(self):
        source = {'movie': 'miss', 'stage': [0, 0, 1280, 720], 'fps': 60,
                  'sourceFrames': 180, 'labels': {}, 'translatedRootControl': []}
        exporter = SimpleNamespace(source_library_sha='fixture', source_families=('s21',), files={},
                                   clip=Mock(return_value={'source': source}))
        with tempfile.TemporaryDirectory() as directory, patch('standard_failure.StandardComponents', return_value=exporter):
            root = Path(directory)
            result = export_failure(root, root, root, root)
        self.assertEqual(call('s21/gameparts', 'miss', 'sf.failed.s21', loop=False, trigger='gameplay.fail'), exporter.clip.call_args)
        receipt = result['families']['s21']['receipt']
        self.assertTrue(receipt['source'].startswith('21 HDD/'))
        self.assertIn('host adaptation', receipt['nativeBoundary'])
        self.assertNotIn('nativeExit', receipt)

    def test_bga_qualifies_own_cover_dimensions_and_original_frame_count(self):
        self.assertEqual({'anime_cover': (800, 586), 'anime_cover2': (938, 720)}, cover_sizes('s21'))
        self.assertEqual(300, cover_source_frames(STYLE))
        self.assertEqual(420, cover_source_frames(21))
        self.assertNotEqual(cover_sizes('s21'), cover_sizes('0'))


if __name__ == '__main__':
    unittest.main()
