// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.DifficultyTable;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsBeatmapConverter : BeatmapConverter<HitObject>
    {
        private const double fallback_initial_bpm = 120;
        private const double stop_freeze_beat_length = 6;

        private readonly RulesetInfo rulesetInfo;

        public BmsBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
            rulesetInfo = ruleset.RulesetInfo;
        }

        public override bool CanConvert() => Beatmap is BmsDecodedBeatmap;

        protected override Beatmap<HitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            if (original is not BmsDecodedBeatmap decodedBeatmap)
            {
                return new BmsBeatmap
                {
                    BeatmapInfo = new BeatmapInfo(rulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata())
                };
            }

            return convertDecodedChart(decodedBeatmap.DecodedChart, cancellationToken);
        }

        private BmsBeatmap convertDecodedChart(BmsDecodedChart decodedChart, CancellationToken cancellationToken)
        {
            var beatmap = new BmsBeatmap
            {
                BmsInfo = decodedChart.BeatmapInfo.Clone(),
                BeatmapInfo = new BeatmapInfo(rulesetInfo, new BeatmapDifficulty(), new BeatmapMetadata())
            };

            populateMetadata(beatmap, decodedChart.BeatmapInfo);
            buildControlPointsAndHitObjects(beatmap, decodedChart, cancellationToken);

            beatmap.BeatmapInfo.UpdateStatisticsFromBeatmap(beatmap);

            if (string.IsNullOrEmpty(beatmap.BeatmapInfo.Metadata.AudioFile) && beatmap.HitObjects.Count > 0)
                beatmap.BeatmapInfo.Length = beatmap.GetLastObjectTime();

            beatmap.BeatmapInfo.TotalObjectCount = beatmap.HitObjects.Count(h => h is BmsHitObject);
            beatmap.BeatmapInfo.EndTimeObjectCount = beatmap.HitObjects.Count(h => h is BmsHoldNote);

            return beatmap;
        }

        private static void populateMetadata(BmsBeatmap beatmap, BmsBeatmapInfo bmsInfo)
        {
            float overallDifficulty = OsuOdJudgementSystem.MapRankToOverallDifficulty(bmsInfo.Rank);
            int keyCount = BmsRuleset.GetKeyCount(bmsInfo.Keymode);

            beatmap.Difficulty.CircleSize = keyCount;
            beatmap.Difficulty.OverallDifficulty = overallDifficulty;
            beatmap.Difficulty.SliderMultiplier = 1;
            beatmap.BeatmapInfo.Difficulty.CircleSize = keyCount;
            beatmap.BeatmapInfo.Difficulty.OverallDifficulty = overallDifficulty;
            beatmap.BeatmapInfo.Difficulty.SliderMultiplier = 1;
            beatmap.BeatmapInfo.DifficultyName = getDifficultyName(bmsInfo);

            var metadata = beatmap.BeatmapInfo.Metadata;

            metadata.Title = string.IsNullOrWhiteSpace(bmsInfo.Title) ? "Unknown" : bmsInfo.Title;
            metadata.TitleUnicode = metadata.Title;
            metadata.Artist = string.IsNullOrWhiteSpace(bmsInfo.Artist) ? "Unknown" : bmsInfo.Artist;
            metadata.ArtistUnicode = metadata.Artist;
            metadata.Tags = bmsInfo.Genre;
            metadata.BackgroundFile = bmsInfo.BackgroundFile ?? bmsInfo.StageFile ?? bmsInfo.BannerFile ?? string.Empty;

            var chartMetadata = BmsChartMetadata.FromBeatmapInfo(bmsInfo);
            string? chartCreator = chartMetadata.TryGetChartCreator();

            if (!string.IsNullOrWhiteSpace(chartCreator))
                metadata.Author.Username = chartCreator;

            metadata.SetChartMetadata(chartMetadata);
        }

        private static string getDifficultyName(BmsBeatmapInfo bmsInfo)
        {
            string difficultyLabel = bmsInfo.HeaderDifficulty switch
            {
                1 => "Beginner",
                2 => "Normal",
                3 => "Hyper",
                4 => "Another",
                5 => "Insane",
                _ => string.Empty,
            };

            if (!string.IsNullOrWhiteSpace(difficultyLabel) && !string.IsNullOrWhiteSpace(bmsInfo.PlayLevel))
                return $"{difficultyLabel} {bmsInfo.PlayLevel}";

            if (!string.IsNullOrWhiteSpace(difficultyLabel))
                return difficultyLabel;

            if (!string.IsNullOrWhiteSpace(bmsInfo.PlayLevel))
                return bmsInfo.PlayLevel;

            return "BMS";
        }

        private static void buildControlPointsAndHitObjects(BmsBeatmap beatmap, BmsDecodedChart decodedChart, CancellationToken cancellationToken)
        {
            var timeline = buildEventTimeline(beatmap.ControlPointInfo, decodedChart, cancellationToken);
            var eventTimes = timeline.EventTimes;
            var hitObjects = new List<HitObject>(decodedChart.ObjectEvents.Count + decodedChart.LongNoteEvents.Count);

            beatmap.SetMeasureStartTimes(timeline.MeasureStartTimes);

            foreach (var objectEvent in decodedChart.ObjectEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double time = eventTimes[new BmsEventTimeKey(objectEvent.MeasureIndex, objectEvent.FractionWithinMeasure)];

                if (objectEvent.AutoPlay)
                {
                    hitObjects.Add(new BmsBgmEvent
                    {
                        StartTime = time,
                        KeysoundId = objectEvent.ObjectId,
                        KeysoundSample = createKeysoundSample(decodedChart.BeatmapInfo, objectEvent.ObjectId),
                    });

                    continue;
                }

                hitObjects.Add(new BmsHitObject
                {
                    StartTime = time,
                    LaneIndex = mapLaneIndex(decodedChart.BeatmapInfo.Keymode, objectEvent.Channel),
                    KeysoundId = objectEvent.ObjectId,
                    KeysoundSample = createKeysoundSample(decodedChart.BeatmapInfo, objectEvent.ObjectId),
                    Keymode = decodedChart.BeatmapInfo.Keymode,
                    IsScratch = isScratchLane(decodedChart.BeatmapInfo.Keymode, objectEvent.Channel),
                    AutoPlay = false,
                });
            }

            foreach (var longNoteEvent in decodedChart.LongNoteEvents)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double startTime = eventTimes[new BmsEventTimeKey(longNoteEvent.StartMeasureIndex, longNoteEvent.StartFractionWithinMeasure)];
                double endTime = eventTimes[new BmsEventTimeKey(longNoteEvent.EndMeasureIndex, longNoteEvent.EndFractionWithinMeasure)];

                hitObjects.Add(new BmsHoldNote
                {
                    StartTime = startTime,
                    EndTime = endTime,
                    LaneIndex = mapLaneIndex(decodedChart.BeatmapInfo.Keymode, longNoteEvent.LaneChannel),
                    HeadKeysoundId = longNoteEvent.HeadObjectId,
                    HeadKeysoundSample = createKeysoundSample(decodedChart.BeatmapInfo, longNoteEvent.HeadObjectId),
                    TailKeysoundId = longNoteEvent.TailObjectId,
                    TailKeysoundSample = createKeysoundSample(decodedChart.BeatmapInfo, longNoteEvent.TailObjectId),
                    Keymode = decodedChart.BeatmapInfo.Keymode,
                    IsScratch = isScratchLane(decodedChart.BeatmapInfo.Keymode, longNoteEvent.LaneChannel),
                    AutoPlay = false,
                });
            }

            beatmap.HitObjects = hitObjects.OrderBy(h => h.StartTime).ToList();

            foreach (var hitObject in beatmap.HitObjects)
                hitObject.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
        }

        private static TimelineBuildResult buildEventTimeline(ControlPointInfo controlPointInfo, BmsDecodedChart decodedChart, CancellationToken cancellationToken)
        {
            var eventTimes = new Dictionary<BmsEventTimeKey, double>();
            var measureStartTimes = new List<double>();
            var eventFractionsByMeasure = new SortedDictionary<int, SortedSet<double>>();
            var bpmEventsByKey = decodedChart.BpmChangeEvents
                                             .GroupBy(toKey)
                                             .ToDictionary(group => group.Key, group => (IReadOnlyList<BmsBpmChangeEvent>)group.ToList());
            var stopEventsByKey = decodedChart.StopEvents
                                              .GroupBy(toKey)
                                              .ToDictionary(group => group.Key, group => (IReadOnlyList<BmsStopEvent>)group.ToList());

            int maxMeasureIndex = 0;

            register(decodedChart.ObjectEvents.Select(e => new BmsEventTimeKey(e.MeasureIndex, e.FractionWithinMeasure)));
            register(decodedChart.LongNoteEvents.SelectMany(e => new[]
            {
                new BmsEventTimeKey(e.StartMeasureIndex, e.StartFractionWithinMeasure),
                new BmsEventTimeKey(e.EndMeasureIndex, e.EndFractionWithinMeasure),
            }));
            register(decodedChart.BpmChangeEvents.Select(toKey));
            register(decodedChart.StopEvents.Select(toKey));

            double currentTime = 0;
            double currentBpm = getInitialBpm(decodedChart);

            addTimingControlPoint(controlPointInfo, 0, currentBpm);

            for (int measureIndex = 0; measureIndex <= maxMeasureIndex; measureIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                measureStartTimes.Add(currentTime);

                double beatsPerMeasure = 4 * decodedChart.BeatmapInfo.GetMeasureLengthMultiplier(measureIndex);
                double previousFraction = 0;

                if (eventFractionsByMeasure.TryGetValue(measureIndex, out var fractions))
                {
                    foreach (double fraction in fractions)
                    {
                        double fractionDelta = fraction - previousFraction;

                        if (fractionDelta > 0)
                            currentTime += fractionDelta * beatsPerMeasure * getBeatLength(currentBpm);

                        var key = new BmsEventTimeKey(measureIndex, fraction);
                        eventTimes[key] = currentTime;

                        if (bpmEventsByKey.TryGetValue(key, out var bpmEvents))
                        {
                            foreach (var bpmEvent in bpmEvents)
                            {
                                currentBpm = bpmEvent.Bpm;
                                addTimingControlPoint(controlPointInfo, currentTime, currentBpm);
                            }
                        }

                        if (stopEventsByKey.TryGetValue(key, out var stopEvents))
                        {
                            foreach (var stopEvent in stopEvents)
                            {
                                double stopDuration = stopEvent.StopValue / 192.0 * 4.0 * getBeatLength(currentBpm);

                                if (stopDuration <= 0)
                                    continue;

                                controlPointInfo.Add(currentTime, new TimingControlPoint { BeatLength = stop_freeze_beat_length });
                                currentTime += stopDuration;
                                addTimingControlPoint(controlPointInfo, currentTime, currentBpm);
                            }
                        }

                        previousFraction = fraction;
                    }
                }

                currentTime += (1 - previousFraction) * beatsPerMeasure * getBeatLength(currentBpm);
            }

            return new TimelineBuildResult(eventTimes, measureStartTimes);

            void register(IEnumerable<BmsEventTimeKey> keys)
            {
                foreach (var key in keys)
                {
                    maxMeasureIndex = Math.Max(maxMeasureIndex, key.MeasureIndex);

                    if (!eventFractionsByMeasure.TryGetValue(key.MeasureIndex, out var measureFractions))
                    {
                        measureFractions = new SortedSet<double>();
                        eventFractionsByMeasure[key.MeasureIndex] = measureFractions;
                    }

                    measureFractions.Add(key.FractionWithinMeasure);
                }
            }
        }

        private static BmsEventTimeKey toKey(BmsBpmChangeEvent bpmChangeEvent) => new BmsEventTimeKey(bpmChangeEvent.MeasureIndex, bpmChangeEvent.FractionWithinMeasure);

        private static BmsEventTimeKey toKey(BmsStopEvent stopEvent) => new BmsEventTimeKey(stopEvent.MeasureIndex, stopEvent.FractionWithinMeasure);

        private static BmsKeysoundSampleInfo? createKeysoundSample(BmsBeatmapInfo beatmapInfo, int? keysoundId)
        {
            if (!keysoundId.HasValue)
                return null;

            return beatmapInfo.KeysoundTable.TryGetValue(keysoundId.Value, out string? filename) && BmsKeysoundSampleInfo.TryCreate(filename, out var sample)
                ? sample
                : null;
        }

        private static double getInitialBpm(BmsDecodedChart decodedChart)
        {
            if (decodedChart.BeatmapInfo.InitialBpm > 0)
                return decodedChart.BeatmapInfo.InitialBpm;

            var firstBpmEvent = decodedChart.BpmChangeEvents.FirstOrDefault();
            return firstBpmEvent.Bpm > 0 ? firstBpmEvent.Bpm : fallback_initial_bpm;
        }

        private static void addTimingControlPoint(ControlPointInfo controlPointInfo, double time, double bpm)
            => controlPointInfo.Add(time, new TimingControlPoint { BeatLength = getBeatLength(bpm) });

        private static double getBeatLength(double bpm) => 60000.0 / Math.Max(1, bpm);

        private static int mapLaneIndex(BmsKeymode keymode, int channel)
        {
            if (keymode == BmsKeymode.Key9K_Bms || keymode == BmsKeymode.Key9K_Pms)
                return channel - 0x11;

            if (keymode == BmsKeymode.Key5K || keymode == BmsKeymode.Key7K)
            {
                return channel switch
                {
                    0x16 => 0,
                    0x11 => 1,
                    0x12 => 2,
                    0x13 => 3,
                    0x14 => 4,
                    0x15 => 5,
                    0x18 => 6,
                    0x19 => 7,
                    _ => channel - 0x11,
                };
            }

            if (keymode == BmsKeymode.Key14K)
            {
                return channel switch
                {
                    0x16 => 0,
                    0x11 => 1,
                    0x12 => 2,
                    0x13 => 3,
                    0x14 => 4,
                    0x15 => 5,
                    0x18 => 6,
                    0x19 => 7,
                    0x21 => 8,
                    0x22 => 9,
                    0x23 => 10,
                    0x24 => 11,
                    0x25 => 12,
                    0x28 => 13,
                    0x29 => 14,
                    0x26 => 15,
                    _ => channel - 0x11,
                };
            }

            return channel - 0x11;
        }

        private static bool isScratchLane(BmsKeymode keymode, int channel)
        {
            if (keymode == BmsKeymode.Key5K || keymode == BmsKeymode.Key7K)
                return channel == 0x16;

            if (keymode == BmsKeymode.Key14K)
                return channel == 0x16 || channel == 0x26;

            return false;
        }

        private readonly struct TimelineBuildResult
        {
            public Dictionary<BmsEventTimeKey, double> EventTimes { get; }

            public IReadOnlyList<double> MeasureStartTimes { get; }

            public TimelineBuildResult(Dictionary<BmsEventTimeKey, double> eventTimes, IReadOnlyList<double> measureStartTimes)
            {
                EventTimes = eventTimes;
                MeasureStartTimes = measureStartTimes;
            }
        }

        private readonly struct BmsEventTimeKey : IEquatable<BmsEventTimeKey>
        {
            public int MeasureIndex { get; }

            public double FractionWithinMeasure { get; }

            public BmsEventTimeKey(int measureIndex, double fractionWithinMeasure)
            {
                MeasureIndex = measureIndex;
                FractionWithinMeasure = fractionWithinMeasure;
            }

            public bool Equals(BmsEventTimeKey other)
                => MeasureIndex == other.MeasureIndex && FractionWithinMeasure.Equals(other.FractionWithinMeasure);

            public override bool Equals(object? obj)
                => obj is BmsEventTimeKey other && Equals(other);

            public override int GetHashCode()
                => HashCode.Combine(MeasureIndex, FractionWithinMeasure);
        }
    }
}
