// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// The full-screen playfield backdrop. Shows the beatmap background blurred + dimmed across the whole surface
    /// (the same look as song select) so it fills the margins around the playfield instead of a flat dark box; the
    /// opaque play strip (baseplate + lanes) and the BGA panel still draw on top. Falls back to the flat dark backdrop
    /// colour when the beatmap has no background.
    /// </summary>
    internal sealed partial class DefaultBmsPlayfieldBackdropDisplay : CompositeDrawable
    {
        // Song select blurs the beatmap background at sigma 20 (SongSelect.cs). The buffered container is rendered at
        // half resolution, so the stored sigma is halved to cover the same pixel block (see Background.blurSigma).
        private const float blur_sigma = 10f;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        public DefaultBmsPlayfieldBackdropDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var background = workingBeatmap?.Value?.GetBackground();

            if (background == null)
            {
                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = BmsDefaultPlayfieldPalette.PlayfieldBackdrop,
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

    internal sealed partial class DefaultBmsPlayfieldBaseplateDisplay : Box
    {
        public DefaultBmsPlayfieldBaseplateDisplay()
        {
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.PlayfieldBaseplate;
        }
    }

    internal sealed partial class DefaultBmsLaneBackgroundDisplay : Box
    {
        public int LaneIndex { get; }

        public bool IsScratch { get; }

        public DefaultBmsLaneBackgroundDisplay(int laneIndex, bool isScratch, BmsKeymode keymode = BmsKeymode.Key7K)
        {
            LaneIndex = laneIndex;
            IsScratch = isScratch;
            RelativeSizeAxes = Axes.Both;
            Colour = BmsDefaultPlayfieldPalette.GetLaneBackground(laneIndex, isScratch, keymode);
        }
    }

    internal sealed partial class DefaultBmsLaneDividerDisplay : Box
    {
        public bool IsScratch { get; }

        public DefaultBmsLaneDividerDisplay(bool isScratch)
        {
            IsScratch = isScratch;
            Anchor = Anchor.CentreRight;
            Origin = Anchor.CentreRight;
            RelativeSizeAxes = Axes.Y;
            Width = 1;
            Colour = BmsDefaultPlayfieldPalette.GetLaneDivider(isScratch);
        }
    }
}
