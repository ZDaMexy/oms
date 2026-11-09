"""Source20 MIN/MAX gate from the actual synthetic author, without private art."""
from copy import deepcopy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import Contribution, walk_nodes
from beatoraja_export import Composer
from beatoraja_hud import _Sources, export_hud
from beatoraja_native import AssetSink, binary_numeric_opacity_condition, lua
import test_standard20_author as source20_author


class Standard20NativeNumericGateTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / '.dev-cache/temp/beatoraja-standard20-numeric-tests'
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        self.source, self.output = self.folder / 'source', self.folder / 'output'
        self.source.mkdir()
        self.output.mkdir()
        self.sink = AssetSink(self.source, self.output)

    def fixture(self):
        frame = source20_author.Standard20AuthorTests().complete_numeric_frame()
        frame.numbers()
        group = next(node for node in frame.info if node['id'].endswith('.bpm-range'))
        font_ids = {node.get('properties', {}).get('bitmap-font') for node in walk_nodes(group)} - {None}
        fonts = [frame.fonts[name] for name in font_ids]
        dimensions = {}
        for font in fonts:
            for glyph in font['glyphs']:
                dimensions[glyph['resource']] = (glyph['advance'], font['lineHeight'])
        dimensions.update({node['resource']: (13 if node['id'].endswith('bpm_min_t') else 17, 4)
                           for node in walk_nodes(group) if node['type'] == 'sprite'})
        resources = []
        for index, (identifier, size) in enumerate(dimensions.items()):
            relative = f'synthetic-{index}.png'
            Image.new('RGBA', size, (index, 255 - index, 80, 255)).save(self.source / relative)
            resources.append({'id': identifier, 'type': 'texture', 'path': relative})
        scene = {**frame.scene, 'root': group}
        contribution = Contribution('frame', 'source20-gmframe0', None, None, None,
            {'resources': resources, 'bitmapFonts': fonts}, scene, '')
        return contribution, group

    def compose(self, contribution, group):
        hud = export_hud(contribution, group, (0, 0, 1280, 720), self.sink, canvas_height=720)
        composer = Composer(SimpleNamespace(family='standard', name='synthetic20', root=self.source),
                            self.sink, {}, 'p1', {})
        composer.draw_tree(contribution, group)
        return hud, composer

    def test_actual_author_group_exports_both_labels_and_original_bitmap_numbers(self):
        contribution, group = self.fixture()
        hud, composer = self.compose(contribution, group)
        self.assertEqual(2, len(composer.skin['image']))
        self.assertEqual(2, len(composer.skin['destination']))
        self.assertEqual(6, len(hud['text']))
        self.assertEqual(2, len([text for text in hud['text'] if 'value' in text]))
        for result in (hud['destination'], composer.skin['destination']):
            self.assertTrue(all('math.ceil(' in row['draw'] and 'math.min(' in row['draw'] for row in result))
        self.assertEqual(['native-bpm-extrema-integer-adaptation'], [g['code'] for g in hud['gaps']])
        self.assertEqual(['native-bpm-extrema-integer-adaptation'], [g['feature'] for g in composer.gaps])
        self.assertTrue(all(g['informational'] for g in [*hud['gaps'], *composer.gaps]))

    def test_unknown_source_operation_or_cycle_cannot_reveal_labels_or_values(self):
        contribution, group = self.fixture()
        for mutation in ('source', 'operation', 'cycle'):
            with self.subTest(mutation=mutation):
                scene = deepcopy(contribution.scene)
                if mutation == 'source':
                    scene['numericSources'][0]['left'] = 'timing.nonexistent'
                elif mutation == 'operation':
                    scene['numericSources'][1]['operation'] = 'unknown'
                else:
                    scene['numericSources'][0]['left'] = 'value.' + scene['numericSources'][-1]['id']
                changed = Contribution(contribution.module, contribution.choice, None, None, None,
                                       contribution.manifest, scene, '')
                if mutation == 'cycle':
                    with self.assertRaisesRegex(ValueError, 'Cyclic author numeric source'):
                        self.compose(changed, group)
                else:
                    hud, composer = self.compose(changed, group)
                    self.assertFalse(hud['text'])
                    self.assertFalse(composer.skin['destination'])
                    self.assertEqual('unmapped-text-track', hud['gaps'][0]['reason'])
                    self.assertEqual('unmapped numeric opacity', composer.gaps[0]['feature'])

    def test_art_route_rejects_arbitrary_fades_and_noncontainer_numeric_tracks(self):
        contribution, group = self.fixture()
        gate = next(t for t in contribution.scene['tracks'] if t['target'] == group['id'])
        for change in ({'easing': 'linear'}, {'loop': True},
                       {'keyframes': [{'time': 0, 'value': 0}, {'time': 1, 'value': .3}]}):
            with self.subTest(change=change):
                with self.assertRaisesRegex(ValueError, 'original nonlooping0/1 step gate'):
                    binary_numeric_opacity_condition({**gate, **change}, ('1', 'number'))
        scene = deepcopy(contribution.scene)
        scene['tracks'] = [{**gate, 'target': group['children'][-1]['id']}]
        changed = Contribution(contribution.module, contribution.choice, None, None, None,
                               contribution.manifest, scene, '')
        _, composer = self.compose(changed, {**group, 'properties': {'opacity': 1},
                                             'children': [group['children'][-1]]})
        self.assertFalse(composer.skin['destination'])
        self.assertIn('requires its source container', composer.gaps[0]['detail'])

    def test_ceil_and_min_keep_fractional_context_and_do_not_read_unary_right(self):
        contribution, group = self.fixture()
        gate = next(t for t in contribution.scene['tracks'] if t['target'] == group['id'])
        sources = _Sources([contribution], {'timing.bpm-min': 128, 'timing.bpm-max': 128.01})
        expression = sources.expression(gate['clock'])
        self.assertEqual(('math.min(math.ceil((128.01 - 128)), 1)', 'number'), expression)
        self.assertIn('>= 1', binary_numeric_opacity_condition(gate, expression))
        with self.assertRaisesRegex(ValueError, 'unmapped numeric source or operation'):
            binary_numeric_opacity_condition(gate, None)

    def test_official_jar_callbacks_gate_labels_and_numbers_and_expose_integer_boundary(self):
        jar = Path(os.environ.get('BEATORAJA_JAR', 'D:/beatoraja/beatoraja.jar'))
        javac, java = shutil.which('javac'), shutil.which('java')
        if not jar.is_file() or not javac or not java:
            self.skipTest('Official beatoraja0.8.9 JAR and JDK are required')
        contribution, group = self.fixture()
        hud, composer = self.compose(contribution, group)
        recipe = self.output / 'numeric-gate.lua'
        recipe.write_text('return ' + lua({'hud': hud, 'art': composer.skin}), encoding='utf-8')
        probe = Path(__file__).with_name('HudNumericGateProbe.java')
        compiled = subprocess.run([javac, '-encoding', 'UTF-8', '-cp', str(jar), '-d', str(self.folder), str(probe)],
            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        run = subprocess.run([java, '-cp', str(self.folder) + os.pathsep + str(jar),
            'bms.player.beatoraja.skin.lua.HudNumericGateProbe', str(recipe)],
            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60)
        self.assertEqual(0, run.returncode, run.stdout + run.stderr)
        result = json.loads(run.stdout)
        self.assertEqual(4, result['models'])
        self.assertEqual(32, result['gateChecks'])
        self.assertEqual(4, result['fractionalBoundaryChecks'])
        print('Official source20 numeric gate probe: ' + json.dumps(result, sort_keys=True))


if __name__ == '__main__':
    unittest.main()
