// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Mania.Beatmaps;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// Retains the source music when mania conversion mods replace or remove judged objects.
    /// </summary>
    public class BmsConvertedManiaBeatmap : ManiaBeatmap
    {
        internal IReadOnlyList<BmsAutomaticKeysoundPlayback.Event> AutomaticKeysounds { get; private set; }
            = Array.Empty<BmsAutomaticKeysoundPlayback.Event>();

        public BmsConvertedManiaBeatmap(StageDefinition defaultStage)
            : base(defaultStage)
        {
        }

        // Called by the converter before returning this beatmap. The read-only event snapshot can be shared by
        // beatmap clones: each playback owns its cursor, and mods only change the judged objects.
        internal void CaptureAutomaticKeysounds()
            => AutomaticKeysounds = Array.AsReadOnly(BmsAutomaticKeysoundPlayback.CreateEvents(this));
    }
}
