"""Original20 movie routes and host/source boundaries, with synthetic payloads."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from standard_feedback import build_feedback, source_hook
from standard_failure import export_failure
from standard_gauge_effect import export_gauge_feedback
from standard_graph_notifications import export_graph_notifications
from test_standard20_source_identity import composition20
import test_standard_feedback as feedback_fixture
import test_standard_gauge_effect as gauge_fixture
import test_standard_graph_notifications as notification_fixture

STYLE = 'source20-gmframe0'


class SyntheticGaugeExporter:
    calls = []

    def __init__(self, library, output, bemani_root, image_tools):
        self.output, self.files, self.source_library_sha = output, {}, 'synthetic-library'
        output.mkdir(parents=True)

    def clip(self, package, movie, prefix, *, loop, trigger):
        import brotli
        self.calls.append((package, movie, loop, trigger))
        program = gauge_fixture.StandardGaugeEffectTests.source()
        program['frames'] = [copy.deepcopy(program['frames'][0]) for _ in range(60)] + [program['frames'][-1]]
        relative = f'custom/{prefix}/clip.json.br'
        path = self.output / relative
        path.parent.mkdir(parents=True)
        data = brotli.compress(json.dumps(program).encode())
        path.write_bytes(data)
        self.files[relative] = {'path': relative, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)}
        return {'source': {'package': package, 'movie': movie, 'stage': [0, 0, 1280, 720], 'fps': 60, 'sourceFrames': 60},
                'node': {'id': prefix + '.node', 'animation': prefix + '.clip'},
                'resources': [{'id': 'synthetic.texture'}],
                'animations': [{'id': prefix + '.clip', 'path': relative, 'resources': ['synthetic.texture']}],
                'receipt': {'sourceArchive': package}}


class SyntheticNotificationExporter:
    source_families = ('0', 's20')
    calls = []

    def __init__(self, library, output, bemani_root, image_tools):
        self.output, self.files, self.source_library_sha = output, {}, 'synthetic-library'
        self.tools = notification_fixture.source_fixture()[2]
        output.mkdir(parents=True)

    def _package(self, package):
        self.calls.append(('package', package))
        return notification_fixture.source_fixture()[0]

    def asset(self, prefix, package, name):
        self.calls.append(('asset', package, name))
        return {'id': 'synthetic.' + name, 'type': 'texture', 'path': 'synthetic/' + name + '.png'}, notification_fixture.texture(name)

    def clip(self, package, movie, prefix, *, loop, trigger):
        import brotli
        self.calls.append(('clip', package, movie, loop, trigger))
        relative = f'custom/{prefix}/clip.json.br'
        path = self.output / relative
        path.parent.mkdir(parents=True)
        data = brotli.compress(json.dumps(notification_fixture.program_fixture()).encode())
        path.write_bytes(data)
        self.files[relative] = {'path': relative, 'sha256': hashlib.sha256(data).hexdigest(), 'bytes': len(data)}
        research = self.output / 'research' / prefix
        research.mkdir(parents=True)
        (research / 'source-root.json').write_text(json.dumps(notification_fixture.source_fixture()[1]), 'utf-8')
        return {'source': {**notification_fixture.source_metadata(movie), 'package': package}, 'receipt': {}, 'files': [],
                'node': {'id': prefix + '.node', 'animation': prefix + '.clip'},
                'resources': [self.asset('sgn', package, name)[0] for name in ('pass_aaa', 'flash')],
                'animations': [{'id': prefix + '.clip', 'path': relative, 'resources': ['synthetic.pass_aaa', 'synthetic.flash']}]}


class Original20HelperTests(unittest.TestCase):
    def test_gauge_uses_three_original20_movies_without_changing_an_existing_theme(self):
        SyntheticGaugeExporter.calls = []
        with tempfile.TemporaryDirectory() as directory, \
                patch('standard_gauge_effect.StandardComponents', SyntheticGaugeExporter), \
                patch('standard_gauge_effect.metadata', return_value={}), \
                patch('standard_gauge_effect.source_styles', return_value=(26, STYLE)):
            root = Path(directory)
            old = export_gauge_feedback(root, root / 'old', root, root, styles=(26,))
            new = export_gauge_feedback(root, root / 'new', root, root)
            self.assertEqual(old['frames']['f26'], new['frames']['f26'])
            old_files = {row['path']: row for row in old['files']}
            self.assertTrue(all(row == next(item for item in new['files'] if item['path'] == path) for path, row in old_files.items()))
            own = [call for call in SyntheticGaugeExporter.calls if call[0] == 'source20/gmframe0']
            self.assertEqual({call[1] for call in own}, {'gauge_up_1p', 'gauge_up_2p', 'gauge_up_dp'})
            self.assertTrue(all(call[2:] == (False, 'gauge.increase') for call in own))
            self.assertEqual(3, len(new['frames']['f20-gmframe0']))
            for value in new['frames']['f20-gmframe0'].values():
                self.assertEqual('source20/gmframe0', value['source']['package'])
                self.assertIn('not inferred20', value['receipt']['nativeCallerBoundary'])
                self.assertNotIn('sourceSelectionBoundary', old['frames']['f26']['p1']['receipt'])

    def test_failure_keeps_its_own_s20_source_and_does_not_invent_native_exit_timing(self):
        source = {'movie': 'miss', 'stage': [0, 0, 1280, 720], 'fps': 60,
                  'sourceFrames': 180, 'labels': {}, 'translatedRootControl': []}
        exporter = SimpleNamespace(source_library_sha='synthetic', source_families=('0', 's20'), files={},
                                   clip=Mock(side_effect=lambda package, *args, **kwargs: {'source': {**source, 'package': package}}))
        with tempfile.TemporaryDirectory() as directory, patch('standard_failure.StandardComponents', return_value=exporter):
            root = Path(directory)
            result = export_failure(root, root, root, root)
        call = exporter.clip.call_args_list[-1]
        self.assertEqual(('s20/gameparts', 'miss', 'sf.failed.s20'), call.args)
        self.assertEqual({'loop': False, 'trigger': 'gameplay.fail'}, call.kwargs)
        own = result['families']['s20']
        self.assertEqual('s20/gameparts', own['clip']['source']['package'])
        self.assertTrue(own['receipt']['source'].startswith('20 HDD/'))
        self.assertIn('host adaptation', own['receipt']['nativeBoundary'])
        self.assertNotIn('nativeExit', own['receipt'])
        self.assertIn('nativeExit', result['families']['0']['receipt'])

    def test_graph_qualifies_its_live_rank_child_and_separates_host_model_evidence(self):
        SyntheticNotificationExporter.calls = []
        with tempfile.TemporaryDirectory() as directory, \
                patch('standard_graph_notifications.StandardComponents', SyntheticNotificationExporter):
            root = Path(directory)
            result = export_graph_notifications(root, root / 'new', root, root)
        own, old = result['families']['s20'], result['families']['0']
        self.assertEqual({'mybest', 'target', 'aaa', 'aa', 'a', 'update'}, set(own['clips']))
        self.assertTrue(all(clip['source']['package'] == 's20/graph' for clip in own['clips'].values()))
        self.assertEqual(6, len({clip['animations'][0]['path'] for clip in own['clips'].values()}))
        calls = [call for call in SyntheticNotificationExporter.calls if call[0] == 'clip' and call[1] == 's20/graph']
        self.assertEqual({'mybest', 'target', 'update', 'lv_pass'}, {call[2] for call in calls})
        self.assertTrue(all(call[-2:] == (False, 'score.crossing') for call in calls))
        self.assertEqual('lv_pass', own['receipt']['lvPassNamedChild']['namedChild'])
        self.assertEqual(26, own['receipt']['hostNotificationModelEvidence']['softwareVersion'])
        for key in ('sourceAnchor', 'sourcePriorities', 'nativeEligibility'):
            self.assertNotIn(key, own['receipt'])
            self.assertIn(key, old['receipt'])
        self.assertTrue(all(proof['allOtherProgramFieldsUnchanged'] for proof in own['receipt']['rankTextureReplacements'].values()))

    def test_feedback_uses_s20_judge_art_and_refuses_an_unqualified_centre_offset(self):
        fixture = feedback_fixture.StandardFeedbackTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        fixture.inventory.update(composition20())
        fixture.texture['package'] = 'contents/data/graphic/s20/gameparts.ifs'
        fixture.reference['package'], fixture.reference['common_family'] = 'source20/gmframe0', 's20'
        for value in fixture.reference['layouts'].values():
            for draw in value['draws']:
                if draw['texture'] == 'judge_waku':
                    draw['asset']['package'] = 's20/gameparts'
        fixture.save()
        own = source_hook(fixture.reference, 'center_frame_sp', 'p1')
        self.assertEqual('s20/gameparts', own['asset']['package'])
        own['asset']['package'] = 's21/gameparts'
        with self.assertRaisesRegex(ValueError, 'source family'):
            source_hook(fixture.reference, 'center_frame_sp', 'p1')
        own['asset']['package'] = 's20/gameparts'
        fixture.save()
        with self.assertRaisesRegex(ValueError, 'Unqualified caller centre translation'):
            build_feedback(fixture.library, fixture.map, style=STYLE, layout='p1', centre_movie='center_frame_sp',
                           centre_dx=36, contexts=[c for c in material_contexts() if c['layout'] == 'p1'],
                           font_path=fixture.font_file, output=fixture.output, prepared_font=fixture.font)
        self.assertFalse(fixture.output.exists())


if __name__ == '__main__':
    unittest.main()
