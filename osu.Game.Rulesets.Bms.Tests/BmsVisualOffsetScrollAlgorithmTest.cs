// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.UI.Scrolling;
using osu.Game.Rulesets.Timing;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsVisualOffsetScrollAlgorithmTest
    {
        [TestCase(-500)]
        [TestCase(0)]
        [TestCase(500)]
        public void TestConstantScrollArrivalAndInverse(double offset)
        {
            var algorithm = new BmsVisualOffsetScrollAlgorithm(new ConstantScrollAlgorithm(), new BindableDouble(offset));

            Assert.That(algorithm.PositionAt(2000, 2000 - offset, 1000, 600), Is.Zero);
            Assert.That(algorithm.TimeAt(300, 1000, 1000, 600), Is.EqualTo(1500 + offset));
            Assert.That(algorithm.GetDisplayStartTime(2000, 0, 1000, 600), Is.EqualTo(500));
        }

        [Test]
        public void TestLongNoteGeometryDoesNotReceiveOffsetTwice()
        {
            var algorithm = new BmsVisualOffsetScrollAlgorithm(new ConstantScrollAlgorithm(), new BindableDouble(100));

            float root = algorithm.PositionAt(1000, 900, 1000, 1000);
            float tail = algorithm.PositionAt(1500, 1000, 1000, 1000, 1000);
            Assert.That(root, Is.Zero);
            Assert.That(tail, Is.EqualTo(500));
            Assert.That(root + tail, Is.EqualTo(algorithm.PositionAt(1500, 900, 1000, 1000)));
            Assert.That(algorithm.GetLength(1000, 1500, 1000, 1000), Is.EqualTo(500));
        }

        [Test]
        public void TestOffsetCrossingSpeedChangeUsesDisplayTime()
        {
            var algorithm = new BmsVisualOffsetScrollAlgorithm(new SequentialScrollAlgorithm(new[]
            {
                new MultiplierControlPoint { Time = 0, Velocity = 1 },
                new MultiplierControlPoint { Time = 1000, Velocity = 2 },
            }), new BindableDouble(100));

            // The first 50ms of the offset precedes the speed change; its final 50ms uses the new speed.
            Assert.That(algorithm.PositionAt(1500, 950, 1000, 1000), Is.EqualTo(900).Within(0.001));
            Assert.That(algorithm.GetLength(500, 1500, 1000, 1000), Is.EqualTo(1500).Within(0.001));
            Assert.That(algorithm.PositionAt(1500, 500, 1000, 1000, 500), Is.EqualTo(1500).Within(0.001));
        }

        [Test]
        public void TestStopFreezeFollowsShiftedDisplayClock()
        {
            var profile = new BmsScrollProfile(new[] { 0d, 1000d, 2000d, 3000d }, new[] { 0d, 1000d, 1000d, 2000d }, 500);
            var offset = new BindableDouble(100);
            var algorithm = new BmsVisualOffsetScrollAlgorithm(new BmsStopMotionScrollAlgorithm(profile), offset);

            Assert.That(algorithm.PositionAt(2500, 950, 1000, 1000), Is.EqualTo(500));
            Assert.That(algorithm.PositionAt(2500, 1850, 1000, 1000), Is.EqualTo(500));
            Assert.That(algorithm.PositionAt(2500, 1950, 1000, 1000), Is.EqualTo(450));
            Assert.That(algorithm.GetLength(500, 2500, 1000, 1000), Is.EqualTo(1000));

            double firstVisible = algorithm.GetDisplayStartTime(2500, 0, 1000, 1000);
            offset.Value = -500;
            Assert.That(algorithm.GetDisplayStartTime(2500, 0, 1000, 1000), Is.EqualTo(firstVisible));
            offset.Value = 500;
            Assert.That(algorithm.PositionAt(2500, firstVisible, 1000, 1000), Is.EqualTo(1000));
        }
    }
}
