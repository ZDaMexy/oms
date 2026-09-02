// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
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
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Database;
using osu.Game.Extensions;
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
        LiveGameplayActive,
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

    internal enum SkinCurrentRevisionReloadResult
    {
        Success,
        NoChange,
        LiveGameplayActive,
        ParticipantRejected,
        SourceUnsupported,
        SourceUnavailable,
        SourceChanged,
        Superseded,
        Cancelled,
        SchedulerFailed,
        Shutdown,
        Failed,
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
        internal const string CURRENT_REALM_PACKAGE_MUTATION_DISABLED_DIAGNOSTIC =
            "The current Realm skin package can only change through revision preparation and publication.";

        internal const string REALM_PACKAGE_MUTATION_BUSY_DIAGNOSTIC =
            "Realm skin package mutation is unavailable while another skin package operation is in progress.";

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

        public readonly Bindable<Skin> CurrentSkin = new SkinInstanceBindable();

        public readonly Bindable<Live<SkinInfo>> CurrentSkinInfo = new SkinSelectionBindable(OmsSkin.CreateInfo().ToLiveUnmanaged());

        internal SkinSelectionRejectionReason LastSelectionRejectionReason { get; private set; }

        internal Func<SkinManagedPackageCaptureRequest, CancellationToken, SkinManagedPackageCaptureResult> ManagedFolderCapture { get; set; }
            = (request, cancellationToken) => SkinManagedPackageCapture.Capture(request, cancellationToken: cancellationToken);

        internal Func<SkinInfo, IStorageResourceProvider, SkinPackageRevisionCapsule, SkinManagedFolderFactoryResult> ManagedFolderFactoryCreate { get; set; }
            = SkinManagedFolderFactory.Create;

        internal Action<Action> ManagedFolderCompletionSchedule { get; set; }

        internal Action<Action> ManagedFolderDeleteFallbackSchedule { get; set; }

        internal Action<Action> CurrentRevisionCompletionSchedule { get; set; }

        internal Action CurrentRevisionPrepareStarted { get; set; } = () => { };

        internal Action CurrentRevisionBeforeCommitSchedule { get; set; } = () => { };

        internal Action CurrentExternalUnregisterBeforeRealmCommit { get; set; } = () => { };

        internal Action CurrentRealmPackageDeleteBeforeRealmCommit { get; set; } = () => { };

        internal Action RealmPackageMutationBoundaryEntered { get; set; } = () => { };

        internal Action ManagedFolderBeforeCommit { get; set; } = () => { };

        internal Action<Live<SkinInfo>> SelectionRequestBeforeCommitLock { get; set; } = _ => { };

        internal Action ManagedFolderStagedImportAuthorityOpened { get; set; } = () => { };

        internal Action ManagedFolderSelectionFinalBoundaryContended { get; set; } = () => { };

        internal Action ManagedFolderSelectionWaitingForStartup { get; set; } = () => { };

        internal Action ManagedFolderSelectionWaitingForStagedImport { get; set; } = () => { };

        internal Action<string> OpenFolderExternally { get; set; }

        internal Action ExternalFolderSelectionCaptureAuthorityOpened { get; set; } = () => { };

        internal Action<CancellationToken> FolderWorkspaceRecordsReadStarted { get; set; } = _ => { };

        internal Action<CancellationToken> FolderWorkspaceJournalSupportReadStarted { get; set; } = _ => { };

        /// <summary>
        /// Signals that a manager-owned mutation/recovery worker reached an observable completion boundary and the
        /// redacted journal support projection should be re-inspected. No journal or path data crosses this event.
        /// </summary>
        internal event Action ManagedFolderJournalStateChanged;

        internal SkinManagedFolderOperationCoordinator ManagedFolderOperationCoordinator { get; } = new SkinManagedFolderOperationCoordinator();

        internal SkinManagedFolderMutationAuthority ManagedFolderMutationAuthority { get; }

        internal SkinManagedFolderMutationRecoveryResult InitialManagedFolderMutationRecoveryResult { get; }

        private readonly SkinManagedFolderMutationRecovery managedFolderMutationRecovery;
        private readonly ISkinManagedFolderMutationJournalStore managedFolderMutationJournalStore;
        private readonly ISkinManagedFolderMutationNativeAuthority managedFolderMutationNativeAuthority;
        private readonly ISkinExternalFolderCaptureService externalFolderCaptureService;
        private readonly SkinExternalFolderRegistryService externalFolderRegistry;
        private readonly SkinManagedFolderRenameOperation managedFolderRenameOperation;
        private readonly SkinManagedFolderStagedImportOperation managedFolderStagedImportOperation;
        private readonly SkinManagedFolderDeleteOperation managedFolderDeleteOperation;
        private readonly object managedFolderRenameLifecycleGate = new object();
        private readonly HashSet<FolderWorkspaceReadOperation> folderWorkspaceReadOperations = new HashSet<FolderWorkspaceReadOperation>();
        private readonly object managedFolderSelectionLifecycleGate = new object();
        private readonly CancellationTokenSource managedFolderSelectionRetryCancellation = new CancellationTokenSource();
        private readonly HashSet<Task> managedFolderSelectionWorkerTasks = new HashSet<Task>();
        private readonly HashSet<PendingManagedFolderSelectionCompletion> pendingManagedFolderSelectionCompletions = new HashSet<PendingManagedFolderSelectionCompletion>();
        private readonly HashSet<PendingExternalFolderSelectionCompletion> pendingExternalFolderSelectionCompletions = new HashSet<PendingExternalFolderSelectionCompletion>();
        private readonly object currentRevisionRetireGate = new object();
        private readonly Queue<SkinCurrentRevision> currentRevisionRetireQueue = new Queue<SkinCurrentRevision>();
        private readonly SkinCurrentRevisionPublication currentRevisionPublication;
        private PublishedCurrentSkinPair publishedCurrentSkinPair;
        private readonly object currentRevisionReloadGate = new object();
        private readonly HashSet<PendingCurrentRevisionCallback> pendingCurrentRevisionCallbacks = new HashSet<PendingCurrentRevisionCallback>();
        private readonly HashSet<Task<SkinCurrentRevisionReloadResult>> currentRevisionReloadWorkerTasks = new HashSet<Task<SkinCurrentRevisionReloadResult>>();
        private readonly HashSet<CancellationTokenSource> currentRevisionReloadWorkerCancellations = new HashSet<CancellationTokenSource>();
        private CancellationTokenSource activeCurrentRevisionReloadCancellation;
        private Task<SkinCurrentRevisionReloadResult> activeCurrentRevisionReloadTask;
        private long currentRevisionReloadGeneration;
        private int currentRevisionRetireScheduled;
        private int currentRevisionPublicationShutdown;
        private int currentRevisionManagerLeaseReleased;
        private int currentRevisionPublicationBroadcast;
        private int currentSkinProjectionInProgress;
        private CancellationTokenSource activeManagedFolderRenameCancellation;
        private Task<SkinManagedFolderRenameOperationResult> activeManagedFolderRenameTask;
        private CancellationTokenSource activeManagedFolderStagedImportCancellation;
        private Task<SkinManagedFolderStagedImportOperationResult> activeManagedFolderStagedImportTask;
        private CancellationTokenSource activeManagedFolderDeleteCancellation;
        private Task<SkinManagedFolderDeleteOperationResult> activeManagedFolderDeleteTask;
        private CancellationTokenSource activeManagedFolderRecoveryCancellation;
        private Task<bool> activeManagedFolderRecoveryTask;
        private CancellationTokenSource activeFolderWorkspaceCancellation;
        private Task<bool> activeFolderWorkspaceTask;
        private PendingManagedFolderDeleteFallback pendingManagedFolderDeleteFallback;
        private SkinManagedFolderRenameOperationResult lastManagedFolderRenameResult;
        private SkinManagedFolderStagedImportOperationResult lastManagedFolderStagedImportResult;
        private SkinManagedFolderDeleteOperationResult lastManagedFolderDeleteResult;
        private bool managedFolderMutationShutdown;
        private bool currentRevisionMutationAdmissionHeld;
        private bool currentRevisionReloadAdmissionHeld;
        private int currentRevisionReloadAdmissionCount;
        private int realmPackageMutationOwnerManagedThreadId;
        private int realmPackageMutationAdmissionDepth;
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

        private readonly SkinImporter skinImporter;

        internal Action<CancellationToken> SkinImportAfterFileRecordsCommittedTestHook
        {
            get => skinImporter.ImportAfterFileRecordsCommittedTestHook;
            set => skinImporter.ImportAfterFileRecordsCommittedTestHook = value;
        }

        internal Action<CancellationToken> SkinImportAfterPopulateTestHook
        {
            get => skinImporter.ImportAfterPopulateTestHook;
            set => skinImporter.ImportAfterPopulateTestHook = value;
        }

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
            CurrentRevisionCompletionSchedule = completion => scheduler.Add(completion);
            OpenFolderExternally = path => host.OpenFileExternally(path);

            managedFolderMutationJournalStore = new SkinManagedFolderMutationJournalStore(storage);
            managedFolderMutationNativeAuthority = new WindowsSkinManagedFolderMutationNativeAuthority(storage);
            externalFolderCaptureService = new SkinExternalFolderCaptureService();
            externalFolderRegistry = new SkinExternalFolderRegistryService(
                realm,
                storage,
                ManagedFolderOperationCoordinator,
                externalFolderCaptureService);
            ManagedFolderMutationAuthority = new SkinManagedFolderMutationAuthority(
                realm,
                storage,
                ManagedFolderOperationCoordinator,
                managedFolderMutationNativeAuthority,
                managedFolderMutationJournalStore,
                externalFolderCaptureService,
                externalRegistryService: externalFolderRegistry);
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
                            managedFolderMutationNativeAuthority)),
                    (
                        SkinManagedFolderMutationKind.ManagedCopy,
                        new SkinManagedFolderManagedCopyRecoveryHandler(
                            realm,
                            managedFolderMutationNativeAuthority))),
                new SkinManagedFolderMutationRecoveryAuthority(
                    ManagedFolderOperationCoordinator,
                    managedFolderMutationNativeAuthority,
                    externalFolderRegistry));
            InitialManagedFolderMutationRecoveryResult = managedFolderMutationRecovery.Recover();

            userFiles = new StorageBackedResourceStore(storage.GetStorageForDirectory("files"));

            skinImporter = new SkinImporter(
                storage,
                realm,
                this,
                info => !isFilesystemBacked(info) && !isCurrentRevisionRecord(info.ID))
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

            ((SkinInstanceBindable)CurrentSkin).CommitPrepared(DefaultOmsSkin);
            currentRevisionPublication = new SkinCurrentRevisionPublication(
                DefaultOmsSkin,
                DefaultOmsSkin.GetCurrentRevisionContentIdentity(),
                SkinCurrentRevisionSourceKind.ProtectedFallback,
                keepsReusableOwner: true,
                queueCurrentRevisionRetirement);
            publishedCurrentSkinPair = new PublishedCurrentSkinPair(
                CurrentSkinInfo.Value,
                DefaultOmsSkin,
                currentRevisionPublication.Current);
            ((SkinInstanceBindable)CurrentSkin).AuthoritativeValueProvider =
                () => Volatile.Read(ref publishedCurrentSkinPair).Owner;
            ((SkinInstanceBindable)CurrentSkin).IsAuthoritativeRoot = true;
            ((SkinSelectionBindable)CurrentSkinInfo).AuthoritativeValueProvider =
                () => Volatile.Read(ref publishedCurrentSkinPair).Selection;
            ((SkinSelectionBindable)CurrentSkinInfo).IsAuthoritativeRoot = true;
            ((SkinSelectionBindable)CurrentSkinInfo).SelectionRequested = requestSelection;

            skinExporter = new LegacySkinExporter(storage)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };
        }

        /// <summary>
        /// The exact immutable revision currently exposed by this manager. This is intentionally path-free.
        /// </summary>
        internal SkinCurrentRevision CurrentRevision
            => Volatile.Read(ref publishedCurrentSkinPair).Revision;

        internal bool CanReloadCurrentRevision
        {
            get
            {
                PublishedCurrentSkinPair pair = Volatile.Read(ref publishedCurrentSkinPair);

                if (CurrentSkinInfo.Disabled
                    || CurrentSkin.Disabled
                    || pair.Selection.ID != pair.Owner.SkinInfo.ID
                    || !ReferenceEquals(pair.Owner, pair.Revision.Owner))
                {
                    return false;
                }

                return pair.Revision.SourceKind is SkinCurrentRevisionSourceKind.RealmPackage
                    or SkinCurrentRevisionSourceKind.ManagedFolder
                    or SkinCurrentRevisionSourceKind.ExternalFolder;
            }
        }

        internal event Action<SkinCurrentRevision> CurrentRevisionRetired;

        internal SkinRevisionParticipantRegistration RegisterRevisionParticipant(
            SkinRevisionParticipantKind kind,
            string diagnosticName,
            Func<CancellationToken, Task<bool>> prepare = null,
            Func<SkinCurrentRevision, CancellationToken, Task<SkinRevisionParticipantCommit>> prepareCommit = null,
            bool blocksRevisionPublication = false,
            bool affectsGameplayLayoutPublication = false,
            Action shutdownWork = null)
            => currentRevisionPublication.Register(
                kind,
                diagnosticName,
                prepare,
                prepareCommit,
                blocksRevisionPublication,
                affectsGameplayLayoutPublication,
                shutdownWork);

        internal bool IsCurrentRevisionPublicationBroadcast
            => Volatile.Read(ref currentRevisionPublicationBroadcast) != 0;

        internal SkinRevisionParticipantRegistration RegisterRevisionHolderForOwner(
            Skin owner,
            string diagnosticName)
            => currentRevisionPublication.RegisterExactOwner(
                owner,
                SkinRevisionParticipantKind.LifecycleHolder,
                diagnosticName);

        /// <summary>
        /// Retains the exact current owner for a resource tail which can outlive the participant that created it.
        /// Examples include an already-playing sample channel and a cross-revision background fade.
        /// </summary>
        internal SkinCurrentRevisionLease AcquireCurrentRevisionHolderLease()
            => currentRevisionPublication.AcquireCurrentHolderLease();

        /// <summary>
        /// Retains the exact current owner for hidden asynchronous work. Unlike a visual/tail holder, this lease is
        /// explicitly joined during manager shutdown and also participates in current-delete detach gating.
        /// </summary>
        internal SkinCurrentRevisionLease AcquireCurrentRevisionWorkLease()
            => currentRevisionPublication.AcquireCurrentWorkLease();

        internal SkinRevisionParticipantSnapshot CaptureRevisionParticipantSnapshot(
            out SkinRevisionBarrierRejectionReason rejectionReason)
            => currentRevisionPublication.CaptureSnapshot(out rejectionReason);

        internal Task<SkinRevisionBarrierRejectionReason> PrepareRevisionParticipantsAsync(
            SkinRevisionParticipantSnapshot snapshot,
            CancellationToken cancellationToken)
            => currentRevisionPublication.PrepareParticipantsAsync(snapshot, cancellationToken);

        internal Task<SkinRevisionParticipantPrepareResult> PrepareRevisionParticipantsForRevisionAsync(
            SkinRevisionParticipantSnapshot snapshot,
            SkinCurrentRevision nextRevision,
            CancellationToken cancellationToken)
            => currentRevisionPublication.PrepareParticipantsForRevisionAsync(snapshot, nextRevision, cancellationToken);

        internal SkinCurrentRevision CreateProvisionalCurrentRevision(
            Skin owner,
            string contentRevision,
            SkinCurrentRevisionSourceKind sourceKind)
            => currentRevisionPublication.CreateProvisional(
                owner.SkinInfo.ID,
                contentRevision,
                sourceKind,
                owner,
                keepsReusableOwner: sourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback);

        /// <summary>
        /// Performs the allocation- and I/O-free update-thread publication barrier for an already prepared owner.
        /// </summary>
        internal bool TryPublishPreparedCurrentRevision(
            SkinRevisionPreparedBarrier participantBarrier,
            SkinCurrentRevision preparedRevision,
            Live<SkinInfo> authoritativeSelection,
            out SkinRevisionBarrierRejectionReason rejectionReason)
        {
            if (!ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Current skin revision publication must run on the update thread.");

            ArgumentNullException.ThrowIfNull(participantBarrier);
            ArgumentNullException.ThrowIfNull(preparedRevision);
            ArgumentNullException.ThrowIfNull(authoritativeSelection);

            if (CurrentSkinInfo.Disabled
                || CurrentSkin.Disabled
                || CurrentSkinInfo.Value.ID != authoritativeSelection.ID
                || preparedRevision.RecordId != authoritativeSelection.ID
                || preparedRevision.Owner.SkinInfo.ID != authoritativeSelection.ID)
            {
                rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                return false;
            }

            if (!ReferenceEquals(participantBarrier.NextRevision, preparedRevision))
            {
                rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                return false;
            }

            var preparedPair = new PublishedCurrentSkinPair(
                authoritativeSelection,
                preparedRevision.Owner,
                preparedRevision);

            if (!currentRevisionPublication.TryCommitPair(
                    participantBarrier,
                    () => publishAuthoritativeManagerPair(preparedPair),
                    out SkinCurrentRevision previousRevision,
                    out rejectionReason))
            {
                return false;
            }

            completePublishedCurrentRevision(preparedPair, previousRevision, usesCoherentBarrier: true);

            return ReferenceEquals(currentRevisionPublication.Current, preparedRevision)
                   && ReferenceEquals(CurrentSkin.Value, preparedRevision.Owner);
        }

        private bool tryPublishPreparedCurrentSelection(
            SkinRevisionPreparedBarrier participantBarrier,
            SkinCurrentRevision preparedRevision,
            Live<SkinInfo> authoritativeSelection,
            Live<SkinInfo> expectedSelection,
            Skin expectedOwner,
            SkinCurrentRevision expectedRevision,
            out SkinRevisionBarrierRejectionReason rejectionReason)
        {
            if (!ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Current skin selection publication must run on the update thread.");

            if (CurrentSkinInfo.Disabled
                || CurrentSkin.Disabled
                || !ReferenceEquals(CurrentSkinInfo.Value, expectedSelection)
                || !ReferenceEquals(CurrentSkin.Value, expectedOwner)
                || !ReferenceEquals(currentRevisionPublication.Current, expectedRevision)
                || preparedRevision.RecordId != authoritativeSelection.ID
                || preparedRevision.Owner.SkinInfo.ID != authoritativeSelection.ID)
            {
                rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                return false;
            }

            if (!ReferenceEquals(participantBarrier.NextRevision, preparedRevision))
            {
                rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                return false;
            }

            var preparedPair = new PublishedCurrentSkinPair(
                authoritativeSelection,
                preparedRevision.Owner,
                preparedRevision);

            if (!currentRevisionPublication.TryCommitPair(
                    participantBarrier,
                    () => publishAuthoritativeManagerPair(preparedPair),
                    out SkinCurrentRevision previousRevision,
                    out rejectionReason))
            {
                return false;
            }

            completePublishedCurrentRevision(preparedPair, previousRevision, usesCoherentBarrier: true);

            return ReferenceEquals(currentRevisionPublication.Current, preparedRevision)
                   && ReferenceEquals(CurrentSkin.Value, preparedRevision.Owner)
                   && ReferenceEquals(CurrentSkinInfo.Value, authoritativeSelection);
        }

        /// <summary>
        /// Publishes a different selection while every already attached consumer deliberately retains its exact old
        /// revision. This is the safe compatibility path for consumers without a candidate-specific staged receipt;
        /// late instances attach to the committed owner and no existing consumer re-queries it during the barrier.
        /// </summary>
        private bool tryPublishPreparedCurrentSelectionRetainingParticipants(
            SkinRevisionParticipantSnapshot participantSnapshot,
            SkinCurrentRevision preparedRevision,
            Live<SkinInfo> authoritativeSelection,
            Live<SkinInfo> expectedSelection,
            Skin expectedOwner,
            SkinCurrentRevision expectedRevision,
            out SkinRevisionBarrierRejectionReason rejectionReason)
        {
            if (!ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Current skin selection publication must run on the update thread.");

            if (CurrentSkinInfo.Disabled
                || CurrentSkin.Disabled
                || !ReferenceEquals(CurrentSkinInfo.Value, expectedSelection)
                || !ReferenceEquals(CurrentSkin.Value, expectedOwner)
                || !ReferenceEquals(currentRevisionPublication.Current, expectedRevision)
                || preparedRevision.RecordId != authoritativeSelection.ID
                || preparedRevision.Owner.SkinInfo.ID != authoritativeSelection.ID)
            {
                rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                return false;
            }

            var preparedPair = new PublishedCurrentSkinPair(
                authoritativeSelection,
                preparedRevision.Owner,
                preparedRevision);

            if (!currentRevisionPublication.TryCommitPair(
                    participantSnapshot,
                    preparedRevision,
                    () => publishAuthoritativeManagerPair(preparedPair),
                    out SkinCurrentRevision previousRevision,
                    out rejectionReason))
            {
                return false;
            }

            completePublishedCurrentRevision(preparedPair, previousRevision, usesCoherentBarrier: false);

            return ReferenceEquals(currentRevisionPublication.Current, preparedRevision)
                   && ReferenceEquals(CurrentSkin.Value, preparedRevision.Owner)
                   && ReferenceEquals(CurrentSkinInfo.Value, authoritativeSelection);
        }

        /// <summary>
        /// Pure authoritative reference publication executed under the registry lock. The projection flag closes the
        /// lock-release-to-bindable-notification gap against nested selection, reload and mutation admission.
        /// </summary>
        private void publishAuthoritativeManagerPair(PublishedCurrentSkinPair pair)
        {
            Volatile.Write(ref currentSkinProjectionInProgress, 1);
            Volatile.Write(ref publishedCurrentSkinPair, pair);
        }

        /// <summary>
        /// Updates the event projections after the immutable authoritative pair is already committed. This runs after
        /// the publication lock has been released, so arbitrary bindable observers cannot add fallible work to the
        /// commit barrier. Manager getters and resource lookup continue to read the authoritative pair throughout.
        /// </summary>
        private void projectPreparedManagerPair(PublishedCurrentSkinPair pair)
        {
            commitPreparedOwnerWithoutObserverFailure(pair.Owner);
            commitPreparedSelectionWithoutObserverFailure(pair.Selection);

            if (!ReferenceEquals(((SkinInstanceBindable)CurrentSkin).ProjectedValue, pair.Owner)
                || !ReferenceEquals(((SkinSelectionBindable)CurrentSkinInfo).ProjectedValue, pair.Selection))
            {
                throw new InvalidOperationException("The prepared current skin projections could not be committed.");
            }
        }

        private void commitPreparedOwnerWithoutObserverFailure(Skin owner)
        {
            try
            {
                ((SkinInstanceBindable)CurrentSkin).CommitPrepared(owner);
            }
            catch (Exception exception)
            {
                if (!ReferenceEquals(((SkinInstanceBindable)CurrentSkin).ProjectedValue, owner))
                    throw;

                Logger.Log($"A current skin owner observer failed ({exception.GetType().Name}).");
            }
        }

        private void commitPreparedSelectionWithoutObserverFailure(Live<SkinInfo> selection)
        {
            try
            {
                ((SkinSelectionBindable)CurrentSkinInfo).CommitPrepared(selection);
            }
            catch (Exception exception)
            {
                if (!ReferenceEquals(((SkinSelectionBindable)CurrentSkinInfo).ProjectedValue, selection))
                    throw;

                Logger.Log($"A current skin selection observer failed ({exception.GetType().Name}).");
            }
        }

        private void completePublishedCurrentRevision(
            PublishedCurrentSkinPair pair,
            SkinCurrentRevision previousRevision,
            bool usesCoherentBarrier)
        {
            try
            {
                if (usesCoherentBarrier)
                    Volatile.Write(ref currentRevisionPublicationBroadcast, 1);

                projectPreparedManagerPair(pair);
                Volatile.Write(ref currentSkinProjectionInProgress, 0);

                if (Volatile.Read(ref managedFolderDeleteFallbackSourceChangeDeferral) != 0)
                    Volatile.Write(ref managedFolderDeleteFallbackSourceChangePending, 1);
                else
                    notifySourceChanged();
            }
            finally
            {
                if (usesCoherentBarrier)
                    Volatile.Write(ref currentRevisionPublicationBroadcast, 0);

                Volatile.Write(ref currentSkinProjectionInProgress, 0);

                previousRevision.ReleaseManagerLease();
            }
        }

        internal void DiscardProvisionalCurrentRevision(SkinCurrentRevision provisionalRevision)
        {
            if (provisionalRevision == null
                || ReferenceEquals(currentRevisionPublication.Current, provisionalRevision))
            {
                return;
            }

            provisionalRevision.ReleaseManagerLease();
        }

        private SkinCurrentRevision createCurrentRevision(Skin owner)
        {
            SkinCurrentRevisionSourceKind sourceKind = getCurrentRevisionSourceKind(owner);
            return CreateProvisionalCurrentRevision(owner, owner.GetCurrentRevisionContentIdentity(), sourceKind);
        }

        private static SkinCurrentRevisionSourceKind getCurrentRevisionSourceKind(Skin owner)
        {
            return owner.SkinInfo.PerformRead(info =>
            {
                if (info.Protected)
                    return SkinCurrentRevisionSourceKind.ProtectedFallback;

                if (info.IsExternalFilesystemStorage)
                    return SkinCurrentRevisionSourceKind.ExternalFolder;

                if (!string.IsNullOrEmpty(info.FilesystemStoragePath))
                    return SkinCurrentRevisionSourceKind.ManagedFolder;

                if (!string.IsNullOrEmpty(owner.PackageContentRevision))
                    return SkinCurrentRevisionSourceKind.RealmPackage;

                return info.IsManaged
                    ? SkinCurrentRevisionSourceKind.RealmPackage
                    : SkinCurrentRevisionSourceKind.Compatibility;
            });
        }

        private void notifySourceChanged()
        {
            Delegate[] handlers = SourceChanged?.GetInvocationList() ?? Array.Empty<Delegate>();

            foreach (Delegate handler in handlers)
            {
                try
                {
                    ((Action)handler)();
                }
                catch
                {
                    // A participant callback is never allowed to split an already committed pair. Diagnostics expose
                    // only the callback type; source paths and package labels are deliberately absent.
                    Logger.Log($"A skin revision consumer callback failed ({handler.Method.DeclaringType?.Name ?? "unknown"}).");
                }
            }
        }

        private void queueCurrentRevisionRetirement(SkinCurrentRevision revision)
        {
            lock (currentRevisionRetireGate)
                currentRevisionRetireQueue.Enqueue(revision);

            if (ThreadSafety.IsUpdateThread || Volatile.Read(ref currentRevisionPublicationShutdown) != 0)
            {
                // Once shutdown has claimed publication, the framework scheduler may still accept callbacks which it
                // will never execute. Late async/work leases therefore reap their exact owner inline after their real
                // task/tail completes; idempotent owner retirement makes this safe against the final shutdown drain.
                drainCurrentRevisionRetireQueue();
                return;
            }

            if (Interlocked.Exchange(ref currentRevisionRetireScheduled, 1) != 0)
                return;

            try
            {
                scheduler.Add(drainCurrentRevisionRetireQueue);
            }
            catch
            {
                // Scheduler shutdown/fault must not leak a captured package owner. Disposal is idempotent and this is
                // the final fallback after the revision has no remaining consumer lease.
                drainCurrentRevisionRetireQueue();
            }
        }

        private void drainCurrentRevisionRetireQueue()
        {
            while (true)
            {
                SkinCurrentRevision revision;

                lock (currentRevisionRetireGate)
                {
                    if (currentRevisionRetireQueue.Count == 0)
                    {
                        Volatile.Write(ref currentRevisionRetireScheduled, 0);
                        return;
                    }

                    revision = currentRevisionRetireQueue.Dequeue();
                }

                revision.RetireOwner();

                try
                {
                    CurrentRevisionRetired?.Invoke(revision);
                }
                catch
                {
                    Logger.Log("A skin revision retirement observer failed.");
                }
            }
        }

        /// <summary>
        /// Explicit safe-navigation command for rebuilding the current same-record package revision.
        /// </summary>
        internal Task<SkinCurrentRevisionReloadResult> ReloadCurrentRevisionAsync(
            CancellationToken cancellationToken = default)
        {
            if (!ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Current skin reload must be requested on the update thread.");

            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(SkinCurrentRevisionReloadResult.Cancelled);

            if (Volatile.Read(ref currentRevisionPublicationShutdown) != 0)
                return Task.FromResult(SkinCurrentRevisionReloadResult.Shutdown);

            SkinRevisionParticipantSnapshot initialParticipants =
                currentRevisionPublication.CaptureSnapshot(
                    out SkinRevisionBarrierRejectionReason participantRejection,
                    requiresPreparedCoherentReceipts: true);

            if (initialParticipants == null)
                return Task.FromResult(mapBarrierRejection(participantRejection));

            CurrentRevisionReloadRequest request;

            try
            {
                if (CurrentSkinInfo.Disabled
                    || CurrentSkinInfo.Value.ID != CurrentSkin.Value.SkinInfo.ID
                    || !ReferenceEquals(currentRevisionPublication.Current.Owner, CurrentSkin.Value))
                {
                    return Task.FromResult(SkinCurrentRevisionReloadResult.SourceChanged);
                }

                request = CurrentSkinInfo.Value.PerformRead(info =>
                    new CurrentRevisionReloadRequest(
                        CurrentSkinInfo.Value,
                        CurrentSkin.Value,
                        currentRevisionPublication.Current,
                        Interlocked.Read(ref selectionGeneration),
                        createSelectionRequest(info),
                        createRealmPackageSnapshot(info)));
            }
            catch
            {
                return Task.FromResult(SkinCurrentRevisionReloadResult.SourceUnavailable);
            }

            if (request.SourceRequest.Resolution.Authority == SkinFilesystemStorageAuthority.Invalid
                || request.ExpectedRevision.SourceKind == SkinCurrentRevisionSourceKind.ProtectedFallback
                || request.ExpectedRevision.SourceKind == SkinCurrentRevisionSourceKind.Compatibility)
            {
                return Task.FromResult(SkinCurrentRevisionReloadResult.SourceUnsupported);
            }

            if (!tryAdmitCurrentRevisionReload())
                return Task.FromResult(SkinCurrentRevisionReloadResult.ParticipantRejected);

            lock (currentRevisionReloadGate)
            {
                if (Volatile.Read(ref currentRevisionPublicationShutdown) == 0)
                {
                    long generation = Interlocked.Increment(ref currentRevisionReloadGeneration);

                    try
                    {
                        activeCurrentRevisionReloadCancellation?.Cancel();
                    }
                    catch
                    {
                        // Cancellation observers belong to the superseded worker. They cannot consume the newly
                        // admitted latest request or strand its admission count.
                    }

                    var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    activeCurrentRevisionReloadCancellation = operationCancellation;
                    Task<SkinCurrentRevisionReloadResult> operationTask = Task.Run(
                        () => executeCurrentRevisionReloadSafelyAsync(
                            generation,
                            request,
                            initialParticipants,
                            operationCancellation.Token),
                        CancellationToken.None);
                    activeCurrentRevisionReloadTask = operationTask;
                    currentRevisionReloadWorkerTasks.Add(operationTask);
                    currentRevisionReloadWorkerCancellations.Add(operationCancellation);

                    _ = operationTask.ContinueWith(
                        _ => completeCurrentRevisionReload(generation, operationTask, operationCancellation),
                        CancellationToken.None,
                        TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);

                    return operationTask;
                }
            }

            releaseCurrentRevisionReloadAdmission();
            return Task.FromResult(SkinCurrentRevisionReloadResult.Shutdown);
        }

        private async Task<SkinCurrentRevisionReloadResult> executeCurrentRevisionReloadSafelyAsync(
            long generation,
            CurrentRevisionReloadRequest request,
            SkinRevisionParticipantSnapshot initialParticipants,
            CancellationToken cancellationToken)
        {
            try
            {
                return await executeCurrentRevisionReloadAsync(
                    generation,
                    request,
                    initialParticipants,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return generation == Interlocked.Read(ref currentRevisionReloadGeneration)
                    ? SkinCurrentRevisionReloadResult.Cancelled
                    : SkinCurrentRevisionReloadResult.Superseded;
            }
            catch
            {
                return SkinCurrentRevisionReloadResult.Failed;
            }
        }

        private async Task<SkinCurrentRevisionReloadResult> executeCurrentRevisionReloadAsync(
            long generation,
            CurrentRevisionReloadRequest request,
            SkinRevisionParticipantSnapshot initialParticipants,
            CancellationToken cancellationToken)
        {
            SkinRevisionParticipantSnapshot participants = initialParticipants;

            for (int participantRetry = 0; participantRetry < 8; participantRetry++)
            {
                if (generation != Interlocked.Read(ref currentRevisionReloadGeneration))
                    return SkinCurrentRevisionReloadResult.Superseded;

                if (!isCurrentRevisionReloadRequestCurrent(request))
                    return SkinCurrentRevisionReloadResult.SourceChanged;

                cancellationToken.ThrowIfCancellationRequested();
                CurrentRevisionPrepareStarted();

                using CurrentRevisionReloadPreparation preparation = prepareCurrentRevisionReload(request, cancellationToken);

                if (generation != Interlocked.Read(ref currentRevisionReloadGeneration))
                    return SkinCurrentRevisionReloadResult.Superseded;

                cancellationToken.ThrowIfCancellationRequested();

                if (!preparation.IsSuccess)
                    return preparation.FailureResult;

                preparation.Validate(cancellationToken);

                SkinRevisionBarrierRejectionReason ready = await currentRevisionPublication
                                                                 .PrepareParticipantsAsync(participants, cancellationToken)
                                                                 .ConfigureAwait(false);

                // Every await boundary re-establishes latest-wins before interpreting participant or source state.
                // An uncooperative superseded participant may complete after the next generation has already
                // published; that stale worker must never surface SourceChanged/Failed or proceed to NoChange.
                if (generation != Interlocked.Read(ref currentRevisionReloadGeneration))
                    return SkinCurrentRevisionReloadResult.Superseded;

                cancellationToken.ThrowIfCancellationRequested();
                preparation.Validate(cancellationToken);

                if (!isCurrentRevisionReloadRequestCurrent(request)
                    || !validateCurrentRevisionRealmAuthority(request))
                {
                    return SkinCurrentRevisionReloadResult.SourceChanged;
                }

                if (ready == SkinRevisionBarrierRejectionReason.ParticipantSetChanged)
                {
                    participants = currentRevisionPublication.CaptureSnapshot(
                        out ready,
                        requiresPreparedCoherentReceipts: true);

                    if (participants != null)
                        continue;
                }

                if (ready != SkinRevisionBarrierRejectionReason.None)
                    return mapBarrierRejection(ready);

                if (string.Equals(
                        preparation.ContentRevision,
                        request.ExpectedRevision.ContentRevision,
                        StringComparison.Ordinal))
                {
                    return SkinCurrentRevisionReloadResult.NoChange;
                }

                Skin owner = preparation.TransferSkin();
                SkinCurrentRevision provisional;

                try
                {
                    provisional = CreateProvisionalCurrentRevision(
                        owner,
                        preparation.ContentRevision,
                        preparation.SourceKind);
                }
                catch
                {
                    owner.Dispose();
                    return SkinCurrentRevisionReloadResult.Shutdown;
                }

                try
                {
                    SkinRevisionParticipantPrepareResult staged = await currentRevisionPublication
                                                                    .PrepareParticipantsForRevisionAsync(
                                                                        participants,
                                                                        provisional,
                                                                        cancellationToken)
                                                                    .ConfigureAwait(false);

                    if (generation != Interlocked.Read(ref currentRevisionReloadGeneration))
                        return SkinCurrentRevisionReloadResult.Superseded;

                    cancellationToken.ThrowIfCancellationRequested();
                    preparation.Validate(cancellationToken);

                    if (!isCurrentRevisionReloadRequestCurrent(request)
                        || !validateCurrentRevisionRealmAuthority(request))
                    {
                        return SkinCurrentRevisionReloadResult.SourceChanged;
                    }

                    if (!staged.IsSuccess)
                    {
                        if (staged.RejectionReason == SkinRevisionBarrierRejectionReason.ParticipantSetChanged)
                        {
                            participants = currentRevisionPublication.CaptureSnapshot(
                                out SkinRevisionBarrierRejectionReason recaptureRejection,
                                requiresPreparedCoherentReceipts: true);

                            if (participants != null)
                                continue;

                            return mapBarrierRejection(recaptureRejection);
                        }

                        return mapBarrierRejection(staged.RejectionReason);
                    }

                    using SkinRevisionPreparedBarrier participantBarrier = staged.Barrier;

                    // Test/user-controlled delays happen before the final exact authority validation. From the validation
                    // return through the barrier, production mutation entry points are excluded by the reload admission;
                    // the update-thread callback below therefore performs only in-memory generation/reference checks.
                    CurrentRevisionBeforeCommitSchedule();

                    if (generation != Interlocked.Read(ref currentRevisionReloadGeneration))
                        return SkinCurrentRevisionReloadResult.Superseded;

                    cancellationToken.ThrowIfCancellationRequested();
                    preparation.Validate(cancellationToken);

                    if (!isCurrentRevisionReloadRequestCurrent(request)
                        || !validateCurrentRevisionRealmAuthority(request))
                    {
                        return SkinCurrentRevisionReloadResult.SourceChanged;
                    }

                    CurrentRevisionCommitAttempt commit = await scheduleCurrentRevisionCommit(
                        generation,
                        request,
                        participantBarrier,
                        provisional,
                        cancellationToken).ConfigureAwait(false);

                    if (commit == CurrentRevisionCommitAttempt.Success)
                        return SkinCurrentRevisionReloadResult.Success;

                    if (commit == CurrentRevisionCommitAttempt.ParticipantSetChanged)
                    {
                        participants = currentRevisionPublication.CaptureSnapshot(
                            out SkinRevisionBarrierRejectionReason recaptureRejection,
                            requiresPreparedCoherentReceipts: true);

                        if (participants != null)
                            continue;

                        return mapBarrierRejection(recaptureRejection);
                    }

                    return commit switch
                    {
                        CurrentRevisionCommitAttempt.Superseded => SkinCurrentRevisionReloadResult.Superseded,
                        CurrentRevisionCommitAttempt.Cancelled => SkinCurrentRevisionReloadResult.Cancelled,
                        CurrentRevisionCommitAttempt.SchedulerFailed => SkinCurrentRevisionReloadResult.SchedulerFailed,
                        CurrentRevisionCommitAttempt.Shutdown => SkinCurrentRevisionReloadResult.Shutdown,
                        _ => SkinCurrentRevisionReloadResult.SourceChanged,
                    };
                }
                finally
                {
                    // Once created, the provisional owner remains manager-owned until the barrier makes it current.
                    // Cancellation, final validation, participant and scheduler faults all converge through this claim.
                    DiscardProvisionalCurrentRevision(provisional);
                }
            }

            return SkinCurrentRevisionReloadResult.ParticipantRejected;
        }

        private bool isCurrentRevisionReloadRequestCurrent(CurrentRevisionReloadRequest request)
            => Interlocked.Read(ref selectionGeneration) == request.SelectionGeneration
               && !CurrentSkinInfo.Disabled
               && CurrentSkinInfo.Value.ID == request.ExpectedSelection.ID
               && ReferenceEquals(CurrentSkin.Value, request.ExpectedOwner)
               && ReferenceEquals(currentRevisionPublication.Current, request.ExpectedRevision);

        private bool validateCurrentRevisionRealmAuthority(CurrentRevisionReloadRequest request)
        {
            try
            {
                return Realm.Run(realm =>
                {
                    realm.Refresh();
                    SkinInfo current = realm.Find<SkinInfo>(request.ExpectedSelection.ID);

                    if (current == null)
                        return false;

                    switch (request.SourceRequest.Resolution.Authority)
                    {
                        case SkinFilesystemStorageAuthority.RealmPackage:
                            return request.RealmSnapshot != null
                                   && request.RealmSnapshot.Matches(createRealmPackageSnapshot(current));

                        case SkinFilesystemStorageAuthority.ManagedFolder:
                            return request.SourceRequest.Matches(current, storage)
                                   && !current.IsExternalFilesystemStorage
                                   && string.Equals(
                                       current.FilesystemStorageAuthorityOwner,
                                       SkinManagedFolderScanner.AUTHORITY_OWNER,
                                       StringComparison.Ordinal);

                        case SkinFilesystemStorageAuthority.ExternalFolder:
                            return request.SourceRequest.Matches(current, storage)
                                   && externalFolderRegistry.TryReadAndValidateDeclarations(
                                       out SkinExternalFolderRegistryDeclaration[] declarations,
                                       out _,
                                       out _,
                                       out _)
                                   && exactlyMatchesExternalDeclarations(realm.All<SkinInfo>(), declarations);

                        default:
                            return false;
                    }
                });
            }
            catch
            {
                return false;
            }
        }

        private Task<CurrentRevisionCommitAttempt> scheduleCurrentRevisionCommit(
            long generation,
            CurrentRevisionReloadRequest request,
            SkinRevisionPreparedBarrier participantBarrier,
            SkinCurrentRevision provisional,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<CurrentRevisionCommitAttempt>(TaskCreationOptions.RunContinuationsAsynchronously);

            CurrentRevisionCallbackScheduleResult scheduleResult = scheduleCurrentRevisionCallback(
                () =>
                {
                    if (generation != Interlocked.Read(ref currentRevisionReloadGeneration))
                    {
                        completion.TrySetResult(CurrentRevisionCommitAttempt.Superseded);
                        return;
                    }

                    if (Volatile.Read(ref currentRevisionPublicationShutdown) != 0)
                    {
                        completion.TrySetResult(CurrentRevisionCommitAttempt.Shutdown);
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        completion.TrySetResult(CurrentRevisionCommitAttempt.Cancelled);
                        return;
                    }

                    if (Interlocked.Read(ref selectionGeneration) != request.SelectionGeneration
                        || CurrentSkinInfo.Disabled
                        || CurrentSkinInfo.Value.ID != request.ExpectedSelection.ID
                        || !ReferenceEquals(CurrentSkin.Value, request.ExpectedOwner)
                        || !ReferenceEquals(currentRevisionPublication.Current, request.ExpectedRevision))
                    {
                        completion.TrySetResult(CurrentRevisionCommitAttempt.SourceChanged);
                        return;
                    }

                    bool published = TryPublishPreparedCurrentRevision(
                        participantBarrier,
                        provisional,
                        request.ExpectedSelection,
                        out SkinRevisionBarrierRejectionReason rejection);

                    completion.TrySetResult(published
                        ? CurrentRevisionCommitAttempt.Success
                        : rejection switch
                        {
                            SkinRevisionBarrierRejectionReason.ParticipantSetChanged => CurrentRevisionCommitAttempt.ParticipantSetChanged,
                            SkinRevisionBarrierRejectionReason.Shutdown => CurrentRevisionCommitAttempt.Shutdown,
                            _ => CurrentRevisionCommitAttempt.SourceChanged,
                        });
                },
                () => completion.TrySetResult(CurrentRevisionCommitAttempt.Shutdown));

            if (scheduleResult == CurrentRevisionCallbackScheduleResult.Faulted)
                completion.TrySetResult(CurrentRevisionCommitAttempt.SchedulerFailed);

            return completion.Task;
        }

        private CurrentRevisionReloadPreparation prepareCurrentRevisionReload(
            CurrentRevisionReloadRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!validateCurrentRevisionRealmAuthority(request))
                    return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceChanged);

                return request.SourceRequest.Resolution.Authority switch
                {
                    SkinFilesystemStorageAuthority.RealmPackage => prepareRealmPackageRevision(request, cancellationToken),
                    SkinFilesystemStorageAuthority.ManagedFolder => prepareManagedFolderRevision(request, cancellationToken),
                    SkinFilesystemStorageAuthority.ExternalFolder => prepareExternalFolderRevision(request, cancellationToken),
                    _ => CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnsupported),
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnavailable);
            }
        }

        private CurrentRevisionReloadPreparation prepareRealmPackageRevision(
            CurrentRevisionReloadRequest request,
            CancellationToken cancellationToken)
        {
            RealmPackageRevisionSnapshot fresh = readRealmPackageRevisionSnapshot(request.ExpectedSelection.ID);

            if (fresh == null || !request.RealmSnapshot.MatchesMetadata(fresh))
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceChanged);

            var entries = new List<SkinPackageCapturedEntry>(fresh.Files.Count);

            foreach (RealmPackageFileDeclaration file in fresh.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] content = userFiles.Get(new RealmFile { Hash = file.Hash }.GetStoragePath());

                if (content == null)
                    return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnavailable);

                string actualHash = Convert.ToHexString(SHA256.HashData(content));

                if (!string.Equals(actualHash, file.Hash, StringComparison.OrdinalIgnoreCase))
                    return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceChanged);

                entries.Add(SkinPackageCapturedEntry.CreateFile(file.Filename, content));
            }

            SkinPackageRevisionCapsuleCreationResult capsule =
                SkinPackageRevisionCapsuleFactory.Create(entries, cancellationToken: cancellationToken);

            if (!capsule.IsSuccess)
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnavailable);

            SkinInfo exactInfo = createFilesystemSkinSnapshot(fresh.Metadata);
            exactInfo.Hash = capsule.Capsule!.ContentRevision;
            SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(exactInfo, this, capsule.Capsule);

            if (!factory.IsSuccess)
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnsupported);

            RealmPackageRevisionSnapshot post = readRealmPackageRevisionSnapshot(request.ExpectedSelection.ID);

            if (!fresh.Matches(post))
            {
                factory.Skin!.Dispose();
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceChanged);
            }

            return CurrentRevisionReloadPreparation.Success(
                factory.Skin!,
                exactInfo.Hash,
                SkinCurrentRevisionSourceKind.RealmPackage,
                validate: _ =>
                {
                    RealmPackageRevisionSnapshot current = readRealmPackageRevisionSnapshot(request.ExpectedSelection.ID);
                    if (!fresh.Matches(current))
                        throw new InvalidOperationException("The Realm package revision changed during preparation.");
                });
        }

        private CurrentRevisionReloadPreparation prepareManagedFolderRevision(
            CurrentRevisionReloadRequest request,
            CancellationToken cancellationToken)
        {
            if (request.SourceRequest.Resolution.ManagedCaptureRequest == null
                || request.SourceRequest.Snapshot == null
                || !request.SourceRequest.IsRealmManaged
                || !request.SourceRequest.HasExactScannerOwner)
            {
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnsupported);
            }

            SkinManagedPackageHeldCaptureResult capture = SkinManagedPackageCapture.CaptureHeld(
                request.SourceRequest.Resolution.ManagedCaptureRequest,
                cancellationToken: cancellationToken);

            if (!capture.IsSuccess)
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnavailable);

            ISkinManagedPackageCaptureSession session = capture.Session!;
            Skin preparedSkin = null;
            bool authorityTransferred = false;

            try
            {
                SkinPackageRevisionCapsule capsule = session.TakeCapsule();

                if (!SkinManagedFolderPackageMetadataReader.TryRead(capsule, out SkinManagedFolderPackageMetadata metadata))
                {
                    capsule.Dispose();
                    return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnavailable);
                }

                SkinInfo freshInfo = createFilesystemSkinSnapshot(request.SourceRequest.Snapshot);
                freshInfo.Name = metadata!.Name;
                freshInfo.Creator = metadata.Creator;
                freshInfo.Hash = metadata.ContentRevision;
                SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(freshInfo, this, capsule);

                if (!factory.IsSuccess)
                    return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnsupported);

                preparedSkin = factory.Skin!;
                session.Validate(cancellationToken);
                CurrentRevisionReloadPreparation result = CurrentRevisionReloadPreparation.Success(
                    preparedSkin,
                    metadata.ContentRevision,
                    SkinCurrentRevisionSourceKind.ManagedFolder,
                    validate: token => session.Validate(token),
                    authority: session);
                preparedSkin = null;
                authorityTransferred = true;
                return result;
            }
            finally
            {
                preparedSkin?.Dispose();

                if (!authorityTransferred)
                    session.Dispose();
            }
        }

        private CurrentRevisionReloadPreparation prepareExternalFolderRevision(
            CurrentRevisionReloadRequest request,
            CancellationToken cancellationToken)
        {
            ExternalFolderSelectionPreparationResult prepared =
                prepareExternalFolderSelection(request.SourceRequest, cancellationToken);

            if (!prepared.IsSuccess)
                return CurrentRevisionReloadPreparation.Reject(SkinCurrentRevisionReloadResult.SourceUnavailable);

            try
            {
                prepared.PackageSession.Validate(cancellationToken);
                prepared.ManagedAuthority.ValidateCompleteAndStable(cancellationToken);
                return CurrentRevisionReloadPreparation.Success(
                    prepared.TransferSkin(),
                    prepared.Metadata.ContentRevision,
                    SkinCurrentRevisionSourceKind.ExternalFolder,
                    validate: token =>
                    {
                        prepared.PackageSession.Validate(token);
                        prepared.ManagedAuthority.ValidateCompleteAndStable(token);
                    },
                    authority: prepared);
            }
            catch
            {
                prepared.Dispose();
                throw;
            }
        }

        private RealmPackageRevisionSnapshot createRealmPackageSnapshot(SkinInfo info)
        {
            if (!info.IsManaged
                || info.Protected
                || !string.IsNullOrEmpty(info.FilesystemStoragePath)
                || info.IsExternalFilesystemStorage)
            {
                return null;
            }

            return RealmPackageRevisionSnapshot.Create(info);
        }

        private RealmPackageRevisionSnapshot readRealmPackageRevisionSnapshot(Guid recordId)
            => Realm.Run(realm =>
            {
                realm.Refresh();
                SkinInfo current = realm.Find<SkinInfo>(recordId);
                return current == null ? null : createRealmPackageSnapshot(current);
            });

        private static SkinCurrentRevisionReloadResult mapBarrierRejection(
            SkinRevisionBarrierRejectionReason rejection)
            => rejection switch
            {
                SkinRevisionBarrierRejectionReason.LiveGameplayActive => SkinCurrentRevisionReloadResult.LiveGameplayActive,
                SkinRevisionBarrierRejectionReason.ParticipantRejected => SkinCurrentRevisionReloadResult.ParticipantRejected,
                SkinRevisionBarrierRejectionReason.Shutdown => SkinCurrentRevisionReloadResult.Shutdown,
                SkinRevisionBarrierRejectionReason.CurrentRevisionChanged => SkinCurrentRevisionReloadResult.SourceChanged,
                _ => SkinCurrentRevisionReloadResult.ParticipantRejected,
            };

        private void completeCurrentRevisionReload(
            long generation,
            Task<SkinCurrentRevisionReloadResult> operationTask,
            CancellationTokenSource operationCancellation)
        {
            if (operationTask.IsFaulted)
                _ = operationTask.Exception;

            lock (currentRevisionReloadGate)
            {
                currentRevisionReloadWorkerTasks.Remove(operationTask);
                currentRevisionReloadWorkerCancellations.Remove(operationCancellation);

                if (generation == Interlocked.Read(ref currentRevisionReloadGeneration)
                    && ReferenceEquals(activeCurrentRevisionReloadTask, operationTask))
                {
                    activeCurrentRevisionReloadTask = null;
                    activeCurrentRevisionReloadCancellation = null;
                }
            }

            releaseCurrentRevisionReloadAdmission();
            operationCancellation.Dispose();
        }

        private CurrentRevisionCallbackScheduleResult scheduleCurrentRevisionCallback(
            Action callback,
            Action shutdown)
            => scheduleCurrentRevisionCallbackUsing(
                CurrentRevisionCompletionSchedule,
                callback,
                shutdown);

        /// <summary>
        /// Rollback is a convergence obligation after the fallback barrier. A fault injected into the ordinary
        /// completion hook may fail a new publication, but cannot strand the manager on fallback after an unchanged
        /// source mutation. Retry once through the owned framework scheduler before declaring shutdown.
        /// </summary>
        private CurrentRevisionCallbackScheduleResult scheduleCriticalCurrentRevisionCallback(
            Action callback,
            Action shutdown)
        {
            CurrentRevisionCallbackScheduleResult result = scheduleCurrentRevisionCallback(callback, shutdown);

            return result == CurrentRevisionCallbackScheduleResult.Faulted
                ? scheduleCurrentRevisionCallbackUsing(action => scheduler.Add(action), callback, shutdown)
                : result;
        }

        private CurrentRevisionCallbackScheduleResult scheduleCurrentRevisionCallbackUsing(
            Action<Action> completionSchedule,
            Action callback,
            Action shutdown)
        {
            ArgumentNullException.ThrowIfNull(completionSchedule);
            ArgumentNullException.ThrowIfNull(callback);
            ArgumentNullException.ThrowIfNull(shutdown);

            var pending = new PendingCurrentRevisionCallback(callback, shutdown);

            lock (currentRevisionReloadGate)
            {
                if (Volatile.Read(ref currentRevisionPublicationShutdown) != 0)
                {
                    pending.Shutdown();
                    return CurrentRevisionCallbackScheduleResult.Shutdown;
                }

                pendingCurrentRevisionCallbacks.Add(pending);
            }

            try
            {
                completionSchedule(() =>
                {
                    lock (currentRevisionReloadGate)
                    {
                        if (!pendingCurrentRevisionCallbacks.Remove(pending))
                            return;
                    }

                    pending.Run();
                });

                return CurrentRevisionCallbackScheduleResult.Scheduled;
            }
            catch
            {
                bool claimed;

                lock (currentRevisionReloadGate)
                    claimed = pendingCurrentRevisionCallbacks.Remove(pending);

                if (claimed)
                    pending.Abandon();

                return CurrentRevisionCallbackScheduleResult.Faulted;
            }
        }

        private async Task<ProtectedFallbackPublicationTransaction> publishProtectedFallbackAndWaitForDetachAsync(
            Guid expectedRecordId,
            CancellationToken cancellationToken,
            Func<CancellationToken, bool> validatePreparedAuthority = null,
            Func<bool> validateCommitReceipt = null)
        {
            if (!ThreadSafety.IsUpdateThread)
                throw new InvalidOperationException("Protected fallback publication must start on the update thread.");

            if (cancellationToken.IsCancellationRequested
                || CurrentSkinInfo.Disabled
                || CurrentSkinInfo.Value.ID != expectedRecordId
                || CurrentSkin.Value.SkinInfo.ID != expectedRecordId
                || currentRevisionPublication.Current.RecordId != expectedRecordId
                || !ReferenceEquals(currentRevisionPublication.Current.Owner, CurrentSkin.Value))
            {
                return null;
            }

            if (!isExactProtectedFallbackAuthority())
                return null;

            Live<SkinInfo> expectedSelection = CurrentSkinInfo.Value;
            Skin expectedOwner = CurrentSkin.Value;
            SkinCurrentRevision expectedRevision = currentRevisionPublication.Current;
            SkinCurrentRevisionLease rollbackLease = expectedRevision.AcquireOperationLease();
            bool committed = false;

            try
            {
                for (int retry = 0; retry < 8; retry++)
                {
                    if (validatePreparedAuthority != null
                        && !validatePreparedAuthority(cancellationToken))
                    {
                        return null;
                    }

                    SkinRevisionParticipantSnapshot participants =
                        currentRevisionPublication.CaptureSnapshot(
                            out SkinRevisionBarrierRejectionReason captureRejection,
                            requiresPreparedCoherentReceipts: true);

                    if (participants == null)
                        return null;

                    SkinCurrentRevision fallback = CreateProvisionalCurrentRevision(
                        DefaultOmsSkin,
                        DefaultOmsSkin.GetCurrentRevisionContentIdentity(),
                        SkinCurrentRevisionSourceKind.ProtectedFallback);
                    SkinRevisionParticipantPrepareResult staged = await currentRevisionPublication
                                                                        .PrepareParticipantsForRevisionAsync(
                                                                            participants,
                                                                            fallback,
                                                                            cancellationToken)
                                                                        .ConfigureAwait(false);

                    if (!staged.IsSuccess)
                    {
                        DiscardProvisionalCurrentRevision(fallback);

                        if (staged.RejectionReason == SkinRevisionBarrierRejectionReason.ParticipantSetChanged)
                            continue;

                        return null;
                    }

                    using SkinRevisionPreparedBarrier participantBarrier = staged.Barrier;

                    if ((validatePreparedAuthority != null
                         && !validatePreparedAuthority(cancellationToken))
                        || !isExactProtectedFallbackAuthority())
                    {
                        DiscardProvisionalCurrentRevision(fallback);
                        return null;
                    }

                    var completion = new TaskCompletionSource<(bool success, SkinRevisionBarrierRejectionReason rejection)>(
                        TaskCreationOptions.RunContinuationsAsynchronously);

                    CurrentRevisionCallbackScheduleResult scheduleResult = scheduleCurrentRevisionCallback(
                        () =>
                        {
                            if (cancellationToken.IsCancellationRequested
                                || CurrentSkinInfo.Disabled
                                || !ReferenceEquals(CurrentSkinInfo.Value, expectedSelection)
                                || !ReferenceEquals(CurrentSkin.Value, expectedOwner)
                                || !ReferenceEquals(currentRevisionPublication.Current, expectedRevision)
                                || (validateCommitReceipt != null && !validateCommitReceipt()))
                            {
                                completion.TrySetResult((false, SkinRevisionBarrierRejectionReason.CurrentRevisionChanged));
                                return;
                            }

                            bool published = tryPublishPreparedCurrentSelection(
                                participantBarrier,
                                fallback,
                                DefaultOmsSkin.SkinInfo,
                                expectedSelection,
                                expectedOwner,
                                expectedRevision,
                                out SkinRevisionBarrierRejectionReason rejection);
                            completion.TrySetResult((published, rejection));
                        },
                        () => completion.TrySetResult((false, SkinRevisionBarrierRejectionReason.Shutdown)));

                    if (scheduleResult == CurrentRevisionCallbackScheduleResult.Faulted)
                    {
                        DiscardProvisionalCurrentRevision(fallback);
                        return null;
                    }

                    (bool success, SkinRevisionBarrierRejectionReason rejection) result =
                        await completion.Task.ConfigureAwait(false);

                    if (!result.success)
                    {
                        DiscardProvisionalCurrentRevision(fallback);

                        if (result.rejection == SkinRevisionBarrierRejectionReason.ParticipantSetChanged)
                            continue;

                        return null;
                    }

                    committed = true;

                    // Cancellation after the barrier cannot split the pair. From this point the operation always
                    // converges to either its protected fallback or an exact rollback of the old revision.
                    var transaction = new ProtectedFallbackPublicationTransaction(
                        expectedSelection,
                        expectedOwner,
                        expectedRevision,
                        fallback,
                        rollbackLease);

                    try
                    {
                        await expectedRevision.ConsumersDetached.WaitAsync(cancellationToken).ConfigureAwait(false);
                        return transaction;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        await rollbackProtectedFallbackAsync(transaction).ConfigureAwait(false);
                        return null;
                    }
                }

                return null;
            }
            finally
            {
                if (!committed)
                    rollbackLease.Dispose();
            }
        }

        private bool isExactProtectedFallbackAuthority()
        {
            try
            {
                return DefaultOmsSkin.GetType() == typeof(OmsSkin)
                       && DefaultOmsSkin.SkinInfo.PerformRead(
                           SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord)
                       && Realm.Run(realm =>
                       {
                           realm.Refresh();
                           return SkinManagedFolderDeleteOperation.IsExactProtectedFallbackRecord(
                               realm.Find<SkinInfo>(SkinInfo.OMS_SKIN));
                       });
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> rollbackProtectedFallbackAsync(ProtectedFallbackPublicationTransaction transaction)
        {
            while (true)
            {
                if (ReferenceEquals(currentRevisionPublication.Current, transaction.PreviousRevision)
                    && ReferenceEquals(CurrentSkin.Value, transaction.PreviousOwner)
                    && ReferenceEquals(CurrentSkinInfo.Value, transaction.PreviousSelection))
                {
                    transaction.Complete();
                    return true;
                }

                SkinRevisionParticipantSnapshot retainingParticipants =
                    currentRevisionPublication.CaptureRetainingSnapshot(out SkinRevisionBarrierRejectionReason captureRejection);

                if (retainingParticipants == null)
                {
                    transaction.Complete();
                    return false;
                }

                SkinRevisionParticipantRegistration[] liveBlockers = retainingParticipants.Participants
                                                                                          .Where(participant =>
                                                                                              !participant.IsDisposed
                                                                                              && (participant.Kind == SkinRevisionParticipantKind.LiveGameplayHost
                                                                                                  || participant.BlocksRevisionPublication))
                                                                                          .ToArray();

                if (liveBlockers.Length > 0)
                {
                    // A live host or half-loaded coherent consumer which attached to the committed fallback cannot be
                    // split or rebuilt. The latter may transition to a formal staged participant before detaching;
                    // always recapture after the temporary registration leaves and then prepare that formal consumer.
                    await Task.WhenAll(liveBlockers.Select(participant => participant.Detached)).ConfigureAwait(false);
                    continue;
                }

                SkinRevisionParticipantSnapshot participants =
                    currentRevisionPublication.CaptureSnapshot(out captureRejection);

                if (participants == null)
                {
                    // A live/temp blocker may attach between the retaining inventory above and this publication
                    // snapshot. Recapture so the next iteration waits its exact Detached receipt; treating that race
                    // as terminal would strand the protected fallback after an otherwise failed mutation.
                    if (captureRejection is SkinRevisionBarrierRejectionReason.LiveGameplayActive
                        or SkinRevisionBarrierRejectionReason.ParticipantRejected)
                    {
                        continue;
                    }

                    transaction.Complete();
                    return false;
                }

                SkinRevisionParticipantPrepareResult staged = await currentRevisionPublication
                                                                    .PrepareParticipantsForRevisionAsync(
                                                                        participants,
                                                                        transaction.PreviousRevision,
                                                                        CancellationToken.None)
                                                                    .ConfigureAwait(false);

                if (!staged.IsSuccess)
                {
                    if (staged.RejectionReason is SkinRevisionBarrierRejectionReason.ParticipantSetChanged
                        or SkinRevisionBarrierRejectionReason.LiveGameplayActive)
                    {
                        continue;
                    }

                    if (staged.BlockingParticipant != null && !staged.BlockingParticipant.IsDisposed)
                    {
                        // An unsupported visual which attached after fallback must leave before the exact old pair can
                        // be restored. No consumer is changed while this deterministic defer is outstanding.
                        await staged.BlockingParticipant.Detached.ConfigureAwait(false);
                        continue;
                    }

                    transaction.Complete();
                    return false;
                }

                using SkinRevisionPreparedBarrier participantBarrier = staged.Barrier;

                var completion = new TaskCompletionSource<(bool restored, SkinRevisionBarrierRejectionReason rejection)>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                CurrentRevisionCallbackScheduleResult scheduleResult = scheduleCriticalCurrentRevisionCallback(
                    () =>
                    {
                        if (!ReferenceEquals(currentRevisionPublication.Current, transaction.FallbackRevision)
                            || CurrentSkinInfo.Value.ID != SkinInfo.OMS_SKIN
                            || !ReferenceEquals(CurrentSkin.Value, DefaultOmsSkin))
                        {
                            completion.TrySetResult((false, SkinRevisionBarrierRejectionReason.CurrentRevisionChanged));
                            return;
                        }

                        transaction.PreviousRevision.AddManagerLease();

                        bool restored = tryPublishPreparedCurrentSelection(
                            participantBarrier,
                            transaction.PreviousRevision,
                            transaction.PreviousSelection,
                            DefaultOmsSkin.SkinInfo,
                            DefaultOmsSkin,
                            transaction.FallbackRevision,
                            out SkinRevisionBarrierRejectionReason rejection);

                        if (!restored)
                        {
                            transaction.PreviousRevision.ReleaseManagerLease();
                            completion.TrySetResult((false, rejection));
                            return;
                        }

                        if (restored)
                            transaction.Complete();

                        completion.TrySetResult((restored, restored
                            ? SkinRevisionBarrierRejectionReason.None
                            : SkinRevisionBarrierRejectionReason.CurrentRevisionChanged));
                    },
                    () => completion.TrySetResult((false, SkinRevisionBarrierRejectionReason.Shutdown)));

                if (scheduleResult == CurrentRevisionCallbackScheduleResult.Faulted)
                {
                    transaction.Complete();
                    return false;
                }

                (bool restored, SkinRevisionBarrierRejectionReason rejection) result =
                    await completion.Task.ConfigureAwait(false);

                if (result.restored)
                    return true;

                if (result.rejection != SkinRevisionBarrierRejectionReason.ParticipantSetChanged)
                {
                    transaction.Complete();
                    return false;
                }
            }
        }

        internal SkinManagedFolderMutationRecoveryResult RecoverManagedFolderMutations(CancellationToken cancellationToken = default)
        {
            SkinManagedFolderMutationRecoveryResult result = managedFolderMutationRecovery.Recover(cancellationToken);
            notifyManagedFolderJournalStateChanged();
            return result;
        }

        /// <summary>
        /// Returns a path-free support projection. Callers should invoke this away from the update thread because the
        /// operation-specific inspector may need bounded native and Realm reads.
        /// </summary>
        internal Task<FolderSkinJournalSupportSnapshot> GetManagedFolderJournalSupportSnapshotAsync(
            CancellationToken cancellationToken = default)
            => startFolderWorkspaceRead(
                token =>
                {
                    FolderWorkspaceJournalSupportReadStarted(token);
                    token.ThrowIfCancellationRequested();
                    return managedFolderMutationRecovery.InspectSupportSnapshot(token);
                },
                cancellationToken);

        /// <summary>
        /// Re-inspects and, only when still uniquely recoverable, runs the canonical recovery policy on a joined worker.
        /// </summary>
        internal Task<bool> RetryManagedFolderJournalRecoveryAsync(
            CancellationToken cancellationToken = default)
        {
            if (!ThreadSafety.IsUpdateThread)
                return Task.FromResult(false);

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown
                    || cancellationToken.IsCancellationRequested
                    || activeManagedFolderRenameTask is { IsCompleted: false }
                    || activeManagedFolderStagedImportTask is { IsCompleted: false }
                    || activeManagedFolderDeleteTask is { IsCompleted: false }
                    || activeManagedFolderRecoveryTask is { IsCompleted: false }
                    || activeFolderWorkspaceTask is { IsCompleted: false }
                    || currentRevisionMutationAdmissionHeld
                    || currentRevisionReloadAdmissionHeld
                    || realmPackageMutationAdmissionDepth > 0)
                {
                    return Task.FromResult(false);
                }

                var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<bool> operationTask = Task.Run(
                    () => executeManagedFolderRecoveryRetry(operationCancellation.Token),
                    CancellationToken.None);

                activeManagedFolderRecoveryCancellation = operationCancellation;
                activeManagedFolderRecoveryTask = operationTask;

                _ = operationTask.ContinueWith(
                    _ => completeManagedFolderRecoveryTask(operationTask, operationCancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        /// <summary>
        /// Returns immutable, path-free workspace rows from one fresh Realm view. Capability bits are hints only; each
        /// operation re-reads and re-proves its target after confirmation.
        /// </summary>
        internal Task<IReadOnlyList<FolderSkinWorkspaceRecord>> GetFolderSkinWorkspaceRecordsAsync(
            CancellationToken cancellationToken = default)
        {
            Guid currentInfoId = CurrentSkinInfo.Value.ID;
            Guid currentSkinId = CurrentSkin.Value.SkinInfo.ID;
            bool currentPairCoherent = currentInfoId == currentSkinId;

            return startFolderWorkspaceRead(
                cancellationToken =>
                {
                    FolderWorkspaceRecordsReadStarted(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();

                    return Realm.Run(r =>
                    {
                        r.Refresh();
                        SkinInfo[] records = r.All<SkinInfo>().AsEnumerable().ToArray();
                        var rows = new List<FolderSkinWorkspaceRecord>();

                        foreach (SkinInfo record in records)
                        {
                            cancellationToken.ThrowIfCancellationRequested();

                            if (!record.IsManaged
                                || record.Protected
                                || record.DeletePending
                                || record.Files.Count != 0
                                || SkinFilesystemStorageResolver.IsFixedSkinId(record.ID)
                                || string.IsNullOrEmpty(record.FilesystemStoragePath))
                            {
                                continue;
                            }

                            string displayLabel = string.IsNullOrWhiteSpace(record.Name)
                                ? "Folder skin"
                                : record.Name;

                            if (record.IsExternalFilesystemStorage)
                            {
                                if (!string.Equals(
                                        record.FilesystemStorageAuthorityOwner,
                                        SkinExternalFolderRegistry.AUTHORITY_OWNER,
                                        StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                SkinFilesystemStorageResolution resolution =
                                    SkinFilesystemStorageResolver.ResolveExisting(record, storage);
                                bool sourceUsable = resolution.Authority == SkinFilesystemStorageAuthority.ExternalFolder
                                                    && SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
                                                    && !string.IsNullOrEmpty(record.Hash);

                                rows.Add(new FolderSkinWorkspaceRecord(
                                    record.ID,
                                    displayLabel,
                                    FolderSkinWorkspaceRecordKind.External,
                                    canOpenFolder: sourceUsable,
                                    canImportManagedCopy: sourceUsable,
                                    canUnregister: !ManagedFolderOperationCoordinator.IsMutationBlocked
                                                   && currentPairCoherent,
                                    canRename: false,
                                    canDelete: false));
                                continue;
                            }

                            if (!string.Equals(
                                    record.FilesystemStorageAuthorityOwner,
                                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                                    StringComparison.Ordinal)
                                || !SkinManagedFolderPath.TryNormalise(
                                    record.FilesystemStoragePath,
                                    out string normalisedPath)
                                || !string.Equals(
                                    record.FilesystemStoragePath,
                                    normalisedPath,
                                    StringComparison.Ordinal)
                                || records.Count(candidate =>
                                    SkinManagedFolderPath.TryNormalise(
                                        candidate.FilesystemStoragePath,
                                        out string candidatePath)
                                    && string.Equals(
                                        candidatePath,
                                        normalisedPath,
                                        StringComparison.OrdinalIgnoreCase)) != 1
                                || !SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
                                || string.IsNullOrEmpty(record.Hash))
                            {
                                continue;
                            }

                            bool mutationAvailable = !ManagedFolderOperationCoordinator.IsMutationBlocked
                                                     && !ManagedFolderOperationCoordinator.IsPathFrozen(normalisedPath);
                            rows.Add(new FolderSkinWorkspaceRecord(
                                record.ID,
                                displayLabel,
                                FolderSkinWorkspaceRecordKind.Managed,
                                canOpenFolder: !ManagedFolderOperationCoordinator.IsPathFrozen(normalisedPath),
                                canImportManagedCopy: false,
                                canUnregister: false,
                                canRename: mutationAvailable,
                                canDelete: mutationAvailable));
                        }

                        return (IReadOnlyList<FolderSkinWorkspaceRecord>)rows
                            .OrderBy(row => row.DisplayLabel, StringComparer.OrdinalIgnoreCase)
                            .ThenBy(row => row.RecordId)
                            .ToArray();
                    });
                },
                cancellationToken);
        }

        private Task<T> startFolderWorkspaceRead<T>(
            Func<CancellationToken, T> read,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(read);

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown || cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled<T>(new CancellationToken(canceled: true));

                var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<T> operationTask = Task.Run(
                    () =>
                    {
                        operationCancellation.Token.ThrowIfCancellationRequested();
                        T result = read(operationCancellation.Token);
                        operationCancellation.Token.ThrowIfCancellationRequested();
                        return result;
                    },
                    operationCancellation.Token);
                var operation = new FolderWorkspaceReadOperation(operationTask, operationCancellation);
                folderWorkspaceReadOperations.Add(operation);

                _ = operationTask.ContinueWith(
                    _ => completeFolderWorkspaceRead(operation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        private void completeFolderWorkspaceRead(FolderWorkspaceReadOperation operation)
        {
            if (operation.Task.IsFaulted)
                _ = operation.Task.Exception;

            lock (managedFolderRenameLifecycleGate)
                folderWorkspaceReadOperations.Remove(operation);

            operation.Cancellation.Dispose();
        }

        internal Task<bool> RegisterExternalFolderAsync(string selectedDirectory, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(selectedDirectory))
                return Task.FromResult(false);

            return startFolderWorkspaceOperation(
                token => registerExternalFolder(selectedDirectory, token),
                cancellationToken);
        }

        internal Task<bool> OpenFolderAsync(Guid recordId, CancellationToken cancellationToken = default)
        {
            if (recordId == Guid.Empty)
                return Task.FromResult(false);

            return startFolderWorkspaceOperation(
                token => openFolder(recordId, token),
                cancellationToken);
        }

        internal Task<bool> ImportManagedCopyAsync(Guid externalRecordId, string targetChildName, CancellationToken cancellationToken = default)
        {
            if (externalRecordId == Guid.Empty
                || !SkinManagedFolderPath.TryCreateFromChildName(targetChildName, out _))
            {
                return Task.FromResult(false);
            }

            return startFolderWorkspaceOperation(
                token => importManagedCopy(externalRecordId, targetChildName, token),
                cancellationToken);
        }

        internal Task<bool> UnregisterExternalFolderAsync(Guid recordId, CancellationToken cancellationToken = default)
        {
            if (!ThreadSafety.IsUpdateThread
                || recordId == Guid.Empty
                || cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(false);
            }

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown
                    || hasActiveFolderMutationHeld())
                {
                    return Task.FromResult(false);
                }

                bool isCurrentTarget = CurrentSkinInfo.Value.ID == recordId
                                       || CurrentSkin.Value.SkinInfo.ID == recordId;

                if (isCurrentTarget)
                {
                    if (CurrentSkinInfo.Value.ID != recordId
                        || CurrentSkin.Value.SkinInfo.ID != recordId)
                    {
                        return Task.FromResult(false);
                    }

                    if (!tryAdmitCurrentRevisionMutation())
                        return Task.FromResult(false);

                    return trackAdmittedCurrentRevisionMutation(
                        token => unregisterCurrentExternalFolderAdmittedAsync(recordId, token),
                        cancellationToken);
                }

                if (!ManagedFolderOperationCoordinator.TryEnter(out SkinManagedFolderOperationCoordinator.Lease operationLease))
                    return Task.FromResult(false);

                using (operationLease)
                {
                    if (ManagedFolderOperationCoordinator.IsMutationBlocked)
                        return Task.FromResult(false);

                    bool removed = unregisterExternalFolderOnUpdateThread(recordId, cancellationToken);

                    if (removed)
                    {
                        Interlocked.Increment(ref selectionGeneration);
                        cancelPendingSelection();
                    }

                    return Task.FromResult(removed);
                }
            }
        }

        private Task<bool> startFolderWorkspaceOperation(
            Func<CancellationToken, bool> operation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);

            if (!ThreadSafety.IsUpdateThread || cancellationToken.IsCancellationRequested)
                return Task.FromResult(false);

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown || hasActiveFolderMutationHeld())
                    return Task.FromResult(false);

                var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<bool> operationTask = Task.Run(
                    () => executeFolderWorkspaceOperation(operation, operationCancellation.Token),
                    CancellationToken.None);

                activeFolderWorkspaceCancellation = operationCancellation;
                activeFolderWorkspaceTask = operationTask;

                _ = operationTask.ContinueWith(
                    _ => completeFolderWorkspaceTask(operationTask, operationCancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        private bool hasActiveFolderMutationHeld()
            => activeManagedFolderRenameTask is { IsCompleted: false }
               || activeManagedFolderStagedImportTask is { IsCompleted: false }
               || activeManagedFolderDeleteTask is { IsCompleted: false }
               || activeManagedFolderRecoveryTask is { IsCompleted: false }
               || activeFolderWorkspaceTask is { IsCompleted: false }
               || currentRevisionMutationAdmissionHeld
               || currentRevisionReloadAdmissionHeld
               || realmPackageMutationAdmissionDepth > 0
               || Volatile.Read(ref currentSkinProjectionInProgress) != 0;

        private static bool executeFolderWorkspaceOperation(
            Func<CancellationToken, bool> operation,
            CancellationToken cancellationToken)
        {
            try
            {
                return operation(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void completeFolderWorkspaceTask(
            Task<bool> operationTask,
            CancellationTokenSource operationCancellation)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (ReferenceEquals(activeFolderWorkspaceTask, operationTask))
                {
                    activeFolderWorkspaceTask = null;
                    activeFolderWorkspaceCancellation = null;
                }
            }

            operationCancellation.Dispose();
            notifyManagedFolderJournalStateChanged();
        }

        private bool registerExternalFolder(string selectedDirectory, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var candidate = new SkinInfo(
                instantiationInfo: SkinManagedFolderFactory.ALLOWED_INSTANTIATION_INFO)
            {
                FilesystemStoragePath = selectedDirectory,
                IsExternalFilesystemStorage = true,
                FilesystemStorageAuthorityOwner = SkinExternalFolderRegistry.AUTHORITY_OWNER,
            };
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(candidate, storage);

            if (resolution.Authority != SkinFilesystemStorageAuthority.ExternalFolder
                || resolution.ExternalCaptureRequest == null
                || resolution.NormalisedAbsolutePath == null)
            {
                return false;
            }

            using SkinManagedFolderOperationCoordinator.Lease operationLease =
                ManagedFolderOperationCoordinator.EnterMutation(cancellationToken);

            if (ManagedFolderOperationCoordinator.IsMutationBlocked)
                return false;

            using ISkinManagedFolderMutationNativeSession managedAuthority =
                managedFolderMutationNativeAuthority.Open(cancellationToken);
            SkinExternalFolderRegistryCaptureResult registryCapture = externalFolderRegistry.CaptureExactSet(
                operationLease,
                new[] { managedAuthority.ManagedRootAncestryProof },
                cancellationToken);

            if (!registryCapture.IsSuccess)
                return false;

            using SkinExternalFolderRegistrySnapshot registrySnapshot = registryCapture.Snapshot!;

            // A second registration of the same exact committed declaration converges idempotently.
            if (registrySnapshot.ContainsNormalisedPath(resolution.NormalisedAbsolutePath))
                return registrySnapshot.Validate(operationLease, cancellationToken);

            SkinExternalPackageCaptureResult packageCapture = externalFolderCaptureService.CaptureHeld(
                resolution.ExternalCaptureRequest,
                cancellationToken: cancellationToken);

            if (!packageCapture.IsSuccess)
                return false;

            using ISkinExternalPackageCaptureSession packageSession = packageCapture.Session!;

            if (packageSession.PhysicalProof.Overlaps(managedAuthority.ManagedRootAncestryProof)
                || registrySnapshot.Overlaps(packageSession.PhysicalProof)
                || !registrySnapshot.Validate(operationLease, cancellationToken))
            {
                return false;
            }

            SkinPackageRevisionCapsule capsule = packageSession.TakeCapsule();

            if (!SkinManagedFolderPackageMetadataReader.TryRead(
                    capsule,
                    out SkinManagedFolderPackageMetadata metadata))
            {
                capsule.Dispose();
                return false;
            }

            candidate.FilesystemStoragePath = resolution.NormalisedAbsolutePath;
            candidate.Name = metadata!.Name;
            candidate.Creator = metadata.Creator;
            candidate.Hash = metadata.ContentRevision;

            SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(candidate, this, capsule);

            if (!factory.IsSuccess)
                return false;

            factory.Skin!.Dispose();
            packageSession.Validate(cancellationToken);
            managedAuthority.ValidateCompleteAndStable(cancellationToken);

            if (!registrySnapshot.Validate(operationLease, cancellationToken))
                return false;

            bool published = Realm.Write(r =>
            {
                r.Refresh();

                if (!registrySnapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>())
                    || r.Find<SkinInfo>(candidate.ID) != null
                    || r.All<SkinInfo>().AsEnumerable().Any(existing =>
                        existing.IsExternalFilesystemStorage
                        && pathsOverlap(
                            existing.FilesystemStoragePath,
                            candidate.FilesystemStoragePath)))
                {
                    return false;
                }

                r.Add(candidate);
                SkinInfo committed = r.Find<SkinInfo>(candidate.ID);
                return committed != null
                       && isExactExternalRegistryRecord(committed)
                       && folderRecordMatches(candidate, committed);
            });

            if (!published)
                return false;

            Interlocked.Increment(ref selectionGeneration);
            cancelPendingSelection();
            return true;
        }

        private bool importManagedCopy(
            Guid externalRecordId,
            string targetChildName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!SkinManagedFolderPath.TryCreateFromChildName(targetChildName, out string targetManagedPath))
                return false;

            Guid operationId = Guid.NewGuid();
            SkinInfo externalRecord = Realm.Run(r =>
            {
                r.Refresh();
                SkinInfo current = r.Find<SkinInfo>(externalRecordId);
                return current != null && isExactExternalRegistryRecord(current)
                    ? current.Detach()
                    : null;
            });

            if (externalRecord == null)
                return false;

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(externalRecord, storage);

            if (resolution.Authority != SkinFilesystemStorageAuthority.ExternalFolder
                || resolution.ExternalCaptureRequest == null)
            {
                return false;
            }

            string externalRecordFingerprint =
                SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(externalRecord);
            bool firstDestinationWrite = false;
            SkinManagedFolderMutationJournal journal = null;

            using SkinManagedFolderOperationCoordinator.Lease operationLease =
                ManagedFolderOperationCoordinator.EnterStagedImport(cancellationToken);

            if (ManagedFolderOperationCoordinator.IsMutationBlocked
                || hasManagedCopyRealmConflict(operationId, targetManagedPath))
            {
                return false;
            }

            using ISkinManagedFolderMutationNativeSession managedAuthority =
                managedFolderMutationNativeAuthority.Open(cancellationToken);
            SkinExternalFolderRegistryCaptureResult registryCapture = externalFolderRegistry.CaptureExactSet(
                operationLease,
                new[] { managedAuthority.ManagedRootAncestryProof },
                cancellationToken);

            if (!registryCapture.IsSuccess)
                return false;

            using SkinExternalFolderRegistrySnapshot registrySnapshot = registryCapture.Snapshot!;

            if (!registrySnapshot.ContainsRecordId(externalRecordId)
                || !registrySnapshot.TryGetPhysicalProof(
                    externalRecordId,
                    out SkinFolderPhysicalAncestryProof registeredSourceProof)
                || registeredSourceProof == null)
            {
                return false;
            }

            SkinExternalPackageCaptureResult sourceCapture = externalFolderCaptureService.CaptureHeld(
                resolution.ExternalCaptureRequest,
                cancellationToken: cancellationToken);

            if (!sourceCapture.IsSuccess)
                return false;

            using ISkinExternalPackageCaptureSession sourceSession = sourceCapture.Session!;

            if (!string.Equals(
                    registeredSourceProof.Digest,
                    sourceSession.PhysicalProof.Digest,
                    StringComparison.Ordinal)
                || !registrySnapshot.Validate(operationLease, cancellationToken))
            {
                return false;
            }

            SkinPackageRevisionCapsule capturedCapsule = sourceSession.TakeCapsule();
            SkinPackageRevisionCapsule workingCapsule = null;

            try
            {
                SkinPackageRevisionCapsuleCreationResult clone = cloneManagedCopyCapsule(
                    capturedCapsule,
                    sourceSession.LogicalManifest,
                    cancellationToken);

                if (!clone.IsSuccess)
                {
                    capturedCapsule.Dispose();
                    return false;
                }

                workingCapsule = clone.Capsule!;

                if (!SkinManagedFolderPackageMetadataReader.TryRead(
                        capturedCapsule,
                        out SkinManagedFolderPackageMetadata sourceMetadata)
                    || !string.Equals(
                        sourceMetadata!.ContentRevision,
                        workingCapsule.ContentRevision,
                        StringComparison.Ordinal))
                {
                    capturedCapsule.Dispose();
                    return false;
                }

                SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(
                    externalRecord,
                    this,
                    capturedCapsule);
                capturedCapsule = null;

                if (!factory.IsSuccess)
                    return false;

                factory.Skin!.Dispose();
                SkinManagedCopyLogicalManifest logicalManifest =
                    SkinManagedCopyLogicalManifest.Create(sourceSession.LogicalManifest);
                SkinManagedFolderTargetNameSlot targetSlot = managedAuthority.CaptureAbsentTargetNameSlot(
                    targetManagedPath,
                    cancellationToken);
                SkinManagedFolderPhysicalIdentity stagedRootIdentity =
                    managedAuthority.PrepareManagedCopyStaging(operationId, cancellationToken);
                var externalBinding = new SkinExternalRegistryJournalBinding(
                    registrySnapshot.ExternalRegistryGeneration,
                    registrySnapshot.ExternalRegistryDigest,
                    registrySnapshot.IsEmpty
                        ? SkinExternalCollisionDisposition.NoRegisteredExternalFolders
                        : SkinExternalCollisionDisposition.ExactRegisteredExternalSet);

                if (targetSlot.ManagedRootIdentity != managedAuthority.ManagedRootIdentity
                    || stagedRootIdentity.VolumeSerialNumber != managedAuthority.ManagedRootIdentity.VolumeSerialNumber
                    || !validateManagedCopyAuthority(
                        externalRecord,
                        externalRecordFingerprint,
                        operationId,
                        targetManagedPath,
                        operationLease,
                        managedAuthority,
                        registrySnapshot,
                        sourceSession,
                        CancellationToken.None))
                {
                    return false;
                }

                journal = SkinManagedFolderMutationJournal.CreatePreparedManagedCopy(
                    operationId,
                    externalRecordId,
                    managedAuthority.ManagedRootIdentity,
                    targetManagedPath,
                    stagedRootIdentity,
                    workingCapsule.ContentRevision,
                    externalRecordFingerprint,
                    sourceSession.CaptureFingerprint,
                    logicalManifest,
                    externalBinding);
                persistManagedCopyJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);

                SkinManagedFolderPhysicalIdentity provisionalRootIdentity =
                    managedAuthority.CreateManagedCopyProvisionalRoot(
                        operationId,
                        cancellationToken);
                journal = journal.WithCopying(provisionalRootIdentity);
                persistManagedCopyJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);

                try
                {
                    managedAuthority.WriteManagedCopyProvisional(
                        operationId,
                        workingCapsule,
                        logicalManifest,
                        () =>
                        {
                            SkinManagedFolderMutationJournalLoadResult loaded =
                                managedFolderMutationJournalStore.Load();

                            if (!loaded.IsLoaded
                                || !loaded.Journal!.IsExactSameJournal(journal)
                                || !registrySnapshot.Validate(operationLease, CancellationToken.None)
                                || !validateManagedCopyDurableRealmState(
                                    journal,
                                    externalRecord,
                                    externalRecordFingerprint,
                                    operationId,
                                    targetManagedPath))
                            {
                                throw new SkinManagedFolderMutationJournalException();
                            }

                            sourceSession.Validate(CancellationToken.None);
                            firstDestinationWrite = true;
                        },
                        cancellationToken);
                }
                catch
                {
                    if (!firstDestinationWrite
                        && tryRollbackManagedCopyBeforeFirstWrite(
                            journal,
                            externalRecord,
                            externalRecordFingerprint,
                            operationId,
                            targetManagedPath,
                            operationLease,
                            managedAuthority,
                            registrySnapshot,
                            sourceSession))
                    {
                        journal = null;
                    }

                    throw;
                }

                // The durable intent owns every subsequent outcome. Caller cancellation can no longer roll it back.
                using SkinManagedFolderStagedSourceCapture provisional =
                    managedAuthority.CaptureStagedSource(operationId, CancellationToken.None);

                if (!provisional.IsUsableFor(managedAuthority.ManagedRootIdentity)
                    || provisional.LogicalManifest == null
                    || !logicalManifest.Matches(provisional.LogicalManifest)
                    || !string.Equals(
                        provisional.Capsule.ContentRevision,
                        workingCapsule.ContentRevision,
                        StringComparison.Ordinal)
                    || !SkinManagedFolderPackageMetadataReader.TryRead(
                        provisional.Capsule,
                        out SkinManagedFolderPackageMetadata provisionalMetadata))
                {
                    throw new InvalidOperationException("The managed-copy provisional package changed.");
                }

                var publicationPlan = new SkinManagedFolderNewRecordPublicationPlan(
                    operationId,
                    targetManagedPath,
                    managedAuthority.ManagedRootIdentity);
                SkinManagedFolderNewRecordPublicationData publication =
                    publicationPlan.CreatePublicationData(provisionalMetadata!);
                journal = journal.WithProvisionalReady(
                    provisional.SourceIdentity,
                    provisional.TreeFingerprint,
                    publication.Fingerprint);
                persistManagedCopyJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);

                using SkinManagedFolderStagedImportFilesystemResult moved =
                    managedAuthority.MoveCapturedStagedSourceToTarget(
                        targetSlot,
                        provisionalMetadata.ContentRevision,
                        provisional.TreeFingerprint,
                        CancellationToken.None);

                if (moved.TargetIdentity != provisional.SourceIdentity
                    || !string.Equals(moved.TreeFingerprint, provisional.TreeFingerprint, StringComparison.Ordinal)
                    || !string.Equals(moved.Capsule.ContentRevision, provisionalMetadata.ContentRevision, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The managed-copy move changed the captured package.");
                }

                journal = journal.WithFilesystemApplied(
                    moved.TargetIdentity,
                    publication.Fingerprint);
                persistManagedCopyJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);

                if (!publishManagedCopyRecord(
                        publication,
                        externalRecord,
                        externalRecordFingerprint,
                        operationId,
                        targetManagedPath,
                        operationLease,
                        managedAuthority,
                        registrySnapshot,
                        sourceSession))
                {
                    throw new InvalidOperationException("The managed-copy Realm publication was rejected.");
                }

                journal = journal.WithRealmApplied();
                persistManagedCopyJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);
                journal = journal.WithCommitted();
                persistManagedCopyJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);
                deleteManagedCopyTerminalJournalExact(
                    journal,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);
                journal = null;
                Interlocked.Increment(ref selectionGeneration);
                cancelPendingSelection();
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested && !firstDestinationWrite)
            {
                if (journal != null
                    && journal.Phase == SkinManagedFolderMutationPhase.Prepared
                    && tryRollbackManagedCopyBeforeFirstWrite(
                        journal,
                        externalRecord,
                        externalRecordFingerprint,
                        operationId,
                        targetManagedPath,
                        operationLease,
                        managedAuthority,
                        registrySnapshot,
                        sourceSession))
                {
                    journal = null;
                }
                else if (journal != null)
                    ManagedFolderOperationCoordinator.FreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());

                return false;
            }
            catch
            {
                if (journal != null)
                    ManagedFolderOperationCoordinator.FreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());

                return false;
            }
            finally
            {
                capturedCapsule?.Dispose();
                workingCapsule?.Dispose();
            }
        }

        private SkinPackageRevisionCapsuleCreationResult cloneManagedCopyCapsule(
            SkinPackageRevisionCapsule source,
            SkinExternalPackageLogicalManifest manifest,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(manifest);
            using IResourceStore<byte[]> resources = source.CreateResourceView();
            SkinPackageCapturedEntry[] entries = manifest.Entries.Select(entry =>
            {
                if (entry.Kind == SkinExternalPackageLogicalEntryKind.Directory)
                    return SkinPackageCapturedEntry.CreateDirectory(entry.RelativePath);

                return SkinPackageCapturedEntry.CreateFile(
                    entry.RelativePath,
                    entry.Length,
                    () => resources.GetStream(entry.RelativePath));
            }).ToArray();
            return SkinPackageRevisionCapsuleFactory.Create(entries, cancellationToken: cancellationToken);
        }

        private bool validateManagedCopyAuthority(
            SkinInfo externalRecord,
            string externalRecordFingerprint,
            Guid operationId,
            string targetManagedPath,
            SkinManagedFolderOperationCoordinator.Lease operationLease,
            ISkinManagedFolderMutationNativeSession managedAuthority,
            SkinExternalFolderRegistrySnapshot registrySnapshot,
            ISkinExternalPackageCaptureSession sourceSession,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                managedAuthority.ValidateCompleteAndStable(cancellationToken);
                sourceSession.Validate(cancellationToken);

                if (!registrySnapshot.Validate(operationLease, cancellationToken)
                    || hasManagedCopyRealmConflict(operationId, targetManagedPath))
                {
                    return false;
                }

                return Realm.Run(r =>
                {
                    r.Refresh();
                    SkinInfo current = r.Find<SkinInfo>(externalRecord.ID);
                    return current != null
                           && folderRecordMatches(externalRecord, current)
                           && string.Equals(
                               SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(current),
                               externalRecordFingerprint,
                               StringComparison.Ordinal)
                           && registrySnapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>());
                });
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

        private void persistManagedCopyJournalExact(
            SkinManagedFolderMutationJournal journal,
            SkinInfo externalRecord,
            string externalRecordFingerprint,
            Guid operationId,
            string targetManagedPath,
            SkinManagedFolderOperationCoordinator.Lease operationLease,
            ISkinManagedFolderMutationNativeSession managedAuthority,
            SkinExternalFolderRegistrySnapshot registrySnapshot,
            ISkinExternalPackageCaptureSession sourceSession)
        {
            ArgumentNullException.ThrowIfNull(journal);

            try
            {
                managedFolderMutationJournalStore.Write(journal);
                SkinManagedFolderMutationJournalLoadResult loaded = managedFolderMutationJournalStore.Load();

                if (!loaded.IsLoaded
                    || !loaded.Journal!.IsExactSameJournal(journal)
                    || !validateManagedCopyDurableRealmState(
                        journal,
                        externalRecord,
                        externalRecordFingerprint,
                        operationId,
                        targetManagedPath)
                    || !validateManagedCopyHeldAuthority(
                        operationLease,
                        managedAuthority,
                        registrySnapshot,
                        sourceSession))
                {
                    throw new SkinManagedFolderMutationJournalException();
                }
            }
            catch
            {
                ManagedFolderOperationCoordinator.FreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());
                throw;
            }
        }

        private bool validateManagedCopyHeldAuthority(
            SkinManagedFolderOperationCoordinator.Lease operationLease,
            ISkinManagedFolderMutationNativeSession managedAuthority,
            SkinExternalFolderRegistrySnapshot registrySnapshot,
            ISkinExternalPackageCaptureSession sourceSession)
        {
            try
            {
                managedAuthority.ValidateCompleteAndStable(CancellationToken.None);
                sourceSession.Validate(CancellationToken.None);
                return registrySnapshot.Validate(operationLease, CancellationToken.None);
            }
            catch
            {
                return false;
            }
        }

        private void deleteManagedCopyTerminalJournalExact(
            SkinManagedFolderMutationJournal terminalJournal,
            SkinInfo externalRecord,
            string externalRecordFingerprint,
            Guid operationId,
            string targetManagedPath,
            SkinManagedFolderOperationCoordinator.Lease operationLease,
            ISkinManagedFolderMutationNativeSession managedAuthority,
            SkinExternalFolderRegistrySnapshot registrySnapshot,
            ISkinExternalPackageCaptureSession sourceSession)
        {
            ArgumentNullException.ThrowIfNull(terminalJournal);

            try
            {
                if (terminalJournal.Phase is not (SkinManagedFolderMutationPhase.Committed
                    or SkinManagedFolderMutationPhase.RolledBack))
                {
                    throw new SkinManagedFolderMutationJournalException();
                }

                if (!validateManagedCopyDurableRealmState(
                        terminalJournal,
                        externalRecord,
                        externalRecordFingerprint,
                        operationId,
                        targetManagedPath)
                    || !validateManagedCopyHeldAuthority(
                        operationLease,
                        managedAuthority,
                        registrySnapshot,
                        sourceSession))
                {
                    throw new SkinManagedFolderMutationJournalException();
                }

                managedFolderMutationJournalStore.Delete(terminalJournal);
                SkinManagedFolderMutationJournalLoadResult loaded = managedFolderMutationJournalStore.Load();

                if (loaded.Status != SkinManagedFolderMutationJournalLoadStatus.Missing)
                    throw new SkinManagedFolderMutationJournalException();
            }
            catch
            {
                ManagedFolderOperationCoordinator.FreezeRecoveryPaths(
                    terminalJournal.GetAffectedManagedRelativePaths());
                throw;
            }
        }

        private bool validateManagedCopyDurableRealmState(
            SkinManagedFolderMutationJournal journal,
            SkinInfo externalRecord,
            string externalRecordFingerprint,
            Guid operationId,
            string targetManagedPath)
            => Realm.Run(r =>
            {
                r.Refresh();
                SkinInfo currentExternal = r.Find<SkinInfo>(externalRecord.ID);

                if (currentExternal == null
                    || !folderRecordMatches(externalRecord, currentExternal)
                    || !string.Equals(
                        SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(currentExternal),
                        externalRecordFingerprint,
                        StringComparison.Ordinal)
                    || r.All<SkinInfo>().AsEnumerable().Any(candidate =>
                        candidate.ID != operationId
                        && SkinManagedFolderPath.TryNormalise(
                            candidate.FilesystemStoragePath,
                            out string candidatePath)
                        && string.Equals(candidatePath, targetManagedPath, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                SkinInfo published = r.Find<SkinInfo>(operationId);
                bool shouldBePublished = journal.Phase is SkinManagedFolderMutationPhase.RealmApplied
                    or SkinManagedFolderMutationPhase.Committed;

                if (shouldBePublished)
                {
                    return published != null
                           && string.Equals(
                               SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(published),
                               journal.NewRecordPublicationFingerprint,
                               StringComparison.Ordinal);
                }

                return published == null;
            });

        private bool tryRollbackManagedCopyBeforeFirstWrite(
            SkinManagedFolderMutationJournal journal,
            SkinInfo externalRecord,
            string externalRecordFingerprint,
            Guid operationId,
            string targetManagedPath,
            SkinManagedFolderOperationCoordinator.Lease operationLease,
            ISkinManagedFolderMutationNativeSession managedAuthority,
            SkinExternalFolderRegistrySnapshot registrySnapshot,
            ISkinExternalPackageCaptureSession sourceSession)
        {
            try
            {
                managedAuthority.ValidateCompleteAndStable(CancellationToken.None);

                if (!managedAuthority.IsManagedCopyProvisionalAbsent(
                        operationId,
                        CancellationToken.None))
                {
                    return false;
                }

                SkinManagedFolderMutationJournal rolledBack = journal.WithRolledBack();
                persistManagedCopyJournalExact(
                    rolledBack,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);
                deleteManagedCopyTerminalJournalExact(
                    rolledBack,
                    externalRecord,
                    externalRecordFingerprint,
                    operationId,
                    targetManagedPath,
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool publishManagedCopyRecord(
            SkinManagedFolderNewRecordPublicationData publication,
            SkinInfo externalRecord,
            string externalRecordFingerprint,
            Guid operationId,
            string targetManagedPath,
            SkinManagedFolderOperationCoordinator.Lease operationLease,
            ISkinManagedFolderMutationNativeSession managedAuthority,
            SkinExternalFolderRegistrySnapshot registrySnapshot,
            ISkinExternalPackageCaptureSession sourceSession)
        {
            if (!validateManagedCopyHeldAuthority(
                    operationLease,
                    managedAuthority,
                    registrySnapshot,
                    sourceSession))
            {
                return false;
            }

            bool added = Realm.Write(r =>
            {
                r.Refresh();
                SkinInfo currentExternal = r.Find<SkinInfo>(externalRecord.ID);

                if (currentExternal == null
                    || !folderRecordMatches(externalRecord, currentExternal)
                    || !string.Equals(
                        SkinManagedFolderNewRecordPublicationData.ComputeRecordFingerprint(currentExternal),
                        externalRecordFingerprint,
                        StringComparison.Ordinal)
                    || !registrySnapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>())
                    || r.Find<SkinInfo>(operationId) != null
                    || r.All<SkinInfo>().AsEnumerable().Any(candidate =>
                        candidate.ID != operationId
                        && SkinManagedFolderPath.TryNormalise(
                            candidate.FilesystemStoragePath,
                            out string candidatePath)
                        && string.Equals(candidatePath, targetManagedPath, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }

                SkinInfo record = publication.CreateRecord();
                r.Add(record);
                return publication.IsExactRecord(record);
            });

            return added
                   && validateManagedCopyHeldAuthority(
                       operationLease,
                       managedAuthority,
                       registrySnapshot,
                       sourceSession)
                   && Realm.Run(r =>
                   {
                       r.Refresh();
                       SkinInfo record = r.Find<SkinInfo>(operationId);
                       return record != null
                              && publication.IsExactRecord(record)
                              && registrySnapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>());
                   });
        }

        private bool hasManagedCopyRealmConflict(Guid operationId, string targetManagedPath)
            => Realm.Run(r =>
            {
                r.Refresh();
                return r.Find<SkinInfo>(operationId) != null
                       || r.All<SkinInfo>().AsEnumerable().Any(candidate =>
                           SkinManagedFolderPath.TryNormalise(
                               candidate.FilesystemStoragePath,
                               out string candidatePath)
                           && string.Equals(candidatePath, targetManagedPath, StringComparison.OrdinalIgnoreCase));
            });

        private bool unregisterExternalFolderOnUpdateThread(Guid recordId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Guid currentInfoId = CurrentSkinInfo.Value.ID;
            Guid currentSkinId = CurrentSkin.Value.SkinInfo.ID;

            if (currentInfoId != currentSkinId
                || currentInfoId == recordId
                || currentSkinId == recordId
                || !externalFolderRegistry.TryReadAndValidateDeclarations(
                    out SkinExternalFolderRegistryDeclaration[] declarations,
                    out _,
                    out _,
                    out _)
                || declarations.All(declaration => declaration.RecordId != recordId))
            {
                return false;
            }

            try
            {
                return Realm.Write(r =>
                {
                    r.Refresh();

                    if (CurrentSkinInfo.Value.ID != currentInfoId
                        || CurrentSkin.Value.SkinInfo.ID != currentSkinId
                        || currentInfoId != currentSkinId
                        || !exactlyMatchesExternalDeclarations(r.All<SkinInfo>(), declarations))
                    {
                        return false;
                    }

                    SkinInfo target = r.Find<SkinInfo>(recordId);

                    if (target == null || !isExactExternalRegistryRecord(target))
                        return false;

                    r.Remove(target);
                    return r.Find<SkinInfo>(recordId) == null;
                });
            }
            catch
            {
                return false;
            }
        }

        private bool openFolder(Guid recordId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkinInfo snapshot = Realm.Run(r =>
            {
                r.Refresh();
                SkinInfo current = r.Find<SkinInfo>(recordId);
                return current != null && isExactFolderWorkspaceRecord(r.All<SkinInfo>(), current)
                    ? current.Detach()
                    : null;
            });

            if (snapshot == null)
                return false;

            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(snapshot, storage);

            if (!resolution.IsFilesystemBacked || resolution.NormalisedAbsolutePath == null)
                return false;

            if (!snapshot.IsExternalFilesystemStorage
                && (snapshot.FilesystemStoragePath == null
                    || ManagedFolderOperationCoordinator.IsPathFrozen(snapshot.FilesystemStoragePath)))
            {
                return false;
            }

            using SkinManagedFolderOperationCoordinator.Lease operationLease =
                ManagedFolderOperationCoordinator.Enter(cancellationToken);
            using ISkinManagedFolderMutationNativeSession managedAuthority =
                managedFolderMutationNativeAuthority.Open(cancellationToken);
            SkinExternalFolderRegistryCaptureResult registryCapture = externalFolderRegistry.CaptureExactSet(
                operationLease,
                new[] { managedAuthority.ManagedRootAncestryProof },
                cancellationToken);

            if (!registryCapture.IsSuccess)
                return false;

            using SkinExternalFolderRegistrySnapshot registrySnapshot = registryCapture.Snapshot!;

            if (snapshot.IsExternalFilesystemStorage)
            {
                if (resolution.ExternalCaptureRequest == null
                    || !registrySnapshot.ContainsRecordId(recordId))
                {
                    return false;
                }

                SkinExternalFolderAuthorityCaptureResult externalCapture = externalFolderCaptureService.OpenAuthority(
                    resolution.ExternalCaptureRequest,
                    cancellationToken: cancellationToken);

                if (!externalCapture.IsSuccess)
                    return false;

                using ISkinExternalFolderAuthoritySession externalAuthority = externalCapture.Session!;

                if (!registrySnapshot.TryGetPhysicalProof(recordId, out SkinFolderPhysicalAncestryProof registeredProof)
                    || registeredProof == null
                    || !string.Equals(registeredProof.Digest, externalAuthority.PhysicalProof.Digest, StringComparison.Ordinal))
                {
                    return false;
                }

                externalAuthority.Validate(cancellationToken);
            }
            else
            {
                if (resolution.ManagedCaptureRequest == null)
                    return false;

                managedAuthority.CaptureExistingSource(snapshot.FilesystemStoragePath!, cancellationToken);
                managedAuthority.ValidateCompleteAndStable(cancellationToken);
            }

            bool recordStillMatches = Realm.Run(r =>
            {
                r.Refresh();
                SkinInfo current = r.Find<SkinInfo>(recordId);
                return current != null
                       && folderRecordMatches(snapshot, current)
                       && isExactFolderWorkspaceRecord(r.All<SkinInfo>(), current);
            });

            if (!recordStillMatches
                || (!snapshot.IsExternalFilesystemStorage
                    && ManagedFolderOperationCoordinator.IsPathFrozen(snapshot.FilesystemStoragePath!))
                || !registrySnapshot.Validate(operationLease, cancellationToken))
                return false;

            OpenFolderExternally(resolution.NormalisedAbsolutePath + Path.DirectorySeparatorChar);
            return true;
        }

        private static bool exactlyMatchesExternalDeclarations(
            IEnumerable<SkinInfo> records,
            IReadOnlyList<SkinExternalFolderRegistryDeclaration> declarations)
        {
            SkinInfo[] external = records.Where(record => record.IsExternalFilesystemStorage)
                                         .OrderBy(record => record.ID.ToString("N"), StringComparer.Ordinal)
                                         .ToArray();

            if (external.Length != declarations.Count)
                return false;

            for (int i = 0; i < external.Length; i++)
            {
                SkinInfo record = external[i];
                SkinExternalFolderRegistryDeclaration declaration = declarations[i];

                if (record.ID != declaration.RecordId
                    || !isExactExternalRegistryRecord(record)
                    || !string.Equals(record.FilesystemStoragePath, declaration.DeclaredPath, StringComparison.Ordinal)
                    || !string.Equals(record.FilesystemStorageAuthorityOwner, declaration.AuthorityOwner, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool isExactExternalRegistryRecord(SkinInfo record)
            => record.IsManaged
               && record.IsExternalFilesystemStorage
               && !record.Protected
               && !record.DeletePending
               && record.Files.Count == 0
               && !SkinFilesystemStorageResolver.IsFixedSkinId(record.ID)
               && !string.IsNullOrEmpty(record.FilesystemStoragePath)
               && string.Equals(
                   record.FilesystemStorageAuthorityOwner,
                   SkinExternalFolderRegistry.AUTHORITY_OWNER,
                   StringComparison.Ordinal)
               && SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
               && !string.IsNullOrEmpty(record.Hash);

        private static bool isExactFolderWorkspaceRecord(IEnumerable<SkinInfo> records, SkinInfo record)
        {
            if (record.IsExternalFilesystemStorage)
                return isExactExternalRegistryRecord(record);

            if (!record.IsManaged
                || record.Protected
                || record.DeletePending
                || record.Files.Count != 0
                || SkinFilesystemStorageResolver.IsFixedSkinId(record.ID)
                || !string.Equals(
                    record.FilesystemStorageAuthorityOwner,
                    SkinManagedFolderScanner.AUTHORITY_OWNER,
                    StringComparison.Ordinal)
                || !SkinManagedFolderFactory.IsInstantiationInfoAllowed(record.InstantiationInfo)
                || string.IsNullOrEmpty(record.Hash)
                || !SkinManagedFolderPath.TryNormalise(record.FilesystemStoragePath, out string normalisedPath)
                || !string.Equals(record.FilesystemStoragePath, normalisedPath, StringComparison.Ordinal))
            {
                return false;
            }

            return records.Count(candidate =>
                SkinManagedFolderPath.TryNormalise(candidate.FilesystemStoragePath, out string candidatePath)
                && string.Equals(candidatePath, normalisedPath, StringComparison.OrdinalIgnoreCase)) == 1;
        }

        private static bool folderRecordMatches(SkinInfo expected, SkinInfo current)
            => expected.ID == current.ID
               && string.Equals(expected.Name, current.Name, StringComparison.Ordinal)
               && string.Equals(expected.Creator, current.Creator, StringComparison.Ordinal)
               && string.Equals(expected.InstantiationInfo, current.InstantiationInfo, StringComparison.Ordinal)
               && string.Equals(expected.Hash, current.Hash, StringComparison.Ordinal)
               && expected.Protected == current.Protected
               && expected.DeletePending == current.DeletePending
               && string.Equals(expected.FilesystemStoragePath, current.FilesystemStoragePath, StringComparison.Ordinal)
               && expected.IsExternalFilesystemStorage == current.IsExternalFilesystemStorage
               && string.Equals(
                   expected.FilesystemStorageAuthorityOwner,
                   current.FilesystemStorageAuthorityOwner,
                   StringComparison.Ordinal)
               && current.Files.Count == 0;

        private static bool pathsOverlap(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;

            try
            {
                string normalisedLeft = Path.TrimEndingDirectorySeparator(Path.GetFullPath(left));
                string normalisedRight = Path.TrimEndingDirectorySeparator(Path.GetFullPath(right));

                return string.Equals(normalisedLeft, normalisedRight, StringComparison.OrdinalIgnoreCase)
                       || isStrictChildPath(normalisedLeft, normalisedRight)
                       || isStrictChildPath(normalisedRight, normalisedLeft);
            }
            catch
            {
                return true;
            }
        }

        private static bool isStrictChildPath(string candidate, string root)
        {
            string prefix = Path.EndsInDirectorySeparator(root)
                ? root
                : root + Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

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
                    || activeManagedFolderDeleteTask is { IsCompleted: false }
                    || activeManagedFolderRecoveryTask is { IsCompleted: false }
                    || activeFolderWorkspaceTask is { IsCompleted: false }
                    || currentRevisionMutationAdmissionHeld
                    || currentRevisionReloadAdmissionHeld
                    || realmPackageMutationAdmissionDepth > 0)
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
                    || activeManagedFolderDeleteTask is { IsCompleted: false }
                    || activeManagedFolderRecoveryTask is { IsCompleted: false }
                    || activeFolderWorkspaceTask is { IsCompleted: false }
                    || currentRevisionMutationAdmissionHeld
                    || currentRevisionReloadAdmissionHeld
                    || realmPackageMutationAdmissionDepth > 0)
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
                    || activeManagedFolderDeleteTask is { IsCompleted: false }
                    || activeManagedFolderRecoveryTask is { IsCompleted: false }
                    || activeFolderWorkspaceTask is { IsCompleted: false }
                    || currentRevisionMutationAdmissionHeld
                    || currentRevisionReloadAdmissionHeld
                    || realmPackageMutationAdmissionDepth > 0)
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
            Task currentRevisionReloadTask = null;

            CancellationTokenSource renameCancellation;
            CancellationTokenSource importCancellation;
            CancellationTokenSource deleteCancellation;
            CancellationTokenSource recoveryCancellation;
            CancellationTokenSource workspaceCancellation;
            CancellationTokenSource[] currentReloadCancellations;
            FolderWorkspaceReadOperation[] workspaceReadOperations;
            Task<SkinManagedFolderRenameOperationResult> renameTask;
            Task<SkinManagedFolderStagedImportOperationResult> importTask;
            Task<SkinManagedFolderDeleteOperationResult> deleteTask;
            Task<bool> recoveryTask;
            Task<bool> workspaceTask;
            PendingManagedFolderDeleteFallback pendingDeleteFallback;

            lock (managedFolderRenameLifecycleGate)
            {
                managedFolderMutationShutdown = true;
                renameCancellation = activeManagedFolderRenameCancellation;
                importCancellation = activeManagedFolderStagedImportCancellation;
                deleteCancellation = activeManagedFolderDeleteCancellation;
                recoveryCancellation = activeManagedFolderRecoveryCancellation;
                workspaceCancellation = activeFolderWorkspaceCancellation;
                workspaceReadOperations = folderWorkspaceReadOperations.ToArray();
                renameTask = activeManagedFolderRenameTask;
                importTask = activeManagedFolderStagedImportTask;
                deleteTask = activeManagedFolderDeleteTask;
                recoveryTask = activeManagedFolderRecoveryTask;
                workspaceTask = activeFolderWorkspaceTask;
                pendingDeleteFallback = pendingManagedFolderDeleteFallback;
            }

            lock (currentRevisionReloadGate)
                currentReloadCancellations = currentRevisionReloadWorkerCancellations.ToArray();

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

            try
            {
                recoveryCancellation?.Cancel();
            }
            catch
            {
                // Cancellation callback failures must not bypass the recovery join below.
            }

            try
            {
                workspaceCancellation?.Cancel();
            }
            catch
            {
                // Cancellation callback failures must not bypass the workspace join below.
            }

            foreach (CancellationTokenSource currentReloadCancellation in currentReloadCancellations)
            {
                try
                {
                    currentReloadCancellation.Cancel();
                }
                catch
                {
                    // The publication shutdown join below still claims every tracked worker exactly once.
                }
            }

            foreach (FolderWorkspaceReadOperation operation in workspaceReadOperations)
            {
                try
                {
                    operation.Cancellation.Cancel();
                }
                catch
                {
                    // Cancellation callback failures must not bypass the read-worker joins below.
                }
            }

            cancelManagedFolderDeleteFallback(pendingDeleteFallback);

            if (!ThreadSafety.IsUpdateThread && workspaceTask is { IsCompleted: false })
            {
                // A SourceChanged observer can request shutdown from a background task while synchronously waiting on
                // the update thread. Returning that outer call and handing the exact join back to the update scheduler
                // breaks the otherwise unavoidable callback -> join -> callback cycle. Production disposal already
                // enters on the update thread and therefore remains a synchronous exact join.
                try
                {
                    scheduler.Add(ShutdownManagedFolderMutations);
                }
                catch
                {
                    // A later owner/test teardown call can repeat the idempotent join if the scheduler is already gone.
                }

                return;
            }

            try
            {
                joinTaskPumpingCurrentRevisionCallbacks(renameTask);
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            try
            {
                joinTaskPumpingCurrentRevisionCallbacks(importTask);
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            try
            {
                joinTaskPumpingCurrentRevisionCallbacks(deleteTask);
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            try
            {
                joinTaskPumpingCurrentRevisionCallbacks(recoveryTask);
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            try
            {
                joinTaskPumpingCurrentRevisionCallbacks(workspaceTask);
            }
            catch
            {
                // Observe unexpected failures without exposing a potentially sensitive exception.
            }

            foreach (FolderWorkspaceReadOperation operation in workspaceReadOperations)
            {
                try
                {
                    operation.Task.GetAwaiter().GetResult();
                }
                catch
                {
                    // Observe cancellation and unexpected failures before Realm can be released.
                }
            }

            // Current mutations may need the publication scheduler to restore their exact pre-fallback pair after
            // cancellation. Claim publication only after every mutation/read worker has converged and joined.
            currentRevisionReloadTask = beginCurrentRevisionPublicationShutdown();

            try
            {
                currentRevisionReloadTask?.GetAwaiter().GetResult();
            }
            catch
            {
                // Publication preparation failures are reduced to a stable typed outcome.
            }

            try
            {
                Task.WhenAll(currentRevisionPublication.CaptureRevisionWorkDetachments()).GetAwaiter().GetResult();
            }
            catch
            {
                // Work leases release in task completion continuations. Observe unexpected failures without allowing
                // Realm teardown to overtake exact owner-touching work.
            }

            if (Interlocked.Exchange(ref currentRevisionManagerLeaseReleased, 1) == 0)
                currentRevisionPublication.Current.ReleaseManagerLease();

            drainCurrentRevisionRetireQueue();
        }

        private Task<bool> trackAdmittedCurrentRevisionMutation(
            Func<CancellationToken, Task<bool>> operation,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(operation);

            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown)
                {
                    currentRevisionMutationAdmissionHeld = false;
                    return Task.FromResult(false);
                }

                var operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<bool> operationTask;

                try
                {
                    // The exact preflight and first fallback publication boundary must begin on the update thread.
                    // The async operation yields before any detach wait or Realm/source mutation.
                    operationTask = operation(operationCancellation.Token);
                }
                catch
                {
                    currentRevisionMutationAdmissionHeld = false;
                    operationCancellation.Dispose();
                    return Task.FromResult(false);
                }

                activeFolderWorkspaceCancellation = operationCancellation;
                activeFolderWorkspaceTask = operationTask;

                _ = operationTask.ContinueWith(
                    _ => completeFolderWorkspaceTask(operationTask, operationCancellation),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);

                return operationTask;
            }
        }

        private void joinTaskPumpingCurrentRevisionCallbacks(Task task)
        {
            if (task == null)
                return;

            while (!task.IsCompleted)
            {
                pumpCurrentRevisionCallbacksForShutdown();

                if (task.IsCompleted)
                    break;

                try
                {
                    task.Wait(10);
                }
                catch
                {
                    break;
                }
            }

            pumpCurrentRevisionCallbacksForShutdown();
            task.GetAwaiter().GetResult();
        }

        private void pumpCurrentRevisionCallbacksForShutdown()
        {
            if (!ThreadSafety.IsUpdateThread)
                return;

            PendingCurrentRevisionCallback[] pendingCallbacks;

            lock (currentRevisionReloadGate)
            {
                pendingCallbacks = pendingCurrentRevisionCallbacks.ToArray();
                pendingCurrentRevisionCallbacks.Clear();
            }

            foreach (PendingCurrentRevisionCallback pending in pendingCallbacks)
                pending.Run();
        }

        private Task beginCurrentRevisionPublicationShutdown()
        {
            PendingCurrentRevisionCallback[] pendingCallbacks;
            Task<SkinCurrentRevisionReloadResult>[] reloadTasks;
            CancellationTokenSource[] reloadCancellations;

            lock (currentRevisionReloadGate)
            {
                if (Interlocked.Exchange(ref currentRevisionPublicationShutdown, 1) == 0)
                    Interlocked.Increment(ref currentRevisionReloadGeneration);

                reloadTasks = currentRevisionReloadWorkerTasks.ToArray();
                reloadCancellations = currentRevisionReloadWorkerCancellations.ToArray();
                pendingCallbacks = pendingCurrentRevisionCallbacks.ToArray();
                pendingCurrentRevisionCallbacks.Clear();
            }

            foreach (CancellationTokenSource reloadCancellation in reloadCancellations)
            {
                try
                {
                    reloadCancellation.Cancel();
                }
                catch
                {
                }
            }

            foreach (PendingCurrentRevisionCallback pending in pendingCallbacks)
                pending.Shutdown();

            // Stop new registrations and request each claimed real owner to cancel/reap hidden work. The publication
            // never impersonates visual detach or releases a work lease: the owner completion path does both, and the
            // WorkDetached join below remains the exact fence before Realm teardown.
            currentRevisionPublication.ShutdownAndClaimParticipants();

            return reloadTasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(reloadTasks);
        }

        private void shutdownManagedFolderSelections()
        {
            CancellationTokenSource pendingSelection = null;
            PendingManagedFolderSelectionCompletion[] pendingCompletions;
            PendingExternalFolderSelectionCompletion[] pendingExternalCompletions;
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
                pendingExternalCompletions = pendingExternalFolderSelectionCompletions.ToArray();
                pendingExternalFolderSelectionCompletions.Clear();
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

            foreach (PendingExternalFolderSelectionCompletion pendingCompletion in pendingExternalCompletions)
                discardExternalFolderSelectionCompletion(pendingCompletion);

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

        private bool executeManagedFolderRecoveryRetry(CancellationToken cancellationToken)
        {
            try
            {
                FolderSkinJournalSupportSnapshot before =
                    managedFolderMutationRecovery.InspectSupportSnapshot(cancellationToken);

                if (!before.CanRetry)
                    return false;

                SkinManagedFolderMutationRecoveryResult result =
                    managedFolderMutationRecovery.Recover(cancellationToken);

                return result.IsResolved;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch
            {
                // Recovery failures are represented by the next redacted support snapshot.
                return false;
            }
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
            notifyManagedFolderJournalStateChanged();
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
            notifyManagedFolderJournalStateChanged();
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
            notifyManagedFolderJournalStateChanged();
        }

        private void completeManagedFolderRecoveryTask(
            Task<bool> operationTask,
            CancellationTokenSource operationCancellation)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (ReferenceEquals(activeManagedFolderRecoveryTask, operationTask))
                {
                    activeManagedFolderRecoveryTask = null;
                    activeManagedFolderRecoveryCancellation = null;
                }
            }

            operationCancellation.Dispose();
            notifyManagedFolderJournalStateChanged();
        }

        private void notifyManagedFolderJournalStateChanged()
        {
            Action handlers = ManagedFolderJournalStateChanged;

            if (handlers == null)
                return;

            foreach (Action handler in handlers.GetInvocationList())
            {
                try
                {
                    handler();
                }
                catch
                {
                    // A support observer cannot affect worker completion or expose a worker exception.
                }
            }
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

            notifySourceChanged();
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

            // C1's synchronous callback may only observe that C2 already moved the pair away from the record. It must
            // never create a new owner -> CurrentSkin -> immediate-dispose transition of its own. The public current
            // delete path first publishes the protected revision, waits exact detach, then re-enters C1 as NotRequired.
            return SkinManagedFolderProtectedFallbackCommitResult.PairNotCommitted;
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
                                     .Where(s => !s.DeletePending && !s.Protected)
                                     .AsEnumerable()
                                     .Where(s => !s.IsExternalFilesystemStorage
                                                 || isExactExternalRegistryRecord(s))
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

            var skins = getImplicitlySelectableSkins();

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

        private IList<Live<SkinInfo>> getImplicitlySelectableSkins()
        {
            var skins = new List<Live<SkinInfo>>();

            Realm.Run(realm =>
            {
                skins.Add(realm.Find<SkinInfo>(SkinInfo.OMS_SKIN).ToLive(Realm));

                foreach (SkinInfo skin in realm.All<SkinInfo>()
                                              .Where(s => !s.DeletePending
                                                          && !s.Protected
                                                          && !s.IsExternalFilesystemStorage)
                                              .AsEnumerable()
                                              .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
                {
                    skins.Add(skin.ToLive(Realm));
                }
            });

            return skins;
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
            {
                // Imported BMS packages are fixed hash-backed sets. Once selected, their owner must not follow a
                // later Realm notification to a different set before explicit revision publication. Legacy empty/core
                // fixtures remain on their compatibility constructor until their package type has an exact factory.
                if (skinInfo.IsManaged
                    && SkinManagedFolderFactory.IsInstantiationInfoAllowed(skinInfo.InstantiationInfo)
                    && skinInfo.Files.Any(file => string.Equals(file.Filename, "skin.ini", StringComparison.OrdinalIgnoreCase)))
                {
                    return createExactRealmPackageSkin(skinInfo);
                }

                return skinInfo.CreateInstance(this);
            }

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

        private Skin createExactRealmPackageSkin(SkinInfo skinInfo)
        {
            RealmPackageRevisionSnapshot snapshot = RealmPackageRevisionSnapshot.Create(skinInfo);
            var entries = new List<SkinPackageCapturedEntry>(snapshot.Files.Count);

            foreach (RealmPackageFileDeclaration file in snapshot.Files)
            {
                byte[] content = userFiles.Get(new RealmFile { Hash = file.Hash }.GetStoragePath())
                                 ?? throw new InvalidOperationException("The exact Realm skin package is unavailable.");
                string actualHash = Convert.ToHexString(SHA256.HashData(content));

                if (!string.Equals(actualHash, file.Hash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("The exact Realm skin package changed during selection.");

                entries.Add(SkinPackageCapturedEntry.CreateFile(file.Filename, content));
            }

            SkinPackageRevisionCapsuleCreationResult capsule = SkinPackageRevisionCapsuleFactory.Create(entries);

            if (!capsule.IsSuccess)
                throw new InvalidOperationException("The exact Realm skin package could not be captured.");

            SkinInfo exactInfo = createFilesystemSkinSnapshot(snapshot.Metadata);
            exactInfo.Hash = capsule.Capsule!.ContentRevision;
            SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(exactInfo, this, capsule.Capsule);

            if (!factory.IsSuccess)
                throw new InvalidOperationException("The exact Realm skin package type is unsupported.");

            return factory.Skin!;
        }

        private bool requestSelection(Live<SkinInfo> target)
        {
            // Clear at request admission only. A successful publication notifies arbitrary observers; one of those may
            // issue and reject a newer reentrant request whose diagnostic must not be erased by the outer completion.
            LastSelectionRejectionReason = SkinSelectionRejectionReason.None;

            if (isCurrentRevisionMutationAdmitted())
            {
                rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);
                return false;
            }

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

            if (request.Resolution.Authority is SkinFilesystemStorageAuthority.ManagedFolder
                    or SkinFilesystemStorageAuthority.ExternalFolder
                && !ThreadSafety.IsUpdateThread)
            {
                throw new InvalidOperationException("Folder skin selection requests must run on the update thread.");
            }

            SelectionRequestBeforeCommitLock(target);

            SkinRevisionParticipantSnapshot publicationParticipants =
                currentRevisionPublication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason publicationRejection);

            if (publicationParticipants == null)
            {
                rejectSelection(publicationRejection == SkinRevisionBarrierRejectionReason.LiveGameplayActive
                    ? SkinSelectionRejectionReason.LiveGameplayActive
                    : SkinSelectionRejectionReason.PreparationFailed);
                return false;
            }

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
                        Live<SkinInfo> expectedSelection = CurrentSkinInfo.Value;
                        Skin expectedOwner = CurrentSkin.Value;
                        SkinCurrentRevision expectedRevision = currentRevisionPublication.Current;
                        Skin preparedOwner;
                        SkinCurrentRevision preparedRevision;

                        try
                        {
                            preparedOwner = target.PerformRead(GetSkin);
                            preparedRevision = createCurrentRevision(preparedOwner);
                        }
                        catch
                        {
                            rejectSelection(SkinSelectionRejectionReason.PreparationFailed);
                            return false;
                        }

                        bool published = tryPublishPreparedCurrentSelectionRetainingParticipants(
                            publicationParticipants,
                            preparedRevision,
                            target,
                            expectedSelection,
                            expectedOwner,
                            expectedRevision,
                            out SkinRevisionBarrierRejectionReason rejection);

                        if (!published)
                        {
                            DiscardProvisionalCurrentRevision(preparedRevision);

                            if (generation == Interlocked.Read(ref selectionGeneration))
                            {
                                rejectSelection(rejection == SkinRevisionBarrierRejectionReason.LiveGameplayActive
                                    ? SkinSelectionRejectionReason.LiveGameplayActive
                                    : SkinSelectionRejectionReason.CapturedCandidateChanged);
                            }
                        }

                        else if (generation == Interlocked.Read(ref selectionGeneration))
                            LastSelectionRejectionReason = SkinSelectionRejectionReason.None;

                        return false;

                    case SkinFilesystemStorageAuthority.ExternalFolder:
                        beginExternalFolderSelectionPreparation(generation, target, request);
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

        private void beginExternalFolderSelectionPreparation(
            long generation,
            Live<SkinInfo> target,
            SelectionRequest request)
        {
            if (request.Resolution.ExternalCaptureRequest == null
                || request.Snapshot == null
                || !request.IsRealmManaged
                || !request.HasExactExternalOwner
                || !SkinManagedFolderFactory.IsInstantiationInfoAllowed(request.Snapshot.InstantiationInfo))
            {
                rejectSelection(SkinSelectionRejectionReason.UnmanagedFilesystemRecord);
                return;
            }

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
                Task<ExternalFolderSelectionPreparationResult> preparationTask = Task.Run(
                    () => prepareExternalFolderSelection(request, cancellation.Token),
                    cancellation.Token);
                Task completionSchedulingTask = preparationTask.ContinueWith(
                    task => scheduleExternalFolderSelectionCompletion(
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

        private ExternalFolderSelectionPreparationResult prepareExternalFolderSelection(
            SelectionRequest request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ISkinManagedFolderMutationNativeSession managedAuthority = null;
            SkinExternalFolderRegistrySnapshot registrySnapshot = null;
            ISkinExternalPackageCaptureSession packageSession = null;
            Skin preparedSkin = null;

            try
            {
                if (ManagedFolderOperationCoordinator.IsMutationBlocked)
                    return ExternalFolderSelectionPreparationResult.Reject;

                managedAuthority = managedFolderMutationNativeAuthority.Open(cancellationToken);
                SkinExternalFolderRegistryCaptureResult registryCapture = externalFolderRegistry.CaptureExactSetForSelection(
                    new[] { managedAuthority.ManagedRootAncestryProof },
                    ExternalFolderSelectionCaptureAuthorityOpened,
                    cancellationToken);

                if (!registryCapture.IsSuccess)
                    return ExternalFolderSelectionPreparationResult.Reject;

                registrySnapshot = registryCapture.Snapshot!;

                if (!registrySnapshot.ContainsRecordId(request.Snapshot.ID)
                    || !registrySnapshot.TryGetPhysicalProof(
                        request.Snapshot.ID,
                        out SkinFolderPhysicalAncestryProof registeredProof)
                    || registeredProof == null)
                {
                    return ExternalFolderSelectionPreparationResult.Reject;
                }

                SkinExternalPackageCaptureResult packageCapture = externalFolderCaptureService.CaptureHeld(
                    request.Resolution.ExternalCaptureRequest,
                    cancellationToken: cancellationToken);

                if (!packageCapture.IsSuccess)
                    return ExternalFolderSelectionPreparationResult.Reject;

                packageSession = packageCapture.Session!;

                if (!string.Equals(
                        registeredProof.Digest,
                        packageSession.PhysicalProof.Digest,
                        StringComparison.Ordinal))
                {
                    return ExternalFolderSelectionPreparationResult.Reject;
                }

                SkinPackageRevisionCapsule capsule = packageSession.TakeCapsule();

                if (!SkinManagedFolderPackageMetadataReader.TryRead(
                        capsule,
                        out SkinManagedFolderPackageMetadata metadata))
                {
                    capsule.Dispose();
                    return ExternalFolderSelectionPreparationResult.Reject;
                }

                SkinInfo freshSnapshot = createFilesystemSkinSnapshot(request.Snapshot);
                freshSnapshot.Name = metadata!.Name;
                freshSnapshot.Creator = metadata.Creator;
                freshSnapshot.Hash = metadata.ContentRevision;
                SkinManagedFolderFactoryResult factory = ManagedFolderFactoryCreate(
                    freshSnapshot,
                    this,
                    capsule);

                if (!factory.IsSuccess)
                    return ExternalFolderSelectionPreparationResult.Reject;

                preparedSkin = factory.Skin!;
                packageSession.Validate(cancellationToken);
                managedAuthority.ValidateCompleteAndStable(cancellationToken);

                ExternalFolderSelectionPreparationResult result = ExternalFolderSelectionPreparationResult.Success(
                    preparedSkin,
                    metadata,
                    managedAuthority,
                    registrySnapshot,
                    packageSession);
                preparedSkin = null;
                managedAuthority = null;
                registrySnapshot = null;
                packageSession = null;
                return result;
            }
            finally
            {
                preparedSkin?.Dispose();
                packageSession?.Dispose();
                registrySnapshot?.Dispose();
                managedAuthority?.Dispose();
            }
        }

        private void scheduleExternalFolderSelectionCompletion(
            long generation,
            Live<SkinInfo> target,
            SelectionRequest request,
            SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
            CancellationTokenSource cancellation,
            Task<ExternalFolderSelectionPreparationResult> preparationTask)
        {
            var pendingCompletion = new PendingExternalFolderSelectionCompletion(
                generation,
                target,
                request,
                preparationObservation,
                cancellation,
                preparationTask);

            lock (managedFolderSelectionLifecycleGate)
            {
                if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
                {
                    discardExternalFolderSelectionCompletion(pendingCompletion);
                    return;
                }

                pendingExternalFolderSelectionCompletions.Add(pendingCompletion);
            }

            try
            {
                ManagedFolderCompletionSchedule(() => completeExternalFolderSelection(pendingCompletion));
            }
            catch
            {
                bool claimed;

                lock (managedFolderSelectionLifecycleGate)
                    claimed = pendingExternalFolderSelectionCompletions.Remove(pendingCompletion);

                if (claimed)
                {
                    discardExternalFolderSelectionCompletion(pendingCompletion);
                    tryRejectSelectionWithoutBlocking(generation, SkinSelectionRejectionReason.PreparationFailed);
                }
            }
        }

        private void completeExternalFolderSelection(PendingExternalFolderSelectionCompletion pendingCompletion)
        {
            lock (managedFolderSelectionLifecycleGate)
            {
                if (!pendingExternalFolderSelectionCompletions.Remove(pendingCompletion))
                    return;
            }

            Interlocked.CompareExchange(
                ref pendingSelectionCancellation,
                null,
                pendingCompletion.Cancellation);
            pendingCompletion.Cancellation.Dispose();

            if (Volatile.Read(ref managedFolderSelectionShutdown) != 0)
            {
                disposeExternalPreparationTask(pendingCompletion.PreparationTask);
                return;
            }

            if (pendingCompletion.PreparationTask.Status != TaskStatus.RanToCompletion)
            {
                if (pendingCompletion.PreparationTask.IsFaulted)
                    _ = pendingCompletion.PreparationTask.Exception;

                rejectSelection(
                    pendingCompletion.Generation,
                    pendingCompletion.PreparationTask.IsCanceled
                        ? SkinSelectionRejectionReason.PreparationCancelled
                        : SkinSelectionRejectionReason.PreparationFailed);
                return;
            }

            ExternalFolderSelectionPreparationResult prepared =
                pendingCompletion.PreparationTask.GetAwaiter().GetResult();

            if (!prepared.IsSuccess
                || pendingCompletion.Generation != Interlocked.Read(ref selectionGeneration))
            {
                prepared.Dispose();

                if (!prepared.IsSuccess)
                    rejectSelection(pendingCompletion.Generation, SkinSelectionRejectionReason.CaptureRejected);

                return;
            }

            if (!ManagedFolderOperationCoordinator.TryEnterForSelection(
                    out SkinManagedFolderOperationCoordinator.Lease finalLease,
                    out SkinManagedFolderOperationCoordinator.SelectionContention contention))
            {
                prepared.Dispose();

                if (contention != null
                    && ManagedFolderOperationCoordinator.IsMutationReservationEpochCurrent(
                        pendingCompletion.PreparationObservation))
                {
                    scheduleManagedFolderSelectionRetryAfterContention(
                        pendingCompletion.Generation,
                        pendingCompletion.Target.ID,
                        CurrentSkinInfo.Value.ID,
                        CurrentSkin.Value,
                        pendingCompletion.PreparationObservation,
                        contention);
                }
                else
                    rejectSelection(SkinSelectionRejectionReason.ManagedFolderOperationInProgress);

                return;
            }

            try
            {
                using (finalLease)
                {
                    SkinRevisionParticipantSnapshot publicationParticipants =
                        currentRevisionPublication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason publicationRejection);

                    if (pendingCompletion.Generation != Interlocked.Read(ref selectionGeneration)
                        || CurrentSkinInfo.Disabled
                        || publicationParticipants == null
                        || !ManagedFolderOperationCoordinator.IsMutationReservationEpochCurrent(
                            pendingCompletion.PreparationObservation)
                        || ManagedFolderOperationCoordinator.IsMutationBlocked)
                    {
                        rejectSelection(publicationRejection == SkinRevisionBarrierRejectionReason.LiveGameplayActive
                            ? SkinSelectionRejectionReason.LiveGameplayActive
                            : SkinSelectionRejectionReason.CapturedCandidateChanged);
                        return;
                    }

                    try
                    {
                        prepared.PackageSession!.Validate();
                        prepared.ManagedAuthority!.ValidateCompleteAndStable(CancellationToken.None);

                        if (!prepared.RegistrySnapshot!.TryGetPhysicalProof(
                                pendingCompletion.Target.ID,
                                out SkinFolderPhysicalAncestryProof registeredProof)
                            || registeredProof == null
                            || !string.Equals(
                                registeredProof.Digest,
                                prepared.PackageSession.PhysicalProof.Digest,
                                StringComparison.Ordinal)
                            || !prepared.RegistrySnapshot.Validate(finalLease, CancellationToken.None))
                        {
                            rejectSelection(SkinSelectionRejectionReason.CapturedCandidateChanged);
                            return;
                        }
                    }
                    catch
                    {
                        rejectSelection(SkinSelectionRejectionReason.CapturedCandidateChanged);
                        return;
                    }

                    Live<SkinInfo> authoritativeTarget = null;

                    try
                    {
                        bool observationsPublished = Realm.Write(r =>
                        {
                            r.Refresh();
                            SkinInfo current = r.Find<SkinInfo>(pendingCompletion.Target.ID);

                            if (current == null
                                || !pendingCompletion.Request.MatchesDeclaredFields(current)
                                || !prepared.RegistrySnapshot.ExactlyMatchesRealmDeclarations(r.All<SkinInfo>()))
                            {
                                return false;
                            }

                            current.Name = prepared.Metadata!.Name;
                            current.Creator = prepared.Metadata.Creator;
                            current.Hash = prepared.Metadata.ContentRevision;
                            return true;
                        });

                        if (observationsPublished)
                            authoritativeTarget = pendingCompletion.Target;
                    }
                    catch
                    {
                        authoritativeTarget = null;
                    }

                    if (authoritativeTarget == null)
                    {
                        rejectSelection(SkinSelectionRejectionReason.CapturedCandidateChanged);
                        return;
                    }

                    Skin preparedSkin = prepared.TransferSkin();
                    SkinCurrentRevision preparedRevision;

                    try
                    {
                        preparedRevision = CreateProvisionalCurrentRevision(
                            preparedSkin,
                            prepared.Metadata!.ContentRevision,
                            SkinCurrentRevisionSourceKind.ExternalFolder);
                    }
                    catch
                    {
                        preparedSkin.Dispose();
                        rejectSelection(SkinSelectionRejectionReason.PreparationFailed);
                        return;
                    }

                    bool published = tryPublishPreparedCurrentSelectionRetainingParticipants(
                        publicationParticipants,
                        preparedRevision,
                        authoritativeTarget,
                        CurrentSkinInfo.Value,
                        CurrentSkin.Value,
                        publicationParticipants.CurrentRevision,
                        out SkinRevisionBarrierRejectionReason rejection);

                    if (!published)
                    {
                        DiscardProvisionalCurrentRevision(preparedRevision);

                        if (pendingCompletion.Generation == Interlocked.Read(ref selectionGeneration))
                        {
                            rejectSelection(rejection == SkinRevisionBarrierRejectionReason.LiveGameplayActive
                                ? SkinSelectionRejectionReason.LiveGameplayActive
                                : SkinSelectionRejectionReason.CapturedCandidateChanged);
                        }
                    }

                    else if (pendingCompletion.Generation == Interlocked.Read(ref selectionGeneration))
                        LastSelectionRejectionReason = SkinSelectionRejectionReason.None;
                }
            }
            finally
            {
                prepared.Dispose();
            }
        }

        private void discardExternalFolderSelectionCompletion(
            PendingExternalFolderSelectionCompletion pendingCompletion)
        {
            Interlocked.CompareExchange(
                ref pendingSelectionCancellation,
                null,
                pendingCompletion.Cancellation);
            pendingCompletion.Cancellation.Dispose();
            disposeExternalPreparationTask(pendingCompletion.PreparationTask);
        }

        private static void disposeExternalPreparationTask(
            Task<ExternalFolderSelectionPreparationResult> preparationTask)
        {
            if (preparationTask.Status == TaskStatus.RanToCompletion)
                preparationTask.GetAwaiter().GetResult().Dispose();
            else if (preparationTask.IsFaulted)
                _ = preparationTask.Exception;
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

                SkinRevisionParticipantSnapshot publicationParticipants =
                    currentRevisionPublication.CaptureSnapshot(out SkinRevisionBarrierRejectionReason publicationRejection);

                if (publicationParticipants == null)
                {
                    factory.Skin!.Dispose();
                    rejectSelection(publicationRejection == SkinRevisionBarrierRejectionReason.LiveGameplayActive
                        ? SkinSelectionRejectionReason.LiveGameplayActive
                        : SkinSelectionRejectionReason.PreparationFailed);
                    return;
                }

                Live<SkinInfo> expectedSelection = CurrentSkinInfo.Value;
                Skin expectedOwner = CurrentSkin.Value;
                SkinCurrentRevision expectedRevision = publicationParticipants.CurrentRevision;

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

                SkinCurrentRevision preparedRevision;

                try
                {
                    preparedRevision = createCurrentRevision(factory.Skin!);
                }
                catch
                {
                    factory.Skin!.Dispose();
                    rejectSelection(SkinSelectionRejectionReason.PreparationFailed);
                    return;
                }

                SkinRevisionBarrierRejectionReason rejection = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                bool published = generation == Interlocked.Read(ref selectionGeneration)
                                 && tryPublishPreparedCurrentSelectionRetainingParticipants(
                                     publicationParticipants,
                                     preparedRevision,
                                     authoritativeTarget,
                                     expectedSelection,
                                     expectedOwner,
                                     expectedRevision,
                                     out rejection);

                if (!published)
                {
                    DiscardProvisionalCurrentRevision(preparedRevision);
                    rejectSelection(generation,
                        rejection == SkinRevisionBarrierRejectionReason.LiveGameplayActive
                            ? SkinSelectionRejectionReason.LiveGameplayActive
                            : SkinSelectionRejectionReason.CapturedCandidateChanged);
                }

                else if (generation == Interlocked.Read(ref selectionGeneration))
                    LastSelectionRejectionReason = SkinSelectionRejectionReason.None;
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

                if (retryRequest.Resolution.Authority == SkinFilesystemStorageAuthority.ExternalFolder)
                    beginExternalFolderSelectionPreparation(generation, authoritativeTarget, retryRequest);
                else
                    beginManagedFolderSelectionPreparation(generation, authoritativeTarget, retryRequest);
            }
        }

        private SelectionRequest createSelectionRequest(SkinInfo skinInfo)
        {
            SkinFilesystemStorageResolution resolution = SkinFilesystemStorageResolver.ResolveExisting(skinInfo, storage);
            SkinInfo snapshot = resolution.Authority is SkinFilesystemStorageAuthority.ManagedFolder
                    or SkinFilesystemStorageAuthority.ExternalFolder
                ? createFilesystemSkinSnapshot(skinInfo)
                : null;

            return new SelectionRequest(
                resolution,
                snapshot,
                skinInfo.IsManaged,
                string.Equals(skinInfo.FilesystemStorageAuthorityOwner, SkinManagedFolderScanner.AUTHORITY_OWNER, StringComparison.Ordinal),
                string.Equals(skinInfo.FilesystemStorageAuthorityOwner, SkinExternalFolderRegistry.AUTHORITY_OWNER, StringComparison.Ordinal));
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

            using RealmPackageMutationBoundaryLease operationLease =
                acquireRealmPackageMutationBoundary(skin.SkinInfo.ID);

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
                PublishedCurrentSkinPair pair = Volatile.Read(ref publishedCurrentSkinPair);
                yield return pair.Owner;

                // OMS is the only built-in fallback surfaced by the product.
                if (pair.Owner.SkinInfo.ID != DefaultOmsSkin.SkinInfo.ID)
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
            throw new InvalidOperationException(SkinAuthoringAvailability.UPDATE_IMPORT_DISABLED_DIAGNOSTIC);
        }

        public Task<ExternalEditOperation<SkinInfo>> BeginExternalEditing(SkinInfo model)
        {
            throw new InvalidOperationException(SkinAuthoringAvailability.EXTERNAL_EDITING_DISABLED_DIAGNOSTIC);
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

                // Bulk maintenance never owns the current revision publication/detach protocol. Keep the exact current
                // record out of this legacy route; the settings delete command is the sole current-delete caller.
                Guid currentSelectionId = CurrentSkinInfo.Value.ID;
                Guid currentOwnerId = CurrentSkin.Value.SkinInfo.ID;
                Guid currentRevisionId = currentRevisionPublication.Current.RecordId;
                items = items.Where(s => s.ID != currentSelectionId
                                         && s.ID != currentOwnerId
                                         && s.ID != currentRevisionId);

                Delete(items.ToList(), silent);
            });
        }

        public void Rename(Live<SkinInfo> skin, string newName)
        {
            if (skin.PerformRead(isFilesystemBacked))
                throw new InvalidOperationException("Filesystem-backed skins cannot be renamed through the Realm package workflow.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(skin.ID);

            skin.PerformWrite(s =>
            {
                s.Name = newName;
                skinImporter.UpdateSkinIniMetadata(s, s.Realm!);
            });
        }

        public override bool Delete(SkinInfo item)
        {
            if (!tryAcquireRealmPackageMutationBoundary(item.ID, out RealmPackageMutationBoundaryLease operationLease))
                return false;

            using (operationLease)
            {
                if (isCurrentRevisionRecord(item.ID))
                    return false;

                return Realm.Write(realm =>
                {
                    SkinInfo authoritative = realm.Find<SkinInfo>(item.ID);

                    if (authoritative == null
                        || authoritative.Protected
                        || SkinFilesystemStorageResolver.IsFixedSkinId(authoritative.ID)
                        || isFilesystemBacked(authoritative)
                        || authoritative.DeletePending
                        || isCurrentRevisionRecord(authoritative.ID))
                    {
                        return false;
                    }

                    authoritative.DeletePending = true;

                    if (isCurrentRevisionRecord(authoritative.ID))
                    {
                        authoritative.DeletePending = false;
                        return false;
                    }

                    return true;
                });
            }
        }

        public override void Delete(List<SkinInfo> items, bool silent = false)
            => base.Delete(items.Where(item => !isFilesystemBacked(item)).ToList(), silent);

        public override void Undelete(SkinInfo item)
        {
            if (!tryAcquireRealmPackageMutationBoundary(item.ID, out RealmPackageMutationBoundaryLease operationLease))
                return;

            using (operationLease)
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
        }

        public override void Undelete(List<SkinInfo> items, bool silent = false)
            => base.Undelete(items.Where(item => !isFilesystemBacked(item)).ToList(), silent);

        public override void AddFile(SkinInfo item, Stream contents, string filename)
        {
            if (isFilesystemBacked(item))
                throw new InvalidOperationException("Filesystem-backed skins cannot receive Realm package files.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(item.ID);

            base.AddFile(item, contents, filename);
        }

        public override void DeleteFile(SkinInfo item, RealmNamedFileUsage file)
        {
            if (isFilesystemBacked(item))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(item.ID);

            base.DeleteFile(item, file);
        }

        public override void ReplaceFile(SkinInfo item, RealmNamedFileUsage file, Stream contents)
        {
            if (isFilesystemBacked(item))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(item.ID);

            base.ReplaceFile(item, file, contents);
        }

        public override void AddFile(SkinInfo item, Stream contents, string filename, Realm realm)
        {
            if (isFilesystemBacked(item, realm))
                throw new InvalidOperationException("Filesystem-backed skins cannot receive Realm package files.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(item.ID);

            base.AddFile(item, contents, filename, realm);
        }

        public override void DeleteFile(SkinInfo item, RealmNamedFileUsage file, Realm realm)
        {
            if (isFilesystemBacked(item, realm))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(item.ID);

            base.DeleteFile(item, file, realm);
        }

        public override void ReplaceFile(SkinInfo item, RealmNamedFileUsage file, Stream contents, Realm realm)
        {
            if (isFilesystemBacked(item, realm))
                throw new InvalidOperationException("Filesystem-backed skins cannot mutate Realm package files.");

            using RealmPackageMutationBoundaryLease operationLease = acquireRealmPackageMutationBoundary(item.ID);

            base.ReplaceFile(item, file, contents, realm);
        }

        private RealmPackageMutationBoundaryLease acquireRealmPackageMutationBoundary(Guid recordId)
        {
            // The first check preserves the specific stable current-package diagnostic even when another operation is
            // active. Admission then linearises the full synchronous mutation (including SkinImporter callbacks) against
            // current revision reload/delete/unregister before the shared coordinator is entered.
            rejectCurrentRealmPackageMutation(recordId);

            if (!tryEnterRealmPackageMutationAdmission())
                throw new InvalidOperationException(REALM_PACKAGE_MUTATION_BUSY_DIAGNOSTIC);

            SkinManagedFolderOperationCoordinator.Lease operationLease = null;

            try
            {
                if (!ManagedFolderOperationCoordinator.TryEnter(out operationLease))
                    throw new InvalidOperationException(REALM_PACKAGE_MUTATION_BUSY_DIAGNOSTIC);

                rejectCurrentRealmPackageMutation(recordId);
                RealmPackageMutationBoundaryEntered();
                return new RealmPackageMutationBoundaryLease(this, operationLease);
            }
            catch
            {
                try
                {
                    operationLease?.Dispose();
                }
                finally
                {
                    exitRealmPackageMutationAdmission();
                }

                throw;
            }
        }

        /// <summary>
        /// Shares the exact Realm-package mutation admission with the importer public surface. This prevents a second
        /// importer instance from writing the current record without current revision publication.
        /// </summary>
        internal IDisposable AcquireSkinImporterMutationBoundary(Guid recordId)
            => acquireRealmPackageMutationBoundary(recordId);

        private bool tryAcquireRealmPackageMutationBoundary(
            Guid recordId,
            out RealmPackageMutationBoundaryLease operationLease)
        {
            try
            {
                operationLease = acquireRealmPackageMutationBoundary(recordId);
                return true;
            }
            catch (InvalidOperationException)
            {
                operationLease = null;
                return false;
            }
        }

        private bool tryEnterRealmPackageMutationAdmission()
        {
            int currentThreadId = Environment.CurrentManagedThreadId;

            lock (managedFolderRenameLifecycleGate)
            {
                if (realmPackageMutationAdmissionDepth > 0)
                {
                    if (realmPackageMutationOwnerManagedThreadId != currentThreadId)
                        return false;

                    realmPackageMutationAdmissionDepth++;
                    return true;
                }

                if (managedFolderMutationShutdown
                    || currentRevisionMutationAdmissionHeld
                    || currentRevisionReloadAdmissionHeld
                    || Volatile.Read(ref currentSkinProjectionInProgress) != 0
                    || activeManagedFolderRenameTask is { IsCompleted: false }
                    || activeManagedFolderStagedImportTask is { IsCompleted: false }
                    || activeManagedFolderDeleteTask is { IsCompleted: false }
                    || activeManagedFolderRecoveryTask is { IsCompleted: false }
                    || activeFolderWorkspaceTask is { IsCompleted: false })
                {
                    return false;
                }

                realmPackageMutationOwnerManagedThreadId = currentThreadId;
                realmPackageMutationAdmissionDepth = 1;
                return true;
            }
        }

        private void exitRealmPackageMutationAdmission()
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (realmPackageMutationAdmissionDepth <= 0
                    || realmPackageMutationOwnerManagedThreadId != Environment.CurrentManagedThreadId)
                {
                    throw new SynchronizationLockException("The Realm skin package mutation admission is not held by this thread.");
                }

                realmPackageMutationAdmissionDepth--;

                if (realmPackageMutationAdmissionDepth == 0)
                    realmPackageMutationOwnerManagedThreadId = 0;
            }
        }

        private sealed class RealmPackageMutationBoundaryLease : IDisposable
        {
            private SkinManager owner;
            private SkinManagedFolderOperationCoordinator.Lease operationLease;

            public RealmPackageMutationBoundaryLease(
                SkinManager owner,
                SkinManagedFolderOperationCoordinator.Lease operationLease)
            {
                this.owner = owner;
                this.operationLease = operationLease;
            }

            public void Dispose()
            {
                SkinManager heldOwner = Interlocked.Exchange(ref owner, null);

                if (heldOwner == null)
                    return;

                try
                {
                    Interlocked.Exchange(ref operationLease, null)?.Dispose();
                }
                finally
                {
                    heldOwner.exitRealmPackageMutationAdmission();
                }
            }
        }

        private void rejectCurrentRealmPackageMutation(Guid recordId)
        {
            if (isCurrentRevisionRecord(recordId))
                throw new InvalidOperationException(CURRENT_REALM_PACKAGE_MUTATION_DISABLED_DIAGNOSTIC);
        }

        private bool isCurrentRevisionRecord(Guid recordId)
        {
            PublishedCurrentSkinPair pair = Volatile.Read(ref publishedCurrentSkinPair);
            return pair.Revision.RecordId == recordId
                   || pair.Selection.ID == recordId
                   || pair.Owner.SkinInfo.ID == recordId;
        }

        private async Task<bool> unregisterCurrentExternalFolderAdmittedAsync(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await unregisterCurrentExternalFolderAsync(recordId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                releaseCurrentRevisionMutationAdmission();
            }
        }

        public bool CanModify(Live<SkinInfo> skin)
            => skin.PerformRead(info => !info.Protected
                                        && !isFilesystemBacked(info)
                                        && !isCurrentRevisionRecord(info.ID));

        public bool CanExport(Live<SkinInfo> skin)
            => skin.PerformRead(info => !info.Protected && !isFilesystemBacked(info));

        /// <summary>
        /// Returns the settings delete affordance from a fresh authoritative Realm read. This grants no mutation
        /// capability; confirmation re-enters the dedicated operation and repeats every logical/native check.
        /// </summary>
        internal bool CanDelete(Live<SkinInfo> skin)
        {
            if (skin == null)
                return false;

            return CanDelete(skin.ID);
        }

        /// <summary>
        /// Path-free workspace affordance. The identifier is never treated as authority; every field is classified
        /// from a fresh Realm view and confirmation repeats the same checks inside the operation.
        /// </summary>
        internal bool CanDelete(Guid recordId)
        {
            if (recordId == Guid.Empty)
                return false;

            try
            {
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
                bool isCurrent = CurrentSkinInfo.Value.ID == recordId
                                 || CurrentSkin.Value.SkinInfo.ID == recordId;

                if (isCurrent)
                {
                    if (CurrentSkinInfo.Value.ID != recordId
                        || CurrentSkin.Value.SkinInfo.ID != recordId)
                    {
                        return Task.FromResult(false);
                    }

                    if (!tryAdmitCurrentRevisionMutation())
                        return Task.FromResult(false);

                    return trackAdmittedCurrentRevisionMutation(
                        token => deleteCurrentRealmPackageAsync(recordId, token),
                        cancellationToken);
                }

                return Task.FromResult(ordinarySnapshot != null && Delete(ordinarySnapshot));
            }

            if (candidateKind != DeleteCandidateKind.ManagedFolder)
                return Task.FromResult(false);

            if (CurrentSkinInfo.Value.ID == recordId || CurrentSkin.Value.SkinInfo.ID == recordId)
            {
                if (CurrentSkinInfo.Value.ID != recordId
                    || CurrentSkin.Value.SkinInfo.ID != recordId)
                {
                    return Task.FromResult(false);
                }

                if (!tryAdmitCurrentRevisionMutation())
                    return Task.FromResult(false);

                return trackAdmittedCurrentRevisionMutation(
                    token => deleteCurrentManagedFolderAsync(recordId, token),
                    cancellationToken);
            }

            return reduceManagedFolderDeleteResult(DeleteManagedFolderAsync(recordId, cancellationToken));
        }

        private async Task<bool> deleteCurrentRealmPackageAsync(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await deleteCurrentRealmPackageHeldAsync(recordId, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                releaseCurrentRevisionMutationAdmission();
            }
        }

        private async Task<bool> deleteCurrentRealmPackageHeldAsync(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            SkinCurrentRevision activeRevision = currentRevisionPublication.Current;

            if (activeRevision.RecordId != recordId
                || activeRevision.SourceKind != SkinCurrentRevisionSourceKind.RealmPackage
                || !ReferenceEquals(activeRevision.Owner, CurrentSkin.Value)
                || !string.Equals(
                    activeRevision.Owner.PackageContentRevision,
                    activeRevision.ContentRevision,
                    StringComparison.Ordinal))
            {
                return false;
            }

            RealmPackageRevisionSnapshot snapshot;

            try
            {
                snapshot = readRealmPackageRevisionSnapshot(recordId);
            }
            catch
            {
                return false;
            }

            if (snapshot == null
                || !tryCaptureRealmPackageContentRevision(snapshot, cancellationToken, out string contentRevision)
                || !string.Equals(contentRevision, activeRevision.ContentRevision, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                if (!snapshot.Matches(readRealmPackageRevisionSnapshot(recordId)))
                    return false;
            }
            catch
            {
                return false;
            }

            ProtectedFallbackPublicationTransaction fallback =
                await publishProtectedFallbackAndWaitForDetachAsync(recordId, cancellationToken).ConfigureAwait(false);

            if (fallback == null)
                return false;

            bool deleted;

            try
            {
                CurrentRealmPackageDeleteBeforeRealmCommit();

                deleted = Realm.Write(realm =>
                {
                    realm.Refresh();

                    if (!ReferenceEquals(currentRevisionPublication.Current, fallback.FallbackRevision)
                        || CurrentSkinInfo.Value.ID != SkinInfo.OMS_SKIN
                        || !ReferenceEquals(CurrentSkin.Value, DefaultOmsSkin))
                    {
                        return false;
                    }

                    SkinInfo current = realm.Find<SkinInfo>(recordId);
                    RealmPackageRevisionSnapshot fresh = current == null ? null : createRealmPackageSnapshot(current);

                    if (!snapshot.Matches(fresh))
                        return false;

                    current.DeletePending = true;
                    return true;
                });
            }
            catch
            {
                deleted = false;
            }

            if (deleted)
            {
                fallback.Complete();
                Interlocked.Increment(ref selectionGeneration);
                cancelPendingSelection();
                return true;
            }

            await rollbackProtectedFallbackAsync(fallback).ConfigureAwait(false);
            return false;
        }

        private bool tryCaptureRealmPackageContentRevision(
            RealmPackageRevisionSnapshot snapshot,
            CancellationToken cancellationToken,
            out string contentRevision)
        {
            contentRevision = string.Empty;

            try
            {
                var entries = new List<SkinPackageCapturedEntry>(snapshot.Files.Count);

                foreach (RealmPackageFileDeclaration file in snapshot.Files)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] content = userFiles.Get(new RealmFile { Hash = file.Hash }.GetStoragePath());

                    if (content == null
                        || !string.Equals(
                            Convert.ToHexString(SHA256.HashData(content)),
                            file.Hash,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }

                    entries.Add(SkinPackageCapturedEntry.CreateFile(file.Filename, content));
                }

                SkinPackageRevisionCapsuleCreationResult result =
                    SkinPackageRevisionCapsuleFactory.Create(entries, cancellationToken: cancellationToken);

                if (!result.IsSuccess)
                    return false;

                using SkinPackageRevisionCapsule capsule = result.Capsule;
                contentRevision = capsule.ContentRevision;
                return true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> deleteCurrentManagedFolderAsync(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            try
            {
                return await deleteCurrentManagedFolderHeldAsync(recordId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            finally
            {
                releaseCurrentRevisionMutationAdmission();
            }
        }

        private async Task<bool> deleteCurrentManagedFolderHeldAsync(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            if (!isExactCurrentManagedRevision(recordId))
                return false;

            if (!isExactProtectedFallbackAuthority())
            {
                Volatile.Write(
                    ref lastManagedFolderDeleteResult,
                    SkinManagedFolderDeleteOperationResult.FallbackRejected(
                        SkinManagedFolderProtectedFallbackCommitResult.FallbackInvalid));
                return false;
            }

            SkinCurrentRevision expectedRevision = currentRevisionPublication.Current;
            Skin expectedOwner = CurrentSkin.Value;
            Live<SkinInfo> expectedSelection = CurrentSkinInfo.Value;
            long expectedSelectionGeneration = Interlocked.Read(ref selectionGeneration);
            SkinManagedFolderPreparedDelete prepared;

            try
            {
                prepared = await Task.Run(
                    () => managedFolderDeleteOperation.Prepare(
                        Guid.NewGuid(),
                        recordId,
                        captureSourceRevision: true,
                        cancellationToken),
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                return false;
            }

            using (prepared)
            {
                if (!prepared.IsSuccess)
                {
                    Volatile.Write(ref lastManagedFolderDeleteResult, prepared.FailureResult);
                    return false;
                }

                if (!prepared.HoldsContentRevision(expectedRevision.ContentRevision)
                    || !string.Equals(
                        expectedOwner.PackageContentRevision,
                        expectedRevision.ContentRevision,
                        StringComparison.Ordinal)
                    || !prepared.ValidateHeldSource(cancellationToken))
                {
                    return false;
                }

                ProtectedFallbackPublicationTransaction fallback =
                    await publishProtectedFallbackForPreparedManagedDeleteAsync(
                        recordId,
                        expectedSelection,
                        expectedOwner,
                        expectedRevision,
                        expectedSelectionGeneration,
                        prepared,
                        cancellationToken).ConfigureAwait(false);

                if (fallback == null)
                    return false;

                SkinManagedFolderDeleteOperationResult result;

                try
                {
                    result = await Task.Run(
                        () => managedFolderDeleteOperation.ExecutePrepared(
                            prepared,
                            cancellationToken,
                            requireProtectedFallback: true),
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch
                {
                    result = SkinManagedFolderDeleteOperationResult.Failure(
                        SkinManagedFolderDeleteOperationStatus.PreparedJournalOutcomeUncertain);
                }

                Volatile.Write(ref lastManagedFolderDeleteResult, result);

                if (result.IsSuccess)
                {
                    fallback.Complete();
                    return true;
                }

                if (result.Status is SkinManagedFolderDeleteOperationStatus.AuthorityRejected
                    or SkinManagedFolderDeleteOperationStatus.Busy
                    or SkinManagedFolderDeleteOperationStatus.WrongThread
                    or SkinManagedFolderDeleteOperationStatus.Cancelled
                    or SkinManagedFolderDeleteOperationStatus.FallbackRejected)
                {
                    await rollbackProtectedFallbackAsync(fallback).ConfigureAwait(false);
                }
                else
                {
                    // Outcome-uncertain C1 states are owned by recovery. The protected fallback must remain
                    // authoritative because the exact source may already be in its operation tombstone.
                    fallback.Complete();
                }

                return false;
            }
        }

        private async Task<ProtectedFallbackPublicationTransaction>
            publishProtectedFallbackForPreparedManagedDeleteAsync(
                Guid recordId,
                Live<SkinInfo> expectedSelection,
                Skin expectedOwner,
                SkinCurrentRevision expectedRevision,
                long expectedSelectionGeneration,
                SkinManagedFolderPreparedDelete prepared,
                CancellationToken cancellationToken)
        {
            var admitted = new TaskCompletionSource<Task<ProtectedFallbackPublicationTransaction>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            CurrentRevisionCallbackScheduleResult scheduleResult = scheduleCurrentRevisionCallback(
                () =>
                {
                    if (!ReferenceEquals(CurrentSkinInfo.Value, expectedSelection)
                        || !ReferenceEquals(CurrentSkin.Value, expectedOwner)
                        || !ReferenceEquals(currentRevisionPublication.Current, expectedRevision)
                        || Interlocked.Read(ref selectionGeneration) != expectedSelectionGeneration
                        || !prepared.HoldsContentRevision(expectedRevision.ContentRevision))
                    {
                        admitted.TrySetResult(Task.FromResult<ProtectedFallbackPublicationTransaction>(null));
                        return;
                    }

                    admitted.TrySetResult(
                        publishProtectedFallbackAndWaitForDetachAsync(
                            recordId,
                            cancellationToken,
                            token => prepared.HoldsContentRevision(expectedRevision.ContentRevision)
                                     && Interlocked.Read(ref selectionGeneration) == expectedSelectionGeneration
                                     && prepared.ValidateHeldSource(token),
                            () => prepared.HoldsContentRevision(expectedRevision.ContentRevision)
                                  && Interlocked.Read(ref selectionGeneration) == expectedSelectionGeneration
                                  && ReferenceEquals(currentRevisionPublication.Current, expectedRevision)
                                  && ReferenceEquals(CurrentSkinInfo.Value, expectedSelection)
                                  && ReferenceEquals(CurrentSkin.Value, expectedOwner)));
                },
                () => admitted.TrySetResult(Task.FromResult<ProtectedFallbackPublicationTransaction>(null)));

            if (scheduleResult == CurrentRevisionCallbackScheduleResult.Faulted)
                return null;

            try
            {
                Task<ProtectedFallbackPublicationTransaction> publication = await admitted.Task.ConfigureAwait(false);
                return await publication.ConfigureAwait(false);
            }
            catch
            {
                return null;
            }
        }

        private bool isExactCurrentManagedRevision(Guid recordId)
        {
            SkinCurrentRevision revision = currentRevisionPublication.Current;

            if (revision.RecordId != recordId
                || revision.SourceKind != SkinCurrentRevisionSourceKind.ManagedFolder
                || !ReferenceEquals(revision.Owner, CurrentSkin.Value)
                || !string.Equals(revision.Owner.PackageContentRevision, revision.ContentRevision, StringComparison.Ordinal))
            {
                return false;
            }

            try
            {
                return Realm.Run(realm =>
                {
                    realm.Refresh();
                    SkinInfo current = realm.Find<SkinInfo>(recordId);
                    return current != null
                           && !current.IsExternalFilesystemStorage
                           && current.IsManaged
                           && !current.Protected
                           && !current.DeletePending
                           && current.Files.Count == 0
                           && !string.IsNullOrEmpty(current.Hash)
                           && string.Equals(
                               current.FilesystemStorageAuthorityOwner,
                               SkinManagedFolderScanner.AUTHORITY_OWNER,
                               StringComparison.Ordinal)
                           && SkinManagedFolderFactory.IsInstantiationInfoAllowed(current.InstantiationInfo);
                });
            }
            catch
            {
                return false;
            }
        }

        private bool tryAdmitCurrentRevisionReload()
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown
                    || currentRevisionMutationAdmissionHeld
                    || Volatile.Read(ref currentSkinProjectionInProgress) != 0
                    || realmPackageMutationAdmissionDepth > 0
                    || activeManagedFolderRenameTask is { IsCompleted: false }
                    || activeManagedFolderStagedImportTask is { IsCompleted: false }
                    || activeManagedFolderDeleteTask is { IsCompleted: false }
                    || activeManagedFolderRecoveryTask is { IsCompleted: false }
                    || activeFolderWorkspaceTask is { IsCompleted: false })
                {
                    return false;
                }

                checked
                {
                    currentRevisionReloadAdmissionCount++;
                }

                currentRevisionReloadAdmissionHeld = true;
                return true;
            }
        }

        private void releaseCurrentRevisionReloadAdmission()
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (currentRevisionReloadAdmissionCount <= 0)
                    return;

                currentRevisionReloadAdmissionCount--;
                currentRevisionReloadAdmissionHeld = currentRevisionReloadAdmissionCount > 0;
            }
        }

        private bool tryAdmitCurrentRevisionMutation()
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown || hasActiveFolderMutationHeld())
                    return false;

                currentRevisionMutationAdmissionHeld = true;
                return true;
            }
        }

        private bool isCurrentRevisionMutationAdmitted()
        {
            lock (managedFolderRenameLifecycleGate)
                return currentRevisionMutationAdmissionHeld
                       || currentRevisionReloadAdmissionHeld
                       || realmPackageMutationAdmissionDepth > 0
                       || Volatile.Read(ref currentSkinProjectionInProgress) != 0;
        }

        private void releaseCurrentRevisionMutationAdmission()
        {
            lock (managedFolderRenameLifecycleGate)
                currentRevisionMutationAdmissionHeld = false;
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
                || ManagedFolderOperationCoordinator.IsPathFrozen(normalisedPath))
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

        private enum CurrentRevisionCommitAttempt
        {
            Success,
            ParticipantSetChanged,
            SourceChanged,
            Superseded,
            Cancelled,
            SchedulerFailed,
            Shutdown,
        }

        private enum CurrentRevisionCallbackScheduleResult
        {
            Scheduled,
            Faulted,
            Shutdown,
        }

        private sealed class PendingCurrentRevisionCallback
        {
            private Action callback;
            private Action shutdown;

            public PendingCurrentRevisionCallback(Action callback, Action shutdown)
            {
                this.callback = callback;
                this.shutdown = shutdown;
            }

            public void Run()
            {
                Action claimed = Interlocked.Exchange(ref callback, null);
                Interlocked.Exchange(ref shutdown, null);
                claimed?.Invoke();
            }

            public void Shutdown()
            {
                Action claimed = Interlocked.Exchange(ref shutdown, null);
                Interlocked.Exchange(ref callback, null);
                claimed?.Invoke();
            }

            public void Abandon()
            {
                Interlocked.Exchange(ref shutdown, null);
                Interlocked.Exchange(ref callback, null);
            }
        }

        private sealed class ProtectedFallbackPublicationTransaction
        {
            private SkinCurrentRevisionLease rollbackLease;

            public Live<SkinInfo> PreviousSelection { get; }
            public Skin PreviousOwner { get; }
            public SkinCurrentRevision PreviousRevision { get; }
            public SkinCurrentRevision FallbackRevision { get; }

            public ProtectedFallbackPublicationTransaction(
                Live<SkinInfo> previousSelection,
                Skin previousOwner,
                SkinCurrentRevision previousRevision,
                SkinCurrentRevision fallbackRevision,
                SkinCurrentRevisionLease rollbackLease)
            {
                PreviousSelection = previousSelection;
                PreviousOwner = previousOwner;
                PreviousRevision = previousRevision;
                FallbackRevision = fallbackRevision;
                this.rollbackLease = rollbackLease;
            }

            public void Complete()
                => Interlocked.Exchange(ref rollbackLease, null)?.Dispose();
        }

        private sealed class CurrentExternalUnregisterSnapshot
        {
            private readonly SkinInfo record;
            private readonly SkinExternalFolderRegistryDeclaration[] declarations;

            public Live<SkinInfo> Selection { get; }
            public Skin Owner { get; }
            public SkinCurrentRevision Revision { get; }
            public long SelectionGeneration { get; }

            public CurrentExternalUnregisterSnapshot(
                SkinInfo record,
                SkinExternalFolderRegistryDeclaration[] declarations,
                Live<SkinInfo> selection,
                Skin owner,
                SkinCurrentRevision revision,
                long selectionGeneration)
            {
                this.record = record;
                this.declarations = declarations;
                Selection = selection;
                Owner = owner;
                Revision = revision;
                SelectionGeneration = selectionGeneration;
            }

            public bool MatchesDeclarations(IReadOnlyList<SkinExternalFolderRegistryDeclaration> current)
            {
                if (current.Count != declarations.Length)
                    return false;

                for (int i = 0; i < declarations.Length; i++)
                {
                    if (!declarations[i].ExactlyMatches(current[i]))
                        return false;
                }

                return true;
            }

            public bool MatchesRecord(SkinInfo current)
                => current != null
                   && current.ID == record.ID
                   && string.Equals(current.Name, record.Name, StringComparison.Ordinal)
                   && string.Equals(current.Creator, record.Creator, StringComparison.Ordinal)
                   && string.Equals(current.InstantiationInfo, record.InstantiationInfo, StringComparison.Ordinal)
                   && string.Equals(current.Hash, record.Hash, StringComparison.Ordinal)
                   && current.Protected == record.Protected
                   && string.Equals(current.FilesystemStoragePath, record.FilesystemStoragePath, StringComparison.Ordinal)
                   && current.IsExternalFilesystemStorage == record.IsExternalFilesystemStorage
                   && string.Equals(current.FilesystemStorageAuthorityOwner, record.FilesystemStorageAuthorityOwner, StringComparison.Ordinal)
                   && current.DeletePending == record.DeletePending
                   && current.Files.Count == record.Files.Count;
        }

        private sealed class CurrentRevisionReloadRequest
        {
            public Live<SkinInfo> ExpectedSelection { get; }
            public Skin ExpectedOwner { get; }
            public SkinCurrentRevision ExpectedRevision { get; }
            public long SelectionGeneration { get; }
            public SelectionRequest SourceRequest { get; }
            public RealmPackageRevisionSnapshot RealmSnapshot { get; }

            public CurrentRevisionReloadRequest(
                Live<SkinInfo> expectedSelection,
                Skin expectedOwner,
                SkinCurrentRevision expectedRevision,
                long selectionGeneration,
                SelectionRequest sourceRequest,
                RealmPackageRevisionSnapshot realmSnapshot)
            {
                ExpectedSelection = expectedSelection ?? throw new ArgumentNullException(nameof(expectedSelection));
                ExpectedOwner = expectedOwner ?? throw new ArgumentNullException(nameof(expectedOwner));
                ExpectedRevision = expectedRevision ?? throw new ArgumentNullException(nameof(expectedRevision));
                SelectionGeneration = selectionGeneration;
                SourceRequest = sourceRequest ?? throw new ArgumentNullException(nameof(sourceRequest));
                RealmSnapshot = realmSnapshot;
            }
        }

        private async Task<bool> unregisterCurrentExternalFolderAsync(
            Guid recordId,
            CancellationToken cancellationToken)
        {
            CurrentExternalUnregisterSnapshot snapshot;

            try
            {
                snapshot = captureCurrentExternalUnregisterSnapshot(recordId);
            }
            catch
            {
                return false;
            }

            if (snapshot == null)
                return false;

            ProtectedFallbackPublicationTransaction fallback =
                await publishProtectedFallbackAndWaitForDetachAsync(recordId, cancellationToken).ConfigureAwait(false);

            if (fallback == null)
                return false;

            bool removed = false;

            try
            {
                CurrentExternalUnregisterBeforeRealmCommit();

                if (!ReferenceEquals(fallback.PreviousRevision, snapshot.Revision)
                    || !ReferenceEquals(fallback.PreviousSelection, snapshot.Selection)
                    || !ReferenceEquals(fallback.PreviousOwner, snapshot.Owner)
                    || Interlocked.Read(ref selectionGeneration) != snapshot.SelectionGeneration
                    || !ReferenceEquals(currentRevisionPublication.Current, fallback.FallbackRevision)
                    || CurrentSkinInfo.Value.ID != SkinInfo.OMS_SKIN
                    || !ReferenceEquals(CurrentSkin.Value, DefaultOmsSkin)
                    || !externalFolderRegistry.TryReadAndValidateDeclarations(
                        out SkinExternalFolderRegistryDeclaration[] freshDeclarations,
                        out _,
                        out _,
                        out _)
                    || !snapshot.MatchesDeclarations(freshDeclarations))
                {
                    removed = false;
                }
                else
                {
                    removed = Realm.Write(realm =>
                    {
                        realm.Refresh();

                        if (Interlocked.Read(ref selectionGeneration) != snapshot.SelectionGeneration
                            || !ReferenceEquals(currentRevisionPublication.Current, fallback.FallbackRevision)
                            || CurrentSkinInfo.Value.ID != SkinInfo.OMS_SKIN
                            || !ReferenceEquals(CurrentSkin.Value, DefaultOmsSkin)
                            || !exactlyMatchesExternalDeclarations(realm.All<SkinInfo>(), freshDeclarations))
                        {
                            return false;
                        }

                        SkinInfo current = realm.Find<SkinInfo>(recordId);

                        if (!snapshot.MatchesRecord(current) || !isExactExternalRegistryRecord(current))
                            return false;

                        realm.Remove(current);
                        return realm.Find<SkinInfo>(recordId) == null;
                    });
                }
            }
            catch
            {
                removed = false;
            }

            if (removed)
            {
                fallback.Complete();
                Interlocked.Increment(ref selectionGeneration);
                cancelPendingSelection();
                return true;
            }

            await rollbackProtectedFallbackAsync(fallback).ConfigureAwait(false);
            return false;
        }

        private CurrentExternalUnregisterSnapshot captureCurrentExternalUnregisterSnapshot(Guid recordId)
        {
            Live<SkinInfo> selection = CurrentSkinInfo.Value;
            Skin owner = CurrentSkin.Value;
            SkinCurrentRevision revision = currentRevisionPublication.Current;
            long generation = Interlocked.Read(ref selectionGeneration);

            if (selection.ID != recordId
                || owner.SkinInfo.ID != recordId
                || revision.RecordId != recordId
                || revision.SourceKind != SkinCurrentRevisionSourceKind.ExternalFolder
                || !ReferenceEquals(revision.Owner, owner)
                || !string.Equals(revision.Owner.PackageContentRevision, revision.ContentRevision, StringComparison.Ordinal)
                || !externalFolderRegistry.TryReadAndValidateDeclarations(
                    out SkinExternalFolderRegistryDeclaration[] declarations,
                    out _,
                    out _,
                    out _))
            {
                return null;
            }

            SkinInfo record = Realm.Run(realm =>
            {
                realm.Refresh();
                SkinInfo current = realm.Find<SkinInfo>(recordId);
                return current != null
                       && isExactExternalRegistryRecord(current)
                       && exactlyMatchesExternalDeclarations(realm.All<SkinInfo>(), declarations)
                    ? current.Detach()
                    : null;
            });

            return record == null
                   || Interlocked.Read(ref selectionGeneration) != generation
                   || !ReferenceEquals(CurrentSkinInfo.Value, selection)
                   || !ReferenceEquals(CurrentSkin.Value, owner)
                   || !ReferenceEquals(currentRevisionPublication.Current, revision)
                ? null
                : new CurrentExternalUnregisterSnapshot(
                    record,
                    declarations,
                    selection,
                    owner,
                    revision,
                    generation);
        }

        private sealed class CurrentRevisionReloadPreparation : IDisposable
        {
            private Skin skin;
            private IDisposable authority;
            private readonly Action<CancellationToken> validate;

            public static CurrentRevisionReloadPreparation Reject(SkinCurrentRevisionReloadResult result)
                => new CurrentRevisionReloadPreparation(null, string.Empty, default, result, null, null);

            public bool IsSuccess => skin != null;
            public SkinCurrentRevisionReloadResult FailureResult { get; }
            public string ContentRevision { get; }
            public SkinCurrentRevisionSourceKind SourceKind { get; }

            private CurrentRevisionReloadPreparation(
                Skin skin,
                string contentRevision,
                SkinCurrentRevisionSourceKind sourceKind,
                SkinCurrentRevisionReloadResult failureResult,
                Action<CancellationToken> validate,
                IDisposable authority)
            {
                this.skin = skin;
                ContentRevision = contentRevision;
                SourceKind = sourceKind;
                FailureResult = failureResult;
                this.validate = validate;
                this.authority = authority;
            }

            public static CurrentRevisionReloadPreparation Success(
                Skin skin,
                string contentRevision,
                SkinCurrentRevisionSourceKind sourceKind,
                Action<CancellationToken> validate,
                IDisposable authority = null)
                => new CurrentRevisionReloadPreparation(
                    skin ?? throw new ArgumentNullException(nameof(skin)),
                    contentRevision ?? throw new ArgumentNullException(nameof(contentRevision)),
                    sourceKind,
                    SkinCurrentRevisionReloadResult.Success,
                    validate ?? throw new ArgumentNullException(nameof(validate)),
                    authority);

            public void Validate(CancellationToken cancellationToken) => validate(cancellationToken);

            public Skin TransferSkin()
                => Interlocked.Exchange(ref skin, null)
                   ?? throw new InvalidOperationException("The prepared revision owner was already transferred.");

            public void Dispose()
            {
                Skin ownedSkin = Interlocked.Exchange(ref skin, null);
                IDisposable ownedAuthority = Interlocked.Exchange(ref authority, null);

                try
                {
                    ownedSkin?.Dispose();
                }
                finally
                {
                    ownedAuthority?.Dispose();
                }
            }
        }

        private sealed class RealmPackageFileDeclaration : IEquatable<RealmPackageFileDeclaration>
        {
            public string Filename { get; }
            public string Hash { get; }

            public RealmPackageFileDeclaration(string filename, string hash)
            {
                Filename = filename;
                Hash = hash;
            }

            public bool Equals(RealmPackageFileDeclaration other)
                => other != null
                   && string.Equals(Filename, other.Filename, StringComparison.Ordinal)
                   && string.Equals(Hash, other.Hash, StringComparison.Ordinal);

            public override bool Equals(object obj) => Equals(obj as RealmPackageFileDeclaration);

            public override int GetHashCode() => HashCode.Combine(Filename, Hash);
        }

        private sealed class RealmPackageRevisionSnapshot
        {
            public SkinInfo Metadata { get; }
            public IReadOnlyList<RealmPackageFileDeclaration> Files { get; }

            private RealmPackageRevisionSnapshot(SkinInfo metadata, RealmPackageFileDeclaration[] files)
            {
                Metadata = metadata;
                Files = files;
            }

            public static RealmPackageRevisionSnapshot Create(SkinInfo source)
            {
                var metadata = new SkinInfo
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
                RealmPackageFileDeclaration[] files = source.Files
                                                                  .Select(file => new RealmPackageFileDeclaration(file.Filename, file.File.Hash))
                                                                  .OrderBy(file => file.Filename, StringComparer.Ordinal)
                                                                  .ThenBy(file => file.Hash, StringComparer.Ordinal)
                                                                  .ToArray();
                return new RealmPackageRevisionSnapshot(metadata, files);
            }

            public bool MatchesMetadata(RealmPackageRevisionSnapshot other) => Matches(other);

            public bool Matches(RealmPackageRevisionSnapshot other)
            {
                if (other == null
                    || Metadata.ID != other.Metadata.ID
                    || !string.Equals(Metadata.Name, other.Metadata.Name, StringComparison.Ordinal)
                    || !string.Equals(Metadata.Creator, other.Metadata.Creator, StringComparison.Ordinal)
                    || !string.Equals(Metadata.InstantiationInfo, other.Metadata.InstantiationInfo, StringComparison.Ordinal)
                    || !string.Equals(Metadata.Hash, other.Metadata.Hash, StringComparison.Ordinal)
                    || Metadata.Protected != other.Metadata.Protected
                    || !string.Equals(Metadata.FilesystemStoragePath, other.Metadata.FilesystemStoragePath, StringComparison.Ordinal)
                    || Metadata.IsExternalFilesystemStorage != other.Metadata.IsExternalFilesystemStorage
                    || !string.Equals(Metadata.FilesystemStorageAuthorityOwner, other.Metadata.FilesystemStorageAuthorityOwner, StringComparison.Ordinal)
                    || Metadata.DeletePending != other.Metadata.DeletePending
                    || Files.Count != other.Files.Count)
                {
                    return false;
                }

                for (int i = 0; i < Files.Count; i++)
                {
                    if (!Files[i].Equals(other.Files[i]))
                        return false;
                }

                return true;
            }
        }

        private sealed class ExternalFolderSelectionPreparationResult : IDisposable
        {
            public static ExternalFolderSelectionPreparationResult Reject { get; } =
                new ExternalFolderSelectionPreparationResult(null, null, null, null, null);

            private Skin skin;
            private ISkinManagedFolderMutationNativeSession managedAuthority;
            private SkinExternalFolderRegistrySnapshot registrySnapshot;
            private ISkinExternalPackageCaptureSession packageSession;

            public Skin Skin => skin;
            public SkinManagedFolderPackageMetadata Metadata { get; }
            public ISkinManagedFolderMutationNativeSession ManagedAuthority => managedAuthority;
            public SkinExternalFolderRegistrySnapshot RegistrySnapshot => registrySnapshot;
            public ISkinExternalPackageCaptureSession PackageSession => packageSession;
            public bool IsSuccess => Skin != null;

            private ExternalFolderSelectionPreparationResult(
                Skin skin,
                SkinManagedFolderPackageMetadata metadata,
                ISkinManagedFolderMutationNativeSession managedAuthority,
                SkinExternalFolderRegistrySnapshot registrySnapshot,
                ISkinExternalPackageCaptureSession packageSession)
            {
                this.skin = skin;
                Metadata = metadata;
                this.managedAuthority = managedAuthority;
                this.registrySnapshot = registrySnapshot;
                this.packageSession = packageSession;
            }

            public static ExternalFolderSelectionPreparationResult Success(
                Skin skin,
                SkinManagedFolderPackageMetadata metadata,
                ISkinManagedFolderMutationNativeSession managedAuthority,
                SkinExternalFolderRegistrySnapshot registrySnapshot,
                ISkinExternalPackageCaptureSession packageSession)
                => new ExternalFolderSelectionPreparationResult(
                    skin ?? throw new ArgumentNullException(nameof(skin)),
                    metadata ?? throw new ArgumentNullException(nameof(metadata)),
                    managedAuthority ?? throw new ArgumentNullException(nameof(managedAuthority)),
                    registrySnapshot ?? throw new ArgumentNullException(nameof(registrySnapshot)),
                    packageSession ?? throw new ArgumentNullException(nameof(packageSession)));

            public Skin TransferSkin()
                => Interlocked.Exchange(ref skin, null)
                   ?? throw new InvalidOperationException("The prepared external skin has already been transferred.");

            public void Dispose()
            {
                Skin ownedSkin = Interlocked.Exchange(ref skin, null);
                ISkinExternalPackageCaptureSession ownedPackageSession = Interlocked.Exchange(ref packageSession, null);
                SkinExternalFolderRegistrySnapshot ownedRegistrySnapshot = Interlocked.Exchange(ref registrySnapshot, null);
                ISkinManagedFolderMutationNativeSession ownedManagedAuthority = Interlocked.Exchange(ref managedAuthority, null);

                try
                {
                    ownedSkin?.Dispose();
                }
                finally
                {
                    try
                    {
                        ownedPackageSession?.Dispose();
                    }
                    finally
                    {
                        try
                        {
                            ownedRegistrySnapshot?.Dispose();
                        }
                        finally
                        {
                            ownedManagedAuthority?.Dispose();
                        }
                    }
                }
            }
        }

        private sealed class PendingExternalFolderSelectionCompletion
        {
            public long Generation { get; }
            public Live<SkinInfo> Target { get; }
            public SelectionRequest Request { get; }
            public SkinManagedFolderOperationCoordinator.SelectionPreparationObservation PreparationObservation { get; }
            public CancellationTokenSource Cancellation { get; }
            public Task<ExternalFolderSelectionPreparationResult> PreparationTask { get; }

            public PendingExternalFolderSelectionCompletion(
                long generation,
                Live<SkinInfo> target,
                SelectionRequest request,
                SkinManagedFolderOperationCoordinator.SelectionPreparationObservation preparationObservation,
                CancellationTokenSource cancellation,
                Task<ExternalFolderSelectionPreparationResult> preparationTask)
            {
                Generation = generation;
                Target = target;
                Request = request;
                PreparationObservation = preparationObservation;
                Cancellation = cancellation;
                PreparationTask = preparationTask;
            }
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

        private sealed class FolderWorkspaceReadOperation
        {
            public Task Task { get; }
            public CancellationTokenSource Cancellation { get; }

            public FolderWorkspaceReadOperation(Task task, CancellationTokenSource cancellation)
            {
                Task = task ?? throw new ArgumentNullException(nameof(task));
                Cancellation = cancellation ?? throw new ArgumentNullException(nameof(cancellation));
            }
        }

        private sealed class SelectionRequest
        {
            public SkinFilesystemStorageResolution Resolution { get; }
            public SkinInfo Snapshot { get; }
            public bool IsRealmManaged { get; }
            public bool HasExactScannerOwner { get; }
            public bool HasExactExternalOwner { get; }

            public SelectionRequest(
                SkinFilesystemStorageResolution resolution,
                SkinInfo snapshot,
                bool isRealmManaged,
                bool hasExactScannerOwner,
                bool hasExactExternalOwner)
            {
                Resolution = resolution;
                Snapshot = snapshot;
                IsRealmManaged = isRealmManaged;
                HasExactScannerOwner = hasExactScannerOwner;
                HasExactExternalOwner = hasExactExternalOwner;
            }

            public bool Matches(SkinInfo current, Storage storage)
            {
                if (!MatchesDeclaredFields(current))
                    return false;

                SkinFilesystemStorageResolution currentResolution = SkinFilesystemStorageResolver.ResolveExisting(current, storage);
                return currentResolution.Authority == Resolution.Authority
                       && SkinManagedFolderFactory.IsInstantiationInfoAllowed(current.InstantiationInfo)
                       && (Resolution.Authority switch
                       {
                           SkinFilesystemStorageAuthority.ManagedFolder =>
                               HasExactScannerOwner
                               && string.Equals(
                                   current.FilesystemStorageAuthorityOwner,
                                   SkinManagedFolderScanner.AUTHORITY_OWNER,
                                   StringComparison.Ordinal)
                               && currentResolution.ManagedCaptureRequest != null,

                           SkinFilesystemStorageAuthority.ExternalFolder =>
                               HasExactExternalOwner
                               && current.IsExternalFilesystemStorage
                               && string.Equals(
                                   current.FilesystemStorageAuthorityOwner,
                                   SkinExternalFolderRegistry.AUTHORITY_OWNER,
                                   StringComparison.Ordinal)
                               && currentResolution.ExternalCaptureRequest != null,

                           _ => false,
                       });
            }

            public bool MatchesDeclaredFields(SkinInfo current)
                => Snapshot != null
                   && current.IsManaged
                   && current.ID == Snapshot.ID
                   && string.Equals(current.Name, Snapshot.Name, StringComparison.Ordinal)
                   && string.Equals(current.Creator, Snapshot.Creator, StringComparison.Ordinal)
                   && string.Equals(current.InstantiationInfo, Snapshot.InstantiationInfo, StringComparison.Ordinal)
                   && string.Equals(current.Hash, Snapshot.Hash, StringComparison.Ordinal)
                   && current.Protected == Snapshot.Protected
                   && string.Equals(current.FilesystemStoragePath, Snapshot.FilesystemStoragePath, StringComparison.Ordinal)
                   && current.IsExternalFilesystemStorage == Snapshot.IsExternalFilesystemStorage
                   && string.Equals(current.FilesystemStorageAuthorityOwner, Snapshot.FilesystemStorageAuthorityOwner, StringComparison.Ordinal)
                   && current.DeletePending == Snapshot.DeletePending
                   && current.Files.Count == 0
                   && (Resolution.Authority switch
                   {
                       SkinFilesystemStorageAuthority.ManagedFolder =>
                           HasExactScannerOwner
                           && !current.IsExternalFilesystemStorage
                           && string.Equals(
                               current.FilesystemStorageAuthorityOwner,
                               SkinManagedFolderScanner.AUTHORITY_OWNER,
                               StringComparison.Ordinal),

                       SkinFilesystemStorageAuthority.ExternalFolder =>
                           HasExactExternalOwner
                           && current.IsExternalFilesystemStorage
                           && string.Equals(
                               current.FilesystemStorageAuthorityOwner,
                               SkinExternalFolderRegistry.AUTHORITY_OWNER,
                               StringComparison.Ordinal),

                       _ => false,
                   });
        }

        private enum DeleteCandidateKind
        {
            Rejected,
            RealmPackage,
            ManagedFolder,
        }

        /// <summary>
        /// The single authoritative manager publication. Bindables are notification projections; all manager getters
        /// and resource lookup resolve through this immutable reference so owner, selection and revision cannot be
        /// split by arbitrary post-barrier observers.
        /// </summary>
        private sealed class PublishedCurrentSkinPair
        {
            public Live<SkinInfo> Selection { get; }
            public Skin Owner { get; }
            public SkinCurrentRevision Revision { get; }

            public PublishedCurrentSkinPair(
                Live<SkinInfo> selection,
                Skin owner,
                SkinCurrentRevision revision)
            {
                Selection = selection ?? throw new ArgumentNullException(nameof(selection));
                Owner = owner ?? throw new ArgumentNullException(nameof(owner));
                Revision = revision ?? throw new ArgumentNullException(nameof(revision));
            }
        }

    }
}
