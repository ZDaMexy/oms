"""22 source routing, distinct native geometry and host boundaries; no game art."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import Mock, call, patch

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard_earlier_graph as earlier
import test_standard_graph as graph_fixture
import test_standard_graph_notifications as notification_fixture
from test_standard_reading import SyntheticFrame, walk
from standard_failure import export_failure
from standard_graph import build_graph, build_graph_target
from standard_graph_notifications import export_graph_notifications
from standard_reading import add_reading, export_ready
from standard_source_catalog import (COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES,
                                     SOURCE22_STYLES, package_key)


class Standard22GraphTests(unittest.TestCase):
    def setUp(self):
        fixture = earlier.EarlierGraphTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.library, self.font = fixture.library, fixture.font
        self.metadata = fixture.metadata
        self.metadata['sourceComposition'] = {'contract': COMPOSITION22,
            'aliases24': EARLIER_ALIASES, 'aliases22': SOURCE22_ALIASES}
        self.metadata['packages'].extend({'source': f'contents/data/graphic/{package_key(style)}.ifs'}
                                        for style in SOURCE22_STYLES)
        original = 'contents/data/graphic/s23/graph.ifs'
        own = 'contents/data/graphic/s22/graph.ifs'
        rows = []
        for value in self.metadata['textures']:
            if value['package'] != original:
                continue
            value = copy.deepcopy(value)
            value['package'] = own
            value['sprite'] = value['sprite'].replace('synthetic-s23', 'synthetic-s22')
            path = self.library / value['sprite']
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(('independent22 fixture:' + value['name']).encode())
            value['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
            rows.append(value)
        self.metadata['textures'].extend(rows)
        archive = self.library / 'source-copies' / own
        archive.parent.mkdir(parents=True)
        archive.write_bytes(b'independent22 synthetic graph archive')
        self.metadata['packages'].append({'source': own, 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()})
        xml = self.library / 'packages/contents/data/graphic/s22/graph/raw/tex/texturelist.xml'
        xml.parent.mkdir(parents=True)
        xml.write_bytes((self.library / 'packages/contents/data/graphic/s23/graph/raw/tex/texturelist.xml').read_bytes())
        self.write_metadata()

    def write_metadata(self):
        (self.library / 'library.json').write_text(json.dumps(self.metadata), 'utf-8')

    def test_22_owns_art_and_its_rank_offset_without_borrowing_old_geometry(self):
        for layout in ('p1', 'p2', 'dp'):
            value = build_graph(self.library, layout, self.font, common_family='s22')
            nodes = earlier.EarlierGraphTests.nodes(value)
            left = 0 if layout == 'p2' else 1000
            for rank in ('a', 'aa', 'aaa'):
                line = nodes[f'standard.graph.fs22.{layout}.line.{rank}']['properties']
                self.assertEqual((left + 5) / 1280, line['x'])
                self.assertEqual((270 / 1280, 15 / 720), (line['width'], line['height']))
                track = next(t for t in value['tracks'] if t['clock'] == 'score.rank-' + rank + '-progress')
                self.assertEqual([529 / 720, 79 / 720], [k['value'] for k in track['keyframes']])
            self.assertFalse(any(n.endswith(('.line.maximum', '.line.baseline')) for n in nodes))
            own_files = [r for r in value['files'] if not r['path'].startswith('fixture/')]
            self.assertTrue(any('/fs22/' in r['path'] for r in own_files))
            self.assertFalse(any('/fs23/' in r['path'] or '/fs24/' in r['path'] for r in own_files))
            self.assertEqual([529, 79], value['receipt']['sourceGeometry']['rankLineY'])
            self.assertEqual('supplied IIDX22 x86 DLL', value['receipt']['callerBasis']['software'])

    def test_22_target_header_uses_its_own_source_with_existing_host_policies(self):
        for layout in ('p1', 'p2', 'dp'):
            value = build_graph_target(self.library, layout, 'next', self.font, common_family='s22')
            nodes = earlier.EarlierGraphTests.nodes(value)
            prefix = f'standard.graph.fs22.target.next.{layout}'
            left = 0 if layout == 'p2' else 1000
            self.assertEqual((left + 1) / 1280, nodes[prefix + '.header']['properties']['x'])
            self.assertEqual('standard.graph.fs22.texture.gr-score-tag-2', nodes[prefix + '.tag']['resource'])
            self.assertIn('not assert a reconstruction of22', value['receipt']['policyBasis'])
            self.assertIn('priorities58/57', value['receipt']['cutins']['layers'])

    def test_missing_22_art_never_falls_back_to_matching_23_or_26_names(self):
        self.metadata['textures'] = [r for r in self.metadata['textures'] if not
            (r['package'].endswith('/s22/graph.ifs') and r['name'] == 'gr_line_aaa.png')]
        self.write_metadata()
        with self.assertRaises(KeyError):
            build_graph(self.library, 'p1', common_family='s22')
        build_graph(self.library, 'p1', common_family='s23')

    def test_existing_rank_extents_remain_separate(self):
        for family, span in (('0', [528, 80]), ('1', [528, 80]), ('s23', [527, 77]), ('s24', [527, 77])):
            value = build_graph(self.library, 'p1', common_family=family)
            track = next(t for t in value['tracks'] if t['clock'] == 'score.rank-aaa-progress')
            self.assertEqual([v / 720 for v in span], [k['value'] for k in track['keyframes']])


class Standard22ReadingTests(unittest.TestCase):
    def test_all_22_frame_sides_keep_current_green_and_do_not_add_later_ranges(self):
        with tempfile.TemporaryDirectory() as directory:
            for style in SOURCE22_STYLES:
                for layout in ('p1', 'p2', 'dp'):
                    frame = SyntheticFrame(Path(directory), layout)
                    frame.style = style
                    source_asset = frame.asset
                    requested = []
                    def asset(package, name):
                        requested.append(package)
                        return source_asset(package, name)
                    frame.asset = asset
                    ready = {layout: {'resources': [], 'animations': [],
                        'node': {'id': 'source22.ready'}, 'source': {'family': 's22'}}}
                    with patch('standard_feedback.rasterize_source_text', return_value=Image.new('RGBA', (84, 20))):
                        add_reading(frame, ready, {'fonts': {'normal': 'host.font2', 'small': 'host.font5'}}, Path(directory) / 'font')
                    self.assertEqual({'s22/gameparts'}, set(requested))
                    sources = {b['source'] for b in frame.scene['bindings']}
                    self.assertIn('scroll.green-number', sources)
                    self.assertFalse(any(s.startswith('scroll.green-number-') for s in sources))
                    self.assertFalse(any(n['id'].endswith('.green.range') for n in walk(frame.info)))
                    self.assertEqual(1500, frame.scene['preparationDuration'])
                    ready_node = next(n for n in walk(frame.info) if n['id'].endswith('.ready.art'))
                    self.assertEqual('preparation.ready-elapsed', ready_node['clock'])
                    receipt = frame.evidence[-1]['reading']
                    self.assertFalse(receipt['sourceCapabilities']['greenRange'])
                    self.assertIn('eleven fixed multipliers', receipt['normal'])

    def test_22_ready_requires_its_full_canvas_and_original_timeline(self):
        source = {'stage': [0, 0, 1280, 720], 'fps': 60, 'sourceFrames': 90,
                  'labels': {}, 'translatedRootControl': []}
        with tempfile.TemporaryDirectory() as directory:
            output = Path(directory)
            (output / 'library.json').write_text('{}', 'utf-8')
            exporter = SimpleNamespace(source_data={}, files={}, receipts=[], clip=Mock(return_value={'source': source}))
            with patch('standard_components.StandardComponents', return_value=exporter), patch('standard_reading.source_styles', return_value=(26, SOURCE22_STYLES[0])):
                result = export_ready(output, output, output, output)
                self.assertEqual({'0', 's22'}, set(result['families']))
                self.assertEqual(3, sum(c.args[0] == 's22/gameparts' for c in exporter.clip.call_args_list))
                for change in ({'sourceFrames': 89}, {'stage': [0, 0, 280, 720]}, {'labels': {'loop': 0}}):
                    exporter.clip.return_value = {'source': {**source, **change}}
                    with self.assertRaises(ValueError):
                        export_ready(output, output, output, output)


class Standard22EventTests(unittest.TestCase):
    def test_22_failure_is_own_movie_and_real_fail_with_no_old_exit_claim(self):
        source = {'movie': 'miss', 'stage': [0, 0, 1280, 720], 'fps': 60,
                  'sourceFrames': 180, 'labels': {}, 'translatedRootControl': []}
        exporter = SimpleNamespace(source_library_sha='fixture', source_families=('s22',), files={},
                                   clip=Mock(return_value={'source': source}))
        with tempfile.TemporaryDirectory() as directory, patch('standard_failure.StandardComponents', return_value=exporter):
            root = Path(directory)
            value = export_failure(root, root, root, root)
        self.assertEqual(call('s22/gameparts', 'miss', 'sf.failed.s22', loop=False, trigger='gameplay.fail'), exporter.clip.call_args)
        receipt = value['families']['s22']['receipt']
        self.assertTrue(receipt['source'].startswith('22 HDD/'))
        self.assertIn('exit duration', receipt['nativeBoundary'])
        self.assertNotIn('nativeExit', receipt)

    def test_22_notification_rank_replacement_and_single_update_preserve_source_program(self):
        import brotli
        fixture = notification_fixture
        class Exporter:
            def __init__(self, library, output, *_):
                self.output, self.files, self.source_library_sha = output, {}, 'fixture'
                self.source_families, self.tools = ('s22',), fixture.source_fixture()[2]
            def _package(self, package):
                self.assert_package(package)
                return fixture.source_fixture()[0]
            @staticmethod
            def assert_package(package):
                if package != 's22/graph': raise AssertionError(package)
            def asset(self, category, package, name):
                self.assert_package(package)
                return {'id': 'synthetic.' + name, 'type': 'texture', 'path': name + '.png'}, fixture.texture(name)
            def clip(self, package, movie, prefix, *, loop, trigger):
                self.assert_package(package)
                if loop or trigger != 'score.crossing': raise AssertionError((loop, trigger))
                relative = f'custom/{prefix}/clip.json.br'
                path = self.output / relative
                path.parent.mkdir(parents=True)
                path.write_bytes(brotli.compress(json.dumps(fixture.program_fixture()).encode()))
                research = self.output / 'research' / prefix
                research.mkdir(parents=True)
                (research / 'source-root.json').write_text(json.dumps(fixture.source_fixture()[1]), 'utf-8')
                return {'source': fixture.source_metadata(movie), 'receipt': {}, 'node': {'id': prefix},
                    'resources': [self.asset('sgn', package, n)[0] for n in ('pass_aaa', 'flash')],
                    'animations': [{'id': prefix + '.clip', 'path': relative}]}
        with tempfile.TemporaryDirectory() as directory, patch('standard_graph_notifications.StandardComponents', Exporter):
            output = Path(directory)
            value = export_graph_notifications(output, output, output, output)['families']['s22']
            self.assertEqual({'mybest', 'target', 'aaa', 'aa', 'a', 'update'}, set(value['clips']))
            self.assertEqual({'mybest': 57, 'target': 57, 'lv_pass': 57, 'update': 58}, value['receipt']['sourcePriorities'])
            aaa = value['clips']['aaa']
            original = json.loads(brotli.decompress((output / aaa['animations'][0]['path']).read_bytes()))
            for rank in ('aa', 'a'):
                clip = value['clips'][rank]
                result = json.loads(brotli.decompress((output / clip['animations'][0]['path']).read_bytes()))
                result['geometries'][0]['resource'] = 'synthetic.pass_aaa'
                self.assertEqual(original, result)
            self.assertIn('hostEligibilityBoundary', value['receipt'])


if __name__ == '__main__':
    unittest.main()
