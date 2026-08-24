// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// An OMS legacy skin that, on top of the core <see cref="LegacySkin"/> behaviour (general + mania sections),
    /// also parses the BMS-specific (<c>[General]</c> / <c>[Bms]</c>) sections of skin.ini and answers
    /// <see cref="BmsSkinConfigurationLookup"/> queries.
    /// </summary>
    /// <remarks>
    /// Lives in the ruleset assembly so it can reference <see cref="BmsSkinDecoder"/> (osu.Game must never depend on a
    /// ruleset). It extends core skin parsing purely via the <see cref="ParseConfigurationStream"/> hook — mania-section
    /// parsing (which lives in core <see cref="LegacySkin"/>) is preserved untouched, BMS parsing is layered on top.
    /// Intended to be reached by reflection (skin <c>InstantiationInfo</c>) so the core importer needs no compile-time
    /// ruleset reference.
    /// </remarks>
    public class BmsLegacySkin : LegacySkin
    {
        private readonly Dictionary<BmsKeymode, BmsSkinConfiguration> bmsConfigurations = new Dictionary<BmsKeymode, BmsSkinConfiguration>();
        private readonly BmsLegacySkinBackingKind backingKind;
        private readonly IStorageResourceProvider? resourceProvider;
        private readonly BmsManagedPackageSourceRevision? immutablePackageSourceRevision;
        private readonly object managedPackageNotePreparationLock = new object();
        private readonly CancellationTokenSource managedPackageNotePreparationCancellation = new CancellationTokenSource();
        // Includes active, cached and request-abandoned generations until their true Task completion. A cancelled
        // caller may stop waiting before synchronous package IO observes cancellation, so the revision owner must keep
        // every such generation claimable for final detach/shutdown join.
        private readonly HashSet<ManagedPackageNotePreparationGeneration> managedPackageNotePreparations = new HashSet<ManagedPackageNotePreparationGeneration>();
        private ManagedPackageNotePreparationGeneration? managedPackageNotePreparation;
        private string? parsedConfigurationContentHash;
        private bool disposed;

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(SkinInfo skin, IStorageResourceProvider resources)
            : this(skin, resources, null, @"skin.ini", BmsLegacySkinBackingKind.RealmPackage)
        {
        }

        /// <summary>
        /// Compatibility/test construction with an explicit fallback store. This is not managed-folder authority.
        /// </summary>
        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(SkinInfo skin, IStorageResourceProvider resources, IResourceStore<byte[]> folderStore)
            : this(skin, resources, folderStore, @"skin.ini", BmsLegacySkinBackingKind.ExplicitFallbackStore)
        {
        }

        /// <summary>
        /// Managed-folder construction over one already captured immutable package revision. The package store is the
        /// only source of skin bytes; renderer/audio/texture-loader services still come from <paramref name="resources"/>.
        /// </summary>
        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(
            SkinInfo skin,
            IStorageResourceProvider resources,
            IResourceStore<byte[]> packageStore,
            bool useExactPackageStore)
            : this(skin, resources, packageStore, @"skin.ini", BmsLegacySkinBackingKind.CapturedManagedFolder)
        {
            if (!useExactPackageStore)
                throw new ArgumentException("The managed-folder constructor requires exact package authority.", nameof(useExactPackageStore));
        }

        protected BmsLegacySkin(SkinInfo skin, IStorageResourceProvider? resources, IResourceStore<byte[]>? fallbackStore, string configurationFilename = @"skin.ini")
            : this(
                skin,
                resources,
                fallbackStore,
                configurationFilename,
                fallbackStore == null ? BmsLegacySkinBackingKind.RealmPackage : BmsLegacySkinBackingKind.ExplicitFallbackStore)
        {
        }

        private BmsLegacySkin(
            SkinInfo skin,
            IStorageResourceProvider? resources,
            IResourceStore<byte[]>? fallbackStore,
            string configurationFilename,
            BmsLegacySkinBackingKind backingKind)
            : base(
                skin,
                resources,
                fallbackStore,
                configurationFilename,
                useExactPackageStore: backingKind == BmsLegacySkinBackingKind.CapturedManagedFolder)
        {
            this.backingKind = backingKind;

            if (backingKind == BmsLegacySkinBackingKind.CapturedManagedFolder)
            {
                var packageStore = (ISkinPackageRevisionResourceStore)fallbackStore!;
                resourceProvider = new ExactPackageResourceProvider(resources!, packageStore);
                immutablePackageSourceRevision = BmsManagedPackageSourceRevision.CreateImmutableCapsule(
                    skin.ID,
                    parsedConfigurationContentHash,
                    packageStore.ContentRevision,
                    packageStore.Files);
            }
            else
                resourceProvider = resources;
        }

        protected override void ParseConfigurationStream(Stream stream)
        {
            // Snapshot the stream first: the base (mania) parse consumes its own reader, so BMS parsing reads a copy.
            // Field initialisers run before the base constructor, so bmsConfigurations is already set when the base
            // constructor calls this virtual during construction (same pattern as core mania parsing).
            using var copy = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(copy);

            copy.Position = 0;
            parsedConfigurationContentHash = copy.ComputeSHA2Hash();

            // CopyTo() leaves the original stream at EOF. The base legacy parser still needs to read
            // [General], [Colours], and [Mania] from the beginning of the same stream.
            stream.Position = 0;
            base.ParseConfigurationStream(stream);

            copy.Position = 0;
            using var reader = new StreamReader(copy);
            var decoder = new BmsSkinDecoder();
            decoder.Parse(reader);

            foreach (var config in decoder.Configurations)
                bmsConfigurations[config.Keymode] = config;
        }

        public override IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
        {
            if (lookup is BmsSkinConfigurationLookup bmsLookup)
                return lookupBms<TValue>(bmsLookup);

            return base.GetConfig<TLookup, TValue>(lookup);
        }

        private IBindable<TValue>? lookupBms<TValue>(BmsSkinConfigurationLookup lookup)
        {
            if (!bmsConfigurations.TryGetValue(lookup.Keymode, out var config))
                return null;

            // The typeof(TValue) guards keep SkinUtils.As (a hard cast) safe even if a caller queries the wrong type.
            if (typeof(TValue) == typeof(float) && config.Geometry.TryGetValue(lookup.Lookup, out float number))
                return SkinUtils.As<TValue>(new Bindable<float>(number));

            if (typeof(TValue) == typeof(Color4) && config.Colours.TryGetValue(lookup.Lookup, out var colour))
                return SkinUtils.As<TValue>(new Bindable<Color4>(colour));

            if (typeof(TValue) == typeof(string))
            {
                string? imageKey = resolveImageKey(lookup);

                if (imageKey != null && config.ImageLookups.TryGetValue(imageKey, out string? path))
                    return SkinUtils.As<TValue>(new Bindable<string>(path));
            }

            return null;
        }

        /// <summary>
        /// Whether this instance owns an exact eligible package revision which may participate in the first
        /// source-isolated gameplay component path.
        /// </summary>
        /// <remarks>
        /// Realm liveness alone is not storage authority. Realm packages must pass their existing metadata contract;
        /// managed folders instead use the immutable revision captured before this instance was constructed.
        /// </remarks>
        internal bool HasManagedPackageGameplayAuthority => CaptureManagedPackageSourceRevision().HasGameplayAuthority;

        internal BmsManagedPackageSourceRevision CaptureManagedPackageSourceRevision()
        {
            if (immutablePackageSourceRevision != null)
                return immutablePackageSourceRevision;

            bool isManaged = SkinInfo.IsManaged;

            return SkinInfo.PerformRead(info =>
            {
                BmsManagedPackageFileRevision[] files = info.Files
                                                            .Select(file => new BmsManagedPackageFileRevision(
                                                                file.Filename,
                                                                file.File.Hash,
                                                                file.File.GetStoragePath()))
                                                            .OrderBy(file => file.PackageName, StringComparer.Ordinal)
                                                            .ThenBy(file => file.ContentHash, StringComparer.Ordinal)
                                                            .ToArray();

                return new BmsManagedPackageSourceRevision(
                    info.ID,
                    backingKind == BmsLegacySkinBackingKind.RealmPackage && resourceProvider != null && isManaged,
                    info.FilesystemStoragePath,
                    info.IsExternalFilesystemStorage,
                    info.DeletePending,
                    parsedConfigurationContentHash,
                    files);
            });
        }

        internal IStorageResourceProvider GetManagedPackageResourceProvider()
            => resourceProvider ?? throw new InvalidOperationException("The gameplay skin package has no managed resource provider.");

        internal BmsManagedPackageNoteRevision GetOrPrepareManagedPackageNotes(CancellationToken requestCancellationToken)
        {
            requestCancellationToken.ThrowIfCancellationRequested();

            ManagedPackageNotePreparationGeneration preparation;
            ManagedPackageNotePreparationGeneration? stalePreparation = null;

            lock (managedPackageNotePreparationLock)
            {
                ObjectDisposedException.ThrowIf(disposed, this);

                if (managedPackageNotePreparation == null
                    || managedPackageNotePreparation.Abandoned
                    || managedPackageNotePreparation.Task.IsCanceled
                    || managedPackageNotePreparation.Task.IsFaulted)
                {
                    stalePreparation = managedPackageNotePreparation;

                    if (stalePreparation != null)
                        stalePreparation.Abandoned = true;

                    BmsManagedPackageSourceRevision sourceRevision = CaptureManagedPackageSourceRevision();
                    var workCancellation = CancellationTokenSource.CreateLinkedTokenSource(managedPackageNotePreparationCancellation.Token);
                    CancellationToken cancellationToken = workCancellation.Token;
                    SkinCurrentRevisionLease? revisionWorkLease = BmsManagedPackageNoteLoadContext.TryTakeRevisionWorkLease();
                    Task<BmsManagedPackageNoteRevision> task;

                    try
                    {
                        task = Task.Run(
                            () => BmsManagedPackageNoteMaterializer.Prepare(this, sourceRevision, cancellationToken),
                            cancellationToken);
                    }
                    catch
                    {
                        revisionWorkLease?.Dispose();
                        workCancellation.Dispose();
                        throw;
                    }

                    managedPackageNotePreparation = new ManagedPackageNotePreparationGeneration(task, workCancellation, revisionWorkLease);
                    managedPackageNotePreparations.Add(managedPackageNotePreparation);
                }

                preparation = managedPackageNotePreparation;
                preparation.Waiters++;
            }

            if (stalePreparation != null)
                disposePreparationWhenComplete(stalePreparation);

            try
            {
                return requestCancellationToken.CanBeCanceled
                    ? preparation.Task.WaitAsync(requestCancellationToken).GetAwaiter().GetResult()
                    : preparation.Task.GetAwaiter().GetResult();
            }
            finally
            {
                bool cancelAbandonedPreparation = false;

                lock (managedPackageNotePreparationLock)
                {
                    preparation.Waiters--;

                    if (preparation.Waiters == 0
                        && !preparation.Task.IsCompleted
                        && requestCancellationToken.IsCancellationRequested)
                    {
                        preparation.Abandoned = true;

                        if (ReferenceEquals(preparation, managedPackageNotePreparation))
                            managedPackageNotePreparation = null;

                        cancelAbandonedPreparation = true;
                    }
                }

                if (cancelAbandonedPreparation)
                {
                    tryCancelPreparation(preparation);
                    disposePreparationWhenComplete(preparation);
                }
            }
        }

        private static void tryCancelPreparation(ManagedPackageNotePreparationGeneration preparation)
        {
            try
            {
                preparation.Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Completion cleanup won the race; there is no remaining package work to cancel.
            }
        }

        private void disposePreparationWhenComplete(ManagedPackageNotePreparationGeneration preparation)
        {
            if (Interlocked.Exchange(ref preparation.CleanupRegistered, 1) != 0)
                return;

            _ = preparation.Task.ContinueWith(
                _ => cleanupPreparation(preparation),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        private void cleanupPreparation(ManagedPackageNotePreparationGeneration preparation)
        {
            if (Interlocked.Exchange(ref preparation.CleanupClaimed, 1) != 0)
            {
                preparation.CleanupCompletion.Task.GetAwaiter().GetResult();
                return;
            }

            try
            {
                if (preparation.Task.Status == TaskStatus.RanToCompletion)
                {
                    try
                    {
                        preparation.Task.GetAwaiter().GetResult().Dispose();
                    }
                    catch
                    {
                        // Retirement is best-effort for provider-owned GPU resources, but the remaining generation
                        // bookkeeping and exact package release must still complete.
                    }
                }
                else if (preparation.Task.IsFaulted)
                    _ = preparation.Task.Exception;

                try
                {
                    preparation.Cancellation.Dispose();
                }
                catch (ObjectDisposedException)
                {
                    // Exactly-once cleanup normally prevents this; tolerate an already reaped CTS defensively.
                }

                lock (managedPackageNotePreparationLock)
                    managedPackageNotePreparations.Remove(preparation);
            }
            finally
            {
                preparation.CleanupCompletion.TrySetResult();
            }
        }

        /// <summary>
        /// Returns the decoder-time accepted native <c>[Bms] NoteImage*</c> declaration for one supported note element
        /// and canonical BMS lane.
        /// </summary>
        /// <remarks>
        /// This deliberately reads the closed accepted-declaration sidecar rather than the mutable compatibility
        /// dictionary. Invalid engine lane/role pairs fail closed as absent and do not inherit the legacy lookup's
        /// permissive 14K scratch aliasing.
        /// </remarks>
        internal GameplaySkinConfigurationDeclaration<string> GetAcceptedBmsNoteResource(
            BmsNoteSkinElements element,
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch)
        {
            GameplaySkinLaneResourceField? field = element switch
            {
                BmsNoteSkinElements.Note => GameplaySkinLaneResourceFieldCatalog.Note,
                BmsNoteSkinElements.LongNoteHead => GameplaySkinLaneResourceFieldCatalog.LongNoteHead,
                BmsNoteSkinElements.LongNoteBody => GameplaySkinLaneResourceFieldCatalog.LongNoteBody,
                BmsNoteSkinElements.LongNoteTail => GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
                _ => null,
            };

            if (field == null)
                return GameplaySkinConfigurationDeclaration<string>.Absent;

            if (!tryGetCanonicalLaneToken(keymode, laneIndex, isScratch, out string? laneToken)
                || !bmsConfigurations.TryGetValue(keymode, out BmsSkinConfiguration? configuration))
            {
                return GameplaySkinConfigurationDeclaration<string>.Absent;
            }

            return configuration.GetAcceptedLaneResource(field, laneToken);
        }

        /// <summary>
        /// Returns one decoder-time accepted native BMS scalar geometry declaration for an exact keymode bucket.
        /// </summary>
        /// <remarks>
        /// This deliberately bypasses the mutable compatibility dictionary and the aggregate skin source. Validation
        /// and defaulting remain the responsibility of <see cref="BmsGameplaySkinScalarGeometryResolver"/>.
        /// </remarks>
        internal GameplaySkinConfigurationDeclaration<float> GetAcceptedBmsGeometry(
            BmsSkinConfigurationLookups field,
            BmsKeymode keymode)
        {
            BmsGameplaySkinBucketGeometryFieldCatalog.Validate(field, nameof(field));

            return bmsConfigurations.TryGetValue(keymode, out BmsSkinConfiguration? configuration)
                ? configuration.GetAcceptedGeometry(field)
                : GameplaySkinConfigurationDeclaration<float>.Absent;
        }

        internal GameplaySkinConfigurationDeclaration<string> GetAcceptedBmsOrdinaryNoteResource(
            BmsKeymode keymode,
            int laneIndex,
            bool isScratch)
            => GetAcceptedBmsNoteResource(BmsNoteSkinElements.Note, keymode, laneIndex, isScratch);

        private static bool tryGetCanonicalLaneToken(BmsKeymode keymode, int laneIndex, bool isScratch, out string laneToken)
        {
            laneToken = string.Empty;

            switch (keymode)
            {
                case BmsKeymode.Key5K:
                    if (laneIndex is < 0 or > 5 || isScratch != (laneIndex == 0))
                        return false;

                    break;

                case BmsKeymode.Key7K:
                    if (laneIndex is < 0 or > 7 || isScratch != (laneIndex == 0))
                        return false;

                    break;

                case BmsKeymode.Key9K_Bms:
                case BmsKeymode.Key9K_Pms:
                    if (laneIndex is < 0 or > 8 || isScratch)
                        return false;

                    break;

                case BmsKeymode.Key14K:
                    if (laneIndex is < 0 or > 15 || isScratch != (laneIndex is 0 or 15))
                        return false;

                    if (laneIndex == 15)
                    {
                        laneToken = "S2";
                        return true;
                    }

                    break;

                default:
                    return false;
            }

            laneToken = isScratch ? "S" : laneIndex.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        /// <summary>
        /// Maps a logical texture lookup to the full ini key the decoder stored it under. Per-lane keys embed the lane
        /// token (a digit, or <c>S</c> for scratch); global slots use the enum name verbatim.
        /// </summary>
        private static string? resolveImageKey(BmsSkinConfigurationLookup lookup)
        {
            bool isSecondScratch = lookup.IsScratch
                                   && lookup.Keymode == BmsKeymode.Key14K
                                   && lookup.LaneIndex.HasValue
                                   && lookup.LaneIndex.Value >= 8;
            string laneToken = lookup.IsScratch
                ? (isSecondScratch ? "S2" : "S")
                : lookup.LaneIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
            bool hasLane = laneToken.Length > 0;

            switch (lookup.Lookup)
            {
                case BmsSkinConfigurationLookups.NoteImage:
                    return hasLane ? $"NoteImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.HoldNoteHeadImage:
                    return hasLane ? $"NoteImage{laneToken}H" : null;

                case BmsSkinConfigurationLookups.HoldNoteBodyImage:
                    return hasLane ? $"NoteImage{laneToken}L" : null;

                case BmsSkinConfigurationLookups.HoldNoteTailImage:
                    return hasLane ? $"NoteImage{laneToken}T" : null;

                case BmsSkinConfigurationLookups.KeyImage:
                    return hasLane ? $"KeyImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.KeyImageDown:
                    return hasLane ? $"KeyImage{laneToken}D" : null;

                case BmsSkinConfigurationLookups.LaneBackgroundImage:
                    return hasLane ? $"LaneBackgroundImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.LaneDividerImage:
                    return hasLane ? $"LaneDividerImage{laneToken}" : null;

                case BmsSkinConfigurationLookups.HitTargetImage:
                case BmsSkinConfigurationLookups.StageLeftImage:
                case BmsSkinConfigurationLookups.StageRightImage:
                case BmsSkinConfigurationLookups.StageBottomImage:
                case BmsSkinConfigurationLookups.StageHintImage:
                case BmsSkinConfigurationLookups.PlayfieldBackdropImage:
                case BmsSkinConfigurationLookups.LaneCoverTopImage:
                case BmsSkinConfigurationLookups.LaneCoverBottomImage:
                    return lookup.Lookup.ToString();

                default:
                    return null;
            }
        }

        private enum BmsLegacySkinBackingKind
        {
            RealmPackage,
            ExplicitFallbackStore,
            CapturedManagedFolder,
        }

        private sealed class ExactPackageResourceProvider : IStorageResourceProvider
        {
            private readonly IStorageResourceProvider services;

            public IRenderer Renderer => services.Renderer;
            public AudioManager? AudioManager => services.AudioManager;
            public IResourceStore<byte[]> Files { get; }
            public IResourceStore<byte[]> Resources => services.Resources;
            public RealmAccess RealmAccess => services.RealmAccess;

            public ExactPackageResourceProvider(IStorageResourceProvider services, IResourceStore<byte[]> files)
            {
                this.services = services;
                Files = files;
            }

            public IResourceStore<TextureUpload>? CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore)
                => services.CreateTextureLoaderStore(underlyingStore);
        }

        private sealed class ManagedPackageNotePreparationGeneration
        {
            public readonly Task<BmsManagedPackageNoteRevision> Task;
            public readonly CancellationTokenSource Cancellation;
            private SkinCurrentRevisionLease? revisionWorkLease;

            public int Waiters;
            public int CleanupRegistered;
            public int CleanupClaimed;
            public bool Abandoned;
            public readonly TaskCompletionSource CleanupCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            public ManagedPackageNotePreparationGeneration(
                Task<BmsManagedPackageNoteRevision> task,
                CancellationTokenSource cancellation,
                SkinCurrentRevisionLease? revisionWorkLease)
            {
                Task = task;
                Cancellation = cancellation;
                this.revisionWorkLease = revisionWorkLease;

                _ = task.ContinueWith(
                    _ => Interlocked.Exchange(ref this.revisionWorkLease, null)?.Dispose(),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            ManagedPackageNotePreparationGeneration[] preparations;

            lock (managedPackageNotePreparationLock)
            {
                if (disposed)
                    return;

                disposed = true;
                managedPackageNotePreparation = null;
                preparations = managedPackageNotePreparations.ToArray();

                foreach (ManagedPackageNotePreparationGeneration preparation in preparations)
                    preparation.Abandoned = true;
            }

            managedPackageNotePreparationCancellation.Cancel();

            foreach (ManagedPackageNotePreparationGeneration preparation in preparations)
                tryCancelPreparation(preparation);

            foreach (ManagedPackageNotePreparationGeneration preparation in preparations)
            {
                try
                {
                    preparation.Task.GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    // Cancellation is the expected owner-retirement path for unfinished materialization.
                }
                catch
                {
                    // Materialization failures are already fail-closed at the provider boundary. Retirement must still
                    // join and reap every generation before the exact package store is released.
                }

                cleanupPreparation(preparation);
            }

            base.Dispose(isDisposing);
            managedPackageNotePreparationCancellation.Dispose();
        }
    }
}
