// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    public sealed partial class GameplaySkinSceneRuntimeHostTest
    {
        [Test]
        public void TestDerivedBindingsAndClockUseFoldedScoreStateAndClearUnavailableAcrossReset()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture, numericScene());
            GameplaySkinEventStateSnapshot initial = numericSnapshot(publication, 10, 2);
            using var stream = new GameplaySkinEventStream(publication, 0, initial);
            GameplaySkinEventProducer producer = stream.CreateProducer();
            try
            {
                using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
                host.ProcessFrame();
                Assert.That(pacemakerText(host, "next.value"), Is.EqualTo("5"));
                Assert.That(runtime(host, "next.track.value").TransformDrawable.Rotation, Is.EqualTo(5));

                publishScore(stream, producer, 1, GameplaySkinEventKind.ScoreChanged, numericSnapshot(publication, 30, 0).Score);
                host.ProcessFrame();
                Assert.That(pacemakerText(host, "next.value"), Is.Empty);
                Assert.That(runtime(host, "next.track.value").TransformDrawable.Rotation, Is.EqualTo(7));

                publishScore(stream, producer, 2, GameplaySkinEventKind.ScoreChanged, numericSnapshot(publication, 30, 3).Score);
                host.ProcessFrame();
                Assert.That(pacemakerText(host, "next.value"), Is.EqualTo("10"));
                Assert.That(runtime(host, "next.track.value").TransformDrawable.Rotation, Is.EqualTo(10));

                producer = producer.Reset(0, numericSnapshot(publication, 0, 0), GameplaySkinEventResetReason.Retry);
                host.ProcessFrame();
                Assert.That(pacemakerText(host, "next.value"), Is.Empty);
                Assert.That(runtime(host, "next.track.value").TransformDrawable.Rotation, Is.EqualTo(7));
                Assert.That(host.RuntimeFaults, Is.Empty);
            }
            finally
            {
                producer.Dispose();
            }
        }

        [Test]
        public void TestDerivedPersonalBestRequestsRealStorageCapabilityAndKeepsOriginalMissingBestZero()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject scene = numericScene();
            scene["numericSources"] = JArray.Parse("""[{"id":"ratio","operation":"add","left":"score.personal-best","right":0}]""");
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture, scene);
            Assert.That(GameplaySkinEventRuntimeHost.RequiresPersonalBest(publication.PreparedScene.Program), Is.True);
            using var stream = new GameplaySkinEventStream(publication, 0, pacemakerSnapshot(publication, true, 100, null));
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(pacemakerText(host, "next.value"), Is.EqualTo("0"));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        [Test]
        public void TestDerivedPoorStatisticsDoNotChangeNativeScoreAndCountEmptyPoorOnlyInPresentation()
        {
            using Texture texture = new DummyRenderer().CreateTexture(8, 8);
            JObject scene = numericScene();
            scene["numericSources"] = JArray.Parse("""[{"id":"ratio","operation":"add","left":"judgement.count.miss","right":"judgement.count.ok"}]""");
            using GameplaySkinLayoutPublication publication = pacemakerPublication(texture, scene);
            GameplaySkinEventStateSnapshot basis = numericSnapshot(publication, 100, 3);
            var statistics = new GameplaySkinJudgementStatistics(10, 4, 3, 7, 2, 5, 1);
            var snapshot = new GameplaySkinEventStateSnapshot(basis.LifecycleState, basis.Inputs, basis.ActiveObjects, basis.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(100, 3, 3, 1, 0.5, statistics, maximumScore: 200), basis.Timing, basis.BgaViewports);
            using var stream = new GameplaySkinEventStream(publication, 0, snapshot);
            using var host = new GameplaySkinSceneRuntimeHost(publication, stream);
            host.ProcessFrame();
            Assert.That(pacemakerText(host, "next.value"), Is.EqualTo("12"));
            Assert.That(snapshot.Score.Statistics.Miss, Is.EqualTo(5));
            Assert.That(snapshot.Score.Statistics.Ok, Is.EqualTo(7));
            Assert.That(snapshot.Score.Score, Is.EqualTo(100));
            Assert.That(host.RuntimeFaults, Is.Empty);
        }

        private static GameplaySkinEventStateSnapshot numericSnapshot(GameplaySkinLayoutPublication publication, long score, int combo)
        {
            GameplaySkinEventStateSnapshot initial = publication.PreparedScene.InitialEventState;
            return new GameplaySkinEventStateSnapshot(initial.LifecycleState, initial.Inputs, initial.ActiveObjects, initial.CurrentJudgements,
                new GameplaySkinScoreStateSnapshot(score, combo, combo, 1, 0.5, initial.Score.Statistics, maximumScore: 100),
                initial.Timing, initial.BgaViewports);
        }

        private static JObject numericScene()
        {
            JObject scene = pacemakerScene();
            scene["numericSources"] = JArray.Parse("""[{"id":"ratio","operation":"divide","left":"score.value","right":"combo.value"}]""");
            scene["bindings"] = JArray.Parse("""[{"id":"derived.text","target":"next.value","property":"text","source":"value.ratio"}]""");
            scene["variants"] = new JArray();
            scene["tracks"] = JArray.Parse("""
                [{"id":"derived.clock","type":"tween","target":"next.track.value","property":"rotation","easing":"linear","loop":false,"clock":"value.ratio",
                  "keyframes":[{"id":"derived.zero","time":0,"value":0},{"id":"derived.end","time":100,"value":100}]}]
                """);
            scene["root"]!["children"]![8]!["properties"]!["rotation"] = 7;
            return scene;
        }
    }
}
