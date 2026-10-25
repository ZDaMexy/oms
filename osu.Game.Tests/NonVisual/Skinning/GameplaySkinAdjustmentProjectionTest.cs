// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinAdjustmentProjectionTest
    {
        [Test]
        public void TestUnavailableDoesNotInventAnAdjustmentOrLoseInactiveCoverValues()
        {
            Assert.That(default(GameplaySkinAdjustmentState).IsAvailable, Is.False);
            Assert.That(typeof(IGameplaySkinAdjustmentStateSource).IsAssignableFrom(typeof(DrawableManiaRuleset)), Is.False);
            var inactive = new GameplaySkinAdjustmentState(true, false, GameplaySkinHiSpeedMode.Floating, 300, 350, 125, false, false);
            Assert.That(inactive.SuddenUnits, Is.EqualTo(350));
            Assert.That(inactive.HiddenUnits, Is.EqualTo(125));
            Assert.Throws<ArgumentException>(() => new GameplaySkinAdjustmentState(false, true, GameplaySkinHiSpeedMode.Unspecified, 0, 0, 0, false, false));
            Assert.Throws<ArgumentException>(() => new GameplaySkinAdjustmentState(true, false, GameplaySkinHiSpeedMode.Unspecified, 0, 0, 0, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinAdjustmentState(true, false, (GameplaySkinHiSpeedMode)99, 0, 0, 0, false, false));
            foreach (float value in new[] { float.NaN, float.PositiveInfinity, -1, 1000.1f })
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinAdjustmentState(true, true, GameplaySkinHiSpeedMode.Normal, 0, value, 0, false, false));
                Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinAdjustmentState(true, true, GameplaySkinHiSpeedMode.Normal, 0, 0, value, false, false));
            }
        }

        [TestCase(GameplaySkinEventKind.TimingBeat)]
        [TestCase(GameplaySkinEventKind.TimingBar)]
        [TestCase(GameplaySkinEventKind.TimingBpmChanged)]
        [TestCase(GameplaySkinEventKind.TimingScrollChanged)]
        [TestCase(GameplaySkinEventKind.TimingStopStarted)]
        [TestCase(GameplaySkinEventKind.TimingStopEnded)]
        public void TestCompactTimingRetainsAdjustmentAndIndependentLift(GameplaySkinEventKind kind)
        {
            foreach (GameplaySkinHiSpeedMode mode in new[] { GameplaySkinHiSpeedMode.Normal, GameplaySkinHiSpeedMode.Floating, GameplaySkinHiSpeedMode.Classic })
            {
                foreach (bool active in new[] { false, true })
                {
                    var adjustment = new GameplaySkinAdjustmentState(true, active, mode, int.MaxValue, float.Epsilon, 999.1234f, active, !active);
                    var lift = new GameplaySkinLiftState(true, 150.123456789, 0.12345678901234, true);
                    var timing = new GameplaySkinTimingStateSnapshot(3.5, 2, 150, kind == GameplaySkinEventKind.TimingStopStarted, -0.5, 0.625, 2.25,
                        lift, -500, adjustmentState: adjustment);
                    GameplaySkinEventValue compact = GameplaySkinEventValue.Timing(kind, timing);
                    foreach (GameplaySkinTimingStateSnapshot decoded in new[] { compact.GetTiming(), ((GameplaySkinTimingEventPayload)compact.Materialize(null, null)).State })
                    {
                        Assert.That(decoded.AdjustmentState, Is.EqualTo(adjustment));
                        Assert.That(BitConverter.SingleToInt32Bits(decoded.AdjustmentState.SuddenUnits), Is.EqualTo(BitConverter.SingleToInt32Bits(float.Epsilon)));
                        Assert.That(decoded.LiftState, Is.EqualTo(lift));
                        Assert.That(decoded.IsStopped, Is.EqualTo(timing.IsStopped));
                        Assert.That(decoded.StartTime, Is.EqualTo(-500));
                        Assert.That(decoded.ScrollSpeed, Is.EqualTo(2.25));
                    }
                }
            }
        }

        [TestCase(GameplaySkinEventResetReason.Seek)]
        [TestCase(GameplaySkinEventResetReason.Retry)]
        [TestCase(GameplaySkinEventResetReason.ConsumerRebuilt)]
        public void TestQueuedTimingAndLateConsumersKeepTheirAdjustmentHighWater(GameplaySkinEventResetReason reason)
        {
            var applied = new GameplaySkinAdjustmentState(true, true, GameplaySkinHiSpeedMode.Normal, 300, 350, 0, true, false);
            var newer = new GameplaySkinAdjustmentState(true, false, GameplaySkinHiSpeedMode.Floating, 300, 500, 100, false, true);
            using var stream = new GameplaySkinEventStream(GameplaySkinEventRevision.Create(1, 1, 1, 1), 0, state(default));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription observer = stream.Subscribe();
            Assert.That(stream.TryDequeue(observer, out GameplaySkinEventRecord initial), Is.True);
            stream.Publish(producer, 10, GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingScrollChanged, timing(applied)), null, null);
            producer.SynchroniseTiming(20, timing(newer));
            stream.ReadConsumerTimingHighWater(observer, initial.GameplayTime, initial.AuthoritativeTiming, out _, out GameplaySkinTimingStateSnapshot before);
            Assert.That(before.AdjustmentState, Is.EqualTo(default(GameplaySkinAdjustmentState)));
            Assert.That(stream.TryDequeue(observer, out GameplaySkinEventRecord edge), Is.True);
            Assert.That(edge.AuthoritativeTiming.AdjustmentState, Is.EqualTo(applied));
            Assert.That(edge.Payload.GetTiming().AdjustmentState, Is.EqualTo(applied));
            stream.ReadConsumerTimingHighWater(observer, edge.GameplayTime, edge.AuthoritativeTiming, out double highWater, out GameplaySkinTimingStateSnapshot after);
            Assert.That(highWater, Is.EqualTo(20));
            Assert.That(after.AdjustmentState, Is.EqualTo(newer));
            Assert.That(observer.TryDequeue(out _), Is.False, "Adjustment high-water alone does not add an author event callback.");
            using (GameplaySkinEventSubscription late = stream.Subscribe())
            {
                Assert.That(late.TryDequeue(out GameplaySkinEventEnvelope? snapshot), Is.True);
                Assert.That(((GameplaySkinStateEventPayload)snapshot!.Payload).State.Timing.AdjustmentState, Is.EqualTo(newer));
            }
            producer = producer.Reset(0, state(default), reason);
            Assert.That(observer.TryDequeue(out GameplaySkinEventEnvelope? reset), Is.True);
            Assert.That(((GameplaySkinStateEventPayload)reset!.Payload).State.Timing.AdjustmentState.IsAvailable, Is.False);
            producer.Dispose();
        }

        [TestCase("cover.value")]
        [TestCase("cover.position")]
        public void TestRemovedCoverContextSourcesAreRejected(string source)
        {
            const string json = """
                                {"contract":"oms-gameplay-skin-scene.v1",
                                 "root":{"id":"readout","type":"text","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{"text":"","font-size":24},"effects":[],"children":[]},
                                 "tracks":[],"stateMachines":[],"templates":[],"instances":[],
                                 "bindings":[{"id":"readout.binding","target":"readout","property":"text","source":"__SOURCE__"}]}
                                """;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.Replace("__SOURCE__", source),
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(item => item.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.UnknownBindingSource));
        }

        private static GameplaySkinTimingStateSnapshot timing(GameplaySkinAdjustmentState adjustment)
            => new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, adjustmentState: adjustment);

        private static GameplaySkinEventStateSnapshot state(GameplaySkinAdjustmentState adjustment)
            => new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Loaded, Array.Empty<GameplaySkinInputStateSnapshot>(),
                Array.Empty<GameplaySkinObjectStateSnapshot>(), Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 0), timing(adjustment), Array.Empty<GameplaySkinBgaStateSnapshot>());
    }

    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase("scroll.available", "true", "false")]
        [TestCase("scroll.adjusting", "true", "false")]
        [TestCase("scroll.mode", "floating", "unavailable")]
        [TestCase("scroll.green-number", "300", "0")]
        [TestCase("cover.sudden-units", "350", "0")]
        [TestCase("cover.hidden-units", "125", "0")]
        [TestCase("cover.sudden-active", "true", "false")]
        [TestCase("cover.hidden-active", "false", "false")]
        public void TestAdjustmentBindingsChangeAtStationaryGameplayTimeAndReset(string source, string expected, string unavailable)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: source);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(((SpriteText)runtime(host, "accuracy").ContentDrawable).Text.ToString(), Is.EqualTo(unavailable));
            var adjustment = new GameplaySkinAdjustmentState(true, true, GameplaySkinHiSpeedMode.Floating, 300, 350, 125, true, false);
            GameplaySkinTimingStateSnapshot initial = publication.PreparedScene.InitialEventState.Timing;
            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(initial.Beat, initial.BarIndex, initial.Bpm, initial.IsStopped,
                initial.ScrollMultiplier, initial.Progress, initial.ScrollSpeed, initial.LiftState, initial.StartTime, initial.Preparation,
                initial.ArrivalCueFadeStartTime, adjustment));
            host.ProcessFrame();
            Assert.That(((SpriteText)runtime(host, "accuracy").ContentDrawable).Text.ToString(), Is.EqualTo(expected));
            using (var late = new GameplaySkinSceneRuntimeHost(publication, stream))
            {
                late.ProcessFrame();
                Assert.That(((SpriteText)runtime(late, "accuracy").ContentDrawable).Text.ToString(), Is.EqualTo(expected));
            }
            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(((SpriteText)runtime(host, "accuracy").ContentDrawable).Text.ToString(), Is.EqualTo(unavailable));
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        [TestCase("score.maximum-available")]
        [TestCase("score.personal-best-available")]
        [TestCase("score.rank-available")]
        public void TestSubTargetAvailabilityVariantsUseActualScoreData(string sourceId)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = adjustmentAvailabilityPublication(texture, sourceId);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(runtime(host, "availability.probe").TransformDrawable.Alpha, Is.EqualTo(1));
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            var available = new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(10, 0, 0, 1, 0, maximumScore: 100), initial.Timing, initial.BgaViewports,
                chartPresentation: new GameplaySkinChartPresentation(Array.Empty<GameplaySkinLaneArrival>(), true, 75));
            producer = producer.Reset(0, available, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(runtime(host, "availability.probe").TransformDrawable.Alpha, Is.Zero);
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLiftReversalVariantTracksAppliedProjectionAtStationaryTimeAndReset(bool reversed)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = adjustmentAvailabilityPublication(texture, "lift.reversed");
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            GameplaySkinTimingStateSnapshot initial = publication.PreparedScene.InitialEventState.Timing;
            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(initial.Beat, initial.BarIndex, initial.Bpm, initial.IsStopped, initial.ScrollMultiplier,
                liftState: new GameplaySkinLiftState(true, 125, 0.25, reversed)));
            host.ProcessFrame();
            Assert.That(runtime(host, "availability.probe").TransformDrawable.Alpha, Is.EqualTo(reversed ? 0 : 1));
            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(initial.Beat, initial.BarIndex, initial.Bpm, initial.IsStopped, initial.ScrollMultiplier,
                liftState: new GameplaySkinLiftState(true, 125, 0.25, !reversed)));
            host.ProcessFrame();
            Assert.That(runtime(host, "availability.probe").TransformDrawable.Alpha, Is.EqualTo(reversed ? 1 : 0),
                "Changing only the already-applied native projection must select the opposite readout branch.");
            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(runtime(host, "availability.probe").TransformDrawable.Alpha, Is.EqualTo(1), "An unavailable reset cannot retain the prior reversed branch.");
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        private static GameplaySkinLayoutPublication adjustmentAvailabilityPublication(Texture texture, string sourceId)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, texture, source()) });
            const string json = """
                                {"contract":"oms-gameplay-skin-scene.v1",
                                 "root":{"id":"availability.root","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                                   {"id":"availability.probe","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{},"effects":[],"children":[]}]},
                                 "tracks":[],"bindings":[],"templates":[],"instances":[],"stateMachines":[],
                                 "variants":[{"id":"availability.variant","target":"availability.probe","property":"visible","source":"__SOURCE__","default":false,
                                   "cases":[{"id":"availability.absent","key":"false","value":true}]}]}
                                """;
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.Replace("__SOURCE__", sourceId), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", decoded.Diagnostics.Select(d => d.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface("mania.hud").Rect;
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "availability-test", manifest, document,
                Array.Empty<GameplaySkinPreparedSceneResource>(), new[] { prepared(document.Root, rect, global, GameplaySkinSlotCatalog.TextHud, null,
                    new GameplaySkinPreparedSceneNode(document.Root.Children.Single().Id, document.Root.Children.Single(), document.Root.Children.Single().Target,
                        rect, global, null, null, null, Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false)) });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }

    }
}
