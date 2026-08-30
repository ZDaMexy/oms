// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
    public partial class Column : ScrollingPlayfield, IKeyBindingHandler<ManiaAction>
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

        /// <summary>
        /// Whether this is a special (ie. scratch) column.
        /// </summary>
        public readonly bool IsSpecial;

        public readonly Bindable<Color4> AccentColour = new Bindable<Color4>(Color4.Black);

        private IBindable<bool> touchOverlay = null!;

        private float leftInputInflationRatio;
        private float rightInputInflationRatio;

        private ManiaGameplaySkinLaneContext layoutLaneContext = null!;

        public GameplaySkinLayoutSnapshot LayoutSnapshot => layoutLaneContext.Snapshot;

        public GameplaySkinLaneId LayoutLaneId => layoutLaneContext.Lane.LaneId;

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

        [BackgroundDependencyLoader]
        private void load(GameHost host, ManiaRulesetConfigManager? rulesetConfig)
        {
            SkinnableDrawable keyArea;

            skin.SourceChanged += onSourceChanged;
            onSourceChanged();

            InternalChildren = new Drawable[]
            {
                hitExplosionPool = new DrawablePool<PoolableHitExplosion>(5),
                sampleTriggerSource = new GameplaySampleTriggerSource(HitObjectContainer),
                HitObjectArea,
                keyArea = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.KeyArea), _ => new DefaultKeyArea())
                {
                    RelativeSizeAxes = Axes.Both,
                },
                // For input purposes, the background is added at the highest depth, but is then proxied back below all other elements externally
                // (see `Stage.columnBackgrounds`).
                BackgroundContainer,
                TopLevelContainer
            };

            var background = new SkinnableDrawable(new ManiaSkinComponentLookup(ManiaSkinComponents.ColumnBackground), _ => new DefaultColumnBackground())
            {
                RelativeSizeAxes = Axes.Both,
            };

            background.ApplyGameWideClock(host);
            keyArea.ApplyGameWideClock(host);

            BackgroundContainer.Add(background);
            TopLevelContainer.Add(HitObjectArea.Explosions.CreateProxy());

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
            NewResult += OnNewResult;
        }

        protected override void Dispose(bool isDisposing)
        {
            // must happen before children are disposed in base call to prevent illegal accesses to the hit explosion pool.
            NewResult -= OnNewResult;

            base.Dispose(isDisposing);

            if (skin.IsNotNull())
                skin.SourceChanged -= onSourceChanged;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            IReadOnlyDependencyContainer effectiveParent = parent;
            parent.TryGet(out GameplaySkinLayoutRevisionOwner layoutOwner);

            if (!parent.TryGet(out             GameplaySkinLayoutSnapshot snapshot))
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

            HitObjectArea.Explosions.Add(hitExplosionPool.Get(e => e.Apply(result)));
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
