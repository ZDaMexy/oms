// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Bms.DifficultyTable;
using Realms;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    internal static class BmsChartFilterStatsBackfill
    {
        private static readonly BmsRuleset ruleset = new BmsRuleset();
        private static readonly ConcurrentDictionary<Guid, BmsChartFilterStats> cachedStats = new ConcurrentDictionary<Guid, BmsChartFilterStats>();
        private static readonly ConcurrentDictionary<Guid, object> syncRoots = new ConcurrentDictionary<Guid, object>();

        // Guards the one-time backfill bootstrap and the cache-updated subscriber list. The static guard used to be a
        // bare `if (beatmapManager != null) return;`, which let whichever caller ran first win and silently drop every
        // later caller's onCacheUpdated callback. In practice the beatmap-details graph (which passes no callback) often
        // initialises before the song-select filter, so the filter's "re-run matching once the cache fills" callback was
        // discarded and the composition filter never tightened after Phase 1 populated the cache.
        private static readonly object initLock = new object();
        private static readonly List<Action> cacheUpdatedCallbacks = new List<Action>();
        private static bool backfillStarted;

        private static BeatmapManager? beatmapManager;
        private static RealmAccess? realmAccess;
        private static Storage? gameStorage;
        private static INotificationOverlay? gameNotifications;

        internal static Func<BeatmapInfo, BmsChartFilterStats?>? TestResolver { get; private set; }

        public static void Initialise(BeatmapManager manager, RealmAccess realm, Storage? storage = null, INotificationOverlay? notifications = null, Action? onCacheUpdated = null)
        {
            bool startBackfill = false;

            lock (initLock)
            {
                // Register every distinct subscriber regardless of init order so no callback is lost to the race.
                if (onCacheUpdated != null && !cacheUpdatedCallbacks.Contains(onCacheUpdated))
                    cacheUpdatedCallbacks.Add(onCacheUpdated);

                if (!backfillStarted)
                {
                    backfillStarted = true;
                    beatmapManager = manager;
                    realmAccess = realm;
                    gameStorage = storage;
                    gameNotifications = notifications;
                    startBackfill = true;
                }
            }

            // A subscriber that registers after the backfill already started (or finished) must still get a single
            // refresh so it picks up whatever the cache already holds — otherwise its filter stays stuck fail-open.
            if (!startBackfill)
                onCacheUpdated?.Invoke();

            if (!startBackfill)
                return;

            // Run off the calling thread to avoid blocking the game update thread.
            Task.Run(() =>
            {
                // Phase 1: Pre-populate the in-memory cache from already-persisted Realm data.
                // Detached BeatmapInfo snapshots used by the carousel may have stale RulesetDataJson,
                // so GetChartFilterStats() on those snapshots is unreliable during filter loops.
                var missingIds = new List<Guid>();
                int cachedCount = 0;

                try
                {
                    realm.Run(r =>
                    {
                        int loadedCount = 0;

                        foreach (var info in EnumerateBmsBeatmaps(r))
                        {
                            var stats = info.Metadata.GetChartFilterStats();

                            if (stats != null)
                            {
                                if (cachedStats.TryAdd(info.ID, stats))
                                    cachedCount++;
                            }
                            else
                                missingIds.Add(info.ID);

                            // Notify periodically so the filter shows incremental results during load
                            // rather than waiting for the entire Phase 1 to complete. Throttled so the carousel
                            // does not get hammered with full re-filter passes on the update thread.
                            if (++loadedCount % 2000 == 0)
                                notifyCacheUpdatedThrottled();
                        }
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"[BmsCompositionFilter] backfill Phase 1 failed: {ex.Message}", LoggingTarget.Database, LogLevel.Important);
                }

                Logger.Log($"[BmsCompositionFilter] backfill Phase 1 done: {cachedCount} beatmaps already have persisted chart-filter-stats, {missingIds.Count} are missing (will be computed in Phase 2).",
                    LoggingTarget.Database, LogLevel.Verbose);

                // Phase 2: For beatmaps without persisted stats, compute them in background.
                // This runs once; subsequent startups will have stats in Realm and skip this phase.
                if (missingIds.Count == 0)
                {
                    notifyCacheUpdated();
                    return;
                }

                // One-time work over a (potentially huge) legacy library — surface it as a real progress notification
                // so the player understands what the background load is and that it will not recur, instead of just
                // feeling an unexplained drain.
                var progress = beginProgressNotification(missingIds.Count);

                int processed = 0;
                int computed = 0;

                // Computed stats are written back to Realm in batches (one transaction per write_batch_size) rather than
                // one tiny transaction per beatmap, to cut the per-item write churn that contends with the game's realm.
                var pendingWrites = new ConcurrentQueue<(Guid id, BmsChartFilterStats stats)>();

                Parallel.ForEach(missingIds, new ParallelOptions { MaxDegreeOfParallelism = 2 }, id =>
                {
                    if (!cachedStats.ContainsKey(id))
                    {
                        try
                        {
                            long r0 = Stopwatch.GetTimestamp();
                            var detached = realm.Run(r => r.Find<BeatmapInfo>(id)?.Detach());
                            Interlocked.Add(ref diagRealmReadTicks, Stopwatch.GetTimestamp() - r0);

                            var stats = detached != null ? sanitise(computeStats(detached)) : null;

                            if (stats != null)
                            {
                                cachedStats[id] = stats;
                                pendingWrites.Enqueue((id, stats));
                                Interlocked.Increment(ref computed);
                            }
                        }
                        catch { }
                    }

                    // Refresh the filter as stats stream in, but THROTTLED: each notify triggers a full carousel
                    // re-filter+sort+group over the whole library on the update thread (1-3s at 57k). Time-throttle it so
                    // the carousel rebuilds at most once every notify_throttle_seconds while the backfill grinds.
                    int done = Interlocked.Increment(ref processed);

                    notifyCacheUpdatedThrottled();
                    updateProgressNotification(progress, done, missingIds.Count);

                    if (pendingWrites.Count >= write_batch_size)
                        flushPendingWrites(realm, pendingWrites);

                    if (done % 1000 == 0)
                        logPhase2Timing(done, missingIds.Count, computed);
                });

                flushPendingWrites(realm, pendingWrites);
                notifyCacheUpdated();
                completeProgressNotification(progress, computed);
                Logger.Log($"[BmsCompositionFilter] backfill Phase 2 done: {computed}/{missingIds.Count} previously-missing beatmaps now have computed chart-filter-stats. Total cached = {cachedStats.Count}.",
                    LoggingTarget.Database, LogLevel.Verbose);
            });
        }

        // Number of computed stats accumulated before they are flushed to Realm in a single write transaction.
        private const int write_batch_size = 200;
        private static readonly object writeFlushLock = new object();

        private static void flushPendingWrites(RealmAccess realm, ConcurrentQueue<(Guid id, BmsChartFilterStats stats)> pendingWrites)
        {
            if (pendingWrites.IsEmpty)
                return;

            // Only one worker drains+writes at a time; the other keeps computing. Draining the whole queue into one
            // transaction is what turns ~50k tiny write transactions into a few hundred.
            lock (writeFlushLock)
            {
                var batch = new List<(Guid id, BmsChartFilterStats stats)>();

                while (pendingWrites.TryDequeue(out var item))
                    batch.Add(item);

                if (batch.Count == 0)
                    return;

                long w0 = Stopwatch.GetTimestamp();

                try
                {
                    realm.Write(r =>
                    {
                        foreach (var (id, stats) in batch)
                            r.Find<BeatmapInfo>(id)?.Metadata.SetChartFilterStats(stats);
                    });
                }
                catch
                {
                }

                Interlocked.Add(ref diagRealmWriteTicks, Stopwatch.GetTimestamp() - w0);
            }
        }

        private static ProgressNotification? beginProgressNotification(int total)
        {
            if (gameNotifications == null)
                return null;

            var notification = new ProgressNotification
            {
                Text = $"正在分析 BMS 谱面构成（一次性，用于启用选歌「谱面构成」筛选）… 0/{total}",
                State = ProgressNotificationState.Active,
            };

            gameNotifications.Post(notification);
            return notification;
        }

        private static void updateProgressNotification(ProgressNotification? notification, int done, int total)
        {
            // Update sparingly: the notification only needs a smooth-looking bar, not a per-item refresh.
            if (notification == null || (done % 100 != 0 && done != total))
                return;

            notification.Progress = total <= 0 ? 1 : (float)done / total;
            notification.Text = $"正在分析 BMS 谱面构成（一次性）… {done}/{total}";
        }

        private static void completeProgressNotification(ProgressNotification? notification, int computed)
        {
            if (notification == null)
                return;

            notification.Progress = 1;
            notification.Text = $"BMS 谱面构成分析完成，选歌「谱面构成」筛选已就绪（{computed} 张）。";
            notification.State = ProgressNotificationState.Completed;
        }

        /// <summary>
        /// Fans a cache-updated notification out to every registered subscriber. Invoked from the background backfill
        /// task; subscribers are responsible for marshalling back to their own thread (the song-select filter wraps the
        /// callback in <c>Scheduler.AddOnce</c>).
        /// </summary>
        private static void notifyCacheUpdated()
        {
            Action[] callbacks;

            lock (initLock)
                callbacks = cacheUpdatedCallbacks.ToArray();

            foreach (var callback in callbacks)
                callback();
        }

        // Each cache-updated notify makes the song-select carousel re-run a full filter+sort+group pass over the entire
        // library on the update thread (1-3s at 57k). The background backfill must therefore NOT notify on every chunk
        // or it starves the update thread and tanks FPS. This gate lets at most one periodic notify through every
        // notify_throttle_seconds; the phase-completion paths still call notifyCacheUpdated() directly so the final
        // result is never delayed.
        private const double notify_throttle_seconds = 20;
        private static long nextNotifyTicks;

        private static void notifyCacheUpdatedThrottled()
        {
            long now = DateTime.UtcNow.Ticks;
            long next = Interlocked.Read(ref nextNotifyTicks);

            if (now < next || Interlocked.CompareExchange(ref nextNotifyTicks, now + (long)(TimeSpan.TicksPerSecond * notify_throttle_seconds), next) != next)
                return;

            notifyCacheUpdated();
        }

        // Lightweight diagnostic counters fed from BmsFilterCriteria.Matches while a composition filter is active.
        // They answer the only question that distinguishes "logic bug" from "data gap": when the user has a composition
        // filter on, how many candidate beatmaps actually resolve stats (from cache vs the snapshot) vs come back null.
        // Time-throttled so it never spams the hot match loop (at most one summary every few seconds regardless of
        // library size).
        private static long diagCacheHits;
        private static long diagSnapshotOnlyHits;
        private static long diagNullStats;
        private static long diagNextLogTicks;

        internal static void RecordCompositionMatchDiagnostic(bool cacheHit, bool snapshotHit)
        {
            if (cacheHit)
                Interlocked.Increment(ref diagCacheHits);
            else if (snapshotHit)
                Interlocked.Increment(ref diagSnapshotOnlyHits);
            else
                Interlocked.Increment(ref diagNullStats);

            long now = DateTime.UtcNow.Ticks;
            long next = Interlocked.Read(ref diagNextLogTicks);

            // Emit at most one summary every 5 seconds; the winner of the CAS resets the counters and logs.
            if (now < next || Interlocked.CompareExchange(ref diagNextLogTicks, now + TimeSpan.TicksPerSecond * 5, next) != next)
                return;

            long cacheHits = Interlocked.Exchange(ref diagCacheHits, 0);
            long snapshotOnly = Interlocked.Exchange(ref diagSnapshotOnlyHits, 0);
            long nullStats = Interlocked.Exchange(ref diagNullStats, 0);

            Logger.Log($"[BmsCompositionFilter] composition-filter match sample: cache-hit={cacheHits}, snapshot-only={snapshotOnly}, null-stats(fail-open)={nullStats}. Cache size={cachedStats.Count}.",
                LoggingTarget.Database, LogLevel.Verbose);
        }

        /// <summary>
        /// Enumerates every BMS <see cref="BeatmapInfo"/> in the realm. The ruleset predicate MUST run in-memory
        /// (<see cref="System.Linq.Enumerable.AsEnumerable{T}"/> first): Realm's LINQ provider cannot translate the
        /// link-traversal <c>b.Ruleset.ShortName == ...</c> on the left of <c>==</c> and throws "The left-hand side of
        /// the Equal operator must be a direct access to a persisted property". That exception previously aborted the
        /// entire Phase 1, left the cache empty AND skipped Phase 2 (because <c>missingIds</c> stayed empty), so the
        /// composition filter silently fail-opened for the whole library. Mirrors the proven pattern in
        /// <see cref="DifficultyTable.BmsDifficultyTableManager"/>.
        /// </summary>
        internal static IEnumerable<BeatmapInfo> EnumerateBmsBeatmaps(Realm realm)
        {
            ArgumentNullException.ThrowIfNull(realm);

            return realm.All<BeatmapInfo>().AsEnumerable().Where(b => b.Ruleset.ShortName == BmsRuleset.SHORT_NAME);
        }

        /// <summary>
        /// Returns the cached stats for <paramref name="beatmapId"/>, or <see langword="null"/> if not cached.
        /// Does not perform any file I/O.
        /// </summary>
        public static BmsChartFilterStats? GetCachedStats(Guid beatmapId)
        {
            cachedStats.TryGetValue(beatmapId, out var stats);
            return stats;
        }

        public static BmsChartFilterStats? GetOrBackfill(BeatmapInfo beatmapInfo)
        {
            ArgumentNullException.ThrowIfNull(beatmapInfo);

            if (beatmapInfo.Metadata.GetChartFilterStats() is BmsChartFilterStats existing)
            {
                cachedStats[beatmapInfo.ID] = existing;
                return existing;
            }

            if (cachedStats.TryGetValue(beatmapInfo.ID, out var cached))
                return cached;

            object syncRoot = syncRoots.GetOrAdd(beatmapInfo.ID, _ => new object());

            lock (syncRoot)
            {
                if (beatmapInfo.Metadata.GetChartFilterStats() is BmsChartFilterStats persisted)
                {
                    cachedStats[beatmapInfo.ID] = persisted;
                    return persisted;
                }

                if (cachedStats.TryGetValue(beatmapInfo.ID, out cached))
                    return cached;

                var computed = sanitise(computeStats(beatmapInfo));

                if (computed == null)
                    return null;

                beatmapInfo.Metadata.SetChartFilterStats(computed);
                cachedStats[beatmapInfo.ID] = computed;

                long w0 = Stopwatch.GetTimestamp();

                try
                {
                    realmAccess?.Write(realm => realm.Find<BeatmapInfo>(beatmapInfo.ID)?.Metadata.SetChartFilterStats(computed));
                }
                catch
                {
                }

                Interlocked.Add(ref diagRealmWriteTicks, Stopwatch.GetTimestamp() - w0);

                return computed;
            }
        }

        internal static IDisposable UseTestResolver(Func<BeatmapInfo, BmsChartFilterStats?> resolver)
        {
            ArgumentNullException.ThrowIfNull(resolver);

            var previousResolver = TestResolver;
            clearCache();
            TestResolver = resolver;

            return new ActionDisposable(() =>
            {
                TestResolver = previousResolver;
                clearCache();
            });
        }

        private static BmsChartFilterStats? computeStats(BeatmapInfo beatmapInfo)
        {
            if (TestResolver != null)
                return TestResolver(beatmapInfo);

            if (beatmapInfo.Ruleset?.ShortName != BmsRuleset.SHORT_NAME || beatmapManager == null)
                return null;

            try
            {
                // Primary path: read the .bms straight off disk and count, with NO GetWorkingBeatmap. GetWorkingBeatmap
                // runs under a process-global `lock (workingCache)` (which the song-select/carousel UI also takes),
                // re-Detach()es, and in debug does a per-call Realm hash assert — so hammering it 57k times serialises
                // on that lock and stalls the update thread. Bypassing it is what actually unbinds the per-item rate
                // and stops the UI hitching during the backfill.
                long d0 = Stopwatch.GetTimestamp();
                var direct = computeStatsDirect(beatmapInfo);

                if (direct != null)
                {
                    Interlocked.Add(ref diagComputeTicks, Stopwatch.GetTimestamp() - d0);
                    Interlocked.Increment(ref diagLightweightHits);
                    return direct;
                }

                // Fallback: resolve through the working-beatmap cache. Covers any beatmap whose raw stream couldn't be
                // opened directly (e.g. a detached snapshot missing FilesystemStoragePath, or no game storage wired).
                long t0 = Stopwatch.GetTimestamp();
                var workingBeatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo);
                long t1 = Stopwatch.GetTimestamp();
                Interlocked.Add(ref diagWorkingBeatmapTicks, t1 - t0);

                var full = ComputeForWorkingBeatmap(workingBeatmap);
                Interlocked.Add(ref diagComputeTicks, Stopwatch.GetTimestamp() - t1);
                Interlocked.Increment(ref diagFallbackHits);
                return full;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Reads the chart's raw <c>.bms</c> straight from its filesystem folder (mirroring
        /// <c>WorkingBeatmapCache.createResourceProvider</c>: external sets use a <see cref="NativeStorage"/> at the
        /// absolute path, managed sets resolve under the game storage) and counts notes via decode-only. Returns
        /// <see langword="null"/> if the path / storage / stream can't be resolved so the caller can fall back.
        /// </summary>
        private static BmsChartFilterStats? computeStatsDirect(BeatmapInfo beatmapInfo)
        {
            if (string.IsNullOrEmpty(beatmapInfo.Path))
                return null;

            var beatmapSet = beatmapInfo.BeatmapSet;

            if (beatmapSet?.FilesystemStoragePath is not string filesystemStoragePath || string.IsNullOrEmpty(filesystemStoragePath))
                return null;

            try
            {
                Storage? chartStorage = beatmapSet.IsExternalFilesystemStorage
                    ? new NativeStorage(filesystemStoragePath)
                    : gameStorage?.GetStorageForDirectory(filesystemStoragePath);

                using var stream = chartStorage?.GetStream(beatmapInfo.Path);

                if (stream == null)
                    return null;

                return ComputeFromDecodedChart(BmsImportedBeatmapFactory.DecodeChart(stream, beatmapInfo.Path));
            }
            catch
            {
                // Any path/IO/decode failure falls through to the working-beatmap fallback.
                return null;
            }
        }

        // Per-item Phase 2 cost buckets (accumulated tick deltas across the parallel workers). Logged + reset every
        // 1000 items so a single backfill run reveals whether time goes to GetWorkingBeatmap construction, the
        // decode/count itself, or the per-item Realm read/write — and whether the lightweight decode is actually taken.
        private static long diagWorkingBeatmapTicks;
        private static long diagComputeTicks;
        private static long diagRealmReadTicks;
        private static long diagRealmWriteTicks;
        private static int diagLightweightHits;
        private static int diagFallbackHits;

        private static void logPhase2Timing(int done, int total, int computed)
        {
            long wb = Interlocked.Exchange(ref diagWorkingBeatmapTicks, 0);
            long cp = Interlocked.Exchange(ref diagComputeTicks, 0);
            long rr = Interlocked.Exchange(ref diagRealmReadTicks, 0);
            long rw = Interlocked.Exchange(ref diagRealmWriteTicks, 0);
            int lh = Interlocked.Exchange(ref diagLightweightHits, 0);
            int fh = Interlocked.Exchange(ref diagFallbackHits, 0);
            int windowItems = Math.Max(1, lh + fh);

            double avgMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency / windowItems;

            Logger.Log($"[BmsCompositionFilter] backfill Phase 2 progress: {done}/{total} processed, {computed} computed. " +
                       $"Per-item avg ms (last {lh + fh}): GetWorkingBeatmap={avgMs(wb):0.0}, decode/count={avgMs(cp):0.0}, realmRead={avgMs(rr):0.0}, realmWrite={avgMs(rw):0.0}; lightweight={lh}, fallback={fh}.",
                LoggingTarget.Database, LogLevel.Verbose);
        }

        /// <summary>
        /// Counts RC / LN / SCR directly from the decoded chart. This is provably identical to
        /// <see cref="BmsChartFilterStats.FromBeatmap"/> over the fully converted beatmap: note classification depends
        /// ONLY on <see cref="BmsObjectEvent.AutoPlay"/> (BGM objects become non-counted <c>BmsBgmEvent</c>s) and
        /// <see cref="BmsBeatmapConverter.IsScratchLane"/>, and the converter turns every non-autoplay object event /
        /// long-note event into exactly one <c>BmsHitObject</c> / <c>BmsHoldNote</c> with that same classification.
        /// </summary>
        internal static BmsChartFilterStats? ComputeFromDecodedChart(BmsDecodedChart decodedChart)
        {
            ArgumentNullException.ThrowIfNull(decodedChart);

            var keymode = decodedChart.BeatmapInfo.Keymode;
            int regular = 0;
            int longNote = 0;
            int scratch = 0;

            foreach (var objectEvent in decodedChart.ObjectEvents)
            {
                if (objectEvent.AutoPlay)
                    continue;

                if (BmsBeatmapConverter.IsScratchLane(keymode, objectEvent.Channel))
                    scratch++;
                else
                    regular++;
            }

            foreach (var longNoteEvent in decodedChart.LongNoteEvents)
            {
                if (BmsBeatmapConverter.IsScratchLane(keymode, longNoteEvent.LaneChannel))
                    scratch++;
                else
                    longNote++;
            }

            return sanitise(new BmsChartFilterStats
            {
                TotalPlayableObjectCount = regular + longNote + scratch,
                RegularNoteCount = regular,
                LongNoteCount = longNote,
                ScratchNoteCount = scratch,
            });
        }

        internal static BmsChartFilterStats? ComputeForWorkingBeatmap(IWorkingBeatmap workingBeatmap)
        {
            ArgumentNullException.ThrowIfNull(workingBeatmap);

            var sourceBeatmap = workingBeatmap.Beatmap;

            // New imports already copy converted BMS hitobjects onto the stored beatmap,
            // but legacy libraries may still require the playable conversion path.
            if (sourceBeatmap != null && sanitise(BmsChartFilterStats.FromBeatmap(sourceBeatmap)) is BmsChartFilterStats sourceStats)
                return sourceStats;

            return sanitise(BmsChartFilterStats.FromBeatmap(workingBeatmap.GetPlayableBeatmap(ruleset.RulesetInfo)));
        }

        private static BmsChartFilterStats? sanitise(BmsChartFilterStats? stats)
            => stats?.IsEmpty == true ? null : stats;

        private static void clearCache()
        {
            cachedStats.Clear();
            syncRoots.Clear();

            lock (initLock)
            {
                cacheUpdatedCallbacks.Clear();
                backfillStarted = false;
                beatmapManager = null;
                realmAccess = null;
                gameStorage = null;
                gameNotifications = null;
            }
        }

        private sealed class ActionDisposable : IDisposable
        {
            private readonly Action onDispose;

            public ActionDisposable(Action onDispose)
                => this.onDispose = onDispose;

            public void Dispose() => onDispose();
        }
    }
}
