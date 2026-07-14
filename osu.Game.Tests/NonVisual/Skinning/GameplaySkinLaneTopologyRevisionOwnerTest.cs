// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinLaneTopologyRevisionOwnerTest
    {
        [Test]
        public void TestInitialPublicationStartsAtZeroAndKeepsExactTopology()
        {
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals);
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneTopologyPublication publication = owner.Publish("test.native", topology);

            Assert.Multiple(() =>
            {
                Assert.That(publication.Revision, Is.Zero);
                Assert.That(publication.Topology, Is.SameAs(topology));
                Assert.That(owner.Current, Is.SameAs(publication));
            });
        }

        [Test]
        public void TestEquivalentRebuildAndPresentationChangeIncrementRevision()
        {
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals);
            GameplaySkinLaneTopologyPublication initial = owner.Publish("test.native", createTopology());
            GameplaySkinLaneTopologyPublication rebuilt = owner.Publish("test.native", createTopology());
            GameplaySkinLaneTopologySnapshot reorderedTopology = createTopology(
                side: GameplaySkinLaneSide.Secondary,
                reverseVisualOrder: true);
            GameplaySkinLaneTopologyPublication reordered = owner.Publish("test.native", reorderedTopology);

            Assert.Multiple(() =>
            {
                Assert.That(initial.Revision, Is.Zero);
                Assert.That(rebuilt.Revision, Is.EqualTo(1));
                Assert.That(reordered.Revision, Is.EqualTo(2));
                Assert.That(reordered.Topology, Is.SameAs(reorderedTopology));
                Assert.That(owner.Current, Is.SameAs(reordered));
            });
        }

        [Test]
        public void TestNativeContextChangeIsRejectedAtomically()
        {
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals);
            GameplaySkinLaneTopologyPublication previous = owner.Publish("test.native", createTopology());

            Assert.That(() => owner.Publish("test.other", createTopology()),
                Throws.TypeOf<ArgumentException>()
                      .With.Property(nameof(ArgumentException.ParamName)).EqualTo("nativeContext"));

            GameplaySkinLaneTopologyPublication accepted = owner.Publish("test.native", createTopology());

            Assert.Multiple(() =>
            {
                Assert.That(previous.Revision, Is.Zero);
                Assert.That(accepted.Revision, Is.EqualTo(1));
                Assert.That(owner.Current, Is.SameAs(accepted));
            });
        }

        [Test]
        public void TestNeutralTopologyChangeIsRejectedAtomically()
        {
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals);
            GameplaySkinLaneTopologyPublication previous = owner.Publish("test.native", createTopology());

            Assert.That(() => owner.Publish("test.native", createTopology(laneCount: 3)), Throws.ArgumentException);
            Assert.That(owner.Current, Is.SameAs(previous));

            GameplaySkinLaneTopologyPublication accepted = owner.Publish("test.native", createTopology());

            Assert.That(accepted.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TestNativeComparerExceptionIsRejectedAtomically()
        {
            bool throwOnComparison = true;
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>((previous, current) =>
            {
                if (throwOnComparison)
                    throw new TestException("comparison failed");

                return StringComparer.Ordinal.Equals(previous, current);
            });
            GameplaySkinLaneTopologyPublication previous = owner.Publish("test.native", createTopology());

            Assert.That(() => owner.Publish("test.native", createTopology()), Throws.TypeOf<TestException>());
            Assert.That(owner.Current, Is.SameAs(previous));

            throwOnComparison = false;
            GameplaySkinLaneTopologyPublication accepted = owner.Publish("test.native", createTopology());

            Assert.That(accepted.Revision, Is.EqualTo(1));
        }

        [Test]
        public void TestRevisionOverflowIsRejectedAtomically()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinLaneTopologyPublication maximum = GameplaySkinLaneTopologyPublication.Create(long.MaxValue, topology);
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals, "test.native", maximum);

            Assert.That(() => owner.Publish("test.native", createTopology()), Throws.TypeOf<OverflowException>());
            Assert.That(owner.Current, Is.SameAs(maximum));
        }

        [Test]
        public void TestRejectsInvalidInputsWithoutPublishing()
        {
            Assert.That(() => new GameplaySkinLaneTopologyRevisionOwner<string>(null!), Throws.ArgumentNullException);

            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals);

            Assert.Multiple(() =>
            {
                Assert.That(() => owner.Publish(null!, createTopology()), Throws.ArgumentNullException);
                Assert.That(() => owner.Publish("test.native", null!), Throws.ArgumentNullException);
                Assert.That(owner.Current, Is.Null);
                Assert.That(() => GameplaySkinLaneTopologyPublication.Create(-1, createTopology()), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLaneTopologyPublication.Create(0, null!), Throws.ArgumentNullException);
            });
        }

        [Test]
        public void TestPublicSurfaceIsClosedImmutableAndSafe()
        {
            Type publicationType = typeof(GameplaySkinLaneTopologyPublication);
            Type ownerType = typeof(GameplaySkinLaneTopologyRevisionOwner<>);
            GameplaySkinLaneTopologyPublication publication =
                new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals).Publish("private.native", createTopology());
            var owner = new GameplaySkinLaneTopologyRevisionOwner<string>(StringComparer.Ordinal.Equals);

            Assert.Multiple(() =>
            {
                Assert.That(publicationType.IsPublic && publicationType.IsSealed, Is.True);
                Assert.That(publicationType.GetConstructors(), Is.Empty);
                Assert.That(publicationType.GetProperties().Select(property => property.Name),
                    Is.EquivalentTo(new[] { nameof(GameplaySkinLaneTopologyPublication.Revision), nameof(GameplaySkinLaneTopologyPublication.Topology) }));
                Assert.That(publicationType.GetProperties().Select(property => property.SetMethod), Is.All.Null);
                Assert.That(ownerType.IsPublic && ownerType.IsSealed, Is.True);
                Assert.That(ownerType.GetProperties().Select(property => property.Name), Is.EquivalentTo(new[] { "Current" }));
                Assert.That(ownerType.GetProperties().Single().SetMethod?.IsPrivate, Is.True);
                Assert.That(publication.ToString(), Is.EqualTo(nameof(GameplaySkinLaneTopologyPublication)));
                Assert.That(owner.ToString(), Is.EqualTo("GameplaySkinLaneTopologyRevisionOwner"));
                Assert.That(publication.ToString(), Does.Not.Contain("private.native"));
                Assert.That(owner.ToString(), Does.Not.Contain("private.native"));
            });
        }

        private static GameplaySkinLaneTopologySnapshot createTopology(
            GameplaySkinLaneSide side = GameplaySkinLaneSide.Primary,
            bool reverseVisualOrder = false,
            int laneCount = 2)
        {
            GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group"), side);
            string[] laneIds = Enumerable.Range(1, laneCount).Select(index => $"test.lane-{index}").ToArray();
            string[] visualLaneIds = reverseVisualOrder ? laneIds.Reverse().ToArray() : laneIds;
            GameplaySkinLaneTopologyEntry[] lanes = laneIds.Select((laneId, logicalIndex) =>
            {
                GameplaySkinLaneIdentity identity = GameplaySkinLaneIdentity.Create(
                    GameplaySkinLaneId.Create(laneId),
                    groupIdentity,
                    logicalIndex == 0 ? GameplaySkinLaneRole.Scratch : GameplaySkinLaneRole.Key);

                return GameplaySkinLaneTopologyEntry.Create(
                    identity,
                    logicalIndex,
                    logicalIndex,
                    Array.IndexOf(visualLaneIds, laneId),
                    Array.IndexOf(visualLaneIds, laneId));
            }).ToArray();

            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, lanes),
            });
        }

        private sealed class TestException : Exception
        {
            public TestException(string message)
                : base(message)
            {
            }
        }
    }
}
