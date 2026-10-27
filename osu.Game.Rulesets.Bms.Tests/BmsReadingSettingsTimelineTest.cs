// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsReadingSettingsTimelineTest
    {
        private static BmsReadingSettingsSnapshot initial => new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Normal, 8, 2.5, 3.5,
            true, true, true, true, 250, 125, 80, 650, 750);

        [Test]
        public void TestSameFrameAdjustmentsKeepTheFinalWholeStateAndSeekInBothDirections()
        {
            var timeline = new BmsReadingSettingsTimeline();
            var covered = initial with { HiSpeedMode = BmsHiSpeedMode.Floating, FloatingHiSpeed = 1.75, SuddenUnits = 300, HiddenUnits = 175 };
            var uncovered = covered with { LaneCoversEnabled = false, LiftUnits = 60, SuddenOpacity = 900 };
            var restored = uncovered with { LaneCoversEnabled = true, SuddenUnits = 325, HiddenUnits = 200, HiddenOpacity = 800 };
            timeline.Record(1000, covered with { SuddenUnits = 299 }, initial);
            timeline.Record(1000, covered, initial);
            timeline.Record(1500, covered, initial);
            timeline.Record(2000, uncovered, initial);
            timeline.Record(3000, restored, initial);
            timeline.Validate();

            Assert.Multiple(() =>
            {
                Assert.That(timeline.Changes.Select(change => change.Time), Is.EqualTo(new[] { 1000d, 2000d, 3000d }));
                Assert.That(timeline.SettingsAt(999, initial), Is.EqualTo(initial));
                Assert.That(timeline.SettingsAt(1000, initial), Is.EqualTo(covered));
                Assert.That(timeline.SettingsAt(2999, initial), Is.EqualTo(uncovered));
                Assert.That(timeline.SettingsAt(3000, initial), Is.EqualTo(restored));
                Assert.That(timeline.SettingsAt(1500, initial), Is.EqualTo(covered));
                Assert.That(timeline.SettingsAt(-400, initial), Is.EqualTo(initial));
            });
        }

        [Test]
        public void TestSameFrameReturnToPreviousStateDoesNotRecordAnIntermediateAdjustment()
        {
            var timeline = new BmsReadingSettingsTimeline();
            timeline.Record(-400, initial with { SuddenUnits = 300 }, initial);
            timeline.Record(-400, initial, initial);
            Assert.That(timeline.Changes, Is.Empty);
        }

        [Test]
        public void TestRecordingEndKeepsReadingHistorySeparateFromInputAndOriginalScoreConditions()
        {
            var score = new Score { ScoreInfo = { Ruleset = new BmsRuleset().RulesetInfo, Mods = new Mod[] { new BmsModMirror() } } };
            string originalMods = score.ScoreInfo.ModsJson;
            var adjusted = initial with { HiSpeedMode = BmsHiSpeedMode.Floating, FloatingHiSpeed = 1.75, LaneCoversEnabled = false, LiftUnits = 60 };
            using var recorder = new BmsReplayRecorder(score, null, initial);
            recorder.RecordReadingSettings(1200, adjusted);
            recorder.EndRecording();
            recorder.RecordReadingSettings(1300, adjusted with { LiftUnits = 40 });
            recorder.EndRecording();

            var saved = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
            Assert.Multiple(() =>
            {
                Assert.That(saved.Version, Is.EqualTo(BmsScoreInfoData.TOTAL_RULES_VERSION));
                Assert.That(saved.ReadingSettings, Is.EqualTo(initial));
                Assert.That(saved.ReadingSettingsTimeline!.Changes, Has.Count.EqualTo(1));
                Assert.That(saved.ReadingSettingsTimeline.SettingsAt(1300, initial), Is.EqualTo(adjusted));
                Assert.That(score.Replay.Frames, Is.Empty, "Reading controls must not invent gameplay input frames.");
                Assert.That(score.ScoreInfo.ModsJson, Is.EqualTo(originalMods));
            });

            var exported = LegacyReplaySoloScoreInfo.FromScore(score.ScoreInfo);
            var imported = JsonConvert.DeserializeObject<LegacyReplaySoloScoreInfo>(JsonConvert.SerializeObject(exported))!;
            var replayScore = new ScoreInfo { Ruleset = new BmsRuleset().RulesetInfo, APIMods = imported.Mods, RulesetDataJson = imported.RulesetDataJson! };
            var replayData = replayScore.GetRulesetData<BmsScoreInfoData>()!;
            replayData.ReadingSettingsTimeline!.Validate();
            Assert.That(replayData.ReadingSettingsTimeline.SettingsAt(1200, replayData.ReadingSettings!.Value), Is.EqualTo(adjusted));
            Assert.That(replayScore.ModsJson, Is.EqualTo(originalMods));
            var json = JObject.Parse(replayScore.RulesetDataJson);
            Assert.That(((JObject)json["reading_settings_timeline"]!["changes"]![0]!["settings"]!).Properties().Count(), Is.EqualTo(13));
        }

        [Test]
        public void TestRecordRejectsBackwardTimeInsteadOfSortingAFalseHistory()
        {
            var timeline = new BmsReadingSettingsTimeline();
            timeline.Record(2000, initial with { SuddenUnits = 300 }, initial);
            Assert.That(() => timeline.Record(1000, initial, initial), Throws.TypeOf<InvalidOperationException>());
            Assert.That(timeline.Changes.Single().Time, Is.EqualTo(2000));
        }

        [TestCase(1000, 1000)]
        [TestCase(2000, 1000)]
        [TestCase(double.NaN, 2000)]
        [TestCase(1000, double.PositiveInfinity)]
        public void TestExternalTimelineRejectsInvalidOrderAndNonFiniteTimes(double first, double second)
        {
            var timeline = new BmsReadingSettingsTimeline
            {
                Changes = new List<BmsReadingSettingsTimeline.Change>
                {
                    new BmsReadingSettingsTimeline.Change { Time = first, Settings = initial },
                    new BmsReadingSettingsTimeline.Change { Time = second, Settings = initial },
                },
            };
            Assert.That(timeline.Validate, Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void TestExternalTimelineRejectsMissingChangesAndInvalidSnapshots()
        {
            var timeline = new BmsReadingSettingsTimeline { Changes = null! };
            Assert.That(timeline.Validate, Throws.TypeOf<InvalidDataException>());
            timeline.Changes = new List<BmsReadingSettingsTimeline.Change> { null! };
            Assert.That(timeline.Validate, Throws.TypeOf<InvalidDataException>());
            timeline.Changes[0] = new BmsReadingSettingsTimeline.Change { Time = 1000, Settings = initial with { HiddenUnits = float.NaN } };
            Assert.That(timeline.Validate, Throws.TypeOf<InvalidDataException>());
        }
    }
}
