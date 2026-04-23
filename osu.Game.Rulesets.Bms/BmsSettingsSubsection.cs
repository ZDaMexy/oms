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
            var hiSpeedMode = config.GetBindable<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode);
            var normalHiSpeed = config.GetBindable<double>(BmsRulesetSetting.ScrollSpeed);
            var floatingHiSpeed = config.GetBindable<double>(BmsRulesetSetting.FloatingHiSpeed);
            var classicHiSpeed = config.GetBindable<double>(BmsRulesetSetting.ClassicHiSpeed);
            var keysoundConcurrentChannels = new BindableInt
            {
                Default = BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS,
                MinValue = BmsKeysoundStore.MIN_CONCURRENT_CHANNELS,
                MaxValue = BmsKeysoundStore.MAX_CONCURRENT_CHANNELS,
                Precision = 1,
            };

            config.BindWith(BmsRulesetSetting.KeysoundConcurrentChannels, keysoundConcurrentChannels);

            var hiSpeedSlider = new FormSliderBar<double>
            {
                Caption = hiSpeedMode.Value.ToString(),
                HintText = @"只调整当前模式的数值。Green Number 与可见时间需要在游玩界面结合 Sudden / Hidden / Lift 后才有意义。",
                Current = normalHiSpeed,
                KeyboardStep = (float)hiSpeedMode.Value.GetAdjustmentStep(),
                LabelFormat = value => hiSpeedMode.Value.FormatValue(value),
            };

            hiSpeedMode.BindValueChanged(mode =>
            {
                hiSpeedSlider.Caption = mode.NewValue switch
                {
                    BmsHiSpeedMode.Normal => @"Normal Hi-Speed",
                    BmsHiSpeedMode.Floating => @"Floating Hi-Speed",
                    BmsHiSpeedMode.Classic => @"Classic Hi-Speed",
                    _ => @"Hi-Speed",
                };
                hiSpeedSlider.Current = mode.NewValue switch
                {
                    BmsHiSpeedMode.Normal => normalHiSpeed,
                    BmsHiSpeedMode.Floating => floatingHiSpeed,
                    BmsHiSpeedMode.Classic => classicHiSpeed,
                    _ => normalHiSpeed,
                };
                hiSpeedSlider.KeyboardStep = (float)mode.NewValue.GetAdjustmentStep();
            }, true);

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormEnumDropdown<BmsHiSpeedMode>
                {
                    Caption = @"Hi-Speed 模式",
                    HintText = @"在 Normal、Floating 与 Classic 三种调速模式之间切换。",
                    Current = hiSpeedMode,
                }),
                new SettingsItemV2(hiSpeedSlider),
                new SettingsItemV2(new FormEnumDropdown<BmsPlayfieldStyle>
                {
                    Caption = @"游玩区域样式",
                    HintText = @"仅作用于5K/7K",
                    Current = config.GetBindable<BmsPlayfieldStyle>(BmsRulesetSetting.PlayfieldStyle),
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = @"键音通道数",
                    Current = keysoundConcurrentChannels,
                    KeyboardStep = 1,
                    LabelFormat = value => $@"{value} 通道",
                }),
                new BmsSupplementalBindingSettingsSection(ruleset),
                new BmsDifficultyTableSettingsSection(),
            };
        }
    }
}
