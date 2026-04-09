// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsHoldNoteBodyTick : DrawableHitObject<BmsHoldNoteBodyTick>
    {
        public override bool DisplayResult => false;

        public DrawableBmsHoldNoteBodyTick(BmsHoldNoteBodyTick hitObject)
            : base(hitObject)
        {
            Alpha = 0;
            HandleUserInput = false;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
        }

        internal void ApplyTickResult(bool hit)
        {
            if (Judged)
                return;

            if (hit)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }
    }
}
