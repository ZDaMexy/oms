// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinJudgementTimingTransportTest
    {
        private static readonly GameplaySkinLaneGroupId firstGroup = GameplaySkinLaneGroupId.Create("timing.first");
        private static readonly GameplaySkinLaneGroupId secondGroup = GameplaySkinLaneGroupId.Create("timing.second");
        private static readonly GameplaySkinLaneId firstLane = GameplaySkinLaneId.Create("timing.first.key");
        private static readonly GameplaySkinLaneId secondLane = GameplaySkinLaneId.Create("timing.second.scratch");
        private static readonly GameplaySkinEventRevision revision = GameplaySkinEventRevision.Create(1, 1, 1, 1);

        [Test]
        public void TestAllNullableChannelAndMarkerCombinationsRoundTripWithoutGrowingTheUnion()
        {
            GameplaySkinJudgementTimingDirection[] directions =
            {
                GameplaySkinJudgementTimingDirection.None, GameplaySkinJudgementTimingDirection.Fast, GameplaySkinJudgementTimingDirection.Slow,
            };
            foreach (bool marker in new[] { false, true })
            foreach (GameplaySkinJudgementTimingDirection combined in directions)
            foreach (GameplaySkinJudgementTimingDirection keys in directions)
            foreach (GameplaySkinJudgementTimingDirection scratch in directions)
            {
                GameplaySkinJudgementTimingStateSnapshot channels = GameplaySkinJudgementTimingStateSnapshot.Restore(
                    hint(combined, -123.125), hint(keys, -0.0), hint(scratch, 12.000000000000002));
                var original = new GameplaySkinJudgementStateSnapshot(9007199254740993, firstGroup, firstLane,
                    GameplaySkinJudgementGrade.Good, -17.125, 0.03125, marker, channels);
                GameplaySkinEventValue compact = GameplaySkinEventValue.Judgement(original);
                GameplaySkinJudgementStateSnapshot restored = compact.GetJudgement(firstGroup, firstLane);
                GameplaySkinJudgementStateSnapshot materialized = ((GameplaySkinJudgementEventPayload)compact.Materialize(firstGroup, firstLane)).State;
                Assert.That(restored, Is.EqualTo(original));
                Assert.That(materialized, Is.EqualTo(original));
                assertHintBits(restored.JudgementTimingState.Combined, channels.Combined);
                assertHintBits(restored.JudgementTimingState.Keys, channels.Keys);
                assertHintBits(restored.JudgementTimingState.Scratch, channels.Scratch);
            }
            Assert.That(typeof(GameplaySkinEventValue).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                .Any(field => field.FieldType == typeof(GameplaySkinJudgementTimingStateSnapshot)
                              || field.FieldType == typeof(GameplaySkinJudgementTimingHint)), Is.False,
                "Judgement channels reuse existing scalar/double union slots instead of adding a timing struct to every record.");
        }

        [Test]
        public void TestTimingGroupsAreCopiedAndRejectDuplicatesUnknownGroupsAndFutureApplications()
        {
            GameplaySkinJudgementTimingStateSnapshot timing = fast(100);
            GameplaySkinJudgementTimingGroupStateSnapshot[] entries = { new(firstGroup, timing) };
            GameplaySkinEventStateSnapshot captured = state(entries);
            entries[0] = new GameplaySkinJudgementTimingGroupStateSnapshot(secondGroup, default);
            Assert.That(captured.JudgementTimingGroups.Single().GroupId, Is.EqualTo(firstGroup));
            Assert.That(captured.JudgementTimingGroups.Single().State, Is.EqualTo(timing));
            Assert.Throws<ArgumentException>(() => state(new GameplaySkinJudgementTimingGroupStateSnapshot(firstGroup, timing),
                new GameplaySkinJudgementTimingGroupStateSnapshot(firstGroup, timing)));
            Assert.Throws<ArgumentException>(() => state(new GameplaySkinJudgementTimingGroupStateSnapshot(
                GameplaySkinLaneGroupId.Create("timing.unknown"), timing)));
            Assert.Throws<ArgumentException>(() => captured.ValidateForGameplayTime(99));
            Assert.DoesNotThrow(() => captured.ValidateForGameplayTime(100));
            Assert.Throws<ArgumentException>(() => GameplaySkinEventRecord.Create(0, 1, 99, revision, clock(), firstGroup, firstLane,
                GameplaySkinEventValue.Judgement(judgement(firstGroup, firstLane, timing))));
        }

        [Test]
        public void TestLateAttachRetainsTimingGroupsAfterTheTransientGradeExpires()
        {
            using var stream = new GameplaySkinEventStream(revision, 0, state());
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            GameplaySkinJudgementTimingStateSnapshot timing = fast(100).Apply(GameplaySkinLaneRole.Scratch,
                new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Slow, 101));
            publish(stream, producer, 101, firstGroup, firstLane, timing);
            producer.SynchroniseTiming(1000, clock());
            GameplaySkinEventStateSnapshot late = current(stream);
            Assert.That(late.CurrentJudgements, Is.Empty, "The 500ms current grade display has expired.");
            Assert.That(group(late, firstGroup), Is.EqualTo(timing), "Timing origins are separate from the transient grade collection.");
            Assert.That(group(late, secondGroup), Is.EqualTo(default(GameplaySkinJudgementTimingStateSnapshot)));
        }

        [Test]
        public void TestStreamReplacesCompleteChannelsAndNeverInfersThemFromOffsetOrGrade()
        {
            using var stream = new GameplaySkinEventStream(revision, 0, state());
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            GameplaySkinJudgementTimingStateSnapshot original = fast(1);
            publish(stream, producer, 1, firstGroup, firstLane, original);
            stream.Publish(producer, 2, GameplaySkinEventValue.Judgement(new GameplaySkinJudgementStateSnapshot(null, firstGroup, firstLane,
                GameplaySkinJudgementGrade.Perfect, 999, 0, hasPlayerTiming: false, judgementTimingState: original)), firstGroup, firstLane);
            Assert.That(group(current(stream), firstGroup), Is.EqualTo(original));
            publish(stream, producer, 3, secondGroup, secondLane, fast(3));
            Assert.That(group(current(stream), firstGroup), Is.EqualTo(original), "One group's result cannot overwrite another group.");
            stream.Publish(producer, 4, GameplaySkinEventValue.Judgement(new GameplaySkinJudgementStateSnapshot(null, firstGroup, firstLane,
                GameplaySkinJudgementGrade.Great, -50, 0, hasPlayerTiming: true)), firstGroup, firstLane);
            Assert.That(group(current(stream), firstGroup), Is.EqualTo(default(GameplaySkinJudgementTimingStateSnapshot)),
                "An explicitly empty authoritative state clears the group; the stream does not recreate FAST from -50.");
        }

        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt)]
        [TestCase(GameplaySkinEventResetReason.CapacityExceeded)]
        public void TestRebuildResetPreservesTheLatestAuthoritativeChannels(GameplaySkinEventResetReason reason)
        {
            using var stream = new GameplaySkinEventStream(revision, 0, state());
            GameplaySkinEventProducer producer = stream.CreateProducer();
            GameplaySkinJudgementTimingStateSnapshot timing = fast(10);
            publish(stream, producer, 10, firstGroup, firstLane, timing);
            producer.SynchroniseTiming(1000, clock());
            GameplaySkinEventStateSnapshot replacement = current(stream);
            Assert.That(replacement.CurrentJudgements, Is.Empty, "The complete replacement retires grades at its actual gameplay time.");
            Assert.That(group(replacement, firstGroup), Is.EqualTo(timing), "Grade retirement does not remove independently retained timing channels.");
            producer = producer.Reset(1000, replacement, reason);
            Assert.That(group(current(stream), firstGroup), Is.EqualTo(timing));
            producer.Dispose();
        }

        [Test]
        public void TestPublicationReplacementRetainsTimingChannelsWithoutARecentGrade()
        {
            GameplaySkinJudgementTimingStateSnapshot timing = fast(-10);
            GameplaySkinEventStateSnapshot initial = state(new GameplaySkinJudgementTimingGroupStateSnapshot(firstGroup, timing));
            using var stream = new GameplaySkinEventStream(revision, 0, initial);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            producer = producer.AnnounceCurrentPublication(1000, initial);
            Assert.That(group(current(stream), firstGroup), Is.EqualTo(timing));
            producer.Dispose();
        }

        [Test]
        public void TestBackpressureIsAtomicAndTheReplacementCarriesTheLatestProducerFold()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(texture, false);
            using GameplaySkinEventRuntimeHost host = createHost(publication, 100);
            using GameplaySkinEventSubscription blocked = host.EventStream.Subscribe();
            GameplaySkinInputStateSnapshot target = publication.PreparedScene.InitialEventState.Inputs.First();
            for (int i = 1; i < GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION; i++)
                host.PublishJudgement(null, target.GroupId, target.LaneId, GameplaySkinJudgementGrade.Great, -5, 0, hasPlayerTiming: true);
            host.PublishJudgement(null, target.GroupId, target.LaneId, GameplaySkinJudgementGrade.Good, 7, 0, hasPlayerTiming: true);
            Assert.That(group(current(host.EventStream), target.GroupId).Combined?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Fast),
                "A full queue rejects the edge before changing the stream's folded state.");
            GameplaySkinEventStateSnapshot replacement = producerSnapshot(host, 100);
            Assert.That(group(replacement, target.GroupId).Combined?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow),
                "The producer retains the actual last judgement even when its edge could not be queued.");
            GameplaySkinEventProducer producer = (GameplaySkinEventProducer)field("producer").GetValue(host)!;
            producer = producer.Reset(100, replacement, GameplaySkinEventResetReason.ConsumerRebuilt);
            field("producer").SetValue(host, producer);
            Assert.That(blocked.PendingCount, Is.EqualTo(1));
            Assert.That(group(current(host.EventStream), target.GroupId).Combined?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow));
        }

        [TestCase(GameplaySkinEventResetReason.Seek, false)]
        [TestCase(GameplaySkinEventResetReason.Rewind, false)]
        [TestCase(GameplaySkinEventResetReason.Retry, false)]
        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt, true)]
        public void TestProducerClearsOnlyDiscontinuousEpochs(GameplaySkinEventResetReason reason, bool retain)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = GameplaySkinSceneRuntimeHostTest.CreateEssentialInformationPublication(texture, false);
            using GameplaySkinEventRuntimeHost host = createHost(publication, 100);
            GameplaySkinInputStateSnapshot target = publication.PreparedScene.InitialEventState.Inputs.First();
            host.PublishJudgement(null, target.GroupId, target.LaneId, GameplaySkinJudgementGrade.Great, -5, 0, hasPlayerTiming: true);
            host.RequestReset(reason);
            Assert.That(group(producerSnapshot(host, 100), target.GroupId).Combined.HasValue, Is.EqualTo(retain));
        }

        [Test]
        public void TestOmsQualificationExcludesAutomaticPerfectMissAndNearZeroResults()
        {
            GameplaySkinJudgementTimingStateSnapshot previous = fast(10);
            foreach (GameplaySkinJudgementGrade grade in new[]
                     {
                         GameplaySkinJudgementGrade.Great, GameplaySkinJudgementGrade.Good, GameplaySkinJudgementGrade.Ok, GameplaySkinJudgementGrade.Meh,
                     })
            {
                Assert.That(GameplaySkinEventRuntimeHost.FoldJudgementTiming(previous, GameplaySkinLaneRole.Scratch, true, false, grade, 5, 20)
                    .Scratch?.Direction, Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow));
                Assert.That(GameplaySkinEventRuntimeHost.FoldJudgementTiming(previous, GameplaySkinLaneRole.Key, false, false, grade, 5, 20), Is.EqualTo(previous));
                Assert.That(GameplaySkinEventRuntimeHost.FoldJudgementTiming(previous, GameplaySkinLaneRole.Key, true, true, grade, 5, 20), Is.EqualTo(previous));
            }
            foreach (GameplaySkinJudgementGrade grade in new[] { GameplaySkinJudgementGrade.Perfect, GameplaySkinJudgementGrade.Miss })
                Assert.That(GameplaySkinEventRuntimeHost.FoldJudgementTiming(previous, GameplaySkinLaneRole.Key, true, false, grade, -40, 20), Is.EqualTo(previous));
            foreach (double offset in new[] { 0, 1e-12, -1e-12 })
                Assert.That(GameplaySkinEventRuntimeHost.FoldJudgementTiming(previous, GameplaySkinLaneRole.Key, true, false,
                    GameplaySkinJudgementGrade.Great, offset, 20), Is.EqualTo(previous));
            Assert.That(GameplaySkinEventRuntimeHost.QualifyJudgementTiming(true, false, GameplaySkinJudgementGrade.Great, -1e-6),
                Is.EqualTo(GameplaySkinJudgementTimingDirection.Fast));
            Assert.That(GameplaySkinEventRuntimeHost.QualifyJudgementTiming(true, false, GameplaySkinJudgementGrade.Great, 1e-6),
                Is.EqualTo(GameplaySkinJudgementTimingDirection.Slow));
        }

        private static GameplaySkinEventRuntimeHost createHost(GameplaySkinLayoutPublication publication, double time)
        {
            var host = new GameplaySkinEventRuntimeHost(publication, new Beatmap());
            field("pendingDiscontinuityGameplayTime").SetValue(host, (double?)time);
            field("producer").SetValue(host, host.EventStream.CreateProducer());
            return host;
        }

        private static FieldInfo field(string name)
            => typeof(GameplaySkinEventRuntimeHost).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;

        private static GameplaySkinEventStateSnapshot producerSnapshot(GameplaySkinEventRuntimeHost host, double time)
            => (GameplaySkinEventStateSnapshot)typeof(GameplaySkinEventRuntimeHost).GetMethod("buildSnapshot", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(host, new object[] { time })!;

        private static GameplaySkinJudgementTimingHint? hint(GameplaySkinJudgementTimingDirection direction, double time)
            => direction == GameplaySkinJudgementTimingDirection.None ? null : new GameplaySkinJudgementTimingHint(direction, time);

        private static void assertHintBits(GameplaySkinJudgementTimingHint? actual, GameplaySkinJudgementTimingHint? expected)
        {
            Assert.That(actual?.Direction, Is.EqualTo(expected?.Direction));
            if (expected.HasValue)
                Assert.That(BitConverter.DoubleToInt64Bits(actual!.Value.AppliedTime), Is.EqualTo(BitConverter.DoubleToInt64Bits(expected.Value.AppliedTime)));
        }

        private static GameplaySkinJudgementTimingStateSnapshot fast(double time)
            => default(GameplaySkinJudgementTimingStateSnapshot).Apply(GameplaySkinLaneRole.Key,
                new GameplaySkinJudgementTimingHint(GameplaySkinJudgementTimingDirection.Fast, time));

        private static GameplaySkinJudgementStateSnapshot judgement(GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId,
                                                                  GameplaySkinJudgementTimingStateSnapshot channels)
            => new(null, groupId, laneId, GameplaySkinJudgementGrade.Great, -5, 0, hasPlayerTiming: true, judgementTimingState: channels);

        private static void publish(GameplaySkinEventStream stream, GameplaySkinEventProducer producer, double time,
                                    GameplaySkinLaneGroupId groupId, GameplaySkinLaneId laneId, GameplaySkinJudgementTimingStateSnapshot channels)
            => stream.Publish(producer, time, GameplaySkinEventValue.Judgement(judgement(groupId, laneId, channels)), groupId, laneId);

        private static GameplaySkinEventStateSnapshot current(GameplaySkinEventStream stream)
        {
            using GameplaySkinEventSubscription subscription = stream.Subscribe();
            Assert.That(subscription.TryDequeue(out GameplaySkinEventEnvelope? envelope), Is.True);
            return ((GameplaySkinStateEventPayload)envelope!.Payload).State;
        }

        private static GameplaySkinJudgementTimingStateSnapshot group(GameplaySkinEventStateSnapshot snapshot, GameplaySkinLaneGroupId groupId)
            => snapshot.JudgementTimingGroups.Single(entry => entry.GroupId == groupId).State;

        private static GameplaySkinTimingStateSnapshot clock() => new(0, 0, 120, false, 1);

        private static GameplaySkinEventStateSnapshot state(params GameplaySkinJudgementTimingGroupStateSnapshot[] channels)
            => new(GameplaySkinLifecycleState.Running,
                new[] { new GameplaySkinInputStateSnapshot(firstGroup, firstLane, false, 0), new GameplaySkinInputStateSnapshot(secondGroup, secondLane, false, 0) },
                Array.Empty<GameplaySkinObjectStateSnapshot>(), Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 1), clock(), Array.Empty<GameplaySkinBgaStateSnapshot>(), judgementTimingGroups: channels);
    }
}
