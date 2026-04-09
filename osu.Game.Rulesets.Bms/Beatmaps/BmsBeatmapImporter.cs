// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsBeatmapImporter : ICanAcceptFiles, IPostNotifications
    {
        private readonly BmsArchiveReader archiveReader = new BmsArchiveReader();
        private readonly BmsFolderImporter folderImporter;
        private Action<Notification>? postNotification;

        public IEnumerable<string> HandledExtensions => BmsImportExtensions.SupportedImportExtensions;

        public Action<Notification>? PostNotification
        {
            set
            {
                postNotification = value;
            }
        }

        public BmsBeatmapImporter(Storage storage, RealmAccess realm)
        {
            folderImporter = new BmsFolderImporter(storage, realm);
        }

        public Task Import(params string[] paths)
            => Import(paths.Select(path => new ImportTask(path)).ToArray());

        public async Task Import(ImportTask[] tasks, ImportParameters parameters = default)
        {
            foreach (var task in tasks)
            {
                var notification = new ProgressNotification
                {
                    Text = "Preparing BMS import...",
                    State = ProgressNotificationState.Active,
                };

                postNotification?.Invoke(notification);

                try
                {
                    using var preparedImport = archiveReader.Prepare(task);

                    var imported = new List<Live<BeatmapSetInfo>>();
                    var skippedBeatmapFiles = new List<string>();

                    for (int i = 0; i < preparedImport.FolderTasks.Count; i++)
                    {
                        notification.CancellationToken.ThrowIfCancellationRequested();
                        notification.Text = preparedImport.FolderTasks.Count == 1
                            ? "Importing BMS set..."
                            : $"Importing BMS sets ({i + 1} of {preparedImport.FolderTasks.Count})";
                        notification.Progress = (float)i / preparedImport.FolderTasks.Count;

                        try
                        {
                            var result = await folderImporter.Import(preparedImport.FolderTasks[i], parameters, notification.CancellationToken).ConfigureAwait(false);

                            if (result.ImportedBeatmapSet != null)
                                imported.Add(result.ImportedBeatmapSet);

                            skippedBeatmapFiles.AddRange(result.SkippedBeatmapFiles);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error(ex, $"Failed to import BMS folder {preparedImport.FolderTasks[i]}", LoggingTarget.Database);
                            skippedBeatmapFiles.AddRange(getBeatmapFilenames(preparedImport.FolderTasks[i]));
                        }

                        notification.Progress = (float)(i + 1) / preparedImport.FolderTasks.Count;
                    }

                    if (imported.Any() && preparedImport.DeleteSourceOnSuccess)
                        task.DeleteFile();

                    if (imported.Count == 0)
                    {
                        notification.Text = "BMS import failed! Check logs for more information.";
                        notification.State = ProgressNotificationState.Cancelled;

                        postNotification?.Invoke(new SimpleErrorNotification
                        {
                            Text = "Import failed: no valid BMS files found in archive."
                        });

                        continue;
                    }

                    notification.CompletionText = imported.Count < preparedImport.FolderTasks.Count
                        ? $"Imported {imported.Count} of {preparedImport.FolderTasks.Count} BMS sets."
                        : imported.Count == 1
                            ? "Imported 1 BMS set!"
                            : $"Imported {imported.Count} BMS sets!";
                    notification.State = ProgressNotificationState.Completed;

                    postSkippedFilesWarning(skippedBeatmapFiles);
                }
                catch (OperationCanceledException)
                {
                    notification.State = ProgressNotificationState.Cancelled;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to import BMS source {task}.", LoggingTarget.Database);
                    notification.State = ProgressNotificationState.Cancelled;
                    postNotification?.Invoke(new SimpleErrorNotification
                    {
                        Text = "Import failed: no valid BMS files found in archive."
                    });
                }
            }
        }

        private void postSkippedFilesWarning(IReadOnlyCollection<string> skippedBeatmapFiles)
        {
            if (skippedBeatmapFiles.Count == 0)
                return;

            string[] uniqueFiles = skippedBeatmapFiles.Where(path => !string.IsNullOrWhiteSpace(path))
                                                     .Select(path => Path.GetFileName(path))
                                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                                                     .ToArray();

            string preview = string.Join(", ", uniqueFiles.Take(5));

            if (uniqueFiles.Length > 5)
                preview += $", and {uniqueFiles.Length - 5} more";

            postNotification?.Invoke(new SimpleNotification
            {
                Icon = FontAwesome.Solid.ExclamationTriangle,
                Text = $"Imported with warnings. Skipped BMS files: {preview}"
            });
        }

        private static IEnumerable<string> getBeatmapFilenames(ImportTask task)
        {
            try
            {
                using var reader = task.GetReader();
                return reader.Filenames.Where(BmsImportExtensions.IsBeatmapFile).ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
