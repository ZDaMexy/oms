"""Own21 Graph closure and old17 source routing, using only synthetic bytes."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard22_graph_reading_events as source22_fixture
import test_standard_earlier_graph as earlier_fixture
from standard_graph import TARGETS, build_graph, build_graph_target
from standard_graph_targets import export_graph_target_module
from standard_source_catalog import (COMPOSITION21, SOURCE21_ALIASES, SOURCE21_STYLES,
                                     frame_id, package_key, presentation_family, styles)


class Standard21GraphTests(unittest.TestCase):
    def setUp(self):
        fixture = source22_fixture.Standard22GraphTests()
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.library, self.font, self.metadata = fixture.library, fixture.font, fixture.metadata
        self.old_styles = styles(self.metadata)
        self.before = {(family, layout, target): self.build(family, layout, target)
                       for family in ('0', '1', 's23', 's24', 's22')
                       for layout in ('p1', 'p2', 'dp') for target in (None, *TARGETS)}
        self.metadata['sourceComposition'].update(contract=COMPOSITION21, aliases21=SOURCE21_ALIASES)
        self.metadata['packages'].extend({'source': f'contents/data/graphic/{package_key(style)}.ifs'}
                                        for style in SOURCE21_STYLES)
        own = 'contents/data/graphic/s21/graph.ifs'
        rows = []
        for original in self.metadata['textures']:
            if original['package'] != 'contents/data/graphic/s22/graph.ifs':
                continue
            row = copy.deepcopy(original)
            row['package'] = own
            row['sprite'] = row['sprite'].replace('synthetic-s22', 'synthetic-s21')
            path = self.library / row['sprite']
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(('independent21 fixture:' + row['name']).encode())
            row['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
            rows.append(row)
        self.metadata['textures'].extend(rows)
        archive = self.library / 'source-copies' / own
        archive.parent.mkdir(parents=True)
        archive.write_bytes(b'independent21 synthetic graph archive')
        self.metadata['packages'].append({'source': own, 'sha256': hashlib.sha256(archive.read_bytes()).hexdigest()})
        xml = self.library / 'packages/contents/data/graphic/s21/graph/raw/tex/texturelist.xml'
        xml.parent.mkdir(parents=True)
        xml.write_bytes((self.library / 'packages/contents/data/graphic/s22/graph/raw/tex/texturelist.xml').read_bytes())
        self.write_metadata()

    def write_metadata(self):
        (self.library / 'library.json').write_text(json.dumps(self.metadata), 'utf-8')

    def build(self, family, layout, target=None):
        if target is None:
            return build_graph(self.library, layout, self.font, common_family=family)
        return build_graph_target(self.library, layout, target, self.font, common_family=family)

    def test_21_uses_own_panel_and_qualified_rank_geometry_in_all_layouts(self):
        for layout in ('p1', 'p2', 'dp'):
            value = self.build('s21', layout)
            nodes = earlier_fixture.EarlierGraphTests.nodes(value)
            left = 0 if layout == 'p2' else 1000
            for rank in ('a', 'aa', 'aaa'):
                line = nodes[f'standard.graph.fs21.{layout}.line.{rank}']['properties']
                self.assertEqual((left + 5) / 1280, line['x'])
                self.assertEqual((270 / 1280, 15 / 720), (line['width'], line['height']))
                track = next(t for t in value['tracks'] if t['clock'] == 'score.rank-' + rank + '-progress')
                self.assertEqual([529 / 720, 79 / 720], [k['value'] for k in track['keyframes']])
            self.assertFalse(any(n.endswith(('.line.maximum', '.line.baseline')) for n in nodes))
            font_paths = {row['path'] for row in self.font['files']}
            self.assertTrue(all('/fs21/' in r['path'] or r['path'] in font_paths for r in value['files']))
            self.assertEqual('supplied IIDX21 x86 DLL', value['receipt']['callerBasis']['software'])
            self.assertNotIn('supplied24', json.dumps(value['receipt']))

    def test_21_all_targets_keep_host_policies_explicit_and_own_source_tag(self):
        for layout in ('p1', 'p2', 'dp'):
            for target in TARGETS:
                value = self.build('s21', layout, target)
                nodes = earlier_fixture.EarlierGraphTests.nodes(value)
                tag = nodes[f'standard.graph.fs21.target.{target}.{layout}.tag']
                self.assertEqual('standard.graph.fs21.texture.gr-score-tag-2', tag['resource'])
                self.assertIn('not assert a reconstruction of21', value['receipt']['policyBasis'])
                self.assertIn('priorities58/57', value['receipt']['cutins']['layers'])
                self.assertIn('host adaptation', value['receipt']['cutins']['layers'])

    def test_missing_or_resized_21_resource_is_rejected_without_cross_family_fallback(self):
        row = next(r for r in self.metadata['textures'] if r['package'].endswith('/s21/graph.ifs')
                   and r['name'] == 'gr_line_aaa.png')
        row['width'] = 272
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, 'dimensions changed'):
            self.build('s21', 'p1')
        self.metadata['textures'].remove(row)
        self.write_metadata()
        with self.assertRaises(KeyError):
            self.build('s21', 'p1')
        self.build('s22', 'p1')

    def test_existing_17_themes_keep_all_graph_and_target_nodes_exact(self):
        self.assertEqual(17, len(self.old_styles))
        for style in self.old_styles:
            family = presentation_family(style)
            for layout in ('p1', 'p2', 'dp'):
                for target in (None, *TARGETS):
                    self.assertEqual(self.before[family, layout, target], self.build(family, layout, target))

    def test_target_module_routes_all_18_themes_and_three_layouts(self):
        value = export_graph_target_module(self.library, self.library.parent / 'graph-targets', self.font)
        self.assertEqual(108, len(value['receipts']))
        for choice in value['module']['choices']:
            for style in styles(self.metadata):
                family = presentation_family(style)
                for layout in ('p1', 'p2', 'dp'):
                    selected = {'frame': frame_id(style)}
                    if layout != 'dp':
                        selected['layout'] = layout
                    branch = next((v for v in choice['variants'] if
                                   all(selected.get(k) == item for k, item in v['when'].items())), choice)
                    suffix = '' if family == '0' else '/f' + family
                    self.assertEqual(f"standard/graph{suffix}/targets/{choice['value']}/{layout}/scene.json", branch['scene'])


if __name__ == '__main__':
    unittest.main()
