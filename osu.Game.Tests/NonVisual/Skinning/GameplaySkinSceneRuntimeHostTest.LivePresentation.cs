// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase("judgement.count.fast", "3")]
        [TestCase("judgement.count.slow", "4")]
        [TestCase("offset.value", "-7.5")]
        [TestCase("offset.available", "true")]
        [TestCase("offset.automatic", "true")]
        [TestCase("timing.time", "250")]
        [TestCase("timing.elapsed", "750")]
        [TestCase("presentation.elapsed", "750")]
        [TestCase("timing.start-available", "true")]
        [TestCase("timing.beat-phase", "0.75")]
        [TestCase("arrival.available", "true")]
        [TestCase("arrival.time", "1000")]
        [TestCase("arrival.remaining", "750")]
        [TestCase("arrival.visible", "true")]
        [TestCase("score.personal-best", "75")]
        [TestCase("score.personal-best-progress", "0.75")]
        [TestCase("score.personal-best-available", "true")]
        [TestCase("score.rank-aaa", "89")]
        [TestCase("score.rank-aa", "78")]
        [TestCase("score.rank-a", "67")]
        [TestCase("score.rank-aaa-progress", "0.89")]
        [TestCase("score.rank-available", "true")]
        [TestCase("input.motion-counts-1", "-20")]
        [TestCase("input.motion-revolutions-1", "0.25")]
        [TestCase("input.motion-available-1", "true")]
        [TestCase("input.motion-available-2", "false")]
        public void TestLivePresentationBindingsRetainRealMetadataAtLateAttachAndReset(string binding, string expected)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: binding);
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            GameplaySkinInputStateSnapshot lane = initial.Inputs.Single();
            var snapshot = new GameplaySkinEventStateSnapshot(initial.LifecycleState,
                new[] { new GameplaySkinInputStateSnapshot(lane.GroupId, lane.LaneId, false, 0, motionCounts: -20, motionRevolutions: 0.25, motionAvailable: true) },
                initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(29, 0, 19, 0.8, 0.5, new GameplaySkinJudgementStatistics(10, 9, 8, 7, 6, 5, 4, 3, 4), maximumScore: 100,
                    offsetState: new GameplaySkinOffsetStateSnapshot(true, -7.5, true)),
                new GameplaySkinTimingStateSnapshot(-0.25, -1, 120, false, 1, startTime: -500), initial.BgaViewports,
                chartPresentation: new GameplaySkinChartPresentation(new[] { new GameplaySkinLaneArrival(lane.GroupId, lane.LaneId, 1000) }, true, 75));
            using var stream = new GameplaySkinEventStream(publication, 250, snapshot);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            for (int epoch = 0; epoch < 2; epoch++)
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                host.ProcessFrame();
                Assert.That(host.TryGetRuntimeNode("accuracy", out GameplaySkinSceneRuntimeNode? text), Is.True);
                Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo(expected));
                Assert.That(host.RuntimeFaults, Is.Empty);
                producer = producer.Reset(250, snapshot, GameplaySkinEventResetReason.Retry);
            }
            producer.Dispose();
        }

        [TestCase("offset.available")]
        [TestCase("offset.automatic")]
        [TestCase("score.personal-best-available")]
        [TestCase("score.rank-available")]
        [TestCase("arrival.available")]
        [TestCase("timing.start-available")]
        [TestCase("input.motion-available-1")]
        public void TestMissingLivePresentationIsExplicitlyUnavailable(string binding)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: binding);
            using GameplaySkinEventStream stream = createStream(publication);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("accuracy", out GameplaySkinSceneRuntimeNode? text), Is.True);
            Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo("false"));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestReadOnlyPhaseClockSamplesNegativeMotionAndBeatWithoutInventingPresses()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = liveClockPublication(texture, "input.motion-revolutions-1");
            GameplaySkinInputStateSnapshot lane = publication.PreparedScene.InitialEventState.Inputs.Single();
            using GameplaySkinEventStream stream = createStream(publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.Zero);

            stream.Publish(producer, 100, GameplaySkinEventValue.Input(GameplaySkinEventKind.InputMotionChanged,
                new GameplaySkinInputStateSnapshot(lane.GroupId, lane.LaneId, false, 0, motionRevolutions: -0.25, motionAvailable: true)), lane.GroupId, lane.LaneId);
            producer.SynchroniseTiming(100, new GameplaySkinTimingStateSnapshot(0.5, 0, 120, false, 1));
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(270).Within(0.0001));
            Assert.That(runtime(host, "beat").TransformDrawable.Alpha, Is.EqualTo(0.5).Within(0.0001));
            using (GameplaySkinEventSubscription observer = stream.Subscribe())
            {
                GameplaySkinEventEnvelope? attach = null;
                observer.DrainFrame(envelope => attach = envelope);
                var state = ((GameplaySkinStateEventPayload)attach!.Payload).State.Inputs.Single();
                Assert.That(state.IsPressed, Is.False);
                Assert.That(state.PressedAt, Is.Null);
                Assert.That(state.MotionRevolutions, Is.EqualTo(-0.25));
            }
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(270), "A paused clock cannot continue cosmetic integration.");
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestGameplayTimeBindingAdvancesWhenTheBeatAndScoreStayUnchanged()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = CreateEssentialInformationPublication(texture, true, bindingSource: "timing.time");
            using GameplaySkinEventStream stream = createStream(publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            long initialApplications = host.BindingApplicationCount;
            for (int i = 1; i <= 4; i++)
            {
                producer.SynchroniseTiming(i, publication.PreparedScene.InitialEventState.Timing);
                host.ProcessFrame();
                Assert.That(((SpriteText)runtime(host, "accuracy").ContentDrawable).Text.ToString(), Is.EqualTo(i.ToString()));
            }
            Assert.That(host.BindingApplicationCount - initialApplications, Is.EqualTo(4),
                "Only the explicit gameplay time source changes; existing timing.progress and score.accuracy bindings stay untouched.");
            host.ProcessFrame();
            Assert.That(host.BindingApplicationCount - initialApplications, Is.EqualTo(4), "A stationary gameplay clock must not reformat any time text.");
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestElapsedClockKeepsAuthoredPreviewDefaultsAndRestoresThemAfterUnavailableReset()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = liveClockPublication(texture, "timing.elapsed", 37);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(37), "Settings/preview has no gameplay start authority.");
            producer.SynchroniseTiming(0.25, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(90).Within(0.0001));
            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(37), "An unavailable epoch restores author defaults, not the prior animation frame.");
            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.Zero, "A real zero elapsed time is distinct from unavailable metadata.");
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        [Test]
        public void TestPresentationClockContinuesFromPreparationIntoGameplayAndResetsWithItsAuthority()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = liveClockPublication(texture, "presentation.elapsed", 37, 10000);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(37));

            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1,
                preparation: new GameplaySkinPreparationStateSnapshot(true, true, 2500, 2500, false, 0)));
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(90).Within(0.0001));

            var completed = new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1,
                preparation: new GameplaySkinPreparationStateSnapshot(true, false, 5000, 0, false, 0));
            producer.SynchroniseTiming(0, completed);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(180).Within(0.0001));
            var started = new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0, preparation: completed.Preparation);
            producer.SynchroniseTiming(0, started);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(180).Within(0.0001));
            producer.SynchroniseTiming(1000, started);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(216).Within(0.0001));
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(216).Within(0.0001), "Pause freezes both authorities.");

            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(37), "An unavailable epoch restores the authored preview.");
            producer.SynchroniseTiming(1000, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
            host.ProcessFrame();
            Assert.That(runtime(host, "phase").TransformDrawable.Rotation, Is.EqualTo(36).Within(0.0001), "Skipping optional preparation uses the real gameplay clock.");
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        private static GameplaySkinLayoutPublication liveClockPublication(Texture texture, string clock, double initialRotation = 0, double period = 1)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.TextHud, global, texture, source()) });
            JObject json = JObject.Parse("""
                {"contract":"oms-gameplay-skin-scene.v1",
                 "root":{"id":"live.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                   {"id":"phase","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{},"effects":[],"children":[]},
                   {"id":"beat","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{},"effects":[],"children":[]}]},
                 "tracks":[{"id":"motion.clock","type":"tween","target":"phase","property":"rotation","easing":"linear","loop":true,"clock":"input.motion-revolutions-1","keyframes":[
                   {"id":"motion.zero","time":0,"value":0},{"id":"motion.end","time":1,"value":360}]},
                   {"id":"beat.clock","type":"tween","target":"beat","property":"opacity","easing":"linear","loop":true,"clock":"timing.beat","keyframes":[
                   {"id":"beat.zero","time":0,"value":0},{"id":"beat.end","time":1,"value":1}]}],
                 "stateMachines":[],"templates":[],"instances":[],"bindings":[]}
                """);
            json["tracks"]![0]!["clock"] = clock;
            json["tracks"]![0]!["keyframes"]![1]!["time"] = period;
            json["root"]!["children"]![0]!["properties"]!["rotation"] = initialRotation;
            var manifest = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(json.ToString(), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(", ", decoded.Diagnostics.Select(d => d.Code)));
            GameplaySkinSceneDocument document = decoded.Value!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(document), manifest);
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(roundTrip.Value!.Tracks[0].ClockSourceId, Is.EqualTo(clock));
            var owner = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.TextHud, global);
            GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface("mania.hud").Rect;
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "live-presentation", manifest, document,
                Array.Empty<GameplaySkinPreparedSceneResource>(), new[]
                {
                    prepared(document.Root, rect, global, GameplaySkinSlotCatalog.TextHud, null,
                        document.Root.Children.Select(child => new GameplaySkinPreparedSceneNode(child.Id, child, child.Target, rect, global, null, null, null,
                            Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.HudForeground, owner, false)).ToArray()),
                });
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene);
        }
    }
}
