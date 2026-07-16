// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
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
        private readonly bool allowAggregateTextureOverride;

        public DefaultBmsNoteDisplay(int laneIndex, bool isScratch, BmsKeymode keymode, bool allowAggregateTextureOverride = true)
            : base(laneIndex, isScratch, keymode)
        {
            this.allowAggregateTextureOverride = allowAggregateTextureOverride;
            // Programmatic default visual; ApplyVisual replaces it with a texture/config visual under a real skin.
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode) };
        }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.NoteImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetNote(LaneIndex, IsScratch, Keymode);

        protected override void ApplyVisual(ISkinSource skin)
        {
            if (allowAggregateTextureOverride)
            {
                base.ApplyVisual(skin);
                return;
            }

            // A managed selected package has already had its exact ordinary-note declaration resolved by its own
            // source-bound provider. The migration fallback must not query that name again through the aggregate source,
            // where a lower provider could accidentally satisfy it. Colour is a separate scalar slot and remains
            // compatible while OmsSkin is still the migration-chain bottom.
            Color4 colour = skin.GetBmsSkinConfig<Color4>(ColourLookup, Keymode)?.Value ?? DefaultColour;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = colour };
        }
    }

    internal sealed partial class DefaultBmsLongNoteHeadDisplay : DefaultBmsNoteDisplayBase
    {
        private readonly bool allowAggregateTextureOverride;

        public DefaultBmsLongNoteHeadDisplay(int laneIndex, bool isScratch, BmsKeymode keymode, bool allowAggregateTextureOverride = true)
            : base(laneIndex, isScratch, keymode)
        {
            this.allowAggregateTextureOverride = allowAggregateTextureOverride;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode) };
        }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.HoldNoteHeadImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetLongNoteHead(LaneIndex, IsScratch, Keymode);

        protected override void ApplyVisual(ISkinSource skin)
        {
            if (allowAggregateTextureOverride)
            {
                base.ApplyVisual(skin);
                return;
            }

            // The selected managed package's exact head declaration has already been resolved by its source-bound
            // provider. Do not let an aggregate lookup fill a rejected declaration from a lower same-named texture.
            // Colour remains a separate scalar fallback while the programmatic palette is still in the chain.
            Color4 colour = skin.GetBmsSkinConfig<Color4>(ColourLookup, Keymode)?.Value ?? DefaultColour;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = colour };
        }
    }

    internal sealed partial class DefaultBmsLongNoteBodyDisplay : BmsLongNoteBodyVisualHost
    {
        private readonly bool allowAggregateResourceAndGeometryOverride;

        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        private BmsSkinConfigurationLookups colourLookup => BmsDefaultPlayfieldPalette.GetNoteColourLookup(LaneIndex, IsScratch, Keymode);
        private Color4 defaultColour => BmsDefaultPlayfieldPalette.GetLongNoteHead(LaneIndex, IsScratch, Keymode);

        public DefaultBmsLongNoteBodyDisplay(
            int laneIndex,
            bool isScratch,
            BmsKeymode keymode,
            bool allowAggregateResourceAndGeometryOverride = true)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
            this.allowAggregateResourceAndGeometryOverride = allowAggregateResourceAndGeometryOverride;

            Color4 colour = BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode);
            ApplyMaterial(
                new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                BmsGameplaySkinScalarGeometryResolver.DEFAULT_LONG_NOTE_BODY_WIDTH,
                colour);
        }

        [BackgroundDependencyLoader]
        private void loadDefaultMaterial(ISkinSource skin)
        {
            if (!allowAggregateResourceAndGeometryOverride)
            {
                // The selected package's exact body declaration and geometry have already been resolved by its
                // source-bound provider. A protected migration fallback must not fill either from the aggregate chain.
                // Colour remains an independent scalar fallback while OmsSkin is still the migration-chain bottom.
                Color4 colour = skin.GetBmsSkinConfig<Color4>(colourLookup, Keymode)?.Value ?? defaultColour;
                ApplyMaterial(
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colour },
                    BmsGameplaySkinScalarGeometryResolver.DEFAULT_LONG_NOTE_BODY_WIDTH,
                    colour);
                return;
            }

            Drawable visual = BmsSkinnableVisual.Resolve(
                skin,
                BmsSkinConfigurationLookups.HoldNoteBodyImage,
                colourLookup,
                Keymode,
                defaultColour,
                out bool hasTexture,
                LaneIndex,
                IsScratch);

            var configuredWidth = skin.GetBmsSkinConfig<float>(BmsSkinConfigurationLookups.LongNoteBodyWidth, Keymode);
            GameplaySkinConfigurationDeclaration<float> widthDeclaration = configuredWidth == null
                ? GameplaySkinConfigurationDeclaration<float>.Absent
                : GameplaySkinConfigurationDeclaration<float>.Declared(configuredWidth.Value);
            float width = BmsGameplaySkinScalarGeometryResolver.Resolve(
                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                widthDeclaration).Value;
            Color4 activeColour = hasTexture
                ? Color4.White
                : skin.GetBmsSkinConfig<Color4>(colourLookup, Keymode)?.Value ?? defaultColour;

            ApplyMaterial(visual, width, activeColour);
        }
    }

    internal sealed partial class DefaultBmsLongNoteTailDisplay : DefaultBmsNoteDisplayBase
    {
        private readonly bool allowAggregateTextureOverride;

        public DefaultBmsLongNoteTailDisplay(int laneIndex, bool isScratch, BmsKeymode keymode, bool allowAggregateTextureOverride = true)
            : base(laneIndex, isScratch, keymode)
        {
            this.allowAggregateTextureOverride = allowAggregateTextureOverride;
            // No distinct tail end-cap by default: the body spans the full hold up to the release end (tail-less look).
            // Tail judgement is unaffected — this is the visual element only.
            Alpha = 0;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLongNoteTail(laneIndex, isScratch, keymode) };
        }

        protected override BmsSkinConfigurationLookups ImageLookup => BmsSkinConfigurationLookups.HoldNoteTailImage;
        protected override Color4 DefaultColour => BmsDefaultPlayfieldPalette.GetLongNoteTail(LaneIndex, IsScratch, Keymode);

        protected override void ApplyVisual(ISkinSource skin)
        {
            if (!allowAggregateTextureOverride)
            {
                // A rejected exact declaration must not be filled from a lower aggregate texture. The programmatic
                // tail-less visual remains a real migration fallback (not author suppression), but is transparent.
                Alpha = 0;
                return;
            }

            // Only show when the skin supplies a tail texture; otherwise stay hidden (Alpha 0 from the ctor).
            var resolved = CreateVisual(skin, out bool hasTexture);

            if (hasTexture)
            {
                InternalChild = resolved;
                Alpha = 1;
            }
            else
            {
                resolved.Dispose();
                Alpha = 0;
            }
        }
    }
}
