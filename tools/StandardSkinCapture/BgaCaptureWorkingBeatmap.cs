// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using osu.Framework.Audio;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Tests.Visual;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Tools.StandardSkinCapture;

/// <summary>Supplies one generated test image through the ordinary native BMS resource stream.</summary>
internal sealed class BgaCaptureWorkingBeatmap : OsuTestScene.ClockBackedTestWorkingBeatmap
{
    public const string ImageFilename = "oms-bga-test.png";
    public const int ImageWidth = 640;
    public const int ImageHeight = 480;

    public string ImagePath { get; }
    public int ImageReads => Volatile.Read(ref imageReads);
    private int imageReads;

    public BgaCaptureWorkingBeatmap(IBeatmap beatmap, IFrameBasedClock clock, AudioManager audio, string fixtureDirectory)
        : base(beatmap, null!, clock, audio)
    {
        ImagePath = Path.Combine(fixtureDirectory, ImageFilename);
        if (!File.Exists(ImagePath))
            throw new FileNotFoundException("Generate the BGA fixture before creating its working beatmap.", ImagePath);
    }

    public override Stream? GetStream(string storagePath)
    {
        if (!string.Equals(storagePath.Replace('\\', '/'), ImageFilename, StringComparison.Ordinal))
            return null;
        FileStream stream = File.OpenRead(ImagePath);
        Interlocked.Increment(ref imageReads);
        return stream;
    }

    public static string GenerateImage(string outputDirectory)
    {
        string directory = Path.Combine(outputDirectory, "bga-fixture");
        Directory.CreateDirectory(directory);
        using var image = new Image<Rgba32>(ImageWidth, ImageHeight);
        Rgba32[] quadrants = [new(235, 66, 60), new(50, 210, 110), new(70, 130, 245), new(245, 210, 55)];
        for (int y = 0; y < ImageHeight; y++)
            for (int x = 0; x < ImageWidth; x++)
            {
                Rgba32 colour = quadrants[(y >= ImageHeight / 2 ? 2 : 0) + (x >= ImageWidth / 2 ? 1 : 0)];
                if (((x / 40 + y / 40) & 1) != 0)
                    colour = new Rgba32((byte)(colour.R * 0.6), (byte)(colour.G * 0.6), (byte)(colour.B * 0.6));
                if (x < 12 || x >= ImageWidth - 12 || y < 12 || y >= ImageHeight - 12
                    || Math.Abs(x - ImageWidth / 2) < 3 || Math.Abs(y - ImageHeight / 2) < 3)
                    colour = new Rgba32(255, 255, 255);
                image[x, y] = colour;
            }

        DrawLabel(image, "1", 28, 28, 6);
        DrawLabel(image, "2", 560, 28, 6);
        DrawLabel(image, "3", 28, 390, 6);
        DrawLabel(image, "4", 560, 390, 6);
        DrawLabel(image, "OMS BGA TEST", 112, 208, 6);
        using FileStream stream = new FileStream(Path.Combine(directory, ImageFilename), FileMode.CreateNew, FileAccess.Write);
        image.SaveAsPng(stream);
        return directory;
    }

    private static void DrawLabel(Image<Rgba32> image, string text, int left, int top, int scale)
    {
        for (int y = top - 6; y < top + 7 * scale + 6; y++)
            for (int x = left - 6; x < left + text.Length * 6 * scale; x++)
                image[x, y] = new Rgba32(0, 0, 0);
        for (int letter = 0; letter < text.Length; letter++)
        {
            string[] glyph = glyphs[text[letter]];
            for (int row = 0; row < 7; row++)
                for (int column = 0; column < 5; column++)
                    if (glyph[row][column] == '1')
                        for (int dy = 0; dy < scale; dy++)
                            for (int dx = 0; dx < scale; dx++)
                                image[left + (letter * 6 + column) * scale + dx, top + row * scale + dy] = new Rgba32(255, 255, 255);
        }
    }

    private static readonly IReadOnlyDictionary<char, string[]> glyphs = new Dictionary<char, string[]>
    {
        [' '] = ["00000", "00000", "00000", "00000", "00000", "00000", "00000"],
        ['O'] = ["01110", "10001", "10001", "10001", "10001", "10001", "01110"],
        ['M'] = ["10001", "11011", "10101", "10101", "10001", "10001", "10001"],
        ['S'] = ["01111", "10000", "10000", "01110", "00001", "00001", "11110"],
        ['B'] = ["11110", "10001", "10001", "11110", "10001", "10001", "11110"],
        ['G'] = ["01110", "10001", "10000", "10111", "10001", "10001", "01110"],
        ['A'] = ["01110", "10001", "10001", "11111", "10001", "10001", "10001"],
        ['T'] = ["11111", "00100", "00100", "00100", "00100", "00100", "00100"],
        ['E'] = ["11111", "10000", "10000", "11110", "10000", "10000", "11111"],
        ['1'] = ["00100", "01100", "00100", "00100", "00100", "00100", "01110"],
        ['2'] = ["01110", "10001", "00001", "00010", "00100", "01000", "11111"],
        ['3'] = ["11110", "00001", "00001", "01110", "00001", "00001", "11110"],
        ['4'] = ["00010", "00110", "01010", "10010", "11111", "00010", "00010"],
    };
}
