package bms.player.beatoraja.skin.lua;

import java.awt.image.BufferedImage;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import javax.imageio.ImageIO;

import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.property.BooleanProperty;
import com.badlogic.gdx.math.Rectangle;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.ZeroArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Official CPU geometry/clock path; no textures, applications or GPU drawing. */
public final class GaugeClipProbe {
    private static final class Geometry extends SkinObject {
        public void draw(Skin.SkinObjectRenderer renderer) {
            throw new AssertionError("GPU drawing is prohibited in this probe");
        }
        public void dispose() { setDisposed(); }
    }

    private static final class Fixture {
        long time;
        float gauge;
        int kind;
        LuaTable reads() {
            LuaTable reads = new LuaTable();
            reads.set("time", new ZeroArgFunction() {
                public LuaValue call() { return LuaValue.valueOf((double) time); }
            });
            reads.set("gauge", new ZeroArgFunction() {
                public LuaValue call() { return LuaValue.valueOf((double) gauge); }
            });
            reads.set("gauge_type", new ZeroArgFunction() {
                public LuaValue call() { return LuaValue.valueOf(kind); }
            });
            return reads;
        }
    }

    private static double edgeError(Rectangle actual, LuaValue expected, double scale) {
        return Math.max(Math.max(Math.abs(actual.x/scale-expected.get(1).todouble()),
                                Math.abs(actual.y/scale-expected.get(2).todouble())),
                        Math.max(Math.abs((actual.x+actual.width)/scale-expected.get(1).todouble()-expected.get(3).todouble()),
                                 Math.abs((actual.y+actual.height)/scale-expected.get(2).todouble()-expected.get(4).todouble())));
    }

