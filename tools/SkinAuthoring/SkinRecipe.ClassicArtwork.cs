// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Resources;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkinAuthoring
{
    internal static partial class SkinRecipe
    {
        private static readonly Rgba32 classicBlack = new(16, 15, 21);
        private static readonly Rgba32 classicPanel = new(40, 37, 49);
        private static readonly Rgba32 classicRim = new(67, 61, 82);
        private static readonly Rgba32 classicScratch = new(255, 102, 163);

        private static void drawClassicPart(string path, string part, string role, Rgba32 accent, bool bms)
        {
            string key = role == "white" ? "1" : role == "accent" ? "2" : "S";
            bool isKey = part is "key" or "pressed";
            string file = isKey
                ? "mania-key" + key + (part == "pressed" ? "D" : "")
                : "mania-note" + key + (part == "body" ? "L-0" : part is "head" or "tail" ? "H" : "");
            using Stream source = OsuResources.ResourceAssembly.GetManifestResourceStream($"osu.Game.Resources.Skins.Legacy.{file}@2x.png")
                                  ?? throw new InvalidDataException($"The Classic resource {file} is missing from osu.Game.Resources.");

            if (!bms && !isKey)
            {
                using Stream output = File.Create(path);
                source.CopyTo(output);
                return;
            }

            using var sprite = Image.Load<Rgba32>(source);
            if (!bms)
            {
                // Legacy key art inherits its rendered height from image aspect ratio. Fit the
                // released and pressed Classic art to the existing key strip below the notes.
                draw(path, 128, 72, (x, y, w, h) =>
                    sprite[x * (sprite.Width - 1) / (w - 1), y * (sprite.Height - 1) / (h - 1)]);
                return;
            }
            bool hold = part == "body";
            int height = isKey || hold ? 128 : 24;
            int top = isKey ? 5 : hold ? 0 : 12;
            // Keep the last visible note row on the same timing anchor as the existing BMS parts.
            int bottom = isKey ? (role == "accent" ? 86 : 114) : hold ? height : 22;
            int inset = isKey ? 4 : hold ? 0 : 2;
            draw(path, 128, height, (x, y, width, _) =>
            {
                if (x < inset || x >= width - inset || y < top || y >= bottom)
                    return isKey ? classicBlack : default;
                int sourceX = (x - inset) * (sprite.Width - 1) / (width - 2 * inset - 1);
                int sourceY = (y - top) * (sprite.Height - 1) / (bottom - top - 1);
                Rgba32 pixel = sprite[sourceX, sourceY];
                if (role == "white")
                    return pixel;
                Rgba32 light = role == "scratch" ? classicScratch : accent;
                if (isKey && part == "key")
                    light = mix(classicPanel, light, 0.43);
                double intensity = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) / 255.0;
                return mix(classicBlack, light, intensity, pixel.A);
            });
        }

        private static Rgba32 classicBackdrop(int x, int y, int w, int h, Rgba32 background)
        {
            double across = (x + 0.5) / w - 0.5;
            double down = (y + 0.5) / h;
            double light = Math.Max(0, 1 - across * across * 3 - down);
            return mix(background, classicPanel, light * 0.43);
        }

        private static Rgba32 classicLane(string role)
            => role switch
            {
                "accent" => new Rgba32(14, 14, 19),
                "scratch" => new Rgba32(27, 17, 25),
                _ => new Rgba32(25, 24, 31),
            };

        private static Rgba32 classicInstrument(int x, int y, int w, int h)
        {
            const int radius = 9;
            int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
            int rx = Math.Max(0, radius - dx), ry = Math.Max(0, radius - dy);
            if (rx * rx + ry * ry > radius * radius)
                return default;
            if (dy == 0 || dx == 0)
                return classicRim;
            return mix(classicPanel, classicBlack, 0.28 + 0.36 * y / h);
        }

        private static Rgba32 classicPlatter(int x, int y, int w, Rgba32 accent, bool pressed)
        {
            (double radius, double angle) = polar(x, y, w, w);
            double opacity = Math.Clamp((0.96 - radius) * w / 2, 0, 1);
            if (opacity == 0)
                return default;
            Rgba32 colour = radius switch
            {
                > 0.89 => classicRim,
                > 0.79 => mix(classicPanel, pressed ? classicScratch : accent, pressed ? 0.85 : 0.43),
                > 0.26 => mix(classicBlack, classicPanel, 0.52 + Math.Sin(radius * 122) * 0.12),
                _ => Math.Abs(angle - 1.1) < 0.26 ? porcelain : mix(classicPanel, accent, 0.51),
            };
            colour.A = (byte)(255 * opacity);
            return colour;
        }

        private static Rgba32 classicGaugeCell(int x, int y, int h, Rgba32 accent)
            => x % 20 is >= 3 and <= 16 && y >= 3 && y < h - 3
                ? mix(accent, porcelain, y < 8 ? 0.28 : 0.04)
                : default;
    }
}
