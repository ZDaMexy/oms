// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A single resolved BGA switch on the playable timeline: at <see cref="StartTime"/> the given <see cref="Layer"/>
    /// starts displaying <see cref="AssetFile"/> (an image, or a video when <see cref="IsVideo"/>). Built at conversion
    /// time from <see cref="BmsBgaEvent"/> + the chart's bitmap table, and carried on <see cref="BmsBeatmap.BgaTimeline"/>
    /// like <see cref="BmsBeatmap.Mines"/> / <see cref="BmsBeatmap.ScrollProfile"/> — rendering-only, never in
    /// <see cref="osu.Game.Beatmaps.Beatmap{T}.HitObjects"/> and never fed to judgement/scoring (P1-L Phase 5).
    /// </summary>
    public readonly record struct BmsBgaTimelineEntry
    {
        /// <summary>Absolute gameplay time (ms) at which this BGA switch takes effect.</summary>
        public double StartTime { get; }

        public BmsBgaLayer Layer { get; }

        /// <summary>The resolved asset storage path (image or video file) this switch displays.</summary>
        public string AssetFile { get; }

        /// <summary>Whether <see cref="AssetFile"/> is a video file (decoded via FFmpeg) rather than a still image.</summary>
        public bool IsVideo { get; }

        public BmsBgaTimelineEntry(double startTime, BmsBgaLayer layer, string assetFile, bool isVideo)
        {
            if (string.IsNullOrWhiteSpace(assetFile))
                throw new ArgumentException(@"BGA asset file must not be empty.", nameof(assetFile));

            StartTime = startTime;
            Layer = layer;
            AssetFile = assetFile;
            IsVideo = isVideo;
        }

        /// <summary>
        /// Classifies a BGA bitmap reference as a video by its file extension. Covers the formats BMS charts ship
        /// (legacy <c>.mpg/.avi/.wmv</c> through modern <c>.mp4/.webm</c>); anything else is treated as a still image.
        /// </summary>
        public static bool IsVideoAsset(string? assetFile)
        {
            if (string.IsNullOrWhiteSpace(assetFile))
                return false;

            string extension = Path.GetExtension(assetFile);

            foreach (string videoExtension in video_extensions)
            {
                if (extension.Equals(videoExtension, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private static readonly string[] video_extensions =
        {
            ".mpg", ".mpeg", ".avi", ".wmv", ".mp4", ".m4v", ".webm", ".mov", ".flv", ".mkv",
        };
    }
}
