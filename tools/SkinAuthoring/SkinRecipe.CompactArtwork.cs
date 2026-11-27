// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using SixLabors.ImageSharp.PixelFormats;

namespace SkinAuthoring
{
    internal static partial class SkinRecipe
    {
        private static readonly Rgba32 carbon = new(8, 13, 21);
        private static readonly Rgba32 bezel = new(31, 45, 61);
        private static readonly Rgba32 steel = new(113, 141, 164);
        private static readonly Rgba32 porcelain = new(239, 250, 255);
        private static readonly Rgba32 copper = new(240, 167, 106);
        private static readonly Rgba32 scratch = new(250, 106, 131);

        private static void drawCompactStage(string root, AuthorProfile profile)
        {
            Rgba32 bms = colour(profile.BmsAccent);
            Rgba32 mania = colour(profile.ManiaAccent);
            string scene = Path.Combine(root, "scene");
            if (profile.ClassicArtwork)
            {
                Rgba32 background = colour(profile.Background);
                foreach (string path in new[] { Path.Combine(scene, "cabinet.png"), Path.Combine(root, "mania", "cabinet.png") })
                    draw(path, 160, 90, (x, y, w, h) => classicBackdrop(x, y, w, h, background));
                draw(Path.Combine(scene, "instrument.png"), 640, 128, classicInstrument);
                draw(Path.Combine(scene, "instrument-mania.png"), 640, 112, classicInstrument);
                draw(Path.Combine(scene, "gauge-frame.png"), 768, 128, classicInstrument);
                draw(Path.Combine(scene, "gauge-cells.png"), 1000, 40, (x, y, _, h) => classicGaugeCell(x, y, h, bms));
                draw(Path.Combine(scene, "gauge-cells-mania.png"), 1000, 40, (x, y, _, h) => classicGaugeCell(x, y, h, mania));
                foreach ((string name, Rgba32 accent) in new[] { ("cool", bms), ("warm", colour(profile.Highlight)), ("fault", classicScratch) })
                    draw(Path.Combine(scene, $"judgement-{name}.png"), 384, 84, (x, y, w, _) =>
                        y is >= 75 and <= 81 && Math.Abs(x - w / 2) < 46 ? accent : default);
                return;
            }
            draw(Path.Combine(scene, "cabinet.png"), 1536, 864, (x, y, w, h) => cabinet(x, y, w, h, bms, true));
            draw(Path.Combine(root, "mania", "cabinet.png"), 1280, 720, (x, y, w, h) => cabinet(x, y, w, h, mania, false));
            draw(Path.Combine(scene, "instrument.png"), 640, 128, (x, y, w, h) => instrument(x, y, w, h, bms, false));
            draw(Path.Combine(scene, "instrument-mania.png"), 384, 112, (x, y, w, h) => instrument(x, y, w, h, mania, false));
            draw(Path.Combine(scene, "gauge-frame.png"), 768, 128, (x, y, w, h) => instrument(x, y, w, h, bms, true));
            draw(Path.Combine(scene, "gauge-cells.png"), 1000, 40, (x, y, w, h) => gaugeCell(x, y, w, h, bms));
            draw(Path.Combine(scene, "gauge-cells-mania.png"), 1000, 40, (x, y, w, h) => gaugeCell(x, y, w, h, mania));
            foreach ((string name, Rgba32 accent) in new[] { ("cool", bms), ("warm", copper), ("fault", scratch) })
                draw(Path.Combine(scene, $"judgement-{name}.png"), 384, 84, (x, y, w, h) => judgementPlate(x, y, w, h, accent));
        }

