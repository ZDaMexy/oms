// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using System;
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
                HintText = @"括号内为不启用sudden/hidden/lift的下落时间（ms），绿字（GreenNumber）需要在游戏内结合sudden/hidden/lift调节查看。",
                Current = normalHiSpeed,
                KeyboardStep = (float)hiSpeedMode.Value.GetAdjustmentStep(),
                LabelFormat = value => formatHiSpeedSettingValue(hiSpeedMode.Value, value),
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
                    HintText = "Normal：基础定速模式，直接按数值调速。\n"
                               + "Floating：按谱面初始 BPM 做补偿，更适合配合 sudden/hidden/lift 调整可见时间。\n"
                               + "Classic：按传统 Hi-Speed 语义计算。",
                    Current = hiSpeedMode,
                }),
                new SettingsItemV2(hiSpeedSlider),
                new SettingsItemV2(new FormEnumDropdown<BmsGimmickScrollMode>
                {
                    Caption = @"特效谱滚动（实验性）",
                    HintText = "为特效谱／变速谱（如定格动画、瞬移、STOP 定帧）提供更好的渲染还原；若出现异常，将此项调回 Off 即可恢复常规滚动（实验性功能）。\n\n"
                               + "Off：所有谱走常规前进式滚动，渲染零变化。\n"
                               + "On：有滚动剖面的谱一律启用特效旁路。\n"
                               + "Auto（默认）：仅对自动识别为特效/变速谱（极端 BPM 瞬移或明显 STOP 冻结）的谱启用，其余谱走常规滚动。\n\n"
                               + "仅影响视觉定位；判定/计分不变。",
                    Current = config.GetBindable<BmsGimmickScrollMode>(BmsRulesetSetting.GimmickScrollMode),
                }),
                new SettingsItemV2(new FormEnumDropdown<BmsPlayfieldStyle>
                {
                    Caption = @"游玩区域样式",
                    HintText = @"仅作用于5K/7K",
                    Current = config.GetBindable<BmsPlayfieldStyle>(BmsRulesetSetting.PlayfieldStyle),
                }),
                new SettingsItemV2(new FormSliderBar<int>
                {
                    Caption = @"键音通道数",
                    HintText = "控制共享键音播放池大小。\n\n"
                               + "1-8 通道最省资源，但高密谱面更容易出现 BGM、键音或长按尾音被抢占截断。\n"
                               + "16 通道仍偏保守。\n\n"
                               + "默认 32 通道通常最均衡，能明显减少截音。\n"
                               + "64-256 通道更适合极高密谱面或较强机器。\n\n"
                               + "游戏中调高会立即补充可用通道；调低会在当前仍在播放的通道自然结束后再逐步回收，不会直接切断正在发声的音频。\n\n"
                               + "若听到缺音、尾音消失或 BGM 被盖掉，先提高到 48 或 64。\n"
                               + "若感觉额外负载增加，再逐步下调。",
                    Current = keysoundConcurrentChannels,
                    KeyboardStep = 1,
                    LabelFormat = value => $@"{value} 通道",
                }),
                new BmsSupplementalBindingSettingsSection(ruleset),
                new BmsDifficultyTableSettingsSection(),
            };

            static string formatHiSpeedSettingValue(BmsHiSpeedMode mode, double value)
            {
                double fallTime = DrawableBmsRuleset.ComputeScrollTime(value);
                int fallTimeMs = (int)Math.Round(fallTime, MidpointRounding.AwayFromZero);
                return $@"{mode.FormatValue(value)} ({fallTimeMs}ms)";
            }
        }
    }
}
