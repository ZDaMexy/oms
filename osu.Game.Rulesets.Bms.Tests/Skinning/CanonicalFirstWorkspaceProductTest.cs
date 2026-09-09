// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        private readonly List<FirstWorkspaceFixture> firstWorkspaceFixtures = new List<FirstWorkspaceFixture>();

        [TearDownSteps]
        public void TearDownFirstWorkspaceFixtures()
            => AddStep("close first-install author workspace fixtures", disposeFirstWorkspaceFixtures);

        [TearDown]
        public void CleanUpFirstWorkspaceFixtures() => disposeFirstWorkspaceFixtures();

        private void disposeFirstWorkspaceFixtures()
        {
            foreach (FirstWorkspaceFixture fixture in firstWorkspaceFixtures)
                fixture.Dispose();
            firstWorkspaceFixtures.Clear();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestFirstExternalRegistrationCreatesOwnedRootWithoutJournalOrAuthorWrites(bool authorInsideDataRoot)
        {
            FirstWorkspaceFixture fixture = null!;
            Task<bool>? registration = null;
            AddStep("open a fresh installation with no managed skin directory", () =>
            {
                fixture = createFirstWorkspaceFixture(authorInsideDataRoot: authorInsideDataRoot);
                Assert.That(Directory.Exists(fixture.ManagedRoot), Is.False);
                Assert.That(fixture.Manager.InitialManagedFolderMutationRecoveryResult.IsResolved, Is.True);
                registration = fixture.Manager.RegisterExternalFolderAsync(fixture.AuthorRoot);
            });
            AddUntilStep("first author registration completes", () => registration?.IsCompleted == true);
            AddStep("the author is registered and all original bytes remain intact", () =>
            {
                Assert.That(registration!.GetAwaiter().GetResult(), Is.True);
                Assert.That(Directory.Exists(fixture.ManagedRoot), Is.True);
                Assert.That(Directory.GetFileSystemEntries(fixture.ManagedRoot), Is.Empty);
                Assert.That(File.Exists(fixture.JournalPath), Is.False);
                Assert.That(fixture.Realm.Run(realm => realm.All<SkinInfo>().Count(info => info.IsExternalFilesystemStorage)), Is.EqualTo(1));
                fixture.AssertAuthorUnchanged();
            });
            AddStep("select the registered author package", () => fixture.Manager.CurrentSkinInfo.Value =
                fixture.Manager.Query(info => info.IsExternalFilesystemStorage)!);
            AddUntilStep("the real external package can be selected", () => fixture.Manager.CurrentSkin.Value.SkinInfo.Value.IsExternalFilesystemStorage);
            AddStep("selection also leaves every author file and directory untouched", () => fixture.AssertAuthorUnchanged());
            AddAssert("first registration does not manufacture recovery evidence", () => !File.Exists(fixture.JournalPath));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMissingManagedRootWithOldFilesystemEvidenceIsNeverRecreated(bool unknown)
        {
            FirstWorkspaceFixture fixture = null!;
            Guid oldId = Guid.NewGuid();
            string oldPath = unknown ? "unknown-old-author-location" : "chartskin/missing-author";
            Task<bool>? registration = null;
            AddStep("open preserved historical filesystem evidence without its directory", () =>
            {
                fixture = createFirstWorkspaceFixture((storage, realm) => realm.Write(r => r.Add(new SkinInfo
                {
                    ID = oldId,
                    Name = "Preserved pre-existing author record",
                    FilesystemStoragePath = oldPath,
                    FilesystemStorageAuthorityOwner = unknown ? null : SkinManagedFolderScanner.AUTHORITY_OWNER,
                    InstantiationInfo = SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO,
                })));
                registration = fixture.Manager.RegisterExternalFolderAsync(fixture.AuthorRoot);
            });
            AddUntilStep("registration refuses missing historical data", () => registration?.IsCompleted == true);
            AddStep("the old record and absent root remain exact", () =>
            {
                Assert.That(registration!.GetAwaiter().GetResult(), Is.False);
                Assert.That(Directory.Exists(fixture.ManagedRoot), Is.False);
                Assert.That(File.Exists(fixture.JournalPath), Is.False);
                Assert.That(fixture.Realm.Run(realm => realm.Find<SkinInfo>(oldId)!.FilesystemStoragePath), Is.EqualTo(oldPath));
                Assert.That(fixture.Realm.Run(realm => realm.Find<SkinInfo>(oldId)!.DeletePending), Is.False);
                Assert.That(fixture.Manager.LastFolderWorkspaceRepairMessage, Does.Contain("从备份恢复原皮肤目录"));
                fixture.AssertAuthorUnchanged();
            });
        }

        [Test]
        public void TestUnresolvedFirstWorkspaceJournalCannotCreateManagedRoot()
        {
            FirstWorkspaceFixture fixture = null!;
            Task<bool>? registration = null;
            AddStep("open an interrupted installation with unrecognised evidence", () =>
            {
                fixture = createFirstWorkspaceFixture((storage, _) => File.WriteAllText(
                    storage.GetFullPath(SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME), "{}"));
                Assert.That(fixture.Manager.InitialManagedFolderMutationRecoveryResult.IsResolved, Is.False);
                registration = fixture.Manager.RegisterExternalFolderAsync(fixture.AuthorRoot);
            });
            AddUntilStep("frozen first registration finishes", () => registration?.IsCompleted == true);
            AddStep("unknown evidence stays untouched and no replacement directory appears", () =>
            {
                Assert.That(registration!.GetAwaiter().GetResult(), Is.False);
                Assert.That(Directory.Exists(fixture.ManagedRoot), Is.False);
                Assert.That(File.ReadAllText(fixture.JournalPath), Is.EqualTo("{}"));
                Assert.That(fixture.Realm.Run(realm => realm.Find<SkinInfo>(SkinInfo.OMS_SKIN)), Is.Null);
                fixture.AssertAuthorUnchanged();
            });
        }

        [Test]
        public void TestFirstWorkspaceJunctionNeverWritesIntoAuthorDirectory()
        {
            FirstWorkspaceFixture fixture = null!;
            Task<bool>? registration = null;
            AddStep("place an author-directory junction in the managed root slot", () =>
            {
                fixture = createFirstWorkspaceFixture();
                string script = fixture.Storage.GetFullPath("create-junction.ps1");
                File.WriteAllText(script, "param([string]$linkPath, [string]$targetPath)\n$null = New-Item -ItemType Junction -Path $linkPath -Target $targetPath -ErrorAction Stop\n");
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                    },
                };
                foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", script, fixture.ManagedRoot, fixture.AuthorRoot })
                    process.StartInfo.ArgumentList.Add(argument);
                Assert.That(process.Start(), Is.True);
                process.WaitForExit();
                Assert.That(process.ExitCode, Is.Zero, process.StandardError.ReadToEnd());
                registration = fixture.Manager.RegisterExternalFolderAsync(fixture.AuthorRoot);
            });
            AddUntilStep("linked managed root is refused", () => registration?.IsCompleted == true);
            AddStep("the author directory and link remain unchanged", () =>
            {
                Assert.That(registration!.GetAwaiter().GetResult(), Is.False);
                Assert.That(File.GetAttributes(fixture.ManagedRoot).HasFlag(FileAttributes.ReparsePoint), Is.True);
                Assert.That(File.Exists(fixture.JournalPath), Is.False);
                fixture.AssertAuthorUnchanged();
            });
        }

        private FirstWorkspaceFixture createFirstWorkspaceFixture(Action<Storage, RealmAccess>? seed = null, bool authorInsideDataRoot = false)
        {
            Storage storage = LocalStorage.GetStorageForDirectory($"first author workspace {Guid.NewGuid():N}");
            var realm = new RealmAccess(storage, "client");
            seed?.Invoke(storage, realm);
            string authorRoot;
            if (authorInsideDataRoot)
            {
                authorRoot = storage.GetFullPath("author-source");
                Directory.CreateDirectory(authorRoot);
                createCompletePackage(authorRoot);
            }
            else
                authorRoot = createExternalPackage(createCompletePackage);
            var fixture = new FirstWorkspaceFixture(storage, realm,
                new SkinManager(storage, realm, host, Resources, Audio, Scheduler), authorRoot);
            firstWorkspaceFixtures.Add(fixture);
            return fixture;
        }

        private sealed class FirstWorkspaceFixture : IDisposable
        {
            public Storage Storage { get; }
            public RealmAccess Realm { get; }
            public SkinManager Manager { get; }
            public string AuthorRoot { get; }
            public string ManagedRoot => Storage.GetFullPath(SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY);
            public string JournalPath => Storage.GetFullPath(SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME);
            private readonly Dictionary<string, (string Hash, DateTime Modified)> authorFiles;
            private readonly Dictionary<string, DateTime> authorDirectories;

            public FirstWorkspaceFixture(Storage storage, RealmAccess realm, SkinManager manager, string authorRoot)
            {
                Storage = storage;
                Realm = realm;
                Manager = manager;
                AuthorRoot = authorRoot;
                authorFiles = captureAuthorFiles();
                authorDirectories = captureAuthorDirectories();
            }

            public void AssertAuthorUnchanged()
            {
                Assert.That(captureAuthorFiles(), Is.EquivalentTo(authorFiles));
                Assert.That(captureAuthorDirectories(), Is.EquivalentTo(authorDirectories));
            }

            private Dictionary<string, DateTime> captureAuthorDirectories()
                => Directory.GetDirectories(AuthorRoot, "*", SearchOption.AllDirectories).Prepend(AuthorRoot)
                    .ToDictionary(path => Path.GetRelativePath(AuthorRoot, path), Directory.GetLastWriteTimeUtc);

            private Dictionary<string, (string Hash, DateTime Modified)> captureAuthorFiles()
                => Directory.GetFiles(AuthorRoot, "*", SearchOption.AllDirectories).ToDictionary(
                    path => Path.GetRelativePath(AuthorRoot, path),
                    path => (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))), File.GetLastWriteTimeUtc(path)));

            public void Dispose()
            {
                Manager.ShutdownManagedFolderMutations();
                if (Directory.Exists(ManagedRoot) && File.GetAttributes(ManagedRoot).HasFlag(FileAttributes.ReparsePoint))
                    Directory.Delete(ManagedRoot);
                Realm.Dispose();
            }
        }
    }
}
