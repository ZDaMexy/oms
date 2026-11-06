// Copyright (c) OMS contributors. Licensed under the MIT Licence.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using osu.Game.Skinning.Gameplay;

namespace osu.Tools.IidxSkinBake;

internal sealed class BakeInput
{
    public string AuthorRoot { get; }
    public string Output { get; }
    public string ManifestPath { get; }
    public string AnimationId { get; }
    public GameplaySkinRecordedAnimationDefinition Definition { get; }
    public GameplaySkinRecordedAnimationProgram Animation { get; }
    public IReadOnlyList<ResourceInput> Resources { get; }
    public int[] Frames { get; }
    public Dictionary<string, string> SourceHashes { get; } = new(StringComparer.Ordinal);
    public int PageSize { get; }
    public bool KeepVerificationImages { get; }
    public int NormalRunPieceLimit { get; }

    public BakeInput(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < args.Length; i += 2)
        {
            if (i + 1 == args.Length || !args[i].StartsWith("--", StringComparison.Ordinal)
                                     || !options.TryAdd(args[i], args[i + 1]))
                throw new ArgumentException("Options must be unique --name value pairs.");
        }
        foreach (string key in options.Keys)
            if (!new[] { "--runtime", "--author", "--manifest", "--animation", "--frames", "--output", "--page-size", "--verification-images", "--normal-run-pieces" }.Contains(key))
                throw new ArgumentException("Unknown option: " + key);
        string required(string key) => options.TryGetValue(key, out string? value) ? value : throw new ArgumentException("Missing " + key);
        AuthorRoot = Path.GetFullPath(required("--author"));
        Output = Path.GetFullPath(required("--output"));
        if (Directory.Exists(Output) || File.Exists(Output))
            throw new IOException("Output must be a new directory.");
        string repository = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        if (!IsBelow(Output, Path.Combine(repository, ".dev-cache", "temp", "iidx-bake-dev"))
            && !IsBelow(Output, Path.Combine(repository, "artifacts")))
            throw new ArgumentException("Output must be beneath this checkout's .dev-cache/temp/iidx-bake-dev or artifacts.");
        if (IsBelow(Output, AuthorRoot) || IsBelow(AuthorRoot, Output))
            throw new ArgumentException("Output and source may not overlap.");
        for (string? current = Path.GetDirectoryName(Output); current != null; current = Path.GetDirectoryName(current))
            if (Directory.Exists(current) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("Output ancestors may not contain reparse points.");
        ManifestPath = required("--manifest").Replace('\\', '/');
        AnimationId = required("--animation");
        PageSize = int.Parse(options.GetValueOrDefault("--page-size", "2048"), System.Globalization.CultureInfo.InvariantCulture);
        if (PageSize is < 64 or > 8192 || (PageSize & (PageSize - 1)) != 0)
            throw new ArgumentException("Page size must be a power of two from 64 to 8192.");
        KeepVerificationImages = bool.Parse(options.GetValueOrDefault("--verification-images", "false"));
        NormalRunPieceLimit = int.Parse(options.GetValueOrDefault("--normal-run-pieces", "4096"), System.Globalization.CultureInfo.InvariantCulture);
        if (NormalRunPieceLimit is < 1 or > 4096)
            throw new ArgumentException("Normal run piece limit must be in 1..4096.");
        var manifestResult = GameplaySkinSceneCodec.DecodeManifest(Read(ManifestPath));
        GameplaySkinSceneManifest manifest = manifestResult.Value ?? throw new InvalidDataException("Formal manifest decoding rejected the input: " + string.Join(", ", manifestResult.Diagnostics));
        Definition = manifest.Animations.Single(a => a.Id == AnimationId);
        Animation = GameplaySkinRecordedAnimationCodec.Decode(Read(Definition.Path), Definition);
        if (Animation.Width != Math.Truncate(Animation.Width) || Animation.Height != Math.Truncate(Animation.Height)
            || Animation.Width > 8192 || Animation.Height > 8192)
            throw new InvalidDataException("Baking requires an integer source canvas no larger than 8192 pixels on either axis.");
        Resources = Definition.Resources.Select(id =>
        {
            GameplaySkinSceneResource resource = manifest.Resources.Single(r => r.Id == id);
            return new ResourceInput(resource, Read(resource.Path));
        }).ToArray();
        string selection = required("--frames");
        Frames = selection == "all" ? Enumerable.Range(0, Animation.FrameCount).ToArray()
            : selection.Split(',').SelectMany(part =>
            {
                string[] range = part.Split('-');
                int first = int.Parse(range[0], System.Globalization.CultureInfo.InvariantCulture);
                return range.Length == 1 ? new[] { first } : range.Length == 2
                    ? Enumerable.Range(first, checked(int.Parse(range[1], System.Globalization.CultureInfo.InvariantCulture) - first + 1))
                    : throw new ArgumentException("Invalid frame range.");
            }).Distinct().Order().ToArray();
        if (Frames.Length == 0 || Frames.Any(f => f < 0 || f >= Animation.FrameCount))
            throw new ArgumentException("Frames are zero-based, inclusive and must be present in the source.");
    }

    private byte[] Read(string relative)
    {
        string path = Resolve(AuthorRoot, relative);
        long length = new FileInfo(path).Length;
        if (length > 16 * 1024 * 1024)
            throw new InvalidDataException("Source file exceeds the formal per-file limit.");
        byte[] bytes = File.ReadAllBytes(path);
        SourceHashes.Add(relative, Hash(bytes));
        return bytes;
    }

    public void VerifyUnchanged()
    {
        foreach (var (relative, expected) in SourceHashes)
            if (Hash(File.ReadAllBytes(Resolve(AuthorRoot, relative))) != expected)
                throw new IOException("A source file changed while baking: " + relative);
    }

    public static string Resolve(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Any(s => s is "" or "." or ".."))
            throw new InvalidDataException("A source reference is not a contained relative path.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsBelow(path, root))
            throw new InvalidDataException("A source reference escapes its author root.");
        for (string? current = path; current != null && IsBelow(current, root); current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Reparse points are not allowed in source references.");
        return path;
    }

    public static bool IsBelow(string path, string root) => path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record ResourceInput(GameplaySkinSceneResource Definition, byte[] Bytes);
