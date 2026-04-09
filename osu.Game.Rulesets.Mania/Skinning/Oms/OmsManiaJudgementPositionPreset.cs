// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaJudgementPositionPreset
    {
        public float ScorePosition { get; }
        public float ComboPosition { get; }

        private OmsManiaJudgementPositionPreset(float scorePosition, float comboPosition)
        {
            ScorePosition = scorePosition;
            ComboPosition = comboPosition;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaJudgementPositionPreset> presets = new Dictionary<int, OmsManiaJudgementPositionPreset>
        {
            [4] = new OmsManiaJudgementPositionPreset(toPosition(100), toPosition(465)),
            [5] = new OmsManiaJudgementPositionPreset(toPosition(325), toPosition(85)),
            [6] = new OmsManiaJudgementPositionPreset(toPosition(105), toPosition(375)),
            [7] = new OmsManiaJudgementPositionPreset(toPosition(100), toPosition(90)),
            [8] = new OmsManiaJudgementPositionPreset(toPosition(325), toPosition(85)),
            [9] = new OmsManiaJudgementPositionPreset(toPosition(325), toPosition(85)),
        };

        public static OmsManiaJudgementPositionPreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);

        private static float toPosition(float value)
            => value * LegacyManiaSkinConfiguration.POSITION_SCALE_FACTOR;
    }
}
