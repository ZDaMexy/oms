// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Screens.Play;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Where the BGA panel sits. The default skin uses a screen corner: 5/7/9K mirror the playfield side (P1 → top-right,
    /// P2 → top-left); 14K (double play, which fills the screen width) also defaults to a corner (top-right, compact size)
    /// instead of the centre gap. All four corners are available; <see cref="Center"/> remains for skin overrides.
    /// </summary>
    public enum BmsBgaPlacement
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
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
        private BmsBgaPlacement placement = BmsBgaPlacement.TopRight;

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
        /// Default-skin placement: a screen corner mirroring the playfield side. 5/7/9K → P1 top-right / P2 top-left;
        /// 14K (fills the screen width) also defaults to the top-right corner (rendered compact so it clears the lanes)
        /// rather than the centre gap.
        /// </summary>
        public static BmsBgaPlacement ResolveDefaultPlacement(BmsKeymode keymode, BmsPlayfieldStyle style)
        {
            if (keymode == BmsKeymode.Key14K)
                return BmsBgaPlacement.TopRight;

            return style.GetAppliedStyle(keymode) switch
            {
                BmsPlayfieldStyle.P1 => BmsBgaPlacement.TopRight,
                BmsPlayfieldStyle.P2 => BmsBgaPlacement.TopLeft,
                _ => BmsBgaPlacement.TopRight,
            };
        }
    }

    public partial class DefaultBmsBgaPanelDisplay : CompositeDrawable, IBmsBgaPanelDisplay
    {
        // 14K (double play) mirrors the BGA into all four corners; everything else uses a single corner.
        private static readonly BmsBgaPlacement[] all_corners =
        {
            BmsBgaPlacement.TopLeft, BmsBgaPlacement.TopRight, BmsBgaPlacement.BottomLeft, BmsBgaPlacement.BottomRight,
        };

        private Container framesContainer = null!;
        private readonly List<BmsBgaPlayer> players = new List<BmsBgaPlayer>();

        private IReadOnlyList<BmsBgaTimelineEntry> timeline = Array.Empty<BmsBgaTimelineEntry>();
        private BmsPoorBgaMode poorMode = BmsPoorBgaMode.Default;
        private BmsBgaPlacement placement = BmsBgaPlacement.TopRight;
        private bool loaded;
        private bool is14K;

        [Resolved(CanBeNull = true)]
        private GameplayState? gameplayState { get; set; }

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        public DefaultBmsBgaPanelDisplay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            // Resolve the keymode from the (reliable) playable beatmap so 14K renders the four-corner compact layout.
            is14K = gameplayState != null && BmsLaneLayout.CreateFor(gameplayState.Beatmap).Keymode == BmsKeymode.Key14K;

            InternalChild = framesContainer = new Container { RelativeSizeAxes = Axes.Both };

            loaded = true;
            rebuild();
        }

        public void SetBgaSource(IReadOnlyList<BmsBgaTimelineEntry> newTimeline, BmsPoorBgaMode newPoorMode)
        {
            timeline = newTimeline;
            poorMode = newPoorMode;

            if (loaded)
                rebuild();
        }

        public void SetLayout(BmsBgaPlacement newPlacement)
        {
            placement = newPlacement;

            if (loaded)
                rebuild();
        }

        public void NotifyMiss()
        {
            foreach (var player in players)
                player.NotifyMiss();
        }

        private void rebuild()
        {
            framesContainer.Clear();
            players.Clear();

            var background = timeline.Count == 0 ? workingBeatmap?.Value?.GetBackground() : null;
            var placements = is14K ? all_corners : new[] { placement };

            foreach (var framePlacement in placements)
                framesContainer.Add(createFrame(framePlacement, background));
        }

        private Container createFrame(BmsBgaPlacement framePlacement, Texture? background)
        {
            var content = new Container { RelativeSizeAxes = Axes.Both };
            bool hasContent = false;

            if (timeline.Count > 0)
            {
                var player = new BmsBgaPlayer(timeline, poorMode);
                players.Add(player);
                content.Add(player);
                hasContent = true;
            }
            else if (background != null)
            {
                content.Add(new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Texture = background,
                });
                hasContent = true;
            }

            (Anchor anchor, osuTK.Vector2 position, osuTK.Vector2 size) = resolveLayout(framePlacement);

            return new Container
            {
                Anchor = anchor,
                Origin = anchor,
                RelativePositionAxes = Axes.Both,
                RelativeSizeAxes = Axes.Both,
                Size = size,
                Position = position,
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 2,
                BorderColour = BmsDefaultPlayfieldPalette.MetadataPanelBorder,
                Alpha = hasContent ? 1 : 0,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                    content,
                },
            };
        }

        private (Anchor anchor, osuTK.Vector2 position, osuTK.Vector2 size) resolveLayout(BmsBgaPlacement framePlacement)
        {
            if (framePlacement == BmsBgaPlacement.Center)
                return (Anchor.TopCentre, new osuTK.Vector2(0, top_inset), center_size);

            // 14K corners use the smallest size so all four fit the narrow double-play side margins without covering lanes.
            var size = is14K ? corner_14k_size : side_size;

            return framePlacement switch
            {
                BmsBgaPlacement.TopLeft => (Anchor.TopLeft, new osuTK.Vector2(side_inset, top_inset), size),
                BmsBgaPlacement.BottomLeft => (Anchor.BottomLeft, new osuTK.Vector2(side_inset, -bottom_inset), size),
                BmsBgaPlacement.BottomRight => (Anchor.BottomRight, new osuTK.Vector2(-side_inset, -bottom_inset), size),
                _ => (Anchor.TopRight, new osuTK.Vector2(-side_inset, top_inset), size),
            };
        }

        // Relative (to the full overlay) default box geometry; skins may override entirely. 5/7/9K use one side corner;
        // 14K mirrors a compact BGA into all four corners (small enough to fit the narrow double-play side margins).
        private static readonly osuTK.Vector2 side_size = new osuTK.Vector2(0.225f, 0.30f);
        private static readonly osuTK.Vector2 center_size = new osuTK.Vector2(0.15f, 0.225f);
        private static readonly osuTK.Vector2 corner_14k_size = new osuTK.Vector2(0.13f, 0.16f);
        private const float side_inset = 0.012f;
        private const float top_inset = 0.04f;
        private const float bottom_inset = 0.06f;
    }
}
