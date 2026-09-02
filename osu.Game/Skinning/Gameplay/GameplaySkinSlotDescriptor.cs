// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    public enum GameplaySkinSlotCatalogFamily
    {
        Common = 0,
        Bms = 1,
    }

    public enum GameplaySkinSlotClassification
    {
        Required = 0,
        Recommended = 1,
        Optional = 2,
    }

    [Flags]
    public enum GameplaySkinSlotScope
    {
        None = 0,
        Global = 1 << 0,
        Stage = 1 << 1,
        Group = 1 << 2,
        Lane = 1 << 3,
    }

    public enum GameplaySkinSlotValueType
    {
        Resource = 0,
        Colour = 1,
        Number = 2,
        Boolean = 3,
        Text = 4,
    }

    public enum GameplaySkinSlotDefaultSemantics
    {
        InheritToLowerAuthorityThenCanonicalFallback = 0,
    }

    public enum GameplaySkinSlotSuppressEligibility
    {
        Forbidden = 0,
        Allowed = 1,
    }

    [Flags]
    public enum GameplaySkinRulesetApplicability
    {
        None = 0,
        Mania = 1 << 0,
        Bms = 1 << 1,
    }

    [Flags]
    public enum GameplaySkinStageApplicability
    {
        None = 0,
        Single = 1 << 0,
        Dual = 1 << 1,
    }

    [Flags]
    public enum GameplaySkinLaneRoleApplicability
    {
        None = 0,
        Key = 1 << 0,
        SpecialKey = 1 << 1,
        Scratch = 1 << 2,
    }

    /// <summary>
    /// Explicit ruleset/keymode families to which a public slot applies.
    /// </summary>
    [Flags]
    public enum GameplaySkinKeymodeApplicability
    {
        None = 0,
        Mania = 1 << 0,
        Bms5K = 1 << 1,
        Bms7K = 1 << 2,
        Bms9K = 1 << 3,
        Bms14K = 1 << 4,
    }

    /// <summary>
    /// Immutable keymode, stage and lane-role applicability for one public catalog slot.
    /// </summary>
    public sealed class GameplaySkinSlotApplicability
    {
        public GameplaySkinRulesetApplicability Rulesets { get; }

        public GameplaySkinStageApplicability Stages { get; }

        public GameplaySkinLaneRoleApplicability LaneRoles { get; }

        public GameplaySkinKeymodeApplicability Keymodes { get; }

        public int MinimumKeyCount { get; }

        public int MaximumKeyCount { get; }

        internal GameplaySkinSlotApplicability(
            GameplaySkinRulesetApplicability rulesets,
            GameplaySkinStageApplicability stages,
            GameplaySkinLaneRoleApplicability laneRoles,
            GameplaySkinKeymodeApplicability keymodes,
            int minimumKeyCount,
            int maximumKeyCount)
        {
            const GameplaySkinRulesetApplicability known_rulesets = GameplaySkinRulesetApplicability.Mania | GameplaySkinRulesetApplicability.Bms;
            const GameplaySkinStageApplicability known_stages = GameplaySkinStageApplicability.Single | GameplaySkinStageApplicability.Dual;
            const GameplaySkinLaneRoleApplicability known_lane_roles =
                GameplaySkinLaneRoleApplicability.Key | GameplaySkinLaneRoleApplicability.SpecialKey | GameplaySkinLaneRoleApplicability.Scratch;
            const GameplaySkinKeymodeApplicability known_keymodes =
                GameplaySkinKeymodeApplicability.Mania | GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K
                | GameplaySkinKeymodeApplicability.Bms9K | GameplaySkinKeymodeApplicability.Bms14K;

            if (rulesets == GameplaySkinRulesetApplicability.None || (rulesets & ~known_rulesets) != 0)
                throw new ArgumentOutOfRangeException(nameof(rulesets));

            if (stages == GameplaySkinStageApplicability.None || (stages & ~known_stages) != 0)
                throw new ArgumentOutOfRangeException(nameof(stages));

            if ((laneRoles & ~known_lane_roles) != 0)
                throw new ArgumentOutOfRangeException(nameof(laneRoles));

            if (keymodes == GameplaySkinKeymodeApplicability.None || (keymodes & ~known_keymodes) != 0)
                throw new ArgumentOutOfRangeException(nameof(keymodes));

            if ((rulesets & GameplaySkinRulesetApplicability.Mania) == 0 && (keymodes & GameplaySkinKeymodeApplicability.Mania) != 0)
                throw new ArgumentException("Mania keymode applicability requires mania ruleset applicability.", nameof(keymodes));

            const GameplaySkinKeymodeApplicability bms_keymodes =
                GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K
                | GameplaySkinKeymodeApplicability.Bms9K | GameplaySkinKeymodeApplicability.Bms14K;

            if ((rulesets & GameplaySkinRulesetApplicability.Bms) == 0 && (keymodes & bms_keymodes) != 0)
                throw new ArgumentException("BMS keymode applicability requires BMS ruleset applicability.", nameof(keymodes));

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumKeyCount);
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumKeyCount, minimumKeyCount);

            Rulesets = rulesets;
            Stages = stages;
            LaneRoles = laneRoles;
            Keymodes = keymodes;
            MinimumKeyCount = minimumKeyCount;
            MaximumKeyCount = maximumKeyCount;
        }
    }

    /// <summary>
    /// Defines one stable, versioned public gameplay skin slot.
    /// </summary>
    /// <remarks>
    /// Renderer support is deliberately not stored here. Runtime capability is represented separately by
    /// <see cref="GameplaySkinRuntimeCapabilitySet"/> and cannot change catalog semantics.
    /// </remarks>
    public sealed class GameplaySkinSlotDescriptor
    {
        public string Id { get; }

        public string StableName { get; }

        public GameplaySkinSlotCatalogFamily CatalogFamily { get; }

        public int CatalogVersion { get; }

        public GameplaySkinSlotScope AllowedScopes { get; }

        public GameplaySkinSlotValueType ValueType { get; }

        public GameplaySkinSlotClassification Classification { get; }

        public GameplaySkinSlotDefaultSemantics DefaultSemantics { get; }

        public GameplaySkinSlotSuppressEligibility SuppressEligibility { get; }

        public GameplaySkinSlotApplicability Applicability { get; }

        /// <summary>
        /// A stable, content-free diagnostic token unique within the public catalog.
        /// </summary>
        public string DiagnosticId { get; }

        /// <summary>
        /// Compatibility projection used by the existing three-state resolver API.
        /// </summary>
        public SkinSlotRequirement Requirement { get; }

        internal GameplaySkinSlotDescriptor(string id, SkinSlotRequirement requirement)
            : this(
                id,
                "CompatibilitySlot",
                GameplaySkinSlotCatalogFamily.Common,
                GameplaySkinSlotCatalog.COMMON_VERSION,
                GameplaySkinSlotScope.Global,
                GameplaySkinSlotValueType.Resource,
                requirement == SkinSlotRequirement.Critical ? GameplaySkinSlotClassification.Required : GameplaySkinSlotClassification.Optional,
                GameplaySkinSlotDefaultSemantics.InheritToLowerAuthorityThenCanonicalFallback,
                requirement == SkinSlotRequirement.Critical ? GameplaySkinSlotSuppressEligibility.Forbidden : GameplaySkinSlotSuppressEligibility.Allowed,
                new GameplaySkinSlotApplicability(
                    GameplaySkinRulesetApplicability.Mania | GameplaySkinRulesetApplicability.Bms,
                    GameplaySkinStageApplicability.Single | GameplaySkinStageApplicability.Dual,
                    GameplaySkinLaneRoleApplicability.None,
                    GameplaySkinKeymodeApplicability.Mania | GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K
                    | GameplaySkinKeymodeApplicability.Bms9K | GameplaySkinKeymodeApplicability.Bms14K,
                    1,
                    20),
                "OMS-SKIN-SLOT-000")
        {
            if (requirement is not SkinSlotRequirement.Critical and not SkinSlotRequirement.Optional)
                throw new ArgumentOutOfRangeException(nameof(requirement), requirement, "Unknown gameplay skin slot requirement.");
        }

        internal GameplaySkinSlotDescriptor(
            string id,
            string stableName,
            GameplaySkinSlotCatalogFamily catalogFamily,
            int catalogVersion,
            GameplaySkinSlotScope allowedScopes,
            GameplaySkinSlotValueType valueType,
            GameplaySkinSlotClassification classification,
            GameplaySkinSlotDefaultSemantics defaultSemantics,
            GameplaySkinSlotSuppressEligibility suppressEligibility,
            GameplaySkinSlotApplicability applicability,
            string diagnosticId)
        {
            ArgumentNullException.ThrowIfNull(id);
            ArgumentNullException.ThrowIfNull(stableName);
            ArgumentNullException.ThrowIfNull(applicability);
            ArgumentNullException.ThrowIfNull(diagnosticId);

            if (!isValidId(id))
                throw new ArgumentException("Gameplay skin slot IDs must contain lowercase ASCII dot-separated segments.", nameof(id));

            if (!isValidStableName(stableName))
                throw new ArgumentException("Gameplay skin slot stable names must be non-empty PascalCase ASCII identifiers.", nameof(stableName));

            if (!Enum.IsDefined(catalogFamily))
                throw new ArgumentOutOfRangeException(nameof(catalogFamily));

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(catalogVersion);

            const GameplaySkinSlotScope known_scopes =
                GameplaySkinSlotScope.Global | GameplaySkinSlotScope.Stage | GameplaySkinSlotScope.Group | GameplaySkinSlotScope.Lane;

            if (allowedScopes == GameplaySkinSlotScope.None || (allowedScopes & ~known_scopes) != 0)
                throw new ArgumentOutOfRangeException(nameof(allowedScopes));

            if (!Enum.IsDefined(valueType))
                throw new ArgumentOutOfRangeException(nameof(valueType));

            if (!Enum.IsDefined(classification))
                throw new ArgumentOutOfRangeException(nameof(classification));

            if (!Enum.IsDefined(defaultSemantics))
                throw new ArgumentOutOfRangeException(nameof(defaultSemantics));

            if (!Enum.IsDefined(suppressEligibility))
                throw new ArgumentOutOfRangeException(nameof(suppressEligibility));

            if (classification != GameplaySkinSlotClassification.Optional && suppressEligibility != GameplaySkinSlotSuppressEligibility.Forbidden)
                throw new ArgumentException("Only optional gameplay skin slots may be suppressible.", nameof(suppressEligibility));

            if (!isValidDiagnosticId(diagnosticId))
                throw new ArgumentException("Gameplay skin diagnostic IDs must use OMS-SKIN-SLOT-NNN.", nameof(diagnosticId));

            if ((allowedScopes & GameplaySkinSlotScope.Lane) == 0 && applicability.LaneRoles != GameplaySkinLaneRoleApplicability.None)
                throw new ArgumentException("Only lane-scoped slots may declare lane-role applicability.", nameof(applicability));

            Id = id;
            StableName = stableName;
            CatalogFamily = catalogFamily;
            CatalogVersion = catalogVersion;
            AllowedScopes = allowedScopes;
            ValueType = valueType;
            Classification = classification;
            DefaultSemantics = defaultSemantics;
            SuppressEligibility = suppressEligibility;
            Applicability = applicability;
            DiagnosticId = diagnosticId;
            // Preserve the pre-C4 ABI projection: the original seven required slots remain Critical and every
            // presentation slot remains Optional. New code must use SuppressEligibility for suppression decisions.
            Requirement = classification == GameplaySkinSlotClassification.Required
                ? SkinSlotRequirement.Critical
                : SkinSlotRequirement.Optional;
        }

        public override string ToString() => Id;

        private static bool isValidId(string id)
        {
            if (id.Length == 0)
                return false;

            foreach (string segment in id.Split('.'))
            {
                if (segment.Length == 0 || !isAsciiLower(segment[0]) || !isAsciiLowerOrDigit(segment[^1]))
                    return false;

                foreach (char character in segment)
                {
                    if (!isAsciiLowerOrDigit(character) && character != '-')
                        return false;
                }
            }

            return true;
        }

        private static bool isValidStableName(string value)
        {
            if (value.Length == 0 || value[0] is < 'A' or > 'Z')
                return false;

            foreach (char character in value)
            {
                if (character is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z') and not (>= '0' and <= '9'))
                    return false;
            }

            return true;
        }

        private static bool isValidDiagnosticId(string value)
            => value.Length == 17
               && value.StartsWith("OMS-SKIN-SLOT-", StringComparison.Ordinal)
               && value[14] is >= '0' and <= '9'
               && value[15] is >= '0' and <= '9'
               && value[16] is >= '0' and <= '9';

        private static bool isAsciiLower(char character) => character is >= 'a' and <= 'z';

        private static bool isAsciiLowerOrDigit(char character) => isAsciiLower(character) || character is >= '0' and <= '9';
    }
}
