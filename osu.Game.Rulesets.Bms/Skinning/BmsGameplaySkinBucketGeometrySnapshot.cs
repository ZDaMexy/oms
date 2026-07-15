// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// An immutable snapshot of exact native <c>[Bms]</c> geometry declarations accepted for one keymode bucket.
    /// </summary>
    /// <remarks>
    /// Values are parser-accepted invariant floats, including non-finite, negative and zero values. No finite, range,
    /// screen-space or cross-field validation, defaulting, layout solving or renderer connection occurs here.
    /// This source-specific process-local carrier is not a manifest or wire ABI.
    /// </remarks>
    internal sealed class BmsGameplaySkinBucketGeometrySnapshot
    {
        private readonly IReadOnlyDictionary<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>> declarations;

        public BmsKeymode SourceKeymode { get; }

        private BmsGameplaySkinBucketGeometrySnapshot(
            BmsKeymode sourceKeymode,
            IEnumerable<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>> declarations)
        {
            ArgumentNullException.ThrowIfNull(declarations);

            if (sourceKeymode is not (BmsKeymode.Key5K
                or BmsKeymode.Key7K
                or BmsKeymode.Key9K_Bms
                or BmsKeymode.Key9K_Pms
                or BmsKeymode.Key14K))
            {
                throw new ArgumentOutOfRangeException(nameof(sourceKeymode), sourceKeymode, "The source BMS keymode is not supported.");
            }

            var copy = new Dictionary<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>();

            foreach (var pair in declarations)
            {
                BmsGameplaySkinBucketGeometryFieldCatalog.Validate(pair.Key, nameof(declarations));

                if (!pair.Value.IsDeclared)
                    throw new ArgumentException("A stored BMS geometry snapshot entry must be declared.", nameof(declarations));

                if (!copy.TryAdd(pair.Key, pair.Value))
                    throw new ArgumentException("A BMS geometry snapshot cannot contain duplicate fields.", nameof(declarations));
            }

            SourceKeymode = sourceKeymode;
            this.declarations = new ReadOnlyDictionary<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>(copy);
        }

        public GameplaySkinConfigurationDeclaration<float> GetDeclaration(BmsSkinConfigurationLookups field)
        {
            BmsGameplaySkinBucketGeometryFieldCatalog.Validate(field, nameof(field));

            return declarations.TryGetValue(field, out GameplaySkinConfigurationDeclaration<float> declaration)
                ? declaration
                : GameplaySkinConfigurationDeclaration<float>.Absent;
        }

        internal static BmsGameplaySkinBucketGeometrySnapshot Create(
            BmsKeymode sourceKeymode,
            IEnumerable<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<float>>> declarations)
            => new(sourceKeymode, declarations);

        /// <summary>
        /// Returns only the carrier type and never includes source geometry or keymode data.
        /// </summary>
        public override string ToString() => nameof(BmsGameplaySkinBucketGeometrySnapshot);
    }
}
