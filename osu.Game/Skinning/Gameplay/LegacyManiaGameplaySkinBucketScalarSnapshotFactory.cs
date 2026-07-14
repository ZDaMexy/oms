// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects scalar declaration provenance from one actual legacy mania decoder bucket.
    /// </summary>
    /// <remarks>
    /// This adapter never queries <see cref="LegacySkin"/>, because that path synthesises default configurations for missing
    /// <c>Keys:</c> buckets. It records only fields successfully accepted by <see cref="LegacyManiaSkinDecoder"/> and does not
    /// infer declaration from mutable public values on <see cref="LegacyManiaSkinConfiguration"/>. Accepted values are captured
    /// by the decoder before this factory runs, so later native configuration mutation cannot alter their provenance. The type is
    /// public only as a cross-ruleset CLR bridge; it is not a stable plugin, package, manifest or script API.
    /// </remarks>
    public static class LegacyManiaGameplaySkinBucketScalarSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketScalarSnapshot> Create(
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
                return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketScalarSnapshot>.Absent;

            return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketScalarSnapshot>.Declared(
                LegacyManiaGameplaySkinBucketScalarSnapshot.Create(
                    totalColumns,
                    source.AcceptedWidthForNoteHeightScale,
                    source.AcceptedHitPosition,
                    source.AcceptedLightPosition,
                    source.AcceptedComboPosition,
                    source.AcceptedScorePosition,
                    source.AcceptedBarLineHeight,
                    source.AcceptedShowJudgementLine,
                    source.AcceptedKeysUnderNotes,
                    source.AcceptedLightFramePerSecond));
        }
    }
}
