"""Source boundaries and numeric layout regressions, using synthetic art only."""
import base64
import contextlib
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import MagicMock, patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import build_standard_skin as builder
from build_standard_skin import Frame, LOGICAL, Package, box, contained
from customization_modules import material_contexts


class SyntheticOwner:
    empty_path = "synthetic/empty.png"
    solid_path = "synthetic/solid.png"

    def __init__(self, gauge_size):
        self.gauge_size = gauge_size
        self.dimensions = {"std.empty": (1, 1)}

    def asset(self, package, name):
        if name.startswith("playm_gg_"):
            width, height = self.gauge_size
        elif name.startswith("playm_hs_"):
            width, height = 14, 12
        elif name.startswith("playm_bpm_m"):
            width, height = 19, 16
        elif name.startswith(("playm_sco", "playm_bpm")):
            width, height = 30, 22
        else:
            width, height = 300, 21
        identifier = "synthetic." + package.replace("/", ".") + "." + name
        self.dimensions[identifier] = width, height
        return ({"id": identifier, "type": "texture", "path": identifier + ".png"},
                {"width": width, "height": height})


def source_draw(hook, bounds, texture="synthetic", package="gmframe26"):
    return {"game_bindings": [hook], "bounds": bounds, "texture": texture,
            "asset": {"package": package}}


