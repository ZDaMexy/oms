// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaShellPreset
    {
        private static readonly float default_light_position = (480 - 413) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;

        public bool ShowJudgementLine { get; }

        public float LightPosition { get; }

        public int LightFramePerSecond { get; }

        public IReadOnlyList<float> ColumnLineWidths { get; }

        private OmsManiaShellPreset(bool showJudgementLine, float lightPosition, int lightFramePerSecond, IReadOnlyList<float> columnLineWidths)
        {
            ShowJudgementLine = showJudgementLine;
            LightPosition = lightPosition;
            LightFramePerSecond = lightFramePerSecond;
            ColumnLineWidths = columnLineWidths;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaShellPreset> presets = new Dictionary<int, OmsManiaShellPreset>
        {
            [4] = new OmsManiaShellPreset(false, toPosition(0), 24, new[] { 0f, 0f, 0f, 0f, 0f }),
            [5] = new OmsManiaShellPreset(false, default_light_position, 24, new[] { 0f, 0f, 0f, 0f, 0f, 0f }),
            [6] = new OmsManiaShellPreset(false, toPosition(415), 24, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f }),
            [7] = new OmsManiaShellPreset(false, toPosition(0), 24, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }),
            [8] = new OmsManiaShellPreset(false, default_light_position, 24, new[] { 1f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 1f }),
            [9] = new OmsManiaShellPreset(false, toPosition(415), 24, new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 2f }),
        };

        public static OmsManiaShellPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        public float GetLeftLineWidth(int columnIndex)
            => ColumnLineWidths[columnIndex];

        public float GetRightLineWidth(int columnIndex)
            => ColumnLineWidths[columnIndex + 1];

        private static float toPosition(float value)
            => (480 - value) * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
    }
}
