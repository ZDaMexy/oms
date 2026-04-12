// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
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
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        private FillFlowContainer rootsList = null!;
        private SettingsButtonV2 scanAllButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (libraryConfig == null)
                return;

            AddRange(new Drawable[]
            {
                rootsList = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 4),
                },
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
                scanAllButton = new SettingsButtonV2
                {
                    Text = ExternalLibrarySettingsStrings.ScanAllLibraries,
                    Action = scanAll,
                },
            });

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

        private void scanAll()
        {
            if (libraryScanner == null) return;

            scanAllButton.Enabled.Value = false;

            var notification = new ProgressNotification
            {
                Text = ExternalLibrarySettingsStrings.ScanningExternalLibraries,
            };

            notificationOverlay?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    var result = await libraryScanner.ScanAllRoots(
                        new Progress<ExternalLibraryScanner.ScanProgress>(p =>
                        {
                            Schedule(() =>
                            {
                                notification.Text = ExternalLibrarySettingsStrings.ScanningProgress(Path.GetFileName(p.CurrentRoot), p.RootIndex + 1, p.TotalRoots);
                                notification.Progress = p.TotalRoots > 0 ? (float)(p.RootIndex + 1) / p.TotalRoots : 0;
                            });
                        }),
                        CancellationToken.None
                    ).ConfigureAwait(false);

                    Schedule(() =>
                    {
                        notification.CompletionText = ExternalLibrarySettingsStrings.ScanComplete(result.Imported, result.Skipped, result.Errors);
                        notification.State = ProgressNotificationState.Completed;
                        scanAllButton.Enabled.Value = true;
                        refreshRootsList();
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "External library scan failed.");
                    Schedule(() =>
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        scanAllButton.Enabled.Value = true;
                    });
                }
            });
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

        private partial class ExternalLibraryRootRow : FillFlowContainer
        {
            public Action<ExternalLibraryRoot>? OnRemove;
            private readonly ExternalLibraryRoot root;

            public ExternalLibraryRootRow(ExternalLibraryRoot root)
            {
                this.root = root;

                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Direction = FillDirection.Horizontal;
                Spacing = new Vector2(8, 0);
                Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS };
            }

            [BackgroundDependencyLoader]
            private void load(OsuColour colours)
            {
                bool pathExists = Directory.Exists(root.Path);

                AddRange(new Drawable[]
                {
                    new SpriteIcon
                    {
                        Icon = root.Type == ExternalLibraryRootType.BMS ? FontAwesome.Solid.Music : FontAwesome.Solid.FileAudio,
                        Size = new Vector2(14),
                        Colour = pathExists ? colours.Green : colours.Red,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Text = root.Path,
                                Font = OsuFont.Default.With(size: 14),
                                Colour = pathExists ? Colour4.White : colours.Red,
                            },
                            new OsuSpriteText
                            {
                                Text = buildRowStatusText(root, pathExists),
                                Font = OsuFont.Default.With(size: 11),
                                Colour = colours.Yellow,
                            },
                        }
                    },
                    new DangerousSettingsButtonV2
                    {
                        Text = ExternalLibrarySettingsStrings.Remove,
                        Width = 80,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        RelativeSizeAxes = Axes.None,
                        Padding = new MarginPadding(),
                        Action = () => OnRemove?.Invoke(root),
                    },
                });
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
