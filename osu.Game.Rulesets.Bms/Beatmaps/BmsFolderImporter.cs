// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO.Archives;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsFolderImporter
    {
        public const string SONGS_STORAGE_PATH = "chartbms";

        private readonly Storage storage;
        private readonly Storage songsStorage;
        private readonly RealmAccess realmAccess;
        private readonly Lazy<BmsTableMd5Index> tableMd5Index;

        public BmsDifficultyTableManager DifficultyTableManager => TableMd5Index.TableManager;

        public BmsTableMd5Index TableMd5Index => tableMd5Index.Value;

        public BmsFolderImporter(Storage storage, RealmAccess realm, BmsTableMd5Index? tableMd5Index = null)
        {
            this.storage = storage;
            songsStorage = storage.GetStorageForDirectory(SONGS_STORAGE_PATH);
            realmAccess = realm;
            this.tableMd5Index = new Lazy<BmsTableMd5Index>(() => tableMd5Index ?? new BmsTableMd5Index(BmsDifficultyTableManager.GetShared(storage), realm));
        }

        public Task<FolderImportResult> Import(ImportTask task, ImportParameters parameters = default, CancellationToken cancellationToken = default)
            => Task.Run(() => import(task, cancellationToken), cancellationToken);

        public Task<FolderImportResult> RegisterExternalDirectory(string path, CancellationToken cancellationToken = default)
            => Task.Run(() => registerExternalDirectory(path, cancellationToken), cancellationToken);

        public Task<FolderImportResult> RegisterManagedDirectory(string path, CancellationToken cancellationToken = default)
            => Task.Run(() => registerManagedDirectory(path, cancellationToken), cancellationToken);

        private FolderImportResult import(ImportTask task, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using ArchiveReader reader = task.GetReader();

            if (reader is not DirectoryArchiveReader directoryReader)
                throw new InvalidOperationException("BMS folder import requires a directory source.");

            var preparedBeatmapSet = createBeatmapSet(directoryReader, cancellationToken);

            if (preparedBeatmapSet.BeatmapSet == null)
                return new FolderImportResult(null, preparedBeatmapSet.SkippedBeatmapFiles);

            var existing = tryReuseImportedCopy(preparedBeatmapSet.BeatmapSet.Hash);

            if (existing != null)
                return new FolderImportResult(existing, preparedBeatmapSet.SkippedBeatmapFiles);

            string destinationPath = allocateDestinationPath(directoryReader.Name, preparedBeatmapSet.BeatmapSet.Hash);
            preparedBeatmapSet.BeatmapSet.FilesystemStoragePath = destinationPath;

            try
            {
                copyDirectory(directoryReader, destinationPath, cancellationToken);
                return new FolderImportResult(importIntoRealm(preparedBeatmapSet.BeatmapSet, cancellationToken), preparedBeatmapSet.SkippedBeatmapFiles);
            }
            catch
            {
                if (storage.ExistsDirectory(destinationPath))
                    storage.DeleteDirectory(destinationPath);

                throw;
            }
        }

        private FolderImportResult registerExternalDirectory(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var reader = new ImportTask(path).GetReader();

            if (reader is not DirectoryArchiveReader directoryReader)
                throw new InvalidOperationException("BMS external registration requires a directory source.");

            var preparedBeatmapSet = createBeatmapSet(directoryReader, cancellationToken);

            if (preparedBeatmapSet.BeatmapSet == null)
                return new FolderImportResult(null, preparedBeatmapSet.SkippedBeatmapFiles);

            string sourcePath = normaliseExternalPath(directoryReader.GetFullPath(string.Empty));
            var existing = tryReuseExternal(preparedBeatmapSet.BeatmapSet.Hash, sourcePath);

            if (existing != null)
                return new FolderImportResult(existing, preparedBeatmapSet.SkippedBeatmapFiles);

            preparedBeatmapSet.BeatmapSet.FilesystemStoragePath = sourcePath;
            preparedBeatmapSet.BeatmapSet.IsExternalFilesystemStorage = true;

            return new FolderImportResult(importIntoRealm(preparedBeatmapSet.BeatmapSet, cancellationToken), preparedBeatmapSet.SkippedBeatmapFiles);
        }

        private FolderImportResult registerManagedDirectory(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var reader = new ImportTask(path).GetReader();

            if (reader is not DirectoryArchiveReader directoryReader)
                throw new InvalidOperationException("BMS managed registration requires a directory source.");

            var preparedBeatmapSet = createBeatmapSet(directoryReader, cancellationToken);

            if (preparedBeatmapSet.BeatmapSet == null)
                return new FolderImportResult(null, preparedBeatmapSet.SkippedBeatmapFiles);

            string relativePath = getManagedRelativePath(directoryReader.GetFullPath(string.Empty));
            var existing = tryReuseManaged(preparedBeatmapSet.BeatmapSet.Hash, relativePath);

            if (existing != null)
                return new FolderImportResult(existing, preparedBeatmapSet.SkippedBeatmapFiles);

            preparedBeatmapSet.BeatmapSet.FilesystemStoragePath = relativePath;
            preparedBeatmapSet.BeatmapSet.IsExternalFilesystemStorage = false;

            return new FolderImportResult(importIntoRealm(preparedBeatmapSet.BeatmapSet, cancellationToken), preparedBeatmapSet.SkippedBeatmapFiles);
        }

        private Live<BeatmapSetInfo>? tryReuseImportedCopy(string hash)
            => tryReuseExisting(hash, existing => !existing.IsExternalFilesystemStorage);

        private Live<BeatmapSetInfo>? tryReuseExternal(string hash, string externalPath)
            => tryReuseExisting(hash, existing => existing.IsExternalFilesystemStorage
                                                 && string.Equals(normaliseExternalPath(existing.FilesystemStoragePath), externalPath, StringComparison.OrdinalIgnoreCase));

        private Live<BeatmapSetInfo>? tryReuseManaged(string hash, string managedPath)
            => tryReuseExisting(hash, existing => !existing.IsExternalFilesystemStorage
                                                 && string.Equals(existing.FilesystemStoragePath?.ToStandardisedPath(), managedPath, StringComparison.OrdinalIgnoreCase));

        private Live<BeatmapSetInfo>? tryReuseExisting(string hash, Func<BeatmapSetInfo, bool> canReuse)
        {
            BeatmapSetInfo? existing = realmAccess.Run(realm => realm.All<BeatmapSetInfo>()
                                                                  .OrderBy(set => set.DeletePending)
                                                                  .FirstOrDefault(set => set.Hash == hash)
                                                                  ?.Detach());

            if (existing == null || string.IsNullOrEmpty(existing.FilesystemStoragePath))
                return null;

            bool pathExists = existing.IsExternalFilesystemStorage
                ? Directory.Exists(existing.FilesystemStoragePath)
                : storage.ExistsDirectory(existing.FilesystemStoragePath);

            if (!pathExists || !canReuse(existing))
                return null;

            return realmAccess.Run(realm =>
            {
                var managedExisting = realm.Find<BeatmapSetInfo>(existing.ID);

                if (managedExisting == null)
                    return null;

                using var transaction = realm.BeginWrite();
                managedExisting.DeletePending = false;
                transaction.Commit();

                return managedExisting.ToLive(realmAccess);
            });
        }

        private static string normaliseExternalPath(string path) => Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private string getManagedRelativePath(string path)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string managedRoot = songsStorage.GetFullPath(string.Empty);

            if (!FilesystemSanityCheckHelpers.IsSubDirectory(managedRoot, fullPath))
                throw new InvalidOperationException($"BMS managed registration requires a directory under '{managedRoot}'.");

            string relativePath = Path.GetRelativePath(storage.GetFullPath(string.Empty), fullPath).ToStandardisedPath();

            if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(relativePath))
                throw new InvalidOperationException($"Managed storage path '{relativePath}' is not allowed.");

            return relativePath;
        }

        private Live<BeatmapSetInfo> importIntoRealm(BeatmapSetInfo beatmapSet, CancellationToken cancellationToken)
            => realmAccess.Run(realm =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var transaction = realm.BeginWrite();

                var ruleset = realm.All<RulesetInfo>().FirstOrDefault(r => r.ShortName == BmsRuleset.SHORT_NAME);

                if (ruleset?.Available != true)
                    throw new InvalidOperationException($"Unable to import BMS beatmaps because ruleset '{BmsRuleset.SHORT_NAME}' is not available locally.");

                var existing = realm.All<BeatmapSetInfo>().OrderBy(set => set.DeletePending).FirstOrDefault(set => set.Hash == beatmapSet.Hash);

                if (existing != null)
                    existing.DeletePending = true;

                foreach (var beatmap in beatmapSet.Beatmaps)
                {
                    beatmap.BeatmapSet = beatmapSet;
                    beatmap.Ruleset = ruleset;
                }

                realm.Add(beatmapSet);
                transaction.Commit();

                return beatmapSet.ToLive(realmAccess);
            });

        private PreparedBeatmapSet createBeatmapSet(DirectoryArchiveReader reader, CancellationToken cancellationToken)
        {
            var beatmaps = new List<BeatmapInfo>();
            var skippedBeatmapFiles = new List<string>();
            var allKeysoundFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? firstPreviewFile = null;
            var beatmapFiles = reader.Filenames.Where(isTopLevelBeatmapFile)
                                      .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                                      .ToArray();

            if (beatmapFiles.Length == 0)
            {
                Logger.Log($"No BMS beatmap files found in the import source ({reader.Name}).", LoggingTarget.Database);
                return new PreparedBeatmapSet(null, Array.Empty<string>());
            }

            var beatmapSet = new BeatmapSetInfo
            {
                OnlineID = -1,
                DateAdded = getDateAdded(reader, beatmapFiles),
            };

            using var hashableBeatmaps = new MemoryStream();

            foreach (string beatmapFile in beatmapFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var memoryStream = new MemoryStream();
                using (var sourceStream = reader.GetStream(beatmapFile))
                    sourceStream.CopyTo(memoryStream);

                try
                {
                    memoryStream.Position = 0;
                    var loadedBeatmap = BmsImportedBeatmapFactory.Create(memoryStream, beatmapFile);
                    var loadedInfo = loadedBeatmap.BeatmapInfo;

                    foreach (string keysoundFile in loadedBeatmap.DecodedChart.BeatmapInfo.KeysoundTable.Values)
                    {
                        if (Audio.BmsKeysoundSampleInfo.TryNormaliseFilename(keysoundFile, out string? normalised))
                        {
                            allKeysoundFiles.Add(normalised);

                            // BMS charts commonly reference keysound filenames with an extension that differs from the
                            // actual file on disk (e.g. #WAV01 bgm.wav but the file is bgm.ogg). The audio store
                            // resolves these at lookup time by trying alternate extensions, but the keysound exclusion
                            // set must also cover all variants to prevent a cross-extension keysound from being
                            // mistakenly detected as a full music file.
                            string baseName = Path.ChangeExtension(normalised, null)?.ToStandardisedPath() ?? normalised;
                            allKeysoundFiles.Add(baseName);
                            allKeysoundFiles.Add(baseName + ".wav");
                            allKeysoundFiles.Add(baseName + ".ogg");
                            allKeysoundFiles.Add(baseName + ".mp3");
                            allKeysoundFiles.Add(baseName + ".flac");
                        }
                    }

                    if (firstPreviewFile == null
                        && !string.IsNullOrWhiteSpace(loadedBeatmap.DecodedChart.BeatmapInfo.PreviewFile)
                        && Audio.BmsKeysoundSampleInfo.TryNormaliseFilename(loadedBeatmap.DecodedChart.BeatmapInfo.PreviewFile, out string? normalisedPreview))
                    {
                        firstPreviewFile = normalisedPreview;
                    }

                    memoryStream.Position = 0;
                    string hash = memoryStream.ComputeSHA2Hash();

                    if (beatmaps.Any(b => b.Hash == hash))
                    {
                        Logger.Log($"Skipping import of {beatmapFile} due to duplicate file content.", LoggingTarget.Database);
                        skippedBeatmapFiles.Add(beatmapFile);
                        continue;
                    }

                    memoryStream.Position = 0;
                    string md5Hash = memoryStream.ComputeMD5Hash();

                    memoryStream.Position = 0;
                    memoryStream.CopyTo(hashableBeatmaps);

                    var beatmapInfo = new BeatmapInfo(loadedInfo.Ruleset, new BeatmapDifficulty(loadedInfo.Difficulty), loadedInfo.Metadata.DeepClone())
                    {
                        Hash = hash,
                        LocalFilePath = beatmapFile.ToStandardisedPath(),
                        StarRating = loadedInfo.StarRating,
                        DifficultyName = loadedInfo.DifficultyName,
                        BeatDivisor = loadedInfo.BeatDivisor,
                        MD5Hash = md5Hash,
                        Length = loadedInfo.Length,
                        BPM = loadedInfo.BPM,
                        EndTimeObjectCount = loadedInfo.EndTimeObjectCount,
                        TotalObjectCount = loadedInfo.TotalObjectCount,
                    };

                    TableMd5Index.ApplyTo(beatmapInfo);
                    beatmaps.Add(beatmapInfo);
                }
                catch (Exception ex)
                {
                    Logger.Log($"Skipping invalid BMS file {beatmapFile}: {ex.Message}", LoggingTarget.Database, LogLevel.Verbose);
                    skippedBeatmapFiles.Add(beatmapFile);
                }
            }

            if (!beatmaps.Any())
                return new PreparedBeatmapSet(null, skippedBeatmapFiles);

            string? detectedAudioFile = detectFullMusicFile(reader, allKeysoundFiles)
                                        ?? resolvePreviewFile(reader, firstPreviewFile);

            if (detectedAudioFile != null)
            {
                foreach (var beatmap in beatmaps)
                    beatmap.Metadata.AudioFile = detectedAudioFile;
            }

            hashableBeatmaps.Position = 0;
            beatmapSet.Hash = hashableBeatmaps.Length > 0 ? hashableBeatmaps.ComputeSHA2Hash() : reader.Name.ComputeSHA2Hash();

            foreach (var beatmap in beatmaps)
            {
                beatmap.BeatmapSet = beatmapSet;
                beatmapSet.Beatmaps.Add(beatmap);
            }

            return new PreparedBeatmapSet(beatmapSet, skippedBeatmapFiles);
        }

        private void copyDirectory(DirectoryArchiveReader reader, string destinationPath, CancellationToken cancellationToken)
        {
            string sourceRoot = reader.GetFullPath(string.Empty);
            Storage destinationStorage = storage.GetStorageForDirectory(destinationPath);

            foreach (string sourceFile in Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                string relativePath = Path.GetRelativePath(sourceRoot, sourceFile).ToStandardisedPath();

                if (FilesystemSanityCheckHelpers.IncursPathTraversalRisk(relativePath))
                    throw new InvalidOperationException($"Filename '{relativePath}' is not allowed.");

                using var inputStream = File.OpenRead(sourceFile);
                using var outputStream = destinationStorage.CreateFileSafely(relativePath);
                inputStream.CopyTo(outputStream);
            }
        }

        private string allocateDestinationPath(string sourceName, string hash)
        {
            string safeName = sourceName.GetValidFilename();

            if (string.IsNullOrWhiteSpace(safeName))
                safeName = "bms";

            string baseDirectoryName = $"{safeName}-{hash[..8]}";
            string directoryName = baseDirectoryName;
            int suffix = 2;

            while (songsStorage.ExistsDirectory(directoryName))
                directoryName = $"{baseDirectoryName}-{suffix++}";

            return Path.Combine(SONGS_STORAGE_PATH, directoryName).ToStandardisedPath();
        }

        private static DateTimeOffset getDateAdded(DirectoryArchiveReader reader, IEnumerable<string> beatmapFiles)
        {
            var dateAdded = DateTimeOffset.UtcNow;

            foreach (string beatmapFile in beatmapFiles)
            {
                var currentDateAdded = File.GetLastWriteTimeUtc(reader.GetFullPath(beatmapFile));

                if (currentDateAdded < dateAdded)
                    dateAdded = currentDateAdded;
            }

            return dateAdded;
        }

        private static bool isTopLevelBeatmapFile(string filename)
            => !filename.ToStandardisedPath().Contains('/') && BmsImportExtensions.IsBeatmapFile(filename);

        /// <summary>
        /// Scans the BMS directory for an audio file that is NOT referenced by any keysound table entry
        /// and is large enough to be a full music track (≥ 1 MB). Returns the largest such file, or null.
        /// </summary>
        private static string? detectFullMusicFile(DirectoryArchiveReader reader, HashSet<string> keysoundFiles)
        {
            string[] audioExtensions = { ".mp3", ".ogg", ".flac", ".wav" };
            const long minimum_music_file_size = 1_000_000; // 1 MB

            string? bestCandidate = null;
            long bestSize = 0;

            foreach (string filename in reader.Filenames)
            {
                string ext = Path.GetExtension(filename);

                if (string.IsNullOrEmpty(ext) || !audioExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                    continue;

                string standardised = filename.ToStandardisedPath();

                if (keysoundFiles.Contains(standardised))
                    continue;

                try
                {
                    long fileSize = new FileInfo(reader.GetFullPath(filename)).Length;

                    if (fileSize >= minimum_music_file_size && fileSize > bestSize)
                    {
                        bestSize = fileSize;
                        bestCandidate = standardised;
                    }
                }
                catch
                {
                    // Ignore files we can't stat.
                }
            }

            return bestCandidate;
        }

        /// <summary>
        /// Resolves a #PREVIEW filename from a BMS header against the actual files in the archive.
        /// Returns the standardised path if an audio file matching the preview filename (with or without extension
        /// substitution) exists in the archive, or null otherwise.
        /// </summary>
        private static string? resolvePreviewFile(DirectoryArchiveReader reader, string? previewFilename)
        {
            if (string.IsNullOrWhiteSpace(previewFilename))
                return null;

            string[] audioExtensions = { ".mp3", ".ogg", ".flac", ".wav" };
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { previewFilename };

            string baseName = Path.ChangeExtension(previewFilename, null)?.ToStandardisedPath() ?? previewFilename;

            foreach (string ext in audioExtensions)
                candidates.Add(baseName + ext);

            foreach (string filename in reader.Filenames)
            {
                string standardised = filename.ToStandardisedPath();

                if (candidates.Contains(standardised))
                    return standardised;
            }

            return null;
        }

        public sealed class FolderImportResult
        {
            public Live<BeatmapSetInfo>? ImportedBeatmapSet { get; }

            public IReadOnlyList<string> SkippedBeatmapFiles { get; }

            public FolderImportResult(Live<BeatmapSetInfo>? importedBeatmapSet, IReadOnlyList<string> skippedBeatmapFiles)
            {
                ImportedBeatmapSet = importedBeatmapSet;
                SkippedBeatmapFiles = skippedBeatmapFiles;
            }
        }

        private sealed class PreparedBeatmapSet
        {
            public BeatmapSetInfo? BeatmapSet { get; }

            public IReadOnlyList<string> SkippedBeatmapFiles { get; }

            public PreparedBeatmapSet(BeatmapSetInfo? beatmapSet, IReadOnlyList<string> skippedBeatmapFiles)
            {
                BeatmapSet = beatmapSet;
                SkippedBeatmapFiles = skippedBeatmapFiles;
            }
        }
    }
}
