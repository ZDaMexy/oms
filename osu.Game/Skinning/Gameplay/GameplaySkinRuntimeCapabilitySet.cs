// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    [Flags]
    public enum GameplaySkinRuntimeSlotCapability
    {
        None = 0,
        Provide = 1 << 0,
        Suppress = 1 << 1,
    }

    /// <summary>
    /// Runtime support for one stable catalog slot. This does not alter catalog eligibility or authoring semantics.
    /// </summary>
    public sealed class GameplaySkinRuntimeSlotSupport
    {
        public GameplaySkinSlotDescriptor Descriptor { get; }

        public GameplaySkinRuntimeSlotCapability Capabilities { get; }

        private GameplaySkinRuntimeSlotSupport(GameplaySkinSlotDescriptor descriptor, GameplaySkinRuntimeSlotCapability capabilities)
        {
            Descriptor = descriptor;
            Capabilities = capabilities;
        }

        public static GameplaySkinRuntimeSlotSupport Create(GameplaySkinSlotDescriptor descriptor, GameplaySkinRuntimeSlotCapability capabilities)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            const GameplaySkinRuntimeSlotCapability known = GameplaySkinRuntimeSlotCapability.Provide | GameplaySkinRuntimeSlotCapability.Suppress;

            if (capabilities == GameplaySkinRuntimeSlotCapability.None || (capabilities & ~known) != 0)
                throw new ArgumentOutOfRangeException(nameof(capabilities));

            if ((capabilities & GameplaySkinRuntimeSlotCapability.Suppress) != 0
                && descriptor.SuppressEligibility != GameplaySkinSlotSuppressEligibility.Allowed)
                throw new ArgumentException("Runtime support cannot grant suppression which the public catalog forbids.", nameof(capabilities));

            return new GameplaySkinRuntimeSlotSupport(descriptor, capabilities);
        }
    }

    /// <summary>
    /// Immutable runtime capability view for one consumer or ruleset.
    /// </summary>
    public sealed class GameplaySkinRuntimeCapabilitySet
    {
        public IReadOnlyDictionary<string, GameplaySkinRuntimeSlotSupport> Support { get; }

        private GameplaySkinRuntimeCapabilitySet(IReadOnlyDictionary<string, GameplaySkinRuntimeSlotSupport> support)
        {
            Support = support;
        }

        public static GameplaySkinRuntimeCapabilitySet Create(IEnumerable<GameplaySkinRuntimeSlotSupport> support)
        {
            ArgumentNullException.ThrowIfNull(support);

            var result = new Dictionary<string, GameplaySkinRuntimeSlotSupport>(StringComparer.Ordinal);

            foreach (GameplaySkinRuntimeSlotSupport item in support)
            {
                ArgumentNullException.ThrowIfNull(item);

                if (!GameplaySkinSlotCatalog.TryGet(item.Descriptor.Id, out GameplaySkinSlotDescriptor? catalogued)
                    || !ReferenceEquals(catalogued, item.Descriptor))
                    throw new ArgumentException("Runtime capability entries must reference the exact public catalog descriptor.", nameof(support));

                if (!result.TryAdd(item.Descriptor.Id, item))
                    throw new ArgumentException("Runtime capability entries must be unique by stable slot ID.", nameof(support));
            }

            return new GameplaySkinRuntimeCapabilitySet(new ReadOnlyDictionary<string, GameplaySkinRuntimeSlotSupport>(result));
        }

        public bool TryGet(GameplaySkinSlotDescriptor descriptor, out GameplaySkinRuntimeSlotSupport? slotSupport)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            return Support.TryGetValue(descriptor.Id, out slotSupport);
        }

        public override string ToString() => $"{nameof(GameplaySkinRuntimeCapabilitySet)}:{Support.Count}";
    }

    /// <summary>
    /// One explicit decision in a versioned ruleset runtime-support profile. Catalogued does not imply supported.
    /// </summary>
    public enum GameplaySkinRuntimeSupportDecisionKind
    {
        Supported = 0,
        NotApplicable = 1,
    }

    public sealed class GameplaySkinRuntimeSupportDecision
    {
        public GameplaySkinSlotDescriptor Descriptor { get; }

        public GameplaySkinRuntimeSupportDecisionKind Kind { get; }

        internal GameplaySkinRuntimeSupportDecision(
            GameplaySkinSlotDescriptor descriptor,
            GameplaySkinRuntimeSupportDecisionKind kind)
        {
            Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));

            if (!Enum.IsDefined(kind))
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown gameplay-skin runtime-support decision.");

            Kind = kind;
        }
    }

    /// <summary>
    /// The single versioned truth for which frozen public catalog slots one ruleset can actually host.
    /// </summary>
    /// <remarks>
    /// Every profile contains one decision for every catalog descriptor in canonical catalog order. It therefore
    /// cannot silently turn a catalogued-but-unreachable slot into inheritance, nor can a renderer grow an unversioned
    /// private support list. Suppression is projected only for supported slots whose catalog contract permits it.
    /// </remarks>
    public sealed class GameplaySkinRuntimeSupportProfile
    {
        public const string CONTRACT_ID = "oms-gameplay-skin-runtime-support.v1";
        public const string BMS_PROFILE_ID = "oms-gameplay-skin-runtime-support.bms.v1";
        public const string MANIA_PROFILE_ID = "oms-gameplay-skin-runtime-support.mania.v1";

        internal const string COMPATIBILITY_PROFILE_ID = "compatibility.empty";

        private readonly IReadOnlyDictionary<string, GameplaySkinRuntimeSupportDecision> decisionsById;

        public static GameplaySkinRuntimeSupportProfile Bms { get; } = create(
            "bms",
            BMS_PROFILE_ID,
            GameplaySkinSlotCatalog.All);

        public static GameplaySkinRuntimeSupportProfile Mania { get; } = create(
            "mania",
            MANIA_PROFILE_ID,
            GameplaySkinSlotCatalog.Common.Where(descriptor =>
                !ReferenceEquals(descriptor, GameplaySkinSlotCatalog.Mine)
                && !ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaViewport)
                && !ReferenceEquals(descriptor, GameplaySkinSlotCatalog.BgaFrame)));

        internal static GameplaySkinRuntimeSupportProfile CompatibilityEmpty { get; } = create(
            "compatibility",
            COMPATIBILITY_PROFILE_ID,
            Array.Empty<GameplaySkinSlotDescriptor>(),
            COMPATIBILITY_PROFILE_ID);

        public string ContractVersion { get; }

        public string ProfileId { get; }

        public string RulesetId { get; }

        public IReadOnlyList<GameplaySkinRuntimeSupportDecision> Decisions { get; }

        public GameplaySkinRuntimeCapabilitySet Capabilities { get; }

        private GameplaySkinRuntimeSupportProfile(
            string rulesetId,
            string profileId,
            string contractVersion,
            IReadOnlyList<GameplaySkinRuntimeSupportDecision> decisions,
            GameplaySkinRuntimeCapabilitySet capabilities)
        {
            RulesetId = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(rulesetId, nameof(rulesetId));
            ProfileId = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(profileId, nameof(profileId));
            ContractVersion = GameplaySkinMaterialTokenValidation.ValidateOpaqueToken(contractVersion, nameof(contractVersion));
            Decisions = decisions;
            Capabilities = capabilities;
            decisionsById = new ReadOnlyDictionary<string, GameplaySkinRuntimeSupportDecision>(
                decisions.ToDictionary(decision => decision.Descriptor.Id, StringComparer.Ordinal));
        }

        public static GameplaySkinRuntimeSupportProfile ForRuleset(string rulesetId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(rulesetId);

            return rulesetId switch
            {
                "bms" => Bms,
                "mania" => Mania,
                _ => throw new ArgumentException("No versioned gameplay-skin runtime-support profile exists for this ruleset.", nameof(rulesetId)),
            };
        }

        public bool TryGetDecision(
            GameplaySkinSlotDescriptor descriptor,
            out GameplaySkinRuntimeSupportDecision? decision)
        {
            ArgumentNullException.ThrowIfNull(descriptor);

            if (!GameplaySkinSlotCatalog.TryGet(descriptor.Id, out GameplaySkinSlotDescriptor? catalogued)
                || !ReferenceEquals(catalogued, descriptor))
            {
                decision = null;
                return false;
            }

            return decisionsById.TryGetValue(descriptor.Id, out decision);
        }

        public bool IsSupported(GameplaySkinSlotDescriptor descriptor)
            => TryGetDecision(descriptor, out GameplaySkinRuntimeSupportDecision? decision)
               && decision!.Kind == GameplaySkinRuntimeSupportDecisionKind.Supported;

        public override string ToString() => $"{ContractVersion}:{ProfileId}:{Capabilities.Support.Count}/{Decisions.Count}";

        private static GameplaySkinRuntimeSupportProfile create(
            string rulesetId,
            string profileId,
            IEnumerable<GameplaySkinSlotDescriptor> supportedDescriptors,
            string contractVersion = CONTRACT_ID)
        {
            ArgumentNullException.ThrowIfNull(supportedDescriptors);
            GameplaySkinSlotDescriptor[] supported = supportedDescriptors.ToArray();

            if (supported.Any(descriptor => descriptor == null
                                            || !GameplaySkinSlotCatalog.TryGet(descriptor.Id, out GameplaySkinSlotDescriptor? catalogued)
                                            || !ReferenceEquals(catalogued, descriptor)))
            {
                throw new ArgumentException("Runtime-support profiles require exact public catalog descriptors.", nameof(supportedDescriptors));
            }

            if (supported.Select(descriptor => descriptor.Id).Distinct(StringComparer.Ordinal).Count() != supported.Length)
                throw new ArgumentException("Runtime-support profile entries must be unique by stable catalog ID.", nameof(supportedDescriptors));

            var supportedIds = new HashSet<string>(supported.Select(descriptor => descriptor.Id), StringComparer.Ordinal);
            GameplaySkinRuntimeSupportDecision[] decisions = GameplaySkinSlotCatalog.All.Select(descriptor =>
                new GameplaySkinRuntimeSupportDecision(
                    descriptor,
                    supportedIds.Contains(descriptor.Id)
                        ? GameplaySkinRuntimeSupportDecisionKind.Supported
                        : GameplaySkinRuntimeSupportDecisionKind.NotApplicable)).ToArray();
            GameplaySkinRuntimeCapabilitySet capabilities = GameplaySkinRuntimeCapabilitySet.Create(
                decisions.Where(decision => decision.Kind == GameplaySkinRuntimeSupportDecisionKind.Supported)
                         .Select(decision => GameplaySkinRuntimeSlotSupport.Create(
                             decision.Descriptor,
                             GameplaySkinRuntimeSlotCapability.Provide
                             | (decision.Descriptor.SuppressEligibility == GameplaySkinSlotSuppressEligibility.Allowed
                                 ? GameplaySkinRuntimeSlotCapability.Suppress
                                 : GameplaySkinRuntimeSlotCapability.None))));

            return new GameplaySkinRuntimeSupportProfile(
                rulesetId,
                profileId,
                contractVersion,
                Array.AsReadOnly(decisions),
                capabilities);
        }
    }
}
