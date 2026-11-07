package bms.player.beatoraja.skin.lua;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import bms.model.BMSModel;
import bms.model.TimeLine;
import bms.player.beatoraja.MainState;
import bms.player.beatoraja.TimerManager;
import bms.player.beatoraja.play.BMSPlayer;
import bms.player.beatoraja.play.RhythmTimerProcessor;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.json.JsonSkin;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;
import sun.misc.Unsafe;

/** Official 0.8.9 CPU clock and colour path, without MainController or GPU.
 * Unsafe only allocates an inert BMSPlayer fixture without its game/database
 * constructor. Its real TimerManager and real RhythmTimerProcessor execute.
 * Reflection supplies deterministic fixture time/speed, never user game state.
 */
public final class BeatTimerProbe {
    private static final class Geometry extends SkinObject {
        public void draw(Skin.SkinObjectRenderer renderer) {
            throw new AssertionError("This probe cannot draw or start a game");
        }
        public void dispose() { setDisposed(); }
    }

    private static final class Fixture {
        final BMSPlayer player;
        final TimerManager timer = new TimerManager();
        final Field nowField = TimerManager.class.getDeclaredField("nowmicrotime");
        final Field speedField = BMSPlayer.class.getDeclaredField("playspeed");

        Fixture() throws Exception {
            Field field = Unsafe.class.getDeclaredField("theUnsafe");
            field.setAccessible(true);
            player = (BMSPlayer) ((Unsafe) field.get(null)).allocateInstance(BMSPlayer.class);
            Field timerField = MainState.class.getField("timer");
            timerField.setAccessible(true);
            timerField.set(player, timer);
            nowField.setAccessible(true);
            speedField.setAccessible(true);
            timer.setMainState(player);
            now(0); speed(100);
        }

        void now(long value) throws Exception { nowField.setLong(timer, value); }
        void speed(int value) throws Exception { speedField.setInt(player, value); }
        LuaTable state() {
            LuaTable exported = new LuaTable();
            new MainStatePropertyLuaApiExporter(player).export(exported);
            LuaTable reads = new LuaTable();
            for (String key : new String[] {"time", "timer", "timer_off_value"})
                reads.set(key, exported.get(key));
            LuaTable proxy = new LuaTable(), meta = new LuaTable();
            meta.set("__index", reads);
            meta.set("__newindex", new TwoArgFunction() {
                public LuaValue call(LuaValue key, LuaValue value) {
                    throw new AssertionError("Beat adapter tried to write public state: " + key);
                }
            });
            proxy.setmetatable(meta);
            return proxy;
        }
    }

    private static TimeLine line(double section, long micros, boolean measure, long stop) {
        TimeLine line = new TimeLine(section, micros, 8);
        line.setBPM(120); line.setSectionLine(measure); line.setStop(stop);
        return line;
    }

    private static RhythmTimerProcessor processor(Fixture f, TimeLine... lines) {
        BMSModel model = new BMSModel();
        model.setBpm(120); model.setAllTimeLine(lines);
        f.timer.setMicroTimer(40, 0);
        f.timer.setMicroTimer(41, 0);
        f.timer.setMicroTimer(140, 0);
        return new RhythmTimerProcessor(model, false);
    }

    private static void equal(long expected, long actual, String label) {
        if (expected != actual)
            throw new AssertionError(label + ": expected " + expected + ", got " + actual);
    }

