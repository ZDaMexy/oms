// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class HitTargetInsetContainer : Container
    {
        private readonly Bindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        protected override Container<Drawable> Content => content;
        private readonly Container content;

        private float hitPositionFraction;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public HitTargetInsetContainer()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = content = new Container { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(ManiaGameplaySkinStageContext stageContext)
        {
            LayoutSnapshot = stageContext.Snapshot;
            hitPositionFraction = ManiaGameplaySkinLayoutProjection.GetHitTargetInsetFraction(stageContext);
            direction.Value = stageContext.Snapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up
                ? ScrollingDirection.Up
                : ScrollingDirection.Down;
            updatePosition();
        }

        private void updatePosition()
        {
            float hitPosition = DrawHeight * hitPositionFraction;
            content.Padding = direction.Value == ScrollingDirection.Up
                ? new MarginPadding { Top = hitPosition }
                : new MarginPadding { Bottom = hitPosition };
        }

        protected override void Update()
        {
            base.Update();
            updatePosition();
        }
    }
}
