// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase(GameplaySkinLongNoteMode.Standard, "ln")]
        [TestCase(GameplaySkinLongNoteMode.Charge, "cn")]
        [TestCase(GameplaySkinLongNoteMode.HellCharge, "hcn")]
        public void TestLongNoteModeSurvivesCompactProgressAndEpochReplacement(GameplaySkinLongNoteMode mode, string expected)
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: "object.long-note-mode");
            GameplaySkinObjectStateSnapshot hold = projectionObject(publication, GameplaySkinObjectState.Holding, mode);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, hold);
            Assert.That(compact.GetObject(hold.GroupId, hold.LaneId).LongNoteMode, Is.EqualTo(mode));
            Assert.That(((GameplaySkinObjectEventPayload)compact.Materialize(hold.GroupId, hold.LaneId)).State.LongNoteMode, Is.EqualTo(mode));

            GameplaySkinObjectStateSnapshot rewound = GameplaySkinEventRuntimeHost.SnapshotObjectAt(hold, 150);
            Assert.That(rewound.Progress, Is.EqualTo(0.25));
            Assert.That(rewound.LongNoteMode, Is.EqualTo(mode));
            using var stream = new GameplaySkinEventStream(publication, 150, projectionState(publication, new[] { rewound }));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertProjectionText(host, expected);

            foreach (GameplaySkinEventResetReason reason in new[]
                     {
                         GameplaySkinEventResetReason.Seek, GameplaySkinEventResetReason.Rewind, GameplaySkinEventResetReason.Retry,
                     })
            {
                producer = producer.Reset(150, projectionState(publication, new[] { rewound }), reason);
                host.ProcessFrame();
                assertProjectionText(host, expected);
                using GameplaySkinEventSubscription late = stream.Subscribe();
                var events = new List<GameplaySkinEventEnvelope>();
                late.DrainFrame(events.Add);
                Assert.That(((GameplaySkinStateEventPayload)events.Single().Payload).State.ActiveObjects.Single().LongNoteMode, Is.EqualTo(mode));
            }

            producer.Dispose();
        }

        [TestCase(GameplaySkinLongNoteMode.Standard, GameplaySkinObjectState.Visible, "ln")]
        [TestCase(GameplaySkinLongNoteMode.Standard, GameplaySkinObjectState.Holding, "ln-holding")]
        [TestCase(GameplaySkinLongNoteMode.Standard, GameplaySkinObjectState.Missed, "ln-released")]
        [TestCase(GameplaySkinLongNoteMode.Charge, GameplaySkinObjectState.Visible, "cn")]
        [TestCase(GameplaySkinLongNoteMode.Charge, GameplaySkinObjectState.Holding, "cn-holding")]
        [TestCase(GameplaySkinLongNoteMode.Charge, GameplaySkinObjectState.Missed, "cn-released")]
        [TestCase(GameplaySkinLongNoteMode.HellCharge, GameplaySkinObjectState.Visible, "hcn")]
        [TestCase(GameplaySkinLongNoteMode.HellCharge, GameplaySkinObjectState.Holding, "hcn-holding")]
        [TestCase(GameplaySkinLongNoteMode.HellCharge, GameplaySkinObjectState.Missed, "hcn-released")]
        public void TestLongNoteAppearanceUsesTypedModeAndCurrentObjectState(GameplaySkinLongNoteMode mode, GameplaySkinObjectState state, string expected)
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: "object.long-note-appearance");
            using var stream = new GameplaySkinEventStream(publication, 150, projectionState(publication, new[] { projectionObject(publication, state, mode) }));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertProjectionText(host, expected);
        }

        [Test]
        public void TestLongNoteDefaultsKeepOrdinaryManiaHoldsAndNonHoldObjectsDistinct()
        {
            GameplaySkinLaneGroupId groupId = GameplaySkinLaneGroupId.Create("stage-0");
            GameplaySkinLaneId laneId = GameplaySkinLaneId.Create("lane-0");
            var ordinaryHold = new GameplaySkinObjectStateSnapshot(1, GameplaySkinObjectKind.LongNote, GameplaySkinObjectState.Visible,
                groupId, laneId, 100, 300, 0);
            var shortNote = new GameplaySkinObjectStateSnapshot(2, GameplaySkinObjectKind.Note, GameplaySkinObjectState.Visible,
                groupId, laneId, 100, 100, 0);
            Assert.That(ordinaryHold.LongNoteMode, Is.EqualTo(GameplaySkinLongNoteMode.Standard));
            Assert.That(shortNote.LongNoteMode, Is.EqualTo(GameplaySkinLongNoteMode.None));
            Assert.Throws<ArgumentException>(() => new GameplaySkinObjectStateSnapshot(1, GameplaySkinObjectKind.LongNote, GameplaySkinObjectState.Visible,
                groupId, laneId, 100, 300, 0, GameplaySkinLongNoteMode.None));
            Assert.Throws<ArgumentException>(() => new GameplaySkinObjectStateSnapshot(2, GameplaySkinObjectKind.Note, GameplaySkinObjectState.Visible,
                groupId, laneId, 100, 100, 0, GameplaySkinLongNoteMode.HellCharge));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinObjectStateSnapshot(1, GameplaySkinObjectKind.LongNote, GameplaySkinObjectState.Visible,
                groupId, laneId, 100, 300, 0, (GameplaySkinLongNoteMode)99));
        }

        [Test]
        public void TestLongNoteModeCannotChangeWithinAnActiveObjectEpoch()
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, false);
            GameplaySkinObjectStateSnapshot hold = projectionObject(publication, GameplaySkinObjectState.Visible, GameplaySkinLongNoteMode.Standard);
            using var stream = new GameplaySkinEventStream(publication, 150, projectionState(publication, new[] { hold }));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            GameplaySkinObjectStateSnapshot changed = projectionObject(publication, GameplaySkinObjectState.Holding, GameplaySkinLongNoteMode.HellCharge);
            Assert.Throws<InvalidOperationException>(() => stream.Publish(producer, 150,
                GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectStateChanged, changed), hold.GroupId, hold.LaneId));
        }

        [Test]
        public void TestLongNoteAppearanceVariantRestartsFromExactStateAfterRegrabAndReset()
        {
            using var first = new DummyRenderer().CreateTexture(8, 8);
            using var second = new DummyRenderer().CreateTexture(16, 16);
            CompiledProgramFixture fixture = createCompiledProgramFixture(first, second,
                variantSource: "object.long-note-appearance", variantKey: "hcn-holding");
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new Adapter(fixture.Scene.Snapshot), fixture.Scene.MaterialSet, fixture.Scene);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("compiled.sprite", out GameplaySkinSceneRuntimeNode? node), Is.True);
            Assert.That(((Sprite)node!.ContentDrawable).Texture, Is.SameAs(first));

            GameplaySkinObjectStateSnapshot hold = projectionObject(publication, GameplaySkinObjectState.Visible, GameplaySkinLongNoteMode.HellCharge);
            stream.Publish(producer, 150, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, hold), hold.GroupId, hold.LaneId);
            foreach (GameplaySkinObjectState state in new[] { GameplaySkinObjectState.Holding, GameplaySkinObjectState.Missed, GameplaySkinObjectState.Holding })
            {
                stream.Publish(producer, 150, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectStateChanged,
                    projectionObject(publication, state, GameplaySkinLongNoteMode.HellCharge)), hold.GroupId, hold.LaneId);
                host.ProcessFrame();
                Assert.That(((Sprite)node.ContentDrawable).Texture, Is.SameAs(state == GameplaySkinObjectState.Holding ? second : first));
            }

            producer = producer.Reset(125, projectionState(publication, new[]
            {
                GameplaySkinEventRuntimeHost.SnapshotObjectAt(hold, 125),
            }), GameplaySkinEventResetReason.Rewind);
            host.ProcessFrame();
            Assert.That(((Sprite)node.ContentDrawable).Texture, Is.SameAs(first));
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        [TestCase("gauge.type", "hard", "none")]
        [TestCase("gauge.maximum", "1", "0")]
        [TestCase("gauge.clear-threshold", "0", "0")]
        [TestCase("gauge.clear-state", "clear", "unavailable")]
        public void TestGaugeProjectionBindingsFollowGaugeChangedAndReset(string binding, string changedText, string unavailableText)
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: binding);
            using var stream = new GameplaySkinEventStream(publication, 0, projectionState(publication,
                gauge: new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.Normal, 1, 0.8, false)));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            var gauge = new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.Hard, 1, 0, true);
            // The numeric gauge intentionally stays fixed. Type and engine-owned clear state are independent
            // of Health.Value change notifications and must still dirty the existing score state family.
            stream.Publish(producer, 1, GameplaySkinEventValue.Score(GameplaySkinEventKind.GaugeChanged,
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0.25, gaugeState: gauge)), null, null);
            host.ProcessFrame();
            assertProjectionText(host, changedText);

            producer = producer.Reset(0, projectionState(publication), GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            assertProjectionText(host, unavailableText);
            producer.Dispose();
        }

        [TestCase(GameplaySkinGaugeType.AssistEasy)]
        [TestCase(GameplaySkinGaugeType.Easy)]
        [TestCase(GameplaySkinGaugeType.Normal)]
        [TestCase(GameplaySkinGaugeType.Hard)]
        [TestCase(GameplaySkinGaugeType.ExHard)]
        [TestCase(GameplaySkinGaugeType.Hazard)]
        public void TestGaugeProjectionSurvivesCompactDelivery(GameplaySkinGaugeType type)
        {
            foreach (bool clear in new[] { false, true })
            {
                var gauge = new GameplaySkinGaugeStateSnapshot(type, 1, 0.6, clear);
                var score = new GameplaySkinScoreStateSnapshot(17, 3, 5, 0.8, 0.25, gaugeState: gauge);
                GameplaySkinEventValue compact = GameplaySkinEventValue.Score(GameplaySkinEventKind.GaugeChanged, score);
                Assert.That(compact.GetScore().GaugeState, Is.EqualTo(gauge));
                Assert.That(((GameplaySkinScoreEventPayload)compact.Materialize(null, null)).State.GaugeState, Is.EqualTo(gauge));
                Assert.That(compact.GetScore().GaugeState.ClearState,
                    Is.EqualTo(clear ? GameplaySkinGaugeClearState.Clear : GameplaySkinGaugeClearState.Uncleared));
            }

            var unavailable = new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0.25);
            Assert.That(GameplaySkinEventValue.Score(GameplaySkinEventKind.GaugeChanged, unavailable).GetScore().GaugeState.ClearState,
                Is.EqualTo(GameplaySkinGaugeClearState.Unavailable));
        }

        [Test]
        public void TestGaugeProjectionRejectsInvalidEngineValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinGaugeStateSnapshot((GameplaySkinGaugeType)99, 1, 0.8, false));
            Assert.Throws<ArgumentException>(() => new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.None, 1, 0.8, false));
            Assert.Throws<ArgumentException>(() => new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.None, 0, 0, true));
            foreach (double maximum in new[] { double.NaN, double.PositiveInfinity, 0, -1, 1.1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.Normal, maximum, 0, false));
            foreach (double threshold in new[] { double.NaN, double.PositiveInfinity, -0.1, 1.1 })
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinGaugeStateSnapshot(GameplaySkinGaugeType.Normal, 1, threshold, false));
        }

        private static GameplaySkinObjectStateSnapshot projectionObject(GameplaySkinLayoutPublication publication, GameplaySkinObjectState state, GameplaySkinLongNoteMode mode)
        {
            GameplaySkinLaneTopologyGroup group = publication.Snapshot.Context.Topology.GroupsInLogicalOrder.Single();
            return new GameplaySkinObjectStateSnapshot(17, GameplaySkinObjectKind.LongNote, state, group.Identity.Id,
                group.LanesInLogicalOrder.First().Identity.Id, 100, 300, 0.25, mode);
        }

        private static GameplaySkinEventStateSnapshot projectionState(GameplaySkinLayoutPublication publication,
                                                                     GameplaySkinObjectStateSnapshot[]? objects = null, GameplaySkinGaugeStateSnapshot gauge = default)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, objects ?? initial.ActiveObjects,
                initial.CurrentJudgements, new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0.25, gaugeState: gauge), initial.Timing, initial.BgaViewports);
        }

        private static void assertProjectionText(GameplaySkinSceneRuntimeHost host, string expected)
        {
            Assert.That(host.TryGetRuntimeNode("accuracy", out GameplaySkinSceneRuntimeNode? node), Is.True);
            Assert.That(((SpriteText)node!.ContentDrawable).Text.ToString(), Is.EqualTo(expected));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }
    }
}
