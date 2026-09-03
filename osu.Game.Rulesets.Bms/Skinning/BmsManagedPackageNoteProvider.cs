// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.IO;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Retains the pre-C4 nullable managed-package note lookup for detached compatibility callers.
    /// </summary>
    /// <remarks>
    /// Eligible sources are either one validated Realm <c>.osk</c> revision or one immutable managed-folder capsule.
    /// Exact C3 lookups are prohibited here: production consumes only the committed material set. This provider exists
    /// solely for callers which have no exact layout/material publication and is not part of C4 completion authority.
    /// </remarks>
    internal sealed class BmsManagedPackageNoteCompatibilityProvider :
        IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsNoteSkinLookup>, IBmsResolvedNoteMaterial>
    {
        private readonly BmsLegacySkin source;

        public string Name => "legacy.compatibility.managed-package-bms-note";

        public BmsManagedPackageNoteCompatibilityProvider(BmsLegacySkin source)
        {
            ArgumentNullException.ThrowIfNull(source);
            this.source = source;
        }

        public bool ClaimsCompatibilityDeclaration(BmsNoteSkinLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            return lookup.LayoutSnapshot == null
                   && lookup.MaterialSet == null
                   && TryGetDescriptor(lookup.Element, out _)
                   && source.GetAcceptedBmsNoteResource(lookup.Element, lookup.Keymode, lookup.LaneIndex, lookup.IsScratch).IsDeclared;
        }

        public GameplaySkinSlotResolution<IBmsResolvedNoteMaterial> ResolveCompatibility(BmsNoteSkinLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            if (lookup.LayoutSnapshot != null || lookup.MaterialSet != null)
                throw new ArgumentException("The legacy managed-package compatibility resolver cannot consume an exact publication lookup.", nameof(lookup));

            if (!TryGetDescriptor(lookup.Element, out GameplaySkinSlotDescriptor descriptor))
            {
                return GameplaySkinSlotResolver.Resolve(
                    GameplaySkinSlotCatalog.Note,
                    lookup,
                    Array.Empty<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsNoteSkinLookup>, IBmsResolvedNoteMaterial>>());
            }

            return GameplaySkinSlotResolver.Resolve(
                descriptor,
                lookup,
                new[] { this },
                material => material.FrameCount > 0);
        }

        public SkinSlotResult<IBmsResolvedNoteMaterial> GetSlot(GameplaySkinSlotLookup<BmsNoteSkinLookup> slot)
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (slot.Context.LayoutSnapshot != null || slot.Context.MaterialSet != null)
                throw new ArgumentException("The legacy managed-package compatibility provider cannot consume an exact publication lookup.", nameof(slot));

            if (!TryGetDescriptor(slot.Context.Element, out GameplaySkinSlotDescriptor descriptor)
                || !ReferenceEquals(slot.Descriptor, descriptor))
            {
                return SkinSlotResult<IBmsResolvedNoteMaterial>.Inherit;
            }

            BmsManagedPackageSourceRevision currentRevision = source.CaptureManagedPackageSourceRevision();

            if (!currentRevision.HasGameplayAuthority)
                throw new InvalidOperationException("The selected gameplay skin source is not an eligible managed package.");

            GameplaySkinConfigurationDeclaration<string> declaration = source.GetAcceptedBmsNoteResource(
                slot.Context.Element,
                slot.Context.Keymode,
                slot.Context.LaneIndex,
                slot.Context.IsScratch);

            if (!declaration.IsDeclared)
                return SkinSlotResult<IBmsResolvedNoteMaterial>.Inherit;

            var materialSlot = new BmsManagedPackageNoteSlotKey(
                slot.Context.Element,
                slot.Context.Keymode,
                slot.Context.LaneIndex,
                slot.Context.IsScratch);
            BmsManagedPackageNoteRevision prepared = source.GetOrPrepareManagedPackageNotes(BmsManagedPackageNoteLoadContext.CurrentCancellationToken);

            if (!prepared.SourceRevision.Equals(currentRevision))
                throw new InvalidOperationException("The selected gameplay skin package changed while its note resources were being prepared.");

            if (!prepared.TryGetMaterial(materialSlot, out IBmsResolvedNoteMaterial? material))
                return SkinSlotResult<IBmsResolvedNoteMaterial>.Inherit;

            return SkinSlotResult<IBmsResolvedNoteMaterial>.Provide(material!);
        }

        internal static bool TryGetDescriptor(BmsNoteSkinElements element, out GameplaySkinSlotDescriptor descriptor)
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
        private static readonly AsyncLocal<CancellationToken?> current_cancellation_token = new AsyncLocal<CancellationToken?>();
        private static readonly AsyncLocal<SkinCurrentRevisionLeaseTransfer?> current_revision_lease_transfer = new AsyncLocal<SkinCurrentRevisionLeaseTransfer?>();

        public static CancellationToken CurrentCancellationToken => current_cancellation_token.Value ?? CancellationToken.None;

        public static IDisposable Enter(
            CancellationToken cancellationToken,
            SkinCurrentRevisionLeaseTransfer? revisionLeaseTransfer = null)
        {
            CancellationToken? previous = current_cancellation_token.Value;
            SkinCurrentRevisionLeaseTransfer? previousRevisionLeaseTransfer = current_revision_lease_transfer.Value;
            current_cancellation_token.Value = cancellationToken;
            current_revision_lease_transfer.Value = revisionLeaseTransfer;
            return new Scope(previous, previousRevisionLeaseTransfer);
        }

        public static SkinCurrentRevisionLease? TryTakeRevisionWorkLease()
            => current_revision_lease_transfer.Value?.TryTake();

        private sealed class Scope : IDisposable
        {
            private readonly CancellationToken? previous;
            private readonly SkinCurrentRevisionLeaseTransfer? previousRevisionLeaseTransfer;
            private bool disposed;

            public Scope(
                CancellationToken? previous,
                SkinCurrentRevisionLeaseTransfer? previousRevisionLeaseTransfer)
            {
                this.previous = previous;
                this.previousRevisionLeaseTransfer = previousRevisionLeaseTransfer;
            }

            public void Dispose()
            {
                if (disposed)
                    return;

                disposed = true;
                current_cancellation_token.Value = previous;
                current_revision_lease_transfer.Value = previousRevisionLeaseTransfer;
            }
        }
    }

    internal readonly record struct BmsManagedPackageNoteSlotKey(
        BmsNoteSkinElements Element,
        BmsKeymode Keymode,
        int LaneIndex,
        bool IsScratch);

    /// <summary>
    /// Final authority retained for each exact Note/LN result. Compatibility buckets are still part of the selected
    /// package; the programmatic fallback is a separate terminal authority.
    /// </summary>
    internal enum BmsPreparedNoteMaterialAuthority
    {
        SelectedDocument = 0,
        SelectedLegacyBms = 1,
        SelectedLegacyMania = 2,
        ProgrammaticFallback = 3,
    }

    /// <summary>
    /// Owns the stable source identity and precedence contract for every selected-package BMS Note/LN authority.
    /// Callers must use this factory rather than matching stable-id strings independently.
    /// </summary>
    internal static class BmsPreparedNoteMaterialAuthorityIdentity
    {
        public static IReadOnlyList<BmsPreparedNoteMaterialAuthority> SelectedInPrecedenceOrder { get; } =
            Array.AsReadOnly(new[]
            {
                BmsPreparedNoteMaterialAuthority.SelectedDocument,
                BmsPreparedNoteMaterialAuthority.SelectedLegacyBms,
                BmsPreparedNoteMaterialAuthority.SelectedLegacyMania,
            });

        public static GameplaySkinResolvedMaterialSourceIdentity CreateSelected(
            BmsPreparedNoteMaterialAuthority authority,
            string configurationRevision)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(configurationRevision);

            string stableId = authority switch
            {
                BmsPreparedNoteMaterialAuthority.SelectedDocument => "selected-document",
                BmsPreparedNoteMaterialAuthority.SelectedLegacyBms => "selected-legacy-bms",
                BmsPreparedNoteMaterialAuthority.SelectedLegacyMania => "selected-legacy-mania",
                _ => throw new ArgumentOutOfRangeException(nameof(authority), authority, "Only a selected-package authority has a selected identity."),
            };

            return authority == BmsPreparedNoteMaterialAuthority.SelectedDocument
                ? GameplaySkinResolvedMaterialSourceIdentity.CreateSelectedDocument(stableId, configurationRevision)
                : GameplaySkinResolvedMaterialSourceIdentity.Create(
                    GameplaySkinResolvedMaterialSourceKind.SelectedPackage,
                    stableId,
                    configurationRevision);
        }

        public static GameplaySkinResolvedMaterialSourceIdentity Programmatic { get; } =
            GameplaySkinResolvedMaterialSourceIdentity.Create(
                GameplaySkinResolvedMaterialSourceKind.ProgrammaticFallback,
                "bms-programmatic",
                "v1");

        public static bool TryGetSelectedAuthority(
            GameplaySkinResolvedMaterialSourceIdentity source,
            out BmsPreparedNoteMaterialAuthority authority)
        {
            ArgumentNullException.ThrowIfNull(source);

            foreach (BmsPreparedNoteMaterialAuthority candidate in SelectedInPrecedenceOrder)
            {
                if (source.Equals(CreateSelected(candidate, source.ContentRevision)))
                {
                    authority = candidate;
                    return true;
                }
            }

            authority = default;
            return false;
        }
    }

    /// <summary>
    /// Validates and projects one exact BMS lane to the shared material target without deriving any identity or index.
    /// </summary>
    internal static class BmsGameplayNoteMaterialTarget
    {
        public static BmsGameplayLayoutLane ValidateLookup(BmsNoteSkinLookup lookup)
        {
            ArgumentNullException.ThrowIfNull(lookup);

            BmsGameplayLayoutSnapshot layout = lookup.LayoutSnapshot
                                               ?? throw new ArgumentException("An exact BMS material lookup requires its committed layout.", nameof(lookup));

            if (layout.Keymode != lookup.Keymode)
                throw new ArgumentException("A BMS material lookup keymode must match its exact layout.", nameof(lookup));

            BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(lookup.LaneIndex);

            if (lookup.LaneId == null
                || !lane.LaneId.Equals(lookup.LaneId)
                || lane.IsScratch != lookup.IsScratch)
            {
                throw new ArgumentException("A BMS material lookup must retain the exact stable lane identity and role.", nameof(lookup));
            }

            return lane;
        }

        public static GameplaySkinResolvedMaterialTarget Create(
            BmsGameplayLayoutSnapshot layout,
            BmsGameplayLayoutLane lane)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(lane);

            GameplaySkinLaneTopologySnapshot topology = layout.Neutral.Context.Topology;

            if (!topology.TryGetLane(lane.LaneId, out GameplaySkinLaneTopologyEntry? topologyLane)
                || topologyLane == null
                || !ReferenceEquals(lane.NeutralLane.TopologyEntry, topologyLane)
                || topologyLane.GlobalLogicalIndex != lane.LogicalIndex
                || topologyLane.GlobalVisualIndex != lane.VisualIndex
                || topologyLane.GroupLocalLogicalIndex != lane.GroupLocalLogicalIndex
                || topologyLane.GroupLocalVisualIndex != lane.GroupLocalVisualIndex
                || !topology.TryGetGroup(topologyLane.Identity.Group.Id, out GameplaySkinLaneTopologyGroup? group)
                || group == null
                || group.LogicalIndex != lane.GroupLogicalIndex)
            {
                throw new ArgumentException("A BMS material target must retain every exact C3 lane coordinate.", nameof(lane));
            }

            return GameplaySkinResolvedMaterialTarget.ForLane(group, topologyLane);
        }
    }

    internal sealed record BmsManagedPackageFileRevision(string PackageName, string ContentHash, string StorageKey);

    /// <summary>
    /// Immutable authority and file mapping captured from one eligible package revision.
    /// </summary>
    internal sealed class BmsManagedPackageSourceRevision : IEquatable<BmsManagedPackageSourceRevision>
    {
        private readonly BmsManagedPackageFileRevision[] files;
        private readonly Dictionary<string, BmsManagedPackageFileRevision> filesByName;

        public Guid SkinId { get; }
        public string? ParsedConfigurationContentHash { get; }
        public string? PackageContentRevision { get; }
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
            IEnumerable<BmsManagedPackageFileRevision> files,
            string? packageContentRevision = null)
            : this(
                skinId,
                isRealmManaged,
                filesystemStoragePath,
                isExternalFilesystemStorage,
                deletePending,
                parsedConfigurationContentHash,
                packageContentRevision,
                files)
        {
        }

        private BmsManagedPackageSourceRevision(
            Guid skinId,
            bool hasPackageAuthority,
            string? filesystemStoragePath,
            bool isExternalFilesystemStorage,
            bool deletePending,
            string? parsedConfigurationContentHash,
            string? packageContentRevision,
            IEnumerable<BmsManagedPackageFileRevision> files)
        {
            ArgumentNullException.ThrowIfNull(files);

            SkinId = skinId;
            ParsedConfigurationContentHash = parsedConfigurationContentHash;
            PackageContentRevision = packageContentRevision;
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
                                                       && StringComparer.OrdinalIgnoreCase.Equals(configurationFile.ContentHash, parsedConfigurationContentHash);

            HasFileNameConflict = conflict;
            HasGameplayAuthority = hasPackageAuthority
                                   && string.IsNullOrEmpty(filesystemStoragePath)
                                   && !isExternalFilesystemStorage
                                   && !deletePending
                                   && this.files.Length > 0
                                   && !conflict
                                   && parsedConfigurationMatchesPackage;
        }

        public static BmsManagedPackageSourceRevision CreateImmutableCapsule(
            Guid skinId,
            string? parsedConfigurationContentHash,
            string packageContentRevision,
            IReadOnlyList<SkinPackageFileRevision> files)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packageContentRevision);
            ArgumentNullException.ThrowIfNull(files);

            return new BmsManagedPackageSourceRevision(
                skinId,
                hasPackageAuthority: true,
                filesystemStoragePath: null,
                isExternalFilesystemStorage: false,
                deletePending: false,
                parsedConfigurationContentHash,
                packageContentRevision,
                files.Select(file => new BmsManagedPackageFileRevision(
                    file.ResourceName,
                    file.ContentHash,
                    file.ResourceName)));
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
                || !StringComparer.Ordinal.Equals(PackageContentRevision, other.PackageContentRevision)
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
            hash.Add(PackageContentRevision, StringComparer.Ordinal);
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
        private readonly IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, IBmsResolvedNoteMaterial> materials;
        private readonly IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority> materialAuthorities;
        private readonly IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority> suppressedSlots;
        private readonly IReadOnlyList<BmsPreparedNoteDiagnostic> diagnostics;
        private readonly TextureStore? textures;
        public BmsManagedPackageSourceRevision SourceRevision { get; }

        public BmsGameplayLayoutSnapshot? LayoutSnapshot { get; }

        internal bool IsDisposed { get; private set; }

        public BmsManagedPackageNoteRevision(
            BmsManagedPackageSourceRevision sourceRevision,
            IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, IBmsResolvedNoteMaterial>? materials = null,
            TextureStore? textures = null,
            BmsGameplayLayoutSnapshot? layoutSnapshot = null,
            IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority>? materialAuthorities = null,
            IReadOnlyDictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority>? suppressedSlots = null,
            IReadOnlyList<BmsPreparedNoteDiagnostic>? diagnostics = null)
        {
            SourceRevision = sourceRevision ?? throw new ArgumentNullException(nameof(sourceRevision));
            this.materials = materials ?? new Dictionary<BmsManagedPackageNoteSlotKey, IBmsResolvedNoteMaterial>();
            this.textures = textures;
            LayoutSnapshot = layoutSnapshot;
            this.materialAuthorities = materialAuthorities ?? new Dictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority>();
            this.suppressedSlots = suppressedSlots ?? new Dictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority>();
            this.diagnostics = diagnostics ?? Array.Empty<BmsPreparedNoteDiagnostic>();

            if (layoutSnapshot == null && (this.materialAuthorities.Count != 0 || this.suppressedSlots.Count != 0 || this.diagnostics.Count != 0))
                throw new ArgumentException("Only an exact-layout note revision may carry resolved provenance or diagnostics.", nameof(layoutSnapshot));

            if (layoutSnapshot != null && this.materials.Keys.Any(slot => slot.Keymode != layoutSnapshot.Keymode))
                throw new ArgumentException("An exact-layout note revision can contain only its parser-owned keymode.", nameof(materials));

            if (this.materialAuthorities.Keys.Any(slot => !this.materials.ContainsKey(slot)))
                throw new ArgumentException("Every exact note material provenance must identify a prepared material.", nameof(materialAuthorities));

            if (layoutSnapshot != null && this.materials.Keys.Any(slot => !this.materialAuthorities.ContainsKey(slot)))
                throw new ArgumentException("Every exact note material must retain its final authority.", nameof(materialAuthorities));

            if (this.suppressedSlots.Keys.Any(slot => this.materials.ContainsKey(slot)))
                throw new ArgumentException("A resolved BMS note slot cannot be both provided and suppressed.", nameof(suppressedSlots));

            if (this.suppressedSlots.Any(pair => getDescriptor(pair.Key.Element).SuppressEligibility != GameplaySkinSlotSuppressEligibility.Allowed
                                                 || pair.Value != BmsPreparedNoteMaterialAuthority.SelectedDocument))
            {
                throw new ArgumentException("Only a selected-document declaration may suppress a catalog-eligible BMS note slot.", nameof(suppressedSlots));
            }
        }

        public bool TryGetMaterial(BmsManagedPackageNoteSlotKey slot, out IBmsResolvedNoteMaterial? material)
            => materials.TryGetValue(slot, out material);

        public GameplaySkinResolvedMaterialSet CreateMaterialSet(
            BmsGameplayLayoutSnapshot layout,
            GameplaySkinMaterialContractIdentity contractIdentity)
        {
            ArgumentNullException.ThrowIfNull(layout);
            ArgumentNullException.ThrowIfNull(contractIdentity);

            if (!ReferenceEquals(LayoutSnapshot, layout))
                throw new ArgumentException("A BMS resolved material set must use the exact layout prepared by this revision.", nameof(layout));

            GameplaySkinPackageRevision packageRevision = layout.Neutral.Context.PackageRevision;

            if (SourceRevision.SkinId != packageRevision.RecordId
                || !StringComparer.Ordinal.Equals(SourceRevision.PackageContentRevision, packageRevision.ContentRevision))
            {
                throw new ArgumentException("A BMS resolved material set must use the exact selected package identity and content revision.", nameof(layout));
            }

            string configurationRevision = SourceRevision.ParsedConfigurationContentHash
                                           ?? throw new InvalidOperationException("An exact selected-package material must retain its configuration content revision.");
            var entries = new List<GameplaySkinResolvedMaterialEntry>(materials.Count + suppressedSlots.Count);

            foreach ((BmsManagedPackageNoteSlotKey slot, IBmsResolvedNoteMaterial material) in materials)
            {
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(slot.LaneIndex);

                if (slot.Keymode != layout.Keymode || lane.IsScratch != slot.IsScratch)
                    throw new InvalidOperationException("A prepared BMS material no longer matches its exact layout lane.");

                GameplaySkinSlotDescriptor descriptor = getDescriptor(slot.Element);
                GameplaySkinResolvedMaterialTarget target = BmsGameplayNoteMaterialTarget.Create(layout, lane);
                BmsPreparedNoteMaterialAuthority authority = materialAuthorities[slot];
                GameplaySkinResolvedMaterialSourceIdentity sourceIdentity = authority == BmsPreparedNoteMaterialAuthority.ProgrammaticFallback
                    ? BmsPreparedNoteMaterialAuthorityIdentity.Programmatic
                    : BmsPreparedNoteMaterialAuthorityIdentity.CreateSelected(authority, configurationRevision);
                entries.Add(GameplaySkinResolvedMaterialEntry.Provide(descriptor, target, sourceIdentity, material));
            }

            foreach ((BmsManagedPackageNoteSlotKey slot, _) in suppressedSlots)
            {
                BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(slot.LaneIndex);

                if (slot.Keymode != layout.Keymode || lane.IsScratch != slot.IsScratch)
                    throw new InvalidOperationException("A suppressed BMS material no longer matches its exact layout lane.");

                entries.Add(GameplaySkinResolvedMaterialEntry.Suppress(
                    getDescriptor(slot.Element),
                    BmsGameplayNoteMaterialTarget.Create(layout, lane),
                    BmsPreparedNoteMaterialAuthorityIdentity.CreateSelected(
                        BmsPreparedNoteMaterialAuthority.SelectedDocument,
                        configurationRevision)));
            }

            GameplaySkinResolvedMaterialDiagnostic[] resolvedDiagnostics = diagnostics
                .Select(diagnostic =>
                {
                    GameplaySkinResolvedMaterialSourceIdentity source = diagnostic.Authority == BmsPreparedNoteMaterialAuthority.ProgrammaticFallback
                        ? BmsPreparedNoteMaterialAuthorityIdentity.Programmatic
                        : BmsPreparedNoteMaterialAuthorityIdentity.CreateSelected(diagnostic.Authority, configurationRevision);
                    return diagnostic.Key != null
                        ? new GameplaySkinResolvedMaterialDiagnostic(diagnostic.Code, diagnostic.Key, source)
                        : GameplaySkinResolvedMaterialDiagnostic.ForDocument(diagnostic.Code, source, diagnostic.CatalogSlot);
                })
                .ToArray();

            return GameplaySkinResolvedMaterialSet.Create(layout.Neutral, contractIdentity, entries, resolvedDiagnostics);
        }

        private static GameplaySkinSlotDescriptor getDescriptor(BmsNoteSkinElements element)
        {
            if (!BmsManagedPackageNoteCompatibilityProvider.TryGetDescriptor(element, out GameplaySkinSlotDescriptor descriptor))
                throw new ArgumentOutOfRangeException(nameof(element), element, "Unsupported prepared BMS note element.");

            return descriptor;
        }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;
            textures?.Dispose();
        }
    }

    internal sealed record BmsPreparedNoteDiagnostic(
        GameplaySkinResolvedMaterialKey? Key,
        GameplaySkinSlotDescriptor? CatalogSlot,
        string Code,
        BmsPreparedNoteMaterialAuthority Authority);

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

        /// <summary>
        /// Exact C4 surface hosted by the BMS Note/LN material prepare. Catalog authoring eligibility remains separate.
        /// </summary>
        internal static GameplaySkinRuntimeCapabilitySet RuntimeCapabilities { get; } = GameplaySkinRuntimeCapabilitySet.Create(new[]
        {
            GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.Note, GameplaySkinRuntimeSlotCapability.Provide),
            GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.LongNoteHead, GameplaySkinRuntimeSlotCapability.Provide),
            GameplaySkinRuntimeSlotSupport.Create(GameplaySkinSlotCatalog.LongNoteBody, GameplaySkinRuntimeSlotCapability.Provide),
            GameplaySkinRuntimeSlotSupport.Create(
                GameplaySkinSlotCatalog.LongNoteTail,
                GameplaySkinRuntimeSlotCapability.Provide | GameplaySkinRuntimeSlotCapability.Suppress),
        });

        /// <summary>
        /// Resolves and prepares every Note/LN component for one exact C3 layout in a single package-wide pass.
        /// </summary>
        public static BmsManagedPackageNoteRevision PrepareExact(
            BmsLegacySkin source,
            BmsManagedPackageSourceRevision sourceRevision,
            BmsGameplayLayoutSnapshot layout,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(sourceRevision);
            ArgumentNullException.ThrowIfNull(layout);

            if (!sourceRevision.HasGameplayAuthority)
                throw new InvalidOperationException("An exact BMS material prepare requires an eligible immutable package revision.");

            TextureStore? textureStore = null;
            var diagnostics = new List<BmsPreparedNoteDiagnostic>();

            try
            {
                IStorageResourceProvider resources = source.GetManagedPackageResourceProvider();
                validatePackageInventory(resources.Files, sourceRevision, cancellationToken);

                GameplaySkinDocument? document = null;

                if (layout.Neutral.Context.PackageRevision.SourceKind != GameplaySkinPackageSourceKind.Compatibility)
                {
                    if (!source.AllowsGameplaySkinDocumentAuthoring)
                        throw new InvalidOperationException("This selected source is not eligible for package-author gameplay skin declarations.");

                    document = source.GameplaySkinDocument.BindToPublication(layout.Neutral);

                    if (!string.Equals(document.Identity.ContentRevision, sourceRevision.ParsedConfigurationContentHash, StringComparison.Ordinal)
                        || document.Identity.SourceId != sourceRevision.SkinId)
                    {
                        throw new InvalidOperationException("The retained gameplay skin document does not match the exact selected package revision.");
                    }

                    addDocumentDiagnostics(document, layout, diagnostics);
                    addUnsupportedCapabilityDiagnostics(document, layout, diagnostics);
                }

                BmsGameplaySkinConfigurationCandidatePlan candidatePlan = BmsGameplaySkinConfigurationCandidateFactory.CreateExact(
                    layout,
                    source.GetParsedBmsConfigurationsForGameplaySkinCompatibility(),
                    source.GetParsedManiaConfigurationsForGameplaySkinCompatibility());
                var pendingPlans = new Dictionary<BmsManagedPackageNoteSlotKey, PlannedNoteSelection>();
                var attemptedPlans = new List<NotePlan>();
                var suppressedSlots = new Dictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority>();
                var materials = new Dictionary<BmsManagedPackageNoteSlotKey, IBmsResolvedNoteMaterial>();
                var materialAuthorities = new Dictionary<BmsManagedPackageNoteSlotKey, BmsPreparedNoteMaterialAuthority>();

                using (var owner = new NotePlanOwner(source, resources.Files, sourceRevision, layout.Keymode, cancellationToken))
                {
                    var providers = new List<IGameplaySkinSlotProvider<GameplaySkinSlotLookup<BmsGameplaySkinLaneResourceContext>, PlannedNoteComponent>>();

                    if (document != null)
                    {
                        providers.Add(new GameplaySkinDocumentSlotProvider<BmsGameplaySkinLaneResourceContext, PlannedNoteComponent>(
                            document,
                            RuntimeCapabilities,
                            "selected.document",
                            context => createMaterialTarget(layout, context),
                            (entry, context) => owner.Materialize(new BmsGameplaySkinLaneResourceReference(context, entry.Value!))));
                    }

                    providers.AddRange(BmsGameplaySkinLaneResourceCandidateProviderFactory.Create(candidatePlan, owner));

                    if (providers.Select(provider => provider.Name).Distinct(StringComparer.Ordinal).Count() != providers.Count)
                        throw new InvalidOperationException("Selected-package material provider names must be unique within one exact candidate plan.");

                    foreach (BmsManagedPackageNoteSlotKey slot in enumerateExactSlots(layout))
                        resolveSlot(slot, 0);

                    var decodedFrames = new Dictionary<string, Texture?>(StringComparer.OrdinalIgnoreCase);

                    while (pendingPlans.Count > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        // Failed higher-authority plans remain in this cumulative budget. Retrying a lower authority
                        // must not let one package evade the decoded-byte/frame limits by rotating provider winners.
                        Dictionary<string, FrameDescriptor> uniqueFrames = validatePackageRuntimeBudget(attemptedPlans);
                        textureStore ??= createTextureStore(resources, sourceRevision);
                        decodeFrames(textureStore, uniqueFrames, decodedFrames, cancellationToken);
                        var retries = new List<(BmsManagedPackageNoteSlotKey Slot, int ProviderIndex)>();

                        foreach ((BmsManagedPackageNoteSlotKey slot, PlannedNoteSelection selection) in pendingPlans.ToArray())
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            pendingPlans.Remove(slot);
                            NotePlan plan = selection.Component.Plan;
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

                            if (!valid)
                            {
                                diagnostics.Add(createDiagnostic(
                                    layout,
                                    slot,
                                    "bms.material.decode-failed",
                                    getAuthority(selection.Component.Source)));
                                retries.Add((slot, selection.ProviderIndex + 1));
                                continue;
                            }

                            materials.Add(slot, new BmsSourceBoundNoteMaterial(slot.Element, frames, plan.LongNoteBodyWidth));
                            materialAuthorities.Add(slot, getAuthority(selection.Component.Source));
                        }

                        foreach ((BmsManagedPackageNoteSlotKey slot, int providerIndex) in retries)
                            resolveSlot(slot, providerIndex);
                    }

                    void resolveSlot(BmsManagedPackageNoteSlotKey slot, int providerIndex)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (providerIndex < 0 || providerIndex > providers.Count)
                            throw new ArgumentOutOfRangeException(nameof(providerIndex));

                        BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(slot.LaneIndex);
                        GameplaySkinLaneResourceField field = getField(slot.Element);
                        var context = new BmsGameplaySkinLaneResourceContext(
                            layout.Neutral.Context.Topology,
                            lane.LaneId,
                            field);
                        GameplaySkinSlotResolution<PlannedNoteComponent> resolution = GameplaySkinSlotResolver.Resolve(
                            field.Slot,
                            context,
                            providers.Skip(providerIndex),
                            component => component.Plan.Frames.Length > 0);

                        foreach (GameplaySkinSlotDiagnostic diagnostic in resolution.Diagnostics)
                            diagnostics.Add(createDiagnostic(
                                layout,
                                slot,
                                GetDiagnosticCode(diagnostic),
                                getDiagnosticAuthority(diagnostic)));

                        if (resolution.Result.Kind == SkinSlotResultKind.Provide)
                        {
                            int resolvedProviderIndex = providers.FindIndex(
                                providerIndex,
                                provider => string.Equals(provider.Name, resolution.ProviderName, StringComparison.Ordinal));

                            if (resolvedProviderIndex < 0)
                                throw new InvalidOperationException("The selected-package resolver did not retain its winning provider authority.");

                            PlannedNoteComponent component = resolution.Result.Value;
                            pendingPlans.Add(slot, new PlannedNoteSelection(component, resolvedProviderIndex));
                            attemptedPlans.Add(component.Plan);
                        }
                        else if (resolution.Result.Kind == SkinSlotResultKind.Suppress)
                        {
                            suppressedSlots.Add(slot, BmsPreparedNoteMaterialAuthority.SelectedDocument);
                        }
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();

                if (!sourceRevision.Equals(source.CaptureManagedPackageSourceRevision()))
                    throw new InvalidOperationException("The selected gameplay skin package changed during exact material preparation.");

                TextureStore? publishedTextureStore = textureStore;
                textureStore = null;
                return new BmsManagedPackageNoteRevision(
                    sourceRevision,
                    materials,
                    publishedTextureStore,
                    layout,
                    materialAuthorities,
                    suppressedSlots,
                    diagnostics);
            }
            catch (OperationCanceledException)
            {
                textureStore?.Dispose();
                throw;
            }
            catch
            {
                textureStore?.Dispose();
                throw;
            }
        }

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
                var decodedFrames = new Dictionary<string, Texture?>(StringComparer.OrdinalIgnoreCase);
                decodeFrames(textureStore, uniqueFrames, decodedFrames, cancellationToken);
                var materials = new Dictionary<BmsManagedPackageNoteSlotKey, IBmsResolvedNoteMaterial>();

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

        private static void decodeFrames(
            TextureStore textureStore,
            IReadOnlyDictionary<string, FrameDescriptor> frames,
            IDictionary<string, Texture?> decoded,
            CancellationToken cancellationToken)
        {
            foreach ((string name, FrameDescriptor frame) in frames)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (decoded.ContainsKey(name))
                    continue;

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

        private static IEnumerable<BmsManagedPackageNoteSlotKey> enumerateExactSlots(BmsGameplayLayoutSnapshot layout)
        {
            foreach (BmsNoteSkinElements element in new[]
                     {
                         BmsNoteSkinElements.Note,
                         BmsNoteSkinElements.LongNoteHead,
                         BmsNoteSkinElements.LongNoteBody,
                         BmsNoteSkinElements.LongNoteTail,
                     })
            {
                foreach (BmsGameplayLayoutLane lane in layout.LanesInLogicalOrder)
                    yield return new BmsManagedPackageNoteSlotKey(element, layout.Keymode, lane.LogicalIndex, lane.IsScratch);
            }
        }

        private static GameplaySkinLaneResourceField getField(BmsNoteSkinElements element)
            => element switch
            {
                BmsNoteSkinElements.Note => GameplaySkinLaneResourceFieldCatalog.Note,
                BmsNoteSkinElements.LongNoteHead => GameplaySkinLaneResourceFieldCatalog.LongNoteHead,
                BmsNoteSkinElements.LongNoteBody => GameplaySkinLaneResourceFieldCatalog.LongNoteBody,
                BmsNoteSkinElements.LongNoteTail => GameplaySkinLaneResourceFieldCatalog.LongNoteTail,
                _ => throw new ArgumentOutOfRangeException(nameof(element), element, "Unsupported exact BMS note element."),
            };

        private static GameplaySkinSlotDescriptor getSlotDescriptor(BmsNoteSkinElements element)
        {
            if (!BmsManagedPackageNoteCompatibilityProvider.TryGetDescriptor(element, out GameplaySkinSlotDescriptor descriptor))
                throw new ArgumentOutOfRangeException(nameof(element), element, "Unsupported exact BMS note element.");

            return descriptor;
        }

        internal static string GetDiagnosticCode(GameplaySkinSlotDiagnostic diagnostic)
        {
            if (diagnostic.Exception is GameplaySkinDocumentSlotRejectedException rejection)
                return $"bms.material.{rejection.Code}";

            return diagnostic.Code switch
            {
                GameplaySkinSlotDiagnosticCode.ProviderFailed => "bms.material.provider-failed",
                GameplaySkinSlotDiagnosticCode.ProvidedValueRejected => "bms.material.value-rejected",
                GameplaySkinSlotDiagnosticCode.ProvidedValueValidationFailed => "bms.material.validation-failed",
                GameplaySkinSlotDiagnosticCode.CriticalSuppressionRejected => "bms.material.suppress-rejected",
                GameplaySkinSlotDiagnosticCode.InvalidResult => "bms.material.result-invalid",
                _ => "bms.material.resolution-failed",
            };
        }

        private static GameplaySkinResolvedMaterialTarget createMaterialTarget(
            BmsGameplayLayoutSnapshot layout,
            BmsGameplaySkinLaneResourceContext context)
        {
            if (!ReferenceEquals(context.Topology, layout.Neutral.Context.Topology))
                throw new ArgumentException("A document material lookup must retain the exact publication topology.", nameof(context));

            return BmsGameplayNoteMaterialTarget.Create(layout, layout.GetLane(context.LaneId));
        }

        private static BmsPreparedNoteMaterialAuthority getAuthority(BmsGameplaySkinConfigurationCandidateSource source)
            => source switch
            {
                BmsGameplaySkinConfigurationCandidateSource.SelectedDocument => BmsPreparedNoteMaterialAuthority.SelectedDocument,
                BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride => BmsPreparedNoteMaterialAuthority.SelectedLegacyBms,
                BmsGameplaySkinConfigurationCandidateSource.ManiaFullVisualLane
                    or BmsGameplaySkinConfigurationCandidateSource.ManiaEightColumnDeck
                    or BmsGameplaySkinConfigurationCandidateSource.ManiaKeyOnly => BmsPreparedNoteMaterialAuthority.SelectedLegacyMania,
                _ => throw new ArgumentOutOfRangeException(nameof(source), source, "A resolved selected-package component has no material authority."),
            };

        private static BmsPreparedNoteMaterialAuthority getDiagnosticAuthority(GameplaySkinSlotDiagnostic diagnostic)
        {
            if (diagnostic.ProviderName == "selected.document")
                return BmsPreparedNoteMaterialAuthority.SelectedDocument;

            if (diagnostic.ProviderName == "selected.bms-role-override")
                return BmsPreparedNoteMaterialAuthority.SelectedLegacyBms;

            if (diagnostic.ProviderName.StartsWith("selected.mania-", StringComparison.Ordinal))
                return BmsPreparedNoteMaterialAuthority.SelectedLegacyMania;

            // Redacted/invalid provider identity is still a selected-package resolution failure. It cannot claim the
            // programmatic terminal authority and carries no author-controlled token into the persisted diagnostic.
            return BmsPreparedNoteMaterialAuthority.SelectedDocument;
        }

        private static BmsPreparedNoteDiagnostic createDiagnostic(
            BmsGameplayLayoutSnapshot layout,
            BmsManagedPackageNoteSlotKey slot,
            string code,
            BmsPreparedNoteMaterialAuthority authority)
        {
            BmsGameplayLayoutLane lane = layout.GetLaneByLogicalIndex(slot.LaneIndex);
            return new BmsPreparedNoteDiagnostic(
                new GameplaySkinResolvedMaterialKey(
                    getSlotDescriptor(slot.Element),
                    BmsGameplayNoteMaterialTarget.Create(layout, lane)),
                getSlotDescriptor(slot.Element),
                code,
                authority);
        }

        private static void addDocumentDiagnostics(
            GameplaySkinDocument document,
            BmsGameplayLayoutSnapshot layout,
            ICollection<BmsPreparedNoteDiagnostic> diagnostics)
        {
            foreach (GameplaySkinCodecDiagnostic diagnostic in document.Diagnostics)
            {
                GameplaySkinDocumentEntry? entry = document.Sections
                    .SelectMany(section => section.Entries)
                    .LastOrDefault(candidate => candidate.LineNumber == diagnostic.LineNumber);

                if (entry != null
                    && !GameplaySkinSlotApplicabilityValidator.IsSelectorApplicable(entry.Target, layout.Neutral))
                {
                    continue;
                }

                GameplaySkinSlotDescriptor? descriptor = entry?.Descriptor;

                if (diagnostic.SlotId != null
                    && GameplaySkinSlotCatalog.TryGet(diagnostic.SlotId, out GameplaySkinSlotDescriptor? catalogDescriptor))
                {
                    descriptor = catalogDescriptor;
                }

                string code = diagnostic.Id;

                if (entry != null
                    && descriptor != null
                    && GameplaySkinSlotApplicabilityValidator.ValidatePublicationTarget(entry.Target, layout.Neutral)
                    == GameplaySkinDocumentPublicationTargetValidationResult.Valid
                    && tryCreateMaterialTarget(layout, entry.Target, out GameplaySkinResolvedMaterialTarget? target))
                {
                    try
                    {
                        diagnostics.Add(new BmsPreparedNoteDiagnostic(
                            new GameplaySkinResolvedMaterialKey(descriptor, target!),
                            descriptor,
                            code,
                            BmsPreparedNoteMaterialAuthority.SelectedDocument));
                        continue;
                    }
                    catch (ArgumentException)
                    {
                        // A known slot with invalid scope/applicability remains a document-level diagnostic carrying
                        // the catalog ID. It must never borrow an unrelated first-lane material key.
                    }
                }

                diagnostics.Add(new BmsPreparedNoteDiagnostic(
                    null,
                    descriptor,
                    code,
                    BmsPreparedNoteMaterialAuthority.SelectedDocument));
            }
        }

        private static void addUnsupportedCapabilityDiagnostics(
            GameplaySkinDocument document,
            BmsGameplayLayoutSnapshot layout,
            ICollection<BmsPreparedNoteDiagnostic> diagnostics)
        {
            foreach (GameplaySkinDocumentEntry entry in document.Sections.SelectMany(section => section.Entries))
            {
                if (entry.Presence != GameplaySkinDocumentDeclarationPresence.Declared
                    || entry.Descriptor == null
                    || RuntimeCapabilities.TryGet(entry.Descriptor, out _)
                    || GameplaySkinSlotApplicabilityValidator.ValidatePublicationTarget(entry.Target, layout.Neutral)
                    != GameplaySkinDocumentPublicationTargetValidationResult.Valid
                    || !tryCreateMaterialTarget(layout, entry.Target, out GameplaySkinResolvedMaterialTarget? target))
                {
                    continue;
                }

                try
                {
                    diagnostics.Add(new BmsPreparedNoteDiagnostic(
                        new GameplaySkinResolvedMaterialKey(entry.Descriptor, target!),
                        entry.Descriptor,
                        "bms.capability.unsupported-slot",
                        BmsPreparedNoteMaterialAuthority.SelectedDocument));
                }
                catch (ArgumentException)
                {
                    // Scope/index invalidity is already retained by the shared codec diagnostic.
                }
            }
        }

        private static bool tryCreateMaterialTarget(
            BmsGameplayLayoutSnapshot layout,
            GameplaySkinDocumentTarget documentTarget,
            out GameplaySkinResolvedMaterialTarget? target)
        {
            GameplaySkinLaneTopologySnapshot topology = layout.Neutral.Context.Topology;

            switch (documentTarget.Kind)
            {
                case GameplaySkinDocumentTargetKind.Global:
                    target = GameplaySkinResolvedMaterialTarget.Global;
                    break;

                case GameplaySkinDocumentTargetKind.Stage:
                case GameplaySkinDocumentTargetKind.Group:
                    if (documentTarget.GroupId == null
                        || !topology.TryGetGroup(documentTarget.GroupId, out GameplaySkinLaneTopologyGroup? group)
                        || group == null)
                    {
                        target = null;
                        return false;
                    }

                    target = documentTarget.Kind == GameplaySkinDocumentTargetKind.Stage
                        ? GameplaySkinResolvedMaterialTarget.ForStage(group)
                        : GameplaySkinResolvedMaterialTarget.ForGroup(group);
                    break;

                case GameplaySkinDocumentTargetKind.Lane:
                    if (documentTarget.GroupId == null
                        || documentTarget.LaneId == null
                        || !topology.TryGetGroup(documentTarget.GroupId, out GameplaySkinLaneTopologyGroup? laneGroup)
                        || laneGroup == null
                        || !topology.TryGetLane(documentTarget.LaneId, out GameplaySkinLaneTopologyEntry? topologyLane)
                        || topologyLane == null)
                    {
                        target = null;
                        return false;
                    }

                    target = GameplaySkinResolvedMaterialTarget.ForLane(laneGroup, topologyLane);
                    break;

                default:
                    target = null;
                    return false;
            }

            if (!documentTarget.Matches(layout.Neutral, target))
            {
                target = null;
                return false;
            }

            return true;
        }

        private static void addDiagnosticForEverySlot(
            BmsGameplayLayoutSnapshot layout,
            ICollection<BmsPreparedNoteDiagnostic> diagnostics,
            string code)
        {
            foreach (BmsManagedPackageNoteSlotKey slot in enumerateExactSlots(layout))
                diagnostics.Add(createDiagnostic(layout, slot, code, BmsPreparedNoteMaterialAuthority.SelectedDocument));
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

        private sealed record PlannedNoteComponent(
            NotePlan Plan,
            BmsGameplaySkinConfigurationCandidateSource Source);

        private sealed record PlannedNoteSelection(
            PlannedNoteComponent Component,
            int ProviderIndex);

        private sealed class NotePlanOwner : IBmsGameplaySkinLaneResourceComponentOwner<PlannedNoteComponent>
        {
            private readonly BmsLegacySkin source;
            private readonly IResourceStore<byte[]> files;
            private readonly BmsManagedPackageSourceRevision sourceRevision;
            private readonly BmsKeymode keymode;
            private readonly CancellationToken cancellationToken;
            private readonly List<PlannedNoteComponent> components = new List<PlannedNoteComponent>();
            private bool disposed;

            public NotePlanOwner(
                BmsLegacySkin source,
                IResourceStore<byte[]> files,
                BmsManagedPackageSourceRevision sourceRevision,
                BmsKeymode keymode,
                CancellationToken cancellationToken)
            {
                this.source = source;
                this.files = files;
                this.sourceRevision = sourceRevision;
                this.keymode = keymode;
                this.cancellationToken = cancellationToken;
            }

            public PlannedNoteComponent Materialize(BmsGameplaySkinLaneResourceReference reference)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                cancellationToken.ThrowIfCancellationRequested();

                NotePlan plan = createPlan(files, sourceRevision, reference.ResourceName, cancellationToken);

                if (ReferenceEquals(reference.Field, GameplaySkinLaneResourceFieldCatalog.LongNoteBody))
                {
                    GameplaySkinConfigurationDeclaration<float> widthDeclaration =
                        reference.Source == BmsGameplaySkinConfigurationCandidateSource.BmsRoleOverride
                            ? source.GetAcceptedBmsGeometry(BmsSkinConfigurationLookups.LongNoteBodyWidth, keymode)
                            : GameplaySkinConfigurationDeclaration<float>.Absent;
                    plan = plan with
                    {
                        LongNoteBodyWidth = BmsGameplaySkinScalarGeometryResolver.Resolve(
                            BmsSkinConfigurationLookups.LongNoteBodyWidth,
                            widthDeclaration),
                    };
                }

                var component = new PlannedNoteComponent(plan, reference.Source);
                components.Add(component);
                return component;
            }

            public void Dispose()
            {
                disposed = true;
                components.Clear();
            }
        }

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
    /// One final immutable Note/LN material. A committed renderer never re-runs source lookup or fallback selection.
    /// </summary>
    internal interface IBmsResolvedNoteMaterial
    {
        BmsNoteSkinElements Element { get; }

        int FrameCount { get; }

        Drawable CreateDrawable();
    }

    /// <summary>
    /// Immutable decoded note material and its component-local scalar geometry owned by one prepared package revision.
    /// </summary>
    internal sealed class BmsSourceBoundNoteMaterial : IBmsResolvedNoteMaterial
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
    /// Final programmatic fallback captured during prepare rather than chosen by a renderer after commit.
    /// </summary>
    internal sealed class BmsProgrammaticNoteMaterial : IBmsResolvedNoteMaterial
    {
        private readonly int laneIndex;
        private readonly bool isScratch;
        private readonly BmsKeymode keymode;

        public BmsNoteSkinElements Element { get; }

        public int FrameCount => 1;

        public BmsProgrammaticNoteMaterial(
            BmsNoteSkinElements element,
            int laneIndex,
            bool isScratch,
            BmsKeymode keymode)
        {
            if (element is not (BmsNoteSkinElements.Note
                or BmsNoteSkinElements.LongNoteHead
                or BmsNoteSkinElements.LongNoteBody
                or BmsNoteSkinElements.LongNoteTail))
            {
                throw new ArgumentOutOfRangeException(nameof(element), element, "Unsupported BMS programmatic note element.");
            }

            Element = element;
            this.laneIndex = laneIndex;
            this.isScratch = isScratch;
            this.keymode = keymode;
        }

        public Drawable CreateDrawable()
        {
            Color4 colour = Element switch
            {
                BmsNoteSkinElements.Note => BmsDefaultPlayfieldPalette.GetNote(laneIndex, isScratch, keymode),
                BmsNoteSkinElements.LongNoteHead or BmsNoteSkinElements.LongNoteBody =>
                    BmsDefaultPlayfieldPalette.GetLongNoteHead(laneIndex, isScratch, keymode),
                BmsNoteSkinElements.LongNoteTail => BmsDefaultPlayfieldPalette.GetLongNoteTail(laneIndex, isScratch, keymode),
                _ => throw new InvalidOperationException("Unknown prepared BMS programmatic note element."),
            };
            var visual = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = colour,
            };

            if (Element == BmsNoteSkinElements.LongNoteBody)
            {
                return new BmsSourceBoundLongNoteBodyDrawable(
                    visual,
                    BmsGameplaySkinScalarGeometryResolver.DEFAULT_LONG_NOTE_BODY_WIDTH,
                    colour);
            }

            var drawable = new BmsSourceBoundNoteDrawable(visual);

            if (Element == BmsNoteSkinElements.LongNoteTail)
                drawable.Alpha = 0;

            return drawable;
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
