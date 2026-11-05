// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Screens.Play;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestFailureTriggerNeedsRealLifecycleAndPreservesOriginAcrossLateAttachAndRetry()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = createGaugeIncreasePublication(texture, false, "gameplay.fail");
            using var stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            try
            {
                publishScore(stream, producer, 5, GameplaySkinEventKind.GaugeChanged, gaugeScore(0, null));
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "Zero health is not a committed gameplay failure.");
                publishLifecycle(stream, producer, 10, GameplaySkinEventKind.GameplayFailed, GameplaySkinLifecycleState.Failed);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
                producer.SynchroniseTiming(20, publication.PreparedScene.InitialEventState.Timing);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                publishLifecycle(stream, producer, 25, GameplaySkinEventKind.GameplayFailed, GameplaySkinLifecycleState.Failed);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "A duplicate failure observation retains the first edge.");
                using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                late.ProcessFrame();
                Assert.That(gaugeEffectAlpha(late), Is.Zero);
                producer.SynchroniseTiming(30, publication.PreparedScene.InitialEventState.Timing);
                late.ProcessFrame();
                Assert.That(gaugeEffectAlpha(late), Is.EqualTo(1), "Late attachment uses the original time10, not its attach time25.");
                producer = producer.Reset(5, failureSnapshot(publication, GameplaySkinEventRuntimeHost.SnapshotFailureAt(10, 5)),
                    GameplaySkinEventResetReason.Rewind);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "An unknown restored failure cannot invent an animation start.");
                producer.SynchroniseTiming(100, publication.PreparedScene.InitialEventState.Timing);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                publishLifecycle(stream, producer, 10, GameplaySkinEventKind.GameplayFailed, GameplaySkinLifecycleState.Failed);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
                Assert.That(host.RuntimeFaults, Is.Empty);
                Assert.That(late.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNativeFailurePresentationFinishesWithFrozenChartTimeAndPauseAndLateAttach(bool skip)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = createGaugeIncreasePublication(texture, false, "gameplay.fail", 30);
            using var stream = createStream(publication);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            var controller = new GameplaySkinFailurePresentationController();
            try
            {
                host.ProcessFrame();
                Assert.That(host.GetFailureOutroDuration(false), Is.Zero);
                Assert.That(host.GetFailureOutroDuration(true), Is.EqualTo(30));
                publishLifecycle(stream, producer, 100, GameplaySkinEventKind.GameplayFailed, GameplaySkinLifecycleState.Failed);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "An opted-in outro requires its native Player presentation provider.");
                controller.Begin(30);
                sample();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
                controller.Advance(10, false);
                sample();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                controller.Advance(100, true);
                sample();
                Assert.That(controller.Elapsed, Is.EqualTo(10));
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                using var late = new GameplaySkinSceneRuntimeHost(publication, stream);
                late.ProcessFrame();
                Assert.That(gaugeEffectAlpha(late), Is.Zero);
                controller.Advance(10, false);
                sample();
                late.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.EqualTo(1));
                Assert.That(gaugeEffectAlpha(late), Is.EqualTo(1));
                if (skip)
                    controller.Finish();
                else
                    controller.Advance(1000, false);
                sample();
                Assert.That(controller.Finished, Is.True);
                Assert.That(controller.IsActive, Is.False);
                Assert.That(controller.Elapsed, Is.EqualTo(30));
                Assert.That(gaugeEffectAlpha(host), Is.Zero, "The exact terminal empty frame is reached without advancing the chart.");
                Assert.That(host.LastGameplayTime, Is.EqualTo(100));
                controller.Cancel();
                Assert.That(controller.Elapsed, Is.Null);
                Assert.That(controller.Finished, Is.False);
                producer = producer.Reset(0, publication.PreparedScene.InitialEventState, GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(gaugeEffectAlpha(host), Is.Zero);
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }

            void sample()
            {
                producer.SynchroniseTiming(100, new GameplaySkinTimingStateSnapshot(0, 0, 120, true, 1, failureElapsed: controller.Elapsed));
                host.ProcessFrame();
            }
        }

        [Test]
        public void TestFailurePresentationControllerFinishDoesNotCreateAnUnstartedPresentation()
        {
            var controller = new GameplaySkinFailurePresentationController();
            controller.Finish();
            Assert.That(controller.Elapsed, Is.Null);
            Assert.That(controller.Finished, Is.False);
            Assert.That(controller.IsActive, Is.False);
            controller.Begin(3000);
            Assert.That(controller.Elapsed, Is.Zero);
        }

        [Test]
        public void TestFailurePresentationControllerFinishEndsTheActivePresentationAtItsExactDuration()
        {
            var controller = new GameplaySkinFailurePresentationController();
            controller.Begin(3000);
            controller.Advance(300, false);
            controller.Finish();
            Assert.That(controller.Elapsed, Is.EqualTo(3000));
            Assert.That(controller.Finished, Is.True);
            Assert.That(controller.IsActive, Is.False);
        }

        [Test]
        public void TestFailurePresentationControllerRepeatedFinishDoesNotReopenThePresentation()
        {
            var controller = new GameplaySkinFailurePresentationController();
            controller.Begin(3000);
            controller.Finish();
            controller.Finish();
            controller.Advance(500, false);
            Assert.That(controller.Elapsed, Is.EqualTo(3000));
            Assert.That(controller.Finished, Is.True);
            Assert.That(controller.IsActive, Is.False);
            Assert.Throws<InvalidOperationException>(() => controller.Begin(3000));
        }

        [Test]
        public void TestFailurePresentationControllerFinishAfterCancelDoesNotRestoreThePresentation()
        {
            var controller = new GameplaySkinFailurePresentationController();
            controller.Begin(3000);
            controller.Advance(300, false);
            controller.Cancel();
            controller.Finish();
            Assert.That(controller.Elapsed, Is.Null);
            Assert.That(controller.Finished, Is.False);
            Assert.That(controller.IsActive, Is.False);
            controller.Begin(1000);
            controller.Finish();
            Assert.That(controller.Elapsed, Is.EqualTo(1000), "A later failure uses its own duration.");
        }

        [TestCase(0d)]
        [TestCase(12.3456789)]
        [TestCase(5000d)]
        [TestCase(null)]
        public void TestFailurePresentationSurvivesCompactTimingWithoutChangingPreparation(double? elapsed)
        {
            var preparation = new GameplaySkinPreparationStateSnapshot(true, false, 1234, 0, false, 0);
            var timing = new GameplaySkinTimingStateSnapshot(3.5, 2, 160, true, 1, 0.5, 2.25, startTime: -500,
                preparation: preparation, arrivalCueFadeStartTime: 10, failureElapsed: elapsed);
            GameplaySkinEventValue compact = GameplaySkinEventValue.Timing(GameplaySkinEventKind.TimingBeat, timing);
            GameplaySkinTimingStateSnapshot delivered = compact.GetTiming();
            Assert.That(delivered.FailureElapsed, Is.EqualTo(elapsed));
            Assert.That(((GameplaySkinTimingEventPayload)compact.Materialize(null, null)).State.FailureElapsed, Is.EqualTo(elapsed));
            Assert.That(delivered.Preparation, Is.EqualTo(preparation));
            Assert.That(delivered.StartTime, Is.EqualTo(-500));
            Assert.That(delivered.ArrivalCueFadeStartTime, Is.EqualTo(10));
            Assert.That(delivered.ScrollSpeed, Is.EqualTo(2.25));
        }

        [TestCase(-1d)]
        [TestCase(5000.01)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void TestFailurePresentationRejectsOutOfBoundsNativeTime(double elapsed)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinTimingStateSnapshot(0, 0, 120, true, 1, failureElapsed: elapsed));

        [TestCase(0d)]
        [TestCase(-1d)]
        [TestCase(5000.01)]
        [TestCase(double.NaN)]
        public void TestFailurePresentationControllerCannotStartOutsideItsBound(double duration)
            => Assert.Throws<ArgumentOutOfRangeException>(() => new GameplaySkinFailurePresentationController().Begin(duration));

        [TestCase("gameplay.fail", false, 30d, null, true, true)]
        [TestCase("gameplay.fail", false, 31d, null, true, false)]
        [TestCase("gameplay.fail", true, 30d, null, true, false)]
        [TestCase("gauge.increase", false, 30d, null, true, false)]
        [TestCase("gameplay.fail", false, 30d, "timing.time", true, false)]
        [TestCase("gameplay.fail", false, 30d, null, false, false)]
        public void TestFailureOutroRequiresTheActualMountedNonLoopingFailureProgram(string trigger, bool loop, double duration, string? clock,
                                                                                   bool mounted, bool valid)
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            if (valid)
            {
                using GameplaySkinLayoutPublication publication = createGaugeIncreasePublication(texture, loop, trigger, duration, clock, mounted);
                Assert.That(publication.PreparedScene.Program.FailureOutro, Is.EqualTo(duration));
            }
            else
            {
                Assert.Throws<GameplaySkinScenePreparationException>(() => createGaugeIncreasePublication(texture, loop, trigger, duration, clock, mounted));
            }
        }

        [TestCase(0d, GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase(5000d, GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase(-1d, GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase(5000.1, GameplaySkinSceneDecodeStatus.Invalid)]
        public void TestFailureOutroSchemaIsBoundedAndRoundTrips(double duration, GameplaySkinSceneDecodeStatus expected)
        {
            var scene = comboActiveScene();
            scene["failureOutro"] = duration;
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = GameplaySkinSceneCodec.DecodeScene(scene.ToString(), comboActiveManifest());
            Assert.That(decoded.Status, Is.EqualTo(expected));
            if (expected == GameplaySkinSceneDecodeStatus.Valid)
            {
                Assert.That(decoded.Value!.FailureOutro, Is.EqualTo(duration));
                GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(
                    GameplaySkinSceneCodec.EncodeScene(decoded.Value), comboActiveManifest());
                Assert.That(roundTrip.Value!.FailureOutro, Is.EqualTo(duration));
            }
        }

        private static GameplaySkinEventStateSnapshot failureSnapshot(GameplaySkinLayoutPublication publication, double? origin)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(GameplaySkinLifecycleState.Failed, initial.Inputs, initial.ActiveObjects,
                initial.CurrentJudgements, initial.Score, initial.Timing, initial.BgaViewports, failedAt: origin);
        }
    }
}
