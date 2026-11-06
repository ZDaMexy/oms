import bms.model.*;
import bms.player.beatoraja.*;
import bms.player.beatoraja.input.KeyInputLog;
import bms.player.beatoraja.play.*;
import bms.player.beatoraja.result.MusicResult;
import bms.player.beatoraja.select.MusicSelector;
import bms.player.beatoraja.skin.*;
import bms.player.beatoraja.song.SQLiteSongDatabaseAccessor;
import com.badlogic.gdx.ApplicationListener;
import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.backends.lwjgl.*;
import com.badlogic.gdx.utils.ScreenUtils;

import javax.imageio.ImageIO;
import java.awt.Color;
import java.awt.Graphics2D;
import java.awt.image.BufferedImage;
import java.io.*;
import java.net.*;
import java.nio.*;
import java.nio.charset.StandardCharsets;
import java.nio.file.*;
import java.security.MessageDigest;
import java.time.Instant;
import java.util.*;
import java.util.concurrent.*;
import java.util.logging.*;
import java.util.concurrent.atomic.AtomicInteger;

/** Runs the official engine with disposable fixtures; never fabricates a score or skin event. */
public final class Capture implements ApplicationListener {
    private final Path root = Path.of("").toAbsolutePath().normalize();
    private final Map<String, List<String>> args;
    private final Map<String, Object> receipt = new LinkedHashMap<>();
    private final List<Object> frames = new ArrayList<>(), transitions = new ArrayList<>(), warnings = new ArrayList<>();
    private final Map<String, Object> seenTimers = new LinkedHashMap<>();
    private final Map<String, Integer> requestedOptions = new LinkedHashMap<>();
    private final Map<String, Float> requestedReading = new LinkedHashMap<>();
    private final Map<Integer, Long> firstEffectTimers = new LinkedHashMap<>();
    private final List<Long> playRenderNs = new ArrayList<>(), afterFcRenderNs = new ArrayList<>();
    private final List<Long> pngWriteNs = Collections.synchronizedList(new ArrayList<>());
    private final List<Long> captureNs = Collections.synchronizedList(new ArrayList<>());
    private final List<Long> readbackNs = new ArrayList<>();
    private final Queue<String> pngFailures = new ConcurrentLinkedQueue<>();
    private final ThreadPoolExecutor pngWriter = new ThreadPoolExecutor(1, 1, 0, TimeUnit.MILLISECONDS,
            new ArrayBlockingQueue<>(4), task -> new Thread(task, "capture-png-writer"),
            new ThreadPoolExecutor.CallerRunsPolicy());
    private boolean pngWritesFinished;
    private final Set<String> captured = new HashSet<>();
    private final long started = System.nanoTime();
    private MainController main;
    private Path skin, chart;
    private String scenario, mode, lastState = "";
    private boolean created, disposed, exiting, outcome, identityVerified;
    private String error;
    private int expectedNotes, maxPastNotes, maxCombo, maxExscore, minimumGauge = 100;
    private int[] lastJudges = new int[6];
    private long lastPeriodic = -1;
    private long completedAt = -1;
    private final AtomicInteger blockedNetworkRequests = new AtomicInteger();
    private static final int[] EFFECT_TIMERS = {51, 71, 101};
    private static final int[] EFFECT_OFFSETS_MS = {0, 50, 100, 250};
    private static final int[] TIMERS = { 2, 3, 40, 41, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57,
        60, 61, 62, 63, 64, 65, 66, 67, 70, 71, 72, 73, 74, 75, 76, 77, 80, 81, 82, 83, 84, 85, 86, 87,
        100, 101, 102, 103, 104, 105, 106, 107, 110, 111, 112, 113, 114, 115, 116, 117,
        143, 144, 250, 251, 252, 253, 254, 255, 256, 257, 260, 261, 262, 263, 264, 265, 266, 267,
        270, 271, 272, 273, 274, 275, 276, 277, 280, 281, 282, 283, 284, 285, 286, 287, 348, 349, 350, 351, 352, 908 };

