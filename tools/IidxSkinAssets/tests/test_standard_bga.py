"""Cabinet/BGA viewport selection and unchanged source-art regression checks.

The local fixtures are synthetic. They do not claim to replace the private
full-timeline prepare pass against an actual source library.
"""
import copy
import json
from pathlib import Path
import re
import struct
import sys
import tempfile
import unittest
import zlib

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from standard_bga import (CANVAS, CENTRES, CONTRACT, COVERS, FRAME_IDS, clear_hole,
                          cover_frame_state, cover_sizes, digest, export_bga_module, project_hole, validated_geometry)


def bounds(left, top, right, bottom):
    return dict(zip(("left", "top", "right", "bottom"), (left, top, right, bottom)))


def png(size, hole, colour):
    """Encode synthetic RGBA source bytes with an exact transparent rectangle."""
    width, height = size
    left, top, right, bottom = hole
    opaque = bytes((*colour, 255))
    rows = b"".join(b"\0" + (opaque * left + bytes(4 * (right - left)) + opaque * (width - right)
                             if top <= y < bottom else opaque * width) for y in range(height))

    def chunk(kind, data):
        return struct.pack(">I", len(data)) + kind + data + struct.pack(">I", zlib.crc32(kind + data))

    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")


def scene_nodes(root):
    yield root
    for child in root["children"]:
        yield from scene_nodes(child)


def select(choice, options):
    return next((variant for variant in choice["variants"]
                 if all(key in options and options[key] == value for key, value in variant["when"].items())), choice)


class StandardBgaTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.library = Path(self.temporary.name) / "library"
        self.output = Path(self.temporary.name) / "author"
        self.library.mkdir()
        self.source_files = {}
        packages, textures, texture_evidence, rows = [], [], [], []
        required = [f"contents/data/graphic/gmframe{style:02}.ifs" for style in sorted(FRAME_IDS)]
        required += [f"contents/data/graphic/{family}/gameparts.ifs" for family in ("0", "1")]
        for source in required:
            path = self.put("source-copies/" + source, ("Synthetic archive: " + source).encode())
            packages.append({"source": source, "sha256": digest(path)})
            self.put(f"packages/{source.removesuffix('.ifs')}/raw/tex/texturelist.xml", b"<texturelist />")
        for family in ("0", "1"):
            for name, size in COVERS.items():
                hole = [171, 141, 629, 621] if name == "anime_cover" else [190, 47, 740, 623]
                path = self.put(f"sprites/f{family}/{name}.png", png(size, hole, (40 + int(family) * 80, 80, 100)))
                row = {"package": f"contents/data/graphic/{family}/gameparts.ifs", "name": name + ".png",
                       "sprite": path.relative_to(self.library).as_posix(), "sha256": digest(path),
                       "width": size[0], "height": size[1], "imageRect": [0, size[0], 0, size[1]],
                       "uvRect": [0, size[0], 0, size[1]]}
                textures.append(row)
                texture_evidence.append({"family": family, "name": name, "path": row["sprite"], "sha256": row["sha256"],
                    "size": list(size), "imageRect": row["imageRect"], "uvRect": row["uvRect"], "filter": "nearest", "clearHole": hole})
        for style in sorted(FRAME_IDS):
            raw = f"packages/contents/data/graphic/gmframe{style:02}/raw"
            for movie in CENTRES:
                self.put(raw + "/afp/" + movie, ("Synthetic AFP: " + movie).encode())
                self.put(raw + "/afp/bsi/" + movie, b"Synthetic BSI")
                row = {"style": style, "movie": movie, "frames": 420, "fps": 60, "covers": []}
                if "_dp_" not in movie:
                    name = "anime_cover2" if "_wide_" in movie else "anime_cover"
                    width, height = COVERS[name]
                    if movie == "center_frame_wide_2p" and style == 25:
                        quad = [[width, 0], [0, 0], [0, height], [width, height]]
                    else:
                        x = 348 if movie == "center_frame_wide_1p" else -2 if movie == "center_frame_wide_2p" else 240
                        quad = [[x, 0], [x + width, 0], [x + width, height], [x, height]]
                    geometry = movie + "_shape"
                    self.put(raw + "/geo/" + geometry, ("Synthetic GEO: " + name).encode())
                    row.update(covers=[name], texture=name, geometry=geometry, quad=quad, constantSourceFrames=420)
                rows.append(row)
        self.metadata = {"status": "complete", "sourceMetadataUnchanged": True, "packages": packages, "textures": textures}
        self.put("library.json", json.dumps(self.metadata).encode())
        self.evidence = {"contract": CONTRACT, "librarySHA256": digest(self.library / "library.json"), "canvas": list(CANVAS),
                         "sourceFiles": [{"path": path, "sha256": value} for path, value in self.source_files.items()],
                         "rows": rows, "textures": texture_evidence}
        apertures = {("p1", False): [bounds(356, 141, 996, 621)], ("p2", False): [bounds(284, 141, 924, 621)],
                     ("p1", True): [bounds(431, 47, 1199, 623)], ("p2", True): [bounds(81, 47, 849, 623)],
                     ("dp", False): [bounds(13, 50, 241, 350), bounds(13, 370, 241, 662)]}
        self.contexts = [{"style": style, "layout": layout, "wide": wide, "apertures": copy.deepcopy(boxes)}
                         for style in sorted(FRAME_IDS) for (layout, wide), boxes in apertures.items()]

    def put(self, relative, data):
        path = self.library / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_bytes(data)
        self.source_files[relative] = digest(path)
        return path

    def export(self, evidence=None, contexts=None):
        return export_bga_module(self.library, self.output, self.contexts if contexts is None else contexts,
                                 source_geometry=self.evidence if evidence is None else evidence)

    def read(self, relative):
        return json.loads((self.output / relative).read_text(encoding="utf-8"))

    def test_off_is_default_and_keeps_all_actual_viewports(self):
        result = self.export()
        module = result["module"]
        self.assertEqual("bga-frame", module["id"])
        self.assertEqual(["bms"], module["rulesets"])
        self.assertEqual("off", module["default"])
        self.assertEqual(["off", "on"], [choice["value"] for choice in module["choices"]])
        off = module["choices"][0]
        self.assertTrue(off["orderedVariants"])
        self.assertEqual(50, len(off["variants"]))
        for context in self.contexts:
            options = {"frame": f"iidx{context['style']:02}", "graph-visible": not context["wide"]}
            if context["layout"] != "dp":
                options["layout"] = context["layout"]
            contribution = select(off, options)
            self.assertIn(f"/f{context['style']:02}/{context['layout']}/", contribution["manifest"])
            self.assertEqual([], self.read(contribution["manifest"])["resources"])
            self.assertEqual([], self.read(contribution["scene"])["templates"])
            receipt = next(row for row in result["receipts"] if row["choice"] == "off"
                           and row["frame"] == options["frame"] and row["layout"] == context["layout"] and row["wide"] == context["wide"])
            self.assertEqual(context["apertures"], receipt["viewports"])

    def test_on_selects_source_family_and_intersects_real_opening(self):
        result = self.export()
        on = result["module"]["choices"][1]
        expected = {("p1", False): bounds(447, 141, 905, 621), ("p2", False): bounds(375, 141, 833, 621),
                    ("p1", True): bounds(538, 47, 1088, 623), ("p2", True): bounds(188, 47, 738, 623)}
        for context in self.contexts:
            if context["layout"] == "dp":
                continue
            frame, layout, wide = f"iidx{context['style']:02}", context["layout"], context["wide"]
            contribution = select(on, {"frame": frame, "layout": layout, "graph-visible": not wide})
            resource, = self.read(contribution["manifest"])["resources"]
            family = "1" if context["style"] == 25 else "0"
            self.assertIn("/f" + family + "/", resource["path"])
            receipt = next(row for row in result["receipts"] if row["choice"] == "on" and row["frame"] == frame
                           and row["layout"] == layout and row["wide"] == wide)
            aperture = bounds(192, 47, 742, 623) if (context["style"], layout, wide) == (25, "p2", True) else expected[layout, wide]
            self.assertEqual([aperture], receipt["viewports"])
            self.assertEqual(420, receipt["sourceConstantFrames"])
            self.assertEqual((self.library / receipt["sourceTexture"]["path"]).read_bytes(), (self.output / resource["path"]).read_bytes())
            self.assertNotIn("camera", contribution["when"])

    def test_dp_has_no_fake_cover_and_both_choices_keep_two_viewports(self):
        result = self.export()
        off, on = result["module"]["choices"]
        for style in FRAME_IDS:
            for graph_visible in (False, True):
                options = {"frame": f"iidx{style:02}", "graph-visible": graph_visible}
                left, right = select(off, options), select(on, options)
                self.assertEqual((self.output / left["ini"]).read_bytes(), (self.output / right["ini"]).read_bytes())
                ini = (self.output / right["ini"]).read_text()
                self.assertIn("Keymode: 14K", ini)
                self.assertEqual(2, ini.count(",fit"))
                self.assertEqual([], self.read(right["manifest"])["resources"])
                self.assertEqual([], self.read(right["scene"])["instances"])
                self.assertNotIn("layout", right["when"])

    def test_mirrored_full_texture_quad_stays_inside_the_original_aperture_clip(self):
        result = self.export()
        contribution = select(result["module"]["choices"][1], {"frame": "iidx25", "layout": "p2", "graph-visible": False})
        scene, manifest = self.read(contribution["scene"]), self.read(contribution["manifest"])
        root = scene["templates"][0]["root"]
        clip, = root["children"]
        art, = clip["children"]
        self.assertEqual(10, root["properties"]["z"])
        self.assertEqual("bounds", clip["properties"]["clip-mode"])
        self.assertEqual("projective-sprite", art["type"])
        self.assertEqual({"slot": "decoration", "resource": "markers/frame-p2-information"}, scene["instances"][0]["material"])
        self.assertEqual([], manifest["animations"])
        track, = scene["tracks"]
        self.assertEqual(art["id"], track["target"])
        self.assertFalse(track["loop"])
        self.assertNotIn("clock", track)
        keyframe, = track["keyframes"]
        self.assertEqual(0, keyframe["time"])
        self.assertEqual([[1, 0, 1], [0, 0, 1], [0, 1, 1], [1, 1, 1]], keyframe["value"]["vertices"])
        cp, ap = clip["properties"], art["properties"]
        cw, ch = cp["width"] * 1280, cp["height"] * 720
        ax, ay = cp["x"] * 1280 + ap["x"] * cw, cp["y"] * 720 + ap["y"] * ch
        world = [[ax + u * ap["width"] * cw, ay + v * ap["height"] * ch] for u, v, _ in keyframe["value"]["vertices"]]
        for actual, original in zip(world, [[932, 0], [0, 0], [0, 720], [932, 720]]):
            self.assertAlmostEqual(original[0], actual[0])
            self.assertAlmostEqual(original[1], actual[1])
        for expected, actual in zip([81, 47, 768, 576], [cp["x"] * 1280, cp["y"] * 720, cw, ch]):
            self.assertAlmostEqual(expected, actual)
        self.assertNotIn("region", manifest["resources"][0])

    def test_all_exported_references_and_public_identifiers_close(self):
        result = self.export()
        paths = {row["path"] for row in result["files"]}
        self.assertEqual(4, sum(path.endswith(".png") for path in paths))
        for choice in result["module"]["choices"]:
            self.assertLessEqual(len(choice["variants"]), 64)
            for contribution in choice["variants"]:
                self.assertTrue({contribution[field] for field in ("manifest", "scene", "ini")} <= paths)
                manifest, scene = self.read(contribution["manifest"]), self.read(contribution["scene"])
                self.assertEqual("gameplay-skin.scene.json", manifest["scene"])
                self.assertTrue({row["path"] for row in manifest["resources"]} <= paths)
                resources = {row["id"] for row in manifest["resources"]}
                nodes = [scene["root"], *[node for template in scene["templates"] for node in scene_nodes(template["root"])]]
                identities = [row["id"] for row in [*manifest["resources"], *nodes, *scene["templates"], *scene["instances"], *scene["tracks"],
                              *[keyframe for track in scene["tracks"] for keyframe in track["keyframes"]]]]
                self.assertEqual(len(identities), len(set(identities)))
                self.assertTrue(all(re.fullmatch(r"[a-z][a-z0-9.-]{0,127}", identity) for identity in identities))
                self.assertTrue({row["resource"] for row in nodes if "resource" in row} <= resources)
                self.assertTrue({row["target"] for row in scene["tracks"]} <= {row["id"] for row in nodes})
                self.assertTrue({row["template"] for row in scene["instances"]} <= {row["id"] for row in scene["templates"]})
                for node in nodes:
                    for field in ("x", "y", "width", "height"):
                        if field in node["properties"]:
                            self.assertLessEqual(abs(node["properties"][field]), 4)

    def test_source_pixels_and_raw_movie_must_still_match_prepared_evidence(self):
        for relative in ("sprites/f1/anime_cover.png", "packages/contents/data/graphic/gmframe25/raw/afp/center_frame_sp",
                         "source-copies/contents/data/graphic/gmframe25.ifs"):
            with self.subTest(relative=relative):
                path = self.library / relative
                original = path.read_bytes()
                path.write_bytes(original + b"changed")
                with self.assertRaises(ValueError):
                    validated_geometry(self.library, self.evidence)
                path.write_bytes(original)

    def test_missing_source_closure_nonconstant_geometry_and_wrong_family_are_rejected(self):
        evidence = copy.deepcopy(self.evidence)
        evidence["sourceFiles"] = [row for row in evidence["sourceFiles"] if row["path"] != "packages/contents/data/graphic/gmframe25/raw/afp/center_frame_sp"]
        with self.assertRaisesRegex(ValueError, "movie.*closure"):
            validated_geometry(self.library, evidence)
        evidence = copy.deepcopy(self.evidence)
        evidence["rows"][0]["constantSourceFrames"] = 419
        with self.assertRaisesRegex(ValueError, "constant"):
            validated_geometry(self.library, evidence)
        evidence = copy.deepcopy(self.evidence)
        evidence["textures"][2]["path"] = evidence["textures"][0]["path"]
        evidence["textures"][2]["sha256"] = evidence["textures"][0]["sha256"]
        with self.assertRaisesRegex(ValueError, "texture.*closure"):
            validated_geometry(self.library, evidence)
        with self.assertRaisesRegex(ValueError, "all ten.*contexts"):
            self.export(contexts=self.contexts[:-1])

    def test_clear_aperture_intersection_is_used_instead_of_replacing_it(self):
        contexts = copy.deepcopy(self.contexts)
        context = next(row for row in contexts if (row["style"], row["layout"], row["wide"]) == (25, "p1", True))
        context["apertures"] = [bounds(431, 56, 1000, 623)]
        result = self.export(contexts=contexts)
        receipt = next(row for row in result["receipts"] if (row["frame"], row["layout"], row["wide"], row["choice"]) == ("iidx25", "p1", True, "on"))
        self.assertEqual([bounds(538, 56, 1000, 623)], receipt["viewports"])
        with self.assertRaisesRegex(ValueError, "scale changed"):
            project_hole([[0, 0], [466, 0], [466, 720], [0, 720]], [932, 720], [190, 47, 740, 623])

    def test_other_frame_generation_cannot_qualify_as_this_library(self):
        self.metadata["packages"].append({"source": "contents/data/graphic/gmframe33.ifs"})
        (self.library / "library.json").write_text(json.dumps(self.metadata), encoding="utf-8")
        with self.assertRaisesRegex(ValueError, "independent ten-frame"):
            validated_geometry(self.library, self.evidence)

    def test_preparation_uses_exact_alpha_hole(self):
        from PIL import Image
        with Image.open(self.library / "sprites/f1/anime_cover2.png") as image:
            image = image.convert("RGBA")
        self.assertEqual([190, 47, 740, 623], clear_hole(image))
        image.putpixel((0, 719), (0, 0, 0, 0))
        image.putpixel((931, 719), (0, 0, 0, 0))
        self.assertEqual([190, 47, 740, 623], clear_hole(image), "External decoration holes are not part of the BGA opening")
        image.putpixel((200, 100), (1, 2, 3, 1))
        hole = clear_hole(image)
        self.assertEqual([201, 47, 740, 623], hole)
        self.assertEqual((0, 0), image.getchannel("A").crop(hole).getextrema(), "Even alpha1 pixels must stay out of the viewport")

    def test_rounded_opening_uses_maximum_containing_rectangle(self):
        from PIL import Image
        image = Image.new("RGBA", (10, 10), (20, 30, 40, 255))
        image.paste((0, 0, 0, 0), (1, 1, 9, 9))
        for point in ((1, 1), (8, 1), (1, 8), (8, 8)):
            image.putpixel(point, (1, 2, 3, 1))
        self.assertEqual([1, 2, 9, 8], clear_hole(image))
        image.putpixel((5, 5), (1, 2, 3, 1))
        with self.assertRaisesRegex(ValueError, "centre opening"):
            clear_hole(image)


