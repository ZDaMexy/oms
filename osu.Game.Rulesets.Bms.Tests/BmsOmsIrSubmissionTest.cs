// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsOmsIrSubmissionTest
    {
        [TestCase("chart.bms", "#00111:01\n#00112:01\n#00113:01\n#00114:01\n#00115:01", "bms_5k")]
        [TestCase("chart.bme", "#00111:01", "bms_7k")]
        [TestCase("chart.bms", "#00117:01", "bms_9k")]
        [TestCase("chart.pms", "#00111:01", "pms_9k")]
        [TestCase("chart.bme", "#00122:01", "bms_14k")]
        public void TestParserResolvedKeymodeAndSourceIdentityAreUsed(string filename, string channels, string expectedKeymode)
        {
            var chart = new BmsBeatmapDecoder().DecodeText($"#TITLE IR score test\n#BPM 120\n#RANK 3\n{channels}", filename);
            var ruleset = new BmsRuleset();
            var playable = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), ruleset).Convert();
            var source = createSource(ruleset);
            playable.BeatmapInfo.Difficulty.CircleSize = 9;

            var context = ruleset.CaptureOmsIrSubmissionContext(playable, source);
            var score = createScore(source, ruleset);
            score.SetRulesetData(new BmsScoreInfoData { Version = 7, FinalGauge = 1, ClearLamp = BmsClearLamp.Perfect });
            var payload = context.Create(score).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload.Value<string>("keymode"), Is.EqualTo(expectedKeymode));
                Assert.That(payload["chart"]!.Value<string>("sha256"), Is.EqualTo(source.Hash));
                Assert.That(payload["bms_chart"]!.Value<int>("judge_rank"), Is.EqualTo(3));
                Assert.That(payload["bms_chart"]!.Value<string>("branch_policy"), Is.EqualTo("fixed-1-v1"));
            });
        }

        [Test]
        public void TestFinalNativeBmsResultsAndEmptyPoorAreProjectedAfterPreparation()
        {
            var ruleset = new BmsRuleset();
            var source = createSource(ruleset);
            source.MD5Hash = new string('c', 32);
            source.Hash = new string('d', 64);
            source.Metadata.Title = "合成契约样例 BMS 7K（非玩家成绩）";
            source.Metadata.Artist = "OMS synthetic verification";
            source.DifficultyName = "Synthetic IR contract";
            var playable = new BmsBeatmap();
            var note = new BmsHitObject { StartTime = 1000, LaneIndex = 1 };
            playable.HitObjects.Add(note);
            var score = createScore(source, ruleset);
            score.ClientVersion = "synthetic-client-contract";
            score.Date = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
            score.Statistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1, [HitResult.Ok] = 1 };
            score.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 };
            score.HitEvents = new List<HitEvent>
            {
                new HitEvent(0, 1, HitResult.Perfect, note, null, null),
                new HitEvent(0, 1, HitResult.Ok, new BmsEmptyPoorHitObject { StartTime = 1100 }, note, null),
            };

            var visualOffset = new BmsVisualOffsetTimeline { InitialOffset = 15 };
            var recorder = new BmsReplayRecorder(new Score { ScoreInfo = score }, visualOffset);
            recorder.EndRecording();
            var context = ruleset.CaptureOmsIrSubmissionContext(playable, source);
            Assert.Throws<InvalidOperationException>(() => context.Create(score));

            ruleset.PrepareScoreInfoForResults(score, playable);
            var finalData = score.GetRulesetData<BmsScoreInfoData>()!;

            // This synthetic saved identity is stable across API probes; it does not claim a Realm import or player run.
            score.ID = Guid.Parse("8bcde376-ceec-4f54-a08a-034d596ceee3");
            var submission = context.Create(score);
            var payload = submission.Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload["statistics"]!.Value<int>("ok"), Is.EqualTo(1));
                Assert.That(payload["maximum_statistics"]!.Value<int>("perfect"), Is.EqualTo(1));
                Assert.That(payload["ruleset_data"]!.Value<int>("version"), Is.EqualTo(7));
                Assert.That(payload["ruleset_data"]!["clear_lamp"]!.Type, Is.EqualTo(JTokenType.Integer));
                Assert.That(payload["ruleset_data"]!.Value<int>("clear_lamp"), Is.EqualTo((int)finalData.ClearLamp!.Value));
                Assert.That(payload["ruleset_data"]!.Value<double>("final_gauge"), Is.EqualTo(finalData.FinalGauge));
                Assert.That(finalData.ClearLamp, Is.EqualTo(BmsClearLamp.Failed));
                Assert.That(score.Passed, Is.True, "Local completion is retained even below the final clear threshold.");
                Assert.That(payload.Value<bool>("passed"), Is.False, "IR clearance follows the final lamp, not mere local completion.");
                Assert.That(payload["ruleset_data"]!["visual_offset"], Is.Null);
                Assert.That(finalData.VisualOffset!.InitialOffset, Is.EqualTo(15));
            });

            string? fixtureDirectory = Environment.GetEnvironmentVariable("OMS_IR_CONTRACT_FIXTURE_DIRECTORY");

            if (!string.IsNullOrEmpty(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                File.WriteAllText(Path.Combine(fixtureDirectory, "bms-contract-v1.json"), payload.ToString(Formatting.Indented));
            }
        }

        [TestCase("ASCR")]
        [TestCase("ANOT")]
        [TestCase("AT")]
        public void TestSupportedNativeDefaultModsMatchTheFirstContract(string acronym)
        {
            var ruleset = new BmsRuleset();
            var source = createSource(ruleset);
            var score = createScore(source, ruleset);
            score.Mods = new[] { ruleset.CreateModFromAcronym(acronym)! };
            score.SetRulesetData(new BmsScoreInfoData { Version = 7, FinalGauge = 1, ClearLamp = BmsClearLamp.Perfect });

            var payload = ruleset.CaptureOmsIrSubmissionContext(new BmsBeatmap(), source).Create(score).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload["mods"]![0]!.Value<string>("acronym"), Is.EqualTo(acronym));
                Assert.That((JObject)payload["mods"]![0]!["settings"]!, Is.Empty);
            });
        }

        [Test]
        public void TestNonDefaultAssistSettingsAreRetainedForAnExplicitServiceRejection()
        {
            var ruleset = new BmsRuleset();
            var source = createSource(ruleset);
            var score = createScore(source, ruleset);
            var assist = new BmsModAutoScratch();
            assist.TintScratchNotes.Value = false;
            score.Mods = new Mod[] { assist };
            score.SetRulesetData(new BmsScoreInfoData { Version = 7, FinalGauge = 1, ClearLamp = BmsClearLamp.Perfect });

            var payload = ruleset.CaptureOmsIrSubmissionContext(new BmsBeatmap(), source).Create(score).Payload;

            Assert.That(payload["mods"]![0]!["settings"]!.Value<bool>("tint_scratch_notes"), Is.False);
        }

        [TestCase(1, "LR2", "judge_mode", "LR2")]
        [TestCase(2, "BRJ", "judge_mode", "Beatoraja")]
        [TestCase(3, "IIDXJ", "judge_mode", "IIDX")]
        [TestCase(4, "LR2G", "gauge_rules_family", "LR2")]
        [TestCase(5, "BRG", "gauge_rules_family", "Beatoraja")]
        [TestCase(6, "IIDXG", "gauge_rules_family", "IIDX")]
        [TestCase(7, "A-EASY", "gauge_type", "AssistEasy")]
        [TestCase(8, "EASY", "gauge_type", "Easy")]
        [TestCase(9, "HARD", "gauge_type", "Hard")]
        [TestCase(10, "EX-HARD", "gauge_type", "ExHard")]
        [TestCase(11, "HAZARD", "gauge_type", "Hazard")]
        [TestCase(12, "CN", "long_note_mode", "CN")]
        [TestCase(13, "HCN", "long_note_mode", "HCN")]
        public void TestSavedNativeRuleModProjectsItsActualFinalAxis(int fixtureId, string acronym, string field, string expected)
        {
            var ruleset = new BmsRuleset();
            JObject payload = createPreparedPayload(fixtureId, new[] { ruleset.CreateModFromAcronym(acronym)! });

            Assert.Multiple(() =>
            {
                Assert.That(payload["mods"]![0]!.Value<string>("acronym"), Is.EqualTo(acronym));
                Assert.That((JObject)payload["mods"]![0]!["settings"]!, Is.Empty);
                Assert.That(payload["ruleset_data"]!.Value<string>(field), Is.EqualTo(expected));
                if (field == "gauge_type")
                {
                    Assert.That(payload["ruleset_data"]!.Value<string>("starting_gauge_type"), Is.EqualTo(expected));
                    Assert.That(payload["ruleset_data"]!.Value<string>("floor_gauge_type"), Is.EqualTo(expected));
                    Assert.That(payload["ruleset_data"]!.Value<bool>("gauge_auto_shift"), Is.False);
                }
            });

            exportFixture($"bms-m1-{fixtureId:D2}-{acronym.ToLowerInvariant()}.json", payload);
        }

        [Test]
        public void TestSavedLr2PlayKeepsFiveRealAxesAndTheSavedUuid()
        {
            var ruleset = new BmsRuleset();
            string[] acronyms = { "ASCR", "LR2", "LR2G", "HARD", "HCN" };
            JObject payload = createPreparedPayload(14, acronyms.Select(acronym => ruleset.CreateModFromAcronym(acronym)!).ToArray());

            Assert.Multiple(() =>
            {
                Assert.That(payload["mods"]!.Select(mod => mod.Value<string>("acronym")), Is.EqualTo(acronyms));
                Assert.That(payload["mods"]!.All(mod => !((JObject)mod["settings"]!).HasValues), Is.True);
                Assert.That(payload["ruleset_data"]!.Value<string>("judge_mode"), Is.EqualTo("LR2"));
                Assert.That(payload["ruleset_data"]!.Value<string>("gauge_rules_family"), Is.EqualTo("LR2"));
                Assert.That(payload["ruleset_data"]!.Value<string>("gauge_type"), Is.EqualTo("Hard"));
                Assert.That(payload["ruleset_data"]!.Value<string>("long_note_mode"), Is.EqualTo("HCN"));
            });

            exportFixture("bms-m1-14-lr2-five-axes.json", payload);
        }

        [TestCase(20, null, null, BmsGaugeType.ExHard, BmsGaugeType.Easy, true)]
        [TestCase(21, BmsGaugeType.Hazard, BmsGaugeType.AssistEasy, BmsGaugeType.Hazard, BmsGaugeType.AssistEasy, true)]
        [TestCase(22, BmsGaugeType.Normal, BmsGaugeType.Hazard, BmsGaugeType.Normal, BmsGaugeType.Normal, false)]
        public void TestSavedGasSettingsKeepOriginalIntegersAndActualEffectiveGauges(int fixtureId, BmsGaugeType? configuredStart,
                                                                                  BmsGaugeType? configuredFloor, BmsGaugeType expectedStart,
                                                                                  BmsGaugeType expectedFloor, bool downgrade)
        {
            var gas = new BmsModGaugeAutoShift();
            if (configuredStart.HasValue)
                gas.StartingGauge.Value = configuredStart.Value;
            if (configuredFloor.HasValue)
                gas.FloorGauge.Value = configuredFloor.Value;
            JObject payload = createPreparedPayload(fixtureId, new Mod[] { gas }, downgrade);
            var settings = (JObject)payload["mods"]![0]!["settings"]!;
            var data = (JObject)payload["ruleset_data"]!;
            BmsGaugeType active = Enum.Parse<BmsGaugeType>(data.Value<string>("gauge_type")!);

            Assert.Multiple(() =>
            {
                Assert.That(payload["mods"]![0]!.Value<string>("acronym"), Is.EqualTo("GAS"));
                Assert.That(data.Value<bool>("gauge_auto_shift"), Is.True);
                Assert.That(data.Value<string>("starting_gauge_type"), Is.EqualTo(expectedStart.ToString()));
                Assert.That(data.Value<string>("floor_gauge_type"), Is.EqualTo(expectedFloor.ToString()));
                Assert.That(active, Is.InRange(expectedFloor, expectedStart));
                if (downgrade)
                    Assert.That(active, Is.LessThan(expectedStart), "The final active gauge comes from prepared hit events, not the configured start.");
                if (configuredStart.HasValue)
                {
                    Assert.That(settings["starting_gauge"]!.Type, Is.EqualTo(JTokenType.Integer));
                    Assert.That(settings.Value<int>("starting_gauge"), Is.EqualTo((int)configuredStart.Value));
                    Assert.That(settings["floor_gauge"]!.Type, Is.EqualTo(JTokenType.Integer));
                    Assert.That(settings.Value<int>("floor_gauge"), Is.EqualTo((int)configuredFloor!.Value));
                }
                else
                    Assert.That(settings, Is.Empty, "Default GAS settings stay empty; effective values are carried by the final rule axes.");
            });

            exportFixture($"bms-m1-{fixtureId:D2}-gas.json", payload);
        }

        private static JObject createPreparedPayload(int fixtureId, Mod[] mods, bool downgrade = false)
        {
            var ruleset = new BmsRuleset();
            var chart = new BmsBeatmapDecoder().DecodeText("#TITLE OMS IR synthetic M1\n#BPM 120\n#RANK 3\n#TOTAL 800\n#00111:" + string.Concat(Enumerable.Repeat("01", 100)), "synthetic.bme");
            var playable = (BmsBeatmap)new BmsBeatmapConverter(new BmsDecodedBeatmap(chart), ruleset).Convert();
            var source = createSource(ruleset);
            source.Metadata.Title = "合成 M1 保存契约样例（非玩家成绩）";
            source.Metadata.Artist = "OMS synthetic verification";
            var context = ruleset.CaptureOmsIrSubmissionContext(playable, source);
            var score = createScore(source, ruleset);
            score.Mods = mods;
            score.ClientVersion = "OMS synthetic M1 contract";
            score.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 100 };
            score.Statistics = downgrade
                ? new Dictionary<HitResult, int> { [HitResult.Meh] = 30, [HitResult.Perfect] = 70 }
                : new Dictionary<HitResult, int> { [HitResult.Perfect] = 100 };
            score.MaxCombo = downgrade ? 70 : 100;
            score.TotalScore = BmsScoreProcessor.CalculateExScore(score.Statistics);
            score.Accuracy = (double)score.TotalScore / 200;
            score.HitEvents = playable.HitObjects.Select((note, index) => new HitEvent(0, 1, downgrade && index < 30 ? HitResult.Meh : HitResult.Perfect,
                note, index > 0 ? playable.HitObjects[index - 1] : null, null)).ToList();
            new BmsReplayRecorder(new Score { ScoreInfo = score }).EndRecording();
            ruleset.PrepareScoreInfoForResults(score, playable);

            // These identities belong only to exported synthetic contracts; real plays use the locally saved UUID.
            score.ID = Guid.Parse("d14d1800-0000-4000-8000-" + fixtureId.ToString("D12"));
            ScoreInfo saved = score.DeepClone();
            JObject payload = context.Create(saved).Payload;
            Assert.Multiple(() =>
            {
                Assert.That(payload.Value<string>("submission_id"), Is.EqualTo(saved.ID.ToString("D")));
                Assert.That(payload.Value<string>("keymode"), Is.EqualTo("bms_7k"));
                Assert.That(payload["bms_chart"]!.Value<int>("judge_rank"), Is.EqualTo(3));
                Assert.That(payload["ruleset_data"]!.Value<int>("version"), Is.EqualTo(7));
                Assert.That(payload["ruleset_data"]!["clear_lamp"]!.Type, Is.EqualTo(JTokenType.Integer));
                Assert.That(JToken.DeepEquals(context.Create(saved).Payload, payload), Is.True);
            });
            return payload;
        }

        private static void exportFixture(string name, JObject payload)
        {
            string? fixtureDirectory = Environment.GetEnvironmentVariable("OMS_IR_CONTRACT_FIXTURE_DIRECTORY");
            if (string.IsNullOrEmpty(fixtureDirectory))
                return;
            Directory.CreateDirectory(fixtureDirectory);
            File.WriteAllText(Path.Combine(fixtureDirectory, name), payload.ToString(Formatting.Indented));
        }

        private static BeatmapInfo createSource(BmsRuleset ruleset) => new BeatmapInfo(ruleset.RulesetInfo)
        {
            MD5Hash = new string('a', 32),
            Hash = new string('b', 64),
            Metadata = new BeatmapMetadata { Title = "IR score test", Artist = "OMS test" },
            DifficultyName = "Normal",
        };

        private static ScoreInfo createScore(BeatmapInfo source, BmsRuleset ruleset) => new ScoreInfo(source, ruleset.RulesetInfo)
        {
            ClientVersion = "OMS IR test",
            Date = new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero),
            TotalScore = 2,
            Accuracy = 1,
            MaxCombo = 1,
            Statistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 },
            MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 1 },
        };
    }
}
