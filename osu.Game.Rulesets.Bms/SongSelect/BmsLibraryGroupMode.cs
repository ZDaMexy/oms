// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Extensions;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Bms.SongSelect
{
    public static class BmsLibraryGroupMode
    {
        private const string internal_root_key = "internal-root";
        private const string unmapped_external_root_key = "external-root:unmapped";

        public static IEnumerable<GroupDefinition> GetGroupDefinitions(GroupMode mode, IBeatmapInfo beatmapInfo)
        {
            if (beatmapInfo is not BeatmapInfo beatmap || beatmap.BeatmapSet == null || string.IsNullOrEmpty(beatmap.BeatmapSet.FilesystemStoragePath))
                return Array.Empty<GroupDefinition>();

            return mode switch
            {
                GroupMode.InternalLibrary => getInternalGroupDefinitions(beatmap.BeatmapSet),
                GroupMode.ExternalLibrary => getExternalGroupDefinitions(beatmap.BeatmapSet),
                _ => Array.Empty<GroupDefinition>(),
            };
        }

        private static IEnumerable<GroupDefinition> getInternalGroupDefinitions(BeatmapSetInfo beatmapSet)
        {
            if (beatmapSet.IsExternalFilesystemStorage)
                return Array.Empty<GroupDefinition>();

            string managedPath = beatmapSet.FilesystemStoragePath!.ToStandardisedPath();
            string managedRootPrefix = $"{BmsFolderImporter.SONGS_STORAGE_PATH}/";

            if (!managedPath.StartsWith(managedRootPrefix, StringComparison.OrdinalIgnoreCase))
                return Array.Empty<GroupDefinition>();

            string[] segments = managedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            string[] parentSegments = segments.Skip(1).Take(Math.Max(segments.Length - 2, 0)).ToArray();

            if (parentSegments.Length == 0)
                return new[] { createInternalRootGroup() };

            return createDirectoryPathGroups(internal_root_key, parentSegments);
        }

        private static IEnumerable<GroupDefinition> getExternalGroupDefinitions(BeatmapSetInfo beatmapSet)
        {
            if (!beatmapSet.IsExternalFilesystemStorage)
                return Array.Empty<GroupDefinition>();

            if (string.IsNullOrWhiteSpace(beatmapSet.ExternalLibraryRootPath))
                return new[] { createUnmappedExternalRootGroup() };

            string setPath = normalisePath(beatmapSet.FilesystemStoragePath!);
            string rootPath = normalisePath(beatmapSet.ExternalLibraryRootPath!);

            if (!string.Equals(setPath, rootPath, StringComparison.OrdinalIgnoreCase)
                && !FilesystemSanityCheckHelpers.IsSubDirectory(rootPath, setPath))
                return new[] { createUnmappedExternalRootGroup() };

            GroupDefinition rootGroup = new LibraryPathGroupDefinition($"external-root:{rootPath}", 0, getDisplayName(rootPath));
            string relativePath = string.Equals(setPath, rootPath, StringComparison.OrdinalIgnoreCase)
                ? string.Empty
                : Path.GetRelativePath(rootPath, setPath).ToStandardisedPath();

            string[] relativeSegments = string.IsNullOrWhiteSpace(relativePath) || relativePath == "."
                ? Array.Empty<string>()
                : relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

            string[] parentSegments = relativeSegments.Take(Math.Max(relativeSegments.Length - 1, 0)).ToArray();

            if (parentSegments.Length == 0)
                return new[] { rootGroup };

            return createDirectoryPathGroups($"external-root:{rootPath}", parentSegments, rootGroup);
        }

        private static IEnumerable<GroupDefinition> createDirectoryPathGroups(string rootKey, IEnumerable<string> segments, GroupDefinition? rootGroup = null)
        {
            GroupDefinition? current = rootGroup;
            string currentKey = rootKey;

            foreach (string segment in segments)
            {
                currentKey = $"{currentKey}/{segment.ToLowerInvariant()}";
                current = new LibraryPathGroupDefinition(currentKey, 0, segment, current);
            }

            return current?.GetPathFromRoot() ?? Array.Empty<GroupDefinition>();
        }

        private static GroupDefinition createInternalRootGroup()
            => new LibraryPathGroupDefinition(internal_root_key, int.MaxValue, OmsSongSelectStrings.InternalLibrary);

        private static GroupDefinition createUnmappedExternalRootGroup()
            => new LibraryPathGroupDefinition(unmapped_external_root_key, int.MaxValue, OmsSongSelectStrings.UnmappedExternalLibrary);

        private static string normalisePath(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static LocalisableString getDisplayName(string path)
        {
            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);

            return string.IsNullOrEmpty(name) ? trimmed : name;
        }

        private sealed record LibraryPathGroupDefinition(string Key, int GroupOrder, LocalisableString GroupTitle, GroupDefinition? GroupParent = null)
            : GroupDefinition(GroupOrder, GroupTitle, GroupParent);
    }
}
