import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import AuthorPackage, Contribution, material_bindings, source_path
from beatoraja_native import Lua, lua, native_rect, node_rect


class AuthorTests(unittest.TestCase):
    def fixture(self, root):
        (root / "skin.ini").write_text("[General]\nName: Standard Model Skin Omni Until 26\n", encoding="utf-8")
        (root / "gameplay-skin.json").write_text(json.dumps({
            "contract": "oms-gameplay-skin-manifest.v1", "contentCatalog": "content.json", "scene": "base.json",
            "options": [{"id": "layout", "default": "p1", "keymodes": ["7k"]}],
        }), encoding="utf-8")
        (root / "content.json").write_text(json.dumps({
            "contract": "oms-gameplay-skin-content.v1", "modules": [{"id": "frame", "default": "only", "choices": [{
                "value": "only", "orderedVariants": True, "manifest": "base.json", "variants": [
                    {"when": {"layout": "p1"}, "manifest": "first.json"},
                    {"when": {"layout": "p1"}, "manifest": "second.json"},
                    {"when": {}, "manifest": "dp.json"},
                ]}]}]}), encoding="utf-8")
        for name in ("base", "first", "second", "dp"):
            (root / (name + ".json")).write_text(json.dumps({"name": name}), encoding="utf-8")
        return AuthorPackage(root, "standard")

    def test_first_matching_variant_and_dp_omission(self):
        with tempfile.TemporaryDirectory() as directory:
            author = self.fixture(Path(directory))
            sp = author.options("7k", "p1")
            dp = author.options("14k", "dp")
            self.assertNotIn("layout", dp)
            self.assertEqual(author.contribution("frame", sp).manifest["name"], "first")
            self.assertEqual(author.contribution("frame", dp).manifest["name"], "dp")
            self.assertIsNone(author.contribution("frame", sp).scene_path)

    def test_family_identity_and_author_containment(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.fixture(root)
            with self.assertRaisesRegex(ValueError, "identity"):
                AuthorPackage(root, "lightning")
            for name in ("../skin.ini", "C:/skin.ini", "a\\skin.ini", "/skin.ini"):
                with self.assertRaises(ValueError):
                    source_path(root, name)

    def test_material_scope_does_not_leak_between_modes(self):
        with tempfile.TemporaryDirectory() as directory:
            author = self.fixture(Path(directory))
            contribution = author.contribution("frame", author.options())
            from dataclasses import replace
            contribution = replace(contribution, ini='''[GameplaySkin.Common:1]
Target: Lane ruleset=bms keymode=7k presentation=p1 group-logical=0 lane=bms.lane.key-1
object.note: resource Provide "one"
Target: Lane ruleset=bms keymode=7k presentation=p2 group-logical=0 lane=bms.lane.key-1
object.note: resource Provide "two"
Target: Lane ruleset=mania keymode=7k presentation=p1 group-logical=0 lane=mania.lane.column-1
object.note: resource Provide "mania"
''')
            self.assertEqual([b["resource"] for b in material_bindings(contribution, "7k", "p2")], ["two"])

    def test_relative_anchor_and_native_y_keep_pixel_edges(self):
        rect = node_rect({"x": .5, "y": 1, "width": .25, "height": .5,
                          "origin": "bottom-centre"}, (100, 20, 400, 200))
        self.assertEqual(rect, (250, 120, 100, 100))
        self.assertEqual(native_rect(rect, 720), {"x": 250, "y": 500, "w": 100, "h": 100})

    def test_serialized_data_cannot_become_lua_code(self):
        self.assertEqual(lua('"; os.exit(); --'), '"\\\"; os.exit(); --"')
        self.assertEqual(lua({"value": Lua("function() return 2 end")}), '{["value"]=function() return 2 end}')
        with self.assertRaises(ValueError):
            lua(float("nan"))

    def test_sp2_maps_logical_keys_and_right_scratch(self):
        from types import SimpleNamespace
        from beatoraja_export import Composer
        author = SimpleNamespace(family="standard", name="Standard Model Skin Omni Until 26")
        composer = Composer(author, None, {}, "p2", {})
        logical = [[.8, .2], *[[i * .1, .1] for i in range(7)]]
        layout = {"id": "right", "ruleset": "bms", "keymode": "7k", "presentation": "p2",
                  "stages": [{"rect": [.5, 0, .5, .7], "lanes": logical, "keys": logical}], "keyAreaHeight": .1}
        ini = '\n'.join('[GameplaySkin.Common:1]\nTarget: Lane ruleset=bms keymode=7k presentation=p2 group-logical=0 lane=bms.lane.'
                        + ("scratch-1" if i == 0 else "key-" + str(i))
                        + f' group-local-logical={i} group-local-visual={7 if i == 0 else i - 1}\nplayfield.key: resource Provide "key"'
                        for i in range(8))
        frame = Contribution("frame", "only", None, None, None, {"layouts": [layout]}, {}, ini)
        _, lanes = composer.lanes(frame)
        self.assertEqual(lanes[0]["x"], 640)
        self.assertEqual(lanes[-1]["x"], 1152)
        materials = list(composer.lane_materials(frame, frame, "playfield.key"))
        self.assertEqual(materials[0][2], 1)
        self.assertEqual(materials[0][3][0], 640)
        self.assertEqual(materials[-1][2], 0)
        self.assertEqual(materials[-1][3][0], 1152)

    def test_event_rejects_partial_bake_without_showing_a_still(self):
        from types import SimpleNamespace
        from beatoraja_export import Composer
        author = SimpleNamespace(family="standard", name="Standard Model Skin Omni Until 26")
        composer = Composer(author, None, {}, "p1", {("frame.json", "fc"): (None, {"selectionComplete": False})})
        composer.draw_baked("frame.json", "fc", (0, 0, 100, 100), timer=48)
        self.assertEqual(composer.skin["destination"], [])
        self.assertEqual(composer.gaps[0]["feature"], "event animation needs complete bake")

    def test_changed_author_file_rejects_old_bake(self):
        import hashlib
        from beatoraja_export import read_bakes
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            author = self.fixture(root)
            bake = root / "bake"
            bake.mkdir()
            original = (root / "first.json").read_bytes()
            (bake / "index.json").write_text(json.dumps({
                "contract": "oms-iidx-baked-animation.v1", "selectionComplete": True,
                "source": {"authorRoot": str(root), "manifest": "first.json", "animation": "one",
                           "files": {"first.json": hashlib.sha256(original).hexdigest()}},
                "verification": {"passed": True}, "pages": [],
            }), encoding="utf-8")
            self.assertIn(("first.json", "one"), read_bakes([bake], author))
            (root / "first.json").write_text('{"changed":true}', encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Stale bake source"):
                read_bakes([bake], author)


if __name__ == "__main__":
    unittest.main()
