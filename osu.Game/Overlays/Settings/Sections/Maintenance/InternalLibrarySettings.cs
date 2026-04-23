// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class InternalLibrarySettings : SettingsSubsection
    {
        protected override LocalisableString Header => ExternalLibrarySettingsStrings.InternalLibraryHeader;

        [Resolved(CanBeNull = true)]
        private ManagedLibraryScanner? managedLibraryScanner { get; set; }

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notificationOverlay { get; set; }

        private SettingsButtonV2 scanInternalRebuildButton = null!;
        private SettingsButtonV2 scanInternalIncrementalButton = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            if (managedLibraryScanner == null)
                return;

            AddRange(new Drawable[]
            {
                scanInternalRebuildButton = new SettingsButtonV2
                {
                    Text = ExternalLibrarySettingsStrings.ScanInternalLibrariesRebuild,
                    Action = () => scanInternalLibraries(ExternalLibraryScanner.ScanMode.Rebuild),
                },
                scanInternalIncrementalButton = new SettingsButtonV2
                {
                    Text = ExternalLibrarySettingsStrings.ScanInternalLibrariesIncremental,
                    Action = () => scanInternalLibraries(ExternalLibraryScanner.ScanMode.Incremental),
                },
            });
        }

        private void scanInternalLibraries(ExternalLibraryScanner.ScanMode mode)
        {
            if (managedLibraryScanner == null)
                return;

            setScanButtonsEnabled(false);

            var notification = new ProgressNotification
            {
                Text = mode == ExternalLibraryScanner.ScanMode.Rebuild
                    ? ExternalLibrarySettingsStrings.ScanningInternalLibrariesRebuild
                    : ExternalLibrarySettingsStrings.ScanningInternalLibrariesIncremental,
                Progress = 0,
                State = ProgressNotificationState.Active,
            };

            notificationOverlay?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    var result = await managedLibraryScanner.ScanAllRoots(
                        mode,
                        new Progress<ExternalLibraryScanner.ScanProgress>(progress => updateProgressNotification(notification, progress,
                            mode == ExternalLibraryScanner.ScanMode.Rebuild
                                ? ExternalLibrarySettingsStrings.ScanningInternalLibrariesRebuild
                                : ExternalLibrarySettingsStrings.ScanningInternalLibrariesIncremental)),
                        notification.CancellationToken).ConfigureAwait(false);

                    notification.CompletionText = ExternalLibrarySettingsStrings.ScanComplete(result.Imported, result.Skipped, result.Errors);
                    notification.State = ProgressNotificationState.Completed;

                    Schedule(() =>
                    {
                        if (!IsDisposed)
                            setScanButtonsEnabled(true);
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
                    Logger.Error(ex, "Managed library scan failed.");
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
            scanInternalRebuildButton.Enabled.Value = enabled;
            scanInternalIncrementalButton.Enabled.Value = enabled;
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
    }
}