    public static void main(String[] args) throws Exception {
        Path recipePath = Path.of(args[0]).toAbsolutePath();
        Fixture fixture = new Fixture();
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", fixture.reads());
        LuaTable cases = globals.load(Files.readString(recipePath)).call().checktable();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(recipePath);
        int geometryChecks=0, emptyClipGuards=0, pulseChecks=0, maxAlphaByteError=0;
        double maxClockError=0, maxEdgeError=0;
        boolean positiveOrigin=false, negativeOrigin=false, negativeImageWidth=false;
        long[] times = {0, 1, 999, 1000, 1999, 12345, 999999, 500000001, 2000000000L, 2000000999L};
        for (int index=1; index<=cases.length(); index++) {
            LuaValue source = cases.get(index);
            String name = source.get("name").checkjstring();
            JsonSkin.Skin document = loader.fromLuaValue(JsonSkin.Skin.class, source.get("skin"));
            for (Resolution resolution : new Resolution[] {Resolution.HD, Resolution.FULLHD}) {
                SkinHeader header = new SkinHeader();
                header.setSourceResolution(Resolution.FULLHD); header.setDestinationResolution(resolution);
                Skin skin = new Skin(header);
                double scale = resolution.height/1080.0;
                for (JsonSkin.Destination destination : document.destination) {
                    Geometry object = new Geometry();
                    for (JsonSkin.Animation point : destination.dst) {
                        skin.setDestination(object, point.time, point.x, point.y, point.w, point.h,
                            0, point.a, point.r, point.g, point.b, 1, 0, 0, 0,
                            destination.loop, destination.timer, new int[0]);
                        skin.setDestinationClip(object, point.time, point.clip_x, point.clip_y, point.clip_w, point.clip_h);
                    }
                    object.setDrawCondition(new BooleanProperty[] {destination.draw});
                    fixture.kind = Integer.parseInt(destination.id.substring(destination.id.lastIndexOf('.')+1));
                    LuaValue samples = source.get("samples");
                    for (int sampleIndex=1; sampleIndex<=samples.length(); sampleIndex++) {
                        LuaValue sample = samples.get(sampleIndex);
                        fixture.gauge = sample.get("gauge").tofloat();
                        for (long now : times) {
                            fixture.time = now;
                            long origin = destination.timer.getMicro(null);
                            positiveOrigin |= origin > 0; negativeOrigin |= origin < 0;
                            long elapsed = now/1000-destination.timer.get(null);
                            double desired = Math.max(0, Math.min(1, fixture.gauge/100.0))*1000000;
                            maxClockError = Math.max(maxClockError, Math.abs(elapsed-desired));
                            String location = name+" "+resolution+" Gauge="+fixture.gauge+" us="+now;
                            object.prepare(now/1000, null);
                            if (fixture.gauge==0 && object.draw)
                                throw new AssertionError(location+": zero Gauge must not draw");
                            if (!object.draw) {
                                if (sample.get("clip").get(3).todouble()>Math.abs(source.get("span").todouble())*.0000021)
                                    throw new AssertionError(location+": non-empty shade disappeared");
                                emptyClipGuards++;
                                continue;
                            }
                            if (!object.hasClip())
                                throw new AssertionError(location+": empty native clip would draw the entire shade");
                            Rectangle region = object.getDestination(now/1000, null);
                            if (region == null) throw new AssertionError(location+": visible destination lost");
                            negativeImageWidth |= region.width < 0;
                            double imageError=edgeError(region, sample.get("rect"), scale);
                            double clipError=edgeError(object.getClip(), sample.get("clip"), scale);
                            maxEdgeError=Math.max(maxEdgeError,Math.max(imageError,clipError));
                            if (imageError>1.002 || clipError>1.002)
                                throw new AssertionError(location+": source geometry drift image="+imageError+" clip="+clipError);
                            if (Math.abs(object.getColor().r-64/255.0)>.000001 || Math.abs(object.getColor().a-1)>.000001)
                                throw new AssertionError(location+": original Gauge tint changed");
                            geometryChecks++;
                        }
                    }
                    object.dispose();
                }
                skin.dispose();
            }
            if (source.get("pulse").checkboolean()) {
                Map<String, BufferedImage> atlases = new HashMap<>();
                Map<String, JsonSkin.Image> images = new HashMap<>();
                for (JsonSkin.Source resource : document.source)
                    atlases.put(resource.id, ImageIO.read(recipePath.getParent().resolve(resource.path).toFile()));
                for (JsonSkin.Image image : document.image) images.put(image.id,image);
                for (JsonSkin.ImageSet palette : document.imageset) {
                    for (long now=0; now<=800000; now+=997) {
                        fixture.time=now;
                        // Pulse selection is independent of Gauge position and kind.
                        fixture.gauge = now%2 == 0 ? .01f : 100;
                        fixture.kind=(int)(now%9);
                        int selected=palette.value.get(null);
                        if (selected<0 || selected>=palette.images.length)
                            throw new AssertionError(name+": alpha palette index escaped source range");
                        JsonSkin.Image image=images.get(palette.images[selected]);
                        BufferedImage atlas=atlases.get(image.src);
                        double phase=(now/1000.0)%400;
                        double alpha=phase<200 ? .2+phase*.004 : 1-(phase-200)*.004;
                        for (int pixel=0;pixel<2;pixel++) {
                            int value=atlas.getRGB(image.x+pixel,image.y);
                            int expectedRGB=pixel==0 ? 0x141e28 : 0x323c46;
                            if ((value&0xffffff)!=expectedRGB)
                                throw new AssertionError(name+": pulse modified source RGB");
                            int error=Math.abs((value>>>24)-(int)Math.round((pixel==0 ? 128 : 255)*alpha));
                            maxAlphaByteError=Math.max(maxAlphaByteError,error);
                            if (error>1) throw new AssertionError(name+": alpha error exceeds one byte");
                            pulseChecks++;
                        }
                    }
                }
            }
        }
        if (!positiveOrigin || !negativeOrigin || !negativeImageWidth)
            throw new AssertionError("Probe did not exercise both timer signs and signed image width");
        System.out.println("{\"cases\":"+cases.length()+",\"geometryChecks\":"+geometryChecks
            +",\"emptyClipGuards\":"+emptyClipGuards+",\"pulseChecks\":"+pulseChecks
            +",\"maxClockErrorMs\":"+maxClockError+",\"maxSourceEdgeErrorPx\":"+maxEdgeError
            +",\"maxAlphaByteError\":"+maxAlphaByteError+"}");
    }
}
