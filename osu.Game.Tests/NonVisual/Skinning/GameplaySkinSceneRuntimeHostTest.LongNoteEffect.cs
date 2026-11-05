// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestLongNoteNativeHistoryDistinguishesHeadMissReleaseRegrabAndTerminalState()
        {
            LayoutFixture layout = createLayout();
            GameplaySkinObjectStateSnapshot current = lnState(layout, GameplaySkinObjectState.Visible);
            current = advance(GameplaySkinObjectState.Missed, 10);
            Assert.That(current.LastLongNoteTransitionAt, Is.Null, "A direct head miss cannot invent a held transition.");
            current = advance(GameplaySkinObjectState.Holding, 20);
            Assert.That(current.LastLongNoteTransitionAt, Is.EqualTo(20));
            current = advance(GameplaySkinObjectState.Holding, 25);
            Assert.That(current.StateChangedAt, Is.EqualTo(20), "Polling does not restart a held loop.");
            current = advance(GameplaySkinObjectState.Missed, 30);
            Assert.That(current.PreviousState, Is.EqualTo(GameplaySkinObjectState.Holding));
            current = advance(GameplaySkinObjectState.Completed, 31);
            Assert.That(current.StateChangedAt, Is.EqualTo(31));
            Assert.That(current.LastLongNoteTransitionAt, Is.EqualTo(30), "Terminal state must not restart the preceding release burst.");
            GameplaySkinObjectStateSnapshot normalized = GameplaySkinEventRuntimeHost.SnapshotObjectAt(current, 31);
            Assert.That(normalized.EndTime, Is.EqualTo(1000));
            Assert.That(normalized.Progress, Is.EqualTo(0.031).Within(0.000001),
                "A terminal observation must not replace the object's elapsed chart progress with an invented 100%.");

            GameplaySkinObjectStateSnapshot advance(GameplaySkinObjectState next, double time)
            {
                var history = GameplaySkinEventRuntimeHost.AdvanceObjectStateHistory(current, GameplaySkinObjectKind.LongNote, next, time, false);
                return lnState(layout, next, history.ChangedAt, history.Previous, history.LongNoteTransitionAt);
            }
        }

        [TestCase(null)]
        [TestCase(0d)]
        [TestCase(25d)]
        public void TestLongNoteHistorySurvivesCompactDeliveryAndSnapshotWithoutFillingUnknownTime(double? origin)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinObjectStateSnapshot state = lnState(layout, GameplaySkinObjectState.Missed, origin,
                origin.HasValue ? GameplaySkinObjectState.Holding : null, origin);
            GameplaySkinEventValue value = GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectStateChanged, state);
            GameplaySkinObjectStateSnapshot delivered = value.GetObject(state.GroupId, state.LaneId);
            Assert.That(delivered.StateChangedAt, Is.EqualTo(origin));
            Assert.That(delivered.PreviousState, Is.EqualTo(state.PreviousState));
            Assert.That(delivered.LastLongNoteTransitionAt, Is.EqualTo(origin));
            GameplaySkinObjectStateSnapshot snapshot = GameplaySkinEventRuntimeHost.SnapshotObjectAt(delivered, 100);
            Assert.That(snapshot.StateChangedAt, Is.EqualTo(origin));
            Assert.That(snapshot.PreviousState, Is.EqualTo(state.PreviousState));
            Assert.That(snapshot.LastLongNoteTransitionAt, Is.EqualTo(origin));
            var polled = GameplaySkinEventRuntimeHost.AdvanceObjectStateHistory(snapshot, snapshot.Kind, snapshot.State, 200, false);
            Assert.That(polled.ChangedAt, Is.EqualTo(origin));
            Assert.That(polled.Previous, Is.EqualTo(state.PreviousState));
        }

        [Test]
        public void TestLongNoteRewindClearsFutureHistoryAndRestorationDoesNotCreateAnEdge()
        {
            LayoutFixture layout = createLayout();
            var state = lnState(layout, GameplaySkinObjectState.Holding, 90, GameplaySkinObjectState.Missed, 90);
            var rewound = GameplaySkinEventRuntimeHost.SnapshotObjectAt(state, 50);
            Assert.That(rewound.StateChangedAt, Is.Null);
            Assert.That(rewound.PreviousState, Is.Null);
            Assert.That(rewound.LastLongNoteTransitionAt, Is.Null);
            var restored = GameplaySkinEventRuntimeHost.AdvanceObjectStateHistory(rewound, rewound.Kind, GameplaySkinObjectState.Missed, 51, true);
            Assert.That(restored.ChangedAt, Is.Null);
            Assert.That(restored.Previous, Is.Null);
            Assert.That(restored.LongNoteTransitionAt, Is.Null);
        }

        [TestCase("object.state", 10d, 0f)]
        [TestCase("object.state", null, 1f)]
        [TestCase("object.long-note-transition", 10d, 0f)]
        [TestCase("object.long-note-transition", null, 0f)]
        public void TestLongNoteRecordedClockLateAttachAndUnknownPhase(string trigger, double? origin, float alpha)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createLongNoteEffectFixture(texture, trigger);
            using GameplaySkinLayoutPublication publication = fixture.Publication;
            var state = lnState(createLayoutFromFixture(fixture), GameplaySkinObjectState.Holding, origin,
                origin.HasValue ? GameplaySkinObjectState.Visible : null, origin);
            using var stream = new GameplaySkinEventStream(publication, 20, lnSnapshot(publication, state));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LongNoteEffect,
                GameplaySkinResolvedMaterialTarget.ForLane(fixture.Group, fixture.Lane));
            using GameplaySkinSpecialisedSceneVisual visual = host.PrepareSpecialisedVisual(key, new Container())!;
            visual.OnApply(101);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.EqualTo(alpha));
            if (!origin.HasValue)
            {
                producer.SynchroniseTiming(120, publication.PreparedScene.InitialEventState.Timing);
                host.ProcessFrame();
                Assert.That(effectAlpha(visual), Is.EqualTo(alpha), "An unknown origin does not advance with reception time.");
            }
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestLongNoteTerminalOutroKeepsRealReleaseOriginAndCannotLeakAcrossResetOrPoolReuse()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createLongNoteEffectFixture(texture, "object.long-note-transition");
            using GameplaySkinLayoutPublication publication = fixture.Publication;
            LayoutFixture layout = createLayoutFromFixture(fixture);
            using var stream = new GameplaySkinEventStream(publication, 10,
                lnSnapshot(publication, lnState(layout, GameplaySkinObjectState.Holding, 10, GameplaySkinObjectState.Visible, 10)));
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LongNoteEffect,
                GameplaySkinResolvedMaterialTarget.ForLane(fixture.Group, fixture.Lane));
            using GameplaySkinSpecialisedSceneVisual visual = host.PrepareSpecialisedVisual(key, new Container())!;
            visual.OnApply(101);
            publishObject(stream, producer, 20, GameplaySkinEventKind.ObjectStateChanged,
                lnState(layout, GameplaySkinObjectState.Missed, 20, GameplaySkinObjectState.Holding, 20));
            publishObject(stream, producer, 21, GameplaySkinEventKind.ObjectStateChanged,
                lnState(layout, GameplaySkinObjectState.Completed, 21, GameplaySkinObjectState.Missed, 20));
            publishObject(stream, producer, 22, GameplaySkinEventKind.ObjectDespawned,
                lnState(layout, GameplaySkinObjectState.Despawned, 22, GameplaySkinObjectState.Completed, 20));
            producer.SynchroniseTiming(40, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.EqualTo(1), "Frame2 is sampled from release20, not terminal21 or despawn22.");
            producer = producer.Reset(5, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Rewind);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.Zero);
            visual.OnFree();
            visual.OnApply(202);
            var next = new GameplaySkinObjectStateSnapshot(202, GameplaySkinObjectKind.LongNote, GameplaySkinObjectState.Missed,
                layout.Group.Identity.Id, layout.Lane.Identity.Id, 0, 1000, 0, stateChangedAt: 6, previousState: GameplaySkinObjectState.Visible);
            publishObject(stream, producer, 6, GameplaySkinEventKind.ObjectSpawned, next);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.Zero, "An unrelated head miss cannot inherit the retired hold's burst.");
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLongNoteNativeLeaseFinishStopsOnlyLoopsAndReuseRestoresThem(bool loop)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createLongNoteEffectFixture(texture, "object.long-note-transition", loop);
            using GameplaySkinLayoutPublication publication = fixture.Publication;
            LayoutFixture layout = createLayoutFromFixture(fixture);
            using var stream = new GameplaySkinEventStream(publication, 20,
                lnSnapshot(publication, lnState(layout, GameplaySkinObjectState.Missed, 20, GameplaySkinObjectState.Holding, 20)));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            var key = new GameplaySkinResolvedMaterialKey(GameplaySkinSlotCatalog.LongNoteEffect,
                GameplaySkinResolvedMaterialTarget.ForLane(fixture.Group, fixture.Lane));
            using GameplaySkinSpecialisedSceneVisual visual = host.PrepareSpecialisedVisual(key, new Container())!;
            visual.OnApply(101);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.EqualTo(1));

            using GameplaySkinEventSubscription observer = stream.Subscribe();
            observer.DrainFrame(_ => { });
            visual.FinishLongNoteEffect();
            Assert.That(effectAlpha(visual), Is.EqualTo(loop ? 0 : 1));
            Assert.That(visual.IsApplied, Is.True, "The exact object lease still owns its bounded outro.");
            Assert.That(visual.BoundObjectId, Is.EqualTo(101));
            var afterFinish = new List<GameplaySkinEventEnvelope>();
            observer.DrainFrame(afterFinish.Add);
            Assert.That(afterFinish, Is.Empty, "Finishing a visual must not invent a Completed object edge.");

            producer.SynchroniseTiming(40, publication.PreparedScene.InitialEventState.Timing);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.EqualTo(loop ? 0 : 1),
                "The non-loop outro keeps release20 as its origin; a loop cannot resume on the next scene update.");
            visual.OnFree();
            visual.OnApply(101);
            host.ProcessFrame();
            Assert.That(effectAlpha(visual), Is.EqualTo(1), "Pool reuse restores animation admission without reusing the finished flag.");
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        private static GameplaySkinObjectStateSnapshot lnState(LayoutFixture layout, GameplaySkinObjectState state,
            double? origin = null, GameplaySkinObjectState? previous = null, double? transition = null)
            => new(101, GameplaySkinObjectKind.LongNote, state, layout.Group.Identity.Id, layout.Lane.Identity.Id, 0, 1000,
                state is GameplaySkinObjectState.Completed or GameplaySkinObjectState.Despawned ? 1 : 0,
                GameplaySkinLongNoteMode.HellCharge, GameplaySkinObjectAutoAssist.None, origin, previous, transition);

        private static LayoutFixture createLayoutFromFixture(RuntimeFixture fixture)
            => new(fixture.Publication.Snapshot, fixture.Group, fixture.Lane, fixture.StageRect, fixture.LaneRect);

        private static GameplaySkinEventStateSnapshot lnSnapshot(GameplaySkinLayoutPublication publication, GameplaySkinObjectStateSnapshot state)
        {
            var initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, new[] { state }, initial.CurrentJudgements,
                initial.Score, initial.Timing, initial.BgaViewports);
        }

        private static float effectAlpha(GameplaySkinSpecialisedSceneVisual visual)
            => visual.RuntimeNodes.Single().ContentDrawable.ChildrenOfType<Sprite>().Single().Alpha;

        private static RuntimeFixture createLongNoteEffectFixture(Texture texture, string trigger, bool loop = false)
        {
            LayoutFixture layout = createLayout();
            var laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            var materials = materialSetFor(layout.Snapshot, new[] { provide(GameplaySkinSlotCatalog.LongNoteEffect, laneTarget, texture, source()) });
            var definition = new GameplaySkinRecordedAnimationDefinition("hold.clip", "clip.json", new[] { "hold.texture" });
            string json = """
                {"contract":"oms-recorded-animation.v1","width":8,"height":8,"fps":100,"loop":LOOP,"trigger":"TRIGGER",
                 "geometries":[{"resource":"hold.texture","vertices":[[0,0,0,0,0],[8,0,0,1,0],[0,8,0,0,1]],"triangles":[0,1,2]}],
                 "nodes":[{"parent":-1,"geometry":0,"blend":"normal"}],
                 "states":[{"matrix":[1,0,0,0,1,0,0,0,1,0,0,0],"origin":[0,0,0],"projection":"affine","multiply":[1,1,1,1],
                 "add":[0,0,0,0],"hsl":[0,0,0],"mask":null,"colourMatrix":null}],
                 "frames":[{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null},{"nodes":[[0,0]],"camera":null},{"nodes":[],"camera":null}]}
                """.Replace("TRIGGER", trigger, StringComparison.Ordinal)
                   .Replace("LOOP", loop ? "true" : "false", StringComparison.Ordinal)
                   .Replace("oms-recorded-animation.v1", GameplaySkinRecordedAnimationCodec.CONTRACT, StringComparison.Ordinal);
            GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationCodec.Decode(Encoding.UTF8.GetBytes(json), definition);
            var resource = new GameplaySkinSceneResource("hold.texture", GameplaySkinSceneResourceType.Texture, "hold.png");
            var manifest = new GameplaySkinSceneManifest(new[] { resource }, animations: new[] { definition });
            var root = new GameplaySkinSceneNode("hold.root", GameplaySkinSceneNodeType.RecordedAnimation,
                target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0), GameplaySkinSlotCatalog.LongNoteEffect.Id,
                null, GameplaySkinSceneBlendMode.Alpha, new Dictionary<string, GameplaySkinScenePropertyValue>(),
                Array.Empty<GameplaySkinSceneEffect>(), Array.Empty<GameplaySkinSceneNode>(), definition.Id);
            var document = new GameplaySkinSceneDocument(root, Array.Empty<GameplaySkinSceneTrack>(), Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(), Array.Empty<GameplaySkinSceneTemplate>(), Array.Empty<GameplaySkinSceneInstance>());
            var package = new GameplaySkinPreparedPackage("long-note", manifest, document, Array.Empty<GameplaySkinCapturedSceneResource>(), null, null,
                recordedAnimations: new Dictionary<string, GameplaySkinRecordedAnimationProgram> { [definition.Id] = program });
            var scene = new GameplaySkinPreparedScene(layout.Snapshot, materials, "long-note", manifest, document,
                new[] { new GameplaySkinPreparedSceneResource(resource, "long-note", 16, 16, texture) },
                new[] { prepared(root, layout.LaneRect, laneTarget, GameplaySkinSlotCatalog.LongNoteEffect, null) }, package: package);
            return new RuntimeFixture(GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, scene),
                layout.Group, layout.Lane, layout.StageRect, layout.LaneRect);
        }
    }
}
