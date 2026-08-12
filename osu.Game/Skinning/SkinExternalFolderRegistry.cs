// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Framework.Platform;
using osu.Game.Database;
using Realms.Exceptions;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Persistent Realm ownership identity and canonical exact-set version for external folder registrations.
    /// </summary>
    internal static class SkinExternalFolderRegistry
    {
        internal const string AUTHORITY_OWNER = "oms.skin.external-folder.registry.v1";
        internal const int EXACT_SET_VERSION = 1;

        internal static string EmptyRegistryDigest => SkinExternalFolderRegistryService.EmptyRegistryDigest;
    }

    /// <summary>
    /// Aggregate limits for one held exact external-registry set.
    /// </summary>
    internal sealed class SkinExternalFolderRegistryLimits
    {
        public const int DEFAULT_MAX_RECORD_COUNT = 32;
        public const int DEFAULT_MAX_MANAGED_PROOF_COUNT = 8;
        public const int DEFAULT_MAX_TOTAL_PROOF_NODE_COUNT = 2048;
        public const int DEFAULT_MAX_TOTAL_HELD_HANDLE_COUNT = 2048;
        public const int DEFAULT_MAX_TOTAL_PATH_CHARACTERS = 64 * 1024;

        public static SkinExternalFolderRegistryLimits Default { get; } = new SkinExternalFolderRegistryLimits(
            SkinExternalPackageCaptureLimits.Default,
            DEFAULT_MAX_RECORD_COUNT,
            DEFAULT_MAX_MANAGED_PROOF_COUNT,
            DEFAULT_MAX_TOTAL_PROOF_NODE_COUNT,
            DEFAULT_MAX_TOTAL_HELD_HANDLE_COUNT,
            DEFAULT_MAX_TOTAL_PATH_CHARACTERS);

        public SkinExternalPackageCaptureLimits CaptureLimits { get; }

        public int MaxRecordCount { get; }

        public int MaxManagedProofCount { get; }

        public int MaxTotalProofNodeCount { get; }

        public int MaxTotalHeldHandleCount { get; }

        public int MaxTotalPathCharacters { get; }

        public SkinExternalFolderRegistryLimits(
            SkinExternalPackageCaptureLimits captureLimits,
            int maxRecordCount,
            int maxManagedProofCount,
            int maxTotalProofNodeCount,
            int maxTotalHeldHandleCount,
            int maxTotalPathCharacters)
        {
            CaptureLimits = captureLimits ?? throw new ArgumentNullException(nameof(captureLimits));
            ArgumentOutOfRangeException.ThrowIfNegative(maxRecordCount);
            ArgumentOutOfRangeException.ThrowIfNegative(maxManagedProofCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalProofNodeCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalHeldHandleCount);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTotalPathCharacters);

            MaxRecordCount = maxRecordCount;
            MaxManagedProofCount = maxManagedProofCount;
            MaxTotalProofNodeCount = maxTotalProofNodeCount;
            MaxTotalHeldHandleCount = maxTotalHeldHandleCount;
            MaxTotalPathCharacters = maxTotalPathCharacters;
        }

        public override string ToString() => nameof(SkinExternalFolderRegistryLimits);
    }

    internal enum SkinExternalFolderRegistryRejectionReason
    {
        None,
        CoordinatorLeaseMissing,
        RealmReadFailed,
        RecordCountBudgetExceeded,
        ManagedProofCountBudgetExceeded,
        AggregateProofBudgetExceeded,
        AggregatePathBudgetExceeded,
        UntrustedOwner,
        RecordUnresolved,
        LexicalOverlap,
        PhysicalOverlap,
        ManagedAuthorityOverlap,
        CaptureRejected,
    }

    internal sealed class SkinExternalFolderRegistryCaptureResult
    {
        public SkinExternalFolderRegistryRejectionReason RejectionReason { get; }

        public SkinManagedPackageCaptureRejectionReason CaptureRejectionReason { get; }

        public SkinExternalFolderRegistrySnapshot? Snapshot { get; }

        public bool IsSuccess => Snapshot != null;

        private SkinExternalFolderRegistryCaptureResult(
            SkinExternalFolderRegistryRejectionReason rejectionReason,
            SkinManagedPackageCaptureRejectionReason captureRejectionReason,
            SkinExternalFolderRegistrySnapshot? snapshot)
        {
            RejectionReason = rejectionReason;
            CaptureRejectionReason = captureRejectionReason;
            Snapshot = snapshot;
        }

        internal static SkinExternalFolderRegistryCaptureResult Success(SkinExternalFolderRegistrySnapshot snapshot)
            => new SkinExternalFolderRegistryCaptureResult(
                SkinExternalFolderRegistryRejectionReason.None,
                SkinManagedPackageCaptureRejectionReason.None,
                snapshot ?? throw new ArgumentNullException(nameof(snapshot)));

        internal static SkinExternalFolderRegistryCaptureResult Reject(
            SkinExternalFolderRegistryRejectionReason reason,
            SkinManagedPackageCaptureRejectionReason captureReason = SkinManagedPackageCaptureRejectionReason.None)
        {
            if (!Enum.IsDefined(reason) || reason == SkinExternalFolderRegistryRejectionReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));

            if (!Enum.IsDefined(captureReason)
                || (reason == SkinExternalFolderRegistryRejectionReason.CaptureRejected) != (captureReason != SkinManagedPackageCaptureRejectionReason.None))
            {
                throw new ArgumentException("The capture rejection reason does not match the registry rejection.");
            }

            return new SkinExternalFolderRegistryCaptureResult(reason, captureReason, null);
        }

        public override string ToString()
            => $"{nameof(SkinExternalFolderRegistryCaptureResult)}:{RejectionReason}:{CaptureRejectionReason}";
    }

    /// <summary>
    /// One exact service-owned registry set with all external root/ancestry proof sessions still held.
    /// </summary>
    internal sealed class SkinExternalFolderRegistrySnapshot : IDisposable
    {
        private readonly SkinExternalFolderRegistryService owner;
        private SkinExternalFolderRegistryEntry[]? entries;

        public long ExternalRegistryGeneration { get; }

        public string ExternalRegistryDigest { get; }

        internal string DeclarationDigest { get; }

        public int Count => getEntries().Length;

        public bool IsEmpty => Count == 0;

        public int HeldHandleCount => getEntries().Sum(entry => entry.Session.HeldHandleCount);

        internal SkinExternalFolderRegistrySnapshot(
            SkinExternalFolderRegistryService owner,
            SkinExternalFolderRegistryEntry[] entries,
            long externalRegistryGeneration,
            string declarationDigest,
            string externalRegistryDigest)
        {
            this.owner = owner;
            this.entries = entries;
            ExternalRegistryGeneration = externalRegistryGeneration;
            DeclarationDigest = declarationDigest;
            ExternalRegistryDigest = externalRegistryDigest;
        }

        /// <summary>
        /// Revalidates the exact Realm declaration set and every held native proof under the same coordinator lease.
        /// </summary>
        public bool Validate(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkinExternalFolderRegistryEntry[] currentEntries = getEntries();

            if (!owner.IsLeaseHeld(coordinatorLease)
                || !owner.TryReadAndValidateDeclarations(
                    out SkinExternalFolderRegistryDeclaration[] declarations,
                    out string currentDeclarationDigest,
                    out long currentGeneration,
                    out _)
                || currentGeneration != ExternalRegistryGeneration
                || !string.Equals(currentDeclarationDigest, DeclarationDigest, StringComparison.Ordinal)
                || declarations.Length != currentEntries.Length)
            {
                return false;
            }

            for (int i = 0; i < currentEntries.Length; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!currentEntries[i].Declaration.ExactlyMatches(declarations[i]))
                    return false;

                try
                {
                    currentEntries[i].Session.Validate(cancellationToken);
                }
                catch (Windows.WindowsSkinPackageCaptureFileSystemException)
                {
                    return false;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }

            string currentDigest = SkinExternalFolderRegistryService.ComputeRegistryDigest(
                currentDeclarationDigest,
                currentEntries);
            return string.Equals(currentDigest, ExternalRegistryDigest, StringComparison.Ordinal);
        }

        public bool Overlaps(SkinFolderPhysicalAncestryProof managedOrCandidateProof)
        {
            ArgumentNullException.ThrowIfNull(managedOrCandidateProof);
            return getEntries().Any(entry => entry.Session.PhysicalProof.Overlaps(managedOrCandidateProof));
        }

        internal bool ContainsNormalisedPath(string normalisedAbsolutePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(normalisedAbsolutePath);
            return getEntries().Any(entry => string.Equals(
                entry.Declaration.NormalisedAbsolutePath,
                normalisedAbsolutePath,
                StringComparison.OrdinalIgnoreCase));
        }

        internal bool ContainsRecordId(Guid recordId)
            => getEntries().Any(entry => entry.Declaration.RecordId == recordId);

        /// <summary>
        /// Compares the complete Realm external declaration set at an already-authorised transaction linearisation
        /// point. This performs no path resolution and no filesystem I/O.
        /// </summary>
        internal bool ExactlyMatchesRealmDeclarations(IEnumerable<SkinInfo> records)
        {
            ArgumentNullException.ThrowIfNull(records);
            SkinExternalFolderRegistryEntry[] baseline = getEntries();
            var seen = new HashSet<Guid>();
            int externalRecordCount = 0;

            foreach (SkinInfo? record in records)
            {
                if (record?.IsExternalFilesystemStorage != true)
                    continue;

                if (++externalRecordCount > baseline.Length
                    || !seen.Add(record.ID)
                    || record.Protected
                    || record.DeletePending
                    || record.Files.Count != 0
                    || SkinFilesystemStorageResolver.IsFixedSkinId(record.ID)
                    || !string.Equals(
                        record.FilesystemStorageAuthorityOwner,
                        SkinExternalFolderRegistry.AUTHORITY_OWNER,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                SkinExternalFolderRegistryEntry? expected = baseline.SingleOrDefault(entry => entry.Declaration.RecordId == record.ID);

                if (expected == null
                    || !string.Equals(
                        record.FilesystemStoragePath,
                        expected.Declaration.DeclaredPath,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        record.FilesystemStorageAuthorityOwner,
                        expected.Declaration.AuthorityOwner,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return externalRecordCount == baseline.Length && seen.Count == baseline.Length;
        }

        internal bool LexicallyOverlapsNormalisedPath(string normalisedAbsolutePath)
        {
            ArgumentException.ThrowIfNullOrEmpty(normalisedAbsolutePath);
            return getEntries().Any(entry => SkinExternalFolderRegistryService.PathsOverlap(
                entry.Declaration.NormalisedAbsolutePath,
                normalisedAbsolutePath));
        }

        internal bool TryGetPhysicalProof(Guid recordId, out SkinFolderPhysicalAncestryProof? proof)
        {
            SkinExternalFolderRegistryEntry? entry = getEntries().SingleOrDefault(candidate => candidate.Declaration.RecordId == recordId);
            proof = entry?.Session.PhysicalProof;
            return proof != null;
        }

        public void Dispose()
        {
            SkinExternalFolderRegistryEntry[]? owned = Interlocked.Exchange(ref entries, null);

            if (owned == null)
                return;

            Exception? firstException = null;

            for (int i = owned.Length - 1; i >= 0; i--)
            {
                try
                {
                    owned[i].Session.Dispose();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            if (firstException != null)
                ExceptionDispatchInfo.Capture(firstException).Throw();
        }

        private SkinExternalFolderRegistryEntry[] getEntries()
            => entries ?? throw new ObjectDisposedException(nameof(SkinExternalFolderRegistrySnapshot));

        public override string ToString()
            => $"{nameof(SkinExternalFolderRegistrySnapshot)}:Count={entries?.Length ?? 0}:Held={entries != null}";
    }

    /// <summary>
    /// Captures a bounded exact service-owned external registry set while a caller-owned coordinator lease is held.
    /// </summary>
    internal sealed class SkinExternalFolderRegistryService
    {
        private static readonly byte[] declaration_digest_domain = Encoding.ASCII.GetBytes("OMS/SkinExternalFolderRegistryDeclarations/v1\0");
        private static readonly byte[] registry_digest_domain = Encoding.ASCII.GetBytes("OMS/SkinExternalFolderRegistryExactSet/v1\0");
        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);

        internal static string EmptyRegistryDigest { get; } = ComputeRegistryDigest(
            computeDeclarationDigest(Array.Empty<SkinExternalFolderRegistryDeclaration>()),
            Array.Empty<SkinExternalFolderRegistryEntry>());

        private readonly RealmAccess realm;
        private readonly Storage storage;
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly ISkinExternalFolderCaptureService captureService;
        private readonly SkinExternalFolderRegistryLimits limits;

        public SkinExternalFolderRegistryService(
            RealmAccess realm,
            Storage storage,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinExternalFolderCaptureService captureService,
            SkinExternalFolderRegistryLimits? limits = null)
        {
            this.realm = realm ?? throw new ArgumentNullException(nameof(realm));
            this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.captureService = captureService ?? throw new ArgumentNullException(nameof(captureService));
            this.limits = limits ?? SkinExternalFolderRegistryLimits.Default;
        }

        public SkinExternalFolderRegistryCaptureResult CaptureExactSet(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            IReadOnlyList<SkinFolderPhysicalAncestryProof>? managedAncestryProofs = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!IsLeaseHeld(coordinatorLease))
            {
                return SkinExternalFolderRegistryCaptureResult.Reject(
                    SkinExternalFolderRegistryRejectionReason.CoordinatorLeaseMissing);
            }

            return captureExactSet(
                managedAncestryProofs,
                authorityOpened: null,
                cancellationToken);
        }

        /// <summary>
        /// Captures a held external-registry proof for asynchronous selection preparation.
        /// </summary>
        /// <remarks>
        /// This method deliberately grants no publication authority. The returned snapshot must remain held and be
        /// revalidated under a fresh coordinator lease at the final Realm linearisation point. Keeping native capture
        /// outside that lease lets a later update-thread selection advance its generation and cancel this preparation.
        /// </remarks>
        internal SkinExternalFolderRegistryCaptureResult CaptureExactSetForSelection(
            IReadOnlyList<SkinFolderPhysicalAncestryProof>? managedAncestryProofs = null,
            Action? authorityOpened = null,
            CancellationToken cancellationToken = default)
            => captureExactSet(managedAncestryProofs, authorityOpened, cancellationToken);

        private SkinExternalFolderRegistryCaptureResult captureExactSet(
            IReadOnlyList<SkinFolderPhysicalAncestryProof>? managedAncestryProofs,
            Action? authorityOpened,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            managedAncestryProofs ??= Array.Empty<SkinFolderPhysicalAncestryProof>();

            if (managedAncestryProofs.Count > limits.MaxManagedProofCount)
            {
                return SkinExternalFolderRegistryCaptureResult.Reject(
                    SkinExternalFolderRegistryRejectionReason.ManagedProofCountBudgetExceeded);
            }

            if (!TryReadAndValidateDeclarations(
                    out SkinExternalFolderRegistryDeclaration[] declarations,
                    out string declarationDigest,
                    out long generation,
                    out SkinExternalFolderRegistryRejectionReason declarationRejection))
            {
                return SkinExternalFolderRegistryCaptureResult.Reject(declarationRejection);
            }

            var entries = new List<SkinExternalFolderRegistryEntry>(declarations.Length);
            int totalProofNodes = 0;
            int totalHeldHandles = 0;

            try
            {
                foreach (SkinFolderPhysicalAncestryProof managedProof in managedAncestryProofs)
                {
                    if (managedProof == null)
                    {
                        return SkinExternalFolderRegistryCaptureResult.Reject(
                            SkinExternalFolderRegistryRejectionReason.AggregateProofBudgetExceeded);
                    }

                    totalProofNodes = checked(totalProofNodes + managedProof.HeldNodeCount);
                }

                if (totalProofNodes > limits.MaxTotalProofNodeCount)
                {
                    return SkinExternalFolderRegistryCaptureResult.Reject(
                        SkinExternalFolderRegistryRejectionReason.AggregateProofBudgetExceeded);
                }

                foreach (SkinExternalFolderRegistryDeclaration declaration in declarations)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var record = new SkinInfo
                    {
                        ID = declaration.RecordId,
                        FilesystemStoragePath = declaration.DeclaredPath,
                        IsExternalFilesystemStorage = true,
                        FilesystemStorageAuthorityOwner = declaration.AuthorityOwner,
                    };
                    SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(record, storage);

                    if (resolution.Authority != SkinFilesystemStorageAuthority.ExternalFolder
                        || resolution.ExternalCaptureRequest == null
                        || !string.Equals(
                            resolution.NormalisedAbsolutePath,
                            declaration.NormalisedAbsolutePath,
                            StringComparison.Ordinal))
                    {
                        return SkinExternalFolderRegistryCaptureResult.Reject(
                            SkinExternalFolderRegistryRejectionReason.RecordUnresolved);
                    }

                    SkinExternalFolderAuthorityCaptureResult capture = captureService.OpenAuthority(
                        resolution.ExternalCaptureRequest,
                        limits.CaptureLimits,
                        cancellationToken);

                    if (!capture.IsSuccess)
                    {
                        return SkinExternalFolderRegistryCaptureResult.Reject(
                            SkinExternalFolderRegistryRejectionReason.CaptureRejected,
                            capture.RejectionReason);
                    }

                    ISkinExternalFolderAuthoritySession session = capture.Session!;
                    bool sessionOwned = false;

                    try
                    {
                        totalProofNodes = checked(totalProofNodes + session.PhysicalProof.HeldNodeCount);
                        totalHeldHandles = checked(totalHeldHandles + session.HeldHandleCount);

                        if (totalProofNodes > limits.MaxTotalProofNodeCount
                            || totalHeldHandles > limits.MaxTotalHeldHandleCount)
                        {
                            return SkinExternalFolderRegistryCaptureResult.Reject(
                                SkinExternalFolderRegistryRejectionReason.AggregateProofBudgetExceeded);
                        }

                        if (entries.Any(existing => existing.Session.PhysicalProof.Overlaps(session.PhysicalProof)))
                        {
                            return SkinExternalFolderRegistryCaptureResult.Reject(
                                SkinExternalFolderRegistryRejectionReason.PhysicalOverlap);
                        }

                        if (managedAncestryProofs.Any(managed => session.PhysicalProof.Overlaps(managed)))
                        {
                            return SkinExternalFolderRegistryCaptureResult.Reject(
                                SkinExternalFolderRegistryRejectionReason.ManagedAuthorityOverlap);
                        }

                        entries.Add(new SkinExternalFolderRegistryEntry(declaration, session));
                        sessionOwned = true;
                        authorityOpened?.Invoke();
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    finally
                    {
                        if (!sessionOwned)
                            session.Dispose();
                    }
                }

                SkinExternalFolderRegistryEntry[] exactEntries = entries.ToArray();
                string registryDigest = ComputeRegistryDigest(declarationDigest, exactEntries);
                var snapshot = new SkinExternalFolderRegistrySnapshot(
                    this,
                    exactEntries,
                    generation,
                    declarationDigest,
                    registryDigest);
                entries.Clear();
                return SkinExternalFolderRegistryCaptureResult.Success(snapshot);
            }
            catch (OverflowException)
            {
                return SkinExternalFolderRegistryCaptureResult.Reject(
                    SkinExternalFolderRegistryRejectionReason.AggregateProofBudgetExceeded);
            }
            finally
            {
                for (int i = entries.Count - 1; i >= 0; i--)
                    entries[i].Session.Dispose();
            }
        }

        internal bool IsLeaseHeld(SkinManagedFolderOperationCoordinator.Lease? lease)
            => lease?.IsHeldBy(coordinator) == true;

        internal bool TryReadAndValidateDeclarations(
            out SkinExternalFolderRegistryDeclaration[] declarations,
            out string declarationDigest,
            out long generation,
            out SkinExternalFolderRegistryRejectionReason rejectionReason)
        {
            declarations = Array.Empty<SkinExternalFolderRegistryDeclaration>();
            declarationDigest = string.Empty;
            generation = 0;
            rejectionReason = SkinExternalFolderRegistryRejectionReason.RealmReadFailed;
            RawRegistryRecord[] rawRecords;

            try
            {
                int boundedRecordCount = limits.MaxRecordCount == int.MaxValue
                    ? int.MaxValue
                    : limits.MaxRecordCount + 1;
                rawRecords = realm.Run(r => Enumerable.Take(
                                                 (IEnumerable<SkinInfo>)r.All<SkinInfo>()
                                                                          .Where(record => record.IsExternalFilesystemStorage),
                                                 boundedRecordCount)
                                             .Select(record => new RawRegistryRecord(
                                                 record.ID,
                                                 record.FilesystemStoragePath,
                                                 record.FilesystemStorageAuthorityOwner,
                                                 record.Protected,
                                                 record.DeletePending,
                                                 record.Files.Count))
                                             .ToArray());
            }
            catch (Exception exception) when (exception is RealmException or ObjectDisposedException or InvalidOperationException or IOException or SecurityException)
            {
                return false;
            }

            if (rawRecords.Length > limits.MaxRecordCount)
            {
                rejectionReason = SkinExternalFolderRegistryRejectionReason.RecordCountBudgetExceeded;
                return false;
            }

            var validated = new List<SkinExternalFolderRegistryDeclaration>(rawRecords.Length);
            int totalPathCharacters = 0;

            foreach (RawRegistryRecord raw in rawRecords)
            {
                if (!string.Equals(raw.AuthorityOwner, SkinExternalFolderRegistry.AUTHORITY_OWNER, StringComparison.Ordinal))
                {
                    rejectionReason = SkinExternalFolderRegistryRejectionReason.UntrustedOwner;
                    return false;
                }

                if (string.IsNullOrEmpty(raw.DeclaredPath)
                    || raw.Protected
                    || raw.DeletePending
                    || raw.FileCount != 0
                    || SkinFilesystemStorageResolver.IsFixedSkinId(raw.RecordId))
                {
                    rejectionReason = SkinExternalFolderRegistryRejectionReason.RecordUnresolved;
                    return false;
                }

                try
                {
                    totalPathCharacters = checked(totalPathCharacters + raw.DeclaredPath.Length);
                }
                catch (OverflowException)
                {
                    rejectionReason = SkinExternalFolderRegistryRejectionReason.AggregatePathBudgetExceeded;
                    return false;
                }

                if (totalPathCharacters > limits.MaxTotalPathCharacters)
                {
                    rejectionReason = SkinExternalFolderRegistryRejectionReason.AggregatePathBudgetExceeded;
                    return false;
                }

                string normalisedPath;

                try
                {
                    normalisedPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(raw.DeclaredPath));
                    totalPathCharacters = checked(totalPathCharacters + normalisedPath.Length);
                }
                catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException or OverflowException or SecurityException)
                {
                    rejectionReason = exception is OverflowException
                        ? SkinExternalFolderRegistryRejectionReason.AggregatePathBudgetExceeded
                        : SkinExternalFolderRegistryRejectionReason.RecordUnresolved;
                    return false;
                }

                if (totalPathCharacters > limits.MaxTotalPathCharacters)
                {
                    rejectionReason = SkinExternalFolderRegistryRejectionReason.AggregatePathBudgetExceeded;
                    return false;
                }

                validated.Add(new SkinExternalFolderRegistryDeclaration(
                    raw.RecordId,
                    raw.DeclaredPath,
                    normalisedPath,
                    raw.AuthorityOwner!));
            }

            SkinExternalFolderRegistryDeclaration[] ordered = validated.OrderBy(record => record.RecordId.ToString("N"), StringComparer.Ordinal)
                                                       .ToArray();

            for (int i = 0; i < ordered.Length; i++)
            {
                for (int j = i + 1; j < ordered.Length; j++)
                {
                    if (PathsOverlap(ordered[i].NormalisedAbsolutePath, ordered[j].NormalisedAbsolutePath))
                    {
                        rejectionReason = SkinExternalFolderRegistryRejectionReason.LexicalOverlap;
                        return false;
                    }
                }
            }

            try
            {
                declarationDigest = computeDeclarationDigest(ordered);
            }
            catch (EncoderFallbackException)
            {
                rejectionReason = SkinExternalFolderRegistryRejectionReason.RecordUnresolved;
                return false;
            }

            generation = deriveGeneration(declarationDigest, ordered.Length);
            declarations = ordered;
            rejectionReason = SkinExternalFolderRegistryRejectionReason.None;
            return true;
        }

        internal static string ComputeRegistryDigest(
            string declarationDigest,
            IReadOnlyList<SkinExternalFolderRegistryEntry> entries)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(registry_digest_domain);
            appendInt32(hash, SkinExternalFolderRegistry.EXACT_SET_VERSION);
            appendUtf8(hash, declarationDigest);
            appendInt32(hash, entries.Count);

            foreach (SkinExternalFolderRegistryEntry entry in entries)
            {
                appendUtf8(hash, entry.Declaration.RecordId.ToString("N"));
                appendUtf8(hash, entry.Session.PhysicalProof.Digest);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static string computeDeclarationDigest(IReadOnlyList<SkinExternalFolderRegistryDeclaration> declarations)
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            hash.AppendData(declaration_digest_domain);
            appendInt32(hash, SkinExternalFolderRegistry.EXACT_SET_VERSION);
            appendInt32(hash, declarations.Count);

            foreach (SkinExternalFolderRegistryDeclaration declaration in declarations)
            {
                appendUtf8(hash, declaration.RecordId.ToString("N"));
                appendUtf8(hash, declaration.DeclaredPath);
                appendUtf8(hash, declaration.NormalisedAbsolutePath);
                appendUtf8(hash, declaration.AuthorityOwner);
            }

            return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
        }

        private static long deriveGeneration(string declarationDigest, int recordCount)
        {
            if (recordCount == 0)
                return 0;

            byte[] digest;

            try
            {
                digest = Convert.FromHexString(declarationDigest);
            }
            catch (FormatException exception)
            {
                throw new InvalidOperationException("The external registry declaration digest is invalid.", exception);
            }

            if (digest.Length != 32)
                throw new InvalidOperationException("The external registry declaration digest is invalid.");

            long generation = BinaryPrimitives.ReadInt64BigEndian(digest) & long.MaxValue;
            return generation == 0 ? 1 : generation;
        }

        internal static bool PathsOverlap(string left, string right)
            => string.Equals(left, right, StringComparison.OrdinalIgnoreCase)
               || isStrictChildOf(left, right)
               || isStrictChildOf(right, left);

        private static bool isStrictChildOf(string candidate, string root)
        {
            if (string.Equals(candidate, root, StringComparison.OrdinalIgnoreCase))
                return false;

            string rootWithSeparator = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;
            return candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static void appendInt32(IncrementalHash hash, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            hash.AppendData(bytes);
        }

        private static void appendUtf8(IncrementalHash hash, string value)
        {
            byte[] bytes = strict_utf8.GetBytes(value);
            appendInt32(hash, bytes.Length);
            hash.AppendData(bytes);
        }

        public override string ToString() => nameof(SkinExternalFolderRegistryService);
    }

    internal sealed class SkinExternalFolderRegistryEntry
    {
        public SkinExternalFolderRegistryDeclaration Declaration { get; }

        public ISkinExternalFolderAuthoritySession Session { get; }

        public SkinExternalFolderRegistryEntry(
            SkinExternalFolderRegistryDeclaration declaration,
            ISkinExternalFolderAuthoritySession session)
        {
            Declaration = declaration;
            Session = session;
        }

        public override string ToString() => nameof(SkinExternalFolderRegistryEntry);
    }

    internal sealed class SkinExternalFolderRegistryDeclaration
    {
        public Guid RecordId { get; }

        public string DeclaredPath { get; }

        public string NormalisedAbsolutePath { get; }

        public string AuthorityOwner { get; }

        public SkinExternalFolderRegistryDeclaration(
            Guid recordId,
            string declaredPath,
            string normalisedAbsolutePath,
            string authorityOwner)
        {
            RecordId = recordId;
            DeclaredPath = declaredPath;
            NormalisedAbsolutePath = normalisedAbsolutePath;
            AuthorityOwner = authorityOwner;
        }

        public bool ExactlyMatches(SkinExternalFolderRegistryDeclaration other)
            => RecordId == other.RecordId
               && string.Equals(DeclaredPath, other.DeclaredPath, StringComparison.Ordinal)
               && string.Equals(NormalisedAbsolutePath, other.NormalisedAbsolutePath, StringComparison.Ordinal)
               && string.Equals(AuthorityOwner, other.AuthorityOwner, StringComparison.Ordinal);

        public override string ToString() => nameof(SkinExternalFolderRegistryDeclaration);
    }

    internal readonly record struct RawRegistryRecord(
        Guid RecordId,
        string? DeclaredPath,
        string? AuthorityOwner,
        bool Protected,
        bool DeletePending,
        int FileCount);
}