    private Capture(String[] argv) throws IOException {
        args = parse(argv);
        Path contractRoot = Path.of(one("work")).toRealPath();
        if (!root.toRealPath().equals(contractRoot) || !Files.isRegularFile(root.resolve("capture-workspace.json")))
            throw new IllegalArgumentException("Use the launcher with a newly created isolated working directory.");
        skin = Path.of(one("skin")).toRealPath();
        if (!(skin.toString().endsWith(".luaskin") || skin.toString().endsWith(".json")))
            throw new IllegalArgumentException("Expected .luaskin or .json");
        scenario = one("scenario"); mode = one("mode");
        if (!Set.of("autoplay", "success", "failure").contains(scenario) || !Set.of("sp1", "sp2", "dp").contains(mode))
            throw new IllegalArgumentException("Invalid scenario or mode");
        receipt.put("schema", 1); receipt.put("startedUtc", Instant.now().toString());
        receipt.put("scenario", scenario); receipt.put("mode", mode); receipt.put("skinRequested", skin.toString());
        receipt.put("javaVersion", System.getProperty("java.version"));
        receipt.put("engineVersion", MainController.getVersion());
        receipt.put("workingDirectory", root.toString());
        receipt.put("engineJar", MainController.class.getProtectionDomain().getCodeSource().getLocation().toString());
        receipt.put("evidenceBoundary", "Official gameplay and framebuffer with generated chart/replay; not human play, hardware latency, or visual acceptance.");
        receipt.put("telemetryBoundary", "State is sampled after render; the native judge thread runs independently, so telemetry is not an atomic snapshot of every pixel.");
        // Selection starts an unconditional BMS Search request even with empty table URLs.
        URL.setURLStreamHandlerFactory(protocol -> protocol.equals("http") || protocol.equals("https") ? new URLStreamHandler() {
            protected URLConnection openConnection(URL url) throws IOException {
                blockedNetworkRequests.incrementAndGet(); throw new IOException("Network disabled by skin capture host");
            }
        } : null);
        configureLog();
    }

    public static void main(String[] argv) {
        Capture capture = null;
        try {
            capture = new Capture(argv);
            capture.launch();
        } catch (Throwable e) {
            e.printStackTrace();
            if (capture != null) { capture.error = e.toString(); capture.writeReceipt(); }
            System.exit(2);
        }
    }

    private void configureLog() throws IOException {
        Logger logger = Logger.getGlobal();
        logger.setUseParentHandlers(false);
        FileHandler file = new FileHandler(root.resolve("engine.log").toString());
        file.setFormatter(new SimpleFormatter()); logger.addHandler(file);
        ConsoleHandler console = new ConsoleHandler(); console.setFormatter(new SimpleFormatter()); logger.addHandler(console);
        logger.addHandler(new Handler() {
            public void publish(LogRecord record) {
                if (record.getLevel().intValue() >= Level.WARNING.intValue()) {
                    synchronized (warnings) { warnings.add(Map.of("level", record.getLevel().getName(), "message", String.valueOf(record.getMessage()))); }
                }
            }
            public void flush() { }
            public void close() { }
        });
    }

