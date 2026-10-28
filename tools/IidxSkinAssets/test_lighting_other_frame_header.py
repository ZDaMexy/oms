"""Synthetic contracts for externally qualified other-Frame headers; no original art."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from PIL import Image, PngImagePlugin

import lighting_other_frame_header as header
import build_lighting_other_frame_header as builder
from build_lighting_other_frame_header import append_resources, copy_originals, read_qualification, resolve_art, verify_draws
from lighting_header import ART_NAMES, DIFFICULTY_TEXTURES, LEVEL_TEXTURES, identifiers, walk


def box(left, top, width, height):
    return dict(zip(("left", "top", "right", "bottom"), (left, top, left + width, top + height)))


def fixture(frame_id="iidx31", layout="p1"):
    scale = 1.5 if frame_id in ("iidx27", "iidx28") else 1
    source_canvases = ((130, 16), (28, 14), (18, 12)) if frame_id == "iidx27" else ((118, 16), (38, 14), (28, 14)) if frame_id == "iidx28" else ((186, 26), (46, 20), (36, 20))
    if frame_id == "iidx27":
        positions = {"p1": ((274.5, 978, 195, 24), (441, 1002, 42, 21), (486, 1003.5, 27, 18)),
                     "p2": ((1455, 978, 195, 24), (1408.5, 1002, 42, 21), (1453.5, 1003.5, 27, 18)),
                     "dp": ((456, 900, 195, 24), (612, 924, 42, 21), (654, 925.5, 27, 18))}[layout]
    elif frame_id == "iidx28":
        positions = ((33, 952.5, 177, 24), (73.5, 979.5, 57, 21), (127.5, 979.5, 42, 21))
    elif layout == "dp":
        positions = ((21, 129, 186, 26), (72, 158, 46, 20), (118, 158, 36, 20))
    else:
        dx = 0 if layout == "p1" else 437
        y = 41 if frame_id == "iidx31" else 40 if frame_id == "iidx32" else 38
        ly = 71 if frame_id in ("iidx31", "iidx32") else 69
        positions = ((649 + dx, y, 186, 26), (699 + dx, ly, 46, 20), (747 + dx, ly, 36, 20))
    geometry = {}
    for role, canvas, coordinates in zip(("difficulty", "label", "number"), source_canvases, positions, strict=True):
        projected = box(*coordinates)
        original = {key: value / scale for key, value in projected.items()}
        geometry[role] = {"sourceCanvas": list(canvas), "originalAffineFullCanvasMatch": True, "negativeTextScale": False,
            "sourceTexture": {"difficulty": "playm_leggendaria" if frame_id == "iidx27" else "playm_hyper", "label": "txt_lv_dot", "number": "lvlnum_12"}[role],
            "sourceBounds": original, "matrix": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": original["left"], "ty": original["top"]},
            "projectedBounds": projected, "appliedBounds": copy.deepcopy(projected)}
    adapted = frame_id in header.BPL and layout == "dp"
    if adapted:
        geometry["label"]["appliedBounds"] = box(250.7, 132, 46, 20)
        geometry["number"]["appliedBounds"] = box(298.7, 132, 36, 20)
    host = {"id": "old.host", "type": "container", "slot": "decoration", "target": {"kind": "global"}, "blend": "alpha",
            "properties": {}, "effects": [], "children": [header.text("old.text", box(10, 10, 100, 20), 20),
                *(header.text("old.value" + str(index), box(10, 40 + index * 25, 100, 20), 20) for index in range(3))]}
    host["children"][0]["properties"]["text"] = "legacy"
    instance = {"id": "old.instance", "template": "old.template", "material": {"slot": "decoration", "resource": "markers/frame-" + layout + "-information"}}
    scene = {"root": {"id": "old.root", "type": "container", "target": {"kind": "global"}, "blend": "alpha", "properties": {}, "effects": [], "children": []},
             "templates": [{"id": "old.template", "root": host}], "instances": [instance],
             "bindings": [{"id": "old.b" + str(index), "target": "old.value" + str(index), "property": "text", "source": source}
                          for index, source in enumerate(("score.value", "scroll.speed", "timing.bpm"))],
             "variants": [], "tracks": [{"id": "old.track", "type": "tween", "target": "old.text", "property": "opacity", "clock": "timing.progress", "easing": "linear", "loop": False,
                 "keyframes": [{"id": "old.key", "time": 0, "value": 1}, {"id": "old.end", "time": 1, "value": 0}]}], "stateMachines": []}
    manifest = {"resources": [{"id": "old.r", "type": "texture", "path": "old.png", "filter": "nearest"}],
                "bitmapFonts": [{"id": "old.font", "lineHeight": 20}], "layouts": [{"ruleset": "bms", "modsRect": [0.015, 0.15, 0.18, 0.065]}],
                "options": [{"id": "old.option", "default": "old"}], "animations": [{"id": "old.animation"}]}
    branch = {"layout": layout, "owner": {"template": "old.template", "root": "old.host", "rootProperties": {}, "instances": [copy.deepcopy(instance)]},
              "nativeModsBounds": header.native_mods(manifest), "geometry": geometry, "originalDirect": not adapted,
              "same33": dict.fromkeys(("sourceHeaderGEOExactlyLike33", "nativeModsExactlyLike33", "all18PngsExact33Aliases", "canReuseAlreadyApproved33HeaderGeometryAdaptation"), adapted),
              "qualification": "exact-source" if not adapted else "existing33-author-adaptation"}
    frame = {"id": frame_id, "package": "fixture", "stage": {"width": 1280 if scale == 1.5 else 1920, "height": 720 if scale == 1.5 else 1080, "fps": 60},
             "difficultyKeys": list(header.known_difficulties(frame_id))}
    names = (*header.known_difficulties(frame_id).values(), "txt_lv_dot", *LEVEL_TEXTURES.values())
    sizes = {name: list(source_canvases[0] if name in DIFFICULTY_TEXTURES.values() else source_canvases[1] if name == "txt_lv_dot" else source_canvases[2]) for name in names}
    art = {"mapping": {name: "art.r" + str(index) for index, name in enumerate(names)}, "sizes": sizes,
           "resources": [{"id": "art.r" + str(index), "type": "texture", "path": "art/" + name + ".png", "filter": "nearest"} for index, name in enumerate(names)]}
    return scene, manifest, frame, branch, art


def field(variant, value):
    match = next((case for case in variant["cases"] if case["key"] == value), None)
    return variant["default"] if match is None else match["resource" if variant["property"] == "resource" else "value"]


def catalog_fixture():
    frame_ids = ("iidx27", "iidx28", "iidx29", "iidx30", "iidx31", "iidx32", "iidx33", *sorted(header.BPL), "bpl-supernova-hd")
    choices, replacements = [], {}
    for frame in frame_ids:
        def part(selected_layout, **when):
            pair = ("old/" + frame + "/" + selected_layout + ".scene", "old/" + frame + "/" + selected_layout + ".manifest")
            if (frame, selected_layout) in header.TARGETS: replacements[pair] = tuple(path.replace("old/", "new/") for path in pair)
            return {"when": when, "scene": pair[0], "manifest": pair[1], "ini": "exact-" + str(when) + ".ini"}
        choices.append({"value": frame, "preview": "previews/" + frame + ".png", **part("dp"), "variants": [
            part("p1", layout="p1", **{"graph-visible": True}), part("p1", layout="p1", **{"graph-visible": False}),
            part("p2", layout="p2"), part("dp", **{"judge-panel-visible": True})], "orderedVariants": True})
    return {"modules": [{"id": "frame", "choices": choices}, *({"id": "other-" + str(index), "choices": [{"value": "old", "ini": "unchanged.ini"}]} for index in range(12))],
            "options": ["unchanged"] * 32}, replacements


def source_hook_fixture():
    _, _, frame, branch, _ = fixture()
    hooks, draws = [], []
    for index, (role, family) in enumerate((("difficulty", "diff_"), ("label", "lv_"), ("number", "lvlnum_"))):
        component = branch["geometry"][role]
        component["sourceBounds"]["left"] -= 270; component["sourceBounds"]["right"] -= 270
        component["matrix"]["tx"] -= 270
        draw = {"draw_order": 10 + index * 10, "geometry": "shape-" + role, "texture": component["sourceTexture"], "game_bindings": [family + "1p"],
                "bounds": copy.deepcopy(component["sourceBounds"]), "matrix": copy.deepcopy(component["matrix"]), "asset": {"package": "fixture"},
                "layer_path": [{"depth": -1, "tag_id": None, "instance_name": None}, {"depth": 31 + index * 2, "tag_id": 82 + index, "instance_name": family + "1p"}],
                "mult_color": dict.fromkeys(("r", "g", "b", "a"), 1), "add_color": dict.fromkeys(("r", "g", "b", "a"), 0), "blend": 0}
        draws.append(draw)
        hooks.append({"movie": "source-sp", "geometry": "shape-" + role, "texture": component["sourceTexture"], "bindings": [family + "1p"],
                      "sourceBounds": copy.deepcopy(component["sourceBounds"]), "matrix": copy.deepcopy(component["matrix"]),
                      "sourceCanvasScale": 1, "existingCentreDx": 270, "projectedBounds": copy.deepcopy(component["projectedBounds"]),
                      "sourceDraw": copy.deepcopy(draw), "inactiveSourceDraws": []})
    branch["sourceHooks"] = hooks
    return frame, branch, {"stage": copy.deepcopy(frame["stage"]), "layouts": {"source-sp": {"draws": draws}}}


class OtherFrameHeaderProposalTest(unittest.TestCase):
    def test_exact29_layout_set_and_all_appends_retain_parent_typed_scene_owner_and_manifest(self):
        self.assertEqual(len(header.TARGETS), 29)
        self.assertNotIn(("iidx29", "dp"), header.TARGETS)
        self.assertNotIn(("bpl-supernova-hd", "p1"), header.TARGETS)
        for frame_id, layout in sorted(header.TARGETS):
            with self.subTest(frame=frame_id, layout=layout):
                scene, manifest, frame, branch, art = fixture(frame_id, layout)
                before_scene, before_manifest = copy.deepcopy(scene), copy.deepcopy(manifest)
                updated, receipt = header.append_branch(scene, manifest, frame, branch, art, identifiers(scene))
                self.assertEqual(header.reverse_branch(updated, receipt), scene)
                self.assertEqual(scene, before_scene); self.assertEqual(manifest, before_manifest)
                self.assertEqual((receipt["addedNodes"], receipt["addedBindings"], receipt["addedVariants"], receipt["additionalReservedTextGlyphs"]), (7, 2, 7, 512))
                self.assertEqual(receipt["addedCases"], 49 if frame_id == "iidx27" else 52)
                self.assertEqual(updated["instances"], scene["instances"])
                root = next(item for item in updated["templates"][0]["root"]["children"] if item["id"] == receipt["root"])
                self.assertTrue(all("slot" not in node and node["target"] == {"kind": "global"} for node in walk(root)))
                self.assertEqual(updated["tracks"], scene["tracks"]); self.assertEqual(updated["stateMachines"], scene["stateMachines"])
                new_manifest, added = append_resources(manifest, art)
                del new_manifest["resources"][-len(added):]
                self.assertEqual(new_manifest, manifest)
                for identifier in receipt["addedIds"]: self.assertRegex(identifier, r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")

    def test_exact_literal_known_unknown_empty_and_unavailable_never_fabricate_original_values(self):
        scene, manifest, frame, branch, art = fixture()
        updated, receipt = header.append_branch(scene, manifest, frame, branch, art, identifiers(scene))
        prefix = receipt["root"]
        variants = {item["id"].removeprefix(prefix + "."): item for item in updated["variants"]}
        for value in (None, "", "INSANE", "normal", " NORMAL", "CUSTOM"):
            self.assertFalse(field(variants["dv"], value)); self.assertTrue(field(variants["dfv"], value))
        for value in DIFFICULTY_TEXTURES:
            self.assertTrue(field(variants["dv"], value)); self.assertFalse(field(variants["dfv"], value))
            self.assertEqual(field(variants["dr"], value), art["mapping"][DIFFICULTY_TEXTURES[value]])
        for value in (None, "", "0", "13", "01", "1.0", "12.5", " 12"):
            self.assertFalse(field(variants["lvv"], value)); self.assertTrue(field(variants["lfv"], value))
        for value in map(str, range(1, 13)):
            self.assertTrue(field(variants["lvv"], value)); self.assertFalse(field(variants["lfv"], value))
        self.assertFalse(field(variants["bms"], "mania")); self.assertTrue(field(variants["bms"], "bms"))
        texts = [node for node in walk(updated["templates"][0]["root"]) if node["id"] in (prefix + ".df", prefix + ".lf")]
        self.assertTrue(all(node["properties"]["text"] == "" and "bitmap-font" not in node["properties"] for node in texts))

    def test_27_beginner_remains_raw_text_and_sd_own_geometry_spacing_and_upright_p2_are_retained(self):
        for frame_id, layout in (("iidx27", "p1"), ("iidx27", "p2"), ("iidx27", "dp"), ("iidx28", "dp")):
            scene, manifest, frame, branch, art = fixture(frame_id, layout)
            updated, receipt = header.append_branch(scene, manifest, frame, branch, art, identifiers(scene))
            self.assertEqual(receipt["bounds"]["difficulty"], branch["geometry"]["difficulty"]["projectedBounds"])
            nodes = {node["id"]: node for node in walk(updated["templates"][0]["root"])}
            difficulty = nodes[receipt["root"] + ".d"]
            self.assertAlmostEqual(difficulty["properties"]["width"] * 1920, 195 if frame_id == "iidx27" else 177, delta=1e-4)
            self.assertNotIn("scale-x", difficulty["properties"])
            self.assertFalse(any(item["target"] == difficulty["id"] for item in updated["tracks"]))
            if frame_id == "iidx27":
                self.assertNotIn("playm_beginner", art["mapping"])
                visibility = next(item for item in updated["variants"] if item["id"] == receipt["root"] + ".dv")
                self.assertFalse(field(visibility, "BEGINNER"))
                self.assertEqual(receipt["bounds"]["number"]["top"] - receipt["bounds"]["label"]["top"], 1.5)
            else:
                self.assertEqual(receipt["bounds"]["number"]["left"] - receipt["bounds"]["label"]["right"], -3)

    def test_seven_bpl_dp_groups_reuse_only_the_existing_alias_adaptation_without_changing_mods(self):
        for frame_id in sorted(header.BPL):
            scene, manifest, frame, branch, art = fixture(frame_id, "dp")
            saved = copy.deepcopy(manifest)
            updated, receipt = header.append_branch(scene, manifest, frame, branch, art, identifiers(scene))
            self.assertEqual(manifest, saved)
            self.assertEqual(receipt["bounds"]["difficulty"], box(21, 129, 186, 26))
            self.assertEqual(receipt["bounds"]["label"], box(250.7, 132, 46, 20))
            self.assertEqual(receipt["bounds"]["number"], box(298.7, 132, 36, 20))
            self.assertEqual(receipt["bounds"]["number"]["left"] - receipt["bounds"]["label"]["right"], 2)
        for frame_id, expected in (("iidx31", 41), ("iidx32", 40), ("bpl-apina", 38)):
            scene, manifest, frame, branch, art = fixture(frame_id, "p1")
            _, receipt = header.append_branch(scene, manifest, frame, branch, art, identifiers(scene))
            self.assertEqual(receipt["bounds"]["difficulty"]["top"], expected)

    def test_unqualified_shape_owner_mode_existing_consumers_and_collisions_are_rejected(self):
        for change in ("layout", "canvas", "mirror", "mods", "owner", "pool", "existing", "collision", "move", "27-beginner"):
            with self.subTest(change=change):
                scene, manifest, frame, branch, art = fixture()
                occupied = identifiers(scene)
                if change == "layout": frame["id"] = "iidx29"
                elif change == "canvas": art["sizes"]["playm_beginner"] = [94, 16]
                elif change == "mirror": branch["geometry"]["difficulty"]["negativeTextScale"] = True
                elif change == "mods":
                    manifest["layouts"][0]["modsRect"] = [649 / 1920, 41 / 1080, 186 / 1920, 26 / 1080]
                    branch["nativeModsBounds"] = header.native_mods(manifest)
                elif change == "owner": scene["templates"][0]["root"]["properties"]["x"] = 0.1
                elif change == "pool": scene["instances"].append({**scene["instances"][0], "id": "old.second"})
                elif change == "existing": scene["bindings"].append({"id": "old.header", "target": "old.text", "source": "song.level", "property": "text"})
                elif change == "collision": occupied.add("oh.iidx31.p1.d")
                elif change == "move": branch["geometry"]["difficulty"]["appliedBounds"]["left"] += 1
                else:
                    scene, manifest, frame, branch, art = fixture("iidx27")
                    frame["difficultyKeys"].insert(0, "BEGINNER")
                with self.assertRaises(ValueError): header.append_branch(scene, manifest, frame, branch, art, occupied)

    def test_catalog_reversal_keeps_all13_excluded_layouts_and_every_other_module_ini_condition_and_preview(self):
        catalog, replacements = catalog_fixture()
        before = copy.deepcopy(catalog)
        updated = header.rewrite_catalog(catalog, replacements)
        self.assertEqual(catalog, before)
        self.assertEqual(updated["modules"][1:], before["modules"][1:])
        excluded = 0
        touched = set()
        for old, new in zip(catalog["modules"][0]["choices"], updated["modules"][0]["choices"], strict=True):
            # A choice is also the default contribution, but its variants are
            # separate contributions and may have legitimately replaced paths.
            old_default = {key: value for key, value in old.items() if key != "variants"}
            new_default = {key: value for key, value in new.items() if key != "variants"}
            for old_part, new_part in zip((old_default, *old["variants"]), (new_default, *new["variants"]), strict=True):
                pair = old_part["scene"], old_part["manifest"]
                if pair not in replacements:
                    self.assertEqual(new_part, old_part)
                else:
                    touched.add(pair)
                    self.assertEqual((new_part["scene"], new_part["manifest"]), replacements[pair])
                    self.assertEqual({k: v for k, v in new_part.items() if k not in ("scene", "manifest")},
                                     {k: v for k, v in old_part.items() if k not in ("scene", "manifest")})
            if old["value"] != "iidx33": excluded += 3 - len(header.TARGET_LAYOUTS.get(old["value"], ()))
        self.assertEqual(excluded, 13)
        self.assertEqual(touched, set(replacements))
        bad = copy.deepcopy(catalog)
        bad["modules"][1]["choices"][0].update(scene=next(iter(replacements))[0], manifest=next(iter(replacements))[1])
        with self.assertRaises(ValueError): header.rewrite_catalog(bad, replacements)

    def test_private_qualification_set_and_exact_identity_reject_missing_duplicate_or_changed_input(self):
        frames = [{"id": frame, "branches": [{"layout": layout} for layout in layouts]} for frame, layouts in header.TARGET_LAYOUTS.items()]
        all_frames = {"iidx27", "iidx28", "iidx29", "iidx30", "iidx31", "iidx32", "bpl-supernova-hd", *header.BPL}
        excluded = [{"frame": frame, "layout": layout} for frame in all_frames for layout in ("p1", "p2", "dp") if (frame, layout) not in header.TARGETS]
        document = {"schemaVersion": 1, "frames": frames, "excludedBranches": excluded, "baselineFrameFiles": [{}] * 84}
        with tempfile.TemporaryDirectory() as temporary:
            path = Path(temporary) / "qualification.json"
            path.write_text(json.dumps(document), encoding="utf-8")
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            self.assertEqual(read_qualification(path, digest), document)
            with self.assertRaises(ValueError): read_qualification(path, "0" * 64)
            document["frames"][0]["branches"].pop()
            path.write_text(json.dumps(document), encoding="utf-8")
            with self.assertRaises(ValueError): read_qualification(path, hashlib.sha256(path.read_bytes()).hexdigest())
            path.write_text('{"schemaVersion":1,"schemaVersion":1}', encoding="utf-8")
            with self.assertRaises(ValueError): read_qualification(path, hashlib.sha256(path.read_bytes()).hexdigest())

    def test_real_upgrade_rejects_a_different_qualification_parent_even_when_command_pin_matches_actual_parent(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); source = root / "parent"; source.mkdir()
            (source / "sentinel.bin").write_bytes(b"synthetic immutable parent")
            actual_pin = builder.identity(builder.inventory(source, builder.all_files(source)))
            different_pin = hashlib.sha256(b"different qualified parent").hexdigest()
            self.assertNotEqual(actual_pin, different_pin)
            frames = [{"id": frame, "branches": [{"layout": layout} for layout in layouts]} for frame, layouts in header.TARGET_LAYOUTS.items()]
            all_frames = {"iidx27", "iidx28", "iidx29", "iidx30", "iidx31", "iidx32", "bpl-supernova-hd", *header.BPL}
            document = {"schemaVersion": 1, "frames": frames,
                "excludedBranches": [{"frame": frame, "layout": layout} for frame in all_frames for layout in ("p1", "p2", "dp") if (frame, layout) not in header.TARGETS],
                "baselineFrameFiles": [{}] * 84, "parent": {"path": str(source), "fullPayloadSha256": different_pin}}
            path = root / "qualification.json"; path.write_text(json.dumps(document), encoding="utf-8")
            digest = hashlib.sha256(path.read_bytes()).hexdigest()
            before = {item.relative_to(root).as_posix(): item.read_bytes() for item in root.rglob("*") if item.is_file()}
            with self.assertRaisesRegex(ValueError, "qualification parent"):
                builder.upgrade(source, root / "fresh", path, root / "evidence", expected_source_sha256=actual_pin, qualification_sha256=digest)
            self.assertEqual({item.relative_to(root).as_posix(): item.read_bytes() for item in root.rglob("*") if item.is_file()}, before)
            self.assertEqual({item.name for item in root.iterdir()}, {"parent", "qualification.json"})

    def test_real_upgrade_rejects_existing_output_evidence_or_archive_before_reading_qualification_and_writes_nothing(self):
        for case in ("output", "evidence", "parent-as-output", "archive"):
            with self.subTest(case=case), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary); source = root / "parent"; source.mkdir()
                output, evidence = root / "fresh", root / "evidence"
                if case == "parent-as-output": output = source
                elif case == "archive": output.with_suffix(".osk").write_bytes(b"existing delivery")
                else: (output if case == "output" else evidence).mkdir()
                protected = evidence if case == "evidence" else output
                if protected.exists(): (protected / "sentinel.bin").write_bytes(b"must remain unchanged")
                path = root / "unread-qualification.json"; path.write_text("not JSON", encoding="utf-8")
                before = {item.relative_to(root).as_posix(): item.read_bytes() for item in root.rglob("*") if item.is_file()}
                names = {item.relative_to(root).as_posix() for item in root.rglob("*")}
                with self.assertRaisesRegex(ValueError, "strictly fresh sibling"):
                    builder.upgrade(source, output, path, evidence, expected_source_sha256="0" * 64, qualification_sha256="0" * 64)
                self.assertEqual({item.relative_to(root).as_posix(): item.read_bytes() for item in root.rglob("*") if item.is_file()}, before)
                self.assertEqual({item.relative_to(root).as_posix() for item in root.rglob("*")}, names)

    def test_original_pngs_are_byte_preserved_and_hd_reuses_actual_h33_ids_with_no_encoding(self):
        with tempfile.TemporaryDirectory() as temporary:
            for hd in (False, True):
                root = Path(temporary) / str(hd); root.mkdir()
                library, source, output = root / "library", root / "parent", root / "output"
                library.mkdir(); source.mkdir(); output.mkdir()
                frame = {"id": "iidx31" if hd else "iidx28", "package": "fixture", "art": []}
                library_rows, parent_rows, shared = {}, [], []
                for index, name in enumerate(ART_NAMES):
                    size = ((186, 26) if name in DIFFICULTY_TEXTURES.values() else (46, 20) if name == "txt_lv_dot" else (36, 20)) if hd else ((118, 16) if name in DIFFICULTY_TEXTURES.values() else (38, 14) if name == "txt_lv_dot" else (28, 14))
                    path = library / (name + ".png")
                    info = PngImagePlugin.PngInfo(); info.add_text("fixture", name)
                    Image.new("RGBA", size, (12, 34, 56, 0)).save(path, format="PNG", pnginfo=info)
                    digest = hashlib.sha256(path.read_bytes()).hexdigest()
                    aliases = []
                    if hd:
                        target = source / (name + ".png"); target.write_bytes(path.read_bytes())
                        resource = {"id": "h33.r" + str(index), "type": "texture", "path": target.name, "filter": "nearest"}
                        shared.append(resource); aliases = [resource]
                        parent_rows.append({"path": target.name, "sha256": digest})
                    frame["art"].append({"name": name, "source": {"path": str(path), "sha256": digest, "width": size[0], "height": size[1]}, "existing33Resources": aliases})
                    library_rows[path.name] = {"package": "fixture.ifs", "sha256": digest, "width": size[0], "height": size[1], "sampler": {"min": "nearest", "mag": "nearest"}}
                with mock.patch.object(Image.Image, "save", side_effect=AssertionError("Do not re-encode originals")):
                    art = resolve_art(frame, library, library_rows, source, parent_rows, shared)
                    copy_originals(art["copies"], output)
                self.assertEqual(len(art["resources"]), 18)
                self.assertEqual(len(art["copies"]), 0 if hd else 18)
                self.assertEqual(all(row["reused33"] for row in art["proofs"]), hd)
                for row in art["copies"]:
                    self.assertEqual((output / row["path"]).read_bytes(), Path(row["source"]).read_bytes())
                Path(frame["art"][0]["source"]["path"]).write_bytes(b"changed")
                with self.assertRaises(ValueError): resolve_art(frame, library, library_rows, source, parent_rows, shared)

    def test_actual_hook_shape_keeps_its_source_translation_and_rejects_changed_geo_tint_or_stage(self):
        frame, branch, mapping = source_hook_fixture()
        verify_draws(frame, branch, mapping)
        for change in ("geo", "alpha", "translation", "stage"):
            with self.subTest(change=change):
                changed_branch, changed_mapping = copy.deepcopy(branch), copy.deepcopy(mapping)
                if change == "geo": changed_mapping["layouts"]["source-sp"]["draws"][0]["bounds"]["left"] += 1
                elif change == "alpha": changed_mapping["layouts"]["source-sp"]["draws"][0]["mult_color"]["a"] = 0.5
                elif change == "translation": changed_branch["sourceHooks"][0]["existingCentreDx"] = 271
                else: changed_mapping["stage"]["width"] = 1280
                with self.assertRaises(ValueError): verify_draws(frame, changed_branch, changed_mapping)

    def test_exact_white_main_instance_is_not_confused_with_its_alpha_zero_entry_effect_neighbour(self):
        frame, branch, mapping = source_hook_fixture()
        neighbour = copy.deepcopy(mapping["layouts"]["source-sp"]["draws"][0])
        neighbour["draw_order"] += 1; neighbour["layer_path"][1]["depth"] += 1
        neighbour["blend"] = 8; neighbour["mult_color"]["a"] = 0
        mapping["layouts"]["source-sp"]["draws"].insert(1, neighbour)
        branch["sourceHooks"][0]["inactiveSourceDraws"] = [copy.deepcopy(neighbour)]
        before_branch, before_map = copy.deepcopy(branch), copy.deepcopy(mapping)
        verify_draws(frame, branch, mapping)
        self.assertEqual(branch, before_branch); self.assertEqual(mapping, before_map)

    def test_exact_source_draw_identity_colours_geometry_and_all_inactive_neighbours_still_fail_closed(self):
        for change in ("order", "layer", "main-colour", "declared-main-colour", "declared-main-alpha", "declared-main-blend", "bounds", "matrix", "package", "neighbour-alpha", "declared-neighbour-alpha", "declared-neighbour-colour", "extra", "missing-neighbour", "choose-neighbour", "duplicate-role"):
            with self.subTest(change=change):
                frame, branch, mapping = source_hook_fixture()
                draws = mapping["layouts"]["source-sp"]["draws"]
                main = draws[0]
                neighbour = copy.deepcopy(main)
                neighbour["draw_order"] += 1; neighbour["layer_path"][1]["depth"] += 1
                neighbour["blend"] = 8; neighbour["mult_color"]["a"] = 0
                draws.insert(1, neighbour)
                hook = branch["sourceHooks"][0]
                hook["inactiveSourceDraws"] = [copy.deepcopy(neighbour)]
                if change == "order": hook["sourceDraw"]["draw_order"] += 100
                elif change == "layer": hook["sourceDraw"]["layer_path"][1]["depth"] += 1
                elif change == "main-colour": main["mult_color"]["r"] = 0.5
                elif change == "declared-main-colour":
                    main["mult_color"]["g"] = 0.5; hook["sourceDraw"] = copy.deepcopy(main)
                elif change == "declared-main-alpha":
                    main["mult_color"]["a"] = 0.5; hook["sourceDraw"] = copy.deepcopy(main)
                elif change == "declared-main-blend":
                    main["blend"] = 8; hook["sourceDraw"] = copy.deepcopy(main)
                elif change == "bounds":
                    main["bounds"]["left"] += 1; hook["sourceDraw"] = copy.deepcopy(main)
                elif change == "matrix":
                    main["matrix"]["tx"] += 1; hook["sourceDraw"] = copy.deepcopy(main)
                elif change == "package":
                    main["asset"]["package"] = "another"; hook["sourceDraw"] = copy.deepcopy(main)
                elif change == "neighbour-alpha": neighbour["mult_color"]["a"] = 0.25
                elif change == "declared-neighbour-alpha":
                    neighbour["mult_color"]["a"] = 0.25; hook["inactiveSourceDraws"] = [copy.deepcopy(neighbour)]
                elif change == "declared-neighbour-colour":
                    neighbour["mult_color"]["r"] = 0.5; hook["inactiveSourceDraws"] = [copy.deepcopy(neighbour)]
                elif change == "extra": draws.insert(2, {**copy.deepcopy(main), "draw_order": 12})
                elif change == "missing-neighbour": hook["inactiveSourceDraws"] = []
                elif change == "choose-neighbour":
                    hook["sourceDraw"] = copy.deepcopy(neighbour); hook["inactiveSourceDraws"] = [copy.deepcopy(main)]
                else: branch["sourceHooks"].append(copy.deepcopy(hook))
                with self.assertRaises(ValueError): verify_draws(frame, branch, mapping)


if __name__ == "__main__":
    unittest.main()
