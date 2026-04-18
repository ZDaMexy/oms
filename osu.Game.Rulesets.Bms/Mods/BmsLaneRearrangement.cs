// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;

namespace osu.Game.Rulesets.Bms.Mods
{
    internal static class BmsLaneRearrangement
    {
        public static void ApplyMirror(IBeatmap beatmap)
        {
            foreach (var laneGroup in getLaneGroups(beatmap))
                applyPermutation(beatmap, laneGroup, laneGroup.Lanes.Reverse().ToArray());
        }

        public static void ApplyRandom(IBeatmap beatmap, BmsRandomMode mode, int? seed, string? customPattern)
        {
            var laneGroups = getLaneGroups(beatmap).ToArray();

            if (laneGroups.Length == 0)
                return;

            if (tryCreateCustomPatterns(laneGroups, customPattern, out var customPermutations))
            {
                for (int i = 0; i < laneGroups.Length; i++)
                    applyPermutation(beatmap, laneGroups[i], customPermutations[i]);

                return;
            }

            seed ??= RNG.Next();
            var random = new Random(seed.Value);

            foreach (var laneGroup in laneGroups)
            {
                switch (mode)
                {
                    case BmsRandomMode.RRandom:
                        applyPermutation(beatmap, laneGroup, createRotationPermutation(laneGroup.Lanes, random));
                        break;

                    case BmsRandomMode.SRandom:
                        applyScatterRandom(beatmap, laneGroup, random);
                        break;

                    default:
                        applyPermutation(beatmap, laneGroup, shuffle(laneGroup.Lanes, random));
                        break;
                }
            }
        }

        private static LaneGroup[] getLaneGroups(IBeatmap beatmap)
        {
            var keymode = getKeymode(beatmap);

            return keymode switch
            {
                BmsKeymode.Key5K => new[] { new LaneGroup(1, 2, 3, 4, 5) },
                BmsKeymode.Key7K => new[] { new LaneGroup(1, 2, 3, 4, 5, 6, 7) },
                BmsKeymode.Key14K => new[]
                {
                    new LaneGroup(1, 2, 3, 4, 5, 6, 7),
                    new LaneGroup(8, 9, 10, 11, 12, 13, 14),
                },
                BmsKeymode.Key9K_Bms => new[] { new LaneGroup(0, 1, 2, 3, 4, 5, 6, 7, 8) },
                BmsKeymode.Key9K_Pms => new[] { new LaneGroup(0, 1, 2, 3, 4, 5, 6, 7, 8) },
                _ => Array.Empty<LaneGroup>(),
            };
        }

        private static BmsKeymode getKeymode(IBeatmap beatmap)
        {
            if (beatmap is BmsBeatmap bmsBeatmap)
                return bmsBeatmap.BmsInfo.Keymode;

            int laneCount = beatmap.HitObjects.OfType<BmsHitObject>().Select(hitObject => hitObject.LaneIndex + 1).DefaultIfEmpty(0).Max();
            int scratchCount = beatmap.HitObjects.OfType<BmsHitObject>().Count(hitObject => hitObject.IsScratch);

            return laneCount switch
            {
                6 when scratchCount > 0 => BmsKeymode.Key5K,
                8 when scratchCount > 0 => BmsKeymode.Key7K,
                9 => BmsKeymode.Key9K_Bms,
                16 when scratchCount > 1 => BmsKeymode.Key14K,
                _ => BmsKeymode.Key7K,
            };
        }

        private static void applyPermutation(IBeatmap beatmap, LaneGroup laneGroup, IReadOnlyList<int> targetLanes)
        {
            if (targetLanes.Count != laneGroup.Lanes.Length)
                throw new ArgumentException("Permutation length must match the lane group length.", nameof(targetLanes));

            var laneMapping = new Dictionary<int, int>(laneGroup.Lanes.Length);

            for (int i = 0; i < laneGroup.Lanes.Length; i++)
                laneMapping[laneGroup.Lanes[i]] = targetLanes[i];

            foreach (var hitObject in beatmap.HitObjects.OfType<BmsHitObject>().Where(hitObject => laneMapping.ContainsKey(hitObject.LaneIndex)).ToArray())
                hitObject.LaneIndex = laneMapping[hitObject.LaneIndex];
        }

        private static void applyScatterRandom(IBeatmap beatmap, LaneGroup laneGroup, Random random)
        {
            var playableObjects = beatmap.HitObjects.OfType<BmsHitObject>()
                                       .Where(hitObject => laneGroup.Contains(hitObject.LaneIndex))
                                       .OrderBy(hitObject => hitObject.StartTime)
                                       .ThenBy(hitObject => hitObject.LaneIndex)
                                       .ToList();

            if (playableObjects.Count == 0)
                return;

            var activeHolds = new List<ActiveHoldLane>();

            foreach (var timeGroup in playableObjects.GroupBy(hitObject => hitObject.StartTime))
            {
                double currentTime = timeGroup.Key;
                activeHolds.RemoveAll(active => active.EndTime <= currentTime);

                var groupedObjects = timeGroup.OrderBy(hitObject => hitObject.LaneIndex).ToList();
                var preferredLanes = laneGroup.Lanes.Where(lane => activeHolds.All(active => active.LaneIndex != lane)).ToArray();
                var assignedLanes = createScatterAssignments(groupedObjects.Count, preferredLanes, laneGroup.Lanes, random);

                for (int i = 0; i < groupedObjects.Count; i++)
                {
                    var hitObject = groupedObjects[i];
                    int assignedLane = assignedLanes[i];

                    hitObject.LaneIndex = assignedLane;

                    if (hitObject is BmsHoldNote holdNote && holdNote.EndTime > holdNote.StartTime)
                        activeHolds.Add(new ActiveHoldLane(assignedLane, holdNote.EndTime));
                }
            }
        }