    private void launch() throws Exception {
        Path resources = Path.of(one("resources")).toRealPath();
        int lnmode = Integer.parseInt(one("lnmode"));
        if (lnmode < 0 || lnmode > 2) throw new IllegalArgumentException("lnmode must be 0, 1 or 2");
        Config config = new Config();
        config.setPlayername("capture"); config.setPlayerpath(root.resolve("player").toString());
        config.setSongpath(root.resolve("songdata.db").toString()); config.setSonginfopath(root.resolve("songinfo.db").toString());
        config.setTablepath(root.resolve("table").toString()); config.setSkinpath(skin.getParent().toString());
        config.setBmsroot(new String[0]); config.setTableURL(new String[0]);
        config.setEnableIpfs(false); config.setUseDiscordRPC(false); config.setUpdatesong(false);
        config.setScanSongArchives(false); config.setUseSongInfo(false); config.setCacheSkinImage(false);
        config.setBgmpath(""); config.setSoundpath(""); config.setClipboardWhenScreenshot(false);
        config.setSystemfontpath(resources.resolve("font/VL-Gothic-Regular.ttf").toString());
        config.setMessagefontpath(resources.resolve("font/VL-Gothic-Regular.ttf").toString());
        if (!Files.isRegularFile(Path.of(config.getSystemfontpath()))) throw new IOException("Resource root lacks the official font");
        config.setResolution(Resolution.valueOf(one("resolution"))); config.setDisplaymode(Config.DisplayMode.WINDOW);
        config.setMaxFramePerSecond(120); config.setVsync(false); config.setFrameskip(1);
        AudioConfig audio = new AudioConfig(); audio.setDriver(AudioConfig.DriverType.OpenAL);
        audio.setSystemvolume(0); audio.setKeyvolume(0); audio.setBgvolume(0); config.setAudioConfig(audio);
        config.validate();
        PlayerConfig player = new PlayerConfig(); player.setId("capture"); player.setName("Synthetic capture");
        player.setIrconfig(new IRConfig[0]); player.setRequestEnable(false); player.setRandom(0); player.setRandom2(0);
        player.setDoubleoption(0); player.setGauge(GrooveGauge.HARD); player.setGaugeAutoShift(PlayerConfig.GAUGEAUTOSHIFT_NONE);
        player.setLnmode(lnmode); player.setTargetid("RATE_AAA"); player.setTargetlist(new String[] {"RATE_AAA"});
        player.setJudgetiming(0); player.setNotesDisplayTimingAutoAdjust(false);
        // The official distribution changes Default paths to ModernChic; use explicit source resources.
        Map<Integer, String> defaults = Map.ofEntries(
            Map.entry(0, "play/play7.luaskin"), Map.entry(1, "play5.json"), Map.entry(2, "play14.json"),
            Map.entry(3, "play10.json"), Map.entry(4, "play9.json"), Map.entry(5, "select.json"),
            Map.entry(6, "decide/decide.luaskin"), Map.entry(7, "result/result.luaskin"),
            Map.entry(8, "keyconfig/keyconfig.luaskin"), Map.entry(9, "skinselect/skinselect.luaskin"),
            Map.entry(15, "graderesult.json"), Map.entry(16, "play24.json"), Map.entry(17, "play24double.json"));
        for (Map.Entry<Integer, String> entry : defaults.entrySet()) {
            Path path = resources.resolve("skin/default").resolve(entry.getValue());
            if (!Files.isRegularFile(path)) throw new IOException("Missing official default skin: " + path);
            player.getSkin()[entry.getKey()] = new SkinConfig(path.toString());
        }
        SkinConfig selected = new SkinConfig(skin.toString());
        SkinConfig.Property properties = new SkinConfig.Property();
        Map<String, Integer> options = requestedOptions;
        if (!mode.equals("dp") && !one("side-option").isEmpty()) options.put(one("side-option"), mode.equals("sp2") ? 921 : 920);
        for (String value : args.getOrDefault("option", List.of())) {
            String[] parts = pair(value); options.put(parts[0], Integer.parseInt(parts[1]));
        }
        List<SkinConfig.Option> customOptions = new ArrayList<>();
        options.forEach((name, value) -> { SkinConfig.Option o = new SkinConfig.Option(); o.name = name; o.value = value; customOptions.add(o); });
        properties.setOption(customOptions.toArray(SkinConfig.Option[]::new));
        List<SkinConfig.FilePath> files = new ArrayList<>();
        for (String value : args.getOrDefault("file", List.of())) {
            String[] parts = pair(value); SkinConfig.FilePath f = new SkinConfig.FilePath(); f.name = parts[0]; f.path = parts[1]; files.add(f);
        }
        properties.setFile(files.toArray(SkinConfig.FilePath[]::new)); selected.setProperties(properties);
        player.getSkin()[mode.equals("dp") ? 2 : 0] = selected;
        receipt.put("skinOptionsRequested", options); receipt.put("longNoteMode", lnmode);
        PlayConfig play = player.getPlayConfig(mode.equals("dp") ? Mode.BEAT_14K : Mode.BEAT_7K).getPlayconfig();
        play.setFixhispeed(PlayConfig.FIX_HISPEED_OFF); play.setHispeed(Float.parseFloat(one("hispeed")));
        play.setEnablelanecover(false); play.setEnablelift(false); play.setEnablehidden(false);
        for (String name : new String[]{"lanecover", "lift", "hidden"}) if (args.containsKey(name)) {
            float value = Float.parseFloat(one(name));
            if (!Float.isFinite(value) || value < 0 || value > 1) throw new IllegalArgumentException(name + " must be a finite ratio from 0 to 1");
            switch (name) {
                case "lanecover" -> { play.setLanecover(value); play.setEnablelanecover(value > 0); }
                case "lift" -> { play.setLift(value); play.setEnablelift(value > 0); }
                case "hidden" -> { play.setHidden(value); play.setEnablehidden(value > 0); }
            }
            requestedReading.put(name, value);
        }
        receipt.put("readingRequested", requestedReading); receipt.put("readingConfigured", readingConfiguration(play));
        Files.createDirectories(root.resolve("player/capture")); Files.createDirectories(root.resolve("table"));
        Files.createDirectories(root.resolve("frames")); Files.createDirectories(root.resolve("fixture"));
        for (String directory : new String[]{"folder", "random"}) {
            Files.createDirectories(root.resolve(directory)); Files.writeString(root.resolve(directory + "/default.json"), "[]\n", StandardCharsets.UTF_8);
        }
        chart = fixture();
        BMSModel model = new BMSDecoder(lnmode).decode(chart);
        if (model == null || model.getMode() != (mode.equals("dp") ? Mode.BEAT_14K : Mode.BEAT_7K))
            throw new IllegalStateException("Synthetic chart has the wrong mode");
        expectedNotes = model.getTotalNotes();
        receipt.put("chart", chart.toString()); receipt.put("chartSha256", model.getSHA256()); receipt.put("expectedNotes", expectedNotes);
        receipt.put("chartLastNoteUs", model.getLastNoteMilliTime() * 1000);
        if (!scenario.equals("autoplay")) prepareReplay(model, config, play, lnmode);
        Config.write(config); PlayerConfig.write(config.getPlayerpath(), player);
        SQLiteSongDatabaseAccessor songs = new SQLiteSongDatabaseAccessor(config.getSongpath(), new String[0], root.resolve("player/capture/songreview.db").toString());
        main = new MainController(chart, config, player, scenario.equals("autoplay") ? BMSPlayerMode.AUTOPLAY : BMSPlayerMode.REPLAY_1, false, songs);
        LwjglApplicationConfiguration window = new LwjglApplicationConfiguration();
        window.width = config.getResolution().width; window.height = config.getResolution().height;
        window.fullscreen = false; window.vSyncEnabled = false; window.foregroundFPS = 120; window.backgroundFPS = 120;
        window.title = "beatoraja skin capture / " + scenario; window.forceExit = true;
        new LwjglApplication(this, window);
    }

