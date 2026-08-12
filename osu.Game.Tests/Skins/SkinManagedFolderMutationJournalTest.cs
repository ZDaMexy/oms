// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Skinning;
using osu.Game.Tests.Database;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class SkinManagedFolderMutationJournalTest
    {
        private const string source_path = "chartskin/source";
        private const string target_path = "chartskin/target";
        private const string publication_fingerprint = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
        private const string replacement_publication_fingerprint = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string second_delete_node_fingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const string staged_content_revision = "E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855";
        private const string staged_tree_fingerprint = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static readonly SkinManagedFolderPhysicalIdentity root_identity = new SkinManagedFolderPhysicalIdentity(11, 101, 102);
        private static readonly SkinManagedFolderPhysicalIdentity source_identity = new SkinManagedFolderPhysicalIdentity(11, 12, 13);
        private static readonly SkinManagedFolderPhysicalIdentity target_identity = new SkinManagedFolderPhysicalIdentity(11, 22, 23);
        private static readonly SkinManagedFolderPhysicalIdentity staged_root_identity = new SkinManagedFolderPhysicalIdentity(11, 31, 32);
        private static readonly SkinManagedFolderPhysicalIdentity staged_identity = new SkinManagedFolderPhysicalIdentity(11, 33, 34);
        private static readonly SkinManagedFolderPhysicalIdentity replacement_root_identity = new SkinManagedFolderPhysicalIdentity(11, 91, 92);
        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);

        [Test]
        public void TestAllPreparedKindsRoundTripWithExactIdentityAuthorityAndAffectedPaths()
        {
            Guid renameOperationId = Guid.NewGuid();
            Guid deleteOperationId = Guid.NewGuid();
            Guid importOperationId = Guid.NewGuid();
            Guid renameRecordId = Guid.NewGuid();
            Guid deleteRecordId = Guid.NewGuid();

            SkinManagedFolderMutationJournal rename = SkinManagedFolderMutationJournal.CreatePreparedRename(
                renameOperationId,
                renameRecordId,
                root_identity,
                source_path,
                source_identity,
                target_path);
            SkinManagedFolderMutationJournal delete = SkinManagedFolderMutationJournal.CreatePreparedDelete(
                deleteOperationId,
                deleteRecordId,
                root_identity,
                source_path,
                source_identity,
                publication_fingerprint,
                SkinManagedFolderDeleteManifest.Create(
                    new[] { publication_fingerprint }));
            SkinManagedFolderMutationJournal stagedImport = SkinManagedFolderMutationJournal.CreatePreparedStagedImport(
                importOperationId,
                root_identity,
                target_path,
                staged_identity,
                staged_root_identity,
                staged_content_revision,
                staged_tree_fingerprint);

            assertRoundTrip(rename);
            assertRoundTrip(delete);
            assertRoundTrip(stagedImport);

            Assert.Multiple(() =>
            {
                Assert.That(rename.RecordId, Is.EqualTo(renameRecordId));
                Assert.That(rename.SourceIdentity, Is.EqualTo(source_identity));
                Assert.That(rename.TargetIdentity, Is.Null);
                Assert.That(rename.GetAffectedManagedRelativePaths(), Is.EqualTo(new[] { source_path, target_path }));

                Assert.That(delete.RecordId, Is.EqualTo(deleteRecordId));
                Assert.That(delete.SourceIdentity, Is.EqualTo(source_identity));
                Assert.That(
                    delete.TargetManagedRelativePath,
                    Is.EqualTo(SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(deleteOperationId)));
                Assert.That(delete.NewRecordPublicationFingerprint, Is.EqualTo(publication_fingerprint));
                Assert.That(
                    delete.GetAffectedManagedRelativePaths(),
                    Is.EqualTo(new[]
                    {
                        source_path,
                        SkinManagedFolderMutationJournal.GetExpectedDeleteTombstoneRelativePath(deleteOperationId),
                    }));

                Assert.That(stagedImport.RecordId, Is.EqualTo(importOperationId));
                Assert.That(stagedImport.ManagedRootIdentity, Is.EqualTo(root_identity));
                Assert.That(stagedImport.SourceManagedRelativePath, Is.Null);
                Assert.That(stagedImport.StagedSourceAuthority, Is.EqualTo(SkinManagedFolderMutationJournal.STAGED_SOURCE_AUTHORITY));
                Assert.That(
                    stagedImport.StagedSourceRelativePath,
                    Is.EqualTo(SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(importOperationId)));
                Assert.That(stagedImport.StagedSourceIdentity, Is.EqualTo(staged_identity));
                Assert.That(stagedImport.StagedRootIdentity, Is.EqualTo(staged_root_identity));
                Assert.That(
                    stagedImport.StagedSourceContentRevision,
                    Is.EqualTo(staged_content_revision));
                Assert.That(
                    stagedImport.StagedSourceTreeFingerprint,
                    Is.EqualTo(staged_tree_fingerprint));
                Assert.That(
                    stagedImport.NewRecordPublicationPlanVersion,
                    Is.EqualTo(SkinManagedFolderMutationJournal.NEW_RECORD_PUBLICATION_PLAN_VERSION));
                Assert.That(stagedImport.GetAffectedManagedRelativePaths(), Is.EqualTo(new[] { target_path }));
            });
        }

        [Test]
        public void TestDeleteManifestIsCanonicalBoundedAndSupportsCrashSubsetProof()
        {
            string manifest = SkinManagedFolderDeleteManifest.Create(new[]
            {
                second_delete_node_fingerprint,
                publication_fingerprint,
            });
            string firstOnly = SkinManagedFolderDeleteManifest.Create(
                new[] { publication_fingerprint });

            Assert.Multiple(() =>
            {
                Assert.That(SkinManagedFolderDeleteManifest.IsValid(manifest), Is.True);
                Assert.That(
                    SkinManagedFolderDeleteManifest.IsSubset(firstOnly, manifest),
                    Is.True);
                Assert.That(
                    SkinManagedFolderDeleteManifest.IsSubset(manifest, firstOnly),
                    Is.False);
                Assert.That(
                    () => SkinManagedFolderDeleteManifest.Create(Array.Empty<string>()),
                    Throws.ArgumentException);
                Assert.That(
                    () => SkinManagedFolderDeleteManifest.Create(new[]
                    {
                        publication_fingerprint,
                        publication_fingerprint,
                    }),
                    Throws.ArgumentException);
            });
        }

        [Test]
        public void TestMaximumDeleteManifestRoundTripsWithinDurableJournalBudget()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            string[] nodeFingerprints = Enumerable.Range(
                    0,
                    SkinManagedFolderDeleteManifest.MaximumNodeCount)
                .Select(index => Convert.ToHexString(
                        SHA256.HashData(BitConverter.GetBytes(index)))
                    .ToLowerInvariant())
                .ToArray();
            string manifest = SkinManagedFolderDeleteManifest.Create(
                nodeFingerprints);
            SkinManagedFolderMutationJournal journal =
                SkinManagedFolderMutationJournal.CreatePreparedDelete(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        root_identity,
                        source_path,
                        source_identity,
                        publication_fingerprint,
                        manifest)
                    .WithDeleteFallbackDisposition(
                        SkinManagedFolderDeleteFallbackDisposition.NotRequired);

            store.Write(journal);
            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(
                    SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.DeleteSourceNodeManifest,
                    Is.EqualTo(manifest));
                Assert.That(new FileInfo(getJournalPath(storage)).Length,
                    Is.LessThan(1024 * 1024));
            });
        }

        [Test]
        public void TestDeleteFallbackDispositionIsDurableMonotonicEvidence()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal rawPrepared =
                SkinManagedFolderMutationJournal.CreatePreparedDelete(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    root_identity,
                    source_path,
                    source_identity,
                    publication_fingerprint,
                    SkinManagedFolderDeleteManifest.Create(
                        new[] { publication_fingerprint }));
            SkinManagedFolderMutationJournal confirmed =
                rawPrepared.WithDeleteFallbackDisposition(
                    SkinManagedFolderDeleteFallbackDisposition.NotRequired);

            store.Write(rawPrepared);
            store.Write(confirmed);

            Assert.Multiple(() =>
            {
                Assert.That(rawPrepared.DeleteFallbackDisposition, Is.Null);
                Assert.That(rawPrepared.IsSameMonotonicIntent(confirmed), Is.True);
                Assert.That(confirmed.IsSameMonotonicIntent(rawPrepared), Is.False);
                Assert.That(rawPrepared.IsExactSameJournal(confirmed), Is.False);
                Assert.That(
                    () => confirmed.WithDeleteFallbackDisposition(
                        SkinManagedFolderDeleteFallbackDisposition.ProtectedPairCommitted),
                    Throws.InvalidOperationException);
                Assert.That(
                    confirmed.WithFilesystemApplied().DeleteFallbackDisposition,
                    Is.EqualTo(
                        SkinManagedFolderDeleteFallbackDisposition.NotRequired));
            });
            assertLoadedEquivalent(store, confirmed);
        }

        [Test]
        public void TestDeleteManifestAndFallbackTamperAreRejectedWithMatchingChecksum()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedDelete();
            SkinManagedFolderMutationJournal filesystem =
                prepared.WithFilesystemApplied();
            Action<JObject>[] tampers =
            {
                payload => payload.Remove(
                    nameof(SkinManagedFolderMutationJournal.DeleteSourceNodeManifest)),
                payload => payload[
                    nameof(SkinManagedFolderMutationJournal.DeleteSourceNodeManifest)] =
                    "v1:not-a-fingerprint",
                payload => payload.Remove(
                    nameof(SkinManagedFolderMutationJournal.DeleteFallbackDisposition)),
                payload => payload[
                    nameof(SkinManagedFolderMutationJournal.DeleteFallbackDisposition)] = 999,
            };

            foreach (Action<JObject> tamper in tampers)
            {
                string journalPath = getJournalPath(storage);

                if (File.Exists(journalPath))
                    File.Delete(journalPath);

                store.Write(prepared);
                store.Write(filesystem);
                JObject document = readDocument(storage);
                var payload = (JObject)document["payload"]!;
                tamper(payload);
                document["sha256"] =
                    computeChecksum(payload.ToString(Formatting.None));
                writeDocument(storage, document);

                Assert.That(store.Load().Status, Is.EqualTo(
                    SkinManagedFolderMutationJournalLoadStatus.Invalid));
            }
        }

        [Test]
        public void TestSemanticallyInvalidIdentityAndAuthorityAreRejectedWithMatchingChecksum()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal stagedImport = SkinManagedFolderMutationJournal.CreatePreparedStagedImport(
                Guid.NewGuid(),
                root_identity,
                target_path,
                staged_identity,
                staged_root_identity,
                staged_content_revision,
                staged_tree_fingerprint);

            assertSemanticTamperRejected(
                storage,
                store,
                stagedImport,
                payload => payload[nameof(SkinManagedFolderMutationJournal.StagedSourceAuthority)] = "foreign-staging-authority");
            assertSemanticTamperRejected(
                storage,
                store,
                stagedImport,
                payload => payload[nameof(SkinManagedFolderMutationJournal.NewRecordPublicationPlanVersion)] = "foreign-publication-plan");
            assertSemanticTamperRejected(
                storage,
                store,
                stagedImport,
                payload => payload[nameof(SkinManagedFolderMutationJournal.StagedSourceRelativePath)] = "skin-mutation-staging/wrong-operation");
            assertSemanticTamperRejected(
                storage,
                store,
                stagedImport,
                payload =>
                {
                    var identity = (JObject)payload[nameof(SkinManagedFolderMutationJournal.StagedSourceIdentity)]!;
                    identity[nameof(SkinManagedFolderPhysicalIdentity.VolumeSerialNumber)] = 0;
                    identity[nameof(SkinManagedFolderPhysicalIdentity.FileIdPart0)] = 0;
                    identity[nameof(SkinManagedFolderPhysicalIdentity.FileIdPart1)] = 0;
                });

            Assert.That(
                () => SkinManagedFolderMutationJournal.CreatePreparedDelete(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    root_identity,
                    source_path,
                    default,
                    publication_fingerprint,
                    SkinManagedFolderDeleteManifest.Create(
                        new[] { publication_fingerprint })),
                Throws.ArgumentException);
        }

        [Test]
        public void TestStagedImportTargetIdentityMustExactlyMatchStagedSource()
        {
            SkinManagedFolderMutationJournal prepared = createPreparedStagedImport();

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => prepared.WithFilesystemApplied(target_identity, publication_fingerprint),
                    Throws.InvalidOperationException,
                    "A same-volume target with a different file identity must not be accepted.");
                Assert.That(
                    () => prepared.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.Committed,
                        target_identity,
                        publication_fingerprint),
                    Throws.InvalidOperationException,
                    "Recovery must enforce the same exact identity contract.");
            });

            SkinManagedFolderMutationJournal filesystem =
                prepared.WithFilesystemApplied(staged_identity, publication_fingerprint);
            SkinManagedFolderMutationJournal realm = filesystem.WithRealmApplied();
            SkinManagedFolderMutationJournal recovered = realm.WithRecoveryTerminalPhase(
                SkinManagedFolderMutationPhase.Committed,
                staged_identity,
                publication_fingerprint);

            Assert.Multiple(() =>
            {
                Assert.That(filesystem.TargetIdentity, Is.EqualTo(staged_identity));
                Assert.That(recovered.TargetIdentity, Is.EqualTo(staged_identity));
            });
        }

        [Test]
        public void TestManagedCopyPreparedIntentRoundTripsAndFreezesExactEvidence()
        {
            Guid operationId = Guid.NewGuid();
            Guid externalRecordId = Guid.NewGuid();
            SkinManagedCopyLogicalManifest manifest = createManagedCopyManifest(
                SkinPackageCapturedEntry.CreateDirectory("empty"),
                SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 }));
            var externalRegistry = new SkinExternalRegistryJournalBinding(
                7,
                replacement_publication_fingerprint,
                SkinExternalCollisionDisposition.ExactRegisteredExternalSet);

            SkinManagedFolderMutationJournal prepared =
                SkinManagedFolderMutationJournal.CreatePreparedManagedCopy(
                    operationId,
                    externalRecordId,
                    root_identity,
                    target_path,
                    staged_root_identity,
                    staged_content_revision,
                    publication_fingerprint,
                    replacement_publication_fingerprint,
                    manifest,
                    externalRegistry);

            assertRoundTrip(prepared);

            Assert.Multiple(() =>
            {
                Assert.That(prepared.Kind, Is.EqualTo(SkinManagedFolderMutationKind.ManagedCopy));
                Assert.That(prepared.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                Assert.That(prepared.RecordId, Is.EqualTo(externalRecordId));
                Assert.That(prepared.SourceManagedRelativePath, Is.Null);
                Assert.That(prepared.TargetManagedRelativePath, Is.EqualTo(target_path));
                Assert.That(prepared.StagedSourceAuthority, Is.EqualTo(SkinManagedFolderMutationJournal.STAGED_SOURCE_AUTHORITY));
                Assert.That(
                    prepared.StagedSourceRelativePath,
                    Is.EqualTo(SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(operationId)));
                Assert.That(prepared.StagedSourceIdentity, Is.Null);
                Assert.That(prepared.StagedRootIdentity, Is.EqualTo(staged_root_identity));
                Assert.That(prepared.StagedSourceContentRevision, Is.EqualTo(staged_content_revision));
                Assert.That(prepared.ManagedCopyExternalRecordFingerprint, Is.EqualTo(publication_fingerprint));
                Assert.That(prepared.ManagedCopyExternalCaptureFingerprint, Is.EqualTo(replacement_publication_fingerprint));
                Assert.That(prepared.ManagedCopyLogicalManifest, Is.EqualTo(manifest.Encoded));
                Assert.That(prepared.ManagedCopyLogicalManifestDigest, Is.EqualTo(manifest.Digest));
                Assert.That(prepared.ExternalRegistryGeneration, Is.EqualTo(7));
                Assert.That(prepared.ExternalRegistryDigest, Is.EqualTo(replacement_publication_fingerprint));
                Assert.That(
                    prepared.ExternalCollisionDisposition,
                    Is.EqualTo(SkinExternalCollisionDisposition.ExactRegisteredExternalSet));
                Assert.That(prepared.GetAffectedManagedRelativePaths(), Is.EqualTo(new[] { target_path }));
            });
        }

        [Test]
        public void TestManagedCopyPhaseGraphIsStrictMonotonicAndTerminal()
        {
            SkinManagedFolderMutationJournal prepared = createPreparedManagedCopy();
            SkinManagedFolderMutationJournal copying = prepared.WithCopying(staged_identity);
            SkinManagedFolderMutationJournal provisionalReady = copying.WithProvisionalReady(
                staged_identity,
                staged_tree_fingerprint,
                publication_fingerprint);
            SkinManagedFolderMutationJournal filesystem = provisionalReady.WithFilesystemApplied(
                staged_identity,
                publication_fingerprint);
            SkinManagedFolderMutationJournal realm = filesystem.WithRealmApplied();
            SkinManagedFolderMutationJournal committed = realm.WithCommitted();

            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);

            foreach (SkinManagedFolderMutationJournal journal in new[]
                     {
                         prepared,
                         copying,
                         provisionalReady,
                         filesystem,
                         realm,
                         committed,
                     })
            {
                store.Write(journal);
                assertLoadedEquivalent(store, journal);
            }

            Assert.Multiple(() =>
            {
                Assert.That(
                    new[]
                    {
                        prepared.Phase,
                        copying.Phase,
                        provisionalReady.Phase,
                        filesystem.Phase,
                        realm.Phase,
                        committed.Phase,
                    },
                    Is.EqualTo(new[]
                    {
                        SkinManagedFolderMutationPhase.Prepared,
                        SkinManagedFolderMutationPhase.Copying,
                        SkinManagedFolderMutationPhase.ProvisionalReady,
                        SkinManagedFolderMutationPhase.FilesystemApplied,
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    }));
                Assert.That(prepared.IsSameMonotonicIntent(copying), Is.True);
                Assert.That(copying.IsSameMonotonicIntent(prepared), Is.False);
                Assert.That(copying.IsSameMonotonicIntent(provisionalReady), Is.True);
                Assert.That(provisionalReady.IsSameMonotonicIntent(filesystem), Is.True);
                Assert.That(filesystem.IsSameMonotonicIntent(realm), Is.True);
                Assert.That(realm.IsSameMonotonicIntent(committed), Is.True);
                Assert.That(() => prepared.WithProvisionalReady(
                    staged_identity,
                    staged_tree_fingerprint,
                    publication_fingerprint), Throws.InvalidOperationException);
                Assert.That(() => prepared.WithFilesystemApplied(
                    staged_identity,
                    publication_fingerprint), Throws.InvalidOperationException);
                Assert.That(() => copying.WithCopying(staged_identity), Throws.InvalidOperationException);
                Assert.That(() => copying.WithRealmApplied(), Throws.InvalidOperationException);
                Assert.That(() => provisionalReady.WithCopying(staged_identity), Throws.InvalidOperationException);
                Assert.That(() => committed.WithRolledBack(), Throws.InvalidOperationException);
            });
        }

        [Test]
        public void TestManagedCopyManifestTamperIsRejectedEvenWithMatchingOuterChecksum()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createPreparedManagedCopy();

            Action<JObject>[] tampers =
            {
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifest)),
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifestDigest)),
                payload => payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifest)] = "not-base64",
                payload => payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifestDigest)] =
                    replacement_publication_fingerprint,
                payload =>
                {
                    string encoded = payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifest)]!.Value<string>()!;
                    byte[] bytes = Convert.FromBase64String(encoded);
                    bytes[^1] ^= 1;
                    payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifest)] = Convert.ToBase64String(bytes);
                },
                payload => payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyExternalRecordFingerprint)] =
                    publication_fingerprint.ToUpperInvariant(),
                payload => payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyExternalCaptureFingerprint)] = "invalid",
                payload => payload[nameof(SkinManagedFolderMutationJournal.Phase)] =
                    (int)SkinManagedFolderMutationPhase.ProvisionalReady,
            };

            foreach (Action<JObject> tamper in tampers)
                assertSemanticTamperRejected(storage, store, journal, tamper);
        }

        [Test]
        public void TestManagedCopyMalformedDurableTreeAndBudgetsAreRejectedWithMatchingChecksums()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createPreparedManagedCopy();
            (string Name, SkinExternalPackageLogicalEntryKind Kind, long Length)[][] malformedManifests =
            {
                new[]
                {
                    ("parent", SkinExternalPackageLogicalEntryKind.File, 0L),
                    ("parent/child.ini", SkinExternalPackageLogicalEntryKind.File, 0L),
                },
                new[]
                {
                    ("missing/child.ini", SkinExternalPackageLogicalEntryKind.File, 0L),
                },
                new[]
                {
                    ("huge.bin", SkinExternalPackageLogicalEntryKind.File,
                        SkinPackageRevisionCapsuleLimits.Default.MaxFileBytes + 1),
                },
                Enumerable.Range(0, 9)
                          .Select(index => (
                              $"aggregate-{index:D2}.bin",
                              SkinExternalPackageLogicalEntryKind.File,
                              SkinPackageRevisionCapsuleLimits.Default.MaxFileBytes))
                          .ToArray(),
            };

            foreach ((string Name, SkinExternalPackageLogicalEntryKind Kind, long Length)[] entries in malformedManifests)
            {
                (string encoded, string digest) = encodeManagedCopyManifest(entries);
                assertSemanticTamperRejected(
                    storage,
                    store,
                    journal,
                    payload =>
                    {
                        payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifest)] = encoded;
                        payload[nameof(SkinManagedFolderMutationJournal.ManagedCopyLogicalManifestDigest)] = digest;
                    });
            }
        }

        [Test]
        public void TestMaximumManagedCopyManifestRoundTripsBelowOneMiBJournalBoundary()
        {
            SkinManagedCopyLogicalManifest maximum = createBoundaryManagedCopyManifest(63);
            SkinManagedFolderMutationJournal journal = createPreparedManagedCopy(maximum);
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);

            store.Write(journal);
            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            long journalBytes = new FileInfo(getJournalPath(storage)).Length;

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.ManagedCopyLogicalManifest, Is.EqualTo(maximum.Encoded));
                Assert.That(loaded.Journal.ManagedCopyLogicalManifestDigest, Is.EqualTo(maximum.Digest));
                Assert.That(journalBytes, Is.GreaterThan(900 * 1024));
                Assert.That(journalBytes, Is.LessThanOrEqualTo(1024 * 1024));
                Assert.That(() => createBoundaryManagedCopyManifest(64), Throws.InvalidOperationException);
            });
        }

        [Test]
        public void TestJournalVersionAndPersistedEnumValuesAreFrozen()
        {
            Assert.Multiple(() =>
            {
                Assert.That(SkinManagedFolderMutationJournal.LEGACY_VERSION, Is.EqualTo(1));
                Assert.That(SkinManagedFolderMutationJournal.PRE_C1_VERSION, Is.EqualTo(2));
                Assert.That(SkinManagedFolderMutationJournal.CURRENT_VERSION, Is.EqualTo(3));
                Assert.That(
                    Enum.GetValues<SkinManagedFolderMutationKind>().Select(value => (int)value),
                    Is.EqualTo(new[] { 1, 2, 3, 4 }),
                    "Rename/StagedImport/Delete/ManagedCopy numeric values are durable schema.");
                Assert.That(
                    Enum.GetValues<SkinManagedFolderMutationPhase>().Select(value => (int)value),
                    Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6, 7 }),
                    "Prepared through ProvisionalReady numeric values are durable schema.");
                Assert.That(
                    Enum.GetValues<SkinManagedFolderDeleteFallbackDisposition>().Select(value => (int)value),
                    Is.EqualTo(new[] { 1, 2 }));
                Assert.That(
                    Enum.GetValues<SkinExternalCollisionDisposition>().Select(value => (int)value),
                    Is.EqualTo(new[] { 1, 2 }));
            });
        }

        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, (int)SkinManagedFolderMutationKind.Rename, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, (int)SkinManagedFolderMutationKind.Rename, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, (int)SkinManagedFolderMutationKind.StagedImport, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, (int)SkinManagedFolderMutationKind.StagedImport, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, (int)SkinManagedFolderMutationKind.Delete, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, (int)SkinManagedFolderMutationKind.Delete, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, (int)SkinManagedFolderMutationKind.Rename, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, (int)SkinManagedFolderMutationKind.Rename, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, (int)SkinManagedFolderMutationKind.StagedImport, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, (int)SkinManagedFolderMutationKind.StagedImport, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, (int)SkinManagedFolderMutationKind.Delete, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, (int)SkinManagedFolderMutationKind.Delete, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.CURRENT_VERSION, (int)SkinManagedFolderMutationKind.Rename, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.CURRENT_VERSION, (int)SkinManagedFolderMutationKind.Rename, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.CURRENT_VERSION, (int)SkinManagedFolderMutationKind.StagedImport, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.CURRENT_VERSION, (int)SkinManagedFolderMutationKind.StagedImport, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        [TestCase(SkinManagedFolderMutationJournal.CURRENT_VERSION, (int)SkinManagedFolderMutationKind.Delete, (int)SkinManagedFolderMutationPhase.Copying)]
        [TestCase(SkinManagedFolderMutationJournal.CURRENT_VERSION, (int)SkinManagedFolderMutationKind.Delete, (int)SkinManagedFolderMutationPhase.ProvisionalReady)]
        public void TestManagedCopyOnlyPhasesAreRejectedForEveryLegacyKindWithoutHandlerDispatch(
            int version,
            int kindValue,
            int phaseValue)
        {
            var kind = (SkinManagedFolderMutationKind)kindValue;
            using var storage = createStorage();
            var durableStore = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createCurrentPreparedJournal(kind);

            if (version == SkinManagedFolderMutationJournal.CURRENT_VERSION)
                durableStore.Write(journal);
            else
                writeVersionFixture(storage, journal, version);

            JObject document = readDocument(storage);
            var payload = (JObject)document["payload"]!;
            payload[nameof(SkinManagedFolderMutationJournal.Phase)] = phaseValue;
            document["sha256"] = computeChecksum(payload.ToString(Formatting.None));
            writeDocument(storage, document);

            SkinManagedFolderMutationJournalLoadResult loaded = durableStore.Load();
            var memoryStore = new MemoryMutationJournalStore(loaded);
            var handler = new RecordingRecoveryHandler(SkinManagedFolderMutationRecoveryDecision.RollForward);
            SkinManagedFolderMutationRecoveryResult recovery = new SkinManagedFolderMutationRecovery(
                memoryStore,
                new SkinManagedFolderOperationCoordinator(),
                handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
                Assert.That(recovery.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.InvalidJournal));
                Assert.That(handler.InspectCalls, Is.Zero);
                Assert.That(handler.ForwardCalls, Is.Zero);
                Assert.That(handler.RollbackCalls, Is.Zero);
            });
        }

        [TestCase((int)SkinManagedFolderMutationKind.Rename)]
        [TestCase((int)SkinManagedFolderMutationKind.StagedImport)]
        [TestCase((int)SkinManagedFolderMutationKind.Delete)]
        public void TestPreC1VersionTwoPreparedSchemaLoadsAndExistingJournalRoundTrips(int kindValue)
        {
            var kind = (SkinManagedFolderMutationKind)kindValue;
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal current = createCurrentPreparedJournal(kind);
            writeVersionFixture(
                storage,
                current,
                SkinManagedFolderMutationJournal.PRE_C1_VERSION);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal, Is.Not.Null);
                Assert.That(loaded.Journal!.Version, Is.EqualTo(SkinManagedFolderMutationJournal.PRE_C1_VERSION));
                Assert.That(loaded.Journal.Kind, Is.EqualTo(kind));
                Assert.That(loaded.Journal.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                Assert.That(loaded.Journal.ExternalRegistryGeneration, Is.Null);
                Assert.That(loaded.Journal.ExternalRegistryDigest, Is.Null);
                Assert.That(loaded.Journal.ExternalCollisionDisposition, Is.Null);
                Assert.That(loaded.Journal.IsValid(), Is.True);
            });

            // Rewriting an already-durable v2 intent remains supported for recovery. This is deliberately not a
            // new-v2 creation path: the canonical v2 fixture is present before Write() is called.
            store.Write(loaded.Journal!);
            assertLoadedEquivalent(store, loaded.Journal!);
            assertPayloadOmitsC1Fields(readDocument(storage));

            // Once absent, the exact same v2 prepared value cannot be used to create a new intent.
            File.Delete(getJournalPath(storage));
            Assert.That(() => store.Write(loaded.Journal!), Throws.InvalidOperationException);
        }

        [TestCase(
            (int)SkinManagedFolderMutationKind.Rename,
            (int)SkinManagedFolderMutationPhase.Prepared,
            (int)SkinManagedFolderMutationRecoveryDecision.RollBack,
            (int)SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
            (int)SkinManagedFolderMutationPhase.RolledBack)]
        [TestCase(
            (int)SkinManagedFolderMutationKind.StagedImport,
            (int)SkinManagedFolderMutationPhase.FilesystemApplied,
            (int)SkinManagedFolderMutationRecoveryDecision.RollBack,
            (int)SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
            (int)SkinManagedFolderMutationPhase.RolledBack)]
        [TestCase(
            (int)SkinManagedFolderMutationKind.Delete,
            (int)SkinManagedFolderMutationPhase.RealmApplied,
            (int)SkinManagedFolderMutationRecoveryDecision.RollForward,
            (int)SkinManagedFolderMutationRecoveryStatus.RecoveredForward,
            (int)SkinManagedFolderMutationPhase.Committed)]
        public void TestPreC1VersionTwoPhaseRecoveryRemainsCompatible(
            int kindValue,
            int phaseValue,
            int decisionValue,
            int expectedStatusValue,
            int expectedTerminalPhaseValue)
        {
            var kind = (SkinManagedFolderMutationKind)kindValue;
            var phase = (SkinManagedFolderMutationPhase)phaseValue;
            var decision = (SkinManagedFolderMutationRecoveryDecision)decisionValue;
            var expectedStatus = (SkinManagedFolderMutationRecoveryStatus)expectedStatusValue;
            var expectedTerminalPhase = (SkinManagedFolderMutationPhase)expectedTerminalPhaseValue;
            using var storage = createStorage();
            var durableStore = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal current = createCurrentRecoveryJournal(kind);

            Assert.That(current.Phase, Is.EqualTo(phase));
            writeVersionFixture(
                storage,
                current,
                SkinManagedFolderMutationJournal.PRE_C1_VERSION);
            SkinManagedFolderMutationJournalLoadResult loaded = durableStore.Load();
            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));

            var memoryStore = new MemoryMutationJournalStore(loaded.Journal!);
            var handler = new RecordingRecoveryHandler(decision);
            var recovery = new SkinManagedFolderMutationRecovery(
                memoryStore,
                new SkinManagedFolderOperationCoordinator(),
                handler);

            SkinManagedFolderMutationRecoveryResult result = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(expectedStatus));
                Assert.That(result.IsResolved, Is.True);
                Assert.That(memoryStore.Writes, Is.Not.Empty);
                Assert.That(memoryStore.Writes.Last().Version, Is.EqualTo(SkinManagedFolderMutationJournal.PRE_C1_VERSION));
                Assert.That(memoryStore.Writes.Last().Phase, Is.EqualTo(expectedTerminalPhase));
                Assert.That(memoryStore.Writes.All(write => write.IsValid()), Is.True);
                Assert.That(memoryStore.Writes.All(write => write.ExternalRegistryGeneration == null), Is.True);
                Assert.That(memoryStore.Writes.All(write => write.ExternalRegistryDigest == null), Is.True);
                Assert.That(memoryStore.Writes.All(write => write.ExternalCollisionDisposition == null), Is.True);
                Assert.That(memoryStore.DeleteCalls, Is.EqualTo(1));
                Assert.That(memoryStore.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
            });
        }

        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration))]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest))]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition))]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration))]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest))]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition))]
        public void TestFrozenLegacySchemasRejectOptionalC1Fields(int version, string propertyName)
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            writeVersionFixture(
                storage,
                SkinManagedFolderMutationJournal.CreatePreparedRename(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    root_identity,
                    source_path,
                    source_identity,
                    target_path),
                version);
            JObject document = readDocument(storage);
            var payload = (JObject)document["payload"]!;
            payload[propertyName] = propertyName switch
            {
                nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration) => 0,
                nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest) => SkinExternalFolderRegistry.EmptyRegistryDigest,
                _ => (int)SkinExternalCollisionDisposition.NoRegisteredExternalFolders,
            };
            document["sha256"] = computeChecksum(payload.ToString(Formatting.None));
            writeDocument(storage, document);

            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
        }

        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION)]
        public void TestFrozenLegacyVersionCannotStartNewIntent(int version)
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            writeVersionFixture(
                storage,
                SkinManagedFolderMutationJournal.CreatePreparedRename(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    root_identity,
                    source_path,
                    source_identity,
                    target_path),
                version);
            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));

            File.Delete(getJournalPath(storage));

            Assert.That(() => store.Write(loaded.Journal!), Throws.InvalidOperationException);
        }

        [Test]
        public void TestVersionThreeExternalRegistryBindingMissingUnknownOrMismatchedIsRejected()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = SkinManagedFolderMutationJournal.CreatePreparedRename(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                target_path);
            Action<JObject>[] tampers =
            {
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration)),
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest)),
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition)),
                payload => payload[nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration)] = -1,
                payload => payload[nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest)] = "not-a-digest",
                payload => payload[nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition)] = 999,
                payload => payload[nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration)] = 1,
                payload => payload[nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition)] =
                    (int)SkinExternalCollisionDisposition.ExactRegisteredExternalSet,
                payload => payload[nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest)] =
                    replacement_publication_fingerprint,
            };

            foreach (Action<JObject> tamper in tampers)
                assertSemanticTamperRejected(storage, store, journal, tamper);
        }

        [Test]
        public void TestVersionThreeMonotonicRewriteRejectsChangedExternalRegistryBinding()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            Guid operationId = Guid.NewGuid();
            Guid recordId = Guid.NewGuid();
            SkinManagedFolderMutationJournal emptyBinding = SkinManagedFolderMutationJournal.CreatePreparedRename(
                operationId,
                recordId,
                root_identity,
                source_path,
                source_identity,
                target_path);
            var exactBinding = new SkinExternalRegistryJournalBinding(
                1,
                replacement_publication_fingerprint,
                SkinExternalCollisionDisposition.ExactRegisteredExternalSet);
            SkinManagedFolderMutationJournal changedBinding = SkinManagedFolderMutationJournal.CreatePreparedRename(
                operationId,
                recordId,
                root_identity,
                source_path,
                source_identity,
                target_path,
                exactBinding);

            store.Write(emptyBinding);

            Assert.Multiple(() =>
            {
                Assert.That(emptyBinding.IsSameMonotonicIntent(changedBinding), Is.False);
                Assert.That(changedBinding.IsSameMonotonicIntent(emptyBinding), Is.False);
                Assert.That(() => store.Write(changedBinding), Throws.InvalidOperationException);
            });
            assertLoadedEquivalent(store, emptyBinding);
        }

        [Test]
        public void TestVersionThreeStagedPublicationFingerprintRoundTripsAndRemainsMonotonic()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedStagedImport();
            SkinManagedFolderMutationJournal filesystem =
                prepared.WithFilesystemApplied(staged_identity, publication_fingerprint);
            SkinManagedFolderMutationJournal realm = filesystem.WithRealmApplied();
            SkinManagedFolderMutationJournal committed = realm.WithCommitted();
            SkinManagedFolderMutationJournal changedRealm = prepared
                                                               .WithFilesystemApplied(
                                                                   staged_identity,
                                                                   replacement_publication_fingerprint)
                                                               .WithRealmApplied();

            Assert.That(prepared.Version, Is.EqualTo(SkinManagedFolderMutationJournal.CURRENT_VERSION));
            Assert.That(prepared.NewRecordPublicationFingerprint, Is.Null);

            store.Write(prepared);
            assertLoadedEquivalent(store, prepared);
            store.Write(filesystem);
            assertLoadedEquivalent(store, filesystem);

            Assert.Multiple(() =>
            {
                Assert.That(filesystem.NewRecordPublicationFingerprint, Is.EqualTo(publication_fingerprint));
                Assert.That(filesystem.IsSameMonotonicIntent(realm), Is.True);
                Assert.That(filesystem.IsSameMonotonicIntent(changedRealm), Is.False);
                Assert.That(() => store.Write(changedRealm), Throws.InvalidOperationException);
                Assert.That(
                    () => realm.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.Committed,
                        staged_identity,
                        replacement_publication_fingerprint),
                    Throws.InvalidOperationException);
            });
            assertLoadedEquivalent(store, filesystem);

            store.Write(realm);
            assertLoadedEquivalent(store, realm);
            store.Write(committed);
            assertLoadedEquivalent(store, committed);

            Assert.Multiple(() =>
            {
                Assert.That(realm.NewRecordPublicationFingerprint, Is.EqualTo(publication_fingerprint));
                Assert.That(committed.NewRecordPublicationFingerprint, Is.EqualTo(publication_fingerprint));
            });
        }

        [Test]
        public void TestVersionThreeStagedPublicationFingerprintTamperIsRejected()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedStagedImport();
            SkinManagedFolderMutationJournal filesystem =
                prepared.WithFilesystemApplied(staged_identity, publication_fingerprint);

            store.Write(prepared);
            store.Write(filesystem);
            JObject validDocument = readDocument(storage);

            assertPersistedPayloadTamperRejected(
                storage,
                store,
                validDocument,
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint)));
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                validDocument,
                payload => payload[nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint)] = "not-a-fingerprint");

            File.Delete(getJournalPath(storage));
            store.Write(prepared);
            JObject preparedDocument = readDocument(storage);
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                preparedDocument,
                payload => payload[nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint)] =
                    publication_fingerprint);
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                preparedDocument,
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.StagedSourceContentRevision)));
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                preparedDocument,
                payload => payload[nameof(SkinManagedFolderMutationJournal.StagedSourceContentRevision)] =
                    staged_content_revision.ToLowerInvariant());
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                preparedDocument,
                payload => payload.Remove(nameof(SkinManagedFolderMutationJournal.StagedSourceTreeFingerprint)));
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                preparedDocument,
                payload => payload[nameof(SkinManagedFolderMutationJournal.StagedSourceTreeFingerprint)] =
                    staged_tree_fingerprint.ToUpperInvariant());
        }

        [Test]
        public void TestVersionThreeStagedFixedRecordIdTamperIsRejected()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedStagedImport();
            store.Write(prepared);

            JObject document = readDocument(storage);
            var payload = (JObject)document["payload"]!;
            payload[nameof(SkinManagedFolderMutationJournal.OperationId)] =
                SkinInfo.OMS_SKIN.ToString();
            payload[nameof(SkinManagedFolderMutationJournal.RecordId)] =
                SkinInfo.OMS_SKIN.ToString();
            payload[nameof(SkinManagedFolderMutationJournal.StagedSourceRelativePath)] =
                SkinManagedFolderMutationJournal.GetExpectedStagedSourceRelativePath(
                    SkinInfo.OMS_SKIN);
            document["sha256"] =
                computeChecksum(payload.ToString(Formatting.None));
            writeDocument(storage, document);

            Assert.That(
                store.Load().Status,
                Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
        }

        [Test]
        public void TestLegacyVersionOneStagedFilesystemAppliedJournalCanRollbackWithoutFingerprint()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedStagedImport();
            SkinManagedFolderMutationJournal filesystem =
                prepared.WithFilesystemApplied(staged_identity, publication_fingerprint);
            store.Write(prepared);
            store.Write(filesystem);

            writeVersionFixture(
                storage,
                filesystem,
                SkinManagedFolderMutationJournal.LEGACY_VERSION);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal, Is.Not.Null);
                Assert.That(loaded.Journal!.Version, Is.EqualTo(SkinManagedFolderMutationJournal.LEGACY_VERSION));
                Assert.That(loaded.Journal.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                Assert.That(loaded.Journal.TargetIdentity, Is.EqualTo(staged_identity));
                Assert.That(loaded.Journal.StagedSourceContentRevision, Is.Null);
                Assert.That(loaded.Journal.StagedSourceTreeFingerprint, Is.Null);
                Assert.That(loaded.Journal.NewRecordPublicationFingerprint, Is.Null);
                Assert.That(loaded.Journal.IsValid(), Is.True);
            });

            Assert.That(
                () => loaded.Journal!.WithRecoveryTerminalPhase(
                    SkinManagedFolderMutationPhase.RolledBack,
                    staged_identity,
                    publication_fingerprint),
                Throws.ArgumentException);

            SkinManagedFolderMutationJournal rolledBack =
                loaded.Journal!.WithRecoveryTerminalPhase(
                    SkinManagedFolderMutationPhase.RolledBack);
            store.Write(rolledBack);
            assertLoadedEquivalent(store, rolledBack);
        }

        [Test]
        public void TestVersionThreeStagedRolledBackTargetAndFingerprintMustRemainPaired()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedStagedImport();

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => prepared.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.RolledBack,
                        staged_identity),
                    Throws.ArgumentException);
                Assert.That(
                    () => prepared.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.RolledBack,
                        recoveredNewRecordPublicationFingerprint: publication_fingerprint),
                    Throws.ArgumentException);
            });

            SkinManagedFolderMutationJournal rolledBack =
                prepared.WithRecoveryTerminalPhase(
                    SkinManagedFolderMutationPhase.RolledBack);
            store.Write(prepared);
            store.Write(rolledBack);
            assertLoadedEquivalent(store, rolledBack);

            JObject validDocument = readDocument(storage);
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                validDocument,
                payload => payload[nameof(SkinManagedFolderMutationJournal.TargetIdentity)] =
                    payload[nameof(SkinManagedFolderMutationJournal.StagedSourceIdentity)]!
                        .DeepClone());
            assertPersistedPayloadTamperRejected(
                storage,
                store,
                validDocument,
                payload => payload[nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint)] =
                    publication_fingerprint);

            File.Delete(getJournalPath(storage));
            SkinManagedFolderMutationJournal secondPrepared =
                createPreparedStagedImport();
            SkinManagedFolderMutationJournal rolledBackAfterFilesystem =
                secondPrepared
                    .WithFilesystemApplied(
                        staged_identity,
                        publication_fingerprint)
                    .WithRolledBack();
            store.Write(secondPrepared);
            store.Write(rolledBackAfterFilesystem);
            assertLoadedEquivalent(store, rolledBackAfterFilesystem);
        }

        [Test]
        public void TestLegacyVersionOneRenameCanRewriteAndDeleteTerminalJournal()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared =
                SkinManagedFolderMutationJournal.CreatePreparedRename(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    root_identity,
                    source_path,
                    source_identity,
                    target_path);
            store.Write(prepared);

            writeVersionFixture(
                storage,
                prepared,
                SkinManagedFolderMutationJournal.LEGACY_VERSION);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
            SkinManagedFolderMutationJournal terminal =
                loaded.Journal!.WithRecoveryTerminalPhase(
                    SkinManagedFolderMutationPhase.RolledBack);

            store.Write(terminal);
            assertLoadedEquivalent(store, terminal);
            store.Delete(terminal);

            Assert.Multiple(() =>
            {
                Assert.That(terminal.Version, Is.EqualTo(SkinManagedFolderMutationJournal.LEGACY_VERSION));
                Assert.That(
                    store.Load().Status,
                    Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
            });
        }

        [Test]
        public void TestStoreRejectsChecksumDuplicateUnknownVersionTruncationAndOversize()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createPreparedDelete();

            store.Write(journal);
            string validDocument = File.ReadAllText(getJournalPath(storage), strict_utf8);
            JObject document = readDocument(storage);
            document["sha256"] = new string('0', 64);
            writeDocument(storage, document);
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid), "checksum");

            writeRaw(
                storage,
                "{\"version\":1,\"version\":1,\"payload\":{},\"sha256\":\"\"}");
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid), "duplicate property");

            writeRaw(storage, validDocument);
            document = readDocument(storage);
            document["version"] = SkinManagedFolderMutationJournal.CURRENT_VERSION + 1;
            writeDocument(storage, document);
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.UnsupportedVersion), "unknown version");

            writeRaw(storage, "{\"version\":1,\"payload\":");
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid), "truncated");

            File.WriteAllBytes(getJournalPath(storage), new byte[1024 * 1024 + 1]);
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid), "oversize");
        }

        [Test]
        public void TestMalformedCodecValuesAreInvalidAndCannotEscapeStartupRecovery()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createPreparedDelete();

            File.WriteAllBytes(getJournalPath(storage), new byte[] { 0xff, 0xfe, 0xfd });
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid), "invalid UTF-8");

            File.Delete(getJournalPath(storage));
            store.Write(journal);
            string validDocument = File.ReadAllText(getJournalPath(storage), strict_utf8);
            writeRaw(
                storage,
                validDocument.Replace(
                    $"\"version\":{SkinManagedFolderMutationJournal.CURRENT_VERSION}",
                    "\"version\":999999999999999999999999999999999999",
                    StringComparison.Ordinal));
            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid), "overflowing envelope version");

            assertSemanticTamperRejected(
                storage,
                store,
                journal,
                payload => payload[nameof(SkinManagedFolderMutationJournal.OperationId)] = "not-a-guid");
            assertSemanticTamperRejected(
                storage,
                store,
                journal,
                payload =>
                {
                    var identity = (JObject)payload[nameof(SkinManagedFolderMutationJournal.SourceIdentity)]!;
                    identity[nameof(SkinManagedFolderPhysicalIdentity.VolumeSerialNumber)] =
                        JToken.Parse("18446744073709551616");
                });
        }

        [Test]
        public void TestStableMissingRequiresNoCanonicalOrJournalLikeSibling()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);

            SkinManagedFolderMutationJournalLoadResult first = store.Load();
            SkinManagedFolderMutationJournalLoadResult second = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(second.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
            });
        }

        [Test]
        public void TestCanonicalJournalPathAsDirectoryIsInvalid()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            Directory.CreateDirectory(getJournalPath(storage));

            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
        }

        [Test]
        public void TestLockedCanonicalJournalIsIoFailureAndDoesNotBecomeMissing()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            store.Write(journal);

            using (var locked = new FileStream(
                       getJournalPath(storage),
                       FileMode.Open,
                       FileAccess.ReadWrite,
                       FileShare.None))
            {
                Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.IoFailure));
            }

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.IsExactSameJournal(journal), Is.True);
            });
        }

        [Test]
        public void TestStableMissingCleansOnlyExactOrphanTemporaryJournal()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            string orphan = Path.Combine(
                storage.GetFullPath(string.Empty),
                $".{SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME}.{Guid.NewGuid():N}.tmp");
            File.WriteAllText(orphan, "uncommitted temporary journal", strict_utf8);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(File.Exists(orphan), Is.False);
            });
        }

        [Test]
        public void TestUnknownJournalLikeSiblingMakesMissingAnIoFailure()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            string unknownSibling = Path.Combine(
                storage.GetFullPath(string.Empty),
                $".{SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME}.not-a-guid.tmp");
            File.WriteAllText(unknownSibling, "unknown sibling", strict_utf8);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();

            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.IoFailure));
                Assert.That(File.Exists(unknownSibling), Is.True);
            });
        }

        [Test]
        public void TestDeleteRejectsNonTerminalJournalAndPreservesIt()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedDelete();
            store.Write(prepared);

            Assert.That(() => store.Delete(prepared), Throws.InvalidOperationException);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.IsExactSameJournal(prepared), Is.True);
            });
        }

        [Test]
        public void TestStoreRejectsAnyRewriteOfTerminalJournal()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedDelete();
            SkinManagedFolderMutationJournal filesystem = prepared.WithFilesystemApplied();
            SkinManagedFolderMutationJournal realm = filesystem.WithRealmApplied();
            SkinManagedFolderMutationJournal terminal = realm.WithCommitted();
            store.Write(prepared);
            Assert.That(() => store.Write(terminal), Throws.InvalidOperationException);
            store.Write(filesystem);
            store.Write(realm);
            store.Write(terminal);

            Assert.That(() => store.Write(terminal), Throws.InvalidOperationException);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.IsExactSameJournal(terminal), Is.True);
            });
        }

        [Test]
        public void TestStoreRejectsDifferentPreparedIntentOverwrite()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal first = createPreparedDelete();
            SkinManagedFolderMutationJournal second = createPreparedDelete();
            store.Write(first);

            Assert.That(() => store.Write(second), Throws.InvalidOperationException);

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.IsExactSameJournal(first), Is.True);
            });
        }

        [Test]
        public void TestFractionalAndStringNumericTokensAreRejectedWithMatchingChecksum()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal journal = createPreparedDelete();

            assertSemanticTamperRejected(
                storage,
                store,
                journal,
                payload => payload[nameof(SkinManagedFolderMutationJournal.Version)] = "1");
            assertSemanticTamperRejected(
                storage,
                store,
                journal,
                payload => payload[nameof(SkinManagedFolderMutationJournal.Kind)] = JToken.Parse("1.5"));
            assertSemanticTamperRejected(
                storage,
                store,
                journal,
                payload =>
                {
                    var identity = (JObject)payload[nameof(SkinManagedFolderMutationJournal.ManagedRootIdentity)]!;
                    identity[nameof(SkinManagedFolderPhysicalIdentity.VolumeSerialNumber)] = "11";
                });
            assertSemanticTamperRejected(
                storage,
                store,
                journal,
                payload =>
                {
                    var identity = (JObject)payload[nameof(SkinManagedFolderMutationJournal.SourceIdentity)]!;
                    identity[nameof(SkinManagedFolderPhysicalIdentity.FileIdPart0)] = JToken.Parse("12.5");
                });
        }

        [Test]
        public void TestPayloadSchemaAndPhaseTransitionsAreExactAndMonotonic()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal prepared = createPreparedDelete();

            assertSemanticTamperRejected(
                storage,
                store,
                prepared,
                payload =>
                {
                    JToken version = payload[nameof(SkinManagedFolderMutationJournal.Version)]!;
                    payload.Remove(nameof(SkinManagedFolderMutationJournal.Version));
                    payload["version"] = version;
                });
            assertSemanticTamperRejected(
                storage,
                store,
                prepared,
                payload => payload["Unexpected"] = true);

            SkinManagedFolderMutationJournal filesystem = prepared.WithFilesystemApplied();
            SkinManagedFolderMutationJournal realm = filesystem.WithRealmApplied();
            SkinManagedFolderMutationJournal committed = realm.WithCommitted();
            SkinManagedFolderMutationJournal renameFilesystem = SkinManagedFolderMutationJournal.CreatePreparedRename(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                target_path).WithFilesystemApplied(source_identity);

            Assert.Multiple(() =>
            {
                Assert.That(() => prepared.WithRealmApplied(), Throws.InvalidOperationException);
                Assert.That(() => prepared.WithCommitted(), Throws.InvalidOperationException);
                Assert.That(
                    () => prepared.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.Committed),
                    Throws.InvalidOperationException);
                Assert.That(() => filesystem.WithFilesystemApplied(), Throws.InvalidOperationException);
                Assert.That(
                    () => filesystem.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.Committed),
                    Throws.InvalidOperationException);
                Assert.That(() => committed.WithRolledBack(), Throws.InvalidOperationException);
                Assert.That(() => committed.WithRecoveryTerminalPhase(SkinManagedFolderMutationPhase.RolledBack), Throws.InvalidOperationException);
                Assert.That(
                    () => renameFilesystem.WithRecoveryTerminalPhase(
                        SkinManagedFolderMutationPhase.Committed,
                        target_identity),
                    Throws.InvalidOperationException);
            });
        }

        [Test]
        public void TestFailureBeforeAtomicReplacePreservesOldCompleteJournal()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal oldJournal = createPreparedDelete();
            SkinManagedFolderMutationJournal newJournal = oldJournal.WithFilesystemApplied();
            store.Write(oldJournal);
            store.BeforeAtomicReplace = () => throw new IOException("injected-before-replace");

            Assert.That(() => store.Write(newJournal), Throws.TypeOf<IOException>());

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.OperationId, Is.EqualTo(oldJournal.OperationId));
                Assert.That(loaded.Journal.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Prepared));
                Assert.That(findTemporaryJournals(storage), Is.Empty);
            });
        }

        [Test]
        public void TestFailureAfterAtomicReplaceLeavesNewCompleteJournal()
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal oldJournal = createPreparedDelete();
            SkinManagedFolderMutationJournal newJournal = oldJournal.WithFilesystemApplied();
            store.Write(oldJournal);
            store.AfterAtomicReplace = () => throw new IOException("injected-after-replace");

            Assert.That(() => store.Write(newJournal), Throws.TypeOf<IOException>());

            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.Multiple(() =>
            {
                Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
                Assert.That(loaded.Journal!.OperationId, Is.EqualTo(newJournal.OperationId));
                Assert.That(loaded.Journal.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                Assert.That(findTemporaryJournals(storage), Is.Empty);
            });
        }

        [TestCase(
            (int)SkinManagedFolderMutationRecoveryDecision.RollForward,
            (int)SkinManagedFolderMutationRecoveryStatus.RecoveredForward,
            (int)SkinManagedFolderMutationPhase.Committed,
            "forward")]
        [TestCase(
            (int)SkinManagedFolderMutationRecoveryDecision.RollBack,
            (int)SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
            (int)SkinManagedFolderMutationPhase.RolledBack,
            "rollback")]
        public void TestDeterminableRecoveryRunsIdempotentActionThenPersistsAndDeletes(
            int decisionValue,
            int expectedStatusValue,
            int expectedTerminalPhaseValue,
            string expectedActionEvent)
        {
            var decision = (SkinManagedFolderMutationRecoveryDecision)decisionValue;
            var expectedStatus = (SkinManagedFolderMutationRecoveryStatus)expectedStatusValue;
            var expectedTerminalPhase = (SkinManagedFolderMutationPhase)expectedTerminalPhaseValue;
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var events = new List<string>();
            var store = new MemoryMutationJournalStore(journal, events);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(decision, events);
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator, handler);

            SkinManagedFolderMutationRecoveryResult first = recovery.Recover();
            string[] expectedEvents =
                decision == SkinManagedFolderMutationRecoveryDecision.RollForward
                    ? new[]
                    {
                        "load",
                        "inspect",
                        $"write:{SkinManagedFolderMutationPhase.FilesystemApplied}",
                        "load",
                        "inspect",
                        expectedActionEvent,
                        "inspect",
                        $"write:{SkinManagedFolderMutationPhase.RealmApplied}",
                        "load",
                        "inspect",
                        $"write:{SkinManagedFolderMutationPhase.Committed}",
                        "load",
                        "delete",
                        "load",
                    }
                    : new[]
                    {
                        "load",
                        "inspect",
                        expectedActionEvent,
                        $"write:{SkinManagedFolderMutationPhase.RolledBack}",
                        "load",
                        "delete",
                        "load",
                    };

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(expectedStatus));
                Assert.That(first.IsResolved, Is.True);
                Assert.That(events, Is.EqualTo(expectedEvents));
                Assert.That(
                    store.Writes.Select(write => write.Phase),
                    Is.EqualTo(
                        decision == SkinManagedFolderMutationRecoveryDecision.RollForward
                            ? new[]
                            {
                                SkinManagedFolderMutationPhase.FilesystemApplied,
                                SkinManagedFolderMutationPhase.RealmApplied,
                                SkinManagedFolderMutationPhase.Committed,
                            }
                            : new[] { expectedTerminalPhase }));
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                Assert.That(handler.ForwardCalls, Is.EqualTo(decision == SkinManagedFolderMutationRecoveryDecision.RollForward ? 1 : 0));
                Assert.That(handler.RollbackCalls, Is.EqualTo(decision == SkinManagedFolderMutationRecoveryDecision.RollBack ? 1 : 0));
            });

            SkinManagedFolderMutationRecoveryResult second = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(second.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.NoJournal));
                Assert.That(
                    handler.InspectCalls,
                    Is.EqualTo(
                        decision == SkinManagedFolderMutationRecoveryDecision.RollForward
                            ? 4
                            : 1));
                Assert.That(handler.ForwardCalls + handler.RollbackCalls, Is.EqualTo(1));
                Assert.That(
                    store.Writes,
                    Has.Count.EqualTo(
                        decision == SkinManagedFolderMutationRecoveryDecision.RollForward
                            ? 3
                            : 1));
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(events.Last(), Is.EqualTo("load"));
            });
        }

        [TestCase((int)SkinManagedFolderMutationRecoveryDecision.RollForward)]
        [TestCase((int)SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted)]
        public void TestRenameForwardRecoveryMustPublishObservedTargetIdentity(int decisionValue)
        {
            var decision = (SkinManagedFolderMutationRecoveryDecision)decisionValue;
            SkinManagedFolderMutationJournal prepared = SkinManagedFolderMutationJournal.CreatePreparedRename(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                target_path);
            var store = new MemoryMutationJournalStore(prepared);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(decision, targetIdentity: source_identity);

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredForward));
                Assert.That(
                    store.Writes.Select(write => write.Phase),
                    Is.EqualTo(new[]
                    {
                        SkinManagedFolderMutationPhase.FilesystemApplied,
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    }));
                Assert.That(store.Writes.All(write => write.TargetIdentity == source_identity), Is.True);
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestRenameRecoveryWithoutTargetIdentityRemainsAmbiguous()
        {
            SkinManagedFolderMutationJournal prepared = SkinManagedFolderMutationJournal.CreatePreparedRename(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                target_path);
            var store = new MemoryMutationJournalStore(prepared);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted);

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
            });
        }

        [TestCase((int)SkinManagedFolderMutationPhase.Committed)]
        [TestCase((int)SkinManagedFolderMutationPhase.RolledBack)]
        public void TestTerminalJournalIsRemovedWithoutRecoveryAction(int terminalPhaseValue)
        {
            var terminalPhase = (SkinManagedFolderMutationPhase)terminalPhaseValue;
            SkinManagedFolderMutationJournal terminal =
                terminalPhase == SkinManagedFolderMutationPhase.Committed
                    ? createCommittedDelete()
                    : createPreparedDelete().WithRecoveryTerminalPhase(terminalPhase);
            var store = new MemoryMutationJournalStore(terminal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator);

            SkinManagedFolderMutationRecoveryResult result = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal));
                Assert.That(result.IsResolved, Is.True);
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        [Test]
        public void TestAmbiguousJournalIsRetainedAndFreezesOnlyExactAffectedPaths()
        {
            SkinManagedFolderMutationJournal journal = SkinManagedFolderMutationJournal.CreatePreparedRename(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                target_path);
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator);

            SkinManagedFolderMutationRecoveryResult first = recovery.Recover();
            SkinManagedFolderMutationRecoveryResult second = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(second.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(first.IsResolved, Is.False);
                Assert.That(store.Current.IsLoaded, Is.True);
                Assert.That(store.Current.Journal, Is.SameAs(journal));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.False);
                Assert.That(coordinator.IsMutationBlocked, Is.True);
            });
        }

        [Test]
        public void TestRecoveryInspectionFaultRetainsJournalAndExactFreeze()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(SkinManagedFolderMutationRecoveryDecision.RollForward)
            {
                ThrowOnInspect = true,
            };

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(store.Current.Journal, Is.SameAs(journal));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.False);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRecoveryActionFailureRetainsJournalAndExactFreeze(bool throws)
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(SkinManagedFolderMutationRecoveryDecision.RollForward)
            {
                ActionSucceeds = false,
                ThrowOnForward = throws,
            };
            SkinManagedFolderMutationJournal filesystem = journal.WithFilesystemApplied();

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(handler.ForwardCalls, Is.EqualTo(1));
                Assert.That(store.Current.Journal!.IsExactSameJournal(filesystem), Is.True);
                Assert.That(store.Writes, Has.Count.EqualTo(1));
                Assert.That(store.Writes[0].Phase, Is.EqualTo(SkinManagedFolderMutationPhase.FilesystemApplied));
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.False);
            });
        }

        [Test]
        public void TestRecoveryWriteFaultRetainsJournalAndFreezesNamespace()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal)
            {
                ThrowOnWrite = true,
            };
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(SkinManagedFolderMutationRecoveryDecision.RollForward);

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(handler.ForwardCalls, Is.Zero);
                Assert.That(store.Current.Journal, Is.SameAs(journal));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.True);
            });
        }

        [TestCase(
            (int)SkinManagedFolderMutationPhase.RealmApplied,
            (int)SkinManagedFolderMutationPhase.FilesystemApplied)]
        [TestCase(
            (int)SkinManagedFolderMutationPhase.Committed,
            (int)SkinManagedFolderMutationPhase.RealmApplied)]
        public void TestForwardRecoveryCheckpointFailureRestartsFromLastDurablePhaseWithoutRepeatingAction(
            int faultPhaseValue,
            int expectedRetainedPhaseValue)
        {
            var faultPhase = (SkinManagedFolderMutationPhase)faultPhaseValue;
            var expectedRetainedPhase =
                (SkinManagedFolderMutationPhase)expectedRetainedPhaseValue;
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal)
            {
                ThrowOnceOnWritePhase = faultPhase,
            };
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollForward);
            var firstCoordinator = new SkinManagedFolderOperationCoordinator();

            SkinManagedFolderMutationRecoveryResult first =
                new SkinManagedFolderMutationRecovery(
                    store,
                    firstCoordinator,
                    handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(
                    first.Status,
                    Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus
                            .JournalIoFailure));
                Assert.That(store.Current.Journal!.Phase, Is.EqualTo(expectedRetainedPhase));
                Assert.That(handler.ForwardCalls, Is.EqualTo(1));
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(firstCoordinator.IsPathFrozen("chartskin/unrelated"), Is.True);
            });

            var restartedCoordinator =
                new SkinManagedFolderOperationCoordinator();
            SkinManagedFolderMutationRecoveryResult restarted =
                new SkinManagedFolderMutationRecovery(
                    store,
                    restartedCoordinator,
                    handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(
                    restarted.Status,
                    Is.EqualTo(
                        SkinManagedFolderMutationRecoveryStatus
                            .RecoveredForward));
                Assert.That(handler.ForwardCalls, Is.EqualTo(1));
                Assert.That(
                    store.Writes.Select(write => write.Phase),
                    Is.EqualTo(new[]
                    {
                        SkinManagedFolderMutationPhase.FilesystemApplied,
                        SkinManagedFolderMutationPhase.RealmApplied,
                        SkinManagedFolderMutationPhase.Committed,
                    }));
                Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(restartedCoordinator.IsMutationBlocked, Is.False);
            });
        }

        [Test]
        public void TestRecoveryDeleteFaultRetainsTerminalJournalAndFreezesNamespace()
        {
            SkinManagedFolderMutationJournal terminal = createCommittedDelete();
            var store = new MemoryMutationJournalStore(terminal)
            {
                ThrowOnDelete = true,
            };
            var coordinator = new SkinManagedFolderOperationCoordinator();

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(store.Current.Journal, Is.SameAs(terminal));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.True);
            });
        }

        [Test]
        public void TestPriorAmbiguousIntentRemainsFrozenIfJournalLaterAppearsMissing()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator);

            SkinManagedFolderMutationRecoveryResult first = recovery.Recover();
            store.SetCurrent(new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Missing));
            SkinManagedFolderMutationRecoveryResult second = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(second.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(second.IsResolved, Is.False);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsMutationBlocked, Is.True);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestManagedRootReplacementDuringInspectionOrActionIsAmbiguous(bool replacementDuringAction)
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollForward,
                inspectionRootIdentity: replacementDuringAction ? null : replacement_root_identity,
                actionRootIdentity: replacementDuringAction ? replacement_root_identity : null);
            SkinManagedFolderMutationJournal filesystem = journal.WithFilesystemApplied();

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(handler.ForwardCalls, Is.EqualTo(replacementDuringAction ? 1 : 0));
                Assert.That(
                    store.Current.Journal!.IsExactSameJournal(
                        replacementDuringAction
                            ? filesystem
                            : journal),
                    Is.True);
                Assert.That(
                    store.Writes,
                    Has.Count.EqualTo(replacementDuringAction ? 1 : 0));
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
            });
        }

        [Test]
        public void TestTerminalDeleteIsConfirmedAndRetriedIfExactJournalRemains()
        {
            SkinManagedFolderMutationJournal terminal = createCommittedDelete();
            var store = new MemoryMutationJournalStore(terminal)
            {
                DeleteNoOpCallsRemaining = 1,
            };
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator);

            SkinManagedFolderMutationRecoveryResult first = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure));
                Assert.That(first.IsResolved, Is.False);
                Assert.That(store.Current.Journal, Is.SameAs(terminal));
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
            });

            SkinManagedFolderMutationRecoveryResult second = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(second.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal));
                Assert.That(second.IsResolved, Is.True);
                Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(store.DeleteCalls, Is.EqualTo(2));
                Assert.That(coordinator.IsPathFrozen(source_path), Is.False);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        private static void assertRoundTrip(SkinManagedFolderMutationJournal expected)
        {
            using var storage = createStorage();
            var store = new SkinManagedFolderMutationJournalStore(storage);
            store.Write(expected);
            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();

            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
            Assert.That(loaded.Journal, Is.Not.Null);
            assertEquivalent(expected, loaded.Journal!);
        }

        private static void assertEquivalent(
            SkinManagedFolderMutationJournal expected,
            SkinManagedFolderMutationJournal actual)
        {
            Assert.Multiple(() =>
            {
                Assert.That(actual.Version, Is.EqualTo(expected.Version));
                Assert.That(actual.OperationId, Is.EqualTo(expected.OperationId));
                Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
                Assert.That(actual.Phase, Is.EqualTo(expected.Phase));
                Assert.That(actual.RecordId, Is.EqualTo(expected.RecordId));
                Assert.That(actual.ManagedRootIdentity, Is.EqualTo(expected.ManagedRootIdentity));
                Assert.That(actual.SourceManagedRelativePath, Is.EqualTo(expected.SourceManagedRelativePath));
                Assert.That(actual.TargetManagedRelativePath, Is.EqualTo(expected.TargetManagedRelativePath));
                Assert.That(actual.SourceIdentity, Is.EqualTo(expected.SourceIdentity));
                Assert.That(actual.TargetIdentity, Is.EqualTo(expected.TargetIdentity));
                Assert.That(actual.StagedSourceAuthority, Is.EqualTo(expected.StagedSourceAuthority));
                Assert.That(actual.StagedSourceRelativePath, Is.EqualTo(expected.StagedSourceRelativePath));
                Assert.That(actual.StagedSourceIdentity, Is.EqualTo(expected.StagedSourceIdentity));
                Assert.That(actual.StagedRootIdentity, Is.EqualTo(expected.StagedRootIdentity));
                Assert.That(actual.NewRecordPublicationPlanVersion, Is.EqualTo(expected.NewRecordPublicationPlanVersion));
                Assert.That(actual.StagedSourceContentRevision, Is.EqualTo(expected.StagedSourceContentRevision));
                Assert.That(actual.StagedSourceTreeFingerprint, Is.EqualTo(expected.StagedSourceTreeFingerprint));
                Assert.That(actual.NewRecordPublicationFingerprint, Is.EqualTo(expected.NewRecordPublicationFingerprint));
                Assert.That(actual.DeleteSourceNodeManifest, Is.EqualTo(expected.DeleteSourceNodeManifest));
                Assert.That(actual.DeleteFallbackDisposition, Is.EqualTo(expected.DeleteFallbackDisposition));
                Assert.That(actual.ExternalRegistryGeneration, Is.EqualTo(expected.ExternalRegistryGeneration));
                Assert.That(actual.ExternalRegistryDigest, Is.EqualTo(expected.ExternalRegistryDigest));
                Assert.That(actual.ExternalCollisionDisposition, Is.EqualTo(expected.ExternalCollisionDisposition));
                Assert.That(actual.ManagedCopyExternalRecordFingerprint, Is.EqualTo(expected.ManagedCopyExternalRecordFingerprint));
                Assert.That(actual.ManagedCopyExternalCaptureFingerprint, Is.EqualTo(expected.ManagedCopyExternalCaptureFingerprint));
                Assert.That(actual.ManagedCopyLogicalManifest, Is.EqualTo(expected.ManagedCopyLogicalManifest));
                Assert.That(actual.ManagedCopyLogicalManifestDigest, Is.EqualTo(expected.ManagedCopyLogicalManifestDigest));
                Assert.That(actual.GetAffectedManagedRelativePaths(), Is.EqualTo(expected.GetAffectedManagedRelativePaths()));
                Assert.That(actual.IsValid(), Is.True);
            });
        }

        private static SkinManagedFolderMutationJournal createCurrentPreparedJournal(
            SkinManagedFolderMutationKind kind)
            => kind switch
            {
                SkinManagedFolderMutationKind.Rename =>
                    SkinManagedFolderMutationJournal.CreatePreparedRename(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        root_identity,
                        source_path,
                        source_identity,
                        target_path),

                SkinManagedFolderMutationKind.StagedImport =>
                    createPreparedStagedImport(),

                SkinManagedFolderMutationKind.Delete =>
                    createPreparedDelete(),

                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

        private static SkinManagedFolderMutationJournal createCurrentRecoveryJournal(
            SkinManagedFolderMutationKind kind)
            => kind switch
            {
                SkinManagedFolderMutationKind.Rename =>
                    createCurrentPreparedJournal(kind),

                SkinManagedFolderMutationKind.StagedImport =>
                    createPreparedStagedImport().WithFilesystemApplied(
                        staged_identity,
                        publication_fingerprint),

                SkinManagedFolderMutationKind.Delete =>
                    createPreparedDelete()
                        .WithFilesystemApplied()
                        .WithRealmApplied(),

                _ => throw new ArgumentOutOfRangeException(nameof(kind)),
            };

        internal static void writeVersionFixture(
            TemporaryNativeStorage storage,
            SkinManagedFolderMutationJournal current,
            int version)
        {
            if (version is not (SkinManagedFolderMutationJournal.LEGACY_VERSION
                or SkinManagedFolderMutationJournal.PRE_C1_VERSION))
            {
                throw new ArgumentOutOfRangeException(nameof(version));
            }

            string currentPayload = JsonConvert.SerializeObject(
                current,
                Formatting.None,
                SkinManagedFolderMutationJson.CreateSettings());
            var payload = JObject.Parse(currentPayload);
            payload[nameof(SkinManagedFolderMutationJournal.Version)] = version;
            payload.Remove(nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration));
            payload.Remove(nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest));
            payload.Remove(nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition));

            if (version == SkinManagedFolderMutationJournal.LEGACY_VERSION)
            {
                payload.Remove(nameof(SkinManagedFolderMutationJournal.StagedSourceContentRevision));
                payload.Remove(nameof(SkinManagedFolderMutationJournal.StagedSourceTreeFingerprint));
                payload.Remove(nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint));
                payload.Remove(nameof(SkinManagedFolderMutationJournal.DeleteSourceNodeManifest));
                payload.Remove(nameof(SkinManagedFolderMutationJournal.DeleteFallbackDisposition));
            }

            var document = new JObject
            {
                ["version"] = version,
                ["payload"] = payload,
                ["sha256"] = computeChecksum(payload.ToString(Formatting.None)),
            };
            writeDocument(storage, document);
        }

        private static void assertPayloadOmitsC1Fields(JObject document)
        {
            var payload = (JObject)document["payload"]!;

            Assert.Multiple(() =>
            {
                Assert.That(payload.ContainsKey(nameof(SkinManagedFolderMutationJournal.ExternalRegistryGeneration)), Is.False);
                Assert.That(payload.ContainsKey(nameof(SkinManagedFolderMutationJournal.ExternalRegistryDigest)), Is.False);
                Assert.That(payload.ContainsKey(nameof(SkinManagedFolderMutationJournal.ExternalCollisionDisposition)), Is.False);
            });
        }

        private static void assertLoadedEquivalent(
            SkinManagedFolderMutationJournalStore store,
            SkinManagedFolderMutationJournal expected)
        {
            SkinManagedFolderMutationJournalLoadResult loaded = store.Load();
            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
            Assert.That(loaded.Journal, Is.Not.Null);
            assertEquivalent(expected, loaded.Journal!);
        }

        private static void assertSemanticTamperRejected(
            TemporaryNativeStorage storage,
            SkinManagedFolderMutationJournalStore store,
            SkinManagedFolderMutationJournal journal,
            Action<JObject> tamper)
        {
            string journalPath = getJournalPath(storage);

            if (File.Exists(journalPath))
                File.Delete(journalPath);

            store.Write(journal);
            JObject document = readDocument(storage);
            var payload = (JObject)document["payload"]!;
            tamper(payload);
            document["sha256"] = computeChecksum(payload.ToString(Formatting.None));
            writeDocument(storage, document);

            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
        }

        private static void assertPersistedPayloadTamperRejected(
            TemporaryNativeStorage storage,
            SkinManagedFolderMutationJournalStore store,
            JObject validDocument,
            Action<JObject> tamper)
        {
            var document = (JObject)validDocument.DeepClone();
            var payload = (JObject)document["payload"]!;
            tamper(payload);
            document["sha256"] = computeChecksum(payload.ToString(Formatting.None));
            writeDocument(storage, document);

            Assert.That(store.Load().Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Invalid));
        }

        private static SkinManagedFolderMutationJournal createPreparedDelete()
            => SkinManagedFolderMutationJournal.CreatePreparedDelete(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity,
                publication_fingerprint,
                SkinManagedFolderDeleteManifest.Create(
                    new[] { publication_fingerprint }))
                .WithDeleteFallbackDisposition(
                    SkinManagedFolderDeleteFallbackDisposition.NotRequired);

        private static SkinManagedFolderMutationJournal createCommittedDelete()
            => createPreparedDelete()
                .WithFilesystemApplied()
                .WithRealmApplied()
                .WithCommitted();

        private static SkinManagedFolderMutationJournal createPreparedStagedImport()
            => SkinManagedFolderMutationJournal.CreatePreparedStagedImport(
                Guid.NewGuid(),
                root_identity,
                target_path,
                staged_identity,
                staged_root_identity,
                staged_content_revision,
                staged_tree_fingerprint);

        private static SkinManagedFolderMutationJournal createPreparedManagedCopy(
            SkinManagedCopyLogicalManifest? manifest = null)
            => SkinManagedFolderMutationJournal.CreatePreparedManagedCopy(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                target_path,
                staged_root_identity,
                staged_content_revision,
                publication_fingerprint,
                replacement_publication_fingerprint,
                manifest ?? createManagedCopyManifest(
                    SkinPackageCapturedEntry.CreateDirectory("empty"),
                    SkinPackageCapturedEntry.CreateFile("skin.ini", new byte[] { 1, 2, 3 })),
                new SkinExternalRegistryJournalBinding(
                    1,
                    replacement_publication_fingerprint,
                    SkinExternalCollisionDisposition.ExactRegisteredExternalSet));

        private static SkinManagedCopyLogicalManifest createBoundaryManagedCopyManifest(int paddingLength)
        {
            var entries = new SkinPackageCapturedEntry[8191];
            entries[0] = SkinPackageCapturedEntry.CreateDirectory("m");
            string padding = new string('x', paddingLength);

            for (int i = 1; i < entries.Length; i++)
            {
                entries[i] = SkinPackageCapturedEntry.CreateFile(
                    $"m/{i - 1:D4}-{padding}.bin",
                    Array.Empty<byte>());
            }

            return createManagedCopyManifest(entries);
        }

        private static SkinManagedCopyLogicalManifest createManagedCopyManifest(
            params SkinPackageCapturedEntry?[] entries)
        {
            SkinPackageRevisionCapsuleCreationResult capsuleResult =
                SkinPackageRevisionCapsuleFactory.Create(entries);
            Assert.That(capsuleResult.IsSuccess, Is.True);
            Assert.That(capsuleResult.Capsule, Is.Not.Null);

            using SkinPackageRevisionCapsule capsule = capsuleResult.Capsule!;
            bool created = SkinExternalPackageLogicalManifest.TryCreate(
                entries,
                capsule,
                int.MaxValue,
                out SkinExternalPackageLogicalManifest? externalManifest);
            Assert.That(created, Is.True);
            Assert.That(externalManifest, Is.Not.Null);
            return SkinManagedCopyLogicalManifest.Create(externalManifest!);
        }

        private static (string Encoded, string Digest) encodeManagedCopyManifest(
            IReadOnlyList<(string Name, SkinExternalPackageLogicalEntryKind Kind, long Length)> entries)
        {
            using var stream = new MemoryStream();
            stream.Write(Encoding.ASCII.GetBytes("OMS/SkinManagedCopyLogicalManifest/v1\0"));
            writeInt32(1);
            writeInt32(entries.Count);

            foreach ((string name, SkinExternalPackageLogicalEntryKind kind, long length) in entries)
            {
                byte[] nameBytes = strict_utf8.GetBytes(name);
                writeInt32(nameBytes.Length);
                stream.Write(nameBytes);
                stream.WriteByte((byte)kind);
                writeInt64(length);
            }

            byte[] payload = stream.ToArray();
            return (
                Convert.ToBase64String(payload),
                Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant());

            void writeInt32(int value)
            {
                Span<byte> bytes = stackalloc byte[sizeof(int)];
                BinaryPrimitives.WriteInt32BigEndian(bytes, value);
                stream.Write(bytes);
            }

            void writeInt64(long value)
            {
                Span<byte> bytes = stackalloc byte[sizeof(long)];
                BinaryPrimitives.WriteInt64BigEndian(bytes, value);
                stream.Write(bytes);
            }
        }

        internal static TemporaryNativeStorage createStorage([System.Runtime.CompilerServices.CallerMemberName] string testName = "")
            => new TemporaryNativeStorage($"{testName}-{Guid.NewGuid():N}");

        private static string getJournalPath(TemporaryNativeStorage storage)
            => storage.GetFullPath(SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME);

        private static JObject readDocument(TemporaryNativeStorage storage)
            => JObject.Parse(File.ReadAllText(getJournalPath(storage), strict_utf8));

        private static void writeDocument(TemporaryNativeStorage storage, JObject document)
            => writeRaw(storage, document.ToString(Formatting.None));

        private static void writeRaw(TemporaryNativeStorage storage, string contents)
            => File.WriteAllText(getJournalPath(storage), contents, strict_utf8);

        private static string computeChecksum(string value)
            => Convert.ToHexString(SHA256.HashData(strict_utf8.GetBytes(value))).ToLowerInvariant();

        private static string[] findTemporaryJournals(TemporaryNativeStorage storage)
            => Directory.GetFiles(
                storage.GetFullPath(string.Empty),
                $".{SkinManagedFolderMutationJournalStore.JOURNAL_FILENAME}.*.tmp",
                SearchOption.TopDirectoryOnly);
    }

    [TestFixture]
    public class SkinManagedFolderMutationRecoveryScannerTest : RealmTest
    {
        private const string source_path = "chartskin/recovery-source";
        private const string target_path = "chartskin/recovery-target";
        private const string unrelated_path = "chartskin/unrelated";

        [Test]
        public void TestAmbiguousRecoveryRetainsJournalAndSuppressesNegativeCleanupOnlyForAffectedPaths()
        {
            RunTestWithRealm((realm, _) =>
            {
                Guid source = addRecord(realm, source_path);
                Guid target = addRecord(realm, target_path);
                Guid unrelated = addRecord(realm, unrelated_path);
                SkinManagedFolderMutationJournal journal = SkinManagedFolderMutationJournal.CreatePreparedRename(
                    Guid.NewGuid(),
                    source,
                    new SkinManagedFolderPhysicalIdentity(41, 401, 402),
                    source_path,
                    new SkinManagedFolderPhysicalIdentity(41, 42, 43),
                    target_path);
                var store = new MemoryMutationJournalStore(journal);
                var coordinator = new SkinManagedFolderOperationCoordinator();

                SkinManagedFolderMutationRecoveryResult recovery = new SkinManagedFolderMutationRecovery(store, coordinator).Recover();
                SkinManagedFolderScanResult scan = new SkinManagedFolderScanner(
                    realm,
                    new StaticDiscoverySource(SkinManagedFolderDiscoverySnapshot.Complete(
                        Array.Empty<string>(),
                        Array.Empty<SkinManagedFolderDiscovery>())),
                    coordinator).Scan();

                Assert.Multiple(() =>
                {
                    Assert.That(recovery.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                    Assert.That(store.Current.Journal, Is.SameAs(journal));
                    Assert.That(store.DeleteCalls, Is.Zero);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(target_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(unrelated_path), Is.False);

                    Assert.That(scan.IsSuccess, Is.True);
                    Assert.That(scan.SoftDeleted, Is.EqualTo(1));
                    Assert.That(scan.Conflicts, Is.EqualTo(2));
                    Assert.That(realm.Realm.Find<SkinInfo>(source)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(target)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(unrelated)!.DeletePending, Is.True);
                });
            });
        }

        [TestCase(
            (int)SkinManagedFolderMutationJournalLoadStatus.Invalid,
            (int)SkinManagedFolderMutationRecoveryStatus.InvalidJournal)]
        [TestCase(
            (int)SkinManagedFolderMutationJournalLoadStatus.UnsupportedVersion,
            (int)SkinManagedFolderMutationRecoveryStatus.UnsupportedJournal)]
        public void TestInvalidOrUnknownJournalFreezesNamespaceAndSuppressesAllNegativeCleanup(
            int loadStatusValue,
            int expectedRecoveryStatusValue)
        {
            var loadStatus = (SkinManagedFolderMutationJournalLoadStatus)loadStatusValue;
            var expectedRecoveryStatus = (SkinManagedFolderMutationRecoveryStatus)expectedRecoveryStatusValue;

            RunTestWithRealm((realm, _) =>
            {
                Guid first = addRecord(realm, source_path);
                Guid second = addRecord(realm, unrelated_path);
                var store = new MemoryMutationJournalStore(new SkinManagedFolderMutationJournalLoadResult(loadStatus));
                var coordinator = new SkinManagedFolderOperationCoordinator();

                SkinManagedFolderMutationRecoveryResult recovery = new SkinManagedFolderMutationRecovery(store, coordinator).Recover();
                SkinManagedFolderScanResult scan = new SkinManagedFolderScanner(
                    realm,
                    new StaticDiscoverySource(SkinManagedFolderDiscoverySnapshot.Complete(
                        Array.Empty<string>(),
                        Array.Empty<SkinManagedFolderDiscovery>())),
                    coordinator).Scan();

                Assert.Multiple(() =>
                {
                    Assert.That(recovery.Status, Is.EqualTo(expectedRecoveryStatus));
                    Assert.That(recovery.IsResolved, Is.False);
                    Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen(unrelated_path), Is.True);
                    Assert.That(coordinator.IsPathFrozen("chartskin/any-other-path"), Is.True);
                    Assert.That(coordinator.IsMutationBlocked, Is.True);

                    Assert.That(scan.IsSuccess, Is.True);
                    Assert.That(scan.SoftDeleted, Is.Zero);
                    Assert.That(scan.Conflicts, Is.EqualTo(2));
                    Assert.That(realm.Realm.Find<SkinInfo>(first)!.DeletePending, Is.False);
                    Assert.That(realm.Realm.Find<SkinInfo>(second)!.DeletePending, Is.False);
                    Assert.That(store.DeleteCalls, Is.Zero);
                });
            });
        }

        [Test]
        public void TestC1RecoveryHoldsExactEmptyAuthorityThroughTerminalDelete()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: true);
            store.AfterWrite = _ => Assert.That(authority.ActiveSessions, Is.EqualTo(1));
            store.BeforeDelete = () => Assert.That(authority.ActiveSessions, Is.EqualTo(1));
            var recovery = new SkinManagedFolderMutationRecovery(
                store,
                coordinator,
                handler,
                authority);

            SkinManagedFolderMutationRecoveryResult result = recovery.Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                Assert.That(authority.ObservedMutationLease, Is.True);
                Assert.That(authority.OpenCalls, Is.EqualTo(1));
                Assert.That(authority.ActiveSessions, Is.Zero);
                Assert.That(authority.DisposedSessions, Is.EqualTo(1));
                Assert.That(authority.ValidateCalls, Is.GreaterThan(3));
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        [Test]
        public void TestC1RecoveryAllowsExactNonEmptyExternalRegistryBinding()
        {
            var binding = new SkinExternalRegistryJournalBinding(
                77,
                new string('a', 64),
                SkinExternalCollisionDisposition.ExactRegisteredExternalSet);
            SkinManagedFolderMutationJournal journal = createPreparedDelete(binding);
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: false,
                binding);

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler,
                    authority)
                .Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                Assert.That(handler.RollbackCalls, Is.EqualTo(1));
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(authority.DisposedSessions, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestC1RecoveryRejectsExternalBindingMismatchBeforeHandler()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var foreignBinding = new SkinExternalRegistryJournalBinding(
                88,
                new string('b', 64),
                SkinExternalCollisionDisposition.ExactRegisteredExternalSet);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: false,
                foreignBinding);

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler,
                    authority)
                .Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(handler.InspectCalls, Is.Zero);
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(authority.ActiveSessions, Is.Zero);
                Assert.That(coordinator.IsMutationBlocked, Is.True);
            });
        }

        [Test]
        public void TestC1RecoveryAuthorityDriftAfterWriteStopsBeforeTerminalDelete()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: true);
            store.AfterWrite = _ => authority.Invalidate();

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler,
                    authority)
                .Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(store.Writes.Select(write => write.Phase),
                    Is.EqualTo(new[] { SkinManagedFolderMutationPhase.RolledBack }));
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(store.Current.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.RolledBack));
                Assert.That(authority.ActiveSessions, Is.Zero);
                Assert.That(coordinator.IsMutationBlocked, Is.True);
            });
        }

        [Test]
        public void TestC1RecoveryAuthorityDriftImmediatelyBeforeDeletePreservesTerminalJournal()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: true)
            {
                InvalidateOnValidateCall = 8,
            };

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler,
                    authority)
                .Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(store.Writes.Select(write => write.Phase),
                    Is.EqualTo(new[] { SkinManagedFolderMutationPhase.RolledBack }));
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(store.Current.Journal!.Phase, Is.EqualTo(SkinManagedFolderMutationPhase.RolledBack));
                Assert.That(coordinator.IsMutationBlocked, Is.True);
            });
        }

        [Test]
        public void TestC1RecoveryAuthorityDriftAfterCompareDeleteMissingRemainsResolved()
        {
            SkinManagedFolderMutationJournal prepared = createPreparedDelete();
            SkinManagedFolderMutationJournal terminal = prepared.WithRecoveryTerminalPhase(
                SkinManagedFolderMutationPhase.RolledBack,
                null,
                prepared.NewRecordPublicationFingerprint);
            var store = new MemoryMutationJournalStore(terminal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: true,
                allowTerminalWithoutHandler: true);
            store.AfterDelete = authority.Invalidate;

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    recoveryAuthority: authority)
                .Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal));
                Assert.That(result.IsResolved, Is.True);
                Assert.That(store.DeleteCalls, Is.EqualTo(1));
                Assert.That(store.Current.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Missing));
                Assert.That(authority.Invalidated, Is.True);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, false)]
        [TestCase(SkinManagedFolderMutationJournal.LEGACY_VERSION, true)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, false)]
        [TestCase(SkinManagedFolderMutationJournal.PRE_C1_VERSION, true)]
        public void TestPreC1RecoveryRequiresEmptyExternalRegistry(
            int version,
            bool externalRegistryPresent)
        {
            using var storage = SkinManagedFolderMutationJournalTest.createStorage();
            var durableStore = new SkinManagedFolderMutationJournalStore(storage);
            SkinManagedFolderMutationJournal legacyCompatible =
                SkinManagedFolderMutationJournal.CreatePreparedRename(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    new SkinManagedFolderPhysicalIdentity(41, 401, 402),
                    source_path,
                    new SkinManagedFolderPhysicalIdentity(41, 42, 43),
                    target_path);
            SkinManagedFolderMutationJournalTest.writeVersionFixture(
                storage,
                legacyCompatible,
                version);
            SkinManagedFolderMutationJournalLoadResult loaded = durableStore.Load();
            Assert.That(loaded.Status, Is.EqualTo(SkinManagedFolderMutationJournalLoadStatus.Loaded));
            Assert.That(loaded.Journal, Is.Not.Null);
            var store = new MemoryMutationJournalStore(loaded.Journal!);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: !externalRegistryPresent);

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler,
                    authority)
                .Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(externalRegistryPresent
                    ? SkinManagedFolderMutationRecoveryStatus.Ambiguous
                    : SkinManagedFolderMutationRecoveryStatus.RecoveredRollback));
                Assert.That(handler.InspectCalls, Is.EqualTo(externalRegistryPresent ? 0 : 1));
                Assert.That(store.DeleteCalls, Is.EqualTo(externalRegistryPresent ? 0 : 1));
                Assert.That(store.Writes.All(write => write.Version == version), Is.True);
            });
        }

        [Test]
        public void TestC1SupportInspectionIsReadOnlyExactAndRequiresHeldHandler()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var authority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: true);
            var recovery = new SkinManagedFolderMutationRecovery(
                store,
                coordinator,
                handler,
                authority);

            FolderSkinJournalSupportSnapshot snapshot = recovery.InspectSupportSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CanRetry, Is.True);
                Assert.That(snapshot.DiagnosticBundle, Does.Not.Contain(journal.OperationId.ToString()));
                Assert.That(snapshot.DiagnosticBundle, Does.Not.Contain(journal.SourceManagedRelativePath));
                Assert.That(handler.InspectCalls, Is.EqualTo(1));
                Assert.That(handler.ForwardCalls + handler.RollbackCalls, Is.Zero);
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(authority.ObservedMutationLease, Is.True);
                Assert.That(authority.ActiveSessions, Is.Zero);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });

            var missingHandlerAuthority = new FakeMutationRecoveryAuthority(
                coordinator,
                registryIsEmpty: true);
            FolderSkinJournalSupportSnapshot withoutHandler =
                new SkinManagedFolderMutationRecovery(
                    store,
                    coordinator,
                    handler: null,
                    recoveryAuthority: missingHandlerAuthority)
                .InspectSupportSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(withoutHandler.CanRetry, Is.False);
                Assert.That(missingHandlerAuthority.OpenCalls, Is.Zero);
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
            });
        }

        [Test]
        public void TestSupportInspectionMissingIsReadOnlyAndNotRetryable()
        {
            var store = new MemoryMutationJournalStore(
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Missing));
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator);

            FolderSkinJournalSupportSnapshot snapshot = recovery.InspectSupportSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CanRetry, Is.False);
                Assert.That(snapshot.Status, Is.Not.Empty);
                Assert.That(snapshot.Reason, Is.Not.Empty);
                Assert.That(snapshot.DiagnosticBundle, Does.Contain("state=missing"));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        [Test]
        public void TestSupportInspectionOffersRetryOnlyForUniqueActionAndDoesNotExecuteIt()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var coordinator = new SkinManagedFolderOperationCoordinator();
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.RollBack);
            var recovery = new SkinManagedFolderMutationRecovery(store, coordinator, handler);

            FolderSkinJournalSupportSnapshot snapshot = recovery.InspectSupportSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CanRetry, Is.True);
                Assert.That(handler.InspectCalls, Is.EqualTo(1));
                Assert.That(handler.ForwardCalls, Is.Zero);
                Assert.That(handler.RollbackCalls, Is.Zero);
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsMutationBlocked, Is.False);
            });
        }

        [Test]
        public void TestSupportInspectionAmbiguousIsNotRetryableAndBundleIsRedacted()
        {
            SkinManagedFolderMutationJournal journal = createPreparedDelete();
            var store = new MemoryMutationJournalStore(journal);
            var handler = new RecordingRecoveryHandler(
                SkinManagedFolderMutationRecoveryDecision.Ambiguous);
            var recovery = new SkinManagedFolderMutationRecovery(
                store,
                new SkinManagedFolderOperationCoordinator(),
                handler);

            FolderSkinJournalSupportSnapshot snapshot = recovery.InspectSupportSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CanRetry, Is.False);
                Assert.That(snapshot.DiagnosticBundle, Does.Not.Contain(journal.OperationId.ToString()));
                Assert.That(snapshot.DiagnosticBundle, Does.Not.Contain(journal.RecordId!.Value.ToString()));
                Assert.That(snapshot.DiagnosticBundle, Does.Not.Contain(journal.SourceManagedRelativePath));
                Assert.That(snapshot.DiagnosticBundle, Does.Not.Contain(journal.ManagedRootIdentity.VolumeSerialNumber.ToString()));
                Assert.That(handler.ForwardCalls + handler.RollbackCalls, Is.Zero);
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
            });
        }

        [Test]
        public void TestSupportInspectionTerminalOffersOnlyCanonicalCleanupRetry()
        {
            SkinManagedFolderMutationJournal terminal = createPreparedDelete()
                                                          .WithFilesystemApplied()
                                                          .WithRealmApplied()
                                                          .WithCommitted();
            var store = new MemoryMutationJournalStore(terminal);
            var recovery = new SkinManagedFolderMutationRecovery(
                store,
                new SkinManagedFolderOperationCoordinator());

            FolderSkinJournalSupportSnapshot snapshot = recovery.InspectSupportSnapshot();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CanRetry, Is.True);
                Assert.That(snapshot.DiagnosticBundle, Does.Contain("state=terminal"));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
            });
        }

        private static SkinManagedFolderMutationJournal createPreparedDelete(
            SkinExternalRegistryJournalBinding? externalRegistry = null)
        {
            const string fingerprint = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

            return SkinManagedFolderMutationJournal.CreatePreparedDelete(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    new SkinManagedFolderPhysicalIdentity(41, 401, 402),
                    source_path,
                    new SkinManagedFolderPhysicalIdentity(41, 42, 43),
                    fingerprint,
                    SkinManagedFolderDeleteManifest.Create(new[] { fingerprint }),
                    externalRegistry)
                .WithDeleteFallbackDisposition(
                    SkinManagedFolderDeleteFallbackDisposition.NotRequired);
        }

        private static Guid addRecord(RealmAccess realm, string path)
        {
            var record = new SkinInfo("Recovery record", "OMS tests", SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                Hash = "recovery-revision",
                FilesystemStoragePath = path,
                FilesystemStorageAuthorityOwner = SkinManagedFolderScanner.AUTHORITY_OWNER,
            };
            realm.Write(r => r.Add(record));
            return record.ID;
        }

        private sealed class StaticDiscoverySource : ISkinManagedFolderDiscoverySource
        {
            private readonly SkinManagedFolderDiscoverySnapshot snapshot;

            public StaticDiscoverySource(SkinManagedFolderDiscoverySnapshot snapshot)
            {
                this.snapshot = snapshot;
            }

            public SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default)
                => snapshot;
        }
    }

    internal sealed class MemoryMutationJournalStore : ISkinManagedFolderMutationJournalStore
    {
        private readonly IList<string>? events;

        public SkinManagedFolderMutationJournalLoadResult Current { get; private set; }

        public List<SkinManagedFolderMutationJournal> Writes { get; } = new List<SkinManagedFolderMutationJournal>();

        public int DeleteCalls { get; private set; }

        public bool ThrowOnWrite { get; init; }

        public SkinManagedFolderMutationPhase? ThrowOnceOnWritePhase { get; set; }

        public bool ThrowOnDelete { get; init; }

        public int DeleteNoOpCallsRemaining { get; set; }

        public Action<SkinManagedFolderMutationJournal>? AfterWrite { get; set; }

        public Action? BeforeDelete { get; set; }

        public Action? AfterDelete { get; set; }

        public MemoryMutationJournalStore(
            SkinManagedFolderMutationJournal journal,
            IList<string>? events = null)
            : this(
                new SkinManagedFolderMutationJournalLoadResult(
                    SkinManagedFolderMutationJournalLoadStatus.Loaded,
                    journal),
                events)
        {
        }

        public MemoryMutationJournalStore(
            SkinManagedFolderMutationJournalLoadResult current,
            IList<string>? events = null)
        {
            Current = current;
            this.events = events;
        }

        public SkinManagedFolderMutationJournalLoadResult Load()
        {
            events?.Add("load");
            return Current;
        }

        public void Write(SkinManagedFolderMutationJournal journal)
        {
            events?.Add($"write:{journal.Phase}");

            if (ThrowOnWrite
                || ThrowOnceOnWritePhase == journal.Phase)
            {
                ThrowOnceOnWritePhase = null;
                throw new IOException("Injected journal write fault.");
            }

            Writes.Add(journal);
            Current = new SkinManagedFolderMutationJournalLoadResult(
                SkinManagedFolderMutationJournalLoadStatus.Loaded,
                journal);
            AfterWrite?.Invoke(journal);
        }

        public void Delete(SkinManagedFolderMutationJournal expectedJournal)
        {
            if (expectedJournal.Phase is not (SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack))
                throw new InvalidOperationException();

            if (!Current.IsLoaded || !Current.Journal!.IsExactSameJournal(expectedJournal))
                throw new InvalidOperationException();

            events?.Add("delete");
            DeleteCalls++;
            BeforeDelete?.Invoke();

            if (ThrowOnDelete)
                throw new IOException("Injected journal delete fault.");

            if (DeleteNoOpCallsRemaining > 0)
            {
                DeleteNoOpCallsRemaining--;
                return;
            }

            Current = new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Missing);
            AfterDelete?.Invoke();
        }

        public void SetCurrent(SkinManagedFolderMutationJournalLoadResult current)
        {
            Current = current;
        }
    }

    internal sealed class RecordingRecoveryHandler
        : ISkinManagedFolderMutationRecoveryHandler,
          ISkinManagedFolderMutationHeldRecoveryHandler
    {
        private readonly SkinManagedFolderMutationRecoveryDecision decision;
        private readonly IList<string>? events;
        private readonly SkinManagedFolderPhysicalIdentity? targetIdentity;
        private readonly SkinManagedFolderPhysicalIdentity? inspectionRootIdentity;
        private readonly SkinManagedFolderPhysicalIdentity? actionRootIdentity;
        private bool forwardApplied;

        public int InspectCalls { get; private set; }

        public int ForwardCalls { get; private set; }

        public int RollbackCalls { get; private set; }

        public bool ThrowOnInspect { get; init; }

        public bool ThrowOnForward { get; init; }

        public bool ThrowOnRollback { get; init; }

        public bool ActionSucceeds { get; init; } = true;

        public RecordingRecoveryHandler(
            SkinManagedFolderMutationRecoveryDecision decision,
            IList<string>? events = null,
            SkinManagedFolderPhysicalIdentity? targetIdentity = null,
            SkinManagedFolderPhysicalIdentity? inspectionRootIdentity = null,
            SkinManagedFolderPhysicalIdentity? actionRootIdentity = null)
        {
            this.decision = decision;
            this.events = events;
            this.targetIdentity = targetIdentity;
            this.inspectionRootIdentity = inspectionRootIdentity;
            this.actionRootIdentity = actionRootIdentity;
        }

        public SkinManagedFolderMutationRecoveryInspection Inspect(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            events?.Add("inspect");
            InspectCalls++;

            if (ThrowOnInspect)
                throw new IOException("Injected recovery inspection fault.");

            return new SkinManagedFolderMutationRecoveryInspection(
                decision == SkinManagedFolderMutationRecoveryDecision.RollForward
                && forwardApplied
                    ? SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted
                    : decision,
                inspectionRootIdentity ?? journal.ManagedRootIdentity,
                targetIdentity,
                journal.NewRecordPublicationFingerprint);
        }

        public bool CanHandle(SkinManagedFolderMutationKind kind)
            => kind is SkinManagedFolderMutationKind.Rename
                or SkinManagedFolderMutationKind.StagedImport
                or SkinManagedFolderMutationKind.Delete;

        public SkinManagedFolderMutationRecoveryInspection InspectHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => Inspect(journal, cancellationToken);

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            events?.Add("forward");
            ForwardCalls++;

            if (ThrowOnForward)
                throw new IOException("Injected roll-forward fault.");

            forwardApplied = ActionSucceeds;

            return new SkinManagedFolderMutationRecoveryActionResult(
                ActionSucceeds,
                actionRootIdentity ?? journal.ManagedRootIdentity,
                targetIdentity,
                journal.NewRecordPublicationFingerprint);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => TryRollForward(journal, cancellationToken);

        public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            events?.Add("rollback");
            RollbackCalls++;

            if (ThrowOnRollback)
                throw new IOException("Injected rollback fault.");

            return new SkinManagedFolderMutationRecoveryActionResult(
                ActionSucceeds,
                actionRootIdentity ?? journal.ManagedRootIdentity);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => TryRollBack(journal, cancellationToken);
    }

    internal sealed class FakeMutationRecoveryAuthority
        : ISkinManagedFolderMutationRecoveryAuthority
    {
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly SkinExternalRegistryJournalBinding binding;

        private readonly bool allowTerminalWithoutHandler;

        public bool RegistryIsEmpty { get; }

        public bool Invalidated { get; private set; }

        public int OpenCalls { get; private set; }

        public int ActiveSessions { get; private set; }

        public int DisposedSessions { get; private set; }

        public int ValidateCalls { get; private set; }

        public bool ObservedMutationLease { get; private set; }

        public int? InvalidateOnValidateCall { get; init; }

        public FakeMutationRecoveryAuthority(
            SkinManagedFolderOperationCoordinator coordinator,
            bool registryIsEmpty,
            SkinExternalRegistryJournalBinding? binding = null,
            bool allowTerminalWithoutHandler = false)
        {
            this.coordinator = coordinator;
            RegistryIsEmpty = registryIsEmpty;
            this.binding = binding ?? SkinExternalRegistryJournalBinding.Empty;
            this.allowTerminalWithoutHandler = allowTerminalWithoutHandler;
        }

        public ISkinManagedFolderMutationRecoveryAuthoritySession? TryOpen(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCalls++;
            ObservedMutationLease = coordinatorLease?.IsMutationReservationHeldBy(coordinator) == true;

            if (!ObservedMutationLease)
                return null;

            ActiveSessions++;
            return new Session(this);
        }

        public void Invalidate() => Invalidated = true;

        private sealed class Session : ISkinManagedFolderMutationRecoveryAuthoritySession
        {
            private readonly FakeMutationRecoveryAuthority owner;
            private bool disposed;

            public ISkinManagedFolderMutationNativeSession NativeSession
                => throw new InvalidOperationException("The recording handler must not consume native authority.");

            public Session(FakeMutationRecoveryAuthority owner)
            {
                this.owner = owner;
            }

            public bool IsExactFor(SkinManagedFolderMutationJournal journal)
            {
                if (disposed || journal == null || !journal.IsValid())
                    return false;

                if (owner.allowTerminalWithoutHandler
                    && journal.Phase is SkinManagedFolderMutationPhase.Committed
                        or SkinManagedFolderMutationPhase.RolledBack)
                {
                    return true;
                }

                if (journal.Version is SkinManagedFolderMutationJournal.LEGACY_VERSION
                    or SkinManagedFolderMutationJournal.PRE_C1_VERSION)
                {
                    return owner.RegistryIsEmpty;
                }

                SkinExternalCollisionDisposition expectedDisposition = owner.RegistryIsEmpty
                    ? SkinExternalCollisionDisposition.NoRegisteredExternalFolders
                    : SkinExternalCollisionDisposition.ExactRegisteredExternalSet;

                return journal.Version == SkinManagedFolderMutationJournal.CURRENT_VERSION
                       && journal.ExternalRegistryGeneration == owner.binding.Generation
                       && string.Equals(
                           journal.ExternalRegistryDigest,
                           owner.binding.Digest,
                           StringComparison.Ordinal)
                       && journal.ExternalCollisionDisposition == expectedDisposition
                       && owner.binding.Disposition == expectedDisposition;
            }

            public bool Validate(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                owner.ValidateCalls++;

                if (owner.InvalidateOnValidateCall == owner.ValidateCalls)
                    owner.Invalidate();

                return !disposed && !owner.Invalidated;
            }

            public bool ExactlyMatchesRealmDeclarations(IEnumerable<SkinInfo> records)
            {
                ArgumentNullException.ThrowIfNull(records);
                return !disposed && !owner.Invalidated;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                owner.ActiveSessions--;
                owner.DisposedSessions++;
            }

            public override string ToString() => nameof(Session);
        }
    }
}
