// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Rulesets.Bms.DifficultyTable
{
    public partial class BmsDifficultyTableSettingsSection : CompositeDrawable
    {
        private readonly Bindable<string> importPath = new Bindable<string>(string.Empty);
        private readonly BindableBool operationInProgress = new BindableBool();

        private BmsDifficultyTableManager tableManager = null!;
        private FormTextBox pathTextBox = null!;
        private SettingsButtonV2 browseButton = null!;
        private SettingsButtonV2 importButton = null!;
        private SettingsButtonV2 refreshAllButton = null!;
        private OsuSpriteText summaryText = null!;
        private FillFlowContainer sourcesContainer = null!;
        private IReadOnlyList<BmsDifficultyTableSourceInfo> visibleSources = Array.Empty<BmsDifficultyTableSourceInfo>();
        private int managerLoadSequence;
        private int sourcesRefreshSequence;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformFromScreenRunner? performer { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;

            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, SettingsSection.ITEM_SPACING_V2),
                Children = new Drawable[]
                {
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
                            Spacing = new Vector2(0, 6),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = "难度表",
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                },
                                new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = "导入本地镜像目录、index.html、header.json，或直接粘贴在线难度表链接。当导入的表名与内置预置一致时，会自动认领对应预置来源。",
                                },
                            }
                        }
                    },
                    new SettingsItemV2(pathTextBox = new FormTextBox
                    {
                        Caption = "表来源路径或链接",
                        PlaceholderText = "目录、index.html、header.json 或 https://...",
                        Current = importPath,
                    }),
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Direction = FillDirection.Full,
                        Spacing = new Vector2(10),
                        Children = new Drawable[]
                        {
                            browseButton = createActionButton("浏览文件夹", openDirectoryPicker, 150),
                            importButton = createActionButton("导入路径", startImport, 130),
                            refreshAllButton = createActionButton("全部刷新", startRefreshAll, 130),
                        }
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Child = summaryText = new OsuSpriteText
                        {
                            Font = OsuFont.GetFont(size: 14, weight: FontWeight.SemiBold),
                        }
                    },
                    sourcesContainer = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                    },
                }
            };

            pathTextBox.OnCommit += (_, _) => startImport();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            importPath.BindValueChanged(_ => updateActionStates(), true);
            operationInProgress.BindValueChanged(_ =>
            {
                updateActionStates();
                refreshSources();
            }, true);

            showLoadingState();
            _ = initialiseTableManagerAsync();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                Interlocked.Increment(ref managerLoadSequence);
                Interlocked.Increment(ref sourcesRefreshSequence);

                if (tableManager != null)
                    tableManager.TableDataChanged -= handleTableDataChanged;
            }

            base.Dispose(isDisposing);
        }

        private SettingsButtonV2 createActionButton(LocalisableString text, Action action, float width)
            => new SettingsButtonV2
            {
                RelativeSizeAxes = Axes.None,
                AutoSizeAxes = Axes.None,
                Width = width,
                Height = 40,
                Text = text,
                Action = action,
            };

        private void updateActionStates()
        {
            bool ready = tableManager != null;
            bool busy = operationInProgress.Value;
            bool hasImportPath = !string.IsNullOrWhiteSpace(importPath.Value);
            bool hasRefreshableSources = visibleSources.Any(source => !string.IsNullOrWhiteSpace(source.LocalPath));

            browseButton.Enabled.Value = performer != null && ready && !busy;
            importButton.Enabled.Value = ready && hasImportPath && !busy;
            refreshAllButton.Enabled.Value = ready && hasRefreshableSources && !busy;
        }

        private void refreshSources()
        {
            _ = refreshSourcesAsync();
        }

        private async Task initialiseTableManagerAsync()
        {
            int loadSequence = Interlocked.Increment(ref managerLoadSequence);

            try
            {
                var manager = await BmsDifficultyTableManager.GetSharedAsync(storage).ConfigureAwait(false);

                Schedule(() =>
                {
                    if (IsDisposed || loadSequence != managerLoadSequence)
                        return;

                    tableManager = manager;
                    tableManager.TableDataChanged -= handleTableDataChanged;
                    tableManager.TableDataChanged += handleTableDataChanged;

                    refreshSources();
                    updateActionStates();
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to initialise the BMS difficulty table manager.");

                Schedule(() =>
                {
                    if (IsDisposed || loadSequence != managerLoadSequence)
                        return;

                    visibleSources = Array.Empty<BmsDifficultyTableSourceInfo>();
                    summaryText.Text = "难度表功能当前不可用。";
                    sourcesContainer.Clear();
                    sourcesContainer.Add(new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 13))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = $"加载难度表失败：{getErrorMessage(ex)}",
                    });
                    updateActionStates();
                });
            }
        }

        private async Task refreshSourcesAsync()
        {
            if (tableManager == null)
                return;

            int refreshSequence = Interlocked.Increment(ref sourcesRefreshSequence);

            try
            {
                var sources = await tableManager.GetSourcesAsync().ConfigureAwait(false);
                var filteredSources = filterVisibleSources(sources);
                int enabledEntries = filteredSources.Where(source => source.Enabled).Sum(source => source.Entries.Count);
                int enabledSources = filteredSources.Count(source => source.Enabled);

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != sourcesRefreshSequence)
                        return;

                    visibleSources = filteredSources;
                    applyVisibleSources(filteredSources, enabledSources, enabledEntries);
                    updateActionStates();
                });
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to load visible BMS difficulty table sources.");

                Schedule(() =>
                {
                    if (IsDisposed || refreshSequence != sourcesRefreshSequence)
                        return;

                    visibleSources = Array.Empty<BmsDifficultyTableSourceInfo>();
                    summaryText.Text = "难度表功能当前不可用。";
                    sourcesContainer.Clear();
                    sourcesContainer.Add(new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 13))
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Text = $"读取难度表失败：{getErrorMessage(ex)}",
                    });
                    updateActionStates();
                });
            }
        }

        private void applyVisibleSources(IReadOnlyList<BmsDifficultyTableSourceInfo> sources, int enabledSources, int enabledEntries)
        {
            summaryText.Text = sources.Count == 0
                ? "尚未导入本地难度表。"
                : $"已启用 {enabledSources} 个来源，已加载 {enabledEntries} 个条目。";

            sourcesContainer.Clear();

            if (sources.Count == 0)
            {
                sourcesContainer.Add(new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 13))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "添加本地难度表镜像或在线难度表链接后，这里会显示已导入的来源。",
                });

                return;
            }

            sourcesContainer.AddRange(sources.Select(source => new DifficultyTableSourceCard(source, operationInProgress)
            {
                ToggleEnabled = () => toggleSourceEnabled(source),
                Refresh = string.IsNullOrWhiteSpace(source.LocalPath) ? null : () => startRefresh(source),
                Remove = source.IsPreset ? null : () => confirmRemove(source),
            }));
        }

        private void showLoadingState()
        {
            summaryText.Text = "正在加载难度表...";
            sourcesContainer.Clear();
            sourcesContainer.Add(new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 13))
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Text = "正在后台准备本地难度表缓存...",
            });
        }

        private static IReadOnlyList<BmsDifficultyTableSourceInfo> filterVisibleSources(IReadOnlyList<BmsDifficultyTableSourceInfo> sources)
            => sources.Where(source => !source.IsPreset || !string.IsNullOrWhiteSpace(source.LocalPath) || source.Entries.Count > 0)
                      .OrderBy(source => source.SortOrder)
                      .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
                      .ToList();

        private void handleTableDataChanged() => refreshSources();

        private void openDirectoryPicker()
        {
            if (operationInProgress.Value)
                return;

            performer?.PerformFromScreen(screen =>
            {
                if (screen is Screen stackScreen)
                    stackScreen.Push(new BmsDifficultyTableDirectorySelectScreen(path => Schedule(() => importPath.Value = path)));
            });
        }

        private void startImport()
        {
            if (tableManager == null || operationInProgress.Value)
                return;

            string path = importPath.Value.Trim();

            if (string.IsNullOrWhiteSpace(path))
                return;

            operationInProgress.Value = true;
            tableManager.ImportFromPath(path).ContinueWith(task => Schedule(() => completeImport(path, task)));
        }

        private void completeImport(string path, Task<BmsDifficultyTableSourceInfo> task)
        {
            operationInProgress.Value = false;

            if (task.IsCompletedSuccessfully)
            {
                var source = task.GetResultSafely();
                importPath.Value = string.Equals(path, source.LocalPath, StringComparison.OrdinalIgnoreCase) ? string.Empty : importPath.Value;
                notificationOverlay?.Post(new ProgressCompletionNotification
                {
                    Text = $"已导入难度表“{source.DisplayName}”。"
                });
                return;
            }

            if (task.IsFaulted)
            {
                Logger.Error(task.Exception, "Failed to import BMS difficulty table source.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = $"导入难度表失败：{getErrorMessage(task.Exception)}"
                });
            }
        }

        private void startRefreshAll()
        {
            if (tableManager == null || operationInProgress.Value)
                return;

            operationInProgress.Value = true;
            tableManager.RefreshAllTables().ContinueWith(task => Schedule(() => completeRefreshAll(task)));
        }

        private void completeRefreshAll(Task task)
        {
            operationInProgress.Value = false;

            if (task.IsCompletedSuccessfully)
            {
                notificationOverlay?.Post(new ProgressCompletionNotification
                {
                    Text = "已刷新本地难度表。"
                });
                return;
            }

            if (task.IsFaulted)
            {
                Logger.Error(task.Exception, "Failed to refresh all BMS difficulty table sources.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = $"刷新难度表失败：{getErrorMessage(task.Exception)}"
                });
            }
        }

        private void toggleSourceEnabled(BmsDifficultyTableSourceInfo source)
        {
            if (operationInProgress.Value)
                return;

            tableManager.SetSourceEnabled(source.ID, !source.Enabled);
        }

        private void startRefresh(BmsDifficultyTableSourceInfo source)
        {
            if (operationInProgress.Value)
                return;

            operationInProgress.Value = true;
            tableManager.RefreshTable(source.ID).ContinueWith(task => Schedule(() => completeRefresh(source, task)));
        }

        private void completeRefresh(BmsDifficultyTableSourceInfo source, Task<BmsDifficultyTableSourceInfo> task)
        {
            operationInProgress.Value = false;

            if (task.IsCompletedSuccessfully)
            {
                notificationOverlay?.Post(new ProgressCompletionNotification
                {
                    Text = $"已刷新难度表“{task.GetResultSafely().DisplayName}”。"
                });
                return;
            }

            if (task.IsFaulted)
            {
                Logger.Error(task.Exception, $"Failed to refresh BMS difficulty table source {source.ID}.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = $"刷新“{source.DisplayName}”失败：{getErrorMessage(task.Exception)}"
                });
            }
        }

        private void confirmRemove(BmsDifficultyTableSourceInfo source)
        {
            if (operationInProgress.Value)
                return;

            if (dialogOverlay == null)
            {
                remove(source);
                return;
            }

            dialogOverlay.Push(new ConfirmDialog($"要移除难度表来源“{source.DisplayName}”吗？", () => remove(source)));
        }

        private void remove(BmsDifficultyTableSourceInfo source)
        {
            tableManager.RemoveSource(source.ID);
            notificationOverlay?.Post(new ProgressCompletionNotification
            {
                Text = $"已移除难度表来源“{source.DisplayName}”。"
            });
        }

        private static string getErrorMessage(Exception exception)
            => exception.GetBaseException().Message;

        private partial class DifficultyTableSourceCard : CompositeDrawable
        {
            private readonly BmsDifficultyTableSourceInfo source;
            private readonly IBindable<bool> operationInProgress;

            public Action? ToggleEnabled { get; init; }

            public Action? Refresh { get; init; }

            public Action? Remove { get; init; }

            private SettingsButtonV2 toggleButton = null!;
            private SettingsButtonV2? refreshButton;
            private DangerousSettingsButtonV2? removeButton;

            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            public DifficultyTableSourceCard(BmsDifficultyTableSourceInfo source, BindableBool operationInProgress)
            {
                this.source = source;
                this.operationInProgress = operationInProgress.GetBoundCopy();
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;

                string sourceType = source.IsPreset ? "预置" : "自定义";
                string enabledState = source.Enabled ? "已启用" : "已禁用";
                string entryCount = source.Entries.Count == 1 ? "1 个条目" : $"{source.Entries.Count} 个条目";
                string refreshState = source.LastRefreshed?.ToLocalTime().ToString("g") ?? "尚未刷新";

                InternalChild = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 10,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = colourProvider.Background4,
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(15),
                            Spacing = new Vector2(0, 8),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = string.IsNullOrWhiteSpace(source.Symbol) ? source.DisplayName : $"{source.DisplayName}  {source.Symbol}",
                                    Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold),
                                },
                                new OsuSpriteText
                                {
                                    Text = $"{sourceType}来源 · {enabledState} · {entryCount} · 导入于 {source.ImportedAt.LocalDateTime:g}",
                                    Font = OsuFont.GetFont(size: 13),
                                    Colour = colourProvider.Content2,
                                },
                                new OsuSpriteText
                                {
                                    Text = $"上次刷新：{refreshState}",
                                    Font = OsuFont.GetFont(size: 13),
                                    Colour = colourProvider.Content2,
                                },
                                new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 12))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = source.LocalPath ?? "内置预置占位来源",
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Full,
                                    Spacing = new Vector2(10),
                                    Children = buildButtons(),
                                },
                            }
                        },
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                operationInProgress.BindValueChanged(_ => updateActionStates(), true);
            }

            private Drawable[] buildButtons()
            {
                toggleButton = createButton(source.Enabled ? "禁用" : "启用", ToggleEnabled);

                var buttons = new List<Drawable>
                {
                    toggleButton,
                };

                if (Refresh != null)
                    buttons.Add(refreshButton = createButton("刷新", Refresh));

                if (Remove != null)
                {
                    buttons.Add(removeButton = new DangerousSettingsButtonV2
                    {
                        RelativeSizeAxes = Axes.None,
                        AutoSizeAxes = Axes.None,
                        Width = 110,
                        Height = 40,
                        Text = "移除",
                        Action = Remove,
                    });
                }

                return buttons.ToArray();
            }

            private SettingsButtonV2 createButton(LocalisableString text, Action? action)
                => new SettingsButtonV2
                {
                    RelativeSizeAxes = Axes.None,
                    AutoSizeAxes = Axes.None,
                    Width = 110,
                    Height = 40,
                    Text = text,
                    Action = action,
                };

            private void updateActionStates()
            {
                bool enabled = !operationInProgress.Value;

                toggleButton.Enabled.Value = enabled;

                if (refreshButton != null)
                    refreshButton.Enabled.Value = enabled;

                if (removeButton != null)
                    removeButton.Enabled.Value = enabled;
            }
        }
    }
}
