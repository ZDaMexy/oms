// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Carousel;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;

namespace osu.Game.Screens.Select
{
    public class BeatmapCarouselFilterSorting : ICarouselFilter
    {
        private static readonly IReadOnlyDictionary<Guid, ScoreInfo> empty_top_local_scores = new Dictionary<Guid, ScoreInfo>();

        public int BeatmapItemsCount { get; private set; }

        private readonly Func<FilterCriteria> getCriteria;
        private readonly Func<FilterCriteria, IReadOnlyDictionary<Guid, ScoreInfo>> getTopLocalScores;

        public BeatmapCarouselFilterSorting(Func<FilterCriteria> getCriteria)
            : this(getCriteria, _ => empty_top_local_scores)
        {
        }

        public BeatmapCarouselFilterSorting(Func<FilterCriteria> getCriteria, Func<FilterCriteria, IReadOnlyDictionary<Guid, ScoreInfo>> getTopLocalScores)
        {
            this.getCriteria = getCriteria;
            this.getTopLocalScores = getTopLocalScores;
        }

        public async Task<List<CarouselItem>> Run(IEnumerable<CarouselItem> items, CancellationToken cancellationToken) => await Task.Run(() =>
        {
            var criteria = getCriteria();
            var rulesetInstance = criteria.Ruleset?.CreateInstance();
            var topLocalScores = requiresTopLocalScoreLookup(criteria.Sort) ? getTopLocalScores(criteria) : null;

            bool groupedSets = BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether(criteria);

            BeatmapItemsCount = items.Count();

            return items.Order(Comparer<CarouselItem>.Create((a, b) =>
            {
                var ab = (BeatmapInfo)a.Model;
                var bb = (BeatmapInfo)b.Model;

                if (groupedSets)
                {
                    if (ab.BeatmapSet!.Equals(bb.BeatmapSet))
                        return compareDifficulty(ab, bb, criteria.Sort);

                    // If we're grouping by sets, all fallback sorts need to be aggregates for the set.
                    return compare(ab, bb, criteria.Sort, aggregate: true, rulesetInstance, topLocalScores);
                }

                return compare(ab, bb, criteria.Sort, aggregate: false, rulesetInstance, topLocalScores);
            })).ToList();
        }, cancellationToken).ConfigureAwait(false);

        private static int compare(BeatmapInfo a, BeatmapInfo b, SortMode sort, bool aggregate, Ruleset? rulesetInstance, IReadOnlyDictionary<Guid, ScoreInfo>? topLocalScores)
        {
            int comparison;

            switch (sort)
            {
                case SortMode.Artist:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Artist, b.BeatmapSet!.Metadata.Artist);
                    if (comparison == 0)
                        goto case SortMode.Title;
                    break;

                case SortMode.Title:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Title, b.BeatmapSet!.Metadata.Title);
                    break;

                case SortMode.Author:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Author.Username, b.BeatmapSet!.Metadata.Author.Username);
                    break;

                case SortMode.Source:
                    comparison = OrdinalSortByCaseStringComparer.DEFAULT.Compare(a.BeatmapSet!.Metadata.Source, b.BeatmapSet!.Metadata.Source);
                    break;

                case SortMode.Difficulty:
                    comparison = a.StarRating.CompareTo(b.StarRating);
                    break;

