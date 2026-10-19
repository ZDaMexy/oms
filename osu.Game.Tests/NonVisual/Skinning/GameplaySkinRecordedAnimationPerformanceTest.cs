// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning.Gameplay;
using osuTK;

namespace osu.Game.Tests.NonVisual.Skinning
{
    /// <summary>
    /// Opt-in CPU/allocation evidence from a read-only author archive. Uses the real evaluator, animation drawable
    /// and recorded-piece DrawNode.ApplyState; dummy textures deliberately exclude PNG decoding and GPU submission.
    /// </summary>
    [TestFixture]
    public sealed partial class GameplaySkinRecordedAnimationPerformanceTest
    {
        private const int measured_frames = 120;
        private const int warmup_frames = 32;
        private const double presentation_fps = 60;

        [TestCase("explosion")]
        [TestCase("key-beam")]
        [TestCase("full-combo")]
        public void MeasureSelectedAuthorAnimations(string moduleId)
        {
            string? packagePath = Environment.GetEnvironmentVariable("OMS_RECORDED_ANIMATION_BENCHMARK_PACKAGE");
            string? output = Environment.GetEnvironmentVariable("OMS_RECORDED_ANIMATION_BENCHMARK_OUTPUT");

            if (string.IsNullOrEmpty(packagePath) || string.IsNullOrEmpty(output))
                Assert.Ignore("Opt-in animation measurement requires a read-only author archive and an isolated evidence directory.");

            using ZipArchive archive = ZipFile.OpenRead(packagePath!);
            GameplaySkinSceneManifest manifest = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeManifest(
                read(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES)!));
            Assert.That(manifest.ContentCatalogFile, Is.Not.Null);
            GameplaySkinContentCatalog catalog = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeContentCatalog(
                read(manifest.ContentCatalogFile!, GameplaySkinContentBudgets.MAX_CATALOG_BYTES)!));
            GameplaySkinOptionValues options = GameplaySkinOptionValues.Resolve(
                [.. manifest.Options, .. catalog.Modules.Select(module => module.Option)], null, "bms", "7k");
            GameplaySkinContentModule module = catalog.Modules.Single(candidate => candidate.Option.Id == moduleId);
            GameplaySkinContentChoice choice = module.Choices.Single(candidate => candidate.Value == options.Values[moduleId].CanonicalValue);
            GameplaySkinContentVariant? variant = choice.Variants.FirstOrDefault(candidate => candidate.When.All(condition =>
                options.TryGetValue(condition.Key, out GameplaySkinOptionValue value) && value.CanonicalValue == condition.Value));
            GameplaySkinSceneManifest selectedManifest = GameplaySkinPreparedPackage.RequireValid(GameplaySkinSceneCodec.DecodeManifest(
                read(variant?.ManifestFile ?? choice.ManifestFile!, GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES)!));

            var preparation = Stopwatch.StartNew();
            long preparationAllocatedBefore = GC.GetAllocatedBytesForCurrentThread();
            IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram> programs = GameplaySkinRecordedAnimationCodec.PrepareAnimations(
                selectedManifest, read, CancellationToken.None);
            long preparationAllocated = GC.GetAllocatedBytesForCurrentThread() - preparationAllocatedBefore;
            preparation.Stop();
            Assert.That(programs, Is.Not.Empty, "The selected contribution must contain real recorded animation programs.");

            using Texture texture = new DummyRenderer().CreateTexture(2, 2);
            GameplaySkinPreparedSceneResource[] resources = selectedManifest.Resources.Select(resource =>
                new GameplaySkinPreparedSceneResource(resource, "recorded-animation-cpu-probe", 2, 2, texture)).ToArray();
            var inventory = new List<ProgramInventory>();
            var measurements = new List<Measurement>();

            foreach ((string id, GameplaySkinRecordedAnimationProgram program) in programs)
            {
                inventory.Add(describe(id, program));

                foreach (int cloneCount in new[] { 1, 16, 64 })
                {
                    foreach (bool staggered in new[] { false, true })
                    {
                        measurements.Add(measure(id, program, resources, cloneCount, staggered, false));
                        measurements.Add(measure(id, program, resources, cloneCount, staggered, true));
                    }
                }
            }

            Directory.CreateDirectory(output!);
            File.WriteAllText(Path.Combine(output!, moduleId + ".json"), JsonConvert.SerializeObject(new
            {
                module = moduleId,
                selectedChoice = choice.Value,
                scope = "bms/7k author defaults",
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString(),
                logicalProcessors = Environment.ProcessorCount,
                presentationFps = presentation_fps,
                measuredFrames = measured_frames,
                warmupFrames = warmup_frames,
                textureMode = "Shared 2x2 DummyRenderer texture; actual recorded-piece ApplyState, no PNG decoding or GPU submission",
                cpuScope = "Animation application and actual ApplyState only; Blit/screen-space submission work is excluded, so elapsed is not whole-frame rendering performance",
                preparationMilliseconds = preparation.Elapsed.TotalMilliseconds,
                preparationAllocatedBytes = preparationAllocated,
                inventory,
                measurements,
            }, Formatting.Indented));
            TestContext.Progress.WriteLine($"Recorded animation CPU evidence: {moduleId}, {programs.Count} programs, "
                                           + $"{inventory.Sum(item => item.ProjectedTriangles)} projected triangle applications.");

