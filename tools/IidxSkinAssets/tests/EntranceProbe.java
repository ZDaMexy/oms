package bms.player.beatoraja.skin.lua;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

import bms.player.beatoraja.MainState;
import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinObject;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.property.BooleanProperty;
import bms.player.beatoraja.skin.property.TimerProperty;
import com.badlogic.gdx.math.Rectangle;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.ZeroArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Official JSON parser and SkinObject geometry, with a read-only clock cache fixture. */
public final class EntranceProbe {
    private static final class Geometry extends SkinObject {
        public void draw(Skin.SkinObjectRenderer renderer) { throw new AssertionError("No GPU in this probe"); }
        public void dispose() { setDisposed(); }
    }
    private static final class Fixture {
        long now, origin = Long.MIN_VALUE;
        boolean allowed = true;
        LuaTable state() {
            LuaTable table = new LuaTable();
            table.set("timer_off_value", LuaValue.valueOf((double) Long.MIN_VALUE));
            table.set("time", new ZeroArgFunction() {
                public LuaValue call() { return LuaValue.valueOf((double) now); }
            });
            table.set("timer", new OneArgFunction() {
                public LuaValue call(LuaValue id) {
                    if (id.checkint() != 10020) throw new AssertionError("Unexpected timer: "+id);
                    return LuaValue.valueOf((double) origin);
                }
            });
            table.set("option", new OneArgFunction() {
                public LuaValue call(LuaValue id) {
                    if (id.checkint() != 9001) throw new AssertionError("Unexpected option: "+id);
                    return LuaValue.valueOf(allowed);
                }
            });
            table.set("number", new OneArgFunction() {
                public LuaValue call(LuaValue id) { return LuaValue.valueOf(100); }
            });
            table.set("event_index", new OneArgFunction() {
                public LuaValue call(LuaValue id) {
                    if (id.checkint() != 75) throw new AssertionError("Unexpected event index: "+id);
                    return LuaValue.valueOf(1);
                }
            });
            return table;
        }
    }
    private record Item(JsonSkin.Destination definition, Geometry object) {}
    private static int value(int candidate, int fallback) { return candidate==Integer.MIN_VALUE ? fallback : candidate; }
    private static void require(boolean success, String reason) { if (!success) throw new AssertionError(reason); }

