// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Models;
using Realms;

namespace osu.Game.Database
{
    /// <summary>
    /// Handles the storing of files to the file system (and database) backing.
    /// </summary>
    public class RealmFileStore
    {
        private static readonly object import_scope_lock = new object();
        private static readonly HashSet<ImportScope> active_import_scopes = new HashSet<ImportScope>();
        private static readonly Dictionary<ImportGroupKey, ImportParticipantGroup> import_participant_groups =
            new Dictionary<ImportGroupKey, ImportParticipantGroup>();
        private static bool cleanupInProgress;

        [ThreadStatic]
        private static ImportScope? currentImportScope;

        private readonly RealmAccess realm;
        private readonly string storageIdentity;

        public readonly IResourceStore<byte[]> Store;

        public readonly Storage Storage;

        public RealmFileStore(RealmAccess realm, Storage storage)
        {
            this.realm = realm;

            Storage = storage.GetStorageForDirectory(@"files");
            Store = new StorageBackedResourceStore(Storage);
            storageIdentity = Storage.GetFullPath(string.Empty);
        }

        /// <summary>
        /// Add a new file to the game-wide database, copying it to permanent storage if not already present.
        /// </summary>
        /// <param name="data">The file data stream.</param>
        /// <param name="realm">The realm instance to add to. Should already be in a transaction.</param>
        /// <param name="addToRealm">Whether the <see cref="RealmFile"/> should immediately be added to the underlying realm. If <c>false</c> is provided here, the instance must be manually added.</param>
        /// <param name="preferHardLinks">Whether this import should use hard links rather than file copy operations if available.</param>
        public RealmFile Add(Stream data, Realm realm, bool addToRealm = true, bool preferHardLinks = false)
        {
            CancellationToken cancellationToken = currentImportScope?.CancellationToken ?? CancellationToken.None;
            string hash = computeSha256(data, cancellationToken);
            var key = new ImportGroupKey(this.realm, storageIdentity, hash);
            ImportParticipantGroup group;

            lock (import_scope_lock)
            {
                if (cleanupInProgress)
                    throw new InvalidOperationException("The Realm file store is being cleaned up.");

                if (!import_participant_groups.TryGetValue(key, out group!))
                {
                    group = new ImportParticipantGroup(key, Storage);
                    import_participant_groups.Add(key, group);
                }

                // Claiming an add cancels a pending finalizer before any Realm snapshot is taken. If the finalizer has
                // already entered its Realm transaction it commits while holding this lock, so this add starts a new group.
                group.Finalizing = false;
                group.ActiveAdds++;

                if (currentImportScope != null)
                    currentImportScope.Join(key, group);
                else
                    group.UnscopedWriterObserved = true;
            }

            try
            {
                var existing = realm.Find<RealmFile>(hash);
                var file = existing ?? new RealmFile { Hash = hash };

                lock (import_scope_lock)
                {
                    if (!group.BaselineInitialised)
                    {
                        group.BaselineRealmRecordExisted = existing != null;
                        group.BaselineBlobExisted = Storage.Exists(file.GetStoragePath());
                        group.BaselineInitialised = true;
                    }

                    if (!checkFileExistsAndMatchesHash(file, cancellationToken))
                    {
                        // Record the write before opening the destination. If the copy faults after creating a partial blob,
                        // the import receipt must still own and remove that blob during rollback.
                        group.BlobWritten = true;
                        copyToStore(file, data, preferHardLinks, cancellationToken);
                    }
                }

                if (addToRealm && !file.IsManaged)
                    realm.Add(file);

                return file;
            }
            finally
            {
                lock (import_scope_lock)
                {
                    group.ActiveAdds--;
                    removePreservedOrUnownedGroup(group);
                }
            }
        }

        /// <summary>
        /// Begins an exact, opt-in receipt scope for one archive import. All <see cref="RealmFileStore"/> instances used on
        /// this thread participate, which includes file changes made by a model manager during population.
        /// </summary>
        internal ImportScope BeginImportScope(CancellationToken cancellationToken = default)
            => new ImportScope(this, cancellationToken);

        private void copyToStore(
            RealmFile file,
            Stream data,
            bool preferHardLinks,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (data is FileStream fs && preferHardLinks)
            {
                // attempt to do a fast hard link rather than copy.
                if (HardLinkHelper.TryCreateHardLink(Storage.GetFullPath(file.GetStoragePath(), true), fs.Name))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }
            }

            data.Seek(0, SeekOrigin.Begin);

            using (var output = Storage.CreateFileSafely(file.GetStoragePath()))
                copyWithCancellation(data, output, cancellationToken);

            data.Seek(0, SeekOrigin.Begin);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private bool checkFileExistsAndMatchesHash(RealmFile file, CancellationToken cancellationToken)
        {
            string path = file.GetStoragePath();

            // we may be re-adding a file to fix missing store entries.
            if (!Storage.Exists(path))
                return false;

            // even if the file already exists, check the existing checksum for safety.
            using (var stream = Storage.GetStream(path))
                return computeSha256(stream, cancellationToken) == file.Hash;
        }

