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
using osu.Game.Skinning.Gameplay;

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

        [Test]
        public async Task TestGameplayLayoutPrepareRetainsExactWorkLeaseAndLateCommitFailsAfterRootDetach()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "live layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            GameplaySkinLayoutPublication? preparedPublication = null;
            GameplaySkinPreparedLayout prepared = layoutOwner.PreparePublication(revision =>
                preparedPublication = createMaterialPublication(package, revision, "material-a"));

            Assert.Multiple(() =>
            {
                Assert.That(initial.LeaseCount, Is.EqualTo(3));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(2));
                Assert.That(initial.WorkDetached.IsCompleted, Is.False);
                Assert.That(layoutOwner.Current, Is.Null);
                Assert.That(prepared.Publication, Is.SameAs(preparedPublication));
                Assert.That(prepared.Publication.MaterialSet.Snapshot, Is.SameAs(prepared.Snapshot));
                Assert.That(prepared.Publication.MaterialSet.PackageRevision, Is.SameAs(package));
            });

            live.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(1));
                Assert.That(initial.WorkDetached.IsCompleted, Is.False);
                Assert.That(layoutOwner.TryCommit(prepared), Is.False);
                Assert.That(layoutOwner.Current, Is.Null);
                Assert.That(layoutOwner.CurrentPublication, Is.Null);
            });

            await initial.WorkDetached.WaitAsync(test_timeout);
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutSolveFailureReleasesWorkLeaseWithoutPublication()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "live layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            Assert.That(
                () => layoutOwner.PreparePublication(_ => throw new InvalidOperationException("geometry failed")),
                Throws.InvalidOperationException.With.Message.EqualTo("geometry failed"));

            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.Current, Is.Null);
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(1));
                Assert.That(initial.WorkDetached.IsCompleted, Is.True);
            });

            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutCancellationAfterCarrierCreationReleasesWorkLeaseAndRetirement()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "cancelled layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            using var cancellation = new CancellationTokenSource();
            var retirement = new CountingDisposable();

            Assert.That(
                () => layoutOwner.PreparePublication(
                    revision =>
                    {
                        GameplaySkinLayoutPublication publication = createMaterialPublication(
                            package,
                            revision,
                            "cancelled-material",
                            retirement);
                        cancellation.Cancel();
                        return publication;
                    },
                    cancellation.Token),
                Throws.TypeOf<OperationCanceledException>());

            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.CurrentPublication, Is.Null);
                Assert.That(retirement.DisposeCount, Is.EqualTo(1));
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(1));
                Assert.That(initial.WorkDetached.IsCompleted, Is.True);
            });

            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutExactOwnerRejectsEmptyCompatibilityMaterialContract()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "exact material contract root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            GameplaySkinLayoutSnapshot snapshot = createLayoutSnapshot(package, 0);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot)),
                    Throws.ArgumentException.With.Message.Contains("exact gameplay layout publication"));
                Assert.That(
                    () => layoutOwner.Prepare(revision => createLayoutSnapshot(package, revision)),
                    Throws.InvalidOperationException.With.Message.EqualTo(
                        "An exact gameplay layout preparation must publish a resolved material set through PreparePublication."));
                Assert.That(layoutOwner.CurrentPublication, Is.Null);
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(1));
            });

            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutExactOwnerRejectsDirectSecondPublication()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "single-publication layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);

            GameplaySkinPreparedLayout first = layoutOwner.PreparePublication(
                revision => createMaterialPublication(package, revision, "material-a"));
            Assert.That(layoutOwner.TryCommit(first), Is.True);
            GameplaySkinLayoutPublication published = layoutOwner.CurrentPublication!;

            Assert.That(
                () => layoutOwner.PreparePublication(revision => createMaterialPublication(package, revision, "material-b")),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "An exact gameplay layout root may publish only one immutable layout."));

            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.CurrentPublication, Is.SameAs(published));
                Assert.That(initial.LeaseCount, Is.EqualTo(2));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(1));
                Assert.That(initial.WorkDetached.IsCompleted, Is.True);
            });

            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutExactOwnerRejectsPreparationWithoutFreshWorkLease()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => true,
                acquireWorkLease: () => null,
                captureParticipantGeneration: () => 0,
                validateParticipantGeneration: _ => true,
                commitAtParticipantGeneration: (_, commit) =>
                {
                    commit();
                    return true;
                },
                dispatchCommit: runLayoutCommitImmediately);

            Assert.That(
                () => layoutOwner.PreparePublication(revision => createMaterialPublication(package, revision, "material-a")),
                Throws.InvalidOperationException.With.Message.EqualTo(
                    "An exact gameplay layout preparation requires a fresh package work lease."));
            Assert.That(layoutOwner.Current, Is.Null);

            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutParticipantAttachInvalidatesPrepareAndFreshBarrierCanCommit()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "live layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            SkinRevisionParticipantRegistration? attached = null;

            Assert.That(
                () => layoutOwner.PreparePublication(revision =>
                {
                    attached = harness.Publication.Register(
                        SkinRevisionParticipantKind.LifecycleHolder,
                        "attached during layout prepare");
                    return createMaterialPublication(package, revision, "stale-material");
                }),
                Throws.TypeOf<GameplaySkinLayoutParticipantBarrierChangedException>().With.Message.EqualTo(
                    "The gameplay layout participant barrier changed during background preparation."));

            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.Current, Is.Null);
                Assert.That(initial.LeaseCount, Is.EqualTo(3));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(2));
            });

            GameplaySkinPreparedLayout fresh = layoutOwner.PreparePublication(
                revision => createMaterialPublication(package, revision, "fresh-material"));
            Assert.That(layoutOwner.TryCommit(fresh), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.Current, Is.SameAs(fresh.Snapshot));
                Assert.That(layoutOwner.CurrentPublication, Is.SameAs(fresh.Publication));
                Assert.That(layoutOwner.CurrentPublication!.MaterialSet, Is.SameAs(fresh.Publication.MaterialSet));
            });

            attached!.Dispose();
            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestNonLayoutParticipantAttachDoesNotInvalidateGameplayLayoutCarrier()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "live layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            SkinRevisionParticipantRegistration? nonLayout = null;

            GameplaySkinPreparedLayout prepared = layoutOwner.PreparePublication(revision =>
            {
                nonLayout = harness.Publication.Register(
                    SkinRevisionParticipantKind.LiveGameplayHost,
                    "sample-only ruleset provider",
                    affectsGameplayLayoutPublication: false);
                return createMaterialPublication(package, revision, "material-a");
            });

            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.TryCommit(prepared), Is.True);
                Assert.That(layoutOwner.Current, Is.SameAs(prepared.Snapshot));
                Assert.That(live.IsPublicationGenerationCurrent(prepared.ParticipantGeneration), Is.True);
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(2));
            });

            nonLayout!.Dispose();
            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutParticipantAttachBeforeCommitRejectsStaleCarrier()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "live layout root");
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.Create(initial);
            var layoutOwner = new GameplaySkinLayoutRevisionOwner(
                package,
                validateRoot: () => live.TryGetCurrentRevision(out SkinCurrentRevision? revision)
                                    && package.RetainsExact(revision!),
                acquireWorkLease: live.AcquireWorkLease,
                captureParticipantGeneration: () => live.TryCapturePublicationGeneration(out long generation) ? generation : null,
                validateParticipantGeneration: live.IsPublicationGenerationCurrent,
                commitAtParticipantGeneration: live.TryCommitAtPublicationGeneration,
                dispatchCommit: runLayoutCommitImmediately);
            GameplaySkinPreparedLayout stale = layoutOwner.PreparePublication(
                revision => createMaterialPublication(package, revision, "stale-material"));
            using SkinRevisionParticipantRegistration attached = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "attached before layout commit");

            Assert.Multiple(() =>
            {
                Assert.That(layoutOwner.TryCommit(stale), Is.False);
                Assert.That(layoutOwner.Current, Is.Null);
                Assert.That(initial.LeaseCount, Is.EqualTo(3));
                Assert.That(initial.ParticipantLeaseCount, Is.EqualTo(2));
            });

            GameplaySkinPreparedLayout fresh = layoutOwner.PreparePublication(
                revision => createMaterialPublication(package, revision, "fresh-material"));
            Assert.That(layoutOwner.TryCommit(fresh), Is.True);

            attached.Dispose();
            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestGameplayLayoutCommitAdmissionAndLateParticipantAttachHaveOneAtomicOrder()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "atomic layout root");
            Assert.That(live.TryCapturePublicationGeneration(out long generation), Is.True);
            using var commitAdmitted = new ManualResetEventSlim();
            using var releaseCommit = new ManualResetEventSlim();
            using var attachStarted = new ManualResetEventSlim();
            bool exchanged = false;

            Task<bool> commit = Task.Run(() => live.TryCommitAtPublicationGeneration(generation, () =>
            {
                commitAdmitted.Set();

                while (!releaseCommit.Wait(TimeSpan.FromMilliseconds(100)))
                {
                }

                exchanged = true;
            }));
            Assert.That(commitAdmitted.Wait(test_timeout), Is.True);

            Task<SkinRevisionParticipantRegistration> lateAttach = Task.Run(() =>
            {
                attachStarted.Set();
                return harness.Publication.Register(
                    SkinRevisionParticipantKind.LifecycleHolder,
                    "late layout observer");
            });
            Assert.That(attachStarted.Wait(test_timeout), Is.True);
            Assert.That(lateAttach.Wait(TimeSpan.FromMilliseconds(100)), Is.False,
                "Participant registration must not enter between generation admission and the reference exchange.");

            releaseCommit.Set();
            Assert.That(await commit.WaitAsync(test_timeout), Is.True);
            using SkinRevisionParticipantRegistration attached = await lateAttach.WaitAsync(test_timeout);

            Assert.Multiple(() =>
            {
                Assert.That(exchanged, Is.True);
                Assert.That(attached.CurrentRevision, Is.SameAs(initial));
                Assert.That(live.IsPublicationGenerationCurrent(generation), Is.False,
                    "The participant which attached after the exchange must advance the next barrier generation.");
            });

            attached.Dispose();
            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        [Test]
        public async Task TestParticipantAttachWinningBeforeGameplayLayoutAdmissionRejectsStaleExchange()
        {
            using var harness = new PublicationHarness();
            SkinCurrentRevision initial = harness.Publication.Current;
            using SkinRevisionParticipantRegistration live = harness.Publication.Register(
                SkinRevisionParticipantKind.LiveGameplayHost,
                "stale layout root");
            Assert.That(live.TryCapturePublicationGeneration(out long generation), Is.True);
            using SkinRevisionParticipantRegistration attached = harness.Publication.Register(
                SkinRevisionParticipantKind.LifecycleHolder,
                "attachment which wins admission");
            bool exchanged = false;

            Assert.Multiple(() =>
            {
                Assert.That(live.TryCommitAtPublicationGeneration(generation, () => exchanged = true), Is.False);
                Assert.That(exchanged, Is.False);
            });

            attached.Dispose();
            live.Dispose();
            initial.ReleaseManagerLease();
            await harness.AssertRetiredExactlyOnce(initial);
        }

        private static GameplaySkinLayoutSnapshot createLayoutSnapshot(GameplaySkinPackageRevision package, long layoutRevision)
        {
            GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group"), GameplaySkinLaneSide.Neutral);
            GameplaySkinLaneTopologySnapshot topology = GameplaySkinLaneTopologySnapshot.Create(new[]
            {
                GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, new[]
                {
                    GameplaySkinLaneTopologyEntry.Create(
                        GameplaySkinLaneIdentity.Create(
                            GameplaySkinLaneId.Create("test.lane"), groupIdentity, GameplaySkinLaneRole.Key),
                        0, 0, 0, 0),
                }),
            });
            GameplaySkinLayoutRect screen = GameplaySkinLayoutRect.Create(0, 0, 1, 1);
            GameplaySkinLayoutRect playfield = GameplaySkinLayoutRect.Create(0.25f, 0, 0.5f, 0.9f);
            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(
                "mania",
                "stages-1",
                "1k",
                "mania-single",
                topology,
                screen,
                screen,
                16f / 9f,
                1,
                GameplaySkinScrollDirection.Down,
                package,
                topologyRevision: 0,
                layoutRevision);

            return GameplaySkinLayoutSnapshot.Create(
                context,
                new[] { new GameplaySkinLayoutGroup(topology.GroupsInLogicalOrder[0], playfield) },
                new[] { new GameplaySkinLayoutLane(topology.LanesInLogicalOrder[0], playfield) },
                new[] { new GameplaySkinLayoutSurface("playfield", playfield, 0, true, true) });
        }

        private static GameplaySkinLayoutPublication createMaterialPublication(
            GameplaySkinPackageRevision package,
            long layoutRevision,
            string contentRevision,
            IDisposable? retirement = null)
        {
            GameplaySkinLayoutSnapshot snapshot = createLayoutSnapshot(package, layoutRevision);
            GameplaySkinLaneTopologySnapshot topology = snapshot.Context.Topology;
            GameplaySkinResolvedMaterialEntry note = GameplaySkinResolvedMaterialEntry.Provide(
                GameplaySkinSlotCatalog.Note,
                GameplaySkinResolvedMaterialTarget.ForLane(topology.GroupsInLogicalOrder[0], topology.LanesInLogicalOrder[0]),
                GameplaySkinResolvedMaterialSourceIdentity.Create(
                    GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                    "selected.current",
                    contentRevision),
                contentRevision);
            GameplaySkinResolvedMaterialSet materialSet = GameplaySkinResolvedMaterialSet.Create(
                snapshot,
                GameplaySkinMaterialContractIdentity.CurrentFor(snapshot),
                new[] { note });
            return retirement == null
                ? GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot), materialSet)
                : GameplaySkinLayoutPublication.Create(new TestLayoutAdapter(snapshot), materialSet, retirement);
        }

        private static bool runLayoutCommitImmediately(Action commit)
        {
            commit();
            return true;
        }

        private sealed class TestLayoutAdapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public TestLayoutAdapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }

        private sealed class CountingDisposable : IDisposable
        {
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            public void Dispose() => Interlocked.Increment(ref disposeCount);
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
