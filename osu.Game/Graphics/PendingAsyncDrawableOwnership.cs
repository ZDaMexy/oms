// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Framework.Threading;

namespace osu.Game.Graphics
{
    /// <summary>
    /// Owns an unparented drawable across <c>LoadComponentAsync</c> until the update-thread callback explicitly
    /// transfers it into a live hierarchy. Cancellation, load failure and a skipped callback all converge through the
    /// load task's completion continuation, so an in-flight loader is never raced by premature disposal.
    /// </summary>
    internal sealed partial class PendingAsyncDrawableOwnership<TDrawable>
        where TDrawable : Drawable
    {
        private const int state_pending = 0;
        private const int state_transferred = 1;
        private const int state_cancelled = 2;
        private const int state_reclaimed = 3;

        private readonly LoadWrapper loadable;
        private Action? ownershipResolved;
        private Task? loadTask;
        private int state;
        private int taskAttached;
        private int loadStopped;

        /// <summary>
        /// The resource-free wrapper which must be passed to <c>LoadComponentAsync</c>. Framework cancellation and
        /// parent disposal may both dispose this wrapper; the target drawable remains protected by its internal CAS.
        /// </summary>
        internal Drawable Loadable => loadable;

        internal Task? LoadTask => Volatile.Read(ref loadTask);

        internal PendingAsyncDrawableOwnership(TDrawable drawable, Action? ownershipResolved = null)
        {
            ArgumentNullException.ThrowIfNull(drawable);

            // The wrapper intentionally creates a nested (non-direct) async-load context. Framework rejects a
            // long-running target in that context, so fail before accepting ownership rather than stranding a claim.
            // This reflection check is the uncached equivalent used by Drawable.IsLongRunning itself.
            if (drawable.GetType().GetCustomAttribute<LongRunningLoadAttribute>() != null)
            {
                throw new ArgumentException(
                    "A pending async drawable ownership wrapper cannot own a long-running load target.",
                    nameof(drawable));
            }

            this.ownershipResolved = ownershipResolved;
            loadable = new LoadWrapper(
                drawable,
                loadableDisposedWithTarget);
        }

        /// <summary>
        /// Attaches the framework task which covers background loading through enqueueing its update-thread callback.
        /// </summary>
        internal void Attach(Task task, Scheduler callbackScheduler)
        {
            ArgumentNullException.ThrowIfNull(task);
            ArgumentNullException.ThrowIfNull(callbackScheduler);

            if (Interlocked.Exchange(ref taskAttached, 1) != 0)
                throw new InvalidOperationException("A pending drawable load task was already attached.");

            Volatile.Write(ref loadTask, task);
            _ = task.ContinueWith(
                completed =>
                {
                    // Framework's returned task stops after background loading and enqueueing the update-thread
                    // callback; it does not wait for that callback. A normal pending claim must therefore remain alive
                    // until a sentinel queued behind the framework callback observes whether ownership transferred.
                    if (completed.IsFaulted)
                    {
                        _ = completed.Exception;
                        Cancel();
                    }
                    else if (completed.IsCanceled)
                    {
                        Cancel();
                    }

                    Volatile.Write(ref loadStopped, 1);

                    if (Volatile.Read(ref state) == state_reclaimed)
                    {
                        resolveReclaimedOwnership();
                        return;
                    }

                    if (Volatile.Read(ref state) == state_cancelled)
                    {
                        reclaimCancelledAfterLoad();
                        return;
                    }

                    try
                    {
                        // The framework callback was enqueued before its returned task completed. FIFO scheduling puts
                        // this sentinel after it. If loading faulted inside that callback before invoking the caller,
                        // or the callback was otherwise skipped, the still-pending ownership is reclaimed here.
                        callbackScheduler.Add(() =>
                        {
                            Cancel();
                            reclaimCancelledAfterLoad();
                        });
                    }
                    catch
                    {
                        Cancel();
                        reclaimCancelledAfterLoad();
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        /// <summary>
        /// Atomically transfers ownership to the display callback. A cancellation which wins first prevents display.
        /// </summary>
        internal bool TryTransfer(Drawable loaded, out TDrawable? transferred)
        {
            ArgumentNullException.ThrowIfNull(loaded);

            if (!ReferenceEquals(loadable, loaded))
                throw new InvalidOperationException("The loaded drawable does not belong to this pending claim.");

            if (!loadable.TryTakeDrawable(
                    () => Interlocked.CompareExchange(ref state, state_transferred, state_pending) == state_pending,
                    out transferred))
                return false;

            // The target has been detached without disposal. Retire the now resource-free framework load wrapper;
            // its disposal callback deliberately does nothing once no target remains.
            loadable.Dispose();
            return true;
        }

        /// <summary>
        /// Completes any lifetime work which was retained alongside a successfully transferred drawable.
        /// </summary>
        internal void CompleteTransfer()
            => Interlocked.Exchange(ref ownershipResolved, null)?.Invoke();

        /// <summary>
        /// Prevents a later callback from taking ownership. Reclamation remains deferred until loading has stopped.
        /// </summary>
        internal void Cancel()
        {
            Interlocked.CompareExchange(ref state, state_cancelled, state_pending);

            if (Volatile.Read(ref loadStopped) != 0)
                reclaimCancelledAfterLoad();
        }

        /// <summary>
        /// Reclaims a drawable when starting its load threw before a task could be returned.
        /// </summary>
        internal void ReclaimUnstarted()
        {
            Volatile.Write(ref loadStopped, 1);
            Cancel();
        }

        /// <summary>
        /// Joins the exact load task after the framework parent has been disposed, then closes the narrow window in
        /// which the wrapper already completed loading (and was removed from the parent's loading set) but the task
        /// continuation has not yet reclaimed it. Callers must cancel both this claim and their load token before
        /// disposing the parent, and only call this method after the parent disposal has returned.
        /// </summary>
        internal void JoinAfterParentDisposal()
        {
            Cancel();

            Task? task = Volatile.Read(ref loadTask);

            if (task != null)
            {
                try
                {
                    task.GetAwaiter().GetResult();
                }
                catch
                {
                    // Disposal is a teardown boundary. The framework callback (and the caller's normal error path)
                    // remains responsible for surfacing load errors; this join only guarantees ownership closure.
                }
            }

            Volatile.Write(ref loadStopped, 1);
            reclaimCancelledAfterLoad();
            resolveReclaimedOwnership();
        }

        private void reclaimCancelledAfterLoad()
        {
            if (Interlocked.CompareExchange(ref state, state_reclaimed, state_cancelled) == state_cancelled)
            {
                try
                {
                    loadable.Dispose();
                }
                finally
                {
                    Interlocked.Exchange(ref ownershipResolved, null)?.Invoke();
                }
            }
        }

        private void loadableDisposedWithTarget()
        {
            while (true)
            {
                int observed = Volatile.Read(ref state);

                if (observed is state_transferred or state_reclaimed)
                    return;

                if (Interlocked.CompareExchange(ref state, state_reclaimed, observed) != observed)
                    continue;

                // Drawable.Dispose() first acquires the wrapper's framework LoadLock, so target resources are no
                // longer being touched here. Keep any outer work lease until the exact returned load task has also
                // completed, closing the OnLoadComplete-to-task-continuation window deterministically.
                if (Volatile.Read(ref loadStopped) != 0)
                    resolveReclaimedOwnership();

                return;
            }
        }

        private void resolveReclaimedOwnership()
        {
            if (Volatile.Read(ref state) == state_reclaimed)
                Interlocked.Exchange(ref ownershipResolved, null)?.Invoke();
        }

        /// <summary>
        /// Framework sees and may dispose only this wrapper. The target is atomically detached on successful transfer;
        /// otherwise the first wrapper disposal owns its teardown through the normal child hierarchy. Repeated wrapper
        /// disposal therefore never invokes target disposal twice.
        /// </summary>
        private sealed partial class LoadWrapper : ScreenStack
        {
            private readonly object ownershipLock = new object();
            private readonly Action disposedWithTarget;
            private TDrawable? drawable;

            internal LoadWrapper(TDrawable drawable, Action disposedWithTarget)
            {
                this.drawable = drawable;
                this.disposedWithTarget = disposedWithTarget;
                InternalChild = drawable;
            }

            internal bool TryTakeDrawable(Func<bool> tryClaim, out TDrawable? transferred)
            {
                lock (ownershipLock)
                {
                    transferred = drawable;

                    if (transferred == null || !tryClaim())
                    {
                        transferred = null;
                        return false;
                    }

                    drawable = null;
                    RemoveInternal(transferred, disposeImmediately: false);
                    return true;
                }
            }

            protected override void Dispose(bool isDisposing)
            {
                bool ownedTarget = false;

                try
                {
                    lock (ownershipLock)
                    {
                        // Base owns the still-attached target. Nulling this reference is the exact ownership claim which
                        // makes repeated framework/helper wrapper disposal target-free. Holding the same lock as transfer
                        // keeps target detachment atomic with respect to CompositeDrawable's child disposal.
                        ownedTarget = drawable != null;
                        drawable = null;
                        base.Dispose(isDisposing);
                    }
                }
                finally
                {
                    if (ownedTarget)
                        disposedWithTarget();
                }
            }
        }
    }
}
