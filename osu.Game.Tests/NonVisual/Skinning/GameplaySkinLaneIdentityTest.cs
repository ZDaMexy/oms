// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLaneIdentityTest
    {
        [TestCase("lane")]
        [TestCase("test.lane-1")]
        [TestCase("group.primary-2")]
        public void TestStableIdsAcceptCanonicalValues(string value)
        {
            GameplaySkinLaneId laneId = GameplaySkinLaneId.Create(value);
            GameplaySkinLaneGroupId groupId = GameplaySkinLaneGroupId.Create(value);

            Assert.Multiple(() =>
            {
                Assert.That(laneId.Value, Is.EqualTo(value));
                Assert.That(laneId.ToString(), Is.EqualTo(value));
                Assert.That(groupId.Value, Is.EqualTo(value));
                Assert.That(groupId.ToString(), Is.EqualTo(value));
            });
        }

        [Test]
        public void TestStableIdsRejectNull()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneId.Create(null!), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneGroupId.Create(null!), Throws.ArgumentNullException);
            });
        }

        [TestCase("")]
        [TestCase("Lane")]
        [TestCase("1lane")]
        [TestCase("lane.")]
        [TestCase(".lane")]
        [TestCase("lane..key")]
        [TestCase("lane-")]
        [TestCase("lane key")]
        [TestCase("lane_key")]
        [TestCase("lane/key")]
        [TestCase("lane\\key")]
        public void TestStableIdsRejectMalformedValues(string value)
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneId.Create(value), Throws.ArgumentException);
                Assert.That(() => GameplaySkinLaneGroupId.Create(value), Throws.ArgumentException);
            });
        }

        [Test]
        public void TestStableIdsUseStrongOrdinalValueSemantics()
        {
            GameplaySkinLaneId firstLane = GameplaySkinLaneId.Create("test.lane-1");
            GameplaySkinLaneId secondLane = GameplaySkinLaneId.Create("test.lane-1");
            GameplaySkinLaneId otherLane = GameplaySkinLaneId.Create("test.lane-2");
            GameplaySkinLaneGroupId firstGroup = GameplaySkinLaneGroupId.Create("test.lane-1");
            GameplaySkinLaneGroupId secondGroup = GameplaySkinLaneGroupId.Create("test.lane-1");

            Assert.Multiple(() =>
            {
                Assert.That(firstLane, Is.EqualTo(secondLane));
                Assert.That(firstLane == secondLane, Is.True);
                Assert.That(firstLane != otherLane, Is.True);
                Assert.That(firstLane.GetHashCode(), Is.EqualTo(secondLane.GetHashCode()));
                Assert.That(new HashSet<GameplaySkinLaneId> { firstLane }.Contains(secondLane), Is.True);

                Assert.That(firstGroup, Is.EqualTo(secondGroup));
                Assert.That(firstGroup == secondGroup, Is.True);
                Assert.That(new HashSet<GameplaySkinLaneGroupId> { firstGroup }.Contains(secondGroup), Is.True);
                Assert.That(EqualityComparer<object>.Default.Equals(firstLane, firstGroup), Is.False);
            });
        }

        [TestCase(GameplaySkinLaneRole.Key)]
        [TestCase(GameplaySkinLaneRole.SpecialKey)]
        public void TestCreatesNeutralKeyIdentity(GameplaySkinLaneRole role)
        {
            GameplaySkinLaneGroupIdentity group = createGroup("test.group", GameplaySkinLaneSide.Neutral);
            GameplaySkinLaneIdentity lane = createLane("test.key-1", group, role);

            Assert.Multiple(() =>
            {
                Assert.That(lane.Id.Value, Is.EqualTo("test.key-1"));
                Assert.That(lane.Group, Is.SameAs(group));
                Assert.That(lane.Role, Is.EqualTo(role));
                Assert.That(lane.Side, Is.EqualTo(GameplaySkinLaneSide.Neutral));
                Assert.That(lane.ToString(), Is.EqualTo("test.key-1"));
                Assert.That(group.ToString(), Is.EqualTo("test.group"));
            });
        }

        [TestCase(GameplaySkinLaneSide.Primary)]
        [TestCase(GameplaySkinLaneSide.Secondary)]
        public void TestCreatesScratchIdentityForLogicalSide(GameplaySkinLaneSide side)
        {
            GameplaySkinLaneGroupIdentity group = createGroup("test.group", side);
            GameplaySkinLaneIdentity lane = createLane("test.scratch", group, GameplaySkinLaneRole.Scratch);

            Assert.Multiple(() =>
            {
                Assert.That(group.Side, Is.EqualTo(side));
                Assert.That(lane.Side, Is.EqualTo(side));
                Assert.That(lane.Role, Is.EqualTo(GameplaySkinLaneRole.Scratch));
            });
        }

        [Test]
        public void TestGroupRejectsNullIdOrInvalidSide()
        {
            GameplaySkinLaneGroupId groupId = GameplaySkinLaneGroupId.Create("test.group");

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneGroupIdentity.Create(null!, GameplaySkinLaneSide.Primary), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneGroupIdentity.Create(groupId, GameplaySkinLaneSide.Unspecified), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLaneGroupIdentity.Create(groupId, (GameplaySkinLaneSide)99), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestLaneRejectsNullIdentityPartsOrInvalidRole()
        {
            GameplaySkinLaneId laneId = GameplaySkinLaneId.Create("test.lane");
            GameplaySkinLaneGroupIdentity group = createGroup("test.group", GameplaySkinLaneSide.Primary);

            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLaneIdentity.Create(null!, group, GameplaySkinLaneRole.Key), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneIdentity.Create(laneId, null!, GameplaySkinLaneRole.Key), Throws.ArgumentNullException);
                Assert.That(() => GameplaySkinLaneIdentity.Create(laneId, group, GameplaySkinLaneRole.Unspecified), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLaneIdentity.Create(laneId, group, (GameplaySkinLaneRole)99), Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestIdentityEqualityIncludesNeutralMetadataButNotObjectReference()
        {
            GameplaySkinLaneGroupIdentity primaryGroup = createGroup("test.group", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity equivalentPrimaryGroup = createGroup("test.group", GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondaryGroup = createGroup("test.group", GameplaySkinLaneSide.Secondary);
            GameplaySkinLaneIdentity first = createLane("test.lane", primaryGroup, GameplaySkinLaneRole.Key);
            GameplaySkinLaneIdentity equivalent = createLane("test.lane", equivalentPrimaryGroup, GameplaySkinLaneRole.Key);
            GameplaySkinLaneIdentity otherRole = createLane("test.lane", equivalentPrimaryGroup, GameplaySkinLaneRole.Scratch);
            GameplaySkinLaneIdentity otherSide = createLane("test.lane", secondaryGroup, GameplaySkinLaneRole.Key);

            Assert.Multiple(() =>
            {
                Assert.That(primaryGroup, Is.EqualTo(equivalentPrimaryGroup));
                Assert.That(primaryGroup, Is.Not.EqualTo(secondaryGroup));
                Assert.That(first, Is.EqualTo(equivalent));
                Assert.That(first == equivalent, Is.True);
                Assert.That(first, Is.Not.EqualTo(otherRole));
                Assert.That(first, Is.Not.EqualTo(otherSide));
                Assert.That(new HashSet<GameplaySkinLaneIdentity> { first }.Contains(equivalent), Is.True);
            });
        }

        [Test]
        public void TestStableIdDoesNotEncodePresentationSideOrImplyRole()
        {
            GameplaySkinLaneId id = GameplaySkinLaneId.Create("test.opaque-lane");
            GameplaySkinLaneIdentity primaryKey = GameplaySkinLaneIdentity.Create(
                id, createGroup("test.group", GameplaySkinLaneSide.Primary), GameplaySkinLaneRole.Key);
            GameplaySkinLaneIdentity secondaryKey = GameplaySkinLaneIdentity.Create(
                id, createGroup("test.group", GameplaySkinLaneSide.Secondary), GameplaySkinLaneRole.Key);
            GameplaySkinLaneIdentity opaqueNamedKey = createLane(
                "test.scratch", createGroup("test.other-group", GameplaySkinLaneSide.Neutral), GameplaySkinLaneRole.Key);

            Assert.Multiple(() =>
            {
                Assert.That(primaryKey.Id, Is.SameAs(secondaryKey.Id));
                Assert.That(primaryKey.Group.Id, Is.EqualTo(secondaryKey.Group.Id));
                Assert.That(primaryKey.Role, Is.EqualTo(secondaryKey.Role));
                Assert.That(primaryKey.ToString(), Is.EqualTo(secondaryKey.ToString()));
                Assert.That(primaryKey, Is.Not.EqualTo(secondaryKey));
                Assert.That(opaqueNamedKey.Role, Is.EqualTo(GameplaySkinLaneRole.Key));
            });
        }

        [Test]
        public void TestIdentitySurfaceExcludesLayoutAndRulesetAuthority()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(GameplaySkinLaneGroupId).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneId).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneGroupIdentity).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneIdentity).IsSealed, Is.True);
                Assert.That(typeof(GameplaySkinLaneGroupId).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneId).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneGroupIdentity).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneIdentity).GetConstructors(), Is.Empty);
                Assert.That(typeof(GameplaySkinLaneGroupIdentity).GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(typeof(GameplaySkinLaneIdentity).GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(typeof(GameplaySkinLaneIdentity).GetProperties().Select(property => property.Name).Intersect(new[]
                {
                    "LogicalIndex",
                    "VisualIndex",
                    "GroupLogicalIndex",
                    "GroupVisualIndex",
                    "Keymode",
                    "Action",
                    "SourceChannel",
                    "Bounds",
                    "Rect",
                }), Is.Empty);
            });
        }

        [Test]
        public void TestResolverDiagnosticDoesNotSerialiseIdentityContext()
        {
            GameplaySkinLaneIdentity lane = createLane(
                "test.private-lane", createGroup("test.private-group", GameplaySkinLaneSide.Primary), GameplaySkinLaneRole.Key);
            var failure = new InvalidOperationException("private identity failure");
            var broken = new TestProvider("selected", _ => throw failure);
            var fallback = new TestProvider("oms-simple", _ => SkinSlotResult<TestComponent>.Provide(new TestComponent()));

            GameplaySkinSlotResolution<TestComponent> resolution = GameplaySkinSlotResolver.Resolve(
                GameplaySkinSlotCatalog.Note, lane, new[] { broken, fallback });
            string serialised = JsonConvert.SerializeObject(resolution.Diagnostics.Single());

            Assert.Multiple(() =>
            {
                Assert.That(resolution.Result.Kind, Is.EqualTo(SkinSlotResultKind.Provide));
                Assert.That(resolution.Diagnostics.Single().SlotId, Is.EqualTo(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(serialised, Does.Contain(GameplaySkinSlotCatalog.Note.Id));
                Assert.That(serialised, Does.Not.Contain(lane.Id.Value));
                Assert.That(serialised, Does.Not.Contain(lane.Group.Id.Value));
                Assert.That(serialised, Does.Not.Contain(failure.Message));
            });
        }

        private static GameplaySkinLaneGroupIdentity createGroup(string id, GameplaySkinLaneSide side)
            => GameplaySkinLaneGroupIdentity.Create(GameplaySkinLaneGroupId.Create(id), side);

        private static GameplaySkinLaneIdentity createLane(string id, GameplaySkinLaneGroupIdentity group, GameplaySkinLaneRole role)
            => GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create(id), group, role);

        private sealed class TestComponent
        {
        }

        private sealed class TestProvider : IGameplaySkinSlotProvider<GameplaySkinSlotLookup<GameplaySkinLaneIdentity>, TestComponent>
        {
            private readonly Func<GameplaySkinSlotLookup<GameplaySkinLaneIdentity>, SkinSlotResult<TestComponent>> getSlot;

            public string Name { get; }

            public TestProvider(string name, Func<GameplaySkinSlotLookup<GameplaySkinLaneIdentity>, SkinSlotResult<TestComponent>> getSlot)
            {
                Name = name;
                this.getSlot = getSlot;
            }

            public SkinSlotResult<TestComponent> GetSlot(GameplaySkinSlotLookup<GameplaySkinLaneIdentity> slot) => getSlot(slot);
        }
    }
}
