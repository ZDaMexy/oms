// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.Extensions;

namespace osu.Game.Skinning
{
    public class SkinFolderImporter
    {
        public const string SKINS_STORAGE_PATH = "chartskin";

        private readonly Storage storage;
        private readonly Storage skinsStorage;
        private readonly RealmAccess realmAccess;

        // OMS G1: route folder-backed skins to BmsLegacySkin so skin.ini [Bms] sections are parsed.
        // Same reflection pattern as SkinImporter routing; when the BMS ruleset assembly is absent
        // this stays null and skins use plain LegacySkin — non-OMS environments are untouched.
        private static readonly string? bms_legacy_skin_instantiation_info =
            Type.GetType("osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms")?.GetInvariantInstantiationInfo();

        public SkinFolderImporter(Storage storage, RealmAccess realm)
        {
            this.storage = storage;
            skinsStorage = storage.GetStorageForDirectory(SKINS_STORAGE_PATH);
            realmAccess = realm;
        }

        public Task ImportManaged(string sourcePath, CancellationToken cancellationToken = default)
            => Task.Run(() => importManaged(sourcePath, cancellationToken), cancellationToken);

        public Task ImportExternal(string sourcePath, CancellationToken cancellationToken = default)
            => Task.Run(() => importExternal(sourcePath, cancellationToken), cancellationToken);

        public Task ScanManagedFolders(CancellationToken cancellationToken = default)
            => Task.Run(() => scanManagedFolders(cancellationToken), cancellationToken);

        public bool ShouldImportManaged(string path)
            => !hasActiveManagedSkin(getManagedPath(path));

        private void importManaged(string sourcePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(sourcePath);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException($"Source skin directory not found: {fullPath}");

            string skinIniPath = Path.Combine(fullPath, @"skin.ini");
            if (!File.Exists(skinIniPath))
                throw new InvalidOperationException($"Managed skin import requires a skin.ini in the source directory ({fullPath}).");

            string folderName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            if (string.IsNullOrWhiteSpace(folderName))
                folderName = @"skin";

            string destinationName = allocateDestinationName(folderName);
            string relativePath = Path.Combine(SKINS_STORAGE_PATH, destinationName).ToStandardisedPath();

            copyDirectory(fullPath, skinsStorage.GetFullPath(destinationName));
            writeSkinInfo(destinationName, relativePath, false);
        }

        private void importExternal(string sourcePath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string fullPath = Path.GetFullPath(sourcePath);
            if (!Directory.Exists(fullPath))
                throw new DirectoryNotFoundException($"External skin directory not found: {fullPath}");

            string skinIniPath = Path.Combine(fullPath, @"skin.ini");
            if (!File.Exists(skinIniPath))
                throw new InvalidOperationException($"External skin registration requires a skin.ini in the directory ({fullPath}).");

            string normalisedPath = normaliseExternalPath(fullPath);

            if (hasActiveExternalSkin(normalisedPath))
                return;

            string name = new DirectoryInfo(fullPath).Name;
            writeSkinInfo(name, normalisedPath, true);
        }

        private void scanManagedFolders(CancellationToken cancellationToken)
        {
            if (!storage.ExistsDirectory(SKINS_STORAGE_PATH))
                return;

            foreach (string dir in skinsStorage.GetDirectories(string.Empty))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string dirName = Path.GetFileName(dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                string relativePath = Path.Combine(SKINS_STORAGE_PATH, dirName).ToStandardisedPath();

                if (!skinsStorage.Exists(Path.Combine(dirName, @"skin.ini")))
                    continue;

                if (hasActiveManagedSkin(relativePath))
                    continue;

                writeSkinInfo(dirName, relativePath, false);
            }
        }

        private void writeSkinInfo(string name, string storagePath, bool external)
        {
            realmAccess.Write(r =>
            {
                r.Add(new SkinInfo
                {
                    Name = name,
                    FilesystemStoragePath = storagePath,
                    IsExternalFilesystemStorage = external,
                    InstantiationInfo = bms_legacy_skin_instantiation_info ?? string.Empty,
                });
            });
        }

        private bool hasActiveManagedSkin(string relativePath)
            => realmAccess.Run(r => r.All<SkinInfo>()
                .Where(s => !s.DeletePending && !s.IsExternalFilesystemStorage)
                .AsEnumerable()
                .Any(s => string.Equals(s.FilesystemStoragePath?.ToStandardisedPath(), relativePath.ToStandardisedPath(), StringComparison.OrdinalIgnoreCase)));

        private bool hasActiveExternalSkin(string externalPath)
            => realmAccess.Run(r => r.All<SkinInfo>()
                .Where(s => !s.DeletePending && s.IsExternalFilesystemStorage)
                .AsEnumerable()
                .Any(s => string.Equals(s.FilesystemStoragePath, externalPath, StringComparison.OrdinalIgnoreCase)));

        private string getManagedPath(string fullPath)
        {
            string full = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetRelativePath(storage.GetFullPath(string.Empty), full).ToStandardisedPath();
        }

        private string allocateDestinationName(string sourceName)
        {
            string safeName = sourceName.GetValidFilename();

            if (string.IsNullOrWhiteSpace(safeName))
                safeName = @"skin";

            string candidateName = safeName;
            int suffix = 2;

            while (hasActiveManagedSkin(Path.Combine(SKINS_STORAGE_PATH, candidateName).ToStandardisedPath()))
                candidateName = $"{safeName}-{suffix++}";

            return candidateName;
        }

        private static string normaliseExternalPath(string path)
            => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static void copyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                copyDirectory(subDir, destSubDir);
            }
        }
    }
}
