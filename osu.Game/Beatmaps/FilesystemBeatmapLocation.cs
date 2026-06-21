// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Resolves the on-disk location of filesystem-backed (folder) beatmaps — BMS (chartbms/) and direct-read
    /// mania (chartmania/) — and reveals them in the OS file browser. Hash-backed beatmaps (the hash-keyed files/
    /// store) have no browsable folder, so the helpers return false / no menu item for them.
    /// </summary>
    public static class FilesystemBeatmapLocation
    {
        /// <summary>
        /// Whether the set is read directly from a real folder on disk (and therefore can be revealed in the file browser).
        /// </summary>
        public static bool IsFilesystemBacked(BeatmapSetInfo? beatmapSet)
            => !string.IsNullOrEmpty(beatmapSet?.FilesystemStoragePath);

        /// <summary>
        /// Resolves the absolute directory of a filesystem-backed set. External libraries store an absolute
        /// <see cref="BeatmapSetInfo.FilesystemStoragePath"/>; internal managed sets store one relative to the data root.
        /// </summary>
        public static bool TryGetSetDirectory(BeatmapSetInfo? beatmapSet, Storage storage, out string absolutePath)
        {
            absolutePath = string.Empty;

            if (beatmapSet == null || string.IsNullOrEmpty(beatmapSet.FilesystemStoragePath))
                return false;

            absolutePath = beatmapSet.IsExternalFilesystemStorage
                ? beatmapSet.FilesystemStoragePath
                : storage.GetFullPath(beatmapSet.FilesystemStoragePath);

            return true;
        }

        /// <summary>
        /// Resolves the absolute path of a filesystem-backed beatmap's chart file (its set directory joined with the
        /// beatmap's local file path).
        /// </summary>
        public static bool TryGetBeatmapFile(BeatmapInfo? beatmap, Storage storage, out string absolutePath)
        {
            absolutePath = string.Empty;

            if (beatmap == null || string.IsNullOrEmpty(beatmap.LocalFilePath))
                return false;

            if (!TryGetSetDirectory(beatmap.BeatmapSet, storage, out string setDirectory))
                return false;

            absolutePath = Path.Combine(setDirectory, beatmap.LocalFilePath.Replace('/', Path.DirectorySeparatorChar));
            return true;
        }

        /// <summary>
        /// Reveals an absolute path in the OS file browser, opening its parent folder and selecting the item. Goes
        /// straight through <see cref="GameHost"/> rather than <see cref="Storage.PresentFileExternally"/> because
        /// external-library paths live outside the data-storage root (which would reject them). Falls back to opening
        /// the parent folder if the exact target is gone (e.g. an external library was moved), and no-ops otherwise.
        /// External directories are only ever read here, never mutated.
        /// </summary>
        public static void Reveal(GameHost host, string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return;

            if (File.Exists(absolutePath) || Directory.Exists(absolutePath))
            {
                host.PresentFileExternally(absolutePath);
                return;
            }

            string? parent = Path.GetDirectoryName(absolutePath);

            if (!string.IsNullOrEmpty(parent) && Directory.Exists(parent))
                host.OpenFileExternally(parent + Path.DirectorySeparatorChar);
        }

        /// <summary>
        /// Builds the "open song folder" context-menu item for a set, or null if the set is not filesystem-backed.
        /// Reveals (selects) the song folder within its parent directory.
        /// </summary>
        public static OsuMenuItem? CreateOpenSongFolderItem(BeatmapSetInfo? beatmapSet, Storage storage, GameHost host)
        {
            if (!TryGetSetDirectory(beatmapSet, storage, out string path))
                return null;

            return new OsuMenuItem("打开歌曲文件位置", MenuItemType.Standard, () => Reveal(host, path))
            {
                Icon = FontAwesome.Solid.FolderOpen
            };
        }

        /// <summary>
        /// Builds the "open chart file" context-menu item for a beatmap, or null if it is not filesystem-backed.
        /// Reveals (selects) the chart file within its song folder.
        /// </summary>
        public static OsuMenuItem? CreateOpenChartFileItem(BeatmapInfo? beatmap, Storage storage, GameHost host)
        {
            if (!TryGetBeatmapFile(beatmap, storage, out string path))
                return null;

            return new OsuMenuItem("打开谱面文件位置", MenuItemType.Standard, () => Reveal(host, path))
            {
                Icon = FontAwesome.Regular.File
            };
        }
    }
}
