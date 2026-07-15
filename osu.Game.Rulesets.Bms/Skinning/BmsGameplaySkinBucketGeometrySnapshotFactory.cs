// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Projects exact geometry declaration provenance from one actual native BMS decoder bucket.
    /// </summary>
    /// <remarks>
    /// The factory reads only decoder-time sidecars. Public compatibility dictionary mutation and accidental enum-composite
    /// key aliases cannot forge, erase or alter this closed source snapshot.
    /// </remarks>
    internal static class BmsGameplaySkinBucketGeometrySnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<BmsGameplaySkinBucketGeometrySnapshot> Create(
            IReadOnlyList<BmsSkinConfiguration> decodedConfigurations,
            BmsKeymode keymode)
        {
            ArgumentNullException.ThrowIfNull(decodedConfigurations);

            if (!isSupportedKeymode(keymode))
                throw new ArgumentOutOfRangeException(nameof(keymode), keymode, "The requested BMS keymode is not supported.");

            BmsSkinConfiguration? source = null;

            foreach (BmsSkinConfiguration configuration in decodedConfigurations)
            {
                if (configuration == null)
                    throw new ArgumentException("Decoded BMS configurations cannot contain null entries.", nameof(decodedConfigurations));

                if (configuration.Keymode != keymode)
                    continue;

                if (source != null)
                    throw new ArgumentException("Decoded BMS configurations cannot contain duplicate keymode buckets.", nameof(decodedConfigurations));

                source = configuration;
            }

            if (source == null)
                return GameplaySkinConfigurationDeclaration<BmsGameplaySkinBucketGeometrySnapshot>.Absent;

            var declarations = new List<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>>();

            foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketGeometryFieldCatalog.All)
            {
                GameplaySkinConfigurationDeclaration<float> declaration = source.GetAcceptedGeometry(field);

                if (declaration.IsDeclared)
                    declarations.Add(new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(field, declaration));
            }

            return GameplaySkinConfigurationDeclaration<BmsGameplaySkinBucketGeometrySnapshot>.Declared(
                BmsGameplaySkinBucketGeometrySnapshot.Create(keymode, declarations));
        }

        private static bool isSupportedKeymode(BmsKeymode keymode)
            => keymode is BmsKeymode.Key5K
                or BmsKeymode.Key7K
                or BmsKeymode.Key9K_Bms
                or BmsKeymode.Key9K_Pms
                or BmsKeymode.Key14K;
    }
}
