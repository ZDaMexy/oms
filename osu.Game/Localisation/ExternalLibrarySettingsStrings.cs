// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ExternalLibrarySettingsStrings
    {
        private const string prefix = @"osu.Game.Localisation.ExternalLibrarySettings";

        /// <summary>
        /// "External Library"
        /// </summary>
        public static LocalisableString ExternalLibraryHeader => new TranslatableString(getKey(@"external_library_header"), @"External Library");

        /// <summary>
        /// "Select an external beatmap library folder"
        /// </summary>
        public static LocalisableString SelectExternalLibraryFolder => new TranslatableString(getKey(@"select_external_library_folder"), @"Select an external beatmap library folder");

        /// <summary>
        /// "Add BMS Library Folder"
        /// </summary>
        public static LocalisableString AddBmsLibraryFolder => new TranslatableString(getKey(@"add_bms_library_folder"), @"Add BMS Library Folder");

        /// <summary>
        /// "Add Mania Library Folder"
        /// </summary>
        public static LocalisableString AddManiaLibraryFolder => new TranslatableString(getKey(@"add_mania_library_folder"), @"Add Mania Library Folder");

        /// <summary>
        /// "Scan All Libraries"
        /// </summary>
        public static LocalisableString ScanAllLibraries => new TranslatableString(getKey(@"scan_all_libraries"), @"Scan All Libraries");

        /// <summary>
        /// "Added {0} library: {1}"
        /// </summary>
        public static LocalisableString AddedLibrary(string type, string path) => new TranslatableString(getKey(@"added_library"), @"Added {0} library: {1}", type, path);

        /// <summary>
        /// "This folder is already registered."
        /// </summary>
        public static LocalisableString FolderAlreadyRegistered => new TranslatableString(getKey(@"folder_already_registered"), @"This folder is already registered.");

        /// <summary>
        /// "Directory not found: {0}"
        /// </summary>
        public static LocalisableString DirectoryNotFound(string path) => new TranslatableString(getKey(@"directory_not_found"), @"Directory not found: {0}", path);

        /// <summary>
        /// "Scanning external libraries..."
        /// </summary>
        public static LocalisableString ScanningExternalLibraries => new TranslatableString(getKey(@"scanning_external_libraries"), @"Scanning external libraries...");

        /// <summary>
        /// "Scanning: {0} ({1}/{2})"
        /// </summary>
        public static LocalisableString ScanningProgress(string name, int index, int total) => new TranslatableString(getKey(@"scanning_progress"), @"Scanning: {0} ({1}/{2})", name, index, total);

        /// <summary>
        /// "Scan complete: {0} imported, {1} skipped, {2} errors"
        /// </summary>
        public static LocalisableString ScanComplete(int imported, int skipped, int errors) => new TranslatableString(getKey(@"scan_complete"), @"Scan complete: {0} imported, {1} skipped, {2} errors", imported, skipped, errors);

        /// <summary>
        /// "Remove"
        /// </summary>
        public static LocalisableString Remove => new TranslatableString(getKey(@"remove"), @"Remove");

        /// <summary>
        /// "(disabled)"
        /// </summary>
        public static LocalisableString DisabledLabel => new TranslatableString(getKey(@"disabled_label"), @"(disabled)");

        /// <summary>
        /// "— path not found"
        /// </summary>
        public static LocalisableString PathNotFound => new TranslatableString(getKey(@"path_not_found"), @"— path not found");

        /// <summary>
        /// "— last scan: {0}"
        /// </summary>
        public static LocalisableString LastScan(string time) => new TranslatableString(getKey(@"last_scan"), @"— last scan: {0}", time);

        /// <summary>
        /// "— never scanned"
        /// </summary>
        public static LocalisableString NeverScanned => new TranslatableString(getKey(@"never_scanned"), @"— never scanned");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
