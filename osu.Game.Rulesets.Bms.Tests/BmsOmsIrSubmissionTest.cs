// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
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
