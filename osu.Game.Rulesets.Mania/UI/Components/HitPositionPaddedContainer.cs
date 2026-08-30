// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.UI.Components
{
    public partial class HitPositionPaddedContainer : Container
    {
        protected readonly Bindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        private float hitPositionFraction;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load(ManiaGameplaySkinStageContext stageContext)
        {
            LayoutSnapshot = stageContext.Snapshot;
            Direction.Value = stageContext.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down;
            hitPositionFraction = ManiaGameplaySkinLayoutProjection.GetHitTargetInsetFraction(stageContext);
            UpdateHitPosition();
        }

        protected virtual void UpdateHitPosition()
        {
            float hitPosition = DrawHeight * hitPositionFraction;

            Padding = Direction.Value == ScrollingDirection.Up
                ? new MarginPadding { Top = hitPosition }
                : new MarginPadding { Bottom = hitPosition };
        }

        protected override void Update()
        {
            base.Update();
            UpdateHitPosition();
        }
    }
}
