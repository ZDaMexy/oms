// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.Input;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsRulesetConfigurationTest
    {
        [Test]
        public void TestCreatesRulesetConfigManagerWithDefaultSettings()
        {
            var config = (BmsRulesetConfigManager)new BmsRuleset().CreateConfig(null)!;
            var scrollSpeed = (BindableDouble)config.GetBindable<double>(BmsRulesetSetting.ScrollSpeed);
            var floatingHiSpeed = (BindableDouble)config.GetBindable<double>(BmsRulesetSetting.FloatingHiSpeed);
            var classicHiSpeed = (BindableDouble)config.GetBindable<double>(BmsRulesetSetting.ClassicHiSpeed);

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode).Value, Is.EqualTo(BmsHiSpeedMode.Normal));
                Assert.That(scrollSpeed.Value, Is.EqualTo(8.0));
                Assert.That(scrollSpeed.MinValue, Is.EqualTo(BmsRulesetConfigManager.NORMAL_HI_SPEED_MIN));
                Assert.That(scrollSpeed.MaxValue, Is.EqualTo(BmsRulesetConfigManager.NORMAL_HI_SPEED_MAX));
                Assert.That(scrollSpeed.Precision, Is.EqualTo(BmsRulesetConfigManager.NORMAL_HI_SPEED_PRECISION));
                Assert.That(floatingHiSpeed.Value, Is.EqualTo(2.50));
                Assert.That(floatingHiSpeed.MinValue, Is.EqualTo(BmsRulesetConfigManager.FLOATING_HI_SPEED_MIN));
                Assert.That(floatingHiSpeed.MaxValue, Is.EqualTo(BmsRulesetConfigManager.FLOATING_HI_SPEED_MAX));
                Assert.That(floatingHiSpeed.Precision, Is.EqualTo(BmsRulesetConfigManager.FLOATING_HI_SPEED_PRECISION));
                Assert.That(classicHiSpeed.Value, Is.EqualTo(2.50));
                Assert.That(classicHiSpeed.MinValue, Is.EqualTo(BmsRulesetConfigManager.CLASSIC_HI_SPEED_MIN));
                Assert.That(classicHiSpeed.MaxValue, Is.EqualTo(BmsRulesetConfigManager.CLASSIC_HI_SPEED_MAX));
                Assert.That(classicHiSpeed.Precision, Is.EqualTo(BmsRulesetConfigManager.CLASSIC_HI_SPEED_PRECISION));
                Assert.That(config.GetBindable<BmsPlayfieldStyle>(BmsRulesetSetting.PlayfieldStyle).Value, Is.EqualTo(BmsPlayfieldStyle.Center));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.PlayfieldWidth).Value, Is.EqualTo(0.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.PlayfieldHeight).Value, Is.EqualTo(0.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.LaneWidth).Value, Is.EqualTo(1.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.LaneSpacing).Value, Is.EqualTo(0.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.ScratchLaneWidthRatio).Value, Is.EqualTo(1.25));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.ScratchLaneSpacing).Value, Is.EqualTo(0.12));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.HitTargetHeight).Value, Is.EqualTo(16.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.HitTargetBarHeight).Value, Is.EqualTo(12.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.HitTargetLineHeight).Value, Is.EqualTo(3.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.HitTargetGlowRadius).Value, Is.EqualTo(6.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.HitTargetVerticalOffset).Value, Is.EqualTo(0.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.BarLineHeight).Value, Is.EqualTo(2.0));
                Assert.That(config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value, Is.Empty);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.SuddenEnabled).Value, Is.False);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.HiddenEnabled).Value, Is.False);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.LiftEnabled).Value, Is.False);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.LaneCoversEnabled).Value, Is.True);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.True);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.ReadingSettingsMigrated).Value, Is.True);

                foreach (var setting in new[] { BmsRulesetSetting.SuddenUnits, BmsRulesetSetting.HiddenUnits, BmsRulesetSetting.LiftUnits, BmsRulesetSetting.SuddenOpacity, BmsRulesetSetting.HiddenOpacity })
                {
                    var value = (BindableFloat)config.GetBindable<float>(setting);
                    bool opacity = setting is BmsRulesetSetting.SuddenOpacity or BmsRulesetSetting.HiddenOpacity;
                    Assert.That(value.Value, Is.EqualTo(opacity ? 1000 : 250), setting.ToString());
                    Assert.That(value.MinValue, Is.Zero, setting.ToString());
                    Assert.That(value.MaxValue, Is.EqualTo(1000), setting.ToString());
                    Assert.That(value.Precision, Is.EqualTo(1), setting.ToString());
                }
            });
        }

        [Test]
        public void TestReadingSettingsSnapshotIsDetachedAndSavesAllReadingFieldsTogether()
        {
            using var config = (BmsRulesetConfigManager)new BmsRuleset().CreateConfig(null)!;
            var before = config.CaptureReadingSettings();
            var adjusted = before with
            {
                HiSpeedMode = BmsHiSpeedMode.Floating,
                NormalHiSpeed = 9,
                FloatingHiSpeed = 1.75,
                ClassicHiSpeed = 3.25,
                SuddenEnabled = true,
                HiddenEnabled = true,
                LiftEnabled = true,
                LaneCoversEnabled = false,
                SuddenUnits = 325,
                HiddenUnits = 175,
                LiftUnits = 125,
                SuddenOpacity = 800,
                HiddenOpacity = 600,
            };
            config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value = false;

            Assert.That(config.CaptureReadingSettings(), Is.EqualTo(before), "Editing the play snapshot must not change the stored preferences.");
            config.SaveReadingSettings(adjusted);

            Assert.Multiple(() =>
            {
                Assert.That(config.CaptureReadingSettings(), Is.EqualTo(adjusted));
                Assert.That(before.NormalHiSpeed, Is.EqualTo(8));
                Assert.That(before.SuddenEnabled, Is.False);
                Assert.That(config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges).Value, Is.False,
                    "Saving one play must not change the independent remember preference.");
            });
        }

        [Test]
        public void TestReadingSnapshotJsonPreservesAllFields()
        {
            using var config = (BmsRulesetConfigManager)new BmsRuleset().CreateConfig(null)!;
            var snapshot = config.CaptureReadingSettings() with
            {
                HiSpeedMode = BmsHiSpeedMode.Classic,
                NormalHiSpeed = 11,
                FloatingHiSpeed = 1.75,
                ClassicHiSpeed = 3.25,
                SuddenEnabled = true,
                HiddenEnabled = true,
                LiftEnabled = true,
                LaneCoversEnabled = false,
                SuddenUnits = 123,
                HiddenUnits = 456,
                LiftUnits = 789,
                SuddenOpacity = 654,
                HiddenOpacity = 321,
            };

            var restored = JsonConvert.DeserializeObject<BmsReadingSettingsSnapshot>(JsonConvert.SerializeObject(snapshot));
            restored.Validate();
            Assert.That(restored, Is.EqualTo(snapshot));
        }

        [TestCaseSource(nameof(invalidReadingSnapshots))]
        public void TestInvalidReadingSnapshotDoesNotPartiallySave(BmsReadingSettingsSnapshot invalid)
        {
            using var config = (BmsRulesetConfigManager)new BmsRuleset().CreateConfig(null)!;
            var before = config.CaptureReadingSettings();

            Assert.That(() => config.SaveReadingSettings(invalid), Throws.TypeOf<InvalidDataException>());
            Assert.That(config.CaptureReadingSettings(), Is.EqualTo(before));
        }

        private static IEnumerable<TestCaseData> invalidReadingSnapshots()
        {
            var valid = new BmsReadingSettingsSnapshot(BmsHiSpeedMode.Floating, 9, 2, 3, true, false, true, false, 123, 456, 789, 1000, 500);
            yield return new TestCaseData(valid with { HiSpeedMode = (BmsHiSpeedMode)99 }).SetName("InvalidReadingMode");
            yield return new TestCaseData(valid with { NormalHiSpeed = double.NaN }).SetName("NonFiniteNormalHiSpeed");
            yield return new TestCaseData(valid with { FloatingHiSpeed = double.PositiveInfinity }).SetName("NonFiniteFloatingHiSpeed");
            yield return new TestCaseData(valid with { ClassicHiSpeed = 0.25 }).SetName("ClassicHiSpeedBelowRange");
            yield return new TestCaseData(valid with { SuddenUnits = float.NaN }).SetName("NonFiniteSuddenUnits");
            yield return new TestCaseData(valid with { HiddenUnits = 1001 }).SetName("HiddenUnitsAboveRange");
            yield return new TestCaseData(valid with { LiftUnits = -1 }).SetName("LiftUnitsBelowRange");
            yield return new TestCaseData(valid with { SuddenOpacity = float.NegativeInfinity }).SetName("NonFiniteSuddenOpacity");
            yield return new TestCaseData(valid with { HiddenOpacity = 1001 }).SetName("HiddenOpacityAboveRange");
        }

        [Test]
        public void TestCreatesRulesetSettingsSubsection()
            => Assert.That(new BmsRuleset().CreateSettings(), Is.TypeOf<BmsSettingsSubsection>());

        [Test]
        public void TestCreatesSupplementalBindingsKeyBindingSection()
            => Assert.That(new BmsRuleset().CreateKeyBindingSections().Single(), Is.TypeOf<BmsSupplementalBindingSettingsSection>());

        [TestCase(6, BmsAction.Key1, -1)]
        [TestCase(6, BmsAction.Key2, 1)]
        [TestCase(8, BmsAction.Key7, -1)]
        [TestCase(8, BmsAction.Key6, 1)]
        [TestCase(9, BmsAction.Key9, 1)]
        [TestCase(9, BmsAction.Key8, -1)]
        [TestCase(16, BmsAction.Key8, -1)]
        [TestCase(16, BmsAction.Key9, 1)]
        [TestCase(16, BmsAction.Scratch1, 0)]
        public void TestHiSpeedAdjustmentDirectionMapping(int variant, BmsAction action, int expectedDirection)
            => Assert.That(action.GetHiSpeedAdjustmentDirection(variant), Is.EqualTo(expectedDirection));
    }
}
