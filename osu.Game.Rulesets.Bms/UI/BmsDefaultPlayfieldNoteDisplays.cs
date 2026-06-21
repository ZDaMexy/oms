// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal abstract partial class DefaultBmsNoteDisplayBase : Box
    {
        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        protected DefaultBmsNoteDisplayBase(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
            RelativeSizeAxes = Axes.Both;
        }
    }

    internal sealed partial class DefaultBmsNoteDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsNoteDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Colour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLongNoteHeadDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteHeadDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLongNoteBodyDisplay : DefaultBmsNoteDisplayBase
    {
        // Idle/Holding alpha — kept slightly translucent so the body still reads as a fill beneath the solid caps.
        private const float active_alpha = 0.8f;
        // Broken (missed) alpha — faded well back so a dropped hold visibly recedes.
        private const float broken_alpha = 0.32f;
        private const double state_fade_duration = 80;

        private readonly Color4 activeColour;
        private readonly Color4 brokenColour;
        private readonly IBindable<BmsLongNoteBodyState> bodyState = new Bindable<BmsLongNoteBodyState>();

        public DefaultBmsLongNoteBodyDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            // Long-note body width: 0.5775 = 0.525 + 10% (relative to the lane width).
            Width = 0.5775f;

            // Body colour now matches the head (full note colour) instead of a darkened body tint; a broken hold
            // greys + dims toward the same colour.
            activeColour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode);
            brokenColour = BmsDefaultPlayfieldPalette.GetLongNoteBodyBroken(laneIndex, isScratch, keymode);

            Alpha = active_alpha;
            Colour = activeColour;
        }

        [BackgroundDependencyLoader(true)]
        private void load(DrawableHitObject? drawableObject)
        {
            // In gameplay the body is mounted under its DrawableBmsHoldNote, whose live BodyState drives the look.
            // Outside gameplay (skin fallback queries) there is no parent, so the body stays in its Idle look.
            if (drawableObject is DrawableBmsHoldNote holdNote)
                bodyState.BindTo(holdNote.BodyState);

            bodyState.BindValueChanged(onStateChanged, true);
        }

        private void onStateChanged(ValueChangedEvent<BmsLongNoteBodyState> state)
        {
            // Unactivated and activated look identical; only a broken (missed) hold changes — fading and greying out.
            bool broken = state.NewValue == BmsLongNoteBodyState.Broken;

            this.FadeColour(broken ? brokenColour : activeColour, state_fade_duration, Easing.OutQuint);
            this.FadeTo(broken ? broken_alpha : active_alpha, state_fade_duration, Easing.OutQuint);
        }
    }

    internal sealed partial class DefaultBmsLongNoteTailDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteTailDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Colour = BmsDefaultPlayfieldPalette.GetLongNoteTail(laneIndex, isScratch, keymode);

            // No distinct tail end-cap marker: the long-note body already spans the full hold up to the
            // release end, so the default tail renders nothing (tail-less long-note look). Tail judgement
            // is unaffected — this is the visual element only.
            Alpha = 0;
        }
    }
}
