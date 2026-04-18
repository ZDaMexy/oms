// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// The tail judgement point of a <see cref="BmsHoldNote"/>.
    /// </summary>
    public class BmsHoldNoteTailEvent : BmsHitObject
    {
        public override double MaximumJudgementOffset
            => HitWindows is BmsTimingWindows bmsTimingWindows
                ? bmsTimingWindows.WindowFor(HitResult.Miss, isLongNoteRelease: true)
                : base.MaximumJudgementOffset * BmsHoldNote.DEFAULT_RELEASE_MISS_LENIENCE;

        public override Judgement CreateJudgement() => new BmsHoldNoteTailJudgement { CountsForScore = CountsForScore };
    }
}
