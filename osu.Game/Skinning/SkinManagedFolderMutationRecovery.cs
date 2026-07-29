// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
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
        SkinManagedFolderPhysicalIdentity? TargetIdentity = null);

    internal readonly record struct SkinManagedFolderMutationRecoveryActionResult(
        bool IsSuccess,
        SkinManagedFolderPhysicalIdentity? ObservedManagedRootIdentity = null,
        SkinManagedFolderPhysicalIdentity? TargetIdentity = null);

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
                    recoverDeterminable(
                        journal,
                        SkinManagedFolderMutationPhase.Committed,
                        SkinManagedFolderMutationRecoveryStatus.RecoveredForward,
                        handler.TryRollForward,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.RollBack =>
                    recoverDeterminable(
                        journal,
                        SkinManagedFolderMutationPhase.RolledBack,
                        SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
                        handler.TryRollBack,
                        cancellationToken),

                SkinManagedFolderMutationRecoveryDecision.AlreadyCommitted =>
                    persistAndRemoveTerminal(
                        journal,
                        SkinManagedFolderMutationPhase.Committed,
                        SkinManagedFolderMutationRecoveryStatus.RecoveredForward,
                        inspection.TargetIdentity),

                SkinManagedFolderMutationRecoveryDecision.AlreadyRolledBack =>
                    persistAndRemoveTerminal(
                        journal,
                        SkinManagedFolderMutationPhase.RolledBack,
                        SkinManagedFolderMutationRecoveryStatus.RecoveredRollback,
                        null),

                _ => result(SkinManagedFolderMutationRecoveryStatus.Ambiguous),
            };
        }

        private SkinManagedFolderMutationRecoveryResult recoverDeterminable(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationPhase terminalPhase,
            SkinManagedFolderMutationRecoveryStatus successStatus,
            Func<SkinManagedFolderMutationJournal, CancellationToken, SkinManagedFolderMutationRecoveryActionResult> recoverAction,
            CancellationToken cancellationToken)
        {
            SkinManagedFolderMutationRecoveryActionResult actionResult;

            try
            {
                actionResult = recoverAction(journal, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            if (!actionResult.IsSuccess)
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

            if (actionResult.ObservedManagedRootIdentity != journal.ManagedRootIdentity)
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);

            cancellationToken.ThrowIfCancellationRequested();
            return persistAndRemoveTerminal(
                journal,
                terminalPhase,
                successStatus,
                actionResult.TargetIdentity);
        }

        private SkinManagedFolderMutationRecoveryResult persistAndRemoveTerminal(
            SkinManagedFolderMutationJournal journal,
            SkinManagedFolderMutationPhase terminalPhase,
            SkinManagedFolderMutationRecoveryStatus successStatus,
            SkinManagedFolderPhysicalIdentity? recoveredTargetIdentity)
        {
            SkinManagedFolderMutationJournal terminal;

            try
            {
                terminal = journal.WithRecoveryTerminalPhase(terminalPhase, recoveredTargetIdentity);
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                return result(SkinManagedFolderMutationRecoveryStatus.Ambiguous);
            }

            try
            {
                journalStore.Write(terminal);
            }
            catch
            {
                globalUnresolved = true;
                coordinator.FreezeAllRecoveryPaths();
                return result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);
            }

            return tryRemoveTerminalJournal(terminal)
                ? result(successStatus)
                : result(SkinManagedFolderMutationRecoveryStatus.JournalIoFailure);
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
