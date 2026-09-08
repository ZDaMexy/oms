// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using osu.Game.Skinning.Gameplay.Scripting;
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

        private GameplaySkinPreparedPackage(
            string contentRevision,
            GameplaySkinSceneManifest? manifest,
            GameplaySkinSceneDocument? document,
            IEnumerable<GameplaySkinCapturedSceneResource> resources,
            GameplaySkinScriptProgram? scriptProgram,
            GameplaySkinScriptAuthorization? scriptAuthorization)
        {
            ContentRevision = contentRevision;
            Manifest = manifest;
            Document = document;
            Resources = Array.AsReadOnly(resources.ToArray());
            ScriptProgram = scriptProgram;
            ScriptAuthorization = scriptAuthorization;
        }

        public static GameplaySkinPreparedPackage Prepare(
            Func<string, int, byte[]?> read,
            string packageRevision,
            Guid recordId,
            GameplaySkinScriptAuthorizationStore? authorizations,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? manifestBytes = read(GameplaySkinSceneContracts.MANIFEST_FILE_NAME, GameplaySkinSceneBudgets.MAX_MANIFEST_BYTES);
            if (manifestBytes == null)
                return new GameplaySkinPreparedPackage(packageRevision, null, null, Array.Empty<GameplaySkinCapturedSceneResource>(), null, null);

            GameplaySkinSceneManifest manifest = requireValid(GameplaySkinSceneCodec.DecodeManifest(manifestBytes));
            byte[] sceneBytes = read(manifest.SceneFile, GameplaySkinSceneBudgets.MAX_SCENE_BYTES)
                                ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidReference);
            cancellationToken.ThrowIfCancellationRequested();
            GameplaySkinSceneDocument document = requireValid(GameplaySkinSceneCodec.DecodeScene(sceneBytes, manifest));
            var captured = new List<(GameplaySkinSceneResource Source, byte[] Bytes, ImageInfo Info)>();
            var resources = new List<GameplaySkinCapturedSceneResource>();
            long encodedBytes = 0;
            long totalPixels = 0;

            // Inspect the complete footprint before any decoder allocates a pixel buffer. Decoding is capped at
            // one image frame: scene frame animation is the existing versioned track/resource contract.
            foreach (GameplaySkinSceneResource resource in manifest.Resources)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte[] bytes = read(resource.Path, GameplaySkinPreparedSceneBudgets.MAX_RESOURCE_BYTES)
                               ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
                ImageInfo info = identify(bytes);
                long pixels = (long)info.Width * info.Height;
                encodedBytes += bytes.Length;
                totalPixels += pixels;
                if (encodedBytes > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_RESOURCE_BYTES
                    || pixels <= 0 || pixels > GameplaySkinPreparedSceneBudgets.MAX_TEXTURE_PIXELS
                    || totalPixels > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_TEXTURE_PIXELS
                    || totalPixels * 4 > GameplaySkinPreparedSceneBudgets.MAX_TOTAL_DECODED_TEXTURE_BYTES)
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
            return new GameplaySkinPreparedPackage(packageRevision, manifest, document, resources, program, authorization);
        }

        private static IEnumerable<GameplaySkinSceneNode> enumerateNodes(GameplaySkinSceneNode node)
        {
            yield return node;
            foreach (GameplaySkinSceneNode child in node.Children)
                foreach (GameplaySkinSceneNode descendant in enumerateNodes(child))
                    yield return descendant;
        }

        private static ImageInfo identify(byte[] bytes)
        {
            try
            {
                return Image.Identify(new DecoderOptions { MaxFrames = 1 }, bytes) ?? throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource);
            }
            catch (UnknownImageFormatException) { throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource); }
            catch (InvalidImageContentException) { throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource); }
        }

        private static Image<Rgba32> decode(byte[] bytes)
        {
            try
            {
                return Image.Load<Rgba32>(new DecoderOptions { MaxFrames = 1 }, bytes);
            }
            catch (UnknownImageFormatException) { throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource); }
            catch (InvalidImageContentException) { throw fail(GameplaySkinSceneDiagnosticCode.InvalidResource); }
        }

        private static T requireValid<T>(GameplaySkinSceneDecodeResult<T> result) where T : class
            => result.Status == GameplaySkinSceneDecodeStatus.Valid && result.Value != null
                ? result.Value
                : throw fail(result.Diagnostics.FirstOrDefault()?.Code ?? GameplaySkinSceneDiagnosticCode.InvalidReference);

        private static GameplaySkinScenePreparationException fail(GameplaySkinSceneDiagnosticCode code) => new GameplaySkinScenePreparationException(code);
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
