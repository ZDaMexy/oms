// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// The sole scrolling owner for measure bar lines targeting one exact C3 stage/deck group.
    /// </summary>
    internal partial class BmsBarLinePlayfield : ScrollingPlayfield
    {
        internal const int POOL_CAPACITY = GameplaySkinPreparedSceneBudgets.MAX_SPECIALISED_VISUALS_PER_KEY;

        private readonly BmsHitObjectAreaLayoutController layoutController;
        private readonly DrawablePool<DrawableBmsBarLine> pool;
        private readonly Container visualArea;
        private readonly List<BmsBarLine> measureBarLines = new List<BmsBarLine>();

        public GameplaySkinLayoutGroup LayoutGroup { get; }

        public GameplaySkinLaneGroupId GroupId => LayoutGroup.GroupId;

        public int GroupLogicalIndex => LayoutGroup.TopologyGroup.LogicalIndex;

        internal float ProjectedBarLineHeight { get; }

        internal int PoolCapacity => POOL_CAPACITY;

        internal int PoolSize => pool.CurrentPoolSize;

        internal int PoolInUse => pool.CountInUse;

        internal IReadOnlyList<BmsBarLine> MeasureBarLines => measureBarLines;

        public BmsBarLinePlayfield(
            GameplaySkinLayoutGroup layoutGroup,
            BmsGameplayLayoutSnapshot layoutSnapshot,
            GameplaySkinResolvedMaterialSet materialSet,
            BindableFloat liftUnits)
        {
            LayoutGroup = layoutGroup ?? throw new ArgumentNullException(nameof(layoutGroup));
            ArgumentNullException.ThrowIfNull(layoutSnapshot);
            ArgumentNullException.ThrowIfNull(materialSet);
            ArgumentNullException.ThrowIfNull(liftUnits);

            if (!ReferenceEquals(materialSet.Snapshot, layoutSnapshot.Neutral)
                || !ReferenceEquals(layoutSnapshot.Neutral.GetGroup(layoutGroup.GroupId), layoutGroup))
            {
                throw new ArgumentException("A BMS bar-line owner must retain its exact C3 layout/material group.", nameof(materialSet));
            }

            RelativeSizeAxes = Axes.Both;
            ProjectedBarLineHeight = layoutSnapshot.ProjectVerticalProfileMetric(layoutSnapshot.Profile.BarLineHeight);
            visualArea = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                Child = HitObjectContainer,
            };
            layoutController = new BmsHitObjectAreaLayoutController(visualArea, layoutSnapshot.Profile, liftUnits);
            pool = new ExactBarLinePool(layoutGroup, layoutSnapshot, materialSet, POOL_CAPACITY);
            RegisterPool<BmsBarLine, DrawableBmsBarLine>(pool);
            AddInternal(visualArea);
        }

        [BackgroundDependencyLoader]
        private void load(IScrollingInfo scrollingInfo)
        {
            layoutController.Bind(scrollingInfo);
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            layoutController.Update();
        }

        internal void AddMeasureBarLine(double startTime)
        {
            var barLine = new BmsBarLine
            {
                StartTime = startTime,
                GroupLogicalIndex = GroupLogicalIndex,
                GroupId = GroupId,
                Major = true,
            };

            measureBarLines.Add(barLine);
            Add(barLine);
        }

        private sealed partial class ExactBarLinePool : DrawablePool<DrawableBmsBarLine>
        {
            private readonly GameplaySkinLayoutGroup group;
            private readonly BmsGameplayLayoutSnapshot layout;
            private readonly GameplaySkinResolvedMaterialSet materials;

            public ExactBarLinePool(
                GameplaySkinLayoutGroup group,
                BmsGameplayLayoutSnapshot layout,
                GameplaySkinResolvedMaterialSet materials,
                int capacity)
                : base(capacity, capacity)
            {
                this.group = group;
                this.layout = layout;
                this.materials = materials;
            }

            protected override DrawableBmsBarLine CreateNewDrawable()
                => new DrawableBmsBarLine(
                    new BmsBarLine
                    {
                        GroupLogicalIndex = group.TopologyGroup.LogicalIndex,
                        GroupId = group.GroupId,
                        Major = true,
                    },
                    group,
                    layout,
                    materials);
        }
    }
}
