// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;

namespace osu.Game.Rulesets.Bms.Difficulty
{
    public class BmsNoteDensityAnalyzer
    {
        public const double DefaultWindowLengthMs = 1000;
        public const double DefaultWindowStepMs = 500;

        private const double chord_tolerance_ms = 1;
        private const double chord_extra_note_bonus = 0.3;
        private const double scratch_bonus = 0.5;
        private const double ln_bonus_per_ms = 0.001;

        public BmsDensityAnalysis Analyze(IBeatmap beatmap, double windowLengthMs = DefaultWindowLengthMs, double windowStepMs = DefaultWindowStepMs)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            if (windowLengthMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowLengthMs), "Window length must be positive.");

            if (windowStepMs <= 0)
                throw new ArgumentOutOfRangeException(nameof(windowStepMs), "Window step must be positive.");

            var playableObjects = beatmap.HitObjects.OfType<BmsHitObject>()
                                       .OrderBy(hitObject => hitObject.StartTime)
                                       .ThenBy(hitObject => hitObject.LaneIndex)
                                       .ToArray();

            if (playableObjects.Length == 0)
            {
                return new BmsDensityAnalysis(Array.Empty<BmsDensityWindow>(), 0, 0, 0, 0, 0, 0);
            }

            var weightedObjects = createWeightedObjects(playableObjects);
            var windows = createWindows(weightedObjects, windowLengthMs, windowStepMs);
            var peakWindow = windows.MaxBy(window => window.NotesPerSecond);

            return new BmsDensityAnalysis(
                windows,
                playableObjects.Length,
                playableObjects.Count(hitObject => hitObject.IsScratch),
                playableObjects.Count(hitObject => hitObject is BmsHoldNote),
                peakWindow.NotesPerSecond,
                peakWindow.StartTime,
                GetPercentileDensity(windows, 0.95));
        }

        public double GetPercentileDensity(IReadOnlyList<BmsDensityWindow> windows, double percentile)
        {
            if (windows.Count == 0)
                return 0;

            percentile = Math.Clamp(percentile, 0, 1);

            var sortedDensities = windows.Select(window => window.NotesPerSecond)
                                         .OrderBy(density => density)
                                         .ToArray();

            double position = (sortedDensities.Length - 1) * percentile;
            int lowerIndex = (int)Math.Floor(position);
            int upperIndex = (int)Math.Ceiling(position);

            if (lowerIndex == upperIndex)
                return sortedDensities[lowerIndex];

            double interpolation = position - lowerIndex;
            return sortedDensities[lowerIndex] + (sortedDensities[upperIndex] - sortedDensities[lowerIndex]) * interpolation;
        }

        private static BmsDensityWindow[] createWindows(IReadOnlyList<WeightedHitObject> weightedObjects, double windowLengthMs, double windowStepMs)
        {
            double firstStartTime = weightedObjects[0].StartTime;
            double lastStartTime = weightedObjects[^1].StartTime;

            List<BmsDensityWindow> windows = new List<BmsDensityWindow>();

            int windowStartIndex = 0;
            int windowEndIndex = 0;
            double weightedNoteCount = 0;
            int normalCount = 0;
            int scratchCount = 0;
            int lnCount = 0;

            for (double windowStart = firstStartTime; windowStart <= lastStartTime; windowStart += windowStepMs)
            {
                double windowEnd = windowStart + windowLengthMs;

                while (windowEndIndex < weightedObjects.Count && weightedObjects[windowEndIndex].StartTime < windowEnd)
                {
                    var hitObject = weightedObjects[windowEndIndex++];
                    weightedNoteCount += hitObject.Weight;

                    if (hitObject.IsScratch)
                        scratchCount++;
                    else if (hitObject.IsLongNote)
                        lnCount++;
                    else
                        normalCount++;
                }

                while (windowStartIndex < windowEndIndex && weightedObjects[windowStartIndex].StartTime < windowStart)
                {
                    var hitObject = weightedObjects[windowStartIndex++];
                    weightedNoteCount -= hitObject.Weight;

                    if (hitObject.IsScratch)
                        scratchCount--;
                    else if (hitObject.IsLongNote)
                        lnCount--;
                    else
                        normalCount--;
                }

                windows.Add(new BmsDensityWindow(windowStart, windowEnd, weightedNoteCount, weightedNoteCount * 1000 / windowLengthMs, normalCount, scratchCount, lnCount));
            }

            return windows.ToArray();
        }

        private static WeightedHitObject[] createWeightedObjects(IReadOnlyList<BmsHitObject> playableObjects)
        {
            WeightedHitObject[] weightedObjects = new WeightedHitObject[playableObjects.Count];

            int chordStartIndex = 0;

            while (chordStartIndex < playableObjects.Count)
            {
                int chordEndIndex = chordStartIndex + 1;

                while (chordEndIndex < playableObjects.Count && playableObjects[chordEndIndex].StartTime - playableObjects[chordStartIndex].StartTime <= chord_tolerance_ms)
                    chordEndIndex++;

                for (int i = chordStartIndex; i < chordEndIndex; i++)
                {
                    double weight = 1;
                    var hitObject = playableObjects[i];

                    if (i > chordStartIndex)
                        weight += chord_extra_note_bonus;

                    if (hitObject.IsScratch)
                        weight += scratch_bonus;

                    if (hitObject is BmsHoldNote holdNote)
                        weight += Math.Max(0, holdNote.Duration) * ln_bonus_per_ms;

                    weightedObjects[i] = new WeightedHitObject(hitObject.StartTime, weight, hitObject.IsScratch, hitObject is BmsHoldNote);
                }

                chordStartIndex = chordEndIndex;
            }

            return weightedObjects;
        }

        private readonly record struct WeightedHitObject(double StartTime, double Weight, bool IsScratch, bool IsLongNote);
    }

    public class BmsDensityAnalysis
    {
        public IReadOnlyList<BmsDensityWindow> Windows { get; }

        public int TotalNoteCount { get; }

        public int ScratchNoteCount { get; }

        public int LnNoteCount { get; }

        public double PeakDensityNps { get; }

        public double PeakDensityMs { get; }

        public double Percentile95DensityNps { get; }

        public BmsDensityAnalysis(IReadOnlyList<BmsDensityWindow> windows, int totalNoteCount, int scratchNoteCount, int lnNoteCount, double peakDensityNps, double peakDensityMs, double percentile95DensityNps)
        {
            Windows = windows;
            TotalNoteCount = totalNoteCount;
            ScratchNoteCount = scratchNoteCount;
            LnNoteCount = lnNoteCount;
            PeakDensityNps = peakDensityNps;
            PeakDensityMs = peakDensityMs;
            Percentile95DensityNps = percentile95DensityNps;
        }
    }

    public readonly record struct BmsDensityWindow(double StartTime, double EndTime, double WeightedNoteCount, double NotesPerSecond, int NormalCount, int ScratchCount, int LnCount);
}
