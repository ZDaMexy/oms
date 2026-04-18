// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public enum BmsGaugeType
    {
        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.GaugeTypeAssistEasy))]
        AssistEasy,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.GaugeTypeEasy))]
        Easy,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.GaugeTypeNormal))]
        Normal,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.GaugeTypeHard))]
        Hard,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.GaugeTypeExHard))]
        ExHard,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.GaugeTypeHazard))]
        Hazard,
    }

    public static class BmsGaugeTypeExtensions
    {
        public static string GetDisplayName(this BmsGaugeType gaugeType)
            => gaugeType switch
            {
                BmsGaugeType.AssistEasy => "ASSIST EASY",
                BmsGaugeType.Easy => "EASY",
                BmsGaugeType.Normal => "NORMAL",
                BmsGaugeType.Hard => "HARD",
                BmsGaugeType.ExHard => "EX-HARD",
                BmsGaugeType.Hazard => "HAZARD",
                _ => gaugeType.ToString(),
            };

        internal static BmsClearLamp ToClearLamp(this BmsGaugeType gaugeType)
            => gaugeType switch
            {
                BmsGaugeType.AssistEasy => BmsClearLamp.AssistEasyClear,
                BmsGaugeType.Easy => BmsClearLamp.EasyClear,
                BmsGaugeType.Normal => BmsClearLamp.NormalClear,
                BmsGaugeType.Hard => BmsClearLamp.HardClear,
                BmsGaugeType.ExHard => BmsClearLamp.ExHardClear,
                BmsGaugeType.Hazard => BmsClearLamp.HazardClear,
                _ => BmsClearLamp.NormalClear,
            };
    }
}
