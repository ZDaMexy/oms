// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Bms;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Mania;
using osu.Game.Scoring;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestScenePanelLocalRankDisplay : OsuTestScene
    {
        protected override Ruleset CreateRuleset() => new BmsRuleset();

        protected override bool UseFreshStoragePerRun => true;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.Cache(Realm);
            return dependencies;
        }

        [Test]
        public void TestIndexedLookupRetainsChartAccountAndBmsBucket()
        {
            const int firstUser = 101;
            const int secondUser = 102;
            PanelLocalRankDisplay display = null!;
            BeatmapInfo selected = null!;
            Guid unlinkedScoreId = Guid.Empty;

            AddStep("store local chart copies and scores sharing a hash", () =>
            {
                var bms = Ruleset.Value.Clone();
                var original = new BeatmapInfo(bms) { Hash = "shared-chart-hash" };
                var copy = new BeatmapInfo(bms) { Hash = original.Hash };

                var firstScore = createScore(original, firstUser, ScoreRank.A, 90);
                var secondScore = createScore(original, secondUser, ScoreRank.S, 95);
                var copyScore = createScore(copy, firstUser, ScoreRank.X, 100);
                var otherRulesetScore = createScore(original, firstUser, ScoreRank.X, 100);
                otherRulesetScore.Ruleset = new ManiaRuleset().RulesetInfo;
                var lr2Score = createScore(original, secondUser, ScoreRank.B, 98);
                lr2Score.Mods = [new BmsModJudgeLr2()];

                var unlinkedScore = createScore(original, secondUser, ScoreRank.SH, 99);
                unlinkedScore.BeatmapInfo = null;
                unlinkedScoreId = unlinkedScore.ID;

                Realm.Write(realm =>
                {
                    realm.Add(original);
                    realm.Add(copy);
                    realm.Add(firstScore);
                    realm.Add(secondScore);
                    realm.Add(copyScore);
                    realm.Add(otherRulesetScore);
                    realm.Add(lr2Score);
                    realm.Add(unlinkedScore);
                    selected = original.Detach();
                });

                ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = firstUser };
                Add(display = new PanelLocalRankDisplay(selected));
            });

            AddUntilStep("display the selected chart and account's rank", getRank, () => Is.EqualTo(ScoreRank.A));
            AddStep("change local user", () => ((DummyAPIAccess)API).LocalUser.Value = new APIUser { Id = secondUser });
            AddUntilStep("display the second user's rank", getRank, () => Is.EqualTo(ScoreRank.S));
            AddStep("switch the BMS judgement mode", () => SelectedMods.Value = [new BmsModJudgeLr2()]);
            AddUntilStep("display only the matching BMS bucket", getRank, () => Is.EqualTo(ScoreRank.B));
            AddStep("reset the BMS judgement mode", () => SelectedMods.SetDefault());
            AddUntilStep("restore the default BMS bucket", getRank, () => Is.EqualTo(ScoreRank.S));
            AddStep("reattach a historical score after its chart returns", () => Realm.Write(realm =>
                realm.Find<ScoreInfo>(unlinkedScoreId)!.BeatmapInfo = realm.Find<BeatmapInfo>(selected.ID)));
            AddUntilStep("the score link update changes the displayed rank", getRank, () => Is.EqualTo(ScoreRank.SH));

            ScoreRank? getRank() => display.ChildrenOfType<UpdateableRank>().Single().Rank;
        }

        private static ScoreInfo createScore(BeatmapInfo beatmap, int user, ScoreRank rank, long total) => new ScoreInfo(beatmap, beatmap.Ruleset)
        {
            User = new APIUser { Id = user },
            Rank = rank,
            TotalScore = total,
        };
    }
}
