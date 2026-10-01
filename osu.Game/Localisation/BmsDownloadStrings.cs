// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BmsDownloadStrings
    {
        private static LocalisableString text(string key, string fallback, params object[] args) => new TranslatableString("osu.Game.Localisation.BmsDownload:" + key, fallback, args);

        public static LocalisableString Title => text("title", "Browse BMS");
        public static LocalisableString Description => text("description", "Find BMS charts, download and play.");
        public static LocalisableString Search => text("search", "Search title, artist or chart");
        public static LocalisableString SearchNow => text("search_now", "Search");
        public static LocalisableString AllTables => text("all_tables", "All difficulty tables");
        public static LocalisableString Source => text("source", "Download source");
        public static LocalisableString Table => text("table", "Difficulty table");
        public static LocalisableString TableLevel => text("table_level", "Table level");
        public static LocalisableString SelectTable => text("select_table", "Choose a table first");
        public static LocalisableString AllLevels => text("all_levels", "All levels");
        public static LocalisableString Ungraded => text("ungraded", "Ungraded");
        public static LocalisableString LevelsLoading => text("levels_loading", "Reading table levels…");
        public static LocalisableString LevelsFailed => text("levels_failed", "Could not read levels. Click Search to retry.");
        public static LocalisableString Download => text("download", "Download");
        public static LocalisableString Cancel => text("cancel", "Cancel");
        public static LocalisableString Retry => text("retry", "Retry");
        public static LocalisableString Open => text("open", "Open in song select");
        public static LocalisableString Expand => text("expand", "Charts");
        public static LocalisableString Queued => text("queued", "Queued");
        public static LocalisableString Downloading => text("downloading", "Downloading");
        public static LocalisableString DownloadProgress(int percent) => text("download_progress", "Downloading {0}%", percent);
        public static LocalisableString ChartsCount(int count) => text("charts_count", "{0} charts", count);
        public static LocalisableString AuthorLevel(string level) => text("author_level", "Author level {0}", level);
        public static LocalisableString Importing => text("importing", "Adding to library");
        public static LocalisableString Available => text("available", "Ready to play");
        public static LocalisableString Cancelled => text("cancelled", "Cancelled");
        public static LocalisableString Failed => text("failed", "Download or import failed. Please retry.");
        public static LocalisableString NoPackage => text("no_package", "No package available from this source");
        public static LocalisableString UnsupportedChart => text("unsupported_chart", "This chart format or key count is not supported yet");
        public static LocalisableString NoResults => text("no_results", "No matching charts found.");
        public static LocalisableString SearchFailed => text("search_failed", "Could not load this source. Please retry.");
        public static LocalisableString TableFailed => text("table_failed", "Difficulty tables are unavailable. You can still search.");
        public static LocalisableString BrowseMore => text("browse_more", "Load more");
        public static LocalisableString OutsideGameplay => text("outside_gameplay", "Open this chart after the current play finishes.");
        public static LocalisableString NoLongerAvailable => text("no_longer_available", "This chart is no longer available in your library.");
        public static LocalisableString ExternalPreview => text("external_preview", "View chart on website");
    }
}
