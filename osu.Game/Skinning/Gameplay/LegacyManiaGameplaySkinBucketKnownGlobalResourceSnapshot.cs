// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Skinning.Gameplay
{
    /// <summary>
    /// An immutable snapshot of thirteen exact global image declarations accepted for one legacy mania <c>Keys:</c> bucket.
    /// </summary>
    /// <remarks>
    /// Values are the legacy decoder's accepted strings before filename cleaning, file validation or materialisation. An empty
    /// string remains declared. Values may contain source-provided resource names or paths and must not be written to diagnostics
    /// without sanitisation. This closed source-specific carrier is not a neutral scene model, manifest or serialisation ABI.
    /// </remarks>
    public sealed class LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot
    {
        public int SourceColumnCount { get; }

        /// <summary>The exact legacy <c>LightingN</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> ExplosionResource { get; }

        /// <summary>The exact legacy <c>LightingL</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> HoldNoteLightResource { get; }

        /// <summary>The exact legacy <c>StageLeft</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> LeftStageResource { get; }

        /// <summary>The exact legacy <c>StageRight</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> RightStageResource { get; }

        /// <summary>The exact legacy <c>StageBottom</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> BottomStageResource { get; }

        /// <summary>The exact legacy <c>StageLight</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> KeyFlashResource { get; }

        /// <summary>The exact legacy <c>StageHint</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> HitTargetResource { get; }

        /// <summary>The exact legacy <c>Hit0</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> MissJudgementResource { get; }

        /// <summary>The exact legacy <c>Hit50</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> MehJudgementResource { get; }

        /// <summary>The exact legacy <c>Hit100</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> OkJudgementResource { get; }

        /// <summary>The exact legacy <c>Hit200</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> GoodJudgementResource { get; }

        /// <summary>The exact legacy <c>Hit300</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> GreatJudgementResource { get; }

        /// <summary>The exact legacy <c>Hit300g</c> declaration.</summary>
        public GameplaySkinConfigurationDeclaration<string> PerfectJudgementResource { get; }

        private LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<string> lightingN,
            GameplaySkinConfigurationDeclaration<string> lightingL,
            GameplaySkinConfigurationDeclaration<string> stageLeft,
            GameplaySkinConfigurationDeclaration<string> stageRight,
            GameplaySkinConfigurationDeclaration<string> stageBottom,
            GameplaySkinConfigurationDeclaration<string> stageLight,
            GameplaySkinConfigurationDeclaration<string> stageHint,
            GameplaySkinConfigurationDeclaration<string> hit0,
            GameplaySkinConfigurationDeclaration<string> hit50,
            GameplaySkinConfigurationDeclaration<string> hit100,
            GameplaySkinConfigurationDeclaration<string> hit200,
            GameplaySkinConfigurationDeclaration<string> hit300,
            GameplaySkinConfigurationDeclaration<string> hit300g)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceColumnCount);

            SourceColumnCount = sourceColumnCount;
            ExplosionResource = lightingN;
            HoldNoteLightResource = lightingL;
            LeftStageResource = stageLeft;
            RightStageResource = stageRight;
            BottomStageResource = stageBottom;
            KeyFlashResource = stageLight;
            HitTargetResource = stageHint;
            MissJudgementResource = hit0;
            MehJudgementResource = hit50;
            OkJudgementResource = hit100;
            GoodJudgementResource = hit200;
            GreatJudgementResource = hit300;
            PerfectJudgementResource = hit300g;
        }

        internal static LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot Create(
            int sourceColumnCount,
            GameplaySkinConfigurationDeclaration<string> lightingN,
            GameplaySkinConfigurationDeclaration<string> lightingL,
            GameplaySkinConfigurationDeclaration<string> stageLeft,
            GameplaySkinConfigurationDeclaration<string> stageRight,
            GameplaySkinConfigurationDeclaration<string> stageBottom,
            GameplaySkinConfigurationDeclaration<string> stageLight,
            GameplaySkinConfigurationDeclaration<string> stageHint,
            GameplaySkinConfigurationDeclaration<string> hit0,
            GameplaySkinConfigurationDeclaration<string> hit50,
            GameplaySkinConfigurationDeclaration<string> hit100,
            GameplaySkinConfigurationDeclaration<string> hit200,
            GameplaySkinConfigurationDeclaration<string> hit300,
            GameplaySkinConfigurationDeclaration<string> hit300g)
            => new(
                sourceColumnCount,
                lightingN,
                lightingL,
                stageLeft,
                stageRight,
                stageBottom,
                stageLight,
                stageHint,
                hit0,
                hit50,
                hit100,
                hit200,
                hit300,
                hit300g);

        /// <summary>
        /// Returns only the carrier type and never includes source keys or resource names.
        /// </summary>
        public override string ToString() => nameof(LegacyManiaGameplaySkinBucketKnownGlobalResourceSnapshot);
    }
}
