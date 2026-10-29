// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Configuration;
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
        public void TestBackgroundSavedReadingGroupSurvivesImmediateManagerRecreation()
        {
            var initial = settings(BmsHiSpeedMode.Normal);
            var changed = initial with
            {
                HiSpeedMode = BmsHiSpeedMode.Floating,
                FloatingHiSpeed = 1.7533334096272786,
                SuddenUnits = 350,
                LaneCoversEnabled = false,
            };
            prepare(initial);
            AddStep("save on the real background path and recreate without an intervening frame", () =>
            {
                config.SaveReadingSettings(changed);
                Assert.That(Task.Run(config.Save).GetAwaiter().GetResult(), Is.True);
                config.Dispose();
                Realm.Realm.Refresh();
                config = new BmsRulesetConfigManager(new SettingsStore(Realm), new BmsRuleset().RulesetInfo);
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(changed));
            });
        }

        [TestCase("missing snapshot")]
        [TestCase("missing initial multiplier")]
        [TestCase("missing timeline multiplier")]
        public void TestMalformedNativeReadingStateIsRejectedBeforeAdoption(string invalidity)
        {
            var personal = settings(BmsHiSpeedMode.Classic);
            prepare(personal);
            loadGame();
            AddStep("reject incomplete version-one reading state without changing the live game", () =>
            {
                var initial = personal with { NormalHiSpeedMultiplier = 2.9126275380452475 };
                var data = new BmsScoreInfoData
                {
                    ReadingCalculationVersion = 1,
                    ReadingSettings = invalidity == "missing snapshot" ? null
                        : invalidity == "missing initial multiplier" ? personal : initial,
                    ReadingSettingsTimeline = invalidity == "missing timeline multiplier" ? new BmsReadingSettingsTimeline
                    {
                        Changes = new List<BmsReadingSettingsTimeline.Change>
                        {
                            new BmsReadingSettingsTimeline.Change { Time = 1200, Settings = personal },
                        },
                    } : null,
                };
                var score = new Score { Replay = new Replay { Frames = { new BmsReplayFrame(0), new BmsReplayFrame(20000) } } };
                score.ScoreInfo.SetRulesetData(data);
                Assert.That(() => renderer.SetReplayScore(score), Throws.TypeOf<InvalidDataException>());
                Assert.That(renderer.HasReplayLoaded.Value, Is.False);
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(personal.HiSpeedMode));
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(personal.ClassicHiSpeed));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(personal));
            });
        }

        [TestCase(false, 1.5)]
        [TestCase(true, 2)]
        public void TestFloatingFineMotionSurvivesSavingAndChoosesTheCorrectClassicNeighbour(bool fineMotion, double expectedClassic)
        {
            var initial = settings(BmsHiSpeedMode.Classic, sudden: false, hidden: false) with { ClassicHiSpeed = 2.25, LiftEnabled = false };
            double expectedFloating = fineMotion ? 1.7533334096272786 : 1.75;
            prepare(initial);
            loadGame();
            AddStep("enter Floating and select the exact lower midpoint", () =>
            {
                Assert.That(renderer.ToggleHiSpeedMode(), Is.True);
                Assert.That(renderer.AdjustSelectedHiSpeed(-1), Is.True);
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(1.75));
                renderer.SetAdjustmentHeld(true);
                if (fineMotion)
                    Assert.That(renderer.AdjustFromScratch(scratch(4)), Is.True);
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(expectedFloating));
                Assert.That(BmsHiSpeedMode.Floating.FormatValue(renderer.SelectedHiSpeed.Value), Is.EqualTo("1.75"));
                Assert.That(renderer.ToggleHiSpeedMode(), Is.True);
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(expectedClassic));
            });
            recreateConfigAndGame();
            AddStep("the database retained the internal Floating value independently of its display", () =>
            {
                Assert.That(config.Get<double>(BmsRulesetSetting.FloatingHiSpeed), Is.EqualTo(expectedFloating));
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(expectedClassic));
            });
        }

        [Test]
        public void TestNormalMultiplierSurvivesBpmChangesAndRecordedReplaySeeks()
        {
            const string chart = """
                #TITLE Normal preset anchor
                #BPM 120
                #BPM01 240
                #00111:01
                #00208:01
                #00311:01
                #00411:01
                #00511:01
                """;
            var initial = settings(BmsHiSpeedMode.Normal, sudden: false, hidden: false) with { LiftEnabled = false };
            Score score = null!;
            double openingMultiplier = 0;
            double openingTime = 0;
            double reselectedMultiplier = 0;
            prepare(initial, remember: false);
            loadGame(chartText: chart);
            advanceReadingClock(1500);
            AddStep("record the original preset multiplier", () =>
            {
                openingMultiplier = renderer.SpeedMetrics.Value.ScrollSpeed;
                openingTime = renderer.SpeedMetrics.Value.VisibleLaneTime;
                Assert.That(openingMultiplier, Is.EqualTo(2.9126275380452475));
                renderer.SetRecordTarget(score = new Score());
            });
            advanceReadingClock(4500);
            AddStep("doubling BPM keeps the multiplier and halves the readable time", () =>
            {
                Assert.That(renderer.SpeedMetrics.Value.ScrollSpeed, Is.EqualTo(openingMultiplier));
                Assert.That(renderer.SpeedMetrics.Value.VisibleLaneTime, Is.EqualTo(openingTime / 2).Within(0.001));
            });
            advanceReadingClock(5500);
            AddStep("a new preset uses the current BPM", () =>
            {
                Assert.That(renderer.AdjustSelectedHiSpeed(1), Is.True);
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(9));
                Assert.That(renderer.SpeedMetrics.Value.VisibleLaneTime, Is.EqualTo(800).Within(0.001));
                reselectedMultiplier = renderer.SpeedMetrics.Value.ScrollSpeed;
            });
            advanceReadingClock(6500);
            AddStep("finish and adopt the native replay", () =>
            {
                ((BmsReplayRecorder)renderer.GameplayInputManager!.Recorder!).RecordFrame(true);
                renderer.SetRecordTarget(null!);
                var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                Assert.That(data.ReadingCalculationVersion, Is.EqualTo(1));
                Assert.That(data.ReadingSettings!.Value.NormalHiSpeedMultiplier, Is.EqualTo(openingMultiplier));
                Assert.That(data.ReadingSettingsTimeline!.Changes[0].Settings.NormalHiSpeedMultiplier, Is.EqualTo(reselectedMultiplier));
                renderer.SetReplayScore(score);
            });
            advanceReadingClock(4500);
            AddAssert("seeking before the reselect restores its original multiplier", () => renderer.SpeedMetrics.Value.ScrollSpeed, () => Is.EqualTo(openingMultiplier));
            advanceReadingClock(5500);
            AddAssert("seeking after the reselect restores the recorded multiplier", () => renderer.SpeedMetrics.Value.ScrollSpeed, () => Is.EqualTo(reselectedMultiplier));
            advanceReadingClock(0);
            AddAssert("reset restores the initial multiplier", () => renderer.SpeedMetrics.Value.ScrollSpeed, () => Is.EqualTo(openingMultiplier));
            AddAssert("recording and replay never write a disabled Remember preference", () => config.CaptureReadingSettings(), () => Is.EqualTo(initial));
        }
    }
}
