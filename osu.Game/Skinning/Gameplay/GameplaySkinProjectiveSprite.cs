// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Sprites;
using osuTK;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Draws the four supplied screen-plane corners with projective texture interpolation. Geometry is immutable
    /// prepared frame data, sampled by the same gameplay clock and bounded track evaluator as ordinary sprites.
    /// </summary>
    internal partial class GameplaySkinProjectiveSprite : Sprite
    {
        private GameplaySkinProjectiveQuad? quad;

        public GameplaySkinProjectiveSprite()
        {
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void loadProjectiveShader(ShaderManager shaders)
        {
            TextureShader = shaders.Load("GameplaySkinProjective", "GameplaySkinProjective");
        }

        public void ApplyQuad(GameplaySkinProjectiveQuad value)
        {
            if (ReferenceEquals(quad, value))
                return;

            quad = value;
            Alpha = (float)value.Opacity;
            Invalidate(Invalidation.DrawInfo);
        }

        internal GameplaySkinProjectiveQuad? AppliedQuad => quad;

        protected override Quad ComputeScreenSpaceDrawQuad()
            => quad == null ? base.ComputeScreenSpaceDrawQuad()
                : new Quad(toScreen(quad.TopLeft), toScreen(quad.TopRight), toScreen(quad.BottomLeft), toScreen(quad.BottomRight));

        private Vector2 toScreen(GameplaySkinProjectiveVertex vertex)
            => ToScreenSpace(new Vector2((float)vertex.X * DrawWidth, (float)vertex.Y * DrawHeight));

        protected override DrawNode CreateDrawNode() => new ProjectiveDrawNode(this);

        private sealed class ProjectiveDrawNode : SpriteDrawNode
        {
            private GameplaySkinProjectiveSprite source => (GameplaySkinProjectiveSprite)base.Source;

            private GameplaySkinProjectiveQuad? quad;
            private Quad screenQuad;
            private readonly Action<TexturedVertex2D> appendVertex;
            private IVertexBatch<TexturedVertex2D>? batch;
            private int nextCorner;

            public ProjectiveDrawNode(GameplaySkinProjectiveSprite source)
                : base(source)
            {
                appendVertex = append;
            }

            public override void ApplyState()
            {
                base.ApplyState();
                quad = source.quad;

                if (quad == null)
                    return;

                screenQuad = source.ScreenSpaceDrawQuad;
            }

            protected override void Blit(IRenderer renderer)
            {
                if (quad == null || quad.Opacity == 0)
                    return;

                batch ??= renderer.CreateQuadBatch<TexturedVertex2D>(1, 2);
                nextCorner = 0;
                // DrawQuad supplies atlas coordinates, sRGB vertex colour, texture binding and masking data.
                // Its order is BL, BR, TR, TL; our public corners are TL, TR, BR, BL.
                renderer.DrawQuad(Texture, screenQuad, DrawColourInfo.Colour, vertexAction: appendVertex);
            }

            private void append(TexturedVertex2D vertex)
            {
                float q = (float)quad!.GetVertex(3 - nextCorner++).ReciprocalW;
                // This shader uses the standard vertex's otherwise-zero edge inflation attribute for W.
                // Keep the original vertex intact, including the renderer-owned private backbuffer depth.
                vertex.BlendRange = new Vector2(q, 0);
                batch!.Add(vertex);
            }

            protected override bool CanDrawOpaqueInterior => false;

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                if (isDisposing)
                    batch?.Dispose();
            }
        }

    }
}
