// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinEventStreamTest
    {
        private static readonly GameplaySkinLaneGroupId group = GameplaySkinLaneGroupId.Create("stage.primary");
        private static readonly GameplaySkinLaneId lane = GameplaySkinLaneId.Create("stage.primary.key1");

        [Test]
        public void TestPublicSurfaceIsReadOnlyAndCannotCreateProducerState()
        {
            Type[] engineConstructedTypes =
            {
                typeof(GameplaySkinInputStateSnapshot),
                typeof(GameplaySkinObjectStateSnapshot),
                typeof(GameplaySkinJudgementStateSnapshot),
                typeof(GameplaySkinScoreStateSnapshot),
                typeof(GameplaySkinTimingStateSnapshot),
                typeof(GameplaySkinBgaStateSnapshot),
                typeof(GameplaySkinEventStateSnapshot),
                typeof(GameplaySkinStateEventPayload),
                typeof(GameplaySkinPublicationEventPayload),
                typeof(GameplaySkinLifecycleEventPayload),
                typeof(GameplaySkinInputEventPayload),
                typeof(GameplaySkinObjectEventPayload),
                typeof(GameplaySkinJudgementEventPayload),
                typeof(GameplaySkinScoreEventPayload),
                typeof(GameplaySkinTimingEventPayload),
                typeof(GameplaySkinBgaEventPayload),
            };
            string[] publicStreamMethods = typeof(GameplaySkinEventStream)
                                           .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                           .Where(method => !method.IsSpecialName)
                                           .Select(method => method.Name)
                                           .ToArray();
            string[] publicHostMutationMethods = typeof(GameplaySkinEventRuntimeHost)
                                                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                                .Where(method => method.Name.StartsWith("Publish", StringComparison.Ordinal)
                                                                 || method.Name is "SetPaused" or "RequestReset")
                                                .Select(method => method.Name)
                                                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(typeof(GameplaySkinEventStream).GetConstructors(), Is.Empty);
                Assert.That(publicStreamMethods, Is.EquivalentTo(new[] { nameof(GameplaySkinEventStream.Subscribe), nameof(IDisposable.Dispose) }));
                Assert.That(publicHostMutationMethods, Is.Empty);
                Assert.That(engineConstructedTypes.SelectMany(type => type.GetConstructors()), Is.Empty);
            });
        }

        [Test]
        public void TestLateAttachReceivesAtomicCurrentSnapshotAndAllFamiliesAreOrdered()
        {
            using var stream = new GameplaySkinEventStream(revision(1), -100, state());
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription first = stream.Subscribe();

            GameplaySkinEventEnvelope attached = dequeue(first);
            Assert.Multiple(() =>
            {
                Assert.That(attached.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                Assert.That(attached.EventKind, Is.EqualTo(GameplaySkinEventKind.StateSnapshot));
                Assert.That(attached.Sequence, Is.Zero);
                Assert.That(attached.Revision, Is.EqualTo(revision(1)));
            });

            publishLifecycle(stream, producer, 0, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            publishInput(stream, producer, 1, input(true));
            publishObject(stream, producer, 2, GameplaySkinEventKind.ObjectSpawned, obj(GameplaySkinObjectState.Visible));
            publishObject(stream, producer, 3, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Holding, progress: 0.25));
            publishJudgement(stream, producer, 4, judgement());
            publishScore(stream, producer, 5, GameplaySkinEventKind.ComboChanged, score(combo: 12));
            publishTiming(stream, producer, 6, GameplaySkinEventKind.TimingBeat, timing(beat: 8));
            publishTiming(stream, producer, 6, GameplaySkinEventKind.TimingScrollChanged, timing(beat: 8, scroll: 2));
            publishTiming(stream, producer, 6, GameplaySkinEventKind.TimingStopStarted, timing(beat: 8, stopped: true, scroll: 2));
            publishTiming(stream, producer, 6, GameplaySkinEventKind.TimingStopEnded, timing(beat: 8, scroll: 2));
            publishBga(stream, producer, 7, GameplaySkinEventKind.BgaViewportChanged, bga(GameplaySkinBgaContentState.Ready));
            publishBga(stream, producer, 7, GameplaySkinEventKind.BgaContentStateChanged, bga(GameplaySkinBgaContentState.Playing));
            publishObject(stream, producer, 8, GameplaySkinEventKind.ObjectDespawned, obj(GameplaySkinObjectState.Despawned, progress: 1));

            GameplaySkinEventEnvelope[] edges = Enumerable.Range(0, 13).Select(_ => dequeue(first)).ToArray();
            Assert.Multiple(() =>
            {
                Assert.That(edges.Select(edge => edge.Sequence), Is.EqualTo(Enumerable.Range(1, 13)));
                Assert.That(edges.Select(edge => edge.EventKind), Is.EqualTo(new[]
                {
                    GameplaySkinEventKind.GameplayStarted,
                    GameplaySkinEventKind.InputPressed,
                    GameplaySkinEventKind.ObjectSpawned,
                    GameplaySkinEventKind.ObjectStateChanged,
                    GameplaySkinEventKind.JudgementApplied,
                    GameplaySkinEventKind.ComboChanged,
                    GameplaySkinEventKind.TimingBeat,
                    GameplaySkinEventKind.TimingScrollChanged,
                    GameplaySkinEventKind.TimingStopStarted,
                    GameplaySkinEventKind.TimingStopEnded,
                    GameplaySkinEventKind.BgaViewportChanged,
                    GameplaySkinEventKind.BgaContentStateChanged,
                    GameplaySkinEventKind.ObjectDespawned,
                }));
                Assert.That(edges[1].GroupId, Is.EqualTo(group));
                Assert.That(edges[1].LaneId, Is.EqualTo(lane));
                Assert.That(edges[1].ContractId, Is.EqualTo(GameplaySkinSceneContracts.EVENT_CONTRACT_ID));
                Assert.That(edges.All(edge => edge.Revision == revision(1)), Is.True);
            });

            using GameplaySkinEventSubscription late = stream.Subscribe();
            GameplaySkinEventEnvelope lateSnapshot = dequeue(late);
            var payload = (GameplaySkinStateEventPayload)lateSnapshot.Payload;

            Assert.Multiple(() =>
            {
                Assert.That(lateSnapshot.Sequence, Is.EqualTo(13));
                Assert.That(lateSnapshot.GameplayTime, Is.EqualTo(8));
                Assert.That(payload.State.LifecycleState, Is.EqualTo(GameplaySkinLifecycleState.Running));
                Assert.That(payload.State.Inputs.Single().IsPressed, Is.True);
                Assert.That(payload.State.ActiveObjects, Is.Empty);
                Assert.That(payload.State.LastJudgement?.Grade, Is.EqualTo(GameplaySkinJudgementGrade.Great));
                Assert.That(payload.State.Score.Combo, Is.EqualTo(12));
                Assert.That(payload.State.Timing.Beat, Is.EqualTo(8));
                Assert.That(payload.State.Timing.IsStopped, Is.False);
                Assert.That(payload.State.Timing.ScrollMultiplier, Is.EqualTo(2));
                Assert.That(payload.State.BgaViewports.Single().ContentState, Is.EqualTo(GameplaySkinBgaContentState.Playing));
            });
        }

        [Test]
        public void TestLateAttachReceivesCurrentFractionalTimingWithoutPerFrameEdge()
        {
            using var stream = new GameplaySkinEventStream(revision(1), 0, state());
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription existing = stream.Subscribe();
            dequeue(existing);

            producer.SynchroniseTiming(250, timing(beat: 0.5, scroll: 0.75));

            Assert.That(existing.PendingCount, Is.Zero, "A fractional timing refresh is snapshot state, not an incremental event edge.");

            using GameplaySkinEventSubscription late = stream.Subscribe();
            GameplaySkinEventEnvelope snapshot = dequeue(late);
            var payload = (GameplaySkinStateEventPayload)snapshot.Payload;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.GameplayTime, Is.EqualTo(250));
                Assert.That(snapshot.Sequence, Is.Zero);
                Assert.That(payload.State.Timing.Beat, Is.EqualTo(0.5));
                Assert.That(payload.State.Timing.ScrollMultiplier, Is.EqualTo(0.75));
            });
        }

        [Test]
        public void TestLateAttachResamplesLongObjectProgressAtLatestAuthoritativeTime()
        {
            var initial = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running,
                new[] { input(false) },
                new[] { obj(GameplaySkinObjectState.Holding, progress: 0) },
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                Array.Empty<GameplaySkinBgaStateSnapshot>());
            using var stream = new GameplaySkinEventStream(revision(1), 100, initial);
            using GameplaySkinEventProducer producer = stream.CreateProducer();

            producer.SynchroniseTiming(300, timing(beat: 0.5));
            using GameplaySkinEventSubscription late = stream.Subscribe();
            GameplaySkinEventStateSnapshot snapshot = ((GameplaySkinStateEventPayload)dequeue(late).Payload).State;

            Assert.That(snapshot.ActiveObjects.Single().Progress, Is.EqualTo(0.5));
        }

        [Test]
        public void TestLateSnapshotRetainsExactJudgementScopesAndExpiresOnlyTransientScopes()
        {
            GameplaySkinObjectStateSnapshot active = obj(GameplaySkinObjectState.Visible);
            var initial = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running,
                new[] { input(false) },
                new[] { active },
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                Array.Empty<GameplaySkinBgaStateSnapshot>());
            using var stream = new GameplaySkinEventStream(revision(1), 0, initial);
            using GameplaySkinEventProducer producer = stream.CreateProducer();

            publishJudgement(stream, producer, 100, judgement());

            using GameplaySkinEventSubscription visible = stream.Subscribe();
            GameplaySkinEventStateSnapshot visibleState = ((GameplaySkinStateEventPayload)dequeue(visible).Payload).State;
            Assert.Multiple(() =>
            {
                Assert.That(visibleState.CurrentJudgements.Select(item => item.Scope), Is.EqualTo(new[]
                {
                    GameplaySkinJudgementScope.Global,
                    GameplaySkinJudgementScope.Group,
                    GameplaySkinJudgementScope.Lane,
                    GameplaySkinJudgementScope.Object,
                }));
                Assert.That(visibleState.CurrentJudgements.All(item => item.AppliedTime == 100), Is.True);
                Assert.That(visibleState.CurrentJudgements.All(item => item.DisplayUntil == 600), Is.True);
                Assert.That(visibleState.LastJudgement?.ObjectId, Is.EqualTo(active.ObjectId));
            });

            producer.SynchroniseTiming(600, timing(beat: 1));
            using GameplaySkinEventSubscription expired = stream.Subscribe();
            GameplaySkinEventStateSnapshot expiredState = ((GameplaySkinStateEventPayload)dequeue(expired).Payload).State;
            Assert.Multiple(() =>
            {
                Assert.That(expiredState.CurrentJudgements.Select(item => item.Scope), Is.EqualTo(new[] { GameplaySkinJudgementScope.Object }));
                Assert.That(expiredState.LastJudgement, Is.Null);
            });

            publishObject(stream, producer, 601, GameplaySkinEventKind.ObjectDespawned, obj(GameplaySkinObjectState.Despawned, progress: 1));
            using GameplaySkinEventSubscription despawned = stream.Subscribe();
            GameplaySkinEventStateSnapshot despawnedState = ((GameplaySkinStateEventPayload)dequeue(despawned).Payload).State;
            Assert.That(despawnedState.CurrentJudgements, Is.Empty);
        }

        [Test]
        public void TestResetInvalidatesOldProducerAndReanchorsCaughtUpAndLaggingConsumers()
        {
            using var stream = new GameplaySkinEventStream(revision(2), 100, state());
            GameplaySkinEventProducer oldProducer = stream.CreateProducer();
            using GameplaySkinEventSubscription caughtUp = stream.Subscribe();
            using GameplaySkinEventSubscription lagging = stream.Subscribe();
            dequeue(caughtUp);

            publishLifecycle(stream, oldProducer, 101, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            dequeue(caughtUp);

            GameplaySkinEventStateSnapshot replacement = state(GameplaySkinLifecycleState.Paused, input(true));
            GameplaySkinEventProducer currentProducer = oldProducer.Reset(-20, replacement, GameplaySkinEventResetReason.Seek);

            Assert.That(
                () => publishLifecycle(stream, oldProducer, -20, GameplaySkinEventKind.GameplayResumed, GameplaySkinLifecycleState.Running),
                Throws.InvalidOperationException);

            GameplaySkinEventEnvelope caughtReset = dequeue(caughtUp);
            GameplaySkinEventEnvelope laggingSnapshot = dequeue(lagging);

            Assert.Multiple(() =>
            {
                Assert.That(caughtReset.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));
                Assert.That(caughtReset.EventKind, Is.EqualTo(GameplaySkinEventKind.StateReset));
                Assert.That(caughtReset.Epoch, Is.EqualTo(1));
                Assert.That(caughtReset.Sequence, Is.Zero);
                Assert.That(caughtReset.GameplayTime, Is.EqualTo(-20));
                Assert.That(((GameplaySkinStateEventPayload)caughtReset.Payload).ResetReason, Is.EqualTo(GameplaySkinEventResetReason.Seek));
                Assert.That(laggingSnapshot.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                Assert.That(laggingSnapshot.Epoch, Is.EqualTo(1));
                Assert.That(lagging.PendingCount, Is.Zero);
            });

            publishLifecycle(stream, currentProducer, -20, GameplaySkinEventKind.GameplayResumed, GameplaySkinLifecycleState.Running);
            Assert.That(dequeue(caughtUp).Sequence, Is.EqualTo(1));
            Assert.That(dequeue(lagging).Sequence, Is.EqualTo(1));
            currentProducer.Dispose();
            oldProducer.Dispose();
        }

        [Test]
        public void TestCurrentProductionPublicationIsAnnouncedOnceFromPreparedSnapshot()
        {
            GameplaySkinEventRevision exact = GameplaySkinEventRevision.Create(2, 3, 4, 5);
            using var stream = new GameplaySkinEventStream(exact, -50, state());
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            GameplaySkinEventEnvelope preparedSnapshot = dequeue(subscription);

            producer = producer.AnnounceCurrentPublication(0, state(GameplaySkinLifecycleState.Running));
            GameplaySkinEventEnvelope committed = dequeue(subscription);

            Assert.Multiple(() =>
            {
                Assert.That(preparedSnapshot.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                Assert.That(preparedSnapshot.Epoch, Is.Zero);
                Assert.That(committed.EventKind, Is.EqualTo(GameplaySkinEventKind.LayoutPublicationCommitted));
                Assert.That(committed.DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));
                Assert.That(committed.Epoch, Is.EqualTo(1));
                Assert.That(committed.Sequence, Is.Zero);
                Assert.That(committed.Revision, Is.EqualTo(exact));
                Assert.That(() => producer.AnnounceCurrentPublication(0, state()), Throws.InvalidOperationException);
            });

            producer.Dispose();
        }

        [Test]
        public void TestBackpressureFailsAtomicallyWithoutSilentDrop()
        {
            using var stream = new GameplaySkinEventStream(revision(1), 0, state(), pendingEventBudget: 1);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();

            Assert.That(
                () => publishLifecycle(stream, producer, 1, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running),
                Throws.TypeOf<GameplaySkinEventBackpressureException>());

            GameplaySkinEventEnvelope initial = dequeue(subscription);
            Assert.That(initial.Sequence, Is.Zero);

            publishLifecycle(stream, producer, 1, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            GameplaySkinEventEnvelope edge = dequeue(subscription);
            Assert.Multiple(() =>
            {
                Assert.That(edge.Sequence, Is.EqualTo(1));
                Assert.That(edge.EventKind, Is.EqualTo(GameplaySkinEventKind.GameplayStarted));
            });
        }

        [Test]
        public void TestPublicDrainIsCappedByPerFrameBudget()
        {
            using var stream = new GameplaySkinEventStream(revision(1), 0, state());
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            var consumed = new List<GameplaySkinEventEnvelope>();

            Assert.Multiple(() =>
            {
                Assert.That(subscription.DrainFrame(consumed.Add, 1), Is.EqualTo(1));
                Assert.That(consumed.Single().DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Snapshot));
                Assert.That(subscription.DrainFrame(consumed.Add, 1), Is.Zero);
                Assert.That(() => subscription.DrainFrame(consumed.Add, 0), Throws.TypeOf<ArgumentOutOfRangeException>());
                Assert.That(
                    () => subscription.DrainFrame(consumed.Add, GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME + 1),
                    Throws.TypeOf<ArgumentOutOfRangeException>());
            });
        }

        [Test]
        public void TestCompleteStateDefensivelyCopiesAndSortsCollections()
        {
            GameplaySkinInputStateSnapshot firstInput = input(false);
            GameplaySkinLaneGroupId earlierGroup = GameplaySkinLaneGroupId.Create("stage.earlier");
            GameplaySkinLaneId earlierLane = GameplaySkinLaneId.Create("stage.earlier.key1");
            var earlierInput = new GameplaySkinInputStateSnapshot(earlierGroup, earlierLane, false, 0);
            var mutableInputs = new List<GameplaySkinInputStateSnapshot> { firstInput, earlierInput };
            var mutableObjects = new List<GameplaySkinObjectStateSnapshot>
            {
                obj(GameplaySkinObjectState.Visible, objectId: 9),
                obj(GameplaySkinObjectState.Visible, objectId: 2),
            };
            var mutableBga = new List<GameplaySkinBgaStateSnapshot>
            {
                new GameplaySkinBgaStateSnapshot(3, GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinBgaContentState.Ready, 1),
                new GameplaySkinBgaStateSnapshot(1, GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinBgaContentState.Ready, 1),
            };
            var snapshot = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Loaded,
                mutableInputs,
                mutableObjects,
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                mutableBga);

            mutableInputs.Clear();
            mutableObjects.Clear();
            mutableBga.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Inputs.Select(state => state.LaneId), Is.EqualTo(new[] { earlierLane, lane }));
                Assert.That(snapshot.ActiveObjects.Select(state => state.ObjectId), Is.EqualTo(new long[] { 2, 9 }));
                Assert.That(snapshot.BgaViewports.Select(state => state.ViewportIndex), Is.EqualTo(new[] { 1, 3 }));
                Assert.That(() => ((IList<GameplaySkinInputStateSnapshot>)snapshot.Inputs).Add(input(true)), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinObjectStateSnapshot>)snapshot.ActiveObjects).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestDenseFourteenThousandObjectStateDoesNotCloneGraphPerEdge()
        {
            using var stream = new GameplaySkinEventStream(revision(1), 0, state());
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription draining = stream.Subscribe();
            dequeue(draining);

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 14000; i++)
            {
                publishObject(stream, producer, i, GameplaySkinEventKind.ObjectSpawned, obj(GameplaySkinObjectState.Visible, objectId: i));

                if ((i + 1) % 1024 == 0)
                    draining.DrainFrame(_ => { });
            }

            draining.DrainFrame(_ => { }, GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            using GameplaySkinEventSubscription late = stream.Subscribe();
            var snapshot = (GameplaySkinStateEventPayload)dequeue(late).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.State.ActiveObjects, Has.Count.EqualTo(14000));
                Assert.That(snapshot.State.ActiveObjects.First().ObjectId, Is.Zero);
                Assert.That(snapshot.State.ActiveObjects.Last().ObjectId, Is.EqualTo(13999));
                Assert.That(allocated, Is.LessThan(128L * 1024 * 1024), "Dense publication must not clone the active-object graph for every edge.");
            });
        }

        [Test]
        public void TestProductionDigitalInputAndObjectEdgesAreAllocationFreeAfterWarmup()
        {
            var initial = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running,
                new[] { input(true) },
                new[] { obj(GameplaySkinObjectState.Visible, progress: 0.1) },
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                Array.Empty<GameplaySkinBgaStateSnapshot>());
            using var stream = new GameplaySkinEventStream(revision(1), 0, initial);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            Action<GameplaySkinEventRecord> consume = static _ => { };

            Assert.That(subscription.DrainProductionFrame(consume, 1), Is.EqualTo(1));
            publishInput(stream, producer, 1, input(false));
            publishObject(stream, producer, 1, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Holding, progress: 0.5));
            Assert.That(subscription.DrainProductionFrame(consume, 2), Is.EqualTo(2));

            // Cross both tiered-JIT and dynamic-PGO promotion thresholds before measuring the steady production path.
            // The event value constructors are deliberately defensive and are called at several production
            // validation points; a shorter warmup can let the runtime publish a new code version during the
            // allocation window when this fixture follows the dense snapshot tests.
            for (int i = 0; i < 16384; i++)
            {
                double time = i + 2;
                publishInput(stream, producer, time, input((i & 1) == 0));
                publishObject(stream, producer, time, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Holding, progress: 0.5));
                subscription.DrainProductionFrame(consume, 2);
            }

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 8192; i++)
            {
                double time = i + 16386;
                publishInput(stream, producer, time, input((i & 1) == 0));
                publishObject(stream, producer, time, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Holding, progress: 0.5));
                subscription.DrainProductionFrame(consume, 2);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(allocated, Is.Zero,
                "The production record queue must not materialise envelopes, payloads, closures or state objects for steady input/object edges.");
        }

        [Test]
        public void TestProductionRecordBackpressureFailsAtomicallyAndKeepsSequenceContiguous()
        {
            using var stream = new GameplaySkinEventStream(revision(1), 0, state(), pendingEventBudget: 1);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            var consumed = new List<GameplaySkinEventRecord>();
            Action<GameplaySkinEventRecord> consume = consumed.Add;

            Assert.That(subscription.DrainProductionFrame(consume, 1), Is.EqualTo(1));
            publishLifecycle(stream, producer, 1, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            Assert.That(
                () => publishLifecycle(stream, producer, 2, GameplaySkinEventKind.GameplayPaused, GameplaySkinLifecycleState.Paused),
                Throws.TypeOf<GameplaySkinEventBackpressureException>());

            Assert.That(subscription.DrainProductionFrame(consume, 1), Is.EqualTo(1));
            publishLifecycle(stream, producer, 2, GameplaySkinEventKind.GameplayPaused, GameplaySkinLifecycleState.Paused);
            Assert.That(subscription.DrainProductionFrame(consume, 1), Is.EqualTo(1));

            Assert.Multiple(() =>
            {
                Assert.That(consumed.Select(record => record.Sequence), Is.EqualTo(new long[] { 0, 1, 2 }));
                Assert.That(consumed.Select(record => record.EventKind), Is.EqualTo(new[]
                {
                    GameplaySkinEventKind.StateSnapshot,
                    GameplaySkinEventKind.GameplayStarted,
                    GameplaySkinEventKind.GameplayPaused,
                }));
            });
        }

        [Test]
        public void TestActiveObjectBudgetRejectsOverflowBeforeStateOrSequenceMutation()
        {
            using var stream = new GameplaySkinEventStream(revision(1), 0, state());
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            Action<GameplaySkinEventRecord> consume = static _ => { };
            subscription.DrainProductionFrame(consume, 1);

            for (int objectId = 0; objectId < GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS; objectId++)
            {
                publishObject(
                    stream,
                    producer,
                    objectId,
                    GameplaySkinEventKind.ObjectSpawned,
                    obj(GameplaySkinObjectState.Visible, objectId));
                subscription.DrainProductionFrame(consume, 1);
            }

            Assert.That(
                () => publishObject(
                    stream,
                    producer,
                    GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS,
                    GameplaySkinEventKind.ObjectSpawned,
                    obj(GameplaySkinObjectState.Visible, GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS)),
                Throws.InvalidOperationException);

            using GameplaySkinEventSubscription late = stream.Subscribe();
            GameplaySkinEventEnvelope snapshot = dequeue(late);
            var payload = (GameplaySkinStateEventPayload)snapshot.Payload;

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.Sequence, Is.EqualTo(GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS));
                Assert.That(payload.State.ActiveObjects, Has.Count.EqualTo(GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS));
                Assert.That(payload.State.ActiveObjects[^1].ObjectId, Is.EqualTo(GameplaySkinEventBudgets.MAX_ACTIVE_OBJECTS - 1));
            });
        }

        [Test]
        public void TestStableTargetAndRevisionViolationsFailWithoutAdvancing()
        {
            GameplaySkinObjectStateSnapshot active = obj(GameplaySkinObjectState.Visible);
            var initial = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Loaded,
                new[] { input(false) },
                new[] { active },
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                new[] { bga(GameplaySkinBgaContentState.Ready) });
            using var stream = new GameplaySkinEventStream(revision(1), 0, initial);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            dequeue(subscription);

            GameplaySkinLaneGroupId otherGroup = GameplaySkinLaneGroupId.Create("stage.secondary");
            GameplaySkinLaneId otherLane = GameplaySkinLaneId.Create("stage.secondary.key1");

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => publishInput(stream, producer, 1, new GameplaySkinInputStateSnapshot(otherGroup, lane, true, 1)),
                    Throws.InvalidOperationException);
                Assert.That(
                    () => publishObject(
                        stream,
                        producer,
                        1,
                        GameplaySkinEventKind.ObjectStateChanged,
                        new GameplaySkinObjectStateSnapshot(
                            active.ObjectId,
                            active.Kind,
                            GameplaySkinObjectState.Holding,
                            otherGroup,
                            otherLane,
                            active.StartTime,
                            active.EndTime,
                            0.5)),
                    Throws.InvalidOperationException);
                Assert.That(
                    () => publishJudgement(
                        stream,
                        producer,
                        1,
                        new GameplaySkinJudgementStateSnapshot(
                            active.ObjectId,
                            otherGroup,
                            otherLane,
                            GameplaySkinJudgementGrade.Great,
                            0,
                            0)),
                    Throws.InvalidOperationException);
                Assert.That(
                    () => publishBga(
                        stream,
                        producer,
                        1,
                        GameplaySkinEventKind.BgaContentStateChanged,
                        new GameplaySkinBgaStateSnapshot(0, GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinBgaContentState.Playing, 3)),
                    Throws.InvalidOperationException);
            });

            publishInput(stream, producer, 1, input(true));
            Assert.That(dequeue(subscription).Sequence, Is.EqualTo(1));
        }

        [Test]
        public void TestNewEpochAllowsExactlyOneObjectAndBgaResynchronisation()
        {
            GameplaySkinObjectStateSnapshot active = obj(GameplaySkinObjectState.Holding, progress: 0.8);
            var replacement = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running,
                new[] { input(true) },
                new[] { active },
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                new[] { new GameplaySkinBgaStateSnapshot(0, GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinBgaContentState.Playing, 9) });
            using var stream = new GameplaySkinEventStream(revision(1), 100, replacement);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            dequeue(subscription);

            producer = producer.Reset(-20, replacement, GameplaySkinEventResetReason.Rewind);
            Assert.That(dequeue(subscription).DeliveryKind, Is.EqualTo(GameplaySkinEventDeliveryKind.Reset));

            publishObject(stream, producer, -20, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Visible, progress: 0.2));
            publishBga(
                stream,
                producer,
                -20,
                GameplaySkinEventKind.BgaContentStateChanged,
                new GameplaySkinBgaStateSnapshot(0, GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinBgaContentState.Ready, 2));

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => publishObject(stream, producer, -20, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Visible, progress: 0.1)),
                    Throws.InvalidOperationException);
                Assert.That(
                    () => publishBga(
                        stream,
                        producer,
                        -20,
                        GameplaySkinEventKind.BgaContentStateChanged,
                        new GameplaySkinBgaStateSnapshot(0, GameplaySkinLayoutRect.Create(0, 0, 1, 1), GameplaySkinBgaContentState.Ready, 1)),
                    Throws.InvalidOperationException);
            });

            publishObject(stream, producer, -20, GameplaySkinEventKind.ObjectStateChanged, obj(GameplaySkinObjectState.Holding, progress: 0.3));
            Assert.That(Enumerable.Range(0, 3).Select(_ => dequeue(subscription).Sequence), Is.EqualTo(new long[] { 1, 2, 3 }));
            producer.Dispose();
        }

        [Test]
        public void TestDetachedSubscriptionAndDisposedStreamCannotLeakEvents()
        {
            var stream = new GameplaySkinEventStream(revision(1), 0, state());
            GameplaySkinEventProducer producer = stream.CreateProducer();
            GameplaySkinEventSubscription subscription = stream.Subscribe();
            subscription.Dispose();

            publishLifecycle(stream, producer, 1, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            Assert.That(subscription.TryDequeue(out _), Is.False);

            stream.Dispose();
            Assert.Multiple(() =>
            {
                Assert.That(() => stream.Subscribe(), Throws.TypeOf<ObjectDisposedException>());
                Assert.That(
                    () => publishLifecycle(stream, producer, 2, GameplaySkinEventKind.GameplayPaused, GameplaySkinLifecycleState.Paused),
                    Throws.TypeOf<ObjectDisposedException>());
            });
        }

        private static GameplaySkinEventEnvelope dequeue(GameplaySkinEventSubscription subscription)
        {
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return envelope!;
        }

        private static void publishLifecycle(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinEventKind eventKind,
            GameplaySkinLifecycleState state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Lifecycle(eventKind, state), null, null);

        private static void publishInput(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinInputStateSnapshot state)
            => stream.Publish(
                producer,
                gameplayTime,
                GameplaySkinEventValue.Input(state.IsPressed ? GameplaySkinEventKind.InputPressed : GameplaySkinEventKind.InputReleased, state),
                state.GroupId,
                state.LaneId);

        private static void publishObject(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinEventKind eventKind,
            GameplaySkinObjectStateSnapshot state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Object(eventKind, state), state.GroupId, state.LaneId);

        private static void publishJudgement(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinJudgementStateSnapshot state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Judgement(state), state.GroupId, state.LaneId);

        private static void publishScore(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinEventKind eventKind,
            GameplaySkinScoreStateSnapshot state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Score(eventKind, state), null, null);

        private static void publishTiming(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinEventKind eventKind,
            GameplaySkinTimingStateSnapshot state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Timing(eventKind, state), null, null);

        private static void publishBga(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinEventKind eventKind,
            GameplaySkinBgaStateSnapshot state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Bga(eventKind, state), null, null);

        private static GameplaySkinEventRevision revision(long value)
            => GameplaySkinEventRevision.Create(value, value, value, value);

        private static GameplaySkinEventStateSnapshot state(
            GameplaySkinLifecycleState lifecycle = GameplaySkinLifecycleState.Loaded,
            params GameplaySkinInputStateSnapshot[] inputs)
            => new GameplaySkinEventStateSnapshot(
                lifecycle,
                inputs,
                Array.Empty<GameplaySkinObjectStateSnapshot>(),
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(),
                timing(),
                Array.Empty<GameplaySkinBgaStateSnapshot>());

        private static GameplaySkinInputStateSnapshot input(bool pressed, float strength = 1)
            => new GameplaySkinInputStateSnapshot(group, lane, pressed, pressed ? strength : 0);

        private static GameplaySkinObjectStateSnapshot obj(GameplaySkinObjectState objectState, long objectId = 7, double progress = 0)
            => new GameplaySkinObjectStateSnapshot(
                objectId,
                GameplaySkinObjectKind.LongNote,
                objectState,
                group,
                lane,
                100,
                500,
                progress);

        private static GameplaySkinJudgementStateSnapshot judgement()
            => new GameplaySkinJudgementStateSnapshot(7, group, lane, GameplaySkinJudgementGrade.Great, -4.5, 0.02);

        private static GameplaySkinScoreStateSnapshot score(int combo = 0)
            => new GameplaySkinScoreStateSnapshot(1000, combo, combo, 0.98, 0.75);

        private static GameplaySkinTimingStateSnapshot timing(double beat = 0, bool stopped = false, double scroll = 1)
            => new GameplaySkinTimingStateSnapshot(beat, 0, 120, stopped, scroll);

        private static GameplaySkinBgaStateSnapshot bga(GameplaySkinBgaContentState contentState)
            => new GameplaySkinBgaStateSnapshot(0, GameplaySkinLayoutRect.Create(0, 0, 1, 1), contentState, 4);
    }
}
