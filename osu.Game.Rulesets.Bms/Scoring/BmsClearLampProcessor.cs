// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public static class BmsClearLampProcessor
    {
        public static BmsClearLamp Calculate(ScoreInfo score, IBeatmap playableBeatmap)
            => Calculate(score, playableBeatmap, out _);

        public static BmsClearLamp Calculate(ScoreInfo score, IBeatmap playableBeatmap, out double finalGauge)
        {
            if (TryCalculate(score, playableBeatmap, out BmsClearLamp clearLamp, out finalGauge))
                return clearLamp;

            throw new InvalidOperationException("Cannot calculate BMS clear lamp without hit events or persisted result data.");
        }

        public static bool TryCalculate(ScoreInfo score, IBeatmap playableBeatmap, out BmsClearLamp clearLamp, out double finalGauge)
        {
            if (TryGetStoredResult(score, out clearLamp, out finalGauge))
                return true;

            long exScore = BmsScoreProcessor.CalculateExScore(score.Statistics);
            long maxExScore = BmsScoreProcessor.CalculateMaxExScore(score.MaximumStatistics);

            if (exScore == 0 && maxExScore == 0 && score.HitEvents.Count == 0)
            {
                clearLamp = BmsClearLamp.NoPlay;
                finalGauge = BmsGaugeProcessor.GetStartingGauge(
                    BmsGaugeProcessor.GetStartingGaugeType(score),
                    BmsGaugeProcessor.GetGaugeRulesFamily(score),
                    BmsGaugeProcessor.ResolveKeymode(playableBeatmap));
                return true;
            }

            if (score.HitEvents.Count == 0)
            {
                clearLamp = default;
                finalGauge = default;
                return false;
            }

            clearLamp = calculateFromHitEvents(score, playableBeatmap, exScore, maxExScore, out finalGauge);
            return true;
        }

        public static BmsScoreInfoData CreatePersistentData(ScoreInfo score, IBeatmap playableBeatmap)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>() ?? new BmsScoreInfoData();

            scoreData.Version = BmsScoreInfoData.EMPTY_POOR_SEPARATION_VERSION;
            scoreData.UsesGaugeAutoShift = BmsGaugeProcessor.UsesGaugeAutoShift(score);
            scoreData.StartingGaugeType = BmsGaugeProcessor.GetStartingGaugeType(score);
            scoreData.FloorGaugeType = BmsGaugeProcessor.GetFloorGaugeType(score);
            scoreData.GaugeType = BmsGaugeProcessor.GetGaugeType(score);
            scoreData.GaugeRulesFamily = BmsGaugeProcessor.GetGaugeRulesFamily(score);
            scoreData.LongNoteMode = BmsScoreProcessor.GetLongNoteMode(score);
            scoreData.JudgeMode = BmsJudgeModeExtensions.GetJudgeMode(score);

            if (score.HitEvents.Count > 0)
            {
                long exScore = BmsScoreProcessor.CalculateExScore(score.Statistics);
                long maxExScore = BmsScoreProcessor.CalculateMaxExScore(score.MaximumStatistics);
                var gaugeProcessor = calculateGaugeState(score, playableBeatmap);

                scoreData.GaugeType = gaugeProcessor.GaugeType;
                scoreData.GaugeRulesFamily = gaugeProcessor.GaugeRulesFamily;
                scoreData.ClearLamp = calculateFromGaugeState(score, exScore, maxExScore, gaugeProcessor, out double finalGauge);
                scoreData.FinalGauge = finalGauge;
            }

            return scoreData;
        }

        public static bool TryGetStoredResult(ScoreInfo score, out BmsClearLamp clearLamp, out double finalGauge)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            if (scoreData?.HasResultStatistics != true)
            {
                clearLamp = default;
                finalGauge = default;
                return false;
            }

            clearLamp = scoreData.ClearLamp!.Value;
            finalGauge = scoreData.FinalGauge!.Value;
            return true;
        }

        public static double CalculateFinalGauge(ScoreInfo score, IBeatmap playableBeatmap)
        {
            return calculateGaugeState(score, playableBeatmap).Health.Value;
        }

        public static BmsGaugeHistory CreateGaugeHistory(ScoreInfo score, IBeatmap playableBeatmap)
        {
            BmsScoreProcessor.GetLongNoteMode(score).ApplyToBeatmap(playableBeatmap);

            double startTime = playableBeatmap.HitObjects.Count == 0 ? 0 : playableBeatmap.HitObjects.Min(hitObject => hitObject.StartTime);
            double endTime = playableBeatmap.HitObjects.Count == 0 ? 1 : playableBeatmap.HitObjects.Max(hitObject => hitObject.GetEndTime());

            if (endTime <= startTime)
                endTime = startTime + 1;

            var gaugeProcessor = BmsGaugeProcessor.CreateForScore(0, score);
            gaugeProcessor.ApplyBeatmap(playableBeatmap);

            var timelineBuilders = new List<TimelineBuilder>();
            var activeTimeline = new TimelineBuilder(gaugeProcessor.GaugeType);
            timelineBuilders.Add(activeTimeline);
            activeTimeline.AddSample(startTime, gaugeProcessor.Health.Value);

            foreach (var hitEvent in score.HitEvents)
            {
                if (hitEvent.HitObject == null)
                    continue;

                double time = Math.Clamp(hitEvent.HitObject.GetEndTime(), startTime, endTime);
                var previousGaugeType = gaugeProcessor.GaugeType;
                var judgementResult = new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = hitEvent.Result,
                };

                gaugeProcessor.ApplyResult(judgementResult);

                if (gaugeProcessor.GaugeType != previousGaugeType)
                {
                    activeTimeline.AddSample(time, 0);

                    activeTimeline = new TimelineBuilder(gaugeProcessor.GaugeType);
                    timelineBuilders.Add(activeTimeline);
                }

                activeTimeline.AddSample(time, gaugeProcessor.Health.Value);
            }

            activeTimeline.AddSample(endTime, gaugeProcessor.Health.Value);

            return new BmsGaugeHistory(startTime, endTime, timelineBuilders.Select(builder => builder.CreateTimeline()).ToArray());
        }

        private static BmsGaugeProcessor calculateGaugeState(ScoreInfo score, IBeatmap playableBeatmap)
        {
            BmsScoreProcessor.GetLongNoteMode(score).ApplyToBeatmap(playableBeatmap);

            var gaugeProcessor = BmsGaugeProcessor.CreateForScore(0, score);
            gaugeProcessor.ApplyBeatmap(playableBeatmap);

            foreach (var hitEvent in score.HitEvents)
            {
                if (hitEvent.HitObject == null)
                    continue;

                var judgementResult = new JudgementResult(hitEvent.HitObject, hitEvent.HitObject.CreateJudgement())
                {
                    Type = hitEvent.Result,
                };

                gaugeProcessor.ApplyResult(judgementResult);
            }

            return gaugeProcessor;
        }

        public static string GetDisplayName(BmsClearLamp lamp)
            => lamp switch
            {
                BmsClearLamp.NoPlay => "NO PLAY",
                BmsClearLamp.Failed => "FAILED",
                BmsClearLamp.AssistEasyClear => "ASSIST EASY CLEAR",
                BmsClearLamp.EasyClear => "EASY CLEAR",
                BmsClearLamp.NormalClear => "NORMAL CLEAR",
                BmsClearLamp.HardClear => "HARD CLEAR",
                BmsClearLamp.ExHardClear => "EX-HARD CLEAR",
                BmsClearLamp.HazardClear => "HAZARD CLEAR",
                BmsClearLamp.FullCombo => "FULL COMBO",
                BmsClearLamp.Perfect => "PERFECT",
                _ => lamp.ToString(),
            };

        private static bool isFullCombo(ScoreInfo score)
            => BmsScoreProcessor.GetEmptyPoorCount(score) == 0
               && getCount(score, HitResult.Good) == 0
               && getCount(score, HitResult.Meh) == 0
               && getCount(score, HitResult.Miss) == 0;

        private static BmsClearLamp calculateFromHitEvents(ScoreInfo score, IBeatmap playableBeatmap, long exScore, long maxExScore, out double finalGauge)
        {
            return calculateFromGaugeState(score, exScore, maxExScore, calculateGaugeState(score, playableBeatmap), out finalGauge);
        }

        private static BmsClearLamp calculateFromGaugeState(ScoreInfo score, long exScore, long maxExScore, BmsGaugeProcessor gaugeProcessor, out double finalGauge)
        {
            var gaugeType = gaugeProcessor.GaugeType;
            finalGauge = gaugeProcessor.Health.Value;

            if (maxExScore > 0 && exScore == maxExScore && BmsScoreProcessor.GetEmptyPoorCount(score) == 0)
                return BmsClearLamp.Perfect;

            if (isFullCombo(score))
                return BmsClearLamp.FullCombo;

            return BmsGaugeProcessor.MeetsClearCondition(gaugeType, gaugeProcessor.GaugeRulesFamily, gaugeProcessor.Keymode, finalGauge, gaugeProcessor.HasFailed)
                ? gaugeType.ToClearLamp()
                : BmsClearLamp.Failed;
        }

        private static int getCount(ScoreInfo score, HitResult result)
            => score.Statistics.TryGetValue(result, out int count) ? count : 0;

        private sealed class TimelineBuilder
        {
            private readonly List<BmsGaugeHistoryPoint> samples = new List<BmsGaugeHistoryPoint>();

            public readonly BmsGaugeType GaugeType;

            public TimelineBuilder(BmsGaugeType gaugeType)
            {
                GaugeType = gaugeType;
            }

            public void AddSample(double time, double value)
            {
                var sample = new BmsGaugeHistoryPoint(time, value);

                if (samples.Count > 0
                    && Math.Abs(samples[^1].Time - time) <= 0.000001
                    && Math.Abs(samples[^1].Value - value) <= 0.000001)
                    return;

                samples.Add(sample);
            }

            public BmsGaugeHistoryTimeline CreateTimeline()
                => new BmsGaugeHistoryTimeline(GaugeType, samples.ToArray());
        }
    }
}
