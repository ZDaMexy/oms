// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;

namespace osu.Game.Online.IR
{
    /// <summary>Public pages on the configured OMS origin. Web authentication stays in the browser.</summary>
    public static class OmsWebsite
    {
        public static bool HasChart(IBeatmapInfo beatmap) => beatmap.Ruleset.ShortName is "bms" or "mania"
                                                           && beatmap.MD5Hash.Length == 32 && beatmap.MD5Hash.All(Uri.IsHexDigit);

        public static string? Chart(string origin, IBeatmapInfo beatmap) => HasChart(beatmap)
            ? ChartBoard(origin, beatmap.MD5Hash, beatmap.Ruleset.ShortName) : null;

        public static string? ChartBoard(string origin, string md5, string ruleset, string? mode = null, string? condition = null,
                                        IEnumerable<string>? sources = null, string? group = null, int page = 1)
        {
            var query = new List<(string, string)>
            {
                ("ruleset", ruleset), ("md5", md5.ToLowerInvariant()),
            };
            if (mode != null) query.Add(("mode", mode));
            if (condition != null) query.Add(("condition", condition));
            if (sources != null) query.Add(("sources", string.Join(",", sources.OrderBy(source => source, StringComparer.Ordinal))));
            if (group != null) query.Add(("group", group));
            if (page != 1) query.Add(("page", page.ToString(CultureInfo.InvariantCulture)));
            return link(origin, "beatmaps", query);
        }

        public static string? Profile(string origin, long userId, string ruleset, string? keymode = null) => userId > 0
            ? link(origin, "users/" + userId.ToString(CultureInfo.InvariantCulture), scope(ruleset, keymode)) : null;

        public static string? Rankings(string origin, string ruleset, string? keymode = null) => link(origin, "rankings", scope(ruleset, keymode));

        /// <summary>Read persisted selection metadata without decoding a chart just to construct a link.</summary>
        public static (string Ruleset, string? Keymode) CurrentScope(IBeatmapInfo beatmap, RulesetInfo ruleset)
            => (ruleset.ShortName, HasChart(beatmap) ? ruleset.CreateInstance()?.GetOmsWebsiteKeymode(beatmap) : null);

        private static IEnumerable<(string, string)> scope(string ruleset, string? keymode)
        {
            yield return ("ruleset", ruleset);
            if (keymode != null) yield return ("keymode", keymode);
        }

        private static string? link(string origin, string path, IEnumerable<(string Key, string Value)> query) => origin.Length == 0 ? null
            : new Uri(new Uri(origin), path).AbsoluteUri + "?" + string.Join("&", query.Select(value => Uri.EscapeDataString(value.Key) + "=" + Uri.EscapeDataString(value.Value)));
    }
}
