package bms.player.beatoraja.skin.lua;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;

import bms.player.beatoraja.MainState;
import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.TimerManager;
import bms.player.beatoraja.play.BMSPlayer;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.SkinSlider;
import bms.player.beatoraja.skin.StretchType;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.json.JsonSkinObjectLoader;
import bms.player.beatoraja.skin.property.FloatPropertyFactory;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.math.Rectangle;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.jse.JsePlatform;
import sun.misc.Unsafe;

/** Official parser, properties, destinations and Slider CPU draw; no GL or app. */
public final class ProgressProbe {
    private static final class Destinations extends JsonSkinObjectLoader<Skin> {
        Destinations() { super(null); }
        public Skin getSkin(SkinHeader header) { return new Skin(header); }
        void apply(Skin skin, SkinObject object, JsonSkin.Destination destination) {
            setDestination(skin, object, destination);
        }
    }

    private static Field field(Class<?> owner, String name) throws Exception {
        Field field = owner.getDeclaredField(name);
        field.setAccessible(true);
        return field;
    }

    public static void main(String[] args) throws Exception {
        Path path = Path.of(args[0]).toAbsolutePath();
        LuaValue cases = JsePlatform.standardGlobals().load(Files.readString(path)).call();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(path);
        // Allocate a data-only player without running its constructor, loading
        // charts, opening an app or touching any user's configuration/database.
        Unsafe unsafe = (Unsafe) field(Unsafe.class, "theUnsafe").get(null);
        BMSPlayer player = (BMSPlayer) unsafe.allocateInstance(BMSPlayer.class);
        TimerManager timer = new TimerManager();
        field(MainState.class, "timer").set(player, timer);
        field(BMSPlayer.class, "playtime").setInt(player, 120000);
        timer.setMainState(player);
        Field now = field(TimerManager.class, "nowmicrotime");
        Field drawRect = field(SkinObject.class, "tmpRect");
        Field writer = field(SkinSlider.class, "writer");
        Destinations destinations = new Destinations();
        int checks = 0, propertyChecks = 0, sourceObjects = 0;
        double maxOriginError = 0, maxEdgeError = 0;
        // Includes before PLAY, start, fractional/end/overshoot, practice's
        // pre-existing PLAY elapsed, timer-origin reset, and later failure.
        long[][] states = {{5000000, Long.MIN_VALUE, 0}, {5000000, 5000000, 0},
            {35000000, 5000000, 0}, {65000000, 5000000, 0}, {124999000, 5000000, 0},
            {125000000, 5000000, 0}, {130000000, 5000000, 0},
            {1000000, -59000000, 0}, {65000000, 65000000, 0}, {90000000, 5000000, 1}};
        for (int i = 1; i <= cases.length(); i++) {
            LuaValue item = cases.get(i);
            JsonSkin.Skin document = loader.fromLuaValue(JsonSkin.Skin.class, item.get("skin"));
            sourceObjects += document.slider.length;
            Resolution sourceResolution = document.h == 720 ? Resolution.HD : Resolution.FULLHD;
            for (Resolution resolution : new Resolution[] {Resolution.HD, Resolution.FULLHD}) {
                SkinHeader header = new SkinHeader();
                header.setSourceResolution(sourceResolution);
                header.setDestinationResolution(resolution);
                Skin skin = new Skin(header);
                double scale = resolution.height / (double) document.h;
                for (int j = 0; j < document.slider.length; j++) {
                    JsonSkin.Slider spec = document.slider[j];
                    if (spec.type != 6 || spec.angle != 2 || spec.changeable || spec.value != null || spec.isRefNum)
                        throw new AssertionError("Not a read-only native music-progress Slider");
                    // JsonSkinObjectLoader's documented whole-destination-pixel
                    // range conversion, without its texture allocation/GL path.
                    int range = (int) (((float) resolution.height / document.h) * spec.range);
                    SkinSlider slider = new SkinSlider(new TextureRegion[] {new TextureRegion()},
                        spec.timer, spec.cycle, spec.angle, range, spec.type, spec.changeable);
                    // Default STRETCH only copies UVs; it does not alter the
                    // rectangle. Skip that texture-only copy for this GL-free
                    // destination probe, retaining the real Slider math.
                    slider.setStretch((StretchType) null);
                    if (writer.get(slider) != null) throw new AssertionError("Slider gained an input writer");
                    JsonSkin.Destination destination = document.destination[j];
                    if (!destination.id.equals(spec.id)) throw new AssertionError("Destination identity changed");
                    destinations.apply(skin, slider, destination);
                    LuaValue expected = item.get("expected").get(j + 1);
                    for (long[] state : states) {
                        now.setLong(timer, state[0]);
                        timer.setMicroTimer(41, state[1]);
                        timer.setMicroTimer(3, state[2] == 1 ? state[0] - 1000 : Long.MIN_VALUE);
                        float fraction = state[1] == Long.MIN_VALUE ? 0 : Math.min((float) ((state[0] - state[1]) / 1000) / 120000, 1);
                        float actual = FloatPropertyFactory.getRateProperty(6).get(player);
                        if (actual != fraction || FloatPropertyFactory.getRateProperty(101).get(player) != fraction)
                            throw new AssertionError("Official PLAY progress property changed");
                        propertyChecks += 2;
                        slider.prepare(state[0] / 1000, player);
                        if (!slider.draw) throw new AssertionError("Source cursor unexpectedly hidden");
                        // The official draw method computes its destination
                        // before the first renderer call. A null renderer stops
                        // exactly there; no fake renderer or GPU is involved.
                        try {
                            slider.draw(null);
                            throw new AssertionError("CPU draw did not reach its renderer boundary");
                        } catch (NullPointerException stopped) {
                            StackTraceElement first = stopped.getStackTrace()[0];
                            if (!first.getClassName().equals(SkinObject.class.getName()) || !first.getMethodName().equals("draw"))
                                throw stopped;
                        }
                        Rectangle rect = (Rectangle) drawRect.get(slider);
                        double x = expected.get(1).todouble(), y = expected.get(2).todouble() - fraction * expected.get(5).todouble();
                        double w = expected.get(3).todouble(), h = expected.get(4).todouble();
                        double originError = Math.max(Math.abs(rect.x / scale - x), Math.abs(rect.y / scale - y));
                        double edgeError = Math.max(originError, Math.max(Math.abs((rect.x + rect.width) / scale - x - w),
                            Math.abs((rect.y + rect.height) / scale - y - h)));
                        if (originError >= 1 + 1 / scale + .001 || edgeError >= 1.5 + 1 / scale + .001)
                            throw new AssertionError(item.get("name") + " cursor exceeds native integer-geometry bound");
                        maxOriginError = Math.max(maxOriginError, originError);
                        maxEdgeError = Math.max(maxEdgeError, edgeError);
                        checks++;
                    }
                }
                skin.dispose();
            }
        }
        System.out.println("{\"cases\":" + cases.length() + ",\"sourceObjects\":" + sourceObjects
            + ",\"geometryChecks\":" + checks + ",\"propertyChecks\":" + propertyChecks
            + ",\"maxSourceOriginErrorPx\":" + maxOriginError + ",\"maxSourceEdgeErrorPx\":" + maxEdgeError + "}");
    }
}
