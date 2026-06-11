// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Bms.Diagnostics
{
    /// <summary>
    /// Long-term gameplay stall diagnostics seam (P1-J J6): while a <see cref="BmsKeysoundStore"/> is hosted in real
    /// gameplay (converted-BMS mania OR native BMS), this measures the real update-thread frame time and correlates
    /// spikes with GC activity, allocation, and first-time keysound plays — so any future freeze report can be
    /// attributed directly from an exported performance.log (a blocking gen2 shows as <c>STALL+GEN2</c> on the same
    /// frame; a promotion storm shows as a gen0:gen1 ratio collapsing to 1:1) without re-instrumenting.
    /// </summary>
    /// <remarks>
    /// This probe pinned both 2026-06 converted-mania stutter causes (per-play sample-drawable rebuild churn → gen1
    /// promotion storm; mid-gameplay cold keysound decodes → ~220ms blocking gen2) and is intentionally retained.
    /// Per-frame cost is a handful of cheap counter reads (<see cref="GC.CollectionCount"/>,
    /// <see cref="GC.GetTotalAllocatedBytes"/> with <c>precise:false</c>, a <see cref="Stopwatch"/> read). A log line is
    /// emitted ONLY on a stall frame (>= <see cref="stall_threshold_ms"/>), a gen2 collection, or once per
    /// <see cref="heartbeat_interval_ms"/> heartbeat — so steady-state log noise is near zero. Stays silent when there
    /// is no <see cref="GameplayClockContainer"/> (e.g. headless test scenes). Added as an internal child by
    /// <see cref="BmsKeysoundStore"/>.
    /// </remarks>
    public partial class BmsGameplayStallDiagnostics : Drawable
    {
        private const double stall_threshold_ms = 40;
        private const double heartbeat_interval_ms = 2000;
        private const int max_logged_lines = 600;

        private readonly BmsKeysoundStore store;

        [Resolved(CanBeNull = true)]
        private GameplayClockContainer? gameplayClock { get; set; }

        private readonly Stopwatch sessionTimer = new Stopwatch();
        private bool active;
        private bool primed;

        private double lastFrameWallMs;
        private int loggedLines;

        // per-frame deltas baseline
        private int lastGen0, lastGen1, lastGen2;
        private long lastAllocated;
        private long lastColdPlays;

        // session baseline
        private int startGen0, startGen1, startGen2;
        private long startAllocated;
        private long startStorePlayAlloc;
        private int totalStalls;
        private double maxStallMs;

        // heartbeat window accumulators
        private double windowStartWallMs;
        private int windowFrames;
        private double windowSumFrameMs;
        private double windowMaxFrameMs;
        private int windowStartGen0, windowStartGen1, windowStartGen2;
        private long windowStartAllocated;
        private long windowStartColdPlays;
        private long windowStartStorePlayAlloc;
        private long windowStartPlays;
        private TimeSpan windowStartGcPause;

        public BmsGameplayStallDiagnostics(BmsKeysoundStore store)
        {
            this.store = store;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Only instrument real gameplay; a hosted store with no gameplay clock is a test scene.
            if (gameplayClock == null)
                return;

            active = true;

            startGen0 = lastGen0 = windowStartGen0 = GC.CollectionCount(0);
            startGen1 = lastGen1 = windowStartGen1 = GC.CollectionCount(1);
            startGen2 = lastGen2 = windowStartGen2 = GC.CollectionCount(2);
            startAllocated = lastAllocated = windowStartAllocated = GC.GetTotalAllocatedBytes();
            lastColdPlays = windowStartColdPlays = store.ColdKeysoundFirstPlayCount;
            startStorePlayAlloc = windowStartStorePlayAlloc = store.PlayPathAllocatedBytes;
            windowStartPlays = store.TotalKeysoundPlays;
            windowStartGcPause = GC.GetTotalPauseDuration();

            sessionTimer.Start();
            lastFrameWallMs = windowStartWallMs = 0;

            Logger.Log($"[stall-probe] armed: logs frames >= {stall_threshold_ms:N0}ms, every gen2 GC, and a {heartbeat_interval_ms / 1000:N0}s heartbeat.", LoggingTarget.Performance);
        }

        protected override void Update()
        {
            base.Update();

            if (!active)
                return;

            double nowMs = sessionTimer.Elapsed.TotalMilliseconds;
            double frameMs = nowMs - lastFrameWallMs;
            lastFrameWallMs = nowMs;

            int gen0 = GC.CollectionCount(0), gen1 = GC.CollectionCount(1), gen2 = GC.CollectionCount(2);
            long allocated = GC.GetTotalAllocatedBytes();
            long coldPlays = store.ColdKeysoundFirstPlayCount;

            if (!primed)
            {
                // The first frame after load carries the (large) load gap; snapshot baselines and skip it.
                primed = true;
                lastGen0 = gen0;
                lastGen1 = gen1;
                lastGen2 = gen2;
                lastAllocated = allocated;
                lastColdPlays = coldPlays;
                return;
            }

            bool paused = gameplayClock?.IsPaused.Value == true;

            // accumulate heartbeat window (skip while paused so idle frames don't pollute the trend)
            if (!paused)
            {
                windowFrames++;
                windowSumFrameMs += frameMs;
                if (frameMs > windowMaxFrameMs) windowMaxFrameMs = frameMs;
            }

            int d0 = gen0 - lastGen0, d1 = gen1 - lastGen1, d2 = gen2 - lastGen2;
            long dAllocKb = (allocated - lastAllocated) / 1024;
            long dCold = coldPlays - lastColdPlays;

            bool stall = frameMs >= stall_threshold_ms && !paused;

            if ((stall || d2 > 0) && loggedLines < max_logged_lines)
            {
                loggedLines++;
                double t = gameplayClock?.CurrentTime ?? 0;
                string kind = stall && d2 > 0 ? "STALL+GEN2" : stall ? "STALL" : "gen2";
                Logger.Log($"[stall-probe] {kind} t={t:N0}ms frame={frameMs:N0}ms gen0+={d0} gen1+={d1} gen2+={d2} alloc+={dAllocKb:N0}KB coldKeysounds+={dCold}", LoggingTarget.Performance);
            }

            if (stall)
            {
                totalStalls++;
                if (frameMs > maxStallMs) maxStallMs = frameMs;
            }

            if (nowMs - windowStartWallMs >= heartbeat_interval_ms && windowFrames > 0 && loggedLines < max_logged_lines)
            {
                loggedLines++;
                double windowSec = (nowMs - windowStartWallMs) / 1000;
                double avgFrame = windowSumFrameMs / windowFrames;
                double fps = windowFrames / windowSec;
                long windowAllocKb = (allocated - windowStartAllocated) / 1024;
                long windowStoreAllocKb = (store.PlayPathAllocatedBytes - windowStartStorePlayAlloc) / 1024;
                long storePct = windowAllocKb > 0 ? windowStoreAllocKb * 100 / windowAllocKb : 0;
                long windowPlays = store.TotalKeysoundPlays - windowStartPlays;
                double gcPauseMs = (GC.GetTotalPauseDuration() - windowStartGcPause).TotalMilliseconds;
                var gcInfo = GC.GetGCMemoryInfo();
                double t = gameplayClock?.CurrentTime ?? 0;

                Logger.Log(
                    $"[stall-probe] HB t={t:N0}ms fps≈{fps:N0} avgFrame={avgFrame:N1}ms maxFrame={windowMaxFrameMs:N0}ms " +
                    $"gen0+={gen0 - windowStartGen0} gen1+={gen1 - windowStartGen1} gen2+={gen2 - windowStartGen2} " +
                    $"gcPause={gcPauseMs:N1}ms promoted={gcInfo.PromotedBytes / 1024:N0}KB pinned={gcInfo.PinnedObjectsCount} " +
                    $"alloc={windowAllocKb:N0}KB ({windowAllocKb / windowSec:N0}KB/s) keysoundPlayAlloc={windowStoreAllocKb:N0}KB ({storePct}%) plays+={windowPlays} coldKeysounds+={coldPlays - windowStartColdPlays}",
                    LoggingTarget.Performance);

                windowStartWallMs = nowMs;
                windowFrames = 0;
                windowSumFrameMs = 0;
                windowMaxFrameMs = 0;
                windowStartGen0 = gen0;
                windowStartGen1 = gen1;
                windowStartGen2 = gen2;
                windowStartAllocated = allocated;
                windowStartColdPlays = coldPlays;
                windowStartStorePlayAlloc = store.PlayPathAllocatedBytes;
                windowStartPlays = store.TotalKeysoundPlays;
                windowStartGcPause = GC.GetTotalPauseDuration();
            }

            lastGen0 = gen0;
            lastGen1 = gen1;
            lastGen2 = gen2;
            lastAllocated = allocated;
            lastColdPlays = coldPlays;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (active && primed)
            {
                double totalAllocMb = (GC.GetTotalAllocatedBytes() - startAllocated) / (1024.0 * 1024.0);
                double storeAllocMb = (store.PlayPathAllocatedBytes - startStorePlayAlloc) / (1024.0 * 1024.0);
                long storePct = totalAllocMb > 0 ? (long)(storeAllocMb * 100 / totalAllocMb) : 0;

                Logger.Log(
                    $"[stall-probe] SESSION END: stalls(>={stall_threshold_ms:N0}ms)={totalStalls} maxStall={maxStallMs:N0}ms " +
                    $"gen0={GC.CollectionCount(0) - startGen0} gen1={GC.CollectionCount(1) - startGen1} gen2={GC.CollectionCount(2) - startGen2} " +
                    $"totalGcPause={GC.GetTotalPauseDuration().TotalMilliseconds:N0}ms (process lifetime) " +
                    $"totalAlloc={totalAllocMb:N1}MB keysoundPlayAlloc={storeAllocMb:N1}MB ({storePct}% of total) " +
                    $"distinctKeysoundSlots={store.DistinctKeysoundSlotsPlayed} totalKeysoundPlays={store.TotalKeysoundPlays} coldKeysoundLoads={store.ColdKeysoundFirstPlayCount}",
                    LoggingTarget.Performance);
            }

            base.Dispose(isDisposing);
        }
    }
}
