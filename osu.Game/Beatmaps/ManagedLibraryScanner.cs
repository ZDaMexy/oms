// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Beatmaps
{
    public class ManagedLibraryScanner
    {
        private readonly ExternalLibraryScanner scanner;
        private readonly ExternalLibraryScanner.ScanRootDefinition[] roots;

        public Func<string, CancellationToken, Task>? BmsDirectoryImporter { get; set; }

        public Func<string, CancellationToken, Task>? ManiaDirectoryImporter { get; set; }

        public ManagedLibraryScanner(ExternalLibraryScanner scanner, IEnumerable<ExternalLibraryScanner.ScanRootDefinition> roots)
        {
            this.scanner = scanner;
            this.roots = roots.ToArray();
        }

        public Task<ExternalLibraryScanner.ScanResult> ScanAllRoots(IProgress<ExternalLibraryScanner.ScanProgress>? progress = null, CancellationToken cancellationToken = default)
            => scanner.ScanRoots(roots, progress, cancellationToken, BmsDirectoryImporter, ManiaDirectoryImporter);
    }
}
