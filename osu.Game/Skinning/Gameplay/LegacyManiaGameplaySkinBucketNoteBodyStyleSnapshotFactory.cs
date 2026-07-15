// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// Projects note-body style declaration provenance from one actual legacy mania decoder bucket.
    /// </summary>
    /// <remarks>
    /// This adapter never queries <see cref="LegacySkin"/>, because that path synthesises default configurations for missing
    /// <c>Keys:</c> buckets and derives an effective style from the global legacy skin version. It also never infers declaration
    /// from the mutable public <see cref="LegacyManiaSkinConfiguration.NoteBodyStyle"/> compatibility field. The decoder-time
    /// sidecar remains authoritative for this source-specific process-local snapshot.
    /// </remarks>
    public static class LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot> Create(
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
                return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot>.Absent;

            return GameplaySkinConfigurationDeclaration<LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot>.Declared(
                LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot.Create(
                    totalColumns,
                    source.AcceptedNoteBodyStyle));
        }
    }
}
