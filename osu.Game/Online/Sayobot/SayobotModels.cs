// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;

namespace osu.Game.Online.Sayobot
{
    public enum SayobotCategory
    {
        All = 0,
        Ranked = 1,
        Qualified = 2,
        Loved = 4,
        Pending = 8,
        Graveyard = 16
    }

    public record SayobotBeatmap(int Id, string DifficultyName, int KeyCount, double StarRating);

    public record SayobotBeatmapSet(int Id, string Title, string Artist, string Creator, int Status, IReadOnlyList<SayobotBeatmap> Beatmaps, Uri DownloadUrl, Uri CoverUrl)
    {
        public string Key => "Sayobot:" + Id.ToString(CultureInfo.InvariantCulture);
    }

    public record SayobotSearchQuery(string Text, int? KeyCount = null, double? MinStars = null, double? MaxStars = null, SayobotCategory Category = SayobotCategory.All);

    public record SayobotSearchResult(IReadOnlyList<SayobotBeatmapSet> Sets, int NextOffset, bool HasMore);
}
