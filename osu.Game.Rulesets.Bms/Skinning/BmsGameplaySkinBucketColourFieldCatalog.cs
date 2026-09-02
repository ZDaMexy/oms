// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// The exact native <c>[Bms]</c> colour source fields currently consumed by production BMS visuals.
    /// </summary>
    /// <remarks>
    /// This is a source-specific process-local catalog, not a neutral slot, author manifest or serialisation ABI.
    /// </remarks>
    internal static class BmsGameplaySkinBucketColourFieldCatalog
    {
        private static readonly IReadOnlyDictionary<string, BmsSkinConfigurationLookups> exact_fields =
            new ReadOnlyDictionary<string, BmsSkinConfigurationLookups>(
                new Dictionary<string, BmsSkinConfigurationLookups>(StringComparer.Ordinal)
                {
                    [nameof(BmsSkinConfigurationLookups.NoteColourWhite)] = BmsSkinConfigurationLookups.NoteColourWhite,
                    [nameof(BmsSkinConfigurationLookups.NoteColourCyan)] = BmsSkinConfigurationLookups.NoteColourCyan,
                    [nameof(BmsSkinConfigurationLookups.NoteColourYellow)] = BmsSkinConfigurationLookups.NoteColourYellow,
                    [nameof(BmsSkinConfigurationLookups.NoteColourScratch)] = BmsSkinConfigurationLookups.NoteColourScratch,
                    [nameof(BmsSkinConfigurationLookups.LaneBackgroundEvenColour)] = BmsSkinConfigurationLookups.LaneBackgroundEvenColour,
                    [nameof(BmsSkinConfigurationLookups.LaneBackgroundOddColour)] = BmsSkinConfigurationLookups.LaneBackgroundOddColour,
                    [nameof(BmsSkinConfigurationLookups.ScratchLaneBackgroundColour)] = BmsSkinConfigurationLookups.ScratchLaneBackgroundColour,
                    [nameof(BmsSkinConfigurationLookups.LaneDividerColour)] = BmsSkinConfigurationLookups.LaneDividerColour,
                    [nameof(BmsSkinConfigurationLookups.ScratchLaneDividerColour)] = BmsSkinConfigurationLookups.ScratchLaneDividerColour,
                    [nameof(BmsSkinConfigurationLookups.HitTargetBarColour)] = BmsSkinConfigurationLookups.HitTargetBarColour,
                    [nameof(BmsSkinConfigurationLookups.HitTargetLineColour)] = BmsSkinConfigurationLookups.HitTargetLineColour,
                    [nameof(BmsSkinConfigurationLookups.HitTargetGlowColour)] = BmsSkinConfigurationLookups.HitTargetGlowColour,
                    [nameof(BmsSkinConfigurationLookups.ScratchHitTargetBarColour)] = BmsSkinConfigurationLookups.ScratchHitTargetBarColour,
                    [nameof(BmsSkinConfigurationLookups.ScratchHitTargetLineColour)] = BmsSkinConfigurationLookups.ScratchHitTargetLineColour,
                    [nameof(BmsSkinConfigurationLookups.ScratchHitTargetGlowColour)] = BmsSkinConfigurationLookups.ScratchHitTargetGlowColour,
                    [nameof(BmsSkinConfigurationLookups.MajorBarLineColour)] = BmsSkinConfigurationLookups.MajorBarLineColour,
                    [nameof(BmsSkinConfigurationLookups.MinorBarLineColour)] = BmsSkinConfigurationLookups.MinorBarLineColour,
                    [nameof(BmsSkinConfigurationLookups.LaneCoverFillColour)] = BmsSkinConfigurationLookups.LaneCoverFillColour,
                    [nameof(BmsSkinConfigurationLookups.LaneCoverShadeColour)] = BmsSkinConfigurationLookups.LaneCoverShadeColour,
                    [nameof(BmsSkinConfigurationLookups.LaneCoverFocusColour)] = BmsSkinConfigurationLookups.LaneCoverFocusColour,
                    [nameof(BmsSkinConfigurationLookups.PlayfieldBackdropColour)] = BmsSkinConfigurationLookups.PlayfieldBackdropColour,
                    [nameof(BmsSkinConfigurationLookups.PlayfieldBaseplateColour)] = BmsSkinConfigurationLookups.PlayfieldBaseplateColour,
                });

        public static bool IsCanonical(BmsSkinConfigurationLookups field)
            => field is BmsSkinConfigurationLookups.NoteColourWhite
                or BmsSkinConfigurationLookups.NoteColourCyan
                or BmsSkinConfigurationLookups.NoteColourYellow
                or BmsSkinConfigurationLookups.NoteColourScratch
                or BmsSkinConfigurationLookups.LaneBackgroundEvenColour
                or BmsSkinConfigurationLookups.LaneBackgroundOddColour
                or BmsSkinConfigurationLookups.ScratchLaneBackgroundColour
                or BmsSkinConfigurationLookups.LaneDividerColour
                or BmsSkinConfigurationLookups.ScratchLaneDividerColour
                or BmsSkinConfigurationLookups.HitTargetBarColour
                or BmsSkinConfigurationLookups.HitTargetLineColour
                or BmsSkinConfigurationLookups.HitTargetGlowColour
                or BmsSkinConfigurationLookups.ScratchHitTargetBarColour
                or BmsSkinConfigurationLookups.ScratchHitTargetLineColour
                or BmsSkinConfigurationLookups.ScratchHitTargetGlowColour
                or BmsSkinConfigurationLookups.MajorBarLineColour
                or BmsSkinConfigurationLookups.MinorBarLineColour
                or BmsSkinConfigurationLookups.LaneCoverFillColour
                or BmsSkinConfigurationLookups.LaneCoverShadeColour
                or BmsSkinConfigurationLookups.LaneCoverFocusColour
                or BmsSkinConfigurationLookups.PlayfieldBackdropColour
                or BmsSkinConfigurationLookups.PlayfieldBaseplateColour;

        public static bool TryGetExact(string sourceKey, out BmsSkinConfigurationLookups field)
        {
            ArgumentNullException.ThrowIfNull(sourceKey);
            return exact_fields.TryGetValue(sourceKey, out field);
        }

        public static void Validate(BmsSkinConfigurationLookups field, string parameterName)
        {
            if (!IsCanonical(field))
                throw new ArgumentOutOfRangeException(parameterName, field, "The field is not a canonical native BMS colour source field.");
        }
    }
}
