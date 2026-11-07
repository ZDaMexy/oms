"""Entrance selection must follow mounted branches, not one initial phase."""

from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import plan_entrances as planner


def movie(name, clock="presentation.elapsed"):
    return {"id": name, "type": "recorded-animation", "animation": name + ".clip", "clock": clock}


def contribution(keymode="7k"):
    root = {"id": "mounted", "type": "container", "properties": {"opacity": 0},
            "children": [movie("opening"), movie("lane", "preparation.elapsed"),
                         movie("ready", "preparation.ready-elapsed"), movie("steady", "value.fixed"),
                         {"id": "off", "type": "container", "children": [movie("disabled")]}]}
    return SimpleNamespace(
        manifest_path="shared.json", scene_path="scene.json", ini_path="material.ini",
        ini='[Material]\nTarget: global ruleset=bms keymode=' + keymode + '\nstage.background: resource Provide "marker"',
        scene={"root": {"id": "root", "type": "container"},
               "instances": [{"template": "mounted", "material": {"slot": "stage.background", "resource": "marker"}}],
               "bindings": [{"id": "enable", "target": "mounted", "property": "visible", "source": "option.entrance-animation"},
                            {"id": "disable", "target": "off", "property": "visible", "source": "option.unused"}],
               "tracks": [{"id": "fade", "target": "mounted", "property": "opacity", "clock": "preparation.elapsed",
                           "keyframes": [{"time": 0, "value": 0}, {"time": 200, "value": 1}]}]},
        templates={"mounted": root, "unmounted": movie("unmounted")})


class EntranceSelectionTest(unittest.TestCase):
    def test_initial_fade_zero_does_not_drop_the_whole_opening(self):
        source = contribution()
        options = {"entrance-animation": True, "unused": False}
        selected = list(planner.mounted_entrances(source, "7k", "p1", options))
        self.assertEqual({"opening.clip", "lane.clip"}, {node["animation"] for node, _ in selected})
        self.assertTrue(all(mount["slot"] == "stage.background" for _, mount in selected))
        options["entrance-animation"] = False
        self.assertEqual([], list(planner.mounted_entrances(source, "7k", "p1", options)))

    def test_judge_panel_variants_sharing_manifest_keep_distinct_movies(self):
        class Author:
            def __init__(self, root, family):
                self.root = root
                self.modules = {"frame": {"default": "f0", "choices": [{"value": "f0"}]}}

            def options(self, keymode, mode, overrides=None):
                return {"frame": "f0", "graph-visible": True, "judge-panel-visible": True,
                        "entrance-animation": True, "lane-opening": True, "unused": False,
                        "keymode": keymode, **(overrides or {})}

            def contribution(self, module, options):
                value = contribution(options["keymode"])
                suffix = "with-panel" if options["judge-panel-visible"] else "without-panel"
                value.scene_path = suffix + ".json"
                value.templates["mounted"]["children"] = [movie(suffix)]
                return value

        class Sources:
            def sha(self, path):
                return str(path)

            def describe(self, family, root, manifest, animation):
                return {"signature": animation, "files": {}, "sourceDecodedBytes": 4,
                        "program": {"width": 1, "height": 1, "fps": 60, "trigger": "gameplay.attach",
                                    "loop": True, "loopStartFrame": 1, "nodes": [{"geometry": 0}],
                                    "frames": [{"nodes": [[0]]}, {"nodes": [[0]]}, {"nodes": []}]}}

        with patch.object(planner, "AuthorPackage", Author), \
                patch.object(planner, "source_path", lambda root, relative: root / relative):
            jobs, selections = planner.collect({"lightning": Path("synthetic")}, Sources())
        self.assertEqual(12, len(selections))
        self.assertEqual({"with-panel.clip", "without-panel.clip"}, {j["animation"] for j in jobs})
        self.assertTrue(all(len(j["consumers"]) == 6 for j in jobs))
        self.assertTrue(all(j["frames"] == "all" and j["sourceFrameCount"] == 3 for j in jobs))
        self.assertTrue(all(j["loopStartFrame"] == 1 and j["loop"] for j in jobs))


if __name__ == "__main__":
    unittest.main()
