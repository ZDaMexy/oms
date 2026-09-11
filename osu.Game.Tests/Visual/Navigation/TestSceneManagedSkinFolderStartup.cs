// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Development;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Online.API;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Navigation
{
    [TestFixture]
    [HeadlessTest]
    [Platform("Win")]
    public partial class TestSceneManagedSkinFolderStartup : OsuGameTestScene
    {
        private StartupTestGame startupGame = null!;

        protected override TestOsuGame CreateTestGame()
            => startupGame = new StartupTestGame(LocalStorage, API);

        public override void SetUpSteps()
        {
            // Each case prepares its fresh, owned installation before starting the real worker.
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestProductionRecoveryAndDiscoveryCompleteBeforeNormalDisposal(bool hasManagedPackage)
        {
            AddStep("prepare fresh installation and start game", () =>
            {
                RecycleLocalStorage(false);
                if (hasManagedPackage)
                {
                    string packageRoot = LocalStorage.GetFullPath("chartskin/startup-author");
                    Directory.CreateDirectory(packageRoot);
                    File.WriteAllText(Path.Combine(packageRoot, "skin.ini"), "[General]\nName: Startup author\nAuthor: OMS tests\n");
                }
                CreateGame();
            });
            AddUntilStep("game is loaded", () => Game.IsLoaded);
            AddUntilStep("real recovery attempt completed", () => startupGame.RecoveryCompleted);
            AddAssert("real recovery did not throw", () => startupGame.RecoveryFailure, () => Is.Null);
            AddUntilStep("real discovery completed", () => startupGame.ScanCompleted);
            AddAssert("real discovery did not throw", () => startupGame.ScanFailure, () => Is.Null);
            AddAssert("recovery and discovery share startup ownership off update thread", () =>
                startupGame.RecoveryOwnedStartup && startupGame.ScanOwnedStartup
                                                && !startupGame.RecoveryOnUpdateThread && !startupGame.ScanOnUpdateThread);
            AddAssert("real discovery preserves fresh root or registers ordinary author package", () =>
            {
                SkinInfo[] records = startupGame.Realm.Run(r => r.All<SkinInfo>()
                    .Where(s => s.FilesystemStoragePath == "chartskin/startup-author").ToArray());
                if (!hasManagedPackage)
                    return records.Length == 0 && !Directory.Exists(LocalStorage.GetFullPath("chartskin"));
                return records.Length == 1 && records[0].Name == "Startup author"
                                           && records[0].FilesystemStorageAuthorityOwner == SkinManagedFolderScanner.AUTHORITY_OWNER
                                           && !records[0].DeletePending;
            });
            AddStep("exit completed startup", () => startupGame.Exit());
            AddUntilStep("game disposal completed", () => startupGame.DisposeCompleted);
            AddAssert("startup completed without an exception deferred until disposal", () =>
                startupGame.RecoveryFailure == null && startupGame.ScanFailure == null);
        }

        private partial class StartupTestGame : TestOsuGame
        {
            private int recoveryCompleted;
            private int scanCompleted;
            private int disposeCompleted;

            public bool RecoveryCompleted => Volatile.Read(ref recoveryCompleted) == 1;
            public bool ScanCompleted => Volatile.Read(ref scanCompleted) == 1;
            public bool DisposeCompleted => Volatile.Read(ref disposeCompleted) == 1;
            public bool RecoveryOwnedStartup { get; private set; }
            public bool ScanOwnedStartup { get; private set; }
            public bool RecoveryOnUpdateThread { get; private set; }
            public bool ScanOnUpdateThread { get; private set; }
            public Exception? RecoveryFailure { get; private set; }
            public Exception? ScanFailure { get; private set; }

            public StartupTestGame(Storage storage, IAPIProvider api)
                : base(storage, api)
            {
            }

            protected override void PerformManagedSkinFolderMutationRecovery(CancellationToken cancellationToken)
            {
                RecoveryOwnedStartup = SkinManager.ManagedFolderOperationCoordinator.IsStartupSequenceHeldByCurrentThread;
                RecoveryOnUpdateThread = ThreadSafety.IsUpdateThread;
                try
                {
                    base.PerformManagedSkinFolderMutationRecovery(cancellationToken);
                }
                catch (Exception exception)
                {
                    RecoveryFailure = exception;
                    throw;
                }
                finally
                {
                    Volatile.Write(ref recoveryCompleted, 1);
                }
            }

            protected override void PerformManagedSkinFolderScan(CancellationToken cancellationToken)
            {
                ScanOwnedStartup = SkinManager.ManagedFolderOperationCoordinator.IsStartupSequenceHeldByCurrentThread;
                ScanOnUpdateThread = ThreadSafety.IsUpdateThread;
                try
                {
                    base.PerformManagedSkinFolderScan(cancellationToken);
                }
                catch (Exception exception)
                {
                    ScanFailure = exception;
                    throw;
                }
                finally
                {
                    Volatile.Write(ref scanCompleted, 1);
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                try
                {
                    base.Dispose(isDisposing);
                }
                finally
                {
                    Volatile.Write(ref disposeCompleted, 1);
                }
            }
        }
    }
}
