// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace osu.Game.Beatmaps
{
    public interface IManiaDownloadImporter
    {
        Task<IReadOnlyList<ManiaDownloadImportedBeatmap>> ImportAsync(string archivePath, int expectedSetId, int requestedBeatmapId, CancellationToken cancellationToken);
    }

    public record ManiaDownloadImportedBeatmap(Guid BeatmapId, int OnlineId, string Md5);
}
