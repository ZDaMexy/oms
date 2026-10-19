// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Development;
using osu.Game.Database;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Skinning
{
    public partial class SkinManager
    {
        public SkinSelectionPreferences? SelectionPreferences { get; internal set; }

        /// <summary>The scope which produced the current gameplay projection, independent of settings navigation.</summary>
        public SkinSelectionScope? CurrentSkinScope { get; private set; }

        /// <summary>Suppresses persistence of transient projection changes while an exact stored scope is prepared.</summary>
        public bool IsApplyingSkinScope => Volatile.Read(ref pendingSkinScopeSelection) != null;

        private ScopedSelection? pendingSkinScopeSelection;
        private bool admittingSkinScopeSelection;

        /// <summary>Resolves deleted/unavailable key overrides through the mode default, then the protected fallback.</summary>
        public Live<SkinInfo> GetConfiguredSkin(SkinSelectionScope scope)
        {
            if (SelectionPreferences == null)
                return CurrentSkinInfo.Value;

            // OsuGame migrates legacy choices into both mode defaults before its first screen. Embedded hosts which
            // have not run that bootstrap still use their explicitly supplied current skin as the mode default.
            if (SelectionPreferences.GetConfiguredSkinId(scope) == null
                && SelectionPreferences.GetConfiguredSkinId(new SkinSelectionScope(scope.RulesetId)) == null)
                return CurrentSkinInfo.Value;

            Live<SkinInfo>? resolve(Guid id) => id == SkinInfo.OMS_SKIN ? DefaultOmsSkin.SkinInfo
                : Query(info => info.ID == id && !info.Protected && !info.DeletePending);

            Live<SkinInfo>? result = resolve(SelectionPreferences.GetEffectiveSkinId(scope));
            if (result != null)
                return result;

            if (scope.KeyCount != null)
            {
                result = resolve(SelectionPreferences.GetEffectiveSkinId(new SkinSelectionScope(scope.RulesetId)));
                if (result != null)
                    return result;
            }
            return DefaultOmsSkin.SkinInfo;
        }

        /// <summary>
        /// Captures a read-only settings owner on the tracked background read path. It never publishes the global
        /// selection, writes the source, or registers a live gameplay participant. The caller must dispose the result.
        /// </summary>
        public Task<SkinSettingsPreview> PrepareSettingsSkinAsync(Live<SkinInfo> target, CancellationToken cancellationToken = default)
        {
            lock (managedFolderRenameLifecycleGate)
            {
                if (managedFolderMutationShutdown || cancellationToken.IsCancellationRequested)
                    return Task.FromCanceled<SkinSettingsPreview>(new CancellationToken(canceled: true));

                var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                Task<SkinSettingsPreview> task = Task.Run(() => prepareSettingsSkin(target, cancellation.Token), cancellation.Token);
                var operation = new FolderWorkspaceReadOperation(task, cancellation);
                folderWorkspaceReadOperations.Add(operation);
                _ = task.ContinueWith(_ => completeFolderWorkspaceRead(operation), CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                return task;
            }
        }

        private SkinSettingsPreview prepareSettingsSkin(Live<SkinInfo> target, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SkinCurrentRevisionLease lease = currentRevisionPublication.AcquireCurrentLease();
            if (lease.Revision.Owner.SkinInfo.ID == target.ID)
                return new SkinSettingsPreview(lease.Revision.Owner, lease);
            lease.Dispose();

            using (ManagedFolderOperationCoordinator.EnterIndependentRead(cancellationToken))
            {
                // The worker may have waited for a delete reservation after the UI captured this Live reference.
                // Revalidate at the authoritative read boundary before any storage or package lookup.
                SelectionRequest request = target.PerformRead(info => info == null || info.DeletePending
                    ? throw new SkinSettingsTargetUnavailableException()
                    : createSelectionRequest(info));
                if (request.Resolution.Authority == SkinFilesystemStorageAuthority.ExternalFolder)
                {
                    using ExternalFolderSelectionPreparationResult captured = prepareExternalFolderSelection(request, cancellationToken);
                    if (!captured.IsSuccess)
                        throw new InvalidOperationException("The selected skin could not be prepared safely.");
                    return new SkinSettingsPreview(captured.TransferSkin());
                }

                Skin prepared = target.PerformRead(info => getSkin(info, cancellationToken));
                try
                {
                    prepared.PrepareGameplaySkinPackage(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    return new SkinSettingsPreview(prepared, ownsSkin: !ReferenceEquals(prepared, DefaultOmsSkin));
                }
                catch
                {
                    if (!ReferenceEquals(prepared, DefaultOmsSkin))
                        prepared.Dispose();
                    throw;
                }
            }
        }

        /// <summary>
        /// Publishes the stored scope using the existing preparation/publication transaction. Settings browsing does
        /// not call this; gameplay calls it only after conversion/mods establish the final key count, before attaching.
        /// </summary>
        public Task<bool> ApplySkinScopeAsync(SkinSelectionScope scope, CancellationToken cancellationToken = default)
            => applySkinScopeAsync(scope, null, false, cancellationToken);

        /// <summary>Used by explicit scoped editor application; persists only after the exact pair commits.</summary>
        public Task<bool> SelectSkinForScopeAsync(SkinSelectionScope scope, Live<SkinInfo> target, CancellationToken cancellationToken = default)
            => applySkinScopeAsync(scope, target, true, cancellationToken);

        private async Task<bool> applySkinScopeAsync(SkinSelectionScope scope, Live<SkinInfo>? explicitTarget, bool saveSelection, CancellationToken cancellationToken)
        {
            var operation = new ScopedSelection(scope, saveSelection);
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                if (!operation.TryCancel(cancellationToken))
                    return;
                scheduler.Add(() =>
                {
                    if (!ReferenceEquals(Volatile.Read(ref pendingSkinScopeSelection), operation))
                        return;
                    finishSkinScopeSelection(false);
                    Interlocked.Increment(ref selectionGeneration);
                    cancelPendingSelection();
                });
            });

            void begin()
            {
                if (operation.Completion.Task.IsCompleted || Volatile.Read(ref managedFolderSelectionShutdown) != 0)
                {
                    operation.Completion.TrySetResult(false);
                    return;
                }

                // An observer of an already committed pair cannot replace that request before its notifications
                // finish. Reject only the nested request; completing the pending scope here would misreport C2.
                if (isCurrentRevisionMutationAdmitted() || Volatile.Read(ref pendingSkinScopeSelection)?.IsCommitClaimed == true)
                {
                    LastSelectionRejectionReason = SkinSelectionRejectionReason.ManagedFolderOperationInProgress;
                    operation.Completion.TrySetResult(false);
                    return;
                }

                Live<SkinInfo> target = explicitTarget ?? GetConfiguredSkin(scope);
                if (CurrentSkinInfo.Value.ID == target.ID && CurrentSkin.Value.SkinInfo.ID == target.ID)
                {
                    if (!operation.TryClaimCommit())
                        return;
                    // Keeping the current owner is still a newer selection. Retire an older asynchronous candidate
                    // and its generation before reporting success, so its held callback cannot publish afterwards.
                    Interlocked.Increment(ref selectionGeneration);
                    finishSkinScopeSelection(false);
                    cancelPendingSelection();
                    CurrentSkinScope = scope;
                    if (saveSelection)
                        SelectionPreferences?.SetSkin(scope, target.ID);
                    operation.Completion.TrySetResult(true);
                    return;
                }

                finishSkinScopeSelection(false);
                operation.TargetId = target.ID;
                Volatile.Write(ref pendingSkinScopeSelection, operation);
                admittingSkinScopeSelection = true;
                try
                {
                    requestSelection(target);
                    if (CurrentSkinInfo.Value.ID == target.ID && CurrentSkin.Value.SkinInfo.ID == target.ID)
                        completeSkinScopeSelection(target.ID);
                }
                catch (Exception exception)
                {
                    Interlocked.CompareExchange(ref pendingSkinScopeSelection, null, operation);
                    operation.Completion.TrySetException(exception);
                }
                finally
                {
                    admittingSkinScopeSelection = false;
                }
            }

            if (ThreadSafety.IsUpdateThread)
                begin();
            else
            {
                CurrentRevisionCallbackScheduleResult scheduled = scheduleCurrentRevisionCallback(begin,
                    () => operation.Completion.TrySetResult(false));
                if (scheduled == CurrentRevisionCallbackScheduleResult.Faulted)
                    operation.Completion.TrySetResult(false);
            }

            return await operation.Completion.Task.ConfigureAwait(false);
        }

        private void completeSkinScopeSelection(Guid selectionId)
        {
            ScopedSelection? operation = Volatile.Read(ref pendingSkinScopeSelection);
            if (operation == null)
                return;
            if (selectionId == operation.TargetId)
            {
                CurrentSkinScope = operation.Scope;
                if (operation.SaveSelection)
                    SelectionPreferences?.SetSkin(operation.Scope, selectionId);
            }
            finishSkinScopeSelection(selectionId == operation.TargetId);
        }

        private void finishSkinScopeSelection(bool success)
            => Interlocked.Exchange(ref pendingSkinScopeSelection, null)?.Completion.TrySetResult(success);

        private bool tryClaimSkinScopeCommit()
            => Volatile.Read(ref pendingSkinScopeSelection) is not ScopedSelection operation || operation.TryClaimCommit();

        private sealed class ScopedSelection
        {
            private const int pending = 0;
            private const int cancelled = 1;
            private const int commit_claimed = 2;
            private int admission;

            public SkinSelectionScope Scope { get; }
            public bool SaveSelection { get; }
            public Guid TargetId { get; set; }
            public TaskCompletionSource<bool> Completion { get; } = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            public bool IsCommitClaimed => Volatile.Read(ref admission) == commit_claimed;

            public ScopedSelection(SkinSelectionScope scope, bool saveSelection)
            {
                Scope = scope;
                SaveSelection = saveSelection;
            }

            public bool TryClaimCommit() => Interlocked.CompareExchange(ref admission, commit_claimed, pending) == pending;

            public bool TryCancel(CancellationToken cancellationToken)
            {
                if (Interlocked.CompareExchange(ref admission, cancelled, pending) != pending)
                    return false;
                Completion.TrySetCanceled(cancellationToken);
                return true;
            }
        }

        internal void SetCurrentSkinMode(string? rulesetId)
            => CurrentSkinScope = rulesetId is "bms" or "mania" ? new SkinSelectionScope(rulesetId) : null;
    }

    internal sealed class SkinSettingsTargetUnavailableException : InvalidOperationException
    {
        public SkinSettingsTargetUnavailableException()
            : base("The settings skin is no longer available.")
        {
        }
    }

    /// <summary>An owned immutable settings read. Disposing never removes a skin package or changes player choices.</summary>
    public sealed class SkinSettingsPreview : IDisposable
    {
        public Skin Skin { get; }
        private IDisposable? revisionLease;
        private Skin? ownedSkin;

        internal SkinSettingsPreview(Skin skin, IDisposable? revisionLease = null, bool ownsSkin = true)
        {
            Skin = skin;
            this.revisionLease = revisionLease;
            if (revisionLease == null && ownsSkin)
                ownedSkin = skin;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref revisionLease, null)?.Dispose();
            Interlocked.Exchange(ref ownedSkin, null)?.Dispose();
        }
    }
}
