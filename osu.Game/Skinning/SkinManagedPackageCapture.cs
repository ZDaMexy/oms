// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Skinning.Windows;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A resolver-issued request for one managed <c>chartskin/&lt;name&gt;</c> package.
    /// </summary>
    /// <remarks>
    /// The contained paths are process-local and sensitive. This is not a filesystem capability: the Windows capture
    /// adapter must reopen every segment from a verified volume handle and reject aliases, reparses and identity races.
    /// </remarks>
    internal sealed class SkinManagedPackageCaptureRequest
    {
        internal string NormalisedDataRootAbsolutePath { get; }

        internal string PackageDirectoryName { get; }

        internal SkinManagedPackageCaptureRequest(
            string normalisedDataRootAbsolutePath,
            string packageDirectoryName,
            object resolverIssuer)
        {
            if (!SkinFilesystemStorageResolver.IsManagedCaptureRequestIssuer(resolverIssuer))
                throw new InvalidOperationException("Only the storage resolver can issue a managed capture request.");

            NormalisedDataRootAbsolutePath = normalisedDataRootAbsolutePath ?? throw new ArgumentNullException(nameof(normalisedDataRootAbsolutePath));
            PackageDirectoryName = packageDirectoryName ?? throw new ArgumentNullException(nameof(packageDirectoryName));
        }

        public override string ToString() => nameof(SkinManagedPackageCaptureRequest);
    }

    /// <summary>
    /// A stable, non-sensitive reason why a managed package could not be captured.
    /// </summary>
    internal enum SkinManagedPackageCaptureRejectionReason
    {
        None,
        UnsupportedPlatform,
        InvalidRequest,
        UnsupportedVolumeMapping,
        PackageUnavailable,
        AccessDenied,
        SourceBusy,
        NativeIoFailure,
        DirectoryEnumerationBudgetExceeded,
        ReparsePointEncountered,
        AlternateNameAlias,
        UnsupportedEntryType,
        HardLinkedFile,
        DuplicatePhysicalIdentity,
        PackageRootIdentityChanged,
        EntryChangedDuringCapture,
        InventoryChanged,
        CapsuleRejected,
    }

    /// <summary>
    /// The all-or-nothing result of capturing one managed package into an immutable revision capsule.
    /// </summary>
    internal sealed class SkinManagedPackageCaptureResult
    {
        public SkinManagedPackageCaptureRejectionReason RejectionReason { get; }

        public SkinPackageRevisionCapsuleRejectionReason CapsuleRejectionReason { get; }

        public SkinPackageRevisionCapsule? Capsule { get; }

        /// <summary>
        /// A non-sensitive SHA-256 commitment to the captured capsule revision and exact physical package tree.
        /// </summary>
        /// <remarks>
        /// Present only for successful results. The fingerprint is suitable for equality checks and durable recovery
        /// evidence, but it is not a filesystem capability and cannot be used to reopen any captured handle.
        /// </remarks>
        public string? PhysicalTreeFingerprint { get; }

        public bool IsSuccess => Capsule != null;

        private SkinManagedPackageCaptureResult(
            SkinManagedPackageCaptureRejectionReason rejectionReason,
            SkinPackageRevisionCapsuleRejectionReason capsuleRejectionReason,
            SkinPackageRevisionCapsule? capsule,
            string? physicalTreeFingerprint)
        {
            RejectionReason = rejectionReason;
            CapsuleRejectionReason = capsuleRejectionReason;
            Capsule = capsule;
            PhysicalTreeFingerprint = physicalTreeFingerprint;
        }

        internal static SkinManagedPackageCaptureResult Success(
            SkinPackageRevisionCapsule capsule,
            string physicalTreeFingerprint)
        {
            ArgumentNullException.ThrowIfNull(capsule);

            if (!isLowercaseSha256(physicalTreeFingerprint))
                throw new ArgumentException("The physical tree fingerprint must be a lowercase SHA-256 value.", nameof(physicalTreeFingerprint));

            return new SkinManagedPackageCaptureResult(
                SkinManagedPackageCaptureRejectionReason.None,
                SkinPackageRevisionCapsuleRejectionReason.None,
                capsule,
                physicalTreeFingerprint);
        }

        private static bool isLowercaseSha256(string? value)
            => value is { Length: 64 }
               && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

        internal static SkinManagedPackageCaptureResult Reject(SkinManagedPackageCaptureRejectionReason reason)
        {
            if (!Enum.IsDefined(reason)
                || reason is SkinManagedPackageCaptureRejectionReason.None or SkinManagedPackageCaptureRejectionReason.CapsuleRejected)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinManagedPackageCaptureResult(
                reason,
                SkinPackageRevisionCapsuleRejectionReason.None,
                null,
                null);
        }

        internal static SkinManagedPackageCaptureResult RejectCapsule(SkinPackageRevisionCapsuleRejectionReason reason)
        {
            if (!Enum.IsDefined(reason) || reason == SkinPackageRevisionCapsuleRejectionReason.None)
                throw new ArgumentOutOfRangeException(nameof(reason));

            return new SkinManagedPackageCaptureResult(
                SkinManagedPackageCaptureRejectionReason.CapsuleRejected,
                reason,
                null,
                null);
        }

        public override string ToString()
            => $"{nameof(SkinManagedPackageCaptureResult)}:{RejectionReason}:{CapsuleRejectionReason}";
    }

    /// <summary>
    /// Platform-safe entry point for managed-folder capture.
    /// </summary>
    internal static class SkinManagedPackageCapture
    {
        public static SkinManagedPackageCaptureResult Capture(
            SkinManagedPackageCaptureRequest? request,
            SkinPackageRevisionCapsuleLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null)
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            // NtQueryDirectoryFileEx is available from Windows 10 1709. OMS supports Windows 10 22H2 and newer.
            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299))
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.UnsupportedPlatform);

            if (!NativeMethods.HasExpectedLayouts)
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            return new WindowsSkinManagedPackageCapture().Capture(request, limits, cancellationToken);
        }
    }
}
