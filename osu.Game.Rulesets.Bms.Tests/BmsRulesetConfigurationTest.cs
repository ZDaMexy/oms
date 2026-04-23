// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Game.Rulesets.Bms.Audio;
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
                Assert.That(config.GetBindable<int>(BmsRulesetSetting.KeysoundConcurrentChannels).Value, Is.EqualTo(BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS));
                Assert.That(config.GetBindable<string>(BmsRulesetSetting.PersistedModState).Value, Is.Empty);
            });
        }

        [Test]
        public void TestCreatesRulesetSettingsSubsection()
            => Assert.That(new BmsRuleset().CreateSettings(), Is.TypeOf<BmsSettingsSubsection>());

        [Test]
        public void TestCreatesSupplementalBindingsKeyBindingSection()
            => Assert.That(new BmsRuleset().CreateKeyBindingSections().Single(), Is.TypeOf<BmsSupplementalBindingSettingsSection>());

        [TestCase(6, BmsAction.Key1, 1)]
        [TestCase(6, BmsAction.Key2, -1)]
        [TestCase(8, BmsAction.Key7, 1)]
        [TestCase(8, BmsAction.Key6, -1)]
        [TestCase(9, BmsAction.Key9, 1)]
        [TestCase(9, BmsAction.Key8, -1)]
        [TestCase(16, BmsAction.Key8, 1)]
        [TestCase(16, BmsAction.Key9, -1)]
        [TestCase(16, BmsAction.Scratch1, 0)]
        public void TestHiSpeedAdjustmentDirectionMapping(int variant, BmsAction action, int expectedDirection)
            => Assert.That(action.GetHiSpeedAdjustmentDirection(variant), Is.EqualTo(expectedDirection));
    }
}
