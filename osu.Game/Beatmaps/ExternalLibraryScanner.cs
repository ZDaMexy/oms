// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Walks registered <see cref="ExternalLibraryRoot"/>s and delegates discovered beatmap directories
    /// to the appropriate ruleset-specific importer.
    /// </summary>
    /// <remarks>
    /// The scanner itself is import-format agnostic; actual import logic is injected as delegates
    /// so that osu.Game does not depend on ruleset assemblies.
    /// </remarks>
    public class ExternalLibraryScanner
    {
        public enum ScanMode
        {
            Rebuild,
            Incremental,
        }

        public readonly struct ScanRootDefinition
        {
            public string Path { get; }

            public ExternalLibraryRootType Type { get; }

            public bool External { get; }

            public ScanRootDefinition(string path, ExternalLibraryRootType type, bool external = true)
            {
                Path = path;
                Type = type;
                External = external;
            }
        }

        private readonly ExternalLibraryConfig config;
        private readonly FilesystemBeatmapIndex? index;
        private readonly SemaphoreSlim scanLock = new SemaphoreSlim(1);

        private enum DirectoryScanClassification
        {
            NotRelevant,
            Candidate,
            Rejected,
        }

        /// <summary>
        /// Delegate that imports a single BMS folder (a directory containing .bms/.bme/.bml/.pms files)
        /// into <c>chartbms/</c>. Accepts the directory path, the registered root path, and a cancellation token.
        /// </summary>
        public Func<string, string, CancellationToken, Task>? BmsDirectoryImporter { get; set; }

        /// <summary>
        /// Delegate that imports a single mania folder (a directory containing .osu files)
        /// into <c>chartmania/</c>. Accepts the directory path and a cancellation token.
        /// </summary>
        public Func<string, CancellationToken, Task>? ManiaDirectoryImporter { get; set; }

        /// <summary>
        /// Optional predicate used by incremental scans to determine whether a discovered BMS directory
        /// still needs importing for the current registered root. Returning <see langword="false"/>
        /// treats the directory as skipped.
        /// </summary>
        public Func<string, string, bool>? BmsDirectoryShouldImport { get; set; }

        /// <summary>
        /// Optional predicate used by incremental scans to determine whether a discovered mania directory
        /// still needs importing. Returning <see langword="false"/> treats the directory as skipped.
        /// </summary>
        public Func<string, bool>? ManiaDirectoryShouldImport { get; set; }

        public ExternalLibraryScanner(ExternalLibraryConfig config, FilesystemBeatmapIndex? index = null)
        {
            this.config = config;
            this.index = index;
        }

        public async Task RemoveRoot(ExternalLibraryRoot root)
        {
            await scanLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var remainingRoots = config.Roots.Where(r => r.Type == root.Type
                                                            && !string.Equals(FilesystemBeatmapIndex.NormalisePath(r.Path), FilesystemBeatmapIndex.NormalisePath(root.Path), StringComparison.OrdinalIgnoreCase))
                                           .Select(r => r.Path).ToArray();
                index?.Unregister(root.Path, root.Type, remainingRoots);
                config.RemoveRoot(root.Path);
            }
            finally
            {
                scanLock.Release();
            }
        }

        /// <summary>
        /// Scan all enabled roots and import discovered beatmap directories.
        /// Returns the total number of directories successfully passed to importers.
        /// </summary>
        public async Task<ScanResult> ScanAllRoots(IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
            => await ScanAllRoots(ScanMode.Rebuild, progress, cancellationToken).ConfigureAwait(false);

        /// <summary>
        /// Scan all enabled roots and import discovered beatmap directories using the specified scan mode.
        /// </summary>
        public async Task<ScanResult> ScanAllRoots(ScanMode mode, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var roots = config.GetEnabledRoots().ToList();
            int totalImported = 0;
            int totalSkipped = 0;
            int totalErrors = 0;

            if (roots.Count == 0)
                progress?.Report(new ScanProgress(string.Empty, 0, 0, null, 0, 0, 0, 0, 0));

            for (int i = 0; i < roots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ScanRoot(new ScanRootDefinition(roots[i].Path, roots[i].Type), i, roots.Count, totalImported, totalSkipped, totalErrors,
                    progress, cancellationToken, mode, BmsDirectoryImporter, ManiaDirectoryImporter, BmsDirectoryShouldImport, ManiaDirectoryShouldImport, requireRegistered: true).ConfigureAwait(false);
                totalImported += result.Imported;
                totalSkipped += result.Skipped;
                totalErrors += result.Errors;

                config.MarkScanned(roots[i]);
            }

            return new ScanResult(totalImported, totalSkipped, totalErrors);
        }

        /// <summary>
        /// Scan a single root and import discovered beatmap directories.
        /// </summary>
        public async Task<ScanResult> ScanRoot(ExternalLibraryRoot root, CancellationToken cancellationToken = default)
            => await ScanRoot(root, ScanMode.Rebuild, cancellationToken).ConfigureAwait(false);

        public async Task<ScanResult> ScanRoot(ExternalLibraryRoot root, ScanMode mode, CancellationToken cancellationToken = default)
            => await ScanRoot(new ScanRootDefinition(root.Path, root.Type), 0, 1, 0, 0, 0, null, cancellationToken, mode,
                BmsDirectoryImporter, ManiaDirectoryImporter, BmsDirectoryShouldImport, ManiaDirectoryShouldImport).ConfigureAwait(false);

        public async Task<ScanResult> ScanRoots(IEnumerable<ScanRootDefinition> roots, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default,
                                                Func<string, string, CancellationToken, Task>? bmsDirectoryImporter = null, Func<string, CancellationToken, Task>? maniaDirectoryImporter = null)
            => await ScanRoots(roots, ScanMode.Rebuild, progress, cancellationToken, bmsDirectoryImporter, maniaDirectoryImporter).ConfigureAwait(false);

        public async Task<ScanResult> ScanRoots(IEnumerable<ScanRootDefinition> roots, ScanMode mode, IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default,
                                                Func<string, string, CancellationToken, Task>? bmsDirectoryImporter = null, Func<string, CancellationToken, Task>? maniaDirectoryImporter = null,
                                                Func<string, string, bool>? bmsDirectoryShouldImport = null, Func<string, bool>? maniaDirectoryShouldImport = null)
        {
            var rootList = roots.ToList();
            int totalImported = 0;
            int totalSkipped = 0;
            int totalErrors = 0;

            if (rootList.Count == 0)
                progress?.Report(new ScanProgress(string.Empty, 0, 0, null, 0, 0, 0, 0, 0));

            for (int i = 0; i < rootList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await ScanRoot(rootList[i], i, rootList.Count, totalImported, totalSkipped, totalErrors,
                    progress, cancellationToken, mode, bmsDirectoryImporter ?? BmsDirectoryImporter, maniaDirectoryImporter ?? ManiaDirectoryImporter,
                    bmsDirectoryShouldImport ?? BmsDirectoryShouldImport, maniaDirectoryShouldImport ?? ManiaDirectoryShouldImport).ConfigureAwait(false);

                totalImported += result.Imported;
                totalSkipped += result.Skipped;
                totalErrors += result.Errors;
            }

            return new ScanResult(totalImported, totalSkipped, totalErrors);
        }

        private async Task<ScanResult> ScanRoot(ScanRootDefinition root, int rootIndex, int totalRoots, int importedSoFar, int skippedSoFar, int errorsSoFar,
                                                IProgress<ScanProgress>? progress, CancellationToken cancellationToken, ScanMode mode,
                                                Func<string, string, CancellationToken, Task>? bmsDirectoryImporter, Func<string, CancellationToken, Task>? maniaDirectoryImporter,
                                                Func<string, string, bool>? bmsDirectoryShouldImport, Func<string, bool>? maniaDirectoryShouldImport, bool requireRegistered = false)
        {
            await scanLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // A root can have been removed while another root was scanning.
                if (requireRegistered && !config.Roots.Any(r => string.Equals(FilesystemBeatmapIndex.NormalisePath(r.Path), FilesystemBeatmapIndex.NormalisePath(root.Path), StringComparison.OrdinalIgnoreCase)))
                    return new ScanResult(0, 0, 0);

                return await scanRootCore(root, rootIndex, totalRoots, importedSoFar, skippedSoFar, errorsSoFar,
                    progress, cancellationToken, mode, bmsDirectoryImporter, maniaDirectoryImporter, bmsDirectoryShouldImport, maniaDirectoryShouldImport).ConfigureAwait(false);
            }
            finally
            {
                scanLock.Release();
            }
        }

        private async Task<ScanResult> scanRootCore(ScanRootDefinition root, int rootIndex, int totalRoots, int importedSoFar, int skippedSoFar, int errorsSoFar,
                                                  IProgress<ScanProgress>? progress, CancellationToken cancellationToken, ScanMode mode,
                                                  Func<string, string, CancellationToken, Task>? bmsDirectoryImporter, Func<string, CancellationToken, Task>? maniaDirectoryImporter,
                                                  Func<string, string, bool>? bmsDirectoryShouldImport, Func<string, bool>? maniaDirectoryShouldImport)
        {
            if (!Directory.Exists(root.Path))
            {
                Logger.Log($"External library root not found, skipping: {root.Path}", LoggingTarget.Database, LogLevel.Important);
                progress?.Report(new ScanProgress(root.Path, rootIndex, totalRoots, null, 0, 0, importedSoFar, skippedSoFar, errorsSoFar + 1));
                return new ScanResult(0, 0, 1);
            }

            try
            {
                // Existing configuration and direct scan calls must obey the same no-directory-links boundary as new roots.
                for (DirectoryInfo? directory = new DirectoryInfo(root.Path); directory != null; directory = directory.Parent)
                {
                    if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        Logger.Log($"Skipping linked library root: {root.Path}", LoggingTarget.Database, LogLevel.Important);
                        return new ScanResult(0, 0, 1);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Logger.Error(ex, $"Failed to inspect library root: {root.Path}");
                return new ScanResult(0, 0, 1);
            }

            var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Func<string, string, CancellationToken, Task>? bmsImport = bmsDirectoryImporter == null ? null : async (dir, registeredRoot, ct) =>
            {
                await bmsDirectoryImporter(dir, registeredRoot, ct).ConfigureAwait(false);
                present.Add(dir);
            };
            Func<string, CancellationToken, Task>? maniaImport = maniaDirectoryImporter == null ? null : async (dir, ct) =>
            {
                await maniaDirectoryImporter(dir, ct).ConfigureAwait(false);
                present.Add(dir);
            };

            var result = root.Type switch
            {
                ExternalLibraryRootType.BMS => await scanBmsRoot(root.Path, rootIndex, totalRoots, importedSoFar, skippedSoFar, errorsSoFar,
                    progress, cancellationToken, bmsImport, bmsDirectoryShouldImport, mode).ConfigureAwait(false),
                ExternalLibraryRootType.Mania => await scanManiaRoot(root.Path, rootIndex, totalRoots, importedSoFar, skippedSoFar, errorsSoFar,
                    progress, cancellationToken, maniaImport, maniaDirectoryShouldImport, mode).ConfigureAwait(false),
                _ => new ScanResult(0, 0, 0),
            };

            cancellationToken.ThrowIfCancellationRequested();
            bool importerAvailable = root.Type == ExternalLibraryRootType.BMS ? bmsImport != null : maniaImport != null;
            if (mode == ScanMode.Rebuild && result.Errors == 0 && importerAvailable)
                index?.Reconcile(root.Path, root.Type, root.External, present);
            return result;
        }

        private async Task<ScanResult> scanBmsRoot(string rootPath, int rootIndex, int totalRoots, int importedSoFar, int skippedSoFar, int errorsSoFar,
                                                   IProgress<ScanProgress>? progress, CancellationToken cancellationToken,
                                                   Func<string, string, CancellationToken, Task>? importer, Func<string, string, bool>? shouldImport, ScanMode mode)
        {
            if (importer == null)
            {
                Logger.Log("BMS directory importer not registered; skipping BMS root scan.", LoggingTarget.Database, LogLevel.Important);
                return new ScanResult(0, 0, 0);
            }

            return await scanRootDirectories(
                rootPath,
                dir => Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly).Any(isBmsFile)
                    ? DirectoryScanClassification.Candidate
                    : DirectoryScanClassification.NotRelevant,
                (dir, ct) => importer(dir, rootPath, ct),
                rootIndex,
                totalRoots,
                importedSoFar,
                skippedSoFar,
                errorsSoFar,
                progress,
                cancellationToken,
                mode,
                dir => shouldImport?.Invoke(dir, rootPath) ?? true,
                "BMS").ConfigureAwait(false);
        }

        private async Task<ScanResult> scanManiaRoot(string rootPath, int rootIndex, int totalRoots, int importedSoFar, int skippedSoFar, int errorsSoFar,
                                                     IProgress<ScanProgress>? progress, CancellationToken cancellationToken,
                                                     Func<string, CancellationToken, Task>? importer, Func<string, bool>? shouldImport, ScanMode mode)
        {
            if (importer == null)
            {
                Logger.Log("Mania directory importer not registered; skipping mania root scan.", LoggingTarget.Database, LogLevel.Important);
                return new ScanResult(0, 0, 0);
            }

            return await scanRootDirectories(
                rootPath,
                classifyManiaDirectory,
                importer,
                rootIndex,
                totalRoots,
                importedSoFar,
                skippedSoFar,
                errorsSoFar,
                progress,
                cancellationToken,
                mode,
                shouldImport,
                "mania").ConfigureAwait(false);
        }

        private async Task<ScanResult> scanRootDirectories(string rootPath, Func<string, DirectoryScanClassification> classifyDirectory, Func<string, CancellationToken, Task> importer,
                                                           int rootIndex, int totalRoots, int importedSoFar, int skippedSoFar, int errorsSoFar,
                                                           IProgress<ScanProgress>? progress, CancellationToken cancellationToken, ScanMode mode,
                                                           Func<string, bool>? shouldImport, string rulesetName)
        {
            int imported = 0;
            int skipped = 0;
            int errors = 0;
            string[] directories;
            var timer = Stopwatch.StartNew();

            Logger.Log($"Starting {rulesetName} library {mode} scan: {rootPath}", LoggingTarget.Database);

            try
            {
                (directories, errors) = enumerateDirectories(rootPath, rulesetName, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to enumerate {rulesetName} root: {rootPath}");
                progress?.Report(new ScanProgress(rootPath, rootIndex, totalRoots, null, 0, 0, importedSoFar, skippedSoFar, errorsSoFar + 1));
                return new ScanResult(0, 0, 1);
            }

            long discoveryMilliseconds = timer.ElapsedMilliseconds;

            progress?.Report(new ScanProgress(rootPath, rootIndex, totalRoots, null, 0, directories.Length,
                importedSoFar, skippedSoFar, errorsSoFar + errors));

            int processedDirectories = 0;

            foreach (string dir in directories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress(rootPath, rootIndex, totalRoots, dir, processedDirectories, directories.Length,
                                                  importedSoFar + imported, skippedSoFar + skipped, errorsSoFar + errors));

                bool completed = false;

                try
                {
                    switch (classifyDirectory(dir))
                    {
                        case DirectoryScanClassification.NotRelevant:
                            completed = true;
                            continue;

                        case DirectoryScanClassification.Rejected:
                            skipped++;
                            completed = true;
                            continue;
                    }

                    if (mode == ScanMode.Incremental && shouldImport != null && !shouldImport(dir))
                    {
                        skipped++;
                        completed = true;
                        continue;
                    }

                    await importer(dir, cancellationToken).ConfigureAwait(false);
                    imported++;
                    completed = true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to import {rulesetName} directory: {dir}");
                    errors++;
                    completed = true;
                }
                finally
                {
                    if (completed)
                    {
                        processedDirectories++;
                        progress?.Report(new ScanProgress(rootPath, rootIndex, totalRoots, dir, processedDirectories, directories.Length,
                                                          importedSoFar + imported, skippedSoFar + skipped, errorsSoFar + errors));
                    }
                }
            }

            Logger.Log($"Finished {rulesetName} library {mode} scan: {directories.Length} directories, {imported} imported, {skipped} skipped, {errors} errors; "
                       + $"discovery {discoveryMilliseconds} ms, processing {timer.ElapsedMilliseconds - discoveryMilliseconds} ms. Root: {rootPath}", LoggingTarget.Database);
            return new ScanResult(imported, skipped, errors);
        }

        private static readonly string[] bms_extensions = { ".bms", ".bme", ".bml", ".pms" };

        private static bool isBmsFile(string path)
        {
            string ext = Path.GetExtension(path);
            return bms_extensions.Any(e => ext.Equals(e, StringComparison.OrdinalIgnoreCase));
        }

        private static bool isManiaOsuFile(string path)
        {
            using var stream = File.OpenRead(path);
            return OsuFileModeDetector.IsMania(stream);
        }

        private static DirectoryScanClassification classifyManiaDirectory(string dir)
        {
            bool sawOsuFile = false;

            foreach (string file in Directory.EnumerateFiles(dir, "*.osu", SearchOption.TopDirectoryOnly))
            {
                sawOsuFile = true;

                if (isManiaOsuFile(file))
                    return DirectoryScanClassification.Candidate;
            }

            return sawOsuFile ? DirectoryScanClassification.Rejected : DirectoryScanClassification.NotRelevant;
        }

        private static (string[] Directories, int Errors) enumerateDirectories(string rootPath, string rulesetName, CancellationToken cancellationToken)
        {
            var directories = new List<string> { rootPath };
            var queue = new Queue<string>();
            int errors = 0;

            queue.Enqueue(rootPath);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string current = queue.Dequeue();
                string[] children;

                try
                {
                    children = Directory.GetDirectories(current);
                    Array.Sort(children, StringComparer.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to enumerate {rulesetName} directory: {current}");
                    errors++;
                    continue;
                }

                foreach (string child in children)
                {
                    try
                    {
                        if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0)
                        {
                            Logger.Log($"Skipping linked library directory: {child}", LoggingTarget.Database, LogLevel.Important);
                            errors++;
                            continue;
                        }
                    }
                    catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                    {
                        Logger.Error(ex, $"Failed to inspect library directory: {child}");
                        errors++;
                        continue;
                    }
                    directories.Add(child);
                    queue.Enqueue(child);
                }
            }

            return (directories.ToArray(), errors);
        }

        public readonly struct ScanProgress
        {
            public string CurrentRoot { get; }
            public int RootIndex { get; }
            public int TotalRoots { get; }
            public string? CurrentDirectory { get; }
            public int ProcessedDirectories { get; }
            public int TotalDirectories { get; }
            public int ImportedSoFar { get; }
            public int SkippedSoFar { get; }
            public int ErrorsSoFar { get; }

            public int CurrentDirectoryIndex => string.IsNullOrEmpty(CurrentDirectory) ? ProcessedDirectories : Math.Min(ProcessedDirectories + 1, TotalDirectories);

            public float RootProgress => TotalDirectories <= 0 ? 1 : (float)ProcessedDirectories / TotalDirectories;

            public float OverallProgress => TotalRoots <= 0 ? 1 : (RootIndex + RootProgress) / TotalRoots;

            public ScanProgress(string currentRoot, int rootIndex, int totalRoots, string? currentDirectory, int processedDirectories, int totalDirectories,
                                int importedSoFar, int skippedSoFar, int errorsSoFar)
            {
                CurrentRoot = currentRoot;
                RootIndex = rootIndex;
                TotalRoots = totalRoots;
                CurrentDirectory = currentDirectory;
                ProcessedDirectories = processedDirectories;
                TotalDirectories = totalDirectories;
                ImportedSoFar = importedSoFar;
                SkippedSoFar = skippedSoFar;
                ErrorsSoFar = errorsSoFar;
            }
        }

        public readonly struct ScanResult
        {
            public int Imported { get; }
            public int Skipped { get; }
            public int Errors { get; }

            public ScanResult(int imported, int skipped, int errors)
            {
                Imported = imported;
                Skipped = skipped;
                Errors = errors;
            }
        }
    }
}
