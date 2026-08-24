// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Stable, ruleset-neutral identity for the backing authority of one published skin revision.
    /// </summary>
    internal enum SkinCurrentRevisionSourceKind
    {
        ProtectedFallback,
        RealmPackage,
        ManagedFolder,
        ExternalFolder,
        Compatibility,
    }

    /// <summary>
    /// The lifecycle contract of an object which can retain resources from the current skin revision.
    /// </summary>
    internal enum SkinRevisionParticipantKind
    {
        CoherentVisualConsumer,
        LifecycleHolder,
        LiveGameplayHost,
    }

    internal enum SkinRevisionBarrierRejectionReason
    {
        None,
        Shutdown,
        LiveGameplayActive,
        ParticipantRejected,
        ParticipantSetChanged,
        CurrentRevisionChanged,
    }

    /// <summary>
    /// One immutable, published package revision and the single owner of all resources prepared for it.
    /// </summary>
    /// <remarks>
    /// The manager and every attached participant hold a lease. Retirement is claimed exactly once when the final
    /// lease detaches. Protected built-ins retire their publication wrapper but deliberately keep their reusable owner.
    /// </remarks>
    internal sealed class SkinCurrentRevision
    {
        private readonly object sync = new object();
        private readonly Action<SkinCurrentRevision> retire;
        private readonly TaskCompletionSource detachCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource retireCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? consumerDetachCompletion;
        private TaskCompletionSource? workDetachCompletion;
        private int leaseCount = 1;
        private int participantLeaseCount;
        private int workLeaseCount;
        private bool retirementClaimed;
        private int ownerRetired;

        public long Generation { get; }

        public Guid RecordId { get; }

        public string ContentRevision { get; }

        public SkinCurrentRevisionSourceKind SourceKind { get; }

        public Skin Owner { get; }

        public bool KeepsReusableOwner { get; }

        public Task Detached => detachCompletion.Task;

        public Task Retired => retireCompletion.Task;

        public Task ConsumersDetached
        {
            get
            {
                lock (sync)
                    return participantLeaseCount == 0
                        ? Task.CompletedTask
                        : consumerDetachCompletion!.Task;
            }
        }

        /// <summary>
        /// Completes after every owner-touching asynchronous job for this revision has really stopped. Visual/tail
        /// holders are deliberately excluded so shutdown can join hidden work without impersonating drawable detach.
        /// </summary>
        public Task WorkDetached
        {
            get
            {
                lock (sync)
                    return workDetachCompletion?.Task ?? Task.CompletedTask;
            }
        }

        internal int LeaseCount
        {
            get
            {
                lock (sync)
                    return leaseCount;
            }
        }

        internal int ParticipantLeaseCount
        {
            get
            {
                lock (sync)
                    return participantLeaseCount;
            }
        }

        internal SkinCurrentRevision(
            long generation,
            Guid recordId,
            string contentRevision,
            SkinCurrentRevisionSourceKind sourceKind,
            Skin owner,
            bool keepsReusableOwner,
            Action<SkinCurrentRevision> retire)
        {
            Generation = generation;
            RecordId = recordId;
            ContentRevision = string.IsNullOrEmpty(contentRevision) ? "unversioned" : contentRevision;
            SourceKind = sourceKind;
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            KeepsReusableOwner = keepsReusableOwner;
            this.retire = retire ?? throw new ArgumentNullException(nameof(retire));
        }

        internal SkinCurrentRevisionLease AcquireParticipantLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(retirementClaimed, this);

                checked
                {
                    leaseCount++;
                }

                if (participantLeaseCount++ == 0)
                {
                    consumerDetachCompletion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            return new SkinCurrentRevisionLease(this, participant: true, work: false);
        }

        internal SkinCurrentRevisionLease AcquireWorkLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(retirementClaimed, this);

                checked
                {
                    leaseCount++;
                }

                if (participantLeaseCount++ == 0)
                {
                    consumerDetachCompletion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }

                if (workLeaseCount++ == 0)
                {
                    workDetachCompletion = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            return new SkinCurrentRevisionLease(this, participant: true, work: true);
        }

        internal SkinCurrentRevisionLease AcquireOperationLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(retirementClaimed, this);

                checked
                {
                    leaseCount++;
                }
            }

            return new SkinCurrentRevisionLease(this, participant: false, work: false);
        }

        internal void ReleaseManagerLease() => releaseLease();

        internal void AddManagerLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(retirementClaimed, this);

                checked
                {
                    leaseCount++;
                }
            }
        }

        internal void ReleaseLease(bool participant, bool work) => releaseLease(participant, work);

        private void releaseLease(bool participant = false, bool work = false)
        {
            bool shouldRetire = false;
            TaskCompletionSource? detachedConsumers = null;
            TaskCompletionSource? detachedWork = null;

            lock (sync)
            {
                if (leaseCount <= 0)
                    return;

                if (participant && participantLeaseCount <= 0)
                    return;

                if (work && (!participant || workLeaseCount <= 0))
                    return;

                leaseCount--;

                if (participant)
                {
                    participantLeaseCount--;

                    if (participantLeaseCount == 0)
                        detachedConsumers = consumerDetachCompletion;
                }

                if (work)
                {
                    workLeaseCount--;

                    if (workLeaseCount == 0)
                        detachedWork = workDetachCompletion;
                }

                if (leaseCount == 0 && !retirementClaimed)
                {
                    retirementClaimed = true;
                    shouldRetire = true;
                }
            }

            detachedConsumers?.TrySetResult();

            if (!shouldRetire)
            {
                detachedWork?.TrySetResult();
                return;
            }

            detachCompletion.TrySetResult();

            try
            {
                retire(this);
            }
            finally
            {
                // Shutdown waits WorkDetached before Realm teardown. If this work lease was also the final revision
                // lease, do not wake that waiter until synchronous shutdown retirement has reaped the exact owner.
                detachedWork?.TrySetResult();
            }
        }

        internal void RetireOwner()
        {
            if (Interlocked.Exchange(ref ownerRetired, 1) != 0)
                return;

            try
            {
                if (!KeepsReusableOwner)
                    Owner.Dispose();

                retireCompletion.TrySetResult();
            }
            catch (Exception exception)
            {
                retireCompletion.TrySetException(exception);
            }
        }

        public override string ToString()
            => $"{nameof(SkinCurrentRevision)}:{SourceKind}:Generation{Generation}";
    }

    internal sealed class SkinCurrentRevisionLease : IDisposable
    {
        private SkinCurrentRevision? revision;
        private readonly bool participant;
        private readonly bool work;

        internal SkinCurrentRevision Revision
            => Volatile.Read(ref revision) ?? throw new ObjectDisposedException(nameof(SkinCurrentRevisionLease));

        internal SkinCurrentRevisionLease(SkinCurrentRevision revision, bool participant, bool work)
        {
            this.revision = revision ?? throw new ArgumentNullException(nameof(revision));
            this.participant = participant;
            this.work = work;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref revision, null)?.ReleaseLease(participant, work);
        }
    }

    /// <summary>
    /// Transfers one exact revision lease from an asynchronous consumer to owner-internal work which may outlive the
    /// consumer's cancellable wait. If no owner claims it, disposing the transfer releases the original lease.
    /// </summary>
    internal sealed class SkinCurrentRevisionLeaseTransfer : IDisposable
    {
        private SkinCurrentRevisionLease? lease;

        internal SkinCurrentRevisionLeaseTransfer(SkinCurrentRevisionLease lease)
        {
            this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        }

        internal SkinCurrentRevisionLease? TryTake()
            => Interlocked.Exchange(ref lease, null);

        public void Dispose()
            => Interlocked.Exchange(ref lease, null)?.Dispose();
    }

    internal sealed class SkinRevisionParticipantSnapshot
    {
        internal long ParticipantGeneration { get; }
        internal SkinCurrentRevision CurrentRevision { get; }
        internal IReadOnlyList<SkinRevisionParticipantRegistration> Participants { get; }

        internal SkinRevisionParticipantSnapshot(
            long participantGeneration,
            SkinCurrentRevision currentRevision,
            IReadOnlyList<SkinRevisionParticipantRegistration> participants)
        {
            ParticipantGeneration = participantGeneration;
            CurrentRevision = currentRevision;
            Participants = participants;
        }
    }

    /// <summary>
    /// A fully prepared, allocation-free update-thread swap for one coherent visual consumer.
    /// </summary>
    /// <remarks>
    /// Preparation and disposal may perform fallible work. <see cref="Apply"/> is deliberately a reversible action
    /// which may only exchange already prepared references. It must not perform lookups, loading, parsing or I/O.
    /// </remarks>
    internal sealed class SkinRevisionParticipantCommit : IDisposable
    {
        private Action? commit;
        private Action? rollback;
        private Action? abort;
        private SkinCurrentRevisionLease? preparedLease;
        private int applied;

        internal SkinRevisionParticipantCommit(Action commit, Action rollback, Action? abort = null)
        {
            this.commit = commit ?? throw new ArgumentNullException(nameof(commit));
            this.rollback = rollback ?? throw new ArgumentNullException(nameof(rollback));
            this.abort = abort;
        }

        internal void AttachPreparedLease(SkinCurrentRevisionLease lease)
        {
            ArgumentNullException.ThrowIfNull(lease);

            if (Interlocked.CompareExchange(ref preparedLease, lease, null) != null)
                throw new InvalidOperationException("A participant receipt already owns a prepared revision lease.");
        }

        internal void Apply()
        {
            Action? action = Volatile.Read(ref commit) ?? throw new InvalidOperationException("A participant receipt was already consumed.");
            Volatile.Write(ref applied, 1);
            action();
        }

        internal void Rollback()
        {
            if (Interlocked.Exchange(ref applied, 0) == 0)
                return;

            Volatile.Read(ref rollback)?.Invoke();
        }

        internal SkinCurrentRevisionLease TakePreparedLease()
            => Interlocked.Exchange(ref preparedLease, null)
               ?? throw new InvalidOperationException("A participant receipt has no prepared revision lease.");

        internal void Complete()
        {
            Interlocked.Exchange(ref commit, null);
            Interlocked.Exchange(ref rollback, null);
            Interlocked.Exchange(ref abort, null);
            Volatile.Write(ref applied, 0);
        }

        public void Dispose()
        {
            Rollback();
            Interlocked.Exchange(ref commit, null);
            Interlocked.Exchange(ref rollback, null);
            Interlocked.Exchange(ref abort, null)?.Invoke();
            Interlocked.Exchange(ref preparedLease, null)?.Dispose();
        }
    }

    /// <summary>
    /// The exact participant generation and prepared swaps admitted to one publication barrier.
    /// </summary>
    internal sealed class SkinRevisionPreparedBarrier : IDisposable
    {
        private KeyValuePair<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit>[]? commits;
        private SkinCurrentRevisionLease?[]? previousLeases;

        internal SkinRevisionParticipantSnapshot Snapshot { get; }

        internal SkinCurrentRevision NextRevision { get; }

        internal SkinRevisionPreparedBarrier(
            SkinRevisionParticipantSnapshot snapshot,
            SkinCurrentRevision nextRevision,
            Dictionary<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit> commits)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            NextRevision = nextRevision ?? throw new ArgumentNullException(nameof(nextRevision));
            ArgumentNullException.ThrowIfNull(commits);
            this.commits = commits.ToArray();
            previousLeases = new SkinCurrentRevisionLease?[commits.Count];
        }

        internal KeyValuePair<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit>[] TakeCommits(
            out SkinCurrentRevisionLease?[] detachedPreviousLeases)
        {
            KeyValuePair<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit>[] owned =
                Interlocked.Exchange(ref commits, null)
                ?? throw new InvalidOperationException("The prepared participant barrier was already consumed.");
            detachedPreviousLeases = Interlocked.Exchange(ref previousLeases, null)
                                     ?? throw new InvalidOperationException("The prepared participant lease buffer was already consumed.");
            return owned;
        }

        public void Dispose()
        {
            KeyValuePair<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit>[]? owned =
                Interlocked.Exchange(ref commits, null);
            SkinCurrentRevisionLease?[]? ownedPreviousLeases = Interlocked.Exchange(ref previousLeases, null);

            if (owned == null)
                return;

            foreach ((_, SkinRevisionParticipantCommit commit) in owned)
                commit.Dispose();

            if (ownedPreviousLeases != null)
            {
                foreach (SkinCurrentRevisionLease? lease in ownedPreviousLeases)
                    lease?.Dispose();
            }
        }
    }

    internal sealed class SkinRevisionParticipantPrepareResult
    {
        internal SkinRevisionBarrierRejectionReason RejectionReason { get; }

        internal SkinRevisionPreparedBarrier? Barrier { get; }

        internal SkinRevisionParticipantRegistration? BlockingParticipant { get; }

        internal bool IsSuccess => RejectionReason == SkinRevisionBarrierRejectionReason.None && Barrier != null;

        private SkinRevisionParticipantPrepareResult(
            SkinRevisionBarrierRejectionReason rejectionReason,
            SkinRevisionPreparedBarrier? barrier,
            SkinRevisionParticipantRegistration? blockingParticipant)
        {
            RejectionReason = rejectionReason;
            Barrier = barrier;
            BlockingParticipant = blockingParticipant;
        }

        internal static SkinRevisionParticipantPrepareResult Success(SkinRevisionPreparedBarrier barrier)
            => new SkinRevisionParticipantPrepareResult(
                SkinRevisionBarrierRejectionReason.None,
                barrier ?? throw new ArgumentNullException(nameof(barrier)),
                null);

        internal static SkinRevisionParticipantPrepareResult Reject(
            SkinRevisionBarrierRejectionReason reason,
            SkinRevisionParticipantRegistration? blockingParticipant = null)
            => new SkinRevisionParticipantPrepareResult(reason, null, blockingParticipant);
    }

    /// <summary>
    /// A participant registration owns the exact revision lease retained by one production consumer or holder.
    /// </summary>
    internal sealed class SkinRevisionParticipantRegistration : IDisposable
    {
        private readonly SkinCurrentRevisionPublication publication;
        private readonly object leaseGate = new object();
        private readonly TaskCompletionSource detachedCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private Action? shutdownWork;
        private SkinCurrentRevisionLease? lease;
        private int disposed;

        internal long Id { get; }
        internal SkinRevisionParticipantKind Kind { get; }
        internal string DiagnosticName { get; }
        internal Func<CancellationToken, Task<bool>>? Prepare { get; }
        internal Func<SkinCurrentRevision, CancellationToken, Task<SkinRevisionParticipantCommit?>>? PrepareCommit { get; }
        internal bool BlocksRevisionPublication { get; }

        internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

        internal Task Detached => detachedCompletion.Task;

        internal SkinCurrentRevision CurrentRevision => lease!.Revision;

        internal SkinRevisionParticipantRegistration(
            SkinCurrentRevisionPublication publication,
            long id,
            SkinRevisionParticipantKind kind,
            string diagnosticName,
            Func<CancellationToken, Task<bool>>? prepare,
            Func<SkinCurrentRevision, CancellationToken, Task<SkinRevisionParticipantCommit?>>? prepareCommit,
            bool blocksRevisionPublication,
            Action? shutdownWork,
            SkinCurrentRevisionLease lease)
        {
            this.publication = publication;
            Id = id;
            Kind = kind;
            DiagnosticName = diagnosticName;
            Prepare = prepare;
            PrepareCommit = prepareCommit;
            BlocksRevisionPublication = blocksRevisionPublication;
            this.shutdownWork = shutdownWork;
            this.lease = lease;
        }

        /// <summary>
        /// Requests the exact consumer which owns hidden revision work to cancel or reap that work. This never
        /// releases either the participant lease or a work lease on the consumer's behalf; the real owner must still
        /// finish through its ordinary completion path, including owner-internal graph reaping when teardown is
        /// required before a lease can detach.
        /// </summary>
        internal void RequestShutdownWork()
        {
            Action? request = Interlocked.Exchange(ref shutdownWork, null);

            if (request == null)
                return;

            try
            {
                request();
            }
            catch
            {
                // Shutdown requests are best-effort signals. The exact WorkDetached fence below remains authoritative
                // and must never be bypassed by a consumer callback failure or expose consumer-controlled diagnostics.
            }
        }

        /// <summary>
        /// Called only after this participant has synchronously rebuilt or replaced everything which referenced the
        /// previous owner. The new lease is acquired before the old one is detached.
        /// </summary>
        internal void AdoptCurrentRevision()
            => publication.AdoptCurrentRevision(this);

        internal void AdoptUnderPublicationLock(SkinCurrentRevision revision)
        {
            SkinCurrentRevisionLease? previous;

            lock (leaseGate)
            {
                if (IsDisposed || lease == null || ReferenceEquals(lease.Revision, revision))
                    return;

                SkinCurrentRevisionLease replacement = revision.AcquireParticipantLease();
                previous = lease;
                lease = replacement;
            }

            previous.Dispose();
        }

        internal SkinCurrentRevisionLease CommitPreparedLeaseUnderPublicationLock(
            SkinCurrentRevisionLease replacement)
        {
            ArgumentNullException.ThrowIfNull(replacement);

            lock (leaseGate)
            {
                if (IsDisposed || lease == null)
                    throw new InvalidOperationException("A detached participant reached the publication barrier.");

                SkinCurrentRevisionLease previous = lease;
                lease = replacement;
                return previous;
            }
        }

        internal SkinCurrentRevisionLease? AcquireWorkLease()
            => publication.AcquireWorkLease(this);

        internal SkinCurrentRevisionLease? AcquireWorkLeaseUnderPublicationLock()
        {
            lock (leaseGate)
            {
                if (IsDisposed || lease == null)
                    return null;

                return lease.Revision.AcquireWorkLease();
            }
        }

        internal bool TryDisposeAndDetachUnderPublicationLock(out SkinCurrentRevisionLease? detached)
        {
            lock (leaseGate)
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                {
                    detached = null;
                    return false;
                }

                detached = lease;
                lease = null;
                return true;
            }
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref shutdownWork, null);

            try
            {
                publication.UnregisterAndDetach(this)?.Dispose();
            }
            finally
            {
                detachedCompletion.TrySetResult();
            }
        }
    }

    /// <summary>
    /// Ruleset-neutral registry and commit barrier for current package revisions.
    /// </summary>
    internal sealed class SkinCurrentRevisionPublication
    {
        private readonly object sync = new object();
        private readonly HashSet<SkinRevisionParticipantRegistration> participants = new HashSet<SkinRevisionParticipantRegistration>();
        private readonly HashSet<SkinCurrentRevision> liveRevisions = new HashSet<SkinCurrentRevision>();
        private readonly Action<SkinCurrentRevision> queueRetirement;
        private SkinCurrentRevision current;
        private long participantGeneration;
        private long registrationId;
        private long revisionGeneration;
        private bool shutdown;

        internal SkinCurrentRevision Current
        {
            get
            {
                lock (sync)
                    return current;
            }
        }

        internal SkinCurrentRevisionPublication(
            Skin initialOwner,
            string initialContentRevision,
            SkinCurrentRevisionSourceKind initialSourceKind,
            bool keepsReusableOwner,
            Action<SkinCurrentRevision> queueRetirement)
        {
            this.queueRetirement = queueRetirement ?? throw new ArgumentNullException(nameof(queueRetirement));
            current = new SkinCurrentRevision(
                ++revisionGeneration,
                initialOwner.SkinInfo.ID,
                initialContentRevision,
                initialSourceKind,
                initialOwner,
                keepsReusableOwner,
                queueRevisionRetirement);
            liveRevisions.Add(current);
        }

        internal SkinCurrentRevision CreateProvisional(
            Guid recordId,
            string contentRevision,
            SkinCurrentRevisionSourceKind sourceKind,
            Skin owner,
            bool keepsReusableOwner = false)
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(shutdown, this);

                var revision = new SkinCurrentRevision(
                    ++revisionGeneration,
                    recordId,
                    contentRevision,
                    sourceKind,
                    owner,
                    keepsReusableOwner,
                    queueRevisionRetirement);
                liveRevisions.Add(revision);
                return revision;
            }
        }

        private void queueRevisionRetirement(SkinCurrentRevision revision)
        {
            queueRetirement(revision);

            // A final work release may synchronously enter owner reaping while shutdown snapshots its join fences.
            // Keep the revision discoverable until RetireOwner has actually completed; removing it at claim time lets
            // Realm teardown miss an in-flight exact owner disposal.
            _ = revision.Retired.ContinueWith(
                _ =>
                {
                    lock (sync)
                        liveRevisions.Remove(revision);
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        internal SkinRevisionParticipantRegistration Register(
            SkinRevisionParticipantKind kind,
            string diagnosticName,
            Func<CancellationToken, Task<bool>>? prepare = null,
            Func<SkinCurrentRevision, CancellationToken, Task<SkinRevisionParticipantCommit?>>? prepareCommit = null,
            bool blocksRevisionPublication = false,
            Action? shutdownWork = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(diagnosticName);

            lock (sync)
            {
                ObjectDisposedException.ThrowIf(shutdown, this);

                var registration = new SkinRevisionParticipantRegistration(
                    this,
                    ++registrationId,
                    kind,
                    diagnosticName,
                    prepare,
                    prepareCommit,
                    blocksRevisionPublication,
                    shutdownWork,
                    current.AcquireParticipantLease());
                participants.Add(registration);
                participantGeneration++;
                return registration;
            }
        }

        internal SkinRevisionParticipantRegistration? RegisterExactOwner(
            Skin owner,
            SkinRevisionParticipantKind kind,
            string diagnosticName)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentException.ThrowIfNullOrEmpty(diagnosticName);

            lock (sync)
            {
                if (shutdown || !ReferenceEquals(current.Owner, owner))
                    return null;

                var registration = new SkinRevisionParticipantRegistration(
                    this,
                    ++registrationId,
                    kind,
                    diagnosticName,
                    prepare: null,
                    prepareCommit: null,
                    blocksRevisionPublication: false,
                    shutdownWork: null,
                    lease: current.AcquireParticipantLease());
                participants.Add(registration);
                participantGeneration++;
                return registration;
            }
        }

        internal SkinRevisionParticipantSnapshot CaptureSnapshot(
            out SkinRevisionBarrierRejectionReason rejectionReason,
            bool requiresPreparedCoherentReceipts = false)
        {
            lock (sync)
            {
                if (shutdown)
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.Shutdown;
                    return null!;
                }

                if (participants.Any(participant => participant.Kind == SkinRevisionParticipantKind.LiveGameplayHost))
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.LiveGameplayActive;
                    return null!;
                }

                // A background-loading consumer can already perform dynamic source lookups before its fully-built
                // replacement receipt exists. Such a temporary participant blocks every publication, including a
                // retaining selection, so no unleased intermediate owner can become visible to the loader. For staged
                // revision publication all attached unsupported coherent consumers are rejected here as well, before
                // package capture, parsing or any other fallible source preparation starts.
                if (participants.Any(participant =>
                        !participant.IsDisposed
                        && (participant.BlocksRevisionPublication
                            || (requiresPreparedCoherentReceipts
                                && participant.Kind == SkinRevisionParticipantKind.CoherentVisualConsumer
                                && participant.PrepareCommit == null))))
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.ParticipantRejected;
                    return null!;
                }

                rejectionReason = SkinRevisionBarrierRejectionReason.None;
                return new SkinRevisionParticipantSnapshot(
                    participantGeneration,
                    current,
                    participants.OrderBy(participant => participant.Id).ToArray());
            }
        }

        /// <summary>
        /// Captures participants for a manager-pair rollback in which every existing consumer deliberately retains its
        /// exact revision and receives no source rebuild. Live gameplay is safe in this mode because it is not changed.
        /// </summary>
        internal SkinRevisionParticipantSnapshot CaptureRetainingSnapshot(
            out SkinRevisionBarrierRejectionReason rejectionReason)
        {
            lock (sync)
            {
                if (shutdown)
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.Shutdown;
                    return null!;
                }

                rejectionReason = SkinRevisionBarrierRejectionReason.None;
                return new SkinRevisionParticipantSnapshot(
                    participantGeneration,
                    current,
                    participants.OrderBy(participant => participant.Id).ToArray());
            }
        }

        internal async Task<SkinRevisionBarrierRejectionReason> PrepareParticipantsAsync(
            SkinRevisionParticipantSnapshot snapshot,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            foreach (SkinRevisionParticipantRegistration participant in snapshot.Participants)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (participant.IsDisposed)
                    continue;

                if (participant.Kind == SkinRevisionParticipantKind.LiveGameplayHost)
                    return SkinRevisionBarrierRejectionReason.LiveGameplayActive;

                if (participant.Prepare != null && !await participant.Prepare(cancellationToken).ConfigureAwait(false))
                    return SkinRevisionBarrierRejectionReason.ParticipantRejected;
            }

            lock (sync)
            {
                if (shutdown)
                    return SkinRevisionBarrierRejectionReason.Shutdown;

                if (!ReferenceEquals(current, snapshot.CurrentRevision))
                    return SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;

                if (participantGeneration != snapshot.ParticipantGeneration)
                    return SkinRevisionBarrierRejectionReason.ParticipantSetChanged;
            }

            return SkinRevisionBarrierRejectionReason.None;
        }

        /// <summary>
        /// Prepares every coherent consumer against an exact provisional revision. Lifecycle holders deliberately keep
        /// their existing revision lease and never query the new current source as part of this publication.
        /// </summary>
        internal async Task<SkinRevisionParticipantPrepareResult> PrepareParticipantsForRevisionAsync(
            SkinRevisionParticipantSnapshot snapshot,
            SkinCurrentRevision nextRevision,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(nextRevision);

            var commits = new Dictionary<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit>();

            try
            {
                foreach (SkinRevisionParticipantRegistration participant in snapshot.Participants)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (participant.IsDisposed)
                        continue;

                    if (participant.Kind == SkinRevisionParticipantKind.LiveGameplayHost)
                        return reject(SkinRevisionBarrierRejectionReason.LiveGameplayActive, participant);

                    bool participantReady;

                    try
                    {
                        participantReady = participant.Prepare == null
                                           || await participant.Prepare(cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantRejected, participant);
                    }

                    if (!participantReady)
                    {
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantRejected, participant);
                    }

                    if (participant.Kind != SkinRevisionParticipantKind.CoherentVisualConsumer)
                        continue;

                    if (participant.PrepareCommit == null)
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantRejected, participant);

                    SkinRevisionParticipantCommit? commit;

                    try
                    {
                        commit = await participant.PrepareCommit(nextRevision, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch
                    {
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantRejected, participant);
                    }

                    if (commit == null)
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantRejected, participant);

                    try
                    {
                        // Every coherent replacement lease is acquired during preparation. The update-thread barrier
                        // therefore performs only reversible reference swaps and cannot allocate a lease midway.
                        commit.AttachPreparedLease(nextRevision.AcquireParticipantLease());
                    }
                    catch
                    {
                        commit.Dispose();
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantRejected, participant);
                    }

                    commits.Add(participant, commit);
                }

                lock (sync)
                {
                    if (shutdown)
                        return reject(SkinRevisionBarrierRejectionReason.Shutdown);

                    if (!ReferenceEquals(current, snapshot.CurrentRevision))
                        return reject(SkinRevisionBarrierRejectionReason.CurrentRevisionChanged);

                    if (participantGeneration != snapshot.ParticipantGeneration)
                        return reject(SkinRevisionBarrierRejectionReason.ParticipantSetChanged);
                }

                return SkinRevisionParticipantPrepareResult.Success(
                    new SkinRevisionPreparedBarrier(snapshot, nextRevision, commits));
            }
            catch
            {
                foreach (SkinRevisionParticipantCommit commit in commits.Values)
                    commit.Dispose();

                throw;
            }

            SkinRevisionParticipantPrepareResult reject(
                SkinRevisionBarrierRejectionReason reason,
                SkinRevisionParticipantRegistration? blockingParticipant = null)
            {
                foreach (SkinRevisionParticipantCommit commit in commits.Values)
                    commit.Dispose();

                commits.Clear();
                return SkinRevisionParticipantPrepareResult.Reject(reason, blockingParticipant);
            }
        }

        /// <summary>
        /// The update-thread commit barrier. No callback, I/O, parsing, allocation-heavy preparation or fallible
        /// participant work is performed here.
        /// </summary>
        internal bool TryCommit(
            SkinRevisionParticipantSnapshot snapshot,
            SkinCurrentRevision next,
            out SkinCurrentRevision previous,
            out SkinRevisionBarrierRejectionReason rejectionReason)
            => tryCommit(snapshot, next, publishManagerPair: null, out previous, out rejectionReason);

        /// <summary>
        /// Commits the revision and its manager-facing owner/selection pair while registration is still excluded by
        /// the publication lock. <paramref name="publishManagerPair"/> is part of the infallible reference-swap
        /// barrier: it must perform no I/O, allocation-heavy work or callbacks which can escape as failures.
        /// </summary>
        internal bool TryCommitPair(
            SkinRevisionParticipantSnapshot snapshot,
            SkinCurrentRevision next,
            Action publishManagerPair,
            out SkinCurrentRevision previous,
            out SkinRevisionBarrierRejectionReason rejectionReason)
            => tryCommit(snapshot, next, publishManagerPair, out previous, out rejectionReason);

        private bool tryCommit(
            SkinRevisionParticipantSnapshot snapshot,
            SkinCurrentRevision next,
            Action? publishManagerPair,
            out SkinCurrentRevision previous,
            out SkinRevisionBarrierRejectionReason rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(next);

            lock (sync)
            {
                previous = current;

                if (shutdown)
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.Shutdown;
                    return false;
                }

                if (!ReferenceEquals(current, snapshot.CurrentRevision))
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                    return false;
                }

                if (participantGeneration != snapshot.ParticipantGeneration)
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.ParticipantSetChanged;
                    return false;
                }

                current = next;
                publishManagerPair?.Invoke();
                rejectionReason = SkinRevisionBarrierRejectionReason.None;
                return true;
            }
        }

        internal bool TryCommit(
            SkinRevisionPreparedBarrier barrier,
            out SkinCurrentRevision previous,
            out SkinRevisionBarrierRejectionReason rejectionReason)
            => tryCommit(barrier, publishManagerPair: null, out previous, out rejectionReason);

        /// <inheritdoc cref="TryCommitPair(SkinRevisionParticipantSnapshot,SkinCurrentRevision,Action,out SkinCurrentRevision,out SkinRevisionBarrierRejectionReason)"/>
        internal bool TryCommitPair(
            SkinRevisionPreparedBarrier barrier,
            Action publishManagerPair,
            out SkinCurrentRevision previous,
            out SkinRevisionBarrierRejectionReason rejectionReason)
            => tryCommit(barrier, publishManagerPair, out previous, out rejectionReason);

        private bool tryCommit(
            SkinRevisionPreparedBarrier barrier,
            Action? publishManagerPair,
            out SkinCurrentRevision previous,
            out SkinRevisionBarrierRejectionReason rejectionReason)
        {
            ArgumentNullException.ThrowIfNull(barrier);

            KeyValuePair<SkinRevisionParticipantRegistration, SkinRevisionParticipantCommit>[]? commits = null;
            SkinCurrentRevisionLease?[]? previousLeases = null;
            int appliedCount = 0;
            bool participantCommitFailed = false;
            rejectionReason = SkinRevisionBarrierRejectionReason.ParticipantRejected;

            lock (sync)
            {
                SkinRevisionParticipantSnapshot snapshot = barrier.Snapshot;
                SkinCurrentRevision next = barrier.NextRevision;
                previous = current;

                if (shutdown)
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.Shutdown;
                    return false;
                }

                if (!ReferenceEquals(current, snapshot.CurrentRevision))
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.CurrentRevisionChanged;
                    return false;
                }

                if (participantGeneration != snapshot.ParticipantGeneration)
                {
                    rejectionReason = SkinRevisionBarrierRejectionReason.ParticipantSetChanged;
                    return false;
                }

                commits = barrier.TakeCommits(out previousLeases);

                try
                {
                    for (; appliedCount < commits.Length; appliedCount++)
                    {
                        SkinRevisionParticipantCommit commit = commits[appliedCount].Value;
                        commit.Apply();
                    }
                }
                catch
                {
                    // Apply() marks the throwing receipt before invoking its pure reference swap. Reverse it and all
                    // preceding receipts while the publication lock is still held, so no observer can see a split
                    // participant set even if it reads from a non-update thread.
                    if (appliedCount < commits.Length)
                    {
                        try
                        {
                            commits[appliedCount].Value.Rollback();
                        }
                        catch
                        {
                            // Keep unwinding every receipt below. Production rollbacks are required to be pure
                            // reference swaps and must not throw.
                        }
                    }

                    for (int i = appliedCount - 1; i >= 0; i--)
                    {
                        try
                        {
                            commits[i].Value.Rollback();
                        }
                        catch
                        {
                            // Production receipts are mandatory pure reference swaps with pure reverse swaps. Keep
                            // unwinding every receipt even if an invalid test/extension violates that internal contract.
                        }
                    }

                    rejectionReason = SkinRevisionBarrierRejectionReason.ParticipantRejected;
                    participantCommitFailed = true;
                }

                if (!participantCommitFailed)
                {
                    for (int i = 0; i < commits.Length; i++)
                    {
                        previousLeases[i] = commits[i].Key.CommitPreparedLeaseUnderPublicationLock(
                            commits[i].Value.TakePreparedLease());
                    }

                    current = next;
                    publishManagerPair?.Invoke();
                    rejectionReason = SkinRevisionBarrierRejectionReason.None;
                }
            }

            if (participantCommitFailed)
            {
                foreach ((_, SkinRevisionParticipantCommit commit) in commits)
                    commit.Dispose();

                foreach (SkinCurrentRevisionLease? lease in previousLeases)
                    lease?.Dispose();

                return false;
            }

            foreach (SkinCurrentRevisionLease? previousLease in previousLeases)
                previousLease?.Dispose();

            // Successful commit receipts no longer own provisional resources.
            foreach ((_, SkinRevisionParticipantCommit commit) in commits)
                commit.Complete();

            return true;
        }

        internal SkinCurrentRevisionLease AcquireCurrentLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(shutdown, this);
                return current.AcquireOperationLease();
            }
        }

        internal SkinCurrentRevisionLease AcquireCurrentHolderLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(shutdown, this);
                return current.AcquireParticipantLease();
            }
        }

        internal SkinCurrentRevisionLease AcquireCurrentWorkLease()
        {
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(shutdown, this);
                return current.AcquireWorkLease();
            }
        }

        internal SkinCurrentRevisionLease? AcquireWorkLease(SkinRevisionParticipantRegistration registration)
        {
            lock (sync)
            {
                if (shutdown || !participants.Contains(registration))
                    return null;

                return registration.AcquireWorkLeaseUnderPublicationLock();
            }
        }

        internal Task[] CaptureRevisionWorkDetachments()
        {
            lock (sync)
                return liveRevisions.Select(revision => revision.WorkDetached).ToArray();
        }

        internal void AdoptCurrentRevision(SkinRevisionParticipantRegistration registration)
        {
            lock (sync)
                registration.AdoptUnderPublicationLock(current);
        }

        internal SkinCurrentRevisionLease? UnregisterAndDetach(SkinRevisionParticipantRegistration registration)
        {
            lock (sync)
            {
                if (!registration.TryDisposeAndDetachUnderPublicationLock(out SkinCurrentRevisionLease? detached))
                    return null;

                if (participants.Remove(registration))
                    participantGeneration++;

                return detached;
            }
        }

        internal IReadOnlyList<SkinRevisionParticipantRegistration> ShutdownAndClaimParticipants()
        {
            SkinRevisionParticipantRegistration[] claimed;

            lock (sync)
            {
                if (shutdown)
                    return Array.Empty<SkinRevisionParticipantRegistration>();

                shutdown = true;
                participantGeneration++;
                claimed = participants.ToArray();
                participants.Clear();
            }

            // A shutdown callback may re-enter participant disposal or other owner code. Claim the immutable set under
            // the publication lock, then let each real owner cancel/reap its work without holding that global lock.
            foreach (SkinRevisionParticipantRegistration participant in claimed)
                participant.RequestShutdownWork();

            return claimed;
        }
    }
}
