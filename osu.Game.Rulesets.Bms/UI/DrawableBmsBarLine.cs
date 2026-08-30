// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    public partial class DrawableBmsBarLine : DrawableHitObject<BmsBarLine>
    {
        public override bool DisplayResult => false;

        protected override double InitialLifetimeOffset => 2000;

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        public DrawableBmsBarLine(
            BmsBarLine hitObject,
            BmsLaneLayout.Lane lane,
            int laneCount,
            BmsKeymode keymode,
            BmsPlayfieldLayoutProfile layoutProfile,
            BmsGameplayLayoutLane? layoutSnapshotLane = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null)
            : base(hitObject)
        {
            LayoutSnapshot = layoutSnapshot;
            HandleUserInput = false;

            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
            RelativeSizeAxes = layoutSnapshot == null ? Axes.X : Axes.Both;
            Width = 1;
            Height = layoutSnapshot?.ProjectVerticalProfileMetric(layoutProfile.BarLineHeight)
                     ?? layoutProfile.BarLineHeight;

            AddInternal(new SkinnableDrawable(new BmsLaneSkinLookup(
                    BmsLaneSkinElements.BarLine,
                    lane.LaneIndex,
                    laneCount,
                    lane.IsScratch,
                    keymode,
                    hitObject.Major,
                    layoutSnapshotLane?.LaneId),
                _ => new DefaultBmsBarLineDisplay(hitObject.Major, keymode))
            {
                RelativeSizeAxes = Axes.Both,
                CentreComponent = false,
            });
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
