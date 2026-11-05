// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinNumericSourcesTest
    {
        [Test]
        public void TestReadOnlyPoorCountCustomTargetAndSignedDifferenceRoundTrip()
        {
            JObject scene = sceneWithSources("""
                [{"id":"poor","operation":"add","left":"judgement.count.miss","right":"judgement.count.ok"},
                 {"id":"custom-product","operation":"multiply","left":"score.maximum","right":83},
                 {"id":"custom-ratio","operation":"divide","left":"value.custom-product","right":100},
                 {"id":"custom","operation":"floor","left":"value.custom-ratio"},
                 {"id":"difference","operation":"subtract","left":"score.value","right":"value.custom"}]
                """);
            GameplaySkinSceneDocument document = decode(scene).Value!;
            Assert.That(document, Is.Not.Null);
            var runtime = new GameplaySkinNumericSourceRuntime(document.NumericSources);
            runtime.Evaluate(operand => operand.SourceId switch
            {
                "judgement.count.miss" => 3,
                "judgement.count.ok" => 4,
                "score.maximum" => 222,
                "score.value" => 100,
                _ => throw new InvalidOperationException(),
            });
            Assert.Multiple(() =>
            {
                Assert.That(runtime.TryGetValue(0, out double poor), Is.True);
                Assert.That(poor, Is.EqualTo(7));
                Assert.That(runtime.TryGetValue(3, out double target), Is.True);
                Assert.That(target, Is.EqualTo(184));
                Assert.That(runtime.TryGetValue(4, out double difference), Is.True);
                Assert.That(difference, Is.EqualTo(-84));
            });
            string encoded = GameplaySkinSceneCodec.EncodeScene(document);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> roundTrip = GameplaySkinSceneCodec.DecodeScene(encoded, manifest());
            Assert.That(roundTrip.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            Assert.That(GameplaySkinSceneCodec.EncodeScene(roundTrip.Value!), Is.EqualTo(encoded));
        }

        [TestCase("add", 3, 4, 7)]
        [TestCase("subtract", 3, 4, -1)]
        [TestCase("multiply", 3, 4, 12)]
        [TestCase("divide", 3, 4, 0.75)]
        [TestCase("min", 3, 4, 3)]
        [TestCase("max", 3, 4, 4)]
        [TestCase("floor", -1.2, 0, -2)]
        [TestCase("ceil", -1.2, 0, -1)]
        [TestCase("multiply32", 1.5, 2, 3)]
        [TestCase("divide32", 1.5, 2, 0.75)]
        public void TestFiniteOperations(string operation, double left, double right, double expected)
        {
            var declaration = new JObject { ["id"] = "value", ["operation"] = operation, ["left"] = left };
            if (operation is not ("floor" or "ceil"))
                declaration["right"] = right;
            GameplaySkinSceneDocument document = decode(sceneWithSources(new JArray(declaration).ToString())).Value!;
            var runtime = new GameplaySkinNumericSourceRuntime(document.NumericSources);
            runtime.Evaluate(_ => throw new InvalidOperationException());
            Assert.That(runtime.TryGetValue(0, out double value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [TestCase("true")]
        [TestCase("\"input.pressed\"")]
        [TestCase("\"score.personal-best-available\"")]
        [TestCase("\"arrival.time\"")]
        [TestCase("\"judgement.offset\"")]
        [TestCase("\"timing.elapsed\"")]
        [TestCase("\"value.self\"")]
        [TestCase("\"unknown\"")]
        public void TestRejectsBooleanLocalClockUnknownAndSelfOperands(string operand)
            => Assert.That(decode(sceneWithSources("[{\"id\":\"self\",\"operation\":\"add\",\"left\":" + operand + ",\"right\":1}]")).Status,
                Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));

        [TestCase("[{\"id\":\"a\",\"operation\":\"add\",\"left\":\"value.b\",\"right\":1},{\"id\":\"b\",\"operation\":\"add\",\"left\":1,\"right\":1}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"divide\",\"left\":1,\"right\":0}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"subtract\",\"left\":1,\"right\":1},{\"id\":\"b\",\"operation\":\"divide\",\"left\":1,\"right\":\"value.a\"}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"multiply\",\"left\":1e308,\"right\":1e308}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"ceil\",\"left\":1.1,\"right\":0}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"eval\",\"left\":1,\"right\":0}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"divide32\",\"left\":\"score.value\",\"right\":1e-100}]")]
        [TestCase("[{\"id\":\"a\",\"operation\":\"divide32\",\"left\":2,\"right\":1e308}]")]
        public void TestRejectsForwardReferenceStaticDivisionZeroOverflowAndUnknownFields(string declarations)
            => Assert.That(decode(sceneWithSources(declarations)).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));

        [Test]
        public void TestUnavailableDivisionPropagatesAndRecoversWithoutRetainingOldValues()
        {
            GameplaySkinSceneDocument document = decode(sceneWithSources("""
                [{"id":"ratio","operation":"divide","left":"score.value","right":"combo.value"},
                 {"id":"up","operation":"ceil","left":"value.ratio"}]
                """)).Value!;
            var runtime = new GameplaySkinNumericSourceRuntime(document.NumericSources);
            double denominator = 3;
            Func<GameplaySkinNumericOperand, double?> read = operand => operand.SourceId == "score.value" ? 10 : denominator;
            runtime.Evaluate(read);
            Assert.That(runtime.TryGetValue(1, out double value), Is.True);
            Assert.That(value, Is.EqualTo(4));
            denominator = 0;
            runtime.Evaluate(read);
            Assert.That(runtime.TryGetValue(0, out value), Is.False);
            Assert.That(double.IsNaN(value), Is.True);
            Assert.That(runtime.TryGetValue(1, out value), Is.False);
            Assert.That(double.IsNaN(value), Is.True);
            denominator = 5;
            runtime.Evaluate(read);
            Assert.That(runtime.TryGetValue(1, out value), Is.True);
            Assert.That(value, Is.EqualTo(2));
        }

        [TestCase(654, 454, 436, 456, false)]
        [TestCase(618, 418, 412, 421, false)]
        [TestCase(400, 267, 267, 269, false)]
        [TestCase(654, 454, 436, 456, true)]
        [TestCase(618, 418, 412, 421, true)]
        [TestCase(400, 267, 267, 269, true)]
        public void TestFloat32PreservesRoundedIntermediateArithmeticAtIntegerBoundaries(int maximum, int best, int lower, int expected, bool fused)
        {
            GameplaySkinSceneDocument document = decode(sceneWithSources(fused ? """
                [{"id":"rate","operation":"float32","left":0.005},
                 {"id":"gap","operation":"subtract","left":"score.maximum","right":"score.personal-best"},
                 {"id":"span","operation":"subtract","left":"score.maximum","right":"score.rank-a"},
                 {"id":"numerator","operation":"multiply32","left":"value.gap","right":"value.rate"},
                 {"id":"ratio","operation":"divide32","left":"value.numerator","right":"value.span"},
                 {"id":"limited","operation":"min","left":"value.ratio","right":"value.rate"},
                 {"id":"positive","operation":"max","left":"value.limited","right":0},
                 {"id":"increment","operation":"multiply32","left":"value.positive","right":"score.maximum"},
                 {"id":"integer","operation":"floor","left":"value.increment"},
                 {"id":"minimum","operation":"max","left":"value.integer","right":1},
                 {"id":"target","operation":"add","left":"score.personal-best","right":"value.minimum"}]
                """ : """
                [{"id":"rate","operation":"float32","left":0.005},
                 {"id":"gap","operation":"subtract","left":"score.maximum","right":"score.personal-best"},
                 {"id":"span","operation":"subtract","left":"score.maximum","right":"score.rank-a"},
                 {"id":"numerator","operation":"multiply","left":"value.gap","right":"value.rate"},
                 {"id":"numerator32","operation":"float32","left":"value.numerator"},
                 {"id":"ratio","operation":"divide","left":"value.numerator32","right":"value.span"},
                 {"id":"ratio32","operation":"float32","left":"value.ratio"},
                 {"id":"limited","operation":"min","left":"value.ratio32","right":"value.rate"},
                 {"id":"positive","operation":"max","left":"value.limited","right":0},
                 {"id":"increment","operation":"multiply","left":"value.positive","right":"score.maximum"},
                 {"id":"increment32","operation":"float32","left":"value.increment"},
                 {"id":"integer","operation":"floor","left":"value.increment32"},
                 {"id":"minimum","operation":"max","left":"value.integer","right":1},
                 {"id":"target","operation":"add","left":"score.personal-best","right":"value.minimum"}]
                """)).Value!;
            Assert.That(document, Is.Not.Null);
            var runtime = new GameplaySkinNumericSourceRuntime(document.NumericSources);
            runtime.Evaluate(operand => operand.SourceId switch
            {
                "score.maximum" => maximum,
                "score.personal-best" => best,
                "score.rank-a" => lower,
                _ => throw new InvalidOperationException(),
            });
            Assert.That(runtime.TryGetValue(document.NumericSources.Count - 1, out double value), Is.True);
            Assert.That(value, Is.EqualTo(expected));
        }

        [TestCase(GameplaySkinOptionKind.Number, GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase(GameplaySkinOptionKind.Toggle, GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase(GameplaySkinOptionKind.Choice, GameplaySkinSceneDecodeStatus.Invalid)]
        public void TestOperandsAcceptNumberOptionsWithoutBooleanOrChoiceCoercion(GameplaySkinOptionKind kind, GameplaySkinSceneDecodeStatus expected)
        {
            var option = new GameplaySkinOptionDefinition("percentage", "Percentage", kind,
                kind == GameplaySkinOptionKind.Number ? "80" : kind == GameplaySkinOptionKind.Toggle ? "true" : "one",
                kind == GameplaySkinOptionKind.Choice ? new[] { new GameplaySkinOptionChoice("one", "One") } : Array.Empty<GameplaySkinOptionChoice>(),
                0, 100, 1, Array.Empty<string>(), Array.Empty<string>(), GameplaySkinOptionEffect.None);
            var options = new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>(), options: new[] { option });
            JObject scene = sceneWithSources("""[{"id":"percent","operation":"divide","left":"option.percentage","right":100}]""");
            Assert.That(GameplaySkinSceneCodec.DecodeScene(scene.ToString(), options).Status, Is.EqualTo(expected));
        }

        [Test]
        public void TestOrderedEvaluationAllocatesNoPerFrameStorageAndPropagatesOverflow()
        {
            GameplaySkinSceneDocument document = decode(sceneWithSources("""
                [{"id":"large","operation":"multiply","left":"score.value","right":1e308},
                 {"id":"derived","operation":"floor","left":"value.large"}]
                """)).Value!;
            var runtime = new GameplaySkinNumericSourceRuntime(document.NumericSources);
            Func<GameplaySkinNumericOperand, double?> reader = _ => 2;
            for (int i = 0; i < 256; i++)
                runtime.Evaluate(reader);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 4096; i++)
                runtime.Evaluate(reader);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(runtime.TryGetValue(0, out _), Is.False);
            Assert.That(runtime.TryGetValue(1, out _), Is.False);
        }

        [TestCase(64, false, GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase(65, false, GameplaySkinSceneDecodeStatus.Invalid)]
        [TestCase(16, true, GameplaySkinSceneDecodeStatus.Valid)]
        [TestCase(17, true, GameplaySkinSceneDecodeStatus.Invalid)]
        public void TestDeclarationAndDependencyDepthBudgets(int count, bool chain, GameplaySkinSceneDecodeStatus expected)
        {
            var declarations = new JArray(Enumerable.Range(0, count).Select(i => new JObject
            {
                ["id"] = $"n{i}",
                ["operation"] = "add",
                ["left"] = chain && i > 0 ? new JValue($"value.n{i - 1}") : new JValue(0),
                ["right"] = 1,
            }));
            Assert.That(decode(sceneWithSources(declarations.ToString())).Status, Is.EqualTo(expected));
        }

        private static GameplaySkinSceneManifest manifest() => new GameplaySkinSceneManifest(Array.Empty<GameplaySkinSceneResource>());

        private static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decode(JObject scene)
            => GameplaySkinSceneCodec.DecodeScene(scene.ToString(), manifest());

        private static JObject sceneWithSources(string declarations)
        {
            JObject scene = JObject.Parse("""
                {"contract":"oms-gameplay-skin-scene.v1","root":{"id":"root","type":"container","target":{"kind":"global"},"slot":"hud.text","blend":"alpha","properties":{},"effects":[],"children":[]},
                 "tracks":[],"stateMachines":[],"bindings":[],"variants":[],"templates":[],"instances":[]}
                """);
            scene["numericSources"] = JArray.Parse(declarations);
            return scene;
        }
    }
}