    private Path fixture() throws IOException {
        Path dir = root.resolve("fixture");
        ByteBuffer wav = ByteBuffer.allocate(44 + 4410 * 2).order(ByteOrder.LITTLE_ENDIAN);
        wav.put("RIFF".getBytes(StandardCharsets.US_ASCII)).putInt(wav.capacity() - 8).put("WAVEfmt ".getBytes(StandardCharsets.US_ASCII));
        wav.putInt(16).putShort((short) 1).putShort((short) 1).putInt(44100).putInt(88200).putShort((short) 2).putShort((short) 16);
        wav.put("data".getBytes(StandardCharsets.US_ASCII)).putInt(wav.capacity() - 44); Files.write(dir.resolve("silence.wav"), wav.array());
        BufferedImage image = new BufferedImage(512, 384, BufferedImage.TYPE_INT_RGB);
        Graphics2D g = image.createGraphics();
        g.setColor(new Color(10, 20, 38)); g.fillRect(0, 0, 512, 384);
        for (int i = 0; i < 8; i++) { g.setColor(Color.getHSBColor(i / 8f, .7f, .65f)); g.fillRect(i * 64, 64, 64, 256); }
        g.setColor(Color.WHITE); g.drawString("SYNTHETIC BGA / beatoraja skin capture", 36, 36); g.dispose();
        ImageIO.write(image, "png", dir.resolve("bga.png").toFile());
        StringBuilder bms = new StringBuilder("#PLAYER " + (mode.equals("dp") ? "2" : "1") + "\n#TITLE Synthetic Skin Capture\n#ARTIST OMS QA\n#BPM 120\n#PLAYLEVEL 1\n#RANK 2\n#TOTAL 300\n#WAV01 silence.wav\n#BMP01 bga.png\n#00104:01\n");
        int[] channels = {11, 12, 13, 14, 15, 18, 19, 16};
        for (int side = 0; side < (mode.equals("dp") ? 2 : 1); side++) {
            for (int lane = 0; lane < channels.length; lane++) {
                String[] row = new String[16]; Arrays.fill(row, "00"); row[lane * 2] = "01";
                bms.append(String.format(Locale.ROOT, "#001%02d:%s\n", channels[lane] + side * 10, String.join("", row)));
                // Four chords after the opening sweep make HARD failure occur naturally.
                bms.append(String.format(Locale.ROOT, "#002%02d:01010101\n", channels[lane] + side * 10));
            }
            // A normal-key LN and scratch LN, without overlapping regular notes.
            bms.append(String.format(Locale.ROOT, "#003%02d:01\n#004%02d:01\n", 51 + side * 10, 51 + side * 10));
            bms.append(String.format(Locale.ROOT, "#003%02d:0001\n#004%02d:0001\n", 56 + side * 10, 56 + side * 10));
        }
        Path path = dir.resolve("capture.bms"); Files.writeString(path, bms, StandardCharsets.UTF_8); return path;
    }

    private void prepareReplay(BMSModel model, Config config, PlayConfig play, int lnmode) throws IOException {
        List<KeyInputLog> keys = new ArrayList<>(); LaneProperty lanes = new LaneProperty(model.getMode());
        boolean first = true;
        for (TimeLine time : model.getAllTimeLines()) {
            for (int lane = 0; lane < model.getMode().key; lane++) {
                Note note = time.getNote(lane); if (note == null) continue;
                if (scenario.equals("failure") && !first) continue;
                long us = time.getMicroTime(); int[] key = lanes.getLaneKeyAssign()[lane];
                if (note instanceof LongNote ln) {
                    if (!ln.isEnd()) keys.add(new KeyInputLog(us, key[0], true));
                    else {
                        keys.add(new KeyInputLog(us, key[0], false));
                        if (key.length == 2 && (ln.getType() >= LongNote.TYPE_CHARGENOTE || (ln.getType() == 0 && lnmode > 0))) {
                            keys.add(new KeyInputLog(us, key[1], true)); keys.add(new KeyInputLog(us + 50000, key[1], false));
                        }
                    }
                } else if (note instanceof NormalNote) {
                    keys.add(new KeyInputLog(us, key[0], true)); keys.add(new KeyInputLog(us + 50000, key[0], false));
                }
                first = false;
            }
        }
        keys.sort(Comparator.comparingLong(KeyInputLog::getTime));
        ReplayData replay = new ReplayData(); replay.player = "Synthetic capture"; replay.sha256 = model.getSHA256();
        replay.mode = lnmode; replay.gauge = GrooveGauge.HARD; replay.config = play.clone(); replay.keylog = keys.toArray(KeyInputLog[]::new);
        receipt.put("replayInputCount", keys.size()); receipt.put("replayFirstInputUs", keys.get(0).getTime());
        PlayDataAccessor data = new PlayDataAccessor(config);
        data.wrireReplayData(replay, model, lnmode, 0);
        ReplayData check = data.readReplayData(model, lnmode, 0);
        if (check == null || check.keylog.length != keys.size() || check.keylog[0].getTime() != keys.get(0).getTime())
            throw new IOException("Native replay round trip failed");
    }

