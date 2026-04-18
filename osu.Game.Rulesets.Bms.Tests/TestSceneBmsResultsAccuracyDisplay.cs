// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsResultsAccuracyDisplay : OsuTestScene
    {
        private BmsResultsAccuracyDisplay display = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Child = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(260),
                    Child = display = new BmsResultsAccuracyDisplay(createScore(exScore: 14, maxExScore: 18))
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                    }
                }
            };
        });

        [Test]
        public void TestMainDisplayShowsDjLevelLabelAboveRank()
        {
            AddUntilStep("display loaded", () => display.IsLoaded);
            AddAssert("dj level label exists", () => tryGetText(BmsDjLevelDisplay.LabelText), () => Is.Not.Null);
            AddAssert("aa rank text exists", () => tryGetRankText("AA"), () => Is.Not.Null);
            AddAssert("dj level label sits above rank", () => getText(BmsDjLevelDisplay.LabelText).ScreenSpaceDrawQuad.Centre.Y < getRankText("AA").ScreenSpaceDrawQuad.Centre.Y);
        }

        private static ScoreInfo createScore(long exScore, long maxExScore)
            => new ScoreInfo
            {
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = (int)(exScore / 2),
                    [HitResult.Great] = (int)(exScore % 2),
                },
                MaximumStatistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = (int)(maxExScore / 2),
                    [HitResult.Great] = (int)(maxExScore % 2),
                },
            };

        private GlowingSpriteText getRankText(string text)
            => tryGetRankText(text) ?? throw new AssertionException($"Could not find DJ level rank text '{text}'.");

        private GlowingSpriteText? tryGetRankText(string text)
            => display.ChildrenOfType<GlowingSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);

        private OsuSpriteText getText(string text)
            => tryGetText(text) ?? throw new AssertionException($"Could not find sprite text '{text}'.");

        private OsuSpriteText? tryGetText(string text)
            => display.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);
    }
}
