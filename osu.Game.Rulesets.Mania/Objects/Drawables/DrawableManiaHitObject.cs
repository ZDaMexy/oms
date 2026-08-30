// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Objects.Drawables
{
    public abstract partial class DrawableManiaHitObject : DrawableHitObject<ManiaHitObject>
    {
        /// <summary>
        /// The <see cref="ManiaAction"/> which causes this <see cref="DrawableManiaHitObject{TObject}"/> to be hit.
        /// </summary>
        protected readonly IBindable<ManiaAction> Action = new Bindable<ManiaAction>();

        protected readonly IBindable<ScrollingDirection> Direction = new Bindable<ScrollingDirection>();

        [Resolved(canBeNull: true)]
        private ManiaGameplaySkinLaneContext layoutLaneContext { get; set; }

        public GameplaySkinLayoutSnapshot LayoutSnapshot => layoutLaneContext?.Snapshot;

        public GameplaySkinLaneId LayoutLaneId => layoutLaneContext?.Lane.LaneId;

        protected override float SamplePlaybackPosition
        {
            get
            {
                if (layoutLaneContext == null)
                    return base.SamplePlaybackPosition;

                GameplaySkinLayoutRect safe = layoutLaneContext.Snapshot.Context.SafeBounds;
                GameplaySkinLayoutRect lane = layoutLaneContext.Lane.Rect;
                return (lane.Left + lane.Width / 2 - safe.Left) / safe.Width;
            }
        }

        /// <summary>
        /// Whether this <see cref="DrawableManiaHitObject"/> can be hit, given a time value.
        /// If non-null, judgements will be ignored whilst the function returns false.
        /// </summary>
        public Func<DrawableHitObject, double, bool> CheckHittable;

        protected DrawableManiaHitObject(ManiaHitObject hitObject)
            : base(hitObject)
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader(true)]
        private void load([CanBeNull] IBindable<ManiaAction> action, [NotNull] IScrollingInfo scrollingInfo)
        {
            if (action != null)
                Action.BindTo(action);

            Direction.BindTo(scrollingInfo.Direction);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Direction.BindValueChanged(OnDirectionChanged, true);
        }

        protected virtual void OnDirectionChanged(ValueChangedEvent<ScrollingDirection> e)
        {
            Anchor = Origin = e.NewValue == ScrollingDirection.Up ? Anchor.TopCentre : Anchor.BottomCentre;
        }

        protected override void OnApply()
        {
            base.OnApply();

            if (layoutLaneContext != null
                && layoutLaneContext.Lane.TopologyEntry.GlobalLogicalIndex != HitObject.Column)
            {
                throw new InvalidOperationException("A mania object must resolve the same modded target LaneId as its production column.");
            }
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Miss:
                    this.FadeOut(150, Easing.In);
                    break;

                case ArmedState.Hit:
                    this.FadeOut();
                    break;
            }
        }

        /// <summary>
        /// Causes this <see cref="DrawableManiaHitObject"/> to get missed, disregarding all conditions in implementations of <see cref="DrawableHitObject.CheckForResult"/>.
        /// </summary>
        public virtual void MissForcefully() => ApplyMinResult();
    }

    public abstract partial class DrawableManiaHitObject<TObject> : DrawableManiaHitObject
        where TObject : ManiaHitObject
    {
        public new TObject HitObject => (TObject)base.HitObject;

        protected DrawableManiaHitObject(TObject hitObject)
            : base(hitObject)
        {
        }
    }
}
