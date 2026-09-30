// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Beatmaps
{
    public interface IBmsDownloadImporter
    {
        Task<IReadOnlyList<BmsDownloadImportedBeatmap>> ImportAsync(string archivePath, IReadOnlyCollection<string> expectedMd5s, CancellationToken cancellationToken);
    }

    public record BmsDownloadImportedBeatmap(Guid BeatmapId, string Md5);
}
