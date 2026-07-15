// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// The exact native <c>[Bms]</c> geometry source fields currently consumed by production BMS visuals.
    /// </summary>
    /// <remarks>
    /// This is a source-specific process-local catalog, not a neutral layout descriptor, validation schema or serialisation ABI.
    /// </remarks>
    internal static class BmsGameplaySkinBucketGeometryFieldCatalog
    {
        private static readonly IReadOnlyDictionary<string, BmsSkinConfigurationLookups> exact_fields =
            new ReadOnlyDictionary<string, BmsSkinConfigurationLookups>(
                new Dictionary<string, BmsSkinConfigurationLookups>(StringComparer.Ordinal)
                {
                    [nameof(BmsSkinConfigurationLookups.PlayfieldWidth)] = BmsSkinConfigurationLookups.PlayfieldWidth,
                    [nameof(BmsSkinConfigurationLookups.PlayfieldHeight)] = BmsSkinConfigurationLookups.PlayfieldHeight,
                    [nameof(BmsSkinConfigurationLookups.NormalLaneWidth)] = BmsSkinConfigurationLookups.NormalLaneWidth,
                    [nameof(BmsSkinConfigurationLookups.ScratchLaneWidth)] = BmsSkinConfigurationLookups.ScratchLaneWidth,
                    [nameof(BmsSkinConfigurationLookups.NormalLaneSpacing)] = BmsSkinConfigurationLookups.NormalLaneSpacing,
                    [nameof(BmsSkinConfigurationLookups.ScratchLaneSpacing)] = BmsSkinConfigurationLookups.ScratchLaneSpacing,
                    [nameof(BmsSkinConfigurationLookups.HitTargetHeight)] = BmsSkinConfigurationLookups.HitTargetHeight,
                    [nameof(BmsSkinConfigurationLookups.HitTargetBarHeight)] = BmsSkinConfigurationLookups.HitTargetBarHeight,
                    [nameof(BmsSkinConfigurationLookups.HitTargetLineHeight)] = BmsSkinConfigurationLookups.HitTargetLineHeight,
                    [nameof(BmsSkinConfigurationLookups.HitTargetGlowRadius)] = BmsSkinConfigurationLookups.HitTargetGlowRadius,
                    [nameof(BmsSkinConfigurationLookups.BarLineHeight)] = BmsSkinConfigurationLookups.BarLineHeight,
                    [nameof(BmsSkinConfigurationLookups.LongNoteBodyWidth)] = BmsSkinConfigurationLookups.LongNoteBodyWidth,
                });

        public static IReadOnlyList<BmsSkinConfigurationLookups> All { get; } = Array.AsReadOnly(new[]
        {
            BmsSkinConfigurationLookups.PlayfieldWidth,
            BmsSkinConfigurationLookups.PlayfieldHeight,
            BmsSkinConfigurationLookups.NormalLaneWidth,
            BmsSkinConfigurationLookups.ScratchLaneWidth,
            BmsSkinConfigurationLookups.NormalLaneSpacing,
            BmsSkinConfigurationLookups.ScratchLaneSpacing,
            BmsSkinConfigurationLookups.HitTargetHeight,
            BmsSkinConfigurationLookups.HitTargetBarHeight,
            BmsSkinConfigurationLookups.HitTargetLineHeight,
            BmsSkinConfigurationLookups.HitTargetGlowRadius,
            BmsSkinConfigurationLookups.BarLineHeight,
            BmsSkinConfigurationLookups.LongNoteBodyWidth,
        });

        public static bool IsCanonical(BmsSkinConfigurationLookups field)
            => field is BmsSkinConfigurationLookups.PlayfieldWidth
                or BmsSkinConfigurationLookups.PlayfieldHeight
                or BmsSkinConfigurationLookups.NormalLaneWidth
                or BmsSkinConfigurationLookups.ScratchLaneWidth
                or BmsSkinConfigurationLookups.NormalLaneSpacing
                or BmsSkinConfigurationLookups.ScratchLaneSpacing
                or BmsSkinConfigurationLookups.HitTargetHeight
                or BmsSkinConfigurationLookups.HitTargetBarHeight
                or BmsSkinConfigurationLookups.HitTargetLineHeight
                or BmsSkinConfigurationLookups.HitTargetGlowRadius
                or BmsSkinConfigurationLookups.BarLineHeight
                or BmsSkinConfigurationLookups.LongNoteBodyWidth;

        public static bool TryGetExact(string sourceKey, out BmsSkinConfigurationLookups field)
        {
            ArgumentNullException.ThrowIfNull(sourceKey);
            return exact_fields.TryGetValue(sourceKey, out field);
        }

        public static void Validate(BmsSkinConfigurationLookups field, string parameterName)
        {
            if (!IsCanonical(field))
                throw new ArgumentOutOfRangeException(parameterName, field, "The field is not a canonical native BMS geometry source field.");
        }
    }
}
