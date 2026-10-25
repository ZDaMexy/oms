// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    public partial class GameplaySkinSceneRuntimeHost
    {
        private double sceneGameplayTime;
        private GameplaySkinChartPresentation? chartPresentation;

        private double? arrivalFor(GameplaySkinSceneRuntimeNode node)
            => chartPresentation?.ArrivalFor(node.PreparedNode.MaterialTarget?.LaneId, node.PreparedNode.MaterialTarget?.GroupId);

        private GameplaySkinInputStateSnapshot? motionForGroup(int index)
        {
            var groups = Publication.Snapshot.Context.Topology.GroupsInLogicalOrder;
            if (index >= groups.Count)
                return null;
            var lanes = groups[index].LanesInLogicalOrder;
            for (int laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
                if (inputs.TryGetValue(lanes[laneIndex].Identity.Id, out GameplaySkinInputStateSnapshot input) && input.MotionAvailable)
                    return input;
            return null;
        }

        private bool rankAvailable => chartPresentation?.UsesExScore == true && scoreState.MaximumScore > 0;

        private double rankValue(int ninths)
            => rankAvailable ? GameplaySkinChartPresentation.RankThreshold(scoreState.MaximumScore, ninths) : 0;

        private bool tryPresentationBoolean(GameplaySkinSceneBindingSource source, GameplaySkinSceneRuntimeNode node, out bool value)
        {
            switch (source)
            {
                case GameplaySkinSceneBindingSource.ScrollAvailable: value = timingState.AdjustmentState.IsAvailable; return true;
                case GameplaySkinSceneBindingSource.ScrollAdjusting: value = timingState.AdjustmentState.Adjusting; return true;
                case GameplaySkinSceneBindingSource.CoverSuddenActive: value = timingState.AdjustmentState.SuddenActive; return true;
                case GameplaySkinSceneBindingSource.CoverHiddenActive: value = timingState.AdjustmentState.HiddenActive; return true;
                case GameplaySkinSceneBindingSource.TimingStartAvailable: value = timingState.StartTime.HasValue; return true;
                case GameplaySkinSceneBindingSource.PreparationAvailable: value = timingState.Preparation.Available; return true;
                case GameplaySkinSceneBindingSource.PreparationActive: value = timingState.Preparation.Active; return true;
                case GameplaySkinSceneBindingSource.PreparationReady: value = timingState.Preparation.Ready; return true;
                case GameplaySkinSceneBindingSource.ArrivalFirstRow:
                    value = chartPresentation?.IsFirstRow(node.PreparedNode.MaterialTarget?.LaneId, node.PreparedNode.MaterialTarget?.GroupId) == true;
                    return true;
                case GameplaySkinSceneBindingSource.ArrivalAvailable: value = arrivalFor(node).HasValue; return true;
                case GameplaySkinSceneBindingSource.ArrivalVisible: value = arrivalFor(node) is double arrival && sceneGameplayTime < arrival; return true;
                case GameplaySkinSceneBindingSource.InputMotionAvailable1: value = motionForGroup(0).HasValue; return true;
                case GameplaySkinSceneBindingSource.InputMotionAvailable2: value = motionForGroup(1).HasValue; return true;
                case GameplaySkinSceneBindingSource.OffsetAvailable: value = scoreState.OffsetState.IsAvailable; return true;
                case GameplaySkinSceneBindingSource.OffsetAutomatic: value = scoreState.OffsetState.Automatic; return true;
                case GameplaySkinSceneBindingSource.ScorePersonalBestAvailable: value = chartPresentation?.PersonalBestScore.HasValue == true; return true;
                case GameplaySkinSceneBindingSource.ScoreMaximumAvailable: value = scoreState.MaximumScore > 0; return true;
                case GameplaySkinSceneBindingSource.ScoreRankAvailable: value = rankAvailable; return true;
                default: value = false; return false;
            }
        }

        private bool tryPresentationNumber(GameplaySkinSceneBindingSource source, GameplaySkinSceneRuntimeNode node, out double value)
        {
            switch (source)
            {
                case GameplaySkinSceneBindingSource.ScrollGreenNumber: value = timingState.AdjustmentState.GreenNumber; return true;
                case GameplaySkinSceneBindingSource.CoverSuddenUnits: value = timingState.AdjustmentState.SuddenUnits; return true;
                case GameplaySkinSceneBindingSource.CoverHiddenUnits: value = timingState.AdjustmentState.HiddenUnits; return true;
                case GameplaySkinSceneBindingSource.TimingTime: value = sceneGameplayTime; return true;
                case GameplaySkinSceneBindingSource.PreparationElapsed: value = timingState.Preparation.Elapsed; return true;
                case GameplaySkinSceneBindingSource.PreparationRemaining: value = timingState.Preparation.Remaining; return true;
                case GameplaySkinSceneBindingSource.PreparationReadyElapsed: value = timingState.Preparation.ReadyElapsed; return true;
                case GameplaySkinSceneBindingSource.ArrivalCueOpacity:
                    value = chartPresentation?.ArrivalCueAvailable != true ? 0
                        : timingState.ArrivalCueFadeStartTime is double fadeStart ? 1 - Math.Clamp((sceneGameplayTime - fadeStart) / 200, 0, 1) : 1;
                    return true;
                case GameplaySkinSceneBindingSource.TimingElapsed: value = timingState.StartTime is double start ? Math.Max(0, sceneGameplayTime - start) : 0; return true;
                case GameplaySkinSceneBindingSource.PresentationElapsed:
                    value = timingState.Preparation.Elapsed + (timingState.StartTime is double gameplayStart ? Math.Max(0, sceneGameplayTime - gameplayStart) : 0);
                    return true;
                case GameplaySkinSceneBindingSource.TimingBeatPhase: value = timingState.Beat - Math.Floor(timingState.Beat); return true;
                case GameplaySkinSceneBindingSource.ArrivalTime: value = arrivalFor(node) ?? 0; return true;
                case GameplaySkinSceneBindingSource.ArrivalRemaining: value = arrivalFor(node) is double arrival ? arrival - sceneGameplayTime : 0; return true;
                case GameplaySkinSceneBindingSource.InputMotionCounts1: value = motionForGroup(0)?.MotionCounts ?? 0; return true;
                case GameplaySkinSceneBindingSource.InputMotionCounts2: value = motionForGroup(1)?.MotionCounts ?? 0; return true;
                case GameplaySkinSceneBindingSource.InputMotionRevolutions1: value = motionForGroup(0)?.MotionRevolutions ?? 0; return true;
                case GameplaySkinSceneBindingSource.InputMotionRevolutions2: value = motionForGroup(1)?.MotionRevolutions ?? 0; return true;
                case GameplaySkinSceneBindingSource.JudgementCountFast: value = scoreState.Statistics.Fast; return true;
                case GameplaySkinSceneBindingSource.JudgementCountSlow: value = scoreState.Statistics.Slow; return true;
                case GameplaySkinSceneBindingSource.OffsetValue: value = scoreState.OffsetState.Milliseconds; return true;
                case GameplaySkinSceneBindingSource.ScorePersonalBest: value = chartPresentation?.PersonalBestScore ?? 0; return true;
                case GameplaySkinSceneBindingSource.ScorePersonalBestProgress:
                    value = scoreState.MaximumScore > 0 && chartPresentation?.PersonalBestScore is long best ? Math.Clamp((double)best / scoreState.MaximumScore, 0, 1) : 0;
                    return true;
                case GameplaySkinSceneBindingSource.ScoreRankAaa: value = rankValue(8); return true;
                case GameplaySkinSceneBindingSource.ScoreRankAa: value = rankValue(7); return true;
                case GameplaySkinSceneBindingSource.ScoreRankA: value = rankValue(6); return true;
                case GameplaySkinSceneBindingSource.ScoreRankAaaProgress: value = rankAvailable ? rankValue(8) / scoreState.MaximumScore : 0; return true;
                case GameplaySkinSceneBindingSource.ScoreRankAaProgress: value = rankAvailable ? rankValue(7) / scoreState.MaximumScore : 0; return true;
                case GameplaySkinSceneBindingSource.ScoreRankAProgress: value = rankAvailable ? rankValue(6) / scoreState.MaximumScore : 0; return true;
                default: value = 0; return false;
            }
        }

        private double trackClock(GameplaySkinPreparedSceneTrack track, GameplaySkinSceneRuntimeNode node)
            => presentationClock(track.ClockSource, track.ClockOptionId, node);

        private static string adjustmentModeId(GameplaySkinHiSpeedMode mode)
            => mode switch
            {
                GameplaySkinHiSpeedMode.Unspecified => "unavailable",
                GameplaySkinHiSpeedMode.Normal => "normal",
                GameplaySkinHiSpeedMode.Floating => "floating",
                GameplaySkinHiSpeedMode.Classic => "classic",
                _ => throw new ArgumentOutOfRangeException(nameof(mode)),
            };

        private double presentationClock(GameplaySkinSceneBindingSource source, string? optionId, GameplaySkinSceneRuntimeNode node)
        {
            if (tryPresentationNumber(source, node, out double presentation))
                return presentation;
            return source switch
            {
                GameplaySkinSceneBindingSource.Option => Publication.Snapshot.Context.OptionValues.TryGetValue(optionId!, out GameplaySkinOptionValue option) ? option.NumberValue : 0,
                GameplaySkinSceneBindingSource.TimingBeat => timingState.Beat,
                GameplaySkinSceneBindingSource.TimingMeasure => timingState.BarIndex,
                GameplaySkinSceneBindingSource.TimingBpm => timingState.Bpm,
                GameplaySkinSceneBindingSource.TimingProgress => timingState.Progress,
                GameplaySkinSceneBindingSource.ScrollSpeed => timingState.ScrollSpeed,
                GameplaySkinSceneBindingSource.TimingBpmMinimum => songInformation?.MinimumBpm ?? 0,
                GameplaySkinSceneBindingSource.TimingBpmMaximum => songInformation?.MaximumBpm ?? 0,
                GameplaySkinSceneBindingSource.JudgementOffset => judgementFor(node, node.PreparedNode.MaterialTarget)?.Offset ?? 0,
                GameplaySkinSceneBindingSource.ScoreValue => scoreState.Score,
                GameplaySkinSceneBindingSource.ScoreMaximum => scoreState.MaximumScore,
                GameplaySkinSceneBindingSource.ScoreProgress => scoreState.MaximumScore > 0 ? Math.Clamp((double)scoreState.Score / scoreState.MaximumScore, 0, 1) : 0,
                GameplaySkinSceneBindingSource.ScoreAccuracy => scoreState.Accuracy,
                GameplaySkinSceneBindingSource.ComboValue => scoreState.Combo,
                GameplaySkinSceneBindingSource.ComboMaximum => scoreState.MaxCombo,
                GameplaySkinSceneBindingSource.GaugeValue => scoreState.Gauge,
                GameplaySkinSceneBindingSource.GaugeMaximum => scoreState.GaugeState.Maximum,
                GameplaySkinSceneBindingSource.GaugeClearThreshold => scoreState.GaugeState.ClearThreshold,
                GameplaySkinSceneBindingSource.LiftValue => timingState.LiftState.Units,
                GameplaySkinSceneBindingSource.LiftOffset => timingState.LiftState.OffsetRatio,
                GameplaySkinSceneBindingSource.JudgementCountPerfect => scoreState.Statistics.Perfect,
                GameplaySkinSceneBindingSource.JudgementCountGreat => scoreState.Statistics.Great,
                GameplaySkinSceneBindingSource.JudgementCountGood => scoreState.Statistics.Good,
                GameplaySkinSceneBindingSource.JudgementCountOk => scoreState.Statistics.Ok,
                GameplaySkinSceneBindingSource.JudgementCountMeh => scoreState.Statistics.Meh,
                GameplaySkinSceneBindingSource.JudgementCountMiss => scoreState.Statistics.Miss,
                GameplaySkinSceneBindingSource.ComboBreaks => scoreState.Statistics.ComboBreak,
                _ => throw new InvalidOperationException("A prepared track clock must reference a numeric read-only source."),
            };
        }
    }
}
