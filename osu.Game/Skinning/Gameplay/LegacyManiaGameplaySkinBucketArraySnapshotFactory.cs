// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects indexed array declaration provenance from one actual legacy mania decoder bucket.
    /// </summary>
    /// <remarks>
    /// This adapter never queries <see cref="LegacySkin"/>, because that path synthesises default configurations for missing
    /// <c>Keys:</c> buckets. Accepted values are copied by <see cref="LegacyManiaSkinDecoder"/> before this factory runs, so
    /// mutable native arrays cannot forge declarations or alter their values. The type is public only as a cross-ruleset CLR
    /// bridge; it is not a stable plugin, package, manifest or script API.
    /// </remarks>
    public static class LegacyManiaGameplaySkinBucketArraySnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketArraySnapshot> Create(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            int totalColumns)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);

            if (totalColumns <= 0)
                throw new ArgumentOutOfRangeException(nameof(totalColumns), totalColumns, "A legacy mania configuration bucket must contain at least one column.");

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
                return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketArraySnapshot>.Absent;

            return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketArraySnapshot>.Declared(
                LegacyManiaGameplaySkinBucketArraySnapshot.Create(
                    totalColumns,
                    source.CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField.ColumnLineWidth),
                    source.CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField.ColumnSpacing),
                    source.CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField.ColumnWidth),
                    source.CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField.ExplosionWidth),
                    source.CopyAcceptedArrayDeclarations(LegacyManiaSkinArrayField.HoldNoteLightWidth)));
        }
    }
}