    public void create() {
        try { main.create(); created = true; }
        catch (Throwable e) { fail(e); }
    }
    public void render() {
        if (exiting) return;
        try {
            if (elapsed() > Long.parseLong(one("timeout")) * 1000) throw new IllegalStateException("Timed out before natural completion");
            long renderStart = System.nanoTime();
            main.render();
            long renderDuration = System.nanoTime() - renderStart;
            MainState state = main.getCurrentState();
            String label = state.getClass().getSimpleName() + (state instanceof BMSPlayer play ? ":" + play.getState() : "");
            boolean transition = !label.equals(lastState);
            if (transition) { transitions.add(Map.of("state", label, "elapsedMs", elapsed())); lastState = label; }
            if (state instanceof BMSPlayer play) {
                // The judge thread may change timers during a render. Classify
                // by the observed state after render, and record this boundary.
                if (state.timer.isTimerOn(48)) afterFcRenderNs.add(renderDuration);
                else if (play.getState() == BMSPlayer.STATE_PLAY) playRenderNs.add(renderDuration);
                verifySkin(state);
                if (!scenario.equals("autoplay") && state.resource.getPlayMode().mode != BMSPlayerMode.Mode.REPLAY)
                    throw new IllegalStateException("Native replay fell back to another play mode");
                for (int timer : TIMERS) if (state.timer.isTimerOn(timer))
                    seenTimers.putIfAbsent(Integer.toString(timer), Map.of("elapsedMs", elapsed(), "timerStartUs", state.timer.getMicroTimer(timer)));
                maxPastNotes = Math.max(maxPastNotes, play.getPastNotes()); maxCombo = Math.max(maxCombo, play.getJudgeManager().getCombo());
                maxExscore = Math.max(maxExscore, play.getJudgeManager().getScoreData().getExscore());
                minimumGauge = Math.min(minimumGauge, Math.round(play.getGauge().getValue()));
                for (int i = 0; i < 6; i++) lastJudges[i] = play.getJudgeManager().getJudgeCount(i);
                captureFirstEffects(state);
                if (transition) capture("state-" + label.replace(':', '-'), state);
                long playMs = state.timer.isTimerOn(41) ? state.timer.getNowTime(41) : -1;
                if (playMs >= 0 && playMs / 1000 > lastPeriodic && playMs <= 10000) {
                    lastPeriodic = playMs / 1000; capture("play-" + lastPeriodic + "s", state);
                }
                for (int timer : new int[]{3, 48, 49, 143, 908}) {
                    if (state.timer.isTimerOn(timer)) {
                        long age = state.timer.getNowTime(timer);
                        for (int threshold : new int[]{0, 250, 500})
                            if (age >= threshold && age < threshold + 250) capture("timer-" + timer + "-" + threshold, state);
                    }
                }
            } else if (identityVerified && (state instanceof MusicResult || state instanceof MusicSelector)) {
                if (completedAt == -1) completedAt = elapsed();
                long completionAge = elapsed() - completedAt;
                for (int threshold : new int[]{0, 250, 1000})
                    if (completionAge >= threshold) capture("completion-" + state.getClass().getSimpleName() + "-" + threshold, state);
                boolean failed = seenTimers.containsKey("3"), fullCombo = seenTimers.containsKey("48");
                ScoreData score = state.resource.getScoreData();
                if (score != null) receipt.put("result", Map.of("clear", score.getClear(), "exscore", score.getExscore(), "notes", score.getNotes(), "combo", score.getCombo(), "minbp", score.getMinbp()));
                receipt.put("completionState", state.getClass().getSimpleName());
                outcome = scenario.equals("failure")
                    ? failed && minimumGauge == 0 && state instanceof MusicResult && score != null && score.getClear() == ClearType.Failed.id
                    : !failed && fullCombo && maxPastNotes == expectedNotes && maxExscore == expectedNotes * 2
                      && (scenario.equals("autoplay") ? state instanceof MusicSelector : state instanceof MusicResult && score != null && score.getClear() >= ClearType.FullCombo.id);
                if (!outcome) error = "Natural outcome did not satisfy scenario assertions";
                if (completionAge >= 1000) { exiting = true; writeReceipt(); main.exit(); }
            }
        } catch (Throwable e) { fail(e); }
    }

