// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Database;
using Realms;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Stable, non-sensitive reason why managed-folder discovery or reconciliation did not complete.
    /// </summary>
    internal enum SkinManagedFolderScanFailureReason
    {
        None,
        UnsupportedPlatform,
        InvalidDataRoot,
        RootUnavailable,
        RootUnreadable,
        RootUnstable,
        NativeFailure,
        SnapshotRejected,
        PreparationFailed,
        ReconciliationFailed,
    }

    /// <summary>
    /// One package which was captured and validated during a complete managed-root discovery pass.
    /// </summary>
    /// <remarks>
    /// The relative path, package metadata and revision are user-controlled and must never be included in diagnostics.
    /// </remarks>
    internal sealed class SkinManagedFolderDiscovery
    {
        public string ManagedRelativePath { get; }

        public string Name { get; }

        public string Creator { get; }

        public string ContentRevision { get; }

        public SkinManagedFolderDiscovery(string managedRelativePath, string name, string creator, string contentRevision)
        {
            ManagedRelativePath = managedRelativePath;
            Name = name;
            Creator = creator;
            ContentRevision = contentRevision;
        }

        public override string ToString() => nameof(SkinManagedFolderDiscovery);
    }

    /// <summary>
    /// A discovery result which separates all observed direct-child paths from packages that were valid to import.
    /// </summary>
    /// <remarks>
    /// An observed but invalid package intentionally prevents negative reconciliation of an existing owned record.
    /// Only a complete, stable snapshot may be reconciled. No path or package metadata is exposed by
    /// <see cref="ToString"/>.
    /// </remarks>
    internal sealed class SkinManagedFolderDiscoverySnapshot
    {
        public bool IsComplete { get; }

        public SkinManagedFolderScanFailureReason FailureReason { get; }

        public IReadOnlyList<string> ObservedManagedRelativePaths { get; }

        public IReadOnlyList<SkinManagedFolderDiscovery> ValidDiscoveries { get; }

        private SkinManagedFolderDiscoverySnapshot(
            bool isComplete,
            SkinManagedFolderScanFailureReason failureReason,
            IEnumerable<string> observedManagedRelativePaths,
            IEnumerable<SkinManagedFolderDiscovery> validDiscoveries)
        {
            ArgumentNullException.ThrowIfNull(observedManagedRelativePaths);
            ArgumentNullException.ThrowIfNull(validDiscoveries);

            IsComplete = isComplete;
            FailureReason = failureReason;
            ObservedManagedRelativePaths = Array.AsReadOnly(observedManagedRelativePaths.ToArray());
            ValidDiscoveries = Array.AsReadOnly(validDiscoveries.ToArray());
        }

        public static SkinManagedFolderDiscoverySnapshot Complete(
            IEnumerable<string> observedManagedRelativePaths,
            IEnumerable<SkinManagedFolderDiscovery> validDiscoveries)
            => new SkinManagedFolderDiscoverySnapshot(
                true,
                SkinManagedFolderScanFailureReason.None,
                observedManagedRelativePaths,
                validDiscoveries);

        public static SkinManagedFolderDiscoverySnapshot Incomplete(SkinManagedFolderScanFailureReason failureReason)
        {
            if (!Enum.IsDefined(failureReason) || failureReason == SkinManagedFolderScanFailureReason.None)
                throw new ArgumentOutOfRangeException(nameof(failureReason));

            return new SkinManagedFolderDiscoverySnapshot(
                false,
                failureReason,
                Array.Empty<string>(),
                Array.Empty<SkinManagedFolderDiscovery>());
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderDiscoverySnapshot)}:{IsComplete}:{FailureReason}:Observed={ObservedManagedRelativePaths.Count}:Valid={ValidDiscoveries.Count}";
    }

    internal interface ISkinManagedFolderDiscoverySource
    {
        SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Non-sensitive result of one managed-folder scan.
    /// </summary>
    internal sealed class SkinManagedFolderScanResult
    {
        public SkinManagedFolderScanFailureReason FailureReason { get; }

        public int Added { get; }

        public int Updated { get; }

        public int Revived { get; }

        public int SoftDeleted { get; }

        public int Conflicts { get; }

        public bool IsSuccess => FailureReason == SkinManagedFolderScanFailureReason.None;

        private SkinManagedFolderScanResult(
            SkinManagedFolderScanFailureReason failureReason,
            int added = 0,
            int updated = 0,
            int revived = 0,
            int softDeleted = 0,
            int conflicts = 0)
        {
            FailureReason = failureReason;
            Added = added;
            Updated = updated;
            Revived = revived;
            SoftDeleted = softDeleted;
            Conflicts = conflicts;
        }

        public static SkinManagedFolderScanResult Failure(SkinManagedFolderScanFailureReason failureReason)
        {
            if (!Enum.IsDefined(failureReason) || failureReason == SkinManagedFolderScanFailureReason.None)
                throw new ArgumentOutOfRangeException(nameof(failureReason));

            return new SkinManagedFolderScanResult(failureReason);
        }

        internal static SkinManagedFolderScanResult Success(int added, int updated, int revived, int softDeleted, int conflicts)
            => new SkinManagedFolderScanResult(
                SkinManagedFolderScanFailureReason.None,
                added,
                updated,
                revived,
                softDeleted,
                conflicts);

        public override string ToString()
            => $"{nameof(SkinManagedFolderScanResult)}:{FailureReason}:Added={Added}:Updated={Updated}:Revived={Revived}:SoftDeleted={SoftDeleted}:Conflicts={Conflicts}";
    }

    /// <summary>
    /// Reconciles one complete native discovery snapshot into records owned by this exact scanner authority.
    /// </summary>
    /// <remarks>
    /// The scanner never claims or mutates null, unknown or foreign authority records. Negative reconciliation is a
    /// Realm-only soft delete and is performed only for this scanner's unique, structurally valid records in a complete
    /// snapshot. It never mutates the filesystem.
    /// </remarks>
    internal sealed class SkinManagedFolderScanner
    {
        internal const string AUTHORITY_OWNER = "oms.skin.managed-folder.scanner.v1";

        private readonly RealmAccess realm;
        private readonly ISkinManagedFolderDiscoverySource source;
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly SemaphoreSlim scanGate = new SemaphoreSlim(1, 1);

        internal Action ReconciliationBeforeCommit { get; set; } = () => { };

        public SkinManagedFolderScanner(
            RealmAccess realm,
            ISkinManagedFolderDiscoverySource source,
            SkinManagedFolderOperationCoordinator? coordinator = null)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.coordinator = coordinator ?? new SkinManagedFolderOperationCoordinator();
        }

        public SkinManagedFolderScanResult Scan(CancellationToken cancellationToken = default)
        {
            scanGate.Wait(cancellationToken);

            try
            {
                using SkinManagedFolderOperationCoordinator.Lease operationLease = coordinator.Enter(cancellationToken);
                SkinManagedFolderDiscoverySnapshot snapshot;

                try
                {
                    snapshot = source.Discover(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return SkinManagedFolderScanResult.Failure(SkinManagedFolderScanFailureReason.PreparationFailed);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (snapshot == null)
                    return SkinManagedFolderScanResult.Failure(SkinManagedFolderScanFailureReason.PreparationFailed);

                if (!snapshot.IsComplete)
                {
                    return snapshot.FailureReason == SkinManagedFolderScanFailureReason.None
                        ? SkinManagedFolderScanResult.Failure(SkinManagedFolderScanFailureReason.SnapshotRejected)
                        : SkinManagedFolderScanResult.Failure(snapshot.FailureReason);
                }

                if (snapshot.FailureReason != SkinManagedFolderScanFailureReason.None
                    || !tryValidateSnapshot(snapshot, out ValidatedSnapshot validated))
                {
                    return SkinManagedFolderScanResult.Failure(SkinManagedFolderScanFailureReason.SnapshotRejected);
                }

                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return realm.Write(r => reconcile(r, validated, cancellationToken));
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return SkinManagedFolderScanResult.Failure(SkinManagedFolderScanFailureReason.ReconciliationFailed);
                }
            }
            finally
            {
                scanGate.Release();
            }
        }

        private SkinManagedFolderScanResult reconcile(Realm realm, ValidatedSnapshot snapshot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            SkinInfo[] records = realm.All<SkinInfo>().ToArray();
            var recordsByPath = new Dictionary<string, List<SkinInfo>>(StringComparer.OrdinalIgnoreCase);

            foreach (SkinInfo record in records)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!tryNormaliseManagedRelativePath(record.FilesystemStoragePath, out string normalisedPath))
                    continue;

                if (!recordsByPath.TryGetValue(normalisedPath, out List<SkinInfo>? matches))
                    recordsByPath.Add(normalisedPath, matches = new List<SkinInfo>());

                matches.Add(record);
            }

            var additions = new List<SkinManagedFolderDiscovery>();
            var updates = new List<PlannedUpdate>();
            var softDeletes = new List<SkinInfo>();
            var conflicts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (SkinManagedFolderDiscovery discovery in snapshot.ValidDiscoveries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (coordinator.IsPathFrozen(discovery.ManagedRelativePath))
                {
                    conflicts.Add(discovery.ManagedRelativePath);
                    continue;
                }

                if (!recordsByPath.TryGetValue(discovery.ManagedRelativePath, out List<SkinInfo>? matches))
                {
                    additions.Add(discovery);
                    continue;
                }

                if (matches.Count != 1 || !isMutableOwnedRecord(matches[0]))
                {
                    conflicts.Add(discovery.ManagedRelativePath);
                    continue;
                }

                SkinInfo record = matches[0];
                bool wasDeletePending = record.DeletePending;
                bool changed = wasDeletePending
                               || !string.Equals(record.Name, discovery.Name, StringComparison.Ordinal)
                               || !string.Equals(record.Creator, discovery.Creator, StringComparison.Ordinal)
                               || !string.Equals(record.InstantiationInfo, SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO, StringComparison.Ordinal)
                               || !string.Equals(record.Hash, discovery.ContentRevision, StringComparison.Ordinal)
                               || !string.Equals(record.FilesystemStoragePath, discovery.ManagedRelativePath, StringComparison.Ordinal);

                if (changed)
                    updates.Add(new PlannedUpdate(record, discovery, wasDeletePending));
            }

            foreach ((string path, List<SkinInfo> matches) in recordsByPath)
            {
                cancellationToken.ThrowIfCancellationRequested();

                SkinInfo[] owned = matches.Where(hasExactOwner).ToArray();

                if (owned.Length == 0)
                    continue;

                if (matches.Count != 1 || owned.Length != 1 || !isMutableOwnedRecord(owned[0]))
                {
                    conflicts.Add(path);
                    continue;
                }

                if (coordinator.IsPathFrozen(path))
                {
                    conflicts.Add(path);
                    continue;
                }

                if (!snapshot.ObservedManagedRelativePaths.Contains(path)
                    && !owned[0].DeletePending)
                {
                    softDeletes.Add(owned[0]);
                }
            }

            // Realm rolls the whole transaction back if cancellation is observed during application. The final check
            // below is the cancellation linearisation point immediately before Realm is allowed to commit.
            cancellationToken.ThrowIfCancellationRequested();

            foreach (SkinManagedFolderDiscovery discovery in additions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                realm.Add(new SkinInfo(discovery.Name, discovery.Creator, SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
                {
                    Hash = discovery.ContentRevision,
                    FilesystemStoragePath = discovery.ManagedRelativePath,
                    IsExternalFilesystemStorage = false,
                    FilesystemStorageAuthorityOwner = AUTHORITY_OWNER,
                    Protected = false,
                    DeletePending = false,
                });
            }

            int updated = 0;
            int revived = 0;

            foreach (PlannedUpdate update in updates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                update.Record.Name = update.Discovery.Name;
                update.Record.Creator = update.Discovery.Creator;
                update.Record.InstantiationInfo = SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO;
                update.Record.Hash = update.Discovery.ContentRevision;
                update.Record.FilesystemStoragePath = update.Discovery.ManagedRelativePath;
                update.Record.IsExternalFilesystemStorage = false;
                update.Record.DeletePending = false;

                if (update.WasDeletePending)
                    revived++;
                else
                    updated++;
            }

            foreach (SkinInfo record in softDeletes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                record.DeletePending = true;
            }

            ReconciliationBeforeCommit();
            cancellationToken.ThrowIfCancellationRequested();

            return SkinManagedFolderScanResult.Success(additions.Count, updated, revived, softDeletes.Count, conflicts.Count);
        }

        private static bool tryValidateSnapshot(SkinManagedFolderDiscoverySnapshot snapshot, out ValidatedSnapshot validated)
        {
            validated = null!;
            var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in snapshot.ObservedManagedRelativePaths)
            {
                if (!tryNormaliseManagedRelativePath(path, out string normalisedPath)
                    || !observed.Add(normalisedPath))
                {
                    return false;
                }
            }

            var discoveryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var discoveries = new List<SkinManagedFolderDiscovery>(snapshot.ValidDiscoveries.Count);

            foreach (SkinManagedFolderDiscovery discovery in snapshot.ValidDiscoveries)
            {
                if (discovery == null
                    || discovery.Name == null
                    || discovery.Creator == null
                    || string.IsNullOrEmpty(discovery.ContentRevision)
                    || !tryNormaliseManagedRelativePath(discovery.ManagedRelativePath, out string normalisedPath)
                    || !observed.Contains(normalisedPath)
                    || !discoveryPaths.Add(normalisedPath))
                {
                    return false;
                }

                discoveries.Add(new SkinManagedFolderDiscovery(
                    normalisedPath,
                    discovery.Name,
                    discovery.Creator,
                    discovery.ContentRevision));
            }

            validated = new ValidatedSnapshot(observed, discoveries);
            return true;
        }

        private static bool tryNormaliseManagedRelativePath(string? path, out string normalisedPath)
            => SkinManagedFolderPath.TryNormalise(path, out normalisedPath);

        private static bool hasExactOwner(SkinInfo record)
            => string.Equals(record.FilesystemStorageAuthorityOwner, AUTHORITY_OWNER, StringComparison.Ordinal);

        private static bool isMutableOwnedRecord(SkinInfo record)
            => hasExactOwner(record)
               && !record.IsExternalFilesystemStorage
               && record.Files.Count == 0
               && !record.Protected
               && !SkinFilesystemStorageResolver.IsFixedSkinId(record.ID);

        private sealed class ValidatedSnapshot
        {
            public HashSet<string> ObservedManagedRelativePaths { get; }

            public IReadOnlyList<SkinManagedFolderDiscovery> ValidDiscoveries { get; }

            public ValidatedSnapshot(HashSet<string> observedManagedRelativePaths, IReadOnlyList<SkinManagedFolderDiscovery> validDiscoveries)
            {
                ObservedManagedRelativePaths = observedManagedRelativePaths;
                ValidDiscoveries = validDiscoveries;
            }
        }

        private readonly record struct PlannedUpdate(
            SkinInfo Record,
            SkinManagedFolderDiscovery Discovery,
            bool WasDeletePending);
    }
}
