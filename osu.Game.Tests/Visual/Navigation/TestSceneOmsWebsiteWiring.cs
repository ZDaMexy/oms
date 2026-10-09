// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Localisation;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets.Bms;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.Navigation
{
    public partial class TestSceneOmsWebsiteWiring : OsuGameTestScene
    {
        [Test]
        public void TestSongSelectAndToolbarUseOmsWithLegacyOnlineDisabled()
        {
            SoloSongSelect select = null!;
            var chart = new BeatmapInfo(new BmsRuleset().RulesetInfo)
            {
                MD5Hash = new string('a', 32),
                Hash = new string('b', 64),
                OnlineID = -1,
            };
            AddAssert("legacy API stays disabled", () => Game.OnlineFeaturesEnabled, () => Is.False);
            AddAssert("player rankings entry is present", () => Game.ChildrenOfType<ToolbarOmsRankingsButton>().Any());
            AddAssert("toolbar has no generic chart leaderboard toggle", () => !Game.ChildrenOfType<ToolbarOverlayToggleButton>().Any(button => button.TooltipMain.ToString() == "谱面排行榜"));
            AddStep("open native solo selection", () => Game.ScreenStack.Push(select = new SoloSongSelect()));
            AddUntilStep("selection loaded", () => select.IsLoaded);
            AddAssert("local MD5 has details and a board without a ppy ID", () =>
            {
                return select.GetForwardActions(chart).Any(item => item.Text.Value == CommonStrings.Details)
                       && select.GetForwardActions(chart).Any(item => item.Text.Value.ToString() == "谱面排行榜")
                       && Game.GetOmsBeatmapActions(chart).Any(item => item.Text.Value == CommonStrings.CopyLink);
            });
            AddAssert("guest can copy the actual OMS chart link", () => select.GetForwardActions(chart).Any(item => item.Text.Value == CommonStrings.CopyLink));
            AddAssert("public links do not sign in or enable submission", () => Game.OmsIr.State.Enabled, () => Is.False);
            AddStep("remove the chart identity", () => chart.MD5Hash = string.Empty);
            AddAssert("no link is fabricated for an unidentified chart", () => Game.GetOmsBeatmapActions(chart).Any(), () => Is.False);
        }
    }
}
