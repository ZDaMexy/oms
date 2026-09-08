// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Windows;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        private C6BackupContext? c6Backup;

        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm, false)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder, false)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder, false)]
        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm, true)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder, true)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder, true)]
        public void TestActualCandidateOnVerifiedBackupRootPreservesUserDataAcrossRestartReloadAndMutation(MaterialDiagnosticPackageSource source, bool bytecode)
        {
            string? baselineRoot = Environment.GetEnvironmentVariable("OMS_C6_BACKUP_ROOT");
            string? candidateArchive = Environment.GetEnvironmentVariable("OMS_C6_CANDIDATE_OSK");
            string? candidateBytecode = Environment.GetEnvironmentVariable("OMS_C6_CANDIDATE_BYTECODE");
            if (string.IsNullOrEmpty(baselineRoot) || string.IsNullOrEmpty(candidateArchive) || bytecode && string.IsNullOrEmpty(candidateBytecode))
            {
                Assert.Ignore("Requires the privately verified data-root backup and generated C6 candidate; synthetic storage is not this gate.");
                return;
            }

            Task? work = null;
            Task<IList<Live<SkinInfo>>>? dropdown = null;
            Task<bool>? mutation = null;
            Task<bool>? registration = null;
            Task<SkinManagedFolderRenameOperationResult>? rename = null;
            Guid candidateId = Guid.Empty;
            ExactLayoutJourneyHost renderer = null!;
            C6GameplayTestClock gameplayClock = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            FullSkinSettingsCallerHost caller = null!;
            GameplaySkinScriptAuthorization originalAuthorization = null!;
            SkinCurrentRevision originalRevision = null!;
            int preparations = 0;

            AddStep("copy verified backup into an independent disposable working root", () =>
            {
                manager.ShutdownManagedFolderMutations();
                c6Backup = new C6BackupContext(manager, Path.GetFullPath(baselineRoot), Path.GetFullPath(candidateArchive), bytecode ? Path.GetFullPath(candidateBytecode!) : null);
                work = Task.Run(c6Backup.PrepareWorkingCopy);
            });
            AddUntilStep("private backup copy is ready", () => work?.IsCompleted == true);
            AddStep("open Realm and the real skin manager only on the working copy", () =>
            {
                work!.GetAwaiter().GetResult();
                c6Backup!.Realm = new RealmAccess(c6Backup.Storage, "client");
                c6Backup.ExistingRecords = captureBackupRecords(c6Backup.Realm);
                manager = new SkinManager(c6Backup.Storage, c6Backup.Realm, host, Resources, Audio, Scheduler);
                c6Backup.Manager = manager;
                c6Backup.PackageRoot = source == MaterialDiagnosticPackageSource.ManagedFolder
                    ? c6Backup.Storage.GetFullPath("chartskin/c6-momentum-backup")
                    : Path.Combine(c6Backup.RunRoot, "external-author-package");

                work = Task.Run(async () =>
                {
                    if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
                    {
                        string import = Path.Combine(c6Backup.RunRoot, "candidate-import.osk");
                        File.Copy(c6Backup.ArchivePath, import);
                        await manager.Import(import).ConfigureAwait(false);
                    }
                    else
                    {
                        unpackBackupCandidate(c6Backup.ArchivePath, c6Backup.PackageRoot, bytecode);
                        if (source == MaterialDiagnosticPackageSource.ManagedFolder)
                        {
                            var scanner = new SkinManagedFolderScanner(c6Backup.Realm,
                                new WindowsSkinManagedFolderDiscoverySource(c6Backup.Storage), manager.ManagedFolderOperationCoordinator);
                            Assert.That(scanner.Scan().IsSuccess, Is.True);
                        }
                        else
                            c6Backup.ExternalFiles = hashBackupFiles(c6Backup.PackageRoot);
                    }
                });
            });
            AddUntilStep("ordinary import or real folder discovery completes", () => work!.IsCompleted);
            AddStep("register the external package through its actual update-thread caller", () =>
            {
                work!.GetAwaiter().GetResult();
                registration = source == MaterialDiagnosticPackageSource.ExternalFolder
                    ? manager.RegisterExternalFolderAsync(c6Backup!.PackageRoot) : Task.FromResult(true);
            });
            AddUntilStep("the production registration finishes", () => registration?.IsCompleted == true);
            AddStep("query the production skin dropdown", () =>
            {
                Assert.That(registration!.GetAwaiter().GetResult(), Is.True);
                dropdown = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("dropdown query completes", () => dropdown?.IsCompleted == true);
            AddStep("select the actual generated complex package", () =>
            {
                string expectedName = source == MaterialDiagnosticPackageSource.OrdinaryRealm
                    ? "OMS Momentum C6 [candidate-import]" : "OMS Momentum C6";
                Live<SkinInfo> candidate = dropdown!.GetAwaiter().GetResult().Single(item => item.PerformRead(info =>
                    info.Name == expectedName && !c6Backup!.ExistingRecords.ContainsKey(info.ID)));
                candidateId = candidate.ID;
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("current revision owns the candidate compiler and authorization", () =>
                manager.CurrentSkinInfo.Value.ID == candidateId && manager.CurrentSkin.Value.PreparedGameplaySkinPackage?.ScriptAuthorization != null);
            AddStep("explicitly authorize the package on the copied data root", () =>
            {
                originalAuthorization = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(originalAuthorization.RequiredSatisfied, Is.False);
                work = grantC6Script(originalAuthorization);
            });
            AddUntilStep("consent is durable in the copied root", () => work!.IsCompleted);
            AddStep("mount the actual BMS and mania renderer consumers", () =>
            {
                work!.GetAwaiter().GetResult();
                renderer = new ExactLayoutJourneyHost(manager);
                gameplayClock = renderer.AttachC6GameplayClock(1_000);
                Add(renderer);
            });
            AddUntilStep("both current-revision script hosts attach", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("real deterministic gameplay ticks make the complex overlay visible", () =>
            {
                gameplayClock.Sample(1_100);
                foreach (GameplaySkinSceneRuntimeHost scene in new[] { bms, mania })
                {
                    Assert.That(scene.ScriptStatus, Is.EqualTo("running"));
                    Assert.That(scene.ScriptInstance!.Fault, Is.Null);
                    Assert.That(scene.TryGetRuntimeNode("momentum.pulse", out GameplaySkinSceneRuntimeNode? pulse), Is.True);
                    Assert.That(pulse!.TransformDrawable.Rotation, Is.EqualTo(45));
                    Assert.That(scene.ScriptInstance.Profiler.Writes, Is.GreaterThan(0));
                }
                renderer.Expire();
            });
            AddUntilStep("both consumer trees release their exact revision leases", () => renderer.Parent == null);
            AddStep("close and reopen both Realm and manager on the copied data root", () =>
            {
                manager.ShutdownManagedFolderMutations();
                Assert.That(originalAuthorization.RequiredSatisfied, Is.False);
                c6Backup!.Realm!.Dispose();
                c6Backup.Realm = new RealmAccess(c6Backup.Storage, "client");
                manager = new SkinManager(c6Backup.Storage, c6Backup.Realm, host, Resources, Audio, Scheduler);
                c6Backup.Manager = manager;
                manager.SetSkinFromConfiguration(candidateId.ToString());
            });
            AddUntilStep("the exact recorded selection restarts", () =>
                manager.CurrentSkinInfo.Value.ID == candidateId && manager.CurrentSkin.Value.PreparedGameplaySkinPackage?.ScriptAuthorization != null);
            AddStep("restart retains exact consent and uses Settings for manual reload", () =>
            {
                GameplaySkinScriptAuthorization restarted = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(restarted.Key, Is.EqualTo(originalAuthorization.Key));
                Assert.That(restarted.RequiredSatisfied, Is.True);
                originalRevision = manager.CurrentRevision;
                manager.CurrentRevisionPrepareStarted = () => Interlocked.Increment(ref preparations);
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("the Settings reload button is enabled", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("reload the copied source through its real Settings caller", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("whole package preparation completes", () => Volatile.Read(ref preparations) == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("unchanged whole package keeps its exact publication", () =>
            {
                Assert.That(manager.CurrentRevision, Is.SameAs(originalRevision));
                Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!.RequiredSatisfied, Is.True);
                caller.Expire();
            });
            AddUntilStep("Settings detaches before the copied source mutation", () => caller.Parent == null);

            if (source == MaterialDiagnosticPackageSource.ManagedFolder)
            {
                AddStep("rename the actual managed directory on the copied root", () => rename = manager.RenameManagedFolderAsync(candidateId, "c6-momentum-renamed"));
                AddUntilStep("managed rename finishes", () => rename?.IsCompleted == true);
                AddStep("managed rename preserves the script authorization identity", () =>
                {
                    Assert.That(rename!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!.Key, Is.EqualTo(originalAuthorization.Key));
                });
            }

            AddStep("attach the scripted production consumers before the final current mutation", () =>
            {
                renderer = new ExactLayoutJourneyHost(manager);
                gameplayClock = renderer.AttachC6GameplayClock(1_000);
                bms = null!;
                mania = null!;
                originalRevision = manager.CurrentRevision;
                Add(renderer);
            });
            AddUntilStep("both scripts retain the exact current revision", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady && bms.ScriptInstance?.IsEnabled == true && mania.ScriptInstance?.IsEnabled == true;
            });
            AddStep("request the current mutation with both real script hosts attached", () => mutation = source == MaterialDiagnosticPackageSource.ExternalFolder
                ? manager.UnregisterExternalFolderAsync(candidateId) : manager.DeleteSkinAsync(candidateId));
            AddUntilStep("live gameplay rejects current mutation before publication", () => mutation?.IsCompleted == true);
            AddStep("keep A and detach only the BMS production consumer", () =>
            {
                Assert.That(mutation!.GetAwaiter().GetResult(), Is.False);
                Assert.That(manager.CurrentRevision, Is.SameAs(originalRevision));
                Assert.That(originalRevision.ConsumersDetached.IsCompleted, Is.False);
                Assert.That(bms.ScriptInstance!.IsEnabled, Is.True);
                Assert.That(mania.ScriptInstance!.IsEnabled, Is.True);
                renderer.BmsProvider.Expire();
            });
            AddUntilStep("the BMS script and its provider detach through the real tree", () => renderer.BmsProvider.Parent == null && !bms.ScriptInstance!.IsEnabled);
            AddStep("request current mutation while the mania script remains attached", () =>
            {
                Assert.That(mania.ScriptInstance!.IsEnabled, Is.True);
                Assert.That(originalRevision.ConsumersDetached.IsCompleted, Is.False);
                mutation = source == MaterialDiagnosticPackageSource.ExternalFolder
                    ? manager.UnregisterExternalFolderAsync(candidateId) : manager.DeleteSkinAsync(candidateId);
            });
            AddUntilStep("the remaining actual host still rejects mutation", () => mutation?.IsCompleted == true);
            AddStep("detach the final script consumer and its owning tree", () =>
            {
                Assert.That(mutation!.GetAwaiter().GetResult(), Is.False);
                Assert.That(manager.CurrentRevision, Is.SameAs(originalRevision));
                renderer.Expire();
            });
            AddUntilStep("the last script loses runtime access and every exact consumer lease releases", () =>
                renderer.Parent == null && !mania.ScriptInstance!.IsEnabled && originalRevision.ConsumersDetached.IsCompleted);
            AddStep("delete the copied candidate or unregister the read-only external package", () => mutation = source == MaterialDiagnosticPackageSource.ExternalFolder
                ? manager.UnregisterExternalFolderAsync(candidateId) : manager.DeleteSkinAsync(candidateId));
            AddUntilStep("current mutation joins fallback and retirement", () => mutation?.IsCompleted == true);
            AddStep("verify the fallback and preserve all existing user data and unknown blobs", () =>
            {
                Assert.That(mutation!.GetAwaiter().GetResult(), Is.True);
                Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                Dictionary<Guid, string> after = captureBackupRecords(c6Backup!.Realm!);
                Assert.That(c6Backup.ExistingRecords.All(entry => after.TryGetValue(entry.Key, out string? value) && value == entry.Value), Is.True);
                Assert.That(c6Backup.ExistingBlobs.All(entry => hashBackupFile(Path.Combine(c6Backup.Storage.GetFullPath("files"), entry.Key)) == entry.Value), Is.True);
                Assert.That(equalBackupFiles(c6Backup.BaselineFiles, hashBackupFiles(c6Backup.BaselineRoot)), Is.True);
                if (source == MaterialDiagnosticPackageSource.ExternalFolder)
                    Assert.That(equalBackupFiles(c6Backup.ExternalFiles!, hashBackupFiles(c6Backup.PackageRoot)), Is.True);

                File.WriteAllText(Path.Combine(c6Backup.RunRoot, "outcome.json"), JsonConvert.SerializeObject(new
                {
                    Contract = "oms-c6-backup-root-gate.v1",
                    Source = source.ToString(),
                    Format = bytecode ? "bytecode-v1" : "source-v1",
                    ExistingUserRecords = c6Backup.ExistingRecords.Count,
                    ExistingBlobs = c6Backup.ExistingBlobs.Count,
                    BaselinePreserved = true,
                    OriginalSourceOpened = false,
                    BothRulesetRenderers = true,
                    RestartAndSettingsReload = true,
                    MutationAndFallback = true,
                    LiveMutationRejectedUntilLastScriptDetach = true,
                }, Formatting.Indented));
                TestContext.Progress.WriteLine($"C6 backup-root gate passed: {source}/{(bytecode ? "bytecode" : "source")}; preserved {c6Backup.ExistingRecords.Count} user records and {c6Backup.ExistingBlobs.Count} original blobs.");
            });
        }

        [TearDown]
        public void ReleaseC6BackupRealm()
        {
            if (c6Backup == null)
                return;
            c6Backup.Manager?.ShutdownManagedFolderMutations();
            c6Backup.Realm?.Dispose();
            manager = c6Backup.PreviousManager;
            c6Backup = null;
        }

        private static Dictionary<Guid, string> captureBackupRecords(RealmAccess realm)
            => realm.Run(database => database.All<SkinInfo>().Where(info => !info.Protected).ToArray()
                .ToDictionary(info => info.ID, info => JsonConvert.SerializeObject(new
                {
                    info.Name,
                    info.Creator,
                    info.Hash,
                    info.InstantiationInfo,
                    info.FilesystemStoragePath,
                    info.FilesystemStorageAuthorityOwner,
                    info.IsExternalFilesystemStorage,
                    info.DeletePending,
                    Files = info.Files.Select(file => new { file.Filename, file.File.Hash }).ToArray(),
                })));

        private static void unpackBackupCandidate(string archivePath, string destination, bool bytecode)
        {
            Directory.CreateDirectory(destination);
            using var archive = ZipFile.OpenRead(archivePath);
            var expected = new HashSet<string>(new[] { "skin.ini", "gameplay-skin.json", "gameplay-skin.scene.json", bytecode ? "gameplay-skin.bytecode" : "gameplay-skin.script", "tile.png" }, StringComparer.Ordinal);
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                Assert.That(expected.Remove(entry.FullName), Is.True, "This gate consumes the reviewed candidate's exact five files.");
                using Stream input = entry.Open();
                using var output = new FileStream(Path.Combine(destination, entry.FullName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                input.CopyTo(output);
            }
            Assert.That(expected, Is.Empty);
        }

        private static Dictionary<string, string> hashBackupFiles(string root)
            => Directory.Exists(root) ? Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(root, path), hashBackupFile, StringComparer.Ordinal) : new Dictionary<string, string>();

        private static string hashBackupFile(string path)
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }

        private static bool equalBackupFiles(Dictionary<string, string> left, Dictionary<string, string> right)
            => left.Count == right.Count && left.All(entry => right.TryGetValue(entry.Key, out string? value) && value == entry.Value);

        private sealed class C6BackupContext
        {
            public SkinManager PreviousManager { get; }
            public string BaselineRoot { get; }
            public string ArchivePath { get; private set; }
            private readonly string? bytecodePath;
            public string RunRoot { get; } = Path.Combine(Path.GetTempPath(), "backup-C6-run-" + Guid.NewGuid().ToString("N"));
            public NativeStorage Storage { get; }
            public RealmAccess? Realm;
            public SkinManager? Manager;
            public string PackageRoot = string.Empty;
            public Dictionary<Guid, string> ExistingRecords = null!;
            public Dictionary<string, string> BaselineFiles = null!;
            public Dictionary<string, string> ExistingBlobs = null!;
            public Dictionary<string, string>? ExternalFiles;

            public C6BackupContext(SkinManager previousManager, string baselineRoot, string archivePath, string? bytecodePath)
            {
                PreviousManager = previousManager;
                BaselineRoot = baselineRoot;
                ArchivePath = archivePath;
                this.bytecodePath = bytecodePath;
                Storage = new NativeStorage(Path.Combine(RunRoot, "working"));
            }

            public void PrepareWorkingCopy()
            {
                Assert.That(File.Exists(Path.Combine(BaselineRoot, "client.realm")), Is.True);
                BaselineFiles = hashBackupFiles(BaselineRoot);
                ExistingBlobs = hashBackupFiles(Path.Combine(BaselineRoot, "files"));
                foreach (string directory in Directory.EnumerateDirectories(BaselineRoot, "*", SearchOption.AllDirectories))
                {
                    Assert.That((File.GetAttributes(directory) & FileAttributes.ReparsePoint) == 0, Is.True);
                    Directory.CreateDirectory(Storage.GetFullPath(Path.GetRelativePath(BaselineRoot, directory)));
                }
                foreach (string relative in BaselineFiles.Keys)
                {
                    string source = Path.Combine(BaselineRoot, relative);
                    Assert.That((File.GetAttributes(source) & FileAttributes.ReparsePoint) == 0, Is.True);
                    string target = Storage.GetFullPath(relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    File.Copy(source, target);
                }
                Assert.That(equalBackupFiles(BaselineFiles, hashBackupFiles(Storage.GetFullPath("."))), Is.True);
                if (bytecodePath != null)
                {
                    string compiledArchive = Path.Combine(RunRoot, "compiled-candidate.osk");
                    using var source = ZipFile.OpenRead(ArchivePath);
                    using var target = ZipFile.Open(compiledArchive, ZipArchiveMode.Create);
                    foreach (ZipArchiveEntry entry in source.Entries)
                    {
                        byte[] bytes;
                        string name = entry.FullName;
                        if (name == "gameplay-skin.script")
                        {
                            name = "gameplay-skin.bytecode";
                            bytes = File.ReadAllBytes(bytecodePath);
                        }
                        else
                        {
                            using Stream input = entry.Open();
                            using var buffer = new MemoryStream();
                            input.CopyTo(buffer);
                            bytes = buffer.ToArray();
                            if (name == "gameplay-skin.json")
                            {
                                JObject manifest = JObject.Parse(Encoding.UTF8.GetString(bytes));
                                manifest["script"] = "gameplay-skin.bytecode";
                                bytes = Encoding.UTF8.GetBytes(manifest.ToString(Formatting.Indented));
                            }
                        }
                        using Stream output = target.CreateEntry(name).Open();
                        output.Write(bytes);
                    }
                    ArchivePath = compiledArchive;
                }
            }
        }
    }
}
