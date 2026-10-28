"""Unrun synthetic regressions for the two-node DP song-information correction."""
from __future__ import annotations

import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

import build_lighting_dp_song_information as fix


def box(left, top, right, bottom):
    return dict(zip(("left", "top", "right", "bottom"), (left, top, right, bottom)))


def reference_fixture():
    def draw(texture, bounds, bindings, matrix=None, tags=()):
        return {"texture": texture, "geometry": "center_frame_dp_1p_shape" + ("26" if texture == "usr_l_music" else "21" if texture == "usr_artist_name" else "314"),
                "asset": {"package": "gmframe33", "width": 540 if bindings else 610, "height": 26 if texture == "usr_l_music" else 24 if bindings else 1080},
                "bounds": bounds, "matrix": matrix or {"a": 1, "b": 0, "c": 0, "d": 1, "tx": bounds["left"], "ty": bounds["top"]},
                "game_bindings": bindings, "layer_path": [{"tag_id": tag} for tag in tags],
                "mult_color": dict.fromkeys(("r", "g", "b", "a"), 1), "add_color": dict.fromkeys(("r", "g", "b", "a"), 0), "blend": 0}
    title = draw("usr_l_music", box(15, 56, 555, 82), ["title"], tags=(None, 28, 27, 26))
    artist = draw("usr_artist_name", box(15, 83, 446.993408203125, 102.19970703125), ["artist"],
                  {"a": 0.79998779296875, "b": 0, "c": 0, "d": 0.79998779296875, "tx": 15, "ty": 83}, tags=(None, 23, 22, 21))
    # Wrong same-name unit deliberately comes first, just as in the real map.
    right = draw("unit_main_dp_1p", box(926, 0, 1536, 1080), [], tags=(None, 459, 416, 314))
    left = draw("unit_main_dp_1p", box(384, 0, 994, 1080), [], tags=(None, 459, 336, 314))
    cabinet = draw("unit_vj_area_dp", box(0, 0, 401, 960), [])
    return {"stage": {"width": 1920, "height": 1080, "fps": 60}, "layouts": {fix.MOVIE: {"package": "gmframe33", "draws": [right, title, left, artist, cabinet]}}}


def scene_fixture():
    def node(identifier, kind="container", properties=None, children=None):
        return {"id": identifier, "type": kind, "target": {"kind": "global"}, "blend": "alpha", "properties": properties or {}, "effects": [], "children": children or []}
    title = node("f6a", "text", {"x": 0.31770833, "y": 0.039814815, "width": 0.36458333, "height": 0.0259259259,
        "font-size": 17.066667, "text": "", "alignment": "centre", "text-overflow": "ellipsis", "colour": "#ffffffff"})
    artist = copy.deepcopy(title); artist["id"] = "f6b"
    artist["properties"].update(y=0.06666667, **{"font-size": 12.8})
    host = node("f61", children=[title, artist, node("other", "text", {"text": "preserve", "font-size": 20})]); host["slot"] = "decoration"
    instance = {"id": "ffb", "template": "fee", "material": {"slot": "decoration", "resource": "markers/frame-dp-information"}}
    bindings = [{"id": identifier, "target": target, "property": "text", "source": source}
                for identifier, target, source in (("dzd", "f6a", "song.title"), ("dze", "f6b", "song.artist"))]
    scene = {"root": node("root"), "templates": [{"id": "fee", "root": host}], "instances": [instance], "bindings": bindings,
        "variants": [{"id": "preserved.v", "target": "other", "property": "visible", "source": "layout.ruleset", "default": False,
                      "cases": [{"id": "preserved.case", "key": "bms", "value": True}]}],
        "tracks": [{"id": "preserved.track", "type": "tween", "target": "other", "property": "opacity", "clock": "timing.progress", "loop": False,
                    "easing": "linear", "keyframes": [{"id": "preserved.start", "time": 0, "value": 1}, {"id": "preserved.end", "time": 1, "value": 0}]}], "stateMachines": []}
    owner = {"template": "fee", "root": "f61", "rootProperties": {}, "instances": [instance]}
    consumers = [{"source": binding["source"], "binding": copy.deepcopy(binding), "node": copy.deepcopy(value), "owner": copy.deepcopy(owner)}
                 for binding, value in zip(bindings, (title, artist), strict=True)]
    return scene, consumers


