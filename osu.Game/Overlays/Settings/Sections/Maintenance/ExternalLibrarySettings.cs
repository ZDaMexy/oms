// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class ExternalLibrarySettings : SettingsSubsection
    {
        protected override LocalisableString Header => ExternalLibrarySettingsStrings.ExternalLibraryHeader;

        [Resolved(CanBeNull = true)]
        private ExternalLibraryConfig? libraryConfig { get; set; }

        [Resolved(CanBeNull = true)]
        private ExternalLibraryScanner? libraryScanner { get; set; }

        [Resolved(CanBeNull = true)]
        private ManagedLibraryScanner? managedLibraryScanner { get; set; }

        [Resolved(CanBeNull = true)]
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        private FillFlowContainer rootsList = null!;
        private SettingsButtonV2 scanExternalButton = null!;
        private SettingsButtonV2? scanInternalButton;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (libraryConfig == null)
                return;

            rootsList = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 4),
            };

            scanExternalButton = new SettingsButtonV2
            {
                Text = ExternalLibrarySettingsStrings.ScanExternalLibraries,
                Action = scanExternalLibraries,
            };

            var children = new List<Drawable>
            {
                rootsList,
                new SettingsButtonV2
                {
                    Text = ExternalLibrarySettingsStrings.AddBmsLibraryFolder,
                    Action = () => addRoot(ExternalLibraryRootType.BMS),
                },
                new SettingsButtonV2
                {
                    Text = ExternalLibrarySettingsStrings.AddManiaLibraryFolder,
                    Action = () => addRoot(ExternalLibraryRootType.Mania),
                },
                scanExternalButton,
            };

            if (managedLibraryScanner != null)
            {
                children.Add(scanInternalButton = new SettingsButtonV2
                {
                    Text = ExternalLibrarySettingsStrings.ScanInternalLibraries,
                    Action = scanInternalLibraries,
                });
            }

            AddRange(children.ToArray());

            refreshRootsList();
        }

        private void addRoot(ExternalLibraryRootType type)
        {
            performer?.PerformFromScreen(menu =>
            {
                var selectScreen = new ExternalLibrarySelectScreen();
                selectScreen.Selected = directory =>
                {
                    try
                    {
                        bool added = libraryConfig!.AddRoot(directory.FullName, type);

                        Schedule(() =>
                        {
                            if (added)
                            {
                                refreshRootsList();
                                notificationOverlay?.Post(new SimpleNotification
                                {
                                    Text = ExternalLibrarySettingsStrings.AddedLibrary(type.ToString(), directory.FullName),
                                });
                            }
                            else
                            {
                                notificationOverlay?.Post(new SimpleNotification
                                {
                                    Text = ExternalLibrarySettingsStrings.FolderAlreadyRegistered,
                                });
                            }
                        });
                    }
                    catch (DirectoryNotFoundException)
                    {
                        Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification
                        {
                            Text = ExternalLibrarySettingsStrings.DirectoryNotFound(directory.FullName),
                        }));
                    }
                };

                menu.Push(selectScreen);
            });
        }

        private void removeRoot(ExternalLibraryRoot root)
        {
            if (libraryConfig == null) return;

            libraryConfig.RemoveRoot(root.Path);
            Schedule(refreshRootsList);
        }

        private void scanExternalLibraries()
        {
            if (libraryScanner == null) return;

            scanLibraries(
                (progress, cancellationToken) => libraryScanner.ScanAllRoots(progress, cancellationToken),
                ExternalLibrarySettingsStrings.ScanningExternalLibraries,
                "External library scan failed.",
                refreshRoots: true);
        }

        private void scanInternalLibraries()
        {
            if (managedLibraryScanner == null) return;

            scanLibraries(
                (progress, cancellationToken) => managedLibraryScanner.ScanAllRoots(progress, cancellationToken),
                ExternalLibrarySettingsStrings.ScanningInternalLibraries,
                "Managed library scan failed.");
        }

        private void scanLibraries(Func<IProgress<ExternalLibraryScanner.ScanProgress>, CancellationToken, Task<ExternalLibraryScanner.ScanResult>> scanOperation,
                                   LocalisableString initialText, string failureLogMessage, bool refreshRoots = false)
        {
            setScanButtonsEnabled(false);

            var notification = new ProgressNotification
            {
                Text = initialText,
                Progress = 0,
                State = ProgressNotificationState.Active,
            };

            notificationOverlay?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    var result = await scanOperation(
                        new Progress<ExternalLibraryScanner.ScanProgress>(progress => updateProgressNotification(notification, progress, initialText)),
                        notification.CancellationToken
                    ).ConfigureAwait(false);

                    notification.CompletionText = ExternalLibrarySettingsStrings.ScanComplete(result.Imported, result.Skipped, result.Errors);
                    notification.State = ProgressNotificationState.Completed;

                    Schedule(() =>
                    {
                        if (IsDisposed)
                            return;

                        setScanButtonsEnabled(true);

                        if (refreshRoots)
                            refreshRootsList();
                    });
                }
                catch (OperationCanceledException)
                {
                    notification.State = ProgressNotificationState.Cancelled;

                    Schedule(() =>
                    {
                        if (!IsDisposed)
                            setScanButtonsEnabled(true);
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, failureLogMessage);

                    notification.State = ProgressNotificationState.Cancelled;

                    Schedule(() =>
                    {
                        if (!IsDisposed)
                            setScanButtonsEnabled(true);
                    });
                }
            });
        }

        private void setScanButtonsEnabled(bool enabled)
        {
            scanExternalButton.Enabled.Value = enabled;

            if (scanInternalButton != null)
                scanInternalButton.Enabled.Value = enabled;
        }

        private static void updateProgressNotification(ProgressNotification notification, ExternalLibraryScanner.ScanProgress progress, LocalisableString initialText)
        {
            if (string.IsNullOrWhiteSpace(progress.CurrentRoot))
            {
                notification.Text = initialText;
                notification.Progress = 0;
                return;
            }

            string rootName = getDisplayName(progress.CurrentRoot);

            notification.Text = string.IsNullOrWhiteSpace(progress.CurrentDirectory)
                ? ExternalLibrarySettingsStrings.ScanningRootProgress(rootName, progress.RootIndex + 1, progress.TotalRoots, progress.TotalDirectories,
                    progress.ImportedSoFar, progress.SkippedSoFar, progress.ErrorsSoFar)
                : ExternalLibrarySettingsStrings.ScanningDirectoryProgress(rootName, progress.RootIndex + 1, progress.TotalRoots,
                    getDisplayName(progress.CurrentDirectory), progress.CurrentDirectoryIndex, progress.TotalDirectories,
                    progress.ImportedSoFar, progress.SkippedSoFar, progress.ErrorsSoFar);

            notification.Progress = progress.OverallProgress;
        }

        private static string getDisplayName(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return path;

            string trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string name = Path.GetFileName(trimmed);

            return string.IsNullOrEmpty(name) ? path : name;
        }

        private void refreshRootsList()
        {
            rootsList.Clear();

            if (libraryConfig == null) return;

            foreach (var root in libraryConfig.Roots)
            {
                rootsList.Add(new ExternalLibraryRootRow(root)
                {
                    OnRemove = removeRoot,
                });
            }
        }

        private partial class ExternalLibraryRootRow : CompositeDrawable
        {
            public Action<ExternalLibraryRoot>? OnRemove;
            private readonly ExternalLibraryRoot root;

            public ExternalLibraryRootRow(ExternalLibraryRoot root)
            {
                this.root = root;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                bool pathExists = Directory.Exists(root.Path);

                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding
                    {
                        Horizontal = SettingsPanel.CONTENT_MARGINS,
                        Vertical = 4,
                    },
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    RowDimensions = new[]
                    {
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            new SpriteIcon
                            {
                                Icon = root.Type == ExternalLibraryRootType.BMS ? FontAwesome.Solid.Music : FontAwesome.Solid.FileAudio,
                                Size = new Vector2(14),
                                Colour = pathExists ? colours.Green : colours.Red,
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Margin = new MarginPadding { Right = 10, Top = 2 },
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 2),
                                Children = new Drawable[]
                                {
                                    new OsuTextFlowContainer(text => text.Font = OsuFont.Default.With(size: 14))
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Text = root.Path,
                                        Colour = pathExists ? Colour4.White : colours.Red,
                                    },
                                    new OsuTextFlowContainer(text => text.Font = OsuFont.Default.With(size: 11))
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Text = buildRowStatusText(root, pathExists),
                                        Colour = colours.Yellow,
                                    },
                                }
                            },
                            new DangerousSettingsButtonV2
                            {
                                Text = ExternalLibrarySettingsStrings.Remove,
                                Width = 90,
                                Height = 40,
                                RelativeSizeAxes = Axes.None,
                                AutoSizeAxes = Axes.None,
                                Padding = new MarginPadding { Left = 12 },
                                Action = () => OnRemove?.Invoke(root),
                            },
                        }
                    }
                };
            }

            private static LocalisableString buildRowStatusText(ExternalLibraryRoot root, bool pathExists)
            {
                LocalisableString scanStatus = root.LastScanTime.HasValue
                    ? ExternalLibrarySettingsStrings.LastScan(root.LastScanTime.Value.LocalDateTime.ToString("g"))
                    : ExternalLibrarySettingsStrings.NeverScanned;

                if (!root.Enabled && !pathExists)
                    return LocalisableString.Interpolate($"[{root.Type}] {ExternalLibrarySettingsStrings.DisabledLabel} {ExternalLibrarySettingsStrings.PathNotFound} {scanStatus}");

                if (!root.Enabled)
                    return LocalisableString.Interpolate($"[{root.Type}] {ExternalLibrarySettingsStrings.DisabledLabel} {scanStatus}");

                if (!pathExists)
                    return LocalisableString.Interpolate($"[{root.Type}] {ExternalLibrarySettingsStrings.PathNotFound} {scanStatus}");

                return LocalisableString.Interpolate($"[{root.Type}] {scanStatus}");
            }
        }
    }
}
