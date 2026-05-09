// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FirstRunOverlayImportFromStableOmsStrings
    {
        private const string prefix = @"osu.Game.Localisation.FirstRunOverlayImportFromStableScreen";

        /// <summary>
        /// "导入"
        /// </summary>
        public static LocalisableString Header => new TranslatableString(getKey(@"header"), @"导入");

        /// <summary>
        /// "If you have osu!stable (non-lazer) installed, or already have a local BMS library in another directory, you can choose an external directory for direct traversal indexing. Indexing will not copy beatmap files or use additional storage space."
        /// </summary>
        public static LocalisableString Description => new TranslatableString(getKey(@"description"), @"If you have osu!stable (non-lazer) installed, or already have a local BMS library in another directory, you can choose an external directory for direct traversal indexing. Indexing will not copy beatmap files or use additional storage space.");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
