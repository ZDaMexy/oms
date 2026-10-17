// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestJudgementTriggeredTracksRestartGlobalAndKeepPooledLaneAndObjectOriginsIndependent()
        {
            using var firstTexture = new DummyRenderer().CreateTexture(8, 8);
            using var secondTexture = new DummyRenderer().CreateTexture(16, 16);
            DualJudgementFixture fixture = createTriggeredJudgementFixture(firstTexture, secondTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            using var firstOwner = new Container();
            using var secondOwner = new Container();
            using var repeatedOwner = new Container();
            host.ProcessFrame();

            GameplaySkinResolvedMaterialKey firstKey = explosionKey(fixture.FirstGroup, fixture.FirstLane);
            GameplaySkinResolvedMaterialKey secondKey = explosionKey(fixture.SecondGroup, fixture.SecondLane);
            using GameplaySkinSpecialisedSceneVisual first = host.PrepareSpecialisedVisual(firstKey, firstOwner)!;
            using GameplaySkinSpecialisedSceneVisual second = host.PrepareSpecialisedVisual(secondKey, secondOwner)!;
            using GameplaySkinSpecialisedSceneVisual repeated = host.PrepareSpecialisedVisual(firstKey, repeatedOwner)!;
            Assert.That(host.TryGetRuntimeNode("trigger.global", out GameplaySkinSceneRuntimeNode? global), Is.True);

            publishLifecycle(stream, producer, 0, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
            spawn(101, fixture.FirstGroup, fixture.FirstLane);
            spawn(202, fixture.SecondGroup, fixture.SecondLane);
            hit(101, fixture.FirstGroup, fixture.FirstLane, 100);
            first.OnApply(101);
            host.ProcessFrame();
            assertFrame(first.RuntimeNodes.Single(), 0, firstTexture);

            advance(150);
            assertFrame(first.RuntimeNodes.Single(), 50, secondTexture);
            publishLifecycle(stream, producer, 150, GameplaySkinEventKind.GameplayPaused, GameplaySkinLifecycleState.Paused);
            host.ProcessFrame();
            host.ProcessFrame();
            assertFrame(first.RuntimeNodes.Single(), 50, secondTexture);
            publishLifecycle(stream, producer, 150, GameplaySkinEventKind.GameplayResumed, GameplaySkinLifecycleState.Running);

            hit(202, fixture.SecondGroup, fixture.SecondLane, 180);
            second.OnApply(202);
            host.ProcessFrame();
            assertFrame(first.RuntimeNodes.Single(), 80, secondTexture);
            assertFrame(second.RuntimeNodes.Single(), 0, firstTexture);
            assertFrame(global!, 0, firstTexture);

            // CN head/tail share one object ID. Capture each apply before draining the queued second result.
            hit(101, fixture.FirstGroup, fixture.FirstLane, 190);
            repeated.OnApply(101);
            host.ProcessFrame();
            assertFrame(first.RuntimeNodes.Single(), 90, secondTexture);
            assertFrame(second.RuntimeNodes.Single(), 10, firstTexture);
            assertFrame(repeated.RuntimeNodes.Single(), 0, firstTexture);
            assertFrame(global!, 0, firstTexture);

            publishObject(stream, producer, 200, GameplaySkinEventKind.ObjectDespawned,
                triggeredObject(101, fixture.FirstGroup, fixture.FirstLane, GameplaySkinObjectState.Despawned));
            host.ProcessFrame();
            advance(240);
            assertFrame(repeated.RuntimeNodes.Single(), 50, secondTexture);

            first.OnFree();
            first.OnApply(303);
            host.ProcessFrame();
            assertFrame(first.RuntimeNodes.Single(), 0, firstTexture);

            // Two applications can occur between scene frames. Folded latest state must not erase the first time.
            publishObject(stream, producer, 240, GameplaySkinEventKind.ObjectSpawned,
                triggeredObject(303, fixture.FirstGroup, fixture.FirstLane, GameplaySkinObjectState.Visible));
            hit(303, fixture.FirstGroup, fixture.FirstLane, 250);
            first.OnFree();
            first.OnApply(303);
            hit(303, fixture.FirstGroup, fixture.FirstLane, 270);
            repeated.OnFree();
            repeated.OnApply(303);
            host.ProcessFrame();
            assertFrame(first.RuntimeNodes.Single(), 20, firstTexture);
            assertFrame(repeated.RuntimeNodes.Single(), 0, firstTexture);
            Assert.That(host.RuntimeFaults, Is.Empty);

            void spawn(long id, GameplaySkinLaneTopologyGroup group, GameplaySkinLaneTopologyEntry lane)
                => publishObject(stream, producer, 0, GameplaySkinEventKind.ObjectSpawned,
                    triggeredObject(id, group, lane, GameplaySkinObjectState.Visible));

            void hit(long id, GameplaySkinLaneTopologyGroup group, GameplaySkinLaneTopologyEntry lane, double time)
                => publishJudgement(stream, producer, time,
                    new GameplaySkinJudgementStateSnapshot(id, group.Identity.Id, lane.Identity.Id, GameplaySkinJudgementGrade.Perfect, 0, 0.01));

            void advance(double time)
            {
                producer.SynchroniseTiming(time, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1));
                host.ProcessFrame();
            }
        }

        [TestCase(GameplaySkinEventResetReason.Seek)]
        [TestCase(GameplaySkinEventResetReason.Rewind)]
        [TestCase(GameplaySkinEventResetReason.Retry)]
        public void TestJudgementTriggeredTracksHydrateLateAndResetWithoutOldEpochOrigin(GameplaySkinEventResetReason reason)
        {
            using var firstTexture = new DummyRenderer().CreateTexture(8, 8);
            using var secondTexture = new DummyRenderer().CreateTexture(16, 16);
            DualJudgementFixture fixture = createTriggeredJudgementFixture(firstTexture, secondTexture);
            var judgement = new GameplaySkinJudgementStateSnapshot(101, fixture.FirstGroup.Identity.Id, fixture.FirstLane.Identity.Id,
                GameplaySkinJudgementGrade.Perfect, 0, 0.01);
            var retained = new[]
            {
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Global, judgement, 100, 600),
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Group, judgement, 100, 600),
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Lane, judgement, 100, 600),
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Object, judgement, 100, 600),
            };
            var state = new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Running,
                Array.Empty<GameplaySkinInputStateSnapshot>(),
                new[] { triggeredObject(101, fixture.FirstGroup, fixture.FirstLane, GameplaySkinObjectState.Visible) },
                retained, score(1), new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1), Array.Empty<GameplaySkinBgaStateSnapshot>());
            using var stream = new GameplaySkinEventStream(fixture.Publication, 150, state);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            using var owner = new Container();
            host.ProcessFrame();
            using GameplaySkinSpecialisedSceneVisual visual = host.PrepareSpecialisedVisual(explosionKey(fixture.FirstGroup, fixture.FirstLane), owner)!;
            visual.OnApply(101);
            host.ProcessFrame();
            assertFrame(visual.RuntimeNodes.Single(), 50, secondTexture);
            Assert.That(host.TryGetRuntimeNode("trigger.global", out GameplaySkinSceneRuntimeNode? global), Is.True);
            assertFrame(global!, 50, secondTexture);

            try
            {
                producer = producer.Reset(120, state, reason);
                host.ProcessFrame();
                assertFrame(visual.RuntimeNodes.Single(), 20, firstTexture);
                assertFrame(global!, 20, firstTexture);

                producer = producer.Reset(0, lifecycleState(GameplaySkinLifecycleState.Running), reason);
                host.ProcessFrame();
                assertFrame(visual.RuntimeNodes.Single(), 0, firstTexture);
                assertFrame(global!, 0, firstTexture);
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        private static DualJudgementFixture createTriggeredJudgementFixture(Texture firstTexture, Texture secondTexture)
        {
            DualLayoutFixture layout = createDualStageLayout();
            GameplaySkinLaneTopologyGroup firstGroup = layout.Groups[0].TopologyGroup;
            GameplaySkinLaneTopologyGroup secondGroup = layout.Groups[1].TopologyGroup;
            GameplaySkinLaneTopologyEntry firstLane = firstGroup.LanesInLogicalOrder.Single();
            GameplaySkinLaneTopologyEntry secondLane = secondGroup.LanesInLogicalOrder.Single();
            var sourceResources = new[]
            {
                new GameplaySkinSceneResource("trigger.first", GameplaySkinSceneResourceType.Texture, "textures/first.png"),
                new GameplaySkinSceneResource("trigger.second", GameplaySkinSceneResourceType.Texture, "textures/second.png"),
            };
            var resources = new[]
            {
                new GameplaySkinPreparedSceneResource(sourceResources[0], "first", 8, 8, firstTexture),
                new GameplaySkinPreparedSceneResource(sourceResources[1], "second", 16, 16, secondTexture),
            };
            GameplaySkinSceneNode global = node("trigger.global", GameplaySkinSceneNodeType.Sprite, target(GameplaySkinSceneTargetKind.Global), GameplaySkinSlotCatalog.Decoration.Id, resources[0].Id);
            GameplaySkinSceneNode first = node("trigger.first-lane", GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Lane, firstLane.Identity.Id.Value, firstLane.GlobalLogicalIndex), GameplaySkinSlotCatalog.HitExplosion.Id, resources[0].Id);
            GameplaySkinSceneNode second = node("trigger.second-lane", GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Lane, secondLane.Identity.Id.Value, secondLane.GlobalLogicalIndex), GameplaySkinSlotCatalog.HitExplosion.Id, resources[0].Id);
            GameplaySkinSceneNode root = withChildren(node("trigger.root", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null), global, first, second);
            var tracks = new List<GameplaySkinSceneTrack>();

            foreach (GameplaySkinSceneNode child in root.Children)
            {
                tracks.Add(new GameplaySkinSceneTrack($"{child.Id}.frames", GameplaySkinSceneTrackType.Frame, child.Id, "resource", GameplaySkinSceneEasing.Step, false,
                    new[]
                    {
                        new GameplaySkinSceneKeyframe($"{child.Id}.frame-0", 0, GameplaySkinScenePropertyValue.FromString(resources[0].Id)),
                        new GameplaySkinSceneKeyframe($"{child.Id}.frame-1", 40, GameplaySkinScenePropertyValue.FromString(resources[1].Id)),
                    }, GameplaySkinSceneEvent.JudgementHit));
                tracks.Add(new GameplaySkinSceneTrack($"{child.Id}.rotation", GameplaySkinSceneTrackType.Tween, child.Id, "rotation", GameplaySkinSceneEasing.Linear, false,
                    new[]
                    {
                        new GameplaySkinSceneKeyframe($"{child.Id}.rotation-0", 0, GameplaySkinScenePropertyValue.FromNumber(0)),
                        new GameplaySkinSceneKeyframe($"{child.Id}.rotation-1", 100, GameplaySkinScenePropertyValue.FromNumber(100)),
                    }, GameplaySkinSceneEvent.JudgementHit));
            }

            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[]
            {
                provide(GameplaySkinSlotCatalog.Decoration, GameplaySkinResolvedMaterialTarget.Global, firstTexture, source()),
                provide(GameplaySkinSlotCatalog.HitExplosion, explosionKey(firstGroup, firstLane).Target, firstTexture, source()),
                provide(GameplaySkinSlotCatalog.HitExplosion, explosionKey(secondGroup, secondLane).Target, firstTexture, source()),
            });
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materialSet, "triggered-judgement-scene",
                new GameplaySkinSceneManifest(sourceResources),
                new GameplaySkinSceneDocument(root, tracks, Array.Empty<GameplaySkinSceneStateMachine>(), Array.Empty<GameplaySkinSceneBinding>(),
                    Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>()),
                resources, new[]
                {
                    prepared(root, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, null, null,
                        prepared(global, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, GameplaySkinSlotCatalog.Decoration, resources[0]),
                        prepared(first, layout.Groups[0].Rect, explosionKey(firstGroup, firstLane).Target, GameplaySkinSlotCatalog.HitExplosion, resources[0]),
                        prepared(second, layout.Groups[1].Rect, explosionKey(secondGroup, secondLane).Target, GameplaySkinSlotCatalog.HitExplosion, resources[0])),
                });
            return new DualJudgementFixture(GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene), firstGroup, firstLane, secondGroup, secondLane);
        }

        private static GameplaySkinResolvedMaterialKey explosionKey(GameplaySkinLaneTopologyGroup group, GameplaySkinLaneTopologyEntry lane)
            => new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.HitExplosion, GameplaySkinResolvedMaterialTarget.ForLane(group, lane));

        private static GameplaySkinObjectStateSnapshot triggeredObject(long id, GameplaySkinLaneTopologyGroup group, GameplaySkinLaneTopologyEntry lane, GameplaySkinObjectState state)
            => new GameplaySkinObjectStateSnapshot(id, GameplaySkinObjectKind.LongNote, state, group.Identity.Id, lane.Identity.Id, 0, 1000, state == GameplaySkinObjectState.Despawned ? 1 : 0);

        private static void assertFrame(GameplaySkinSceneRuntimeNode node, float rotation, Texture texture)
        {
            Assert.Multiple(() =>
            {
                Assert.That(node.TransformDrawable.Rotation, Is.EqualTo(rotation).Within(0.001f));
                Assert.That(((Sprite)node.ContentDrawable).Texture, Is.SameAs(texture));
            });
        }
    }
}
