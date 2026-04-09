// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Provides ruleset-specific beatmap loading for file formats not handled by the default legacy decoder pipeline.
    /// </summary>
    public interface ICustomBeatmapLoader
    {
        IEnumerable<string> FileExtensions { get; }

        bool CanLoad(BeatmapInfo beatmapInfo, string filename);

        IBeatmap Load(Stream stream, string filename, BeatmapInfo beatmapInfo);
    }
}
