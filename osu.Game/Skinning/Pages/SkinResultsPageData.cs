// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Skinning.Pages
{
    /// <summary>
    /// Ruleset-neutral presentation values copied from the selected real score. Result names and counts are supplied
    /// by that score's ruleset, including its compatibility handling for older scores; no skin calculates scoring.
    /// </summary>
    public sealed class SkinResultsPageData
    {
        private static readonly FrozenDictionary<HitResult, string> result_sources = Enum.GetValues<HitResult>()
            .Where(result => result is not (HitResult.None or HitResult.IgnoreHit or HitResult.IgnoreMiss))
            .ToFrozenDictionary(result => result, result => "hit." + typeof(HitResult).GetMember(result.ToString()).Single()
                .GetCustomAttribute<EnumMemberAttribute>()!.Value!);

        private static readonly FrozenSet<string> allowed_sources = new[]
            {
                "beatmap.title", "beatmap.artist", "beatmap.version", "score.value", "score.label",
                "score.accuracy", "score.accuracy.label", "score.max-combo", "score.max-combo.label", "score.rank", "score.passed", "score.cleared",
            }.Concat(result_sources.Values.SelectMany(source => new[] { source, source + ".label" }))
            .ToFrozenSet(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, string> Values { get; }
        public bool Passed { get; }
        public bool? Cleared { get; }

        private SkinResultsPageData(Dictionary<string, string> values, bool passed, bool? cleared)
        {
            Values = new ReadOnlyDictionary<string, string>(values);
            Passed = passed;
            Cleared = cleared;
        }

        public static bool IsAllowedSource(string source) => allowed_sources.Contains(source);

        public static SkinResultsPageData FromScore(ScoreInfo score, long displayedScore)
        {
            ArgumentNullException.ThrowIfNull(score);
            var ruleset = score.Ruleset.CreateInstance();
            bool? cleared = ruleset.GetResultsClearState(score);
            var beatmap = score.BeatmapInfo;
            var values = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["beatmap.title"] = bounded(beatmap == null ? string.Empty : BeatmapLocalMetadataDisplayResolver.GetDisplayTitle(beatmap)),
                ["beatmap.artist"] = bounded(beatmap == null ? string.Empty : BeatmapLocalMetadataDisplayResolver.GetDisplayArtist(beatmap)),
                ["beatmap.version"] = bounded(score.BeatmapInfo?.DifficultyName ?? string.Empty),
                ["score.value"] = displayedScore.ToString("N0", CultureInfo.InvariantCulture),
                ["score.label"] = bounded(ruleset.GetResultsScoreLabel(score)?.ToString() ?? string.Empty),
                ["score.accuracy"] = (score.Accuracy * 100).ToString("0.00", CultureInfo.InvariantCulture) + "%",
                ["score.accuracy.label"] = "ACCURACY",
                ["score.max-combo"] = score.MaxCombo.ToString(CultureInfo.InvariantCulture),
                ["score.max-combo.label"] = "MAX COMBO",
                ["score.rank"] = score.Rank.ToString(),
                ["score.passed"] = score.Passed ? "true" : "false",
                ["score.cleared"] = cleared is bool outcome ? outcome ? "true" : "false" : string.Empty,
            };
            foreach (var (result, displayName) in ruleset.GetHitResultsForDisplay())
            {
                string source = result_sources[result];
                values.Add(source, ruleset.GetDisplayCountForHitResult(score, result).ToString(CultureInfo.InvariantCulture));
                values.Add(source + ".label", bounded(displayName.ToString()));
            }
            return new SkinResultsPageData(values, score.Passed, cleared);
        }

        public bool? GetBoolean(string source) => source switch
        {
            "score.passed" => Passed,
            "score.cleared" => Cleared,
            _ => throw new ArgumentException("Unknown skin results boolean source.", nameof(source)),
        };

        /// <summary>An allowed but inapplicable result is unavailable, rather than a fabricated zero count.</summary>
        public string GetText(string source)
        {
            if (!IsAllowedSource(source))
                throw new ArgumentException("Unknown skin results source.", nameof(source));
            return Values.GetValueOrDefault(source, string.Empty);
        }

        private static string bounded(string value)
        {
            var output = new StringBuilder(Math.Min(value.Length, SkinPageContracts.MAX_TEXT_CHARACTERS));
            foreach (Rune character in value.EnumerateRunes())
            {
                if (Rune.IsControl(character))
                    continue;
                if (output.Length + character.Utf16SequenceLength > SkinPageContracts.MAX_TEXT_CHARACTERS)
                    break;
                output.Append(character.ToString());
            }
            return output.ToString();
        }
    }
}
