// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Bms.Tests
{
    [TestFixture]
    public class BmsRulesetConfigurationTest
    {
        [Test]
        public void TestCreatesRulesetConfigManagerWithDefaultSettings()
        {
            var config = (BmsRulesetConfigManager)new BmsRuleset().CreateConfig(null)!;

            Assert.Multiple(() =>
            {
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.ScrollSpeed).Value, Is.EqualTo(8.0));
                Assert.That(config.GetBindable<ScrollingDirection>(BmsRulesetSetting.ScrollDirection).Value, Is.EqualTo(ScrollingDirection.Down));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.PlayfieldScale).Value, Is.EqualTo(1.0));
                Assert.That(config.GetBindable<double>(BmsRulesetSetting.PlayfieldHorizontalOffset).Value, Is.EqualTo(0.0));
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
            });
        }

        [Test]
        public void TestCreatesRulesetSettingsSubsection()
            => Assert.That(new BmsRuleset().CreateSettings(), Is.TypeOf<BmsSettingsSubsection>());

        [Test]
        public void TestCreatesSupplementalBindingsKeyBindingSection()
            => Assert.That(new BmsRuleset().CreateKeyBindingSections().Single(), Is.TypeOf<BmsSupplementalBindingSettingsSection>());
    }
}
