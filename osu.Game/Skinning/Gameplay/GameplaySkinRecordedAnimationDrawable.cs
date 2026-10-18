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
            IReadOnlyList<GameplaySkinRecordedPiece> pieces = GameplaySkinRecordedAnimationEvaluator.Evaluate(Program, frame);
            for (int i = 0; i < pieces.Count; i++)
            {
                GameplaySkinRecordedPiece piece = pieces[i];
                sprites[i].Apply(piece, piece.Resource == null ? null : textures[piece.Resource]);
            }
            for (int i = pieces.Count; i < sprites.Count; i++)
                sprites[i].Alpha = 0;
        }

        private partial class RecordedPieceSprite : Sprite
        {
            private GameplaySkinRecordedPiece? piece;
            private Texture whitePixel = null!;

            [BackgroundDependencyLoader]
            private void load(ShaderManager shaders, IRenderer renderer)
            {
                TextureShader = shaders.Load("GameplaySkinProjective", "GameplaySkinRecordedAnimation");
                whitePixel = renderer.WhitePixel;
                if (piece?.Resource == null)
                    Texture = whitePixel;
            }

            public void Apply(GameplaySkinRecordedPiece value, Texture? texture)
            {
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
                private GameplaySkinRecordedPiece? piece;
                private Vector2[] positions = [];
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
                    piece = source.piece;
                    if (piece == null)
                        return;
                    positions = [.. piece.Triangles.Select(vertex => source.ToScreenSpace(new Vector2((float)vertex.X * source.DrawWidth, (float)vertex.Y * source.DrawHeight)))];
                    double[] m = piece.ColourMatrix;
                    colours = new RecordedColourData
                    {
                        Multiply = vector(piece.Multiply),
                        Add = vector(piece.Add),
                        Hsl = new Vector4((float)piece.Hsl[0], (float)piece.Hsl[1], (float)piece.Hsl[2], 0),
                        Row0 = new Vector4((float)m[0], (float)m[1], (float)m[2], (float)m[3]),
                        Row1 = new Vector4((float)m[5], (float)m[6], (float)m[7], (float)m[8]),
                        Row2 = new Vector4((float)m[10], (float)m[11], (float)m[12], (float)m[13]),
                        Row3 = new Vector4((float)m[15], (float)m[16], (float)m[17], (float)m[18]),
                        Offset = new Vector4((float)m[4], (float)m[9], (float)m[14], (float)m[19]),
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
                    if (piece == null)
                        return;
                    batch ??= renderer.CreateQuadBatch<TexturedVertex2D>(Math.Clamp(positions.Length / 3, 1, IRenderer.MAX_QUADS), 2);
                    for (triangle = 0; triangle < positions.Length; triangle += 3)
                    {
                        nextCorner = 0;
                        // A degenerate second triangle lets the standard renderer keep texture/atlas ownership,
                        // blend state, masking and backbuffer depth while drawing the original mesh triangle.
                        var quad = new Quad(positions[triangle], positions[triangle + 1], positions[triangle + 2], positions[triangle + 2]);
                        renderer.DrawQuad(Texture, quad, DrawColourInfo.Colour, vertexAction: appendAction);
                    }
                }

                private void append(TexturedVertex2D vertex)
                {
                    // DrawQuad emits BL, BR, TR, TL. Both bottom vertices are the third source triangle vertex.
                    int corner = nextCorner++ switch { 0 or 1 => 2, 2 => 1, _ => 0 };
                    GameplaySkinRecordedProjectedVertex v = piece!.Triangles[triangle + corner];
                    vertex.TexturePosition = new Vector2(vertex.TextureRect.X + (float)v.U * (vertex.TextureRect.Z - vertex.TextureRect.X),
                        vertex.TextureRect.Y + (float)v.V * (vertex.TextureRect.W - vertex.TextureRect.Y));
                    vertex.BlendRange = new Vector2((float)v.Q, 0);
                    batch!.Add(vertex);
                }

                private static Vector4 vector(double[] value) => new((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
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
