// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Newtonsoft.Json;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The independently versioned contracts used to decode, catalogue and resolve one material set.
    /// </summary>
    public sealed class GameplaySkinMaterialContractIdentity : IEquatable<GameplaySkinMaterialContractIdentity>
    {
        public static GameplaySkinMaterialContractIdentity Current { get; } = new GameplaySkinMaterialContractIdentity(
            GameplaySkinSlotCatalog.CONTRACT_ID,
            GameplaySkinDocumentCodec.CONTRACT_ID,
            GameplaySkinSlotResolver.CONTRACT_ID);

        public static GameplaySkinMaterialContractIdentity CompatibilityEmpty { get; } =
            new GameplaySkinMaterialContractIdentity("compatibility.empty", "compatibility.empty", "compatibility.empty");

        public string CatalogVersion { get; }

        public string CodecVersion { get; }

        public string ResolverVersion { get; }

        public GameplaySkinMaterialContractIdentity(
            string catalogVersion,
            string codecVersion,
            string resolverVersion)
        {
            CatalogVersion = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(catalogVersion, nameof(catalogVersion));
            CodecVersion = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(codecVersion, nameof(codecVersion));
            ResolverVersion = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(resolverVersion, nameof(resolverVersion));
        }

        public bool Equals(GameplaySkinMaterialContractIdentity? other)
            => other != null
               && string.Equals(CatalogVersion, other.CatalogVersion, StringComparison.Ordinal)
               && string.Equals(CodecVersion, other.CodecVersion, StringComparison.Ordinal)
               && string.Equals(ResolverVersion, other.ResolverVersion, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is GameplaySkinMaterialContractIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(CatalogVersion, CodecVersion, ResolverVersion);

        public override string ToString() => $"{CatalogVersion}:{CodecVersion}:{ResolverVersion}";
    }

    /// <summary>
    /// The authority which supplied one final material result.
    /// </summary>
    public enum GameplaySkinResolvedMaterialSourceKind
    {
        ProgrammaticFallback = 0,
        CanonicalPackage = 1,
        RulesetResources = 2,
        SelectedPackage = 3,

        /// <summary>
        /// Existing read-only beatmap skin visuals prepared as a runtime compatibility layer. This is deliberately
        /// not a gameplay-document producer, sidecar, capture, reload or beatmap-local C4 authoring authority.
        /// </summary>
        LegacyBeatmapCompatibility = 4,
    }

    /// <summary>
    /// Path-free identity of the source which won resolution for one material entry.
    /// </summary>
    /// <remarks>
    /// Both tokens are deliberately restricted to an opaque ASCII alphabet. Display names, resource names, author
    /// content and filesystem paths are not accepted and therefore cannot bleed through diagnostics.
    /// </remarks>
    public sealed class GameplaySkinResolvedMaterialSourceIdentity : IEquatable<GameplaySkinResolvedMaterialSourceIdentity>
    {
        public GameplaySkinResolvedMaterialSourceKind Kind { get; }

        public string StableId { get; }

        public string ContentRevision { get; }

        private GameplaySkinResolvedMaterialSourceIdentity(
            GameplaySkinResolvedMaterialSourceKind kind,
            string stableId,
            string contentRevision)
        {
            if (!Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown gameplay skin material source authority.");

            Kind = kind;
            StableId = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(stableId, nameof(stableId));
            ContentRevision = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(contentRevision, nameof(contentRevision));
        }

        public static GameplaySkinResolvedMaterialSourceIdentity Create(
            GameplaySkinResolvedMaterialSourceKind kind,
            string stableId,
            string contentRevision)
            => new GameplaySkinResolvedMaterialSourceIdentity(kind, stableId, contentRevision);

        public bool Equals(GameplaySkinResolvedMaterialSourceIdentity? other)
            => other != null
               && Kind == other.Kind
               && string.Equals(StableId, other.StableId, StringComparison.Ordinal)
               && string.Equals(ContentRevision, other.ContentRevision, StringComparison.Ordinal);

        public override bool Equals(object? obj) => obj is GameplaySkinResolvedMaterialSourceIdentity other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind, StableId, ContentRevision);

        /// <summary>
        /// Deliberately omits stable source and content-revision tokens. Exact values remain available for in-memory
        /// equality and correlation, but must not bleed into logs or persistence-safe diagnostics.
        /// </summary>
        public override string ToString() => $"{Kind}:Bound";
    }

    public enum GameplaySkinResolvedMaterialTargetKind
    {
        Global = 0,
        Stage = 1,
        Group = 2,
        Lane = 3,
    }

    /// <summary>
    /// Exact topology coordinates targeted by one resolved material entry.
    /// </summary>
    /// <remarks>
    /// Lane targets retain stable lane/group IDs and every logical/visual index explicitly. They never derive identity
    /// from enum ordinals, geometry, lane count or drawable order. A material set revalidates these copied coordinates
    /// against its exact layout topology before publication.
    /// </remarks>
    public sealed class GameplaySkinResolvedMaterialTarget : IEquatable<GameplaySkinResolvedMaterialTarget>
    {
        public GameplaySkinResolvedMaterialTargetKind Kind { get; }

        public GameplaySkinLaneGroupId? GroupId { get; }

        public GameplaySkinLaneId? LaneId { get; }

        public int? GroupLogicalIndex { get; }

        public int? GroupVisualIndex { get; }

        public int? GlobalLogicalIndex { get; }

        public int? GlobalVisualIndex { get; }

        public int? GroupLocalLogicalIndex { get; }

        public int? GroupLocalVisualIndex { get; }

        public GameplaySkinSlotScope Scope => Kind switch
        {
            GameplaySkinResolvedMaterialTargetKind.Global => GameplaySkinSlotScope.Global,
            GameplaySkinResolvedMaterialTargetKind.Stage => GameplaySkinSlotScope.Stage,
            GameplaySkinResolvedMaterialTargetKind.Group => GameplaySkinSlotScope.Group,
            GameplaySkinResolvedMaterialTargetKind.Lane => GameplaySkinSlotScope.Lane,
            _ => GameplaySkinSlotScope.None,
        };

        private GameplaySkinResolvedMaterialTarget()
        {
            Kind = GameplaySkinResolvedMaterialTargetKind.Global;
        }

        private GameplaySkinResolvedMaterialTarget(
            GameplaySkinLaneTopologyGroup group,
            GameplaySkinResolvedMaterialTargetKind kind)
        {
            if (kind is not GameplaySkinResolvedMaterialTargetKind.Stage and not GameplaySkinResolvedMaterialTargetKind.Group)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "A non-lane topology target must be a stage or group.");

            Kind = kind;
            GroupId = group.Identity.Id;
            GroupLogicalIndex = group.LogicalIndex;
            GroupVisualIndex = group.VisualIndex;
        }

        private GameplaySkinResolvedMaterialTarget(
            GameplaySkinLaneTopologyGroup group,
            GameplaySkinLaneTopologyEntry lane)
        {
            if (!lane.Identity.Group.Equals(group.Identity))
                throw new ArgumentException("A lane material target must use its containing topology group.", nameof(group));

            Kind = GameplaySkinResolvedMaterialTargetKind.Lane;
            GroupId = group.Identity.Id;
            LaneId = lane.Identity.Id;
            GroupLogicalIndex = group.LogicalIndex;
            GroupVisualIndex = group.VisualIndex;
            GlobalLogicalIndex = lane.GlobalLogicalIndex;
            GlobalVisualIndex = lane.GlobalVisualIndex;
            GroupLocalLogicalIndex = lane.GroupLocalLogicalIndex;
            GroupLocalVisualIndex = lane.GroupLocalVisualIndex;
        }

        public static GameplaySkinResolvedMaterialTarget Global { get; } = new GameplaySkinResolvedMaterialTarget();

        public static GameplaySkinResolvedMaterialTarget ForGroup(GameplaySkinLaneTopologyGroup group)
        {
            ArgumentNullException.ThrowIfNull(group);
            return new GameplaySkinResolvedMaterialTarget(group, GameplaySkinResolvedMaterialTargetKind.Group);
        }

        public static GameplaySkinResolvedMaterialTarget ForStage(GameplaySkinLaneTopologyGroup group)
        {
            ArgumentNullException.ThrowIfNull(group);
            return new GameplaySkinResolvedMaterialTarget(group, GameplaySkinResolvedMaterialTargetKind.Stage);
        }

        public static GameplaySkinResolvedMaterialTarget ForLane(
            GameplaySkinLaneTopologyGroup group,
            GameplaySkinLaneTopologyEntry lane)
        {
            ArgumentNullException.ThrowIfNull(group);
            ArgumentNullException.ThrowIfNull(lane);
            return new GameplaySkinResolvedMaterialTarget(group, lane);
        }

        internal bool Matches(GameplaySkinLaneTopologySnapshot topology)
        {
            ArgumentNullException.ThrowIfNull(topology);

            switch (Kind)
            {
                case GameplaySkinResolvedMaterialTargetKind.Global:
                    return GroupId == null
                           && LaneId == null
                           && GroupLogicalIndex == null
                           && GroupVisualIndex == null
                           && GlobalLogicalIndex == null
                           && GlobalVisualIndex == null
                           && GroupLocalLogicalIndex == null
                           && GroupLocalVisualIndex == null;

                case GameplaySkinResolvedMaterialTargetKind.Stage:
                case GameplaySkinResolvedMaterialTargetKind.Group:
                    return GroupId != null
                           && LaneId == null
                           && topology.TryGetGroup(GroupId, out GameplaySkinLaneTopologyGroup? group)
                           && group != null
                           && group.LogicalIndex == GroupLogicalIndex
                           && group.VisualIndex == GroupVisualIndex
                           && GlobalLogicalIndex == null
                           && GlobalVisualIndex == null
                           && GroupLocalLogicalIndex == null
                           && GroupLocalVisualIndex == null;

                case GameplaySkinResolvedMaterialTargetKind.Lane:
                    if (GroupId == null
                        || LaneId == null
                        || !topology.TryGetGroup(GroupId, out GameplaySkinLaneTopologyGroup? laneGroup)
                        || !topology.TryGetLane(LaneId, out GameplaySkinLaneTopologyEntry? lane))
                    {
                        return false;
                    }

                    return laneGroup != null
                           && lane != null
                           && lane.Identity.Group.Id.Equals(GroupId)
                           && laneGroup.LogicalIndex == GroupLogicalIndex
                           && laneGroup.VisualIndex == GroupVisualIndex
                           && lane.GlobalLogicalIndex == GlobalLogicalIndex
                           && lane.GlobalVisualIndex == GlobalVisualIndex
                           && lane.GroupLocalLogicalIndex == GroupLocalLogicalIndex
                           && lane.GroupLocalVisualIndex == GroupLocalVisualIndex;

                default:
                    return false;
            }
        }

        public bool Equals(GameplaySkinResolvedMaterialTarget? other)
            => other != null
               && Kind == other.Kind
               && EqualityComparer<GameplaySkinLaneGroupId?>.Default.Equals(GroupId, other.GroupId)
               && EqualityComparer<GameplaySkinLaneId?>.Default.Equals(LaneId, other.LaneId)
               && GroupLogicalIndex == other.GroupLogicalIndex
               && GroupVisualIndex == other.GroupVisualIndex
               && GlobalLogicalIndex == other.GlobalLogicalIndex
               && GlobalVisualIndex == other.GlobalVisualIndex
               && GroupLocalLogicalIndex == other.GroupLocalLogicalIndex
               && GroupLocalVisualIndex == other.GroupLocalVisualIndex;

        public override bool Equals(object? obj) => obj is GameplaySkinResolvedMaterialTarget other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Kind);
            hash.Add(GroupId);
            hash.Add(LaneId);
            hash.Add(GroupLogicalIndex);
            hash.Add(GroupVisualIndex);
            hash.Add(GlobalLogicalIndex);
            hash.Add(GlobalVisualIndex);
            hash.Add(GroupLocalLogicalIndex);
            hash.Add(GroupLocalVisualIndex);
            return hash.ToHashCode();
        }

        public override string ToString() => Kind switch
        {
            GameplaySkinResolvedMaterialTargetKind.Global => "global",
            GameplaySkinResolvedMaterialTargetKind.Stage => $"stage:{GroupId}",
            GameplaySkinResolvedMaterialTargetKind.Group => $"group:{GroupId}",
            GameplaySkinResolvedMaterialTargetKind.Lane => $"lane:{LaneId}",
            _ => "invalid",
        };
    }

    /// <summary>
    /// Stable catalog slot plus explicit topology target.
    /// </summary>
    public sealed class GameplaySkinResolvedMaterialKey : IEquatable<GameplaySkinResolvedMaterialKey>
    {
        public GameplaySkinSlotDescriptor Slot { get; }

        public GameplaySkinResolvedMaterialTarget Target { get; }

        public GameplaySkinResolvedMaterialKey(
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target)
        {
            Slot = slot ?? throw new ArgumentNullException(nameof(slot));
            Target = target ?? throw new ArgumentNullException(nameof(target));

            if ((slot.AllowedScopes & target.Scope) == 0)
                throw new ArgumentException("A resolved gameplay skin material target must use a scope allowed by its catalog slot.", nameof(target));
        }

        public bool Equals(GameplaySkinResolvedMaterialKey? other)
            => other != null
               && string.Equals(Slot.Id, other.Slot.Id, StringComparison.Ordinal)
               && Target.Equals(other.Target);

        public override bool Equals(object? obj) => obj is GameplaySkinResolvedMaterialKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(StringComparer.Ordinal.GetHashCode(Slot.Id), Target);

        public override string ToString() => $"{Slot.Id}:{Target}";
    }

    public enum GameplaySkinResolvedMaterialState
    {
        Provide = 0,
        Suppress = 1,
    }

    /// <summary>
    /// One final, explicit Provide or Suppress result produced by the shared resolver.
    /// </summary>
    /// <remarks>
    /// A Provide entry always carries a non-null prepared material payload and its declared value type. The material is
    /// borrowed from the exact revision/root resource owner; this immutable set does not introduce a second disposal or
    /// retirement authority. A Suppress entry cannot carry a payload.
    /// </remarks>
    public sealed class GameplaySkinResolvedMaterialEntry
    {
        private readonly object? material;

        public GameplaySkinResolvedMaterialKey Key { get; }

        public GameplaySkinSlotDescriptor Slot => Key.Slot;

        public GameplaySkinResolvedMaterialTarget Target => Key.Target;

        public GameplaySkinResolvedMaterialState State { get; }

        public GameplaySkinResolvedMaterialSourceIdentity Source { get; }

        public Type? DeclaredValueType { get; }

        public Type? RuntimeValueType => material?.GetType();

        public object Material => State == GameplaySkinResolvedMaterialState.Provide
            ? material!
            : throw new InvalidOperationException("A suppressed gameplay skin material entry has no payload.");

        private GameplaySkinResolvedMaterialEntry(
            GameplaySkinResolvedMaterialKey key,
            GameplaySkinResolvedMaterialState state,
            GameplaySkinResolvedMaterialSourceIdentity source,
            Type? declaredValueType,
            object? material)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Source = source ?? throw new ArgumentNullException(nameof(source));

            if (state is not GameplaySkinResolvedMaterialState.Provide and not GameplaySkinResolvedMaterialState.Suppress)
                throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown resolved gameplay skin material state.");

            if (state == GameplaySkinResolvedMaterialState.Provide)
            {
                ArgumentNullException.ThrowIfNull(declaredValueType);
                ArgumentNullException.ThrowIfNull(material);

                if (!declaredValueType.IsInstanceOfType(material))
                    throw new ArgumentException("A provided gameplay skin material must match its declared value type.", nameof(material));
            }
            else if (declaredValueType != null || material != null)
                throw new ArgumentException("A suppressed gameplay skin material cannot carry a payload.", nameof(material));

            State = state;
            DeclaredValueType = declaredValueType;
            this.material = material;
        }

        public static GameplaySkinResolvedMaterialEntry Provide<TMaterial>(
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target,
            GameplaySkinResolvedMaterialSourceIdentity source,
            TMaterial material)
            where TMaterial : notnull
            => Provide(
                slot,
                target,
                source,
                typeof(TMaterial),
                material);

        public static GameplaySkinResolvedMaterialEntry Provide(
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target,
            GameplaySkinResolvedMaterialSourceIdentity source,
            Type declaredValueType,
            object material)
            => new GameplaySkinResolvedMaterialEntry(
                new GameplaySkinResolvedMaterialKey(slot, target),
                GameplaySkinResolvedMaterialState.Provide,
                source,
                declaredValueType,
                material);

        public static GameplaySkinResolvedMaterialEntry Suppress(
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target,
            GameplaySkinResolvedMaterialSourceIdentity source)
        {
            ArgumentNullException.ThrowIfNull(slot);

            if (slot.SuppressEligibility != GameplaySkinSlotSuppressEligibility.Allowed)
                throw new ArgumentException("A catalog-required gameplay skin slot cannot be suppressed.", nameof(slot));

            return new GameplaySkinResolvedMaterialEntry(
                new GameplaySkinResolvedMaterialKey(slot, target),
                GameplaySkinResolvedMaterialState.Suppress,
                source,
                null,
                null);
        }

        public bool TryGetMaterial<TMaterial>([MaybeNullWhen(false)] out TMaterial material)
            where TMaterial : notnull
        {
            if (State == GameplaySkinResolvedMaterialState.Provide && this.material is TMaterial typed)
            {
                material = typed;
                return true;
            }

            material = default;
            return false;
        }

        public TMaterial GetMaterial<TMaterial>()
            where TMaterial : notnull
        {
            if (State == GameplaySkinResolvedMaterialState.Provide && material is TMaterial typed)
                return typed;

            throw new InvalidOperationException($"The resolved gameplay skin material is not a provided {typeof(TMaterial).Name} payload.");
        }
    }

    /// <summary>
    /// Stable, path-free diagnostic emitted while constructing one resolved material set.
    /// </summary>
    public sealed class GameplaySkinResolvedMaterialDiagnostic
    {
        public string Code { get; }

        public GameplaySkinResolvedMaterialKey? Key { get; }

        /// <summary>
        /// Stable public catalog ID when this diagnostic concerns a known slot, including document diagnostics whose
        /// malformed author target cannot truthfully be represented by a resolved material key.
        /// </summary>
        public string? CatalogSlotId { get; }

        [JsonIgnore]
        public GameplaySkinResolvedMaterialSourceIdentity? Source { get; }

        /// <summary>
        /// Persistence-safe source authority which does not expose a record ID, content revision or author value.
        /// </summary>
        public GameplaySkinResolvedMaterialSourceKind? SourceKind => Source?.Kind;

        public GameplaySkinResolvedMaterialDiagnostic(
            string code,
            GameplaySkinResolvedMaterialKey key,
            GameplaySkinResolvedMaterialSourceIdentity? source = null)
        {
            Code = GameplaySkinMaterialTokenValidation.ValidateDiagnosticCode(code, nameof(code));
            Key = key ?? throw new ArgumentNullException(nameof(key));
            CatalogSlotId = key.Slot.Id;
            Source = source;
        }

        private GameplaySkinResolvedMaterialDiagnostic(
            string code,
            GameplaySkinResolvedMaterialSourceIdentity? source,
            GameplaySkinSlotDescriptor? catalogSlot)
        {
            Code = GameplaySkinMaterialTokenValidation.ValidateDiagnosticCode(code, nameof(code));
            CatalogSlotId = catalogSlot?.Id;
            Source = source;
        }

        public static GameplaySkinResolvedMaterialDiagnostic ForDocument(
            string code,
            GameplaySkinResolvedMaterialSourceIdentity? source = null,
            GameplaySkinSlotDescriptor? catalogSlot = null)
            => new GameplaySkinResolvedMaterialDiagnostic(code, source, catalogSlot);

        public override string ToString()
        {
            string subject = Key?.ToString() ?? CatalogSlotId ?? "document";
            return Source == null ? $"{Code}:{subject}" : $"{Code}:{subject}:{Source.Kind}";
        }

        /// <summary>
        /// Formats the stable, persistence-safe author diagnostic emitted by the production publication sink.
        /// </summary>
        /// <remarks>
        /// Exact source/content identities remain available through <see cref="Source"/> for in-memory correlation,
        /// but this text deliberately contains only the public code, catalog ID, stable topology coordinates, source
        /// authority kind and versioned material contracts. It never includes paths, author values, record IDs,
        /// content hashes or exception text.
        /// </remarks>
        public string ToPersistenceSafeString(GameplaySkinMaterialContractIdentity contractIdentity)
        {
            ArgumentNullException.ThrowIfNull(contractIdentity);

            return $"code={Code}; slot={CatalogSlotId ?? "document"}; target={formatTarget(Key?.Target)}; "
                   + $"source={SourceKind?.ToString() ?? "None"}; catalog={contractIdentity.CatalogVersion}; "
                   + $"codec={contractIdentity.CodecVersion}; resolver={contractIdentity.ResolverVersion}";
        }

        private static string formatTarget(GameplaySkinResolvedMaterialTarget? target)
        {
            if (target == null)
                return "document";

            return target.Kind switch
            {
                GameplaySkinResolvedMaterialTargetKind.Global => "global",
                GameplaySkinResolvedMaterialTargetKind.Stage or GameplaySkinResolvedMaterialTargetKind.Group =>
                    $"{target.Kind.ToString().ToLowerInvariant()}:{target.GroupId}:gl={target.GroupLogicalIndex}:gv={target.GroupVisualIndex}",
                GameplaySkinResolvedMaterialTargetKind.Lane =>
                    $"lane:{target.GroupId}:{target.LaneId}:gl={target.GroupLogicalIndex}:gv={target.GroupVisualIndex}:"
                    + $"l={target.GlobalLogicalIndex}:v={target.GlobalVisualIndex}:gll={target.GroupLocalLogicalIndex}:glv={target.GroupLocalVisualIndex}",
                _ => "invalid",
            };
        }
    }

    /// <summary>
    /// The one immutable, completely resolved material result paired with an exact package and layout snapshot.
    /// </summary>
    public sealed class GameplaySkinResolvedMaterialSet
    {
        private readonly IReadOnlyDictionary<GameplaySkinResolvedMaterialKey, GameplaySkinResolvedMaterialEntry> entriesByKey;

        public GameplaySkinLayoutSnapshot Snapshot { get; }

        public GameplaySkinPackageRevision PackageRevision => Snapshot.Context.PackageRevision;

        public long LayoutRevision => Snapshot.Context.LayoutRevision;

        public GameplaySkinMaterialContractIdentity ContractIdentity { get; }

        public IReadOnlyList<GameplaySkinResolvedMaterialEntry> Entries { get; }

        public IReadOnlyList<GameplaySkinResolvedMaterialDiagnostic> Diagnostics { get; }

        /// <summary>
        /// Stable, deduplicated and persistence-safe diagnostic strings prepared with this immutable material set.
        /// </summary>
        /// <remarks>
        /// Product log observers consume only this text surface, so an asynchronous listener can never extend the
        /// lifetime of package-owned textures or resolved material objects.
        /// </remarks>
        public IReadOnlyList<string> PersistenceSafeDiagnostics { get; }

        /// <summary>
        /// Complete product-log payload prepared before publication, or <see langword="null"/> when there are no diagnostics.
        /// </summary>
        public string? PersistenceSafeDiagnosticBatch { get; }

        public bool IsEmpty => Entries.Count == 0;

        private GameplaySkinResolvedMaterialSet(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinMaterialContractIdentity contractIdentity,
            IEnumerable<GameplaySkinResolvedMaterialEntry> entries,
            IEnumerable<GameplaySkinResolvedMaterialDiagnostic> diagnostics)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            ContractIdentity = contractIdentity ?? throw new ArgumentNullException(nameof(contractIdentity));
            ArgumentNullException.ThrowIfNull(entries);
            ArgumentNullException.ThrowIfNull(diagnostics);

            GameplaySkinResolvedMaterialEntry[] copiedEntries = entries.ToArray();
            GameplaySkinResolvedMaterialDiagnostic[] copiedDiagnostics = diagnostics.ToArray();

            if (contractIdentity.Equals(GameplaySkinMaterialContractIdentity.CompatibilityEmpty)
                && (copiedEntries.Length != 0 || copiedDiagnostics.Length != 0))
            {
                throw new ArgumentException(
                    "The compatibility material contract can only represent an empty, unmigrated gameplay surface.",
                    nameof(contractIdentity));
            }

            if (copiedEntries.Any(entry => entry == null))
                throw new ArgumentException("A resolved gameplay skin material set cannot contain null entries.", nameof(entries));

            if (copiedDiagnostics.Any(diagnostic => diagnostic == null))
                throw new ArgumentException("A resolved gameplay skin material set cannot contain null diagnostics.", nameof(diagnostics));

            foreach (GameplaySkinResolvedMaterialEntry entry in copiedEntries)
            {
                validateTarget(entry.Target, nameof(entries));

                if (GameplaySkinSlotApplicabilityValidator.Validate(entry.Slot, Snapshot, entry.Target)
                    != GameplaySkinSlotApplicabilityResult.Applicable)
                {
                    throw new ArgumentException(
                        "A resolved gameplay skin material entry must satisfy its public catalog applicability contract.",
                        nameof(entries));
                }
            }

            foreach (GameplaySkinResolvedMaterialDiagnostic diagnostic in copiedDiagnostics)
            {
                if (diagnostic.Key != null)
                    validateTarget(diagnostic.Key.Target, nameof(diagnostics));
            }

            Dictionary<GameplaySkinResolvedMaterialKey, GameplaySkinResolvedMaterialEntry> indexed;

            try
            {
                indexed = copiedEntries.ToDictionary(entry => entry.Key);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException("A resolved gameplay skin material set cannot contain duplicate slot targets.", nameof(entries), exception);
            }

            Entries = Array.AsReadOnly(copiedEntries);
            Diagnostics = Array.AsReadOnly(copiedDiagnostics);
            string[] persistenceSafeDiagnostics = copiedDiagnostics
                                                  .Select(diagnostic => diagnostic.ToPersistenceSafeString(contractIdentity))
                                                  .Distinct(StringComparer.Ordinal)
                                                  .OrderBy(message => message, StringComparer.Ordinal)
                                                  .ToArray();
            PersistenceSafeDiagnostics = Array.AsReadOnly(persistenceSafeDiagnostics);
            PersistenceSafeDiagnosticBatch = persistenceSafeDiagnostics.Length == 0
                ? null
                : $"Gameplay skin material diagnostic: count={persistenceSafeDiagnostics.Length}\n{string.Join('\n', persistenceSafeDiagnostics)}";
            entriesByKey = new ReadOnlyDictionary<GameplaySkinResolvedMaterialKey, GameplaySkinResolvedMaterialEntry>(indexed);
        }

        public static GameplaySkinResolvedMaterialSet Create(
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinMaterialContractIdentity contractIdentity,
            IEnumerable<GameplaySkinResolvedMaterialEntry> entries,
            IEnumerable<GameplaySkinResolvedMaterialDiagnostic>? diagnostics = null)
            => new GameplaySkinResolvedMaterialSet(
                snapshot,
                contractIdentity,
                entries,
                diagnostics ?? Array.Empty<GameplaySkinResolvedMaterialDiagnostic>());

        public static GameplaySkinResolvedMaterialSet CreateEmpty(GameplaySkinLayoutSnapshot snapshot)
            => Create(
                snapshot,
                GameplaySkinMaterialContractIdentity.CompatibilityEmpty,
                Array.Empty<GameplaySkinResolvedMaterialEntry>());

        public bool TryGet(
            GameplaySkinResolvedMaterialKey key,
            [NotNullWhen(true)] out GameplaySkinResolvedMaterialEntry? entry)
        {
            ArgumentNullException.ThrowIfNull(key);
            return entriesByKey.TryGetValue(key, out entry);
        }

        public bool TryGetMaterial<TMaterial>(
            GameplaySkinSlotDescriptor slot,
            GameplaySkinResolvedMaterialTarget target,
            [MaybeNullWhen(false)] out TMaterial material)
            where TMaterial : notnull
        {
            var key = new GameplaySkinResolvedMaterialKey(slot, target);

            if (TryGet(key, out GameplaySkinResolvedMaterialEntry? entry))
                return entry.TryGetMaterial(out material);

            material = default;
            return false;
        }

        private void validateTarget(GameplaySkinResolvedMaterialTarget target, string parameterName)
        {
            if (!target.Matches(Snapshot.Context.Topology))
                throw new ArgumentException("A resolved gameplay skin material target must match the exact layout topology.", parameterName);
        }
    }

    internal static class GameplaySkinMaterialTokenValidation
    {
        private const int maximum_token_length = 160;

        internal static string ValidateOpaqueToken(string token, string parameterName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(token, parameterName);

            if (token.Length > maximum_token_length
                || token.StartsWith('.')
                || token.EndsWith('.')
                || token.Contains("..", StringComparison.Ordinal)
                || token.Any(character => !isAsciiLetterOrDigit(character)
                                           && character is not '.' and not '_' and not '-'))
            {
                throw new ArgumentException(
                    "Gameplay skin material identity tokens must use only path-free ASCII letters, digits, dot, underscore or hyphen.",
                    parameterName);
            }

            return token;
        }

        internal static string ValidateDiagnosticCode(string code, string parameterName)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code, parameterName);

            bool isInternalCode = code.All(character => character is >= 'a' and <= 'z'
                                                        or >= '0' and <= '9'
                                                        or '.' or '-');
            bool isPublicCode = false;

            // Public IDs also contain decimal suffixes and separators. Keep this separate from the lowercase
            // implementation-code grammar so arbitrary mixed-case aliases cannot silently become stable author IDs.
            if (code.StartsWith("OMS-SKIN-", StringComparison.Ordinal))
            {
                isPublicCode = code.AsSpan("OMS-SKIN-".Length)
                                   .ToArray()
                                   .All(character => character is >= 'A' and <= 'Z'
                                                      or >= '0' and <= '9'
                                                      or '-');
            }

            if (code.Length > maximum_token_length
                || code.StartsWith('.')
                || code.EndsWith('.')
                || code.Contains("..", StringComparison.Ordinal)
                || !isInternalCode && !isPublicCode)
            {
                throw new ArgumentException(
                    "Gameplay skin material diagnostic codes must be lowercase implementation codes or canonical OMS-SKIN public IDs.",
                    parameterName);
            }

            return code;
        }

        private static bool isAsciiLetterOrDigit(char character)
            => character is >= 'a' and <= 'z'
               or >= 'A' and <= 'Z'
               or >= '0' and <= '9';
    }
}
