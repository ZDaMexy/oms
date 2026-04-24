// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Rulesets.Objects;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Mods
{
    public partial class TestSceneManiaModFadeIn : ModTestScene
    {
        private float? fullWidthCoverageReferenceHeight;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [Test]
        public void TestMinCoverageFullWidth()
        {
            AddStep("reset coverage reference", () => fullWidthCoverageReferenceHeight = null);

            CreateModTest(new ModTestData
            {
                Mod = new ManiaModFadeIn(),
                PassCondition = () => checkCoverage(ManiaModHidden.MIN_COVERAGE)
            });
        }

        [Test]
        public void TestMinCoverageHalfWidth()
        {
            AddStep("reset coverage reference", () => fullWidthCoverageReferenceHeight = null);

            CreateModTest(new ModTestData
            {
                Mod = new ManiaModFadeIn(),
                PassCondition = () => checkCoverage(ManiaModHidden.MIN_COVERAGE)
            });

            AddStep("capture full width coverage reference", captureFullWidthCoverageReference);
            AddStep("set playfield width to 0.5", () => Player.Width = 0.5f);
        }

        [Test]
        public void TestMaxCoverageFullWidth()
        {
            AddStep("reset coverage reference", () => fullWidthCoverageReferenceHeight = null);

            CreateModTest(new ModTestData
            {
                Mod = new ManiaModFadeIn(),
                PassCondition = () => checkCoverage(ManiaModHidden.MAX_COVERAGE)
            });

            AddStep("set combo to 480", () => Player.ScoreProcessor.Combo.Value = 480);
        }

        [Test]
        public void TestMaxCoverageHalfWidth()
        {
            AddStep("reset coverage reference", () => fullWidthCoverageReferenceHeight = null);

            CreateModTest(new ModTestData
            {
                Mod = new ManiaModFadeIn(),
                PassCondition = () => checkCoverage(ManiaModHidden.MAX_COVERAGE)
            });

            AddStep("capture full width coverage reference", captureFullWidthCoverageReference);
            AddStep("set playfield width to 0.5", () => Player.Width = 0.5f);
            AddStep("set combo to 480", () => Player.ScoreProcessor.Combo.Value = 480);
        }

        [Test]
        public void TestNoCoverageDuringBreak()
        {
            AddStep("reset coverage reference", () => fullWidthCoverageReferenceHeight = null);

            CreateModTest(new ModTestData
            {
                Mod = new ManiaModFadeIn(),
                CreateBeatmap = () => new Beatmap
                {
                    HitObjects = Enumerable.Range(1, 100).Select(i => (HitObject)new Note { StartTime = 1000 + 200 * i }).ToList(),
                    Breaks = { new BreakPeriod(2000, 28000) }
                },
                PassCondition = () => Player.IsBreakTime.Value && checkCoverage(0)
            });
        }

        private void captureFullWidthCoverageReference()
            => fullWidthCoverageReferenceHeight = this.ChildrenOfType<PlayfieldCoveringWrapper>().FirstOrDefault()?.LayoutSize.Y;

        private bool checkCoverage(float expected)
        {
            Drawable? cover = this.ChildrenOfType<PlayfieldCoveringWrapper>().FirstOrDefault();
            Drawable? filledArea = cover?.ChildrenOfType<Box>().LastOrDefault();

            if (filledArea == null)
                return false;

            float currentCoverageHeight = cover!.LayoutSize.Y;
            float referenceCoverageHeight = fullWidthCoverageReferenceHeight ?? currentCoverageHeight;
            float expectedDisplayedHeight = expected * currentCoverageHeight / referenceCoverageHeight;

            // Use the local layout height rather than DrawHeight so parent gameplay scaling does
            // not change the expected stable-space coverage contract.
            return Precision.AlmostEquals(filledArea.LayoutSize.Y, expectedDisplayedHeight, 1f);
        }
    }
}
