// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Screens.Play;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Drives the BGA video transcode during gameplay LOADING (P1-L Phase 5.2). Mounted directly in
    /// <c>DrawableBmsRuleset.Overlays</c> (not inside the skinnable <see cref="BmsBgaPanel"/>), so it is part of the
    /// tree <c>LoadComponentAsync</c> awaits: blocking its background load defers the player push until the legacy
    /// video is transcoded, so the BGA plays from the first frame instead of popping in seconds later. It is also the
    /// single owner of the session-level cache wipe, so the wipe always runs before any transcode is started (no race
    /// with a concurrent writer). On timeout it falls through to the pre-existing static-then-hot-swap path, so this is
    /// strictly an improvement over the previous fire-and-forget prewarm. Visual-only; never touches judgement/scoring.
    /// </summary>
    public partial class BmsBgaVideoPreloader : Component
    {
        // Upper bound on how long loading waits for the transcode. A finished transcode releases earlier; on timeout the
        // transcode keeps running in the background and the player hot-swaps it in mid-play (the existing fallback), so
        // gameplay is never blocked for longer than this.
        private const int transcode_wait_cap_ms = 8000;

        private readonly IReadOnlyList<BmsBgaTimelineEntry> timeline;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        [Resolved(CanBeNull = true)]
        private Storage? storage { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        // Shared with the loading screen's scanline indicator (cached by PlayerLoader). Optional: absent in isolated tests.
        [Resolved(CanBeNull = true)]
        private GameplayLoadProgress? loadProgress { get; set; }

        public BmsBgaVideoPreloader(IReadOnlyList<BmsBgaTimelineEntry> timeline)
        {
            this.timeline = timeline;
        }

        [BackgroundDependencyLoader]
        private void load(BmsRulesetConfigManager config)
        {
            if (!config.GetBindable<bool>(BmsRulesetSetting.BgaVideoTranscode).Value || storage == null)
                return;

            string cacheDirectory = storage.GetFullPath("bga-video-cache");

            // Session-level cache (R2/R3): wipe last session's transcodes exactly once per process, BEFORE starting any
            // transcode this session, so the cache never accumulates across sessions while within-session replays still
            // hit it. Runs here (the sole pre-gameplay transcode kicker) so it can never race a concurrent writer.
            BmsBgaVideoCache.ClearSessionCacheOnce(cacheDirectory);

            var sources = timeline.Where(entry => entry.IsVideo)
                                  .Select(entry => entry.AssetFile)
                                  .Distinct()
                                  .Where(BmsBgaVideoCache.RequiresTranscode)
                                  .Select(tryGetAbsolutePath)
                                  .Where(source => source != null)
                                  .Select(source => source!)
                                  .ToList();

            if (sources.Count == 0)
                return;

            var ffmpegCandidates = new List<string>
            {
                storage.GetFullPath("ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
            };

            // Forward the mean transcode fraction to the loading screen's scanline indicator (best-effort; null in tests).
            new BmsBgaVideoCache(cacheDirectory, ffmpegCandidates)
                .PrewarmAndWait(sources, transcode_wait_cap_ms, cancellation.Token, fraction => loadProgress?.Report(fraction));
        }

        // Resolves a BGA asset's absolute filesystem path (BMS is always filesystem-backed; external libraries store an
        // absolute set path, internal managed sets one relative to the data root) — same resolution the player uses.
        private string? tryGetAbsolutePath(string assetFile)
        {
            var set = workingBeatmap?.Value?.BeatmapSetInfo;

            if (set == null || storage == null || !FilesystemBeatmapLocation.TryGetSetDirectory(set, storage, out string directory))
                return null;

            return Path.Combine(directory, assetFile.Replace('/', Path.DirectorySeparatorChar));
        }

        protected override void Dispose(bool isDisposing)
        {
            cancellation.Cancel();
            cancellation.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
