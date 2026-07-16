// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Bindables;
using osu.Framework.IO.Stores;
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
        private readonly object managedPackageNotePreparationLock = new object();
        private readonly CancellationTokenSource managedPackageNotePreparationCancellation = new CancellationTokenSource();
        private ManagedPackageNotePreparationGeneration? managedPackageNotePreparation;
        private bool disposed;

        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(SkinInfo skin, IStorageResourceProvider resources)
            : this(skin, resources, null, @"skin.ini", BmsLegacySkinBackingKind.RealmPackage)
        {
        }

        /// <summary>
        /// Folder-backed construction (G1 visible-folder skins): skin.ini + textures are read directly from
        /// <paramref name="folderStore"/> — a store over a visible skin folder such as <c>chartskin/&lt;name&gt;</c> —
        /// instead of the realm hash-backed file store. Intended to be reached by reflection from the core skin
        /// instantiation path for skins whose <see cref="SkinInfo"/> carries a filesystem storage path; the empty realm
        /// <c>Files</c> list falls through to this store (the same fallback-store pattern <see cref="OmsSkin"/> uses).
        /// </summary>
        [UsedImplicitly(ImplicitUseKindFlags.InstantiatedWithFixedConstructorSignature)]
        public BmsLegacySkin(SkinInfo skin, IStorageResourceProvider resources, IResourceStore<byte[]> folderStore)
            : this(skin, resources, folderStore, @"skin.ini", BmsLegacySkinBackingKind.ExplicitFallbackStore)
        {
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
            : base(skin, resources, fallbackStore, configurationFilename)
        {
            this.backingKind = backingKind;
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
        /// Whether this instance is a normal Realm-backed <c>.osk</c> package which may participate in the first
        /// source-isolated gameplay component path.
        /// </summary>
        /// <remarks>
        /// Realm liveness alone is not storage authority. Folder-backed and conflicting schema records remain excluded
        /// until the G1 authority and migration gates are implemented. No filesystem path is returned or logged here.
        /// </remarks>
        internal bool HasManagedPackageGameplayAuthority
        {
            get
            {
                if (backingKind != BmsLegacySkinBackingKind.RealmPackage || !SkinInfo.IsManaged)
                    return false;

                return SkinInfo.PerformRead(info =>
                    string.IsNullOrEmpty(info.FilesystemStoragePath)
                    && !info.IsExternalFilesystemStorage
                    && !info.DeletePending
                    && info.Files.Count > 0);
            }
        }

        internal BmsManagedPackageSourceRevision CaptureManagedPackageSourceRevision()
        {
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

                    Task<BmsManagedPackageNoteRevision> task = Task.Run(
                        () => BmsManagedPackageNoteMaterializer.Prepare(this, sourceRevision, cancellationToken),
                        cancellationToken);

                    managedPackageNotePreparation = new ManagedPackageNotePreparationGeneration(task, workCancellation);
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

        private static void disposePreparationWhenComplete(ManagedPackageNotePreparationGeneration preparation)
        {
            if (Interlocked.Exchange(ref preparation.CleanupRegistered, 1) != 0)
                return;

            _ = preparation.Task.ContinueWith(
                task =>
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                        task.GetAwaiter().GetResult().Dispose();

                    preparation.Cancellation.Dispose();
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
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
        }

        private sealed class ManagedPackageNotePreparationGeneration
        {
            public readonly Task<BmsManagedPackageNoteRevision> Task;
            public readonly CancellationTokenSource Cancellation;

            public int Waiters;
            public int CleanupRegistered;
            public bool Abandoned;

            public ManagedPackageNotePreparationGeneration(
                Task<BmsManagedPackageNoteRevision> task,
                CancellationTokenSource cancellation)
            {
                Task = task;
                Cancellation = cancellation;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            lock (managedPackageNotePreparationLock)
            {
                if (disposed)
                    return;

                disposed = true;
            }

            managedPackageNotePreparationCancellation.Cancel();

            ManagedPackageNotePreparationGeneration? preparation;

            lock (managedPackageNotePreparationLock)
            {
                preparation = managedPackageNotePreparation;
                managedPackageNotePreparation = null;

                if (preparation != null)
                    preparation.Abandoned = true;
            }

            if (preparation != null)
            {
                tryCancelPreparation(preparation);
                disposePreparationWhenComplete(preparation);
            }

            base.Dispose(isDisposing);
            managedPackageNotePreparationCancellation.Dispose();
        }
    }
}
