// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An immutable snapshot of indexed array declarations accepted for one legacy mania <c>Keys:</c> bucket.
    /// </summary>
    /// <remarks>
    /// Values use the legacy decoder's existing converted compatibility units. Each source index preserves declaration
    /// independently, so synthetic native defaults cannot be mistaken for explicit values. These source arrays are not lane
    /// identities, validated layout values or a complete ruleset-neutral configuration. This process-local carrier is not a
    /// manifest or serialisation ABI.
    /// </remarks>
    public sealed class LegacyManiaGameplaySkinBucketArraySnapshot
    {
        public int SourceColumnCount { get; }

        /// <summary>
        /// The <c>Keys + 1</c> source column-boundary widths. Legacy decoding does not scale these values.
        /// </summary>
        public IReadOnlyList<GameplaySkinConfigurationDeclaration<float>> ColumnLineWidth { get; }

        /// <summary>
        /// The <c>Keys - 1</c> source gaps between adjacent columns.
        /// </summary>
        public IReadOnlyList<GameplaySkinConfigurationDeclaration<float>> ColumnSpacing { get; }

        public IReadOnlyList<GameplaySkinConfigurationDeclaration<float>> ColumnWidth { get; }

        /// <summary>
        /// Values accepted from legacy <c>LightingNWidth</c> declarations.
        /// </summary>
        public IReadOnlyList<GameplaySkinConfigurationDeclaration<float>> ExplosionWidth { get; }

        /// <summary>
        /// Values accepted from legacy <c>LightingLWidth</c> declarations.
        /// </summary>
        public IReadOnlyList<GameplaySkinConfigurationDeclaration<float>> HoldNoteLightWidth { get; }

        private LegacyManiaGameplaySkinBucketArraySnapshot(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<float>[] columnLineWidth,
            GameplaySkinConfigurationDeclaration<float>[] columnSpacing,
            GameplaySkinConfigurationDeclaration<float>[] columnWidth,
            GameplaySkinConfigurationDeclaration<float>[] explosionWidth,
            GameplaySkinConfigurationDeclaration<float>[] holdNoteLightWidth)
        {
            SourceColumnCount = sourceColumnCount;
            ColumnLineWidth = Array.AsReadOnly(columnLineWidth);
            ColumnSpacing = Array.AsReadOnly(columnSpacing);
            ColumnWidth = Array.AsReadOnly(columnWidth);
            ExplosionWidth = Array.AsReadOnly(explosionWidth);
            HoldNoteLightWidth = Array.AsReadOnly(holdNoteLightWidth);
        }

        internal static LegacyManiaGameplaySkinBucketArraySnapshot Create(
            int sourceColumnCount,
            IEnumerable<GameplaySkinConfigurationDeclaration<float>> columnLineWidth,
            IEnumerable<GameplaySkinConfigurationDeclaration<float>> columnSpacing,
            IEnumerable<GameplaySkinConfigurationDeclaration<float>> columnWidth,
            IEnumerable<GameplaySkinConfigurationDeclaration<float>> explosionWidth,
            IEnumerable<GameplaySkinConfigurationDeclaration<float>> holdNoteLightWidth)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceColumnCount);
            ArgumentNullException.ThrowIfNull(columnLineWidth);
            ArgumentNullException.ThrowIfNull(columnSpacing);
            ArgumentNullException.ThrowIfNull(columnWidth);
            ArgumentNullException.ThrowIfNull(explosionWidth);
            ArgumentNullException.ThrowIfNull(holdNoteLightWidth);

            GameplaySkinConfigurationDeclaration<float>[] copiedColumnLineWidth = columnLineWidth.ToArray();
            GameplaySkinConfigurationDeclaration<float>[] copiedColumnSpacing = columnSpacing.ToArray();
            GameplaySkinConfigurationDeclaration<float>[] copiedColumnWidth = columnWidth.ToArray();
            GameplaySkinConfigurationDeclaration<float>[] copiedExplosionWidth = explosionWidth.ToArray();
            GameplaySkinConfigurationDeclaration<float>[] copiedHoldNoteLightWidth = holdNoteLightWidth.ToArray();

            validateLength(copiedColumnLineWidth, checked(sourceColumnCount + 1), nameof(columnLineWidth));
            validateLength(copiedColumnSpacing, sourceColumnCount - 1, nameof(columnSpacing));
            validateLength(copiedColumnWidth, sourceColumnCount, nameof(columnWidth));
            validateLength(copiedExplosionWidth, sourceColumnCount, nameof(explosionWidth));
            validateLength(copiedHoldNoteLightWidth, sourceColumnCount, nameof(holdNoteLightWidth));

            return new LegacyManiaGameplaySkinBucketArraySnapshot(
                sourceColumnCount,
                copiedColumnLineWidth,
                copiedColumnSpacing,
                copiedColumnWidth,
                copiedExplosionWidth,
                copiedHoldNoteLightWidth);
        }

        private static void validateLength(
            GameplaySkinConfigurationDeclaration<float>[] declarations,
            int expectedLength,
            string parameterName)
        {
            if (declarations.Length != expectedLength)
                throw new ArgumentException("A legacy mania array declaration must match its source bucket cardinality.", parameterName);
        }

        /// <summary>
        /// Returns only the carrier type and never includes array values or source data.
        /// </summary>
        public override string ToString() => nameof(LegacyManiaGameplaySkinBucketArraySnapshot);
    }
}
