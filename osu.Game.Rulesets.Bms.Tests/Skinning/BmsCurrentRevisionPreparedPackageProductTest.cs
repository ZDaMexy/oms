// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Windows;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestAuthorAtomicFileReplacementKeepsActiveAUntilSettingsPublishesB(MaterialDiagnosticPackageSource source)
        {
            var selection = new C6ScriptSelection();
            ExactLayoutJourneyHost renderer = null!;
            GameplaySkinSceneRuntimeHost bms = null!;
            GameplaySkinSceneRuntimeHost mania = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            GameplaySkinPreparedPackage packageA = null!;
            GameplaySkinScriptAuthorization authorizationA = null!;
            GameplaySkinScriptAuthorization authorizationB = null!;
            Dictionary<string, byte[]> bytesA = null!;
            Dictionary<string, byte[]> bytesB = null!;
            Task? authorization = null;

            addSelectC6ScriptPackage(selection, source, writeC6ScriptPackage);
            AddStep("retain the exact A receipt and authorize its script", () =>
            {
                revisionA = manager.CurrentRevision;
                packageA = revisionA.Owner.PreparedGameplaySkinPackage!;
                authorizationA = packageA.ScriptAuthorization!;
                bytesA = Directory.GetFiles(selection.Root).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
                Assert.That(bytesA.Count, Is.EqualTo(5));
                authorization = grantC6Script(authorizationA);
            });
            AddUntilStep("A authorization is durable", () => authorization?.IsCompleted == true);
            AddStep("mount both actual ruleset consumers of A", () =>
            {
                authorization!.GetAwaiter().GetResult();
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
                renderer.ShowMania();
            });
            AddUntilStep("both A scene consumers are ready", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady;
            });
            AddStep("send the first real input to A", () => c6Input(renderer, true));
            AddUntilStep("both A scripts apply their first fifteen degree step", () => c6Rotation(bms) == 15 && c6Rotation(mania) == 15);
            AddStep("author atomically replaces each source file with B", () =>
            {
                string preparedRoot = LocalStorage.GetFullPath($"c6-atomic-author-b-{Guid.NewGuid():N}");
                writeC6ScriptPackage(preparedRoot);
                string iniPath = Path.Combine(preparedRoot, "skin.ini");
                File.WriteAllText(iniPath, File.ReadAllText(iniPath).Replace("C6 script production candidate", "C6 atomic-save B", StringComparison.Ordinal));
                string scriptPath = Path.Combine(preparedRoot, GameplaySkinSceneContracts.SCRIPT_FILE_NAME);
                File.WriteAllText(scriptPath, File.ReadAllText(scriptPath).Replace("add spin spin 15", "add spin spin 37", StringComparison.Ordinal));
                string manifestPath = Path.Combine(preparedRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME);
                JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
                manifest["resources"]![0]!["id"] = "texture.glow-b";
                File.WriteAllText(manifestPath, manifest.ToString());
                string scenePath = Path.Combine(preparedRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME);
                JObject scene = JObject.Parse(File.ReadAllText(scenePath));
                scene["root"]!["resource"] = "texture.glow-b";
                scene["root"]!["properties"]!["rotation"] = 7;
                File.WriteAllText(scenePath, scene.ToString());
                File.WriteAllBytes(Path.Combine(preparedRoot, "glow.png"), createPng(new Rgba32(220, 70, 30, 255)));
                bytesB = Directory.GetFiles(preparedRoot).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
                Assert.That(bytesB.Keys, Is.EquivalentTo(bytesA.Keys));

                // This is the author's atomic-save operation on each file, not a filesystem transaction across
                // the five files. OMS keeps serving immutable A until the later explicit whole-package reload.
                foreach ((string name, byte[] bytes) in bytesB)
                {
                    string target = Path.Combine(selection.Root, name);
                    string temporary = target + ".atomic-save.tmp";
                    File.WriteAllBytes(temporary, bytes);
                    File.Replace(temporary, target, null);
                    Assert.That(File.Exists(temporary), Is.False);
                    Assert.That(File.ReadAllBytes(target), Is.EqualTo(bytes));
                }

                Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage, Is.SameAs(packageA));
                Assert.That(packageA.ScriptAuthorization, Is.SameAs(authorizationA));
                Assert.That(authorizationA.RequiredSatisfied, Is.True);
                assertOwnerBytes(revisionA.Owner, bytesA);
            });
            AddStep("release the original real input after atomic saves", () => c6Input(renderer, false));
            AddStep("send another real input while immutable A remains selected", () => c6Input(renderer, true));
            AddUntilStep("both active hosts still execute A and never the live B script", () => c6Rotation(bms) == 30 && c6Rotation(mania) == 30);
            AddStep("detach gameplay before requesting the Settings-only reload", () =>
            {
                c6Input(renderer, false);
                renderer.Expire();
            });
            AddUntilStep("the last A gameplay consumer releases its lease", () => renderer.Parent == null && revisionA.ConsumersDetached.IsCompleted);
            AddStep("mount the real Settings reload caller", () => Add(caller = new FullSkinSettingsCallerHost(manager)));
            AddUntilStep("Settings enables safe manual reload", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("reload the completed atomic saves through Settings", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("the exact whole B package publishes", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA) && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("check ini manifest scene script and material all belong to B", () =>
            {
                Skin ownerB = manager.CurrentSkin.Value;
                GameplaySkinPreparedPackage packageB = ownerB.PreparedGameplaySkinPackage!;
                authorizationB = packageB.ScriptAuthorization!;
                Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(revisionA.RecordId));
                Assert.That(packageB.ContentRevision, Is.EqualTo(manager.CurrentRevision.ContentRevision));
                Assert.That(packageB.ContentRevision, Is.Not.EqualTo(packageA.ContentRevision));
                assertOwnerBytes(ownerB, bytesB);
                Assert.That(ownerB.Configuration.SkinInfo.Name, Is.EqualTo("C6 atomic-save B"));
                Assert.That(packageB.Manifest!.Resources.Single().Id, Is.EqualTo("texture.glow-b"));
                Assert.That(packageB.Document!.Root.ResourceId, Is.EqualTo("texture.glow-b"));
                Assert.That(packageB.ScriptProgram!.SourceDigest, Is.EqualTo(Convert.ToHexString(SHA256.HashData(bytesB[GameplaySkinSceneContracts.SCRIPT_FILE_NAME]))));
                Assert.That(packageB.Resources.Single().ContentRevision, Is.EqualTo(Convert.ToHexString(SHA256.HashData(bytesB["glow.png"])).ToLowerInvariant()));
                Assert.That(authorizationB.Key, Is.Not.EqualTo(authorizationA.Key));
                Assert.That(authorizationB.RequiredSatisfied, Is.False);
                bms = null!;
                mania = null!;
                Add(renderer = new ExactLayoutJourneyHost(manager));
                renderer.ShowBms();
                renderer.ShowMania();
            });
            AddUntilStep("both late consumers render B's unauthorised scene baseline", () =>
            {
                if (!renderer.BmsReady || !renderer.ManiaReady)
                    return false;
                bms ??= renderer.BmsDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                mania ??= renderer.ManiaDrawable.ChildrenOfType<GameplaySkinSceneRuntimeHost>().Single();
                return bms.IsSceneReady && mania.IsSceneReady && c6Rotation(bms) == 7 && c6Rotation(mania) == 7;
            });
            AddStep("authorize the exact newly published B script", () => authorization = grantC6Script(authorizationB));
            AddUntilStep("B authorization is durable", () => authorization!.IsCompleted);
            AddStep("send the real input to both B consumers", () =>
            {
                authorization!.GetAwaiter().GetResult();
                c6Input(renderer, true);
            });
            AddUntilStep("both actual hosts consume B's thirty-seven degree script", () => c6Rotation(bms) == 37 && c6Rotation(mania) == 37);
            AddUntilStep("A retires only after its production consumers detach", () => revisionA.Retired.IsCompleted);
            AddStep("release and detach both B gameplay trees", () =>
            {
                c6Input(renderer, false);
                renderer.Expire();
                caller.Expire();
            });
            AddUntilStep("the new production trees release their revision leases", () => renderer.Parent == null && caller.Parent == null);

            static void assertOwnerBytes(Skin owner, Dictionary<string, byte[]> expected)
            {
                foreach ((string name, byte[] bytes) in expected)
                {
                    Assert.That(owner.TryCaptureGameplaySkinResource(name, 1024 * 1024, out byte[] captured), Is.True);
                    Assert.That(captured, Is.EqualTo(bytes), $"The exact publication must retain its own {name} bytes.");
                }
            }
        }

        [Test]
        public void TestNewerOrdinaryAuthorRequestAtFinalCoordinatorBoundaryCannotPublishStalePreparedOwner()
        {
            Live<SkinInfo> first = null!;
            Live<SkinInfo> second = null!;
            SkinCurrentRevision revisionA = null!;
            var completions = new ConcurrentQueue<Action>();
            bool latestEntered = false;
            AddStep("prepare two actual ordinary author packages and delay publication", () =>
            {
                string firstRoot = LocalStorage.GetFullPath($"c6-first-{Guid.NewGuid():N}");
                string secondRoot = LocalStorage.GetFullPath($"c6-second-{Guid.NewGuid():N}");
                writeC6ScriptPackage(firstRoot);
                writeC6ScriptPackage(secondRoot);
                File.AppendAllText(Path.Combine(secondRoot, "skin.ini"), "\n; second package\n");
                first = createRealmRevisionCandidate(firstRoot);
                second = createRealmRevisionCandidate(secondRoot);
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionCompletionSchedule = callback => completions.Enqueue(callback);
                manager.CurrentSkinInfo.Value = first;
            });
            AddUntilStep("first real compiler worker reaches publication", () => completions.Count == 1);
            AddStep("new ordinary caller wins coordinator admission before old final commit", () =>
            {
                manager.SelectionRequestBeforeCommitLock = target =>
                {
                    if (target.ID != first.ID || latestEntered)
                        return;
                    latestEntered = true;
                    // Ordinary selection retains its supported off-update caller. The old prepared completion
                    // stays on the real update thread while this newer request crosses the shared coordinator.
                    Task latest = Task.Run(() => manager.SetSkinFromConfiguration(second.ID.ToString()));
                    Assert.That(latest.Wait(TimeSpan.FromSeconds(5)), Is.True);
                    latest.GetAwaiter().GetResult();
                };
                Assert.That(completions.TryDequeue(out Action? completeFirst), Is.True);
                completeFirst!();
                Assert.That(latestEntered, Is.True);
                Assert.That(manager.CurrentRevision, Is.SameAs(revisionA), "The superseded package must never become visible.");
                manager.SelectionRequestBeforeCommitLock = _ => { };
            });
            AddUntilStep("latest compiler produces its publication receipt", () => completions.Count == 1);
            AddStep("publish only the latest owner", () =>
            {
                Assert.That(completions.TryDequeue(out Action? completeSecond), Is.True);
                completeSecond!();
                Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(second.ID));
                Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptProgram, Is.Not.Null);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestOrdinaryAuthorSelectionAndReloadRejectOversizedBlobBeforeAllocatingIt(bool reload)
        {
            var selection = new C6ScriptSelection();
            SkinCurrentRevision revisionA = null!;
            Task<SkinCurrentRevisionReloadResult>? operation = null;
            long allocatedBefore = 0;
            string blobPath = string.Empty;
            byte[] originalBlob = Array.Empty<byte>();
            addSelectC6ScriptPackage(selection, MaterialDiagnosticPackageSource.OrdinaryRealm, root =>
            {
                writeC6ScriptPackage(root);
                File.WriteAllBytes(Path.Combine(root, "payload.bin"), new byte[] { 42 });
            });
            if (!reload)
            {
                AddStep("detach author package before new selection", () => manager.SetSkinFromConfiguration(SkinInfo.OMS_SKIN.ToString()));
                AddUntilStep("protected A selected", () => manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            }
            AddStep("replace one local content blob with an oversized sparse file", () =>
            {
                revisionA = manager.CurrentRevision;
                string hash = selection.Candidate.PerformRead(info => info.Files.Single(file => file.Filename == "payload.bin").File.Hash);
                blobPath = LocalStorage.GetFullPath("files/" + new RealmFile { Hash = hash }.GetStoragePath());
                originalBlob = File.ReadAllBytes(blobPath);
                using (var stream = new FileStream(blobPath, FileMode.Open, FileAccess.Write, FileShare.Read))
                    stream.SetLength(SkinPackageRevisionCapsuleLimits.Default.MaxFileBytes + 1);
                allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
                if (reload)
                    operation = manager.ReloadCurrentRevisionAsync();
                else
                    manager.SetSkinFromConfiguration(selection.Candidate.ID.ToString());
            });
            AddUntilStep("oversized source is rejected", () => reload
                ? operation?.IsCompleted == true
                : manager.LastSelectionRejectionReason == SkinSelectionRejectionReason.PreparationFailed);
            AddStep("budget rejects before allocating the entire untrusted blob and keeps A", () =>
            {
                long allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
                File.WriteAllBytes(blobPath, originalBlob);
                Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                if (reload)
                    Assert.That(operation!.GetAwaiter().GetResult(), Is.EqualTo(SkinCurrentRevisionReloadResult.SourceChanged));
                // Includes the real test host and scheduler while the worker runs; the rejected 64 MiB blob alone
                // exceeds this allowance. No large buffer is necessary to inspect a local file's declared length.
                Assert.That(allocated, Is.LessThan(32L * 1024 * 1024), $"Allocated {allocated} bytes before rejection.");
            });
        }

        [Test]
        public void TestSettingsReloadWithoutGameplayHostRejectsMalformedWholePackageAndKeepsExactA(
            [Values(0, 1, 2)] int source,
            [Values("manifest", "missing-scene", "script", "bytecode", "resource")] string failure)
        {
            var context = new CurrentRevisionProductContext();
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            string sourceRoot = string.Empty;
            int prepareCount = 0;

            if (source == 0)
            {
                AddStep("import ordinary whole-package A", () =>
                {
                    sourceRoot = LocalStorage.GetFullPath($"whole-package-{Guid.NewGuid():N}");
                    writeRevisionPackage(sourceRoot, "A", new Rgba32(240, 40, 80, 255));
                    candidate = createRealmRevisionCandidate(sourceRoot);
                    manager.CurrentSkinInfo.Value = candidate;
                });
                AddUntilStep("wait for exact ordinary A", () =>
                    manager.CurrentSkinInfo.Value.ID == candidate.ID
                    && manager.CurrentSkin.Value.PackageContentRevision != null);
            }
            else
            {
                addSelectRevisionA(context, source == 2);
                AddStep("retain registered source", () => sourceRoot = context.PackageRoot);
            }

            AddStep("mount only real Settings caller", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.CurrentRevisionPrepareStarted = () => prepareCount++;
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for menu reload button", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("damage author manifest and request menu reload", () =>
            {
                writeC6AuthorPackage(sourceRoot, failure);
                if (source == 0)
                    replaceRealmRevisionFiles(candidate.ID, sourceRoot);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("wait for whole-package rejection", () => prepareCount == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("exact A survives without synthetic scene participant", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(revisionA.Owner));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    if (failure is "script" or "bytecode")
                    {
                        Assert.That(manager.LastScriptPreparationDiagnostic, Does.StartWith("OMS-SKIN-SCRIPT-"));
                        Assert.That(manager.LastScriptPreparationDiagnostic, Does.Contain(": line "));
                        Assert.That(manager.LastScriptPreparationDiagnostic, Does.Not.Contain(sourceRoot));
                    }
                });
            });
            AddStep("repair whole author package and retry", () =>
            {
                writeC6AuthorPackage(sourceRoot, null);
                if (source == 0)
                    replaceRealmRevisionFiles(candidate.ID, sourceRoot);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("repaired package publishes through Settings", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA) && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("new owner retains complete prepared author content", () =>
            {
                Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage?.Document, Is.Not.Null);
                Assert.That(manager.LastScriptPreparationDiagnostic, Is.Null);
            });
        }

        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder)]
        public void TestScriptAuthorizationRestartsForExactPackageAndInvalidatesOnScriptOrPackageReload(MaterialDiagnosticPackageSource source)
        {
            var selection = new C6ScriptSelection();
            GameplaySkinScriptAuthorization original = null!;
            GameplaySkinScriptAuthorization restarted = null!;
            GameplaySkinScriptAuthorization scriptChanged = null!;
            Task? consent = null;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision beforeReload = null!;

            addSelectC6ScriptPackage(selection, source, writeC6ScriptPackage);
            AddStep("grant the selected script's actual requests", () =>
            {
                original = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                consent = grantC6Script(original);
            });
            AddUntilStep("authorization is durably saved", () => consent?.IsCompleted == true);
            AddStep("restart manager with the same persisted selection", () =>
            {
                consent!.GetAwaiter().GetResult();
                Assert.That(original.RequiredSatisfied, Is.True);
                manager.ShutdownManagedFolderMutations();
                Assert.That(original.RequiredSatisfied, Is.False, "shutdown must invalidate every previous runtime token");
                manager = new SkinManager(LocalStorage, Realm, host, Resources, Audio, Scheduler);
                manager.SetSkinFromConfiguration(selection.Candidate.ID.ToString());
            });
            AddUntilStep("same author package selected after restart", () =>
                manager.CurrentSkinInfo.Value.ID == selection.Candidate.ID
                && manager.CurrentSkin.Value.PreparedGameplaySkinPackage?.ScriptAuthorization != null);
            AddStep("exact identity restores authorization", () =>
            {
                restarted = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(restarted, Is.Not.SameAs(original));
                Assert.That(restarted.Key, Is.EqualTo(original.Key));
                Assert.That(restarted.RequiredSatisfied, Is.True);
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("Settings can reload the restarted package", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("edit script and reload the same record", () =>
            {
                beforeReload = manager.CurrentRevision;
                File.AppendAllText(Path.Combine(selection.Root, GameplaySkinSceneContracts.SCRIPT_FILE_NAME), "\n# script revision B\n");
                if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
                    replaceRealmRevisionFiles(selection.Candidate.ID, selection.Root);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("changed script publication completes", () =>
                !ReferenceEquals(manager.CurrentRevision, beforeReload) && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("same record with changed compiled content requires fresh permission", () =>
            {
                scriptChanged = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(scriptChanged.Key, Is.Not.EqualTo(restarted.Key));
                Assert.That(scriptChanged.RequiredSatisfied, Is.False);
                Assert.That(scriptChanged.Requests.All(request => scriptChanged.GetChoice(request.Id) == GameplaySkinScriptAuthorizationChoice.NotDecided), Is.True);
                consent = grantC6Script(scriptChanged);
            });
            AddUntilStep("changed script authorization saves", () => consent!.IsCompleted);
            AddStep("edit only skin.ini and reload same script", () =>
            {
                consent!.GetAwaiter().GetResult();
                beforeReload = manager.CurrentRevision;
                File.AppendAllText(Path.Combine(selection.Root, "skin.ini"), "\n; package-only revision C\n");
                if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
                    replaceRealmRevisionFiles(selection.Candidate.ID, selection.Root);
                caller.ReloadCurrentButton.TriggerClick();
            });
            AddUntilStep("whole-package revision completes", () =>
                !ReferenceEquals(manager.CurrentRevision, beforeReload) && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("full package identity invalidates unchanged-script grants", () =>
            {
                GameplaySkinScriptAuthorization changed = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(changed.Key, Is.Not.EqualTo(scriptChanged.Key));
                Assert.That(changed.RequiredSatisfied, Is.False);
                Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(selection.Candidate.ID));
            });
        }

        [Test]
        public void TestManagedDirectoryRenamePreservesScriptIdentityAndPermissionAfterReselection()
        {
            var selection = new C6ScriptSelection();
            Task? consent = null;
            Task<SkinManagedFolderRenameOperationResult>? rename = null;
            GameplaySkinScriptAuthorization original = null!;
            string targetName = $"c6-renamed-{Guid.NewGuid():N}";
            Task<SkinManagedFolderScanResult>? scan = null;
            string relativePath = $"chartskin/c6-original-{Guid.NewGuid():N}";
            AddStep("write a real managed author workspace", () =>
            {
                selection.Root = LocalStorage.GetFullPath(relativePath);
                writeC6ScriptPackage(selection.Root);
                scan = Task.Run(() => new SkinManagedFolderScanner(Realm,
                    new WindowsSkinManagedFolderDiscoverySource(LocalStorage), manager.ManagedFolderOperationCoordinator).Scan());
            });
            AddUntilStep("native scanner captures and registers content", () => scan?.IsCompleted == true);
            AddStep("select the scanner's authoritative record", () =>
            {
                Assert.That(scan!.GetAwaiter().GetResult().IsSuccess, Is.True, scan.GetAwaiter().GetResult().ToString());
                selection.Candidate = manager.Query(info => info.FilesystemStoragePath == relativePath);
                manager.CurrentSkinInfo.Value = selection.Candidate;
            });
            AddUntilStep("registered managed script selects", () =>
                manager.CurrentSkinInfo.Value.ID == selection.Candidate.ID && manager.CurrentSkin.Value.PreparedGameplaySkinPackage != null);
            AddStep("authorize managed author package", () =>
            {
                original = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                consent = grantC6Script(original);
            });
            AddUntilStep("managed permission saved", () => consent?.IsCompleted == true);
            AddStep("rename the actual managed directory", () =>
            {
                consent!.GetAwaiter().GetResult();
                rename = manager.RenameManagedFolderAsync(selection.Candidate.ID, targetName);
            });
            AddUntilStep("managed rename commits", () => rename?.IsCompleted == true);
            AddStep("active immutable script keeps its exact permission", () =>
            {
                SkinManagedFolderRenameOperationResult result = rename!.GetAwaiter().GetResult();
                Assert.That(result.IsSuccess, Is.True, $"{result.Status}:{result.AuthorityRejectionReason}");
                Assert.That(manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization, Is.SameAs(original));
                Assert.That(original.RequiredSatisfied, Is.True);
                manager.SetSkinFromConfiguration(SkinInfo.OMS_SKIN.ToString());
            });
            AddUntilStep("protected skin selection detaches previous package", () => manager.CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN);
            AddStep("select same record from its renamed source", () => manager.SetSkinFromConfiguration(selection.Candidate.ID.ToString()));
            AddUntilStep("renamed author package selected", () =>
                manager.CurrentSkinInfo.Value.ID == selection.Candidate.ID && manager.CurrentSkin.Value.PreparedGameplaySkinPackage != null);
            AddStep("directory rename never changes content authorization identity", () =>
            {
                var reselected = manager.CurrentSkin.Value.PreparedGameplaySkinPackage!.ScriptAuthorization!;
                Assert.That(reselected.Key, Is.EqualTo(original.Key));
                Assert.That(reselected.RequiredSatisfied, Is.True);
            });
        }

        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm, false)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder, false)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder, false)]
        [TestCase(MaterialDiagnosticPackageSource.OrdinaryRealm, true)]
        [TestCase(MaterialDiagnosticPackageSource.ManagedFolder, true)]
        [TestCase(MaterialDiagnosticPackageSource.ExternalFolder, true)]
        public void TestCompiledWholePackageCancellationOrShutdownRetiresProvisionalBeforeLateCommit(MaterialDiagnosticPackageSource source, bool shutdown)
        {
            var selection = new C6ScriptSelection();
            var cancellation = new CancellationTokenSource();
            Task<SkinCurrentRevisionReloadResult>? reload = null;
            Task? stop = null;
            Action? pendingCommit = null;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision? retiredB = null;
            Skin? compiledB = null;
            int retiredCount = 0;

            addSelectC6ScriptPackage(selection, source, writeC6ScriptPackage);
            AddStep("retain A and intercept the existing publication callback", () =>
            {
                revisionA = manager.CurrentRevision;
                manager.ManagedFolderFactoryCreate = (info, resources, capsule) =>
                {
                    var created = SkinManagedFolderFactory.Create(info, resources, capsule);
                    compiledB = created.Skin;
                    return created;
                };
                manager.CurrentRevisionRetired += retired =>
                {
                    if (!ReferenceEquals(retired, revisionA))
                    {
                        retiredB = retired;
                        Interlocked.Increment(ref retiredCount);
                    }
                };
                manager.CurrentRevisionCompletionSchedule = callback => pendingCommit = callback;
                File.AppendAllText(Path.Combine(selection.Root, GameplaySkinSceneContracts.SCRIPT_FILE_NAME), "\n# cancelled B\n");
                if (source == MaterialDiagnosticPackageSource.OrdinaryRealm)
                    replaceRealmRevisionFiles(selection.Candidate.ID, selection.Root);
                reload = manager.ReloadCurrentRevisionAsync(cancellation.Token);
            });
            AddUntilStep("B compiler and package preparation finish before commit", () => pendingCommit != null);
            AddStep("cancel or shut down a fully prepared script publication", () =>
            {
                Assert.That(compiledB!.PreparedGameplaySkinPackage!.ScriptProgram, Is.Not.Null);
                if (shutdown)
                    stop = Task.Run(() => manager.ShutdownManagedFolderMutations());
                else
                {
                    cancellation.Cancel();
                    pendingCommit!();
                }
            });
            AddUntilStep("real preparation worker and provisional owner exit", () =>
                reload!.IsCompleted && retiredCount == 1 && (!shutdown || stop!.IsCompleted));
            AddStep("late callback cannot publish the retired script", () =>
            {
                pendingCommit!();
                if (shutdown)
                    stop!.GetAwaiter().GetResult();
                Assert.That(reload!.GetAwaiter().GetResult(), Is.EqualTo(shutdown ? SkinCurrentRevisionReloadResult.Shutdown : SkinCurrentRevisionReloadResult.Cancelled));
                Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                Assert.That(retiredB!.Retired.IsCompleted, Is.True);
                Assert.That(compiledB!.PreparedGameplaySkinPackage, Is.Null);
                Assert.That(retiredCount, Is.EqualTo(1));
                cancellation.Dispose();
            });
        }

        private static void writeC6AuthorPackage(string sourceRoot, string? failure, string source = "oms-script 1\nrequired scene.numeric.write\ntarget node.c6 rotation\nset node.c6 rotation 12\nhalt\n")
        {
            var manifest = new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = new JArray(),
                ["script"] = failure == "bytecode" ? GameplaySkinSceneContracts.BYTECODE_FILE_NAME : GameplaySkinSceneContracts.SCRIPT_FILE_NAME,
            };
            var scene = new JObject
            {
                ["contract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["root"] = new JObject
                {
                    ["id"] = "node.c6",
                    ["type"] = "container",
                    ["target"] = new JObject { ["kind"] = "global" },
                    ["slot"] = "decoration",
                    ["blend"] = "alpha",
                    ["properties"] = new JObject { ["opacity"] = 1 },
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(),
                },
                ["tracks"] = new JArray(),
                ["stateMachines"] = new JArray(),
                ["bindings"] = new JArray(),
                ["templates"] = new JArray(),
                ["instances"] = new JArray(),
            };
            if (failure == "resource")
            {
                manifest["resources"] = new JArray(new JObject { ["id"] = "texture.bad", ["type"] = "texture", ["path"] = "c6-broken.png" });
                File.WriteAllBytes(Path.Combine(sourceRoot, "c6-broken.png"), new byte[] { 1, 2, 3 });
            }
            File.WriteAllText(Path.Combine(sourceRoot, GameplaySkinSceneContracts.MANIFEST_FILE_NAME),
                failure == "manifest" ? "{\"contract\":false}" : manifest.ToString());
            if (failure == "missing-scene")
                File.Delete(Path.Combine(sourceRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME));
            else
                File.WriteAllText(Path.Combine(sourceRoot, GameplaySkinSceneContracts.SCENE_FILE_NAME), scene.ToString());
            File.WriteAllText(Path.Combine(sourceRoot, GameplaySkinSceneContracts.SCRIPT_FILE_NAME), failure == "script" ? "oms-script 999\nhalt\n" : source);
            if (failure == "bytecode")
                File.WriteAllBytes(Path.Combine(sourceRoot, GameplaySkinSceneContracts.BYTECODE_FILE_NAME), new byte[] { 1, 2, 3 });
        }
    }
}