    private void captureFirstEffects(MainState state) throws IOException {
        long now = state.timer.getNowMicroTime();
        for (int timer : EFFECT_TIMERS) {
            if (state.timer.isTimerOn(timer)) firstEffectTimers.putIfAbsent(timer, state.timer.getMicroTimer(timer));
            Long origin = firstEffectTimers.get(timer);
            if (origin == null) continue;
            long age = now - origin;
            for (int offset : EFFECT_OFFSETS_MS) {
                if (age < offset * 1000L) continue;
                boolean sameTrigger = state.timer.isTimerOn(timer) && state.timer.getMicroTimer(timer) == origin;
                Map<String, Object> frame = capture("effect-first-" + timer + "-" + offset + "ms", state);
                if (frame != null) frame.put("firstEffect", Map.of(
                        "timer", timer, "firstTimerStartUs", origin, "targetOffsetMs", offset,
                        "renderClockAgeMs", age / 1000.0, "latenessMs", age / 1000.0 - offset,
                        "timerStillOnFirstTrigger", sameTrigger));
            }
        }
    }

    private void verifySkin(MainState state) throws IOException {
        if (identityVerified) return;
        Skin loaded = state.getSkin();
        if (loaded == null || loaded.header == null || !loaded.header.getPath().toRealPath().equals(skin))
            throw new IllegalStateException("Target skin did not load; default fallback is not accepted");
        if (loaded.header.getSkinType().getId() != (mode.equals("dp") ? 2 : 0)) throw new IllegalStateException("Skin type does not match chart mode");
        Map<String, Integer> selectedOptions = new LinkedHashMap<>();
        for (SkinHeader.CustomOption option : loaded.header.getCustomOptions()) selectedOptions.put(option.name, option.getSelectedOption());
        for (Map.Entry<String, Integer> requested : requestedOptions.entrySet())
            if (!requested.getValue().equals(selectedOptions.get(requested.getKey())))
                throw new IllegalStateException("Skin ignored option: " + requested.getKey() + "=" + requested.getValue());
        receipt.put("skinOptionsLoaded", selectedOptions);
        Map<String, Integer> objects = new TreeMap<>();
        for (SkinObject object : loaded.getAllSkinObjects()) objects.merge(object.getClass().getSimpleName(), 1, Integer::sum);
        if (!objects.containsKey("SkinNote")) throw new IllegalStateException("Loaded play skin has no native note object");
        receipt.put("skinLoaded", Map.of("path", loaded.header.getPath().toString(), "name", loaded.header.getName(), "type", loaded.header.getSkinType().toString(), "objects", objects));
        PlayConfig actual = ((BMSPlayer) state).getLanerender().getPlayConfig();
        for (Map.Entry<String, Float> requested : requestedReading.entrySet()) {
            float value = switch (requested.getKey()) { case "lanecover" -> actual.getLanecover(); case "lift" -> actual.getLift(); default -> actual.getHidden(); };
            boolean enabled = switch (requested.getKey()) { case "lanecover" -> actual.isEnablelanecover(); case "lift" -> actual.isEnablelift(); default -> actual.isEnablehidden(); };
            if (Math.abs(value - requested.getValue()) > 0.000001f || enabled != (requested.getValue() > 0))
                throw new IllegalStateException("Native play config ignored reading setting: " + requested.getKey());
        }
        receipt.put("readingLoaded", readingConfiguration(actual));
        identityVerified = true;
    }

    private static Map<String, Object> readingConfiguration(PlayConfig play) {
        return Map.of("lanecover", play.getLanecover(), "lanecoverEnabled", play.isEnablelanecover(),
                "lift", play.getLift(), "liftEnabled", play.isEnablelift(),
                "hidden", play.getHidden(), "hiddenEnabled", play.isEnablehidden());
    }

    private Map<String, Object> snapshot(MainState state) {
        Map<String, Object> value = new LinkedHashMap<>(); value.put("elapsedMs", elapsed()); value.put("state", lastState);
        Map<String, Long> timers = new LinkedHashMap<>();
        for (int timer : TIMERS) if (state.timer.isTimerOn(timer)) timers.put(Integer.toString(timer), state.timer.getNowTime(timer));
        value.put("timerAgeMs", timers);
        if (state instanceof BMSPlayer play) {
            value.put("pastNotes", play.getPastNotes()); value.put("combo", play.getJudgeManager().getCombo());
            value.put("exscore", play.getJudgeManager().getScoreData().getExscore()); value.put("gauge", play.getGauge().getValue());
            value.put("judgeCounts", Arrays.stream(lastJudges).boxed().toList());
            value.put("reading", readingConfiguration(play.getLanerender().getPlayConfig()));
        }
        return value;
    }

