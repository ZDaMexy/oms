// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Graphics;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Resolves a BMS ordinary note or long-note component away from the update thread and atomically publishes the fully loaded result.
    /// </summary>
    /// <remarks>
    /// Initial and live resolution never perform package IO on the update thread. A dynamically-added host keeps a
    /// protected component-specific migration fallback, while later skin changes keep the previous visual, until a new selected-package
    /// component or its final fallback has fully loaded; the prepared result is then replaced once on update.
    /// Materialisation remains per-host; cross-consumer publication is coordinated by the SkinManager current-revision barrier.
    /// </remarks>
    internal sealed partial class BmsAsyncNoteDrawable : SkinReloadableDrawable
    {
        private protected override bool AdoptRevisionAfterSynchronousSkinChanged => false;

        private protected override Scheduler SkinChangeScheduler => RevisionPublicationScheduler;

        private readonly object revisionWorkAdmissionGate = new object();
        private CancellationTokenSource? loadCancellation;
        private PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? pendingLoad;
        private Drawable? publishedChild;
        private int generation;
        private int revisionWorkShutdownRequested;

        internal Func<ISkinSource, BmsNoteSkinLookup, Drawable?> DrawableResolver { get; set; }
            = (source, requestedLookup) => source.GetDrawableComponent(requestedLookup);

        internal Scheduler? LoadCallbackScheduler { get; set; }

        internal Action? RevisionWorkAdmissionTestHook { get; set; }

        internal Task? PendingLoadTask
            => Volatile.Read(ref pendingLoad)?.LoadTask;

        public Drawable? Drawable { get; private set; }

        internal BmsNoteSkinLookup Lookup { get; }

        public BmsAsyncNoteDrawable(BmsNoteSkinLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            if (lookup.Element is not (BmsNoteSkinElements.Note
                or BmsNoteSkinElements.LongNoteHead
                or BmsNoteSkinElements.LongNoteBody
                or BmsNoteSkinElements.LongNoteTail))
            {
                throw new ArgumentException("The asynchronous BMS note host only accepts ordinary notes and supported long-note components.", nameof(lookup));
            }

            Lookup = lookup;
            RelativeSizeAxes = Axes.Both;

            // A C4 lookup already names one final immutable material entry. It must not briefly re-run the protected
            // legacy fallback while the framework builds that committed payload's drawable.
            Drawable = Lookup.UsesResolvedMaterial
                ? new BmsPublishedNotePendingDrawable()
                : createProtectedFallback(Lookup);
            InternalChild = publishedChild = Drawable;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                return;

            if (DeferRevisionWorkUntilReady(() => startSkinChange(skin)))
                return;

            startSkinChange(skin);
        }

        private void startSkinChange(ISkinSource skin)
        {
            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                return;

            int requestedGeneration;

            lock (revisionWorkAdmissionGate)
            {
                if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                    return;

                requestedGeneration = ++generation;
            }

            cancelPendingLoad();

            var provisional = new BmsPreparedNoteDrawable(skin, Lookup, DrawableResolver);
            SkinCurrentRevisionLease? revisionLease = null;
            SkinCurrentRevisionLeaseTransfer? revisionLeaseTransfer = null;
            CancellationTokenSource? localCancellation = null;
            CancellationToken loadToken = default;
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? ownership = null;
            int leaseReleased = 0;

            try
            {
                lock (revisionWorkAdmissionGate)
                {
                    if (Volatile.Read(ref revisionWorkShutdownRequested) != 0
                        || requestedGeneration != Volatile.Read(ref generation))
                    {
                        provisional.Dispose();
                        return;
                    }

                    revisionLease = AcquireCommittedRevisionWorkLease();

                    if (revisionLease == null)
                    {
                        provisional.Dispose();
                        return;
                    }

                    // This hook exists only to deterministically hold the formerly-racy acquire-to-install window.
                    RevisionWorkAdmissionTestHook?.Invoke();

                    // The outer drawable load continues to compare/materialise/publish after the shared package
                    // materializer Task completes. Keep this lease until that outer callback/cancellation really ends,
                    // and transfer a second lease from the same immutable revision to owner-internal work.
                    revisionLeaseTransfer = new SkinCurrentRevisionLeaseTransfer(revisionLease.Revision.AcquireWorkLease());
                    provisional.SetRevisionLeaseTransfer(revisionLeaseTransfer);

                    localCancellation = new CancellationTokenSource();
                    loadToken = localCancellation.Token;

                    void releaseRevisionLease()
                    {
                        if (Interlocked.Exchange(ref leaseReleased, 1) == 0)
                        {
                            revisionLeaseTransfer.Dispose();
                            revisionLease.Dispose();
                        }
                    }

                    ownership = new PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>(provisional, releaseRevisionLease);
                    loadCancellation = localCancellation;
                    pendingLoad = ownership;
                }

                // A future hitobject may finish loading while it is still outside the active lifetime window. Its
                // instance scheduler is not pumped in that state, but revision work must still publish or reject on
                // the update thread. Use the host scheduler as the visibility-independent publication boundary.
                Scheduler callbackScheduler = LoadCallbackScheduler ?? RevisionPublicationScheduler;
                Task loadTask = LoadComponentAsync(
                    ownership.Loadable,
                    loaded => finishLoad(ownership, loaded, requestedGeneration, localCancellation),
                    loadToken,
                    callbackScheduler);
                ownership.Attach(loadTask, callbackScheduler);
            }
            catch
            {
                if (ownership != null)
                {
                    Interlocked.CompareExchange(ref pendingLoad, null, ownership);

                    if (ReferenceEquals(Interlocked.CompareExchange(ref loadCancellation, null, localCancellation), localCancellation))
                        localCancellation?.Dispose();

                    ownership.ReclaimUnstarted();
                }
                else
                {
                    localCancellation?.Dispose();
                    revisionLeaseTransfer?.Dispose();
                    revisionLease?.Dispose();
                    provisional.Dispose();
                }

                throw;
            }
        }

        private void finishLoad(
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable> ownership,
            Drawable loaded,
            int requestedGeneration,
            CancellationTokenSource localCancellation)
        {
            if (!ReferenceEquals(Volatile.Read(ref pendingLoad), ownership)
                || !ownership.TryTransfer(loaded, out BmsPreparedNoteDrawable? transferred))
            {
                ownership.Cancel();
                return;
            }

            Interlocked.CompareExchange(ref pendingLoad, null, ownership);

            if (ReferenceEquals(Interlocked.CompareExchange(ref loadCancellation, null, localCancellation), localCancellation))
                localCancellation.Dispose();

            BmsPreparedNoteDrawable owned = transferred!;

            try
            {
                bool shouldReject;

                lock (revisionWorkAdmissionGate)
                {
                    shouldReject = Volatile.Read(ref revisionWorkShutdownRequested) != 0
                                   || requestedGeneration != Volatile.Read(ref generation)
                                   || IsDisposed;

                    if (!shouldReject)
                        publish(owned);
                }

                if (shouldReject)
                {
                    owned.Dispose();
                    return;
                }
            }
            catch
            {
                if (owned.Parent == null)
                    owned.Dispose();

                throw;
            }
            finally
            {
                ownership.CompleteTransfer();
            }
        }

        private PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? cancelPendingLoad()
        {
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? ownership =
                Interlocked.Exchange(ref pendingLoad, null);
            ownership?.Cancel();

            CancellationTokenSource? cancellation = Interlocked.Exchange(ref loadCancellation, null);

            if (cancellation != null)
            {
                try
                {
                    cancellation.Cancel();
                }
                finally
                {
                    cancellation.Dispose();
                }
            }

            return ownership;
        }

        private protected override Action? RevisionWorkShutdown => cancelPendingLoadForShutdown;

        private void cancelPendingLoadForShutdown()
        {
            // The publication has claimed shutdown admission but deliberately leaves both the participant and work
            // leases to this real owner. Cancelling the exact pending framework task lets its completion continuation
            // reclaim the provisional drawable and release outer/materializer transfers without a scheduler callback.
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? ownership;
            CancellationTokenSource? cancellation;

            lock (revisionWorkAdmissionGate)
            {
                Volatile.Write(ref revisionWorkShutdownRequested, 1);
                Interlocked.Increment(ref generation);
                ownership = Interlocked.Exchange(ref pendingLoad, null);
                cancellation = Interlocked.Exchange(ref loadCancellation, null);
            }

            CancelDeferredRevisionWorkAdmission();
            cancelPendingLoad(ownership, cancellation);
        }

        private protected override void InvalidateSkinChange()
        {
            // Invalidate synchronously in the base source event before its replacement callback is scheduled. A second
            // event handler could otherwise race the host update scheduler and cancel the only fresh generation.
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? ownership;
            CancellationTokenSource? cancellation;

            lock (revisionWorkAdmissionGate)
            {
                if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                    return;

                generation++;
                ownership = Interlocked.Exchange(ref pendingLoad, null);
                cancellation = Interlocked.Exchange(ref loadCancellation, null);
            }

            try
            {
                cancelPendingLoad(ownership, cancellation);
            }
            catch (AggregateException)
            {
                // Cancellation callbacks cannot be allowed to suppress the fresh scheduled generation. The exact old
                // ownership was already claimed and generation rejection still prevents its completion from publishing.
            }
        }

        private void publish(BmsPreparedNoteDrawable prepared)
        {
            Drawable? previous = publishedChild;
            bool preparedAttached = false;

            try
            {
                AddInternal(prepared);
                preparedAttached = true;

                // CompositeDrawable.InternalChild/Children replacement uses the framework async disposal queue. The
                // old revision participant may be the final lease, so synchronously destroy its exact wrapper before
                // adopting B and making A eligible for retirement.
                if (previous != null && !RemoveInternal(previous, disposeImmediately: true))
                    throw new InvalidOperationException("The previous BMS note revision child could not be detached.");
            }
            catch
            {
                if (preparedAttached && prepared.Parent != null)
                    RemoveInternal(prepared, disposeImmediately: true);

                throw;
            }

            publishedChild = prepared;
            Drawable = prepared.Visual;
            AdoptCommittedCurrentRevision();
        }

        protected override void Dispose(bool isDisposing)
        {
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? pendingOwnership;
            CancellationTokenSource? cancellation;

            lock (revisionWorkAdmissionGate)
            {
                Volatile.Write(ref revisionWorkShutdownRequested, 1);
                Interlocked.Increment(ref generation);
                pendingOwnership = Interlocked.Exchange(ref pendingLoad, null);
                cancellation = Interlocked.Exchange(ref loadCancellation, null);
            }

            CancelDeferredRevisionWorkAdmission();
            cancelPendingLoad(pendingOwnership, cancellation);
            base.Dispose(isDisposing);
            pendingOwnership?.JoinAfterParentDisposal();
        }

        private static void cancelPendingLoad(
            PendingAsyncDrawableOwnership<BmsPreparedNoteDrawable>? ownership,
            CancellationTokenSource? cancellation)
        {
            ownership?.Cancel();

            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            finally
            {
                cancellation.Dispose();
            }
        }

        private sealed partial class BmsPreparedNoteDrawable : CompositeDrawable
        {
            private readonly ISkinSource source;
            private readonly BmsNoteSkinLookup lookup;
            private readonly Func<ISkinSource, BmsNoteSkinLookup, Drawable?> drawableResolver;
            private SkinCurrentRevisionLeaseTransfer? revisionLeaseTransfer;

            public Drawable Visual { get; private set; } = null!;

            public BmsPreparedNoteDrawable(
                ISkinSource source,
                BmsNoteSkinLookup lookup,
                Func<ISkinSource, BmsNoteSkinLookup, Drawable?> drawableResolver)
            {
                this.source = source ?? throw new ArgumentNullException(nameof(source));
                this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
                this.drawableResolver = drawableResolver ?? throw new ArgumentNullException(nameof(drawableResolver));
                RelativeSizeAxes = Axes.Both;
            }

            internal void SetRevisionLeaseTransfer(SkinCurrentRevisionLeaseTransfer transfer)
            {
                ArgumentNullException.ThrowIfNull(transfer);

                if (Interlocked.CompareExchange(ref revisionLeaseTransfer, transfer, null) != null)
                    throw new InvalidOperationException("A BMS prepared note already owns a revision lease transfer.");
            }

            [BackgroundDependencyLoader]
            private void load(CancellationToken? cancellationToken)
            {
                cancellationToken?.ThrowIfCancellationRequested();

                Drawable? resolved = null;
                Drawable? candidate = null;
                bool adopted = false;

                try
                {
                    try
                    {
                        using (BmsManagedPackageNoteLoadContext.Enter(
                                   cancellationToken ?? CancellationToken.None,
                                   Volatile.Read(ref revisionLeaseTransfer)))
                            resolved = drawableResolver(source, lookup);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        if (lookup.UsesResolvedMaterial)
                            throw;

                        // A source exception must not bypass the component-specific protected fallback. The final migration
                        // fallback is intentionally generic here and does not preserve source-controlled exception text.
                        resolved = null;
                    }

                    candidate = resolved;
                    cancellationToken?.ThrowIfCancellationRequested();

                    if (candidate == null)
                    {
                        if (lookup.UsesResolvedMaterial)
                            throw new InvalidOperationException("A committed BMS material lookup did not produce an explicit visual result.");

                        candidate = createProtectedFallback(lookup);
                    }
                    Visual = candidate;
                    InternalChild = Visual;
                    adopted = true;
                }
                finally
                {
                    if (!adopted)
                        candidate?.Dispose();
                }
            }
        }

        private static Drawable createProtectedFallback(BmsNoteSkinLookup lookup)
            => lookup.Element switch
            {
                BmsNoteSkinElements.Note => new DefaultBmsNoteDisplay(
                    lookup.LaneIndex,
                    lookup.IsScratch,
                    lookup.Keymode,
                    allowAggregateTextureOverride: false),
                BmsNoteSkinElements.LongNoteHead => new DefaultBmsLongNoteHeadDisplay(
                    lookup.LaneIndex,
                    lookup.IsScratch,
                    lookup.Keymode,
                    allowAggregateTextureOverride: false),
                BmsNoteSkinElements.LongNoteBody => new DefaultBmsLongNoteBodyDisplay(
                    lookup.LaneIndex,
                    lookup.IsScratch,
                    lookup.Keymode,
                    allowAggregateResourceAndGeometryOverride: false),
                BmsNoteSkinElements.LongNoteTail => new DefaultBmsLongNoteTailDisplay(
                    lookup.LaneIndex,
                    lookup.IsScratch,
                    lookup.Keymode,
                    allowAggregateTextureOverride: false),
                _ => throw new ArgumentOutOfRangeException(nameof(lookup), lookup.Element, "Unsupported asynchronous BMS note element."),
            };
    }
}
