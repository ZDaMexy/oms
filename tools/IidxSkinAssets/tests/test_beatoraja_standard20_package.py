"""Synthetic combined-header regression for original20's fixed Graph panel."""
from pathlib import Path
import sys
from types import SimpleNamespace
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_package import (available_selections, entry_script,
                               fixed_original20_graph_area, has_component_output)

FRAME = "source20-gmframe0"


def author():
    choices = [{"value": "blank", "label": "Original panel"},
               *[{"value": f"l{i:03}", "label": f"Area {i}"} for i in range(175)]]
    result = SimpleNamespace(family="standard", manifest={"options": [
        {"id": "graph-visible", "type": "toggle", "default": True}]},
        modules={"graph-area": {"choices": choices}})

    def contribution(module, options):
        assert module == "graph-area"
        value = options[module]
        if options["frame"] == FRAME:
            value = "blank"
        return SimpleNamespace(manifest_path=f"graph/{value}/manifest.json",
                               scene_path=f"graph/{value}/scene.json", ini_path="empty.ini")

    result.contribution = contribution
    return result


class Original20PackageTests(unittest.TestCase):
    def test_combined19_keeps_all176_choices_and_all_graph_toggle_routes(self):
        a = author()
        frames = [*[f"retained{i}" for i in range(18)], FRAME]
        modes = ["p1", "p2", "dp", "5p1", "5p2"]
        coverage = []
        for mode in modes:
            for frame in frames:
                for choice in a.modules["graph-area"]["choices"]:
                    for visible in (True, False):
                        options = {"frame": frame, "graph-area": choice["value"], "graph-visible": visible}
                        fragment = {} if frame == FRAME or choice["value"] == "blank" else {"destination": [{"id": "art"}]}
                        available = has_component_output(fragment, "graph-area", choice["value"], options) or fixed_original20_graph_area(a, options)
                        coverage.append({"mode": mode, "frame": frame, "choice": choice["value"],
                                         "stage": "graph-area", "options": {"graph-visible": visible},
                                         "available": available, "gaps": []})
        choices, controls = available_selections(a, "graph-area", coverage, modes, frames)
        self.assertEqual(choices, [c["value"] for c in a.modules["graph-area"]["choices"]])
        self.assertEqual(controls, {"graph-visible": [True, False]})

    def test_fixed_absence_requires_standard_exact_source_and_exact_blank_paths(self):
        a = author()
        options = {"frame": FRAME, "graph-area": "l000"}
        self.assertTrue(fixed_original20_graph_area(a, options))
        for frame in ("iidx20", "source22-gmframe9", "source21-gmframe0"):
            self.assertFalse(fixed_original20_graph_area(a, {**options, "frame": frame}))
        a.family = "lightning"
        self.assertFalse(fixed_original20_graph_area(a, options))
        a.family = "standard"
        normal = a.contribution
        for key in ("manifest_path", "scene_path", "ini_path"):
            def changed(module, values):
                value = normal(module, values)
                if values[module] != "blank":
                    setattr(value, key, "different")
                return value
            a.contribution = changed
            self.assertFalse(fixed_original20_graph_area(a, options), key)
        a.contribution = lambda *_: SimpleNamespace(manifest_path=None, scene_path=None, ini_path=None)
        self.assertFalse(fixed_original20_graph_area(a, options))

    def test_missing_art_on_retained_frame_still_has_no_output(self):
        a = author()
        options = {"frame": "retained0", "graph-area": "l000", "graph-visible": True}
        self.assertFalse(has_component_output({}, "graph-area", "l000", options))
        self.assertFalse(fixed_original20_graph_area(a, options))

    def test_fixed_panel_notice_changes_only_original20_item_name_and_default(self):
        for frames, default in ((["retained0", FRAME], "retained0"), ([FRAME], FRAME)):
            choices = {"frame": [{"value": f, "label": f} for f in frames],
                       "graph-area": [{"value": "blank", "label": "Original panel"}]}
            script = entry_script({"name": "Synthetic"}, ["dp"], frames, {}, choices,
                                  {"frame": default, "graph-area": "blank"}, {})
            header = script.split("if not skin_config", 1)[0]
            label = FRAME + " (Graph Area: fixed original panel)"
            self.assertEqual(header.count(label), 2 if default == FRAME else 1)
            self.assertIn('"Graph Area"', header)
            if "retained0" in frames:
                self.assertNotIn("retained0 (Graph Area:", header)


if __name__ == "__main__":
    unittest.main()
