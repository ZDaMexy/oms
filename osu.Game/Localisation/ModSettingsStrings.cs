// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ModSettingsStrings
    {
        private const string prefix = @"osu.Game.Localisation.ModSettings";

        /// <summary>
        /// "Seed"
        /// </summary>
        public static LocalisableString Seed => new TranslatableString(getKey(@"seed"), @"Seed");

        /// <summary>
        /// "Use a custom seed instead of a random one"
        /// </summary>
        public static LocalisableString SeedDescription => new TranslatableString(getKey(@"seed_description"), @"Use a custom seed instead of a random one");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}