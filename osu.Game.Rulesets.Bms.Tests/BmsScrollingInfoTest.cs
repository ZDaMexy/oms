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

        [Test]
        public void TestFixedOffsetRestoresExactAlgorithmAtZero()
        {
            var baseInfo = new FakeScrollingInfo();
            var info = new BmsScrollingInfo(baseInfo);

            info.VisualOffset.Value = 20;
            Assert.That(info.Algorithm.Value.PositionAt(1000, 980, 1000, 1000), Is.Zero);

            info.VisualOffset.Value = 0;
            Assert.That(info.Algorithm.Value, Is.SameAs(baseInfo.AlgorithmBindable.Value));
        }

        [Test]
        public void TestAutomaticAdjustmentKeepsLifetimeAlgorithmAcrossZero()
        {
            var info = new BmsScrollingInfo(new FakeScrollingInfo());
            info.AutomaticVisualOffsetEnabled.Value = true;
            var initial = info.Algorithm.Value;
            int algorithmChanges = 0;
            info.Algorithm.BindValueChanged(_ => algorithmChanges++);

            foreach (double offset in new[] { 500d, 1, 0, -1, -500, 0 })
            {
                info.VisualOffset.Value = offset;
                Assert.That(info.Algorithm.Value.PositionAt(1000, 1000, 1000, 1000), Is.EqualTo(-offset));
                Assert.That(info.Algorithm.Value.GetDisplayStartTime(2000, 0, 1000, 1000), Is.EqualTo(500));
            }

            Assert.That(info.Algorithm.Value, Is.SameAs(initial));
            Assert.That(algorithmChanges, Is.Zero);
        }

        [Test]
        public void TestStopMotionAndBaseChangesPreserveOffset()
        {
            var baseInfo = new FakeScrollingInfo();
            var info = new BmsScrollingInfo(baseInfo);
            info.VisualOffset.Value = 100;
            var stopMotion = new BmsStopMotionScrollAlgorithm(new BmsScrollProfile(new[] { 0d, 1000d, 2000d, 3000d }, new[] { 0d, 1000d, 1000d, 2000d }, 500));
            info.EngageStopMotion(stopMotion);

            Assert.That(info.Algorithm.Value.PositionAt(2500, 950, 1000, 1000), Is.EqualTo(500));
            var active = info.Algorithm.Value;
            var replacement = new ConstantScrollAlgorithm();
            baseInfo.AlgorithmBindable.Value = replacement;
            Assert.That(info.Algorithm.Value, Is.SameAs(active));

            info.Disengage();
            Assert.That(info.Algorithm.Value.PositionAt(2500, 950, 1000, 1000), Is.EqualTo(1450));
            info.VisualOffset.Value = 0;
            Assert.That(info.Algorithm.Value, Is.SameAs(replacement));
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
