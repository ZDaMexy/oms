// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Configuration
{
    public enum AutomaticOffsetStyle
    {
        [LocalisableDescription(typeof(AudioSettingsStrings), nameof(AudioSettingsStrings.AutomaticOffsetOff))]
        Off,

        [LocalisableDescription(typeof(AudioSettingsStrings), nameof(AudioSettingsStrings.AutomaticOffsetLazer))]
        Lazer,

        [LocalisableDescription(typeof(AudioSettingsStrings), nameof(AudioSettingsStrings.AutomaticOffsetBeatoraja))]
        Beatoraja
    }
}
