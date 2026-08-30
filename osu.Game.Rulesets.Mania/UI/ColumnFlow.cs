// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Rulesets.Mania.UI
{
    /// <summary>
    /// Projects the exact lane rectangles of a stage from the one gameplay layout snapshot.
    /// </summary>
    public partial class ColumnFlow<TContent> : CompositeDrawable
        where TContent : Drawable
    {
        public TContent[] Content { get; }

        private readonly Container<Container<TContent>> columns;
        private readonly StageDefinition stageDefinition;

        public GameplaySkinLayoutSnapshot LayoutSnapshot { get; private set; } = null!;

        public new bool Masking
        {
            get => base.Masking;
            set => base.Masking = value;
        }

        public ColumnFlow(StageDefinition stageDefinition)
        {
            this.stageDefinition = stageDefinition ?? throw new ArgumentNullException(nameof(stageDefinition));
            Content = new TContent[stageDefinition.Columns];
            RelativeSizeAxes = Axes.Both;
            Masking = true;

            InternalChild = columns = new Container<Container<TContent>>
            {
                RelativeSizeAxes = Axes.Both,
            };

            for (int i = 0; i < stageDefinition.Columns; i++)
            {
                columns.Add(new Container<TContent>
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativePositionAxes = Axes.Both,
                    RelativeSizeAxes = Axes.Both,
                });
            }
        }

        [BackgroundDependencyLoader(true)]
        private void load(
            ManiaGameplaySkinStageContext? stageContext,
            ISkinSource skin,
            GameplaySkinLayoutRevisionOwner? layoutOwner)
        {
            if (stageContext == null)
            {
                if (layoutOwner == null || layoutOwner.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                {
                    throw new InvalidOperationException(
                        "A standalone mania column flow requires an explicitly cached compatibility layout owner.");
                }

                GameplaySkinLayoutSnapshot compatibility = ManiaGameplaySkinLayout.CreateCompatibility(
                    new[] { stageDefinition }, skin, useSkinGeometry: false).Snapshot;
                stageContext = new ManiaGameplaySkinStageContext(
                    compatibility,
                    compatibility.Context.Topology.GroupsInLogicalOrder.Single());
            }

            ManiaGameplaySkinLayout.ValidateConsumerCarrier(stageContext.Snapshot, layoutOwner, "column flow");
            LayoutSnapshot = stageContext.Snapshot;
            GameplaySkinLayoutGroup group = stageContext.Group;

            if (group.TopologyGroup.LanesInLogicalOrder.Count != stageDefinition.Columns
                || group.TopologyGroup.LogicalIndex >= stageContext.Snapshot.Context.Topology.GroupsInLogicalOrder.Count
                || !ReferenceEquals(stageContext.Snapshot.Context.Topology.GroupsInLogicalOrder[group.TopologyGroup.LogicalIndex], group.TopologyGroup))
            {
                throw new InvalidOperationException("The exact mania layout stage does not match the production column flow.");
            }

            foreach (GameplaySkinLaneTopologyEntry topologyLane in group.TopologyGroup.LanesInLogicalOrder)
            {
                if ((uint)topologyLane.GroupLocalLogicalIndex >= (uint)stageDefinition.Columns)
                    throw new InvalidOperationException("The mania column flow received an invalid explicit group-local logical index.");

                GameplaySkinLayoutRect rect = stageContext.Snapshot.GetLane(topologyLane.Identity.Id).Rect;
                Container<TContent> column = columns[topologyLane.GroupLocalLogicalIndex];
                column.Position = new Vector2(
                    (rect.X - group.Rect.X) / group.Rect.Width,
                    (rect.Y - group.Rect.Y) / group.Rect.Height);
                column.Size = new Vector2(rect.Width / group.Rect.Width, rect.Height / group.Rect.Height);
            }
        }

        public void SetContentForColumn(int column, TContent content)
        {
            Content[column] = columns[column].Child = content;
        }
    }
}
