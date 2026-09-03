// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Compatibility skin contract for the BGA panel (P1-L Phase 5). Outside an exact C5 scene publication, a custom
    /// skin may return its own <see cref="IBmsBgaPanelDisplay"/> and receive the resolved timeline, miss signal and
    /// default placement. Exact C5 gameplay requires the closed engine state/surface seam below; an opaque custom
    /// display fails closed to the built-in player so public viewport/frame authoring cannot take P1-L authority.
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
    /// Optional read-only state surface implemented by a BGA display which can report the content it actually owns.
    /// It deliberately exposes neither the BGA timeline nor playback controls to gameplay-skin consumers.
    /// </summary>
    internal interface IBmsBgaPanelStateSource
    {
        bool TryGetContentState(int viewportIndex, out GameplaySkinBgaContentState state, out long contentRevision);

        bool TryGetContentStateAt(
            int viewportIndex,
            double gameplayTime,
            out GameplaySkinBgaContentState state,
            out long contentRevision);
    }

    /// <summary>
    /// Closed exact-publication seam implemented by the built-in BGA display. The skinnable lookup can construct that
    /// display through the compatibility transformer rather than the fallback factory, so the owning panel supplies
    /// the already-committed context explicitly after selection. No package or resource lookup occurs here.
    /// </summary>
    internal interface IBmsBgaPanelGameplaySkinDisplay
    {
        void InitialiseGameplaySkinPublication(
            BmsGameplayLayoutProvider layoutProvider,
            GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime);
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
        private readonly GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime;
        private BmsBgaPlacement placement = BmsBgaPlacement.TopRight;

        public BmsGameplayLayoutSnapshot LayoutSnapshot => layoutProvider.Current;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => layoutProvider.CurrentMaterialSet;

        public BmsBgaPanel(
            IReadOnlyList<BmsBgaTimelineEntry> timeline,
            BmsPoorBgaMode poorMode,
            BmsGameplayLayoutProvider layoutProvider,
            GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime = null)
            : base(new BmsSkinComponentLookup(BmsSkinComponents.BgaPanel),
                _ => new DefaultBmsBgaPanelDisplay(layoutProvider, gameplaySkinSceneRuntime))
        {
            this.timeline = timeline;
            this.poorMode = poorMode;
            this.layoutProvider = layoutProvider ?? throw new ArgumentNullException(nameof(layoutProvider));
            this.gameplaySkinSceneRuntime = gameplaySkinSceneRuntime;

            RelativeSizeAxes = Axes.Both;
            CentreComponent = false;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);

            // An arbitrary legacy BGA display owns one opaque content tree: hiding it for a public viewport/frame
            // replacement would also hide P1-L playback, while mounting decorations without a state/surface seam
            // would advertise a host that cannot preserve the content contract. Exact C5 gameplay therefore fails
            // closed to the protected engine display. Isolated compatibility previews (no scene runtime) retain the
            // historical custom display behaviour.
            if (requiresClosedGameplaySkinDisplay() && Drawable is not IBmsBgaPanelGameplaySkinDisplay)
            {
                SetDrawable(
                    new DefaultBmsBgaPanelDisplay(layoutProvider, gameplaySkinSceneRuntime),
                    replacementIsDefault: true);
            }

            if (Drawable is IBmsBgaPanelDisplay display)
            {
                if (Drawable is IBmsBgaPanelGameplaySkinDisplay gameplaySkinDisplay)
                    gameplaySkinDisplay.InitialiseGameplaySkinPublication(layoutProvider, gameplaySkinSceneRuntime);

                if (Drawable is not IBmsBgaPanelLayoutDisplay layoutDisplay)
                    throw new InvalidOperationException("bms.layout.bga-display-missing-snapshot-carrier");

                layoutDisplay.InitialiseLayoutSnapshot(layoutProvider.Current);
                display.SetBgaSource(timeline, poorMode);
                display.SetLayout(placement);
            }
        }

        private bool requiresClosedGameplaySkinDisplay()
        {
            GameplaySkinSceneRuntimeHost? runtime = gameplaySkinSceneRuntime;

            if (runtime == null)
                return false;

            // The protected fallback is the canonical exact C5 production package. It intentionally has no authored
            // resource declaration, but its BGA slots are still owned by the closed engine display; allowing an
            // opaque legacy component here would let it retain P1-L playback authority while an author frame/viewport
            // is absent. A selected managed/realm/external package with no BGA declaration remains the explicit
            // compatibility case and may retain its legacy display until that package authors a BGA slot.
            if (layoutProvider.Current.Context.PackageRevision.SourceKind == GameplaySkinPackageSourceKind.ProtectedFallback)
                return true;

            foreach (GameplaySkinSlotDescriptor descriptor in new[]
                     {
                         GameplaySkinSlotCatalog.BgaViewport,
                         GameplaySkinSlotCatalog.BgaFrame,
                     })
            {
                var key = new GameplaySkinResolvedMaterialKey(descriptor, GameplaySkinResolvedMaterialTarget.Global);

                if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact BMS BGA scene gate is missing from its committed publication.");

                if (gate.RoutedNodes.Count != 0)
                    return true;

                GameplaySkinResolvedMaterialEntry entry = gate.PreparedRoute.Entry;

                if (entry.State == GameplaySkinResolvedMaterialState.Suppress
                    || entry.Material is GameplaySkinPublicSlotMaterial { IsProgrammaticFallback: false })
                {
                    return true;
                }
            }

            GameplaySkinPreparedSceneProgram program = runtime.PreparedScene.Program;

            return program.UsesBindingSource(GameplaySkinSceneBindingSource.BgaContentState)
                   || program.UsesEvent(GameplaySkinSceneEvent.BgaState);
        }

        public void SetLayout(BmsBgaPlacement newPlacement)
        {
            placement = newPlacement;

            if (Drawable is IBmsBgaPanelDisplay display)
                display.SetLayout(placement);
        }

        public void NotifyMiss() => (Drawable as IBmsBgaPanelDisplay)?.NotifyMiss();

        internal bool TryGetContentState(int viewportIndex, out GameplaySkinBgaContentState state, out long contentRevision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(viewportIndex);

            if (Drawable is IBmsBgaPanelStateSource source)
                return source.TryGetContentState(viewportIndex, out state, out contentRevision);

            state = GameplaySkinBgaContentState.Empty;
            contentRevision = 0;
            return false;
        }

        internal bool TryGetContentStateAt(
            int viewportIndex,
            double gameplayTime,
            out GameplaySkinBgaContentState state,
            out long contentRevision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(viewportIndex);

            if (Drawable is IBmsBgaPanelStateSource source)
                return source.TryGetContentStateAt(viewportIndex, gameplayTime, out state, out contentRevision);

            state = GameplaySkinBgaContentState.Empty;
            contentRevision = 0;
            return false;
        }

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

    public partial class DefaultBmsBgaPanelDisplay : CompositeDrawable, IBmsBgaPanelDisplay, IBmsBgaPanelLayoutDisplay,
                                                     IBmsBgaPanelStateSource, IBmsBgaPanelGameplaySkinDisplay
    {
        private Container framesContainer = null!;
        private readonly List<BmsBgaPlayer> players = new List<BmsBgaPlayer>();
        private readonly List<GameplaySkinSpecialisedSceneVisual> frameSceneVisuals = new List<GameplaySkinSpecialisedSceneVisual>();
        private readonly List<GameplaySkinSpecialisedSceneVisual> viewportSceneVisuals = new List<GameplaySkinSpecialisedSceneVisual>();
        private readonly List<GameplaySkinSpecialisedSceneVisual?> frameSceneVisualsByViewport = new List<GameplaySkinSpecialisedSceneVisual?>();
        private readonly List<GameplaySkinSpecialisedSceneVisual?> viewportSceneVisualsByViewport = new List<GameplaySkinSpecialisedSceneVisual?>();
        private readonly List<Container> viewportSceneOwners = new List<Container>();
        private readonly List<Container> nativeFrameVisuals = new List<Container>();
        private readonly List<bool> nativeFrameBaseVisibility = new List<bool>();
        private readonly List<bool> frameSceneSuppressed = new List<bool>();

        private IReadOnlyList<BmsBgaTimelineEntry> timeline = Array.Empty<BmsBgaTimelineEntry>();
        private BmsPoorBgaMode poorMode = BmsPoorBgaMode.Default;
        private BmsBgaPlacement placement = BmsBgaPlacement.TopRight;
        private bool hasStaticBackground;
        private bool loaded;
        private BmsGameplayLayoutProvider? layoutProvider;

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        internal GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; private set; }

        internal IReadOnlyList<GameplaySkinSpecialisedSceneVisual> GameplaySkinViewportSceneVisuals => viewportSceneVisuals;

        internal IReadOnlyList<GameplaySkinSpecialisedSceneVisual> GameplaySkinFrameSceneVisuals => frameSceneVisuals;

        internal IReadOnlyList<GameplaySkinSpecialisedSceneVisual?> GameplaySkinViewportSceneVisualsByViewport => viewportSceneVisualsByViewport;

        internal IReadOnlyList<GameplaySkinSpecialisedSceneVisual?> GameplaySkinFrameSceneVisualsByViewport => frameSceneVisualsByViewport;

        internal IReadOnlyList<Container> NativeFrameVisuals => nativeFrameVisuals;

        [Resolved(CanBeNull = true)]
        private IBindable<WorkingBeatmap>? workingBeatmap { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinSceneRuntimeHost? sceneRuntime { get; set; }

        public DefaultBmsBgaPanelDisplay(
            BmsGameplayLayoutProvider? layoutProvider = null,
            GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime = null)
        {
            this.layoutProvider = layoutProvider;
            sceneRuntime = gameplaySkinSceneRuntime;
            RelativeSizeAxes = Axes.Both;
        }

        void IBmsBgaPanelGameplaySkinDisplay.InitialiseGameplaySkinPublication(
            BmsGameplayLayoutProvider exactLayoutProvider,
            GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime)
        {
            ArgumentNullException.ThrowIfNull(exactLayoutProvider);

            if (layoutProvider != null && !ReferenceEquals(layoutProvider, exactLayoutProvider))
                throw new InvalidOperationException("A BMS BGA display cannot change its exact layout provider.");

            layoutProvider = exactLayoutProvider;
            sceneRuntime = gameplaySkinSceneRuntime;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BmsGameplayLayoutSnapshot? resolvedSnapshot = layoutProvider?.Current;

            if (LayoutSnapshot != null && resolvedSnapshot != null && !ReferenceEquals(LayoutSnapshot, resolvedSnapshot))
                throw new InvalidOperationException("A BMS BGA display cannot change its immutable layout snapshot.");

            LayoutSnapshot ??= resolvedSnapshot;

            if (layoutProvider != null)
            {
                ResolvedMaterialSet = layoutProvider.CurrentMaterialSet;

                if (LayoutSnapshot != null && !ReferenceEquals(ResolvedMaterialSet.Snapshot, LayoutSnapshot.Neutral))
                    throw new InvalidOperationException("A BMS BGA display requires the material set from its exact layout publication.");
            }

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

            if (layoutProvider != null)
            {
                ResolvedMaterialSet = layoutProvider.CurrentMaterialSet;

                if (!ReferenceEquals(ResolvedMaterialSet.Snapshot, snapshot.Neutral))
                    throw new InvalidOperationException("A BMS BGA display requires the material set from its exact layout publication.");
            }

            if (loaded)
                rebuild();
        }

        public void NotifyMiss()
        {
            foreach (var player in players)
                player.NotifyMiss();
        }

        protected override void Update()
        {
            base.Update();

            for (int index = 0; index < viewportSceneOwners.Count; index++)
            {
                GameplaySkinBgaContentState state = index < players.Count
                    ? players[index].ContentState
                    : hasStaticBackground
                        ? GameplaySkinBgaContentState.Ready
                        : GameplaySkinBgaContentState.Empty;
                viewportSceneOwners[index].Alpha = state is GameplaySkinBgaContentState.Ready
                    or GameplaySkinBgaContentState.Playing
                    or GameplaySkinBgaContentState.Paused
                    ? 1
                    : 0;
            }

            // BgaFrame is one global material key with one prepared factory, but replacement readiness belongs to
            // each exact native viewport instance. A ready/failed clone must never hide or revive another viewport's
            // engine-owned frame, and the content/player itself is never part of this gate.
            for (int index = 0; index < nativeFrameVisuals.Count; index++)
            {
                bool instanceReplacementReady = frameSceneSuppressed[index]
                                                || frameSceneVisualsByViewport[index]?.Alpha > 0;
                nativeFrameVisuals[index].Alpha = nativeFrameBaseVisibility[index] && !instanceReplacementReady ? 1 : 0;
            }
        }

        bool IBmsBgaPanelStateSource.TryGetContentState(
            int viewportIndex,
            out GameplaySkinBgaContentState state,
            out long contentRevision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(viewportIndex);

            int viewportCount = LayoutSnapshot?.BgaViewports.Count ?? 0;

            if (viewportIndex >= viewportCount)
            {
                state = GameplaySkinBgaContentState.Empty;
                contentRevision = 0;
                return false;
            }

            if (viewportIndex < players.Count)
            {
                BmsBgaPlayer player = players[viewportIndex];
                state = player.ContentState;
                contentRevision = player.ContentRevision;
            }
            else
            {
                state = hasStaticBackground ? GameplaySkinBgaContentState.Ready : GameplaySkinBgaContentState.Empty;
                contentRevision = 0;
            }

            return true;
        }

        bool IBmsBgaPanelStateSource.TryGetContentStateAt(
            int viewportIndex,
            double gameplayTime,
            out GameplaySkinBgaContentState state,
            out long contentRevision)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(viewportIndex);

            int viewportCount = LayoutSnapshot?.BgaViewports.Count ?? 0;

            if (viewportIndex >= viewportCount)
            {
                state = GameplaySkinBgaContentState.Empty;
                contentRevision = 0;
                return false;
            }

            if (viewportIndex < players.Count)
                players[viewportIndex].GetContentStateAt(gameplayTime, out state, out contentRevision);
            else
            {
                state = hasStaticBackground ? GameplaySkinBgaContentState.Ready : GameplaySkinBgaContentState.Empty;
                contentRevision = 0;
            }

            return true;
        }

        private void rebuild()
        {
            // Detach the old native owner graph before retiring its specialised handles. Framework clear/disposal
            // is deferred; disposing a still-mounted handle would leave a disposed drawable in the next update.
            framesContainer.Clear(disposeChildren: false);
            retireFrameSceneVisuals();
            players.Clear();
            hasStaticBackground = false;

            if (LayoutSnapshot == null)
                return;

            var background = timeline.Count == 0 ? workingBeatmap?.Value?.GetBackground() : null;
            hasStaticBackground = background != null;

            for (int index = 0; index < LayoutSnapshot.BgaViewports.Count; index++)
                framesContainer.Add(createFrame(LayoutSnapshot.BgaViewports[index], background, index));
        }

        private Container createFrame(GameplaySkinLayoutRect viewport, Texture? background, int viewportIndex)
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

            content.Alpha = hasContent ? 1 : 0;
            var nativeFrame = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                CornerRadius = 6,
                BorderThickness = 2,
                BorderColour = BmsDefaultPlayfieldPalette.MetadataPanelBorder,
                Alpha = hasContent ? 1 : 0,
                Child = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 },
            };
            nativeFrameVisuals.Add(nativeFrame);
            nativeFrameBaseVisibility.Add(hasContent);
            frameSceneSuppressed.Add(false);
            frameSceneVisualsByViewport.Add(null);
            viewportSceneVisualsByViewport.Add(null);
            var sceneViewportOwner = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = hasContent ? 1 : 0,
            };
            viewportSceneOwners.Add(sceneViewportOwner);
            var sceneFrameOwner = new Container { RelativeSizeAxes = Axes.Both };
            var result = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
                RelativePositionAxes = Axes.Both,
                RelativeSizeAxes = Axes.Both,
                Size = new osuTK.Vector2(viewport.Width, viewport.Height),
                Position = new osuTK.Vector2(viewport.X, viewport.Y),
                Masking = true,
                CornerRadius = 6,
                Alpha = 1,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black, Alpha = hasContent ? 1 : 0 },
                    content,
                    // This owner is clipped by the engine's exact viewport and only decorates its read-only
                    // content-state surface. It never receives the BGA player, timeline or playback clock.
                    sceneViewportOwner,
                    nativeFrame,
                    sceneFrameOwner,
                },
            };

            // BgaViewport/BgaFrame share one global material key and immutable prepared factory, while explicit BGA
            // scene targets retain their exact C3 viewport index. Every real engine-owned viewport receives one
            // bounded clone; the player/content stays below that decoration and never enters the scene authority.
            if (sceneRuntime != null && ResolvedMaterialSet != null)
            {
                var viewportKey = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.BgaViewport,
                    GameplaySkinResolvedMaterialTarget.Global);

                if (!sceneRuntime.TryGetVisualGate(viewportKey, out GameplaySkinSceneHostedSlot? viewportGate) || viewportGate == null)
                    throw new InvalidOperationException("The exact BMS BGA-viewport scene gate is missing from its committed publication.");

                GameplaySkinSpecialisedSceneVisual? viewportSceneVisual = viewportGate.Route == GameplaySkinSceneHostRoute.Specialised
                    ? sceneRuntime.PrepareSpecialisedVisual(viewportKey, sceneViewportOwner, viewportIndex)
                    : null;

                if (viewportSceneVisual != null)
                {
                    viewportSceneVisual.OnApply();
                    viewportSceneVisuals.Add(viewportSceneVisual);
                    viewportSceneVisualsByViewport[viewportIndex] = viewportSceneVisual;
                }

                var key = new GameplaySkinResolvedMaterialKey(
                    GameplaySkinSlotCatalog.BgaFrame,
                    GameplaySkinResolvedMaterialTarget.Global);

                if (!sceneRuntime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact BMS BGA-frame scene gate is missing from its committed publication.");

                GameplaySkinSpecialisedSceneVisual? sceneVisual = gate.Route == GameplaySkinSceneHostRoute.Specialised
                    ? sceneRuntime.PrepareSpecialisedVisual(key, sceneFrameOwner, viewportIndex)
                    : null;

                if (sceneVisual != null)
                {
                    sceneVisual.OnApply();
                    frameSceneVisuals.Add(sceneVisual);
                    frameSceneVisualsByViewport[viewportIndex] = sceneVisual;
                }

                frameSceneSuppressed[viewportIndex] = gate.Route == GameplaySkinSceneHostRoute.Suppressed;
            }

            return result;
        }

        private void retireFrameSceneVisuals()
        {
            foreach (GameplaySkinSpecialisedSceneVisual visual in frameSceneVisuals)
                visual.Dispose();

            frameSceneVisuals.Clear();
            frameSceneVisualsByViewport.Clear();

            foreach (GameplaySkinSpecialisedSceneVisual visual in viewportSceneVisuals)
                visual.Dispose();

            viewportSceneVisuals.Clear();
            viewportSceneVisualsByViewport.Clear();
            viewportSceneOwners.Clear();
            nativeFrameVisuals.Clear();
            nativeFrameBaseVisibility.Clear();
            frameSceneSuppressed.Clear();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                retireFrameSceneVisuals();

            base.Dispose(isDisposing);
        }

    }
}
