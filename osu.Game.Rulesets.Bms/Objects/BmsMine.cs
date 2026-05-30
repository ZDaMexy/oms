// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// A landmine (channel D/E) rendered as a non-judged, non-scoring visual object on a lane.
    /// <para>
    /// Mines are intentionally NOT part of <see cref="osu.Game.Beatmaps.Beatmap{T}.HitObjects"/>. They are added
    /// directly to their lane like bar lines (see <c>BmsPlayfield</c>), and use <see cref="IgnoreJudgement"/> with
    /// empty hit windows, so they never enter the scoring / statistics / judged-note path and cannot affect the
    /// normal gameplay chain.
    /// </para>
    /// </summary>
    public class BmsMine : HitObject
    {
        public int LaneIndex { get; set; }

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;

        public override Judgement CreateJudgement() => new IgnoreJudgement();
    }
}
