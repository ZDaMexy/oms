// Compile against the explicitly supplied official JAR. This never creates a
// MainController, opens graphics, or touches player files. The one chart-size
// value needed during graph construction is an explicit fixture, not a host read.
import java.lang.reflect.Method;
import java.nio.file.*;
import java.util.*;
import org.luaj.vm2.*;
import bms.player.beatoraja.SkinConfig;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.json.JsonSkin;
import bms.player.beatoraja.skin.lua.LuaSkinLoader;
import bms.player.beatoraja.skin.lua.SkinLuaAccessor;

public class BeatorajaPackageProbe {
    static int loads, headers, shards, timerMergeFixtures;
    static boolean checkedReuse;
    static boolean representative;
    static boolean smokeSelections;
    static final Set<String> arrays = Set.of("image", "imageset", "value", "floatvalue", "text",
        "slider", "graph", "gaugegraph", "judgegraph", "bpmgraph", "judge", "hiddenCover", "liftCover");
    static final Set<String> notes = Set.of("note", "lnstart", "lnend", "lnbody", "lnbodyActive",
        "hcnstart", "hcnend", "hcnbody", "hcnbodyActive", "hcnbodyReactive", "hcnbodyMiss", "mine");

    static void require(boolean condition, String reason) {
        if (!condition) throw new IllegalStateException(reason);
    }
    static void references(LuaValue value, Set<String> ids, Set<String> sources, Set<String> fonts, String key) {
        if (value.istable()) {
            for (LuaValue k : value.checktable().keys()) references(value.get(k), ids, sources, fonts, k.isint() ? key : k.tojstring());
        } else if (value.isstring() && !value.isnumber()) {
            String s = value.tojstring();
            if (s.isEmpty() && key.equals("id")) return; // Native Judge intentionally has no number for BAD/POOR.
            if (key.equals("id") || key.equals("images") || notes.contains(key)) require(ids.contains(s), "Dangling object: " + s);
            if (key.equals("src")) require(sources.contains(s), "Dangling source: " + s);
            if (key.equals("font")) require(fonts.contains(s), "Dangling font: " + s);
        }
    }
    static void validate(Path root, LuaValue skin, boolean full) throws Exception {
        Set<String> ids = new HashSet<>(), sources = new HashSet<>(), fonts = new HashSet<>();
        Set<Integer> customTimers = new HashSet<>();
        LuaValue timers = skin.get("customTimers");
        for (LuaValue i : timers.isnil() ? new LuaValue[0] : timers.checktable().keys()) {
            LuaValue timer = timers.get(i);
            int id = timer.get("id").checkint();
            require(id >= 10000 && id <= 19999 && customTimers.add(id), "Invalid/duplicate custom timer: " + id);
            require(timer.get("timer").isfunction(), "Custom timer is not an actual TimerProperty function");
        }
        if (full) require(skin.get("_customTimerSignatures").isnil(), "Internal timer identity escaped the package merge");
        for (String field : arrays) for (LuaValue i : skin.get(field).isnil() ? new LuaValue[0] : skin.get(field).checktable().keys()) {
            String id = skin.get(field).get(i).get("id").checkjstring();
            require(ids.add(id), "Duplicate object ID: " + id);
        }
        for (String field : List.of("note", "bga", "gauge")) if (!skin.get(field).isnil() && !skin.get(field).get("id").isnil())
            require(ids.add(skin.get(field).get("id").checkjstring()), "Duplicate singleton ID");
        for (String field : List.of("source", "font")) {
            LuaValue values = skin.get(field);
            for (LuaValue i : values.isnil() ? new LuaValue[0] : values.checktable().keys()) {
                LuaValue item = values.get(i);
                boolean fresh = (field.equals("source") ? sources : fonts).add(item.get("id").checkjstring());
                require(fresh || !full, "Duplicate " + field); // Merge deduplicates identical shared fallback fonts.
                Path file = root.resolve(item.get("path").checkjstring()).normalize();
                require(file.startsWith(root) && Files.isRegularFile(file), "Missing/outside asset: " + file);
            }
        }
        for (String field : arrays) if (!skin.get(field).isnil()) references(skin.get(field), ids, sources, fonts, field);
        references(skin.get("note"), ids, sources, fonts, "");
        references(skin.get("destination"), ids, sources, fonts, "");
        if (full) {
            int lanes = skin.get("type").toint() == 2 ? 16 : 8;
            for (String field : notes) require(skin.get("note").get(field).length() == lanes, "Incomplete NoteSet: " + field);
            require(skin.get("note").get("size").length() == lanes, "Incomplete lane size");
            require(skin.get("judge").length() > 0, "No native judgement");
        }
    }
    static LuaValue load(Path path, SkinHeader header, SkinConfig.Property property, Method deserialize) throws Exception {
        header.setSkinConfigProperty(property);
        SkinLuaAccessor accessor = new SkinLuaAccessor(false);
        accessor.setDirectory(path.getParent());
        accessor.exec("local state=require('main_state'); state.number=function(id) assert(id==106, 'Unexpected build-time number '..id); return 84 end");
        accessor.exportSkinProperty(header, property, p -> path.getParent().resolve(p).toString());
        LuaValue value = accessor.execFile(path);
        validate(path.getParent(), value, true);
        if (!checkedReuse) {
            LuaValue second = accessor.execFile(path);
            validate(path.getParent(), second, true);
            require(second.get("destination").length() == value.get("destination").length(), "require cache mutated destinations");
            require(second != value, "A cached mutable skin table escaped the factory");
            checkedReuse = true;
        }
        JsonSkin.Skin nativeSkin = (JsonSkin.Skin) deserialize.invoke(new LuaSkinLoader(), JsonSkin.Skin.class, value);
        require(nativeSkin.note != null && nativeSkin.destination.length > 0, "Native deserialization failed");
        loads++;
        if (loads % 100 == 0) System.out.println("Native assembled selections checked: " + loads);
        accessor.dispose();
        return value;
    }
    public static void main(String[] args) throws Exception {
        Method deserialize = LuaSkinLoader.class.getDeclaredMethod("fromLuaValue", Class.class, LuaValue.class);
        deserialize.setAccessible(true);
        for (String argument : args) {
            if (argument.equals("--representative")) { representative = true; continue; }
            if (argument.equals("--smoke")) { smokeSelections = true; continue; }
            if (argument.startsWith("--timer-merge-fixtures=")) {
                Path root = Path.of(argument.substring("--timer-merge-fixtures=".length())).toAbsolutePath().normalize();
                SkinLuaAccessor accessor = new SkinLuaAccessor(false);
                accessor.setDirectory(root);
                accessor.exec("skin_config={option={}}");
                LuaValue same = accessor.execFile(root.resolve("same.luaskin"));
                validate(root, same, false);
                require(same.get("customTimers").length() == 1, "Equivalent custom timers were not deduplicated");
                require(same.get("_customTimerSignatures").isnil(), "Internal custom timer identity escaped");
                deserialize.invoke(new LuaSkinLoader(), JsonSkin.Skin.class, same);
                boolean rejected = false;
                try { accessor.execFile(root.resolve("conflict.luaskin")); }
                catch (LuaError expected) { rejected = expected.getMessage().contains("Conflicting custom timer: 10000"); }
                require(rejected, "Different definitions of one custom timer silently merged");
                accessor.dispose();
                timerMergeFixtures += 2;
                continue;
            }
            Path path = Path.of(argument).toAbsolutePath().normalize();
            if (Files.isDirectory(path)) {
                SkinLuaAccessor accessor = new SkinLuaAccessor(false);
                accessor.setDirectory(path);
                accessor.exec("local state=require('main_state'); state.number=function(id) assert(id==106, 'Unexpected build-time number '..id); return 84 end");
                try (var files = Files.list(path.resolve("shards"))) {
                    for (Path shard : files.filter(p -> p.toString().endsWith(".lua")).sorted().toList()) {
                        LuaValue value = accessor.execFile(shard).call();
                        validate(path, value, false);
                        deserialize.invoke(new LuaSkinLoader(), JsonSkin.Skin.class, value);
                        shards++;
                        if (shards % 1000 == 0) System.out.println("Native fragments checked: " + shards);
                    }
                }
                accessor.dispose();
                System.out.println("Every fragment parsed with native deserializer: " + path);
                continue;
            }
            SkinHeader header = new LuaSkinLoader().loadHeader(path);
            require(header != null, "Header parse failed: " + path);
            headers++;
            SkinLuaAccessor onlyHeader = new SkinLuaAccessor(false);
            onlyHeader.setDirectory(path.getParent());
            onlyHeader.exec("skin_config={onlyload=true}");
            LuaValue unloaded = onlyHeader.execFile(path);
            require(unloaded.get("source").isnil() && unloaded.get("destination").isnil(), "onlyload loaded payload");
            onlyHeader.dispose();
            SkinLuaAccessor invalid = new SkinLuaAccessor(false);
            invalid.setDirectory(path.getParent());
            invalid.exec("skin_config={option={Frame=-12345}}");
            boolean rejected = false;
            try { invalid.execFile(path); } catch (LuaError expected) {
                rejected = expected.getMessage().contains("Unknown native option for Frame");
            }
            require(rejected, "An unknown selection silently fell back");
            invalid.dispose();
            load(path, header, new SkinConfig.Property(), deserialize);
            // Each choice once, with all other choices at explicit header defaults.
            // This is linear coverage, not a hidden Cartesian matrix.
            Set<String> allControlValues = Set.of("Frame", "Play Side", "Note Size", "Charge Size", "HCN Colour", "HCN Display",
                "Judgement Display", "Judgement Position", "Beam Length", "Ready Animation", "Get Ready", "Timing Display", "Timing Position", "Lift Readout");
            for (SkinHeader.CustomOption option : header.getCustomOptions()) for (int selected : option.option) {
                if (smokeSelections && !Set.of("Frame", "Play Side").contains(option.name)) continue;
                if (representative && !allControlValues.contains(option.name) && selected != option.option[0]
                    && selected != option.option[option.option.length - 1]) continue;
                SkinConfig.Option value = new SkinConfig.Option(); value.name = option.name; value.value = selected;
                SkinConfig.Property property = new SkinConfig.Property(); property.setOption(new SkinConfig.Option[]{value});
                load(path, header, property, deserialize);
            }
            List<SkinConfig.Option> mixed = new ArrayList<>();
            for (SkinHeader.CustomOption option : header.getCustomOptions()) {
                SkinConfig.Option value = new SkinConfig.Option(); value.name = option.name;
                value.value = option.option[option.option.length - 1]; mixed.add(value);
            }
            SkinConfig.Property mixture = new SkinConfig.Property(); mixture.setOption(mixed.toArray(SkinConfig.Option[]::new));
            load(path, header, mixture, deserialize);
            System.out.println("Native header + independent selections PASS: " + path);
        }
        System.out.printf("PASS headers=%d nativeLoads=%d shards=%d representativeModuleChoices=%s smokeSelections=%s timerMergeFixtures=%d; static only, chart-size fixture=84, no rendering%n",
            headers, loads, shards, representative, smokeSelections, timerMergeFixtures);
    }
}
