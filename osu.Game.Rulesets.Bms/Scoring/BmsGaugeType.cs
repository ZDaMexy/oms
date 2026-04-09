// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Scoring
{
    public enum BmsGaugeType
    {
        AssistEasy,
        Easy,
        Normal,
        Hard,
        ExHard,
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
