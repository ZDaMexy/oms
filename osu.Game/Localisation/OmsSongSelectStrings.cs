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
        /// "External Library"
        /// </summary>
        public static LocalisableString ExternalLibrary => new TranslatableString(getKey(@"external_library"), @"External Library");

        /// <summary>
        /// "Internal Library"
        /// </summary>
        public static LocalisableString InternalLibrary => new TranslatableString(getKey(@"internal_library"), @"Internal Library");

        /// <summary>
        /// "Unmapped External Library"
        /// </summary>
        public static LocalisableString UnmappedExternalLibrary => new TranslatableString(getKey(@"unmapped_external_library"), @"Unmapped External Library");

        /// <summary>
        /// "Lamp Status"
        /// </summary>
        public static LocalisableString LampStatus => new TranslatableString(getKey(@"lamp_status"), @"Lamp Status");

        /// <summary>
        /// "Achievement Rate"
        /// </summary>
        public static LocalisableString AchievementRate => new TranslatableString(getKey(@"achievement_rate"), @"Achievement Rate");

        /// <summary>
        /// "Display Level"
        /// </summary>
        public static LocalisableString DisplayLevel => new TranslatableString(getKey(@"display_level"), @"Display Level");

        /// <summary>
        /// "Songs → Difficulties"
        /// </summary>
        public static LocalisableString DisplayLevelSongs => new TranslatableString(getKey(@"display_level_songs"), @"Songs → Difficulties");

        /// <summary>
        /// "Difficulties"
        /// </summary>
        public static LocalisableString DisplayLevelDifficulties => new TranslatableString(getKey(@"display_level_difficulties"), @"Difficulties");

        /// <summary>
        /// "Hidden"
        /// </summary>
        public static LocalisableString ConvertedBeatmapsHidden => new TranslatableString(getKey(@"converted_beatmaps_hidden"), @"Hidden");

        /// <summary>
        /// "Shown"
        /// </summary>
        public static LocalisableString ConvertedBeatmapsShown => new TranslatableString(getKey(@"converted_beatmaps_shown"), @"Shown");

        /// <summary>
        /// "Converts only"
        /// </summary>
        public static LocalisableString ConvertedBeatmapsConvertedOnly => new TranslatableString(getKey(@"converted_beatmaps_converted_only"), @"Converts only");

        /// <summary>
        /// "The difficulty table grouping only shows BMS converts."
        /// </summary>
        public static LocalisableString DifficultyTableManiaOnlyConverts => new TranslatableString(getKey(@"difficulty_table_mania_only_converts"), @"The difficulty table grouping only shows BMS converts.");

        /// <summary>
        /// "Enable showing converts"
        /// </summary>
        public static LocalisableString DifficultyTableEnableConverts => new TranslatableString(getKey(@"difficulty_table_enable_converts"), @"Enable showing converts");

        /// <summary>
        /// "Import BMS charts to browse them by difficulty table here."
        /// </summary>
        public static LocalisableString DifficultyTableNoConvertsHint => new TranslatableString(getKey(@"difficulty_table_no_converts_hint"), @"Import BMS charts to browse them by difficulty table here.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