    private Map<String, Object> capture(String label, MainState state) throws IOException {
        if (!captured.add(label)) return null;
        long captureStart = System.nanoTime();
        Map<String, Object> info = Collections.synchronizedMap(snapshot(state));
        int w = Gdx.graphics.getBackBufferWidth(), h = Gdx.graphics.getBackBufferHeight();
        long readbackStart = System.nanoTime();
        byte[] pixels = ScreenUtils.getFrameBufferPixels(0, 0, w, h, true);
        long readbackDuration = System.nanoTime() - readbackStart;
        readbackNs.add(readbackDuration);
        BufferedImage image = new BufferedImage(w, h, BufferedImage.TYPE_INT_ARGB);
        int[] argb = new int[w * h];
        for (int i = 0; i < argb.length; i++) argb[i] = 0xff000000 | (pixels[i * 4] & 255) << 16 | (pixels[i * 4 + 1] & 255) << 8 | (pixels[i * 4 + 2] & 255);
        image.setRGB(0, 0, w, h, argb, 0, w);
        String filename = String.format(Locale.ROOT, "%03d-%s.png", frames.size(), label);
        Path output = root.resolve("frames").resolve(filename);
        info.put("file", "frames/" + filename); info.put("width", w); info.put("height", h);
        frames.add(info);
        long enqueueTime = System.nanoTime();
        // Only readback and pixel conversion run on the GL thread. The bounded
        // queue avoids retaining a run's worth of UHD images; when full it
        // applies backpressure on this thread, visible in sampling lateness.
        pngWriter.execute(() -> {
            try {
                long pngStart = System.nanoTime();
                if (!ImageIO.write(image, "png", output.toFile())) throw new IOException("No PNG writer available");
                long pngDuration = System.nanoTime() - pngStart;
                pngWriteNs.add(pngDuration);
                info.put("sha256", HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest(Files.readAllBytes(output))));
                long completionDuration = System.nanoTime() - captureStart;
                captureNs.add(completionDuration);
                info.put("captureTimingMs", Map.of("framebufferReadback", readbackDuration / 1_000_000.0,
                        "pngEncodeAndWrite", pngDuration / 1_000_000.0,
                        "queueWait", (pngStart - enqueueTime) / 1_000_000.0,
                        "completionIncludingQueueAndHash", completionDuration / 1_000_000.0));
            } catch (Exception e) {
                e.printStackTrace(); pngFailures.add(filename + ": " + e);
            }
        });
        return info;
    }

    private void fail(Throwable e) {
        e.printStackTrace(); error = e.toString(); exiting = true; writeReceipt();
        if (Gdx.app != null) Gdx.app.exit(); else System.exit(2);
    }
    public void resize(int width, int height) { if (created && !exiting) main.resize(width, height); }
    public void pause() { if (created && !exiting) main.pause(); }
    public void resume() { if (created && !exiting) main.resume(); }
    public void dispose() {
        try { if (created) main.dispose(); disposed = created; }
        catch (Throwable e) { e.printStackTrace(); error = "Dispose failed: " + e; }
        finally {
            writeReceipt();
            for (Handler handler : Logger.getGlobal().getHandlers()) { handler.flush(); handler.close(); }
        }
    }
    private void writeReceipt() {
        finishPngWrites();
        boolean diagnosticsClean;
        synchronized (warnings) {
            diagnosticsClean = warnings.stream().allMatch(w -> ((Map<?, ?>) w).get("message").toString().contains("Network disabled by skin capture host"));
            receipt.put("warnings", new ArrayList<>(warnings));
        }
        receipt.put("success", outcome && identityVerified && disposed && diagnosticsClean && error == null);
        receipt.put("diagnosticsClean", diagnosticsClean);
        receipt.put("normalDispose", disposed); receipt.put("skinIdentityVerified", identityVerified); receipt.put("error", error);
        receipt.put("elapsedMs", elapsed()); receipt.put("frames", frames); receipt.put("transitions", transitions); receipt.put("seenTimers", seenTimers);
        receipt.put("maxPastNotes", maxPastNotes); receipt.put("maxCombo", maxCombo); receipt.put("maxExscore", maxExscore); receipt.put("minimumGauge", minimumGauge);
        receipt.put("finalJudgeCounts", Arrays.stream(lastJudges).boxed().toList());
        receipt.put("blockedNetworkRequests", blockedNetworkRequests.get());
        receipt.put("firstEffectTimerOriginsUs", firstEffectTimers);
        receipt.put("performance", Map.of(
                "measurement", "System.nanoTime wall duration of MainController.render(); readback/PNG/hash excluded. Includes any engine CPU/GPU waits; not pure CPU time, FPS, latency or play-feel acceptance.",
                "classification", "State sampled after render: BMSPlayer STATE_PLAY without timer48 versus BMSPlayer with timer48 on, including its finished/outro state.",
                "percentile", "Median averages the two middle samples; p95 is nearest rank ceil(0.95*count). Empty groups use null values.",
                "playBeforeFc", timingSummary(playRenderNs), "afterFc", timingSummary(afterFcRenderNs),
                "pngEncodeAndWrite", timingSummary(pngWriteNs), "framebufferReadback", timingSummary(readbackNs),
                "captureCompletionIncludingQueueAndHash", timingSummary(captureNs)));
        try { Files.writeString(root.resolve("receipt.json"), json(receipt) + "\n", StandardCharsets.UTF_8); }
        catch (IOException e) { e.printStackTrace(); }
    }
    private void finishPngWrites() {
        if (pngWritesFinished) return;
        pngWriter.shutdown();
        try {
            if (!pngWriter.awaitTermination(30, TimeUnit.SECONDS)) {
                pngWriter.shutdownNow(); pngFailures.add("PNG writer did not finish within 30 seconds");
            }
        } catch (InterruptedException e) {
            pngWriter.shutdownNow(); Thread.currentThread().interrupt(); pngFailures.add("Interrupted while joining PNG writer");
        }
        pngWritesFinished = true;
        if (captureNs.size() != frames.size())
            pngFailures.add("PNG completion count does not match captured frames: " + captureNs.size() + "/" + frames.size());
        if (!pngFailures.isEmpty()) {
            String failure = "Capture writes failed: " + String.join("; ", pngFailures);
            error = error == null ? failure : error + "; " + failure;
        }
        receipt.put("pngWritesFinished", pngWriter.isTerminated());
        receipt.put("pngWriteFailures", new ArrayList<>(pngFailures));
    }
    private static Map<String, Object> timingSummary(List<Long> samples) {
        List<Long> sorted = new ArrayList<>(samples); Collections.sort(sorted);
        int count = sorted.size(); Map<String, Object> summary = new LinkedHashMap<>();
        summary.put("count", count);
        summary.put("medianMs", count == 0 ? null : (count % 2 == 1 ? sorted.get(count / 2) / 1_000_000.0
                : (sorted.get(count / 2 - 1) / 2.0 + sorted.get(count / 2) / 2.0) / 1_000_000.0));
        summary.put("p95Ms", count == 0 ? null : sorted.get((int)Math.ceil(count * 0.95) - 1) / 1_000_000.0);
        summary.put("maxMs", count == 0 ? null : sorted.get(count - 1) / 1_000_000.0);
        return summary;
    }
    private long elapsed() { return (System.nanoTime() - started) / 1_000_000; }
    private String one(String name) {
        List<String> values = args.get(name);
        if (values == null || values.size() != 1) throw new IllegalArgumentException("Expected one --" + name);
        return values.get(0);
    }
    private static String[] pair(String value) {
        int index = value.indexOf('=');
        if (index <= 0 || index == value.length() - 1) throw new IllegalArgumentException("Expected name=value: " + value);
        return new String[]{value.substring(0, index), value.substring(index + 1)};
    }
    private static Map<String, List<String>> parse(String[] argv) {
        Map<String, List<String>> args = new LinkedHashMap<>();
        for (int i = 0; i < argv.length; i += 2) {
            if (i + 1 >= argv.length || !argv[i].startsWith("--")) throw new IllegalArgumentException("Expected --name value pairs");
            args.computeIfAbsent(argv[i].substring(2), k -> new ArrayList<>()).add(argv[i + 1]);
        }
        return args;
    }
    private static String json(Object value) {
        if (value == null) return "null";
        if (value instanceof Number || value instanceof Boolean) return value.toString();
        if (value instanceof Map<?, ?> map) {
            List<String> items = new ArrayList<>(); map.forEach((key, item) -> items.add(json(key.toString()) + ":" + json(item)));
            return "{" + String.join(",", items) + "}";
        }
        if (value instanceof Iterable<?> list) { List<String> items = new ArrayList<>(); for (Object item : list) items.add(json(item)); return "[" + String.join(",", items) + "]"; }
        StringBuilder text = new StringBuilder("\"");
        for (char c : value.toString().toCharArray()) {
            switch (c) {
                case '\\' -> text.append("\\\\"); case '"' -> text.append("\\\""); case '\n' -> text.append("\\n"); case '\r' -> text.append("\\r"); case '\t' -> text.append("\\t");
                default -> { if (c < 32) text.append(String.format(Locale.ROOT, "\\u%04x", (int) c)); else text.append(c); }
            }
        }
        return text.append('"').toString();
    }
}
