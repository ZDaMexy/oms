// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Tools.IidxSkinBake;

internal sealed class BakeWriter
{
    private readonly BakeInput input;
    private readonly BakeGame game;
    public BakeWriter(BakeInput input, BakeGame game)
    {
        this.input = input;
        this.game = game;
    }

    public async Task Run()
    {
        var frames = new List<BakedFrame>();
        var verification = new List<object>();
        using var atlas = new AtlasWriter(input.Output, input.PageSize);
        var identicalFrames = new Dictionary<string, BakedFrame>(StringComparer.Ordinal);
        bool passed = true;
        int worst = 0;
        double worstMean = 0;
        double worstMeanExcess = 0;
        foreach (int frame in input.Frames)
        {
            string identity = game.PreparedFrameIdentity(frame);
            if (identicalFrames.TryGetValue(identity, out BakedFrame? previous))
            {
                frames.Add(new BakedFrame(frame, frame * 1000 / input.Animation.FramesPerSecond,
                    frame == input.Animation.FrameCount - 1 ? 0 : 1000 / input.Animation.FramesPerSecond,
                    frame == input.Animation.FrameCount - 1, previous.Runs));
                verification.Add(new { sourceFrame = frame, reusedFromSourceFrame = previous.SourceFrame, exactPreparedFrameSha256 = identity });
                continue;
            }
            Console.WriteLine($"Frame {frame}/{input.Animation.FrameCount - 1}");
            var runs = new List<BakedRun>();
            var entries = new List<RunEntry>();
            BlendRun[] sourceRuns = game.Runs(frame).SelectMany(run =>
                run.Additive ? new[] { run } : Enumerable.Range(0, (run.Count + input.NormalRunPieceLimit - 1) / input.NormalRunPieceLimit)
                    .Select(part => new BlendRun(run.First + part * input.NormalRunPieceLimit,
                        Math.Min(input.NormalRunPieceLimit, run.Count - part * input.NormalRunPieceLimit), false))).ToArray();
            for (int runIndex = 0; runIndex < sourceRuns.Length; runIndex++)
            {
                BlendRun run = sourceRuns[runIndex];
                using Image<Rgba32> raw = await game.CaptureOriginal(frame, run, new Color4(0f, 0f, 0f, 0f));
                if (input.KeepVerificationImages)
                    await raw.SaveAsPngAsync(Path.Combine(input.Output, $"probe-{frame:D4}-run-{runIndex:D2}-raw.png"));
                BakedRun? converted = ConvertRun(raw, run.Additive);
                if (converted != null)
                {
                    runs.Add(converted);
                    AtlasPlacement location = await atlas.Add(converted.Pixels);
                    entries.Add(new RunEntry(runIndex, run.Additive ? "additive" : "normal", location.Page,
                        location.Rect, new[] { converted.X, converted.Y }, new[] { run.First, run.Count }, location.PixelSha256));
                }
            }
            var checks = new List<object>();
            var backgrounds = new[] { ("black", Color4.Black), ("white", Color4.White), ("colour", new Color4(0.18f, 0.43f, 0.71f, 1)) };
            foreach (var (name, colour) in backgrounds)
            {
                using Image<Rgba32> original = await game.CaptureOriginal(frame, null, colour);
                using Image<Rgba32> recomposed = await game.CaptureBaked(runs, colour);
                var difference = Compare(original, recomposed);
                passed &= difference.MaxChannelDifference <= 4 && difference.MeanChannelExcessAboveOne <= 0.05;
                worst = Math.Max(worst, difference.MaxChannelDifference);
                worstMean = Math.Max(worstMean, difference.MeanChannelDifference);
                worstMeanExcess = Math.Max(worstMeanExcess, difference.MeanChannelExcessAboveOne);
                checks.Add(new { background = name, difference });
                if (input.KeepVerificationImages)
                {
                    await original.SaveAsPngAsync(Path.Combine(input.Output, $"probe-{frame:D4}-{name}-original.png"));
                    await recomposed.SaveAsPngAsync(Path.Combine(input.Output, $"probe-{frame:D4}-{name}-recomposed.png"));
                }
                Console.WriteLine($"  {name}: max={difference.MaxChannelDifference}, mean={difference.MeanChannelDifference:F5}, >3={difference.PixelsAboveThree}");
            }
            verification.Add(new { sourceFrame = frame, exactPreparedFrameSha256 = identity, checks });
            var bakedFrame = new BakedFrame(frame, frame * 1000 / input.Animation.FramesPerSecond,
                frame == input.Animation.FrameCount - 1 ? 0 : 1000 / input.Animation.FramesPerSecond,
                frame == input.Animation.FrameCount - 1, entries);
            frames.Add(bakedFrame);
            identicalFrames.Add(identity, bakedFrame);
            foreach (BakedRun run in runs)
                run.Pixels.Dispose();
        }
        await atlas.Flush();
        await atlas.VerifySavedPixels();
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        await File.WriteAllTextAsync(Path.Combine(input.Output, "verification.json"), JsonSerializer.Serialize(new
        {
            passed, tolerance = new { max8BitChannelDifference = 4, maxMean8BitChannelExcessAboveOne = 0.05,
                reason = "Allow the one-LSB RGBA8 coverage quantization floor while retaining the unmodified raw mean" },
            worst8BitChannelDifference = worst, worstMean8BitChannelDifference = worstMean,
            worstMean8BitChannelExcessAboveOne = worstMeanExcess,
            scope = "Every distinct evaluated frame, full opaque black/white/colour backgrounds, production RecordedAnimationDrawable versus ordered baked sprites at integer native pixels; byte-identical prepared vertices/materials/order reuse a checked frame without changing source timing",
            exactAtlasInverseVerified = true,
            distinctEvaluatedFrames = identicalFrames.Count,
            frames = verification,
        }, jsonOptions));
        if (!passed)
            throw new InvalidDataException("The baked pixels failed the declared production-renderer comparison tolerance. No index was published.");
        input.VerifyUnchanged();
        var program = input.Animation;
        var index = new
        {
            contract = "oms-iidx-baked-animation.v1",
            canvas = new[] { (int)program.Width, (int)program.Height },
            coordinateSystem = "top-left; x-right; y-down",
            registrationPoint = new[] { 0, 0 },
            framesPerSecond = program.FramesPerSecond,
            sourceFrameCount = program.FrameCount,
            durationMs = program.Duration,
            program.Loop,
            program.LoopStartFrame,
            program.Trigger,
            heldLoop = program.HeldLoop is { } held ? new[] { held.Start, held.End } : null,
            program.ReleaseFrame,
            selectionComplete = input.Frames.Length == program.FrameCount,
            selectedFrames = input.Frames,
            normalRunPieceLimit = input.NormalRunPieceLimit,
            encoding = new
            {
                normal = "straight RGBA8; RGB unpremultiplied from byte-space framebuffer using measured Porter-Duff coverage; destination blend SRC_ALPHA, ONE_MINUS_SRC_ALPHA",
                additive = "RGBA8 contribution: RGB already weighted by all original source alphas; A=255 wherever the contribution is nonzero, otherwise 0; destination blend SRC_ALPHA, ONE",
                alphaScope = "Opaque final destinations; native OMS alpha accumulation is not preserved as destination coverage",
                colourSpace = "Source PNG byte values and the supplied OMS renderer's non-sRGB framebuffer blend; no extra gamma conversion",
                order = "Draw frames[].runs in ascending array order; never flatten normal and additive runs together",
                sampling = "Native integer pixel coordinates, nearest filtering; one transparent pixel atlas gutter; scaling is a separate target-host adaptation",
                pixelHash = "SHA256 of uint32 little-endian width and height, then top-down RGBA8 row bytes",
            },
            source = new
            {
                input.AuthorRoot, manifest = input.ManifestPath, animation = input.AnimationId, animationFile = input.Definition.Path,
                files = input.SourceHashes,
                productionAssembly = AssemblyIdentity(typeof(osu.Game.Skinning.Gameplay.GameplaySkinRecordedAnimationProgram).Assembly),
                frameworkAssembly = AssemblyIdentity(typeof(osu.Framework.Game).Assembly),
            },
            verification = new { file = "verification.json", passed, exactAtlasInverseVerified = true, worst8BitChannelDifference = worst,
                worstMean8BitChannelDifference = worstMean, worstMean8BitChannelExcessAboveOne = worstMeanExcess },
            pages = atlas.Pages,
            frames,
        };
        await File.WriteAllTextAsync(Path.Combine(input.Output, "index.json"), JsonSerializer.Serialize(index, jsonOptions));
        Console.WriteLine($"Published {frames.Count} frames, {atlas.Pages.Count} atlas pages; verified max channel error {worst}.");
    }

