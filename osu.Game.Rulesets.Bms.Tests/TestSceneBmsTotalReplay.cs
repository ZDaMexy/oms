// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Bms.Tests
{
    [HeadlessTest]
    [TestFixture]
    public partial class TestSceneBmsTotalReplay : OsuTestScene
    {
        private BmsBeatmap beatmap = null!;
        private BmsGaugeProcessor gauge = null!;
        private DrawableBmsRuleset drawable = null!;
        private Score score = null!;

        [TestCase(0, false)]
        [TestCase(0, true)]
        [TestCase(6, false)]
        [TestCase(6, true)]
        [TestCase(7, false)]
        [TestCase(7, true)]
        public void TestSavedReplayKeepsItsTotalRules(int version, bool applyModAfterReplay)
        {
            loadRuleset(false);
            AddStep("create saved replay", () =>
            {
                score = new Score
                {
                    Replay = new Replay { Frames = { new BmsReplayFrame(0) } },
                    ScoreInfo = { Mods = new Mod[] { new BmsModGaugeRulesBeatoraja() } },
                };

                if (version != 0)
                    score.ScoreInfo.SetRulesetData(new BmsScoreInfoData
                    {
                        Version = version,
                        GaugeRulesFamily = BmsGaugeRulesFamily.Beatoraja,
                    });

                if (!applyModAfterReplay)
                    new BmsModGaugeRulesBeatoraja().ApplyToHealthProcessor(gauge);

                drawable.SetReplayScore(score);

                if (applyModAfterReplay)
                    new BmsModGaugeRulesBeatoraja().ApplyToHealthProcessor(gauge);
            });
            assertTotalAndRecovery(version >= 7 ? 260 : 200);
            AddAssert("historical identity unchanged", () => score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.Version ?? 0, () => Is.EqualTo(version));
        }

        [Test]
        public void TestFreshAutoplayUsesNewTotalRules()
        {
            loadRuleset(true);
            AddStep("load newly generated autoplay", () =>
            {
                var autoplay = new BmsModAutoplay();
                var mods = new Mod[] { autoplay, new BmsModGaugeRulesBeatoraja() };
                score = autoplay.CreateScoreFromReplayData(beatmap, mods);
                score.ScoreInfo.Mods = mods;
                drawable.SetReplayScore(score);
                new BmsModGaugeRulesBeatoraja().ApplyToHealthProcessor(gauge);
            });
            assertTotalAndRecovery(260);
            assertNewIdentity();
        }

        [Test]
        public void TestNewRecordingStoresNewTotalRules()
        {
            loadRuleset(false);
            AddStep("start new recording", () =>
            {
                score = new Score { ScoreInfo = { Mods = new Mod[] { new BmsModGaugeRulesBeatoraja() } } };
                new BmsModGaugeRulesBeatoraja().ApplyToHealthProcessor(gauge);
                drawable.SetRecordTarget(score);
            });
            assertTotalAndRecovery(260);
            assertNewIdentity();
            AddStep("stop recording", () => drawable.SetRecordTarget(null!));
        }

        private void loadRuleset(bool autoplay)
        {
            AddStep("load BMS with injected gauge", () =>
            {
                var decoded = new BmsBeatmapDecoder().DecodeText(@"
#TITLE TOTAL replay
#BPM 120
#WAV01 note.wav
#00111:010101010101010101
#00118:01
", "total-replay.bme");
                beatmap = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(decoded), new BmsRuleset()).Convert();
                gauge = new BmsGaugeProcessor(0);
                gauge.ApplyBeatmap(beatmap);
                var mods = autoplay
                    ? new Mod[] { new BmsModGaugeRulesBeatoraja(), new BmsModAutoplay() }
                    : new Mod[] { new BmsModGaugeRulesBeatoraja() };
                drawable = new TestDrawableRuleset(beatmap, mods)
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = new FramedClock(new ManualClock { CurrentTime = -10000 }),
                };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(HealthProcessor), gauge) },
                    Child = drawable,
                };
            });
            AddUntilStep("ruleset loaded", () => drawable.IsLoaded);
        }

        private void assertTotalAndRecovery(double expectedTotal)
        {
            AddAssert("resolved TOTAL", () => gauge.ChartTotal, () => Is.EqualTo(expectedTotal));
            AddStep("judge one perfect", () =>
            {
                var note = beatmap.HitObjects.OfType<BmsHitObject>().First();
                gauge.ApplyResult(new JudgementResult(note, note.CreateJudgement()) { Type = HitResult.Perfect });
            });
            AddAssert("correct actual recovery", () => gauge.Health.Value, () => Is.EqualTo(0.2 + expectedTotal / 1000).Within(0.000001));
        }

        private void assertNewIdentity()
        {
            AddAssert("new TOTAL version persisted", () => score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.Version, () => Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
            AddAssert("selected rules retained", () => score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.GaugeRulesFamily, () => Is.EqualTo(BmsGaugeRulesFamily.Beatoraja));
        }

        private sealed partial class TestDrawableRuleset : DrawableBmsRuleset
        {
            public TestDrawableRuleset(IBeatmap beatmap, IReadOnlyList<Mod> mods)
                : base(new BmsRuleset(), beatmap, mods)
            {
                InitialiseCompatibilityLayoutForTesting();
            }
        }
    }
}
