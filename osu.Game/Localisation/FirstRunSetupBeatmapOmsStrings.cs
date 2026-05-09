// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class FirstRunSetupBeatmapOmsStrings
    {
        private const string prefix = @"osu.Game.Localisation.FirstRunSetupBeatmapScreen";

        /// <summary>
        /// "获取谱面"
        /// </summary>
        public static LocalisableString Header => new TranslatableString(getKey(@"header"), @"获取谱面");

        /// <summary>
        /// "您可以从以下地址直接获取 mania 或 bms 谱面。"
        /// </summary>
        public static LocalisableString Description => new TranslatableString(getKey(@"description"), @"您可以从以下地址直接获取 mania 或 bms 谱面。");

        /// <summary>
        /// "mania"
        /// </summary>
        public static LocalisableString ManiaHeader => new TranslatableString(getKey(@"mania_header"), @"mania");

        /// <summary>
        /// "打开 osu! 官方谱面站"
        /// </summary>
        public static LocalisableString ManiaOfficialButton => new TranslatableString(getKey(@"mania_official_button"), @"打开 osu! 官方谱面站");

        /// <summary>
        /// "Open Sayobot beatmap mirror"
        /// </summary>
        public static LocalisableString ManiaSayobotButton => new TranslatableString(getKey(@"mania_sayobot_button"), @"Open Sayobot beatmap mirror");

        /// <summary>
        /// "bms"
        /// </summary>
        public static LocalisableString BmsHeader => new TranslatableString(getKey(@"bms_header"), @"bms");

        /// <summary>
        /// "Open hakura bms"
        /// </summary>
        public static LocalisableString BmsDownloadButton => new TranslatableString(getKey(@"bms_download_button"), @"Open hakura bms");

        /// <summary>
        /// "下载到的谱面，请将 mania 或 bms 谱面文件解压至 oms 游戏目录的 /chartmania 或 /chartbms 下，后续请在 设置-维护-内部谱库-扫描内部谱库 选项进行扫描添加。"
        /// </summary>
        public static LocalisableString ImportInstructions => new TranslatableString(getKey(@"import_instructions"), @"下载到的谱面，请将 mania 或 bms 谱面文件解压至 oms 游戏目录的 /chartmania 或 /chartbms 下，后续请在 设置-维护-内部谱库-扫描内部谱库 选项进行扫描添加。");

        /// <summary>
        /// "The above pages link to external websites and are unrelated to the OMS project. If you manage one of the above sites and do not wish it to appear here, please contact the developers promptly. We sincerely apologise."
        /// </summary>
        public static LocalisableString ExternalLinkDisclaimer => new TranslatableString(getKey(@"external_link_disclaimer"), @"The above pages link to external websites and are unrelated to the OMS project. If you manage one of the above sites and do not wish it to appear here, please contact the developers promptly. We sincerely apologise.");

        /// <summary>
        /// "目前游戏内已有 {0} 张谱面！"
        /// </summary>
        public static LocalisableString CurrentlyLoadedBeatmaps(int beatmaps) => new TranslatableString(getKey(@"currently_loaded_beatmaps"), @"目前游戏内已有 {0} 张谱面！", beatmaps);

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}