    public static void main(String[] args) throws Exception {
        Path path = Path.of(args[0]).toAbsolutePath();
        Fixture fixture = new Fixture();
        Globals globals = JsePlatform.standardGlobals();
        globals.set("main_state", fixture.state());
        LuaValue recipe = globals.load(Files.readString(path)).call();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(path);
        int geometryChecks=0, clipChecks=0, visibilityChecks=0, opacityChecks=0;
        double maxError=0, maxSettled=0, maxClock=0;
        LuaValue cases = recipe.get("geometry");
        long[] elapsedSamples={0,1,999,1000,125500,250125,349999,350000,499001,499999,500000,999999,1000000,5000000};
        for (int index=1; index<=cases.length(); index++) {
            LuaValue test=cases.get(index);
            String name=test.get("name").checkjstring();
            JsonSkin.Skin document=loader.fromLuaValue(JsonSkin.Skin.class,test.get("skin"));
            JsonSkin.Destination[] baseline=loader.fromLuaValue(JsonSkin.Destination[].class,test.get("baseline"));
            Map<String,JsonSkin.Destination> original=new HashMap<>();
            for (JsonSkin.Destination destination:baseline) original.put(destination.id,destination);
            String axis=test.get("track").get("property").checkjstring();
            LuaValue keys=test.get("track").get("keyframes");
            double start=keys.get(1).get("value").todouble(),end=keys.get(2).get("value").todouble();
            double duration=keys.get(2).get("time").todouble();
            double baselineValue=test.get("baselineValue").todouble();
            double span=test.get("parent").get(axis.equals("x")?3:4).todouble()*(axis.equals("x")?1:-1);
            for (Resolution resolution:new Resolution[]{Resolution.HD,Resolution.FULLHD}) {
                SkinHeader header=new SkinHeader();
                header.setSourceResolution(Resolution.FULLHD); header.setDestinationResolution(resolution);
                Skin skin=new Skin(header);
                double scale=resolution.height/1080.0;
                List<Item> objects=new ArrayList<>();
                for (JsonSkin.Destination destination:document.destination) {
                    Geometry object=new Geometry();
                    TimerProperty clock=null;
                    if (destination.timer!=null) {
                        require(destination.timer.getTimerId()==10020,name+": native parser lost timer ID");
                        // This fixture supplies the public cached origin without constructing
                        // a player/controller or opening its databases. TimerProperty.get()
                        // retains the official microseconds-to-integer-ms truncation.
                        clock=new TimerProperty() { public long getMicro(MainState ignored) { return fixture.origin; } };
                    }
                    for (JsonSkin.Animation point:destination.dst) {
                        skin.setDestination(object,point.time,point.x,point.y,point.w,point.h,
                            value(point.acc,0),value(point.a,255),value(point.r,255),value(point.g,255),value(point.b,255),
                            destination.blend,0,0,0,destination.loop,clock,new int[0]);
                        if (point.clip_x!=Integer.MIN_VALUE)
                            skin.setDestinationClip(object,point.time,point.clip_x,point.clip_y,point.clip_w,point.clip_h);
                    }
                    if(destination.draw!=null) object.setDrawCondition(new BooleanProperty[]{destination.draw});
                    objects.add(new Item(destination,object));
                }
                for (boolean allowed:new boolean[]{true,false}) {
                    fixture.allowed=allowed;
                    for (long origin:new long[]{Long.MIN_VALUE,100000,100999}) {
                        fixture.origin=origin;
                        for(long elapsed:elapsedSamples) {
                            fixture.now=(origin==Long.MIN_VALUE?100000:origin)+elapsed;
                            Map<String,Integer> visible=new HashMap<>();
                            double time=origin==Long.MIN_VALUE?0:elapsed/1000.0;
                            double fraction=Math.max(0,Math.min(1,time/duration));
                            double authored=start+(end-start)*(1-(1-fraction)*(1-fraction));
                            double delta=origin==Long.MIN_VALUE?0:(authored-baselineValue)*span;
                            if(origin!=Long.MIN_VALUE)
                                maxClock=Math.max(maxClock,Math.abs(fixture.now/1000-origin/1000-time));
                            for(Item item:objects) {
                                JsonSkin.Destination before=original.get(item.definition.id);
                                item.object.prepare(fixture.now/1000,null);
                                if(!item.object.draw) continue;
                                visible.merge(item.definition.id,1,Integer::sum);
                                JsonSkin.Animation initial=before.dst[0];
                                Rectangle rect=item.object.getDestination(fixture.now/1000,null);
                                require(rect!=null,name+": visible object lost geometry");
                                double expectedX=initial.x+(axis.equals("x")?delta:0);
                                double expectedY=initial.y+(axis.equals("y")?delta:0);
                                double error=Math.max(Math.abs(rect.x/scale-expectedX),Math.abs(rect.y/scale-expectedY));
                                maxError=Math.max(maxError,error);
                                if(origin!=Long.MIN_VALUE && time>=duration) maxSettled=Math.max(maxSettled,error);
                                double bound=.501+Math.abs(span*(end-start))*2/duration;
                                require(error<bound+.001,name+": motion exceeds integer endpoint + <1ms sampling bound: "+error);
                                require(Math.abs(rect.width/scale-initial.w)<.001 && Math.abs(rect.height/scale-initial.h)<.001,
                                        name+": entrance resized original material");
                                require(Math.abs(item.object.getColor().r-value(initial.r,255)/255.0)<.00001
                                        && Math.abs(item.object.getColor().a-value(initial.a,255)/255.0)<.00001,
                                        name+": entrance changed source tint/opacity");
                                if(initial.clip_x!=Integer.MIN_VALUE) {
                                    require(item.object.hasClip(),name+": internal clip lost");
                                    Rectangle clip=item.object.getClip();
                                    require(Math.abs(clip.x/scale-initial.clip_x-(axis.equals("x")?delta:0))<bound+.001
                                            && Math.abs(clip.y/scale-initial.clip_y-(axis.equals("y")?delta:0))<bound+.001,
                                            name+": internal clip did not follow original parent");
                                    require(Math.abs(clip.width/scale-initial.clip_w)<.001 && Math.abs(clip.height/scale-initial.clip_h)<.001,
                                            name+": internal clip resized");
                                    clipChecks++;
                                }
                                geometryChecks++;
                            }
                            for(JsonSkin.Destination before:baseline) {
                                boolean expected=before.draw==null || before.draw.get(null);
                                require(visible.getOrDefault(before.id,0)==(expected?1:0),name+": duplicate/missing static or prepared branch");
                                visibilityChecks++;
                            }
                        }
                    }
                }
                for(Item object:objects) object.object.dispose();
                skin.dispose();
            }
        }
        LuaValue opacity=recipe.get("opacity");
        for(int index=1;index<=opacity.length();index++) {
            LuaValue test=opacity.get(index);
            JsonSkin.Skin document=loader.fromLuaValue(JsonSkin.Skin.class,test.get("skin"));
            for(long origin:new long[]{Long.MIN_VALUE,100000,100999}) {
                fixture.origin=origin;
                for(long elapsed:elapsedSamples) {
                    fixture.now=(origin==Long.MIN_VALUE?100000:origin)+elapsed;
                    boolean expected=test.get(origin==Long.MIN_VALUE?"baseline":elapsed<1000000?"first":"last").checkint()==1;
                    require(document.destination[0].draw.get(null)==expected,"Binary step changed OFF/default or threshold boundary");
                    opacityChecks++;
                }
            }
        }
        System.out.println("{\"cases\":"+cases.length()+",\"geometryChecks\":"+geometryChecks
            +",\"clipChecks\":"+clipChecks+",\"visibilityChecks\":"+visibilityChecks
            +",\"opacityChecks\":"+opacityChecks+",\"maxSourceMotionErrorPx\":"+maxError
            +",\"maxSettledEndpointErrorPx\":"+maxSettled+",\"maxClockErrorMs\":"+maxClock+"}");
    }
}
