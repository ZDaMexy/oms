// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Bms.Tests.Skinning.ManualGate
{
    /// <summary>
    /// Generates the source-owned, deterministic artifacts used to manually verify managed BMS note animation.
    /// </summary>
    internal static class BmsNoteAnimationManualGateGenerator
    {
        public const string OUTPUT_ENVIRONMENT_VARIABLE = "OMS_BMS_NOTE_ANIMATION_GATE_OUTPUT";
        public const string GOOD_PACKAGE_FILENAME = "bms-note-animation-manual-gate.osk";
        public const string BROKEN_PACKAGE_FILENAME = "bms-note-animation-manual-gate-broken.osk";
        public const string CHART_FILENAME = "bms-note-animation-manual-gate.bme";
        public const int ANIMATION_FRAME_COUNT = 60;
        public const int FRAME_WIDTH = 96;
        public const int FRAME_HEIGHT = 32;

        private const string note_resource = "notes/ordinary";

        private static readonly DateTimeOffset archive_timestamp = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        private static readonly UTF8Encoding utf8_without_bom = new UTF8Encoding(false);

        private const string good_skin_ini = """
[General]
Name: OMS BMS Note Animation Manual Gate
Author: OMS contributors
Version: 2.7

[Bms]
Keymode: 7K
NoteImage1: notes/ordinary
""";

        private const string broken_skin_ini = """
[General]
Name: OMS BMS Note Animation Manual Gate (broken)
Author: OMS contributors
Version: 2.7

[Bms]
Keymode: 7K
NoteImage1: notes/ordinary
""";

        private const string chart = """
#PLAYER 1
#GENRE OMS Manual Gate
#TITLE OMS BMS Note Animation Manual Gate
#ARTIST OMS contributors
#BPM 90
#PLAYLEVEL 1
#RANK 2
#TOTAL 100

#00111:0101010101010101
#00112:0100000000000000
#00113:0001000000000000
#00114:0000010000000000
#00115:0000000100000000
#00118:0000000001000000
#00119:0000000000010000
#00211:0101010101010101
#00311:0101010101010101
#00411:0101010101010101
#00511:0101010101010101
#00611:0101010101010101
#00711:0101010101010101
#00811:0101010101010101
#00911:0101010101010101
#01011:0101010101010101
#01111:0101010101010101
#01211:0101010101010101
""";

        public static IReadOnlyList<string> Generate(string outputDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

            string root = Path.GetFullPath(outputDirectory);
            Directory.CreateDirectory(root);

            byte[][] frames = Enumerable.Range(0, ANIMATION_FRAME_COUNT).Select(createFrame).ToArray();
            byte[] goodPackage = createGoodPackage(frames);
            byte[] brokenPackage = createBrokenPackage(frames[1]);
            var generated = new List<string>();

            write(GOOD_PACKAGE_FILENAME, goodPackage);
            write(BROKEN_PACKAGE_FILENAME, brokenPackage);
            write(Path.Combine("chartbms", "bms-note-animation-manual-gate", CHART_FILENAME), utf8_without_bom.GetBytes(normaliseText(chart)));

            string manifest = string.Join(
                "\n",
                generated.OrderBy(path => path, StringComparer.Ordinal)
                         .Select(path => $"{sha256(path)}  {Path.GetRelativePath(root, path).Replace('\\', '/')}")
            ) + "\n";

            write("SHA256SUMS.txt", utf8_without_bom.GetBytes(manifest));
            return generated;

            void write(string relativePath, byte[] content)
            {
                string path = Path.Combine(root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, content);
                generated.Add(path);
            }
        }

        public static byte[] CreateGoodPackage()
            => createGoodPackage(Enumerable.Range(0, ANIMATION_FRAME_COUNT).Select(createFrame).ToArray());

        public static byte[] CreateBrokenPackage()
            => createBrokenPackage(createFrame(1));

        private static byte[] createGoodPackage(IReadOnlyList<byte[]> frames)
        {
            var entries = new List<(string Name, byte[] Content)>
            {
                ("skin.ini", utf8_without_bom.GetBytes(normaliseText(good_skin_ini))),
            };

            for (int i = 0; i < frames.Count; i++)
                entries.Add(($"{note_resource}-{i}.png", frames[i]));

            return createArchive(entries);
        }

        private static byte[] createBrokenPackage(byte[] laterFrame)
            => createArchive(new[]
            {
                ("skin.ini", utf8_without_bom.GetBytes(normaliseText(broken_skin_ini))),
                ($"{note_resource}-1.png", laterFrame),
            });

        private static byte[] createFrame(int frameIndex)
        {
            using var image = new Image<Rgba32>(FRAME_WIDTH, FRAME_HEIGHT);
            int bandCentre = frameIndex * FRAME_WIDTH / ANIMATION_FRAME_COUNT;
            int activeQuarter = frameIndex / (ANIMATION_FRAME_COUNT / 4);

            for (int y = 0; y < FRAME_HEIGHT; y++)
            {
                for (int x = 0; x < FRAME_WIDTH; x++)
                {
                    bool border = x < 2 || x >= FRAME_WIDTH - 2 || y < 2 || y >= FRAME_HEIGHT - 2;
                    int directDistance = Math.Abs(x - bandCentre);
                    int cyclicDistance = Math.Min(directDistance, FRAME_WIDTH - directDistance);
                    bool movingBand = cyclicDistance <= 5;
                    int quarter = (x - 8) / 20;
                    bool phaseMarker = y >= FRAME_HEIGHT - 8
                                       && y < FRAME_HEIGHT - 4
                                       && x >= 8
                                       && quarter is >= 0 and < 4
                                       && x - (8 + quarter * 20) < 10;

                    image[x, y] = border
                        ? new Rgba32(35, 217, 255, 255)
                        : movingBand
                            ? cyclicDistance <= 1 ? new Rgba32(255, 255, 255, 255) : new Rgba32(255, 61, 206, 255)
                            : phaseMarker
                                ? quarter == activeQuarter ? new Rgba32(255, 255, 255, 255) : new Rgba32(46, 78, 108, 255)
                                : ((x / 8 + y / 8) & 1) == 0
                                    ? new Rgba32(13, 32, 57, 255)
                                    : new Rgba32(19, 45, 76, 255);
                }
            }

            using var output = new MemoryStream();
            image.SaveAsPng(output, new PngEncoder
            {
                ColorType = PngColorType.RgbWithAlpha,
                CompressionLevel = PngCompressionLevel.NoCompression,
                FilterMethod = PngFilterMethod.None,
            });
            return output.ToArray();
        }

        private static byte[] createArchive(IEnumerable<(string Name, byte[] Content)> entries)
        {
            using var output = new MemoryStream();

            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true, utf8_without_bom))
            {
                foreach ((string name, byte[] content) in entries.OrderBy(entry => entry.Name, StringComparer.Ordinal))
                {
                    ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.NoCompression);
                    entry.LastWriteTime = archive_timestamp;
                    entry.ExternalAttributes = 0;

                    using Stream stream = entry.Open();
                    stream.Write(content);
                }
            }

            return output.ToArray();
        }

        private static string normaliseText(string text) => text.TrimStart('\r', '\n').Replace("\r\n", "\n") + "\n";

        private static string sha256(string path)
        {
            using FileStream stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
    }
}
