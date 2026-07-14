// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Projects bucket declaration provenance from the real BMS decoder output without exposing its mutable configuration.
    /// </summary>
    internal static class BmsGameplaySkinConfigurationDeclarationFactory
    {
        public static GameplaySkinConfigurationDeclaration<BmsKeymode> FindDeclaredBucket(
            IReadOnlyList<BmsSkinConfiguration> decodedConfigurations,
            BmsKeymode keymode)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);

            if (!isSupportedKeymode(keymode))
                throw new ArgumentOutOfRangeException(nameof(keymode), keymode, "The requested BMS keymode is not supported.");

            bool found = false;

            foreach (BmsSkinConfiguration configuration in decodedConfigurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Decoded BMS configurations cannot contain null entries.", nameof(decodedConfigurations));

                if (configuration.Keymode != keymode)
                    continue;

                if (found)
                    throw new ArgumentException("Decoded BMS configurations cannot contain duplicate keymode buckets.", nameof(decodedConfigurations));

                found = true;
            }

            return found
                ? GameplaySkinConfigurationDeclaration<BmsKeymode>.Declared(keymode)
                : GameplaySkinConfigurationDeclaration<BmsKeymode>.Absent;
        }

        private static bool isSupportedKeymode(BmsKeymode keymode)
            => keymode is BmsKeymode.Key5K
                or BmsKeymode.Key7K
                or BmsKeymode.Key9K_Bms
                or BmsKeymode.Key9K_Pms
                or BmsKeymode.Key14K;
    }
}
