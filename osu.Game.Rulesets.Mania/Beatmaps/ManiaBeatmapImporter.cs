// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Rulesets.Mania.Beatmaps
{
    /// <summary>
    /// Accepts mania beatmap directories for filesystem-backed import into <c>chartmania/</c>.
    /// Wraps <see cref="ManiaFolderImporter"/> and implements <see cref="ICanAcceptFiles"/>
    /// so it can be registered as a drag-and-drop import handler.
    /// </summary>
    public class ManiaBeatmapImporter : ICanAcceptFiles, IPostNotifications
    {
        private readonly ManiaFolderImporter folderImporter;
        private Action<Notification>? postNotification;

        /// <summary>
        /// This importer handles mania directories containing .osu files.
        /// It does NOT handle .osz archives — those go through the standard BeatmapImporter.
        /// </summary>
        public IEnumerable<string> HandledExtensions => Array.Empty<string>();

        public Action<Notification>? PostNotification
        {
            set => postNotification = value;
        }

        public ManiaBeatmapImporter(Storage storage, RealmAccess realm)
        {
            folderImporter = new ManiaFolderImporter(storage, realm);
        }

        /// <summary>
        /// Gets the underlying folder importer for direct use by the external library scanner.
        /// </summary>
        public ManiaFolderImporter FolderImporter => folderImporter;

        public Task Import(params string[] paths)
            => Import(paths.Select(path => new ImportTask(path)).ToArray());

        public Task RegisterExternalDirectory(string path, CancellationToken cancellationToken = default)
            => folderImporter.RegisterExternalDirectory(path, cancellationToken);

        public Task RegisterManagedDirectory(string path, CancellationToken cancellationToken = default)
            => folderImporter.RegisterManagedDirectory(path, cancellationToken);

        public bool ShouldImportExternalDirectory(string path)
            => folderImporter.ShouldImportExternalDirectory(path);

        public bool ShouldImportManagedDirectory(string path)
            => folderImporter.ShouldImportManagedDirectory(path);

        public async Task Import(ImportTask[] tasks, ImportParameters parameters = default)
        {
            foreach (var task in tasks)
            {
                // Only accept directories.
                if (!Directory.Exists(task.Path))
                    continue;

                var notification = new ProgressNotification
                {
                    Text = "Preparing mania import...",
                    State = ProgressNotificationState.Active,
                };

                postNotification?.Invoke(notification);

                try
                {
                    notification.Text = "Importing mania beatmap set...";
                    notification.Progress = 0;

                    var result = await folderImporter.Import(task, parameters, notification.CancellationToken).ConfigureAwait(false);

                    if (result.ImportedBeatmapSet == null)
                    {
                        notification.Text = "Mania import failed! No valid .osu files found.";
                        notification.State = ProgressNotificationState.Cancelled;
                        continue;
                    }

                    notification.CompletionText = "Imported 1 mania beatmap set!";
                    notification.State = ProgressNotificationState.Completed;

                    postSkippedFilesWarning(result.SkippedBeatmapFiles);
                }
                catch (OperationCanceledException)
                {
                    notification.State = ProgressNotificationState.Cancelled;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to import mania source {task}.", LoggingTarget.Database);
                    notification.State = ProgressNotificationState.Cancelled;

                    postNotification?.Invoke(new SimpleErrorNotification
                    {
                        Text = $"Import failed: {ex.Message}"
                    });
                }
            }
        }

        private void postSkippedFilesWarning(IReadOnlyList<string> skippedFiles)
        {
            if (skippedFiles.Count == 0)
                return;

            string[] unique = skippedFiles.Where(f => !string.IsNullOrWhiteSpace(f))
                                          .Select(Path.GetFileName)
                                          .Distinct(StringComparer.OrdinalIgnoreCase)
                                          .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                          .ToArray()!;

            string preview = string.Join(", ", unique.Take(5));

            if (unique.Length > 5)
                preview += $", and {unique.Length - 5} more";

            postNotification?.Invoke(new SimpleNotification
            {
                Icon = FontAwesome.Solid.ExclamationTriangle,
                Text = $"Imported with warnings. Skipped .osu files: {preview}"
            });
        }
    }
}
