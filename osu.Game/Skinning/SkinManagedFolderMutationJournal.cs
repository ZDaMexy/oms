// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.Platform;

namespace osu.Game.Skinning
{
    internal sealed class SkinManagedFolderMutationJournalException : Exception
    {
        public SkinManagedFolderMutationJournalException()
            : base(nameof(SkinManagedFolderMutationJournalException))
        {
        }

        public override string ToString() => nameof(SkinManagedFolderMutationJournalException);
    }

    internal enum SkinManagedFolderMutationKind
    {
        Rename = 1,
        StagedImport = 2,
        Delete = 3,
    }

    internal enum SkinManagedFolderMutationPhase
    {
        Prepared = 1,
        FilesystemApplied = 2,
        RealmApplied = 3,
        Committed = 4,
        RolledBack = 5,
    }

    [JsonObject(MemberSerialization.OptIn)]
    internal readonly struct SkinManagedFolderPhysicalIdentity : IEquatable<SkinManagedFolderPhysicalIdentity>
    {
        [JsonProperty(nameof(VolumeSerialNumber), Required = Required.Always)]
        public ulong VolumeSerialNumber { get; }

        [JsonProperty(nameof(FileIdPart0), Required = Required.Always)]
        public ulong FileIdPart0 { get; }

        [JsonProperty(nameof(FileIdPart1), Required = Required.Always)]
        public ulong FileIdPart1 { get; }

        [JsonIgnore]
        public bool IsUsable => VolumeSerialNumber != 0 && (FileIdPart0 != 0 || FileIdPart1 != 0);

        [JsonConstructor]
        public SkinManagedFolderPhysicalIdentity(ulong volumeSerialNumber, ulong fileIdPart0, ulong fileIdPart1)
        {
            VolumeSerialNumber = volumeSerialNumber;
            FileIdPart0 = fileIdPart0;
            FileIdPart1 = fileIdPart1;
        }

        public bool Equals(SkinManagedFolderPhysicalIdentity other)
            => VolumeSerialNumber == other.VolumeSerialNumber
               && FileIdPart0 == other.FileIdPart0
               && FileIdPart1 == other.FileIdPart1;

        public override bool Equals(object? obj) => obj is SkinManagedFolderPhysicalIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(VolumeSerialNumber, FileIdPart0, FileIdPart1);

        public static bool operator ==(SkinManagedFolderPhysicalIdentity left, SkinManagedFolderPhysicalIdentity right) => left.Equals(right);

        public static bool operator !=(SkinManagedFolderPhysicalIdentity left, SkinManagedFolderPhysicalIdentity right) => !left.Equals(right);

        public override string ToString() => nameof(SkinManagedFolderPhysicalIdentity);
    }

    /// <summary>
    /// Versioned durable intent for one managed-folder mutation.
    /// </summary>
    /// <remarks>
    /// Relative slots and physical identities are recovery data and must not be included in logs or safe diagnostics.
    /// The journal intentionally has no public path-based mutation API.
    /// </remarks>
    [JsonObject(MemberSerialization.OptIn)]
    internal sealed class SkinManagedFolderMutationJournal
    {
        public const int LEGACY_VERSION = 1;
        public const int CURRENT_VERSION = 2;
        public const string STAGED_SOURCE_AUTHORITY = "oms.skin.managed-folder.staging.v1";
        public const string NEW_RECORD_PUBLICATION_PLAN_VERSION = "oms.skin.managed-folder.scanner-publication-plan.v1";
        private const string delete_tombstone_prefix = ".oms-delete-";

        [JsonProperty(nameof(Version), Required = Required.Always)]
        public int Version { get; }

        [JsonProperty(nameof(OperationId), Required = Required.Always)]
        public Guid OperationId { get; }

        [JsonProperty(nameof(Kind), Required = Required.Always)]
        public SkinManagedFolderMutationKind Kind { get; }

        [JsonProperty(nameof(Phase), Required = Required.Always)]
        public SkinManagedFolderMutationPhase Phase { get; }

        [JsonProperty(nameof(RecordId), Required = Required.AllowNull)]
        public Guid? RecordId { get; }

        [JsonProperty(nameof(ManagedRootIdentity), Required = Required.Always)]
        public SkinManagedFolderPhysicalIdentity ManagedRootIdentity { get; }

        [JsonProperty(nameof(SourceManagedRelativePath), Required = Required.AllowNull)]
        public string? SourceManagedRelativePath { get; }

        [JsonProperty(nameof(TargetManagedRelativePath), Required = Required.AllowNull)]
        public string? TargetManagedRelativePath { get; }

        [JsonProperty(nameof(SourceIdentity), Required = Required.AllowNull)]
        public SkinManagedFolderPhysicalIdentity? SourceIdentity { get; }

        [JsonProperty(nameof(TargetIdentity), Required = Required.AllowNull)]
        public SkinManagedFolderPhysicalIdentity? TargetIdentity { get; }

        [JsonProperty(nameof(StagedSourceAuthority), Required = Required.AllowNull)]
        public string? StagedSourceAuthority { get; }

        [JsonProperty(nameof(StagedSourceRelativePath), Required = Required.AllowNull)]
        public string? StagedSourceRelativePath { get; }

        [JsonProperty(nameof(StagedSourceIdentity), Required = Required.AllowNull)]
        public SkinManagedFolderPhysicalIdentity? StagedSourceIdentity { get; }

        [JsonProperty(nameof(StagedRootIdentity), Required = Required.AllowNull)]
        public SkinManagedFolderPhysicalIdentity? StagedRootIdentity { get; }

        [JsonProperty(nameof(NewRecordPublicationPlanVersion), Required = Required.AllowNull)]
        public string? NewRecordPublicationPlanVersion { get; }

        [JsonProperty(nameof(StagedSourceContentRevision), Required = Required.Default)]
        public string? StagedSourceContentRevision { get; }

        [JsonProperty(nameof(StagedSourceTreeFingerprint), Required = Required.Default)]
        public string? StagedSourceTreeFingerprint { get; }

        [JsonProperty(
            nameof(NewRecordPublicationFingerprint),
            Required = Required.Default,
            NullValueHandling = NullValueHandling.Ignore)]
        public string? NewRecordPublicationFingerprint { get; }

        [JsonProperty(
            nameof(DeleteSourceNodeManifest),
            Required = Required.Default,
            NullValueHandling = NullValueHandling.Ignore)]
        public string? DeleteSourceNodeManifest { get; }

        [JsonProperty(
            nameof(DeleteFallbackDisposition),
            Required = Required.Default,
            NullValueHandling = NullValueHandling.Ignore)]
        public SkinManagedFolderDeleteFallbackDisposition? DeleteFallbackDisposition { get; }

