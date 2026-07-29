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
        string? NewRecordPublicationFingerprint = null);

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
    /// Routes the single canonical mutation journal to an operation-specific production recovery policy.
    /// </summary>
    internal sealed class SkinManagedFolderMutationRecoveryHandlerRouter : ISkinManagedFolderMutationRecoveryHandler
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

        private bool tryGetHandler(
            SkinManagedFolderMutationJournal journal,
            out ISkinManagedFolderMutationRecoveryHandler? handler)
        {
            handler = null;
            return journal != null && handlers.TryGetValue(journal.Kind, out handler);
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
        private SkinManagedFolderMutationJournal? unresolvedIntent;
        private SkinManagedFolderMutationJournal? terminalDeletionAwaitingConfirmation;
        private bool globalUnresolved;

        public SkinManagedFolderMutationRecovery(
            ISkinManagedFolderMutationJournalStore journalStore,
            SkinManagedFolderOperationCoordinator coordinator,
            ISkinManagedFolderMutationRecoveryHandler? handler = null)
        {
            this.journalStore = journalStore ?? throw new ArgumentNullException(nameof(journalStore));
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
            this.handler = handler;
        }

        public SkinManagedFolderMutationRecoveryResult Recover(CancellationToken cancellationToken = default)
            => coordinator.RunExclusive(() => recover(cancellationToken), cancellationToken);

        private SkinManagedFolderMutationRecoveryResult recover(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkinManagedFolderMutationJournalLoadResult loaded = journalStore.Load();
            cancellationToken.ThrowIfCancellationRequested();

            switch (loaded.Status)
            {
                case SkinManagedFolderMutationJournalLoadStatus.Missing:
                    if (!globalUnresolved && terminalDeletionAwaitingConfirmation != null)
                    {
                        coordinator.UnfreezeRecoveryPaths(
                            terminalDeletionAwaitingConfirmation.GetAffectedManagedRelativePaths());
                        unresolvedIntent = null;
                        terminalDeletionAwaitingConfirmation = null;

                        return coordinator.HasRecoveryFreeze
                            ? result(SkinManagedFolderMutationRecoveryStatus.Ambiguous)
                            : result(SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal);
                    }

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

            if (journal.Phase is SkinManagedFolderMutationPhase.Committed or SkinManagedFolderMutationPhase.RolledBack)
            {
                return tryRemoveTerminalJournal(journal)
                    ? result(SkinManagedFolderMutationRecoveryStatus.RemovedTerminalJournal)
                    : result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);
            }

            if (handler == null)
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

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
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            cancellationToken.ThrowIfCancellationRequested();

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
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.RollBack =>
                    recoverRollback(
                        journal,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted =>
                    recoverForward(
                        journal,
                        inspection,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack =>
                    persistRolledBackAndRemove(
                        journal,
                        null,
                        null),

                _ => result(SkinManagedFolderMutationRecoveryStatus.Ambiguous),
            };
        }

        private SkinManagedFolderMutationRecoveryResult recoverForward(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationRecoveryInspection inspection,
            CancellationToken cancellationToken)
        {
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

                if (!tryPersistAndConfirm(filesystemApplied))
                    return result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);

                current = filesystemApplied;
                cancellationToken.ThrowIfCancellationRequested();

                if (!tryInspect(current, cancellationToken, out inspection)
                    || !isExactForwardEvidence(current, inspection))
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }
            }

            if (inspection.Decision == SkinManagedFolderMutationRecoveryDecision.RollForward)
            {
                SkinManagedFolderMutationRecoveryActionResult actionResult;

                try
                {
                    actionResult = handler!.TryRollForward(current, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
                }

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

                if (!tryInspect(current, cancellationToken, out inspection)
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

                if (!tryPersistAndConfirm(realmApplied))
                    return result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);

                current = realmApplied;
                cancellationToken.ThrowIfCancellationRequested();

                if (!tryInspect(current, cancellationToken, out inspection)
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

            if (!tryPersistAndConfirm(committed))
                return result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);

            return tryRemoveTerminalJournal(committed)
                ? result(SkinManagedFolderMutationRecoveryStatus.RecoveredForward)
                : result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);
        }

        private SkinManagedFolderMutationRecoveryResult recoverRollback(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderMutationRecoveryActionResult actionResult;

            try
            {
                actionResult = handler!.TryRollBack(journal, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            if (!actionResult.IsSuccess
                || actionResult.ObservedManagedRootIdentity != journal.ManagedRootIdentity)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            cancellationToken.ThrowIfCancellationRequested();
            return persistRolledBackAndRemove(
                journal,
                actionResult.TargetIdentity,
                actionResult.NewRecordPublicationFingerprint);
        }

        private SkinManagedFolderMutationRecoveryResult persistRolledBackAndRemove(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderPhysicalIdentity? recoveredTargetIdentity,
            string? recoveredNewRecordPublicationFingerprint)
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

            if (!tryPersistAndConfirm(rolledBack))
                return result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);

            return tryRemoveTerminalJournal(rolledBack)
                ? result(SkinManagedFolderMutationRecoveryStatus.RecoveredRollback)
                : result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);
        }

        private bool tryInspect(
            SkinManagedFolderMutationJournal journal,
            CancellationToken cancellationToken,
            out SkinManagedFolderMutationRecoveryInspection inspection)
        {
            try
            {
                inspection = handler!.Inspect(journal, cancellationToken);
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

            return inspection.Decision != SkinManagedFolderMutationRecoveryDecision.Ambiguous
                   && inspection.ObservedManagedRootIdentity == journal.ManagedRootIdentity;
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
                    && publicationFingerprint == null,

                _ => false,
            };
        }

        private bool tryPersistAndConfirm(
            SkinManagedFolderMutationJournal journal)
        {
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
                return true;
            }
            catch
            {
                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return false;
            }
        }

        private bool tryRemoveTerminalJournal(SkinManagedFolderMutationJournal journal)
        {
            try
            {
                journalStore.Delete(journal);
                terminalDeletionAwaitingConfirmation = journal;
                SkinManagedFolderMutationJournalLoadResult afterDelete = journalStore.Load();

                if (afterDelete.Status == SkinManagedFolderMutationJournalLoadStatus.Missing)
                {
                    coordinator.UnfreezeRecoveryPaths(journal.GetAffectedManagedRelativePaths());
                    unresolvedIntent = null;
                    terminalDeletionAwaitingConfirmation = null;
                    return !globalUnresolved && !coordinator.HasRecoveryFreeze;
                }

                if (afterDelete.IsLoaded
                    && afterDelete.Journal!.IsExactSameJournal(journal))
                {
                    return false;
                }

                if (afterDelete.Status == SkinManagedFolderMutationJournalLoadStatus.IoFailure)
                    return false;

                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return false;
            }
            catch
            {
                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return false;
            }
        }

        private static SkinManagedFolderMutationRecoveryResult result(SkinManagedFolderMutationRecoveryStatus status)
            => new SkinManagedFolderMutationRecoveryResult(status);
    }
}
