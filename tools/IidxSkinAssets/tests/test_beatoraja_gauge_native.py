"""CPU-only Gauge clip/pulse qualification against the installed native JAR.

Fixtures retain the source Lightning 27/33 P1/P2 nesting, geometry and pulse;
their tiny synthetic PNGs contain no private artwork. An independent affine
oracle evaluates original source coordinates before native integer rounding.
"""

import json
import math
import os
from pathlib import Path
import shutil
import struct
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_gauge import compose_gauge_shade
from beatoraja_native import AssetSink, lua


def source_fixture(version, side, sink, *, signed_width=False):
    early = version == 27
    fraction = 0.017777777777777774 if early else 0.017817371937639197
    left = (0.047945205 if early else 0.04090909)
    width = 1.0273973 if early else 1.0204545
    height = 0.28636364 if early else 0.27272727
    outer_properties = {"x": -left, "y": 0.37745455, "width": width, "height": height,
                        "clip-mode": "bounds", "reveal-x": 0}
    art_properties = {"x": round(fraction, 9), "colour": "#404040ff"}
    if side == "p2":
        outer_properties.update(x=1.04794521 if early else 1.040909,
                                y=0.6638182 if early else 0.65018182, rotation=180)
        art_properties.update(x=1.0177778 if early else 1.0178174, y=1, rotation=180)
    if signed_width:
        outer_properties.update(x=1 + left, width=-width)
    art = {"id": "shade", "type": "sprite", "resource": "bar", "properties": art_properties}
    inner = {"id": "inner", "type": "clip", "properties": {"x": -round(fraction, 9),
             "clip-mode": "bounds", "reveal-x": fraction}, "children": [art]}
    moving = {"id": "moving", "type": "container", "properties": {}, "children": [inner]}
    outer = {"id": "outer", "type": "clip", "properties": outer_properties, "children": [moving]}
    root = {"id": "root", "type": "container", "properties": {}, "children": [outer]}
    tracks = [{"target": "shade", "property": "x", "clock": "gauge.value", "easing": "linear", "loop": False,
               "keyframes": [{"time": 0, "value": fraction + int(side == "p2")},
                             {"time": 1, "value": fraction - 1 + int(side == "p2")}]}]
    if early:
        tracks.append({"target": "moving", "property": "opacity", "clock": "timing.time", "easing": "linear", "loop": True,
                       "keyframes": [{"time": 0, "value": .2}, {"time": 200, "value": 1}, {"time": 400, "value": .2}]})
    scene = {"root": root, "bindings": [
        {"target": "outer", "property": "reveal-x", "source": "gauge.value"},
        {"target": "moving", "property": "x", "source": "gauge.value"}], "tracks": tracks}
    recipe = {"family": "lightning", "context": {},
        "images": [{"id": "art", "src": sink.add_source("test-bar.png"), "source": "test-bar.png",
                    "x": 0, "y": 0, "w": 2, "h": 1}],
        "resource_images": {"bar": "art"}, "bars": [{"ancestors": [root, outer],
            "hosts": [{"slot": "hud.gauge", "target": {"kind": "stage", "group-logical": 0}}]}]}
    stage = ((72 if side == "p1" else 1410), 852.4799748, 438, 109.999998) if early else (
        (70.9999968 if side == "p1" else 1409.000064), 854.4799728, 440.0000064, 109.999998)
    return recipe, SimpleNamespace(scene=scene, templates={}), stage, fraction


