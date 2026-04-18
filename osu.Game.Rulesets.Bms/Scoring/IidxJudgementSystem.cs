// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public class IidxJudgementSystem : BmsJudgementSystem
    {
        private static readonly double[] windows =
        {
            16.67,
            33.33,
            116.67,
            250,
            250,
        };

        public override IReadOnlyList<double> Windows => windows;

        public override void SetDifficulty(double overallDifficulty)
        {
        }

        public override HitResult Evaluate(double offsetMs, bool isLongNoteRelease = false, bool isScratch = false)
            => EvaluateOffset(offsetMs, windows);

        public override double? GetExcessivePoorEarlyWindow(bool isScratch = false) => 500;

        public override double? GetExcessivePoorLateWindow(bool isScratch = false) => 150;
    }
}
