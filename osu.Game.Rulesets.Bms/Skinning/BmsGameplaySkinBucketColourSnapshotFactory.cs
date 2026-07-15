// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Projects exact colour declaration provenance from one actual native BMS decoder bucket.
    /// </summary>
    /// <remarks>
    /// The factory reads only decoder-time sidecars. Public compatibility dictionary mutation and accidental enum-composite
    /// key aliases cannot forge, erase or alter this closed source snapshot.
    /// </remarks>
    internal static class BmsGameplaySkinBucketColourSnapshotFactory
    {
        public static GameplaySkinConfigurationDeclaration<BmsGameplaySkinBucketColourSnapshot> Create(
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
                return GameplaySkinConfigurationDeclaration<BmsGameplaySkinBucketColourSnapshot>.Absent;

            var declarations = new List<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>>();

            foreach (BmsSkinConfigurationLookups field in BmsGameplaySkinBucketColourFieldCatalog.All)
            {
                GameplaySkinConfigurationDeclaration<Color4> declaration = source.GetAcceptedColour(field);

                if (declaration.IsDeclared)
                    declarations.Add(new KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(field, declaration));
            }

            return GameplaySkinConfigurationDeclaration<BmsGameplaySkinBucketColourSnapshot>.Declared(
                BmsGameplaySkinBucketColourSnapshot.Create(keymode, declarations));
        }

        private static bool isSupportedKeymode(BmsKeymode keymode)
            => keymode is BmsKeymode.Key5K
                or BmsKeymode.Key7K
                or BmsKeymode.Key9K_Bms
                or BmsKeymode.Key9K_Pms
                or BmsKeymode.Key14K;
    }
}
