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
    public sealed class GameplaySkinLaneTopologySnapshotTest
    {
        [Test]
        public void TestCreatesCanonicalDefensiveSnapshotAndLookups()
        {
            GameplaySkinLaneGroupIdentity firstIdentity = createGroupIdentity("test.group-1", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondIdentity = createGroupIdentity("test.group-2", GameplaySkinLaneSide.Secondary);
            var firstLaneSource = new List<GameplaySkinLaneTopologyEntry>
            {
                createLane("test.lane-2", firstIdentity, GameplaySkinLaneRole.Key, 1, 1, 2, 0),
                createLane("test.lane-1", firstIdentity, GameplaySkinLaneRole.Scratch, 0, 0, 3, 1),
            };
            GameplaySkinLaneTopologyGroup firstGroup = GameplaySkinLaneTopologyGroup.Create(firstIdentity, 0, 1, firstLaneSource);
            GameplaySkinLaneTopologyGroup secondGroup = GameplaySkinLaneTopologyGroup.Create(secondIdentity, 1, 0, new[]
            {
                createLane("test.lane-4", secondIdentity, GameplaySkinLaneRole.Key, 3, 1, 1, 1),
                createLane("test.lane-3", secondIdentity, GameplaySkinLaneRole.Key, 2, 0, 0, 0),
            });
            var groupSource = new List<GameplaySkinLaneTopologyGroup> { secondGroup, firstGroup };

            GameplaySkinLaneTopologySnapshot snapshot = GameplaySkinLaneTopologySnapshot.Create(groupSource);
            firstLaneSource.Clear();
            groupSource.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GroupsInLogicalOrder.Select(group => group.Identity.Id.Value),
                    Is.EqualTo(new[] { "test.group-1", "test.group-2" }));
                Assert.That(snapshot.GroupsInVisualOrder.Select(group => group.Identity.Id.Value),
                    Is.EqualTo(new[] { "test.group-2", "test.group-1" }));
                Assert.That(snapshot.LanesInLogicalOrder.Select(lane => lane.Identity.Id.Value),
                    Is.EqualTo(new[] { "test.lane-1", "test.lane-2", "test.lane-3", "test.lane-4" }));
                Assert.That(snapshot.LanesInVisualOrder.Select(lane => lane.Identity.Id.Value),
                    Is.EqualTo(new[] { "test.lane-3", "test.lane-4", "test.lane-2", "test.lane-1" }));
                Assert.That(firstGroup.LanesInLogicalOrder, Has.Count.EqualTo(2));
                Assert.That(snapshot.TryGetGroup(firstIdentity.Id, out GameplaySkinLaneTopologyGroup? resolvedGroup), Is.True);
                Assert.That(resolvedGroup, Is.SameAs(firstGroup));
                Assert.That(snapshot.TryGetLane(GameplaySkinLaneId.Create("test.lane-4"), out GameplaySkinLaneTopologyEntry? resolvedLane), Is.True);
                Assert.That(resolvedLane, Is.SameAs(secondGroup.LanesInLogicalOrder[1]));
                Assert.That(snapshot.TryGetGroup(GameplaySkinLaneGroupId.Create("test.missing-group"), out _), Is.False);
                Assert.That(snapshot.TryGetLane(GameplaySkinLaneId.Create("test.missing-lane"), out _), Is.False);
            });
        }

        [Test]
        public void TestTopologyRejectsNullEmptyOrNullGroup()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(null!), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(Array.Empty<GameplaySkinLaneTopologyGroup>()), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new GameplaySkinLaneTopologyGroup[] { null! }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestGroupRejectsNullEmptyOrNullLane()
        {
            GameplaySkinLaneGroupIdentity groupIdentity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologyEntry.Create(null!, 0, 0, 0, 0), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneTopologyGroup.Create(null!, 0, 0, Array.Empty<GameplaySkinLaneTopologyEntry>()), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, null!), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, Array.Empty<GameplaySkinLaneTopologyEntry>()), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, new GameplaySkinLaneTopologyEntry[] { null! }), Throws.ArgumentException);
            });
        }

        [TestCase(-1, 0, 0, 0)]
        [TestCase(0, -1, 0, 0)]
        [TestCase(0, 0, -1, 0)]
        [TestCase(0, 0, 0, -1)]
        public void TestEntryRejectsNegativeIndex(int globalLogical, int groupLocalLogical, int globalVisual, int groupLocalVisual)
        {
            GameplaySkinLaneGroupIdentity groupIdentity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);
            GameplaySkinLaneIdentity laneIdentity = GameplaySkinLaneIdentity.Create(
                GameplaySkinLaneId.Create("test.lane"), groupIdentity, GameplaySkinLaneRole.Key);

            Assert.That(
                () => GameplaySkinLaneTopologyEntry.Create(laneIdentity, globalLogical, groupLocalLogical, globalVisual, groupLocalVisual),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(-1, 0)]
        [TestCase(0, -1)]
        public void TestGroupRejectsNegativeIndex(int logicalIndex, int visualIndex)
        {
            GameplaySkinLaneGroupIdentity groupIdentity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);
            GameplaySkinLaneTopologyEntry lane = createLane("test.lane", groupIdentity, GameplaySkinLaneRole.Key, 0, 0, 0, 0);

            Assert.That(
                () => GameplaySkinLaneTopologyGroup.Create(groupIdentity, logicalIndex, visualIndex, new[] { lane }),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TestGroupRejectsIdentityMismatch()
        {
            GameplaySkinLaneGroupIdentity expected = createGroupIdentity("test.group", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity changedSide = createGroupIdentity("test.group", GameplaySkinLaneSide.Secondary);
            GameplaySkinLaneGroupIdentity other = createGroupIdentity("test.other-group", GameplaySkinLaneSide.Primary);

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologyGroup.Create(expected, 0, 0, new[]
                {
                    createLane("test.lane", changedSide, GameplaySkinLaneRole.Key, 0, 0, 0, 0),
                }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologyGroup.Create(expected, 0, 0, new[]
                {
                    createLane("test.lane", other, GameplaySkinLaneRole.Key, 0, 0, 0, 0),
                }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestRejectsDuplicateLaneIdWithinOrAcrossGroups()
        {
            GameplaySkinLaneGroupIdentity firstIdentity = createGroupIdentity("test.group-1", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondIdentity = createGroupIdentity("test.group-2", GameplaySkinLaneSide.Secondary);

            Assert.That(() => GameplaySkinLaneTopologyGroup.Create(firstIdentity, 0, 0, new[]
            {
                createLane("test.lane", firstIdentity, GameplaySkinLaneRole.Key, 0, 0, 0, 0),
                createLane("test.lane", firstIdentity, GameplaySkinLaneRole.Scratch, 1, 1, 1, 1),
            }), Throws.ArgumentException);

            GameplaySkinLaneTopologyGroup firstGroup = createSingleLaneGroup(firstIdentity, "test.lane", 0, 0, 0, 0);
            GameplaySkinLaneTopologyGroup secondGroup = createSingleLaneGroup(secondIdentity, "test.lane", 1, 1, 1, 1);
            Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { firstGroup, secondGroup }), Throws.ArgumentException);
        }

        [Test]
        public void TestRejectsDuplicateGroupIdWithSameOrDifferentMetadata()
        {
            GameplaySkinLaneGroupIdentity primary = createGroupIdentity("test.group", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity equivalent = createGroupIdentity("test.group", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondary = createGroupIdentity("test.group", GameplaySkinLaneSide.Secondary);

            GameplaySkinLaneTopologyGroup firstGroup = createSingleLaneGroup(primary, "test.lane-1", 0, 0, 0, 0);
            GameplaySkinLaneTopologyGroup equivalentGroup = createSingleLaneGroup(equivalent, "test.lane-2", 1, 1, 1, 1);
            GameplaySkinLaneTopologyGroup changedGroup = createSingleLaneGroup(secondary, "test.lane-2", 1, 1, 1, 1);

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { firstGroup, equivalentGroup }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { firstGroup, changedGroup }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestRejectsInvalidGroupLocalIndexPermutations()
        {
            GameplaySkinLaneGroupIdentity identity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);

            Assert.Multiple(() =>
            {
                Assert.That(() => createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 1 }), Throws.ArgumentException);
                Assert.That(() => createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 1 }, new[] { 0, 1 }), Throws.ArgumentException);
                Assert.That(() => createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 0 }), Throws.ArgumentException);
                Assert.That(() => createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 2 }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestRejectsLocalOrderThatDisagreesWithGlobalOrder()
        {
            GameplaySkinLaneGroupIdentity identity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);

            Assert.Multiple(() =>
            {
                Assert.That(() => createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 0, 1 }), Throws.ArgumentException);
                Assert.That(() => createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 1, 0 }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestRejectsInvalidGlobalLaneIndexPermutations()
        {
            GameplaySkinLaneGroupIdentity identity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[]
                {
                    createGroupWithIndices(identity, new[] { 0, 0 }, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 }),
                }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[]
                {
                    createGroupWithIndices(identity, new[] { 0, 2 }, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 1 }),
                }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[]
                {
                    createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 0 }, new[] { 0, 1 }),
                }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[]
                {
                    createGroupWithIndices(identity, new[] { 0, 1 }, new[] { 0, 1 }, new[] { 0, 2 }, new[] { 0, 1 }),
                }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestRejectsInvalidGroupIndexPermutations()
        {
            GameplaySkinLaneGroupIdentity firstIdentity = createGroupIdentity("test.group-1", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondIdentity = createGroupIdentity("test.group-2", GameplaySkinLaneSide.Secondary);

            Assert.Multiple(() =>
            {
                Assert.That(() => createTwoGroupTopology(firstIdentity, secondIdentity, 0, 0, 0, 1), Throws.ArgumentException);
                Assert.That(() => createTwoGroupTopology(firstIdentity, secondIdentity, 0, 2, 0, 1), Throws.ArgumentException);
                Assert.That(() => createTwoGroupTopology(firstIdentity, secondIdentity, 0, 1, 0, 0), Throws.ArgumentException);
                Assert.That(() => createTwoGroupTopology(firstIdentity, secondIdentity, 0, 1, 0, 2), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestRejectsNonContiguousLogicalOrVisualGroupBlocks()
        {
            GameplaySkinLaneGroupIdentity firstIdentity = createGroupIdentity("test.group-1", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondIdentity = createGroupIdentity("test.group-2", GameplaySkinLaneSide.Secondary);
            GameplaySkinLaneTopologyGroup logicalFirst = createSingleLaneGroup(firstIdentity, "test.lane-1", 1, 0, 0, 0);
            GameplaySkinLaneTopologyGroup logicalSecond = createSingleLaneGroup(secondIdentity, "test.lane-2", 0, 1, 1, 1);
            GameplaySkinLaneTopologyGroup visualFirst = createSingleLaneGroup(firstIdentity, "test.lane-1", 0, 0, 1, 0);
            GameplaySkinLaneTopologyGroup visualSecond = createSingleLaneGroup(secondIdentity, "test.lane-2", 1, 1, 0, 1);
            GameplaySkinLaneTopologyGroup logicalInterleavedFirst = GameplaySkinLaneTopologyGroup.Create(firstIdentity, 0, 0, new[]
            {
                createLane("test.logical-lane-1", firstIdentity, GameplaySkinLaneRole.Key, 0, 0, 0, 0),
                createLane("test.logical-lane-3", firstIdentity, GameplaySkinLaneRole.Key, 2, 1, 1, 1),
            });
            GameplaySkinLaneTopologyGroup logicalInterleavedSecond = GameplaySkinLaneTopologyGroup.Create(secondIdentity, 1, 1, new[]
            {
                createLane("test.logical-lane-2", secondIdentity, GameplaySkinLaneRole.Key, 1, 0, 2, 0),
                createLane("test.logical-lane-4", secondIdentity, GameplaySkinLaneRole.Key, 3, 1, 3, 1),
            });
            GameplaySkinLaneTopologyGroup visualInterleavedFirst = GameplaySkinLaneTopologyGroup.Create(firstIdentity, 0, 0, new[]
            {
                createLane("test.visual-lane-1", firstIdentity, GameplaySkinLaneRole.Key, 0, 0, 0, 0),
                createLane("test.visual-lane-3", firstIdentity, GameplaySkinLaneRole.Key, 1, 1, 2, 1),
            });
            GameplaySkinLaneTopologyGroup visualInterleavedSecond = GameplaySkinLaneTopologyGroup.Create(secondIdentity, 1, 1, new[]
            {
                createLane("test.visual-lane-2", secondIdentity, GameplaySkinLaneRole.Key, 2, 0, 1, 0),
                createLane("test.visual-lane-4", secondIdentity, GameplaySkinLaneRole.Key, 3, 1, 3, 1),
            });

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { logicalFirst, logicalSecond }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { visualFirst, visualSecond }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { logicalInterleavedFirst, logicalInterleavedSecond }), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneTopologySnapshot.Create(new[] { visualInterleavedFirst, visualInterleavedSecond }), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestLookupRejectsNullId()
        {
            GameplaySkinLaneTopologySnapshot snapshot = createOneLaneTopology();

            Assert.Multiple(() =>
            {
                Assert.That(() => snapshot.TryGetGroup(null!, out _), Throws.ArgumentNullException);
                Assert.That(() => snapshot.TryGetLane(null!, out _), Throws.ArgumentNullException);
            });
        }

        [Test]
        public void TestPublicSurfaceExcludesLayoutAndRulesetAuthority()
        {
            Type[] types =
            {
                typeof(GameplaySkinLaneTopologyEntry),
                typeof(GameplaySkinLaneTopologyGroup),
                typeof(GameplaySkinLaneTopologySnapshot),
            };
            string[] forbiddenProperties =
            {
                "Keymode",
                "Style",
                "Action",
                "SourceChannel",
                "Bounds",
                "Rect",
                "Geometry",
                "Revision",
                "NativeContext",
            };
            string[] propertyTypeNames = types.SelectMany(type => type.GetProperties())
                                              .Select(property => property.PropertyType.FullName ?? string.Empty)
                                              .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(types.All(type => type.IsSealed), Is.True);
                Assert.That(types.SelectMany(type => type.GetConstructors()), Is.Empty);
                Assert.That(types.SelectMany(type => type.GetProperties()).Select(property => property.SetMethod), Is.All.Null);
                Assert.That(types.SelectMany(type => type.GetProperties()).Select(property => property.Name).Intersect(forbiddenProperties), Is.Empty);
                Assert.That(propertyTypeNames.Any(name => name.Contains("Rulesets.Bms", StringComparison.Ordinal)
                                                          || name.Contains("Rulesets.Mania", StringComparison.Ordinal)), Is.False);
            });
        }

        private static GameplaySkinLaneTopologySnapshot createOneLaneTopology()
        {
            GameplaySkinLaneGroupIdentity identity = createGroupIdentity("test.group", GameplaySkinLaneSide.Neutral);
            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                createSingleLaneGroup(identity, "test.lane", 0, 0, 0, 0),
            });
        }

        private static GameplaySkinLaneTopologySnapshot createTwoGroupTopology(
            GameplaySkinLaneGroupIdentity firstIdentity,
            GameplaySkinLaneGroupIdentity secondIdentity,
            int firstLogicalIndex,
            int secondLogicalIndex,
            int firstVisualIndex,
            int secondVisualIndex)
            => GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                createSingleLaneGroup(firstIdentity, "test.lane-1", 0, firstLogicalIndex, 0, firstVisualIndex),
                createSingleLaneGroup(secondIdentity, "test.lane-2", 1, secondLogicalIndex, 1, secondVisualIndex),
            });

        private static GameplaySkinLaneTopologyGroup createGroupWithIndices(
            GameplaySkinLaneGroupIdentity identity,
            IReadOnlyList<int> globalLogical,
            IReadOnlyList<int> groupLocalLogical,
            IReadOnlyList<int> globalVisual,
            IReadOnlyList<int> groupLocalVisual)
            => GameplaySkinLaneTopologyGroup.Create(identity, 0, 0, Enumerable.Range(0, globalLogical.Count).Select(index =>
                createLane(
                    $"test.lane-{index + 1}",
                    identity,
                    GameplaySkinLaneRole.Key,
                    globalLogical[index],
                    groupLocalLogical[index],
                    globalVisual[index],
                    groupLocalVisual[index])));

        private static GameplaySkinLaneTopologyGroup createSingleLaneGroup(
            GameplaySkinLaneGroupIdentity identity,
            string laneId,
            int globalLogical,
            int groupLogical,
            int globalVisual,
            int groupVisual)
            => GameplaySkinLaneTopologyGroup.Create(identity, groupLogical, groupVisual, new[]
            {
                createLane(laneId, identity, GameplaySkinLaneRole.Key, globalLogical, 0, globalVisual, 0),
            });

        private static GameplaySkinLaneTopologyEntry createLane(
            string id,
            GameplaySkinLaneGroupIdentity group,
            GameplaySkinLaneRole role,
            int globalLogical,
            int groupLocalLogical,
            int globalVisual,
            int groupLocalVisual)
            => GameplaySkinLaneTopologyEntry.Create(
                GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create(id), group, role),
                globalLogical,
                groupLocalLogical,
                globalVisual,
                groupLocalVisual);

        private static GameplaySkinLaneGroupIdentity createGroupIdentity(string id, GameplaySkinLaneSide side)
            => GameplaySkinLaneGroupIdentity.Create(GameplaySkinLaneGroupId.Create(id), side);
    }
}
