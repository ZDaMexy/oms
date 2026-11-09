package bms.player.beatoraja.skin.lua;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.List;
import bms.player.beatoraja.MainState;
import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.TimerManager;
import bms.player.beatoraja.play.BMSPlayer;
import bms.player.beatoraja.skin.CustomTimer;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.SkinSlider;
import bms.player.beatoraja.skin.StretchType;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.json.JsonSkinObjectLoader;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.math.Rectangle;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;
import sun.misc.Unsafe;

/** Actual cached timers, Slider.value and acc3 colour preparation; no app, texture or DB. */
public final class Standard20ProgressProbe {
    private static final long OFF = Long.MIN_VALUE;
    private static Field field(Class<?> owner, String name) throws Exception {
        Field value = owner.getDeclaredField(name);
        value.setAccessible(true);
        return value;
    }
    private static final class Destinations extends JsonSkinObjectLoader<Skin> {
        Destinations() { super(null); }
        public Skin getSkin(SkinHeader header) { return new Skin(header); }
        void apply(Skin skin, SkinObject object, JsonSkin.Destination destination) {
            setDestination(skin, object, destination);
        }
    }
    private static void near(double expected, double actual, double tolerance, String message) {
        if (Math.abs(expected - actual) > tolerance)
            throw new AssertionError(message + ": " + expected + " != " + actual);
    }

