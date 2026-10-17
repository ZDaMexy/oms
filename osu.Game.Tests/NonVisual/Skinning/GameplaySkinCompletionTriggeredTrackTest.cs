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
        [Test]
        public void TestCompletionTracksPreserveOriginAcrossPauseLateAttachAndConsumerResetThenClearForRetry()
        {
            using var first = new DummyRenderer().CreateTexture(8, 8);
            using var second = new DummyRenderer().CreateTexture(16, 16);
            RuntimeFixture fixture = createCompletionFixture(first, second);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var live = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            live.ProcessFrame();
            assertCompletion(live, 0, false, first);
            Assert.That(live.GetCompletionOutroDuration(false), Is.Zero);
            Assert.That(live.GetCompletionOutroDuration(true), Is.EqualTo(2500));

            try
            {
                stream.Publish(producer, 100, GameplaySkinEventValue.Lifecycle(
                    GameplaySkinEventKind.GameplayCompleted, GameplaySkinLifecycleState.Completed, true), null, null);
                live.ProcessFrame();
                assertCompletion(live, 0, true, first);
                advance(150);
                assertCompletion(live, 50, true, second);
                Assert.That(live.GetCompletionOutroEndTime(150, 2500), Is.EqualTo(2600), "A later consumer cannot restart the Player outro deadline.");

                // No presentation or wall-clock sample may advance a paused gameplay clock.
                live.ProcessFrame();
                live.ProcessFrame();
                assertCompletion(live, 50, true, second);

                // Duplicate terminal observation cannot restart the already retained origin.
                stream.Publish(producer, 160, GameplaySkinEventValue.Lifecycle(
                    GameplaySkinEventKind.GameplayCompleted, GameplaySkinLifecycleState.Completed, true), null, null);
                live.ProcessFrame();
                assertCompletion(live, 60, true, second);
                using var late = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                late.ProcessFrame();
                assertCompletion(late, 60, true, second);

                producer = producer.Reset(175, completionSnapshot(100, true), GameplaySkinEventResetReason.ConsumerRebuilt);
                live.ProcessFrame();
                late.ProcessFrame();
                assertCompletion(live, 75, true, second);
                assertCompletion(late, 75, true, second);

                producer = producer.Reset(0, lifecycleState(GameplaySkinLifecycleState.Running), GameplaySkinEventResetReason.Retry);
                live.ProcessFrame();
                late.ProcessFrame();
                assertCompletion(live, 0, false, first);
                assertCompletion(late, 0, false, first);

                stream.Publish(producer, 1000, GameplaySkinEventValue.Lifecycle(
                    GameplaySkinEventKind.GameplayCompleted, GameplaySkinLifecycleState.Completed, true), null, null);
                live.ProcessFrame();
                assertCompletion(live, 0, true, first);
                advance(3500);
                Assert.That(live.TryGetRuntimeNode("completion.sprite", out GameplaySkinSceneRuntimeNode? sprite), Is.True);
                Assert.That(sprite!.TransformDrawable.Alpha, Is.Zero, "The declared transparent tail stays hidden after the last frame.");
                using var ended = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                ended.ProcessFrame();
                Assert.That(ended.TryGetRuntimeNode("completion.sprite", out GameplaySkinSceneRuntimeNode? endedSprite), Is.True);
                Assert.That(endedSprite!.TransformDrawable.Alpha, Is.Zero, "Late attach after the outro must not replay it.");
                Assert.That(live.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }

            void advance(double time)
            {
                producer.SynchroniseTiming(time, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1));
                live.ProcessFrame();
                Assert.That(live.TryGetRuntimeNode("completion.global", out GameplaySkinSceneRuntimeNode? control), Is.True);
                Assert.That(control!.TransformDrawable.Rotation, Is.EqualTo(Math.Min(2500, time)).Within(0.001), "An omitted trigger keeps global-clock semantics.");
            }
        }

        [TestCase(GameplaySkinEventResetReason.Seek)]
        [TestCase(GameplaySkinEventResetReason.Rewind)]
        public void TestCompletionResetUsesTheReplacementOriginOnly(GameplaySkinEventResetReason reason)
        {
            using var first = new DummyRenderer().CreateTexture(8, 8);
            using var second = new DummyRenderer().CreateTexture(16, 16);
            RuntimeFixture fixture = createCompletionFixture(first, second);
            using var stream = new GameplaySkinEventStream(fixture.Publication, 150, completionSnapshot(100, true));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            host.ProcessFrame();
            assertCompletion(host, 50, true, second);
            try
            {
                producer = producer.Reset(10, lifecycleState(GameplaySkinLifecycleState.Running), reason);
                host.ProcessFrame();
                assertCompletion(host, 0, false, first);
                producer = producer.Reset(30, completionSnapshot(20, false), reason);
                host.ProcessFrame();
                assertCompletion(host, 10, false, first);
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestCompletionCompactOutcomeAndBackpressureResetRetainOneOrigin()
        {
            GameplaySkinEventValue compact = GameplaySkinEventValue.Lifecycle(
                GameplaySkinEventKind.GameplayCompleted, GameplaySkinLifecycleState.Completed, true);
            Assert.That(compact.GetCompletionFullCombo(), Is.True);
            Assert.That(((GameplaySkinLifecycleEventPayload)compact.Materialize(null, null)).IsFullCombo, Is.True);
            Assert.Throws<ArgumentException>(() => GameplaySkinEventValue.Lifecycle(
                GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running, true));
            Assert.Throws<ArgumentException>(() => completionSnapshot(100, true).ValidateForGameplayTime(99));
            Assert.Throws<ArgumentException>(() => new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running, Array.Empty<GameplaySkinInputStateSnapshot>(),
                Array.Empty<GameplaySkinObjectStateSnapshot>(), Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(0), new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1), Array.Empty<GameplaySkinBgaStateSnapshot>(),
                completion: new GameplaySkinCompletionStateSnapshot(100, true)));

            using var first = new DummyRenderer().CreateTexture(8, 8);
            using var second = new DummyRenderer().CreateTexture(16, 16);
            RuntimeFixture fixture = createCompletionFixture(first, second);
            using var stream = new GameplaySkinEventStream(fixture.Publication, 0,
                fixture.Publication.PreparedScene.InitialEventState, pendingEventBudget: 1);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using GameplaySkinEventSubscription slow = stream.Subscribe();
            slow.DrainFrame(_ => { });
            stream.Publish(producer, 1, GameplaySkinEventValue.Lifecycle(
                GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running), null, null);
            Assert.Throws<GameplaySkinEventBackpressureException>(() => stream.Publish(producer, 100, compact, null, null));
            try
            {
                producer = producer.Reset(150, completionSnapshot(100, true), GameplaySkinEventResetReason.ConsumerRebuilt);
                slow.DrainFrame(_ => { });
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                host.ProcessFrame();
                assertCompletion(host, 50, true, second);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestCompletionAuthorContractRoundTripsAndRejectsUnboundedOrUnconsumedOutro()
        {
            using var first = new DummyRenderer().CreateTexture(8, 8);
            using var second = new DummyRenderer().CreateTexture(16, 16);
            createCompletionFixture(first, second, out GameplaySkinSceneDocument document, out GameplaySkinSceneManifest manifest);
            string encoded = GameplaySkinSceneCodec.EncodeScene(document);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(encoded, manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", decoded.Diagnostics.Select(d => d.Id)));
            Assert.That(decoded.Value!.CompletionOutro, Is.EqualTo(2500));
            Assert.That(GameplaySkinSceneCodec.EncodeScene(decoded.Value), Is.EqualTo(encoded));
            Assert.That(decoded.Value.Tracks.Count(track => track.Trigger == GameplaySkinSceneEvent.GameplayComplete), Is.EqualTo(3));

            JObject json = JObject.Parse(encoded);
            json["completionOutro"] = 2501;
            invalid(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            json["completionOutro"] = -1;
            invalid(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
            json["completionOutro"] = "2500";
            invalid(GameplaySkinSceneDiagnosticCode.InvalidValueType);
            json["completionOutro"] = double.NaN;
            invalid(GameplaySkinSceneDiagnosticCode.InvalidValueType); // Newtonsoft serialises NaN as a JSON string.
            json["completionOutro"] = 2500;
            json["bindings"]![0]!["source"] = "input.pressed";
            invalid(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
            json["bindings"]![0]!["source"] = "gameplay.full-combo";
            json["tracks"]![0]!["loop"] = true;
            invalid(GameplaySkinSceneDiagnosticCode.InvalidAnimation);
            json["tracks"]![0]!["loop"] = false;
            json["bindings"]![0]!["property"] = "opacity";
            invalid(GameplaySkinSceneDiagnosticCode.InvalidPropertyValue);

            json = JObject.Parse(encoded);
            json.Remove("completionOutro");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> oldDefault = GameplaySkinSceneCodec.DecodeScene(json.ToString(), manifest);
            Assert.That(oldDefault.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(oldDefault.Value!.CompletionOutro, Is.Zero);
            Assert.That(JObject.Parse(GameplaySkinSceneCodec.EncodeScene(oldDefault.Value))["completionOutro"], Is.Null);

            void invalid(GameplaySkinSceneDiagnosticCode code)
            {
                GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = GameplaySkinSceneCodec.DecodeScene(json.ToString(), manifest);
                Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
                Assert.That(result.Diagnostics.Select(d => d.Code), Does.Contain(code));
            }
        }

        private static GameplaySkinEventStateSnapshot completionSnapshot(double time, bool fullCombo)
            => new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Completed,
                Array.Empty<GameplaySkinInputStateSnapshot>(), Array.Empty<GameplaySkinObjectStateSnapshot>(),
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(), score(3),
                new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1), Array.Empty<GameplaySkinBgaStateSnapshot>(),
                completion: new GameplaySkinCompletionStateSnapshot(time, fullCombo));

        private static void assertCompletion(GameplaySkinSceneRuntimeHost host, double elapsed, bool fullCombo, Texture texture)
        {
            string routes = string.Join(",", host.PreparedScene.HostedSlots.Select(slot => $"{slot.Key.Slot.Id}:{slot.Route}"));
            Assert.That(host.RuntimeFaults, Is.Empty, $"Completion fixture must retain its exact owner and layer; routes={routes}");
            Assert.That(host.TryGetRuntimeNode("completion.gate", out GameplaySkinSceneRuntimeNode? gate), Is.True,
                $"Expected the prepared completion owner; routes={routes}, instances={host.RuntimeInstanceCount}");
            Assert.That(host.TryGetRuntimeNode("completion.sprite", out GameplaySkinSceneRuntimeNode? sprite), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(gate!.TransformDrawable.Alpha, Is.EqualTo(fullCombo ? 1 : 0));
                Assert.That(sprite!.TransformDrawable.Rotation, Is.EqualTo(elapsed).Within(0.001));
                Assert.That(((Sprite)sprite.ContentDrawable).Texture, Is.SameAs(texture));
            });
        }

        private static RuntimeFixture createCompletionFixture(Texture first, Texture second)
            => createCompletionFixture(first, second, out _, out _);

        private static RuntimeFixture createCompletionFixture(Texture first, Texture second,
                                                               out GameplaySkinSceneDocument document, out GameplaySkinSceneManifest manifest)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot,
                new[] { provide(GameplaySkinSlotCatalog.StageBackground, stage, first, source()) });
            var firstResource = new GameplaySkinSceneResource("completion.first", GameplaySkinSceneResourceType.Texture, "textures/first.png");
            var secondResource = new GameplaySkinSceneResource("completion.second", GameplaySkinSceneResourceType.Texture, "textures/second.png");
            var firstPrepared = new GameplaySkinPreparedSceneResource(firstResource, "first", 8, 8, first);
            var secondPrepared = new GameplaySkinPreparedSceneResource(secondResource, "second", 16, 16, second);
            GameplaySkinSceneTarget stageTarget = target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, 0);
            GameplaySkinSceneNode sprite = node("completion.sprite", GameplaySkinSceneNodeType.Sprite, stageTarget, null, firstResource.Id);
            GameplaySkinSceneNode control = node("completion.global", GameplaySkinSceneNodeType.Container, stageTarget, null, null);
            GameplaySkinSceneNode gate = withChildren(node("completion.gate", GameplaySkinSceneNodeType.Container,
                stageTarget, GameplaySkinSlotCatalog.StageBackground.Id, null), sprite, control);
            GameplaySkinSceneNode root = withChildren(node("completion.root", GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global), null, null, blend: GameplaySkinSceneBlendMode.Inherit), gate);
            document = new GameplaySkinSceneDocument(root,
                new[]
                {
                    new GameplaySkinSceneTrack("completion.frames", GameplaySkinSceneTrackType.Frame, sprite.Id, "resource", GameplaySkinSceneEasing.Step, false,
                        new[] { frame("completion.frame0", 0, firstResource.Id), frame("completion.frame1", 50, secondResource.Id) }, GameplaySkinSceneEvent.GameplayComplete),
                    tween("completion.rotation", sprite.Id, "rotation", 0, 2500, GameplaySkinSceneEvent.GameplayComplete),
                    tween("completion.fade", sprite.Id, "opacity", 1, 0, GameplaySkinSceneEvent.GameplayComplete),
                    tween("completion.clock", control.Id, "rotation", 0, 2500),
                }, Array.Empty<GameplaySkinSceneStateMachine>(),
                new[] { new GameplaySkinSceneBinding("completion.visibility", gate.Id, "visible", "gameplay.full-combo") },
                Array.Empty<GameplaySkinSceneVariant>(), Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>(), 2500);
            manifest = new GameplaySkinSceneManifest(new[] { firstResource, secondResource });
            var ownerKey = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.StageBackground, stage);
            // These are the same frozen layer and nearest-slot owner inherited by the real preparer. The short
            // test constructor otherwise gives a slot-less child Underlay/no owner even below a Background slot.
            var preparedSprite = new GameplaySkinPreparedSceneNode(sprite.Id, sprite, stageTarget, layout.StageRect,
                stage, null, firstPrepared, first, Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.Background, ownerKey, false);
            var preparedControl = new GameplaySkinPreparedSceneNode(control.Id, control, stageTarget, layout.StageRect,
                stage, null, null, null, Array.Empty<GameplaySkinPreparedSceneNode>(), GameplaySkinSceneLayer.Background, ownerKey, false);
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "completion",
                manifest, document,
                new[] { firstPrepared, secondPrepared },
                new[] { prepared(root, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, null, null,
                    prepared(gate, layout.StageRect, stage, GameplaySkinSlotCatalog.StageBackground, null,
                        preparedSprite, preparedControl)) });
            return new RuntimeFixture(GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene),
                layout.Group, layout.Lane, layout.StageRect, layout.LaneRect);

            static GameplaySkinSceneKeyframe frame(string id, double time, string resource)
                => new GameplaySkinSceneKeyframe(id, time, GameplaySkinScenePropertyValue.FromString(resource));

            static GameplaySkinSceneTrack tween(string id, string targetId, string property, double start, double end,
                                                 GameplaySkinSceneEvent trigger = GameplaySkinSceneEvent.Unspecified)
                => new GameplaySkinSceneTrack(id, GameplaySkinSceneTrackType.Tween, targetId, property, GameplaySkinSceneEasing.Linear, false,
                    new[] { new GameplaySkinSceneKeyframe(id + ".start", 0, GameplaySkinScenePropertyValue.FromNumber(start)),
                        new GameplaySkinSceneKeyframe(id + ".end", 2500, GameplaySkinScenePropertyValue.FromNumber(end)) }, trigger);
        }
    }
}
