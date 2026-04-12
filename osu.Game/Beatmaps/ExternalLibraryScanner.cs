// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Logging;
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
        private readonly ExternalLibraryConfig config;

        /// <summary>
        /// Delegate that imports a single BMS folder (a directory containing .bms/.bme/.bml/.pms files)
        /// into <c>chartbms/</c>. Accepts the directory path and a cancellation token.
        /// </summary>
        public Func<string, CancellationToken, Task>? BmsDirectoryImporter { get; set; }

        /// <summary>
        /// Delegate that imports a single mania folder (a directory containing .osu files)
        /// into <c>chartmania/</c>. Accepts the directory path and a cancellation token.
        /// </summary>
        public Func<string, CancellationToken, Task>? ManiaDirectoryImporter { get; set; }

        public ExternalLibraryScanner(ExternalLibraryConfig config)
        {
            this.config = config;
        }

        /// <summary>
        /// Scan all enabled roots and import discovered beatmap directories.
        /// Returns the total number of directories successfully passed to importers.
        /// </summary>
        public async Task<ScanResult> ScanAllRoots(IProgress<ScanProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            var roots = config.GetEnabledRoots().ToList();
            int totalImported = 0;
            int totalSkipped = 0;
            int totalErrors = 0;

            for (int i = 0; i < roots.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress(roots[i].Path, i, roots.Count, totalImported));

                var result = await ScanRoot(roots[i], cancellationToken).ConfigureAwait(false);
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
        {
            if (!Directory.Exists(root.Path))
            {
                Logger.Log($"External library root not found, skipping: {root.Path}", LoggingTarget.Database, LogLevel.Important);
                return new ScanResult(0, 0, 1);
            }

            return root.Type switch
            {
                ExternalLibraryRootType.BMS => await scanBmsRoot(root.Path, cancellationToken).ConfigureAwait(false),
                ExternalLibraryRootType.Mania => await scanManiaRoot(root.Path, cancellationToken).ConfigureAwait(false),
                _ => new ScanResult(0, 0, 0),
            };
        }

        private async Task<ScanResult> scanBmsRoot(string rootPath, CancellationToken cancellationToken)
        {
            if (BmsDirectoryImporter == null)
            {
                Logger.Log("BMS directory importer not registered; skipping BMS root scan.", LoggingTarget.Database, LogLevel.Important);
                return new ScanResult(0, 0, 0);
            }

            int imported = 0;
            int skipped = 0;
            int errors = 0;

            // BMS convention: each immediate subdirectory under the root is a potential BMS set.
            // A valid BMS set contains at least one .bms/.bme/.bml/.pms file at its top level.
            string[] subdirectories;

            try
            {
                subdirectories = Directory.GetDirectories(rootPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to enumerate BMS root: {rootPath}");
                return new ScanResult(0, 0, 1);
            }

            foreach (string dir in subdirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    bool hasBmsFiles = Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
                                                .Any(f => isBmsFile(f));

                    if (!hasBmsFiles)
                    {
                        skipped++;
                        continue;
                    }

                    await BmsDirectoryImporter(dir, cancellationToken).ConfigureAwait(false);
                    imported++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to import BMS directory: {dir}");
                    errors++;
                }
            }

            return new ScanResult(imported, skipped, errors);
        }

        private async Task<ScanResult> scanManiaRoot(string rootPath, CancellationToken cancellationToken)
        {
            if (ManiaDirectoryImporter == null)
            {
                Logger.Log("Mania directory importer not registered; skipping mania root scan.", LoggingTarget.Database, LogLevel.Important);
                return new ScanResult(0, 0, 0);
            }

            int imported = 0;
            int skipped = 0;
            int errors = 0;

            // Mania convention: each immediate subdirectory under the root is a potential beatmap set.
            // A valid mania set contains at least one .osu file at its top level.
            string[] subdirectories;

            try
            {
                subdirectories = Directory.GetDirectories(rootPath);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Failed to enumerate mania root: {rootPath}");
                return new ScanResult(0, 0, 1);
            }

            foreach (string dir in subdirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    bool hasOsuFiles = Directory.EnumerateFiles(dir, "*.osu", SearchOption.TopDirectoryOnly).Any();

                    if (!hasOsuFiles)
                    {
                        skipped++;
                        continue;
                    }

                    await ManiaDirectoryImporter(dir, cancellationToken).ConfigureAwait(false);
                    imported++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, $"Failed to import mania directory: {dir}");
                    errors++;
                }
            }

            return new ScanResult(imported, skipped, errors);
        }

        private static readonly string[] bms_extensions = { ".bms", ".bme", ".bml", ".pms" };

        private static bool isBmsFile(string path)
        {
            string ext = Path.GetExtension(path);
            return bms_extensions.Any(e => ext.Equals(e, StringComparison.OrdinalIgnoreCase));
        }

        public readonly struct ScanProgress
        {
            public string CurrentRoot { get; }
            public int RootIndex { get; }
            public int TotalRoots { get; }
            public int ImportedSoFar { get; }

            public ScanProgress(string currentRoot, int rootIndex, int totalRoots, int importedSoFar)
            {
                CurrentRoot = currentRoot;
                RootIndex = rootIndex;
                TotalRoots = totalRoots;
                ImportedSoFar = importedSoFar;
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
