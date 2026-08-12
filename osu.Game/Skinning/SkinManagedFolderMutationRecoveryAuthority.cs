// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Opens one held managed-root and exact external-registry authority for a complete recovery attempt.
    /// </summary>
    internal interface ISkinManagedFolderMutationRecoveryAuthority
    {
        ISkinManagedFolderMutationRecoveryAuthoritySession? TryOpen(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Non-path-bearing authority retained from recovery inspection through terminal journal cleanup.
    /// </summary>
    internal interface ISkinManagedFolderMutationRecoveryAuthoritySession : IDisposable
    {
        ISkinManagedFolderMutationNativeSession NativeSession { get; }

        bool IsExactFor(SkinManagedFolderMutationJournal journal);

        bool Validate(CancellationToken cancellationToken = default);

        bool ExactlyMatchesRealmDeclarations(IEnumerable<SkinInfo> records);
    }

    /// <summary>
    /// Production recovery authority. All native and registry failures collapse to a null session.
    /// </summary>
    internal sealed class SkinManagedFolderMutationRecoveryAuthority
        : ISkinManagedFolderMutationRecoveryAuthority
    {
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly ISkinManagedFolderMutationNativeAuthority nativeAuthority;
        private readonly SkinExternalFolderRegistryService externalRegistry;

        public SkinManagedFolderMutationRecoveryAuthority(
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationNativeAuthority nativeAuthority,
            SkinExternalFolderRegistryService externalRegistry)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.nativeAuthority = nativeAuthority ?? throw new ArgumentNullException(nameof(nativeAuthority));
            this.externalRegistry = externalRegistry ?? throw new ArgumentNullException(nameof(externalRegistry));
        }

        public ISkinManagedFolderMutationRecoveryAuthoritySession? TryOpen(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (coordinatorLease?.IsMutationReservationHeldBy(coordinator) != true)
                return null;

            ISkinManagedFolderMutationNativeSession? native = null;
            SkinExternalFolderRegistrySnapshot? registrySnapshot = null;

            try
            {
                native = nativeAuthority.Open(cancellationToken);
                SkinFolderPhysicalAncestryProof managedProof = native.ManagedRootAncestryProof;

                if (managedProof.RootIdentity != native.ManagedRootIdentity)
                    return null;

                SkinExternalFolderRegistryCaptureResult captured = externalRegistry.CaptureExactSet(
                    coordinatorLease,
                    new[] { managedProof },
                    cancellationToken);

                if (!captured.IsSuccess)
                    return null;

                registrySnapshot = captured.Snapshot!;
                var session = new Session(
                    coordinator,
                    coordinatorLease,
                    native,
                    registrySnapshot);
                native = null;
                registrySnapshot = null;

                if (!session.Validate(cancellationToken))
                {
                    session.Dispose();
                    return null;
                }

                return session;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
            finally
            {
                try
                {
                    registrySnapshot?.Dispose();
                }
                catch
                {
                }

                try
                {
                    native?.Dispose();
                }
                catch
                {
                }
            }
        }

        private sealed class Session : ISkinManagedFolderMutationRecoveryAuthoritySession
        {
            private readonly SkinManagedFolderOperationCoordinator coordinator;
            private readonly SkinManagedFolderOperationCoordinator.Lease coordinatorLease;
            private ISkinManagedFolderMutationNativeSession? native;
            private SkinExternalFolderRegistrySnapshot? registrySnapshot;

            public ISkinManagedFolderMutationNativeSession NativeSession
                => native ?? throw new ObjectDisposedException(nameof(Session));

            public Session(
                SkinManagedFolderOperationCoordinator coordinator,
                SkinManagedFolderOperationCoordinator.Lease coordinatorLease,
                ISkinManagedFolderMutationNativeSession native,
                SkinExternalFolderRegistrySnapshot registrySnapshot)
            {
                this.coordinator = coordinator;
                this.coordinatorLease = coordinatorLease;
                this.native = native;
                this.registrySnapshot = registrySnapshot;
            }

            public bool IsExactFor(SkinManagedFolderMutationJournal journal)
            {
                if (journal == null || !journal.IsValid() || registrySnapshot == null)
                    return false;

                if (journal.Version is SkinManagedFolderMutationJournal.LEGACY_VERSION
                    or SkinManagedFolderMutationJournal.PRE_C1_VERSION)
                {
                    return registrySnapshot.IsEmpty;
                }

                if (journal.Version != SkinManagedFolderMutationJournal.CURRENT_VERSION
                    || journal.ExternalRegistryGeneration is not { } generation
                    || journal.ExternalRegistryDigest is not { } digest
                    || journal.ExternalCollisionDisposition is not { } disposition)
                {
                    return false;
                }

                SkinExternalCollisionDisposition expectedDisposition = registrySnapshot.IsEmpty
                    ? SkinExternalCollisionDisposition.NoRegisteredExternalFolders
                    : SkinExternalCollisionDisposition.ExactRegisteredExternalSet;

                return generation == registrySnapshot.ExternalRegistryGeneration
                       && string.Equals(
                           digest,
                           registrySnapshot.ExternalRegistryDigest,
                           StringComparison.Ordinal)
                       && disposition == expectedDisposition;
            }

            public bool Validate(CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                ISkinManagedFolderMutationNativeSession? heldNative = native;
                SkinExternalFolderRegistrySnapshot? heldRegistry = registrySnapshot;

                if (heldNative == null
                    || heldRegistry == null
                    || coordinatorLease.IsMutationReservationHeldBy(coordinator) != true)
                {
                    return false;
                }

                try
                {
                    heldNative.ValidateCompleteAndStable(cancellationToken);
                    return heldRegistry.Validate(coordinatorLease, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return false;
                }
            }

            public bool ExactlyMatchesRealmDeclarations(IEnumerable<SkinInfo> records)
            {
                ArgumentNullException.ThrowIfNull(records);

                try
                {
                    return registrySnapshot?.ExactlyMatchesRealmDeclarations(records) == true;
                }
                catch
                {
                    return false;
                }
            }

            public void Dispose()
            {
                SkinExternalFolderRegistrySnapshot? heldRegistry =
                    Interlocked.Exchange(ref registrySnapshot, null);
                ISkinManagedFolderMutationNativeSession? heldNative =
                    Interlocked.Exchange(ref native, null);

                try
                {
                    heldRegistry?.Dispose();
                }
                catch
                {
                }

                try
                {
                    heldNative?.Dispose();
                }
                catch
                {
                }
            }

            public override string ToString() => nameof(Session);
        }

        public override string ToString() => nameof(SkinManagedFolderMutationRecoveryAuthority);
    }
}
