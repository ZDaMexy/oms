// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;

namespace osu.Game.Skinning
{
    internal enum SkinManagedFolderMutationRecoveryDecision
    {
        Ambiguous,
        RollForward,
        RollBack,
        AlreadyCommitted,
        AlreadyRolledBack,
    }

    internal readonly record struct SkinManagedFolderMutationRecoveryInspection(
        SkinManagedFolderMutationRecoveryDecision Decision,
        SkinManagedFolderPhysicalIdentity? ObservedManagedRootIdentity = null,
        SkinManagedFolderPhysicalIdentity? TargetIdentity = null,
        string? NewRecordPublicationFingerprint = null,
        string? StagedSourceTreeFingerprint = null);

    internal readonly record struct SkinManagedFolderMutationRecoveryActionResult(
        bool IsSuccess,
        SkinManagedFolderPhysicalIdentity? ObservedManagedRootIdentity = null,
        SkinManagedFolderPhysicalIdentity? TargetIdentity = null,
        string? NewRecordPublicationFingerprint = null);

    internal interface ISkinManagedFolderMutationRecoveryHandler
    {
        SkinManagedFolderMutationRecoveryInspection Inspect(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken);

        SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken);

        SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Recovery handler surface which consumes the single caller-held native/registry authority session.
    /// </summary>
    internal interface ISkinManagedFolderMutationHeldRecoveryHandler
    {
        bool CanHandle(SkinManagedFolderMutationKind kind);

        SkinManagedFolderMutationRecoveryInspection InspectHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken);

        SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken);

        SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Routes the single canonical mutation journal to an operation-specific production recovery policy.
    /// </summary>
    internal sealed class SkinManagedFolderMutationRecoveryHandlerRouter
        : ISkinManagedFolderMutationRecoveryHandler,
          ISkinManagedFolderMutationHeldRecoveryHandler
    {
        private readonly IReadOnlyDictionary<SkinManagedFolderMutationKind, ISkinManagedFolderMutationRecoveryHandler> handlers;

        public SkinManagedFolderMutationRecoveryHandlerRouter(
            params (SkinManagedFolderMutationKind Kind, ISkinManagedFolderMutationRecoveryHandler Handler)[] handlers)
        {
            ArgumentNullException.ThrowIfNull(handlers);

            var mapped = new Dictionary<SkinManagedFolderMutationKind, ISkinManagedFolderMutationRecoveryHandler>();

            foreach ((SkinManagedFolderMutationKind kind, ISkinManagedFolderMutationRecoveryHandler handler) in handlers)
            {
                if (!Enum.IsDefined(kind)
                    || handler == null
                    || !mapped.TryAdd(kind, handler))
                {
                    throw new ArgumentException("The managed-folder recovery route is invalid.", nameof(handlers));
                }
            }

            this.handlers = mapped;
        }

        public SkinManagedFolderMutationRecoveryInspection Inspect(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => tryGetHandler(journal, out ISkinManagedFolderMutationRecoveryHandler? handler)
                ? handler!.Inspect(journal, cancellationToken)
                : ambiguousInspection();

        public SkinManagedFolderMutationRecoveryActionResult TryRollForward(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => tryGetHandler(journal, out ISkinManagedFolderMutationRecoveryHandler? handler)
                ? handler!.TryRollForward(journal, cancellationToken)
                : failedAction();

        public SkinManagedFolderMutationRecoveryActionResult TryRollBack(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
            => tryGetHandler(journal, out ISkinManagedFolderMutationRecoveryHandler? handler)
                ? handler!.TryRollBack(journal, cancellationToken)
                : failedAction();

        public bool CanHandle(SkinManagedFolderMutationKind kind)
            => handlers.TryGetValue(kind, out ISkinManagedFolderMutationRecoveryHandler? handler)
               && handler is ISkinManagedFolderMutationHeldRecoveryHandler held
               && held.CanHandle(kind);

        public SkinManagedFolderMutationRecoveryInspection InspectHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => tryGetHeldHandler(journal, authority, out ISkinManagedFolderMutationHeldRecoveryHandler? handler)
                ? handler!.InspectHeld(journal, authority, cancellationToken)
                : ambiguousInspection();

        public SkinManagedFolderMutationRecoveryActionResult TryRollForwardHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => tryGetHeldHandler(journal, authority, out ISkinManagedFolderMutationHeldRecoveryHandler? handler)
                ? handler!.TryRollForwardHeld(journal, authority, cancellationToken)
                : failedAction();

        public SkinManagedFolderMutationRecoveryActionResult TryRollBackHeld(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            CancellationToken cancellationToken)
            => tryGetHeldHandler(journal, authority, out ISkinManagedFolderMutationHeldRecoveryHandler? handler)
                ? handler!.TryRollBackHeld(journal, authority, cancellationToken)
                : failedAction();

        private bool tryGetHandler(
            SkinManagedFolderMutationJournal journal,
            out ISkinManagedFolderMutationRecoveryHandler? handler)
        {
            handler = null;
            return journal != null && handlers.TryGetValue(journal.Kind, out handler);
        }

        private bool tryGetHeldHandler(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession authority,
            out ISkinManagedFolderMutationHeldRecoveryHandler? handler)
        {
            handler = null;

            if (journal == null
                || authority == null
                || !handlers.TryGetValue(journal.Kind, out ISkinManagedFolderMutationRecoveryHandler? routed)
                || routed is not ISkinManagedFolderMutationHeldRecoveryHandler held
                || !held.CanHandle(journal.Kind))
            {
                return false;
            }

            handler = held;
            return true;
        }

        private static SkinManagedFolderMutationRecoveryInspection ambiguousInspection()
            => new SkinManagedFolderMutationRecoveryInspection(
                SkinManagedFolderMutationRecoveryDecision.Ambiguous);

        private static SkinManagedFolderMutationRecoveryActionResult failedAction()
            => new SkinManagedFolderMutationRecoveryActionResult(false);

        public override string ToString()
            => $"{nameof(SkinManagedFolderMutationRecoveryHandlerRouter)}:Count={handlers.Count}";
    }

    internal enum SkinManagedFolderMutationRecoveryStatus
    {
        NoJournal,
        RecoveredForward,
        RecoveredRollback,
        RemovedTerminalJournal,
        Ambiguous,
        InvalidJournal,
        UnsupportedJournal,
        JournalIoFailure,
    }

    /// <summary>
    /// Non-sensitive outcome of the startup recovery pass.
    /// </summary>
    internal sealed class SkinManagedFolderMutationRecoveryResult
    {
        public SkinManagedFolderMutationRecoveryStatus Status { get; }

        public bool IsResolved => Status is SkinManagedFolderMutationRecoveryStatus.NoJournal
            or SkinManagedFolderMutationRecoveryStatus.RecoveredForward
            or SkinManagedFolderMutationRecoveryStatus.RecoveredRollback
            or SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal;

        public SkinManagedFolderMutationRecoveryResult(SkinManagedFolderMutationRecoveryStatus status)
        {
            Status = status;
        }

        public override string ToString() => $"{nameof(SkinManagedFolderMutationRecoveryResult)}:{Status}";
    }

    /// <summary>
    /// Recovers one journal before managed-folder scanning is allowed to reconcile Realm records.
    /// </summary>
    /// <remarks>
    /// An operation-specific handler is injected only after that vertical slice passes its own gate. Rename now has a
    /// production handler; unimplemented kinds remain ambiguous and freeze their exact paths. Invalid or unknown
    /// journal data freezes the whole managed namespace because no safe path set can be derived.
    /// </remarks>
    internal sealed class SkinManagedFolderMutationRecovery
    {
        private readonly ISkinManagedFolderMutationJournalStore journalStore;
        private readonly SkinManagedFolderOperationCoordinator coordinator;
        private readonly ISkinManagedFolderMutationRecoveryHandler? handler;
        private readonly ISkinManagedFolderMutationRecoveryAuthority? recoveryAuthority;
        private SkinManagedFolderMutationJournal? unresolvedIntent;
        private bool globalUnresolved;

        public SkinManagedFolderMutationRecovery(
            ISkinManagedFolderMutationJournalStore journalStore,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationRecoveryHandler? handler = null,
            ISkinManagedFolderMutationRecoveryAuthority? recoveryAuthority = null)
        {
            this.journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.handler = handler;
            this.recoveryAuthority = recoveryAuthority;
        }

        public SkinManagedFolderMutationRecoveryResult Recover(CancellationToken cancellationToken = default)
        {
            if (recoveryAuthority == null)
                return coordinator.RunExclusive(() => recover(null, cancellationToken), cancellationToken);

            using SkinManagedFolderOperationCoordinator.Lease lease =
                coordinator.EnterMutation(cancellationToken);
            return recover(lease, cancellationToken);
        }

        /// <summary>
        /// Inspects the canonical journal without performing recovery, deleting a journal or changing freeze state.
        /// The returned projection is deliberately redacted and is safe for settings/support UI.
        /// </summary>
        public FolderSkinJournalSupportSnapshot InspectSupportSnapshot(
            CancellationToken cancellationToken = default)
        {
            if (recoveryAuthority == null)
            {
                return coordinator.RunExclusive(
                    () => inspectSupportSnapshot(null, cancellationToken),
                    cancellationToken);
            }

            using SkinManagedFolderOperationCoordinator.Lease lease =
                coordinator.EnterMutation(cancellationToken);
            return inspectSupportSnapshot(lease, cancellationToken);
        }

        private FolderSkinJournalSupportSnapshot inspectSupportSnapshot(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();
            cancellationToken.ThrowIfCancellationRequested();

            switch (loaded.Status)
            {
                case SkinManagedFolderMutationJournalLoadStatus.Missing:
                    return supportSnapshot(
                        "No pending recovery",
                        "No managed folder operation needs recovery.",
                        "missing",
                        canRetry: false);

                case SkinManagedFolderMutationJournalLoadStatus.UnsupportedVersion:
                    return supportSnapshot(
                        "Recovery needs support",
                        "The recovery record was written by an unsupported version. Repair or update this installation.",
                        "unsupported",
                        canRetry: false);

                case SkinManagedFolderMutationJournalLoadStatus.Invalid:
                    return supportSnapshot(
                        "Recovery needs support",
                        "The recovery record is invalid. Repair this installation before changing folder skins.",
                        "invalid",
                        canRetry: false);

                case SkinManagedFolderMutationJournalLoadStatus.IoFailure:
                    return supportSnapshot(
                        "Recovery unavailable",
                        "The recovery record could not be inspected. Check storage access and retry after the cause is resolved.",
                        "io-failure",
                        canRetry: false);

                case SkinManagedFolderMutationJournalLoadStatus.Loaded:
                    break;

                default:
                    return supportSnapshot(
                        "Recovery needs support",
                        "The recovery state is not recognised.",
                        "unknown",
                        canRetry: false);
            }

            SkinManagedFolderMutationJournal journal = loaded.Journal!;

            if (recoveryAuthority != null)
            {
                if (coordinatorLease == null
                    || handler is not ISkinManagedFolderMutationHeldRecoveryHandler heldHandler
                    || !heldHandler.CanHandle(journal.Kind))
                {
                    return supportSnapshot(
                        "Recovery needs support",
                        "No recovery handler is available for this operation.",
                        "handler-unavailable",
                        canRetry: false,
                        journal);
                }

                using ISkinManagedFolderMutationRecoveryAuthoritySession? authority =
                    tryOpenExactAuthority(coordinatorLease, journal, cancellationToken);

                if (authority == null)
                {
                    return supportSnapshot(
                        "Recovery needs support",
                        "The pending operation authority no longer matches its durable recovery record.",
                        "authority-mismatch",
                        canRetry: false,
                        journal);
                }

                if (journal.Phase is SkinManagedFolderMutationPhase.Committed
                    or SkinManagedFolderMutationPhase.RolledBack)
                {
                    return supportSnapshot(
                        "Recovery cleanup pending",
                        "The operation is terminal and its recovery record can be cleaned up safely.",
                        "terminal",
                        canRetry: true,
                        journal);
                }

                SkinManagedFolderMutationRecoveryInspection heldInspection;

                try
                {
                    heldInspection = heldHandler.InspectHeld(
                        journal,
                        authority,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return supportSnapshot(
                        "Recovery needs support",
                        "The pending operation could not be inspected safely.",
                        "inspection-failed",
                        canRetry: false,
                        journal);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!authority.Validate(cancellationToken))
                {
                    return supportSnapshot(
                        "Recovery needs support",
                        "The pending operation authority changed during inspection.",
                        "authority-drift",
                        canRetry: false,
                        journal);
                }

                return supportSnapshotForInspection(journal, heldInspection);
            }

            if (journal.Phase is SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack)
            {
                return supportSnapshot(
                    "Recovery cleanup pending",
                    "The operation is terminal and its recovery record can be cleaned up safely.",
                    "terminal",
                    canRetry: true,
                    journal);
            }

            if (handler == null)
            {
                return supportSnapshot(
                    "Recovery needs support",
                    "No recovery handler is available for this operation.",
                    "handler-unavailable",
                    canRetry: false,
                    journal);
            }

            SkinManagedFolderMutationRecoveryInspection inspection;

            try
            {
                inspection = handler.Inspect(journal, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Native exception text is intentionally discarded at this boundary.
                return supportSnapshot(
                    "Recovery needs support",
                    "The pending operation could not be inspected safely.",
                    "inspection-failed",
                    canRetry: false,
                    journal);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return supportSnapshotForInspection(journal, inspection);
        }

        private static FolderSkinJournalSupportSnapshot supportSnapshotForInspection(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryInspection inspection)
        {
            bool exactRoot = inspection.ObservedManagedRootIdentity == journal.ManagedRootIdentity;
            bool canRetry = exactRoot && (journal.Kind == SkinManagedFolderMutationKind.ManagedCopy
                ? inspection.Decision switch
                {
                    SkinManagedFolderMutationRecoveryDecision.RollForward or
                        SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted =>
                        isExactManagedCopyForwardEvidence(
                            journal,
                            inspection,
                            requireDurableTree: journal.Phase != SkinManagedFolderMutationPhase.Copying),

                    SkinManagedFolderMutationRecoveryDecision.RollBack or
                        SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack => true,

                    _ => false,
                }
                : inspection.Decision switch
                {
                    SkinManagedFolderMutationRecoveryDecision.RollForward or
                        SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted =>
                        isExactForwardEvidence(journal, inspection),

                    SkinManagedFolderMutationRecoveryDecision.RollBack or
                        SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack => true,

                    _ => false,
                });

            return canRetry
                ? supportSnapshot(
                    "Recovery ready",
                    "A single safe recovery action is available. Retry will re-inspect the operation before changing anything.",
                    "retryable",
                    canRetry: true,
                    journal)
                : supportSnapshot(
                    "Recovery needs support",
                    "The pending operation has no unique safe recovery action.",
                    "ambiguous",
                    canRetry: false,
                    journal);
        }

        private static FolderSkinJournalSupportSnapshot supportSnapshot(
            string status,
            string reason,
            string stableState,
            bool canRetry,
            SkinManagedFolderMutationJournal? journal = null)
        {
            string bundle = journal == null
                ? $"component=folder-skin-journal\nstate={stableState}\nretry={(canRetry ? "available" : "unavailable")}"
                : $"component=folder-skin-journal\nstate={stableState}\nversion={journal.Version}\nkind={journal.Kind}\nphase={journal.Phase}\nretry={(canRetry ? "available" : "unavailable")}";

            return new FolderSkinJournalSupportSnapshot(status, reason, bundle, canRetry);
        }

        private SkinManagedFolderMutationRecoveryResult recover(
            SkinManagedFolderOperationCoordinator.Lease? coordinatorLease,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();
            cancellationToken.ThrowIfCancellationRequested();

            switch (loaded.Status)
            {
                case SkinManagedFolderMutationJournalLoadStatus.Missing:
                    return globalUnresolved || unresolvedIntent != null || coordinator.HasRecoveryFreeze
                        ? result(SkinManagedFolderMutationRecoveryStatus.Ambiguous)
                        : result(SkinManagedFolderMutationRecoveryStatus.NoJournal);

                case SkinManagedFolderMutationJournalLoadStatus.UnsupportedVersion:
                    globalUnresolved = true;
                    coordinator.FreezeAllRecoveryPaths();
                    return result(SkinManagedFolderMutationRecoveryStatus.UnsupportedJournal);

                case SkinManagedFolderMutationJournalLoadStatus.Invalid:
                    globalUnresolved = true;
                    coordinator.FreezeAllRecoveryPaths();
                    return result(SkinManagedFolderMutationRecoveryStatus.InvalidJournal);

                case SkinManagedFolderMutationJournalLoadStatus.IoFailure:
                    globalUnresolved = true;
                    coordinator.FreezeAllRecoveryPaths();
                    return result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);

                case SkinManagedFolderMutationJournalLoadStatus.Loaded:
                    break;

                default:
                    globalUnresolved = true;
                    coordinator.FreezeAllRecoveryPaths();
                    return result(SkinManagedFolderMutationRecoveryStatus.InvalidJournal);
            }

            SkinManagedFolderMutationJournal journal = loaded.Journal!;

            if (globalUnresolved
                || (unresolvedIntent != null && !unresolvedIntent.IsSameMonotonicIntent(journal)))
            {
                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            unresolvedIntent = journal;
            coordinator.FreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());

            if (recoveryAuthority == null)
                return recoverLoadedJournal(journal, null, cancellationToken);

            if (coordinatorLease == null
                || (journal.Phase is not (SkinManagedFolderMutationPhase.Committed
                        or SkinManagedFolderMutationPhase.RolledBack)
                    && (handler is not ISkinManagedFolderMutationHeldRecoveryHandler heldHandler
                        || !heldHandler.CanHandle(journal.Kind))))
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            using ISkinManagedFolderMutationRecoveryAuthoritySession? authority =
                tryOpenExactAuthority(coordinatorLease, journal, cancellationToken);

            return authority == null
                ? result(SkinManagedFolderMutationRecoveryStatus.Ambiguous)
                : recoverLoadedJournal(journal, authority, cancellationToken);
        }

        private SkinManagedFolderMutationRecoveryResult recoverLoadedJournal(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {

            if (journal.Phase is SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack)
            {
                return terminalRemovalResult(
                    tryRemoveTerminalJournal(journal, authority, cancellationToken),
                    SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal);
            }

            if (handler == null)
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);


            if (!tryCallInspect(journal, authority, cancellationToken, out var inspection))
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

            cancellationToken.ThrowIfCancellationRequested();

            if (authority != null && !authority.Validate(cancellationToken))
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

            if (inspection.Decision != SkinManagedFolderMutationRecoveryDecision.Ambiguous
                && inspection.ObservedManagedRootIdentity != journal.ManagedRootIdentity)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            return inspection.Decision switch
            {
                SkinManagedFolderMutationRecoveryDecision.RollForward =>
                    recoverForward(
                        journal,
                        inspection,
                        authority,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.RollBack =>
                    recoverRollback(
                        journal,
                        authority,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted =>
                    recoverForward(
                        journal,
                        inspection,
                        authority,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack =>
                    persistRolledBackAndRemove(
                        journal,
                        null,
                        null,
                        authority,
                        cancellationToken),

                _ => result(SkinManagedFolderMutationRecoveryStatus.Ambiguous),
            };
        }

        private SkinManagedFolderMutationRecoveryResult recoverForward(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryInspection inspection,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            if (journal.Kind == SkinManagedFolderMutationKind.ManagedCopy)
            {
                return recoverManagedCopyForward(
                    journal,
                    inspection,
                    authority,
                    cancellationToken);
            }

            if (!isExactForwardEvidence(journal, inspection))
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

            SkinManagedFolderMutationJournal current = journal;

            if (current.Phase == SkinManagedFolderMutationPhase.Prepared)
            {
                SkinManagedFolderMutationJournal filesystemApplied;

                try
                {
                    filesystemApplied = current.WithFilesystemApplied(
                        inspection.TargetIdentity,
                        inspection.NewRecordPublicationFingerprint);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                AuthorityProtectedMutationStatus persisted =
                    tryPersistAndConfirm(filesystemApplied, authority, cancellationToken);

                if (persisted != AuthorityProtectedMutationStatus.Success)
                    return protectedMutationFailure(persisted);

                current = filesystemApplied;
                cancellationToken.ThrowIfCancellationRequested();

                if (!tryInspect(current, authority, cancellationToken, out inspection)
                    || !isExactForwardEvidence(current, inspection))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }
            }

            if (inspection.Decision == SkinManagedFolderMutationRecoveryDecision.RollForward)
            {

                if (!tryCallRollForward(current, authority, cancellationToken, out var actionResult))
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

                if (!actionResult.IsSuccess
                    || actionResult.ObservedManagedRootIdentity != current.ManagedRootIdentity
                    || !isExactForwardEvidence(
                        current,
                        actionResult.TargetIdentity,
                        actionResult.NewRecordPublicationFingerprint))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!tryInspect(current, authority, cancellationToken, out inspection)
                    || inspection.Decision != SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted
                    || !isExactForwardEvidence(current, inspection))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }
            }
            else if (inspection.Decision != SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            if (current.Phase == SkinManagedFolderMutationPhase.FilesystemApplied)
            {
                SkinManagedFolderMutationJournal realmApplied;

                try
                {
                    realmApplied = current.WithRealmApplied();
                }
                catch (InvalidOperationException)
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                AuthorityProtectedMutationStatus persisted =
                    tryPersistAndConfirm(realmApplied, authority, cancellationToken);

                if (persisted != AuthorityProtectedMutationStatus.Success)
                    return protectedMutationFailure(persisted);

                current = realmApplied;
                cancellationToken.ThrowIfCancellationRequested();

                if (!tryInspect(current, authority, cancellationToken, out inspection)
                    || inspection.Decision != SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted
                    || !isExactForwardEvidence(current, inspection))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }
            }
            else if (current.Phase != SkinManagedFolderMutationPhase.RealmApplied)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            SkinManagedFolderMutationJournal committed;

            try
            {
                committed = current.WithCommitted();
            }
            catch (InvalidOperationException)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            AuthorityProtectedMutationStatus committedPersist =
                tryPersistAndConfirm(committed, authority, cancellationToken);

            if (committedPersist != AuthorityProtectedMutationStatus.Success)
                return protectedMutationFailure(committedPersist);

            return terminalRemovalResult(
                tryRemoveTerminalJournal(committed, authority, cancellationToken),
                SkinManagedFolderMutationRecoveryStatus.RecoveredForward);
        }

        private SkinManagedFolderMutationRecoveryResult recoverManagedCopyForward(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryInspection inspection,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            if (authority == null
                || journal.Kind != SkinManagedFolderMutationKind.ManagedCopy
                || journal.Phase is SkinManagedFolderMutationPhase.Prepared
                    or SkinManagedFolderMutationPhase.Committed
                    or SkinManagedFolderMutationPhase.RolledBack)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            SkinManagedFolderMutationJournal current = journal;

            if (current.Phase == SkinManagedFolderMutationPhase.Copying)
            {
                if (inspection.Decision != SkinManagedFolderMutationRecoveryDecision.RollForward
                    || !isExactManagedCopyForwardEvidence(current, inspection, requireDurableTree: false)
                    || inspection.StagedSourceTreeFingerprint == null)
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                SkinManagedFolderMutationJournal provisionalReady;

                try
                {
                    provisionalReady = current.WithProvisionalReady(
                        current.StagedSourceIdentity!.Value,
                        inspection.StagedSourceTreeFingerprint,
                        inspection.NewRecordPublicationFingerprint!);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                AuthorityProtectedMutationStatus persisted =
                    tryPersistAndConfirm(provisionalReady, authority, cancellationToken);

                if (persisted != AuthorityProtectedMutationStatus.Success)
                    return protectedMutationFailure(persisted);

                current = provisionalReady;

                if (!tryInspect(current, authority, cancellationToken, out inspection))
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            if (current.Phase == SkinManagedFolderMutationPhase.ProvisionalReady)
            {
                if (inspection.Decision != SkinManagedFolderMutationRecoveryDecision.RollForward
                    || !isExactManagedCopyForwardEvidence(current, inspection, requireDurableTree: true)
                    || !tryCallRollForward(current, authority, cancellationToken, out SkinManagedFolderMutationRecoveryActionResult moved)
                    || !isExactManagedCopyForwardAction(current, moved))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                SkinManagedFolderMutationJournal filesystemApplied;

                try
                {
                    filesystemApplied = current.WithFilesystemApplied(
                        moved.TargetIdentity,
                        moved.NewRecordPublicationFingerprint);
                }
                catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                AuthorityProtectedMutationStatus persisted =
                    tryPersistAndConfirm(filesystemApplied, authority, cancellationToken);

                if (persisted != AuthorityProtectedMutationStatus.Success)
                    return protectedMutationFailure(persisted);

                current = filesystemApplied;

                if (!tryInspect(current, authority, cancellationToken, out inspection))
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            if (current.Phase == SkinManagedFolderMutationPhase.FilesystemApplied)
            {
                if (!isExactManagedCopyForwardEvidence(current, inspection, requireDurableTree: true))
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

                if (inspection.Decision == SkinManagedFolderMutationRecoveryDecision.RollForward)
                {
                    if (!tryCallRollForward(current, authority, cancellationToken, out SkinManagedFolderMutationRecoveryActionResult published)
                        || !isExactManagedCopyForwardAction(current, published)
                        || !tryInspect(current, authority, cancellationToken, out inspection))
                    {
                        return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                    }
                }

                if (inspection.Decision != SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted
                    || !isExactManagedCopyForwardEvidence(current, inspection, requireDurableTree: true))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                SkinManagedFolderMutationJournal realmApplied;

                try
                {
                    realmApplied = current.WithRealmApplied();
                }
                catch (InvalidOperationException)
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

                AuthorityProtectedMutationStatus persisted =
                    tryPersistAndConfirm(realmApplied, authority, cancellationToken);

                if (persisted != AuthorityProtectedMutationStatus.Success)
                    return protectedMutationFailure(persisted);

                current = realmApplied;

                if (!tryInspect(current, authority, cancellationToken, out inspection))
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            if (current.Phase != SkinManagedFolderMutationPhase.RealmApplied
                || inspection.Decision != SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted
                || !isExactManagedCopyForwardEvidence(current, inspection, requireDurableTree: true))
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            SkinManagedFolderMutationJournal committed;

            try
            {
                committed = current.WithCommitted();
            }
            catch (InvalidOperationException)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            AuthorityProtectedMutationStatus committedPersist =
                tryPersistAndConfirm(committed, authority, cancellationToken);

            if (committedPersist != AuthorityProtectedMutationStatus.Success)
                return protectedMutationFailure(committedPersist);

            return terminalRemovalResult(
                tryRemoveTerminalJournal(committed, authority, cancellationToken),
                SkinManagedFolderMutationRecoveryStatus.RecoveredForward);
        }

        private SkinManagedFolderMutationRecoveryResult recoverRollback(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {

            if (!tryCallRollBack(journal, authority, cancellationToken, out var actionResult))
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

            if (!actionResult.IsSuccess
                || actionResult.ObservedManagedRootIdentity != journal.ManagedRootIdentity)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return persistRolledBackAndRemove(
                journal,
                actionResult.TargetIdentity,
                actionResult.NewRecordPublicationFingerprint,
                authority,
                cancellationToken);
        }

        private SkinManagedFolderMutationRecoveryResult persistRolledBackAndRemove(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderPhysicalIdentity? recoveredTargetIdentity,
            string? recoveredNewRecordPublicationFingerprint,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderMutationJournal rolledBack;

            try
            {
                rolledBack = journal.WithRecoveryTerminalPhase(
                    SkinManagedFolderMutationPhase.RolledBack,
                    recoveredTargetIdentity,
                    recoveredNewRecordPublicationFingerprint);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            AuthorityProtectedMutationStatus persisted =
                tryPersistAndConfirm(rolledBack, authority, cancellationToken);

            if (persisted != AuthorityProtectedMutationStatus.Success)
                return protectedMutationFailure(persisted);

            return terminalRemovalResult(
                tryRemoveTerminalJournal(rolledBack, authority, cancellationToken),
                SkinManagedFolderMutationRecoveryStatus.RecoveredRollback);
        }

        private bool tryInspect(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken,
            out SkinManagedFolderMutationRecoveryInspection inspection)
        {
            if (!tryCallInspect(journal, authority, cancellationToken, out inspection))
                return false;

            return inspection.Decision != SkinManagedFolderMutationRecoveryDecision.Ambiguous
                   && inspection.ObservedManagedRootIdentity == journal.ManagedRootIdentity;
        }

        private bool tryCallInspect(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken,
            out SkinManagedFolderMutationRecoveryInspection inspection)
        {
            inspection = default;

            try
            {
                if (authority == null)
                {
                    inspection = handler!.Inspect(journal, cancellationToken);
                    return true;
                }

                if (!authority.Validate(cancellationToken)
                    || handler is not ISkinManagedFolderMutationHeldRecoveryHandler held
                    || !held.CanHandle(journal.Kind))
                {
                    return false;
                }

                inspection = held.InspectHeld(journal, authority, cancellationToken);
                return authority.Validate(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                inspection = default;
                return false;
            }
        }

        private bool tryCallRollForward(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken,
            out SkinManagedFolderMutationRecoveryActionResult actionResult)
        {
            actionResult = default;

            try
            {
                if (authority == null)
                {
                    actionResult = handler!.TryRollForward(journal, cancellationToken);
                    return true;
                }

                if (!authority.Validate(cancellationToken)
                    || handler is not ISkinManagedFolderMutationHeldRecoveryHandler held
                    || !held.CanHandle(journal.Kind))
                {
                    return false;
                }

                actionResult = held.TryRollForwardHeld(journal, authority, cancellationToken);
                return authority.Validate(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                actionResult = default;
                return false;
            }
        }

        private bool tryCallRollBack(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken,
            out SkinManagedFolderMutationRecoveryActionResult actionResult)
        {
            actionResult = default;

            try
            {
                if (authority == null)
                {
                    actionResult = handler!.TryRollBack(journal, cancellationToken);
                    return true;
                }

                if (!authority.Validate(cancellationToken)
                    || handler is not ISkinManagedFolderMutationHeldRecoveryHandler held
                    || !held.CanHandle(journal.Kind))
                {
                    return false;
                }

                actionResult = held.TryRollBackHeld(journal, authority, cancellationToken);
                return authority.Validate(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                actionResult = default;
                return false;
            }
        }

        private ISkinManagedFolderMutationRecoveryAuthoritySession? tryOpenExactAuthority(
            SkinManagedFolderOperationCoordinator.Lease coordinatorLease,
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority = null;

            try
            {
                authority = recoveryAuthority!.TryOpen(coordinatorLease, cancellationToken);

                if (authority == null
                    || !authority.IsExactFor(journal)
                    || !authority.Validate(cancellationToken))
                {
                    authority?.Dispose();
                    return null;
                }

                return authority;
            }
            catch (OperationCanceledException)
            {
                authority?.Dispose();
                throw;
            }
            catch
            {
                authority?.Dispose();
                return null;
            }
        }

        private static bool isExactForwardEvidence(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryInspection inspection)
            => inspection.Decision is SkinManagedFolderMutationRecoveryDecision.RollForward
                    or SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted
               && inspection.ObservedManagedRootIdentity == journal.ManagedRootIdentity
               && isExactForwardEvidence(
                   journal,
                   inspection.TargetIdentity,
                   inspection.NewRecordPublicationFingerprint);

        private static bool isExactManagedCopyForwardEvidence(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryInspection inspection,
            bool requireDurableTree)
        {
            if (journal.Kind != SkinManagedFolderMutationKind.ManagedCopy
                || inspection.ObservedManagedRootIdentity != journal.ManagedRootIdentity
                || inspection.TargetIdentity != journal.StagedSourceIdentity
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    inspection.NewRecordPublicationFingerprint)
                || !SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                    inspection.StagedSourceTreeFingerprint))
            {
                return false;
            }

            return !requireDurableTree
                   || (string.Equals(
                           inspection.NewRecordPublicationFingerprint,
                           journal.NewRecordPublicationFingerprint,
                           StringComparison.Ordinal)
                       && string.Equals(
                           inspection.StagedSourceTreeFingerprint,
                           journal.StagedSourceTreeFingerprint,
                           StringComparison.Ordinal));
        }

        private static bool isExactManagedCopyForwardAction(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryActionResult action)
            => action.IsSuccess
               && action.ObservedManagedRootIdentity == journal.ManagedRootIdentity
               && action.TargetIdentity == journal.StagedSourceIdentity
               && string.Equals(
                   action.NewRecordPublicationFingerprint,
                   journal.NewRecordPublicationFingerprint,
                   StringComparison.Ordinal);

        private static bool isExactForwardEvidence(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderPhysicalIdentity? targetIdentity,
            string? publicationFingerprint)
        {
            if (journal.TargetIdentity != null
                && journal.TargetIdentity != targetIdentity)
            {
                return false;
            }

            if (journal.NewRecordPublicationFingerprint != null
                && !string.Equals(
                    journal.NewRecordPublicationFingerprint,
                    publicationFingerprint,
                    StringComparison.Ordinal))
            {
                return false;
            }

            return journal.Kind switch
            {
                SkinManagedFolderMutationKind.Rename =>
                    targetIdentity == journal.SourceIdentity
                    && publicationFingerprint == null,

                SkinManagedFolderMutationKind.StagedImport =>
                    targetIdentity == journal.StagedSourceIdentity
                    && SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(
                        publicationFingerprint),

                SkinManagedFolderMutationKind.Delete =>
                    targetIdentity == null
                    && SkinManagedFolderNewRecordPublicationData.IsValidFingerprint(publicationFingerprint)
                    && string.Equals(
                        publicationFingerprint,
                        journal.NewRecordPublicationFingerprint,
                        StringComparison.Ordinal),

                _ => false,
            };
        }

        private AuthorityProtectedMutationStatus tryPersistAndConfirm(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            if (!validateAuthority(authority, cancellationToken))
                return AuthorityProtectedMutationStatus.AuthorityRejected;

            try
            {
                journalStore.Write(journal);
                SkinManagedFolderMutationJournalLoadResult loaded =
                    journalStore.Load();

                if (!loaded.IsLoaded
                    || !loaded.Journal!.IsExactSameJournal(journal))
                {
                    throw new SkinManagedFolderMutationJournalException();
                }

                unresolvedIntent = journal;

                return validateAuthority(authority, cancellationToken)
                    ? AuthorityProtectedMutationStatus.Success
                    : AuthorityProtectedMutationStatus.AuthorityRejected;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return AuthorityProtectedMutationStatus.IoFailure;
            }
        }

        private AuthorityProtectedMutationStatus tryRemoveTerminalJournal(
            SkinManagedFolderMutationJournal journal,
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
        {
            try
            {
                if (!validateAuthority(authority, cancellationToken))
                    return AuthorityProtectedMutationStatus.AuthorityRejected;

                journalStore.Delete(journal);
                SkinManagedFolderMutationJournalLoadResult afterDelete = journalStore.Load();

                if (afterDelete.Status == SkinManagedFolderMutationJournalLoadStatus.Missing)
                {
                    coordinator.UnfreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());
                    unresolvedIntent = null;
                    return !globalUnresolved && !coordinator.HasRecoveryFreeze
                        ? AuthorityProtectedMutationStatus.Success
                        : AuthorityProtectedMutationStatus.IoFailure;
                }

                if (afterDelete.IsLoaded
                    && afterDelete.Journal!.IsExactSameJournal(journal))
                {
                    return AuthorityProtectedMutationStatus.IoFailure;
                }

                if (afterDelete.Status == SkinManagedFolderMutationJournalLoadStatus.IoFailure)
                    return AuthorityProtectedMutationStatus.IoFailure;

                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return AuthorityProtectedMutationStatus.IoFailure;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return AuthorityProtectedMutationStatus.IoFailure;
            }
        }

        private static bool validateAuthority(
            ISkinManagedFolderMutationRecoveryAuthoritySession? authority,
            CancellationToken cancellationToken)
            => authority?.Validate(cancellationToken) ?? true;

        private static SkinManagedFolderMutationRecoveryResult protectedMutationFailure(
            AuthorityProtectedMutationStatus status)
            => result(status == AuthorityProtectedMutationStatus.AuthorityRejected
                ? SkinManagedFolderMutationRecoveryStatus.Ambiguous
                : SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);

        private static SkinManagedFolderMutationRecoveryResult terminalRemovalResult(
            AuthorityProtectedMutationStatus status,
            SkinManagedFolderMutationRecoveryStatus successStatus)
            => status == AuthorityProtectedMutationStatus.Success
                ? result(successStatus)
                : protectedMutationFailure(status);

        private enum AuthorityProtectedMutationStatus
        {
            Success,
            AuthorityRejected,
            IoFailure,
        }

        private static SkinManagedFolderMutationRecoveryResult result(SkinManagedFolderMutationRecoveryStatus status)
            => new SkinManagedFolderMutationRecoveryResult(status);
    }
}