        private static void drawCompactDeck(string assets, bool bms, Rgba32 accent, bool classic)
        {
            if (bms)
            {
                foreach (string role in new[] { "white", "accent", "scratch" })
                    draw(Path.Combine(assets, $"lane-{role}.png"), 256, 64, (x, y, w, h) => classic ? classicLane(role) : laneSurface(x, y, w, h, role, accent));
            }
            else
            {
                draw(Path.Combine(assets, "lane.png"), 256, 64, (x, y, w, h) => classic ? classicLane("white") : laneSurface(x, y, w, h, "white", accent));
            }

            var dividers = bms ? new[] { ("divider", 10), ("divider-accent", 13), ("divider-scratch", 6) } : new[] { ("divider", 10) };
            foreach ((string name, int edge) in dividers)
                draw(Path.Combine(assets, name + ".png"), 256, 32, (x, _, _, _) =>
                    x < edge ? (classic ? classicRim : mix(steel, accent, x == 0 ? 0.37 : 0.10, x == 0 ? (byte)200 : (byte)125)) : default);

            draw(Path.Combine(assets, "target.png"), 256, 32, (x, y, w, _) => hitLine(x, y, w, bms ? scratch : accent));
            draw(Path.Combine(assets, "bar.png"), 256, 32, (x, y, w, _) => y == 15
                ? mix(steel, accent, x is < 24 || x >= w - 24 ? 0.40 : 0.08, 105)
                : default);
            draw(Path.Combine(assets, "frame.png"), 256, 256, (x, y, w, h) => deckRail(x, y, w, h, accent));
            draw(Path.Combine(assets, "plate.png"), 256, 96, (x, y, w, h) => classic ? classicInstrument(x, y, w, h) : deckBase(x, y, w, h, accent));
            if (!bms)
            {
                // The legacy stage gauge projects across each lane group. Keep it as a fine lower-edge
                // rail so it stays useful to partial skins without covering the complete global HUD.
                draw(Path.Combine(assets, "gauge.png"), 512, 48, (x, y, w, h) =>
                    y >= h - 2 && x is >= 8 and < 504 && x % 16 < 13 ? mix(accent, porcelain, 0.14) : default);
                return;
            }

            draw(Path.Combine(assets, "stage.png"), 256, 256, (_, y, _, h) => classic ? classicBlack : mix(carbon, bezel, y < h * 0.70 ? 0.07 : 0.36));
            if (!classic)
                draw(Path.Combine(assets, "bga-frame.png"), 1024, 768, (x, y, w, h) => videoBezel(x, y, w, h, accent));
            draw(Path.Combine(assets, "scratch-platter.png"), 512, 512, (x, y, w, _) => classic ? classicPlatter(x, y, w, accent, false) : platter(x, y, w, accent, false));
            draw(Path.Combine(assets, "flash.png"), 192, 256, (x, y, w, h) => keyLight(x, y, w, h, accent));
            draw(Path.Combine(assets, "explosion.png"), 192, 192, (x, y, w, _) => hitSpark(x, y, w, accent));
            draw(Path.Combine(assets, "gauge.png"), 512, 80, (x, y, w, h) => instrument(x, y, w, h, accent, true));
        }

        private static Rgba32 compactPart(int x, int y, int width, int height, string part, string role, Rgba32 accent, bool bms)
        {
            Rgba32 signal = role switch { "white" => porcelain, "special" => copper, "scratch" => scratch, _ => accent };
            if (part is "key" or "pressed")
                return role == "scratch" && bms
                    ? platter(x, y, width, signal, part == "pressed")
                    : keyCap(x, y, width, height, signal, role, bms, part == "pressed");

            if (part == "body")
            {
                int side = Math.Min(x, width - 1 - x) - 24;
                if (side < 0)
                    return default;
                // Vertical rails stay continuous when long-note bodies are stretched.
                double light = side < 3 ? 0.42 : side < 7 ? 0.86 : side < 13 ? 0.54 : 0.30 + 0.27 * side / 40.0;
                return mix(carbon, signal, light, 245);
            }

            int inset = role == "scratch" ? 2 : role == "accent" ? 4 : 3;
            int firstRow = bms ? 12 : part == "tail" ? 11 : 2;
            const int last_row = 21;
            int edge = Math.Min(x - inset, width - 1 - inset - x);
            if (y < firstRow || y > last_row || edge < (y == firstRow ? 2 : 0))
                return default;

            double depth = (y - firstRow) / (double)(last_row - firstRow);
            if (y == last_row)
                return mix(carbon, signal, 0.63);
            if (edge < 2)
                return mix(carbon, signal, 0.43);
            if (part == "head" && edge is >= 7 and < 13 && depth > 0.5)
                return porcelain;
            if (part == "tail" && depth > 0.56 && edge < 12)
                return mix(carbon, signal, 0.40);
            if (depth is >= 0.16 and < 0.36)
                return mix(signal, porcelain, 0.58);
            return mix(carbon, signal, (0.96 - depth * 0.30) * (0.90 + 0.10 * Math.Sin(Math.PI * x / width)));
        }