class StandardBuilderTests(unittest.TestCase):
    def frame(self, directory, layout="p1", *, gauge_size=(26, 24), advance=26, wide=False):
        """A complete minimal map, deliberately unrelated to private source art."""
        style = 25 if gauge_size == (28, 26) else 26
        player = "2p" if layout == "p2" else "1p"
        side = "dp_frame_1p" if layout == "dp" else f"{player}_frame"
        centre = ("center_frame_dp_1p" if layout == "dp" else
                  f"center_frame_wide_{player}" if wide else "center_frame_sp")
        score_left = {"p1": 100, "p2": 800, "dp": 400}[layout]
        centre_draws = [source_draw(f"score_{player}_0", box(score_left, 80, 30, 22))]
        for hook, left, width, height in ((f"maxcombo_{player}_0", 180, 30, 22),
                                          ("bpm_0", 500, 30, 22),
                                          ("bpm_min_0", 440, 19, 16),
                                          ("bpm_max_0", 590, 19, 16)):
            centre_draws.append(source_draw(hook, box(left, 110, width, height)))
        side_draws = [source_draw(f"hs_{player}_{i}", box(x, 200, 14, 12))
                      for i, x in enumerate((100, 113, 133, 146))]
        side_draws.append(source_draw(f"gg_bar_{player}", box(90, 550, 300, 21)))
        effect_left = (675 if layout == "p1" else 325) if wide else 500
        centre_draws.append(source_draw("effect", box(effect_left, 680, 254, 44)))
        if layout == "dp":
            centre_draws.extend(source_draw(f"lane_{p}", box(x, 0, 292, 484))
                                for p, x in (("1p", 300), ("2p", 680)))
        else:
            side_draws.append(source_draw(f"lane_{player}", box(50 if layout == "p1" else 930, 0, 292, 484)))
        width, height = gauge_size
        # Hundreds use a compressed native anchor, not another regular advance.
        positions = (300, 300 - advance, 300 - advance - 18)
        anchors = [box(x, 580 + (i == 2), width, height) for i, x in enumerate(positions)]
        side_draws.extend(source_draw(f"percent_{player}_{i}", bounds)
                          for i, bounds in enumerate(anchors))
        mapping = {"stage": {"width": 1280, "height": 720, "fps": 60.0},
                   "layouts": {side: {"draws": side_draws}, centre: {"draws": centre_draws}}}
        (directory / "skin-reference-map.json").write_text(json.dumps(mapping), encoding="utf-8")
        frame = Frame(SyntheticOwner(gauge_size), directory, style, layout, wide=wide)
        return frame, score_left, anchors

    def test_source_paths_cannot_cross_the_declared_package_root(self):
        with tempfile.TemporaryDirectory() as temporary:
            parent = Path(temporary).resolve()
            root, sibling = parent / "package", parent / "package-other"
            root.mkdir()
            sibling.mkdir()
            (sibling / "art.png").write_bytes(b"outside")
            (root / "safe art.png").write_bytes(b"inside")
            self.assertEqual(contained(root, "safe%20art.png"), root / "safe art.png")
            for relative in ("../package-other/art.png", "..%2Fpackage-other/art.png",
                             "..%5Cpackage-other%5Cart.png", str(sibling / "art.png")):
                with self.subTest(relative=relative), self.assertRaises(ValueError):
                    contained(root, relative)

    def test_identical_png_aliases_keep_one_resource_and_both_provenances(self):
        # Public one-pixel fixture; all hashes are computed locally by the test.
        png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJ"
                               "AAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==")
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            owner = object.__new__(Package)
            owner.library, owner.output = root / "library", root / "author"
            owner.library.mkdir()
            owner.output.mkdir()
            owner.files, owner.sources, owner.index = {}, {}, {}
            for package, name in (("frame-a", "digit"), ("frame-b", "alias")):
                source = f"{package}-{name}.png"
                (owner.library / source).write_bytes(png)
                owner.index[package, name] = {"sprite": source, "sha256": hashlib.sha256(png).hexdigest(),
                    "imageRect": [0, 1, 0, 1], "uvRect": [0, 1, 0, 1]}
            first, _ = owner.asset("frame-a", "digit")
            second, _ = owner.asset("frame-b", "alias")
            self.assertEqual(first, second)
            self.assertEqual((owner.output / first["path"]).read_bytes(), png)
            self.assertEqual(len(owner.files), 1)
            self.assertEqual(set(owner.sources), {("frame-a", "digit"), ("frame-b", "alias")})
            self.assertEqual(len(list(owner.output.rglob("*.png"))), 1)

            (owner.library / "frame-b-alias.png").write_bytes(png + b"changed")
            with self.assertRaisesRegex(ValueError, "Source changed"):
                owner.asset("frame-b", "alias")

    def test_copy_rejects_output_escape_and_conflicting_existing_content(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary).resolve()
            owner = object.__new__(Package)
            owner.output, owner.files = root / "author", {}
            owner.output.mkdir()
            source = root / "source.png"
            source.write_bytes(b"first")
            with self.assertRaisesRegex(ValueError, "escapes"):
                owner.copy(source, "../escape.png")
            self.assertFalse((root / "escape.png").exists())
            owner.copy(source, "art.png")
            source.write_bytes(b"replacement")
            with self.assertRaisesRegex(ValueError, "Conflicting author content"):
                owner.copy(source, "art.png")
            self.assertEqual((owner.output / "art.png").read_bytes(), b"first")

    def test_global_information_is_not_repeated_for_every_stage(self):
        for layout in ("p1", "p2", "dp"):
            with self.subTest(layout=layout), tempfile.TemporaryDirectory() as temporary:
                frame, _, _ = self.frame(Path(temporary), layout)
                contexts = [context for context in material_contexts() if context["layout"] == layout]
                controls = {lane["target"]: "synthetic/key"
                            for context in contexts for lane in context["lanes"]}
                # Layout generation is independent of the material fallback under test.
                frame.profiles = lambda: []
                frame.material_ini("synthetic/background", "synthetic/information", controls)
                sections = [block.splitlines() for block in "\n".join(frame.ini).split("[GameplaySkin.Common:1]")
                            if block.strip()]
                stages = [block for block in sections if any(line.startswith("Target: Stage ") for line in block)]
                globals_ = [block for block in sections if any(line.startswith("Target: Global ") for line in block)]
                self.assertEqual(len(stages), len(contexts))
                self.assertTrue(globals_)
                for block in stages:
                    self.assertIn("decoration: resource Suppress", block)
                for block in globals_:
                    self.assertIn('decoration: resource Provide "synthetic/information"', block)
                    target = next(line for line in block if line.startswith("Target: Global "))
                    if "ruleset=bms" in target.split():
                        self.assertIn("bga.frame: resource Suppress", block)
                    else:
                        self.assertIn("ruleset=mania", target.split())
                        self.assertFalse(any(line.startswith("bga.frame:") for line in block))

    def test_bga_viewport_does_not_translate_an_already_projected_aperture_twice(self):
        for layout, aperture_left in (("p1", 356), ("p2", 284)):
            with self.subTest(layout=layout), tempfile.TemporaryDirectory() as temporary:
                root = Path(temporary)
                frame, _, _ = self.frame(root, layout)
                frame.owner.output, frame.owner.receipts = root / "author", []
                frame.background = lambda: "synthetic/background"
                for method in ("numbers", "header", "gauge", "progress", "graph"):
                    setattr(frame, method, lambda: None)
                frame.template = lambda *args: "synthetic/information"
                frame.controls = lambda: {}
                frame.material_ini = lambda *args: []
                # apertures() measures the composition after centre translation.
                frame.apertures = lambda: [box(aperture_left, 141, 640, 480)]
                contribution = frame.build()
                ini = (frame.owner.output / contribution["ini"]).read_text(encoding="utf-8")
                viewports = [line.split(": ", 1)[1].split(",") for line in ini.splitlines()
                             if line.startswith("BgaViewports")]
                self.assertTrue(viewports)
                for viewport in viewports:
                    self.assertAlmostEqual(float(viewport[0]) * 1280, aperture_left)
                    self.assertAlmostEqual(float(viewport[2]) * 1280, 640)
                    self.assertEqual(viewport[4], "fit")

    def test_dp_keeps_both_left_monitors_with_the_public_hud_clip_recorded(self):
        from PIL import Image

        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            frame, _, _ = self.frame(root, "dp")
            frame.owner.Image = Image
            frame.map["layouts"][frame.centre]["static_frame"] = "centre.png"
            frame.map["layouts"][frame.side]["static_frame"] = "side.png"
            frame.map["layouts"][frame.centre]["draws"].append(
                source_draw("qpro_bg", box(12, 360, 232, 340)))
            image = Image.new("RGBA", (1280, 720), (0, 0, 0, 255))
            # The lower opening is larger, and the right Graph area is larger
            # still. Neither may replace the upper-left movie window.
            for bounds in ((30, 40, 230, 330), (20, 370, 240, 690), (1020, 30, 1260, 690)):
                image.paste((0, 0, 0, 0), bounds)
            image.save(root / "centre.png")
            Image.new("RGBA", image.size).save(root / "side.png")
            self.assertEqual(frame.apertures(), [box(30, 40, 200, 290), box(20, 370, 220, 292)])
            evidence = next(row for row in frame.evidence if "dpOriginalMonitorOpenings" in row)
            self.assertEqual(evidence["dpOriginalMonitorOpenings"],
                             [box(30, 40, 200, 290), box(20, 370, 220, 320)])
            self.assertEqual(evidence["publicHudBottomLimit"], 662)

    def test_graph_catalog_selects_one_centre_and_preserves_dp_fallback(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            library, author, research, components = [root / name for name in ("library", "author", "research", "components")]
            for folder in (library, author, research / "gmframe26", components):
                folder.mkdir(parents=True)
            (library / "library.json").write_text("{}", encoding="utf-8")
            mapping = {"stage": {"width": 1280, "height": 720, "fps": 60.0},
                       "layouts": {name: {"static_frame": name + ".png"}
                                   for name in ("center_frame_sp", "1p_frame")}}
            (research / "gmframe26/skin-reference-map.json").write_text(json.dumps(mapping), encoding="utf-8")
            (components / "modules.json").write_text(json.dumps({
                "source": {"artIdentity": "IIDX26-only", "librarySHA256": builder.sha(library / "library.json")},
                "files": [], "modules": [], "options": []}), encoding="utf-8")
            owner = SyntheticOwner((26, 24))
            owner.output, owner.library = author, library
            owner.exclusions, owner.receipts, owner.sources = [], [], {}
            owner.Image = MagicMock()
            # Preview pixels are outside this catalog-selection regression.
            owner.Image.new.return_value.save.side_effect = lambda path, **_: path.write_bytes(b"synthetic-preview")
            selected = []

            def contribution(frame):
                selected.append((frame.layout, frame.wide, frame.centre, frame.dx))
                suffix = frame.layout + ("-wide" if frame.wide else "")
                return {name: f"synthetic/{suffix}/{name}" for name in ("manifest", "scene", "ini")}

            args = SimpleNamespace(library=library, output=author, research=research,
                                   components=components, component_overrides=[], image_tools=root)
            with patch.object(builder, "Package", return_value=owner), \
                    patch.dict(builder.FRAMES, {26: "Synthetic"}, clear=True), \
                    patch.object(Frame, "build", contribution), contextlib.redirect_stdout(io.StringIO()):
                builder.build(args)
            catalog = json.loads((author / "gameplay/content.json").read_text(encoding="utf-8"))
            choice = catalog["modules"][0]["choices"][0]
            self.assertEqual(len(choice["variants"]), 4)
            for layout in ("p1", "p2"):
                for shown in (True, False):
                    values = {"layout": layout, "graph-visible": shown}
                    matches = [v for v in choice["variants"] if all(values.get(k) == value for k, value in v["when"].items())]
                    self.assertEqual(len(matches), 1)
                    self.assertEqual(set(matches[0]["when"]), {"layout", "graph-visible"})
                    self.assertIs(type(matches[0]["when"]["graph-visible"]), bool)
                    self.assertEqual(matches[0]["scene"], f"synthetic/{layout}{'' if shown else '-wide'}/scene")
            for shown in (True, False):
                dp_values = {"graph-visible": shown}  # layout is unavailable for dual-stage keymodes.
                self.assertFalse(any(all(dp_values.get(k) == value for k, value in v["when"].items()) for v in choice["variants"]))
                self.assertEqual(choice["scene"], "synthetic/dp/scene")
            self.assertEqual(set(selected), {("p1", False, "center_frame_sp", 36),
                                            ("p2", False, "center_frame_sp", -36),
                                            ("dp", False, "center_frame_dp_1p", 0),
                                            ("p1", True, "center_frame_wide_1p", 0),
                                            ("p2", True, "center_frame_wide_2p", 0)})

    def test_mods_follow_the_selected_original_effect_hook_once(self):
        for layout, wide in (("p1", False), ("p2", False), ("dp", False), ("p1", True), ("p2", True)):
            with self.subTest(layout=layout, wide=wide), tempfile.TemporaryDirectory() as temporary:
                frame, _, _ = self.frame(Path(temporary), layout, wide=wide)
                source_left = (675 if layout == "p1" else 325) if wide else 500
                expected = [(source_left + frame.dx) / 1280, 680 / 720, 254 / 1280, 40 / 720]
                profiles = frame.profiles()
                self.assertEqual(len(profiles), 3 if layout == "dp" else 6)
                for profile in profiles:
                    for actual, value in zip(profile["modsRect"], expected):
                        self.assertAlmostEqual(actual, value)

    def test_missing_effect_hook_does_not_create_a_floating_mods_fallback(self):
        with tempfile.TemporaryDirectory() as temporary:
            frame, _, _ = self.frame(Path(temporary))
            frame.map["layouts"][frame.centre]["draws"] = [draw for draw in frame.map["layouts"][frame.centre]["draws"]
                                                         if "effect" not in draw["game_bindings"]]
            with self.assertRaisesRegex(ValueError, "required original binding"):
                frame.profiles()

    def test_score_seven_digits_fit_the_same_native_six_digit_field(self):
        for layout in ("p1", "p2", "dp"):
            with self.subTest(layout=layout), tempfile.TemporaryDirectory() as temporary:
                frame, source_left, _ = self.frame(Path(temporary), layout)
                frame.numbers()
                score = next(n for n in frame.info if n["id"] == frame.prefix + ".score")
                props = score["properties"]
                self.assertAlmostEqual(props["x"] * 1280, source_left + frame.dx)
                self.assertAlmostEqual(props["width"] * 1280, 160)
                self.assertAlmostEqual(props["y"] * 720, 80)
                self.assertAlmostEqual(props["height"] * 720, 22)
                self.assertEqual(props["alignment"], "right")
                self.assertAlmostEqual(props["font-size"], 22 * LOGICAL)
                track = next(t for t in frame.scene["tracks"] if t["target"] == score["id"])
                self.assertEqual((track["clock"], track["property"], track["easing"], track["loop"]),
                                 ("score.value", "font-size", "step", False))
                self.assertEqual([k["time"] for k in track["keyframes"]], [0, 1_000_000])
                self.assertAlmostEqual(track["keyframes"][0]["value"], props["font-size"])
                # Seven 30px glyphs at 26px advances span 186px including overhang.
                rendered_width = 186 * track["keyframes"][1]["value"] / (22 * LOGICAL)
                self.assertAlmostEqual(rendered_width, props["width"] * 1280)
                self.assertTrue(all(k["id"] for k in track["keyframes"]))

    def test_percent_keeps_ones_and_tens_at_their_original_hooks(self):
        for gauge_size, advance in (((26, 24), 26), ((28, 26), 30)):
            for layout in ("p1", "p2", "dp"):
                with self.subTest(size=gauge_size, layout=layout), tempfile.TemporaryDirectory() as temporary:
                    frame, _, anchors = self.frame(Path(temporary), layout, gauge_size=gauge_size, advance=advance)
                    frame.gauge()
                    readout = next(n for n in frame.info if n["id"].endswith(".gauge.percent"))
                    props = readout["properties"]
                    font = frame.fonts[props["bitmap-font"]]
                    glyphs = {g["character"]: g for g in font["glyphs"]}
                    self.assertEqual(props["format"], "percent")
                    self.assertEqual(glyphs["%"]["resource"], frame.empty)
                    self.assertEqual(glyphs["%"]["advance"], 1)
                    right = (props["x"] + props["width"]) * 1280
                    for text in ("8%", "37%"):
                        cursor, ink_right, positions = 0, 0, []
                        for character in text:
                            glyph = glyphs[character]
                            positions.append(cursor)
                            ink_right = max(ink_right, cursor + frame.owner.dimensions[glyph["resource"]][0])
                            cursor += glyph["advance"]
                        start = right - max(cursor, ink_right)
                        self.assertAlmostEqual(start + positions[-2], anchors[0]["left"])
                        if len(text) == 3:
                            self.assertAlmostEqual(start, anchors[1]["left"])

    def test_hundred_uses_the_native_compressed_anchor_at_the_rounding_boundary(self):
        with tempfile.TemporaryDirectory() as temporary:
            frame, _, anchors = self.frame(Path(temporary), "p2", gauge_size=(28, 26), advance=30)
            frame.gauge()
            hundred = next(n for n in frame.info if n["id"].endswith(".gauge.hundred"))
            for i, sprite in enumerate(hundred["children"]):
                props = sprite["properties"]
                self.assertAlmostEqual(props["x"] * 1280, anchors[i]["left"])
                self.assertAlmostEqual(props["y"] * 720, anchors[i]["top"])
                self.assertAlmostEqual(props["width"] * 1280, 28)
                self.assertAlmostEqual(props["height"] * 720, 26)
                self.assertTrue(sprite["resource"].endswith("playm_gg_01" if i == 2 else "playm_gg_00"))
            for suffix, values in ((".gauge.percent", [1, 0]), (".gauge.hundred", [0, 1])):
                track = next(t for t in frame.scene["tracks"] if t["target"].endswith(suffix))
                self.assertEqual((track["clock"], track["property"], track["easing"], track["loop"]),
                                 ("gauge.value", "opacity", "step", False))
                self.assertEqual([k["time"] for k in track["keyframes"]], [0, .995])
                self.assertEqual([k["value"] for k in track["keyframes"]], values)


if __name__ == "__main__":
    unittest.main()
