// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    /// <summary>
    /// A container to be used in a <see cref="ManiaSkinnableTestScene"/> to provide a resolvable <see cref="Column"/> dependency.
    /// </summary>
    public partial class ColumnTestContainer : Container
    {
        protected override Container<Drawable> Content => content;

        private readonly Container content;

        [Cached]
        private readonly Column column;

        [Cached]
        private readonly StageDefinition stageDefinition;

        private readonly StageDefinition[] layoutStageDefinitions;
        private readonly int layoutStageIndex;
        private readonly bool useSkinGeometry;

        public ColumnTestContainer(
            int column,
            ManiaAction action,
            bool showColumn = false,
            int stageColumns = 5,
            IReadOnlyList<int>? layoutStageColumns = null,
            int layoutStageIndex = 0,
            bool useSkinGeometry = false)
        {
            stageDefinition = new StageDefinition(stageColumns);
            layoutStageDefinitions = (layoutStageColumns ?? new[] { stageColumns }).Select(count => new StageDefinition(count)).ToArray();
            this.layoutStageIndex = layoutStageIndex;
            this.useSkinGeometry = useSkinGeometry;

            InternalChildren = new[]
            {
                this.column = new Column(column, false)
                {
                    Action = { Value = action },
                    Alpha = showColumn ? 1 : 0
                },
                content = new ManiaInputManager(new ManiaRuleset().RulesetInfo, stageColumns)
                {
                    RelativeSizeAxes = Axes.Both
                },
                this.column.TopLevelContainer.CreateProxy()
            };
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            ManiaGameplaySkinLayout compatibility = ManiaGameplaySkinLayout.CreateCompatibility(
                layoutStageDefinitions, parent.Get<ISkinSource>(), useSkinGeometry: useSkinGeometry);
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            // This helper is an isolation-only visual harness. Override any non-gameplay skin provider's exact owner
            // with an explicit detached authority so its locally constructed compatibility snapshot cannot impersonate
            // a production publication.
            dependencies.Cache(GameplaySkinLayoutRevisionOwner.CreateCompatibility());
            dependencies.Cache(compatibility);
            dependencies.Cache(compatibility.Snapshot);
            dependencies.Cache(GameplaySkinResolvedMaterialSet.CreateEmpty(compatibility.Snapshot));
            dependencies.Cache(new ManiaGameplaySkinStageContext(
                compatibility.Snapshot,
                compatibility.Snapshot.GroupsInLogicalOrder[layoutStageIndex].TopologyGroup));
            dependencies.Cache(new ManiaGameplaySkinLaneContext(compatibility.Snapshot, column.Index));
            return dependencies;
        }
    }
}
