// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Drawable for <see cref="BmsConvertedKeyNoteHitObject"/>: plays the KEY note's keysound through the shared
    /// <see cref="BmsKeysoundStore"/> (per-WAV cut) instead of mania's one-shot <c>DrawableHitObject.PlaySamples</c>, so
    /// the keysound participates in the same cross-channel cut as BGM / scratch. Non-pooled (a <c>Note</c> subclass goes
    /// through <c>CreateDrawableRepresentation</c>, not mania's exact-type pool); see the perf caveat on
    /// <see cref="BmsConvertedKeyNoteHitObject"/>. Tap notes have no nested objects, so this avoids the LN nested-head
    /// crash that constraint (a) forbids.
    /// </summary>
    public partial class DrawableBmsConvertedKeyNote : DrawableNote
    {
        [Resolved(CanBeNull = true)]
        private BmsKeysoundStore? keysoundStore { get; set; }

        public DrawableBmsConvertedKeyNote(BmsConvertedKeyNoteHitObject hitObject)
            : base(hitObject)
        {
        }

        public override void PlaySamples()
        {
            var keyNote = (BmsConvertedKeyNoteHitObject)HitObject;

            if (keysoundStore == null || keyNote.KeysoundSample == null)
            {
                // No shared store (isolated context) or no keysound: fall back to mania's one-shot playback.
                base.PlaySamples();
                return;
            }

            double balance = CalculateSamplePlaybackBalance(SamplePlaybackPosition);

            if (keyNote.KeysoundId is int cutGroup)
                keysoundStore.Play(keyNote.KeysoundSample, balance, cutGroup);
            else
                keysoundStore.Play(keyNote.KeysoundSample, balance);
        }
    }
}
