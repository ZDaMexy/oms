// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.IO;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.DifficultyTable;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    internal static class BmsImportedBeatmapFactory
    {
        private static readonly BmsBeatmapDecoder decoder = new BmsBeatmapDecoder();
        private static readonly BmsRuleset ruleset = new BmsRuleset();

        /// <summary>
        /// Decodes the raw BMS chart structure ONLY (channel/note events), skipping the expensive playable conversion
        /// (timeline D(t) walk, keysound materialisation, mines/BGA/scroll profile, ApplyDefaults). Used by the
        /// chart-filter-stats backfill, which only needs note counts and must not pay the full <see cref="Create"/> cost
        /// across a large library.
        /// </summary>
        internal static BmsDecodedChart DecodeChart(Stream stream, string filename) => decoder.Decode(stream, filename);

        public static BmsDecodedBeatmap Create(Stream stream, string filename)
        {
            var decodedChart = decoder.Decode(stream, filename);

            var decodedBeatmap = new BmsDecodedBeatmap(decodedChart);
            var convertedBeatmap = (BmsBeatmap)ruleset.CreateBeatmapConverter(decodedBeatmap).Convert();

            prepareModlessPlayableBeatmap(convertedBeatmap);

            if (convertedBeatmap.HitObjects.Count == 0)
                throw new InvalidDataException($"BMS file {filename} did not produce any playable notes.");

            // Reuse the converted timing and hitobject data for raw working-beatmap consumers like
            // Song Select, which read WorkingBeatmap.Beatmap rather than a ruleset-playable conversion.
            decodedBeatmap.CacheModlessPlayableBeatmap(ruleset.RulesetInfo, convertedBeatmap);
            decodedBeatmap.ControlPointInfo = convertedBeatmap.ControlPointInfo;
            decodedBeatmap.HitObjects = convertedBeatmap.HitObjects;
            decodedBeatmap.Breaks = convertedBeatmap.Breaks;
            decodedBeatmap.BeatmapInfo = createBeatmapInfo(convertedBeatmap);
            return decodedBeatmap;
        }

        private static void prepareModlessPlayableBeatmap(BmsBeatmap convertedBeatmap)
        {
            var processor = ruleset.CreateBeatmapProcessor(convertedBeatmap);

            processor?.PreProcess();

            foreach (var obj in convertedBeatmap.HitObjects)
                obj.ApplyDefaults(convertedBeatmap.ControlPointInfo, convertedBeatmap.Difficulty, CancellationToken.None);

            processor?.PostProcess();
        }

        private static BeatmapInfo createBeatmapInfo(BmsBeatmap convertedBeatmap)
        {
            var convertedInfo = convertedBeatmap.BeatmapInfo;
            var metadata = convertedInfo.Metadata.DeepClone();

            // Resolve (not just Set) so even a chart with no playable objects is marked processed at import time and
            // never falls into the startup composition backfill's Phase 2 "missing" set.
            metadata.ResolveChartFilterStats(BmsChartFilterStats.FromBeatmap(convertedBeatmap));

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
