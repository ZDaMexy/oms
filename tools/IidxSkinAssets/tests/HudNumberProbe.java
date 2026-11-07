package bms.player.beatoraja.skin.lua;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import bms.player.beatoraja.Resolution;
import bms.player.beatoraja.skin.Skin;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.SkinNumber;
import bms.player.beatoraja.skin.json.JsonSkin;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.math.Rectangle;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Official native numeric preparation with textureless glyph identities. No GPU. */
public final class HudNumberProbe {
    private static void near(double expected, double actual, double tolerance, String message) {
        if (Math.abs(expected-actual)>tolerance)
            throw new AssertionError(message+": expected "+expected+", got "+actual);
    }

    public static void main(String[] args) throws Exception {
        Path recipePath=Path.of(args[0]).toAbsolutePath();
        Globals globals=JsePlatform.standardGlobals();
        globals.load("N=0; main_state={number=function(id) if id~=91 then error('Unexpected numeric source') end; return N end}").call();
        LuaValue cases=globals.load(Files.readString(recipePath)).call();
        LuaSkinLoader loader=LuaSkinLoader.sandboxed(recipePath);
        Field current=SkinNumber.class.getDeclaredField("currentImages"); current.setAccessible(true);
        Field shift=SkinNumber.class.getDeclaredField("shift"); shift.setAccessible(true);
        int glyphChecks=0, sentinelChecks=0, layoutChecks=0;
        double maximumSourceEdgeError=0;
        int[] numbers={0,1,9,10,99,100,999,1000,9999,100000000,2147483646};
        for(int index=1;index<=cases.length();index++) {
            LuaValue source=cases.get(index);
            JsonSkin.Skin document=loader.fromLuaValue(JsonSkin.Skin.class,source.get("skin"));
            JsonSkin.Value value=document.value[0];
            JsonSkin.Destination destination=document.destination[0];
            for(Resolution resolution:new Resolution[]{Resolution.HD,Resolution.FULLHD}) {
                SkinHeader header=new SkinHeader();
                header.setSourceResolution(Resolution.FULLHD);header.setDestinationResolution(resolution);
                Skin skin=new Skin(header);
                TextureRegion[][] cells=new TextureRegion[1][10];
                for(int digit=0;digit<10;digit++)cells[0][digit]=new TextureRegion();
                SkinNumber number=new SkinNumber(cells,value.timer,0,value.digit,value.padding,value.space,value.value,value.align);
                for(JsonSkin.Animation point:destination.dst) {
                    skin.setDestination(number,point.time,point.x,point.y,point.w,point.h,0,255,255,255,255,1,0,0,0,0,destination.timer,new int[0]);
                }
                double scale=resolution.height/1080.0;
                for(int nativeValue:numbers) {
                    globals.set("N",LuaValue.valueOf(nativeValue));
                    number.prepare(0,null);
                    if(!number.draw)throw new AssertionError("Valid nonnegative BPM disappeared");
                    Rectangle region=number.getDestination(0,null);
                    TextureRegion[] active=(TextureRegion[])current.get(number);
                    String text=Integer.toString(nativeValue);
                    double actualStep=number.getLength()/text.length();
                    near((destination.dst[0].w+value.space)*scale,actualStep,.0002,"Native spaceScale must follow source resolution");
                    double start=Double.NaN,end=Double.NaN;
                    int character=0;
                    for(int digit=0;digit<active.length;digit++) {
                        if(active[digit]==null)continue;
                        if(active[digit]!=cells[0][text.charAt(character)-'0'])throw new AssertionError("Wrong original digit identity");
                        double x=region.x+actualStep*digit-shift.getFloat(number);
                        if(character==0)start=x;
                        end=x+region.width;
                        near(start+actualStep*character,x,.0003,"Native glyph advance changed");
                        character++;glyphChecks++;
                    }
                    if(character!=text.length())throw new AssertionError("Number was padded or truncated");
                    double nativeSpan=((text.length()-1)*(destination.dst[0].w+value.space)+destination.dst[0].w);
                    double sourceSpan=(text.length()-1)*source.get("sourceAdvance").todouble()+source.get("sourceCell").todouble();
                    double x=source.get("rect").get(1).todouble(),width=source.get("rect").get(3).todouble();
                    String align=source.get("alignment").checkjstring();
                    double weight=align.equals("left")?0:align.equals("centre")?.5:1;
                    double expected=x+(width-nativeSpan)*weight;
                    near(expected,start/scale,.501,"Left/centre/right must include final image overhang");
                    near(nativeSpan,(end-start)/scale,.0005,"Full image span differs from native advance count");
                    double sourceStart=x+(width-sourceSpan)*weight;
                    double error=Math.max(Math.abs(start/scale-sourceStart),Math.abs(end/scale-sourceStart-sourceSpan));
                    maximumSourceEdgeError=Math.max(maximumSourceEdgeError,error);
                    if(error>source.get("spanBound").todouble()+.501)
                        throw new AssertionError("Original layout drift exceeds declared integer quantization bound");
                    layoutChecks++;
                }
                for(int sentinel:new int[]{Integer.MIN_VALUE,Integer.MAX_VALUE}) {
                    globals.set("N",LuaValue.valueOf(sentinel));number.prepare(0,null);
                    if(number.draw || number.getLength()!=0)throw new AssertionError("Missing native BPM sentinel was displayed");
                    sentinelChecks++;
                }
                // The TextureRegion identities own no Texture. Do not call
                // SkinNumber.dispose(), whose source set would dispose textures.
                skin.dispose();
            }
        }
        System.out.println("{\"cases\":"+cases.length()+",\"glyphChecks\":"+glyphChecks
            +",\"layoutChecks\":"+layoutChecks+",\"sentinelChecks\":"+sentinelChecks
            +",\"maxSourceEdgeErrorPx\":"+maximumSourceEdgeError+"}");
    }
}
