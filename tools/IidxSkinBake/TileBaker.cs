// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Tools.IidxSkinBake;

/// <summary>
/// Re-packs already verified RGBA. There is no animation evaluation or GPU rendering in this path.
/// Canvas-aligned cells have a movie-wide union crop, keeping destination image-set identities stable.
/// </summary>
internal static class TileBaker
{
    private static readonly JsonSerializerOptions jsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static async Task Run(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || !options.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Tile options must be unique --name value pairs.");
        }
        if (options.Keys.Any(k => k is not ("--runtime" or "--tile-index" or "--tile-size" or "--tile-crop" or "--output" or "--page-size")))
            throw new ArgumentException("Unknown tile option.");
        string indexPath = Path.GetFullPath(options["--tile-index"]);
        string sourceRoot = Path.GetDirectoryName(indexPath)!;
        string output = Path.GetFullPath(options["--output"]);
        int tileSize = int.Parse(options.GetValueOrDefault("--tile-size", "256"), System.Globalization.CultureInfo.InvariantCulture);
        int pageSize = int.Parse(options.GetValueOrDefault("--page-size", "2048"), System.Globalization.CultureInfo.InvariantCulture);
        string cropPolicy = options.GetValueOrDefault("--tile-crop", "movie-union");
        if (cropPolicy is not ("movie-union" or "aligned16"))
            throw new ArgumentException("Tile crop must be movie-union or aligned16.");
        if (tileSize is not (128 or 256) || pageSize is < 512 or > 8192 || (pageSize & (pageSize - 1)) != 0)
            throw new ArgumentException("Use tile size 128 or 256 and a power-of-two atlas page size in 512..8192.");
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        if (Directory.Exists(output) || File.Exists(output)
            || !BakeInput.IsBelow(output, Path.Combine(repository, ".dev-cache", "temp", "iidx-bake-dev")) && !BakeInput.IsBelow(output, Path.Combine(repository, "artifacts")))
            throw new ArgumentException("Tile output must be a new directory under the checkout's bake temp or artifacts directory.");
        for (string? parent = Path.GetDirectoryName(output); parent != null; parent = Path.GetDirectoryName(parent))
            if (Directory.Exists(parent) && (File.GetAttributes(parent) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Tile output may not traverse reparse points.");

        byte[] indexBytes = await File.ReadAllBytesAsync(indexPath);
        JsonObject source = JsonNode.Parse(indexBytes)!.AsObject();
        if (source["contract"]!.GetValue<string>() != "oms-iidx-baked-animation.v1"
            || source["verification"]!["passed"]!.GetValue<bool>() != true || source["tiling"] != null)
            throw new InvalidDataException("Tiling requires an untiled, verified bake index.");
        string renderingVerification = BakeInput.Resolve(sourceRoot, source["verification"]!["file"]!.GetValue<string>());
        byte[] renderingVerificationBytes = await File.ReadAllBytesAsync(renderingVerification);
        if (JsonNode.Parse(renderingVerificationBytes)!["passed"]!.GetValue<bool>() != true)
            throw new InvalidDataException("The original rendering verification did not pass.");
        int canvasWidth = source["canvas"]![0]!.GetValue<int>();
        int canvasHeight = source["canvas"]![1]!.GetValue<int>();
        int columns = (canvasWidth + tileSize - 1) / tileSize;
        int rows = (canvasHeight + tileSize - 1) / tileSize;
        int cellCount = checked(columns * rows);
        var frames = source["frames"]!.AsArray().Select(n => new SourceFrame(n!.AsObject())).ToArray();
        var pages = source["pages"]!.AsArray().Select(n => n!.Deserialize<AtlasPage>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!).ToDictionary(p => p.Id);
        using var cache = new PageCache(sourceRoot, pages);
        var unions = new Dictionary<CellKey, Rectangle>();
        long tightPixels = 0;
        int populatedCells = 0;

        Console.WriteLine($"Measure {tileSize}px canvas-aligned cells across {frames.Length} source frames.");
        foreach (SourceFrame frame in frames)
        {
            int previousOrder = -1;
            foreach (SourceRun run in frame.Runs)
            {
                if (run.Order <= previousOrder || run.Blend is not ("normal" or "additive"))
                    throw new InvalidDataException("Original runs are not in strict normal/additive order.");
                previousOrder = run.Order;
                using Image<Rgba32> pixels = cache.Crop(run);
                foreach (var (cell, bounds) in NonzeroCells(pixels, run, tileSize, columns, canvasWidth, canvasHeight))
                {
                    var key = new CellKey(run.Order, run.Blend, cell);
                    unions[key] = unions.TryGetValue(key, out Rectangle previous) ? Rectangle.Union(previous, bounds) : bounds;
                    tightPixels += (long)bounds.Width * bounds.Height;
                    populatedCells++;
                }
            }
        }

        Directory.CreateDirectory(output);
        using var atlas = new AtlasWriter(output, pageSize);
        var outputFrames = new JsonArray();
        var reconstruction = new List<object>();
        var destinationGroups = new HashSet<string>(StringComparer.Ordinal);
        long emittedPixels = 0;
        int totalDraws = 0, maxDraws = 0;
        foreach (SourceFrame frame in frames)
        {
            var emitted = new JsonArray();
            var reconstructedRuns = new List<object>();
            foreach (SourceRun run in frame.Runs)
            {
                using Image<Rgba32> pixels = cache.Crop(run);
                using var restored = new Image<Rgba32>(pixels.Width, pixels.Height);
                foreach (var (cell, tight) in NonzeroCells(pixels, run, tileSize, columns, canvasWidth, canvasHeight))
                {
                    Rectangle bounds = cropPolicy == "movie-union" ? unions[new CellKey(run.Order, run.Blend, cell)]
                        : Rectangle.FromLTRB(tight.Left / 16 * 16, tight.Top / 16 * 16,
                            Math.Min(canvasWidth, (tight.Right + 15) / 16 * 16), Math.Min(canvasHeight, (tight.Bottom + 15) / 16 * 16));
                    using var tile = new Image<Rgba32>(bounds.Width, bounds.Height);
                    Rectangle overlap = Rectangle.Intersect(bounds, new Rectangle(run.X, run.Y, pixels.Width, pixels.Height));
                    Copy(pixels, new Rectangle(overlap.X - run.X, overlap.Y - run.Y, overlap.Width, overlap.Height),
                        tile, overlap.X - bounds.X, overlap.Y - bounds.Y);
                    Copy(tile, new Rectangle(overlap.X - bounds.X, overlap.Y - bounds.Y, overlap.Width, overlap.Height),
                        restored, overlap.X - run.X, overlap.Y - run.Y);
                    AtlasPlacement placement = await atlas.Add(tile);
                    int order = checked(run.Order * cellCount + cell);
                    JsonObject item = (JsonObject)run.Node.DeepClone();
                    item["order"] = order;
                    item["page"] = placement.Page;
                    item["rect"] = JsonSerializer.SerializeToNode(placement.Rect);
                    item["offset"] = new JsonArray(bounds.X, bounds.Y);
                    item["pixelSha256"] = placement.PixelSha256;
                    item["parentRunOrder"] = run.Order;
                    item["tileCell"] = new JsonArray(cell % columns, cell / columns);
                    emitted.Add(item);
                    emittedPixels += (long)bounds.Width * bounds.Height;
                    destinationGroups.Add($"{order}|{run.Blend}|{bounds.X}|{bounds.Y}|{bounds.Width}|{bounds.Height}");
                }
                string restoredHash = AtlasWriter.PixelHash(restored);
                if (restoredHash != run.PixelHash)
                    throw new InvalidDataException("A source run failed exact RGBA reconstruction from non-overlapping tiles.");
                reconstructedRuns.Add(new { order = run.Order, rgbaReconstructionSha256 = restoredHash, equal = true });
            }
            int last = -1;
            foreach (JsonNode? tile in emitted)
            {
                int order = tile!["order"]!.GetValue<int>();
                if (order <= last)
                    throw new InvalidDataException("Flattened tile order is not strictly increasing.");
                last = order;
            }
            var outputFrame = (JsonObject)frame.Node.DeepClone();
            outputFrame["runs"] = emitted;
            outputFrames.Add(outputFrame);
            reconstruction.Add(new { sourceFrame = frame.Node["sourceFrame"]!.GetValue<int>(), exactOriginalRunReconstruction = true, runs = reconstructedRuns });
            totalDraws += emitted.Count;
            maxDraws = Math.Max(maxDraws, emitted.Count);
        }
        await atlas.Flush();
        await atlas.VerifySavedPixels();
        if (BakeInput.Hash(await File.ReadAllBytesAsync(indexPath)) != BakeInput.Hash(indexBytes))
            throw new IOException("The source bake index changed while tiling.");
        long originalDecodedBytes = pages.Values.Sum(p => (long)p.Width * p.Height * 4);
        long decodedBytes = atlas.Pages.Sum(p => (long)p.Width * p.Height * 4);
        var tiling = new
        {
            tileSize,
            cropPolicy,
            sourceIndex = indexPath,
            sourceIndexSha256 = BakeInput.Hash(indexBytes),
            sourceVerificationSha256 = BakeInput.Hash(renderingVerificationBytes),
            coordinatePartition = "Original canvas grid; each original run is partitioned separately into disjoint cells",
            crop = cropPolicy == "movie-union"
                ? "Only RGBA=(0,0,0,0) is empty; use movie-wide union crop for each original run order, blend and grid cell"
                : "Only RGBA=(0,0,0,0) is empty; each frame's tight cell bounds expand to outward 16px canvas grid, with transparent padding and no resampling",
            order = "originalRunOrder * canvasGridCellCount + rowMajorGridCell; stable across frames, strictly increasing within each frame",
            columns, rows, cellCount,
            exactOriginalRunReconstruction = true,
            exactAtlasInverseVerified = true,
            originalDecodedBytes,
            decodedBytes,
            originalPngBytes = pages.Values.Sum(p => p.Bytes),
            pngBytes = atlas.Pages.Sum(p => p.Bytes),
            originalTotalDraws = frames.Sum(f => f.Runs.Length),
            totalDraws,
            maxDrawsPerFrame = maxDraws,
            destinationImageSetGroups = destinationGroups.Count,
            populatedCells,
            tightCropPixelsWithFrameDuplicates = tightPixels,
            emittedPixelsWithFrameDuplicates = emittedPixels,
        };
        await File.WriteAllBytesAsync(Path.Combine(output, "render-verification.json"), renderingVerificationBytes);
        var verification = new
        {
            passed = true,
            scope = "All original runs of every source frame reconstructed byte-for-byte from tiles; unchanged per-pixel run sequence proves identical frame composition for either blend mode and any background",
            inheritedRenderingVerification = "render-verification.json",
            exactOriginalRunReconstruction = true,
            exactAtlasInverseVerified = true,
            frames = reconstruction,
        };
        await File.WriteAllTextAsync(Path.Combine(output, "verification.json"), JsonSerializer.Serialize(verification, jsonOptions));
        var result = (JsonObject)source.DeepClone();
        result["pages"] = JsonSerializer.SerializeToNode(atlas.Pages, jsonOptions);
        result["frames"] = outputFrames;
        result["tiling"] = JsonSerializer.SerializeToNode(tiling, jsonOptions);
        result["optimization"] = JsonSerializer.SerializeToNode(new
        {
            kind = "lossless-canvas-tiles", tileSize, cropPolicy,
            decodedPixels = decodedBytes / 4,
            originalDecodedPixels = originalDecodedBytes / 4,
            pngBytes = atlas.Pages.Sum(p => p.Bytes),
            maxDrawsPerFrame = maxDraws,
            destinationImageSetGroups = destinationGroups.Count,
        }, jsonOptions);
        result["verification"]!["file"] = "verification.json";
        result["verification"]!["exactOriginalRunReconstruction"] = true;
        result["verification"]!["exactAtlasInverseVerified"] = true;
        await File.WriteAllTextAsync(Path.Combine(output, "index.json"), result.ToJsonString(jsonOptions));
        await File.WriteAllTextAsync(Path.Combine(output, "tiling-summary.json"), JsonSerializer.Serialize(tiling, jsonOptions));
        Console.WriteLine($"Tiled {frames.Length} frames: {decodedBytes / 1048576d:F2} MiB decoded, {atlas.Pages.Count} pages, max {maxDraws} draws/frame, {destinationGroups.Count} destination groups. Exact RGBA reconstruction passed.");
    }

    private static IEnumerable<(int Cell, Rectangle Bounds)> NonzeroCells(Image<Rgba32> pixels, SourceRun run, int size, int columns, int canvasWidth, int canvasHeight)
    {
        if (run.X < 0 || run.Y < 0 || run.X + pixels.Width > canvasWidth || run.Y + pixels.Height > canvasHeight)
            throw new InvalidDataException("A baked run lies outside its declared source canvas.");
        int firstColumn = run.X / size, lastColumn = (run.X + pixels.Width - 1) / size;
        int firstRow = run.Y / size, lastRow = (run.Y + pixels.Height - 1) / size;
        for (int row = firstRow; row <= lastRow; row++)
        for (int column = firstColumn; column <= lastColumn; column++)
        {
            Rectangle cell = Rectangle.Intersect(new Rectangle(column * size, row * size, size, size), new Rectangle(run.X, run.Y, pixels.Width, pixels.Height));
            int left = cell.Right, top = cell.Bottom, right = cell.Left - 1, bottom = cell.Top - 1;
            for (int y = cell.Top; y < cell.Bottom; y++)
            for (int x = cell.Left; x < cell.Right; x++)
            {
                Rgba32 value = pixels[x - run.X, y - run.Y];
                if (value.R == 0 && value.G == 0 && value.B == 0 && value.A == 0)
                    continue;
                left = Math.Min(left, x); top = Math.Min(top, y); right = Math.Max(right, x); bottom = Math.Max(bottom, y);
            }
            if (right >= left)
                yield return (row * columns + column, new Rectangle(left, top, right - left + 1, bottom - top + 1));
        }
    }

    private static void Copy(Image<Rgba32> source, Rectangle sourceRect, Image<Rgba32> target, int x, int y)
    {
        for (int row = 0; row < sourceRect.Height; row++)
            source.DangerousGetPixelRowMemory(sourceRect.Y + row).Span.Slice(sourceRect.X, sourceRect.Width)
                .CopyTo(target.DangerousGetPixelRowMemory(y + row).Span.Slice(x, sourceRect.Width));
    }

    private sealed record CellKey(int OriginalOrder, string Blend, int Cell);
    private sealed class SourceFrame
    {
        public JsonObject Node { get; }
        public SourceRun[] Runs { get; }
        public SourceFrame(JsonObject node)
        {
            Node = node;
            Runs = node["runs"]!.AsArray().Select(n => new SourceRun(n!.AsObject())).ToArray();
        }
    }
    private sealed class SourceRun
    {
        public JsonObject Node { get; }
        public int Order { get; }
        public string Blend { get; }
        public int Page { get; }
        public Rectangle Rect { get; }
        public int X { get; }
        public int Y { get; }
        public string PixelHash { get; }
        public SourceRun(JsonObject node)
        {
            Node = node;
            Order = node["order"]!.GetValue<int>();
            Blend = node["blend"]!.GetValue<string>();
            Page = node["page"]!.GetValue<int>();
            int[] rect = node["rect"]!.Deserialize<int[]>()!;
            Rect = new Rectangle(rect[0], rect[1], rect[2], rect[3]);
            X = node["offset"]![0]!.GetValue<int>();
            Y = node["offset"]![1]!.GetValue<int>();
            PixelHash = node["pixelSha256"]!.GetValue<string>();
        }
    }
    private sealed class PageCache : IDisposable
    {
        private readonly string root;
        private readonly IReadOnlyDictionary<int, AtlasPage> pages;
        private readonly Dictionary<int, Image<Rgba32>> loaded = new();
        private readonly LinkedList<int> recent = new();
        public PageCache(string root, IReadOnlyDictionary<int, AtlasPage> pages)
        {
            this.root = root;
            this.pages = pages;
        }
        public Image<Rgba32> Crop(SourceRun run)
        {
            if (!loaded.TryGetValue(run.Page, out Image<Rgba32>? image))
            {
                AtlasPage page = pages[run.Page];
                byte[] bytes = File.ReadAllBytes(BakeInput.Resolve(root, page.File));
                if (BakeInput.Hash(bytes) != page.Sha256)
                    throw new InvalidDataException("A source atlas page differs from its recorded bytes.");
                image = Image.Load<Rgba32>(bytes);
                if (image.Width != page.Width || image.Height != page.Height)
                    throw new InvalidDataException("Source atlas dimensions differ.");
                if (loaded.Count == 4)
                {
                    int expired = recent.First!.Value;
                    recent.RemoveFirst();
                    loaded[expired].Dispose();
                    loaded.Remove(expired);
                }
                loaded.Add(run.Page, image);
            }
            recent.Remove(run.Page);
            recent.AddLast(run.Page);
            Image<Rgba32> crop = image.Clone(c => c.Crop(run.Rect));
            if (AtlasWriter.PixelHash(crop) != run.PixelHash)
            {
                crop.Dispose();
                throw new InvalidDataException("A source atlas rect differs from its exact RGBA identity.");
            }
            return crop;
        }
        public void Dispose()
        {
            foreach (Image<Rgba32> image in loaded.Values)
                image.Dispose();
        }
    }
}
