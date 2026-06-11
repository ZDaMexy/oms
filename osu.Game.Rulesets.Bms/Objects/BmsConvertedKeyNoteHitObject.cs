// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Audio;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// A converted playable KEY note whose keysound is routed through the shared <see cref="BmsKeysoundStore"/> (per-WAV
    /// cut), like the converted BGM / scratch samples, instead of mania's one-shot <c>PlaySamples</c>. Emitted for every
    /// playable tap note by <see cref="Beatmaps.BmsToManiaBeatmapConverter"/> so a WAV slot shared between the BGM layer
    /// and a KEY note — or the same slot retriggered across consecutive notes — cuts cleanly instead of stacking
    /// duplicate copies (the BMS-native single-store behaviour; P1-J #10 / #1).
    /// </summary>
    /// <remarks>
    /// A <see cref="Note"/> subclass that stays fully scorable / combo- and star-affecting (mania difficulty / autoplay /
    /// note-lock treat it as an ordinary note); only keysound playback is rerouted, via <see cref="IHasManiaKeysound"/>.
    /// It is <b>pooled</b> like any mania note: the converter's drawable factory does not claim it, so
    /// <c>DrawableManiaRuleset.CreateDrawableRepresentation</c> returns null and the playfield serves it a pooled
    /// <c>DrawableNote</c> from the registered <c>Note</c> pool (matched by the framework's base-type pool fallback,
    /// <c>Playfield.prepareDrawableHitObjectPool</c>). That pooled <c>DrawableNote.PlaySamples</c> sees this object as
    /// <see cref="IHasManiaKeysound"/> and plays the keysound through the hosted <see cref="IManiaKeysoundStore"/> (the
    /// shared <see cref="BmsKeysoundStore"/>). Keeping it pooled avoids a per-note non-pooled drawable on dense charts —
    /// the resolution of the earlier P1-J dense "D" allocation/retention concern. Tap notes have no nested objects, so
    /// none of the LN nested-head pooling hazard applies (#10 (a)); LN keysound routing remains future work.
    /// </remarks>
    public class BmsConvertedKeyNoteHitObject : Note, IHasManiaKeysound
    {
        /// <summary>
        /// The BMS keysound played for this KEY note, routed through the shared <see cref="BmsKeysoundStore"/>.
        /// </summary>
        public BmsKeysoundSampleInfo? KeysoundSample { get; set; }

        /// <summary>
        /// The BMS WAV slot (#WAVxx) of <see cref="KeysoundSample"/>, used as the per-WAV cut group.
        /// </summary>
        public int? KeysoundId { get; set; }

        ISampleInfo? IHasManiaKeysound.KeysoundSample => KeysoundSample;

        int? IHasManiaKeysound.KeysoundCutGroup => KeysoundId;
    }
}
