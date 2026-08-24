// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestManagedCopyProductJourneyPreservesExternalAndDoesNotAutoSelect()
        {
            string externalRoot = string.Empty;
            FolderInventorySnapshot externalSnapshot = default;
            string externalPhysicalProofBefore = string.Empty;
            string targetChildName = string.Empty;
            string targetRoot = string.Empty;
            string renamedChildName = string.Empty;
            string renamedRoot = string.Empty;
            string managedName = string.Empty;
            string managedCreator = string.Empty;
            string managedHash = string.Empty;
            string managedInstantiationInfo = string.Empty;
            byte[] copiedSkinIni = Array.Empty<byte>();
            Guid externalRecordId = Guid.Empty;
            Guid managedRecordId = Guid.Empty;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Guid[] managedRecordBaseline = Array.Empty<Guid>();
            Live<SkinInfo> managedDropdown = null!;
            Live<SkinInfo> externalDropdown = null!;
            Skin? restartedManagedSkin = null;
            Skin? reselectedManagedSkin = null;
            JourneyRendererHost firstRenderer = null!;
            JourneyRendererHost restartedRenderer = null!;
            JourneyRendererHost renamedRenderer = null!;
            Task<bool>? registrationTask = null;
            Task<bool>? copyTask = null;
            Task<bool>? openTask = null;
            Task<bool>? unregisterTask = null;
            Task<bool>? deleteTask = null;
            Task<SkinManagedFolderRenameOperationResult>? renameTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<IList<Live<SkinInfo>>>? dropdownTask = null;
            Stopwatch? copyWait = null;
            var stageWaits = new Dictionary<string, Stopwatch>();
            int openCalls = 0;
            string? openedPath = null;

            AddStep("create immutable renderer package and physical proof", () =>
            {
                externalRoot = createExternalPackage(populateManagedCopyRendererPackage);
                externalSnapshot = captureFolderInventory(externalRoot);
                externalPhysicalProofBefore = captureExternalPhysicalProofDigest(externalRoot);
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                targetChildName = $"copied-{Guid.NewGuid():N}";
                targetRoot = LocalStorage.GetFullPath($"chartskin/{targetChildName}");
                renamedChildName = $"renamed-{Guid.NewGuid():N}";
                renamedRoot = LocalStorage.GetFullPath($"chartskin/{renamedChildName}");
                startStage("external registration");
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep("wait for external registration", () => registrationTask?.IsCompleted == true);
            AddStep("resolve external row and start managed copy", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                finishStage("external registration", TimeSpan.FromSeconds(20));
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for external workspace row", () => workspaceTask?.IsCompleted == true);
            AddStep("start manager-owned managed copy", () =>
            {
                FolderSkinWorkspaceRecord external = workspaceTask!.GetAwaiter().GetResult()
                                                               .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External);
                externalRecordId = external.RecordId;
                Assert.Multiple(() =>
                {
                    Assert.That(external.CanImportManagedCopy, Is.True);
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                });
                copyWait = Stopwatch.StartNew();
                copyTask = manager.ImportManagedCopyAsync(externalRecordId, targetChildName);
            });
            AddUntilStep("wait for managed copy first bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(8));
            AddUntilStep("wait for managed copy second bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(16));
            AddUntilStep("wait for managed copy third bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(24));
            AddUntilStep("wait for managed copy fourth bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(32));
            AddStep("query managed workspace and dropdown", () =>
            {
                bool copied = copyTask!.IsCompleted && copyTask.GetAwaiter().GetResult();

                if (!copied)
                {
                    SkinManagedFolderMutationJournalLoadResult journal =
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load();
                    string staging = LocalStorage.GetFullPath("skin-mutation-staging");
                    int stagingChildren = Directory.Exists(staging)
                        ? Directory.GetDirectories(staging, "*", SearchOption.TopDirectoryOnly).Length
                        : 0;
                    int managedRecords = Realm.Run(realm => realm.All<SkinInfo>()
                        .Count(record => record.FilesystemStorageAuthorityOwner == SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.Fail(
                        $"Managed copy failed: journal={journal.Status}; phase={journal.Journal?.Phase}; "
                        + $"stagingExists={Directory.Exists(staging)}; stagingChildren={stagingChildren}; "
                        + $"targetExists={Directory.Exists(targetRoot)}; "
                        + $"managedRecords={managedRecords}; taskCompleted={copyTask.IsCompleted}; "
                        + $"elapsed={copyWait?.Elapsed}");
                }

                copyWait!.Stop();
                TestContext.Progress.WriteLine($"ManagedCopy task elapsed: {copyWait.Elapsed}");
                Assert.That(copyWait.Elapsed, Is.LessThan(TimeSpan.FromSeconds(40)),
                    "Managed copy exceeded its explicit four-slice test budget.");
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                dropdownTask = manager.GetAllUsableSkinsAsync();
            });
            AddUntilStep("wait for copied workspace", () => workspaceTask?.IsCompleted == true);
            AddUntilStep("wait for copied dropdown", () => dropdownTask?.IsCompleted == true);
            AddStep("assert copy publication is non-selecting and source immutable", () =>
            {
                IReadOnlyList<FolderSkinWorkspaceRecord> rows = workspaceTask!.GetAwaiter().GetResult();
                FolderSkinWorkspaceRecord external = rows.Single(record => record.RecordId == externalRecordId);
                FolderSkinWorkspaceRecord managed = rows.Single(record =>
                    record.Kind == FolderSkinWorkspaceRecordKind.Managed
                    && Realm.Run(realm =>
                    {
                        SkinInfo? candidate = realm.Find<SkinInfo>(record.RecordId);
                        return candidate != null
                               && string.Equals(
                                   candidate.FilesystemStoragePath,
                                   $"chartskin/{targetChildName}",
                                   StringComparison.Ordinal);
                    }));
                managedRecordId = managed.RecordId;
                managedDropdown = dropdownTask!.GetAwaiter().GetResult().Single(record => record.ID == managedRecordId);
                managedDropdown.PerformRead(info =>
                {
                    managedName = info.Name;
                    managedCreator = info.Creator;
                    managedHash = info.Hash;
                    managedInstantiationInfo = info.InstantiationInfo;
                });
                copiedSkinIni = File.ReadAllBytes(Path.Combine(targetRoot, "skin.ini"));

                Assert.Multiple(() =>
                {
                    Assert.That(external.Kind, Is.EqualTo(FolderSkinWorkspaceRecordKind.External));
                    Assert.That(external.CanImportManagedCopy, Is.True);
                    Assert.That(managed.DisplayLabel, Is.EqualTo("external renderer journey"));
                    Assert.That(managed.CanImportManagedCopy, Is.False);
                    Assert.That(managed.CanRename, Is.True);
                    Assert.That(managed.CanDelete, Is.True);
                    Assert.That(managedDropdown.PerformRead(info => info.IsExternalFilesystemStorage), Is.False);
                    Assert.That(
                        managedDropdown.PerformRead(info => info.FilesystemStoragePath),
                        Is.EqualTo($"chartskin/{targetChildName}"));
                    Assert.That(
                        managedDropdown.PerformRead(info => info.FilesystemStorageAuthorityOwner),
                        Is.EqualTo(SkinManagedFolderScanner.AUTHORITY_OWNER));
                    Assert.That(managedDropdown.PerformRead(info => info.Files.Count), Is.Zero);
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(sourceChangedCount, Is.Zero);
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(captureFolderInventory(targetRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(Directory.Exists(Path.Combine(targetRoot, "empty", "nested")), Is.True);
                    Assert.That(Directory.GetFileSystemEntries(Path.Combine(targetRoot, "empty", "nested")), Is.Empty);
                    Assert.That(
                        new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
            AddStep("explicitly select copied managed record", () =>
            {
                startStage("copied managed selection");
                manager.CurrentSkinInfo.Value = managedDropdown;
            });
            AddUntilStep("wait for copied managed selection", () =>
                manager.CurrentSkinInfo.Value.ID == managedRecordId
                && manager.CurrentSkin.Value.SkinInfo.ID == managedRecordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount copied managed renderer host", () =>
            {
                finishStage("copied managed selection", TimeSpan.FromSeconds(20));
                startStage("copied renderer");
                Add(firstRenderer = new JourneyRendererHost(manager, Clock.CurrentTime + 60_000, Clock.CurrentTime + 5_000));
            });
            AddUntilStep("wait for copied renderer host", () => firstRenderer.IsLoaded);
            AddStep("show copied BMS renderer", () => firstRenderer.ShowBms());
            AddUntilStep("wait for copied BMS artifacts", () => firstRenderer.BmsArtifactsLoaded);
            AddStep("assert copied BMS artifacts and external immutability", () =>
            {
                assertJourneyBmsRendererArtifacts(firstRenderer);
                Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
            });
            AddStep("show copied mania renderer", () => firstRenderer.ShowMania());
            AddUntilStep("wait for copied mania artifacts", () => firstRenderer.ManiaArtifactsLoaded);
            AddStep("assert copied mania artifacts", () =>
            {
                assertJourneyManiaRendererArtifacts(firstRenderer);
                Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                finishStage("copied renderer", TimeSpan.FromSeconds(30));
            });
            AddStep("retire copied renderer host", () => firstRenderer.Expire());
            AddUntilStep("wait for copied renderer retirement", () => firstRenderer.Parent == null);
            AddStep("restart manager and request persisted copied record", () =>
            {
                startStage("copied manager restart");
                manager.ShutdownManagedFolderMutations();
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                sourceChangedCount = 0;
                manager.SourceChanged += () => sourceChangedCount++;
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                manager.SetSkinFromConfiguration(managedRecordId.ToString());
            });
            AddUntilStep("wait for copied restart discovery", () =>
                workspaceTask?.IsCompleted == true
                && manager.CurrentSkinInfo.Value.ID == managedRecordId
                && manager.CurrentSkin.Value.SkinInfo.ID == managedRecordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount copied restart renderer host", () =>
            {
                finishStage("copied manager restart", TimeSpan.FromSeconds(30));
                FolderSkinWorkspaceRecord recovered = workspaceTask!.GetAwaiter().GetResult()
                                                                    .Single(record => record.RecordId == managedRecordId);

                Assert.Multiple(() =>
                {
                    Assert.That(recovered.Kind, Is.EqualTo(FolderSkinWorkspaceRecordKind.Managed));
                    Assert.That(manager.CurrentSkin.Value, Is.TypeOf<BmsLegacySkin>());
                    Assert.That(manager.InitialManagedFolderMutationRecoveryResult.Status,
                        Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.NoJournal));
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(Directory.Exists(Path.Combine(targetRoot, "empty", "nested")), Is.True);
                });
                restartedManagedSkin = manager.CurrentSkin.Value;
                startStage("copied restart renderer");
                Add(restartedRenderer = new JourneyRendererHost(manager, Clock.CurrentTime + 60_000, Clock.CurrentTime + 5_000));
            });
            AddUntilStep("wait for copied restart renderer host", () => restartedRenderer.IsLoaded);
            AddStep("show copied restart BMS renderer", () => restartedRenderer.ShowBms());
            AddUntilStep("wait for copied restart BMS artifacts", () => restartedRenderer.BmsArtifactsLoaded);
            AddStep("assert copied restart BMS artifacts", () => assertJourneyBmsRendererArtifacts(restartedRenderer));
            AddStep("show copied restart mania renderer", () => restartedRenderer.ShowMania());
            AddUntilStep("wait for copied restart mania artifacts", () => restartedRenderer.ManiaArtifactsLoaded);
            AddStep("assert copied restart mania artifacts", () =>
            {
                assertJourneyManiaRendererArtifacts(restartedRenderer);
                Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                finishStage("copied restart renderer", TimeSpan.FromSeconds(30));
            });
            AddStep("retire copied restart renderer", () => restartedRenderer.Expire());
            AddUntilStep("wait for copied restart renderer retirement", () => restartedRenderer.Parent == null);
            AddStep("open copied managed folder through fresh authority", () =>
            {
                manager.OpenFolderExternally = path =>
                {
                    openedPath = path;
                    Interlocked.Increment(ref openCalls);
                };
                startStage("managed open folder");
                openTask = manager.OpenFolderAsync(managedRecordId);
            });
            AddUntilStep("wait for copied managed folder open", () => openTask?.IsCompleted == true);
            AddStep("assert exact copied folder was opened", () =>
            {
                finishStage("managed open folder", TimeSpan.FromSeconds(20));
                Assert.Multiple(() =>
                {
                    Assert.That(openTask!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(openCalls, Is.EqualTo(1));
                    Assert.That(openedPath,
                        Is.EqualTo(Path.TrimEndingDirectorySeparator(targetRoot) + Path.DirectorySeparatorChar)
                          .IgnoreCase);
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                });
                startStage("managed rename");
                renameTask = manager.RenameManagedFolderAsync(managedRecordId, renamedChildName);
            });
            AddUntilStep("wait for managed rename first bounded slice", () =>
                renameTask?.IsCompleted == true || stageWaits["managed rename"].Elapsed >= TimeSpan.FromSeconds(8));
            AddUntilStep("wait for managed rename second bounded slice", () =>
                renameTask?.IsCompleted == true || stageWaits["managed rename"].Elapsed >= TimeSpan.FromSeconds(16));
            AddUntilStep("wait for managed rename third bounded slice", () =>
                renameTask?.IsCompleted == true || stageWaits["managed rename"].Elapsed >= TimeSpan.FromSeconds(24));
            AddUntilStep("wait for managed rename fourth bounded slice", () =>
                renameTask?.IsCompleted == true || stageWaits["managed rename"].Elapsed >= TimeSpan.FromSeconds(32));
            AddStep("assert rename preserved active capsule and external source", () =>
            {
                finishStage("managed rename", TimeSpan.FromSeconds(40));
                Assert.That(renameTask!.IsCompleted, Is.True,
                    "Managed rename did not complete within its explicit four-slice test budget.");
                Assert.Multiple(() =>
                {
                    Assert.That(renameTask!.GetAwaiter().GetResult().IsSuccess, Is.True);
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(Directory.Exists(renamedRoot), Is.True);
                    Assert.That(captureFolderInventory(renamedRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(restartedManagedSkin));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(managedRecordId));
                    Assert.That(manager.Query(record => record.ID == managedRecordId)
                                       .PerformRead(record => record.FilesystemStoragePath),
                        Is.EqualTo($"chartskin/{renamedChildName}"));
                    Assert.That(manager.Query(record => record.ID == managedRecordId)
                                       .PerformRead(record => record.Name), Is.EqualTo(managedName));
                    Assert.That(manager.Query(record => record.ID == managedRecordId)
                                       .PerformRead(record => record.Creator), Is.EqualTo(managedCreator));
                    Assert.That(manager.Query(record => record.ID == managedRecordId)
                                       .PerformRead(record => record.Hash), Is.EqualTo(managedHash));
                    Assert.That(manager.Query(record => record.ID == managedRecordId)
                                       .PerformRead(record => record.InstantiationInfo), Is.EqualTo(managedInstantiationInfo));
                    Assert.That(File.ReadAllBytes(Path.Combine(renamedRoot, "skin.ini")), Is.EqualTo(copiedSkinIni));
                });
            });
            AddStep("restart manager from renamed directory", () =>
            {
                startStage("renamed manager restart");
                manager.ShutdownManagedFolderMutations();
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                sourceChangedCount = 0;
                manager.SourceChanged += () => sourceChangedCount++;
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
                manager.SetSkinFromConfiguration(managedRecordId.ToString());
            });
            AddUntilStep("wait for renamed restart selection", () =>
                workspaceTask?.IsCompleted == true
                && manager.CurrentSkinInfo.Value.ID == managedRecordId
                && manager.CurrentSkin.Value.SkinInfo.ID == managedRecordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("mount renamed restart renderer host", () =>
            {
                finishStage("renamed manager restart", TimeSpan.FromSeconds(30));
                FolderSkinWorkspaceRecord managed = workspaceTask!.GetAwaiter().GetResult()
                                                                  .Single(record => record.RecordId == managedRecordId);
                Assert.Multiple(() =>
                {
                    Assert.That(managed.Kind, Is.EqualTo(FolderSkinWorkspaceRecordKind.Managed));
                    Assert.That(managed.CanOpenFolder, Is.True);
                    Assert.That(managed.CanRename, Is.True);
                    Assert.That(managed.CanDelete, Is.True);
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                });
                startStage("renamed restart renderer");
                Add(renamedRenderer = new JourneyRendererHost(manager, Clock.CurrentTime + 60_000, Clock.CurrentTime + 5_000));
            });
            AddUntilStep("wait for renamed renderer host", () => renamedRenderer.IsLoaded);
            AddStep("show renamed BMS renderer", () => renamedRenderer.ShowBms());
            AddUntilStep("wait for renamed BMS artifacts", () => renamedRenderer.BmsArtifactsLoaded);
            AddStep("assert renamed BMS artifacts", () => assertJourneyBmsRendererArtifacts(renamedRenderer));
            AddStep("show renamed mania renderer", () => renamedRenderer.ShowMania());
            AddUntilStep("wait for renamed mania artifacts", () => renamedRenderer.ManiaArtifactsLoaded);
            AddStep("assert renamed mania artifacts", () =>
            {
                assertJourneyManiaRendererArtifacts(renamedRenderer);
                Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                finishStage("renamed restart renderer", TimeSpan.FromSeconds(30));
            });
            AddStep("retire renamed renderer", () => renamedRenderer.Expire());
            AddUntilStep("wait for renamed renderer retirement", () => renamedRenderer.Parent == null);
            AddStep("query external and managed dropdown records", () =>
                dropdownTask = manager.GetAllUsableSkinsAsync());
            AddUntilStep("wait for final dropdown records", () => dropdownTask?.IsCompleted == true);
            AddStep("select external before making it non-current", () =>
            {
                IList<Live<SkinInfo>> records = dropdownTask!.GetAwaiter().GetResult();
                externalDropdown = records.Single(record => record.ID == externalRecordId);
                managedDropdown = records.Single(record => record.ID == managedRecordId);
                startStage("external selection");
                manager.CurrentSkinInfo.Value = externalDropdown;
            });
            AddUntilStep("wait for current external", () =>
                manager.CurrentSkinInfo.Value.ID == externalRecordId
                && manager.CurrentSkin.Value.SkinInfo.ID == externalRecordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("switch external non-current", () =>
            {
                finishStage("external selection", TimeSpan.FromSeconds(20));
                startStage("managed reselection");
                manager.CurrentSkinInfo.Value = managedDropdown;
            });
            AddUntilStep("wait for managed reselection", () =>
                manager.CurrentSkinInfo.Value.ID == managedRecordId
                && manager.CurrentSkin.Value.SkinInfo.ID == managedRecordId
                && manager.CurrentSkin.Value is BmsLegacySkin);
            AddStep("unregister non-current external record", () =>
            {
                finishStage("managed reselection", TimeSpan.FromSeconds(20));
                reselectedManagedSkin = manager.CurrentSkin.Value;
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for unregister-ready workspace", () => workspaceTask?.IsCompleted == true);
            AddStep("start external unregister", () =>
            {
                FolderSkinWorkspaceRecord external = workspaceTask!.GetAwaiter().GetResult()
                                                               .Single(record => record.RecordId == externalRecordId);
                Assert.That(external.CanUnregister, Is.True);
                startStage("external unregister");
                unregisterTask = manager.UnregisterExternalFolderAsync(externalRecordId);
            });
            AddUntilStep("wait for external unregister", () => unregisterTask?.IsCompleted == true);
            AddStep("assert unregister and query final managed row", () =>
            {
                finishStage("external unregister", TimeSpan.FromSeconds(20));
                Assert.Multiple(() =>
                {
                    Assert.That(unregisterTask!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.Query(record => record.ID == externalRecordId), Is.Null);
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(managedRecordId));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(reselectedManagedSkin));
                });
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for final managed row", () => workspaceTask?.IsCompleted == true);
            AddStep("delete final managed row", () =>
            {
                FolderSkinWorkspaceRecord managed = workspaceTask!.GetAwaiter().GetResult()
                                                                  .Single(record => record.RecordId == managedRecordId);
                Assert.That(managed.CanDelete, Is.True);
                startStage("managed delete");
                deleteTask = manager.DeleteSkinAsync(managedRecordId);
            });
            AddUntilStep("wait for managed delete first bounded slice", () =>
                deleteTask?.IsCompleted == true || stageWaits["managed delete"].Elapsed >= TimeSpan.FromSeconds(8));
            AddUntilStep("wait for managed delete second bounded slice", () =>
                deleteTask?.IsCompleted == true || stageWaits["managed delete"].Elapsed >= TimeSpan.FromSeconds(16));
            AddUntilStep("wait for managed delete third bounded slice", () =>
                deleteTask?.IsCompleted == true || stageWaits["managed delete"].Elapsed >= TimeSpan.FromSeconds(24));
            AddUntilStep("wait for managed delete fourth bounded slice", () =>
                deleteTask?.IsCompleted == true || stageWaits["managed delete"].Elapsed >= TimeSpan.FromSeconds(32));
            AddStep("assert complete journey residue and physical source proof", () =>
            {
                finishStage("managed delete", TimeSpan.FromSeconds(40));
                Assert.That(deleteTask!.IsCompleted, Is.True,
                    "Managed delete did not complete within its explicit four-slice test budget.");
                string externalPhysicalProofAfter = captureExternalPhysicalProofDigest(externalRoot);

                Assert.Multiple(() =>
                {
                    Assert.That(deleteTask!.GetAwaiter().GetResult(), Is.True);
                    Assert.That(manager.Query(record => record.ID == managedRecordId), Is.Null);
                    Assert.That(Directory.Exists(renamedRoot), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(SkinInfo.OMS_SKIN));
                    Assert.That(captureFolderInventory(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(externalPhysicalProofAfter, Is.EqualTo(externalPhysicalProofBefore));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });

                manager.ShutdownManagedFolderMutations();
            });

            void startStage(string stage) => stageWaits.Add(stage, Stopwatch.StartNew());

            void finishStage(string stage, TimeSpan upperBound)
            {
                Stopwatch wait = stageWaits[stage];
                wait.Stop();
                TestContext.Progress.WriteLine($"ManagedCopy journey {stage}: {wait.Elapsed}");
                Assert.That(wait.Elapsed, Is.LessThan(upperBound), $"{stage} exceeded its explicit test budget.");
            }
        }

        [Test]
        public void TestManagedCopyRejectsInvalidTargetAndExistingChildWithoutMutation()
        {
            string externalRoot = string.Empty;
            string[] externalSnapshot = Array.Empty<string>();
            string collisionName = string.Empty;
            string collisionRoot = string.Empty;
            Guid externalRecordId = Guid.Empty;
            Live<SkinInfo> originalInfo = null!;
            Skin originalSkin = null!;
            Guid[] managedRecordBaseline = Array.Empty<Guid>();
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? collisionTask = null;

            AddStep("register collision source", () =>
            {
                externalRoot = createExternalPackage(populateManagedCopyExternalPackage);
                externalSnapshot = snapshotPhysicalTree(externalRoot);
                originalInfo = manager.CurrentSkinInfo.Value;
                originalSkin = manager.CurrentSkin.Value;
                managedRecordBaseline = snapshotManagedRecordIds();
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep("wait for collision source registration", () => registrationTask?.IsCompleted == true);
            AddStep("resolve source and reject invalid direct children", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for collision source row", () => workspaceTask?.IsCompleted == true);
            AddStep("exercise invalid and colliding targets", () =>
            {
                externalRecordId = workspaceTask!.GetAwaiter().GetResult()
                                                 .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                                 .RecordId;

                foreach (string invalid in new[] { string.Empty, "../escape", "nested/name", "CON", "trailing." })
                {
                    Task<bool> rejected = manager.ImportManagedCopyAsync(externalRecordId, invalid);
                    Assert.That(rejected.IsCompleted, Is.True, invalid);
                    Assert.That(rejected.GetAwaiter().GetResult(), Is.False, invalid);
                }

                collisionName = $"collision-{Guid.NewGuid():N}";
                collisionRoot = LocalStorage.GetFullPath($"chartskin/{collisionName}");
                Directory.CreateDirectory(collisionRoot);
                File.WriteAllText(Path.Combine(collisionRoot, "foreign.keep"), "do-not-overwrite");
                collisionTask = manager.ImportManagedCopyAsync(externalRecordId, collisionName);
            });
            AddUntilStep("wait for collision rejection", () => collisionTask?.IsCompleted == true);
            AddStep("assert collision failed closed", () =>
            {
                Guid[] managedRecords = snapshotManagedRecordIds();

                Assert.Multiple(() =>
                {
                    Assert.That(collisionTask!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(File.ReadAllText(Path.Combine(collisionRoot, "foreign.keep")), Is.EqualTo("do-not-overwrite"));
                    Assert.That(managedRecords, Is.EqualTo(managedRecordBaseline));
                    Assert.That(snapshotPhysicalTree(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(originalInfo));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(originalSkin));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        [Test]
        public void TestManagedCopyCancellationBeforeFirstByteLeavesNoResidue()
        {
            string externalRoot = string.Empty;
            string[] externalSnapshot = Array.Empty<string>();
            string targetName = string.Empty;
            string targetRoot = string.Empty;
            Guid externalRecordId = Guid.Empty;
            SkinManagedFolderOperationCoordinator.Lease? heldLease = null;
            var cancellation = new CancellationTokenSource();
            Guid[] managedRecordBaseline = Array.Empty<Guid>();
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? copyTask = null;

            AddStep("register cancellation source", () =>
            {
                externalRoot = createExternalPackage(populateManagedCopyExternalPackage);
                externalSnapshot = snapshotPhysicalTree(externalRoot);
                managedRecordBaseline = snapshotManagedRecordIds();
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep("wait for cancellation source registration", () => registrationTask?.IsCompleted == true);
            AddStep("resolve cancellation source", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for cancellation source row", () => workspaceTask?.IsCompleted == true);
            AddStep("block authority and start cancellable copy", () =>
            {
                externalRecordId = workspaceTask!.GetAwaiter().GetResult()
                                                 .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                                 .RecordId;
                targetName = $"cancelled-{Guid.NewGuid():N}";
                targetRoot = LocalStorage.GetFullPath($"chartskin/{targetName}");
                heldLease = manager.ManagedFolderOperationCoordinator.Enter();
                copyTask = manager.ImportManagedCopyAsync(externalRecordId, targetName, cancellation.Token);
                Assert.That(copyTask.IsCompleted, Is.False);
            });
            AddStep("cancel before authority acquisition", () =>
            {
                cancellation.Cancel();
                heldLease!.Dispose();
                heldLease = null;
            });
            AddUntilStep("wait for cancelled managed copy", () => copyTask?.IsCompleted == true);
            AddStep("assert pre-write cancellation cleanup", () =>
            {
                Guid[] managedRecords = snapshotManagedRecordIds();

                Assert.Multiple(() =>
                {
                    Assert.That(copyTask!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(managedRecords, Is.EqualTo(managedRecordBaseline));
                    Assert.That(snapshotPhysicalTree(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
                cancellation.Dispose();
            });
        }

        [Test]
        public void TestManagedCopyCancellationAfterPreparedDurabilityRollsBackBeforeOperationRoot()
            => addManagedCopyDurablePhaseCancellationTest(SkinManagedFolderMutationPhase.Prepared);

        [TestCase("Present")]
        [TestCase("Invalid")]
        [TestCase("RealmDrift")]
        public void TestManagedCopyPreparedRollbackDeleteRequiresMissingJournalAndExactRealm(string fault)
            => addManagedCopyDurablePhaseCancellationTest(
                SkinManagedFolderMutationPhase.Prepared,
                fault);

        [Test]
        public void TestManagedCopyCancellationAfterCopyingDurabilityCleansExactOperationRoot()
            => addManagedCopyDurablePhaseCancellationTest(SkinManagedFolderMutationPhase.Copying);

        [TestCase("Present")]
        [TestCase("Invalid")]
        [TestCase("RealmDrift")]
        public void TestManagedCopyTerminalDeleteRequiresMissingJournalAndExactRealm(string fault)
        {
            string externalRoot = string.Empty;
            string targetName = string.Empty;
            string targetRoot = string.Empty;
            Guid externalRecordId = Guid.Empty;
            TerminalDeleteFaultJournalStore? journalStore = null;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? copyTask = null;
            Stopwatch? copyWait = null;

            AddStep($"register {fault} terminal source", () =>
            {
                externalRoot = createExternalPackage(populateManagedCopyExternalPackage);
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep($"wait for {fault} terminal source registration", () => registrationTask?.IsCompleted == true);
            AddStep($"resolve {fault} terminal source", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep($"wait for {fault} terminal source row", () => workspaceTask?.IsCompleted == true);
            AddStep($"inject {fault} after terminal delete", () =>
            {
                externalRecordId = workspaceTask!.GetAwaiter().GetResult()
                                                 .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                                 .RecordId;
                targetName = $"terminal-{fault.ToLowerInvariant()}-{Guid.NewGuid():N}";
                targetRoot = LocalStorage.GetFullPath($"chartskin/{targetName}");
                journalStore = new TerminalDeleteFaultJournalStore(
                    new SkinManagedFolderMutationJournalStore(LocalStorage),
                    fault,
                    terminal =>
                    {
                        if (fault != "RealmDrift"
                            || terminal.Phase != SkinManagedFolderMutationPhase.Committed)
                        {
                            return;
                        }

                        Realm.Write(realm =>
                        {
                            SkinInfo? published = realm.Find<SkinInfo>(terminal.OperationId);

                            if (published != null)
                                published.Name = $"drifted-{Guid.NewGuid():N}";
                        });
                    });
                replaceManagerJournalStore(manager, journalStore);
                copyWait = Stopwatch.StartNew();
                copyTask = manager.ImportManagedCopyAsync(externalRecordId, targetName);
            });
            AddUntilStep($"wait for {fault} terminal first bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(8));
            AddUntilStep($"wait for {fault} terminal second bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(16));
            AddUntilStep($"wait for {fault} terminal third bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(24));
            AddUntilStep($"wait for {fault} terminal fourth bounded slice", () =>
                copyTask?.IsCompleted == true || copyWait?.Elapsed >= TimeSpan.FromSeconds(32));
            AddStep($"assert {fault} terminal freeze", () =>
            {
                Assert.That(copyTask!.IsCompleted, Is.True,
                    $"ManagedCopy {fault} terminal fault exceeded its explicit four-slice test budget.");
                copyWait!.Stop();
                Assert.Multiple(() =>
                {
                    Assert.That(copyTask!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(journalStore!.DeleteCalls, Is.EqualTo(fault == "RealmDrift" ? 0 : 1));
                    Assert.That(
                        journalStore.DeletedPhase,
                        fault == "RealmDrift"
                            ? Is.Null
                            : Is.EqualTo(SkinManagedFolderMutationPhase.Committed));
                    Assert.That(manager.ManagedFolderOperationCoordinator.IsMutationBlocked, Is.True);
                    Assert.That(Directory.Exists(targetRoot), Is.True);
                    Assert.That(
                        journalStore.Load().Status,
                        Is.EqualTo(
                            fault switch
                            {
                                "Present" => SkinManagedFolderMutationJournalLoadStatus.Loaded,
                                "Invalid" => SkinManagedFolderMutationJournalLoadStatus.Invalid,
                                _ => SkinManagedFolderMutationJournalLoadStatus.Loaded,
                            }));
                });
            });
        }

        [Test]
        public void TestManagedCopyShutdownJoinsBlockedWorkerAndRejectsReentry()
        {
            string externalRoot = string.Empty;
            string[] externalSnapshot = Array.Empty<string>();
            string targetName = string.Empty;
            string targetRoot = string.Empty;
            Guid externalRecordId = Guid.Empty;
            SkinManagedFolderOperationCoordinator.Lease? heldLease = null;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? copyTask = null;
            Task<bool>? afterShutdown = null;
            Guid[] managedRecordBaseline = Array.Empty<Guid>();

            AddStep("register shutdown source", () =>
            {
                externalRoot = createExternalPackage(populateManagedCopyExternalPackage);
                externalSnapshot = snapshotPhysicalTree(externalRoot);
                managedRecordBaseline = snapshotManagedRecordIds();
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep("wait for shutdown source registration", () => registrationTask?.IsCompleted == true);
            AddStep("resolve shutdown source", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep("wait for shutdown source row", () => workspaceTask?.IsCompleted == true);
            AddStep("block authority and start shutdown-owned copy", () =>
            {
                externalRecordId = workspaceTask!.GetAwaiter().GetResult()
                                                 .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                                 .RecordId;
                targetName = $"shutdown-{Guid.NewGuid():N}";
                targetRoot = LocalStorage.GetFullPath($"chartskin/{targetName}");
                heldLease = manager.ManagedFolderOperationCoordinator.Enter();
                copyTask = manager.ImportManagedCopyAsync(externalRecordId, targetName);
                Assert.That(copyTask.IsCompleted, Is.False);
            });
            AddStep("shutdown and synchronously join copy", () =>
            {
                try
                {
                    manager.ShutdownManagedFolderMutations();
                }
                finally
                {
                    heldLease!.Dispose();
                    heldLease = null;
                }

                afterShutdown = manager.ImportManagedCopyAsync(
                    externalRecordId,
                    $"after-shutdown-{Guid.NewGuid():N}");
            });
            AddStep("assert shutdown terminal state and reentry rejection", () =>
            {
                Guid[] managedRecords = snapshotManagedRecordIds();

                Assert.Multiple(() =>
                {
                    Assert.That(copyTask!.IsCompleted, Is.True);
                    Assert.That(copyTask.GetAwaiter().GetResult(), Is.False);
                    Assert.That(afterShutdown!.IsCompleted, Is.True);
                    Assert.That(afterShutdown.GetAwaiter().GetResult(), Is.False);
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(managedRecords, Is.EqualTo(managedRecordBaseline));
                    Assert.That(snapshotPhysicalTree(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(new SkinManagedFolderMutationJournalStore(LocalStorage).Load().Status,
                        Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                });
            });
        }

        private static void populateManagedCopyExternalPackage(string packageRoot)
        {
            createCompletePackage(packageRoot);
            Directory.CreateDirectory(Path.Combine(packageRoot, "empty", "nested"));
        }

        private static void populateManagedCopyRendererPackage(string packageRoot)
        {
            createRendererJourneyPackage(packageRoot);
            Directory.CreateDirectory(Path.Combine(packageRoot, "empty", "nested"));
        }

        private string captureExternalPhysicalProofDigest(string externalRoot)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(
                new SkinInfo
                {
                    FilesystemStoragePath = externalRoot,
                    IsExternalFilesystemStorage = true,
                },
                LocalStorage);
            Assert.Multiple(() =>
            {
                Assert.That(resolution.Authority, Is.EqualTo(SkinFilesystemStorageAuthority.ExternalFolder));
                Assert.That(resolution.ExternalCaptureRequest, Is.Not.Null);
            });

            SkinExternalFolderAuthorityCaptureResult capture = new SkinExternalFolderCaptureService().OpenAuthority(
                resolution.ExternalCaptureRequest);
            Assert.That(capture.IsSuccess, Is.True, capture.ToString());

            using ISkinExternalFolderAuthoritySession authority = capture.Session!;
            authority.Validate(CancellationToken.None);
            return authority.PhysicalProof.Digest;
        }

        private void addManagedCopyDurablePhaseCancellationTest(
            SkinManagedFolderMutationPhase cancellationPhase,
            string? terminalDeleteFault = null)
        {
            string externalRoot = string.Empty;
            string[] externalSnapshot = Array.Empty<string>();
            string targetName = string.Empty;
            string targetRoot = string.Empty;
            Guid externalRecordId = Guid.Empty;
            var cancellation = new CancellationTokenSource();
            PhaseCancellingJournalStore? journalStore = null;
            TerminalDeleteFaultJournalStore? terminalFaultStore = null;
            Task<bool>? registrationTask = null;
            Task<IReadOnlyList<FolderSkinWorkspaceRecord>>? workspaceTask = null;
            Task<bool>? copyTask = null;
            Guid[] managedRecordBaseline = Array.Empty<Guid>();
            string[] stagingRootBaseline = Array.Empty<string>();

            AddStep($"register {cancellationPhase} cancellation source", () =>
            {
                externalRoot = createExternalPackage(populateManagedCopyExternalPackage);
                externalSnapshot = snapshotPhysicalTree(externalRoot);
                managedRecordBaseline = snapshotManagedRecordIds();
                stagingRootBaseline = snapshotManagedCopyStagingRoots();
                registrationTask = manager.RegisterExternalFolderAsync(externalRoot);
            });
            AddUntilStep($"wait for {cancellationPhase} source registration", () => registrationTask?.IsCompleted == true);
            AddStep($"resolve {cancellationPhase} cancellation source", () =>
            {
                Assert.That(registrationTask!.GetAwaiter().GetResult(), Is.True);
                workspaceTask = manager.GetFolderSkinWorkspaceRecordsAsync();
            });
            AddUntilStep($"wait for {cancellationPhase} source row", () => workspaceTask?.IsCompleted == true);
            AddStep($"cancel after durable {cancellationPhase}", () =>
            {
                externalRecordId = workspaceTask!.GetAwaiter().GetResult()
                                                 .Single(record => record.Kind == FolderSkinWorkspaceRecordKind.External)
                                                 .RecordId;
                targetName = $"phase-cancel-{Guid.NewGuid():N}";
                targetRoot = LocalStorage.GetFullPath($"chartskin/{targetName}");
                ISkinManagedFolderMutationJournalStore replacement = new PhaseCancellingJournalStore(
                    new SkinManagedFolderMutationJournalStore(LocalStorage),
                    cancellation,
                    cancellationPhase);
                journalStore = (PhaseCancellingJournalStore)replacement;

                if (terminalDeleteFault != null)
                {
                    replacement = terminalFaultStore = new TerminalDeleteFaultJournalStore(
                        replacement,
                        terminalDeleteFault,
                        terminal =>
                        {
                            if (terminalDeleteFault != "RealmDrift"
                                || terminal.Phase != SkinManagedFolderMutationPhase.RolledBack)
                            {
                                return;
                            }

                            Realm.Write(realm =>
                            {
                                SkinInfo? external = realm.Find<SkinInfo>(externalRecordId);

                                if (external != null)
                                    external.Name = $"drifted-{Guid.NewGuid():N}";
                            });
                        });
                }

                replaceManagerJournalStore(manager, replacement);
                copyTask = manager.ImportManagedCopyAsync(externalRecordId, targetName, cancellation.Token);
            });
            AddUntilStep($"wait for {cancellationPhase} cancellation", () => copyTask?.IsCompleted == true);
            AddStep($"assert {cancellationPhase} rollback residue", () =>
            {
                string[] operationRoots = snapshotManagedCopyStagingRoots();
                Guid[] managedRecords = snapshotManagedRecordIds();

                Assert.Multiple(() =>
                {
                    Assert.That(copyTask!.GetAwaiter().GetResult(), Is.False);
                    Assert.That(cancellation.IsCancellationRequested, Is.True);
                    Assert.That(journalStore!.Writes.Select(journal => journal.Phase),
                        Does.Contain(cancellationPhase));
                    Assert.That(
                        journalStore.Writes.Last().Phase,
                        Is.EqualTo(SkinManagedFolderMutationPhase.RolledBack));
                    if (terminalFaultStore != null)
                    {
                        Assert.That(
                            terminalFaultStore.DeleteCalls,
                            Is.EqualTo(terminalDeleteFault == "RealmDrift" ? 0 : 1));
                    }
                    Assert.That(operationRoots, Is.EqualTo(stagingRootBaseline));
                    Assert.That(Directory.Exists(targetRoot), Is.False);
                    Assert.That(managedRecords, Is.EqualTo(managedRecordBaseline));
                    Assert.That(snapshotPhysicalTree(externalRoot), Is.EqualTo(externalSnapshot));
                    Assert.That(
                        (terminalFaultStore?.Load() ?? journalStore.Load()).Status,
                        Is.EqualTo(
                            terminalDeleteFault switch
                            {
                                "Present" => SkinManagedFolderMutationJournalLoadStatus.Loaded,
                                "Invalid" => SkinManagedFolderMutationJournalLoadStatus.Invalid,
                                "RealmDrift" => SkinManagedFolderMutationJournalLoadStatus.Loaded,
                                _ => SkinManagedFolderMutationJournalLoadStatus.Missing,
                            }));
                    Assert.That(
                        manager.ManagedFolderOperationCoordinator.IsMutationBlocked,
                        Is.EqualTo(terminalDeleteFault != null));
                });
                cancellation.Dispose();
            });
        }

        private static string[] snapshotPhysicalTree(string root)
        {
            IEnumerable<string> directories = Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                                                       .Select(path => $"D:{Path.GetRelativePath(root, path).Replace('\\', '/')}");
            IEnumerable<string> files = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                                                 .Select(path =>
                                                 {
                                                     string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                                                     byte[] bytes = File.ReadAllBytes(path);
                                                     string digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                                                     return $"F:{relative}:{bytes.Length}:{digest}";
                                                 });
            return directories.Concat(files).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        private Guid[] snapshotManagedRecordIds()
            => Realm.Run(realm => realm.All<SkinInfo>()
                .Where(record => record.FilesystemStorageAuthorityOwner == SkinManagedFolderScanner.AUTHORITY_OWNER)
                .AsEnumerable()
                .Select(record => record.ID)
                .OrderBy(id => id)
                .ToArray());

        private string[] snapshotManagedCopyStagingRoots()
        {
            string stagingRoot = LocalStorage.GetFullPath("skin-mutation-staging");
            return Directory.Exists(stagingRoot)
                ? Directory.GetDirectories(stagingRoot, "*", SearchOption.TopDirectoryOnly)
                           .Select(path => Path.GetFileName(path)!)
                           .OrderBy(name => name, StringComparer.Ordinal)
                           .ToArray()
                : Array.Empty<string>();
        }

        private static void replaceManagerJournalStore(
            SkinManager target,
            ISkinManagedFolderMutationJournalStore replacement)
        {
            FieldInfo? field = typeof(SkinManager).GetField(
                "managedFolderMutationJournalStore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field!.SetValue(target, replacement);
        }

        private sealed class PhaseCancellingJournalStore : ISkinManagedFolderMutationJournalStore
        {
            private readonly ISkinManagedFolderMutationJournalStore inner;
            private readonly CancellationTokenSource cancellation;
            private readonly SkinManagedFolderMutationPhase cancellationPhase;
            private int cancellationTriggered;

            public List<SkinManagedFolderMutationJournal> Writes { get; } = new List<SkinManagedFolderMutationJournal>();

            public PhaseCancellingJournalStore(
                ISkinManagedFolderMutationJournalStore inner,
                CancellationTokenSource cancellation,
                SkinManagedFolderMutationPhase cancellationPhase)
            {
                this.inner = inner;
                this.cancellation = cancellation;
                this.cancellationPhase = cancellationPhase;
            }

            public SkinManagedFolderMutationJournalLoadResult Load() => inner.Load();

            public void Write(SkinManagedFolderMutationJournal journal)
            {
                inner.Write(journal);
                Writes.Add(journal);

                if (journal.Kind == SkinManagedFolderMutationKind.ManagedCopy
                    && journal.Phase == cancellationPhase
                    && Interlocked.Exchange(ref cancellationTriggered, 1) == 0)
                {
                    cancellation.Cancel();
                }
            }

            public void Delete(SkinManagedFolderMutationJournal expectedJournal)
                => inner.Delete(expectedJournal);
        }

        private sealed class TerminalDeleteFaultJournalStore : ISkinManagedFolderMutationJournalStore
        {
            private readonly ISkinManagedFolderMutationJournalStore inner;
            private readonly string fault;
            private readonly Action<SkinManagedFolderMutationJournal> beforeTerminalValidation;
            private SkinManagedFolderMutationJournalLoadResult? injectedLoad;
            private int terminalValidationFaultInjected;

            public int DeleteCalls { get; private set; }

            public SkinManagedFolderMutationPhase? DeletedPhase { get; private set; }

            public TerminalDeleteFaultJournalStore(
                ISkinManagedFolderMutationJournalStore inner,
                string fault,
                Action<SkinManagedFolderMutationJournal> beforeTerminalValidation)
            {
                this.inner = inner;
                this.fault = fault;
                this.beforeTerminalValidation = beforeTerminalValidation;
            }

            public SkinManagedFolderMutationJournalLoadResult Load()
            {
                if (injectedLoad != null)
                    return injectedLoad;

                SkinManagedFolderMutationJournalLoadResult loaded = inner.Load();

                if (fault == "RealmDrift"
                    && loaded.IsLoaded
                    && loaded.Journal!.Phase is SkinManagedFolderMutationPhase.Committed
                        or SkinManagedFolderMutationPhase.RolledBack
                    && Interlocked.Exchange(ref terminalValidationFaultInjected, 1) == 0)
                {
                    beforeTerminalValidation(loaded.Journal);
                }

                return loaded;
            }

            public void Write(SkinManagedFolderMutationJournal journal)
            {
                injectedLoad = null;
                inner.Write(journal);
            }

            public void Delete(SkinManagedFolderMutationJournal expectedJournal)
            {
                inner.Delete(expectedJournal);
                DeleteCalls++;
                DeletedPhase = expectedJournal.Phase;
                injectedLoad = fault switch
                {
                    "Present" => new SkinManagedFolderMutationJournalLoadResult(
                        SkinManagedFolderMutationJournalLoadStatus.Loaded,
                        expectedJournal),
                    "Invalid" => new SkinManagedFolderMutationJournalLoadResult(
                        SkinManagedFolderMutationJournalLoadStatus.Invalid),
                    _ => null,
                };
            }
        }
    }
}
