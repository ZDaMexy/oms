// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Text.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkinAuthoring
{
    /// <summary>Offline author artwork contact sheet, explicitly labelled rather than represented as a gameplay capture.</summary>
    internal static class SkinPreview
    {
        private static readonly JsonSerializerOptions json_options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static void Create(string root, string output)
        {
            var profile = JsonSerializer.Deserialize<AuthorProfile>(File.ReadAllText(Path.Combine(root, "author.json")), json_options)!;
            using var image = new Image<Rgba32>(1600, 1000, new Rgba32(7, 11, 19));
            text(image, profile.Complex ? "OMS COMPLEX / ASTRAL" : "OMS SIMPLE / STILL", 55, 45, 5, new Rgba32(233, 242, 251));
            text(image, "BMS", 65, 124, 3, new Rgba32(82, 223, 223));
            text(image, "MANIA", 850, 124, 3, new Rgba32(191, 154, 255));
            rect(image, 54, 106, 1492, 2, new Rgba32(67, 90, 110));
            foreach ((string ruleset, int panel, int count) in new[] { ("bms", 65, 8), ("mania", 850, 7) })
            {
                int top = 215, stageWidth = ruleset == "bms" ? 650 : 580, laneWidth = stageWidth / count;
                if (profile.Complex)
                {
                    blit(image, Path.Combine(root, "scene", "console.png"), panel, 159, stageWidth, 44);
                    text(image, "ASTRAL", panel + 12, 170, 2, new Rgba32(255, 211, 138));
                    text(image, "SCORE", panel + 145, 167, 1, new Rgba32(145, 161, 185));
                    text(image, "0874200", panel + 145, 180, 2, new Rgba32(233, 243, 255));
                    text(image, "ACCURACY", panel + 255, 167, 1, new Rgba32(145, 161, 185));
                    text(image, "99.50%", panel + 255, 181, 2, new Rgba32(233, 243, 255));
                    text(image, "COMBO", panel + 355, 167, 1, new Rgba32(145, 161, 185));
                    text(image, "128", panel + 355, 180, 2, new Rgba32(255, 211, 138));
                    text(image, "BPM 150", panel + 445, 176, 2, new Rgba32(192, 206, 224));
                    rect(image, panel, 204, stageWidth * 2 / 5, 2, new Rgba32(255, 211, 138));
                    rect(image, panel, 209, stageWidth * 3 / 4, 2, new Rgba32(82, 223, 223));
                }
                blit(image, Path.Combine(root, ruleset, "plate.png"), panel, top, stageWidth, 680);
                for (int lane = 0; lane < count; lane++)
                {
                    string role = ruleset == "bms" && lane == 0 ? "scratch" : ruleset == "mania" && lane == 3 ? "special" : lane % 2 == 0 ? "accent" : "white";
                    int x = panel + lane * laneWidth;
                    blit(image, Path.Combine(root, ruleset, "lane.png"), x, top, laneWidth - 1, 610);
                    blit(image, Path.Combine(root, ruleset, "divider.png"), x, top, laneWidth, 610);
                    int noteY = top + 50 + lane * 61 % 420;
                    if (lane % 3 == 1)
                    {
                        blit(image, Path.Combine(root, ruleset, $"body-{role}.png"), x, noteY, laneWidth - 1, 210);
                        blit(image, Path.Combine(root, ruleset, $"head-{role}.png"), x, noteY + 200, laneWidth - 1, 19);
                        blit(image, Path.Combine(root, ruleset, $"tail-{role}.png"), x, noteY - 2, laneWidth - 1, 19);
                    }
                    else
                    {
                        blit(image, Path.Combine(root, ruleset, $"note-{role}.png"), x, noteY, laneWidth - 1, 19);
                        blit(image, Path.Combine(root, ruleset, $"note-{role}.png"), x, noteY + 170, laneWidth - 1, 19);
                    }
                    blit(image, Path.Combine(root, ruleset, $"key-{role}.png"), x, top + 610, laneWidth - 1, 54);
                }
                for (int bar = 0; bar < 4; bar++)
                    blit(image, Path.Combine(root, ruleset, "bar.png"), panel, top + 100 + bar * 140, stageWidth, 16);
                blit(image, Path.Combine(root, ruleset, "target.png"), panel, top + 601, stageWidth, 26);
                blit(image, Path.Combine(root, ruleset, "frame.png"), panel - 6, top, stageWidth + 12, 680);
                blit(image, Path.Combine(root, ruleset, "gauge.png"), panel, 922, stageWidth * 3 / 4, 12);
                text(image, "PERFECT", panel + stageWidth / 2 - 70, 675, 3, new Rgba32(243, 247, 255));
                text(image, "128", panel + stageWidth / 2 - 27, 720, 3, new Rgba32(243, 247, 255));
                if (!profile.Complex)
                {
                    // Public global children resolve the safe screen independently of the slot owner's band.
                    rect(image, panel, 159, stageWidth, 44, new Rgba32(12, 18, 27));
                    text(image, "SCORE", panel + 12, 167, 1, new Rgba32(145, 161, 185));
                    text(image, "0874200", panel + 12, 181, 2, new Rgba32(233, 243, 255));
                    text(image, "ACCURACY", panel + 205, 167, 1, new Rgba32(145, 161, 185));
                    text(image, "99.50%", panel + 205, 181, 2, new Rgba32(233, 243, 255));
                    text(image, "COMBO", panel + 370, 167, 1, new Rgba32(145, 161, 185));
                    text(image, "128", panel + 370, 181, 2, new Rgba32(233, 243, 255));
                    text(image, "BPM 150", panel + 455, 176, 2, new Rgba32(192, 206, 224));
                    rect(image, panel, 209, stageWidth * 2 / 5, 2, new Rgba32(163, 183, 203));
                }
                if (profile.Complex)
                {
                    blit(image, Path.Combine(root, "scene", "orbit.png"), panel + stageWidth + 14, 370, 60, 60);
                    blit(image, Path.Combine(root, "scene", "prism.png"), panel + stageWidth + 29, 385, 30, 30);
                    for (int bar = 0; bar < 4; bar++)
                        rect(image, panel + stageWidth + 20 + 11 * bar, 545 + 12 * bar, 5, 78 - 12 * bar, new Rgba32(82, 223, 223, 150));
                }
            }
            text(image, "AUTHOR DESIGN PREVIEW / ACTUAL GAMEPLAY REVIEW PENDING", 55, 955, 2, new Rgba32(146, 159, 178));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            image.SaveAsPng(output);
            Console.WriteLine($"作者设计预览（并非游戏截图）：{output}");
        }

        private static void rect(Image<Rgba32> target, int left, int top, int width, int height, Rgba32 colour)
        {
            for (int y = Math.Max(0, top); y < Math.Min(target.Height, top + height); y++)
                for (int x = Math.Max(0, left); x < Math.Min(target.Width, left + width); x++)
                    target[x, y] = colour;
        }

        private static void blit(Image<Rgba32> target, string path, int left, int top, int width, int height)
        {
            using var source = Image.Load<Rgba32>(path);
            for (int y = Math.Max(0, top); y < Math.Min(target.Height, top + height); y++)
                for (int x = Math.Max(0, left); x < Math.Min(target.Width, left + width); x++)
                {
                    Rgba32 pixel = source[(x - left) * source.Width / width, (y - top) * source.Height / height];
                    Rgba32 under = target[x, y];
                    float alpha = pixel.A / 255f;
                    target[x, y] = new Rgba32((byte)(under.R * (1 - alpha) + pixel.R * alpha), (byte)(under.G * (1 - alpha) + pixel.G * alpha), (byte)(under.B * (1 - alpha) + pixel.B * alpha));
                }
        }

        private static void text(Image<Rgba32> target, string content, int left, int top, int size, Rgba32 colour)
        {
            const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789/";
            string[] glyphs = {
                "0111010001111111000110001", "1111010001111101000111110", "0111110000100001000001111", "1111010001100011000111110", "1111110000111101000011111", "1111110000111101000010000", "0111110000101111000101111", "1000110001111111000110001", "1111100100001000010011111", "0011100010000101001001100", "1000110010111001001010001", "1000010000100001000011111", "1000111011101011000110001", "1000111001101011001110001", "0111010001100011000101110", "1111010001111101000010000", "0111010001101011001001101", "1111010001111101001010001", "0111110000011100000111110", "1111100100001000010000100", "1000110001100011000101110", "1000110001100010101000100", "1000110001101011101110001", "1000101010001000101010001", "1000101010001000010000100", "1111100010001000100011111", "0111010011101011100101110", "0010001100001000010001110", "0111010001000100010011111", "1111000001001100000111110", "1001010010111110001000010", "1111110000111100000111110", "0111010000111101000101110", "1111100010001000100001000", "0111010001011101000101110", "0111010001011110000101110", "0000100010001000100010000"
            };
            foreach (char character in content)
            {
                int index = alphabet.IndexOf(character);
                string? punctuation = character switch { '.' => "0000000000000000011000110", '%' => "1100111010001000101110011", _ => null };
                if (punctuation != null)
                    for (int i = 0; i < 25; i++)
                        if (punctuation[i] == '1')
                            rect(target, left + i % 5 * size, top + i / 5 * size, size, size, colour);
                if (index >= 0)
                    for (int i = 0; i < 25; i++)
                        if (glyphs[index][i] == '1')
                            rect(target, left + i % 5 * size, top + i / 5 * size, size, size, colour);
                left += 6 * size;
            }
        }
    }
}