        [JsonConstructor]
        private SkinManagedFolderMutationJournal(
            int version,
            Guid operationId,
            SkinManagedFolderMutationKind kind,
            SkinManagedFolderMutationPhase phase,
            Guid? recordId,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            string? sourceManagedRelativePath,
            string? targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity? sourceIdentity,
            SkinManagedFolderPhysicalIdentity? targetIdentity,
            string? stagedSourceAuthority,
            string? stagedSourceRelativePath,
            SkinManagedFolderPhysicalIdentity? stagedSourceIdentity,
            SkinManagedFolderPhysicalIdentity? stagedRootIdentity,
            string? newRecordPublicationPlanVersion,
            string? stagedSourceContentRevision = null,
            string? stagedSourceTreeFingerprint = null,
            string? newRecordPublicationFingerprint = null,
            string? deleteSourceNodeManifest = null,
            SkinManagedFolderDeleteFallbackDisposition? deleteFallbackDisposition = null)
        {
            Version = version;
            OperationId = operationId;
            Kind = kind;
            Phase = phase;
            RecordId = recordId;
            ManagedRootIdentity = managedRootIdentity;
            SourceManagedRelativePath = sourceManagedRelativePath;
            TargetManagedRelativePath = targetManagedRelativePath;
            SourceIdentity = sourceIdentity;
            TargetIdentity = targetIdentity;
            StagedSourceAuthority = stagedSourceAuthority;
            StagedSourceRelativePath = stagedSourceRelativePath;
            StagedSourceIdentity = stagedSourceIdentity;
            StagedRootIdentity = stagedRootIdentity;
            NewRecordPublicationPlanVersion = newRecordPublicationPlanVersion;
            StagedSourceContentRevision = stagedSourceContentRevision;
            StagedSourceTreeFingerprint = stagedSourceTreeFingerprint;
            NewRecordPublicationFingerprint = newRecordPublicationFingerprint;
            DeleteSourceNodeManifest = deleteSourceNodeManifest;
            DeleteFallbackDisposition = deleteFallbackDisposition;
        }

        public static SkinManagedFolderMutationJournal CreatePreparedRename(
            Guid operationId,
            Guid recordId,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            string sourceManagedRelativePath,
            SkinManagedFolderPhysicalIdentity sourceIdentity,
            string targetManagedRelativePath)
            => createValidated(
                CURRENT_VERSION,
                operationId,
                SkinManagedFolderMutationKind.Rename,
                SkinManagedFolderMutationPhase.Prepared,
                recordId,
                managedRootIdentity,
                sourceManagedRelativePath,
                targetManagedRelativePath,
                sourceIdentity,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null);

        public static SkinManagedFolderMutationJournal CreatePreparedDelete(
            Guid operationId,
            Guid recordId,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            string sourceManagedRelativePath,
            SkinManagedFolderPhysicalIdentity sourceIdentity,
            string existingRecordFingerprint,
            string deleteSourceNodeManifest)
            => createValidated(
                CURRENT_VERSION,
                operationId,
                SkinManagedFolderMutationKind.Delete,
                SkinManagedFolderMutationPhase.Prepared,
                recordId,
                managedRootIdentity,
                sourceManagedRelativePath,
                GetExpectedDeleteTombstoneRelativePath(operationId),
                sourceIdentity,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                existingRecordFingerprint,
                deleteSourceNodeManifest,
                null);

        public static SkinManagedFolderMutationJournal CreatePreparedStagedImport(
            Guid operationId,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            string targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity stagedSourceIdentity,
            SkinManagedFolderPhysicalIdentity stagedRootIdentity,
            string stagedSourceContentRevision,
            string stagedSourceTreeFingerprint)
            => createValidated(
                CURRENT_VERSION,
                operationId,
                SkinManagedFolderMutationKind.StagedImport,
                SkinManagedFolderMutationPhase.Prepared,
                operationId,
                managedRootIdentity,
                null,
                targetManagedRelativePath,
                null,
                null,
                STAGED_SOURCE_AUTHORITY,
                GetExpectedStagedSourceRelativePath(operationId),
                stagedSourceIdentity,
                stagedRootIdentity,
                NEW_RECORD_PUBLICATION_PLAN_VERSION,
                stagedSourceContentRevision,
                stagedSourceTreeFingerprint,
                null,
                null,
                null);

        public SkinManagedFolderMutationJournal WithFilesystemApplied(
            SkinManagedFolderPhysicalIdentity? targetIdentity = null,
            string? newRecordPublicationFingerprint = null)
        {
            if (Phase != SkinManagedFolderMutationPhase.Prepared)
                throw new InvalidOperationException("The managed-folder mutation phase transition is invalid.");

            if (Kind == SkinManagedFolderMutationKind.Rename)
            {
                if (targetIdentity == null || targetIdentity.Value != SourceIdentity)
                    throw new InvalidOperationException("The managed-folder move did not preserve physical identity.");
            }
            else if (Kind == SkinManagedFolderMutationKind.StagedImport)
            {
                if (targetIdentity is not { IsUsable: true }
                    || targetIdentity != StagedSourceIdentity
                    || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        newRecordPublicationFingerprint))
                {
                    throw new InvalidOperationException("The staged import target publication is invalid.");
                }
            }
            else if (Kind == SkinManagedFolderMutationKind.Delete
                     && (targetIdentity != null
                         || (newRecordPublicationFingerprint != null
                             && !string.Equals(
                                 newRecordPublicationFingerprint,
                                 NewRecordPublicationFingerprint,
                                 StringComparison.Ordinal))))
                throw new InvalidOperationException("A managed-folder delete cannot publish a target identity.");

            if (Kind == SkinManagedFolderMutationKind.Rename && newRecordPublicationFingerprint != null)
                throw new InvalidOperationException("A managed-folder rename cannot publish a new record.");

            return createWithPhase(
                SkinManagedFolderMutationPhase.FilesystemApplied,
                targetIdentity,
                newRecordPublicationFingerprint);
        }

        public SkinManagedFolderMutationJournal WithDeleteFallbackDisposition(
            SkinManagedFolderDeleteFallbackDisposition disposition)
        {
            if (Kind != SkinManagedFolderMutationKind.Delete
                || Phase != SkinManagedFolderMutationPhase.Prepared
                || DeleteFallbackDisposition != null
                || !Enum.IsDefined(disposition))
            {
                throw new InvalidOperationException(
                    "The managed-folder delete fallback disposition cannot be changed.");
            }

            return createValidated(
                Version,
                OperationId,
                Kind,
                Phase,
                RecordId,
                ManagedRootIdentity,
                SourceManagedRelativePath,
                TargetManagedRelativePath,
                SourceIdentity,
                TargetIdentity,
                StagedSourceAuthority,
                StagedSourceRelativePath,
                StagedSourceIdentity,
                StagedRootIdentity,
                NewRecordPublicationPlanVersion,
                StagedSourceContentRevision,
                StagedSourceTreeFingerprint,
                NewRecordPublicationFingerprint,
                DeleteSourceNodeManifest,
                disposition);
        }

