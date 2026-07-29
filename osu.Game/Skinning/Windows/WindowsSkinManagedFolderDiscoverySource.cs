// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Security;
using System.Threading;
using osu.Framework.Platform;

namespace osu.Game.Skinning.Windows
{
    /// <summary>
    /// Produces one complete, stable inventory of direct children under the managed <c>chartskin</c> root.
    /// </summary>
    /// <remarks>
    /// All filesystem traversal and package capture is handle-relative and no-follow. Package paths and metadata only
    /// leave this type as the user-facing discovery payload and are never included in exceptions or diagnostics.
    /// </remarks>
    internal sealed class WindowsSkinManagedFolderDiscoverySource : ISkinManagedFolderDiscoverySource
    {
        private const int max_stable_inventory_attempts = 3;
        private const int stable_inventory_retry_delay_milliseconds = 25;

        private readonly Func<string> getDataRootAbsolutePath;
        private readonly IWindowsSkinPackageCaptureFileSystem? fileSystem;
        private readonly SkinPackageRevisionCapsuleLimits limits;

        public WindowsSkinManagedFolderDiscoverySource(Storage storage)
            : this(
                createDataRootAccessor(storage),
                null,
                SkinPackageRevisionCapsuleLimits.Default)
        {
        }

        internal WindowsSkinManagedFolderDiscoverySource(
            string dataRootAbsolutePath,
            IWindowsSkinPackageCaptureFileSystem fileSystem,
            SkinPackageRevisionCapsuleLimits? limits = null)
            : this(
                () => dataRootAbsolutePath,
                fileSystem ?? throw new ArgumentNullException(nameof(fileSystem)),
                limits ?? SkinPackageRevisionCapsuleLimits.Default)
        {
        }

        private WindowsSkinManagedFolderDiscoverySource(
            Func<string> getDataRootAbsolutePath,
            IWindowsSkinPackageCaptureFileSystem? fileSystem,
            SkinPackageRevisionCapsuleLimits limits)
        {
            this.getDataRootAbsolutePath = getDataRootAbsolutePath ?? throw new ArgumentNullException(nameof(getDataRootAbsolutePath));
            this.fileSystem = fileSystem;
            this.limits = limits ?? throw new ArgumentNullException(nameof(limits));
        }

        public SkinManagedFolderDiscoverySnapshot Discover(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
                return SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.UnsupportedPlatform);

            if (!NativeMethods.HasExpectedLayouts)
                return SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.NativeFailure);

            string dataRootAbsolutePath;

