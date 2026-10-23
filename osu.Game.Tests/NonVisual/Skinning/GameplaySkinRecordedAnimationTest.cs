// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public sealed class GameplaySkinRecordedAnimationTest
    {
        [Test]
        public void TestLoopStartPlaysThePrefixOnceThenRepeatsOnlyTheDeclaredTail()
        {
            JObject json = clip();
            json["fps"] = 100;
            json["loop"] = true;
            json["loopStartFrame"] = 3;
            json["frames"] = new JArray(Enumerable.Range(0, 8).Select(_ => frame(assignments((0, 0)))).Append(frame(new JArray())));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            Assert.Multiple(() =>
            {
                Assert.That(program.LoopStartFrame, Is.EqualTo(3));
                Assert.That(program.Duration, Is.EqualTo(80));
                Assert.That(program.FrameAt(0), Is.Zero);
                Assert.That(program.FrameAt(29.999), Is.EqualTo(2));
                Assert.That(program.FrameAt(30), Is.EqualTo(3));
                Assert.That(program.FrameAt(79.999), Is.EqualTo(7));
                Assert.That(program.FrameAt(80), Is.EqualTo(3));
                Assert.That(program.FrameAt(120), Is.EqualTo(7));
                Assert.That(program.FrameAt(130), Is.EqualTo(3));
                Assert.That(program.FrameAt(10000), Is.EqualTo(5));
                Assert.That(program.FrameAt(-1), Is.EqualTo(-1));
            });
        }

        [TestCase(-1)]
        [TestCase(1)]
        [TestCase(0.5)]
        public void TestLoopStartCannotReferenceTheTerminalFrameOrAnInvalidIndex(double start)
        {
            JObject json = clip();
            json["loop"] = true;
            json["loopStartFrame"] = start;
            Assert.Throws<GameplaySkinScenePreparationException>(() => decode(json));
        }

        [TestCase(60)]
        [TestCase(120)]
        public void TestFractionalMillisecondTerminalBoundaryDoesNotReplayTheLastFrame(double fps)
        {
            JObject json = clip();
            json["fps"] = fps;
            json["frames"] = new JArray(Enumerable.Range(0, 31).Select(_ => frame(assignments((0, 0)))).Append(frame(new JArray())));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            Assert.Multiple(() =>
            {
                Assert.That(program.FrameAt(Math.BitDecrement(program.Duration)), Is.EqualTo(30));
                Assert.That(program.FrameAt(program.Duration), Is.EqualTo(-1));
                Assert.That(program.FrameAt(Math.BitIncrement(program.Duration)), Is.EqualTo(-1));
            });
        }

        [TestCase(0)]
        [TestCase(3)]
        [TestCase(30)]
        public void TestFractionalMillisecondLoopBoundariesRetainOnlyTheDeclaredTail(int start)
        {
            JObject json = clip();
            json["fps"] = 60;
            json["loop"] = true;
            json["loopStartFrame"] = start;
            json["frames"] = new JArray(Enumerable.Range(0, 31).Select(_ => frame(assignments((0, 0)))).Append(frame(new JArray())));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            double period = (31 - start) * 1000d / 60;
            foreach (int cycle in new[] { 0, 1, 2, 3, 7, 1000 })
            {
                double boundary = program.Duration + cycle * period;
                Assert.Multiple(() =>
                {
                    Assert.That(program.FrameAt(Math.BitDecrement(boundary)), Is.EqualTo(30), $"Before cycle {cycle}");
                    Assert.That(program.FrameAt(boundary), Is.EqualTo(start), $"At cycle {cycle}");
                    Assert.That(program.FrameAt(Math.BitIncrement(boundary)), Is.EqualTo(start), $"After cycle {cycle}");
                });
            }
            Assert.That(program.FrameAt(double.MaxValue), Is.InRange(start, 30));
        }

        [TestCase(0, 5200, 2)]
        [TestCase(3, 2400, 4)]
        public void TestInternalLoopFrameBoundariesUseAbsoluteTime(int start, double boundary, int expected)
        {
            JObject json = clip();
            json["fps"] = 60;
            json["loop"] = true;
            json["loopStartFrame"] = start;
            json["frames"] = new JArray(Enumerable.Range(0, 31).Select(_ => frame(assignments((0, 0)))).Append(frame(new JArray())));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            Assert.Multiple(() =>
            {
                Assert.That(program.FrameAt(Math.BitDecrement(boundary)), Is.EqualTo(expected - 1));
                Assert.That(program.FrameAt(boundary), Is.EqualTo(expected));
                Assert.That(program.FrameAt(Math.BitIncrement(boundary)), Is.EqualTo(expected));
            });
        }

        [Test]
        public void TestLoopStartRequiresAContinuousLoopAndDoesNotReplaceInputHeldLoops()
        {
            JObject json = clip();
            json["loopStartFrame"] = 0;
            Assert.Throws<GameplaySkinScenePreparationException>(() => decode(json));
            json["trigger"] = "input.key.down";
            json["loop"] = true;
            json["heldLoop"] = new JArray(0, 1);
            json["releaseFrame"] = 0;
            Assert.Throws<GameplaySkinScenePreparationException>(() => decode(json));
        }

        [Test]
        public void TestRepeatedColourCompositionKeepsSharedSourceStatesAndEachPreparedInstanceComplete()
        {
            JObject json = clip();
            json["nodes"] = new JArray(node(-1, -1), node(0, 0), node(0, 0));
            JObject colour = state();
            colour["multiply"] = new JArray(0.2, 0.4, 0.6, 19d / 255);
            json["states"] = new JArray(state(), colour);
            json["frames"]![0]!["nodes"] = assignments((0, 0), (1, 1), (2, 1));
            byte[] encoded = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            for (int repeat = 0; repeat < 1000; repeat++)
            {
                GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationCodec.Decode(encoded, definition());
                GameplaySkinRecordedPreparedFrame prepared = program.GetPreparedFrame(0);
                Assert.That(prepared.PieceCount, Is.EqualTo(2));
                for (int index = 0; index < prepared.PieceCount; index++)
                    Assert.That(prepared.GetPiece(index).Multiply, Is.EqualTo(new Vector4(0.2f, 0.4f, 0.6f, 19f / 255)));
            }
        }

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

        [TestCase(false)]
        [TestCase(true)]
        public void TestRemainingPackageTriangleBudgetCountsEveryRenderedFrameAndAcceptsItsExactBoundary(bool clipped)
        {
            JObject json = clip();
            if (clipped)
                json["geometries"]![0]!["vertices"] = vertices(
                    new[] { -100d, -100, 0, 0, 0 }, new[] { 300d, -100, 0, 1, 0 }, new[] { -100d, 300, 0, 0, 1 });
            json["frames"] = new JArray(frame(assignments((0, 0))), frame(assignments((0, 0))), frame(new JArray()));
            byte[] encoded = Encoding.UTF8.GetBytes(json.ToString(Formatting.None));
            GameplaySkinRecordedAnimationProgram standalone = GameplaySkinRecordedAnimationCodec.Decode(encoded, definition());
            long required = standalone.FrameTriangleApplications;
            Assert.That(required, clipped ? Is.GreaterThan(2) : Is.EqualTo(2));

            GameplaySkinRecordedAnimationProgram exact = GameplaySkinRecordedAnimationCodec.Decode(encoded, definition(), required, CancellationToken.None);
            Assert.That(exact.FrameTriangleApplications, Is.EqualTo(required));
            Assert.That(exact.PreparedFramePayloadBytes, Is.EqualTo(standalone.PreparedFramePayloadBytes));
            Assert.That(exact.FrameTriangleApplications, Is.EqualTo(required));
            Assert.That(exact.PreparedFramePayloadBytes, Is.EqualTo(standalone.PreparedFramePayloadBytes));
            var failure = Assert.Throws<GameplaySkinScenePreparationException>(() =>
                GameplaySkinRecordedAnimationCodec.Decode(encoded, definition(), required - 1, CancellationToken.None));
            Assert.That(failure!.Code, Is.EqualTo(GameplaySkinSceneDiagnosticCode.BudgetExceeded));
        }

        [Test]
        public void TestAnEmptyProgramCanUseACompletelyConsumedPackageTriangleBudget()
        {
            JObject json = clip();
            json["frames"] = new JArray(frame(new JArray()), frame(new JArray()));
            GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationCodec.Decode(
                Encoding.UTF8.GetBytes(json.ToString(Formatting.None)), definition(), 0, CancellationToken.None);
            Assert.That(program.FrameTriangleApplications, Is.Zero);
            Assert.That(program.PreparedFramePayloadBytes, Is.Zero);
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

        [TestCase(false)]
        [TestCase(true)]
        public void TestPreparedFramesRetainExactlyThePreviouslyRenderedFloatGeometryAndColour(bool perspective)
        {
            JObject json = clip();
            if (perspective)
            {
                json["geometries"]![0]!["vertices"] = vertices(
                    new[] { 50d, 50, -10, 0, 0 }, new[] { 60d, 50, 10, 1, 0 }, new[] { 50d, 60, 10, 0, 1 });
                json["states"]![0]!["projection"] = "perspective";
                json["frames"]![0]!["camera"] = new JArray(50, 50, 0, 50);
            }
            else
            {
                json["geometries"]![0]!["vertices"] = vertices(
                    new[] { -100d, -100, 0, 0, 0 }, new[] { 300d, -100, 0, 1, 0 }, new[] { -100d, 300, 0, 0, 1 });
            }
            json["nodes"]![0]!["blend"] = "additive";
            json["states"]![0]!["multiply"] = new JArray(0.123456789, 0.2, 0.3, 0.4);
            json["states"]![0]!["add"] = new JArray(0.01, 0.02, 0.03, 0.04);
            json["states"]![0]!["hsl"] = new JArray(0.05, 0.06, 0.07);
            json["states"]![0]!["colourMatrix"] = new JArray(Enumerable.Range(0, 20).Select(index => index / 17d));
            GameplaySkinRecordedAnimationProgram program = decode(json);
            IReadOnlyList<GameplaySkinRecordedPiece> evaluated = GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 0);
            GameplaySkinRecordedPreparedFrame prepared = program.GetPreparedFrame(0);
            Assert.That(prepared.PieceCount, Is.EqualTo(evaluated.Count));
            int offset = 0;
            for (int index = 0; index < evaluated.Count; index++)
            {
                GameplaySkinRecordedPiece source = evaluated[index];
                GameplaySkinRecordedRenderPiece actual = prepared.GetPiece(index);
                Assert.Multiple(() =>
                {
                    Assert.That(actual.Resource, Is.EqualTo(source.Resource));
                    Assert.That(actual.Additive, Is.EqualTo(source.Additive));
                    Assert.That(actual.VertexOffset, Is.EqualTo(offset));
                    Assert.That(actual.VertexCount, Is.EqualTo(source.Triangles.Count));
                    Assert.That(actual.Multiply, Is.EqualTo(vector(source.Multiply, 0)));
                    Assert.That(actual.Add, Is.EqualTo(vector(source.Add, 0)));
                    Assert.That(actual.Hsl, Is.EqualTo(new Vector4((float)source.Hsl[0], (float)source.Hsl[1], (float)source.Hsl[2], 0)));
                    Assert.That(actual.Row0, Is.EqualTo(vector(source.ColourMatrix, 0)));
                    Assert.That(actual.Row1, Is.EqualTo(vector(source.ColourMatrix, 5)));
                    Assert.That(actual.Row2, Is.EqualTo(vector(source.ColourMatrix, 10)));
                    Assert.That(actual.Row3, Is.EqualTo(vector(source.ColourMatrix, 15)));
                    Assert.That(actual.Offset, Is.EqualTo(new Vector4((float)source.ColourMatrix[4], (float)source.ColourMatrix[9],
                        (float)source.ColourMatrix[14], (float)source.ColourMatrix[19])));
                });
                foreach (GameplaySkinRecordedProjectedVertex vertex in source.Triangles)
                    Assert.That(prepared.GetVertex(offset++), Is.EqualTo(new GameplaySkinRecordedRenderVertex(
                        (float)vertex.X, (float)vertex.Y, (float)vertex.U, (float)vertex.V, (float)vertex.Q)));
            }
            Assert.That(offset, Is.EqualTo(prepared.VertexCount));
            Assert.That(program.GetPreparedFrame(-1), Is.SameAs(program.GetPreparedFrame(program.FrameCount - 1)));
            Assert.That(program.PreparedFramePayloadBytes, Is.EqualTo((long)prepared.VertexCount * Unsafe.SizeOf<GameplaySkinRecordedRenderVertex>()
                                                                    + (long)prepared.PieceCount * Unsafe.SizeOf<GameplaySkinRecordedRenderPiece>()));
        }

        [Test]
        public void TestNativeClonesRetainTheSamePreparedFrameAndNoMutableEvaluatorBuffers()
        {
            GameplaySkinRecordedAnimationProgram program = decode(clip());
            GameplaySkinRecordedPreparedFrame prepared = program.GetPreparedFrame(0);
            GameplaySkinRecordedRenderPiece piece = prepared.GetPiece(0);
            GameplaySkinRecordedRenderVertex vertex = prepared.GetVertex(0);
            IReadOnlyList<GameplaySkinRecordedPiece> independent = GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 0);
            independent[0].Multiply[0] = 999;
            independent[0].ColourMatrix[0] = 999;
            ((IList<GameplaySkinRecordedProjectedVertex>)independent[0].Triangles).Clear();
            Assert.That(prepared.GetPiece(0), Is.EqualTo(piece));
            Assert.That(prepared.GetVertex(0), Is.EqualTo(vertex));
            Assert.That(program.GetPreparedFrame(0), Is.SameAs(prepared));

            using var texture = new DummyRenderer().CreateTexture(2, 2);
            var resource = new GameplaySkinPreparedSceneResource(
                new GameplaySkinSceneResource("texture", GameplaySkinSceneResourceType.Texture, "texture.png"), "shared-frame", 2, 2, texture);
            using var first = new GameplaySkinRecordedAnimationPerformanceTest.ProbeDrawable(program, new[] { resource });
            using var second = new GameplaySkinRecordedAnimationPerformanceTest.ProbeDrawable(program, new[] { resource });
            foreach (GameplaySkinRecordedAnimationPerformanceTest.ProbeDrawable clone in new[] { first, second })
            {
                clone.ApplyTime(0);
                foreach (GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode node in clone.CreatePieceDrawNodes())
                {
                    Assert.That(node.Source.GetType().GetField("frame", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(node.Source), Is.SameAs(prepared));
                    node.DrawNode.Dispose();
                }
            }
        }

        [Test]
        public void TestPreparedFrameConversionHonoursCancellationAndHasAnExplicitFinitePayloadBound()
        {
            GameplaySkinRecordedAnimationProgram program = decode(clip());
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => GameplaySkinRecordedPreparedFrame.Create(
                GameplaySkinRecordedAnimationEvaluator.Evaluate(program, 0), cancellation.Token));
            Assert.Multiple(() =>
            {
                Assert.That(Unsafe.SizeOf<GameplaySkinRecordedRenderVertex>(), Is.EqualTo(20));
                Assert.That(Unsafe.SizeOf<GameplaySkinRecordedRenderPiece>(), Is.LessThanOrEqualTo(160));
            });
        }

        [Test]
        public void TestActualDrawStateRetainsOnlyItsImmutableFrameWithoutRecurringAllocation()
        {
            GameplaySkinRecordedAnimationProgram program = CreateChangingPreparedFrameProgram();
            Assert.That(program.GetPreparedFrame(0).VertexCount, Is.LessThan(program.GetPreparedFrame(1).VertexCount));
            using var texture = new DummyRenderer().CreateTexture(2, 2);
            var resource = new GameplaySkinPreparedSceneResource(
                new GameplaySkinSceneResource("texture", GameplaySkinSceneResourceType.Texture, "texture.png"), "draw-state", 2, 2, texture);
            using var drawable = new GameplaySkinRecordedAnimationPerformanceTest.ProbeDrawable(program, new[] { resource })
            {
                RelativeSizeAxes = Axes.None,
                Size = new Vector2(100),
            };
            GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode[] nodes = drawable.CreatePieceDrawNodes();
            foreach (GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode node in nodes)
            {
                node.Source.RelativeSizeAxes = Axes.None;
                node.Source.Size = new Vector2(100);
                node.Source.Position = new Vector2(7, 12);
                node.Source.Scale = new Vector2(1.5f);
                node.Source.Rotation = 13;
            }
            Func<int, Vector2>[] positions = nodes.Select(node => node.DrawNode.GetType().GetMethod(
                    "screenPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                .CreateDelegate<Func<int, Vector2>>(node.DrawNode)).ToArray();
            try
            {
                sample(1);
                GameplaySkinRecordedPreparedFrame large = program.GetPreparedFrame(1);
                GameplaySkinRecordedRenderPiece piece = large.GetPiece(0);
                Vector2[] expected = Enumerable.Range(0, piece.VertexCount).Select(index =>
                {
                    GameplaySkinRecordedRenderVertex vertex = large.GetVertex(piece.VertexOffset + index);
                    return nodes[0].Source.ToScreenSpace(new Vector2(vertex.X * nodes[0].Source.DrawWidth, vertex.Y * nodes[0].Source.DrawHeight));
                }).ToArray();
                for (int index = 0; index < expected.Length; index++)
                    Assert.That(positions[0](index), Is.EqualTo(expected[index]));

                sample(0);
                FieldInfo frame = nodes[0].DrawNode.GetType().GetField("frame", BindingFlags.Instance | BindingFlags.NonPublic)!;
                FieldInfo currentPiece = nodes[0].DrawNode.GetType().GetField("piece", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.That(frame.GetValue(nodes[0].DrawNode), Is.SameAs(program.GetPreparedFrame(0)));
                Assert.That(((GameplaySkinRecordedRenderPiece)currentPiece.GetValue(nodes[0].DrawNode)!).VertexCount,
                    Is.EqualTo(program.GetPreparedFrame(0).VertexCount));
                Assert.That(nodes[0].DrawNode.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Any(field => field.FieldType.IsArray), Is.False, "Native clones must not retain per-piece frame-history arrays.");

                for (int index = 0; index < 64; index++)
                    sample(index % 2);

                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int index = 0; index < 2048; index++)
                    sample(index % 2);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero, "Shared prepared frames, actual draw-state application and submitted vertex transforms allocate no recurring buffers.");
            }
            finally
            {
                foreach (GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode node in nodes)
                    node.DrawNode.Dispose();
            }

            void sample(int frameIndex)
            {
                drawable.ApplyTime((frameIndex + 0.25) * 1000 / program.FramesPerSecond);
                GameplaySkinRecordedPreparedFrame prepared = program.GetPreparedFrame(frameIndex);
                for (int index = 0; index < nodes.Length; index++)
                {
                    nodes[index].DrawNode.ApplyState();
                    for (int vertex = 0; vertex < prepared.GetPiece(index).VertexCount; vertex++)
                        _ = positions[index](vertex);
                }
            }
        }

        internal static GameplaySkinRecordedAnimationProgram CreateChangingPreparedFrameProgram()
        {
            JObject json = clip();
            json["loop"] = true;
            json["states"] = new JArray(state(), state(matrix: new[] { 20d, 0, 0, 0, 20, 0, 0, 0, 1, 0, 0, 0 }));
            json["frames"] = new JArray(frame(assignments((0, 0))), frame(assignments((0, 1))), frame(new JArray()));
            return decode(json);
        }

        private static Vector4 vector(double[] value, int start)
            => new((float)value[start], (float)value[start + 1], (float)value[start + 2], (float)value[start + 3]);

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
            ["width"] = width,
            ["height"] = height,
            ["fps"] = 60,
            ["loop"] = false,
            ["trigger"] = "gameplay.attach",
            ["geometries"] = new JArray { new JObject
            {
                ["resource"] = "texture",
                ["vertices"] = vertices(new[] { 0d, 0, 0, 0, 0 }, new[] { 10d, 0, 0, 1, 0 }, new[] { 0d, 10, 0, 0, 1 }),
                ["triangles"] = new JArray(0, 1, 2),
            } },
            ["nodes"] = new JArray { node(-1, 0) },
            ["states"] = new JArray { state() },
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
            ["origin"] = new JArray(origin ?? new[] { 0d, 0, 0 }),
            ["projection"] = "affine",
            ["multiply"] = new JArray(1, 1, 1, 1),
            ["add"] = new JArray(0, 0, 0, 0),
            ["hsl"] = new JArray(0, 0, 0),
            ["mask"] = null,
            ["colourMatrix"] = null,
        };
    }
}
