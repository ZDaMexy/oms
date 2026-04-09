// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsResultsSummaryPanelLayout : OsuTestScene
    {
        private DefaultBmsResultsSummaryDisplay display = null!;

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(20),
                Child = new Container
                {
                    Width = 340,
                    AutoSizeAxes = Axes.Y,
                    Child = display = new DefaultBmsResultsSummaryDisplay(),
                }
            };
        });

        [Test]
        public void TestStatisticRowsDoNotCollapseOrOverlap()
        {
            AddStep("set summary", () => display.SetSummary(new BmsResultsSummaryData(
                BmsGaugeType.Hazard,
                "TEST GAUGE",
                BmsJudgeMode.Beatoraja,
                BmsLongNoteMode.HCN,
                2450,
                3600,
                3,
                0.1234,
                BmsDjLevel.AAA,
                new BmsClearLampData(BmsClearLamp.HardClear, "HARD CLEAR", 0.82))));

            AddUntilStep("summary labels loaded", () => tryGetText("FINAL GAUGE") != null);
            AddAssert("display height compact", () => display.DrawHeight < 370);
            AddAssert("judge mode appears in right column", () => getText("JUDGE MODE").ScreenSpaceDrawQuad.TopLeft.X - getText("GAUGE TYPE").ScreenSpaceDrawQuad.TopLeft.X > 100);
            AddAssert("ex-score appears in right column", () => getText("EX-SCORE").ScreenSpaceDrawQuad.TopLeft.X - getText("LONG NOTE MODE").ScreenSpaceDrawQuad.TopLeft.X > 100);
            AddAssert("second row separated from first", () => getText("LONG NOTE MODE").ScreenSpaceDrawQuad.TopLeft.Y - getText("JUDGE MODE").ScreenSpaceDrawQuad.TopLeft.Y > 28);
            AddAssert("last row separated from previous", () => getText("FINAL GAUGE").ScreenSpaceDrawQuad.TopLeft.Y - getText("DJ LEVEL").ScreenSpaceDrawQuad.TopLeft.Y > 28);
            AddAssert("final row stays inside display bounds", () => getText("FINAL GAUGE").ScreenSpaceDrawQuad.BottomLeft.Y <= display.ScreenSpaceDrawQuad.BottomLeft.Y + 1);
        }

        private OsuSpriteText getText(string text)
            => tryGetText(text) ?? throw new InvalidOperationException($"Could not find results summary text '{text}'.");

        private OsuSpriteText? tryGetText(string text)
            => display.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);
    }
}
