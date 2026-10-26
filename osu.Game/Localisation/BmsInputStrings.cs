// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BmsInputStrings
    {
        private const string prefix = @"osu.Game.Localisation.BmsInput";

        /// <summary>
        /// "Gameplay: Cover / Hi-Speed (press to choose Sudden, Hidden or Lift for mouse-wheel adjustment; hold Start and press to switch between Normal and Floating Hi-Speed)"
        /// </summary>
        public static LocalisableString CycleScrollAdjustmentTarget => new TranslatableString(getKey(@"cycle_scroll_adjustment_target"), @"Gameplay: Cover / Hi-Speed (press to choose Sudden, Hidden or Lift for mouse-wheel adjustment; hold Start and press to switch between Normal and Floating Hi-Speed)");

        /// <summary>
        /// "Start (hold: speed / ready hold; double tap: covers)"
        /// </summary>
        public static LocalisableString PreStartHold => new TranslatableString(getKey(@"pre_start_hold"), @"Start (hold: speed / ready hold; double tap: covers)");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
