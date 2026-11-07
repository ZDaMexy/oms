"""Source22 component isolation and old-contribution regressions, synthetic art only."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import re
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from customization_modules import material_contexts
from standard_components import StandardComponents, source22_root_repeat_control, source22_turntable_size
from standard_source_catalog import (COMPOSITION, COMPOSITION22, EARLIER_ALIASES, SOURCE22_ALIASES,
                                     SOURCE22_STYLES, SOURCE22_AVAILABLE_STYLES, frame_id, package_key, styles, retained_styles)


def composition(with22):
    result = {"contract": COMPOSITION22 if with22 else COMPOSITION, "aliases24": EARLIER_ALIASES}
    if with22:
        result["aliases22"] = SOURCE22_ALIASES
    return {"sourceComposition": result}


def owner(with22=False):
    value = object.__new__(StandardComponents)
    value.source_data = composition(with22)
    value.frame_ids = tuple(frame_id(s) for s in styles(value.source_data))
    value.modules, value.options, value.receipts, value.branch_counts = [], [], [], []
    value.documents, value.calls, value.marker_calls = {}, [], []
    value.contexts = list(material_contexts())
    value.json = lambda path, item: value.documents.__setitem__(path, deepcopy(item))
    value.text = lambda path, item: value.documents.__setitem__(path, item)
    value.preview = lambda module, choice, originals: f"previews/{'lane-cover' if module in ('lift-art','graph-area') and choice != 'blank' else module}/{choice}.png"
    value.packages = {f"lane{i:03}": {} for i in range(167)} | {f"turn{i:02}": {} for i in range(21)}
    if with22:
        value.packages.update({f"source22/lane{i:03}": {} for i in (27, 40, 58, 68, 86, 87, 88, 89)})
    value.index = {(package, package.rsplit("/", 1)[-1]): {"width": 288, "height": 482} for package in value.packages if "lane" in package}
    for package in ("notes_cn00", "source22/notes_cn00"):
        for colour, width in (("white", 34), ("black", 26), ("scratch", 60)):
            first = "notes_bss" if colour == "scratch" else "notes_cn_" + colour
            second = "notes_bss2" if colour == "scratch" else "notes_cn2_" + colour
            for stem in (first,) if package.startswith("source22/") else (first, second):
                for suffix in ("start", "end", "beam0", "beam1", "beam2", "beam4"):
                    if package.startswith("source22/") and suffix == "beam4":
                        continue
                    value.index[package, stem + "_" + suffix] = {"width": width, "height": 16 if package.startswith("source22/") else 8}

    def asset(category, package, name):
        identity = hashlib.sha256((package + "/" + name).encode()).hexdigest()
        return ({"id": category + ".a.t" + identity[:24], "type": "texture", "filter": "nearest",
                 "path": "custom/art/" + category + "/" + identity + ".png"}, value.index.get((package, name), {}))

    def marker(category, name, package, texture):
        value.marker_calls.append((category, name, package, texture))
        return "custom/markers/" + category + "/" + name

    def clip(package, movie, prefix, *, loop, trigger):
        value.calls.append((package, movie, prefix, loop, trigger))
        if package.startswith("source22/"):
            width, height = (80, 84) if "scr" in movie else (100, 105) if movie.endswith("_push") else (74, 100)
        else:
            width, height = (300, 300) if "2_push" in movie else (200, 200) if movie == "notes_break" else (125, 105) if movie.endswith("_push") else (96, 100)
        return {"node": {"id": prefix + ".art", "type": "recorded-animation", "animation": prefix + ".clip", "children": []},
                "resources": [], "animations": [{"id": prefix + ".clip", "path": "clips/" + prefix + ".json.br", "resources": []}],
                "files": [], "source": {"stage": [0, 0, width, height], "durationMs": 450 if not loop else 25 * 1000 / 60}}

    def black(branch):
        branch.resources["control.black"] = {"id": "control.black", "type": "texture", "path": "control/black.png"}
        return "control.black", "control/black"

    value.asset, value.fallback_marker, value.clip, value.control_black = asset, marker, clip, black
    return value


def walk(root):
    yield root
    for child in root.get("children", []):
        yield from walk(child)


class AP2DoActionTag:
    def __init__(self, actions):
        self.actions = actions
        self.bytecode = SimpleNamespace(as_dict=lambda: {"actions": self.actions})

    def as_dict(self):
        return {"type": type(self).__name__, "bytecode": self.bytecode.as_dict()}


class AP2RemoveObjectTag:
    def __init__(self, depth):
        self.object_id, self.depth = 0, depth

    def as_dict(self):
        return {"type": type(self).__name__, "object_id": self.object_id, "depth": self.depth}


def pendual_movie(name):
    held = name.endswith("_push")
    count = 25 if held else 27
    tags = [AP2DoActionTag([{"action": "END"}]), AP2DoActionTag([{"action": "END"}]),
            AP2RemoveObjectTag(2 if held else 14), AP2DoActionTag([
                {"action": "PUSH", "objects": ["'loop'", "1", "StringConstant(0x815: deepGotoAndPlay)"]},
                {"action": "CALL_FUNCTION"}, {"action": "END"}, {"action": "END"}])]
    frames = [SimpleNamespace(start_tag_offset=0, num_tags=2)]
    frames.extend(SimpleNamespace(start_tag_offset=2, num_tags=0) for _ in range(count - 2))
    frames.append(SimpleNamespace(start_tag_offset=2, num_tags=2))
    return SimpleNamespace(name=name, tags=tags, frames=frames, fps=60, labels={"loop": 0},
                           location=SimpleNamespace(left=0, top=0, right=74, bottom=100))


class Hook:
    movie_name = "customize"
    source_tag_id = 7

    def as_dict(self):
        return {"movie_name": "customize", "source_tag_id": 7}


class GraphRecorder:
    def __init__(self, *args):
        pass

    def record(self, package, movie, *args):
        x = 1020 if movie == "graph_1p" else 20
        identity = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0]
        return {"nodes": [{"id": 0, "parent": -1, "sourceTag": None}, {"id": 1, "parent": 0, "sourceTag": 7}],
                "states": [{"matrix": identity, "origin": [0, 0, 0], "mask": [0, 0, 1280, 720]},
                           {"matrix": [*identity[:9], x, 180, 0], "origin": [0, 0, 0], "mask": [0, 0, 220, 482]}],
                "frames": [{"nodes": [[0, 0], [1, 1]]}, {"nodes": []}]}


def setup_graph(value, output):
    value.output = output
    value.tools = SimpleNamespace(AP2DefineSpriteTag=type("UnusedSprite", (), {}), AP2PlaceObjectTag=Hook)
    value.StateRenderer = object
    source = {"movies": {name: SimpleNamespace(tags=[Hook()]) for name in ("graph_1p", "graph_2p")},
              "shapes": {}, "mapping": {}, "filters": None}
    value._package = lambda package: source if package == "0/graph" else None


def turn_draw(binding, source22=False):
    size = 25 if source22 else 100
    points = [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (size, 0), (0, size), (size, size))]
    return {"game_bindings": [binding], "texture": "turn00", "asset": None if source22 else {"width": size, "height": size},
            "matrix": {"a": 1, "b": 0, "c": 0, "d": 1, "tx": 50, "ty": 500},
            "source_vertices": points, "projected_vertices": [{"x": p["x"] + 50, "y": p["y"] + 500, "z": 0} for p in points],
            "bounds": {"left": 50, "top": 500, "right": 50 + size, "bottom": 500 + size},
            "uv": [{"x": x, "y": y, "z": 0} for x, y in ((0, 0), (1, 0), (0, 1), (1, 1))],
            "draw_params": [{"flags": 67, "region": "turn00", "vertexes": [0, 1, 2, 2, 1, 3], "blend": None}]}


def build_turntables(value):
    value.frame_maps = Path("synthetic-frame-maps.json")
    inputs = {str(value.frame_maps): {frame_id(style): frame_id(style) + ".json" for style in retained_styles(value.source_data)}}
    for style in retained_styles(value.source_data):
        identity = frame_id(style)
        is22 = identity in SOURCE22_STYLES
        inputs[identity + ".json"] = {"package": package_key(style), "common_family": "s22" if is22 else "0",
            "stage": {"width": 1280, "height": 720}, "layouts": {
                "1p_frame": {"draws": [turn_draw("turn_1p", is22)]},
                "2p_frame": {"draws": [turn_draw("turn_2p", is22)]},
                "dp_frame_1p": {"draws": [turn_draw("turn_1p", is22), turn_draw("turn_2p", is22)]}}}
    with patch("standard_components.read_json", side_effect=lambda path: inputs[str(path)]), \
            patch("standard_components.within", side_effect=lambda root, relative: Path(relative)):
        value.turntables()


class PendualComponentsTests(unittest.TestCase):
    def assert_old_choices_unchanged(self, old, new):
        self.assertEqual(old.options, new.options)
        self.assertEqual(len(old.modules), len(new.modules))
        for first, extended in zip(old.modules, new.modules):
            self.assertEqual(first["id"], extended["id"])
            self.assertEqual(first["default"], extended["default"])
            self.assertEqual(first["choices"], extended["choices"][:len(first["choices"])])
        for path, value in old.documents.items():
            self.assertEqual(value, new.documents[path], path)

    def test_lane_and_lift_add_exact_eight_original_sources_without_replacing_old_choices(self):
        expected = [f"l22-{i:03}" for i in (27, 40, 58, 68, 86, 87, 88, 89)]
        for method in ("static_lanes", "lift_art"):
            with self.subTest(method=method):
                old, new = owner(), owner(True)
                self.assertEqual(list(old.lane_sources()), list(new.lane_sources())[:167])
                getattr(old, method)()
                getattr(new, method)()
                self.assert_old_choices_unchanged(old, new)
                choices = new.modules[0]["choices"]
                self.assertEqual(175, len(choices))
                self.assertEqual(expected, [c["value"] for c in choices[167:]])
                self.assertEqual(175, len({c["scene"] for c in choices}))
                for choice in choices[167:]:
                    scene = new.documents[choice["scene"]]
                    artwork = [n for t in scene["templates"] for n in walk(t["root"]) if n["id"].endswith(".art")]
                    self.assertEqual(1, len(artwork))
                    package = "source22/lane" + choice["value"][-3:]
                    self.assertEqual(new.asset("sl", package, package.rsplit("/", 1)[-1])[0]["id"], artwork[0]["resource"])

    def test_graph_uses_the_same_eight_images_without_replacing_old_geometry_or_scores(self):
        with tempfile.TemporaryDirectory() as temporary, patch("standard_components.LocalStateRecorder", GraphRecorder):
            old, new = owner(), owner(True)
            setup_graph(old, Path(temporary) / "old")
            setup_graph(new, Path(temporary) / "new")
            old.graph_art()
            new.graph_art()
            self.assert_old_choices_unchanged(old, new)
            self.assertEqual(176, len(new.modules[0]["choices"]))
            for choice in new.modules[0]["choices"][168:]:
                scene = new.documents[choice["scene"]]
                self.assertEqual({"markers/frame-p1-information", "markers/frame-p2-information", "markers/frame-dp-information"},
                                 {i["material"]["resource"] for i in scene["instances"]})
                self.assertEqual({"option.graph-visible"}, {b["source"] for b in scene["bindings"]})
                self.assertEqual([], scene["tracks"])
                self.assertEqual([], scene["variants"])

    def test_charge_source22_keeps_old_contribution_exact_and_owns_all_its_pixels_and_clocks(self):
        old, new = owner(), owner(True)
        old.charge_notes()
        new.charge_notes()
        self.assert_old_choices_unchanged(old, new)
        choice = new.modules[0]["choices"][1]
        self.assertEqual("c22", choice["value"])
        self.assertEqual([{"auto-scratch-display": False}], [v["when"] for v in choice["variants"]])
        own_calls = [c for c in new.calls if c[0] == "source22/lnote00"]
        self.assertEqual(6, len({c[1] for c in own_calls}))
        self.assertEqual({"source22/notes_cn00"}, {c[2] for c in new.marker_calls if c[0] == "sc22"})
        for _, movie, prefix, loop, trigger in own_calls:
            self.assertTrue(prefix.startswith("sc22."))
            self.assertEqual(movie.endswith("_push"), loop)
            self.assertEqual("object.state" if loop else "object.long-note-transition", trigger)
        for contribution in (choice, *choice["variants"]):
            scene = new.documents[contribution["scene"]]
            self.assertFalse(any(b["source"] == "option.explosion-scale" for b in scene["bindings"]))
            effects = [t["root"] for t in scene["templates"] if t["root"].get("slot") == "effect.long-note"]
            self.assertEqual(6, len(effects))
            self.assertTrue(all(len(t["children"]) == 2 for t in effects))
            for effect in effects:
                continuous = next(n for n in effect["children"] if n["id"].endswith(".cn"))
                gate = next(v for v in scene["variants"] if v["target"] == continuous["id"])
                self.assertEqual(["ln-holding", "cn-holding", "hcn-holding"], [c["key"] for c in gate["cases"]])
                self.assertFalse(gate["default"])

    def test_c22_ascr_gates_whole_notes_but_not_effects_and_has_valid_unique_scene_identities(self):
        value = owner(True)
        value.charge_notes()
        choice = value.modules[0]["choices"][1]
        for contribution in (choice, *choice["variants"]):
            scene, manifest = value.documents[contribution["scene"]], value.documents[contribution["manifest"]]
            all_nodes = [n for tree in [scene["root"], *[t["root"] for t in scene["templates"]]] for n in walk(tree)]
            nodes = {n["id"]: n for n in all_nodes}
            self.assertEqual(len(all_nodes), len(nodes))
            identities = list(nodes) + [x["id"] for field in ("templates", "instances", "bindings", "variants", "tracks") for x in scene[field]]
            identities += [c["id"] for variant in scene["variants"] for c in variant["cases"]]
            self.assertEqual(len(identities), len(set(identities)))
            for identifier in identities + [r["id"] for r in manifest["resources"]] + [a["id"] for a in manifest["animations"]]:
                self.assertLessEqual(len(identifier), 128)
                self.assertRegex(identifier, r"^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)*$")
            gates = [v for v in scene["variants"] if v["source"] == "object.auto-assist"]
            self.assertEqual(18 if contribution.get("when") else 0, len(gates))
            for gate in gates:
                self.assertIn(nodes[gate["target"]]["slot"], ("object.long-note.head", "object.long-note.body", "object.long-note.tail"))
                self.assertEqual(True, gate["default"])
                self.assertEqual([("scratch", False)], [(c["key"], c["value"]) for c in gate["cases"]])
            resources = {r["id"] for r in manifest["resources"]}
            for variant in scene["variants"]:
                self.assertIn(variant["target"], nodes)
                if variant["property"] == "resource":
                    self.assertIn(variant["default"], resources)
                    self.assertTrue(all(c["resource"] in resources for c in variant["cases"]))

    def test_mixed_frame_ids_keep_existing_turntable_nodes_and_use_original22_normalized_geometry(self):
        old, new = owner(), owner(True)
        build_turntables(old)
        build_turntables(new)
        for first, extended in zip(old.modules[0]["choices"], new.modules[0]["choices"]):
            old_scene, scene = old.documents[first["scene"]], new.documents[extended["scene"]]
            nodes = {n["id"]: n for t in scene["templates"] for n in walk(t["root"])}
            for template in old_scene["templates"]:
                for frame in template["root"]["children"]:
                    self.assertEqual(frame, nodes[frame["id"]])
            old_tracks = {t["id"]: t for t in old_scene["tracks"]}
            self.assertEqual(old_tracks, {t["id"]: t for t in scene["tracks"] if t["id"] in old_tracks})
            self.assertEqual(68, len([t for t in scene["tracks"] if t["property"] == "rotation"]))
            for track in scene["tracks"]:
                if track["property"] != "rotation" or "source22-" not in track["target"]:
                    continue
                node = nodes[track["target"]]
                self.assertEqual(25 / 1280, node["properties"]["width"])
                self.assertEqual(25 / 720, node["properties"]["height"])
                self.assertEqual("input.motion-revolutions-2" if ".dp." in track["target"] and track["target"].endswith(".n1")
                                 else "input.motion-revolutions-1", track["clock"])

    def test_unresolved22_turntable_cannot_borrow_another_family_or_partial_uv(self):
        frame = "source22-gmframe0-00"
        for mutation in ("family", "package", "uv", "geometry", "flags"):
            with self.subTest(mutation=mutation):
                source = {"package": package_key(frame), "common_family": "s22"}
                draw = turn_draw("turn_1p", True)
                if mutation == "family":
                    source["common_family"] = "0"
                elif mutation == "package":
                    source["package"] = "gmframe26"
                elif mutation == "uv":
                    draw["uv"][1]["x"] = .9
                elif mutation == "geometry":
                    draw["source_vertices"][3]["x"] = 26
                else:
                    draw["draw_params"][0]["flags"] = 3
                with self.assertRaises(ValueError):
                    source22_turntable_size(frame, source, draw)

    def test_constructor_qualifies_full_nested_frame_names_not_just_numeric_basenames(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            library = root / "library"
            library.mkdir()
            data = {**composition(True), "status": "complete", "sourceMetadataUnchanged": True, "textures": [],
                    "packages": [{"source": "contents/data/graphic/" + package_key(s) + ".ifs"} for s in retained_styles(composition(True))]}
            path = library / "library.json"
            path.write_text(json.dumps(data), encoding="utf-8")
            with patch("standard_components.load_tools", return_value=None), patch("standard_components.exporter_classes", return_value=(None, None)):
                value = StandardComponents(library, root / "output", root, root)
                self.assertEqual(17, len(value.frame_ids))
                self.assertTrue(set(SOURCE22_AVAILABLE_STYLES) <= set(value.frame_ids))
                self.assertNotIn("source22-gmframe0", value.frame_ids)
                data["packages"][-1]["source"] = "contents/data/graphic/gmframe10.ifs"
                path.write_text(json.dumps(data), encoding="utf-8")
                with self.assertRaisesRegex(ValueError, "qualified Standard frame"):
                    StandardComponents(library, root / "rejected", root, root)
                self.assertFalse((root / "rejected").exists())

    def test_22_recorded_effect_uses_lossless_atlas_and_keeps_all_declared_native_frames(self):
        import brotli
        from PIL import Image
        with tempfile.TemporaryDirectory() as temporary:
            base = Path(temporary)
            library, output = base / "library", base / "output"
            library.mkdir()
            output.mkdir()
            png = library / "synthetic.png"
            Image.new("RGBA", (8, 6), (50, 70, 90, 123)).save(png)
            original_bytes = png.read_bytes()
            row = {"name": "synthetic.png", "sprite": png.name, "width": 8, "height": 6,
                   "sha256": hashlib.sha256(original_bytes).hexdigest(),
                   "sampler": {"mag": "nearest", "min": "nearest", "wrapS": "clamp", "wrapT": "clamp"}}
            state = {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0], "origin": [0, 0, 0],
                     "projection": 1, "mult": [1, 1, 1, 1], "add": [0, 0, 0, 0], "hsl": [0, 0, 0],
                     "mask": None, "visible": True, "blend": 0, "filters": []}

            class Recorder:
                math_checks, math_max_error = 0, 0

                def __init__(self, tools, renderer_class):
                    self.renderer = renderer_class()
                    self.root = object()
                    self.renderer._AFPRenderer__root = self.root

                def record(self, package, name, movie, *args, frame_count=None):
                    if frame_count is not None:
                        raise AssertionError("22 native counter includes the full declared27/25 source timeline")
                    place = self.renderer._AFPRenderer__place
                    # Preserve initialization/removal and child actions. Only
                    # the qualified jump on this actual root is translated.
                    for tag in movie.tags[:-1]:
                        if place(tag, self.root) != (tag, True):
                            raise AssertionError("Original root initialization/removal was skipped")
                    if place(movie.tags[-1], object()) != (movie.tags[-1], True):
                        raise AssertionError("A nested clip was mistaken for the source root")
                    if place(movie.tags[-1], self.root) != (None, False):
                        raise AssertionError("The original root still rewinds inside the bounded export")
                    return {"canvas": [74, 100], "fps": 60, "cameras": [], "states": [state],
                            "nodes": [{"parent": -1, "geometry": 0}],
                            "frames": [{"nodes": [[0, 0]], "camera": None} for _ in movie.frames] + [{"nodes": [], "camera": None}]}

            value = object.__new__(StandardComponents)
            value.library, value.output = library, output
            class Renderer:
                def _AFPRenderer__place(self, tag, clip, prefix=""):
                    return tag, True

            value.tools, value.StateRenderer = None, Renderer
            value.image_tools = Path(__file__).resolve().parents[3] / ".dev-cache/tools/iidx-assets"
            value.clip_cache, value.files, value.used_assets, value.receipts = {}, {}, {}, []
            value.packages = {"source22/lnote00": {"source": "contents/data/graphic/source22/lnote00.ifs", "sha256": "synthetic-source"}}
            movies = {name: pendual_movie(name) for name in ("lnote00_lo_siro", "lnote00_lo_siro_push")}
            package = {"movies": movies, "rows": [row], "geometry": [{"source": "synthetic", "colours": [],
                "vertices": [[0, 0, 0], [8, 0, 0], [0, 6, 0], [8, 6, 0]],
                "draws": [{"texture": 0, "flags": 65, "sourceColour": None,
                           "uv": [[0, 0], [1, 0], [0, 1], [1, 1]], "indices": [0, 1, 2, 2, 1, 3]}]}],
                "shapes": {}, "mapping": {}, "filters": SimpleNamespace(values=[]), "parseWarnings": [], "hashes": {}}
            value._package = lambda name: package
            with patch("standard_components.LocalStateRecorder", Recorder), \
                    patch("standard_components.terminal_repeat_control", side_effect=AssertionError("Do not apply the26 terminal ABI to22")):
                for name, loop, trigger, count in (("lnote00_lo_siro", False, "object.long-note-transition", 27),
                                                  ("lnote00_lo_siro_push", True, "object.state", 25)):
                    exported = value.clip("source22/lnote00", name, "sc22.test." + ("push" if loop else "base"), loop=loop, trigger=trigger)
                    atlas = exported["receipt"]["losslessSourcePixelAtlas"]
                    self.assertEqual(1, atlas["atlasResources"])
                    self.assertTrue(atlas["allXYZTopologyStatesFramesAndMetadataUnchanged"])
                    self.assertEqual(count, exported["source"]["sourceFrames"])
                    self.assertEqual(count, exported["source"]["translatedSource22RootRepeat"]["playbackFrames"])
                    self.assertNotIn("translatedTerminalRepeat", exported["source"])
                    animation, = exported["animations"]
                    clip = json.loads(brotli.decompress((output / animation["path"]).read_bytes()))
                    self.assertEqual(count + 1, len(clip["frames"]))
                    self.assertEqual([], clip["frames"][-1]["nodes"])
                    self.assertEqual((loop, trigger), (clip["loop"], clip["trigger"]))
                package["parseWarnings"] = ["WARNING: Unsupported other source action"]
                with self.assertRaisesRegex(ValueError, "Unsupported other source action"):
                    value.clip("source22/lnote00", "lnote00_lo_siro", "sc22.unknown",
                               loop=False, trigger="object.long-note-transition")
            self.assertEqual(original_bytes, png.read_bytes())

    def test_22_global_end_loop_never_shrinks_and_rejects_other_control_shapes(self):
        for colour in ("siro", "ao", "scr"):
            for suffix, expected in (("", 27), ("_push", 25)):
                movie = pendual_movie("lnote00_lo_" + colour + suffix)
                control = source22_root_repeat_control(movie)
                self.assertEqual(expected, control["playbackFrames"])
                self.assertEqual(expected - 1, control["sourceFrame"])
                self.assertEqual(3, control["tagIndex"])
                self.assertEqual("global.deepGotoAndPlay", control["method"])
        changes = {
            "other-movie": lambda m: setattr(m, "name", "lnote00_lo_siro2"),
            "trimmed-frame": lambda m: m.frames.pop(-2),
            "other-label": lambda m: m.labels.update(loop=1),
            "other-target": lambda m: m.tags[-1].actions[0]["objects"].__setitem__(0, "'next'"),
            "method-ABI": lambda m: m.tags[-1].actions[1].update(action="CALL_METHOD"),
            "additional-action": lambda m: m.tags[-1].actions.insert(1, {"action": "STOP"}),
            "other-removal": lambda m: setattr(m.tags[-2], "depth", 2),
            "middle-jump": lambda m: setattr(m.frames[1], "num_tags", 2),
        }
        for name, change in changes.items():
            with self.subTest(name=name):
                movie = pendual_movie("lnote00_lo_siro")
                change(movie)
                with self.assertRaises(ValueError):
                    source22_root_repeat_control(movie)


if __name__ == "__main__":
    unittest.main()
