// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Version of the explicit mapping between stable nine-key lane numbers and legacy raw <c>[Bms]</c> tokens.
    /// </summary>
    internal enum BmsGameplaySkinNineKeyLaneIndexVersion
    {
        LegacyRawV1 = 1,
    }

    /// <summary>
    /// The only BMS 9K raw/canonical lane-index conversion contract.
    /// </summary>
    internal static class BmsGameplaySkinNineKeyLaneIndexContract
    {
        public const int MINIMUM_CANONICAL_INDEX = 1;
        public const int MAXIMUM_CANONICAL_INDEX = 9;
        public const int MINIMUM_LEGACY_RAW_INDEX = 0;
        public const int MAXIMUM_LEGACY_RAW_INDEX = 8;

        public static int ToLegacyRaw(
            BmsGameplaySkinNineKeyLaneIndexVersion version,
            int canonicalIndex)
        {
            validateVersion(version);

            if (canonicalIndex is < MINIMUM_CANONICAL_INDEX or > MAXIMUM_CANONICAL_INDEX)
                throw new ArgumentOutOfRangeException(nameof(canonicalIndex), canonicalIndex, "A canonical BMS 9K lane index must be in the closed 1..9 range.");

            return canonicalIndex - 1;
        }

        public static int ToCanonical(
            BmsGameplaySkinNineKeyLaneIndexVersion version,
            int legacyRawIndex)
        {
            validateVersion(version);

            if (legacyRawIndex is < MINIMUM_LEGACY_RAW_INDEX or > MAXIMUM_LEGACY_RAW_INDEX)
                throw new ArgumentOutOfRangeException(nameof(legacyRawIndex), legacyRawIndex, "A legacy raw BMS 9K lane index must be in the closed 0..8 range.");

            return legacyRawIndex + 1;
        }

        private static void validateVersion(BmsGameplaySkinNineKeyLaneIndexVersion version)
        {
            if (version != BmsGameplaySkinNineKeyLaneIndexVersion.LegacyRawV1)
                throw new ArgumentOutOfRangeException(nameof(version), version, "Unknown BMS 9K lane-index mapping version.");
        }
    }
}
