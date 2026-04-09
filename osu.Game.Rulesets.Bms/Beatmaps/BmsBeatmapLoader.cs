// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.IO;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsBeatmapLoader : ICustomBeatmapLoader
    {
        public IEnumerable<string> FileExtensions => BmsImportExtensions.BeatmapFileExtensions;

        public bool CanLoad(BeatmapInfo beatmapInfo, string filename)
            => beatmapInfo.Ruleset.ShortName == BmsRuleset.SHORT_NAME && BmsImportExtensions.IsBeatmapFile(filename);

        public IBeatmap Load(Stream stream, string filename, BeatmapInfo beatmapInfo)
            => BmsImportedBeatmapFactory.Create(stream, filename);
    }
}
