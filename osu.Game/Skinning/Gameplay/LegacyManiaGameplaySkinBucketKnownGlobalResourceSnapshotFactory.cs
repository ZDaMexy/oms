// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects thirteen exact known global image declarations from one actual legacy mania decoder bucket.
    /// </summary>
    /// <remarks>
    /// The adapter never queries <see cref="LegacySkin"/> and never infers declarations from the mutable
    /// <see cref="LegacyManiaSkinConfiguration.ImageLookups"/> compatibility dictionary. Accepted strings are captured at
    /// decoder time, so later dictionary replacement or mutation cannot forge, erase or alter provenance. Arbitrary
    /// <c>Hit*</c>, <c>Stage*</c> and <c>Lighting*</c> keys remain deliberately outside this closed carrier.
    /// </remarks>
    public static class LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot> Create(
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
                return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot>.Absent;

            return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot>.Declared(
                LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot.Create(
                    totalColumns,
                    source.AcceptedLightingNResource,
                    source.AcceptedLightingLResource,
                    source.AcceptedStageLeftResource,
                    source.AcceptedStageRightResource,
                    source.AcceptedStageBottomResource,
                    source.AcceptedStageLightResource,
                    source.AcceptedStageHintResource,
                    source.AcceptedHit0Resource,
                    source.AcceptedHit50Resource,
                    source.AcceptedHit100Resource,
                    source.AcceptedHit200Resource,
                    source.AcceptedHit300Resource,
                    source.AcceptedHit300gResource));
        }
    }
}
