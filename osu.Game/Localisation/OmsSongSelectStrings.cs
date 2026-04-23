// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class OmsSongSelectStrings
    {
        private const string prefix = @"osu.Game.Localisation.OmsSongSelect";

        /// <summary>
        /// "Difficulty Table"
        /// </summary>
        public static LocalisableString DifficultyTable => new TranslatableString(getKey(@"difficulty_table"), @"Difficulty Table");

        /// <summary>
        /// "Lamp Status"
        /// </summary>
        public static LocalisableString LampStatus => new TranslatableString(getKey(@"lamp_status"), @"Lamp Status");

        /// <summary>
        /// "Achievement Rate"
        /// </summary>
        public static LocalisableString AchievementRate => new TranslatableString(getKey(@"achievement_rate"), @"Achievement Rate");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
