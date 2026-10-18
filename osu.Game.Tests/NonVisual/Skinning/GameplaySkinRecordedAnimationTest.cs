// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinRecordedAnimationTest
    {
        [Test]
        public void TestPreparedNativePoolBudgetIncludesTrianglesCreatedByCanvasClipping()
        {
            JObject json = clip();
            json["geometries"]![0]!["vertices"] = vertices(
                new[] { -100d, -100, 0, 0, 0 }, new[] { 300d, -100, 0, 1, 0 }, new[] { -100d, 300, 0, 0, 1 });
            GameplaySkinRecordedAnimationProgram program = decode(json);
            int rendered = GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 0).Sum(piece => piece.Triangles.Count / 3);
            Assert.That(rendered, Is.GreaterThan(1), "One original triangle is split into the canvas polygon.");
            Assert.That(program.MaximumFrameTriangles, Is.EqualTo(rendered));
            Assert.That(program.FrameTriangleApplications, Is.EqualTo(rendered));
        }

        [Test]
        public void TestKeyBeamPlaysItsIntroHeldLoopAndReleaseAtTheirExactSourceFrames()
        {
            JObject json = clip();
            json["trigger"] = "input.key.down";
            json["fps"] = 100;
            json["heldLoop"] = new JArray(3, 5);
            json["releaseFrame"] = 5;
            json["frames"] = new JArray(Enumerable.Range(0, 8).Select(_ => frame(assignments((0, 0)))).Append(frame(new JArray())));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            Assert.Multiple(() =>
            {
                Assert.That(program.InputFrameAt(0, -1, true), Is.Zero);
                Assert.That(program.InputFrameAt(29.999, -1, true), Is.EqualTo(2));
                Assert.That(program.InputFrameAt(30, -1, true), Is.EqualTo(3));
                Assert.That(program.InputFrameAt(50, -1, true), Is.EqualTo(3));
                Assert.That(program.InputFrameAt(60, -1, true), Is.EqualTo(4));
                Assert.That(program.InputFrameAt(1000, 0, false), Is.EqualTo(5));
                Assert.That(program.InputFrameAt(1010, 10, false), Is.EqualTo(6));
                Assert.That(program.InputFrameAt(1030, 30, false), Is.EqualTo(-1));
                Assert.That(program.InputFrameAt(0, 100, true), Is.Zero, "A new press starts a fresh intro, even after a previous release.");
                Assert.That(program.InputFrameAt(-1, -1, true), Is.EqualTo(3), "An unknown pre-seek press retains the steady held image without a fabricated clock.");
                Assert.That(program.InputFrameAt(-1, 0, false), Is.EqualTo(5), "A real release plays the out segment even when its older press origin is unknown.");
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestTerminalEmptyFrameDoesNotExtendDurationOrEnterTheLoop(bool loop)
        {
            JObject json = clip();
            json["fps"] = 20;
            json["loop"] = loop;
            ((JArray)json["frames"]!).Insert(1, frame(assignments((0, 0))));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            Assert.Multiple(() =>
            {
                Assert.That(program.FrameCount, Is.EqualTo(3), "Two source frames plus a terminal empty frame.");
                Assert.That(program.Duration, Is.EqualTo(100));
                Assert.That(program.FrameAt(-1), Is.EqualTo(-1));
                Assert.That(program.FrameAt(double.NaN), Is.EqualTo(-1));
                Assert.That(program.FrameAt(double.PositiveInfinity), Is.EqualTo(-1));
                Assert.That(program.FrameAt(0), Is.Zero);
                Assert.That(program.FrameAt(49.999), Is.Zero);
                Assert.That(program.FrameAt(50), Is.EqualTo(1));
                Assert.That(program.FrameAt(99.999), Is.EqualTo(1));
                Assert.That(program.FrameAt(100), Is.EqualTo(loop ? 0 : -1));
                Assert.That(program.FrameAt(150), Is.EqualTo(loop ? 1 : -1));
                Assert.That(GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 2), Is.Empty);
                Assert.That(GameplaySkinRecordedAnimationEvaluator.Evaluate(program, -1), Is.Empty);
            });
            if (loop)
                for (int index = 0; index < 20; index++)
                    Assert.That(program.FrameAt(index * 50), Is.EqualTo(index % 2));
        }

        [Test]
        public void TestChildOriginIsAppliedBeforeTheLocalAndParentTransforms()
        {
            JObject json = clip(400, 400);
            json["geometries"]![0]!["vertices"] = vertices(new[] { 4d, 5, 0, 0, 0 }, new[] { 14d, 5, 0, 1, 0 }, new[] { 4d, 15, 0, 0, 1 });
            json["nodes"] = new JArray(node(-1, -1), node(0, 0));
            JObject parent = state(matrix: new[] { 2d, 0, 0, 0, 3, 0, 0, 0, 1, 100, 200, 0 }, origin: new[] { 10d, 20, 0 });
            JObject child = state(matrix: new[] { 3d, 0, 0, 0, 4, 0, 0, 0, 1, 5, 7, 0 }, origin: new[] { 3d, 4, 0 });
            json["states"] = new JArray(parent, child);
            json["frames"]![0]!["nodes"] = assignments((0, 0), (1, 1));
            GameplaySkinRecordedPiece piece = evaluate(json).Single();
            GameplaySkinRecordedProjectedVertex corner = piece.Triangles.Single(vertex => vertex.U == 0 && vertex.V == 0);
            // ((point - childOrigin) * childScale + childPosition - parentOrigin) * parentScale + parentPosition.
            Assert.That(corner.X, Is.EqualTo(96d / 400).Within(0.000000001));
            Assert.That(corner.Y, Is.EqualTo(173d / 400).Within(0.000000001));
            Assert.That(piece.Triangles.Single(vertex => vertex.U == 1).X, Is.EqualTo(156d / 400).Within(0.000000001));
            Assert.That(piece.Triangles.Single(vertex => vertex.V == 1).Y, Is.EqualTo(293d / 400).Within(0.000000001));
        }

        [Test]
        public void TestNegativeScaleAndShearRetainTheirGeometryAndTextureCoordinates()
        {
            JObject json = clip(200, 200);
            json["states"] = new JArray { state(matrix: new[] { -2d, 0.5, 0, 1, 3, 0, 0, 0, 1, 120, 40, 0 }) };
            GameplaySkinRecordedPiece piece = evaluate(json).Single();
            Assert.That(piece.Triangles, Has.Count.EqualTo(3));
            assertVertex(piece, 0, 0, 120d / 200, 40d / 200);
            assertVertex(piece, 1, 0, 100d / 200, 45d / 200);
            assertVertex(piece, 0, 1, 130d / 200, 70d / 200);
            Assert.That(signedArea(piece.Triangles), Is.LessThan(0), "Mirroring must preserve the real negative winding.");
        }

        [Test]
        public void TestCameraPlaneCrossingClipsBeforeProjectionAndPreservesPerspectiveCorrectUv()
        {
            JObject json = clip();
            json["geometries"]![0]!["vertices"] = vertices(
                new[] { 50d, 50, -10, 0, 0 }, new[] { 60d, 50, 10, 1, 0 }, new[] { 50d, 60, 10, 0, 1 });
            json["states"]![0]!["projection"] = "perspective";
            json["frames"]![0]!["camera"] = new JArray(50, 50, 0, 50);
            IReadOnlyList<GameplaySkinRecordedProjectedVertex> result = evaluate(json).Single().Triangles;
            Assert.That(result, Is.Not.Empty);
            foreach (GameplaySkinRecordedProjectedVertex point in result)
            {
                Assert.That(double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Q), Is.True);
                Assert.That(point.X, Is.InRange(-0.0000001, 1.0000001));
                Assert.That(point.Y, Is.InRange(-0.0000001, 1.0000001));
                Assert.That(point.Q, Is.GreaterThan(0));
                Assert.That(point.U, Is.InRange(-0.0000001, 1.0000001));
                Assert.That(point.V, Is.InRange(-0.0000001, 1.0000001));
                Assert.That(point.U + point.V, Is.LessThanOrEqualTo(1.0000001));
                // This triangle's source x/y/z are 50+10u, 50+10v, -10+20(u+v).
                Assert.That(point.X, Is.EqualTo(0.5 + 0.1 * point.U * point.Q).Within(0.0000001));
                Assert.That(point.Y, Is.EqualTo(0.5 + 0.1 * point.V * point.Q).Within(0.0000001));
            }
            GameplaySkinRecordedProjectedVertex clippedCorner = result.First(point => Math.Abs(point.X - 1) < 0.0000001 && Math.Abs(point.Y - 1) < 0.0000001);
            Assert.Multiple(() =>
            {
                Assert.That(clippedCorner.U, Is.EqualTo(1d / 3).Within(0.0000001));
                Assert.That(clippedCorner.V, Is.EqualTo(1d / 3).Within(0.0000001));
                Assert.That(clippedCorner.Q, Is.EqualTo(15).Within(0.0000001));
            });
        }

        [Test]
        public void TestGeometryEntirelyBehindTheCameraProducesNoTriangles()
        {
            JObject json = clip();
            json["states"]![0]!["projection"] = "perspective";
            json["states"]![0]!["matrix"]![11] = -10;
            json["frames"]![0]!["camera"] = new JArray(50, 50, 0, 50);
            Assert.That(evaluate(json), Is.Empty);
        }

        [Test]
        public void TestRotatedAncestorMaskClipsItsActualPolygonInsteadOfTheBoundingBox()
        {
            JObject json = clip();
            json["geometries"]![0]!["vertices"] = vertices(
                new[] { -20d, -20, 0, 0, 0 }, new[] { 20d, -20, 0, 1, 0 }, new[] { 20d, 20, 0, 1, 1 }, new[] { -20d, 20, 0, 0, 1 });
            json["geometries"]![0]!["triangles"] = new JArray(0, 1, 2, 0, 2, 3);
            json["nodes"] = new JArray(node(-1, -1), node(0, 0));
            double angle = Math.Sqrt(0.5);
            JObject parent = state(matrix: new[] { angle, angle, 0, -angle, angle, 0, 0, 0, 1, 50, 50, 0 });
            parent["mask"] = new JArray(-10, -10, 10, 10);
            json["states"] = new JArray(parent, state());
            json["frames"]![0]!["nodes"] = assignments((0, 0), (1, 1));
            GameplaySkinRecordedPiece result = evaluate(json).Single();
            double area = 0;
            for (int index = 0; index < result.Triangles.Count; index += 3)
                area += Math.Abs(signedArea(result.Triangles.Skip(index).Take(3).ToArray()));
            Assert.That(area, Is.EqualTo(400d / 10000).Within(0.0000001), "A rotated 20 x 20 mask has area400; its axis-aligned bounding box would double it.");
            foreach (GameplaySkinRecordedProjectedVertex vertex in result.Triangles)
                Assert.That(Math.Abs(vertex.X - 0.5) + Math.Abs(vertex.Y - 0.5), Is.LessThanOrEqualTo(Math.Sqrt(200) / 100 + 0.0000001));
        }

        [Test]
        public void TestSolidPrimitiveRetainsItsOriginalRgbaWithoutAPlaceholderTexture()
        {
            JObject json = clip();
            json["geometries"]![0]!["resource"] = null;
            json["geometries"]![0]!["colour"] = new JArray(0.1, 0.2, 0.3, 0.4);
            var definition = new GameplaySkinRecordedAnimationDefinition("clip", "clip.json", Array.Empty<string>());
            GameplaySkinRecordedAnimationProgram program = decode(json, definition);
            GameplaySkinRecordedPiece piece = GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 0).Single();
            Assert.Multiple(() =>
            {
                Assert.That(piece.Resource, Is.Null);
                Assert.That(piece.Multiply, Is.EqualTo(new[] { 0.1, 0.2, 0.3, 0.4 }));
                Assert.That(piece.Add, Is.EqualTo(new[] { 0d, 0, 0, 0 }));
                Assert.That(piece.Triangles, Has.Count.EqualTo(3));
            });
        }

        [TestCase("camera-length")]
        [TestCase("camera-zero-focal")]
        [TestCase("camera-negative-focal")]
        [TestCase("perspective-no-camera")]
        [TestCase("inherited-perspective-no-camera")]
        [TestCase("nan")]
        [TestCase("unknown-resource")]
        [TestCase("resource-and-colour")]
        [TestCase("missing-terminal-empty")]
        [TestCase("future-parent")]
        [TestCase("unavailable-parent")]
        [TestCase("duplicate-frame-node")]
        [TestCase("duplicate-field")]
        public void TestMalformedOrUnresolvedAnimationFailsBeforeEvaluation(string reason)
        {
            JObject json = clip();
            Assert.DoesNotThrow(() => decode(json), "The original fixture must be valid before testing one malformed field.");
            switch (reason)
            {
                case "camera-length": json["frames"]![0]!["camera"] = new JArray(0, 0, 0); break;
                case "camera-zero-focal": json["frames"]![0]!["camera"] = new JArray(0, 0, 0, 0); break;
                case "camera-negative-focal": json["frames"]![0]!["camera"] = new JArray(0, 0, 0, -1); break;
                case "perspective-no-camera": json["states"]![0]!["projection"] = "perspective"; break;
                case "inherited-perspective-no-camera":
                    json["nodes"] = new JArray(node(-1, -1), node(0, 0));
                    json["states"] = new JArray(state(), state());
                    json["states"]![0]!["projection"] = "perspective";
                    json["frames"]![0]!["nodes"] = assignments((0, 0), (1, 1));
                    break;
                case "nan": json["states"]![0]!["matrix"]![0] = double.NaN; break;
                case "unknown-resource": json["geometries"]![0]!["resource"] = "other.texture"; break;
                case "resource-and-colour": json["geometries"]![0]!["colour"] = new JArray(1, 0, 0, 1); break;
                case "missing-terminal-empty": json["frames"]![1]!["nodes"] = assignments((0, 0)); break;
                case "future-parent": json["nodes"]![0]!["parent"] = 0; break;
                case "unavailable-parent":
                    json["nodes"] = new JArray(node(-1, -1), node(0, 0));
                    json["frames"]![0]!["nodes"] = assignments((1, 0));
                    break;
                case "duplicate-frame-node": ((JArray)json["frames"]![0]!["nodes"]!).Add(new JArray(0, 0)); break;
                case "duplicate-field":
                    string duplicated = json.ToString(Formatting.None).Replace("\"fps\":60", "\"fps\":60,\"fps\":60", StringComparison.Ordinal);
                    Assert.Throws<GameplaySkinScenePreparationException>(() => GameplaySkinRecordedAnimationCodec.Decode(Encoding.UTF8.GetBytes(duplicated), definition()));
                    return;
            }
            Assert.Throws<GameplaySkinScenePreparationException>(() => decode(json));
        }

        [TestCase("nodes")]
        [TestCase("frames")]
        [TestCase("hierarchy")]
        [TestCase("frame-triangles")]
        [TestCase("encoded-bytes")]
        public void TestOversizedDataIsRejectedByThePreparationBudget(string reason)
        {
            JObject json = clip();
            Assert.DoesNotThrow(() => decode(json), "The original fixture must be valid before applying a budget overflow.");
            switch (reason)
            {
                case "nodes": json["nodes"] = new JArray(Enumerable.Range(0, GameplaySkinRecordedAnimationBudgets.MAX_NODES + 1).Select(_ => node(-1, 0))); break;
                case "frames": json["frames"] = new JArray(Enumerable.Range(0, GameplaySkinRecordedAnimationBudgets.MAX_FRAMES + 1).Select(_ => frame(new JArray()))); break;
                case "hierarchy":
                    json["nodes"] = new JArray(Enumerable.Range(0, GameplaySkinRecordedAnimationBudgets.MAX_HIERARCHY_DEPTH + 1).Select(index => node(index - 1, -1)));
                    break;
                case "frame-triangles":
                    int triangles = GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLES / 2 + 1;
                    json["geometries"]![0]!["triangles"] = new JArray(Enumerable.Range(0, triangles).SelectMany(_ => new[] { 0, 1, 2 }));
                    json["nodes"] = new JArray(node(-1, 0), node(-1, 0));
                    json["frames"]![0]!["nodes"] = assignments((0, 0), (1, 0));
                    break;
                case "encoded-bytes":
                    GameplaySkinScenePreparationException? oversized = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                        GameplaySkinRecordedAnimationCodec.Decode(new byte[GameplaySkinRecordedAnimationBudgets.MAX_ENCODED_BYTES + 1], definition()));
                    Assert.That(oversized!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
                    return;
            }
            GameplaySkinScenePreparationException? failure = Assert.Throws<GameplaySkinScenePreparationException>(() => decode(json));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        [Test]
        public void TestBrotliBytesDecodeIdenticallyAndCapturedProgramsCannotBeMutatedByCallers()
        {
            JObject json = clip();
            byte[] bytes = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            byte[] compressed;
            using (var target = new MemoryStream())
            {
                using (var compressor = new BrotliStream(target, CompressionLevel.SmallestSize, leaveOpen: true))
                    compressor.Write(bytes);
                compressed = target.ToArray();
            }
            var names = new List<string> { "texture" };
            var compressedDefinition = new GameplaySkinRecordedAnimationDefinition("clip", "clip.json.br", names);
            GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationCodec.Decode(compressed, compressedDefinition);
            GameplaySkinRecordedPiece expected = evaluate(json).Single();
            names.Clear();
            Array.Fill(compressed, (byte)0);
            Array.Fill(bytes, (byte)0);
            GameplaySkinRecordedPiece actual = GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 0).Single();
            Assert.That(actual.Triangles, Is.EqualTo(expected.Triangles));
            Assert.Throws<NotSupportedException>(() => ((IList<string>)compressedDefinition.Resources).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<GameplaySkinRecordedGeometry>)program.Geometries).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<GameplaySkinRecordedNode>)program.Nodes).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<GameplaySkinRecordedState>)program.States).Clear());
            Assert.Throws<NotSupportedException>(() => ((IList<GameplaySkinRecordedFrame>)program.Frames).Clear());

            var manifest = new GameplaySkinSceneManifest(new[] { new GameplaySkinSceneResource("texture", GameplaySkinSceneResourceType.Texture, "texture.png") },
                animations: new[] { definition() });
            IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram> prepared = GameplaySkinRecordedAnimationCodec.PrepareAnimations(manifest,
                (_, _) => Encoding.UTF8.GetBytes(json.ToString(Formatting.None)), CancellationToken.None);
            Assert.Throws<NotSupportedException>(() => ((IDictionary<string, GameplaySkinRecordedAnimationProgram>)prepared).Clear());
        }

        private static void assertVertex(GameplaySkinRecordedPiece piece, double u, double v, double x, double y)
        {
            GameplaySkinRecordedProjectedVertex vertex = piece.Triangles.Single(point => point.U == u && point.V == v);
            Assert.That(vertex.X, Is.EqualTo(x).Within(0.000000001));
            Assert.That(vertex.Y, Is.EqualTo(y).Within(0.000000001));
            Assert.That(vertex.Q, Is.EqualTo(1));
        }

        private static double signedArea(IReadOnlyList<GameplaySkinRecordedProjectedVertex> triangle)
            => ((triangle[1].X - triangle[0].X) * (triangle[2].Y - triangle[0].Y)
                - (triangle[1].Y - triangle[0].Y) * (triangle[2].X - triangle[0].X)) / 2;

        private static IReadOnlyList<GameplaySkinRecordedPiece> evaluate(JObject json)
            => GameplaySkinRecordedAnimationEvaluator.Evaluate(decode(json), 0);

        private static GameplaySkinRecordedAnimationProgram decode(JObject json, GameplaySkinRecordedAnimationDefinition? metadata = null)
            => GameplaySkinRecordedAnimationCodec.Decode(Encoding.UTF8.GetBytes(json.ToString(Formatting.None)), metadata ?? definition());

        private static GameplaySkinRecordedAnimationDefinition definition() => new GameplaySkinRecordedAnimationDefinition("clip", "clip.json", new[] { "texture" });

        private static JObject clip(double width = 100, double height = 100) => new JObject
        {
            ["contract"] = GameplaySkinRecordedAnimationCodec.CONTRACT,
            ["width"] = width, ["height"] = height, ["fps"] = 60, ["loop"] = false, ["trigger"] = "gameplay.attach",
            ["geometries"] = new JArray { new JObject
            {
                ["resource"] = "texture",
                ["vertices"] = vertices(new[] { 0d, 0, 0, 0, 0 }, new[] { 10d, 0, 0, 1, 0 }, new[] { 0d, 10, 0, 0, 1 }),
                ["triangles"] = new JArray(0, 1, 2),
            } },
            ["nodes"] = new JArray { node(-1, 0) }, ["states"] = new JArray { state() },
            ["frames"] = new JArray { frame(assignments((0, 0))), frame(new JArray()) },
        };

        private static JArray vertices(params double[][] points) => new JArray(points.Select(point => new JArray(point)));

        private static JArray assignments(params (int Node, int State)[] pairs)
            => new JArray(pairs.Select(pair => new JArray(pair.Node, pair.State)));

        private static JObject node(int parent, int geometry) => new JObject { ["parent"] = parent, ["geometry"] = geometry, ["blend"] = "normal" };

        private static JObject frame(JArray nodes) => new JObject { ["nodes"] = nodes, ["camera"] = null };

        private static JObject state(double[]? matrix = null, double[]? origin = null) => new JObject
        {
            ["matrix"] = new JArray(matrix ?? new[] { 1d, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 }),
            ["origin"] = new JArray(origin ?? new[] { 0d, 0, 0 }), ["projection"] = "affine",
            ["multiply"] = new JArray(1, 1, 1, 1), ["add"] = new JArray(0, 0, 0, 0), ["hsl"] = new JArray(0, 0, 0),
            ["mask"] = null, ["colourMatrix"] = null,
        };
    }
}
