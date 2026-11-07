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


if __name__ == '__main__':
    unittest.main()
