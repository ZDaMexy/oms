"""Different old Graph art must not silently acquire the later source geometry."""
import copy
import hashlib
import json
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import test_standard_graph as legacy
from standard_graph import build_graph, build_graph_target
from standard_graph_targets import export_graph_target_module
from standard_source_catalog import COMPOSITION, EARLIER_ALIASES


class EarlierGraphTests(unittest.TestCase):
    def setUp(self):
        # Reuse the established synthetic 0/1 library, not private source art.
        fixture = legacy.StandardGraphTests("test_base_marks_pb_as_terminal_and_missing_pb_bar_follows_current")
        fixture.setUp()
        self.addCleanup(fixture.doCleanups)
        self.library, self.font = fixture.library, fixture.font
        self.metadata = fixture.metadata
        self.metadata["sourceComposition"] = {"contract": COMPOSITION, "aliases24": EARLIER_ALIASES}
        self.metadata["packages"].extend({"source": f"contents/data/graphic/gmframe{style}.ifs"} for style in (23, 24))
        for family in ("s23", "s24"):
            package = f"contents/data/graphic/{family}/graph.ifs"
            sizes = {f"gr_sco_{i:02}": (23, 22) for i in range(10)}
            sizes.update({"gr_score_graph_panel": (280, 720), "gr_gt_pacemaker": (162, 28),
                          "gr_p_nowsc": (135, 31), "gr_p_bstsc": (135, 31), "gr_score_tag_2": (135, 31)})
            sizes.update({f"gr_sco_gauge_{i}": (66, 448) for i in range(3)})
            sizes.update({"gr_line_" + rank: (270, 15) if family == "s23" else (272, 18) for rank in ("aaa", "aa", "a")})
            if family == "s24":
                sizes.update({"gr_score_graph_max": (276, 20), "gr_score_graph_line": (272, 6)})
            for name, (width, height) in sizes.items():
                source = self.library / f"synthetic-{family}/{name}.png"
                source.parent.mkdir(exist_ok=True)
                source.write_bytes(f"synthetic earlier Graph: {family}/{name}".encode("ascii"))
                self.metadata["textures"].append({"package": package, "name": name + ".png", "width": width, "height": height,
                    "sprite": source.relative_to(self.library).as_posix(), "sha256": hashlib.sha256(source.read_bytes()).hexdigest()})
            archive = self.library / "source-copies" / package
            archive.parent.mkdir(parents=True)
            archive.write_bytes(f"synthetic earlier Graph archive: {family}".encode("ascii"))
            self.metadata["packages"].append({"source": package, "sha256": hashlib.sha256(archive.read_bytes()).hexdigest()})
            texture_list = self.library / f"packages/contents/data/graphic/{family}/graph/raw/tex/texturelist.xml"
            texture_list.parent.mkdir(parents=True)
            texture_list.write_text('<texturelist><texture mag_filter="nearest">' +
                "".join(f'<image name="{name}" />' for name in sizes) + '</texture></texturelist>', encoding="utf-8")
        self.write_metadata()

    def write_metadata(self):
        (self.library / "library.json").write_text(json.dumps(self.metadata), encoding="utf-8")

    @staticmethod
    def nodes(contribution):
        return {n["id"]: n for template in contribution["templates"] for n in legacy.walk(template["root"])}

    def test_earlier_anchors_and_full_png_sizes_follow_the_qualified_movies(self):
        for family in ("s23", "s24"):
            for layout in ("p1", "p2", "dp"):
                with self.subTest(family=family, layout=layout):
                    value = build_graph(self.library, layout, self.font, common_family=family)
                    target = build_graph_target(self.library, layout, "aaa", self.font, common_family=family)
                    prefix = f"standard.graph.f{family}.{layout}"
                    target_prefix = f"standard.graph.f{family}.target.aaa.{layout}"
                    nodes, target_nodes = self.nodes(value), self.nodes(target)
                    left = 0 if layout == "p2" else 1000
                    score = nodes[prefix + ".current.value"]["properties"]
                    self.assertAlmostEqual(left + 178, score["x"] * 1280)
                    self.assertAlmostEqual(30, score["y"] * 720)
                    header = target_nodes[target_prefix + ".header"]["properties"]
                    self.assertEqual({"x": (left + 1) / 1280, "y": 54 / 720, "width": 162 / 1280, "height": 28 / 720}, header)
                    self.assertAlmostEqual(left + 178, target_nodes[target_prefix + ".total"]["properties"]["x"] * 1280)
                    self.assertEqual(f"standard.graph.f{family}.texture.gr-score-tag-2", target_nodes[target_prefix + ".tag"]["resource"])
                    for rank in ("aaa", "aa", "a"):
                        line = nodes[prefix + ".line." + rank]["properties"]
                        self.assertAlmostEqual(270 if family == "s23" else 272, line["width"] * 1280)
                        self.assertAlmostEqual(15 if family == "s23" else 18, line["height"] * 720)
                        track = next(t for t in value["tracks"] if t["clock"] == f"score.rank-{rank}-progress")
                        self.assertEqual([527 / 720, 77 / 720], [frame["value"] for frame in track["keyframes"]])
                    self.assertEqual(450, value["receipt"]["sourceGeometry"]["rankNativeScale"])
                    self.assertEqual(448, value["receipt"]["barPlacement"]["bottom"] - value["receipt"]["barPlacement"]["top"])
                    self.assertEqual("0x6b540", value["receipt"]["callerBasis"]["rankPlacementRVA"])
                    self.assertIn("supplied IIDX24", value["receipt"]["familyScope"])
                    if family == "s23":
                        self.assertIn("no IIDX23 executable", value["receipt"]["familyScope"])

    def test_baked_copula_lines_are_not_duplicated_and_sinobuz_uses_its_real_names(self):
        copula = build_graph(self.library, "p1", common_family="s23")
        sinobuz = build_graph(self.library, "p1", common_family="s24")
        self.assertFalse(any(name.endswith((".line.maximum", ".line.baseline")) for name in self.nodes(copula)))
        nodes = self.nodes(sinobuz)
        self.assertEqual("standard.graph.fs24.texture.gr-score-graph-max", nodes["standard.graph.fs24.p1.line.maximum"]["resource"])
        self.assertEqual("standard.graph.fs24.texture.gr-score-graph-line", nodes["standard.graph.fs24.p1.line.baseline"]["resource"])
        for value in (copula, sinobuz):
            self.assertFalse(any(row["path"].endswith(("/gr_line_max.png", "/gr_line_under.png")) for row in value["files"]))
            self.assertTrue(all(row["path"].startswith(f"standard/graph/f{value['receipt']['commonFamily']}/") for row in value["files"]))

    def test_missing_old_art_is_rejected_even_when_later_family_has_the_same_role(self):
        self.metadata["textures"] = [row for row in self.metadata["textures"] if not
            (row["package"] == "contents/data/graphic/s24/graph.ifs" and row["name"] == "gr_score_tag_2.png")]
        self.write_metadata()
        with self.assertRaises(KeyError):
            build_graph_target(self.library, "p1", "aaa", self.font, common_family="s24")
        # The available later red tag is never selected as a fallback.
        build_graph_target(self.library, "p1", "aaa", self.font, common_family="0")

    def test_unexpected_source_dimensions_fail_instead_of_rescaling_to_later_size(self):
        row = next(row for row in self.metadata["textures"] if row["package"] == "contents/data/graphic/s23/graph.ifs" and row["name"] == "gr_line_aaa.png")
        row["width"], row["height"] = 272, 18
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "dimensions changed"):
            build_graph(self.library, "p1", common_family="s23")

    def test_existing_26_geometry_is_unchanged_in_the_expanded_library(self):
        for family in ("0", "1"):
            value = build_graph(self.library, "p1", self.font, common_family=family)
            target = build_graph_target(self.library, "p1", "aaa", self.font, common_family=family)
            prefix = "standard.graph" + (".f1" if family == "1" else "")
            nodes, targets = self.nodes(value), self.nodes(target)
            self.assertEqual(1183 / 1280, nodes[prefix + ".p1.current.value"]["properties"]["x"])
            self.assertEqual(28 / 720, nodes[prefix + ".p1.current.value"]["properties"]["y"])
            self.assertEqual(160 / 1280, targets[prefix + ".target.aaa.p1.header"]["properties"]["width"])
            self.assertEqual(prefix + ".texture.gr-score-tag-red", targets[prefix + ".target.aaa.p1.tag"]["resource"])
            track = next(t for t in value["tracks"] if t["clock"] == "score.rank-aaa-progress")
            self.assertEqual([528 / 720, 80 / 720], [frame["value"] for frame in track["keyframes"]])

    def test_target_variants_select_qualified_source_for_every_earlier_layout(self):
        font_before = copy.deepcopy(self.font)
        output = self.library.parent / "earlier-graph-module"
        value = export_graph_target_module(self.library, output, self.font)
        self.assertEqual(72, len(value["receipts"]))
        for choice in value["module"]["choices"]:
            for style, family in ((23, "s23"), (24, "s24"), (25, "1"), (26, "0")):
                for layout in ("p1", "p2", "dp"):
                    selected = {"frame": f"iidx{style}"}
                    if layout != "dp":
                        selected["layout"] = layout
                    branch = next((variant for variant in choice["variants"]
                        if all(selected.get(key) == setting for key, setting in variant["when"].items())), choice)
                    suffix = "" if family == "0" else "/f" + family
                    self.assertEqual(f"standard/graph{suffix}/targets/{choice['value']}/{layout}/scene.json", branch["scene"])
        self.assertEqual(font_before, self.font)
        for row in value["files"]:
            self.assertEqual(row["sha256"], hashlib.sha256(Path(row["source"]).read_bytes()).hexdigest())


if __name__ == "__main__":
    unittest.main()
