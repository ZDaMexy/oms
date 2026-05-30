// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsScrollingInfoTest
    {
        [Test]
        public void TestFollowsBaseAlgorithmWhileDisengaged()
        {
            var baseInfo = new FakeScrollingInfo();
            var initial = new ConstantScrollAlgorithm();
            baseInfo.AlgorithmBindable.Value = initial;

            var info = new BmsScrollingInfo(baseInfo);

            // Mirrors the base instance-for-instance: the normal path is byte-for-byte unchanged (the red-line guard).
            Assert.That(info.Algorithm.Value, Is.SameAs(initial));

            var updated = new ConstantScrollAlgorithm();
            baseInfo.AlgorithmBindable.Value = updated;
            Assert.That(info.Algorithm.Value, Is.SameAs(updated));
        }

        [Test]
        public void TestEngageStopMotionOverridesBaseAndIgnoresBaseChanges()
        {
            var baseInfo = new FakeScrollingInfo();
            baseInfo.AlgorithmBindable.Value = new ConstantScrollAlgorithm();

            var info = new BmsScrollingInfo(baseInfo);
            var stopMotion = new BmsStopMotionScrollAlgorithm(new BmsScrollProfile(new[] { 0d, 1000d }, new[] { 0d, 1000d }, 500));

            info.EngageStopMotion(stopMotion);
            Assert.That(info.Algorithm.Value, Is.SameAs(stopMotion));

            // While engaged, base changes must not leak through.
            baseInfo.AlgorithmBindable.Value = new ConstantScrollAlgorithm();
            Assert.That(info.Algorithm.Value, Is.SameAs(stopMotion));
        }

        [Test]
        public void TestDisengageRevertsToBaseAlgorithm()
        {
            var baseInfo = new FakeScrollingInfo();
            baseInfo.AlgorithmBindable.Value = new ConstantScrollAlgorithm();

            var info = new BmsScrollingInfo(baseInfo);
            info.EngageStopMotion(new BmsStopMotionScrollAlgorithm(new BmsScrollProfile(new[] { 0d, 1000d }, new[] { 0d, 1000d }, 500)));

            var current = new ConstantScrollAlgorithm();
            baseInfo.AlgorithmBindable.Value = current;

            info.Disengage();
            Assert.That(info.Algorithm.Value, Is.SameAs(current));

            // After disengaging it tracks the base again.
            var next = new ConstantScrollAlgorithm();
            baseInfo.AlgorithmBindable.Value = next;
            Assert.That(info.Algorithm.Value, Is.SameAs(next));
        }

        [Test]
        public void TestDirectionAndTimeRangePassThrough()
        {
            var baseInfo = new FakeScrollingInfo();
            var info = new BmsScrollingInfo(baseInfo);

            baseInfo.DirectionBindable.Value = ScrollingDirection.Up;
            baseInfo.TimeRangeBindable.Value = 1234;

            Assert.Multiple(() =>
            {
                Assert.That(info.Direction.Value, Is.EqualTo(ScrollingDirection.Up));
                Assert.That(info.TimeRange.Value, Is.EqualTo(1234));
            });
        }

        private sealed class FakeScrollingInfo : IScrollingInfo
        {
            public Bindable<ScrollingDirection> DirectionBindable { get; } = new Bindable<ScrollingDirection>();
            public BindableDouble TimeRangeBindable { get; } = new BindableDouble();
            public Bindable<IScrollAlgorithm> AlgorithmBindable { get; } = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());

            IBindable<ScrollingDirection> IScrollingInfo.Direction => DirectionBindable;
            IBindable<double> IScrollingInfo.TimeRange => TimeRangeBindable;
            IBindable<IScrollAlgorithm> IScrollingInfo.Algorithm => AlgorithmBindable;
        }
    }
}
