// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Platform;
using osu.Framework.Threading;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A poolable drawable implementation which has a pre-wired callback (see <see cref="SkinChanged"/>) that fires
    /// once on load and again on any subsequent skin change.
    /// </summary>
    public abstract partial class SkinReloadableDrawable : PoolableDrawable
    {
        private readonly object skinChangeScheduleGate = new object();
        private ScheduledDelegate? pendingSkinChange;
        private long skinChangeScheduleGeneration;
        private int revisionParticipantShutdownRequested;
        private SkinRevisionParticipantRegistration? revisionParticipant;
        private SkinRevisionParticipantRegistration? initialLoadRevisionParticipant;
        private SkinManager? skinManager;
        private readonly object revisionWorkReadyGate = new object();
        private ScheduledDelegate? pendingRevisionWorkReadyAdmission;
        private long revisionWorkReadyGeneration;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        /// <summary>
        /// An update-thread scheduler whose progress does not depend on this consumer being alive or visible.
        /// Background revision work must use this when completion is required outside the drawable lifetime window.
        /// </summary>
        private protected Scheduler RevisionPublicationScheduler => gameHost.UpdateThread.Scheduler;

        /// <summary>
        /// Scheduler used for source invalidation rebuilds. Most drawables deliberately defer rebuilds while not alive;
        /// asynchronous resource owners may override this with <see cref="RevisionPublicationScheduler"/> when their
        /// exact work lease must converge outside the local lifetime window.
        /// </summary>
        private protected virtual Scheduler SkinChangeScheduler => Scheduler;

        /// <summary>
        /// Invalidates resource work derived from the previous source before its replacement rebuild is scheduled.
        /// </summary>
        /// <remarks>
        /// <see cref="ISkinSource.SourceChanged"/> has no thread-affinity guarantee. Asynchronous owners must cancel
        /// stale work synchronously here rather than using a second event subscription, which could race the scheduled
        /// rebuild and cancel the only fresh generation after it has already started.
        /// </remarks>
        private protected virtual void InvalidateSkinChange()
        {
        }

        /// <summary>
        /// Defers initial asynchronous revision work until the outer drawable has left the framework loading state.
        /// The eventual callback still uses the host update scheduler, so it remains reachable when this drawable or an
        /// ancestor is outside its active lifetime window.
        /// </summary>
        /// <returns><see langword="true"/> when <paramref name="admitWork"/> was deferred.</returns>
        private protected bool DeferRevisionWorkUntilReady(Action admitWork)
        {
            ArgumentNullException.ThrowIfNull(admitWork);

            if (Volatile.Read(ref revisionParticipantShutdownRequested) != 0 || IsDisposed)
                return true;

            if (LoadState >= LoadState.Ready)
                return false;

            ScheduledDelegate? previous;
            long generation;

            lock (revisionWorkReadyGate)
            {
                if (Volatile.Read(ref revisionParticipantShutdownRequested) != 0 || IsDisposed)
                    return true;

                if (LoadState >= LoadState.Ready)
                    return false;

                generation = ++revisionWorkReadyGeneration;
                previous = pendingRevisionWorkReadyAdmission;
                pendingRevisionWorkReadyAdmission = scheduleReadyAdmission(generation, admitWork);
            }

            previous?.Cancel();
            return true;
        }

        private ScheduledDelegate scheduleReadyAdmission(long generation, Action admitWork)
            => RevisionPublicationScheduler.AddDelayed(() => runReadyAdmission(generation, admitWork), 1);

        private void runReadyAdmission(long generation, Action admitWork)
        {
            lock (revisionWorkReadyGate)
            {
                if (generation != revisionWorkReadyGeneration
                    || Volatile.Read(ref revisionParticipantShutdownRequested) != 0
                    || IsDisposed)
                    return;

                if (LoadState < LoadState.Ready)
                {
                    pendingRevisionWorkReadyAdmission = scheduleReadyAdmission(generation, admitWork);
                    return;
                }

                pendingRevisionWorkReadyAdmission = null;
                revisionWorkReadyGeneration++;
            }

            admitWork();
        }

        /// <summary>
        /// Cancels a not-yet-admitted initial revision work item during owner shutdown.
        /// </summary>
        private protected void CancelDeferredRevisionWorkAdmission()
        {
            ScheduledDelegate? pending;

            lock (revisionWorkReadyGate)
            {
                revisionWorkReadyGeneration++;
                pending = pendingRevisionWorkReadyAdmission;
                pendingRevisionWorkReadyAdmission = null;
            }

            pending?.Cancel();
        }

        internal Action? LoadAsyncCompleteAfterSkinChangedTestHook { get; set; }

        internal Action? SkinChangeScheduledTestHook { get; set; }

        /// <summary>
        /// A reloadable is a visual/resource consumer unless it proves otherwise. Unsupported consumers reject the
        /// barrier by returning no staged receipt; they are never silently left rendering an indefinitely-live A.
        /// </summary>
        private protected virtual SkinRevisionParticipantKind RevisionParticipantKind
            => SkinRevisionParticipantKind.CoherentVisualConsumer;

        /// <summary>
        /// Set to false only for a structural wrapper which owns no skin-derived resource or source reference itself.
        /// Its resource-owning descendants must still register independently.
        /// </summary>
        private protected virtual bool ParticipatesInCurrentRevision => true;

        /// <summary>
        /// Asynchronous consumers which deliberately keep displaying A override this and adopt only when their
        /// fully-loaded replacement is published.
        /// </summary>
        private protected virtual bool AdoptRevisionAfterSynchronousSkinChanged => true;

        /// <summary>
        /// Cancels or reaps hidden owner-touching work when publication shutdown has claimed this participant. A
        /// consumer may cancel unpublished work without visual detach, or reap its owned resource graph when that graph
        /// must be destroyed first. Every lease remains owned by that real completion/disposal path.
        /// </summary>
        private protected virtual Action? RevisionWorkShutdown => null;

        private protected virtual Task<SkinRevisionParticipantCommit?> PrepareCurrentRevisionAsync(
            SkinCurrentRevision nextRevision,
            CancellationToken cancellationToken)
            => Task.FromResult<SkinRevisionParticipantCommit?>(null);

        /// <summary>
        /// Invoked when <see cref="CurrentSkin"/> has changed.
        /// </summary>
        public event Action? OnSkinChanged;

        /// <summary>
        /// The current skin source.
        /// </summary>
        protected ISkinSource CurrentSkin { get; private set; } = null!;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(ISkinSource source, SkinManager? skinManager = null)
        {
            CurrentSkin = source;
            this.skinManager = skinManager;

            // Derived background loaders may immediately resolve textures, samples or nested skin sources. Register a
            // fail-closed coherent participant before any such lookup is reachable. Unlike the former bare holder
            // lease, this both retains exact A and blocks every publication until the fully-built consumer can provide
            // its real staged receipt.
            if (ParticipatesInCurrentRevision)
            {
                initialLoadRevisionParticipant = skinManager?.RegisterRevisionParticipant(
                    SkinRevisionParticipantKind.CoherentVisualConsumer,
                    $"{GetType().Name} (initial load)",
                    blocksRevisionPublication: true,
                    shutdownWork: requestRevisionParticipantShutdown);
            }

            CurrentSkin.SourceChanged += onChange;
        }

        /// <summary>
        /// Retains the exact revision currently owned by this drawable while an asynchronous consumer still uses it.
        /// </summary>
        private protected SkinCurrentRevisionLease? AcquireRevisionWorkLease()
        {
            if (revisionParticipant == null)
                return null;

            ensureRevisionWorkShutdownOwner();
            return revisionParticipant.AcquireWorkLease();
        }

        /// <summary>
        /// Retains the manager's already committed revision for new asynchronous work. This differs from
        /// <see cref="AcquireRevisionWorkLease"/> during an ordinary source change, where this drawable may still be
        /// finishing its acquire-before-release adoption from the previous revision.
        /// </summary>
        private protected SkinCurrentRevisionLease? AcquireCommittedRevisionWorkLease()
        {
            if (skinManager == null)
                return null;

            ensureRevisionWorkShutdownOwner();

            try
            {
                return skinManager.AcquireCurrentRevisionWorkLease();
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        private void ensureRevisionWorkShutdownOwner()
        {
            if (RevisionWorkShutdown == null)
                throw new InvalidOperationException("A revision work owner must provide deterministic shutdown reaping.");
        }

        private protected void AdoptCommittedCurrentRevision()
            => revisionParticipant?.AdoptCurrentRevision();

        protected override void LoadAsyncComplete()
        {
            try
            {
                base.LoadAsyncComplete();

                // Acquire the fully-built participant and its exact lease before releasing the temporary load barrier.
                // Keeping both registrations through the synchronous rebuild leaves no lookup or ownership window.
                if (ParticipatesInCurrentRevision && revisionParticipant == null)
                {
                    revisionParticipant = skinManager?.RegisterRevisionParticipant(
                        RevisionParticipantKind,
                        GetType().Name,
                        prepareCommit: RevisionParticipantKind == SkinRevisionParticipantKind.CoherentVisualConsumer
                            ? PrepareCurrentRevisionAsync
                            : null,
                        shutdownWork: requestRevisionParticipantShutdown);
                }

                skinChanged();
                LoadAsyncCompleteAfterSkinChangedTestHook?.Invoke();
            }
            finally
            {
                Interlocked.Exchange(ref initialLoadRevisionParticipant, null)?.Dispose();
            }
        }

        /// <summary>
        /// Force any pending <see cref="SkinChanged"/> calls to be performed immediately.
        /// </summary>
        /// <remarks>
        /// When a skin change occurs, the handling provided by this class is scheduled.
        /// In some cases, such a sample playback, this can result in the sample being played
        /// just before it is updated to a potentially different sample.
        ///
        /// Calling this method will ensure any pending update operations are run immediately.
        /// It is recommended to call this before consuming the result of skin changes for anything non-drawable.
        /// </remarks>
        protected void FlushPendingSkinChanges()
        {
            ScheduledDelegate? pending;

            lock (skinChangeScheduleGate)
                pending = pendingSkinChange;

            pending?.RunTask();
        }

        /// <summary>
        /// Called when a change is made to the skin.
        /// </summary>
        /// <param name="skin">The new skin.</param>
        protected virtual void SkinChanged(ISkinSource skin)
        {
        }

        private void onChange()
        {
            // A prepared revision publication either committed this consumer's staged state at the barrier or classified
            // it as a pure holder. Pure holders must keep using exact A until a later ordinary source change or detach;
            // querying manager/B here would create the split which the participant protocol exists to prevent.
            if (skinManager?.IsCurrentRevisionPublicationBroadcast == true
                || Volatile.Read(ref revisionParticipantShutdownRequested) != 0
                || IsDisposed)
                return;

            // This must happen in the source event before dispatch. A separately-subscribed invalidation handler can
            // lose to a visibility-independent update scheduler and cancel the only fresh rebuild after it starts.
            InvalidateSkinChange();

            // Scheduling avoids direct mutation from a source event and permits cancellation during disposal. The
            // default local scheduler defers changes while not alive; exact asynchronous work owners override it.
            ScheduledDelegate? previous;

            lock (skinChangeScheduleGate)
            {
                if (Volatile.Read(ref revisionParticipantShutdownRequested) != 0 || IsDisposed)
                    return;

                long generation = skinChangeScheduleGeneration + 1;
                ScheduledDelegate? scheduled = SkinChangeScheduler.Add(() => runScheduledSkinChange(generation));

                skinChangeScheduleGeneration = generation;
                previous = pendingSkinChange;
                pendingSkinChange = scheduled;
            }

            previous?.Cancel();
            SkinChangeScheduledTestHook?.Invoke();
        }

        private void runScheduledSkinChange(long generation)
        {
            lock (skinChangeScheduleGate)
            {
                if (generation != skinChangeScheduleGeneration
                    || Volatile.Read(ref revisionParticipantShutdownRequested) != 0
                    || IsDisposed)
                    return;

                pendingSkinChange = null;
            }

            skinChanged();
        }

        private void skinChanged()
        {
            if (Volatile.Read(ref revisionParticipantShutdownRequested) != 0 || IsDisposed)
                return;

            bool rebuilt = false;

            try
            {
                SkinChanged(CurrentSkin);

                if (Volatile.Read(ref revisionParticipantShutdownRequested) != 0 || IsDisposed)
                    return;

                OnSkinChanged?.Invoke();
                rebuilt = true;
            }
            finally
            {
                if (rebuilt
                    && Volatile.Read(ref revisionParticipantShutdownRequested) == 0
                    && !IsDisposed
                    && AdoptRevisionAfterSynchronousSkinChanged)
                {
                    revisionParticipant?.AdoptCurrentRevision();
                }
            }
        }

        private void requestRevisionParticipantShutdown()
        {
            if (!stopSkinChangeScheduling())
                return;

            RevisionWorkShutdown?.Invoke();
        }

        private bool stopSkinChangeScheduling()
        {
            if (Interlocked.Exchange(ref revisionParticipantShutdownRequested, 1) != 0)
                return false;

            ScheduledDelegate? pending;

            lock (skinChangeScheduleGate)
            {
                skinChangeScheduleGeneration++;
                pending = pendingSkinChange;
                pendingSkinChange = null;
            }

            pending?.Cancel();
            CancelDeferredRevisionWorkAdmission();
            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            stopSkinChangeScheduling();
            base.Dispose(isDisposing);

            if (CurrentSkin.IsNotNull())
                CurrentSkin.SourceChanged -= onChange;

            revisionParticipant?.Dispose();
            revisionParticipant = null;
            Interlocked.Exchange(ref initialLoadRevisionParticipant, null)?.Dispose();

            OnSkinChanged = null;
        }
    }
}
