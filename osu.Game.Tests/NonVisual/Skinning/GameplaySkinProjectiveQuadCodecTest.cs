// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinProjectiveQuadCodecTest
    {
        [TestCase(49.99, 1)]
        [TestCase(50, 2)]
        [TestCase(50.01, 2)]
        [TestCase(99.99, 2)]
        [TestCase(100, 3)]
        public void TestHeldNumericFramesChangeAtTheExactAuthoredBoundary(double time, double expected)
        {
            JObject json = Scene();
            JToken track = json["tracks"]![0]!;
            track["type"] = "tween";
            track["property"] = "x";
            track["keyframes"] = JArray.Parse("""[{"id":"a","time":0,"value":1},{"id":"b","time":50,"value":2},{"id":"c","time":100,"value":3}]""");
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> decoded = Decode(json);
            Assert.That(decoded.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid));
            var prepared = new GameplaySkinPreparedSceneTrack(decoded.Value!.Tracks.Single(), new Dictionary<string, GameplaySkinPreparedSceneResource>());
            MethodInfo sample = typeof(GameplaySkinSceneRuntimeHost).GetMethod("sampleTweenTrack", BindingFlags.Static | BindingFlags.NonPublic)!;
            Assert.That(sample.Invoke(null, new object[] { prepared, time }), Is.EqualTo(expected));
        }

        [TestCase(null, GameplaySkinSceneTextureFiltering.Linear)]
        [TestCase("linear", GameplaySkinSceneTextureFiltering.Linear)]
        [TestCase("nearest", GameplaySkinSceneTextureFiltering.Nearest)]
        public void TestResourceSamplingIsExplicitAndOldManifestsKeepTheirDefault(string? filter, GameplaySkinSceneTextureFiltering expected)
        {
            JObject json = Manifest();
            if (filter != null)
                json["resources"]![0]!["filter"] = filter;
            GameplaySkinSceneManifest manifest = GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None)).Value!;
            Assert.That(manifest.Resources.Single().Filtering, Is.EqualTo(expected));
            string encoded = GameplaySkinSceneCodec.EncodeManifest(manifest);
            Assert.That(GameplaySkinSceneCodec.DecodeManifest(encoded).Value!.Resources.Single().Filtering, Is.EqualTo(expected));
            Assert.That(JObject.Parse(encoded)["resources"]![0]!["filter"]?.Value<string>(), Is.EqualTo(expected == GameplaySkinSceneTextureFiltering.Nearest ? "nearest" : null));
            json["resources"]![0]!["filter"] = "unbounded-author-sampler";
            Assert.That(GameplaySkinSceneCodec.DecodeManifest(json.ToString(Formatting.None)).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }
        [Test]
        public void TestProjectedTextureFramesRoundTripWithSignedWindingAndExactWeights()
        {
            JObject json = Scene();
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result = Decode(json);
            Assert.That(result.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), diagnostics(result));
            GameplaySkinProjectiveQuad quad = result.Value!.Tracks.Single().Keyframes[0].Value.ProjectiveQuad!;
            Assert.Multiple(() =>
            {
                Assert.That(quad.TopLeft, Is.EqualTo(new GameplaySkinProjectiveVertex(-0.2, 0.1, 0.75)));
                Assert.That(quad.BottomRight, Is.EqualTo(new GameplaySkinProjectiveVertex(0.8, 0.9, 2)));
                Assert.That(quad.Opacity, Is.EqualTo(0.6));
            });
            string encoded = GameplaySkinSceneCodec.EncodeScene(result.Value);
            GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> second = Decode(JObject.Parse(encoded));
            Assert.That(second.Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), diagnostics(second));
            Assert.That(GameplaySkinSceneCodec.EncodeScene(second.Value!), Is.EqualTo(encoded));

            var vertices = (JArray)json["tracks"]![0]!["keyframes"]![0]!["value"]!["vertices"]!;
            JToken[] original = vertices.Select(vertex => vertex.DeepClone()).ToArray();
            vertices.Clear();
            foreach (JToken vertex in new[] { original[1], original[0], original[3], original[2] })
                vertices.Add(vertex);
            Assert.That(Decode(json).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Valid), "Both faces retain their own UV corner identities.");
        }

        [TestCase("three-corners")]
        [TestCase("extra-coordinate")]
        [TestCase("zero-w")]
        [TestCase("negative-w")]
        [TestCase("tiny-w")]
        [TestCase("huge-w")]
        [TestCase("huge-position")]
        [TestCase("negative-opacity")]
        [TestCase("high-opacity")]
        [TestCase("unknown-quad-field")]
        [TestCase("primitive-value")]
        [TestCase("ordinary-sprite")]
        [TestCase("tween")]
        [TestCase("linear")]
        [TestCase("static-property")]
        [TestCase("binding")]
        public void TestUnsupportedOrUnboundedProjectedFramesFailClosed(string change)
        {
            JObject json = Scene();
            var track = (JObject)json["tracks"]![0]!;
            var frame = (JObject)track["keyframes"]![0]!;
            var value = (JObject)frame["value"]!;
            var vertices = (JArray)value["vertices"]!;
            switch (change)
            {
                case "three-corners": vertices.RemoveAt(3); break;
                case "extra-coordinate": ((JArray)vertices[0]!).Add(1); break;
                case "zero-w": vertices[0]![2] = 0; break;
                case "negative-w": vertices[0]![2] = -1; break;
                case "tiny-w": vertices[0]![2] = 1e-200; break;
                case "huge-w": vertices[0]![2] = 257; break;
                case "huge-position": vertices[0]![0] = 4.01; break;
                case "negative-opacity": value["opacity"] = -0.01; break;
                case "high-opacity": value["opacity"] = 1.01; break;
                case "unknown-quad-field": value["shader"] = "author.glsl"; break;
                case "primitive-value": frame["value"] = true; break;
                case "ordinary-sprite": json["root"]!["type"] = "sprite"; break;
                case "tween": track["type"] = "tween"; break;
                case "linear": track["easing"] = "linear"; break;
                case "static-property": json["root"]!["properties"]!["projective-quad"] = value.DeepClone(); break;
                case "binding": json["bindings"] = JArray.Parse("""[{"id":"bad","target":"quad","property":"projective-quad","source":"timing.progress"}]"""); break;
            }
            Assert.That(Decode(json).Status, Is.EqualTo(GameplaySkinSceneDecodeStatus.Invalid));
        }

        internal static JObject Scene() => JObject.Parse("""
            {"contract":"oms-gameplay-skin-scene.v1",
             "root":{"id":"quad","type":"projective-sprite","target":{"kind":"global"},"slot":"decoration",
                     "resource":"quad.image","blend":"additive","properties":{},"effects":[],"children":[]},
             "tracks":[{"id":"quad.frames","type":"frame","target":"quad","property":"projective-quad","easing":"step","loop":false,
                         "keyframes":[{"id":"q0","time":0,"value":{"vertices":[[-0.2,0.1,0.75],[1.2,0.1,0.75],[0.8,0.9,2],[0.2,0.9,2]],"opacity":0.6}},
                                      {"id":"q1","time":100,"value":{"vertices":[[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"opacity":0}}]}],
             "stateMachines":[],"bindings":[],"templates":[],"instances":[]}
            """);

        internal static GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> Decode(JObject scene)
        {
            GameplaySkinSceneManifest manifest = GameplaySkinSceneCodec.DecodeManifest(Manifest().ToString(Formatting.None)).Value!;
            return GameplaySkinSceneCodec.DecodeScene(scene.ToString(Formatting.None), manifest);
        }

        internal static JObject Manifest() => JObject.Parse("""
                {"contract":"oms-gameplay-skin-manifest.v1","scene":"gameplay-skin.scene.json",
                 "sceneContract":"oms-gameplay-skin-scene.v1","eventContract":"oms-gameplay-skin-event.v1",
                 "resources":[{"id":"quad.image","type":"texture","path":"quad.png"}]}
                """);

        private static string diagnostics(GameplaySkinSceneDecodeResult<GameplaySkinSceneDocument> result)
            => string.Join(", ", result.Diagnostics.Select(diagnostic => diagnostic.Code));
    }
}
