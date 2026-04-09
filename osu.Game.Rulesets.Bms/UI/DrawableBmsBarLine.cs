// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsBarLine : DrawableHitObject<BmsBarLine>
    {
        public override bool DisplayResult => false;

        protected override double InitialLifetimeOffset => 2000;

        public DrawableBmsBarLine(BmsBarLine hitObject, BmsLaneLayout.Lane lane, int laneCount, BmsKeymode keymode, BmsPlayfieldLayoutProfile layoutProfile)
            : base(hitObject)
        {
            HandleUserInput = false;

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = Axes.X;
            Width = 1;
            Height = layoutProfile.BarLineHeight;

            AddInternal(new SkinnableDrawable(new BmsLaneSkinLookup(BmsLaneSkinElements.BarLine, lane.LaneIndex, laneCount, lane.IsScratch, keymode, hitObject.Major), _ => new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = BmsDefaultPlayfieldPalette.GetBarLine(hitObject.Major),
            })
            {
                RelativeSizeAxes = Axes.Both,
                CentreComponent = false,
            });
        }

        public void ApplyLayoutProfile(BmsPlayfieldLayoutProfile layoutProfile)
        {
            Height = layoutProfile.BarLineHeight;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (timeOffset >= 0)
                ApplyMaxResult();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            if (state == ArmedState.Hit || state == ArmedState.Miss)
                this.FadeOut(150).Expire();
        }
    }
}
