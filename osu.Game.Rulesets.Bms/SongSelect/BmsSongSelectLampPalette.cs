// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Graphics.Colour;
using osu.Game.Rulesets.Bms.Scoring;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.SongSelect
{
    internal static class BmsSongSelectLampPalette
    {
        private static readonly Color4 failed = new Color4(232, 54, 72, 255);
        private static readonly Color4 assistEasy = new Color4(154, 98, 255, 255);
        private static readonly Color4 easy = new Color4(72, 204, 108, 255);
        private static readonly Color4 normal = new Color4(78, 164, 255, 255);
        private static readonly Color4 hard = new Color4(244, 247, 252, 255);
        private static readonly Color4 exHard = new Color4(255, 208, 84, 255);
        private static readonly Color4 hazard = new Color4(255, 148, 92, 255);

        private static readonly ColourInfo fullCombo = ColourInfo.GradientVertical(
            new Color4(255, 118, 214, 255),
            new Color4(102, 226, 255, 255));

        private static readonly ColourInfo perfect = ColourInfo.GradientVertical(
            new Color4(255, 247, 202, 255),
            new Color4(255, 198, 92, 255));

        private static readonly Color4 brightForeground = new Color4(249, 252, 255, 255);
        private static readonly Color4 darkForeground = new Color4(24, 28, 36, 255);

        public static SongSelectPanelAccent? GetAccent(BmsClearLamp lamp)
            => lamp switch
            {
                BmsClearLamp.NoPlay => null,
                BmsClearLamp.Failed => new SongSelectPanelAccent(failed, brightForeground),
                BmsClearLamp.AssistEasyClear => new SongSelectPanelAccent(assistEasy, brightForeground),
                BmsClearLamp.EasyClear => new SongSelectPanelAccent(easy, brightForeground),
                BmsClearLamp.NormalClear => new SongSelectPanelAccent(normal, brightForeground),
                BmsClearLamp.HardClear => new SongSelectPanelAccent(hard, darkForeground),
                BmsClearLamp.ExHardClear => new SongSelectPanelAccent(exHard, darkForeground),
                BmsClearLamp.HazardClear => new SongSelectPanelAccent(hazard, darkForeground),
                BmsClearLamp.FullCombo => new SongSelectPanelAccent(fullCombo, brightForeground),
                BmsClearLamp.Perfect => new SongSelectPanelAccent(perfect, darkForeground),
                _ => null,
            };
    }
}
