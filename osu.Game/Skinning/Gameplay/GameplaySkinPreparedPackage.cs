// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Game.Skinning.Gameplay.Scripting;
using osu.Game.Skinning.Pages;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Layout-independent author content prepared before C2 selects or reloads an exact package. Render preparation
    /// consumes these immutable decoded values; it never reopens or reparses author content after publication.
    /// </summary>
    internal sealed class GameplaySkinPreparedPackage
    {
        public GameplaySkinSceneManifest? Manifest { get; }
        public GameplaySkinSceneDocument? Document { get; }
        public IReadOnlyList<GameplaySkinCapturedSceneResource> Resources { get; }
        public GameplaySkinScriptProgram? ScriptProgram { get; }
        public GameplaySkinScriptAuthorization? ScriptAuthorization { get; }
        public string ContentRevision { get; }
        public IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram> RecordedAnimations { get; }
        internal GameplaySkinCapturedContentCatalog? ContentCatalog { get; }
        internal GameplaySkinDocument? MaterialDocument { get; }
        internal GameplaySkinOptionPreviews OptionPreviews { get; }
        internal SkinPagePreparedPackage? Pages { get; }

        internal GameplaySkinPreparedPackage(
            string contentRevision,
            GameplaySkinSceneManifest? manifest,
            GameplaySkinSceneDocument? document,
            IEnumerable<GameplaySkinCapturedSceneResource> resources,
            GameplaySkinScriptProgram? scriptProgram,
            GameplaySkinScriptAuthorization? scriptAuthorization,
            GameplaySkinCapturedContentCatalog? contentCatalog = null,
            IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram>? recordedAnimations = null,
            GameplaySkinDocument? materialDocument = null,
            GameplaySkinOptionPreviews? optionPreviews = null,
            SkinPagePreparedPackage? pages = null)
        {
            ContentRevision = contentRevision;
            Manifest = manifest;
            Document = document;
            Resources = Array.AsReadOnly(resources.ToArray());
            ScriptProgram = scriptProgram;
            ScriptAuthorization = scriptAuthorization;
            ContentCatalog = contentCatalog;
            MaterialDocument = materialDocument;
            OptionPreviews = optionPreviews ?? GameplaySkinOptionPreviews.Empty;
            Pages = pages;
            RecordedAnimations = recordedAnimations ?? new System.Collections.ObjectModel.ReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram>(
                new Dictionary<string, GameplaySkinRecordedAnimationProgram>(StringComparer.Ordinal));
        }

        internal GameplaySkinPreparedPackage Select(GameplaySkinOptionValues options, CancellationToken cancellationToken)
            => ContentCatalog?.PrepareSelected(options, cancellationToken).withPages(Pages) ?? this;

        private GameplaySkinPreparedPackage withPages(SkinPagePreparedPackage? pages)
            => pages == null ? this : new GameplaySkinPreparedPackage(ContentRevision, Manifest, Document, Resources,
                ScriptProgram, ScriptAuthorization, ContentCatalog, RecordedAnimations, MaterialDocument, OptionPreviews, pages);

        public static GameplaySkinPreparedPackage Prepare(
            Func<string, int, byte[]?> read,
            string packageRevision,
            Guid recordId,
            GameplaySkinScriptAuthorizationStore? authorizations,
            CancellationToken cancellationToken,
            Func<string, SkinPackageRevisionCapsule.ResourceReference?>? retainResource = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? readPageResource(string path, int limit)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (retainResource == null)
                    return read(path, limit);
                // Optional absence is a fact of this exact capsule. It must never fall back to a live reader.
                SkinPackageRevisionCapsule.ResourceReference? reference = retainResource(path);
                if (reference == null)
                    return null;
                if (reference.Length > limit)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                return reference.ReadOwnedBytes();
            }
            SkinPagePreparedPackage? pages = SkinPageCodec.Prepare(readPageResource, packageRevision, cancellationToken);
            byte[]? manifestBytes = read(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES);
            if (manifestBytes == null)
                return new GameplaySkinPreparedPackage(packageRevision, null, null, Array.Empty<GameplaySkinCapturedSceneResource>(), null, null, pages: pages);

            GameplaySkinSceneManifest manifest = RequireValid(GameplaySkinSceneCodec.DecodeManifest(manifestBytes));
            if (manifest.ContentCatalogFile != null)
                return GameplaySkinCapturedContentCatalog.Capture(read, packageRevision, recordId, authorizations, manifest, manifestBytes, cancellationToken, retainResource).withPages(pages);
            byte[] sceneBytes = read(manifest.SceneFile, GameplaySkinSceneBudgets.MAX_SCENE_BYTES)
                                ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
            cancellationToken.ThrowIfCancellationRequested();
            GameplaySkinSceneDocument document = RequireValid(GameplaySkinSceneCodec.DecodeScene(sceneBytes, manifest));
            return PrepareActive(read, packageRevision, recordId, authorizations, manifest, document, cancellationToken).withPages(pages);
        }

        internal static GameplaySkinPreparedPackage PrepareActive(
            Func<string, int, byte[]?> read,
            string packageRevision,
            Guid recordId,
            GameplaySkinScriptAuthorizationStore? authorizations,
            GameplaySkinSceneManifest manifest,
            GameplaySkinSceneDocument document,
            CancellationToken cancellationToken,
            GameplaySkinDocument? materialDocument = null,
            GameplaySkinOptionPreviews? optionPreviews = null)
        {
            optionPreviews ??= GameplaySkinOptionPreviews.Capture(manifest.Options, read, cancellationToken);
            var captured = new List<(GameplaySkinSceneResource Source, byte[] Bytes, ImageInfo Info)>();
            var resources = new List<GameplaySkinCapturedSceneResource>();
            long encodedBytes = 0;
            long totalPixels = 0;
            long pixelBudget = materialDocument == null ? GameplaySkinPreparedSceneBudgets.MAX_TOTAL_TEXTURE_PIXELS
                : GameplaySkinContentBudgets.MAX_SELECTED_TEXTURE_PIXELS;
            long decodedBudget = materialDocument == null ? GameplaySkinPreparedSceneBudgets.MAX_TOTAL_DECODED_TEXTURE_BYTES
                : GameplaySkinContentBudgets.MAX_SELECTED_DECODED_TEXTURE_BYTES;

            // Inspect the complete footprint before any decoder allocates a pixel buffer. Decoding is capped at
            // one image frame: scene frame animation is the existing versioned track/resource contract.
            foreach (GameplaySkinSceneResource resource in manifest.Resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = read(resource.Path, GameplaySkinPreparedSceneBudgets.MAX_RESOURCE_BYTES)
                               ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
                ImageInfo info = Identify(bytes);
                long pixels = (long)info.Width * info.Height;
                encodedBytes += bytes.Length;
                totalPixels += pixels;
                if (encodedBytes > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_RESOURCE_BYTES
                    || pixels <= 0 || pixels > GameplaySkinPreparedSceneBudgets.MAX_TEXTURE_PIXELS
                    || totalPixels > pixelBudget
                    || totalPixels * 4 > decodedBudget)
                    throw fail(GameplaySkinSceneDiagnosticCode.BudgetExceeded);
                captured.Add((resource, bytes, info));
            }

            foreach ((GameplaySkinSceneResource source, byte[] bytes, ImageInfo info) in captured)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Image<Rgba32> image = decode(bytes);
                if (image.Width != info.Width || image.Height != info.Height)
                    throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
                byte[] pixels = new byte[checked(image.Width * image.Height * 4)];
                image.CopyPixelDataTo(pixels);
                resources.Add(new GameplaySkinCapturedSceneResource(source, bytes.Length,
                    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), image.Width, image.Height, pixels));
            }

            GameplaySkinScriptProgram? program = null;
            GameplaySkinScriptAuthorization? authorization = null;
            if (manifest.ScriptFile != null)
            {
                byte[] source = read(manifest.ScriptFile, manifest.ScriptFile == GameplaySkinSceneContracts.SCRIPT_FILE_NAME
                                    ? GameplaySkinScriptProgram.MAX_SOURCE_BYTES : GameplaySkinScriptProgram.MAX_BYTECODE_BYTES)
                                ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
                program = manifest.ScriptFile == GameplaySkinSceneContracts.SCRIPT_FILE_NAME
                    ? GameplaySkinScriptCompiler.Compile(source, cancellationToken)
                    : GameplaySkinScriptCompiler.DecodeBytecode(source, cancellationToken);
                var nodes = enumerateNodes(document.Root)
                            .Concat(document.Templates.SelectMany(template => enumerateNodes(template.Root)))
                            .ToDictionary(node => node.Id, StringComparer.Ordinal);
                foreach (var target in program.Targets)
                {
                    if (!nodes.ContainsKey(target.NodeId))
                        throw fail(GameplaySkinSceneDiagnosticCode.UnknownTarget);
                }
                string authorizationRevision = Convert.ToHexString(SHA256.HashData(
                    Encoding.UTF8.GetBytes(packageRevision + ":" + program.Fingerprint))).ToLowerInvariant();
                authorization = authorizations?.Bind(recordId, authorizationRevision, program.Requests);
            }
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyDictionary<string, GameplaySkinRecordedAnimationProgram> animations =
                GameplaySkinRecordedAnimationCodec.PrepareAnimations(manifest, read, cancellationToken);
            return new GameplaySkinPreparedPackage(packageRevision, manifest, document, resources, program, authorization,
                recordedAnimations: animations, materialDocument: materialDocument, optionPreviews: optionPreviews);
        }

        private static IEnumerable<GameplaySkinSceneNode> enumerateNodes(GameplaySkinSceneNode node)
        {
            yield return node;
            foreach (GameplaySkinSceneNode child in node.Children)
                foreach (GameplaySkinSceneNode descendant in enumerateNodes(child))
                    yield return descendant;
        }

        internal static ImageInfo Identify(byte[] bytes)
        {
            try
            {
                return Image.Identify(new DecoderOptions { MaxFrames = 1 }, bytes) ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (ImageFormatException) { throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource); }
        }

        private static Image<Rgba32> decode(byte[] bytes)
        {
            try
            {
                return Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1 }, bytes);
            }
            catch (ImageFormatException) { throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource); }
        }

        internal static T RequireValid<T>(GameplaySkinSceneDecodeResult<T> result) where T : class
            => result.Status == GameplaySkinSceneDecodeStatus.Valid && result.Value != null
                ? result.Value
                : throw fail(result.Diagnostics.FirstOrDefault()?.Code ?? GameplaySkinSceneDiagnosticCode.InvalidReference);

        private static GameplaySkinScenePreparationException fail(GameplaySkinSceneDiagnosticCode code) => new(code);
    }

    internal sealed class GameplaySkinCapturedSceneResource
    {
        private readonly byte[] pixels;
        public GameplaySkinSceneResource Source { get; }
        public int EncodedBytes { get; }
        public string ContentRevision { get; }
        public long DecodedBytes => pixels.LongLength;
        public int Width { get; }
        public int Height { get; }

        public GameplaySkinCapturedSceneResource(GameplaySkinSceneResource source, int encodedBytes, string contentRevision, int width, int height, byte[] pixels)
        {
            Source = source;
            EncodedBytes = encodedBytes;
            ContentRevision = contentRevision;
            Width = width;
            Height = height;
            this.pixels = pixels;
        }

        public Image<Rgba32> CreateImage() => Image.LoadPixelData<Rgba32>(pixels, Width, Height);
    }
}
