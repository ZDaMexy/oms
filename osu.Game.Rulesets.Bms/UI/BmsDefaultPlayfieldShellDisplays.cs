// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// The full-screen playfield backdrop. A skin-supplied <c>PlayfieldBackdropImage</c> takes precedence; otherwise it
    /// shows the beatmap background blurred + dimmed across the whole surface (the same look as song select) so it fills
    /// the margins around the playfield instead of a flat dark box; the opaque play strip (baseplate + lanes) and the BGA
    /// panel still draw on top. Falls back to the skin's flat <c>PlayfieldBackdropColour</c> / palette when there is no
    /// background image at all.
    /// </summary>
    internal sealed partial class DefaultBmsPlayfieldBackdropDisplay : CompositeDrawable
    {
        // Song select blurs the beatmap background at sigma 20 (SongSelect.cs). The buffered container is rendered at
        // half resolution, so the stored sigma is halved to cover the same pixel block (see Background.blurSigma).
        private const float blur_sigma = 10f;

        private readonly BmsKeymode keymode;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        public DefaultBmsPlayfieldBackdropDisplay(BmsKeymode keymode)
        {
            this.keymode = keymode;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // A skin backdrop image owns the look (no beatmap-background blur).
            string? imagePath = skin.GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.PlayfieldBackdropImage, keymode)?.Value;
            var skinTexture = !string.IsNullOrEmpty(imagePath) ? skin.GetTexture(imagePath) : null;

            if (skinTexture != null)
            {
                InternalChild = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fill,
                    Texture = skinTexture,
                };
                return;
            }

            var background = workingBeatmap?.Value?.GetBackground();

            if (background == null)
            {
                var flat = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.PlayfieldBackdropColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.PlayfieldBackdrop;
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = flat,
                };
                return;
            }

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                new BufferedContainer(cachedFrameBuffer: true)
                {
                    RelativeSizeAxes = Axes.Both,
                    RedrawOnScale = false,
                    FrameBufferScale = new Vector2(0.5f),
                    BlurSigma = new Vector2(blur_sigma),
                    Child = new Sprite
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                        Texture = background,
                    },
                },
                // Dim so notes stay readable while keeping the background clearly visible (song-select feel).
                new Box { RelativeSizeAxes = Axes.Both, Colour = new Color4(0f, 0f, 0f, 0.4f) },
            };
        }
    }

    /// <summary>The opaque play strip behind the lanes. Colour-only: reads the skin's <c>PlayfieldBaseplateColour</c> override or the palette default.</summary>
    internal sealed partial class DefaultBmsPlayfieldBaseplateDisplay : Box
    {
        private readonly BmsKeymode keymode;

        public DefaultBmsPlayfieldBaseplateDisplay(BmsKeymode keymode)
        {
            this.keymode = keymode;
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.PlayfieldBaseplate;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var configured = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.PlayfieldBaseplateColour, keymode)?.Value;
            if (configured.HasValue)
                Colour = configured.Value;
        }
    }

    /// <summary>
    /// A single lane's background. Resolves to a user-skin texture (<c>LaneBackgroundImage{lane}</c>) when one is
    /// supplied, otherwise a flat box using the skin's even/odd/scratch colour override or the palette default.
    /// </summary>
    internal sealed partial class DefaultBmsLaneBackgroundDisplay : CompositeDrawable
    {
        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        public DefaultBmsLaneBackgroundDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
            RelativeSizeAxes = Axes.Both;
            // Programmatic default; ApplyVisual replaces it with a texture / colour-override visual under a real skin.
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLaneBackground(laneIndex, isScratch, keymode) };
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
            => InternalChild = BmsSkinnableVisual.Resolve(skin, BmsSkinConfigurationLookups.LaneBackgroundImage,
                BmsDefaultPlayfieldPalette.GetLaneBackgroundLookup(LaneIndex, IsScratch, Keymode), Keymode,
                BmsDefaultPlayfieldPalette.GetLaneBackground(LaneIndex, IsScratch, Keymode), out _, LaneIndex, IsScratch);
    }

    /// <summary>
    /// A lane's right-edge divider line. Resolves to a user-skin texture (<c>LaneDividerImage{lane}</c>) when one is
    /// supplied, otherwise a flat box using the skin's scratch / non-scratch colour override or the palette default.
    /// </summary>
    internal sealed partial class DefaultBmsLaneDividerDisplay : CompositeDrawable
    {
        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public BmsKeymode Keymode { get; }

        public DefaultBmsLaneDividerDisplay(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            Keymode = keymode;
            Anchor = Anchor.CentreRight;
            Origin = Anchor.CentreRight;
            RelativeSizeAxes = Axes.Y;
            Width = 1;
            InternalChild = new Box { RelativeSizeAxes = Axes.Both, Colour = BmsDefaultPlayfieldPalette.GetLaneDivider(isScratch) };
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
            => InternalChild = BmsSkinnableVisual.Resolve(skin, BmsSkinConfigurationLookups.LaneDividerImage,
                IsScratch ? BmsSkinConfigurationLookups.ScratchLaneDividerColour : BmsSkinConfigurationLookups.LaneDividerColour, Keymode,
                BmsDefaultPlayfieldPalette.GetLaneDivider(IsScratch), out _, LaneIndex, IsScratch);
    }

    /// <summary>
    /// A measure / beat bar line. Colour-only: reads the skin's major / minor bar-line colour override or the palette
    /// default (there is no bar-line texture slot — height is owned by the layout profile on the parent bar line).
    /// </summary>
    internal sealed partial class DefaultBmsBarLineDisplay : Box
    {
        public bool IsMajor { get; }

        public BmsKeymode Keymode { get; }

        public DefaultBmsBarLineDisplay(bool isMajor, BmsKeymode keymode)
        {
            IsMajor = isMajor;
            Keymode = keymode;
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.GetBarLine(isMajor);
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            var configured = skin.GetBmsSkinConfig<Color4>(
                IsMajor ? BmsSkinConfigurationLookups.MajorBarLineColour : BmsSkinConfigurationLookups.MinorBarLineColour, Keymode)?.Value;
            if (configured.HasValue)
                Colour = configured.Value;
        }
    }
}
