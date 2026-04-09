// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Bms.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal static class BmsGaugeColours
    {
        public static (Color4 Bar, Color4 Accent) Get(BmsGaugeType gaugeType)
            => gaugeType switch
            {
                BmsGaugeType.AssistEasy => (BmsDefaultHudPalette.GaugeAssistEasyBar, BmsDefaultHudPalette.GaugeAssistEasyAccent),
                BmsGaugeType.Easy => (BmsDefaultHudPalette.GaugeEasyBar, BmsDefaultHudPalette.GaugeEasyAccent),
                BmsGaugeType.Normal => (BmsDefaultHudPalette.GaugeNormalBar, BmsDefaultHudPalette.GaugeNormalAccent),
                BmsGaugeType.Hard => (BmsDefaultHudPalette.GaugeHardBar, BmsDefaultHudPalette.GaugeHardAccent),
                BmsGaugeType.ExHard => (BmsDefaultHudPalette.GaugeExHardBar, BmsDefaultHudPalette.GaugeExHardAccent),
                BmsGaugeType.Hazard => (BmsDefaultHudPalette.GaugeHazardBar, BmsDefaultHudPalette.GaugeHazardAccent),
                _ => (BmsDefaultHudPalette.SurfaceText, BmsDefaultHudPalette.SurfaceSubtext),
            };
    }
}
