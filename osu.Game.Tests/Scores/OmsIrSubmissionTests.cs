// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Mods;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Tests.Scores
{
    [TestFixture]
    public class OmsIrSubmissionTests
    {
        [TestCase(30000016)]
        [TestCase(30000015)]
        public void TestSubmissionUsesTheActualSavedIdAndFinalScoreVersion(int scoreVersion)
        {
            var source = createSource();
            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source);
            var score = createScore(source);

            // Local import may reuse an existing score ID. The captured gameplay ID is not authoritative.
            Guid importedId = Guid.NewGuid();
            score.ID = importedId;
            score.TotalScoreVersion = scoreVersion;
            score.TotalScore = 987654;
            score.OnlineID = 456;

            var submission = context.Create(score);
            score.TotalScore = 1;

            Assert.Multiple(() =>
            {
                Assert.That(submission.SubmissionId, Is.EqualTo(importedId));
                Assert.That(submission.Payload.Value<string>("submission_id"), Is.EqualTo(importedId.ToString("D")));
                Assert.That(submission.Payload.Value<int>("total_score_version"), Is.EqualTo(scoreVersion));
                Assert.That(submission.Payload.Value<long>("total_score"), Is.EqualTo(987654));
                Assert.That(submission.Payload["online_id"], Is.Null);
                Assert.That(score.OnlineID, Is.EqualTo(456));
            });
        }

        [Test]
        public void TestActualManiaStagesDetermineTheKeymode()
        {
            var source = createSource();
            source.Difficulty.CircleSize = 4;
            var playable = new ManiaBeatmap(new StageDefinition(7));
            playable.Stages.Add(new StageDefinition(7));

            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(playable, source);
            playable.Stages.Clear();

            var payload = context.Create(createScore(source)).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload.Value<string>("keymode"), Is.EqualTo("mania_14k"));
                Assert.That(payload["ruleset_data"]!.Type, Is.EqualTo(JTokenType.Null));
                Assert.That(payload["bms_chart"]!.Type, Is.EqualTo(JTokenType.Null));
            });
        }

        [Test]
        public void TestCapturedChartIdentitySurvivesMetadataChanges()
        {
            var source = createSource();
            var score = createScore(source);
            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source);

            source.MD5Hash = new string('c', 32);
            source.Hash = new string('d', 64);
            source.Metadata.Title = "Changed after gameplay started";

            var payload = context.Create(score).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload["chart"]!.Value<string>("md5"), Is.EqualTo(new string('a', 32)));
                Assert.That(payload["chart"]!.Value<string>("sha256"), Is.EqualTo(new string('b', 64)));
                Assert.That(payload["chart"]!.Value<string>("title"), Is.EqualTo("IR score test"));
            });
        }

        [Test]
        public void TestUnsupportedRealModsAndTheirParametersArePreserved()
        {
            var source = createSource();
            var score = createScore(source);
            var mod = new ManiaModDoubleTime();
            mod.SpeedChange.Value = 1.25;
            score.Mods = new Mod[] { mod };

            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source);
            var payload = context.Create(score).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload["mods"]![0]!.Value<string>("acronym"), Is.EqualTo("DT"));
                Assert.That(payload["mods"]![0]!["settings"]!.Value<double>("speed_change"), Is.EqualTo(1.25));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSupportedNativeManiaDefaultModsMatchTheFirstContract(bool autoplay)
        {
            var source = createSource();
            source.Metadata.Title = "合成契约样例 Mania 4K（非玩家成绩）";
            source.Metadata.Artist = "OMS synthetic verification";
            source.DifficultyName = "Synthetic IR contract";
            var score = createScore(source);
            score.Mods = autoplay ? new Mod[] { new ManiaModAutoplay() } : Array.Empty<Mod>();
            score.ClientVersion = "synthetic-client-contract";
            score.Date = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero);
            score.TotalScore = 1000000;
            score.Accuracy = 1;
            score.Statistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 100 };
            score.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 100 };

            // This synthetic saved identity is stable across API probes; it does not claim a Realm import or player run.
            score.ID = Guid.Parse("0d9c073d-5733-4840-af0e-ad8ef24c5981");
            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source);

            var submission = context.Create(score);
            var mods = (JArray)submission.Payload["mods"]!;

            Assert.That(mods.Count, Is.EqualTo(autoplay ? 1 : 0));

            if (autoplay)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(mods[0].Value<string>("acronym"), Is.EqualTo("AT"));
                    Assert.That((JObject)mods[0]["settings"]!, Is.Empty);
                });
            }

            string? fixtureDirectory = Environment.GetEnvironmentVariable("OMS_IR_CONTRACT_FIXTURE_DIRECTORY");

            if (!autoplay && !string.IsNullOrEmpty(fixtureDirectory))
            {
                Directory.CreateDirectory(fixtureDirectory);
                File.WriteAllText(Path.Combine(fixtureDirectory, "mania-contract-v1.json"), submission.Payload.ToString(Formatting.Indented));
            }
        }

        [Test]
        public void TestStatisticsUseTheProtocolNamesAndRetainBonusJudgements()
        {
            var source = createSource();
            var score = createScore(source);
            score.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Perfect] = 3,
                [HitResult.SmallTickHit] = 2,
                [HitResult.SmallBonus] = 1,
                [HitResult.LargeBonus] = 1,
                [HitResult.Miss] = 0,
            };
            score.MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Perfect] = 3, [HitResult.SmallTickHit] = 2 };
            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source);

            var payload = context.Create(score).Payload;

            Assert.Multiple(() =>
            {
                Assert.That(payload["statistics"]!.Value<int>("small_tick_hit"), Is.EqualTo(2));
                Assert.That(payload["statistics"]!.Value<int>("small_bonus"), Is.EqualTo(1));
                Assert.That(payload["statistics"]!.Value<int>("large_bonus"), Is.EqualTo(1));
                Assert.That(payload["statistics"]!["miss"], Is.Null);
                Assert.That(payload["maximum_statistics"]!.Value<int>("perfect"), Is.EqualTo(3));
            });
        }

        [Test]
        public void TestDifferentChartOrRulesetCannotBorrowTheCapturedPlay()
        {
            var source = createSource();
            var score = createScore(source);
            var context = new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source);

            score.BeatmapHash = new string('c', 64);
            Assert.Throws<InvalidOperationException>(() => context.Create(score));

            score.BeatmapHash = source.Hash;
            score.Ruleset.ShortName = "bms";
            Assert.Throws<InvalidOperationException>(() => context.Create(score));
        }

        [Test]
        public void TestChartWithoutVerifiedRawIdentityIsNotSubmitted()
        {
            var source = createSource();
            source.Hash = string.Empty;

            Assert.Throws<InvalidOperationException>(() => new ManiaRuleset().CaptureOmsIrSubmissionContext(new ManiaBeatmap(new StageDefinition(4)), source));
        }

        private static BeatmapInfo createSource() => new BeatmapInfo(new ManiaRuleset().RulesetInfo)
        {
            MD5Hash = new string('a', 32),
            Hash = new string('b', 64),
            DifficultyName = "Normal",
            Metadata = new BeatmapMetadata { Title = "IR score test", Artist = "OMS test" },
        };

        private static ScoreInfo createScore(BeatmapInfo source) => new ScoreInfo(source, new ManiaRuleset().RulesetInfo)
        {
            ClientVersion = "OMS IR test",
            Date = new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero),
            TotalScore = 900000,
            Accuracy = 0.9,
            MaxCombo = 100,
        };
    }
}
