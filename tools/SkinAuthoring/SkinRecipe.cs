// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Globalization;
using System.Text;
using System.Text.Json;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SkinAuthoring
{
    /// <summary>
    /// Editable geometric source for the two ordinary author packages. This is an offline asset recipe, never a game provider.
    /// </summary>
    internal static class SkinRecipe
    {
        private static readonly UTF8Encoding utf8 = new(false);
        private static readonly JsonSerializerOptions json_options = new() { WriteIndented = true };
        private static readonly string[] quiet_slots = { "effect.key-flash", "effect.hit-explosion", "playfield.lane-cover.decoration", "playfield.turntable", "playfield.laser", "bga.frame", "decoration" };

        public static void Generate(string root, AuthorProfile profile)
        {
            if (profile.Name.Contains('\n') || profile.Author.Contains('\n') || profile.Name.Contains('\r') || profile.Author.Contains('\r'))
                throw new InvalidDataException("作品名与作者名必须为单行文字。");
            Directory.CreateDirectory(root);
            Rgba32 background = colour(profile.Background);
            Rgba32 highlight = colour(profile.Highlight);
            foreach (string ruleset in new[] { "bms", "mania" })
            {
                Rgba32 accent = colour(ruleset == "bms" ? profile.BmsAccent : profile.ManiaAccent);
                string assets = Path.Combine(root, ruleset);
                Directory.CreateDirectory(assets);
                foreach (string role in new[] { "white", "accent", "special", "scratch" })
                {
                    Rgba32 roleColour = role switch { "white" => new Rgba32(234, 241, 247), "accent" => accent, "special" => highlight, _ => new Rgba32(251, 119, 127) };
                    foreach (string part in new[] { "note", "head", "body", "tail", "key", "pressed" })
                    {
                        draw(Path.Combine(assets, $"{part}-{role}.png"), 128, part == "body" ? 128 : part is "key" or "pressed" ? 72 : 24,
                            (x, y, w, h) => note(x, y, w, h, part, roleColour, background, profile.Complex, 0));
                        if (profile.Complex && part is "note" or "head" or "body" or "tail")
                            for (int frame = 0; frame < 12; frame++)
                            {
                                int current = frame;
                                draw(Path.Combine(assets, $"{part}-{role}-{frame}.png"), 128, part == "body" ? 128 : 24,
                                    (x, y, w, h) => note(x, y, w, h, part, roleColour, background, true, current));
                            }
                    }
                }

                foreach (string name in new[] { "lane", "divider", "target", "bar", "cover", "stage", "frame", "backdrop", "plate", "mine", "hud", "gauge", "flash", "explosion", "turntable", "laser", "viewport" })
                    draw(Path.Combine(assets, name + ".png"), name == "mine" ? 128 : 256, name is "target" or "bar" or "divider" ? 32 : 256,
                        (x, y, w, h) => surface(x, y, w, h, name, accent, background, highlight, profile.Complex));
            }

            Directory.CreateDirectory(Path.Combine(root, "scene"));
            draw(Path.Combine(root, "scene", "white.png"), 1, 1, (_, _, _, _) => new Rgba32(255, 255, 255));
            draw(Path.Combine(root, "scene", "orbit.png"), 256, 256, (x, y, w, h) =>
            {
                double dx = x - w / 2.0, dy = y - h / 2.0, radius = Math.Sqrt(dx * dx + dy * dy);
                double angle = Math.Atan2(dy, dx);
                return radius is > 101 and < 106 || radius is > 72 and < 75 && Math.Sin(angle * 8) > 0.2
                    ? new Rgba32(255, 255, 255, 210) : default;
            });
            draw(Path.Combine(root, "scene", "prism.png"), 128, 128, (x, y, w, h) =>
            {
                double d = Math.Abs(x - w / 2.0) + Math.Abs(y - h / 2.0);
                return d is > 42 and < 49 ? new Rgba32(255, 255, 255, 220) : default;
            });
            if (profile.Complex)
                draw(Path.Combine(root, "scene", "console.png"), 512, 96, (x, y, w, h) =>
                {
                    if (x + y < 12 || w - x + h - y < 12)
                        return default;
                    if (y < 2 || y >= h - 2)
                        return tint(highlight, 0.6, 230);
                    if (x < 3 || x >= w - 3 || x % 128 == 0)
                        return tint(highlight, 0.4, 180);
                    return tint(background, 1.2 + (1 - y / (double)h) * 0.6, 245);
                });
            writeIni(root, profile);
            writeScene(root, profile);
            writeSamples(root, profile.Complex);
        }

        private static void writeSamples(string root, bool complex)
        {
            // These are ordinary legacy sample names used by Mania's head/tail samples and
            // HitObject.CreateSlidingSamples(). BMS chart-owned keysounds keep their existing priority.
            string[] banks = { "normal", "soft", "drum" };
            string[] names = { "hitnormal", "hitclap", "hitfinish", "hitwhistle", "sliderslide", "sliderwhistle" };
            const int sample_rate = 44100;
            for (int bank = 0; bank < banks.Length; bank++)
                for (int sound = 0; sound < names.Length; sound++)
                {
                    string name = names[sound];
                    bool loop = name.StartsWith("slider", StringComparison.Ordinal);
                    double duration = loop ? 0.25 : name == "hitfinish" ? 0.2 : name == "hitwhistle" ? 0.12 : 0.075;
                    double frequency = banks[bank] switch { "soft" => 640, "drum" => 160, _ => 960 };
                    if (complex)
                        frequency *= 1.25;
                    int frames = (int)(sample_rate * duration);
                    uint noise = (uint)(0x61c88647 + bank * 31 + sound);
                    using var file = File.Create(Path.Combine(root, $"{banks[bank]}-{name}.wav"));
                    using var writer = new BinaryWriter(file, Encoding.ASCII);
                    writer.Write(Encoding.ASCII.GetBytes("RIFF"));
                    writer.Write(36 + frames * 2);
                    writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                    writer.Write(16);
                    writer.Write((short)1); // PCM
                    writer.Write((short)1); // Mono
                    writer.Write(sample_rate);
                    writer.Write(sample_rate * 2);
                    writer.Write((short)2);
                    writer.Write((short)16);
                    writer.Write(Encoding.ASCII.GetBytes("data"));
                    writer.Write(frames * 2);
                    for (int frame = 0; frame < frames; frame++)
                    {
                        double time = frame / (double)sample_rate;
                        double phase = 2 * Math.PI * frequency * time;
                        noise ^= noise << 13;
                        noise ^= noise >> 17;
                        noise ^= noise << 5;
                        double grain = (noise / (double)uint.MaxValue) * 2 - 1;
                        double tone = name switch
                        {
                            "hitclap" => grain * 0.75 + Math.Sin(phase) * 0.25,
                            "hitfinish" => Math.Sin(phase) * Math.Sin(phase * 1.417) * 0.7 + grain * 0.3,
                            "hitwhistle" => Math.Sin(phase * 2.5) * 0.7 + Math.Sin(phase * 5) * 0.3,
                            "sliderslide" => Math.Sin(phase) * 0.7 + Math.Sin(phase * 2) * 0.3,
                            "sliderwhistle" => Math.Sin(phase * 2) * 0.7 + Math.Sin(phase * 3) * 0.3,
                            _ => Math.Sin(phase * (1 + Math.Exp(-time * 110) * 0.6)) * 0.85 + grain * 0.15,
                        };
                        double envelope = loop ? 0.12 : Math.Min(1, time / 0.0015) * Math.Exp(-time * (name == "hitfinish" ? 26 : 65)) * 0.6;
                        // Loop tones have an integer number of cycles in 250 ms. Non-loop tails taper to silence.
                        if (!loop)
                            envelope *= Math.Min(1, (frames - 1 - frame) / (sample_rate * 0.004));
                        writer.Write((short)Math.Round(Math.Clamp(tone * envelope, -1, 1) * short.MaxValue));
                    }
                }
        }

        private static Rgba32 colour(string hex)
        {
            if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out uint value))
                throw new InvalidDataException("配色必须是六位十六进制，例如 58d3ea。");
            return new Rgba32((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        }

        private static Rgba32 tint(Rgba32 value, double intensity, byte alpha = 255)
            => new((byte)Math.Clamp(value.R * intensity, 0, 255), (byte)Math.Clamp(value.G * intensity, 0, 255), (byte)Math.Clamp(value.B * intensity, 0, 255), alpha);

        private static void draw(string path, int width, int height, Func<int, int, int, int, Rgba32> pixel)
        {
            using var image = new Image<Rgba32>(width, height);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    image[x, y] = pixel(x, y, width, height);
            image.SaveAsPng(path);
        }

        private static Rgba32 note(int x, int y, int w, int h, string part, Rgba32 accent, Rgba32 background, bool complex, int frame)
        {
            if (x < 3 || x >= w - 3)
                return default;
            if (part == "body")
            {
                if (x < 24 || x >= w - 24)
                    return default;
                if (x < 28 || x >= w - 28)
                    return tint(accent, 0.88, 240);
                double intensity = complex && ((x + y + frame * 3) % 36 < 5) ? 0.8 : 0.45;
                return tint(accent, intensity, 220);
            }

            if (part is "key" or "pressed")
            {
                if (complex)
                {
                    if (y < 5 || y is >= 12 and < 15 && x > 14 && x < w - 14)
                        return accent;
                    if (x < 9 || x >= w - 9)
                        return tint(accent, 0.3);
                    if (y > h - 15 && Math.Abs(x - w / 2) < 13)
                        return tint(accent, part == "pressed" ? 1 : 0.6);
                    return part == "pressed" ? tint(accent, 0.5) : tint(background, 1.5);
                }
                if (y < 5)
                    return accent;
                return part == "pressed" ? tint(accent, 0.58) : tint(background, complex ? 2.3 : 1.8);
            }

            if (y < 2 || y >= h - 2)
                return default;
            if (complex && (x < 10 && y < 10 - x || x >= w - 10 && y < x - (w - 10)))
                return default;
            if (y is >= 3 and < 6)
                return new Rgba32(255, 255, 255);
            if (y >= h - 5)
                return tint(accent, 0.45);
            if (complex && (x is >= 8 and < 12 || x > w - 13 && x <= w - 9))
                return tint(accent, 0.4);
            if (part == "tail" && y < h / 2)
                return default;
            if (part == "head" && y >= 9 && y <= 12 && x > 16 && x < w - 16)
                return tint(accent, 0.38);
            if (complex && Math.Abs(x - (frame * 11 % w)) < 5)
                return tint(accent, 1.25);
            return accent;
        }

        private static Rgba32 surface(int x, int y, int w, int h, string name, Rgba32 accent, Rgba32 background, Rgba32 highlight, bool complex)
        {
            bool edge = x < 3 || x >= w - 3 || y < 3 || y >= h - 3;
            switch (name)
            {
                case "lane":
                    if (complex && (y % 64 == 0 || x < 6 || x >= w - 6))
                        return tint(accent, 0.16);
                    return tint(background, 1.5 + x / (double)w * 0.3);
                case "divider": return x < 3 ? tint(accent, 0.4) : default;
                case "target": return y is >= 12 and <= 17 ? new Rgba32(244, 250, 255) : y is >= 8 and <= 20 ? tint(accent, 0.7, 100) : default;
                case "bar": return y < 2 ? new Rgba32(167, 184, 205, 130) : default;
                case "cover": return background;
                case "stage": return default;
                case "frame":
                    if (!complex)
                        return x < 3 || x >= w - 3 ? tint(accent, 0.3, 220) : default;
                    if (x < 4 || x >= w - 4)
                        return tint(accent, 0.8);
                    if (y < 6 || y >= h - 9)
                        return x % 32 < 24 ? tint(accent, 0.3) : tint(highlight, 0.75);
                    if ((x < 10 || x >= w - 10) && y % 32 < 3)
                        return highlight;
                    return default;
                case "backdrop": return default;
                case "plate": return tint(background, complex && (x + y) % 48 < 2 ? 1.8 : 0.7);
                case "viewport": return default;
                case "hud": return complex && edge ? tint(accent, 0.65, 150) : default;
                case "gauge": return edge ? tint(accent, 0.85) : tint(accent, 0.48);
                case "laser": return Math.Abs(x - w / 2) < 5 ? tint(highlight, 1, (byte)(200 * (1 - y / (double)h))) : default;
                case "flash": return tint(accent, 0.8, (byte)(150 * (1 - y / (double)h)));
                case "explosion":
                {
                    double d = Math.Sqrt(Math.Pow((x - w / 2.0) / w, 2) + Math.Pow((y - h / 2.0) / h, 2));
                    return d < 0.47 ? tint(accent, 1.2, (byte)(Math.Max(0, 1 - d * 2) * 170)) : default;
                }
                case "mine": return Math.Abs(x - y) < 8 || Math.Abs(x + y - w) < 8 ? new Rgba32(255, 97, 126) : default;
                case "turntable":
                {
                    double d = Math.Sqrt(Math.Pow(x - w / 2.0, 2) + Math.Pow(y - h / 2.0, 2));
                    return d is > 82 and < 89 || d < 10 ? highlight : default;
                }
                default: throw new ArgumentOutOfRangeException(nameof(name));
            }
        }

        private static void writeIni(string root, AuthorProfile profile)
        {
            var ini = new StringBuilder().AppendLine("// Generated from author.json by SkinAuthoring. Ordinary editable skin files; no private provider.")
                .AppendLine("[General]").AppendLine($"Name: {profile.Name}").AppendLine($"Author: {profile.Author}").AppendLine("Version: 2.7").AppendLine();
            foreach ((string mode, int keys) in new[] { ("5K", 5), ("7K", 7), ("9K", 9), ("9K_PMS", 9), ("14K", 14) })
            {
                ini.AppendLine("[Bms]").AppendLine($"Keymode: {mode}")
                    .AppendLine($"PlayfieldHeight: {(profile.Complex ? "0.90" : "0.92")}")
                    .AppendLine($"PlayfieldWidth: {(keys == 14 ? "0.66" : keys == 9 ? "0.4455" : keys == 7 ? "0.396" : "0.33")}")
                    .AppendLine("NormalLaneWidth: 1").AppendLine("ScratchLaneWidth: 1.5").AppendLine("ScratchLaneSpacing: 0.12")
                    .AppendLine($"LongNoteBodyWidth: {(profile.Complex ? "0.65" : "0.60")}").AppendLine("BarLineHeight: 2");
                IEnumerable<string> lanes = keys == 14 ? new[] { "S" }.Concat(Enumerable.Range(1, 14).Select(n => n.ToString())).Append("S2")
                    : keys == 9 ? Enumerable.Range(0, 9).Select(n => n.ToString()) : new[] { "S" }.Concat(Enumerable.Range(1, keys).Select(n => n.ToString()));
                foreach (string lane in lanes)
                {
                    string role = lane.StartsWith('S') ? "scratch" : int.Parse(lane) % 2 == 0 ? "accent" : "white";
                    appendLegacyNote(ini, lane, role, "bms");
                }
                ini.AppendLine();
            }

            for (int count = 1; count <= 20; count++)
            {
                ini.AppendLine("[Mania]").AppendLine($"Keys: {count}").AppendLine("HitPosition: 440").AppendLine("JudgementLine: 1")
                    .AppendLine("KeysUnderNotes: 1").AppendLine("UpsideDown: 0").AppendLine("NoteBodyStyle: 1")
                    .AppendLine($"ColumnWidth: {string.Join(',', Enumerable.Repeat(count > 10 ? 22 : 30, count))}")
                    .AppendLine($"ColumnSpacing: {string.Join(',', Enumerable.Repeat(0, count))}")
                    .AppendLine("StageLeft: mania/frame").AppendLine("StageRight: mania/frame")
                    .AppendLine("StageBottom: mania/plate").AppendLine("StageHint: mania/target")
                    .AppendLine("ColourBarline: 155,180,208,140");
                for (int lane = 0; lane < count; lane++)
                {
                    string role = count % 2 == 1 && lane == count / 2 ? "special" : Math.Min(lane, count - 1 - lane) % 2 == 0 ? "white" : "accent";
                    appendLegacyNote(ini, lane.ToString(), role, "mania");
                    ini.AppendLine($"Colour{lane + 1}: 12,18,27,255").AppendLine($"ColourLight{lane + 1}: 106,163,221,180");
                }
                ini.AppendLine();
            }

            // One Target line applies to every following entry. Grouping it once is equivalent to the
            // expanded catalog, while keeping the complete editable INI within the ordinary folder gate.
            var common = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
            var bmsOnly = new Dictionary<string, StringBuilder>(StringComparer.Ordinal);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Layout layout in layouts())
            {
                foreach (var slot in GameplaySkinSlotCatalog.All)
                {
                    if (layout.Ruleset == "mania" && slot.Id is "object.mine" or "playfield.turntable" or "playfield.laser" or "bga.viewport" or "bga.frame")
                        continue;
                    if (layout.Keymode.StartsWith("9k", StringComparison.Ordinal) && slot.Id is "playfield.turntable" or "playfield.laser")
                        continue;
                    var output = slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Bms ? bmsOnly : common;
                    foreach ((string target, string role) in targets(layout, slot))
                    {
                        if (!seen.Add(slot.Id + "\n" + target))
                            continue;
                        string operation = !profile.Complex && quiet_slots.Contains(slot.Id, StringComparer.Ordinal) ? "Suppress"
                            : $"Provide \"{layout.Ruleset}/{asset(slot.Id, role)}\"";
                        // The ordinary mania compatibility fields carry separate released/pressed images.
                        // A single public texture deliberately cannot invent a second image from its filename.
                        if (layout.Ruleset == "mania" && slot.Id == "playfield.key")
                            operation = "Inherit";
                        if (slot.Id == "decoration" && profile.Complex)
                            operation = "Provide \"bms/backdrop\"";
                        appendPublic(output, target, $"{slot.Id}: resource {operation}");
                    }
                    if (slot.Id == "hud.text")
                        for (int group = 0; group < layout.Groups; group++)
                        {
                            string id = layout.Ruleset == "bms" ? $"bms.group.deck-{group + 1}" : $"mania.group.stage-{group + 1}";
                            string target = $"Target: Stage ruleset={layout.Ruleset} keymode=any stage-mode={(group == 0 ? "any" : "dual")} group={id} group-logical={group} group-visual={group}";
                            if (seen.Add(slot.Id + "\n" + target))
                                appendPublic(output, target, "hud.text: resource Suppress");
                        }
                }
            }
            ini.AppendLine("[GameplaySkin.Common:1]");
            foreach ((string target, StringBuilder entries) in common)
                ini.AppendLine(target).Append(entries);
            ini.AppendLine().AppendLine("[GameplaySkin.Bms:1]");
            foreach ((string target, StringBuilder entries) in bmsOnly)
                ini.AppendLine(target).Append(entries);
            File.WriteAllText(Path.Combine(root, "skin.ini"), ini.ToString().Replace("\r\n", "\n", StringComparison.Ordinal), utf8);
        }

        private static void appendPublic(Dictionary<string, StringBuilder> section, string target, string entry)
        {
            if (!section.TryGetValue(target, out StringBuilder? entries))
                section.Add(target, entries = new StringBuilder());
            entries.AppendLine(entry);
        }

        private static void appendLegacyNote(StringBuilder ini, string lane, string role, string ruleset)
        {
            foreach ((string suffix, string part) in new[] { ("", "note"), ("H", "head"), ("L", "body"), ("T", "tail") })
                ini.AppendLine($"NoteImage{lane}{suffix}: {ruleset}/{part}-{role}");
            ini.AppendLine($"KeyImage{lane}: {ruleset}/key-{role}").AppendLine($"KeyImage{lane}D: {ruleset}/pressed-{role}");
        }

        private static string asset(string id, string role) => id switch
        {
            "object.note" => "note-" + role,
            "object.long-note.head" => "head-" + role,
            "object.long-note.body" => "body-" + role,
            "object.long-note.tail" => "tail-" + role,
            "playfield.key" => "key-" + role,
            "object.mine" => "mine",
            "playfield.lane-surface" => "lane",
            "playfield.lane-divider" => "divider",
            "playfield.judgement-line" or "playfield.hit-target" => "target",
            "playfield.bar-line" => "bar",
            "playfield.lane-cover.fill" => "cover",
            "stage.background" => "stage",
            "stage.foreground" => "frame",
            "playfield.backdrop" => "backdrop",
            "playfield.baseplate" => "plate",
            "hud.gauge" => "gauge",
            "effect.key-flash" => "flash",
            "effect.hit-explosion" => "explosion",
            "playfield.turntable" => "turntable",
            "playfield.laser" => "laser",
            "bga.viewport" => "viewport",
            "bga.frame" or "playfield.lane-cover.decoration" => "frame",
            _ => "hud",
        };

        private sealed record Lane(int Group, int Logical, int Visual, int LocalLogical, int LocalVisual, string Id, string Role);
        private sealed record Layout(string Ruleset, string Keymode, int Groups, IReadOnlyList<Lane> Lanes);

        private static IEnumerable<Layout> layouts()
        {
            foreach ((string mode, int keys) in new[] { ("5k", 5), ("7k", 7), ("9k-bms", 9), ("9k-pms", 9), ("14k", 14) })
            {
                foreach (bool right in keys is 5 or 7 ? new[] { false, true } : new[] { false })
                {
                    var lanes = new List<Lane>();
                    int total = keys == 14 ? 16 : keys == 9 ? 9 : keys + 1;
                    for (int index = 0; index < total; index++)
                    {
                        bool scratch = keys != 9 && (index == 0 || keys == 14 && index == 15);
                        int group = keys == 14 && index >= 8 ? 1 : 0;
                        int visual = right ? index == 0 ? total - 1 : index - 1 : index;
                        int key = keys == 9 ? index + 1 : index;
                        string role = scratch ? "scratch" : key % 2 == 0 ? "accent" : "white";
                        lanes.Add(new Lane(group, index, visual, index - group * 8, visual - group * 8,
                            scratch ? $"bms.lane.scratch-{(index == 15 ? 2 : 1)}" : $"bms.lane.key-{key}", role));
                    }
                    yield return new Layout("bms", mode, keys == 14 ? 2 : 1, lanes);
                }
            }

            for (int left = 1; left <= 10; left++)
                for (int right = 0; right <= 10; right++)
                {
                    var lanes = new List<Lane>();
                    int global = 0;
                    int groups = right == 0 ? 1 : 2;
                    for (int group = 0; group < groups; group++)
                    {
                        int count = group == 0 ? left : right;
                        for (int local = 0; local < count; local++, global++)
                        {
                            string role = count % 2 == 1 && local == count / 2 ? "special" : Math.Min(local, count - 1 - local) % 2 == 0 ? "white" : "accent";
                            lanes.Add(new Lane(group, global, global, local, local, $"mania.lane.column-{global + 1}", role));
                        }
                    }
                    yield return new Layout("mania", right == 0 ? $"{left}k" : $"{left}k-{right}k", groups, lanes);
                }
        }

        private static IEnumerable<(string Target, string Role)> targets(Layout layout, GameplaySkinSlotDescriptor slot)
        {
            string context = $"ruleset={layout.Ruleset} keymode={layout.Keymode} stage-mode={(layout.Groups == 1 ? "single" : "dual")}";
            if ((slot.AllowedScopes & GameplaySkinSlotScope.Global) != 0)
            {
                // Shared free decoration is intentionally portable and declared once; per-ruleset textures remain in other slots.
                yield return (slot.Id == "decoration" ? "Target: Global ruleset=any keymode=any stage-mode=any" : $"Target: Global ruleset={layout.Ruleset} keymode=any stage-mode=any", "white");
                yield break;
            }
            for (int group = 0; group < layout.Groups; group++)
            {
                string id = layout.Ruleset == "bms" ? $"bms.group.deck-{group + 1}" : $"mania.group.stage-{group + 1}";
                string groupFields = $"group={id} group-logical={group} group-visual={group}";
                string sharedContext = $"ruleset={layout.Ruleset} keymode=any stage-mode={(group == 0 ? "any" : "dual")}";
                if ((slot.AllowedScopes & GameplaySkinSlotScope.Stage) != 0)
                    yield return ($"Target: Stage {sharedContext} {groupFields}", "white");
                else if ((slot.AllowedScopes & GameplaySkinSlotScope.Group) != 0)
                    yield return ($"Target: Group {sharedContext} {groupFields}", "white");
                else
                    foreach (Lane lane in layout.Lanes.Where(lane => lane.Group == group))
                    {
                        if (slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Bms && lane.Role != "scratch")
                            continue;
                        yield return ($"Target: Lane {context} {groupFields} lane={lane.Id} global-logical={lane.Logical} global-visual={lane.Visual} group-local-logical={lane.LocalLogical} group-local-visual={lane.LocalVisual}", lane.Role);
                    }
            }
        }

        private static void writeScene(string root, AuthorProfile profile)
        {
            var manifest = new Dictionary<string, object>
            {
                ["contract"] = GameplaySkinSceneContracts.MANIFEST_CONTRACT_ID,
                ["scene"] = GameplaySkinSceneContracts.SCENE_FILE_NAME,
                ["sceneContract"] = GameplaySkinSceneContracts.SCENE_CONTRACT_ID,
                ["eventContract"] = GameplaySkinSceneContracts.EVENT_CONTRACT_ID,
                ["resources"] = (profile.Complex ? new[] { "white", "orbit", "prism", "console" } : new[] { "white" })
                    .Select(id => new { id = "texture." + id, type = "texture", path = "scene/" + id + ".png" }).ToArray(),
            };
            var children = new List<object>();
            var tracks = new List<object>();
            var bindings = new List<object>();
            var machines = new List<object>();
            if (!profile.Complex)
            {
                var information = new List<object>
                {
                    sprite("still.hud.panel", "white", new() { ["x"] = 0.18, ["y"] = 0.006, ["width"] = 0.64, ["height"] = 0.061, ["colour"] = "#" + profile.Background + "ee" }),
                    label("still.hud.score-label", "SCORE", 0.195, 0.013, 10, "#91a1b9ff"),
                    label("still.hud.score", "0", 0.195, 0.035, 20, "#e9f3ffff"),
                    label("still.hud.accuracy-label", "ACCURACY", 0.40, 0.013, 10, "#91a1b9ff"),
                    label("still.hud.accuracy", "100.00%", 0.40, 0.035, 20, "#e9f3ffff"),
                    label("still.hud.combo-label", "COMBO", 0.585, 0.013, 10, "#91a1b9ff"),
                    label("still.hud.combo", "0", 0.585, 0.035, 20, "#e9f3ffff"),
                    label("still.hud.bpm-label", "BPM", 0.715, 0.013, 10, "#91a1b9ff"),
                    label("still.hud.bpm", "0", 0.715, 0.035, 20, "#e9f3ffff"),
                    sprite("still.hud.progress-track", "white", new() { ["x"] = 0.18, ["y"] = 0.069, ["width"] = 0.64, ["height"] = 0.002, ["colour"] = "#263444ff" }),
                    sprite("still.hud.progress", "white", new() { ["x"] = 0.18, ["y"] = 0.069, ["width"] = 0.64, ["height"] = 0.002, ["scale-x"] = 0, ["colour"] = "#a6b5c9ff" }),
                };
                children.Add(new { id = "still.hud", type = "container", target = new { kind = "global" }, slot = "hud.text", blend = "alpha", properties = new { }, effects = Array.Empty<object>(), children = information });
                foreach ((string target, string property, string source) in new[]
                         {
                             ("score", "text", "score.value"), ("accuracy", "text", "score.accuracy"), ("combo", "text", "combo.value"),
                             ("bpm", "text", "timing.bpm"), ("progress", "scale-x", "timing.progress"),
                         })
                    bindings.Add(new { id = "still.bind." + target, target = "still.hud." + target, property, source });
            }
            if (profile.Complex)
            {
                manifest["script"] = GameplaySkinSceneContracts.SCRIPT_FILE_NAME;
                var console = new List<object>
                {
                    sprite("astral.console.panel", "console", new() { ["x"] = 0.15, ["y"] = 0.012, ["width"] = 0.70, ["height"] = 0.058 }),
                    label("astral.console.brand", "A S T R A L", 0.166, 0.022, 18, "#" + profile.Highlight + "ff"),
                    label("astral.console.status", "READY", 0.166, 0.049, 10, "#a6b5c9ff"),
                    label("astral.console.score-label", "SCORE", 0.315, 0.021, 10, "#91a1b9ff"),
                    label("astral.console.score", "0", 0.315, 0.037, 20, "#e9f3ffff"),
                    label("astral.console.accuracy-label", "ACCURACY", 0.475, 0.021, 10, "#91a1b9ff"),
                    label("astral.console.accuracy", "100.00%", 0.475, 0.039, 16, "#e9f3ffff"),
                    label("astral.console.combo-label", "COMBO", 0.61, 0.021, 10, "#91a1b9ff"),
                    label("astral.console.combo", "0", 0.61, 0.037, 20, "#" + profile.Highlight + "ff"),
                    label("astral.console.bpm-label", "BPM", 0.705, 0.021, 10, "#91a1b9ff"),
                    label("astral.console.bpm", "0", 0.705, 0.04, 16, "#c0cee0ff"),
                    label("astral.console.judgement", "", 0.785, 0.043, 12, "#" + profile.BmsAccent + "ff"),
                    sprite("astral.console.progress-track", "white", new() { ["x"] = 0.15, ["y"] = 0.071, ["width"] = 0.70, ["height"] = 0.0015, ["colour"] = "#18283aff" }),
                    sprite("astral.console.progress", "white", new() { ["x"] = 0.15, ["y"] = 0.071, ["width"] = 0.70, ["height"] = 0.0015, ["scale-x"] = 0, ["colour"] = "#" + profile.Highlight + "ff" }),
                    sprite("astral.console.energy-track", "white", new() { ["x"] = 0.15, ["y"] = 0.074, ["width"] = 0.70, ["height"] = 0.002, ["colour"] = "#18283aff" }),
                    sprite("astral.console.energy", "white", new() { ["x"] = 0.15, ["y"] = 0.074, ["width"] = 0.70, ["height"] = 0.002, ["scale-x"] = 0, ["colour"] = "#" + profile.BmsAccent + "ff" }),
                };
                children.Add(new { id = "astral.console", type = "container", target = new { kind = "global" }, slot = "hud.text", blend = "alpha", properties = new { }, effects = Array.Empty<object>(), children = console });
                foreach ((string target, string property, string source) in new[]
                         {
                             ("score", "text", "score.value"), ("combo", "text", "combo.value"), ("bpm", "text", "timing.bpm"),
                             ("accuracy", "text", "score.accuracy"), ("progress", "scale-x", "timing.progress"),
                             ("judgement", "text", "judgement.result"), ("energy", "scale-x", "gauge.value"),
                         })
                    bindings.Add(new { id = "astral.bind." + target, target = "astral.console." + target, property, source });
                string[] stateNames = { "ready", "running", "paused", "complete", "failed" };
                var states = stateNames.Select(name => new { id = "astral.state." + name, set = new[] { new { id = "astral.state-set." + name, target = "astral.console.status", property = "text", value = name.ToUpperInvariant() } } }).ToArray();
                var transitions = stateNames.SelectMany(from => new[] { ("running", "gameplay.start"), ("paused", "gameplay.pause"), ("complete", "gameplay.complete"), ("failed", "gameplay.fail") }
                    .Where(pair => pair.Item1 != from).Select(pair => new { id = "astral.transition." + from + "." + pair.Item1, from = "astral.state." + from, to = "astral.state." + pair.Item1, @event = pair.Item2 })).ToArray();
                machines.Add(new { id = "astral.lifecycle", initial = "astral.state.ready", states, transitions });
                var decorations = new List<object>
                {
                    sprite("astral.rail.top", "white", new() { ["x"] = 0.02, ["y"] = 0.025, ["width"] = 0.96, ["height"] = 0.002, ["opacity"] = 0.5, ["colour"] = "#" + profile.BmsAccent + "ff" }),
                    sprite("astral.rail.bottom", "white", new() { ["x"] = 0.02, ["y"] = 0.975, ["width"] = 0.96, ["height"] = 0.002, ["opacity"] = 0.5, ["colour"] = "#" + profile.Highlight + "ff" })
                };
                for (int side = 0; side < 2; side++)
                {
                    double x = side == 0 ? 0.035 : 0.965;
                    decorations.Add(sprite($"astral.orbit-{side}", "orbit", new() { ["x"] = x, ["y"] = 0.5, ["width"] = 0.058, ["height"] = 0.105, ["origin"] = "centre", ["opacity"] = 0.3, ["colour"] = "#" + (side == 0 ? profile.BmsAccent : profile.ManiaAccent) + "ff" }));
                    decorations.Add(sprite($"astral.prism-{side}", "prism", new() { ["x"] = x, ["y"] = 0.5, ["width"] = 0.025, ["height"] = 0.045, ["origin"] = "centre", ["opacity"] = 0.4, ["colour"] = "#" + profile.Highlight + "ff" }));
                    tracks.Add(track($"astral.rotate-{side}", $"astral.orbit-{side}", "rotation", 0, side == 0 ? 360 : -360, 12000));
                    for (int bar = 0; bar < 4; bar++)
                        decorations.Add(sprite($"astral.meter-{side}-{bar}", "white", new() { ["x"] = x - 0.014 + bar * 0.008, ["y"] = 0.66, ["width"] = 0.004, ["height"] = 0.13, ["origin"] = "bottom-centre", ["scale-y"] = 0.15, ["opacity"] = 0.4, ["colour"] = "#" + profile.BmsAccent + "ff" }));
                }
                children.Add(new { id = "astral.decoration", type = "container", target = new { kind = "global" }, slot = "decoration", blend = "alpha", properties = new { }, effects = Array.Empty<object>(), children = decorations });
            }
            var scene = new { contract = GameplaySkinSceneContracts.SCENE_CONTRACT_ID, root = new { id = "skin.root", type = "container", target = new { kind = "global" }, blend = "inherit", properties = new { }, effects = Array.Empty<object>(), children }, tracks, stateMachines = machines, bindings, variants = Array.Empty<object>(), templates = Array.Empty<object>(), instances = Array.Empty<object>() };
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.MANIFEST_FILE_NAME), JsonSerializer.Serialize(manifest, json_options) + "\n", utf8);
            File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCENE_FILE_NAME), JsonSerializer.Serialize(scene, json_options) + "\n", utf8);
            if (profile.Complex)
                File.WriteAllText(Path.Combine(root, GameplaySkinSceneContracts.SCRIPT_FILE_NAME), script(), utf8);
        }

        private static object sprite(string id, string resource, Dictionary<string, object> properties)
            => new { id, type = "sprite", target = new { kind = "global" }, resource = "texture." + resource, blend = "alpha", properties, effects = Array.Empty<object>(), children = Array.Empty<object>() };

        private static object label(string id, string text, double x, double y, int fontSize, string colour)
            => new { id, type = "text", target = new { kind = "global" }, blend = "alpha", properties = new Dictionary<string, object> { ["text"] = text, ["x"] = x, ["y"] = y, ["font-size"] = fontSize, ["colour"] = colour }, effects = Array.Empty<object>(), children = Array.Empty<object>() };

        private static object track(string id, string target, string property, double from, double to, int duration)
            => new { id, type = "tween", target, property, easing = "linear", loop = true, keyframes = new[] { new { id = id + ".start", time = 0, value = from }, new { id = id + ".end", time = duration, value = to } } };

        private static string script()
        {
            var source = new StringBuilder("oms-script 1\n# Eight real judgement intervals combine with gauge and recent activity.\nrequired gameplay.snapshot.read\nrequired gameplay.events.read\nrequired scene.numeric.write\ndeny network.read\n");
            foreach ((string name, double value) in new[] { ("kind", 0d), ("test", 0d), ("now", 0d), ("previous", 0d), ("interval", 0d), ("old", 0d), ("sum", 8000d), ("index", 0d), ("count", 0d), ("energy", 0d), ("gauge", 0d), ("scale", 1d), ("alpha", 0.25d), ("age", 0d), ("level", 0d) })
                source.AppendLine($"state {name} {value.ToString(CultureInfo.InvariantCulture)}");
            source.AppendLine("heap 8");
            for (int side = 0; side < 2; side++)
            {
                source.AppendLine($"target astral.prism-{side} scale-x").AppendLine($"target astral.prism-{side} scale-y").AppendLine($"target astral.orbit-{side} alpha");
                for (int bar = 0; bar < 4; bar++)
                    source.AppendLine($"target astral.meter-{side}-{bar} scale-y");
            }
            source.Append("read now time\nread kind event-kind\neq test kind 40\nwhen test judgement\neq test kind -1\nwhen test render\nhalt\njudgement:\nsub interval now previous\nclamp interval interval 30 1000\nmov previous now\nload old index\nlt test count 8\nwhen test warming\nsub sum sum old\njump insert\nwarming:\nsub sum sum 1000\nadd count count 1\ninsert:\nadd sum sum interval\nstore index interval\nadd index index 1\neq test index 8\nwhen test wrap\njump charge\nwrap:\nmov index 0\ncharge:\ndiv energy 1800 sum\nclamp energy energy 0 1\nhalt\nrender:\nsub age now previous\ndiv age age 5000\nsub level energy age\nclamp level level 0 1\nread gauge gauge\nmul scale level 0.7\nadd scale scale 1\nmul alpha gauge 0.3\nadd alpha alpha 0.25\n");
            for (int side = 0; side < 2; side++)
                source.AppendLine($"set astral.prism-{side} scale-x scale").AppendLine($"set astral.prism-{side} scale-y scale").AppendLine($"set astral.orbit-{side} alpha alpha");
            for (int bar = 0; bar < 4; bar++)
            {
                source.AppendLine($"sub scale level {(bar * 0.16).ToString(CultureInfo.InvariantCulture)}").AppendLine("clamp scale scale 0.12 1");
                for (int side = 0; side < 2; side++)
                    source.AppendLine($"set astral.meter-{side}-{bar} scale-y scale");
            }
            return source.AppendLine("halt").ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        }
    }
}
