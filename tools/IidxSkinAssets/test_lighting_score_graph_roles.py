"""True Current/PB/Target role routing on synthetic C2 authors and PNGs."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

from PIL import Image

import build_lighting_score_graph_roles as builder
import lighting_pacemaker_targets as targets
import lighting_score_graph_roles as roles
import test_lighting_pacemaker_targets as graph_tests
from test_lighting_gameplay_controls import visible_text_sources


def fixture():
    source, manifest = graph_tests.TargetCatalogPublicationTest.fixture()
    base, _, _, contributions, _ = targets.split_target_module(source, manifest)
    resources = {role: {kind: "synthetic." + role + "." + kind for kind in ("fill", "frame")} for role in roles.ROLES}
    # These are intentionally synthetic anchors, not inferred cabinet geometry.
    geometry = {role: {"fill": [100 + index * 100, 800, 60, 600], "frame": [98 + index * 100, 199, 64, 602]}
                for index, role in enumerate(roles.ROLES)}
    return base, contributions, resources, geometry


def visible_sprites(scene, values):
    variants = {item["target"]: item for item in scene["variants"] if item["property"] == "visible"}
    bindings = {(item["target"], item["property"]): item["source"] for item in scene["bindings"]}
    result = []
    def visit(node):
        shown = node["properties"].get("visible", True)
        if node["id"] in variants:
            variant = variants[node["id"]]
            key = str(values.get(variant["source"], "")).lower()
            shown = next((case["value"] for case in variant["cases"] if case["key"] == key), variant["default"])
        if (node["id"], "visible") in bindings:
            shown = values[bindings[node["id"], "visible"]]
        if not shown:
            return
        if node["type"] == "sprite":
            result.append(node)
        for child in node.get("children", []):
            visit(child)
    for tree in (scene["root"], *(item["root"] for item in scene["templates"])):
        visit(tree)
    return result


class ScoreGraphRolesTest(unittest.TestCase):
    def test_real_pb_is_independent_while_all_existing_primary_sub_no_data_readouts_are_preserved(self):
        base, contributions, resources, geometry = fixture()
        updated_base, updated, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        for primary in roles.TARGETS:
            before = graph_tests.TargetCatalogPublicationTest.combine(base, contributions[primary]["scene"])
            after = graph_tests.TargetCatalogPublicationTest.combine(updated_base, updated[primary]["scene"])
            for fallback in ("none", *roles.TARGETS):
                for pb in (False, True):
                    for rank in (False, True):
                        for maximum in (False, True):
                            values = {"option.graph-target": primary, "option.graph-sub-target": fallback,
                                      "option.graph-visible": True, "score.maximum-available": maximum,
                                      "score.personal-best-available": pb, "score.rank-available": rank,
                                      targets.AVAILABLE_SOURCE: maximum and pb and rank, "layout.ruleset": "bms"}
                            with self.subTest(primary=primary, fallback=fallback, pb=pb, rank=rank, maximum=maximum):
                                self.assertCountEqual(visible_text_sources(after, values), visible_text_sources(before, values))
                                sprites = visible_sprites(after, values)
                                self.assertEqual(sum(node["resource"] == resources["personal-best"]["fill"] for node in sprites), int(pb and maximum))
                                def available(target):
                                    return maximum and target != "none" and (pb if target == "personal-best" else rank
                                        if target in ("aaa", "aa", "a") else pb and rank if target == "next" else True)
                                effective = primary if available(primary) else fallback if available(fallback) else None
                                self.assertEqual(sum(node["resource"] == resources["current"]["fill"] for node in sprites), int(maximum))
                                self.assertEqual(sum(node["resource"] == resources["target"]["fill"] for node in sprites),
                                                 int(effective is not None and effective != "personal-best"))
                                self.assertEqual(sum(node["resource"] == resources["target"]["frame"] for node in sprites),
                                                 int(effective is not None and effective != "personal-best"))

    def test_personal_best_target_never_duplicates_the_independent_pb_bar(self):
        base, contributions, resources, geometry = fixture()
        base, contributions, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        scene = graph_tests.TargetCatalogPublicationTest.combine(base, contributions["personal-best"]["scene"])
        values = {"option.graph-sub-target": "maximum", "option.graph-visible": True,
                  "score.maximum-available": True, "score.personal-best-available": True,
                  "score.rank-available": True, "layout.ruleset": "bms"}
        fills = {node["resource"] for node in visible_sprites(scene, values)}
        self.assertIn(resources["current"]["fill"], fills)
        self.assertIn(resources["personal-best"]["fill"], fills)
        self.assertNotIn(resources["target"]["fill"], fills)
        self.assertIn(("MY BEST", None), visible_text_sources(scene, values))

    def test_missing_primary_sub_pb_uses_the_one_genuine_pb_and_retains_sub_readout(self):
        base, contributions, resources, geometry = fixture()
        base, contributions, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        scene = graph_tests.TargetCatalogPublicationTest.combine(base, contributions["aaa"]["scene"])
        values = {"option.graph-sub-target": "personal-best", "option.graph-visible": True,
                  "score.maximum-available": True, "score.personal-best-available": True,
                  "score.rank-available": False, "layout.ruleset": "mania"}
        sprites = visible_sprites(scene, values)
        self.assertEqual(sum(node["resource"] == resources["personal-best"]["fill"] for node in sprites), 1)
        self.assertFalse(any(node["resource"] == resources["target"]["fill"] for node in sprites))
        self.assertIn(("SUB MY BEST", None), visible_text_sources(scene, values))

    def test_graph_off_hides_all_three_roles_and_every_readout(self):
        base, contributions, resources, geometry = fixture()
        base, contributions, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        for choice in roles.TARGETS:
            scene = graph_tests.TargetCatalogPublicationTest.combine(base, contributions[choice]["scene"])
            values = {"option.graph-sub-target": "maximum", "option.graph-visible": False,
                      "score.maximum-available": True, "score.personal-best-available": True,
                      "score.rank-available": True, targets.AVAILABLE_SOURCE: True, "layout.ruleset": "bms"}
            self.assertEqual(visible_sprites(scene, values), [])
            self.assertEqual(visible_text_sources(scene, values), [])

    def test_no_data_does_not_erase_a_valid_independent_pb(self):
        base, contributions, resources, geometry = fixture()
        base, contributions, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        scene = graph_tests.TargetCatalogPublicationTest.combine(base, contributions["aaa"]["scene"])
        values = {"option.graph-sub-target": "none", "option.graph-visible": True,
                  "score.maximum-available": True, "score.personal-best-available": True,
                  "score.rank-available": False, "layout.ruleset": "mania"}
        self.assertTrue(any(text == "NO DATA" for text, _ in visible_text_sources(scene, values)))
        self.assertTrue(any(node["resource"] == resources["personal-best"]["fill"] for node in visible_sprites(scene, values)))

    def test_inputs_manifests_layout_owners_and_target_value_programs_remain_unchanged(self):
        base, contributions, resources, geometry = fixture()
        saved = copy.deepcopy((base, contributions, resources, geometry))
        updated_base, updated, receipt = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        self.assertEqual((base, contributions, resources, geometry), saved)
        self.assertEqual(updated_base["instances"], base["instances"])
        self.assertEqual(updated_base["tracks"], base["tracks"])
        for choice in roles.TARGETS:
            self.assertEqual(updated[choice]["manifest"], contributions[choice]["manifest"])
            for field in ("instances", "tracks", "variants", "stateMachines"):
                self.assertEqual(updated[choice]["scene"][field], contributions[choice]["scene"][field])
            self.assertEqual([x for x in updated[choice]["scene"]["bindings"] if x["property"] == "text"],
                             [x for x in contributions[choice]["scene"]["bindings"] if x["property"] == "text"])
        self.assertEqual((receipt["optionsAdded"], receipt["fontsAdded"]), (0, 0))
        by_id = {node["id"]: node for node in roles._nodes(updated_base)}
        fill = by_id["sgr.pb.fill"]["properties"]
        self.assertEqual([fill[key] for key in ("x", "y", "width", "height")],
                         [value / roles.CANVAS[index % 2] for index, value in enumerate(geometry["personal-best"]["fill"])])
        self.assertEqual(fill["origin"], "bottom-left")
        self.assertNotIn(receipt["retiredSharedTargetFrame"], by_id)
        for choice in roles.TARGETS:
            outlines = [node for node in roles._nodes(updated[choice]["scene"]) if node.get("resource") == resources["target"]["frame"]]
            self.assertEqual(len(outlines), next(item for item in receipt["choices"] if item["choice"] == choice)["targetBarReadouts"])
            for outline in outlines:
                self.assertEqual([outline["properties"][key] for key in ("x", "y", "width", "height")],
                                 [value / roles.CANVAS[index % 2] for index, value in enumerate(geometry["target"]["frame"])])

    def test_ordinary_percentage_label_is_not_a_second_terminal_score_value(self):
        base, contributions, resources, geometry = fixture()
        scene = contributions["percentage"]["scene"]
        readout, = roles._readouts(scene)
        label = roles._node("synthetic.percent-label", "text", {"text": ""})
        readout["owner"]["children"].append(label)
        binding = {"id": "synthetic.percent-label.value", "target": label["id"], "property": "text", "source": "option.pacemaker-percent"}
        scene["bindings"].append(binding)
        _, updated, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        self.assertIn(binding, updated["percentage"]["scene"]["bindings"])
        self.assertIn(label, roles._nodes(updated["percentage"]["scene"]))

    def test_missing_terminal_fill_value_or_shared_outline_is_rejected(self):
        for missing in ("fill", "value", "outline"):
            base, contributions, resources, geometry = fixture()
            scene = contributions["maximum"]["scene"]
            readout, = roles._readouts(scene)
            if missing == "outline":
                current = next(b for b in base["bindings"] if b["source"] == "score.progress")
                nodes = roles._nodes(base)
                owner = next(n for n in nodes if any(c["id"] == current["target"] for c in n.get("children", [])))
                outline = next(c for c in owner["children"] if roles._matches_outline(c, readout["fill"]))
                owner["children"].remove(outline)
            elif missing == "fill":
                readout["owner"]["children"].remove(readout["fill"])
            else:
                value_binding = next(b for b in scene["bindings"] if b["source"] == "score.maximum" and b["property"] == "text")
                scene["bindings"].remove(value_binding)
            saved = copy.deepcopy((base, contributions))
            with self.subTest(missing=missing), self.assertRaises(ValueError):
                roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
            self.assertEqual((base, contributions), saved)

    def test_invalid_or_unreviewed_inputs_fail_without_mutating_the_source(self):
        for altered in ("geometry", "resource", "duplicate", "gate", "stateful-pb"):
            base, contributions, resources, geometry = fixture()
            if altered == "geometry":
                geometry["personal-best"]["fill"][2] = float("nan")
            elif altered == "resource":
                resources["target"]["fill"] = "unknown/invalid"
            elif altered == "duplicate":
                base["root"]["children"].append(roles._node("sgr.pb"))
            elif altered == "gate":
                base["bindings"] = [item for item in base["bindings"] if item["source"] != "option.graph-visible"]
            else:
                scene = contributions["personal-best"]["scene"]
                pb = next(x for x in roles._readouts(scene) if x["source"] == "score.personal-best")
                scene["tracks"].append({"id": "synthetic-conflict", "target": pb["fill"]["id"], "property": "opacity"})
            saved = copy.deepcopy((base, contributions))
            with self.subTest(altered=altered), self.assertRaises(ValueError):
                roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
            self.assertEqual((base, contributions), saved)


class OriginalGraphAliasTest(unittest.TestCase):
    @staticmethod
    def assets(root):
        rows = []
        for index, pair in enumerate(roles.ALIASES):
            image = Image.new("RGBA", (90, 690), (index * 60, 20, 30, 255))
            rgba = hashlib.sha256(image.tobytes()).hexdigest()
            for name in pair:
                path = root / name
                image.save(path)
                rows.append({"package": roles.GRAPH_PACKAGE, "name": name, "sprite": name,
                             "width": 90, "height": 690, "sha256": hashlib.sha256(path.read_bytes()).hexdigest(), "rgbaSha256": rgba})
        return rows

    def test_alias_proof_reads_original_pngs_and_pixels_without_writing(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            rows = self.assets(root)
            before = {path.name: path.read_bytes() for path in root.iterdir()}
            result = roles.verify_graph_aliases(root, rows)
            self.assertEqual({path.name: path.read_bytes() for path in root.iterdir()}, before)
            self.assertEqual(len(result["aliases"]), 3)
            self.assertTrue(all(pair["originalPngBytesEqual"] for pair in result["aliases"]))
            self.assertEqual(result["filesWritten"], 0)

    def test_missing_changed_or_misdeclared_original_alias_is_rejected(self):
        for changed in ("missing", "bytes", "pixels", "alias"):
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                rows = self.assets(root)
                if changed == "missing":
                    rows.pop()
                elif changed == "bytes":
                    (root / rows[0]["sprite"]).write_bytes(b"changed")
                elif changed == "pixels":
                    rows[0]["rgbaSha256"] = "0" * 64
                else:
                    row = rows[0]
                    image = Image.new("RGBA", (90, 690), "yellow")
                    image.save(root / row["sprite"])
                    row["sha256"] = hashlib.sha256((root / row["sprite"]).read_bytes()).hexdigest()
                    row["rgbaSha256"] = hashlib.sha256(image.tobytes()).hexdigest()
                with self.assertRaises(ValueError):
                    roles.verify_graph_aliases(root, rows)


class GraphPublicationInputsTest(unittest.TestCase):
    @staticmethod
    def assets(root):
        manifest = {"resources": []}
        reference = {"assets": {}}
        for index, name in enumerate(("blue", "green", "frame")):
            path = root / (name + ".png")
            Image.new("RGBA", (3, 2), (index * 80, 10, 20, 255)).save(path)
            manifest["resources"].append({"id": "fixture." + name, "type": "texture", "path": path.name, "filter": "nearest"})
            reference["assets"][name] = {"sha256": builder.sha(path)}
        return manifest, reference

    def test_reuses_three_existing_images_and_red_declaration_fits_parent_99_byte_margin(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            manifest, reference = self.assets(root)
            saved = copy.deepcopy(manifest)
            updated, resource_map = builder.add_graph_resources(root, manifest, reference)
            self.assertEqual(manifest, saved)
            self.assertEqual(updated["resources"][:-1], manifest["resources"])
            self.assertEqual(updated["resources"][-1], builder.RED_RESOURCE)
            self.assertEqual(resource_map["current"]["fill"], "fixture.blue")
            self.assertEqual(resource_map["personal-best"]["fill"], "fixture.green")
            self.assertEqual(resource_map["target"]["fill"], builder.RED_RESOURCE["id"])
            self.assertEqual({pair["frame"] for pair in resource_map.values()}, {"fixture.frame"})
            encode = lambda value: json.dumps(value, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
            self.assertLessEqual(len(encode(updated)) - len(encode(manifest)), 99)

    def test_changed_original_resource_or_red_path_collision_is_rejected(self):
        for changed in ("missing", "filter", "collision"):
            with self.subTest(changed=changed), tempfile.TemporaryDirectory() as folder:
                root = Path(folder)
                manifest, reference = self.assets(root)
                if changed == "missing":
                    manifest["resources"].pop()
                elif changed == "filter":
                    manifest["resources"][0]["filter"] = "linear"
                else:
                    manifest["resources"].append(copy.deepcopy(builder.RED_RESOURCE))
                with self.assertRaises(ValueError):
                    builder.add_graph_resources(root, manifest, reference)

    def test_target_manifest_closure_includes_red_and_outline_and_prunes_unused_green(self):
        base, contributions, resources, geometry = fixture()
        _, changed, _ = roles.inject_roles(base, contributions, role_resources=resources, geometry=geometry)
        shared = {"resources": [{"id": resources["target"][kind], "type": "texture", "path": "synthetic/" + kind + ".png", "filter": "nearest"}
                                for kind in ("fill", "frame")]}
        updated = builder.close_target_resources(changed, shared)
        for contribution in updated.values():
            manifest = contribution["manifest"]
            required = builder.resource_references(contribution["scene"], manifest["bitmapFonts"])
            self.assertEqual({item["id"] for item in manifest["resources"]}, required)
            self.assertIn(resources["target"]["fill"], required)
            self.assertIn(resources["target"]["frame"], required)
            self.assertNotIn("omni.lighting.gr-sco-gauge-large-green", required)

    def test_frozen_archive_must_match_sha_and_every_source_closure_file(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            files = ["a.json", "b.png"]
            for name in files:
                (root / name).write_bytes(name.encode())
            archive = root / "source.osk"
            with zipfile.ZipFile(archive, "w", zipfile.ZIP_DEFLATED) as package:
                for name in files:
                    package.write(root / name, name)
            digest = builder.sha(archive)
            hashes = {name: builder.sha(root / name) for name in files}
            self.assertEqual(builder.verify_parent_archive(archive, digest, files, hashes), digest)
            with self.assertRaisesRegex(ValueError, "SHA256 changed"):
                builder.verify_parent_archive(archive, "0" * 64, files, hashes)
            hashes[files[0]] = "0" * 64
            with self.assertRaisesRegex(ValueError, "author file differs"):
                builder.verify_parent_archive(archive, digest, files, hashes)


if __name__ == "__main__":
    unittest.main()
