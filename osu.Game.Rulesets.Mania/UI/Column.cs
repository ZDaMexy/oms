// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Configuration;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Objects.Drawables;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI.Components;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.UI
{
    [Cached]
    public partial class Column : ScrollingPlayfield, IKeyBindingHandler<ManiaAction>, IGameplaySkinSpecialisedSceneConsumer
    {
        /// <summary>
        /// The index of this column as part of the whole playfield.
        /// </summary>
        public readonly int Index;

        public readonly Bindable<ManiaAction> Action = new Bindable<ManiaAction>();

        public readonly ColumnHitObjectArea HitObjectArea;

        internal readonly Container BackgroundContainer = new Container { RelativeSizeAxes = Axes.Both };

        internal readonly Container TopLevelContainer = new Container { RelativeSizeAxes = Axes.Both };

        private DrawablePool<PoolableHitExplosion> hitExplosionPool = null!;
        private readonly OrderedHitPolicy hitPolicy;
        public Container UnderlayElements => HitObjectArea.UnderlayElements;

        private GameplaySampleTriggerSource sampleTriggerSource = null!;
        private SkinnableDrawable keyArea = null!;
        private ManiaGameplaySkinFailClosedSkinnableDrawable columnBackground = null!;
        private Container specialisedKeyVisualOwner = null!;
        private GameplaySkinSpecialisedSceneVisual? specialisedKeyVisual;
        private IDisposable? keyVisualProgrammaticRegistration;
        private readonly List<IDisposable> programmaticVisualPartRegistrations = new List<IDisposable>();
        private readonly HashSet<Drawable> registeredProgrammaticVisualPartOwners = new HashSet<Drawable>();
        private readonly List<(IManiaGameplaySkinProgrammaticVisualPartReadinessSource Source, Action Handler)> programmaticVisualPartReadinessSources = new();
        private GameplaySkinSceneHostedSlot? keyVisualGate;

        /// <summary>
        /// Whether this is a special (ie. scratch) column.
        /// </summary>
        public readonly bool IsSpecial;

        public readonly Bindable<Color4> AccentColour = new Bindable<Color4>(Color4.Black);

        private IBindable<bool> touchOverlay = null!;

        private float leftInputInflationRatio;
        private float rightInputInflationRatio;

        private ManiaGameplaySkinLaneContext layoutLaneContext = null!;
        private ManiaGameplaySkinMaterialContext materialContext = null!;

        public GameplaySkinLayoutSnapshot LayoutSnapshot => layoutLaneContext.Snapshot;

        public GameplaySkinLaneId LayoutLaneId => layoutLaneContext.Lane.LaneId;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet => materialContext.MaterialSet;

        public GameplaySkinResolvedMaterialKey ResolvedMaterialKey
            => materialContext.UsesResolvedMaterial ? materialContext.GetKey(ManiaSkinComponents.KeyArea) : null!;

        public GameplaySkinSceneHostedSlot SceneVisualGate => keyVisualGate!;

        public IReadOnlyList<string> AppliedSceneNodeIds { get; private set; } = Array.Empty<string>();

        internal int HitExplosionPoolCapacity { get; private set; }

        internal int HitExplosionPoolSize => hitExplosionPool.CurrentPoolSize;

        internal int HitExplosionsInUse => hitExplosionPool.CountInUse;

        internal long HitExplosionCapacityDropCount { get; private set; }

        internal IEnumerable<PoolableHitExplosion> ActiveHitExplosions
            => HitObjectArea.Explosions.Children.OfType<PoolableHitExplosion>();

        public Column(int index, bool isSpecial)
        {
            Index = index;
            IsSpecial = isSpecial;

            RelativeSizeAxes = Axes.Both;

            hitPolicy = new OrderedHitPolicy(HitObjectContainer);
            HitObjectArea = new ColumnHitObjectArea
            {
                RelativeSizeAxes = Axes.Both,
                Child = HitObjectContainer,
            };
        }

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private GameplaySkinSceneRuntimeHost? sceneRuntime { get; set; }

        [BackgroundDependencyLoader]
        private void load(GameHost host, ManiaRulesetConfigManager? rulesetConfig)
        {
            skin.SourceChanged += onSourceChanged;
            onSourceChanged();

            GameplaySkinResolvedMaterialKey? hitExplosionKey = null;
            GameplaySkinSceneHostedSlot? hitExplosionGate = null;

            if (sceneRuntime != null && materialContext.UsesResolvedMaterial)
            {
                hitExplosionKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.HitExplosion, materialContext.Target);

                if (!sceneRuntime.TryGetVisualGate(hitExplosionKey, out hitExplosionGate) || hitExplosionGate == null)
                    throw new InvalidOperationException("The exact mania hit-explosion scene gate is missing from its committed publication.");
            }

            HitExplosionPoolCapacity = hitExplosionGate?.SpecialisedPoolCapacity
                                       ?? GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY;
            hitExplosionPool = hitExplosionKey == null
                ? new DrawablePool<PoolableHitExplosion>(HitExplosionPoolCapacity, HitExplosionPoolCapacity)
                : new ExactHitExplosionPool(sceneRuntime!, hitExplosionKey, HitExplosionPoolCapacity);

            InternalChildren = new Drawable[]
            {
                hitExplosionPool,
                sampleTriggerSource = new GameplaySampleTriggerSource(HitObjectContainer),
                HitObjectArea,
                keyArea = new SkinnableDrawable(
                    materialContext.UsesResolvedMaterial
                        ? new ManiaSkinComponentLookup(ManiaSkinComponents.KeyArea, materialContext)
                        : new ManiaSkinComponentLookup(ManiaSkinComponents.KeyArea),
                    _ => new DefaultKeyArea())
                {
                    RelativeSizeAxes = Axes.Both,
                },
                specialisedKeyVisualOwner = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                },
                // For input purposes, the background is added at the highest depth, but is then proxied back below all other elements externally
                // (see `Stage.columnBackgrounds`).
                BackgroundContainer,
                TopLevelContainer
            };

            columnBackground = new ManiaGameplaySkinFailClosedSkinnableDrawable(
                new ManiaSkinComponentLookup(ManiaSkinComponents.ColumnBackground),
                _ => new DefaultColumnBackground())
            {
                RelativeSizeAxes = Axes.Both,
            };

            columnBackground.ApplyGameWideClock(host);
            keyArea.ApplyGameWideClock(host);

            BackgroundContainer.Add(columnBackground);
            TopLevelContainer.Add(HitObjectArea.Explosions.CreateProxy());

            if (sceneRuntime != null && materialContext.UsesResolvedMaterial)
            {
                watchProgrammaticVisualParts(
                    columnBackground,
                    GameplaySkinSlotCatalog.LaneSurface,
                    GameplaySkinSlotCatalog.LaneDivider,
                    GameplaySkinSlotCatalog.KeyFlash);
                watchProgrammaticVisualParts(
                    HitObjectArea.HitTarget,
                    GameplaySkinSlotCatalog.HitTarget,
                    GameplaySkinSlotCatalog.JudgementLine);
            }

            if (sceneRuntime != null && materialContext.UsesResolvedMaterial)
            {
                sceneRuntime.TryGetVisualGate(
                    new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.KeyVisual, materialContext.Target),
                    out keyVisualGate);
                if (keyVisualGate == null)
                    throw new InvalidOperationException("The exact mania key-visual scene gate is missing from its committed publication.");

                if (keyVisualGate.Route == GameplaySkinSceneHostRoute.Specialised)
                    specialisedKeyVisual = sceneRuntime.PrepareSpecialisedVisual(ResolvedMaterialKey, specialisedKeyVisualOwner);

                if (keyVisualGate.Route == GameplaySkinSceneHostRoute.Suppressed || specialisedKeyVisual != null)
                    keyVisualProgrammaticRegistration = sceneRuntime.RegisterProgrammaticVisual(ResolvedMaterialKey, keyArea);

                if (specialisedKeyVisual != null)
                {
                    AppliedSceneNodeIds = Array.AsReadOnly(
                        keyVisualGate.RoutedNodes.Select(node => node.InstanceId).ToArray());
                    specialisedKeyVisual.OnApply();
                }

            }

            RegisterPool<Note, DrawableNote>(10, 50);
            RegisterPool<HoldNote, DrawableHoldNote>(10, 50);
            RegisterPool<HeadNote, DrawableHoldNoteHead>(10, 50);
            RegisterPool<TailNote, DrawableHoldNoteTail>(10, 50);
            RegisterPool<HoldNoteBody, DrawableHoldNoteBody>(10, 50);

            if (rulesetConfig != null)
                touchOverlay = rulesetConfig.GetBindable<bool>(ManiaRulesetSetting.TouchOverlay);
        }

        private void onSourceChanged()
        {
            AccentColour.Value = skin.GetManiaSkinConfig<Color4>(LegacyManiaSkinConfigurationLookups.ColumnBackgroundColour, Index)?.Value ?? Color4.Black;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (sceneRuntime != null && materialContext.UsesResolvedMaterial)
            {
                registerProgrammaticVisualParts(
                    columnBackground,
                    GameplaySkinSlotCatalog.LaneSurface,
                    GameplaySkinSlotCatalog.LaneDivider,
                    GameplaySkinSlotCatalog.KeyFlash);
                registerProgrammaticVisualParts(
                    HitObjectArea.HitTarget,
                    GameplaySkinSlotCatalog.HitTarget,
                    GameplaySkinSlotCatalog.JudgementLine);
            }

            NewResult += OnNewResult;
        }

        private void registerProgrammaticVisualParts(
            ManiaGameplaySkinFailClosedSkinnableDrawable wrapper,
            params GameplaySkinSlotDescriptor[] componentSlots)
        {
            Drawable component = wrapper.Drawable;

            if (component is not IManiaGameplaySkinProgrammaticVisualPartProvider provider)
            {
                if (!hasSelectedPublicOwnership(componentSlots))
                    return;

                // An arbitrary custom component cannot prove independent ownership of the public parts it combines.
                // Once this exact lane/stage has a valid selected declaration, switch only this component to the
                // closed typed fallback whose parts can be gated independently; never any-gate the custom subtree.
                wrapper.UseClosedFallback();
                component = wrapper.Drawable;
                provider = component as IManiaGameplaySkinProgrammaticVisualPartProvider
                           ?? throw new InvalidOperationException("A closed Mania component fallback must expose independently gateable public parts.");
            }

            registerProviderProgrammaticVisualParts(provider);
        }

        private void registerProviderProgrammaticVisualParts(IManiaGameplaySkinProgrammaticVisualPartProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);

            GameplaySkinLaneTopologyEntry lane = layoutLaneContext.Lane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = LayoutSnapshot.GetGroup(lane.Identity.Group.Id).TopologyGroup;
            ManiaGameplaySkinProgrammaticVisualPart[] pending = provider.GameplaySkinProgrammaticVisualParts
                .Where(part => !registeredProgrammaticVisualPartOwners.Contains(part.Owner))
                .ToArray();

            if (pending.Length == 0)
                return;

            if (pending.Select(part => part.Owner).Distinct().Count() != pending.Length)
                throw new InvalidOperationException("One native Mania visual owner cannot carry multiple independent public-slot gates.");

            var provisional = new List<IDisposable>(pending.Length);

            try
            {
                foreach (ManiaGameplaySkinProgrammaticVisualPart part in pending)
                {
                    GameplaySkinResolvedMaterialTarget target = ManiaGameplaySkinProgrammaticVisualPartTargetResolver.Resolve(part, group, lane);
                    provisional.Add(sceneRuntime!.RegisterProgrammaticVisual(
                        new GameplaySkinResolvedMaterialKey(part.Slot, target),
                        part.Owner));
                }
            }
            catch
            {
                for (int index = provisional.Count - 1; index >= 0; index--)
                    provisional[index].Dispose();

                throw;
            }

            foreach (ManiaGameplaySkinProgrammaticVisualPart part in pending)
                registeredProgrammaticVisualPartOwners.Add(part.Owner);

            programmaticVisualPartRegistrations.AddRange(provisional);
        }

        private bool hasSelectedPublicOwnership(IReadOnlyList<GameplaySkinSlotDescriptor> componentSlots)
        {
            if (componentSlots.Count == 0 || componentSlots.Distinct().Count() != componentSlots.Count)
                throw new InvalidOperationException("A custom Mania visual fallback requires a fixed unique public-slot set.");

            GameplaySkinLaneTopologyEntry lane = layoutLaneContext.Lane.TopologyEntry;
            GameplaySkinLaneTopologyGroup group = LayoutSnapshot.GetGroup(lane.Identity.Group.Id).TopologyGroup;

            return componentSlots.Any(slot =>
            {
                var part = new ManiaGameplaySkinProgrammaticVisualPart(slot, this);
                GameplaySkinResolvedMaterialTarget target = ManiaGameplaySkinProgrammaticVisualPartTargetResolver.Resolve(part, group, lane);
                var key = new GameplaySkinResolvedMaterialKey(slot, target);
                return materialContext.MaterialSet.TryGet(key, out GameplaySkinResolvedMaterialEntry? entry)
                       && entry.Source.IsSelectedDocumentDeclaration;
            });
        }

        private void watchProgrammaticVisualParts(
            ManiaGameplaySkinFailClosedSkinnableDrawable wrapper,
            params GameplaySkinSlotDescriptor[] componentSlots)
        {
            Drawable component = wrapper.Drawable;

            if (component is not IManiaGameplaySkinProgrammaticVisualPartProvider
                && hasSelectedPublicOwnership(componentSlots))
            {
                wrapper.UseClosedFallback();
                component = wrapper.Drawable;
            }

            if (component is IManiaGameplaySkinProgrammaticVisualPartReadinessSource readinessSource)
            {
                Action handler = () => registerProgrammaticVisualParts(wrapper, componentSlots);
                readinessSource.GameplaySkinProgrammaticVisualPartsReady += handler;
                programmaticVisualPartReadinessSources.Add((readinessSource, handler));
            }

            registerProgrammaticVisualParts(wrapper, componentSlots);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // must happen before children are disposed in base call to prevent illegal accesses to the hit explosion pool.
                NewResult -= OnNewResult;
                keyVisualProgrammaticRegistration?.Dispose();

                foreach (var (source, handler) in programmaticVisualPartReadinessSources)
                    source.GameplaySkinProgrammaticVisualPartsReady -= handler;

                programmaticVisualPartReadinessSources.Clear();

                foreach (IDisposable registration in programmaticVisualPartRegistrations)
                    registration.Dispose();

                programmaticVisualPartRegistrations.Clear();
            }

            base.Dispose(isDisposing);

            if (isDisposing && skin.IsNotNull())
                skin.SourceChanged -= onSourceChanged;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            IReadOnlyDependencyContainer effectiveParent = parent;
            parent.TryGet(out GameplaySkinLayoutRevisionOwner layoutOwner);

            if (!parent.TryGet(out GameplaySkinLayoutSnapshot snapshot))
            {
                if (layoutOwner == null || layoutOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                {
                    throw new InvalidOperationException(
                        "A standalone mania column requires an explicitly cached compatibility layout owner.");
                }

                int stageColumns = parent.TryGet(out StageDefinition definition)
                    ? Math.Max(definition.Columns, Index + 1)
                    : Index + 1;
                GameplaySkinScrollDirection direction = parent.TryGet(out IScrollingInfo scrollingInfo)
                    && scrollingInfo.Direction.Value == ScrollingDirection.Up
                        ? GameplaySkinScrollDirection.Up
                        : GameplaySkinScrollDirection.Down;
                ManiaGameplaySkinLayout compatibility = ManiaGameplaySkinLayout.CreateCompatibility(
                    new[] { new StageDefinition(stageColumns) }, parent.Get<ISkinSource>(), direction, useSkinGeometry: false);
                var compatibilityDependencies = new DependencyContainer(parent);
                compatibilityDependencies.Cache(compatibility);
                compatibilityDependencies.Cache(compatibility.Snapshot);
                effectiveParent = compatibilityDependencies;
                snapshot = compatibility.Snapshot;
            }

            ManiaGameplaySkinLayout.ValidateConsumerCarrier(snapshot, layoutOwner, "column");

            var dependencies = new DependencyContainer(base.CreateChildDependencies(effectiveParent));
            dependencies.CacheAs<IBindable<ManiaAction>>(Action);
            layoutLaneContext = new ManiaGameplaySkinLaneContext(snapshot, Index);
            dependencies.Cache(layoutLaneContext);

            GameplaySkinLayoutLane lane = layoutLaneContext.Lane;
            GameplaySkinLaneTopologyGroup group = snapshot.GetGroup(lane.TopologyEntry.Identity.Group.Id).TopologyGroup;
            if (!effectiveParent.TryGet(out GameplaySkinResolvedMaterialSet materialSet))
            {
                if (layoutOwner == null || layoutOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                    throw new InvalidOperationException("An exact mania column requires its committed material publication.");

                materialSet = GameplaySkinResolvedMaterialSet.CreateEmpty(snapshot);
                dependencies.Cache(materialSet);
            }

            if (!ReferenceEquals(materialSet.Snapshot, snapshot))
                throw new InvalidOperationException("A mania column cannot mix layout and material revisions.");

            materialContext = new ManiaGameplaySkinMaterialContext(
                materialSet,
                GameplaySkinResolvedMaterialTarget.ForLane(group, lane.TopologyEntry));
            dependencies.Cache(materialContext);
            dependencies.Cache(new ManiaGameplaySkinStageContext(snapshot, group));
            GameplaySkinLaneTopologyEntry[] stageLanes = group.LanesInLogicalOrder.ToArray();
            int localIndex = lane.TopologyEntry.GroupLocalLogicalIndex;

            if (localIndex > 0)
            {
                GameplaySkinLayoutRect previous = snapshot.GetLane(stageLanes[localIndex - 1].Identity.Id).Rect;
                leftInputInflationRatio = Math.Max(0, lane.Rect.Left - previous.Right) / lane.Rect.Width / 2;
            }

            if (localIndex < stageLanes.Length - 1)
            {
                GameplaySkinLayoutRect next = snapshot.GetLane(stageLanes[localIndex + 1].Identity.Id).Rect;
                rightInputInflationRatio = Math.Max(0, next.Left - lane.Rect.Right) / lane.Rect.Width / 2;
            }

            return dependencies;
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawableHitObject)
        {
            base.OnNewDrawableHitObject(drawableHitObject);

            DrawableManiaHitObject maniaObject = (DrawableManiaHitObject)drawableHitObject;

            maniaObject.AccentColour.BindTo(AccentColour);
            maniaObject.CheckHittable = hitPolicy.IsHittable;
        }

        internal void OnNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            if (result.IsHit)
                hitPolicy.HandleHit(judgedObject);

            if (!result.IsHit || !judgedObject.DisplayResult || !DisplayJudgements.Value)
                return;

            ShowHitExplosion(result);
        }

        internal void ShowHitExplosion(JudgementResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (hitExplosionPool.CountInUse >= HitExplosionPoolCapacity)
            {
                HitExplosionCapacityDropCount++;
                return;
            }

            if (gameplaySkinObjectIdentityProvider == null && sceneRuntime != null && materialContext.UsesResolvedMaterial)
                throw new InvalidOperationException("A production mania hit explosion requires the engine-owned object identity provider.");

            long objectId = gameplaySkinObjectIdentityProvider?.GetObjectId(result.HitObject, materialContext.Target.GroupId) ?? 0;
            HitObjectArea.Explosions.Add(hitExplosionPool.Get(e => e.Apply(result, objectId)));
        }

        [Resolved(CanBeNull = true)]
        private IManiaGameplaySkinObjectIdentityProvider? gameplaySkinObjectIdentityProvider { get; set; }

        private sealed partial class ExactHitExplosionPool : DrawablePool<PoolableHitExplosion>
        {
            private readonly GameplaySkinSceneRuntimeHost sceneRuntime;
            private readonly GameplaySkinResolvedMaterialKey key;

            public ExactHitExplosionPool(
                GameplaySkinSceneRuntimeHost sceneRuntime,
                GameplaySkinResolvedMaterialKey key,
                int capacity)
                : base(capacity, capacity)
            {
                this.sceneRuntime = sceneRuntime;
                this.key = key;
            }

            protected override PoolableHitExplosion CreateNewDrawable()
                => new PoolableHitExplosion(sceneRuntime, key);
        }

        public bool OnPressed(KeyBindingPressEvent<ManiaAction> e)
        {
            if (e.Action != Action.Value)
                return false;

            sampleTriggerSource.Play();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<ManiaAction> e)
        {
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            // Extend input coverage to half of the exact solved gaps close to this lane.
            var spacingInflation = new MarginPadding
            {
                Left = DrawWidth * leftInputInflationRatio,
                Right = DrawWidth * rightInputInflationRatio,
            };
            return DrawRectangle.Inflate(spacingInflation).Contains(ToLocalSpace(screenSpacePos));
        }

        #region Touch Input

        [Resolved]
        private ManiaInputManager? maniaInputManager { get; set; }

        private int touchActivationCount;

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            // if touch overlay is visible, disallow columns from handling touch directly.
            if (touchOverlay.Value)
                return false;

            maniaInputManager?.KeyBindingContainer.TriggerPressed(Action.Value);
            touchActivationCount++;
            return true;
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            touchActivationCount--;

            if (touchActivationCount == 0)
                maniaInputManager?.KeyBindingContainer.TriggerReleased(Action.Value);
        }

        #endregion
    }
}
