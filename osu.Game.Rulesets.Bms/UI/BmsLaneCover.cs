// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsLaneCoverDisplay
    {
        void SetFocused(bool isFocused);
    }

    public partial class BmsLaneCover : CompositeDrawable
    {
        public readonly BindableFloat CoverPercent = new BindableFloat();

        public readonly BindableFloat CoverOpacity = new BindableFloat(1000)
        {
            MinValue = 0,
            MaxValue = 1000,
            Precision = 1,
            Default = 1000,
        };

        public readonly BindableBool IsFocused = new BindableBool();

        public BmsLaneCoverPosition CoverPosition { get; }

        private readonly Container cover;
        private readonly Container programmaticVisualOwner;
        private readonly SkinnableLaneCoverDisplay display;
        private readonly Container sceneHost;
        private readonly List<BmsLaneCoverSceneStageHost> sceneStageHosts = new List<BmsLaneCoverSceneStageHost>();
        private readonly List<IDisposable> sceneVisualRegistrations = new List<IDisposable>();
        private readonly List<GameplaySkinSpecialisedSceneVisual> sceneVisuals = new List<GameplaySkinSpecialisedSceneVisual>();
        private GameplaySkinSceneRuntimeHost? sceneRuntime;
        private bool sceneVisualsRegistered;

        [Resolved]
        private BmsGameplayLayoutProvider layoutProvider { get; set; } = null!;

        internal BmsGameplayLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        protected float CoverContainerHeight => cover.Height;

        protected float FocusEdgeAlpha => (display.CurrentDisplay as DefaultBmsLaneCoverDisplay)?.FocusEdgeAlpha ?? 0;

        internal IReadOnlyList<BmsLaneCoverStageVisual> GameplaySkinStageFallbackVisuals
            => (display.CurrentDisplay as DefaultBmsLaneCoverDisplay)?.StageVisuals
               ?? Array.Empty<BmsLaneCoverStageVisual>();

        internal Drawable GameplaySkinCustomFallbackGateOwner => programmaticVisualOwner;

        protected float CoverDisplayAlpha => display.Alpha;

        public BmsLaneCover(BmsLaneCoverPosition position)
        {
            RelativeSizeAxes = Axes.Both;
            AlwaysPresent = true;

            CoverPosition = position;

            InternalChild = cover = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = position == BmsLaneCoverPosition.Sudden ? Anchor.TopCentre : Anchor.BottomCentre,
                Origin = position == BmsLaneCoverPosition.Sudden ? Anchor.TopCentre : Anchor.BottomCentre,
                Width = 1,
                Height = 0,
                Children = new Drawable[]
                {
                    programmaticVisualOwner = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = display = new SkinnableLaneCoverDisplay(this, position)
                        {
                            RelativeSizeAxes = Axes.Both,
                            CentreComponent = false,
                        },
                    },
                    sceneHost = new Container { RelativeSizeAxes = Axes.Both },
                },
            };

            CoverPercent.BindValueChanged(_ => updateCoverage(), true);
            CoverOpacity.BindValueChanged(_ => updateOpacity(), true);
            IsFocused.BindValueChanged(_ => updateFocusState(), true);
        }

        [BackgroundDependencyLoader(true)]
        private void load(GameplaySkinSceneRuntimeHost? runtime)
        {
            LayoutSnapshot = layoutProvider.Current;
            sceneRuntime = runtime;
            createSceneStageHosts();
            display.EnsureExactPublicationDisplay();
            registerProgrammaticVisuals();
        }

        private bool requiresIndependentlyGatedDefault()
        {
            if (sceneRuntime == null)
                return false;

            foreach (BmsLaneCoverSceneStageHost stage in sceneStageHosts)
            {
                if (requiresPart(GameplaySkinSlotCatalog.LaneCoverFill, stage.Target)
                    || requiresPart(GameplaySkinSlotCatalog.LaneCoverDecoration, stage.Target))
                {
                    return true;
                }
            }

            return false;

            bool requiresPart(GameplaySkinSlotDescriptor descriptor, GameplaySkinResolvedMaterialTarget target)
            {
                var key = new GameplaySkinResolvedMaterialKey(descriptor, target);

                if (!sceneRuntime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact BMS lane-cover scene gate is missing from its committed publication.");

                if (gate.RoutedNodes.Count != 0)
                    return true;

                if (!sceneRuntime.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry) || entry == null)
                    throw new InvalidOperationException("The exact BMS lane-cover material entry is missing from its committed publication.");

                return entry.State == GameplaySkinResolvedMaterialState.Suppress
                       || entry.Material is GameplaySkinPublicSlotMaterial { IsProgrammaticFallback: false };
            }
        }

        private void createSceneStageHosts()
        {
            if (sceneStageHosts.Count != 0)
                throw new InvalidOperationException("The BMS lane-cover scene host graph is immutable after load.");

            GameplaySkinLayoutRect playfield = LayoutSnapshot.PlayfieldRect;

            foreach (GameplaySkinLaneTopologyGroup group in LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder)
            {
                GameplaySkinLayoutRect groupRect = LayoutSnapshot.Neutral.GetGroup(group.Identity.Id).Rect;
                var stage = new BmsLaneCoverSceneStageHost(
                    GameplaySkinResolvedMaterialTarget.ForStage(group),
                    (groupRect.X - playfield.X) / playfield.Width,
                    groupRect.Width / playfield.Width);
                sceneStageHosts.Add(stage);
                sceneHost.Add(stage);
            }
        }

        private void updateCoverage()
        {
            float coverage = Math.Clamp(CoverPercent.Value / 1000f, 0, 1);

            cover.Height = coverage;

            updateFocusState();
        }

        private void updateOpacity()
            => display.Alpha = Math.Clamp(CoverOpacity.Value / 1000f, 0, 1);

        private void updateFocusState()
        {
            bool showFocus = CoverPercent.Value > 0 && IsFocused.Value;

            if (display.CurrentDisplay is IBmsLaneCoverDisplay laneCoverDisplay)
                laneCoverDisplay.SetFocused(showFocus);
        }

        private void registerProgrammaticVisuals()
        {
            // The exact publication and native stage graph are immutable for this gameplay root. Skin/load callbacks
            // may converge more than once while a custom component fails closed; never retire and reconstruct an
            // already-mounted specialised visual during that convergence.
            if (sceneVisualsRegistered)
                return;

            if (sceneRuntime == null || display.CurrentDisplay == null || sceneStageHosts.Count == 0)
                return;

            if (!ReferenceEquals(sceneRuntime.Publication.Snapshot, LayoutSnapshot.Neutral))
                throw new InvalidOperationException("The BMS lane-cover scene gate owner requires its exact committed layout publication.");

            if (display.CurrentDisplay is not DefaultBmsLaneCoverDisplay defaultDisplay)
            {
                if (requiresIndependentlyGatedDefault())
                    throw new InvalidOperationException("An authored exact BMS lane-cover publication requires the closed, independently gated default part host.");

                // No public lane-cover part is replacing or suppressing this compatibility component.
                return;
            }

            if (defaultDisplay.StageVisuals.Count == 0)
                return;

            if (defaultDisplay.StageVisuals.Count != sceneStageHosts.Count)
                throw new InvalidOperationException("The default BMS lane cover must expose one fallback partition per exact C3 stage.");

            for (int i = 0; i < sceneStageHosts.Count; i++)
            {
                BmsLaneCoverSceneStageHost sceneStage = sceneStageHosts[i];
                BmsLaneCoverStageVisual fallbackStage = defaultDisplay.StageVisuals[i];

                if (fallbackStage.Target != null && !fallbackStage.Target.Equals(sceneStage.Target))
                    throw new InvalidOperationException("The default BMS lane-cover fallback does not retain exact C3 stage order.");

                registerPart(GameplaySkinSlotCatalog.LaneCoverFill, sceneStage.Target, fallbackStage.FillVisual, sceneStage.FillSceneOwner);
                registerPart(GameplaySkinSlotCatalog.LaneCoverDecoration, sceneStage.Target, fallbackStage.DecorationVisual, sceneStage.DecorationSceneOwner);
            }

            sceneVisualsRegistered = true;

            void registerPart(
                GameplaySkinSlotDescriptor descriptor,
                GameplaySkinResolvedMaterialTarget target,
                Drawable? fallback,
                Container sceneOwner)
            {
                var key = new GameplaySkinResolvedMaterialKey(descriptor, target);

                if (!sceneRuntime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact BMS lane-cover scene gate is missing from its committed publication.");

                GameplaySkinSpecialisedSceneVisual? sceneVisual = gate.Route == GameplaySkinSceneHostRoute.Specialised
                    ? sceneRuntime.PrepareSpecialisedVisual(key, sceneOwner)
                    : null;

                if (sceneVisual != null)
                {
                    sceneVisual.OnApply();
                    sceneVisuals.Add(sceneVisual);
                }

                if (fallback != null && (gate.Route == GameplaySkinSceneHostRoute.Suppressed || sceneVisual != null))
                    sceneVisualRegistrations.Add(sceneRuntime.RegisterProgrammaticVisual(key, fallback));
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (IDisposable registration in sceneVisualRegistrations)
                    registration.Dispose();

                sceneVisualRegistrations.Clear();

                foreach (GameplaySkinSpecialisedSceneVisual visual in sceneVisuals)
                    visual.Dispose();

                sceneVisuals.Clear();
            }

            base.Dispose(isDisposing);
        }

        private sealed partial class SkinnableLaneCoverDisplay : SkinnableDrawable
        {
            private readonly BmsLaneCover owner;
            private readonly BmsLaneCoverPosition position;

            public Drawable? CurrentDisplay => Drawable;

            public SkinnableLaneCoverDisplay(BmsLaneCover owner, BmsLaneCoverPosition position)
                : base(new BmsLaneCoverSkinLookup(position), _ => new DefaultBmsLaneCoverDisplay(position))
            {
                this.owner = owner;
                this.position = position;
            }

            public void EnsureExactPublicationDisplay()
            {
                if (owner.requiresIndependentlyGatedDefault() && CurrentDisplay is not DefaultBmsLaneCoverDisplay)
                    SetDrawable(new DefaultBmsLaneCoverDisplay(position), replacementIsDefault: true);
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);

                // An opaque legacy cover cannot expose independent Fill/Decoration and stage partitions. Exact C5
                // gameplay therefore fails closed to the protected typed display; compatibility-only previews keep
                // the legacy component. This avoids an aggregate gate swallowing inherited parts or another deck.
                EnsureExactPublicationDisplay();

                owner.updateFocusState();
                owner.registerProgrammaticVisuals();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                EnsureExactPublicationDisplay();
                owner.registerProgrammaticVisuals();
            }
        }
    }

    internal partial class DefaultBmsLaneCoverDisplay : CompositeDrawable, IBmsLaneCoverDisplay
    {
        private readonly BmsLaneCoverPosition position;
        private readonly BmsKeymode? isolatedKeymode;
        private readonly List<BmsLaneCoverStageVisual> stageVisuals = new List<BmsLaneCoverStageVisual>();
        private bool isFocused;

        public float FocusEdgeAlpha => stageVisuals.FirstOrDefault()?.FocusEdge.Alpha ?? 0;

        internal IReadOnlyList<BmsLaneCoverStageVisual> StageVisuals => stageVisuals;

        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        public DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition position)
        {
            this.position = position;
            RelativeSizeAxes = Axes.Both;
        }

        /// <summary>
        /// Creates an isolated skin-component preview with an explicit keymode authority. Production gameplay uses the
        /// gameplay-root <see cref="BmsGameplayLayoutProvider"/> resolved by the parameterless overload.
        /// </summary>
        internal DefaultBmsLaneCoverDisplay(BmsLaneCoverPosition position, BmsKeymode isolatedKeymode)
            : this(position)
        {
            this.isolatedKeymode = isolatedKeymode;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            BmsKeymode? keymodeAuthority = layoutProvider?.Current.Keymode ?? isolatedKeymode;

            // A detached component has no parser-owned authority. Keep it inert rather than inventing a 7K surface.
            if (!keymodeAuthority.HasValue)
            {
                InternalChild = new Box { RelativeSizeAxes = Axes.Both, Alpha = 0 };
                return;
            }

            BmsKeymode keymode = keymodeAuthority.Value;

            var fillColour = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.LaneCoverFillColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.LaneCoverFill;
            var shadeColour = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.LaneCoverShadeColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.LaneCoverShade;
            var focusColour = skin.GetBmsSkinConfig<Color4>(BmsSkinConfigurationLookups.LaneCoverFocusColour, keymode)?.Value ?? BmsDefaultPlayfieldPalette.FocusAccent;

            bool isSudden = position == BmsLaneCoverPosition.Sudden;
            var edgeAnchor = isSudden ? Anchor.BottomLeft : Anchor.TopLeft;

            // Texture base: LaneCoverTopImage for Sudden (covers from the top), LaneCoverBottomImage for Hidden (from the bottom).
            string? imagePath = skin.GetBmsSkinConfig<string>(isSudden ? BmsSkinConfigurationLookups.LaneCoverTopImage : BmsSkinConfigurationLookups.LaneCoverBottomImage, keymode)?.Value;
            var texture = !string.IsNullOrEmpty(imagePath) ? skin.GetTexture(imagePath) : null;

            stageVisuals.Clear();
            BmsGameplayLayoutSnapshot? snapshot = layoutProvider?.Current;

            if (snapshot == null)
            {
                stageVisuals.Add(createStageVisual(
                    null,
                    0,
                    1,
                    texture,
                    fillColour,
                    shadeColour,
                    focusColour,
                    isSudden,
                    edgeAnchor));
            }
            else
            {
                GameplaySkinLayoutRect playfield = snapshot.PlayfieldRect;

                foreach (GameplaySkinLaneTopologyGroup group in snapshot.Neutral.Context.Topology.GroupsInLogicalOrder)
                {
                    GameplaySkinLayoutRect groupRect = snapshot.Neutral.GetGroup(group.Identity.Id).Rect;
                    stageVisuals.Add(createStageVisual(
                        GameplaySkinResolvedMaterialTarget.ForStage(group),
                        (groupRect.X - playfield.X) / playfield.Width,
                        groupRect.Width / playfield.Width,
                        texture,
                        fillColour,
                        shadeColour,
                        focusColour,
                        isSudden,
                        edgeAnchor));
                }
            }

            InternalChildren = stageVisuals.ToArray();
            updateFocusState();
        }

        private static BmsLaneCoverStageVisual createStageVisual(
            GameplaySkinResolvedMaterialTarget? target,
            float x,
            float width,
            Framework.Graphics.Textures.Texture? texture,
            Color4 fillColour,
            Color4 shadeColour,
            Color4 focusColour,
            bool isSudden,
            Anchor edgeAnchor)
        {
            var fillVisual = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = texture != null
                    ? new Sprite { RelativeSizeAxes = Axes.Both, Texture = texture }
                    : new Box { RelativeSizeAxes = Axes.Both, Alpha = 1, Colour = fillColour },
            };
            var decorationChildren = new List<Drawable>();

            // The programmatic shade gradient only applies to the box fallback; a texture owns its own look.
            if (texture == null)
            {
                decorationChildren.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Height = 0.18f,
                    Alpha = 0.88f,
                    Anchor = edgeAnchor,
                    Origin = edgeAnchor,
                    Colour = isSudden
                        ? ColourInfo.GradientVertical(Color4.Transparent, shadeColour)
                        : ColourInfo.GradientVertical(shadeColour, Color4.Transparent),
                });
            }

            var focusWash = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Height = 0.3f,
                Alpha = 0,
                Anchor = edgeAnchor,
                Origin = edgeAnchor,
                Colour = isSudden
                    ? ColourInfo.GradientVertical(Color4.Transparent, BmsDefaultPlayfieldPalette.FocusWash)
                    : ColourInfo.GradientVertical(BmsDefaultPlayfieldPalette.FocusWash, Color4.Transparent),
            };
            var focusEdge = new Box
            {
                RelativeSizeAxes = Axes.X,
                Height = 4,
                Alpha = 0,
                Anchor = edgeAnchor,
                Origin = edgeAnchor,
                Colour = focusColour,
            };
            decorationChildren.Add(focusWash);
            decorationChildren.Add(focusEdge);

            return new BmsLaneCoverStageVisual(
                target,
                x,
                width,
                fillVisual,
                new Container { RelativeSizeAxes = Axes.Both, Children = decorationChildren },
                focusWash,
                focusEdge);
        }

        public void SetFocused(bool isFocused)
        {
            this.isFocused = isFocused;
            updateFocusState();
        }

        private void updateFocusState()
        {
            foreach (BmsLaneCoverStageVisual stageVisual in stageVisuals)
            {
                stageVisual.FocusEdge.Alpha = isFocused ? 1 : 0;
                stageVisual.FocusWash.Alpha = isFocused ? 0.24f : 0;
            }
        }
    }

    internal sealed partial class BmsLaneCoverStageVisual : Container
    {
        public GameplaySkinResolvedMaterialTarget? Target { get; }

        public Container FillVisual { get; }

        public Container DecorationVisual { get; }

        public Box FocusWash { get; }

        public Box FocusEdge { get; }

        public BmsLaneCoverStageVisual(
            GameplaySkinResolvedMaterialTarget? target,
            float x,
            float width,
            Container fillVisual,
            Container decorationVisual,
            Box focusWash,
            Box focusEdge)
        {
            Target = target;
            FillVisual = fillVisual;
            DecorationVisual = decorationVisual;
            FocusWash = focusWash;
            FocusEdge = focusEdge;
            RelativePositionAxes = Axes.X;
            RelativeSizeAxes = Axes.Both;
            X = x;
            Width = width;
            Children = new Drawable[] { fillVisual, decorationVisual };
        }
    }

    internal sealed partial class BmsLaneCoverSceneStageHost : Container
    {
        public GameplaySkinResolvedMaterialTarget Target { get; }

        public Container FillSceneOwner { get; }

        public Container DecorationSceneOwner { get; }

        public BmsLaneCoverSceneStageHost(
            GameplaySkinResolvedMaterialTarget target,
            float x,
            float width)
        {
            Target = target ?? throw new ArgumentNullException(nameof(target));
            RelativePositionAxes = Axes.X;
            RelativeSizeAxes = Axes.Both;
            X = x;
            Width = width;
            Children = new Drawable[]
            {
                FillSceneOwner = new Container { RelativeSizeAxes = Axes.Both },
                DecorationSceneOwner = new Container { RelativeSizeAxes = Axes.Both },
            };
        }
    }

    public enum BmsLaneCoverPosition
    {
        Sudden,
        Hidden,
    }
}