        private static Rgba32 keyCap(int x, int y, int w, int h, Rgba32 signal, string role, bool bms, bool pressed)
        {
            bool dark = role == "accent";
            int faceBottom = (int)(h * (bms ? dark ? 0.57 : 0.73 : 0.78));
            int centre = Math.Abs(x - w / 2);
            int faceTop = pressed ? h / 9 : h / 12;
            if (y is < 2 || y >= h - 2)
                return carbon;
            if (y < 5 || y >= h - 6)
                return mix(bezel, steel, y < 5 ? 0.53 : 0.28);
            if (y < faceTop || y >= faceBottom)
            {
                if (centre < 27 && y >= faceBottom + 4 && y < faceBottom + 9)
                    return mix(carbon, signal, pressed ? 0.96 : 0.46);
                if (centre < 30 && y >= faceBottom && y < faceBottom + 13)
                    return carbon;
                return x < 4 || x >= w - 4 ? carbon : mix(carbon, bezel, 0.38);
            }

            int edge = chamferedEdge(x - 8, y - faceTop, w - 16, faceBottom - faceTop, 7);
            if (edge < 0) return carbon;
            if (edge < 3) return mix(carbon, steel, 0.42);
            if (edge < 7) return mix(bezel, pressed ? signal : steel, y < h / 3 ? 0.76 : 0.34);
            double depth = (y - faceTop) / (double)(faceBottom - faceTop);
            if (y >= faceBottom - 13 && y < faceBottom - 10 && centre < 14)
                return mix(carbon, signal, dark || pressed ? 0.86 : 0.48);
            if (pressed)
                return mix(bezel, signal, dark ? 0.44 + (1 - depth) * 0.37 : 0.63 + (1 - depth) * 0.30);
            return dark
                ? mix(carbon, steel, 0.42 - depth * 0.22)
                : mix(steel, role == "special" ? mix(porcelain, signal, 0.28) : porcelain, 0.95 - depth * 0.45);
        }

        private static Rgba32 platter(int x, int y, int w, Rgba32 accent, bool pressed)
        {
            (double radius, double angle) = polar(x, y, w, w);
            double feather = Math.Clamp((0.956 - radius) * w / 2, 0, 1);
            if (feather == 0)
                return default;
            double sheen = 0.50 + 0.50 * Math.Cos(2 * (angle + 0.82));
            double mark = (angle + Math.PI) * 36 / (Math.PI * 2);
            bool tick = mark % 1 < 0.32;
            Rgba32 signal = pressed ? scratch : accent;
            Rgba32 pixel = radius switch
            {
                > 0.93 => mix(bezel, steel, 0.31 + sheen * 0.34),
                > 0.90 => mix(bezel, porcelain, 0.50 + sheen * 0.40),
                > 0.86 => carbon,
                > 0.81 => tick ? mix(carbon, signal, pressed || (int)mark % 9 < 3 ? 0.93 : 0.36) : bezel,
                > 0.77 => mix(bezel, steel, sheen * 0.40),
                > 0.32 => mix(carbon, steel, 0.06 + sheen * 0.23 + 0.08 * Math.Sin(radius * 226)),
                > 0.28 => mix(bezel, steel, 0.42 + sheen * 0.40),
                > 0.24 => carbon,
                _ => Math.Abs((x - w * 0.50) + (y - w * 0.50) * 0.52) < w * 0.014
                    ? mix(bezel, signal, 0.88) : mix(bezel, signal, 0.19 + sheen * 0.20),
            };
            pixel.A = (byte)(255 * feather);
            return pixel;
        }

