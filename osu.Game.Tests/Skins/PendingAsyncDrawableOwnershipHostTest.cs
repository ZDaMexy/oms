// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Skins
{
    [HeadlessTest]
    [TestFixture]
    public partial class PendingAsyncDrawableOwnershipHostTest : OsuTestScene
    {
        [Test]
        public void TestParentDisposeJoinsRealBackgroundLoadBeforeExactlyOnceReclaim()
        {
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var target = new GatedTrackingDrawable(entered, release);
            OwnershipHost host = null!;
            Task disposal = null!;

            AddStep("mount ownership host", () => Add(host = new OwnershipHost()));
            AddUntilStep("wait for host load", () => host.IsLoaded);
            AddStep("start real framework background load", () => host.Begin(target));
            AddUntilStep("wait for target BDL gate", () => entered.IsSet);
            AddStep("detach host without disposal", () => Assert.That(Remove(host, disposeImmediately: false), Is.True));
            AddStep("dispose parent away from update thread", () => disposal = Task.Run(host.Dispose));
            AddWaitStep("hold blocked disposal", 5);
            AddStep("assert loader still owns target and work", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(disposal.IsCompleted, Is.False,
                        "CompositeDrawable.Dispose must wait on the exact loading wrapper LoadLock.");
                    Assert.That(target.DisposeCount, Is.Zero);
                    Assert.That(host.OwnershipResolvedCount, Is.Zero);
                });

                release.Set();
            });
            AddUntilStep("wait for parent disposal join", () => disposal.IsCompleted);
            AddStep("assert exactly-once reclaim after loader stop", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(disposal.IsCompletedSuccessfully, Is.True);
                    Assert.That(host.LoadTask?.IsCompleted, Is.True);
                    Assert.That(target.DisposeCount, Is.EqualTo(1));
                    Assert.That(host.OwnershipResolvedCount, Is.EqualTo(1));
                });

                host.Dispose();

                Assert.Multiple(() =>
                {
                    Assert.That(target.DisposeCount, Is.EqualTo(1));
                    Assert.That(host.OwnershipResolvedCount, Is.EqualTo(1));
                });

                entered.Dispose();
                release.Dispose();
            });
        }

        [Test]
        public void TestScreenTargetTransfersFromScreenStackWrapper()
        {
            ScreenOwnershipHost host = null!;
            var target = new TrackingScreen();

            AddStep("mount screen ownership host", () => Add(host = new ScreenOwnershipHost()));
            AddUntilStep("wait for screen host", () => host.IsLoaded);
            AddStep("begin screen target load", () => host.Begin(target));
            AddUntilStep("wait for screen transfer", () => ReferenceEquals(host.Transferred, target));
            AddStep("assert screen transfer ownership", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(target.DisposeCount, Is.Zero);
                    Assert.That(host.OwnershipResolvedCount, Is.EqualTo(1));
                });

                target.Dispose();
                Assert.That(target.DisposeCount, Is.EqualTo(1));
                Remove(host, disposeImmediately: true);
            });
        }

        [Test]
        public void TestScreenTargetCancellationWaitsForRealBackgroundLoad()
        {
            var entered = new ManualResetEventSlim();
            var release = new ManualResetEventSlim();
            var target = new GatedTrackingScreen(entered, release);
            ScreenOwnershipHost host = null!;

            AddStep("mount gated screen ownership host", () => Add(host = new ScreenOwnershipHost()));
            AddUntilStep("wait for gated screen host", () => host.IsLoaded);
            AddStep("begin gated screen target load", () => host.Begin(target));
            AddUntilStep("wait for screen BDL gate", () => entered.IsSet);
            AddStep("cancel screen ownership while loading", () => host.CancelPending());
            AddStep("assert screen retained by loader", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(target.DisposeCount, Is.Zero);
                    Assert.That(host.OwnershipResolvedCount, Is.Zero);
                });

                release.Set();
            });
            AddUntilStep("wait for cancelled screen reclaim", () =>
                target.DisposeCount == 1 && host.OwnershipResolvedCount == 1);
            AddStep("assert cancelled screen exactly once", () =>
            {
                Remove(host, disposeImmediately: true);
                Assert.Multiple(() =>
                {
                    Assert.That(target.DisposeCount, Is.EqualTo(1));
                    Assert.That(host.OwnershipResolvedCount, Is.EqualTo(1));
                    Assert.That(host.Transferred, Is.Null);
                });

                entered.Dispose();
                release.Dispose();
            });
        }

        private sealed partial class OwnershipHost : CompositeDrawable
        {
            private PendingAsyncDrawableOwnership<GatedTrackingDrawable>? pendingOwnership;
            private CancellationTokenSource? cancellation;
            private Task? lastLoadTask;
            private int ownershipResolvedCount;

            internal int OwnershipResolvedCount => Volatile.Read(ref ownershipResolvedCount);

            internal Task? LoadTask => Volatile.Read(ref lastLoadTask);

            internal void Begin(GatedTrackingDrawable target)
            {
                var ownership = new PendingAsyncDrawableOwnership<GatedTrackingDrawable>(
                    target,
                    () => Interlocked.Increment(ref ownershipResolvedCount));
                var localCancellation = new CancellationTokenSource();
                pendingOwnership = ownership;
                cancellation = localCancellation;

                try
                {
                    Task task = LoadComponentAsync(
                        ownership.Loadable,
                        loaded =>
                        {
                            if (!ownership.TryTransfer(loaded, out GatedTrackingDrawable? transferred))
                                return;

                            try
                            {
                                InternalChild = transferred!;
                            }
                            finally
                            {
                                ownership.CompleteTransfer();
                            }
                        },
                        localCancellation.Token);
                    Volatile.Write(ref lastLoadTask, task);
                    ownership.Attach(task, Scheduler);
                }
                catch
                {
                    ownership.ReclaimUnstarted();
                    throw;
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                PendingAsyncDrawableOwnership<GatedTrackingDrawable>? ownership =
                    Interlocked.Exchange(ref pendingOwnership, null);
                ownership?.Cancel();

                CancellationTokenSource? localCancellation = Interlocked.Exchange(ref cancellation, null);

                if (localCancellation != null)
                {
                    try
                    {
                        localCancellation.Cancel();
                    }
                    finally
                    {
                        localCancellation.Dispose();
                    }
                }

                base.Dispose(isDisposing);
                ownership?.JoinAfterParentDisposal();
            }
        }

        private sealed partial class GatedTrackingDrawable : Drawable
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            internal GatedTrackingDrawable(ManualResetEventSlim entered, ManualResetEventSlim release)
            {
                this.entered = entered;
                this.release = release;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
            }

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class ScreenOwnershipHost : CompositeDrawable
        {
            private PendingAsyncDrawableOwnership<TrackingScreen>? pendingOwnership;
            private CancellationTokenSource? cancellation;
            private int ownershipResolvedCount;

            internal TrackingScreen? Transferred { get; private set; }

            internal int OwnershipResolvedCount => Volatile.Read(ref ownershipResolvedCount);

            internal void Begin(TrackingScreen target)
            {
                var ownership = new PendingAsyncDrawableOwnership<TrackingScreen>(
                    target,
                    () => Interlocked.Increment(ref ownershipResolvedCount));
                var localCancellation = new CancellationTokenSource();
                pendingOwnership = ownership;
                cancellation = localCancellation;

                try
                {
                    Task task = LoadComponentAsync(
                        ownership.Loadable,
                        loaded =>
                        {
                            if (!ownership.TryTransfer(loaded, out TrackingScreen? transferred))
                                return;

                            try
                            {
                                Transferred = transferred;
                            }
                            finally
                            {
                                ownership.CompleteTransfer();
                            }
                        },
                        localCancellation.Token);
                    ownership.Attach(task, Scheduler);
                }
                catch
                {
                    ownership.ReclaimUnstarted();
                    throw;
                }
            }

            internal void CancelPending()
            {
                pendingOwnership?.Cancel();
                cancellation?.Cancel();
            }

            protected override void Dispose(bool isDisposing)
            {
                PendingAsyncDrawableOwnership<TrackingScreen>? ownership =
                    Interlocked.Exchange(ref pendingOwnership, null);
                ownership?.Cancel();

                CancellationTokenSource? localCancellation = Interlocked.Exchange(ref cancellation, null);

                if (localCancellation != null)
                {
                    try
                    {
                        localCancellation.Cancel();
                    }
                    finally
                    {
                        localCancellation.Dispose();
                    }
                }

                base.Dispose(isDisposing);
                ownership?.JoinAfterParentDisposal();
            }
        }

        private partial class TrackingScreen : Screen
        {
            private int disposeCount;

            internal int DisposeCount => Volatile.Read(ref disposeCount);

            protected override void Dispose(bool isDisposing)
            {
                Interlocked.Increment(ref disposeCount);
                base.Dispose(isDisposing);
            }
        }

        private sealed partial class GatedTrackingScreen : TrackingScreen
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;

            internal GatedTrackingScreen(ManualResetEventSlim entered, ManualResetEventSlim release)
            {
                this.entered = entered;
                this.release = release;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                entered.Set();
                release.Wait(TimeSpan.FromSeconds(30));
            }
        }
    }
}
