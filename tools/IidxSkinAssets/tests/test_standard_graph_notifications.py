"""Graph movie identity and exact rank-image replacement, using synthetic data."""
from copy import deepcopy
import hashlib
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_graph_notifications import (export_graph_notifications, rank_child_contract, replace_rank_texture,
                                          validate_notification_program, validate_notification_source)


class Place:
    def __init__(self, reference, *, name=None, object_id=1, depth=1):
        self.source_tag_id, self.movie_name = reference, name
        self.object_id, self.depth, self.update = object_id, depth, False


class Sprite:
    def __init__(self, identifier, tags):
        self.id, self.tags = identifier, tags


def texture(name):
    return {"name": name + ".png", "width": 276, "height": 36,
            "sampler": {"mag": "nearest", "min": "nearest", "wrapS": "clamp", "wrapT": "clamp"},
            "imageRect": [0, 278, 0, 38], "uvRect": [1, 277, 1, 37], "sha256": "synthetic-" + name}


def source_fixture():
    vertices = [[0, 0, 0], [276, 0, 0], [0, 36, 0], [276, 36, 0]]
    uv, triangles = [[0, 0], [1, 0], [0, 1], [1, 1]], [0, 1, 2, 2, 1, 3]
    named = Place(15, name="lv_pass", object_id=141, depth=2)
    movie = SimpleNamespace(tags=[SimpleNamespace(id=13, reference="synthetic-rank"),
        Sprite(14, [Place(13)]), Sprite(15, [Place(14)]), Sprite(18, [deepcopy(named)]), named])
    source = {"movies": {"lv_pass": movie}, "mapping": {"synthetic-rank": 0}, "rows": [texture("pass_aaa")],
              "geometry": [{"vertices": vertices, "colours": [], "draws": [
                  {"texture": 0, "uv": uv, "indices": triangles, "flags": 3, "sourceColour": None}]}]}
    root = {"nodes": [
        {"id": 0, "parent": -1, "depth": -1, "sourceObject": -1, "sourceTag": -1, "geometry": None},
        {"id": 1, "parent": 0, "depth": 2, "sourceObject": 141, "sourceTag": 15, "geometry": None},
        {"id": 2, "parent": 1, "depth": 1, "sourceObject": 1, "sourceTag": 14, "geometry": None},
        {"id": 3, "parent": 2, "depth": 1, "sourceObject": 1, "sourceTag": 13, "geometry": 0}]}
    tools = SimpleNamespace(AP2PlaceObjectTag=Place, AP2DefineSpriteTag=Sprite)
    return source, root, tools


def program_fixture():
    source, _, _ = source_fixture()
    shape = source["geometry"][0]
    return {"contract": "oms-gameplay-recorded-animation.v1", "width": 280, "height": 720, "fps": 60,
        "trigger": "score.crossing", "loop": False,
        "geometries": [{"resource": "synthetic.pass_aaa",
            "vertices": [[*point, *uv] for point, uv in zip(shape["vertices"], shape["draws"][0]["uv"])],
            "triangles": shape["draws"][0]["indices"]},
            {"resource": "synthetic.flash", "vertices": [[0, 0, 0, 0, 0]], "triangles": []}],
        "nodes": [{"parent": -1, "geometry": -1, "blend": "normal"},
                  {"parent": 0, "geometry": 0, "blend": "normal"},
                  {"parent": 0, "geometry": 1, "blend": "additive"}],
        "states": [{"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 140, 276, 0], "origin": [138, 18, 0],
            "projection": "affine", "multiply": [1, 0.8, 0.7, 0.3], "add": [0, 0.2, 0, 0],
            "hsl": [0, 0, 0], "mask": [0, 0, 280, 720], "colourMatrix": None}],
        "frames": [{"nodes": [[0, 0], [1, 0], [2, 0]], "camera": [140, 360, 0, 500]} for _ in range(150)]
                  + [{"nodes": [], "camera": None}]}


