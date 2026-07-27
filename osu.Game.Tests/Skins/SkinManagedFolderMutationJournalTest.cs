// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
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
                source_identity);
            SkinManagedFolderMutationJournal stagedImport = SkinManagedFolderMutationJournal.CreatePreparedStagedImport(
                importOperationId,
                root_identity,
                target_path,
                staged_identity,
                staged_root_identity);

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
                Assert.That(delete.TargetManagedRelativePath, Is.Null);
                Assert.That(delete.GetAffectedManagedRelativePaths(), Is.EqualTo(new[] { source_path }));

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
                    stagedImport.NewRecordPublicationPlanVersion,
                    Is.EqualTo(SkinManagedFolderMutationJournal.NEW_RECORD_PUBLICATION_PLAN_VERSION));
                Assert.That(stagedImport.GetAffectedManagedRelativePaths(), Is.EqualTo(new[] { target_path }));
            });
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
                staged_root_identity);

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
                    default),
                Throws.ArgumentException);
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

            File.WriteAllBytes(getJournalPath(storage), new byte[128 * 1024 + 1]);
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
                    "\"version\":1",
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
            SkinManagedFolderMutationJournal terminal =
                prepared.WithRecoveryTerminalPhase(SkinManagedFolderMutationPhase.Committed);
            store.Write(prepared);
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
                Assert.That(() => filesystem.WithFilesystemApplied(), Throws.InvalidOperationException);
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

            Assert.Multiple(() =>
            {
                Assert.That(first.Status, Is.EqualTo(expectedStatus));
                Assert.That(first.IsResolved, Is.True);
                Assert.That(events, Is.EqualTo(new[]
                {
                    "load",
                    "inspect",
                    expectedActionEvent,
                    $"write:{expectedTerminalPhase}",
                    "delete",
                    "load",
                }));
                Assert.That(store.Writes, Has.Count.EqualTo(1));
                Assert.That(store.Writes[0].Phase, Is.EqualTo(expectedTerminalPhase));
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
                Assert.That(handler.InspectCalls, Is.EqualTo(1));
                Assert.That(handler.ForwardCalls + handler.RollbackCalls, Is.EqualTo(1));
                Assert.That(store.Writes, Has.Count.EqualTo(1));
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
                Assert.That(store.Writes, Has.Count.EqualTo(1));
                Assert.That(store.Writes[0].Phase, Is.EqualTo(SkinManagedFolderMutationPhase.Committed));
                Assert.That(store.Writes[0].TargetIdentity, Is.EqualTo(source_identity));
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
            SkinManagedFolderMutationJournal terminal = createPreparedDelete().WithRecoveryTerminalPhase(terminalPhase);
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

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(handler.ForwardCalls, Is.EqualTo(1));
                Assert.That(store.Current.Journal, Is.SameAs(journal));
                Assert.That(store.Writes, Is.Empty);
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
                Assert.That(store.Current.Journal, Is.SameAs(journal));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
                Assert.That(coordinator.IsPathFrozen("chartskin/unrelated"), Is.True);
            });
        }

        [Test]
        public void TestRecoveryDeleteFaultRetainsTerminalJournalAndFreezesNamespace()
        {
            SkinManagedFolderMutationJournal terminal =
                createPreparedDelete().WithRecoveryTerminalPhase(SkinManagedFolderMutationPhase.Committed);
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

            SkinManagedFolderMutationRecoveryResult result =
                new SkinManagedFolderMutationRecovery(store, coordinator, handler).Recover();

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(SkinManagedFolderMutationRecoveryStatus.Ambiguous));
                Assert.That(result.IsResolved, Is.False);
                Assert.That(handler.ForwardCalls, Is.EqualTo(replacementDuringAction ? 1 : 0));
                Assert.That(store.Current.Journal, Is.SameAs(journal));
                Assert.That(store.Writes, Is.Empty);
                Assert.That(store.DeleteCalls, Is.Zero);
                Assert.That(coordinator.IsPathFrozen(source_path), Is.True);
            });
        }

        [Test]
        public void TestTerminalDeleteIsConfirmedAndRetriedIfExactJournalRemains()
        {
            SkinManagedFolderMutationJournal terminal =
                createPreparedDelete().WithRecoveryTerminalPhase(SkinManagedFolderMutationPhase.Committed);
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
                Assert.That(actual.GetAffectedManagedRelativePaths(), Is.EqualTo(expected.GetAffectedManagedRelativePaths()));
                Assert.That(actual.IsValid(), Is.True);
            });
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

        private static SkinManagedFolderMutationJournal createPreparedDelete()
            => SkinManagedFolderMutationJournal.CreatePreparedDelete(
                Guid.NewGuid(),
                Guid.NewGuid(),
                root_identity,
                source_path,
                source_identity);

        private static TemporaryNativeStorage createStorage([System.Runtime.CompilerServices.CallerMemberName] string testName = "")
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

        public bool ThrowOnDelete { get; init; }

        public int DeleteNoOpCallsRemaining { get; set; }

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

            if (ThrowOnWrite)
                throw new IOException("Injected journal write fault.");

            Writes.Add(journal);
            Current = new SkinManagedFolderMutationJournalLoadResult(
                SkinManagedFolderMutationJournalLoadStatus.Loaded,
                journal);
        }

        public void Delete(SkinManagedFolderMutationJournal expectedJournal)
        {
            if (expectedJournal.Phase is not (SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack))
                throw new InvalidOperationException();

            if (!Current.IsLoaded || !Current.Journal!.IsExactSameJournal(expectedJournal))
                throw new InvalidOperationException();

            events?.Add("delete");
            DeleteCalls++;

            if (ThrowOnDelete)
                throw new IOException("Injected journal delete fault.");

            if (DeleteNoOpCallsRemaining > 0)
            {
                DeleteNoOpCallsRemaining--;
                return;
            }

            Current = new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Missing);
        }

        public void SetCurrent(SkinManagedFolderMutationJournalLoadResult current)
        {
            Current = current;
        }
    }

    internal sealed class RecordingRecoveryHandler : ISkinManagedFolderMutationRecoveryHandler
    {
        private readonly SkinManagedFolderMutationRecoveryDecision decision;
        private readonly IList<string>? events;
        private readonly SkinManagedFolderPhysicalIdentity? targetIdentity;
        private readonly SkinManagedFolderPhysicalIdentity? inspectionRootIdentity;
        private readonly SkinManagedFolderPhysicalIdentity? actionRootIdentity;

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
                decision,
                inspectionRootIdentity ?? journal.ManagedRootIdentity,
                targetIdentity);
        }

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            events?.Add("forward");
            ForwardCalls++;

            if (ThrowOnForward)
                throw new IOException("Injected roll-forward fault.");

            return new SkinManagedFolderMutationRecoveryActionResult(
                ActionSucceeds,
                actionRootIdentity ?? journal.ManagedRootIdentity,
                targetIdentity);
        }

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
    }
}
