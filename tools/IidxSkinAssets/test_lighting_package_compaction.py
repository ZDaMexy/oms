"""Synthetic reversibility, reference closure and immutable-media regressions."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

import lighting_package_compaction as compact
from build_lighting_package_compaction import NAME, destinations, independent, json_plan, prepare
from build_omni_skin import package_closure
from music_progress import _stable_id


def node(identifier, *, children=None, **values):
    return {"id": identifier, "type": "container", "target": {"kind": "global"}, "blend": "inherit",
            "properties": {}, "effects": [], "children": children or [], **values}


def scene_fixture(prefix="fixture"):
    scene = {"contract": compact.SCENE, "root": node(prefix + ".root", children=[
        node(prefix + ".sprite", type="sprite", resource="public.texture", properties={"width-px": 64, "height-px": 22,
             "uv": [0, 0, 1, 1], "bitmap-font": "public.font", "x": 0},
             effects=[{"id": prefix + ".effect", "type": "colour", "colour": "#ffffffff"}])]),
        "templates": [{"id": prefix + ".template", "root": node(prefix + ".template-root", slot="object.note",
                      target={"kind": "lane", "lane": "scratch"}, children=[node(prefix + ".template-sprite", type="sprite", resource="public.texture")])}],
        "instances": [{"id": prefix + ".instance", "template": prefix + ".template",
                       "target": {"kind": "lane", "lane": "scratch"},
                       "material": {"slot": "object.note", "resource": "markers/note-scratch"}}],
        "tracks": [{"id": prefix + ".track", "target": prefix + ".sprite", "clock": "judgement.hit", "property": "x",
                    "duration": 200, "loop": True, "keyframes": [
                        {"id": prefix + ".keyframe-a", "time": 0, "value": 0.0},
                        {"id": prefix + ".keyframe-b", "time": 200, "value": 1.0}]}],
        "bindings": [{"id": prefix + ".binding", "target": prefix + ".template-sprite", "property": "alpha", "source": "object.opacity"}],
        "variants": [{"id": prefix + ".variant", "target": prefix + ".sprite", "property": "resource",
                      "source": "judgement.timing-direction", "default": "public.texture", "cases": [
                          {"id": prefix + ".case", "key": "fast", "resource": "public.fast"}]}],
        "stateMachines": [{"id": prefix + ".machine", "initial": prefix + ".state-a", "states": [
            {"id": prefix + ".state-a", "set": [{"id": prefix + ".assignment-a", "target": prefix + ".sprite", "property": "alpha", "value": 1}]},
            {"id": prefix + ".state-b", "set": [{"id": prefix + ".assignment-b", "target": prefix + ".sprite", "property": "alpha", "value": 0}]}],
            "transitions": [{"id": prefix + ".transition", "from": prefix + ".state-a", "to": prefix + ".state-b", "on": "judgement.hit"}]}],
        "preparationDuration": 5000, "completionOutro": 2500.0}
    return scene


def manifest_fixture():
    return {"contract": compact.MANIFEST, "scene": "gameplay-skin.scene.json", "sceneContract": compact.SCENE,
            "eventContract": "oms-gameplay-skin-event.v1", "resources": [
                {"id": "public.texture", "type": "texture", "path": "original/long-source-image.png", "filter": "nearest"},
                {"id": "public.fast", "type": "texture", "path": "original/long-fast-image.png", "filter": "linear"}],
            "bitmapFonts": [{"id": "public.font", "lineHeight": 22, "glyphs": [{"character": "0", "resource": "public.texture", "rect": [0, 0, 8, 22]}]}],
            "animations": [{"id": "public.animation", "path": "original/unchanged.clip.json.br", "resources": ["public.texture"]}]}


def catalog_fixture():
    return {"contract": compact.CATALOG, "modules": [{"id": "source-module", "label": "Original Module", "default": "a",
        "keymodes": ["7k", "14k"], "choices": [{"value": "a", "label": "Original A", "manifest": "branch.json", "scene": "branch.scene.json",
        "ini": "branch.ini", "preview": "previews/a.png", "orderedVariants": True,
        "variants": [{"when": {"layout": "p2", "enabled": True}, "manifest": "branch.json", "scene": "branch.scene.json", "ini": "branch.ini"}]}]}]}


def rendering(scene, direction, milliseconds, opacity):
    """Small independent consumer: resolve templates, target programs and resources."""
    roots = [scene["root"], *(template["root"] for template in scene["templates"])]
    nodes = {value["id"]: copy.deepcopy(value) for root in roots for value in compact.walk(root)}
    for variant in scene["variants"]:
        nodes[variant["target"]]["resource"] = next((case["resource"] for case in variant["cases"] if case["key"] == direction), variant["default"])
    for track in scene["tracks"]:
        first, last = track["keyframes"]
        phase = milliseconds % track["duration"] if track["loop"] else min(milliseconds, track["duration"])
        value = first["value"] + (last["value"] - first["value"]) * (phase - first["time"]) / (last["time"] - first["time"])
        nodes[track["target"]]["properties"][track["property"]] = value
    for binding in scene["bindings"]:
        if binding["source"] == "object.opacity":
            nodes[binding["target"]]["properties"][binding["property"]] = opacity
    templates = {value["id"]: value for value in scene["templates"]}
    return {"root": [(nodes[value["id"]].get("resource"), nodes[value["id"]]["properties"])
                     for value in compact.walk(scene["root"])],
            "instances": [(instance["material"], instance["target"], [
                (nodes[value["id"]].get("resource"), nodes[value["id"]]["properties"])
                for value in compact.walk(templates[instance["template"]]["root"])]) for instance in scene["instances"]]}


class TypedIdentityCompactionTest(unittest.TestCase):
    def test_complete_typed_grammar_reverses_without_touching_public_values(self):
        source, manifest, catalog = scene_fixture(), manifest_fixture(), catalog_fixture()
        original = compact.json_bytes(source)
        mapping, receipt = compact.plan_identity_aliases({"scene": source}, {"manifest": manifest}, catalog, [])
        result = compact.rewrite_scene(source, mapping)
        inverse = {new: old for old, new in mapping.items()}
        restored = compact.rewrite_scene(result, inverse)
        self.assertEqual(compact.json_bytes(restored), original)
        self.assertEqual(compact.json_bytes(source), original)
        self.assertEqual(len(mapping), receipt["uniqueDeclarations"])
        self.assertEqual(result["root"]["children"][0]["resource"], "public.texture")
        self.assertEqual(result["root"]["children"][0]["properties"], source["root"]["children"][0]["properties"])
        self.assertEqual(result["instances"][0]["material"], source["instances"][0]["material"])
        self.assertEqual(result["instances"][0]["target"], source["instances"][0]["target"])
        self.assertEqual(result["tracks"][0]["clock"], "judgement.hit")
        self.assertEqual(result["variants"][0]["cases"][0]["key"], "fast")
        self.assertEqual(result["variants"][0]["cases"][0]["resource"], "public.fast")
        self.assertEqual(result["preparationDuration"], 5000)
        self.assertEqual(result["completionOutro"], 2500.0)

    def test_independent_consumer_resolves_same_programs_and_materials(self):
        original = scene_fixture()
        mapping, _ = compact.plan_identity_aliases({"scene": original}, {"manifest": manifest_fixture()}, catalog_fixture(), [])
        result = compact.rewrite_scene(original, mapping)
        for direction in ("none", "fast", "slow"):
            for milliseconds in (0, 47, 199, 201, 521):
                for opacity in (0, .25, 1):
                    self.assertEqual(rendering(original, direction, milliseconds, opacity), rendering(result, direction, milliseconds, opacity))

    def test_shared_old_ids_have_one_alias_and_all_aliases_use_public_validator(self):
        source = scene_fixture()
        manifest = manifest_fixture()
        manifest["resources"].append({"id": "a", "type": "texture", "path": "unchanged.png", "filter": "nearest"})
        mapping, _ = compact.plan_identity_aliases({"one": source, "two": copy.deepcopy(source)}, {"manifest": manifest}, catalog_fixture(), [])
        self.assertEqual(len(set(mapping.values())), len(mapping))
        all_original = {item["id"] for item in compact.identity_slots(source)[0]} | {"a"}
        self.assertFalse(set(mapping.values()) & all_original)
        self.assertTrue(all(len(new) < len(old) for old, new in mapping.items()))
        for value in mapping.values():
            self.assertEqual(_stable_id(value), compact.stable_id(value))
        for invalid in ("td.0.keys", "td.p0.0", "a-", "-a", "A", "a..b", "a" * 129):
            with self.assertRaises(ValueError):
                compact.stable_id(invalid)
            with self.assertRaises(ValueError):
                _stable_id(invalid)

    def test_literal_external_identity_pins_node_instead_of_rewriting_text(self):
        source = scene_fixture()
        source["root"]["properties"]["text"] = "fixture.sprite"
        mapping, receipt = compact.plan_identity_aliases({"scene": source}, {"manifest": manifest_fixture()}, catalog_fixture(), [])
        self.assertNotIn("fixture.sprite", mapping)
        self.assertIn("fixture.sprite", receipt["externallyPinnedIdentities"])
        changed = compact.rewrite_scene(source, mapping)
        self.assertEqual(changed["root"]["properties"]["text"], "fixture.sprite")
        self.assertEqual(changed["tracks"][0]["target"], "fixture.sprite")
        with self.assertRaisesRegex(ValueError, "outside the typed"):
            compact.rewrite_scene(source, {"fixture.sprite": "z"})

    def test_unreviewed_scripts_duplicate_ids_and_dangling_references_fail(self):
        source = scene_fixture()
        cases = []
        broken = copy.deepcopy(source)
        broken["root"]["children"][0]["id"] = broken["root"]["id"]
        cases.append(broken)
        broken = copy.deepcopy(source)
        broken["tracks"][0]["target"] = "missing.target"
        cases.append(broken)
        broken = copy.deepcopy(source)
        broken["unknownExternalProgram"] = {"target": "fixture.root"}
        cases.append(broken)
        for broken in cases:
            with self.assertRaises(ValueError):
                compact.plan_identity_aliases({"scene": broken}, {"manifest": manifest_fixture()}, catalog_fixture(), [])
        manifest = manifest_fixture()
        manifest["script"] = "external.json"
        with self.assertRaisesRegex(ValueError, "script-free"):
            compact.plan_identity_aliases({"scene": source}, {"manifest": manifest}, catalog_fixture(), [])
        with self.assertRaisesRegex(ValueError, "injective"):
            compact.rewrite_scene(source, {"fixture.root": "z", "fixture.sprite": "z"})


class MediaAndCatalogCompactionTest(unittest.TestCase):
    def test_texture_paths_preserve_ini_preview_and_c4_selectors(self):
        manifest, scene, catalog = manifest_fixture(), scene_fixture(), catalog_fixture()
        extra = ["previews/a.png", "markers/original.png", "custom/markers/original.png", "special/selector.png", "long/path/ini.png", "another/source.webp"]
        manifest["resources"].extend({"id": f"resource.r{index}", "type": "texture", "path": path, "filter": "nearest"} for index, path in enumerate(extra))
        scene["instances"][0]["material"]["resource"] = "special/selector"
        closure = [resource["path"] for resource in manifest["resources"]] + ["w/0.png"]
        mapping, receipt = compact.plan_texture_aliases({"m": manifest}, {"s": scene}, catalog,
                                                       ['Texture: LONG\\PATH\\INI\nProvide "markers/original"'], closure)
        for protected in extra[:5]:
            self.assertNotIn(protected, mapping)
            self.assertIn(protected, receipt["excludedPaths"])
        self.assertIn("another/source.webp", mapping)
        self.assertTrue(mapping["another/source.webp"].endswith(".webp"))
        self.assertNotIn("w/0.png", mapping.values())
        original = compact.json_bytes(manifest)
        transformed = compact.rewrite_manifest(manifest, mapping)
        self.assertEqual(compact.json_bytes(compact.rewrite_manifest(transformed, {new: old for old, new in mapping.items()})), original)
        self.assertEqual(manifest["animations"], transformed["animations"])
        self.assertEqual(manifest["bitmapFonts"], transformed["bitmapFonts"])

    def test_case_collisions_and_noncanonical_paths_are_rejected(self):
        with self.assertRaisesRegex(ValueError, "case-insensitive"):
            compact.plan_texture_aliases({"m": manifest_fixture()}, {"s": scene_fixture()}, catalog_fixture(), [], ["a.png", "A.png"])
        for path in ("../outside.png", "a/../x.png", "/outside.png", "C:/outside.png", "a//b.png", "a\\b.png"):
            with self.assertRaises(ValueError):
                compact.normal_path(path)

    def test_catalog_order_conditions_defaults_and_saved_choice_ids_restore(self):
        source = catalog_fixture()
        saved = compact.json_bytes(source)
        result = compact.rewrite_catalog(source, {"branch.scene.json": "config/short-scene.json"}, {"branch.json": "config/short-manifest.json"})
        restored = compact.restore_catalog(result, source, {"branch.scene.json": "config/short-scene.json"}, {"branch.json": "config/short-manifest.json"})
        self.assertEqual(compact.json_bytes(restored), saved)
        self.assertEqual(compact.json_bytes(source), saved)
        self.assertEqual(result["modules"][0]["default"], "a")
        self.assertEqual(result["modules"][0]["choices"][0]["variants"][0]["when"], {"layout": "p2", "enabled": True})
        result["modules"][0]["choices"][0]["label"] = "changed"
        with self.assertRaisesRegex(ValueError, "outside configuration paths"):
            compact.restore_catalog(result, source, {"branch.scene.json": "config/short-scene.json"}, {"branch.json": "config/short-manifest.json"})

    def test_projection_uses_real_root_bytes_and_reserves_only_integer_tokens(self):
        catalog, branch = catalog_fixture(), manifest_fixture()
        root = copy.deepcopy(branch)
        root["options"] = [{"id": "enabled", "type": "toggle", "default": True}]
        scenes = {"gameplay-skin.scene.json": scene_fixture("root"), "branch.scene.json": scene_fixture("branch")}
        manifests = {"gameplay-skin.json": root, "branch.json": branch}
        sizes = {path: len(compact.json_bytes(document)) + 7 for path, document in {**scenes, **manifests}.items()}
        result = compact.selected_projection(catalog, manifests, scenes, sizes)
        exclusive = copy.deepcopy(branch)
        exclusive["resources"], exclusive["bitmapFonts"] = [], []
        self.assertEqual(result["selectedUpperBound"]["sceneBytes"], sum(sizes[path] + compact.numeric_reserve(document) for path, document in scenes.items()))
        self.assertEqual(result["selectedUpperBound"]["manifestBytes"], sizes["gameplay-skin.json"] + compact.numeric_reserve(root)
                         + len(compact.json_bytes(exclusive)) + compact.numeric_reserve(exclusive))
        self.assertEqual(compact.numeric_reserve([True, False, 1, 1.0, {"x": -2, "y": 0}]), 6)

    def test_json_store_compacts_all_scenes_and_proves_full_reversal(self):
        source = {"gameplay-skin.scene.json": scene_fixture("root"), "branch.scene.json": scene_fixture("branch")}
        mapping, _ = compact.plan_identity_aliases(source, {"m": manifest_fixture()}, catalog_fixture(), [])
        changed, paths, data, receipts = json_plan(source, mapping, scene=True)
        self.assertEqual(paths["gameplay-skin.scene.json"], "gameplay-skin.scene.json")
        self.assertEqual(paths["branch.scene.json"], "config/s" + hashlib.sha256(data["branch.scene.json"]).hexdigest() + ".json")
        self.assertTrue(all(row["originalCanonicalSha256"] == row["restoredCanonicalSha256"] for row in receipts))
        self.assertEqual(len(changed), len(source))


def write_source_fixture(base):
    source, evidence_parent = base / "authors" / "parent", base / "evidence"
    source.mkdir(parents=True)
    evidence_parent.mkdir()
    root, catalog = manifest_fixture(), catalog_fixture()
    root["contentCatalog"], root["options"] = "gameplay/content.json", [{"id": "enabled", "label": "Enabled", "type": "toggle", "default": True}]
    scene, branch = scene_fixture("root"), scene_fixture("branch")
    documents = {"gameplay-skin.json": root, "gameplay-skin.scene.json": scene, "gameplay/content.json": catalog,
                 "branch.json": manifest_fixture(), "branch.scene.json": branch}
    for relative, document in documents.items():
        path = source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(compact.json_bytes(document))
    raw_files = {"skin.ini": b'\xef\xbb\xbf[General]\r\nName: Immutable fixture\r\nProvide "markers/note-scratch"\r\n',
                 "branch.ini": b'[Mania]\r\nProvide "markers/note-scratch"\r\n',
                 "markers/note-scratch.png": bytes(range(64)) + b'\x00\xff\x00\x00',
                 "previews/a.png": b'preview-exact-bytes\x00\xff\x00\x00',
                 "original/long-source-image.png": b'original-png-byte-stream\x00\xff\x00\x00',
                 "original/long-fast-image.png": b'original-other-png-byte-stream\xff\x00\x00\x00',
                 "original/unchanged.clip.json.br": b'compressed-recorded-clip-kept-opaque'}
    for relative, value in raw_files.items():
        path = source / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(value)
    closure = package_closure(source)
    sizes = {path: (source / path).stat().st_size for path in closure}
    projection = compact.selected_projection(catalog, {"gameplay-skin.json": root, "branch.json": documents["branch.json"]},
                                            {"gameplay-skin.scene.json": scene, "branch.scene.json": branch}, sizes)
    (source / "package-audit.json").write_bytes(compact.json_bytes({"selectedUpperBound": projection["selectedUpperBound"]}))
    with zipfile.ZipFile(source.with_suffix(".osk"), "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for relative in closure:
            archive.write(source / relative, relative)
    digest = hashlib.sha256(source.with_suffix(".osk").read_bytes()).hexdigest()
    (source / "generation.json").write_bytes(compact.json_bytes({"name": NAME, "archive": {"sha256": digest}}))
    return source, base / "authors" / "compact", evidence_parent / "proof", digest


class FreshAuthorPreparationTest(unittest.TestCase):
    def test_preparation_copies_exact_media_and_restores_every_written_json(self):
        with tempfile.TemporaryDirectory() as temporary:
            source, output, evidence, digest = write_source_fixture(Path(temporary))
            originals = {path.relative_to(source).as_posix(): path.read_bytes() for path in source.rglob("*") if path.is_file()}
            result = prepare(source, output, evidence, digest)
            self.assertFalse(result["archiveCreated"])
            self.assertFalse(output.with_suffix(".osk").exists())
            self.assertFalse((output / "package-audit.json").exists())
            self.assertEqual(result["status"], "prepared-current-package-codec-native-gates-pending")
            self.assertEqual(originals, {path.relative_to(source).as_posix(): path.read_bytes() for path in source.rglob("*") if path.is_file()})
            rows = json.loads((evidence / "file-path-sha256-map.json").read_text("utf-8"))
            self.assertEqual(set(package_closure(output)), {row["destination"] for row in rows})
            for row in rows:
                data = (output / row["destination"]).read_bytes()
                self.assertEqual(hashlib.sha256(data).hexdigest(), row["destinationSha256"])
                if row["source"].endswith((".png", ".br", ".ini")):
                    self.assertEqual(data, originals[row["source"]])
            proof = json.loads((evidence / "proof.json").read_text("utf-8"))
            self.assertEqual(proof["preserved"]["choicesBeforeAndAfter"], 1)
            self.assertEqual(proof["preserved"]["orderedContributionsBeforeAndAfter"], 2)
            self.assertTrue(proof["writtenJsonReReadAndReversed"])
            self.assertTrue(proof["preserved"]["allIniPreviewMarkerBytesAndPathsEqual"])
            self.assertLess(result["after"]["sceneBytes"], result["before"]["sceneBytes"])
            self.assertLess(result["after"]["manifestBytes"], result["before"]["manifestBytes"])

    def test_existing_output_and_evidence_are_not_overwritten(self):
        with tempfile.TemporaryDirectory() as temporary:
            source, output, evidence, _ = write_source_fixture(Path(temporary))
            output.mkdir()
            sentinel = output / "sentinel"
            sentinel.write_bytes(b'preserve')
            with self.assertRaisesRegex(ValueError, "new sibling"):
                destinations(source, output, evidence)
            self.assertEqual(sentinel.read_bytes(), b'preserve')
            untouched_output = output.parent / "unused"
            evidence.mkdir()
            with self.assertRaisesRegex(ValueError, "new evidence"):
                destinations(source, untouched_output, evidence)
            self.assertFalse(untouched_output.exists())

    def test_wrong_parent_identity_fails_before_creating_any_output(self):
        with tempfile.TemporaryDirectory() as temporary:
            source, output, evidence, _ = write_source_fixture(Path(temporary))
            with self.assertRaisesRegex(ValueError, "frozen identity"):
                prepare(source, output, evidence, "0" * 64)
            self.assertFalse(output.exists())
            self.assertFalse(evidence.exists())

    def test_protected_named_paths_are_rejected_without_opening_contents(self):
        with tempfile.TemporaryDirectory() as temporary:
            protected = Path(temporary) / "private-data"
            with self.assertRaisesRegex(ValueError, "protected data"):
                independent(protected, exists=False)


if __name__ == "__main__":
    unittest.main()
