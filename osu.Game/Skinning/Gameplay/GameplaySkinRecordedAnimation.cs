// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using osuTK;

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
        public int LoopStartFrame { get; }
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
        internal long PreparedFramePayloadBytes { get; }

        private readonly GameplaySkinRecordedPreparedFrame[] preparedFrames;

        internal GameplaySkinRecordedPreparedFrame GetPreparedFrame(int frameIndex)
            => frameIndex < 0 ? GameplaySkinRecordedPreparedFrame.Empty : preparedFrames[frameIndex];

        internal GameplaySkinRecordedAnimationProgram(double width, double height, double fps, bool loop, string trigger,
                                                       GameplaySkinRecordedGeometry[] geometries, GameplaySkinRecordedNode[] nodes,
                                                       GameplaySkinRecordedState[] states, GameplaySkinRecordedFrame[] frames,
                                                       (int Start, int End)? heldLoop = null, int? releaseFrame = null,
                                                       long remainingTriangleApplications = GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLE_APPLICATIONS,
                                                       CancellationToken cancellationToken = default, int loopStartFrame = 0)
        {
            Width = width;
            Height = height;
            FramesPerSecond = fps;
            Loop = loop;
            LoopStartFrame = loopStartFrame;
            Trigger = trigger;
            HeldLoop = heldLoop;
            ReleaseFrame = releaseFrame;
            Geometries = Array.AsReadOnly(geometries);
            Nodes = Array.AsReadOnly(nodes);
            States = Array.AsReadOnly(states);
            Frames = Array.AsReadOnly(frames);
            preparedFrames = new GameplaySkinRecordedPreparedFrame[frames.Length];
            MaximumFramePieces = frames.Max(frame => frame.Nodes.Count(assignment => nodes[assignment.Node].Geometry >= 0));
            // Camera and polygon masks can split one source triangle into several rendered triangles.
            // Evaluate the bounded immutable frames once during preparation so native pools reserve the real
            // worst frame, rather than admitting an understated source-mesh count.
            for (int index = 0; index < frames.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IReadOnlyList<GameplaySkinRecordedPiece> evaluated = GameplaySkinRecordedAnimationEvaluator.Evaluate(this, index, cancellationToken);
                int triangles = evaluated.Sum(piece => piece.Triangles.Count / 3);
                MaximumFrameTriangles = Math.Max(MaximumFrameTriangles, triangles);
                FrameTriangleApplications += triangles;
                // Enforce the package's remaining budget before retaining this frame, so a later program cannot
                // temporarily freeze a second full package's worth of geometry before aggregate validation.
                if (FrameTriangleApplications > remainingTriangleApplications)
                    throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);

                // Validation already solved the complete immutable hierarchy, camera and masks. Retain that exact
                // result once for every native clone instead of evaluating the same polygons again during play.
                // Floats are the exact values the renderer previously cast immediately before using each vertex
                // and colour; the original double-precision evaluator remains the geometry validation authority.
                preparedFrames[index] = GameplaySkinRecordedPreparedFrame.Create(evaluated, cancellationToken);
                PreparedFramePayloadBytes += preparedFrames[index].PayloadBytes;
            }
        }

        internal int FrameAt(double elapsed)
        {
            if (!double.IsFinite(elapsed) || elapsed < 0)
                return -1;
            int end = Frames.Count - 1;
            double duration = Duration;
            if (elapsed < duration)
                return sampleFrame(elapsed, 0, end);
            if (!Loop)
                return -1;

            int tailFrames = end - LoopStartFrame;
            double period = tailFrames * 1000 / FramesPerSecond;
            double cycle = Math.Floor((elapsed - duration) / period);
            double cycleStart = duration + cycle * period;
            // Compare absolute boundaries: multiplying a fractional Duration back by fps can
            // round down, as can subtracting it before taking the tail's remainder.
            if (elapsed < cycleStart)
                cycleStart = duration + --cycle * period;
            else if (elapsed >= duration + (cycle + 1) * period)
                cycleStart = duration + ++cycle * period;
            return LoopStartFrame + sampleFrame(elapsed, cycleStart, tailFrames);
        }

        private int sampleFrame(double elapsed, double origin, int count)
        {
            int frame = (int)Math.Clamp(Math.Floor((elapsed - origin) * FramesPerSecond / 1000), 0, count - 1);
            if (frame > 0 && elapsed < origin + frame * 1000 / FramesPerSecond)
                frame--;
            else if (frame + 1 < count && elapsed >= origin + (frame + 1) * 1000 / FramesPerSecond)
                frame++;
            return frame;
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

    internal readonly record struct GameplaySkinRecordedRenderVertex(float X, float Y, float U, float V, float Q);

    internal readonly record struct GameplaySkinRecordedRenderPiece(
        string? Resource, bool Additive, int VertexOffset, int VertexCount,
        Vector4 Multiply, Vector4 Add, Vector4 Hsl,
        Vector4 Row0, Vector4 Row1, Vector4 Row2, Vector4 Row3, Vector4 Offset);

    /// <summary>
    /// One immutable, already clipped frame shared by every drawable using its exact program. Private flat arrays
    /// avoid per-piece collection/colour-array overhead and expose only value copies to renderer consumers.
    /// </summary>
    internal sealed class GameplaySkinRecordedPreparedFrame
    {
        internal static GameplaySkinRecordedPreparedFrame Empty { get; } = new([], []);

        private readonly GameplaySkinRecordedRenderPiece[] pieces;
        private readonly GameplaySkinRecordedRenderVertex[] vertices;

        public int PieceCount => pieces.Length;
        public int VertexCount => vertices.Length;
        public long PayloadBytes => (long)pieces.Length * Unsafe.SizeOf<GameplaySkinRecordedRenderPiece>()
                                    + (long)vertices.Length * Unsafe.SizeOf<GameplaySkinRecordedRenderVertex>();

        private GameplaySkinRecordedPreparedFrame(GameplaySkinRecordedRenderPiece[] pieces, GameplaySkinRecordedRenderVertex[] vertices)
        {
            this.pieces = pieces;
            this.vertices = vertices;
        }

        public GameplaySkinRecordedRenderPiece GetPiece(int index) => pieces[index];
        public GameplaySkinRecordedRenderVertex GetVertex(int index) => vertices[index];

        internal static GameplaySkinRecordedPreparedFrame Create(IReadOnlyList<GameplaySkinRecordedPiece> source, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Count == 0)
                return Empty;

            int vertexCount = 0;
            foreach (GameplaySkinRecordedPiece piece in source)
                vertexCount = checked(vertexCount + piece.Triangles.Count);

            var pieces = new GameplaySkinRecordedRenderPiece[source.Count];
            var vertices = new GameplaySkinRecordedRenderVertex[vertexCount];
            int offset = 0;
            for (int index = 0; index < pieces.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameplaySkinRecordedPiece piece = source[index];
                double[] colour = piece.ColourMatrix;
                pieces[index] = new GameplaySkinRecordedRenderPiece(piece.Resource, piece.Additive, offset, piece.Triangles.Count,
                    vector(piece.Multiply), vector(piece.Add), new Vector4((float)piece.Hsl[0], (float)piece.Hsl[1], (float)piece.Hsl[2], 0),
                    new Vector4((float)colour[0], (float)colour[1], (float)colour[2], (float)colour[3]),
                    new Vector4((float)colour[5], (float)colour[6], (float)colour[7], (float)colour[8]),
                    new Vector4((float)colour[10], (float)colour[11], (float)colour[12], (float)colour[13]),
                    new Vector4((float)colour[15], (float)colour[16], (float)colour[17], (float)colour[18]),
                    new Vector4((float)colour[4], (float)colour[9], (float)colour[14], (float)colour[19]));

                for (int vertexIndex = 0; vertexIndex < piece.Triangles.Count; vertexIndex++)
                {
                    if (vertexIndex % 768 == 0)
                        cancellationToken.ThrowIfCancellationRequested();
                    GameplaySkinRecordedProjectedVertex vertex = piece.Triangles[vertexIndex];
                    vertices[offset++] = new GameplaySkinRecordedRenderVertex((float)vertex.X, (float)vertex.Y,
                        (float)vertex.U, (float)vertex.V, (float)vertex.Q);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            return new GameplaySkinRecordedPreparedFrame(pieces, vertices);
        }

        private static Vector4 vector(double[] value) => new((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
    }
}
