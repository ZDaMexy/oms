// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Game.Online.API;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Select;

namespace osu.Game.Beatmaps
{
    public static class BeatmapInfoExtensions
    {
        private const string bms_ruleset_short_name = "bms";
        private const string mania_ruleset_short_name = "mania";

        /// <summary>
        /// Given an <see cref="IBeatmap"/>, update length, BPM and object counts.
        /// </summary>
        public static void UpdateStatisticsFromBeatmap(this BeatmapInfo beatmapInfo, IBeatmap beatmap)
        {
            beatmapInfo.Length = beatmap.CalculatePlayableLength();
            beatmapInfo.BPM = 60000 / beatmap.GetMostCommonBeatLength();
            beatmapInfo.EndTimeObjectCount = beatmap.HitObjects.Count(h => h is IHasDuration);
            beatmapInfo.TotalObjectCount = beatmap.HitObjects.Count;
        }

        /// <summary>
        /// A user-presentable display title representing this beatmap.
        /// </summary>
        public static string GetDisplayTitle(this IBeatmapInfo beatmapInfo) => beatmapInfo.GetDisplayTitle(includeDifficultyName: true, includeCreator: true);

        public static string GetDisplayTitle(this IBeatmapInfo beatmapInfo, bool includeDifficultyName, bool includeCreator = true)
            => beatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName, includeCreator).Romanised ?? string.Empty;

        /// <summary>
        /// A user-presentable display title representing this beatmap, with localisation handling for potentially romanisable fields.
        /// </summary>
        public static RomanisableString GetDisplayTitleRomanisable(this IBeatmapInfo beatmapInfo, bool includeDifficultyName = true, bool includeCreator = true)
        {
            string displayArtist = BeatmapLocalMetadataDisplayResolver.GetDisplayArtist(beatmapInfo);
            string displayArtistUnicode = BeatmapLocalMetadataDisplayResolver.GetDisplayArtistUnicode(beatmapInfo);
            string title = beatmapInfo.Metadata.Title;
            string titleUnicode = string.IsNullOrEmpty(beatmapInfo.Metadata.TitleUnicode) ? title : beatmapInfo.Metadata.TitleUnicode;
            string creatorSuffix = string.Empty;

            if (includeCreator)
            {
                string displayCreator = BeatmapLocalMetadataDisplayResolver.GetDisplayCreator(beatmapInfo);

                if (!string.IsNullOrWhiteSpace(displayCreator))
                    creatorSuffix = $" ({displayCreator})";
            }

            string original = $"{displayArtistUnicode} - {titleUnicode}{creatorSuffix}".Trim();
            string romanised = $"{displayArtist} - {title}{creatorSuffix}".Trim();

            if (includeDifficultyName)
            {
                string versionString = getVersionString(beatmapInfo);
                return new RomanisableString($"{original} {versionString}".Trim(), $"{romanised} {versionString}".Trim());
            }

            return new RomanisableString(original, romanised);
        }

        public static bool Match(this IBeatmapInfo beatmapInfo, params FilterCriteria.OptionalTextFilter[] filters)
        {
            foreach (var filter in filters)
            {
                if (filter.Matches(beatmapInfo.DifficultyName))
                    continue;

                if (BeatmapMetadataInfoExtensions.Match(beatmapInfo.Metadata, filter))
                    continue;

                // failed to match a single filter at all - fail the whole match.
                return false;
            }

            // got through all filters without failing any - pass the whole match.
            return true;
        }

        private static string getVersionString(IBeatmapInfo beatmapInfo) => string.IsNullOrEmpty(beatmapInfo.DifficultyName) ? string.Empty : $"[{beatmapInfo.DifficultyName}]";

        /// <summary>
        /// Whether gameplay is allowed for this beatmap with the provided ruleset (via conversion or direct compatibility).
        /// </summary>
        public static bool AllowGameplayWithRuleset(this IBeatmapInfo beatmap, RulesetInfo ruleset, bool allowConversion)
        {
            if (beatmap.Ruleset.ShortName == ruleset.ShortName)
                return true;

            if (allowConversion && beatmap.Ruleset.ShortName == bms_ruleset_short_name && ruleset.ShortName == mania_ruleset_short_name)
                return true;

            if (allowConversion && beatmap.Ruleset.OnlineID == 0 && ruleset.OnlineID != 0)
                return true;

            return false;
        }

        public static bool RequiresRulesetSwitch(this IBeatmapInfo beatmap, RulesetInfo currentRuleset, bool allowConversion)
            => !beatmap.AllowGameplayWithRuleset(currentRuleset, allowConversion);

        public static bool RequiresRulesetSpecificStarRating(this IBeatmapInfo beatmap, RulesetInfo? ruleset, bool allowConversion)
        {
            if (ruleset == null)
                return false;

            return beatmap.Ruleset.ShortName != ruleset.ShortName && beatmap.AllowGameplayWithRuleset(ruleset, allowConversion);
        }

        public static double GetResolvedStarRating(this BeatmapInfo beatmap, IReadOnlyDictionary<Guid, double>? resolvedStarRatings)
            => resolvedStarRatings != null && resolvedStarRatings.TryGetValue(beatmap.ID, out double starRating) ? starRating : beatmap.StarRating;

        /// <summary>
        /// Get the beatmap info page URL, or <c>null</c> if unavailable.
        /// </summary>
        public static string? GetOnlineURL(this IBeatmapInfo beatmapInfo, IAPIProvider api, IRulesetInfo? ruleset = null)
        {
            if (beatmapInfo.OnlineID <= 0 || beatmapInfo.BeatmapSet == null || string.IsNullOrEmpty(api.Endpoints.WebsiteUrl))
                return null;

            return $@"{api.Endpoints.WebsiteUrl}/beatmapsets/{beatmapInfo.BeatmapSet.OnlineID}#{ruleset?.ShortName ?? beatmapInfo.Ruleset.ShortName}/{beatmapInfo.OnlineID}";
        }
    }
}
