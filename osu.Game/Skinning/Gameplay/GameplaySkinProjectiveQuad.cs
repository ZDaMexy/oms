// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// One already projected texture corner. The positive reciprocal W preserves perspective-correct UVs.
    /// Positions are relative to the node's rectangle; corners retain texture order even when viewed from behind.
    /// </summary>
    public readonly record struct GameplaySkinProjectiveVertex(double X, double Y, double ReciprocalW);

    /// <summary>
    /// Immutable, decode-validated frame of a single textured quad. No author shader, camera or geometry code runs.
    /// </summary>
    public sealed class GameplaySkinProjectiveQuad
    {
        public const double MIN_RECIPROCAL_W = 1.0 / 256;
        public const double MAX_RECIPROCAL_W = 256;

        public GameplaySkinProjectiveVertex TopLeft { get; }
        public GameplaySkinProjectiveVertex TopRight { get; }
        public GameplaySkinProjectiveVertex BottomRight { get; }
        public GameplaySkinProjectiveVertex BottomLeft { get; }
        public double Opacity { get; }

        internal GameplaySkinProjectiveQuad(
            GameplaySkinProjectiveVertex topLeft, GameplaySkinProjectiveVertex topRight,
            GameplaySkinProjectiveVertex bottomRight, GameplaySkinProjectiveVertex bottomLeft, double opacity)
        {
            TopLeft = topLeft;
            TopRight = topRight;
            BottomRight = bottomRight;
            BottomLeft = bottomLeft;
            Opacity = opacity;
        }

        public GameplaySkinProjectiveVertex GetVertex(int index) => index switch
        {
            0 => TopLeft,
            1 => TopRight,
            2 => BottomRight,
            3 => BottomLeft,
            _ => throw new ArgumentOutOfRangeException(nameof(index)),
        };
    }
}
