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

/** CPU-only: official LuaSkinLoader, TimerProperty, CustomTimer and Skin cache. */
public final class FeedbackTimerProbe {
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
            for (String[] pair : new String[][] {{"gauge", "gauge"}, {"gauge_type", "kind"},
                    {"exscore", "score"}, {"exscore_best", "best"}, {"exscore_rival", "rival"}}) {
                final String field = pair[1];
                reads.set(pair[0], new ZeroArgFunction() {
                    public LuaValue call() { return values.get(field); }
                });
            }
            reads.set("timer", new OneArgFunction() {
                public LuaValue call(LuaValue id) {
                    int timer = id.checkint();
                    if (timer == 41) return values.get("play");
                    if (!SkinPropertyMapper.isCustomTimerId(timer))
                        throw new AssertionError("Unexpected public timer read: " + timer);
                    return LuaValue.valueOf((double) skin.getMicroCustomTimer(timer));
                }
            });
            reads.set("number", new OneArgFunction() {
                public LuaValue call(LuaValue id) {
                    if (id.checkint() != 106) throw new AssertionError("Unexpected public number read: " + id);
                    return values.get("notes");
                }
            });
            reads.set("option", new OneArgFunction() {
                public LuaValue call(LuaValue id) {
                    if (id.checkint() != 1240) throw new AssertionError("Unexpected public option read: " + id);
                    return values.get("qualified");
                }
            });
            // No writable host properties or event functions are exposed.
            LuaTable proxy = new LuaTable(), meta = new LuaTable();
            meta.set("__index", reads);
            meta.set("__newindex", new TwoArgFunction() {
                public LuaValue call(LuaValue table, LuaValue key) {
                    throw new AssertionError("Feedback attempted to write public host state: " + key);
                }
            });
            proxy.setmetatable(meta);
            return proxy;
        }
    }

    private static void update(LuaTable destination, LuaValue patch) {
        for (LuaValue key : patch.checktable().keys()) destination.set(key, patch.get(key));
    }

    private static void equal(long expected, long actual, String location) {
        if (expected != actual)
            throw new AssertionError(location + ": expected " + expected + ", got " + actual);
    }

    public static void main(String[] args) throws Exception {
        Path recipePath = Path.of(args[0]).toAbsolutePath();
        Fixture fixture = new Fixture();
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", fixture.state());
        LuaTable scenarios = globals.load(Files.readString(recipePath)).call().checktable();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(recipePath);
        int scenarioCount = 0, timerAssertions = 0, gateAssertions = 0, cacheAssertions = 0;
        for (int index = 1; index <= scenarios.length(); index++) {
            LuaValue scenario = scenarios.get(index);
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
                    throw new AssertionError(name + ": invalid native custom timer");
                fixture.skin.addCustomTimer(new CustomTimer(timer.id, timer.timer));
                equal(Long.MIN_VALUE, fixture.skin.getMicroCustomTimer(timer.id), name + ": creation is off");
            }
            LuaValue steps = scenario.get("steps");
            for (int number = 1; number <= steps.length(); number++) {
                LuaValue step = steps.get(number);
                String location = name + " / step " + number;
                update(fixture.values, step.get("state"));
                int before = fixture.timeReads;
                fixture.skin.updateCustomObjects(null);
                equal(document.customTimers.length, fixture.timeReads - before, location + ": once per render");
                if (!step.get("expected").isnil()) {
                    for (LuaValue key : step.get("expected").checktable().keys()) {
                        int id = key.checkint();
                        equal(step.get("expected").get(key).tolong(), fixture.skin.getMicroCustomTimer(id), location + " / " + id);
                        timerAssertions++;
                    }
                }
                if (!step.get("draws").isnil()) {
                    for (JsonSkin.Destination destination : document.destination) {
                        LuaValue expected = step.get("draws").get(destination.id);
                        if (expected.isnil() || destination.draw == null)
                            throw new AssertionError(location + ": missing actual native condition for " + destination.id);
                        if (expected.checkboolean() != destination.draw.get(null))
                            throw new AssertionError(location + ": wrong source dynamic visibility for " + destination.id);
                        gateAssertions++;
                    }
                }
                before = fixture.timeReads;
                for (JsonSkin.CustomTimer timer : document.customTimers) {
                    long cached = fixture.skin.getMicroCustomTimer(timer.id);
                    equal(cached, fixture.skin.getMicroCustomTimer(timer.id), location + ": repeated cached read");
                    fixture.skin.setMicroCustomTimer(timer.id, -123);
                    equal(cached, fixture.skin.getMicroCustomTimer(timer.id), location + ": active owner rejects external write");
                    cacheAssertions += 2;
                }
                equal(before, fixture.timeReads, location + ": readers must not re-evaluate closure");
            }
            scenarioCount++;
        }
        System.out.println("{\"scenarios\":" + scenarioCount + ",\"timerAssertions\":" + timerAssertions
            + ",\"gateAssertions\":" + gateAssertions + ",\"cacheAssertions\":" + cacheAssertions + "}");
    }
}
