// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.UI;

namespace osu.Game.Rulesets.Bms
{
    public partial class BmsReadingSettingsControl : SettingsSubsection
    {
        protected override LocalisableString Header => "读谱设置";

        public BmsReadingSettingsControl(BmsRulesetConfigManager config)
        {
            var hiSpeedMode = config.GetBindable<BmsHiSpeedMode>(BmsRulesetSetting.HiSpeedMode);
            var normalHiSpeed = config.GetBindable<double>(BmsRulesetSetting.ScrollSpeed);
            var floatingHiSpeed = config.GetBindable<double>(BmsRulesetSetting.FloatingHiSpeed);
            var classicHiSpeed = config.GetBindable<double>(BmsRulesetSetting.ClassicHiSpeed);

            var hiSpeedSlider = new FormSliderBar<double>
            {
                HintText = "括号内为基础下落时间（ms）。绿字（Green Number）在游玩中按实际 BPM、Sudden、Hidden 和 Lift 的可见区域显示。",
                Current = normalHiSpeed,
                LabelFormat = value => formatHiSpeedSettingValue(hiSpeedMode.Value, value),
            };

            hiSpeedMode.BindValueChanged(mode =>
            {
                hiSpeedSlider.Caption = mode.NewValue switch
                {
                    BmsHiSpeedMode.Normal => "Normal Hi-Speed",
                    BmsHiSpeedMode.Floating => "Floating Hi-Speed",
                    BmsHiSpeedMode.Classic => "Classic Hi-Speed",
                    _ => "Hi-Speed",
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
                    Caption = "Hi-Speed 模式",
                    HintText = "Normal：基础定速模式，直接按数值调速。\n"
                               + "Floating：以谱面初始 BPM 为基准；调整已启用的 Sudden／Hidden 时按当前 BPM 和可见区域补偿 Hi-Speed。谱面变速本身不会自动锁定绿字。\n"
                               + "Classic：按传统 Hi-Speed 语义计算。\n\n"
                               + "游玩中按住 Start 打开调速面板，白键降低、黑键提高；按住 Start 再按‘游玩：挡板／调速’绑定键（EFFECT）可切换 Normal／Floating。",
                    Current = hiSpeedMode,
                }),
                new SettingsItemV2(hiSpeedSlider),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "使用 Sudden",
                    HintText = "从上方遮挡轨道。可与 Hidden、Lift 同时使用。按住 Start 转动转盘可调整挡板。",
                    Current = config.GetBindable<bool>(BmsRulesetSetting.SuddenEnabled),
                }),
                unitsSlider(config, BmsRulesetSetting.SuddenUnits, "Sudden 白字", "上方挡板的位置，范围 0–1000；关闭挡板后保留此值。"),
                opacitySlider(config, BmsRulesetSetting.SuddenOpacity, "Sudden 不透明度"),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "使用 Hidden",
                    HintText = "从下方遮挡轨道。与 Sudden 同时使用时，双击 Start 会同时开关两块挡板。",
                    Current = config.GetBindable<bool>(BmsRulesetSetting.HiddenEnabled),
                }),
                unitsSlider(config, BmsRulesetSetting.HiddenUnits, "Hidden 白字", "下方挡板的位置，范围 0–1000；关闭挡板后保留此值。"),
                opacitySlider(config, BmsRulesetSetting.HiddenOpacity, "Hidden 不透明度"),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "显示挡板（游玩中双击 Start 切换）",
                    HintText = "显示或隐藏当前选中的 Sudden／Hidden 组合，保留组合和白字。未选组合时，游玩中双击 Start 会启用 Sudden。Lift 不受此开关影响。",
                    Current = config.GetBindable<bool>(BmsRulesetSetting.LaneCoversEnabled),
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "使用 Lift",
                    HintText = "抬高判定线并缩短读谱区域。双击 Start 保留 Lift，只开关 Sudden／Hidden；没有显示挡板时，按住 Start 转盘可调整 Lift。",
                    Current = config.GetBindable<bool>(BmsRulesetSetting.LiftEnabled),
                }),
                unitsSlider(config, BmsRulesetSetting.LiftUnits, "Lift 白字", "判定线的抬高量，范围 0–1000。"),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "记住游玩中的读谱调整",
                    HintText = "开启后，完成一次游玩中的调整会一起保存 Hi-Speed、挡板组合、显示状态、白字和不透明度。关闭后，游玩中的调整仅用于本局；在此处修改的设置仍会保存。",
                    Current = config.GetBindable<bool>(BmsRulesetSetting.RememberReadingChanges),
                }),
            };
        }

        private static SettingsItemV2 unitsSlider(BmsRulesetConfigManager config, BmsRulesetSetting setting, string caption, string hint)
            => new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = caption,
                HintText = hint,
                Current = config.GetBindable<float>(setting),
                KeyboardStep = 1,
                LabelFormat = value => $"{value:0}",
            });

        private static SettingsItemV2 opacitySlider(BmsRulesetConfigManager config, BmsRulesetSetting setting, string caption)
            => new SettingsItemV2(new FormSliderBar<float>
            {
                Caption = caption,
                HintText = "只改变挡板的不透明度，不改变挡板位置或可见区域的计算。",
                Current = config.GetBindable<float>(setting),
                KeyboardStep = 10,
                LabelFormat = value => $"{value / 10:0.#}%",
            });

        private static string formatHiSpeedSettingValue(BmsHiSpeedMode mode, double value)
        {
            int fallTimeMs = (int)Math.Round(DrawableBmsRuleset.ComputeScrollTime(value), MidpointRounding.AwayFromZero);
            return $"{mode.FormatValue(value)} ({fallTimeMs}ms)";
        }
    }
}
