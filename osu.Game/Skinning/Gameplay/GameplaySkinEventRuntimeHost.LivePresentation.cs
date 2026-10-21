// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Skinning.Gameplay
{
    public partial class GameplaySkinEventRuntimeHost
    {
        private GameplaySkinChartPresentation? chartPresentation;
        private Func<GameplaySkinOffsetStateSnapshot>? appliedOffset;
        private BeatmapInfo? scoreBeatmap;
        private Func<ScoreInfo, bool>? validSavedScore;
        private int countedHitEvents;
        private int fastCount;
        private int slowCount;
        private bool automatedTiming;
        private readonly IGameplaySkinArrivalVisibilityProjection? arrivalVisibility;
        private double? arrivalCueFadeStartTime;

        private double? updateArrivalCue(double gameplayTime)
            => arrivalCueFadeStartTime = SampleArrivalCueFadeStart(chartPresentation, arrivalVisibility, gameplayTime, arrivalCueFadeStartTime);

        private void retireArrivalCue(double gameplayTime)
        {
            if (chartPresentation?.ArrivalCueAvailable == true)
                arrivalCueFadeStartTime = gameplayTime - 200;
        }

        internal static double? SampleArrivalCueFadeStart(GameplaySkinChartPresentation? chart, IGameplaySkinArrivalVisibilityProjection? visibility,
                                                          double gameplayTime, double? previousFadeStart)
        {
            if (chart?.ArrivalCueAvailable != true || visibility == null)
                return null;
            if (previousFadeStart is double completed && gameplayTime - completed >= 200)
                return previousFadeStart;

            bool predicted = false;
            for (int i = 0; i < chart.Arrivals.Count; i++)
            {
                GameplaySkinLaneArrival arrival = chart.Arrivals[i];
                if (!chart.IsFirstRow(arrival.LaneId, arrival.GroupId))
                    continue;
                if (visibility.IsArrivalVisible(arrival.LaneId, arrival.Time, gameplayTime))
                    return gameplayTime - 200;
                if (!previousFadeStart.HasValue && !predicted && visibility.IsArrivalVisible(arrival.LaneId, arrival.Time, gameplayTime + 200))
                    predicted = true;
            }

            return previousFadeStart ?? (predicted ? gameplayTime : null);
        }

        private void preparePersonalBest(RealmAccess? realm, IAPIProvider? api)
        {
            GameplaySkinPreparedSceneProgram program = Publication.PreparedScene.Program;
            if (!program.UsesBindingSource(GameplaySkinSceneBindingSource.ScorePersonalBest)
                && !program.UsesBindingSource(GameplaySkinSceneBindingSource.ScorePersonalBestProgress)
                && !program.UsesBindingSource(GameplaySkinSceneBindingSource.ScorePersonalBestAvailable))
                return;

            if (realm == null || api == null || scoreProcessor == null || scoreBeatmap == null || chartPresentation == null
                || string.IsNullOrEmpty(scoreBeatmap.Hash))
                return;

            Guid beatmapId = scoreBeatmap.ID;
            string beatmapHash = scoreBeatmap.Hash;
            string rulesetName = scoreProcessor.Ruleset.ShortName;
            int userId = api.LocalUser.Value.Id;
            JArray mods = new JArray(scoreProcessor.Mods.Value.Select(mod => new APIMod(mod)).OrderBy(mod => mod.Acronym).Select(mod => JObject.FromObject(mod)));

            // This is a once-per-gameplay background load. Only detached numeric information crosses into the
            // event stream; neither a Realm model nor a query is retained by the renderer.
            long? best = realm.Run(r => r.All<ScoreInfo>().Where(score => !score.DeletePending && score.BeatmapHash == beatmapHash)
                                          .AsEnumerable()
                                          .Where(score => score.BeatmapInfo?.ID == beatmapId && score.Ruleset.ShortName == rulesetName
                                                          && score.UserID == userId && !score.BackgroundReprocessingFailed
                                                          && score.TotalScoreVersion == LegacyScoreEncoder.LATEST_VERSION
                                                          && score.TotalScore >= 0 && score.Rank != ScoreRank.F && score.MaximumStatistics.Count > 0
                                                          && (validSavedScore?.Invoke(score) ?? true)
                                                          && JToken.DeepEquals(mods, new JArray(score.APIMods.OrderBy(mod => mod.Acronym).Select(mod => JObject.FromObject(mod)))))
                                          .Select(score => (long?)score.TotalScore).Max());
            chartPresentation = new GameplaySkinChartPresentation(chartPresentation.Arrivals, chartPresentation.UsesExScore, best, chartPresentation.ArrivalCueAvailable);
        }

        private void prepareTimingCounts()
            => automatedTiming = scoreProcessor?.Mods.Value.Any(mod => mod is ModAutoplay) == true;

        private void synchroniseTimingCounts()
        {
            if (scoreProcessor == null)
                return;

            IReadOnlyList<HitEvent> hits = scoreProcessor.HitEvents;
            if (hits.Count < countedHitEvents)
                resetTimingCounts();

            while (countedHitEvents < hits.Count)
            {
                HitEvent hit = hits[countedHitEvents++];
                // Basic successful timing judgements only. This excludes misses, automatic body ticks, parent
                // hold bookkeeping and BMS empty-POOR objects, whose maximum grade is Ok rather than Perfect.
                int direction = TimingDirection(hit, automatedTiming);
                if (direction < 0)
                    fastCount++;
                else if (direction > 0)
                    slowCount++;
            }
        }

        internal static int TimingDirection(HitEvent hit, bool automated = false)
            // Autoplay may apply a judgement on the next update with a frame-sized offset. It is not a
            // measured player input, even though the original judgement offset remains available.
            => !automated && hit.HitObject.Judgement.MaxResult == HitResult.Perfect && hit.Result.IsBasic() && hit.Result.IsHit()
                // Clock-domain arithmetic can leave ~1e-12 ms on exact replay hits. Below one
                // nanosecond this is numerical residue, not an observed early/late input.
                && Math.Abs(hit.TimeOffset) >= 0.000001
                ? Math.Sign(hit.TimeOffset) : 0;

        private void resetTimingCounts()
        {
            countedHitEvents = 0;
            fastCount = slowCount = 0;
        }

        internal void PublishInputMotion(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId, double counts, double revolutions, bool available)
        {
            validateExactTarget(groupId, laneId);
            GameplaySkinInputStateSnapshot old = inputs[laneId];
            if (!available)
                counts = revolutions = 0;
            if (old.MotionCounts == counts && old.MotionRevolutions == revolutions && old.MotionAvailable == available)
                return;
            var state = new GameplaySkinInputStateSnapshot(groupId, laneId, old.IsPressed, old.Strength,
                old.PressedAt, old.ReleasedAt, counts, revolutions, available);
            inputs[laneId] = state;
            publish(AuthoritativeGameplayTime, GameplaySkinEventValue.Input(GameplaySkinEventKind.InputMotionChanged, state), groupId, laneId);
        }
    }
}
