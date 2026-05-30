// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// Sample-only mania object emitted by <see cref="Beatmaps.BmsToManiaBeatmapConverter"/> in place of BMS
    /// scratch tap / head / tail (K9 #6, #12). It plays the original keysound at the scratch lane's anchor column
    /// but does not participate in mania judgement, combo, statistics, star rating, autoplay key generation, or
    /// note-lock.
    /// </summary>
    /// <remarks>
    /// The cross-cutting contract on the mania side is that "anything whose <see cref="Judgement.MaxResult"/> does
    /// not affect combo is skipped":
    /// <see cref="Mania.Replays.ManiaAutoGenerator"/> filters by <c>canParticipateInAutoplay</c> and
    /// <see cref="Mania.UI.OrderedHitPolicy"/> filters by <c>canParticipateInLocking</c>, both keyed on
    /// <c>HitObject.Judgement.MaxResult.AffectsCombo()</c>. <see cref="IgnoreJudgement.MaxResult"/> is
    /// <c>HitResult.IgnoreHit</c>, which returns <c>false</c> from <c>AffectsCombo()</c>, so this contract is
    /// honoured transitively. If a future mania ignore-only variant ever produced a combo-affecting MaxResult, the
    /// autoplay / note-lock filters here would silently regress; the scratch-only / autoplay focused tests in
    /// BmsToManiaBeatmapConverterTest and TestSceneManiaModAutoplay are the regression guard.
    /// </remarks>
    public class BmsConvertedScratchSampleHitObject : ManiaHitObject
    {
        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
