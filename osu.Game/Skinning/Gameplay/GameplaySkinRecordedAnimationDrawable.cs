// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Shaders.Types;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace osu.Game.Skinning.Gameplay
{
    internal partial class GameplaySkinRecordedAnimationDrawable : CompositeDrawable
    {
        public GameplaySkinRecordedAnimationProgram Program { get; }
        private readonly IReadOnlyDictionary<string, Texture> textures;
        private readonly List<RecordedPieceSprite> sprites = [];
        private int appliedFrame = int.MinValue;

        public GameplaySkinRecordedAnimationDrawable(GameplaySkinRecordedAnimationProgram program, IReadOnlyList<GameplaySkinPreparedSceneResource> resources)
        {
            Program = program;
            textures = resources.Where(resource => resource.Texture != null).ToDictionary(resource => resource.Id, resource => resource.Texture!, StringComparer.Ordinal);
            RelativeSizeAxes = Axes.Both;
            // Native effect pools reuse these exact drawables. Sampling a new frame or applying a judgement
            // only changes retained geometry; it must never construct more drawables during gameplay.
            for (int i = 0; i < program.MaximumFramePieces; i++)
            {
                var sprite = new RecordedPieceSprite { RelativeSizeAxes = Axes.Both, Depth = -i, Alpha = 0 };
                sprites.Add(sprite);
                AddInternal(sprite);
            }
        }

        public void ApplyTime(double elapsed)
            => applyFrame(Program.FrameAt(elapsed));

        public void ApplyInputTime(double sincePress, double sinceRelease, bool pressed)
            => applyFrame(Program.InputFrameAt(sincePress, sinceRelease, pressed));

        private void applyFrame(int frame)
        {
            if (frame == appliedFrame)
                return;
            appliedFrame = frame;
            GameplaySkinRecordedPreparedFrame preparedFrame = Program.GetPreparedFrame(frame);
            for (int i = 0; i < preparedFrame.PieceCount; i++)
            {
                GameplaySkinRecordedRenderPiece piece = preparedFrame.GetPiece(i);
                sprites[i].Apply(preparedFrame, piece, piece.Resource == null ? null : textures[piece.Resource]);
            }
            for (int i = preparedFrame.PieceCount; i < sprites.Count; i++)
                sprites[i].Alpha = 0;
        }

        private partial class RecordedPieceSprite : Sprite
        {
            private GameplaySkinRecordedPreparedFrame? frame;
            private GameplaySkinRecordedRenderPiece piece;
            private Texture whitePixel = null!;

            [BackgroundDependencyLoader]
            private void load(ShaderManager shaders, IRenderer renderer)
            {
                TextureShader = shaders.Load("GameplaySkinProjective", "GameplaySkinRecordedAnimation");
                whitePixel = renderer.WhitePixel;
                if (frame == null || piece.Resource == null)
                    Texture = whitePixel;
            }

            public void Apply(GameplaySkinRecordedPreparedFrame preparedFrame, GameplaySkinRecordedRenderPiece value, Texture? texture)
            {
                frame = preparedFrame;
                piece = value;
                Texture = texture ?? whitePixel;
                Blending = value.Additive ? BlendingParameters.Additive : BlendingParameters.Mixture;
                Alpha = 1;
                Invalidate(Invalidation.DrawInfo);
            }

            protected override DrawNode CreateDrawNode() => new RecordedPieceDrawNode(this);

            // The complete animation canvas is a conservative culling bound. Individual triangles may have
            // negative/mirrored local geometry; their final vertices have already been clipped to this canvas.
            private sealed class RecordedPieceDrawNode : SpriteDrawNode
            {
                private RecordedPieceSprite source => (RecordedPieceSprite)Source;
                private GameplaySkinRecordedPreparedFrame? frame;
                private GameplaySkinRecordedRenderPiece piece;
                private Vector2 drawSize;
                private IVertexBatch<TexturedVertex2D>? batch;
                private IUniformBuffer<RecordedColourData>? buffer;
                private RecordedColourData colours;
                private readonly Action<TexturedVertex2D> appendAction;
                private int triangle;
                private int nextCorner;

                public RecordedPieceDrawNode(RecordedPieceSprite source)
                    : base(source)
                {
                    appendAction = append;
                }

                public override void ApplyState()
                {
                    base.ApplyState();
                    frame = source.frame;
                    if (frame == null)
                        return;
                    piece = source.piece;
                    drawSize = source.DrawSize;
                    colours = new RecordedColourData
                    {
                        Multiply = piece.Multiply,
                        Add = piece.Add,
                        Hsl = piece.Hsl,
                        Row0 = piece.Row0,
                        Row1 = piece.Row1,
                        Row2 = piece.Row2,
                        Row3 = piece.Row3,
                        Offset = piece.Offset,
                    };
                }

                protected override void BindUniformResources(IShader shader, IRenderer renderer)
                {
                    base.BindUniformResources(shader, renderer);
                    buffer ??= renderer.CreateUniformBuffer<RecordedColourData>();
                    buffer.Data = colours;
                    shader.BindUniformBlock("m_RecordedColour", buffer);
                }

                protected override void Blit(IRenderer renderer)
                {
                    if (frame == null)
                        return;
                    batch ??= renderer.CreateQuadBatch<TexturedVertex2D>(Math.Clamp(piece.VertexCount / 3, 1, IRenderer.MAX_QUADS), 2);
                    for (triangle = 0; triangle < piece.VertexCount; triangle += 3)
                    {
                        nextCorner = 0;
                        // A degenerate second triangle lets the standard renderer keep texture/atlas ownership,
                        // blend state, masking and backbuffer depth while drawing the original mesh triangle.
                        Vector2 third = screenPosition(triangle + 2);
                        var quad = new Quad(screenPosition(triangle), screenPosition(triangle + 1), third, third);
                        renderer.DrawQuad(Texture, quad, DrawColourInfo.Colour, vertexAction: appendAction);
                    }
                }

                // Read only the immutable frame and this draw node's frozen DrawInfo/size. Computing each actual
                // submitted vertex once avoids per-piece history buffers whose maxima could accumulate across
                // different frames beyond the publication's simultaneous-triangle reservation.
                private Vector2 screenPosition(int index)
                {
                    GameplaySkinRecordedRenderVertex vertex = frame!.GetVertex(piece.VertexOffset + index);
                    return Vector2Extensions.Transform(new Vector2(vertex.X * drawSize.X, vertex.Y * drawSize.Y), DrawInfo.Matrix);
                }

                private void append(TexturedVertex2D vertex)
                {
                    // DrawQuad emits BL, BR, TR, TL. Both bottom vertices are the third source triangle vertex.
                    int corner = nextCorner++ switch { 0 or 1 => 2, 2 => 1, _ => 0 };
                    GameplaySkinRecordedRenderVertex v = frame!.GetVertex(piece.VertexOffset + triangle + corner);
                    vertex.TexturePosition = new Vector2(vertex.TextureRect.X + v.U * (vertex.TextureRect.Z - vertex.TextureRect.X),
                        vertex.TextureRect.Y + v.V * (vertex.TextureRect.W - vertex.TextureRect.Y));
                    vertex.BlendRange = new Vector2(v.Q, 0);
                    batch!.Add(vertex);
                }

                protected override bool CanDrawOpaqueInterior => false;

                protected override void Dispose(bool isDisposing)
                {
                    base.Dispose(isDisposing);
                    if (!isDisposing)
                        return;
                    batch?.Dispose();
                    buffer?.Dispose();
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            private record struct RecordedColourData
            {
                public UniformVector4 Multiply;
                public UniformVector4 Add;
                public UniformVector4 Hsl;
                public UniformVector4 Row0;
                public UniformVector4 Row1;
                public UniformVector4 Row2;
                public UniformVector4 Row3;
                public UniformVector4 Offset;
            }
        }
    }
}
