// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public abstract class BmsJudgementSystem
    {
        public abstract IReadOnlyList<double> Windows { get; }

        public virtual IReadOnlyList<double> LongNoteReleaseWindows => Windows;

        public abstract void SetDifficulty(double overallDifficulty);

        public abstract HitResult Evaluate(double offsetMs, bool isLongNoteRelease);

        protected static void SetLongNoteReleaseWindows(IReadOnlyList<double> sourceWindows, double[] targetWindows, double missLenience)
        {
            for (int i = 0; i < targetWindows.Length && i < sourceWindows.Count; i++)
                targetWindows[i] = i == 4 ? sourceWindows[i] * missLenience : sourceWindows[i];
        }

        protected static HitResult EvaluateOffset(double offsetMs, IReadOnlyList<double> windows)
        {
            double absoluteOffset = Math.Abs(offsetMs);

            if (absoluteOffset <= windows[0])
                return HitResult.Perfect;

            if (absoluteOffset <= windows[1])
                return HitResult.Great;

            if (absoluteOffset <= windows[2])
                return HitResult.Good;

            if (absoluteOffset <= windows[3])
                return HitResult.Meh;

            if (absoluteOffset <= windows[4])
                return HitResult.Miss;

            return HitResult.None;
        }
    }
}
