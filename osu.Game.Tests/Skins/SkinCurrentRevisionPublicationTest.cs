// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    public class SkinCurrentRevisionPublicationTest
    {
        private static readonly TimeSpan test_timeout = TimeSpan.FromSeconds(10);

        [Test]
        public async Task TestParticipantAttachAndDetachInvalidatesCapturedSnapshot()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "test visual");

            SkinRevisionParticipantSnapshot snapshot = harness.CaptureSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Participants, Is.EqualTo(new[] { participant }));
                Assert.That(participant.CurrentRevision, Is.SameAs(initial));
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
            });

            participant.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(participant.IsDisposed, Is.True);
                Assert.That(initial.LeaseCount, Is.EqualTo(1));
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
            });

            Assert.That(
                await harness.Publication.PrepareParticipantsAsync(snapshot, CancellationToken.None),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantSetChanged));

            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestParticipantFailureLeavesCurrentRevisionUnchanged()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "rejecting visual",
                _ => Task.FromResult(false));
            SkinRevisionParticipantSnapshot snapshot = harness.CaptureSnapshot();
            SkinCurrentRevision provisional = harness.CreateProvisional("revision-b");

            Assert.That(
                await harness.Publication.PrepareParticipantsAsync(snapshot, CancellationToken.None),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantRejected));

            Assert.Multiple(() =>
            {
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
                Assert.That(participant.CurrentRevision, Is.SameAs(initial));
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
                Assert.That(provisional.LeaseCount, Is.EqualTo(1));
            });

            provisional.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(provisional);

            participant.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestAttachDuringPrepareRequiresFreshSnapshotBeforeCommit()
        {
            using var harness = new PublicationHarness();
            var prepareEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowPrepare = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int firstPrepareCount = 0;
            int attachedPrepareCount = 0;

            using SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "first visual",
                async cancellationToken =>
                {
                    Interlocked.Increment(ref firstPrepareCount);
                    prepareEntered.TrySetResult();
                    await allowPrepare.Task.WaitAsync(cancellationToken);
                    return true;
                });

            SkinRevisionParticipantSnapshot staleSnapshot = harness.CaptureSnapshot();
            Task<SkinRevisionBarrierRejectionReason> stalePreparation =
                harness.Publication.PrepareParticipantsAsync(staleSnapshot, CancellationToken.None);

            await prepareEntered.Task.WaitAsync(test_timeout);

            using SkinRevisionParticipantRegistration attached = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "attached during prepare",
                _ =>
                {
                    Interlocked.Increment(ref attachedPrepareCount);
                    return Task.FromResult(true);
                });

            allowPrepare.TrySetResult();

            Assert.That(
                await stalePreparation.WaitAsync(test_timeout),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantSetChanged));

            SkinRevisionParticipantSnapshot freshSnapshot = harness.CaptureSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(freshSnapshot.Participants, Is.EqualTo(new[] { first, attached }));
                Assert.That(firstPrepareCount, Is.EqualTo(1));
                Assert.That(attachedPrepareCount, Is.Zero);
            });

            Assert.That(
                await harness.Publication.PrepareParticipantsAsync(freshSnapshot, CancellationToken.None),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
            Assert.Multiple(() =>
            {
                Assert.That(firstPrepareCount, Is.EqualTo(2));
                Assert.That(attachedPrepareCount, Is.EqualTo(1));
            });

            SkinCurrentRevision next = harness.CreateProvisional("revision-b");
            Assert.That(
                harness.Publication.TryCommit(freshSnapshot, next, out SkinCurrentRevision previous, out SkinRevisionBarrierRejectionReason rejectionReason),
                Is.True);
            Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
            Assert.That(previous, Is.SameAs(staleSnapshot.CurrentRevision));

            first.AdoptCurrentRevision();
            attached.AdoptCurrentRevision();

            using SkinRevisionParticipantRegistration late = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "late participant");

            Assert.Multiple(() =>
            {
                Assert.That(first.CurrentRevision, Is.SameAs(next));
                Assert.That(attached.CurrentRevision, Is.SameAs(next));
                Assert.That(late.CurrentRevision, Is.SameAs(next));
                Assert.That(previous.LeaseCount, Is.EqualTo(1));
                Assert.That(next.LeaseCount, Is.EqualTo(4));
            });

            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);

            first.Dispose();
            attached.Dispose();
            late.Dispose();
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
        }

        [Test]
        public async Task TestDetachBeforeCommitRejectsStaleSnapshotAndFreshSnapshotCanCommit()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            SkinRevisionParticipantRegistration detached = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "detached visual");
            SkinRevisionParticipantSnapshot staleSnapshot = harness.CaptureSnapshot();
            SkinCurrentRevision next = harness.CreateProvisional("revision-b");

            detached.Dispose();

            Assert.That(
                harness.Publication.TryCommit(staleSnapshot, next, out SkinCurrentRevision rejectedPrevious, out SkinRevisionBarrierRejectionReason rejectionReason),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.ParticipantSetChanged));
                Assert.That(rejectedPrevious, Is.SameAs(initial));
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
            });

            SkinRevisionParticipantSnapshot freshSnapshot = harness.CaptureSnapshot();
            Assert.That(
                await harness.Publication.PrepareParticipantsAsync(freshSnapshot, CancellationToken.None),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
            Assert.That(
                harness.Publication.TryCommit(freshSnapshot, next, out SkinCurrentRevision committedPrevious, out rejectionReason),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
                Assert.That(committedPrevious, Is.SameAs(initial));
                Assert.That(harness.Publication.Current, Is.SameAs(next));
            });

            committedPrevious.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(committedPrevious);
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);
        }

        [Test]
        public async Task TestCurrentChangeDuringPrepareRequiresLatestSnapshot()
        {
            using var harness = new PublicationHarness();
            var prepareEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowPrepare = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            int prepareCount = 0;

            using SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "visual",
                async cancellationToken =>
                {
                    Interlocked.Increment(ref prepareCount);
                    prepareEntered.TrySetResult();
                    await allowPrepare.Task.WaitAsync(cancellationToken);
                    return true;
                });

            SkinCurrentRevision initial = harness.Publication.Current;
            SkinRevisionParticipantSnapshot staleSnapshot = harness.CaptureSnapshot();
            Task<SkinRevisionBarrierRejectionReason> stalePreparation =
                harness.Publication.PrepareParticipantsAsync(staleSnapshot, CancellationToken.None);

            await prepareEntered.Task.WaitAsync(test_timeout);
            SkinCurrentRevision intermediate = harness.CreateProvisional("revision-b");
            Assert.That(harness.CommitRetaining(intermediate), Is.SameAs(initial));
            allowPrepare.TrySetResult();

            Assert.That(
                await stalePreparation.WaitAsync(test_timeout),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.CurrentRevisionChanged));

            SkinRevisionParticipantSnapshot latestSnapshot = harness.CaptureSnapshot();
            Assert.That(latestSnapshot.CurrentRevision, Is.SameAs(intermediate));
            Assert.That(
                await harness.Publication.PrepareParticipantsAsync(latestSnapshot, CancellationToken.None),
                Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
            Assert.That(prepareCount, Is.EqualTo(2));

            SkinCurrentRevision latest = harness.CreateProvisional("revision-c");
            Assert.That(
                harness.Publication.TryCommit(latestSnapshot, latest, out SkinCurrentRevision previous, out SkinRevisionBarrierRejectionReason rejectionReason),
                Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(previous, Is.SameAs(intermediate));
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
            });

            participant.AdoptCurrentRevision();
            Assert.That(participant.CurrentRevision, Is.SameAs(latest));

            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
            previous.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            participant.Dispose();
            latest.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(latest);
        }

        [Test]
        public async Task TestFinalLeaseDetachClaimsRetirementExactlyOnce()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision revision = harness.Publication.Current;
            SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "first visual");
            SkinRevisionParticipantRegistration last = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "last holder");
            SkinCurrentRevisionLease directLease = harness.Publication.AcquireCurrentLease();

            revision.ReleaseManagerLease();
            first.Dispose();
            first.Dispose();
            directLease.Dispose();
            directLease.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(revision.LeaseCount, Is.EqualTo(1));
                Assert.That(revision.ParticipantLeaseCount, Is.EqualTo(1));
                Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                Assert.That(revision.Detached.IsCompleted, Is.False);
                Assert.That(revision.Retired.IsCompleted, Is.False);
                Assert.That(harness.RetirementClaims(revision), Is.Zero);
                Assert.That(harness.InitialOwner.DisposeCount, Is.Zero);
            });

            last.Dispose();
            await harness.AssertRetiredExactlyOnce(revision);

            revision.ReleaseManagerLease();
            last.Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(revision.LeaseCount, Is.Zero);
                Assert.That(revision.ParticipantLeaseCount, Is.Zero);
                Assert.That(revision.ConsumersDetached.IsCompletedSuccessfully, Is.True);
                Assert.That(harness.RetirementClaims(revision), Is.EqualTo(1));
                Assert.That(harness.InitialOwner.DisposeCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TestShutdownClaimsParticipantsOnceAndRejectsPublication()
        {
            using var harness = new PublicationHarness();
            SkinRevisionParticipantRegistration first = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "first visual");
            SkinRevisionParticipantRegistration second = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "second holder");
            SkinRevisionParticipantSnapshot snapshot = harness.CaptureSnapshot();
            SkinCurrentRevision provisional = harness.CreateProvisional("revision-b");

            var claimed = harness.Publication.ShutdownAndClaimParticipants();

            Assert.Multiple(() =>
            {
                Assert.That(claimed, Is.EquivalentTo(new[] { first, second }));
                Assert.That(harness.Publication.ShutdownAndClaimParticipants(), Is.Empty);
                Assert.That(harness.Publication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason captureReason), Is.Null);
                Assert.That(captureReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.Shutdown));
                Assert.Throws<ObjectDisposedException>(() => harness.Publication.Register(
                    SkinRevisionParticipantKind.LifecycleHolder,
                    "late holder"));
                Assert.That(
                    harness.Publication.RegisterExactOwner(harness.InitialOwner, SkinRevisionParticipantKind.LifecycleHolder, "exact late holder"),
                    Is.Null);
                Assert.Throws<ObjectDisposedException>(() => harness.CreateProvisional("revision-c"));
            });

            Assert.That(
                harness.Publication.TryCommit(snapshot, provisional, out SkinCurrentRevision previous, out SkinRevisionBarrierRejectionReason commitReason),
                Is.False);
            Assert.Multiple(() =>
            {
                Assert.That(previous, Is.SameAs(harness.Publication.Current));
                Assert.That(commitReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.Shutdown));
            });

            previous.ReleaseManagerLease();
            claimed[0].Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(previous.LeaseCount, Is.EqualTo(1));
                Assert.That(previous.ParticipantLeaseCount, Is.EqualTo(1));
                Assert.That(previous.ConsumersDetached.IsCompleted, Is.False);
                Assert.That(previous.Retired.IsCompleted, Is.False);
                Assert.That(harness.RetirementClaims(previous), Is.Zero);
            });

            claimed[1].Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(previous.ParticipantLeaseCount, Is.Zero);
                Assert.That(previous.ConsumersDetached.IsCompletedSuccessfully, Is.True);
            });

            provisional.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(previous);
            await harness.AssertRetiredExactlyOnce(provisional);
        }

        [Test]
        public async Task TestShutdownSnapshotJoinsWorkLeaseWithoutImpersonatingVisualDetach()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision revision = harness.Publication.Current;
            SkinRevisionParticipantRegistration visual = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "visual with hidden work");
            SkinCurrentRevisionLease work = visual.AcquireWorkLease()!;

            IReadOnlyList<SkinRevisionParticipantRegistration> claimed = harness.Publication.ShutdownAndClaimParticipants();
            Task[] workDetachments = harness.Publication.CaptureRevisionWorkDetachments();

            Assert.Multiple(() =>
            {
                Assert.That(claimed, Is.EqualTo(new[] { visual }));
                Assert.That(workDetachments, Has.Length.EqualTo(1));
                Assert.That(workDetachments[0], Is.SameAs(revision.WorkDetached));
                Assert.That(revision.WorkDetached.IsCompleted, Is.False);
                Assert.That(revision.ConsumersDetached.IsCompleted, Is.False);
                Assert.That(visual.AcquireWorkLease(), Is.Null, "Shutdown must reject work started from a claimed registration.");
                Assert.Throws<ObjectDisposedException>(() => harness.Publication.AcquireCurrentWorkLease());
            });

            revision.ReleaseManagerLease();
            visual.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(revision.WorkDetached.IsCompleted, Is.False);
                Assert.That(revision.ConsumersDetached.IsCompleted, Is.False,
                    "The hidden owner-touching job remains a consumer lease until its real completion.");
                Assert.That(revision.Retired.IsCompleted, Is.False);
            });

            work.Dispose();

            await Task.WhenAll(workDetachments).WaitAsync(test_timeout);
            await harness.AssertRetiredExactlyOnce(revision);
            Assert.Multiple(() =>
            {
                Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                Assert.That(revision.ConsumersDetached.IsCompletedSuccessfully, Is.True);
            });
        }

        [Test]
        public async Task TestShutdownRequestsExactOwnerWorkOnceOutsidePublicationLock()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision revision = harness.Publication.Current;
            SkinCurrentRevisionLease? work = null;
            int shutdownRequests = 0;
            SkinRevisionParticipantRegistration participant = null!;
            participant = harness.Publication.Register(
                SkinRevisionParticipantKind.CoherentVisualConsumer,
                "owner-cancelled hidden work",
                shutdownWork: () =>
                {
                    Interlocked.Increment(ref shutdownRequests);

                    // Re-entering publication proves the owner callback is not invoked under its global lock. New work
                    // is rejected because shutdown admission was claimed before this callback became reachable.
                    Assert.That(harness.Publication.AcquireWorkLease(participant), Is.Null);
                    Interlocked.Exchange(ref work, null)?.Dispose();
                });
            work = participant.AcquireWorkLease();

            IReadOnlyList<SkinRevisionParticipantRegistration> claimed =
                harness.Publication.ShutdownAndClaimParticipants();

            await revision.WorkDetached.WaitAsync(test_timeout);

            Assert.Multiple(() =>
            {
                Assert.That(claimed, Is.EqualTo(new[] { participant }));
                Assert.That(shutdownRequests, Is.EqualTo(1));
                Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                Assert.That(revision.ConsumersDetached.IsCompleted, Is.False,
                    "Shutdown work cancellation must not impersonate the visual participant's detach.");
                Assert.That(participant.IsDisposed, Is.False);
                Assert.That(harness.Publication.ShutdownAndClaimParticipants(), Is.Empty);
                Assert.That(shutdownRequests, Is.EqualTo(1));
            });

            revision.ReleaseManagerLease();
            participant.Dispose();
            await harness.AssertRetiredExactlyOnce(revision);
        }

        [Test]
        public void TestDisposedParticipantCannotReceiveLateShutdownWorkRequest()
        {
            using var harness = new PublicationHarness();
            int shutdownRequests = 0;
            SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "already detached owner",
                shutdownWork: () => Interlocked.Increment(ref shutdownRequests));

            participant.Dispose();
            Assert.That(harness.Publication.ShutdownAndClaimParticipants(), Is.Empty);
            Assert.That(shutdownRequests, Is.Zero);
        }

        [Test]
        public void TestFaultingShutdownWorkRequestCannotSkipAnotherExactOwner()
        {
            using var harness = new PublicationHarness();
            int successfulRequests = 0;
            SkinRevisionParticipantRegistration faulting = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "faulting shutdown owner",
                shutdownWork: () => throw new InvalidOperationException("private owner detail"));
            SkinRevisionParticipantRegistration succeeding = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "succeeding shutdown owner",
                shutdownWork: () => Interlocked.Increment(ref successfulRequests));

            IReadOnlyList<SkinRevisionParticipantRegistration> claimed =
                harness.Publication.ShutdownAndClaimParticipants();

            Assert.Multiple(() =>
            {
                Assert.That(claimed, Is.EquivalentTo(new[] { faulting, succeeding }));
                Assert.That(successfulRequests, Is.EqualTo(1));
                Assert.That(harness.Publication.ShutdownAndClaimParticipants(), Is.Empty);
                Assert.That(successfulRequests, Is.EqualTo(1));
            });

            faulting.Dispose();
            succeeding.Dispose();
        }

        [Test]
        public async Task TestFinalWorkDetachDoesNotCompleteBeforeSynchronousOwnerReap()
        {
            var owner = new TestSkin(Guid.NewGuid(), "blocked work retirement");
            using var retirementEntered = new ManualResetEventSlim();
            using var allowRetirement = new ManualResetEventSlim();
            var revision = new SkinCurrentRevision(
                generation: 1,
                owner.SkinInfo.ID,
                "work-reap",
                SkinCurrentRevisionSourceKind.ManagedFolder,
                owner,
                keepsReusableOwner: false,
                retired =>
                {
                    retirementEntered.Set();

                    if (!allowRetirement.Wait(test_timeout))
                        throw new TimeoutException("Owner reap was not released.");

                    retired.RetireOwner();
                });
            SkinCurrentRevisionLease work = revision.AcquireWorkLease();
            revision.ReleaseManagerLease();

            Task finalDetach = Task.Run(work.Dispose);
            Assert.That(retirementEntered.Wait(test_timeout), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(finalDetach.IsCompleted, Is.False);
                Assert.That(revision.WorkDetached.IsCompleted, Is.False,
                    "Realm teardown must not overtake a synchronous owner reap triggered by final work detach.");
                Assert.That(revision.Retired.IsCompleted, Is.False);
                Assert.That(owner.DisposeCount, Is.Zero);
            });

            allowRetirement.Set();
            await finalDetach.WaitAsync(test_timeout);
            await revision.WorkDetached.WaitAsync(test_timeout);
            await revision.Retired.WaitAsync(test_timeout);

            Assert.Multiple(() =>
            {
                Assert.That(revision.WorkDetached.IsCompletedSuccessfully, Is.True);
                Assert.That(owner.DisposeCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task TestShutdownSnapshotIncludesRevisionWhileFinalWorkReapIsInFlight()
        {
            var owner = new TestSkin(Guid.NewGuid(), "snapshot work retirement");
            using var retirementEntered = new ManualResetEventSlim();
            using var allowRetirement = new ManualResetEventSlim();
            var publication = new SkinCurrentRevisionPublication(
                owner,
                "snapshot-work-reap",
                SkinCurrentRevisionSourceKind.ManagedFolder,
                keepsReusableOwner: false,
                retired =>
                {
                    retirementEntered.Set();

                    if (!allowRetirement.Wait(test_timeout))
                        throw new TimeoutException("Owner reap was not released.");

                    retired.RetireOwner();
                });
            SkinCurrentRevision revision = publication.Current;
            SkinCurrentRevisionLease work = revision.AcquireWorkLease();
            revision.ReleaseManagerLease();

            Task finalDetach = Task.Run(work.Dispose);
            Assert.That(retirementEntered.Wait(test_timeout), Is.True);
            Task[] shutdownFences = publication.CaptureRevisionWorkDetachments();

            Assert.Multiple(() =>
            {
                Assert.That(shutdownFences, Has.Length.EqualTo(1));
                Assert.That(shutdownFences[0], Is.SameAs(revision.WorkDetached));
                Assert.That(shutdownFences[0].IsCompleted, Is.False);
                Assert.That(revision.Retired.IsCompleted, Is.False);
            });

            allowRetirement.Set();
            await Task.WhenAll(shutdownFences).WaitAsync(test_timeout);
            await finalDetach.WaitAsync(test_timeout);
            await revision.Retired.WaitAsync(test_timeout);
            Assert.That(owner.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task TestConcurrentDisposeAndAdoptReleaseExactlyOwnedLeases()
        {
            const int iteration_count = 128;

            for (int i = 0; i < iteration_count; i++)
            {
                using var harness = new PublicationHarness();
                SkinCurrentRevision initial = harness.Publication.Current;
                SkinRevisionParticipantRegistration participant = harness.Publication.Register(
                    SkinRevisionParticipantKind.LifecycleHolder,
                    $"racing holder {i}");
                SkinCurrentRevision next = harness.CreateProvisional($"revision-b-{i}");
                Assert.That(harness.CommitRetaining(next), Is.SameAs(initial));
                using var start = new ManualResetEventSlim();

                Task adopt = Task.Run(() =>
                {
                    if (!start.Wait(test_timeout))
                        throw new TimeoutException("Timed out waiting to race revision adoption.");

                    participant.AdoptCurrentRevision();
                });
                Task dispose = Task.Run(() =>
                {
                    if (!start.Wait(test_timeout))
                        throw new TimeoutException("Timed out waiting to race participant disposal.");

                    participant.Dispose();
                });

                start.Set();
                await Task.WhenAll(adopt, dispose).WaitAsync(test_timeout);

                initial.ReleaseManagerLease();
                next.ReleaseManagerLease();
                await harness.AssertRetiredExactlyOnce(initial);
                await harness.AssertRetiredExactlyOnce(next);

                Assert.Multiple(() =>
                {
                    Assert.That(participant.IsDisposed, Is.True);
                    Assert.That(initial.LeaseCount, Is.Zero);
                    Assert.That(next.LeaseCount, Is.Zero);
                });
            }
        }

        [Test]
        public async Task TestExactOwnerRegistrationUsesOwnerReferenceNotRecordId()
        {
            Guid sharedRecordId = Guid.NewGuid();
            using var harness = new PublicationHarness(sharedRecordId);
            var sameRecordImpostor = new TestSkin(sharedRecordId, "same-record impostor");

            SkinRevisionParticipantRegistration? initialExact = harness.Publication.RegisterExactOwner(
                harness.InitialOwner,
                SkinRevisionParticipantKind.LifecycleHolder,
                "initial exact owner");
            SkinRevisionParticipantRegistration? impostor = harness.Publication.RegisterExactOwner(
                sameRecordImpostor,
                SkinRevisionParticipantKind.LifecycleHolder,
                "same record different owner");

            Assert.Multiple(() =>
            {
                Assert.That(initialExact, Is.Not.Null);
                Assert.That(initialExact!.CurrentRevision.Owner, Is.SameAs(harness.InitialOwner));
                Assert.That(impostor, Is.Null);
            });

            SkinCurrentRevision next = harness.CreateProvisional("revision-b", sharedRecordId);
            SkinCurrentRevision previous = harness.CommitRetaining(next);

            Assert.Multiple(() =>
            {
                Assert.That(
                    harness.Publication.RegisterExactOwner(harness.InitialOwner, SkinRevisionParticipantKind.LifecycleHolder, "stale exact owner"),
                    Is.Null);
                Assert.That(
                    harness.Publication.RegisterExactOwner(sameRecordImpostor, SkinRevisionParticipantKind.LifecycleHolder, "same-id impostor"),
                    Is.Null);
            });

            SkinRevisionParticipantRegistration? nextExact = harness.Publication.RegisterExactOwner(
                next.Owner,
                SkinRevisionParticipantKind.LifecycleHolder,
                "next exact owner");
            Assert.That(nextExact, Is.Not.Null);
            Assert.That(nextExact!.CurrentRevision, Is.SameAs(next));

            previous.ReleaseManagerLease();
            Assert.That(previous.Retired.IsCompleted, Is.False);
            initialExact!.Dispose();
            await harness.AssertRetiredExactlyOnce(previous);

            nextExact.Dispose();
            next.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(next);

            sameRecordImpostor.Dispose();
            Assert.That(sameRecordImpostor.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public async Task TestLiveGameplayParticipantRejectsSnapshotWithoutChangingCurrent()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "live gameplay");

            Assert.That(harness.Publication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason rejectionReason), Is.Null);
            Assert.Multiple(() =>
            {
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.LiveGameplayActive));
                Assert.That(harness.Publication.Current, Is.SameAs(initial));
                Assert.That(live.CurrentRevision, Is.SameAs(initial));
            });

            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        private sealed class PublicationHarness : IDisposable
        {
            private readonly ConcurrentDictionary<long, int> retirementClaims = new ConcurrentDictionary<long, int>();
            private readonly ConcurrentBag<TestSkin> createdOwners = new ConcurrentBag<TestSkin>();

            public TestSkin InitialOwner { get; }
            public SkinCurrentRevisionPublication Publication { get; }

            public PublicationHarness(Guid? initialRecordId = null)
            {
                InitialOwner = createOwner(initialRecordId ?? Guid.NewGuid(), "initial owner");
                Publication = new SkinCurrentRevisionPublication(
                    InitialOwner,
                    "revision-a",
                    SkinCurrentRevisionSourceKind.RealmPackage,
                    keepsReusableOwner: false,
                    queueRetirement);
            }

            public SkinCurrentRevision CreateProvisional(string contentRevision, Guid? recordId = null)
            {
                Guid exactRecordId = recordId ?? Guid.NewGuid();
                return Publication.CreateProvisional(
                    exactRecordId,
                    contentRevision,
                    SkinCurrentRevisionSourceKind.RealmPackage,
                    createOwner(exactRecordId, contentRevision));
            }

            public SkinRevisionParticipantSnapshot CaptureSnapshot()
            {
                SkinRevisionParticipantSnapshot snapshot = Publication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason rejectionReason);
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
                return snapshot;
            }

            public SkinCurrentRevision CommitRetaining(SkinCurrentRevision next)
            {
                SkinRevisionParticipantSnapshot snapshot = CaptureSnapshot();
                Assert.That(
                    Publication.TryCommit(
                        snapshot,
                        next,
                        out SkinCurrentRevision previous,
                        out SkinRevisionBarrierRejectionReason rejectionReason),
                    Is.True);
                Assert.That(rejectionReason, Is.EqualTo(SkinRevisionBarrierRejectionReason.None));
                return previous;
            }

            public int RetirementClaims(SkinCurrentRevision revision)
                => retirementClaims.TryGetValue(revision.Generation, out int count) ? count : 0;

            public async Task AssertRetiredExactlyOnce(SkinCurrentRevision revision)
            {
                await revision.Detached.WaitAsync(test_timeout);
                await revision.Retired.WaitAsync(test_timeout);
                Assert.Multiple(() =>
                {
                    Assert.That(revision.LeaseCount, Is.Zero);
                    Assert.That(RetirementClaims(revision), Is.EqualTo(1));
                    Assert.That(((TestSkin)revision.Owner).DisposeCount, Is.EqualTo(1));
                });
            }

            public void Dispose()
            {
                foreach (TestSkin owner in createdOwners)
                {
                    if (owner.DisposeCount == 0)
                        owner.Dispose();
                }
            }

            private TestSkin createOwner(Guid recordId, string name)
            {
                var owner = new TestSkin(recordId, name);
                createdOwners.Add(owner);
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
