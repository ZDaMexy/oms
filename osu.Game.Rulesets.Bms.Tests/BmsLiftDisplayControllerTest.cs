// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Rulesets.UI.Scrolling.Algorithms;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public partial class BmsLiftDisplayControllerTest : OsuTestScene
    {
        [Test]
        public void TestReadOnlyProjectionRetainsTheActuallyAppliedInputUntilTheControllerUpdates()
        {
            var target = new Container { Size = new Vector2(440, 728) };
            var receptor = new Container();
            var units = new BindableFloat(250);
            var scrolling = new LiftScrollingInfo();
            scrolling.Direction.Value = ScrollingDirection.Down;
            var controller = new BmsHitObjectAreaLayoutController(target, BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8), units, receptor);
            controller.Bind(scrolling);
            Assert.That(controller.HasAppliedProjection, Is.False);
            AddStep("load the native geometry carrier", () => { Clear(); Add(target); target.Add(receptor); });
            AddUntilStep("geometry carrier loaded at its real size", () => target.IsLoaded && target.DrawHeight == 728);
            AddStep("apply the first native projection", () =>
            {
                controller.Update();
                Assert.That(controller.AppliedLiftUnits, Is.EqualTo(250));
                Assert.That(controller.AppliedOffsetPixels, Is.EqualTo(182));
                Assert.That(controller.AppliedOffsetRatio, Is.EqualTo(0.25));
                Assert.That(controller.AppliedHeight, Is.EqualTo(728));
                Assert.That(target.Padding.Bottom, Is.EqualTo(182));
                Assert.That(receptor.Anchor, Is.EqualTo(Anchor.BottomLeft));
            });

            AddStep("observe pending inputs without mixing frames", () =>
            {
                units.Value = 500;
                scrolling.Direction.Value = ScrollingDirection.Up;
                Assert.That(controller.AppliedLiftUnits, Is.EqualTo(250));
                Assert.That(controller.AppliedOffsetRatio, Is.EqualTo(0.25));
                Assert.That(controller.AppliedReverse, Is.False,
                    "A frame-stable snapshot must not combine a new pending bindable input with an earlier applied geometry.");
            });

            AddStep("apply the new native projection", () =>
            {
                controller.Update();
                Assert.That(controller.AppliedLiftUnits, Is.EqualTo(500));
                Assert.That(controller.AppliedOffsetPixels, Is.EqualTo(364));
                Assert.That(controller.AppliedOffsetRatio, Is.EqualTo(0.5));
                Assert.That(controller.AppliedReverse, Is.True);
                Assert.That(target.Padding.Top, Is.EqualTo(364));
                Assert.That(target.Padding.Bottom, Is.Zero);
                Assert.That(receptor.Anchor, Is.EqualTo(Anchor.TopLeft));
                Assert.That(units.Value, Is.EqualTo(500));
            });
        }

        [TestCase(ScrollingDirection.Down)]
        [TestCase(ScrollingDirection.Up)]
        public void TestProjectionUsesTheRealResizedAreaAndTheExistingClampedInputDomain(ScrollingDirection direction)
        {
            var target = new Container { Size = new Vector2(440, 728) };
            var units = new BindableFloat(250);
            var scrolling = new LiftScrollingInfo();
            scrolling.Direction.Value = direction;
            var controller = new BmsHitObjectAreaLayoutController(target, BmsPlayfieldLayoutProfile.CreateDefault(BmsKeymode.Key7K, 8), units);
            controller.Bind(scrolling);
            AddStep("load the resizable geometry carrier", () => { Clear(); Add(target); });
            AddUntilStep("geometry carrier loaded at its real size", () => target.IsLoaded && target.DrawHeight == 728);
            AddStep("resize after applying the first projection", () =>
            {
                controller.Update();
                target.Height = 400;
            });
            AddStep("project the resized carrier", () =>
            {
                controller.Update();
                Assert.That(controller.AppliedHeight, Is.EqualTo(400));
                Assert.That(controller.AppliedOffsetPixels, Is.EqualTo(100));
                Assert.That(controller.AppliedOffsetRatio, Is.EqualTo(0.25));
                Assert.That(direction == ScrollingDirection.Up ? target.Padding.Top : target.Padding.Bottom, Is.EqualTo(100));
                units.Value = 1500;
                controller.Update();
                Assert.That(controller.AppliedLiftUnits, Is.EqualTo(1000));
                Assert.That(controller.AppliedOffsetPixels, Is.EqualTo(400));
                Assert.That(controller.AppliedOffsetRatio, Is.EqualTo(1));
                Assert.That(units.Value, Is.EqualTo(1500), "The visual projection cannot write back to the native input.");
                units.Value = -100;
                controller.Update();
                Assert.That(controller.AppliedLiftUnits, Is.Zero);
                Assert.That(controller.AppliedOffsetRatio, Is.Zero);
                target.Height = 0;
                controller.Update();
                Assert.That(controller.AppliedHeight, Is.Zero);
                Assert.That(controller.AppliedOffsetRatio, Is.Zero);
                Assert.That(float.IsFinite(controller.AppliedOffsetRatio), Is.True);
            });
        }

        private sealed class LiftScrollingInfo : IScrollingInfo
        {
            public Bindable<ScrollingDirection> Direction { get; } = new Bindable<ScrollingDirection>();
            IBindable<ScrollingDirection> IScrollingInfo.Direction => Direction;
            public IBindable<double> TimeRange { get; } = new Bindable<double>(5000);
            public IBindable<IScrollAlgorithm> Algorithm { get; } = new Bindable<IScrollAlgorithm>(new ConstantScrollAlgorithm());
        }
    }
}
