// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Windows;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase("oms-simple", MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase("oms-simple", MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase("oms-simple", MaterialDiagnosticPackageSource.ExternalFolder)]
        [TestCase("oms-complex", MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase("oms-complex", MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase("oms-complex", MaterialDiagnosticPackageSource.ExternalFolder)]
        [TestCase("aurora-study", MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase("aurora-study", MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase("aurora-study", MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestCanonicalAndAuthorProductsOnVerifiedBackupRoot(string package, MaterialDiagnosticPackageSource source)
        {
            string? baseline = Environment.GetEnvironmentVariable("OMS_C6_BACKUP_ROOT");
            if (string.IsNullOrWhiteSpace(baseline))
            {
                Assert.Ignore("Requires the privately verified real data-root backup; synthetic storage does not satisfy this gate.");
                return;
            }

            string archive = findC7AuthorPackage(package);
            string name = package switch
            {
                "oms-simple" => "OMS Simple · 静线",
                "oms-complex" => "OMS Complex · 星轨",
                "aurora-study" => "Aurora Study · 作者演练",
                _ => throw new ArgumentOutOfRangeException(nameof(package)),
            };
            Task? work = null;
            Task<bool>? operation = null;
            Task<IList<Live<SkinInfo>>>? dropdown = null;
            Guid candidateId = Guid.Empty;
            Guid retainedUserId = Guid.Empty;
            Dictionary<string, string> retainedFiles = null!;
            Dictionary<Guid, string> originalUserRecords = null!;
            int preparations = 0;
            bool ordinaryRoundTrip = false;
            ExactLayoutJourneyHost renderer = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revision = null!;

            AddStep("copy the real verified baseline into an independent working root", () =>
            {
                manager.ShutdownManagedFolderMutations();
                c6Backup = new C6BackupContext(manager, Path.GetFullPath(baseline), archive, null);
                work = Task.Run(c6Backup.PrepareWorkingCopy);
            });
            AddUntilStep("the complete backup copy is ready", () => work?.IsCompleted == true);
            AddStep("open only the working Realm and preserve a real ordinary user package", () =>
            {
                work!.GetAwaiter().GetResult();
                c6Backup!.Realm = new RealmAccess(c6Backup.Storage, "client");
                originalUserRecords = captureBackupRecords(c6Backup.Realm);
                manager = new SkinManager(c6Backup.Storage, c6Backup.Realm, host, Resources, Audio, Scheduler);
                c6Backup.Manager = manager;
                assertC7OriginalRecordsPreserved(originalUserRecords, captureBackupRecords(c6Backup.Realm), "initial manager startup");
                manager.EnsureGameplaySkinInstallationAvailable();
                c6Backup.PackageRoot = source == MaterialDiagnosticPackageSource.ManagedFolder
                    ? c6Backup.Storage.GetFullPath("chartskin/c7-author-product")
                    : Path.Combine(c6Backup.RunRoot, "external-c7-author-product");
                work = Task.Run(async () =>
                {
                    // Match the authored name so this independent retained package does not request an unrelated
                    // metadata rename. Otherwise import leaves its original, zero-reference ini for normal startup
                    // cleanup, and a snapshot of all files would incorrectly classify that new temporary blob as old user data.
                    string retained = Path.Combine(c6Backup.RunRoot, "Retained user skin before C7 install.osk");
                    using (var output = new FileStream(retained, FileMode.CreateNew, FileAccess.Write))
                    using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
                    {
                        using (var writer = new StreamWriter(zip.CreateEntry("skin.ini").Open(), new UTF8Encoding(false)))
                            writer.Write("[General]\nName: Retained user skin before C7 install\nAuthor: Existing user fixture\nVersion: 2.7\n");
                        using var notes = new StreamWriter(zip.CreateEntry("author-notes.txt").Open(), new UTF8Encoding(false));
                        notes.Write("This independent pre-existing user content must survive every C7 source operation.");
                    }
                    Live<SkinInfo> retainedRecord = await manager.Import(new ImportTask(retained)).ConfigureAwait(false);
                    retainedUserId = retainedRecord.ID;
                    c6Backup.ExistingRecords = captureBackupRecords(c6Backup.Realm);
                    retainedFiles = hashBackupFiles(c6Backup.Storage.GetFullPath("files"));
                    if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
                    {
                        string import = Path.Combine(c6Backup.RunRoot, "c7-product-import.osk");
                        File.Copy(archive, import);
                        File.SetAttributes(import, FileAttributes.Normal);
                        await manager.Import(new ImportTask(import)).ConfigureAwait(false);
                    }
                    else
                    {
                        ZipFile.ExtractToDirectory(archive, c6Backup.PackageRoot);
                        if (source == MaterialDiagnosticPackageSource.ManagedFolder)
                        {
                            var scanner = new SkinManagedFolderScanner(c6Backup.Realm,
                                new WindowsSkinManagedFolderDiscoverySource(c6Backup.Storage), manager.ManagedFolderOperationCoordinator);
                            SkinManagedFolderScanResult scan = scanner.Scan();
                            Assert.That(scan.IsSuccess, Is.True, scan.ToString());
                            Assert.That(scan.Added, Is.GreaterThanOrEqualTo(1), "The complete authored directory must pass ordinary folder package admission.");
                        }
                        else
                        {
                            c6Backup.ExternalFiles = hashBackupFiles(c6Backup.PackageRoot);
                            var external = new SkinInfo(instantiationInfo: SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                            {
                                FilesystemStoragePath = c6Backup.PackageRoot,
                                IsExternalFilesystemStorage = true,
                                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
                            };
                            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(external, c6Backup.Storage);
                            Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ExternalFolder));
                            SkinExternalPackageCaptureResult capture = new SkinExternalFolderCaptureService().CaptureHeld(resolution.ExternalCaptureRequest);
                            Assert.That(capture.IsSuccess, Is.True, capture.ToString());
                            using ISkinExternalPackageCaptureSession session = capture.Session!;
                            using SkinPackageRevisionCapsule capsule = session.TakeCapsule();
                            Assert.That(SkinManagedFolderPackageMetadataReader.TryRead(capsule, out _), Is.True,
                                "The full author directory must pass ordinary metadata admission before its production registration.");
                            session.Validate(CancellationToken.None);
                        }
                    }
                });
            });
            AddUntilStep("the complete authored files enter their ordinary source", () => work!.IsCompleted);
            AddStep("register external author files through the production caller", () =>
            {
                work!.GetAwaiter().GetResult();
                operation = source == MaterialDiagnosticPackageSource.ExternalFolder
                    ? manager.RegisterExternalFolderAsync(c6Backup!.PackageRoot) : Task.FromResult(true);
            });
            AddUntilStep("source registration is complete", () => operation!.IsCompleted);
            AddStep("read the actual skin selection list", () =>
            {
                Assert.That(operation!.GetAwaiter().GetResult(), Is.True,
                    "Production registration after complete external capture; managed root exists: " + Directory.Exists(c6Backup!.Storage.GetFullPath("chartskin")));
                dropdown = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("selection entries arrive", () => dropdown?.IsCompleted == true);
            AddStep("select the finished package without granting optional effects", () =>
            {
                Live<SkinInfo> candidate = dropdown!.GetAwaiter().GetResult().Single(item => item.PerformRead(info =>
                    !info.Protected && !c6Backup!.ExistingRecords.ContainsKey(info.ID) && info.Name.StartsWith(name, StringComparison.Ordinal)));
                candidateId = candidate.ID;
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("the exact authored package is current", () => manager.CurrentSkin.Value.SkinInfo.ID == candidateId);

            if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
            {
                string[] previousExports = null!;
                Dictionary<string, byte[]> importedFiles = null!;
                string importedName = string.Empty;
                string importedCreator = string.Empty;
                Live<SkinInfo> roundTripped = null!;
                AddStep("export the actual ordinary package through the normal manager", () =>
                {
                    // The current drawable owns a metadata-only immutable snapshot; the complete file declarations
                    // remain in the authoritative ordinary Realm record used by the export operation.
                    importedFiles = c6Backup!.Realm!.Run(database =>
                    {
                        SkinInfo info = database.Find<SkinInfo>(candidateId)!;
                        importedName = info.Name;
                        importedCreator = info.Creator;
                        return info.Files.ToDictionary(file => file.Filename,
                            file => File.ReadAllBytes(c6Backup.Storage.GetFullPath("files/" + file.File.GetStoragePath())), StringComparer.Ordinal);
                    });
                    previousExports = c6Backup!.Storage.GetFiles("exports", "*.osk").ToArray();
                    work = manager.ExportCurrentSkin();
                });
                AddUntilStep("the ordinary export completes", () => work!.IsCompleted);
                AddStep("verify all authored files survive export and reimport the result", () =>
                {
                    work!.GetAwaiter().GetResult();
                    string exported = c6Backup!.Storage.GetFiles("exports", "*.osk").Except(previousExports).Single();
                    string exportedPath = c6Backup.Storage.GetFullPath(exported);
                    using (var expected = ZipFile.OpenRead(archive))
                    using (var actual = ZipFile.OpenRead(exportedPath))
                    {
                        foreach (ZipArchiveEntry entry in expected.Entries.Where(entry => !entry.FullName.EndsWith('/')))
                        {
                            ZipArchiveEntry? counterpart = actual.GetEntry(entry.FullName);
                            Assert.That(counterpart, Is.Not.Null, entry.FullName);
                            using Stream expectedBytes = entry.Open();
                            using Stream actualBytes = counterpart!.Open();
                            Assert.That(System.Security.Cryptography.SHA256.HashData(actualBytes),
                                Is.EqualTo(System.Security.Cryptography.SHA256.HashData(importedFiles[entry.FullName])), "Export must preserve the actual imported file: " + entry.FullName);
                            if (entry.FullName == "skin.ini")
                            {
                                using var expectedReader = new StreamReader(expectedBytes);
                                using var importedReader = new StreamReader(new MemoryStream(importedFiles[entry.FullName]));
                                var originalLines = new List<string>();
                                var importedLines = new List<string>();
                                while (expectedReader.ReadLine() is { } line)
                                    originalLines.Add(line);
                                while (importedReader.ReadLine() is { } line)
                                    importedLines.Add(line);
                                Assert.That(importedLines.Take(originalLines.Count), Is.EqualTo(originalLines), "Ordinary metadata naming must preserve every authored line.");
                                string[] appended = importedLines.Skip(originalLines.Count).ToArray();
                                if (appended.Length > 0)
                                {
                                    Assert.That(appended.Length, Is.EqualTo(5));
                                    Assert.That(appended[0], Is.Empty);
                                    Assert.That(appended[1], Does.StartWith("//"));
                                    Assert.That(appended.Skip(2), Is.EqualTo(new[] { "[General]", "Name: " + importedName, "Author: " + importedCreator }));
                                }
                            }
                            else
                                Assert.That(System.Security.Cryptography.SHA256.HashData(importedFiles[entry.FullName]),
                                    Is.EqualTo(System.Security.Cryptography.SHA256.HashData(expectedBytes)), "Import must preserve the authored asset: " + entry.FullName);
                        }
                    }
                    string reimport = Path.Combine(c6Backup.RunRoot, "c7-export-reimport.osk");
                    File.Copy(exportedPath, reimport);
                    work = Task.Run(async () => roundTripped = await manager.Import(new ImportTask(reimport)).ConfigureAwait(false));
                });
                AddUntilStep("the exported package is imported normally", () => work!.IsCompleted);
                AddStep("select the exported and reimported user package", () =>
                {
                    work!.GetAwaiter().GetResult();
                    candidateId = roundTripped.ID;
                    manager.CurrentSkinInfo.Value = roundTripped;
                    ordinaryRoundTrip = true;
                });
                AddUntilStep("the round-tripped package becomes current", () => manager.CurrentSkin.Value.SkinInfo.ID == candidateId);
            }

            AddStep("mount both actual playfields using the complete package", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
                renderer.ShowMania();
            });
            AddUntilStep("both real renderers have complete required visual parts", () => renderer.BmsReady && renderer.ManiaReady);
            AddUntilStep("both production scene owners are ready", () => new Drawable[] { renderer.BmsDrawable, renderer.ManiaDrawable }
                .All(drawable => drawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().SingleOrDefault()?.IsSceneReady == true));
            AddStep("basic gameplay remains complete without optional script permission", () =>
            {
                foreach (GameplaySkinSceneRuntimeHost scene in renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>()
                             .Concat(renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>()))
                    assertCanonicalProductScene(scene, package);
                renderer.Expire();
            });
            AddUntilStep("both playfields detach before restart", () => renderer.Parent == null);
            AddStep("restart Realm and the real manager on the copied data root", () =>
            {
                manager.ShutdownManagedFolderMutations();
                c6Backup!.Realm!.Dispose();
                c6Backup.Realm = new RealmAccess(c6Backup.Storage, "client");
                manager = new SkinManager(c6Backup.Storage, c6Backup.Realm, host, Resources, Audio, Scheduler);
                c6Backup.Manager = manager;
                manager.SetSkinFromConfiguration(candidateId.ToString());
            });
            AddUntilStep("restart restores the same current record", () => manager.CurrentSkin.Value.SkinInfo.ID == candidateId);
            AddStep("use the real settings reload control", () =>
            {
                revision = manager.CurrentRevision;
                manager.CurrentRevisionPrepareStarted = () => Interlocked.Increment(ref preparations);
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("reload is available after restart", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("request unchanged whole-package reload", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("reload completes at the production boundary", () => Volatile.Read(ref preparations) == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("unchanged reload preserves the exact publication", () =>
            {
                Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                caller.Expire();
            });
            AddUntilStep("settings leaves the mutation boundary", () => caller.Parent == null);
            AddStep("attach both current playfields again", () =>
            {
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
                renderer.ShowMania();
            });
            AddUntilStep("both live playfields hold their current revision", () => renderer.BmsReady && renderer.ManiaReady);
            AddStep("try deleting or unregistering while playing", () => operation = source == MaterialDiagnosticPackageSource.ExternalFolder
                ? manager.UnregisterExternalFolderAsync(candidateId) : manager.DeleteSkinAsync(candidateId));
            AddUntilStep("live gameplay refuses source removal", () => operation!.IsCompleted);
            AddStep("keep the same package and detach both playfields", () =>
            {
                Assert.That(operation!.GetAwaiter().GetResult(), Is.False);
                Assert.That(manager.CurrentRevision, Is.SameAs(revision));
                renderer.Expire();
            });
            AddUntilStep("all actual player leases are released", () => renderer.Parent == null && revision.ConsumersDetached.IsCompleted);
            AddStep("remove only the selected source after leaving gameplay", () => operation = source == MaterialDiagnosticPackageSource.ExternalFolder
                ? manager.UnregisterExternalFolderAsync(candidateId) : manager.DeleteSkinAsync(candidateId));
            AddUntilStep("source removal completes after the canonical fallback", () => operation!.IsCompleted);
            AddStep("retain old user skins, unknown blobs, the real baseline and external author bytes", () =>
            {
                Assert.That(operation!.GetAwaiter().GetResult(), Is.True);
                Assert.That(CanonicalSkinPackage.IsCanonicalSkin(manager.CurrentSkin.Value), Is.True);
                Dictionary<Guid, string> after = captureBackupRecords(c6Backup!.Realm!);
                assertC7OriginalRecordsPreserved(originalUserRecords, after, "completed source operations and restart");
                Assert.That(after.ContainsKey(retainedUserId), Is.True);
                Assert.That(c6Backup.ExistingRecords.All(entry => after.TryGetValue(entry.Key, out string? value) && value == entry.Value), Is.True);
                Assert.That(retainedFiles.All(entry => hashBackupFile(Path.Combine(c6Backup.Storage.GetFullPath("files"), entry.Key)) == entry.Value), Is.True);
                int preservedWorkingFiles = assertC7OriginalWorkingFilesPreserved(c6Backup);
                Assert.That(equalBackupFiles(c6Backup.BaselineFiles, hashBackupFiles(c6Backup.BaselineRoot)), Is.True);
                if (source == MaterialDiagnosticPackageSource.ExternalFolder)
                    Assert.That(equalBackupFiles(c6Backup.ExternalFiles!, hashBackupFiles(c6Backup.PackageRoot)), Is.True);
                File.WriteAllText(Path.Combine(c6Backup.RunRoot, "c7-outcome.json"), JsonConvert.SerializeObject(new
                {
                    Contract = "oms-c7-backup-root-gate.v1",
                    Package = package,
                    Source = source.ToString(),
                    OriginalUserRecords = originalUserRecords.Count,
                    SupplementalExistingUserRecords = 1,
                    BaselineBlobs = c6Backup.ExistingBlobs.Count,
                    PreservedWorkingContentFiles = preservedWorkingFiles,
                    MutableRealmRuntimeFiles = c6Backup.BaselineFiles.Count - preservedWorkingFiles,
                    OrdinaryExportReimport = ordinaryRoundTrip,
                    BothRulesetRenderers = true,
                    RestartAndSettingsReload = true,
                    LiveMutationRejected = true,
                    CanonicalFallbackAfterRemoval = true,
                    BaselineAndUserFilesPreserved = true,
                }, Formatting.Indented));
            });
        }

        private static void assertC7OriginalRecordsPreserved(Dictionary<Guid, string> before, Dictionary<Guid, string> after, string phase)
            => Assert.That(before.All(entry => after.TryGetValue(entry.Key, out string? value) && value == entry.Value), Is.True,
                "Every original non-protected user record must survive " + phase + "; a post-startup recapture is not the original baseline.");

        private static int assertC7OriginalWorkingFilesPreserved(C6BackupContext backup)
        {
            string realmFilename = backup.Realm!.Filename.Replace('\\', '/');
            int preserved = 0;
            foreach ((string relative, string originalHash) in backup.BaselineFiles)
            {
                string normalised = relative.Replace('\\', '/');

                // Opening the independent copy legitimately changes this exact Realm and its transient SDK
                // coordination files. Its user records are compared separately, before manager startup and after
                // all operations. Do not exempt other databases, recovery archives or unknown lookalike files.
                if (normalised.Equals(realmFilename, StringComparison.OrdinalIgnoreCase)
                    || normalised.Equals(realmFilename + ".lock", StringComparison.OrdinalIgnoreCase)
                    || normalised.Equals(realmFilename + ".note", StringComparison.OrdinalIgnoreCase)
                    || normalised.StartsWith(realmFilename + ".management/", StringComparison.OrdinalIgnoreCase))
                    continue;

                string workingFile = backup.Storage.GetFullPath(relative);
                if (normalised.Equals("skin-canonical/oms-simple.osk", StringComparison.OrdinalIgnoreCase)
                    && (!File.Exists(workingFile) || hashBackupFile(workingFile) != originalHash))
                {
                    // An installation upgrade may replace the managed working package only after preserving the
                    // previous exact bytes. The baseline's own unchanged hash cannot prove this preservation.
                    string directory = Path.GetDirectoryName(workingFile)!;
                    Assert.That(Directory.Exists(directory)
                                && Directory.EnumerateFiles(directory, "oms-simple.osk.preserved-*")
                                    .Any(path => hashBackupFile(path) == originalHash), Is.True,
                        "The previous canonical working package must remain preserved after installation recovery.");
                }
                else
                {
                    Assert.That(File.Exists(workingFile), Is.True, "An original working-copy content file disappeared; content index " + preserved);
                    Assert.That(hashBackupFile(workingFile) == originalHash, Is.True, "An original working-copy content file changed; content index " + preserved);
                }

                // This fixture does not point configuration writers or logging at the copied root, so their
                // original files are preserved too. Skin/chart/export/author and unknown content get the same check.
                preserved++;
            }
            return preserved;
        }

        private static string findC7AuthorPackage(string name)
        {
            string? explicitRoot = Environment.GetEnvironmentVariable("OMS_C7_PACKAGES");
            if (!string.IsNullOrWhiteSpace(explicitRoot))
            {
                string explicitPackage = Path.Combine(Path.GetFullPath(explicitRoot), name + ".osk");
                Assert.That(File.Exists(explicitPackage), Is.True, "The supplied C7 package directory must contain all finished works.");
                return explicitPackage;
            }
            var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (directory != null)
            {
                string candidate = Path.Combine(directory.FullName, "skin-authoring", "dist", name + ".osk");
                if (File.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }
            throw new FileNotFoundException("Build the completed public author packages before running the C7 backup-root gate.");
        }
    }
}
