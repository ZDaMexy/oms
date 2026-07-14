// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An immutable snapshot of primitive scalar declarations accepted for one legacy mania <c>Keys:</c> bucket.
    /// </summary>
    /// <remarks>
    /// Values use the legacy decoder's existing converted compatibility units and normalisation. They are not raw ini text,
    /// validated layout values or a complete ruleset-neutral configuration. Arrays, colours, resources and note-body style are
    /// deliberately outside this source-specific process-local carrier, which is not a manifest or serialisation ABI.
    /// </remarks>
    public sealed class LegacyManiaGameplaySkinBucketScalarSnapshot
    {
        public int SourceColumnCount { get; }

        public GameplaySkinConfigurationDeclaration<float> WidthForNoteHeightScale { get; }

        public GameplaySkinConfigurationDeclaration<float> HitPosition { get; }

        public GameplaySkinConfigurationDeclaration<float> LightPosition { get; }

        public GameplaySkinConfigurationDeclaration<float> ComboPosition { get; }

        public GameplaySkinConfigurationDeclaration<float> ScorePosition { get; }

        public GameplaySkinConfigurationDeclaration<float> BarLineHeight { get; }

        public GameplaySkinConfigurationDeclaration<bool> ShowJudgementLine { get; }

        public GameplaySkinConfigurationDeclaration<bool> KeysUnderNotes { get; }

        public GameplaySkinConfigurationDeclaration<int> LightFramePerSecond { get; }

        private LegacyManiaGameplaySkinBucketScalarSnapshot(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<float> widthForNoteHeightScale,
            GameplaySkinConfigurationDeclaration<float> hitPosition,
            GameplaySkinConfigurationDeclaration<float> lightPosition,
            GameplaySkinConfigurationDeclaration<float> comboPosition,
            GameplaySkinConfigurationDeclaration<float> scorePosition,
            GameplaySkinConfigurationDeclaration<float> barLineHeight,
            GameplaySkinConfigurationDeclaration<bool> showJudgementLine,
            GameplaySkinConfigurationDeclaration<bool> keysUnderNotes,
            GameplaySkinConfigurationDeclaration<int> lightFramePerSecond)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceColumnCount);

            SourceColumnCount = sourceColumnCount;
            WidthForNoteHeightScale = widthForNoteHeightScale;
            HitPosition = hitPosition;
            LightPosition = lightPosition;
            ComboPosition = comboPosition;
            ScorePosition = scorePosition;
            BarLineHeight = barLineHeight;
            ShowJudgementLine = showJudgementLine;
            KeysUnderNotes = keysUnderNotes;
            LightFramePerSecond = lightFramePerSecond;
        }

        internal static LegacyManiaGameplaySkinBucketScalarSnapshot Create(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<float> widthForNoteHeightScale,
            GameplaySkinConfigurationDeclaration<float> hitPosition,
            GameplaySkinConfigurationDeclaration<float> lightPosition,
            GameplaySkinConfigurationDeclaration<float> comboPosition,
            GameplaySkinConfigurationDeclaration<float> scorePosition,
            GameplaySkinConfigurationDeclaration<float> barLineHeight,
            GameplaySkinConfigurationDeclaration<bool> showJudgementLine,
            GameplaySkinConfigurationDeclaration<bool> keysUnderNotes,
            GameplaySkinConfigurationDeclaration<int> lightFramePerSecond)
            => new(
                sourceColumnCount,
                widthForNoteHeightScale,
                hitPosition,
                lightPosition,
                comboPosition,
                scorePosition,
                barLineHeight,
                showJudgementLine,
                keysUnderNotes,
                lightFramePerSecond);

        /// <summary>
        /// Returns only the carrier type and never includes scalar values or source data.
        /// </summary>
        public override string ToString() => nameof(LegacyManiaGameplaySkinBucketScalarSnapshot);
    }
}
