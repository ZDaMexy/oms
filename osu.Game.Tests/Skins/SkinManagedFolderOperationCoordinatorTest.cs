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
    }
}
