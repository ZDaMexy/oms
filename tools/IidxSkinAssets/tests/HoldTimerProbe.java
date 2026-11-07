package bms.player.beatoraja.skin.lua;

import java.nio.file.Files;
import java.nio.file.Path;
import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.CustomTimer;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinPropertyMapper;
import bms.player.beatoraja.skin.json.JsonSkin;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.ZeroArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** CPU-only official LuaSkinLoader, TimerProperty, CustomTimer and Skin cache. */
public final class HoldTimerProbe {
    private static final class Fixture {
        LuaTable values = new LuaTable();
        Skin skin;
        int timeReads;

        LuaValue state() {
            LuaTable reads = new LuaTable();
            reads.set("timer_off_value", LuaValue.valueOf((double) Long.MIN_VALUE));
            reads.set("time", new ZeroArgFunction() {
                public LuaValue call() { timeReads++; return values.get("now"); }
            });
            reads.set("timer", new OneArgFunction() {
                public LuaValue call(LuaValue argument) {
                    int id = argument.checkint();
                    if (id == 41) return values.get("play");
                    if (SkinPropertyMapper.isCustomTimerId(id))
                        return LuaValue.valueOf((double) skin.getMicroCustomTimer(id));
                    boolean hold = id >= 70 && id <= 87 && id % 10 < 8;
                    boolean active = id >= 250 && id <= 267 && id % 10 < 8;
                    boolean damage = id >= 270 && id <= 287 && id % 10 < 8;
                    if (!hold && !active && !damage)
                        throw new AssertionError("Unexpected timer (ordinary keys are not hold state): " + id);
                    LuaValue value = values.get("timers").get(id);
                    return value.isnil() ? LuaValue.valueOf((double) Long.MIN_VALUE) : value;
                }
            });
            LuaTable proxy = new LuaTable(), meta = new LuaTable();
            meta.set("__index", reads);
            meta.set("__newindex", new TwoArgFunction() {
                public LuaValue call(LuaValue key, LuaValue value) {
                    throw new AssertionError("Hold effects attempted to write public state: " + key);
                }
            });
            proxy.setmetatable(meta);
            return proxy;
        }
    }

    private static void update(LuaTable destination, LuaValue patch) {
        for (LuaValue key : patch.checktable().keys()) destination.set(key, patch.get(key));
    }

    private static void equal(long expected, long actual, String where) {
        if (expected != actual) throw new AssertionError(where + ": expected " + expected + ", got " + actual);
    }

    public static void main(String[] args) throws Exception {
        Path recipe = Path.of(args[0]).toAbsolutePath();
        Fixture fixture = new Fixture();
        Globals globals = JsePlatform.standardGlobals();
        LuaValue state = fixture.state();
        globals.set("main_state", state);
        globals.get("package").get("loaded").set("main_state", state);
        LuaTable cases = globals.load(Files.readString(recipe)).call().checktable();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(recipe);
        int timerAssertions = 0, gateAssertions = 0, cacheAssertions = 0;
        for (int index = 1; index <= cases.length(); index++) {
            LuaValue scenario = cases.get(index);
            String name = scenario.get("name").checkjstring();
            fixture.values = new LuaTable();
            update(fixture.values, scenario.get("initial"));
            SkinHeader header = new SkinHeader();
            header.setSourceResolution(Resolution.HD);
            header.setDestinationResolution(Resolution.HD);
            fixture.skin = new Skin(header);
            JsonSkin.Skin document = loader.fromLuaValue(JsonSkin.Skin.class, scenario.get("skin"));
            for (JsonSkin.CustomTimer timer : document.customTimers) {
                if (!SkinPropertyMapper.isCustomTimerId(timer.id) || timer.timer == null)
                    throw new AssertionError(name + ": invalid native timer");
                fixture.skin.addCustomTimer(new CustomTimer(timer.id, timer.timer));
                equal(Long.MIN_VALUE, fixture.skin.getMicroCustomTimer(timer.id), name + ": initial off");
            }
            LuaValue steps = scenario.get("steps");
            for (int number = 1; number <= steps.length(); number++) {
                LuaValue step = steps.get(number);
                String where = name + " / step " + number;
                update(fixture.values, step.get("state"));
                int before = fixture.timeReads;
                fixture.skin.updateCustomObjects(null);
                equal(document.customTimers.length, fixture.timeReads - before, where + ": one update per render");
                for (LuaValue key : step.get("expected").checktable().keys()) {
                    equal(step.get("expected").get(key).tolong(), fixture.skin.getMicroCustomTimer(key.checkint()), where + " / " + key);
                    timerAssertions++;
                }
                if (!step.get("draws").isnil()) {
                    for (JsonSkin.Destination destination : document.destination) {
                        LuaValue expected = step.get("draws").get(destination.id);
                        if (expected.isnil() || destination.draw == null || expected.checkboolean() != destination.draw.get(null))
                            throw new AssertionError(where + ": incorrect original animation visibility: " + destination.id);
                        gateAssertions++;
                    }
                }
                before = fixture.timeReads;
                for (JsonSkin.CustomTimer timer : document.customTimers) {
                    long cached = fixture.skin.getMicroCustomTimer(timer.id);
                    fixture.skin.setMicroCustomTimer(timer.id, -123);
                    equal(cached, fixture.skin.getMicroCustomTimer(timer.id), where + ": active timer rejects external writes");
                    equal(cached, fixture.skin.getMicroCustomTimer(timer.id), where + ": cached repeated read");
                    cacheAssertions += 2;
                }
                equal(before, fixture.timeReads, where + ": cache reads do not evaluate callbacks");
            }
        }
        System.out.println("{\"scenarios\":" + cases.length() + ",\"timerAssertions\":" + timerAssertions
            + ",\"gateAssertions\":" + gateAssertions + ",\"cacheAssertions\":" + cacheAssertions + "}");
    }
}