    private static int processorChecks() throws Exception {
        Fixture f = new Fixture();
        RhythmTimerProcessor rhythm = processor(f, line(0, 0, true, 0),
            line(.25, 500000, false, 300000), line(10, 100000000, true, 0));
        rhythm.update(f.player, 0, 120, 100);
        long[] times = {250000, 500000, 600000, 700000, 800000, 900000, 1000000};
        int[] speeds = {100, 100, 100, 50, 100, 0, 100};
        double[] bpms = {120, 120, 120, 120, 240, 240, 120};
        long[] elapsed = {500000, 1000000, 1200000, 1300000, 1700000, 1700000, 1900000};
        long previous = 0, playElapsed = 0;
        for (int i=0; i<times.length; i++) {
            long delta = times[i]-previous;
            playElapsed += delta*speeds[i]/100;
            f.now(times[i]); f.speed(speeds[i]);
            f.timer.setMicroTimer(41, times[i]-playElapsed);
            rhythm.update(f.player, delta, bpms[i], 100);
            equal(elapsed[i], f.timer.getNowMicroTime(140), "official BPM/speed/STOP sample " + i);
            previous = times[i];
        }
        // The original STOP is 500000..800000 us. 140 advances inside it.
        // A 1.5-beat measure resets host phase even though OMS beat ends at .5.
        Fixture shortMeasure = new Fixture();
        rhythm = processor(shortMeasure, line(0, 0, true, 0), line(.375, 750000, true, 0));
        rhythm.update(shortMeasure.player, 0, 120, 100);
        shortMeasure.now(740000); rhythm.update(shortMeasure.player, 740000, 120, 100);
        equal(1480000, shortMeasure.timer.getNowMicroTime(140), "short measure before line");
        shortMeasure.now(750000); rhythm.update(shortMeasure.player, 10000, 120, 100);
        equal(0, shortMeasure.timer.getNowMicroTime(140), "short measure resets host phase");
        shortMeasure.now(760000); rhythm.update(shortMeasure.player, 10000, 120, 100);
        equal(20000, shortMeasure.timer.getNowMicroTime(140), "after section reset");

        // Official practice frequency uses integer 100/freq, including 0 for
        // 200. Observe it, rather than silently repair the host's clock.
        Fixture fastPractice = new Fixture();
        rhythm = processor(fastPractice, line(0, 0, true, 0), line(1, 2000000, true, 0));
        rhythm.update(fastPractice.player, 0, 120, 200);
        fastPractice.now(1000); rhythm.update(fastPractice.player, 1000, 120, 200);
        equal(0, fastPractice.timer.getNowMicroTime(140), "official practice frequency section catch-up");

        // A fresh actual processor/player is the normal restart boundary.
        Fixture restart = new Fixture();
        rhythm = processor(restart, line(0, 0, true, 0));
        rhythm.update(restart.player, 0, 120, 100);
        equal(0, restart.timer.getNowMicroTime(140), "fresh restart phase");
        return elapsed.length + 5;
    }

    public static void main(String[] args) throws Exception {
        int nativeChecks = processorChecks();
        Path path = Path.of(args[0]).toAbsolutePath();
        Fixture f = new Fixture();
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", f.state());
        LuaTable cases = globals.load(Files.readString(path)).call().checktable();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(path);
        int clockChecks=0, colourChecks=0;
        double maxAlphaByteError=0;
        for (int i=1; i<=cases.length(); i++) {
            LuaValue source = cases.get(i);
            f.timer.setMainState(f.player); f.now(0);
            JsonSkin.Destination destination = loader.fromLuaValue(JsonSkin.Destination.class, source.get("destination"));
            Geometry object = new Geometry();
            for (JsonSkin.Animation point : destination.dst)
                object.setDestination(point.time, point.x, point.y, point.w, point.h, point.acc,
                    point.a, point.r, point.g, point.b, 1, 0, 0, 0,
                    destination.loop, destination.timer, new int[0]);
            LuaValue steps = source.get("steps");
            for (int j=1; j<=steps.length(); j++) {
                LuaValue step = steps.get(j);
                long now = step.get("now").tolong(); f.now(now);
                LuaTable timers = step.get("timers").checktable();
                for (LuaValue key : timers.keys()) f.timer.setMicroTimer(key.toint(), timers.get(key).tolong());
                String where = source.get("name").tojstring() + "/" + j;
                long[] before = new long[TimerManager.timerCount];
                for (int id=0; id<before.length; id++) before[id]=f.timer.getMicroTimer(id);
                for (int read=0; read<3; read++) {
                    long actual = now/1000-destination.timer.get(null);
                    equal(step.get("elapsedMs").tolong(), actual, where + "/elapsed");
                    clockChecks++;
                }
                object.prepare(now/1000, null);
                if (!object.draw) throw new AssertionError(where + ": original baseplate disappeared");
                if (Math.abs(object.region.x-20)>.00001 || Math.abs(object.region.y-30)>.00001
                        || Math.abs(object.region.width-200)>.00001 || Math.abs(object.region.height-10)>.00001)
                    throw new AssertionError(where + ": geometry changed");
                if (Math.abs(object.getColor().r-96/255.0)>.00001 || Math.abs(object.getColor().g-128/255.0)>.00001
                        || Math.abs(object.getColor().b-200/255.0)>.00001)
                    throw new AssertionError(where + ": RGB changed");
                double error = Math.abs(object.getColor().a-step.get("idealAlpha").todouble())*255;
                maxAlphaByteError = Math.max(maxAlphaByteError,error);
                if (error>.81) throw new AssertionError(where + ": excessive alpha/time quantization " + error);
                for (int id=0; id<before.length; id++)
                    equal(before[id], f.timer.getMicroTimer(id), where + "/public timer unchanged " + id);
                colourChecks++;
            }
            object.dispose();
        }
        System.out.println("{\"scenarios\":"+cases.length()+",\"rhythmChecks\":"+nativeChecks
            +",\"clockChecks\":"+clockChecks+",\"colourChecks\":"+colourChecks
            +",\"maxAlphaByteError\":"+maxAlphaByteError+"}");
    }
}
