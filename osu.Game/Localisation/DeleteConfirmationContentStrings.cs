// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class DeleteConfirmationContentStrings
    {
        private const string prefix = @"osu.Game.Localisation.DeleteConfirmationContent";

        /// <summary>
        /// "Are you sure you want to delete all internal beatmaps? This deletes OMS-managed beatmap files from disk."
        /// </summary>
        public static LocalisableString InternalBeatmaps => new TranslatableString(getKey(@"internal_beatmaps"), @"Are you sure you want to delete all internal beatmaps? This deletes OMS-managed beatmap files from disk.");

        /// <summary>
        /// "Are you sure you want to delete all external beatmaps? This removes OMS references only and will not delete files from external libraries."
        /// </summary>
        public static LocalisableString ExternalBeatmaps => new TranslatableString(getKey(@"external_beatmaps"), @"Are you sure you want to delete all external beatmaps? This removes OMS references only and will not delete files from external libraries.");

        /// <summary>
        /// "Are you sure you want to delete all beatmaps videos? This cannot be undone!"
        /// </summary>
        public static LocalisableString BeatmapVideos => new TranslatableString(getKey(@"beatmap_videos"), @"Are you sure you want to delete all beatmaps videos? This cannot be undone!");

        /// <summary>
        /// "Are you sure you want to reset all local beatmap offsets? This cannot be undone!"
        /// </summary>
        public static LocalisableString Offsets => new TranslatableString(getKey(@"offsets"), @"Are you sure you want to reset all local beatmap offsets? This cannot be undone!");

        /// <summary>
        /// "Are you sure you want to delete all skins? This cannot be undone!"
        /// </summary>
        public static LocalisableString Skins => new TranslatableString(getKey(@"skins"), @"Are you sure you want to delete all skins? This cannot be undone!");

        /// <summary>
        /// "Are you sure you want to delete all collections? This cannot be undone!"
        /// </summary>
        public static LocalisableString Collections => new TranslatableString(getKey(@"collections"), @"Are you sure you want to delete all collections? This cannot be undone!");

        /// <summary>
        /// "Are you sure you want to delete all scores? This cannot be undone!"
        /// </summary>
        public static LocalisableString Scores => new TranslatableString(getKey(@"scores"), @"Are you sure you want to delete all scores? This cannot be undone!");

        /// <summary>
        /// "Are you sure you want to delete all mod presets?"
        /// </summary>
        public static LocalisableString ModPresets => new TranslatableString(getKey(@"mod_presets"), @"Are you sure you want to delete all mod presets?");

        /// <summary>
        /// "Source: {0}"
        /// </summary>
        public static LocalisableString BeatmapSource(LocalisableString source) => new TranslatableString(getKey(@"beatmap_source"), @"Source: {0}", source);

        /// <summary>
        /// "Delete action: {0}"
        /// </summary>
        public static LocalisableString BeatmapDeleteAction(LocalisableString action) => new TranslatableString(getKey(@"beatmap_delete_action"), @"Delete action: {0}", action);

        /// <summary>
        /// "Storage path: {0}"
        /// </summary>
        public static LocalisableString BeatmapStoragePath(LocalisableString path) => new TranslatableString(getKey(@"beatmap_storage_path"), @"Storage path: {0}", path);

        /// <summary>
        /// "Internal import (OMS-managed storage)"
        /// </summary>
        public static LocalisableString InternalBeatmapSource => new TranslatableString(getKey(@"internal_beatmap_source"), @"Internal import (OMS-managed storage)");

        /// <summary>
        /// "External library reference"
        /// </summary>
        public static LocalisableString ExternalBeatmapSource => new TranslatableString(getKey(@"external_beatmap_source"), @"External library reference");

        /// <summary>
        /// "Delete OMS-managed beatmap files from disk."
        /// </summary>
        public static LocalisableString InternalBeatmapDeleteAction => new TranslatableString(getKey(@"internal_beatmap_delete_action"), @"Delete OMS-managed beatmap files from disk.");

        /// <summary>
        /// "Remove the OMS reference only. Files in the external library will not be deleted."
        /// </summary>
        public static LocalisableString ExternalBeatmapDeleteAction => new TranslatableString(getKey(@"external_beatmap_delete_action"), @"Remove the OMS reference only. Files in the external library will not be deleted.");

        /// <summary>
        /// "OMS internal file store"
        /// </summary>
        public static LocalisableString InternalBeatmapManagedStoragePath => new TranslatableString(getKey(@"internal_beatmap_managed_storage_path"), @"OMS internal file store");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