def boundary_fixture(root):
    source, library = root / fix.SOURCE_NAME, root / "library"
    source.mkdir(); library.mkdir()
    reference_path, audit_path = root / "skin-reference-map.json", root / "analysis-r2.json"
    reference = reference_fixture()
    metadata = {}
    def write(path, data):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data if isinstance(data, bytes) else json.dumps(data).encode())
        metadata[str(path)] = hashlib.sha256(path.read_bytes()).hexdigest()
    write(reference_path, reference)
    geometry_rows, tags, saved = [], [], []
    for name, top, middle, shape in (("title", 28, 27, 26), ("artist", 23, 22, 21)):
        draw = next(item for item in reference["layouts"][fix.MOVIE]["draws"] if name in item["game_bindings"])
        geometry_name = draw["geometry"]
        geo = {"name": geometry_name, "bounds": box(0, 0, 540, 26 if name == "title" else 24), "tex_colors": [], "draw_params": [{"region": draw["texture"]}]}
        chain = [{"id": top, "type": "AP2DefineSpriteTag", "tags": [{"type": "AP2PlaceObjectTag", "source_tag_id": middle, "depth": 1}]},
                 {"id": middle, "type": "AP2DefineSpriteTag", "tags": [{"type": "AP2PlaceObjectTag", "source_tag_id": shape, "depth": 1}]},
                 {"id": shape, "type": "AP2ShapeTag", "reference": geometry_name}]
        tags.extend(chain)
        data = f"author-data/contents/data/graphic/gmframe33/geo/{geometry_name}.json"
        raw = f"packages/contents/data/graphic/gmframe33/raw/geo/{geometry_name}"
        write(library / data, geo); write(library / raw, ("synthetic-geo-" + name).encode())
        geometry_rows.append({"name": geometry_name, "data": data, "raw": raw, "sha256": metadata[str(library / raw)]})
        saved.append({"binding": name, "draw": copy.deepcopy(draw), "parsedGeo": copy.deepcopy(geo), "tagChain": copy.deepcopy(chain)})
    left_geometry_name = "center_frame_dp_1p_shape314"
    tags.extend([{"id": 459, "type": "AP2DefineSpriteTag", "tags": [{"type": "AP2PlaceObjectTag", "source_tag_id": 336, "depth": 5}]},
                 {"id": 336, "type": "AP2DefineSpriteTag", "tags": [{"type": "AP2PlaceObjectTag", "source_tag_id": 314, "depth": 2}]},
                 {"id": 314, "type": "AP2ShapeTag", "reference": left_geometry_name}])
    left_data = f"author-data/contents/data/graphic/gmframe33/geo/{left_geometry_name}.json"
    left_raw = f"packages/contents/data/graphic/gmframe33/raw/geo/{left_geometry_name}"
    write(library / left_data, {"bounds": box(0, 0, 610, 1080)})
    write(library / left_raw, b"synthetic-left-geo")
    geometry_rows.append({"name": left_geometry_name, "data": left_data, "raw": left_raw, "sha256": metadata[str(library / left_raw)]})
    movie_data = "author-data/contents/data/graphic/gmframe33/afp/center_frame_dp_1p.json"
    movie_raw = "packages/contents/data/graphic/gmframe33/raw/afp/center_frame_dp_1p"
    write(library / movie_data, {"exported_tags": {fix.MOVIE: 517, "x_sp_frame_dp_left": 336}, "tags": tags})
    write(library / movie_raw, b"synthetic-afp")
    write(library / "author-data/contents/data/graphic/gmframe33/package.json", {
        "movies": [{"name": fix.MOVIE, "data": movie_data, "raw": movie_raw, "sha256": metadata[str(library / movie_raw)]}], "geometry": geometry_rows})
    builder = root / "build_skin.py"
    write(builder, b"original buggy builder provenance")
    audit = {"author": str(source), "inputs": metadata,
        "rootCause": {"builder": {"path": str(builder), "sha256": metadata[str(builder)]}},
        "backgroundBoundary": {"requiredLeftExportTag": 336, "adjacentMainUnitDraw": {"bounds": {"left": 384}}},
        "minimumCandidate": {"title": box(15, 56, 369, 82), "artist": box(15, 83, 369, 102.19970703125)},
        "originalSource": [{"movie": fix.MOVIE, "drawsAndGeo": saved}]}
    audit_path.write_text(json.dumps(audit), encoding="utf-8")
    return source.resolve(), reference_path.resolve(), library.resolve(), audit_path.resolve(), hashlib.sha256(audit_path.read_bytes()).hexdigest(), builder.resolve()


