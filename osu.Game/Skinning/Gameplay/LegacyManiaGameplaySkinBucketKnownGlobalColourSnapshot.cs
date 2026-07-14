// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK.Graphics;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An immutable snapshot of four exact global colour declarations accepted for one legacy mania <c>Keys:</c> bucket.
    /// </summary>
    /// <remarks>
    /// Values are the legacy decoder's accepted RGB/RGBA colours before any renderer compatibility transformation. This
    /// source-specific process-local carrier intentionally excludes per-column and arbitrary <c>Colour*</c> keys. It is not a
    /// complete colour schema, neutral lane mapping, manifest or serialisation ABI.
    /// </remarks>
    public sealed class LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot
    {
        public int SourceColumnCount { get; }

        /// <summary>
        /// The exact legacy <c>ColourColumnLine</c> declaration.
        /// </summary>
        public GameplaySkinConfigurationDeclaration<Color4> ColumnLineColour { get; }

        /// <summary>
        /// The exact legacy <c>ColourJudgementLine</c> declaration.
        /// </summary>
        public GameplaySkinConfigurationDeclaration<Color4> JudgementLineColour { get; }

        /// <summary>
        /// The exact legacy <c>ColourBreak</c> declaration.
        /// </summary>
        public GameplaySkinConfigurationDeclaration<Color4> ComboBreakColour { get; }

        /// <summary>
        /// The exact legacy <c>ColourBarline</c> declaration.
        /// </summary>
        public GameplaySkinConfigurationDeclaration<Color4> BarLineColour { get; }

        private LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<Color4> columnLineColour,
            GameplaySkinConfigurationDeclaration<Color4> judgementLineColour,
            GameplaySkinConfigurationDeclaration<Color4> comboBreakColour,
            GameplaySkinConfigurationDeclaration<Color4> barLineColour)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceColumnCount);

            SourceColumnCount = sourceColumnCount;
            ColumnLineColour = columnLineColour;
            JudgementLineColour = judgementLineColour;
            ComboBreakColour = comboBreakColour;
            BarLineColour = barLineColour;
        }

        internal static LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot Create(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<Color4> columnLineColour,
            GameplaySkinConfigurationDeclaration<Color4> judgementLineColour,
            GameplaySkinConfigurationDeclaration<Color4> comboBreakColour,
            GameplaySkinConfigurationDeclaration<Color4> barLineColour)
            => new(sourceColumnCount, columnLineColour, judgementLineColour, comboBreakColour, barLineColour);

        /// <summary>
        /// Returns only the carrier type and never includes source keys or colour values.
        /// </summary>
        public override string ToString() => nameof(LegacyManiaGameplaySkinBucketKnownGlobalColourSnapshot);
    }
}
