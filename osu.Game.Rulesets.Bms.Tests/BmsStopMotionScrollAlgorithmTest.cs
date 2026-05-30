// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.UI.Scrolling;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsStopMotionScrollAlgorithmTest
    {
        private const double time_range = 1000;
        private const float scroll_length = 500;

        // Normal slope-1 ramp [0,1000], STOP freeze [1000,1500], extreme snap [1500,1501], slope-1 ramp [1501,2501].
        private static BmsStopMotionScrollAlgorithm createAlgorithm() => new BmsStopMotionScrollAlgorithm(
            new BmsScrollProfile(
                new[] { 0d, 1000d, 1500d, 1501d, 2501d },
                new[] { 0d, 1000d, 1000d, 5000d, 6000d },
                baseBeatLength: 500));

        [Test]
        public void TestPositionMatchesProfileDistance()
        {
            var algorithm = createAlgorithm();

            Assert.Multiple(() =>
            {
                Assert.That(algorithm.PositionAt(1000, 0, time_range, scroll_length), Is.EqualTo(500).Within(1e-3));
                Assert.That(algorithm.PositionAt(500, 0, time_range, scroll_length), Is.EqualTo(250).Within(1e-3));
                Assert.That(algorithm.GetLength(0, 1000, time_range, scroll_length), Is.EqualTo(500).Within(1e-3));
            });
        }

        [Test]
        public void TestPositionFreezesWhileCurrentTimeInsideStop()
        {
            var algorithm = createAlgorithm();

            // While the play head is anywhere inside the STOP plateau, every object holds the same screen position.
            float atStart = algorithm.PositionAt(1501, 1000, time_range, scroll_length);
            float midStop = algorithm.PositionAt(1501, 1250, time_range, scroll_length);
            float atEnd = algorithm.PositionAt(1501, 1499, time_range, scroll_length);

            Assert.Multiple(() =>
            {
                Assert.That(midStop, Is.EqualTo(atStart).Within(1e-3));
                Assert.That(atEnd, Is.EqualTo(atStart).Within(1e-3));
            });
        }

        [Test]
        public void TestExtremeSegmentSnaps()
        {
            var algorithm = createAlgorithm();

            // An object 1ms ahead across the snap segment is far away, whereas 1ms ahead on a normal ramp is ~0.5px.
            float snap = algorithm.PositionAt(1501, 1500, time_range, scroll_length);
            float normal = algorithm.PositionAt(1, 0, time_range, scroll_length);

            Assert.Multiple(() =>
            {
                Assert.That(snap, Is.EqualTo(2000).Within(1e-3));
                Assert.That(normal, Is.LessThan(1));
            });
        }

        [Test]
        public void TestTimeAtInvertsPositionAt()
        {
            var algorithm = createAlgorithm();

            // TimeAt(PositionAt(t)) round-trips on strictly-increasing regions.
            foreach (double time in new[] { 200d, 800d, 1800d, 2400d })
            {
                float position = algorithm.PositionAt(time, 0, time_range, scroll_length);
                Assert.That(algorithm.TimeAt(position, 0, time_range, scroll_length), Is.EqualTo(time).Within(1e-3), $"round-trip failed at t={time}");
            }
        }

        [Test]
        public void TestDisplayStartTimePrecedesObjectByOneScreen()
        {
            var algorithm = createAlgorithm();

            // For a slope-1 region, an object becomes visible one scrollLength (= one time_range of distance) earlier.
            Assert.That(algorithm.GetDisplayStartTime(1000, 0, time_range, scroll_length), Is.EqualTo(0).Within(1e-3));
        }
    }
}
