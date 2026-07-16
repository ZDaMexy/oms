// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using SixLabors.ImageSharp;

namespace osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate
{
    [TestFixture]
    public class BmsNoteAnimationManualGateGeneratorTest
    {
        [Test]
        public void TestGenerateAndValidateManualGateArtifacts()
        {
            string tempRoot = Path.Combine(Path.GetTempPath(), $"oms-bms-note-animation-gate-{Guid.NewGuid():N}");

            try
            {
                string first = Path.Combine(tempRoot, "first");
                string second = Path.Combine(tempRoot, "second");
                BmsNoteAnimationManualGateGenerator.Generate(first);
                BmsNoteAnimationManualGateGenerator.Generate(second);

                assertTreesEqual(first, second);
                assertGoodPackage(Path.Combine(first, BmsNoteAnimationManualGateGenerator.GOOD_PACKAGE_FILENAME));
                assertBrokenPackage(Path.Combine(first, BmsNoteAnimationManualGateGenerator.BROKEN_PACKAGE_FILENAME));
                assertChart(Path.Combine(first, "chartbms", "bms-note-animation-manual-gate", BmsNoteAnimationManualGateGenerator.CHART_FILENAME));

                string? requestedOutput = Environment.GetEnvironmentVariable(BmsNoteAnimationManualGateGenerator.OUTPUT_ENVIRONMENT_VARIABLE);

                if (!string.IsNullOrWhiteSpace(requestedOutput))
                {
                    string exported = Path.GetFullPath(requestedOutput);
                    BmsNoteAnimationManualGateGenerator.Generate(exported);
                    TestContext.Progress.WriteLine($"Manual gate artifacts generated at: {exported}");
                }
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
        }

        private static void assertTreesEqual(string first, string second)
        {
            string[] firstFiles = relativeFiles(first);
            string[] secondFiles = relativeFiles(second);
            Assert.That(secondFiles, Is.EqualTo(firstFiles));

            foreach (string relativePath in firstFiles)
            {
                Assert.That(hash(Path.Combine(second, relativePath)), Is.EqualTo(hash(Path.Combine(first, relativePath))), relativePath);
            }
        }

        private static void assertGoodPackage(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            string[] names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(names, Has.Length.EqualTo(BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT + 1));
                Assert.That(names, Does.Contain("skin.ini"));
                Assert.That(names, Does.Contain("notes/ordinary-0.png"));
                Assert.That(names, Does.Contain($"notes/ordinary-{BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT - 1}.png"));
                Assert.That(names, Does.Not.Contain("notes/ordinary.png"));
            });

            for (int i = 0; i < BmsNoteAnimationManualGateGenerator.ANIMATION_FRAME_COUNT; i++)
            {
                ZipArchiveEntry entry = archive.GetEntry($"notes/ordinary-{i}.png")!;
                using Stream stream = entry.Open();
                ImageInfo? info = Image.Identify(stream);

                Assert.Multiple(() =>
                {
                    Assert.That(info, Is.Not.Null, entry.FullName);
                    Assert.That(info!.Width, Is.EqualTo(BmsNoteAnimationManualGateGenerator.FRAME_WIDTH), entry.FullName);
                    Assert.That(info.Height, Is.EqualTo(BmsNoteAnimationManualGateGenerator.FRAME_HEIGHT), entry.FullName);
                });
            }
        }

        private static void assertBrokenPackage(string path)
        {
            using ZipArchive archive = ZipFile.OpenRead(path);
            string[] names = archive.Entries.Select(entry => entry.FullName).OrderBy(name => name, StringComparer.Ordinal).ToArray();

            Assert.That(names, Is.EqualTo(new[] { "notes/ordinary-1.png", "skin.ini" }));
            Assert.That(archive.GetEntry("notes/ordinary-0.png"), Is.Null);
            Assert.That(archive.GetEntry("notes/ordinary.png"), Is.Null);
        }

        private static void assertChart(string path)
        {
            string text = File.ReadAllText(path);
            BmsDecodedChart decoded = new BmsBeatmapDecoder().DecodeText(text, BmsNoteAnimationManualGateGenerator.CHART_FILENAME);
            int[] expectedChannels = { 0x11, 0x12, 0x13, 0x14, 0x15, 0x18, 0x19 };

            Assert.Multiple(() =>
            {
                Assert.That(decoded.BeatmapInfo.Keymode, Is.EqualTo(BmsKeymode.Key7K));
                Assert.That(decoded.BeatmapInfo.Title, Is.EqualTo("OMS BMS Note Animation Manual Gate"));
                Assert.That(decoded.ObjectEvents.Count, Is.GreaterThan(90));
                Assert.That(decoded.ObjectEvents.Select(note => note.Channel).Distinct(), Is.SupersetOf(expectedChannels));
                Assert.That(decoded.Warnings, Is.Empty);
            });
        }

        private static string[] relativeFiles(string root)
            => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                        .Select(path => Path.GetRelativePath(root, path))
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToArray();

        private static string hash(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
    }
}
