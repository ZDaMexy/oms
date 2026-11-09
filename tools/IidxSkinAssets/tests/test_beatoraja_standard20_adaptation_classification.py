"""Keep qualified20 limits visible without dropping their actual native shards."""
from copy import deepcopy
import json
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import beatoraja_package as package
from beatoraja_author import Contribution
import test_beatoraja_standard20_numeric_gate as numeric
import test_beatoraja_standard20_progress as progress


class Standard20AdaptationClassificationTests(unittest.TestCase):
    def test_only_exact_source20_bpm_nodes_and_actual_notice_forms_are_nonblocking(self):
        for layout in ('p1', 'p2', 'dp', 'p1.wide', 'p2.wide'):
            for identifier in ('code', 'feature'):
                gap = {identifier: 'native-bpm-extrema-integer-adaptation', 'informational': True,
                       'node': 'std.f20-gmframe0.' + layout + '.bpm-range'}
                for stage in ('hud', 'decorations'):
                    self.assertEqual([], package.blocking_gaps([gap], stage))
        intro = {'code': 'native-progress-intro-adaptation', 'informational': True}
        self.assertEqual([], package.blocking_gaps([intro], 'decorations'))
        self.assertEqual([intro], package.blocking_gaps([intro], 'hud'))

    def test_unknown_false_missing_or_nonboolean_notices_still_block(self):
        for kind in ('native-bpm-extrema-integer-adaptation', 'native-progress-intro-adaptation'):
            for flag in (None, False, 1, 'true'):
                gap = {'code': kind, 'node': 'std.f20-gmframe0.p1.bpm-range'}
                if flag is not None:
                    gap['informational'] = flag
                self.assertEqual([gap], package.blocking_gaps([gap], 'decorations'))
        unknown = {'code': 'unknown-source20-adaptation', 'informational': True}
        missing = {'feature': 'missing source artwork', 'informational': True}
        self.assertEqual([unknown, missing], package.blocking_gaps([unknown, missing], 'decorations'))

    def test_wrong_family_node_or_stage_does_not_borrow_source20_qualification(self):
        gap = {'code': 'native-bpm-extrema-integer-adaptation', 'informational': True,
               'node': 'std.f21-gmframe0.p1.bpm-range'}
        self.assertEqual([gap], package.blocking_gaps([gap], 'hud'))
        gap['node'] = 'std.f20-gmframe0.p1.bpm-range'
        self.assertEqual([gap], package.blocking_gaps([gap], 'frame'))
        gap['code'] = 'unknown-primary-diagnostic'
        gap['feature'] = 'native-bpm-extrema-integer-adaptation'
        self.assertEqual([gap], package.blocking_gaps([gap], 'decorations'))

    def test_real_export_writes_hud_and_progress_factories_into_native_selection(self):
        # Use the actual author scene shapes and adapters. Only unrelated Graph,
        # reading/gauge ownership and note-cap scanning are outside this fixture.
        numbers = numeric.Standard20NativeNumericGateTests()
        numbers.setUp()
        self.addCleanup(numbers.doCleanups)
        cursor = progress.Standard20ProgressTests()
        cursor.setUp()
        self.addCleanup(cursor.doCleanups)
        number_part, bpm_group = numbers.fixture()
        _, progress_part, progress_sink, _ = cursor.fixture()
        source = numbers.source
        (source / 'own20.png').write_bytes((progress_sink.author_root / 'own20.png').read_bytes())
        manifest = {'contentCatalog': 'catalog.json', 'options': [
            {'id': 'graph-visible', 'type': 'toggle', 'default': True}]}
        (source / 'gameplay-skin.json').write_text(json.dumps(manifest), encoding='utf-8')
        (source / 'catalog.json').write_text('{}', encoding='utf-8')
        scene = deepcopy(number_part.scene)
        scene['root'] = {'id': 'empty-root', 'type': 'container', 'properties': {}}
        scene['tracks'].extend(deepcopy(progress_part.scene['tracks']))
        scene['bindings'].extend(deepcopy(progress_part.scene['bindings']))
        scene['preparationDuration'] = 2000
        scene['templates'] = deepcopy(progress_part.scene['templates'])
        scene['templates'][0]['root']['children'].insert(0, bpm_group)
        scene['instances'] = deepcopy(progress_part.scene['instances'])
        art = {**number_part.manifest,
               'resources': [*number_part.manifest['resources'], *progress_part.manifest['resources']],
               'layouts': [{'ruleset': 'bms', 'keymode': '7k', 'presentation': 'p1', 'stages': []}]}
        # The reusable scene fixture intentionally uses "fixture.p1" IDs; this
        # package gate must exercise the actual own20-qualified node identity.
        scene = json.loads(json.dumps(scene).replace('fixture.p1', 'std.f20-gmframe0.p1'))
        art = json.loads(json.dumps(art).replace('fixture.p1', 'std.f20-gmframe0.p1'))
        part = Contribution('frame', 'source20-gmframe0', 'frame-manifest.json', 'frame-scene.json', 'skin.ini',
                            art, scene, progress_part.ini)
        base = Contribution('base', None, None, None, None, {},
                            {'root': {'id': 'base-empty', 'type': 'container', 'properties': {}}}, '')
        author = SimpleNamespace(root=source, family='standard', name='Synthetic source20 classification',
            manifest=manifest, modules={'frame': {'default': 'source20-gmframe0',
                'choices': [{'value': 'source20-gmframe0', 'label': '20 synthetic'}]}},
            options=lambda keymode, side, values: {'graph-visible': True, **values},
            contribution=lambda module, options: part, base=lambda: base)
        empty_reading = {'handled_root_ids': set(), 'handled_node_ids': set()}
        empty_feedback = {'handled_node_ids': set(), 'draws': []}
        output = numbers.folder / 'native-package'
        with patch.object(package, 'STAGES', ('decorations', 'hud')), \
                patch.object(package, 'scan_cap_height', return_value=24), \
                patch.object(package.Composer, 'graph', return_value=None), \
                patch('beatoraja_reading.reading_ownership', return_value=empty_reading), \
                patch('beatoraja_feedback.feedback_plan', return_value=empty_feedback):
            receipt = package.export_package(author, output, modes=['p1'])
        rows = receipt['coverage']
        self.assertEqual(4, len(rows))
        self.assertTrue(all(row['available'] and row.get('shard') for row in rows),
                        [(row['stage'], row['gaps']) for row in rows])
        self.assertTrue(all(row['destinations'] > 0 for row in rows))
        self.assertEqual({'hud', 'decorations'}, {row['stage'] for row in rows})
        notices = {gap.get('code', gap.get('feature')) for row in rows for gap in row['gaps']}
        self.assertTrue({'native-bpm-extrema-integer-adaptation', 'native-progress-intro-adaptation'} <= notices)
        self.assertTrue(all(not package.blocking_gaps(row['gaps'], row['stage']) for row in rows))
        selection = (output / 'selection7.lua').read_text(encoding='utf-8')
        for row in rows:
            self.assertIn(row['shard'], selection)
            self.assertTrue((output / (row['shard'] + '.lua')).is_file())
        decoration = next(row for row in rows if row['stage'] == 'decorations')
        hud = next(row for row in rows if row['stage'] == 'hud')
        self.assertIn('["slider"]', (output / (decoration['shard'] + '.lua')).read_text(encoding='utf-8'))
        self.assertIn('["text"]', (output / (hud['shard'] + '.lua')).read_text(encoding='utf-8'))


if __name__ == '__main__':
    unittest.main()
