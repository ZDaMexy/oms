// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Where the BGA panel sits relative to the playfield. The default skin mirrors the playfield: a side-anchored
    /// playfield pushes the BGA to the opposite margin; a centred playfield defaults to the right; a 14K double-play
    /// layout places the BGA in the centre gap between the two halves.
    /// </summary>
    public enum BmsBgaPlacement
    {
        Left,
        Right,
        Center,
    }

    /// <summary>
    /// Skin contract for the BGA panel (P1-L Phase 5). A custom skin returns its own <see cref="IBmsBgaPanelDisplay"/>
    /// to fully control the BGA presentation / placement; the runtime feeds it the resolved timeline, the miss signal
    /// and the default placement derived from the playfield style.
    /// </summary>
    public interface IBmsBgaPanelDisplay
    {
        void SetBgaSource(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode);

        void SetLayout(BmsBgaPlacement placement);

        void NotifyMiss();
    }

    /// <summary>
    /// Skinnable floating BGA panel (P1-L Phase 5). Mounted in <c>DrawableBmsRuleset.Overlays</c> (above the playfield,
    /// never occluded by lanes). The default implementation plays the BGA timeline through a <see cref="BmsBgaPlayer"/>
    /// inside a letterboxed frame positioned per <see cref="BmsBgaPlacement"/>; custom skins override via
    /// <see cref="IBmsBgaPanelDisplay"/>.
    /// </summary>
    public partial class BmsBgaPanel : SkinnableDrawable
    {
        private readonly IReadOnlyList<BmsBgaTimelineEntry> timeline;
        private readonly BmsPoorBgaMode poorMode;
        private BmsBgaPlacement placement = BmsBgaPlacement.Right;

        public BmsBgaPanel(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.BgaPanel), _ => new DefaultBmsBgaPanelDisplay())
        {
            this.timeline = timeline;
            this.poorMode = poorMode;

            RelativeSizeAxes = Axes.Both;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);

            if (Drawable is IBmsBgaPanelDisplay display)
            {
                display.SetBgaSource(timeline, poorMode);
                display.SetLayout(placement);
            }
        }

        public void SetLayout(BmsBgaPlacement newPlacement)
        {
            placement = newPlacement;

            if (Drawable is IBmsBgaPanelDisplay display)
                display.SetLayout(placement);
        }

        public void NotifyMiss() => (Drawable as IBmsBgaPanelDisplay)?.NotifyMiss();

        /// <summary>
        /// Default-skin placement: mirror the playfield. A side-anchored 5K/7K playfield pushes the BGA to the opposite
        /// margin; a centred / 9K playfield defaults to the right; a 14K double-play layout uses the centre gap.
        /// </summary>
        public static BmsBgaPlacement ResolveDefaultPlacement(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            if (keymode == BmsKeymode.Key14K)
                return BmsBgaPlacement.Center;

            return style.GetAppliedStyle(keymode) switch
            {
                BmsPlayfieldStyle.P1 => BmsBgaPlacement.Right,
                BmsPlayfieldStyle.P2 => BmsBgaPlacement.Left,
                _ => BmsBgaPlacement.Right,
            };
        }
    }

    public partial class DefaultBmsBgaPanelDisplay : CompositeDrawable, IBmsBgaPanelDisplay
    {
        private Container frame = null!;
        private Container content = null!;
        private BmsBgaPlayer? player;

        private IReadOnlyList<BmsBgaTimelineEntry> timeline = Array.Empty<BmsBgaTimelineEntry>();
        private BmsPoorBgaMode poorMode = BmsPoorBgaMode.Default;
        private BmsBgaPlacement placement = BmsBgaPlacement.Right;
        private bool loaded;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        public DefaultBmsBgaPanelDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = frame = new Container
            {
                RelativePositionAxes = Axes.Both,
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 2,
                BorderColour = BmsDefaultPlayfieldPalette.MetadataPanelBorder,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                    content = new Container { RelativeSizeAxes = Axes.Both },
                },
            };

            loaded = true;
            applyLayout();
            rebuildContent();
        }

        public void SetBgaSource(IReadOnlyList<BmsBgaTimelineEntry> newTimeline, BmsPoorBgaMode newPoorMode)
        {
            timeline = newTimeline;
            poorMode = newPoorMode;

            if (loaded)
                rebuildContent();
        }

        public void SetLayout(BmsBgaPlacement newPlacement)
        {
            placement = newPlacement;

            if (loaded)
                applyLayout();
        }

        public void NotifyMiss() => player?.NotifyMiss();

        private void rebuildContent()
        {
            content.Clear();
            player = null;

            if (timeline.Count > 0)
            {
                content.Add(player = new BmsBgaPlayer(timeline, poorMode));
                frame.Alpha = 1;
                return;
            }

            // No BGA timeline: fall back to the chart's static background art so the BGA region is not just black.
            var background = workingBeatmap?.Value?.GetBackground();

            if (background != null)
            {
                content.Add(new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = background,
                });
                frame.Alpha = 1;
            }
            else
            {
                // Nothing to show at all — stay hidden so only the global gameplay background remains.
                frame.Alpha = 0;
            }
        }

        private void applyLayout()
        {
            switch (placement)
            {
                case BmsBgaPlacement.Left:
                    frame.Anchor = frame.Origin = Anchor.TopLeft;
                    frame.Size = side_size;
                    frame.Position = new osuTK.Vector2(side_inset, top_inset);
                    break;

                case BmsBgaPlacement.Center:
                    frame.Anchor = frame.Origin = Anchor.TopCentre;
                    frame.Size = center_size;
                    frame.Position = new osuTK.Vector2(0, top_inset);
                    break;

                default:
                    frame.Anchor = frame.Origin = Anchor.TopRight;
                    frame.Size = side_size;
                    frame.Position = new osuTK.Vector2(-side_inset, top_inset);
                    break;
            }
        }

        // Relative (to the full overlay) default box geometry; skins may override entirely. Top-anchored and sized at
        // ~75% of the original box so the BGA sits in the top corner clear of the playfield.
        private static readonly osuTK.Vector2 side_size = new osuTK.Vector2(0.225f, 0.30f);
        private static readonly osuTK.Vector2 center_size = new osuTK.Vector2(0.15f, 0.225f);
        private const float side_inset = 0.012f;
        private const float top_inset = 0.04f;
    }
}
