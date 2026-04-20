// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.ComponentModel;

namespace osu.Game.Rulesets.Bms.Configuration
{
    public enum BmsHiSpeedMode
    {
        [Description("Normal Hi-Speed")]
        Normal,

        [Description("Floating Hi-Speed")]
        Floating,

        [Description("Classic Hi-Speed")]
        Classic,
    }

    public static class BmsHiSpeedModeExtensions
    {
        public static string GetShortLabel(this BmsHiSpeedMode mode)
            => mode switch
            {
                BmsHiSpeedMode.Normal => @"NHS",
                BmsHiSpeedMode.Floating => @"FHS",
                BmsHiSpeedMode.Classic => @"CHS",
                _ => @"HS",
            };

        public static string FormatValue(this BmsHiSpeedMode mode, double value)
            => mode switch
            {
                BmsHiSpeedMode.Normal => $@"{value:0.0}",
                BmsHiSpeedMode.Floating => $@"{value:0.00}",
                BmsHiSpeedMode.Classic => $@"{value:0.00}",
                _ => $@"{value:0.0}",
            };

        public static double GetAdjustmentStep(this BmsHiSpeedMode mode)
            => mode switch
            {
                BmsHiSpeedMode.Normal => 1,
                BmsHiSpeedMode.Floating => 0.25,
                BmsHiSpeedMode.Classic => 0.25,
                _ => 1,
            };
    }
}
