// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

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
        private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);
        private readonly object ownershipGate = new object();
        private readonly object freezeGate = new object();
        private readonly HashSet<string> frozenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> recoveryFrozenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private bool allPathsFrozen;
        private bool allRecoveryPathsFrozen;
        private int ownerManagedThreadId;
        private int leaseDepth;
        private LeaseKind ownerKind;

        public Lease Enter(CancellationToken cancellationToken = default)
            => enter(LeaseKind.ShortScope, cancellationToken);

        internal Lease EnterMutation(CancellationToken cancellationToken = default)
            => enter(LeaseKind.MutationReservation, cancellationToken);

        public bool TryEnter(out Lease? lease)
        {
            int currentThreadId = Environment.CurrentManagedThreadId;

            lock (ownershipGate)
            {
                if (leaseDepth > 0 && ownerManagedThreadId == currentThreadId)
                {
                    if (ownerKind != LeaseKind.ShortScope)
                    {
                        lease = null;
                        return false;
                    }

                    leaseDepth++;
                    lease = new Lease(this, LeaseKind.ShortScope);
                    return true;
                }
            }

            if (!operationGate.Wait(0))
            {
                lease = null;
                return false;
            }

            lock (ownershipGate)
            {
                ownerManagedThreadId = currentThreadId;
                leaseDepth = 1;
                ownerKind = LeaseKind.ShortScope;
            }

            lease = new Lease(this, LeaseKind.ShortScope);
            return true;
        }

        private Lease enter(LeaseKind requestedKind, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int currentThreadId = Environment.CurrentManagedThreadId;

            lock (ownershipGate)
            {
                if (leaseDepth > 0 && ownerManagedThreadId == currentThreadId)
                {
                    if (ownerKind != LeaseKind.ShortScope || requestedKind != LeaseKind.ShortScope)
                    {
                        throw new InvalidOperationException(
                            "A detached managed-folder mutation reservation cannot be re-entered.");
                    }

                    leaseDepth++;
                    return new Lease(this, LeaseKind.ShortScope);
                }
            }

            operationGate.Wait(cancellationToken);

            lock (ownershipGate)
            {
                ownerManagedThreadId = currentThreadId;
                leaseDepth = 1;
                ownerKind = requestedKind;
            }

            return new Lease(this, requestedKind);
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

            lock (ownershipGate)
            {
                if (leaseDepth <= 0)
                    throw new SynchronizationLockException("The managed-folder operation lease is not held.");

                leaseDepth--;
                release = leaseDepth == 0;

                if (release)
                {
                    ownerManagedThreadId = 0;
                    ownerKind = default;
                }
            }

            if (release)
                operationGate.Release();
        }

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
                => kind == LeaseKind.MutationReservation && ReferenceEquals(owner, candidate);

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
        }
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