class DpSongInformationTest(unittest.TestCase):
    def test_source_left_layer_not_first_same_name_and_exact_original_anchors_with_declared_author_width(self):
        reference = reference_fixture()
        fields, receipt = fix.dp_song_geometry(reference)
        self.assertEqual(fields, {"title": box(15, 56, 369, 82), "artist": box(15, 83, 369, 102.19970703125)})
        self.assertEqual((receipt["leftExportTag"], receipt["sourceCabinetBoundary"], receipt["appliedWidth"]), (336, 384, 354))
        self.assertIn("OMS author adaptation", receipt["authorAdaptation"])
        reordered = copy.deepcopy(reference); reordered["layouts"][fix.MOVIE]["draws"].reverse()
        self.assertEqual(fix.dp_song_geometry(reordered)[0], fields)

    def test_rejects_wrong_frame_source_origin_alpha_shape_and_the911_right_unit_boundary(self):
        for change in ("package", "origin", "alpha", "canvas", "left-role", "911", "duplicate"):
            with self.subTest(change=change):
                reference = reference_fixture(); movie = reference["layouts"][fix.MOVIE]; draws = movie["draws"]
                if change == "package": movie["package"] = "gmframe_bpl_apina"
                elif change == "origin": draws[1]["matrix"]["tx"] = 16
                elif change == "alpha": draws[1]["mult_color"]["a"] = 0.5
                elif change == "canvas": draws[1]["asset"]["width"] = 354
                elif change == "left-role": draws[2]["layer_path"][2]["tag_id"] = 416
                elif change == "911": draws[2]["bounds"] = box(926, 0, 1536, 1080)
                else: draws.append(copy.deepcopy(draws[1]))
                with self.assertRaises(ValueError): fix.dp_song_geometry(reference)

    def test_exactly_two_property_dictionaries_change_and_all_owners_ids_sources_fonts_programs_reverse(self):
        scene, consumers = scene_fixture(); before = copy.deepcopy(scene)
        fields, _ = fix.dp_song_geometry(reference_fixture())
        updated, receipt = fix.adjust_scene(scene, consumers, fields)
        self.assertEqual(scene, before); self.assertEqual(fix.reverse_scene(updated, receipt), before)
        self.assertEqual({key: value for key, value in updated.items() if key != "templates"}, {key: value for key, value in before.items() if key != "templates"})
        old_nodes = {node["id"]: node for template in before["templates"] for node in fix.walk(template["root"])}
        new_nodes = {node["id"]: node for template in updated["templates"] for node in fix.walk(template["root"])}
        self.assertEqual(old_nodes.keys(), new_nodes.keys())
        for identifier in ("f6a", "f6b"):
            self.assertEqual({key: value for key, value in new_nodes[identifier].items() if key != "properties"}, {key: value for key, value in old_nodes[identifier].items() if key != "properties"})
            self.assertEqual({key: value for key, value in new_nodes[identifier]["properties"].items() if key not in fix.CHANGED_PROPERTIES},
                             {key: value for key, value in old_nodes[identifier]["properties"].items() if key not in fix.CHANGED_PROPERTIES})
        for node, bounds in zip((new_nodes["f6a"], new_nodes["f6b"]), (fields["title"], fields["artist"]), strict=True):
            for prop, axis, wanted in (("x", 1920, bounds["left"]), ("y", 1080, bounds["top"]), ("width", 1920, 354), ("height", 1080, bounds["bottom"] - bounds["top"])):
                self.assertAlmostEqual(node["properties"][prop] * axis, wanted, delta=1e-4)
            self.assertEqual(node["properties"]["alignment"], "left")
        self.assertEqual((receipt["newNodes"], receipt["newFonts"], receipt["additionalReservedTextGlyphs"], receipt["preservedExistingSongTextReservation"]), (0, 0, 0, 512))
        tampered = copy.deepcopy(updated)
        tampered["templates"][0]["root"]["children"][0]["properties"]["font-size"] += 1
        self.assertNotEqual(fix.reverse_scene(tampered, receipt), before)

    def test_pooled_or_transformed_owner_extra_writer_sp_rect_and_second_application_are_rejected(self):
        for change in ("pool", "transform", "writer", "sp", "changed-font", "already"):
            with self.subTest(change=change):
                scene, consumers = scene_fixture(); fields, _ = fix.dp_song_geometry(reference_fixture())
                if change == "pool": scene["instances"].append({**scene["instances"][0], "id": "second"})
                elif change == "transform": scene["templates"][0]["root"]["properties"]["x"] = 0.1
                elif change == "writer": scene["bindings"].append({"id": "writer", "target": "f6a", "source": "timing.progress", "property": "x"})
                elif change == "sp":
                    scene["templates"][0]["root"]["children"][0]["properties"]["x"] = 880 / 1920
                    consumers[0]["node"] = copy.deepcopy(scene["templates"][0]["root"]["children"][0])
                elif change == "changed-font": scene["templates"][0]["root"]["children"][0]["properties"]["font-size"] = 99
                else: scene = fix.adjust_scene(scene, consumers, fields)[0]
                with self.assertRaises(ValueError): fix.adjust_scene(scene, consumers, fields)

    def test_real_receipt_geo_and_left_export_boundary_allow_a_public_builder_fix_but_not_source_tampering(self):
        with tempfile.TemporaryDirectory() as temporary:
            args = boundary_fixture(Path(temporary))
            source, reference, library, audit_path, audit_sha, builder = args
            builder.write_bytes(b"legitimate corrected public builder")
            fields, geometry, audit, metadata = fix.qualified_geometry(source, reference, library, audit_path, audit_sha)
            self.assertEqual(fields["title"], box(15, 56, 369, 82)); self.assertEqual(geometry["leftExportTag"], 336)
            self.assertNotIn(str(builder), metadata)
            self.assertEqual(audit["rootCause"]["builder"]["sha256"], hashlib.sha256(b"original buggy builder provenance").hexdigest())
            for path, digest in metadata.items(): self.assertEqual(hashlib.sha256(Path(path).read_bytes()).hexdigest(), digest)
            geometry_path = library / "author-data/contents/data/graphic/gmframe33/geo/center_frame_dp_1p_shape26.json"
            geometry_path.write_text('{"changed":true}', encoding="utf-8")
            with self.assertRaises(ValueError): fix.qualified_geometry(source, reference, library, audit_path, audit_sha)

    def test_initial911_report_is_rejected_before_any_source_or_output_is_used(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary); path = root / "analysis.json"
            path.write_text(json.dumps({"backgroundBoundary": {"requiredLeftExportTag": 336, "adjacentMainUnitDraw": {"bounds": {"left": 384}}},
                "minimumCandidate": {"title": {"right": 911}, "artist": {"right": 911}}}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "911"):
                fix.qualified_geometry(root / "unused", root / "missing-map", root / "missing-library", path, hashlib.sha256(path.read_bytes()).hexdigest())
            self.assertEqual(sorted(child.name for child in root.iterdir()), ["analysis.json"])

    def test_fresh_sibling_and_complete_parent_pin_fail_before_output_creation(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            source, reference, library, audit, audit_sha, _ = boundary_fixture(root)
            output, evidence = root / fix.OUTPUT_NAME, root / "evidence"
            with self.assertRaisesRegex(ValueError, "parent payload"):
                fix.upgrade(source, reference, library, audit, output, evidence, expected_source_sha256="0" * 64, audit_sha256=audit_sha)
            self.assertFalse(output.exists()); self.assertFalse(evidence.exists())
            output.mkdir()
            with self.assertRaises(ValueError):
                fix.upgrade(source, reference, library, audit, output, evidence, expected_source_sha256="0" * 64, audit_sha256=audit_sha)

    def test_real_builder_routes_only33_dp_through_the_shared_geometry_and_preserves_sp_and_bpl(self):
        # Root applies build_skin.patch before this test. It deliberately fails
        # against the old public route instead of patching its implementation in memory.
        import build_skin
        class MetadataReached(Exception): pass
        for layout, frame_package in (("dp", "gmframe33"), ("p1", "gmframe33"), ("p2", "gmframe33"), ("dp", "gmframe_bpl_apina")):
            with self.subTest(layout=layout, package=frame_package):
                builder = build_skin.Builder.__new__(build_skin.Builder)
                builder.style, builder.frame_package, builder.sd, builder.prefix = 33, frame_package, False, "fixture"
                builder.map = reference_fixture()
                for movie in ("dp_frame_1p", "1p_frame", "2p_frame", "center_frame_sp"):
                    builder.map["layouts"][movie] = {"draws": []}
                for player, movie in (("1p", "dp_frame_1p"), ("1p", "1p_frame"), ("2p", "2p_frame")):
                    builder.map["layouts"][movie]["draws"] = [{"game_bindings": [f"score_{player}_{index}"]} for index in range(4)]
                builder.score_font = builder.hs_font = builder.bpm_font = builder.small_bpm_font = "fixture-font"
                builder.bindings, builder.calibration_receipts = [], []
                builder.background = lambda selected: "unused-background"
                builder.copy_marker = lambda name: "unused-marker"
                builder.static_turntable_nodes = lambda selected: []
                builder.source_bounds = lambda *args: box(10, 10, 100, 30)
                builder.numeric = lambda identifier, *args, **kwargs: build_skin.node(identifier, "text", {"text": ""})
                original_bind = builder.bind
                def bind(target, prop, source):
                    original_bind(target, prop, source)
                    if source == "song.artist": raise MetadataReached()
                builder.bind = bind
                captured = {}
                original_node = build_skin.node
                def capture(identifier, *args, **kwargs):
                    result = original_node(identifier, *args, **kwargs)
                    if identifier.endswith((".title", ".artist")): captured[identifier.rsplit(".", 1)[1]] = result
                    return result
                with mock.patch.object(build_skin, "node", side_effect=capture), self.assertRaises(MetadataReached): builder.layout_consumers(layout)
                self.assertEqual(set(captured), {"title", "artist"})
                if layout == "dp" and frame_package == "gmframe33":
                    for name, y, height in (("title", 56, 26), ("artist", 83, 19.19970703125)):
                        props = captured[name]["properties"]
                        self.assertAlmostEqual(props["x"] * 1920, 15, delta=1e-4)
                        self.assertAlmostEqual(props["y"] * 1080, y, delta=1e-4)
                        self.assertAlmostEqual(props["width"] * 1920, 354, delta=1e-4)
                        self.assertAlmostEqual(props["height"] * 1080, height, delta=1e-4)
                        self.assertEqual(props["alignment"], "left")
                else:
                    dx = 270 if layout == "p1" else -270 if layout == "p2" else 0
                    self.assertEqual(captured["title"]["properties"]["x"], (610 + dx) / 1920)
                    self.assertEqual(captured["title"]["properties"]["width"], 700 / 1920)
                    self.assertEqual(captured["artist"]["properties"]["alignment"], "centre")
                self.assertEqual([item["source"] for item in builder.bindings], ["song.title", "song.artist"])


if __name__ == "__main__":
    unittest.main()
