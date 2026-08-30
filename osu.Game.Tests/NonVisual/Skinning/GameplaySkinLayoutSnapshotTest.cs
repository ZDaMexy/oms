// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class GameplaySkinLayoutSnapshotTest
    {
        [Test]
        public void TestSnapshotDefensivelyCopiesEveryCollectionAndUsesExactTopologyEntries()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutContext context = createContext(topology, package, 0);
            var group = new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], rect(0.2f, 0, 0.6f, 0.9f));
            var firstLane = new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], rect(0.2f, 0, 0.3f, 0.9f));
            var secondLane = new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[1], rect(0.5f, 0, 0.3f, 0.9f));
            var surface = new GameplaySkinLayoutSurface("playfield", group.Rect, 0, true, true);
            GameplaySkinLayoutGroup[] groups = { group };
            GameplaySkinLayoutLane[] lanes = { firstLane, secondLane };
            GameplaySkinLayoutSurface[] surfaces = { surface };
            GameplaySkinLayoutRect[] viewports = { rect(0.82f, 0.02f, 0.16f, 0.18f) };
            GameplaySkinLayoutDiagnostic[] diagnostics = { new GameplaySkinLayoutDiagnostic("layout.width.fallback") };

            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(
                context, groups, lanes, surfaces, viewports, diagnostics);

            groups[0] = null!;
            lanes[0] = null!;
            surfaces[0] = null!;
            viewports[0] = rect(0.01f, 0.01f, 0.05f, 0.05f);
            diagnostics[0] = null!;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Context, Is.SameAs(context));
                Assert.That(snapshot.GroupsInLogicalOrder.Single(), Is.SameAs(group));
                Assert.That(snapshot.LanesInLogicalOrder, Is.EqualTo(new[] { firstLane, secondLane }));
                Assert.That(snapshot.GetLane(firstLane.LaneId), Is.SameAs(firstLane));
                Assert.That(snapshot.GetGroup(group.GroupId), Is.SameAs(group));
                Assert.That(snapshot.GetSurface("playfield"), Is.SameAs(surface));
                Assert.That(snapshot.BgaViewports.Single(), Is.EqualTo(rect(0.82f, 0.02f, 0.16f, 0.18f)));
                Assert.That(snapshot.Diagnostics.Single().Code, Is.EqualTo("layout.width.fallback"));
            });
        }

        [Test]
        public void TestGeometryRejectsNonFiniteNonPositiveAndOutOfSafeBounds()
        {
            Assert.Multiple(() =>
            {
                Assert.That(() => GameplaySkinLayoutRect.Create(float.NaN, 0, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLayoutRect.Create(0, float.PositiveInfinity, 1, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLayoutRect.Create(0, 0, 0, 1), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(() => GameplaySkinLayoutRect.Create(0, 0, 1, -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            });

            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutContext context = createContext(topology, package, 0);
            var group = new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], rect(0.2f, 0, 0.6f, 0.9f));

            Assert.That(() => GameplaySkinLayoutSnapshot.Create(
                context,
                new[] { group },
                new[]
                {
                    new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], rect(0.2f, 0, 0.3f, 0.9f)),
                    new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[1], rect(0.7f, 0, 0.2f, 0.9f)),
                },
                new[] { new GameplaySkinLayoutSurface("playfield", group.Rect, 0, true, true) }),
                Throws.ArgumentException);

            Assert.That(() => GameplaySkinLayoutContext.Create(
                "test", "test.native", "test.two-key", "test.center", topology,
                rect(0, 0, 1, 1), rect(0, 0, 1, 1), 16f / 9f, 1,
                (GameplaySkinScrollDirection)999, package, 0, 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void TestRevisionOwnerIsLatestWinsAndFailureKeepsExactOldReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout first = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));
            GameplaySkinPreparedLayout latest = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.25f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(first), Is.False);
                Assert.That(owner.Current, Is.Null);
                Assert.That(owner.TryCommit(latest), Is.True);
                Assert.That(owner.Current, Is.SameAs(latest.Snapshot));
                Assert.That(owner.Current!.Context.LayoutRevision, Is.Zero);
            });

            GameplaySkinLayoutSnapshot previous = owner.Current!;
            Assert.That(() => owner.Prepare(_ => throw new InvalidOperationException("solve failed")), Throws.InvalidOperationException);
            Assert.That(owner.Current, Is.SameAs(previous));
        }

        [Test]
        public void TestNeutralSnapshotAndRulesetAdapterCommitAsOnePublicationReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            TestLayoutAdapter? adapter = null;
            GameplaySkinPreparedLayout prepared = owner.PreparePublication(revision =>
            {
                adapter = new TestLayoutAdapter(createSnapshot(topology, package, revision, 0.2f));
                return GameplaySkinLayoutPublication.Create(adapter);
            });

            Assert.That(owner.TryCommit(prepared), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(owner.CurrentPublication, Is.SameAs(prepared.Publication));
                Assert.That(owner.Current, Is.SameAs(prepared.Snapshot));
                Assert.That(owner.CurrentPublication!.Snapshot, Is.SameAs(prepared.Snapshot));
                Assert.That(owner.CurrentPublication.GetAdapter<TestLayoutAdapter>(), Is.SameAs(adapter));
                Assert.That(adapter!.Snapshot, Is.SameAs(owner.Current));
            });
        }

        [Test]
        public void TestRevisionOwnerRejectsDifferentPackageAndForgedRevision()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinPackageRevision otherPackage = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);

            Assert.Multiple(() =>
            {
                Assert.That(() => owner.Prepare(revision => createSnapshot(topology, otherPackage, revision, 0.2f)), Throws.ArgumentException);
                Assert.That(() => owner.Prepare(revision => createSnapshot(topology, package, revision + 1, 0.2f)), Throws.ArgumentException);
                Assert.That(owner.Current, Is.Null);
            });
        }

        [Test]
        public void TestPreparedCarrierIsBoundToIssuingOwner()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var issuingOwner = new GameplaySkinLayoutRevisionOwner(package);
            var foreignOwner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout prepared = issuingOwner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(foreignOwner.TryCommit(prepared), Is.False);
                Assert.That(foreignOwner.Current, Is.Null);
                Assert.That(issuingOwner.TryCommit(prepared), Is.True);
                Assert.That(issuingOwner.Current, Is.SameAs(prepared.Snapshot));
            });
        }

        [Test]
        public void TestAbortedPreparedLayoutCannotCommit()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);
            GameplaySkinPreparedLayout prepared = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));

            prepared.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.False);
                Assert.That(owner.Current, Is.Null);
            });
        }

        [Test]
        public void TestSuccessfulPublicationsUseConsecutiveLayoutRevisionsAndOneExactPackage()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var owner = new GameplaySkinLayoutRevisionOwner(package);

            GameplaySkinPreparedLayout first = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));
            Assert.That(owner.TryCommit(first), Is.True);
            GameplaySkinLayoutSnapshot publishedA = owner.Current!;

            GameplaySkinPreparedLayout second = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.25f));
            Assert.That(owner.TryCommit(second), Is.True);
            GameplaySkinLayoutSnapshot publishedB = owner.Current!;

            Assert.Multiple(() =>
            {
                Assert.That(publishedA.Context.LayoutRevision, Is.Zero);
                Assert.That(publishedB.Context.LayoutRevision, Is.EqualTo(1));
                Assert.That(publishedB, Is.Not.SameAs(publishedA));
                Assert.That(publishedA.Context.PackageRevision, Is.SameAs(package));
                Assert.That(publishedB.Context.PackageRevision, Is.SameAs(package));
            });
        }

        [Test]
        public void TestCommitDispatcherRejectionAndFaultPreserveExactPublishedReference()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            bool reject = false;
            bool fault = false;
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                if (fault)
                    throw new InvalidOperationException("scheduler fault");

                if (reject)
                    return false;

                commit();
                return true;
            });

            GameplaySkinPreparedLayout first = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));
            Assert.That(owner.TryCommit(first), Is.True);
            GameplaySkinLayoutSnapshot published = owner.Current!;

            reject = true;
            GameplaySkinPreparedLayout rejected = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.25f));
            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(rejected), Is.False);
                Assert.That(owner.TryCommit(rejected), Is.False);
                Assert.That(owner.Current, Is.SameAs(published));
            });

            reject = false;
            fault = true;
            GameplaySkinPreparedLayout faulted = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.3f));
            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(faulted), Is.False);
                Assert.That(owner.TryCommit(faulted), Is.False);
                Assert.That(owner.Current, Is.SameAs(published));
            });
        }

        [Test]
        public void TestAsynchronousDispatcherCannotPublishAfterCallerAbortsCarrier()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            Action? lateCommit = null;
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                lateCommit = commit;
                return true;
            });
            GameplaySkinPreparedLayout prepared = owner.Prepare(revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.False);
                Assert.That(owner.Current, Is.Null);
                Assert.That(lateCommit, Is.Not.Null);
            });

            lateCommit!();

            Assert.That(owner.Current, Is.Null, "A dispatcher which returns before its callback must never publish later.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestDispatcherRejectingOrThrowingAfterCallbackClaimJoinsTerminalCommit(bool throwAfterClaim)
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            using var callbackEnteredCommit = new ManualResetEventSlim();
            using var releaseCommit = new ManualResetEventSlim();
            bool blockCommit = false;
            var owner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () =>
                {
                    if (blockCommit)
                    {
                        callbackEnteredCommit.Set();

                        while (!releaseCommit.Wait(TimeSpan.FromMilliseconds(100)))
                        {
                        }
                    }

                    return true;
                },
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0,
                commitAtParticipantGeneration: commitCompatibility,
                dispatchCommit: commit =>
                {
                    var callbackThread = new Thread(() => commit()) { IsBackground = true };
                    callbackThread.Start();
                    Assert.That(callbackEnteredCommit.Wait(TimeSpan.FromSeconds(10)), Is.True);

                    if (throwAfterClaim)
                        throw new InvalidOperationException("dispatcher fault after callback claim");

                    return false;
                });
            GameplaySkinPreparedLayout prepared = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));
            blockCommit = true;

            Task<bool> commitTask = Task.Run(() => owner.TryCommit(prepared));
            Assert.That(callbackEnteredCommit.Wait(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(commitTask.IsCompleted, Is.False, "The caller must join a callback which already owns admission.");

            releaseCommit.Set();
            Assert.That(await commitTask.WaitAsync(TimeSpan.FromSeconds(10)), Is.True);
            Assert.That(owner.Current, Is.SameAs(prepared.Snapshot));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void TestSynchronousCallbackResultWinsDispatcherContradictionAndReentrancy(int mode)
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutRevisionOwner owner = createOwner(package, commit =>
            {
                commit();

                switch (mode)
                {
                    case 0:
                        return false;

                    case 1:
                        throw new InvalidOperationException("fault after synchronous commit");

                    default:
                        commit();
                        return true;
                }
            });
            GameplaySkinPreparedLayout prepared = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.True);
                Assert.That(owner.Current, Is.SameAs(prepared.Snapshot));
                Assert.That(owner.CurrentPublication, Is.SameAs(prepared.Publication));
            });
        }

        [Test]
        public void TestExactRootIsRevalidatedInsideAtomicParticipantAdmission()
        {
            GameplaySkinLaneTopologySnapshot topology = createTopology();
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            bool exactRoot = true;
            var owner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => exactRoot,
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0,
                commitAtParticipantGeneration: (_, commit) =>
                {
                    exactRoot = false;
                    commit();
                    return true;
                },
                dispatchCommit: commit =>
                {
                    commit();
                    return true;
                });
            GameplaySkinPreparedLayout prepared = owner.Prepare(
                revision => createSnapshot(topology, package, revision, 0.2f));

            Assert.Multiple(() =>
            {
                Assert.That(owner.TryCommit(prepared), Is.False);
                Assert.That(owner.Current, Is.Null);
                Assert.That(owner.CurrentPublication, Is.Null);
            });
        }

        private static GameplaySkinLayoutSnapshot createSnapshot(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinPackageRevision package,
            long revision,
            float left)
        {
            GameplaySkinLayoutContext context = createContext(topology, package, revision);
            var groupRect = rect(left, 0, 0.6f, 0.9f);
            var groups = new[] { new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], groupRect) };
            var lanes = new[]
            {
                new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], rect(left, 0, 0.3f, 0.9f)),
                new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[1], rect(left + 0.3f, 0, 0.3f, 0.9f)),
            };
            return GameplaySkinLayoutSnapshot.Create(
                context,
                groups,
                lanes,
                new[] { new GameplaySkinLayoutSurface("playfield", groupRect, 0, true, true) });
        }

        private static GameplaySkinLayoutRevisionOwner createOwner(
            GameplaySkinPackageRevision package,
            Func<Action, bool> dispatchCommit)
            => new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => true,
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: generation => generation == 0,
                commitAtParticipantGeneration: commitCompatibility,
                dispatchCommit);

        private static bool commitCompatibility(long generation, Action commit)
        {
            if (generation != 0)
                return false;

            commit();
            return true;
        }

        private static GameplaySkinLayoutContext createContext(
            GameplaySkinLaneTopologySnapshot topology,
            GameplaySkinPackageRevision package,
            long revision)
            => GameplaySkinLayoutContext.Create(
                "test",
                "test.native",
                "test.two-key",
                "test.center",
                topology,
                rect(0, 0, 1, 1),
                rect(0, 0, 1, 1),
                16f / 9f,
                1,
                GameplaySkinScrollDirection.Down,
                package,
                topologyRevision: 0,
                layoutRevision: revision);

        private static GameplaySkinLaneTopologySnapshot createTopology()
        {
            GameplaySkinLaneGroupIdentity group = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group"), GameplaySkinLaneSide.Neutral);
            return GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(group, 0, 0, new[]
                {
                    GameplaySkinLaneTopologyEntry.Create(
                        GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-1"), group, GameplaySkinLaneRole.Key),
                        0, 0, 0, 0),
                    GameplaySkinLaneTopologyEntry.Create(
                        GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-2"), group, GameplaySkinLaneRole.SpecialKey),
                        1, 1, 1, 1),
                }),
            });
        }

        private static GameplaySkinLayoutRect rect(float x, float y, float width, float height)
            => GameplaySkinLayoutRect.Create(x, y, width, height);

        private sealed class TestLayoutAdapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public TestLayoutAdapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }
    }
}
