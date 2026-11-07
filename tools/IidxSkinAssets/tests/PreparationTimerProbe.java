package bms.player.beatoraja.skin.lua;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashSet;
import java.util.Set;

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

/** CPU-only official parser, TimerProperty, CustomTimer and Skin cache. */
public final class PreparationTimerProbe {
    private static final class Fixture {
        LuaTable values = new LuaTable();
        Skin skin;
        int timeReads;
        boolean updating;

        LuaValue state() {
            LuaTable reads = new LuaTable();
            reads.set("timer_off_value", LuaValue.valueOf((double) Long.MIN_VALUE));
            reads.set("time", new ZeroArgFunction() {
                public LuaValue call() { timeReads++; return values.get("now"); }
            });
            reads.set("timer", new OneArgFunction() {
                public LuaValue call(LuaValue value) {
                    int id = value.checkint();
                    String field = switch (id) {
                        case 40 -> "ready";
                        case 41 -> "play";
                        case 2 -> "fade";
                        case 3 -> "failure";
                        default -> null;
                    };
                    if (field != null) return values.get(field);
                    if (updating || id < 10020 || id > 10023)
                        throw new AssertionError("Unexpected/cross-owner timer read: " + id);
                    return LuaValue.valueOf((double) skin.getMicroCustomTimer(id));
                }
            });
            reads.set("option", new OneArgFunction() {
                public LuaValue call(LuaValue value) {
                    return switch (value.checkint()) {
                        case 1080 -> values.get("practice");
                        case 80 -> values.get("loading");
                        default -> throw new AssertionError("Unexpected public option read: " + value);
                    };
                }
            });
            LuaTable proxy = new LuaTable(), meta = new LuaTable();
            meta.set("__index", new TwoArgFunction() {
                public LuaValue call(LuaValue table, LuaValue key) {
                    LuaValue value = reads.get(key);
                    if (value.isnil()) throw new AssertionError("Unexpected public API: " + key);
                    return value;
                }
            });
            meta.set("__newindex", new TwoArgFunction() {
                public LuaValue call(LuaValue table, LuaValue key) {
                    throw new AssertionError("Preparation attempted to write host state: " + key);
                }
            });
            proxy.setmetatable(meta);
            return proxy;
        }
    }

    private static void patch(LuaTable state, LuaValue values) {
        for (LuaValue key : values.checktable().keys()) state.set(key, values.get(key));
    }

    private static void equal(long expected, long actual, String location) {
        if (expected != actual)
            throw new AssertionError(location + ": expected " + expected + ", got " + actual);
    }

    public static void main(String[] args) throws Exception {
        Path path = Path.of(args[0]).toAbsolutePath();
        String recipe = Files.readString(path);
        int scenarios = 0, timerChecks = 0, gateChecks = 0, numericChecks = 0, cacheChecks = 0;
        // One combined owner set, then each owner alone: no implicit shared
        // state, ordering dependency, or lazy drawable update can pass this.
        for (int pass = 0; pass < 5; pass++) {
            Fixture fixture = new Fixture();
            Globals globals = JsePlatform.standardGlobals();
            globals.set("main_state", fixture.state());
            LuaValue cases = globals.load(recipe).call();
            LuaSkinLoader loader = LuaSkinLoader.sandboxed(path);
            for (int index = 1; index <= cases.length(); index++) {
                LuaValue item = cases.get(index);
                String name = item.get("name").checkjstring() + " / owner pass " + pass;
                fixture.values = new LuaTable();
                patch(fixture.values, item.get("initial"));
                SkinHeader header = new SkinHeader();
                header.setSourceResolution(Resolution.HD);
                header.setDestinationResolution(Resolution.HD);
                fixture.skin = new Skin(header);
                JsonSkin.Skin document = loader.fromLuaValue(JsonSkin.Skin.class, item.get("skin"));
                Set<Integer> selected = new HashSet<>();
                for (JsonSkin.CustomTimer definition : document.customTimers) {
                    if (pass != 0 && definition.id != 10019 + pass) continue;
                    if (!SkinPropertyMapper.isCustomTimerId(definition.id) || definition.timer == null)
                        throw new AssertionError(name + ": invalid official CustomTimer definition");
                    selected.add(definition.id);
                    fixture.skin.addCustomTimer(new CustomTimer(definition.id, definition.timer));
                    equal(Long.MIN_VALUE, fixture.skin.getMicroCustomTimer(definition.id), name + ": creation is OFF");
                }
                LuaValue steps = item.get("steps");
                for (int number = 1; number <= steps.length(); number++) {
                    LuaValue step = steps.get(number);
                    String location = name + " / step " + number;
                    patch(fixture.values, step.get("state"));
                    int before = fixture.timeReads;
                    fixture.updating = true;
                    fixture.skin.updateCustomObjects(null);
                    fixture.updating = false;
                    equal(selected.size(), fixture.timeReads - before, location + ": one update per owner/render");
                    for (int id : selected) {
                        equal(step.get("expected").get(id).tolong(), fixture.skin.getMicroCustomTimer(id), location + " / " + id);
                        timerChecks++;
                    }
                    for (JsonSkin.Destination destination : document.destination) {
                        if (!selected.contains(item.get("expressionTimers").get(destination.id).checkint())) continue;
                        if (destination.draw == null)
                            throw new AssertionError(location + ": native BooleanProperty not parsed");
                        if (destination.draw.get(null) != step.get("draws").get(destination.id).checkboolean())
                            throw new AssertionError(location + ": wrong public source boolean " + destination.id);
                        gateChecks++;
                    }
                    for (JsonSkin.Value value : document.value) {
                        if (!selected.contains(item.get("expressionTimers").get(value.id).checkint())) continue;
                        if (value.value == null)
                            throw new AssertionError(location + ": native IntegerProperty not parsed");
                        equal(step.get("numbers").get(value.id).checklong(), value.value.get(null), location + " / " + value.id);
                        numericChecks++;
                    }
                    before = fixture.timeReads;
                    for (int id : selected) {
                        long cached = fixture.skin.getMicroCustomTimer(id);
                        equal(cached, fixture.skin.getMicroCustomTimer(id), location + ": cached read");
                        fixture.skin.setMicroCustomTimer(id, -123);
                        equal(cached, fixture.skin.getMicroCustomTimer(id), location + ": active owner rejects writes");
                        cacheChecks += 2;
                    }
                    equal(before, fixture.timeReads, location + ": cached readers do not invoke owners");
                }
                scenarios++;
            }
        }
        System.out.println("{\"scenarios\":" + scenarios + ",\"timerChecks\":" + timerChecks
            + ",\"gateChecks\":" + gateChecks + ",\"numericChecks\":" + numericChecks
            + ",\"cacheChecks\":" + cacheChecks + "}");
    }
}
