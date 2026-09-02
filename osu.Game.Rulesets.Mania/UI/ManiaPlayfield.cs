// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    [Cached]
    public partial class ManiaPlayfield : ScrollingPlayfield
    {
        public IReadOnlyList<Stage> Stages => stages;

        private readonly List<Stage> stages = new List<Stage>();

        private readonly Container stageContainer;

        private readonly StageDefinition[] stageDefinitions;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public GameplaySkinResolvedMaterialSet ResolvedMaterialSet { get; private set; } = null!;

        public override Quad SkinnableComponentScreenSpaceDrawQuad
        {
            get
            {
                RectangleF totalArea = RectangleF.Empty;

                for (int i = 0; i < Stages.Count; ++i)
                {
                    var stageArea = Stages[i].ScreenSpaceDrawQuad.AABBFloat;
                    totalArea = i == 0 ? stageArea : RectangleF.Union(totalArea, stageArea);
                }

                return totalArea;
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
        {
            foreach (var s in stages)
            {
                if (s.ReceivePositionalInputAt(screenSpacePos))
                    return true;
            }

            return false;
        }

        public ManiaPlayfield(List<StageDefinition> stageDefinitions)
        {
            ArgumentNullException.ThrowIfNull(stageDefinitions);

            StageDefinition[] copiedStageDefinitions = stageDefinitions.ToArray();
            this.stageDefinitions = copiedStageDefinitions;

            if (copiedStageDefinitions.Length <= 0)
                throw new ArgumentException("Can't have zero or fewer stages.");

            AddInternal(stageContainer = new Container
            {
                RelativeSizeAxes = Axes.Both,
            });

            var columnAction = ManiaAction.Key1;
            int firstColumnIndex = 0;

            for (int i = 0; i < copiedStageDefinitions.Length; i++)
            {
                var newStage = CreateStage(firstColumnIndex, copiedStageDefinitions[i], ref columnAction);

                stageContainer.Add(newStage);

                stages.Add(newStage);
                AddNested(newStage);

                firstColumnIndex += newStage.Columns.Length;
            }
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            parent.TryGet(out GameplaySkinLayoutRevisionOwner layoutOwner);

            if (parent.TryGet(out GameplaySkinLayoutSnapshot existingSnapshot))
            {
                ManiaGameplaySkinLayout.ValidateConsumerCarrier(existingSnapshot, layoutOwner, "playfield");
                if (!parent.TryGet(out GameplaySkinResolvedMaterialSet materialSet)
                    || !ReferenceEquals(materialSet.Snapshot, existingSnapshot))
                {
                    throw new InvalidOperationException("The exact mania playfield requires its matching material publication.");
                }

                ResolvedMaterialSet = materialSet;
                applyLayout(existingSnapshot);
                return base.CreateChildDependencies(parent);
            }

            if (layoutOwner == null || layoutOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
            {
                throw new InvalidOperationException(
                    "A standalone mania playfield requires an explicitly cached compatibility layout owner.");
            }

            ISkinSource skin = parent.Get<ISkinSource>();
            GameplaySkinScrollDirection direction = parent.TryGet(out IScrollingInfo scrollingInfo)
                && scrollingInfo.Direction.Value == ScrollingDirection.Up
                    ? GameplaySkinScrollDirection.Up
                    : GameplaySkinScrollDirection.Down;
            ManiaGameplaySkinLayout compatibility = ManiaGameplaySkinLayout.CreateCompatibility(stageDefinitions, skin, direction, useSkinGeometry: false);
            ManiaGameplaySkinLayout.ValidateConsumerCarrier(compatibility.Snapshot, layoutOwner, "playfield");
            applyLayout(compatibility.Snapshot);
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(compatibility);
            dependencies.Cache(compatibility.Snapshot);
            ResolvedMaterialSet = GameplaySkinResolvedMaterialSet.CreateEmpty(compatibility.Snapshot);
            dependencies.Cache(ResolvedMaterialSet);
            return dependencies;
        }

        private void applyLayout(GameplaySkinLayoutSnapshot layoutSnapshot)
        {
            LayoutSnapshot = layoutSnapshot;
            GameplaySkinLayoutRect screen = layoutSnapshot.Context.ScreenBounds;

            if (layoutSnapshot.GroupsInLogicalOrder.Count != stages.Count)
                throw new InvalidOperationException("The exact mania layout stage vector does not match the production playfield.");

            for (int i = 0; i < stages.Count; i++)
            {
                GameplaySkinLayoutGroup group = layoutSnapshot.GroupsInLogicalOrder[i];

                if (group.TopologyGroup.LogicalIndex != i
                    || group.TopologyGroup.LanesInLogicalOrder.Count != stageDefinitions[i].Columns)
                {
                    throw new InvalidOperationException("The exact mania layout group is not coherent with the ordered native stage vector.");
                }

                GameplaySkinLayoutRect rect = group.Rect;
                Stage stage = stages[i];
                stage.Anchor = stage.Origin = Anchor.TopLeft;
                stage.RelativePositionAxes = Axes.Both;
                stage.RelativeSizeAxes = Axes.Both;
                stage.Position = new Vector2((rect.X - screen.X) / screen.Width, (rect.Y - screen.Y) / screen.Height);
                stage.Size = new Vector2(rect.Width / screen.Width, rect.Height / screen.Height);
            }
        }

        [Pure]
        protected virtual Stage CreateStage(int firstColumnIndex, StageDefinition stageDefinition, ref ManiaAction columnAction) => new Stage(firstColumnIndex, stageDefinition, ref columnAction);

        public override void Add(HitObject hitObject) => getStageByColumn(((ManiaHitObject)hitObject).Column).Add(hitObject);

        public override bool Remove(HitObject hitObject) => getStageByColumn(((ManiaHitObject)hitObject).Column).Remove(hitObject);

        public override void Add(DrawableHitObject h) => getStageByColumn(((ManiaHitObject)h.HitObject).Column).Add(h);

        public override bool Remove(DrawableHitObject h) => getStageByColumn(((ManiaHitObject)h.HitObject).Column).Remove(h);

        public void Add(BarLine barLine) => stages.ForEach(s => s.Add(barLine));

        /// <summary>
        /// Retrieves a column from a screen-space position.
        /// </summary>
        /// <param name="screenSpacePosition">The screen-space position.</param>
        /// <returns>The column which the <paramref name="screenSpacePosition"/> lies in.</returns>
        public Column GetColumnByPosition(Vector2 screenSpacePosition)
        {
            Column found = null;

            foreach (var stage in stages)
            {
                foreach (var column in stage.Columns)
                {
                    if (column.ReceivePositionalInputAt(new Vector2(screenSpacePosition.X, column.ScreenSpaceDrawQuad.Centre.Y)))
                    {
                        found = column;
                        break;
                    }
                }

                if (found != null)
                    break;
            }

            return found;
        }

        /// <summary>
        /// Retrieves a <see cref="Column"/> by index.
        /// </summary>
        /// <param name="index">The index of the column.</param>
        /// <returns>The <see cref="Column"/> corresponding to the given index.</returns>
        /// <exception cref="ArgumentOutOfRangeException">If <paramref name="index"/> is less than 0 or greater than <see cref="TotalColumns"/>.</exception>
        public Column GetColumn(int index)
        {
            if (index < 0 || index > TotalColumns - 1)
                throw new ArgumentOutOfRangeException(nameof(index));

            foreach (var stage in stages)
            {
                if (index >= stage.Columns.Length)
                {
                    index -= stage.Columns.Length;
                    continue;
                }

                return stage.Columns[index];
            }

            throw new ArgumentOutOfRangeException(nameof(index));
        }

        /// <summary>
        /// Retrieves the total amount of columns across all stages in this playfield.
        /// </summary>
        public int TotalColumns
        {
            get
            {
                int sum = 0;

                foreach (var stage in stages)
                    sum += stage.Columns.Length;

                return sum;
            }
        }

        private Stage getStageByColumn(int column)
        {
            int sum = 0;

            foreach (var stage in stages)
            {
                sum += stage.Columns.Length;
                if (sum > column)
                    return stage;
            }

            return null;
        }
    }
}
