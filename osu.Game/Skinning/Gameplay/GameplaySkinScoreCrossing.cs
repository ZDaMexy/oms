// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinScoreCrossingComparison
    {
        Greater,
        AtLeast,
    }

    /// <summary>A stable threshold for one real score edge, independent of a node's visibility.</summary>
    public sealed class GameplaySkinScoreCrossingThreshold
    {
        internal const int MAX_THRESHOLDS = 5;

        public string SourceId => Operand.SourceId!;
        public GameplaySkinScoreCrossingComparison Comparison { get; }
        internal GameplaySkinNumericOperand Operand { get; }

        internal GameplaySkinScoreCrossingThreshold(GameplaySkinNumericOperand operand, GameplaySkinScoreCrossingComparison comparison)
        {
            Operand = operand;
            Comparison = comparison;
        }

        internal bool IsReached(long score, double threshold)
            => Comparison == GameplaySkinScoreCrossingComparison.Greater ? score > threshold : score >= threshold;

        internal static bool IsStableSource(GameplaySkinSceneBindingSource source) => source is
            GameplaySkinSceneBindingSource.Option or GameplaySkinSceneBindingSource.ScoreMaximum
            or GameplaySkinSceneBindingSource.ScorePersonalBest or GameplaySkinSceneBindingSource.ScorePersonalBestProgress
            or GameplaySkinSceneBindingSource.ScorePacemakerNext or GameplaySkinSceneBindingSource.ScorePacemakerNextProgress
            or GameplaySkinSceneBindingSource.ScoreRankAaa or GameplaySkinSceneBindingSource.ScoreRankAa or GameplaySkinSceneBindingSource.ScoreRankA
            or GameplaySkinSceneBindingSource.ScoreRankAaaProgress or GameplaySkinSceneBindingSource.ScoreRankAaProgress or GameplaySkinSceneBindingSource.ScoreRankAProgress;
    }

    /// <summary>Bounded history observed by one scene consumer. Snapshots cannot manufacture a crossing time.</summary>
    internal sealed class GameplaySkinScoreCrossingState
    {
        internal IReadOnlyList<GameplaySkinScoreCrossingThreshold> Thresholds { get; }
        private readonly double?[] values;
        private readonly double?[] origins;
        private readonly bool[] consumed;
        private long previousScore;

        internal GameplaySkinScoreCrossingState(IReadOnlyList<GameplaySkinScoreCrossingThreshold> thresholds)
        {
            Thresholds = thresholds;
            values = new double?[thresholds.Count];
            origins = new double?[thresholds.Count];
            consumed = new bool[thresholds.Count];
        }

        internal void SetBaseline(long score, double gameplayTime, bool retry, Func<GameplaySkinNumericOperand, double?> read)
        {
            for (int i = 0; i < Thresholds.Count; i++)
            {
                double? value = read(Thresholds[i].Operand);
                if (!(value is > 0) || !double.IsFinite(value.Value))
                    value = null;
                bool reached = value.HasValue && Thresholds[i].IsReached(score, value.Value);
                if (retry || !reached || value != values[i] || origins[i] > gameplayTime)
                    origins[i] = null;
                // A snapshot above the threshold establishes that it is spent, never when it was crossed.
                consumed[i] = reached;
                values[i] = value;
            }
            previousScore = score;
        }

        internal void ObserveScore(long score, double gameplayTime)
        {
            for (int i = 0; i < Thresholds.Count; i++)
            {
                if (consumed[i] || values[i] is not { } threshold || !Thresholds[i].IsReached(score, threshold))
                    continue;
                consumed[i] = true;
                if (!Thresholds[i].IsReached(previousScore, threshold))
                    origins[i] = gameplayTime;
            }
            previousScore = score;
        }

        internal double Elapsed(double gameplayTime)
        {
            double? latest = null;
            foreach (double? origin in origins)
                if (origin <= gameplayTime && (!latest.HasValue || origin > latest))
                    latest = origin;
            return latest is { } time ? gameplayTime - time : -1;
        }
    }
}
