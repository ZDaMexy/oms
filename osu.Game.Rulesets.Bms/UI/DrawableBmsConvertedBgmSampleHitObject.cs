// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsConvertedBgmSampleHitObject : DrawableManiaHitObject<BmsConvertedBgmSampleHitObject>
    {
        // Hosted + cached by DrawableManiaRuleset when a BMS chart is played as mania (J6): BGM plays through the
        // shared store so it honours pause / seek and a bounded channel pool, instead of a per-object one-shot sample
        // that would play through a pause. Null in isolated contexts (test scenes) -> fall back to PlaySamples().
        [Resolved(CanBeNull = true)]
        private BmsKeysoundStore? keysoundStore { get; set; }

        public override bool DisplayResult => false;

        public DrawableBmsConvertedBgmSampleHitObject(BmsConvertedBgmSampleHitObject hitObject)
            : base(hitObject)
        {
            Alpha = 0;
            Height = 1;
            RelativeSizeAxes = Axes.X;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (userTriggered || timeOffset < 0)
                return;

            playKeysound();
            ApplyMinResult();
        }

        private void playKeysound()
        {
            if (keysoundStore == null || HitObject.KeysoundSample == null)
            {
                PlaySamples();
                return;
            }

            if (HitObject.KeysoundId is int cutGroup)
                keysoundStore.Play(HitObject.KeysoundSample, 0, cutGroup);
            else
                keysoundStore.Play(HitObject.KeysoundSample, 0);
        }
    }
}
