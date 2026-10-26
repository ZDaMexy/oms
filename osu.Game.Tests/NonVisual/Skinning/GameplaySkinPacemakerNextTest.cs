// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Tests.NonVisual.Skinning
{
    [TestFixture]
    public class GameplaySkinPacemakerNextTest
    {
        [TestCase(0, 1200, "LV A")]
        [TestCase(1199, 1200, "LV A")]
        [TestCase(1200, 1300, "LV A+")]
        [TestCase(1300, 1400, "LV AA")]
        [TestCase(1400, 1500, "LV AA+")]
        [TestCase(1500, 1600, "LV AAA")]
        [TestCase(1600, 1700, "LV AAA+")]
        [TestCase(1700, 1800, "DJ AUTO")]
        [TestCase(1800, 1800, "DJ AUTO")]
        public void TestFirstTargetStrictlyAboveMatchedBestAndSaturation(long best, long expected, string label)
        {
            GameplaySkinPacemakerNextTarget target = GameplaySkinPacemakerNext.Calculate(true, 1800, best);
            Assert.Multiple(() =>
            {
                Assert.That(target.Available, Is.True);
                Assert.That(target.Value, Is.EqualTo(expected));
                Assert.That(target.Progress, Is.EqualTo((double)expected / 1800));
                Assert.That(target.Label, Is.EqualTo(label));
            });
        }

        [TestCase(true, 100, null)]
        [TestCase(false, 100, 75)]
        [TestCase(true, 0, 0)]
        [TestCase(true, -1, null)]
        [TestCase(true, 100, -1)]
        [TestCase(true, 100, 101)]
        public void TestUnavailableInputsDoNotInventATarget(bool usesExScore, long maximum, long? personalBest)
        {
            GameplaySkinPacemakerNextTarget target = GameplaySkinPacemakerNext.Calculate(usesExScore, maximum, personalBest);
            Assert.Multiple(() =>
            {
                Assert.That(target.Available, Is.False);
                Assert.That(target.Value, Is.Zero);
                Assert.That(target.Progress, Is.Zero);
                Assert.That(target.Label, Is.Empty);
            });
        }

        [TestCase(1L)]
        [TestCase(2L)]
        [TestCase(17L)]
        [TestCase(18L)]
        [TestCase(19L)]
        [TestCase(100L)]
        [TestCase(long.MaxValue)]
        public void TestExactCeilingsAndEqualityBoundariesAgainstUnboundedIntegerReference(long maximum)
        {
            string[] labels = { "LV A", "LV A+", "LV AA", "LV AA+", "LV AAA", "LV AAA+", "DJ AUTO" };
            long[] thresholds = Enumerable.Range(12, 7).Select(numerator => (long)(((BigInteger)maximum * numerator + 17) / 18)).ToArray();
            foreach (long best in thresholds.SelectMany(threshold => new[] { threshold - 1, threshold }).Append(0).Distinct())
            {
                int expectedLevel = Array.FindIndex(thresholds, threshold => threshold > best);
                if (expectedLevel < 0)
                    expectedLevel = thresholds.Length - 1;
                GameplaySkinPacemakerNextTarget target = GameplaySkinPacemakerNext.Calculate(true, maximum, best);
                Assert.Multiple(() =>
                {
                    Assert.That(target.Value, Is.EqualTo(thresholds[expectedLevel]), $"maximum={maximum}, best={best}");
                    Assert.That(target.Label, Is.EqualTo(labels[expectedLevel]));
                    Assert.That(target.Available, Is.True);
                    Assert.That(double.IsFinite(target.Progress), Is.True);
                    Assert.That(target.Progress, Is.InRange(0d, 1d));
                });
            }
        }

        [Test]
        public void TestPublishedSourceNumbersRemainStable()
        {
            Assert.Multiple(() =>
            {
                Assert.That((int)GameplaySkinSceneBindingSource.ScorePacemakerNext, Is.EqualTo(96));
                Assert.That((int)GameplaySkinSceneBindingSource.ScorePacemakerNextProgress, Is.EqualTo(97));
                Assert.That((int)GameplaySkinSceneBindingSource.ScorePacemakerNextLabel, Is.EqualTo(98));
                Assert.That((int)GameplaySkinSceneBindingSource.ScorePacemakerNextAvailable, Is.EqualTo(99));
            });
        }
    }
}
