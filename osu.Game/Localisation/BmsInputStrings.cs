// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BmsInputStrings
    {
        private const string prefix = @"osu.Game.Localisation.BmsInput";

        /// <summary>
        /// "Cycle scroll adjustment target"
        /// </summary>
        public static LocalisableString CycleScrollAdjustmentTarget => new TranslatableString(getKey(@"cycle_scroll_adjustment_target"), @"Cycle scroll adjustment target");

        /// <summary>
        /// "Pre-start hold"
        /// </summary>
        public static LocalisableString PreStartHold => new TranslatableString(getKey(@"pre_start_hold"), @"Pre-start hold");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
