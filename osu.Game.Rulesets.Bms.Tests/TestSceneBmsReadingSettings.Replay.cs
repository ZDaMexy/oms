// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using osu.Game.Replays;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Replays;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsReadingSettings
    {
        [Test]
        public void TestRecordedReadingChangesReplayAtTheirGameplayTimesAndRestoreOnSeek()
        {
            var initial = settings(BmsHiSpeedMode.Normal);
            var personal = initial with { HiSpeedMode = BmsHiSpeedMode.Classic, ClassicHiSpeed = 4.25, SuddenEnabled = false, HiddenEnabled = false, LiftEnabled = false };
            BmsReadingSettingsSnapshot first = initial;
            BmsReadingSettingsSnapshot uncovered = initial;
            BmsReadingSettingsSnapshot opacity = initial;
            BmsReadingSettingsSnapshot restored = initial;
            Score score = null!;
            prepare(initial, remember: false);
            loadGame();
            AddStep("start the actual native recorder", () => renderer.SetRecordTarget(score = new Score()));
            AddAssert("new recording explicitly owns the legacy reading calculation", () => score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()?.ReadingCalculationVersion,
                () => Is.EqualTo(0));
            advanceReadingClock(1500);
            AddStep("complete mode and Floating scratch changes at one gameplay time", () =>
            {
                Assert.That(renderer.ToggleHiSpeedMode(), Is.True);
                renderer.SetAdjustmentHeld(true);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                first = initial with
                {
                    HiSpeedMode = BmsHiSpeedMode.Floating,
                    FloatingHiSpeed = renderer.SelectedHiSpeed.Value,
                    SuddenUnits = renderer.SuddenUnits,
                    HiddenUnits = renderer.HiddenUnits,
                };
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial), "Recording is independent of gameplay Remember.");
            });
            advanceReadingClock(2200);
            AddStep("hide the combination and move Lift at the next recorded time", () =>
            {
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
                Assert.That(renderer.AdjustFromScratch(scratch(80)), Is.True);
                uncovered = first with { LaneCoversEnabled = false, LiftUnits = initial.LiftUnits - 20 };
            });
            advanceReadingClock(3200);
            AddStep("record explicit opacity edits without changing the other temporary fields", () =>
            {
                config.SetValue(BmsRulesetSetting.SuddenOpacity, 900f);
                config.SetValue(BmsRulesetSetting.HiddenOpacity, 800f);
                opacity = uncovered with { SuddenOpacity = 900, HiddenOpacity = 800 };
            });
            advanceReadingClock(4200);
            AddStep("restore Floating covers as one completed recorded operation", () =>
            {
                Assert.That(renderer.ToggleGameplayLaneCovers(), Is.True);
                restored = opacity with { LaneCoversEnabled = true, SuddenUnits = renderer.SuddenUnits, HiddenUnits = renderer.HiddenUnits };
            });
            advanceReadingClock(5000);
            AddStep("flush the actual recorder before loading playback", () =>
            {
                ((BmsReplayRecorder)renderer.GameplayInputManager!.Recorder!).RecordFrame(true);
                renderer.SetRecordTarget(null!);
                var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                Assert.That(data.ReadingCalculationVersion, Is.EqualTo(0));
                Assert.That(data.ReadingSettings, Is.EqualTo(initial));
                Assert.That(data.ReadingSettingsTimeline!.Changes.Select(change => change.Time), Is.EqualTo(new[] { 1500d, 2200d, 3200d, 4200d }));
                Assert.That(data.ReadingSettingsTimeline.Changes.Select(change => change.Settings), Is.EqualTo(new[] { first, uncovered, opacity, restored }));
                var json = JObject.Parse(score.ScoreInfo.RulesetDataJson);
                Assert.That(json["reading_calculation_version"]!.Value<int>(), Is.Zero);
                Assert.That(((JObject)json["reading_settings"]!).Properties().Count(), Is.EqualTo(13));
                Assert.That(json["reading_settings_timeline"]!["changes"]!.All(change => ((JObject)change["settings"]!).Properties().Count() == 13), Is.True);
                config.SaveReadingSettings(personal);
                renderer.SetReplayScore(score);
            });

            advanceReadingClock(500);
            assertRuntimeReading(() => initial);
            advanceReadingClock(1500);
            assertRuntimeReading(() => first);
            advanceReadingClock(2199);
            assertRuntimeReading(() => first);
            advanceReadingClock(2200);
            assertRuntimeReading(() => uncovered);
            advanceReadingClock(3200);
            assertRuntimeReading(() => opacity);
            advanceReadingClock(4200);
            assertRuntimeReading(() => restored);
            advanceReadingClock(1200);
            assertRuntimeReading(() => initial);
            advanceReadingClock(2500);
            assertRuntimeReading(() => uncovered);
            AddStep("reattach the same replay after a clock reset", () => renderer.SetReplayScore(score));
            advanceReadingClock(500);
            assertRuntimeReading(() => initial);
            assertReplayCannotAdjust();
            AddAssert("viewing and seeking do not overwrite personal reading preferences", () => config.CaptureReadingSettings(), () => Is.EqualTo(personal));
        }

        [TestCase(null)]
        [TestCase(0)]
        public void TestLiteralLegacyFractionalNormalReadingKeepsNativeSpeedThroughSeekAndReset(int? calculationVersion)
        {
            var initial = new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Normal, 8.3, 2.5, 3.5,
                false, false, false, true, 250, 125, 0, 650, 750);
            var changed = initial with { NormalHiSpeed = 12.7 };
            var personal = initial with { HiSpeedMode = BmsHiSpeedMode.Classic, NormalHiSpeed = 9.2, ClassicHiSpeed = 4.25 };
            Score score = null!;
            string originalJson = string.Empty;
            prepare(personal);
            loadGame();
            AddStep("attach the literal historical thirteen-field reading payload", () =>
            {
                score = legacyFractionalReadingReplay(calculationVersion);
                originalJson = score.ScoreInfo.RulesetDataJson;
                Assert.That(score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!.ReadingCalculationVersion, Is.EqualTo(calculationVersion));
                clock.CurrentTime = 500;
                renderer.SetReplayScore(score);
            });
            advanceReadingClock(500);
            assertRuntimeReading(() => initial);
            assertLegacyNormalNativeSpeed(8.3, 100000d / 107.9, 556);
            advanceReadingClock(1199);
            assertLegacyNormalNativeSpeed(8.3, 100000d / 107.9, 556);
            advanceReadingClock(1200);
            assertRuntimeReading(() => changed);
            assertLegacyNormalNativeSpeed(12.7, 100000d / 165.1, 363);
            advanceReadingClock(2500);
            assertLegacyNormalNativeSpeed(12.7, 100000d / 165.1, 363);
            advanceReadingClock(500);
            assertRuntimeReading(() => initial);
            assertLegacyNormalNativeSpeed(8.3, 100000d / 107.9, 556);
            advanceReadingClock(1200);
            assertLegacyNormalNativeSpeed(12.7, 100000d / 165.1, 363);
            AddStep("detach playback before resetting its clock", () => renderer.SetReplayScore(null!));
            AddAssert("detaching clears the historical replay handler", () => renderer.HasReplayLoaded.Value, () => Is.False);
            AddStep("reset and reattach the same literal history", () =>
            {
                clock.CurrentTime = 0;
                renderer.SetReplayScore(score);
            });
            advanceReadingClock(500);
            assertRuntimeReading(() => initial);
            assertLegacyNormalNativeSpeed(8.3, 100000d / 107.9, 556);
            assertReplayCannotAdjust();
            AddAssert("legacy playback never rewrites the original payload", () => score.ScoreInfo.RulesetDataJson, () => Is.EqualTo(originalJson));
            AddAssert("legacy seeks and resets never learn the historical reading settings", () => config.CaptureReadingSettings(), () => Is.EqualTo(personal));
            recreateConfigAndGame();
            assertRuntimeReading(() => personal);
        }

        [TestCase(1, true)]
        [TestCase(1, false)]
        [TestCase(-1, true)]
        [TestCase(int.MaxValue, true)]
        public void TestUnknownReadingCalculationIsRejectedBeforeHistoricalReplayAdoption(int calculationVersion, bool withSnapshot)
        {
            var personal = settings(BmsHiSpeedMode.Classic, sudden: false, hidden: false) with { LiftEnabled = false, LiftUnits = 0 };
            prepare(personal);
            loadGame();
            AddStep("reject the unknown reading calculation without adopting its replay", () =>
            {
                var score = legacyFractionalReadingReplay(calculationVersion);
                if (!withSnapshot)
                {
                    var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                    data.ReadingSettings = null;
                    data.ReadingSettingsTimeline = null;
                    score.ScoreInfo.SetRulesetData(data);
                }
                string originalJson = score.ScoreInfo.RulesetDataJson;
                Assert.That(renderer.HasReplayLoaded.Value, Is.False);
                Assert.That(() => renderer.SetReplayScore(score), Throws.TypeOf<InvalidDataException>().With.Message.Contains("reading calculation version"));
                Assert.That(renderer.HasReplayLoaded.Value, Is.False);
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(personal.ClassicHiSpeed));
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(personal.HiSpeedMode));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(personal));
                Assert.That(score.ScoreInfo.RulesetDataJson, Is.EqualTo(originalJson));
            });
            AddStep("a known legacy replay can still be attached after rejection", () =>
            {
                clock.CurrentTime = 500;
                renderer.SetReplayScore(legacyFractionalReadingReplay(0));
            });
            advanceReadingClock(500);
            assertLegacyNormalNativeSpeed(8.3, 100000d / 107.9, 556);
            AddAssert("recovering from rejection does not write personal settings", () => config.CaptureReadingSettings(), () => Is.EqualTo(personal));
        }

        [TestCase("missing initial")]
        [TestCase("duplicate time")]
        [TestCase("invalid state")]
        public void TestInvalidReplayReadingTimelineIsRejectedWithoutLearningPreferences(string invalidity)
        {
            var initial = settings(BmsHiSpeedMode.Floating);
            prepare(initial);
            loadGame();
            AddStep("reject malformed external reading history at replay attachment", () =>
            {
                var timeline = new BmsReadingSettingsTimeline
                {
                    Changes = new List<BmsReadingSettingsTimeline.Change>
                    {
                        new BmsReadingSettingsTimeline.Change { Time = 1200, Settings = initial with { LaneCoversEnabled = false } },
                    },
                };
                if (invalidity == "duplicate time")
                    timeline.Changes.Add(new BmsReadingSettingsTimeline.Change { Time = 1200, Settings = initial });
                if (invalidity == "invalid state")
                    timeline.Changes[0].Settings = initial with { FloatingHiSpeed = double.PositiveInfinity };
                var score = new Score { Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(20000) } } };
                score.ScoreInfo.SetRulesetData(new BmsScoreInfoData
                {
                    ReadingSettings = invalidity == "missing initial" ? null : initial,
                    ReadingSettingsTimeline = timeline,
                });
                Assert.That(() => renderer.SetReplayScore(score), Throws.TypeOf<InvalidDataException>());
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(initial));
            });
        }

        private void assertLegacyNormalNativeSpeed(double speed, double expectedTimeRange, int expectedGreenNumber)
        {
            AddStep($"legacy Normal {speed} retains its native time, position and GN", () =>
            {
                var lane = renderer.Playfield.Lanes.First(lane => !lane.IsScratch);
                float height = lane.HitObjectContainer.DrawHeight;
                double distance = Math.Abs(lane.HitObjectContainer.PositionAtTime(1100, 1000));
                Assert.Multiple(() =>
                {
                    Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Normal));
                    Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(speed));
                    Assert.That(renderer.Playfield.ScrollLengthRatio.Value, Is.EqualTo(1).Within(0.001));
                    Assert.That(renderer.ScrollingInfo.TimeRange.Value, Is.EqualTo(expectedTimeRange).Within(0.001));
                    Assert.That(height, Is.GreaterThan(0));
                    Assert.That(distance, Is.EqualTo(100 / expectedTimeRange * height).Within(0.001));
                    Assert.That(renderer.SpeedMetrics.Value.RuntimeTimeRange, Is.EqualTo(expectedTimeRange).Within(0.001));
                    Assert.That(renderer.SpeedMetrics.Value.GreenNumber, Is.EqualTo(expectedGreenNumber));
                });
            });
        }

        private static Score legacyFractionalReadingReplay(int? calculationVersion)
        {
            var data = JObject.Parse("""
                                     {
                                       "version": 7,
                                       "reading_settings": {
                                         "hi_speed_mode": 0,
                                         "normal_hi_speed": 8.3,
                                         "floating_hi_speed": 2.5,
                                         "classic_hi_speed": 3.5,
                                         "sudden_enabled": false,
                                         "hidden_enabled": false,
                                         "lift_enabled": false,
                                         "lane_covers_enabled": true,
                                         "sudden_units": 250,
                                         "hidden_units": 125,
                                         "lift_units": 0,
                                         "sudden_opacity": 650,
                                         "hidden_opacity": 750
                                       },
                                       "reading_settings_timeline": {
                                         "changes": [{
                                           "time": 1200,
                                           "settings": {
                                             "hi_speed_mode": 0,
                                             "normal_hi_speed": 12.7,
                                             "floating_hi_speed": 2.5,
                                             "classic_hi_speed": 3.5,
                                             "sudden_enabled": false,
                                             "hidden_enabled": false,
                                             "lift_enabled": false,
                                             "lane_covers_enabled": true,
                                             "sudden_units": 250,
                                             "hidden_units": 125,
                                             "lift_units": 0,
                                             "sudden_opacity": 650,
                                             "hidden_opacity": 750
                                           }
                                         }]
                                       }
                                     }
                                     """);
            if (calculationVersion is int version)
                data["reading_calculation_version"] = version;
            return new Score
            {
                ScoreInfo = { Ruleset = new BmsRuleset().RulesetInfo, RulesetDataJson = data.ToString() },
                Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(20000) } },
            };
        }
    }
}
