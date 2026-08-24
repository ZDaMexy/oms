// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Textures;
using osu.Game.Audio;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinStagedRevisionPublicationTest
    {
        private static readonly TimeSpan test_timeout = TimeSpan.FromSeconds(10);

        [Test]
        public async Task TestNoPreparedConsumerCanSeeNextRevisionBeforeAllReadyAndCommit()
        {
            using var harness = new PublicationHarness();
            var secondPrepareEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowSecondPrepare = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string firstVisible = "revision-a";
            string secondVisible = "revision-a";
            int firstCommits = 0;
            int secondCommits = 0;

            using SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "first staged visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(() =>
                    {
                        firstVisible = revision.ContentRevision;
                        firstCommits++;
                    },
                    () => firstVisible = "revision-a")));
            using SkinRevisionParticipantRegistration second = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "second staged visual",
                prepareCommit: async (revision, cancellationToken) =>
                {
                    secondPrepareEntered.TrySetResult();
                    await allowSecondPrepare.Task.WaitAsync(cancellationToken);
                    return new SkinRevisionParticipantCommit(() =>
                    {
                        secondVisible = revision.ContentRevision;
                        secondCommits++;
                    },
                    () => secondVisible = "revision-a");
                });

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            Task<SkinRevisionParticipantPrepareResult> preparation =
                harness.Publication.PrepareParticipantsForRevisionAsync(harness.CaptureSnapshot(), next, CancellationToken.None);

            await secondPrepareEntered.Task.WaitAsync(test_timeout);
            Assert.Multiple(() =>
            {
                Assert.That(preparation.IsCompleted, Is.False);
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
                Assert.That(firstVisible, Is.EqualTo("revision-a"));
                Assert.That(secondVisible, Is.EqualTo("revision-a"));
                Assert.That(firstCommits, Is.Zero);
                Assert.That(secondCommits, Is.Zero);
            });

            allowSecondPrepare.TrySetResult();
            SkinRevisionParticipantPrepareResult prepared = await preparation.WaitAsync(test_timeout);
            Assert.That(prepared.IsSuccess, Is.True);
            using SkinRevisionPreparedBarrier barrier = prepared.Barrier!;

            Assert.Multiple(() =>
            {
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
                Assert.That(firstVisible, Is.EqualTo("revision-a"));
                Assert.That(secondVisible, Is.EqualTo("revision-a"));
            });

            Assert.That(
                harness.Publication.TryCommit(barrier, out SkinCurrentRevision previous, out SkinRevisionBarrierRejectionReason rejectionReason),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
                Assert.That(previous, Is.SameAs(initial));
                Assert.That(harness.Publication.Current, Is.SameAs(next));
                Assert.That(firstVisible, Is.EqualTo("revision-b"));
                Assert.That(secondVisible, Is.EqualTo("revision-b"));
                Assert.That(first.CurrentRevision, Is.SameAs(next));
                Assert.That(second.CurrentRevision, Is.SameAs(next));
                Assert.That(firstCommits, Is.EqualTo(1));
                Assert.That(secondCommits, Is.EqualTo(1));
            });

            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            first.Dispose();
            second.Dispose();
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
        }

        [Test]
        public async Task TestRejectedPrepareAbortsPreparedReceiptsAndKeepsExactOldRevision()
        {
            using var harness = new PublicationHarness();
            string visible = "revision-a";
            int commits = 0;
            int aborts = 0;
            using SkinRevisionParticipantRegistration preparedParticipant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "prepared visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () =>
                        {
                            visible = revision.ContentRevision;
                            commits++;
                        },
                        () => visible = "revision-a",
                        () => aborts++)));
            using SkinRevisionParticipantRegistration rejectingParticipant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "rejecting visual",
                prepareCommit: (_, _) => Task.FromResult<SkinRevisionParticipantCommit?>(null));

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            SkinRevisionParticipantPrepareResult result = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.False);
                Assert.That(result.RejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantRejected));
                Assert.That(result.Barrier, Is.Null);
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
                Assert.That(preparedParticipant.CurrentRevision, Is.SameAs(initial));
                Assert.That(rejectingParticipant.CurrentRevision, Is.SameAs(initial));
                Assert.That(visible, Is.EqualTo("revision-a"));
                Assert.That(commits, Is.Zero);
                Assert.That(aborts, Is.EqualTo(1));
            });

            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
            preparedParticipant.Dispose();
            rejectingParticipant.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestAttachDuringPrepareAbortsStaleBarrierAndFreshBarrierIncludesNewConsumer()
        {
            using var harness = new PublicationHarness();
            var prepareEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowPrepare = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            string firstVisible = "revision-a";
            string attachedVisible = "revision-a";
            int staleAborts = 0;
            int firstPrepareCount = 0;
            int attachedPrepareCount = 0;

            using SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "first visual",
                prepareCommit: async (revision, cancellationToken) =>
                {
                    firstPrepareCount++;

                    if (firstPrepareCount == 1)
                    {
                        prepareEntered.TrySetResult();
                        await allowPrepare.Task.WaitAsync(cancellationToken);
                    }

                    return new SkinRevisionParticipantCommit(
                        () => firstVisible = revision.ContentRevision,
                        () => firstVisible = "revision-a",
                        () => staleAborts++);
                });

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            Task<SkinRevisionParticipantPrepareResult> stalePreparation =
                harness.Publication.PrepareParticipantsForRevisionAsync(harness.CaptureSnapshot(), next, CancellationToken.None);

            await prepareEntered.Task.WaitAsync(test_timeout);
            using SkinRevisionParticipantRegistration attached = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "attached during prepare",
                prepareCommit: (revision, _) =>
                {
                    attachedPrepareCount++;
                    return Task.FromResult<SkinRevisionParticipantCommit?>(
                        new SkinRevisionParticipantCommit(
                            () => attachedVisible = revision.ContentRevision,
                            () => attachedVisible = "revision-a"));
                });
            allowPrepare.TrySetResult();

            SkinRevisionParticipantPrepareResult stale = await stalePreparation.WaitAsync(test_timeout);
            Assert.Multiple(() =>
            {
                Assert.That(stale.RejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantSetChanged));
                Assert.That(staleAborts, Is.EqualTo(1));
                Assert.That(firstVisible, Is.EqualTo("revision-a"));
                Assert.That(attachedVisible, Is.EqualTo("revision-a"));
                Assert.That(attachedPrepareCount, Is.Zero);
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
            });

            SkinRevisionParticipantPrepareResult fresh = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);
            Assert.That(fresh.IsSuccess, Is.True);
            using SkinRevisionPreparedBarrier barrier = fresh.Barrier!;
            Assert.That(harness.Publication.TryCommit(barrier, out SkinCurrentRevision previous, out _), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(firstPrepareCount, Is.EqualTo(2));
                Assert.That(attachedPrepareCount, Is.EqualTo(1));
                Assert.That(firstVisible, Is.EqualTo("revision-b"));
                Assert.That(attachedVisible, Is.EqualTo("revision-b"));
                Assert.That(first.CurrentRevision, Is.SameAs(next));
                Assert.That(attached.CurrentRevision, Is.SameAs(next));
            });

            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            first.Dispose();
            attached.Dispose();
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
        }

        [Test]
        public async Task TestDetachBeforeCommitAbortsReceiptAndLateAttachGetsCommittedRevision()
        {
            using var harness = new PublicationHarness();
            int aborts = 0;
            SkinRevisionParticipantRegistration detached = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "detaching visual",
                prepareCommit: (_, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () => Assert.Fail("A stale receipt must not commit."),
                        () => { },
                        () => aborts++)));
            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            SkinRevisionParticipantPrepareResult prepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);
            Assert.That(prepared.IsSuccess, Is.True);
            using (SkinRevisionPreparedBarrier staleBarrier = prepared.Barrier!)
            {
                detached.Dispose();
                Assert.That(
                    harness.Publication.TryCommit(staleBarrier, out SkinCurrentRevision rejectedPrevious, out SkinRevisionBarrierRejectionReason rejectionReason),
                    Is.False);
                Assert.Multiple(() =>
                {
                    Assert.That(rejectedPrevious, Is.SameAs(initial));
                    Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantSetChanged));
                    Assert.That(harness.Publication.Current, Is.SameAs(initial));
                });
            }

            Assert.That(aborts, Is.EqualTo(1));

            SkinRevisionParticipantPrepareResult fresh = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);
            using SkinRevisionPreparedBarrier freshBarrier = fresh.Barrier!;
            Assert.That(harness.Publication.TryCommit(freshBarrier, out SkinCurrentRevision previous, out _), Is.True);

            using SkinRevisionParticipantRegistration late = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "late visual",
                prepareCommit: (_, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(() => { }, () => { })));

            Assert.Multiple(() =>
            {
                Assert.That(harness.Publication.Current, Is.SameAs(next));
                Assert.That(late.CurrentRevision, Is.SameAs(next));
                Assert.That(initial.ParticipantLeaseCount, Is.Zero);
                Assert.That(next.ParticipantLeaseCount, Is.EqualTo(1));
            });

            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            late.Dispose();
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
        }

        [Test]
        public async Task TestCancellationRaisedInsideCommitCannotSplitConsumersOrRollBack()
        {
            using var harness = new PublicationHarness();
            using var cancellation = new CancellationTokenSource();
            string firstVisible = "revision-a";
            string secondVisible = "revision-a";
            using SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "cancelling visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(() =>
                    {
                        firstVisible = revision.ContentRevision;
                        cancellation.Cancel();
                    },
                    () => firstVisible = "revision-a")));
            using SkinRevisionParticipantRegistration second = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "following visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () => secondVisible = revision.ContentRevision,
                        () => secondVisible = "revision-a")));

            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            SkinRevisionParticipantPrepareResult prepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                cancellation.Token);
            using SkinRevisionPreparedBarrier barrier = prepared.Barrier!;

            Assert.That(harness.Publication.TryCommit(barrier, out SkinCurrentRevision previous, out _), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(cancellation.IsCancellationRequested, Is.True);
                Assert.That(harness.Publication.Current, Is.SameAs(next));
                Assert.That(firstVisible, Is.EqualTo("revision-b"));
                Assert.That(secondVisible, Is.EqualTo("revision-b"));
                Assert.That(first.CurrentRevision, Is.SameAs(next));
                Assert.That(second.CurrentRevision, Is.SameAs(next));
            });

            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            first.Dispose();
            second.Dispose();
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
        }

        [Test]
        public async Task TestLatestPreparedBarrierWinsAndStaleCompletionCannotOverwriteIt()
        {
            using var harness = new PublicationHarness();
            string visible = "revision-a";
            int staleAborts = 0;
            using SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "latest wins visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () => visible = revision.ContentRevision,
                        () => visible = "revision-a",
                        () =>
                        {
                            if (revision.ContentRevision == "revision-b")
                                staleAborts++;
                        })));

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinRevisionParticipantSnapshot snapshot = harness.CaptureSnapshot();
            SkinCurrentRevision stale = harness.CreateProvisional("revision-b");
            SkinCurrentRevision latest = harness.CreateProvisional("revision-c");
            SkinRevisionParticipantPrepareResult stalePrepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                snapshot,
                stale,
                CancellationToken.None);
            SkinRevisionParticipantPrepareResult latestPrepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                snapshot,
                latest,
                CancellationToken.None);

            using SkinRevisionPreparedBarrier staleBarrier = stalePrepared.Barrier!;
            using SkinRevisionPreparedBarrier latestBarrier = latestPrepared.Barrier!;
            Assert.That(harness.Publication.TryCommit(latestBarrier, out SkinCurrentRevision previous, out _), Is.True);
            Assert.That(
                harness.Publication.TryCommit(staleBarrier, out SkinCurrentRevision rejectedPrevious, out SkinRevisionBarrierRejectionReason rejectionReason),
                Is.False);

            Assert.Multiple(() =>
            {
                Assert.That(previous, Is.SameAs(initial));
                Assert.That(rejectedPrevious, Is.SameAs(latest));
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.CurrentRevisionChanged));
                Assert.That(harness.Publication.Current, Is.SameAs(latest));
                Assert.That(participant.CurrentRevision, Is.SameAs(latest));
                Assert.That(visible, Is.EqualTo("revision-c"));
            });

            staleBarrier.Dispose();
            Assert.That(staleAborts, Is.EqualTo(1));
            stale.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(stale);
            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            participant.Dispose();
            latest.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(latest);
        }

        [Test]
        public async Task TestThrowingCommitRollsBackEveryAppliedConsumerAndKeepsExactA()
        {
            using var harness = new PublicationHarness();
            string firstVisible = "revision-a";
            string secondVisible = "revision-a";
            int aborts = 0;
            using SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "reversible first visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () => firstVisible = revision.ContentRevision,
                        () => firstVisible = "revision-a",
                        () => aborts++)));
            using SkinRevisionParticipantRegistration throwing = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "throwing second visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () =>
                        {
                            secondVisible = revision.ContentRevision;
                            throw new InvalidOperationException("commit fault");
                        },
                        () => secondVisible = "revision-a",
                        () => aborts++)));

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            SkinRevisionParticipantPrepareResult prepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);
            using SkinRevisionPreparedBarrier barrier = prepared.Barrier!;

            Assert.That(
                harness.Publication.TryCommit(
                    barrier,
                    out SkinCurrentRevision previous,
                    out SkinRevisionBarrierRejectionReason rejectionReason),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(previous, Is.SameAs(initial));
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantRejected));
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
                Assert.That(first.CurrentRevision, Is.SameAs(initial));
                Assert.That(throwing.CurrentRevision, Is.SameAs(initial));
                Assert.That(firstVisible, Is.EqualTo("revision-a"));
                Assert.That(secondVisible, Is.EqualTo("revision-a"));
                Assert.That(next.ParticipantLeaseCount, Is.Zero);
                Assert.That(aborts, Is.EqualTo(2));
            });

            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
            first.Dispose();
            throwing.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestDetachRacingHeldCommitLinearisesAfterCommitWithoutSplit()
        {
            using var harness = new PublicationHarness();
            var commitEntered = new ManualResetEventSlim();
            var releaseCommit = new ManualResetEventSlim();
            string visible = "revision-a";
            SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "detach racing visual",
                prepareCommit: (revision, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () =>
                        {
                            commitEntered.Set();
                            Assert.That(releaseCommit.Wait(test_timeout), Is.True);
                            visible = revision.ContentRevision;
                        },
                        () => visible = "revision-a")));

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            SkinRevisionParticipantPrepareResult prepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);
            using SkinRevisionPreparedBarrier barrier = prepared.Barrier!;

            Task<(bool committed, SkinCurrentRevision previous, SkinRevisionBarrierRejectionReason rejection)> commitTask = Task.Run(() =>
            {
                bool committed = harness.Publication.TryCommit(
                    barrier,
                    out SkinCurrentRevision previous,
                    out SkinRevisionBarrierRejectionReason rejection);
                return (committed, previous, rejection);
            });

            Assert.That(commitEntered.Wait(test_timeout), Is.True);
            Task detachTask = Task.Run(participant.Dispose);
            Assert.That(detachTask.IsCompleted, Is.False, "detach must wait for the held publication barrier");
            releaseCommit.Set();

            (bool committed, SkinCurrentRevision previous, SkinRevisionBarrierRejectionReason rejection) result =
                await commitTask.WaitAsync(test_timeout);
            await detachTask.WaitAsync(test_timeout);
            Assert.Multiple(() =>
            {
                Assert.That(result.committed, Is.True);
                Assert.That(result.rejection, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
                Assert.That(result.previous, Is.SameAs(initial));
                Assert.That(harness.Publication.Current, Is.SameAs(next));
                Assert.That(visible, Is.EqualTo("revision-b"));
                Assert.That(initial.ParticipantLeaseCount, Is.Zero);
                Assert.That(next.ParticipantLeaseCount, Is.Zero);
            });

            result.previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(result.previous);
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
            commitEntered.Dispose();
            releaseCommit.Dispose();
        }

        [Test]
        public async Task TestShutdownClaimsPreparedParticipantAndRetiresBothOwnersExactlyOnce()
        {
            using var harness = new PublicationHarness();
            int aborts = 0;
            SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "shutdown visual",
                prepareCommit: (_, _) => Task.FromResult<SkinRevisionParticipantCommit?>(
                    new SkinRevisionParticipantCommit(
                        () => Assert.Fail("Shutdown must not commit."),
                        () => { },
                        () => aborts++)));
            SkinCurrentRevision initial = harness.Publication.Current;
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            SkinRevisionParticipantPrepareResult prepared = await harness.Publication.PrepareParticipantsForRevisionAsync(
                harness.CaptureSnapshot(),
                next,
                CancellationToken.None);
            using SkinRevisionPreparedBarrier barrier = prepared.Barrier!;

            var claimed = harness.Publication.ShutdownAndClaimParticipants();
            Assert.That(harness.Publication.TryCommit(barrier, out SkinCurrentRevision previous, out SkinRevisionBarrierRejectionReason rejectionReason), Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(claimed, Is.EqualTo(new[] { participant }));
                Assert.That(harness.Publication.ShutdownAndClaimParticipants(), Is.Empty);
                Assert.That(previous, Is.SameAs(initial));
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.Shutdown));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(1));
            });

            barrier.Dispose();
            Assert.That(aborts, Is.EqualTo(1));
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);

            initial.ReleaseManagerLease();
            Assert.That(initial.Retired.IsCompleted, Is.False);
            claimed[0].Dispose();
            claimed[0].Dispose();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        private sealed class PublicationHarness : IDisposable
        {
            private readonly ConcurrentDictionary<long, int> retirementClaims = new ConcurrentDictionary<long, int>();
            private readonly ConcurrentBag<TestSkin> owners = new ConcurrentBag<TestSkin>();

            public SkinCurrentRevisionPublication Publication { get; }

            public PublicationHarness()
            {
                TestSkin initial = createOwner(Guid.NewGuid(), "initial owner");
                Publication = new SkinCurrentRevisionPublication(
                    initial,
                    "revision-a",
                    SkinCurrentRevisionSourceKind.RealmPackage,
                    keepsReusableOwner: false,
                    queueRetirement);
            }

            public SkinCurrentRevision CreateProvisional(string contentRevision)
            {
                Guid recordId = Guid.NewGuid();
                return Publication.CreateProvisional(
                    recordId,
                    contentRevision,
                    SkinCurrentRevisionSourceKind.RealmPackage,
                    createOwner(recordId, contentRevision));
            }

            public SkinRevisionParticipantSnapshot CaptureSnapshot()
            {
                SkinRevisionParticipantSnapshot snapshot = Publication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason rejectionReason);
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
                return snapshot;
            }

            public async Task AssertRetiredExactlyOnce(SkinCurrentRevision revision)
            {
                await revision.Detached.WaitAsync(test_timeout);
                await revision.Retired.WaitAsync(test_timeout);
                Assert.Multiple(() =>
                {
                    Assert.That(revision.LeaseCount, Is.Zero);
                    Assert.That(retirementClaims.TryGetValue(revision.Generation, out int count) ? count : 0, Is.EqualTo(1));
                    Assert.That(((TestSkin)revision.Owner).DisposeCount, Is.EqualTo(1));
                });
            }

            public void Dispose()
            {
                foreach (TestSkin owner in owners)
                {
                    if (owner.DisposeCount == 0)
                        owner.Dispose();
                }
            }

            private TestSkin createOwner(Guid recordId, string name)
            {
                var owner = new TestSkin(recordId, name);
                owners.Add(owner);
                return owner;
            }

            private void queueRetirement(SkinCurrentRevision revision)
            {
                retirementClaims.AddOrUpdate(revision.Generation, 1, (_, count) => count + 1);
                revision.RetireOwner();
            }
        }

        private sealed class TestSkin : Skin
        {
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            public TestSkin(Guid recordId, string name)
                : base(new SkinInfo(name) { ID = recordId }, null)
            {
            }

            public override ISample? GetSample(ISampleInfo sampleInfo) => null;

            public override Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

            public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) => null;

            protected override void Dispose(bool isDisposing)
            {
                if (Interlocked.Exchange(ref disposeCount, 1) == 0)
                    base.Dispose(isDisposing);
            }
        }
    }
}
