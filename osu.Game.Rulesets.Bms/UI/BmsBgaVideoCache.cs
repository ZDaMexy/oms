// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Logging;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Transcodes BGA videos that osu!framework's bundled FFmpeg pipeline cannot open (notably legacy MPEG-1
    /// program-stream <c>.mpg</c>, plus <c>.wmv/.avi/.flv</c>) into a plain H.264 <c>.mp4</c> via an EXTERNAL
    /// <c>ffmpeg</c> (P1-L Phase 5.1). Results are cached on disk keyed by the source's path/size/mtime. Without an
    /// available ffmpeg this is a graceful no-op and the player keeps its static-image fallback.
    /// </summary>
    public class BmsBgaVideoCache
    {
        public enum VideoSourceState
        {
            /// <summary>A playable file path is available (a framework-friendly source as-is, or a finished transcode).</summary>
            Ready,

            /// <summary>A transcode is running; nothing to play yet. Callers should retry later.</summary>
            Pending,

            /// <summary>No transcode is possible (no ffmpeg / no cache dir / a prior transcode failed).</summary>
            Unavailable,
        }

        public readonly record struct VideoSource(VideoSourceState State, string? Path);

        // Containers the framework's FFmpeg integration fails to even open -> must be transcoded.
        private static readonly string[] legacy_extensions = { ".mpg", ".mpeg", ".avi", ".wmv", ".flv", ".m1v", ".m2v", ".mkv" };

        private const int transcode_timeout_ms = 120_000;

        // Bump when the transcode arguments OR pipeline change so previously cached outputs (produced by old, possibly
        // non-decodable/corrupt settings) are re-transcoded under a new cache key instead of being served as-is.
        // v3: unique temp + cross-instance dedup (v2 outputs could be corrupted by concurrent writers to one temp path).
        private const int transcode_version = 3;

        private readonly string? cacheDirectory;
        private readonly IReadOnlyList<string> ffmpegCandidates;
        private readonly Func<string, string, string, bool> runTranscode;

        // In-flight transcodes keyed by DESTINATION path. STATIC so it dedups across cache instances: a cache is created
        // per gameplay, and an orphaned background transcode from a previous play (or a rapid replay) must not start a
        // second ffmpeg for the same destination. (Combined with a unique temp file per attempt, this prevents the
        // concurrent-writer corruption that produced unplayable "Invalid NAL unit size" .mp4s.)
        private static readonly ConcurrentDictionary<string, byte> inProgress = new ConcurrentDictionary<string, byte>();

        // Per-instance: a transient failure should get a fresh attempt on the next gameplay.
        private readonly ConcurrentDictionary<string, byte> failedDestinations = new ConcurrentDictionary<string, byte>();
        private string? resolvedFfmpeg;
        private bool ffmpegResolved;
        private bool ffmpegMissing;

        /// <param name="cacheDirectory">Absolute directory for transcoded outputs (created on demand). Null disables transcoding.</param>
        /// <param name="ffmpegCandidates">Explicit ffmpeg paths to try before falling back to the <c>ffmpeg</c> on PATH.</param>
        /// <param name="runTranscode">Override for the actual transcode (ffmpeg, source, destinationTmp) =&gt; success. Injected by tests.</param>
        public BmsBgaVideoCache(string? cacheDirectory, IReadOnlyList<string>? ffmpegCandidates = null, Func<string, string, string, bool>? runTranscode = null)
        {
            this.cacheDirectory = cacheDirectory;
            this.ffmpegCandidates = ffmpegCandidates ?? Array.Empty<string>();
            this.runTranscode = runTranscode ?? runFfmpeg;
        }

        /// <summary>
        /// Detects an available external ffmpeg the same way <see cref="resolveFfmpeg"/> would for a transcode: the
        /// explicit <paramref name="candidates"/> first (a file that exists), then an <c>ffmpeg(.exe)</c> discoverable on
        /// the system PATH. Returns the resolved executable path, or <c>null</c> when none is available. Used by the
        /// settings UI to report install status without starting a transcode.
        /// </summary>
        public static string? FindAvailableFfmpeg(IReadOnlyList<string>? candidates)
        {
            if (candidates != null)
            {
                foreach (string candidate in candidates)
                {
                    if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate))
                        return candidate;
                }
            }

            string? path = Environment.GetEnvironmentVariable("PATH");

            if (string.IsNullOrEmpty(path))
                return null;

            foreach (string directory in path.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                try
                {
                    foreach (string executable in new[] { "ffmpeg.exe", "ffmpeg" })
                    {
                        string full = Path.Combine(directory.Trim(), executable);

                        if (File.Exists(full))
                            return full;
                    }
                }
                catch
                {
                    // a malformed PATH entry must not abort the scan
                }
            }

            return null;
        }

        /// <summary>The outcome of actually probing an ffmpeg binary (not just checking the file exists).</summary>
        public enum FfmpegProbeState
        {
            /// <summary>No ffmpeg found among the candidates or on PATH.</summary>
            NotFound,

            /// <summary>A file was found but it could not be executed / did not behave like ffmpeg.</summary>
            NotExecutable,

            /// <summary>ffmpeg runs but lacks the libx264 encoder; the transcode will fall back to the built-in mpeg4.</summary>
            ReadyWithoutH264,

            /// <summary>ffmpeg runs and has libx264 — full H.264 transcode available.</summary>
            Ready,
        }

        public readonly record struct FfmpegProbeResult(FfmpegProbeState State, string? Path, string? Detail);

        /// <summary>
        /// Actually probes ffmpeg (runs <c>ffmpeg -hide_banner -encoders</c>) rather than only checking a file exists,
        /// so the settings UI can report a truthful status: not found, found-but-not-runnable, runnable-without-libx264
        /// (transcode will use the mpeg4 fallback), or fully ready. Returns quickly and never throws.
        /// </summary>
        public static FfmpegProbeResult ProbeFfmpeg(IReadOnlyList<string>? candidates)
        {
            string? path = FindAvailableFfmpeg(candidates);

            if (path == null)
                return new FfmpegProbeResult(FfmpegProbeState.NotFound, null, null);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };

                foreach (string argument in new[] { "-hide_banner", "-encoders" })
                    startInfo.ArgumentList.Add(argument);

                using var process = Process.Start(startInfo);

                if (process == null)
                    return new FfmpegProbeResult(FfmpegProbeState.NotExecutable, path, "无法启动进程");

                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(15_000))
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                        // ignored
                    }

                    return new FfmpegProbeResult(FfmpegProbeState.NotExecutable, path, "执行超时");
                }

                string output = stdoutTask.GetAwaiter().GetResult() + stderrTask.GetAwaiter().GetResult();

                if (process.ExitCode != 0)
                    return new FfmpegProbeResult(FfmpegProbeState.NotExecutable, path, $"退出码 {process.ExitCode}");

                bool hasH264 = output.Contains("libx264", StringComparison.OrdinalIgnoreCase);
                return new FfmpegProbeResult(hasH264 ? FfmpegProbeState.Ready : FfmpegProbeState.ReadyWithoutH264, path, null);
            }
            catch (Exception e)
            {
                return new FfmpegProbeResult(FfmpegProbeState.NotExecutable, path, e.Message);
            }
        }

        /// <summary>Whether the asset's container is one the framework can't decode and therefore needs transcoding.</summary>
        public static bool RequiresTranscode(string? assetFile)
        {
            if (string.IsNullOrWhiteSpace(assetFile))
                return false;

            string extension = Path.GetExtension(assetFile);

            foreach (string legacy in legacy_extensions)
            {
                if (extension.Equals(legacy, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Resolves a playable source for a BGA video. Framework-friendly formats pass through as-is; legacy formats
        /// return a finished transcode when cached, otherwise kick off a background transcode and report <see cref="VideoSourceState.Pending"/>.
        /// </summary>
        public VideoSource Resolve(string sourceAbsolutePath)
        {
            if (string.IsNullOrEmpty(sourceAbsolutePath))
                return new VideoSource(VideoSourceState.Unavailable, null);

            // Formats the framework can open are played directly (no transcode).
            if (!RequiresTranscode(sourceAbsolutePath))
                return new VideoSource(VideoSourceState.Ready, sourceAbsolutePath);

            if (cacheDirectory == null || !File.Exists(sourceAbsolutePath))
                return new VideoSource(VideoSourceState.Unavailable, null);

            string destination = Path.Combine(cacheDirectory, cacheKey(sourceAbsolutePath) + ".mp4");

            if (File.Exists(destination))
                return new VideoSource(VideoSourceState.Ready, destination);

            if (failedDestinations.ContainsKey(destination))
                return new VideoSource(VideoSourceState.Unavailable, null);

            string? ffmpeg = resolveFfmpeg();

            if (ffmpeg == null)
                return new VideoSource(VideoSourceState.Unavailable, null);

            startTranscode(ffmpeg, sourceAbsolutePath, destination);
            return new VideoSource(VideoSourceState.Pending, null);
        }

        private void startTranscode(string ffmpeg, string source, string destination)
        {
            // Dedup concurrent transcodes of the same destination (prewarm + per-event, AND across cache instances —
            // inProgress is static — so a replay / orphaned background transcode never runs a second ffmpeg for it).
            if (!inProgress.TryAdd(destination, 0))
                return;

            Task.Run(() =>
            {
                bool success = false;
                // Unique temp per attempt: even if two transcodes for this destination ever overlap, each writes its own
                // file, so neither can interleave bytes into the other (the cause of corrupt, undecodable .mp4 output).
                string tmp = $"{destination}.{Guid.NewGuid():N}.tmp";

                try
                {
                    Directory.CreateDirectory(cacheDirectory!);

                    success = runTranscode(ffmpeg, source, tmp) && File.Exists(tmp);

                    // Atomic publish (overwrite): only a complete file ever appears at the destination path.
                    if (success)
                        File.Move(tmp, destination, true);
                }
                catch (Exception e)
                {
                    Logger.Log($"BGA video transcode failed for '{source}': {e.Message}", level: LogLevel.Debug);
                    success = false;
                }
                finally
                {
                    if (!success)
                    {
                        // One concise line per failed source (full ffmpeg output is logged just above). BGA is cosmetic
                        // and degrades to its static image, so this is Verbose (log only) — not a user-facing notification.
                        // Deduped via the failed-set add so retries never re-log.
                        if (failedDestinations.TryAdd(destination, 0))
                            Logger.Log($"BGA video transcode failed for '{source}'. The BGA will show its static image instead (see above for the ffmpeg error).", level: LogLevel.Verbose);

                        try
                        {
                            if (File.Exists(tmp))
                                File.Delete(tmp);
                        }
                        catch
                        {
                            // best-effort cleanup
                        }
                    }

                    inProgress.TryRemove(destination, out _);
                }
            });
        }

        private bool runFfmpeg(string ffmpeg, string source, string destinationTmp)
        {
            // Prefer H.264 (the codec the framework decoder handles most reliably); fall back to the always-built-in
            // MPEG-4 Part 2 encoder for stripped / LGPL ffmpeg builds that lack libx264. The point of the transcode is
            // moving the unreadable legacy CONTAINER into a clean .mp4 — either codec decodes fine once it's in .mp4.
            if (runFfmpegWithEncoder(ffmpeg, source, destinationTmp, "libx264"))
                return true;

            // ffmpeg itself is missing -> no point retrying with another encoder.
            if (ffmpegMissing)
                return false;

            return runFfmpegWithEncoder(ffmpeg, source, destinationTmp, "mpeg4");
        }

        /// <summary>
        /// Builds the ffmpeg argument list for a BGA transcode. The output must be decodable by osu!framework's bundled
        /// FFmpeg (and its D3D11VA hardware path), which is pickier than standalone players:
        /// <list type="bullet">
        /// <item><c>-f mp4</c> sets the muxer explicitly — the output is a <c>&lt;hash&gt;.mp4.tmp</c> temp path and
        /// ffmpeg otherwise infers the container from the unknown <c>.tmp</c> extension ("Unable to choose an output format").</item>
        /// <item>libx264 <c>-profile:v baseline</c> (no B-frames / no CABAC) — a default High-profile output decoded fine
        /// in standalone players but failed EVERY packet in-framework ("Failed to send avcodec packet: Invalid data"),
        /// leaving the BGA black. Baseline is the broadly decodable subset for both HW and old SW decoders. mpeg4 (the
        /// no-libx264 fallback) has no such profile.</item>
        /// <item><c>setpts=PTS-STARTPTS</c> resets the source's start-time offset (legacy .mpg often starts at ~0.44s) so
        /// playback maps cleanly from the gameplay clock and no edit list is written; <c>setsar=1</c> squares the pixels.</item>
        /// </list>
        /// Exposed for regression testing.
        /// </summary>
        public static string[] BuildTranscodeArguments(string source, string destinationTmp, string encoder)
        {
            var arguments = new List<string>
            {
                "-y", "-hide_banner",
                "-i", source,
                "-an",
                "-map", "0:v:0",
                "-c:v", encoder,
            };

            if (encoder == "libx264")
            {
                arguments.Add("-profile:v");
                arguments.Add("baseline");
            }

            arguments.Add("-pix_fmt");
            arguments.Add("yuv420p");
            arguments.Add("-vf");
            arguments.Add("setpts=PTS-STARTPTS,setsar=1");
            arguments.Add("-movflags");
            arguments.Add("+faststart");
            arguments.Add("-f");
            arguments.Add("mp4");
            arguments.Add(destinationTmp);

            return arguments.ToArray();
        }

        private bool runFfmpegWithEncoder(string ffmpeg, string source, string destinationTmp, string encoder)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            foreach (string argument in BuildTranscodeArguments(source, destinationTmp, encoder))
                startInfo.ArgumentList.Add(argument);

            try
            {
                using var process = Process.Start(startInfo);

                if (process == null)
                    return false;

                // Read both pipes concurrently so a chatty ffmpeg never blocks on a full buffer, and so stderr (which
                // carries the real failure reason) can be surfaced on error.
                var stderrTask = process.StandardError.ReadToEndAsync();
                var stdoutTask = process.StandardOutput.ReadToEndAsync();

                if (!process.WaitForExit(transcode_timeout_ms))
                {
                    try
                    {
                        process.Kill(true);
                    }
                    catch
                    {
                        // ignored
                    }

                    Logger.Log($"BGA video transcode timed out after {transcode_timeout_ms}ms (encoder {encoder}) for '{source}'.", level: LogLevel.Verbose);
                    return false;
                }

                string stderr = stderrTask.GetAwaiter().GetResult();
                stdoutTask.GetAwaiter().GetResult();

                if (process.ExitCode == 0)
                    return true;

                // Full ffmpeg output at Verbose (file log only — no notification spam across retries / both encoders);
                // startTranscode emits a single user-visible Important summary per failed source.
                Logger.Log($"BGA video transcode failed (ffmpeg exit {process.ExitCode}, encoder {encoder}) for '{source}':\n{tail(stderr)}", level: LogLevel.Verbose);
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // ffmpeg not found on PATH: remember so we stop trying for the rest of this session.
                ffmpegMissing = true;
                return false;
            }
            catch (Exception e)
            {
                Logger.Log($"BGA video ffmpeg invocation failed (encoder {encoder}): {e.Message}", level: LogLevel.Verbose);
                return false;
            }
        }

        // Keep only the tail of ffmpeg's (often long) stderr for the log line — the actual error is at the end.
        private static string tail(string? text, int maxChars = 800)
        {
            if (string.IsNullOrWhiteSpace(text))
                return "(no ffmpeg output)";

            text = text.Trim();
            return text.Length <= maxChars ? text : "…" + text[^maxChars..];
        }

        private string? resolveFfmpeg()
        {
            if (ffmpegMissing)
                return null;

            if (ffmpegResolved)
                return resolvedFfmpeg;

            ffmpegResolved = true;

            foreach (string candidate in ffmpegCandidates)
            {
                if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                {
                    resolvedFfmpeg = candidate;
                    return resolvedFfmpeg;
                }
            }

            // Fall back to the ffmpeg on PATH; runFfmpeg flags it missing on the first Win32Exception.
            resolvedFfmpeg = "ffmpeg";
            return resolvedFfmpeg;
        }

        private static string cacheKey(string sourceAbsolutePath)
        {
            var info = new FileInfo(sourceAbsolutePath);
            string raw = $"{sourceAbsolutePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|v{transcode_version}";
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }
    }
}
