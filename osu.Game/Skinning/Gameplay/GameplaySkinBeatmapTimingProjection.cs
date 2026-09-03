// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Immutable cumulative timing projection for rulesets whose timing authority is the decoded beatmap control-point
    /// timeline. A new timing point changes cadence/signature without resetting the published beat or bar identity.
    /// </summary>
    internal sealed class GameplaySkinBeatmapTimingProjection : IGameplaySkinTimingProjection
    {
        private readonly TimingSegment[] timingSegments;
        private readonly EffectSegment[] effectSegments;

        internal GameplaySkinBeatmapTimingProjection(IBeatmap beatmap)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            IReadOnlyList<TimingControlPoint> timingPoints = beatmap.ControlPointInfo.TimingPoints;
            timingSegments = new TimingSegment[Math.Max(1, timingPoints.Count)];

            if (timingPoints.Count == 0)
            {
                TimingControlPoint fallback = TimingControlPoint.DEFAULT;
                timingSegments[0] = new TimingSegment(fallback.Time, fallback.BeatLength, fallback.TimeSignature.Numerator, 0, 0);
            }
            else
            {
                for (int i = 0; i < timingPoints.Count; i++)
                {
                    TimingControlPoint point = timingPoints[i];
                    double startBeat = 0;
                    long startBar = 0;

                    if (i > 0)
                    {
                        TimingSegment previous = timingSegments[i - 1];
                        double elapsedBeats = (point.Time - previous.StartTime) / previous.BeatLength;
                        startBeat = previous.StartBeat + elapsedBeats;

                        // A timing/signature point establishes a new measure origin, matching the engine bar-line
                        // generator. A partial preceding measure is therefore consumed exactly once.
                        if (elapsedBeats > 0)
                        {
                            double elapsedMeasures = elapsedBeats / previous.SignatureNumerator;
                            startBar = previous.StartBar + (long)Math.Ceiling(elapsedMeasures - 1e-9);
                        }
                        else
                        {
                            startBar = previous.StartBar;
                        }
                    }

                    timingSegments[i] = new TimingSegment(
                        point.Time,
                        point.BeatLength,
                        point.TimeSignature.Numerator,
                        startBeat,
                        startBar);
                }
            }

            IReadOnlyList<EffectControlPoint> effectPoints = beatmap.ControlPointInfo.EffectPoints;
            effectSegments = new EffectSegment[effectPoints.Count];

            for (int i = 0; i < effectPoints.Count; i++)
                effectSegments[i] = new EffectSegment(effectPoints[i].Time, effectPoints[i].ScrollSpeed);
        }

        public GameplaySkinTimingStateSnapshot Sample(double gameplayTime)
        {
            if (!double.IsFinite(gameplayTime))
                throw new ArgumentOutOfRangeException(nameof(gameplayTime), gameplayTime, "Gameplay time must be finite.");

            TimingSegment segment = timingSegments[findTimingSegment(gameplayTime)];
            double localBeat = (gameplayTime - segment.StartTime) / segment.BeatLength;
            double beat = segment.StartBeat + localBeat;
            long bar = segment.StartBar + (long)Math.Floor(localBeat / segment.SignatureNumerator);

            return new GameplaySkinTimingStateSnapshot(
                beat,
                Math.Max(-1, bar),
                60000 / segment.BeatLength,
                false,
                scrollMultiplierAt(gameplayTime));
        }

        private int findTimingSegment(double gameplayTime)
        {
            int low = 0;
            int high = timingSegments.Length - 1;

            while (low <= high)
            {
                int middle = low + (high - low) / 2;

                if (timingSegments[middle].StartTime <= gameplayTime)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            // TimingPointAt() deliberately uses the first timing point before the timeline begins.
            return Math.Max(0, high);
        }

        private double scrollMultiplierAt(double gameplayTime)
        {
            int low = 0;
            int high = effectSegments.Length - 1;

            while (low <= high)
            {
                int middle = low + (high - low) / 2;

                if (effectSegments[middle].StartTime <= gameplayTime)
                    low = middle + 1;
                else
                    high = middle - 1;
            }

            return high < 0 ? EffectControlPoint.DEFAULT.ScrollSpeed : effectSegments[high].ScrollMultiplier;
        }

        private readonly record struct TimingSegment(
            double StartTime,
            double BeatLength,
            int SignatureNumerator,
            double StartBeat,
            long StartBar);

        private readonly record struct EffectSegment(double StartTime, double ScrollMultiplier);
    }
}
