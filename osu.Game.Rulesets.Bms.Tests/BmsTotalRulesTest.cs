// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsTotalRulesTest
    {
        [TestCase("", null, false)]
        [TestCase("#TOTAL 200", 200.0, false)]
        [TestCase("#TOTAL 12.5", 12.5, false)]
        [TestCase("#TOTAL 0", null, true)]
        [TestCase("#TOTAL -100", null, true)]
        [TestCase("#TOTAL NaN", null, true)]
        [TestCase("#TOTAL Infinity", null, true)]
        [TestCase("#TOTAL 1e999", null, true)]
        [TestCase("#TOTAL invalid", null, true)]
        [TestCase("#TOTAL 200\n#TOTAL 300", 300.0, false)]
        [TestCase("#TOTAL 200\n#TOTAL -1", 200.0, true)]
        [TestCase("#TOTAL NaN\n#TOTAL 120", 120.0, true)]
        public void TestDeclarationSurvivesLoaderAndProjection(string header, double? expected, bool warning)
        {
            var source = load(header);
            Assert.That(source.DecodedChart.Warnings.Any(w => w.Contains("#TOTAL")), Is.EqualTo(warning));
            Assert.That(source.DecodedChart.BeatmapInfo.Total, Is.EqualTo(expected));
            Assert.That(source.DecodedChart.BeatmapInfo.Clone().Total, Is.EqualTo(expected));
            var ruleset = new BmsRuleset();
            Assert.That(source.TryGetCachedModlessPlayableBeatmap(ruleset.RulesetInfo, out var cached), Is.True);
            Assert.That(((BmsBeatmap)cached).BmsInfo.Total, Is.EqualTo(expected));
            var converted = (BmsBeatmap)ruleset.CreateBeatmapConverter(source).Convert();
            Assert.That(converted.BmsInfo.Total, Is.EqualTo(expected));
        }

        [TestCase(100, 260)]
        [TestCase(1000, 460.9090909090909)]
        [TestCase(2000, 573.9622641509434)]
        public void TestBeatorajaMissingTotalUsesNoteFormula(int notes, double expected)
        {
            var beatmap = playable(load("", notes));
            var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: BmsGaugeRulesFamily.Beatoraja);
            gauge.ApplyBeatmap(beatmap);
            gauge.ApplyResult(result(beatmap, HitResult.Perfect));
            Assert.That(gauge.ChartTotal, Is.EqualTo(expected).Within(1e-9));
            Assert.That(gauge.Health.Value, Is.EqualTo(0.2 + expected / notes / 100).Within(1e-9));
            Assert.That(beatmap.BmsInfo.Total, Is.Null);
        }

        [TestCase(100, 176)]
        [TestCase(400, 224)]
        [TestCase(500, 256)]
        [TestCase(600, 288)]
        [TestCase(1000, 352)]
        public void TestLr2MissingTotalUsesDocumentedCompatibilityFormula(int notes, double expected)
        {
            var beatmap = playable(load("", notes));
            var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: BmsGaugeRulesFamily.LR2);
            gauge.ApplyBeatmap(beatmap);
            gauge.ApplyResult(result(beatmap, HitResult.Perfect));
            Assert.That(gauge.ChartTotal, Is.EqualTo(expected).Within(1e-9));
            Assert.That(gauge.Health.Value, Is.EqualTo(0.2 + expected / notes / 100).Within(1e-9));
        }

        [TestCase(BmsLongNoteMode.LN, 2)]
        [TestCase(BmsLongNoteMode.CN, 3)]
        [TestCase(BmsLongNoteMode.HCN, 3)]
        public void TestAutoScratchKeepsOriginalDefaultBudgetButExcludesAssistedJudgements(BmsLongNoteMode mode, int manualNotes)
        {
            var beatmap = playable(load("", 0, "#00111:01\n#00151:0101\n#00156:" + string.Concat(Enumerable.Repeat("0101", 300))));
            mode.ApplyToBeatmap(beatmap);
            new BmsModAutoScratch().ApplyToBeatmap(beatmap);
            var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: BmsGaugeRulesFamily.LR2);
            gauge.ApplyBeatmap(beatmap);
            int originalNotes = mode == BmsLongNoteMode.LN ? 302 : 603;
            double expected = 160 + (originalNotes + Math.Clamp(originalNotes - 400, 0, 200)) * 0.16;
            Assert.That(gauge.ChartTotal, Is.EqualTo(expected).Within(1e-9));
            Assert.That(gauge.TotalHittableObjects, Is.EqualTo(manualNotes));
            Assert.That(gauge.BaseRate, Is.EqualTo(expected / manualNotes / 100).Within(1e-9));
        }

        [Test]
        public void TestAllAssistedChartHasNoRecoveryDivisionByZero()
        {
            var beatmap = playable(load(""));
            new BmsModAutoNote().ApplyToBeatmap(beatmap);
            var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: BmsGaugeRulesFamily.Beatoraja);
            gauge.ApplyBeatmap(beatmap);
            Assert.That(gauge.TotalHittableObjects, Is.Zero);
            Assert.That(gauge.ChartTotal, Is.EqualTo(460.9090909090909).Within(1e-9));
            Assert.That(gauge.BaseRate, Is.Zero);
            Assert.That(double.IsFinite(gauge.Health.Value), Is.True);
        }

        [TestCase(BmsGaugeRulesFamily.Legacy)]
        [TestCase(BmsGaugeRulesFamily.Beatoraja)]
        [TestCase(BmsGaugeRulesFamily.LR2)]
        [TestCase(BmsGaugeRulesFamily.IIDX)]
        public void TestLowDeclaredTotalAlwaysWins(BmsGaugeRulesFamily family)
        {
            var beatmap = playable(load("#TOTAL 12.5"));
            var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: family);
            gauge.ApplyBeatmap(beatmap);
            Assert.That(gauge.ChartTotal, Is.EqualTo(12.5));
        }

        [Test]
        public void TestFamilyModAfterBeatmapResolvesTotalWithoutMutatingChart()
        {
            var beatmap = playable(load(""));
            var gauge = new BmsGaugeProcessor(0);
            gauge.ApplyBeatmap(beatmap);
            Assert.That(gauge.ChartTotal, Is.EqualTo(200));
            new BmsModGaugeRulesBeatoraja().ApplyToHealthProcessor(gauge);
            Assert.That(gauge.ChartTotal, Is.EqualTo(460.9090909090909).Within(1e-9));
            gauge.SetGaugeRulesFamily(BmsGaugeRulesFamily.Legacy);
            Assert.That(gauge.ChartTotal, Is.EqualTo(200));
            Assert.That(beatmap.BmsInfo.Total, Is.Null);
        }

        [TestCase(BmsGaugeRulesFamily.Beatoraja, -3.0)]
        [TestCase(BmsGaugeRulesFamily.LR2, -4.0)]
        public void TestNormalTotalChangesRecoveryButNotBadDamage(BmsGaugeRulesFamily family, double bad)
        {
            foreach (int total in new[] { 150, 300, 600 })
            {
                var beatmap = playable(load($"#TOTAL {total}"));
                var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: family);
                gauge.ApplyBeatmap(beatmap);
                gauge.ApplyResult(result(beatmap, HitResult.Perfect));
                Assert.That(gauge.Health.Value, Is.EqualTo(0.2 + total / 100000.0).Within(1e-9));
                gauge.Health.Value = 0.8;
                gauge.ApplyResult(result(beatmap, HitResult.Meh));
                Assert.That(gauge.Health.Value, Is.EqualTo(0.8 + bad / 100).Within(1e-9));
            }
        }

        [TestCase(BmsGaugeRulesFamily.Beatoraja, 200, 0.08, -10)]
        [TestCase(BmsGaugeRulesFamily.Beatoraja, 300, 0.15, -10)]
        [TestCase(BmsGaugeRulesFamily.LR2, 200, 0.1, -15)]
        [TestCase(BmsGaugeRulesFamily.LR2, 300, 0.1, -10)]
        public void TestHardTotalRecoveryAndDamage(BmsGaugeRulesFamily family, int total, double recovery, double damage)
        {
            var beatmap = playable(load($"#TOTAL {total}"));
            var gauge = new BmsGaugeProcessor(0, BmsGaugeType.Hard, family);
            gauge.ApplyBeatmap(beatmap);
            gauge.Health.Value = 0.8;
            gauge.ApplyResult(result(beatmap, HitResult.Perfect));
            Assert.That(gauge.Health.Value, Is.EqualTo(0.8 + recovery / 100).Within(1e-9));
            gauge.Health.Value = 0.8;
            gauge.ApplyResult(result(beatmap, HitResult.Miss));
            Assert.That(gauge.Health.Value, Is.EqualTo(0.8 + damage / 100).Within(1e-9));
        }

        [TestCase(BmsGaugeRulesFamily.Beatoraja, 0.30, 0.265)]
        [TestCase(BmsGaugeRulesFamily.Beatoraja, 0.299, 0.269)]
        [TestCase(BmsGaugeRulesFamily.LR2, 0.30, 0.24)]
        [TestCase(BmsGaugeRulesFamily.LR2, 0.299, 0.263)]
        public void TestHardGutsUsesHealthBeforeDamage(BmsGaugeRulesFamily family, double start, double expected)
        {
            var beatmap = playable(load("#TOTAL 300"));
            var gauge = new BmsGaugeProcessor(0, BmsGaugeType.Hard, family);
            gauge.ApplyBeatmap(beatmap);
            gauge.Health.Value = start;
            gauge.ApplyResult(result(beatmap, HitResult.Meh));
            Assert.That(gauge.Health.Value, Is.EqualTo(expected).Within(1e-9));
        }

        [TestCase(6, 0.4728)]
        [TestCase(7, 0.4688)]
        public void TestSparseChartDamageUsesIntegerReferenceBudgetOnlyForNewScores(int version, double expected)
        {
            var beatmap = playable(load("#TOTAL 300", 10));
            var score = new ScoreInfo();
            score.SetRulesetData(new BmsScoreInfoData { Version = version, GaugeRulesFamily = BmsGaugeRulesFamily.LR2, GaugeType = BmsGaugeType.Hard });
            var gauge = BmsGaugeProcessor.CreateForScore(0, score);
            gauge.ApplyBeatmap(beatmap);
            gauge.Health.Value = 0.8;
            gauge.ApplyResult(result(beatmap, HitResult.Miss));
            Assert.That(gauge.Health.Value, Is.EqualTo(expected).Within(1e-9));
        }

        [Test]
        public void TestIidxDoesNotUseTotalForHealth()
        {
            foreach (string header in new[] { "", "#TOTAL 1", "#TOTAL 9999", "#TOTAL NaN" })
            {
                var beatmap = playable(load(header));
                var gauge = new BmsGaugeProcessor(0, gaugeRulesFamily: BmsGaugeRulesFamily.IIDX);
                gauge.ApplyBeatmap(beatmap);
                gauge.ApplyResult(result(beatmap, HitResult.Perfect));
                Assert.That(gauge.Health.Value, Is.EqualTo(0.2246090909090909).Within(1e-9));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestNewResultAndHistoryMatchLiveGauge(bool gas)
        {
            var beatmap = playable(load(""));
            var mods = new List<Mod> { new BmsModGaugeRulesBeatoraja() };
            if (gas)
                mods.Add(new BmsModGaugeAutoShift());
            var gauge = BmsGaugeProcessor.CreateForMods(0, mods);
            gauge.ApplyBeatmap(beatmap);
            var score = new ScoreInfo { Mods = mods.ToArray() };
            BmsScoreInfoData.InitialiseNewPlay(score);
            for (int i = 0; i < 140; i++)
            {
                var type = i < 40 ? HitResult.Miss : HitResult.Perfect;
                var obj = beatmap.HitObjects[i];
                gauge.ApplyResult(new JudgementResult(obj, obj.CreateJudgement()) { Type = type });
                score.HitEvents.Add(new HitEvent(0, 1, type, obj, null, null));
                score.Statistics[type] = score.Statistics.GetValueOrDefault(type) + 1;
            }
            score.MaximumStatistics[HitResult.Perfect] = 1000;
            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);
            Assert.That(score.GetRulesetData<BmsScoreInfoData>()!.Version, Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
            Assert.That(BmsClearLampProcessor.CalculateFinalGauge(score, beatmap), Is.EqualTo(gauge.Health.Value).Within(1e-9));
            Assert.That(score.GetRulesetData<BmsScoreInfoData>()!.FinalGauge, Is.EqualTo(gauge.Health.Value).Within(1e-9));
            Assert.That(BmsClearLampProcessor.CreateGaugeHistory(score, beatmap).Timelines.Last().Samples.Last().Value,
                Is.EqualTo(gauge.Health.Value).Within(1e-9));
        }

        [Test]
        public void TestOldScoreKeepsTwoHundredFallbackAndStoredResult()
        {
            var beatmap = playable(load(""));
            var score = new ScoreInfo();
            score.SetRulesetData(new BmsScoreInfoData
            {
                Version = 6,
                GaugeRulesFamily = BmsGaugeRulesFamily.Beatoraja,
                GaugeType = BmsGaugeType.Normal,
                FinalGauge = 0.202,
                ClearLamp = BmsClearLamp.Failed,
            });
            var obj = beatmap.HitObjects[0];
            score.HitEvents.Add(new HitEvent(0, 1, HitResult.Perfect, obj, null, null));
            Assert.That(BmsClearLampProcessor.CalculateFinalGauge(score, beatmap), Is.EqualTo(0.202).Within(1e-9));
            Assert.That(BmsClearLampProcessor.CreateGaugeHistory(score, beatmap).Timelines.Single().Samples.Last().Value,
                Is.EqualTo(0.202).Within(1e-9));
            Assert.That(BmsClearLampProcessor.Calculate(score, beatmap, out double final), Is.EqualTo(BmsClearLamp.Failed));
            Assert.That(final, Is.EqualTo(0.202));
            new BmsRuleset().PrepareScoreInfoForResults(score, beatmap);
            Assert.That(score.GetRulesetData<BmsScoreInfoData>()!.Version, Is.EqualTo(6));
            Assert.That(score.GetRulesetData<BmsScoreInfoData>()!.FinalGauge, Is.EqualTo(0.202));
        }

        [Test]
        public void TestMissingScoreDataIsHistoricalButRecordingStartsCurrentRules()
        {
            var beatmap = playable(load(""));
            var score = new Score { ScoreInfo = new ScoreInfo { Mods = new Mod[] { new BmsModGaugeRulesLr2(), new BmsModGaugeHard() } } };
            var old = BmsGaugeProcessor.CreateForScore(0, score.ScoreInfo);
            old.ApplyBeatmap(beatmap);
            Assert.That(old.ChartTotal, Is.EqualTo(200));
            _ = new BmsReplayRecorder(score);
            var current = BmsGaugeProcessor.CreateForScore(0, score.ScoreInfo);
            current.ApplyBeatmap(beatmap);
            Assert.That(current.ChartTotal, Is.EqualTo(352));
            Assert.That(current.GaugeType, Is.EqualTo(BmsGaugeType.Hard));
        }

        [TestCase(BmsGaugeRulesFamily.Legacy)]
        [TestCase(BmsGaugeRulesFamily.Beatoraja)]
        [TestCase(BmsGaugeRulesFamily.LR2)]
        [TestCase(BmsGaugeRulesFamily.IIDX)]
        public void TestLargeFiniteTotalDoesNotCorruptHealth(BmsGaugeRulesFamily family)
        {
            var beatmap = playable(load("#TOTAL 1.7976931348623157E+308"));
            foreach (var type in new[] { BmsGaugeType.Easy, BmsGaugeType.Hard, BmsGaugeType.ExHard })
            {
                var gauge = new BmsGaugeProcessor(0, type, family);
                gauge.ApplyBeatmap(beatmap);
                foreach (var judgement in new[] { HitResult.Perfect, HitResult.Great, HitResult.Meh, HitResult.Miss })
                {
                    gauge.ApplyResult(result(beatmap, judgement));
                    Assert.That(double.IsFinite(gauge.Health.Value), Is.True);
                    Assert.That(gauge.Health.Value, Is.InRange(gauge.CurrentFloorGauge, gauge.CurrentMaximumGauge));
                }
            }
        }

        private static JudgementResult result(BmsBeatmap beatmap, HitResult type)
        {
            var obj = beatmap.HitObjects[0];
            return new JudgementResult(obj, obj.CreateJudgement()) { Type = type };
        }

        private static BmsDecodedBeatmap load(string header, int notes = 1000, string? channels = null)
        {
            string chart = $"#BPM 120\n{header}\n" + (channels ?? $"#00111:{string.Concat(Enumerable.Repeat("01", notes))}\n");
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(chart));
            return (BmsDecodedBeatmap)new BmsBeatmapLoader().Load(stream, "total.bme", new BeatmapInfo(new BmsRuleset().RulesetInfo));
        }

        private static BmsBeatmap playable(BmsDecodedBeatmap source)
        {
            Assert.That(source.TryGetCachedModlessPlayableBeatmap(new BmsRuleset().RulesetInfo, out var beatmap), Is.True);
            return (BmsBeatmap)beatmap;
        }
    }
}
