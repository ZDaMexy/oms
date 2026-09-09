// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Skinning.Gameplay;
using osu.Game.Skinning.Gameplay.Scripting;
using osu.Game.Skinning.IO;
using osu.Game.Skinning.Windows;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Installation authority for an ordinary author package. This class contributes no visual components:
    /// admission, immutable capture, the public BMS/mania parser and package preparation are shared with user skins.
    /// </summary>
    public static class CanonicalSkinPackage
    {
        internal const string PACKAGE_FILENAME = "oms-simple.osk";
        internal const string WORKING_DIRECTORY = "skin-canonical";
        internal const string HASH_RESOURCE_NAME = "osu.Game.Skins.Canonical.oms-simple.sha256";
        internal const string RECORD_HASH = "oms.skin.canonical.simple.v1";
        internal const string REPAIR_MESSAGE = "游戏随附的简洁皮肤未能完整加载。请保留现有数据目录，使用完整发行包修复安装后重新启动；修复前不能进入游玩或预览。";

        private static readonly ConditionalWeakTable<Skin, VerifiedPackage> verified_packages = new ConditionalWeakTable<Skin, VerifiedPackage>();

        /// <summary>Only an instance created from the verified installation package can terminate gameplay fallback.</summary>
        public static bool IsCanonicalSkin(Skin skin) => verified_packages.TryGetValue(skin, out _);

        internal static SkinInfo CreateInfo() => new SkinInfo
        {
            ID = SkinInfo.OMS_SKIN,
            Name = "OMS 简洁",
            Creator = "OMS 开发组",
            Protected = true,
            InstantiationInfo = SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO,
            // Stable installation metadata permits recovery across package upgrades. Runtime content identity is
            // always the independently verified immutable capsule revision, never this record marker.
            Hash = RECORD_HASH,
        };

        internal static CanonicalSkinInstallationResult Load(Storage storage, IStorageResourceProvider resources)
        {
            using Stream? hashStream = typeof(CanonicalSkinPackage).Assembly.GetManifestResourceStream(HASH_RESOURCE_NAME);
            if (hashStream == null)
                return CanonicalSkinInstallationResult.Failed(CanonicalSkinInstallationFailure.InstallationManifestMissing);

            using var reader = new StreamReader(hashStream);
            string hash = reader.ReadToEnd().Trim();
            return Load(storage, resources, Path.Combine(AppContext.BaseDirectory, "Skins", "Canonical", PACKAGE_FILENAME), hash);
        }

        internal static CanonicalSkinInstallationResult Load(Storage storage, IStorageResourceProvider resources, string originalPath, string expectedHash)
        {
            CanonicalSkinArchiveResult archive = PrepareArchive(storage.GetFullPath(string.Empty), originalPath, expectedHash);
            if (!archive.IsSuccess)
                return CanonicalSkinInstallationResult.Failed(archive.Failure);

            Skin? skin = null;
            try
            {
                using var source = new MemoryStream(archive.Bytes!, writable: false);
                using var reader = SkinArchiveReader.OpenAsync(new ImportTask(source, PACKAGE_FILENAME), CancellationToken.None).AsTask().GetAwaiter().GetResult();
                var entries = new List<SkinPackageCapturedEntry?>();
                foreach (string filename in reader.Filenames)
                {
                    string name = filename;
                    using Stream stream = reader.GetStream(name);
                    entries.Add(SkinPackageCapturedEntry.CreateFile(name, stream.Length, () => reader.GetStream(name)));
                }

                SkinPackageRevisionCapsuleCreationResult captured = SkinPackageRevisionCapsuleFactory.Create(entries);
                if (!captured.IsSuccess)
                    return CanonicalSkinInstallationResult.Failed(CanonicalSkinInstallationFailure.PackageInvalid);

                SkinManagedFolderFactoryResult constructed = SkinManagedFolderFactory.Create(CreateInfo(), resources, captured.Capsule!);
                if (!constructed.IsSuccess)
                    return CanonicalSkinInstallationResult.Failed(CanonicalSkinInstallationFailure.PackageInvalid);

                skin = constructed.Skin!;
                skin.PrepareGameplaySkinPackage(CancellationToken.None);
                verified_packages.Add(skin, new VerifiedPackage(archive.Bytes!));
                Skin result = skin;
                skin = null;
                return CanonicalSkinInstallationResult.Success(result);
            }
            catch (Exception exception) when (exception is IOException or GameplaySkinScenePreparationException or GameplaySkinScriptException)
            {
                return CanonicalSkinInstallationResult.Failed(CanonicalSkinInstallationFailure.PackageInvalid);
            }
            finally
            {
                skin?.Dispose();
            }
        }

        /// <summary>
        /// Verifies the installation before trusting the working copy, then publishes one complete archive with a
        /// same-directory atomic rename. Existing unknown siblings, interrupted writes and directories are preserved.
        /// Held native parent handles prohibit reparse traversal or parent replacement during all cache writes.
        /// </summary>
        internal static CanonicalSkinArchiveResult PrepareArchive(string dataRoot, string originalPath, string expectedHash)
        {
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
                return CanonicalSkinArchiveResult.Failed(CanonicalSkinInstallationFailure.PlatformUnsupported);

            if (expectedHash.Length != 64 || !expectedHash.All(Uri.IsHexDigit))
                return CanonicalSkinArchiveResult.Failed(CanonicalSkinInstallationFailure.InstallationManifestInvalid);

            byte[] original;
            try
            {
                using var originalDirectory = HeldDirectory.Open(Path.GetDirectoryName(Path.GetFullPath(originalPath))!);
                original = originalDirectory.ReadFile(Path.GetFileName(originalPath));
                if (!matchesHash(original, expectedHash))
                    return CanonicalSkinArchiveResult.Failed(CanonicalSkinInstallationFailure.InstallationPackageInvalid);
            }
            catch (Exception e) when (isFilesystemFailure(e))
            {
                return CanonicalSkinArchiveResult.Failed(CanonicalSkinInstallationFailure.InstallationPackageUnavailable);
            }

            try
            {
                using var root = HeldDirectory.Open(Path.GetFullPath(dataRoot));
                string workingDirectory = Path.Combine(dataRoot, WORKING_DIRECTORY);
                root.CreateDirectoryIfMissing(WORKING_DIRECTORY);
                using var working = HeldDirectory.Open(workingDirectory);
                string target = Path.Combine(workingDirectory, PACKAGE_FILENAME);

                bool existingFile = working.ContainsRegularFile(PACKAGE_FILENAME);
                if (existingFile)
                {
                    byte[] existing = working.ReadFile(PACKAGE_FILENAME, allowOversized: true);
                    if (matchesHash(existing, expectedHash))
                        return CanonicalSkinArchiveResult.Success(existing);
                }

                // A random exclusive file belongs only to this invocation. A process interruption can leave it behind;
                // later startups deliberately never infer ownership from this name and never sweep these siblings.
                string temporary = Path.Combine(workingDirectory, $"{PACKAGE_FILENAME}.{Guid.NewGuid():N}.tmp");
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.WriteThrough))
                {
                    stream.Write(original);
                    stream.Flush(true);
                }

                // Preserve even unrecognised content in the reserved cache slot. The held no-follow file is renamed
                // without replacement; an interruption leaves a complete old copy and/or complete staged archive.
                // The final publication can never overwrite a foreign file which appeared in the target slot.
                if (existingFile)
                    working.PreserveFile(PACKAGE_FILENAME);
                File.Move(temporary, target, overwrite: false);
                byte[] published = working.ReadFile(PACKAGE_FILENAME);
                return matchesHash(published, expectedHash)
                    ? CanonicalSkinArchiveResult.Success(published)
                    : CanonicalSkinArchiveResult.Failed(CanonicalSkinInstallationFailure.WorkingCopyUnavailable);
            }
            catch (Exception e) when (isFilesystemFailure(e))
            {
                return CanonicalSkinArchiveResult.Failed(CanonicalSkinInstallationFailure.WorkingCopyUnavailable);
            }
        }

        internal static void Export(Skin skin, Stream output, CancellationToken cancellationToken)
        {
            if (!verified_packages.TryGetValue(skin, out VerifiedPackage? package))
                throw new InvalidOperationException(REPAIR_MESSAGE);
            cancellationToken.ThrowIfCancellationRequested();
            output.Write(package.Bytes);
        }

        private static bool matchesHash(byte[] bytes, string hash)
            => string.Equals(Convert.ToHexString(SHA256.HashData(bytes)), hash, StringComparison.OrdinalIgnoreCase);

        private static bool isFilesystemFailure(Exception exception)
            => exception is IOException or UnauthorizedAccessException or WindowsSkinPackageCaptureFileSystemException;

        private sealed record VerifiedPackage(byte[] Bytes);

        [SupportedOSPlatform("windows10.0.16299")]
        private sealed class HeldDirectory : IDisposable
        {
            private readonly NativeWindowsSkinPackageCaptureFileSystem fileSystem = new NativeWindowsSkinPackageCaptureFileSystem();
            private readonly List<IWindowsSkinPackageCaptureHandle> handles = new List<IWindowsSkinPackageCaptureHandle>();
            private IWindowsSkinPackageCaptureHandle Current => handles[^1];

            public static HeldDirectory Open(string path)
            {
                string absolute = Path.GetFullPath(path);
                string? root = Path.GetPathRoot(absolute);
                if (root == null || root.Length != 3 || root[1] != ':' || !char.IsAsciiLetter(root[0]))
                    throw new IOException("The installation storage must be on a local volume.");

                var result = new HeldDirectory();
                try
                {
                    result.handles.Add(result.fileSystem.OpenLocalVolumeRoot(root[0]));
                    result.validateDirectory(result.Current);
                    foreach (string segment in absolute[root.Length..].Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        result.handles.Add(result.fileSystem.OpenChildNoFollow(result.Current, segment,
                            WindowsSkinPackageOpenMode.AuthorityDirectory, SkinManagedPackageCaptureRejectionReason.PackageUnavailable));
                        result.validateDirectory(result.Current);
                    }
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }

            public void CreateDirectoryIfMissing(string name)
            {
                WindowsSkinPackageDirectoryEntry? existing = findEntry(name);
                if (existing != null)
                {
                    if (existing.Metadata.Kind != WindowsSkinPackageEntryKind.Directory || existing.Metadata.IsReparsePoint)
                        throw new IOException("The canonical working directory is unavailable.");
                    return;
                }
                using IWindowsSkinPackageCaptureHandle created = fileSystem.CreateChildNoFollowNoReplace(Current, name, directory: true);
                validateDirectory(created);
            }

            public bool ContainsRegularFile(string name)
            {
                WindowsSkinPackageDirectoryEntry? existing = findEntry(name);
                if (existing == null)
                    return false;
                if (existing.Metadata.Kind != WindowsSkinPackageEntryKind.File || existing.Metadata.IsReparsePoint)
                    throw new IOException("The canonical working copy is unavailable.");
                return true;
            }

            private WindowsSkinPackageDirectoryEntry? findEntry(string name)
            {
                WindowsSkinPackageDirectoryEntry[] matches = fileSystem.Enumerate(Current, 65536, CancellationToken.None)
                    .Where(entry => string.Equals(entry.Name, name, StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
                if (matches.Length > 1)
                    throw new IOException("The canonical working directory contains conflicting names.");
                return matches.SingleOrDefault();
            }

            public void PreserveFile(string name)
            {
                using IWindowsSkinPackageCaptureHandle file = fileSystem.OpenChildNoFollow(Current, name,
                    WindowsSkinPackageOpenMode.DeleteExclusiveFile, SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
                WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(file);
                if (metadata.IsReparsePoint || metadata.Kind != WindowsSkinPackageEntryKind.File)
                    throw new IOException("The canonical working copy is unavailable.");
                fileSystem.RenameChildNoReplace(file, Current, $"{name}.preserved-{Guid.NewGuid():N}");
            }

            public byte[] ReadFile(string name, bool allowOversized = false)
            {
                using IWindowsSkinPackageCaptureHandle file = fileSystem.OpenChildNoFollow(Current, name,
                    WindowsSkinPackageOpenMode.CapturedFile, SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
                WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(file);
                if (metadata.IsReparsePoint || metadata.Kind != WindowsSkinPackageEntryKind.File || metadata.Length < 0)
                    throw new IOException("The canonical archive is unavailable.");
                if (metadata.Length > SkinArchiveImportLimits.MAX_ARCHIVE_BYTES)
                {
                    if (allowOversized)
                        return Array.Empty<byte>();
                    throw new IOException("The canonical archive exceeds its byte budget.");
                }
                using Stream stream = fileSystem.CreateNonOwningReadStream(file);
                byte[] bytes = new byte[checked((int)metadata.Length)];
                stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1 || !metadata.Equals(fileSystem.QueryMetadata(file)))
                    throw new IOException("The canonical archive changed while being read.");
                return bytes;
            }

            private void validateDirectory(IWindowsSkinPackageCaptureHandle handle)
            {
                WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(handle);
                if (metadata.IsReparsePoint || metadata.Kind != WindowsSkinPackageEntryKind.Directory)
                    throw new IOException("The canonical directory is unavailable.");
            }

            public void Dispose()
            {
                for (int i = handles.Count - 1; i >= 0; i--)
                    handles[i].Dispose();
            }
        }
    }

    internal enum CanonicalSkinInstallationFailure
    {
        None,
        PlatformUnsupported,
        InstallationManifestMissing,
        InstallationManifestInvalid,
        InstallationPackageUnavailable,
        InstallationPackageInvalid,
        WorkingCopyUnavailable,
        PackageInvalid,
        ProtectedRecordUnrecognised,
    }

    internal sealed record CanonicalSkinArchiveResult(byte[]? Bytes, CanonicalSkinInstallationFailure Failure)
    {
        public bool IsSuccess => Bytes != null;
        public static CanonicalSkinArchiveResult Success(byte[] bytes) => new CanonicalSkinArchiveResult(bytes, CanonicalSkinInstallationFailure.None);
        public static CanonicalSkinArchiveResult Failed(CanonicalSkinInstallationFailure reason) => new CanonicalSkinArchiveResult(null, reason);
    }

    internal sealed record CanonicalSkinInstallationResult(Skin? Skin, CanonicalSkinInstallationFailure Failure)
    {
        public static CanonicalSkinInstallationResult Success(Skin skin) => new CanonicalSkinInstallationResult(skin, CanonicalSkinInstallationFailure.None);
        public static CanonicalSkinInstallationResult Failed(CanonicalSkinInstallationFailure reason) => new CanonicalSkinInstallationResult(null, reason);
    }
}
