// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class PlayerLoaderOmsStrings
    {
        private const string prefix = @"osu.Game.Localisation.PlayerLoader";

        /// <summary>
        /// "The gameplay screen could not be prepared. Please try again."
        /// </summary>
        public static LocalisableString GameplaySkinLayoutPreparationFailed => new TranslatableString(
            getKey(@"gameplay_skin_layout_preparation_failed"), @"The gameplay screen could not be prepared. Please try again.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
