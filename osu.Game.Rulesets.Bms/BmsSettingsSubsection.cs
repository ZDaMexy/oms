// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.UI.Scrolling;

namespace osu.Game.Rulesets.Bms
{
    public partial class BmsSettingsSubsection : RulesetSettingsSubsection
    {
        protected override LocalisableString Header => "BMS";

        private readonly BmsRuleset ruleset;

        public BmsSettingsSubsection(BmsRuleset ruleset)
            : base(ruleset)
        {
            this.ruleset = ruleset;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var config = (BmsRulesetConfigManager)Config;
            var keysoundConcurrentChannels = new BindableInt
            {
                Default = BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS,
                MinValue = BmsKeysoundStore.MIN_CONCURRENT_CHANNELS,
                MaxValue = BmsKeysoundStore.MAX_CONCURRENT_CHANNELS,
                Precision = 1,
            };

            config.BindWith(BmsRulesetSetting.KeysoundConcurrentChannels, keysoundConcurrentChannels);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<ScrollingDirection>
                {
                    Caption = RulesetSettingsStrings.ScrollingDirection,
                    Current = config.GetBindable<ScrollingDirection>(BmsRulesetSetting.ScrollDirection),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = RulesetSettingsStrings.ScrollSpeed,
                    Current = config.GetBindable<double>(BmsRulesetSetting.ScrollSpeed),
                    KeyboardStep = 1,
                    LabelFormat = value => RulesetSettingsStrings.ScrollSpeedTooltip((int)DrawableBmsRuleset.ComputeScrollTime(value), value),
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Playfield Scale",
                    Current = config.GetBindable<double>(BmsRulesetSetting.PlayfieldScale),
                    KeyboardStep = 0.05f,
                    LabelFormat = value => $@"{value:0.00}x",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Playfield Horizontal Offset",
                    Current = config.GetBindable<double>(BmsRulesetSetting.PlayfieldHorizontalOffset),
                    KeyboardStep = 0.02f,
                    LabelFormat = value => $@"{value:+0%;-0%;0%}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Playfield Width",
                    Current = config.GetBindable<double>(BmsRulesetSetting.PlayfieldWidth),
                    KeyboardStep = 0.02f,
                    LabelFormat = value => value <= 0 ? @"Auto" : $@"{value:0%}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Playfield Height",
                    Current = config.GetBindable<double>(BmsRulesetSetting.PlayfieldHeight),
                    KeyboardStep = 0.02f,
                    LabelFormat = value => value <= 0 ? @"Auto" : $@"{value:0%}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Lane Width",
                    Current = config.GetBindable<double>(BmsRulesetSetting.LaneWidth),
                    KeyboardStep = 0.05f,
                    LabelFormat = value => $@"{value:0.00}x",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Scratch Lane Width Ratio",
                    Current = config.GetBindable<double>(BmsRulesetSetting.ScratchLaneWidthRatio),
                    KeyboardStep = 0.05f,
                    LabelFormat = value => $@"{value:0.00}x",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Lane Spacing",
                    Current = config.GetBindable<double>(BmsRulesetSetting.LaneSpacing),
                    KeyboardStep = 0.02f,
                    LabelFormat = value => $@"{value:0%}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Scratch Lane Spacing",
                    Current = config.GetBindable<double>(BmsRulesetSetting.ScratchLaneSpacing),
                    KeyboardStep = 0.02f,
                    LabelFormat = value => $@"{value:0%}",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Hit Target Height",
                    Current = config.GetBindable<double>(BmsRulesetSetting.HitTargetHeight),
                    KeyboardStep = 0.5f,
                    LabelFormat = value => $@"{value:0.0}px",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Hit Target Bar Height",
                    Current = config.GetBindable<double>(BmsRulesetSetting.HitTargetBarHeight),
                    KeyboardStep = 0.5f,
                    LabelFormat = value => $@"{value:0.0}px",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Hit Target Line Height",
                    Current = config.GetBindable<double>(BmsRulesetSetting.HitTargetLineHeight),
                    KeyboardStep = 0.5f,
                    LabelFormat = value => $@"{value:0.0}px",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Hit Target Glow Radius",
                    Current = config.GetBindable<double>(BmsRulesetSetting.HitTargetGlowRadius),
                    KeyboardStep = 0.5f,
                    LabelFormat = value => $@"{value:0.0}px",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Hit Target Vertical Offset",
                    Current = config.GetBindable<double>(BmsRulesetSetting.HitTargetVerticalOffset),
                    KeyboardStep = 4f,
                    LabelFormat = value => $@"{value:0}px",
                }),
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = @"Bar Line Height",
                    Current = config.GetBindable<double>(BmsRulesetSetting.BarLineHeight),
                    KeyboardStep = 0.5f,
                    LabelFormat = value => $@"{value:0.0}px",
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = @"Keysound Channels",
                    Current = keysoundConcurrentChannels,
                    KeyboardStep = 1,
                    LabelFormat = value => $@"{value} channels",
                }),
                new BmsSupplementalBindingSettingsSection(ruleset),
                new BmsDifficultyTableSettingsSection(),
            };
        }
    }
}
