// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Models;
using osu.Game.Overlays.Notifications;
using osu.Game.Skinning.Windows;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Skinning
{
    internal enum SkinSelectionRejectionReason
    {
        None,
        FilesystemDeclarationRejected,
        UnmanagedFilesystemRecord,
        ExternalFolderUnsupported,
        InstantiationInfoNotAllowed,
        CaptureRejected,
        CapturedCandidateChanged,
        MutationRecoveryPending,
        ManagedFolderOperationInProgress,
        FactoryRejected,
        PreparationCancelled,
        PreparationFailed,
    }

    internal enum SkinManagedFolderProtectedFallbackCommitResult
    {
        Committed,
        NotRequired,
        WrongThread,
        AuthorityRejected,
        RecoveryPending,
        SelectionDisabled,
        FallbackInvalid,
        PairNotCommitted,
    }

    /// <summary>
    /// Handles the storage and retrieval of <see cref="Skin"/>s.
    /// </summary>
    /// <remarks>
    /// This is also exposed and cached as <see cref="ISkinSource"/> to allow for any component to potentially have skinning support.
    /// For gameplay components, see <see cref="RulesetSkinProvidingContainer"/> which adds extra legacy and toggle logic that may affect the lookup process.
    /// </remarks>
    public class SkinManager : ModelManager<SkinInfo>, ISkinSource, IStorageResourceProvider, IModelImporter<SkinInfo>
    {
        /// <summary>
        /// The OMS built-in candidate skin host.
        /// </summary>
        public Skin DefaultOmsSkin { get; }

        /// <summary>
        /// The default "classic" skin.
        /// </summary>
        public Skin DefaultClassicSkin { get; }

        private readonly AudioManager audio;

        private readonly Scheduler scheduler;

        private readonly GameHost host;

        private readonly IResourceStore<byte[]> resources;

        private readonly Storage storage;

        public readonly Bindable<Skin> CurrentSkin = new Bindable<Skin>();

        public readonly Bindable<Live<SkinInfo>> CurrentSkinInfo = new SkinSelectionBindable(OmsSkin.CreateInfo().ToLiveUnmanaged());

        internal SkinSelectionRejectionReason LastSelectionRejectionReason { get; private set; }

        internal Func<SkinManagedPackageCaptureRequest, CancellationToken, SkinManagedPackageCaptureResult> ManagedFolderCapture { get; set; }
            = (request, cancellationToken) => SkinManagedPackageCapture.Capture(request, cancellationToken: cancellationToken);

        internal Func<SkinInfo, IStorageResourceProvider, SkinPackageRevisionCapsule, SkinManagedFolderFactoryResult> ManagedFolderFactoryCreate { get; set; }
            = SkinManagedFolderFactory.Create;

        internal Action<Action> ManagedFolderCompletionSchedule { get; set; }

        internal Action<Action> ManagedFolderDeleteFallbackSchedule { get; set; }

        internal Action ManagedFolderBeforeCommit { get; set; } = () => { };

        internal Action<Live<SkinInfo>> SelectionRequestBeforeCommitLock { get; set; } = _ => { };

        internal Action ManagedFolderStagedImportAuthorityOpened { get; set; } = () => { };

        internal Action ManagedFolderSelectionFinalBoundaryContended { get; set; } = () => { };

        internal Action ManagedFolderSelectionWaitingForStartup { get; set; } = () => { };

        internal Action ManagedFolderSelectionWaitingForStagedImport { get; set; } = () => { };

        internal SkinManagedFolderOperationCoordinator ManagedFolderOperationCoordinator { get; } = new SkinManagedFolderOperationCoordinator();

        internal SkinManagedFolderMutationAuthority ManagedFolderMutationAuthority { get; }

        internal SkinManagedFolderMutationRecoveryResult InitialManagedFolderMutationRecoveryResult { get; }

        private readonly SkinManagedFolderMutationRecovery managedFolderMutationRecovery;
        private readonly ISkinManagedFolderMutationJournalStore managedFolderMutationJournalStore;
        private readonly SkinManagedFolderRenameOperation managedFolderRenameOperation;
        private readonly SkinManagedFolderStagedImportOperation managedFolderStagedImportOperation;
        private readonly SkinManagedFolderDeleteOperation managedFolderDeleteOperation;
        private readonly object managedFolderRenameLifecycleGate = new object();
        private readonly object managedFolderSelectionLifecycleGate = new object();
        private readonly CancellationTokenSource managedFolderSelectionRetryCancellation = new CancellationTokenSource();
        private readonly HashSet<Task> managedFolderSelectionWorkerTasks = new HashSet<Task>();
        private readonly HashSet<PendingManagedFolderSelectionCompletion> pendingManagedFolderSelectionCompletions = new HashSet<PendingManagedFolderSelectionCompletion>();
        private CancellationTokenSource activeManagedFolderRenameCancellation;
        private Task<SkinManagedFolderRenameOperationResult> activeManagedFolderRenameTask;
        private CancellationTokenSource activeManagedFolderStagedImportCancellation;
        private Task<SkinManagedFolderStagedImportOperationResult> activeManagedFolderStagedImportTask;
        private CancellationTokenSource activeManagedFolderDeleteCancellation;
        private Task<SkinManagedFolderDeleteOperationResult> activeManagedFolderDeleteTask;
        private PendingManagedFolderDeleteFallback pendingManagedFolderDeleteFallback;
        private SkinManagedFolderRenameOperationResult lastManagedFolderRenameResult;
        private SkinManagedFolderStagedImportOperationResult lastManagedFolderStagedImportResult;
        private SkinManagedFolderDeleteOperationResult lastManagedFolderDeleteResult;
        private bool managedFolderMutationShutdown;
        private int managedFolderSelectionShutdown;
        private int managedFolderDeleteFallbackSourceChangeDeferral;
        private int managedFolderDeleteFallbackSourceChangePending;

        internal SkinManagedFolderRenameOperationResult LastManagedFolderRenameResult
            => Volatile.Read(ref lastManagedFolderRenameResult);

        internal bool IsManagedFolderRenameRunning
        {
            get
            {
                lock (managedFolderRenameLifecycleGate)
                    return activeManagedFolderRenameTask is { IsCompleted: false };
            }
        }

        internal SkinManagedFolderStagedImportOperationResult LastManagedFolderStagedImportResult
            => Volatile.Read(ref lastManagedFolderStagedImportResult);

        internal bool IsManagedFolderStagedImportRunning
        {
            get
            {
                lock (managedFolderRenameLifecycleGate)
                {
                    return activeManagedFolderStagedImportTask
                        is { IsCompleted: false };
                }
            }
        }

        internal SkinManagedFolderDeleteOperationResult LastManagedFolderDeleteResult
            => Volatile.Read(ref lastManagedFolderDeleteResult);

        internal bool IsManagedFolderDeleteRunning
        {
            get
            {
                lock (managedFolderRenameLifecycleGate)
                    return activeManagedFolderDeleteTask is { IsCompleted: false };
            }
        }

        private long selectionGeneration;
        private CancellationTokenSource pendingSelectionCancellation;
        private PreparedManagedFolderSelection preparedManagedFolderSelection;

        private readonly SkinImporter skinImporter;

        private readonly LegacySkinExporter skinExporter;

        private readonly IResourceStore<byte[]> userFiles;

        private static readonly Live<SkinInfo> random_skin_info = new SkinInfo
        {
            ID = SkinInfo.RANDOM_SKIN,
            Name = "<随机皮肤>",
        }.ToLiveUnmanaged();

        private static readonly Guid[] retired_upstream_skin_ids =
        {
            SkinInfo.TRIANGLES_SKIN,
            SkinInfo.ARGON_SKIN,
            SkinInfo.ARGON_PRO_SKIN,
            SkinInfo.CLASSIC_SKIN,
            SkinInfo.RETRO_SKIN,
        };

        public override bool PauseImports
        {
            get => base.PauseImports;
            set
            {
                base.PauseImports = value;
                skinImporter.PauseImports = value;
            }
        }

        public SkinManager(Storage storage, RealmAccess realm, GameHost host, IResourceStore<byte[]> resources, AudioManager audio, Scheduler scheduler)
            : base(storage, realm)
        {
            this.storage = storage;
            this.audio = audio;
            this.scheduler = scheduler;
            this.host = host;
            this.resources = resources;
            ManagedFolderCompletionSchedule = completion => scheduler.Add(completion);
            ManagedFolderDeleteFallbackSchedule = completion => scheduler.Add(completion);

            managedFolderMutationJournalStore = new SkinManagedFolderMutationJournalStore(storage);
            var managedFolderMutationNativeAuthority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            ManagedFolderMutationAuthority = new SkinManagedFolderMutationAuthority(
                realm,
                storage,
                ManagedFolderOperationCoordinator,
                managedFolderMutationNativeAuthority,
                managedFolderMutationJournalStore);
            managedFolderRenameOperation = new SkinManagedFolderRenameOperation(realm, ManagedFolderMutationAuthority);
            managedFolderDeleteOperation = new SkinManagedFolderDeleteOperation(
                realm,
                ManagedFolderMutationAuthority,
                commitManagedFolderDeleteFallback);
            managedFolderStagedImportOperation =
                new SkinManagedFolderStagedImportOperation(
                    realm,
                    ManagedFolderMutationAuthority)
                {
                    AuthorityOpened =
                        () => ManagedFolderStagedImportAuthorityOpened(),
                };
            managedFolderMutationRecovery = new SkinManagedFolderMutationRecovery(
                managedFolderMutationJournalStore,
                ManagedFolderOperationCoordinator,
                new SkinManagedFolderMutationRecoveryHandlerRouter(
                    (
                        SkinManagedFolderMutationKind.Rename,
                        new SkinManagedFolderRenameRecoveryHandler(
                            realm,
                            managedFolderMutationNativeAuthority)),
                    (
                        SkinManagedFolderMutationKind.Delete,
                        new SkinManagedFolderDeleteRecoveryHandler(
                            realm,
                            managedFolderMutationNativeAuthority)),
                    (
                        SkinManagedFolderMutationKind.StagedImport,
                        new SkinManagedFolderStagedImportRecoveryHandler(
                            realm,
                            managedFolderMutationNativeAuthority))));
            InitialManagedFolderMutationRecoveryResult = managedFolderMutationRecovery.Recover();

            userFiles = new StorageBackedResourceStore(storage.GetStorageForDirectory("files"));

            skinImporter = new SkinImporter(storage, realm, this, info => !isFilesystemBacked(info))
            {
                PostNotification = obj => PostNotification?.Invoke(obj),
            };

            DefaultOmsSkin = new OmsSkin(this);
            DefaultClassicSkin = new DefaultLegacySkin(this);

            // Keep OMS as the only protected built-in product skin. Upstream built-ins remain
            // available as compatibility types, but are no longer registered as selectable entries.
            realm.Write(r =>
            {
                var existingOmsSkin = r.Find<SkinInfo>(DefaultOmsSkin.SkinInfo.ID);

                if (existingOmsSkin == null)
                    r.Add(DefaultOmsSkin.SkinInfo.Value);
                else
                {
                    existingOmsSkin.Name = DefaultOmsSkin.SkinInfo.Value.Name;
                    existingOmsSkin.Creator = DefaultOmsSkin.SkinInfo.Value.Creator;
                    existingOmsSkin.InstantiationInfo = DefaultOmsSkin.SkinInfo.Value.InstantiationInfo;
                    existingOmsSkin.Protected = true;
                }

                foreach (var retiredSkinId in retired_upstream_skin_ids)
                {
                    var retiredSkin = r.Find<SkinInfo>(retiredSkinId);

                    if (retiredSkin != null)
                        r.Remove(retiredSkin);
                }
            });

            CurrentSkinInfo.ValueChanged += skin =>
            {
                Skin instance;

                if (preparedManagedFolderSelection is { } prepared
                    && ReferenceEquals(prepared.Target, skin.NewValue))
                {
                    preparedManagedFolderSelection = null;
                    instance = prepared.Skin;
                }
                else
                    instance = skin.NewValue.PerformRead(GetSkin);

                CurrentSkin.Value = instance;
            };

            CurrentSkin.Value = DefaultOmsSkin;
            CurrentSkin.ValueChanged += skin =>
            {
                if (!skin.NewValue.SkinInfo.Equals(CurrentSkinInfo.Value))
                    throw new InvalidOperationException($"Setting {nameof(CurrentSkin)}'s value directly is not supported. Use {nameof(CurrentSkinInfo)} instead.");

                if (Volatile.Read(ref managedFolderDeleteFallbackSourceChangeDeferral) != 0)
                {
                    Volatile.Write(ref managedFolderDeleteFallbackSourceChangePending, 1);
                    return;
                }

                SourceChanged?.Invoke();
            };

            ((SkinSelectionBindable)CurrentSkinInfo).SelectionRequested = requestSelection;

            skinExporter = new LegacySkinExporter(storage)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };
        }

        internal SkinManagedFolderMutationRecoveryResult RecoverManagedFolderMutations(CancellationToken cancellationToken = default)
            => managedFolderMutationRecovery.Recover(cancellationToken);

        /// <summary>
        /// Starts one directory-only managed chartskin rename without exposing it through the current skin UI.
        /// </summary>
        /// <remarks>
        /// The operation ID is generated internally and never returned or logged. A successful rename invalidates any
        /// in-flight selection preparation, while the currently active immutable capsule may continue to serve its
        /// existing consumers until a later selection captures the record's new managed path.
        /// </remarks>
        internal Task<SkinManagedFolderRenameOperationResult> RenameManagedFolderAsync(
            Guid recordId,
            string targetChildName,
            CancellationToken cancellationToken = default)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown)
                    return completedRenameResult(SkinManagedFolderRenameOperationStatus.Shutdown);

                if (activeManagedFolderRenameTask is { IsCompleted: false }
                    || activeManagedFolderStagedImportTask is { IsCompleted: false }
                    || activeManagedFolderDeleteTask is { IsCompleted: false })
                    return completedRenameResult(SkinManagedFolderRenameOperationStatus.Busy);

                if (cancellationToken.IsCancellationRequested)
                    return completedRenameResult(SkinManagedFolderRenameOperationStatus.Cancelled);

                var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<SkinManagedFolderRenameOperationResult> operationTask = Task.Run(
                    () => executeManagedFolderRename(recordId, targetChildName, operationCancellation.Token),
                    CancellationToken.None);

                activeManagedFolderRenameCancellation = operationCancellation;
                activeManagedFolderRenameTask = operationTask;

                _ = operationTask.ContinueWith(
                    _ => completeManagedFolderRenameTask(operationTask, operationCancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        /// <summary>
        /// Starts one internal staged import from the fixed operation-derived provisional slot.
        /// </summary>
        internal Task<SkinManagedFolderStagedImportOperationResult>
            ImportManagedFolderAsync(
                Guid operationId,
                string targetChildName,
                CancellationToken cancellationToken = default)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown)
                {
                    return completedStagedImportResult(
                        SkinManagedFolderStagedImportOperationStatus.Shutdown);
                }

                if (activeManagedFolderRenameTask is { IsCompleted: false }
                    || activeManagedFolderStagedImportTask is { IsCompleted: false }
                    || activeManagedFolderDeleteTask is { IsCompleted: false })
                {
                    return completedStagedImportResult(
                        SkinManagedFolderStagedImportOperationStatus.Busy);
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    return completedStagedImportResult(
                        SkinManagedFolderStagedImportOperationStatus.Cancelled);
                }

                var operationCancellation =
                    CancellationTokenSource.CreateLinkedTokenSource(
                        cancellationToken);
                Task<SkinManagedFolderStagedImportOperationResult> operationTask =
                    Task.Run(
                        () => executeManagedFolderStagedImport(
                            operationId,
                            targetChildName,
                            operationCancellation.Token),
                        CancellationToken.None);

                activeManagedFolderStagedImportCancellation =
                    operationCancellation;
                activeManagedFolderStagedImportTask = operationTask;

                _ = operationTask.ContinueWith(
                    _ => completeManagedFolderStagedImportTask(
                        operationTask,
                        operationCancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        /// <summary>
        /// Starts one exact managed-folder delete. Admission is update-thread-only; all filesystem and Realm work is
        /// owned by the manager worker and remains joined by <see cref="ShutdownManagedFolderMutations"/>.
        /// </summary>
        internal Task<SkinManagedFolderDeleteOperationResult> DeleteManagedFolderAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            if (!ThreadSafety.IsUpdateThread)
                return completedDeleteResult(SkinManagedFolderDeleteOperationStatus.WrongThread);

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown)
                    return completedDeleteResult(SkinManagedFolderDeleteOperationStatus.Shutdown);

                if (activeManagedFolderRenameTask is { IsCompleted: false }
                    || activeManagedFolderStagedImportTask is { IsCompleted: false }
                    || activeManagedFolderDeleteTask is { IsCompleted: false })
                {
                    return completedDeleteResult(SkinManagedFolderDeleteOperationStatus.Busy);
                }

                if (cancellationToken.IsCancellationRequested)
                    return completedDeleteResult(SkinManagedFolderDeleteOperationStatus.Cancelled);

                var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<SkinManagedFolderDeleteOperationResult> operationTask = Task.Run(
                    () => executeManagedFolderDelete(recordId, operationCancellation.Token),
                    CancellationToken.None);

                activeManagedFolderDeleteCancellation = operationCancellation;
                activeManagedFolderDeleteTask = operationTask;

                _ = operationTask.ContinueWith(
                    _ => completeManagedFolderDeleteTask(operationTask, operationCancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        /// <summary>
        /// Cancels and synchronously joins every managed-folder mutation worker before Realm can be released.
        /// </summary>
        internal void ShutdownManagedFolderMutations()
        {
            shutdownManagedFolderSelections();

            CancellationTokenSource renameCancellation;
            CancellationTokenSource importCancellation;
            CancellationTokenSource deleteCancellation;
            Task<SkinManagedFolderRenameOperationResult> renameTask;
            Task<SkinManagedFolderStagedImportOperationResult> importTask;
            Task<SkinManagedFolderDeleteOperationResult> deleteTask;
            PendingManagedFolderDeleteFallback pendingDeleteFallback;

            lock (managedFolderRenameLifecycleGate)
            {
                managedFolderMutationShutdown = true;
                renameCancellation = activeManagedFolderRenameCancellation;
                importCancellation = activeManagedFolderStagedImportCancellation;
                deleteCancellation = activeManagedFolderDeleteCancellation;
                renameTask = activeManagedFolderRenameTask;
                importTask = activeManagedFolderStagedImportTask;
                deleteTask = activeManagedFolderDeleteTask;
                pendingDeleteFallback = pendingManagedFolderDeleteFallback;
            }

            try
            {
                renameCancellation?.Cancel();
            }
            catch
            {
                // Cancellation callback failures must not bypass the join below.
            }

            try
            {
                importCancellation?.Cancel();
            }
            catch
            {
                // Cancellation callback failures must not bypass either join below.
            }

            try
            {
                deleteCancellation?.Cancel();
            }
            catch
            {
                // Cancellation callback failures must not bypass the delete join below.
            }

            cancelManagedFolderDeleteFallback(pendingDeleteFallback);

            try
            {
                renameTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            try
            {
                importTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            try
            {
                deleteTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }
        }

        private void shutdownManagedFolderSelections()
        {
            CancellationTokenSource pendingSelection = null;
            PendingManagedFolderSelectionCompletion[] pendingCompletions;
            bool cancellationRequired = false;

            lock (managedFolderSelectionLifecycleGate)
            {
                if (Volatile.Read(ref managedFolderSelectionShutdown) == 0)
                {
                    Volatile.Write(ref managedFolderSelectionShutdown, 1);
                    Interlocked.Increment(ref selectionGeneration);
                    pendingSelection = Interlocked.Exchange(ref pendingSelectionCancellation, null);
                    cancellationRequired = true;
                }

                pendingCompletions = pendingManagedFolderSelectionCompletions.ToArray();
                pendingManagedFolderSelectionCompletions.Clear();
            }

            if (cancellationRequired)
            {
                try
                {
                    pendingSelection?.Cancel();
                }
                catch
                {
                    // Cancellation callback failures must not bypass the joins below.
                }

                try
                {
                    managedFolderSelectionRetryCancellation.Cancel();
                }
                catch
                {
                    // Cancellation callback failures must not bypass the joins below.
                }
            }

            foreach (PendingManagedFolderSelectionCompletion pendingCompletion in pendingCompletions)
                discardManagedFolderSelectionCompletion(pendingCompletion);

            Task[] workerTasks;

            lock (managedFolderSelectionLifecycleGate)
                workerTasks = managedFolderSelectionWorkerTasks.ToArray();

            foreach (Task workerTask in workerTasks)
            {
                try
                {
                    workerTask.GetAwaiter().GetResult();
                }
                catch
                {
                    // Retry failures are reduced to stable selection state and must not escape shutdown.
                }
            }
        }

        /// <summary>
        /// Compatibility entry point retained for existing rename lifecycle callers.
        /// </summary>
        internal void ShutdownManagedFolderRename()
            => ShutdownManagedFolderMutations();

        private SkinManagedFolderRenameOperationResult executeManagedFolderRename(
            Guid recordId,
            string targetChildName,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderRenameOperationResult result;

            try
            {
                result = managedFolderRenameOperation.Execute(
                    Guid.NewGuid(),
                    recordId,
                    targetChildName,
                    cancellationToken);
            }
            catch
            {
                result = SkinManagedFolderRenameOperationResult.Failure(
                    SkinManagedFolderRenameOperationStatus.PreparedJournalOutcomeUncertain);
            }

            if (result.IsSuccess)
            {
                Interlocked.Increment(ref selectionGeneration);
                cancelPendingSelection();
            }

            Volatile.Write(ref lastManagedFolderRenameResult, result);
            return result;
        }

        private SkinManagedFolderStagedImportOperationResult
            executeManagedFolderStagedImport(
                Guid operationId,
                string targetChildName,
                CancellationToken cancellationToken)
        {
            SkinManagedFolderStagedImportOperationResult result;

            try
            {
                result = managedFolderStagedImportOperation.Execute(
                    operationId,
                    targetChildName,
                    cancellationToken);
            }
            catch
            {
                result = SkinManagedFolderStagedImportOperationResult.Failure(
                    SkinManagedFolderStagedImportOperationStatus
                        .PreparedJournalOutcomeUncertain);
            }

            // Import deliberately does not advance selectionGeneration, cancel an unrelated pending selection,
            // select the new record, or replace the active immutable capsule.
            Volatile.Write(ref lastManagedFolderStagedImportResult, result);
            return result;
        }

        private SkinManagedFolderDeleteOperationResult executeManagedFolderDelete(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderDeleteOperationResult result;

            try
            {
                result = managedFolderDeleteOperation.Execute(
                    Guid.NewGuid(),
                    recordId,
                    cancellationToken);
            }
            catch
            {
                result = SkinManagedFolderDeleteOperationResult.Failure(
                    SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
            }

            Volatile.Write(ref lastManagedFolderDeleteResult, result);
            return result;
        }

        private Task<SkinManagedFolderRenameOperationResult> completedRenameResult(
            SkinManagedFolderRenameOperationStatus status)
        {
            SkinManagedFolderRenameOperationResult result =
                SkinManagedFolderRenameOperationResult.Failure(status);
            Volatile.Write(ref lastManagedFolderRenameResult, result);
            return Task.FromResult(result);
        }

        private Task<SkinManagedFolderStagedImportOperationResult>
            completedStagedImportResult(
                SkinManagedFolderStagedImportOperationStatus status)
        {
            SkinManagedFolderStagedImportOperationResult result =
                SkinManagedFolderStagedImportOperationResult.Failure(status);
            Volatile.Write(ref lastManagedFolderStagedImportResult, result);
            return Task.FromResult(result);
        }

        private Task<SkinManagedFolderDeleteOperationResult> completedDeleteResult(
            SkinManagedFolderDeleteOperationStatus status)
        {
            SkinManagedFolderDeleteOperationResult result =
                SkinManagedFolderDeleteOperationResult.Failure(status);
            Volatile.Write(ref lastManagedFolderDeleteResult, result);
            return Task.FromResult(result);
        }

        private void completeManagedFolderRenameTask(
            Task<SkinManagedFolderRenameOperationResult> operationTask,
            CancellationTokenSource operationCancellation)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (ReferenceEquals(activeManagedFolderRenameTask, operationTask))
                {
                    activeManagedFolderRenameTask = null;
                    activeManagedFolderRenameCancellation = null;
                }
            }

            operationCancellation.Dispose();
        }

        private void completeManagedFolderStagedImportTask(
            Task<SkinManagedFolderStagedImportOperationResult> operationTask,
            CancellationTokenSource operationCancellation)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (ReferenceEquals(
                        activeManagedFolderStagedImportTask,
                        operationTask))
                {
                    activeManagedFolderStagedImportTask = null;
                    activeManagedFolderStagedImportCancellation = null;
                }
            }

            operationCancellation.Dispose();
        }

        private void completeManagedFolderDeleteTask(
            Task<SkinManagedFolderDeleteOperationResult> operationTask,
            CancellationTokenSource operationCancellation)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (ReferenceEquals(activeManagedFolderDeleteTask, operationTask))
                {
                    activeManagedFolderDeleteTask = null;
                    activeManagedFolderDeleteCancellation = null;
                }
            }

            operationCancellation.Dispose();
        }

        private SkinManagedFolderProtectedFallbackCommitResult commitManagedFolderDeleteFallback(
            SkinManagedFolderMutationAuthoritySession authority,
            SkinManagedFolderDurableMutationReceipt durableReceipt,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pending = new PendingManagedFolderDeleteFallback(
                authority,
                durableReceipt,
                cancellationToken);

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown)
                    throw new OperationCanceledException(cancellationToken);

                pendingManagedFolderDeleteFallback = pending;
            }

            using CancellationTokenRegistration cancellationRegistration = cancellationToken.Register(
                () => cancelManagedFolderDeleteFallback(pending));

            try
            {
                ManagedFolderDeleteFallbackSchedule(
                    () => completeManagedFolderDeleteFallback(pending));
            }
            catch
            {
                if (claimManagedFolderDeleteFallback(pending))
                {
                    pending.Completion.TrySetResult(
                        SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted);
                }
            }

            if (cancellationToken.IsCancellationRequested)
                cancelManagedFolderDeleteFallback(pending);

            return pending.Completion.Task.GetAwaiter().GetResult();
        }

        private void completeManagedFolderDeleteFallback(PendingManagedFolderDeleteFallback pending)
        {
            if (!claimManagedFolderDeleteFallback(pending))
                return;

            SkinManagedFolderProtectedFallbackCommitResult result;

            Interlocked.Increment(ref managedFolderDeleteFallbackSourceChangeDeferral);

            try
            {
                result = CommitProtectedFallbackPairForDelete(
                    pending.Authority,
                    pending.DurableReceipt,
                    CancellationToken.None);

                if (result == SkinManagedFolderProtectedFallbackCommitResult.NotRequired)
                {
                    Interlocked.Increment(ref selectionGeneration);
                    cancelPendingSelection();
                }
            }
            catch
            {
                result = SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted;
            }
            finally
            {
                Interlocked.Decrement(ref managedFolderDeleteFallbackSourceChangeDeferral);
            }

            pending.Completion.TrySetResult(result);
            publishDeferredManagedFolderDeleteFallbackSourceChange();
        }

        private void publishDeferredManagedFolderDeleteFallbackSourceChange()
        {
            if (Volatile.Read(ref managedFolderDeleteFallbackSourceChangeDeferral) != 0
                || Interlocked.Exchange(
                    ref managedFolderDeleteFallbackSourceChangePending,
                    0) == 0)
            {
                return;
            }

            SourceChanged?.Invoke();
        }

        private void cancelManagedFolderDeleteFallback(PendingManagedFolderDeleteFallback pending)
        {
            if (pending == null || !claimManagedFolderDeleteFallback(pending))
                return;

            pending.Completion.TrySetCanceled(pending.CancellationToken);
        }

        private bool claimManagedFolderDeleteFallback(PendingManagedFolderDeleteFallback pending)
        {
            if (pending == null || !pending.TryClaim())
                return false;

            lock (managedFolderRenameLifecycleGate)
            {
                if (ReferenceEquals(pendingManagedFolderDeleteFallback, pending))
                    pendingManagedFolderDeleteFallback = null;
            }

            return true;
        }

        /// <summary>
        /// Confirms the protected fallback pair while a future delete authority still owns the shared coordinator.
        /// </summary>
        /// <remarks>
        /// This method performs no Realm or filesystem deletion. Before canonical package takeover the only accepted
        /// fallback is the exact programmatic <see cref="OmsSkin"/> type/record pair. The delete slice must replace this policy
        /// only after <c>oms-simple.osk</c> becomes the validated protected authority.
        /// </remarks>
        internal SkinManagedFolderProtectedFallbackCommitResult CommitProtectedFallbackPairForDelete(
            SkinManagedFolderMutationAuthoritySession authority,
            SkinManagedFolderDurableMutationReceipt durableReceipt,
            CancellationToken cancellationToken = default)
        {
            if (!ThreadSafety.IsUpdateThread)
                return SkinManagedFolderProtectedFallbackCommitResult.WrongThread;

            if (authority == null
                || authority.Kind != SkinManagedFolderMutationKind.Delete
                || authority.ExistingRecord == null
                || durableReceipt == null
                || !authority.HasCoordinatorAuthority(ManagedFolderOperationCoordinator))
            {
                return SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected;
            }

            SkinManagedFolderProtectedFallbackCommitResult result = authority.RunWithDurableReceipt(
                durableReceipt,
                SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected,
                () => commitProtectedFallbackPairForDeleteHeld(authority, cancellationToken),
                cancellationToken);

            if (result is not (SkinManagedFolderProtectedFallbackCommitResult.Committed
                or SkinManagedFolderProtectedFallbackCommitResult.NotRequired))
            {
                authority.TryAbortPreparedJournal(durableReceipt, cancellationToken);
            }

            return result;
        }

        private SkinManagedFolderProtectedFallbackCommitResult commitProtectedFallbackPairForDeleteHeld(
            SkinManagedFolderMutationAuthoritySession authority,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ManagedFolderOperationCoordinator.IsPathFrozen(authority.ExistingRecord.ManagedRelativePath))
                return SkinManagedFolderProtectedFallbackCommitResult.RecoveryPending;

            Guid currentInfoId = CurrentSkinInfo.Value.ID;
            Guid currentSkinId = CurrentSkin.Value.SkinInfo.ID;

            if (currentInfoId != currentSkinId)
                return SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted;

            if (currentInfoId != authority.ExistingRecord.RecordId)
            {
                return SkinManagedFolderProtectedFallbackCommitResult.NotRequired;
            }

            if (CurrentSkinInfo.Disabled)
                return SkinManagedFolderProtectedFallbackCommitResult.SelectionDisabled;

            bool fallbackIsValid = Realm.Run(r =>
            {
                r.Refresh();
                return SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(
                    r.Find<SkinInfo>(SkinInfo.OMS_SKIN));
            });

            if (!fallbackIsValid
                || DefaultOmsSkin.GetType() != typeof(OmsSkin)
                || !DefaultOmsSkin.SkinInfo.PerformRead(
                    SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord))
            {
                return SkinManagedFolderProtectedFallbackCommitResult.FallbackInvalid;
            }

            try
            {
                Interlocked.Increment(ref selectionGeneration);
                cancelPendingSelection();
                ((SkinSelectionBindable)CurrentSkinInfo).CommitPrepared(DefaultOmsSkin.SkinInfo);
            }
            catch
            {
                return SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted;
            }

            bool pairCommitted = CurrentSkinInfo.Value.ID == SkinInfo.OMS_SKIN
                                 && CurrentSkin.Value.GetType() == typeof(OmsSkin)
                                 && CurrentSkin.Value.SkinInfo.ID == SkinInfo.OMS_SKIN
                                 && CurrentSkinInfo.Value.PerformRead(
                                     SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord)
                                 && CurrentSkin.Value.SkinInfo.PerformRead(
                                     SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord);

            if (!pairCommitted)
                return SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted;

            return authority.Validate(cancellationToken)
                ? SkinManagedFolderProtectedFallbackCommitResult.Committed
                : SkinManagedFolderProtectedFallbackCommitResult.AuthorityRejected;
        }

        /// <summary>
        /// Returns the dropdown ordering for use mainly by the skin selection UI.
        /// Inserts the OMS built-in skin first, then user skins.
        /// Returns a list of <see cref="Live{SkinInfo}"/> items.
        /// </summary>
        public IList<Live<SkinInfo>> GetAllUsableSkins()
        {
            var skins = new List<Live<SkinInfo>>();

            Realm.Run(realm =>
            {
                skins.Add(realm.Find<SkinInfo>(SkinInfo.OMS_SKIN).ToLive(Realm));

                var userSkins = realm.All<SkinInfo>()
                                     .Where(s => !s.DeletePending && !s.Protected && !s.IsExternalFilesystemStorage)
                                     .AsEnumerable()
                                     .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                                     .Select(s => s.ToLive(Realm));

                foreach (var s in userSkins)
                    skins.Add(s);
            });

            return skins;
        }

        public Task<IList<Live<SkinInfo>>> GetAllUsableSkinsAsync(CancellationToken cancellationToken = default)
            => Task.Run(() => GetAllUsableSkins(), cancellationToken);

        public void SelectRandomSkin()
        {
            Realm.Run(r =>
            {
                // can be the case when the current skin is externally mounted for editing
                if (CurrentSkinInfo.Disabled)
                    return;

                // Required local for iOS. Will cause runtime crash if inlined.
                Guid currentSkinId = CurrentSkinInfo.Value.ID;

                // choose from only user skins, removing the current selection to ensure a new one is chosen.
                var randomChoices = r.All<SkinInfo>()
                                     .Where(s => !s.DeletePending && !s.Protected && !s.IsExternalFilesystemStorage && s.ID != currentSkinId)
                                     .ToArray();

                if (randomChoices.Length == 0)
                {
                    CurrentSkinInfo.Value = DefaultOmsSkin.SkinInfo;
                    return;
                }

                var chosen = randomChoices.ElementAt(RNG.Next(0, randomChoices.Length));

                CurrentSkinInfo.Value = chosen.ToLive(Realm);
            });
        }

        private void cycleSkins(int direction)
        {
            Debug.Assert(direction != 0);

            // don't change selection if current skin is externally disabled/mounted for editing.
            if (CurrentSkinInfo.Disabled)
                return;

            var skins = GetAllUsableSkins();

            int i = skins.IndexOf(CurrentSkinInfo.Value);

            // If the current skin isn't selectable anymore, start from the top.
            if (i < 0 && direction < 0)
                i = 0;

            do
            {
                i = (i + direction + skins.Count) % skins.Count;
            } while (skins[i].ID == SkinInfo.RANDOM_SKIN);

            CurrentSkinInfo.Value = skins[i];
        }

        /// <summary>
        /// Cycle one skin backward.
        /// </summary>
        public void SelectPreviousSkin() => cycleSkins(-1);

        /// <summary>
        /// Cycle one skin forward.
        /// </summary>
        public void SelectNextSkin() => cycleSkins(1);

        /// <summary>
        /// Retrieve a <see cref="Skin"/> instance for the provided <see cref="SkinInfo"/>
        /// </summary>
        /// <param name="skinInfo">The skin to lookup.</param>
        /// <returns>A <see cref="Skin"/> instance correlating to the provided <see cref="SkinInfo"/>.</returns>
        public Skin GetSkin(SkinInfo skinInfo)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);

            if (resolution.Authority == SkinFilesystemStorageAuthority.RealmPackage)
                return skinInfo.CreateInstance(this);

            if (resolution.Authority != SkinFilesystemStorageAuthority.ManagedFolder
                || resolution.ManagedCaptureRequest == null
                || !skinInfo.IsManaged
                || !string.Equals(skinInfo.FilesystemStorageAuthorityOwner, SkinManagedFolderScanner.AUTHORITY_OWNER, StringComparison.Ordinal)
                || ManagedFolderOperationCoordinator.IsPathFrozen(skinInfo.FilesystemStoragePath)
                || !SkinManagedFolderFactory.IsInstantiationInfoAllowed(skinInfo.InstantiationInfo))
            {
                throw new InvalidOperationException("The filesystem-backed skin cannot be prepared safely.");
            }

            SkinInfo snapshot = createFilesystemSkinSnapshot(skinInfo);
            SkinManagedPackageCaptureResult capture = ManagedFolderCapture(resolution.ManagedCaptureRequest, CancellationToken.None);

            if (!capture.IsSuccess)
                throw new InvalidOperationException("The managed skin folder could not be captured safely.");

            SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(snapshot, this, capture.Capsule!);

            return factory.Skin ?? throw new InvalidOperationException("The captured managed skin folder could not be instantiated safely.");
        }

        private bool requestSelection(Live<SkinInfo> target)
        {
            if (ThreadSafety.IsUpdateThread)
            {
                if (ManagedFolderOperationCoordinator.TryEnterForSelection(
                        out SkinManagedFolderOperationCoordinator.Lease preflightLease,
                        out SkinManagedFolderOperationCoordinator.SelectionContention preflightContention))
                {
                    preflightLease.Dispose();
                }
                else if (preflightContention == null)
                {
                    rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
                    return false;
                }
            }

            SelectionRequest request;

            try
            {
                request = target.PerformRead(info => createSelectionRequest(info));
            }
            catch
            {
                if (tryEnterSelectionBoundary(out SkinManagedFolderOperationCoordinator.Lease operationLease))
                {
                    using (operationLease)
                    {
                        Interlocked.Increment(ref selectionGeneration);
                        cancelPendingSelection();
                        rejectSelection(SkinSelectionRejectionReason.FilesystemDeclarationRejected);
                    }
                }
                else
                    rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);

                return false;
            }

            if (request.Resolution.Authority == SkinFilesystemStorageAuthority.ManagedFolder
                && !ThreadSafety.IsUpdateThread)
            {
                throw new InvalidOperationException("Managed folder skin selection requests must run on the update thread.");
            }

            SelectionRequestBeforeCommitLock(target);

            if (!tryEnterSelectionBoundary(out SkinManagedFolderOperationCoordinator.Lease initialLease))
            {
                rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
                return false;
            }

            using (initialLease)
            {
                long generation = Interlocked.Increment(ref selectionGeneration);
                cancelPendingSelection();

                switch (request.Resolution.Authority)
                {
                    case SkinFilesystemStorageAuthority.RealmPackage:
                        LastSelectionRejectionReason = SkinSelectionRejectionReason.None;
                        ((SkinSelectionBindable)CurrentSkinInfo).CommitPrepared(target);
                        return false;

                    case SkinFilesystemStorageAuthority.ExternalFolder:
                        rejectSelection(SkinSelectionRejectionReason.ExternalFolderUnsupported);
                        return false;

                    case SkinFilesystemStorageAuthority.Invalid:
                        rejectSelection(SkinSelectionRejectionReason.FilesystemDeclarationRejected);
                        return false;

                    case SkinFilesystemStorageAuthority.ManagedFolder:
                        break;

                    default:
                        rejectSelection(SkinSelectionRejectionReason.FilesystemDeclarationRejected);
                        return false;
                }

                if (request.Resolution.ManagedCaptureRequest == null || request.Snapshot == null)
                    rejectSelection(SkinSelectionRejectionReason.FilesystemDeclarationRejected);
                else
                    beginManagedFolderSelectionPreparation(generation, target, request);

                return false;
            }
        }

        private void beginManagedFolderSelectionPreparation(
            long generation,
            Live<SkinInfo> target,
            SelectionRequest request)
        {
            if (request.Resolution.ManagedCaptureRequest == null || request.Snapshot == null)
            {
                rejectSelection(SkinSelectionRejectionReason.FilesystemDeclarationRejected);
                return;
            }

            if (!request.IsRealmManaged || !request.HasExactScannerOwner)
            {
                rejectSelection(SkinSelectionRejectionReason.UnmanagedFilesystemRecord);
                return;
            }

            if (ManagedFolderOperationCoordinator.IsPathFrozen(request.Snapshot.FilesystemStoragePath))
            {
                rejectSelection(SkinSelectionRejectionReason.MutationRecoveryPending);
                return;
            }

            if (!SkinManagedFolderFactory.IsInstantiationInfoAllowed(request.Snapshot.InstantiationInfo))
            {
                rejectSelection(SkinSelectionRejectionReason.InstantiationInfoNotAllowed);
                return;
            }

            SkinManagedPackageCaptureRequest captureRequest = request.Resolution.ManagedCaptureRequest;

            lock (managedFolderSelectionLifecycleGate)
            {
                if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
                {
                    rejectSelection(SkinSelectionRejectionReason.PreparationCancelled);
                    return;
                }

                var cancellation = new CancellationTokenSource();
                pendingSelectionCancellation = cancellation;
                SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation =
                    ManagedFolderOperationCoordinator.CaptureSelectionPreparationObservation();
                Task<SkinManagedPackageCaptureResult> captureTask = Task.Run(
                    () => ManagedFolderCapture(captureRequest, cancellation.Token),
                    cancellation.Token);
                Task completionSchedulingTask = captureTask.ContinueWith(
                    task => scheduleManagedFolderSelectionCompletion(
                        generation,
                        target,
                        request,
                        preparationObservation,
                        cancellation,
                        task),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                trackManagedFolderSelectionWorkerHeld(completionSchedulingTask);
            }
        }

        private void trackManagedFolderSelectionWorkerHeld(Task workerTask)
        {
            managedFolderSelectionWorkerTasks.Add(workerTask);

            _ = workerTask.ContinueWith(
                completed =>
                {
                    lock (managedFolderSelectionLifecycleGate)
                        managedFolderSelectionWorkerTasks.Remove(completed);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void scheduleManagedFolderSelectionCompletion(
            long generation,
            Live<SkinInfo> target,
            SelectionRequest request,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
            CancellationTokenSource cancellation,
            Task<SkinManagedPackageCaptureResult> captureTask)
        {
            var pendingCompletion = new PendingManagedFolderSelectionCompletion(
                generation,
                target,
                request,
                preparationObservation,
                cancellation,
                captureTask);

            lock (managedFolderSelectionLifecycleGate)
            {
                if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
                {
                    discardManagedFolderSelectionCompletion(pendingCompletion);
                    return;
                }

                pendingManagedFolderSelectionCompletions.Add(pendingCompletion);
            }

            try
            {
                ManagedFolderCompletionSchedule(() => completeManagedFolderSelection(pendingCompletion));
            }
            catch
            {
                bool claimed;

                lock (managedFolderSelectionLifecycleGate)
                    claimed = pendingManagedFolderSelectionCompletions.Remove(pendingCompletion);

                if (claimed)
                {
                    discardManagedFolderSelectionCompletion(pendingCompletion);
                    tryRejectSelectionWithoutBlocking(generation, SkinSelectionRejectionReason.PreparationFailed);
                }
            }
        }

        private void completeManagedFolderSelection(PendingManagedFolderSelectionCompletion pendingCompletion)
        {
            lock (managedFolderSelectionLifecycleGate)
            {
                if (!pendingManagedFolderSelectionCompletions.Remove(pendingCompletion))
                    return;

                completeManagedFolderSelectionHeld(
                    pendingCompletion.Generation,
                    pendingCompletion.Target,
                    pendingCompletion.Request,
                    pendingCompletion.PreparationObservation,
                    pendingCompletion.Cancellation,
                    pendingCompletion.CaptureTask);
            }
        }

        private void discardManagedFolderSelectionCompletion(PendingManagedFolderSelectionCompletion pendingCompletion)
        {
            Interlocked.CompareExchange(
                ref pendingSelectionCancellation,
                null,
                pendingCompletion.Cancellation);
            pendingCompletion.Cancellation.Dispose();

            if (pendingCompletion.CaptureTask.Status == TaskStatus.RanToCompletion)
                pendingCompletion.CaptureTask.GetAwaiter().GetResult().Capsule?.Dispose();
            else if (pendingCompletion.CaptureTask.IsFaulted)
                _ = pendingCompletion.CaptureTask.Exception;
        }

        private void completeManagedFolderSelectionHeld(
            long generation,
            Live<SkinInfo> target,
            SelectionRequest request,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
            CancellationTokenSource cancellation,
            Task<SkinManagedPackageCaptureResult> captureTask)
        {
            Interlocked.CompareExchange(ref pendingSelectionCancellation, null, cancellation);

            cancellation.Dispose();

            if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
            {
                if (captureTask.Status == TaskStatus.RanToCompletion)
                    captureTask.GetAwaiter().GetResult().Capsule?.Dispose();
                else if (captureTask.IsFaulted)
                    _ = captureTask.Exception;

                return;
            }

            if (captureTask.Status != TaskStatus.RanToCompletion)
            {
                if (captureTask.IsFaulted)
                    _ = captureTask.Exception;

                rejectSelection(
                    generation,
                    captureTask.IsCanceled
                        ? SkinSelectionRejectionReason.PreparationCancelled
                        : SkinSelectionRejectionReason.PreparationFailed);

                return;
            }

            SkinManagedPackageCaptureResult capture = captureTask.GetAwaiter().GetResult();

            if (generation != Interlocked.Read(ref selectionGeneration))
            {
                capture.Capsule?.Dispose();
                return;
            }

            if (!capture.IsSuccess)
            {
                rejectSelection(generation, SkinSelectionRejectionReason.CaptureRejected);
                return;
            }

            bool candidateStillMatches;

            try
            {
                candidateStillMatches = target.PerformRead(info => request.Matches(info, storage));
            }
            catch
            {
                candidateStillMatches = false;
            }

            if (!candidateStillMatches)
            {
                capture.Capsule!.Dispose();

                if (tryScheduleManagedFolderSelectionRetryAfterCrossedStartup(
                        generation,
                        target.ID,
                        preparationObservation))
                {
                    return;
                }

                rejectSelection(generation, SkinSelectionRejectionReason.CapturedCandidateChanged);
                return;
            }

            SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(request.Snapshot!, this, capture.Capsule!);

            if (!factory.IsSuccess)
            {
                rejectSelection(generation, SkinSelectionRejectionReason.FactoryRejected);
                return;
            }

            if (generation != Interlocked.Read(ref selectionGeneration))
            {
                factory.Skin!.Dispose();
                return;
            }

            if (!ManagedFolderOperationCoordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease finalLease,
                    out SkinManagedFolderOperationCoordinator.SelectionContention selectionContention))
            {
                ManagedFolderSelectionFinalBoundaryContended();

                if (selectionContention != null)
                {
                    if (!ManagedFolderOperationCoordinator.IsMutationReservationEpochCurrent(preparationObservation))
                    {
                        factory.Skin!.Dispose();
                        rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
                        return;
                    }

                    Guid expectedCurrentInfoId = CurrentSkinInfo.Value.ID;
                    Skin expectedCurrentSkin = CurrentSkin.Value;
                    factory.Skin!.Dispose();
                    scheduleManagedFolderSelectionRetryAfterContention(
                        generation,
                        target.ID,
                        expectedCurrentInfoId,
                        expectedCurrentSkin,
                        preparationObservation,
                        selectionContention);
                    return;
                }

                factory.Skin!.Dispose();

                if (generation == Interlocked.Read(ref selectionGeneration))
                    rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);

                return;
            }

            using (finalLease)
            {
                if (generation != Interlocked.Read(ref selectionGeneration))
                {
                    factory.Skin!.Dispose();
                    return;
                }

                if (CurrentSkinInfo.Disabled)
                {
                    factory.Skin!.Dispose();
                    rejectSelection(SkinSelectionRejectionReason.PreparationCancelled);
                    return;
                }

                Live<SkinInfo> authoritativeTarget = null;

                try
                {
                    authoritativeTarget = Realm.Run(r =>
                    {
                        r.Refresh();
                        SkinInfo current = r.Find<SkinInfo>(target.ID);
                        return current != null && request.Matches(current, storage)
                            ? current.ToLive(Realm)
                            : null;
                    });
                    candidateStillMatches = authoritativeTarget != null;
                }
                catch
                {
                    candidateStillMatches = false;
                }

                if (!candidateStillMatches)
                {
                    factory.Skin!.Dispose();

                    if (tryScheduleManagedFolderSelectionRetryAfterCrossedStartup(
                            generation,
                            target.ID,
                            preparationObservation))
                    {
                        return;
                    }

                    rejectSelection(SkinSelectionRejectionReason.CapturedCandidateChanged);
                    return;
                }

                if (ManagedFolderOperationCoordinator.IsPathFrozen(request.Snapshot!.FilesystemStoragePath))
                {
                    factory.Skin!.Dispose();
                    rejectSelection(SkinSelectionRejectionReason.MutationRecoveryPending);
                    return;
                }

                try
                {
                    ManagedFolderBeforeCommit();
                }
                catch
                {
                    factory.Skin!.Dispose();
                    rejectSelection(SkinSelectionRejectionReason.PreparationFailed);
                    return;
                }

                if (generation != Interlocked.Read(ref selectionGeneration))
                {
                    factory.Skin!.Dispose();
                    return;
                }

                var prepared = new PreparedManagedFolderSelection(authoritativeTarget, factory.Skin!);
                preparedManagedFolderSelection = prepared;

                try
                {
                    ((SkinSelectionBindable)CurrentSkinInfo).CommitPrepared(authoritativeTarget);
                }
                finally
                {
                    if (ReferenceEquals(preparedManagedFolderSelection, prepared))
                    {
                        preparedManagedFolderSelection = null;
                        prepared.Skin.Dispose();
                    }
                }

                if (generation == Interlocked.Read(ref selectionGeneration)
                    && CurrentSkinInfo.Value.ID == authoritativeTarget.ID
                    && ReferenceEquals(CurrentSkin.Value, factory.Skin))
                {
                    LastSelectionRejectionReason = SkinSelectionRejectionReason.None;
                }
            }
        }

        private bool tryScheduleManagedFolderSelectionRetryAfterCrossedStartup(
            long generation,
            Guid targetId,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation)
        {
            SkinManagedFolderOperationCoordinator.SelectionContention contention =
                ManagedFolderOperationCoordinator.TryGetRetryableContentionSince(preparationObservation);

            if (contention == null)
                return false;

            scheduleManagedFolderSelectionRetryAfterContention(
                generation,
                targetId,
                CurrentSkinInfo.Value.ID,
                CurrentSkin.Value,
                preparationObservation,
                contention);
            return true;
        }

        private void scheduleManagedFolderSelectionRetryAfterContention(
            long generation,
            Guid targetId,
            Guid expectedCurrentInfoId,
            Skin expectedCurrentSkin,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
            SkinManagedFolderOperationCoordinator.SelectionContention contention)
        {
            if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
                return;

            try
            {
                if (contention.Kind == SkinManagedFolderOperationCoordinator.SelectionContentionKind.StartupSequence)
                    ManagedFolderSelectionWaitingForStartup();
                else
                    ManagedFolderSelectionWaitingForStagedImport();
            }
            catch
            {
                rejectSelection(generation, SkinSelectionRejectionReason.PreparationFailed);
                return;
            }

            lock (managedFolderSelectionLifecycleGate)
            {
                if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
                    return;

                Task workerTask = waitForManagedFolderContentionAndScheduleRetry(
                    generation,
                    targetId,
                    expectedCurrentInfoId,
                    expectedCurrentSkin,
                    preparationObservation,
                    contention.Completion,
                    managedFolderSelectionRetryCancellation.Token);
                trackManagedFolderSelectionWorkerHeld(workerTask);
            }
        }

        private async Task waitForManagedFolderContentionAndScheduleRetry(
            long generation,
            Guid targetId,
            Guid expectedCurrentInfoId,
            Skin expectedCurrentSkin,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
            Task contentionCompletion,
            CancellationToken cancellationToken)
        {
            try
            {
                await contentionCompletion.WaitAsync(cancellationToken).ConfigureAwait(false);
                await Task.Yield();
                cancellationToken.ThrowIfCancellationRequested();

                ManagedFolderCompletionSchedule(
                    () => retryManagedFolderSelectionAfterContention(
                        generation,
                        targetId,
                        expectedCurrentInfoId,
                        expectedCurrentSkin,
                        preparationObservation));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch
            {
                if (Volatile.Read(ref managedFolderSelectionShutdown) == 0)
                    tryRejectSelectionWithoutBlocking(generation, SkinSelectionRejectionReason.PreparationFailed);
            }
        }

        private void retryManagedFolderSelectionAfterContention(
            long generation,
            Guid targetId,
            Guid expectedCurrentInfoId,
            Skin expectedCurrentSkin,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation)
        {
            lock (managedFolderSelectionLifecycleGate)
            {
                retryManagedFolderSelectionAfterContentionHeld(
                    generation,
                    targetId,
                    expectedCurrentInfoId,
                    expectedCurrentSkin,
                    preparationObservation);
            }
        }

        private void retryManagedFolderSelectionAfterContentionHeld(
            long generation,
            Guid targetId,
            Guid expectedCurrentInfoId,
            Skin expectedCurrentSkin,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation)
        {
            if (Volatile.Read(ref managedFolderSelectionShutdown) != 0
                || generation != Interlocked.Read(ref selectionGeneration)
                || CurrentSkinInfo.Disabled
                || CurrentSkinInfo.Value.ID != expectedCurrentInfoId
                || !ReferenceEquals(CurrentSkin.Value, expectedCurrentSkin))
            {
                return;
            }

            if (!ManagedFolderOperationCoordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease retryLease,
                    out SkinManagedFolderOperationCoordinator.SelectionContention nextContention))
            {
                if (nextContention != null)
                {
                    if (!ManagedFolderOperationCoordinator.IsMutationReservationEpochCurrent(preparationObservation))
                    {
                        rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
                        return;
                    }

                    scheduleManagedFolderSelectionRetryAfterContention(
                        generation,
                        targetId,
                        expectedCurrentInfoId,
                        expectedCurrentSkin,
                        preparationObservation,
                        nextContention);
                }
                else if (generation == Interlocked.Read(ref selectionGeneration))
                    rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);

                return;
            }

            using (retryLease)
            {
                if (!ManagedFolderOperationCoordinator.IsMutationReservationEpochCurrent(preparationObservation))
                {
                    rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
                    return;
                }

                if (Volatile.Read(ref managedFolderSelectionShutdown) != 0
                    || generation != Interlocked.Read(ref selectionGeneration)
                    || CurrentSkinInfo.Disabled
                    || CurrentSkinInfo.Value.ID != expectedCurrentInfoId
                    || !ReferenceEquals(CurrentSkin.Value, expectedCurrentSkin))
                {
                    return;
                }

                Live<SkinInfo> authoritativeTarget;
                SelectionRequest retryRequest;

                try
                {
                    authoritativeTarget = Realm.Run(r =>
                    {
                        r.Refresh();
                        return r.Find<SkinInfo>(targetId)?.ToLive(Realm);
                    });

                    if (authoritativeTarget == null)
                    {
                        rejectSelection(SkinSelectionRejectionReason.CapturedCandidateChanged);
                        return;
                    }

                    retryRequest = authoritativeTarget.PerformRead(createSelectionRequest);
                }
                catch
                {
                    rejectSelection(SkinSelectionRejectionReason.PreparationFailed);
                    return;
                }

                if (generation != Interlocked.Read(ref selectionGeneration))
                    return;

                beginManagedFolderSelectionPreparation(generation, authoritativeTarget, retryRequest);
            }
        }

        private SelectionRequest createSelectionRequest(SkinInfo skinInfo)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);
            SkinInfo snapshot = resolution.Authority == SkinFilesystemStorageAuthority.ManagedFolder
                ? createFilesystemSkinSnapshot(skinInfo)
                : null;

            return new SelectionRequest(
                resolution,
                snapshot,
                skinInfo.IsManaged,
                string.Equals(skinInfo.FilesystemStorageAuthorityOwner, SkinManagedFolderScanner.AUTHORITY_OWNER, StringComparison.Ordinal));
        }

        private static SkinInfo createFilesystemSkinSnapshot(SkinInfo source)
            => new SkinInfo
            {
                ID = source.ID,
                Name = source.Name,
                Creator = source.Creator,
                InstantiationInfo = source.InstantiationInfo,
                Hash = source.Hash,
                Protected = source.Protected,
                FilesystemStoragePath = source.FilesystemStoragePath,
                IsExternalFilesystemStorage = source.IsExternalFilesystemStorage,
                FilesystemStorageAuthorityOwner = source.FilesystemStorageAuthorityOwner,
                DeletePending = source.DeletePending,
            };

        private void cancelPendingSelection()
        {
            CancellationTokenSource cancellation = Interlocked.Exchange(ref pendingSelectionCancellation, null);

            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private void rejectSelection(SkinSelectionRejectionReason reason)
        {
            LastSelectionRejectionReason = reason;
        }

        private void rejectSelection(long generation, SkinSelectionRejectionReason reason)
        {
            if (!tryEnterSelectionBoundary(out SkinManagedFolderOperationCoordinator.Lease operationLease))
                return;

            using (operationLease)
            {
                if (generation == Interlocked.Read(ref selectionGeneration))
                    rejectSelection(reason);
            }
        }

        private void tryRejectSelectionWithoutBlocking(long generation, SkinSelectionRejectionReason reason)
        {
            if (!ManagedFolderOperationCoordinator.TryEnter(out SkinManagedFolderOperationCoordinator.Lease operationLease))
                return;

            using (operationLease)
            {
                if (generation == Interlocked.Read(ref selectionGeneration))
                    rejectSelection(reason);
            }
        }

        private bool tryEnterSelectionBoundary(out SkinManagedFolderOperationCoordinator.Lease operationLease)
        {
            if (!ThreadSafety.IsUpdateThread)
            {
                operationLease = ManagedFolderOperationCoordinator.Enter();
                return true;
            }

            return ManagedFolderOperationCoordinator.TryEnter(out operationLease);
        }

        /// <summary>
        /// Ensure that the current skin is in a state it can accept user modifications.
        /// This will create a copy of any internal skin and being tracking in the database if not already.
        /// </summary>
        /// <returns>
        /// Whether a new skin was created to allow for mutation.
        /// </returns>
        public bool EnsureMutableSkin()
        {
            return CurrentSkinInfo.Value.PerformRead(s =>
            {
                if (isFilesystemBacked(s))
                    throw new InvalidOperationException("Filesystem-backed skins are read-only until their mutation protocol is available.");

                if (!s.Protected)
                    return false;

                string[] existingSkinNames = Realm.Run(r => r.All<SkinInfo>()
                                                             .Where(skin => !skin.DeletePending)
                                                             .AsEnumerable()
                                                             .Select(skin => skin.Name).ToArray());

                // if the user is attempting to save one of the default skin implementations, create a copy first.
                var skinInfo = new SkinInfo
                {
                    Creator = s.Creator,
                    InstantiationInfo = s.InstantiationInfo,
                    Name = NamingUtils.GetNextBestName(existingSkinNames, $@"{s.Name} (modified)")
                };

                var result = skinImporter.ImportModel(skinInfo, parameters: new ImportParameters
                {
                    ImportImmediately = true // to avoid possible deadlocks when editing skin during gameplay.
                });

                if (result != null)
                {
                    // save once to ensure the required json content is populated.
                    // currently this only happens on save.
                    result.PerformRead(skin => Save(skin.CreateInstance(this)));
                    CurrentSkinInfo.Value = result;
                    return true;
                }

                return false;
            });
        }

        /// <summary>
        /// Save a skin, serialising any changes to skin layouts to relevant JSON structures.
        /// </summary>
        /// <returns>Whether any change actually occurred.</returns>
        public bool Save(Skin skin)
        {
            if (skin.SkinInfo.PerformRead(isFilesystemBacked))
                throw new InvalidOperationException("Filesystem-backed skins cannot be saved through the Realm package editor.");

            if (!skin.SkinInfo.IsManaged)
                throw new InvalidOperationException($"Attempting to save a skin which is not yet tracked. Call {nameof(EnsureMutableSkin)} first.");

            return skinImporter.Save(skin);
        }

        /// <summary>
        /// Perform a lookup query on available <see cref="SkinInfo"/>s.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The first result for the provided query, or null if no results were found.</returns>
        public Live<SkinInfo> Query(Expression<Func<SkinInfo, bool>> query)
        {
            return Realm.Run(r => r.All<SkinInfo>().FirstOrDefault(query)?.ToLive(Realm));
        }

        public event Action SourceChanged;

        public Drawable GetDrawableComponent(ISkinComponentLookup lookup) => lookupWithFallback(s => s.GetDrawableComponent(lookup));

        public Texture GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => lookupWithFallback(s => s.GetTexture(componentName, wrapModeS, wrapModeT));

        public ISample GetSample(ISampleInfo sampleInfo) => lookupWithFallback(s => s.GetSample(sampleInfo));

        public IBindable<TValue> GetConfig<TLookup, TValue>(TLookup lookup) => lookupWithFallback(s => s.GetConfig<TLookup, TValue>(lookup));

        public ISkin FindProvider(Func<ISkin, bool> lookupFunction)
        {
            foreach (var source in AllSources)
            {
                if (lookupFunction(source))
                    return source;
            }

            return null;
        }

        public IEnumerable<ISkin> AllSources
        {
            get
            {
                yield return CurrentSkin.Value;

                // OMS is the only built-in fallback surfaced by the product.
                if (CurrentSkin.Value.SkinInfo.ID != DefaultOmsSkin.SkinInfo.ID)
                    yield return DefaultOmsSkin;
            }
        }

        private T lookupWithFallback<T>(Func<ISkin, T> lookupFunction)
            where T : class
        {
            try
            {
                Skin.LogLookupDebug(this, lookupFunction, Skin.LookupDebugType.Enter);

                foreach (var source in AllSources)
                {
                    if (lookupFunction(source) is T skinSourced)
                        return skinSourced;
                }

                return null;
            }
            finally
            {
                Skin.LogLookupDebug(this, lookupFunction, Skin.LookupDebugType.Exit);
            }
        }

        #region IResourceStorageProvider

        IRenderer IStorageResourceProvider.Renderer => host.Renderer;
        AudioManager IStorageResourceProvider.AudioManager => audio;
        IResourceStore<byte[]> IStorageResourceProvider.Resources => resources;
        IResourceStore<byte[]> IStorageResourceProvider.Files => userFiles;
        RealmAccess IStorageResourceProvider.RealmAccess => Realm;
        IResourceStore<TextureUpload> IStorageResourceProvider.CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => host.CreateTextureLoaderStore(underlyingStore);

        #endregion

        #region Implementation of IModelImporter<SkinInfo>

        public Action<IEnumerable<Live<SkinInfo>>> PresentImport
        {
            set => skinImporter.PresentImport = value;
        }

        public Task Import(params string[] paths) => skinImporter.Import(paths);

        public Task Import(ImportTask[] imports, ImportParameters parameters = default) => skinImporter.Import(imports, parameters);

        public IEnumerable<string> HandledExtensions => skinImporter.HandledExtensions;

        public Task<IEnumerable<Live<SkinInfo>>> Import(ProgressNotification notification, ImportTask[] tasks, ImportParameters parameters = default) =>
            skinImporter.Import(notification, tasks, parameters);

        public Task<Live<SkinInfo>> ImportAsUpdate(ProgressNotification notification, ImportTask task, SkinInfo original)
        {
            if (isFilesystemBacked(original))
                throw new InvalidOperationException("Filesystem-backed skins cannot be replaced through the Realm package importer.");

            return skinImporter.ImportAsUpdate(notification, task, original);
        }

        public Task<ExternalEditOperation<SkinInfo>> BeginExternalEditing(SkinInfo model)
        {
            SkinInfo authoritative = Realm.Run(realm =>
            {
                SkinInfo record = realm.Find<SkinInfo>(model.ID) ?? throw new InvalidOperationException("The skin is not registered in this Realm.");
                if (isFilesystemBacked(record))
                    throw new InvalidOperationException("Filesystem-backed skins cannot enter the Realm external-edit workflow.");

                return record.Detach();
            });

            return skinImporter.BeginExternalEditing(authoritative);
        }

        public Task<Live<SkinInfo>> Import(ImportTask task, ImportParameters parameters = default, CancellationToken cancellationToken = default) =>
            skinImporter.Import(task, parameters, cancellationToken);

        public Task ExportCurrentSkin() => ExportSkin(CurrentSkinInfo.Value);

        public Task ExportSkin(Live<SkinInfo> skin)
        {
            if (skin.PerformRead(isFilesystemBacked))
                throw new InvalidOperationException("Filesystem-backed skins cannot be exported as Realm packages.");

            return skinExporter.ExportAsync(skin);
        }

        #endregion

        public void Delete([CanBeNull] Expression<Func<SkinInfo, bool>> filter = null, bool silent = false)
        {
            Realm.Run(r =>
            {
                var items = r.All<SkinInfo>()
                             .Where(s => !s.Protected
                                         && !s.DeletePending
                                         && string.IsNullOrEmpty(s.FilesystemStoragePath)
                                         && !s.IsExternalFilesystemStorage);
                if (filter != null)
                    items = items.Where(filter);

                // check the removed skin is not the current user choice. if it is, switch back to default.
                Guid currentUserSkin = CurrentSkinInfo.Value.ID;

                if (items.Any(s => s.ID == currentUserSkin))
                    scheduler.Add(() => CurrentSkinInfo.Value = DefaultOmsSkin.SkinInfo);

                Delete(items.ToList(), silent);
            });
        }

        public void Rename(Live<SkinInfo> skin, string newName)
        {
            if (skin.PerformRead(isFilesystemBacked))
                throw new InvalidOperationException("Filesystem-backed skins cannot be renamed through the Realm package workflow.");

            skin.PerformWrite(s =>
            {
                s.Name = newName;
                skinImporter.UpdateSkinIniMetadata(s, s.Realm!);
            });
        }

        public override bool Delete(SkinInfo item)
        {
            return Realm.Write(realm =>
            {
                SkinInfo authoritative = realm.Find<SkinInfo>(item.ID);

                if (authoritative == null
                    || authoritative.Protected
                    || SkinFilesystemStorageResolver.IsFixedSkinId(authoritative.ID)
                    || isFilesystemBacked(authoritative)
                    || authoritative.DeletePending)
                {
                    return false;
                }

                authoritative.DeletePending = true;
                return true;
            });
        }

        public override void Delete(List<SkinInfo> items, bool silent = false)
            => base.Delete(items.Where(item => !isFilesystemBacked(item)).ToList(), silent);

        public override void Undelete(SkinInfo item)
        {
            Realm.Write(realm =>
            {
                SkinInfo authoritative = realm.Find<SkinInfo>(item.ID);

                if (authoritative == null
                    || isFilesystemBacked(authoritative)
                    || !authoritative.DeletePending)
                {
                    return;
                }

                authoritative.DeletePending = false;
            });
        }

        public override void Undelete(List<SkinInfo> items, bool silent = false)
            => base.Undelete(items.Where(item => !isFilesystemBacked(item)).ToList(), silent);

        public override void AddFile(SkinInfo item, Stream contents, string filename)
        {
            if (isFilesystemBacked(item))
                throw new InvalidOperationException("Filesystem-backed skins cannot receive Realm package files.");

            base.AddFile(item, contents, filename);
        }

        public override void DeleteFile(SkinInfo item, RealmNamedFileUsage file)
        {
            if (isFilesystemBacked(item))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            base.DeleteFile(item, file);
        }

        public override void ReplaceFile(SkinInfo item, RealmNamedFileUsage file, Stream contents)
        {
            if (isFilesystemBacked(item))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            base.ReplaceFile(item, file, contents);
        }

        public override void AddFile(SkinInfo item, Stream contents, string filename, Realm realm)
        {
            if (isFilesystemBacked(item, realm))
                throw new InvalidOperationException("Filesystem-backed skins cannot receive Realm package files.");

            base.AddFile(item, contents, filename, realm);
        }

        public override void DeleteFile(SkinInfo item, RealmNamedFileUsage file, Realm realm)
        {
            if (isFilesystemBacked(item, realm))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            base.DeleteFile(item, file, realm);
        }

        public override void ReplaceFile(SkinInfo item, RealmNamedFileUsage file, Stream contents, Realm realm)
        {
            if (isFilesystemBacked(item, realm))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            base.ReplaceFile(item, file, contents, realm);
        }

        public bool CanModify(Live<SkinInfo> skin)
            => skin.PerformRead(info => !info.Protected && !isFilesystemBacked(info));

        /// <summary>
        /// Returns the settings delete affordance from a fresh authoritative Realm read. This grants no mutation
        /// capability; confirmation re-enters the dedicated operation and repeats every logical/native check.
        /// </summary>
        internal bool CanDelete(Live<SkinInfo> skin)
        {
            if (skin == null)
                return false;

            try
            {
                Guid recordId = skin.ID;
                return Realm.Run(r =>
                {
                    r.Refresh();
                    return classifyDeleteCandidate(r, recordId) != DeleteCandidateKind.Rejected;
                });
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Existing settings delete caller. Realm packages retain their legacy soft-delete/default transition, while
        /// exact managed folders are delegated to the manager-owned journalled worker.
        /// </summary>
        internal Task<bool> DeleteSkinAsync(
            Guid recordId,
            CancellationToken cancellationToken = default)
        {
            if (!ThreadSafety.IsUpdateThread || cancellationToken.IsCancellationRequested)
                return Task.FromResult(false);

            DeleteCandidateKind candidateKind;
            SkinInfo ordinarySnapshot = null;

            try
            {
                candidateKind = Realm.Run(r =>
                {
                    r.Refresh();
                    SkinInfo authoritative = r.Find<SkinInfo>(recordId);
                    DeleteCandidateKind kind = classifyDeleteCandidate(r, recordId);

                    if (kind == DeleteCandidateKind.RealmPackage)
                        ordinarySnapshot = authoritative?.Detach();

                    return kind;
                });
            }
            catch
            {
                return Task.FromResult(false);
            }

            if (candidateKind == DeleteCandidateKind.RealmPackage)
            {
                bool deleted = ordinarySnapshot != null && Delete(ordinarySnapshot);

                if (deleted)
                    CurrentSkinInfo.SetDefault();

                return Task.FromResult(deleted);
            }

            if (candidateKind != DeleteCandidateKind.ManagedFolder)
                return Task.FromResult(false);

            return reduceManagedFolderDeleteResult(DeleteManagedFolderAsync(recordId, cancellationToken));
        }

        private async Task<bool> reduceManagedFolderDeleteResult(
            Task<SkinManagedFolderDeleteOperationResult> operationTask)
        {
            try
            {
                return (await operationTask.ConfigureAwait(false)).IsSuccess;
            }
            catch
            {
                return false;
            }
        }

        private DeleteCandidateKind classifyDeleteCandidate(Realm realm, Guid recordId)
        {
            SkinInfo record = realm.Find<SkinInfo>(recordId);

            if (record == null
                || !record.IsManaged
                || record.Protected
                || record.DeletePending
                || SkinFilesystemStorageResolver.IsFixedSkinId(record.ID))
            {
                return DeleteCandidateKind.Rejected;
            }

            if (string.IsNullOrEmpty(record.FilesystemStoragePath))
            {
                return record.IsExternalFilesystemStorage
                    ? DeleteCandidateKind.Rejected
                    : DeleteCandidateKind.RealmPackage;
            }

            if (record.IsExternalFilesystemStorage
                || record.Files.Count != 0
                || !string.Equals(
                    record.FilesystemStorageAuthorityOwner,
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    StringComparison.Ordinal)
                || !SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
                || string.IsNullOrEmpty(record.Hash)
                || !SkinManagedFolderPath.TryNormalise(record.FilesystemStoragePath, out string normalisedPath)
                || !string.Equals(record.FilesystemStoragePath, normalisedPath, StringComparison.Ordinal)
                || ManagedFolderOperationCoordinator.IsMutationBlocked
                || ManagedFolderOperationCoordinator.IsPathFrozen(normalisedPath)
                || realm.All<SkinInfo>().AsEnumerable().Any(candidate => candidate.IsExternalFilesystemStorage))
            {
                return DeleteCandidateKind.Rejected;
            }

            int matchingPaths = realm.All<SkinInfo>()
                                     .AsEnumerable()
                                     .Count(candidate =>
                                         SkinManagedFolderPath.TryNormalise(
                                             candidate.FilesystemStoragePath,
                                             out string candidatePath)
                                         && string.Equals(
                                             candidatePath,
                                             normalisedPath,
                                             StringComparison.OrdinalIgnoreCase));

            return matchingPaths == 1
                ? DeleteCandidateKind.ManagedFolder
                : DeleteCandidateKind.Rejected;
        }

        private static bool isFilesystemBacked(SkinInfo skin)
            => !string.IsNullOrEmpty(skin.FilesystemStoragePath) || skin.IsExternalFilesystemStorage;

        private static bool isFilesystemBacked(SkinInfo skin, Realm realm)
            => isFilesystemBacked(realm.Find<SkinInfo>(skin.ID) ?? skin);

        public void SetSkinFromConfiguration(string guidString)
        {
            Live<SkinInfo> skinInfo = null;

            if (Guid.TryParse(guidString, out var guid))
            {
                if (guid == SkinInfo.OMS_SKIN)
                    skinInfo = DefaultOmsSkin.SkinInfo;
                else
                    skinInfo = Query(s => s.ID == guid && !s.Protected);
            }

            CurrentSkinInfo.Value = skinInfo ?? DefaultOmsSkin.SkinInfo;
        }

        private sealed class PendingManagedFolderSelectionCompletion
        {
            public long Generation { get; }
            public Live<SkinInfo> Target { get; }
            public SelectionRequest Request { get; }
            public SkinManagedFolderOperationCoordinator.SelectionPreparationObservation PreparationObservation { get; }
            public CancellationTokenSource Cancellation { get; }
            public Task<SkinManagedPackageCaptureResult> CaptureTask { get; }

            public PendingManagedFolderSelectionCompletion(
                long generation,
                Live<SkinInfo> target,
                SelectionRequest request,
                SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
                CancellationTokenSource cancellation,
                Task<SkinManagedPackageCaptureResult> captureTask)
            {
                Generation = generation;
                Target = target;
                Request = request;
                PreparationObservation = preparationObservation;
                Cancellation = cancellation;
                CaptureTask = captureTask;
            }
        }

        private sealed class PendingManagedFolderDeleteFallback
        {
            private int claimed;

            public SkinManagedFolderMutationAuthoritySession Authority { get; }
            public SkinManagedFolderDurableMutationReceipt DurableReceipt { get; }
            public CancellationToken CancellationToken { get; }
            public TaskCompletionSource<SkinManagedFolderProtectedFallbackCommitResult> Completion { get; }
                = new TaskCompletionSource<SkinManagedFolderProtectedFallbackCommitResult>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

            public PendingManagedFolderDeleteFallback(
                SkinManagedFolderMutationAuthoritySession authority,
                SkinManagedFolderDurableMutationReceipt durableReceipt,
                CancellationToken cancellationToken)
            {
                Authority = authority ?? throw new ArgumentNullException(nameof(authority));
                DurableReceipt = durableReceipt ?? throw new ArgumentNullException(nameof(durableReceipt));
                CancellationToken = cancellationToken;
            }

            public bool TryClaim() => Interlocked.CompareExchange(ref claimed, 1, 0) == 0;

            public override string ToString() => nameof(PendingManagedFolderDeleteFallback);
        }

        private sealed class SelectionRequest
        {
            public SkinFilesystemStorageResolution Resolution { get; }
            public SkinInfo Snapshot { get; }
            public bool IsRealmManaged { get; }
            public bool HasExactScannerOwner { get; }

            public SelectionRequest(
                SkinFilesystemStorageResolution resolution,
                SkinInfo snapshot,
                bool isRealmManaged,
                bool hasExactScannerOwner)
            {
                Resolution = resolution;
                Snapshot = snapshot;
                IsRealmManaged = isRealmManaged;
                HasExactScannerOwner = hasExactScannerOwner;
            }

            public bool Matches(SkinInfo current, Storage storage)
            {
                if (Snapshot == null
                    || !current.IsManaged
                    || !string.Equals(current.FilesystemStorageAuthorityOwner, SkinManagedFolderScanner.AUTHORITY_OWNER, StringComparison.Ordinal)
                    || current.ID != Snapshot.ID
                    || !string.Equals(current.Name, Snapshot.Name, StringComparison.Ordinal)
                    || !string.Equals(current.Creator, Snapshot.Creator, StringComparison.Ordinal)
                    || !string.Equals(current.InstantiationInfo, Snapshot.InstantiationInfo, StringComparison.Ordinal)
                    || !string.Equals(current.Hash, Snapshot.Hash, StringComparison.Ordinal)
                    || current.Protected != Snapshot.Protected
                    || !string.Equals(current.FilesystemStoragePath, Snapshot.FilesystemStoragePath, StringComparison.Ordinal)
                    || current.IsExternalFilesystemStorage != Snapshot.IsExternalFilesystemStorage
                    || !string.Equals(current.FilesystemStorageAuthorityOwner, Snapshot.FilesystemStorageAuthorityOwner, StringComparison.Ordinal)
                    || current.DeletePending != Snapshot.DeletePending
                    || current.Files.Count != 0)
                {
                    return false;
                }

                SkinFilesystemStorageResolution currentResolution = SkinFilesystemStorageResolver.ResolveExisting(current, storage);
                return currentResolution.Authority == SkinFilesystemStorageAuthority.ManagedFolder
                       && currentResolution.ManagedCaptureRequest != null
                       && SkinManagedFolderFactory.IsInstantiationInfoAllowed(current.InstantiationInfo);
            }
        }

        private enum DeleteCandidateKind
        {
            Rejected,
            RealmPackage,
            ManagedFolder,
        }

        private sealed class PreparedManagedFolderSelection
        {
            public Live<SkinInfo> Target { get; }
            public Skin Skin { get; }

            public PreparedManagedFolderSelection(Live<SkinInfo> target, Skin skin)
            {
                Target = target;
                Skin = skin;
            }
        }
    }
}
