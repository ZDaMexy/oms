// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                Assert.That(data.ReadingSettings, Is.EqualTo(initial));
                Assert.That(data.ReadingSettingsTimeline!.Changes.Select(change => change.Time), Is.EqualTo(new[] { 1500d, 2200d, 3200d, 4200d }));
                Assert.That(data.ReadingSettingsTimeline.Changes.Select(change => change.Settings), Is.EqualTo(new[] { first, uncovered, opacity, restored }));
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
    }
}
