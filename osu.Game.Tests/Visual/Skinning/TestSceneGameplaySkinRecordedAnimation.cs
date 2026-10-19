// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Testing;
using osu.Game.Skinning.Gameplay;
using osu.Game.Tests.NonVisual.Skinning;
using osuTK;

namespace osu.Game.Tests.Visual.Skinning
{
    [HeadlessTest]
    public partial class TestSceneGameplaySkinRecordedAnimation : OsuTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode[] manualDrawNodes = Array.Empty<GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode>();

        [TearDownSteps]
        public void TearDownSteps() => AddStep("release manually captured draw state", () =>
        {
            foreach (GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode node in manualDrawNodes)
                node.DrawNode.Dispose();
            manualDrawNodes = Array.Empty<GameplaySkinRecordedAnimationPerformanceTest.PieceDrawNode>();
        });

        [Test]
        public void TestLoadedDrawStateKeepsItsExactFrameAndTransformWhileTheSourceChanges()
        {
            GameplaySkinRecordedAnimationProgram program = GameplaySkinRecordedAnimationTest.CreateChangingPreparedFrameProgram();
            GameplaySkinRecordedAnimationPerformanceTest.ProbeDrawable drawable = null!;
            Drawable source = null!;
            DrawNode drawNode = null!;
            Func<int, Vector2> position = null!;
            Vector2[] expected = Array.Empty<Vector2>();
            FieldInfo capturedFrame = null!;

            AddStep("mount a real recorded animation and load its shader", () =>
            {
                var resource = new GameplaySkinPreparedSceneResource(
                    new GameplaySkinSceneResource("texture", GameplaySkinSceneResourceType.Texture, "texture.png"),
                    "loaded-recorded-animation", 1, 1, renderer.WhitePixel);
                Child = drawable = new GameplaySkinRecordedAnimationPerformanceTest.ProbeDrawable(program, new[] { resource })
                {
                    RelativeSizeAxes = Axes.None,
                    Size = new Vector2(100),
                };
            });
            AddUntilStep("actual animation subtree is loaded", () => drawable.IsLoaded);
            AddStep("capture the larger clipped frame with a real nonidentity transform", () =>
            {
                manualDrawNodes = drawable.CreatePieceDrawNodes();
                source = manualDrawNodes[0].Source;
                drawNode = manualDrawNodes[0].DrawNode;
                Assert.That(source.IsLoaded, Is.True);
                source.RelativeSizeAxes = Axes.None;
                source.Size = new Vector2(100);
                source.Position = new Vector2(7, 12);
                source.Scale = new Vector2(1.5f);
                source.Rotation = 13;
                drawable.ApplyTime(1.25 * 1000 / program.FramesPerSecond);
                drawNode.ApplyState();
                position = drawNode.GetType().GetMethod("screenPosition", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .CreateDelegate<Func<int, Vector2>>(drawNode);
                capturedFrame = drawNode.GetType().GetField("frame", BindingFlags.Instance | BindingFlags.NonPublic)!;
                GameplaySkinRecordedPreparedFrame frame = program.GetPreparedFrame(1);
                GameplaySkinRecordedRenderPiece piece = frame.GetPiece(0);
                expected = Enumerable.Range(0, piece.VertexCount).Select(index =>
                {
                    GameplaySkinRecordedRenderVertex vertex = frame.GetVertex(piece.VertexOffset + index);
                    return source.ToScreenSpace(new Vector2(vertex.X * source.DrawWidth, vertex.Y * source.DrawHeight));
                }).ToArray();
                for (int index = 0; index < expected.Length; index++)
                    Assert.That(position(index), Is.EqualTo(expected[index]));
            });
            AddStep("change the loaded source transform and select a shorter frame", () =>
            {
                source.Position = new Vector2(42, 54);
                source.Size = new Vector2(120, 80);
                source.Rotation = 27;
                GameplaySkinRecordedPreparedFrame frame = program.GetPreparedFrame(1);
                GameplaySkinRecordedRenderVertex vertex = frame.GetVertex(frame.GetPiece(0).VertexOffset);
                Assert.That(source.ToScreenSpace(new Vector2(vertex.X * source.DrawWidth, vertex.Y * source.DrawHeight)),
                    Is.Not.EqualTo(expected[0]), "The loaded source must actually invalidate its transform before testing the captured state.");
                drawable.ApplyTime(0);
            });
            AddStep("old captured draw state remains frozen until its next application", () =>
            {
                Assert.That(capturedFrame.GetValue(drawNode), Is.SameAs(program.GetPreparedFrame(1)));
                for (int index = 0; index < expected.Length; index++)
                    Assert.That(position(index), Is.EqualTo(expected[index]));

                drawNode.ApplyState();
                GameplaySkinRecordedPreparedFrame frame = program.GetPreparedFrame(0);
                Assert.That(capturedFrame.GetValue(drawNode), Is.SameAs(frame));
                GameplaySkinRecordedRenderPiece piece = frame.GetPiece(0);
                for (int index = 0; index < piece.VertexCount; index++)
                {
                    GameplaySkinRecordedRenderVertex vertex = frame.GetVertex(piece.VertexOffset + index);
                    Assert.That(position(index), Is.EqualTo(source.ToScreenSpace(
                        new Vector2(vertex.X * source.DrawWidth, vertex.Y * source.DrawHeight))));
                }
                Assert.That(position(0), Is.Not.EqualTo(expected[0]));
            });
        }
    }
}
