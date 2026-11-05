// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    public partial class GameplaySkinSceneRuntimeHost
    {
        private readonly Dictionary<string, GameplaySkinScoreCrossingState> scoreCrossings = new(StringComparer.Ordinal);
        private GameplaySkinNumericSourceRuntime? scoreCrossingNumbers;
        private Func<GameplaySkinNumericOperand, double?>? scoreCrossingFactReader;
        private Func<GameplaySkinNumericOperand, double?>? scoreCrossingThresholdReader;
        private long scoreCrossingMaximum;

        private void initialiseScoreCrossings()
        {
            foreach (var pair in PreparedScene.Program.ScoreCrossings)
                scoreCrossings.Add(pair.Key, new GameplaySkinScoreCrossingState(pair.Value));
        }

        private void resetScoreCrossings(double gameplayTime, bool retry)
        {
            if (scoreCrossings.Count == 0)
                return;
            scoreCrossingMaximum = scoreState.MaximumScore;
            scoreCrossingFactReader ??= readNumericSource;
            scoreCrossingThresholdReader ??= readScoreCrossingThreshold;
            scoreCrossingNumbers ??= new GameplaySkinNumericSourceRuntime(PreparedScene.Program.NumericSources.Declarations);
            scoreCrossingNumbers.Evaluate(scoreCrossingFactReader);
            foreach (GameplaySkinScoreCrossingState state in scoreCrossings.Values)
                state.SetBaseline(scoreState.Score, gameplayTime, retry, scoreCrossingThresholdReader);
        }

        private void observeScoreCrossings(GameplaySkinEventRecord envelope)
        {
            if (scoreCrossings.Count == 0 || envelope.EventKind != GameplaySkinEventKind.ScoreChanged)
                return;
            if (scoreCrossingMaximum != scoreState.MaximumScore)
            {
                // A newly available or changed chart maximum is metadata, not a score crossing.
                resetScoreCrossings(envelope.GameplayTime, false);
                return;
            }
            foreach (GameplaySkinScoreCrossingState state in scoreCrossings.Values)
                state.ObserveScore(scoreState.Score, envelope.GameplayTime);
        }

        private double? readScoreCrossingThreshold(GameplaySkinNumericOperand operand)
            => operand.ReferenceIndex >= 0
                ? scoreCrossingNumbers!.TryGetValue(operand.ReferenceIndex, out double value) ? value : null
                : readNumericSource(operand);
    }
}
