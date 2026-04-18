// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace osu.Game.Beatmaps
{
    internal static class BeatmapLocalMetadataDisplayResolver
    {
        private const string bms_ruleset_short_name = "bms";

        private static readonly string[] bms_creator_prefixes =
        {
            "obj",
            "chart",
            "charts",
            "pattern",
            "patterns",
            "note",
            "notes",
            "fumen",
            "譜面",
            "谱面",
        };

        private static readonly char[] bms_leading_separator_chars =
        {
            '/',
            '-',
            '|',
            '(',
            '[',
            '{',
            '~',
            '・',
            '·',
            '•',
        };

        private static readonly char[] bms_trailing_separator_chars =
        {
            ' ',
            '/',
            '-',
            '|',
            ':',
            '：',
        };

        public static string GetDisplayArtist(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return beatmap.Metadata.Artist;

            return StripBmsCreatorFromArtist(beatmap.Metadata.Artist);
        }

        public static string GetDisplayArtistUnicode(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return string.IsNullOrWhiteSpace(beatmap.Metadata.ArtistUnicode) ? beatmap.Metadata.Artist : beatmap.Metadata.ArtistUnicode;

            string source = string.IsNullOrWhiteSpace(beatmap.Metadata.ArtistUnicode) ? beatmap.Metadata.Artist : beatmap.Metadata.ArtistUnicode;
            string cleaned = StripBmsCreatorFromArtist(source);

            return string.IsNullOrWhiteSpace(cleaned) ? GetDisplayArtist(beatmap) : cleaned;
        }

        public static string GetDisplayCreator(IBeatmapInfo beatmap)
        {
            if (!string.IsNullOrWhiteSpace(beatmap.Metadata.Author.Username))
                return beatmap.Metadata.Author.Username;

            if (!isBmsBeatmap(beatmap))
                return string.Empty;

            return tryGetBmsCreatorFromRulesetData(beatmap.Metadata as BeatmapMetadata)
                   ?? TryExtractBmsCreator(beatmap.Metadata.Artist)?.creator
                   ?? string.Empty;
        }

        public static bool HasLinkedCreatorProfile(IBeatmapInfo beatmap)
            => !string.IsNullOrWhiteSpace(beatmap.Metadata.Author.Username);

        public static string? GetDisplayGenre(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return null;

            return nullIfWhiteSpace(tryGetBmsChartMetadataString(beatmap.Metadata as BeatmapMetadata, "genre"))
                   ?? nullIfWhiteSpace(beatmap.Metadata.Tags);
        }

        public static string[] GetDisplayMapperTags(IBeatmapInfo beatmap)
        {
            if (string.IsNullOrWhiteSpace(beatmap.Metadata.Tags))
                return Array.Empty<string>();

            if (isBmsBeatmap(beatmap))
            {
                string? genre = GetDisplayGenre(beatmap);

                if (!string.IsNullOrWhiteSpace(genre) && string.Equals(beatmap.Metadata.Tags.Trim(), genre, StringComparison.OrdinalIgnoreCase))
                    return Array.Empty<string>();
            }

            return beatmap.Metadata.Tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static string? tryGetBmsCreatorFromRulesetData(BeatmapMetadata? metadata)
            => TryExtractBmsCreator(tryGetBmsChartMetadataString(metadata, "sub_artist"))?.creator
               ?? TryExtractBmsCreator(tryGetBmsChartMetadataString(metadata, "comment"))?.creator;

        private static string? tryGetBmsChartMetadataString(BeatmapMetadata? metadata, string fieldName)
        {
            if (metadata == null || string.IsNullOrWhiteSpace(metadata.RulesetDataJson))
                return null;

            try
            {
                var root = JObject.Parse(metadata.RulesetDataJson);
                return root.SelectToken($@"chart_metadata.{fieldName}")?.Value<string>();
            }
            catch (JsonException)
            {
                return null;
            }
        }

        internal static string StripBmsCreatorFromArtist(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return TryExtractBmsCreator(value)?.leadingText is string leadingText && !string.IsNullOrWhiteSpace(leadingText)
                ? leadingText
                : value.Trim();
        }

        internal static (string creator, string? leadingText)? TryExtractBmsCreator(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string trimmed = value.Trim();

            foreach (string prefix in bms_creator_prefixes)
            {
                int searchIndex = 0;

                while (searchIndex < trimmed.Length)
                {
                    int prefixIndex = trimmed.IndexOf(prefix, searchIndex, StringComparison.OrdinalIgnoreCase);

                    if (prefixIndex < 0)
                        break;

                    searchIndex = prefixIndex + prefix.Length;

                    if (!IsValidBmsCreatorPrefixPosition(trimmed, prefixIndex))
                        continue;

                    int separatorIndex = prefixIndex + prefix.Length;

                    while (separatorIndex < trimmed.Length && char.IsWhiteSpace(trimmed[separatorIndex]))
                        separatorIndex++;

                    if (separatorIndex >= trimmed.Length)
                        continue;

                    char separator = trimmed[separatorIndex];

                    if (separator is not (':' or '：' or '-' or '/' or '='))
                        continue;

                    string creator = trimmed[(separatorIndex + 1)..].Trim();

                    if (string.IsNullOrWhiteSpace(creator))
                        continue;

                    string leadingText = trimmed[..prefixIndex].TrimEnd(bms_trailing_separator_chars);

                    return (creator, string.IsNullOrWhiteSpace(leadingText) ? null : leadingText);
                }
            }

            return null;
        }

        internal static bool IsValidBmsCreatorPrefixPosition(string value, int prefixIndex)
        {
            if (prefixIndex == 0)
                return true;

            int precedingIndex = prefixIndex - 1;

            while (precedingIndex >= 0 && char.IsWhiteSpace(value[precedingIndex]))
                precedingIndex--;

            if (precedingIndex < 0)
                return true;

            return Array.IndexOf(bms_leading_separator_chars, value[precedingIndex]) >= 0;
        }

        private static bool isBmsBeatmap(IBeatmapInfo beatmap)
            => string.Equals(beatmap.Ruleset.ShortName, bms_ruleset_short_name, StringComparison.Ordinal);

        private static string? nullIfWhiteSpace(string? value)
            => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
