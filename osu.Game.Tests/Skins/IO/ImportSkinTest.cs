// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Skinning;
using osu.Game.Skinning.IO;
using osu.Game.Tests.Resources;
using SharpCompress.Archives.Zip;

namespace osu.Game.Tests.Skins.IO
{
    public class ImportSkinTest : ImportTest
    {
        #region Testing filename metadata inclusion

        [TestCase("Archives/modified-classic-20220723.osk")]
        [TestCase("Archives/modified-default-20230117.osk")]
        [TestCase("Archives/modified-argon-20231106.osk")]
        public Task TestImportModifiedSkinHasResources(string archive) => runSkinTest(async osu =>
        {
            using (var stream = TestResources.OpenResource(archive))
            {
                var imported = await loadSkinIntoOsu(osu, new ImportTask(stream, "skin.osk"));

                // When the import filename doesn't match, it should be appended (and update the skin.ini).

                var skinManager = osu.Dependencies.Get<SkinManager>();

                skinManager.CurrentSkinInfo.Value = imported;

                Assert.That(skinManager.CurrentSkin.Value.LayoutInfos.Count, Is.EqualTo(2));
            }
        });

        [Test]
        public Task TestSingleImportDifferentFilename() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner"), "skin.osk"));

            // When the import filename doesn't match, it should be appended (and update the skin.ini).
            assertCorrectMetadata(import1, "test skin [skin]", "skinner", 1.0m, osu);
        });

        [Test]
        public Task TestSingleImportWeirdIniFileCase() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner", iniFilename: "Skin.InI"), "skin.osk"));

            // When the import filename doesn't match, it should be appended (and update the skin.ini).
            assertCorrectMetadata(import1, "test skin [skin]", "skinner", 1.0m, osu);
        });

        [Test]
        public Task TestSingleImportMissingSectionHeader() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner", includeSectionHeader: false), "skin.osk"));

            // When the import filename doesn't match, it should be appended (and update the skin.ini).
            assertCorrectMetadata(import1, "test skin [skin]", "skinner", 1.0m, osu);
        });

        [Test]
        public Task TestSingleImportMatchingFilename() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner"), "test skin.osk"));

            // When the import filename matches it shouldn't be appended.
            assertCorrectMetadata(import1, "test skin", "skinner", 1.0m, osu);
        });

        [Test]
        public Task TestSingleImportNoIniFile() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithNonIniFile(), "test skin.osk"));

            // When the import filename matches it shouldn't be appended.
            assertCorrectMetadata(import1, "test skin", "Unknown", SkinConfiguration.LATEST_VERSION, osu);
        });

        [Test]
        public Task TestEmptyImportImportsWithFilename() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createEmptyOsk(), "test skin.osk"));

            // When the import filename matches it shouldn't be appended.
            assertCorrectMetadata(import1, "test skin", "Unknown", SkinConfiguration.LATEST_VERSION, osu);
        });

        #endregion

        #region Cases where imports should match existing

        [Test]
        public Task TestImportTwiceWithSameMetadataAndFilename([Values] bool batchImport) => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner"), "skin.osk"), batchImport);
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner"), "skin.osk"), batchImport);

            assertImportedOnce(import1, import2);
        });

        [Test]
        public Task TestImportTwiceWithNoMetadataSameDownloadFilename([Values] bool batchImport) => runSkinTest(async osu =>
        {
            // if a user downloads two skins that do have skin.ini files but don't have any creator metadata in the skin.ini, they should both import separately just for safety.
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni(string.Empty, string.Empty), "download.osk"), batchImport);
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni(string.Empty, string.Empty), "download.osk"), batchImport);

            assertImportedOnce(import1, import2);
        });

        [Test]
        public Task TestImportUpperCasedOskArchive() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "name 1.OsK"));
            assertCorrectMetadata(import1, "name 1", "author 1", 1.0m, osu);

            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "name 1.oSK"));

            assertImportedOnce(import1, import2);
        });

        [Test]
        public Task TestImportExportedSkinFilename() => runSkinTest(async osu =>
        {
            MemoryStream exportStream = new MemoryStream();

            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "custom.osk"));
            assertCorrectMetadata(import1, "name 1 [custom]", "author 1", 1.0m, osu);

            await new LegacySkinExporter(osu.Dependencies.Get<Storage>()).ExportToStreamAsync(import1, exportStream);

            string exportFilename = import1.GetDisplayString();

            var import2 = await loadSkinIntoOsu(osu, new ImportTask(exportStream, $"{exportFilename}.osk"));
            assertCorrectMetadata(import2, "name 1 [custom]", "author 1", 1.0m, osu);

            assertImportedOnce(import1, import2);
        });

        [Test]
        public Task TestImportExportedNonAsciiSkinFilename() => runSkinTest(async osu =>
        {
            MemoryStream exportStream = new MemoryStream();

            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 『1』", "author 1"), "custom.osk"));
            assertCorrectMetadata(import1, "name 『1』 [custom]", "author 1", 1.0m, osu);

            await new LegacySkinExporter(osu.Dependencies.Get<Storage>()).ExportToStreamAsync(import1, exportStream);

            string exportFilename = import1.GetDisplayString().GetValidFilename();

            var import2 = await loadSkinIntoOsu(osu, new ImportTask(exportStream, $"{exportFilename}.osk"));
            assertCorrectMetadata(import2, "name 『1』 [custom]", "author 1", 1.0m, osu);
        });

        [Test]
        public Task TestSameMetadataNameSameFolderName([Values] bool batchImport) => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "my custom skin 1"), batchImport);
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "my custom skin 1"), batchImport);

            assertImportedOnce(import1, import2);
            assertCorrectMetadata(import1, "name 1 [my custom skin 1]", "author 1", 1.0m, osu);
        });

        [Test]
        public Task TestImportWithSubfolder() => runSkinTest(async osu =>
        {
            const string filename = "Archives/skin-with-subfolder-zip-entries.osk";
            var import = await loadSkinIntoOsu(osu, new ImportTask(TestResources.OpenResource(filename), filename));

            assertCorrectMetadata(import, $"Totally fully features skin [Real Skin with Real Features] [{filename[..^4]}]", "Unknown", 2.7m, osu);
            Assert.That(import.PerformRead(r => r.Files.Count), Is.EqualTo(3));
        });

        #endregion

        #region Cases where imports should be uniquely imported

        [Test]
        public Task TestImportTwiceWithSameMetadataButDifferentFilename() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner"), "skin.osk"));
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin", "skinner"), "skin2.osk"));

            assertImportedBoth(import1, import2);
        });

        [Test]
        public Task TestImportTwiceWithNoMetadataDifferentDownloadFilename() => runSkinTest(async osu =>
        {
            // if a user downloads two skins that do have skin.ini files but don't have any creator metadata in the skin.ini, they should both import separately just for safety.
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni(string.Empty, string.Empty), "download.osk"));
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni(string.Empty, string.Empty), "download2.osk"));

            assertImportedBoth(import1, import2);
        });

        [Test]
        public Task TestImportTwiceWithSameFilenameDifferentMetadata() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin v2", "skinner"), "skin.osk"));
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("test skin v2.1", "skinner"), "skin.osk"));

            assertImportedBoth(import1, import2);
            assertCorrectMetadata(import1, "test skin v2 [skin]", "skinner", 1.0m, osu);
            assertCorrectMetadata(import2, "test skin v2.1 [skin]", "skinner", 1.0m, osu);
        });

        [Test]
        public Task TestSameMetadataNameDifferentFolderName() => runSkinTest(async osu =>
        {
            var import1 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "my custom skin 1"));
            var import2 = await loadSkinIntoOsu(osu, new ImportTask(createOskWithIni("name 1", "author 1"), "my custom skin 2"));

            assertImportedBoth(import1, import2);
            assertCorrectMetadata(import1, "name 1 [my custom skin 1]", "author 1", 1.0m, osu);
            assertCorrectMetadata(import2, "name 1 [my custom skin 2]", "author 1", 1.0m, osu);
        });

        [Test]
        public Task TestExportThenImportDefaultSkin() => runSkinTest(async osu =>
        {
            var skinManager = osu.Dependencies.Get<SkinManager>();

            skinManager.EnsureMutableSkin();

            MemoryStream exportStream = new MemoryStream();

            Guid originalSkinId = skinManager.CurrentSkinInfo.Value.ID;

            await skinManager.CurrentSkinInfo.Value.PerformRead(async s =>
            {
                Assert.IsFalse(s.Protected);
                Assert.AreEqual(typeof(OmsSkin), s.CreateInstance(skinManager).GetType());

                await new LegacySkinExporter(osu.Dependencies.Get<Storage>()).ExportToStreamAsync(skinManager.CurrentSkinInfo.Value, exportStream);

                Assert.Greater(exportStream.Length, 0);
            });

            var imported = await skinManager.Import(new ImportTask(exportStream, "exported.osk"));

            imported.PerformRead(s =>
            {
                Assert.IsFalse(s.Protected);
                Assert.AreNotEqual(originalSkinId, s.ID);
                Assert.AreEqual(typeof(OmsSkin), s.CreateInstance(skinManager).GetType());
            });
        });

        [Test]
        public Task TestExportThenImportClassicSkin() => runSkinTest(async osu =>
        {
            var skinManager = osu.Dependencies.Get<SkinManager>();

            skinManager.CurrentSkinInfo.Value = skinManager.DefaultClassicSkin.SkinInfo;

            skinManager.EnsureMutableSkin();

            MemoryStream exportStream = new MemoryStream();

            Guid originalSkinId = skinManager.CurrentSkinInfo.Value.ID;

            await skinManager.CurrentSkinInfo.Value.PerformRead(async s =>
            {
                Assert.IsFalse(s.Protected);
                Assert.AreEqual(typeof(DefaultLegacySkin), s.CreateInstance(skinManager).GetType());

                await new LegacySkinExporter(osu.Dependencies.Get<Storage>()).ExportToStreamAsync(skinManager.CurrentSkinInfo.Value, exportStream);

                Assert.Greater(exportStream.Length, 0);
            });

            var imported = await skinManager.Import(new ImportTask(exportStream, "exported.osk"));

            imported.PerformRead(s =>
            {
                Assert.IsFalse(s.Protected);
                Assert.AreNotEqual(originalSkinId, s.ID);
                Assert.AreEqual(typeof(DefaultLegacySkin), s.CreateInstance(skinManager).GetType());
            });
        });

        #endregion

        [Test]
        public Task TestFailedArchiveImportRemovesOnlyNewFileStoreReceipts() => runSkinTest(async osu =>
        {
            var skinManager = osu.Dependencies.Get<SkinManager>();
            var realmAccess = osu.Dependencies.Get<RealmAccess>();
            var fileStore = new RealmFileStore(realmAccess, osu.Dependencies.Get<Storage>());
            byte[] newContent = Guid.NewGuid().ToByteArray();
            byte[] sharedContent = Guid.NewGuid().ToByteArray();

            string newHash = new MemoryStream(newContent).ComputeSHA2Hash();
            string sharedHash = new MemoryStream(sharedContent).ComputeSHA2Hash();
            string newPath = new RealmFile { Hash = newHash }.GetStoragePath();
            string sharedPath = new RealmFile { Hash = sharedHash }.GetStoragePath();

            Assert.That(realmAccess.Run(realm => realm.Find<RealmFile>(newHash)), Is.Null);
            Assert.That(fileStore.Storage.Exists(newPath), Is.False);

            realmAccess.Write(realm => fileStore.Add(new MemoryStream(sharedContent), realm));
            Assert.That(realmAccess.Run(realm => realm.Find<RealmFile>(sharedHash)), Is.Not.Null);
            Assert.That(fileStore.Storage.Exists(sharedPath), Is.True);

            int skinCountBefore = realmAccess.Run(realm => realm.All<SkinInfo>().Count());
            using MemoryStream archive = SkinArchiveReaderTest.BuildZip(
                new SkinArchiveReaderTest.ZipEntry("new-resource.bin", newContent),
                new SkinArchiveReaderTest.ZipEntry("shared-resource.bin", sharedContent),
                new SkinArchiveReaderTest.ZipEntry("broken-resource.bin", new byte[] { 1, 2, 3 }) { DeclaredCrc = 0x12345678 });

            try
            {
                await skinManager.Import(new ImportTask(archive, "failed.osk"));
                Assert.Fail("CRC-invalid archive was accepted.");
            }
            catch (SkinArchiveImportException exception)
            {
                Assert.That(exception.Reason, Is.EqualTo(SkinArchiveRejectionReason.CrcMismatch));
            }

            Assert.That(realmAccess.Run(realm => realm.All<SkinInfo>().Count()), Is.EqualTo(skinCountBefore));
            Assert.That(realmAccess.Run(realm => realm.Find<RealmFile>(newHash)), Is.Null);
            Assert.That(fileStore.Storage.Exists(newPath), Is.False);
            Assert.That(realmAccess.Run(realm => realm.Find<RealmFile>(sharedHash)), Is.Not.Null);
            Assert.That(fileStore.Storage.Exists(sharedPath), Is.True);

            string validPath = Path.Combine(Path.GetTempPath(), $"oms-valid-{Guid.NewGuid():N}.osk");
            string invalidPath = Path.Combine(Path.GetTempPath(), $"oms-invalid-{Guid.NewGuid():N}.osk");

            try
            {
                using (MemoryStream valid = SkinArchiveReaderTest.BuildZip(
                           new SkinArchiveReaderTest.ZipEntry("skin.ini", generateSkinIniBytes("safe source", "OMS"))))
                    File.WriteAllBytes(validPath, valid.ToArray());

                using (MemoryStream invalid = SkinArchiveReaderTest.BuildZip(
                           new SkinArchiveReaderTest.ZipEntry("resource.bin", new byte[] { 1, 2, 3 }) { DeclaredCrc = 0x12345678 }))
                    File.WriteAllBytes(invalidPath, invalid.ToArray());

                var validImported = await skinManager.Import(new ImportTask(validPath));
                Assert.That(validImported, Is.Not.Null);
                Assert.That(File.Exists(validPath), Is.False);

                try
                {
                    await skinManager.Import(new ImportTask(invalidPath));
                    Assert.Fail("CRC-invalid source archive was accepted.");
                }
                catch (SkinArchiveImportException exception)
                {
                    Assert.That(exception.Reason, Is.EqualTo(SkinArchiveRejectionReason.CrcMismatch));
                }

                Assert.That(File.Exists(invalidPath), Is.True);
            }
            finally
            {
                File.Delete(validPath);
                File.Delete(invalidPath);
            }

            const string rawType = "System.String, System.Private.CoreLib";
            using MemoryStream unknownTypeArchive = SkinArchiveReaderTest.BuildZip(
                new SkinArchiveReaderTest.ZipEntry("skininfo.json", Encoding.UTF8.GetBytes($"{{\"InstantiationInfo\":\"{rawType}\"}}")),
                new SkinArchiveReaderTest.ZipEntry("skin.ini", generateSkinIniBytes("safe type", "OMS")));

            var canonicalImported = await skinManager.Import(new ImportTask(unknownTypeArchive, "safe-type.osk"));
            canonicalImported.PerformRead(info =>
            {
                Assert.That(info.InstantiationInfo, Is.Not.EqualTo(rawType));
                Assert.That(() => info.CreateInstance(skinManager), Throws.Nothing);
            });
        }, "osk-archive-safety");

        [TestCase(false)]
        [TestCase(true)]
        public Task TestFailureAfterFileRecordCommitRollsBackExactly(bool cancel) => runSkinTest(async osu =>
        {
            var skinManager = osu.Dependencies.Get<SkinManager>();
            var realmAccess = osu.Dependencies.Get<RealmAccess>();
            var fileStore = new RealmFileStore(realmAccess, osu.Dependencies.Get<Storage>());
            byte[] newContent = Guid.NewGuid().ToByteArray();
            byte[] sharedContent = Guid.NewGuid().ToByteArray();
            string newHash = new MemoryStream(newContent).ComputeSHA2Hash();
            string sharedHash = new MemoryStream(sharedContent).ComputeSHA2Hash();
            string newPath = new RealmFile { Hash = newHash }.GetStoragePath();
            string sharedPath = new RealmFile { Hash = sharedHash }.GetStoragePath();

            realmAccess.Write(realm =>
            {
                RealmFile shared = fileStore.Add(new MemoryStream(sharedContent), realm);
                var beatmapSet = new BeatmapSetInfo();
                beatmapSet.Files.Add(new RealmNamedFileUsage(shared, "shared.resource"));
                realm.Add(beatmapSet);
            });

            int skinCountBefore = realmAccess.Run(realm => realm.All<SkinInfo>().Count());
            using MemoryStream archive = SkinArchiveReaderTest.BuildZip(
                new SkinArchiveReaderTest.ZipEntry("new-resource.bin", newContent),
                new SkinArchiveReaderTest.ZipEntry("shared-resource.bin", sharedContent),
                new SkinArchiveReaderTest.ZipEntry("skin.ini", generateSkinIniBytes("fault after file records", "OMS")));

            using var cancellation = new CancellationTokenSource();
            skinManager.SkinImportAfterFileRecordsCommittedTestHook = token =>
            {
                if (cancel)
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }

                throw new DeterministicSkinImportFaultException();
            };

            try
            {
                if (cancel)
                {
                    Assert.ThrowsAsync<OperationCanceledException>(async () => await skinManager.Import(
                        new ImportTask(archive, "cancel-after-file-records.osk"), cancellationToken: cancellation.Token));
                }
                else
                {
                    Assert.ThrowsAsync<DeterministicSkinImportFaultException>(async () =>
                        await skinManager.Import(new ImportTask(archive, "fault-after-file-records.osk")));
                }
            }
            finally
            {
                skinManager.SkinImportAfterFileRecordsCommittedTestHook = null;
            }

            Assert.That(realmAccess.Run(realm => realm.All<SkinInfo>().Count()), Is.EqualTo(skinCountBefore));
            Assert.That(realmAccess.Run(realm => realm.Find<RealmFile>(newHash)), Is.Null);
            Assert.That(fileStore.Storage.Exists(newPath), Is.False);
            Assert.That(realmAccess.Run(realm => realm.Find<RealmFile>(sharedHash)?.Usages.Count()), Is.EqualTo(1));
            Assert.That(fileStore.Storage.Exists(sharedPath), Is.True);

            using MemoryStream valid = SkinArchiveReaderTest.BuildZip(
                new SkinArchiveReaderTest.ZipEntry("skin.ini", generateSkinIniBytes("queue remains usable", "OMS")));
            var imported = await skinManager.Import(new ImportTask(valid, "valid-after-fault.osk"));
            Assert.That(imported, Is.Not.Null);
        });

        [TestCase(false)]
        [TestCase(true)]
        public Task TestFailureAfterSkinMetadataRewriteRestoresExactStoreBaseline(bool cancel) => runSkinTest(async osu =>
        {
            var skinManager = osu.Dependencies.Get<SkinManager>();
            var realmAccess = osu.Dependencies.Get<RealmAccess>();
            var rootStorage = osu.Dependencies.Get<Storage>();
            var fileStore = new RealmFileStore(realmAccess, rootStorage);
            byte[] sharedContent = Guid.NewGuid().ToByteArray();

            realmAccess.Write(realm =>
            {
                RealmFile shared = fileStore.Add(new MemoryStream(sharedContent), realm);
                var beatmapSet = new BeatmapSetInfo();
                beatmapSet.Files.Add(new RealmNamedFileUsage(shared, "shared.resource"));
                realm.Add(beatmapSet);
            });

            StoreInventory baseline = captureStoreInventory(realmAccess, fileStore);
            using MemoryStream archive = SkinArchiveReaderTest.BuildZip(
                new SkinArchiveReaderTest.ZipEntry("shared-resource.bin", sharedContent),
                new SkinArchiveReaderTest.ZipEntry("new-resource.bin", Guid.NewGuid().ToByteArray()),
                new SkinArchiveReaderTest.ZipEntry("skin.ini", generateSkinIniBytes("metadata source", "OMS")));
            using var cancellation = new CancellationTokenSource();
            skinManager.SkinImportAfterPopulateTestHook = token =>
            {
                if (cancel)
                {
                    cancellation.Cancel();
                    token.ThrowIfCancellationRequested();
                }

                throw new DeterministicSkinImportFaultException();
            };

            try
            {
                if (cancel)
                {
                    Assert.ThrowsAsync<OperationCanceledException>(async () => await skinManager.Import(
                        new ImportTask(archive, "cancel-after-metadata.osk"), cancellationToken: cancellation.Token));
                }
                else
                {
                    Assert.ThrowsAsync<DeterministicSkinImportFaultException>(async () =>
                        await skinManager.Import(new ImportTask(archive, "fault-after-metadata.osk")));
                }
            }
            finally
            {
                skinManager.SkinImportAfterPopulateTestHook = null;
            }

            Assert.That(captureStoreInventory(realmAccess, fileStore), Is.EqualTo(baseline));

            using MemoryStream valid = SkinArchiveReaderTest.BuildZip(
                new SkinArchiveReaderTest.ZipEntry("skin.ini", generateSkinIniBytes("valid after metadata fault", "OMS")));
            Assert.That(await skinManager.Import(new ImportTask(valid, "valid-after-metadata-fault.osk")), Is.Not.Null);
        });

        private static StoreInventory captureStoreInventory(RealmAccess realmAccess, RealmFileStore fileStore)
        {
            string[] realmFiles = realmAccess.Run(realm => realm.All<RealmFile>()
                .AsEnumerable()
                .Select(file => $"{file.Hash}:{file.Usages.Count()}:{file.BacklinksCount}")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
            string[] skinIds = realmAccess.Run(realm => realm.All<SkinInfo>()
                .AsEnumerable()
                .Select(skin => skin.ID.ToString("N"))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
            string filesRoot = fileStore.Storage.GetFullPath(string.Empty);
            string[] blobs = Directory.Exists(filesRoot)
                ? Directory.EnumerateFiles(filesRoot, "*", SearchOption.AllDirectories)
                           .Select(path => Path.GetRelativePath(filesRoot, path).Replace('\\', '/'))
                           .OrderBy(value => value, StringComparer.Ordinal)
                           .ToArray()
                : Array.Empty<string>();

            return new StoreInventory(
                string.Join('|', realmFiles),
                string.Join('|', skinIds),
                string.Join('|', blobs));
        }

        private readonly record struct StoreInventory(
            string RealmFiles,
            string SkinIds,
            string Blobs);

        [Test]
        public async Task TestExternallyMountingWithSubDirectory()
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);

                    var zipStream = new MemoryStream();
                    using var zip = ZipArchive.Create();
                    zip.AddEntry("folder/test.png", new MemoryStream(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }));
                    zip.SaveTo(zipStream);

                    var import = await loadSkinIntoOsu(osu, new ImportTask(zipStream, "test skin.osk"));

                    var skinManager = osu.Dependencies.Get<SkinManager>();
                    var externalEdit = await skinManager.BeginExternalEditing(import.PerformRead(s => s.Detach())); // should not fail

                    Assert.That(Directory.Exists(externalEdit.MountedPath));

                    var directoryInfo = new DirectoryInfo(externalEdit.MountedPath);

                    Assert.That(directoryInfo.GetFiles().Select(f => f.Name), Is.EquivalentTo(new[]
                    {
                        "skin.ini",
                    }));

                    var subDirectory = directoryInfo.GetDirectories().Single();
                    Assert.That(subDirectory.Name, Is.EqualTo("folder"));
                    Assert.That(subDirectory.GetFiles().Select(f => f.Name), Is.EquivalentTo(new[]
                    {
                        "test.png",
                    }));

                    Task finishTask = Task.CompletedTask;
                    host.UpdateThread.Scheduler.Add(() => finishTask = externalEdit.Finish());
                    await finishTask;
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        /// <remarks>
        /// Invalid Windows path semantics are rejected at archive ingress, before any external-edit mount can occur.
        /// </remarks>
        [Test]
        public async Task TestImportRejectsInvalidWindowsFilenameBeforeExternalMount()
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost())
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);

                    var zipStream = new MemoryStream();
                    using var zip = ZipArchive.Create();
                    zip.AddEntry("test?.png", new MemoryStream(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }));
                    zip.SaveTo(zipStream);

                    try
                    {
                        await loadSkinIntoOsu(osu, new ImportTask(zipStream, "test skin.osk"));
                        Assert.Fail("Archive with a Windows-invalid entry name was accepted.");
                    }
                    catch (SkinArchiveImportException exception)
                    {
                        Assert.That(exception.Reason, Is.EqualTo(SkinArchiveRejectionReason.InvalidEntryName));
                    }
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        private void assertCorrectMetadata(Live<SkinInfo> import1, string name, string creator, decimal version, OsuGameBase osu)
        {
            import1.PerformRead(i =>
            {
                Assert.That(i.Name, Is.EqualTo(name));
                Assert.That(i.Creator, Is.EqualTo(creator));

                // for extra safety let's reconstruct the skin, reading from the skin.ini.
                var instance = i.CreateInstance((IStorageResourceProvider)osu.Dependencies.Get(typeof(SkinManager)));

                Assert.That(instance.Configuration.SkinInfo.Name, Is.EqualTo(name));
                Assert.That(instance.Configuration.SkinInfo.Creator, Is.EqualTo(creator));
                Assert.That(instance.Configuration.LegacyVersion, Is.EqualTo(version));
            });
        }

        private void assertImportedBoth(Live<SkinInfo> import1, Live<SkinInfo> import2)
        {
            import1.PerformRead(i1 => import2.PerformRead(i2 =>
            {
                Assert.That(i2.ID, Is.Not.EqualTo(i1.ID));
                Assert.That(i2.Hash, Is.Not.EqualTo(i1.Hash));
                Assert.That(i2.Files.First(), Is.Not.EqualTo(i1.Files.First()));
            }));
        }

        private void assertImportedOnce(Live<SkinInfo> import1, Live<SkinInfo> import2)
        {
            import1.PerformRead(i1 => import2.PerformRead(i2 =>
            {
                Assert.That(i2.ID, Is.EqualTo(i1.ID));
                Assert.That(i2.Hash, Is.EqualTo(i1.Hash));
                Assert.That(i2.Files.First(), Is.EqualTo(i1.Files.First()));
            }));
        }

        private MemoryStream createEmptyOsk()
        {
            var zipStream = new MemoryStream();
            using var zip = ZipArchive.Create();
            zip.SaveTo(zipStream);
            return zipStream;
        }

        private MemoryStream createOskWithNonIniFile()
        {
            var zipStream = new MemoryStream();
            using var zip = ZipArchive.Create();
            zip.AddEntry("hitcircle.png", new MemoryStream(new byte[] { 0, 1, 2, 3 }));
            zip.SaveTo(zipStream);
            return zipStream;
        }

        private MemoryStream createOskWithIni(string name, string author, bool makeUnique = false, string iniFilename = @"skin.ini", bool includeSectionHeader = true)
        {
            var zipStream = new MemoryStream();
            using var zip = ZipArchive.Create();
            zip.AddEntry(iniFilename, generateSkinIni(name, author, makeUnique, includeSectionHeader));
            zip.SaveTo(zipStream);
            return zipStream;
        }

        private MemoryStream generateSkinIni(string name, string author, bool makeUnique = true, bool includeSectionHeader = true)
        {
            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);

            if (includeSectionHeader)
                writer.WriteLine("[General]");

            writer.WriteLine($"Name: {name}");
            writer.WriteLine($"Author: {author}");

            if (makeUnique)
            {
                writer.WriteLine();
                writer.WriteLine($"# unique {Guid.NewGuid()}");
            }

            writer.Flush();

            return stream;
        }

        private static byte[] generateSkinIniBytes(string name, string author)
            => Encoding.UTF8.GetBytes($"[General]{Environment.NewLine}Name: {name}{Environment.NewLine}Author: {author}{Environment.NewLine}");

        private sealed class DeterministicSkinImportFaultException : Exception
        {
        }

        private async Task runSkinTest(Func<OsuGameBase, Task> action, [CallerMemberName] string callingMethodName = @"")
        {
            using (HeadlessGameHost host = new CleanRunHeadlessGameHost(callingMethodName: callingMethodName))
            {
                try
                {
                    var osu = LoadOsuIntoHost(host);
                    await action(osu);
                }
                finally
                {
                    host.Exit();
                }
            }
        }

        private async Task<Live<SkinInfo>> loadSkinIntoOsu(OsuGameBase osu, ImportTask import, bool batchImport = false)
        {
            var skinManager = osu.Dependencies.Get<SkinManager>();
            return await skinManager.Import(import, new ImportParameters { Batch = batchImport });
        }
    }
}
