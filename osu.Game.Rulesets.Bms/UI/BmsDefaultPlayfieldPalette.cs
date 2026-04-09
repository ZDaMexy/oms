// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osuTK.Graphics;

namespace osu.Game.Rulesets.Bms.UI
{
    internal static class BmsDefaultPlayfieldPalette
    {
        public static readonly Color4 PlayfieldBackdrop = new Color4(4, 8, 14, 255);
        public static readonly Color4 PlayfieldBaseplate = new Color4(10, 16, 28, 255);
        public static readonly Color4 LaneBackgroundEven = new Color4(24, 30, 45, 255);
        public static readonly Color4 LaneBackgroundOdd = new Color4(28, 34, 50, 255);
        public static readonly Color4 ScratchLaneBackground = new Color4(40, 29, 20, 255);
        public static readonly Color4 LaneDivider = new Color4(88, 102, 128, 255);
        public static readonly Color4 ScratchLaneDivider = new Color4(220, 170, 100, 255);
        public static readonly Color4 Note = new Color4(244, 246, 250, 255);
        public static readonly Color4 LongNote = new Color4(144, 229, 255, 255);
        public static readonly Color4 LongNoteBody = new Color4(52, 154, 210, 255);
        public static readonly Color4 ScratchNote = new Color4(255, 184, 92, 255);
        public static readonly Color4 ScratchLongNoteBody = new Color4(196, 128, 52, 255);

        public static readonly Color4 MetadataWash = new Color4(14, 20, 31, 176);
        public static readonly Color4 MetadataPanelBackground = new Color4(10, 14, 22, 224);
        public static readonly Color4 MetadataPanelBorder = new Color4(108, 122, 148, 88);
        public static readonly Color4 MetadataLabel = BmsDefaultResultsPalette.PanelStatus;
        public static readonly Color4 MetadataAsset = BmsDefaultResultsPalette.PanelTitle;
        public static readonly Color4 MetadataMissing = new Color4(142, 154, 178, 255);

        public static readonly Color4 LaneCoverFill = new Color4(8, 12, 20, 248);
        public static readonly Color4 LaneCoverShade = new Color4(19, 26, 39, 255);
        public static readonly Color4 FocusAccent = BmsDefaultHudPalette.ComboActiveAccent;
        public static readonly Color4 FocusWash = new Color4(255, 194, 108, 180);

        public static readonly Color4 HitTargetBar = new Color4(8, 12, 20, 232);
        public static readonly Color4 HitTargetLine = new Color4(238, 243, 251, 255);
        public static readonly Color4 HitTargetGlow = new Color4(120, 196, 255, 172);
        public static readonly Color4 ScratchHitTargetBar = new Color4(22, 15, 10, 236);
        public static readonly Color4 ScratchHitTargetLine = new Color4(255, 198, 116, 255);
        public static readonly Color4 ScratchHitTargetGlow = new Color4(255, 186, 104, 172);

        public static readonly Color4 MinorBarLine = new Color4(138, 152, 182, 102);
        public static readonly Color4 MajorBarLine = new Color4(214, 224, 243, 182);

        public static Color4 GetLaneBackground(int laneIndex, bool isScratch)
            => isScratch ? ScratchLaneBackground : laneIndex % 2 == 0 ? LaneBackgroundEven : LaneBackgroundOdd;

        public static Color4 GetLaneDivider(bool isScratch)
            => isScratch ? ScratchLaneDivider : LaneDivider;

        public static Color4 GetNote(bool isScratch)
            => isScratch ? ScratchNote : Note;

        public static Color4 GetLongNoteHead(bool isScratch)
            => isScratch ? ScratchNote : LongNote;

        public static Color4 GetLongNoteBody(bool isScratch)
            => isScratch ? ScratchLongNoteBody : LongNoteBody;

        public static Color4 GetLongNoteTail(bool isScratch)
            => isScratch ? ScratchNote : LongNote;

        public static Color4 GetBarLine(bool isMajor) => isMajor ? MajorBarLine : MinorBarLine;
    }
}