        private static string computeSha256(Stream stream, CancellationToken cancellationToken)
        {
            long? originalPosition = stream.CanSeek ? stream.Position : null;
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[81920];

            try
            {
                if (stream.CanSeek)
                    stream.Position = 0;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read == 0)
                        break;

                    hash.AppendData(buffer, 0, read);
                }

                cancellationToken.ThrowIfCancellationRequested();
                return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
            }
            finally
            {
                if (originalPosition.HasValue)
                    stream.Position = originalPosition.Value;
            }
        }

        private static void copyWithCancellation(
            Stream source,
            Stream destination,
            CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[81920];

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    return;

                destination.Write(buffer, 0, read);
            }
        }

        public void Cleanup()
        {
            lock (import_scope_lock)
            {
                if (cleanupInProgress || active_import_scopes.Count > 0 || import_participant_groups.Values.Any(group => group.ActiveAdds > 0))
                {
                    Logger.Log(@"Skipping realm file store cleanup while an exact import receipt is active.");
                    return;
                }

                cleanupInProgress = true;
            }

            try
            {
                cleanup();
            }
            finally
            {
                lock (import_scope_lock)
                    cleanupInProgress = false;
            }
        }

        private void cleanup()
        {
            Logger.Log(@"Beginning realm file store cleanup");

            int totalFiles = 0;
            int removedFiles = 0;

            // can potentially be run asynchronously, although we will need to consider operation order for disk deletion vs realm removal.
            realm.Write(r =>
            {
                foreach (var file in r.All<RealmFile>().Filter(@$"{nameof(RealmFile.Usages)}.@count = 0"))
                {
                    totalFiles++;

                    Debug.Assert(file.BacklinksCount == 0);

                    try
                    {
                        removedFiles++;
                        Storage.Delete(file.GetStoragePath());
                        r.Remove(file);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $@"Could not delete databased file {file.Hash}");
                    }
                }
            });

            Logger.Log($@"Finished realm file store cleanup ({removedFiles} of {totalFiles} deleted)");
        }

        internal sealed class ImportScope : IDisposable
        {
            private readonly Dictionary<ImportGroupKey, ImportParticipantGroup> participations =
                new Dictionary<ImportGroupKey, ImportParticipantGroup>();

            private bool completed;
            private bool disposed;

            internal CancellationToken CancellationToken { get; }

            internal Action<string>? FinaliseGroupTestHook { get; set; }

            public ImportScope(RealmFileStore owner, CancellationToken cancellationToken)
            {
                CancellationToken = cancellationToken;
                cancellationToken.ThrowIfCancellationRequested();
                lock (import_scope_lock)
                {
                    if (cleanupInProgress)
                        throw new InvalidOperationException("The Realm file store is being cleaned up.");

                    if (currentImportScope != null)
                        throw new InvalidOperationException("A Realm file import receipt scope is already active on this thread.");

                    currentImportScope = this;
                    active_import_scopes.Add(this);
                }
            }

            internal void Join(ImportGroupKey key, ImportParticipantGroup group)
            {
                if (participations.ContainsKey(key))
                    return;

                group.ActiveParticipants++;
                participations.Add(key, group);
            }

            public void Complete() => completed = true;

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;

                List<PendingFinalization> finalizations;

                lock (import_scope_lock)
                {
                    try
                    {
                        finalizations = finishParticipations();
                    }
                    finally
                    {
                        active_import_scopes.Remove(this);
                        currentImportScope = null;
                    }
                }

                var failures = new List<(string Hash, Exception Exception)>();

                foreach (PendingFinalization finalization in finalizations)
                {
                    ImportParticipantGroup group = finalization.Group;

                    try
                    {
                        FinaliseGroupTestHook?.Invoke(group.Key.Hash);
                        finaliseFailedGroup(group, finalization.Generation);
                    }
                    catch (Exception e)
                    {
                        failures.Add((group.Key.Hash, e));
                    }
                    finally
                    {
                        // A failed Realm or storage operation must not leave this participant permanently finalizing.
                        // Retain the receipt so a later scoped add of the same hash can safely retry the rollback.
                        lock (import_scope_lock)
                        {
                            if (group.FinalizationGeneration == finalization.Generation)
                            {
                                group.Finalizing = false;
                                removePreservedOrUnownedGroup(group);
                            }
                        }
                    }
                }

                foreach ((string hash, Exception exception) in failures)
                    Logger.Error(exception, $@"Could not roll back imported file {hash}");
            }

            private List<PendingFinalization> finishParticipations()
            {
                var finalizations = new List<PendingFinalization>();
                foreach ((_, ImportParticipantGroup group) in participations)
                {
                    if (completed)
                        group.SuccessfulWriterObserved = true;

                    group.ActiveParticipants--;
                    if (group.ActiveParticipants == 0 && group.ActiveAdds == 0
                        && group.BaselineInitialised
                        && ownsRollbackAsset(group)
                        && !group.SuccessfulWriterObserved && !group.UnscopedWriterObserved)
                    {
                        group.Finalizing = true;
                        finalizations.Add(new PendingFinalization(group, ++group.FinalizationGeneration));
                    }
                    else
                    {
                        removePreservedOrUnownedGroup(group);
                    }
                }

                return finalizations;
            }

            private static void finaliseFailedGroup(ImportParticipantGroup group, long generation)
            {
                group.Key.RealmAccess.Run(realm =>
                {
                    using Transaction transaction = realm.BeginWrite();

                    lock (import_scope_lock)
                    {
                        if (!group.Finalizing
                            || group.FinalizationGeneration != generation
                            || !import_participant_groups.TryGetValue(group.Key, out ImportParticipantGroup? current)
                            || !ReferenceEquals(current, group)
                            || group.ActiveParticipants != 0 || group.ActiveAdds != 0
                            || group.SuccessfulWriterObserved || group.UnscopedWriterObserved)
                        {
                            transaction.Rollback();
                            removePreservedOrUnownedGroup(group);
                            return;
                        }

                        RealmFile? file = realm.Find<RealmFile>(group.Key.Hash);
                        if (file != null && file.Usages.Any())
                        {
                            transaction.Rollback();
                            import_participant_groups.Remove(group.Key);
                            group.Finalizing = false;
                            return;
                        }

                        if (file != null && !group.BaselineRealmRecordExisted)
                            realm.Remove(file);

                        transaction.Commit();
                    }
                });

                lock (import_scope_lock)
                {
                    if (!group.Finalizing
                        || group.FinalizationGeneration != generation
                        || !import_participant_groups.TryGetValue(group.Key, out ImportParticipantGroup? current)
                        || !ReferenceEquals(current, group)
                        || group.ActiveParticipants != 0 || group.ActiveAdds != 0
                        || group.SuccessfulWriterObserved || group.UnscopedWriterObserved)
                    {
                        removePreservedOrUnownedGroup(group);
                        return;
                    }

                    if (group.BaselineInitialised && !group.BaselineBlobExisted && group.BlobWritten)
                    {
                        string storagePath = new RealmFile { Hash = group.Key.Hash }.GetStoragePath();
                        if (group.Storage.Exists(storagePath))
                            group.Storage.Delete(storagePath);
                    }

                    import_participant_groups.Remove(group.Key);
                    group.Finalizing = false;
                }
            }

            private readonly record struct PendingFinalization(ImportParticipantGroup Group, long Generation);
        }

        private static void removePreservedOrUnownedGroup(ImportParticipantGroup group)
        {
            if (group.ActiveParticipants != 0 || group.ActiveAdds != 0 || group.Finalizing)
                return;

            if (group.SuccessfulWriterObserved || group.UnscopedWriterObserved || !group.BaselineInitialised
                || !ownsRollbackAsset(group))
                import_participant_groups.Remove(group.Key);
        }

        private static bool ownsRollbackAsset(ImportParticipantGroup group)
            => !group.BaselineRealmRecordExisted
               || (!group.BaselineBlobExisted && group.BlobWritten);

        internal readonly struct ImportGroupKey : IEquatable<ImportGroupKey>
        {
            public RealmAccess RealmAccess { get; }
            public string StorageIdentity { get; }
            public string Hash { get; }

            public ImportGroupKey(RealmAccess realmAccess, string storageIdentity, string hash)
            {
                RealmAccess = realmAccess;
                StorageIdentity = storageIdentity;
                Hash = hash;
            }

            public bool Equals(ImportGroupKey other) => ReferenceEquals(RealmAccess, other.RealmAccess)
                                                        && string.Equals(StorageIdentity, other.StorageIdentity, StringComparison.OrdinalIgnoreCase)
                                                        && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

            public override bool Equals(object? obj) => obj is ImportGroupKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(RealmAccess),
                StringComparer.OrdinalIgnoreCase.GetHashCode(StorageIdentity), StringComparer.Ordinal.GetHashCode(Hash));
        }

        internal sealed class ImportParticipantGroup
        {
            public ImportGroupKey Key { get; }
            public Storage Storage { get; }
            public bool BaselineInitialised { get; set; }
            public bool BaselineRealmRecordExisted { get; set; }
            public bool BaselineBlobExisted { get; set; }
            public int ActiveParticipants { get; set; }
            public int ActiveAdds { get; set; }
            public bool BlobWritten { get; set; }
            public bool SuccessfulWriterObserved { get; set; }
            public bool UnscopedWriterObserved { get; set; }
            public bool Finalizing { get; set; }
            public long FinalizationGeneration { get; set; }

            public ImportParticipantGroup(ImportGroupKey key, Storage storage)
            {
                Key = key;
                Storage = storage;
            }
        }
    }
}
