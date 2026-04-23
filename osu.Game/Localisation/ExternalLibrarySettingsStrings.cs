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
        /// "Internal Library"
        /// </summary>
        public static LocalisableString InternalLibraryHeader => new TranslatableString(getKey(@"internal_library_header"), @"Internal Library");

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
        /// "Scan External Libraries"
        /// </summary>
        public static LocalisableString ScanExternalLibraries => new TranslatableString(getKey(@"scan_external_libraries"), @"Scan External Libraries");

        /// <summary>
        /// "Scan External Libraries (Rebuild)"
        /// </summary>
        public static LocalisableString ScanExternalLibrariesRebuild => new TranslatableString(getKey(@"scan_external_libraries_rebuild"), @"Scan External Libraries (Rebuild)");

        /// <summary>
        /// "Scan External Libraries (Incremental)"
        /// </summary>
        public static LocalisableString ScanExternalLibrariesIncremental => new TranslatableString(getKey(@"scan_external_libraries_incremental"), @"Scan External Libraries (Incremental)");

        /// <summary>
        /// "Scan Internal Libraries"
        /// </summary>
        public static LocalisableString ScanInternalLibraries => new TranslatableString(getKey(@"scan_internal_libraries"), @"Scan Internal Libraries");

        /// <summary>
        /// "Scan Internal Libraries (Rebuild)"
        /// </summary>
        public static LocalisableString ScanInternalLibrariesRebuild => new TranslatableString(getKey(@"scan_internal_libraries_rebuild"), @"Scan Internal Libraries (Rebuild)");

        /// <summary>
        /// "Scan Internal Libraries (Incremental)"
        /// </summary>
        public static LocalisableString ScanInternalLibrariesIncremental => new TranslatableString(getKey(@"scan_internal_libraries_incremental"), @"Scan Internal Libraries (Incremental)");

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
        /// "Scanning external libraries (rebuild)..."
        /// </summary>
        public static LocalisableString ScanningExternalLibrariesRebuild => new TranslatableString(getKey(@"scanning_external_libraries_rebuild"), @"Scanning external libraries (rebuild)...");

        /// <summary>
        /// "Scanning external libraries (incremental)..."
        /// </summary>
        public static LocalisableString ScanningExternalLibrariesIncremental => new TranslatableString(getKey(@"scanning_external_libraries_incremental"), @"Scanning external libraries (incremental)...");

        /// <summary>
        /// "Scanning internal libraries..."
        /// </summary>
        public static LocalisableString ScanningInternalLibraries => new TranslatableString(getKey(@"scanning_internal_libraries"), @"Scanning internal libraries...");

        /// <summary>
        /// "Scanning internal libraries (rebuild)..."
        /// </summary>
        public static LocalisableString ScanningInternalLibrariesRebuild => new TranslatableString(getKey(@"scanning_internal_libraries_rebuild"), @"Scanning internal libraries (rebuild)...");

        /// <summary>
        /// "Scanning internal libraries (incremental)..."
        /// </summary>
        public static LocalisableString ScanningInternalLibrariesIncremental => new TranslatableString(getKey(@"scanning_internal_libraries_incremental"), @"Scanning internal libraries (incremental)...");

        /// <summary>
        /// "Scanning: {0} ({1}/{2})"
        /// </summary>
        public static LocalisableString ScanningProgress(string name, int index, int total) => new TranslatableString(getKey(@"scanning_progress"), @"Scanning: {0} ({1}/{2})", name, index, total);

        /// <summary>
        /// "Scanning {0} ({1}/{2}) · {3} folders · {4} indexed, {5} skipped, {6} errors"
        /// </summary>
        public static LocalisableString ScanningRootProgress(string name, int index, int total, int directories, int imported, int skipped, int errors)
            => new TranslatableString(getKey(@"scanning_root_progress"), @"Scanning {0} ({1}/{2}) · {3} folders · {4} indexed, {5} skipped, {6} errors", name, index, total, directories, imported, skipped, errors);

        /// <summary>
        /// "Scanning {0} ({1}/{2}) · {3} ({4}/{5}) · {6} indexed, {7} skipped, {8} errors"
        /// </summary>
        public static LocalisableString ScanningDirectoryProgress(string rootName, int rootIndex, int totalRoots, string directoryName, int directoryIndex, int totalDirectories,
                                                                  int imported, int skipped, int errors)
            => new TranslatableString(getKey(@"scanning_directory_progress"), @"Scanning {0} ({1}/{2}) · {3} ({4}/{5}) · {6} indexed, {7} skipped, {8} errors",
                rootName, rootIndex, totalRoots, directoryName, directoryIndex, totalDirectories, imported, skipped, errors);

        /// <summary>
        /// "Scan complete: {0} indexed, {1} skipped, {2} errors"
        /// </summary>
        public static LocalisableString ScanComplete(int imported, int skipped, int errors) => new TranslatableString(getKey(@"scan_complete"), @"Scan complete: {0} indexed, {1} skipped, {2} errors", imported, skipped, errors);

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
