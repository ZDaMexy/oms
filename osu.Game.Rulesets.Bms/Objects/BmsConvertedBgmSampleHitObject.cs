// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// Sample-only mania object emitted by <see cref="Beatmaps.BmsToManiaBeatmapConverter"/> in place of a BMS
    /// autoplay BGM event (<see cref="BmsBgmEvent"/>, channel <c>0x01</c>) (K11). It plays the original BGM keysound
    /// at its event time but does not participate in mania judgement, combo, statistics, star rating, autoplay key
    /// generation, or note-lock.
    /// </summary>
    /// <remarks>
    /// Carries the BGM (background autoplay) audio layer that would otherwise be dropped when a BMS chart is played in
    /// mania; pure-keysound BMS rely on it for the song body (drums / bass / backing / vocals not mapped to a playable
    /// lane). The cross-cutting "ignore-only object is skipped by autoplay + note-lock" contract is identical to
    /// <see cref="BmsConvertedScratchSampleHitObject"/>: <see cref="IgnoreJudgement.MaxResult"/> is
    /// <see cref="HitResult.IgnoreHit"/> (not combo-affecting) and — unlike a <see cref="HoldNote"/>, whose
    /// press/release live in combo-affecting nested head/tail objects — this object has no nested objects at all, so
    /// <see cref="Mania.Replays.ManiaAutoGenerator"/> (<c>canParticipateInAutoplay</c>) and
    /// <see cref="Mania.UI.OrderedHitPolicy"/> (<c>canParticipateInLocking</c>), which both test an object's own and
    /// its nested MaxResults, skip it.
    /// </remarks>
    public class BmsConvertedBgmSampleHitObject : ManiaHitObject
    {
        /// <summary>
        /// The BMS keysound played for this BGM event. Routed through the shared <see cref="BmsKeysoundStore"/> hosted
        /// in the converted-BMS mania playfield (J6) so it honours pause / seek and a bounded channel pool, rather than
        /// a per-object one-shot sample that would play through a pause. Falls back to the object's <c>Samples</c>
        /// when no shared store is present (e.g. isolated test scenes).
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