        public SkinManagedFolderMutationJournal WithRealmApplied()
        {
            if (Phase != SkinManagedFolderMutationPhase.FilesystemApplied)
                throw new InvalidOperationException("The managed-folder mutation phase transition is invalid.");

            return createWithPhase(
                SkinManagedFolderMutationPhase.RealmApplied,
                TargetIdentity,
                NewRecordPublicationFingerprint);
        }

        public SkinManagedFolderMutationJournal WithCommitted()
        {
            if (Phase != SkinManagedFolderMutationPhase.RealmApplied)
                throw new InvalidOperationException("The managed-folder mutation phase transition is invalid.");

            return createWithPhase(
                SkinManagedFolderMutationPhase.Committed,
                TargetIdentity,
                NewRecordPublicationFingerprint);
        }

        public SkinManagedFolderMutationJournal WithRolledBack()
        {
            if (Phase is SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack)
                throw new InvalidOperationException("A terminal managed-folder mutation journal cannot be reopened.");

            return createWithPhase(
                SkinManagedFolderMutationPhase.RolledBack,
                TargetIdentity,
                NewRecordPublicationFingerprint);
        }

        internal SkinManagedFolderMutationJournal WithRecoveryTerminalPhase(
            SkinManagedFolderMutationPhase terminalPhase,
            SkinManagedFolderPhysicalIdentity? recoveredTargetIdentity = null,
            string? recoveredNewRecordPublicationFingerprint = null)
        {
            if (Phase is SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack
                || terminalPhase is not (SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack))
            {
                throw new InvalidOperationException("The managed-folder recovery transition is invalid.");
            }

            if (terminalPhase == SkinManagedFolderMutationPhase.Committed
                && Version == CURRENT_VERSION
                && Phase != SkinManagedFolderMutationPhase.RealmApplied)
            {
                throw new InvalidOperationException("A current managed-folder mutation must durably pass through every forward phase.");
            }

            if (TargetIdentity != null
                && recoveredTargetIdentity != null
                && TargetIdentity != recoveredTargetIdentity)
            {
                throw new InvalidOperationException("The recovered managed-folder target identity changed.");
            }

            if (NewRecordPublicationFingerprint != null
                && recoveredNewRecordPublicationFingerprint != null
                && !string.Equals(
                    NewRecordPublicationFingerprint,
                    recoveredNewRecordPublicationFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException("The recovered managed-folder publication changed.");
            }

            SkinManagedFolderPhysicalIdentity? finalTargetIdentity =
                TargetIdentity ?? recoveredTargetIdentity;
            string? finalPublicationFingerprint =
                NewRecordPublicationFingerprint
                ?? recoveredNewRecordPublicationFingerprint;

            if (terminalPhase == SkinManagedFolderMutationPhase.Committed
                && Kind == SkinManagedFolderMutationKind.Rename)
            {
                if (finalTargetIdentity != SourceIdentity)
                    throw new InvalidOperationException("The recovered managed-folder move changed physical identity.");
            }

            if (terminalPhase == SkinManagedFolderMutationPhase.Committed
                && Kind == SkinManagedFolderMutationKind.StagedImport
                && (finalTargetIdentity is not { } stagedTarget
                    || stagedTarget != StagedSourceIdentity
                    || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        finalPublicationFingerprint)))
            {
                throw new InvalidOperationException("The recovered staged import publication is invalid.");
            }

            return createWithPhase(
                terminalPhase,
                finalTargetIdentity,
                finalPublicationFingerprint);
        }

        public IReadOnlyList<string> GetAffectedManagedRelativePaths()
        {
            var paths = new List<string>(2);

            if (SourceManagedRelativePath != null)
                paths.Add(SourceManagedRelativePath);

            if (TargetManagedRelativePath != null
                && !paths.Contains(TargetManagedRelativePath, StringComparer.OrdinalIgnoreCase))
            {
                paths.Add(TargetManagedRelativePath);
            }

            return paths.AsReadOnly();
        }

