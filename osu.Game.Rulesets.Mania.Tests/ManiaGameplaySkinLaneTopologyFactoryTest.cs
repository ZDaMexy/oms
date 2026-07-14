// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Tests
{
    [TestFixture]
    public sealed class ManiaGameplaySkinLaneTopologyFactoryTest
    {
        [Test]
        public void TestProjectsSingleFourKeyStage()
        {
            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(4));
            GameplaySkinLaneTopologyGroup group = topology.GroupsInLogicalOrder.Single();

            Assert.Multiple(() =>
            {
                Assert.That(group.Identity.Id.Value, Is.EqualTo("mania.group.stage-1"));
                Assert.That(group.Identity.Side, Is.EqualTo(GameplaySkinLaneSide.Neutral));
                Assert.That(group.LogicalIndex, Is.Zero);
                Assert.That(group.VisualIndex, Is.Zero);
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value),
                    Is.EqualTo(new[]
                    {
                        "mania.lane.column-1",
                        "mania.lane.column-2",
                        "mania.lane.column-3",
                        "mania.lane.column-4",
                    }));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role), Is.All.EqualTo(GameplaySkinLaneRole.Key));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 4)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 4)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex), Is.EqualTo(Enumerable.Range(0, 4)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex), Is.EqualTo(Enumerable.Range(0, 4)));
            });
        }

        [TestCase(1, 0)]
        [TestCase(5, 2)]
        [TestCase(7, 3)]
        public void TestOddSingleStageUsesStageLocalSpecialKey(int columns, int expectedSpecialIndex)
        {
            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(columns));
            GameplaySkinLaneTopologyEntry special = topology.LanesInLogicalOrder.Single(
                lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey);

            Assert.Multiple(() =>
            {
                Assert.That(special.GlobalLogicalIndex, Is.EqualTo(expectedSpecialIndex));
                Assert.That(special.GroupLocalLogicalIndex, Is.EqualTo(expectedSpecialIndex));
                Assert.That(special.Identity.Id.Value, Is.EqualTo($"mania.lane.column-{expectedSpecialIndex + 1}"));
                Assert.That(topology.LanesInLogicalOrder.Count(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey), Is.EqualTo(1));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role), Has.None.EqualTo(GameplaySkinLaneRole.Scratch));
            });
        }

        [Test]
        public void TestDualFiveKeyStagesUseIndependentLocalSpecialKeys()
        {
            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(5, 5));
            GameplaySkinLaneTopologyGroup primary = topology.GroupsInLogicalOrder[0];
            GameplaySkinLaneTopologyGroup secondary = topology.GroupsInLogicalOrder[1];

            Assert.Multiple(() =>
            {
                Assert.That(topology.GroupsInLogicalOrder.Select(group => group.Identity.Id.Value),
                    Is.EqualTo(new[] { "mania.group.stage-1", "mania.group.stage-2" }));
                Assert.That(topology.GroupsInLogicalOrder.Select(group => group.Identity.Side),
                    Is.EqualTo(new[] { GameplaySkinLaneSide.Primary, GameplaySkinLaneSide.Secondary }));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 5)));
                Assert.That(secondary.LanesInLogicalOrder.Select(lane => lane.GlobalLogicalIndex), Is.EqualTo(Enumerable.Range(5, 5)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 5)));
                Assert.That(secondary.LanesInLogicalOrder.Select(lane => lane.GroupLocalLogicalIndex), Is.EqualTo(Enumerable.Range(0, 5)));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex), Is.EqualTo(Enumerable.Range(0, 10)));
                Assert.That(primary.LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex), Is.EqualTo(Enumerable.Range(0, 5)));
                Assert.That(secondary.LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex), Is.EqualTo(Enumerable.Range(0, 5)));
                Assert.That(primary.LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey).GlobalLogicalIndex, Is.EqualTo(2));
                Assert.That(secondary.LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey).GlobalLogicalIndex, Is.EqualTo(7));
                Assert.That(primary.LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey).GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(secondary.LanesInLogicalOrder.Single(lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey).GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(topology.LanesInLogicalOrder[7].Identity.Id.Value, Is.EqualTo("mania.lane.column-8"));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.Identity.Role), Has.None.EqualTo(GameplaySkinLaneRole.Scratch));
            });
        }

        [Test]
        public void TestMixedDualStagesUsePrefixSumRatherThanModuloOrGlobalCentre()
        {
            GameplaySkinLaneTopologySnapshot topology = ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(4, 5));
            GameplaySkinLaneTopologyEntry special = topology.LanesInLogicalOrder.Single(
                lane => lane.Identity.Role == GameplaySkinLaneRole.SpecialKey);

            Assert.Multiple(() =>
            {
                Assert.That(topology.LanesInLogicalOrder, Has.Count.EqualTo(9));
                Assert.That(special.GlobalLogicalIndex, Is.EqualTo(6));
                Assert.That(special.GroupLocalLogicalIndex, Is.EqualTo(2));
                Assert.That(special.Identity.Group.Id.Value, Is.EqualTo("mania.group.stage-2"));
                Assert.That(special.Identity.Id.Value, Is.EqualTo("mania.lane.column-7"));
                Assert.That(topology.LanesInLogicalOrder.Select(lane => lane.GlobalVisualIndex), Is.EqualTo(Enumerable.Range(0, 9)));
                Assert.That(topology.GroupsInLogicalOrder[0].LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex),
                    Is.EqualTo(Enumerable.Range(0, 4)));
                Assert.That(topology.GroupsInLogicalOrder[1].LanesInLogicalOrder.Select(lane => lane.GroupLocalVisualIndex),
                    Is.EqualTo(Enumerable.Range(0, 5)));
            });
        }

        [Test]
        public void TestRejectsNullOrUnsupportedStageShape()
        {
            ManiaBeatmap empty = createBeatmap(2);
            empty.Stages.Clear();
            ManiaBeatmap nullStages = createBeatmap(2);
            nullStages.Stages = null!;
            ManiaBeatmap nullStage = createBeatmap(2);
            nullStage.Stages[0] = null!;

            Assert.Multiple(() =>
            {
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(null!), Throws.ArgumentNullException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(empty), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(nullStages), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(nullStage), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(2, 2, 2)), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(ManiaRuleset.MAX_STAGE_KEYS + 1)), Throws.ArgumentException);
                Assert.That(() => ManiaGameplaySkinLaneTopologyFactory.Create(createBeatmap(2, ManiaRuleset.MAX_STAGE_KEYS + 1)), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestFactoryRemainsInternalAndPureTopologyOnly()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(ManiaGameplaySkinLaneTopologyFactory).IsNotPublic, Is.True);
                Assert.That(typeof(ManiaGameplaySkinLaneTopologyFactory).GetFields(), Is.Empty);
                Assert.That(typeof(ManiaGameplaySkinLaneTopologyFactory).GetProperties(), Is.Empty);
            });
        }

        private static ManiaBeatmap createBeatmap(params int[] stageColumns)
        {
            if (stageColumns.Length == 0)
                throw new ArgumentException("At least one stage is required.", nameof(stageColumns));

            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns[0]));

            foreach (int columns in stageColumns.Skip(1))
                beatmap.Stages.Add(new StageDefinition(columns));

            return beatmap;
        }
    }
}
