package bms.player.beatoraja.skin.lua;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Locale;
import java.util.Map;
import java.util.Set;

import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.json.JsonSkinObjectLoader;
import bms.player.beatoraja.skin.property.FloatPropertyFactory;
import bms.player.beatoraja.skin.property.IntegerPropertyFactory;
import bms.player.beatoraja.skin.property.StringPropertyFactory;
import com.badlogic.gdx.math.Rectangle;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.ZeroArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Official parser/callbacks/destination geometry; no app, assets, DB or GPU. */
public final class GraphLuaProbe {
    private static int notes = 100, target = 101, exscore, percentage = 50;
    private static long time = 123456789;
    private static float rate = .505f;

    private static final class Geometry extends SkinObject {
        public void draw(Skin.SkinObjectRenderer renderer) {
            throw new AssertionError("Graph CPU probe must not draw on a GPU");
        }
        public void dispose() { setDisposed(); }
    }

    private static final class Destinations extends JsonSkinObjectLoader<Skin> {
        Destinations() { super(null); }
        public Skin getSkin(SkinHeader header) { return new Skin(header); }
        void apply(Skin skin, SkinObject object, JsonSkin.Destination destination) {
            setDestination(skin, object, destination);
        }
    }

    private static LuaTable reads() {
        LuaTable reads = new LuaTable();
        reads.set("number", new OneArgFunction() {
            public LuaValue call(LuaValue id) {
                return LuaValue.valueOf(switch (id.checkint()) {
                    case 106 -> notes;
                    case 135 -> percentage;
                    default -> throw new AssertionError("Unexpected Graph number " + id);
                });
            }
        });
        reads.set("exscore", new ZeroArgFunction() {
            public LuaValue call() { return LuaValue.valueOf(exscore); }
        });
        reads.set("exscore_best", new ZeroArgFunction() {
            public LuaValue call() { return LuaValue.valueOf(0); }
        });
        reads.set("exscore_rival", new ZeroArgFunction() {
            public LuaValue call() { return LuaValue.valueOf(target); }
        });
        reads.set("float_number", new OneArgFunction() {
            public LuaValue call(LuaValue id) {
                if (id.checkint() != 115) throw new AssertionError("Target art reads non-target rate " + id);
                return LuaValue.valueOf(rate);
            }
        });
        reads.set("time", new ZeroArgFunction() {
            public LuaValue call() { return LuaValue.valueOf((double) time); }
        });
        LuaTable proxy = new LuaTable(), metatable = new LuaTable();
        metatable.set("__index", reads);
        metatable.set("__newindex", new TwoArgFunction() {
            public LuaValue call(LuaValue table, LuaValue key) {
                throw new AssertionError("Graph artwork tried to change host state: " + key);
            }
        });
        proxy.setmetatable(metatable);
        return proxy;
    }

