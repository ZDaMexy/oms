// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Game.Online.Bms
{
    public enum BmsDownloadSource
    {
        Ginger,
        Konmai
    }

    public record BmsDownloadChart(string Md5, string Title, string Artist, string DifficultyName = "", int? KeyCount = null, string? Level = null, Uri? PreviewUrl = null, string? FileName = null);

    public record BmsDownloadPackage(BmsDownloadSource Source, string Id, string Name, Uri? DownloadUrl, IReadOnlyList<BmsDownloadChart> Charts, long? Size = null, Uri? CoverUrl = null)
    {
        public string Key => Source + ":" + Id;

        public bool CanDownload => DownloadUrl != null;
    }

    public record BmsDownloadTable(string Id, string Name, string OriginalUrl);

    public record BmsDownloadSearchResult(IReadOnlyList<BmsDownloadPackage> Packages, int Page, int TotalPages, int Total);
}
