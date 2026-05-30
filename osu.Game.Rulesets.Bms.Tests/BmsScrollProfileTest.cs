// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsScrollProfileTest
    {
        // A profile exercising every regime: a normal slope-1 ramp, a STOP freeze (flat plateau), an extreme-BPM snap
        // (near-vertical), and another slope-1 ramp.
        private static BmsScrollProfile createProfile() => new BmsScrollProfile(
            new[] { 0d, 1000d, 1500d, 1501d, 2501d },
            new[] { 0d, 1000d, 1000d, 5000d, 6000d },
            baseBeatLength: 500);

        [Test]
        public void TestDistanceInterpolatesWithinSegments()
        {
            var profile = createProfile();

            Assert.Multiple(() =>
            {
                Assert.That(profile.DistanceAt(0), Is.EqualTo(0).Within(1e-6));
                Assert.That(profile.DistanceAt(500), Is.EqualTo(500).Within(1e-6)); // slope-1 ramp
                Assert.That(profile.DistanceAt(1000), Is.EqualTo(1000).Within(1e-6));
                Assert.That(profile.DistanceAt(1500.5), Is.EqualTo(3000).Within(1e-6)); // mid snap
                Assert.That(profile.DistanceAt(2001), Is.EqualTo(5500).Within(1e-6));
            });
        }

        [Test]
        public void TestDistanceFreezesAcrossStopPlateau()
        {
            var profile = createProfile();

            Assert.Multiple(() =>
            {
                Assert.That(profile.DistanceAt(1000), Is.EqualTo(1000).Within(1e-6));
                Assert.That(profile.DistanceAt(1250), Is.EqualTo(1000).Within(1e-6)); // frozen
                Assert.That(profile.DistanceAt(1500), Is.EqualTo(1000).Within(1e-6));
                Assert.That(profile.PositionDelta(1000, 1500), Is.EqualTo(0).Within(1e-6));
            });
        }

        [Test]
        public void TestExtremeSegmentProducesSnapSlope()
        {
            var profile = createProfile();

            // 4000 distance units across 1ms => slope 4000x the base ramp: a visual snap.
            double snapSlope = profile.PositionDelta(1500, 1501);
            Assert.That(snapSlope, Is.EqualTo(4000).Within(1e-6));
            Assert.That(snapSlope, Is.GreaterThan(profile.PositionDelta(0, 1000)));
        }

        [Test]
        public void TestDistanceExtrapolatesPastEnds()
        {
            var profile = createProfile();

            Assert.Multiple(() =>
            {
                // Before the first knot: extrapolate using the first segment slope (1).
                Assert.That(profile.DistanceAt(-100), Is.EqualTo(-100).Within(1e-6));
                // After the last knot: extrapolate using the last segment slope (1).
                Assert.That(profile.DistanceAt(3501), Is.EqualTo(7000).Within(1e-6));
            });
        }

        [Test]
        public void TestTimeAtDistanceReturnsEarliestTimeOnPlateau()
        {
            var profile = createProfile();

            // Distance 1000 is reached at t=1000 and held until t=1500; the earliest time must be returned.
            Assert.That(profile.TimeAtDistance(1000), Is.EqualTo(1000).Within(1e-6));
        }

        [Test]
        public void TestTimeAtDistanceInvertsDistanceAt()
        {
            var profile = createProfile();

            foreach (double time in new[] { 50d, 500d, 999d, 1500.5d, 1800d, 2400d })
                Assert.That(profile.TimeAtDistance(profile.DistanceAt(time)), Is.EqualTo(time).Within(1e-6), $"round-trip failed at t={time}");
        }

        [Test]
        public void TestTimeAtDistanceExtrapolatesPastEnds()
        {
            var profile = createProfile();

            Assert.Multiple(() =>
            {
                Assert.That(profile.TimeAtDistance(-100), Is.EqualTo(-100).Within(1e-6));
                Assert.That(profile.TimeAtDistance(7000), Is.EqualTo(3501).Within(1e-6));
            });
        }

        [Test]
        public void TestSingleKnotProfileIsConstant()
        {
            var profile = new BmsScrollProfile(new[] { 0d }, new[] { 0d }, baseBeatLength: 500);

            Assert.Multiple(() =>
            {
                Assert.That(profile.DistanceAt(1234), Is.EqualTo(0).Within(1e-6));
                Assert.That(profile.TimeAtDistance(1234), Is.EqualTo(0).Within(1e-6));
            });
        }

        [Test]
        public void TestEmptyKnotsFallBackToOrigin()
        {
            var profile = new BmsScrollProfile(Array.Empty<double>(), Array.Empty<double>(), baseBeatLength: 500);

            Assert.Multiple(() =>
            {
                Assert.That(profile.KnotTimes, Has.Count.EqualTo(1));
                Assert.That(profile.DistanceAt(999), Is.EqualTo(0).Within(1e-6));
            });
        }

        [Test]
        public void TestMismatchedKnotCountsThrow()
        {
            Assert.Throws<ArgumentException>(() => new BmsScrollProfile(new[] { 0d, 1d }, new[] { 0d }, baseBeatLength: 500));
        }

        [Test]
        public void TestMetricsCaptureSnapAndFreeze()
        {
            var profile = createProfile();

            Assert.Multiple(() =>
            {
                Assert.That(profile.MaxSlope, Is.EqualTo(4000).Within(1e-6)); // the 4000-distance/1ms snap segment
                Assert.That(profile.FrozenFraction, Is.EqualTo(500.0 / 2501.0).Within(1e-6)); // 500ms frozen of 2501ms
            });
        }

        [Test]
        public void TestDetectsSnapGimmick()
        {
            // The sample profile contains a 4000x snap => detected as stop-motion (well past the 50x threshold).
            Assert.That(createProfile().IsStopMotionGimmick, Is.True);
        }

        [Test]
        public void TestDetectsFreezeOnlyGimmick()
        {
            // No extreme slope, but ~9% of the timeline is frozen by a STOP => detected.
            var profile = new BmsScrollProfile(new[] { 0d, 1000d, 1200d, 2200d }, new[] { 0d, 1000d, 1000d, 2000d }, 500);

            Assert.Multiple(() =>
            {
                Assert.That(profile.MaxSlope, Is.EqualTo(1).Within(1e-6));
                Assert.That(profile.FrozenFraction, Is.GreaterThanOrEqualTo(0.05));
                Assert.That(profile.IsStopMotionGimmick, Is.True);
            });
        }

        [Test]
        public void TestDoesNotDetectNormalChart()
        {
            // Pure slope-1 ramp: D(t)=t, no freeze => not a gimmick.
            var profile = new BmsScrollProfile(new[] { 0d, 1000d, 2000d }, new[] { 0d, 1000d, 2000d }, 500);

            Assert.Multiple(() =>
            {
                Assert.That(profile.MaxSlope, Is.EqualTo(1).Within(1e-6));
                Assert.That(profile.FrozenFraction, Is.EqualTo(0).Within(1e-6));
                Assert.That(profile.IsStopMotionGimmick, Is.False);
            });
        }

        [Test]
        public void TestDoesNotDetectModerateSoflan()
        {
            // A 4x soflan section with no freeze must NOT trip the conservative detector.
            var profile = new BmsScrollProfile(new[] { 0d, 1000d, 1500d }, new[] { 0d, 1000d, 3000d }, 500);

            Assert.Multiple(() =>
            {
                Assert.That(profile.MaxSlope, Is.EqualTo(4).Within(1e-6));
                Assert.That(profile.IsStopMotionGimmick, Is.False);
            });
        }
    }
}
