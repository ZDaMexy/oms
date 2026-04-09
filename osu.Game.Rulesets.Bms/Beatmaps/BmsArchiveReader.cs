// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Extensions;
using osu.Game.Database;
using osu.Game.IO.Archives;
using osu.Game.Utils;
using SharpCompress.Archives;
using SharpCompress.Readers;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsArchiveReader
    {
        public PreparedImport Prepare(ImportTask task)
        {
            if (task.Stream != null)
                return prepareStreamTask(task);

            if (Directory.Exists(task.Path))
                return createPreparedImport(task.Path);

            if (File.Exists(task.Path) && BmsImportExtensions.IsBeatmapFile(task.Path))
                return createPreparedImport(Path.GetDirectoryName(Path.GetFullPath(task.Path)) ?? throw new InvalidOperationException("Beatmap file has no containing directory."));

            if (File.Exists(task.Path) && BmsImportExtensions.IsArchiveFile(task.Path))
                return prepareArchiveStream(File.Open(task.Path, FileMode.Open, FileAccess.Read, FileShare.Read), deleteSourceOnSuccess: true, sourcePath: task.Path);

            throw new InvalidDataException($"Unsupported BMS import source: {task.Path}");
        }

        private PreparedImport prepareStreamTask(ImportTask task)
        {
            using var memoryStream = copyToMemoryStream(task.Stream!);
            task.Stream!.Dispose();

            if (BmsImportExtensions.IsBeatmapFile(task.Path))
            {
                string tempRoot = createTempRoot();
                string filename = Path.GetFileName(task.Path);
                string destinationPath = Path.Combine(tempRoot, filename);

                Directory.CreateDirectory(tempRoot);

                using (var outputStream = File.Create(destinationPath))
                {
                    memoryStream.Position = 0;
                    memoryStream.CopyTo(outputStream);
                }

                return createPreparedImport(tempRoot, cleanupPath: tempRoot);
            }

            return prepareArchiveStream(memoryStream, deleteSourceOnSuccess: false, sourcePath: task.Path);
        }

        private PreparedImport prepareArchiveStream(Stream stream, bool deleteSourceOnSuccess, string sourcePath)
        {
            string tempRoot = createTempRoot();

            Directory.CreateDirectory(tempRoot);

            try
            {
                extractArchive(stream, tempRoot);
                return createPreparedImport(tempRoot, cleanupPath: tempRoot, deleteSourceOnSuccess: deleteSourceOnSuccess);
            }
            catch
            {
                Directory.Delete(tempRoot, true);
                throw new InvalidDataException($"Failed to extract BMS archive {sourcePath}.");
            }
        }

        private static void extractArchive(Stream stream, string destinationRoot)
        {
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            using var archive = ArchiveFactory.Open(stream, new ReaderOptions
            {
                ArchiveEncoding = ZipArchiveReader.DEFAULT_ENCODING
            });

            foreach (var entry in archive.Entries.Where(e => !e.IsDirectory && !string.IsNullOrEmpty(e.Key)))
            {
                string relativePath = entry.Key!.ToStandardisedPath();

                if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(relativePath))
                    throw new InvalidDataException($"Archive entry '{relativePath}' is not allowed.");

                string destinationPath = Path.Combine(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
                string destinationDirectory = Path.GetDirectoryName(destinationPath) ?? destinationRoot;

                if (!FilesystemSanityCheckHelpers.IsSubDirectory(destinationRoot, destinationDirectory))
                    throw new InvalidDataException($"Archive entry '{relativePath}' escapes the extraction root.");

                Directory.CreateDirectory(destinationDirectory);

                using var inputStream = entry.OpenEntryStream();
                using var outputStream = File.Create(destinationPath);
                inputStream.CopyTo(outputStream);
            }
        }

        private static PreparedImport createPreparedImport(string rootPath, string? cleanupPath = null, bool deleteSourceOnSuccess = false)
        {
            var folderTasks = Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories)
                                     .Where(BmsImportExtensions.IsBeatmapFile)
                                     .Select(path => Path.GetDirectoryName(path) ?? rootPath)
                                     .Distinct(StringComparer.OrdinalIgnoreCase)
                                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                                     .Select(path => new ImportTask(path))
                                     .ToArray();

            return new PreparedImport(folderTasks, cleanupPath, deleteSourceOnSuccess);
        }

        private static MemoryStream copyToMemoryStream(Stream stream)
        {
            if (stream.CanSeek)
                stream.Seek(0, SeekOrigin.Begin);

            var output = new MemoryStream();
            stream.CopyTo(output);
            output.Position = 0;
            return output;
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
