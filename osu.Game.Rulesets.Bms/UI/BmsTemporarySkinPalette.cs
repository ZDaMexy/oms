// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal static class BmsTemporarySkinPalette
    {
        // IIDX-inspired temporary palette for the BMS failure-feedback layer when skin loading breaks.
        // This is not the future OMS built-in skin direction.
        public static readonly Color4 PlayfieldBackdrop = new Color4(2, 4, 8, 255);
        public static readonly Color4 PlayfieldBaseplate = new Color4(12, 16, 24, 255);

        public static readonly Color4 LaneBackgroundEven = new Color4(24, 28, 38, 255);
        public static readonly Color4 LaneBackgroundOdd = new Color4(30, 35, 46, 255);
        public static readonly Color4 LaneDivider = new Color4(76, 90, 114, 255);

        public static readonly Color4 ScratchLaneBackground = new Color4(44, 30, 20, 255);
        public static readonly Color4 ScratchLaneDivider = new Color4(214, 156, 82, 255);

        public static readonly Color4 HitTargetBar = new Color4(4, 6, 10, 200);
        public static readonly Color4 HitTargetLine = new Color4(228, 234, 246, 255);
        public static readonly Color4 HitTargetGlow = new Color4(164, 192, 235, 128);

        public static readonly Color4 ScratchHitTargetBar = new Color4(28, 18, 10, 220);
        public static readonly Color4 ScratchHitTargetLine = new Color4(255, 186, 96, 255);
        public static readonly Color4 ScratchHitTargetGlow = new Color4(255, 170, 84, 168);

        public static readonly Color4 LaneCoverFill = new Color4(4, 6, 10, 255);
        public static readonly Color4 LaneCoverFocus = new Color4(255, 206, 132, 255);
        public static readonly Color4 LaneCoverFocusWash = new Color4(255, 194, 108, 180);

        public static readonly Color4 BackgroundPlaceholderWash = new Color4(116, 150, 208, 255);
        public static readonly Color4 BackgroundLabel = new Color4(196, 207, 228, 255);
        public static readonly Color4 BackgroundAsset = new Color4(238, 243, 251, 255);
        public static readonly Color4 BackgroundMissing = new Color4(140, 152, 176, 255);

        public static readonly Color4 HudPanelBackground = new Color4(10, 12, 18, 240);
        public static readonly Color4 HudPanelBorder = new Color4(176, 188, 212, 44);
        public static readonly Color4 HudPanelBaseLine = new Color4(236, 240, 248, 22);
        public static readonly Color4 HudPanelText = new Color4(238, 243, 251, 255);
        public static readonly Color4 HudPanelSubtext = new Color4(214, 222, 238, 191);
        public static readonly Color4 HudThresholdMarker = new Color4(244, 247, 252, 180);

        public static readonly Color4 GaugeAssistEasyBar = new Color4(84, 204, 176, 255);
        public static readonly Color4 GaugeAssistEasyAccent = new Color4(166, 243, 221, 255);
        public static readonly Color4 GaugeEasyBar = new Color4(116, 204, 116, 255);
        public static readonly Color4 GaugeEasyAccent = new Color4(194, 235, 172, 255);
        public static readonly Color4 GaugeNormalBar = new Color4(255, 184, 92, 255);
        public static readonly Color4 GaugeNormalAccent = new Color4(255, 222, 164, 255);
        public static readonly Color4 GaugeHardBar = new Color4(255, 120, 114, 255);
        public static readonly Color4 GaugeHardAccent = new Color4(255, 188, 180, 255);
        public static readonly Color4 GaugeExHardBar = new Color4(236, 108, 170, 255);
        public static readonly Color4 GaugeExHardAccent = new Color4(255, 178, 214, 255);
        public static readonly Color4 GaugeHazardBar = new Color4(208, 58, 58, 255);
        public static readonly Color4 GaugeHazardAccent = new Color4(248, 132, 124, 255);

        public static readonly Color4 NormalNote = new Color4(244, 246, 250, 255);
        public static readonly Color4 LongNote = new Color4(144, 229, 255, 255);
        public static readonly Color4 LongNoteBody = new Color4(52, 154, 210, 255);
        public static readonly Color4 ScratchNote = new Color4(255, 184, 92, 255);
        public static readonly Color4 ScratchLongNoteBody = new Color4(196, 128, 52, 255);

        public static readonly Color4 BarLine = new Color4(150, 164, 194, 200);

        public static Color4 GetLaneBackground(int laneIndex) => laneIndex % 2 == 0 ? LaneBackgroundEven : LaneBackgroundOdd;
    }
}