        public bool IsValid()
        {
            if (Version is not (LEGACY_VERSION or CURRENT_VERSION)
                || OperationId == Guid.Empty
                || !Enum.IsDefined(Kind)
                || !Enum.IsDefined(Phase)
                || Phase == 0
                || !ManagedRootIdentity.IsUsable)
            {
                return false;
            }

            bool sourcePathValid = SourceManagedRelativePath == null
                                   || (SkinManagedFolderPath.TryNormalise(SourceManagedRelativePath, out string normalisedSource)
                                       && string.Equals(SourceManagedRelativePath, normalisedSource, StringComparison.Ordinal));
            bool targetPathValid = TargetManagedRelativePath == null
                                   || (SkinManagedFolderPath.TryNormalise(TargetManagedRelativePath, out string normalisedTarget)
                                       && string.Equals(TargetManagedRelativePath, normalisedTarget, StringComparison.Ordinal));

            if (!sourcePathValid
                || !targetPathValid
                || SourceIdentity is { IsUsable: false }
                || TargetIdentity is { IsUsable: false }
                || StagedSourceIdentity is { IsUsable: false }
                || StagedRootIdentity is { IsUsable: false }
                || (StagedSourceContentRevision != null
                    && !IsValidContentRevision(StagedSourceContentRevision))
                || (StagedSourceTreeFingerprint != null
                    && !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        StagedSourceTreeFingerprint))
                || (NewRecordPublicationFingerprint != null
                    && !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        NewRecordPublicationFingerprint))
                || (DeleteSourceNodeManifest != null
                    && !SkinManagedFolderDeleteManifest.IsValid(DeleteSourceNodeManifest))
                || (DeleteFallbackDisposition != null
                    && !Enum.IsDefined(DeleteFallbackDisposition.Value))
                || (Kind != SkinManagedFolderMutationKind.Delete
                    && Phase == SkinManagedFolderMutationPhase.Prepared
                    && (TargetIdentity != null
                        || NewRecordPublicationFingerprint != null)))
            {
                return false;
            }

            if (Phase is SkinManagedFolderMutationPhase.FilesystemApplied
                    or SkinManagedFolderMutationPhase.RealmApplied
                    or SkinManagedFolderMutationPhase.Committed
                && Kind is SkinManagedFolderMutationKind.Rename or SkinManagedFolderMutationKind.StagedImport
                && TargetIdentity == null)
            {
                return false;
            }

            return Kind switch
            {
                SkinManagedFolderMutationKind.Rename =>
                    RecordId is { } renameRecordId
                    && renameRecordId != Guid.Empty
                    && SourceManagedRelativePath != null
                    && TargetManagedRelativePath != null
                    && !string.Equals(SourceManagedRelativePath, TargetManagedRelativePath, StringComparison.OrdinalIgnoreCase)
                    && SourceIdentity is { IsUsable: true }
                    && SourceIdentity.Value.VolumeSerialNumber == ManagedRootIdentity.VolumeSerialNumber
                    && (TargetIdentity == null || TargetIdentity == SourceIdentity)
                    && StagedSourceAuthority == null
                    && StagedSourceRelativePath == null
                    && StagedSourceIdentity == null
                    && StagedRootIdentity == null
                    && NewRecordPublicationPlanVersion == null
                    && StagedSourceContentRevision == null
                    && StagedSourceTreeFingerprint == null
                    && NewRecordPublicationFingerprint == null
                    && DeleteSourceNodeManifest == null
                    && DeleteFallbackDisposition == null,

                SkinManagedFolderMutationKind.StagedImport =>
                    RecordId == OperationId
                    && !SkinFilesystemStorageResolver.IsFixedSkinId(OperationId)
                    && SourceManagedRelativePath == null
                    && SourceIdentity == null
                    && TargetManagedRelativePath != null
                    && string.Equals(StagedSourceAuthority, STAGED_SOURCE_AUTHORITY, StringComparison.Ordinal)
                    && string.Equals(StagedSourceRelativePath, GetExpectedStagedSourceRelativePath(OperationId), StringComparison.Ordinal)
                    && StagedSourceIdentity is { IsUsable: true }
                    && StagedRootIdentity is { IsUsable: true }
                    && StagedSourceIdentity.Value.VolumeSerialNumber == ManagedRootIdentity.VolumeSerialNumber
                    && StagedRootIdentity.Value.VolumeSerialNumber == ManagedRootIdentity.VolumeSerialNumber
                    && (TargetIdentity == null
                        || TargetIdentity == StagedSourceIdentity)
                    && string.Equals(NewRecordPublicationPlanVersion, NEW_RECORD_PUBLICATION_PLAN_VERSION, StringComparison.Ordinal)
                    && (Version == LEGACY_VERSION
                        ? StagedSourceContentRevision == null
                          && StagedSourceTreeFingerprint == null
                        : IsValidContentRevision(StagedSourceContentRevision)
                          && SkinManagedFolderNewRecordPublicationData
                              .IsValidFingerprint(
                                  StagedSourceTreeFingerprint))
                    && hasValidStagedPublicationState()
                    && DeleteSourceNodeManifest == null
                    && DeleteFallbackDisposition == null,

                SkinManagedFolderMutationKind.Delete =>
                    RecordId is { } deleteRecordId
                    && deleteRecordId != Guid.Empty
                    && SourceManagedRelativePath != null
                    && string.Equals(
                        TargetManagedRelativePath,
                        GetExpectedDeleteTombstoneRelativePath(OperationId),
                        StringComparison.Ordinal)
                    && !string.Equals(
                        SourceManagedRelativePath,
                        TargetManagedRelativePath,
                        StringComparison.OrdinalIgnoreCase)
                    && SourceIdentity is { IsUsable: true }
                    && SourceIdentity.Value.VolumeSerialNumber == ManagedRootIdentity.VolumeSerialNumber
                    && TargetIdentity == null
                    && StagedSourceAuthority == null
                    && StagedSourceRelativePath == null
                    && StagedSourceIdentity == null
                    && StagedRootIdentity == null
                    && NewRecordPublicationPlanVersion == null
                    && StagedSourceContentRevision == null
                    && StagedSourceTreeFingerprint == null
                    && SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        NewRecordPublicationFingerprint)
                    && SkinManagedFolderDeleteManifest.IsValid(DeleteSourceNodeManifest)
                    && hasValidDeleteFallbackState(),

                _ => false,
            };
        }

        public override string ToString()
            => $"{nameof(SkinManagedFolderMutationJournal)}:V{Version}:{Kind}:{Phase}";

        public bool ShouldSerializeStagedSourceContentRevision()
            => Version != LEGACY_VERSION;

        public bool ShouldSerializeStagedSourceTreeFingerprint()
            => Version != LEGACY_VERSION;

        private bool hasValidDeleteFallbackState()
            => Phase switch
            {
                SkinManagedFolderMutationPhase.Prepared
                    or SkinManagedFolderMutationPhase.RolledBack => true,

                SkinManagedFolderMutationPhase.FilesystemApplied
                    or SkinManagedFolderMutationPhase.RealmApplied
                    or SkinManagedFolderMutationPhase.Committed =>
                    DeleteFallbackDisposition != null,

                _ => false,
            };

        private bool hasValidStagedPublicationState()
        {
            if (Version == LEGACY_VERSION)
                return NewRecordPublicationFingerprint == null;

            bool hasTarget = TargetIdentity != null;
            bool hasPublicationFingerprint =
                SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    NewRecordPublicationFingerprint);

            return Phase switch
            {
                SkinManagedFolderMutationPhase.Prepared =>
                    !hasTarget && NewRecordPublicationFingerprint == null,

                SkinManagedFolderMutationPhase.FilesystemApplied
                    or SkinManagedFolderMutationPhase.RealmApplied
                    or SkinManagedFolderMutationPhase.Committed =>
                    hasTarget && hasPublicationFingerprint,

                SkinManagedFolderMutationPhase.RolledBack =>
                    hasTarget
                        ? hasPublicationFingerprint
                        : NewRecordPublicationFingerprint == null,

                _ => false,
            };
        }

        internal static string GetExpectedStagedSourceRelativePath(Guid operationId)
            => $"skin-mutation-staging/{operationId:N}";

        internal static string GetExpectedDeleteTombstoneRelativePath(Guid operationId)
        {
            if (operationId == Guid.Empty)
                throw new ArgumentException("The managed-folder delete operation ID is invalid.", nameof(operationId));

            return $"{SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY}/{delete_tombstone_prefix}{operationId:N}";
        }

        internal static bool IsValidContentRevision(string? contentRevision)
            => contentRevision is { Length: 64 }
               && contentRevision.All(character => character is >= '0' and <= '9'
                   or >= 'A' and <= 'F');

        internal bool IsSameMonotonicIntent(SkinManagedFolderMutationJournal candidate)
        {
            ArgumentNullException.ThrowIfNull(candidate);

            return Version == candidate.Version
                   && OperationId == candidate.OperationId
                   && Kind == candidate.Kind
                   && RecordId == candidate.RecordId
                   && ManagedRootIdentity == candidate.ManagedRootIdentity
                   && string.Equals(SourceManagedRelativePath, candidate.SourceManagedRelativePath, StringComparison.Ordinal)
                   && string.Equals(TargetManagedRelativePath, candidate.TargetManagedRelativePath, StringComparison.Ordinal)
                   && SourceIdentity == candidate.SourceIdentity
                   && (TargetIdentity == null || TargetIdentity == candidate.TargetIdentity)
                   && string.Equals(StagedSourceAuthority, candidate.StagedSourceAuthority, StringComparison.Ordinal)
                   && string.Equals(StagedSourceRelativePath, candidate.StagedSourceRelativePath, StringComparison.Ordinal)
                   && StagedSourceIdentity == candidate.StagedSourceIdentity
                   && StagedRootIdentity == candidate.StagedRootIdentity
                   && string.Equals(NewRecordPublicationPlanVersion, candidate.NewRecordPublicationPlanVersion, StringComparison.Ordinal)
                   && string.Equals(
                       StagedSourceContentRevision,
                       candidate.StagedSourceContentRevision,
                       StringComparison.Ordinal)
                   && string.Equals(
                       StagedSourceTreeFingerprint,
                       candidate.StagedSourceTreeFingerprint,
                       StringComparison.Ordinal)
                   && string.Equals(
                       DeleteSourceNodeManifest,
                       candidate.DeleteSourceNodeManifest,
                       StringComparison.Ordinal)
                   && (DeleteFallbackDisposition == null
                       || DeleteFallbackDisposition == candidate.DeleteFallbackDisposition)
                   && (NewRecordPublicationFingerprint == null
                       || string.Equals(
                           NewRecordPublicationFingerprint,
                           candidate.NewRecordPublicationFingerprint,
                           StringComparison.Ordinal))
                   && isAllowedPhaseProgression(candidate.Phase)
                   && (Phase != candidate.Phase
                       || (TargetIdentity == candidate.TargetIdentity
                           && string.Equals(
                               NewRecordPublicationFingerprint,
                               candidate.NewRecordPublicationFingerprint,
                               StringComparison.Ordinal)));
        }

        internal bool IsExactSameJournal(SkinManagedFolderMutationJournal candidate)
            => IsSameMonotonicIntent(candidate)
               && candidate.IsSameMonotonicIntent(this)
               && Phase == candidate.Phase
               && TargetIdentity == candidate.TargetIdentity
               && string.Equals(
                   NewRecordPublicationFingerprint,
                   candidate.NewRecordPublicationFingerprint,
                   StringComparison.Ordinal)
               && string.Equals(
                   DeleteSourceNodeManifest,
                   candidate.DeleteSourceNodeManifest,
                   StringComparison.Ordinal)
               && DeleteFallbackDisposition == candidate.DeleteFallbackDisposition;

        private bool isAllowedPhaseProgression(
            SkinManagedFolderMutationPhase candidate)
        {
            if (Version == LEGACY_VERSION)
            {
                return Phase switch
                {
                    SkinManagedFolderMutationPhase.Prepared =>
                        candidate is SkinManagedFolderMutationPhase.Prepared
                            or SkinManagedFolderMutationPhase.FilesystemApplied
                            or SkinManagedFolderMutationPhase.Committed
                            or SkinManagedFolderMutationPhase.RolledBack,

                    SkinManagedFolderMutationPhase.FilesystemApplied =>
                        candidate is SkinManagedFolderMutationPhase.FilesystemApplied
                            or SkinManagedFolderMutationPhase.RealmApplied
                            or SkinManagedFolderMutationPhase.Committed
                            or SkinManagedFolderMutationPhase.RolledBack,

                    SkinManagedFolderMutationPhase.RealmApplied =>
                        candidate is SkinManagedFolderMutationPhase.RealmApplied
                            or SkinManagedFolderMutationPhase.Committed
                            or SkinManagedFolderMutationPhase.RolledBack,

                    SkinManagedFolderMutationPhase.Committed =>
                        candidate == SkinManagedFolderMutationPhase.Committed,

                    SkinManagedFolderMutationPhase.RolledBack =>
                        candidate == SkinManagedFolderMutationPhase.RolledBack,

                    _ => false,
                };
            }

            return Phase switch
            {
                SkinManagedFolderMutationPhase.Prepared =>
                    candidate is SkinManagedFolderMutationPhase.Prepared
                        or SkinManagedFolderMutationPhase.FilesystemApplied
                        or SkinManagedFolderMutationPhase.RolledBack,

                SkinManagedFolderMutationPhase.FilesystemApplied =>
                    candidate is SkinManagedFolderMutationPhase.FilesystemApplied
                        or SkinManagedFolderMutationPhase.RealmApplied
                        or SkinManagedFolderMutationPhase.RolledBack,

                SkinManagedFolderMutationPhase.RealmApplied =>
                    candidate is SkinManagedFolderMutationPhase.RealmApplied
                        or SkinManagedFolderMutationPhase.Committed
                        or SkinManagedFolderMutationPhase.RolledBack,

                SkinManagedFolderMutationPhase.Committed =>
                    candidate == SkinManagedFolderMutationPhase.Committed,

                SkinManagedFolderMutationPhase.RolledBack =>
                    candidate == SkinManagedFolderMutationPhase.RolledBack,

                _ => false,
            };
        }

        private static SkinManagedFolderMutationJournal createValidated(
            int version,
            Guid operationId,
            SkinManagedFolderMutationKind kind,
            SkinManagedFolderMutationPhase phase,
            Guid? recordId,
            SkinManagedFolderPhysicalIdentity managedRootIdentity,
            string? sourceManagedRelativePath,
            string? targetManagedRelativePath,
            SkinManagedFolderPhysicalIdentity? sourceIdentity,
            SkinManagedFolderPhysicalIdentity? targetIdentity,
            string? stagedSourceAuthority,
            string? stagedSourceRelativePath,
            SkinManagedFolderPhysicalIdentity? stagedSourceIdentity,
            SkinManagedFolderPhysicalIdentity? stagedRootIdentity,
            string? newRecordPublicationPlanVersion,
            string? stagedSourceContentRevision,
            string? stagedSourceTreeFingerprint,
            string? newRecordPublicationFingerprint,
            string? deleteSourceNodeManifest,
            SkinManagedFolderDeleteFallbackDisposition? deleteFallbackDisposition)
        {
            var journal = new SkinManagedFolderMutationJournal(
                version,
                operationId,
                kind,
                phase,
                recordId,
                managedRootIdentity,
                sourceManagedRelativePath,
                targetManagedRelativePath,
                sourceIdentity,
                targetIdentity,
                stagedSourceAuthority,
                stagedSourceRelativePath,
                stagedSourceIdentity,
                stagedRootIdentity,
                newRecordPublicationPlanVersion,
                stagedSourceContentRevision,
                stagedSourceTreeFingerprint,
                newRecordPublicationFingerprint,
                deleteSourceNodeManifest,
                deleteFallbackDisposition);

            if (!journal.IsValid())
                throw new ArgumentException("The managed-folder mutation journal is invalid.");

            return journal;
        }

        private SkinManagedFolderMutationJournal createWithPhase(
            SkinManagedFolderMutationPhase phase,
            SkinManagedFolderPhysicalIdentity? targetIdentity,
            string? newRecordPublicationFingerprint)
            => createValidated(
                Version,
                OperationId,
                Kind,
                phase,
                RecordId,
                ManagedRootIdentity,
                SourceManagedRelativePath,
                TargetManagedRelativePath,
                SourceIdentity,
                targetIdentity,
                StagedSourceAuthority,
                StagedSourceRelativePath,
                StagedSourceIdentity,
                StagedRootIdentity,
                NewRecordPublicationPlanVersion,
                StagedSourceContentRevision,
                StagedSourceTreeFingerprint,
                Kind == SkinManagedFolderMutationKind.Delete
                    ? NewRecordPublicationFingerprint
                    : newRecordPublicationFingerprint,
                DeleteSourceNodeManifest,
                DeleteFallbackDisposition);
    }

    internal enum SkinManagedFolderMutationJournalLoadStatus
    {
        Missing,
        Loaded,
        UnsupportedVersion,
        Invalid,
        IoFailure,
    }

    internal sealed class SkinManagedFolderMutationJournalLoadResult
    {
        public SkinManagedFolderMutationJournalLoadStatus Status { get; }
        public SkinManagedFolderMutationJournal? Journal { get; }

        public bool IsLoaded => Status == SkinManagedFolderMutationJournalLoadStatus.Loaded && Journal != null;

        public SkinManagedFolderMutationJournalLoadResult(
            SkinManagedFolderMutationJournalLoadStatus status,
            SkinManagedFolderMutationJournal? journal = null)
        {
            Status = status;
            Journal = journal;
        }

        public override string ToString() => $"{nameof(SkinManagedFolderMutationJournalLoadResult)}:{Status}";
    }

    internal interface ISkinManagedFolderMutationJournalStore
    {
        SkinManagedFolderMutationJournalLoadResult Load();

        void Write(SkinManagedFolderMutationJournal journal);

        void Delete(SkinManagedFolderMutationJournal expectedJournal);
    }

    /// <summary>
    /// Atomic, flushed storage for the single in-flight managed-folder mutation journal.
    /// </summary>
    internal sealed class SkinManagedFolderMutationJournalStore : ISkinManagedFolderMutationJournalStore
    {
        internal const string JOURNAL_FILENAME = "skin-managed-mutation-journal.json";

        private const int max_journal_bytes = 1024 * 1024;
        private const int movefile_replace_existing = 0x1;
        private const int movefile_write_through = 0x8;

        private static readonly UTF8Encoding strict_utf8 = new UTF8Encoding(false, true);
        private static readonly string[] legacy_journal_payload_properties =
        {
            nameof(SkinManagedFolderMutationJournal.Version),
            nameof(SkinManagedFolderMutationJournal.OperationId),
            nameof(SkinManagedFolderMutationJournal.Kind),
            nameof(SkinManagedFolderMutationJournal.Phase),
            nameof(SkinManagedFolderMutationJournal.RecordId),
            nameof(SkinManagedFolderMutationJournal.ManagedRootIdentity),
            nameof(SkinManagedFolderMutationJournal.SourceManagedRelativePath),
            nameof(SkinManagedFolderMutationJournal.TargetManagedRelativePath),
            nameof(SkinManagedFolderMutationJournal.SourceIdentity),
            nameof(SkinManagedFolderMutationJournal.TargetIdentity),
            nameof(SkinManagedFolderMutationJournal.StagedSourceAuthority),
            nameof(SkinManagedFolderMutationJournal.StagedSourceRelativePath),
            nameof(SkinManagedFolderMutationJournal.StagedSourceIdentity),
            nameof(SkinManagedFolderMutationJournal.StagedRootIdentity),
            nameof(SkinManagedFolderMutationJournal.NewRecordPublicationPlanVersion),
        };

        private static readonly string[] current_journal_payload_properties =
            legacy_journal_payload_properties
                .Append(nameof(SkinManagedFolderMutationJournal.StagedSourceContentRevision))
                .Append(nameof(SkinManagedFolderMutationJournal.StagedSourceTreeFingerprint))
                .ToArray();

        private static readonly string[] physical_identity_properties =
        {
            nameof(SkinManagedFolderPhysicalIdentity.VolumeSerialNumber),
            nameof(SkinManagedFolderPhysicalIdentity.FileIdPart0),
            nameof(SkinManagedFolderPhysicalIdentity.FileIdPart1),
        };

        private readonly string journalPath;
        private readonly object storeGate = new object();

        internal Action BeforeAtomicReplace { get; set; } = () => { };
        internal Action AfterAtomicReplace { get; set; } = () => { };

        public SkinManagedFolderMutationJournalStore(Storage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);
            journalPath = storage.GetFullPath(JOURNAL_FILENAME);
        }

        public SkinManagedFolderMutationJournalLoadResult Load()
        {
            lock (storeGate)
                return load();
        }

        private SkinManagedFolderMutationJournalLoadResult load()
        {
            bool entryObserved = false;

            try
            {
                FileAttributes attributes = File.GetAttributes(journalPath);
                entryObserved = true;

                if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);

                byte[] bytes;

                using (var stream = new FileStream(
                           journalPath,
                           FileMode.Open,
                           FileAccess.Read,
                           FileShare.Read,
                           4096,
                           FileOptions.SequentialScan))
                {
                    if (stream.Length <= 0 || stream.Length > max_journal_bytes)
                        return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);

                    bytes = new byte[(int)stream.Length];
                    stream.ReadExactly(bytes);

                    if (stream.ReadByte() != -1)
                        return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);
                }

                if (bytes.Length == 0 || bytes.Length > max_journal_bytes)
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);

                string json = strict_utf8.GetString(bytes);
                JObject document;

                using (var stringReader = new StringReader(json))
                using (var jsonReader = new JsonTextReader(stringReader)
                {
                    DateParseHandling = DateParseHandling.None,
                    FloatParseHandling = FloatParseHandling.Decimal,
                    MaxDepth = 16,
                })
                {
                    document = JObject.Load(
                        jsonReader,
                        new JsonLoadSettings
                        {
                            DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        });

                    if (jsonReader.Read())
                        return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);
                }

                if (!hasExactProperties(document, "version", "payload", "sha256")
                    || document["version"]?.Type != JTokenType.Integer
                    || document["payload"] is not JObject payload
                    || document["sha256"]?.Type != JTokenType.String)
                {
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);
                }

                int version = document.Value<int>("version");

                if (version is not (SkinManagedFolderMutationJournal.LEGACY_VERSION
                    or SkinManagedFolderMutationJournal.CURRENT_VERSION))
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.UnsupportedVersion);

                if (!hasExactJournalPayloadSchema(payload, version))
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);

                string canonicalPayload = payload.ToString(Formatting.None);
                string expectedChecksum = computeChecksum(canonicalPayload);
                string suppliedChecksum = document.Value<string>("sha256")!;

                if (!string.Equals(expectedChecksum, suppliedChecksum, StringComparison.Ordinal))
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);

                SkinManagedFolderMutationJournal? journal = payload.ToObject<SkinManagedFolderMutationJournal>(
                    SkinManagedFolderMutationJson.CreateSerializer());

                if (journal == null || !journal.IsValid())
                    return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);

                return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Loaded, journal);
            }
            catch (Exception exception) when (exception is JsonException
                                               or ArgumentException
                                               or FormatException
                                               or InvalidCastException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Invalid);
            }
            catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
            {
                return !entryObserved && isStableMissing()
                    ? new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.Missing)
                    : new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.IoFailure);
            }
            catch (Exception exception) when (exception is IOException
                                               or UnauthorizedAccessException
                                               or NotSupportedException
                                               or System.Security.SecurityException)
            {
                return new SkinManagedFolderMutationJournalLoadResult(SkinManagedFolderMutationJournalLoadStatus.IoFailure);
            }
        }

        public void Write(SkinManagedFolderMutationJournal journal)
        {
            lock (storeGate)
                write(journal);
        }

        private void write(SkinManagedFolderMutationJournal journal)
        {
            ArgumentNullException.ThrowIfNull(journal);

            if (!journal.IsValid())
                throw new ArgumentException("The managed-folder mutation journal is invalid.", nameof(journal));

            SkinManagedFolderMutationJournalLoadResult existing = load();

            if (existing.Status == SkinManagedFolderMutationJournalLoadStatus.Missing)
            {
                if (journal.Version != SkinManagedFolderMutationJournal.CURRENT_VERSION
                    || journal.Phase != SkinManagedFolderMutationPhase.Prepared)
                {
                    throw new InvalidOperationException("A new managed-folder mutation journal must start prepared.");
                }
            }
            else if (!existing.IsLoaded
                     || existing.Journal!.Phase is SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack
                     || !existing.Journal.IsSameMonotonicIntent(journal))
            {
                throw new InvalidOperationException("The existing managed-folder mutation journal cannot be overwritten.");
            }

            string payload = JsonConvert.SerializeObject(journal, Formatting.None, SkinManagedFolderMutationJson.CreateSettings());
            var document = new JObject
            {
                ["version"] = journal.Version,
                ["payload"] = JObject.Parse(payload),
                ["sha256"] = computeChecksum(payload),
            };

            byte[] bytes = strict_utf8.GetBytes(document.ToString(Formatting.None));

            if (bytes.Length > max_journal_bytes)
                throw new InvalidOperationException("The managed-folder mutation journal exceeds its fixed size budget.");

            string? directory = Path.GetDirectoryName(journalPath);

            if (string.IsNullOrEmpty(directory))
                throw new InvalidOperationException("The managed-folder mutation journal has no storage directory.");

            Directory.CreateDirectory(directory);
            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(journalPath)}.{Guid.NewGuid():N}.tmp");
            bool replaced = false;

            try
            {
                using (var stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                {
                    stream.Write(bytes);
                    stream.Flush(true);
                }

                BeforeAtomicReplace();
                replaceAtomicallyAndDurably(temporaryPath, journalPath);
                replaced = true;
                AfterAtomicReplace();
            }
            finally
            {
                if (!replaced && File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        public void Delete(SkinManagedFolderMutationJournal expectedJournal)
        {
            ArgumentNullException.ThrowIfNull(expectedJournal);

            if (expectedJournal.Phase is not (SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack))
                throw new InvalidOperationException("A non-terminal managed-folder mutation journal cannot be deleted.");

            lock (storeGate)
            {
                SkinManagedFolderMutationJournalLoadResult existing = load();

                if (!existing.IsLoaded || !existing.Journal!.IsExactSameJournal(expectedJournal))
                    throw new InvalidOperationException("The managed-folder mutation journal changed before deletion.");

                File.Delete(journalPath);
            }
        }

        public override string ToString() => nameof(SkinManagedFolderMutationJournalStore);

        private static bool hasExactProperties(JObject value, params string[] expected)
        {
            string[] actual = value.Properties().Select(property => property.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray();
            string[] sortedExpected = expected.OrderBy(name => name, StringComparer.Ordinal).ToArray();
            return actual.SequenceEqual(sortedExpected, StringComparer.Ordinal);
        }

        private static string computeChecksum(string value)
            => Convert.ToHexString(SHA256.HashData(strict_utf8.GetBytes(value))).ToLowerInvariant();

        private static bool hasExactJournalPayloadSchema(JObject payload, int version)
        {
            bool hasStagedSourceContentRevision = payload.ContainsKey(
                nameof(SkinManagedFolderMutationJournal.StagedSourceContentRevision));
            bool hasStagedSourceTreeFingerprint = payload.ContainsKey(
                nameof(SkinManagedFolderMutationJournal.StagedSourceTreeFingerprint));
            bool hasFingerprint = payload.ContainsKey(
                nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint));
            bool hasDeleteManifest = payload.ContainsKey(
                nameof(SkinManagedFolderMutationJournal.DeleteSourceNodeManifest));
            bool hasDeleteFallbackDisposition = payload.ContainsKey(
                nameof(SkinManagedFolderMutationJournal.DeleteFallbackDisposition));
            string[] expectedProperties = version == SkinManagedFolderMutationJournal.LEGACY_VERSION
                ? legacy_journal_payload_properties
                : current_journal_payload_properties
                    .Concat(hasFingerprint
                        ? new[] { nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint) }
                        : Array.Empty<string>())
                    .Concat(hasDeleteManifest
                        ? new[] { nameof(SkinManagedFolderMutationJournal.DeleteSourceNodeManifest) }
                        : Array.Empty<string>())
                    .Concat(hasDeleteFallbackDisposition
                        ? new[] { nameof(SkinManagedFolderMutationJournal.DeleteFallbackDisposition) }
                        : Array.Empty<string>())
                    .ToArray();

            if (!hasExactProperties(payload, expectedProperties)
                || (version == SkinManagedFolderMutationJournal.LEGACY_VERSION
                    && (hasStagedSourceContentRevision
                        || hasStagedSourceTreeFingerprint
                        || hasFingerprint
                        || hasDeleteManifest
                        || hasDeleteFallbackDisposition))
                || (version == SkinManagedFolderMutationJournal.CURRENT_VERSION
                    && (!hasStagedSourceContentRevision
                        || !hasStagedSourceTreeFingerprint))
                || payload.Value<int?>(nameof(SkinManagedFolderMutationJournal.Version)) != version
                || payload[nameof(SkinManagedFolderMutationJournal.Kind)]?.Type != JTokenType.Integer
                || payload[nameof(SkinManagedFolderMutationJournal.Phase)]?.Type != JTokenType.Integer)
            {
                return false;
            }

            var kind = (SkinManagedFolderMutationKind)payload.Value<int>(
                nameof(SkinManagedFolderMutationJournal.Kind));
            var phase = (SkinManagedFolderMutationPhase)payload.Value<int>(
                nameof(SkinManagedFolderMutationJournal.Phase));

            if (version == SkinManagedFolderMutationJournal.CURRENT_VERSION
                && kind == SkinManagedFolderMutationKind.StagedImport
                && phase is SkinManagedFolderMutationPhase.FilesystemApplied
                    or SkinManagedFolderMutationPhase.RealmApplied
                    or SkinManagedFolderMutationPhase.Committed
                && !hasFingerprint)
            {
                return false;
            }

            return
                payload[nameof(SkinManagedFolderMutationJournal.Version)]?.Type == JTokenType.Integer
                && payload[nameof(SkinManagedFolderMutationJournal.OperationId)]?.Type == JTokenType.String
                && payload[nameof(SkinManagedFolderMutationJournal.Kind)]?.Type == JTokenType.Integer
                && payload[nameof(SkinManagedFolderMutationJournal.Phase)]?.Type == JTokenType.Integer
                && hasType(payload[nameof(SkinManagedFolderMutationJournal.RecordId)], JTokenType.String, true)
                && hasExactPhysicalIdentitySchema(payload[nameof(SkinManagedFolderMutationJournal.ManagedRootIdentity)], false)
                && hasType(payload[nameof(SkinManagedFolderMutationJournal.SourceManagedRelativePath)], JTokenType.String, true)
                && hasType(payload[nameof(SkinManagedFolderMutationJournal.TargetManagedRelativePath)], JTokenType.String, true)
                && hasExactPhysicalIdentitySchema(payload[nameof(SkinManagedFolderMutationJournal.SourceIdentity)])
                && hasExactPhysicalIdentitySchema(payload[nameof(SkinManagedFolderMutationJournal.TargetIdentity)])
                && hasType(payload[nameof(SkinManagedFolderMutationJournal.StagedSourceAuthority)], JTokenType.String, true)
                && hasType(payload[nameof(SkinManagedFolderMutationJournal.StagedSourceRelativePath)], JTokenType.String, true)
                && hasExactPhysicalIdentitySchema(payload[nameof(SkinManagedFolderMutationJournal.StagedSourceIdentity)])
                && hasExactPhysicalIdentitySchema(payload[nameof(SkinManagedFolderMutationJournal.StagedRootIdentity)])
                && hasType(payload[nameof(SkinManagedFolderMutationJournal.NewRecordPublicationPlanVersion)], JTokenType.String, true)
                && (version == SkinManagedFolderMutationJournal.LEGACY_VERSION
                    || hasType(
                        payload[nameof(SkinManagedFolderMutationJournal.StagedSourceContentRevision)],
                        JTokenType.String,
                        true))
                && (version == SkinManagedFolderMutationJournal.LEGACY_VERSION
                    || hasType(
                        payload[nameof(SkinManagedFolderMutationJournal.StagedSourceTreeFingerprint)],
                        JTokenType.String,
                        true))
                && (!hasFingerprint
                    || payload[nameof(SkinManagedFolderMutationJournal.NewRecordPublicationFingerprint)]?.Type
                    == JTokenType.String)
                && (!hasDeleteManifest
                    || payload[nameof(SkinManagedFolderMutationJournal.DeleteSourceNodeManifest)]?.Type
                    == JTokenType.String)
                && (!hasDeleteFallbackDisposition
                    || payload[nameof(SkinManagedFolderMutationJournal.DeleteFallbackDisposition)]?.Type
                    == JTokenType.Integer);
        }

        private static bool hasExactPhysicalIdentitySchema(JToken? value, bool allowNull = true)
            => (allowNull && value?.Type == JTokenType.Null)
               || (value is JObject identity
                   && hasExactProperties(identity, physical_identity_properties)
                   && physical_identity_properties.All(property => identity[property]?.Type == JTokenType.Integer));

        private static bool hasType(JToken? value, JTokenType expected, bool allowNull)
            => value?.Type == expected || (allowNull && value?.Type == JTokenType.Null);

        private bool isStableMissing()
        {
            string? parent = Path.GetDirectoryName(journalPath);

            if (string.IsNullOrEmpty(parent))
                return false;

            try
            {
                FileAttributes attributes = File.GetAttributes(parent);

                if ((attributes & FileAttributes.Directory) == 0
                    || (attributes & FileAttributes.ReparsePoint) != 0)
                {
                    return false;
                }

                foreach (string entry in Directory.EnumerateFileSystemEntries(parent))
                {
                    string? name = Path.GetFileName(entry);

                    if (name == null)
                        continue;

                    if (isOrphanTemporaryName(name))
                    {
                        FileAttributes temporaryAttributes = File.GetAttributes(entry);

                        if ((temporaryAttributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                            return false;

                        File.Delete(entry);
                        continue;
                    }

                    if (name.StartsWith("skin-managed-mutation-journal", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith(".skin-managed-mutation-journal", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool isOrphanTemporaryName(string name)
        {
            string prefix = $".{JOURNAL_FILENAME}.";

            if (!name.StartsWith(prefix, StringComparison.Ordinal)
                || !name.EndsWith(".tmp", StringComparison.Ordinal))
            {
                return false;
            }

            ReadOnlySpan<char> token = name.AsSpan(prefix.Length, name.Length - prefix.Length - ".tmp".Length);
            return token.Length == 32 && Guid.TryParseExact(token, "N", out _);
        }

        private static void replaceAtomicallyAndDurably(string source, string destination)
        {
            if (!OperatingSystem.IsWindows())
            {
                File.Move(source, destination, true);
                return;
            }

            if (!MoveFileExW(source, destination, movefile_replace_existing | movefile_write_through))
            {
                int error = Marshal.GetLastWin32Error();
                throw new IOException(
                    "The managed-folder mutation journal could not be committed durably.",
                    new Win32Exception(error));
            }
        }

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MoveFileExW(
            string existingFileName,
            string newFileName,
            int flags);
    }

    internal static class SkinManagedFolderMutationJson
    {
        public static JsonSerializerSettings CreateSettings()
            => new JsonSerializerSettings
            {
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,
                Culture = System.Globalization.CultureInfo.InvariantCulture,
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal,
                Formatting = Formatting.None,
                MaxDepth = 16,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Error,
                NullValueHandling = NullValueHandling.Include,
                TypeNameHandling = TypeNameHandling.None,
            };

        public static JsonSerializer CreateSerializer() => JsonSerializer.Create(CreateSettings());
    }
}
