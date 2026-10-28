// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [TestCase(257, true)]
        [TestCase(384, true)]
        [TestCase(385, false)]
        public void TestOnlyFrozenContentCombinationsHaveTheExtendedResourceCount(int count, bool selectedAccepted)
        {
            var manifest = new GameplaySkinSceneManifest(Enumerable.Range(0, count).Select(index =>
                new GameplaySkinSceneResource($"texture.r{index}", GameplaySkinSceneResourceType.Texture, $"textures/{index}.png")));
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            Assert.That(GameplaySkinSceneCodec.DecodeManifest(encoded).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(GameplaySkinSceneCodec.DecodeContentManifest(encoded).Status,
                Is.EqualTo(selectedAccepted ? GameplaySkinSceneDecodeStatus.Valid : GameplaySkinSceneDecodeStatus.Invalid));
        }

        [Test]
        public void TestPersistentRecordedClockUsesPreparationWithoutStartThenGameplayAndHidesAfterRetry()
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second, persistentIntro: true);
            using GameplaySkinEventStream stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.Zero);

            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1,
                preparation: new GameplaySkinPreparationStateSnapshot(true, true, 250, 4750, false, 0)));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.EqualTo(1));
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(first));

            var completed = new GameplaySkinPreparationStateSnapshot(true, false, 5750, 0, false, 0);
            producer.SynchroniseTiming(0, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, preparation: completed));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(second));
            var started = new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0, preparation: completed);
            producer.SynchroniseTiming(0, started);
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(second));
            producer.SynchroniseTiming(250, started);
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(first));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(first));

            producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.Zero);
            producer.SynchroniseTiming(750, new GameplaySkinTimingStateSnapshot(0, 0, 120, false, 1, startTime: 0));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.EqualTo(1));
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(second));
            Assert.That(host.RuntimeFaults, Is.Empty);
            producer.Dispose();
        }

        [Test]
        public void TestPreparedPackageAnimationAdvancesDuringFrozenGameplayAndCancellationRestoresAuthorDefaults()
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second);
            using var stream = new GameplaySkinEventStream(publication, -2000, preparationSnapshot(publication));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(publication.PreparedScene.Program.PreparationDuration, Is.EqualTo(5000));
            Assert.That(runtime(host, "prep.entry").TransformDrawable.Rotation, Is.EqualTo(37));
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.Zero);
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.Zero);

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 0, 5000, false, 0)));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.entry").TransformDrawable.Rotation, Is.Zero);
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(first));
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.EqualTo(1));

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 500, 4500, false, 0)));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.entry").TransformDrawable.Rotation, Is.EqualTo(90).Within(0.0001));
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(second), "The recorded drawable really selects its second prepared texture.");
            Assert.That(((SpriteText)runtime(host, "prep.remaining").ContentDrawable).Text.ToString(), Is.EqualTo("4500"));
            Assert.That(host.LastGameplayTime, Is.EqualTo(-2000));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.entry").TransformDrawable.Rotation, Is.EqualTo(90), "A paused preparation state cannot advance itself.");
            Assert.That(preparationPiece(host, "prep.intro").Texture, Is.SameAs(second));

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 1000, 4000, false, 0)));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.entry").TransformDrawable.Rotation, Is.EqualTo(180));
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.Zero, "The terminal recorded frame hides its native piece pool.");

            producer.SynchroniseTiming(-2000, preparationTiming(default));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.entry").TransformDrawable.Rotation, Is.EqualTo(37), "Cancelling preparation restores the author preview pose.");
            Assert.That(runtime(host, "prep.ready-phase").TransformDrawable.Rotation, Is.EqualTo(23));
            Assert.That(preparationPiece(host, "prep.intro").Alpha, Is.Zero);
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.Zero);
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestPreparedReadyClockUsesItsSourceFramesAndHidesAtTerminalAndUnavailableStates()
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second);
            using var stream = new GameplaySkinEventStream(publication, -2000, preparationSnapshot(publication));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.ready-visible").TransformDrawable.Alpha, Is.Zero);
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.Zero);

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 3500, 1500, true, 0)));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.ready-visible").TransformDrawable.Alpha, Is.EqualTo(1));
            Assert.That(preparationPiece(host, "prep.ready").Texture, Is.SameAs(first));
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.EqualTo(1));

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 4000, 1000, true, 500)));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.ready").Texture, Is.SameAs(second));
            Assert.That(runtime(host, "prep.ready-phase").TransformDrawable.Rotation, Is.EqualTo(50).Within(0.0001));
            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 4500, 500, true, 1000)));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.ready").Texture, Is.SameAs(first));

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 5000, 0, true, 1500)));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.Zero, "The empty terminal frame is not stretched or looped.");
            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, false, 5000, 0, false, 0)));
            host.ProcessFrame();
            Assert.That(runtime(host, "prep.ready-visible").TransformDrawable.Alpha, Is.Zero);
            Assert.That(runtime(host, "prep.ready-phase").TransformDrawable.Rotation, Is.EqualTo(23));
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.Zero);

            producer.SynchroniseTiming(-2000, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 0, 1000, true, 500)));
            host.ProcessFrame();
            Assert.That(preparationPiece(host, "prep.ready").Texture, Is.SameAs(second), "Short preparation samples the existing GET READY clip at its remaining-time coordinate.");
            Assert.That(preparationPiece(host, "prep.ready").Alpha, Is.EqualTo(1));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [TestCase(0, "+0.00")]
        [TestCase(12.375, "+12.38")]
        [TestCase(-3.126, "-3.13")]
        public void TestPreparedSignedOffsetUsesAuthorBitmapGlyphsAndTheSamePlainTextFormat(double offset, string expected)
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second);
            using var stream = new GameplaySkinEventStream(publication, -2000, preparationSnapshot(publication, offset: offset));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            var bitmap = (GameplaySkinBitmapText)runtime(host, "offset.bitmap").ContentDrawable;
            Assert.That(bitmap.AuthorText, Is.EqualTo(expected));
            Assert.That(bitmap.NaturalSize.X, Is.GreaterThan(0));
            Assert.That(((SpriteText)bitmap).Text.ToString(), Is.Empty, "Before loading, the author text never enters system-font pre-caching.");
            Assert.That(((SpriteText)runtime(host, "offset.plain").ContentDrawable).Text.ToString(), Is.EqualTo(expected));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestPreparedFirstRowCueUsesItsLaneAndUpdatesOpacityAtTheSameGameplayTime()
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second);
            GameplaySkinEventStateSnapshot snapshot = preparationSnapshot(publication);
            using var stream = new GameplaySkinEventStream(publication, 100, snapshot);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                host.ProcessFrame();
                Assert.That(runtime(host, "cue.first").TransformDrawable.Alpha, Is.EqualTo(1));
                Assert.That(runtime(host, "cue.later").TransformDrawable.Alpha, Is.Zero);
                Assert.That(runtime(host, "cue.first.pixel").TransformDrawable.Alpha, Is.EqualTo(1));

                producer.SynchroniseTiming(100, preparationTiming(default, fade: 0));
                host.ProcessFrame();
                Assert.That(runtime(host, "cue.first.pixel").TransformDrawable.Alpha, Is.EqualTo(0.5f), "Fade-state changes refresh the binding even while gameplay time is frozen.");
                producer.SynchroniseTiming(200, preparationTiming(default, fade: 0));
                host.ProcessFrame();
                Assert.That(runtime(host, "cue.first.pixel").TransformDrawable.Alpha, Is.Zero);

                GameplaySkinEventStateSnapshot unavailable = preparationSnapshot(publication, cueAvailable: false);
                producer = producer.Reset(200, unavailable, GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(runtime(host, "cue.first.pixel").TransformDrawable.Alpha, Is.Zero, "Missing native visibility authority does not claim a visible cue.");
                producer = producer.Reset(200, preparationSnapshot(publication, swapFirstRow: true), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(runtime(host, "cue.first").TransformDrawable.Alpha, Is.Zero);
                Assert.That(runtime(host, "cue.later").TransformDrawable.Alpha, Is.EqualTo(1), "An epoch snapshot replaces the first-row lane instead of retaining the former chart's opening.");
                Assert.That(runtime(host, "cue.later.pixel").TransformDrawable.Alpha, Is.EqualTo(1));
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestPreparedCueParentGateMultipliesNativeFadeAndHidesLateNotesAfterPreparation()
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second, gateFirstCueDuringPreparation: true);
            using var stream = new GameplaySkinEventStream(publication, 100, preparationSnapshot(publication));
            using GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            assertCue(1, 0, 0);

            producer.SynchroniseTiming(100, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 0, 5000, false, 0)));
            host.ProcessFrame();
            assertCue(1, 1, 1);

            producer.SynchroniseTiming(100, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 500, 4500, false, 0), fade: 0));
            host.ProcessFrame();
            assertCue(0.5f, 1, 0.5f);
            host.ProcessFrame();
            assertCue(0.5f, 1, 0.5f);

            producer.SynchroniseTiming(200, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, true, 1000, 4000, false, 0), fade: 0));
            host.ProcessFrame();
            assertCue(0, 1, 0);

            // A late first note still has native opacity one when READY finishes. Only its separate
            // preparation parent closes; the unchanged opacity source and ordinary cue retain one.
            producer.SynchroniseTiming(200, preparationTiming(new GameplaySkinPreparationStateSnapshot(true, false, 5000, 0, false, 0)));
            host.ProcessFrame();
            assertCue(1, 0, 0);
            producer.SynchroniseTiming(200, preparationTiming(default));
            host.ProcessFrame();
            assertCue(1, 0, 0);

            void assertCue(float opacity, float preparationAlpha, float effectiveAlpha)
            {
                GameplaySkinSceneRuntimeNode art = runtime(host, "cue.first.pixel");
                GameplaySkinSceneRuntimeNode gate = runtime(host, "cue.first.pixel.preparation");
                Assert.Multiple(() =>
                {
                    Assert.That(gate.ContentDrawable.ChildrenOfType<Container>(), Does.Contain(art.TransformDrawable),
                        "The native art must actually be mounted under its separate visibility parent.");
                    Assert.That(runtime(host, "cue.first").TransformDrawable.Alpha, Is.EqualTo(1), "The same lane remains the globally first row.");
                    Assert.That(art.TransformDrawable.Alpha, Is.EqualTo(opacity));
                    Assert.That(gate.TransformDrawable.Alpha, Is.EqualTo(preparationAlpha));
                    Assert.That(art.TransformDrawable.Alpha * gate.TransformDrawable.Alpha, Is.EqualTo(effectiveAlpha));
                    Assert.That(runtime(host, "cue.later.pixel").TransformDrawable.Alpha, Is.EqualTo(opacity),
                        "An ordinary consumer keeps the same native fade source without the preparation parent.");
                    Assert.That(host.RuntimeFaults, Is.Empty);
                });
            }
        }

        [TestCase(0)]
        [TestCase(1000)]
        [TestCase(5000)]
        [TestCase(10000)]
        public void TestPreparationPackageCodecAndPreparedProgramKeepDurationAndRecordedNodeClocks(double duration)
        {
            using Texture first = new DummyRenderer().CreateTexture(2, 2);
            using Texture second = new DummyRenderer().CreateTexture(2, 2);
            using GameplaySkinLayoutPublication publication = preparationPublication(first, second, duration);
            Assert.That(publication.PreparedScene.Program.PreparationDuration, Is.EqualTo(duration));
            Assert.That(publication.PreparedScene.Program.UsesBindingSource(GameplaySkinSceneBindingSource.PreparationElapsed), Is.True);
            Assert.That(publication.PreparedScene.Program.UsesBindingSource(GameplaySkinSceneBindingSource.PreparationReadyElapsed), Is.True);
            Assert.That(publication.PreparedScene.RecordedAnimations["anim.intro"].FrameCount, Is.EqualTo(3));
            Assert.That(publication.PreparedScene.RecordedAnimations["anim.ready"].FrameCount, Is.EqualTo(4));
        }

        [TestCase(-1)]
        [TestCase(10001)]
        public void TestPreparationSourceRejectsAnInvalidDelay(double duration)
        {
            JObject source = preparationSceneSource();
            source["preparationDuration"] = duration;
            GameplaySkinSceneManifest manifest = preparationManifest();
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(source.ToString(Formatting.None), manifest);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.InvalidAnimation));
        }

        [TestCase("preparation.active", "recorded-animation", GameplaySkinSceneDiagnosticCode.UnknownBindingSource)]
        [TestCase("preparation.elapsed", "container", GameplaySkinSceneDiagnosticCode.InvalidAnimation)]
        public void TestRecordedSourceClocksRejectBooleanSourcesAndOrdinaryContainers(string clock, string type, GameplaySkinSceneDiagnosticCode expected)
        {
            JObject source = preparationSceneSource();
            JObject node = (JObject)source["root"]!["children"]![0]!["children"]![1]!;
            node["clock"] = clock;
            node["type"] = type;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(source.ToString(Formatting.None), preparationManifest());
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(expected));
        }

        [TestCase("0123456789.-")]
        [TestCase("0123456789.+")]
        public void TestSignedBitmapSourceRequiresBothAuthorSignGlyphs(string characters)
        {
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(
                preparationSceneSource().ToString(Formatting.None), preparationManifest(characters));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
            Assert.That(decoded.Diagnostics.Select(diagnostic => diagnostic.Code), Does.Contain(GameplaySkinSceneDiagnosticCode.InvalidReference));
        }

        private static Sprite preparationPiece(GameplaySkinSceneRuntimeHost host, string node)
            => runtime(host, node).ContentDrawable.ChildrenOfType<Sprite>().Single();

        private static GameplaySkinTimingStateSnapshot preparationTiming(GameplaySkinPreparationStateSnapshot preparation, double? fade = null)
            => new(0, -1, 120, false, 1, startTime: -2000, preparation: preparation, arrivalCueFadeStartTime: fade);

        private static GameplaySkinEventStateSnapshot preparationSnapshot(GameplaySkinLayoutPublication publication, double offset = 0,
                                                                         bool cueAvailable = true, bool swapFirstRow = false)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            IReadOnlyList<GameplaySkinLaneTopologyEntry> lanes = publication.Snapshot.Context.Topology.LanesInLogicalOrder;
            var arrivals = new[]
            {
                new GameplaySkinLaneArrival(lanes[0].Identity.Group.Id, lanes[0].Identity.Id, swapFirstRow ? 11000 : 10000),
                new GameplaySkinLaneArrival(lanes[1].Identity.Group.Id, lanes[1].Identity.Id, swapFirstRow ? 10000 : 11000),
            };
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(0, 0, 0, 1, 1, offsetState: new GameplaySkinOffsetStateSnapshot(true, offset, false, true)),
                preparationTiming(default), initial.BgaViewports, chartPresentation: new GameplaySkinChartPresentation(arrivals, arrivalCueAvailable: cueAvailable));
        }

        private static GameplaySkinLayoutPublication preparationPublication(Texture first, Texture second, double duration = 5000,
                                                                            bool gateFirstCueDuringPreparation = false, bool persistentIntro = false)
        {
            DualLayoutFixture layout = createDualStageLayout();
            GameplaySkinSceneManifest manifest = preparationManifest();
            JObject sourceScene = preparationSceneSource();
            sourceScene["preparationDuration"] = duration;
            if (persistentIntro)
                sourceScene["root"]!["children"]![0]!["children"]![1]!["clock"] = "presentation.elapsed";
            IReadOnlyList<GameplaySkinLaneTopologyEntry> lanes = layout.Snapshot.Context.Topology.LanesInLogicalOrder;
            sourceScene["root"]!["children"]![1]!["target"] = new JObject { ["kind"] = "lane", ["id"] = lanes[0].Identity.Id.Value, ["index"] = 0 };
            sourceScene["root"]!["children"]![2]!["target"] = new JObject { ["kind"] = "lane", ["id"] = lanes[1].Identity.Id.Value, ["index"] = 1 };
            sourceScene["root"]!["children"]![1]!["children"]![0]!["target"] = sourceScene["root"]!["children"]![1]!["target"]!.DeepClone();
            sourceScene["root"]!["children"]![2]!["children"]![0]!["target"] = sourceScene["root"]!["children"]![2]!["target"]!.DeepClone();
            if (gateFirstCueDuringPreparation)
            {
                JObject cue = (JObject)sourceScene["root"]!["children"]![1]!;
                JToken art = cue["children"]![0]!.DeepClone();
                cue["children"] = new JArray(new JObject
                {
                    ["id"] = "cue.first.pixel.preparation",
                    ["type"] = "container",
                    ["target"] = cue["target"]!.DeepClone(),
                    ["blend"] = "alpha",
                    ["properties"] = new JObject { ["visible"] = false },
                    ["effects"] = new JArray(),
                    ["children"] = new JArray(art),
                });
                ((JArray)sourceScene["bindings"]!).Add(new JObject
                {
                    ["id"] = "cue.first.preparation-binding",
                    ["target"] = "cue.first.pixel.preparation",
                    ["property"] = "visible",
                    ["source"] = "preparation.active",
                });
            }
            JObject intro = preparationClip(false);
            intro["loop"] = persistentIntro;
            var files = new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [GameplaySkinSceneContracts.MANIFEST_FILE_NAME] = GameplaySkinSceneCodec.EncodeManifestUtf8(manifest),
                [GameplaySkinSceneContracts.SCENE_FILE_NAME] = Encoding.UTF8.GetBytes(sourceScene.ToString(Formatting.None)),
                ["one.png"] = preparationPng(),
                ["two.png"] = preparationPng(),
                ["intro.json"] = Encoding.UTF8.GetBytes(intro.ToString(Formatting.None)),
                ["ready.json"] = Encoding.UTF8.GetBytes(preparationClip(true).ToString(Formatting.None)),
            };
            GameplaySkinPreparedPackage package = GameplaySkinPreparedPackage.Prepare((path, _) => files.GetValueOrDefault(path),
                "preparation-source-fixture", Guid.NewGuid(), null, CancellationToken.None);
            GameplaySkinSceneDocument document = package.Document!;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(GameplaySkinSceneCodec.EncodeScene(document), manifest);
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), string.Join(",", roundTrip.Diagnostics.Select(diagnostic => diagnostic.Code)));
            GameplaySkinSceneDocument recovered = roundTrip.Value!;
            Assert.That(recovered.PreparationDuration, Is.EqualTo(duration));
            Assert.That(recovered.Root.Children[0].Children[1].ClockSource, Is.EqualTo(persistentIntro
                ? GameplaySkinSceneBindingSource.PresentationElapsed : GameplaySkinSceneBindingSource.PreparationElapsed));
            Assert.That(recovered.Root.Children[0].Children[3].ClockSource, Is.EqualTo(GameplaySkinSceneBindingSource.PreparationReadyElapsed));
            files.Clear();

            GameplaySkinResolvedMaterialTarget global = GameplaySkinResolvedMaterialTarget.Global;
            GameplaySkinResolvedMaterialEntry[] entries = new[] { provide(GameplaySkinSlotCatalog.TextHud, global, first, source()) }
                .Concat(layout.Snapshot.Context.Topology.GroupsInLogicalOrder.Select(group => provide(GameplaySkinSlotCatalog.Decoration,
                    GameplaySkinResolvedMaterialTarget.ForLane(group, group.LanesInLogicalOrder[0]), first, source()))).ToArray();
            GameplaySkinResolvedMaterialSet materials = materialSetFor(layout.Snapshot, entries);
            GameplaySkinPreparedSceneResource[] resources = package.Resources.Select(captured => new GameplaySkinPreparedSceneResource(captured.Source,
                captured.ContentRevision, captured.EncodedBytes, captured.DecodedBytes, captured.Source.Id == "texture.one" ? first : second)).ToArray();
            GameplaySkinPreparedSceneNode preparedRoot = prepareNode(document.Root, null);
            var preparedScene = new GameplaySkinPreparedScene(layout.Snapshot, materials, package.ContentRevision, package.Manifest, document, resources,
                new[] { preparedRoot }, package: package);
            return GameplaySkinLayoutPublication.Create(new Adapter(layout.Snapshot), materials, preparedScene);

            GameplaySkinPreparedSceneNode prepareNode(GameplaySkinSceneNode node, GameplaySkinResolvedMaterialKey? inherited)
            {
                GameplaySkinResolvedMaterialTarget target = global;
                GameplaySkinLayoutRect rect = layout.Snapshot.GetSurface("mania.hud").Rect;
                if (node.Target.Kind == GameplaySkinSceneTargetKind.Lane)
                {
                    GameplaySkinLaneId id = GameplaySkinLaneId.Create(node.Target.StableId!);
                    Assert.That(layout.Snapshot.Context.Topology.TryGetLane(id, out GameplaySkinLaneTopologyEntry? lane), Is.True);
                    GameplaySkinLaneTopologyEntry exactLane = lane!;
                    GameplaySkinLaneTopologyGroup group = layout.Snapshot.Context.Topology.GroupsInLogicalOrder[exactLane.Identity.Group.Id == lanes[0].Identity.Group.Id ? 0 : 1];
                    target = GameplaySkinResolvedMaterialTarget.ForLane(group, exactLane);
                    rect = layout.Snapshot.GetLane(id).Rect;
                }
                GameplaySkinSlotDescriptor? slot = null;
                if (node.SlotId != null)
                    Assert.That(GameplaySkinSlotCatalog.TryGet(node.SlotId, out slot), Is.True);
                GameplaySkinResolvedMaterialKey? owner = slot == null ? inherited : new GameplaySkinResolvedMaterialKey(slot, target);
                if (owner == null)
                    rect = layout.Snapshot.Context.SafeBounds;
                return new GameplaySkinPreparedSceneNode(node.Id, node, node.Target, rect, target, slot, null, null,
                    node.Children.Select(child => prepareNode(child, owner)), owner == null ? GameplaySkinSceneLayer.Underlay : GameplaySkinSceneHostPolicy.LayerFor(owner.Slot),
                    owner, owner == null && node.Type == GameplaySkinSceneNodeType.Container && node.Properties.Count == 0 && node.Effects.Count == 0);
            }
        }

        private static GameplaySkinSceneManifest preparationManifest(string characters = "0123456789.+-")
        {
            var manifest = new GameplaySkinSceneManifest(new[]
            {
                new GameplaySkinSceneResource("texture.one", GameplaySkinSceneResourceType.Texture, "one.png"),
                new GameplaySkinSceneResource("texture.two", GameplaySkinSceneResourceType.Texture, "two.png"),
            }, bitmapFonts: new[] { new GameplaySkinBitmapFont("font.offset", 2, characters.Select(character => new GameplaySkinBitmapGlyph(character, "texture.one", 2))) },
                animations: new[]
                {
                    new GameplaySkinRecordedAnimationDefinition("anim.intro", "intro.json", new[] { "texture.one", "texture.two" }),
                    new GameplaySkinRecordedAnimationDefinition("anim.ready", "ready.json", new[] { "texture.one", "texture.two" }),
                });
            GameplaySkinSceneDecodeResult<GameplaySkinSceneManifest> decoded = GameplaySkinSceneCodec.DecodeManifest(GameplaySkinSceneCodec.EncodeManifest(manifest));
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            return decoded.Value!;
        }

        private static JObject preparationSceneSource() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-scene.v1","preparationDuration":5000,
             "root":{"id":"prep.dispatch","type":"container","target":{"kind":"global"},"blend":"inherit","properties":{},"effects":[],"children":[
               {"id":"prep.hud","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[
                 {"id":"prep.entry","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"rotation":37},"effects":[],"children":[]},
                 {"id":"prep.intro","type":"recorded-animation","target":{"kind":"global"},"animation":"anim.intro","clock":"preparation.elapsed","blend":"alpha","properties":{},"effects":[],"children":[]},
                 {"id":"prep.ready-phase","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{"rotation":23},"effects":[],"children":[]},
                 {"id":"prep.ready","type":"recorded-animation","target":{"kind":"global"},"animation":"anim.ready","clock":"preparation.ready-elapsed","blend":"alpha","properties":{},"effects":[],"children":[]},
                 {"id":"prep.ready-visible","type":"container","target":{"kind":"global"},"blend":"alpha","properties":{},"effects":[],"children":[]},
                 {"id":"prep.remaining","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"","font-size":12},"effects":[],"children":[]},
                 {"id":"offset.bitmap","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"+0.00","font-size":12,"bitmap-font":"font.offset","format":"signed-fixed-2"},"effects":[],"children":[]},
                 {"id":"offset.plain","type":"text","target":{"kind":"global"},"blend":"alpha","properties":{"text":"","font-size":12,"format":"signed-fixed-2"},"effects":[],"children":[]}]},
               {"id":"cue.first","type":"container","target":{"kind":"lane","id":"test.lane-1","index":0},"slot":"decoration","blend":"alpha","properties":{},"effects":[],"children":[
                 {"id":"cue.first.pixel","type":"container","target":{"kind":"lane","id":"test.lane-1","index":0},"blend":"alpha","properties":{},"effects":[],"children":[]}]},
               {"id":"cue.later","type":"container","target":{"kind":"lane","id":"test.lane-2","index":1},"slot":"decoration","blend":"alpha","properties":{},"effects":[],"children":[
                 {"id":"cue.later.pixel","type":"container","target":{"kind":"lane","id":"test.lane-2","index":1},"blend":"alpha","properties":{},"effects":[],"children":[]}]}]},
             "tracks":[
               {"id":"prep.entry-track","type":"tween","target":"prep.entry","property":"rotation","easing":"linear","loop":false,"clock":"preparation.elapsed","keyframes":[
                 {"id":"prep.entry-zero","time":0,"value":0},{"id":"prep.entry-end","time":1000,"value":180}]},
               {"id":"prep.ready-track","type":"tween","target":"prep.ready-phase","property":"rotation","easing":"linear","loop":false,"clock":"preparation.ready-elapsed","keyframes":[
                 {"id":"prep.ready-zero","time":0,"value":0},{"id":"prep.ready-end","time":1500,"value":150}]}],
             "bindings":[
               {"id":"prep.ready-binding","target":"prep.ready-visible","property":"visible","source":"preparation.ready"},
               {"id":"prep.remaining-binding","target":"prep.remaining","property":"text","source":"preparation.remaining"},
               {"id":"offset.bitmap-binding","target":"offset.bitmap","property":"text","source":"offset.value"},
               {"id":"offset.plain-binding","target":"offset.plain","property":"text","source":"offset.value"},
               {"id":"cue.first-row-binding","target":"cue.first","property":"visible","source":"arrival.first-row"},
               {"id":"cue.later-row-binding","target":"cue.later","property":"visible","source":"arrival.first-row"},
               {"id":"cue.first-alpha-binding","target":"cue.first.pixel","property":"opacity","source":"arrival.cue-opacity"},
               {"id":"cue.later-alpha-binding","target":"cue.later.pixel","property":"opacity","source":"arrival.cue-opacity"}],
             "stateMachines":[],"templates":[],"instances":[]}
            """);

        private static JObject preparationClip(bool ready)
        {
            JObject result = JObject.Parse("""
                {"contract":"oms-recorded-animation.v1","width":100,"height":100,"fps":2,"loop":false,"trigger":"gameplay.attach",
                 "geometries":[
                   {"resource":"texture.one","vertices":[[0,0,0,0,0],[10,0,0,1,0],[0,10,0,0,1]],"triangles":[0,1,2]},
                   {"resource":"texture.two","vertices":[[0,0,0,0,0],[10,0,0,1,0],[0,10,0,0,1]],"triangles":[0,1,2]}],
                 "nodes":[{"parent":-1,"geometry":0,"blend":"normal"},{"parent":-1,"geometry":1,"blend":"normal"}],
                 "states":[{"matrix":[1,0,0,0,1,0,0,0,1,0,0,0],"origin":[0,0,0],"projection":"affine","multiply":[1,1,1,1],"add":[0,0,0,0],"hsl":[0,0,0],"mask":null,"colourMatrix":null}],
                 "frames":[{"nodes":[[0,0]],"camera":null},{"nodes":[[1,0]],"camera":null},{"nodes":[],"camera":null}]}
                """);
            result["contract"] = GameplaySkinRecordedAnimationCodec.CONTRACT;
            if (ready)
                ((JArray)result["frames"]!).Insert(2, new JObject { ["nodes"] = new JArray { new JArray(0, 0) }, ["camera"] = null });
            return result;
        }

        private static byte[] preparationPng()
        {
            using var image = new Image<Rgba32>(2, 2, new Rgba32(255, 255, 255, 255));
            using var stream = new MemoryStream();
            image.SaveAsPng(stream);
            return stream.ToArray();
        }
    }
}
