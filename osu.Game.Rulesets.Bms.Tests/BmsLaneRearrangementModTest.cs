// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsLaneRearrangementModTest
    {
        [Test]
        public void TestMirrorKeepsScratchFixedOnSevenKeyBeatmap()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key7K,
                createNote(1, 1),
                createNote(7, 7),
                createScratch(101, 0));

            new BmsModMirror().ApplyToBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(getLane(beatmap, 1), Is.EqualTo(7));
                Assert.That(getLane(beatmap, 7), Is.EqualTo(1));
                Assert.That(getLane(beatmap, 101), Is.EqualTo(0));
                Assert.That(getObject(beatmap, 101).IsScratch, Is.True);
            });
        }

        [Test]
        public void TestMirrorMirrorsEachFourteenKeySideIndependently()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key14K,
                createScratch(101, 0),
                createNote(1, 1),
                createNote(7, 7),
                createNote(8, 8),
                createNote(14, 14),
                createScratch(102, 15));

            new BmsModMirror().ApplyToBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(getLane(beatmap, 101), Is.EqualTo(0));
                Assert.That(getLane(beatmap, 1), Is.EqualTo(7));
                Assert.That(getLane(beatmap, 7), Is.EqualTo(1));
                Assert.That(getLane(beatmap, 8), Is.EqualTo(14));
                Assert.That(getLane(beatmap, 14), Is.EqualTo(8));
                Assert.That(getLane(beatmap, 102), Is.EqualTo(15));
            });
        }

        [Test]
        public void TestRandomCustomPatternOverridesPermutation()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key7K,
                createScratch(101, 0),
                createNote(1, 1),
                createNote(4, 4),
                createNote(7, 7));

            var mod = new BmsModRandom();
            mod.RandomMode.Value = BmsRandomMode.Random;
            mod.CustomPattern.Value = "S7654321";

            mod.ApplyToBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(getLane(beatmap, 101), Is.EqualTo(0));
                Assert.That(getLane(beatmap, 1), Is.EqualTo(7));
                Assert.That(getLane(beatmap, 4), Is.EqualTo(4));
                Assert.That(getLane(beatmap, 7), Is.EqualTo(1));
            });
        }

        [Test]
        public void TestRandomCustomPatternSingleSideCopiesAcrossFourteenKeySides()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key14K,
                createNote(1, 1),
                createNote(8, 8));

            var mod = new BmsModRandom();
            mod.CustomPattern.Value = "7654321";

            mod.ApplyToBeatmap(beatmap);

            Assert.Multiple(() =>
            {
                Assert.That(getLane(beatmap, 1), Is.EqualTo(7));
                Assert.That(getLane(beatmap, 8), Is.EqualTo(14));
            });
        }

        [Test]
        public void TestRRandomProducesRotationFamilyPattern()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key7K,
                Enumerable.Range(1, 7).Select(index => createNote(index, index)).ToArray());

            var mod = new BmsModRandom();
            mod.RandomMode.Value = BmsRandomMode.RRandom;
            mod.Seed.Value = 20260417;

            mod.ApplyToBeatmap(beatmap);

            var actualPattern = Enumerable.Range(1, 7).Select(index => getLane(beatmap, index)).ToArray();
            var allowedPatterns = createAllowedRotationPatterns(Enumerable.Range(1, 7).ToArray()).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(allowedPatterns.Any(pattern => pattern.SequenceEqual(actualPattern)), Is.True);
                Assert.That(actualPattern.SequenceEqual(new[] { 1, 2, 3, 4, 5, 6, 7 }), Is.False);
                Assert.That(actualPattern.SequenceEqual(new[] { 7, 6, 5, 4, 3, 2, 1 }), Is.False);
            });
        }

        [Test]
        public void TestSRandomKeepsSimultaneousChordDistinctAndScratchFixed()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key7K,
                createScratch(101, 0, 0),
                createNote(1, 1, 0),
                createNote(2, 2, 0),
                createNote(3, 3, 0));

            var mod = new BmsModRandom();
            mod.RandomMode.Value = BmsRandomMode.SRandom;
            mod.Seed.Value = 20260417;

            mod.ApplyToBeatmap(beatmap);

            var lanes = new[]
            {
                getLane(beatmap, 1),
                getLane(beatmap, 2),
                getLane(beatmap, 3),
            };

            Assert.Multiple(() =>
            {
                Assert.That(lanes.Distinct().Count(), Is.EqualTo(3));
                Assert.That(lanes.All(lane => lane >= 1 && lane <= 7), Is.True);
                Assert.That(getLane(beatmap, 101), Is.EqualTo(0));
            });
        }

        [Test]
        public void TestSRandomAvoidsActiveHoldLaneWhenSpaceExists()
        {
            var beatmap = createBeatmap(
                BmsKeymode.Key7K,
                createHold(10, 1, 0, 1000),
                createNote(11, 2, 500),
                createNote(12, 3, 500));

            var mod = new BmsModRandom();
            mod.RandomMode.Value = BmsRandomMode.SRandom;
            mod.Seed.Value = 20260417;

            mod.ApplyToBeatmap(beatmap);

            int holdLane = getLane(beatmap, 10);
            var noteLanes = new[]
            {
                getLane(beatmap, 11),
                getLane(beatmap, 12),
            };

            Assert.Multiple(() =>
            {
                Assert.That(noteLanes.Distinct().Count(), Is.EqualTo(2));
                Assert.That(noteLanes.Contains(holdLane), Is.False);
            });
        }

        private static IEnumerable<int[]> createAllowedRotationPatterns(IReadOnlyList<int> basePattern)
        {
            for (int rotation = 1; rotation < basePattern.Count; rotation++)
            {
                var rotated = basePattern.Skip(rotation).Concat(basePattern.Take(rotation)).ToArray();
                yield return rotated;
                yield return rotated.Reverse().ToArray();
            }
        }

        private static BmsBeatmap createBeatmap(BmsKeymode keymode, params BmsHitObject[] hitObjects)
        {
            return new BmsBeatmap
            {
                BmsInfo = new BmsBeatmapInfo { Keymode = keymode },
                HitObjects = hitObjects.Cast<HitObject>().ToList(),
            };
        }

        private static BmsHitObject createNote(int id, int laneIndex, double startTime = 0)
        {
            return new BmsHitObject
            {
                StartTime = startTime,
                KeysoundId = id,
                LaneIndex = laneIndex,
                IsScratch = false,
            };
        }

        private static BmsHitObject createScratch(int id, int laneIndex, double startTime = 0)
        {
            return new BmsHitObject
            {
                StartTime = startTime,
                KeysoundId = id,
                LaneIndex = laneIndex,
                IsScratch = true,
            };
        }

        private static BmsHoldNote createHold(int id, int laneIndex, double startTime, double endTime)
        {
            return new BmsHoldNote
            {
                StartTime = startTime,
                EndTime = endTime,
                KeysoundId = id,
                LaneIndex = laneIndex,
            };
        }

        private static BmsHitObject getObject(BmsBeatmap beatmap, int id)
            => beatmap.HitObjects.OfType<BmsHitObject>().Single(hitObject => hitObject.KeysoundId == id);

        private static int getLane(BmsBeatmap beatmap, int id)
            => getObject(beatmap, id).LaneIndex;
    }
}
