// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.Objects;

namespace osu.Game.Rulesets.Bms.UI
{
    /// <summary>
    /// Describes the current lane ordering, scratch positions and relative lane widths for the placeholder BMS playfield.
    /// </summary>
    public class BmsLaneLayout
    {
        private readonly Lane[] lanes;

        public IReadOnlyList<Lane> Lanes => lanes;

        public BmsKeymode Keymode { get; }

        public BmsPlayfieldLayoutProfile Profile { get; }

        public float TotalRelativeWidth { get; }

        private BmsLaneLayout(Lane[] lanes, BmsKeymode keymode, BmsPlayfieldLayoutProfile profile)
        {
            if (lanes.Length == 0)
                throw new ArgumentException("Lane layout must contain at least one lane.", nameof(lanes));

            this.lanes = lanes;
            Keymode = keymode;
            Profile = profile;
            TotalRelativeWidth = lanes.Max(lane => lane.RelativeStart + lane.RelativeWidth);
        }

        public static BmsLaneLayout CreateFor(IBeatmap beatmap, BmsPlayfieldLayoutProfile? profile = null)
        {
            ArgumentNullException.ThrowIfNull(beatmap);

            int detectedLaneCount = beatmap.HitObjects.OfType<BmsHitObject>().Select(hitObject => hitObject.LaneIndex + 1).DefaultIfEmpty(0).Max();
            var detectedScratchLanes = beatmap.HitObjects.OfType<BmsHitObject>().Where(hitObject => hitObject.IsScratch).Select(hitObject => hitObject.LaneIndex).ToHashSet();
            var keymode = beatmap is BmsBeatmap bmsBeatmap ? bmsBeatmap.BmsInfo.Keymode : BmsKeymode.Key7K;

            return CreateForKeymode(keymode, detectedLaneCount, detectedScratchLanes, profile);
        }

        public static BmsLaneLayout CreateForKeymode(BmsKeymode keymode, int minimumLaneCount = 0, ISet<int>? scratchLaneIndices = null, BmsPlayfieldLayoutProfile? profile = null)
        {
            int laneCount = Math.Max(getExpectedLaneCount(keymode), minimumLaneCount);
            profile ??= BmsPlayfieldLayoutProfile.CreateDefault(keymode, laneCount);

            if (profile.Keymode != keymode || profile.LaneCount != laneCount)
                throw new ArgumentException("Provided layout profile must match the resolved keymode and lane count.", nameof(profile));

            var allScratchLaneIndices = getExpectedScratchLaneIndices(keymode, laneCount);

            if (scratchLaneIndices != null)
            {
                foreach (int laneIndex in scratchLaneIndices.Where(laneIndex => laneIndex >= 0 && laneIndex < laneCount))
                    allScratchLaneIndices.Add(laneIndex);
            }

            var lanes = new Lane[laneCount];
            float currentStart = 0;
            int scratchOrdinal = 0;
            int keyOrdinal = 0;

            for (int i = 0; i < laneCount; i++)
            {
                bool isScratch = allScratchLaneIndices.Contains(i);
                float spacingBefore = i == 0 ? 0 : profile.GetRelativeLaneSpacing(lanes[i - 1].IsScratch, isScratch);
                float relativeWidth = profile.GetRelativeLaneWidth(isScratch);
                var action = isScratch ? BmsActionExtensions.GetScratchAction(scratchOrdinal++) : BmsActionExtensions.GetKeyAction(keyOrdinal++);

                currentStart += spacingBefore;

                lanes[i] = new Lane(i, currentStart, relativeWidth, spacingBefore, isScratch, action);
                currentStart += relativeWidth;
            }

            return new BmsLaneLayout(lanes, keymode, profile);
        }

        public Lane GetLane(int laneIndex)
        {
            laneIndex = Math.Clamp(laneIndex, 0, lanes.Length - 1);
            return lanes[laneIndex];
        }

        private static int getExpectedLaneCount(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => 6,
                BmsKeymode.Key7K => 8,
                BmsKeymode.Key9K_Bms => 9,
                BmsKeymode.Key9K_Pms => 9,
                BmsKeymode.Key14K => 16,
                _ => 8,
            };

        private static HashSet<int> getExpectedScratchLaneIndices(BmsKeymode keymode, int laneCount)
        {
            return keymode switch
            {
                BmsKeymode.Key5K => new HashSet<int> { 0 },
                BmsKeymode.Key7K => new HashSet<int> { 0 },
                BmsKeymode.Key14K when laneCount > 8 => new HashSet<int> { 0, 8 },
                BmsKeymode.Key14K => new HashSet<int> { 0 },
                _ => new HashSet<int>(),
            };
        }

        public readonly struct Lane
        {
            public int LaneIndex { get; }

            public float RelativeStart { get; }

            public float RelativeWidth { get; }

            public float RelativeSpacingBefore { get; }

            public bool IsScratch { get; }

            public BmsAction Action { get; }

            internal Lane(int laneIndex, float relativeStart, float relativeWidth, float relativeSpacingBefore, bool isScratch, BmsAction action)
            {
                LaneIndex = laneIndex;
                RelativeStart = relativeStart;
                RelativeWidth = relativeWidth;
                RelativeSpacingBefore = relativeSpacingBefore;
                IsScratch = isScratch;
                Action = action;
            }
        }
    }
}
