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
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Rulesets.Mania.Beatmaps
{
    /// <summary>
    /// Imports mania beatmap directories into filesystem-backed storage under <c>chartmania/</c>,
    /// analogous to <c>BmsFolderImporter</c> which uses <c>chartbms/</c>.
    /// </summary>
    public class ManiaFolderImporter
    {
        public const string MANIA_STORAGE_PATH = "chartmania";

        private readonly Storage storage;
        private readonly Storage maniaStorage;
        private readonly RealmAccess realmAccess;

        public ManiaFolderImporter(Storage storage, RealmAccess realm)
        {
            this.storage = storage;
            maniaStorage = storage.GetStorageForDirectory(MANIA_STORAGE_PATH);
            realmAccess = realm;
        }

        public Task<FolderImportResult> Import(ImportTask task, ImportParameters parameters = default, CancellationToken cancellationToken = default)
            => Task.Run(() => import(task, cancellationToken), cancellationToken);

        private FolderImportResult import(ImportTask task, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using ArchiveReader reader = task.GetReader();

            if (reader is not DirectoryArchiveReader directoryReader)
                throw new InvalidOperationException("Mania folder import requires a directory source.");

            var prepared = createBeatmapSet(directoryReader, cancellationToken);

            if (prepared.BeatmapSet == null)
                return new FolderImportResult(null, prepared.SkippedBeatmapFiles);

            var existing = tryReuseExisting(prepared.BeatmapSet.Hash);

            if (existing != null)
                return new FolderImportResult(existing, prepared.SkippedBeatmapFiles);

            string destinationPath = allocateDestinationPath(directoryReader.Name, prepared.BeatmapSet.Hash);
            prepared.BeatmapSet.FilesystemStoragePath = destinationPath;

            try
            {
                copyDirectory(directoryReader, destinationPath, cancellationToken);
                return new FolderImportResult(importIntoRealm(prepared.BeatmapSet, cancellationToken), prepared.SkippedBeatmapFiles);
            }
            catch
            {
                if (storage.ExistsDirectory(destinationPath))
                    storage.DeleteDirectory(destinationPath);

                throw;
            }
        }

        private Live<BeatmapSetInfo>? tryReuseExisting(string hash)
        {
            BeatmapSetInfo? existing = realmAccess.Run(realm => realm.All<BeatmapSetInfo>()
                                                                  .OrderBy(set => set.DeletePending)
                                                                  .FirstOrDefault(set => set.Hash == hash)
                                                                  ?.Detach());

            if (existing == null || string.IsNullOrEmpty(existing.FilesystemStoragePath) || !storage.ExistsDirectory(existing.FilesystemStoragePath))
                return null;

            return realmAccess.Run(realm =>
            {
                var managed = realm.Find<BeatmapSetInfo>(existing.ID);

                if (managed == null)
                    return null;

                using var transaction = realm.BeginWrite();
                managed.DeletePending = false;
                transaction.Commit();

                return managed.ToLive(realmAccess);
            });
        }

        private Live<BeatmapSetInfo> importIntoRealm(BeatmapSetInfo beatmapSet, CancellationToken cancellationToken)
            => realmAccess.Run(realm =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var transaction = realm.BeginWrite();

                var existing = realm.All<BeatmapSetInfo>().OrderBy(set => set.DeletePending).FirstOrDefault(set => set.Hash == beatmapSet.Hash);

                if (existing != null)
                    existing.DeletePending = true;

                foreach (var beatmap in beatmapSet.Beatmaps)
                {
                    beatmap.BeatmapSet = beatmapSet;

                    // Resolve the managed RulesetInfo from Realm by OnlineID.
                    var ruleset = realm.All<RulesetInfo>().FirstOrDefault(r => r.OnlineID == beatmap.Ruleset.OnlineID);

                    if (ruleset?.Available != true)
                    {
                        Logger.Log($"Skipping mania beatmap {beatmap.LocalFilePath}: ruleset {beatmap.Ruleset.OnlineID} not available.", LoggingTarget.Database);
                        continue;
                    }

                    beatmap.Ruleset = ruleset;
                }

                realm.Add(beatmapSet);
                transaction.Commit();

                return beatmapSet.ToLive(realmAccess);
            });

        private PreparedBeatmapSet createBeatmapSet(DirectoryArchiveReader reader, CancellationToken cancellationToken)
        {
            var beatmaps = new List<BeatmapInfo>();
            var skippedFiles = new List<string>();

            var osuFiles = reader.Filenames
                                 .Where(f => !f.ToStandardisedPath().Contains('/') && f.EndsWith(@".osu", StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                                 .ToArray();

            if (osuFiles.Length == 0)
            {
                Logger.Log($"No .osu files found in mania import source ({reader.Name}).", LoggingTarget.Database);
                return new PreparedBeatmapSet(null, Array.Empty<string>());
            }

            var beatmapSet = new BeatmapSetInfo
            {
                OnlineID = -1,
                DateAdded = getDateAdded(reader, osuFiles),
            };

            using var hashableContent = new MemoryStream();

            foreach (string osuFile in osuFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                using var memoryStream = new MemoryStream();

                using (var sourceStream = reader.GetStream(osuFile))
                    sourceStream.CopyTo(memoryStream);

                try
                {
                    memoryStream.Position = 0;

                    IBeatmap decoded;

                    using (var lineReader = new LineBufferedReader(memoryStream, true))
                    {
                        if (lineReader.PeekLine() == null)
                        {
                            Logger.Log($"No content found in {osuFile}.", LoggingTarget.Database);
                            skippedFiles.Add(osuFile);
                            continue;
                        }

                        decoded = Decoder.GetDecoder<Beatmap>(lineReader).Decode(lineReader);
                    }

                    string hash = memoryStream.ComputeSHA2Hash();

                    if (beatmaps.Any(b => b.Hash == hash))
                    {
                        Logger.Log($"Skipping import of {osuFile} due to duplicate file content.", LoggingTarget.Database);
                        skippedFiles.Add(osuFile);
                        continue;
                    }

                    string md5 = memoryStream.ComputeMD5Hash();

                    // Accumulate for set-level hash.
                    memoryStream.Position = 0;
                    memoryStream.CopyTo(hashableContent);

                    var decodedInfo = decoded.BeatmapInfo;
                    var decodedDifficulty = decodedInfo.Difficulty;

                    var difficulty = new BeatmapDifficulty
                    {
                        DrainRate = decodedDifficulty.DrainRate,
                        CircleSize = decodedDifficulty.CircleSize,
                        OverallDifficulty = decodedDifficulty.OverallDifficulty,
                        ApproachRate = decodedDifficulty.ApproachRate,
                        SliderMultiplier = decodedDifficulty.SliderMultiplier,
                        SliderTickRate = decodedDifficulty.SliderTickRate,
                    };

                    var metadata = new BeatmapMetadata
                    {
                        Title = decoded.Metadata.Title,
                        TitleUnicode = decoded.Metadata.TitleUnicode,
                        Artist = decoded.Metadata.Artist,
                        ArtistUnicode = decoded.Metadata.ArtistUnicode,
                        Author =
                        {
                            OnlineID = decoded.Metadata.Author.OnlineID,
                            Username = decoded.Metadata.Author.Username,
                        },
                        Source = decoded.Metadata.Source,
                        Tags = decoded.Metadata.Tags,
                        PreviewTime = decoded.Metadata.PreviewTime,
                        AudioFile = decoded.Metadata.AudioFile,
                        BackgroundFile = decoded.Metadata.BackgroundFile,
                    };

                    var beatmapInfo = new BeatmapInfo(decodedInfo.Ruleset, difficulty, metadata)
                    {
                        Hash = hash,
                        LocalFilePath = osuFile.ToStandardisedPath(),
                        DifficultyName = decodedInfo.DifficultyName,
                        OnlineID = decodedInfo.OnlineID,
                        BeatDivisor = decodedInfo.BeatDivisor,
                        MD5Hash = md5,
                        Length = decoded.HitObjects.LastOrDefault()?.GetEndTime() ?? 0,
                        BPM = decoded.ControlPointInfo.BPMMinimum,
                        EndTimeObjectCount = decoded.HitObjects.Count(h => h is IHasDuration),
                        TotalObjectCount = decoded.HitObjects.Count,
                    };

                    beatmaps.Add(beatmapInfo);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Skipping invalid .osu file {osuFile}.", LoggingTarget.Database);
                    skippedFiles.Add(osuFile);
                }
            }

            if (!beatmaps.Any())
                return new PreparedBeatmapSet(null, skippedFiles);

            hashableContent.Position = 0;
            beatmapSet.Hash = hashableContent.Length > 0 ? hashableContent.ComputeSHA2Hash() : reader.Name.ComputeSHA2Hash();

            foreach (var beatmap in beatmaps)
            {
                beatmap.BeatmapSet = beatmapSet;
                beatmapSet.Beatmaps.Add(beatmap);
            }

            return new PreparedBeatmapSet(beatmapSet, skippedFiles);
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
                safeName = "chartmania";

            string baseDirectoryName = $"{safeName}-{hash[..8]}";
            string directoryName = baseDirectoryName;
            int suffix = 2;

            while (maniaStorage.ExistsDirectory(directoryName))
                directoryName = $"{baseDirectoryName}-{suffix++}";

            return Path.Combine(MANIA_STORAGE_PATH, directoryName).ToStandardisedPath();
        }

        private static DateTimeOffset getDateAdded(DirectoryArchiveReader reader, IEnumerable<string> beatmapFiles)
        {
            var dateAdded = DateTimeOffset.UtcNow;

            foreach (string file in beatmapFiles)
            {
                var fileDate = File.GetLastWriteTimeUtc(reader.GetFullPath(file));

                if (fileDate < dateAdded)
                    dateAdded = fileDate;
            }

            return dateAdded;
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
