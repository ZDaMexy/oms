// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsHoldNoteHead : DrawableBmsHitObject
    {
        public DrawableBmsHoldNoteHead()
            : this(new BmsHoldNoteHead { Keymode = BmsKeymode.Key7K })
        {
        }

        public DrawableBmsHoldNoteHead(
            BmsHoldNoteHead hitObject,
            BmsGameplayLayoutSnapshot? gameplayLayoutSnapshot = null,
            GameplaySkinResolvedMaterialSet? gameplayMaterialSet = null)
            : base(hitObject, gameplayLayoutSnapshot, gameplayMaterialSet)
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
