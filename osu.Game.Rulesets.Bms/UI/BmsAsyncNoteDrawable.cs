// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
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
    /// This is per-host publication; package/playfield-wide atomic reload remains an SV1-2 responsibility.
    /// </remarks>
    internal sealed partial class BmsAsyncNoteDrawable : SkinReloadableDrawable
    {
        private readonly BmsNoteSkinLookup lookup;
        private CancellationTokenSource? loadCancellation;
        private int generation;
        private bool sourceInvalidationSubscribed;

        public Drawable? Drawable { get; private set; }

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

            this.lookup = lookup;
            RelativeSizeAxes = Axes.Both;

            // A dynamically-added host (notably the pre-start speed preview) may begin loading on the update thread.
            // Keep the component-specific protected fallback while its exact source is prepared asynchronously.
            Drawable = createProtectedFallback(lookup);
            InternalChild = Drawable;
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            int requestedGeneration = Interlocked.Increment(ref generation);
            var provisional = new BmsPreparedNoteDrawable(skin, lookup);

            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = new CancellationTokenSource();
            CancellationToken loadToken = loadCancellation.Token;
            int ownershipClaimed = 0;

            Task loadTask = LoadComponentAsync(
                provisional,
                loaded =>
                {
                    // The cancellation path may already have reclaimed this unparented provisional after background
                    // loading completed but before the framework's scheduled callback reached the update thread.
                    if (Interlocked.CompareExchange(ref ownershipClaimed, 1, 0) != 0)
                        return;

                    if (requestedGeneration != Volatile.Read(ref generation) || IsDisposed)
                    {
                        loaded.Dispose();
                        return;
                    }

                    publish(loaded);
                },
                loadToken);

            _ = loadToken.Register(() =>
            {
                _ = loadTask.ContinueWith(
                    task =>
                    {
                        _ = task.Exception;

                        // LoadComponentAsync intentionally skips its callback after cancellation. Once its background
                        // task has stopped touching the provisional, reclaim it if the callback never took ownership.
                        if (Interlocked.CompareExchange(ref ownershipClaimed, 2, 0) == 0)
                            provisional.Dispose();
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            });
        }

        protected override void LoadAsyncComplete()
        {
            if (!sourceInvalidationSubscribed)
            {
                CurrentSkin.SourceChanged += invalidatePendingResult;
                sourceInvalidationSubscribed = true;
            }

            base.LoadAsyncComplete();
        }

        private void invalidatePendingResult()
        {
            // SkinReloadableDrawable schedules SkinChanged. Invalidate synchronously at event arrival so an already
            // queued completion from the previous source cannot publish before that scheduled callback runs.
            Interlocked.Increment(ref generation);
            loadCancellation?.Cancel();
        }

        private void publish(BmsPreparedNoteDrawable prepared)
        {
            Drawable = prepared.Visual;
            InternalChild = prepared;
        }

        protected override void Dispose(bool isDisposing)
        {
            Interlocked.Increment(ref generation);

            if (sourceInvalidationSubscribed)
            {
                CurrentSkin.SourceChanged -= invalidatePendingResult;
                sourceInvalidationSubscribed = false;
            }

            loadCancellation?.Cancel();
            loadCancellation?.Dispose();
            loadCancellation = null;
            base.Dispose(isDisposing);
        }

        private sealed partial class BmsPreparedNoteDrawable : CompositeDrawable
        {
            private readonly ISkinSource source;
            private readonly BmsNoteSkinLookup lookup;

            public Drawable Visual { get; private set; } = null!;

            public BmsPreparedNoteDrawable(ISkinSource source, BmsNoteSkinLookup lookup)
            {
                this.source = source ?? throw new ArgumentNullException(nameof(source));
                this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
                RelativeSizeAxes = Axes.Both;
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
                        using (BmsManagedPackageNoteLoadContext.Enter(cancellationToken ?? CancellationToken.None))
                            resolved = source.GetDrawableComponent(lookup);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // A source exception must not bypass the component-specific protected fallback. The final migration
                        // fallback is intentionally generic here and does not preserve source-controlled exception text.
                        resolved = null;
                    }

                    candidate = resolved;
                    cancellationToken?.ThrowIfCancellationRequested();

                    candidate ??= createProtectedFallback(lookup);
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
