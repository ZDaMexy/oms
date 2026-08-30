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
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
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

    public interface IBmsBgaPanelLayoutDisplay
    {
        void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot);
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
        private readonly BmsGameplayLayoutProvider layoutProvider;
        private BmsBgaPlacement placement = BmsBgaPlacement.TopRight;

        public BmsGameplayLayoutSnapshot LayoutSnapshot => layoutProvider.Current;

        public BmsBgaPanel(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode, BmsGameplayLayoutProvider layoutProvider)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.BgaPanel), _ => new DefaultBmsBgaPanelDisplay(layoutProvider))
        {
            this.timeline = timeline;
            this.poorMode = poorMode;
            this.layoutProvider = layoutProvider ?? throw new ArgumentNullException(nameof(layoutProvider));

            RelativeSizeAxes = Axes.Both;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);

            if (Drawable is IBmsBgaPanelDisplay display)
            {
                if (Drawable is not IBmsBgaPanelLayoutDisplay layoutDisplay)
                    throw new InvalidOperationException("bms.layout.bga-display-missing-snapshot-carrier");

                layoutDisplay.InitialiseLayoutSnapshot(layoutProvider.Current);
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

    public partial class DefaultBmsBgaPanelDisplay : CompositeDrawable, IBmsBgaPanelDisplay, IBmsBgaPanelLayoutDisplay
    {
        private Container framesContainer = null!;
        private readonly List<BmsBgaPlayer> players = new List<BmsBgaPlayer>();

        private IReadOnlyList<BmsBgaTimelineEntry> timeline = Array.Empty<BmsBgaTimelineEntry>();
        private BmsPoorBgaMode poorMode = BmsPoorBgaMode.Default;
        private BmsBgaPlacement placement = BmsBgaPlacement.TopRight;
        private bool loaded;
        private readonly BmsGameplayLayoutProvider? layoutProvider;

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        public DefaultBmsBgaPanelDisplay(BmsGameplayLayoutProvider? layoutProvider = null)
        {
            this.layoutProvider = layoutProvider;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BmsGameplayLayoutSnapshot? resolvedSnapshot = layoutProvider?.Current;

            if (LayoutSnapshot != null && resolvedSnapshot != null && !ReferenceEquals(LayoutSnapshot, resolvedSnapshot))
                throw new InvalidOperationException("A BMS BGA display cannot change its immutable layout snapshot.");

            LayoutSnapshot ??= resolvedSnapshot;

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
        }

        public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (LayoutSnapshot != null && !ReferenceEquals(LayoutSnapshot, snapshot))
                throw new InvalidOperationException("A BMS BGA display cannot change its immutable layout snapshot.");

            LayoutSnapshot = snapshot;

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

            if (LayoutSnapshot == null)
                return;

            var background = timeline.Count == 0 ? workingBeatmap?.Value?.GetBackground() : null;

            foreach (GameplaySkinLayoutRect viewport in LayoutSnapshot.BgaViewports)
                framesContainer.Add(createFrame(viewport, background));
        }

        private Container createFrame(GameplaySkinLayoutRect viewport, Texture? background)
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

            return new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                RelativePositionAxes = Axes.Both,
                RelativeSizeAxes = Axes.Both,
                Size = new osuTK.Vector2(viewport.Width, viewport.Height),
                Position = new osuTK.Vector2(viewport.X, viewport.Y),
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

    }
}