                case SortMode.DateAdded:
                    comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);
                    break;

                case SortMode.DateRanked:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateRanked, a.BeatmapSet!.DateRanked);
                    break;

                case SortMode.DateSubmitted:
                    comparison = Nullable.Compare(b.BeatmapSet!.DateSubmitted, a.BeatmapSet!.DateSubmitted);
                    break;

                case SortMode.LastPlayed:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(b, a, static b => (b.LastPlayed ?? DateTimeOffset.MinValue).ToUnixTimeSeconds());
                    else
                        comparison = Nullable.Compare(b.LastPlayed, a.LastPlayed);
                    break;

                case SortMode.BPM:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(a, b, static b => b.BPM);
                    else
                        comparison = a.BPM.CompareTo(b.BPM);
                    break;

                case SortMode.Length:
                    if (aggregate)
                        comparison = compareUsingAggregateMax(a, b, static b => b.Length);
                    else
                        comparison = a.Length.CompareTo(b.Length);
                    break;

                case SortMode.ClearLamp:
                case SortMode.Accuracy:
                case SortMode.Misses:
                    if (rulesetInstance == null || topLocalScores == null)
                    {
                        comparison = 0;
                        break;
                    }

                    comparison = aggregate
                        ? compareUsingAggregateLocalScoreSort(a, b, sort, rulesetInstance, topLocalScores)
                        : rulesetInstance.CompareSongSelectScores(sort, tryGetTopLocalScore(a, topLocalScores), tryGetTopLocalScore(b, topLocalScores));
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            // If the initial sort could not differentiate, attempt to use DateAdded to order sets in a stable fashion.
            // The directionality of this matches the current SortMode.DateAdded, but we may want to reconsider if that becomes a user decision (ie. asc / desc).
            if (comparison == 0)
                comparison = b.BeatmapSet!.DateAdded.CompareTo(a.BeatmapSet!.DateAdded);

            // If DateAdded fails to break the tie, fallback to our internal GUID for stability.
            // This basically means it's a stable random sort.
            if (comparison == 0)
                comparison = b.BeatmapSet!.ID.CompareTo(a.BeatmapSet!.ID);

            return comparison;
        }

        private static int compareDifficulty(BeatmapInfo a, BeatmapInfo b, SortMode sort)
        {
            int comparison = a.Ruleset.CompareTo(b.Ruleset);

            if (comparison == 0)
                comparison = a.StarRating.CompareTo(b.StarRating);

            return comparison;
        }

        private static int compareUsingAggregateMax(BeatmapInfo a, BeatmapInfo b, Func<BeatmapInfo, double> func)
        {
            var aMatchedBeatmaps = a.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden);
            var bMatchedBeatmaps = b.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden);

            bool aAny = aMatchedBeatmaps.Any();
            bool bAny = bMatchedBeatmaps.Any();

            if (!aAny && !bAny) return 0;
            if (!aAny) return -1;
            if (!bAny) return 1;

            return aMatchedBeatmaps.Max(func).CompareTo(bMatchedBeatmaps.Max(func));
        }

        private static int compareUsingAggregateLocalScoreSort(BeatmapInfo a, BeatmapInfo b, SortMode sort, Ruleset rulesetInstance, IReadOnlyDictionary<Guid, ScoreInfo> topLocalScores)
            => rulesetInstance.CompareSongSelectScores(sort, getBestLocalScore(a, sort, rulesetInstance, topLocalScores), getBestLocalScore(b, sort, rulesetInstance, topLocalScores));

        private static ScoreInfo? getBestLocalScore(BeatmapInfo beatmap, SortMode sort, Ruleset rulesetInstance, IReadOnlyDictionary<Guid, ScoreInfo> topLocalScores)
        {
            ScoreInfo? bestScore = null;

            foreach (var matchedBeatmap in beatmap.BeatmapSet!.Beatmaps.Where(bb => !bb.Hidden))
            {
                var candidateScore = tryGetTopLocalScore(matchedBeatmap, topLocalScores);

                if (bestScore == null || rulesetInstance.CompareSongSelectScores(sort, candidateScore, bestScore) < 0)
                    bestScore = candidateScore;
            }

            return bestScore;
        }

        private static ScoreInfo? tryGetTopLocalScore(BeatmapInfo beatmap, IReadOnlyDictionary<Guid, ScoreInfo> topLocalScores)
            => topLocalScores.TryGetValue(beatmap.ID, out var score) ? score : null;

        private static bool requiresTopLocalScoreLookup(SortMode sort)
            => sort is SortMode.ClearLamp or SortMode.Accuracy or SortMode.Misses;
    }
}
