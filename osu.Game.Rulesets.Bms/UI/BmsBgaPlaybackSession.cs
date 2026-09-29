// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// The presentation-only connection to one gameplay-owned BGA session. A display may create views and read
    /// content state, but cannot replace media, seek, trigger POOR, or dispose the playback owner.
    /// </summary>
    internal interface IBmsBgaContentSource
    {
        bool HasContent { get; }

        GameplaySkinBgaContentState ContentState { get; }

        long ContentRevision { get; }

        Drawable CreateView(GameplaySkinBgaScaleMode scaleMode);

        void ReleaseView(Drawable view);

        void GetContentStateAt(double gameplayTime, out GameplaySkinBgaContentState state, out long revision);
    }

    /// <summary>
    /// Owns the only timeline player and composited surface for one BMS gameplay root. Skin displays own only views
    /// of this surface, so changing their lifetime or layout cannot restart a decoder or lose an active POOR flash.
    /// </summary>
    internal partial class BmsBgaPlaybackSession : CompositeDrawable, IBmsBgaContentSource
    {
        private const float bga_canvas_aspect_ratio = 4f / 3f;

        private readonly BmsBgaCaptureSurface capture;
        private readonly List<BufferedContainerView<Drawable>> views = new List<BufferedContainerView<Drawable>>();
        private bool hasBackground;
        private float contentAspectRatio = bga_canvas_aspect_ratio;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        internal BmsBgaPlayer? Player { get; }

        internal bool PresentationEnabled { get; set; } = true;

        internal long SurfaceRedrawCount { get; private set; }

        public bool HasContent => Player != null || hasBackground;

        public GameplaySkinBgaContentState ContentState => Player?.ContentState
                                                         ?? (hasBackground ? GameplaySkinBgaContentState.Ready : GameplaySkinBgaContentState.Empty);

        public long ContentRevision => Player?.ContentRevision ?? 0;

        public BmsBgaPlaybackSession(IReadOnlyList<BmsBgaTimelineEntry> timeline, BmsPoorBgaMode poorMode)
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;
            // The offscreen source must be rendered before every visible view, including a replacement display.
            Depth = 1;

            InternalChild = capture = new BmsBgaCaptureSurface();

            if (timeline.Count > 0)
                capture.Add(Player = new BmsBgaPlayer(timeline, poorMode));
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (Player != null)
                return;

            var background = workingBeatmap?.Value.GetBackground();
            hasBackground = background != null;

            if (background != null)
            {
                contentAspectRatio = background.DisplayWidth / background.DisplayHeight;
                capture.Add(new Sprite { RelativeSizeAxes = Axes.Both, Texture = background });
            }
        }

        public Drawable CreateView(GameplaySkinBgaScaleMode scaleMode)
        {
            BufferedContainerView<Drawable> view = capture.CreateView();
            view.SynchronisedDrawQuad = false;
            view.RelativeSizeAxes = Axes.Both;
            view.Anchor = Anchor.Centre;
            view.Origin = Anchor.Centre;
            view.FillAspectRatio = contentAspectRatio;
            view.FillMode = scaleMode switch
            {
                GameplaySkinBgaScaleMode.Fit => FillMode.Fit,
                GameplaySkinBgaScaleMode.Fill => FillMode.Fill,
                GameplaySkinBgaScaleMode.Stretch => FillMode.Stretch,
                _ => throw new ArgumentOutOfRangeException(nameof(scaleMode)),
            };
            views.Add(view);
            return view;
        }

        // Display disposal may run off the update thread. Keep registry mutation on its owner thread.
        public void ReleaseView(Drawable view) => Schedule(() => views.Remove((BufferedContainerView<Drawable>)view));

        public void GetContentStateAt(double gameplayTime, out GameplaySkinBgaContentState state, out long revision)
        {
            if (Player != null)
                Player.GetContentStateAt(gameplayTime, out state, out revision);
            else
            {
                state = ContentState;
                revision = 0;
            }
        }

        internal void NotifyMiss() => Player?.NotifyMiss();

        protected override void Update()
        {
            base.Update();

            // A hidden panel still advances its one gameplay clock and content state. It needs neither a GPU
            // redraw nor a separate decoder. A skin with no BGA windows has the same presentation behaviour.
            capture.Alpha = PresentationEnabled && HasContent && views.Count > 0 ? 1 : 0;
            if (capture.Alpha == 0)
                return;

            float requiredWidth = 1;
            foreach (BufferedContainerView<Drawable> view in views)
            {
                // A static STAGEFILE may have become available during asynchronous load. Timeline compositions
                // always retain the BMS 4:3 canvas; author Fit/Fill/Stretch applies to that complete composition.
                view.FillAspectRatio = contentAspectRatio;
                requiredWidth = Math.Max(requiredWidth, Math.Max(view.DrawWidth, view.DrawHeight * contentAspectRatio));
            }

            // Match the largest projected view instead of allocating a screen-sized buffer for a small corner.
            capture.Size = new Vector2(requiredWidth, requiredWidth / contentAspectRatio);
            capture.ForceRedraw();
            SurfaceRedrawCount++;
        }

        private sealed partial class BmsBgaCaptureSurface : BufferedContainer
        {
            // Cached framebuffers normally stop updating children after rendering. The gameplay timeline and
            // video clock must keep advancing even while presentation is hidden or has no authored windows.
            protected override bool RequiresChildrenUpdate => true;

            // Views only blit an already-rendered buffer. Ignore screen bounds for this offscreen source, but
            // suppress its whole draw subtree when presentation is hidden; child clocks still update above.
            protected override bool ComputeIsMaskedAway(RectangleF maskingBounds) => Alpha == 0;

            public BmsBgaCaptureSurface()
                : base(cachedFrameBuffer: true)
            {
                RelativePositionAxes = Axes.X;
                X = 2;
                Size = new Vector2(bga_canvas_aspect_ratio, 1);
                AlwaysPresent = true;
            }
        }
    }
}
