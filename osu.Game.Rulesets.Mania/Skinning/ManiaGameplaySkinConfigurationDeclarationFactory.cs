// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Skinning;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Mania.Skinning
{
    /// <summary>
    /// Projects bucket declaration provenance from the real legacy mania decoder output.
    /// </summary>
    /// <remarks>
    /// This intentionally does not query <see cref="LegacySkin"/> because its compatibility lookup synthesises
    /// a default <see cref="LegacyManiaSkinConfiguration"/> when the requested <c>Keys:</c> bucket is absent.
    /// It does not expose the decoder's mutable configuration as neutral contract data.
    /// </remarks>
    internal static class ManiaGameplaySkinConfigurationDeclarationFactory
    {
        public static GameplaySkinConfigurationDeclaration<int> FindDeclaredBucket(
            IReadOnlyList<LegacyManiaSkinConfiguration> decodedConfigurations,
            int totalColumns)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);

            if (!isSupportedTotalColumns(totalColumns))
                throw new ArgumentOutOfRangeException(nameof(totalColumns), totalColumns, "The requested mania key count is outside the supported total-column range.");

            bool found = false;

            foreach (LegacyManiaSkinConfiguration configuration in decodedConfigurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Decoded mania configurations cannot contain null entries.", nameof(decodedConfigurations));

                if (configuration.Keys != totalColumns)
                    continue;

                if (found)
                    throw new ArgumentException("Decoded mania configurations cannot contain duplicate key-count buckets.", nameof(decodedConfigurations));

                found = true;
            }

            return found
                ? GameplaySkinConfigurationDeclaration<int>.Declared(totalColumns)
                : GameplaySkinConfigurationDeclaration<int>.Absent;
        }

        private static bool isSupportedTotalColumns(int totalColumns)
            => totalColumns is >= 1 and <= ManiaRuleset.MAX_STAGE_KEYS * 2;
    }
}
