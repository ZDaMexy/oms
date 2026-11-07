"""Original bar-line references and native CPU geometry; no GPU or player IO."""

from copy import deepcopy
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest

from PIL import Image

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from beatoraja_author import Contribution
from beatoraja_barline import export_barline
from beatoraja_native import AssetSink, lua


PROBE = r'''
package bms.player.beatoraja.skin.lua;

import java.nio.file.*;
import java.util.*;
import javax.imageio.ImageIO;
import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.*;
import bms.player.beatoraja.skin.json.*;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import org.luaj.vm2.*;
import org.luaj.vm2.lib.jse.JsePlatform;

public final class BarlineProbe {
    private static final class Destinations extends JsonPlaySkinObjectLoader {
        Destinations() { super(null); }
        void apply(Skin skin, SkinObject object, JsonSkin.Destination destination) {
            setDestination(skin, object, destination);
        }
    }
    static void check(boolean condition, String detail) {
        if (!condition) throw new AssertionError(detail);
    }
    public static void main(String[] args) throws Exception {
        Path file = Path.of(args[0]).toAbsolutePath();
        LuaValue cases = JsePlatform.standardGlobals().load(Files.readString(file)).call();
        LuaSkinLoader decoder = LuaSkinLoader.sandboxed(file);
        Destinations destinations = new Destinations();
        int geometry=0, groups=0, references=0;
        for (int i=1; i<=cases.length(); i++) {
            LuaValue fixture = cases.get(i);
            JsonSkin.Skin document = decoder.fromLuaValue(JsonSkin.Skin.class, fixture.get("skin"));
            Map<String, JsonSkin.Image> images = new HashMap<>();
            Map<String, JsonSkin.Source> resources = new HashMap<>();
            for (JsonSkin.Image image : document.image) images.put(image.id, image);
            for (JsonSkin.Source source : document.source) resources.put(source.id, source);
            check(document.note.group.length==fixture.get("groups").toint(), "Wrong native group count");
            check(document.destination.length==0, "Bar-lines must not also be ordinary destinations");
            Resolution sourceResolution = fixture.get("height").toint()==720 ? Resolution.HD : Resolution.FULLHD;
            for (JsonSkin.Destination group : document.note.group) {
                JsonSkin.Image image = images.get(group.id);
                check(image!=null && resources.containsKey(image.src), "Unresolved native group image");
                var pixels = ImageIO.read(file.getParent().resolve(resources.get(image.src).path).toFile());
                check(pixels.getWidth()==image.w && pixels.getHeight()==image.h && image.x==0 && image.y==0,
                      "Original image dimensions/crop changed");
                references++;
                for (Resolution resolution : new Resolution[]{Resolution.HD,Resolution.FULLHD}) {
                    SkinHeader header = new SkinHeader();
                    header.setSourceResolution(sourceResolution); header.setDestinationResolution(resolution);
                    Skin skin = new Skin(header);
                    // Textureless identity only: SkinImage.prepare is the real official
                    // CPU path. No Texture, MainController, draw or player files exist.
                    TextureRegion identity = new TextureRegion();
                    SkinImage line = new SkinImage(identity);
                    destinations.apply(skin,line,group);
                    var point = group.dst[0];
                    double scale = resolution.height/(double)sourceResolution.height;
                    for (long time : new long[]{0,149,150,100000}) {
                        for (int offset : new int[]{-12,0,37}) {
                            line.prepare(time,null,0,offset);
                            check(line.draw && line.getImage(time,null)==identity, "Actual line image was not selected");
                            check(Math.abs(line.region.x-point.x*scale)<.0001
                                && Math.abs(line.region.y-point.y*scale-offset)<.0001
                                && Math.abs(line.region.width-point.w*scale)<.0001
                                && Math.abs(line.region.height-point.h*scale)<.0001,
                                "Native source resolution/scroll translation mismatch");
                            check(Math.abs(line.getColor().r-fixture.get("rgb").todouble()/255)<.00001
                                && Math.abs(line.getColor().g-line.getColor().r)<.00001
                                && Math.abs(line.getColor().b-line.getColor().r)<.00001
                                && Math.abs(line.getColor().a-1)<.00001,
                                "RGB brightness changed alpha or was lost by the native destination");
                            geometry++;
                        }
                    }
                    // No GL resources were allocated. A dummy region must not be
                    // disposed through SkinSourceImage's real Texture disposer.
                    skin.dispose();
                }
                groups++;
            }
        }
        System.out.println("{\"cases\":"+cases.length()+",\"groups\":"+groups+
                           ",\"references\":"+references+",\"geometryChecks\":"+geometry+"}");
    }
}
'''


