"""Selected original cursor art, playback ownership and official CPU geometry."""

from copy import deepcopy
import hashlib
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
from beatoraja_author import AuthorPackage, Contribution, material_bindings, material_templates
from beatoraja_native import AssetSink, lua
from beatoraja_progress import export_progress, progress_ownership


AUTHORS = {
    "standard": "E:/OMS-Development/sources/OMS-Standard-Model-24-20261107/author-r8",
    "lightning": "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1",
}


class ProgressTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-progress-dev"
        parent.mkdir(parents=True, exist_ok=True)
        temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(temporary.cleanup)
        self.folder = Path(temporary.name)

    def fixture(self):
        source, output = self.folder / "source", self.folder / "output"
        source.mkdir(); output.mkdir()
        raster = Image.new("RGBA", (22, 30), (27, 59, 114, 0))
        raster.putpixel((3, 7), (180, 90, 40, 200))
        raster.save(source / "cursor.png")
        cursor = {"id": "cursor", "type": "sprite", "resource": "original", "blend": "additive",
                  "properties": {"x": 0, "y": .7, "width": 1, "height": 30 / 414}}
        travel = {"id": "travel", "type": "container", "properties": {
            "x": 25 / 1280, "y": 30 / 720, "width": 22 / 1280, "height": 414 / 720}, "children": [cursor]}
        root = {"id": "information", "type": "container", "properties": {"opacity": .8, "brightness": .9}, "children": [travel]}
        scene = {"templates": [{"id": "template", "root": root}],
                 "instances": [{"template": "template", "material": {"slot": "decoration", "resource": "selected"}}],
                 "bindings": [{"id": "progress", "target": "cursor", "property": "y", "source": "timing.progress"}],
                 "variants": [], "tracks": []}
        manifest = {"resources": [{"id": "original", "type": "texture", "path": "cursor.png"}]}
        ini = '[GameplaySkin.Common:1]\nTarget: Global ruleset=bms keymode=7k presentation=p1\ndecoration: resource Provide "selected"'
        frame = Contribution("frame", "fixture", "manifest.json", "scene.json", "skin.ini", manifest, scene, ini)
        author = SimpleNamespace(family="standard", contribution=lambda name, options: frame)
        return author, frame, AssetSink(source, output)

    def test_original_pixels_binding_override_tint_and_noninteractive_travel(self):
        author, frame, sink = self.fixture()
        before = deepcopy(frame.scene)
        result = export_progress(author, {}, "7k", "p1", sink, (0, 0, 1280, 720))
        slider, destination = result["slider"][0], result["destination"][0]
        self.assertEqual((6, 2, 414, False), (slider["type"], slider["angle"], slider["range"], slider["changeable"]))
        self.assertEqual((22, 30), (slider["w"], slider["h"]))
        self.assertEqual({"x": 25, "y": 660, "w": 22, "h": 30, "r": 230, "g": 230, "b": 230, "a": 204}, destination["dst"][0])
        self.assertEqual(2, destination["blend"])
        self.assertEqual((sink.author_root / "cursor.png").read_bytes(), (sink.output / sink.source[slider["src"]]["path"]).read_bytes())
        self.assertEqual(["cursor"], progress_ownership(author, {}, "7k", "p1")["handled_node_ids"])
        self.assertEqual(before, frame.scene)

    def test_unselected_material_and_other_presentation_are_not_claimed(self):
        author, frame, sink = self.fixture()
        frame.scene["instances"].append({"template": "template", "material": {"slot": "decoration", "resource": "unused"}})
        self.assertEqual(["cursor"], progress_ownership(author, {}, "7k", "p1")["handled_node_ids"])
        result = export_progress(author, {}, "7k", "p2", sink, (0, 0, 1280, 720))
        self.assertEqual([], result["slider"])
        self.assertEqual([], result["handled_node_ids"])
        self.assertEqual({}, sink.source)

    def test_unmapped_geometry_clip_or_clock_is_rejected_instead_of_static(self):
        author, frame, sink = self.fixture()
        root = frame.scene["templates"][0]["root"]
        for change in ("dynamic", "clip", "rotation", "track"):
            original = deepcopy(frame.scene)
            if change == "dynamic":
                frame.scene["bindings"].append({"id": "other", "target": "travel", "property": "x", "source": "unmapped"})
            elif change == "clip":
                root["type"] = "clip"
            elif change == "rotation":
                root["properties"]["rotation"] = 180
            else:
                frame.scene["tracks"].append({"target": "cursor", "property": "opacity", "clock": "timing.beat"})
            with self.subTest(change=change), self.assertRaises(ValueError):
                export_progress(author, {}, "7k", "p1", sink, (0, 0, 1280, 720))
            frame.scene.clear(); frame.scene.update(original)
            root = frame.scene["templates"][0]["root"]

    def test_all_source_routes_and_official_slider_progress_at_hd_fullhd(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja JAR and JDK required")
        cases, counts = [], {}
        for family, default in AUTHORS.items():
            path = Path(os.environ.get("OMS_" + family.upper() + "_AUTHOR", default))
            if not path.is_dir():
                self.skipTest("Both frozen independent authors are required")
            author = AuthorPackage(path, family)
            output = self.folder / family
            output.mkdir()
            sink = AssetSink(path, output)
            width, height = (1280, 720) if family == "standard" else (1920, 1080)
            counts[family] = 0
            for choice in author.modules["frame"]["choices"]:
                for keymode, side in (("7k", "p1"), ("7k", "p2"), ("14k", "dp")):
                    options = author.options(keymode, side, {"frame": choice["value"]})
                    frame = author.contribution("frame", options)
                    result = export_progress(author, options, keymode, side, sink, (0, 0, width, height))
                    self.assertEqual(2 if side == "dp" else 1, len(result["slider"]))
                    self.assertEqual(result["handled_node_ids"], progress_ownership(author, options, keymode, side)["handled_node_ids"])
                    targets = set(result["handled_node_ids"])
                    expected = {}
                    # Independently expand the actual simple author routes;
                    # don't reuse the adapter's node_rect or private selector.
                    def visit(node, parent):
                        p = node.get("properties", {})
                        self.assertFalse(any(p.get(key, 0) for key in ("rotation", "scale-x", "scale-y")))
                        x = parent[0] + p.get("x", 0) * parent[2]
                        y = parent[1] + p.get("y", 0) * parent[3]
                        w, h = parent[2] * p.get("width", 1), parent[3] * p.get("height", 1)
                        if node["id"] in targets:
                            self.assertEqual(0, p.get("y", 0))
                            expected[node["id"]] = [x, height - y - h, w, h, parent[3]]
                            return
                        for child in node.get("children", []):
                            if child["id"] in targets or any(n["id"] in targets for n in descendants(child)):
                                visit(child, (x, y, w, h))
                    def descendants(node):
                        yield node
                        for child in node.get("children", []):
                            yield from descendants(child)
                    for binding in material_bindings(frame, keymode, side):
                        if binding["target"]["kind"] == "global" and binding["slot"] == "decoration" and binding["mode"] == "Provide":
                            for root in material_templates(frame, "decoration", binding["resource"]):
                                if any(n["id"] in targets for n in descendants(root)):
                                    visit(root, (0, 0, width, height))
                    self.assertEqual(targets, set(expected))
                    for slider in result["slider"]:
                        digest = sink.source[slider["src"]]["path"].split("/")[-1][:-4]
                        self.assertEqual(digest, hashlib.sha256((sink.output / sink.source[slider["src"]]["path"]).read_bytes()).hexdigest())
                    cases.append({"name": family + "/" + choice["value"] + "/" + side,
                                  "skin": {"w": width, "h": height, "slider": result["slider"], "destination": result["destination"]},
                                  "expected": [expected[node] for node in result["handled_node_ids"]]})
                    counts[family] += len(result["slider"])
        self.assertEqual({"standard": 48, "lightning": 60}, counts)
        recipe = self.folder / "progress.lua"
        recipe.write_text("return " + lua(cases), encoding="utf-8")
        compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.folder),
                                   str(Path(__file__).with_name("ProgressProbe.java"))],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout + compiled.stderr)
        result = subprocess.run([java, "-cp", str(self.folder) + os.pathsep + str(jar),
                                 "bms.player.beatoraja.skin.lua.ProgressProbe", str(recipe)],
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        receipt = json.loads(result.stdout)
        self.assertEqual((81, 108, 2160, 4320), tuple(receipt[k] for k in ("cases", "sourceObjects", "geometryChecks", "propertyChecks")))
        print("Original progress source + native CPU: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
