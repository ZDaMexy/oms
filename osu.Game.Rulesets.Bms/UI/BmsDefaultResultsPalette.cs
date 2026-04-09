// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal static class BmsDefaultResultsPalette
    {
        public static readonly Color4 PanelBackground = new Color4(11, 15, 24, 248);
        public static readonly Color4 PanelBackgroundAccent = new Color4(18, 24, 36, 255);
        public static readonly Color4 PanelBorder = new Color4(120, 134, 162, 96);
        public static readonly Color4 PanelTitle = new Color4(242, 246, 252, 255);
        public static readonly Color4 PanelStatus = new Color4(172, 184, 206, 255);

        public static readonly Color4 StatisticBackground = new Color4(14, 19, 30, 245);
        public static readonly Color4 StatisticBackgroundAccent = new Color4(20, 27, 40, 255);
        public static readonly Color4 StatisticBorder = new Color4(88, 102, 126, 92);
        public static readonly Color4 StatisticLabel = new Color4(170, 183, 205, 255);
        public static readonly Color4 StatisticValue = new Color4(242, 246, 252, 255);

        public static readonly Color4 LampFullComboAccent = new Color4(122, 216, 255, 255);
        public static readonly Color4 LampPerfectAccent = new Color4(255, 226, 148, 255);

        public static Color4 GetClearLampAccent(BmsClearLamp lamp)
            => lamp switch
            {
                BmsClearLamp.NoPlay => PanelStatus,
                BmsClearLamp.Failed => BmsDefaultHudPalette.GaugeHazardAccent,
                BmsClearLamp.AssistEasyClear => BmsDefaultHudPalette.GaugeAssistEasyAccent,
                BmsClearLamp.EasyClear => BmsDefaultHudPalette.GaugeEasyAccent,
                BmsClearLamp.NormalClear => BmsDefaultHudPalette.GaugeNormalAccent,
                BmsClearLamp.HardClear => BmsDefaultHudPalette.GaugeHardAccent,
                BmsClearLamp.ExHardClear => BmsDefaultHudPalette.GaugeExHardAccent,
                BmsClearLamp.HazardClear => BmsDefaultHudPalette.GaugeHazardAccent,
                BmsClearLamp.FullCombo => LampFullComboAccent,
                BmsClearLamp.Perfect => LampPerfectAccent,
                _ => PanelTitle,
            };
    }
}