def fixture(family="lightning", side="p1"):
    keymode = "14k" if side == "dp" else "7k"
    count = 2 if side == "dp" else 1
    targets = []
    for ruleset in ("bms", "mania"):
        for index in range(2):
            name = "deck" if ruleset == "bms" else "stage"
            targets.extend([f'Target: Group ruleset={ruleset} keymode=any stage-mode={"any" if index==0 else "dual"} '
                            f'group={ruleset}.group.{name}-{index+1} group-logical={index} group-visual={index}',
                            'playfield.bar-line: resource Provide "bar-material"'])
    node = {"id": "source-bar", "type": "sprite", "slot": "playfield.bar-line", "target": {"kind": "global"},
            "resource": "bar", "blend": "alpha", "properties": {}, "children": [], "effects": []}
    base = Contribution("base", "base", "gameplay-skin.json", "gameplay-skin.scene.json", "skin.ini",
        {"resources": [{"id": "bar", "type": "texture", "path": "original-bar.png"}]},
        {"root": {"id": "empty", "type": "container", "children": []},
         "templates": [{"id": "bar-template", "root": node}],
         "instances": [{"template": "bar-template", "material": {"slot": "playfield.bar-line", "resource": "bar-material"}}],
         "bindings": [{"target": "source-bar", "property": "brightness", "source": "option.lane-objects-brightness"}],
         "tracks": [], "variants": [], "stateMachines": []},
        "[GameplaySkin.Common:1]\n"+"\n".join(targets) if family == "lightning" else "[General]\n")
    stages = [{"rect": [(.65 if side == "p2" else .05)+index*.5, .01, .3, .67]} for index in range(count)]
    frame = Contribution("frame", "selected-frame", "frame.json", "frame.scene.json", "frame.ini",
        {"layouts": [{"ruleset": "bms", "keymode": keymode, "presentation": side, "stages": stages}]}, {},
        "[Bms]\nKeymode: "+keymode+"\nBarLineHeight: 1\n")
    author = SimpleNamespace(family=family, base=lambda: base)
    return author, base, frame, keymode


