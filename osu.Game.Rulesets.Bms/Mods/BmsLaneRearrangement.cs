// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Audio;
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

            if (!string.IsNullOrWhiteSpace(customPattern))
            {
                // A custom pattern is a deliberate fixed arrangement that overrides the random mode + seed.
                // Apply it when it is a valid permutation for this chart; if it does not fit (wrong length /
                // not a permutation), leave the chart unchanged rather than silently substituting a random
                // shuffle the player did not ask for.
                if (tryCreateCustomPatterns(laneGroups, customPattern, out var customPermutations))
                {
                    for (int i = 0; i < laneGroups.Length; i++)
                        applyPermutation(beatmap, laneGroups[i], customPermutations[i]);
                }

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

            throw new ArgumentException("BMS lane rearrangement requires parser-owned BmsBeatmapInfo keymode authority.", nameof(beatmap));
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

            // Mines live outside beatmap.HitObjects (see BmsMine / BmsBeatmap.Mines) but still belong to
            // a lane, so they must follow the same permutation or they desync from the rearranged chart.
            // Per-group mappings are disjoint, so iterating here for each group never double-remaps a mine.
            if (beatmap is BmsBeatmap bmsBeatmap)
            {
                foreach (var mine in bmsBeatmap.Mines)
                {
                    if (laneMapping.TryGetValue(mine.LaneIndex, out int targetLane))
                        mine.LaneIndex = targetLane;
                }

                remapArmedKeysoundTimelines(bmsBeatmap, laneMapping);
            }
        }

        private static void applyScatterRandom(IBeatmap beatmap, LaneGroup laneGroup, Random random)
        {
            // S-RANDOM reassigns each note's lane per timestamp and so has no single column permutation;
            // mines are therefore left on their original lanes and armed empty-press timelines are explicitly disabled
            // for the scattered key group. Keeping the converter's fixed-lane timeline here would play a confidently
            // wrong keysound after an object moves; object/head keysounds themselves remain attached and authoritative.
            if (beatmap is BmsBeatmap bmsBeatmap)
                disableScatterArmedKeysoundTimelines(bmsBeatmap, laneGroup);

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
                int[] preferredLanes = laneGroup.Lanes.Where(lane => activeHolds.All(active => active.LaneIndex != lane)).ToArray();
                int[] assignedLanes = createScatterAssignments(groupedObjects.Count, preferredLanes, laneGroup.Lanes, random);

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

        private static void remapArmedKeysoundTimelines(BmsBeatmap beatmap, IReadOnlyDictionary<int, int> laneMapping)
        {
            if (beatmap.LaneKeysoundTimelines.Count == 0)
                return;

            var remapped = new Dictionary<int, IReadOnlyList<BmsLaneKeysoundEntry>>(beatmap.LaneKeysoundTimelines.Count);

            foreach (var pair in beatmap.LaneKeysoundTimelines)
            {
                int targetLane = laneMapping.TryGetValue(pair.Key, out int mappedLane) ? mappedLane : pair.Key;

                if (!remapped.TryAdd(targetLane, pair.Value))
                    throw new InvalidOperationException("bms.keysound.timeline.non-bijective-lane-permutation");
            }

            beatmap.LaneKeysoundTimelines = remapped;
        }

        private static void disableScatterArmedKeysoundTimelines(BmsBeatmap beatmap, LaneGroup laneGroup)
        {
            beatmap.LaneKeysoundTimelines = beatmap.LaneKeysoundTimelines
                                                     .Where(pair => !laneGroup.Contains(pair.Key))
                                                     .ToDictionary(pair => pair.Key, pair => pair.Value);
            beatmap.LaneKeysoundTimelineDiagnostic = "bms.keysound.timeline.disabled-s-random";
        }

        private static int[] createScatterAssignments(int objectCount, IReadOnlyList<int> preferredLanes, IReadOnlyList<int> allLanes, Random random)
        {
            var chosenLanes = new List<int>(objectCount);
            var shuffledPreferred = shuffle(preferredLanes, random).ToList();
            int[] shuffledAll = shuffle(allLanes, random);

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
            int[] rotated = new int[lanes.Count];

            for (int i = 0; i < lanes.Count; i++)
                rotated[i] = lanes[(i + rotation) % lanes.Count];

            if (mirror)
                Array.Reverse(rotated);

            return rotated;
        }

        private static int[] shuffle(IReadOnlyList<int> lanes, Random random)
        {
            int[] shuffled = lanes.ToArray();

            for (int i = shuffled.Length - 1; i > 0; i--)
            {
                int target = random.Next(i + 1);
                (shuffled[i], shuffled[target]) = (shuffled[target], shuffled[i]);
            }

            return shuffled;
        }

        /// <summary>
        /// Whether <paramref name="character"/> is accepted in a custom pattern input. This is the exact
        /// character set the parser tolerates (digits, plus the separators / scratch marker it strips),
        /// so the settings text box and <see cref="tryCreateCustomPatterns"/> stay in agreement.
        /// </summary>
        internal static bool IsCustomPatternCharacter(char character)
            => char.IsAsciiDigit(character) || isStrippablePatternCharacter(character);

        private static bool isStrippablePatternCharacter(char character)
            => char.IsWhiteSpace(character) || character is '|' or '/' or ',' or ';' or '-' or 'S' or 's';

        /// <summary>
        /// Validates <paramref name="customPattern"/> against a key count (5 / 7 / 9 / 14) and, when valid, returns the
        /// effective per-side digit layout for display (e.g. 14K "7654321" -> "7654321 / 7654321"). Mirrors the
        /// validation in <see cref="tryCreateCustomPatterns"/> exactly: 14K is two independent 1–7 sides (NOT a single
        /// 1–14 permutation), and a single side is mirrored across both. Used by the settings preview; keep the two in sync.
        /// </summary>
        internal static bool TryNormaliseCustomPattern(int keyCount, string? customPattern, out string normalised)
        {
            normalised = string.Empty;

            int[]? groupSizes = keyCount switch
            {
                5 => new[] { 5 },
                7 => new[] { 7 },
                9 => new[] { 9 },
                14 => new[] { 7, 7 },
                _ => null,
            };

            if (groupSizes == null || string.IsNullOrWhiteSpace(customPattern))
                return false;

            string cleaned = new string(customPattern.Where(character => !isStrippablePatternCharacter(character)).ToArray());

            if (cleaned.Length == 0 || cleaned.Any(character => !char.IsAsciiDigit(character)))
                return false;

            if (groupSizes.Length > 1 && cleaned.Length == groupSizes[0] && groupSizes.All(size => size == groupSizes[0]))
                cleaned = string.Concat(Enumerable.Repeat(cleaned, groupSizes.Length));

            if (cleaned.Length != groupSizes.Sum())
                return false;

            string[] parts = new string[groupSizes.Length];
            int offset = 0;

            for (int i = 0; i < groupSizes.Length; i++)
            {
                int size = groupSizes[i];
                string part = cleaned.Substring(offset, size);
                offset += size;

                var expectedDigits = Enumerable.Range(1, size).Select(index => (char)('0' + index)).OrderBy(character => character);

                if (!part.OrderBy(character => character).SequenceEqual(expectedDigits))
                    return false;

                parts[i] = part;
            }

            normalised = string.Join(" / ", parts);
            return true;
        }

        private static bool tryCreateCustomPatterns(IReadOnlyList<LaneGroup> laneGroups, string? customPattern, out IReadOnlyList<int>[] permutations)
        {
            permutations = Array.Empty<IReadOnlyList<int>>();

            if (string.IsNullOrWhiteSpace(customPattern))
                return false;

            string cleanedPattern = new string(customPattern.Where(character => !isStrippablePatternCharacter(character)).ToArray());

            if (string.IsNullOrEmpty(cleanedPattern) || cleanedPattern.Any(character => !char.IsDigit(character)))
                return false;

            int[] groupSizes = laneGroups.Select(group => group.Lanes.Length).ToArray();
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
            char[] expectedDigits = Enumerable.Range(1, laneGroup.Lanes.Length).Select(index => (char)('0' + index)).OrderBy(character => character).ToArray();
            char[] actualDigits = groupPattern.OrderBy(character => character).ToArray();

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
