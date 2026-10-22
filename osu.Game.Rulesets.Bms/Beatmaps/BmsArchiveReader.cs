// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Game.Database;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsArchiveReader
    {
        public PreparedImport Prepare(ImportTask task, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (task.Stream != null)
                return prepareStreamTask(task, cancellationToken);

            if (Directory.Exists(task.Path))
                return createPreparedImport(task.Path, cancellationToken);

            if (File.Exists(task.Path) && BmsImportExtensions.IsBeatmapFile(task.Path))
                return createPreparedImport(Path.GetDirectoryName(Path.GetFullPath(task.Path)) ?? throw new InvalidOperationException("Beatmap file has no containing directory."), cancellationToken);

            if (File.Exists(task.Path) && BmsImportExtensions.IsArchiveFile(task.Path))
            {
                using var source = File.Open(task.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return prepareArchiveStream(source, true, cancellationToken);
            }

            throw new InvalidDataException($"Unsupported BMS import source: {task.Path}");
        }

        private PreparedImport prepareStreamTask(ImportTask task, CancellationToken cancellationToken)
        {
            using Stream source = task.Stream!;

            if (source.CanSeek)
                source.Position = 0;

            if (BmsImportExtensions.IsBeatmapFile(task.Path))
            {
                string tempRoot = createTempRoot();
                Directory.CreateDirectory(tempRoot);

                try
                {
                    using (var output = new FileStream(Path.Combine(tempRoot, Path.GetFileName(task.Path)), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        copyBounded(source, output, BmsDownloadImporter.MAX_CHART_BYTES, cancellationToken);

                    return createPreparedImport(tempRoot, cancellationToken, cleanupPath: tempRoot);
                }
                catch
                {
                    Directory.Delete(tempRoot, true);
                    throw;
                }
            }

            if (source.CanSeek)
                return prepareArchiveStream(source, false, cancellationToken);

            // Archive readers require seeking. Spool non-seekable input to an owned, bounded file rather
            // than allocating up to the archive limit on the managed heap.
            string stagingRoot = createTempRoot();
            Directory.CreateDirectory(stagingRoot);

            try
            {
                using var buffered = new FileStream(Path.Combine(stagingRoot, "source.archive"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                copyBounded(source, buffered, BmsDownloadImporter.MAX_COMPRESSED_BYTES, cancellationToken);
                buffered.Position = 0;
                return prepareArchiveStream(buffered, false, cancellationToken);
            }
            finally
            {
                Directory.Delete(stagingRoot, true);
            }
        }

        private PreparedImport prepareArchiveStream(Stream stream, bool deleteSourceOnSuccess, CancellationToken cancellationToken)
        {
            string tempRoot = createTempRoot();

            Directory.CreateDirectory(tempRoot);

            try
            {
                BmsDownloadImporter.ExtractArchive(stream, tempRoot, cancellationToken);
                return createPreparedImport(tempRoot, cancellationToken, cleanupPath: tempRoot, deleteSourceOnSuccess: deleteSourceOnSuccess);
            }
            catch
            {
                Directory.Delete(tempRoot, true);
                throw;
            }
        }

        private static void copyBounded(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
        {
            if (source.CanSeek && source.Length - source.Position > limit)
                throw new InvalidDataException("The BMS import source exceeds its byte limit.");

            byte[] buffer = new byte[128 * 1024];
            long copied = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = source.Read(buffer, 0, buffer.Length);

                if (read == 0)
                    return;

                if (read > limit - copied)
                    throw new InvalidDataException("The BMS import source exceeds its byte limit.");

                destination.Write(buffer, 0, read);
                copied += read;
            }
        }

        private static PreparedImport createPreparedImport(string rootPath, CancellationToken cancellationToken, string? cleanupPath = null, bool deleteSourceOnSuccess = false)
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string path in Directory.EnumerateFiles(rootPath, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (BmsImportExtensions.IsBeatmapFile(path))
                    folders.Add(Path.GetDirectoryName(path) ?? rootPath);
            }

            cancellationToken.ThrowIfCancellationRequested();
            var folderTasks = folders.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).Select(path => new ImportTask(path)).ToArray();
            return new PreparedImport(folderTasks, cleanupPath, deleteSourceOnSuccess);
        }

        private static string createTempRoot()
            => Path.Combine(Path.GetTempPath(), "oms-bms-import", Guid.NewGuid().ToString("N"));

        public sealed class PreparedImport : IDisposable
        {
            public IReadOnlyList<ImportTask> FolderTasks { get; }

            public string? CleanupPath { get; }

            public bool DeleteSourceOnSuccess { get; }

            public PreparedImport(IReadOnlyList<ImportTask> folderTasks, string? cleanupPath, bool deleteSourceOnSuccess)
            {
                FolderTasks = folderTasks;
                CleanupPath = cleanupPath;
                DeleteSourceOnSuccess = deleteSourceOnSuccess;
            }

            public void Dispose()
            {
                if (CleanupPath == null || !Directory.Exists(CleanupPath))
                    return;

                Directory.Delete(CleanupPath, true);
            }
        }
    }
}