    public static void main(String[] args) throws Exception {
        Path path = Path.of(args[0]).toAbsolutePath();
        Unsafe unsafe = (Unsafe) field(Unsafe.class, "theUnsafe").get(null);
        BMSPlayer player = (BMSPlayer) unsafe.allocateInstance(BMSPlayer.class);
        TimerManager timers = new TimerManager();
        field(MainState.class, "timer").set(player, timers);
        field(BMSPlayer.class, "playtime").setInt(player, 120000);
        Field nowField = field(TimerManager.class, "nowmicrotime");
        Field drawRect = field(SkinObject.class, "tmpRect");
        Field writer = field(SkinSlider.class, "writer");
        LuaTable actual = new LuaTable(), reads = new LuaTable(), proxy = new LuaTable(), meta = new LuaTable();
        new MainStatePropertyLuaApiExporter(player).export(actual);
        for (String key : new String[]{"time", "timer", "timer_off_value", "float_number"})
            reads.set(key, actual.get(key));
        reads.set("option", new OneArgFunction() {
            public LuaValue call(LuaValue id) {
                if (id.checkint() != 80 && id.checkint() != 1080)
                    throw new AssertionError("Unexpected option read");
                return LuaValue.FALSE; // A normal, fully-loaded synthetic READY epoch.
            }
        });
        meta.set("__index", new TwoArgFunction() {
            public LuaValue call(LuaValue table, LuaValue key) {
                LuaValue value = reads.get(key);
                if (value.isnil()) throw new AssertionError("Unexpected public API " + key);
                return value;
            }
        });
        meta.set("__newindex", new TwoArgFunction() {
            public LuaValue call(LuaValue key, LuaValue value) {
                throw new AssertionError("Progress attempted to write public state");
            }
        });
        proxy.setmetatable(meta);
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", proxy);
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(path);
        int geometryChecks = 0, alphaChecks = 0, readOnlyChecks = 0, count = 0;
        double maxOriginError = 0, maxAlphaByteError = 0;
        for (Resolution resolution : new Resolution[]{Resolution.HD, Resolution.FULLHD}) {
            LuaValue cases = globals.load(Files.readString(path)).call();
            count = cases.length();
            for (int i = 1; i <= count; i++) {
                LuaValue item = cases.get(i);
                JsonSkin.Skin document = loader.fromLuaValue(JsonSkin.Skin.class, item.get("skin"));
                SkinHeader header = new SkinHeader();
                header.setSourceResolution(Resolution.HD);
                header.setDestinationResolution(resolution);
                Skin skin = new Skin(header);
                field(MainState.class, "skin").set(player, skin);
                timers.setMainState(player);
                for (JsonSkin.CustomTimer definition : document.customTimers)
                    skin.addCustomTimer(new CustomTimer(definition.id, definition.timer));
                List<SkinSlider> sliders = new ArrayList<>();
                for (int j = 0; j < document.slider.length; j++) {
                    JsonSkin.Slider spec = document.slider[j];
                    if (spec.value == null || spec.changeable || spec.event != null)
                        throw new AssertionError("Original20 cursor is not a read-only native FloatProperty Slider");
                    SkinSlider slider = new SkinSlider(new TextureRegion[]{new TextureRegion()}, spec.timer,
                        spec.cycle, spec.angle, (int) (resolution.height / 720.0f * spec.range), spec.value, null);
                    slider.setStretch((StretchType) null);
                    if (writer.get(slider) != null) throw new AssertionError("Cursor became interactive");
                    new Destinations().apply(skin, slider, document.destination[j]);
                    sliders.add(slider);
                }
                List<long[]> states = new ArrayList<>();
                states.add(new long[]{0, OFF, OFF, OFF, OFF, 0});
                for (int tick = 0; tick <= 120; tick++)
                    states.add(new long[]{1000000 + Math.round(tick * 1000000.0 / 60), 1000000, OFF, OFF, OFF, 0});
                states.add(new long[]{3000000, 1000000, 3000000, 3000000, OFF, 0});
                for (long phase : new long[]{1, 19999, 20000, 20999, 319999, 320000, 999999, 1000000, 1300000})
                    states.add(new long[]{33000000 + phase, 1000000, 3000000, 33000000, OFF, phase});
                states.add(new long[]{35000000, 1000000, 3000000, 34450000, 35000000, 550000});
                states.add(new long[]{45000000, 1000000, 3000000, 44800000, 35000000, 550000});
                states.add(new long[]{50000000, 50000000, OFF, OFF, OFF, 0});
                states.add(new long[]{51000000, 50000000, OFF, OFF, OFF, 0});
                for (long[] state : states) {
                    long now = state[0];
                    nowField.setLong(timers, now);
                    timers.setMicroTimer(40, state[1]);
                    timers.setMicroTimer(41, state[2]);
                    timers.setMicroTimer(140, state[3]);
                    timers.setMicroTimer(3, state[4]);
                    skin.updateCustomObjects(player);
                    long presentation = skin.getMicroCustomTimer(10021);
                    double progress = state[2] == OFF ? 0 : Math.min((float) ((now - state[2]) / 1000) / 120000, 1);
                    double expectedY;
                    if (presentation != OFF && now - presentation < 2000000) {
                        int tick = Math.min(119, (int) ((now - presentation) * 60 / 1000000));
                        expectedY = 415 - (int) (405 * Math.sin(tick * Math.PI / 240));
                    } else expectedY = 10 + progress * 405;
                    double phase = (state[5] / 1000 % 1000) / 1000.0;
                    // Work with the native millisecond phase, then exact source byte key.
                    int alphaStep = (int) (state[5] / 1000 % 1000 / 20);
                    double expectedAlpha = Math.round(item.get("alpha").todouble() * (1 - alphaStep / 100.0) * 255) / 255.0;
                    long[] before = new long[TimerManager.timerCount];
                    for (int id = 0; id < before.length; id++) before[id] = timers.getMicroTimer(id);
                    double scale = resolution.height / 720.0;
                    for (int j = 0; j < sliders.size(); j++) {
                        SkinSlider slider = sliders.get(j);
                        slider.prepare(now / 1000, player);
                        if (!slider.draw) throw new AssertionError("Original20 cursor disappeared");
                        try { slider.draw(null); throw new AssertionError("CPU draw did not reach renderer"); }
                        catch (NullPointerException stopped) {
                            StackTraceElement first = stopped.getStackTrace()[0];
                            if (!first.getClassName().equals(SkinObject.class.getName()) || !first.getMethodName().equals("draw")) throw stopped;
                        }
                        Rectangle rect = (Rectangle) drawRect.get(slider);
                        double y = 720 - rect.y / scale - rect.height / scale;
                        double error = Math.abs(y - expectedY);
                        maxOriginError = Math.max(maxOriginError, error);
                        near(expectedY, y, 1.01, item.get("name") + " original20 Y at " + now);
                        near(item.get("x").get(j + 1).todouble(), rect.x / scale, .51, "Source20 X");
                        near(66, rect.width / scale, .51, "Complete source width");
                        near(72, rect.height / scale, .51, "Complete source height");
                        geometryChecks++;
                        double alphaError = Math.abs(slider.getColor().a - expectedAlpha) * 255;
                        maxAlphaByteError = Math.max(maxAlphaByteError, alphaError);
                        near(expectedAlpha, slider.getColor().a, .001, "Native acc3 must preserve step alpha");
                        alphaChecks++;
                        for (int id = 0; id < before.length; id++)
                            if (before[id] != timers.getMicroTimer(id)) throw new AssertionError("Public timer changed");
                        readOnlyChecks++;
                    }
                }
                skin.dispose();
            }
        }
        System.out.println("{\"cases\":" + count + ",\"geometryChecks\":" + geometryChecks
            + ",\"alphaChecks\":" + alphaChecks + ",\"readOnlyChecks\":" + readOnlyChecks
            + ",\"maxSourceOriginErrorPx\":" + maxOriginError + ",\"maxAlphaByteError\":" + maxAlphaByteError + "}");
    }
}
