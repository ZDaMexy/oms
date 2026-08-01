// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinManagedFolderOperationCoordinatorTest
    {
        private static readonly TimeSpan coordination_timeout = TimeSpan.FromSeconds(10);

        [Test]
        public void TestShortScopeCanNestOnSameThread()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();

            using SkinManagedFolderOperationCoordinator.Lease outer = coordinator.Enter();
            using SkinManagedFolderOperationCoordinator.Lease nested = coordinator.Enter();

            Assert.That(coordinator.TryEnter(out SkinManagedFolderOperationCoordinator.Lease? attempted), Is.True);
            Assert.That(attempted, Is.Not.Null);
            attempted!.Dispose();
        }

        [Test]
        public void TestMutationReservationRejectsSameThreadReentry()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();

            using SkinManagedFolderOperationCoordinator.Lease mutation = coordinator.EnterMutation();

            Assert.Multiple(() =>
            {
                Assert.That(coordinator.TryEnter(out SkinManagedFolderOperationCoordinator.Lease? attempted), Is.False);
                Assert.That(attempted, Is.Null);
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease unexpected = coordinator.Enter();
                });
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using SkinManagedFolderOperationCoordinator.Lease unexpected = coordinator.EnterMutation();
                });
            });
        }

        [Test]
        public void TestOtherThreadTryEnterRejectsAndEnterWaitsUntilRelease()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.Lease held = coordinator.EnterMutation();
            using var tryCompleted = new ManualResetEventSlim();
            using var blockingEnterStarted = new ManualResetEventSlim();
            using var blockingEnterCompleted = new ManualResetEventSlim();
            bool tryEntered = true;

            Task participant = Task.Run(() =>
            {
                tryEntered = coordinator.TryEnter(out SkinManagedFolderOperationCoordinator.Lease? attempted);
                attempted?.Dispose();
                tryCompleted.Set();
                blockingEnterStarted.Set();

                using SkinManagedFolderOperationCoordinator.Lease entered = coordinator.Enter();
                blockingEnterCompleted.Set();
            });

            try
            {
                Assert.That(tryCompleted.Wait(coordination_timeout), Is.True);
                Assert.That(blockingEnterStarted.Wait(coordination_timeout), Is.True);

                Assert.Multiple(() =>
                {
                    Assert.That(tryEntered, Is.False);
                    Assert.That(blockingEnterCompleted.IsSet, Is.False);
                    Assert.That(participant.IsCompleted, Is.False);
                });
            }
            finally
            {
                held.Dispose();
            }

            Assert.That(blockingEnterCompleted.Wait(coordination_timeout), Is.True);
            Assert.That(participant.Wait(coordination_timeout), Is.True);
            Assert.That(participant.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void TestMutationReservationCanBeDisposedAcrossThreadsAndReused()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.Lease mutation = coordinator.EnterMutation();

            Task disposal = Task.Run(mutation.Dispose);

            Assert.That(disposal.Wait(coordination_timeout), Is.True);
            Assert.That(disposal.IsCompletedSuccessfully, Is.True);

            using SkinManagedFolderOperationCoordinator.Lease subsequent = coordinator.Enter();
            Assert.That(subsequent, Is.Not.Null);
        }

        [Test]
        public void TestStartupSequenceReportsCompletionWithoutBlockingSelection()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.Lease startup = coordinator.EnterStartupSequence();
            using SkinManagedFolderOperationCoordinator.Lease nested = coordinator.Enter();
            using var attemptCompleted = new ManualResetEventSlim();
            SkinManagedFolderOperationCoordinator.SelectionContention? contention = null;
            bool entered = true;

            Task participant = Task.Run(() =>
            {
                entered = coordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease? attempted,
                    out contention);
                attempted?.Dispose();
                attemptCompleted.Set();
            });

            Assert.That(attemptCompleted.Wait(coordination_timeout), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(entered, Is.False);
                Assert.That(contention, Is.Not.Null);
                Assert.That(
                    contention!.Kind,
                    Is.EqualTo(SkinManagedFolderOperationCoordinator.SelectionContentionKind.StartupSequence));
                Assert.That(contention.Completion.IsCompleted, Is.False);
            });

            nested.Dispose();
            startup.Dispose();

            Assert.That(contention!.Completion.Wait(coordination_timeout), Is.True);
            Assert.That(participant.Wait(coordination_timeout), Is.True);
            Assert.That(participant.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void TestMutationReservationNeverReportsStartupCompletion()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            using SkinManagedFolderOperationCoordinator.Lease mutation = coordinator.EnterMutation();
            SkinManagedFolderOperationCoordinator.SelectionContention? contention = null;

            Task<bool> participant = Task.Run(() =>
            {
                bool entered = coordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease? attempted,
                    out contention);
                attempted?.Dispose();
                return entered;
            });

            Assert.That(participant.Wait(coordination_timeout), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(participant.GetAwaiter().GetResult(), Is.False);
                Assert.That(contention, Is.Null);
            });
        }

        [Test]
        public void TestStagedImportReportsTypedRetryableContention()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.Lease stagedImport = coordinator.EnterStagedImport();

            Task<(bool Entered, SkinManagedFolderOperationCoordinator.SelectionContention? Contention)> participant = Task.Run(() =>
            {
                bool entered = coordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease? attempted,
                    out SkinManagedFolderOperationCoordinator.SelectionContention? contention);
                attempted?.Dispose();
                return (entered, contention);
            });

            Assert.That(participant.Wait(coordination_timeout), Is.True);
            (bool Entered, SkinManagedFolderOperationCoordinator.SelectionContention? Contention) result = participant.GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(result.Entered, Is.False);
                Assert.That(result.Contention, Is.Not.Null);
                Assert.That(
                    result.Contention!.Kind,
                    Is.EqualTo(SkinManagedFolderOperationCoordinator.SelectionContentionKind.StagedImport));
                Assert.That(result.Contention.Completion.IsCompleted, Is.False);
            });

            stagedImport.Dispose();

            Assert.That(result.Contention!.Completion.Wait(coordination_timeout), Is.True);
        }

        [Test]
        public void TestCancelledBlockingWaiterDoesNotWaitForCurrentHolder()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            using SkinManagedFolderOperationCoordinator.Lease mutation = coordinator.EnterMutation();
            using var waiterStarted = new ManualResetEventSlim();
            using var cancellation = new CancellationTokenSource();
            Exception? waiterException = null;

            var waiter = new Thread(() =>
            {
                waiterStarted.Set();

                try
                {
                    using SkinManagedFolderOperationCoordinator.Lease unexpected =
                        coordinator.EnterStartupSequence(cancellation.Token);
                }
                catch (Exception exception)
                {
                    waiterException = exception;
                }
            });
            waiter.Start();

            Assert.That(waiterStarted.Wait(coordination_timeout), Is.True);
            Assert.That(
                SpinWait.SpinUntil(
                    () => (waiter.ThreadState & ThreadState.WaitSleepJoin) != 0,
                    coordination_timeout),
                Is.True);
            cancellation.Cancel();

            Assert.That(waiter.Join(coordination_timeout), Is.True);
            Assert.That(waiterException, Is.TypeOf<OperationCanceledException>());
        }

        [Test]
        public void TestQueuedStartupDoesNotMaskCurrentMutationReservation()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.Lease mutation = coordinator.EnterMutation();
            using var startupAttempting = new ManualResetEventSlim();
            using var startupAcquired = new ManualResetEventSlim();

            Task startupParticipant = Task.Run(() =>
            {
                startupAttempting.Set();

                using SkinManagedFolderOperationCoordinator.Lease startup =
                    coordinator.EnterStartupSequence();
                startupAcquired.Set();
            });

            Assert.That(startupAttempting.Wait(coordination_timeout), Is.True);

            Task<(bool Entered, SkinManagedFolderOperationCoordinator.SelectionContention? Contention)> selectionParticipant = Task.Run(() =>
            {
                bool entered = coordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease? attempted,
                    out SkinManagedFolderOperationCoordinator.SelectionContention? contention);
                attempted?.Dispose();
                return (entered, contention);
            });

            Assert.That(selectionParticipant.Wait(coordination_timeout), Is.True);
            (bool Entered, SkinManagedFolderOperationCoordinator.SelectionContention? Contention) selectionResult = selectionParticipant.GetAwaiter().GetResult();
            Assert.Multiple(() =>
            {
                Assert.That(selectionResult.Entered, Is.False);
                Assert.That(selectionResult.Contention, Is.Null);
                Assert.That(startupAcquired.IsSet, Is.False);
            });

            mutation.Dispose();

            Assert.That(startupAcquired.Wait(coordination_timeout), Is.True);
            Assert.That(startupParticipant.Wait(coordination_timeout), Is.True);
            Assert.That(startupParticipant.IsCompletedSuccessfully, Is.True);
        }

        [Test]
        public void TestCompletedStartupCrossingReportsRetryableContention()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation observation =
                coordinator.CaptureSelectionPreparationObservation();

            using (coordinator.EnterStartupSequence())
            {
            }

            SkinManagedFolderOperationCoordinator.SelectionContention? contention =
                coordinator.TryGetRetryableContentionSince(observation);

            Assert.Multiple(() =>
            {
                Assert.That(contention, Is.Not.Null);
                Assert.That(
                    contention!.Kind,
                    Is.EqualTo(SkinManagedFolderOperationCoordinator.SelectionContentionKind.StartupSequence));
                Assert.That(contention.Completion.IsCompletedSuccessfully, Is.True);
            });
        }

        [Test]
        public void TestMutationCrossingSuppressesCompletedStartupContention()
        {
            var coordinator = new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation observation =
                coordinator.CaptureSelectionPreparationObservation();

            using (coordinator.EnterStartupSequence())
            {
            }

            using (coordinator.EnterMutation())
            {
                Assert.That(
                    coordinator.TryGetRetryableContentionSince(observation),
                    Is.Null);
            }

            Assert.That(
                coordinator.TryGetRetryableContentionSince(observation),
                Is.Null);
        }
    }
}
