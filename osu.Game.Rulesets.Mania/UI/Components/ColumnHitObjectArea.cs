// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.UI.Components
{
    public partial class ColumnHitObjectArea : HitPositionPaddedContainer
    {
        public readonly Container Explosions;

        public readonly Container UnderlayElements;

        internal readonly ManiaGameplaySkinFailClosedSkinnableDrawable HitTarget;

        protected override Container<Drawable> Content => content;

        private readonly Container content;

        public ColumnHitObjectArea()
        {
            AddRangeInternal(new Drawable[]
            {
                UnderlayElements = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                HitTarget = new ManiaGameplaySkinFailClosedSkinnableDrawable(
                    new ManiaSkinComponentLookup(ManiaSkinComponents.HitTarget),
                    _ => new DefaultHitTarget())
                {
                    RelativeSizeAxes = Axes.X,
                },
                content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                Explosions = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                }
            });
        }

        protected override void UpdateHitPosition()
        {
            base.UpdateHitPosition();

            if (Direction.Value == ScrollingDirection.Up)
                HitTarget.Anchor = HitTarget.Origin = Anchor.TopLeft;
            else
                HitTarget.Anchor = HitTarget.Origin = Anchor.BottomLeft;
        }
    }

    /// <summary>
    /// Keeps an arbitrary custom component only while the selected package has no valid public declaration for that
    /// component. Once public ownership exists, the host switches atomically to its known independently gateable
    /// fallback instead of hiding an indivisible custom subtree across unrelated public slots.
    /// </summary>
    internal partial class ManiaGameplaySkinFailClosedSkinnableDrawable : SkinnableDrawable
    {
        private readonly Func<ISkinComponentLookup, Drawable> closedFallback;

        public ManiaGameplaySkinFailClosedSkinnableDrawable(
            ISkinComponentLookup lookup,
            Func<ISkinComponentLookup, Drawable> closedFallback)
            : base(lookup, closedFallback)
        {
            this.closedFallback = closedFallback ?? throw new ArgumentNullException(nameof(closedFallback));
        }

        public void UseClosedFallback()
            => SetDrawable(closedFallback(ComponentLookup), true);
    }
}
