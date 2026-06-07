// Copyright (c) OMS contributors. Licensed under the MIT Licence.

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
    /// A <see cref="Note"/> subclass: it remains fully scorable / combo- and star-affecting (mania difficulty / autoplay
    /// / note-lock treat it as a note), so only keysound playback is rerouted. Because mania pools by exact type
    /// (<c>Column.RegisterPool&lt;Note, DrawableNote&gt;</c>), this subclass is NOT pooled — it goes through
    /// <c>DrawableManiaRuleset.CreateDrawableRepresentation</c> to <see cref="UI.DrawableBmsConvertedKeyNote"/>.
    /// PERF CAVEAT: that makes every converted tap note a non-pooled drawable (on top of the already non-pooled BGM /
    /// scratch sample objects), which adds per-alive allocation on dense charts — a known follow-up for the P1-J dense
    /// "D" perf line if it regresses; the pooling-preserving alternative would route a plain pooled DrawableNote through
    /// the store from mania core. Tap notes have no nested objects, so this avoids the LN nested-head crash (#10 (a)).
    /// </remarks>
    public class BmsConvertedKeyNoteHitObject : Note
    {
        /// <summary>
        /// The BMS keysound played for this KEY note, routed through the shared <see cref="BmsKeysoundStore"/>.
        /// </summary>
        public BmsKeysoundSampleInfo? KeysoundSample { get; set; }

        /// <summary>
        /// The BMS WAV slot (#WAVxx) of <see cref="KeysoundSample"/>, used as the per-WAV cut group.
        /// </summary>
        public int? KeysoundId { get; set; }
    }
}
