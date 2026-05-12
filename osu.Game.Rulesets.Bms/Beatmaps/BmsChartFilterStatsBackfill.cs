// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    internal static class BmsChartFilterStatsBackfill
    {
        private static readonly BmsRuleset ruleset = new BmsRuleset();
        private static readonly ConcurrentDictionary<Guid, BmsChartFilterStats> cachedStats = new ConcurrentDictionary<Guid, BmsChartFilterStats>();
        private static readonly ConcurrentDictionary<Guid, object> syncRoots = new ConcurrentDictionary<Guid, object>();

        private static BeatmapManager? beatmapManager;
        private static RealmAccess? realmAccess;

        internal static Func<BeatmapInfo, BmsChartFilterStats?>? TestResolver { get; private set; }

        public static void Initialise(BeatmapManager manager, RealmAccess realm, Action? onCacheUpdated = null)
        {
            if (beatmapManager != null)
                return;

            beatmapManager = manager;
            realmAccess = realm;

            // Run off the calling thread to avoid blocking the game update thread.
            Task.Run(() =>
            {
                // Phase 1: Pre-populate the in-memory cache from already-persisted Realm data.
                // Detached BeatmapInfo snapshots used by the carousel may have stale RulesetDataJson,
                // so GetChartFilterStats() on those snapshots is unreliable during filter loops.
                var missingIds = new List<Guid>();

                try
                {
                    realm.Run(r =>
                    {
                        int loadedCount = 0;

                        foreach (var info in r.All<BeatmapInfo>().Where(b => b.Ruleset.ShortName == BmsRuleset.SHORT_NAME).ToList())
                        {
                            var stats = info.Metadata.GetChartFilterStats();

                            if (stats != null)
                                cachedStats.TryAdd(info.ID, stats);
                            else
                                missingIds.Add(info.ID);

                            // Notify periodically so the filter shows incremental results during load
                            // rather than waiting for the entire Phase 1 to complete.
                            if (++loadedCount % 2000 == 0)
                                onCacheUpdated?.Invoke();
                        }
                    });
                }
                catch { }

                // Phase 2: For beatmaps without persisted stats, compute them in background.
                // This runs once; subsequent startups will have stats in Realm and skip this phase.
                if (missingIds.Count == 0)
                {
                    onCacheUpdated?.Invoke();
                    return;
                }

                int processed = 0;

                Parallel.ForEach(missingIds, new ParallelOptions { MaxDegreeOfParallelism = 2 }, id =>
                {
                    if (cachedStats.ContainsKey(id))
                        return;

                    try
                    {
                        var detached = realm.Run(r => r.Find<BeatmapInfo>(id)?.Detach());

                        if (detached != null)
                            GetOrBackfill(detached);
                    }
                    catch { }

                    // Notify periodically so the filter can refresh with newly computed stats.
                    if (Interlocked.Increment(ref processed) % 100 == 0)
                        onCacheUpdated?.Invoke();
                });

                onCacheUpdated?.Invoke();
            });
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

                try
                {
                    realmAccess?.Write(realm => realm.Find<BeatmapInfo>(beatmapInfo.ID)?.Metadata.SetChartFilterStats(computed));
                }
                catch
                {
                }

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
                return ComputeForWorkingBeatmap(beatmapManager.GetWorkingBeatmap(beatmapInfo));
            }
            catch
            {
                return null;
            }
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
