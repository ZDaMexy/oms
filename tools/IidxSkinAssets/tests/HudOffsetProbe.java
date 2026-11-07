package bms.player.beatoraja.skin.lua;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Locale;
import java.util.Map;

import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.property.IntegerPropertyFactory;
import org.luaj.vm2.Globals;
import org.luaj.vm2.LuaTable;
import org.luaj.vm2.LuaValue;
import org.luaj.vm2.lib.OneArgFunction;
import org.luaj.vm2.lib.TwoArgFunction;
import org.luaj.vm2.lib.jse.JsePlatform;

/** Actual generated StringProperty/BooleanProperty callbacks; no player, DB or GPU. */
public final class HudOffsetProbe {
    private static int offset, automatic, offsetReads, autoReads;

    public static void main(String[] args) throws Exception {
        if (IntegerPropertyFactory.getIntegerProperty(12) == null
                || IntegerPropertyFactory.getImageIndexProperty(75) == null)
            throw new AssertionError("Official offset or AUTO property is unavailable");
        Path recipe = Path.of(args[0]).toAbsolutePath();
        Globals globals = JsePlatform.standardGlobals();
        LuaTable reads = new LuaTable();
        reads.set("number", new OneArgFunction() {
            public LuaValue call(LuaValue id) {
                if (id.checkint() != 12) throw new AssertionError("Offset read incorrect number: "+id);
                offsetReads++;
                return LuaValue.valueOf(offset);
            }
        });
        reads.set("event_index", new OneArgFunction() {
            public LuaValue call(LuaValue id) {
                if (id.checkint() != 75) throw new AssertionError("AUTO read incorrect image index: "+id);
                autoReads++;
                return LuaValue.valueOf(automatic);
            }
        });
        LuaTable proxy = new LuaTable(), meta = new LuaTable();
        meta.set("__index", reads);
        meta.set("__newindex", new TwoArgFunction() {
            public LuaValue call(LuaValue table, LuaValue key) {
                throw new AssertionError("Offset display attempted to write host state: "+key);
            }
        });
        proxy.setmetatable(meta); globals.set("main_state", proxy);
        LuaValue cases = globals.load(Files.readString(recipe)).call();
        LuaSkinLoader loader = LuaSkinLoader.sandboxed(recipe);
        int gateChecks = 0, signedChecks = 0, unitChecks = 0, artGateChecks = 0, artStateChecks = 0, artResourceChecks = 0;
        for (int i=1; i<=cases.length(); i++) {
            LuaValue item = cases.get(i), kinds = item.get("kinds");
            JsonSkin.Skin skin = loader.fromLuaValue(JsonSkin.Skin.class, item.get("skin"));
            Map<String, JsonSkin.Destination> destinations = new HashMap<>();
            for (JsonSkin.Destination destination : skin.destination) destinations.put(destination.id, destination);
            Map<String, JsonSkin.Image> images = new HashMap<>();
            for (JsonSkin.Image image : skin.image) images.put(image.id, image);
            Map<String, JsonSkin.ImageSet> sets = new HashMap<>();
            for (JsonSkin.ImageSet set : skin.imageset) sets.put(set.id, set);
            for (int value : new int[]{Integer.MIN_VALUE, -500, -1, 0, 1, 500, Integer.MAX_VALUE}) {
                offset = value;
                boolean available = value!=Integer.MIN_VALUE && value!=Integer.MAX_VALUE;
                for (int index : new int[]{Integer.MIN_VALUE, -1, 0, 1, 2, Integer.MAX_VALUE}) {
                    automatic = index;
                    for (JsonSkin.Text text : skin.text) {
                        String kind = kinds.get(text.id).checkjstring();
                        boolean expected = switch (kind) {
                            case "signed", "unit", "available" -> available;
                            case "automatic" -> index==1;
                            case "known" -> index==0 || index==1;
                            default -> throw new AssertionError("Unexpected probe kind "+kind);
                        };
                        JsonSkin.Destination destination = destinations.get(text.id);
                        boolean actual = destination.draw==null || destination.draw.get(null);
                        if (actual!=expected)
                            throw new AssertionError(item.get("name")+" "+kind+" invalid gate at "+value+" / "+index);
                        gateChecks++;
                        if (!actual) continue;
                        if (kind.equals("signed")) {
                            String formatted = text.value.get(null);
                            String wanted = String.format(Locale.ROOT, "%+.2f", (double)value);
                            if (!wanted.equals(formatted)) throw new AssertionError("Signed source formatting or sign changed: "+formatted);
                            signedChecks++;
                        } else if (kind.equals("unit")) {
                            if (!"ms".equals(text.constantText)) throw new AssertionError("Original source unit changed");
                            unitChecks++;
                        }
                    }
                    if (!item.get("artNodes").isnil()) {
                        LuaTable artNodes = item.get("artNodes").checktable();
                        LuaTable expectedArt = item.get("artExpected").get(value+":"+index).checktable();
                        Map<String, String> actualArt = new HashMap<>();
                        for (LuaValue id : artNodes.keys()) {
                            String identifier = id.checkjstring(), node = artNodes.get(id).checkjstring();
                            JsonSkin.Destination destination = destinations.get(identifier);
                            boolean visible = destination.draw==null || destination.draw.get(null);
                            if (visible != !expectedArt.get(node).isnil())
                                throw new AssertionError(item.get("name")+" art gate "+node+" at "+value+" / "+index);
                            artGateChecks++;
                            if (!visible) continue;
                            JsonSkin.ImageSet set = sets.get(identifier);
                            if (set != null) {
                                int selected = set.value.get(null);
                                if (selected<0 || selected>=set.images.length)
                                    throw new AssertionError("AUTO selector left original resource states");
                                identifier = set.images[selected];
                            }
                            String source = images.get(identifier).src;
                            if (!source.equals(expectedArt.get(node).checkjstring()))
                                throw new AssertionError(item.get("name")+" wrong original AUTO/background resource "+node);
                            if (actualArt.put(node, source)!=null)
                                throw new AssertionError("Source offset node was duplicated");
                            artResourceChecks++;
                        }
                        if (actualArt.size()!=expectedArt.keys().length)
                            throw new AssertionError("Original source offset art was omitted");
                        artStateChecks++;
                    }
                }
            }
        }
        System.out.println("{\"cases\":"+cases.length()+",\"gateChecks\":"+gateChecks
            +",\"signedChecks\":"+signedChecks+",\"unitChecks\":"+unitChecks
            +",\"offsetReads\":"+offsetReads+",\"autoIndexReads\":"+autoReads
            +",\"artGateChecks\":"+artGateChecks+",\"artStateChecks\":"+artStateChecks
            +",\"artResourceChecks\":"+artResourceChecks+"}");
    }
}