    public static void main(String[] args) throws Exception {
        if (IntegerPropertyFactory.getIntegerProperty(135) == null
                || FloatPropertyFactory.getRateProperty(115) == null
                || StringPropertyFactory.getStringProperty(3) == null)
            throw new AssertionError("Official target properties are unavailable");
        Path recipe = Path.of(args[0]).toAbsolutePath();
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", reads());
        LuaValue cases = globals.load(Files.readString(recipe)).call();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(recipe);
        int numericChecks = 0, gateChecks = 0, nameChecks = 0, clockChecks = 0, geometryChecks = 0;
        double maxClockError = 0, maxGeometryError = 0;
        boolean fractionalPercentage = false, positiveOrigin = false, negativeOrigin = false;
        Destinations destinationsLoader = new Destinations();
        for (int i = 1; i <= cases.length(); i++) {
            LuaValue item = cases.get(i), roles = item.get("roles");
            String caseName = item.get("name").checkjstring();
            JsonSkin.Skin document = loader.fromLuaValue(JsonSkin.Skin.class, item.get("skin"));
            Map<String, JsonSkin.Destination> destinations = new HashMap<>();
            for (JsonSkin.Destination destination : document.destination) destinations.put(destination.id, destination);
            Map<String, JsonSkin.Value> values = new HashMap<>();
            for (JsonSkin.Value value : document.value) values.put(value.id, value);
            Map<String, JsonSkin.Text> texts = new HashMap<>();
            for (JsonSkin.Text text : document.text) texts.put(text.id, text);
            Set<String> expectedKinds = new HashSet<>();
            for (LuaValue id : roles.checktable().keys()) expectedKinds.add(roles.get(id).get("kind").checkjstring());
            for (int count : new int[] {0, 1, 49, 50, 51, 100, 499, 500, 501, 4999, 5000, 5001,
                    49999, 50000, 50001, 499999, 500000, 500001, 4999999, 5000000, 5000001, Integer.MAX_VALUE}) {
                notes = count;
                boolean available = notes > 0 && notes < Integer.MAX_VALUE;
                int maximum = available ? notes * 2 : 0;
                target = available ? maximum / 2 + 1 : 0;
                rate = available ? (float) target / maximum : 0;
                percentage = (int) (rate * 100);
                fractionalPercentage |= percentage != Math.round(rate * 100);
                for (int current : new int[] {0, target, Math.min(maximum, target + 1)}) {
                    exscore = current;
                    Map<String, Integer> active = new HashMap<>();
                    for (LuaValue key : roles.checktable().keys()) {
                        String id = key.checkjstring();
                        LuaValue role = roles.get(key);
                        String kind = role.get("kind").checkjstring();
                        JsonSkin.Destination destination = destinations.get(id);
                        if (destination == null) throw new AssertionError(caseName + ": missing role " + id);
                        boolean expected = available
                            && (role.get("low").isnil() || maximum >= role.get("low").todouble())
                            && (role.get("high").isnil() || maximum < role.get("high").todouble());
                        boolean visible = destination.draw == null || destination.draw.get(null);
                        if (visible != expected) throw new AssertionError(caseName + ": font/existence gate " + id + " maximum=" + maximum);
                        if (visible) active.merge(kind, 1, Integer::sum);
                        gateChecks++;
                        if (kind.equals("name")) {
                            JsonSkin.Text text = texts.get(id);
                            if (text == null || text.ref != 3 || text.value != null || text.constantText != null)
                                throw new AssertionError(caseName + ": source target label survived " + id);
                            nameChecks++;
                            continue;
                        }
                        int wanted = switch (kind) {
                            case "total" -> target;
                            case "difference" -> exscore - target;
                            case "percentage" -> percentage;
                            default -> throw new AssertionError("Unknown expected role " + kind);
                        };
                        if (values.containsKey(id)) {
                            if (values.get(id).value.get(null) != wanted)
                                throw new AssertionError(caseName + ": wrong native number " + id);
                        } else if (!texts.containsKey(id) || !Integer.toString(wanted).equals(texts.get(id).value.get(null))) {
                            throw new AssertionError(caseName + ": wrong native text " + id);
                        }
                        numericChecks++;
                    }
                    for (String kind : expectedKinds)
                        if (active.getOrDefault(kind, 0) != (available ? 1 : 0))
                            throw new AssertionError(caseName + ": overlapping/missing role " + kind + " maximum=" + maximum);
                }
            }
            notes = 100;
            for (JsonSkin.Graph graph : document.graph) {
                if (graph.type != 115) continue;
                JsonSkin.Destination destination = destinations.get(graph.id);
                if (graph.angle != 1) throw new AssertionError("Target bar changed growth direction");
                JsonSkin.Animation full = destination.dst[destination.dst.length - 1];
                if (!item.get("targetBarHeight").isnil() && full.h != item.get("targetBarHeight").checkint())
                    throw new AssertionError(caseName + ": original full bar height changed");
                if (destination.timer == null) continue; // Standard keeps its original reveal clip.
                if (graph.value == null || graph.value.get(null) != 1)
                    throw new AssertionError("Scaled gradient became a cropped native bar");
                for (Resolution resolution : new Resolution[] {Resolution.HD, Resolution.FULLHD}) {
                    SkinHeader header = new SkinHeader();
                    header.setSourceResolution(document.h == 720 ? Resolution.HD : Resolution.FULLHD);
                    header.setDestinationResolution(resolution);
                    Skin skin = new Skin(header);
                    Geometry geometry = new Geometry();
                    destinationsLoader.apply(skin, geometry, destination);
                    double scale = resolution.height / (double) document.h;
                    for (float sample : new float[] {0, .0000003f, .25f, .505f, .9999998f, 1}) {
                        rate = sample;
                        for (long now : new long[] {500, 123456789, 123456789012L}) {
                            time = now;
                            long origin = destination.timer.getMicro(null);
                            positiveOrigin |= origin > 0;
                            negativeOrigin |= origin < 0;
                            double wanted = sample * (double) destination.loop;
                            long elapsed = now / 1000 - destination.timer.get(null);
                            double error = Math.abs(elapsed - wanted);
                            if (error > .500001) throw new AssertionError(caseName + ": target timer quantisation " + error);
                            maxClockError = Math.max(maxClockError, error);
                            clockChecks++;
                            geometry.prepare(now / 1000, null);
                            Rectangle rect = geometry.getDestination(now / 1000, null);
                            if (rect == null) throw new AssertionError(caseName + ": target bar lost its destination");
                            double geometryError = Math.abs(rect.height / scale - full.h * (double) sample);
                            if (geometryError > full.h * .0000005 + .0001)
                                throw new AssertionError(caseName + ": target bar height drift " + geometryError);
                            maxGeometryError = Math.max(maxGeometryError, geometryError);
                            geometryChecks++;
                        }
                    }
                    geometry.dispose();
                    skin.dispose();
                }
            }
        }
        if (!fractionalPercentage || !positiveOrigin || !negativeOrigin)
            throw new AssertionError("Probe omitted integer-percent or signed-timer boundaries");
        System.out.printf(Locale.ROOT,
            "{\"cases\":%d,\"numericChecks\":%d,\"gateChecks\":%d,\"nameChecks\":%d,\"clockChecks\":%d,\"geometryChecks\":%d,\"maxClockErrorMs\":%.9f,\"maxGeometryErrorPx\":%.9f}%n",
            cases.length(), numericChecks, gateChecks, nameChecks, clockChecks, geometryChecks, maxClockError, maxGeometryError);
    }
}
