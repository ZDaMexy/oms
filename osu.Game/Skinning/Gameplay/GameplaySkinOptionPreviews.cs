// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Skinning.Gameplay
{
    public static class GameplaySkinOptionPreviewBudgets
    {
        public const int MAX_ENCODED_BYTES = 256 * 1024;
        public const int MAX_DIMENSION = 512;
        public const int MAX_FILES = 4096;
        public const long MAX_TOTAL_ENCODED_BYTES = 32L * 1024 * 1024;
    }

    /// <summary>
    /// Immutable settings-only PNG captured with an exact package. It allocates pixels only when a settings
    /// consumer explicitly requests this choice; it is never a gameplay resource or a live filesystem reader.
    /// </summary>
    internal sealed class GameplaySkinOptionPreview
    {
        private readonly byte[] encoded;
        public string ContentRevision { get; }
        public int Width { get; }
        public int Height { get; }

        internal GameplaySkinOptionPreview(byte[] captured)
        {
            encoded = [.. captured];
            ReadOnlySpan<byte> signature = [137, 80, 78, 71, 13, 10, 26, 10];
            if (!encoded.AsSpan().StartsWith(signature))
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);
            ImageInfo info;
            try
            {
                info = Image.Identify(new DecoderOptions { MaxFrames = 1, SkipMetadata = true }, encoded)
                       ?? throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (ImageFormatException)
            {
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            if (info.Width <= 0 || info.Height <= 0 || info.Width > GameplaySkinOptionPreviewBudgets.MAX_DIMENSION
                || info.Height > GameplaySkinOptionPreviewBudgets.MAX_DIMENSION)
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
            Width = info.Width;
            Height = info.Height;
            ContentRevision = Convert.ToHexString(SHA256.HashData(encoded)).ToLowerInvariant();
        }

        /// <summary>Decodes one bounded first frame off the draw loop. The caller owns the returned image.</summary>
        public Image<Rgba32> CreateImage(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Image<Rgba32> image;
            try
            {
                image = Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1, SkipMetadata = true }, encoded);
            }
            catch (ImageFormatException)
            {
                throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (image.Width != Width || image.Height != Height)
                    throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidResource);
                return image;
            }
            catch
            {
                image.Dispose();
                throw;
            }
        }
    }

    internal sealed class GameplaySkinOptionPreviews
    {
        public static GameplaySkinOptionPreviews Empty { get; } = new(new Dictionary<(string, string), GameplaySkinOptionPreview>());

        private readonly IReadOnlyDictionary<(string, string), GameplaySkinOptionPreview> choices;

        private GameplaySkinOptionPreviews(Dictionary<(string, string), GameplaySkinOptionPreview> choices)
            => this.choices = choices;

        public bool TryGet(string optionId, string choiceValue, out GameplaySkinOptionPreview preview)
            => choices.TryGetValue((optionId, choiceValue), out preview!);

        internal static GameplaySkinOptionPreviews Capture(IReadOnlyList<GameplaySkinOptionDefinition> options,
                                                           Func<string, int, byte[]?> read, CancellationToken token)
        {
            var byPath = new Dictionary<string, GameplaySkinOptionPreview>(StringComparer.OrdinalIgnoreCase);
            var choices = new Dictionary<(string, string), GameplaySkinOptionPreview>();
            long totalBytes = 0;
            foreach (GameplaySkinOptionDefinition option in options)
                foreach (GameplaySkinOptionChoice choice in option.Choices)
                {
                    token.ThrowIfCancellationRequested();
                    if (choice.PreviewPath == null)
                        continue;
                    if (!byPath.TryGetValue(choice.PreviewPath, out GameplaySkinOptionPreview? preview))
                    {
                        byte[] bytes = read(choice.PreviewPath, GameplaySkinOptionPreviewBudgets.MAX_ENCODED_BYTES)
                                       ?? throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.InvalidReference);
                        totalBytes += bytes.LongLength;
                        if (bytes.Length > GameplaySkinOptionPreviewBudgets.MAX_ENCODED_BYTES
                            || totalBytes > GameplaySkinOptionPreviewBudgets.MAX_TOTAL_ENCODED_BYTES
                            || byPath.Count >= GameplaySkinOptionPreviewBudgets.MAX_FILES)
                            throw new GameplaySkinScenePreparationException(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                        preview = new GameplaySkinOptionPreview(bytes);
                        byPath.Add(choice.PreviewPath, preview);
                    }
                    choices.Add((option.Id, choice.Value), preview);
                }
            return choices.Count == 0 ? Empty : new GameplaySkinOptionPreviews(choices);
        }

        /// <summary>Author validation checks even unselected thumbnails, one small image at a time.</summary>
        internal void ValidateAll(CancellationToken cancellationToken)
        {
            foreach (GameplaySkinOptionPreview preview in choices.Values.Distinct())
            {
                using Image<Rgba32> image = preview.CreateImage(cancellationToken);
            }
        }
    }
}
