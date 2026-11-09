"""Real20 cursor scene shape and independent native Slider/beat carriers."""
from copy import deepcopy
import json
import math
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
from beatoraja_author import Contribution
from beatoraja_beat import apply_beat_opacity
from beatoraja_native import AssetSink, lua
from beatoraja_preparation import preparation_plan
from beatoraja_progress import export_progress
import test_standard20_author as source20_author


class Standard20ProgressTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / '.dev-cache/temp/beatoraja-standard20-progress-tests'
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)

    def fixture(self, side='p1'):
        source, output = self.folder / ('source-' + side), self.folder / ('output-' + side)
        source.mkdir()
        output.mkdir()
        Image.new('RGBA', (66, 72), (220, 140, 40, 180)).save(source / 'own20.png')
        frame = source20_author.Source20Frame(side)
        frame.asset = lambda package, name: ('own20', {'width': 66, 'height': 72})
        frame.source20_progress()
        root = {'id': 'information', 'type': 'container', 'properties': {'opacity': .8}, 'children': frame.info}
        frame.scene.update(templates=[{'id': 'template', 'root': root}],
            instances=[{'template': 'template', 'material': {'slot': 'decoration', 'resource': 'selected'}}])
        mode = '14k' if side == 'dp' else '7k'
        ini = f'[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode={mode} presentation={side}\ndecoration: resource Provide "selected"'
        part = Contribution('frame', 'source20-gmframe0', 'manifest.json', 'scene.json', 'skin.ini',
            {'resources': [{'id': 'own20', 'type': 'texture', 'path': 'own20.png'}]}, frame.scene, ini)
        author = SimpleNamespace(family='standard', contribution=lambda name, options: part)
        return author, part, AssetSink(source, output), mode

    def test_actual_own20_scene_keeps_additive_source_pixels_and_independent_clocks(self):
        for side, positions in (('p1', [2]), ('p2', [1212]), ('dp', [258, 956])):
            with self.subTest(side=side):
                author, frame, sink, mode = self.fixture(side)
                before = deepcopy(frame.scene)
                result = export_progress(author, {}, mode, side, sink, (0, 0, 1280, 720))
                self.assertEqual(positions, [d['dst'][0]['x'] for d in result['destination']])
                self.assertEqual(len(positions), len(result['slider']))
                for slider, destination in zip(result['slider'], result['destination']):
                    self.assertEqual((66, 72, 405, False), (slider['w'], slider['h'], slider['range'], slider['changeable']))
                    self.assertIn('main_state.timer(10021)', slider['value'])
                    self.assertIn('main_state.float_number(6)', slider['value'])
                    self.assertIn('main_state.timer(140)', destination['timer'])
                    self.assertEqual(2, destination['blend'])
                    self.assertEqual([20 * n for n in range(51)], [p['time'] for p in destination['dst']])
                    self.assertTrue(all(p['acc'] == 3 for p in destination['dst']))
                    self.assertEqual([round(.8 * (1 - n / 100 if n < 50 else 1) * 255) for n in range(51)],
                                     [p['a'] for p in destination['dst']])
                    self.assertEqual((638, 66, 72), tuple(destination['dst'][0][k] for k in ('y', 'w', 'h')))
                    self.assertEqual((sink.author_root / 'own20.png').read_bytes(),
                                     (sink.output / sink.source[slider['src']]['path']).read_bytes())
                self.assertEqual(before, frame.scene)
                self.assertTrue(all(gap['informational'] for gap in result['gaps']))
                self.assertIn('trunc(3600/BPM)', next(gap['detail'] for gap in result['gaps']
                                                      if gap['code'] == 'native-beat-opacity-adaptation'))

    def test_unknown_intro_clock_curve_duration_and_beat_policy_refuse_the_route(self):
        author, frame, sink, mode = self.fixture()
        for change in ('clock', 'duration', 'curve', 'alpha'):
            original = deepcopy(frame.scene)
            if change == 'clock':
                frame.scene['tracks'][0]['clock'] = 'unmapped.elapsed'
            elif change == 'duration':
                frame.scene['preparationDuration'] = 1500
            elif change == 'curve':
                frame.scene['tracks'][0]['keyframes'][60]['value'] = 128 / 720
            else:
                frame.scene['tracks'][1]['keyframes'][1]['value'] = .97
            with self.subTest(change=change), self.assertRaises(ValueError):
                export_progress(author, {}, mode, 'p1', sink, (0, 0, 1280, 720))
            frame.scene.clear()
            frame.scene.update(original)

    def test_step_alpha_preserves_the_linear_carrier_fields_and_refuses_competing_clock(self):
        author, frame, sink, _ = self.fixture()
        track = next(t for t in frame.scene['tracks'] if t['clock'] == 'timing.beat')
        destination = {'id': 'cursor', 'blend': 2, 'filter': 0,
                       'dst': [{'time': 0, 'x': 2, 'y': 638, 'w': 66, 'h': 72, 'r': 255, 'g': 255, 'b': 255, 'a': 204}]}
        result = apply_beat_opacity(destination, track, .8)
        self.assertEqual(destination['dst'][0], {k: v for k, v in result['dst'][0].items() if k != 'acc'})
        with self.assertRaisesRegex(ValueError, 'another clock'):
            apply_beat_opacity({**destination, 'timer': 40}, track, .8)

    def test_official_slider_value_step_alpha_and_preparation_clock_at_both_resolutions(self):
        jar = Path(os.environ.get('BEATORAJA_JAR', 'D:/beatoraja/beatoraja.jar'))
        javac, java = shutil.which('javac'), shutil.which('java')
        if not jar.is_file() or not javac or not java:
            self.skipTest('Official beatoraja0.8.9 JAR and JDK required')
        cases = []
        for side in ('p1', 'p2', 'dp'):
            author, frame, sink, mode = self.fixture(side)
            result = export_progress(author, {}, mode, side, sink, (0, 0, 1280, 720))
            cases.append({'name': side, 'skin': {**result, 'w': 1280, 'h': 720,
                          'customTimers': preparation_plan(frame)['customTimers']},
                          'alpha': .8, 'x': [p['dst'][0]['x'] for p in result['destination']]})
        recipe = self.folder / 'progress20.lua'
        recipe.write_text('return ' + lua(cases), encoding='utf-8')
        compiled = subprocess.run([javac, '-encoding', 'UTF-8', '-cp', str(jar), '-d', str(self.folder),
            str(Path(__file__).with_name('Standard20ProgressProbe.java'))],
            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        run = subprocess.run([java, '-cp', str(self.folder) + os.pathsep + str(jar),
            'bms.player.beatoraja.skin.lua.Standard20ProgressProbe', str(recipe)],
            capture_output=True, text=True, encoding='utf-8', errors='replace', timeout=60)
        self.assertEqual(0, run.returncode, run.stdout + run.stderr)
        receipt = json.loads(run.stdout)
        self.assertEqual(3, receipt['cases'])
        self.assertGreater(receipt['geometryChecks'], 900)
        self.assertEqual(receipt['geometryChecks'], receipt['alphaChecks'])
        self.assertEqual(receipt['geometryChecks'], receipt['readOnlyChecks'])
        print('Official source20 progress/step-alpha probe: ' + json.dumps(receipt, sort_keys=True))


if __name__ == '__main__':
    unittest.main()
