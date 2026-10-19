// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Threading;
using osu.Game.Audio;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A sample corresponding to an <see cref="ISampleInfo"/> that supports being pooled and responding to skin changes.
    /// </summary>
    public partial class PoolableSkinnableSample : SkinReloadableDrawable, IAdjustableAudioComponent
    {
        private protected override SkinRevisionParticipantKind RevisionParticipantKind
            => SkinRevisionParticipantKind.CoherentVisualConsumer;

        private protected override Task<SkinRevisionParticipantCommit> PrepareCurrentRevisionAsync(
            SkinCurrentRevision nextRevision,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            DrawableSample previousSample;
            RevisionLeaseReleases releases = default;

            try
            {
                lock (revisionMaintenanceGate)
                {
                    if (revisionWorkShutdownRequested != 0)
                        return Task.FromResult<SkinRevisionParticipantCommit>(null);

                    // A rollback may begin in the same update callback which committed B, before the host's next
                    // maintenance tick. Finish that already-published swap before preparing another exact revision.
                    if (ThreadSafety.IsUpdateThread)
                    {
                        applyPendingSampleSwap(ref releases);
                        releaseCompletedRevisionChannels(ref releases);
                    }

                    if (pendingSampleSwap != null)
                        return Task.FromResult<SkinRevisionParticipantCommit>(null);

                    previousSample = Sample;
                }
            }
            finally
            {
                releases.Dispose();
            }

            DrawableSample preparedSample = null;

            if (sampleInfo != null)
            {
                ISample sample = nextRevision.Owner.GetSample(sampleInfo);
                cancellationToken.ThrowIfCancellationRequested();

                if (sample != null)
                    preparedSample = new RevisionDrawableSample(sample);
            }

            SkinCurrentRevisionLease previousRevisionLease = null;
            PendingSampleSwap swap = null;

            try
            {
                // Acquiring a publication lease must stay outside the graph gate. A commit owns the publication
                // lock before entering that gate, while disposal leaves the gate before detaching its participant.
                previousRevisionLease = AcquireRevisionWorkLease();
                swap = new PendingSampleSwap(previousSample, preparedSample, previousRevisionLease);
                previousRevisionLease = null;

                bool ownerChanged;

                lock (revisionMaintenanceGate)
                {
                    ownerChanged = revisionWorkShutdownRequested != 0
                                   || pendingSampleSwap != null
                                   || !ReferenceEquals(Sample, previousSample);

                    if (!ownerChanged)
                    {
                        swap.PreparationPending = true;
                        preparedSampleSwaps++;

                        // Scheduling is fallible preparation work, never part of the reversible publication barrier.
                        // One owner task serves all candidates and historical channels; an aborted newer candidate must
                        // not cancel maintenance which an older, still-playing revision continues to need.
                        if (revisionMaintenance == null)
                        {
                            revisionMaintenance = RevisionPublicationScheduler.AddDelayed(maintainRevisionSamples, 1, repeat: true);
                            revisionMaintenance.PerformRepeatCatchUpExecutions = false;
                        }
                    }
                }

                if (ownerChanged)
                {
                    abortPreparedSample(swap);
                    return Task.FromResult<SkinRevisionParticipantCommit>(null);
                }

                return Task.FromResult(
                    new SkinRevisionParticipantCommit(
                        () => commitPreparedSample(swap),
                        () => rollbackPreparedSample(swap),
                        () => abortPreparedSample(swap)));
            }
            catch
            {
                if (swap != null)
                    abortPreparedSample(swap);
                else
                {
                    preparedSample?.Dispose();
                    previousRevisionLease?.Dispose();
                }

                throw;
            }
        }

        /// <summary>
        /// The currently-loaded <see cref="DrawableSample"/>.
        /// </summary>
        [CanBeNull]
        public DrawableSample Sample { get; private set; }

        private readonly AudioContainer<DrawableSample> sampleContainer;
        private ISampleInfo sampleInfo;
        private SampleChannel activeChannel;
        private readonly List<ActiveRevisionChannel> revisionChannels = new List<ActiveRevisionChannel>();
        private PendingSampleSwap pendingSampleSwap;
        private readonly object revisionMaintenanceGate = new object();
        private ScheduledDelegate revisionMaintenance;
        private int preparedSampleSwaps;
        private int revisionWorkShutdownRequested;
        private int revisionWorkGraphReaped;

        private protected override Action RevisionWorkShutdown => requestRevisionOwnerShutdown;

        private void requestRevisionOwnerShutdown()
        {
            ScheduledDelegate maintenance;

            lock (revisionMaintenanceGate)
            {
                if (Interlocked.Exchange(ref revisionWorkShutdownRequested, 1) != 0)
                    return;

                maintenance = takeRevisionMaintenance();
            }

            // Cancel may wait for an executing callback which needs the graph gate. Never join it under that gate.
            maintenance?.Cancel();

            if (ThreadSafety.IsUpdateThread)
            {
                reapRevisionWorkGraphForShutdown();
                return;
            }

            try
            {
                // Drawable/audio hierarchy mutation remains update-thread-owned. The manager waits WorkDetached while
                // the normal host keeps updating; production game disposal enters this callback on the update thread
                // and therefore never schedules into the very thread it is synchronously joining.
                RevisionPublicationScheduler.AddOnce(reapRevisionWorkGraphForShutdown);
            }
            catch
            {
                // Never fake-release an owner lease if its scheduler is already unavailable. A real parent disposal
                // can still perform the same idempotent teardown; the exact WorkDetached fence stays fail-closed.
            }
        }

        private void reapRevisionWorkGraphForShutdown()
        {
            RevisionLeaseReleases releases = default;

            try
            {
                lock (revisionMaintenanceGate)
                    reapRevisionWorkGraphForShutdownHeld(ref releases);
            }
            finally
            {
                releases.Dispose();
            }
        }

        private void reapRevisionWorkGraphForShutdownHeld(ref RevisionLeaseReleases releases)
        {
            if (Interlocked.Exchange(ref revisionWorkGraphReaped, 1) != 0)
                return;

            PendingSampleSwap pendingSwap = Interlocked.Exchange(ref pendingSampleSwap, null);
            ActiveRevisionChannel[] channels = revisionChannels.ToArray();

            foreach (ActiveRevisionChannel channel in channels)
            {
                try
                {
                    channel.Channel.Stop();
                }
                catch
                {
                    // The owned drawable/sample graph below is still the authoritative cleanup boundary.
                }
            }

            try
            {
                // AudioContainer.Clear() defers child disposal through the framework's async disposal queue, which
                // is too late for a revision-retirement fence. Snapshot the exact graph and synchronously remove each
                // child before either A/channel work lease can detach. Keep the long-lived AudioContainer itself valid
                // for the retained root's next update/audio bindables.
                DrawableSample[] parentedSamples = sampleContainer.Children.ToArray();
                bool preparedSampleIsParented = pendingSwap?.PreparedSample != null
                                                && parentedSamples.Any(sample => ReferenceEquals(sample, pendingSwap.PreparedSample));

                if (!preparedSampleIsParented)
                    pendingSwap?.DisposePreparedSample();

                foreach (DrawableSample sample in parentedSamples)
                {
                    if (!sampleContainer.Remove(sample, true) || !IsOwnedSampleDisposed(sample))
                        throw new InvalidOperationException("The owned sample graph could not be synchronously reaped.");

                    if (ReferenceEquals(sample, pendingSwap?.PreparedSample))
                        pendingSwap.MarkPreparedSampleDisposed();
                }

                if (sampleContainer.Any())
                    throw new InvalidOperationException("The owned sample graph changed while shutdown was reaping it.");
            }
            catch
            {
                // Preserve exact reachability for the later real parent Dispose. Never release A/channel leases after
                // a failed graph reap, and allow an idempotent owner retry instead of orphaning the claimed swap.
                if (pendingSwap != null)
                    Interlocked.CompareExchange(ref pendingSampleSwap, pendingSwap, null);

                Volatile.Write(ref revisionWorkGraphReaped, 0);
                throw;
            }

            Sample = null;
            activeChannel = null;
            wasPlaying = false;
            revisionChannels.Clear();

            releases.Add(pendingSwap?.TakePreviousRevisionLease());

            foreach (ActiveRevisionChannel channel in channels)
                releases.Add(channel.Lease);
        }

        /// <summary>
        /// Creates a new <see cref="PoolableSkinnableSample"/> with no applied <see cref="ISampleInfo"/>.
        /// An <see cref="ISampleInfo"/> can be applied later via <see cref="Apply"/>.
        /// </summary>
        public PoolableSkinnableSample()
        {
            InternalChild = sampleContainer = new AudioContainer<DrawableSample> { RelativeSizeAxes = Axes.Both };
        }

        /// <summary>
        /// Creates a new <see cref="PoolableSkinnableSample"/> with an applied <see cref="ISampleInfo"/>.
        /// </summary>
        /// <param name="sampleInfo">The <see cref="ISampleInfo"/> to attach.</param>
        public PoolableSkinnableSample(ISampleInfo sampleInfo)
            : this()
        {
            Apply(sampleInfo);
        }

        /// <summary>
        /// Applies an <see cref="ISampleInfo"/> that describes the sample to retrieve.
        /// Only one <see cref="ISampleInfo"/> can ever be applied to a <see cref="PoolableSkinnableSample"/>.
        /// </summary>
        /// <param name="sampleInfo">The <see cref="ISampleInfo"/> to apply.</param>
        /// <exception cref="InvalidOperationException">If an <see cref="ISampleInfo"/> has already been applied to this <see cref="PoolableSkinnableSample"/>.</exception>
        public void Apply(ISampleInfo sampleInfo)
        {
            if (this.sampleInfo != null)
                throw new InvalidOperationException($"A {nameof(PoolableSkinnableSample)} cannot be applied multiple {nameof(ISampleInfo)}s.");

            this.sampleInfo = sampleInfo;

            Volume.Value = sampleInfo.Volume / 100.0;

            if (LoadState >= LoadState.Ready)
                updateSample();
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            updateSample();
        }

        /// <summary>
        /// Whether this sample was playing before a skin source change.
        /// </summary>
        private bool wasPlaying;

        private void clearPreviousSamples()
        {
            // only run if the samples aren't already cleared.
            // this ensures the "wasPlaying" state is stored correctly even if multiple clear calls are executed.
            if (!sampleContainer.Any()) return;

            wasPlaying = Playing;

            foreach (DrawableSample sample in sampleContainer.Children.ToArray())
            {
                // AudioContainer.Clear() queues child disposal asynchronously. The revision participant may adopt B
                // as soon as SkinChanged returns, so synchronously destroy every exact A sample before that release.
                if (!sampleContainer.Remove(sample, true) || !IsOwnedSampleDisposed(sample))
                    throw new InvalidOperationException("The previous skinnable sample could not be synchronously detached.");
            }

            Sample = null;
        }

        private void updateSample()
        {
            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                return;

            applyPendingSampleSwap();
            clearPreviousSamples();

            if (sampleInfo == null)
                return;

            var sample = CurrentSkin.GetSample(sampleInfo);

            if (sample == null)
                return;

            sampleContainer.Add(Sample = new RevisionDrawableSample(sample));

            // Start playback internally for the new sample if the previous one was playing beforehand.
            if (wasPlaying && Looping)
                Play();
        }

        private void commitPreparedSample(PendingSampleSwap swap)
        {
            lock (revisionMaintenanceGate)
            {
                if (revisionWorkShutdownRequested != 0
                    || pendingSampleSwap != null
                    || !ReferenceEquals(Sample, swap.PreviousSample))
                {
                    throw new InvalidOperationException("The prepared sample owner changed before publication.");
                }

                // The host callback was prepared before entering this barrier. These are only reversible fields;
                // real graph mutation and A's cleanup lease release happen outside publication, even when hidden.
                pendingSampleSwap = swap;
                Sample = swap.PreparedSample;
                finishSamplePreparation(swap);
            }
        }

        private void rollbackPreparedSample(PendingSampleSwap swap)
        {
            lock (revisionMaintenanceGate)
            {
                if (ReferenceEquals(pendingSampleSwap, swap))
                {
                    pendingSampleSwap = null;
                    Sample = swap.PreviousSample;
                }
            }
        }

        private void abortPreparedSample(PendingSampleSwap swap)
        {
            SkinCurrentRevisionLease release = null;

            try
            {
                lock (revisionMaintenanceGate)
                {
                    finishSamplePreparation(swap);

                    try
                    {
                        swap.DisposePreparedSample();
                    }
                    finally
                    {
                        release = swap.TakePreviousRevisionLease();
                    }
                }
            }
            finally
            {
                release?.Dispose();
            }
        }

        private void finishSamplePreparation(PendingSampleSwap swap)
        {
            if (!swap.PreparationPending)
                return;

            swap.PreparationPending = false;
            preparedSampleSwaps--;
        }

        private void maintainRevisionSamples()
        {
            RevisionLeaseReleases releases = default;

            try
            {
                lock (revisionMaintenanceGate)
                {
                    if (revisionWorkShutdownRequested != 0 || IsDisposed)
                    {
                        takeRevisionMaintenance()?.Cancel();
                        return;
                    }

                    applyPendingSampleSwap(ref releases);
                    releaseCompletedRevisionChannels(ref releases);

                    if (preparedSampleSwaps != 0 || pendingSampleSwap != null)
                        return;

                    foreach (ActiveRevisionChannel channel in revisionChannels)
                    {
                        if (!ReferenceEquals(channel.Sample, Sample))
                            return;
                    }

                    takeRevisionMaintenance()?.Cancel();
                }
            }
            finally
            {
                releases.Dispose();
            }
        }

        private ScheduledDelegate takeRevisionMaintenance()
        {
            ScheduledDelegate maintenance = revisionMaintenance;
            revisionMaintenance = null;
            return maintenance;
        }

        /// <summary>
        /// Plays the sample.
        /// </summary>
        public void Play()
        {
            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                return;

            FlushPendingSkinChanges();

            if (Sample == null)
                return;

            activeChannel = Sample.GetChannel();
            SkinCurrentRevisionLease revisionLease = AcquireRevisionWorkLease();

            if (revisionLease != null)
                revisionChannels.Add(new ActiveRevisionChannel(Sample, activeChannel, revisionLease));

            activeChannel.Looping = Looping;
            activeChannel.Play();

            Played = true;
        }

        /// <summary>
        /// Stops the sample.
        /// </summary>
        public void Stop()
        {
            activeChannel?.Stop();
            activeChannel = null;
            releaseCompletedRevisionChannels();
        }

        /// <summary>
        /// Whether the sample is currently playing.
        /// </summary>
        public bool Playing => activeChannel?.Playing ?? false;

        public bool Played { get; private set; }

        protected override void Update()
        {
            base.Update();

            if (Volatile.Read(ref revisionWorkShutdownRequested) != 0)
                return;

            applyPendingSampleSwap();
            releaseCompletedRevisionChannels();
        }

        private void applyPendingSampleSwap()
        {
            RevisionLeaseReleases releases = default;

            try
            {
                applyPendingSampleSwap(ref releases);
            }
            finally
            {
                releases.Dispose();
            }
        }

        private void applyPendingSampleSwap(ref RevisionLeaseReleases releases)
        {
            PendingSampleSwap swap = Interlocked.Exchange(ref pendingSampleSwap, null);

            if (swap == null)
                return;

            try
            {
                // Resource lookup/construction already completed in prepare. This only installs the prepared drawable
                // into the live audio hierarchy. A's drawable must remain attached while any channel created from it
                // is still playing; retaining only the channel and revision lease is insufficient because disposing
                // DrawableSample stops all of its channels.
                if (swap.PreparedSample != null && swap.PreparedSample.Parent == null)
                    sampleContainer.Add(swap.PreparedSample);

                removeHistoricalSampleIfUnused(swap.PreviousSample);

                releases.Add(swap.TakePreviousRevisionLease());
            }
            catch
            {
                // Do not release A on a failed detach. Disposal will make a final exact claim of both the hierarchy and
                // this cleanup lease; surfacing the exception prevents a silent split from being treated as success.
                Interlocked.CompareExchange(ref pendingSampleSwap, swap, null);
                throw;
            }
        }

        private void releaseCompletedRevisionChannels()
        {
            RevisionLeaseReleases releases = default;

            try
            {
                releaseCompletedRevisionChannels(ref releases);
            }
            finally
            {
                releases.Dispose();
            }
        }

        private void releaseCompletedRevisionChannels(ref RevisionLeaseReleases releases)
        {
            RevisionLeaseReleases completed = default;
            HashSet<DrawableSample> completedHistoricalSamples = null;

            for (int i = revisionChannels.Count - 1; i >= 0; i--)
            {
                ActiveRevisionChannel channel = revisionChannels[i];

                if (channel.Channel.Playing)
                    continue;

                // Ordinary Stop/Play completes one voice on the current decoded sample. Keep that
                // path, idle updates and still-playing updates free of temporary collections.
                completed.Add(channel.Lease);

                if (!ReferenceEquals(channel.Sample, Sample))
                    (completedHistoricalSamples ??= new HashSet<DrawableSample>()).Add(channel.Sample);

                revisionChannels.RemoveAt(i);
            }

            if (completedHistoricalSamples != null)
            {
                foreach (DrawableSample completedSample in completedHistoricalSamples)
                    removeHistoricalSampleIfUnused(completedSample);
            }

            // Removing the final historical drawable must precede the final old-revision detach. A lease release can
            // synchronously retire its owner; the drawable hierarchy may no longer touch that owner afterwards.
            completed.MoveTo(ref releases);
        }

        private void removeHistoricalSampleIfUnused(DrawableSample sample)
        {
            if (sample == null
                || ReferenceEquals(sample, Sample)
                || revisionChannels.Any(channel => ReferenceEquals(channel.Sample, sample)))
            {
                return;
            }

            // AudioContainer<T> delegates child ownership to its internal Container<T>, so a child's Parent is not the
            // public wrapper. Let the container perform the exact membership check; a successful removal disposes the
            // historical DrawableSample synchronously before its final revision lease can be released.
            sampleContainer.Remove(sample, true);
        }

        protected override void Dispose(bool isDisposing)
        {
            PendingSampleSwap pendingSwap;
            ActiveRevisionChannel[] channels;
            ScheduledDelegate maintenance;

            lock (revisionMaintenanceGate)
            {
                // A host callback can run while this hidden drawable is being disposed on another thread. Claim
                // terminal state and wait for any graph maintenance before allowing base to tear down that graph.
                Volatile.Write(ref revisionWorkShutdownRequested, 1);
                Volatile.Write(ref revisionWorkGraphReaped, 1);
                maintenance = takeRevisionMaintenance();
                pendingSwap = Interlocked.Exchange(ref pendingSampleSwap, null);
                channels = revisionChannels.ToArray();

                foreach (ActiveRevisionChannel channel in channels)
                {
                    try
                    {
                        channel.Channel.Stop();
                    }
                    catch
                    {
                        // Base disposal remains the exact graph cleanup boundary even if one backend channel faults.
                    }
                }
            }

            maintenance?.Cancel();

            try
            {
                // A committed pending swap has already exposed PreparedSample as B but has not yet parented it into
                // the base drawable hierarchy. Dispose that provisional B resource while the formal B participant is
                // still attached; after shutdown, base.Dispose() may perform the final B detach and retire its owner.
                pendingSwap?.DisposePreparedSample();
            }
            finally
            {
                try
                {
                    // The parented A/B drawable and channel hierarchy is removed before the formal participant detach.
                    // Participant detach enters the publication lock. Do not hold revisionMaintenanceGate here:
                    // a concurrent publication enters that gate from inside the same publication lock.
                    base.Dispose(isDisposing);
                }
                finally
                {
                    // The pending swap's previous A work lease is intentionally released only after base has destroyed
                    // the historical A drawable graph. Both phases are idempotent against rollback/disposal races.
                    pendingSwap?.Complete();

                    foreach (ActiveRevisionChannel channel in channels)
                        channel.Lease.Dispose();

                    lock (revisionMaintenanceGate)
                    {
                        revisionChannels.Clear();
                        activeChannel = null;
                    }
                }
            }
        }

        internal bool IsOwnedSampleDisposed(DrawableSample sample)
            => sample is RevisionDrawableSample { HasBeenDisposed: true };

        private bool looping;

        /// <summary>
        /// Whether the sample should loop on completion.
        /// </summary>
        public bool Looping
        {
            get => looping;
            set
            {
                looping = value;

                if (activeChannel != null)
                    activeChannel.Looping = value;
            }
        }

        #region Re-expose AudioContainer

        public BindableNumber<double> Volume => sampleContainer.Volume;

        public BindableNumber<double> Balance => sampleContainer.Balance;

        public BindableNumber<double> Frequency => sampleContainer.Frequency;

        public BindableNumber<double> Tempo => sampleContainer.Tempo;

        public void BindAdjustments(IAggregateAudioAdjustment component) => sampleContainer.BindAdjustments(component);

        public void UnbindAdjustments(IAggregateAudioAdjustment component) => sampleContainer.UnbindAdjustments(component);

        public void AddAdjustment(AdjustableProperty type, IBindable<double> adjustBindable) => sampleContainer.AddAdjustment(type, adjustBindable);

        public void RemoveAdjustment(AdjustableProperty type, IBindable<double> adjustBindable) => sampleContainer.RemoveAdjustment(type, adjustBindable);

        public void RemoveAllAdjustments(AdjustableProperty type) => sampleContainer.RemoveAllAdjustments(type);

        public IBindable<double> AggregateVolume => sampleContainer.AggregateVolume;

        public IBindable<double> AggregateBalance => sampleContainer.AggregateBalance;

        public IBindable<double> AggregateFrequency => sampleContainer.AggregateFrequency;

        public IBindable<double> AggregateTempo => sampleContainer.AggregateTempo;

        #endregion

        /// <summary>
        /// Transfers leases only after their drawable cleanup succeeded. Releasing may synchronously retire an owner
        /// and enter the publication lock, so callers drain this value after leaving revisionMaintenanceGate.
        /// The idle and single-release paths allocate nothing, including ordinary sample Stop/Update.
        /// </summary>
        private struct RevisionLeaseReleases : IDisposable
        {
            private SkinCurrentRevisionLease first;
            private List<SkinCurrentRevisionLease> additional;

            public void Add(SkinCurrentRevisionLease lease)
            {
                if (lease == null)
                    return;

                if (first == null)
                    first = lease;
                else
                    (additional ??= new List<SkinCurrentRevisionLease>()).Add(lease);
            }

            public void MoveTo(ref RevisionLeaseReleases target)
            {
                if (first == null)
                    return;

                if (target.first == null)
                {
                    target = this;
                    this = default;
                    return;
                }

                // Reuse the completed-channel list when combining it with a single pending-swap release.
                if (target.additional == null && additional != null)
                {
                    target.additional = additional;
                    additional = null;
                }

                target.Add(first);

                if (additional != null)
                {
                    foreach (SkinCurrentRevisionLease lease in additional)
                        target.Add(lease);
                }

                this = default;
            }

            public void Dispose()
            {
                SkinCurrentRevisionLease ownedFirst = first;
                List<SkinCurrentRevisionLease> ownedAdditional = additional;
                this = default;

                try
                {
                    ownedFirst?.Dispose();
                }
                finally
                {
                    if (ownedAdditional != null)
                    {
                        foreach (SkinCurrentRevisionLease lease in ownedAdditional)
                            lease.Dispose();
                    }
                }
            }
        }

        private readonly struct ActiveRevisionChannel
        {
            public DrawableSample Sample { get; }
            public SampleChannel Channel { get; }
            public SkinCurrentRevisionLease Lease { get; }

            public ActiveRevisionChannel(
                DrawableSample sample,
                SampleChannel channel,
                SkinCurrentRevisionLease lease)
            {
                Sample = sample;
                Channel = channel;
                Lease = lease;
            }
        }

        private sealed partial class RevisionDrawableSample : DrawableSample
        {
            public bool HasBeenDisposed => IsDisposed;

            public RevisionDrawableSample(ISample sample)
                : base(sample)
            {
            }
        }

        private sealed class PendingSampleSwap
        {
            private readonly object preparedSampleDisposalGate = new object();
            private SkinCurrentRevisionLease previousRevisionLease;
            private bool preparedSampleDisposed;

            public DrawableSample PreviousSample { get; }
            public DrawableSample PreparedSample { get; }
            public bool PreparationPending { get; set; }

            public PendingSampleSwap(
                DrawableSample previousSample,
                DrawableSample preparedSample,
                SkinCurrentRevisionLease previousRevisionLease)
            {
                PreviousSample = previousSample;
                PreparedSample = preparedSample;
                this.previousRevisionLease = previousRevisionLease;
            }

            public void Complete()
                => TakePreviousRevisionLease()?.Dispose();

            public SkinCurrentRevisionLease TakePreviousRevisionLease()
                => Interlocked.Exchange(ref previousRevisionLease, null);

            public void DisposePreparedSample()
            {
                lock (preparedSampleDisposalGate)
                {
                    if (preparedSampleDisposed)
                        return;

                    PreparedSample?.Dispose();
                    preparedSampleDisposed = true;
                }
            }

            public void MarkPreparedSampleDisposed()
            {
                lock (preparedSampleDisposalGate)
                    preparedSampleDisposed = true;
            }
        }
    }
}
