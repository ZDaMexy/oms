// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using SixLabors.ImageSharp;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Resolves the first production Skin V1 note components from one exact managed <c>.osk</c> source.
    /// </summary>
    /// <remarks>
    /// Only the native BMS ordinary-note and long-note declarations are in scope. Mania compatibility candidates,
    /// folder-backed packages and the future <c>oms-simple</c> provider remain outside this adapter.
    /// </remarks>
    internal sealed class BmsManagedPackageNoteProvider :
        IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsNoteSkinLookup>, BmsSourceBoundNoteMaterial>
    {
        private readonly BmsLegacySkin source;

        public string Name => "selected.managed-osk.bms-note";

        public BmsManagedPackageNoteProvider(BmsLegacySkin source)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
        }

        public bool ClaimsDeclaration(BmsNoteSkinLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            return tryGetDescriptor(lookup.Element, out _)
                   && source.GetAcceptedBmsNoteResource(lookup.Element, lookup.Keymode, lookup.LaneIndex, lookup.IsScratch).IsDeclared;
        }

        public GameplaySkinSlotResolution<BmsSourceBoundNoteMaterial> Resolve(BmsNoteSkinLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            if (!tryGetDescriptor(lookup.Element, out GameplaySkinSlotDescriptor descriptor))
            {
                return GameplaySkinSlotResolver.Resolve(
                    GameplaySkinSlotCatalog.Note,
                    lookup,
                    Array.Empty<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsNoteSkinLookup>, BmsSourceBoundNoteMaterial>>());
            }

            return GameplaySkinSlotResolver.Resolve(
                descriptor,
                lookup,
                new[] { this },
                material => material.FrameCount > 0);
        }

        public SkinSlotResult<BmsSourceBoundNoteMaterial> GetSlot(GameplaySkinSlotLookup<BmsNoteSkinLookup> slot)
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (!tryGetDescriptor(slot.Context.Element, out GameplaySkinSlotDescriptor descriptor)
                || !ReferenceEquals(slot.Descriptor, descriptor))
            {
                return SkinSlotResult<BmsSourceBoundNoteMaterial>.Inherit;
            }

            GameplaySkinConfigurationDeclaration<string> declaration = source.GetAcceptedBmsNoteResource(
                slot.Context.Element,
                slot.Context.Keymode,
                slot.Context.LaneIndex,
                slot.Context.IsScratch);

            if (!declaration.IsDeclared)
                return SkinSlotResult<BmsSourceBoundNoteMaterial>.Inherit;

            BmsManagedPackageSourceRevision currentRevision = source.CaptureManagedPackageSourceRevision();

            if (!currentRevision.HasGameplayAuthority)
                throw new InvalidOperationException("The selected gameplay skin source is not an eligible managed package.");

            BmsManagedPackageNoteRevision prepared = source.GetOrPrepareManagedPackageNotes(BmsManagedPackageNoteLoadContext.CurrentCancellationToken);

            if (!prepared.SourceRevision.Equals(currentRevision))
                throw new InvalidOperationException("The selected gameplay skin package changed while its note resources were being prepared.");

            if (!prepared.TryGetMaterial(
                    new BmsManagedPackageNoteSlotKey(slot.Context.Element, slot.Context.Keymode, slot.Context.LaneIndex, slot.Context.IsScratch),
                    out BmsSourceBoundNoteMaterial? material))
                throw new InvalidDataException("The selected gameplay note component could not be prepared safely.");

            return SkinSlotResult<BmsSourceBoundNoteMaterial>.Provide(material!);
        }

        private static bool tryGetDescriptor(BmsNoteSkinElements element, out GameplaySkinSlotDescriptor descriptor)
        {
            GameplaySkinSlotDescriptor? candidate = element switch
            {
                BmsNoteSkinElements.Note => GameplaySkinSlotCatalog.Note,
                BmsNoteSkinElements.LongNoteHead => GameplaySkinSlotCatalog.LongNoteHead,
                BmsNoteSkinElements.LongNoteBody => GameplaySkinSlotCatalog.LongNoteBody,
                BmsNoteSkinElements.LongNoteTail => GameplaySkinSlotCatalog.LongNoteTail,
                _ => null,
            };

            descriptor = candidate!;
            return candidate != null;
        }
    }

    /// <summary>
    /// Carries the current drawable-load cancellation through the nullable aggregate skin ABI without changing that ABI.
    /// </summary>
    internal static class BmsManagedPackageNoteLoadContext
    {
        private static readonly AsyncLocal<CancellationToken?> currentCancellationToken = new AsyncLocal<CancellationToken?>();

        public static CancellationToken CurrentCancellationToken => currentCancellationToken.Value ?? CancellationToken.None;

        public static IDisposable Enter(CancellationToken cancellationToken)
        {
            CancellationToken? previous = currentCancellationToken.Value;
            currentCancellationToken.Value = cancellationToken;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly CancellationToken? previous;
            private bool disposed;

            public Scope(CancellationToken? previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                currentCancellationToken.Value = previous;
            }
        }
    }

    internal readonly record struct BmsManagedPackageNoteSlotKey(
        BmsNoteSkinElements Element,
        BmsKeymode Keymode,
        int LaneIndex,
        bool IsScratch);

    internal sealed record BmsManagedPackageFileRevision(string PackageName, string ContentHash, string StorageKey);

    /// <summary>
    /// Immutable authority and file mapping captured from one Realm-backed package revision.
    /// </summary>
    internal sealed class BmsManagedPackageSourceRevision : IEquatable<BmsManagedPackageSourceRevision>
    {
        private readonly BmsManagedPackageFileRevision[] files;
        private readonly Dictionary<string, BmsManagedPackageFileRevision> filesByName;

        public Guid SkinId { get; }
        public string? ParsedConfigurationContentHash { get; }
        public bool HasGameplayAuthority { get; }
        public bool HasFileNameConflict { get; }
        public IReadOnlyList<BmsManagedPackageFileRevision> Files => files;

        public BmsManagedPackageSourceRevision(
            Guid skinId,
            bool isRealmManaged,
            string? filesystemStoragePath,
            bool isExternalFilesystemStorage,
            bool deletePending,
            string? parsedConfigurationContentHash,
            IEnumerable<BmsManagedPackageFileRevision> files)
        {
            ArgumentNullException.ThrowIfNull(files);

            SkinId = skinId;
            ParsedConfigurationContentHash = parsedConfigurationContentHash;
            var normalisedFiles = new List<BmsManagedPackageFileRevision>();
            filesByName = new Dictionary<string, BmsManagedPackageFileRevision>(StringComparer.OrdinalIgnoreCase);

            bool conflict = false;

            foreach (BmsManagedPackageFileRevision file in files)
            {
                string normalisedName;

                try
                {
                    normalisedName = normalisePackageName(file.PackageName);
                }
                catch
                {
                    conflict = true;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(file.ContentHash) || string.IsNullOrWhiteSpace(file.StorageKey))
                {
                    conflict = true;
                    continue;
                }

                var normalised = file with { PackageName = normalisedName };

                if (!filesByName.TryAdd(normalisedName, normalised))
                {
                    conflict = true;
                    continue;
                }

                normalisedFiles.Add(normalised);
            }

            this.files = normalisedFiles
                         .OrderBy(file => file.PackageName, StringComparer.OrdinalIgnoreCase)
                         .ThenBy(file => file.ContentHash, StringComparer.Ordinal)
                         .ToArray();

            bool parsedConfigurationMatchesPackage = !string.IsNullOrWhiteSpace(parsedConfigurationContentHash)
                                                       && filesByName.TryGetValue("skin.ini", out BmsManagedPackageFileRevision? configurationFile)
                                                       && StringComparer.Ordinal.Equals(configurationFile.ContentHash, parsedConfigurationContentHash);

            HasFileNameConflict = conflict;
            HasGameplayAuthority = isRealmManaged
                                   && string.IsNullOrEmpty(filesystemStoragePath)
                                   && !isExternalFilesystemStorage
                                   && !deletePending
                                   && this.files.Length > 0
                                   && !conflict
                                   && parsedConfigurationMatchesPackage;
        }

        public bool TryGetFile(string packageName, out BmsManagedPackageFileRevision file)
            => filesByName.TryGetValue(normalisePackageName(packageName), out file!);

        public bool Equals(BmsManagedPackageSourceRevision? other)
        {
            if (ReferenceEquals(this, other))
                return true;

            if (other == null
                || SkinId != other.SkinId
                || !StringComparer.Ordinal.Equals(ParsedConfigurationContentHash, other.ParsedConfigurationContentHash)
                || HasGameplayAuthority != other.HasGameplayAuthority
                || HasFileNameConflict != other.HasFileNameConflict
                || files.Length != other.files.Length)
            {
                return false;
            }

            for (int i = 0; i < files.Length; i++)
            {
                if (!StringComparer.OrdinalIgnoreCase.Equals(files[i].PackageName, other.files[i].PackageName)
                    || !StringComparer.Ordinal.Equals(files[i].ContentHash, other.files[i].ContentHash)
                    || !StringComparer.Ordinal.Equals(files[i].StorageKey, other.files[i].StorageKey))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object? obj) => obj is BmsManagedPackageSourceRevision other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(SkinId);
            hash.Add(ParsedConfigurationContentHash, StringComparer.Ordinal);
            hash.Add(HasGameplayAuthority);
            hash.Add(HasFileNameConflict);

            foreach (BmsManagedPackageFileRevision file in files)
            {
                hash.Add(file.PackageName, StringComparer.OrdinalIgnoreCase);
                hash.Add(file.ContentHash, StringComparer.Ordinal);
                hash.Add(file.StorageKey, StringComparer.Ordinal);
            }

            return hash.ToHashCode();
        }

        private static string normalisePackageName(string packageName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageName);

            string normalised = packageName.Replace('\\', '/');

            if (normalised.Length > 512
                || normalised.StartsWith('/')
                || normalised.IndexOf(':') >= 0
                || normalised.IndexOf('\0') >= 0)
            {
                throw new InvalidDataException("The package contains an invalid resource name.");
            }

            string[] segments = normalised.Split('/');

            if (segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
                throw new InvalidDataException("The package contains an uncontained resource name.");

            return normalised;
        }
    }

    /// <summary>
    /// Immutable, package-owned material revision. Textures are published only after the complete package note plan has
    /// passed the runtime inventory and decoded-resource budgets.
    /// </summary>
    internal sealed class BmsManagedPackageNoteRevision : IDisposable
    {
        private readonly IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, BmsSourceBoundNoteMaterial> materials;
        private readonly TextureStore? textures;
        private bool disposed;

        public BmsManagedPackageSourceRevision SourceRevision { get; }

        public BmsManagedPackageNoteRevision(
            BmsManagedPackageSourceRevision sourceRevision,
            IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, BmsSourceBoundNoteMaterial>? materials = null,
            TextureStore? textures = null)
        {
            SourceRevision = sourceRevision ?? throw new ArgumentNullException(nameof(sourceRevision));
            this.materials = materials ?? new Dictionary<BmsManagedPackageNoteSlotKey, BmsSourceBoundNoteMaterial>();
            this.textures = textures;
        }

        public bool TryGetMaterial(BmsManagedPackageNoteSlotKey slot, out BmsSourceBoundNoteMaterial? material)
            => materials.TryGetValue(slot, out material);

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            textures?.Dispose();
        }
    }

    /// <summary>
    /// Preflights and decodes all supported native note declarations for one immutable managed package revision.
    /// </summary>
    internal static class BmsManagedPackageNoteMaterializer
    {
        internal const int MAX_ANIMATION_FRAMES = 256;
        internal const int MAX_PACKAGE_FILES = 8192;
        internal const long MAX_PACKAGE_RAW_BYTES = 512L * 1024 * 1024;
        internal const long MAX_FILE_RAW_BYTES = 64L * 1024 * 1024;
        internal const long MAX_IMAGE_RAW_BYTES = 16L * 1024 * 1024;
        internal const long MAX_FRAME_PIXELS = 16_777_216;
        internal const long MAX_COMPONENT_DECODED_BYTES = 64L * 1024 * 1024;
        internal const long MAX_PACKAGE_DECODED_BYTES = 256L * 1024 * 1024;
        internal const long MAX_PACKAGE_REFERENCED_RAW_BYTES = 256L * 1024 * 1024;
        internal const int MAX_PACKAGE_TEXTURES = 2048;
        internal const int MAX_PACKAGE_DECLARED_FRAMES = 4096;
        internal const int MAX_RESOURCE_NAME_LENGTH = 256;
        internal const int MAX_FRAME_DIMENSION = 8192;

        public static BmsManagedPackageNoteRevision Prepare(
            BmsLegacySkin source,
            BmsManagedPackageSourceRevision sourceRevision,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(sourceRevision);

            if (!sourceRevision.HasGameplayAuthority)
                return new BmsManagedPackageNoteRevision(sourceRevision);

            TextureStore? textureStore = null;

            try
            {
                IStorageResourceProvider resources = source.GetManagedPackageResourceProvider();
                validatePackageInventory(resources.Files, sourceRevision, cancellationToken);

                var plans = new Dictionary<BmsManagedPackageNoteSlotKey, NotePlan>();

                foreach (BmsManagedPackageNoteSlotKey slot in enumerateCanonicalSlots())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    GameplaySkinConfigurationDeclaration<string> declaration = source.GetAcceptedBmsNoteResource(
                        slot.Element,
                        slot.Keymode,
                        slot.LaneIndex,
                        slot.IsScratch);

                    if (!declaration.TryGetValue(out string? resourceName))
                        continue;

                    try
                    {
                        NotePlan plan = createPlan(resources.Files, sourceRevision, resourceName, cancellationToken);

                        if (slot.Element == BmsNoteSkinElements.LongNoteBody)
                        {
                            GameplaySkinConfigurationDeclaration<float> widthDeclaration = source.GetAcceptedBmsGeometry(
                                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                                slot.Keymode);
                            BmsGameplaySkinScalarGeometryResolution width = BmsGameplaySkinScalarGeometryResolver.Resolve(
                                BmsSkinConfigurationLookups.LongNoteBodyWidth,
                                widthDeclaration);

                            plan = plan with { LongNoteBodyWidth = width };
                        }

                        plans[slot] = plan;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // A bad declaration rejects only this component. No user-controlled name or storage key escapes.
                    }
                }

                if (plans.Count == 0)
                    return new BmsManagedPackageNoteRevision(sourceRevision);

                Dictionary<string, FrameDescriptor> uniqueFrames = validatePackageRuntimeBudget(plans.Values);
                textureStore = createTextureStore(resources, sourceRevision);
                Dictionary<string, Texture?> decodedFrames = decodeFrames(textureStore, uniqueFrames, cancellationToken);
                var materials = new Dictionary<BmsManagedPackageNoteSlotKey, BmsSourceBoundNoteMaterial>();

                foreach ((BmsManagedPackageNoteSlotKey slot, NotePlan plan) in plans)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var frames = new Texture[plan.Frames.Length];
                    bool valid = true;

                    for (int i = 0; i < plan.Frames.Length; i++)
                    {
                        if (!decodedFrames.TryGetValue(plan.Frames[i].File.PackageName, out Texture? texture) || texture == null)
                        {
                            valid = false;
                            break;
                        }

                        frames[i] = texture;
                    }

                    if (valid)
                        materials[slot] = new BmsSourceBoundNoteMaterial(slot.Element, frames, plan.LongNoteBodyWidth);
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!sourceRevision.Equals(source.CaptureManagedPackageSourceRevision()))
                {
                    textureStore.Dispose();
                    return new BmsManagedPackageNoteRevision(sourceRevision);
                }

                if (materials.Count == 0)
                {
                    textureStore.Dispose();
                    return new BmsManagedPackageNoteRevision(sourceRevision);
                }

                TextureStore publishedTextureStore = textureStore;
                textureStore = null;
                return new BmsManagedPackageNoteRevision(sourceRevision, materials, publishedTextureStore);
            }
            catch (OperationCanceledException)
            {
                textureStore?.Dispose();
                throw;
            }
            catch
            {
                textureStore?.Dispose();
                return new BmsManagedPackageNoteRevision(sourceRevision);
            }
        }

        private static void validatePackageInventory(
            IResourceStore<byte[]> files,
            BmsManagedPackageSourceRevision sourceRevision,
            CancellationToken cancellationToken)
        {
            if (sourceRevision.HasFileNameConflict || sourceRevision.Files.Count > MAX_PACKAGE_FILES)
                throw new InvalidDataException("The gameplay skin package inventory is invalid or exceeds its file-count budget.");

            long packageBytes = 0;

            foreach (BmsManagedPackageFileRevision file in sourceRevision.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using Stream? stream = files.GetStream(file.StorageKey);

                if (stream == null || !stream.CanSeek)
                    throw new InvalidDataException("A gameplay skin package resource is unavailable or not seekable.");

                long length = stream.Length;

                if (length < 0 || length > MAX_FILE_RAW_BYTES)
                    throw new InvalidDataException("A gameplay skin package resource exceeds its raw-byte budget.");

                packageBytes = checked(packageBytes + length);

                if (packageBytes > MAX_PACKAGE_RAW_BYTES)
                    throw new InvalidDataException("The gameplay skin package exceeds its runtime raw-byte budget.");
            }
        }

        private static NotePlan createPlan(
            IResourceStore<byte[]> files,
            BmsManagedPackageSourceRevision sourceRevision,
            string resourceName,
            CancellationToken cancellationToken)
        {
            validateResourceName(resourceName);

            var frames = new List<FrameDescriptor>();
            CandidateResult firstAnimationFrame = resolveFrame(files, sourceRevision, frameName(resourceName, 0), cancellationToken);

            if (firstAnimationFrame.Descriptor != null)
            {
                frames.Add(firstAnimationFrame.Descriptor);

                for (int i = 1; i < MAX_ANIMATION_FRAMES; i++)
                {
                    CandidateResult next = resolveFrame(files, sourceRevision, frameName(resourceName, i), cancellationToken);

                    if (next.Descriptor != null)
                    {
                        frames.Add(next.Descriptor);
                        continue;
                    }

                    if (next.HadPhysicalCandidate)
                        throw new InvalidDataException("A gameplay note animation frame is invalid.");

                    break;
                }

                CandidateResult overBudget = resolveFrame(files, sourceRevision, frameName(resourceName, MAX_ANIMATION_FRAMES), cancellationToken);

                if (overBudget.HadPhysicalCandidate)
                    throw new InvalidDataException("The gameplay note animation exceeds its frame budget.");
            }
            else
            {
                CandidateResult staticFrame = resolveFrame(files, sourceRevision, resourceName, cancellationToken);

                if (staticFrame.Descriptor == null)
                    throw new InvalidDataException("The declared gameplay note resource is missing or invalid.");

                frames.Add(staticFrame.Descriptor);
            }

            long decodedBytes = frames.Aggregate(0L, (total, frame) => checked(total + frame.DecodedBytes));

            if (decodedBytes > MAX_COMPONENT_DECODED_BYTES)
                throw new InvalidDataException("The gameplay note component exceeds its decoded-byte budget.");

            return new NotePlan(frames.ToArray());
        }

        private static CandidateResult resolveFrame(
            IResourceStore<byte[]> files,
            BmsManagedPackageSourceRevision sourceRevision,
            string logicalName,
            CancellationToken cancellationToken)
        {
            string componentName = logicalName.Replace("@2x", string.Empty, StringComparison.Ordinal);
            string highResolutionName = $"{Path.ChangeExtension(componentName, null)}@2x{Path.GetExtension(componentName)}";

            CandidateResult highResolution = resolveCandidateGroup(files, sourceRevision, highResolutionName, 2, cancellationToken);

            if (highResolution.Descriptor != null)
                return highResolution;

            CandidateResult standard = resolveCandidateGroup(files, sourceRevision, componentName, 1, cancellationToken);

            if (standard.Descriptor != null)
                return new CandidateResult(standard.Descriptor, highResolution.HadPhysicalCandidate || standard.HadPhysicalCandidate);

            return new CandidateResult(null, highResolution.HadPhysicalCandidate || standard.HadPhysicalCandidate);
        }

        private static CandidateResult resolveCandidateGroup(
            IResourceStore<byte[]> files,
            BmsManagedPackageSourceRevision sourceRevision,
            string candidateName,
            float scaleAdjust,
            CancellationToken cancellationToken)
        {
            foreach (string candidate in new[] { candidateName, $"{candidateName}.png", $"{candidateName}.jpg" })
            {
                if (!sourceRevision.TryGetFile(candidate, out BmsManagedPackageFileRevision? file))
                    continue;

                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    using Stream? stream = files.GetStream(file.StorageKey);

                    if (stream == null || !stream.CanSeek || stream.Length < 0 || stream.Length > MAX_IMAGE_RAW_BYTES)
                        return new CandidateResult(null, true);

                    ImageInfo? imageInfo = SixLabors.ImageSharp.Image.Identify(stream);

                    if (imageInfo == null
                        || imageInfo.Width <= 0
                        || imageInfo.Height <= 0
                        || imageInfo.Width > MAX_FRAME_DIMENSION
                        || imageInfo.Height > MAX_FRAME_DIMENSION)
                    {
                        return new CandidateResult(null, true);
                    }

                    long pixels = checked((long)imageInfo.Width * imageInfo.Height);

                    if (pixels > MAX_FRAME_PIXELS)
                        return new CandidateResult(null, true);

                    return new CandidateResult(
                        new FrameDescriptor(file, imageInfo.Width, imageInfo.Height, stream.Length, checked(pixels * 4), scaleAdjust),
                        true);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    return new CandidateResult(null, true);
                }
            }

            return new CandidateResult(null, false);
        }

        private static Dictionary<string, FrameDescriptor> validatePackageRuntimeBudget(IEnumerable<NotePlan> plans)
        {
            var uniqueFrames = new Dictionary<string, FrameDescriptor>(StringComparer.OrdinalIgnoreCase);
            int declaredFrames = 0;

            foreach (NotePlan plan in plans)
            {
                declaredFrames = checked(declaredFrames + plan.Frames.Length);

                foreach (FrameDescriptor frame in plan.Frames)
                    uniqueFrames.TryAdd(frame.File.PackageName, frame);
            }

            if (declaredFrames > MAX_PACKAGE_DECLARED_FRAMES || uniqueFrames.Count > MAX_PACKAGE_TEXTURES)
                throw new InvalidDataException("The gameplay skin package exceeds its note frame or texture-count budget.");

            long decodedBytes = uniqueFrames.Values.Aggregate(0L, (total, frame) => checked(total + frame.DecodedBytes));
            long rawBytes = uniqueFrames.Values.Aggregate(0L, (total, frame) => checked(total + frame.RawBytes));

            if (decodedBytes > MAX_PACKAGE_DECODED_BYTES || rawBytes > MAX_PACKAGE_REFERENCED_RAW_BYTES)
                throw new InvalidDataException("The gameplay skin package exceeds its note texture memory budget.");

            return uniqueFrames;
        }

        private static TextureStore createTextureStore(IStorageResourceProvider resources, BmsManagedPackageSourceRevision sourceRevision)
        {
            var snapshotStore = new BmsManagedPackageSnapshotResourceStore(resources.Files, sourceRevision);
            IResourceStore<TextureUpload>? loader = resources.CreateTextureLoaderStore(snapshotStore) ?? throw new InvalidOperationException("The gameplay skin package texture loader is unavailable.");
            return new TextureStore(
                resources.Renderer,
                new LegacyTextureLoaderStore(new MaxDimensionLimitedTextureLoaderStore(loader)));
        }

        private static Dictionary<string, Texture?> decodeFrames(
            TextureStore textureStore,
            IReadOnlyDictionary<string, FrameDescriptor> frames,
            CancellationToken cancellationToken)
        {
            var decoded = new Dictionary<string, Texture?>(StringComparer.OrdinalIgnoreCase);

            foreach ((string name, FrameDescriptor frame) in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    Texture? texture = textureStore.Get(name, WrapMode.ClampToEdge, WrapMode.ClampToEdge);

                    if (texture == null || texture.Width != frame.Width || texture.Height != frame.Height)
                    {
                        decoded[name] = null;
                        continue;
                    }

                    texture.ScaleAdjust = frame.ScaleAdjust;
                    decoded[name] = texture;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // A frame may pass metadata identification but fail full pixel decode. Keep that failure scoped to
                    // the note components which reference this exact frame instead of rejecting every note in the package.
                    decoded[name] = null;
                }
            }

            return decoded;
        }

        private static IEnumerable<BmsManagedPackageNoteSlotKey> enumerateCanonicalSlots()
        {
            foreach (BmsNoteSkinElements element in new[]
                     {
                          BmsNoteSkinElements.Note,
                          BmsNoteSkinElements.LongNoteHead,
                          BmsNoteSkinElements.LongNoteBody,
                          BmsNoteSkinElements.LongNoteTail,
                     })
            {
                yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key5K, 0, true);
                for (int i = 1; i <= 5; i++)
                    yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key5K, i, false);

                yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key7K, 0, true);
                for (int i = 1; i <= 7; i++)
                    yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key7K, i, false);

                foreach (BmsKeymode keymode in new[] { BmsKeymode.Key9K_Bms, BmsKeymode.Key9K_Pms })
                {
                    for (int i = 0; i <= 8; i++)
                        yield return new BmsManagedPackageNoteSlotKey(element, keymode, i, false);
                }

                yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key14K, 0, true);
                for (int i = 1; i <= 14; i++)
                    yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key14K, i, false);
                yield return new BmsManagedPackageNoteSlotKey(element, BmsKeymode.Key14K, 15, true);
            }
        }

        private static void validateResourceName(string resourceName)
        {
            ArgumentNullException.ThrowIfNull(resourceName);

            if (string.IsNullOrWhiteSpace(resourceName)
                || resourceName.Length > MAX_RESOURCE_NAME_LENGTH
                || Path.IsPathRooted(resourceName)
                || resourceName.IndexOf(':') >= 0
                || resourceName.IndexOf('\0') >= 0)
            {
                throw new InvalidDataException("The declared gameplay note resource name is not a valid package-relative name.");
            }

            string[] segments = resourceName.Split(new[] { '/', '\\' });

            if (segments.Any(segment => string.IsNullOrEmpty(segment) || segment is "." or ".."))
                throw new InvalidDataException("The declared gameplay note resource name is not contained by its package.");
        }

        private static string frameName(string resourceName, int index) => $"{resourceName}-{index}";

        private sealed record NotePlan(
            FrameDescriptor[] Frames,
            BmsGameplaySkinScalarGeometryResolution? LongNoteBodyWidth = null);

        private sealed record FrameDescriptor(
            BmsManagedPackageFileRevision File,
            int Width,
            int Height,
            long RawBytes,
            long DecodedBytes,
            float ScaleAdjust);

        private readonly record struct CandidateResult(FrameDescriptor? Descriptor, bool HadPhysicalCandidate);
    }

    /// <summary>
    /// Maps immutable package filenames to immutable Realm content-addressed storage keys for one prepared revision.
    /// </summary>
    internal sealed class BmsManagedPackageSnapshotResourceStore : IResourceStore<byte[]>
    {
        private readonly IResourceStore<byte[]> files;
        private readonly BmsManagedPackageSourceRevision sourceRevision;

        public BmsManagedPackageSnapshotResourceStore(IResourceStore<byte[]> files, BmsManagedPackageSourceRevision sourceRevision)
        {
            this.files = files ?? throw new ArgumentNullException(nameof(files));
            this.sourceRevision = sourceRevision ?? throw new ArgumentNullException(nameof(sourceRevision));
        }

        public byte[] Get(string name)
            => sourceRevision.TryGetFile(name, out BmsManagedPackageFileRevision? file) ? files.Get(file.StorageKey) : null!;

        public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
            => sourceRevision.TryGetFile(name, out BmsManagedPackageFileRevision? file)
                ? files.GetAsync(file.StorageKey, cancellationToken)
                : Task.FromResult<byte[]>(null!);

        public Stream? GetStream(string name)
            => sourceRevision.TryGetFile(name, out BmsManagedPackageFileRevision? file) ? files.GetStream(file.StorageKey) : null;

        public IEnumerable<string> GetAvailableResources() => sourceRevision.Files.Select(file => file.PackageName);

        public void Dispose()
        {
            // The global Realm file store is owned by SkinManager. This immutable view never disposes it.
        }
    }

    /// <summary>
    /// Immutable decoded note material and its component-local scalar geometry owned by one prepared package revision.
    /// </summary>
    internal sealed class BmsSourceBoundNoteMaterial
    {
        private readonly Texture[] frames;

        public BmsNoteSkinElements Element { get; }
        public int FrameCount => frames.Length;
        public BmsGameplaySkinScalarGeometryResolution? LongNoteBodyWidth { get; }

        public BmsSourceBoundNoteMaterial(
            BmsNoteSkinElements element,
            Texture[] frames,
            BmsGameplaySkinScalarGeometryResolution? longNoteBodyWidth = null)
        {
            ArgumentNullException.ThrowIfNull(frames);

            if (frames.Length == 0 || Array.Exists(frames, frame => frame == null))
                throw new ArgumentException("A gameplay note material must contain at least one texture frame.", nameof(frames));

            if (element is not (BmsNoteSkinElements.Note
                or BmsNoteSkinElements.LongNoteHead
                or BmsNoteSkinElements.LongNoteBody
                or BmsNoteSkinElements.LongNoteTail))
            {
                throw new ArgumentOutOfRangeException(nameof(element), element, "The gameplay note material uses an unsupported element.");
            }

            if ((element == BmsNoteSkinElements.LongNoteBody) != longNoteBodyWidth.HasValue)
                throw new ArgumentException("Only a long-note body material must carry its resolved width.", nameof(longNoteBodyWidth));

            if (longNoteBodyWidth is { } width
                && (!float.IsFinite(width.Value) || width.Value <= 0 || width.Value > 1))
            {
                throw new ArgumentOutOfRangeException(nameof(longNoteBodyWidth), width.Value, "The resolved long-note body width is invalid.");
            }

            Element = element;
            this.frames = (Texture[])frames.Clone();
            LongNoteBodyWidth = longNoteBodyWidth;
        }

        public Drawable CreateDrawable()
        {
            Drawable visual;

            if (frames.Length == 1)
            {
                visual = new Sprite { Texture = frames[0] };
            }
            else
            {
                var animation = new LegacySkinExtensions.SkinnableTextureAnimation
                {
                    DefaultFrameLength = LegacySkinExtensions.SIXTY_FRAME_TIME,
                    Loop = true,
                };

                foreach (Texture frame in frames)
                    animation.AddFrame(frame);

                visual = animation;
            }

            return Element == BmsNoteSkinElements.LongNoteBody
                ? new BmsSourceBoundLongNoteBodyDrawable(visual, LongNoteBodyWidth!.Value.Value)
                : new BmsSourceBoundNoteDrawable(visual);
        }
    }

    /// <summary>
    /// Neutral note host sizing for a source-bound static sprite or frame animation.
    /// </summary>
    internal sealed partial class BmsSourceBoundNoteDrawable : CompositeDrawable
    {
        public BmsSourceBoundNoteDrawable(Drawable visual)
        {
            ArgumentNullException.ThrowIfNull(visual);

            RelativeSizeAxes = Axes.Both;
            visual.RelativeSizeAxes = Axes.Both;
            visual.Size = Vector2.One;
            InternalChild = visual;
        }
    }
}
