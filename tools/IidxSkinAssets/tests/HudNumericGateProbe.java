package bms.player.beatoraja.skin.lua;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import bms.model.BMSModel;
import bms.model.Mode;
import bms.model.TimeLine;
import bms.player.beatoraja.MainState;
import bms.player.beatoraja.PlayerResource;
import bms.player.beatoraja.play.BMSPlayer;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.property.FloatPropertyFactory;
import bms.player.beatoraja.song.SongData;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;
import sun.misc.Unsafe;

/** Real public properties and LuaJ callbacks, data-only: no app, GPU, database or installation writes. */
public final class HudNumericGateProbe {
    private static Field field(Class<?> owner, String name) throws Exception {
        Field value = owner.getDeclaredField(name);
        value.setAccessible(true);
        return value;
    }

    private static SongData song(double minimum, double maximum) {
        BMSModel model = new BMSModel();
        model.setMode(Mode.BEAT_7K);
        model.setBpm(minimum);
        model.setJudgerank(100);
        TimeLine first = new TimeLine(0, 0, 8), last = new TimeLine(1, 2000000, 8);
        first.setBPM(minimum);
        last.setBPM(maximum);
        model.setAllTimeLine(new TimeLine[]{first, last});
        return new SongData(model, false, false);
    }

    public static void main(String[] args) throws Exception {
        Unsafe unsafe = (Unsafe) field(Unsafe.class, "theUnsafe").get(null);
        BMSPlayer player = (BMSPlayer) unsafe.allocateInstance(BMSPlayer.class);
        PlayerResource resource = (PlayerResource) unsafe.allocateInstance(PlayerResource.class);
        field(MainState.class, "resource").set(player, resource);
        LuaTable actual = new LuaTable();
        new MainStatePropertyLuaApiExporter(player).export(actual);
        // Only the exported read API is supplied to generated callbacks.
        LuaTable reads = new LuaTable(), proxy = new LuaTable(), meta = new LuaTable();
        reads.set("number", actual.get("number"));
        meta.set("__index", reads);
        meta.set("__newindex", new TwoArgFunction() {
            public LuaValue call(LuaValue key, LuaValue value) {
                throw new AssertionError("Numeric display attempted to write host state");
            }
        });
        proxy.setmetatable(meta);
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", proxy);
        Path path = Path.of(args[0]).toAbsolutePath();
        LuaValue recipe = globals.load(Files.readString(path)).call();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(path);
        JsonSkin.Skin hud = loader.fromLuaValue(JsonSkin.Skin.class, recipe.get("hud"));
        JsonSkin.Skin art = loader.fromLuaValue(JsonSkin.Skin.class, recipe.get("art"));
        Map<String, JsonSkin.Text> texts = new HashMap<>();
        for (JsonSkin.Text text : hud.text) texts.put(text.id, text);
        if (FloatPropertyFactory.getFloatProperty(90) != null || FloatPropertyFactory.getFloatProperty(91) != null
                || FloatPropertyFactory.getFloatProperty("maxbpm") != null
                || FloatPropertyFactory.getFloatProperty("minbpm") != null)
            throw new AssertionError("Official API changed: requalify precise BPM extrema");
        int checks = 0, boundary = 0, models = 0;
        for (double[] pair : new double[][]{{128, 128}, {90, 150}, {128.01, 128.99}, {128.99, 129.01}}) {
            SongData song = song(pair[0], pair[1]);
            field(PlayerResource.class, "songdata").set(resource, song);
            int minimum = actual.get("number").call(LuaValue.valueOf(91)).checkint();
            int maximum = actual.get("number").call(LuaValue.valueOf(90)).checkint();
            if (minimum != (int) pair[0] || maximum != (int) pair[1])
                throw new AssertionError("Official extrema did not retain the observed integer cast");
            if (actual.get("float_number").call(LuaValue.valueOf(90)).todouble() != 0
                    || actual.get("float_number").call(LuaValue.valueOf(91)).todouble() != 0)
                throw new AssertionError("Unknown float BPM property unexpectedly appeared");
            boundary++;
            boolean available = maximum > minimum;
            for (JsonSkin.Destination destination : art.destination) {
                if (destination.draw == null || destination.draw.get(player) != available)
                    throw new AssertionError("Original MIN/MAX label gate differs from integer host range");
                checks++;
            }
            for (JsonSkin.Destination destination : hud.destination) {
                JsonSkin.Text text = texts.get(destination.id);
                boolean expected = available;
                if (destination.id.contains("leading-zero.n0"))
                    expected &= (destination.id.contains("min-bpm") ? minimum : maximum) < 99.5;
                if (destination.id.contains("leading-zero.n1"))
                    expected &= (destination.id.contains("min-bpm") ? minimum : maximum) < 9.5;
                if (destination.draw == null || destination.draw.get(player) != expected)
                    throw new AssertionError("Original number/dim-zero gate escaped MIN/MAX parent");
                if (available && text.value != null) {
                    int expectedValue = destination.id.contains("min-bpm") ? minimum : maximum;
                    if (!Integer.toString(expectedValue).equals(text.value.get(player)))
                        throw new AssertionError("Displayed MIN/MAX did not read real public property");
                }
                checks++;
            }
            models++;
        }
        System.out.println("{\"models\":" + models + ",\"gateChecks\":" + checks
            + ",\"fractionalBoundaryChecks\":" + boundary + "}");
    }
}
