// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
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
                                    Text = "Difficulty tables",
                                    Font = OsuFont.GetFont(size: 20, weight: FontWeight.Bold),
                                },
                                new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 14))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = "Import a local mirror directory, index.html, or header.json file. Bundled presets are claimed automatically when the imported table name matches.",
                                },
                            }
                        }
                    },
                    new SettingsItemV2(pathTextBox = new FormTextBox
                    {
                        Caption = "Local table path",
                        PlaceholderText = "Directory, index.html, or header.json",
                        Current = importPath,
                    }),
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = SettingsPanel.CONTENT_PADDING,
                        Direction = FillDirection.Horizontal,
                        Spacing = new Vector2(10, 0),
                        Children = new Drawable[]
                        {
                            browseButton = createActionButton("Browse folder", openDirectoryPicker, 150),
                            importButton = createActionButton("Import path", startImport, 130),
                            refreshAllButton = createActionButton("Refresh all", startRefreshAll, 130),
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

            tableManager = BmsDifficultyTableManager.GetShared(storage);
            tableManager.TableDataChanged += handleTableDataChanged;

            importPath.BindValueChanged(_ => updateActionStates(), true);
            operationInProgress.BindValueChanged(_ =>
            {
                updateActionStates();
                refreshSources();
            }, true);

            refreshSources();
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && tableManager != null)
                tableManager.TableDataChanged -= handleTableDataChanged;

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
            bool busy = operationInProgress.Value;
            bool hasImportPath = !string.IsNullOrWhiteSpace(importPath.Value);
            bool hasRefreshableSources = tableManager != null && getVisibleSources().Any(source => !string.IsNullOrWhiteSpace(source.LocalPath));

            browseButton.Enabled.Value = performer != null && !busy;
            importButton.Enabled.Value = hasImportPath && !busy;
            refreshAllButton.Enabled.Value = hasRefreshableSources && !busy;
        }

        private void refreshSources()
        {
            IReadOnlyList<BmsDifficultyTableSourceInfo> sources = getVisibleSources();
            int enabledEntries = sources.Where(source => source.Enabled).Sum(source => source.Entries.Count);
            int enabledSources = sources.Count(source => source.Enabled);

            summaryText.Text = sources.Count == 0
                ? "No local difficulty tables imported yet."
                : $"{enabledSources} enabled source{(enabledSources == 1 ? string.Empty : "s")}, {enabledEntries} loaded entr{(enabledEntries == 1 ? "y" : "ies")}.";

            sourcesContainer.Clear();

            if (sources.Count == 0)
            {
                sourcesContainer.Add(new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 13))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Text = "Imported sources will appear here once a local table mirror has been added.",
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

        private IReadOnlyList<BmsDifficultyTableSourceInfo> getVisibleSources()
            => tableManager.GetSources()
                           .Where(source => !source.IsPreset || !string.IsNullOrWhiteSpace(source.LocalPath) || source.Entries.Count > 0)
                           .OrderBy(source => source.SortOrder)
                           .ThenBy(source => source.DisplayName, StringComparer.OrdinalIgnoreCase)
                           .ToList();

        private void handleTableDataChanged() => Schedule(refreshSources);

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
            if (operationInProgress.Value)
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
                    Text = $"Imported difficulty table '{source.DisplayName}'."
                });
                return;
            }

            if (task.IsFaulted)
            {
                Logger.Error(task.Exception, "Failed to import BMS difficulty table source.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = $"Failed to import difficulty table: {getErrorMessage(task.Exception)}"
                });
            }
        }

        private void startRefreshAll()
        {
            if (operationInProgress.Value)
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
                    Text = "Refreshed local difficulty tables."
                });
                return;
            }

            if (task.IsFaulted)
            {
                Logger.Error(task.Exception, "Failed to refresh all BMS difficulty table sources.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = $"Failed to refresh difficulty tables: {getErrorMessage(task.Exception)}"
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
                    Text = $"Refreshed difficulty table '{task.GetResultSafely().DisplayName}'."
                });
                return;
            }

            if (task.IsFaulted)
            {
                Logger.Error(task.Exception, $"Failed to refresh BMS difficulty table source {source.ID}.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = $"Failed to refresh '{source.DisplayName}': {getErrorMessage(task.Exception)}"
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

            dialogOverlay.Push(new ConfirmDialog($"Remove difficulty table source '{source.DisplayName}'?", () => remove(source)));
        }

        private void remove(BmsDifficultyTableSourceInfo source)
        {
            tableManager.RemoveSource(source.ID);
            notificationOverlay?.Post(new ProgressCompletionNotification
            {
                Text = $"Removed difficulty table source '{source.DisplayName}'."
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

                string sourceType = source.IsPreset ? "Preset" : "Custom";
                string enabledState = source.Enabled ? "Enabled" : "Disabled";
                string entryCount = source.Entries.Count == 1 ? "1 entry" : $"{source.Entries.Count} entries";
                string refreshState = source.LastRefreshed?.ToLocalTime().ToString("g") ?? "Not refreshed yet";

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
                                    Text = $"{sourceType} source · {enabledState} · {entryCount} · Imported {source.ImportedAt.LocalDateTime:g}",
                                    Font = OsuFont.GetFont(size: 13),
                                    Colour = colourProvider.Content2,
                                },
                                new OsuSpriteText
                                {
                                    Text = $"Last refreshed: {refreshState}",
                                    Font = OsuFont.GetFont(size: 13),
                                    Colour = colourProvider.Content2,
                                },
                                new OsuTextFlowContainer(text => text.Font = OsuFont.GetFont(size: 12))
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Text = source.LocalPath ?? "Bundled preset placeholder",
                                },
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(10, 0),
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
                toggleButton = createButton(source.Enabled ? "Disable" : "Enable", ToggleEnabled);

                var buttons = new List<Drawable>
                {
                    toggleButton,
                };

                if (Refresh != null)
                    buttons.Add(refreshButton = createButton("Refresh", Refresh));

                if (Remove != null)
                {
                    buttons.Add(removeButton = new DangerousSettingsButtonV2
                    {
                        RelativeSizeAxes = Axes.None,
                        AutoSizeAxes = Axes.None,
                        Width = 110,
                        Height = 40,
                        Text = "Remove",
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
