// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Linq;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace osu.Tools.IidxSkinBake;

/// <summary>Deterministic shelf packing. Pixels are copied without resampling, with a one-pixel clear gutter.</summary>
internal sealed class AtlasWriter : IDisposable
{
    private readonly string output;
    private readonly int limit;
    private readonly Dictionary<string, AtlasPlacement> identical = new(StringComparer.Ordinal);
    private Image<Rgba32>? current;
    private int x = 1, y = 1, rowHeight, usedRight, usedBottom;
    public List<AtlasPage> Pages { get; } = new();

    public AtlasWriter(string output, int limit)
    {
        this.output = output;
        this.limit = limit;
    }

    public async Task<AtlasPlacement> Add(Image<Rgba32> image)
    {
        string hash = PixelHash(image);
        if (identical.TryGetValue(hash, out AtlasPlacement? previous))
            return previous;
        if (image.Width + 2 > limit || image.Height + 2 > limit)
            throw new InvalidDataException("A cropped run exceeds the atlas page. Increase --page-size without resizing the source.");
        if (x + image.Width + 1 > limit)
        {
            x = 1;
            y += rowHeight + 2;
            rowHeight = 0;
        }
        if (y + image.Height + 1 > limit)
            await Flush();
        current ??= new Image<Rgba32>(limit, limit);
        for (int row = 0; row < image.Height; row++)
            image.DangerousGetPixelRowMemory(row).Span.CopyTo(current.DangerousGetPixelRowMemory(y + row).Span.Slice(x, image.Width));
        var placement = new AtlasPlacement(Pages.Count, new[] { x, y, image.Width, image.Height }, hash);
        identical.Add(hash, placement);
        usedRight = Math.Max(usedRight, x + image.Width + 1);
        usedBottom = Math.Max(usedBottom, y + image.Height + 1);
        rowHeight = Math.Max(rowHeight, image.Height);
        x += image.Width + 2;
        return placement;
    }

    public async Task Flush()
    {
        if (current == null)
            return;
        string file = $"atlas-{Pages.Count:D4}.png";
        using Image<Rgba32> trimmed = current.Clone(c => c.Crop(new Rectangle(0, 0, usedRight, usedBottom)));
        await trimmed.SaveAsPngAsync(Path.Combine(output, file));
        byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(output, file));
        Pages.Add(new AtlasPage(Pages.Count, file, trimmed.Width, trimmed.Height, "nearest", "per-run", BakeInput.Hash(bytes), bytes.Length));
        current.Dispose();
        current = null;
        x = y = 1;
        rowHeight = usedRight = usedBottom = 0;
    }

    public static string PixelHash(Image<Rgba32> image)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> dimensions = stackalloc byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(dimensions, image.Width);
        BinaryPrimitives.WriteInt32LittleEndian(dimensions[4..], image.Height);
        hash.AppendData(dimensions);
        for (int y = 0; y < image.Height; y++)
            hash.AppendData(System.Runtime.InteropServices.MemoryMarshal.AsBytes(image.DangerousGetPixelRowMemory(y).Span));
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    public async Task VerifySavedPixels()
    {
        foreach (AtlasPage page in Pages)
        {
            byte[] bytes = await File.ReadAllBytesAsync(Path.Combine(output, page.File));
            if (BakeInput.Hash(bytes) != page.Sha256)
                throw new IOException("A written atlas page changed.");
            using Image<Rgba32> decoded = Image.Load<Rgba32>(bytes);
            if (decoded.Width != page.Width || decoded.Height != page.Height)
                throw new InvalidDataException("The PNG page dimensions differ from its declaration.");
            foreach (AtlasPlacement placement in identical.Values.Where(p => p.Page == page.Id))
            {
                int[] rect = placement.Rect;
                using Image<Rgba32> restored = decoded.Clone(c => c.Crop(new Rectangle(rect[0], rect[1], rect[2], rect[3])));
                if (PixelHash(restored) != placement.PixelSha256)
                    throw new InvalidDataException("An atlas rect failed exact inverse RGBA verification.");
            }
        }
    }

    public void Dispose() => current?.Dispose();
}

internal sealed record AtlasPlacement(int Page, int[] Rect, string PixelSha256);
internal sealed record AtlasPage(int Id, string File, int Width, int Height, string Filter, string PixelEncoding, string Sha256, long Bytes);
