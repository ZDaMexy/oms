// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Bms.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsPreviewAudioBackfillTest
    {
        private const string chart_with_preview = @"
#TITLE Preview Policy Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#PREVIEW preview.ogg
#00111:AA00
";

        private const string chart_without_preview = @"
#TITLE No Preview Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#00111:AA00
";

        [Test]
        public async Task TestImportSetsPreviewFromHeaderAndPlaysFromStart()
        {
            using var storage = new TemporaryNativeStorage($"bms-preview-import-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            var id = await importManagedSet(storage, realm, "with-preview", chart_with_preview, ("preview.ogg", new byte[] { 1, 2, 3, 4 })).ConfigureAwait(false);

            // Import already honours the policy: a #PREVIEW chart gets AudioFile = the preview file and PreviewTime = 0.
            var (audioFile, previewTime) = readPreview(realm, id);
            Assert.Multiple(() =>
            {
                Assert.That(audioFile, Is.EqualTo("preview.ogg"));
                Assert.That(previewTime, Is.EqualTo(0));
            });

            // The backfill is a no-op when the stored value already matches.
            int corrected = BmsPreviewAudioBackfill.RunForTesting(realm, storage);
            Assert.That(corrected, Is.EqualTo(0));
        }

        [Test]
        public async Task TestImportSetsNoPreviewWhenHeaderAbsent()
        {
            using var storage = new TemporaryNativeStorage($"bms-preview-none-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            // A large unreferenced audio file in the folder must NOT become the AudioFile (the old detectFullMusicFile
            // heuristic is gone): without a #PREVIEW header there is no preview at all.
            var id = await importManagedSet(storage, realm, "no-preview", chart_without_preview, ("song.ogg", new byte[2_000_000])).ConfigureAwait(false);

            var (audioFile, _) = readPreview(realm, id);
            Assert.That(audioFile, Is.Empty);
        }

        [Test]
        public async Task TestBackfillCorrectsStalePreviewChart()
        {
            using var storage = new TemporaryNativeStorage($"bms-preview-correct-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            var id = await importManagedSet(storage, realm, "stale-preview", chart_with_preview, ("preview.ogg", new byte[] { 1, 2, 3, 4 })).ConfigureAwait(false);

            // Simulate a chart imported by the old logic: AudioFile points at a different (full-music) file and the
            // preview point is the legacy default.
            realm.Write(r =>
            {
                var info = r.Find<BeatmapInfo>(id);
                info!.Metadata.AudioFile = "fullsong.ogg";
                info.Metadata.PreviewTime = -1;
            });

            int corrected = BmsPreviewAudioBackfill.RunForTesting(realm, storage);

            var (audioFile, previewTime) = readPreview(realm, id);
            Assert.Multiple(() =>
            {
                Assert.That(corrected, Is.EqualTo(1));
                Assert.That(audioFile, Is.EqualTo("preview.ogg"));
                Assert.That(previewTime, Is.EqualTo(0));
            });
        }

        [Test]
        public async Task TestBackfillClearsAudioFileWhenNoPreviewHeader()
        {
            using var storage = new TemporaryNativeStorage($"bms-preview-clear-{Guid.NewGuid():N}");
            using var realm = new RealmAccess(storage, OsuGameBase.CLIENT_DATABASE_FILENAME);
            using var rulesets = new RealmRulesetStore(realm, storage);

            var id = await importManagedSet(storage, realm, "clear-audio", chart_without_preview, ("song.ogg", new byte[2_000_000])).ConfigureAwait(false);

            // Simulate a chart imported by the old detectFullMusicFile heuristic: a non-#PREVIEW chart that still got an
            // AudioFile. The backfill must clear it (no #PREVIEW => no preview).
            realm.Write(r =>
            {
                var info = r.Find<BeatmapInfo>(id);
                info!.Metadata.AudioFile = "song.ogg";
                info.Metadata.PreviewTime = -1;
            });

            int corrected = BmsPreviewAudioBackfill.RunForTesting(realm, storage);

            var (audioFile, previewTime) = readPreview(realm, id);
            Assert.Multiple(() =>
            {
                Assert.That(corrected, Is.EqualTo(1));
                Assert.That(audioFile, Is.Empty);
                Assert.That(previewTime, Is.EqualTo(-1));
            });
        }

        private static async Task<Guid> importManagedSet(Storage storage, RealmAccess realm, string setName, string chartText, params (string name, byte[] content)[] extraFiles)
        {
            string managedRoot = Path.Combine(storage.GetFullPath(BmsFolderImporter.SONGS_STORAGE_PATH), "packs", setName);
            Directory.CreateDirectory(managedRoot);
            File.WriteAllText(Path.Combine(managedRoot, "chart.bms"), chartText);

            foreach (var (name, content) in extraFiles)
                File.WriteAllBytes(Path.Combine(managedRoot, name), content);

            var importer = new BmsFolderImporter(storage, realm);
            var result = await importer.RegisterManagedDirectory(managedRoot).ConfigureAwait(false);

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);
            return result.ImportedBeatmapSet!.PerformRead(set => set.Beatmaps.Single().ID);
        }

        private static (string audioFile, int previewTime) readPreview(RealmAccess realm, Guid id)
            => realm.Run(r =>
            {
                var info = r.Find<BeatmapInfo>(id);
                return (info!.Metadata.AudioFile ?? string.Empty, info.Metadata.PreviewTime);
            });
    }
}
