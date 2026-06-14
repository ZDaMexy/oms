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

        private readonly string? cacheDirectory;
        private readonly IReadOnlyList<string> ffmpegCandidates;
        private readonly Func<string, string, string, bool> runTranscode;

        // Per-instance state (a cache is created per gameplay). The on-disk cache file is the cross-session dedup.
        private readonly ConcurrentDictionary<string, byte> inProgress = new ConcurrentDictionary<string, byte>();
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
            // Dedup concurrent transcodes of the same destination (prewarm + per-event can both request it).
            if (!inProgress.TryAdd(destination, 0))
                return;

            Task.Run(() =>
            {
                bool success = false;
                string tmp = destination + ".tmp";

                try
                {
                    Directory.CreateDirectory(cacheDirectory!);

                    if (File.Exists(tmp))
                        File.Delete(tmp);

                    success = runTranscode(ffmpeg, source, tmp) && File.Exists(tmp);

                    if (success)
                    {
                        if (File.Exists(destination))
                            File.Delete(destination);

                        // Atomic publish: only a complete file ever appears at the destination path.
                        File.Move(tmp, destination);
                    }
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
                        failedDestinations.TryAdd(destination, 0);

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
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpeg,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };

            // Video-only (BGA carries no audio; the chart's keysounds are the audio); H.264 / yuv420p / mp4 is exactly
            // the format the framework decodes reliably.
            foreach (string argument in new[] { "-y", "-i", source, "-an", "-c:v", "libx264", "-pix_fmt", "yuv420p", "-movflags", "+faststart", destinationTmp })
                startInfo.ArgumentList.Add(argument);

            try
            {
                using var process = Process.Start(startInfo);

                if (process == null)
                    return false;

                // Drain pipes so a chatty ffmpeg never blocks on a full buffer.
                process.StandardError.ReadToEnd();
                process.StandardOutput.ReadToEnd();

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

                    return false;
                }

                return process.ExitCode == 0;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // ffmpeg not found on PATH: remember so we stop trying for the rest of this session.
                ffmpegMissing = true;
                return false;
            }
            catch (Exception e)
            {
                Logger.Log($"BGA video ffmpeg invocation failed: {e.Message}", level: LogLevel.Debug);
                return false;
            }
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
            string raw = $"{sourceAbsolutePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            byte[] hash = SHA1.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash);
        }
    }
}
