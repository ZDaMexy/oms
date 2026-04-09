// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public static class BmsImportExtensions
    {
        public static readonly string[] BeatmapFileExtensions =
        {
            ".bms",
            ".bme",
            ".bml",
            ".pms",
        };

        public static readonly string[] ArchiveExtensions =
        {
            ".zip",
            ".rar",
            ".7z",
        };

        public static IEnumerable<string> SupportedImportExtensions => ArchiveExtensions.Concat(BeatmapFileExtensions);

        public static bool IsBeatmapFile(string path)
            => hasExtension(path, BeatmapFileExtensions);

        public static bool IsArchiveFile(string path)
            => hasExtension(path, ArchiveExtensions);

        private static bool hasExtension(string path, IEnumerable<string> extensions)
        {
            string extension = Path.GetExtension(path);
            return extensions.Any(candidate => extension.Equals(candidate, StringComparison.OrdinalIgnoreCase));
        }
    }
}
