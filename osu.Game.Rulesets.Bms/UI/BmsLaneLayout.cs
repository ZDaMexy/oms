// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Skinning;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Describes the current lane ordering, scratch positions and relative lane widths for the placeholder BMS playfield.
    /// </summary>
    public class BmsLaneLayout
    {
        private readonly Lane[] lanes;

        /// <summary>
        /// Lanes indexed by logical <see cref="Lane.LaneIndex"/>. Use <see cref="Lane.VisualIndex"/> for resolved left-to-right order.
        /// </summary>
        public IReadOnlyList<Lane> Lanes { get; }

        public BmsKeymode Keymode { get; }

        internal BmsPlayfieldLayoutProfile Profile { get; }

        public BmsPlayfieldStyle Style { get; }

        public float TotalRelativeWidth { get; }

        private BmsLaneLayout(Lane[] lanes, BmsKeymode keymode, BmsPlayfieldLayoutProfile profile, BmsPlayfieldStyle style)
        {
            if (lanes.Length == 0)
                throw new ArgumentException("Lane layout must contain at least one lane.", nameof(lanes));

            this.lanes = (Lane[])lanes.Clone();
            Lanes = Array.AsReadOnly(this.lanes);
            Keymode = keymode;
            Profile = profile;
            Style = style;
            TotalRelativeWidth = this.lanes.Max(lane => lane.RelativeStart + lane.RelativeWidth);
        }

        internal static BmsLaneLayout CreateCanonical(BmsKeymode keymode, BmsPlayfieldLayoutProfile profile, BmsPlayfieldStyle style)
        {
            ArgumentNullException.ThrowIfNull(profile);

            int laneCount = getExpectedLaneCount(keymode);
            var appliedStyle = style.GetAppliedStyle(keymode);

            if (profile.Keymode != keymode || profile.LaneCount != laneCount)
                throw new ArgumentException("Provided layout profile must match the resolved keymode and lane count.", nameof(profile));

            var allScratchLaneIndices = getExpectedScratchLaneIndices(keymode, laneCount);

            float[] laneWidths = new float[laneCount];
            var laneActions = new BmsAction[laneCount];
            bool[] laneIsScratch = new bool[laneCount];

            int scratchOrdinal = 0;
            int keyOrdinal = 0;

            for (int i = 0; i < laneCount; i++)
            {
                bool isScratch = allScratchLaneIndices.Contains(i);

                laneIsScratch[i] = isScratch;
                laneWidths[i] = profile.GetRelativeLaneWidth(isScratch);
                laneActions[i] = isScratch ? BmsActionExtensions.GetScratchAction(scratchOrdinal++) : BmsActionExtensions.GetKeyAction(keyOrdinal++);
            }

            int[] visualLaneOrder = getVisualLaneOrder(keymode, laneCount, appliedStyle);
            var lanes = new Lane[laneCount];
            float currentStart = 0;

            for (int visualIndex = 0; visualIndex < visualLaneOrder.Length; visualIndex++)
            {
                int laneIndex = visualLaneOrder[visualIndex];
                bool isScratch = laneIsScratch[laneIndex];
                float spacingBefore = visualIndex == 0 ? 0 : profile.GetRelativeLaneSpacing(laneIsScratch[visualLaneOrder[visualIndex - 1]], isScratch);

                // Insert a 2-lane-width DP centre gap between 1P keys and 2P keys.
                if (keymode == BmsKeymode.Key14K && laneCount > 8 && laneIndex == 8)
                    spacingBefore += profile.NormalLaneRelativeWidth * 2;

                currentStart += spacingBefore;

                lanes[laneIndex] = new Lane(laneIndex, visualIndex, currentStart, laneWidths[laneIndex], spacingBefore, isScratch, laneActions[laneIndex]);
                currentStart += laneWidths[laneIndex];
            }

            return new BmsLaneLayout(lanes, keymode, profile, appliedStyle);
        }

        /// <summary>
        /// Non-rendering compatibility projection used only by topology-focused tests. Production gameplay and the
        /// BMS-to-mania adapter use parser-owned keymode authority directly and never consume this geometry.
        /// </summary>
        internal static BmsLaneLayout CreateForKeymode(
            BmsKeymode keymode,
            int minimumLaneCount = 0,
            ISet<int>? scratchLaneIndices = null,
            BmsPlayfieldLayoutProfile? profile = null,
            BmsPlayfieldStyle style = BmsPlayfieldStyle.Center)
        {
            int canonicalCount = getExpectedLaneCount(keymode);

            if (minimumLaneCount > canonicalCount)
                throw new ArgumentException("A BMS projection cannot extend the parser-owned canonical lane count.", nameof(minimumLaneCount));

            if (scratchLaneIndices != null && !scratchLaneIndices.SetEquals(getExpectedScratchLaneIndices(keymode, canonicalCount)))
                throw new ArgumentException("A BMS projection cannot override canonical scratch semantics.", nameof(scratchLaneIndices));

            profile ??= BmsPlayfieldLayoutProfile.CreateDefault(keymode, canonicalCount);
            return CreateCanonical(keymode, profile, style);
        }

        /// <summary>
        /// Explicit isolated-test compatibility view which delegates to the one immutable gameplay layout solver. Production
        /// renderers retain the gameplay-root <see cref="BmsGameplayLayoutProvider"/> instead.
        /// </summary>
        internal static BmsLaneLayout CreateCompatibilityForTesting(
            IBeatmap beatmap,
            BmsPlayfieldLayoutProfile? profile = null,
            BmsPlayfieldStyle style = BmsPlayfieldStyle.Center)
        {
            if (beatmap is not BmsBeatmap bmsBeatmap)
                throw new ArgumentException("BMS layout requires parser-owned keymode authority.", nameof(beatmap));

            var provider = new BmsGameplayLayoutProvider(bmsBeatmap);

            var configuration = profile == null
                ? new BmsGameplayLayoutConfiguration()
                : new BmsGameplayLayoutConfiguration
                {
                    NormalLaneRelativeWidth = profile.NormalLaneRelativeWidth,
                    ScratchLaneRelativeWidth = profile.ScratchLaneRelativeWidth,
                    NormalLaneRelativeSpacing = profile.NormalLaneRelativeSpacing,
                    ScratchLaneRelativeSpacing = profile.ScratchLaneRelativeSpacing,
                    PlayfieldWidth = profile.PlayfieldWidth,
                    PlayfieldHeight = profile.PlayfieldHeight,
                    HitTargetHeight = profile.HitTargetHeight,
                    HitTargetBarHeight = profile.HitTargetBarHeight,
                    HitTargetLineHeight = profile.HitTargetLineHeight,
                    HitTargetGlowRadius = profile.HitTargetGlowRadius,
                    BarLineHeight = profile.BarLineHeight,
                };
            return provider.PublishForTesting(style, configuration).LaneLayout;
        }

        public Lane GetLane(int laneIndex)
        {
            if ((uint)laneIndex >= (uint)lanes.Length)
                throw new ArgumentOutOfRangeException(nameof(laneIndex), laneIndex, "Lane is not part of the canonical BMS layout.");

            return lanes[laneIndex];
        }

        // Single source of truth for keys + scratch lane count (shared with mine lane-bounds in BmsBeatmapConverter).
        private static int getExpectedLaneCount(BmsKeymode keymode) => BmsRuleset.GetLaneCount(keymode);

        private static HashSet<int> getExpectedScratchLaneIndices(BmsKeymode keymode, int laneCount)
        {
            return keymode switch
            {
                BmsKeymode.Key5K => new HashSet<int> { 0 },
                BmsKeymode.Key7K => new HashSet<int> { 0 },
                BmsKeymode.Key14K when laneCount > 8 => new HashSet<int> { 0, laneCount - 1 },
                BmsKeymode.Key14K => new HashSet<int> { 0 },
                _ => new HashSet<int>(),
            };
        }

        private static int[] getVisualLaneOrder(BmsKeymode keymode, int laneCount, BmsPlayfieldStyle style)
        {
            if (style.UsesScratchVisualRight() && (keymode == BmsKeymode.Key5K || keymode == BmsKeymode.Key7K))
                return Enumerable.Range(1, laneCount - 1).Append(0).ToArray();

            return Enumerable.Range(0, laneCount).ToArray();
        }

        public readonly struct Lane
        {
            public int LaneIndex { get; }

            /// <summary>
            /// The zero-based left-to-right visual position of this lane in the resolved layout.
            /// </summary>
            public int VisualIndex { get; }

            public float RelativeStart { get; }

            public float RelativeWidth { get; }

            public float RelativeSpacingBefore { get; }

            public bool IsScratch { get; }

            public BmsAction Action { get; }

            internal Lane(int laneIndex, int visualIndex, float relativeStart, float relativeWidth, float relativeSpacingBefore, bool isScratch, BmsAction action)
            {
                LaneIndex = laneIndex;
                VisualIndex = visualIndex;
                RelativeStart = relativeStart;
                RelativeWidth = relativeWidth;
                RelativeSpacingBefore = relativeSpacingBefore;
                IsScratch = isScratch;
                Action = action;
            }
        }
    }
}
