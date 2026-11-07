import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch

import plan_object_feedback as planner


class ObjectFeedbackSelectionTest(unittest.TestCase):
    def test_shared_manifest_different_scenes_keep_both_mounted_movies(self):
        class Author:
            def __init__(self, root, family):
                self.root = root
                self.modules = {
                    'charge-notes': {'default': 'c0', 'choices': [
                        {'value': 'c0', 'variants': [{'when': {'toggle': False}}]}]},
                    'frame': {'default': 'f0', 'choices': [{'value': 'f0'}]},
                }

            def options(self, keymode, presentation, overrides=None):
                return {'charge-notes': 'c0', 'frame': 'f0', 'toggle': True,
                        'keymode': keymode, **(overrides or {})}

            def contribution(self, module, options):
                slot = 'effect.long-note' if module == 'charge-notes' else 'effect.key-flash'
                mode = 'Provide "marker"' if module == 'charge-notes' else 'Suppress'
                suffix = 'a' if options['toggle'] else 'b'
                node = {'id': 'n.' + suffix, 'type': 'recorded-animation', 'animation': 'clip.' + suffix}
                return SimpleNamespace(
                    manifest_path='shared.json', scene_path=suffix + '.json', ini_path='material.ini',
                    ini='[Material]\nTarget: lane ruleset=bms keymode=' + options['keymode'] + '\n' + slot + ': resource ' + mode,
                    scene={'instances': [{'template': 't', 'material': {'slot': slot, 'resource': 'marker'}}]},
                    templates={'t': node, 'unused': {'id': 'unused', 'type': 'recorded-animation', 'animation': 'unused.clip'}},
                )

        class Sources:
            def sha(self, path):
                return 'fixture-scene'

            def describe(self, family, root, manifest, animation):
                return {'signature': animation, 'files': {}, 'sourceDecodedBytes': 4,
                        'program': {'width': 1, 'height': 1, 'fps': 60, 'frames': [{}, {}],
                                    'trigger': 'object.state', 'loop': True}}

        with patch.object(planner, 'AuthorPackage', Author), patch.object(planner, 'Sources', Sources), \
                patch.object(planner, 'source_path', lambda root, relative: root / relative):
            jobs, declarations = planner.collect({'standard': Path('synthetic-author')})

        self.assertEqual({'clip.a', 'clip.b'}, {j['animation'] for j in jobs})
        self.assertTrue(all(len(j['consumers']) == 3 for j in jobs))
        self.assertEqual(3, declarations['standard:effect.key-flash:Suppress'])
        self.assertEqual(6, declarations['standard:effect.long-note:Provide'])
        self.assertTrue(all(j['kind'] == 'long-note-effect' for j in jobs))


if __name__ == '__main__':
    unittest.main()