class BarlineTests(unittest.TestCase):
    def setUp(self):
        parent = Path(__file__).resolve().parents[3] / ".dev-cache/temp/beatoraja-barline-tests"
        parent.mkdir(parents=True, exist_ok=True)
        temporary = tempfile.TemporaryDirectory(dir=parent)
        self.addCleanup(temporary.cleanup)
        self.folder = Path(temporary.name)
        self.source, self.output = self.folder / "source", self.folder / "output"
        self.source.mkdir(); self.output.mkdir()
        image = Image.new("RGBA", (256, 32))
        image.putdata([(x % 168, y % 185, (x+y) % 206, (x*3+y) % 131)
                       for y in range(32) for x in range(256)])
        image.save(self.source / "original-bar.png")
        self.sink = AssetSink(self.source, self.output)

    def export(self, family="lightning", side="p1", brightness=.9):
        author, base, frame, keymode = fixture(family, side)
        canvas = (0, 0, 1280, 720) if family == "standard" else (0, 0, 1920, 1080)
        result = export_barline(author, {"lane-objects-brightness": brightness}, keymode, side, frame, self.sink, canvas)
        return result, base, frame

    def test_lightning_uses_original_bytes_dimensions_and_all_three_actual_groups(self):
        original = (self.source / "original-bar.png").read_bytes()
        for side in ("p1", "p2", "dp"):
            with self.subTest(side=side):
                result, base, frame = self.export(side=side)
                image, = result["image"]
                self.assertEqual((256, 32, 0, 0), (image["w"], image["h"], image["x"], image["y"]))
                self.assertEqual(original, (self.output / self.sink.source[image["src"]]["path"]).read_bytes())
                self.assertEqual(2 if side == "dp" else 1, len(result["group"]))
                self.assertTrue(all(group["id"] == image["id"] for group in result["group"]))
                self.assertTrue(all(group["dst"][0]["r"] == round(.9*255) and group["dst"][0]["a"] == 255
                                    for group in result["group"]))
                self.assertNotIn("destination", result)
                self.assertEqual(1.40625, result["gaps"][0]["sourceHeightPx"])
                self.assertEqual(.40625, result["gaps"][0]["heightErrorPx"])

    def test_standard_retains_its_declared_host_addition_without_borrowing_lightning(self):
        result, _, _ = self.export("standard", "dp", .1)
        image, = result["image"]
        self.assertEqual((1, 1), (image["w"], image["h"]))
        with Image.open(self.output / self.sink.source[image["src"]]["path"]) as pixels:
            self.assertEqual((255, 255, 255, 80), pixels.getpixel((0, 0)))
        self.assertEqual(2, len(result["group"]))
        self.assertTrue(all(g["dst"][0]["h"] == 1 and g["dst"][0]["r"] == 255 for g in result["group"]))
        self.assertEqual(["native BMS barline addition"], [g["code"] for g in result["gaps"]])
        self.assertNotIn("original-bar.png", self.sink._loaded_sources)

    def test_missing_deck_source_and_unmapped_sprite_program_fail_instead_of_synthetic_success(self):
        author, base, frame, keymode = fixture(side="dp")
        base.scene["bindings"][0]["source"] = "object.unavailable"
        with self.assertRaisesRegex(ValueError, "qualified static"):
            export_barline(author, {}, keymode, "dp", frame, self.sink, (0, 0, 1920, 1080))
        author, base, frame, keymode = fixture(side="dp")
        object.__setattr__(base, "ini", base.ini.replace("group-logical=1", "group-logical=2"))
        with self.assertRaisesRegex(ValueError, "every active group"):
            export_barline(author, {"lane-objects-brightness": 1}, keymode, "dp", frame, self.sink, (0, 0, 1920, 1080))
        author, base, frame, keymode = fixture()
        base.scene["tracks"].append({"target": "source-bar", "property": "opacity"})
        with self.assertRaisesRegex(ValueError, "qualified static"):
            export_barline(author, {"lane-objects-brightness": 1}, keymode, "p1", frame, self.sink, (0, 0, 1920, 1080))

    def test_source_scene_and_frame_are_not_mutated(self):
        author, base, frame, keymode = fixture()
        before = deepcopy((base.scene, base.manifest, frame.manifest))
        export_barline(author, {"lane-objects-brightness": 1}, keymode, "p1", frame, self.sink, (0, 0, 1920, 1080))
        self.assertEqual(before, (base.scene, base.manifest, frame.manifest))

    def test_official_jar_parses_group_references_and_keeps_rgb_alpha_and_scroll_geometry(self):
        jar = Path(os.environ.get("BEATORAJA_JAR", "D:/beatoraja/beatoraja.jar"))
        javac, java = shutil.which("javac"), shutil.which("java")
        if not jar.is_file() or not javac or not java:
            self.skipTest("Official beatoraja 0.8.9 JAR and JDK are required")
        cases = []
        for family, levels in (("lightning", (.9, .92, .94, .96, .98, 1)), ("standard", (1,))):
            for side in ("p1", "p2", "dp"):
                for brightness in levels:
                    result, _, _ = self.export(family, side, brightness)
                    image, = result["image"]
                    cases.append({"height": 720 if family == "standard" else 1080,
                        "groups": 2 if side == "dp" else 1, "rgb": round(brightness*255),
                        "skin": {"image": result["image"], "note": {"id": "notes", "group": result["group"]},
                                 "source": [self.sink.source[image["src"]]], "destination": []}})
        recipe = self.output / "barline-cases.lua"
        recipe.write_text("return "+lua(cases), encoding="utf-8")
        source = self.folder / "BarlineProbe.java"
        source.write_text(PROBE, encoding="utf-8")
        compile_result = subprocess.run([javac, "-encoding", "UTF-8", "-cp", str(jar), "-d", str(self.folder), str(source)],
                                        capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, compile_result.returncode, compile_result.stdout+compile_result.stderr)
        run = subprocess.run([java, "-cp", str(self.folder)+os.pathsep+str(jar),
                              "bms.player.beatoraja.skin.lua.BarlineProbe", str(recipe)],
                             capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=60)
        self.assertEqual(0, run.returncode, run.stdout+run.stderr)
        receipt = json.loads(run.stdout)
        self.assertEqual({"cases": 21, "groups": 28, "references": 28, "geometryChecks": 672}, receipt)
        print("Native bar-line probe: "+json.dumps(receipt, sort_keys=True))


if __name__ == "__main__":
    unittest.main()
