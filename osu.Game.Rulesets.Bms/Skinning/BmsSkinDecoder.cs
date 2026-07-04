// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using osu.Game.Rulesets.Bms.Difficulty;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Parses the BMS-specific sections (<c>[General]</c> + per-keymode <c>[Bms]</c>) of a skin.ini into
    /// <see cref="BmsSkinConfiguration"/>s. Standalone by design: it does NOT extend the core
    /// <see cref="osu.Game.Beatmaps.Formats.LegacyDecoder{T}"/> (whose <c>Section</c> enum has no <c>Bms</c> entry),
    /// so all BMS skin parsing stays inside the ruleset and never touches osu.Game.
    /// </summary>
    /// <remarks>
    /// Fail-open per the F1 contract: unknown keys, unparseable keymodes and malformed values are silently skipped
    /// (a skin may carry forward-compatible keys without breaking), never thrown. Comments use <c>//</c> to match the
    /// osu/mania skin.ini convention (a BMS skin shares one skin.ini with the mania sections under scheme A).
    /// </remarks>
    public class BmsSkinDecoder
    {
        /// <summary>Keymodes declared in <c>[General] Keymodes:</c> (informational — editor hints, not load-time gating).</summary>
        public IReadOnlyList<BmsKeymode> DeclaredKeymodes => declaredKeymodes;

        /// <summary>One configuration per distinct keymode <c>[Bms]</c> section encountered.</summary>
        public IReadOnlyList<BmsSkinConfiguration> Configurations => configurations;

        private readonly List<BmsKeymode> declaredKeymodes = new List<BmsKeymode>();
        private readonly List<BmsSkinConfiguration> configurations = new List<BmsSkinConfiguration>();

        // Per-lane texture keys: NoteImage{lane}[H|L|T] / KeyImage{lane}[D] / LaneBackgroundImage{lane} /
        // LaneDividerImage{lane} / KeyFlashImage{lane} / HitLightingImage{lane}, lane = digits, "S" (P1 scratch), or "S2" (14K DP P2 scratch).
        // The optional [HLTD] suffix is only meaningful for NoteImage (H/L/T) and KeyImage (D).
        private static readonly Regex per_lane_image = new Regex(@"^(NoteImage|KeyImage|LaneBackgroundImage|LaneDividerImage|KeyFlashImage|HitLightingImage|HoldLightImage|MineHitImage)(\d+|S2?)([HLTD])?$", RegexOptions.Compiled);

        private static readonly HashSet<BmsSkinConfigurationLookups> geometry_keys = new HashSet<BmsSkinConfigurationLookups>
        {
            BmsSkinConfigurationLookups.PlayfieldWidth,
            BmsSkinConfigurationLookups.PlayfieldHeight,
            BmsSkinConfigurationLookups.NormalLaneWidth,
            BmsSkinConfigurationLookups.ScratchLaneWidth,
            BmsSkinConfigurationLookups.NormalLaneSpacing,
            BmsSkinConfigurationLookups.ScratchLaneSpacing,
            BmsSkinConfigurationLookups.HitTargetHeight,
            BmsSkinConfigurationLookups.HitTargetBarHeight,
            BmsSkinConfigurationLookups.HitTargetLineHeight,
            BmsSkinConfigurationLookups.HitTargetGlowRadius,
            BmsSkinConfigurationLookups.BarLineHeight,
            BmsSkinConfigurationLookups.LongNoteBodyWidth,
        };

        public void Parse(string skinIni)
        {
            using var reader = new StringReader(skinIni);
            Parse(reader);
        }

        public void Parse(TextReader reader)
        {
            string? section = null;
            BmsSkinConfiguration? current = null;
            var pending = new List<KeyValuePair<string, string>>();

            string? line;

            while ((line = reader.ReadLine()) != null)
            {
                line = stripComment(line).Trim();

                if (line.Length == 0)
                    continue;

                if (line[0] == '[' && line[^1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    current = null;
                    pending.Clear();
                    continue;
                }

                if (!trySplit(line, out string key, out string value))
                    continue;

                switch (section)
                {
                    case "General":
                        if (key.Equals("Keymodes", StringComparison.OrdinalIgnoreCase))
                            parseDeclaredKeymodes(value);

                        break;

                    case "Bms":
                        if (key.Equals("Keymode", StringComparison.OrdinalIgnoreCase))
                        {
                            current = tryParseKeymode(value, out var keymode) ? getOrAddConfiguration(keymode) : null;

                            // Any keys seen before the Keymode line can now be flushed into the resolved bucket.
                            if (current != null)
                            {
                                foreach (var pair in pending)
                                    applyKey(current, pair.Key, pair.Value);
                            }

                            pending.Clear();
                        }
                        else if (current != null)
                            applyKey(current, key, value);
                        else
                            pending.Add(new KeyValuePair<string, string>(key, value)); // hold until a Keymode line appears

                        break;
                }
            }
        }

        private BmsSkinConfiguration getOrAddConfiguration(BmsKeymode keymode)
        {
            foreach (var config in configurations)
            {
                if (config.Keymode == keymode)
                    return config; // silently merge duplicate sections (matches mania)
            }

            var added = new BmsSkinConfiguration(keymode);
            configurations.Add(added);
            return added;
        }

        private static void applyKey(BmsSkinConfiguration config, string key, string value)
        {
            // Per-lane texture slots keep their full ini key (lane token embedded), matching mania's ImageLookups.
            if (per_lane_image.IsMatch(key))
            {
                config.ImageLookups[key] = value;
                return;
            }

            if (key.Length == 0 || !char.IsLetter(key[0])
                                || !Enum.TryParse(key, out BmsSkinConfigurationLookups lookup)
                                || !Enum.IsDefined(lookup))
                return; // unknown key — ignore (fail-open, forward-compatible)

            if (geometry_keys.Contains(lookup))
            {
                if (tryParseFloat(value, out float number))
                    config.Geometry[lookup] = number;
            }
            else if (key.Contains("Colour", StringComparison.Ordinal))
            {
                if (tryParseColour(value, out var colour))
                    config.Colours[lookup] = colour;
            }
            else if (key.EndsWith("Image", StringComparison.Ordinal))
            {
                config.ImageLookups[key] = value; // global texture slot (HitTargetImage / StageLeftImage / ...)
            }
        }

        private void parseDeclaredKeymodes(string value)
        {
            foreach (string token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (tryParseKeymode(token, out var keymode) && !declaredKeymodes.Contains(keymode))
                    declaredKeymodes.Add(keymode);
            }
        }

        private static bool tryParseKeymode(string value, out BmsKeymode keymode)
        {
            switch (value.Trim().ToUpperInvariant())
            {
                case "5K":
                    keymode = BmsKeymode.Key5K;
                    return true;

                case "7K":
                    keymode = BmsKeymode.Key7K;
                    return true;

                case "9K":
                case "9K_BMS":
                    keymode = BmsKeymode.Key9K_Bms;
                    return true;

                case "9K_PMS":
                case "PMS":
                    keymode = BmsKeymode.Key9K_Pms;
                    return true;

                case "14K":
                    keymode = BmsKeymode.Key14K;
                    return true;

                default:
                    keymode = default;
                    return false;
            }
        }

        private static bool trySplit(string line, out string key, out string value)
        {
            int separator = line.IndexOf(':');

            if (separator < 0)
            {
                key = string.Empty;
                value = string.Empty;
                return false;
            }

            key = line.Substring(0, separator).Trim();
            value = line.Substring(separator + 1).Trim();
            return key.Length > 0;
        }

        private static string stripComment(string line)
        {
            int index = line.IndexOf("//", StringComparison.Ordinal);
            return index >= 0 ? line.Substring(0, index) : line;
        }

        private static bool tryParseFloat(string value, out float number)
            => float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number);

        private static bool tryParseColour(string value, out Color4 colour)
        {
            colour = default;

            string[] parts = value.Split(',', StringSplitOptions.TrimEntries);

            if (parts.Length < 3 || parts.Length > 4)
                return false;

            if (!byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte r)
                || !byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte g)
                || !byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out byte b))
                return false;

            byte a = 255;

            if (parts.Length == 4 && !byte.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out a))
                return false;

            colour = new Color4(r, g, b, a);
            return true;
        }
    }
}
