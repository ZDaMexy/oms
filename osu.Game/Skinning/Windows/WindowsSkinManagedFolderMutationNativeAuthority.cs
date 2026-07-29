// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Runtime.Versioning;
using System.Threading;
using osu.Framework.Platform;

namespace osu.Game.Skinning.Windows
{
    /// <summary>
    /// Opens a held, handle-relative authority session for managed-folder mutations.
    /// </summary>
    /// <remarks>
    /// This adapter fixes existing source identity and an absent direct-child name slot beneath the same held
    /// <c>chartskin</c> root. Its only write primitive is an identity-preserving, handle-relative, no-replace rename.
    /// </remarks>
    internal sealed class WindowsSkinManagedFolderMutationNativeAuthority : ISkinManagedFolderMutationNativeAuthority
    {
        private readonly Func<string> getDataRootAbsolutePath;
        private readonly IWindowsSkinPackageCaptureFileSystem? fileSystem;

        public WindowsSkinManagedFolderMutationNativeAuthority(Storage storage)
        {
            ArgumentNullException.ThrowIfNull(storage);
            getDataRootAbsolutePath = () => storage.GetFullPath(string.Empty);
        }

        internal WindowsSkinManagedFolderMutationNativeAuthority(
            string dataRootAbsolutePath,
            IWindowsSkinPackageCaptureFileSystem fileSystem)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(dataRootAbsolutePath);
            getDataRootAbsolutePath = () => dataRootAbsolutePath;
            this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        public ISkinManagedFolderMutationNativeSession Open(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 16299) || !NativeMethods.HasExpectedLayouts)
                throw new SkinManagedFolderMutationNativeAuthorityException();

            string dataRootAbsolutePath;

            try
            {
                dataRootAbsolutePath = getDataRootAbsolutePath();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new SkinManagedFolderMutationNativeAuthorityException();
            }

            try
            {
                return openWindows(dataRootAbsolutePath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                throw new SkinManagedFolderMutationNativeAuthorityException();
            }
        }

        [SupportedOSPlatform("windows10.0.16299")]
        private ISkinManagedFolderMutationNativeSession openWindows(
            string dataRootAbsolutePath,
            CancellationToken cancellationToken)
            => new Session(
                WindowsSkinManagedAuthoritySession.Open(
                    dataRootAbsolutePath,
                    fileSystem ?? new NativeWindowsSkinPackageCaptureFileSystem(),
                    cancellationToken));

        [SupportedOSPlatform("windows10.0.16299")]
        private sealed class Session : ISkinManagedFolderMutationNativeSession
        {
            private WindowsSkinManagedAuthoritySession? session;

            public SkinManagedFolderPhysicalIdentity ManagedRootIdentity
                => session?.ManagedRootIdentity ?? throw new ObjectDisposedException(nameof(Session));

            public Session(WindowsSkinManagedAuthoritySession session)
            {
                this.session = session ?? throw new ArgumentNullException(nameof(session));
            }

            public SkinManagedFolderPhysicalIdentity CaptureExistingSource(
                string managedRelativePath,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().CaptureExistingMutationSource(managedRelativePath, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public SkinManagedFolderTargetNameSlot CaptureAbsentTargetNameSlot(
                string managedRelativePath,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().CaptureAbsentMutationTargetNameSlot(managedRelativePath, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public SkinManagedFolderStagedSourceCapture CaptureStagedSource(
                Guid operationId,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().CaptureStagedMutationSource(operationId, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public SkinManagedFolderPhysicalIdentity RenameCapturedSourceToTarget(
                SkinManagedFolderTargetNameSlot targetNameSlot,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().RenameCapturedMutationSourceToTarget(targetNameSlot, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public SkinManagedFolderStagedImportFilesystemResult MoveCapturedStagedSourceToTarget(
                SkinManagedFolderTargetNameSlot targetNameSlot,
                string expectedContentRevision,
                string expectedTreeFingerprint,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().MoveCapturedStagedMutationSourceToTarget(
                        targetNameSlot,
                        expectedContentRevision,
                        expectedTreeFingerprint,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public SkinManagedFolderRenameInspection InspectRenameState(
                string sourceManagedRelativePath,
                string targetManagedRelativePath,
                SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().InspectMutationRenameState(
                        sourceManagedRelativePath,
                        targetManagedRelativePath,
                        expectedSourceIdentity,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public SkinManagedFolderStagedImportInspection InspectStagedImportState(
                Guid operationId,
                string targetManagedRelativePath,
                SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                CancellationToken cancellationToken)
            {
                try
                {
                    return getSession().InspectStagedMutationImportState(
                        operationId,
                        targetManagedRelativePath,
                        expectedStagedRootIdentity,
                        expectedSourceIdentity,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public void CleanupExactStagedSource(
                Guid operationId,
                string targetManagedRelativePath,
                SkinManagedFolderPhysicalIdentity expectedStagedRootIdentity,
                SkinManagedFolderPhysicalIdentity expectedSourceIdentity,
                CancellationToken cancellationToken)
            {
                try
                {
                    getSession().CleanupExactStagedMutationSource(
                        operationId,
                        targetManagedRelativePath,
                        expectedStagedRootIdentity,
                        expectedSourceIdentity,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public void ValidateCompleteAndStable(CancellationToken cancellationToken)
            {
                try
                {
                    getSession().ValidateCompleteAndStable(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    throw new SkinManagedFolderMutationNativeAuthorityException();
                }
            }

            public void Dispose()
            {
                WindowsSkinManagedAuthoritySession? held = Interlocked.Exchange(ref session, null);
                held?.Dispose();
            }

            private WindowsSkinManagedAuthoritySession getSession()
                => session ?? throw new ObjectDisposedException(nameof(Session));
        }
    }
}