            byte[]? read(string path, int maximumBytes)
            {
                ZipArchiveEntry? entry = archive.GetEntry(path);
                if (entry == null)
                    return null;
                if (entry.Length > maximumBytes)
                    throw new InvalidDataException("The author probe input exceeded the production entry budget.");

                using Stream stream = entry.Open();
                using var bytes = new MemoryStream(checked((int)entry.Length));
                stream.CopyTo(bytes);
                return bytes.ToArray();
            }
        }

        private static ProgramInventory describe(string id, GameplaySkinRecordedAnimationProgram program)
        {
            long pieces = 0;
            long projectedVertices = 0;
            long maximumVerticesPerPiece = 0;
            for (int frameIndex = 0; frameIndex < program.FrameCount; frameIndex++)
            {
                IReadOnlyList<GameplaySkinRecordedPiece> frame = GameplaySkinRecordedAnimationEvaluator.Evaluate(program, frameIndex);
                pieces += frame.Count;
                foreach (GameplaySkinRecordedPiece piece in frame)
                {
                    projectedVertices += piece.Triangles.Count;
                    maximumVerticesPerPiece = Math.Max(maximumVerticesPerPiece, piece.Triangles.Count);
                }
            }

            return new ProgramInventory(id, program.FrameCount, program.FramesPerSecond, program.Duration,
                program.Nodes.Count, program.States.Count, program.MaximumFramePieces, program.MaximumFrameTriangles,
                pieces, projectedVertices / 3, projectedVertices * 5 * sizeof(double), pieces * 31 * sizeof(double),
                maximumVerticesPerPiece, program.PreparedFramePayloadBytes);
        }

        private static Measurement measure(string id, GameplaySkinRecordedAnimationProgram program,
                                           IReadOnlyList<GameplaySkinPreparedSceneResource> resources, int cloneCount, bool staggered, bool includeDrawState)
        {
            var clones = new ProbeDrawable[cloneCount];
            var drawNodes = new PieceDrawNode[cloneCount][];
            try
            {
                for (int i = 0; i < cloneCount; i++)
                {
                    clones[i] = new ProbeDrawable(program, resources)
                    {
                        RelativeSizeAxes = Axes.None,
                        Size = new Vector2((float)program.Width, (float)program.Height),
                    };
                    Assert.That(clones[i].Program, Is.SameAs(program), "Native clones must share one exact prepared animation program.");
                    drawNodes[i] = includeDrawState ? clones[i].CreatePieceDrawNodes() : Array.Empty<PieceDrawNode>();
                }

                for (int frame = 0; frame < warmup_frames; frame++)
                    sampleFrame(frame);

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                long elapsedBefore = Stopwatch.GetTimestamp();
                long drawStateCalls = 0;
                for (int frame = 0; frame < measured_frames; frame++)
                    drawStateCalls += sampleFrame(frame + warmup_frames);
                long elapsed = Stopwatch.GetTimestamp() - elapsedBefore;
                long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                if (includeDrawState)
                    Assert.That(drawStateCalls, Is.GreaterThan(0), "The probe must execute real recorded-piece ApplyState calls.");
                return new Measurement(id, cloneCount, staggered ? "staggered" : "synchronous", includeDrawState,
                    allocated, elapsed * 1000d / Stopwatch.Frequency, cloneCount * measured_frames, drawStateCalls);

                long sampleFrame(int frame)
                {
                    long calls = 0;
                    double cycle = program.Duration;
                    if (program.HeldLoop.HasValue)
                        cycle += (program.FrameCount - 1 - program.ReleaseFrame!.Value) * 1000 / program.FramesPerSecond;

                    for (int cloneIndex = 0; cloneIndex < clones.Length; cloneIndex++)
                    {
                        double phase = staggered ? cloneIndex * cycle / clones.Length : 0;
                        double elapsedTime = (frame * 1000 / presentation_fps + phase) % cycle;
                        if (program.HeldLoop.HasValue)
                            clones[cloneIndex].ApplyInputTime(elapsedTime, elapsedTime - program.Duration, elapsedTime < program.Duration);
                        else
                            clones[cloneIndex].ApplyTime(elapsedTime);

                        foreach (PieceDrawNode piece in drawNodes[cloneIndex])
                        {
                            if (piece.Source.Alpha <= 0)
                                continue;
                            piece.DrawNode.ApplyState();
                            calls++;
                        }
                    }
                    return calls;
                }
            }
            finally
            {
                foreach (PieceDrawNode[]? nodes in drawNodes)
                    if (nodes != null)
                        foreach (PieceDrawNode node in nodes)
                            node.DrawNode.Dispose();
                foreach (ProbeDrawable? clone in clones)
                    clone?.Dispose();
            }
        }

        internal sealed partial class ProbeDrawable : GameplaySkinRecordedAnimationDrawable
        {
            public ProbeDrawable(GameplaySkinRecordedAnimationProgram program, IReadOnlyList<GameplaySkinPreparedSceneResource> resources)
                : base(program, resources)
            {
            }

            public PieceDrawNode[] CreatePieceDrawNodes()
                => InternalChildren.Select(piece => new PieceDrawNode(piece,
                    (DrawNode)piece.GetType().GetMethod("CreateDrawNode", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(piece, null)!)).ToArray();
        }

        internal sealed record PieceDrawNode(Drawable Source, DrawNode DrawNode);

        private sealed record ProgramInventory(string Id, int Frames, double FramesPerSecond, double DurationMilliseconds,
                                              int Nodes, int States, int MaximumFramePieces, int MaximumFrameTriangles,
                                              long ProjectedPieces, long ProjectedTriangles, long VertexPayloadBytes,
                                              long ColourPayloadBytes, long MaximumVerticesPerPiece, long PreparedFramePayloadBytes);

        private sealed record Measurement(string Program, int Clones, string Phase, bool IncludesActualDrawState,
                                          long AllocatedBytes, double ElapsedMilliseconds, int AnimationSamples, long DrawStateCalls);
    }
}
