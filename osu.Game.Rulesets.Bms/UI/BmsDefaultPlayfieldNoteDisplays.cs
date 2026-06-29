// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Base for the programmatic-default note visuals. Resolves to a user-skin texture when one is supplied (the file
    /// skin then owns the look — no programmatic colour tint), otherwise a flat box using the skin's colour override or
    /// the palette default. This keeps the "asset skin = sprite, no asset = colour/palette fallback" layering.
    /// </summary>
    internal abstract partial class DefaultBmsNoteDisplayBase : CompositeDrawable
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

        /// <summary>The per-lane texture key for this element (e.g. <c>NoteImage</c>, <c>HoldNoteHeadImage</c>).</summary>
        protected abstract BmsSkinConfigurationLookups ImageLookup { get; }

        /// <summary>Programmatic fallback colour used by the box visual when the skin supplies neither texture nor colour.</summary>
        protected abstract Color4 DefaultColour { get; }

        /// <summary>The colour-group config key for the box fallback (defaults to this lane's note colour group).</summary>
        protected virtual BmsSkinConfigurationLookups ColourLookup => BmsDefaultPlayfieldPalette.GetNoteColourLookup(LaneIndex, IsScratch, Keymode);

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin) => ApplyVisual(skin);

        /// <summary>Mounts the resolved visual. Overridden where the element needs extra behaviour (tail hide, LN body states).</summary>
        protected virtual void ApplyVisual(ISkinSource skin) => InternalChild = CreateVisual(skin, out _);

        /// <summary>
        /// Resolves the visual via <see cref="BmsSkinnableVisual"/>: a user-skin texture (sprite, owns the look) takes
        /// precedence; otherwise a flat box using the skin colour override or the palette default.
        /// </summary>
        protected Drawable CreateVisual(ISkinSource skin, out bool hasTexture)
            => BmsSkinnableVisual.Resolve(skin, ImageLookup, ColourLookup, Keymode, DefaultColour, out hasTexture, LaneIndex, IsScratch);
    }

    internal sealed partial class DefaultBmsNoteDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsNoteDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            // Programmatic default visual; ApplyVisual replaces it with a texture/config visual under a real skin.
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode) };
        }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.NoteImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetNote(LaneIndex, IsScratch, Keymode);
    }

    internal sealed partial class DefaultBmsLongNoteHeadDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteHeadDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode) };
        }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.HoldNoteHeadImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetLongNoteHead(LaneIndex, IsScratch, Keymode);
    }

    internal sealed partial class DefaultBmsLongNoteBodyDisplay : DefaultBmsNoteDisplayBase
    {
        // Idle/Holding alpha — kept slightly translucent so the body still reads as a fill beneath the solid caps.
        private const float active_alpha = 0.8f;
        // Broken (missed) alpha — faded well back so a dropped hold visibly recedes.
        private const float broken_alpha = 0.32f;
        private const double state_fade_duration = 80;
        // Long-note body width relative to the lane (0.525 + 10%); skinnable via the LongNoteBodyWidth geometry key.
        private const float default_body_width = 0.5775f;

        private Drawable visual = null!;
        private Color4 activeColour;
        private Color4 brokenColour;
        private readonly IBindable<BmsLongNoteBodyState> bodyState = new Bindable<BmsLongNoteBodyState>();

        [Resolved(CanBeNull = true)]
        private DrawableHitObject? drawableObject { get; set; }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.HoldNoteBodyImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetLongNoteHead(LaneIndex, IsScratch, Keymode);

        public DefaultBmsLongNoteBodyDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Width = default_body_width;
            Alpha = active_alpha;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode) };
        }

        protected override void ApplyVisual(ISkinSource skin)
        {
            visual = CreateVisual(skin, out bool hasTexture);
            InternalChild = visual;

            // Body width is the one LN geometry knob that lives on the element, not the layout profile.
            Width = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.LongNoteBodyWidth, Keymode)?.Value ?? default_body_width;

            // With a texture the sprite owns the colour (active = white tint); broken just greys/dims it. With the box
            // fallback, active follows the colour override / palette and broken is its greyed derivative.
            activeColour = hasTexture
                ? Color4.White
                : skin.GetBmsSkinConfig<Color4>(ColourLookup, Keymode)?.Value ?? DefaultColour;
            brokenColour = BmsDefaultPlayfieldPalette.GreyOutBroken(activeColour);

            Alpha = active_alpha;
            visual.Colour = activeColour;

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

            visual.FadeColour(broken ? brokenColour : activeColour, state_fade_duration, Easing.OutQuint);
            this.FadeTo(broken ? broken_alpha : active_alpha, state_fade_duration, Easing.OutQuint);
        }
    }

    internal sealed partial class DefaultBmsLongNoteTailDisplay : DefaultBmsNoteDisplayBase
    {
        public DefaultBmsLongNoteTailDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
            : base(laneIndex, isScratch, keymode)
        {
            // No distinct tail end-cap by default: the body spans the full hold up to the release end (tail-less look).
            // Tail judgement is unaffected — this is the visual element only.
            Alpha = 0;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLongNoteTail(laneIndex, isScratch, keymode) };
        }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.HoldNoteTailImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetLongNoteTail(LaneIndex, IsScratch, Keymode);

        protected override void ApplyVisual(ISkinSource skin)
        {
            // Only show when the skin supplies a tail texture; otherwise stay hidden (Alpha 0 from the ctor).
            var resolved = CreateVisual(skin, out bool hasTexture);

            if (hasTexture)
            {
                InternalChild = resolved;
                Alpha = 1;
            }
        }
    }
}
