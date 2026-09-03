// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Bms.UI
{
    public interface IBmsHudLayoutDisplay
    {
        void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter);

        void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot);
    }

    /// <summary>
    /// Gameplay-root HUD owner. Unlike the generic global HUD lookup, this drawable lives below
    /// <see cref="DrawableBmsRuleset"/>, so it can enforce the exact typed publication around every custom display.
    /// </summary>
    internal partial class BmsHudLayoutPanel : SkinReloadableDrawable
    {
        private readonly BmsGameplayLayoutProvider layoutProvider;

        [Resolved(CanBeNull = true)]
        private GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime { get; set; }

        public BmsGameplayLayoutSnapshot LayoutSnapshot => layoutProvider.Current;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => layoutProvider.CurrentMaterialSet;

        internal BmsHudLayoutSnapshotCarrier? Carrier { get; private set; }

        public BmsHudLayoutPanel(BmsGameplayLayoutProvider layoutProvider)
        {
            this.layoutProvider = layoutProvider ?? throw new ArgumentNullException(nameof(layoutProvider));
            RelativeSizeAxes = Axes.Both;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            Drawable hudLayout = skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.HudLayout))
                                 ?? new DefaultBmsHudLayoutDisplay();

            if (hudLayout is not IBmsHudLayoutDisplay hudLayoutDisplay)
                throw new InvalidOperationException("bms.layout.hud-display-missing-snapshot-carrier");

            Drawable gaugeBar = skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.GaugeBar))
                                ?? new BmsGaugeBar();
            ComboCounter comboCounter = skin.GetDrawableComponent(new BmsSkinComponentLookup(BmsSkinComponents.ComboCounter)) as ComboCounter
                                        ?? new BmsComboCounter();

            // Opaque legacy HUD components have no stage-local partitions. Retain them for an all-fallback exact
            // publication, but fail closed to the typed engine host as soon as this revision authors/suppresses any
            // exact stage. This avoids a deck-2 key aggregate-hiding deck 1 (or vice versa).
            if (gaugeBar is not BmsGaugeBar && requiresIndependentlyGatedDefault(GameplaySkinSlotCatalog.GaugeVisual))
                gaugeBar = new BmsGaugeBar();

            if (comboCounter is not BmsComboCounter && requiresIndependentlyGatedDefault(GameplaySkinSlotCatalog.ComboDisplay))
                comboCounter = new BmsComboCounter();

            var replacement = new BmsHudLayoutSnapshotCarrier(hudLayout, hudLayoutDisplay);
            replacement.SetComponents(null, gaugeBar, comboCounter);

            BmsHudLayoutSnapshotCarrier? previous = Carrier;
            AddInternal(replacement);
            Carrier = replacement;

            if (previous != null)
                RemoveInternal(previous, disposeImmediately: true);
        }

        private bool requiresIndependentlyGatedDefault(GameplaySkinSlotDescriptor descriptor)
        {
            GameplaySkinSceneRuntimeHost? runtime = gameplaySkinSceneRuntime;

            if (runtime == null)
                return false;

            foreach (GameplaySkinLaneTopologyGroup group in layoutProvider.Current.Neutral.Context.Topology.GroupsInLogicalOrder)
            {
                var key = new GameplaySkinResolvedMaterialKey(descriptor, GameplaySkinResolvedMaterialTarget.ForStage(group));

                if (!runtime.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate) || gate == null)
                    throw new InvalidOperationException("The exact BMS HUD scene gate is missing from its committed publication.");

                if (gate.RoutedNodes.Count != 0)
                    return true;

                if (!runtime.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry) || entry == null)
                    throw new InvalidOperationException("The exact BMS HUD material entry is missing from its committed publication.");

                if (entry.State == GameplaySkinResolvedMaterialState.Suppress
                    || entry.Material is GameplaySkinPublicSlotMaterial { IsProgrammaticFallback: false })
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Production carrier which injects the exact gameplay-root snapshot before the custom HUD display loads.
    /// </summary>
    internal partial class BmsHudLayoutSnapshotCarrier : CompositeDrawable, IBmsHudLayoutDisplay
    {
        private readonly IBmsHudLayoutDisplay layoutDisplay;
        private readonly List<IDisposable> gameplaySkinSceneVisualRegistrations = new List<IDisposable>();
        private bool componentsForwarded;

        public Drawable Display { get; }

        internal Drawable? WrappedHud { get; private set; }

        internal Drawable? GaugeBar { get; private set; }

        internal ComboCounter? ComboCounter { get; private set; }

        internal Drawable? GaugeProgrammaticVisualOwner { get; private set; }

        internal Drawable? ComboProgrammaticVisualOwner { get; private set; }

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        internal GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; private set; }

        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinLayoutRevisionOwner? layoutOwner { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinSceneRuntimeHost? gameplaySkinSceneRuntime { get; set; }

        public BmsHudLayoutSnapshotCarrier(
            Drawable display,
            IBmsHudLayoutDisplay layoutDisplay)
        {
            Display = display ?? throw new ArgumentNullException(nameof(display));
            this.layoutDisplay = layoutDisplay ?? throw new ArgumentNullException(nameof(layoutDisplay));

            if (!ReferenceEquals(display, layoutDisplay))
                throw new ArgumentException("The BMS HUD layout carrier requires one exact display instance.", nameof(layoutDisplay));

            RelativeSizeAxes = Axes.Both;
            display.RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BmsGameplayLayoutSnapshot resolvedSnapshot = BmsGameplayLayoutProvider.ResolveOwnerPublication(
                layoutOwner,
                layoutProvider,
                "bms.layout.missing-hud-carrier-publication");

            if (LayoutSnapshot == null)
                InitialiseLayoutSnapshot(resolvedSnapshot);
            else if (!ReferenceEquals(LayoutSnapshot, resolvedSnapshot))
                throw new InvalidOperationException("The BMS HUD carrier does not retain the exact root publication.");

            ResolvedMaterialSet = BmsGameplayLayoutProvider.ResolveOwnerMaterialSet(
                layoutOwner,
                layoutProvider,
                "bms.material.missing-hud-carrier-publication");

            if (!ReferenceEquals(ResolvedMaterialSet.Snapshot, resolvedSnapshot.Neutral))
                throw new InvalidOperationException("The BMS HUD carrier does not retain the material set from its exact root publication.");

            // Do not attach (and therefore do not load) a custom display until it has received the exact owner adapter
            // and its component tuple. This prevents the display's first BDL/callback from observing an uninitialised
            // geometry carrier while keeping the transformer entirely stateless.
            InternalChild = Display;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            registerGameplaySkinSceneVisuals();
        }

        public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
        {
            if (GaugeBar != null || ComboCounter != null)
                throw new InvalidOperationException("A BMS HUD carrier accepts one immutable component tuple.");

            WrappedHud = wrappedHud;
            GaugeBar = gaugeBar ?? throw new ArgumentNullException(nameof(gaugeBar));
            ComboCounter = comboCounter ?? throw new ArgumentNullException(nameof(comboCounter));
            GaugeProgrammaticVisualOwner = gaugeBar is BmsGaugeBar
                ? gaugeBar
                : new StaticProgrammaticVisualOwner(gaugeBar);
            ComboProgrammaticVisualOwner = comboCounter is BmsComboCounter
                ? comboCounter
                : new StaticComboProgrammaticVisualOwner(comboCounter);
            forwardComponentsWhenReady();
        }

        public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (LayoutSnapshot != null && !ReferenceEquals(LayoutSnapshot, snapshot))
                throw new InvalidOperationException("A BMS HUD carrier cannot change its immutable layout snapshot.");

            if (LayoutSnapshot == null)
            {
                LayoutSnapshot = snapshot;
                layoutDisplay.InitialiseLayoutSnapshot(snapshot);
            }

            forwardComponentsWhenReady();
        }

        private void forwardComponentsWhenReady()
        {
            if (componentsForwarded || LayoutSnapshot == null || GaugeBar == null || ComboCounter == null)
                return;

            componentsForwarded = true;
            layoutDisplay.SetComponents(
                WrappedHud,
                GaugeProgrammaticVisualOwner ?? throw new InvalidOperationException("The BMS HUD gauge owner is not ready."),
                (ComboCounter)(ComboProgrammaticVisualOwner ?? throw new InvalidOperationException("The BMS HUD combo owner is not ready.")));
        }

        private void registerGameplaySkinSceneVisuals()
        {
            if (gameplaySkinSceneRuntime == null)
                return;

            if (LayoutSnapshot == null || ResolvedMaterialSet == null)
                throw new InvalidOperationException("The BMS HUD carrier cannot register scene gates before its exact publication is ready.");

            if (!ReferenceEquals(gameplaySkinSceneRuntime.Publication.Snapshot, LayoutSnapshot.Neutral)
                || !ReferenceEquals(gameplaySkinSceneRuntime.MaterialSet, ResolvedMaterialSet))
            {
                throw new InvalidOperationException("The BMS HUD scene gate owner requires its exact committed layout/material publication.");
            }

            GameplaySkinLaneTopologyGroup[] groups = LayoutSnapshot.Neutral.Context.Topology.GroupsInLogicalOrder.ToArray();
            registerComponent(
                GameplaySkinSlotCatalog.GaugeVisual,
                GaugeBar!,
                GaugeProgrammaticVisualOwner!,
                (GaugeBar as BmsGaugeBar)?.GameplaySkinStageFallbackVisuals,
                groups);
            registerComponent(
                GameplaySkinSlotCatalog.ComboDisplay,
                ComboCounter!,
                ComboProgrammaticVisualOwner!,
                (ComboCounter as BmsComboCounter)?.GameplaySkinStageFallbackVisuals,
                groups);
        }

        private void registerComponent(
            GameplaySkinSlotDescriptor slot,
            Drawable actualComponent,
            Drawable programmaticVisualOwner,
            IReadOnlyList<Drawable>? exactStagePartitions,
            IReadOnlyList<GameplaySkinLaneTopologyGroup> groups)
        {
            var keys = groups.Select(group => new GameplaySkinResolvedMaterialKey(
                slot,
                GameplaySkinResolvedMaterialTarget.ForStage(group))).ToArray();

            if (exactStagePartitions != null)
            {
                if (exactStagePartitions.Count != keys.Length)
                    throw new InvalidOperationException("A built-in BMS HUD component must expose one fallback partition per exact C3 stage.");

                for (int i = 0; i < keys.Length; i++)
                    gameplaySkinSceneVisualRegistrations.Add(gameplaySkinSceneRuntime!.RegisterProgrammaticVisual(keys[i], exactStagePartitions[i]));

                return;
            }

            // An all-fallback publication retains its indivisible legacy component without any scene gate. An
            // authored publication must already have failed closed in BmsHudLayoutPanel, before the display tree was
            // mounted; registering an aggregate here would incorrectly couple otherwise independent stages.
            if (ReferenceEquals(programmaticVisualOwner, actualComponent))
                throw new InvalidOperationException("An indivisible custom BMS HUD component requires a stable parent gate owner.");

            if (keys.Any(key =>
                gameplaySkinSceneRuntime!.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate)
                && gate != null
                && (gate.RoutedNodes.Count != 0
                    || gameplaySkinSceneRuntime.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry)
                    && entry != null
                    && (entry.State == GameplaySkinResolvedMaterialState.Suppress
                        || entry.Material is GameplaySkinPublicSlotMaterial { IsProgrammaticFallback: false }))))
            {
                throw new InvalidOperationException("An authored BMS HUD publication cannot retain an opaque aggregate component.");
            }
        }

        /// <summary>
        /// Stable multiplicative gate parent for an opaque custom gauge. The custom drawable remains free to animate
        /// its own alpha and transform without being able to undo an exact author replacement on this owner.
        /// </summary>
        private sealed partial class StaticProgrammaticVisualOwner : Container
        {
            public StaticProgrammaticVisualOwner(Drawable component)
            {
                RelativeSizeAxes = Axes.Both;
                InternalChild = component;
            }
        }

        /// <summary>
        /// ComboCounter-shaped stable owner required by the legacy HUD layout interface. The actual custom counter is
        /// the only visual child; this type adds no geometry, lookup or state authority.
        /// </summary>
        private sealed partial class StaticComboProgrammaticVisualOwner : ComboCounter
        {
            private readonly ComboCounter component;

            public StaticComboProgrammaticVisualOwner(ComboCounter component)
            {
                this.component = component;
            }

            protected override IHasText CreateText() => new ComponentHost(component);

            private sealed partial class ComponentHost : Container, IHasText
            {
                public LocalisableString Text { get; set; }

                public ComponentHost(Drawable component)
                {
                    AutoSizeAxes = Axes.Both;
                    InternalChild = component;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                foreach (IDisposable registration in gameplaySkinSceneVisualRegistrations)
                    registration.Dispose();

                gameplaySkinSceneVisualRegistrations.Clear();
            }

            base.Dispose(isDisposing);
        }
    }

    public partial class DefaultBmsHudLayoutDisplay : DefaultSkinComponentsContainer, IBmsHudLayoutDisplay
    {
        private Drawable gaugeBar = null!;
        private ComboCounter comboCounter = null!;

        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinLayoutRevisionOwner? layoutOwner { get; set; }

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        internal GameplaySkinResolvedMaterialSet? ResolvedMaterialSet { get; private set; }

        public DefaultBmsHudLayoutDisplay()
            : base(_ => { })
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            BmsGameplayLayoutSnapshot snapshot = BmsGameplayLayoutProvider.ResolveOwnerPublication(
                layoutOwner,
                layoutProvider,
                "bms.layout.missing-hud-publication");
            InitialiseLayoutSnapshot(snapshot);
            ResolvedMaterialSet = BmsGameplayLayoutProvider.ResolveOwnerMaterialSet(
                layoutOwner,
                layoutProvider,
                "bms.material.missing-hud-publication");

            if (!ReferenceEquals(ResolvedMaterialSet.Snapshot, snapshot.Neutral))
                throw new InvalidOperationException("The BMS HUD display does not retain the material set from its exact publication.");
        }

        public void InitialiseLayoutSnapshot(BmsGameplayLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (LayoutSnapshot != null && !ReferenceEquals(LayoutSnapshot, snapshot))
                throw new InvalidOperationException("A BMS HUD display cannot change its immutable layout snapshot.");

            LayoutSnapshot ??= snapshot;
        }

        public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
        {
            this.gaugeBar = gaugeBar;
            this.comboCounter = comboCounter;

            Clear();

            if (wrappedHud != null)
                Add(wrappedHud);

            Add(gaugeBar);
            Add(comboCounter);

            ScheduleAfterChildren(applyDefaults);
        }

        private void applyDefaults()
        {
            applyGaugePlacement();
            applyComboPlacement();

            // Safety net: the BMS default combo is removed from the wrapped HUD at the transformer level
            // (BmsSkinTransformer.stripDefaultHudElements). Any other stray combo gets hidden here.
            foreach (var combo in this.ChildrenOfType<ComboCounter>().Where(combo => combo != comboCounter))
                combo.Hide();

            foreach (var drawable in this.ChildrenOfType<ISerialisableDrawable>())
                drawable.UsesFixedAnchor = true;
        }

        // Centres the combo counter on the playfield centre (the intersection of the playfield's width / height midlines),
        // mirroring the playfield's P1/P2/centre side anchoring. Falls back to screen centre when geometry is unresolved.
        private void applyComboPlacement()
        {
            if (comboCounter == null)
                return;

            if (comboCounter is BmsComboCounter bmsCombo)
            {
                bmsCombo.ApplyStageLocalLayout();
                return;
            }

            if (LayoutSnapshot == null)
            {
                comboCounter.Hide();
                return;
            }

            var rect = LayoutSnapshot.ComboRect;

            comboCounter.Anchor = Anchor.TopLeft;
            comboCounter.Origin = Anchor.Centre;
            comboCounter.RelativePositionAxes = Axes.Both;
            comboCounter.Position = new Vector2(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        }

        // Places the gauge just below the judgement line, width-matched to the playfield strip and mirroring its P1/P2/centre
        // side anchoring. Falls back to a centred default when the playable beatmap / ruleset config are unavailable
        // (skin-editor preview, isolated tests) so the layout never throws.
        private void applyGaugePlacement()
        {
            if (gaugeBar == null)
                return;

            if (LayoutSnapshot == null)
            {
                gaugeBar.Hide();
                return;
            }

            var rect = LayoutSnapshot.GaugeRect;
            gaugeBar.Anchor = gaugeBar.Origin = Anchor.TopLeft;
            gaugeBar.RelativeSizeAxes = Axes.Both;
            gaugeBar.RelativePositionAxes = Axes.Both;
            gaugeBar.Position = new Vector2(rect.X, rect.Y);
            gaugeBar.Size = new Vector2(rect.Width, rect.Height);
        }
    }
}
