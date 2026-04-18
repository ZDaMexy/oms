// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using osu.Game.IO;

namespace osu.Game.Beatmaps.Formats
{
    /// <summary>
    /// Lightweight parser for determining the legacy Mode of an .osu file without invoking a full beatmap decoder.
    /// </summary>
    public static class OsuFileModeDetector
    {
        private const int default_mode = 0;
        private const int mania_mode = 3;

        public static bool IsMania(Stream stream) => IsMode(stream, mania_mode);

        public static bool IsMode(Stream stream, int expectedMode)
        {
            TryReadMode(stream, out int mode);
            return mode == expectedMode;
        }

        public static bool TryReadMode(Stream stream, out int mode)
        {
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            using var reader = new LineBufferedReader(stream, true);

            string? currentSection = null;

            while (reader.PeekLine() != null)
            {
                string? rawLine = reader.ReadLine();

                if (rawLine == null)
                    break;

                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    currentSection = line;

                    if (!string.Equals(currentSection, "[General]", StringComparison.OrdinalIgnoreCase))
                        break;

                    continue;
                }

                if (!string.Equals(currentSection, "[General]", StringComparison.OrdinalIgnoreCase))
                    continue;

                const string mode_prefix = "Mode:";

                if (!line.StartsWith(mode_prefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (int.TryParse(line[mode_prefix.Length..].Trim(), out mode))
                    return true;

                break;
            }

            mode = default_mode;
            return false;
        }
    }
}
