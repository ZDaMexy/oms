// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Platform;
using osu.Game.Database;
using Realms;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Index availability only: never deletes files or historical beatmap identities.
    /// </summary>
    public class FilesystemBeatmapIndex
    {
        private readonly Storage storage;
        private readonly RealmAccess realm;

        public FilesystemBeatmapIndex(Storage storage, RealmAccess realm)
        {
            this.storage = storage;
            this.realm = realm;
        }

        public static string NormalisePath(string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        public static bool ContainsPath(string root, string path)
        {
            root = NormalisePath(root);
            path = NormalisePath(path);
            return string.Equals(root, path, StringComparison.OrdinalIgnoreCase)
                   || path.StartsWith(Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        public void Reconcile(string root, ExternalLibraryRootType type, bool external, IReadOnlyCollection<string> presentDirectories)
        {
            var present = new HashSet<string>(presentDirectories.Select(NormalisePath), StringComparer.OrdinalIgnoreCase);
            setUnavailable(root, type, external, set => !present.Contains(resolvePath(set)));
        }

        public void Unregister(string root, ExternalLibraryRootType type, IReadOnlyCollection<string> remainingRoots)
            => setUnavailable(root, type, true, set => !remainingRoots.Any(remaining => ContainsPath(remaining, resolvePath(set))));

        private void setUnavailable(string root, ExternalLibraryRootType type, bool external, Func<BeatmapSetInfo, bool> missing)
        {
            string ruleset = type == ExternalLibraryRootType.BMS ? "bms" : "mania";
            realm.Write(r =>
            {
                foreach (var set in r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.FilesystemUnavailable).ToList())
                {
                    if (set.IsExternalFilesystemStorage != external || string.IsNullOrEmpty(set.FilesystemStoragePath)
                        || !set.Beatmaps.Any(b => b.Ruleset.ShortName == ruleset) || !ContainsPath(root, resolvePath(set)))
                        continue;

                    if (missing(set))
                        set.FilesystemUnavailable = true;
                }
            });
        }

        private string resolvePath(BeatmapSetInfo set)
            => NormalisePath(set.IsExternalFilesystemStorage ? set.FilesystemStoragePath! : storage.GetFullPath(set.FilesystemStoragePath!));

        public static void SupersedeDirectory(Realm realm, BeatmapSetInfo replacement)
        {
            string ruleset = replacement.Beatmaps.First().Ruleset.ShortName;
            foreach (var set in realm.All<BeatmapSetInfo>()
                                     .Filter("FilesystemStoragePath ==[c] $0 AND IsExternalFilesystemStorage == $1 AND DeletePending == false AND FilesystemUnavailable == false",
                                         replacement.FilesystemStoragePath, replacement.IsExternalFilesystemStorage)
                                     .ToList())
            {
                if (set.ID != replacement.ID && set.Beatmaps.Any(b => b.Ruleset.ShortName == ruleset))
                    set.FilesystemUnavailable = true;
            }
        }
    }
}
