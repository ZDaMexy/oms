package bms.player.beatoraja.skin.lua;

import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;

import bms.player.beatoraja.MainState;
import bms.player.beatoraja.play.SkinHidden;
import bms.player.beatoraja.skin.SkinImage;
import bms.player.beatoraja.skin.SkinSlider;
import bms.player.beatoraja.skin.SkinSource;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.property.TimerProperty;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.ZeroArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Official 0.8.9 Lua loader and native image-index paths, CPU only.
 * TextureRegions are distinct inert markers, without textures or GL resources.
 * This checks cell selection and draw membership, not rendering or VRAM usage.
 */
public final class CoverGridTimerProbe {
    private static final class Fixture {
        long now;
        LuaValue state() {
            LuaTable reads = new LuaTable();
            reads.set("timer_off_value", LuaValue.valueOf((double) Long.MIN_VALUE));
            reads.set("time", new ZeroArgFunction() {
                public LuaValue call() { return LuaValue.valueOf((double) now); }
            });
            reads.set("timer", new OneArgFunction() {
                public LuaValue call(LuaValue id) { throw new AssertionError("Cover read an unrelated host timer"); }
            });
            LuaTable proxy = new LuaTable(), meta = new LuaTable();
            meta.set("__index", reads);
            meta.set("__newindex", new TwoArgFunction() {
                public LuaValue call(LuaValue key, LuaValue value) {
                    throw new AssertionError("Cover attempted to write host state");
                }
            });
            proxy.setmetatable(meta);
            return proxy;
        }
    }

    private static final class NativeSelection {
        final TextureRegion[] markers;
        final TimerProperty timer;
        final SkinHidden hidden;
        final SkinSlider slider;
        final SkinImage image;
        final Method hiddenIndex;
        final Field sliderSource;

        NativeSelection(int length, TimerProperty timer, int cycle, String kind) throws Exception {
            markers = new TextureRegion[length];
            for (int i = 0; i < length; i++) markers[i] = new TextureRegion();
            this.timer = timer;
            hidden = kind.equals("hiddenCover") || kind.equals("liftCover") ? new SkinHidden(markers, timer, cycle) : null;
            slider = kind.equals("slider") ? new SkinSlider(markers, timer, cycle, 2, 471, 4, false) : null;
            image = kind.equals("image") ? new SkinImage(markers, timer, cycle) : null;
            hiddenIndex = SkinHidden.class.getDeclaredMethod("getImageIndex", int.class, long.class, MainState.class);
            hiddenIndex.setAccessible(true);
            sliderSource = SkinSlider.class.getDeclaredField("source");
            sliderSource.setAccessible(true);
        }

        int select(long milliseconds) throws Exception {
            if (hidden != null) return (Integer) hiddenIndex.invoke(hidden, markers.length, milliseconds, null);
            TextureRegion selected = slider != null ? ((SkinSource) sliderSource.get(slider)).getImage(milliseconds, null)
                                                   : image.getImage(milliseconds, null);
            for (int i = 0; i < markers.length; i++) if (markers[i] == selected) return i;
            throw new AssertionError("Native source did not return an original marker");
        }
    }

    private static void equal(long expected, long actual, String label) {
        if (expected != actual) throw new AssertionError(label + ": expected " + expected + ", got " + actual);
    }

    public static void main(String[] args) throws Exception {
        Path recipe = Path.of(args[0]).toAbsolutePath();
        Fixture fixture = new Fixture();
        Globals globals = JsePlatform.standardGlobals();
        LuaValue state = fixture.state();
        globals.set("main_state", state);
        globals.get("package").get("loaded").set("main_state", state);
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(recipe);
        int selectionChecks = 0, gateChecks = 0, offChecks = 0, fieldChecks = 0;
        boolean negativeTimestamp = false, positiveTimestamp = false;
        // Reload the factories at a fresh epoch too; cached Python assets may
        // never carry mutable Lua timer state across independent skin loads.
        for (int reload = 0; reload < 2; reload++) {
            LuaTable cases = globals.load(Files.readString(recipe)).call().checktable();
            for (int c = 1; c <= cases.length(); c++) {
                LuaValue scenario = cases.get(c);
                JsonSkin.Skin skin = loader.fromLuaValue(JsonSkin.Skin.class, scenario.get("skin"));
                Map<String, NativeSelection> selections = new HashMap<>();
                for (JsonSkin.Slider item : skin.slider) {
                    if (item.angle != 2 || item.range != 471 || item.type != 4 || item.changeable)
                        throw new AssertionError("Slider host movement fields changed");
                    selections.put(item.id, new NativeSelection(item.divx * item.divy, item.timer, item.cycle, "slider"));
                    fieldChecks++;
                }
                for (JsonSkin.HiddenCover item : skin.hiddenCover) {
                    if (item.disapearLine != 137 || !item.isDisapearLineLinkLift)
                        throw new AssertionError("Hidden host clipping fields changed");
                    selections.put(item.id, new NativeSelection(item.divx * item.divy, item.timer, item.cycle, "hiddenCover"));
                    fieldChecks++;
                }
                for (JsonSkin.LiftCover item : skin.liftCover) {
                    if (item.disapearLine != 137 || item.isDisapearLineLinkLift)
                        throw new AssertionError("Lift host clipping fields changed");
                    selections.put(item.id, new NativeSelection(item.divx * item.divy, item.timer, item.cycle, "liftCover"));
                    fieldChecks++;
                }
                for (JsonSkin.Image item : skin.image)
                    selections.put(item.id, new NativeSelection(item.divx * item.divy, item.timer, item.cycle, "image"));
                LuaValue samples = scenario.get("samples");
                for (int s = 1; s <= samples.length(); s++) {
                    LuaValue sample = samples.get(s);
                    fixture.now = sample.get("now").tolong();
                    for (JsonSkin.Destination dst : skin.destination) {
                        NativeSelection nativeObject = selections.get(dst.id);
                        int expected = sample.get("cells").get(dst.id).toint();
                        boolean active = expected >= 0;
                        for (int repeat = 0; repeat < 3; repeat++) {
                            if (dst.draw.get(null) != active) throw new AssertionError(dst.id + ": page/member draw mismatch");
                            gateChecks++;
                            long timestamp = nativeObject.timer.getMicro(null);
                            if (active) {
                                negativeTimestamp |= timestamp < 0;
                                positiveTimestamp |= timestamp > 0;
                                equal(0, timestamp % 1000, "integer-millisecond timestamp");
                                equal(expected * 100L + 50, fixture.now / 1000 - nativeObject.timer.get(null), "cell elapsed");
                                equal(expected, nativeObject.select(fixture.now / 1000), dst.id);
                                selectionChecks++;
                            } else {
                                equal(Long.MIN_VALUE, timestamp, "absent frame timer");
                                // OFF selects zero, so the separately checked draw=false is essential.
                                equal(0, nativeObject.select(fixture.now / 1000), "off native cell");
                                offChecks++;
                            }
                        }
                    }
                }
            }
        }
        if (!negativeTimestamp || !positiveTimestamp) throw new AssertionError("Both timestamp signs must be covered");
        System.out.println("{\"complete\":true,\"cpuOnly\":true,\"gpuVerified\":false,\"reloads\":2,\"selectionChecks\":"
            + selectionChecks + ",\"gateChecks\":" + gateChecks + ",\"offChecks\":" + offChecks
            + ",\"hostFieldChecks\":" + fieldChecks + ",\"positiveAndNegativeTimestamps\":true}");
    }
}
