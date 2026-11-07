// Compile against the explicitly supplied official beatoraja 0.8.9 JAR.
// CPU-only: allocate only the stateless scanner object without its MainState
// constructor. No MainController, player data, gameplay or graphics is created.
import bms.player.beatoraja.config.SkinConfiguration;
import bms.player.beatoraja.skin.SkinHeader;
import bms.player.beatoraja.skin.lua.LuaSkinLoader;
import com.badlogic.gdx.utils.JsonValue;
import com.badlogic.gdx.utils.JsonWriter;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.nio.file.*;
import java.util.*;

public final class SkinDiscoveryProbe {
    record ScanResult(List<Path> paths, long nanos) { }
    static ScanResult scan(Path root) throws Exception {
        Class<?> unsafeType = Class.forName("sun.misc.Unsafe");
        Field field = unsafeType.getDeclaredField("theUnsafe");
        field.setAccessible(true);
        Object allocator = field.get(null);
        Object scanner = unsafeType.getMethod("allocateInstance", Class.class).invoke(allocator, SkinConfiguration.class);
        Method method = SkinConfiguration.class.getDeclaredMethod("scanSkins", Path.class, List.class);
        method.setAccessible(true);
        List<Path> paths = new ArrayList<>();
        long started = System.nanoTime();
        method.invoke(scanner, root, paths);
        return new ScanResult(paths, System.nanoTime() - started);
    }

    public static void main(String[] args) throws Exception {
        if (args.length == 0) throw new IllegalArgumentException("Pass complete five-layout package directories");
        Map<String, Integer> expected = Map.of("play5.luaskin", 1, "play7.luaskin", 0, "play14.luaskin", 2);
        for (String argument : args) {
            Path root = Path.of(argument).toRealPath();
            if (!Files.isDirectory(root)) throw new IllegalArgumentException("Not a package directory: " + root);
            ScanResult scan = scan(root);
            Set<String> found = new TreeSet<>();
            for (Path path : scan.paths()) found.add(root.relativize(path).toString().replace('\\', '/'));
            if (scan.paths().size() != 3 || !found.equals(expected.keySet()))
                throw new IllegalStateException("Native skin discovery differs: " + root + " -> " + found);
            // loadAllSkins hardcodes cwd/skin. After its actual scanner above,
            // use the identical native Lua header branch at this package path.
            // This avoids scanning a user installation or copying/linking skins.
            long headerStarted = System.nanoTime();
            int headers = 0;
            for (Path path : scan.paths()) {
                SkinHeader header = LuaSkinLoader.sandboxed(path).loadHeader(path);
                if (header == null || header.getSkinType().getId() != expected.get(path.getFileName().toString()))
                    throw new IllegalStateException("Native header type differs: " + path);
                headers++;
            }
            long headerNanos = System.nanoTime() - headerStarted;
            JsonValue metrics = new JsonValue(JsonValue.ValueType.object);
            metrics.addChild("phase", new JsonValue("native-skin-discovery"));
            metrics.addChild("root", new JsonValue(root.toString()));
            metrics.addChild("discoveredEntries", new JsonValue(scan.paths().size()));
            metrics.addChild("nativeHeaders", new JsonValue(headers));
            metrics.addChild("scanSkinsMs", new JsonValue(scan.nanos() / 1e6));
            metrics.addChild("nativeHeaderLoadMs", new JsonValue(headerNanos / 1e6));
            metrics.addChild("equivalentLoadAllSkinsMs", new JsonValue((scan.nanos() + headerNanos) / 1e6));
            metrics.addChild("boundary", new JsonValue("Actual scanner plus identical sandboxed native Lua header branch; excludes scanner object setup, skin preview and settings-window interaction."));
            System.out.println(metrics.toJson(JsonWriter.OutputType.json));
            System.out.println("Native SkinConfiguration.scanSkins PASS: " + root + " -> " + found);
        }
        System.out.printf("PASS roots=%d nativeEntries=%d; CPU-only actual scanner, no controller/player/graphics%n", args.length, args.length * 3);
    }
}
