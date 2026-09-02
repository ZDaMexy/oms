// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// The complete versioned public gameplay-skin slot catalog shared by mania and BMS.
    /// </summary>
    /// <remarks>
    /// Catalog order is deterministic for generated documentation and diagnostics; it has no render or z-order meaning.
    /// Current renderer support is deliberately not part of these descriptors.
    /// </remarks>
    public static class GameplaySkinSlotCatalog
    {
        public const int COMMON_VERSION = 1;
        public const int BMS_EXTENSION_VERSION = 1;
        public const string CONTRACT_ID = "oms-gameplay-skin-catalog.v1";

        private const GameplaySkinRulesetApplicability common_rulesets = GameplaySkinRulesetApplicability.Mania | GameplaySkinRulesetApplicability.Bms;
        private const GameplaySkinStageApplicability all_stages = GameplaySkinStageApplicability.Single | GameplaySkinStageApplicability.Dual;
        private const GameplaySkinLaneRoleApplicability all_lane_roles =
            GameplaySkinLaneRoleApplicability.Key | GameplaySkinLaneRoleApplicability.SpecialKey | GameplaySkinLaneRoleApplicability.Scratch;
        private const GameplaySkinKeymodeApplicability all_keymodes =
            GameplaySkinKeymodeApplicability.Mania | GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K
            | GameplaySkinKeymodeApplicability.Bms9K | GameplaySkinKeymodeApplicability.Bms14K;

        public static GameplaySkinSlotDescriptor LaneSurface { get; } = required(
            "playfield.lane-surface", "LaneSurface", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-001");

        public static GameplaySkinSlotDescriptor JudgementLine { get; } = required(
            "playfield.judgement-line", "JudgementLine", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-002");

        public static GameplaySkinSlotDescriptor Note { get; } = required(
            "object.note", "Note", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-003");

        public static GameplaySkinSlotDescriptor LongNoteHead { get; } = required(
            "object.long-note.head", "LongNoteHead", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-004");

        public static GameplaySkinSlotDescriptor LongNoteBody { get; } = required(
            "object.long-note.body", "LongNoteBody", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-005");

        public static GameplaySkinSlotDescriptor Mine { get; } = required(
            "object.mine", "Mine", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-006");

        public static GameplaySkinSlotDescriptor LaneCoverFill { get; } = required(
            "playfield.lane-cover.fill", "LaneCoverFill", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-007");

        public static GameplaySkinSlotDescriptor LongNoteTail { get; } = optional(
            "object.long-note.tail", "LongNoteTail", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-008");

        public static GameplaySkinSlotDescriptor KeyVisual { get; } = optional(
            "playfield.key", "KeyVisual", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-009");

        public static GameplaySkinSlotDescriptor KeyFlash { get; } = optional(
            "effect.key-flash", "KeyFlash", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-010");

        public static GameplaySkinSlotDescriptor HitExplosion { get; } = optional(
            "effect.hit-explosion", "HitExplosion", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-011");

        public static GameplaySkinSlotDescriptor JudgementDisplay { get; } = optional(
            "hud.judgement", "JudgementDisplay", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-012");

        public static GameplaySkinSlotDescriptor ComboDisplay { get; } = optional(
            "hud.combo", "ComboDisplay", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-013");

        public static GameplaySkinSlotDescriptor GaugeVisual { get; } = optional(
            "hud.gauge", "GaugeVisual", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-014");

        public static GameplaySkinSlotDescriptor TextHud { get; } = optional(
            "hud.text", "TextHud", GameplaySkinSlotScope.Global | GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-015");

        public static GameplaySkinSlotDescriptor BarLine { get; } = recommended(
            "playfield.bar-line", "BarLine", GameplaySkinSlotScope.Group, commonNonLane(), "OMS-SKIN-SLOT-016");

        public static GameplaySkinSlotDescriptor StageBackground { get; } = recommended(
            "stage.background", "StageBackground", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-017");

        public static GameplaySkinSlotDescriptor StageForeground { get; } = recommended(
            "stage.foreground", "StageForeground", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-018");

        public static GameplaySkinSlotDescriptor PlayfieldBackdrop { get; } = recommended(
            "playfield.backdrop", "PlayfieldBackdrop", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-019");

        public static GameplaySkinSlotDescriptor PlayfieldBaseplate { get; } = recommended(
            "playfield.baseplate", "PlayfieldBaseplate", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-020");

        public static GameplaySkinSlotDescriptor LaneCoverDecoration { get; } = optional(
            "playfield.lane-cover.decoration", "LaneCoverDecoration", GameplaySkinSlotScope.Stage, commonNonLane(), "OMS-SKIN-SLOT-021");

        public static GameplaySkinSlotDescriptor Turntable { get; } = bmsOptional(
            "playfield.turntable", "Turntable", GameplaySkinSlotScope.Lane, bmsScratchLane(), "OMS-SKIN-SLOT-022");

        public static GameplaySkinSlotDescriptor Laser { get; } = bmsOptional(
            "playfield.laser", "Laser", GameplaySkinSlotScope.Lane, bmsScratchLane(), "OMS-SKIN-SLOT-023");

        public static GameplaySkinSlotDescriptor BgaViewport { get; } = optional(
            "bga.viewport", "BgaViewport", GameplaySkinSlotScope.Global, commonNonLane(), "OMS-SKIN-SLOT-024");

        public static GameplaySkinSlotDescriptor BgaFrame { get; } = optional(
            "bga.frame", "BgaFrame", GameplaySkinSlotScope.Global, commonNonLane(), "OMS-SKIN-SLOT-025");

        public static GameplaySkinSlotDescriptor Decoration { get; } = optional(
            "decoration", "Decoration", GameplaySkinSlotScope.Global | GameplaySkinSlotScope.Stage | GameplaySkinSlotScope.Group | GameplaySkinSlotScope.Lane,
            commonLane(), "OMS-SKIN-SLOT-026");

        public static GameplaySkinSlotDescriptor HitTarget { get; } = recommended(
            "playfield.hit-target", "HitTarget", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-027");

        public static GameplaySkinSlotDescriptor LaneDivider { get; } = recommended(
            "playfield.lane-divider", "LaneDivider", GameplaySkinSlotScope.Lane, commonLane(), "OMS-SKIN-SLOT-028");

        public static IReadOnlyList<GameplaySkinSlotDescriptor> All { get; } = Array.AsReadOnly(new[]
        {
            LaneSurface,
            JudgementLine,
            Note,
            LongNoteHead,
            LongNoteBody,
            Mine,
            LaneCoverFill,
            LongNoteTail,
            KeyVisual,
            KeyFlash,
            HitExplosion,
            JudgementDisplay,
            ComboDisplay,
            GaugeVisual,
            TextHud,
            BarLine,
            StageBackground,
            StageForeground,
            PlayfieldBackdrop,
            PlayfieldBaseplate,
            LaneCoverDecoration,
            Turntable,
            Laser,
            BgaViewport,
            BgaFrame,
            Decoration,
            HitTarget,
            LaneDivider,
        });

        public static IReadOnlyList<GameplaySkinSlotDescriptor> Common { get; } = Array.AsReadOnly(All.Where(slot => slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Common).ToArray());

        public static IReadOnlyList<GameplaySkinSlotDescriptor> BmsExtension { get; } = Array.AsReadOnly(All.Where(slot => slot.CatalogFamily == GameplaySkinSlotCatalogFamily.Bms).ToArray());

        private static readonly IReadOnlyDictionary<string, GameplaySkinSlotDescriptor> by_id =
            All.ToDictionary(slot => slot.Id, StringComparer.Ordinal);

        public static bool TryGet(string? id, [NotNullWhen(true)] out GameplaySkinSlotDescriptor? descriptor)
        {
            if (id != null && by_id.TryGetValue(id, out GameplaySkinSlotDescriptor? found))
            {
                descriptor = found;
                return true;
            }

            descriptor = null;
            return false;
        }

        public static bool IsSupportedVersion(GameplaySkinSlotCatalogFamily family, int version)
            => family switch
            {
                GameplaySkinSlotCatalogFamily.Common => version == COMMON_VERSION,
                GameplaySkinSlotCatalogFamily.Bms => version == BMS_EXTENSION_VERSION,
                _ => false,
            };

        private static GameplaySkinSlotDescriptor required(
            string id,
            string stableName,
            GameplaySkinSlotScope scope,
            GameplaySkinSlotApplicability applicability,
            string diagnosticId)
            => create(id, stableName, GameplaySkinSlotCatalogFamily.Common, COMMON_VERSION, scope,
                GameplaySkinSlotClassification.Required, GameplaySkinSlotSuppressEligibility.Forbidden, applicability, diagnosticId);

        private static GameplaySkinSlotDescriptor recommended(
            string id,
            string stableName,
            GameplaySkinSlotScope scope,
            GameplaySkinSlotApplicability applicability,
            string diagnosticId)
            => create(id, stableName, GameplaySkinSlotCatalogFamily.Common, COMMON_VERSION, scope,
                GameplaySkinSlotClassification.Recommended, GameplaySkinSlotSuppressEligibility.Forbidden, applicability, diagnosticId);

        private static GameplaySkinSlotDescriptor optional(
            string id,
            string stableName,
            GameplaySkinSlotScope scope,
            GameplaySkinSlotApplicability applicability,
            string diagnosticId)
            => create(id, stableName, GameplaySkinSlotCatalogFamily.Common, COMMON_VERSION, scope,
                GameplaySkinSlotClassification.Optional, GameplaySkinSlotSuppressEligibility.Allowed, applicability, diagnosticId);

        private static GameplaySkinSlotDescriptor bmsOptional(
            string id,
            string stableName,
            GameplaySkinSlotScope scope,
            GameplaySkinSlotApplicability applicability,
            string diagnosticId)
            => create(id, stableName, GameplaySkinSlotCatalogFamily.Bms, BMS_EXTENSION_VERSION, scope,
                GameplaySkinSlotClassification.Optional, GameplaySkinSlotSuppressEligibility.Allowed, applicability, diagnosticId);

        private static GameplaySkinSlotDescriptor create(
            string id,
            string stableName,
            GameplaySkinSlotCatalogFamily family,
            int version,
            GameplaySkinSlotScope scope,
            GameplaySkinSlotClassification classification,
            GameplaySkinSlotSuppressEligibility suppressEligibility,
            GameplaySkinSlotApplicability applicability,
            string diagnosticId)
            => new GameplaySkinSlotDescriptor(
                id,
                stableName,
                family,
                version,
                scope,
                GameplaySkinSlotValueType.Resource,
                classification,
                GameplaySkinSlotDefaultSemantics.InheritToLowerAuthorityThenCanonicalFallback,
                suppressEligibility,
                applicability,
                diagnosticId);

        private static GameplaySkinSlotApplicability commonLane()
            => new GameplaySkinSlotApplicability(common_rulesets, all_stages, all_lane_roles, all_keymodes, 1, 20);

        private static GameplaySkinSlotApplicability commonNonLane()
            => new GameplaySkinSlotApplicability(common_rulesets, all_stages, GameplaySkinLaneRoleApplicability.None, all_keymodes, 1, 20);

        private static GameplaySkinSlotApplicability bmsScratchLane()
            => new GameplaySkinSlotApplicability(
                GameplaySkinRulesetApplicability.Bms,
                all_stages,
                GameplaySkinLaneRoleApplicability.Scratch,
                GameplaySkinKeymodeApplicability.Bms5K | GameplaySkinKeymodeApplicability.Bms7K | GameplaySkinKeymodeApplicability.Bms14K,
                5,
                14);
    }
}
