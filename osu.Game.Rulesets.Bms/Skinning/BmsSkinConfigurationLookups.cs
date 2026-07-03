// Copyright (c) OMS contributors. Licensed under the MIT Licence.

namespace osu.Game.Rulesets.Bms.Skinning
{
    /// <summary>
    /// Logical configuration keys a BMS skin (the <c>[Bms]</c> sections of <c>skin.ini</c>, bucketed per keymode)
    /// can override, mirroring the role of <see cref="osu.Game.Skinning.LegacyManiaSkinConfigurationLookups"/> for mania.
    /// </summary>
    /// <remarks>
    /// The key set is derived from what the built-in programmatic default actually exposes — the geometry on
    /// <see cref="UI.BmsPlayfieldLayoutProfile"/>, the colours on <see cref="UI.BmsDefaultPlayfieldPalette"/>, and the
    /// per-lane note / lane texture slots currently drawn as flat boxes — NOT from the (规划/draft) SKINNING.md field
    /// table. This covers the ①-class static elements owned by F1; the gauge / combo / turntable / keyflash families
    /// (②③-class <c>[Bms]</c> keys) are owned by F2/F3 and intentionally omitted. Judgement-display art is reached
    /// through the existing <see cref="BmsJudgementSkinLookup"/> path, not this enum.
    /// </remarks>
    public enum BmsSkinConfigurationLookups
    {
        // ---- Geometry — maps to BmsPlayfieldLayoutProfile.CreateDefault(...) parameters ----
        PlayfieldWidth,
        PlayfieldHeight,
        NormalLaneWidth,
        ScratchLaneWidth,
        NormalLaneSpacing,
        ScratchLaneSpacing,
        HitTargetHeight,
        HitTargetBarHeight,
        HitTargetLineHeight,
        HitTargetGlowRadius,
        BarLineHeight,
        LongNoteBodyWidth,

        // NOTE: HitTargetVerticalOffset is deliberately NOT exposed — it must stay 0 to preserve the BMS timing
        // invariant (scrollLengthRatio == 1, so GN / judgement windows are independent of field height).
        // See P1-A TECHNICAL_CONSTRAINTS 术语与产品约束 #18.

        // ---- Per-lane texture slots — lane resolved via BmsSkinConfigurationLookup.LaneIndex / IsScratch ----
        NoteImage,
        HoldNoteHeadImage,
        HoldNoteBodyImage,
        HoldNoteTailImage,
        KeyImage,
        KeyImageDown,
        LaneBackgroundImage,
        LaneDividerImage,

        // ---- Stage / global texture slots ----
        HitTargetImage,
        StageLeftImage,
        StageRightImage,
        StageBottomImage,
        StageHintImage,
        PlayfieldBackdropImage,
        LaneCoverTopImage,
        LaneCoverBottomImage,

        // ---- Note colours — IIDX colour groups (BmsDefaultPlayfieldPalette.NoteColourGroup), not per-lane ----
        NoteColourWhite,
        NoteColourCyan,
        NoteColourYellow,
        NoteColourScratch,

        // ---- Lane / divider / hit-target / barline / cover / backdrop colours ----
        LaneBackgroundEvenColour,
        LaneBackgroundOddColour,
        ScratchLaneBackgroundColour,
        LaneDividerColour,
        ScratchLaneDividerColour,
        HitTargetBarColour,
        HitTargetLineColour,
        HitTargetGlowColour,
        ScratchHitTargetBarColour,
        ScratchHitTargetLineColour,
        ScratchHitTargetGlowColour,
        MajorBarLineColour,
        MinorBarLineColour,
        LaneCoverFillColour,
        LaneCoverShadeColour,
        LaneCoverFocusColour,
        PlayfieldBackdropColour,
        PlayfieldBaseplateColour,

        // ---- F2 engine-driven elements: keyflash (column light on key press) / hit lighting (flash on hit) ----
        KeyFlashImage,
        HitLightingImage,
        KeyFlashColour,
        HitLightingColour,
    }
}
