// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Graphics;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A container which holds many skinnable components, with functionality to add, remove and reload layouts.
    /// Used to allow user customisation of skin layouts.
    /// </summary>
    /// <remarks>
    /// This is currently used as a means of serialising skin layouts to files.
    /// Currently, one json file in a skin will represent one <see cref="SkinnableContainer"/>, containing
    /// the output of <see cref="ISerialisableDrawableContainer.CreateSerialisedInfo"/>.
    /// </remarks>
    public partial class SkinnableContainer : SkinReloadableDrawable, ISerialisableDrawableContainer
    {
        private protected override Scheduler SkinChangeScheduler => RevisionPublicationScheduler;

        private protected override void InvalidateSkinChange()
        {
            PendingAsyncDrawableOwnership<Container>? ownership;
            CancellationTokenSource? cancellation;

            lock (revisionWorkAdmissionGate)
            {
                if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                    return;

                contentGeneration++;
                ownership = Interlocked.Exchange(ref pendingContentLoad, null);
                cancellation = Interlocked.Exchange(ref cancellationSource, null);
            }

            try
            {
                cancelPendingContentLoad(ownership, cancellation);
            }
            catch (AggregateException)
            {
                // The exact stale ownership was already claimed and its generation is terminal. A cancellation
                // callback fault must not prevent the base event handler from scheduling the only fresh rebuild.
            }
        }

        /// <summary>
        /// Invoked when the skinnable components of this container finish loading.
        /// </summary>
        public event Action<Drawable>? OnComponentsLoaded;

        private readonly object revisionWorkAdmissionGate = new object();
        private Container? content;

        /// <summary>
        /// The lookup criteria which will be used to retrieve components from the active skin.
        /// </summary>
        public GlobalSkinnableContainerLookup Lookup { get; }

        public IBindableList<ISerialisableDrawable> Components => components;

        private readonly BindableList<ISerialisableDrawable> components = new BindableList<ISerialisableDrawable>();

        public override bool IsPresent => base.IsPresent || Scheduler.HasPendingTasks; // ensure that components are loaded even if the target container is hidden (ie. due to user toggle).

        public bool ComponentsLoaded { get; private set; }

        private CancellationTokenSource? cancellationSource;
        private PendingAsyncDrawableOwnership<Container>? pendingContentLoad;
        private int contentGeneration;
        private int revisionWorkShutdownRequested;

        internal Scheduler? ContentLoadCallbackScheduler { get; set; }

        internal Action? RevisionWorkAdmissionTestHook { get; set; }

        internal Task? PendingContentLoadTask
            => Volatile.Read(ref pendingContentLoad)?.LoadTask;

        public SkinnableContainer(GlobalSkinnableContainerLookup lookup)
        {
            Lookup = lookup;
        }

        public void Reload()
        {
            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                return;

            Reload((
                    CurrentSkin.GetDrawableComponent(new UserSkinComponentLookup(Lookup))
                    ?? CurrentSkin.GetDrawableComponent(Lookup))
                as Container);
        }

        public void Reload(Container? componentsContainer)
        {
            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
            {
                componentsContainer?.Dispose();
                return;
            }

            PendingAsyncDrawableOwnership<Container>? previousOwnership;
            CancellationTokenSource? previousCancellation;
            int requestedGeneration;

            lock (revisionWorkAdmissionGate)
            {
                if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                {
                    componentsContainer?.Dispose();
                    return;
                }

                requestedGeneration = ++contentGeneration;
                previousOwnership = Interlocked.Exchange(ref pendingContentLoad, null);
                previousCancellation = Interlocked.Exchange(ref cancellationSource, null);
            }

            cancelPendingContentLoad(previousOwnership, previousCancellation);

            clearPublishedContentSynchronously();
            components.Clear();
            ComponentsLoaded = false;

            Container provisional = componentsContainer ?? new Container
            {
                RelativeSizeAxes = Axes.Both
            };

            SkinCurrentRevisionLease? revisionLease = null;
            CancellationTokenSource? localCancellationSource = null;
            CancellationToken cancellationToken = default;
            PendingAsyncDrawableOwnership<Container>? ownership = null;
            int leaseReleased = 0;

            try
            {
                lock (revisionWorkAdmissionGate)
                {
                    if (Volatile.Read(ref revisionWorkShutdownRequested) != 0
                        || requestedGeneration != Volatile.Read(ref contentGeneration))
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

                    localCancellationSource = new CancellationTokenSource();
                    cancellationToken = localCancellationSource.Token;

                    void releaseRevisionLease()
                    {
                        if (Interlocked.Exchange(ref leaseReleased, 1) == 0)
                            revisionLease.Dispose();
                    }

                    ownership = new PendingAsyncDrawableOwnership<Container>(provisional, releaseRevisionLease);
                    content = provisional;
                    cancellationSource = localCancellationSource;
                    pendingContentLoad = ownership;
                }

                // Layout content may finish loading while its owning HUD/hitobject is outside the active lifetime
                // window. Publication remains an update-thread operation, but must not depend on this local scheduler
                // being pumped in order to release its revision work lease.
                Scheduler callbackScheduler = ContentLoadCallbackScheduler ?? RevisionPublicationScheduler;
                Task loadTask = LoadComponentAsync(
                    ownership.Loadable,
                    loaded => finishContentLoad(ownership, loaded, provisional, requestedGeneration, localCancellationSource),
                    cancellationToken,
                    callbackScheduler);
                ownership.Attach(loadTask, callbackScheduler);
            }
            catch
            {
                if (ownership != null)
                {
                    Interlocked.CompareExchange(ref pendingContentLoad, null, ownership);

                    if (ReferenceEquals(Interlocked.CompareExchange(ref cancellationSource, null, localCancellationSource), localCancellationSource))
                        localCancellationSource?.Dispose();

                    ownership.ReclaimUnstarted();
                }
                else
                {
                    localCancellationSource?.Dispose();
                    revisionLease?.Dispose();
                    provisional.Dispose();

                    if (ReferenceEquals(content, provisional))
                        content = null;
                }

                throw;
            }
        }

        private void finishContentLoad(
            PendingAsyncDrawableOwnership<Container> ownership,
            Drawable loaded,
            Container provisional,
            int requestedGeneration,
            CancellationTokenSource localCancellationSource)
        {
            if (!ReferenceEquals(Volatile.Read(ref pendingContentLoad), ownership)
                || !ownership.TryTransfer(loaded, out Container? transferred))
            {
                ownership.Cancel();
                return;
            }

            Interlocked.CompareExchange(ref pendingContentLoad, null, ownership);

            if (ReferenceEquals(Interlocked.CompareExchange(ref cancellationSource, null, localCancellationSource), localCancellationSource))
                localCancellationSource.Dispose();

            Container owned = transferred!;

            try
            {
                bool shouldReject;

                lock (revisionWorkAdmissionGate)
                {
                    shouldReject = Volatile.Read(ref revisionWorkShutdownRequested) != 0
                                   || requestedGeneration != Volatile.Read(ref contentGeneration)
                                   || localCancellationSource.IsCancellationRequested
                                   || IsDisposed
                                   || !ReferenceEquals(content, provisional);

                    if (!shouldReject)
                    {
                        content = owned;
                        AddInternal(owned);
                    }
                }

                if (shouldReject)
                {
                    owned.Dispose();
                    return;
                }

                components.AddRange(owned.Children.OfType<ISerialisableDrawable>());
                ComponentsLoaded = true;
                OnComponentsLoaded?.Invoke(this);
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

        private void clearPublishedContentSynchronously()
        {
            foreach (Drawable child in InternalChildren.ToArray())
            {
                // ClearInternal() queues child disposal asynchronously. Synchronous removal is the exact A-resource
                // cleanup receipt required before SkinReloadableDrawable may adopt B after this rebuild returns.
                if (!RemoveInternal(child, disposeImmediately: true))
                    throw new InvalidOperationException("The previous skinnable content could not be detached.");
            }
        }

        private protected override Action? RevisionWorkShutdown => cancelPendingContentLoadForShutdown;

        private void cancelPendingContentLoadForShutdown()
        {
            // A completed framework load may still be waiting on a callback scheduler which no longer updates during
            // game teardown. Cancelling the real pending owner makes its task continuation reclaim the provisional and
            // release the exact revision work lease without manager-side lease impersonation.
            PendingAsyncDrawableOwnership<Container>? ownership;
            CancellationTokenSource? cancellation;

            lock (revisionWorkAdmissionGate)
            {
                Volatile.Write(ref revisionWorkShutdownRequested, 1);
                contentGeneration++;
                ownership = Interlocked.Exchange(ref pendingContentLoad, null);
                cancellation = Interlocked.Exchange(ref cancellationSource, null);
                content = null;
            }

            CancelDeferredRevisionWorkAdmission();
            cancelPendingContentLoad(ownership, cancellation);
        }

        /// <inheritdoc cref="ISerialisableDrawableContainer"/>
        /// <exception cref="NotSupportedException">Thrown when attempting to add an element to a target which is not supported by the current skin.</exception>
        /// <exception cref="ArgumentException">Thrown if the provided instance is not a <see cref="Drawable"/>.</exception>
        public void Add(ISerialisableDrawable component)
        {
            if (content == null)
                throw new NotSupportedException("Attempting to add a new component to a target container which is not supported by the current skin.");

            if (!(component is Drawable drawable))
                throw new ArgumentException($"Provided argument must be of type {nameof(Drawable)}.", nameof(component));

            content.Add(drawable);
            components.Add(component);
        }

        /// <inheritdoc cref="ISerialisableDrawableContainer"/>
        /// <exception cref="NotSupportedException">Thrown when attempting to add an element to a target which is not supported by the current skin.</exception>
        /// <exception cref="ArgumentException">Thrown if the provided instance is not a <see cref="Drawable"/>.</exception>
        public void Remove(ISerialisableDrawable component, bool disposeImmediately)
        {
            if (content == null)
                throw new NotSupportedException("Attempting to remove a new component from a target container which is not supported by the current skin.");

            if (!(component is Drawable drawable))
                throw new ArgumentException($"Provided argument must be of type {nameof(Drawable)}.", nameof(component));

            content.Remove(drawable, disposeImmediately);
            components.Remove(component);
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

            base.SkinChanged(skin);

            Reload();
        }

        protected override void Dispose(bool isDisposing)
        {
            PendingAsyncDrawableOwnership<Container>? pendingOwnership;
            CancellationTokenSource? cancellation;

            lock (revisionWorkAdmissionGate)
            {
                Volatile.Write(ref revisionWorkShutdownRequested, 1);
                contentGeneration++;
                pendingOwnership = Interlocked.Exchange(ref pendingContentLoad, null);
                cancellation = Interlocked.Exchange(ref cancellationSource, null);
                content = null;
            }

            CancelDeferredRevisionWorkAdmission();
            cancelPendingContentLoad(pendingOwnership, cancellation);

            base.Dispose(isDisposing);
            pendingOwnership?.JoinAfterParentDisposal();

            OnComponentsLoaded = null;
        }

        private static void cancelPendingContentLoad(
            PendingAsyncDrawableOwnership<Container>? ownership,
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
    }
}
