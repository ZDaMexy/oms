// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    /// <summary>
    /// How beatmaps playable in the current ruleset only via conversion (in OMS: BMS charts under mania)
    /// are shown at song select. Replaces the former boolean "show converted beatmaps" toggle with a tri-state.
    /// </summary>
    public enum ConvertedBeatmapsDisplay
    {
        /// <summary>
        /// Only charts native to the current ruleset are shown; converts are hidden.
        /// </summary>
        [LocalisableDescription(typeof(OmsSongSelectStrings), nameof(OmsSongSelectStrings.ConvertedBeatmapsHidden))]
        Hidden,

        /// <summary>
        /// Native charts and converts are both shown.
        /// </summary>
        [LocalisableDescription(typeof(OmsSongSelectStrings), nameof(OmsSongSelectStrings.ConvertedBeatmapsShown))]
        Shown,

        /// <summary>
        /// Only converts are shown; charts native to the current ruleset are hidden. Meaningful only for a ruleset that
        /// can actually have converts (mania); otherwise it is treated as <see cref="Shown"/> so the list is never emptied.
        /// </summary>
        [LocalisableDescription(typeof(OmsSongSelectStrings), nameof(OmsSongSelectStrings.ConvertedBeatmapsConvertedOnly))]
        ConvertedOnly,
    }
}
