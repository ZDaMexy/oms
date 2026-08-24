// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Models;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    public partial class BmsManagedFolderSelectionProductTest
    {
        [Test]
        public void TestOrdinaryRealmPackageReloadButtonPublishesSameIdExactRevision()
        {
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            string sourceRoot = string.Empty;

            AddStep("create ordinary Realm revision A", () =>
            {
                sourceRoot = LocalStorage.GetFullPath($"realm-revision-{Guid.NewGuid():N}");
                writeRevisionPackage(sourceRoot, "A", new Rgba32(240, 40, 80, 255));
                candidate = createRealmRevisionCandidate(sourceRoot);
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for immutable ordinary A", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("capture ordinary A and mount real caller", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                Add(caller = new FullSkinSettingsCallerHost(manager));

                Assert.Multiple(() =>
                {
                    Assert.That(revisionA.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.RealmPackage));
                    Assert.That(revisionA.ContentRevision, Is.EqualTo(ownerA.PackageContentRevision));
                    Assert.That(ownerA.Configuration.SkinInfo.Name, Is.EqualTo("current revision A"));
                });
            });
            AddUntilStep("wait for ordinary reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("replace exact Realm declaration set with B", () =>
            {
                writeRevisionPackage(sourceRoot, "B", new Rgba32(20, 210, 120, 255));
                replaceRealmRevisionFiles(candidate.ID, sourceRoot);

                // The active immutable A owner cannot observe the new Realm declaration set before publication.
                Assert.That(ownerA.Configuration.SkinInfo.Name, Is.EqualTo("current revision A"));
            });
            AddStep("invoke ordinary real reload button", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for ordinary coherent B", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert ordinary same-ID A to B", () =>
            {
                SkinCurrentRevision revisionB = manager.CurrentRevision;

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(candidate.ID));
                    Assert.That(manager.CurrentSkin.Value.SkinInfo.ID, Is.EqualTo(candidate.ID));
                    Assert.That(revisionB.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(revisionB.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(revisionB.ContentRevision, Is.EqualTo(revisionB.Owner.PackageContentRevision));
                    Assert.That(revisionB.SourceKind, Is.EqualTo(SkinCurrentRevisionSourceKind.RealmPackage));
                    Assert.That(revisionB.Owner.Configuration.SkinInfo.Name, Is.EqualTo("current revision B"));
                });
            });
        }

        [Test]
        public void TestOrdinaryRealmPackagePrepareFailureKeepsExactImmutableA()
        {
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            string sourceRoot = string.Empty;
            int prepareCount = 0;

            AddStep("create failing ordinary Realm revision A", () =>
            {
                sourceRoot = LocalStorage.GetFullPath($"realm-revision-failure-{Guid.NewGuid():N}");
                writeRevisionPackage(sourceRoot, "A", new Rgba32(240, 40, 80, 255));
                candidate = createRealmRevisionCandidate(sourceRoot);
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for failing ordinary A", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("capture A and publish unavailable B declarations", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                manager.CurrentRevisionPrepareStarted = () => prepareCount++;
                Add(caller = new FullSkinSettingsCallerHost(manager));

                writeRevisionPackage(sourceRoot, "B", new Rgba32(20, 210, 120, 255));
                string missingBlob = replaceRealmRevisionFiles(candidate.ID, sourceRoot);
                new RealmFileStore(Realm, LocalStorage).Storage.Delete(missingBlob);
            });
            AddUntilStep("wait for failing ordinary reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("invoke unavailable ordinary revision", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for ordinary failure boundary", () => prepareCount == 1 && caller.ReloadCurrentButton.Enabled.Value);
            AddStep("assert exact ordinary A survived", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value.ID, Is.EqualTo(candidate.ID));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(ownerA.PackageContentRevision));
                    Assert.That(ownerA.Configuration.SkinInfo.Name, Is.EqualTo("current revision A"));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                });
            });
        }

        [Test]
        public void TestOrdinaryRealmPackageFinalDeclarationDriftRetiresProvisionalKeepsAAndRetriesB()
        {
            Live<SkinInfo> candidate = null!;
            FullSkinSettingsCallerHost caller = null!;
            SkinCurrentRevision revisionA = null!;
            SkinCurrentRevision? retiredProvisional = null;
            Skin ownerA = null!;
            RealmPackageAtomicSnapshot packageB = null!;
            string sourceRoot = string.Empty;
            string originalFilename = string.Empty;
            int finalBoundaryCalls = 0;
            int provisionalRetireCount = 0;
            int retiredACount = 0;

            AddStep("create ordinary Realm revision A", () =>
            {
                sourceRoot = LocalStorage.GetFullPath($"realm-revision-final-drift-{Guid.NewGuid():N}");
                writeRevisionPackage(sourceRoot, "A", new Rgba32(240, 40, 80, 255));
                candidate = createRealmRevisionCandidate(sourceRoot);
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for exact ordinary A", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.SkinInfo.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("capture A and mount real reload caller", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;
                manager.CurrentRevisionRetired += revision =>
                {
                    if (ReferenceEquals(revision, revisionA))
                    {
                        Interlocked.Increment(ref retiredACount);
                        return;
                    }

                    if (revision.RecordId == candidate.ID)
                    {
                        retiredProvisional = revision;
                        Interlocked.Increment(ref provisionalRetireCount);
                    }
                };
                Add(caller = new FullSkinSettingsCallerHost(manager));
            });
            AddUntilStep("wait for ordinary reload affordance", () => caller.ReloadCurrentButton.Enabled.Value);
            AddStep("publish exact Realm B declaration and install final drift", () =>
            {
                writeRevisionPackage(sourceRoot, "B", new Rgba32(20, 210, 120, 255));
                replaceRealmRevisionFiles(candidate.ID, sourceRoot);
                packageB = captureRealmPackageAtomicSnapshot(candidate);

                manager.CurrentRevisionBeforeCommitSchedule = () =>
                {
                    if (Interlocked.Increment(ref finalBoundaryCalls) != 1)
                        return;

                    Realm.Write(realm =>
                    {
                        SkinInfo current = realm.Find<SkinInfo>(candidate.ID)!;
                        RealmNamedFileUsage declaration = current.Files
                                                                 .OrderBy(file => file.Filename, StringComparer.Ordinal)
                                                                 .First();
                        originalFilename = declaration.Filename;
                        declaration.Filename = $"drift/{originalFilename}";
                    });
                };
            });
            AddStep("invoke real reload into final Realm declaration drift", () =>
                caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for final-drift failure and provisional retirement", () =>
                finalBoundaryCalls == 1
                && provisionalRetireCount == 1
                && retiredProvisional?.Retired.IsCompleted == true
                && caller.ReloadCurrentButton.Enabled.Value
                && caller.PostedNotifications.Count == 1);
            AddStep("assert exact A and B blobs survived declaration drift", () =>
            {
                RealmPackageAtomicSnapshot drifted = captureRealmPackageAtomicSnapshot(candidate);

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(candidate));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentRevision.Owner, Is.SameAs(ownerA));
                    Assert.That(revisionA.Retired.IsCompleted, Is.False);
                    Assert.That(retiredACount, Is.Zero);
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                    Assert.That(retiredProvisional, Is.Not.Null);
                    Assert.That(retiredProvisional!.Retired.IsCompletedSuccessfully, Is.True);
                    Assert.That(
                        drifted.Files.Select(file => (file.Hash, file.BlobDigest)),
                        Is.EqualTo(packageB.Files.Select(file => (file.Hash, file.BlobDigest))),
                        "The concurrent actor changed only the exact Realm declaration; reload must not mutate blobs.");
                    Assert.That(
                        caller.PostedNotifications[0].Text.ToString(),
                        Is.EqualTo(SkinSettingsStrings.CurrentSkinReloadFailed.ToString()));
                });
            });
            AddStep("restore exact B declaration for retry", () =>
            {
                Realm.Write(realm =>
                {
                    SkinInfo current = realm.Find<SkinInfo>(candidate.ID)!;
                    RealmNamedFileUsage declaration = current.Files
                                                             .Single(file => file.Filename == $"drift/{originalFilename}");
                    declaration.Filename = originalFilename;
                });
                manager.CurrentRevisionBeforeCommitSchedule = () => { };
                Assert.That(captureRealmPackageAtomicSnapshot(candidate), Is.EqualTo(packageB));
            });
            AddStep("retry B through real reload caller", () => caller.ReloadCurrentButton.TriggerClick());
            AddUntilStep("wait for coherent ordinary B retry", () =>
                !ReferenceEquals(manager.CurrentRevision, revisionA)
                && !ReferenceEquals(manager.CurrentSkin.Value, ownerA)
                && revisionA.Retired.IsCompleted
                && retiredACount == 1
                && caller.ReloadCurrentButton.Enabled.Value
                && caller.PostedNotifications.Count == 2);
            AddStep("assert retry published exact same-ID B", () =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentSkinInfo.Value, Is.SameAs(candidate));
                    Assert.That(manager.CurrentRevision.RecordId, Is.EqualTo(revisionA.RecordId));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.Not.EqualTo(revisionA.ContentRevision));
                    Assert.That(manager.CurrentRevision.ContentRevision, Is.EqualTo(manager.CurrentSkin.Value.PackageContentRevision));
                    Assert.That(manager.CurrentSkin.Value.Configuration.SkinInfo.Name, Is.EqualTo("current revision B"));
                    Assert.That(captureRealmPackageAtomicSnapshot(candidate), Is.EqualTo(packageB));
                    Assert.That(provisionalRetireCount, Is.EqualTo(1));
                    Assert.That(retiredACount, Is.EqualTo(1));
                    Assert.That(
                        caller.PostedNotifications[1].Text.ToString(),
                        Is.EqualTo(SkinSettingsStrings.CurrentSkinReloaded.ToString()));
                });
            });
        }

        [Test]
        public void TestCurrentOrdinaryRealmFileMutationSurfaceIsFailClosed()
        {
            Live<SkinInfo> candidate = null!;
            SkinCurrentRevision revisionA = null!;
            Skin ownerA = null!;
            string sourceRoot = string.Empty;

            AddStep("create current ordinary mutation target", () =>
            {
                sourceRoot = LocalStorage.GetFullPath($"realm-revision-mutation-{Guid.NewGuid():N}");
                writeRevisionPackage(sourceRoot, "A", new Rgba32(240, 40, 80, 255));
                candidate = createRealmRevisionCandidate(sourceRoot);
                manager.CurrentSkinInfo.Value = candidate;
            });
            AddUntilStep("wait for current ordinary mutation target", () =>
                manager.CurrentSkinInfo.Value.ID == candidate.ID
                && manager.CurrentSkin.Value.PackageContentRevision != null);
            AddStep("reject direct current file mutation", () =>
            {
                revisionA = manager.CurrentRevision;
                ownerA = manager.CurrentSkin.Value;

                Assert.That(
                    () => candidate.PerformRead(info =>
                        manager.AddFile(info, new MemoryStream(new byte[] { 1, 2, 3 }), "bypass.bin")),
                    Throws.TypeOf<InvalidOperationException>());

                Assert.Multiple(() =>
                {
                    Assert.That(manager.CurrentRevision, Is.SameAs(revisionA));
                    Assert.That(manager.CurrentSkin.Value, Is.SameAs(ownerA));
                    Assert.That(candidate.PerformRead(info => info.Files.SingleOrDefault(file => file.Filename == "bypass.bin")), Is.Null);
                });
            });
        }

        private Live<SkinInfo> createRealmRevisionCandidate(string sourceRoot)
        {
            var info = new SkinInfo("ordinary revision package", "OMS tests", typeof(BmsLegacySkin).GetInvariantInstantiationInfo());
            var fileStore = new RealmFileStore(Realm, LocalStorage);

            Realm.Write(realm =>
            {
                realm.Add(info);

                foreach (string path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories))
                {
                    using Stream stream = File.OpenRead(path);
                    RealmFile file = fileStore.Add(stream, realm);
                    info.Files.Add(new RealmNamedFileUsage(file, Path.GetRelativePath(sourceRoot, path)));
                }
            });

            return manager.Query(skin => skin.ID == info.ID);
        }

        private string replaceRealmRevisionFiles(Guid recordId, string sourceRoot)
        {
            var fileStore = new RealmFileStore(Realm, LocalStorage);
            string firstStoragePath = string.Empty;

            Realm.Write(realm =>
            {
                SkinInfo info = realm.Find<SkinInfo>(recordId)!;
                info.Files.Clear();

                foreach (string path in Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
                                                        .OrderBy(path => path, StringComparer.Ordinal))
                {
                    using Stream stream = File.OpenRead(path);
                    RealmFile file = fileStore.Add(stream, realm);
                    info.Files.Add(new RealmNamedFileUsage(file, Path.GetRelativePath(sourceRoot, path)));
                    firstStoragePath = firstStoragePath.Length == 0 ? file.GetStoragePath() : firstStoragePath;
                }
            });

            return firstStoragePath;
        }
    }
}