            try
            {
                dataRootAbsolutePath = getDataRootAbsolutePath();
            }
            catch (Exception exception) when (isExpectedRootException(exception))
            {
                return SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.InvalidDataRoot);
            }

            if (string.IsNullOrWhiteSpace(dataRootAbsolutePath))
                return SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.InvalidDataRoot);

            for (int attempt = 0; attempt < max_stable_inventory_attempts; attempt++)
            {
                try
                {
                    return discoverOnce(dataRootAbsolutePath, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception) when (
                    attempt + 1 < max_stable_inventory_attempts
                    && isRetryableRootRace(exception.RejectionReason))
                {
                    waitForStableInventoryRetry(cancellationToken);
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception)
                {
                    return SkinManagedFolderDiscoverySnapshot.Incomplete(mapFailure(exception.RejectionReason));
                }
                catch (Exception exception) when (isExpectedRootException(exception))
                {
                    return SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.NativeFailure);
                }
            }

            return SkinManagedFolderDiscoverySnapshot.Incomplete(SkinManagedFolderScanFailureReason.RootUnstable);
        }

        [SupportedOSPlatform("windows10.0.16299")]
        private SkinManagedFolderDiscoverySnapshot discoverOnce(
            string dataRootAbsolutePath,
            CancellationToken cancellationToken)
        {
            using WindowsSkinManagedAuthoritySession session = WindowsSkinManagedAuthoritySession.Open(
                dataRootAbsolutePath,
                fileSystem ?? new NativeWindowsSkinPackageCaptureFileSystem(),
                cancellationToken);

            var observedEntries = new Dictionary<string, List<WindowsSkinPackageDirectoryEntry>>(StringComparer.OrdinalIgnoreCase);

            foreach (WindowsSkinPackageDirectoryEntry entry in session.BaselineEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!WindowsSkinManagedAuthoritySession.TryGetManagedRelativePath(entry.Name, out string managedRelativePath))
                    continue;

                if (!observedEntries.TryGetValue(managedRelativePath, out List<WindowsSkinPackageDirectoryEntry>? entries))
                    observedEntries.Add(managedRelativePath, entries = new List<WindowsSkinPackageDirectoryEntry>());

                entries.Add(entry);
            }

            var discoveries = new List<SkinManagedFolderDiscovery>();

            foreach ((string managedRelativePath, List<WindowsSkinPackageDirectoryEntry> entries) in observedEntries
                                                                                                  .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                                                                                                  .ThenBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Case-insensitive aliases cannot identify one authoritative child. They remain observed so an
                // existing record cannot be negatively reconciled, but neither entry is importable.
                if (entries.Count != 1)
                    continue;

                WindowsSkinPackageDirectoryEntry entry = entries[0];

                if (entry.Metadata.Kind != WindowsSkinPackageEntryKind.Directory
                    || entry.Metadata.IsReparsePoint
                    || entry.Metadata.DeletePending
                    || !entry.Metadata.Identity.IsUsable)
                {
                    continue;
                }

                SkinManagedPackageCaptureResult capture = session.CaptureObservedChild(entry, limits, cancellationToken);

                if (!capture.IsSuccess)
                    continue;

                using SkinPackageRevisionCapsule capsule = capture.Capsule!;

                if (SkinManagedFolderPackageMetadataReader.TryRead(
                        capsule,
                        out SkinManagedFolderPackageMetadata? metadata))
                {
                    discoveries.Add(new SkinManagedFolderDiscovery(
                        managedRelativePath,
                        metadata!.Name,
                        metadata.Creator,
                        metadata.ContentRevision));
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            session.ValidateCompleteAndStable(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            return SkinManagedFolderDiscoverySnapshot.Complete(
                observedEntries.Keys
                               .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                               .ThenBy(path => path, StringComparer.Ordinal),
                discoveries);
        }

        private static Func<string> createDataRootAccessor(Storage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);
            return () => storage.GetFullPath(string.Empty);
        }

        private static bool isRetryableRootRace(SkinManagedPackageCaptureRejectionReason reason)
            => reason is SkinManagedPackageCaptureRejectionReason.InventoryChanged
                or SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged
                or SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture;

        private static void waitForStableInventoryRetry(CancellationToken cancellationToken)
        {
            if (cancellationToken.WaitHandle.WaitOne(stable_inventory_retry_delay_milliseconds))
                cancellationToken.ThrowIfCancellationRequested();
        }

        private static SkinManagedFolderScanFailureReason mapFailure(SkinManagedPackageCaptureRejectionReason reason)
            => reason switch
            {
                SkinManagedPackageCaptureRejectionReason.InvalidRequest or
                    SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping => SkinManagedFolderScanFailureReason.InvalidDataRoot,

                SkinManagedPackageCaptureRejectionReason.PackageUnavailable => SkinManagedFolderScanFailureReason.RootUnavailable,

                SkinManagedPackageCaptureRejectionReason.AccessDenied or
                    SkinManagedPackageCaptureRejectionReason.SourceBusy or
                    SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType => SkinManagedFolderScanFailureReason.RootUnreadable,

                SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered or
                    SkinManagedPackageCaptureRejectionReason.AlternateNameAlias or
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged or
                    SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture or
                    SkinManagedPackageCaptureRejectionReason.InventoryChanged => SkinManagedFolderScanFailureReason.RootUnstable,

                SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded => SkinManagedFolderScanFailureReason.SnapshotRejected,
                _ => SkinManagedFolderScanFailureReason.NativeFailure,
            };

        private static bool isExpectedRootException(Exception exception)
            => exception is ArgumentException
                or IOException
                or UnauthorizedAccessException
                or NotSupportedException
                or ObjectDisposedException
                or SecurityException
                or InvalidOperationException;
    }
}
