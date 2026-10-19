// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace osu.Game.Skinning.Gameplay
{
    public static class GameplaySkinRecordedAnimationCodec
    {
        public const string CONTRACT = "oms-gameplay-recorded-animation.v1";

        internal static IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram> PrepareAnimations(
            GameplaySkinSceneManifest manifest, Func<string, int, byte[]?> read, CancellationToken cancellationToken)
        {
            var result = new Dictionary<string, GameplaySkinRecordedAnimationProgram>(StringComparer.Ordinal);
            long encoded = 0;
            long states = 0;
            long nodes = 0;
            long vertices = 0;
            long applications = 0;
            long triangleApplications = 0;
            foreach (GameplaySkinRecordedAnimationDefinition definition in manifest.Animations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = read(definition.Path, GameplaySkinRecordedAnimationBudgets.MAX_ENCODED_BYTES) ?? throw fail();
                encoded += bytes.Length;
                if (encoded > GameplaySkinRecordedAnimationBudgets.MAX_DECODED_BYTES)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                GameplaySkinRecordedAnimationProgram program = Decode(bytes, definition,
                    GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLE_APPLICATIONS - triangleApplications, cancellationToken);
                states += program.States.Count;
                nodes += program.Nodes.Count;
                vertices += program.Geometries.Sum(geometry => (long)geometry.Vertices.Count);
                applications += program.Frames.Sum(frame => (long)frame.Nodes.Count);
                triangleApplications += program.FrameTriangleApplications;
                if (states > GameplaySkinRecordedAnimationBudgets.MAX_TOTAL_STATES || nodes > GameplaySkinRecordedAnimationBudgets.MAX_TOTAL_NODES
                    || vertices > GameplaySkinRecordedAnimationBudgets.MAX_VERTICES || applications > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_APPLICATIONS
                    || triangleApplications > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLE_APPLICATIONS)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                result.Add(definition.Id, program);
            }
            return new ReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram>(result);
        }

        public static GameplaySkinRecordedAnimationProgram Decode(
            ReadOnlyMemory<byte> encoded, GameplaySkinRecordedAnimationDefinition definition, CancellationToken cancellationToken = default)
            => Decode(encoded, definition, GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLE_APPLICATIONS, cancellationToken);

        internal static GameplaySkinRecordedAnimationProgram Decode(
            ReadOnlyMemory<byte> encoded, GameplaySkinRecordedAnimationDefinition definition, long remainingTriangleApplications, CancellationToken cancellationToken)
        {
            if (encoded.Length > GameplaySkinRecordedAnimationBudgets.MAX_ENCODED_BYTES)
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            byte[] bytes;
            if (definition.Path.EndsWith(".br", StringComparison.Ordinal))
            {
                using var input = new MemoryStream(encoded.ToArray(), false);
                using var compressed = new BrotliStream(input, CompressionMode.Decompress);
                using var output = new MemoryStream();
                byte[] buffer = new byte[64 * 1024];
                int length;
                try
                {
                    while ((length = compressed.Read(buffer)) != 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (output.Length + length > GameplaySkinRecordedAnimationBudgets.MAX_DECODED_BYTES)
                            throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                        output.Write(buffer, 0, length);
                    }
                }
                catch (InvalidDataException) { throw fail(); }
                bytes = output.ToArray();
            }
            else
                bytes = encoded.ToArray();

            try
            {
                using JsonDocument json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 24 });
                JsonElement root = json.RootElement;
                bool held = root.TryGetProperty("heldLoop", out JsonElement heldValue);
                if (held)
                    fields(root, "contract", "width", "height", "fps", "loop", "trigger", "geometries", "nodes", "states", "frames", "heldLoop", "releaseFrame");
                else
                    fields(root, "contract", "width", "height", "fps", "loop", "trigger", "geometries", "nodes", "states", "frames");
                if (text(root, "contract") != CONTRACT)
                    throw fail();
                double width = number(root.GetProperty("width"), 1, 16384);
                double height = number(root.GetProperty("height"), 1, 16384);
                double fps = number(root.GetProperty("fps"), 1, 240);
                bool loop = boolean(root.GetProperty("loop"));
                string trigger = text(root, "trigger");
                if (trigger is not ("gameplay.attach" or "gameplay.complete" or "judgement.hit" or "input.key.down"))
                    throw fail();

                var allowed = definition.Resources.ToHashSet(StringComparer.Ordinal);
                var geometries = new List<GameplaySkinRecordedGeometry>();
                int vertexCount = 0;
                foreach (JsonElement item in array(root, "geometries", GameplaySkinRecordedAnimationBudgets.MAX_NODES))
                {
                    bool solid = item.TryGetProperty("colour", out JsonElement colourValue);
                    if (solid)
                        fields(item, "resource", "colour", "vertices", "triangles");
                    else
                        fields(item, "resource", "vertices", "triangles");
                    string? resource = item.GetProperty("resource").ValueKind == JsonValueKind.Null ? null : text(item, "resource");
                    double[]? colour = solid ? vector(colourValue, 4, 1) : null;
                    if ((resource == null) != solid || resource != null && !allowed.Contains(resource)
                        || colour?.Any(component => component < 0) == true)
                        throw fail();
                    var vertices = new List<GameplaySkinRecordedVertex>();
                    foreach (JsonElement point in array(item, "vertices", GameplaySkinRecordedAnimationBudgets.MAX_VERTICES))
                    {
                        double[] v = vector(point, 5);
                        if (Math.Abs(v[3]) > 4 || Math.Abs(v[4]) > 4)
                            throw fail();
                        vertices.Add(new GameplaySkinRecordedVertex(v[0], v[1], v[2], v[3], v[4]));
                    }
                    vertexCount += vertices.Count;
                    if (vertexCount > GameplaySkinRecordedAnimationBudgets.MAX_VERTICES)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    int[] indices = [.. array(item, "triangles", GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLES * 3).Select(value => integer(value, 0, vertices.Count - 1))];
                    if (indices.Length % 3 != 0 || indices.Length == 0)
                        throw fail();
                    geometries.Add(new GameplaySkinRecordedGeometry(resource, colour, [.. vertices], indices));
                }

                var nodes = new List<GameplaySkinRecordedNode>();
                var depths = new List<int>();
                foreach (JsonElement item in array(root, "nodes", GameplaySkinRecordedAnimationBudgets.MAX_NODES))
                {
                    fields(item, "parent", "geometry", "blend");
                    int parent = integer(item.GetProperty("parent"), -1, nodes.Count - 1);
                    int geometry = integer(item.GetProperty("geometry"), -1, geometries.Count - 1);
                    string blend = text(item, "blend");
                    if (blend is not ("normal" or "additive"))
                        throw fail();
                    int depth = parent < 0 ? 1 : depths[parent] + 1;
                    if (depth > GameplaySkinRecordedAnimationBudgets.MAX_HIERARCHY_DEPTH)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    depths.Add(depth);
                    nodes.Add(new GameplaySkinRecordedNode(parent, geometry, blend == "additive"));
                }

                var states = new List<GameplaySkinRecordedState>();
                foreach (JsonElement item in array(root, "states", GameplaySkinRecordedAnimationBudgets.MAX_STATES))
                {
                    fields(item, "matrix", "origin", "projection", "multiply", "add", "hsl", "mask", "colourMatrix");
                    string projection = text(item, "projection");
                    if (projection is not ("affine" or "perspective"))
                        throw fail();
                    double[]? mask = nullableVector(item.GetProperty("mask"), 4);
                    if (mask != null && (mask[2] < mask[0] || mask[3] < mask[1]))
                        throw fail();
                    states.Add(new GameplaySkinRecordedState(vector(item.GetProperty("matrix"), 12), vector(item.GetProperty("origin"), 3),
                        projection == "perspective", vector(item.GetProperty("multiply"), 4, 16), vector(item.GetProperty("add"), 4, 16),
                        vector(item.GetProperty("hsl"), 3, 16), mask, nullableVector(item.GetProperty("colourMatrix"), 20, 1024)));
                }

                var frames = new List<GameplaySkinRecordedFrame>();
                long applications = 0;
                long sourceTriangles = 0;
                foreach (JsonElement item in array(root, "frames", GameplaySkinRecordedAnimationBudgets.MAX_FRAMES))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    fields(item, "nodes", "camera");
                    double[]? camera = nullableVector(item.GetProperty("camera"), 4);
                    if (camera != null && camera[3] <= 0)
                        throw fail();
                    var assignments = new List<GameplaySkinRecordedNodeState>();
                    var present = new HashSet<int>();
                    var projected = new HashSet<int>();
                    int triangles = 0;
                    foreach (JsonElement pair in array(item, "nodes", GameplaySkinRecordedAnimationBudgets.MAX_NODES))
                    {
                        if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2)
                            throw fail();
                        int node = integer(pair[0], 0, nodes.Count - 1);
                        int state = integer(pair[1], 0, states.Count - 1);
                        if (!present.Add(node) || nodes[node].Parent >= 0 && !present.Contains(nodes[node].Parent))
                            throw fail();
                        if (states[state].Perspective || projected.Contains(nodes[node].Parent))
                        {
                            if (camera == null)
                                throw fail();
                            projected.Add(node);
                        }
                        if (nodes[node].Geometry >= 0)
                            triangles += geometries[nodes[node].Geometry].Triangles.Count / 3;
                        assignments.Add(new GameplaySkinRecordedNodeState(node, state));
                    }
                    applications += assignments.Count;
                    sourceTriangles += triangles;
                    if (applications > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_APPLICATIONS || triangles > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLES
                        || sourceTriangles > GameplaySkinRecordedAnimationBudgets.MAX_FRAME_TRIANGLE_APPLICATIONS)
                        throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                    frames.Add(new GameplaySkinRecordedFrame([.. assignments], camera == null ? null : new GameplaySkinRecordedCamera(camera[0], camera[1], camera[2], camera[3])));
                }
                if (frames.Count < 2 || nodes.Count == 0 || frames[^1].Nodes.Count != 0)
                    throw fail();
                (int Start, int End)? heldLoop = null;
                int? releaseFrame = null;
                if (held)
                {
                    if (trigger != "input.key.down" || loop || heldValue.ValueKind != JsonValueKind.Array || heldValue.GetArrayLength() != 2)
                        throw fail();
                    int start = integer(heldValue[0], 0, frames.Count - 2);
                    int end = integer(heldValue[1], start + 1, frames.Count - 1);
                    releaseFrame = integer(root.GetProperty("releaseFrame"), end, frames.Count - 2);
                    heldLoop = (start, end);
                }
                return new GameplaySkinRecordedAnimationProgram(width, height, fps, loop, trigger, [.. geometries], [.. nodes], [.. states], [.. frames],
                    heldLoop, releaseFrame, remainingTriangleApplications, cancellationToken);
            }
            catch (JsonException) { throw fail(); }
        }

        private static void fields(JsonElement value, params string[] names)
        {
            if (value.ValueKind != JsonValueKind.Object)
                throw fail();
            var found = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonProperty property in value.EnumerateObject())
                if (!names.Contains(property.Name, StringComparer.Ordinal) || !found.Add(property.Name))
                    throw fail();
            if (found.Count != names.Length)
                throw fail();
        }

        private static JsonElement.ArrayEnumerator array(JsonElement parent, string property, int maximum)
        {
            JsonElement value = parent.GetProperty(property);
            if (value.ValueKind != JsonValueKind.Array)
                throw fail();
            if (value.GetArrayLength() > maximum)
                throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            return value.EnumerateArray();
        }

        private static double[] vector(JsonElement value, int length, double maximum = GameplaySkinRecordedAnimationBudgets.MAX_ABSOLUTE_COORDINATE)
        {
            if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() != length)
                throw fail();
            return [.. value.EnumerateArray().Select(component => number(component, -maximum, maximum))];
        }

        private static double[]? nullableVector(JsonElement value, int length, double maximum = GameplaySkinRecordedAnimationBudgets.MAX_ABSOLUTE_COORDINATE)
            => value.ValueKind == JsonValueKind.Null ? null : vector(value, length, maximum);

        private static double number(JsonElement value, double minimum, double maximum)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out double result)
                || !double.IsFinite(result) || result < minimum || result > maximum)
                throw fail();
            return result;
        }

        private static int integer(JsonElement value, int minimum, int maximum)
        {
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result < minimum || result > maximum)
                throw fail();
            return result;
        }

        private static bool boolean(JsonElement value)
            => value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => throw fail() };

        private static string text(JsonElement parent, string property)
        {
            JsonElement value = parent.GetProperty(property);
            if (value.ValueKind != JsonValueKind.String)
                throw fail();
            return value.GetString()!;
        }

        private static GameplaySkinScenePreparationException fail(GameplaySkinSceneDiagnosticCode code = GameplaySkinSceneDiagnosticCode.InvalidReference)
            => new(code);
    }
}
