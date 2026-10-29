// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Tests
{
    public partial class TestSceneBmsReadingSettings
    {
        [TestCase(1.25, 3, 2.25)]
        [TestCase(4.25, -3, 3.5)]
        [TestCase(0.5, 11, 4)]
        [TestCase(10, -11, 1)]
        [TestCase(0.5, -3, 0.5)]
        [TestCase(10, 3, 10)]
        public void TestClassicMultiStepAdjustmentUsesConsecutiveDirectionalChoices(double initial, int amount, double expected)
        {
            BmsReadingSettingsSnapshot state = settings(BmsHiSpeedMode.Classic) with { ClassicHiSpeed = initial };
            prepare(state, remember: false);
            loadGame();
            assertRuntimeReading(() => state);
            AddStep("adjust the actual loaded Classic controller", () =>
            {
                Assert.That(renderer.AdjustSelectedHiSpeed(amount), Is.EqualTo(initial != expected));
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(expected));
                Assert.That(renderer.HiSpeedMode.Value, Is.EqualTo(BmsHiSpeedMode.Classic));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(state));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestClassicMultiStepAdjustmentPublishesAndRemembersOnlyItsFinalReadingGroup(bool remember)
        {
            BmsReadingSettingsSnapshot initial = settings(BmsHiSpeedMode.Classic) with { ClassicHiSpeed = 1.25 };
            BmsReadingSettingsSnapshot expected = initial with { ClassicHiSpeed = 2.25 };
            Score score = null!;
            IBindable<double> runtimeSpeed = null!;
            IBindable<double> storedSpeed = null!;
            int runtimeChanges = 0;
            int storedChanges = 0;
            prepare(initial, remember);
            loadGame();
            AddStep("start the real native recorder and observe actual speed publications", () =>
            {
                renderer.SetRecordTarget(score = new Score());
                runtimeSpeed = renderer.SelectedHiSpeed.GetBoundCopy();
                storedSpeed = config.GetBindable<double>(BmsRulesetSetting.ClassicHiSpeed);
                runtimeSpeed.BindValueChanged(_ => runtimeChanges++);
                storedSpeed.BindValueChanged(_ => storedChanges++);
            });
            advanceReadingClock(1500);
            AddStep("apply three steps as one gameplay operation", () => Assert.That(renderer.AdjustSelectedHiSpeed(3), Is.True));
            AddStep("only the final actual speed reaches consumers and the Remember preference", () =>
            {
                Assert.That(renderer.SelectedHiSpeed.Value, Is.EqualTo(2.25));
                Assert.That(runtimeChanges, Is.EqualTo(1));
                Assert.That(storedChanges, Is.EqualTo(remember ? 1 : 0));
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(remember ? expected : initial));
            });
            assertRuntimeReading(() => expected);
            advanceReadingClock(2000);
            AddStep("finish the actual recording and verify its one completed reading change", () =>
            {
                ((BmsReplayRecorder)renderer.GameplayInputManager!.Recorder!).RecordFrame(true);
                renderer.SetRecordTarget(null!);
                var data = score.ScoreInfo.GetRulesetData<BmsScoreInfoData>()!;
                Assert.That(data.ReadingCalculationVersion, Is.EqualTo(1));
                Assert.That(readingPreferences(data.ReadingSettings!.Value), Is.EqualTo(initial));
                Assert.That(data.ReadingSettings.Value.NormalHiSpeedMultiplier, Is.EqualTo(2.9126275380452475));
                Assert.That(data.ReadingSettingsTimeline!.Changes.Count, Is.EqualTo(1));
                Assert.That(data.ReadingSettingsTimeline.Changes.Single().Time, Is.EqualTo(1500));
                Assert.That(readingPreferences(data.ReadingSettingsTimeline.Changes.Single().Settings), Is.EqualTo(expected));
                Assert.That(data.ReadingSettingsTimeline.Changes.Single().Settings.NormalHiSpeedMultiplier, Is.EqualTo(data.ReadingSettings.Value.NormalHiSpeedMultiplier));
                runtimeSpeed.UnbindAll();
                storedSpeed.UnbindAll();
            });
            recreateConfigAndGame();
            assertRuntimeReading(() => remember ? expected : initial);
        }
    }
}
