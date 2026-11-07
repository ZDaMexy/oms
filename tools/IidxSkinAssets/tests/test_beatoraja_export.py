import sys
from pathlib import Path
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_export import Composer


class NativeStaticClipTests(unittest.TestCase):
    def make(self):
        sink = SimpleNamespace(dimensions={'source': (4, 4)}, add_source=lambda _: 'source')
        source = SimpleNamespace(scene={}, resources={'art': {'path': 'art.png'}})
        composer = Composer(SimpleNamespace(family='standard', name='fixture'), sink, {}, 'p1', {})
        return composer, source

    def tree(self, inner_x):
        return {'id': 'outer', 'type': 'clip', 'properties': {'x': .1, 'y': .1, 'width': .4, 'height': .4, 'clip-mode': 'bounds'}, 'children': [
            {'id': 'inner', 'type': 'clip', 'properties': {'x': inner_x, 'width': 1, 'clip-mode': 'bounds'}, 'children': [
                {'id': 'art', 'type': 'sprite', 'resource': 'art', 'properties': {'width': 2, 'height': 2}}]}]}

    def test_nested_clips_intersect_without_resizing_source_image(self):
        composer, source = self.make()
        composer.draw_tree(source, self.tree(.5), (0, 0, 100, 100))
        point = composer.skin['destination'][0]['dst'][0]
        self.assertEqual({k: point[k] for k in ('x', 'w', 'h', 'clip_x', 'clip_y', 'clip_w', 'clip_h')},
                         {'x': 30, 'w': 80, 'h': 80, 'clip_x': 30, 'clip_y': 670, 'clip_w': 20, 'clip_h': 40})
        self.assertFalse(composer.gaps)

    def test_empty_intersection_does_not_disable_clip_and_leak_full_image(self):
        composer, source = self.make()
        composer.draw_tree(source, self.tree(2), (0, 0, 100, 100))
        self.assertFalse(composer.skin['destination'])

    def test_text_only_visibility_belongs_to_hud_pass(self):
        composer, source = self.make()
        source.scene = {'bindings': [{'target': 'text-gate', 'property': 'visible', 'source': 'gauge.type'}]}
        composer.draw_tree(source, {'id': 'text-gate', 'type': 'container', 'children': [{'id': 'text', 'type': 'text'}]})
        self.assertFalse(composer.gaps)

    def test_input_visibility_uses_the_real_lane_keyon_clock(self):
        composer, source = self.make()
        source.scene = {'bindings': [{'target': 'beam', 'property': 'visible', 'source': 'input.pressed'}]}
        node = {'id': 'beam', 'type': 'sprite', 'resource': 'art', 'properties': {}}
        for timer in (100, 101, 107, 110, 111, 117):
            composer.draw_tree(source, node, (0, 0, 20, 100), timer=timer)
            gate = composer.skin['destination'][-1]['draw']
            self.assertIn(f'main_state.timer({timer}) ~= main_state.timer_off_value', gate)
        self.assertFalse(composer.gaps)
        for timer in (0, 71, 108, 118):
            before = len(composer.skin['destination'])
            composer.draw_tree(source, node, (0, 0, 20, 100), timer=timer)
            self.assertEqual(len(composer.skin['destination']), before)
        self.assertTrue(any(g.get('feature') == 'unmapped visibility' for g in composer.gaps))

    def test_recorded_beam_keeps_its_native_key_gate_through_atlas_composition(self):
        composer, source = self.make()
        source.scene = {'bindings': [{'target': 'beam', 'property': 'visible', 'source': 'input.pressed'}]}
        source.manifest_path = 'fixture-manifest.json'
        index = {'framesPerSecond': 60, 'sourceFrameCount': 3, 'loop': True, 'loopStartFrame': 0,
                 'canvas': [4, 4], 'pages': [{'id': 'page'}],
                 'frames': [{'sourceFrame': 0, 'runs': [{'page': 'page', 'order': 0, 'blend': 'normal', 'offset': [0, 0], 'rect': [0, 0, 4, 4]}]},
                            {'sourceFrame': 1, 'runs': [{'page': 'page', 'order': 0, 'blend': 'normal', 'offset': [0, 0], 'rect': [0, 0, 4, 4]}]},
                            {'sourceFrame': 2, 'terminal': True, 'runs': []}]}
        composer.sink.add_baked_page = lambda path, page: 'source'
        # Exercise the actual atlas destination creation; replace only lookup
        # of a private source file with this two-frame synthetic input.
        composer.draw_baked = lambda manifest, animation, rect, rgba, **kw: composer.draw_baked_sequence(
            Path('fixture-index.json'), index, rect, rgba, timer=kw['timer'], start_frame=None,
            conditions=kw['conditions'], clip=kw['clip'])
        composer.draw_tree(source, {'id': 'beam', 'type': 'recorded-animation', 'animation': 'beam'},
                           (0, 0, 20, 100), timer=111)
        self.assertEqual(len(composer.skin['destination']), 1)
        self.assertIn('main_state.timer(111) ~= main_state.timer_off_value', composer.skin['destination'][0]['draw'])
        self.assertIn('animation_frame(111,60,2,0,0)', composer.skin['imageset'][0]['value'])
        self.assertFalse(composer.gaps)


if __name__ == '__main__':
    unittest.main()
