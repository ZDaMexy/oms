// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;

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

        // Trailing bracket pairs a BMS chart commonly uses to embed the difficulty / subtitle in #TITLE
        // (e.g. "GOODBOUNCE [ANOTHER]", "Song（7keys）"). ASCII plus the fullwidth / CJK variants seen in JP charts.
        private static readonly (char open, char close)[] bms_title_subtitle_brackets =
        {
            ('[', ']'),
            ('(', ')'),
            ('<', '>'),
            ('（', '）'),
            ('［', '］'),
            ('〈', '〉'),
            ('【', '】'),
            ('〔', '〕'),
        };

        // Symmetric wrappers ("Song -HYPER-", "Song ~ANOTHER~"). Only treated as a difficulty tag when the title
        // actually ends with the wrapper, so ordinary hyphenated titles ("Re-Loaded") are left untouched.
        private static readonly char[] bms_title_subtitle_wrappers =
        {
            '-',
            '~',
            '～',
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

        public static string GetDisplayTitle(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return beatmap.Metadata.Title;

            return getBmsCleanTitle(beatmap, beatmap.Metadata.Title);
        }

        public static string GetDisplayTitleUnicode(IBeatmapInfo beatmap)
        {
            string source = string.IsNullOrWhiteSpace(beatmap.Metadata.TitleUnicode) ? beatmap.Metadata.Title : beatmap.Metadata.TitleUnicode;

            if (!isBmsBeatmap(beatmap))
                return source;

            string cleaned = getBmsCleanTitle(beatmap, source);

            return string.IsNullOrWhiteSpace(cleaned) ? GetDisplayTitle(beatmap) : cleaned;
        }

        /// <summary>
        /// The difficulty name to display for a BMS chart. BMS has no authoritative difficulty field, so this prefers,
        /// in order: the charter's explicit name — an <c>#SUBTITLE</c> or the bracket tag split off the title tail
        /// (e.g. <c>Dead Soul [Revive]</c> → "Revive") — then the generic <c>#DIFFICULTY</c> category
        /// (Beginner/Normal/Hyper/Another/Insane). The explicit name MUST win: the category would otherwise overwrite
        /// a meaningful name like "Revive" with "Insane". The bare play-level number is dropped (it only duplicates the
        /// star rating); a symbolic/textual stored name is kept as a last resort.
        /// </summary>
        public static string GetDisplayDifficultyName(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return beatmap.DifficultyName;

            var chartMetadata = BmsPersistedMetadataResolver.GetChartMetadata(beatmap.Metadata as BeatmapMetadata);

            string? subtitle = nullIfWhiteSpace(chartMetadata?.Subtitle)
                               ?? TryExtractTitleSubtitle(beatmap.Metadata.Title)?.subtitle;

            if (!string.IsNullOrWhiteSpace(subtitle))
                return subtitle!;

            string? label = getBmsHeaderDifficultyLabel(chartMetadata?.HeaderDifficulty);

            if (!string.IsNullOrWhiteSpace(label))
                return label!;

            return isBareNumericPlayLevel(beatmap.DifficultyName) ? string.Empty : beatmap.DifficultyName;
        }

        /// <summary>
        /// The difficulty-table classification label(s) for a BMS chart, e.g. <c>"sl4"</c>, or <c>"★8/sl4"</c> when the
        /// chart is indexed by more than one difficulty table. Labels are ordered by the song-select difficulty-table
        /// order (the table sort order) and joined with <c>'/'</c>, listing one label per indexing table. Returns an empty
        /// string for non-BMS charts or charts not indexed by any enabled table. Identical between BMS-mode and the
        /// converted-mania view (the chart stays a BMS beatmap in both). Display-only.
        /// </summary>
        public static string GetDisplayDifficultyTableClassification(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return string.Empty;

            var entries = BmsPersistedMetadataResolver.GetDifficultyTableEntries(beatmap.Metadata as BeatmapMetadata);

            if (entries.Count == 0)
                return string.Empty;

            var labels = entries
                         .GroupBy(entry => (entry.TableSortOrder, entry.TableName))
                         .OrderBy(group => group.Key.TableSortOrder)
                         .ThenBy(group => group.Key.TableName, StringComparer.Ordinal)
                         .Select(group => group.OrderBy(entry => entry.Level)
                                               .ThenBy(entry => entry.LevelLabel, StringComparer.Ordinal)
                                               .First().LevelLabel)
                         .Where(label => !string.IsNullOrWhiteSpace(label));

            return string.Join('/', labels);
        }

        /// <summary>
        /// The BMS-mode difficulty-level pill text, e.g. <c>"NORMAL 7"</c>. The label comes from <c>#DIFFICULTY</c>
        /// (0 / undefined → "UNKNOWN", 1-5 → BEGINNER/NORMAL/HYPER/ANOTHER/INSANE) and the level is the raw
        /// <c>#PLAYLEVEL</c> string (the charter's stated level, kept verbatim). The label is shown on its own when no
        /// play level is present. Returns an empty string for non-BMS charts. This replaces the numeric star rating
        /// in BMS mode only; the converted-mania view keeps the real star rating. Display-only.
        /// </summary>
        public static string GetDisplayDifficultyLevel(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return string.Empty;

            var chartMetadata = BmsPersistedMetadataResolver.GetChartMetadata(beatmap.Metadata as BeatmapMetadata);

            string label = getBmsIidxDifficultyLabel(chartMetadata?.HeaderDifficulty);
            string playLevel = chartMetadata?.PlayLevel?.Trim() ?? string.Empty;

            return string.IsNullOrEmpty(playLevel) ? label : $"{label} {playLevel}";
        }

        /// <summary>
        /// The BMS <c>#DIFFICULTY</c> tier used to colour the difficulty-level pill: <c>1-5</c> for the defined
        /// categories, or <c>0</c> for an undefined / out-of-range value (UNKNOWN). Always <c>0</c> for non-BMS charts.
        /// </summary>
        public static int GetBmsDifficultyTier(IBeatmapInfo beatmap)
        {
            if (!isBmsBeatmap(beatmap))
                return 0;

            int? headerDifficulty = BmsPersistedMetadataResolver.GetChartMetadata(beatmap.Metadata as BeatmapMetadata)?.HeaderDifficulty;

            return headerDifficulty is >= 1 and <= 5 ? headerDifficulty.Value : 0;
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

            return nullIfWhiteSpace(BmsPersistedMetadataResolver.GetChartMetadata(beatmap.Metadata as BeatmapMetadata)?.Genre)
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

        private static string getBmsCleanTitle(IBeatmapInfo beatmap, string title)
        {
            // Only split a trailing bracket off the title when the chart did NOT provide an explicit #SUBTITLE.
            // This mirrors the BMS-player convention of implicitly deriving the subtitle from the title tail only
            // when one wasn't given, and avoids second-guessing a charter who already separated the two.
            if (!string.IsNullOrWhiteSpace(BmsPersistedMetadataResolver.GetChartMetadata(beatmap.Metadata as BeatmapMetadata)?.Subtitle))
                return title;

            return TryExtractTitleSubtitle(title)?.cleanTitle ?? title;
        }

        private static string? getBmsHeaderDifficultyLabel(int? headerDifficulty)
            => headerDifficulty switch
            {
                1 => "Beginner",
                2 => "Normal",
                3 => "Hyper",
                4 => "Another",
                5 => "Insane",
                _ => null,
            };

        // Uppercase IIDX-style category label used by the difficulty-level pill. Unlike getBmsHeaderDifficultyLabel,
        // an undefined / out-of-range #DIFFICULTY maps to "UNKNOWN" rather than null (the pill always shows a label).
        private static string getBmsIidxDifficultyLabel(int? headerDifficulty)
            => headerDifficulty switch
            {
                1 => "BEGINNER",
                2 => "NORMAL",
                3 => "HYPER",
                4 => "ANOTHER",
                5 => "INSANE",
                _ => "UNKNOWN",
            };

        private static bool isBareNumericPlayLevel(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            foreach (char c in value.Trim())
            {
                if (!char.IsDigit(c))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Splits a trailing bracket / wrapper tag (the embedded difficulty or subtitle) off the end of a BMS title.
        /// Returns the cleaned title and the extracted tag, or null when the title has no such trailing tag.
        /// Only the last trailing group is removed, and only when a non-empty title body remains.
        /// </summary>
        internal static (string cleanTitle, string subtitle)? TryExtractTitleSubtitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            string trimmed = title.TrimEnd();

            if (trimmed.Length < 2)
                return null;

            char lastChar = trimmed[^1];

            foreach (var (open, close) in bms_title_subtitle_brackets)
            {
                if (lastChar != close)
                    continue;

                int openIndex = trimmed.LastIndexOf(open);

                // openIndex <= 0 means there is no matching opener, or the whole title is the bracket — leave as-is.
                return openIndex <= 0 ? null : buildTitleSubtitleSplit(trimmed, openIndex);
            }

            foreach (char wrapper in bms_title_subtitle_wrappers)
            {
                if (lastChar != wrapper)
                    continue;

                int openIndex = trimmed.LastIndexOf(wrapper, trimmed.Length - 2);

                return openIndex <= 0 ? null : buildTitleSubtitleSplit(trimmed, openIndex);
            }

            return null;
        }

        private static (string cleanTitle, string subtitle)? buildTitleSubtitleSplit(string trimmed, int openIndex)
        {
            string subtitle = trimmed[(openIndex + 1)..^1].Trim();
            string cleanTitle = trimmed[..openIndex].Trim();

            if (string.IsNullOrWhiteSpace(subtitle) || string.IsNullOrWhiteSpace(cleanTitle))
                return null;

            return (cleanTitle, subtitle);
        }

        private static string? tryGetBmsCreatorFromRulesetData(BeatmapMetadata? metadata)
        {
            var chartMetadata = BmsPersistedMetadataResolver.GetChartMetadata(metadata);

            return TryExtractBmsCreator(chartMetadata?.SubArtist)?.creator
                   ?? TryExtractBmsCreator(chartMetadata?.Comment)?.creator;
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
