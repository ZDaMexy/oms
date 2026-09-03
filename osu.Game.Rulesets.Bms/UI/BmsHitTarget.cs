// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsHitTargetDisplay
    {
        void SetPressed(bool isPressed);

        void SetFocused(bool isFocused);
    }

    public partial class BmsHitTarget : CompositeDrawable, IGameplaySkinSpecialisedSceneConsumer
    {
        public readonly BindableBool IsPressed = new BindableBool();

        public readonly BindableBool IsFocused = new BindableBool();

        protected float PressedOverlayAlpha => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.PressedOverlayAlpha ?? 0;

        protected float FocusEdgeAlpha => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.FocusEdgeAlpha ?? 0;

        private readonly BmsPlayfieldLayoutProfile layoutProfile;
        private readonly BmsLaneSkinLookup lookup;
        private readonly Container programmaticVisualOwner;
        private readonly SkinnableHitTargetDisplay display;
        private readonly Container sceneVisualContainer;
        private GameplaySkinSceneRuntimeHost? sceneRuntime;
        private GameplaySkinSpecialisedSceneVisual? sceneVisual;
        private IDisposable? hitTargetVisualRegistration;
        private IDisposable? judgementLineVisualRegistration;
        private IDisposable? keyFlashVisualRegistration;
        private GameplaySkinResolvedMaterialKey? keyVisualMaterialKey;
        private GameplaySkinSceneHostedSlot? keyVisualGate;

        internal Container HitExplosions { get; }

        public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet
            => sceneRuntime?.MaterialSet
               ?? throw new InvalidOperationException("A compatibility BMS hit target has no exact C4 material publication.");

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => keyVisualMaterialKey
               ?? throw new InvalidOperationException("A compatibility BMS hit target has no specialised C5 KeyVisual key.");

        public GameplaySkinSceneHostedSlot SceneVisualGate
            => keyVisualGate
               ?? throw new InvalidOperationException("A compatibility BMS hit target has no specialised C5 KeyVisual gate.");

        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();

        internal Drawable? GameplaySkinHitTargetFallbackVisual
            => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.HitTargetVisual;

        internal Drawable? GameplaySkinJudgementLineFallbackVisual
            => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.JudgementLineVisual;

        internal Drawable? GameplaySkinKeyFlashFallbackVisual
            => (display.CurrentDisplay as DefaultBmsHitTargetDisplay)?.KeyFlashVisual;

        internal Drawable GameplaySkinCustomFallbackGateOwner => programmaticVisualOwner;

        public BmsHitTarget(BmsLaneSkinLookup lookup, BmsPlayfieldLayoutProfile layoutProfile, BmsGameplayLayoutSnapshot? layoutSnapshot = null)
        {
            this.layoutProfile = layoutProfile;
            this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
            LayoutSnapshot = layoutSnapshot;
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;

            if (layoutSnapshot == null)
            {
                // Explicit compatibility-only construction retains the historical local pixel metric.
                RelativeSizeAxes = Axes.X;
                Height = layoutProfile.HitTargetHeight;
            }
            else
            {
                // Production geometry is projected exclusively from the exact immutable snapshot. In particular this
                // keeps the renderer and the neutral surface identical when DPI scaling changes the solved height.
                RelativeSizeAxes = Axes.Both;
                Height = layoutSnapshot.HitTargetRect.Height / layoutSnapshot.PlayfieldRect.Height;
            }

            InternalChildren = new Drawable[]
            {
                programmaticVisualOwner = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = display = new SkinnableHitTargetDisplay(this, lookup)
                    {
                        RelativeSizeAxes = Axes.Both,
                        CentreComponent = false,
                    },
                },
                sceneVisualContainer = new Container { RelativeSizeAxes = Axes.Both },
                HitExplosions = new Container { RelativeSizeAxes = Axes.Both },
            };

            IsPressed.BindValueChanged(_ => updateState(), true);
            IsFocused.BindValueChanged(_ => updateState(), true);
        }

        [BackgroundDependencyLoader(true)]
        private void loadGameplaySkinScene(GameplaySkinSceneRuntimeHost? runtime)
        {
            if (runtime == null || LayoutSnapshot == null || lookup.LaneId == null)
                return;

            BmsGameplayLayoutLane exactLane = LayoutSnapshot.GetLane(lookup.LaneId);

            if (exactLane.LogicalIndex != lookup.LaneIndex || exactLane.IsScratch != lookup.IsScratch)
                throw new InvalidOperationException("The BMS hit-target scene consumer requires the exact C3 lane identity carried by its lookup.");

            GameplaySkinLaneTopologyEntry lane = exactLane.NeutralLane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                candidate.Identity.Id.Equals(lane.Identity.Group.Id));
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
            GameplaySkinResolvedMaterialTarget stageTarget = GameplaySkinResolvedMaterialTarget.ForStage(group);
            var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyVisual, laneTarget);

            if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                throw new InvalidOperationException("The exact BMS KeyVisual scene gate is missing from the committed publication.");

            sceneRuntime = runtime;
            keyVisualMaterialKey = key;
            keyVisualGate = gate;

            // An opaque legacy/custom target cannot expose the independently authored HitTarget,
            // JudgementLine and KeyFlash parts. Exact C5 gameplay therefore fails closed to the
            // protected typed host instead of allowing one part to hide the others (or another deck).
            display.EnsureExactPublicationDisplay();

            if (gate.Route == GameplaySkinSceneHostRoute.Specialised)
            {
                sceneVisual = runtime.PrepareSpecialisedVisual(key, sceneVisualContainer);

                if (sceneVisual != null)
                {
                    AppliedSceneNodeIds = Array.AsReadOnly(
                        sceneVisual.RuntimeNodes.Select(node => node.PreparedNode.InstanceId).ToArray());
                    sceneVisual.OnApply();
                }
            }

            registerProgrammaticVisuals(laneTarget, stageTarget);
        }

        private void updateState()
        {
            if (display.CurrentDisplay is not IBmsHitTargetDisplay hitTargetDisplay)
                return;

            hitTargetDisplay.SetPressed(IsPressed.Value);
            hitTargetDisplay.SetFocused(IsFocused.Value);
        }

        private bool requiresIndependentlyGatedDefault()
        {
            GameplaySkinSceneRuntimeHost? runtime = sceneRuntime;

            if (runtime == null || LayoutSnapshot == null || lookup.LaneId == null)
                return false;

            BmsGameplayLayoutLane exactLane = LayoutSnapshot.GetLane(lookup.LaneId);
            GameplaySkinLaneTopologyEntry lane = exactLane.NeutralLane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                candidate.Identity.Id.Equals(lane.Identity.Group.Id));
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(group, lane);
            GameplaySkinResolvedMaterialTarget stageTarget = GameplaySkinResolvedMaterialTarget.ForStage(group);

            return requiresPart(GameplaySkinSlotCatalog.HitTarget, laneTarget)
                   || requiresPart(GameplaySkinSlotCatalog.JudgementLine, stageTarget)
                   || requiresPart(GameplaySkinSlotCatalog.KeyFlash, laneTarget);

            bool requiresPart(GameplaySkinSlotDescriptor descriptor, GameplaySkinResolvedMaterialTarget target)
            {
                var key = new GameplaySkinResolvedMaterialKey(descriptor, target);

                if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact BMS hit-target scene gate is missing from its committed publication.");

                if (gate.RoutedNodes.Count != 0)
                    return true;

                if (!runtime.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry) || entry == null)
                    throw new InvalidOperationException("The exact BMS hit-target material entry is missing from its committed publication.");

                return entry.State == GameplaySkinResolvedMaterialState.Suppress
                       || entry.Material is GameplaySkinPublicSlotMaterial { IsProgrammaticFallback: false };
            }
        }

        private void registerProgrammaticVisuals(
            GameplaySkinResolvedMaterialTarget laneTarget,
            GameplaySkinResolvedMaterialTarget stageTarget)
        {
            hitTargetVisualRegistration?.Dispose();
            judgementLineVisualRegistration?.Dispose();
            keyFlashVisualRegistration?.Dispose();
            hitTargetVisualRegistration = null;
            judgementLineVisualRegistration = null;
            keyFlashVisualRegistration = null;

            if (sceneRuntime == null || display.CurrentDisplay == null)
                return;

            var hitTargetKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.HitTarget, laneTarget);
            var judgementLineKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.JudgementLine, stageTarget);
            var keyFlashKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyFlash, laneTarget);

            if (display.CurrentDisplay is not DefaultBmsHitTargetDisplay defaultDisplay)
            {
                if (requiresIndependentlyGatedDefault())
                    throw new InvalidOperationException("An authored exact BMS hit-target publication requires the closed, independently gated default part host.");

                // With no public replacement/suppression, retain the user's indivisible legacy target unchanged.
                return;
            }

            hitTargetVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(
                hitTargetKey,
                defaultDisplay.HitTargetVisual);
            judgementLineVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(
                judgementLineKey,
                defaultDisplay.JudgementLineVisual);
            // The default BMS target has no separate static key/receptor visual: its only key-like child is the
            // pressed glow and therefore belongs exclusively to KeyFlash. KeyVisual author scenes are mounted in
            // sceneVisualContainer above and must never swallow this independent fallback.
            keyFlashVisualRegistration = sceneRuntime.RegisterProgrammaticVisual(
                keyFlashKey,
                defaultDisplay.KeyFlashVisual);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                sceneVisual?.OnFree();
                hitTargetVisualRegistration?.Dispose();
                judgementLineVisualRegistration?.Dispose();
                keyFlashVisualRegistration?.Dispose();
            }

            base.Dispose(isDisposing);
        }

        private sealed partial class SkinnableHitTargetDisplay : SkinnableDrawable
        {
            private readonly BmsHitTarget owner;
            private readonly BmsLaneSkinLookup lookup;

            public Drawable? CurrentDisplay => Drawable;

            public SkinnableHitTargetDisplay(BmsHitTarget owner, BmsLaneSkinLookup lookup)
                : base(lookup, _ => new DefaultBmsHitTargetDisplay(lookup.IsScratch, lookup.Keymode, owner.layoutProfile, owner.LayoutSnapshot))
            {
                this.owner = owner;
                this.lookup = lookup;
            }

            public void EnsureExactPublicationDisplay()
            {
                if (owner.requiresIndependentlyGatedDefault() && CurrentDisplay is not DefaultBmsHitTargetDisplay)
                {
                    SetDrawable(
                        new DefaultBmsHitTargetDisplay(lookup.IsScratch, lookup.Keymode, owner.layoutProfile, owner.LayoutSnapshot),
                        replacementIsDefault: true);
                }
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);

                EnsureExactPublicationDisplay();

                owner.updateState();

                if (owner.LayoutSnapshot != null && owner.lookup.LaneId != null)
                {
                    BmsGameplayLayoutLane exactLane = owner.LayoutSnapshot.GetLane(owner.lookup.LaneId);
                    GameplaySkinLaneTopologyEntry lane = exactLane.NeutralLane.TopologyEntry;
                    GameplaySkinLaneTopologyGroup group = owner.LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                        candidate.Identity.Id.Equals(lane.Identity.Group.Id));
                    owner.registerProgrammaticVisuals(
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        GameplaySkinResolvedMaterialTarget.ForStage(group));
                }
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                EnsureExactPublicationDisplay();
                owner.updateState();

                if (owner.LayoutSnapshot != null && owner.lookup.LaneId != null)
                {
                    BmsGameplayLayoutLane exactLane = owner.LayoutSnapshot.GetLane(owner.lookup.LaneId);
                    GameplaySkinLaneTopologyEntry lane = exactLane.NeutralLane.TopologyEntry;
                    GameplaySkinLaneTopologyGroup group = owner.LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.Single(candidate =>
                        candidate.Identity.Id.Equals(lane.Identity.Group.Id));
                    owner.registerProgrammaticVisuals(
                        GameplaySkinResolvedMaterialTarget.ForLane(group, lane),
                        GameplaySkinResolvedMaterialTarget.ForStage(group));
                }
            }
        }
    }

    internal partial class DefaultBmsHitTargetDisplay : CompositeDrawable, IBmsHitTargetDisplay
    {
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        private Container hitTargetVisual = null!;
        private Box bar = null!;
        private Container line = null!;
        private Box lineFill = null!;
        private Container keyFlashVisual = null!;
        private Box pressedOverlay = null!;
        private Box focusEdge = null!;
        private Sprite? textureBase;
        private bool isPressed;
        private bool isFocused;
        private Color4 glowColour;

        public float PressedOverlayAlpha => pressedOverlay?.Alpha ?? 0;

        public float FocusEdgeAlpha => focusEdge?.Alpha ?? 0;

        internal Drawable HitTargetVisual => hitTargetVisual;

        internal Drawable JudgementLineVisual => line;

        internal Drawable KeyFlashVisual => keyFlashVisual;

        internal float BarHeight => bar?.Height ?? 0;

        internal float LineHeight => line?.Height ?? 0;

        internal float LineDrawHeight => line?.DrawHeight ?? 0;

        internal float LineScreenSpaceHeight => line?.ScreenSpaceDrawQuad.Height ?? 0;

        internal float LineScreenSpaceTop => line?.ScreenSpaceDrawQuad.TopLeft.Y ?? 0;

        internal float FocusEdgeHeight => focusEdge?.Height ?? 0;

        internal float GlowRadius { get; private set; }

        public DefaultBmsHitTargetDisplay(
            bool isScratch,
            BmsKeymode keymode,
            BmsPlayfieldLayoutProfile layoutProfile,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null)
        {
            this.isScratch = isScratch;
            this.keymode = keymode;
            RelativeSizeAxes = Axes.Both;

            var barColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetBar : BmsDefaultPlayfieldPalette.HitTargetBar;
            var lineColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetLine : BmsDefaultPlayfieldPalette.HitTargetLine;
            glowColour = isScratch ? BmsDefaultPlayfieldPalette.ScratchHitTargetGlow : BmsDefaultPlayfieldPalette.HitTargetGlow;

            InternalChildren = new Drawable[]
            {
                hitTargetVisual = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        bar = new Box
                        {
                            Anchor = Anchor.BottomLeft,
                            Origin = Anchor.BottomLeft,
                            RelativeSizeAxes = Axes.X,
                            Colour = barColour,
                        },
                        focusEdge = new Box
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            RelativeSizeAxes = Axes.X,
                            Alpha = 0,
                            Colour = BmsDefaultPlayfieldPalette.FocusAccent,
                        }
                    }
                },
                line = new Container
                {
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    RelativeSizeAxes = Axes.X,
                    Masking = true,
                    Child = lineFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = lineColour,
                    }
                },
                keyFlashVisual = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = pressedOverlay = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0,
                        Blending = BlendingParameters.Additive,
                        Colour = glowColour,
                    }
                }
            };

            initialiseLayout(layoutProfile, layoutSnapshot);
            updateState();
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin)
        {
            // Colour overrides drive the programmatic bar / line / glow; a texture (below) instead owns the look.
            var configuredBar = skin.GetBmsSkinConfig<Color4>(isScratch ? BmsSkinConfigurationLookups.ScratchHitTargetBarColour : BmsSkinConfigurationLookups.HitTargetBarColour, keymode)?.Value;
            if (configuredBar.HasValue)
                bar.Colour = configuredBar.Value;

            var configuredLine = skin.GetBmsSkinConfig<Color4>(isScratch ? BmsSkinConfigurationLookups.ScratchHitTargetLineColour : BmsSkinConfigurationLookups.HitTargetLineColour, keymode)?.Value;
            if (configuredLine.HasValue)
                lineFill.Colour = configuredLine.Value;

            var configuredGlow = skin.GetBmsSkinConfig<Color4>(isScratch ? BmsSkinConfigurationLookups.ScratchHitTargetGlowColour : BmsSkinConfigurationLookups.HitTargetGlowColour, keymode)?.Value;
            if (configuredGlow.HasValue)
            {
                glowColour = configuredGlow.Value;
                pressedOverlay.Colour = glowColour;
                applyGlow();
            }

            // Texture override: a HitTargetImage owns the static look — hide the programmatic bar / line; the press and
            // focus overlays still draw on top (Depth keeps the texture behind them).
            string? imagePath = skin.GetBmsSkinConfig<string>(BmsSkinConfigurationLookups.HitTargetImage, keymode)?.Value;
            var texture = !string.IsNullOrEmpty(imagePath) ? skin.GetTexture(imagePath) : null;

            if (texture != null)
            {
                bar.Alpha = 0;
                line.Alpha = 0;
                hitTargetVisual.Add(textureBase = new Sprite { RelativeSizeAxes = Axes.Both, Texture = texture, Depth = 1 });
            }
        }

        private void initialiseLayout(BmsPlayfieldLayoutProfile layoutProfile, BmsGameplayLayoutSnapshot? layoutSnapshot)
        {
            GlowRadius = layoutProfile.HitTargetGlowRadius;

            if (layoutSnapshot == null)
            {
                // Explicit isolated compatibility displays retain their historical pixel-sized metrics.
                bar.Height = layoutProfile.HitTargetBarHeight;
                line.Height = layoutProfile.HitTargetLineHeight;
                focusEdge.Height = layoutProfile.HitTargetLineHeight;
            }
            else
            {
                GameplaySkinLayoutRect targetRect = layoutSnapshot.HitTargetRect;
                GameplaySkinLayoutRect lineRect = layoutSnapshot.JudgementLineRect;
                bool reverse = layoutSnapshot.Context.ScrollDirection == GameplaySkinScrollDirection.Up;
                Anchor lineAnchor = reverse ? Anchor.TopLeft : Anchor.BottomLeft;

                // The outer target owns the exact target surface. Its children use ratios of that same surface so no
                // profile pixel metric can diverge from the neutral publication at DPI 1/2 (or any later scale).
                bar.RelativeSizeAxes = Axes.Both;
                bar.Height = Math.Clamp(layoutProfile.HitTargetBarHeight / layoutProfile.HitTargetHeight, 0, 1);
                bar.Anchor = bar.Origin = lineAnchor;

                line.RelativeSizeAxes = Axes.Both;
                line.Height = Math.Clamp(lineRect.Height / targetRect.Height, 0, 1);
                line.Anchor = line.Origin = lineAnchor;

                focusEdge.RelativeSizeAxes = Axes.Both;
                focusEdge.Height = line.Height;
                focusEdge.Anchor = focusEdge.Origin = lineAnchor;
            }

            applyGlow();
        }

        // Rebuilds the line's glow edge effect from the current radius + (possibly skin-overridden) glow colour. Called
        // from both layout changes (radius) and the skin load (colour) so a later layout change keeps the configured colour.
        private void applyGlow()
            => line.EdgeEffect = new EdgeEffectParameters
            {
                Type = EdgeEffectType.Glow,
                Radius = GlowRadius,
                Colour = glowColour,
            };

        public void SetPressed(bool isPressed)
        {
            this.isPressed = isPressed;
            updateState();
        }

        public void SetFocused(bool isFocused)
        {
            this.isFocused = isFocused;
            updateState();
        }

        private void updateState()
        {
            if (pressedOverlay == null || focusEdge == null)
                return;

            pressedOverlay.Alpha = isPressed ? 0.18f : 0;
            focusEdge.Alpha = isFocused ? 1 : 0;
        }
    }
}
