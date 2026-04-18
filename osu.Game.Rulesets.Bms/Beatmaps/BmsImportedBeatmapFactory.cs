// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    internal static class BmsImportedBeatmapFactory
    {
        private static readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private static readonly BmsRuleset ruleset = new BmsRuleset();

        public static BmsDecodedBeatmap Create(Stream stream, string filename)
        {
            var decodedChart = decoder.Decode(stream, filename);

            var decodedBeatmap = new BmsDecodedBeatmap(decodedChart);
            var convertedBeatmap = (BmsBeatmap)ruleset.CreateBeatmapConverter(decodedBeatmap).Convert();

            if (convertedBeatmap.HitObjects.Count == 0)
                throw new InvalidDataException($"BMS file {filename} did not produce any playable notes.");

            decodedBeatmap.BeatmapInfo = createBeatmapInfo(convertedBeatmap.BeatmapInfo);
            return decodedBeatmap;
        }

        private static BeatmapInfo createBeatmapInfo(BeatmapInfo convertedInfo)
        {
            return new BeatmapInfo(ruleset.RulesetInfo.Clone(), new BeatmapDifficulty(convertedInfo.Difficulty), convertedInfo.Metadata.DeepClone())
            {
                StarRating = convertedInfo.StarRating,
                DifficultyName = convertedInfo.DifficultyName,
                BeatDivisor = convertedInfo.BeatDivisor,
                Length = convertedInfo.Length,
                BPM = convertedInfo.BPM,
                EndTimeObjectCount = convertedInfo.EndTimeObjectCount,
                TotalObjectCount = convertedInfo.TotalObjectCount,
            };
        }
    }
}
