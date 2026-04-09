// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Lines;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsGaugeHistoryPanelLayout : OsuTestScene
    {
        private DefaultBmsGaugeHistoryDisplay display = null!;

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
                    Child = display = new DefaultBmsGaugeHistoryDisplay(),
                }
            };
        });

        [Test]
        public void TestGaugeTimelineUsesCompactLayout()
        {
            AddStep("set history", () => display.SetHistory(new BmsGaugeHistory(0, 1000, new[]
            {
                new BmsGaugeHistoryTimeline(BmsGaugeType.Normal, new[]
                {
                    new BmsGaugeHistoryPoint(0, 0.08),
                    new BmsGaugeHistoryPoint(250, 0.24),
                    new BmsGaugeHistoryPoint(500, 0.51),
                    new BmsGaugeHistoryPoint(1000, 0.92),
                })
            })));

            AddUntilStep("timeline labels loaded", () => tryGetText("92%") != null);
            AddAssert("display height compact", () => display.DrawHeight < 110);
            AddAssert("percentage label aligned right", () => getText("92%").ScreenSpaceDrawQuad.TopLeft.X - getText("NORMAL").ScreenSpaceDrawQuad.TopLeft.X > 120);
            AddAssert("path contains all samples", () => display.ChildrenOfType<SmoothPath>().Single().Vertices.Count >= 4);
        }

        private OsuSpriteText getText(string text)
            => tryGetText(text)!;

        private OsuSpriteText? tryGetText(string text)
            => display.ChildrenOfType<OsuSpriteText>().SingleOrDefault(drawable => drawable.Text.ToString() == text);
    }
}
