// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Stable reason why a declared BMS scalar geometry value was replaced by its field default.
    /// </summary>
    internal enum BmsGameplaySkinScalarGeometryRejectionReason
    {
        DeclarationAbsent = 0,
        NonFinite = 1,
        AtOrBelowMinimum = 2,
        AboveMaximum = 3,
    }

    /// <summary>
    /// One fieldwise-resolved BMS scalar geometry value.
    /// </summary>
    internal readonly record struct BmsGameplaySkinScalarGeometryResolution(
        float Value,
        BmsGameplaySkinScalarGeometryRejectionReason? RejectionReason)
    {
        public bool UsedDefault => RejectionReason.HasValue;
    }

    /// <summary>
    /// The single validation and defaulting policy for native BMS scalar geometry declarations.
    /// </summary>
    /// <remarks>
    /// This is deliberately fieldwise and source-local. Screen-space and cross-field layout validation belong to the
    /// future resolved layout snapshot, not to this scalar gate.
    /// </remarks>
    internal static class BmsGameplaySkinScalarGeometryResolver
    {
        internal const float DEFAULT_LONG_NOTE_BODY_WIDTH = 0.5775f;

        public static BmsGameplaySkinScalarGeometryResolution Resolve(
            BmsSkinConfigurationLookups field,
            GameplaySkinConfigurationDeclaration<float> declaration)
        {
            if (field != BmsSkinConfigurationLookups.LongNoteBodyWidth)
                throw new ArgumentOutOfRangeException(nameof(field), field, "The field has no BMS scalar geometry policy.");

            if (!declaration.TryGetValue(out float value))
                return useDefault(BmsGameplaySkinScalarGeometryRejectionReason.DeclarationAbsent);

            if (!float.IsFinite(value))
                return useDefault(BmsGameplaySkinScalarGeometryRejectionReason.NonFinite);

            if (value <= 0)
                return useDefault(BmsGameplaySkinScalarGeometryRejectionReason.AtOrBelowMinimum);

            if (value > 1)
                return useDefault(BmsGameplaySkinScalarGeometryRejectionReason.AboveMaximum);

            return new BmsGameplaySkinScalarGeometryResolution(value, null);
        }

        private static BmsGameplaySkinScalarGeometryResolution useDefault(
            BmsGameplaySkinScalarGeometryRejectionReason reason)
            => new(DEFAULT_LONG_NOTE_BODY_WIDTH, reason);
    }
}
