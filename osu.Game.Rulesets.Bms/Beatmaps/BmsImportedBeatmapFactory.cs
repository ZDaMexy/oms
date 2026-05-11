// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;

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

            // Reuse the converted timing and hitobject data for raw working-beatmap consumers like
            // Song Select, which read WorkingBeatmap.Beatmap rather than a ruleset-playable conversion.
            decodedBeatmap.ControlPointInfo = convertedBeatmap.ControlPointInfo;
            decodedBeatmap.HitObjects = convertedBeatmap.HitObjects;
            decodedBeatmap.Breaks = convertedBeatmap.Breaks;
            decodedBeatmap.BeatmapInfo = createBeatmapInfo(convertedBeatmap);
            return decodedBeatmap;
        }

        private static BeatmapInfo createBeatmapInfo(BmsBeatmap convertedBeatmap)
        {
            var convertedInfo = convertedBeatmap.BeatmapInfo;
            var metadata = convertedInfo.Metadata.DeepClone();

            metadata.SetChartFilterStats(BmsChartFilterStats.FromBeatmap(convertedBeatmap));

            return new BeatmapInfo(ruleset.RulesetInfo.Clone(), new BeatmapDifficulty(convertedInfo.Difficulty), metadata)
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
