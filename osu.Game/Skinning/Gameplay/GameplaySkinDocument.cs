// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// A skin instance which exposes its one immutable shared-codec result to ruleset adapters.
    /// </summary>
    /// <remarks>
    /// This interface exposes no package stream or filesystem path. A ruleset must bind the retained document to its
    /// exact C2/C3 publication before it can use any author declaration.
    /// </remarks>
    public interface IGameplaySkinDocumentSource
    {
        GameplaySkinDocument GameplaySkinDocument { get; }

        bool AllowsGameplaySkinDocumentAuthoring { get; }
    }

    public enum GameplaySkinDocumentTargetKind
    {
        Global = 0,
        Stage = 1,
        Group = 2,
        Lane = 3,
    }

    public enum GameplaySkinDocumentRulesetSelector
    {
        Any = 0,
        Mania = 1,
        Bms = 2,
    }

    public enum GameplaySkinDocumentStageModeSelector
    {
        Any = 0,
        Single = 1,
        Dual = 2,
    }

    /// <summary>
    /// Exact C3 topology coordinates targeted by an author declaration.
    /// Stage and group targets both retain the stable GroupId plus group logical/visual indices; no bare stage ordinal exists.
    /// </summary>
    public sealed class GameplaySkinDocumentTarget : IEquatable<GameplaySkinDocumentTarget>
    {
        public const string ANY_KEYMODE = "any";

        public GameplaySkinDocumentTargetKind Kind { get; }

        public GameplaySkinDocumentRulesetSelector RulesetSelector { get; }

        public string KeymodeSelector { get; }

        public GameplaySkinDocumentStageModeSelector StageModeSelector { get; }

        public GameplaySkinLaneGroupId? GroupId { get; }

        public GameplaySkinLaneId? LaneId { get; }

        public int? GroupLogicalIndex { get; }

        public int? GroupVisualIndex { get; }

        public int? GlobalLogicalIndex { get; }

        public int? GlobalVisualIndex { get; }

        public int? GroupLocalLogicalIndex { get; }

        public int? GroupLocalVisualIndex { get; }

        private GameplaySkinDocumentTarget(
            GameplaySkinDocumentTargetKind kind,
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector)
        {
            Kind = kind;
            validateSelectors(rulesetSelector, keymodeSelector, stageModeSelector);
            RulesetSelector = rulesetSelector;
            KeymodeSelector = keymodeSelector;
            StageModeSelector = stageModeSelector;
        }

        private GameplaySkinDocumentTarget(
            GameplaySkinDocumentTargetKind kind,
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector,
            GameplaySkinLaneGroupId groupId,
            GameplaySkinLaneId? laneId,
            int groupLogicalIndex,
            int groupVisualIndex,
            int? globalLogicalIndex,
            int? globalVisualIndex,
            int? groupLocalLogicalIndex,
            int? groupLocalVisualIndex)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(groupLogicalIndex);
            ArgumentOutOfRangeException.ThrowIfNegative(groupVisualIndex);

            if (kind == GameplaySkinDocumentTargetKind.Lane)
            {
                ArgumentNullException.ThrowIfNull(laneId);
                ArgumentOutOfRangeException.ThrowIfNegative(globalLogicalIndex!.Value);
                ArgumentOutOfRangeException.ThrowIfNegative(globalVisualIndex!.Value);
                ArgumentOutOfRangeException.ThrowIfNegative(groupLocalLogicalIndex!.Value);
                ArgumentOutOfRangeException.ThrowIfNegative(groupLocalVisualIndex!.Value);
            }

            Kind = kind;
            validateSelectors(rulesetSelector, keymodeSelector, stageModeSelector);
            RulesetSelector = rulesetSelector;
            KeymodeSelector = keymodeSelector;
            StageModeSelector = stageModeSelector;
            GroupId = groupId;
            LaneId = laneId;
            GroupLogicalIndex = groupLogicalIndex;
            GroupVisualIndex = groupVisualIndex;
            GlobalLogicalIndex = globalLogicalIndex;
            GlobalVisualIndex = globalVisualIndex;
            GroupLocalLogicalIndex = groupLocalLogicalIndex;
            GroupLocalVisualIndex = groupLocalVisualIndex;
        }

        public static GameplaySkinDocumentTarget Global { get; } = new GameplaySkinDocumentTarget(
            GameplaySkinDocumentTargetKind.Global,
            GameplaySkinDocumentRulesetSelector.Any,
            ANY_KEYMODE,
            GameplaySkinDocumentStageModeSelector.Any);

        public static GameplaySkinDocumentTarget ForGlobal(
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector)
            => new GameplaySkinDocumentTarget(
                GameplaySkinDocumentTargetKind.Global,
                rulesetSelector,
                keymodeSelector,
                stageModeSelector);

        public static GameplaySkinDocumentTarget ForStage(GameplaySkinLaneGroupId groupId, int groupLogicalIndex, int groupVisualIndex)
            => ForStage(
                GameplaySkinDocumentRulesetSelector.Any,
                ANY_KEYMODE,
                GameplaySkinDocumentStageModeSelector.Any,
                groupId,
                groupLogicalIndex,
                groupVisualIndex);

        public static GameplaySkinDocumentTarget ForStage(
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector,
            GameplaySkinLaneGroupId groupId,
            int groupLogicalIndex,
            int groupVisualIndex)
            => forGroupLike(
                GameplaySkinDocumentTargetKind.Stage,
                rulesetSelector,
                keymodeSelector,
                stageModeSelector,
                groupId,
                groupLogicalIndex,
                groupVisualIndex);

        public static GameplaySkinDocumentTarget ForGroup(GameplaySkinLaneGroupId groupId, int groupLogicalIndex, int groupVisualIndex)
            => ForGroup(
                GameplaySkinDocumentRulesetSelector.Any,
                ANY_KEYMODE,
                GameplaySkinDocumentStageModeSelector.Any,
                groupId,
                groupLogicalIndex,
                groupVisualIndex);

        public static GameplaySkinDocumentTarget ForGroup(
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector,
            GameplaySkinLaneGroupId groupId,
            int groupLogicalIndex,
            int groupVisualIndex)
            => forGroupLike(
                GameplaySkinDocumentTargetKind.Group,
                rulesetSelector,
                keymodeSelector,
                stageModeSelector,
                groupId,
                groupLogicalIndex,
                groupVisualIndex);

        public static GameplaySkinDocumentTarget ForLane(
            GameplaySkinLaneGroupId groupId,
            GameplaySkinLaneId laneId,
            int groupLogicalIndex,
            int groupVisualIndex,
            int globalLogicalIndex,
            int globalVisualIndex,
            int groupLocalLogicalIndex,
            int groupLocalVisualIndex)
            => ForLane(
                GameplaySkinDocumentRulesetSelector.Any,
                ANY_KEYMODE,
                GameplaySkinDocumentStageModeSelector.Any,
                groupId,
                laneId,
                groupLogicalIndex,
                groupVisualIndex,
                globalLogicalIndex,
                globalVisualIndex,
                groupLocalLogicalIndex,
                groupLocalVisualIndex);

        public static GameplaySkinDocumentTarget ForLane(
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector,
            GameplaySkinLaneGroupId groupId,
            GameplaySkinLaneId laneId,
            int groupLogicalIndex,
            int groupVisualIndex,
            int globalLogicalIndex,
            int globalVisualIndex,
            int groupLocalLogicalIndex,
            int groupLocalVisualIndex)
        {
            ArgumentNullException.ThrowIfNull(groupId);
            ArgumentNullException.ThrowIfNull(laneId);

            return new GameplaySkinDocumentTarget(
                GameplaySkinDocumentTargetKind.Lane,
                rulesetSelector,
                keymodeSelector,
                stageModeSelector,
                groupId,
                laneId,
                groupLogicalIndex,
                groupVisualIndex,
                globalLogicalIndex,
                globalVisualIndex,
                groupLocalLogicalIndex,
                groupLocalVisualIndex);
        }

        private static GameplaySkinDocumentTarget forGroupLike(
            GameplaySkinDocumentTargetKind kind,
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector,
            GameplaySkinLaneGroupId groupId,
            int groupLogicalIndex,
            int groupVisualIndex)
        {
            ArgumentNullException.ThrowIfNull(groupId);
            return new GameplaySkinDocumentTarget(
                kind,
                rulesetSelector,
                keymodeSelector,
                stageModeSelector,
                groupId,
                null,
                groupLogicalIndex,
                groupVisualIndex,
                null,
                null,
                null,
                null);
        }

        private static void validateSelectors(
            GameplaySkinDocumentRulesetSelector rulesetSelector,
            string keymodeSelector,
            GameplaySkinDocumentStageModeSelector stageModeSelector)
        {
            if (!Enum.IsDefined(rulesetSelector))
                throw new ArgumentOutOfRangeException(nameof(rulesetSelector));

            if (!Enum.IsDefined(stageModeSelector))
                throw new ArgumentOutOfRangeException(nameof(stageModeSelector));

            ArgumentException.ThrowIfNullOrEmpty(keymodeSelector);

            if (keymodeSelector.Length > 80
                || keymodeSelector.Any(character => character is not (>= 'a' and <= 'z')
                                                     and not (>= '0' and <= '9')
                                                     and not '.' and not '-'))
            {
                throw new ArgumentException("A gameplay skin keymode selector must be a short lowercase ASCII token.", nameof(keymodeSelector));
            }
        }

        public bool Equals(GameplaySkinDocumentTarget? other)
            => other != null
               && Kind == other.Kind
               && RulesetSelector == other.RulesetSelector
               && string.Equals(KeymodeSelector, other.KeymodeSelector, StringComparison.Ordinal)
               && StageModeSelector == other.StageModeSelector
               && EqualityComparer<GameplaySkinLaneGroupId?>.Default.Equals(GroupId, other.GroupId)
               && EqualityComparer<GameplaySkinLaneId?>.Default.Equals(LaneId, other.LaneId)
               && GroupLogicalIndex == other.GroupLogicalIndex
               && GroupVisualIndex == other.GroupVisualIndex
               && GlobalLogicalIndex == other.GlobalLogicalIndex
               && GlobalVisualIndex == other.GlobalVisualIndex
               && GroupLocalLogicalIndex == other.GroupLocalLogicalIndex
               && GroupLocalVisualIndex == other.GroupLocalVisualIndex;

        public override bool Equals(object? obj) => obj is GameplaySkinDocumentTarget other && Equals(other);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Kind);
            hash.Add(RulesetSelector);
            hash.Add(KeymodeSelector, StringComparer.Ordinal);
            hash.Add(StageModeSelector);
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

        public bool Matches(GameplaySkinResolvedMaterialTarget target)
        {
            ArgumentNullException.ThrowIfNull(target);

            GameplaySkinDocumentTargetKind targetKind = target.Kind switch
            {
                GameplaySkinResolvedMaterialTargetKind.Global => GameplaySkinDocumentTargetKind.Global,
                GameplaySkinResolvedMaterialTargetKind.Stage => GameplaySkinDocumentTargetKind.Stage,
                GameplaySkinResolvedMaterialTargetKind.Group => GameplaySkinDocumentTargetKind.Group,
                GameplaySkinResolvedMaterialTargetKind.Lane => GameplaySkinDocumentTargetKind.Lane,
                _ => (GameplaySkinDocumentTargetKind)(-1),
            };

            return Kind == targetKind
                   && EqualityComparer<GameplaySkinLaneGroupId?>.Default.Equals(GroupId, target.GroupId)
                   && EqualityComparer<GameplaySkinLaneId?>.Default.Equals(LaneId, target.LaneId)
                   && GroupLogicalIndex == target.GroupLogicalIndex
                   && GroupVisualIndex == target.GroupVisualIndex
                   && GlobalLogicalIndex == target.GlobalLogicalIndex
                   && GlobalVisualIndex == target.GlobalVisualIndex
                   && GroupLocalLogicalIndex == target.GroupLocalLogicalIndex
                   && GroupLocalVisualIndex == target.GroupLocalVisualIndex;
        }

        public bool Matches(GameplaySkinLayoutSnapshot snapshot, GameplaySkinResolvedMaterialTarget target)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(target);

            if (!GameplaySkinSlotApplicabilityValidator.IsSelectorApplicable(this, snapshot))
                return false;

            if (Kind == GameplaySkinDocumentTargetKind.Global)
                return true;

            if (GroupId == null
                || target.GroupId == null
                || !GroupId.Equals(target.GroupId)
                || GroupLogicalIndex != target.GroupLogicalIndex
                || GroupVisualIndex != target.GroupVisualIndex)
            {
                return false;
            }

            if (Kind == GameplaySkinDocumentTargetKind.Stage)
                return target.Kind is GameplaySkinResolvedMaterialTargetKind.Stage
                    or GameplaySkinResolvedMaterialTargetKind.Group
                    or GameplaySkinResolvedMaterialTargetKind.Lane;

            if (Kind == GameplaySkinDocumentTargetKind.Group)
                return target.Kind is GameplaySkinResolvedMaterialTargetKind.Group
                    or GameplaySkinResolvedMaterialTargetKind.Lane;

            return target.Kind == GameplaySkinResolvedMaterialTargetKind.Lane
                   && LaneId != null
                   && LaneId.Equals(target.LaneId)
                   && GlobalLogicalIndex == target.GlobalLogicalIndex
                   && GlobalVisualIndex == target.GlobalVisualIndex
                   && GroupLocalLogicalIndex == target.GroupLocalLogicalIndex
                   && GroupLocalVisualIndex == target.GroupLocalVisualIndex;
        }

        internal int ScopeSpecificity => Kind switch
        {
            GameplaySkinDocumentTargetKind.Global => 0,
            GameplaySkinDocumentTargetKind.Stage => 1,
            GameplaySkinDocumentTargetKind.Group => 2,
            GameplaySkinDocumentTargetKind.Lane => 3,
            _ => -1,
        };

        public override string ToString() => Kind.ToString();
    }

    public enum GameplaySkinDocumentDeclarationPresence
    {
        Absent = 0,
        Declared = 1,
    }

    public enum GameplaySkinDocumentValueValidity
    {
        None = 0,
        Empty = 1,
        Invalid = 2,
        Valid = 3,
    }

    public enum GameplaySkinDocumentOperation
    {
        None = 0,
        Provide = 1,
        Inherit = 2,
        Suppress = 3,
    }

    public sealed class GameplaySkinDocumentEntry
    {
        public GameplaySkinDocumentDeclarationPresence Presence { get; }

        public GameplaySkinDocumentValueValidity Validity { get; }

        public GameplaySkinDocumentOperation Operation { get; }

        public GameplaySkinSlotDescriptor? Descriptor { get; }

        public string? DeclaredSlotId { get; }

        public GameplaySkinSlotValueType? DeclaredValueType { get; }

        public GameplaySkinDocumentTarget Target { get; }

        public string? Value { get; }

        public int LineNumber { get; }

        internal GameplaySkinDocumentEntry(
            GameplaySkinDocumentDeclarationPresence presence,
            GameplaySkinDocumentValueValidity validity,
            GameplaySkinDocumentOperation operation,
            GameplaySkinSlotDescriptor? descriptor,
            string? declaredSlotId,
            GameplaySkinSlotValueType? declaredValueType,
            GameplaySkinDocumentTarget target,
            string? value,
            int lineNumber)
        {
            Presence = presence;
            Validity = validity;
            Operation = operation;
            Descriptor = descriptor;
            DeclaredSlotId = declaredSlotId;
            DeclaredValueType = declaredValueType;
            Target = target;
            Value = value;
            LineNumber = lineNumber;
        }

        internal static GameplaySkinDocumentEntry Absent(GameplaySkinSlotDescriptor descriptor, GameplaySkinDocumentTarget target)
            => new GameplaySkinDocumentEntry(
                GameplaySkinDocumentDeclarationPresence.Absent,
                GameplaySkinDocumentValueValidity.None,
                GameplaySkinDocumentOperation.None,
                descriptor,
                descriptor.Id,
                descriptor.ValueType,
                target,
                null,
                0);

        public override string ToString() => $"{Presence}:{Validity}:{Operation}:{Descriptor?.Id ?? "unknown"}";
    }

    public sealed class GameplaySkinDocumentSection
    {
        public GameplaySkinSlotCatalogFamily Family { get; }

        public int Version { get; }

        public IReadOnlyList<GameplaySkinDocumentEntry> Entries { get; }

        internal GameplaySkinDocumentSection(GameplaySkinSlotCatalogFamily family, int version, GameplaySkinDocumentEntry[] entries)
        {
            Family = family;
            Version = version;
            Entries = Array.AsReadOnly(entries);
        }
    }

    public enum GameplaySkinLegacyLineKind
    {
        Blank = 0,
        Comment = 1,
        Field = 2,
        Unparsed = 3,
    }

    /// <summary>
    /// One immutable normalized legacy token. Unknown legacy fields are retained without C4 diagnostics.
    /// </summary>
    public sealed class GameplaySkinLegacyLine
    {
        public GameplaySkinLegacyLineKind Kind { get; }

        public int LineNumber { get; }

        public string NormalizedText { get; }

        public string? Key { get; }

        public string? Value { get; }

        public char? Separator { get; }

        internal GameplaySkinLegacyLine(
            GameplaySkinLegacyLineKind kind,
            int lineNumber,
            string normalizedText,
            string? key,
            string? value,
            char? separator)
        {
            Kind = kind;
            LineNumber = lineNumber;
            NormalizedText = normalizedText;
            Key = key;
            Value = value;
            Separator = separator;
        }

        public override string ToString() => $"{nameof(GameplaySkinLegacyLine)}:{Kind}:Line{LineNumber}";
    }

    public sealed class GameplaySkinLegacySection
    {
        public string Name { get; }

        public int HeaderLineNumber { get; }

        public IReadOnlyList<GameplaySkinLegacyLine> Lines { get; }

        internal GameplaySkinLegacySection(string name, int headerLineNumber, GameplaySkinLegacyLine[] lines)
        {
            Name = name;
            HeaderLineNumber = headerLineNumber;
            Lines = Array.AsReadOnly(lines);
        }

        public override string ToString() => $"{nameof(GameplaySkinLegacySection)}:Line{HeaderLineNumber}";
    }

    public enum GameplaySkinCodecDiagnosticCode
    {
        InvalidUtf8 = 0,
        UnknownExtension = 1,
        UnsupportedVersion = 2,
        MissingTarget = 3,
        InvalidTargetScope = 4,
        InvalidTargetIdentity = 5,
        InvalidTargetIndex = 6,
        UnknownField = 7,
        UnknownSlot = 8,
        ExtensionSlotMismatch = 9,
        InvalidValueType = 10,
        InvalidState = 11,
        MissingValue = 12,
        InvalidEscape = 13,
        DuplicateDeclaration = 14,
        SuppressionForbidden = 15,
        MalformedSectionHeader = 16,
        InvalidTargetContext = 17,
        InvalidTargetApplicability = 18,
        UnexpectedBom = 19,
        InvalidPublicationTarget = 20,
    }

    public enum GameplaySkinCodecDiagnosticSeverity
    {
        Field = 0,
        DocumentFatal = 1,
    }

    public sealed class GameplaySkinCodecDiagnostic
    {
        public GameplaySkinCodecDiagnosticCode Code { get; }

        public string Id => $"OMS-SKIN-CODEC-{(int)Code + 1:000}";

        public GameplaySkinCodecDiagnosticSeverity Severity => Code is GameplaySkinCodecDiagnosticCode.InvalidUtf8
            or GameplaySkinCodecDiagnosticCode.UnknownExtension
            or GameplaySkinCodecDiagnosticCode.UnsupportedVersion
            or GameplaySkinCodecDiagnosticCode.MalformedSectionHeader
            or GameplaySkinCodecDiagnosticCode.UnexpectedBom
            ? GameplaySkinCodecDiagnosticSeverity.DocumentFatal
            : GameplaySkinCodecDiagnosticSeverity.Field;

        public int LineNumber { get; }

        public string? SlotId { get; }

        internal GameplaySkinCodecDiagnostic(GameplaySkinCodecDiagnosticCode code, int lineNumber, string? slotId = null)
        {
            Code = code;
            LineNumber = lineNumber;
            SlotId = slotId;
        }

        public override string ToString() => SlotId == null ? $"{Id}:Line{LineNumber}" : $"{Id}:{SlotId}:Line{LineNumber}";
    }

    /// <summary>
    /// Immutable result of tokenizing and decoding one exact configuration content revision.
    /// </summary>
    public sealed class GameplaySkinDocument
    {
        private readonly GameplaySkinLayoutSnapshot? boundPublicationSnapshot;

        public GameplaySkinDocumentIdentity Identity { get; }

        public IReadOnlyList<GameplaySkinDocumentSection> Sections { get; }

        public IReadOnlyList<GameplaySkinLegacySection> LegacySections { get; }

        public IReadOnlyList<GameplaySkinCodecDiagnostic> Diagnostics { get; }

        public bool HasFatalDiagnostics => Diagnostics.Any(diagnostic => diagnostic.Severity == GameplaySkinCodecDiagnosticSeverity.DocumentFatal);

        internal bool IsBoundToPublication => boundPublicationSnapshot != null;

        internal GameplaySkinLayoutSnapshot BoundPublicationSnapshot => boundPublicationSnapshot
                                                                         ?? throw new InvalidOperationException(
                                                                             "The gameplay skin document is not bound to an exact publication.");

        internal IReadOnlyList<string> NormalizedSourceLines { get; }

        internal GameplaySkinDocument(
            GameplaySkinDocumentIdentity identity,
            IReadOnlyList<GameplaySkinDocumentSection> sections,
            IReadOnlyList<GameplaySkinLegacySection> legacySections,
            IReadOnlyList<GameplaySkinCodecDiagnostic> diagnostics,
            IReadOnlyList<string> normalizedSourceLines,
            GameplaySkinLayoutSnapshot? boundPublicationSnapshot = null)
        {
            Identity = identity;
            Sections = sections;
            LegacySections = legacySections;
            Diagnostics = diagnostics;
            NormalizedSourceLines = normalizedSourceLines;
            this.boundPublicationSnapshot = boundPublicationSnapshot;
        }

        internal GameplaySkinDocument WithIdentity(GameplaySkinDocumentIdentity identity)
        {
            ArgumentNullException.ThrowIfNull(identity);

            if (!string.Equals(Identity.ContentRevision, identity.ContentRevision, StringComparison.Ordinal))
                throw new ArgumentException("A tokenized document cannot be rebound to different content.", nameof(identity));

            if (Identity.Equals(identity))
                return this;

            if (boundPublicationSnapshot != null)
                throw new InvalidOperationException("A document bound to an exact publication cannot change revision identity.");

            return new GameplaySkinDocument(identity, Sections, LegacySections, Diagnostics, NormalizedSourceLines);
        }

        /// <summary>
        /// Binds this retained token stream to one exact C2/C3 package and layout publication without reparsing it.
        /// </summary>
        public GameplaySkinDocument BindToPublication(GameplaySkinLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (boundPublicationSnapshot != null)
            {
                if (!ReferenceEquals(boundPublicationSnapshot, snapshot))
                    throw new InvalidOperationException("A gameplay skin document cannot be rebound across an exact publication boundary.");

                return this;
            }

            GameplaySkinPackageRevision package = snapshot.Context.PackageRevision;
            GameplaySkinDocumentSourceKind sourceKind = package.SourceKind switch
            {
                GameplaySkinPackageSourceKind.ProtectedFallback => GameplaySkinDocumentSourceKind.ProtectedFallback,
                GameplaySkinPackageSourceKind.RealmPackage => GameplaySkinDocumentSourceKind.RealmPackage,
                GameplaySkinPackageSourceKind.ManagedFolder => GameplaySkinDocumentSourceKind.ManagedFolder,
                GameplaySkinPackageSourceKind.ExternalFolder => GameplaySkinDocumentSourceKind.ExternalFolder,
                GameplaySkinPackageSourceKind.Compatibility => GameplaySkinDocumentSourceKind.Compatibility,
                _ => throw new InvalidOperationException("The gameplay skin publication has an unknown package source authority."),
            };
            GameplaySkinDocumentIdentity boundIdentity = GameplaySkinDocumentIdentity.CreateBound(
                sourceKind,
                package.RecordId,
                Identity.ContentRevision,
                package.Generation,
                package.Generation,
                snapshot.Context.LayoutRevision);

            if (Identity.IsBound)
            {
                if (!Identity.Equals(boundIdentity))
                    throw new InvalidOperationException("A gameplay skin document cannot be rebound across an exact publication boundary.");
            }

            GameplaySkinCodecDiagnostic[] publicationDiagnostics = Sections
                                                                    .SelectMany(section => section.Entries)
                                                                    .Where(entry => entry.Presence == GameplaySkinDocumentDeclarationPresence.Declared
                                                                                    && entry.Descriptor != null
                                                                                    && GameplaySkinSlotApplicabilityValidator.ValidatePublicationTarget(entry.Target, snapshot)
                                                                                    is not GameplaySkinDocumentPublicationTargetValidationResult.Valid
                                                                                    and not GameplaySkinDocumentPublicationTargetValidationResult.SelectorNotApplicable)
                                                                    .Select(entry => new GameplaySkinCodecDiagnostic(
                                                                        GameplaySkinCodecDiagnosticCode.InvalidPublicationTarget,
                                                                        entry.LineNumber,
                                                                        entry.Descriptor!.Id))
                                                                    .ToArray();
            IReadOnlyList<GameplaySkinCodecDiagnostic> boundDiagnostics = publicationDiagnostics.Length == 0
                ? Diagnostics
                : Array.AsReadOnly(Diagnostics.Concat(publicationDiagnostics).ToArray());

            return new GameplaySkinDocument(boundIdentity, Sections, LegacySections, boundDiagnostics, NormalizedSourceLines, snapshot);
        }

        public GameplaySkinDocumentEntry GetEntry(GameplaySkinSlotDescriptor descriptor, GameplaySkinDocumentTarget target)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(target);

            GameplaySkinDocumentEntry? found = Sections
                .SelectMany(section => section.Entries)
                .LastOrDefault(entry => ReferenceEquals(entry.Descriptor, descriptor) && entry.Target.Equals(target));

            return found ?? GameplaySkinDocumentEntry.Absent(descriptor, target);
        }

        public GameplaySkinDocumentEntry GetEntry(GameplaySkinSlotDescriptor descriptor, GameplaySkinResolvedMaterialTarget target)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(target);

            if (boundPublicationSnapshot == null)
                throw new InvalidOperationException("Resolved gameplay material lookup requires an exact bound publication.");

            GameplaySkinDocumentEntry? found = Sections
                .SelectMany(section => section.Entries)
                .Where(entry => ReferenceEquals(entry.Descriptor, descriptor)
                                && entry.Target.Matches(boundPublicationSnapshot, target))
                .OrderByDescending(entry => entry.Target.RulesetSelector != GameplaySkinDocumentRulesetSelector.Any)
                .ThenByDescending(entry => entry.Target.KeymodeSelector != GameplaySkinDocumentTarget.ANY_KEYMODE)
                .ThenByDescending(entry => entry.Target.StageModeSelector != GameplaySkinDocumentStageModeSelector.Any)
                .ThenByDescending(entry => entry.Target.ScopeSpecificity)
                .ThenByDescending(entry => entry.LineNumber)
                .FirstOrDefault();

            if (found != null)
                return found;

            GameplaySkinLayoutContext context = boundPublicationSnapshot.Context;
            GameplaySkinDocumentRulesetSelector rulesetSelector = context.RulesetId switch
            {
                "mania" => GameplaySkinDocumentRulesetSelector.Mania,
                "bms" => GameplaySkinDocumentRulesetSelector.Bms,
                _ => GameplaySkinDocumentRulesetSelector.Any,
            };
            GameplaySkinDocumentStageModeSelector stageSelector = context.Topology.GroupsInLogicalOrder.Count switch
            {
                1 => GameplaySkinDocumentStageModeSelector.Single,
                2 => GameplaySkinDocumentStageModeSelector.Dual,
                _ => GameplaySkinDocumentStageModeSelector.Any,
            };
            GameplaySkinDocumentTarget absentTarget = target.Kind switch
            {
                GameplaySkinResolvedMaterialTargetKind.Global => GameplaySkinDocumentTarget.ForGlobal(
                    rulesetSelector, context.KeymodeId, stageSelector),
                GameplaySkinResolvedMaterialTargetKind.Stage => GameplaySkinDocumentTarget.ForStage(
                    rulesetSelector, context.KeymodeId, stageSelector,
                    target.GroupId!, target.GroupLogicalIndex!.Value, target.GroupVisualIndex!.Value),
                GameplaySkinResolvedMaterialTargetKind.Group => GameplaySkinDocumentTarget.ForGroup(
                    rulesetSelector, context.KeymodeId, stageSelector,
                    target.GroupId!, target.GroupLogicalIndex!.Value, target.GroupVisualIndex!.Value),
                GameplaySkinResolvedMaterialTargetKind.Lane => GameplaySkinDocumentTarget.ForLane(
                    rulesetSelector,
                    context.KeymodeId,
                    stageSelector,
                    target.GroupId!,
                    target.LaneId!,
                    target.GroupLogicalIndex!.Value,
                    target.GroupVisualIndex!.Value,
                    target.GlobalLogicalIndex!.Value,
                    target.GlobalVisualIndex!.Value,
                    target.GroupLocalLogicalIndex!.Value,
                    target.GroupLocalVisualIndex!.Value),
                _ => throw new ArgumentOutOfRangeException(nameof(target)),
            };

            return GameplaySkinDocumentEntry.Absent(descriptor, absentTarget);
        }

        public override string ToString() => $"{nameof(GameplaySkinDocument)}:{Sections.Count}:{LegacySections.Count}:{Diagnostics.Count}";
    }
}
