// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BmsInputStrings
    {
        private const string prefix = @"osu.Game.Localisation.BmsInput";

        /// <summary>
        /// "EFFECT / adjustment target (Start + EFFECT: Hi-Speed mode)"
        /// </summary>
        public static LocalisableString CycleScrollAdjustmentTarget => new TranslatableString(getKey(@"cycle_scroll_adjustment_target"), @"EFFECT / adjustment target (Start + EFFECT: Hi-Speed mode)");

        /// <summary>
        /// "Start (hold: speed / ready hold; double tap: covers)"
        /// </summary>
        public static LocalisableString PreStartHold => new TranslatableString(getKey(@"pre_start_hold"), @"Start (hold: speed / ready hold; double tap: covers)");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
