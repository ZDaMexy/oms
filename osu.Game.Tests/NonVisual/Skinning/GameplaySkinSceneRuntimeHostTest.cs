// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestAuthorGraphUsesPreparedDrawableTypesTextureGeometryEffectsAnimationBindingAndTemplate()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            producer.SynchroniseTiming(16, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.Publication, Is.SameAs(fixture.Publication));
                Assert.That(host.PreparedScene, Is.SameAs(fixture.Publication.PreparedScene));
                Assert.That(host.MaterialSet, Is.SameAs(fixture.Publication.MaterialSet));
                Assert.That(host.EventStream, Is.SameAs(stream));
                Assert.That(host.PendingCreationCount, Is.Zero);
                Assert.That(host.TryGetRuntimeNode("node.root", out GameplaySkinSceneRuntimeNode? root), Is.True);
                Assert.That(root!.ContentDrawable, Is.TypeOf<Container>());
                Assert.That(host.TryGetRuntimeNode("node.sprite", out GameplaySkinSceneRuntimeNode? sprite), Is.True);
                Assert.That(sprite!.ContentDrawable, Is.TypeOf<Sprite>());
                Assert.That(((Sprite)sprite.ContentDrawable).Texture, Is.SameAs(texture2));
                Assert.That(((Container)sprite.RootDrawable).ChildrenOfType<BufferedContainer>(), Has.Exactly(1).Items,
                    "The prepared blur effect must wrap the universal transform even before a non-visual host is loaded.");
                Assert.That(sprite.Rect, Is.EqualTo(fixture.StageRect));
                Assert.That(sprite.RootDrawable.RelativePositionAxes, Is.EqualTo(Axes.Both));
                Assert.That(sprite.RootDrawable.RelativeSizeAxes, Is.EqualTo(Axes.Both));
                Assert.That(sprite.RootDrawable.Blending, Is.EqualTo(BlendingParameters.Additive));
                Assert.That(sprite.TransformDrawable.Alpha, Is.EqualTo(0.37f).Within(0.001f));
                Assert.That(host.TryGetRuntimeNode("node.text", out GameplaySkinSceneRuntimeNode? text), Is.True);
                Assert.That(text!.ContentDrawable, Is.InstanceOf<SpriteText>());
                Assert.That(((SpriteText)text.ContentDrawable).Text.ToString(), Is.EqualTo("0"));
                Assert.That(host.TryGetRuntimeNode("node.mask", out GameplaySkinSceneRuntimeNode? mask), Is.True);
                Assert.That(mask!.ContentDrawable, Is.TypeOf<GameplaySkinSceneRuntimeHost.GameplaySkinShapeMaskContainer>());
                Assert.That(((Container)mask.ContentDrawable).Masking, Is.True);
                Assert.That(mask.RootDrawable.Blending.Source, Is.EqualTo(BlendingType.DstColor));
                Assert.That(mask.RootDrawable.Blending.Destination, Is.EqualTo(BlendingType.Zero));
                Assert.That(host.TryGetRuntimeNode("node.clip", out GameplaySkinSceneRuntimeNode? clip), Is.True);
                Assert.That(((Container)clip!.ContentDrawable).Masking, Is.True);
                Assert.That(clip.RootDrawable.Blending.Source, Is.EqualTo(BlendingType.One));
                Assert.That(clip.RootDrawable.Blending.Destination, Is.EqualTo(BlendingType.OneMinusSrcColor));
                Assert.That(host.TryGetRuntimeNode("instance.lane-1/template-node.root", out _), Is.True);
                Assert.That(host.TryGetRuntimeNode("instance.lane-1/template-node.root", out GameplaySkinSceneRuntimeNode? template), Is.True);
                Assert.That(GameplaySkinSceneRuntimeHost.ResolveBlend(GameplaySkinSceneBlendMode.Inherit), Is.EqualTo(BlendingParameters.Inherit));
                Assert.That(GameplaySkinSceneRuntimeHost.ResolveBlend(GameplaySkinSceneBlendMode.Alpha), Is.EqualTo(BlendingParameters.Mixture));
                Assert.That(GameplaySkinSceneRuntimeHost.ResolveBlend(GameplaySkinSceneBlendMode.Additive), Is.EqualTo(BlendingParameters.Additive));
                Assert.That(host.HostedSlots.Single(slot => slot.Key.Slot == GameplaySkinSlotCatalog.StageBackground).Route,
                    Is.EqualTo(GameplaySkinSceneHostRoute.Scene));
            });
        }

        [Test]
        public void TestPreparedProgramCompilesEveryDynamicResourceToExactImmutableResourceAndTexture()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            CompiledProgramFixture fixture = createCompiledProgramFixture(texture1, texture2);
            GameplaySkinPreparedSceneProgram program = fixture.Scene.Program;

            GameplaySkinPreparedSceneValue firstFrame = program.Tracks.Single().Keyframes[0].Value;
            GameplaySkinPreparedSceneValue stateAssignment = program.StateMachines.Single().States.Single().Assignments.Single().Value;
            GameplaySkinPreparedSceneVariant variant = program.Variants.Single();

            Assert.Multiple(() =>
            {
                Assert.That(typeof(GameplaySkinPreparedScene).GetProperty("Document", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null,
                    "The source document must not remain reachable after the immutable runtime program is compiled.");
                Assert.That(typeof(GameplaySkinPreparedScene).GetProperty("Manifest", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null,
                    "The source manifest must not remain reachable after resources and contracts are prepared.");
                Assert.That(typeof(GameplaySkinPreparedScene).GetMethod("TryGetResource", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null,
                    "A renderer must not regain string-to-resource lookup authority after preparation.");
                Assert.That(program.HasAuthorScene, Is.True);
                Assert.That(firstFrame.Resource, Is.SameAs(fixture.Resource1));
                Assert.That(firstFrame.Texture, Is.SameAs(texture1));
                Assert.That(stateAssignment.Resource, Is.SameAs(fixture.Resource2));
                Assert.That(stateAssignment.Texture, Is.SameAs(texture2));
                Assert.That(variant.DefaultResource, Is.SameAs(fixture.Resource1));
                Assert.That(variant.DefaultTexture, Is.SameAs(texture1));
                Assert.That(variant.Cases.Single().Resource, Is.SameAs(fixture.Resource2));
                Assert.That(variant.Cases.Single().Texture, Is.SameAs(texture2));
                Assert.That(variant.SelectResource("visible"), Is.SameAs(fixture.Resource2));
                Assert.That(variant.SelectResource("scheduled"), Is.SameAs(fixture.Resource1));
                Assert.That(() => ((IList<GameplaySkinPreparedSceneTrack>)program.Tracks).Clear(), Throws.TypeOf<NotSupportedException>());
                Assert.That(() => ((IList<GameplaySkinPreparedSceneVariantCase>)variant.Cases).Clear(), Throws.TypeOf<NotSupportedException>());
            });
        }

        [Test]
        public void TestCompiledVariantSwitchesDirectPreparedTexturesWithoutRuntimeResourceLookup()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            CompiledProgramFixture fixture = createCompiledProgramFixture(texture1, texture2);
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(
                new Adapter(fixture.Scene.Snapshot),
                fixture.Scene.MaterialSet,
                fixture.Scene);
            using GameplaySkinEventStream stream = createStream(publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            GameplaySkinLaneTopologyGroup group = fixture.Scene.Snapshot.Context.Topology.GroupsInLogicalOrder.Single();
            GameplaySkinLaneTopologyEntry lane = group.LanesInLogicalOrder.Single();
            var visible = new GameplaySkinObjectStateSnapshot(
                101,
                GameplaySkinObjectKind.Note,
                GameplaySkinObjectState.Visible,
                group.Identity.Id,
                lane.Identity.Id,
                0,
                100,
                0);

            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("compiled.sprite", out GameplaySkinSceneRuntimeNode? runtime), Is.True);
            Assert.That(((Sprite)runtime!.ContentDrawable).Texture, Is.SameAs(texture1));

            stream.Publish(producer, 0, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, visible), group.Identity.Id, lane.Identity.Id);
            host.ProcessFrame();
            Assert.That(((Sprite)runtime.ContentDrawable).Texture, Is.SameAs(texture2));

            var despawned = new GameplaySkinObjectStateSnapshot(
                visible.ObjectId,
                visible.Kind,
                GameplaySkinObjectState.Despawned,
                visible.GroupId,
                visible.LaneId,
                visible.StartTime,
                visible.EndTime,
                1);
            stream.Publish(producer, 1, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectDespawned, despawned), group.Identity.Id, lane.Identity.Id);
            host.ProcessFrame();
            Assert.That(((Sprite)runtime.ContentDrawable).Texture, Is.SameAs(texture1));
        }

        [TestCase("track")]
        [TestCase("state")]
        [TestCase("variant-default")]
        [TestCase("variant-case")]
        public void TestInvalidDynamicResourceFailsBackgroundProgramPreparation(string invalidReference)
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);

            GameplaySkinScenePreparationException exception = Assert.Throws<GameplaySkinScenePreparationException>(
                () => createCompiledProgramFixture(texture1, texture2, invalidReference))!;

            Assert.That(exception.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.UnknownResource));
        }

        [Test]
        public void TestDynamicResourceWithoutPreparedTextureFailsBackgroundProgramPreparation()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);

            GameplaySkinScenePreparationException exception = Assert.Throws<GameplaySkinScenePreparationException>(
                () => createCompiledProgramFixture(texture1, texture2, "unprepared-texture"))!;

            Assert.That(exception.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.InvalidResource));
        }

        [Test]
        public void TestSceneRuntimeRejectsSameValuedForeignAndUnboundEventStreams()
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createMaterialFixture(texture);
            using GameplaySkinLayoutPublication foreignPublication = GameplaySkinLayoutPublication.Create(
                new Adapter(fixture.Publication.Snapshot),
                fixture.Publication.MaterialSet);

            Assert.Multiple(() =>
            {
                Assert.That(foreignPublication, Is.Not.SameAs(fixture.Publication));
                Assert.That(foreignPublication.EventRevision, Is.EqualTo(fixture.Publication.EventRevision));
            });

            using var foreignStream = new GameplaySkinEventStream(
                foreignPublication,
                0,
                foreignPublication.PreparedScene.InitialEventState);
            using var unboundStream = new GameplaySkinEventStream(
                fixture.Publication.EventRevision,
                0,
                fixture.Publication.PreparedScene.InitialEventState);

            Assert.Multiple(() =>
            {
                Assert.That(
                    () => new GameplaySkinSceneRuntimeHost(fixture.Publication, foreignStream),
                    Throws.ArgumentException.With.Property("ParamName").EqualTo("eventStream"));
                Assert.That(
                    () => new GameplaySkinSceneRuntimeHost(fixture.Publication, unboundStream),
                    Throws.ArgumentException.With.Property("ParamName").EqualTo("eventStream"));
            });
        }

        [TestCase(GameplaySkinEventKind.GameplayLoaded, GameplaySkinLifecycleState.Loaded)]
        [TestCase(GameplaySkinEventKind.GameplayFailed, GameplaySkinLifecycleState.Failed)]
        public void TestLifecycleEdgeCannotRetainStateOutsideCanonicalSnapshotProjection(
            GameplaySkinEventKind eventKind,
            GameplaySkinLifecycleState lifecycle)
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            publishLifecycle(stream, producer, 1, eventKind, lifecycle);
            host.ProcessFrame();

            Assert.That(host.StateMachineStates["machine.key"], Is.EqualTo("state.idle"));
        }

        [TestCase(GameplaySkinLifecycleState.Loaded, "lifecycle.loaded")]
        [TestCase(GameplaySkinLifecycleState.Running, "lifecycle.running")]
        [TestCase(GameplaySkinLifecycleState.Paused, "lifecycle.paused")]
        [TestCase(GameplaySkinLifecycleState.Completed, "lifecycle.completed")]
        [TestCase(GameplaySkinLifecycleState.Failed, "lifecycle.failed")]
        public void TestLifecycleLiveLateAttachAndResetReplayConverge(
            GameplaySkinLifecycleState lifecycle,
            string expectedState)
        {
            RuntimeFixture fixture = createLifecycleReplayFixture();

            string live = runLive();
            string lateAttach = runLateAttach();
            string reset = runReset();

            Assert.Multiple(() =>
            {
                Assert.That(live, Is.EqualTo(expectedState));
                Assert.That(lateAttach, Is.EqualTo(live));
                Assert.That(reset, Is.EqualTo(live));
            });

            string runLive()
            {
                using var stream = new GameplaySkinEventStream(
                    fixture.Publication,
                    0,
                    lifecycleState(GameplaySkinLifecycleState.Loaded));
                using GameplaySkinEventProducer producer = stream.CreateProducer();
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                host.ProcessFrame();

                publishLifecycle(stream, producer, 1, GameplaySkinEventKind.GameplayLoaded, GameplaySkinLifecycleState.Loaded);

                if (lifecycle is GameplaySkinLifecycleState.Running
                    or GameplaySkinLifecycleState.Paused
                    or GameplaySkinLifecycleState.Completed
                    or GameplaySkinLifecycleState.Failed)
                {
                    publishLifecycle(stream, producer, 2, GameplaySkinEventKind.GameplayStarted, GameplaySkinLifecycleState.Running);
                }

                if (lifecycle == GameplaySkinLifecycleState.Paused)
                    publishLifecycle(stream, producer, 3, GameplaySkinEventKind.GameplayPaused, lifecycle);
                else if (lifecycle == GameplaySkinLifecycleState.Completed)
                    publishLifecycle(stream, producer, 3, GameplaySkinEventKind.GameplayCompleted, lifecycle);
                else if (lifecycle == GameplaySkinLifecycleState.Failed)
                    publishLifecycle(stream, producer, 3, GameplaySkinEventKind.GameplayFailed, lifecycle);

                host.ProcessFrame();
                return host.StateMachineStates["lifecycle.machine"];
            }

            string runLateAttach()
            {
                using var stream = new GameplaySkinEventStream(
                    fixture.Publication,
                    3,
                    lifecycleState(lifecycle));
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                host.ProcessFrame();
                return host.StateMachineStates["lifecycle.machine"];
            }

            string runReset()
            {
                using var stream = new GameplaySkinEventStream(
                    fixture.Publication,
                    0,
                    lifecycleState(GameplaySkinLifecycleState.Loaded));
                GameplaySkinEventProducer producer = stream.CreateProducer();
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

                try
                {
                    host.ProcessFrame();
                    producer = producer.Reset(3, lifecycleState(lifecycle), GameplaySkinEventResetReason.ConsumerRebuilt);
                    host.ProcessFrame();
                    return host.StateMachineStates["lifecycle.machine"];
                }
                finally
                {
                    producer.Dispose();
                }
            }
        }

        [Test]
        public void TestDenseEdgeBatchPerformsAtMostOneCanonicalStateMachineProjectionPerFrame()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            int before = host.StateMachineProjectionPassCount;

            for (int i = 0; i < 512; i++)
            {
                bool pressed = (i & 1) == 0;
                publishInput(stream, producer, i + 1, input(fixture, pressed));
                publishScore(stream, producer, i + 1, GameplaySkinEventKind.ComboChanged, score(i + 1));
            }

            host.ProcessFrame();

            Assert.That(host.StateMachineProjectionPassCount - before, Is.EqualTo(1));
        }

        [Test]
        public void TestBoundedBacklogPinsAnimationAndBindingsToEachConsumedHighWater()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            host.ProcessFrame();

            int eventCount = GameplaySkinEventBudgets.MAX_PENDING_EVENTS_PER_SUBSCRIPTION;

            for (int i = 1; i <= eventCount; i++)
            {
                double gameplayTime = i * 100d / eventCount;
                producer.SynchroniseTiming(
                    gameplayTime,
                    new GameplaySkinTimingStateSnapshot(gameplayTime, (long)Math.Floor(gameplayTime / 4), 120, false, 1));
                publishScore(stream, producer, gameplayTime, GameplaySkinEventKind.ComboChanged, score(combo: i));
            }

            producer.SynchroniseTiming(125, new GameplaySkinTimingStateSnapshot(125, 31, 120, false, 1));

            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("node.sprite", out GameplaySkinSceneRuntimeNode? sprite), Is.True);
            Assert.That(host.TryGetRuntimeNode("node.text", out GameplaySkinSceneRuntimeNode? text), Is.True);
            Assert.That(host.TryGetRuntimeNode("node.mask", out GameplaySkinSceneRuntimeNode? mask), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(host.LastSequence, Is.EqualTo(GameplaySkinEventBudgets.MAX_EVENTS_CONSUMED_PER_FRAME));
                Assert.That(host.LastGameplayTime, Is.EqualTo(50).Within(0.0001));
                Assert.That(sprite!.TransformDrawable.Alpha, Is.EqualTo(0.625f).Within(0.001f),
                    "Track sampling must not jump to the stream's latest time while older records remain queued.");
                Assert.That(mask!.TransformDrawable.Rotation, Is.EqualTo(50).Within(0.001f),
                    "Timing binding state must be stamped at the last consumed record, not read past the backlog.");
                Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo("2048"));
            });

            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.LastSequence, Is.EqualTo(eventCount));
                Assert.That(host.LastGameplayTime, Is.EqualTo(100).Within(0.0001));
                Assert.That(sprite!.TransformDrawable.Alpha, Is.EqualTo(1).Within(0.001f));
                Assert.That(mask!.TransformDrawable.Rotation, Is.EqualTo(125).Within(0.001f),
                    "Once the queue is empty the renderer must catch up to the latest fractional timing sample.");
                Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo(eventCount.ToString()));
            });
        }

        [Test]
        public void TestTimingFramesOnlyReapplyTimingBindingsAndNeverReformatScoreText()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            Assert.That(host.TryGetRuntimeNode("node.text", out GameplaySkinSceneRuntimeNode? text), Is.True);
            long applicationsBeforeTiming = host.BindingApplicationCount;

            const int timing_frames = 32;

            for (int i = 1; i <= timing_frames; i++)
            {
                producer.SynchroniseTiming(i, new GameplaySkinTimingStateSnapshot(i / 4d, i / 16, 120, false, 1));
                host.ProcessFrame();
            }

            Assert.Multiple(() =>
            {
                Assert.That(host.BindingApplicationCount - applicationsBeforeTiming, Is.EqualTo(timing_frames),
                    "Only the timing.beat binding may be sampled; combo.value must not allocate or rewrite text on timing-only frames.");
                Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo("0"));
            });

            publishScore(stream, producer, timing_frames + 1, GameplaySkinEventKind.ComboChanged, score(combo: 12));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.BindingApplicationCount - applicationsBeforeTiming, Is.EqualTo(timing_frames + 1));
                Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo("12"));
            });
        }

        [Test]
        public void TestTimingFramesDoNotResampleObjectVariantOrScoreBinding()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            CompiledProgramFixture fixture = createCompiledProgramFixture(texture1, texture2);
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(
                new Adapter(fixture.Scene.Snapshot),
                fixture.Scene.MaterialSet,
                fixture.Scene);
            using GameplaySkinEventStream stream = createStream(publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);

            host.ProcessFrame();
            long bindingsBefore = host.BindingApplicationCount;
            long variantsBefore = host.VariantApplicationCount;

            for (int i = 1; i <= 32; i++)
            {
                producer.SynchroniseTiming(i, new GameplaySkinTimingStateSnapshot(i / 4d, i / 16, 120, false, 1));
                host.ProcessFrame();
            }

            Assert.Multiple(() =>
            {
                Assert.That(host.BindingApplicationCount, Is.EqualTo(bindingsBefore));
                Assert.That(host.VariantApplicationCount, Is.EqualTo(variantsBefore));
            });
        }

        [Test]
        public void TestTimingFramesDoNotReapplyScoreSemanticText()
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialSourceIdentity sourceIdentity = source();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialEntry combo = provide(GameplaySkinSlotCatalog.ComboDisplay, stage, texture, sourceIdentity);
            GameplaySkinResolvedMaterialEntry hud = provide(GameplaySkinSlotCatalog.TextHud, stage, texture, sourceIdentity);
            RuntimeFixture runtimeFixture = fixture(layout, new[] { combo, hud });
            using GameplaySkinEventStream stream = createStream(runtimeFixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(runtimeFixture.Publication, stream);

            host.ProcessFrame();
            long applicationsBeforeTiming = host.SemanticStateApplicationCount;

            for (int i = 1; i <= 32; i++)
            {
                producer.SynchroniseTiming(i, new GameplaySkinTimingStateSnapshot(i / 4d, i / 16, 120, false, 1));
                host.ProcessFrame();
            }

            Assert.That(host.SemanticStateApplicationCount, Is.EqualTo(applicationsBeforeTiming),
                "Timing-only frames must not reformat ComboDisplay/TextHud semantic strings.");

            publishScore(stream, producer, 33, GameplaySkinEventKind.ComboChanged, score(combo: 12));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.SemanticStateApplicationCount - applicationsBeforeTiming, Is.EqualTo(2));
                Assert.That(host.TryGetHostedDrawable(combo.Key, out Drawable? comboDrawable), Is.True);
                Assert.That(((Container)comboDrawable!).Children.OfType<SpriteText>().Single().Text.ToString(), Is.EqualTo("12"));
                Assert.That(host.TryGetHostedDrawable(hud.Key, out Drawable? hudDrawable), Is.True);
                Assert.That(((Container)hudDrawable!).Children.OfType<SpriteText>().Single().Text.ToString(), Is.EqualTo("1000  12x"));
            });
        }

        [Test]
        public void TestHostedSlotTruthDistinguishesSemanticSpecialisedAndSuppressAndDisplaysSelectedTexture()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createMaterialFixture(selectedTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();

            GameplaySkinSceneHostedSlot background = host.HostedSlots.Single(slot => slot.Key.Slot == GameplaySkinSlotCatalog.StageBackground);
            GameplaySkinSceneHostedSlot note = host.HostedSlots.Single(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Note);
            GameplaySkinSceneHostedSlot decoration = host.HostedSlots.Single(slot => slot.Key.Slot == GameplaySkinSlotCatalog.Decoration);
            GameplaySkinSceneHostedSlot programmatic = host.HostedSlots.Single(slot => slot.Key.Slot == GameplaySkinSlotCatalog.LaneSurface);

            Assert.Multiple(() =>
            {
                Assert.That(host.HostedSlots.Count, Is.EqualTo(fixture.Publication.MaterialSet.Entries.Count));
                Assert.That(host.HostedSlotDescriptors, Is.EquivalentTo(fixture.Publication.MaterialSet.Entries.Select(entry => entry.Slot).Distinct()));
                Assert.That(background.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Semantic));
                Assert.That(background.Layer, Is.EqualTo(GameplaySkinSceneLayer.Background));
                Assert.That(background.SuppressesProgrammaticVisual, Is.True);
                Assert.That(note.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(note.Layer, Is.EqualTo(GameplaySkinSceneLayer.Object));
                Assert.That(decoration.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Suppressed));
                Assert.That(decoration.SuppressesProgrammaticVisual, Is.True);
                Assert.That(programmatic.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                Assert.That(programmatic.AllowsProgrammaticVisual, Is.True);
                Assert.That(host.TryGetHostedDrawable(background.Key, out Drawable? backgroundDrawable), Is.True);
                Assert.That(((Container)backgroundDrawable!).Children.OfType<Sprite>().Single().Texture, Is.SameAs(selectedTexture));
                Assert.That(host.TryGetHostedDrawable(note.Key, out _), Is.False);
                Assert.That(host.TryGetHostedDrawable(decoration.Key, out _), Is.False);
                Assert.That(host.TryGetHostedDrawable(programmatic.Key, out _), Is.False);
                Assert.That(host.TryGetVisualGate(background.Key, out GameplaySkinSceneHostedSlot? exactGate), Is.True);
                Assert.That(exactGate, Is.SameAs(background));
                Assert.That(host.RuntimeCapabilities.Support.Count, Is.EqualTo(4));
                Assert.That(host.Layers.Background.Children, Does.Contain(backgroundDrawable));
                Assert.That(host.Layers.Background, Is.Not.SameAs(host.Layers.HudForeground));
            });
        }

        [Test]
        public void TestSemanticSlotsConsumeExactC3SurfacesInsteadOfWholeStageOrLane()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialTarget lane = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialEntry[] entries =
            {
                provide(GameplaySkinSlotCatalog.JudgementLine, stage, selectedTexture, source()),
                provide(GameplaySkinSlotCatalog.HitTarget, lane, selectedTexture, source()),
                provide(GameplaySkinSlotCatalog.HitExplosion, lane, selectedTexture, source()),
                provide(GameplaySkinSlotCatalog.JudgementDisplay, stage, selectedTexture, source()),
                provide(GameplaySkinSlotCatalog.ComboDisplay, stage, selectedTexture, source()),
                provide(GameplaySkinSlotCatalog.GaugeVisual, stage, selectedTexture, source()),
                provide(GameplaySkinSlotCatalog.TextHud, stage, selectedTexture, source()),
            };
            RuntimeFixture runtimeFixture = fixture(layout, entries);
            using GameplaySkinEventStream stream = createStream(runtimeFixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(runtimeFixture.Publication, stream);

            host.ProcessFrame();

            assertGeometry(GameplaySkinSlotCatalog.JudgementLine, intersect(layout.StageRect, layout.Snapshot.GetSurface("mania.hit-target").Rect));
            assertGeometry(GameplaySkinSlotCatalog.HitTarget, intersect(layout.LaneRect, layout.Snapshot.GetSurface("mania.hit-target").Rect));
            assertGeometry(GameplaySkinSlotCatalog.JudgementDisplay, projectStageWidth(layout.StageRect, layout.Snapshot.GetSurface("mania.judgement").Rect));
            assertGeometry(GameplaySkinSlotCatalog.ComboDisplay, projectStageWidth(layout.StageRect, layout.Snapshot.GetSurface("mania.combo").Rect));
            assertGeometry(GameplaySkinSlotCatalog.GaugeVisual, projectStageWidth(layout.StageRect, layout.Snapshot.GetSurface("mania.gauge").Rect));
            assertGeometry(GameplaySkinSlotCatalog.TextHud, projectStageWidth(layout.StageRect, layout.Snapshot.GetSurface("mania.hud").Rect));

            GameplaySkinResolvedMaterialEntry hitExplosion = entries.Single(candidate => ReferenceEquals(candidate.Slot, GameplaySkinSlotCatalog.HitExplosion));
            var nativeHitTargetOwner = new Container { RelativeSizeAxes = Axes.Both };
            Assert.That(host.TryGetHostedDrawable(hitExplosion.Key, out _), Is.False,
                "HitExplosion is instantiated per judgement and must never become one shared semantic drawable.");
            Assert.That(host.TryGetVisualGate(hitExplosion.Key, out GameplaySkinSceneHostedSlot? hitExplosionGate), Is.True);
            using GameplaySkinSpecialisedSceneVisual? hitExplosionVisual = host.PrepareSpecialisedVisual(hitExplosion.Key, nativeHitTargetOwner);
            Assert.Multiple(() =>
            {
                Assert.That(hitExplosionGate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(hitExplosionGate.SpecialisedPoolCapacity,
                    Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_HIT_EXPLOSION_VISUALS_PER_KEY));
                Assert.That(hitExplosionVisual, Is.Not.Null);
                Assert.That(hitExplosionVisual!.Key.Target, Is.EqualTo(lane));
                Assert.That(hitExplosionVisual.RelativeSizeAxes, Is.EqualTo(Axes.Both),
                    "The pooled visual must consume the production lane hit-target owner rather than invent global geometry.");
                Assert.That(nativeHitTargetOwner.Children, Does.Contain(hitExplosionVisual));
            });

            void assertGeometry(GameplaySkinSlotDescriptor slot, GameplaySkinLayoutRect expected)
            {
                GameplaySkinResolvedMaterialEntry entry = entries.Single(candidate => ReferenceEquals(candidate.Slot, slot));
                Assert.That(host.TryGetHostedDrawable(entry.Key, out Drawable? drawable), Is.True);
                Assert.Multiple(() =>
                {
                    Assert.That(drawable!.X, Is.EqualTo(expected.X).Within(0.0001f));
                    Assert.That(drawable.Y, Is.EqualTo(expected.Y).Within(0.0001f));
                    Assert.That(drawable.Width, Is.EqualTo(expected.Width).Within(0.0001f));
                    Assert.That(drawable.Height, Is.EqualTo(expected.Height).Within(0.0001f));
                    GameplaySkinLayoutRect targetRect = entry.Target.Kind switch
                    {
                        GameplaySkinResolvedMaterialTargetKind.Global => layout.Snapshot.Context.SafeBounds,
                        GameplaySkinResolvedMaterialTargetKind.Lane => layout.LaneRect,
                        _ => layout.StageRect,
                    };
                    Assert.That(expected, Is.Not.EqualTo(targetRect));
                });
            }
        }

        [Test]
        public void TestStageScopedHudSurfacesProjectIntoIndependentDualGroups()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            DualLayoutFixture layout = createDualStageLayout();
            GameplaySkinSlotDescriptor[] slots =
            {
                GameplaySkinSlotCatalog.JudgementDisplay,
                GameplaySkinSlotCatalog.ComboDisplay,
                GameplaySkinSlotCatalog.GaugeVisual,
                GameplaySkinSlotCatalog.TextHud,
            };
            GameplaySkinResolvedMaterialEntry[] entries = slots.SelectMany(slot => layout.Groups.Select(group =>
                provide(slot, GameplaySkinResolvedMaterialTarget.ForStage(group.TopologyGroup), selectedTexture, source()))).ToArray();
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, entries);
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials);
            using GameplaySkinEventStream stream = createStream(publication);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);

            host.ProcessFrame();

            foreach (GameplaySkinSlotDescriptor slot in slots)
            {
                GameplaySkinLayoutRect surface = layout.Snapshot.GetSurface(slot == GameplaySkinSlotCatalog.JudgementDisplay
                    ? "mania.judgement"
                    : slot == GameplaySkinSlotCatalog.ComboDisplay
                        ? "mania.combo"
                        : slot == GameplaySkinSlotCatalog.GaugeVisual
                            ? "mania.gauge"
                            : "mania.hud").Rect;
                var rects = new List<GameplaySkinLayoutRect>();

                foreach (GameplaySkinLayoutGroup group in layout.Groups)
                {
                    GameplaySkinResolvedMaterialKey key = entries.Single(entry =>
                        ReferenceEquals(entry.Slot, slot) && entry.Target.GroupId == group.TopologyGroup.Identity.Id).Key;
                    Assert.That(host.TryGetHostedDrawable(key, out Drawable? drawable), Is.True);
                    GameplaySkinLayoutRect expected = projectStageWidth(group.Rect, surface);
                    var actual = GameplaySkinLayoutRect.Create(drawable!.X, drawable.Y, drawable.Width, drawable.Height);
                    Assert.That(actual, Is.EqualTo(expected));
                    rects.Add(actual);
                }

                Assert.That(rects[0].Right, Is.LessThanOrEqualTo(rects[1].Left),
                    $"{slot.Id} must retain distinct stage-local ownership in a dual-group publication.");
            }
        }

        [Test]
        public void TestDualStageLaneJudgementProjectionConvergesAcrossLiveLateAttachAndResetWithoutScopeLeakage()
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            DualJudgementFixture fixture = createDualJudgementFixture(texture);
            GameplaySkinJudgementStateSnapshot judgement = new GameplaySkinJudgementStateSnapshot(
                101,
                fixture.FirstGroup.Identity.Id,
                fixture.FirstLane.Identity.Id,
                GameplaySkinJudgementGrade.Great,
                -7,
                0.01);
            GameplaySkinObjectStateSnapshot firstObject = scopedObject(101, fixture.FirstGroup, fixture.FirstLane);
            GameplaySkinObjectStateSnapshot secondObject = scopedObject(202, fixture.SecondGroup, fixture.SecondLane);
            GameplaySkinCurrentJudgementStateSnapshot[] retained =
            {
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Global, judgement, 0, 500),
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Group, judgement, 0, 500),
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Lane, judgement, 0, 500),
                new GameplaySkinCurrentJudgementStateSnapshot(GameplaySkinJudgementScope.Object, judgement, 0, 500),
            };
            var complete = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Loaded,
                new[]
                {
                    new GameplaySkinInputStateSnapshot(fixture.FirstGroup.Identity.Id, fixture.FirstLane.Identity.Id, false, 0),
                    new GameplaySkinInputStateSnapshot(fixture.SecondGroup.Identity.Id, fixture.SecondLane.Identity.Id, false, 0),
                },
                new[] { firstObject, secondObject },
                retained,
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 1),
                new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1),
                Array.Empty<GameplaySkinBgaStateSnapshot>());

            ScopedProjection live = runLive();
            ScopedProjection lateAttach = runLateAttach();
            ScopedProjection reset = runReset();

            Assert.Multiple(() =>
            {
                Assert.That(live, Is.EqualTo(new ScopedProjection(0.9f, -7, 0.1f, 0)));
                Assert.That(lateAttach, Is.EqualTo(live));
                Assert.That(reset, Is.EqualTo(live));
            });

            ScopedProjection runLive()
            {
                using GameplaySkinEventStream stream = createStream(fixture.Publication);
                using GameplaySkinEventProducer producer = stream.CreateProducer();
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                host.ProcessFrame();
                stream.Publish(producer, 0, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, firstObject),
                    firstObject.GroupId, firstObject.LaneId);
                stream.Publish(producer, 0, GameplaySkinEventValue.Object(GameplaySkinEventKind.ObjectSpawned, secondObject),
                    secondObject.GroupId, secondObject.LaneId);
                stream.Publish(producer, 0, GameplaySkinEventValue.Judgement(judgement), judgement.GroupId, judgement.LaneId);
                host.ProcessFrame();
                return projection(host);
            }

            ScopedProjection runLateAttach()
            {
                using var stream = new GameplaySkinEventStream(fixture.Publication, 0, complete);
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
                host.ProcessFrame();
                return projection(host);
            }

            ScopedProjection runReset()
            {
                using GameplaySkinEventStream stream = createStream(fixture.Publication);
                GameplaySkinEventProducer producer = stream.CreateProducer();
                using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

                try
                {
                    host.ProcessFrame();
                    producer = producer.Reset(0, complete, GameplaySkinEventResetReason.ConsumerRebuilt);
                    host.ProcessFrame();
                    return projection(host);
                }
                finally
                {
                    producer.Dispose();
                }
            }

            static GameplaySkinObjectStateSnapshot scopedObject(
                long id,
                GameplaySkinLaneTopologyGroup group,
                GameplaySkinLaneTopologyEntry lane)
                => new GameplaySkinObjectStateSnapshot(
                    id,
                    GameplaySkinObjectKind.Note,
                    GameplaySkinObjectState.Visible,
                    group.Identity.Id,
                    lane.Identity.Id,
                    0,
                    100,
                    0);

            static ScopedProjection projection(GameplaySkinSceneRuntimeHost host)
            {
                Assert.That(host.TryGetRuntimeNode("scope.first", out GameplaySkinSceneRuntimeNode? first), Is.True);
                Assert.That(host.TryGetRuntimeNode("scope.second", out GameplaySkinSceneRuntimeNode? second), Is.True);
                return new ScopedProjection(
                    first!.TransformDrawable.Alpha,
                    first.TransformDrawable.Rotation,
                    second!.TransformDrawable.Alpha,
                    second.TransformDrawable.Rotation);
            }
        }

        [Test]
        public void TestSnapshotEdgesAndResetDriveBindingStateMachineAndEpoch()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            try
            {
                host.ProcessFrame();
                publishScore(stream, producer, 10, GameplaySkinEventKind.ComboChanged, score(combo: 12));
                publishInput(stream, producer, 10, input(fixture, true));
                host.ProcessFrame();

                Assert.Multiple(() =>
                {
                    Assert.That(host.CurrentEpoch, Is.Zero);
                    Assert.That(host.LastSequence, Is.EqualTo(2));
                    Assert.That(host.StateMachineStates["machine.key"], Is.EqualTo("state.pressed"));
                    Assert.That(host.TryGetRuntimeNode("node.text", out GameplaySkinSceneRuntimeNode? text), Is.True);
                    Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo("12"));
                });

                producer = producer.Reset(5, completeState(fixture, combo: 3, pressed: false), GameplaySkinEventResetReason.Rewind);
                host.ProcessFrame();

                Assert.Multiple(() =>
                {
                    Assert.That(host.CurrentEpoch, Is.EqualTo(1));
                    Assert.That(host.LastSequence, Is.Zero);
                    Assert.That(host.LastGameplayTime, Is.EqualTo(5));
                    Assert.That(host.StateMachineStates["machine.key"], Is.EqualTo("state.idle"));
                    Assert.That(host.TryGetRuntimeNode("node.text", out GameplaySkinSceneRuntimeNode? text), Is.True);
                    Assert.That(((SpriteText)text!.ContentDrawable).Text.ToString(), Is.EqualTo("3"));
                });
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestCreationQueueIsBoundedPerFrame()
        {
            RuntimeFixture fixture = createLargeAuthorFixture(GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME + 1);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.CreatedThisFrame, Is.LessThanOrEqualTo(GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME));
                Assert.That(host.PendingCreationCount, Is.Positive);
                Assert.That(host.RuntimeNodeCount, Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME));
            });

            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.PendingCreationCount, Is.Zero);
                Assert.That(host.RuntimeNodeCount, Is.EqualTo(GameplaySkinPreparedSceneBudgets.MAX_CREATIONS_PER_FRAME + 1));
            });
        }

        [Test]
        public void TestPreparedRuntimeSteadyFramesDoNotAllocate()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            host.ProcessFrame();
            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int frame = 0; frame < 1024; frame++)
                host.ProcessFrame();

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(allocatedBytes, Is.Zero,
                "A committed scene with tracks and bindings must not allocate on unchanged steady-state frames.");
        }

        [Test]
        public void TestProductionObjectEdgesUseAllocationFreeIncrementalMinimumIndexes()
        {
            const int active_count = 4096;

            RuntimeFixture runtimeFixture = fixture(createLayout(), Array.Empty<GameplaySkinResolvedMaterialEntry>());
            GameplaySkinObjectStateSnapshot[] active = Enumerable.Range(0, active_count)
                                                                 .Select(id => objectState(runtimeFixture, id, GameplaySkinObjectState.Visible))
                                                                 .ToArray();
            var initial = new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running,
                Array.Empty<GameplaySkinInputStateSnapshot>(),
                active,
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(0),
                new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1),
                Array.Empty<GameplaySkinBgaStateSnapshot>());
            using var stream = new GameplaySkinEventStream(runtimeFixture.Publication, 0, initial);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(runtimeFixture.Publication, stream);
            GameplaySkinObjectStateSnapshot high = objectState(runtimeFixture, active_count - 1, GameplaySkinObjectState.Holding);

            for (int i = 0; i < 2048; i++)
            {
                publishObject(stream, producer, i + 1, GameplaySkinEventKind.ObjectStateChanged, high);
                host.ProcessFrame();
            }

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int i = 0; i < 8192; i++)
            {
                publishObject(stream, producer, i + 2049, GameplaySkinEventKind.ObjectStateChanged, high);
                host.ProcessFrame();
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            Assert.That(allocated, Is.Zero,
                "A production object state edge must update its retained entry without envelope materialisation or an active-object index rebuild.");

            publishObject(
                stream,
                producer,
                10241,
                GameplaySkinEventKind.ObjectDespawned,
                objectState(runtimeFixture, 0, GameplaySkinObjectState.Despawned));
            host.ProcessFrame();

            GameplaySkinObjectStateSnapshot first = firstObject(host);
            Assert.Multiple(() =>
            {
                Assert.That(first.ObjectId, Is.EqualTo(1), "Removing the minimum advances only the bounded heap index.");
                Assert.That(firstObjectForLane(host, runtimeFixture.Lane.Identity.Id).ObjectId, Is.EqualTo(1));
                Assert.That(firstObjectForGroup(host, runtimeFixture.Group.Identity.Id).ObjectId, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestNativeGeometrySlotsUsePrebuiltReusableLifecycleAndExactFallbackGate()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createMineFixture(selectedTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;
            var nativeOwner = new Container { RelativeSizeAxes = Axes.Both };
            var programmatic = new Container { Alpha = 0.75f };

            host.ProcessFrame();
            using IDisposable registration = host.RegisterProgrammaticVisual(key, programmatic);
            GameplaySkinSpecialisedSceneVisual? visual = host.PrepareSpecialisedVisual(key, nativeOwner);
            int preparedInstances = host.RuntimeInstanceCount;

            Assert.Multiple(() =>
            {
                Assert.That(visual, Is.Not.Null);
                Assert.That(host.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(gate.IsReplacementReady, Is.True);
                Assert.That(programmatic.Alpha, Is.Zero);
                Assert.That(nativeOwner.Children, Does.Contain(visual));
                Assert.That(visual!.RootDrawables.OfType<Sprite>().Single().Texture, Is.SameAs(selectedTexture));
                Assert.That(visual.Alpha, Is.Zero);
            });

            visual!.OnApply();
            visual.OnFree();
            visual.OnApply();

            long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();

            for (int iteration = 0; iteration < 1024; iteration++)
            {
                visual.OnFree();
                visual.OnApply();
            }

            long lifecycleAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

            Assert.Multiple(() =>
            {
                Assert.That(visual.IsApplied, Is.True);
                Assert.That(visual.Alpha, Is.EqualTo(1));
                Assert.That(host.RuntimeInstanceCount, Is.EqualTo(preparedInstances));
                Assert.That(lifecycleAllocatedBytes, Is.Zero,
                    "A native pool apply/free cycle must not allocate or rebuild its specialised scene visual.");
            });

            visual.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(programmatic.Alpha, Is.EqualTo(0.75f));
                Assert.That(host.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(gate!.IsReplacementReady, Is.False);
                Assert.That(host.RuntimeInstanceCount, Is.Zero);
            });
        }

        [Test]
        public void TestAppliedSpecialisedVisualSwapsAtomicallyOnlyAfterSharedSceneReady()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            using var alternateTexture = new DummyRenderer().CreateTexture(16, 16);
            RuntimeFixture fixture = createSpecialisedAuthorFixture(selectedTexture, alternateTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;
            var nativeOwner = new Container { RelativeSizeAxes = Axes.Both };
            var programmatic = new Container { Alpha = 0.75f };
            using IDisposable registration = host.RegisterProgrammaticVisual(key, programmatic);
            using GameplaySkinSpecialisedSceneVisual visual = host.PrepareSpecialisedVisual(key, nativeOwner)!;

            Assert.That(visual.Alpha, Is.Zero);
            Assert.That(host.IsSceneReady, Is.False);
            visual.OnApply();

            Assert.Multiple(() =>
            {
                Assert.That(host.IsSceneReady, Is.False);
                Assert.That(programmatic.Alpha, Is.EqualTo(0.75f));
                Assert.That(visual.Alpha, Is.Zero,
                    "A native-local author visual cannot appear before the shared exact scene is ready to swap.");
            });

            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.IsSceneReady, Is.True);
                Assert.That(programmatic.Alpha, Is.Zero);
                Assert.That(visual.Alpha, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestSpecialisedPoolCapacityFailureRestoresWholeExactKey()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createMineFixture(selectedTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;
            var nativeOwner = new Container { RelativeSizeAxes = Axes.Both };
            var programmatic = new Container { Alpha = 0.75f };
            var visuals = new List<GameplaySkinSpecialisedSceneVisual>();

            host.ProcessFrame();
            using IDisposable registration = host.RegisterProgrammaticVisual(key, programmatic);
            Assert.That(host.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);

            for (int index = 0; index < gate!.SpecialisedPoolCapacity; index++)
            {
                GameplaySkinSpecialisedSceneVisual? visual = host.PrepareSpecialisedVisual(key, nativeOwner);
                Assert.That(visual, Is.Not.Null);
                visuals.Add(visual!);
            }

            visuals[0].OnApply();
            Assert.That(programmatic.Alpha, Is.Zero);
            Assert.That(host.PrepareSpecialisedVisual(key, nativeOwner), Is.Null);

            Assert.Multiple(() =>
            {
                Assert.That(host.RuntimeFaults.Select(fault => fault.Code), Does.Contain("OMS-SKIN-SCENE-RUNTIME-004"));
                Assert.That(gate.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                Assert.That(gate.IsReplacementReady, Is.False);
                Assert.That(programmatic.Alpha, Is.EqualTo(0.75f));
                Assert.That(visuals.Select(visual => visual.Alpha), Is.All.Zero);
                Assert.That(host.RuntimeNodeCount, Is.Zero);
                Assert.That(host.RuntimeInstanceCount, Is.Zero);
            });

            Assert.DoesNotThrow(() =>
            {
                foreach (GameplaySkinSpecialisedSceneVisual visual in visuals)
                {
                    visual.OnFree();
                    visual.OnApply();
                    visual.Dispose();
                }
            });
        }

        [Test]
        public void TestSpecialisedAuthorSubtreeUsesPreparedResourceTrackAndLaneScopedStateWithoutDuplicateLayerDrawable()
        {
            using var texture1 = new DummyRenderer().CreateTexture(8, 8);
            using var texture2 = new DummyRenderer().CreateTexture(16, 16);
            RuntimeFixture fixture = createSpecialisedAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            var owner = new Container { RelativeSizeAxes = Axes.Both };
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;

            try
            {
                host.ProcessFrame();
                GameplaySkinSpecialisedSceneVisual? visual = host.PrepareSpecialisedVisual(key, owner);

                Assert.Multiple(() =>
                {
                    Assert.That(visual, Is.Not.Null);
                    Assert.That(host.Layers.Object.Children, Is.Empty);
                    Assert.That(visual!.RuntimeNodes.Count, Is.EqualTo(2));
                    Assert.That(visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "native.sprite").ContentDrawable,
                        Is.TypeOf<Sprite>());
                    Assert.That(visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "native.root").TransformDrawable.Alpha,
                        Is.EqualTo(0.25f).Within(0.001f));
                });

                visual!.OnApply();
                publishInput(stream, producer, 10, input(fixture, true));
                producer.SynchroniseTiming(20, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1));
                host.ProcessFrame();

                GameplaySkinSceneRuntimeNode root = visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "native.root");
                var sprite = (Sprite)visual.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "native.sprite").ContentDrawable;

                Assert.Multiple(() =>
                {
                    Assert.That(root.TransformDrawable.Alpha, Is.EqualTo(1));
                    Assert.That(sprite.Texture, Is.SameAs(texture2));
                    Assert.That(host.StateMachineStates["native.machine"], Is.EqualTo("native.pressed"));
                });

                publishInput(stream, producer, 30, input(fixture, false));
                producer.SynchroniseTiming(30, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1));
                host.ProcessFrame();

                Assert.Multiple(() =>
                {
                    Assert.That(root.TransformDrawable.Alpha, Is.EqualTo(0.25f).Within(0.001f));
                    Assert.That(host.StateMachineStates["native.machine"], Is.EqualTo("native.idle"));
                });

                visual.OnFree();
                visual.OnApply();
                host.ProcessFrame();

                Assert.Multiple(() =>
                {
                    Assert.That(root.TransformDrawable.Alpha, Is.EqualTo(0.25f).Within(0.001f),
                        "A pooled reapply must rebuild current state-machine input rather than retain the prior apply.");
                    Assert.That(sprite.Texture, Is.SameAs(texture2),
                        "A pooled reapply keeps the one authoritative global track phase; it never starts a local clock.");
                });

                visual.Dispose();
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestPooledSpecialisedClonesConsumeOnlyTheirExactBoundObjectEvents()
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createObjectScopedSpecialisedFixture(texture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            var firstOwner = new Container { RelativeSizeAxes = Axes.Both };
            var secondOwner = new Container { RelativeSizeAxes = Axes.Both };
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;

            host.ProcessFrame();
            using GameplaySkinSpecialisedSceneVisual first = host.PrepareSpecialisedVisual(key, firstOwner)!;
            using GameplaySkinSpecialisedSceneVisual second = host.PrepareSpecialisedVisual(key, secondOwner)!;
            first.OnApply(101);
            second.OnApply(202);

            GameplaySkinSceneRuntimeNode firstRoot = first.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "object.root");
            GameplaySkinSceneRuntimeNode secondRoot = second.RuntimeNodes.Single(node => node.PreparedNode.Source.Id == "object.root");
            publishObject(stream, producer, 10, GameplaySkinEventKind.ObjectSpawned, objectState(fixture, 101, GameplaySkinObjectState.Visible));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(first.BoundObjectId, Is.EqualTo(101));
                Assert.That(second.BoundObjectId, Is.EqualTo(202));
                Assert.That(firstRoot.TransformDrawable.Alpha, Is.EqualTo(0.9f).Within(0.001f));
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.1f).Within(0.001f),
                    "A same-lane pooled clone must not consume another object's spawn edge.");
            });

            publishObject(stream, producer, 20, GameplaySkinEventKind.ObjectSpawned, objectState(fixture, 202, GameplaySkinObjectState.Visible));
            publishJudgement(stream, producer, 30, new GameplaySkinJudgementStateSnapshot(
                101,
                fixture.Group.Identity.Id,
                fixture.Lane.Identity.Id,
                GameplaySkinJudgementGrade.Great,
                -3,
                0.01));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(firstRoot.TransformDrawable.Alpha, Is.EqualTo(0.5f).Within(0.001f));
                Assert.That(firstRoot.TransformDrawable.Rotation, Is.EqualTo(-3).Within(0.001f));
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.9f).Within(0.001f),
                    "A same-lane pooled clone must not consume another object's judgement edge.");
                Assert.That(secondRoot.TransformDrawable.Rotation, Is.Zero.Within(0.001f),
                    "A bound clone without an exact judgement must not fall through to the lane's latest result binding.");
            });

            publishObject(stream, producer, 40, GameplaySkinEventKind.ObjectDespawned, objectState(fixture, 101, GameplaySkinObjectState.Despawned));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(firstRoot.TransformDrawable.Alpha, Is.EqualTo(0.1f).Within(0.001f));
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.9f).Within(0.001f));
            });

            first.OnFree();
            first.OnApply(303);
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(firstRoot.TransformDrawable.Alpha, Is.EqualTo(0.1f).Within(0.001f));
                Assert.That(firstRoot.TransformDrawable.Rotation, Is.Zero.Within(0.001f),
                    "A reused pooled clone must not retain the previous object's judgement binding.");
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.9f).Within(0.001f));
            });
        }

        [Test]
        public void TestBgaSpecialisedStateMachinesRebuildFromExactViewportSnapshot()
        {
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createBgaScopedSpecialisedFixture(texture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;
            var firstOwner = new Container { RelativeSizeAxes = Axes.Both };
            var secondOwner = new Container { RelativeSizeAxes = Axes.Both };

            host.ProcessFrame();
            using GameplaySkinSpecialisedSceneVisual first = host.PrepareSpecialisedVisual(key, firstOwner, 0)!;
            using GameplaySkinSpecialisedSceneVisual second = host.PrepareSpecialisedVisual(key, secondOwner, 1)!;
            first.OnApply();
            second.OnApply();
            GameplaySkinSceneRuntimeNode firstRoot = first.RuntimeNodes.Single();
            GameplaySkinSceneRuntimeNode secondRoot = second.RuntimeNodes.Single();

            Assert.Multiple(() =>
            {
                Assert.That(firstRoot.TransformDrawable.Alpha, Is.EqualTo(0.1f).Within(0.001f));
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.1f).Within(0.001f));
            });

            GameplaySkinLayoutRect firstViewport = fixture.Publication.Snapshot.BgaViewports[0];
            publishBga(stream, producer, 10, GameplaySkinEventKind.BgaContentStateChanged,
                new GameplaySkinBgaStateSnapshot(0, firstViewport, GameplaySkinBgaContentState.Playing, 1));
            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(firstRoot.TransformDrawable.Alpha, Is.EqualTo(0.2f).Within(0.001f));
                Assert.That(secondRoot.TransformDrawable.Alpha, Is.EqualTo(0.1f).Within(0.001f),
                    "An empty adjacent viewport must not receive another viewport's snapshot-projectable BGA state.");
            });
        }

        [TestCase("object.note")]
        [TestCase("object.long-note.head")]
        [TestCase("object.long-note.body")]
        [TestCase("object.long-note.tail")]
        [TestCase("playfield.key")]
        [TestCase("effect.hit-explosion")]
        [TestCase("object.mine")]
        [TestCase("playfield.bar-line")]
        public void TestEveryNativeGeometrySlotHasOneReusableSpecialisedFactoryRoute(string slotId)
        {
            Assert.That(GameplaySkinSlotCatalog.TryGet(slotId, out GameplaySkinSlotDescriptor? slot), Is.True);
            using var texture = new DummyRenderer().CreateTexture(8, 8);
            LayoutFixture layout = createLayout(ReferenceEquals(slot, GameplaySkinSlotCatalog.Mine) ? "bms" : "mania");
            GameplaySkinResolvedMaterialTarget target = ReferenceEquals(slot, GameplaySkinSlotCatalog.BarLine)
                ? GameplaySkinResolvedMaterialTarget.ForGroup(layout.Group)
                : GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            RuntimeFixture runtimeFixture = fixture(layout, new[] { provide(slot!, target, texture, source()) });
            using GameplaySkinEventStream stream = createStream(runtimeFixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(runtimeFixture.Publication, stream);
            var nativeOwner = new Container { RelativeSizeAxes = Axes.Both };
            GameplaySkinResolvedMaterialKey key = runtimeFixture.Publication.MaterialSet.Entries.Single().Key;

            host.ProcessFrame();
            using GameplaySkinSpecialisedSceneVisual? visual = host.PrepareSpecialisedVisual(key, nativeOwner);

            Assert.Multiple(() =>
            {
                Assert.That(visual, Is.Not.Null);
                Assert.That(host.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Specialised));
                Assert.That(gate.IsReplacementReady, Is.True);
                Assert.That(nativeOwner.Children.Count, Is.EqualTo(1));
                Assert.That(visual!.RootDrawables.OfType<Sprite>().Single().Texture, Is.SameAs(texture));
            });
        }

        [Test]
        public void TestSingleNodeFaultFallsBackLocallyWithoutBreakingOtherSceneNodes()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createFaultedAuthorFixture(selectedTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            host.ProcessFrame();

            GameplaySkinResolvedMaterialKey background = fixture.Publication.MaterialSet.Entries.Single().Key;

            Assert.Multiple(() =>
            {
                Assert.That(host.RuntimeFaults.Select(fault => fault.Code), Does.Contain("OMS-SKIN-SCENE-RUNTIME-001"));
                Assert.That(host.TryGetRuntimeNode("node.invalid", out _), Is.False);
                Assert.That(host.TryGetRuntimeNode("node.valid", out _), Is.True);
                Assert.That(host.TryGetHostedDrawable(background, out Drawable? fallback), Is.True);
                Assert.That(((Container)fallback!).Children.OfType<Sprite>().Single().Texture, Is.SameAs(selectedTexture));
            });
        }

        [Test]
        public void TestRuntimeProgramFaultRetiresTheWholeExactOwnerWithoutBreakingSiblingTrees()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createRuntimeProgramFaultFixture(selectedTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            host.ProcessFrame();

            GameplaySkinResolvedMaterialKey background = fixture.Publication.MaterialSet.Entries.Single().Key;

            Assert.Multiple(() =>
            {
                Assert.That(host.RuntimeFaults.Select(fault => fault.Code), Does.Contain("OMS-SKIN-SCENE-RUNTIME-006"));
                Assert.That(host.TryGetRuntimeNode("runtime-fault.owner", out _), Is.False);
                Assert.That(host.TryGetRuntimeNode("runtime-fault.child", out _), Is.False,
                    "A descendant program fault must retire the whole exact slot owner transactionally.");
                Assert.That(host.TryGetRuntimeNode("runtime-fault.sibling", out _), Is.True);
                Assert.That(host.TryGetHostedDrawable(background, out Drawable? fallback), Is.True);
                Assert.That(((Container)fallback!).Children.OfType<Sprite>().Single().Texture, Is.SameAs(selectedTexture));
            });
        }

        [Test]
        public void TestSpecialisedRuntimeFaultFailsWholeExactKeyAndKeepsPoolLifecycleSafe()
        {
            using var selectedTexture = new DummyRenderer().CreateTexture(8, 8);
            RuntimeFixture fixture = createSpecialisedRuntimeFaultFixture(selectedTexture);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);
            GameplaySkinResolvedMaterialKey key = fixture.Publication.MaterialSet.Entries.Single().Key;
            var firstOwner = new Container { RelativeSizeAxes = Axes.Both };
            var secondOwner = new Container { RelativeSizeAxes = Axes.Both };
            var firstProgrammatic = new Container { Alpha = 0.75f };
            var secondProgrammatic = new Container { Alpha = 0.5f };

            host.ProcessFrame();
            using IDisposable firstRegistration = host.RegisterProgrammaticVisual(key, firstProgrammatic);
            using IDisposable secondRegistration = host.RegisterProgrammaticVisual(key, secondProgrammatic);
            using GameplaySkinSpecialisedSceneVisual first = host.PrepareSpecialisedVisual(key, firstOwner)!;
            using GameplaySkinSpecialisedSceneVisual second = host.PrepareSpecialisedVisual(key, secondOwner)!;
            first.OnApply(101);
            second.OnApply(202);

            Assert.Multiple(() =>
            {
                Assert.That(firstProgrammatic.Alpha, Is.Zero);
                Assert.That(secondProgrammatic.Alpha, Is.Zero);
                Assert.That(host.RuntimeNodeCount, Is.EqualTo(2));
            });

            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.RuntimeFaults.Select(fault => fault.Code), Does.Contain("OMS-SKIN-SCENE-RUNTIME-006"));
                Assert.That(host.TryGetVisualGate(key, out GameplaySkinSceneHostedSlot? gate), Is.True);
                Assert.That(gate!.Route, Is.EqualTo(GameplaySkinSceneHostRoute.Programmatic));
                Assert.That(gate.IsReplacementReady, Is.False);
                Assert.That(firstProgrammatic.Alpha, Is.EqualTo(0.75f));
                Assert.That(secondProgrammatic.Alpha, Is.EqualTo(0.5f));
                Assert.That(first.Alpha, Is.Zero);
                Assert.That(second.Alpha, Is.Zero);
                Assert.That(first.IsApplied, Is.False);
                Assert.That(second.IsApplied, Is.False);
                Assert.That(host.RuntimeNodeCount, Is.Zero);
                Assert.That(host.RuntimeInstanceCount, Is.Zero);
                Assert.That(host.PrepareSpecialisedVisual(key, new Container()), Is.Null,
                    "A failed exact key must never rebuild a later pool clone from the same publication.");
            });

            Assert.DoesNotThrow(() =>
            {
                first.OnFree();
                first.OnApply(303);
                second.OnFree();
                second.OnApply();
            }, "Engine-owned pools may still free or reapply handles retained before the exact key failed.");

            Assert.Multiple(() =>
            {
                Assert.That(first.Alpha, Is.Zero);
                Assert.That(second.Alpha, Is.Zero);
                Assert.That(first.BoundObjectId, Is.Null);
                Assert.That(second.BoundObjectId, Is.Null);
            });
        }

        [Test]
        public void TestContinuousTimingBindingReadsOnlyTheAuthoritativeStreamSample()
        {
            using var texture1 = new DummyRenderer().CreateTexture(2, 2);
            using var texture2 = new DummyRenderer().CreateTexture(4, 4);
            RuntimeFixture fixture = createAuthorFixture(texture1, texture2);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();
            producer.SynchroniseTiming(100, new GameplaySkinTimingStateSnapshot(0.625, 0, 120, false, 1));
            host.ProcessFrame();

            Assert.That(host.TryGetRuntimeNode("node.mask", out GameplaySkinSceneRuntimeNode? mask), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(mask!.ContentDrawable.Rotation, Is.EqualTo(0.625f).Within(0.0001f));
                Assert.That(host.LastSequence, Is.EqualTo(0), "continuous timing samples must not manufacture edge envelopes");
                Assert.That(host.CurrentEpoch, Is.Zero);
            });
        }

        [Test]
        public void TestDynamicTextGlyphReservationsAreRejectedBeforeRuntimeAdmission()
        {
            int admittedTextNodes = GameplaySkinPreparedSceneBudgets.MAX_RUNTIME_TEXT_GLYPHS
                                    / GameplaySkinPreparedSceneBudgets.MAX_DYNAMIC_TEXT_GLYPHS_PER_NODE;
            GameplaySkinScenePreparationException? exception = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                createDynamicTextBudgetFixture(admittedTextNodes + 1));
            Assert.That(exception!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));

            RuntimeFixture fixture = createDynamicTextBudgetFixture(admittedTextNodes);
            using GameplaySkinEventStream stream = createStream(fixture.Publication);
            using var host = new GameplaySkinSceneRuntimeHost(fixture.Publication, stream);

            host.ProcessFrame();

            Assert.Multiple(() =>
            {
                Assert.That(host.PendingCreationCount, Is.Zero);
                Assert.That(host.RuntimeNodeCount, Is.EqualTo(admittedTextNodes + 1), "root plus the bounded text nodes are admitted");
                Assert.That(host.RuntimeFaults, Is.Empty);
                Assert.That(fixture.Publication.PreparedScene.ReservedTextGlyphs,
                    Is.EqualTo(admittedTextNodes * GameplaySkinPreparedSceneBudgets.MAX_DYNAMIC_TEXT_GLYPHS_PER_NODE));
                Assert.That(Enumerable.Range(0, admittedTextNodes).All(index =>
                        host.TryGetRuntimeNode($"text.{index}", out GameplaySkinSceneRuntimeNode? node)
                        && node!.ContentDrawable is SpriteText text
                        && text.Text.ToString() == "0"),
                    Is.True);
            });
        }

        [Test]
        public void TestRuntimePublicApiCannotReadPackageOrPublishEvents()
        {
            Type type = typeof(GameplaySkinSceneRuntimeHost);
            string[] forbidden = { "ISkin", "Codec", "File", "Directory", "StreamProducer", "Publish", "Reset" };

            Assert.Multiple(() =>
            {
                Assert.That(type.GetConstructors().SelectMany(constructor => constructor.GetParameters())
                                .Select(parameter => parameter.ParameterType.Name),
                    Has.None.Matches<string>(name => forbidden.Any(token => name.Contains(token, StringComparison.Ordinal))));
                Assert.That(type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
                                .Select(method => method.Name),
                    Has.None.Matches<string>(name => forbidden.Any(token => name.Contains(token, StringComparison.Ordinal))));
                Assert.That(type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
                                .Select(field => field.FieldType.Name),
                    Has.None.Matches<string>(name => name.Contains("Codec", StringComparison.Ordinal)
                                                     || name.Contains("ISkin", StringComparison.Ordinal)));
            });
        }

        private static RuntimeFixture createLifecycleReplayFixture()
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(
                layout.Snapshot,
                Array.Empty<GameplaySkinResolvedMaterialEntry>());
            GameplaySkinSceneNode root = node(
                "lifecycle.root",
                GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global),
                null,
                null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["opacity"] = GameplaySkinScenePropertyValue.FromNumber(0.1),
                });
            var machine = new GameplaySkinSceneStateMachine(
                "lifecycle.machine",
                "lifecycle.detached",
                new[]
                {
                    state("lifecycle.detached", 0.1),
                    state("lifecycle.attached", 0.2),
                    state("lifecycle.loaded", 0.3),
                    state("lifecycle.running", 0.4),
                    state("lifecycle.paused", 0.5),
                    state("lifecycle.completed", 0.6),
                    state("lifecycle.failed", 0.7),
                },
                new[]
                {
                    transition("attach", "lifecycle.detached", "lifecycle.attached", "gameplay.attach"),
                    transition("loaded", "lifecycle.attached", "lifecycle.loaded", "gameplay.loaded"),
                    transition("start", "lifecycle.loaded", "lifecycle.running", "gameplay.start"),
                    transition("pause", "lifecycle.running", "lifecycle.paused", "gameplay.pause"),
                    transition("complete", "lifecycle.running", "lifecycle.completed", "gameplay.complete"),
                    transition("fail", "lifecycle.running", "lifecycle.failed", "gameplay.fail"),
                });
            var document = new GameplaySkinSceneDocument(
                root,
                Array.Empty<GameplaySkinSceneTrack>(),
                new[] { machine },
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "lifecycle-replay-scene",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[]
                {
                    prepared(
                        root,
                        layout.Snapshot.Context.SafeBounds,
                        GameplaySkinResolvedMaterialTarget.Global,
                        null,
                        null),
                });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);

            GameplaySkinSceneState state(string id, double opacity)
                => new GameplaySkinSceneState(id, new[]
                {
                    new GameplaySkinSceneStateAssignment(
                        $"{id}.opacity",
                        root.Id,
                        "opacity",
                        GameplaySkinScenePropertyValue.FromNumber(opacity)),
                });

            static GameplaySkinSceneTransition transition(string id, string from, string to, string eventId)
                => new GameplaySkinSceneTransition($"lifecycle.{id}", from, to, eventId);
        }

        private static DualJudgementFixture createDualJudgementFixture(Texture texture)
        {
            DualLayoutFixture layout = createDualStageLayout();
            GameplaySkinLaneTopologyGroup firstGroup = layout.Groups[0].TopologyGroup;
            GameplaySkinLaneTopologyGroup secondGroup = layout.Groups[1].TopologyGroup;
            GameplaySkinLaneTopologyEntry firstLane = firstGroup.LanesInLogicalOrder.Single();
            GameplaySkinLaneTopologyEntry secondLane = secondGroup.LanesInLogicalOrder.Single();
            GameplaySkinResolvedMaterialTarget firstTarget = GameplaySkinResolvedMaterialTarget.ForLane(firstGroup, firstLane);
            GameplaySkinResolvedMaterialTarget secondTarget = GameplaySkinResolvedMaterialTarget.ForLane(secondGroup, secondLane);
            GameplaySkinResolvedMaterialEntry[] entries =
            {
                provide(GameplaySkinSlotCatalog.KeyFlash, firstTarget, texture, source()),
                provide(GameplaySkinSlotCatalog.KeyFlash, secondTarget, texture, source()),
            };
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, entries);
            var resource = new GameplaySkinSceneResource("scope.texture", GameplaySkinSceneResourceType.Texture, "textures/scope.png");
            var preparedResource = new GameplaySkinPreparedSceneResource(resource, "scope", 16, 16, texture);
            GameplaySkinSceneNode dispatcher = node(
                "scope.root",
                GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global),
                null,
                null);
            GameplaySkinSceneNode first = node(
                "scope.first",
                GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Lane, firstLane.Identity.Id.Value, firstLane.GlobalLogicalIndex),
                GameplaySkinSlotCatalog.KeyFlash.Id,
                resource.Id);
            GameplaySkinSceneNode second = node(
                "scope.second",
                GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Lane, secondLane.Identity.Id.Value, secondLane.GlobalLogicalIndex),
                GameplaySkinSlotCatalog.KeyFlash.Id,
                resource.Id);
            dispatcher = withChildren(dispatcher, first, second);

            GameplaySkinSceneState state(string id, double opacity)
                => new GameplaySkinSceneState(id, new[]
                {
                    new GameplaySkinSceneStateAssignment($"{id}.first", first.Id, "opacity", GameplaySkinScenePropertyValue.FromNumber(opacity)),
                    new GameplaySkinSceneStateAssignment($"{id}.second", second.Id, "opacity", GameplaySkinScenePropertyValue.FromNumber(opacity)),
                });

            var machine = new GameplaySkinSceneStateMachine(
                "scope.machine",
                "scope.idle",
                new[] { state("scope.idle", 0.1), state("scope.judged", 0.9) },
                new[]
                {
                    new GameplaySkinSceneTransition("scope.hit", "scope.idle", "scope.judged", "judgement.hit"),
                });
            var document = new GameplaySkinSceneDocument(
                dispatcher,
                Array.Empty<GameplaySkinSceneTrack>(),
                new[] { machine },
                new[]
                {
                    new GameplaySkinSceneBinding("scope.first-offset", first.Id, "rotation", "judgement.offset"),
                    new GameplaySkinSceneBinding("scope.second-offset", second.Id, "rotation", "judgement.offset"),
                },
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            GameplaySkinPreparedSceneNode preparedRoot = prepared(
                dispatcher,
                layout.Snapshot.Context.SafeBounds,
                GameplaySkinResolvedMaterialTarget.Global,
                null,
                null,
                prepared(first, layout.Groups[0].Rect, firstTarget, GameplaySkinSlotCatalog.KeyFlash, preparedResource),
                prepared(second, layout.Groups[1].Rect, secondTarget, GameplaySkinSlotCatalog.KeyFlash, preparedResource));
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "scope-scene",
                new GameplaySkinSceneManifest(new[] { resource }),
                document,
                new[] { preparedResource },
                new[] { preparedRoot });
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene);
            return new DualJudgementFixture(publication, firstGroup, firstLane, secondGroup, secondLane);
        }

        private static CompiledProgramFixture createCompiledProgramFixture(
            Texture texture1,
            Texture texture2,
            string? invalidReference = null)
        {
            const string missing = "texture.missing";

            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialEntry entry = provide(GameplaySkinSlotCatalog.StageBackground, stage, texture1, source());
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[] { entry });
            var resource1 = new GameplaySkinSceneResource("compiled.texture-one", GameplaySkinSceneResourceType.Texture, "textures/compiled-one.png");
            var resource2 = new GameplaySkinSceneResource("compiled.texture-two", GameplaySkinSceneResourceType.Texture, "textures/compiled-two.png");
            var preparedResource1 = new GameplaySkinPreparedSceneResource(resource1, "compiled-one", 16, 16, texture1);
            var preparedResource2 = new GameplaySkinPreparedSceneResource(
                resource2,
                "compiled-two",
                64,
                64,
                invalidReference == "unprepared-texture" ? null : texture2);
            GameplaySkinSceneTarget stageTarget = target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, 0);
            GameplaySkinSceneNode sprite = node(
                "compiled.sprite",
                GameplaySkinSceneNodeType.Sprite,
                stageTarget,
                GameplaySkinSlotCatalog.StageBackground.Id,
                resource1.Id);
            string trackResource = invalidReference == "track" ? missing : resource1.Id;
            string stateResource = invalidReference == "state" ? missing : resource2.Id;
            string defaultResource = invalidReference == "variant-default" ? missing : resource1.Id;
            string caseResource = invalidReference == "variant-case" ? missing : resource2.Id;
            var document = new GameplaySkinSceneDocument(
                sprite,
                new[]
                {
                    new GameplaySkinSceneTrack(
                        "compiled.track",
                        GameplaySkinSceneTrackType.Frame,
                        sprite.Id,
                        "resource",
                        GameplaySkinSceneEasing.Step,
                        false,
                        new[]
                        {
                            new GameplaySkinSceneKeyframe("compiled.frame-one", 0, GameplaySkinScenePropertyValue.FromString(trackResource)),
                            new GameplaySkinSceneKeyframe("compiled.frame-two", 16, GameplaySkinScenePropertyValue.FromString(resource2.Id)),
                        }),
                },
                new[]
                {
                    new GameplaySkinSceneStateMachine(
                        "compiled.machine",
                        "compiled.state",
                        new[]
                        {
                            new GameplaySkinSceneState("compiled.state", new[]
                            {
                                new GameplaySkinSceneStateAssignment(
                                    "compiled.assignment",
                                    sprite.Id,
                                    "resource",
                                    GameplaySkinScenePropertyValue.FromString(stateResource)),
                            }),
                        },
                        Array.Empty<GameplaySkinSceneTransition>()),
                },
                new[]
                {
                    new GameplaySkinSceneBinding("compiled.binding", sprite.Id, "opacity", "gauge.value"),
                },
                new[]
                {
                    new GameplaySkinSceneVariant(
                        "compiled.variant",
                        sprite.Id,
                        "object.state",
                        defaultResource,
                        new[]
                        {
                            new GameplaySkinSceneVariantCase("compiled.variant-visible", "visible", caseResource),
                        }),
                },
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            GameplaySkinPreparedSceneNode preparedSprite = prepared(
                sprite,
                layout.StageRect,
                stage,
                GameplaySkinSlotCatalog.StageBackground,
                preparedResource1);
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "compiled-program",
                new GameplaySkinSceneManifest(new[] { resource1, resource2 }),
                document,
                new[] { preparedResource1, preparedResource2 },
                new[] { preparedSprite });
            return new CompiledProgramFixture(scene, preparedResource1, preparedResource2);
        }

        private static RuntimeFixture createAuthorFixture(Texture texture1, Texture texture2)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget stageTarget = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialTarget globalTarget = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialSourceIdentity sourceIdentity = source();
            GameplaySkinResolvedMaterialEntry[] entries =
            {
                provide(GameplaySkinSlotCatalog.StageBackground, stageTarget, texture1, sourceIdentity),
                GameplaySkinResolvedMaterialEntry.Provide(
                    GameplaySkinSlotCatalog.TextHud,
                    globalTarget,
                    sourceIdentity,
                    GameplaySkinPublicSlotMaterial.CreateProgrammaticFallback(GameplaySkinSlotCatalog.TextHud)),
            };
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, entries);
            var resource1 = new GameplaySkinSceneResource("texture.one", GameplaySkinSceneResourceType.Texture, "textures/one.png");
            var resource2 = new GameplaySkinSceneResource("texture.two", GameplaySkinSceneResourceType.Texture, "textures/two.png");
            var preparedResource1 = new GameplaySkinPreparedSceneResource(resource1, "one", 16, 16, texture1);
            var preparedResource2 = new GameplaySkinPreparedSceneResource(resource2, "two", 64, 64, texture2);

            GameplaySkinSceneNode root = node("node.root", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null);
            GameplaySkinSceneNode sprite = node(
                "node.sprite",
                GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, 0),
                GameplaySkinSlotCatalog.StageBackground.Id,
                "texture.one",
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["opacity"] = GameplaySkinScenePropertyValue.FromNumber(0.25),
                },
                new[]
                {
                    new GameplaySkinSceneEffect(
                        "effect.glow",
                        "glow",
                        new Dictionary<string, GameplaySkinScenePropertyValue>
                        {
                            ["radius"] = GameplaySkinScenePropertyValue.FromNumber(2),
                            ["strength"] = GameplaySkinScenePropertyValue.FromNumber(1),
                            ["colour"] = GameplaySkinScenePropertyValue.FromString("#ffffffff"),
                        }),
                },
                blend: GameplaySkinSceneBlendMode.Additive);
            GameplaySkinSceneNode text = node(
                "node.text",
                GameplaySkinSceneNodeType.Text,
                target(GameplaySkinSceneTargetKind.Global),
                GameplaySkinSlotCatalog.TextHud.Id,
                null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["text"] = GameplaySkinScenePropertyValue.FromString("0"),
                    ["font-size"] = GameplaySkinScenePropertyValue.FromNumber(20),
                });
            GameplaySkinSceneNode mask = node(
                "node.mask",
                GameplaySkinSceneNodeType.Mask,
                target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, 0),
                null,
                null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["mask-mode"] = GameplaySkinScenePropertyValue.FromString("ellipse"),
                },
                blend: GameplaySkinSceneBlendMode.Multiply);
            GameplaySkinSceneNode clip = node(
                "node.clip",
                GameplaySkinSceneNodeType.Clip,
                target(GameplaySkinSceneTargetKind.Global),
                null,
                null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["clip-mode"] = GameplaySkinScenePropertyValue.FromString("bounds"),
                },
                blend: GameplaySkinSceneBlendMode.Screen);
            root = withChildren(root, sprite, text, mask, clip);
            GameplaySkinSceneNode templateRoot = node(
                "template-node.root",
                GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0),
                null,
                null);
            var document = new GameplaySkinSceneDocument(
                root,
                new[]
                {
                    new GameplaySkinSceneTrack(
                        "track.frame",
                        GameplaySkinSceneTrackType.Frame,
                        "node.sprite",
                        "resource",
                        GameplaySkinSceneEasing.Step,
                        false,
                        new[]
                        {
                            new GameplaySkinSceneKeyframe("keyframe.one", 0, GameplaySkinScenePropertyValue.FromString("texture.one")),
                            new GameplaySkinSceneKeyframe("keyframe.two", 16, GameplaySkinScenePropertyValue.FromString("texture.two")),
                        }),
                    new GameplaySkinSceneTrack(
                        "track.opacity",
                        GameplaySkinSceneTrackType.Tween,
                        "node.sprite",
                        "opacity",
                        GameplaySkinSceneEasing.Linear,
                        false,
                        new[]
                        {
                            new GameplaySkinSceneKeyframe("keyframe.opacity-one", 0, GameplaySkinScenePropertyValue.FromNumber(0.25)),
                            new GameplaySkinSceneKeyframe("keyframe.opacity-two", 100, GameplaySkinScenePropertyValue.FromNumber(1)),
                        }),
                },
                new[]
                {
                    new GameplaySkinSceneStateMachine(
                        "machine.key",
                        "state.idle",
                        new[]
                        {
                            new GameplaySkinSceneState("state.idle", new[]
                            {
                                new GameplaySkinSceneStateAssignment(
                                    "assignment.idle-opacity",
                                    "node.sprite",
                                    "opacity",
                                    GameplaySkinScenePropertyValue.FromNumber(0.25)),
                            }),
                            new GameplaySkinSceneState("state.pressed", new[]
                            {
                                new GameplaySkinSceneStateAssignment(
                                    "assignment.pressed-opacity",
                                    "node.sprite",
                                    "opacity",
                                    GameplaySkinScenePropertyValue.FromNumber(1)),
                            }),
                            new GameplaySkinSceneState("state.failed", new[]
                            {
                                new GameplaySkinSceneStateAssignment(
                                    "assignment.failed-opacity",
                                    "node.sprite",
                                    "opacity",
                                    GameplaySkinScenePropertyValue.FromNumber(0.5)),
                            }),
                        },
                        new[]
                        {
                            new GameplaySkinSceneTransition("transition.press", "state.idle", "state.pressed", "input.key.down"),
                            new GameplaySkinSceneTransition("transition.release", "state.pressed", "state.idle", "input.key.up"),
                            new GameplaySkinSceneTransition("transition.loaded", "state.idle", "state.pressed", "gameplay.loaded"),
                            new GameplaySkinSceneTransition("transition.failed", "state.idle", "state.failed", "gameplay.fail"),
                        }),
                },
                new[]
                {
                    new GameplaySkinSceneBinding("binding.combo", "node.text", "text", "combo.value"),
                    new GameplaySkinSceneBinding("binding.timing", "node.mask", "rotation", "timing.beat"),
                },
                new[] { new GameplaySkinSceneTemplate("template.lane", templateRoot) },
                new[]
                {
                    new GameplaySkinSceneInstance(
                        "instance.lane-1",
                        "template.lane",
                        target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0)),
                });
            var preparedRoot = prepared(
                root,
                layout.Snapshot.Context.SafeBounds,
                globalTarget,
                null,
                null,
                prepared(sprite, layout.StageRect, stageTarget, GameplaySkinSlotCatalog.StageBackground, preparedResource1),
                prepared(text, layout.Snapshot.Context.SafeBounds, globalTarget, GameplaySkinSlotCatalog.TextHud, null),
                prepared(mask, layout.StageRect, stageTarget, null, null),
                prepared(clip, layout.Snapshot.Context.SafeBounds, globalTarget, null, null));
            GameplaySkinPreparedSceneNode preparedTemplate = prepared(
                templateRoot,
                layout.LaneRect,
                GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane),
                null,
                null,
                instanceId: "instance.lane-1/template-node.root");
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "scene-one",
                new GameplaySkinSceneManifest(new[] { resource1, resource2 }),
                document,
                new[] { preparedResource1, preparedResource2 },
                new[] { preparedRoot, preparedTemplate });
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene);
            return new RuntimeFixture(publication, layout.Group, layout.Lane, layout.StageRect, layout.LaneRect);
        }

        private static RuntimeFixture createMaterialFixture(Texture texture)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialSourceIdentity sourceIdentity = source();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialTarget lane = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialEntry[] entries =
            {
                provide(GameplaySkinSlotCatalog.StageBackground, stage, texture, sourceIdentity),
                GameplaySkinResolvedMaterialEntry.Provide(
                    GameplaySkinSlotCatalog.Note,
                    lane,
                    sourceIdentity,
                    new SpecialisedMaterial("note")),
                GameplaySkinResolvedMaterialEntry.Suppress(
                    GameplaySkinSlotCatalog.Decoration,
                    GameplaySkinResolvedMaterialTarget.Global,
                    sourceIdentity),
                GameplaySkinResolvedMaterialEntry.Provide(
                    GameplaySkinSlotCatalog.LaneSurface,
                    lane,
                    sourceIdentity,
                    GameplaySkinPublicSlotMaterial.CreateProgrammaticFallback(GameplaySkinSlotCatalog.LaneSurface)),
            };
            return fixture(layout, entries);
        }

        private static RuntimeFixture createMineFixture(Texture texture)
        {
            LayoutFixture layout = createLayout("bms");
            GameplaySkinResolvedMaterialEntry entry = provide(
                GameplaySkinSlotCatalog.Mine,
                GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane),
                texture,
                source());
            return fixture(layout, new[] { entry });
        }

        private static RuntimeFixture createSpecialisedAuthorFixture(Texture texture1, Texture texture2)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialEntry entry = provide(GameplaySkinSlotCatalog.Note, laneTarget, texture1, source());
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[] { entry });
            var resource1 = new GameplaySkinSceneResource("native.texture-one", GameplaySkinSceneResourceType.Texture, "textures/native-one.png");
            var resource2 = new GameplaySkinSceneResource("native.texture-two", GameplaySkinSceneResourceType.Texture, "textures/native-two.png");
            var preparedResource1 = new GameplaySkinPreparedSceneResource(resource1, "native-one", 16, 16, texture1);
            var preparedResource2 = new GameplaySkinPreparedSceneResource(resource2, "native-two", 64, 64, texture2);
            GameplaySkinSceneTarget lane = target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0);
            GameplaySkinSceneNode sprite = node(
                "native.sprite",
                GameplaySkinSceneNodeType.Sprite,
                lane,
                null,
                resource1.Id);
            GameplaySkinSceneNode root = node(
                "native.root",
                GameplaySkinSceneNodeType.Container,
                lane,
                GameplaySkinSlotCatalog.Note.Id,
                null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["opacity"] = GameplaySkinScenePropertyValue.FromNumber(0.25),
                });
            root = withChildren(root, sprite);
            var document = new GameplaySkinSceneDocument(
                root,
                new[]
                {
                    new GameplaySkinSceneTrack(
                        "native.track",
                        GameplaySkinSceneTrackType.Frame,
                        sprite.Id,
                        "resource",
                        GameplaySkinSceneEasing.Step,
                        false,
                        new[]
                        {
                            new GameplaySkinSceneKeyframe("native.frame-one", 0, GameplaySkinScenePropertyValue.FromString(resource1.Id)),
                            new GameplaySkinSceneKeyframe("native.frame-two", 16, GameplaySkinScenePropertyValue.FromString(resource2.Id)),
                        }),
                },
                new[]
                {
                    new GameplaySkinSceneStateMachine(
                        "native.machine",
                        "native.idle",
                        new[]
                        {
                            new GameplaySkinSceneState("native.idle", new[]
                            {
                                new GameplaySkinSceneStateAssignment(
                                    "native.set-idle",
                                    root.Id,
                                    "opacity",
                                    GameplaySkinScenePropertyValue.FromNumber(0.25)),
                            }),
                            new GameplaySkinSceneState("native.pressed", new[]
                            {
                                new GameplaySkinSceneStateAssignment(
                                    "native.set-pressed",
                                    root.Id,
                                    "opacity",
                                    GameplaySkinScenePropertyValue.FromNumber(1)),
                            }),
                        },
                        new[]
                        {
                            new GameplaySkinSceneTransition("native.press", "native.idle", "native.pressed", "input.key.down"),
                            new GameplaySkinSceneTransition("native.release", "native.pressed", "native.idle", "input.key.up"),
                        }),
                },
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            GameplaySkinPreparedSceneNode preparedSprite = prepared(sprite, layout.LaneRect, laneTarget, null, preparedResource1);
            GameplaySkinPreparedSceneNode preparedRoot = prepared(
                root,
                layout.LaneRect,
                laneTarget,
                GameplaySkinSlotCatalog.Note,
                null,
                preparedSprite);
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "native-scene",
                new GameplaySkinSceneManifest(new[] { resource1, resource2 }),
                document,
                new[] { preparedResource1, preparedResource2 },
                new[] { preparedRoot });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);
        }

        private static RuntimeFixture createObjectScopedSpecialisedFixture(Texture texture)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[]
            {
                provide(GameplaySkinSlotCatalog.Note, laneTarget, texture, source()),
            });
            GameplaySkinSceneTarget lane = target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0);
            GameplaySkinSceneNode root = node(
                "object.root",
                GameplaySkinSceneNodeType.Sprite,
                lane,
                GameplaySkinSlotCatalog.Note.Id,
                null,
                new Dictionary<string, GameplaySkinScenePropertyValue>
                {
                    ["opacity"] = GameplaySkinScenePropertyValue.FromNumber(0.1),
                });
            var machine = new GameplaySkinSceneStateMachine(
                "object.machine",
                "object.idle",
                new[]
                {
                    state("object.idle", 0.1),
                    state("object.active", 0.9),
                    state("object.judged", 0.5),
                },
                new[]
                {
                    new GameplaySkinSceneTransition("object.spawn", "object.idle", "object.active", "object.spawn"),
                    new GameplaySkinSceneTransition("object.judgement", "object.active", "object.judged", "judgement.hit"),
                });
            var document = new GameplaySkinSceneDocument(
                root,
                Array.Empty<GameplaySkinSceneTrack>(),
                new[] { machine },
                new[]
                {
                    new GameplaySkinSceneBinding(
                        "object.binding.judgement-offset",
                        root.Id,
                        "rotation",
                        "judgement.offset"),
                },
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "object-scoped-scene",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[] { prepared(root, layout.LaneRect, laneTarget, GameplaySkinSlotCatalog.Note, null) });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);

            GameplaySkinSceneState state(string id, double opacity)
                => new GameplaySkinSceneState(id, new[]
                {
                    new GameplaySkinSceneStateAssignment(
                        $"{id}.opacity",
                        root.Id,
                        "opacity",
                        GameplaySkinScenePropertyValue.FromNumber(opacity)),
                });
        }

        private static RuntimeFixture createBgaScopedSpecialisedFixture(Texture texture)
        {
            LayoutFixture layout = createLayout("bms", 2);
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[]
            {
                provide(GameplaySkinSlotCatalog.BgaViewport, GameplaySkinResolvedMaterialTarget.Global, texture, source()),
            });
            GameplaySkinSceneNode dispatcher = node(
                "bga.root",
                GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global),
                null,
                null);
            GameplaySkinSceneNode first = node(
                "bga.viewport-0",
                GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Bga, null, 0),
                GameplaySkinSlotCatalog.BgaViewport.Id,
                null);
            GameplaySkinSceneNode second = node(
                "bga.viewport-1",
                GameplaySkinSceneNodeType.Sprite,
                target(GameplaySkinSceneTargetKind.Bga, null, 1),
                GameplaySkinSlotCatalog.BgaViewport.Id,
                null);
            dispatcher = withChildren(dispatcher, first, second);
            var machine = new GameplaySkinSceneStateMachine(
                "bga.machine",
                "bga.idle",
                new[]
                {
                    state("bga.idle", 0.1),
                    state("bga.snapshot", 0.2),
                    state("bga.changed", 0.9),
                },
                new[]
                {
                    new GameplaySkinSceneTransition("bga.snapshot", "bga.idle", "bga.snapshot", "bga.state"),
                    new GameplaySkinSceneTransition("bga.changed", "bga.snapshot", "bga.changed", "bga.state"),
                });
            var document = new GameplaySkinSceneDocument(
                dispatcher,
                Array.Empty<GameplaySkinSceneTrack>(),
                new[] { machine },
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            GameplaySkinPreparedSceneNode preparedFirst = prepared(
                first,
                layout.Snapshot.BgaViewports[0],
                GameplaySkinResolvedMaterialTarget.Global,
                GameplaySkinSlotCatalog.BgaViewport,
                null);
            GameplaySkinPreparedSceneNode preparedSecond = prepared(
                second,
                layout.Snapshot.BgaViewports[1],
                GameplaySkinResolvedMaterialTarget.Global,
                GameplaySkinSlotCatalog.BgaViewport,
                null);
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "bga-scoped-scene",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[]
                {
                    prepared(
                        dispatcher,
                        layout.Snapshot.Context.SafeBounds,
                        GameplaySkinResolvedMaterialTarget.Global,
                        null,
                        null,
                        preparedFirst,
                        preparedSecond),
                });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);

            GameplaySkinSceneState state(string id, double opacity)
                => new GameplaySkinSceneState(id, new[]
                {
                    new GameplaySkinSceneStateAssignment(
                        $"{id}.first",
                        first.Id,
                        "opacity",
                        GameplaySkinScenePropertyValue.FromNumber(opacity)),
                    new GameplaySkinSceneStateAssignment(
                        $"{id}.second",
                        second.Id,
                        "opacity",
                        GameplaySkinScenePropertyValue.FromNumber(opacity)),
                });
        }

        private static RuntimeFixture createLargeAuthorFixture(int nodeCount)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, Array.Empty<GameplaySkinResolvedMaterialEntry>());
            GameplaySkinSceneNode sourceRoot = node("node.root", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null);
            var sourceChildren = new List<GameplaySkinSceneNode>();
            var preparedChildren = new List<GameplaySkinPreparedSceneNode>();

            for (int index = 1; index < nodeCount; index++)
            {
                GameplaySkinSceneNode child = node($"node.n-{index}", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null);
                sourceChildren.Add(child);
                preparedChildren.Add(prepared(child, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, null, null));
            }

            sourceRoot = withChildren(sourceRoot, sourceChildren.ToArray());
            GameplaySkinPreparedSceneNode preparedRoot = prepared(
                sourceRoot,
                layout.Snapshot.Context.SafeBounds,
                GameplaySkinResolvedMaterialTarget.Global,
                null,
                null,
                preparedChildren.ToArray());
            var document = new GameplaySkinSceneDocument(
                sourceRoot,
                Array.Empty<GameplaySkinSceneTrack>(),
                Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "scene-large",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[] { preparedRoot });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);
        }

        private static RuntimeFixture createDynamicTextBudgetFixture(int textNodeCount)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, Array.Empty<GameplaySkinResolvedMaterialEntry>());
            GameplaySkinSceneNode root = node("text.root", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null);
            var sourceChildren = new List<GameplaySkinSceneNode>();
            var preparedChildren = new List<GameplaySkinPreparedSceneNode>();
            var bindings = new List<GameplaySkinSceneBinding>();

            for (int index = 0; index < textNodeCount; index++)
            {
                GameplaySkinSceneNode text = node($"text.{index}", GameplaySkinSceneNodeType.Text, target(GameplaySkinSceneTargetKind.Global), null, null);
                sourceChildren.Add(text);
                preparedChildren.Add(prepared(text, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, null, null));
                bindings.Add(new GameplaySkinSceneBinding($"binding.text.{index}", text.Id, "text", "combo.value"));
            }

            root = withChildren(root, sourceChildren.ToArray());
            var document = new GameplaySkinSceneDocument(
                root,
                Array.Empty<GameplaySkinSceneTrack>(),
                Array.Empty<GameplaySkinSceneStateMachine>(),
                bindings,
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            var preparedScene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "text-budget",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[]
                {
                    prepared(
                        root,
                        layout.Snapshot.Context.SafeBounds,
                        GameplaySkinResolvedMaterialTarget.Global,
                        null,
                        null,
                        preparedChildren.ToArray()),
                });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, preparedScene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);
        }

        private static RuntimeFixture createFaultedAuthorFixture(Texture texture)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialEntry entry = provide(GameplaySkinSlotCatalog.StageBackground, stage, texture, source());
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[] { entry });
            GameplaySkinSceneNode root = node("node.root", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null);
            GameplaySkinSceneNode invalid = node(
                "node.invalid",
                (GameplaySkinSceneNodeType)999,
                target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, 0),
                GameplaySkinSlotCatalog.StageBackground.Id,
                null);
            GameplaySkinSceneNode valid = node("node.valid", GameplaySkinSceneNodeType.Container, target(GameplaySkinSceneTargetKind.Global), null, null);
            root = withChildren(root, invalid, valid);
            var document = new GameplaySkinSceneDocument(
                root,
                Array.Empty<GameplaySkinSceneTrack>(),
                Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            GameplaySkinPreparedSceneNode preparedRoot = prepared(
                root,
                layout.Snapshot.Context.SafeBounds,
                GameplaySkinResolvedMaterialTarget.Global,
                null,
                null,
                prepared(invalid, layout.StageRect, stage, GameplaySkinSlotCatalog.StageBackground, null),
                prepared(valid, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, null, null));
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "scene-fault",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[] { preparedRoot });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);
        }

        private static RuntimeFixture createRuntimeProgramFaultFixture(Texture texture)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget stage = GameplaySkinResolvedMaterialTarget.ForStage(layout.Group);
            GameplaySkinResolvedMaterialEntry entry = provide(GameplaySkinSlotCatalog.StageBackground, stage, texture, source());
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[] { entry });
            GameplaySkinResolvedMaterialKey ownerKey = entry.Key;
            GameplaySkinSceneTarget stageTarget = target(GameplaySkinSceneTargetKind.Stage, layout.Group.Identity.Id.Value, 0);
            GameplaySkinSceneNode root = node("runtime-fault.root", GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global), null, null);
            GameplaySkinSceneNode owner = node("runtime-fault.owner", GameplaySkinSceneNodeType.Container,
                stageTarget, GameplaySkinSlotCatalog.StageBackground.Id, null);
            GameplaySkinSceneNode child = node("runtime-fault.child", GameplaySkinSceneNodeType.Sprite, stageTarget, null, null);
            GameplaySkinSceneNode sibling = node("runtime-fault.sibling", GameplaySkinSceneNodeType.Container,
                target(GameplaySkinSceneTargetKind.Global), null, null);
            owner = withChildren(owner, child);
            root = withChildren(root, owner, sibling);
            var badTrack = new GameplaySkinSceneTrack(
                "runtime-fault.track",
                GameplaySkinSceneTrackType.Frame,
                child.Id,
                "opacity",
                GameplaySkinSceneEasing.Step,
                false,
                new[]
                {
                    new GameplaySkinSceneKeyframe(
                        "runtime-fault.frame",
                        0,
                        GameplaySkinScenePropertyValue.FromString("not-a-number")),
                });
            var document = new GameplaySkinSceneDocument(
                root,
                new[] { badTrack },
                Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            var preparedChild = new GameplaySkinPreparedSceneNode(
                child.Id,
                child,
                child.Target,
                layout.StageRect,
                stage,
                null,
                null,
                null,
                Array.Empty<GameplaySkinPreparedSceneNode>(),
                GameplaySkinSceneLayer.Background,
                ownerKey,
                false);
            var preparedOwner = new GameplaySkinPreparedSceneNode(
                owner.Id,
                owner,
                owner.Target,
                layout.StageRect,
                stage,
                GameplaySkinSlotCatalog.StageBackground,
                null,
                null,
                new[] { preparedChild },
                GameplaySkinSceneLayer.Background,
                ownerKey,
                false);
            GameplaySkinPreparedSceneNode preparedRoot = prepared(
                root,
                layout.Snapshot.Context.SafeBounds,
                GameplaySkinResolvedMaterialTarget.Global,
                null,
                null,
                preparedOwner,
                prepared(sibling, layout.Snapshot.Context.SafeBounds, GameplaySkinResolvedMaterialTarget.Global, null, null));
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "runtime-program-fault",
                new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>()),
                document,
                Array.Empty<GameplaySkinPreparedSceneResource>(),
                new[] { preparedRoot });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);
        }

        private static RuntimeFixture createSpecialisedRuntimeFaultFixture(Texture texture)
        {
            LayoutFixture layout = createLayout();
            GameplaySkinResolvedMaterialTarget laneTarget = GameplaySkinResolvedMaterialTarget.ForLane(layout.Group, layout.Lane);
            GameplaySkinResolvedMaterialEntry entry = provide(GameplaySkinSlotCatalog.Note, laneTarget, texture, source());
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, new[] { entry });
            var resource = new GameplaySkinSceneResource(
                "specialised-fault.texture",
                GameplaySkinSceneResourceType.Texture,
                "textures/specialised-fault.png");
            var preparedResource = new GameplaySkinPreparedSceneResource(resource, "specialised-fault", 16, 16, texture);
            GameplaySkinSceneTarget lane = target(GameplaySkinSceneTargetKind.Lane, layout.Lane.Identity.Id.Value, 0);
            GameplaySkinSceneNode root = node(
                "specialised-fault.root",
                GameplaySkinSceneNodeType.Sprite,
                lane,
                GameplaySkinSlotCatalog.Note.Id,
                resource.Id);
            var badTrack = new GameplaySkinSceneTrack(
                "specialised-fault.track",
                GameplaySkinSceneTrackType.Frame,
                root.Id,
                "opacity",
                GameplaySkinSceneEasing.Step,
                false,
                new[]
                {
                    new GameplaySkinSceneKeyframe(
                        "specialised-fault.frame",
                        0,
                        GameplaySkinScenePropertyValue.FromString("not-a-number")),
                });
            var document = new GameplaySkinSceneDocument(
                root,
                new[] { badTrack },
                Array.Empty<GameplaySkinSceneStateMachine>(),
                Array.Empty<GameplaySkinSceneBinding>(),
                Array.Empty<GameplaySkinSceneTemplate>(),
                Array.Empty<GameplaySkinSceneInstance>());
            var scene = new GameplaySkinPreparedScene(
                layout.Snapshot,
                materialSet,
                "specialised-runtime-fault",
                new GameplaySkinSceneManifest(new[] { resource }),
                document,
                new[] { preparedResource },
                new[]
                {
                    prepared(
                        root,
                        layout.LaneRect,
                        laneTarget,
                        GameplaySkinSlotCatalog.Note,
                        preparedResource),
                });
            return new RuntimeFixture(
                GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet, scene),
                layout.Group,
                layout.Lane,
                layout.StageRect,
                layout.LaneRect);
        }

        private static RuntimeFixture fixture(LayoutFixture layout, IEnumerable<GameplaySkinResolvedMaterialEntry> entries)
        {
            GameplaySkinResolvedMaterialSet materialSet = materialSetFor(layout.Snapshot, entries);
            GameplaySkinLayoutPublication publication = GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materialSet);
            return new RuntimeFixture(publication, layout.Group, layout.Lane, layout.StageRect, layout.LaneRect);
        }

        private static GameplaySkinResolvedMaterialEntry provide(
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target,
            Texture texture,
            GameplaySkinResolvedMaterialSourceIdentity sourceIdentity)
            => GameplaySkinResolvedMaterialEntry.Provide(
                slot,
                target,
                sourceIdentity,
                GameplaySkinPublicSlotMaterial.FromPreparedResource(slot, $"textures/{slot.StableName}.png", texture));

        private static GameplaySkinResolvedMaterialSet materialSetFor(
            GameplaySkinLayoutSnapshot snapshot,
            IEnumerable<GameplaySkinResolvedMaterialEntry> entries)
            => GameplaySkinResolvedMaterialSet.Create(snapshot, GameplaySkinMaterialContractIdentity.CurrentFor(snapshot), entries);

        private static GameplaySkinEventStream createStream(GameplaySkinLayoutPublication publication)
            => new GameplaySkinEventStream(publication, 0, publication.PreparedScene.InitialEventState);

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

        private static void publishBga(
            GameplaySkinEventStream stream,
            GameplaySkinEventProducer producer,
            double gameplayTime,
            GameplaySkinEventKind eventKind,
            GameplaySkinBgaStateSnapshot state)
            => stream.Publish(producer, gameplayTime, GameplaySkinEventValue.Bga(eventKind, state), null, null);

        private static GameplaySkinPreparedSceneNode prepared(
            GameplaySkinSceneNode sourceNode,
            GameplaySkinLayoutRect rect,
            GameplaySkinResolvedMaterialTarget materialTarget,
            GameplaySkinSlotDescriptor? slot,
            GameplaySkinPreparedSceneResource? resource,
            params GameplaySkinPreparedSceneNode[] children)
            => prepared(sourceNode, rect, materialTarget, slot, resource, sourceNode.Id, children);

        private static GameplaySkinPreparedSceneNode prepared(
            GameplaySkinSceneNode sourceNode,
            GameplaySkinLayoutRect rect,
            GameplaySkinResolvedMaterialTarget materialTarget,
            GameplaySkinSlotDescriptor? slot,
            GameplaySkinPreparedSceneResource? resource,
            string instanceId,
            params GameplaySkinPreparedSceneNode[] children)
            => new GameplaySkinPreparedSceneNode(instanceId, sourceNode, rect, materialTarget, slot, resource, children);

        private static GameplaySkinSceneNode node(
            string id,
            GameplaySkinSceneNodeType type,
            GameplaySkinSceneTarget nodeTarget,
            string? slot,
            string? resource,
            IReadOnlyDictionary<string, GameplaySkinScenePropertyValue>? properties = null,
            IEnumerable<GameplaySkinSceneEffect>? effects = null,
            GameplaySkinSceneBlendMode blend = GameplaySkinSceneBlendMode.Alpha)
            => new GameplaySkinSceneNode(
                id,
                type,
                nodeTarget,
                slot,
                resource,
                blend,
                properties ?? new Dictionary<string, GameplaySkinScenePropertyValue>(),
                effects ?? Array.Empty<GameplaySkinSceneEffect>(),
                Array.Empty<GameplaySkinSceneNode>());

        private static GameplaySkinSceneNode withChildren(GameplaySkinSceneNode source, params GameplaySkinSceneNode[] children)
            => new GameplaySkinSceneNode(
                source.Id,
                source.Type,
                source.Target,
                source.SlotId,
                source.ResourceId,
                source.Blend,
                source.Properties,
                source.Effects,
                children);

        private static GameplaySkinSceneTarget target(GameplaySkinSceneTargetKind kind, string? id = null, int? index = null)
            => new GameplaySkinSceneTarget(kind, id, index);

        private static GameplaySkinResolvedMaterialSourceIdentity source()
            => GameplaySkinResolvedMaterialSourceIdentity.Create(GameplaySkinResolvedMaterialSourceKind.SelectedPackage, "selected", "v1");

        private static GameplaySkinScoreStateSnapshot score(int combo)
            => new GameplaySkinScoreStateSnapshot(1000, combo, combo, 1, 0.5);

        private static GameplaySkinInputStateSnapshot input(RuntimeFixture fixture, bool pressed)
            => new GameplaySkinInputStateSnapshot(fixture.Group.Identity.Id, fixture.Lane.Identity.Id, pressed, pressed ? 1 : 0);

        private static GameplaySkinEventStateSnapshot completeState(RuntimeFixture fixture, int combo, bool pressed)
            => new GameplaySkinEventStateSnapshot(
                GameplaySkinLifecycleState.Running,
                new[] { input(fixture, pressed) },
                Array.Empty<GameplaySkinObjectStateSnapshot>(),
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                score(combo),
                new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1),
                Array.Empty<GameplaySkinBgaStateSnapshot>());

        private static GameplaySkinEventStateSnapshot lifecycleState(GameplaySkinLifecycleState lifecycle)
            => new GameplaySkinEventStateSnapshot(
                lifecycle,
                Array.Empty<GameplaySkinInputStateSnapshot>(),
                Array.Empty<GameplaySkinObjectStateSnapshot>(),
                Array.Empty<GameplaySkinCurrentJudgementStateSnapshot>(),
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 1),
                new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1),
                Array.Empty<GameplaySkinBgaStateSnapshot>());

        private static GameplaySkinObjectStateSnapshot objectState(RuntimeFixture fixture, long id, GameplaySkinObjectState state)
            => new GameplaySkinObjectStateSnapshot(
                id,
                GameplaySkinObjectKind.Mine,
                state,
                fixture.Group.Identity.Id,
                fixture.Lane.Identity.Id,
                0,
                100,
                state == GameplaySkinObjectState.Despawned ? 1 : 0.5);

        private static GameplaySkinObjectStateSnapshot firstObject(GameplaySkinSceneRuntimeHost host)
        {
            FieldInfo field = typeof(GameplaySkinSceneRuntimeHost).GetField("firstObject", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return ((GameplaySkinObjectStateSnapshot?)field.GetValue(host))!.Value;
        }

        private static GameplaySkinObjectStateSnapshot firstObjectForLane(GameplaySkinSceneRuntimeHost host, GameplaySkinLaneId laneId)
        {
            FieldInfo field = typeof(GameplaySkinSceneRuntimeHost).GetField("firstObjectByLane", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var values = (Dictionary<GameplaySkinLaneId, GameplaySkinObjectStateSnapshot>)field.GetValue(host)!;
            return values[laneId];
        }

        private static GameplaySkinObjectStateSnapshot firstObjectForGroup(GameplaySkinSceneRuntimeHost host, GameplaySkinLaneGroupId groupId)
        {
            FieldInfo field = typeof(GameplaySkinSceneRuntimeHost).GetField("firstObjectByGroup", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var values = (Dictionary<GameplaySkinLaneGroupId, GameplaySkinObjectStateSnapshot>)field.GetValue(host)!;
            return values[groupId];
        }

        private static LayoutFixture createLayout(string rulesetId = "mania", int bgaViewportCount = 1)
        {
            GameplaySkinLaneGroupIdentity groupIdentity = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group"),
                GameplaySkinLaneSide.Neutral);
            GameplaySkinLaneTopologyEntry lane = GameplaySkinLaneTopologyEntry.Create(
                GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-1"), groupIdentity, GameplaySkinLaneRole.Key),
                0,
                0,
                0,
                0);
            GameplaySkinLaneTopologyGroup group = GameplaySkinLaneTopologyGroup.Create(groupIdentity, 0, 0, new[] { lane });
            GameplaySkinLaneTopologySnapshot topology = GameplaySkinLaneTopologySnapshot.Create(new[] { group });
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            GameplaySkinLayoutRect stageRect = GameplaySkinLayoutRect.Create(0.2f, 0.1f, 0.6f, 0.8f);
            GameplaySkinLayoutRect laneRect = GameplaySkinLayoutRect.Create(0.3f, 0.1f, 0.2f, 0.8f);
            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(
                rulesetId,
                $"{rulesetId}.test",
                rulesetId == "bms" ? "7k" : "1k",
                "single",
                topology,
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                16f / 9,
                1,
                GameplaySkinScrollDirection.Down,
                package,
                0,
                1);
            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(
                context,
                new[] { new GameplaySkinLayoutGroup(group, stageRect) },
                new[] { new GameplaySkinLayoutLane(lane, laneRect) },
                new[]
                {
                    new GameplaySkinLayoutSurface("mania.playfield", stageRect, 10, true, true),
                    new GameplaySkinLayoutSurface("mania.barline", GameplaySkinLayoutRect.Create(0.2f, 0.1f, 0.6f, 0.02f), 20, true, false),
                    new GameplaySkinLayoutSurface("mania.hit-target", GameplaySkinLayoutRect.Create(0.2f, 0.82f, 0.6f, 0.02f), 40, true, false),
                    new GameplaySkinLayoutSurface("mania.judgement", GameplaySkinLayoutRect.Create(0.25f, 0.65f, 0.5f, 0.1f), 50, false, false),
                    new GameplaySkinLayoutSurface("mania.hud", GameplaySkinLayoutRect.Create(0, 0, 1, 0.08f), 60, false, false),
                    new GameplaySkinLayoutSurface("mania.gauge", GameplaySkinLayoutRect.Create(0, 0, 0.25f, 0.08f), 61, false, false),
                    new GameplaySkinLayoutSurface("mania.combo", GameplaySkinLayoutRect.Create(0.4f, 0.2f, 0.2f, 0.1f), 62, false, false),
                },
                Enumerable.Range(0, bgaViewportCount)
                          .Select(index => GameplaySkinLayoutRect.Create(0.65f - index * 0.32f, 0.1f, 0.3f, 0.4f)));
            return new LayoutFixture(snapshot, group, lane, stageRect, laneRect);
        }

        private static DualLayoutFixture createDualStageLayout()
        {
            GameplaySkinLaneGroupIdentity firstIdentity = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group-1"),
                GameplaySkinLaneSide.Primary);
            GameplaySkinLaneGroupIdentity secondIdentity = GameplaySkinLaneGroupIdentity.Create(
                GameplaySkinLaneGroupId.Create("test.group-2"),
                GameplaySkinLaneSide.Secondary);
            GameplaySkinLaneTopologyEntry firstLane = GameplaySkinLaneTopologyEntry.Create(
                GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-1"), firstIdentity, GameplaySkinLaneRole.Key),
                0,
                0,
                0,
                0);
            GameplaySkinLaneTopologyEntry secondLane = GameplaySkinLaneTopologyEntry.Create(
                GameplaySkinLaneIdentity.Create(GameplaySkinLaneId.Create("test.lane-2"), secondIdentity, GameplaySkinLaneRole.Key),
                1,
                0,
                1,
                0);
            GameplaySkinLaneTopologyGroup first = GameplaySkinLaneTopologyGroup.Create(firstIdentity, 0, 0, new[] { firstLane });
            GameplaySkinLaneTopologyGroup second = GameplaySkinLaneTopologyGroup.Create(secondIdentity, 1, 1, new[] { secondLane });
            GameplaySkinLaneTopologySnapshot topology = GameplaySkinLaneTopologySnapshot.Create(new[] { first, second });
            GameplaySkinPackageRevision package = GameplaySkinPackageRevision.CreateCompatibility();
            var firstRect = GameplaySkinLayoutRect.Create(0.1f, 0.12f, 0.34f, 0.7f);
            var secondRect = GameplaySkinLayoutRect.Create(0.56f, 0.12f, 0.34f, 0.7f);
            GameplaySkinLayoutContext context = GameplaySkinLayoutContext.Create(
                "mania",
                "mania.test-dual",
                "1k-1k",
                "mania-dual",
                topology,
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                GameplaySkinLayoutRect.Create(0, 0, 1, 1),
                16f / 9,
                1,
                GameplaySkinScrollDirection.Down,
                package,
                0,
                1);
            GameplaySkinLayoutSnapshot snapshot = GameplaySkinLayoutSnapshot.Create(
                context,
                new[]
                {
                    new GameplaySkinLayoutGroup(first, firstRect),
                    new GameplaySkinLayoutGroup(second, secondRect),
                },
                new[]
                {
                    new GameplaySkinLayoutLane(firstLane, firstRect),
                    new GameplaySkinLayoutLane(secondLane, secondRect),
                },
                new[]
                {
                    new GameplaySkinLayoutSurface("mania.playfield", GameplaySkinLayoutRect.Create(0.1f, 0.12f, 0.8f, 0.7f), 10, true, true),
                    new GameplaySkinLayoutSurface("mania.barline", GameplaySkinLayoutRect.Create(0.1f, 0.12f, 0.8f, 0.02f), 20, true, false),
                    new GameplaySkinLayoutSurface("mania.hit-target", GameplaySkinLayoutRect.Create(0.1f, 0.8f, 0.8f, 0.02f), 40, true, false),
                    new GameplaySkinLayoutSurface("mania.judgement", GameplaySkinLayoutRect.Create(0.18f, 0.62f, 0.64f, 0.1f), 50, false, false),
                    new GameplaySkinLayoutSurface("mania.hud", GameplaySkinLayoutRect.Create(0.05f, 0.02f, 0.9f, 0.08f), 60, false, false),
                    new GameplaySkinLayoutSurface("mania.gauge", GameplaySkinLayoutRect.Create(0, 0.86f, 0.2f, 0.06f), 61, false, false),
                    new GameplaySkinLayoutSurface("mania.combo", GameplaySkinLayoutRect.Create(0.2f, 0.28f, 0.6f, 0.1f), 62, false, false),
                });
            return new DualLayoutFixture(
                snapshot,
                snapshot.GroupsInLogicalOrder);
        }

        private static GameplaySkinLayoutRect intersect(GameplaySkinLayoutRect first, GameplaySkinLayoutRect second)
        {
            float left = Math.Max(first.Left, second.Left);
            float top = Math.Max(first.Top, second.Top);
            float right = Math.Min(first.Right, second.Right);
            float bottom = Math.Min(first.Bottom, second.Bottom);
            return GameplaySkinLayoutRect.Create(left, top, right - left, bottom - top);
        }

        private static GameplaySkinLayoutRect projectStageWidth(GameplaySkinLayoutRect stage, GameplaySkinLayoutRect surface)
            => GameplaySkinLayoutRect.Create(stage.Left, surface.Top, stage.Width, surface.Height);

        private sealed record LayoutFixture(
            GameplaySkinLayoutSnapshot Snapshot,
            GameplaySkinLaneTopologyGroup Group,
            GameplaySkinLaneTopologyEntry Lane,
            GameplaySkinLayoutRect StageRect,
            GameplaySkinLayoutRect LaneRect);

        private sealed record RuntimeFixture(
            GameplaySkinLayoutPublication Publication,
            GameplaySkinLaneTopologyGroup Group,
            GameplaySkinLaneTopologyEntry Lane,
            GameplaySkinLayoutRect StageRect,
            GameplaySkinLayoutRect LaneRect);

        private sealed record CompiledProgramFixture(
            GameplaySkinPreparedScene Scene,
            GameplaySkinPreparedSceneResource Resource1,
            GameplaySkinPreparedSceneResource Resource2);

        private sealed record DualJudgementFixture(
            GameplaySkinLayoutPublication Publication,
            GameplaySkinLaneTopologyGroup FirstGroup,
            GameplaySkinLaneTopologyEntry FirstLane,
            GameplaySkinLaneTopologyGroup SecondGroup,
            GameplaySkinLaneTopologyEntry SecondLane);

        private sealed record ScopedProjection(
            float FirstOpacity,
            float FirstRotation,
            float SecondOpacity,
            float SecondRotation);

        private sealed record DualLayoutFixture(
            GameplaySkinLayoutSnapshot Snapshot,
            IReadOnlyList<GameplaySkinLayoutGroup> Groups);

        private sealed record SpecialisedMaterial(string Name);

        private sealed class Adapter : IGameplaySkinLayoutAdapter
        {
            public GameplaySkinLayoutSnapshot Snapshot { get; }

            public Adapter(GameplaySkinLayoutSnapshot snapshot)
            {
                Snapshot = snapshot;
            }
        }
    }
}
