// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Skinning.Gameplay;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// An immutable snapshot of exact native <c>[Bms]</c> colour declarations accepted for one keymode bucket.
    /// </summary>
    /// <remarks>
    /// Values are parser-accepted RGB/RGBA colours. No theme defaulting, contrast adjustment, slot fallback or renderer
    /// connection occurs here. This source-specific process-local carrier is not a manifest or wire ABI.
    /// </remarks>
    internal sealed class BmsGameplaySkinBucketColourSnapshot
    {
        private readonly IReadOnlyDictionary<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>> declarations;

        public BmsKeymode SourceKeymode { get; }

        private BmsGameplaySkinBucketColourSnapshot(
            BmsKeymode sourceKeymode,
            IEnumerable<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>> declarations)
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

            var copy = new Dictionary<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>();

            foreach (var pair in declarations)
            {
                BmsGameplaySkinBucketColourFieldCatalog.Validate(pair.Key, nameof(declarations));

                if (!pair.Value.IsDeclared)
                    throw new ArgumentException("A stored BMS colour snapshot entry must be declared.", nameof(declarations));

                if (!copy.TryAdd(pair.Key, pair.Value))
                    throw new ArgumentException("A BMS colour snapshot cannot contain duplicate fields.", nameof(declarations));
            }

            SourceKeymode = sourceKeymode;
            this.declarations = new ReadOnlyDictionary<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>(copy);
        }

        public GameplaySkinConfigurationDeclaration<Color4> GetDeclaration(BmsSkinConfigurationLookups field)
        {
            BmsGameplaySkinBucketColourFieldCatalog.Validate(field, nameof(field));

            return declarations.TryGetValue(field, out GameplaySkinConfigurationDeclaration<Color4> declaration)
                ? declaration
                : GameplaySkinConfigurationDeclaration<Color4>.Absent;
        }

        internal static BmsGameplaySkinBucketColourSnapshot Create(
            BmsKeymode sourceKeymode,
            IEnumerable<KeyValuePair<BmsSkinConfigurationLookups, GameplaySkinConfigurationDeclaration<Color4>>> declarations)
            => new(sourceKeymode, declarations);

        /// <summary>
        /// Returns only the carrier type and never includes source colours or keymode data.
        /// </summary>
        public override string ToString() => nameof(BmsGameplaySkinBucketColourSnapshot);
    }
}
