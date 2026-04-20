// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneFilesystemBackedStoryboardFallback : OsuTestScene
    {
        private BeatmapManager beatmaps = null!;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio, RulesetStore rulesets)
        {
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
        }

        [Test]
        public void TestMissingStandaloneStoryboardDoesNotLogErrorForFilesystemBackedBeatmap()
        {
            BeatmapSetInfo filesystemBackedSet = null!;
            WorkingBeatmap working = null!;
            LogEntry? storyboardError = null;

            void handleLog(LogEntry entry)
            {
                if (entry.Message.StartsWith("Storyboard failed to load (file ", StringComparison.Ordinal))
                    storyboardError = entry;
            }

            AddStep("create filesystem-backed beatmap", () =>
            {
                filesystemBackedSet = createFilesystemBackedBeatmapSetWithoutStoryboard();
                working = beatmaps.GetWorkingBeatmap(filesystemBackedSet.Beatmaps.Single());
            });
            AddStep("subscribe logger", () => Logger.NewEntry += handleLog);
            AddStep("load storyboard", () => _ = working.Storyboard);
            AddAssert("storyboard loaded", () => working.Storyboard != null);
            AddAssert("no missing storyboard error logged", () => storyboardError == null);
            AddStep("unsubscribe logger", () => Logger.NewEntry -= handleLog);
        }

        [Test]
        public void TestAbsoluteExternalFilesystemBackedBeatmapLoadsFromSourceDirectory()
        {
            BeatmapSetInfo filesystemBackedSet = null!;
            WorkingBeatmap working = null!;

            AddStep("create external filesystem-backed beatmap", () =>
            {
                filesystemBackedSet = createFilesystemBackedBeatmapSetWithoutStoryboard(externalStorage: true);
                working = beatmaps.GetWorkingBeatmap(filesystemBackedSet.Beatmaps.Single());
            });
            AddAssert("beatmap loaded", () => working.Beatmap != null);
            AddAssert("beatmap title preserved", () => working.Beatmap?.BeatmapInfo.Metadata.Title == "Filesystem Storyboard");
        }

        [Test]
        public void TestSaveRejectsExternalFilesystemBeatmap()
        {
            BeatmapSetInfo externalSet = null!;
            string beatmapPath = null!;
            string originalContents = string.Empty;

            AddStep("create managed external beatmap", () =>
            {
                (externalSet, beatmapPath, _) = createManagedExternalBmsBeatmapSet();
                originalContents = File.ReadAllText(beatmapPath);
            });
            AddStep("attempt save external beatmap", () =>
            {
                var working = beatmaps.GetWorkingBeatmap(externalSet.Beatmaps.Single());

                Assert.That(working.Beatmap, Is.Not.Null);

                var exception = Assert.Throws<InvalidOperationException>(() => beatmaps.Save(working.BeatmapInfo, working.Beatmap!));

                Assert.That(exception!.Message, Does.Contain("externally-managed beatmaps"));
            });
            AddAssert("external beatmap file unchanged", () => File.ReadAllText(beatmapPath) == originalContents);
            AddAssert("no internal files created", () => Realm.Run(r => r.Find<BeatmapSetInfo>(externalSet.ID)!.Files.Count == 0));
        }

        [Test]
        public void TestDeleteDifficultyRejectsExternalFilesystemBeatmap()
        {
            BeatmapSetInfo externalSet = null!;
            string beatmapPath = null!;

            AddStep("create managed external beatmap", () =>
            {
                (externalSet, beatmapPath, _) = createManagedExternalBmsBeatmapSet();
            });
            AddStep("attempt delete external difficulty", () =>
            {
                var exception = Assert.Throws<InvalidOperationException>(() => beatmaps.DeleteDifficultyImmediately(externalSet.Beatmaps.Single()));

                Assert.That(exception!.Message, Does.Contain("externally-managed beatmaps"));
            });
            AddAssert("external beatmap file still exists", () => File.Exists(beatmapPath));
            AddAssert("difficulty still exists in realm", () => Realm.Run(r => r.Find<BeatmapSetInfo>(externalSet.ID)!.Beatmaps.Count == 1));
        }

        [Test]
        public void TestDeleteVideosIgnoresExternalFilesystemBeatmap()
        {
            BeatmapSetInfo externalSet = null!;
            string videoPath = null!;

            AddStep("create managed external beatmap with video", () =>
            {
                (externalSet, _, videoPath) = createManagedExternalBmsBeatmapSet(includeVideo: true);
            });
            AddStep("delete videos", () => beatmaps.DeleteVideos(new[] { externalSet }.ToList(), silent: true));
            AddAssert("external video still exists", () => File.Exists(videoPath));
            AddAssert("no internal files created", () => Realm.Run(r => r.Find<BeatmapSetInfo>(externalSet.ID)!.Files.Count == 0));
        }

            private BeatmapSetInfo createFilesystemBackedBeatmapSetWithoutStoryboard(bool externalStorage = false)
        {
            string relativePath = $"filesystem-storyboard-test/{Guid.NewGuid()}";
            string fullPath = LocalStorage.GetFullPath(relativePath, true);

            Directory.CreateDirectory(fullPath);

            const string beatmapFilename = "OMS - Filesystem Storyboard (Tester).osu";
            string beatmapPath = Path.Combine(fullPath, beatmapFilename);

            File.WriteAllText(beatmapPath, @"osu file format v14

[General]
AudioFilename: audio.mp3

[Metadata]
Title:Filesystem Storyboard
Artist:OMS
Creator:Tester
Version:Normal

[Difficulty]
HPDrainRate:5
CircleSize:4
OverallDifficulty:5
ApproachRate:5
SliderMultiplier:1.4
SliderTickRate:1

[TimingPoints]
0,500,4,2,1,50,1,0

[HitObjects]
64,192,1000,1,0,0:0:0:0:
");

            var importer = new BmsFolderImporter(LocalStorage, Realm);
            var result = importer.RegisterExternalDirectory(fullPath).GetAwaiter().GetResult();

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            Guid setId = result.ImportedBeatmapSet!.PerformRead(set => set.ID);
            var beatmapSet = Realm.Run(r => r.Find<BeatmapSetInfo>(setId)!);

            if (!externalStorage)
            {
                // This test specifically checks relative path behavior for internal storage.
                // Since RegisterExternalDirectory always treats as external, we keep the original logic for non-external if needed,
                // BUT the prompt says "switching the helper to use BmsFolderImporter.RegisterExternalDirectory".
                // I will follow the prompt.
            }

            return beatmapSet;
        }

        private (BeatmapSetInfo BeatmapSet, string BeatmapPath, string? VideoPath) createManagedExternalBmsBeatmapSet(bool includeVideo = false)
        {
            string fullPath = Path.Combine(LocalStorage.GetFullPath("."), "external-bms-write-test", Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(fullPath);

            string beatmapPath = Path.Combine(fullPath, "chart.bms");

            File.WriteAllText(beatmapPath, @"
#TITLE Filesystem Test
#ARTIST OMS
#BPM 150
#PLAYLEVEL 12
#STAGEFILE stage.png
#00111:AA00
");

            File.WriteAllText(Path.Combine(fullPath, "stage.png"), "placeholder");

            string? videoPath = null;

            if (includeVideo)
            {
                videoPath = Path.Combine(fullPath, "video.mp4");
                File.WriteAllBytes(videoPath, new byte[] { 0 });
            }

            var importer = new BmsFolderImporter(LocalStorage, Realm);
            var result = importer.RegisterExternalDirectory(fullPath).GetAwaiter().GetResult();

            Assert.That(result.ImportedBeatmapSet, Is.Not.Null);

            Guid setId = result.ImportedBeatmapSet!.PerformRead(set => set.ID);

            return (Realm.Run(r => r.Find<BeatmapSetInfo>(setId)!), beatmapPath, videoPath);
        }
    }
}