        private static int[] createScatterAssignments(int objectCount, IReadOnlyList<int> preferredLanes, IReadOnlyList<int> allLanes, Random random)
        {
            var chosenLanes = new List<int>(objectCount);
            var shuffledPreferred = shuffle(preferredLanes, random).ToList();
            var shuffledAll = shuffle(allLanes, random);

            while (chosenLanes.Count < objectCount && shuffledPreferred.Count > 0)
            {
                chosenLanes.Add(shuffledPreferred[0]);
                shuffledPreferred.RemoveAt(0);
            }

            foreach (int lane in shuffledAll)
            {
                if (chosenLanes.Count >= objectCount)
                    break;

                if (!chosenLanes.Contains(lane))
                    chosenLanes.Add(lane);
            }

            while (chosenLanes.Count < objectCount)
                chosenLanes.Add(shuffledAll[random.Next(shuffledAll.Length)]);

            return chosenLanes.ToArray();
        }

        private static int[] createRotationPermutation(IReadOnlyList<int> lanes, Random random)
        {
            if (lanes.Count <= 1)
                return lanes.ToArray();

            int rotation = random.Next(1, lanes.Count);
            bool mirror = random.Next(2) == 1;
            var rotated = new int[lanes.Count];

            for (int i = 0; i < lanes.Count; i++)
                rotated[i] = lanes[(i + rotation) % lanes.Count];

            if (mirror)
                Array.Reverse(rotated);

            return rotated;
        }

        private static int[] shuffle(IReadOnlyList<int> lanes, Random random)
        {
            var shuffled = lanes.ToArray();

            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int target = random.Next(i + 1);
                (shuffled[i], shuffled[target]) = (shuffled[target], shuffled[i]);
            }

            return shuffled;
        }

        private static bool tryCreateCustomPatterns(IReadOnlyList<LaneGroup> laneGroups, string? customPattern, out IReadOnlyList<int>[] permutations)
        {
            permutations = Array.Empty<IReadOnlyList<int>>();

            if (string.IsNullOrWhiteSpace(customPattern))
                return false;

            var cleanedPattern = new string(customPattern.Where(character => !char.IsWhiteSpace(character) && character is not '|' and not '/' and not ',' and not ';' and not '-' && character != 'S' && character != 's').ToArray());

            if (string.IsNullOrEmpty(cleanedPattern) || cleanedPattern.Any(character => !char.IsDigit(character)))
                return false;

            var groupSizes = laneGroups.Select(group => group.Lanes.Length).ToArray();
            int totalRequiredLength = groupSizes.Sum();

            if (laneGroups.Count > 1 && cleanedPattern.Length == groupSizes[0] && groupSizes.All(size => size == groupSizes[0]))
                cleanedPattern = string.Concat(Enumerable.Repeat(cleanedPattern, laneGroups.Count));

            if (cleanedPattern.Length != totalRequiredLength)
                return false;

            var result = new IReadOnlyList<int>[laneGroups.Count];
            int offset = 0;

            for (int i = 0; i < laneGroups.Count; i++)
            {
                int groupSize = groupSizes[i];
                string groupPattern = cleanedPattern.Substring(offset, groupSize);
                offset += groupSize;

                if (!tryCreateCustomPermutation(laneGroups[i], groupPattern, out var permutation))
                    return false;

                result[i] = permutation;
            }

            permutations = result;
            return true;
        }

        private static bool tryCreateCustomPermutation(LaneGroup laneGroup, string groupPattern, out IReadOnlyList<int> permutation)
        {
            permutation = Array.Empty<int>();
            var expectedDigits = Enumerable.Range(1, laneGroup.Lanes.Length).Select(index => (char)('0' + index)).OrderBy(character => character).ToArray();
            var actualDigits = groupPattern.OrderBy(character => character).ToArray();

            if (!actualDigits.SequenceEqual(expectedDigits))
                return false;

            permutation = groupPattern.Select(character => laneGroup.Lanes[character - '1']).ToArray();
            return true;
        }

        private readonly record struct LaneGroup(params int[] Lanes)
        {
            public bool Contains(int laneIndex) => Array.IndexOf(Lanes, laneIndex) >= 0;
        }

        private readonly record struct ActiveHoldLane(int LaneIndex, double EndTime);
    }

    public enum BmsRandomMode
    {
        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.RandomModeRandom))]
        Random,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.RandomModeRRandom))]
        RRandom,

        [LocalisableDescription(typeof(BmsModStrings), nameof(BmsModStrings.RandomModeSRandom))]
        SRandom,
    }
}
