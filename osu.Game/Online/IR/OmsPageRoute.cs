// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Game.Online.Sayobot;

namespace osu.Game.Online.IR
{
    public enum OmsPageKind { Home, Rankings, Profile, Chart, Board, Library, Account, Community, Topic, News, Article, Help }

    /// <summary>OMS identities and the selected public scope, independent of ppy online IDs.</summary>
    public sealed record OmsPageRoute
    {
        public OmsPageKind Kind { get; init; }
        public long UserId { get; init; }
        public long PostId { get; init; }
        public string? Slug { get; init; }
        public string? Md5 { get; init; }
        public string? Title { get; init; }
        public string Ruleset { get; init; } = "bms";
        public string? Keymode { get; init; }
        public string? Sources { get; init; }
        public string? Condition { get; init; }
        public string? Metric { get; init; }
        public int Page { get; init; } = 1;
        public int ScopePage { get; init; } = 1;
        public bool Recent { get; init; }
        public float BoardScroll { get; init; }
        public string? HelpAnchor { get; init; }
        public string? Group { get; init; }
        public string Mode { get; init; } = "reference";
        public string? Category { get; init; }
        public string Query { get; init; } = string.Empty;

        // Reading state of the native browser. These source-specific filters are not website query parameters.
        public string? BrowserTable { get; init; }
        public string? BrowserLevel { get; init; }
        public int BrowserKeys { get; init; }
        public int BrowserStars { get; init; }
        public SayobotCategory BrowserCategory { get; init; }

        public string WebsiteUrl(string origin)
        {
            if (Kind is OmsPageKind.Chart or OmsPageKind.Board)
                return OmsWebsite.ChartBoard(origin, Md5!, Ruleset, Ruleset == "bms" ? Mode : null,
                    Ruleset == "bms" ? Condition : null, Sources?.Split(',', StringSplitOptions.RemoveEmptyEntries), Group, Page)!;
            string path = Kind switch
            {
                OmsPageKind.Profile => "users/" + UserId.ToString(CultureInfo.InvariantCulture),
                OmsPageKind.Rankings => "rankings",
                OmsPageKind.Library => "beatmapsets",
                OmsPageKind.Account => "account",
                OmsPageKind.Community => "community",
                OmsPageKind.Topic => "community/" + PostId.ToString(CultureInfo.InvariantCulture),
                OmsPageKind.News => "news",
                OmsPageKind.Article => "news/" + Slug,
                OmsPageKind.Help => "help",
                _ => string.Empty,
            };
            var values = new List<(string Key, string? Value)>();
            if (Kind is OmsPageKind.Profile or OmsPageKind.Rankings)
                values.AddRange(new[] { ("ruleset", (string?)Ruleset), ("keymode", Keymode), (Kind == OmsPageKind.Rankings ? "source" : "sources", Sources), ("condition", Condition), ("metric", Metric) });
            if (Kind == OmsPageKind.Profile && Recent) values.Add(("section", "recent"));
            if (Kind == OmsPageKind.Rankings && ScopePage != 1) values.Add(("scope_page", ScopePage.ToString(CultureInfo.InvariantCulture)));
            if (Kind == OmsPageKind.Community)
                values.AddRange(new[] { ("category", Category), ("q", Query.Length == 0 ? null : Query) });
            if (Kind == OmsPageKind.Library)
                values.AddRange(new[] { ("ruleset", (string?)Ruleset), ("q", Query.Length == 0 ? null : Query) });
            if (Page != 1) values.Add(("page", Page.ToString(CultureInfo.InvariantCulture)));
            string query = string.Join("&", values.Where(v => v.Value != null).Select(v => Uri.EscapeDataString(v.Key) + "=" + Uri.EscapeDataString(v.Value!)));
            return new Uri(new Uri(origin), path).AbsoluteUri + (query.Length == 0 ? string.Empty : "?" + query)
                + (Kind == OmsPageKind.Help && HelpAnchor != null ? "#" + Uri.EscapeDataString(HelpAnchor) : string.Empty);
        }

        public static bool TryParse(string origin, string url, out OmsPageRoute route)
        {
            route = new OmsPageRoute();
            if (!Uri.TryCreate(new Uri(origin), url, out Uri? uri) || uri.GetLeftPart(UriPartial.Authority) != new Uri(origin).GetLeftPart(UriPartial.Authority)
                || uri.UserInfo.Length != 0 || url.Length > 4096)
                return false;
            var query = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string value in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = value.Split('=', 2);
                if (!query.TryAdd(Uri.UnescapeDataString(pair[0]), pair.Length == 2 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty))
                    return false;
            }
            string? read(string name) => query.GetValueOrDefault(name);
            string ruleset = read("ruleset") ?? "bms";
            if (ruleset is not ("bms" or "mania") || (read("page") is string pageText
                && (!int.TryParse(pageText, NumberStyles.None, CultureInfo.InvariantCulture, out int pageNumber) || pageNumber is < 1 or > 1000000))
                || (read("scope_page") is string scopeText && (!int.TryParse(scopeText, NumberStyles.None, CultureInfo.InvariantCulture, out int scopeNumber) || scopeNumber is < 1 or > 1000000)))
                return false;
            route = route with
            {
                Ruleset = ruleset,
                Keymode = read("keymode"),
                Sources = read("sources"),
                Condition = read("condition"),
                Metric = read("metric"),
                Page = read("page") == null ? 1 : int.Parse(read("page")!, CultureInfo.InvariantCulture),
                ScopePage = read("scope_page") == null ? 1 : int.Parse(read("scope_page")!, CultureInfo.InvariantCulture),
                Recent = read("section") == "recent",
                Group = read("group"),
                Mode = read("mode") ?? "reference",
                Category = read("category"),
                Query = read("q") ?? string.Empty
            };
            string[] parts = uri.AbsolutePath.Trim('/').Split('/');
            if (parts.Length == 1)
            {
                OmsPageKind? kind = parts[0] switch
                {
                    "" => OmsPageKind.Home,
                    "rankings" => OmsPageKind.Rankings,
                    "community" => OmsPageKind.Community,
                    "news" => OmsPageKind.News,
                    "help" => OmsPageKind.Help,
                    "account" => OmsPageKind.Account,
                    "beatmapsets" => OmsPageKind.Library,
                    _ => null
                };
                if (parts[0] is "beatmaps" or "ir" && read("md5") is string md5 && md5.Length == 32 && md5.All(Uri.IsHexDigit)
                    && route.Mode is "reference" or "comparable")
                { route = route with { Kind = OmsPageKind.Chart, Md5 = md5.ToLowerInvariant() }; return true; }
                if (kind != null)
                {
                    if (kind == OmsPageKind.Rankings)
                    {
                        if (read("source") != null && read("sources") != null && read("source") != read("sources")) return false;
                        route = route with { Sources = read("source") ?? read("sources") };
                    }
                    route = route with { Kind = kind.Value, HelpAnchor = uri.Fragment.Length > 1 ? Uri.UnescapeDataString(uri.Fragment[1..]) : null };
                    return true;
                }
            }
            if (parts.Length == 2 && parts[0] == "news" && parts[1].Length is > 0 and <= 100 && parts[1].All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            { route = route with { Kind = OmsPageKind.Article, Slug = parts[1] }; return true; }
            if (parts.Length == 2 && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long id) && id > 0)
            {
                if (parts[0] == "users") { route = route with { Kind = OmsPageKind.Profile, UserId = id }; return true; }
                if (parts[0] == "community") { route = route with { Kind = OmsPageKind.Topic, PostId = id }; return true; }
            }
            return false;
        }
    }
}
