// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Mania.Skinning.Oms
{
    internal sealed class OmsManiaBarLinePreset
    {
        public float BarLineHeight { get; }

        public Color4 BarLineColour { get; }

        private OmsManiaBarLinePreset(float barLineHeight, Color4 barLineColour)
        {
            BarLineHeight = barLineHeight;
            BarLineColour = barLineColour;
        }

        private static readonly IReadOnlyDictionary<int, OmsManiaBarLinePreset> presets = new Dictionary<int, OmsManiaBarLinePreset>
        {
            [4] = new OmsManiaBarLinePreset(1f, new Color4(255, 255, 255, 150)),
            [5] = new OmsManiaBarLinePreset(1f, Color4.White),
            [6] = new OmsManiaBarLinePreset(1f, new Color4(255, 255, 255, 150)),
            [7] = new OmsManiaBarLinePreset(1f, new Color4(255, 255, 255, 150)),
            [8] = new OmsManiaBarLinePreset(1f, new Color4(255, 255, 255, 150)),
            [9] = new OmsManiaBarLinePreset(1.2f, Color4.White),
        };

        public static OmsManiaBarLinePreset? ForStageColumns(int stageColumns)
            => presets.GetValueOrDefault(stageColumns);
    }
}
