// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsConvertedScratchSampleHitObject : DrawableManiaHitObject<BmsConvertedScratchSampleHitObject>
    {
        public override bool DisplayResult => false;

        public DrawableBmsConvertedScratchSampleHitObject(BmsConvertedScratchSampleHitObject hitObject)
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

            PlaySamples();
            ApplyMinResult();
        }
    }
}
