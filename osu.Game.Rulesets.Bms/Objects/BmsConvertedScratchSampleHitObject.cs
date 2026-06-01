// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Audio;
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
    /// The cross-cutting contract on the mania side is that an object participates in autoplay / note-lock when its
    /// own <see cref="Judgement.MaxResult"/> — or that of any nested object — affects combo:
    /// <see cref="Mania.Replays.ManiaAutoGenerator"/> filters by <c>canParticipateInAutoplay</c> and
    /// <see cref="Mania.UI.OrderedHitPolicy"/> filters by <c>canParticipateInLocking</c>, both keyed on
    /// <c>HitObject.Judgement.MaxResult.AffectsCombo()</c> across the object and its nested objects. This object's
    /// <see cref="IgnoreJudgement.MaxResult"/> is <c>HitResult.IgnoreHit</c> (<c>AffectsCombo()</c> is <c>false</c>)
    /// and it has no nested objects, so it is skipped — whereas a <see cref="HoldNote"/> (also IgnoreHit at the top
    /// level) survives via its combo-affecting nested head/tail. If a future mania ignore-only variant ever produced a
    /// combo-affecting MaxResult, the autoplay / note-lock filters here would silently regress; the scratch-only /
    /// autoplay focused tests in BmsToManiaBeatmapConverterTest and TestSceneManiaModAutoplay are the regression guard.
    /// </remarks>
    public class BmsConvertedScratchSampleHitObject : ManiaHitObject
    {
        /// <summary>
        /// The BMS keysound played for this scratch tap / hold head. Routed through the shared
        /// <see cref="BmsKeysoundStore"/> hosted in the converted-BMS mania playfield (J6) so it honours pause / seek
        /// and a bounded channel pool. Falls back to the object's <c>Samples</c> when no shared store is present.
        /// </summary>
        public BmsKeysoundSampleInfo? KeysoundSample { get; set; }

        /// <summary>
        /// The BMS WAV slot (#WAVxx) of <see cref="KeysoundSample"/>, used as the per-WAV cut group when playing
        /// through <see cref="BmsKeysoundStore"/>. Null leaves the playback uncut.
        /// </summary>
        public int? KeysoundId { get; set; }

        public override Judgement CreateJudgement() => new IgnoreJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
