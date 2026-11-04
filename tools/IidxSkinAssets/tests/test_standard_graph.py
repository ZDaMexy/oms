"""Public-scene and source-boundary checks for the independent26 Graph."""
import hashlib
import json
from pathlib import Path
import re
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from standard_graph import BAR_BOTTOM, BAR_TOP, FRAME_IDS, PACKAGE, build_graph


def walk(root):
    yield root
    for child in root.get("children", []):
        yield from walk(child)


class StandardGraphTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.library = Path(self.temporary.name)
        sizes = {f"gr_sco_{i:02}": (23, 22) for i in range(10)}
        sizes.update({"gr_score_graph_panel": (280, 720), "gr_line_max": (276, 20), "gr_line_under": (272, 6),
                      "gr_p_nowsc": (135, 31), "gr_p_bstsc": (135, 31), "gr_score_tag_red": (135, 31)})
        sizes.update({"gr_line_" + rank: (272, 18) for rank in ("aaa", "aa", "a")})
        sizes.update({"gr_sco_gauge_large_" + colour: (66, 448) for colour in ("blue", "green", "red", "ref")})
        textures = []
        for name, (width, height) in sizes.items():
            # These byte tokens test provenance and copying, not PNG decoding.
            path = self.library / ("sprites/" + name + ".png")
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(("source-byte-fixture:" + name).encode("ascii"))
            textures.append({"package": PACKAGE, "name": name + ".png", "width": width, "height": height,
                             "sprite": path.relative_to(self.library).as_posix(),
                             "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        archive = self.library / "source-copies" / PACKAGE
        archive.parent.mkdir(parents=True)
        archive.write_bytes(b"unit-test archive identity, not original game data")
        self.source_sha256 = hashlib.sha256(archive.read_bytes()).hexdigest()
        self.metadata = {"status": "complete", "sourceMetadataUnchanged": True,
                         "packages": [{"source": PACKAGE, "sha256": self.source_sha256}]
                         + [{"source": f"contents/data/graphic/gmframe{frame:02}.ifs"} for frame in FRAME_IDS],
                         "textures": textures}
        self.write_metadata()
        xml = self.library / "packages/contents/data/graphic/0/graph/raw/tex/texturelist.xml"
        xml.parent.mkdir(parents=True)
        xml.write_text('<texturelist><texture mag_filter="nearest">' +
                       "".join(f'<image name="{name}" />' for name in sizes) + '</texture></texturelist>', encoding="utf-8")

    def write_metadata(self):
        (self.library / "library.json").write_text(json.dumps(self.metadata), encoding="utf-8")

    def test_source_panel_surrounds_custom_art_and_live_data(self):
        value = build_graph(self.library, "p1")
        roots = [template["root"] for template in value["templates"]]
        depths = {root["id"].rsplit(".", 1)[-1]: root["properties"]["z"] for root in roots}
        self.assertLess(depths["data"], -20)
        self.assertGreater(depths["base"], -20)
        for instance in value["instances"]:
            self.assertEqual({"slot": "decoration", "resource": "markers/frame-p1-information"}, instance["material"])
        visible = {b["target"] for b in value["bindings"] if b["source"] == "option.graph-visible"}
        self.assertEqual({root["id"] for root in roots}, visible)

    def test_three_columns_use_real_values_without_invented_opponents(self):
        value = build_graph(self.library, "p1")
        bindings = {(b["target"], b["property"]): b["source"] for b in value["bindings"]}
        self.assertEqual("score.progress", bindings["standard.graph.p1.bar.current.fill", "height"])
        self.assertEqual("score.personal-best-progress", bindings["standard.graph.p1.bar.best.fill", "height"])
        self.assertEqual("score.maximum-available", bindings["standard.graph.p1.maximum.available", "visible"])
        self.assertEqual("score.personal-best-available", bindings["standard.graph.p1.bar.best.available", "visible"])
        self.assertEqual("score.personal-best-available", bindings["standard.graph.p1.best.available", "visible"])
        nodes = [n for t in value["templates"] for n in walk(t["root"])]
        maximum = next(n for n in nodes if n["id"] == "standard.graph.p1.bar.maximum.fill")
        self.assertEqual(1, maximum["properties"]["height"])
        self.assertTrue(any(n["properties"].get("text") == "MAXIMUM" for n in nodes))
        self.assertTrue(any(n["properties"].get("text") == "NO DATA" for n in nodes))
        self.assertFalse(any("pacemaker" in r["path"] or "rival" in r["path"] for r in value["resources"]))

    def test_zero_score_has_zero_bar_height_and_full_score_reaches_source_top(self):
        value = build_graph(self.library, "p1")
        nodes = {n["id"]: n for t in value["templates"] for n in walk(t["root"])}
        parent = nodes["standard.graph.p1.bar.current"]["properties"]
        fill = nodes["standard.graph.p1.bar.current.fill"]["properties"]
        self.assertEqual("bottom-left", fill["origin"])
        self.assertEqual(0, fill["height"])
        baseline = (parent["y"] + parent["height"] * fill["y"]) * 720
        self.assertAlmostEqual(BAR_BOTTOM, baseline)
        self.assertAlmostEqual(BAR_TOP, baseline - parent["height"] * 720)

    def test_rank_strokes_follow_available_native_thresholds(self):
        value = build_graph(self.library, "p1")
        gate = next(b for b in value["bindings"] if b["source"] == "score.rank-available")
        self.assertEqual("visible", gate["property"])
        for rank in ("aaa", "aa", "a"):
            track = next(t for t in value["tracks"] if t["clock"] == f"score.rank-{rank}-progress")
            first, last = track["keyframes"]
            for ratio in (0, 2 / 3, 7 / 9, 8 / 9, 1):
                y = first["value"] + (last["value"] - first["value"]) * ratio
                self.assertAlmostEqual(BAR_BOTTOM - (BAR_BOTTOM - BAR_TOP) * ratio, y * 720 + 14)

    def test_p1_p2_and_dp_keep_original_canvas_and_separate_ids(self):
        sets = []
        for layout, left in (("p1", 1000), ("p2", 0), ("dp", 1000)):
            value = build_graph(self.library, layout)
            roots = [t["root"] for t in value["templates"]]
            panel = next(n for root in roots for n in walk(root) if n["id"].endswith(".panel"))
            self.assertAlmostEqual(left, panel["properties"]["x"] * 1280)
            self.assertAlmostEqual(280, panel["properties"]["width"] * 1280)
            self.assertEqual(1, panel["properties"]["height"])
            ids = [n["id"] for root in roots for n in walk(root)]
            self.assertEqual(len(ids), len(set(ids)))
            sets.append(set(ids))
        self.assertFalse(sets[0] & sets[1] or sets[0] & sets[2] or sets[1] & sets[2])

    def test_every_exported_stable_id_and_reference_matches_the_public_codec(self):
        # GameplaySkinStableIdentityId.Validate: each dot-delimited segment
        # begins with ASCII a-z, ends with a-z/0-9, and otherwise allows '-'.
        # GameplaySkinSceneCodec.getStableId additionally limits length to128.
        segment = r"[a-z](?:[a-z0-9-]*[a-z0-9])?"
        stable_id = re.compile(segment + r"(?:\." + segment + r")*")

        def fields(value, parent=None):
            if isinstance(value, dict):
                for key, child in value.items():
                    if key == "id" or key in ("target", "template", "bitmap-font", "animation", "initial", "from", "to"):
                        if isinstance(child, str):
                            yield key, child
                    elif key == "resource" and parent != "material":
                        yield key, child
                    yield from fields(child, key)
            elif isinstance(value, list):
                for child in value:
                    yield from fields(child, parent)

        for layout in ("p1", "p2", "dp"):
            value = build_graph(self.library, layout)
            public = {key: value[key] for key in ("resources", "bitmapFonts", "templates", "instances", "bindings", "variants", "tracks")}
            entries = list(fields(public))
            declarations = [identifier for key, identifier in entries if key == "id"]
            self.assertEqual(len(declarations), len(set(declarations)))
            for key, identifier in entries:
                with self.subTest(layout=layout, field=key, identifier=identifier):
                    self.assertLessEqual(len(identifier), 128)
                    self.assertIsNotNone(stable_id.fullmatch(identifier))
                    if key != "id":
                        self.assertIn(identifier, declarations)
            # Files retain original names; only the public identity is encoded.
            self.assertTrue(any("gr_sco_00.png" in row["path"] for row in value["files"]))
            self.assertIn("standard.graph.texture.gr-sco-00", declarations)

    def test_other_hdd_identity_and_changed_sprite_are_rejected(self):
        self.metadata["packages"].append({"source": "contents/data/graphic/gmframe33.ifs"})
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "independent IIDX26"):
            build_graph(self.library, "p1")
        self.metadata["packages"].pop()
        self.metadata["packages"][0]["sha256"] = "0" * 64
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "archive differs"):
            build_graph(self.library, "p1")
        self.metadata["packages"][0]["sha256"] = self.source_sha256
        self.write_metadata()
        (self.library / "sprites/gr_sco_00.png").write_bytes(b"changed")
        with self.assertRaisesRegex(ValueError, "bytes changed"):
            build_graph(self.library, "p1")


if __name__ == "__main__":
    unittest.main()
