// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsHoldNoteHead : DrawableBmsHitObject
    {
        public DrawableBmsHoldNoteHead(BmsHoldNoteHead hitObject)
            : base(hitObject)
        {
            HandleUserInput = false;
        }

        protected override void OnApply()
        {
            base.OnApply();
            HandleUserInput = false;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
        }

        internal void ApplyHeadResult(HitResult result)
        {
            if (!Judged)
                ApplyResult(result);
        }
    }
}