class CoverTimelineTests(unittest.TestCase):
    def test_copula_cover_retains_its_actual_shorter_source_quad(self):
        root, source = self.source()
        for vertex in source['geometry'][0]['vertices']:
            if vertex[1] == 720:
                vertex[1] = 586
        result = cover_frame_state(root, source, sizes=cover_sizes('s23'))
        self.assertEqual([[240, 0], [1040, 0], [1040, 586], [240, 586]], result['quad'])
        with self.assertRaises(ValueError):
            cover_frame_state(root, source, sizes=cover_sizes('s24'))

    def source(self):
        state = {"matrix": [1, 0, 0, 0, 1, 0, 0, 0, 1, 240, 0, 0], "origin": [0, 0, 0], "projection": 1,
                 "mult": [1, 1, 1, 1], "add": [0, 0, 0, 0], "hsl": [0, 0, 0], "blend": 0, "mask": None, "filters": [], "visible": True}
        child = copy.deepcopy(state)
        child["matrix"][-3] = 0
        source = {"geometry": [{"source": "synthetic-quad", "vertices": [[0, 0, 0], [800, 0, 0], [0, 720, 0], [800, 720, 0]],
                   "atlasUV": [[0, 0, 0], [1, 0, 0], [0, 1, 0], [1, 1, 0]], "colours": [],
                   "draws": [{"unresolvedTexture": "anime_cover", "indices": [0, 1, 2, 2, 1, 3], "flags": 67, "sourceColour": None}]}]}
        root = {"nodes": [{"parent": None, "geometry": None}, {"parent": 0, "geometry": 0}], "states": [state, child],
                "frames": [{"nodes": [[0, 0], [1, 1]]}, {"nodes": [[0, 0], [1, 1]]}, {"nodes": []}]}
        return root, source

    def test_actual_ancestor_geometry_is_retained(self):
        root, source = self.source()
        value = cover_frame_state(root, source)
        self.assertEqual([[240, 0], [1040, 0], [1040, 720], [240, 720]], value["quad"])
        self.assertEqual(root["states"], value["stateChain"])

    def test_change_after_first_frame_rejects_static_export(self):
        root, source = self.source()
        changed = copy.deepcopy(root["states"][0])
        changed["matrix"][-3] += 1
        root["states"].append(changed)
        root["frames"][1]["nodes"][0][1] = 2
        with self.assertRaisesRegex(ValueError, "changes.*entry/loop"):
            cover_frame_state(root, source)

    def test_uv_colour_or_mask_is_not_silently_discarded(self):
        root, source = self.source()
        source["geometry"][0]["atlasUV"][1][0] = .5
        with self.assertRaisesRegex(ValueError, "geometry/UV"):
            cover_frame_state(root, source)
        root, source = self.source()
        root["states"][0]["mult"][3] = .5
        with self.assertRaisesRegex(ValueError, "colour/mask"):
            cover_frame_state(root, source)


if __name__ == "__main__":
    unittest.main()
