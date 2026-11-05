// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using osu.Framework;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Tests;

namespace osu.Tools.StandardSkinCapture;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        CaptureSettings? settings = null;
        int exitCode = 1;
        string? failure = null;
        try
        {
            settings = CaptureSettings.Parse(args);
            CaptureSettings.Current = settings;
            Directory.CreateDirectory(settings.Output);
            Directory.CreateDirectory(settings.TemporaryRoot);
            Environment.SetEnvironmentVariable("TEMP", settings.TemporaryRoot);
            Environment.SetEnvironmentVariable("TMP", settings.TemporaryRoot);
            string nativeRuntimeDirectory = ConfigureNativeRuntime();
            settings.VerifyPackage();
            settings.Write("host-started.json", new
            {
                utc = DateTimeOffset.UtcNow,
                settings.Package,
                settings.PackageSha256,
                settings.Ruleset,
                settings.Keys,
                settings.Presentation,
                settings.LongNoteMode,
                settings.ChartVariant,
                settings.Assist,
                settings.ReadingMode,
                settings.Options,
                settings.TemporaryRoot,
                runtime = AppContext.BaseDirectory,
                nativeRuntimeDirectory,
                assemblies = new[] { "osu.Game.dll", "osu.Game.Rulesets.Bms.dll", "osu.Game.Rulesets.Mania.dll", "osu.Framework.dll" }
                    .Select(name => new { name, sha256 = CaptureSettings.Hash(Path.Combine(AppContext.BaseDirectory, name)) }).ToArray(),
                isolation = "Portable host, current ExactVisualTestGame, fresh disposable inner storage, dummy offline API",
                input = settings.ChartVariant == "graph-pb"
                    ? "Two ordinary BmsSoloPlayer plays in one isolated store; the warmup omits the first two complete short-note chords, native Player/ScoreManager saves its completed result, and the second play reads the matching PB through the production query"
                    : settings.ChartVariant == "failure-skip"
                    ? "Actual BmsSoloPlayer with Hazard and empty ordinary replay input; native misses cause failure, followed by ordinary InputManager Escape press/release to skip the outro"
                    : settings.ChartVariant == "failure"
                    ? "Actual BmsSoloPlayer with Hazard and empty ordinary replay input; native misses cause failure, without Autoplay/NoFail or injected failure events"
                    : settings.ChartVariant == "reading"
                    ? "Ordinary BmsSoloPlayer with synthetic native Oms actions for Start and chart keys; no Autoplay, replay ownership, injected skin values or device qualification"
                    : settings.Assist == "scratch"
                    ? "Actual BmsModAutoScratch plus ordinary key replay input with all scratch inputs removed; no Autoplay/NoFail or injected roles"
                    : settings.ChartVariant == "long-note-transitions"
                    ? "Constructed real replay input through ReplayPlayer, including release/regrab/head miss/early tail; no injected skin events or human/device qualification"
                    : "Ruleset-generated Autoplay through the actual ReplayPlayer; no human/device qualification",
                evidence = "Synthetic chart and exact imported OSK; no original-game equivalence claim",
            });
            using var log = new StreamWriter(new FileStream(Path.Combine(settings.Output, "runtime.log"), FileMode.CreateNew, FileAccess.Write))
            { AutoFlush = true };
            object logGate = new object();
            Action<LogEntry> writeLog = entry =>
            {
                lock (logGate)
                    log.WriteLine($"{DateTimeOffset.UtcNow:O} [{entry.Level}] {entry.Message}"
                                  + (entry.Exception == null ? "" : Environment.NewLine + FormatException(entry.Exception)));
            };
            Logger.NewEntry += writeLog;
            try
            {
                using var game = new ExactVisualTestGame(typeof(TestSceneStandardSkinCapture));
                using DesktopGameHost host = Host.GetSuitableDesktopHost("oms-standard-skin-capture-" + Guid.NewGuid().ToString("N"),
                    new HostOptions { PortableInstallation = true, FriendlyGameName = OsuGameBase.GAME_NAME });
                host.Run(game);
                exitCode = game.ExitCode;
                if (exitCode == 0 && !File.Exists(Path.Combine(settings.Output, "completed.json")))
                    throw new InvalidOperationException("The runner exited without the complete native scenario receipt.");
                settings.VerifyPackage();
            }
            finally
            {
                Logger.Flush();
                Logger.NewEntry -= writeLog;
            }
        }
        catch (Exception exception)
        {
            exitCode = 1;
            failure = FormatException(exception);
            Console.Error.WriteLine(failure);
        }
        finally
        {
            if (settings != null && Directory.Exists(settings.Output))
                settings.Write("host-exit.json", new { exitCode, failure, utc = DateTimeOffset.UtcNow });
        }
        return exitCode;
    }

    private static string FormatException(Exception exception)
    {
        var diagnostics = new List<object>();
        var visited = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
        Append(exception, "root");
        return exception + (diagnostics.Count == 0 ? "" : Environment.NewLine + "Exception numeric data: " + JsonSerializer.Serialize(diagnostics));

        void Append(Exception current, string chain)
        {
            if (!visited.Add(current))
                return;
            var numericData = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry item in current.Data)
            {
                if (item.Key is not string key || !Regex.IsMatch(key, "\\A[A-Za-z][A-Za-z0-9_.-]{0,95}\\z", RegexOptions.CultureInvariant))
                    continue;
                if (item.Value is byte or sbyte or short or ushort or int or uint or long or ulong or decimal
                    || item.Value is float single && float.IsFinite(single)
                    || item.Value is double number && double.IsFinite(number))
                    numericData[key] = item.Value;
            }
            if (numericData.Count != 0)
                diagnostics.Add(new { chain, type = current.GetType().FullName, data = numericData });
            if (current is AggregateException aggregate)
            {
                for (int index = 0; index < aggregate.InnerExceptions.Count; index++)
                    Append(aggregate.InnerExceptions[index], chain + ".inner[" + index + "]");
            }
            else if (current.InnerException != null)
                Append(current.InnerException, chain + ".inner");
        }
    }

    private static string ConfigureNativeRuntime()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Standard skin capture requires the Windows OMS runtime.");
        string runtimeId = "win-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        string directory = Path.Combine(AppContext.BaseDirectory, "runtimes", runtimeId, "native");
        CaptureSettings.AssertNonSystemPlainDirectory(directory);
        if (!Directory.Exists(directory) || !File.Exists(Path.Combine(directory, "SDL2.dll")))
            throw new DirectoryNotFoundException("The current OMS runtime's native dependencies are missing: " + directory);

        // Direct assembly references do not carry the Desktop project's runtime-native dependency entries.
        // Restrict the search-path change to this process and keep the runtime's existing native tree intact.
        string current = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Process) ?? "";
        if (!current.Split(Path.PathSeparator).Contains(directory, StringComparer.OrdinalIgnoreCase))
            Environment.SetEnvironmentVariable("PATH", directory + Path.PathSeparator + current, EnvironmentVariableTarget.Process);
        return directory;
    }
}

