// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using osu.Game.Rulesets.Bms.Difficulty;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    /// <summary>
    /// BMS-specific metadata decoded from a single chart file.
    /// This remains separate from osu! beatmap persistence types until conversion time.
    /// </summary>
    public class BmsBeatmapInfo
    {
        private readonly List<BmsMeasureLengthControlPoint> measureLengthControlPoints = new List<BmsMeasureLengthControlPoint>();

        public string Title { get; set; } = string.Empty;

        public string Subtitle { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string Genre { get; set; } = string.Empty;

        public double InitialBpm { get; set; }

        public string PlayLevel { get; set; } = string.Empty;

        public int? HeaderDifficulty { get; set; }

        public int Rank { get; set; } = 2;

        public double Total { get; set; } = 200;

        public string? StageFile { get; set; }

        public string? BannerFile { get; set; }

        public string? BackgroundFile { get; set; }

        public int? LongNoteObjectId { get; set; }

        public int? LongNoteType { get; set; }

        public BmsKeymode Keymode { get; set; } = BmsKeymode.Key7K;

        public IReadOnlyList<BmsMeasureLengthControlPoint> MeasureLengthControlPoints => measureLengthControlPoints;

        public IDictionary<int, string> KeysoundTable { get; } = new SortedDictionary<int, string>();

        public IDictionary<int, string> BitmapTable { get; } = new SortedDictionary<int, string>();

        public IDictionary<int, double> ExtendedBpmTable { get; } = new SortedDictionary<int, double>();

        public IDictionary<int, double> StopTable { get; } = new SortedDictionary<int, double>();

        public void SetMeasureLengthControlPoints(IEnumerable<BmsMeasureLengthControlPoint> controlPoints)
        {
            var deduplicated = new SortedDictionary<int, double>();

            foreach (var controlPoint in controlPoints)
                deduplicated[controlPoint.MeasureIndex] = controlPoint.Multiplier;

            measureLengthControlPoints.Clear();

            foreach (var pair in deduplicated)
                measureLengthControlPoints.Add(new BmsMeasureLengthControlPoint(pair.Key, pair.Value));
        }

        public double GetMeasureLengthMultiplier(int measureIndex)
        {
            foreach (var controlPoint in measureLengthControlPoints)
            {
                if (controlPoint.MeasureIndex == measureIndex)
                    return controlPoint.Multiplier;

                if (controlPoint.MeasureIndex > measureIndex)
                    break;
            }

            return 1.0;
        }

        public BmsBeatmapInfo Clone()
        {
            var clone = new BmsBeatmapInfo
            {
                Title = Title,
                Subtitle = Subtitle,
                Artist = Artist,
                Genre = Genre,
                InitialBpm = InitialBpm,
                PlayLevel = PlayLevel,
                HeaderDifficulty = HeaderDifficulty,
                Rank = Rank,
                Total = Total,
                StageFile = StageFile,
                BannerFile = BannerFile,
                BackgroundFile = BackgroundFile,
                LongNoteObjectId = LongNoteObjectId,
                LongNoteType = LongNoteType,
                Keymode = Keymode,
            };

            clone.SetMeasureLengthControlPoints(measureLengthControlPoints);

            foreach (var pair in KeysoundTable)
                clone.KeysoundTable[pair.Key] = pair.Value;

            foreach (var pair in BitmapTable)
                clone.BitmapTable[pair.Key] = pair.Value;

            foreach (var pair in ExtendedBpmTable)
                clone.ExtendedBpmTable[pair.Key] = pair.Value;

            foreach (var pair in StopTable)
                clone.StopTable[pair.Key] = pair.Value;

            return clone;
        }
    }
}