def source_metadata(movie="lv_pass"):
    return {"movie": movie, "stage": [0, 0, 280, 720], "fps": 60,
            "sourceFrames": 150, "durationMs": 2500, "labels": {}, "translatedRootControl": []}


class StandardGraphNotificationTests(unittest.TestCase):
    def test_original_full_movie_and_terminal_are_required(self):
        validate_notification_source(source_metadata())
        validate_notification_program(program_fixture())
        for changed in ({"sourceFrames": 140}, {"durationMs": 2300}, {"movie": "achieve_pass"},
                        {"stage": [0, 0, 276, 36]}, {"fps": 30}, {"labels": {"loop": 0}},
                        {"translatedRootControl": [{"heldLoop": [0, 20]}]}):
            with self.subTest(change=changed), self.assertRaises(ValueError):
                validate_notification_source({**source_metadata(), **changed})
        for changed in ({"loop": True}, {"trigger": "gameplay.complete"}, {"heldLoop": [0, 20]},
                        {"frames": program_fixture()["frames"][:-1]}, {"width": 276}):
            with self.subTest(change=changed), self.assertRaises(ValueError):
                validate_notification_program({**program_fixture(), **changed})

    def test_named_live_child_is_traced_without_counting_unused_duplicate_definition(self):
        child = rank_child_contract(*source_fixture())
        self.assertEqual([15, 14, 13], child["sourceTagChain"])
        self.assertEqual(3, child["recordedMeshNode"])
        self.assertEqual("pass_aaa", child["sourceTexture"])

    def test_ambiguous_live_child_unqualified_subtree_and_cycles_are_rejected(self):
        source, root, tools = source_fixture()
        source["movies"]["lv_pass"].tags.append(Place(15, name="lv_pass", object_id=142, depth=4))
        with self.assertRaises(ValueError):
            rank_child_contract(source, root, tools)
        source, root, tools = source_fixture()
        source["movies"]["lv_pass"].tags[2].tags.append(Place(14, object_id=2))
        with self.assertRaises(ValueError):
            rank_child_contract(source, root, tools)
        source, root, tools = source_fixture()
        source["movies"]["lv_pass"].tags[1].tags[0].source_tag_id = 15
        with self.assertRaises(ValueError):
            rank_child_contract(source, root, tools)

    def test_shared_live_geometry_or_partial_source_uv_cannot_be_swapped(self):
        source, root, tools = source_fixture()
        root["nodes"].append({**root["nodes"][3], "id": 4, "parent": 0})
        with self.assertRaises(ValueError):
            rank_child_contract(source, root, tools)
        source, root, tools = source_fixture()
        source["geometry"][0]["draws"][0]["uv"][1][0] = 0.9
        with self.assertRaises(ValueError):
            rank_child_contract(source, root, tools)

    def test_a_and_aa_change_only_one_reference_preserving_frames_and_all_draw_data(self):
        original = program_fixture()
        snapshot = deepcopy(original)
        child = rank_child_contract(*source_fixture())
        for rank in ("pass_aaa", "pass_aa", "pass_a"):
            result, proof = replace_rank_texture(original, "synthetic.pass_aaa", "synthetic." + rank,
                                                   texture("pass_aaa"), texture(rank), child)
            restored = deepcopy(result)
            restored["geometries"][0]["resource"] = "synthetic.pass_aaa"
            self.assertEqual(snapshot, restored)
            self.assertEqual(snapshot, original)
            self.assertTrue(proof["allOtherProgramFieldsUnchanged"])
            self.assertEqual(151, len(result["frames"]))
            result["states"][0]["multiply"][3] = 0
            self.assertEqual(0.3, original["states"][0]["multiply"][3])

    def test_different_size_sampler_crop_or_non_rank_image_is_rejected(self):
        child = rank_child_contract(*source_fixture())
        for change in ({"width": 275}, {"height": 35}, {"uvRect": [1, 276, 1, 37]},
                       {"sampler": {"mag": "linear"}}, {"name": "pass_platinum.png"}):
            with self.subTest(change=change), self.assertRaises(ValueError):
                replace_rank_texture(program_fixture(), "synthetic.pass_aaa", "synthetic.pass_a",
                                     texture("pass_aaa"), {**texture("pass_a"), **change}, child)

    def test_missing_shared_or_changed_runtime_geometry_is_rejected(self):
        child = rank_child_contract(*source_fixture())
        for mutation in (lambda p: p["geometries"].pop(0),
                         lambda p: p["geometries"].append(deepcopy(p["geometries"][0])),
                         lambda p: p["geometries"][0]["vertices"][0].__setitem__(0, 1)):
            program = program_fixture()
            mutation(program)
            with self.assertRaises(ValueError):
                replace_rank_texture(program, "synthetic.pass_aaa", "synthetic.pass_a",
                                     texture("pass_aaa"), texture("pass_a"), child)

    def test_both_families_export_six_clips_and_one_shared_update_each(self):
        import brotli

        class SyntheticExporter:
            def __init__(self, library, output, bemani_root, image_tools):
                self.output, self.files, self.source_library_sha = output, {}, "synthetic-library"
                self.source_families = ("0", "1")
                self.tools = source_fixture()[2]
                self.calls = []

            def _package(self, package):
                return source_fixture()[0]

            def asset(self, category, package, name):
                return {"id": "synthetic." + name, "type": "texture", "path": name + ".png", "filter": "nearest"}, texture(name)

            def clip(self, package, movie, prefix, *, loop, trigger):
                self.calls.append((package, movie, loop, trigger))
                relative = f"custom/{prefix}/clip.json.br"
                path = self.output / relative
                path.parent.mkdir(parents=True)
                data = brotli.compress(json.dumps(program_fixture()).encode())
                path.write_bytes(data)
                self.files[relative] = {"path": relative, "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data)}
                research = self.output / "research" / prefix
                research.mkdir(parents=True)
                (research / "source-root.json").write_text(json.dumps(source_fixture()[1]), "utf-8")
                return {"source": source_metadata(movie), "receipt": {}, "files": [],
                    "node": {"id": prefix + ".node", "animation": prefix + ".clip"},
                    "resources": [self.asset("sgn", package, name)[0] for name in ("pass_aaa", "flash")],
                    "animations": [{"id": prefix + ".clip", "path": relative,
                                    "resources": ["synthetic.pass_aaa", "synthetic.flash"]}]}

        with tempfile.TemporaryDirectory() as directory, patch("standard_graph_notifications.StandardComponents", SyntheticExporter):
            output = Path(directory)
            result = export_graph_notifications(output, output, output, output)
            self.assertEqual("oms-standard-graph-notifications.v1", result["contract"])
            self.assertEqual("synthetic-library", result["librarySHA256"])
            self.assertEqual({"0", "1"}, set(result["families"]))
            for family in result["families"].values():
                clips = family["clips"]
                self.assertEqual({"mybest", "target", "aaa", "aa", "a", "update"}, set(clips))
                paths = [clip["animations"][0]["path"] for clip in clips.values()]
                self.assertEqual(6, len(set(paths)))
                original = json.loads(brotli.decompress((output / clips["aaa"]["animations"][0]["path"]).read_bytes()))
                for rank in ("aa", "a"):
                    variant = json.loads(brotli.decompress((output / clips[rank]["animations"][0]["path"]).read_bytes()))
                    self.assertEqual("synthetic.pass_" + rank, variant["geometries"][0]["resource"])
                    variant["geometries"][0]["resource"] = "synthetic.pass_aaa"
                    self.assertEqual(original, variant)
                    self.assertNotIn("synthetic.pass_aaa", clips[rank]["animations"][0]["resources"])
                self.assertEqual(68, family["receipt"]["sourcePriorities"]["update"])
            self.assertEqual(12, len(result["files"]))
            self.assertEqual(result, json.loads((output / "graph-notifications.json").read_text("utf-8")))


if __name__ == "__main__":
    unittest.main()
