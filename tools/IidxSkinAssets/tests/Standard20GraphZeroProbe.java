package bms.player.beatoraja.skin.lua;

import java.nio.file.Files;
import java.nio.file.Path;
import java.lang.reflect.Field;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.SkinNumber;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.json.JsonSkinObjectLoader;
import com.badlogic.gdx.math.Rectangle;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.ZeroArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Real official parser/properties/geometry only. Never decodes assets or draws. */
public final class Standard20GraphZeroProbe {
    private static int notes = 100, current = 40, target = 200;
    private static final Pattern ZERO = Pattern.compile(".*\\.leading-zero\\.n([012])\\.fit([0-4])$");
    private static final Pattern RANK = Pattern.compile(".*\\.line\\.(aaa|aa|a)(?:\\.(reached))?$");
    private static final int[] LIMITS = {0, 10000, 100000, 1000000, 10000000};
    private static final double[] FACTORS = {1, 89.0 / 111, 89.0 / 133, 89.0 / 155, 89.0 / 221};

    private static final class Geometry extends SkinObject {
        public void draw(Skin.SkinObjectRenderer renderer) {
            throw new AssertionError("CPU Graph probe must never draw");
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
                    case 135 -> 80;
                    default -> throw new AssertionError("Unexpected Graph source " + id);
                });
            }
        });
        reads.set("exscore", new ZeroArgFunction() {
            public LuaValue call() { return LuaValue.valueOf(current); }
        });
        reads.set("exscore_rival", new ZeroArgFunction() {
            public LuaValue call() { return LuaValue.valueOf(target); }
        });
        LuaTable proxy = new LuaTable(), metatable = new LuaTable();
        metatable.set("__index", reads);
        metatable.set("__newindex", new TwoArgFunction() {
            public LuaValue call(LuaValue table, LuaValue key) {
                throw new AssertionError("Graph tried to write host state " + key);
            }
        });
        proxy.setmetatable(metatable);
        return proxy;
    }

    public static void main(String[] args) throws Exception {
        Path recipe = Path.of(args[0]).toAbsolutePath();
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", reads());
        LuaValue cases = globals.load(Files.readString(recipe)).call();
        LuaSkinLoader parser = LuaSkinLoader.sandboxed(recipe);
        Destinations loader = new Destinations();
        int gates = 0, geometryChecks = 0, numbers = 0, rankChecks = 0, jointChecks = 0;
        double maxError = 0, maxJointError = 0;
        Field images = SkinNumber.class.getDeclaredField("currentImages"); images.setAccessible(true);
        Field shift = SkinNumber.class.getDeclaredField("shift"); shift.setAccessible(true);
        int[] samples = {0, 9, 10, 40, 99, 100, 200, 999, 1000, 9999, 10000};
        for (int i = 1; i <= cases.length(); i++) {
            LuaValue item = cases.get(i);
            JsonSkin.Skin document = parser.fromLuaValue(JsonSkin.Skin.class, item.get("skin"));
            String name = item.get("name").checkjstring();
            int left = item.get("left").checkint();
            for (Resolution resolution : new Resolution[] {Resolution.HD, Resolution.FULLHD}) {
                SkinHeader header = new SkinHeader();
                header.setSourceResolution(Resolution.HD);
                header.setDestinationResolution(resolution);
                Skin skin = new Skin(header);
                double scale = resolution.height / 720.0;
                Map<String, Geometry> objects = new HashMap<>();
                Map<String, JsonSkin.Destination> destinations = new HashMap<>();
                for (JsonSkin.Destination destination : document.destination) {
                    destinations.put(destination.id, destination);
                    if (!ZERO.matcher(destination.id).matches()) continue;
                    Geometry geometry = new Geometry();
                    loader.apply(skin, geometry, destination);
                    objects.put(destination.id, geometry);
                }
                Map<String, SkinNumber> nativeNumbers = new HashMap<>();
                for (JsonSkin.Value value : document.value) {
                    if (!value.id.matches(".*\\.(current\\.value|total)\\.[0-4]$")) continue;
                    TextureRegion[][] cells = new TextureRegion[1][10];
                    for (int digit = 0; digit < 10; digit++) cells[0][digit] = new TextureRegion();
                    SkinNumber number = new SkinNumber(cells, value.timer, 0, value.digit, value.padding, value.space, value.value, value.align);
                    loader.apply(skin, number, destinations.get(value.id));
                    nativeNumbers.put(value.id, number);
                }
                if (objects.size() != 30) throw new AssertionError("Missing own20 zero objects " + name);
                for (int count : new int[] {0, 1, 4999, 5000, 49999, 50000, 499999, 500000, 5000000}) {
                    notes = count;
                    int maximum = notes * 2;
                    for (int sample = 0; sample < samples.length; sample++) {
                        current = samples[sample];
                        target = samples[(sample + 3) % samples.length];
                        int activeCurrent = 0, activeTarget = 0;
                        for (JsonSkin.Destination destination : document.destination) {
                            Matcher match = ZERO.matcher(destination.id);
                            if (!match.matches()) continue;
                            int column = Integer.parseInt(match.group(1));
                            int fit = Integer.parseInt(match.group(2));
                            boolean isTarget = destination.id.contains(".target.");
                            int actual = isTarget ? target : current;
                            int threshold = new int[] {1000, 100, 10}[column];
                            boolean expected = notes > 0 && maximum >= LIMITS[fit]
                                && (fit == 4 || maximum < LIMITS[fit + 1]) && actual < threshold;
                            boolean visible = destination.draw == null || destination.draw.get(null);
                            if (visible != expected)
                                throw new AssertionError(name + " gate " + destination.id + " max=" + maximum + " current=" + current + " target=" + target);
                            gates++;
                            if (!visible) continue;
                            if (isTarget) activeTarget++; else activeCurrent++;
                            Geometry geometry = objects.get(destination.id);
                            geometry.prepare(0, null);
                            Rectangle rect = geometry.getDestination(0, null);
                            if (rect == null) throw new AssertionError("Missing zero geometry");
                            double factor = FACTORS[fit];
                            double x = left + 267 - (89 - column * 22) * factor;
                            double top = isTarget ? 57 : 30;
                            double[] wanted = {x, 720 - (top + 11 - 11 * factor) - 22 * factor, 23 * factor, 22 * factor};
                            double[] found = {rect.x / scale, rect.y / scale, rect.width / scale, rect.height / scale};
                            for (int j = 0; j < 4; j++) {
                                double error = Math.abs(wanted[j] - found[j]);
                                maxError = Math.max(maxError, error);
                                double bound = switch(j) {
                                    case 0 -> Math.abs(Math.rint(23 * factor) - 23 * factor) + 3 * Math.abs(Math.rint(22 * factor) - 22 * factor) + .0002;
                                    case 1 -> .7502;
                                    default -> .5002;
                                };
                                if (error > bound) throw new AssertionError(name + " native cell quantisation bound " + destination.id + " " + error);
                            }
                            String numberId = destination.id.substring(0, destination.id.indexOf(".leading-zero.n")) + "." + fit;
                            SkinNumber number = nativeNumbers.get(numberId);
                            if (number == null) throw new AssertionError("Zero lost its original native integer field");
                            number.prepare(0, null);
                            if (!number.draw) throw new AssertionError("Native integer disappeared while its zero is visible");
                            Rectangle numberRect = number.getDestination(0, null);
                            TextureRegion[] activeImages = (TextureRegion[]) images.get(number);
                            String digits = Integer.toString(actual);
                            double advance = number.getLength() / digits.length();
                            int last = -1;
                            for (int cell = 0; cell < activeImages.length; cell++) if (activeImages[cell] != null) last = cell;
                            if (last < 0) throw new AssertionError("Native integer has no actual prepared glyphs");
                            double right = numberRect.x + advance * last - shift.getFloat(number) + numberRect.width;
                            double expectedX = right - (3 - column) * advance - numberRect.width;
                            double jointError = Math.max(Math.abs(rect.x - expectedX), Math.max(Math.abs(rect.y - numberRect.y),
                                Math.max(Math.abs(rect.width - numberRect.width), Math.abs(rect.height - numberRect.height))));
                            maxJointError = Math.max(maxJointError, jointError / scale);
                            if (jointError / scale > .0003)
                                throw new AssertionError(name + " main-number/zero joint bounds " + destination.id + " error=" + jointError / scale);
                            jointChecks++;
                            if (destination.dst[0].a != 255) throw new AssertionError("Zero was dimmed");
                            geometryChecks++;
                        }
                        int expectedCurrent = notes == 0 ? 0 : Math.max(0, 4 - Integer.toString(current).length());
                        int expectedTarget = notes == 0 ? 0 : Math.max(0, 4 - Integer.toString(target).length());
                        if (activeCurrent != expectedCurrent || activeTarget != expectedTarget)
                            throw new AssertionError(name + " wrong displayed zero prefix");
                        for (JsonSkin.Value value : document.value) {
                            if (value.id.contains(".current.value.")) {
                                if (value.value.get(null) != current) throw new AssertionError("YOU source changed");
                                numbers++;
                            } else if (value.id.contains(".total.")) {
                                if (value.value.get(null) != target) throw new AssertionError("Target source changed");
                                numbers++;
                            }
                        }
                    }
                    for (String rank : new String[] {"aaa", "aa", "a"}) {
                        int numerator = switch (rank) {case "aaa" -> 8; case "aa" -> 7; default -> 6;};
                        int threshold = (int) Math.ceil(maximum * (double) numerator / 9);
                        for (int delta : new int[] {-1, 0, 1}) {
                            current = threshold + delta;
                            int active = 0;
                            for (JsonSkin.Destination destination : document.destination) {
                                Matcher match = RANK.matcher(destination.id);
                                if (!match.matches() || !match.group(1).equals(rank)) continue;
                                boolean reached = match.group(2) != null;
                                boolean expected = notes > 0 && reached == (current >= threshold);
                                boolean visible = destination.draw == null || destination.draw.get(null);
                                if (visible != expected)
                                    throw new AssertionError(name + " rank colour " + rank + " max=" + maximum + " delta=" + delta);
                                if (destination.timer != null) throw new AssertionError("Numeric rank gap became a timer/off sentinel");
                                JsonSkin.Animation frame = destination.dst[0];
                                if (frame.a != 255 || frame.r != (reached ? 255 : 135)
                                        || frame.g != (reached ? 255 : 135) || frame.b != (reached ? 0 : 255))
                                    throw new AssertionError("Wrong original20 ARGB channel projection");
                                if (visible) active++;
                                rankChecks++;
                            }
                            if (active != (notes > 0 ? 1 : 0)) throw new AssertionError("Rank lost/duplicated colour state");
                        }
                    }
                }
                for (Geometry geometry : objects.values()) geometry.dispose();
                skin.dispose();
            }
        }
        System.out.printf(Locale.ROOT, "{\"cases\":%d,\"gateChecks\":%d,\"geometryChecks\":%d,\"numericChecks\":%d,\"rankChecks\":%d,\"jointNativeCellChecks\":%d,\"maxJointNativeCellErrorPx\":%.9f,\"maxSourceGeometryErrorPx\":%.9f}%n",
            cases.length(), gates, geometryChecks, numbers, rankChecks, jointChecks, maxJointError, maxError);
    }
}