def affine_source_sample(contribution, stage, fraction):
    """Source local transforms -> global signed image and intersection of clips.

    Unlike the exporter, this carries a matrix and positive local sizes through
    each rotation, then projects all four clip corners into the canvas.
    """
    matrix = (1.0, 0.0, 0.0, 1.0, stage[0], stage[1])
    size, clip = stage[2:], None
    root = contribution.scene["root"]
    route = []
    while True:
        route.append(root)
        if not root.get("children"):
            break
        root = root["children"][0]
    for node in route:
        properties = dict(node["properties"])
        for binding in contribution.scene["bindings"]:
            if binding["target"] == node["id"]:
                properties[binding["property"]] = fraction
        for track in contribution.scene["tracks"]:
            if track["target"] == node["id"] and track["clock"] == "gauge.value":
                first, last = track["keyframes"]
                properties[track["property"]] = first["value"] + fraction * (last["value"] - first["value"])
        width, height = size[0] * properties.get("width", 1), size[1] * properties.get("height", 1)
        angle = math.radians(properties.get("rotation", 0))
        c, s = math.cos(angle), math.sin(angle)
        tx, ty = size[0] * properties.get("x", 0), size[1] * properties.get("y", 0)
        a, b, d, e, x, y = matrix
        matrix = (a*c+d*s, b*c+e*s, -a*s+d*c, -b*s+e*c, x+a*tx+d*ty, y+b*tx+e*ty)
        size = (width, height)
        if node["type"] == "clip":
            a, b, d, e, x, y = matrix
            reveal = properties.get("reveal-x", 1)
            corners = [(x+a*px+d*py, y+b*px+e*py)
                       for px, py in ((0, 0), (width*reveal, 0), (0, height), (width*reveal, height))]
            bounds = [min(p[0] for p in corners), min(p[1] for p in corners),
                      max(p[0] for p in corners), max(p[1] for p in corners)]
            clip = bounds if clip is None else [max(clip[0], bounds[0]), max(clip[1], bounds[1]),
                                                min(clip[2], bounds[2]), min(clip[3], bounds[3])]
    a, b, d, e, x, y = matrix
    if abs(b) > 1e-8 or abs(d) > 1e-8:
        raise AssertionError("Fixture is not axis aligned")
    return [x, 1080-y-e*size[1], a*size[0], e*size[1]], [clip[0], 1080-clip[3], max(0, clip[2]-clip[0]), max(0, clip[3]-clip[1])]


def float32(number):
    return struct.unpack("f", struct.pack("f", number))[0]


class GaugeNativeTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-gauge-native-tests"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        self.source, self.output = self.folder / "source", self.folder / "output"
        self.source.mkdir(); self.output.mkdir()
        raster = Image.new("RGBA", (2, 1))
        raster.putdata([(20, 30, 40, 128), (50, 60, 70, 255)])
        raster.save(self.source / "test-bar.png")
        self.sink = AssetSink(self.source, self.output)

    def cases(self):
        cases = []
        for version, side, signed in ((27, "p1", False), (27, "p2", False),
                                      (33, "p1", False), (33, "p2", False), (33, "p1", True)):
            recipe, source, stage, fraction = source_fixture(version, side, self.sink, signed_width=signed)
            result = compose_gauge_shade(recipe, source, [stage], 1080, self.sink)
            gauge_values = [0, .0000001, .0001998, .0002001, .01, 1,
                            fraction*100 - .00001, fraction*100, fraction*100 + .00001,
                            12.34567, 50, 99.99999, 100]
            samples = []
            for value in gauge_values:
                value = float32(value)
                rect, clip = affine_source_sample(source, stage, value / 100)
                samples.append({"gauge": value, "rect": rect, "clip": clip})
            cases.append({"name": str(version) + " " + side + (" signed-width" if signed else ""),
                          "skin": {**result, "source": list(self.sink.source.values())}, "samples": samples,
                          "pulse": version == 27, "span": stage[2] * source.scene["root"]["children"][0]["properties"]["width"]})
        return cases

    def test_source_affine_oracle_catches_p2_rotation_and_signed_clip(self):
        for case in self.cases():
            with self.subTest(case=case["name"]):
                destination = case["skin"]["destination"][0]
                source = next(sample for sample in case["samples"] if sample["gauge"] == 100)
                last = destination["dst"][-1]
                for field, expected in zip(("x", "y", "w", "h"), source["rect"]):
                    self.assertAlmostEqual(round(expected), last[field], places=6)
                for field, expected in zip(("clip_x", "clip_y", "clip_w", "clip_h"), source["clip"]):
                    self.assertAlmostEqual(round(expected), last[field], places=6)
                self.assertGreater(last["clip_w"], 0)
                self.assertGreater(last["clip_h"], 0)

    def test_official_native_interpolated_clip_tiny_positive_and_alpha_palette(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        recipe = self.output / "gauge-cases.lua"
        cases = self.cases()
        recipe.write_text("return " + lua(cases), encoding="utf-8")
        source = Path(__file__).with_name("GaugeClipProbe.java")
        result = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.folder), str(source)],
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        result = subprocess.run([java, "-Djava.awt.headless=true", "-cp", str(self.folder)+os.pathsep+str(jar),
                                 "bms.player.beatoraja.skin.lua.GaugeClipProbe", str(recipe)],
                                capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, result.returncode, result.stdout + result.stderr)
        receipt = json.loads(result.stdout)
        self.assertEqual(5, receipt["cases"])
        self.assertLess(receipt["maxClockErrorMs"], 2.001)
        self.assertLess(receipt["maxSourceEdgeErrorPx"], 1.002)
        self.assertLessEqual(receipt["maxAlphaByteError"], 1)
        self.assertGreater(receipt["emptyClipGuards"], 0)
        print("Native Gauge probe: " + json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
