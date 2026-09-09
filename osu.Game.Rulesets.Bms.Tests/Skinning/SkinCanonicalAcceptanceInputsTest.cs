// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;

namespace osu.Game.Rulesets.Bms.Tests.Skinning
{
    [TestFixture]
    public class SkinCanonicalAcceptanceInputsTest
    {
        private string outputDirectory = null!;

        [OneTimeSetUp]
        public async Task GenerateInputs()
        {
            var repository = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (repository != null && !File.Exists(Path.Combine(repository.FullName, "skin-c7-acceptance", "Generate-Inputs.ps1")))
                repository = repository.Parent;

            Assert.That(repository, Is.Not.Null, "The committed acceptance input generator must be available.");
            outputDirectory = Path.Combine(Path.GetTempPath(), $"oms-c7-input-test-{Guid.NewGuid():N}");
            var start = new ProcessStartInfo("powershell.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (string argument in new[]
                     {
                         "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                         Path.Combine(repository!.FullName, "skin-c7-acceptance", "Generate-Inputs.ps1"), "-OutputDirectory", outputDirectory,
                     })
                start.ArgumentList.Add(argument);

            using var process = Process.Start(start)!;
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            Task<string> standardError = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            Assert.That(process.ExitCode, Is.Zero, await standardError.ConfigureAwait(false));
            TestContext.Progress.WriteLine(await standardOutput.ConfigureAwait(false));
        }

        [OneTimeTearDown]
        public void RemoveGeneratedInputs()
        {
            if (outputDirectory != null && Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, recursive: true);
        }

        [TestCase("5K", "bms", BmsKeymode.Key5K, 6)]
        [TestCase("7K", "bme", BmsKeymode.Key7K, 8)]
        [TestCase("9K-BMS", "bms", BmsKeymode.Key9K_Bms, 9)]
        [TestCase("9K-PMS", "pms", BmsKeymode.Key9K_Pms, 9)]
        [TestCase("14K", "bms", BmsKeymode.Key14K, 16)]
        public void TestGeneratedBmsChartLoadsEveryLaneAndVideoWithoutKeymodeOverride(string name, string extension, BmsKeymode keymode, int laneCount)
        {
            string path = Path.Combine(outputDirectory, "chartbms", $"OMS-C7-{name}", $"observe.{extension}");
            BmsDecodedChart decoded = new BmsBeatmapDecoder().DecodeText(File.ReadAllText(path), path);
            var beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
            Assert.Multiple(() =>
            {
                Assert.That(decoded.Warnings, Is.Empty);
                Assert.That(beatmap.BmsInfo.Keymode, Is.EqualTo(keymode));
                Assert.That(beatmap.HitObjects.OfType<BmsHitObject>().Where(note => note is not BmsHoldNote).Select(note => note.LaneIndex).Distinct().Count(), Is.EqualTo(laneCount));
                Assert.That(beatmap.HitObjects.OfType<BmsHoldNote>().Select(note => note.LaneIndex).Distinct().Count(), Is.EqualTo(laneCount));
                Assert.That(beatmap.HitObjects.OfType<BmsHoldNote>().All(note => note.Duration > 0), Is.True);
                Assert.That(beatmap.BgaTimeline.Any(entry => entry.IsVideo && File.Exists(Path.Combine(Path.GetDirectoryName(path)!, entry.AssetFile))), Is.True);
                Assert.That(beatmap.BgaTimeline.Where(entry => entry.IsVideo).All(entry =>
                    Path.GetExtension(entry.AssetFile) == ".mp4" && !BmsBgaVideoCache.RequiresTranscode(entry.AssetFile)), Is.True,
                    "The delivered observation video must play through the existing native path without external transcoding.");
            });
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(12)]
        [TestCase(14)]
        [TestCase(16)]
        [TestCase(18)]
        public void TestGeneratedManiaChartLoadsEveryColumnAndSupportedNativeStage(int keys)
        {
            string path = Path.Combine(outputDirectory, "chartmania", "OMS-C7-mania", $"observe-{keys}K.osu");
            using var reader = new LineBufferedReader(File.OpenRead(path));
            Beatmap decoded = new LegacyBeatmapDecoder { ApplyOffsets = false }.Decode(reader);
            decoded.BeatmapInfo.Ruleset = new ManiaRuleset().RulesetInfo;
            var beatmap = (ManiaBeatmap)new ManiaBeatmapConverter(decoded, new ManiaRuleset()).Convert();
            Assert.Multiple(() =>
            {
                Assert.That(beatmap.Stages.Select(stage => stage.Columns), Is.EqualTo(keys > 10 ? new[] { keys / 2, keys / 2 } : new[] { keys }));
                Assert.That(beatmap.HitObjects.Where(note => note is not HoldNote).Select(note => note.Column).Distinct().Count(), Is.EqualTo(keys));
                Assert.That(beatmap.HitObjects.OfType<HoldNote>().Select(note => note.Column).Distinct().Count(), Is.EqualTo(keys));
                Assert.That(beatmap.HitObjects.OfType<HoldNote>().All(note => note.Duration == 2500), Is.True);
                Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(path)!, decoded.Metadata.AudioFile)), Is.True);
            });
        }
    }
}
