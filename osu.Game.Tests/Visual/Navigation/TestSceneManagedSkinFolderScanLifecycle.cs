// Copyright (c) OMS contributors. Licensed under the MIT Licence.

#nullable disable

using System;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Development;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Online.API;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    [HeadlessTest]
    public partial class TestSceneManagedSkinFolderScanLifecycle : OsuGameTestScene
    {
        private ManagedSkinFolderScanLifecycleTestGame lifecycleGame;

        protected override TestOsuGame CreateTestGame()
            => lifecycleGame = new ManagedSkinFolderScanLifecycleTestGame(LocalStorage, API);

        public override void SetUpSteps()
        {
            AddStep("create game", () =>
            {
                RecycleLocalStorage(false);
                CreateGame();
            });

            AddUntilStep("wait for load", () => Game.IsLoaded);
        }

        public override void TearDownSteps()
        {
            AddStep("release scan worker", () => lifecycleGame?.AllowScanCompletion.Set());
            AddStep("exit game", () => lifecycleGame?.Exit());
            AddUntilStep("wait for disposal", () => lifecycleGame == null || lifecycleGame.DisposeCompleted.IsSet);
            AddStep("release lifecycle resources", releaseTestResources);
        }

        [Test]
        public void TestScannerStartsOnceAndIsJoinedBeforeRealmDisposal()
        {
            AddUntilStep("managed recovery completed", () => lifecycleGame.RecoveryCompleted.IsSet);
            AddUntilStep("managed scan started", () => lifecycleGame.ScanStarted.IsSet);
            AddAssert("load completed on update thread", () => lifecycleGame.LoadCompleteRanOnUpdateThread);
            AddAssert("managed recovery invoked once", () => lifecycleGame.RecoveryInvocationCount == 1);
            AddAssert("managed recovery runs off update thread", () => !lifecycleGame.RecoveryRanOnUpdateThread);
            AddAssert("recovery completed before scan", () => lifecycleGame.RecoveryCompletedBeforeScan);
            AddAssert("managed scan invoked once", () => lifecycleGame.ScanInvocationCount == 1);
            AddAssert("managed scan runs off update thread", () => !lifecycleGame.ScanRanOnUpdateThread);
            AddAssert("managed scan uses a different thread", () => lifecycleGame.ScanThreadId != lifecycleGame.LoadCompleteThreadId);
            AddAssert("managed scan owns startup sequence", () => lifecycleGame.ScanOwnedStartupSequence);

            AddStep("arm worker completion gate", () => lifecycleGame.HoldAfterCancellation = true);
            AddStep("start disposal observer", () => lifecycleGame.StartDisposalObserver());
            AddStep("begin disposal", () => lifecycleGame.Exit());

            AddUntilStep("disposal observer completes", () => lifecycleGame.DisposalObserverCompleted.IsSet);
            AddAssert("scan observes disposal cancellation", () => lifecycleGame.CancellationObserved.IsSet);
            AddAssert("dispose entered", () => lifecycleGame.DisposeStarted.IsSet);
            AddAssert("dispose waited for scan worker", () => lifecycleGame.DisposeWasPendingWhenCancellationObserved);
            AddAssert("disposal observer did not time out", () => !lifecycleGame.DisposalObserverWaitTimedOut);
            AddAssert("dispose completes", () => lifecycleGame.DisposeCompleted.IsSet);

            AddAssert("managed scan still invoked once", () => lifecycleGame.ScanInvocationCount == 1);
            AddAssert("scan worker completed before dispose returned", () => lifecycleGame.ScanCompleted.IsSet);
            AddAssert("cancellation was requested", () => lifecycleGame.CancellationWasRequested);
            AddAssert("cancellation wait did not time out", () => !lifecycleGame.CancellationWaitTimedOut);
            AddAssert("completion gate wait did not time out", () => !lifecycleGame.CompletionGateWaitTimedOut);
            AddAssert("client realm remained accessible in worker finally", () => lifecycleGame.ClientRealmAccessibleInFinally);
            AddAssert("client realm access did not fail", () => lifecycleGame.ClientRealmAccessException == null);

            AddStep("release test resources", releaseTestResources);
        }

        private void releaseTestResources()
        {
            lifecycleGame?.DisposeTestResources();
            lifecycleGame = null;
            Game = null;
        }

        private partial class ManagedSkinFolderScanLifecycleTestGame : TestOsuGame
        {
            private static readonly TimeSpan coordination_timeout = TimeSpan.FromSeconds(30);
            private static readonly TimeSpan join_observation_window = TimeSpan.FromMilliseconds(500);

            public readonly ManualResetEventSlim ScanStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim RecoveryStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim RecoveryCompleted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim CancellationObserved = new ManualResetEventSlim();
            public readonly ManualResetEventSlim AllowScanCompletion = new ManualResetEventSlim();
            public readonly ManualResetEventSlim ScanCompleted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim DisposeStarted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim DisposeCompleted = new ManualResetEventSlim();
            public readonly ManualResetEventSlim DisposalObserverCompleted = new ManualResetEventSlim();

            private int scanInvocationCount;
            private int recoveryInvocationCount;
            private bool loadCompleteRanOnUpdateThread;
            private bool recoveryRanOnUpdateThread;
            private bool recoveryCompletedBeforeScan;
            private bool scanRanOnUpdateThread;
            private bool scanOwnedStartupSequence;
            private bool cancellationWasRequested;
            private bool cancellationWaitTimedOut;
            private bool completionGateWaitTimedOut;
            private bool clientRealmAccessibleInFinally;
            private Exception clientRealmAccessException;
            private int holdAfterCancellation;
            private bool disposeWasPendingWhenCancellationObserved;
            private bool disposalObserverWaitTimedOut;

            public int LoadCompleteThreadId { get; private set; }
            public int ScanThreadId { get; private set; }

            public int ScanInvocationCount => Volatile.Read(ref scanInvocationCount);
            public int RecoveryInvocationCount => Volatile.Read(ref recoveryInvocationCount);
            public bool LoadCompleteRanOnUpdateThread => Volatile.Read(ref loadCompleteRanOnUpdateThread);
            public bool RecoveryRanOnUpdateThread => Volatile.Read(ref recoveryRanOnUpdateThread);
            public bool RecoveryCompletedBeforeScan => Volatile.Read(ref recoveryCompletedBeforeScan);
            public bool ScanRanOnUpdateThread => Volatile.Read(ref scanRanOnUpdateThread);
            public bool ScanOwnedStartupSequence => Volatile.Read(ref scanOwnedStartupSequence);
            public bool CancellationWasRequested => Volatile.Read(ref cancellationWasRequested);
            public bool CancellationWaitTimedOut => Volatile.Read(ref cancellationWaitTimedOut);
            public bool CompletionGateWaitTimedOut => Volatile.Read(ref completionGateWaitTimedOut);
            public bool ClientRealmAccessibleInFinally => Volatile.Read(ref clientRealmAccessibleInFinally);
            public Exception ClientRealmAccessException => Volatile.Read(ref clientRealmAccessException);
            public bool DisposeWasPendingWhenCancellationObserved => Volatile.Read(ref disposeWasPendingWhenCancellationObserved);
            public bool DisposalObserverWaitTimedOut => Volatile.Read(ref disposalObserverWaitTimedOut);

            public bool HoldAfterCancellation
            {
                set => Volatile.Write(ref holdAfterCancellation, value ? 1 : 0);
            }

            public ManagedSkinFolderScanLifecycleTestGame(Storage storage, IAPIProvider api)
                : base(storage, api)
            {
            }

            public void StartDisposalObserver()
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        if (!CancellationObserved.Wait(coordination_timeout))
                        {
                            disposalObserverWaitTimedOut = true;
                            return;
                        }

                        disposeWasPendingWhenCancellationObserved = !DisposeCompleted.Wait(join_observation_window);
                    }
                    finally
                    {
                        AllowScanCompletion.Set();
                        DisposalObserverCompleted.Set();
                    }
                });
            }

            protected override void LoadComplete()
            {
                LoadCompleteThreadId = Environment.CurrentManagedThreadId;
                loadCompleteRanOnUpdateThread = ThreadSafety.IsUpdateThread;

                base.LoadComplete();
            }

            protected override void PerformManagedSkinFolderScan(CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref scanInvocationCount);
                recoveryCompletedBeforeScan = RecoveryCompleted.IsSet;
                ScanThreadId = Environment.CurrentManagedThreadId;
                scanRanOnUpdateThread = ThreadSafety.IsUpdateThread;
                scanOwnedStartupSequence = SkinManager.ManagedFolderOperationCoordinator.IsStartupSequenceHeldByCurrentThread;
                ScanStarted.Set();

                try
                {
                    if (!cancellationToken.WaitHandle.WaitOne(coordination_timeout))
                    {
                        cancellationWaitTimedOut = true;
                        return;
                    }

                    cancellationWasRequested = cancellationToken.IsCancellationRequested;
                    CancellationObserved.Set();

                    if (Volatile.Read(ref holdAfterCancellation) == 1 && !AllowScanCompletion.Wait(coordination_timeout))
                        completionGateWaitTimedOut = true;

                    cancellationToken.ThrowIfCancellationRequested();
                }
                finally
                {
                    try
                    {
                        ClientRealm.Run(_ => { });
                        clientRealmAccessibleInFinally = true;
                    }
                    catch (Exception exception)
                    {
                        clientRealmAccessException = exception;
                    }
                    finally
                    {
                        ScanCompleted.Set();
                    }
                }
            }

            protected override void PerformManagedSkinFolderMutationRecovery(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Interlocked.Increment(ref recoveryInvocationCount);
                recoveryRanOnUpdateThread = ThreadSafety.IsUpdateThread;
                RecoveryStarted.Set();
                RecoveryCompleted.Set();
            }

            protected override void Dispose(bool isDisposing)
            {
                DisposeStarted.Set();

                try
                {
                    base.Dispose(isDisposing);
                }
                finally
                {
                    DisposeCompleted.Set();
                }
            }

            public void DisposeTestResources()
            {
                ScanStarted.Dispose();
                RecoveryStarted.Dispose();
                RecoveryCompleted.Dispose();
                CancellationObserved.Dispose();
                AllowScanCompletion.Dispose();
                ScanCompleted.Dispose();
                DisposeStarted.Dispose();
                DisposeCompleted.Dispose();
                DisposalObserverCompleted.Dispose();
            }
        }
    }
}
