// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Threading;
using osu.Game.Graphics;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public partial class PendingAsyncDrawableOwnershipTest
    {
        [Test]
        public void TestWorkerCompletionBeforeScheduledCallbackStillTransfers()
        {
            var scheduler = new Scheduler();
            var drawable = new TrackingDrawable();
            int ownershipResolved = 0;
            var ownership = new PendingAsyncDrawableOwnership<TrackingDrawable>(
                drawable,
                () => Interlocked.Increment(ref ownershipResolved));
            var worker = new TaskCompletionSource();
            TrackingDrawable? transferred = null;

            // Framework enqueues its callback before the task returned from LoadComponentAsync completes.
            scheduler.Add(() => Assert.That(ownership.TryTransfer(ownership.Loadable, out transferred), Is.True));
            ownership.Attach(worker.Task, scheduler);
            worker.SetResult();
            Assert.That(worker.Task.Wait(TimeSpan.FromSeconds(10)), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(drawable.DisposeCount, Is.Zero);
                Assert.That(transferred, Is.Null);
            });

            scheduler.Update();

            Assert.Multiple(() =>
            {
                Assert.That(transferred, Is.SameAs(drawable));
                Assert.That(drawable.DisposeCount, Is.Zero,
                    "The post-callback sentinel must not reclaim an ownership transfer.");
                Assert.That(ownershipResolved, Is.Zero,
                    "Lifetime work must remain retained until the caller has installed formal ownership.");
            });

            ownership.CompleteTransfer();
            transferred!.Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(drawable.DisposeCount, Is.EqualTo(1));
                Assert.That(ownershipResolved, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestExplicitCancelWaitsForWorkerThenReclaimsExactlyOnce()
        {
            var scheduler = new Scheduler();
            var drawable = new TrackingDrawable();
            var ownership = new PendingAsyncDrawableOwnership<TrackingDrawable>(drawable);
            var worker = new TaskCompletionSource();

            ownership.Attach(worker.Task, scheduler);
            ownership.Cancel();
            ownership.Cancel();
            Assert.That(drawable.DisposeCount, Is.Zero,
                "An in-flight loader must stop before its provisional drawable is reclaimed.");

            worker.SetResult();
            SpinWait.SpinUntil(() => drawable.DisposeCount != 0, TimeSpan.FromSeconds(10));

            ownership.Cancel();
            scheduler.Update();
            Assert.That(drawable.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestSkippedCallbackSentinelReclaimsExactlyOnce()
        {
            var scheduler = new Scheduler();
            var drawable = new TrackingDrawable();
            var ownership = new PendingAsyncDrawableOwnership<TrackingDrawable>(drawable);
            var worker = new TaskCompletionSource();

            scheduler.Add(() => { });
            ownership.Attach(worker.Task, scheduler);
            worker.SetResult();
            scheduler.Update();

            Assert.That(drawable.DisposeCount, Is.EqualTo(1));
            ownership.Cancel();
            Assert.That(drawable.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestThrowingFrameworkCallbackStillReclaimsExactlyOnce()
        {
            var scheduler = new Scheduler();
            var drawable = new TrackingDrawable();
            var ownership = new PendingAsyncDrawableOwnership<TrackingDrawable>(drawable);
            var worker = new TaskCompletionSource();

            scheduler.Add(() => throw new InvalidOperationException("synthetic load callback fault"));
            ownership.Attach(worker.Task, scheduler);
            worker.SetResult();

            Assert.That(() => scheduler.Update(), Throws.TypeOf<InvalidOperationException>());
            scheduler.Update();

            Assert.That(drawable.DisposeCount, Is.EqualTo(1));
            ownership.Cancel();
            Assert.That(drawable.DisposeCount, Is.EqualTo(1));
        }

        [Test]
        public void TestFrameworkParentDisposalRetainsOuterWorkUntilTaskCompletion()
        {
            var scheduler = new Scheduler();
            var drawable = new TrackingDrawable();
            int ownershipResolved = 0;
            var ownership = new PendingAsyncDrawableOwnership<TrackingDrawable>(
                drawable,
                () => Interlocked.Increment(ref ownershipResolved));
            var worker = new TaskCompletionSource();

            ownership.Attach(worker.Task, scheduler);

            // CompositeDrawable.Dispose() may directly dispose an item still present in loadingComponents.
            ownership.Loadable.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.DisposeCount, Is.EqualTo(1));
                Assert.That(ownershipResolved, Is.Zero,
                    "The wrapper target is resource-free in this synthetic test, but outer lifetime work must still await the exact task.");
            });

            worker.SetResult();
            scheduler.Update();
            ownership.Cancel();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.DisposeCount, Is.EqualTo(1));
                Assert.That(ownershipResolved, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestJoinClosesLoadedButNotYetReturnedTaskWindow()
        {
            var scheduler = new Scheduler();
            var drawable = new TrackingDrawable();
            int ownershipResolved = 0;
            var ownership = new PendingAsyncDrawableOwnership<TrackingDrawable>(
                drawable,
                () => Interlocked.Increment(ref ownershipResolved));
            var worker = new TaskCompletionSource();

            // Model OnLoadComplete having removed the wrapper from the parent's loading set while the returned task
            // and its queued callback are still unresolved from the caller's point of view.
            scheduler.Add(() => Assert.That(ownership.TryTransfer(ownership.Loadable, out _), Is.False));
            ownership.Attach(worker.Task, scheduler);
            ownership.Cancel();

            Task join = Task.Run(ownership.JoinAfterParentDisposal);
            Assert.That(join.Wait(TimeSpan.FromMilliseconds(100)), Is.False,
                "The ownership join must retain the target until the exact worker task has stopped.");

            worker.SetResult();
            Assert.That(join.Wait(TimeSpan.FromSeconds(10)), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(drawable.DisposeCount, Is.EqualTo(1));
                Assert.That(ownershipResolved, Is.EqualTo(1));
            });

            scheduler.Update();

            Assert.Multiple(() =>
            {
                Assert.That(drawable.DisposeCount, Is.EqualTo(1));
                Assert.That(ownershipResolved, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestLongRunningTargetRejectedBeforeOwnershipIsAccepted()
        {
            var drawable = new LongRunningTrackingDrawable();

            Assert.That(
                () => new PendingAsyncDrawableOwnership<LongRunningTrackingDrawable>(drawable),
                Throws.ArgumentException);
            Assert.That(drawable.DisposeCount, Is.Zero,
                "A fail-fast constructor must leave the rejected target with its original caller.");

            drawable.Dispose();
            Assert.That(drawable.DisposeCount, Is.EqualTo(1));
        }

        private partial class TrackingDrawable : Drawable
        {
            private int disposeCount;

            public int DisposeCount => Volatile.Read(ref disposeCount);

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        [LongRunningLoad]
        private sealed partial class LongRunningTrackingDrawable : TrackingDrawable
        {
        }
    }
}
