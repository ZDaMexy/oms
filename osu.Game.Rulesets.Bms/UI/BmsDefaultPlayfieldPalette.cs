// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Skinning;
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
        public static readonly Color4 WhiteKeyNote = new Color4(243, 243, 243, 255);
        public static readonly Color4 CyanKeyNote = new Color4(53, 234, 255, 255);
        public static readonly Color4 YellowKeyNote = new Color4(255, 222, 53, 255);
        public static readonly Color4 ScratchNote = new Color4(252, 0, 20, 255);
        public static readonly Color4 WhiteKeyLongNoteBody = darken(WhiteKeyNote, 0.72f);
        public static readonly Color4 CyanKeyLongNoteBody = darken(CyanKeyNote, 0.72f);
        public static readonly Color4 YellowKeyLongNoteBody = darken(YellowKeyNote, 0.72f);
        public static readonly Color4 ScratchLongNoteBody = darken(ScratchNote, 0.72f);

        public static readonly Color4 MetadataWash = new Color4(14, 20, 31, 176);
        public static readonly Color4 MetadataPanelBackground = new Color4(10, 14, 22, 224);
        public static readonly Color4 MetadataPanelBorder = new Color4(108, 122, 148, 88);
        public static readonly Color4 MetadataLabel = BmsDefaultResultsPalette.PanelStatus;
        public static readonly Color4 MetadataAsset = BmsDefaultResultsPalette.PanelTitle;
        public static readonly Color4 MetadataMissing = new Color4(142, 154, 178, 255);

        public static readonly Color4 LaneCoverFill = new Color4(8, 12, 20, 255);
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

        public static Color4 GetLaneBackground(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            if (isScratch)
                return ScratchLaneBackground;

            // For 14K DP, reset parity at the 2P boundary so both sides start with the lighter background.
            int bgIndex = keymode == BmsKeymode.Key14K && laneIndex > 7 ? laneIndex - 7 : laneIndex;
            return bgIndex % 2 == 0 ? LaneBackgroundEven : LaneBackgroundOdd;
        }

        public static Color4 GetLaneDivider(bool isScratch)
            => isScratch ? ScratchLaneDivider : LaneDivider;

        public static Color4 GetNote(int laneIndex, bool isScratch, BmsKeymode keymode)
            => getNoteColourGroup(laneIndex, isScratch, keymode) switch
            {
                NoteColourGroup.Cyan => CyanKeyNote,
                NoteColourGroup.Yellow => YellowKeyNote,
                NoteColourGroup.Scratch => ScratchNote,
                _ => WhiteKeyNote,
            };

        public static Color4 GetLongNoteHead(int laneIndex, bool isScratch, BmsKeymode keymode)
            => GetNote(laneIndex, isScratch, keymode);

        public static Color4 GetLongNoteBody(int laneIndex, bool isScratch, BmsKeymode keymode)
            => getNoteColourGroup(laneIndex, isScratch, keymode) switch
            {
                NoteColourGroup.Cyan => CyanKeyLongNoteBody,
                NoteColourGroup.Yellow => YellowKeyLongNoteBody,
                NoteColourGroup.Scratch => ScratchLongNoteBody,
                _ => WhiteKeyLongNoteBody,
            };

        public static Color4 GetLongNoteTail(int laneIndex, bool isScratch, BmsKeymode keymode)
            => GetNote(laneIndex, isScratch, keymode);

        // Greyed + dimmed long-note body colour for a broken (missed) hold, derived from the active head colour so a
        // break is read as the same note "going dead" rather than a different element.
        public static Color4 GetLongNoteBodyBroken(int laneIndex, bool isScratch, BmsKeymode keymode)
            => greyOut(GetLongNoteHead(laneIndex, isScratch, keymode), greyAmount: 0.85f, dimFactor: 0.45f);

        public static Color4 GetBarLine(bool isMajor) => isMajor ? MajorBarLine : MinorBarLine;

        /// <summary>The IIDX colour-group skin config key (NoteColour White/Cyan/Yellow/Scratch) a lane's notes map to.</summary>
        public static BmsSkinConfigurationLookups GetNoteColourLookup(int laneIndex, bool isScratch, BmsKeymode keymode)
            => getNoteColourGroup(laneIndex, isScratch, keymode) switch
            {
                NoteColourGroup.Cyan => BmsSkinConfigurationLookups.NoteColourCyan,
                NoteColourGroup.Yellow => BmsSkinConfigurationLookups.NoteColourYellow,
                NoteColourGroup.Scratch => BmsSkinConfigurationLookups.NoteColourScratch,
                _ => BmsSkinConfigurationLookups.NoteColourWhite,
            };

        /// <summary>The lane-background skin config key a lane maps to (even / odd / scratch), matching <see cref="GetLaneBackground"/>.</summary>
        public static BmsSkinConfigurationLookups GetLaneBackgroundLookup(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            if (isScratch)
                return BmsSkinConfigurationLookups.ScratchLaneBackgroundColour;

            int bgIndex = keymode == BmsKeymode.Key14K && laneIndex > 7 ? laneIndex - 7 : laneIndex;
            return bgIndex % 2 == 0 ? BmsSkinConfigurationLookups.LaneBackgroundEvenColour : BmsSkinConfigurationLookups.LaneBackgroundOddColour;
        }

        /// <summary>Greyed + dimmed broken-hold colour derived from an active colour (matches <see cref="GetLongNoteBodyBroken"/>).</summary>
        public static Color4 GreyOutBroken(Color4 activeColour) => greyOut(activeColour, greyAmount: 0.85f, dimFactor: 0.45f);

        private static NoteColourGroup getNoteColourGroup(int laneIndex, bool isScratch, BmsKeymode keymode)
        {
            if (isScratch)
                return NoteColourGroup.Scratch;

            int keyNumber = getKeyNumber(laneIndex, keymode);

            if (keymode == BmsKeymode.Key7K)
            {
                return keyNumber switch
                {
                    2 or 6 => NoteColourGroup.Cyan,
                    4 => NoteColourGroup.Yellow,
                    _ => NoteColourGroup.White,
                };
            }

            // For 14K DP, each side independently follows the IIDX pattern:
            // odd position (1,3,5,7) → White, even position (2,4,6) → Cyan.
            if (keymode == BmsKeymode.Key14K)
            {
                int posInSide = keyNumber <= 7 ? keyNumber : keyNumber - 7;
                return posInSide % 2 == 0 ? NoteColourGroup.Cyan : NoteColourGroup.White;
            }

            return keyNumber % 2 == 0 ? NoteColourGroup.White : NoteColourGroup.Cyan;
        }

        private static int getKeyNumber(int laneIndex, BmsKeymode keymode)
        {
            return keymode switch
            {
                BmsKeymode.Key5K => Math.Clamp(laneIndex, 1, 5),
                BmsKeymode.Key7K => Math.Clamp(laneIndex, 1, 7),
                BmsKeymode.Key9K_Bms or BmsKeymode.Key9K_Pms => Math.Clamp(laneIndex + 1, 1, 9),
                BmsKeymode.Key14K => Math.Clamp(laneIndex, 1, 14),
                _ => Math.Max(1, laneIndex),
            };
        }

        private static Color4 darken(Color4 colour, float factor)
            => new Color4(colour.R * factor, colour.G * factor, colour.B * factor, colour.A);

        // Blend a colour toward its own luminance (grey) by greyAmount, then darken by dimFactor. Alpha is preserved.
        private static Color4 greyOut(Color4 colour, float greyAmount, float dimFactor)
        {
            float luminance = colour.R * 0.299f + colour.G * 0.587f + colour.B * 0.114f;
            return new Color4(
                lerp(colour.R, luminance, greyAmount) * dimFactor,
                lerp(colour.G, luminance, greyAmount) * dimFactor,
                lerp(colour.B, luminance, greyAmount) * dimFactor,
                colour.A);
        }

        private static float lerp(float start, float end, float amount) => start + (end - start) * amount;

        private enum NoteColourGroup
        {
            White,
            Cyan,
            Yellow,
            Scratch,
        }
    }
}
