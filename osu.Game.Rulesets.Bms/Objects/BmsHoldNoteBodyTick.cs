// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// A fixed-quantum gauge-only tick used by HCN holds.
    /// </summary>
    public class BmsHoldNoteBodyTick : HitObject
    {
        public const double TICK_QUANTUM = 100;

        public bool CountsForGauge { get; set; }

        public override Judgement CreateJudgement() => new BmsHoldNoteBodyTickJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
