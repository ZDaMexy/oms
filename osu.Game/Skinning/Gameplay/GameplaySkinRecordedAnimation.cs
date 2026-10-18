// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace osu.Game.Skinning.Gameplay
{
    public static class GameplaySkinRecordedAnimationBudgets
    {
        public const int MAX_ENCODED_BYTES = 16 * 1024 * 1024;
        public const int MAX_DECODED_BYTES = 128 * 1024 * 1024;
        public const int MAX_PROGRAMS = 32;
        public const int MAX_FRAMES = 3600;
        public const int MAX_NODES = 4096;
        public const int MAX_STATES = 262144;
        public const int MAX_VERTICES = 262144;
        public const int MAX_FRAME_APPLICATIONS = 2_000_000;
        public const int MAX_FRAME_TRIANGLES = 32768;
        public const int MAX_FRAME_TRIANGLE_APPLICATIONS = 2_000_000;
        public const int MAX_HIERARCHY_DEPTH = 32;
        public const int MAX_CLIPPED_VERTICES = 256;
        public const int MAX_TOTAL_STATES = 131072;
        public const int MAX_TOTAL_NODES = 32768;
        public const int MAX_RUNTIME_TRIANGLES = 131072;
        public const double MAX_JUDGEMENT_VISUAL_DURATION = 1000;
        public const double MAX_ABSOLUTE_COORDINATE = 100_000_000;
    }

    public sealed class GameplaySkinRecordedAnimationDefinition
    {
        public string Id { get; }
        public string Path { get; }
        public IReadOnlyList<string> Resources { get; }

        internal GameplaySkinRecordedAnimationDefinition(string id, string path, IEnumerable<string> resources)
        {
            Id = id;
            Path = path;
            Resources = Array.AsReadOnly(resources.ToArray());
        }
    }

    public readonly record struct GameplaySkinRecordedVertex(double X, double Y, double Z, double U, double V);
    public readonly record struct GameplaySkinRecordedNode(int Parent, int Geometry, bool Additive);
    public readonly record struct GameplaySkinRecordedNodeState(int Node, int State);
    public readonly record struct GameplaySkinRecordedCamera(double X, double Y, double Z, double FocalLength);

    public sealed class GameplaySkinRecordedGeometry
    {
        public string? Resource { get; }
        internal double[]? Colour { get; }
        internal IReadOnlyList<GameplaySkinRecordedVertex> Vertices { get; }
        internal IReadOnlyList<int> Triangles { get; }

        internal GameplaySkinRecordedGeometry(string? resource, double[]? colour, GameplaySkinRecordedVertex[] vertices, int[] triangles)
        {
            Resource = resource;
            Colour = colour;
            Vertices = Array.AsReadOnly(vertices);
            Triangles = Array.AsReadOnly(triangles);
        }
    }

    public sealed class GameplaySkinRecordedState
    {
        internal double[] Matrix { get; }
        internal double[] Origin { get; }
        internal double[] Multiply { get; }
        internal double[] Add { get; }
        internal double[] Hsl { get; }
        internal double[]? Mask { get; }
        internal double[]? ColourMatrix { get; }
        internal bool Perspective { get; }

        internal GameplaySkinRecordedState(double[] matrix, double[] origin, bool perspective, double[] multiply,
                                           double[] add, double[] hsl, double[]? mask, double[]? colourMatrix)
        {
            Matrix = matrix;
            Origin = origin;
            Perspective = perspective;
            Multiply = multiply;
            Add = add;
            Hsl = hsl;
            Mask = mask;
            ColourMatrix = colourMatrix;
        }
    }

    public sealed class GameplaySkinRecordedFrame
    {
        internal IReadOnlyList<GameplaySkinRecordedNodeState> Nodes { get; }
        internal GameplaySkinRecordedCamera? Camera { get; }

        internal GameplaySkinRecordedFrame(GameplaySkinRecordedNodeState[] nodes, GameplaySkinRecordedCamera? camera)
        {
            Nodes = Array.AsReadOnly(nodes);
            Camera = camera;
        }
    }

    /// <summary>
    /// Captured, bounded image geometry and discrete local transform states. This is data only: it has no bytecode,
    /// filesystem access or knowledge of the source game's format. A frame retains its original ordered hierarchy.
    /// </summary>
    public sealed class GameplaySkinRecordedAnimationProgram
    {
        public double Width { get; }
        public double Height { get; }
        public double FramesPerSecond { get; }
        public double Duration => (Frames.Count - 1) * 1000 / FramesPerSecond;
        public bool Loop { get; }
        public string Trigger { get; }
        public (int Start, int End)? HeldLoop { get; }
        public int? ReleaseFrame { get; }
        internal IReadOnlyList<GameplaySkinRecordedGeometry> Geometries { get; }
        internal IReadOnlyList<GameplaySkinRecordedNode> Nodes { get; }
        internal IReadOnlyList<GameplaySkinRecordedState> States { get; }
        internal IReadOnlyList<GameplaySkinRecordedFrame> Frames { get; }
        public int FrameCount => Frames.Count;
        internal int MaximumFramePieces { get; }
        internal int MaximumFrameTriangles { get; }
        internal long FrameTriangleApplications { get; }

        internal GameplaySkinRecordedAnimationProgram(double width, double height, double fps, bool loop, string trigger,
                                                       GameplaySkinRecordedGeometry[] geometries, GameplaySkinRecordedNode[] nodes,
                                                       GameplaySkinRecordedState[] states, GameplaySkinRecordedFrame[] frames,
                                                       (int Start, int End)? heldLoop = null, int? releaseFrame = null,
                                                       CancellationToken cancellationToken = default)
        {
            Width = width;
            Height = height;
            FramesPerSecond = fps;
            Loop = loop;
            Trigger = trigger;
            HeldLoop = heldLoop;
            ReleaseFrame = releaseFrame;
            Geometries = Array.AsReadOnly(geometries);
            Nodes = Array.AsReadOnly(nodes);
            States = Array.AsReadOnly(states);
            Frames = Array.AsReadOnly(frames);
            MaximumFramePieces = frames.Max(frame => frame.Nodes.Count(assignment => nodes[assignment.Node].Geometry >= 0));
            // Camera and polygon masks can split one source triangle into several rendered triangles.
            // Evaluate the bounded immutable frames once during preparation so native pools reserve the real
            // worst frame, rather than admitting an understated source-mesh count.
            for (int index = 0; index < frames.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int triangles = GameplaySkinRecordedAnimationEvaluator.Evaluate(this, index, cancellationToken)
                    .Sum(piece => piece.Triangles.Count / 3);
                MaximumFrameTriangles = Math.Max(MaximumFrameTriangles, triangles);
                FrameTriangleApplications += triangles;
                if (FrameTriangleApplications > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLE_APPLICATIONS)
                    throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            }
        }

        internal int FrameAt(double elapsed)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0)
                return -1;
            if (!Loop && elapsed >= Duration)
                return -1;
            return (int)(Math.Floor(elapsed * FramesPerSecond / 1000) % (Frames.Count - 1));
        }

        internal int InputFrameAt(double sincePress, double sinceRelease, bool pressed)
        {
            if (HeldLoop is not { } held)
                return FrameAt(sincePress);
            if (pressed)
            {
                // A seek may retain a held key without reconstructing its earlier press edge. Show the
                // steady held state until a real edge supplies an origin; do not invent an attach clock.
                if (!double.IsFinite(sincePress) || sincePress < 0)
                    return held.Start;
                double frame = Math.Floor(sincePress * FramesPerSecond / 1000);
                return frame < held.End ? (int)frame : held.Start + (int)((frame - held.Start) % (held.End - held.Start));
            }
            if (!double.IsFinite(sinceRelease) || sinceRelease < 0)
                return -1;
            double release = ReleaseFrame!.Value + Math.Floor(sinceRelease * FramesPerSecond / 1000);
            return release < Frames.Count - 1 ? (int)release : -1;
        }
    }
}
