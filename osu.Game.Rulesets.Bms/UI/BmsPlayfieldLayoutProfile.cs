// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Centralises the current strict BMS playfield geometry so layout-critical values can evolve without editing multiple drawables.
    /// </summary>
    public sealed class BmsPlayfieldLayoutProfile
    {
        public BmsKeymode Keymode { get; }

        public int LaneCount { get; }

        public float NormalLaneRelativeWidth { get; }

        public float ScratchLaneRelativeWidth { get; }

        public float NormalLaneRelativeSpacing { get; }

        public float ScratchLaneRelativeSpacing { get; }

        public float PlayfieldWidth { get; }

        public float PlayfieldHeight { get; }

        public float HitTargetHeight { get; }

        public float HitTargetVerticalOffset { get; }

        public float HitTargetBarHeight { get; }

        public float HitTargetLineHeight { get; }

        public float HitTargetGlowRadius { get; }

        public float BarLineHeight { get; }

        private BmsPlayfieldLayoutProfile(
            BmsKeymode keymode,
            int laneCount,
            float normalLaneRelativeWidth,
            float scratchLaneRelativeWidth,
            float normalLaneRelativeSpacing,
            float scratchLaneRelativeSpacing,
            float playfieldWidth,
            float playfieldHeight,
            float hitTargetHeight,
            float hitTargetVerticalOffset,
            float hitTargetBarHeight,
            float hitTargetLineHeight,
            float hitTargetGlowRadius,
            float barLineHeight)
        {
            if (laneCount <= 0)
                throw new ArgumentOutOfRangeException(nameof(laneCount), laneCount, "Lane count must be above zero.");

            Keymode = keymode;
            LaneCount = laneCount;
            NormalLaneRelativeWidth = normalLaneRelativeWidth;
            ScratchLaneRelativeWidth = scratchLaneRelativeWidth;
            NormalLaneRelativeSpacing = normalLaneRelativeSpacing;
            ScratchLaneRelativeSpacing = scratchLaneRelativeSpacing;
            PlayfieldWidth = playfieldWidth;
            PlayfieldHeight = playfieldHeight;
            HitTargetHeight = hitTargetHeight;
            HitTargetVerticalOffset = hitTargetVerticalOffset;
            HitTargetBarHeight = hitTargetBarHeight;
            HitTargetLineHeight = hitTargetLineHeight;
            HitTargetGlowRadius = hitTargetGlowRadius;
            BarLineHeight = barLineHeight;
        }

        public static BmsPlayfieldLayoutProfile CreateDefault(
            BmsKeymode keymode,
            int laneCount,
            float? normalLaneRelativeWidth = null,
            float? scratchLaneRelativeWidth = null,
            float? normalLaneRelativeSpacing = null,
            float? scratchLaneRelativeSpacing = null,
            float? playfieldWidth = null,
            float? playfieldHeight = null,
            float? hitTargetHeight = null,
            float? hitTargetBarHeight = null,
            float? hitTargetLineHeight = null,
            float? hitTargetGlowRadius = null,
            float? hitTargetVerticalOffset = null,
            float? barLineHeight = null)
            => new BmsPlayfieldLayoutProfile(
                keymode,
                laneCount,
                normalLaneRelativeWidth: normalLaneRelativeWidth ?? 1f,
                // Scratch lane is 1.5× the width of a key lane (2× narrowed by 25%).
                scratchLaneRelativeWidth: scratchLaneRelativeWidth ?? 1.5f,
                normalLaneRelativeSpacing: normalLaneRelativeSpacing ?? 0f,
                scratchLaneRelativeSpacing: scratchLaneRelativeSpacing ?? 0.12f,
                // Lanes are normalised to fill this width, so scaling it scales every single-lane (and note) width
                // uniformly. 0.825 = original × 0.75 (−25%) then × 1.1 (widened back 10%).
                playfieldWidth: playfieldWidth ?? Math.Clamp(laneCount * 0.06f, 0.35f, 0.8f) * 0.825f,
                // Combined with the top-anchored playfield (see BmsPlayfield), 0.95 keeps the hit target at 95% screen
                // height while the playfield top reaches the screen top — so notes appear at the top with no gap.
                playfieldHeight: playfieldHeight ?? 0.95f,
                hitTargetHeight: hitTargetHeight ?? 16f,
                hitTargetVerticalOffset: hitTargetVerticalOffset ?? 0f,
                hitTargetBarHeight: hitTargetBarHeight ?? 12f,
                hitTargetLineHeight: hitTargetLineHeight ?? 3f,
                hitTargetGlowRadius: hitTargetGlowRadius ?? 6f,
                barLineHeight: barLineHeight ?? 2f);

        public float GetRelativeLaneWidth(bool isScratch) => isScratch ? ScratchLaneRelativeWidth : NormalLaneRelativeWidth;

        public float GetRelativeLaneSpacing(bool previousIsScratch, bool currentIsScratch)
            => previousIsScratch || currentIsScratch ? ScratchLaneRelativeSpacing : NormalLaneRelativeSpacing;
    }
}