internal sealed record CaptureSettings(string Package, string PackageSha256, string Output, string TemporaryRoot,
                                       string Ruleset, int Keys, string Presentation, string LongNoteMode, string ChartVariant,
                                       string Assist, string ReadingMode,
                                       IReadOnlyDictionary<string, string> Options)
{
    public static CaptureSettings Current { get; set; } = null!;

    public static CaptureSettings Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] known = ["package", "sha256", "output", "temp-root", "ruleset", "keys", "presentation", "long-note-mode", "chart", "assist", "reading-mode", "options"];
        if (args.Length == 0 || args.Length % 2 != 0)
            throw new ArgumentException("Usage: --package <osk> --sha256 <hash> --output <fresh directory> --ruleset bms|mania --keys <count> --presentation p1|p2|dp [--options <JSON object>] [--long-note-mode ln|cn|hcn] [--chart standard|combo-stress|long-note-transitions|reading|failure|failure-skip|graph-pb] [--assist none|scratch] [--reading-mode normal|floating|classic] [--temp-root <directory>]");
        for (int index = 0; index < args.Length; index += 2)
        {
            string key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || !known.Contains(key[2..], StringComparer.Ordinal)
                || !values.TryAdd(key[2..], args[index + 1]))
                throw new ArgumentException("Unknown or repeated capture argument: " + key);
        }
        string Required(string name) => values.TryGetValue(name, out string? value) && !string.IsNullOrWhiteSpace(value)
            ? value : throw new ArgumentException("Missing --" + name);
        string package = Path.GetFullPath(Required("package"));
        string hash = Required("sha256").ToLowerInvariant();
        if (!File.Exists(package) || !string.Equals(Path.GetExtension(package), ".osk", StringComparison.OrdinalIgnoreCase)
            || !Regex.IsMatch(hash, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant))
            throw new ArgumentException("A real .osk and its exact SHA256 are required.");
        string output = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Required("output")));
        if (Directory.Exists(output) || File.Exists(output))
            throw new ArgumentException("Use a fresh output directory; existing evidence is never overwritten.");
        string temporary = Path.GetFullPath(values.GetValueOrDefault("temp-root", Path.Combine(output, "temporary")));
        string ruleset = Required("ruleset");
        int keys = int.Parse(Required("keys").TrimEnd('K', 'k'), CultureInfo.InvariantCulture);
        if (ruleset is not ("bms" or "mania") || (ruleset == "bms" ? keys is not (5 or 7 or 14) : keys is not (5 or 6 or 7 or 8 or 14 or 16)))
            throw new ArgumentException("Supported modes: BMS 5/7/14 or mania 5/6/7/8/14/16.");
        string presentation = Required("presentation");
        if (presentation is not ("p1" or "p2" or "dp") || (keys >= 14) != (presentation == "dp"))
            throw new ArgumentException("Use p1/p2 for single play and dp for 14/16 keys.");
        string longNoteMode = values.GetValueOrDefault("long-note-mode", "cn");
        if (longNoteMode is not ("ln" or "cn" or "hcn"))
            throw new ArgumentException("The BMS long-note mode must be ln, cn or hcn.");
        string chartVariant = values.GetValueOrDefault("chart", "standard");
        if (chartVariant is not ("standard" or "combo-stress" or "long-note-transitions" or "reading" or "failure" or "failure-skip" or "graph-pb"))
            throw new ArgumentException("The chart must be standard, combo-stress, long-note-transitions, reading, failure, failure-skip or graph-pb.");
        if (chartVariant == "combo-stress" && (ruleset != "mania" || keys != 16 || presentation != "dp"))
            throw new ArgumentException("The combo-stress chart requires mania 16K dp.");
        if (chartVariant is ("failure" or "failure-skip") && ruleset != "bms")
            throw new ArgumentException("The native Hazard failure chart requires BMS.");
        if (chartVariant == "graph-pb" && ruleset != "bms")
            throw new ArgumentException("The two-play native personal-best chart requires BMS.");
        string assist = values.GetValueOrDefault("assist", "none");
        if (assist is not ("none" or "scratch") || assist == "scratch" && (ruleset != "bms" || chartVariant != "standard"))
            throw new ArgumentException("--assist scratch requires BMS and the standard chart; other cases use none.");
        string readingMode = values.GetValueOrDefault("reading-mode", "normal");
        if (readingMode is not ("normal" or "floating" or "classic") || chartVariant == "reading" && ruleset != "bms"
            || values.ContainsKey("reading-mode") && chartVariant != "reading")
            throw new ArgumentException("--reading-mode requires the BMS reading chart and normal, floating or classic.");
        using JsonDocument optionJson = JsonDocument.Parse(values.GetValueOrDefault("options", "{}"));
        if (optionJson.RootElement.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("--options must be a JSON object of canonical string values.");
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JsonProperty property in optionJson.RootElement.EnumerateObject())
            if (property.Value.ValueKind != JsonValueKind.String || !options.TryAdd(property.Name, property.Value.GetString()!))
                throw new ArgumentException("Every option must have one unique string value.");
        if (options.ContainsKey("layout"))
            throw new ArgumentException("Set layout with --presentation, not a second competing option.");
        if (chartVariant == "graph-pb" && (options.GetValueOrDefault("graph-visible") == "false"
                                          || options.GetValueOrDefault("graph-difference") == "false"))
            throw new ArgumentException("The personal-best capture requires the visible Graph and score difference.");
        AssertNonSystemPlainDirectory(output);
        AssertNonSystemPlainDirectory(temporary);
        AssertNonSystemPlainDirectory(AppContext.BaseDirectory);
        return new CaptureSettings(package, hash, output, temporary, ruleset, keys, presentation, longNoteMode, chartVariant, assist, readingMode, options);
    }

    internal static void AssertNonSystemPlainDirectory(string path)
    {
        if (string.Equals(Path.GetPathRoot(path)?.TrimEnd(Path.DirectorySeparatorChar), Environment.GetEnvironmentVariable("SystemDrive"),
                StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Capture runtime, evidence and temporary storage must stay on a non-system drive.");
        for (string? current = Path.TrimEndingDirectorySeparator(path); current != null; current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint))
                throw new ArgumentException("Capture paths may not traverse reparse points: " + current);
    }

    public void VerifyPackage()
    {
        if (!string.Equals(Hash(Package), PackageSha256, StringComparison.Ordinal))
            throw new InvalidOperationException("The supplied OSK no longer matches its expected SHA256.");
    }

    public static string Hash(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public void Write(string name, object value)
    {
        using var stream = new FileStream(Path.Combine(Output, name), FileMode.CreateNew, FileAccess.Write);
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { WriteIndented = true });
    }
}
