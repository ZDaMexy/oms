// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Shared visual-state host for programmatic and source-bound BMS long-note bodies.
    /// </summary>
    /// <remarks>
    /// Gameplay remains the sole owner of <see cref="BmsLongNoteBodyState"/>. This host only projects the read-only
    /// state exposed by <see cref="DrawableBmsHoldNote"/> onto body tint and opacity; it never inspects judgements,
    /// input, timing or long-note mode.
    /// </remarks>
    internal abstract partial class BmsLongNoteBodyVisualHost : CompositeDrawable
    {
        private const float active_alpha = 0.8f;
        private const float broken_alpha = 0.32f;
        private const double state_fade_duration = 80;

        private readonly IBindable<BmsLongNoteBodyState> bodyState = new Bindable<BmsLongNoteBodyState>();

        private Drawable? visual;
        private Color4 activeColour;
        private Color4 brokenColour;

        protected BmsLongNoteBodyVisualHost()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            RelativeSizeAxes = Axes.Both;
            Alpha = active_alpha;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            if (drawableObject is DrawableBmsHoldNote holdNote)
                bodyState.BindTo(holdNote.BodyState);

            // Initial hydration must already match the live hold when an asynchronously prepared body is published.
            // Only later gameplay-state changes use the compatibility 80 ms transition.
            applyState(bodyState.Value, animate: false);
            bodyState.BindValueChanged(onStateChanged, false);
        }

        /// <summary>
        /// Atomically replaces the body material and applies the currently bound visual state before publication.
        /// </summary>
        protected void ApplyMaterial(Drawable newVisual, float width, Color4 newActiveColour)
        {
            ArgumentNullException.ThrowIfNull(newVisual);

            if (!float.IsFinite(width) || width <= 0 || width > 1)
                throw new ArgumentOutOfRangeException(nameof(width), width, "A resolved long-note body width must be finite and in the range (0, 1].");

            Width = width;
            activeColour = newActiveColour;
            brokenColour = BmsDefaultPlayfieldPalette.GreyOutBroken(activeColour);

            newVisual.RelativeSizeAxes = Axes.Both;
            newVisual.Size = Vector2.One;
            visual = newVisual;
            InternalChild = newVisual;

            applyState(bodyState.Value, animate: false);
        }

        private void onStateChanged(ValueChangedEvent<BmsLongNoteBodyState> state)
            => applyState(state.NewValue, animate: true);

        private void applyState(BmsLongNoteBodyState state, bool animate)
        {
            if (visual == null)
                return;

            bool broken = state == BmsLongNoteBodyState.Broken;
            Color4 targetColour = broken ? brokenColour : activeColour;
            float targetAlpha = broken ? broken_alpha : active_alpha;

            visual.ClearTransforms(false, nameof(visual.Colour));
            ClearTransforms(false, nameof(Alpha));

            if (animate)
            {
                visual.FadeColour(targetColour, state_fade_duration, Easing.OutQuint);
                this.FadeTo(targetAlpha, state_fade_duration, Easing.OutQuint);
            }
            else
            {
                visual.Colour = targetColour;
                Alpha = targetAlpha;
            }
        }
    }

    /// <summary>
    /// Source-bound static or animated body whose texture and width were resolved from one exact package revision.
    /// </summary>
    internal sealed partial class BmsSourceBoundLongNoteBodyDrawable : BmsLongNoteBodyVisualHost
    {
        public BmsSourceBoundLongNoteBodyDrawable(Drawable visual, float width, Color4? activeColour = null)
        {
            ApplyMaterial(visual, width, activeColour ?? Color4.White);
        }
    }
}
