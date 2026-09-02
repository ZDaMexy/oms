// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Globalization;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Stable reason why a catalog slot cannot apply to one exact gameplay publication target.
    /// </summary>
    public enum GameplaySkinSlotApplicabilityResult
    {
        Applicable = 0,
        InvalidTarget = 1,
        UnsupportedRuleset = 2,
        UnsupportedKeymode = 3,
        UnsupportedStageMode = 4,
        UnsupportedKeyCount = 5,
        UnsupportedLaneRole = 6,
    }

    /// <summary>
    /// Exact-publication validation for one author target after its selectors match the current ruleset/keymode/stage.
    /// </summary>
    public enum GameplaySkinDocumentPublicationTargetValidationResult
    {
        Valid = 0,
        SelectorNotApplicable = 1,
        UnknownGroup = 2,
        GroupIndexMismatch = 3,
        UnknownLane = 4,
        LaneGroupMismatch = 5,
        LaneIndexMismatch = 6,
    }

    /// <summary>
    /// The sole runtime validator for the ruleset, keymode, stage, key-count and lane-role semantics frozen by
    /// <see cref="GameplaySkinSlotCatalog"/>.
    /// </summary>
    /// <remarks>
    /// The validator consumes only the exact C3 layout publication and its stable topology metadata. Rulesets and
    /// renderers must not duplicate these checks or infer them from drawable order, geometry or enum ordinals.
    /// </remarks>
    public static class GameplaySkinSlotApplicabilityValidator
    {
        /// <summary>
        /// Validates stable IDs and every explicit logical/visual index against one exact C3 topology publication.
        /// A selector for another ruleset/keymode/stage is not an error in a portable author document.
        /// </summary>
        public static GameplaySkinDocumentPublicationTargetValidationResult ValidatePublicationTarget(
            GameplaySkinDocumentTarget target,
            GameplaySkinLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(snapshot);

            if (!matchesSelectors(target, snapshot))
                return GameplaySkinDocumentPublicationTargetValidationResult.SelectorNotApplicable;

            if (target.Kind == GameplaySkinDocumentTargetKind.Global)
                return GameplaySkinDocumentPublicationTargetValidationResult.Valid;

            GameplaySkinLaneTopologySnapshot topology = snapshot.Context.Topology;

            if (target.GroupId == null
                || !topology.TryGetGroup(target.GroupId, out GameplaySkinLaneTopologyGroup? group)
                || group == null)
            {
                return GameplaySkinDocumentPublicationTargetValidationResult.UnknownGroup;
            }

            if (target.GroupLogicalIndex != group.LogicalIndex
                || target.GroupVisualIndex != group.VisualIndex)
            {
                return GameplaySkinDocumentPublicationTargetValidationResult.GroupIndexMismatch;
            }

            if (target.Kind is GameplaySkinDocumentTargetKind.Stage or GameplaySkinDocumentTargetKind.Group)
                return GameplaySkinDocumentPublicationTargetValidationResult.Valid;

            if (target.LaneId == null
                || !topology.TryGetLane(target.LaneId, out GameplaySkinLaneTopologyEntry? lane)
                || lane == null)
            {
                return GameplaySkinDocumentPublicationTargetValidationResult.UnknownLane;
            }

            if (!lane.Identity.Group.Id.Equals(group.Identity.Id))
                return GameplaySkinDocumentPublicationTargetValidationResult.LaneGroupMismatch;

            if (target.GlobalLogicalIndex != lane.GlobalLogicalIndex
                || target.GlobalVisualIndex != lane.GlobalVisualIndex
                || target.GroupLocalLogicalIndex != lane.GroupLocalLogicalIndex
                || target.GroupLocalVisualIndex != lane.GroupLocalVisualIndex)
            {
                return GameplaySkinDocumentPublicationTargetValidationResult.LaneIndexMismatch;
            }

            return GameplaySkinDocumentPublicationTargetValidationResult.Valid;
        }

        public static bool IsSelectorApplicable(GameplaySkinDocumentTarget target, GameplaySkinLayoutSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(snapshot);
            return matchesSelectors(target, snapshot);
        }

        public static GameplaySkinSlotApplicabilityResult ValidateDeclaration(
            GameplaySkinSlotDescriptor descriptor,
            GameplaySkinDocumentTarget target)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(target);

            GameplaySkinRulesetApplicability candidateRulesets = target.RulesetSelector switch
            {
                GameplaySkinDocumentRulesetSelector.Any => descriptor.Applicability.Rulesets,
                GameplaySkinDocumentRulesetSelector.Mania => GameplaySkinRulesetApplicability.Mania,
                GameplaySkinDocumentRulesetSelector.Bms => GameplaySkinRulesetApplicability.Bms,
                _ => GameplaySkinRulesetApplicability.None,
            };
            candidateRulesets &= descriptor.Applicability.Rulesets;

            if (candidateRulesets == GameplaySkinRulesetApplicability.None)
                return GameplaySkinSlotApplicabilityResult.UnsupportedRuleset;

            GameplaySkinStageApplicability selectedStage = target.StageModeSelector switch
            {
                GameplaySkinDocumentStageModeSelector.Any => descriptor.Applicability.Stages,
                GameplaySkinDocumentStageModeSelector.Single => GameplaySkinStageApplicability.Single,
                GameplaySkinDocumentStageModeSelector.Dual => GameplaySkinStageApplicability.Dual,
                _ => GameplaySkinStageApplicability.None,
            };

            if ((descriptor.Applicability.Stages & selectedStage) == 0)
                return GameplaySkinSlotApplicabilityResult.UnsupportedStageMode;

            if (target.KeymodeSelector == GameplaySkinDocumentTarget.ANY_KEYMODE)
                return GameplaySkinSlotApplicabilityResult.Applicable;

            foreach (GameplaySkinRulesetApplicability candidate in new[]
                     {
                         GameplaySkinRulesetApplicability.Mania,
                         GameplaySkinRulesetApplicability.Bms,
                     })
            {
                if ((candidateRulesets & candidate) == 0
                    || !tryGetKeymodeToken(
                        target.KeymodeSelector,
                        candidate,
                        out GameplaySkinKeymodeApplicability keymode,
                        out int keyCount,
                        out GameplaySkinStageApplicability keymodeStage))
                {
                    continue;
                }

                if ((descriptor.Applicability.Keymodes & keymode) == 0)
                    continue;

                if (keyCount < descriptor.Applicability.MinimumKeyCount
                    || keyCount > descriptor.Applicability.MaximumKeyCount)
                {
                    continue;
                }

                if (target.StageModeSelector != GameplaySkinDocumentStageModeSelector.Any
                    && (selectedStage & keymodeStage) == 0)
                {
                    continue;
                }

                return GameplaySkinSlotApplicabilityResult.Applicable;
            }

            return GameplaySkinSlotApplicabilityResult.UnsupportedKeymode;
        }

        public static GameplaySkinSlotApplicabilityResult Validate(
            GameplaySkinSlotDescriptor descriptor,
            GameplaySkinLayoutSnapshot snapshot,
            GameplaySkinResolvedMaterialTarget target)
        {
            ArgumentNullException.ThrowIfNull(descriptor);
            ArgumentNullException.ThrowIfNull(snapshot);
            ArgumentNullException.ThrowIfNull(target);

            GameplaySkinLayoutContext context = snapshot.Context;

            if (!target.Matches(context.Topology))
                return GameplaySkinSlotApplicabilityResult.InvalidTarget;

            if (!tryGetRuleset(context.RulesetId, out GameplaySkinRulesetApplicability ruleset)
                || (descriptor.Applicability.Rulesets & ruleset) == 0)
            {
                return GameplaySkinSlotApplicabilityResult.UnsupportedRuleset;
            }

            if (!tryGetKeymode(context, ruleset, out GameplaySkinKeymodeApplicability keymode, out int keyCount)
                || (descriptor.Applicability.Keymodes & keymode) == 0)
            {
                return GameplaySkinSlotApplicabilityResult.UnsupportedKeymode;
            }

            GameplaySkinStageApplicability stageMode = context.Topology.GroupsInLogicalOrder.Count switch
            {
                1 => GameplaySkinStageApplicability.Single,
                2 => GameplaySkinStageApplicability.Dual,
                _ => GameplaySkinStageApplicability.None,
            };

            if (stageMode == GameplaySkinStageApplicability.None
                || (descriptor.Applicability.Stages & stageMode) == 0)
            {
                return GameplaySkinSlotApplicabilityResult.UnsupportedStageMode;
            }

            if (keyCount < descriptor.Applicability.MinimumKeyCount
                || keyCount > descriptor.Applicability.MaximumKeyCount)
            {
                return GameplaySkinSlotApplicabilityResult.UnsupportedKeyCount;
            }

            if (target.Kind == GameplaySkinResolvedMaterialTargetKind.Lane)
            {
                if (target.LaneId == null
                    || !context.Topology.TryGetLane(target.LaneId, out GameplaySkinLaneTopologyEntry? lane)
                    || lane == null
                    || (descriptor.Applicability.LaneRoles & toApplicability(lane.Identity.Role)) == 0)
                {
                    return GameplaySkinSlotApplicabilityResult.UnsupportedLaneRole;
                }
            }

            return GameplaySkinSlotApplicabilityResult.Applicable;
        }

        private static bool matchesSelectors(GameplaySkinDocumentTarget target, GameplaySkinLayoutSnapshot snapshot)
        {
            GameplaySkinLayoutContext context = snapshot.Context;

            if (!tryGetRuleset(context.RulesetId, out GameplaySkinRulesetApplicability ruleset))
                return false;

            if (target.RulesetSelector != GameplaySkinDocumentRulesetSelector.Any
                && target.RulesetSelector != toDocumentSelector(ruleset))
            {
                return false;
            }

            if (target.KeymodeSelector != GameplaySkinDocumentTarget.ANY_KEYMODE
                && !string.Equals(target.KeymodeSelector, context.KeymodeId, StringComparison.Ordinal))
            {
                return false;
            }

            GameplaySkinDocumentStageModeSelector currentStage = context.Topology.GroupsInLogicalOrder.Count switch
            {
                1 => GameplaySkinDocumentStageModeSelector.Single,
                2 => GameplaySkinDocumentStageModeSelector.Dual,
                _ => (GameplaySkinDocumentStageModeSelector)(-1),
            };

            return target.StageModeSelector == GameplaySkinDocumentStageModeSelector.Any
                   || target.StageModeSelector == currentStage;
        }

        private static bool tryGetRuleset(string rulesetId, out GameplaySkinRulesetApplicability ruleset)
        {
            ruleset = rulesetId switch
            {
                "mania" => GameplaySkinRulesetApplicability.Mania,
                "bms" => GameplaySkinRulesetApplicability.Bms,
                _ => GameplaySkinRulesetApplicability.None,
            };

            return ruleset != GameplaySkinRulesetApplicability.None;
        }

        private static bool tryGetKeymode(
            GameplaySkinLayoutContext context,
            GameplaySkinRulesetApplicability ruleset,
            out GameplaySkinKeymodeApplicability keymode,
            out int keyCount)
        {
            if (ruleset == GameplaySkinRulesetApplicability.Mania)
            {
                if (!tryGetKeymodeToken(
                        context.KeymodeId,
                        ruleset,
                        out keymode,
                        out keyCount,
                        out GameplaySkinStageApplicability stage)
                    || keyCount != context.Topology.LanesInLogicalOrder.Count
                    || stage != (context.Topology.GroupsInLogicalOrder.Count == 1
                        ? GameplaySkinStageApplicability.Single
                        : GameplaySkinStageApplicability.Dual))
                {
                    keymode = GameplaySkinKeymodeApplicability.None;
                    keyCount = 0;
                    return false;
                }

                return true;
            }

            return tryGetKeymodeToken(context.KeymodeId, ruleset, out keymode, out keyCount, out _);
        }

        private static bool tryGetKeymodeToken(
            string token,
            GameplaySkinRulesetApplicability ruleset,
            out GameplaySkinKeymodeApplicability keymode,
            out int keyCount,
            out GameplaySkinStageApplicability stage)
        {
            if (ruleset == GameplaySkinRulesetApplicability.Bms)
            {
                (keymode, keyCount, stage) = token switch
                {
                    "5k" => (GameplaySkinKeymodeApplicability.Bms5K, 5, GameplaySkinStageApplicability.Single),
                    "7k" => (GameplaySkinKeymodeApplicability.Bms7K, 7, GameplaySkinStageApplicability.Single),
                    "9k-bms" or "9k-pms" => (GameplaySkinKeymodeApplicability.Bms9K, 9, GameplaySkinStageApplicability.Single),
                    "14k" => (GameplaySkinKeymodeApplicability.Bms14K, 14, GameplaySkinStageApplicability.Dual),
                    _ => (GameplaySkinKeymodeApplicability.None, 0, GameplaySkinStageApplicability.None),
                };

                return keymode != GameplaySkinKeymodeApplicability.None;
            }

            string[] stageTokens = token.Split('-');

            if (stageTokens.Length is < 1 or > 2
                || stageTokens.Any(part => part.Length < 2 || part[^1] != 'k')
                || stageTokens.Any(part => !tryParseCanonicalPositiveInt(part.AsSpan(0, part.Length - 1), out _)))
            {
                keymode = GameplaySkinKeymodeApplicability.None;
                keyCount = 0;
                stage = GameplaySkinStageApplicability.None;
                return false;
            }

            keyCount = stageTokens.Sum(part => int.Parse(part.AsSpan(0, part.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture));
            stage = stageTokens.Length == 1 ? GameplaySkinStageApplicability.Single : GameplaySkinStageApplicability.Dual;
            keymode = GameplaySkinKeymodeApplicability.Mania;

            return keyCount is >= 1 and <= 20;
        }

        private static bool tryParseCanonicalPositiveInt(ReadOnlySpan<char> token, out int value)
        {
            value = 0;

            if (token.IsEmpty || token[0] == '0')
                return false;

            foreach (char character in token)
            {
                if (character is not (>= '0' and <= '9'))
                    return false;

                try
                {
                    value = checked(value * 10 + character - '0');
                }
                catch (OverflowException)
                {
                    return false;
                }
            }

            return true;
        }

        private static GameplaySkinDocumentRulesetSelector toDocumentSelector(GameplaySkinRulesetApplicability ruleset)
            => ruleset switch
            {
                GameplaySkinRulesetApplicability.Mania => GameplaySkinDocumentRulesetSelector.Mania,
                GameplaySkinRulesetApplicability.Bms => GameplaySkinDocumentRulesetSelector.Bms,
                _ => GameplaySkinDocumentRulesetSelector.Any,
            };

        private static GameplaySkinLaneRoleApplicability toApplicability(GameplaySkinLaneRole role)
            => role switch
            {
                GameplaySkinLaneRole.Key => GameplaySkinLaneRoleApplicability.Key,
                GameplaySkinLaneRole.SpecialKey => GameplaySkinLaneRoleApplicability.SpecialKey,
                GameplaySkinLaneRole.Scratch => GameplaySkinLaneRoleApplicability.Scratch,
                _ => GameplaySkinLaneRoleApplicability.None,
            };
    }
}
