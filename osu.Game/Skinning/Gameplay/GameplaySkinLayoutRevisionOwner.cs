// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Development;
using osu.Framework.Logging;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A fully solved immutable gameplay layout which has not yet replaced the owner's current reference.
    /// </summary>
    public sealed class GameplaySkinPreparedLayout : IDisposable
    {
        private SkinCurrentRevisionLease? workLease;
        private IDisposable? retirement;
        private int disposed;

        internal GameplaySkinLayoutRevisionOwner Owner { get; }

        internal GameplaySkinLayoutPublication? Expected { get; }

        internal long AdmissionGeneration { get; }

        internal long ParticipantGeneration { get; }

        internal bool TryConsume(
            out SkinCurrentRevisionLease? retainedWorkLease,
            out IDisposable? retainedRetirement)
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                retainedWorkLease = null;
                retainedRetirement = null;
                return false;
            }

            retainedWorkLease = Interlocked.Exchange(ref workLease, null);
            retainedRetirement = Interlocked.Exchange(ref retirement, null);
            return true;
        }

        public GameplaySkinLayoutPublication Publication { get; }

        public GameplaySkinLayoutSnapshot Snapshot => Publication.Snapshot;

        internal GameplaySkinPreparedLayout(
            GameplaySkinLayoutRevisionOwner owner,
            GameplaySkinLayoutPublication? expected,
            GameplaySkinLayoutPublication publication,
            long admissionGeneration,
            long participantGeneration,
            SkinCurrentRevisionLease? workLease)
        {
            Owner = owner ?? throw new ArgumentNullException(nameof(owner));
            Expected = expected;
            Publication = publication ?? throw new ArgumentNullException(nameof(publication));
            AdmissionGeneration = admissionGeneration;
            ParticipantGeneration = participantGeneration;
            this.workLease = workLease;
            retirement = publication.TakeRetirement();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
                return;

            try
            {
                Interlocked.Exchange(ref retirement, null)?.Dispose();
            }
            finally
            {
                Interlocked.Exchange(ref workLease, null)?.Dispose();
            }
        }
    }

    /// <summary>
    /// Signs and atomically publishes the one layout reference owned by a gameplay root.
    /// </summary>
    /// <remarks>
    /// The root's existing C2 participant lease remains the package lifetime authority. An exact owner admits one
    /// immutable package/layout pair and verifies that its prepared snapshot carries the package token cached by that
    /// root. Detached compatibility owners may issue checked consecutive revisions for isolated tests. Solving may run
    /// on a background thread, while <see cref="TryCommit"/> is a reference-only commit suitable for the update thread.
    /// </remarks>
    public sealed class GameplaySkinLayoutRevisionOwner : IDisposable
    {
        private readonly object sync = new object();
        private readonly Func<bool> validateRoot;
        private readonly Func<SkinCurrentRevisionLease?> acquireWorkLease;
        private readonly Func<long?> captureParticipantGeneration;
        private readonly Func<long, bool> validateParticipantGeneration;
        private readonly Func<long, Action, bool> commitAtParticipantGeneration;
        private readonly Func<Action, bool> dispatchCommit;
        private readonly ConcurrentDictionary<string, byte> emittedMaterialDiagnosticBatches = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
        private readonly MaterialDiagnosticsObserverReceipt materialDiagnosticsReceipt = new MaterialDiagnosticsObserverReceipt();
        private Task materialDiagnosticsObserver = Task.CompletedTask;
        private GameplaySkinLayoutPublication? current;
        private IDisposable? currentRetirement;
        private long admissionGeneration;
        private int disposed;

        public GameplaySkinPackageRevision PackageRevision { get; }

        public GameplaySkinLayoutPublication? CurrentPublication => Volatile.Read(ref current);

        internal bool IsDisposed => Volatile.Read(ref disposed) != 0;

        /// <summary>
        /// The neutral view of <see cref="CurrentPublication"/>. This is derived from the same committed reference and
        /// is not a second publication point.
        /// </summary>
        public GameplaySkinLayoutSnapshot? Current => Volatile.Read(ref current)?.Snapshot;

        /// <summary>
        /// Test/audit evidence for the execution context of the most recent solve callback.
        /// </summary>
        public bool? LastPrepareWasUpdateThread { get; private set; }

        /// <summary>
        /// Test/audit evidence for the execution context in which the prepared reference was actually committed.
        /// </summary>
        public bool? LastCommitWasUpdateThread { get; private set; }

        /// <summary>
        /// Completion receipt for the latest isolated post-commit diagnostic observer.
        /// </summary>
        /// <remarks>
        /// Publication never waits for this task and observer failure can never change commit success. The receipt
        /// allows deterministic audit of an already-queued observer without replacing the production publisher or
        /// relying on an arbitrary wall-clock delay.
        /// </remarks>
        internal Task MaterialDiagnosticsObserver => Volatile.Read(ref materialDiagnosticsObserver);

        /// <summary>
        /// The exact persistence-safe payload most recently handed to the product logger by the observer.
        /// </summary>
        internal string? LastMaterialDiagnosticsBatch => materialDiagnosticsReceipt.LastBatch;

        /// <summary>
        /// Number of persistence-safe product log calls completed by this owner's isolated observer.
        /// </summary>
        /// <remarks>
        /// This owner-local receipt is independent of process-wide logger enablement and listeners, which may be
        /// changed by unrelated tests or host configuration. It is not a second diagnostic publication surface.
        /// </remarks>
        internal int MaterialDiagnosticsLogOperations => materialDiagnosticsReceipt.LogOperations;

        internal GameplaySkinLayoutRevisionOwner(GameplaySkinPackageRevision packageRevision)
            : this(packageRevision, () => true, () => null, () => 0, _ => true, commitCompatibility, runImmediately)
        {
        }

        /// <summary>
        /// Creates an explicitly detached owner for isolated solver/visual tests which do not mount a
        /// <see cref="RulesetSkinProvidingContainer"/>. Production gameplay must resolve the exact owner cached by that
        /// live root instead; compatibility publications are labelled and cannot impersonate a managed package.
        /// </summary>
        public static GameplaySkinLayoutRevisionOwner CreateCompatibility()
            => new GameplaySkinLayoutRevisionOwner(GameplaySkinPackageRevision.CreateCompatibility());

        internal GameplaySkinLayoutRevisionOwner(
            GameplaySkinPackageRevision packageRevision,
            Func<bool> validateRoot,
            Func<SkinCurrentRevisionLease?> acquireWorkLease,
            Func<long?> captureParticipantGeneration,
            Func<long, bool> validateParticipantGeneration,
            Func<long, Action, bool> commitAtParticipantGeneration,
            Func<Action, bool> dispatchCommit)
        {
            PackageRevision = packageRevision ?? throw new ArgumentNullException(nameof(packageRevision));
            this.validateRoot = validateRoot ?? throw new ArgumentNullException(nameof(validateRoot));
            this.acquireWorkLease = acquireWorkLease ?? throw new ArgumentNullException(nameof(acquireWorkLease));
            this.captureParticipantGeneration = captureParticipantGeneration ?? throw new ArgumentNullException(nameof(captureParticipantGeneration));
            this.validateParticipantGeneration = validateParticipantGeneration ?? throw new ArgumentNullException(nameof(validateParticipantGeneration));
            this.commitAtParticipantGeneration = commitAtParticipantGeneration ?? throw new ArgumentNullException(nameof(commitAtParticipantGeneration));
            this.dispatchCommit = dispatchCommit ?? throw new ArgumentNullException(nameof(dispatchCommit));
        }

        /// <summary>
        /// Solves a candidate against the exact next revision without mutating the published reference.
        /// </summary>
        public GameplaySkinPreparedLayout Prepare(Func<long, GameplaySkinLayoutSnapshot> solve)
        {
            ArgumentNullException.ThrowIfNull(solve);

            if (PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
            {
                throw new InvalidOperationException(
                    "An exact gameplay layout preparation must publish a resolved material set through PreparePublication.");
            }

            return PreparePublication(revision => GameplaySkinLayoutPublication.CreateNeutral(solve(revision)));
        }

        /// <summary>
        /// Solves the neutral snapshot and its ruleset-native adapter as one candidate publication.
        /// </summary>
        public GameplaySkinPreparedLayout PreparePublication(Func<long, GameplaySkinLayoutPublication> solve)
        {
            ArgumentNullException.ThrowIfNull(solve);

            GameplaySkinLayoutPublication? expected;
            long revision;
            long generation;
            long participantGeneration;
            SkinCurrentRevisionLease? workLease;

            lock (sync)
            {
                if (IsDisposed || !validateRoot())
                    throw new InvalidOperationException("The gameplay layout root no longer retains its exact package revision.");

                // An exact gameplay root owns one immutable package/layout pair for its entire lifetime. Keep this
                // invariant at the shared owner boundary so a ruleset helper or cached descendant cannot bypass it
                // by invoking Prepare/PreparePublication directly. Detached compatibility owners deliberately retain
                // their multi-publication behaviour for isolated solver and visual tests.
                if (PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility && current != null)
                    throw new InvalidOperationException("An exact gameplay layout root may publish only one immutable layout.");

                expected = current;
                revision = expected == null ? 0 : checked(expected.Snapshot.Context.LayoutRevision + 1);
                generation = checked(++admissionGeneration);
                participantGeneration = captureParticipantGeneration()
                                        ?? throw new InvalidOperationException("The gameplay layout participant barrier is no longer current.");
                workLease = acquireWorkLease();

                if (PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility && workLease == null)
                    throw new InvalidOperationException("An exact gameplay layout preparation requires a fresh package work lease.");
            }

            GameplaySkinLayoutPublication? prepared = null;

            try
            {
                if (ThreadSafety.IsUpdateThread)
                {
                    // A provider can be attached to an already-loaded parent, in which case its background loader is
                    // entered from the update thread. Geometry parsing/solving remains a fallible background phase;
                    // only the prepared reference exchange below is allowed back on the update thread.
                    prepared = Task.Run(() => solveOnCurrentThread(solve, revision)).GetAwaiter().GetResult();
                }
                else
                    prepared = solveOnCurrentThread(solve, revision);

                if (!ReferenceEquals(prepared.Snapshot.Context.PackageRevision, PackageRevision))
                    throw new ArgumentException("A prepared gameplay layout must retain the root's exact package revision.", nameof(solve));

                if (prepared.Snapshot.Context.LayoutRevision != revision)
                    throw new ArgumentException("A prepared gameplay layout must use the owner-issued layout revision.", nameof(solve));

                if (IsDisposed || !validateRoot())
                    throw new InvalidOperationException("The gameplay layout root changed during background preparation.");

                if (!validateParticipantGeneration(participantGeneration))
                    throw new GameplaySkinLayoutParticipantBarrierChangedException();

                return new GameplaySkinPreparedLayout(this, expected, prepared, generation, participantGeneration, workLease);
            }
            catch
            {
                try
                {
                    prepared?.DisposeRetirement();
                }
                finally
                {
                    workLease?.Dispose();
                }

                throw;
            }
        }

        /// <summary>
        /// Solves one candidate while keeping cancellation inside the prepared carrier ownership boundary.
        /// </summary>
        /// <remarks>
        /// Cancellation may become observable after <paramref name="solve"/> has returned and the carrier has taken
        /// both its fresh package work lease and publication retirement. This overload guarantees that such a carrier
        /// is disposed before cancellation escapes to the caller.
        /// </remarks>
        public GameplaySkinPreparedLayout PreparePublication(
            Func<long, GameplaySkinLayoutPublication> solve,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GameplaySkinPreparedLayout? prepared = null;

            try
            {
                prepared = PreparePublication(solve);
                cancellationToken.ThrowIfCancellationRequested();
                return prepared;
            }
            catch
            {
                prepared?.Dispose();
                throw;
            }
        }

        private GameplaySkinLayoutPublication solveOnCurrentThread(
            Func<long, GameplaySkinLayoutPublication> solve,
            long revision)
        {
            LastPrepareWasUpdateThread = ThreadSafety.IsUpdateThread;
            return solve(revision)
                   ?? throw new InvalidOperationException("A gameplay layout solver returned no publication.");
        }

        /// <summary>
        /// Publishes only if no newer prepared layout has won since preparation began.
        /// </summary>
        public bool TryCommit(GameplaySkinPreparedLayout prepared)
        {
            ArgumentNullException.ThrowIfNull(prepared);

            // A prepared carrier is a capability issued by exactly one owner. Rejecting another owner's carrier must
            // not consume it; the issuing update-thread owner may still commit it through its own admission barrier.
            if (!ReferenceEquals(prepared.Owner, this))
                return false;

            int callbackState = 0;
            int committed = 0;
            using var callbackCompleted = new ManualResetEventSlim();

            const int pending = 0;
            const int callback_owned = 1;
            const int cancelled = 2;
            const int completed = 3;

            try
            {
                dispatchCommit(() =>
                {
                    // A dispatcher is untrusted to be perfectly synchronous: it may reject or throw after invoking,
                    // invoke re-entrantly, or return while another thread has already entered this callback. Exactly
                    // one side therefore owns the carrier: this callback or the cancellation path below.
                    if (Interlocked.CompareExchange(ref callbackState, callback_owned, pending) != pending)
                        return;

                    try
                    {
                        LastCommitWasUpdateThread = ThreadSafety.IsUpdateThread;
                        Volatile.Write(ref committed, tryCommitCore(prepared) ? 1 : 0);
                    }
                    catch
                    {
                        prepared.Dispose();
                        Volatile.Write(ref committed, 0);
                    }
                    finally
                    {
                        Volatile.Write(ref callbackState, completed);
                        callbackCompleted.Set();
                    }
                });
            }
            catch
            {
                // Settle through the same ownership state below. A dispatcher which throws after invoking the callback
                // cannot make a successful reference exchange appear to have failed, and cannot outlive this method.
            }

            if (Interlocked.CompareExchange(ref callbackState, cancelled, pending) == pending)
            {
                prepared.Dispose();
                return false;
            }

            if (Volatile.Read(ref callbackState) == cancelled)
                return false;

            while (!callbackCompleted.Wait(TimeSpan.FromSeconds(1)))
            {
                // A callback which has claimed admission is the only remaining owner of this carrier. The
                // synchronous publication contract requires joining it; timing out and returning would re-open the
                // late-commit race this fence exists to close.
            }

            bool succeeded = Volatile.Read(ref committed) != 0;

            if (succeeded)
                queueMaterialDiagnostics(prepared.Publication.MaterialSet);

            return succeeded;
        }

        private bool tryCommitCore(GameplaySkinPreparedLayout prepared)
        {
            ArgumentNullException.ThrowIfNull(prepared);

            if (!prepared.TryConsume(
                    out SkinCurrentRevisionLease? retainedWorkLease,
                    out IDisposable? retainedRetirement))
                return false;

            IDisposable? previousRetirement = null;

            try
            {
                bool committed;

                lock (sync)
                {
                    if (IsDisposed
                        || !validateRoot()
                        || !validateParticipantGeneration(prepared.ParticipantGeneration)
                        || prepared.AdmissionGeneration != admissionGeneration
                        || !ReferenceEquals(current, prepared.Expected))
                    {
                        return false;
                    }

                    // Participant membership/generation and this one reference exchange share the C2 publication
                    // lock. An attach/detach either wins before this admission (rejecting the carrier) or happens
                    // afterwards as a late participant which can observe only the complete committed publication.
                    bool exchanged = false;
                    bool admitted = commitAtParticipantGeneration(prepared.ParticipantGeneration, () =>
                    {
                        // Re-check the exact package/source/content lease while participant membership is still
                        // locked. This prevents an AdoptCurrentRevision-style lease change from entering between the
                        // earlier root check and the reference exchange.
                        if (!validateRoot())
                            return;

                        previousRetirement = currentRetirement;
                        currentRetirement = retainedRetirement;
                        retainedRetirement = null;
                        Volatile.Write(ref current, prepared.Publication);
                        exchanged = true;
                    });
                    committed = admitted && exchanged;
                }

                return committed;
            }
            finally
            {
                try
                {
                    retainedRetirement?.Dispose();
                }
                finally
                {
                    try
                    {
                        previousRetirement?.Dispose();
                    }
                    finally
                    {
                        retainedWorkLease?.Dispose();
                    }
                }
            }
        }

        public void Dispose()
        {
            IDisposable? retirement;

            lock (sync)
            {
                if (Interlocked.Exchange(ref disposed, 1) != 0)
                    return;

                admissionGeneration = checked(admissionGeneration + 1);
                Volatile.Write(ref current, null);
                retirement = currentRetirement;
                currentRetirement = null;
            }

            retirement?.Dispose();
        }

        private void queueMaterialDiagnostics(GameplaySkinResolvedMaterialSet materialSet)
        {
            Task observer;

            try
            {
                string? productBatch = materialSet.PersistenceSafeDiagnosticBatch;

                if (productBatch == null || !emittedMaterialDiagnosticBatches.TryAdd(productBatch, 0))
                    return;

                MaterialDiagnosticsObserverReceipt receipt = materialDiagnosticsReceipt;
                // The closure intentionally contains only immutable persistence-safe text and a lightweight receipt.
                // It cannot retain the material set, its snapshot, package leases or texture-backed entries.
                observer = Task.Run(() => logMaterialDiagnostics(productBatch, receipt));
            }
            catch
            {
                // Task scheduling is an isolated observer too: a scheduler failure cannot change commit success.
                observer = Task.CompletedTask;
            }

            Volatile.Write(ref materialDiagnosticsObserver, observer);
        }

        private static void logMaterialDiagnostics(string productBatch, MaterialDiagnosticsObserverReceipt receipt)
        {
            try
            {
                Logger.Log(productBatch, LoggingTarget.Runtime, LogLevel.Important);
                receipt.Complete(productBatch);
            }
            catch
            {
                // Listener, persistence and scheduler failures are isolated from the committed reference contract.
            }
        }

        private sealed class MaterialDiagnosticsObserverReceipt
        {
            private string? lastBatch;
            private int logOperations;

            public string? LastBatch => Volatile.Read(ref lastBatch);

            public int LogOperations => Volatile.Read(ref logOperations);

            public void Complete(string productBatch)
            {
                Volatile.Write(ref lastBatch, productBatch);
                Interlocked.Increment(ref logOperations);
            }
        }

        private static bool runImmediately(Action commit)
        {
            commit();
            return true;
        }

        private static bool commitCompatibility(long generation, Action commit)
        {
            if (generation != 0)
                return false;

            commit();
            return true;
        }

        public override string ToString() => nameof(GameplaySkinLayoutRevisionOwner);
    }
}
