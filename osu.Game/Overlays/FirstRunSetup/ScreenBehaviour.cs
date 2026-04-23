// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.FirstRunSetup
{
    [LocalisableDescription(typeof(FirstRunSetupOverlayStrings), nameof(FirstRunSetupOverlayStrings.DifficultyTableSetupTitle))]
    public partial class ScreenBehaviour : WizardScreen
    {
        private const string bmsDifficultyTableManagerTypeName = "osu.Game.Rulesets.Bms.DifficultyTable.BmsDifficultyTableManager";
        private const string bmsRulesetAssemblyName = "osu.Game.Rulesets.Bms";

        private readonly BindableBool importInProgress = new BindableBool();
        private readonly List<PresetSelectionRow> presetRows = new List<PresetSelectionRow>();

        private SettingsButtonV2 importButton = null!;
        private OsuSpriteText statusText = null!;
        private DifficultyTableManagerAdapter? tableManager;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Content.Children = new Drawable[]
            {
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: CONTENT_FONT_SIZE))
                {
                    Text = "oms内置了一些难度表，你可以在此处选择启用。",
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                new OsuTextFlowContainer(cp => cp.Font = OsuFont.Default.With(size: 12))
                {
                    Text = "以下难度表均来自 zris 先生主页的难度表镜像，zris的主页：zris.work",
                    Colour = OverlayColourProvider.Content2,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y
                },
                importButton = new SettingsButtonV2
                {
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.None,
                    Width = 260,
                    Height = 50,
                    Text = "导入所选难度表",
                    Action = importSelectedPresets,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Child = statusText = new OsuSpriteText
                    {
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        Colour = OverlayColourProvider.Content2,
                    },
                },
                createPresetGroup("SP 7K", sp7kPresets),
                createPresetGroup("DP 14K", dp14kPresets),
                createPresetGroup("特殊/杂项", specialPresets),
                createPresetGroup("PMS 9K", pms9kPresets),
            };

            importInProgress.BindValueChanged(_ => updateActionState(), true);

            foreach (var row in presetRows)
                row.Current.BindValueChanged(_ => updateActionState());
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            statusText.Text = "正在加载难度表管理器...";
            importButton.Enabled.Value = false;
            _ = initialiseTableManagerAsync();
        }

        private Drawable createPresetGroup(string title, IEnumerable<DifficultyTablePreset> presets)
        {
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 6),
            };

            foreach (var preset in presets)
            {
                var row = new PresetSelectionRow(preset);
                presetRows.Add(row);
                flow.Add(row);
            }

            return new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 10),
                Children = new Drawable[]
                {
                    new OsuSpriteText
                    {
                        Text = title,
                        Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                        Colour = OverlayColourProvider.Content2,
                    },
                    flow,
                },
            };
        }

        private async Task initialiseTableManagerAsync()
        {
            try
            {
                tableManager = await DifficultyTableManagerAdapter.CreateAsync(storage).ConfigureAwait(true);

                if (IsDisposed)
                    return;

                statusText.Text = "勾选需要启用的难度表后，点击“导入所选难度表”。";
                updateActionState();
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialise difficulty table manager from the first-run setup overlay.");

                if (IsDisposed)
                    return;

                statusText.Text = $"难度表管理器加载失败：{ex.GetBaseException().Message}";
                updateActionState();
            }
        }

        private void updateActionState()
            => importButton.Enabled.Value = tableManager != null && !importInProgress.Value && presetRows.Any(row => row.Current.Value);

        private async void importSelectedPresets()
        {
            if (tableManager == null || importInProgress.Value)
                return;

            var selectedPresets = presetRows.Where(row => row.Current.Value)
                                            .Select(row => row.Preset)
                                            .ToList();

            if (selectedPresets.Count == 0)
            {
                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = "请先勾选至少一个难度表。"
                });
                return;
            }

            importInProgress.Value = true;

            int successCount = 0;
            List<string> failures = new List<string>();

            for (int i = 0; i < selectedPresets.Count; i++)
            {
                DifficultyTablePreset preset = selectedPresets[i];
                statusText.Text = $"正在导入 {i + 1}/{selectedPresets.Count}：{preset.Name}";

                try
                {
                    await tableManager.ImportFromPath(preset.Url).ConfigureAwait(true);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to import difficulty table preset '{preset.Name}'.");
                    failures.Add($"{preset.Name}：{ex.GetBaseException().Message}");
                }
            }

            if (IsDisposed)
                return;

            importInProgress.Value = false;
            statusText.Text = failures.Count == 0
                ? $"已完成导入，共 {successCount} 个难度表。"
                : $"导入完成：成功 {successCount} 个，失败 {failures.Count} 个。";

            if (failures.Count == 0)
            {
                notificationOverlay?.Post(new ProgressCompletionNotification
                {
                    Text = statusText.Text,
                });
            }
            else
            {
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = failures.Count == 1 ? failures[0] : statusText.Text,
                });
            }
        }

        private sealed record DifficultyTablePreset(string Name, string Url);

        private sealed class DifficultyTableManagerAdapter
        {
            private readonly object manager;
            private readonly MethodInfo importFromPathMethod;

            private DifficultyTableManagerAdapter(object manager, MethodInfo importFromPathMethod)
            {
                this.manager = manager;
                this.importFromPathMethod = importFromPathMethod;
            }

            public static async Task<DifficultyTableManagerAdapter> CreateAsync(Storage storage)
            {
                var managerType = resolveManagerType();

                var getSharedAsyncMethod = managerType.GetMethod("GetSharedAsync", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Storage), typeof(System.Threading.CancellationToken) }, null)
                                           ?? throw new MissingMethodException(managerType.FullName, "GetSharedAsync");

                var importFromPathMethod = managerType.GetMethod("ImportFromPath", BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(string), typeof(Guid?), typeof(bool), typeof(System.Threading.CancellationToken) }, null)
                                           ?? throw new MissingMethodException(managerType.FullName, "ImportFromPath");

                var task = (Task)getSharedAsyncMethod.Invoke(null, new object?[] { storage, default(System.Threading.CancellationToken) })!;
                await task.ConfigureAwait(false);

                object manager = task.GetType().GetProperty("Result")?.GetValue(task)
                                 ?? throw new InvalidOperationException("BMS 难度表管理器初始化失败。");

                return new DifficultyTableManagerAdapter(manager, importFromPathMethod);
            }

            public async Task ImportFromPath(string path)
            {
                var task = (Task)importFromPathMethod.Invoke(manager, new object?[] { path, null, true, default(System.Threading.CancellationToken) })!;
                await task.ConfigureAwait(false);
            }

            private static Type resolveManagerType()
            {
                var managerType = Type.GetType($"{bmsDifficultyTableManagerTypeName}, {bmsRulesetAssemblyName}", throwOnError: false);

                if (managerType != null)
                    return managerType;

                var assembly = Assembly.Load(bmsRulesetAssemblyName);

                return assembly.GetType(bmsDifficultyTableManagerTypeName, throwOnError: true)
                       ?? throw new InvalidOperationException("未找到 BMS 难度表模块，请确认 BMS ruleset 已被加载。");
            }
        }

        private partial class PresetSelectionRow : CompositeDrawable
        {
            public DifficultyTablePreset Preset { get; }

            public BindableBool Current { get; } = new BindableBool();

            public PresetSelectionRow(DifficultyTablePreset preset)
            {
                Preset = preset;
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                InternalChild = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 2),
                    Children = new Drawable[]
                    {
                        new SettingsCheckbox
                        {
                            LabelText = Preset.Name,
                            Current = Current,
                        },
                        new OsuSpriteText
                        {
                            Text = Preset.Url,
                            Font = OsuFont.GetFont(size: 12),
                            Colour = Colour4.LightGray,
                            Padding = new MarginPadding { Left = 8 },
                        },
                    },
                };
            }
        }

        private static readonly DifficultyTablePreset[] sp7kPresets =
        {
            new("通常難易度表", "http://zris.work/bmstable/normal/normal_header.json"),
            new("発狂BMS難易度表", "http://zris.work/bmstable/insane/insane_header.json"),
            new("第三期Overjoy", "http://zris.work/bmstable/overjoy/header.json"),
            new("NEW GENERATION通常", "http://zris.work/bmstable/normal2/header.json"),
            new("NEW GENERATION発狂", "http://zris.work/bmstable/insane2/insane_header.json"),
            new("発狂推定EASY", "http://zris.work/bmstable/insane_easy/header_json.json"),
            new("発狂推定NORMAL", "http://zris.work/bmstable/insane_normal/header_json.json"),
            new("発狂推定HARD", "http://zris.work/bmstable/insane_hard/header_json.json"),
            new("発狂推定FC", "http://zris.work/bmstable/insane_fc/header_json.json"),
            new("Satellite", "http://zris.work/bmstable/satellite/header.json"),
            new("Stella", "http://zris.work/bmstable/stella/header.json"),
            new("Stellalite", "http://zris.work/bmstable/stellalite/Stellalite-header.json"),
            new("Satellite推定Easy", "http://zris.work/bmstable/satellite_easy/Satellite-EASY-header.json"),
            new("Satellite推定Normal", "http://zris.work/bmstable/satellite_normal/Satellite-NORMAL-header.json"),
            new("Satellite推定Hard", "http://zris.work/bmstable/satellite_hard/Satellite-HARD-header.json"),
            new("Satellite推定FC", "http://zris.work/bmstable/satellite_fullcombo/Satellite-FULLCOMBO-header.json"),
            new("Stella推定Easy", "http://zris.work/bmstable/stella_easy/Stella-EASY-header.json"),
            new("Stella推定Normal", "http://zris.work/bmstable/stella_normal/Stella-NORMAL-header.json"),
            new("Stella推定Hard", "http://zris.work/bmstable/stella_hard/Stella-HARD-header.json"),
            new("Stella推定FC", "http://zris.work/bmstable/stella_fullcombo/Stella-FULLCOMBO-header.json"),
            new("10K", "http://zris.work/bmstable/10k/head.json"),
        };

        private static readonly DifficultyTablePreset[] dp14kPresets =
        {
            new("DP Satellite", "http://zris.work/bmstable/dp_satellite/header.json"),
            new("DP Stella", "http://zris.work/bmstable/dp_stella/header.json"),
            new("δ難易度表", "http://zris.work/bmstable/dp_normal/dpn_header.json"),
            new("発狂DP難易度表", "http://zris.work/bmstable/dp_insane/dpi_header.json"),
            new("DP Overjoy", "http://zris.work/bmstable/dp_overjoy/header.json"),
            new("DPBMS白(通常)", "http://zris.work/bmstable/dp_white/header.json"),
            new("DPBMS黒(発狂)", "http://zris.work/bmstable/dp_black/header.json"),
            new("発狂DPごった煮", "http://zris.work/bmstable/dp_zhu/header.json"),
            new("発狂14key闇鍋", "http://zris.work/bmstable/dp_anguo/head14.json"),
            new("DPBMSと諸感", "http://zris.work/bmstable/dp_zhugan/header.json"),
            new("ereter.net DP EasyClear", "http://zris.work/bmstable/dp_insane_easy/ereter_ec_head.json"),
            new("ereter.net DP HardClear", "http://zris.work/bmstable/dp_insane_hard/ereter_hc_head.json"),
            new("DP Satellite推定Easy", "http://zris.work/bmstable/dp_satellite_easy/DP-Satellite-EASY-header.json"),
            new("DP Satellite推定Normal", "http://zris.work/bmstable/dp_satellite_normal/DP-Satellite-NORMAL-header.json"),
            new("DP Satellite推定Hard", "http://zris.work/bmstable/dp_satellite_hard/DP-Satellite-HARD-header.json"),
            new("DP Satellite推定FC", "http://zris.work/bmstable/dp_satellite_fullcombo/DP-Satellite-FULLCOMBO-header.json"),
        };

        private static readonly DifficultyTablePreset[] specialPresets =
        {
            new("LN難易度", "http://zris.work/bmstable/ln/ln_header.json"),
            new("Luminous", "http://zris.work/bmstable/luminous/header.json"),
            new("Scramble", "http://zris.work/bmstable/scramble/header.json"),
            new("皿難易度表(3rd)", "http://zris.work/bmstable/scratch/header.json"),
            new("縦連打コレクション", "http://zris.work/bmstable/zonglian/header.json"),
            new("連打難易度表(第二期)", "http://zris.work/bmstable/renda/header.json"),
            new("池田的難易度表", "http://zris.work/bmstable/chitian/header.json"),
            new("BMS図書館", "http://zris.work/bmstable/turbow/header.json"),
            new("発狂難易度DB", "http://zris.work/bmstable/hex/db.json"),
            new("BMS同梱譜面", "http://zris.work/bmstable/tongkun/header.json"),
            new("東方BMSまとめ表", "http://zris.work/bmstable/touhou/header.json"),
            new("オマージュBMS", "http://zris.work/bmstable/homage/header.json"),
        };

        private static readonly DifficultyTablePreset[] pms9kPresets =
        {
            new("PMSデータベース(Lv1~45)", "http://zris.work/bmstable/pms_normal/pmsdatabase_header.json"),
            new("発狂PMSデータベース(lv46~)", "http://zris.work/bmstable/pms_insane/insane_pmsdatabase_header.json"),
            new("発狂PMS難易度表", "http://zris.work/bmstable/pms_upper/header.json"),
            new("PMS Courseデータ", "http://zris.work/bmstable/pms_course/course_header.json"),
        };
    }
}
