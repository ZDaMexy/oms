"""Public author transformations, without original game media or a player installation."""

import copy
import tempfile
from pathlib import Path
import unittest

from build_lighting_details import (bga_ini, compact_added_textures, compact_intro_keyframe_ids,
    compact_frame_resource_ids, compact_scene_identifiers, select_frame_layouts, select_turntable_frame, share_turntable_geometry, deduplicate_inis)
import lighting_motion
from test_lighting_motion import turntables


class LightingDetailsBuilderTest(unittest.TestCase):
    def test_identical_ini_variants_share_bytes_without_merging_different_layouts(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            (output / 'config').mkdir()
            for name, data in [('a.ini', b'original\r\n'), ('b.ini', b'original\r\n'), ('c.ini', b'original\n')]:
                (output / name).write_bytes(data)
            choice = {'ini': 'a.ini', 'variants': [{'ini': 'b.ini'}, {'ini': 'c.ini'}]}
            deduplicate_inis(output, {'modules': [{'choices': [choice]}]})
            self.assertEqual(choice['ini'], choice['variants'][0]['ini'])
            self.assertNotEqual(choice['ini'], choice['variants'][1]['ini'])
            self.assertEqual(b'original\r\n', (output / choice['ini']).read_bytes())
            self.assertEqual(b'original\n', (output / choice['variants'][1]['ini']).read_bytes())

    def test_styles_share_geometry_but_keep_their_own_original_texture(self):
        scenes, manifests = [], []
        for style in ('t00', 't01'):
            identifier = 't.' + style + '.base.p1'
            scene = {'root': {'id': identifier, 'resource': 'original.' + style},
                     'tracks': [{'id': identifier + '.motor', 'target': identifier}]}
            manifest = {'resources': [{'id': 'original.' + style, 'path': style + '.png', 'filter': 'nearest'}]}
            before = copy.deepcopy((scene, manifest))
            shared, texture = share_turntable_geometry(scene, manifest, style)
            self.assertEqual(before, (scene, manifest))
            self.assertEqual(style + '.png', texture['resources'][0]['path'])
            self.assertEqual(texture['resources'][0]['id'], shared['root']['resource'])
            self.assertEqual(shared['root']['id'], shared['tracks'][0]['target'])
            scenes.append(shared)
            manifests.append(texture)
        self.assertEqual(scenes[0], scenes[1])
        self.assertNotEqual(manifests[0], manifests[1])

    def test_new_texture_names_preserve_bytes_filter_and_all_program_references(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            (output / 'original.png').write_bytes(b'original PNG bytes')
            manifest = {'resources': [{'id': 'omni.lighting.counter0', 'type': 'texture', 'path': 'original.png', 'filter': 'nearest'},
                                      {'id': 'old.resource', 'type': 'texture', 'path': 'original.png', 'filter': 'linear'}],
                        'bitmapFonts': [{'glyphs': [{'resource': 'omni.lighting.counter0'}]}]}
            scene = {'root': {'resource': 'omni.lighting.counter0'}, 'templates': [], 'tracks': [], 'variants': []}
            old_resource = copy.deepcopy(manifest['resources'][1])
            receipts = compact_added_textures(output, manifest, scene)
            added = manifest['resources'][0]
            self.assertEqual(b'original PNG bytes', (output / added['path']).read_bytes())
            self.assertEqual('nearest', added['filter'])
            self.assertEqual(added['id'], scene['root']['resource'])
            self.assertEqual(added['id'], manifest['bitmapFonts'][0]['glyphs'][0]['resource'])
            self.assertEqual(old_resource, manifest['resources'][1])
            self.assertTrue(receipts[0]['originalPngBytesPreserved'])
            self.assertTrue(all(segment[0].isalpha() for segment in added['id'].split('.')))

    def test_frame_keeps_exact_coordinates_for_every_applicable_keymode(self):
        original = {'layouts': [
            {'id': f'{mode}.{side}', 'keymode': mode, 'presentation': side, 'stages': [{'rect': [0.1, 0, 0.2, 0.7]}]}
            for mode in ('5k', '6k', '7k', '8k') for side in ('p1', 'p2')
        ] + [{'id': mode, 'keymode': mode, 'presentation': 'any', 'stages': [{}, {}]} for mode in ('14k', '7k-7k', '8k-8k')]}
        for side in ('p1', 'p2', 'dp'):
            manifest = copy.deepcopy(original)
            select_frame_layouts(manifest, side)
            self.assertEqual(3 if side == 'dp' else 4, len(manifest['layouts']))
            self.assertTrue(all(declaration in original['layouts'] for declaration in manifest['layouts']))

    def test_scene_compaction_rewrites_all_local_links_without_changing_source_samples(self):
        document = {'root': {'id': 'very.long.root', 'children': []}, 'templates': [
            {'id': 'very.long.template', 'root': {'id': 'very.long.child', 'children': [], 'resource': 'unchanged.texture'}}],
            'instances': [{'id': 'very.long.instance', 'template': 'very.long.template'}],
            'tracks': [{'id': 'very.long.track', 'target': 'very.long.child', 'clock': 'preparation.elapsed',
                        'keyframes': [{'id': 'very.long.sample', 'time': 500, 'value': [0.123456789, 0.5, 1]}]}]}
        samples = copy.deepcopy(document['tracks'][0]['keyframes'][0]['value'])
        compact_scene_identifiers(document)
        self.assertEqual(document['templates'][0]['id'], document['instances'][0]['template'])
        self.assertEqual(document['templates'][0]['root']['id'], document['tracks'][0]['target'])
        self.assertEqual('unchanged.texture', document['templates'][0]['root']['resource'])
        self.assertEqual('preparation.elapsed', document['tracks'][0]['clock'])
        self.assertEqual(samples, document['tracks'][0]['keyframes'][0]['value'])

    def test_new_recorded_clips_follow_compacted_texture_ids_without_losing_frames(self):
        import brotli
        import json
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            (output / 'source.png').write_bytes(b'original pixels')
            clip = {'geometries': [{'resource': 'omni.intro.source', 'vertices': [[1, 2, 0, 0.5, 0.6]]}],
                    'frames': [{'nodes': [[0, 0]]}, {'nodes': []}], 'states': [{'multiply': [1, 1, 1, 0.4]}]}
            (output / 'clip.json.br').write_bytes(brotli.compress(json.dumps(clip).encode()))
            manifest = {'resources': [{'id': 'omni.intro.source', 'type': 'texture', 'path': 'source.png', 'filter': 'nearest'}],
                        'animations': [{'id': 'animation', 'path': 'clip.json.br', 'resources': ['omni.intro.source']}]}
            scene = {'root': {}, 'templates': [], 'tracks': [], 'variants': []}
            compact_added_textures(output, manifest, scene)
            animation = manifest['animations'][0]
            actual = json.loads(brotli.decompress((output / animation['path']).read_bytes()))
            self.assertEqual(manifest['resources'][0]['id'], actual['geometries'][0]['resource'])
            self.assertEqual(animation['resources'], [actual['geometries'][0]['resource']])
            self.assertEqual(clip['frames'], actual['frames'])
            self.assertEqual(clip['states'], actual['states'])
            self.assertEqual(clip['geometries'][0]['vertices'], actual['geometries'][0]['vertices'])

    def test_frame_resource_names_remain_shared_and_keep_external_module_ids(self):
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            identities = {}
            for ignored in range(2):
                manifest = {'resources': [{'id': 'frame.original-long-name', 'path': 'unchanged.png'},
                                          {'id': 'shared.original', 'path': 'shared.png'}],
                            'bitmapFonts': [{'id': 'font', 'glyphs': [{'resource': 'frame.original-long-name'}]}]}
                scene = {'root': {'resource': 'frame.original-long-name'}, 'source': 'preparation.elapsed'}
                compact_frame_resource_ids(output, scene, manifest, identities, {'shared.original'})
                self.assertEqual('fr0', scene['root']['resource'])
                self.assertEqual('fr0', manifest['bitmapFonts'][0]['glyphs'][0]['resource'])
                self.assertEqual('unchanged.png', manifest['resources'][0]['path'])
                self.assertEqual('shared.original', manifest['resources'][1]['id'])
                self.assertEqual('preparation.elapsed', scene['source'])
            self.assertEqual({'frame.original-long-name': 'fr0'}, identities)

    def test_compact_intro_identity_keeps_every_original_time_and_quad_sample(self):
        scene = {'tracks': [{'id': 'li.iidx33.dp.q20', 'clock': 'timing.elapsed', 'keyframes': [
            {'id': 'li.iidx33.dp.q20.k0', 'time': 0, 'value': {'vertices': [1, 2, 3], 'opacity': 0}},
            {'id': 'li.iidx33.dp.q20.k1', 'time': 16.667, 'value': {'vertices': [4, 5, 6], 'opacity': 1}},
        ]}, {'id': 'other.motor', 'clock': 'timing.time', 'keyframes': [{'id': 'original', 'time': 0, 'value': 20}]}]}
        before = copy.deepcopy(scene)
        compact_intro_keyframe_ids(scene)
        self.assertEqual(before['tracks'][1], scene['tracks'][1])
        for previous, current in zip(before['tracks'][0]['keyframes'], scene['tracks'][0]['keyframes']):
            self.assertEqual(previous['time'], current['time'])
            self.assertEqual(previous['value'], current['value'])
            self.assertLess(len(current['id']), len(previous['id']))
            self.assertTrue(all(segment[0].isalpha() for segment in current['id'].split('.')))

    def test_graph_reserves_its_column_without_changing_gameplay_materials(self):
        materials = ('[GameplaySkin.Common:1]\nTarget: Lane ruleset=bms keymode=7k stage-mode=single\n'
                     'note.head: resource Provide "notes/white"\n')
        original = ('[Bms]\nKeymode: 7K\nNormalLaneWidth: 1\n'
                    'BgaViewports: 0.3,0.1,0.4,0.7,fit\n'
                    'BgaViewportsP1: 0.355208333333,0.0925925925926,0.5703125,0.760416666667,fit\n'
                    'BgaViewportsP2: 0.0739583333333,0.0925925925926,0.5703125,0.760416666667,fit\n' + materials)
        self.assertEqual(original, bga_ini(original, graph=False))
        changed = bga_ini(original, graph=True)
        self.assertTrue(changed.endswith(materials))
        for line in changed.splitlines():
            if line.startswith('BgaViewportsP'):
                values = list(map(float, line.split(':', 1)[1].strip().split(',')[:4]))
                if line.startswith('BgaViewportsP1'):
                    self.assertLessEqual(values[0] + values[2], 1498 / 1920 + 1e-10)
                else:
                    self.assertGreaterEqual(values[0], 422 / 1920 - 1e-10)

    def test_dp_window_repair_respects_native_hud_and_graph_leaves_left_videos(self):
        original = '[Bms]\nKeymode: 14K\nBgaViewports: 0.01,0.7,0.18,0.23,fit; 0.8,0.3,0.18,0.23,fit\n'
        repaired = bga_ini(original, graph=False)
        self.assertIn('0.8,0.3,0.18,0.23,fit', repaired)
        first = repaired.split(':', 2)[-1].strip().split(';')[0].split(',')
        self.assertLess(float(first[1]) + float(first[3]), 0.92)
        graph = bga_ini(original, graph=True)
        self.assertNotIn('0.8,0.3', graph)
        self.assertNotIn(';', graph)

    def test_each_selected_frame_runs_only_its_own_motor_tracks(self):
        source = turntables()
        source.update({'bindings': [], 'variants': [], 'instances': []})
        lighting_motion.inject_turntables(source)
        before = copy.deepcopy(source)
        for frame in lighting_motion.FRAMES:
            selected = select_turntable_frame(source, frame)
            knobs = [node for node in lighting_motion._nodes(selected) if lighting_motion._TURN.fullmatch(node['id'])]
            self.assertEqual(4, len(knobs))
            motors = [track for track in selected['tracks'] if track.get('clock')]
            self.assertEqual(12, len(motors))
            self.assertTrue(all('.' + frame + '.' in track['target'] for track in motors))
            self.assertEqual(2, len([track for track in selected['tracks'] if track['property'] == 'projective-quad']))
        self.assertEqual(before, source)


if __name__ == '__main__':
    unittest.main()