        private static Rgba32 cabinet(int x, int y, int w, int h, Rgba32 accent, bool bms)
        {
            double u = x / (double)w, v = y / (double)h;
            double dx = Math.Min(u, 1 - u), dy = Math.Min(v, 1 - v);
            double light = Math.Clamp(1 - Math.Sqrt(Math.Pow((u - 0.54) * 1.55, 2) + Math.Pow((v - 0.14) * 1.22, 2)), 0, 1);
            Rgba32 matte = mix(carbon, bezel, 0.10 + light * (bms ? 0.47 : 0.33) + (y % 4 == 0 ? 0.023 : 0));
            double cut = Math.Min(dx + dy, Math.Min(dx * 2.2, dy));
            if (cut < 0.004) return carbon;
            if (cut < 0.006) return mix(carbon, steel, 0.48);
            if (cut < 0.025)
            {
                double shoulder = Math.Abs(dx * 0.70 - dy);
                if (cut < 0.014 && (dy < 0.016 ? dx > 0.065 && dx < 0.31 : dy > 0.30))
                    return mix(bezel, accent, v < 0.5 ? 0.65 : 0.42);
                return mix(carbon, bezel, 0.64 + (shoulder < 0.006 ? 0.20 : 0));
            }
            if (dy < 0.076 && dx < (0.070 - dy * 0.61))
                return mix(matte, steel, ((x + y) % 11 < 2 ? 0.26 : 0.12));
            if (v > 0.922 && dx > 0.15 && dx < 0.45)
            {
                if (v is > 0.946 and < 0.950) return mix(matte, accent, 0.38);
                if (v is > 0.954 and < 0.967 && x % 18 < 7) return mix(matte, steel, 0.26);
            }
            return matte;
        }

        private static Rgba32 laneSurface(int x, int y, int w, int h, string role, Rgba32 accent)
        {
            double horizontal = 1 - Math.Abs((2.0 * x / (w - 1)) - 1);
            double bottom = y / (double)(h - 1);
            Rgba32 led = role is "scratch" ? scratch : role is "special" ? copper : accent;
            double body = role == "white" ? 0.44 : role == "accent" ? 0.13 : 0.23;
            Rgba32 matte = mix(carbon, bezel, body + horizontal * 0.11);
            if (x is < 3 || x >= w - 3)
                return mix(matte, led, 0.06 + bottom * 0.17);
            return mix(matte, led, Math.Pow(bottom, 12) * (role is "scratch" or "special" ? 0.13 : 0.07));
        }

        private static Rgba32 hitLine(int x, int y, int w, Rgba32 accent)
            => y switch
            {
                9 or 10 => mix(carbon, accent, 0.58, 150),
                11 or 12 => mix(carbon, accent, 0.75, 210),
                13 or 14 => mix(accent, porcelain, 0.70),
                >= 15 and <= 21 => mix(accent, porcelain, x is < 16 || x >= w - 16 ? 0.44 : 0.04),
                22 or 23 => mix(carbon, accent, 0.64),
                _ => default,
            };

        private static Rgba32 deckRail(int x, int y, int w, int h, Rgba32 accent)
        {
            int side = Math.Min(x, w - 1 - x);
            if (side > 8) return default;
            if (side < 2) return carbon;
            if (side < 4) return mix(steel, accent, y < h * 0.90 ? 0.23 : 0.64);
            return mix(carbon, bezel, 0.68, (byte)(220 - 30 * (side - 4)));
        }

        private static Rgba32 deckBase(int x, int y, int w, int h, Rgba32 accent)
        {
            if (y < 3 || y >= h - 3)
                return carbon;
            if (y < 6 || y >= h - 8)
                return mix(bezel, steel, 0.46);
            if (y > h * 0.78 && x % 36 is >= 9 and <= 21)
                return mix(carbon, accent, 0.34);
            return mix(carbon, bezel, 0.38 + 0.37 * (1 - y / (double)h));
        }

        private static Rgba32 videoBezel(int x, int y, int w, int h, Rgba32 accent)
        {
            int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
            int edge = Math.Min(dx, dy);
            if (edge > 11 && dx + dy > 36) return default;
            if (edge < 2 || dx + dy is >= 33 and <= 36) return carbon;
            if (edge < 4) return mix(bezel, steel, 0.61);
            if (dy < 8 && dx is > 18 and < 88) return mix(bezel, accent, 0.72);
            if (edge < 9 || dx + dy < 32) return bezel;
            return mix(carbon, steel, 0.29);
        }

