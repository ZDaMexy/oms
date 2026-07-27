// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace osu.Game.Skinning.Windows
{
    internal enum WindowsSkinPackageEntryKind
    {
        Directory,
        File,
    }

    internal enum WindowsSkinPackageOpenMode
    {
        AuthorityDirectory,
        CapturedDirectory,
        CapturedFile,
        MutationSourceDirectory,
        MutationSourceVerificationDirectory,
    }

    internal readonly struct WindowsSkinPackagePhysicalIdentity : IEquatable<WindowsSkinPackagePhysicalIdentity>
    {
        private readonly ulong volumeSerialNumber;
        private readonly ulong fileIdPart0;
        private readonly ulong fileIdPart1;

        internal ulong VolumeSerialNumber => volumeSerialNumber;
        internal ulong FileIdPart0 => fileIdPart0;
        internal ulong FileIdPart1 => fileIdPart1;

        public bool IsUsable => volumeSerialNumber != 0 && (fileIdPart0 != 0 || fileIdPart1 != 0);

        public WindowsSkinPackagePhysicalIdentity(ulong volumeSerialNumber, ulong fileIdPart0, ulong fileIdPart1)
        {
            this.volumeSerialNumber = volumeSerialNumber;
            this.fileIdPart0 = fileIdPart0;
            this.fileIdPart1 = fileIdPart1;
        }

        public bool Equals(WindowsSkinPackagePhysicalIdentity other)
            => volumeSerialNumber == other.volumeSerialNumber
               && fileIdPart0 == other.fileIdPart0
               && fileIdPart1 == other.fileIdPart1;

        public override bool Equals(object? obj) => obj is WindowsSkinPackagePhysicalIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(volumeSerialNumber, fileIdPart0, fileIdPart1);

        public static bool operator ==(WindowsSkinPackagePhysicalIdentity left, WindowsSkinPackagePhysicalIdentity right) => left.Equals(right);

        public static bool operator !=(WindowsSkinPackagePhysicalIdentity left, WindowsSkinPackagePhysicalIdentity right) => !left.Equals(right);

        public override string ToString() => nameof(WindowsSkinPackagePhysicalIdentity);
    }

    internal readonly struct WindowsSkinPackageEntryMetadata : IEquatable<WindowsSkinPackageEntryMetadata>
    {
        private const uint file_attribute_reparse_point = 0x00000400;

        public WindowsSkinPackagePhysicalIdentity Identity { get; }

        public WindowsSkinPackageEntryKind Kind { get; }

        public long Length { get; }

        public long CreationTime { get; }

        public long LastWriteTime { get; }

        public long ChangeTime { get; }

        public uint FileAttributes { get; }

        public uint ReparseTag { get; }

        public uint NumberOfLinks { get; }

        public bool DeletePending { get; }

        public bool IsReparsePoint => (FileAttributes & file_attribute_reparse_point) != 0 || ReparseTag != 0;

        public WindowsSkinPackageEntryMetadata(
            WindowsSkinPackagePhysicalIdentity identity,
            WindowsSkinPackageEntryKind kind,
            long length,
            long creationTime,
            long lastWriteTime,
            long changeTime,
            uint fileAttributes,
            uint reparseTag,
            uint numberOfLinks,
            bool deletePending)
        {
            Identity = identity;
            Kind = kind;
            Length = length;
            CreationTime = creationTime;
            LastWriteTime = lastWriteTime;
            ChangeTime = changeTime;
            FileAttributes = fileAttributes;
            ReparseTag = reparseTag;
            NumberOfLinks = numberOfLinks;
            DeletePending = deletePending;
        }

        public bool Equals(WindowsSkinPackageEntryMetadata other)
            => Identity == other.Identity
               && Kind == other.Kind
               && Length == other.Length
               && CreationTime == other.CreationTime
               && LastWriteTime == other.LastWriteTime
               && ChangeTime == other.ChangeTime
               && FileAttributes == other.FileAttributes
               && ReparseTag == other.ReparseTag
               && NumberOfLinks == other.NumberOfLinks
               && DeletePending == other.DeletePending;

        public override bool Equals(object? obj) => obj is WindowsSkinPackageEntryMetadata other && Equals(other);

        public override int GetHashCode()
            => HashCode.Combine(
                HashCode.Combine(Identity, Kind, Length, CreationTime, LastWriteTime),
                HashCode.Combine(ChangeTime, FileAttributes, ReparseTag, NumberOfLinks, DeletePending));

        public override string ToString() => $"{nameof(WindowsSkinPackageEntryMetadata)}:{Kind}";
    }

    internal sealed class WindowsSkinPackageDirectoryEntry
    {
        internal string Name { get; }

        internal WindowsSkinPackageEntryMetadata Metadata { get; }

        public WindowsSkinPackageDirectoryEntry(string name, WindowsSkinPackageEntryMetadata metadata)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Metadata = metadata;
        }

        public override string ToString() => $"{nameof(WindowsSkinPackageDirectoryEntry)}:{Metadata.Kind}";
    }

    internal interface IWindowsSkinPackageCaptureHandle : IDisposable
    {
    }

    internal interface IWindowsSkinPackageCaptureFileSystem
    {
        IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter);

        IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
            IWindowsSkinPackageCaptureHandle directory,
            int maxEntries,
            CancellationToken cancellationToken);

        IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
            IWindowsSkinPackageCaptureHandle parent,
            string name,
            WindowsSkinPackageOpenMode mode,
            SkinManagedPackageCaptureRejectionReason unavailableReason);

        WindowsSkinPackageEntryMetadata QueryMetadata(IWindowsSkinPackageCaptureHandle handle);

        Stream CreateNonOwningReadStream(IWindowsSkinPackageCaptureHandle file);
    }

    internal sealed class WindowsSkinPackageCaptureFileSystemException : Exception
    {
        public SkinManagedPackageCaptureRejectionReason RejectionReason { get; }

        public WindowsSkinPackageCaptureFileSystemException(SkinManagedPackageCaptureRejectionReason rejectionReason)
            : base(nameof(WindowsSkinPackageCaptureFileSystemException))
        {
            if (!Enum.IsDefined(rejectionReason)
                || rejectionReason is SkinManagedPackageCaptureRejectionReason.None or SkinManagedPackageCaptureRejectionReason.CapsuleRejected)
                throw new ArgumentOutOfRangeException(nameof(rejectionReason));

            RejectionReason = rejectionReason;
        }

        public override string ToString() => $"{nameof(WindowsSkinPackageCaptureFileSystemException)}:{RejectionReason}";
    }

    /// <summary>
    /// Captures one resolver-issued managed package using fixed Windows handles and no-follow, handle-relative opens.
    /// </summary>
    /// <remarks>
    /// The guarantee is deliberately narrower than a filesystem transaction: every published byte came from a held
    /// identity handle, and mutations observed before final validation fail the capture. No handle or deferred source
    /// escapes this method, and this type has no production <c>SkinManager</c> caller yet.
    /// </remarks>
    [SupportedOSPlatform("windows10.0.16299")]
    internal sealed class WindowsSkinManagedPackageCapture
    {
        private const int max_authority_directory_entries = 65536;
        private const int max_windows_component_characters = 255;

        private readonly IWindowsSkinPackageCaptureFileSystem fileSystem;

        public WindowsSkinManagedPackageCapture()
            : this(new NativeWindowsSkinPackageCaptureFileSystem())
        {
        }

        internal WindowsSkinManagedPackageCapture(IWindowsSkinPackageCaptureFileSystem fileSystem)
        {
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public SkinManagedPackageCaptureResult Capture(
            SkinManagedPackageCaptureRequest? request,
            SkinPackageRevisionCapsuleLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (request == null || !tryParseDataRoot(request, out char driveLetter, out string[] dataRootSegments))
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            limits ??= SkinPackageRevisionCapsuleLimits.Default;

            var handles = new List<IWindowsSkinPackageCaptureHandle>();
            SkinPackageRevisionCapsule? provisionalCapsule = null;

            try
            {
                IWindowsSkinPackageCaptureHandle volumeRoot = own(fileSystem.OpenLocalVolumeRoot(driveLetter), handles);
                WindowsSkinPackageEntryMetadata volumeRootMetadata = queryStableRoot(volumeRoot);
                var authorityNodes = new List<NodeRecord>
                {
                    new NodeRecord(volumeRoot, volumeRootMetadata),
                };
                var authorityLinks = new List<AuthorityLinkRecord>();

                IWindowsSkinPackageCaptureHandle current = volumeRoot;

                foreach (string segment in dataRootSegments)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    OpenedDirectory opened = openExpectedDirectory(
                        current,
                        segment,
                        WindowsSkinPackageOpenMode.AuthorityDirectory,
                        SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                        cancellationToken,
                        handles);
                    authorityLinks.Add(new AuthorityLinkRecord(current, opened.CanonicalName, opened.Metadata));
                    current = opened.Handle;
                    authorityNodes.Add(new NodeRecord(current, opened.Metadata));
                }

                cancellationToken.ThrowIfCancellationRequested();
                OpenedDirectory managedRoot = openExpectedDirectory(
                    current,
                    SkinFilesystemStorageResolver.MANAGED_ROOT_DIRECTORY,
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    handles);
                authorityNodes.Add(new NodeRecord(managedRoot.Handle, managedRoot.Metadata));
                authorityLinks.Add(new AuthorityLinkRecord(current, managedRoot.CanonicalName, managedRoot.Metadata));

                cancellationToken.ThrowIfCancellationRequested();
                OpenedDirectory packageRoot = openExpectedDirectory(
                    managedRoot.Handle,
                    request.PackageDirectoryName,
                    WindowsSkinPackageOpenMode.CapturedDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageUnavailable,
                    cancellationToken,
                    handles);

                var capture = new CaptureState(fileSystem, limits, handles, cancellationToken);
                capture.CapturePackage(packageRoot.Handle, packageRoot.Metadata);
                capture.ValidatePinnedNodes();

                cancellationToken.ThrowIfCancellationRequested();
                SkinPackageRevisionCapsuleCreationResult capsuleResult = SkinPackageRevisionCapsuleFactory.Create(
                    capture.CapturedEntries,
                    limits,
                    cancellationToken);

                if (!capsuleResult.IsSuccess)
                    return SkinManagedPackageCaptureResult.RejectCapsule(capsuleResult.RejectionReason);

                provisionalCapsule = capsuleResult.Capsule!;

                cancellationToken.ThrowIfCancellationRequested();
                capture.ValidatePinnedNodes();
                capture.ValidateFinalInventories();
                validateAuthorityNodes(authorityNodes, cancellationToken);
                validateAuthorityLinks(authorityLinks, cancellationToken);
                validatePackageRootPath(
                    managedRoot.Handle,
                    packageRoot.CanonicalName,
                    packageRoot.Metadata,
                    cancellationToken);
                capture.ValidatePinnedNodes();

                cancellationToken.ThrowIfCancellationRequested();
                disposeHandles(handles);
                SkinManagedPackageCaptureResult success = SkinManagedPackageCaptureResult.Success(provisionalCapsule);
                provisionalCapsule = null;
                return success;
            }
            catch (CapsuleRejectionException exception)
            {
                return SkinManagedPackageCaptureResult.RejectCapsule(exception.RejectionReason);
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception)
            {
                return SkinManagedPackageCaptureResult.Reject(exception.RejectionReason);
            }
            finally
            {
                provisionalCapsule?.Dispose();

                disposeHandles(handles);
            }
        }

        /// <summary>
        /// Captures one child which was enumerated from an already-held managed-root authority handle.
        /// </summary>
        /// <remarks>
        /// The caller retains ownership of <paramref name="managedRoot"/>. This method owns every package handle it
        /// opens and returns only an immutable capsule. The enumerated metadata is revalidated against the opened child
        /// and against the final managed-root link before a successful result can escape.
        /// </remarks>
        internal SkinManagedPackageCaptureResult CaptureObservedChild(
            IWindowsSkinPackageCaptureHandle managedRoot,
            WindowsSkinPackageDirectoryEntry candidate,
            SkinPackageRevisionCapsuleLimits? limits = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(managedRoot);
            ArgumentNullException.ThrowIfNull(candidate);
            cancellationToken.ThrowIfCancellationRequested();

            if (!isValidRequestSegment(candidate.Name))
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            if (candidate.Metadata.IsReparsePoint)
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (candidate.Metadata.Kind != WindowsSkinPackageEntryKind.Directory)
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);

            if (!candidate.Metadata.Identity.IsUsable || candidate.Metadata.DeletePending)
                return SkinManagedPackageCaptureResult.Reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);

            limits ??= SkinPackageRevisionCapsuleLimits.Default;

            var handles = new List<IWindowsSkinPackageCaptureHandle>();
            SkinPackageRevisionCapsule? provisionalCapsule = null;

            try
            {
                IWindowsSkinPackageCaptureHandle packageRoot = own(
                    fileSystem.OpenChildNoFollow(
                        managedRoot,
                        candidate.Name,
                        WindowsSkinPackageOpenMode.CapturedDirectory,
                        SkinManagedPackageCaptureRejectionReason.PackageUnavailable),
                    handles);
                WindowsSkinPackageEntryMetadata packageRootMetadata = fileSystem.QueryMetadata(packageRoot);
                validateOpenedEntry(candidate.Metadata, packageRootMetadata);

                var capture = new CaptureState(fileSystem, limits, handles, cancellationToken);
                capture.CapturePackage(packageRoot, packageRootMetadata);
                capture.ValidatePinnedNodes();

                cancellationToken.ThrowIfCancellationRequested();
                SkinPackageRevisionCapsuleCreationResult capsuleResult = SkinPackageRevisionCapsuleFactory.Create(
                    capture.CapturedEntries,
                    limits,
                    cancellationToken);

                if (!capsuleResult.IsSuccess)
                    return SkinManagedPackageCaptureResult.RejectCapsule(capsuleResult.RejectionReason);

                provisionalCapsule = capsuleResult.Capsule!;

                cancellationToken.ThrowIfCancellationRequested();
                capture.ValidatePinnedNodes();
                capture.ValidateFinalInventories();
                validatePackageRootPath(
                    managedRoot,
                    candidate.Name,
                    candidate.Metadata,
                    cancellationToken);
                capture.ValidatePinnedNodes();

                cancellationToken.ThrowIfCancellationRequested();
                disposeHandles(handles);
                SkinManagedPackageCaptureResult success = SkinManagedPackageCaptureResult.Success(provisionalCapsule);
                provisionalCapsule = null;
                return success;
            }
            catch (CapsuleRejectionException exception)
            {
                return SkinManagedPackageCaptureResult.RejectCapsule(exception.RejectionReason);
            }
            catch (WindowsSkinPackageCaptureFileSystemException exception)
            {
                return SkinManagedPackageCaptureResult.Reject(exception.RejectionReason);
            }
            finally
            {
                provisionalCapsule?.Dispose();
                disposeHandles(handles);
            }
        }

        private void validateAuthorityNodes(IReadOnlyList<NodeRecord> nodes, CancellationToken cancellationToken)
        {
            foreach (NodeRecord node in nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WindowsSkinPackageEntryMetadata current = fileSystem.QueryMetadata(node.Handle);

                if (current.IsReparsePoint)
                    throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                if (current.Kind != WindowsSkinPackageEntryKind.Directory
                    || current.Identity != node.Baseline.Identity
                    || current.DeletePending)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }
            }
        }

        private void validateAuthorityLinks(
            IReadOnlyList<AuthorityLinkRecord> links,
            CancellationToken cancellationToken)
        {
            foreach (AuthorityLinkRecord link in links)
            {
                cancellationToken.ThrowIfCancellationRequested();
                WindowsSkinPackageDirectoryEntry[] matches = getDirectoryEntries(link.Parent, cancellationToken)
                                                            .Where(entry => namesEqual(entry.Name, link.CanonicalName))
                                                            .ToArray();

                if (matches.Length > 1)
                    throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

                if (matches.Length != 1 || matches[0].Metadata.Identity != link.Baseline.Identity)
                    throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);

                using IWindowsSkinPackageCaptureHandle reopened = fileSystem.OpenChildNoFollow(
                    link.Parent,
                    link.CanonicalName,
                    WindowsSkinPackageOpenMode.AuthorityDirectory,
                    SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                WindowsSkinPackageEntryMetadata reopenedMetadata = fileSystem.QueryMetadata(reopened);

                if (reopenedMetadata.IsReparsePoint)
                    throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                if (reopenedMetadata.Kind != WindowsSkinPackageEntryKind.Directory
                    || reopenedMetadata.Identity != link.Baseline.Identity
                    || reopenedMetadata.DeletePending)
                {
                    throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
                }
            }
        }

        private void validatePackageRootPath(
            IWindowsSkinPackageCaptureHandle managedRoot,
            string packageCanonicalName,
            WindowsSkinPackageEntryMetadata packageBaseline,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WindowsSkinPackageDirectoryEntry[] matches = getDirectoryEntries(managedRoot, cancellationToken)
                                                        .Where(entry => namesEqual(entry.Name, packageCanonicalName))
                                                        .ToArray();

            if (matches.Length > 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            if (matches.Length != 1 || matches[0].Metadata.Identity != packageBaseline.Identity)
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);

            using IWindowsSkinPackageCaptureHandle reopened = fileSystem.OpenChildNoFollow(
                managedRoot,
                packageCanonicalName,
                WindowsSkinPackageOpenMode.CapturedDirectory,
                SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);

            WindowsSkinPackageEntryMetadata reopenedMetadata = fileSystem.QueryMetadata(reopened);

            if (reopenedMetadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (reopenedMetadata.Kind != WindowsSkinPackageEntryKind.Directory
                || reopenedMetadata.Identity != packageBaseline.Identity
                || reopenedMetadata.DeletePending)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.PackageRootIdentityChanged);
            }
        }

        private OpenedDirectory openExpectedDirectory(
            IWindowsSkinPackageCaptureHandle parent,
            string requestedName,
            WindowsSkinPackageOpenMode mode,
            SkinManagedPackageCaptureRejectionReason unavailableReason,
            CancellationToken cancellationToken,
            List<IWindowsSkinPackageCaptureHandle> handles)
        {
            if (!isValidRequestSegment(requestedName))
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);

            string normalisedRequestedName;

            try
            {
                normalisedRequestedName = requestedName.Normalize(NormalizationForm.FormC);
            }
            catch (ArgumentException)
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.InvalidRequest);
            }

            WindowsSkinPackageDirectoryEntry[] matches = getDirectoryEntries(parent, cancellationToken)
                                                        .Where(entry => namesEqual(entry.Name, normalisedRequestedName))
                                                        .ToArray();

            if (matches.Length == 0)
            {
                // A missing long-name match may still resolve through an 8.3 or another filesystem alias. Probe only
                // through the same parent handle and reject a successful alias open; never consume it.
                try
                {
                    using IWindowsSkinPackageCaptureHandle alias = fileSystem.OpenChildNoFollow(
                        parent,
                        requestedName,
                        mode,
                        unavailableReason);
                    throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception) when (exception.RejectionReason == unavailableReason)
                {
                    throw;
                }
            }

            if (matches.Length != 1)
                throw reject(SkinManagedPackageCaptureRejectionReason.AlternateNameAlias);

            WindowsSkinPackageDirectoryEntry candidate = matches[0];

            if (candidate.Metadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (candidate.Metadata.Kind != WindowsSkinPackageEntryKind.Directory)
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);

            IWindowsSkinPackageCaptureHandle opened = own(
                fileSystem.OpenChildNoFollow(parent, candidate.Name, mode, unavailableReason),
                handles);
            WindowsSkinPackageEntryMetadata openedMetadata = fileSystem.QueryMetadata(opened);
            validateOpenedEntry(candidate.Metadata, openedMetadata);
            return new OpenedDirectory(opened, openedMetadata, candidate.Name);
        }

        private WindowsSkinPackageEntryMetadata queryStableRoot(IWindowsSkinPackageCaptureHandle root)
        {
            WindowsSkinPackageEntryMetadata metadata = fileSystem.QueryMetadata(root);

            if (metadata.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (metadata.Kind != WindowsSkinPackageEntryKind.Directory || !metadata.Identity.IsUsable || metadata.DeletePending)
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping);

            return metadata;
        }

        private IReadOnlyList<WindowsSkinPackageDirectoryEntry> getDirectoryEntries(
            IWindowsSkinPackageCaptureHandle directory,
            CancellationToken cancellationToken)
            => fileSystem.Enumerate(directory, max_authority_directory_entries, cancellationToken)
                         .Where(entry => entry.Name is not "." and not "..")
                         .ToArray();

        private static bool namesEqual(string candidate, string requestedName)
        {
            try
            {
                return string.Equals(
                    candidate.Normalize(NormalizationForm.FormC),
                    requestedName.Normalize(NormalizationForm.FormC),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void validateOpenedEntry(
            WindowsSkinPackageEntryMetadata enumerated,
            WindowsSkinPackageEntryMetadata opened)
        {
            if (opened.IsReparsePoint || enumerated.IsReparsePoint)
                throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

            if (!opened.Identity.IsUsable
                || opened.DeletePending
                || opened.Identity != enumerated.Identity
                || opened.Kind != enumerated.Kind
                || opened.FileAttributes != enumerated.FileAttributes
                || opened.ReparseTag != enumerated.ReparseTag
                || (opened.Kind == WindowsSkinPackageEntryKind.File
                    && (opened.Length != enumerated.Length
                        || opened.CreationTime != enumerated.CreationTime
                        || opened.LastWriteTime != enumerated.LastWriteTime
                        || opened.ChangeTime != enumerated.ChangeTime)))
            {
                throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
            }
        }

        private static bool tryParseDataRoot(
            SkinManagedPackageCaptureRequest request,
            out char driveLetter,
            out string[] segments)
        {
            driveLetter = default;
            segments = Array.Empty<string>();

            string path = request.NormalisedDataRootAbsolutePath;

            if (string.IsNullOrWhiteSpace(path)
                || string.IsNullOrEmpty(request.PackageDirectoryName)
                || !isValidRequestSegment(request.PackageDirectoryName)
                || path.Length < 3
                || !char.IsAsciiLetter(path[0])
                || path[1] != ':'
                || path[2] is not ('\\' or '/'))
            {
                return false;
            }

            string normalised = path.Replace('/', '\\').TrimEnd('\\');

            if (normalised.Length == 2)
            {
                driveLetter = char.ToUpperInvariant(normalised[0]);
                return true;
            }

            if (normalised.Length < 4 || normalised[2] != '\\')
                return false;

            string[] parsedSegments = normalised[3..].Split('\\', StringSplitOptions.None);

            if (parsedSegments.Any(segment => !isValidRequestSegment(segment)))
                return false;

            driveLetter = char.ToUpperInvariant(normalised[0]);
            segments = parsedSegments;
            return true;
        }

        private static bool isValidRequestSegment(string segment)
        {
            if (segment.Length > max_windows_component_characters
                || !SkinPackageResourceNameValidator.IsValidWindowsSegment(segment))
            {
                return false;
            }

            try
            {
                return segment.Normalize(NormalizationForm.FormC).Length <= max_windows_component_characters;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static IWindowsSkinPackageCaptureHandle own(
            IWindowsSkinPackageCaptureHandle handle,
            List<IWindowsSkinPackageCaptureHandle> handles)
        {
            if (handle == null)
                throw new InvalidOperationException("The native adapter returned no handle.");

            handles.Add(handle);
            return handle;
        }

        private static WindowsSkinPackageCaptureFileSystemException reject(SkinManagedPackageCaptureRejectionReason reason)
            => new WindowsSkinPackageCaptureFileSystemException(reason);

        private static void disposeHandles(List<IWindowsSkinPackageCaptureHandle> handles)
        {
            Exception? firstException = null;

            for (int i = handles.Count - 1; i >= 0; i--)
            {
                try
                {
                    handles[i].Dispose();
                }
                catch (Exception exception)
                {
                    firstException ??= exception;
                }
            }

            handles.Clear();

            if (firstException != null)
                ExceptionDispatchInfo.Capture(firstException).Throw();
        }

        private sealed class CaptureState
        {
            private readonly IWindowsSkinPackageCaptureFileSystem fileSystem;
            private readonly SkinPackageRevisionCapsuleLimits limits;
            private readonly List<IWindowsSkinPackageCaptureHandle> handles;
            private readonly CancellationToken cancellationToken;
            private readonly List<NodeRecord> nodes = new List<NodeRecord>();
            private readonly List<DirectoryRecord> directories = new List<DirectoryRecord>();
            private readonly Dictionary<string, SkinPackageCapturedEntryKind> logicalEntries = new Dictionary<string, SkinPackageCapturedEntryKind>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<WindowsSkinPackagePhysicalIdentity> physicalIdentities = new HashSet<WindowsSkinPackagePhysicalIdentity>();
            private int fileCount;
            private long totalBytes;

            public List<SkinPackageCapturedEntry?> CapturedEntries { get; } = new List<SkinPackageCapturedEntry?>();

            public CaptureState(
                IWindowsSkinPackageCaptureFileSystem fileSystem,
                SkinPackageRevisionCapsuleLimits limits,
                List<IWindowsSkinPackageCaptureHandle> handles,
                CancellationToken cancellationToken)
            {
                this.fileSystem = fileSystem;
                this.limits = limits;
                this.handles = handles;
                this.cancellationToken = cancellationToken;
            }

            public void CapturePackage(
                IWindowsSkinPackageCaptureHandle packageRoot,
                WindowsSkinPackageEntryMetadata packageRootMetadata)
            {
                if (!physicalIdentities.Add(packageRootMetadata.Identity))
                    throw reject(SkinManagedPackageCaptureRejectionReason.DuplicatePhysicalIdentity);

                captureDirectory(packageRoot, packageRootMetadata, null, 0);
            }

            public void ValidatePinnedNodes()
            {
                foreach (NodeRecord node in nodes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WindowsSkinPackageEntryMetadata current = fileSystem.QueryMetadata(node.Handle);

                    if (current.IsReparsePoint)
                        throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                    if (!current.Equals(node.Baseline))
                    {
                        if (current.Kind == WindowsSkinPackageEntryKind.File && current.NumberOfLinks != 1)
                            throw reject(SkinManagedPackageCaptureRejectionReason.HardLinkedFile);

                        throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);
                    }
                }
            }

            public void ValidateFinalInventories()
            {
                foreach (DirectoryRecord directory in directories)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    WindowsSkinPackageDirectoryEntry[] current;

                    try
                    {
                        current = canonicaliseDirectoryEntries(fileSystem.Enumerate(
                            directory.Handle,
                            directory.Baseline.Length,
                            cancellationToken));
                    }
                    catch (WindowsSkinPackageCaptureFileSystemException exception) when (exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
                    {
                        throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                    }

                    if (!directory.Baseline.SequenceEqual(current, DirectoryEntryComparer.Instance))
                        throw reject(SkinManagedPackageCaptureRejectionReason.InventoryChanged);
                }
            }

            private void captureDirectory(
                IWindowsSkinPackageCaptureHandle directory,
                WindowsSkinPackageEntryMetadata directoryMetadata,
                string? parentResourceName,
                int parentDepth)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int remainingEntries = limits.MaxEntryCount - logicalEntries.Count;
                WindowsSkinPackageDirectoryEntry[] entries;

                try
                {
                    entries = canonicaliseDirectoryEntries(fileSystem.Enumerate(
                        directory,
                        remainingEntries,
                        cancellationToken));
                }
                catch (WindowsSkinPackageCaptureFileSystemException exception) when (exception.RejectionReason == SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded)
                {
                    throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);
                }

                nodes.Add(new NodeRecord(directory, directoryMetadata));
                directories.Add(new DirectoryRecord(directory, entries));

                foreach (WindowsSkinPackageDirectoryEntry entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (entry.Metadata.IsReparsePoint)
                        throw reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered);

                    string rawResourceName = parentResourceName == null
                        ? entry.Name
                        : $"{parentResourceName}/{entry.Name}";

                    if (!SkinPackageResourceNameValidator.TryNormalise(rawResourceName, out string resourceName, out int depth))
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.InvalidResourceName);

                    if (depth != parentDepth + 1)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.InvalidResourceName);

                    if (resourceName.Length > limits.MaxResourceNameLength)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.ResourceNameBudgetExceeded);

                    if (depth > limits.MaxDepth)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.DepthBudgetExceeded);

                    SkinPackageCapturedEntryKind capturedKind = entry.Metadata.Kind switch
                    {
                        WindowsSkinPackageEntryKind.Directory => SkinPackageCapturedEntryKind.Directory,
                        WindowsSkinPackageEntryKind.File => SkinPackageCapturedEntryKind.File,
                        _ => throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType),
                    };

                    if (logicalEntries.TryGetValue(resourceName, out SkinPackageCapturedEntryKind existingKind))
                    {
                        throw new CapsuleRejectionException(existingKind == capturedKind
                            ? SkinPackageRevisionCapsuleRejectionReason.DuplicateEntryPath
                            : SkinPackageRevisionCapsuleRejectionReason.PathTypeConflict);
                    }

                    logicalEntries.Add(resourceName, capturedKind);

                    if (logicalEntries.Count > limits.MaxEntryCount)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.EntryCountBudgetExceeded);

                    WindowsSkinPackageOpenMode openMode = entry.Metadata.Kind == WindowsSkinPackageEntryKind.Directory
                        ? WindowsSkinPackageOpenMode.CapturedDirectory
                        : WindowsSkinPackageOpenMode.CapturedFile;

                    IWindowsSkinPackageCaptureHandle child = own(
                        fileSystem.OpenChildNoFollow(
                            directory,
                            entry.Name,
                            openMode,
                            SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture),
                        handles);
                    WindowsSkinPackageEntryMetadata childMetadata = fileSystem.QueryMetadata(child);
                    validateOpenedEntry(entry.Metadata, childMetadata);

                    if (childMetadata.Kind == WindowsSkinPackageEntryKind.File && childMetadata.NumberOfLinks != 1)
                        throw reject(SkinManagedPackageCaptureRejectionReason.HardLinkedFile);

                    if (!physicalIdentities.Add(childMetadata.Identity))
                        throw reject(SkinManagedPackageCaptureRejectionReason.DuplicatePhysicalIdentity);

                    if (childMetadata.Kind == WindowsSkinPackageEntryKind.Directory)
                    {
                        CapturedEntries.Add(SkinPackageCapturedEntry.CreateDirectory(resourceName));
                        captureDirectory(child, childMetadata, resourceName, depth);
                        continue;
                    }

                    fileCount++;

                    if (fileCount > limits.MaxFileCount)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.FileCountBudgetExceeded);

                    if (childMetadata.Length < 0)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.InvalidDeclaredLength);

                    if (childMetadata.Length > limits.MaxFileBytes)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.FileByteBudgetExceeded);

                    try
                    {
                        totalBytes = checked(totalBytes + childMetadata.Length);
                    }
                    catch (OverflowException)
                    {
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.PackageByteBudgetExceeded);
                    }

                    if (totalBytes > limits.MaxPackageBytes)
                        throw new CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason.PackageByteBudgetExceeded);

                    nodes.Add(new NodeRecord(child, childMetadata));
                    CapturedEntries.Add(SkinPackageCapturedEntry.CreateFile(
                        resourceName,
                        childMetadata.Length,
                        () => fileSystem.CreateNonOwningReadStream(child)));
                }
            }

            private static WindowsSkinPackageDirectoryEntry[] canonicaliseDirectoryEntries(IEnumerable<WindowsSkinPackageDirectoryEntry> entries)
                => entries.Where(entry => entry.Name is not "." and not "..")
                          .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                          .ThenBy(entry => entry.Name, StringComparer.Ordinal)
                          .ToArray();
        }

        private sealed class DirectoryEntryComparer : IEqualityComparer<WindowsSkinPackageDirectoryEntry>
        {
            public static DirectoryEntryComparer Instance { get; } = new DirectoryEntryComparer();

            public bool Equals(WindowsSkinPackageDirectoryEntry? x, WindowsSkinPackageDirectoryEntry? y)
                => ReferenceEquals(x, y)
                   || (x != null
                       && y != null
                       && string.Equals(x.Name, y.Name, StringComparison.Ordinal)
                       && x.Metadata.Equals(y.Metadata));

            public int GetHashCode(WindowsSkinPackageDirectoryEntry obj) => HashCode.Combine(obj.Name, obj.Metadata);
        }

        private sealed class CapsuleRejectionException : Exception
        {
            public SkinPackageRevisionCapsuleRejectionReason RejectionReason { get; }

            public CapsuleRejectionException(SkinPackageRevisionCapsuleRejectionReason rejectionReason)
                : base(nameof(CapsuleRejectionException))
            {
                RejectionReason = rejectionReason;
            }
        }

        private readonly record struct OpenedDirectory(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageEntryMetadata Metadata,
            string CanonicalName);

        private readonly record struct NodeRecord(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageEntryMetadata Baseline);

        private readonly record struct AuthorityLinkRecord(
            IWindowsSkinPackageCaptureHandle Parent,
            string CanonicalName,
            WindowsSkinPackageEntryMetadata Baseline);

        private readonly record struct DirectoryRecord(
            IWindowsSkinPackageCaptureHandle Handle,
            WindowsSkinPackageDirectoryEntry[] Baseline);
    }

    [SupportedOSPlatform("windows10.0.16299")]
    internal sealed class NativeWindowsSkinPackageCaptureFileSystem : IWindowsSkinPackageCaptureFileSystem
    {
        private const uint file_attribute_directory = 0x00000010;
        private const uint file_attribute_reparse_point = 0x00000400;

        public IWindowsSkinPackageCaptureHandle OpenLocalVolumeRoot(char driveLetter)
        {
            char[] target = new char[32768];
            uint targetLength = NativeMethods.QueryDosDeviceW($"{char.ToUpperInvariant(driveLetter)}:", target, target.Length);

            if (targetLength == 0)
                throw mapWin32Error(Marshal.GetLastWin32Error(), SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping);

            int terminator = Array.IndexOf(target, '\0', 0, checked((int)targetLength));
            string currentTarget = new string(target, 0, terminator < 0 ? checked((int)targetLength) : terminator);

            if (!IsExactLocalVolumeTarget(currentTarget))
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedVolumeMapping);

            SafeFileHandle? handle = null;

            try
            {
                int status = NativeMethods.OpenAbsolute(
                    $"{currentTarget}\\",
                    NativeMethods.FILE_LIST_DIRECTORY | NativeMethods.FILE_READ_ATTRIBUTES | NativeMethods.SYNCHRONIZE,
                    NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE | NativeMethods.FILE_SHARE_DELETE,
                    NativeMethods.FILE_DIRECTORY_FILE | NativeMethods.FILE_SYNCHRONOUS_IO_NONALERT | NativeMethods.FILE_OPEN_REPARSE_POINT,
                    out handle);

                if (status != NativeMethods.STATUS_SUCCESS)
                {
                    handle?.Dispose();
                    throw mapNtStatus(status, SkinManagedPackageCaptureRejectionReason.PackageUnavailable);
                }

                if (handle == null || handle.IsInvalid)
                {
                    handle?.Dispose();
                    throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);
                }

                SafeFileHandle owned = handle;
                handle = null;
                return new NativeHandle(owned);
            }
            finally
            {
                handle?.Dispose();
            }
        }

        internal static bool IsExactLocalVolumeTarget(string target)
        {
            const string prefix = "\\Device\\HarddiskVolume";

            if (!target.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || target.Length == prefix.Length)
                return false;

            return uint.TryParse(
                target.AsSpan(prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _);
        }

        public IReadOnlyList<WindowsSkinPackageDirectoryEntry> Enumerate(
            IWindowsSkinPackageCaptureHandle directory,
            int maxEntries,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxEntries);
            cancellationToken.ThrowIfCancellationRequested();
            NativeHandle nativeDirectory = getNativeHandle(directory);
            WindowsSkinPackageEntryMetadata directoryMetadata = QueryMetadata(directory);

            if (directoryMetadata.Kind != WindowsSkinPackageEntryKind.Directory)
                throw reject(SkinManagedPackageCaptureRejectionReason.UnsupportedEntryType);

            const int buffer_size = 64 * 1024;
            IntPtr buffer = Marshal.AllocHGlobal(buffer_size);

            try
            {
                var entries = new List<WindowsSkinPackageDirectoryEntry>();
                bool restart = true;

                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int status = NativeMethods.NtQueryDirectoryFileEx(
                        nativeDirectory.Handle,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        out NativeMethods.IO_STATUS_BLOCK ioStatus,
                        buffer,
                        buffer_size,
                        NativeMethods.FILE_INFORMATION_CLASS.FileIdExtdDirectoryInformation,
                        NativeMethods.SL_RETURN_SINGLE_ENTRY | (restart ? NativeMethods.SL_RESTART_SCAN : 0),
                        IntPtr.Zero);

                    restart = false;

                    if (status == NativeMethods.STATUS_NO_MORE_FILES)
                        break;

                    if (status == NativeMethods.STATUS_BUFFER_OVERFLOW || status == NativeMethods.STATUS_BUFFER_TOO_SMALL)
                        throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                    if (status != NativeMethods.STATUS_SUCCESS)
                        throw mapNtStatus(status, SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                    ulong information = ioStatus.Information.ToUInt64();

                    if (information < NativeMethods.FILE_ID_EXTD_DIRECTORY_INFORMATION_HEADER_SIZE || information > buffer_size)
                        throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                    uint nameLength = unchecked((uint)Marshal.ReadInt32(buffer, NativeMethods.FILE_ID_EXTD_NAME_LENGTH_OFFSET));

                    if ((nameLength & 1) != 0
                        || nameLength > information - NativeMethods.FILE_ID_EXTD_DIRECTORY_INFORMATION_HEADER_SIZE)
                    {
                        throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);
                    }

                    string? name = Marshal.PtrToStringUni(
                        IntPtr.Add(buffer, NativeMethods.FILE_ID_EXTD_DIRECTORY_INFORMATION_HEADER_SIZE),
                        checked((int)nameLength / sizeof(char))) ?? throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (name is "." or "..")
                        continue;

                    if (entries.Count >= maxEntries)
                        throw reject(SkinManagedPackageCaptureRejectionReason.DirectoryEnumerationBudgetExceeded);

                    uint attributes = unchecked((uint)Marshal.ReadInt32(buffer, NativeMethods.FILE_ID_EXTD_ATTRIBUTES_OFFSET));
                    uint reparseTag = unchecked((uint)Marshal.ReadInt32(buffer, NativeMethods.FILE_ID_EXTD_REPARSE_TAG_OFFSET));
                    long length = Marshal.ReadInt64(buffer, NativeMethods.FILE_ID_EXTD_END_OF_FILE_OFFSET);
                    var identity = new WindowsSkinPackagePhysicalIdentity(
                        directoryMetadata.Identity.VolumeSerialNumber,
                        unchecked((ulong)Marshal.ReadInt64(buffer, NativeMethods.FILE_ID_EXTD_FILE_ID_OFFSET)),
                        unchecked((ulong)Marshal.ReadInt64(buffer, NativeMethods.FILE_ID_EXTD_FILE_ID_OFFSET + sizeof(long))));

                    if (!identity.IsUsable || length < 0)
                        throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                    var metadata = new WindowsSkinPackageEntryMetadata(
                        identity,
                        (attributes & file_attribute_directory) != 0 ? WindowsSkinPackageEntryKind.Directory : WindowsSkinPackageEntryKind.File,
                        length,
                        Marshal.ReadInt64(buffer, NativeMethods.FILE_ID_EXTD_CREATION_TIME_OFFSET),
                        Marshal.ReadInt64(buffer, NativeMethods.FILE_ID_EXTD_LAST_WRITE_TIME_OFFSET),
                        Marshal.ReadInt64(buffer, NativeMethods.FILE_ID_EXTD_CHANGE_TIME_OFFSET),
                        attributes,
                        reparseTag,
                        0,
                        false);
                    entries.Add(new WindowsSkinPackageDirectoryEntry(name, metadata));
                }

                cancellationToken.ThrowIfCancellationRequested();
                return entries;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        public IWindowsSkinPackageCaptureHandle OpenChildNoFollow(
            IWindowsSkinPackageCaptureHandle parent,
            string name,
            WindowsSkinPackageOpenMode mode,
            SkinManagedPackageCaptureRejectionReason unavailableReason)
        {
            NativeHandle nativeParent = getNativeHandle(parent);

            if (!SkinPackageResourceNameValidator.IsValidWindowsSegment(name))
                throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            uint desiredAccess;
            uint shareAccess;
            uint openOptions = NativeMethods.FILE_SYNCHRONOUS_IO_NONALERT | NativeMethods.FILE_OPEN_REPARSE_POINT;

            switch (mode)
            {
                case WindowsSkinPackageOpenMode.AuthorityDirectory:
                    desiredAccess = NativeMethods.FILE_LIST_DIRECTORY | NativeMethods.FILE_READ_ATTRIBUTES | NativeMethods.SYNCHRONIZE;
                    shareAccess = NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE;
                    openOptions |= NativeMethods.FILE_DIRECTORY_FILE;
                    break;

                case WindowsSkinPackageOpenMode.CapturedDirectory:
                    desiredAccess = NativeMethods.FILE_LIST_DIRECTORY | NativeMethods.FILE_READ_ATTRIBUTES | NativeMethods.SYNCHRONIZE;
                    shareAccess = NativeMethods.FILE_SHARE_READ;
                    openOptions |= NativeMethods.FILE_DIRECTORY_FILE;
                    break;

                case WindowsSkinPackageOpenMode.CapturedFile:
                    desiredAccess = NativeMethods.FILE_READ_DATA | NativeMethods.FILE_READ_ATTRIBUTES | NativeMethods.SYNCHRONIZE;
                    shareAccess = NativeMethods.FILE_SHARE_READ;
                    openOptions |= NativeMethods.FILE_NON_DIRECTORY_FILE;
                    break;

                case WindowsSkinPackageOpenMode.MutationSourceDirectory:
                    desiredAccess = NativeMethods.FILE_LIST_DIRECTORY
                                    | NativeMethods.FILE_READ_ATTRIBUTES
                                    | NativeMethods.DELETE
                                    | NativeMethods.SYNCHRONIZE;
                    shareAccess = NativeMethods.FILE_SHARE_READ;
                    openOptions |= NativeMethods.FILE_DIRECTORY_FILE;
                    break;

                case WindowsSkinPackageOpenMode.MutationSourceVerificationDirectory:
                    desiredAccess = NativeMethods.FILE_LIST_DIRECTORY | NativeMethods.FILE_READ_ATTRIBUTES | NativeMethods.SYNCHRONIZE;
                    shareAccess = NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_DELETE;
                    openOptions |= NativeMethods.FILE_DIRECTORY_FILE;
                    break;

                default:
                    throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);
            }

            SafeFileHandle? opened = null;
            bool parentAddedRef = false;

            try
            {
                nativeParent.Handle.DangerousAddRef(ref parentAddedRef);
                int status = NativeMethods.OpenRelative(
                    nativeParent.Handle.DangerousGetHandle(),
                    name,
                    desiredAccess,
                    shareAccess,
                    openOptions,
                    out opened);

                if (status != NativeMethods.STATUS_SUCCESS)
                {
                    opened?.Dispose();
                    throw mapNtStatus(status, unavailableReason);
                }

                if (opened == null || opened.IsInvalid)
                {
                    opened?.Dispose();
                    throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);
                }

                SafeFileHandle owned = opened;
                opened = null;
                return new NativeHandle(owned);
            }
            finally
            {
                opened?.Dispose();

                if (parentAddedRef)
                    nativeParent.Handle.DangerousRelease();
            }
        }

        public WindowsSkinPackageEntryMetadata QueryMetadata(IWindowsSkinPackageCaptureHandle handle)
        {
            NativeHandle nativeHandle = getNativeHandle(handle);
            NativeMethods.FILE_ID_INFO id = queryInfo<NativeMethods.FILE_ID_INFO>(nativeHandle.Handle, NativeMethods.FILE_INFO_BY_HANDLE_CLASS.FileIdInfo);
            NativeMethods.FILE_BASIC_INFO basic = queryInfo<NativeMethods.FILE_BASIC_INFO>(nativeHandle.Handle, NativeMethods.FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo);
            NativeMethods.FILE_STANDARD_INFO standard = queryInfo<NativeMethods.FILE_STANDARD_INFO>(nativeHandle.Handle, NativeMethods.FILE_INFO_BY_HANDLE_CLASS.FileStandardInfo);
            NativeMethods.FILE_ATTRIBUTE_TAG_INFO attributeTag = queryInfo<NativeMethods.FILE_ATTRIBUTE_TAG_INFO>(nativeHandle.Handle, NativeMethods.FILE_INFO_BY_HANDLE_CLASS.FileAttributeTagInfo);

            if (basic.FileAttributes != attributeTag.FileAttributes || standard.EndOfFile < 0)
                throw reject(SkinManagedPackageCaptureRejectionReason.EntryChangedDuringCapture);

            var identity = new WindowsSkinPackagePhysicalIdentity(
                id.VolumeSerialNumber,
                id.FileId.Part0,
                id.FileId.Part1);

            if (!identity.IsUsable)
                throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            return new WindowsSkinPackageEntryMetadata(
                identity,
                standard.Directory != 0 ? WindowsSkinPackageEntryKind.Directory : WindowsSkinPackageEntryKind.File,
                standard.EndOfFile,
                basic.CreationTime,
                basic.LastWriteTime,
                basic.ChangeTime,
                basic.FileAttributes,
                attributeTag.ReparseTag,
                standard.NumberOfLinks,
                standard.DeletePending != 0);
        }

        public Stream CreateNonOwningReadStream(IWindowsSkinPackageCaptureHandle file)
        {
            NativeHandle nativeFile = getNativeHandle(file);
            return new NonOwningRandomAccessReadStream(nativeFile.Handle);
        }

        private static T queryInfo<T>(SafeFileHandle handle, NativeMethods.FILE_INFO_BY_HANDLE_CLASS informationClass)
            where T : unmanaged
        {
            unsafe
            {
                T value = default;

                if (!NativeMethods.GetFileInformationByHandleEx(handle, informationClass, (IntPtr)(&value), (uint)sizeof(T)))
                    throw mapWin32Error(Marshal.GetLastWin32Error(), SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

                return value;
            }
        }

        private static NativeHandle getNativeHandle(IWindowsSkinPackageCaptureHandle handle)
        {
            if (handle is not NativeHandle nativeHandle || nativeHandle.Handle.IsClosed || nativeHandle.Handle.IsInvalid)
                throw reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure);

            return nativeHandle;
        }

        private static WindowsSkinPackageCaptureFileSystemException mapNtStatus(
            int status,
            SkinManagedPackageCaptureRejectionReason unavailableReason)
        {
            return status switch
            {
                NativeMethods.STATUS_ACCESS_DENIED => reject(SkinManagedPackageCaptureRejectionReason.AccessDenied),
                NativeMethods.STATUS_SHARING_VIOLATION => reject(SkinManagedPackageCaptureRejectionReason.SourceBusy),
                NativeMethods.STATUS_REPARSE_POINT_ENCOUNTERED or NativeMethods.STATUS_STOPPED_ON_SYMLINK => reject(SkinManagedPackageCaptureRejectionReason.ReparsePointEncountered),
                NativeMethods.STATUS_OBJECT_NAME_NOT_FOUND or NativeMethods.STATUS_OBJECT_PATH_NOT_FOUND or NativeMethods.STATUS_NOT_A_DIRECTORY or NativeMethods.STATUS_FILE_IS_A_DIRECTORY => reject(unavailableReason),
                _ => reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure),
            };
        }

        private static WindowsSkinPackageCaptureFileSystemException mapWin32Error(
            int error,
            SkinManagedPackageCaptureRejectionReason unavailableReason)
        {
            return error switch
            {
                NativeMethods.ERROR_ACCESS_DENIED => reject(SkinManagedPackageCaptureRejectionReason.AccessDenied),
                NativeMethods.ERROR_SHARING_VIOLATION => reject(SkinManagedPackageCaptureRejectionReason.SourceBusy),
                NativeMethods.ERROR_FILE_NOT_FOUND or NativeMethods.ERROR_PATH_NOT_FOUND => reject(unavailableReason),
                _ => reject(SkinManagedPackageCaptureRejectionReason.NativeIoFailure),
            };
        }

        private static WindowsSkinPackageCaptureFileSystemException reject(SkinManagedPackageCaptureRejectionReason reason)
            => new WindowsSkinPackageCaptureFileSystemException(reason);

        private sealed class NativeHandle : IWindowsSkinPackageCaptureHandle
        {
            internal SafeFileHandle Handle { get; }

            public NativeHandle(SafeFileHandle handle)
            {
                Handle = handle;
            }

            public void Dispose() => Handle.Dispose();

            public override string ToString() => nameof(NativeHandle);
        }

        private sealed class NonOwningRandomAccessReadStream : Stream
        {
            private const int max_read_size = 1024 * 1024;

            private readonly SafeFileHandle handle;
            private long position;
            private bool addedRef;
            private bool disposed;

            public override bool CanRead => !disposed;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => position;
                set => throw new NotSupportedException();
            }

            public NonOwningRandomAccessReadStream(SafeFileHandle handle)
            {
                this.handle = handle;
                handle.DangerousAddRef(ref addedRef);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                int read = RandomAccess.Read(handle, buffer.AsSpan(offset, Math.Min(count, max_read_size)), position);
                position += read;
                return read;
            }

            public override int Read(Span<byte> buffer)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                int read = RandomAccess.Read(handle, buffer[..Math.Min(buffer.Length, max_read_size)], position);
                position += read;
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposed)
                {
                    disposed = true;

                    if (addedRef)
                    {
                        handle.DangerousRelease();
                        addedRef = false;
                    }
                }

                base.Dispose(disposing);
            }

            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    [SupportedOSPlatform("windows10.0.16299")]
    internal static class NativeMethods
    {
        internal const int STATUS_SUCCESS = 0;
        internal const int STATUS_BUFFER_OVERFLOW = unchecked((int)0x80000005);
        internal const int STATUS_NO_MORE_FILES = unchecked((int)0x80000006);
        internal const int STATUS_STOPPED_ON_SYMLINK = unchecked((int)0x8000002D);
        internal const int STATUS_INVALID_INFO_CLASS = unchecked((int)0xC0000003);
        internal const int STATUS_INVALID_PARAMETER = unchecked((int)0xC000000D);
        internal const int STATUS_ACCESS_DENIED = unchecked((int)0xC0000022);
        internal const int STATUS_BUFFER_TOO_SMALL = unchecked((int)0xC0000023);
        internal const int STATUS_OBJECT_NAME_NOT_FOUND = unchecked((int)0xC0000034);
        internal const int STATUS_OBJECT_PATH_NOT_FOUND = unchecked((int)0xC000003A);
        internal const int STATUS_SHARING_VIOLATION = unchecked((int)0xC0000043);
        internal const int STATUS_FILE_IS_A_DIRECTORY = unchecked((int)0xC00000BA);
        internal const int STATUS_NOT_A_DIRECTORY = unchecked((int)0xC0000103);
        internal const int STATUS_REPARSE_POINT_ENCOUNTERED = unchecked((int)0xC000050B);

        internal const uint FILE_LIST_DIRECTORY = 0x00000001;
        internal const uint FILE_READ_DATA = 0x00000001;
        internal const uint FILE_READ_ATTRIBUTES = 0x00000080;
        internal const uint DELETE = 0x00010000;
        internal const uint SYNCHRONIZE = 0x00100000;
        internal const uint FILE_SHARE_READ = 0x00000001;
        internal const uint FILE_SHARE_WRITE = 0x00000002;
        internal const uint FILE_SHARE_DELETE = 0x00000004;
        internal const uint FILE_DIRECTORY_FILE = 0x00000001;
        internal const uint FILE_SYNCHRONOUS_IO_NONALERT = 0x00000020;
        internal const uint FILE_NON_DIRECTORY_FILE = 0x00000040;
        internal const uint FILE_OPEN_REPARSE_POINT = 0x00200000;
        internal const uint OBJ_CASE_INSENSITIVE = 0x00000040;
        internal const uint OBJ_DONT_REPARSE = 0x00001000;
        internal const uint SL_RESTART_SCAN = 0x00000001;
        internal const uint SL_RETURN_SINGLE_ENTRY = 0x00000002;

        internal const int ERROR_FILE_NOT_FOUND = 2;
        internal const int ERROR_PATH_NOT_FOUND = 3;
        internal const int ERROR_ACCESS_DENIED = 5;
        internal const int ERROR_SHARING_VIOLATION = 32;

        internal const int FILE_ID_EXTD_DIRECTORY_INFORMATION_HEADER_SIZE = 88;
        internal const int FILE_ID_EXTD_CREATION_TIME_OFFSET = 8;
        internal const int FILE_ID_EXTD_LAST_WRITE_TIME_OFFSET = 24;
        internal const int FILE_ID_EXTD_CHANGE_TIME_OFFSET = 32;
        internal const int FILE_ID_EXTD_END_OF_FILE_OFFSET = 40;
        internal const int FILE_ID_EXTD_ATTRIBUTES_OFFSET = 56;
        internal const int FILE_ID_EXTD_NAME_LENGTH_OFFSET = 60;
        internal const int FILE_ID_EXTD_REPARSE_TAG_OFFSET = 68;
        internal const int FILE_ID_EXTD_FILE_ID_OFFSET = 72;

        [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint QueryDosDeviceW(string deviceName, [Out] char[] targetPath, int maxLength);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandleEx(
            SafeFileHandle file,
            FILE_INFO_BY_HANDLE_CLASS fileInformationClass,
            IntPtr fileInformation,
            uint bufferSize);

        [DllImport("ntdll.dll", ExactSpelling = true)]
        private static extern int NtOpenFile(
            out SafeFileHandle fileHandle,
            uint desiredAccess,
            ref OBJECT_ATTRIBUTES objectAttributes,
            out IO_STATUS_BLOCK ioStatusBlock,
            uint shareAccess,
            uint openOptions);

        [DllImport("ntdll.dll", ExactSpelling = true)]
        internal static extern int NtQueryDirectoryFileEx(
            SafeFileHandle fileHandle,
            IntPtr eventHandle,
            IntPtr apcRoutine,
            IntPtr apcContext,
            out IO_STATUS_BLOCK ioStatusBlock,
            IntPtr fileInformation,
            uint length,
            FILE_INFORMATION_CLASS fileInformationClass,
            uint queryFlags,
            IntPtr fileName);

        internal static unsafe int OpenRelative(
            IntPtr parentHandle,
            string name,
            uint desiredAccess,
            uint shareAccess,
            uint openOptions,
            out SafeFileHandle fileHandle)
            => open(parentHandle, name, desiredAccess, shareAccess, openOptions, out fileHandle);

        internal static unsafe int OpenAbsolute(
            string name,
            uint desiredAccess,
            uint shareAccess,
            uint openOptions,
            out SafeFileHandle fileHandle)
            => open(IntPtr.Zero, name, desiredAccess, shareAccess, openOptions, out fileHandle);

        private static unsafe int open(
            IntPtr parentHandle,
            string name,
            uint desiredAccess,
            uint shareAccess,
            uint openOptions,
            out SafeFileHandle fileHandle)
        {
            fixed (char* nameBuffer = name)
            {
                var unicodeName = new UNICODE_STRING
                {
                    Length = checked((ushort)(name.Length * sizeof(char))),
                    MaximumLength = checked((ushort)((name.Length + 1) * sizeof(char))),
                    Buffer = (IntPtr)nameBuffer,
                };
                var attributes = new OBJECT_ATTRIBUTES
                {
                    Length = (uint)Marshal.SizeOf<OBJECT_ATTRIBUTES>(),
                    RootDirectory = parentHandle,
                    ObjectName = (IntPtr)(&unicodeName),
                    Attributes = OBJ_CASE_INSENSITIVE | OBJ_DONT_REPARSE,
                };

                return NtOpenFile(
                    out fileHandle,
                    desiredAccess,
                    ref attributes,
                    out _,
                    shareAccess,
                    openOptions);
            }
        }

        internal static bool HasExpectedLayouts
            => Marshal.SizeOf<UNICODE_STRING>() == (IntPtr.Size == 8 ? 16 : 8)
               && Marshal.SizeOf<OBJECT_ATTRIBUTES>() == (IntPtr.Size == 8 ? 48 : 24)
               && Marshal.SizeOf<IO_STATUS_BLOCK>() == (IntPtr.Size == 8 ? 16 : 8)
               && Marshal.SizeOf<FILE_ID_128>() == 16
               && Marshal.SizeOf<FILE_ID_INFO>() == 24
               && Marshal.SizeOf<FILE_BASIC_INFO>() == 40
               && Marshal.SizeOf<FILE_STANDARD_INFO>() == 24
               && Marshal.SizeOf<FILE_ATTRIBUTE_TAG_INFO>() == 8;

        internal enum FILE_INFORMATION_CLASS
        {
            FileIdExtdDirectoryInformation = 60,
        }

        internal enum FILE_INFO_BY_HANDLE_CLASS
        {
            FileBasicInfo = 0,
            FileStandardInfo = 1,
            FileAttributeTagInfo = 9,
            FileIdInfo = 18,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct UNICODE_STRING
        {
            public ushort Length;
            public ushort MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct OBJECT_ATTRIBUTES
        {
            public uint Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public uint Attributes;
            public IntPtr SecurityDescriptor;
            public IntPtr SecurityQualityOfService;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct IO_STATUS_BLOCK
        {
            public IntPtr StatusOrPointer;
            public UIntPtr Information;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILE_ID_128
        {
            public ulong Part0;
            public ulong Part1;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILE_ID_INFO
        {
            public ulong VolumeSerialNumber;
            public FILE_ID_128 FileId;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILE_BASIC_INFO
        {
            public long CreationTime;
            public long LastAccessTime;
            public long LastWriteTime;
            public long ChangeTime;
            public uint FileAttributes;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILE_STANDARD_INFO
        {
            public long AllocationSize;
            public long EndOfFile;
            public uint NumberOfLinks;
            public byte DeletePending;
            public byte Directory;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct FILE_ATTRIBUTE_TAG_INFO
        {
            public uint FileAttributes;
            public uint ReparseTag;
        }
    }
}
