// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using System;
using System.IO;
using System.Threading.Tasks;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Bms.Configuration;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.UI;
using osuTK;

namespace osu.Game.Rulesets.Bms
{
    public partial class BmsSettingsSubsection : RulesetSettingsSubsection
    {
        protected override LocalisableString Header => "BMS";

        private readonly BmsRuleset ruleset;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        private OsuTextFlowContainer ffmpegStatusText = null!;

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
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"显示 BGA",
                    HintText = "游玩时在 playfield 旁的浮窗中播放谱面 BGA（背景图/动画/视频）。\n\n"
                               + "默认按游玩区域样式自动排在 playfield 对侧（1P→右、2P→左、居中→右、14K→中缝）。\n"
                               + "关闭后仅保留全屏背景。仅影响视觉，不影响判定/计分。",
                    Current = { BindTarget = config.GetBindable<bool>(BmsRulesetSetting.ShowBga) },
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = @"ffmpeg完整BGA支持",
                    HintText = "对老式BGA提供转码播放支持，需自行放置ffmpeg到数据目录。",
                    Current = { BindTarget = config.GetBindable<bool>(BmsRulesetSetting.BgaVideoTranscode) },
                }),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 8),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Full,
                                Spacing = new Vector2(10),
                                Children = new Drawable[]
                                {
                                    new SettingsButtonV2
                                    {
                                        RelativeSizeAxes = Axes.None,
                                        AutoSizeAxes = Axes.None,
                                        Width = 180,
                                        Height = 40,
                                        Text = "检测 ffmpeg 安装状态",
                                        Action = detectFfmpegStatus,
                                    },
                                    new SettingsButtonV2
                                    {
                                        RelativeSizeAxes = Axes.None,
                                        AutoSizeAxes = Axes.None,
                                        Width = 180,
                                        Height = 40,
                                        Text = "打开 ffmpeg 安装目录",
                                        Action = openFfmpegDirectory,
                                    },
                                }
                            },
                            ffmpegStatusText = new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Alpha = 0,
                            },
                        }
                    }
                },
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

        private void detectFfmpegStatus()
        {
            ffmpegStatusText.Alpha = 1;
            ffmpegStatusText.Text = @"正在检测 ffmpeg…";

            string[] candidates = getFfmpegCandidates();

            // Probing actually runs ffmpeg (and checks for libx264), so do it off the update thread and marshal the
            // result back. ProbeFfmpeg never throws, so reading t.Result here is safe.
            Task.Run(() => BmsBgaVideoCache.ProbeFfmpeg(candidates))
                .ContinueWith(t => Schedule(() => ffmpegStatusText.Text = describeFfmpegProbe(t.GetResultSafely())));
        }

        private static string describeFfmpegProbe(BmsBgaVideoCache.FfmpegProbeResult result)
        {
            switch (result.State)
            {
                case BmsBgaVideoCache.FfmpegProbeState.Ready:
                    return $@"ffmpeg 可用（含 libx264，H.264 转码）：{result.Path}";

                case BmsBgaVideoCache.FfmpegProbeState.ReadyWithoutH264:
                    return $@"ffmpeg 可用，但缺少 libx264 编码器，将回退 mpeg4 转码（如需 H.264 画质请换用完整版 ffmpeg）：{result.Path}";

                case BmsBgaVideoCache.FfmpegProbeState.NotExecutable:
                    return $@"找到 ffmpeg 但无法正常执行（{result.Detail}）：{result.Path}。请确认是有效的 Windows ffmpeg.exe。";

                default:
                    return @"未检测到 ffmpeg。请将 ffmpeg.exe 放入数据目录（或加入系统 PATH）后重试。";
            }
        }

        // Opens the OMS data directory (the storage root) where the user can drop ffmpeg.exe. The trailing separator
        // makes the host present it as a folder rather than trying to open it as a file.
        private void openFfmpegDirectory()
            => host.OpenFileExternally(storage.GetFullPath(string.Empty) + Path.DirectorySeparatorChar);

        // Mirrors the candidate list BmsBgaPlayer feeds the transcode cache: the data directory first, then the app dir.
        private string[] getFfmpegCandidates() => new[]
        {
            storage.GetFullPath("ffmpeg.exe"),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
        };
    }
}
