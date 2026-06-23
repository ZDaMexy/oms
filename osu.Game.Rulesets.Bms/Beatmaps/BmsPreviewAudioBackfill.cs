// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Bms.Audio;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// One-time background migration that re-derives the song-select preview audio for already-imported BMS charts to
    /// match the current import policy: <see cref="osu.Game.Beatmaps.BeatmapMetadata.AudioFile"/> is set ONLY from an
    /// explicit <c>#PREVIEW</c> header (played from the start, <c>PreviewTime</c> = 0); everything else gets no preview.
    /// </summary>
    /// <remarks>
    /// Older imports used a <c>detectFullMusicFile</c> heuristic (any ≥1 MB unreferenced audio became the AudioFile), so
    /// existing libraries hold AudioFile values that no longer match the policy. This pass reads each candidate chart's
    /// <c>#PREVIEW</c> straight off disk and rewrites AudioFile / PreviewTime in Realm.
    /// <para>
    /// It runs ONCE: a completion marker file is written when done, so subsequent launches skip the whole pass (no
    /// enumerate, no decode) — that is what stops it from re-grinding (and dropping FPS) every time song-select opens.
    /// A crash mid-run leaves the marker absent, so the next launch resumes; the pass is idempotent and only touches
    /// charts whose stored AudioFile is non-empty.
    /// </para>
    /// </remarks>
    internal static class BmsPreviewAudioBackfill
    {
        private static readonly string[] audio_extensions = { ".mp3", ".ogg", ".flac", ".wav" };

        private const int write_batch_size = 200;

        // Bump the version suffix to force the migration to re-run after changing its logic.
        private const string marker_file = "bms-preview-audio-backfill-v1.marker";

        private static readonly object initLock = new object();
        private static bool started;

        public static void Initialise(RealmAccess realm, Storage? storage, INotificationOverlay? notifications)
        {
            ArgumentNullException.ThrowIfNull(realm);

            lock (initLock)
            {
                if (started)
                    return;

                started = true;
            }

            // Already migrated on a previous launch: skip entirely so it never recurs.
            if (storage?.Exists(marker_file) == true)
                return;

            Task.Run(() =>
            {
                run(realm, storage, notifications);

                try
                {
                    // Mark complete so the one-time migration does not run again.
                    if (storage != null)
                        using (storage.CreateFileSafely(marker_file)) { }
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BmsPreviewAudio] failed to write completion marker: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
                }
            });
        }

        /// <summary>
        /// Synchronous entry point for tests. Runs the migration on the calling thread (no marker handling) and returns
        /// the number of charts whose preview audio was rewritten.
        /// </summary>
        internal static int RunForTesting(RealmAccess realm, Storage? storage)
        {
            ArgumentNullException.ThrowIfNull(realm);
            return run(realm, storage, null);
        }

        private static int run(RealmAccess realm, Storage? gameStorage, INotificationOverlay? notifications)
        {
            try
            {
                // Single Realm read: snapshot every candidate (non-empty AudioFile) into plain structs so the decode loop
                // below touches no Realm at all — only the final batched writes do — minimising contention with the
                // song-select carousel on the update thread.
                var candidates = realm.Run(r =>
                    BmsChartFilterStatsBackfill.EnumerateBmsBeatmaps(r)
                                               .Where(b => !string.IsNullOrEmpty(b.Metadata.AudioFile) && !string.IsNullOrEmpty(b.Path))
                                               .Select(b => new Candidate(
                                                   b.ID,
                                                   b.Path!,
                                                   b.BeatmapSet?.FilesystemStoragePath ?? string.Empty,
                                                   b.BeatmapSet?.IsExternalFilesystemStorage ?? false,
                                                   b.Metadata.AudioFile ?? string.Empty,
                                                   b.Metadata.PreviewTime))
                                               .Where(c => !string.IsNullOrEmpty(c.FilesystemStoragePath))
                                               .ToList());

                if (candidates.Count == 0)
                    return 0;

                var progress = beginProgressNotification(notifications, candidates.Count);

                int corrected = 0;
                int processed = 0;
                var pendingWrites = new List<(Guid id, string audioFile, int previewTime)>();

                foreach (var candidate in candidates)
                {
                    try
                    {
                        if (tryResolveDesiredPreview(candidate, gameStorage, out string desiredAudioFile, out int desiredPreviewTime)
                            && !(string.Equals(candidate.AudioFile, desiredAudioFile, StringComparison.Ordinal) && candidate.PreviewTime == desiredPreviewTime))
                        {
                            pendingWrites.Add((candidate.Id, desiredAudioFile, desiredPreviewTime));
                            corrected++;

                            if (pendingWrites.Count >= write_batch_size)
                                flushPendingWrites(realm, pendingWrites);
                        }
                    }
                    catch
                    {
                        // Skip any single chart that fails to read/decode; the rest of the pass continues.
                    }

                    processed++;
                    updateProgressNotification(progress, processed, candidates.Count);

                    if (processed % 500 == 0)
                        Logger.Log($"[BmsPreviewAudio] backfill progress: {processed}/{candidates.Count} processed, {corrected} corrected.", LoggingTarget.Database, LogLevel.Verbose);
                }

                flushPendingWrites(realm, pendingWrites);
                completeProgressNotification(progress, corrected);

                Logger.Log($"[BmsPreviewAudio] backfill done: re-derived preview audio for {corrected} of {candidates.Count} BMS charts with a stored audio file.",
                    LoggingTarget.Database, LogLevel.Verbose);

                return corrected;
            }
            catch (Exception ex)
            {
                Logger.Log($"[BmsPreviewAudio] backfill failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
                return 0;
            }
        }

        /// <summary>
        /// Reads the chart's <c>#PREVIEW</c> header off disk and resolves it against the chart folder. Returns
        /// <see langword="false"/> only when the chart can't be read at all (so the caller leaves the stored value
        /// untouched). On success: <paramref name="desiredAudioFile"/> = resolved <c>#PREVIEW</c> file (or empty when the
        /// chart has no usable <c>#PREVIEW</c>), and <paramref name="desiredPreviewTime"/> = 0 when a preview exists else -1.
        /// </summary>
        private static bool tryResolveDesiredPreview(Candidate candidate, Storage? gameStorage, out string desiredAudioFile, out int desiredPreviewTime)
        {
            desiredAudioFile = string.Empty;
            desiredPreviewTime = -1;

            // Mirror WorkingBeatmapCache.createResourceProvider / BmsChartFilterStatsBackfill.computeStatsDirect: external
            // sets live at an absolute path, managed sets resolve under the game storage.
            Storage? chartStorage = candidate.IsExternal
                ? new NativeStorage(candidate.FilesystemStoragePath)
                : gameStorage?.GetStorageForDirectory(candidate.FilesystemStoragePath);

            if (chartStorage == null)
                return false;

            string? previewHeader;

            try
            {
                using var stream = chartStorage.GetStream(candidate.Path);

                if (stream == null)
                    return false;

                previewHeader = BmsImportedBeatmapFactory.DecodeChart(stream, candidate.Path).BeatmapInfo.PreviewFile;
            }
            catch
            {
                return false;
            }

            string? resolved = resolvePreviewFile(chartStorage, previewHeader);

            if (resolved != null)
            {
                desiredAudioFile = resolved;
                desiredPreviewTime = 0;
            }

            return true;
        }

        /// <summary>
        /// Storage-based mirror of <c>BmsFolderImporter.resolveReferencedFile</c> for the <c>#PREVIEW</c> audio file:
        /// resolves the header filename against the chart folder, trying the alternate audio extensions. Returns the
        /// resolved relative path or <see langword="null"/> if no matching audio file exists.
        /// </summary>
        private static string? resolvePreviewFile(Storage chartStorage, string? previewFilename)
        {
            if (!BmsKeysoundSampleInfo.TryNormaliseFilename(previewFilename, out string? normalised))
                return null;

            if (chartStorage.Exists(normalised))
                return normalised;

            string baseName = Path.ChangeExtension(normalised, null)?.ToStandardisedPath() ?? normalised;

            foreach (string ext in audio_extensions)
            {
                string candidate = baseName + ext;

                if (chartStorage.Exists(candidate))
                    return candidate;
            }

            return null;
        }

        private static void flushPendingWrites(RealmAccess realm, List<(Guid id, string audioFile, int previewTime)> pendingWrites)
        {
            if (pendingWrites.Count == 0)
                return;

            var batch = pendingWrites.ToList();
            pendingWrites.Clear();

            try
            {
                realm.Write(r =>
                {
                    foreach (var (id, audioFile, previewTime) in batch)
                    {
                        var info = r.Find<BeatmapInfo>(id);

                        if (info == null)
                            continue;

                        info.Metadata.AudioFile = audioFile;
                        info.Metadata.PreviewTime = previewTime;
                    }
                });
            }
            catch (Exception ex)
            {
                Logger.Log($"[BmsPreviewAudio] backfill write batch of {batch.Count} failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
            }
        }

        private static ProgressNotification? beginProgressNotification(INotificationOverlay? notifications, int total)
        {
            if (notifications == null)
                return null;

            var notification = new ProgressNotification
            {
                Text = $"正在更新 BMS 选歌预览（一次性，仅 #PREVIEW 谱面保留预览）… 0/{total}",
                State = ProgressNotificationState.Active,
            };

            notifications.Post(notification);
            return notification;
        }

        private static void updateProgressNotification(ProgressNotification? notification, int done, int total)
        {
            // Update sparingly: the notification only needs a smooth-looking bar, not a per-item refresh.
            if (notification == null || (done % 100 != 0 && done != total))
                return;

            notification.Progress = total <= 0 ? 1 : (float)done / total;
            notification.Text = $"正在更新 BMS 选歌预览（一次性）… {done}/{total}";
        }

        private static void completeProgressNotification(ProgressNotification? notification, int corrected)
        {
            if (notification == null)
                return;

            notification.Progress = 1;
            notification.Text = $"BMS 选歌预览更新完成（修正 {corrected} 张）。";
            notification.State = ProgressNotificationState.Completed;
        }

        private readonly record struct Candidate(Guid Id, string Path, string FilesystemStoragePath, bool IsExternal, string AudioFile, int PreviewTime);
    }
}
