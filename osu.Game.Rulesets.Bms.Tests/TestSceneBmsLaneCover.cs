// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsLaneCover : OsuTestScene
    {
        private TestBmsLaneCover topCover = null!;
        private TestBmsLaneCover bottomCover = null!;

        [SetUp]
        public void Setup() => Schedule(() =>
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.DarkGray,
                    },
                    topCover = new TestBmsLaneCover(BmsLaneCoverPosition.Top),
                    bottomCover = new TestBmsLaneCover(BmsLaneCoverPosition.Bottom),
                }
            };
        });

        [Test]
        public void TestCoverPositionIsPreserved()
        {
            AddAssert("top cover position is Top", () => topCover.CoverPosition == BmsLaneCoverPosition.Top);
            AddAssert("bottom cover position is Bottom", () => bottomCover.CoverPosition == BmsLaneCoverPosition.Bottom);
        }

        [Test]
        public void TestZeroCoverageHidesVisuals()
        {
            AddStep("set top cover 0%", () => topCover.CoverPercent.Value = 0);
            AddAssert("cover container height is 0", () => topCover.CoverContainerHeight == 0);
        }

        [Test]
        public void TestNonZeroCoverageShowsVisuals()
        {
            AddStep("set top cover 50%", () => topCover.CoverPercent.Value = 50);
            AddAssert("cover container height is 0.5", () => topCover.CoverContainerHeight, () => Is.EqualTo(0.5f));
        }

        [Test]
        public void TestCoverageClampedToMaximum()
        {
            AddStep("set top cover 200%", () => topCover.CoverPercent.Value = 200);
            AddAssert("cover container height is 1", () => topCover.CoverContainerHeight, () => Is.EqualTo(1f));
        }

        [Test]
        public void TestCoverageClampedToMinimum()
        {
            AddStep("set bottom cover -50%", () => bottomCover.CoverPercent.Value = -50);
            AddAssert("cover container height is 0", () => bottomCover.CoverContainerHeight, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestFocusNotVisibleWithZeroCoverage()
        {
            AddStep("set top cover 0% and focus", () =>
            {
                topCover.CoverPercent.Value = 0;
                topCover.IsFocused.Value = true;
            });

            AddAssert("focus edge is hidden", () => topCover.FocusEdgeAlpha, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestFocusVisibleWithNonZeroCoverage()
        {
            AddStep("set top cover 30% and focus", () =>
            {
                topCover.CoverPercent.Value = 30;
                topCover.IsFocused.Value = true;
            });

            AddAssert("focus edge is visible", () => topCover.FocusEdgeAlpha, () => Is.GreaterThan(0));
        }

        [Test]
        public void TestFocusHiddenWhenUnfocused()
        {
            AddStep("set coverage and focus", () =>
            {
                bottomCover.CoverPercent.Value = 50;
                bottomCover.IsFocused.Value = true;
            });

            AddStep("unfocus", () => bottomCover.IsFocused.Value = false);

            AddAssert("focus edge is hidden", () => bottomCover.FocusEdgeAlpha, () => Is.EqualTo(0f));
        }

        private partial class TestBmsLaneCover : BmsLaneCover
        {
            public TestBmsLaneCover(BmsLaneCoverPosition position)
                : base(position)
            {
            }

            public new float CoverContainerHeight => base.CoverContainerHeight;

            public new float FocusEdgeAlpha => base.FocusEdgeAlpha;
        }
    }
}
