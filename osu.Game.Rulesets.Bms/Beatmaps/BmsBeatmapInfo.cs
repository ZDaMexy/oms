// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
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

        public string SubArtist { get; set; } = string.Empty;

        public string Artist { get; set; } = string.Empty;

        public string Comment { get; set; } = string.Empty;

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

        /// <summary>
        /// Preview audio file specified by the #PREVIEW header.
        /// </summary>
        public string? PreviewFile { get; set; }

        public BmsKeymode Keymode { get; set; } = BmsKeymode.Key7K;

        public IReadOnlyList<BmsMeasureLengthControlPoint> MeasureLengthControlPoints => measureLengthControlPoints;

        public IDictionary<int, string> KeysoundTable { get; } = new SortedDictionary<int, string>();

        public IDictionary<int, string> BitmapTable { get; } = new SortedDictionary<int, string>();

        public IDictionary<int, double> ExtendedBpmTable { get; } = new SortedDictionary<int, double>();

        public IDictionary<int, double> StopTable { get; } = new SortedDictionary<int, double>();

        public IDictionary<int, double> ScrollTable { get; } = new SortedDictionary<int, double>();

        public IDictionary<int, BmsBgaDefinition> BgaDefinitions { get; } = new SortedDictionary<int, BmsBgaDefinition>();

        public IDictionary<int, BmsAtBgaDefinition> AtBgaDefinitions { get; } = new SortedDictionary<int, BmsAtBgaDefinition>();

        public IDictionary<int, BmsArgbDefinition> ArgbDefinitions { get; } = new SortedDictionary<int, BmsArgbDefinition>();

        public IDictionary<int, BmsSwBgaDefinition> SwBgaDefinitions { get; } = new SortedDictionary<int, BmsSwBgaDefinition>();

        public BmsPoorBgaMode? PoorBgaMode { get; set; }

        public IDictionary<string, string> UnknownHeaders { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<BmsVisualDefinitionProjection> GetVisualDefinitionProjections()
        {
            var definitionIndices = new SortedSet<int>();

            foreach (int index in BgaDefinitions.Keys)
                definitionIndices.Add(index);

            foreach (int index in AtBgaDefinitions.Keys)
                definitionIndices.Add(index);

            foreach (int index in ArgbDefinitions.Keys)
                definitionIndices.Add(index);

            foreach (int index in SwBgaDefinitions.Keys)
                definitionIndices.Add(index);

            foreach (int index in definitionIndices)
            {
                if (TryGetVisualDefinitionProjection(index, out BmsVisualDefinitionProjection projection))
                    yield return projection;
            }
        }

        public bool TryGetVisualDefinitionProjection(int index, out BmsVisualDefinitionProjection projection)
        {
            bool hasIndexedDefinition = false;

            BmsBgaDefinition? bgaDefinition = null;
            if (BgaDefinitions.TryGetValue(index, out BmsBgaDefinition parsedBgaDefinition))
            {
                bgaDefinition = parsedBgaDefinition;
                hasIndexedDefinition = true;
            }

            BmsAtBgaDefinition? atBgaDefinition = null;
            if (AtBgaDefinitions.TryGetValue(index, out BmsAtBgaDefinition parsedAtBgaDefinition))
            {
                atBgaDefinition = parsedAtBgaDefinition;
                hasIndexedDefinition = true;
            }

            BmsArgbDefinition? argbDefinition = null;
            if (ArgbDefinitions.TryGetValue(index, out BmsArgbDefinition parsedArgbDefinition))
            {
                argbDefinition = parsedArgbDefinition;
                hasIndexedDefinition = true;
            }

            BmsSwBgaDefinition? swBgaDefinition = null;
            if (SwBgaDefinitions.TryGetValue(index, out BmsSwBgaDefinition parsedSwBgaDefinition))
            {
                swBgaDefinition = parsedSwBgaDefinition;
                hasIndexedDefinition = true;
            }

            if (!hasIndexedDefinition)
            {
                projection = default;
                return false;
            }

            projection = new BmsVisualDefinitionProjection(index, bgaDefinition, atBgaDefinition, argbDefinition, swBgaDefinition, PoorBgaMode);
            return true;
        }

        public string? GetPreferredBackgroundAssetReference()
        {
            if (!string.IsNullOrWhiteSpace(StageFile))
                return StageFile;

            if (!string.IsNullOrWhiteSpace(BackgroundFile))
                return BackgroundFile;

            if (!string.IsNullOrWhiteSpace(BannerFile))
                return BannerFile;

            foreach (var projection in GetVisualDefinitionProjections())
            {
                string? bitmapReference = resolveBitmapReference(projection.BgaDefinition?.BitmapReference)
                                          ?? resolveBitmapReference(projection.AtBgaDefinition?.BitmapReference);

                if (!string.IsNullOrWhiteSpace(bitmapReference))
                    return bitmapReference;
            }

            return null;
        }

        private string? resolveBitmapReference(string? bitmapReference)
        {
            if (string.IsNullOrWhiteSpace(bitmapReference))
                return null;

            if (bitmapReference.Length == 2 && BmsBeatmapDecoder.TryParseBase36(bitmapReference, out int bitmapIndex))
            {
                if (BitmapTable.TryGetValue(bitmapIndex, out string? resolvedBitmapReference) && !string.IsNullOrWhiteSpace(resolvedBitmapReference))
                    return resolvedBitmapReference;

                return null;
            }

            return bitmapReference;
        }

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
                SubArtist = SubArtist,
                Artist = Artist,
                Comment = Comment,
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
                PreviewFile = PreviewFile,
                Keymode = Keymode,
                PoorBgaMode = PoorBgaMode,
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

            foreach (var pair in ScrollTable)
                clone.ScrollTable[pair.Key] = pair.Value;

            foreach (var pair in BgaDefinitions)
                clone.BgaDefinitions[pair.Key] = pair.Value;

            foreach (var pair in AtBgaDefinitions)
                clone.AtBgaDefinitions[pair.Key] = pair.Value;

            foreach (var pair in ArgbDefinitions)
                clone.ArgbDefinitions[pair.Key] = pair.Value;

            foreach (var pair in SwBgaDefinitions)
                clone.SwBgaDefinitions[pair.Key] = pair.Value;

            foreach (var pair in UnknownHeaders)
                clone.UnknownHeaders[pair.Key] = pair.Value;

            return clone;
        }
    }
}
