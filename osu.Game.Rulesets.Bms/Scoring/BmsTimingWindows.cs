// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class BmsTimingWindows : HitWindows
    {
        private BmsJudgementSystem? judgementSystem;

        public bool IsScratch { get; set; }

        public BmsJudgementSystem JudgementSystem
        {
            get => judgementSystem ??= new OsuOdJudgementSystem();
            set => judgementSystem = value ?? throw new ArgumentNullException(nameof(value));
        }

        public double PoorWindow => WindowFor(HitResult.Miss, isLongNoteRelease: false);

        public bool SupportsExcessivePoor
            => JudgementSystem.GetExcessivePoorEarlyWindow(IsScratch).HasValue
               && JudgementSystem.GetExcessivePoorLateWindow(IsScratch).HasValue;

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
            => JudgementSystem.Evaluate(offsetMs, isLongNoteRelease, IsScratch);

        public bool CanTriggerExcessivePoor(double offsetMs)
            => JudgementSystem.CanTriggerExcessivePoor(offsetMs, IsScratch);

        public double WindowFor(HitResult result, bool isLongNoteRelease)
            => JudgementSystem.GetMaximumWindow(result, isLongNoteRelease, IsScratch);

        public override double WindowFor(HitResult result)
            => WindowFor(result, isLongNoteRelease: false);
    }
}
