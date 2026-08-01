// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Shared linearisation authority for managed-folder scanning, selection publication and mutation recovery.
    /// </summary>
    /// <remarks>
    /// The coordinator deliberately grants no filesystem capability. A lease only serialises the short authoritative
    /// checks and commits performed by the three participants. Mutation-specific native authority must be acquired
    /// separately while the lease is held.
    /// </remarks>
    internal sealed class SkinManagedFolderOperationCoordinator
    {
        private readonly object ownershipGate = new object();
        private readonly object freezeGate = new object();
        private readonly HashSet<string> frozenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> recoveryFrozenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool allPathsFrozen;
        private bool allRecoveryPathsFrozen;
        private int ownerManagedThreadId;
        private int leaseDepth;
        private LeaseKind ownerKind;
        private long startupSequenceEpoch;
        private long mutationReservationEpoch;
        private TaskCompletionSource<object?>? activeRetryableCompletion;

        public Lease Enter(CancellationToken cancellationToken = default)
            => enter(LeaseKind.ShortScope, cancellationToken);

        internal Lease EnterMutation(CancellationToken cancellationToken = default)
            => enter(LeaseKind.MutationReservation, cancellationToken);

        internal Lease EnterStagedImport(CancellationToken cancellationToken = default)
            => enter(LeaseKind.StagedImportReservation, cancellationToken);

        internal Lease EnterStartupSequence(CancellationToken cancellationToken = default)
            => enter(LeaseKind.StartupSequence, cancellationToken);

        internal bool IsStartupSequenceHeldByCurrentThread
        {
            get
            {
                lock (ownershipGate)
                {
                    return leaseDepth > 0
                           && ownerManagedThreadId == Environment.CurrentManagedThreadId
                           && ownerKind == LeaseKind.StartupSequence;
                }
            }
        }

        internal SelectionPreparationObservation CaptureSelectionPreparationObservation()
        {
            lock (ownershipGate)
                return new SelectionPreparationObservation(startupSequenceEpoch, mutationReservationEpoch);
        }

        internal SelectionContention? TryGetRetryableContentionSince(SelectionPreparationObservation observation)
        {
            lock (ownershipGate)
            {
                if (startupSequenceEpoch == observation.StartupSequenceEpoch
                    || mutationReservationEpoch != observation.MutationReservationEpoch)
                {
                    return null;
                }

                if (leaseDepth > 0)
                {
                    if (ownerKind == LeaseKind.StartupSequence && activeRetryableCompletion != null)
                    {
                        return new SelectionContention(
                            SelectionContentionKind.StartupSequence,
                            activeRetryableCompletion.Task);
                    }

                    if (ownerKind == LeaseKind.StagedImportReservation && activeRetryableCompletion != null)
                    {
                        return new SelectionContention(
                            SelectionContentionKind.StagedImport,
                            activeRetryableCompletion.Task);
                    }

                    if (ownerKind != LeaseKind.ShortScope
                        || ownerManagedThreadId != Environment.CurrentManagedThreadId)
                    {
                        return null;
                    }
                }

                return new SelectionContention(
                    SelectionContentionKind.StartupSequence,
                    Task.CompletedTask);
            }
        }

        internal bool IsMutationReservationEpochCurrent(SelectionPreparationObservation observation)
        {
            lock (ownershipGate)
                return mutationReservationEpoch == observation.MutationReservationEpoch;
        }

        public bool TryEnter(out Lease? lease)
        {
            int currentThreadId = Environment.CurrentManagedThreadId;

            lock (ownershipGate)
            {
                if (leaseDepth > 0)
                {
                    if (ownerManagedThreadId != currentThreadId
                        || !canNestShortScope(ownerKind))
                    {
                        lease = null;
                        return false;
                    }

                    leaseDepth++;
                    lease = new Lease(this, LeaseKind.ShortScope);
                    return true;
                }

                publishOwnerHeld(currentThreadId, LeaseKind.ShortScope, null);
                lease = new Lease(this, LeaseKind.ShortScope);
                return true;
            }
        }

        /// <summary>
        /// Tries to enter a selection publication boundary without blocking the caller.
        /// </summary>
        /// <remarks>
        /// When the exact current holder is the startup recovery/scanner sequence or a staged import, its typed
        /// completion is returned so the selection can retry asynchronously. Generic short scopes, rename and delete
        /// reservations never return retry authority.
        /// </remarks>
        internal bool TryEnterForSelection(out Lease? lease, out SelectionContention? contention)
        {
            int currentThreadId = Environment.CurrentManagedThreadId;

            lock (ownershipGate)
            {
                if (leaseDepth > 0)
                {
                    if (ownerManagedThreadId == currentThreadId
                        && canNestShortScope(ownerKind))
                    {
                        leaseDepth++;
                        lease = new Lease(this, LeaseKind.ShortScope);
                        contention = null;
                        return true;
                    }

                    lease = null;
                    contention = tryCreateSelectionContentionHeld();
                    return false;
                }

                publishOwnerHeld(currentThreadId, LeaseKind.ShortScope, null);
                lease = new Lease(this, LeaseKind.ShortScope);
                contention = null;
                return true;
            }
        }

        private Lease enter(LeaseKind requestedKind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int currentThreadId = Environment.CurrentManagedThreadId;

            TaskCompletionSource<object?>? retryableCompletion = isRetryableSelectionContention(requestedKind)
                ? new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously)
                : null;
            CancellationTokenRegistration cancellationRegistration = default;

            try
            {
                lock (ownershipGate)
                {
                    if (leaseDepth > 0 && ownerManagedThreadId == currentThreadId)
                    {
                        if (!canNestShortScope(ownerKind) || requestedKind != LeaseKind.ShortScope)
                        {
                            throw new InvalidOperationException(
                                "A managed-folder mutation reservation or startup sequence cannot be re-entered.");
                        }

                        leaseDepth++;
                        return new Lease(this, LeaseKind.ShortScope);
                    }

                    cancellationRegistration = cancellationToken.UnsafeRegister(
                        static state =>
                        {
                            var coordinator = (SkinManagedFolderOperationCoordinator)state!;

                            lock (coordinator.ownershipGate)
                                Monitor.PulseAll(coordinator.ownershipGate);
                        },
                        this);

                    while (leaseDepth > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        Monitor.Wait(ownershipGate);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    publishOwnerHeld(currentThreadId, requestedKind, retryableCompletion);
                    return new Lease(this, requestedKind);
                }
            }
            finally
            {
                cancellationRegistration.Dispose();

                if (retryableCompletion != null)
                {
                    lock (ownershipGate)
                    {
                        if (!ReferenceEquals(activeRetryableCompletion, retryableCompletion))
                            retryableCompletion.TrySetResult(null);
                    }
                }
            }
        }

        private void publishOwnerHeld(
            int currentThreadId,
            LeaseKind kind,
            TaskCompletionSource<object?>? retryableCompletion)
        {
            ownerManagedThreadId = currentThreadId;
            leaseDepth = 1;
            ownerKind = kind;
            activeRetryableCompletion = retryableCompletion;

            if (kind == LeaseKind.StartupSequence)
                startupSequenceEpoch++;
            else if (kind == LeaseKind.MutationReservation)
                mutationReservationEpoch++;
        }

        private SelectionContention? tryCreateSelectionContentionHeld()
        {
            if (activeRetryableCompletion == null)
                return null;

            return ownerKind switch
            {
                LeaseKind.StartupSequence => new SelectionContention(
                    SelectionContentionKind.StartupSequence,
                    activeRetryableCompletion.Task),
                LeaseKind.StagedImportReservation => new SelectionContention(
                    SelectionContentionKind.StagedImport,
                    activeRetryableCompletion.Task),
                _ => null,
            };
        }

        public T RunExclusive<T>(Func<T> action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);

            using Lease lease = Enter(cancellationToken);
            return action();
        }

        public void RunExclusive(Action action, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(action);

            using Lease lease = Enter(cancellationToken);
            action();
        }

        public bool IsPathFrozen(string managedRelativePath)
        {
            if (!SkinManagedFolderPath.TryNormalise(managedRelativePath, out string normalisedPath))
                return true;

            lock (freezeGate)
                return allPathsFrozen
                       || allRecoveryPathsFrozen
                       || frozenPaths.Contains(normalisedPath)
                       || recoveryFrozenPaths.Contains(normalisedPath);
        }

        public bool IsMutationBlocked
        {
            get
            {
                lock (freezeGate)
                    return allPathsFrozen
                           || allRecoveryPathsFrozen
                           || frozenPaths.Count > 0
                           || recoveryFrozenPaths.Count > 0;
            }
        }

        internal bool HasRecoveryFreeze
        {
            get
            {
                lock (freezeGate)
                    return allRecoveryPathsFrozen || recoveryFrozenPaths.Count > 0;
            }
        }

        public void FreezeAllPaths()
        {
            lock (freezeGate)
                allPathsFrozen = true;
        }

        public void FreezePaths(IEnumerable<string> managedRelativePaths)
        {
            ArgumentNullException.ThrowIfNull(managedRelativePaths);

            lock (freezeGate)
            {
                foreach (string path in managedRelativePaths)
                {
                    if (!SkinManagedFolderPath.TryNormalise(path, out string normalisedPath))
                    {
                        allPathsFrozen = true;
                        continue;
                    }

                    frozenPaths.Add(normalisedPath);
                }
            }
        }

        public void UnfreezePaths(IEnumerable<string> managedRelativePaths)
        {
            ArgumentNullException.ThrowIfNull(managedRelativePaths);

            lock (freezeGate)
            {
                foreach (string path in managedRelativePaths)
                {
                    if (SkinManagedFolderPath.TryNormalise(path, out string normalisedPath))
                        frozenPaths.Remove(normalisedPath);
                }
            }
        }

        internal void FreezeAllRecoveryPaths()
        {
            lock (freezeGate)
                allRecoveryPathsFrozen = true;
        }

        internal void FreezeRecoveryPaths(IEnumerable<string> managedRelativePaths)
        {
            ArgumentNullException.ThrowIfNull(managedRelativePaths);

            lock (freezeGate)
            {
                foreach (string path in managedRelativePaths)
                {
                    if (!SkinManagedFolderPath.TryNormalise(path, out string normalisedPath))
                    {
                        allRecoveryPathsFrozen = true;
                        continue;
                    }

                    recoveryFrozenPaths.Add(normalisedPath);
                }
            }
        }

        internal void UnfreezeRecoveryPaths(IEnumerable<string> managedRelativePaths)
        {
            ArgumentNullException.ThrowIfNull(managedRelativePaths);

            lock (freezeGate)
            {
                foreach (string path in managedRelativePaths)
                {
                    if (SkinManagedFolderPath.TryNormalise(path, out string normalisedPath))
                        recoveryFrozenPaths.Remove(normalisedPath);
                }
            }
        }

        private void exit()
        {
            bool release;
            TaskCompletionSource<object?>? completedRetryableContention = null;

            lock (ownershipGate)
            {
                if (leaseDepth <= 0)
                    throw new SynchronizationLockException("The managed-folder operation lease is not held.");

                leaseDepth--;
                release = leaseDepth == 0;

                if (release)
                {
                    if (isRetryableSelectionContention(ownerKind))
                        completedRetryableContention = activeRetryableCompletion;

                    ownerManagedThreadId = 0;
                    ownerKind = default;
                    activeRetryableCompletion = null;
                    Monitor.PulseAll(ownershipGate);
                }
            }

            completedRetryableContention?.TrySetResult(null);
        }

        private static bool canNestShortScope(LeaseKind kind)
            => kind is LeaseKind.ShortScope or LeaseKind.StartupSequence;

        private static bool isRetryableSelectionContention(LeaseKind kind)
            => kind is LeaseKind.StartupSequence or LeaseKind.StagedImportReservation;

        public sealed class Lease : IDisposable
        {
            private SkinManagedFolderOperationCoordinator? owner;
            private readonly LeaseKind kind;

            internal Lease(SkinManagedFolderOperationCoordinator owner, LeaseKind kind)
            {
                this.owner = owner;
                this.kind = kind;
            }

            internal bool IsHeldBy(SkinManagedFolderOperationCoordinator candidate)
                => ReferenceEquals(owner, candidate);

            internal bool IsMutationReservationHeldBy(SkinManagedFolderOperationCoordinator candidate)
                => (kind is LeaseKind.MutationReservation or LeaseKind.StagedImportReservation)
                   && ReferenceEquals(owner, candidate);

            public void Dispose()
            {
                SkinManagedFolderOperationCoordinator? heldOwner = Interlocked.Exchange(ref owner, null);

                heldOwner?.exit();
            }
        }

        internal enum LeaseKind
        {
            ShortScope,
            MutationReservation,
            StagedImportReservation,
            StartupSequence,
        }

        internal enum SelectionContentionKind
        {
            StartupSequence,
            StagedImport,
        }

        internal sealed class SelectionContention
        {
            public SelectionContentionKind Kind { get; }
            public Task Completion { get; }

            public SelectionContention(SelectionContentionKind kind, Task completion)
            {
                Kind = kind;
                Completion = completion;
            }
        }

        internal readonly record struct SelectionPreparationObservation(
            long StartupSequenceEpoch,
            long MutationReservationEpoch);
    }

    internal static class SkinManagedFolderPath
    {
        private const int max_windows_component_characters = 255;

        public static bool TryNormalise(string? path, out string normalisedPath)
        {
            normalisedPath = string.Empty;

            if (string.IsNullOrEmpty(path)
                || path.Contains('\\')
                || path.StartsWith('/')
                || path.EndsWith('/'))
            {
                return false;
            }

            string[] segments = path.Split('/', StringSplitOptions.None);

            if (segments.Length != 2
                || !string.Equals(segments[0], SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY, StringComparison.OrdinalIgnoreCase)
                || !tryNormaliseChildName(segments[1], out string child))
            {
                return false;
            }

            normalisedPath = $"{SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY}/{child}";
            return true;
        }

        public static bool TryCreateFromChildName(string? childName, out string managedRelativePath)
        {
            managedRelativePath = string.Empty;

            if (!tryNormaliseChildName(childName, out string normalisedChild))
                return false;

            managedRelativePath = $"{SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY}/{normalisedChild}";
            return true;
        }

        private static bool tryNormaliseChildName(string? childName, out string normalisedChild)
        {
            normalisedChild = string.Empty;

            if (string.IsNullOrEmpty(childName)
                || childName.Length > max_windows_component_characters
                || !SkinPackageResourceNameValidator.IsValidWindowsSegment(childName))
            {
                return false;
            }

            try
            {
                normalisedChild = childName.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return normalisedChild.Length <= max_windows_component_characters
                   && SkinPackageResourceNameValidator.IsValidWindowsSegment(normalisedChild);
        }
    }
}
