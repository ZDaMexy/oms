"""Real-policy, geometry and source-boundary checks for the independent26 Graph."""
import copy
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_graph import BAR_BOTTOM, BAR_TOP, FRAME_IDS, PACKAGE, GraphOPTIONS, TARGETS, bar_columns, build_graph, build_graph_target
from standard_graph_targets import export_graph_target_module


def walk(root):
    yield root
    for child in root.get("children", []):
        yield from walk(child)


def f32(value):
    return struct.unpack("<f", struct.pack("<f", value))[0]


def state(maximum=400, best=267, judged=100, score=170, percent=80, empty_poor=0):
    value = {"score.maximum": maximum, "score.personal-best": best, "score.value": score,
             "option.graph-custom-percent": percent, "judgement.count.ok": empty_poor}
    value.update({"score.rank-" + rank: (maximum * multiplier + 8) // 9 for rank, multiplier in (("aaa", 8), ("aa", 7), ("a", 6))})
    value.update({"judgement.count." + name: judged if name == "perfect" else 0 for name in ("perfect", "great", "good", "meh", "miss")})
    return value


def evaluate(contribution, sources):
    values = dict(sources)
    for row in contribution["numericSources"]:
        operands = [values.get(row[field]) if isinstance(row[field], str) else row[field] for field in ("left", "right") if field in row]
        a = operands[0]
        b = operands[1] if len(operands) == 2 else None
        if any(value is None for value in operands):
            result = None
        else:
            operation = row["operation"]
            if operation == "add": result = a + b
            elif operation == "subtract": result = a - b
            elif operation == "multiply": result = a * b
            elif operation == "divide": result = a / b if b else None
            elif operation == "min": result = min(a, b)
            elif operation == "max": result = max(a, b)
            elif operation == "floor": result = math.floor(a)
            elif operation == "ceil": result = math.ceil(a)
            elif operation == "float32": result = f32(a)
            elif operation == "multiply32": result = f32(f32(a) * f32(b))
            elif operation == "divide32": result = f32(f32(a) / f32(b)) if f32(b) else None
            else: raise AssertionError(operation)
        values["value." + row["id"]] = result
    return values


def native_next(maximum, best):
    best = max(0, min(maximum, best))
    rank = max(0, min(7, 8 - 9 * best // maximum))
    threshold = lambda r: ((8 - r) * maximum + 8) // 9
    if rank > 2:
        return threshold(2)
    lower = threshold(rank)
    upper = maximum if rank == 0 else threshold(rank - 1)
    middle = (lower + upper) // 2
    return middle if best < middle else upper


def native_next_plus(maximum, best):
    best = max(0, min(maximum, best))
    lower = (maximum * 6 + 8) // 9
    rate = f32(.005)
    span = maximum - lower
    ratio = f32(f32(f32(maximum - best) * rate) / f32(span)) if span else 0
    ratio = max(0, min(rate, ratio))
    increment = max(1, int(f32(f32(maximum) * ratio)))
    return max(lower, min(maximum, best + increment))


class StandardGraphTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.library = Path(self.temporary.name) / "library"
        self.library.mkdir()
        sizes = {f"gr_sco_{i:02}": (23, 22) for i in range(10)}
        sizes.update({"gr_score_graph_panel": (280, 720), "gr_line_max": (276, 20), "gr_line_under": (272, 6),
                      "gr_p_nowsc": (135, 31), "gr_p_bstsc": (135, 31), "gr_score_tag_red": (135, 31), "gr_gt_pacemaker": (160, 28)})
        sizes.update({"gr_line_" + rank: (272, 18) for rank in ("aaa", "aa", "a")})
        sizes.update({"gr_sco_gauge_" + str(i): (66, 448) for i in range(3)})
        textures = []
        for name, (width, height) in sizes.items():
            path = self.library / ("sprites/" + name + ".png")
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(("source-byte-fixture:" + name).encode("ascii"))
            textures.append({"package": PACKAGE, "name": name + ".png", "width": width, "height": height,
                "sprite": path.relative_to(self.library).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        archive = self.library / "source-copies" / PACKAGE
        archive.parent.mkdir(parents=True)
        archive.write_bytes(b"unit-test archive identity, not original game data")
        self.source_sha256 = hashlib.sha256(archive.read_bytes()).hexdigest()
        self.metadata = {"status": "complete", "sourceMetadataUnchanged": True,
            "packages": [{"source": PACKAGE, "sha256": self.source_sha256}]
            + [{"source": f"contents/data/graphic/gmframe{frame:02}.ifs"} for frame in FRAME_IDS], "textures": textures}
        family1_package = "contents/data/graphic/1/graph.ifs"
        family1_sizes = {("gr_score_graph_line" if name == "gr_line_under" else name): size for name, size in sizes.items()}
        for name, (width, height) in family1_sizes.items():
            path = self.library / ("sprites-family1/" + name + ".png")
            path.parent.mkdir(exist_ok=True)
            path.write_bytes(("family1-source-byte-fixture:" + name).encode("ascii"))
            textures.append({"package": family1_package, "name": name + ".png", "width": width, "height": height,
                "sprite": path.relative_to(self.library).as_posix(), "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
        family1_archive = self.library / "source-copies" / family1_package
        family1_archive.parent.mkdir(parents=True)
        family1_archive.write_bytes(b"unit-test retained family1 archive identity")
        self.metadata["packages"].append({"source": family1_package, "sha256": hashlib.sha256(family1_archive.read_bytes()).hexdigest()})
        self.write_metadata()
        for family, family_sizes in (("0", sizes), ("1", family1_sizes)):
            xml = self.library / f"packages/contents/data/graphic/{family}/graph/raw/tex/texturelist.xml"
            xml.parent.mkdir(parents=True)
            xml.write_text('<texturelist><texture mag_filter="nearest">' + "".join(f'<image name="{name}" />' for name in family_sizes) + '</texture></texturelist>', encoding="utf-8")
        font = Path(self.temporary.name) / "font.png"
        font.write_bytes(b"prepared-font-byte-fixture")
        resources = [{"id": f"fixture.font.g{i}", "type": "texture", "path": "fixture-font.png"} for i in range(13)]
        self.font = {"fonts": {"small": "fixture.font.small"}, "resources": resources,
            "bitmapFonts": [{"id": "fixture.font.small", "lineHeight": 18,
                "glyphs": [{"character": c, "resource": resources[i]["id"], "advance": 8} for i, c in enumerate("0123456789-+.")]}],
            "files": [{"path": "fixture-font.png", "source": str(font), "sha256": hashlib.sha256(font.read_bytes()).hexdigest()}]}

    def write_metadata(self):
        (self.library / "library.json").write_text(json.dumps(self.metadata), encoding="utf-8")

    def test_cutins_share_one_real_crossing_flash_and_keep_only_the_highest_rank(self):
        clips = {name: {"node": {"id": "source." + name, "type": "recorded-animation", "target": {"kind": "global"},
            "blend": "alpha", "animation": "clip." + name, "properties": {}, "effects": [], "children": []},
            "resources": [], "animations": [{"id": "clip." + name, "path": name + ".json.br", "resources": []}]}
            for name in ("mybest", "target", "aaa", "aa", "a", "update")}
        for family in ("0", "1"):
            for layout in ("p1", "p2", "dp"):
                for target in TARGETS:
                    with self.subTest(family=family, layout=layout, target=target):
                        value = build_graph_target(self.library, layout, target, self.font,
                            common_family=family, notifications=clips)
                        nodes = {n["id"]: n for t in value["templates"] for n in walk(t["root"])}
                        notifications = [n for n in nodes.values() if n["type"] == "recorded-animation"]
                        self.assertEqual(len(notifications), 6)
                        update, = (n for n in notifications if n["id"].endswith(".update"))
                        thresholds = update["scoreCrossing"]["thresholds"]
                        self.assertEqual(len(thresholds), 5)
                        self.assertEqual([t["comparison"] for t in thresholds], ["greater", "greater", "at-least", "at-least", "at-least"])
                        self.assertEqual(thresholds[1]["source"], value["receipt"]["targetTotalSource"])
                        self.assertEqual(update["properties"]["z"], 1)
                        for notification in notifications:
                            if notification is not update:
                                self.assertEqual(len(notification["scoreCrossing"]["thresholds"]), 1)
                                self.assertEqual(notification["properties"]["z"], 0)
                        self.assertLessEqual(len(value["numericSources"]) + 4, 64)  # frame calculations and two original steady-frame clocks
                        self.assertEqual(len(value["animations"]), 6)
                        self.assertTrue(any(b["source"] == "option.graph-cutin" for b in value["bindings"]))
                        gates = {n["id"]: next(b["source"] for b in value["bindings"] if b["target"] == n["id"] and b["property"] == "opacity")
                                 for n in nodes.values() if n["id"].endswith(".rank-gate")}
                        for current, expected in ((267, (1, 1)), (312, (0, 1)), (356, (0, 0))):
                            values = evaluate(value, state(score=current))
                            a = next(source for identity, source in gates.items() if identity.endswith(".a.rank-gate"))
                            aa = next(source for identity, source in gates.items() if identity.endswith(".aa.rank-gate"))
                            self.assertEqual((values[a], values[aa]), expected)
        self.assertTrue(all("scoreCrossing" not in clip["node"] for clip in clips.values()))

    def test_source_panel_surrounds_art_and_data_at_exact_global_marker(self):
        value = build_graph(self.library, "p1", self.font)
        roots = [template["root"] for template in value["templates"]]
        self.assertEqual([-10, -30], [root["properties"]["z"] for root in roots])
        for instance in value["instances"]:
            self.assertEqual({"slot": "decoration", "resource": "markers/frame-p1-information"}, instance["material"])
        self.assertEqual({root["id"] for root in roots}, {b["target"] for b in value["bindings"] if b["source"] == "option.graph-visible"})

    def test_base_marks_pb_as_terminal_and_missing_pb_bar_follows_current(self):
        value = build_graph(self.library, "p1", self.font)
        bindings = {(b["target"], b["property"]): b["source"] for b in value["bindings"]}
        for name, source in (("current", "score.progress"), ("best", "score.personal-best-progress"), ("best-fallback", "score.progress")):
            self.assertEqual(source, bindings[f"standard.graph.p1.bar.{name}.clip", "reveal-x"])
        self.assertEqual("score.personal-best-available", bindings["standard.graph.p1.best.available", "visible"])
        nodes = [n for t in value["templates"] for n in walk(t["root"])]
        captions = {n["properties"].get("text") for n in nodes}
        best_label = next(n for n in nodes if n["id"] == "standard.graph.p1.best.label")
        self.assertEqual("standard.graph.texture.gr-p-bstsc", best_label["resource"])
        self.assertEqual("score.personal-best", bindings["standard.graph.p1.best.value", "text"])
        self.assertIn("EX PACEMAKER: BMS ONLY", captions)
        self.assertNotIn("NO DATA", captions)
        self.assertNotIn("MAXIMUM", captions)
        values = evaluate(value, state(score=100, best=150))
        self.assertEqual(-50, values["value.standard.graph.p1.value.pb-difference"])
        self.assertEqual("fixture.font.small", next(n["properties"]["bitmap-font"] for n in nodes if n["id"].endswith(".best.difference")))

    def test_original_bars_are_cropped_without_texture_compression_and_p2_mirrors_roles(self):
        for layout, panel in (("p1", 1000), ("p2", 0), ("dp", 1000)):
            value = build_graph(self.library, layout, self.font)
            nodes = {n["id"]: n for t in value["templates"] for n in walk(t["root"])}
            for role in ("current", "best"):
                outer = nodes[f"standard.graph.{layout}.bar.{role}"]["properties"]
                inner = nodes[f"standard.graph.{layout}.bar.{role}.fill"]["properties"]
                self.assertEqual(-90, outer["rotation"])
                self.assertEqual(90, inner["rotation"])
                self.assertEqual("bottom-left", inner["origin"])
                self.assertAlmostEqual(panel + bar_columns(layout)[role], outer["x"] * 1280)
                self.assertAlmostEqual(BAR_BOTTOM, outer["y"] * 720)
                self.assertAlmostEqual(66, outer["width"] * 1280 * inner["width"])
                self.assertAlmostEqual(448, outer["height"] * 720 * inner["height"] * inner["scale-y"])
                for ratio in (0, .1, .5, 1):
                    # Opposing rotations yield original sx/sy, with the
                    # retained interval sy in [448*(1-ratio),448].
                    self.assertAlmostEqual(BAR_BOTTOM - 448 * ratio, BAR_TOP + 448 * (1 - ratio))
            self.assertEqual({"current": 202, "best": 128, "target": 54} if layout == "p2" else {"current": 54, "best": 128, "target": 202}, bar_columns(layout))

    def test_rank_strokes_keep_original_offsets_and_availability(self):
        value = build_graph(self.library, "p1")
        self.assertTrue(any(b["source"] == "score.rank-available" and b["property"] == "visible" for b in value["bindings"]))
        for rank in ("aaa", "aa", "a"):
            track = next(t for t in value["tracks"] if t["clock"] == f"score.rank-{rank}-progress")
            first, last = track["keyframes"]
            for ratio in (0, 2 / 3, 7 / 9, 8 / 9, 1):
                y = first["value"] + (last["value"] - first["value"]) * ratio
                self.assertAlmostEqual(BAR_BOTTOM - 448 * ratio, y * 720 + 14)

    def test_six_targets_match_original_policy_and_leave_headroom_for_base_and_pr(self):
        self.assertEqual(["aaa", "aa", "a", "custom", "next", "next-plus"], list(TARGETS))
        for target in TARGETS:
            value = build_graph_target(self.library, "p1", target, self.font)
            self.assertLessEqual(len(value["numericSources"]) + 2, 64)
            self.assertLessEqual(value["receipt"]["numericDepth"], 16)
            self.assertTrue(any(b["source"] == "score.rank-available" for b in value["bindings"]))
            self.assertFalse(any("score.pacemaker-next" in str(row) for row in value["numericSources"]))
            sources = state(maximum=222, best=150, percent=83)
            values = evaluate(value, sources)
            expected = {"aaa": 198, "aa": 173, "a": 148, "custom": 184, "next": native_next(222, 150), "next-plus": native_next_plus(222, 150)}[target]
            self.assertEqual(expected, values[value["receipt"]["targetTotalSource"]])
        custom = build_graph_target(self.library, "dp", "custom", self.font)
        for percent in (0, 1, 50, 99, 100):
            values = evaluate(custom, state(maximum=222, percent=percent))
            self.assertEqual(222 * percent // 100, values[custom["receipt"]["targetTotalSource"]])

    def test_next_integer_midpoints_and_next_plus_float32_boundaries(self):
        next_value = build_graph_target(self.library, "p1", "next", self.font)
        plus = build_graph_target(self.library, "p1", "next-plus", self.font)
        self.assertEqual(23, evaluate(next_value, state(maximum=32, best=22))[next_value["receipt"]["targetTotalSource"]])
        for maximum, best, expected in ((654, 454, 456), (618, 418, 421), (400, 267, 269)):
            self.assertEqual(expected, evaluate(plus, state(maximum=maximum, best=best))[plus["receipt"]["targetTotalSource"]])
        for notes in range(1, 70):
            maximum = notes * 2
            for best in range(maximum + 2):
                for value, oracle in ((next_value, native_next), (plus, native_next_plus)):
                    calculated = evaluate(value, state(maximum=maximum, best=best))[value["receipt"]["targetTotalSource"]]
                    self.assertEqual(oracle(maximum, best), calculated, (notes, best, value["receipt"]["target"]))

    def test_target_advances_only_real_scoring_judgements_with_native_ceilf(self):
        value = build_graph_target(self.library, "p2", "next-plus", self.font)
        for judged in (0, 1, 100, 200, 327):
            sources = state(maximum=654, best=454, judged=judged, empty_poor=999999)
            values = evaluate(value, sources)
            expected = math.ceil(f32(f32(f32(judged) * f32(456)) / f32(327)))
            self.assertEqual(expected, values[value["receipt"]["targetPaceSource"]])
            self.assertEqual(expected / 654, values[value["receipt"]["targetProgressSource"]])
            self.assertEqual(170 - expected, values[value["receipt"]["differenceSource"]])
        self.assertFalse(any("judgement.count.ok" in str(row) or "combo.breaks" in str(row) for row in value["numericSources"]))
        zero = evaluate(value, state(maximum=0, best=0, judged=0))
        self.assertIsNone(zero[value["receipt"]["targetPaceSource"]])

    def test_next_compact_candidates_keep_large_chart_boundary_rounding(self):
        value = build_graph_target(self.library, "p1", "next", self.font)
        for notes in (1000, 30143, 1_000_003, 2_147_483_647):
            maximum = notes * 2
            ranks = [(maximum * multiplier + 8) // 9 for multiplier in (6, 7, 8)] + [maximum]
            boundaries = ranks + [(lower + upper) // 2 for lower, upper in zip(ranks, ranks[1:])]
            probes = {0, maximum + 1} | {max(0, edge + delta) for edge in boundaries for delta in (-1, 0, 1)}
            for best in probes:
                with self.subTest(notes=notes, best=best):
                    actual = evaluate(value, state(maximum=maximum, best=best))[value["receipt"]["targetTotalSource"]]
                    self.assertEqual(native_next(maximum, best), actual)
                    self.assertGreaterEqual(actual, ranks[0])
                    self.assertLessEqual(actual, maximum)

    def test_every_selected_id_reference_and_numeric_dependency_is_public_and_bounded(self):
        pattern = re.compile(r"[a-z](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z](?:[a-z0-9-]*[a-z0-9])?)*")
        for layout in ("p1", "p2", "dp"):
            base = build_graph(self.library, layout, self.font)
            for target in TARGETS:
                value = build_graph_target(self.library, layout, target, self.font)
                declarations = base["numericSources"] + value["numericSources"]
                known = set()
                for row in declarations:
                    self.assertRegex(row["id"], pattern)
                    self.assertNotIn(row["id"], known)
                    for field in ("left", "right"):
                        operand = row.get(field)
                        if isinstance(operand, str) and operand.startswith("value."):
                            self.assertIn(operand[6:], known)
                    known.add(row["id"])
                nodes = [n for part in (base, value) for template in part["templates"] for n in walk(template["root"])]
                ids = [n["id"] for n in nodes]
                self.assertEqual(len(ids), len(set(ids)))
                for identifier in ids:
                    self.assertIsNotNone(pattern.fullmatch(identifier))
                    self.assertLessEqual(len(identifier), 128)
                for binding in base["bindings"] + value["bindings"]:
                    self.assertIn(binding["target"], ids)
                    if binding["source"].startswith("value."):
                        self.assertIn(binding["source"][6:], known)

    def test_export_module_is_bms_only_six_choices_and_preserves_all_exact_files(self):
        output = Path(self.temporary.name) / "author"
        result = export_graph_target_module(self.library, output, self.font)
        self.assertEqual(["bms"], result["module"]["rulesets"])
        self.assertEqual("aaa", result["module"]["default"])
        self.assertEqual(list(TARGETS), [choice["value"] for choice in result["module"]["choices"]])
        self.assertEqual(GraphOPTIONS, result["options"])
        self.assertEqual(36, len(result["receipts"]))
        for row in result["files"]:
            self.assertEqual(row["sha256"], hashlib.sha256((output / row["path"]).read_bytes()).hexdigest())
        first = result["module"]["choices"][0]
        self.assertTrue(first["scene"].endswith("/dp/scene.json"))
        self.assertTrue(first["orderedVariants"])
        self.assertEqual([{"frame": "iidx25", "layout": "p1"}, {"frame": "iidx25", "layout": "p2"},
                          {"frame": "iidx25"}, {"layout": "p1"}, {"layout": "p2"}],
                         [variant["when"] for variant in first["variants"]])
        for choice in result["module"]["choices"]:
            for frame in FRAME_IDS:
                for layout in ("p1", "p2", "dp"):
                    selectors = {"frame": f"iidx{frame:02}"}
                    if layout != "dp":
                        selectors["layout"] = layout
                    selected = next((variant for variant in choice["variants"]
                                     if all(selectors.get(key) == value for key, value in variant["when"].items())), choice)
                    family_path = "/f1" if frame == 25 else ""
                    self.assertEqual(f"standard/graph{family_path}/targets/{choice['value']}/{layout}/scene.json", selected["scene"])
        self.assertEqual(result, export_graph_target_module(self.library, output, self.font))

    def test_family1_uses_qualified_art_with_separate_ids_paths_and_source_baseline(self):
        original_font = copy.deepcopy(self.font)
        zero = build_graph(self.library, "p1", self.font)
        self.assertEqual(zero, build_graph(self.library, "p1", self.font, common_family="0"))
        one = build_graph(self.library, "p1", self.font, common_family="1")
        self.assertEqual("contents/data/graphic/1/graph.ifs", one["receipt"]["sourcePackage"])
        self.assertEqual("1", one["receipt"]["commonFamily"])
        self.assertEqual(original_font, self.font)
        for field in ("resources", "bitmapFonts"):
            shared = {row["id"] for row in self.font[field]}
            self.assertEqual(shared, {row["id"] for row in zero[field]} & {row["id"] for row in one[field]})
            self.assertEqual([row for row in zero[field] if row['id'] in shared],
                             [row for row in one[field] if row['id'] in shared])
        common_paths = {row["path"] for row in self.font["files"]}
        self.assertEqual(common_paths, {row["path"] for row in zero["resources"]} & {row["path"] for row in one["resources"]})
        self.assertTrue(all(row["path"] in common_paths or row["path"].startswith("standard/graph/f1/") for row in one["files"]))
        self.assertTrue(all(row["sha256"] == hashlib.sha256(Path(row["source"]).read_bytes()).hexdigest() for row in one["files"]))
        nodes = {n["id"]: n for template in one["templates"] for n in walk(template["root"])}
        baseline = nodes["standard.graph.f1.p1.line.baseline"]
        self.assertEqual("standard.graph.f1.texture.gr-score-graph-line", baseline["resource"])
        self.assertEqual({"x": 1004 / 1280, "y": 540 / 720, "width": 272 / 1280, "height": 6 / 720}, baseline["properties"])
        current = next(row for row in one["files"] if row["path"].endswith("/gr_p_nowsc.png"))
        self.assertEqual(self.library / "sprites-family1/gr_p_nowsc.png", Path(current["source"]))
        zero_nodes = {n["id"].replace("standard.graph.", "standard.graph.f1."): n for template in zero["templates"] for n in walk(template["root"])}
        for identifier, item in nodes.items():
            before = copy.deepcopy(zero_nodes[identifier]["properties"])
            if "bitmap-font" in before:
                before["bitmap-font"] = item["properties"]["bitmap-font"]
            self.assertEqual(before, item["properties"])
        pattern = re.compile(r"[a-z](?:[a-z0-9-]*[a-z0-9])?(?:\.[a-z](?:[a-z0-9-]*[a-z0-9])?)*")
        for target in TARGETS:
            value = build_graph_target(self.library, "dp", target, self.font, common_family="1")
            other = build_graph_target(self.library, "dp", target, self.font)
            self.assertEqual(evaluate(other, state())[other["receipt"]["targetTotalSource"]],
                             evaluate(value, state())[value["receipt"]["targetTotalSource"]])
            declarations = [row["id"] for field in ("resources", "bitmapFonts", "tracks", "bindings", "variants", "numericSources") for row in value[field]]
            declarations += [n["id"] for template in value["templates"] for n in walk(template["root"])]
            for identifier in declarations:
                self.assertIsNotNone(pattern.fullmatch(identifier))
                self.assertLessEqual(len(identifier), 128)

    def test_original_personal_best_tag_uses_source_slot_and_real_availability(self):
        for family in ("0", "1"):
            for layout in ("p1", "p2", "dp"):
                value = build_graph(self.library, layout, self.font, common_family=family)
                prefix = "standard.graph" + (".f1" if family == "1" else "") + "." + layout
                nodes = {n["id"]: n for template in value["templates"] for n in walk(template["root"])}
                label = nodes[prefix + ".best.label"]
                self.assertEqual("sprite", label["type"])
                self.assertEqual("standard.graph" + (".f1" if family == "1" else "") + ".texture.gr-p-bstsc", label["resource"])
                self.assertEqual({"x": ((0 if layout == "p2" else 1000) + 140) / 1280,
                                  "y": 567 / 720, "width": 135 / 1280, "height": 31 / 720}, label["properties"])
                gate = nodes[prefix + ".best.available"]
                self.assertIn(label, gate["children"])
                bindings = {(row["target"], row["property"]): row["source"] for row in value["bindings"]}
                self.assertEqual("score.personal-best-available", bindings[gate["id"], "visible"])
                self.assertEqual("score.personal-best", bindings[prefix + ".best.value", "text"])
                self.assertFalse(any("gr_mybest_update" in row["path"] for row in value["files"]))

    def test_family1_does_not_fall_back_to_family0_when_unqualified(self):
        for family in ("2", "../1", 1):
            with self.assertRaisesRegex(ValueError, "common family"):
                build_graph(self.library, "p1", common_family=family)
        row = self.metadata["packages"][-1]
        row["sha256"] = "0" * 64
        self.write_metadata()
        with self.assertRaisesRegex(ValueError, "archive differs"):
            build_graph(self.library, "p1", common_family="1")
        # The unchanged family remains usable and does not consume the bad one.
        build_graph(self.library, "p1")

    def test_other_hdd_identity_changed_sprite_and_wrong_font_are_rejected(self):
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
