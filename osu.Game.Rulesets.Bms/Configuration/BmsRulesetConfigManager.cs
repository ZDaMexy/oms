// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Configuration.Tracking;
using osu.Game.Configuration;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.Bms.Configuration
{
    public class BmsRulesetConfigManager : RulesetConfigManager<BmsRulesetSetting>
    {
        public const double NORMAL_HI_SPEED_MIN = 1.0;
        public const double NORMAL_HI_SPEED_MAX = 20.0;
        public const double NORMAL_HI_SPEED_PRECISION = 0.1;

        public const double FLOATING_HI_SPEED_MIN = 0.5;
        public const double FLOATING_HI_SPEED_MAX = 10.0;
        public const double FLOATING_HI_SPEED_PRECISION = 0.01;

        public const double CLASSIC_HI_SPEED_MIN = 0.5;
        public const double CLASSIC_HI_SPEED_MAX = 10.0;
        public const double CLASSIC_HI_SPEED_PRECISION = 0.25;

        public BmsRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(BmsRulesetSetting.HiSpeedMode, BmsHiSpeedMode.Normal);
            SetDefault(BmsRulesetSetting.ScrollSpeed, 8.0, NORMAL_HI_SPEED_MIN, NORMAL_HI_SPEED_MAX, NORMAL_HI_SPEED_PRECISION);
            SetDefault(BmsRulesetSetting.FloatingHiSpeed, 2.50, FLOATING_HI_SPEED_MIN, FLOATING_HI_SPEED_MAX, FLOATING_HI_SPEED_PRECISION);
            SetDefault(BmsRulesetSetting.ClassicHiSpeed, 2.50, CLASSIC_HI_SPEED_MIN, CLASSIC_HI_SPEED_MAX, CLASSIC_HI_SPEED_PRECISION);
            SetDefault(BmsRulesetSetting.PlayfieldStyle, BmsPlayfieldStyle.Center);
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
            SetDefault(BmsRulesetSetting.PersistedModState, string.Empty);
        }

        public override TrackedSettings CreateTrackedSettings() => new TrackedSettings();
    }

    public enum BmsRulesetSetting
    {
        HiSpeedMode,
        ScrollSpeed,
        FloatingHiSpeed,
        ClassicHiSpeed,
        PlayfieldStyle,
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
        PersistedModState,
    }
}
