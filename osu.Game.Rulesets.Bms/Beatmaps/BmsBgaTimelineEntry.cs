// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using osu.Framework.Extensions;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// A single resolved BGA switch on the playable timeline: at <see cref="StartTime"/> the given <see cref="Layer"/>
    /// starts displaying <see cref="AssetFile"/> (an image, or a video when <see cref="IsVideo"/>). Built at conversion
    /// time from <see cref="BmsBgaEvent"/> + the chart's bitmap table, and carried on <see cref="BmsBeatmap.BgaTimeline"/>
    /// like <see cref="BmsBeatmap.Mines"/> / <see cref="BmsBeatmap.ScrollProfile"/> — rendering-only, never in
    /// <see cref="Game.Beatmaps.Beatmap{T}.HitObjects"/> and never fed to judgement/scoring (P1-L Phase 5).
    /// </summary>
    public readonly record struct BmsBgaTimelineEntry
    {
        /// <summary>Absolute gameplay time (ms) at which this BGA switch takes effect.</summary>
        public double StartTime { get; private init; }

        public BmsBgaLayer Layer { get; private init; }

        /// <summary>The resolved asset storage path (image or video file) this switch displays.</summary>
        public string AssetFile { get; private init; }

        /// <summary>Whether <see cref="AssetFile"/> is a video file (decoded via FFmpeg) rather than a still image.</summary>
        public bool IsVideo { get; private init; }

        public BmsBgaTimelineEntry(double startTime, BmsBgaLayer layer, string assetFile, bool isVideo)
        {
            if (!TryCreate(startTime, layer, assetFile, isVideo, out BmsBgaTimelineEntry entry))
                throw new ArgumentException("BGA asset files must be relative paths inside the beatmap directory.", nameof(assetFile));

            this = entry;
        }

        /// <summary>Admits an optional chart reference without rejecting the playable chart when its path is unsafe.</summary>
        public static bool TryCreate(double startTime, BmsBgaLayer layer, string? assetFile, bool isVideo, out BmsBgaTimelineEntry entry)
        {
            entry = default;
            if (string.IsNullOrWhiteSpace(assetFile) || FilesystemSanityCheckHelpers.IncursPathTraversalRisk(assetFile))
                return false;

            string normalisedFile = assetFile.ToStandardisedPath().Trim('/');
            if (string.IsNullOrWhiteSpace(normalisedFile))
                return false;

            entry = new BmsBgaTimelineEntry
            {
                StartTime = startTime,
                Layer = layer,
                AssetFile = normalisedFile,
                IsVideo = isVideo,
            };
            return true;
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
            ".mpg", ".mpeg", ".m1v", ".m2v", ".avi", ".wmv", ".mp4", ".m4v", ".webm", ".mov", ".flv", ".mkv",
        };
    }
}
