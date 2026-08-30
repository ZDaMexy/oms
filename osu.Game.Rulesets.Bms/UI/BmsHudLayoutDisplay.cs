// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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

        public BmsGameplayLayoutSnapshot LayoutSnapshot => layoutProvider.Current;

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

            var replacement = new BmsHudLayoutSnapshotCarrier(hudLayout, hudLayoutDisplay);
            replacement.SetComponents(null, gaugeBar, comboCounter);

            BmsHudLayoutSnapshotCarrier? previous = Carrier;
            AddInternal(replacement);
            Carrier = replacement;

            if (previous != null)
                RemoveInternal(previous, disposeImmediately: true);
        }
    }

    /// <summary>
    /// Production carrier which injects the exact gameplay-root snapshot before the custom HUD display loads.
    /// </summary>
    internal partial class BmsHudLayoutSnapshotCarrier : CompositeDrawable, IBmsHudLayoutDisplay
    {
        private readonly IBmsHudLayoutDisplay layoutDisplay;
        private bool componentsForwarded;

        public Drawable Display { get; }

        internal Drawable? WrappedHud { get; private set; }

        internal Drawable? GaugeBar { get; private set; }

        internal ComboCounter? ComboCounter { get; private set; }

        internal BmsGameplayLayoutSnapshot? LayoutSnapshot { get; private set; }

        [Resolved(CanBeNull = true)]
        private BmsGameplayLayoutProvider? layoutProvider { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplaySkinLayoutRevisionOwner? layoutOwner { get; set; }

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

            // Do not attach (and therefore do not load) a custom display until it has received the exact owner adapter
            // and its component tuple. This prevents the display's first BDL/callback from observing an uninitialised
            // geometry carrier while keeping the transformer entirely stateless.
            InternalChild = Display;
        }

        public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
        {
            if (GaugeBar != null || ComboCounter != null)
                throw new InvalidOperationException("A BMS HUD carrier accepts one immutable component tuple.");

            WrappedHud = wrappedHud;
            GaugeBar = gaugeBar ?? throw new ArgumentNullException(nameof(gaugeBar));
            ComboCounter = comboCounter ?? throw new ArgumentNullException(nameof(comboCounter));
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
            layoutDisplay.SetComponents(WrappedHud, GaugeBar, ComboCounter);
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

        public DefaultBmsHudLayoutDisplay()
            : base(_ => { })
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InitialiseLayoutSnapshot(BmsGameplayLayoutProvider.ResolveOwnerPublication(
                layoutOwner,
                layoutProvider,
                "bms.layout.missing-hud-publication"));
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
