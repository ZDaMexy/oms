// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BeatmapDownloadStrings
    {
        private static LocalisableString text(string key, string fallback) => new TranslatableString("osu.Game.Localisation.BeatmapDownload:" + key, fallback);

        public static LocalisableString Title => text("title", "Browse beatmaps");
        public static LocalisableString Description => text("description", "Find BMS and osu!mania charts, download and play.");
        public static LocalisableString Mode => text("mode", "Game mode");
        public static LocalisableString Keys => text("keys", "Keys");
        public static LocalisableString Stars => text("stars", "Star rating");
        public static LocalisableString Category => text("category", "Status");
        public static LocalisableString AllKeys => text("all_keys", "All keys");
        public static LocalisableString AllStars => text("all_stars", "All star ratings");
        public static LocalisableString AllCategories => text("all_categories", "Any status");
        public static LocalisableString SearchMania => text("search_mania", "Search by title, artist, mapper or beatmapset ID");
        public static LocalisableString Ranked => text("ranked", "Ranked");
        public static LocalisableString Approved => text("approved", "Approved");
        public static LocalisableString Qualified => text("qualified", "Qualified");
        public static LocalisableString Loved => text("loved", "Loved");
        public static LocalisableString Pending => text("pending", "Pending");
        public static LocalisableString Graveyard => text("graveyard", "Graveyard");
        public static LocalisableString Sayobot => text("sayobot", "Sayobot mirror");
    }
}
