"""Original offset fields consume public host state, including missing-value gates."""

from copy import deepcopy
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import AuthorPackage, Contribution, walk_nodes
from beatoraja_hud import export_hud
from beatoraja_native import AssetSink, lua
from beatoraja_export import Composer


def _route(root, target):
    """Retain the actual ancestor transforms/gates; omit unrelated sibling HUDs."""
    if root["id"] == target:
        return deepcopy(root)
    children = [selected for child in root.get("children", [])
                if (selected := _route(child, target)) is not None]
    if not children:
        return None
    return {**deepcopy(root), "children": children}


class _ReadOnlySources:
    def __init__(self, root):
        self.root = root
        self.dimensions, self.sources = {}, {}

    def add_source(self, relative):
        if relative not in self.sources:
            path = self.root / relative
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            with Image.open(path) as image:
                self.dimensions[digest] = image.size
            self.sources[relative] = digest
        return self.sources[relative]


class _TraceComposer(Composer):
    def __init__(self, *args):
        super().__init__(*args)
        self.art_nodes = {}

    def draw_tree(self, contribution, root, *args, **kwargs):
        before = len(self.skin["destination"])
        super().draw_tree(contribution, root, *args, **kwargs)
        if root["type"] in ("sprite", "projective-sprite"):
            for destination in self.skin["destination"][before:]:
                self.art_nodes[destination["id"]] = root["id"]


def _source_art(root, contribution, context, sink):
    """Independent source binding/variant visibility, not native expressions."""
    result = {}

    def visit(node):
        properties = {**node.get("properties", {})}
        if "resource" in node:
            properties["resource"] = node["resource"]
        for binding in contribution.scene.get("bindings", []):
            if binding["target"] == node["id"]:
                properties[binding["property"]] = context[binding["source"]]
        for variant in contribution.scene.get("variants", []):
            if variant["target"] == node["id"]:
                source = context[variant["source"]]
                key = str(source).lower() if isinstance(source, bool) else str(source)
                case = next((c for c in variant["cases"] if c["key"] == key), None)
                properties[variant["property"]] = case.get("resource", case.get("value")) if case else variant["default"]
        if not properties.get("visible", True) or properties.get("opacity", 1) == 0:
            return
        if node["type"] == "sprite":
            result[node["id"]] = sink.add_source(contribution.resources[properties["resource"]]["path"])
        for child in node.get("children", []):
            if child["type"] != "text":
                visit(child)

    visit(root)
    return result


class HudOffsetTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-hud-offset-tests"
        parent.mkdir(parents=True, exist_ok=True)
        self.temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        self.source, self.output = self.folder / "source", self.folder / "output"
        self.source.mkdir(); self.output.mkdir()
        self.sink = AssetSink(self.source, self.output)

    def fixture(self):
        texts = [{"id": name, "type": "text", "properties": {
            "x": .1, "y": .2, "width": .08, "height": .02, "font-size": 12.8,
            "text": name, "alignment": "centre", "format": "signed-fixed-2"}}
            for name in ("signed", "available", "automatic", "known")]
        root = {"id": "offset", "type": "container", "children": texts}
        bindings = [{"id": "value", "target": "signed", "property": "text", "source": "offset.value"}]
        bindings.extend({"id": name+"-gate", "target": name, "property": "visible", "source": source}
                        for name, source in (("available", "offset.available"), ("automatic", "offset.automatic"),
                                             ("known", "offset.automatic-available")))
        scene = {"root": root, "bindings": bindings}
        contribution = Contribution("frame", "offset-probe", None, None, None, {}, scene, "")
        result = export_hud(contribution, root, (0, 0, 1920, 1080), self.sink)
        kinds = {text["id"]: text["id"].rsplit(".", 1)[-1] for text in result["text"]}
        return result, kinds

    def probe(self, cases):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        recipe = self.output / "offset-cases.lua"
        recipe.write_text("return "+lua(cases), encoding="utf-8")
        compiled = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.folder),
                                   str(Path(__file__).with_name("HudOffsetProbe.java"))],
                                  capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compiled.returncode, compiled.stdout+compiled.stderr)
        run = subprocess.run([java, "-cp", str(self.folder)+os.pathsep+str(jar),
                              "bms.player.beatoraja.skin.lua.HudOffsetProbe", str(recipe)],
                             capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, run.returncode, run.stdout+run.stderr)
        return json.loads(run.stdout)

    def test_source_signed_field_and_informational_host_boundary(self):
        result, kinds = self.fixture()
        self.assertEqual(4, len(result["text"]))
        self.assertEqual({"signed", "available", "automatic", "known"}, set(kinds.values()))
        self.assertEqual([{"node": "signed", "source": "offset.value", "native": "main_state.number(12)"}],
                         result["mappings"])
        self.assertEqual(["native-offset-display-adaptation"], [gap["code"] for gap in result["gaps"]])
        signed = next(text for text in result["text"] if kinds[text["id"]] == "signed")
        self.assertIn('"%+.2f"', signed["value"])
        self.assertEqual((1, 18), (signed["align"], signed["size"]))
        self.assertTrue(result["gaps"][0]["informational"])

    def test_actual_luaj_callbacks_distinguish_offset_auto_and_both_sentinels(self):
        result, kinds = self.fixture()
        receipt = self.probe([{"name": "source-shaped-offset", "skin": result, "kinds": kinds}])
        self.assertEqual(1, receipt["cases"])
        self.assertEqual(168, receipt["gateChecks"])
        self.assertEqual(30, receipt["signedChecks"])
        self.assertGreater(receipt["autoIndexReads"], 0)

    def test_all_45_lightning_source_routes_keep_signed_offset_and_panel_ownership(self):
        root = Path(os.environ.get("OMS_LIGHTNING_AUTHOR",
            "E:/OMS-Development/sources/OMS-IIDX-Omni-Until33-20261018/authors/omni-full-v52-current-hispeed-padding-r1"))
        if not root.is_dir():
            self.skipTest("The frozen private Lightning author is required")
        author = AuthorPackage(root, "lightning")
        cases, sources = [], []
        art_sink = _ReadOnlySources(root)
        for choice in author.modules["frame"]["choices"]:
            for mode in ("p1", "p2", "dp"):
                options = author.options("14k" if mode == "dp" else "7k", mode, {"frame": choice["value"]})
                contribution = author.contribution("frame", options)
                gates = [b for b in contribution.scene["bindings"] if b["source"] == "offset.available"]
                self.assertEqual(1, len(gates))
                routes = [route for template in contribution.templates.values()
                          if (route := _route(template, gates[0]["target"])) is not None]
                self.assertEqual(1, len(routes))
                source_sink = AssetSink(root, self.output)
                context = {"layout.ruleset": "bms", "layout.presentation": mode,
                           "preparation.available": False, "preparation.active": False,
                           "preparation.ready": False}
                result = export_hud(contribution, routes[0], (0, 0, 1920, 1080), source_sink,
                                    options=options, context=context)
                self.assertEqual(["native-offset-display-adaptation"], [g.get("code") for g in result["gaps"]])
                self.assertEqual(2, len(result["text"]))
                self.assertEqual(["offset.value"], [m["source"] for m in result["mappings"]])
                kinds = {text["id"]: "signed" if "value" in text else "unit" for text in result["text"]}
                art = _TraceComposer(author, art_sink, options, mode, {})
                art.context.update(context)  # This probe owns the original, unprepared offset pose.
                art.draw_tree(contribution, routes[0])
                self.assertEqual([], art.gaps)
                self.assertEqual(4, len(art.skin["destination"]))
                self.assertEqual(1, len(art.skin["imageset"]))
                self.assertEqual(2, len(art.skin["imageset"][0]["images"]))
                expected = {}
                static_context = {**context, **{"option."+key:value for key,value in options.items()}}
                for offset in (-2147483648, -500, -1, 0, 1, 500, 2147483647):
                    for automatic in (-2147483648, -1, 0, 1, 2, 2147483647):
                        state = {**static_context, "offset.value": offset,
                                 "offset.available": offset not in (-2147483648, 2147483647),
                                 "offset.automatic": automatic == 1,
                                 "offset.automatic-available": automatic in (0, 1)}
                        expected[f"{offset}:{automatic}"] = _source_art(routes[0], contribution, state, art_sink)
                skin = {**result, "image": art.skin["image"], "imageset": art.skin["imageset"],
                        "destination": [*result["destination"], *art.skin["destination"]]}
                cases.append({"name": choice["value"]+"/"+mode, "skin": skin, "kinds": kinds,
                              "artNodes": art.art_nodes, "artExpected": expected})
                hidden = export_hud(contribution, routes[0], (0, 0, 1920, 1080), source_sink,
                                    options={**options, "judge-panel-visible": False}, context=context)
                self.assertEqual([], hidden["text"], "Original Judge panel OFF must own the offset subtree")
                hidden_art = _TraceComposer(author, art_sink, {**options, "judge-panel-visible": False}, mode, {})
                hidden_art.context.update(context)
                hidden_art.draw_tree(contribution, routes[0])
                self.assertEqual([], hidden_art.skin["destination"])
                sources.append({"frame": choice["value"], "mode": mode, "scene": contribution.scene_path,
                                "sha256": hashlib.sha256((root/contribution.scene_path).read_bytes()).hexdigest()})
        self.assertEqual(45, len(cases))
        receipt = self.probe(cases)
        self.assertEqual(45, receipt["cases"])
        self.assertEqual(3780, receipt["gateChecks"])
        self.assertEqual(1350, receipt["signedChecks"])
        self.assertEqual(7560, receipt["artGateChecks"])
        self.assertEqual(1890, receipt["artStateChecks"])
        self.assertGreater(receipt["autoIndexReads"], 0)
        receipt.update(sourceRoutes=sources, originalTextureSha256=art_sink.sources,
                       boundary="Official LuaJ/loader CPU properties and source image identity only; no GPU draw or texture copies.")
        evidence = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-hud-offset-source-audit"
        evidence.mkdir(parents=True, exist_ok=True)
        (evidence/"source-verification.json").write_text(json.dumps(receipt, indent=2)+"\n", encoding="utf-8")
        print("Native source offset probe: "+json.dumps({k:v for k,v in receipt.items()
                                                        if k not in ('sourceRoutes', 'originalTextureSha256')}, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