    private static object AssemblyIdentity(Assembly assembly) => new
    {
        name = assembly.GetName().Name, version = assembly.GetName().Version?.ToString(), moduleVersionId = assembly.ManifestModule.ModuleVersionId,
        sha256 = BakeInput.Hash(File.ReadAllBytes(assembly.Location)),
    };

    private static BakedRun? ConvertRun(Image<Rgba32> raw, bool additive)
    {
        int left = raw.Width, top = raw.Height, right = -1, bottom = -1;
        for (int y = 0; y < raw.Height; y++)
        {
            Span<Rgba32> row = raw.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < row.Length; x++)
            {
                Rgba32 pixel = row[x];
                if (additive ? pixel.R == 0 && pixel.G == 0 && pixel.B == 0 : pixel.A == 0)
                {
                    row[x] = default;
                    continue;
                }
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
                if (additive)
                    row[x] = new Rgba32(pixel.R, pixel.G, pixel.B, (byte)255);
                else if (pixel.A > 0)
                    row[x] = new Rgba32(Unpremultiply(pixel.R, pixel.A), Unpremultiply(pixel.G, pixel.A), Unpremultiply(pixel.B, pixel.A), pixel.A);
            }
        }
        if (right < left)
            return null;
        return new BakedRun(additive, left, top, raw.Clone(c => c.Crop(new Rectangle(left, top, right - left + 1, bottom - top + 1))));
    }

    private static byte Unpremultiply(byte colour, byte alpha)
    {
        return (byte)Math.Clamp((colour * 255 + alpha / 2) / alpha, 0, 255);
    }

    private static PixelDifference Compare(Image<Rgba32> first, Image<Rgba32> second)
    {
        int max = 0;
        long sum = 0, above = 0, excess = 0;
        for (int y = 0; y < first.Height; y++)
        {
            Span<Rgba32> a = first.DangerousGetPixelRowMemory(y).Span;
            Span<Rgba32> b = second.DangerousGetPixelRowMemory(y).Span;
            for (int x = 0; x < a.Length; x++)
            {
                int r = Math.Abs(a[x].R - b[x].R), g = Math.Abs(a[x].G - b[x].G), bl = Math.Abs(a[x].B - b[x].B), al = Math.Abs(a[x].A - b[x].A);
                int local = Math.Max(Math.Max(r, g), Math.Max(bl, al));
                max = Math.Max(max, local);
                sum += r + g + bl + al;
                excess += Math.Max(0, r - 1) + Math.Max(0, g - 1) + Math.Max(0, bl - 1) + Math.Max(0, al - 1);
                if (local > 3) above++;
            }
        }
        return new PixelDifference(max, sum / (double)(first.Width * first.Height * 4), excess / (double)(first.Width * first.Height * 4), above);
    }

    private sealed record PixelDifference(int MaxChannelDifference, double MeanChannelDifference, double MeanChannelExcessAboveOne, long PixelsAboveThree);
    private sealed record RunEntry(int Order, string Blend, int Page, int[] Rect, int[] Offset, int[] SourcePieceRange, string PixelSha256);
    private sealed record BakedFrame(int SourceFrame, double TimeMs, double DurationMs, bool Terminal, IReadOnlyList<RunEntry> Runs);
}
