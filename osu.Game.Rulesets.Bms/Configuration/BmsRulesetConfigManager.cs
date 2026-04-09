// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Configuration.Tracking;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Bms.Configuration
{
    public class BmsRulesetConfigManager : RulesetConfigManager<BmsRulesetSetting>
    {
        public BmsRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(BmsRulesetSetting.ScrollSpeed, 8.0, 1.0, 40.0, 0.1);
            SetDefault(BmsRulesetSetting.ScrollDirection, ScrollingDirection.Down);
            SetDefault(BmsRulesetSetting.PlayfieldScale, 1.0, 0.5, 1.5, 0.01);
            SetDefault(BmsRulesetSetting.PlayfieldHorizontalOffset, 0.0, -0.4, 0.4, 0.01);
            SetDefault(BmsRulesetSetting.PlayfieldWidth, 0.0, 0.0, 1.0, 0.01);
            SetDefault(BmsRulesetSetting.PlayfieldHeight, 0.0, 0.0, 1.0, 0.01);
            SetDefault(BmsRulesetSetting.LaneWidth, 1.0, 0.5, 2.0, 0.01);
            SetDefault(BmsRulesetSetting.LaneSpacing, 0.0, 0.0, 0.4, 0.01);
            SetDefault(BmsRulesetSetting.ScratchLaneWidthRatio, 1.25, 1.0, 2.0, 0.01);
            SetDefault(BmsRulesetSetting.ScratchLaneSpacing, 0.12, 0.0, 0.4, 0.01);
            SetDefault(BmsRulesetSetting.HitTargetHeight, 16.0, 12.0, 32.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetBarHeight, 12.0, 4.0, 16.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetLineHeight, 3.0, 1.0, 8.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetGlowRadius, 6.0, 0.0, 12.0, 0.5);
            SetDefault(BmsRulesetSetting.HitTargetVerticalOffset, 0.0, 0.0, 160.0, 1.0);
            SetDefault(BmsRulesetSetting.BarLineHeight, 2.0, 1.0, 6.0, 0.5);
            SetDefault(BmsRulesetSetting.KeysoundConcurrentChannels, BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS);
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings
        {
            new TrackedSetting<double>(BmsRulesetSetting.ScrollSpeed,
                speed => new SettingDescription(
                    rawValue: speed,
                    name: RulesetSettingsStrings.ScrollSpeed,
                    value: RulesetSettingsStrings.ScrollSpeedTooltip((int)DrawableBmsRuleset.ComputeScrollTime(speed), speed)
                )
            )
        };
    }

    public enum BmsRulesetSetting
    {
        ScrollSpeed,
        ScrollDirection,
        PlayfieldScale,
        PlayfieldHorizontalOffset,
        PlayfieldWidth,
        PlayfieldHeight,
        LaneWidth,
        LaneSpacing,
        ScratchLaneWidthRatio,
        ScratchLaneSpacing,
        HitTargetHeight,
        HitTargetBarHeight,
        HitTargetLineHeight,
        HitTargetGlowRadius,
        HitTargetVerticalOffset,
        BarLineHeight,
        KeysoundConcurrentChannels,
    }
}
