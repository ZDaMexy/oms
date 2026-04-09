// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class BmsTimingWindows : HitWindows
    {
        private BmsJudgementSystem? judgementSystem;

        public BmsJudgementSystem JudgementSystem
        {
            get => judgementSystem ??= new OsuOdJudgementSystem();
            set => judgementSystem = value ?? throw new ArgumentNullException(nameof(value));
        }

        public double PoorWindow => JudgementSystem.Windows[4];

        public override bool IsHitResultAllowed(HitResult result)
        {
            switch (result)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                case HitResult.Good:
                case HitResult.Meh:
                case HitResult.Miss:
                    return true;
            }

            return false;
        }

        public override void SetDifficulty(double difficulty)
            => JudgementSystem.SetDifficulty(difficulty);

        public HitResult Evaluate(double offsetMs, bool isLongNoteRelease = false)
            => JudgementSystem.Evaluate(offsetMs, isLongNoteRelease);

        public double WindowFor(HitResult result, bool isLongNoteRelease)
        {
            var windows = isLongNoteRelease ? JudgementSystem.LongNoteReleaseWindows : JudgementSystem.Windows;

            switch (result)
            {
                case HitResult.Perfect:
                    return windows[0];

                case HitResult.Great:
                    return windows[1];

                case HitResult.Good:
                    return windows[2];

                case HitResult.Meh:
                    return windows[3];

                case HitResult.Miss:
                    return windows[4];

                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, null);
            }
        }

        public override double WindowFor(HitResult result)
            => WindowFor(result, isLongNoteRelease: false);
    }
}
