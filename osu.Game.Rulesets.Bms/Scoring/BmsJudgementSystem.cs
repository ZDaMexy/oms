// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public abstract class BmsJudgementSystem
    {
        public const double BoundaryEpsilon = 1e-7;

        public abstract IReadOnlyList<double> Windows { get; }

        public virtual IReadOnlyList<double> LongNoteReleaseWindows => Windows;

        public virtual IReadOnlyList<double> ScratchWindows => Windows;

        public virtual IReadOnlyList<double> LongScratchReleaseWindows => LongNoteReleaseWindows;

        public virtual double GetEarlyWindow(HitResult result, bool isLongNoteRelease = false, bool isScratch = false)
            => getWindow(result, getWindows(isLongNoteRelease, isScratch));

        public virtual double GetLateWindow(HitResult result, bool isLongNoteRelease = false, bool isScratch = false)
            => getWindow(result, getWindows(isLongNoteRelease, isScratch));

        public virtual double GetMaximumWindow(HitResult result, bool isLongNoteRelease = false, bool isScratch = false)
            => Math.Max(GetEarlyWindow(result, isLongNoteRelease, isScratch), GetLateWindow(result, isLongNoteRelease, isScratch));

        public virtual double? GetExcessivePoorEarlyWindow(bool isScratch = false) => null;

        public virtual double? GetExcessivePoorLateWindow(bool isScratch = false) => null;

        public virtual bool CanTriggerExcessivePoor(double offsetMs, bool isScratch = false)
        {
            double? earlyWindow = GetExcessivePoorEarlyWindow(isScratch);
            double? lateWindow = GetExcessivePoorLateWindow(isScratch);

            return earlyWindow.HasValue
                   && lateWindow.HasValue
                   && offsetMs >= -earlyWindow.Value - BoundaryEpsilon
                   && offsetMs <= lateWindow.Value + BoundaryEpsilon;
        }

        public abstract void SetDifficulty(double overallDifficulty);

        public abstract HitResult Evaluate(double offsetMs, bool isLongNoteRelease = false, bool isScratch = false);

        protected static void SetLongNoteReleaseWindows(IReadOnlyList<double> sourceWindows, double[] targetWindows, double missLenience)
        {
            for (int i = 0; i < targetWindows.Length && i < sourceWindows.Count; i++)
                targetWindows[i] = i == 4 ? sourceWindows[i] * missLenience : sourceWindows[i];
        }

        protected static HitResult EvaluateOffset(double offsetMs, IReadOnlyList<double> windows)
        {
            double absoluteOffset = Math.Abs(offsetMs);

            if (absoluteOffset <= windows[0] + BoundaryEpsilon)
                return HitResult.Perfect;

            if (absoluteOffset <= windows[1] + BoundaryEpsilon)
                return HitResult.Great;

            if (absoluteOffset <= windows[2] + BoundaryEpsilon)
                return HitResult.Good;

            if (absoluteOffset <= windows[3] + BoundaryEpsilon)
                return HitResult.Meh;

            if (absoluteOffset <= windows[4] + BoundaryEpsilon)
                return HitResult.Miss;

            return HitResult.None;
        }

        private IReadOnlyList<double> getWindows(bool isLongNoteRelease, bool isScratch)
            => isLongNoteRelease
                ? isScratch ? LongScratchReleaseWindows : LongNoteReleaseWindows
                : isScratch ? ScratchWindows : Windows;

        private static double getWindow(HitResult result, IReadOnlyList<double> windows)
            => result switch
            {
                HitResult.Perfect => windows[0],
                HitResult.Great => windows[1],
                HitResult.Good => windows[2],
                HitResult.Meh => windows[3],
                HitResult.Miss => windows[4],
                _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
            };
    }
}
