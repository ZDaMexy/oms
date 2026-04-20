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
        private TestBmsLaneCover suddenCover = null!;
        private TestBmsLaneCover hiddenCover = null!;

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
                    suddenCover = new TestBmsLaneCover(BmsLaneCoverPosition.Sudden),
                    hiddenCover = new TestBmsLaneCover(BmsLaneCoverPosition.Hidden),
                }
            };
        });

        [Test]
        public void TestCoverPositionIsPreserved()
        {
            AddAssert("sudden cover position is Sudden", () => suddenCover.CoverPosition == BmsLaneCoverPosition.Sudden);
            AddAssert("hidden cover position is Hidden", () => hiddenCover.CoverPosition == BmsLaneCoverPosition.Hidden);
        }

        [Test]
        public void TestZeroCoverageHidesVisuals()
        {
            AddStep("set sudden cover 0%", () => suddenCover.CoverPercent.Value = 0);
            AddAssert("cover container height is 0", () => suddenCover.CoverContainerHeight == 0);
        }

        [Test]
        public void TestNonZeroCoverageShowsVisuals()
        {
            AddStep("set sudden cover 500 (50%)", () => suddenCover.CoverPercent.Value = 500);
            AddAssert("cover container height is 0.5", () => suddenCover.CoverContainerHeight, () => Is.EqualTo(0.5f));
        }

        [Test]
        public void TestCoverageClampedToMaximum()
        {
            AddStep("set sudden cover 2000 (200%)", () => suddenCover.CoverPercent.Value = 2000);
            AddAssert("cover container height is 1", () => suddenCover.CoverContainerHeight, () => Is.EqualTo(1f));
        }

        [Test]
        public void TestCoverageClampedToMinimum()
        {
            AddStep("set hidden cover -500 (-50%)", () => hiddenCover.CoverPercent.Value = -500);
            AddAssert("cover container height is 0", () => hiddenCover.CoverContainerHeight, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestFocusNotVisibleWithZeroCoverage()
        {
            AddStep("set sudden cover 0% and focus", () =>
            {
                suddenCover.CoverPercent.Value = 0;
                suddenCover.IsFocused.Value = true;
            });

            AddAssert("focus edge is hidden", () => suddenCover.FocusEdgeAlpha, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestFocusVisibleWithNonZeroCoverage()
        {
            AddStep("set sudden cover 300 (30%) and focus", () =>
            {
                suddenCover.CoverPercent.Value = 300;
                suddenCover.IsFocused.Value = true;
            });

            AddAssert("focus edge is visible", () => suddenCover.FocusEdgeAlpha, () => Is.GreaterThan(0));
        }

        [Test]
        public void TestFocusHiddenWhenUnfocused()
        {
            AddStep("set coverage and focus", () =>
            {
                hiddenCover.CoverPercent.Value = 500;
                hiddenCover.IsFocused.Value = true;
            });

            AddStep("unfocus", () => hiddenCover.IsFocused.Value = false);

            AddAssert("focus edge is hidden", () => hiddenCover.FocusEdgeAlpha, () => Is.EqualTo(0f));
        }

        [Test]
        public void TestCoverOpacityScalesDisplayAlpha()
        {
            AddStep("set sudden cover opacity 250", () => suddenCover.CoverOpacity.Value = 250);
            AddAssert("display alpha is 0.25", () => suddenCover.CoverDisplayAlpha, () => Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void TestCoverOpacityClampedToMaximum()
        {
            AddStep("set hidden cover opacity 2000", () => hiddenCover.CoverOpacity.Value = 2000);
            AddAssert("display alpha is 1", () => hiddenCover.CoverDisplayAlpha, () => Is.EqualTo(1f));
        }

        private partial class TestBmsLaneCover : BmsLaneCover
        {
            public TestBmsLaneCover(BmsLaneCoverPosition position)
                : base(position)
            {
            }

            public new float CoverContainerHeight => base.CoverContainerHeight;

            public new float FocusEdgeAlpha => base.FocusEdgeAlpha;

            public new float CoverDisplayAlpha => base.CoverDisplayAlpha;
        }
    }
}