        private static Rgba32 instrument(int x, int y, int w, int h, Rgba32 accent, bool gauge)
        {
            int edge = chamferedEdge(x, y, w, h, gauge ? 20 : 22);
            if (edge < 0) return default;
            if (edge < 2) return carbon;
            if (edge < 5) return mix(bezel, steel, y < h / 2 ? 0.76 : 0.44);
            if (edge < 9) return mix(carbon, bezel, 0.76);
            if (gauge && y > h * 0.44)
                return y < h * 0.47 ? mix(bezel, steel, 0.43) : carbon;
            if (y is >= 9 and <= 12 && x > 27 && x < w * 0.31) return mix(bezel, accent, 0.65);
            if (y >= h - 14 && y < h - 9 && x > w * 0.81 && x < w - 28)
                return x % 13 < 7 ? mix(carbon, accent, 0.44) : bezel;
            return mix(carbon, bezel, 0.13 + 0.55 * (1 - y / (double)h));
        }

        private static Rgba32 gaugeCell(int x, int y, int w, int h, Rgba32 accent)
        {
            int cell = x / 20, local = x % 20;
            int edge = chamferedEdge(local - 3, y - 2, 14, h - 4, 3);
            if (edge < 0 || (cell % 5 == 4 && local >= 14) || x >= w - 4) return default;
            if (edge == 0) return mix(carbon, accent, 0.54);
            if (y < 8) return mix(accent, porcelain, 0.48);
            return mix(carbon, accent, 1.0 - y / (double)h * 0.60);
        }

        private static Rgba32 judgementPlate(int x, int y, int w, int h, Rgba32 accent)
        {
            int edge = chamferedEdge(x, y, w, h, 24);
            if (edge < 0) return default;
            if (edge < 3) return mix(bezel, accent, 0.35, 175);
            if (edge < 9 && (x < 48 || x > w - 49)) return mix(bezel, accent, 0.72, 220);
            return mix(carbon, accent, 0.085, 143);
        }

        private static Rgba32 keyLight(int x, int y, int w, int h, Rgba32 accent)
        {
            double across = 1 - Math.Abs((2.0 * x / w) - 1);
            double length = Math.Pow(Math.Clamp(1 - y / (double)h, 0, 1), 3);
            double opacity = Math.Pow(across, 2.4) * length;
            return mix(accent, porcelain, Math.Pow(across, 10) * 0.45, (byte)(112 * opacity));
        }

        private static Rgba32 hitSpark(int x, int y, int w, Rgba32 accent)
        {
            (double radius, double angle) = polar(x, y, w, w);
            double band = Math.Max(0, 1 - Math.Abs(radius - 0.43) * 12) * 0.52;
            double rays = Math.Pow(Math.Abs(Math.Cos(angle * 4 + 0.3)), 12) * Math.Max(0, 1 - radius) * 0.66;
            double core = 0.92 * Math.Max(0, 1 - radius * 4.2);
            return mix(accent, porcelain, core, (byte)(225 * Math.Clamp(band + rays + core, 0, 1)));
        }

        private static (double Radius, double Angle) polar(int x, int y, int width, int height)
        {
            double dx = 2.0 * (x + 0.5) / width - 1, dy = 2.0 * (y + 0.5) / height - 1;
            return (Math.Sqrt(dx * dx + dy * dy), Math.Atan2(dy, dx));
        }

        private static int chamferedEdge(int x, int y, int w, int h, int cut)
        {
            int dx = Math.Min(x, w - 1 - x), dy = Math.Min(y, h - 1 - y);
            return Math.Min(Math.Min(dx, dy), dx + dy - cut);
        }

        private static Rgba32 mix(Rgba32 left, Rgba32 right, double amount, byte alpha = 255)
        {
            amount = Math.Clamp(amount, 0, 1);
            return new Rgba32(
                (byte)Math.Round(left.R + (right.R - left.R) * amount),
                (byte)Math.Round(left.G + (right.G - left.G) * amount),
                (byte)Math.Round(left.B + (right.B - left.B) * amount),
                alpha);
        }
    }
}
