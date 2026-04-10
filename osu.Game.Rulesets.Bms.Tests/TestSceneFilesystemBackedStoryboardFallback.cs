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

        private BeatmapSetInfo createFilesystemBackedBeatmapSetWithoutStoryboard()
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

            BeatmapInfo beatmapInfo;

            using (var beatmapStream = new LineBufferedReader(File.OpenRead(beatmapPath)))
                beatmapInfo = Decoder.GetDecoder<Beatmap>(beatmapStream).Decode(beatmapStream).BeatmapInfo;

            using (var hashStream = File.OpenRead(beatmapPath))
            {
                beatmapInfo.MD5Hash = hashStream.ComputeMD5Hash();
                hashStream.Seek(0, SeekOrigin.Begin);
                beatmapInfo.Hash = hashStream.ComputeSHA2Hash();
            }

            var beatmapSet = new BeatmapSetInfo
            {
                DateAdded = DateTimeOffset.UtcNow,
                FilesystemStoragePath = relativePath.ToStandardisedPath(),
            };

            beatmapInfo.LocalFilePath = beatmapFilename;
            beatmapInfo.BeatmapSet = beatmapSet;
            beatmapSet.Beatmaps.Add(beatmapInfo);

            return beatmapSet;
        }
    }
}
