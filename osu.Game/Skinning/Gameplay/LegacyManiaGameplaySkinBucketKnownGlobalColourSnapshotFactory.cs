// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects four exact known global colour declarations from one actual legacy mania decoder bucket.
    /// </summary>
    /// <remarks>
    /// The adapter never queries <see cref="LegacySkin"/> and never infers declarations from the mutable
    /// <see cref="LegacyManiaSkinConfiguration.CustomColours"/> compatibility dictionary. Accepted values are captured directly
    /// after successful legacy colour parsing, so later dictionary mutation cannot forge or alter provenance. Arbitrary and
    /// per-column <c>Colour*</c> keys are deliberately outside this closed source-specific carrier.
    /// </remarks>
    public static class LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot> Create(
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
                return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot>.Absent;

            return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot>.Declared(
                LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot.Create(
                    totalColumns,
                    source.AcceptedColumnLineColour,
                    source.AcceptedJudgementLineColour,
                    source.AcceptedComboBreakColour,
                    source.AcceptedBarLineColour));
        }
    }
}
