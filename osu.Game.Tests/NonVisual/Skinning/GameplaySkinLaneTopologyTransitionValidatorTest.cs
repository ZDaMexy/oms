// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLaneTopologyTransitionValidatorTest
    {
        [Test]
        public void TestRejectsNullSnapshot()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologyTransitionValidator.Validate(null!, topology), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneTopologyTransitionValidator.Validate(topology, null!), Throws.ArgumentNullException);
            });
        }

        [Test]
        public void TestAcceptsIndependentEquivalentRebuild()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology();

            Assert.Multiple(() =>
            {
                Assert.That(current, Is.Not.SameAs(previous));
                Assert.That(current.GroupsInLogicalOrder[0].Identity, Is.Not.SameAs(previous.GroupsInLogicalOrder[0].Identity));
                Assert.That(() => GameplaySkinLaneTopologyTransitionValidator.Validate(previous, current), Throws.Nothing);
            });
        }

        [Test]
        public void TestAllowsPresentationSideAndVisualOrderToChange()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(
                firstSide: GameplaySkinLaneSide.Secondary,
                secondSide: GameplaySkinLaneSide.Primary,
                reverseGroupVisualOrder: true,
                reverseFirstLaneVisualOrder: true,
                reverseSecondLaneVisualOrder: true);

            Assert.Multiple(() =>
            {
                Assert.That(previous.GroupsInLogicalOrder[0].Identity, Is.Not.EqualTo(current.GroupsInLogicalOrder[0].Identity));
                Assert.That(previous.GroupsInVisualOrder.Select(group => group.Identity.Id),
                    Is.Not.EqualTo(current.GroupsInVisualOrder.Select(group => group.Identity.Id)));
                Assert.That(previous.LanesInVisualOrder.Select(lane => lane.Identity.Id),
                    Is.Not.EqualTo(current.LanesInVisualOrder.Select(lane => lane.Identity.Id)));
                Assert.That(() => GameplaySkinLaneTopologyTransitionValidator.Validate(previous, current), Throws.Nothing);
            });
        }

        [Test]
        public void TestRejectsChangedGroupCount()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(includeSecondGroup: false);

            assertRejected(previous, current, "same number of lane groups");
        }

        [Test]
        public void TestRejectsChangedGroupIdSet()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(firstGroupId: "test.replacement-group");

            assertRejected(previous, current, "missing lane group 'test.group-1'");
        }

        [Test]
        public void TestRejectsChangedLogicalGroupOrder()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(reverseGroupLogicalOrder: true);

            assertRejected(previous, current, "changed the logical index of lane group 'test.group-1'");
        }

        [Test]
        public void TestRejectsChangedLaneCount()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(firstLogicalLaneIds: new[] { "test.lane-1" });

            assertRejected(previous, current, "same number of lanes");
        }

        [Test]
        public void TestRejectsChangedLaneIdSet()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(firstLogicalLaneIds: new[] { "test.replacement-lane", "test.lane-2" });

            assertRejected(previous, current, "missing lane 'test.lane-1'");
        }

        [Test]
        public void TestRejectsChangedLaneGroupMembership()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(
                firstLogicalLaneIds: new[] { "test.lane-1", "test.lane-3" },
                secondLogicalLaneIds: new[] { "test.lane-2", "test.lane-4" });

            assertRejected(previous, current, "changed the lane group of lane 'test.lane-2'");
        }

        [Test]
        public void TestRejectsChangedLaneRole()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(firstLaneRole: GameplaySkinLaneRole.SpecialKey);

            assertRejected(previous, current, "changed the role of lane 'test.lane-1'");
        }

        [Test]
        public void TestRejectsChangedLogicalLaneOrder()
        {
            GameplaySkinLaneTopologySnapshot previous = createTopology();
            GameplaySkinLaneTopologySnapshot current = createTopology(firstLogicalLaneIds: new[] { "test.lane-2", "test.lane-1" });

            assertRejected(previous, current, "changed the global or group-local logical index of lane 'test.lane-1'");
        }

        [Test]
        public void TestSurfaceRemainsPureProcessLocalTopologyOnly()
        {
            Type type = typeof(GameplaySkinLaneTopologyTransitionValidator);

            Assert.Multiple(() =>
            {
                Assert.That(type.IsPublic, Is.True);
                Assert.That(type.IsAbstract && type.IsSealed, Is.True);
                Assert.That(type.GetFields(), Is.Empty);
                Assert.That(type.GetProperties(), Is.Empty);
                Assert.That(type.GetMethods().Where(method => method.DeclaringType == type).Select(method => method.Name),
                    Is.EquivalentTo(new[] { nameof(GameplaySkinLaneTopologyTransitionValidator.Validate) }));
            });
        }

        private static void assertRejected(
            GameplaySkinLaneTopologySnapshot previous,
            GameplaySkinLaneTopologySnapshot current,
            string expectedMessage)
        {
            Assert.That(() => GameplaySkinLaneTopologyTransitionValidator.Validate(previous, current),
                Throws.TypeOf<ArgumentException>()
                      .With.Property(nameof(ArgumentException.ParamName)).EqualTo("current")
                      .And.Message.Contains(expectedMessage));
        }

        private static GameplaySkinLaneTopologySnapshot createTopology(
            string firstGroupId = "test.group-1",
            string secondGroupId = "test.group-2",
            GameplaySkinLaneSide firstSide = GameplaySkinLaneSide.Primary,
            GameplaySkinLaneSide secondSide = GameplaySkinLaneSide.Secondary,
            bool includeSecondGroup = true,
            bool reverseGroupLogicalOrder = false,
            bool reverseGroupVisualOrder = false,
            bool reverseFirstLaneVisualOrder = false,
            bool reverseSecondLaneVisualOrder = false,
            string[]? firstLogicalLaneIds = null,
            string[]? secondLogicalLaneIds = null,
            GameplaySkinLaneRole firstLaneRole = GameplaySkinLaneRole.Scratch)
        {
            firstLogicalLaneIds ??= new[] { "test.lane-1", "test.lane-2" };
            secondLogicalLaneIds ??= new[] { "test.lane-3", "test.lane-4" };

            var first = new GroupDefinition(firstGroupId, firstSide, firstLogicalLaneIds, reverseFirstLaneVisualOrder);
            var second = new GroupDefinition(secondGroupId, secondSide, secondLogicalLaneIds, reverseSecondLaneVisualOrder);
            GroupDefinition[] groups = includeSecondGroup ? new[] { first, second } : new[] { first };
            GroupDefinition[] logicalGroups = reverseGroupLogicalOrder ? groups.Reverse().ToArray() : groups;
            GroupDefinition[] visualGroups = reverseGroupVisualOrder ? groups.Reverse().ToArray() : groups;
            string[] globalLogicalLaneIds = logicalGroups.SelectMany(group => group.LogicalLaneIds).ToArray();
            string[] globalVisualLaneIds = visualGroups.SelectMany(group => group.VisualLaneIds).ToArray();
            var topologyGroups = new List<GameplaySkinLaneTopologyGroup>();

            foreach (GroupDefinition group in groups)
            {
                GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                    GameplaySkinLaneGroupId.Create(group.Id), group.Side);
                var lanes = new List<GameplaySkinLaneTopologyEntry>();

                foreach (string laneId in group.LogicalLaneIds)
                {
                    GameplaySkinLaneRole role = laneId == "test.lane-1" ? firstLaneRole : GameplaySkinLaneRole.Key;
                    GameplaySkinLaneIdentity laneIdentity = GameplaySkinLaneIdentity.Create(
                        GameplaySkinLaneId.Create(laneId), groupIdentity, role);
                    lanes.Add(GameplaySkinLaneTopologyEntry.Create(
                        laneIdentity,
                        Array.IndexOf(globalLogicalLaneIds, laneId),
                        Array.IndexOf(group.LogicalLaneIds, laneId),
                        Array.IndexOf(globalVisualLaneIds, laneId),
                        Array.IndexOf(group.VisualLaneIds, laneId)));
                }

                topologyGroups.Add(GameplaySkinLaneTopologyGroup.Create(
                    groupIdentity,
                    Array.IndexOf(logicalGroups, group),
                    Array.IndexOf(visualGroups, group),
                    lanes));
            }

            return GameplaySkinLaneTopologySnapshot.Create(topologyGroups);
        }

        private sealed class GroupDefinition
        {
            public string Id { get; }

            public GameplaySkinLaneSide Side { get; }

            public string[] LogicalLaneIds { get; }

            public string[] VisualLaneIds { get; }

            public GroupDefinition(string id, GameplaySkinLaneSide side, string[] logicalLaneIds, bool reverseVisualOrder)
            {
                Id = id;
                Side = side;
                LogicalLaneIds = logicalLaneIds;
                VisualLaneIds = reverseVisualOrder ? logicalLaneIds.Reverse().ToArray() : logicalLaneIds;
            }
        }
    }
}
