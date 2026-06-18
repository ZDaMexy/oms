// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Screens.Select.Filter
{
    /// <summary>
    /// Controls whether song select groups difficulties under their beatmap set ("songs") or
    /// displays each difficulty as its own standalone panel.
    /// </summary>
    /// <remarks>
    /// Currently exposed only for the BMS ruleset. Other rulesets leave the corresponding
    /// <see cref="FilterCriteria.DisplayLevel"/> unset (<see langword="null"/>) and defer to the default
    /// heuristic in <see cref="BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether"/>.
    /// </remarks>
    public enum DisplayLevel
    {
        [LocalisableDescription(typeof(OmsSongSelectStrings), nameof(OmsSongSelectStrings.DisplayLevelSongs))]
        Songs,

        [LocalisableDescription(typeof(OmsSongSelectStrings), nameof(OmsSongSelectStrings.DisplayLevelDifficulties))]
        Difficulties,
    }
}
