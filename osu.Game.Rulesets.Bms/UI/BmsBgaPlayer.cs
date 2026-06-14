// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Video;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Configuration;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Timeline-driven BGA (background image / animation) presenter (P1-L Phase 5). Composites the BMS base / layer /
    /// layer2 channels and shows the poor layer on miss per <see cref="BmsPoorBgaMode"/>. Image frames are swapped on a
    /// shared <see cref="Sprite"/>; video frames are decoded by osu!framework's FFmpeg <see cref="Video"/> and synced to
    /// the gameplay clock (the same pattern as <c>DrawableStoryboardVideo</c>). All content letterboxes via
    /// <see cref="FillMode.Fit"/>. This is rendering-only and never feeds judgement / scoring.
    /// </summary>
    public partial class BmsBgaPlayer : CompositeDrawable
    {
        /// <summary>How long the poor (miss) layer stays visible after a miss.</summary>
        private const double poor_display_duration = 600;

        private readonly IReadOnlyList<BmsBgaTimelineEntry> timeline;
        private readonly BmsPoorBgaMode poorMode;

        private readonly BmsBgaLayerDisplay baseLayer;
        private readonly BmsBgaLayerDisplay overlayLayer;
        private readonly BmsBgaLayerDisplay overlay2Layer;
        private readonly BmsBgaLayerDisplay poorLayer;
        private readonly BmsBgaLayerDisplay[] normalLayers;
        private readonly bool hasPoorLayer;

        private double? poorTriggeredAt;

        private TextureStore textureStore = null!;
        private WorkingBeatmapFileStore? fileStore;
        private BmsBgaVideoCache? videoCache;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        [Resolved(CanBeNull = true)]
        private Storage? storage { get; set; }

        public BmsBgaPlayer(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode)
        {
            this.timeline = timeline;
            this.poorMode = poorMode;

            RelativeSizeAxes = Axes.Both;
            Masking = true;

            // Black backdrop so letterbox bars (and the pre-first-event gap) read as black, matching real BGA panels.
            baseLayer = new BmsBgaLayerDisplay(entriesFor(BmsBgaLayer.Base));
            overlayLayer = new BmsBgaLayerDisplay(entriesFor(BmsBgaLayer.Layer)) { Blending = BlendingParameters.Additive };
            overlay2Layer = new BmsBgaLayerDisplay(entriesFor(BmsBgaLayer.Layer2)) { Blending = BlendingParameters.Additive };
            poorLayer = new BmsBgaLayerDisplay(entriesFor(BmsBgaLayer.Poor)) { Alpha = 0 };

            normalLayers = new[] { baseLayer, overlayLayer, overlay2Layer };
            hasPoorLayer = poorMode != BmsPoorBgaMode.Undisplayed && entriesFor(BmsBgaLayer.Poor).Length > 0;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                baseLayer,
                overlayLayer,
                overlay2Layer,
                poorLayer,
            };
        }

        [BackgroundDependencyLoader]
        private void load(GameHost host, BmsRulesetConfigManager config)
        {
            var working = workingBeatmap?.Value;

            if (working != null)
            {
                fileStore = new WorkingBeatmapFileStore(working);
                textureStore = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(fileStore), false, scaleAdjust: 1);
            }
            else
            {
                // No working beatmap (e.g. isolated test): an empty store so lookups simply return null (no BGA shown).
                textureStore = new TextureStore(host.Renderer, host.CreateTextureLoaderStore(new ResourceStore<byte[]>()), false, scaleAdjust: 1);
            }

            setUpVideoCache(config);

            // Base layer falls back to the chart's static background if its video can't decode (e.g. a legacy MPEG-1
            // .mpg that FFmpeg can't open and that has no transcode available), so the BGA region shows the stage art
            // instead of black. Overlay / poor layers simply hide on fault.
            var faultFallback = working?.GetBackground();

            baseLayer.SetResolvers(getTexture, createVideo, faultFallback);
            overlayLayer.SetResolvers(getTexture, createVideo, null);
            overlay2Layer.SetResolvers(getTexture, createVideo, null);
            poorLayer.SetResolvers(getTexture, createVideo, null);
        }

        // Builds the external-ffmpeg transcode cache (P1-L Phase 5.1) and prewarms transcodes for legacy video assets
        // so they have a chance to finish before their event fires (the 5s pre-start gives extra lead time).
        private void setUpVideoCache(BmsRulesetConfigManager config)
        {
            bool transcodeEnabled = config.GetBindable<bool>(BmsRulesetSetting.BgaVideoTranscode).Value;

            if (!transcodeEnabled || storage == null)
                return;

            var ffmpegCandidates = new List<string>
            {
                storage.GetFullPath("ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
            };

            videoCache = new BmsBgaVideoCache(storage.GetFullPath("bga-video-cache"), ffmpegCandidates);

            foreach (string asset in timeline.Where(entry => entry.IsVideo).Select(entry => entry.AssetFile).Distinct())
            {
                if (!BmsBgaVideoCache.RequiresTranscode(asset))
                    continue;

                string? source = tryGetAbsolutePath(asset);

                if (source != null)
                    videoCache.Resolve(source);
            }
        }

        /// <summary>Flags a miss so the poor BGA layer flashes per <see cref="BmsPoorBgaMode"/>.</summary>
        public void NotifyMiss()
        {
            if (!hasPoorLayer)
                return;

            poorTriggeredAt = Time.Current;
        }

        protected override void Update()
        {
            base.Update();

            bool poorActive = hasPoorLayer
                              && poorTriggeredAt is double triggered
                              && Time.Current >= triggered
                              && Time.Current < triggered + poor_display_duration;

            poorLayer.Alpha = poorActive ? 1 : 0;

            // In Default poor mode the poor layer replaces the normal layers while active; in Overlay mode it sits on top.
            float normalAlpha = poorActive && poorMode == BmsPoorBgaMode.Default ? 0 : 1;

            foreach (var layer in normalLayers)
                layer.Alpha = normalAlpha;
        }

        private Texture? getTexture(string assetFile)
        {
            try
            {
                return textureStore.Get(assetFile);
            }
            catch (Exception e)
            {
                Logger.Log($"BGA image '{assetFile}' failed to load: {e.Message}", level: LogLevel.Debug);
                return null;
            }
        }

        // Creates the FFmpeg video for an asset, returning (video, pending). Prefers opening by absolute FILE PATH:
        // FFmpeg probes/seeks a real file far better than the .NET-stream AVIO. For legacy containers the framework
        // can't open (MPEG-1 .mpg etc.) the transcode cache yields a playable .mp4 once ready; while transcoding it
        // reports pending (the caller keeps the static fallback and retries). When transcoding is off/unavailable it
        // still attempts a direct open (legacy will fault -> static fallback).
        private (Video? video, bool pending) createVideo(string assetFile)
        {
            try
            {
                string? path = tryGetAbsolutePath(assetFile);

                if (videoCache != null && !string.IsNullOrEmpty(path))
                {
                    var resolved = videoCache.Resolve(path);

                    switch (resolved.State)
                    {
                        case BmsBgaVideoCache.VideoSourceState.Ready:
                            return (configureVideo(new Video(resolved.Path!, startAtCurrentTime: false)), false);

                        case BmsBgaVideoCache.VideoSourceState.Pending:
                            return (null, true);

                        // Unavailable: fall through to a direct open below.
                    }
                }

                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    return (configureVideo(new Video(path, startAtCurrentTime: false)), false);

                var stream = fileStore?.GetStream(assetFile);

                if (stream != null)
                    return (configureVideo(new Video(stream, startAtCurrentTime: false)), false);
            }
            catch (Exception e)
            {
                Logger.Log($"BGA video '{assetFile}' could not be created: {e.Message}", level: LogLevel.Debug);
            }

            return (null, false);
        }

        private static Video configureVideo(Video video)
        {
            video.RelativeSizeAxes = Axes.Both;
            video.FillMode = FillMode.Fit;
            video.Anchor = Anchor.Centre;
            video.Origin = Anchor.Centre;
            video.Alpha = 0;
            video.Loop = false;
            return video;
        }

        // Resolves a BGA asset's absolute filesystem path. BMS is always filesystem-backed: external libraries store an
        // absolute FilesystemStoragePath, internal managed sets store one relative to the data storage root.
        private string? tryGetAbsolutePath(string assetFile)
        {
            var set = workingBeatmap?.Value?.BeatmapSetInfo;

            if (set == null || string.IsNullOrEmpty(set.FilesystemStoragePath))
                return null;

            string relative = Path.Combine(set.FilesystemStoragePath, assetFile.Replace('/', Path.DirectorySeparatorChar));

            return set.IsExternalFilesystemStorage ? relative : storage?.GetFullPath(relative);
        }

        private BmsBgaTimelineEntry[] entriesFor(BmsBgaLayer layer)
            => timeline.Where(entry => entry.Layer == layer).OrderBy(entry => entry.StartTime).ToArray();

        protected override void Dispose(bool isDisposing)
        {
            textureStore?.Dispose();
            fileStore?.Dispose();
            base.Dispose(isDisposing);
        }

        /// <summary>
        /// Returns the index of the latest entry whose <see cref="BmsBgaTimelineEntry.StartTime"/> is &lt;= <paramref name="time"/>,
        /// or -1 if none is active yet. Entries must be time-sorted; among equal times the last (later source) wins.
        /// </summary>
        public static int GetActiveIndex(IReadOnlyList<BmsBgaTimelineEntry> sortedEntries, double time)
        {
            int lo = 0;
            int hi = sortedEntries.Count - 1;
            int result = -1;

            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;

                if (sortedEntries[mid].StartTime <= time)
                {
                    result = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return result;
        }

        /// <summary>
        /// Presents a single BGA layer's entry sequence: a reused <see cref="Sprite"/> for image frames plus a lazily
        /// created <see cref="Video"/> per distinct video file. Picks the active entry each frame and keeps the active
        /// video's playback position synced to the gameplay clock (seek / retry safe).
        /// </summary>
        private partial class BmsBgaLayerDisplay : CompositeDrawable
        {
            // How often a pending (transcoding) video is re-checked so it can hot-swap in mid-play once ready.
            private const double video_retry_interval = 1000;

            private readonly BmsBgaTimelineEntry[] entries;
            private readonly Sprite imageSprite;
            private readonly Dictionary<string, Video?> videosByFile = new Dictionary<string, Video?>();

            private Func<string, Texture?> getTexture = _ => null;
            private Func<string, (Video? video, bool pending)> createVideo = _ => (null, false);
            private Texture? faultFallbackTexture;

            private int activeIndex = -1;
            private Video? activeVideo;
            private double lastVideoRetryTime;

            public BmsBgaLayerDisplay(BmsBgaTimelineEntry[] entries)
            {
                this.entries = entries;

                RelativeSizeAxes = Axes.Both;

                InternalChild = imageSprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0,
                };
            }

            public void SetResolvers(Func<string, Texture?> textureResolver, Func<string, (Video? video, bool pending)> videoFactory, Texture? faultFallback)
            {
                getTexture = textureResolver;
                createVideo = videoFactory;
                faultFallbackTexture = faultFallback;
            }

            protected override void Update()
            {
                base.Update();

                if (entries.Length == 0)
                    return;

                double time = Time.Current;
                int index = GetActiveIndex(entries, time);

                if (index != activeIndex)
                {
                    activeIndex = index;
                    applyActiveEntry();
                }

                if (activeIndex < 0)
                    return;

                var entry = entries[activeIndex];

                if (!entry.IsVideo)
                    return;

                if (activeVideo == null)
                {
                    // Pending transcode / no playable video yet: keep the static fallback and periodically retry so a
                    // finished transcode can hot-swap in mid-play.
                    if (time - lastVideoRetryTime > video_retry_interval)
                    {
                        lastVideoRetryTime = time;
                        tryAttachVideo(entry);

                        if (activeVideo != null)
                            imageSprite.Alpha = 0;
                    }
                }
                else if (activeVideo.IsFaulted)
                {
                    // The video can't decode (e.g. a legacy container with no transcode available): drop it for good
                    // and show the static fallback instead of a black panel.
                    dropFaultedVideo(entry.AssetFile);
                    showFallback();
                }
                else
                {
                    imageSprite.Alpha = 0;
                    activeVideo.PlaybackPosition = time - entry.StartTime;
                }
            }

            private void applyActiveEntry()
            {
                hideActiveVideo();

                if (activeIndex < 0)
                {
                    imageSprite.Alpha = 0;
                    return;
                }

                var entry = entries[activeIndex];

                if (entry.IsVideo)
                {
                    imageSprite.Alpha = 0;
                    lastVideoRetryTime = Time.Current;
                    tryAttachVideo(entry);

                    // Nothing to play yet (pending / unavailable): show the static fallback as a placeholder.
                    if (activeVideo == null)
                        showFallback();
                }
                else
                {
                    imageSprite.Texture = getTexture(entry.AssetFile);
                    imageSprite.Alpha = imageSprite.Texture != null ? 1 : 0;
                }
            }

            private void tryAttachVideo(BmsBgaTimelineEntry entry)
            {
                var video = getOrCreateVideo(entry.AssetFile);

                if (!ReferenceEquals(video, activeVideo))
                {
                    hideActiveVideo();
                    activeVideo = video;
                }

                if (activeVideo != null)
                    activeVideo.Alpha = 1;
            }

            private void showFallback()
            {
                imageSprite.Texture = faultFallbackTexture;
                imageSprite.Alpha = faultFallbackTexture != null ? 1 : 0;
            }

            private void hideActiveVideo()
            {
                if (activeVideo != null)
                    activeVideo.Alpha = 0;

                activeVideo = null;
            }

            // Marks a faulted asset as permanently unplayable (never recreated) and removes its drawable.
            private void dropFaultedVideo(string assetFile)
            {
                var faulted = activeVideo;
                activeVideo = null;
                videosByFile[assetFile] = null;

                if (faulted != null)
                    RemoveInternal(faulted, true);
            }

            private Video? getOrCreateVideo(string assetFile)
            {
                if (videosByFile.TryGetValue(assetFile, out var existing))
                    return existing;

                var (video, pending) = createVideo(assetFile);

                if (video != null)
                {
                    AddInternal(video);
                    videosByFile[assetFile] = video;
                    return video;
                }

                // Permanent failure (unavailable) caches null so it is never retried; a pending transcode stays
                // uncached so the next retry re-resolves and can pick up the finished file.
                if (!pending)
                    videosByFile[assetFile] = null;

                return null;
            }
        }

        /// <summary>
        /// Bridges a <see cref="WorkingBeatmap"/> to an <see cref="IResourceStore{T}"/> so BGA assets load straight from
        /// the chart folder (BMS is always filesystem-backed; <see cref="WorkingBeatmap.GetStream"/> resolves the
        /// relative asset name). Never touches the hash-backed files store and never converts to <c>.osz</c>.
        /// </summary>
        private class WorkingBeatmapFileStore : IResourceStore<byte[]>
        {
            private readonly WorkingBeatmap working;

            public WorkingBeatmapFileStore(WorkingBeatmap working) => this.working = working;

            public byte[] Get(string name)
            {
                using var stream = GetStream(name);

                if (stream == null)
                    return null!;

                using var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }

            public async Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            {
                var stream = GetStream(name);

                if (stream == null)
                    return null!;

                using (stream)
                {
                    using var memory = new MemoryStream();
                    await stream.CopyToAsync(memory, cancellationToken).ConfigureAwait(false);
                    return memory.ToArray();
                }
            }

            public Stream? GetStream(string name)
            {
                try
                {
                    return working.GetStream(name);
                }
                catch (Exception e)
                {
                    Logger.Log($"BGA asset '{name}' not resolvable from working beatmap: {e.Message}", level: LogLevel.Debug);
                    return null;
                }
            }

            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

            public void Dispose()
            {
            }
        }
    }
}
