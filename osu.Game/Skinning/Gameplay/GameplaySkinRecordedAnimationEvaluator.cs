// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace osu.Game.Skinning.Gameplay
{
    internal readonly record struct GameplaySkinRecordedProjectedVertex(double X, double Y, double U, double V, double Q);

    internal sealed record GameplaySkinRecordedPiece(
        string? Resource, bool Additive, double[] Multiply, double[] Add, double[] Hsl, double[] ColourMatrix,
        IReadOnlyList<GameplaySkinRecordedProjectedVertex> Triangles);

    /// <summary>
    /// Evaluates the retained local hierarchy before projection. Clipping happens in homogeneous space at the
    /// camera plane and in projected space at ancestor masks, retaining perspective-correct texture coordinates.
    /// It never replaces a transformed mask with its axis-aligned bounding box.
    /// </summary>
    internal static class GameplaySkinRecordedAnimationEvaluator
    {
        private static readonly double[] identity_matrix = [1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0];
        private static readonly double[] identity_colour_matrix = [1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0];
        private static readonly double[] white = [1, 1, 1, 1];
        private static readonly double[] zero = [0, 0, 0, 0];

        public static IReadOnlyList<GameplaySkinRecordedPiece> Evaluate(GameplaySkinRecordedAnimationProgram program, int frameIndex,
                                                                       CancellationToken cancellationToken = default)
        {
            if (frameIndex < 0)
                return Array.Empty<GameplaySkinRecordedPiece>();

            GameplaySkinRecordedFrame frame = program.Frames[frameIndex];
            var worlds = new World[program.Nodes.Count];
            var pieces = new List<GameplaySkinRecordedPiece>();
            int frameTriangles = 0;
            foreach (GameplaySkinRecordedNodeState assignment in frame.Nodes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameplaySkinRecordedNode node = program.Nodes[assignment.Node];
                GameplaySkinRecordedState state = program.States[assignment.State];
                World? parent = node.Parent < 0 ? null : worlds[node.Parent];
                double[] matrix = compose(state.Matrix, parent?.Matrix ?? identity_matrix);
                // A source origin is subtracted before applying the local transform, not after the parent.
                for (int column = 0; column < 3; column++)
                    matrix[9 + column] -= state.Origin[0] * matrix[column] + state.Origin[1] * matrix[3 + column] + state.Origin[2] * matrix[6 + column];
                double[] multiply = combine(state.Multiply, parent?.Multiply ?? white, (a, b) => a * b);
                double[] add = combine(combine(state.Add, parent?.Multiply ?? white, (a, b) => a * b), parent?.Add ?? zero, (a, b) => a + b);
                double[] hsl = combine(state.Hsl, parent?.Hsl ?? zero, (a, b) => a + b);
                double[] colourMatrix = composeColour(state.ColourMatrix ?? identity_colour_matrix, parent?.ColourMatrix ?? identity_colour_matrix);
                bool perspective = state.Perspective || parent?.Perspective == true;
                var masks = parent == null ? new List<IReadOnlyList<GameplaySkinRecordedProjectedVertex>>() : [.. parent.Masks];
                if (state.Mask is { } mask)
                {
                    var rectangle = new[]
                    {
                        new GameplaySkinRecordedVertex(mask[0], mask[1], 0, 0, 0),
                        new GameplaySkinRecordedVertex(mask[2], mask[1], 0, 0, 0),
                        new GameplaySkinRecordedVertex(mask[2], mask[3], 0, 0, 0),
                        new GameplaySkinRecordedVertex(mask[0], mask[3], 0, 0, 0),
                    };
                    masks.Add(project([.. rectangle.Select(vertex => transform(vertex, matrix))], perspective, frame.Camera, program.Width, program.Height));
                }
                worlds[assignment.Node] = new World(matrix, multiply, add, hsl, colourMatrix, perspective, masks);
                if (node.Geometry < 0)
                    continue;

                GameplaySkinRecordedGeometry geometry = program.Geometries[node.Geometry];
                var triangles = new List<GameplaySkinRecordedProjectedVertex>();
                for (int i = 0; i < geometry.Triangles.Count; i += 3)
                {
                    if (i % 768 == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    var polygon = new List<GameplaySkinRecordedVertex>(3);
                    for (int corner = 0; corner < 3; corner++)
                        polygon.Add(transform(geometry.Vertices[geometry.Triangles[i + corner]], matrix));
                    List<GameplaySkinRecordedProjectedVertex> projected = project(polygon, perspective, frame.Camera, program.Width, program.Height);
                    foreach (IReadOnlyList<GameplaySkinRecordedProjectedVertex> clip in masks)
                    {
                        projected = clipProjected(projected, clip);
                        if (projected.Count == 0)
                            break;
                    }
                    frameTriangles += Math.Max(0, projected.Count - 2);
                    if (frameTriangles > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLES)
                        throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    for (int corner = 1; corner + 1 < projected.Count; corner++)
                    {
                        triangles.Add(normalize(projected[0]));
                        triangles.Add(normalize(projected[corner]));
                        triangles.Add(normalize(projected[corner + 1]));
                    }
                }
                if (triangles.Count > 0)
                    pieces.Add(new GameplaySkinRecordedPiece(geometry.Resource, node.Additive,
                        combine(multiply, geometry.Colour ?? white, (a, b) => a * b), add, hsl, colourMatrix, triangles));
            }
            return pieces;

            GameplaySkinRecordedProjectedVertex normalize(GameplaySkinRecordedProjectedVertex vertex)
                => vertex with { X = vertex.X / program.Width, Y = vertex.Y / program.Height };
        }

        private static GameplaySkinRecordedVertex transform(GameplaySkinRecordedVertex vertex, double[] matrix)
            => vertex with
            {
                X = vertex.X * matrix[0] + vertex.Y * matrix[3] + vertex.Z * matrix[6] + matrix[9],
                Y = vertex.X * matrix[1] + vertex.Y * matrix[4] + vertex.Z * matrix[7] + matrix[10],
                Z = vertex.X * matrix[2] + vertex.Y * matrix[5] + vertex.Z * matrix[8] + matrix[11],
            };

        private static double[] compose(double[] local, double[] parent)
        {
            double[] result = new double[12];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 3; column++)
                {
                    for (int k = 0; k < 3; k++)
                        result[row * 3 + column] += local[row * 3 + k] * parent[k * 3 + column];
                    if (row == 3)
                        result[row * 3 + column] += parent[9 + column];
                }
            return result;
        }

        private static double[] composeColour(double[] local, double[] parent)
        {
            double[] result = new double[20];
            for (int row = 0; row < 4; row++)
                for (int column = 0; column < 5; column++)
                {
                    for (int k = 0; k < 4; k++)
                        result[row * 5 + column] += parent[row * 5 + k] * local[k * 5 + column];
                    if (column == 4)
                        result[row * 5 + column] += parent[row * 5 + 4];
                }
            return result;
        }

        private static double[] combine(double[] local, double[] parent, Func<double, double, double> operation)
        {
            double[] result = new double[local.Length];
            for (int index = 0; index < result.Length; index++)
                result[index] = operation(local[index], parent[index]);
            return result;
        }

        private static List<GameplaySkinRecordedProjectedVertex> project(List<GameplaySkinRecordedVertex> polygon, bool perspective,
                                                                          GameplaySkinRecordedCamera? camera, double width, double height)
        {
            if (!perspective)
                return clipProjected([.. polygon.Select(v => new GameplaySkinRecordedProjectedVertex(v.X, v.Y, v.U, v.V, 1))],
                    new[] { point(0, 0), point(width, 0), point(width, height), point(0, height) });

            GameplaySkinRecordedCamera c = camera ?? throw new InvalidOperationException("A prepared perspective frame requires its camera.");
            double near = Math.Max(0.0001, c.FocalLength / 1_000_000);
            polygon = clipWorld(polygon, v => v.Z - c.Z - near);
            polygon = clipWorld(polygon, v => c.X * (v.Z - c.Z) + (v.X - c.X) * c.FocalLength);
            polygon = clipWorld(polygon, v => (width - c.X) * (v.Z - c.Z) - (v.X - c.X) * c.FocalLength);
            polygon = clipWorld(polygon, v => c.Y * (v.Z - c.Z) + (v.Y - c.Y) * c.FocalLength);
            polygon = clipWorld(polygon, v => (height - c.Y) * (v.Z - c.Z) - (v.Y - c.Y) * c.FocalLength);
            return [.. polygon.Select(v =>
            {
                double q = c.FocalLength / (v.Z - c.Z);
                return new GameplaySkinRecordedProjectedVertex(c.X + (v.X - c.X) * q, c.Y + (v.Y - c.Y) * q, v.U, v.V, q);
            })];
        }

        private static GameplaySkinRecordedProjectedVertex point(double x, double y) => new(x, y, 0, 0, 1);

        private static List<GameplaySkinRecordedVertex> clipWorld(List<GameplaySkinRecordedVertex> polygon, Func<GameplaySkinRecordedVertex, double> distance)
        {
            var result = new List<GameplaySkinRecordedVertex>();
            if (polygon.Count == 0)
                return result;
            GameplaySkinRecordedVertex previous = polygon[^1];
            double previousDistance = distance(previous);
            foreach (GameplaySkinRecordedVertex current in polygon)
            {
                double currentDistance = distance(current);
                if ((currentDistance >= 0) != (previousDistance >= 0))
                {
                    double t = previousDistance / (previousDistance - currentDistance);
                    result.Add(new GameplaySkinRecordedVertex(lerp(previous.X, current.X, t), lerp(previous.Y, current.Y, t), lerp(previous.Z, current.Z, t),
                        lerp(previous.U, current.U, t), lerp(previous.V, current.V, t)));
                }
                if (currentDistance >= 0)
                    result.Add(current);
                previous = current;
                previousDistance = currentDistance;
            }
            return result;
        }

        private static List<GameplaySkinRecordedProjectedVertex> clipProjected(List<GameplaySkinRecordedProjectedVertex> polygon,
                                                                                IReadOnlyList<GameplaySkinRecordedProjectedVertex> clip)
        {
            if (clip.Count < 3)
                return [];
            double area = 0;
            for (int i = 0; i < clip.Count; i++)
                area += clip[i].X * clip[(i + 1) % clip.Count].Y - clip[(i + 1) % clip.Count].X * clip[i].Y;
            if (Math.Abs(area) < 1e-10)
                return [];
            double sign = Math.Sign(area);
            for (int edge = 0; edge < clip.Count && polygon.Count > 0; edge++)
            {
                GameplaySkinRecordedProjectedVertex a = clip[edge];
                GameplaySkinRecordedProjectedVertex b = clip[(edge + 1) % clip.Count];
                double distance(GameplaySkinRecordedProjectedVertex v) => sign * ((b.X - a.X) * (v.Y - a.Y) - (b.Y - a.Y) * (v.X - a.X));
                var result = new List<GameplaySkinRecordedProjectedVertex>();
                GameplaySkinRecordedProjectedVertex previous = polygon[^1];
                double previousDistance = distance(previous);
                foreach (GameplaySkinRecordedProjectedVertex current in polygon)
                {
                    double currentDistance = distance(current);
                    if ((currentDistance >= 0) != (previousDistance >= 0))
                    {
                        double t = previousDistance / (previousDistance - currentDistance);
                        double q = lerp(previous.Q, current.Q, t);
                        result.Add(new GameplaySkinRecordedProjectedVertex(lerp(previous.X, current.X, t), lerp(previous.Y, current.Y, t),
                            lerp(previous.U * previous.Q, current.U * current.Q, t) / q, lerp(previous.V * previous.Q, current.V * current.Q, t) / q, q));
                    }
                    if (currentDistance >= 0)
                        result.Add(current);
                    previous = current;
                    previousDistance = currentDistance;
                }
                if (result.Count > GameplaySkinRecordedAnimationBudgets.MAX_CLIPPED_VERTICES)
                    throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                polygon = result;
            }
            return polygon;
        }

        private static double lerp(double a, double b, double t) => a + (b - a) * t;

        private sealed record World(double[] Matrix, double[] Multiply, double[] Add, double[] Hsl, double[] ColourMatrix, bool Perspective,
                                    IReadOnlyList<IReadOnlyList<GameplaySkinRecordedProjectedVertex>> Masks);
    }
}
