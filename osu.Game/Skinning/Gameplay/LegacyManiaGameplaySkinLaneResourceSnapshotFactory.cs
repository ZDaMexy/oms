// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects one actual <see cref="LegacyManiaSkinDecoder"/> bucket into neutral lane-resource declarations.
    /// </summary>
    /// <remarks>
    /// This adapter never queries <see cref="LegacySkin"/>, because that lookup path synthesises default configurations
    /// for missing <c>Keys:</c> buckets. The supplied lane-to-column map is copied into declarations for a target topology,
    /// allowing both native mania and BMS compatibility projections to use the same legacy field semantics.
    /// Only decoder-time accepted sidecars are read; later mutation of the public compatibility image dictionary cannot
    /// forge, erase or alter a declaration.
    /// The type is public only as a cross-ruleset CLR bridge; it is not a stable plugin, package, manifest or script API.
    /// </remarks>
    public static class LegacyManiaGameplaySkinLaneResourceSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot> Create(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            int totalColumns,
            GameplaySkinLaneTopologySnapshot targetTopology,
            IReadOnlyDictionary<GameplaySkinLaneId, int> targetLaneColumns)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);
            ArgumentNullException.ThrowIfNull(targetTopology);
            ArgumentNullException.ThrowIfNull(targetLaneColumns);

            if (totalColumns <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalColumns), totalColumns, "A legacy mania configuration bucket must contain at least one column.");

            foreach (KeyValuePair<GameplaySkinLaneId, int> mapping in targetLaneColumns)
            {
                if (mapping.Key == null)
                    throw new ArgumentException("A legacy mania lane-to-column map cannot contain a null lane ID.", nameof(targetLaneColumns));

                if (!targetTopology.TryGetLane(mapping.Key, out _))
                    throw new ArgumentException("Every mapped target lane must belong to the supplied topology.", nameof(targetLaneColumns));

                if (mapping.Value < 0 || mapping.Value >= totalColumns)
                    throw new ArgumentOutOfRangeException(nameof(targetLaneColumns), mapping.Value, "A mapped legacy mania column is outside its Keys bucket.");
            }

            LegacyManiaSkinConfiguration? source = null;

            foreach (LegacyManiaSkinConfiguration configuration in decodedConfigurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Decoded legacy mania configurations cannot contain null entries.", nameof(decodedConfigurations));

                if (configuration.Keys != totalColumns)
                    continue;

                if (source != null)
                    throw new ArgumentException("Decoded legacy mania configurations cannot contain duplicate key-count buckets.", nameof(decodedConfigurations));

                source = configuration;
            }

            if (source == null)
                return GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Absent;

            var declarations = new List<GameplaySkinLaneResourceDeclaration>();

            foreach (KeyValuePair<GameplaySkinLaneId, int> mapping in targetLaneColumns)
            {
                foreach (GameplaySkinLaneResourceField field in GameplaySkinLaneResourceFieldCatalog.All)
                {
                    GameplaySkinConfigurationDeclaration<string> declaration = source.GetAcceptedLaneResource(
                        getLegacyField(field),
                        mapping.Value);

                    if (!declaration.TryGetValue(out string? resourceName))
                        continue;

                    declarations.Add(GameplaySkinLaneResourceDeclaration.Create(mapping.Key, field, resourceName));
                }
            }

            return GameplaySkinConfigurationDeclaration<GameplaySkinLaneResourceSnapshot>.Declared(
                GameplaySkinLaneResourceSnapshot.Create(targetTopology, declarations));
        }

        internal static string GetImageLookupKey(GameplaySkinLaneResourceField field, string laneToken)
        {
            ArgumentNullException.ThrowIfNull(field);
            ArgumentException.ThrowIfNullOrEmpty(laneToken);

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Note))
                return $"NoteImage{laneToken}";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteHead))
                return $"NoteImage{laneToken}H";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteBody))
                return $"NoteImage{laneToken}L";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteTail))
                return $"NoteImage{laneToken}T";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Key))
                return $"KeyImage{laneToken}";

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.KeyPressed))
                return $"KeyImage{laneToken}D";

            throw new ArgumentException("The requested field is not a legacy mania lane resource.", nameof(field));
        }

        private static LegacyManiaSkinLaneResourceField getLegacyField(GameplaySkinLaneResourceField field)
        {
            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Note))
                return LegacyManiaSkinLaneResourceField.Note;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteHead))
                return LegacyManiaSkinLaneResourceField.LongNoteHead;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteBody))
                return LegacyManiaSkinLaneResourceField.LongNoteBody;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.LongNoteTail))
                return LegacyManiaSkinLaneResourceField.LongNoteTail;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.Key))
                return LegacyManiaSkinLaneResourceField.Key;

            if (ReferenceEquals(field, GameplaySkinLaneResourceFieldCatalog.KeyPressed))
                return LegacyManiaSkinLaneResourceField.KeyPressed;

            throw new ArgumentException("The requested field is not a legacy mania lane resource.", nameof(field));
        }
    }
}
