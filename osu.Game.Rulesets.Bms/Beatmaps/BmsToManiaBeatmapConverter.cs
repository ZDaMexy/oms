// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Bms.UI;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mania.Beatmaps;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Bms.Beatmaps
{
    public class BmsToManiaBeatmapConverter : BeatmapConverter<ManiaHitObject>
    {
        private const double control_point_epsilon = 0.0001;

        private readonly BmsDecodedBeatmap sourceBeatmap;
        private readonly BmsLaneLayout sourceLaneLayout;
        private readonly ManiaRuleset targetRuleset;
        private readonly int totalColumns;
        private readonly int stageColumns;
        private readonly bool dualStage;
        private readonly int?[] judgementColumnsByLane;
        private readonly int[] scratchSampleColumnsByLane;

        public BmsToManiaBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
            sourceBeatmap = beatmap as BmsDecodedBeatmap ?? throw new ArgumentException($"{nameof(BmsToManiaBeatmapConverter)} requires a {nameof(BmsDecodedBeatmap)} source.", nameof(beatmap));
            targetRuleset = ruleset as ManiaRuleset ?? throw new ArgumentException($"{nameof(BmsToManiaBeatmapConverter)} requires a {nameof(ManiaRuleset)} target.", nameof(ruleset));

            sourceLaneLayout = BmsLaneLayout.CreateForKeymode(sourceBeatmap.DecodedChart.BeatmapInfo.Keymode);
            judgementColumnsByLane = new int?[sourceLaneLayout.Lanes.Count];
            scratchSampleColumnsByLane = new int[sourceLaneLayout.Lanes.Count];
            totalColumns = sourceLaneLayout.Lanes.Count(lane => !lane.IsScratch);
            dualStage = sourceBeatmap.DecodedChart.BeatmapInfo.Keymode == BmsKeymode.Key14K;
            stageColumns = dualStage ? totalColumns / 2 : totalColumns;

            initialiseLaneColumnMaps();
        }

        public override bool CanConvert() => totalColumns > 0;

        protected override Beatmap<ManiaHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            var playableSourceBeatmap = createSourceBeatmap(cancellationToken);
            var convertedBeatmap = (ManiaBeatmap)base.ConvertBeatmap(playableSourceBeatmap, cancellationToken);
            var scorableHitObjects = convertedBeatmap.HitObjects.Where(isScorableHitObject).ToList();

            // K9 #9: a source chart that flattens / degrades to zero scorable mania objects (e.g. scratch-only or
            // empty) must be judged invalid so it never reaches presentation / song-select as a playable mania
            // conversion. BeatmapDifficultyCache.computeDifficulty and BackgroundDataStoreProcessor both catch this
            // and persist it as a Failed converted-star-rating, so deterministic non-convertibility doesn't get
            // retried on every lookup.
            if (scorableHitObjects.Count == 0)
                throw new BeatmapInvalidForRulesetException($"BMS source chart ({sourceBeatmap.DecodedChart.BeatmapInfo.Keymode}) produced no scorable mania hit objects after lane flatten and scratch sample-only degrade.");

            convertedBeatmap.ControlPointInfo = createConvertedControlPointInfo(playableSourceBeatmap.ControlPointInfo);

            convertedBeatmap.Difficulty.CircleSize = totalColumns;
            convertedBeatmap.BeatmapInfo.Difficulty.CircleSize = totalColumns;
            convertedBeatmap.BeatmapInfo.TotalObjectCount = scorableHitObjects.Count;
            convertedBeatmap.BeatmapInfo.EndTimeObjectCount = scorableHitObjects.Count(hitObject => hitObject is HoldNote);

            foreach (var hitObject in convertedBeatmap.HitObjects)
                hitObject.ApplyDefaults(convertedBeatmap.ControlPointInfo, convertedBeatmap.Difficulty, cancellationToken);

            convertedBeatmap.BeatmapInfo.StarRating = targetRuleset.CreateDifficultyCalculator(new DirectPlayableWorkingBeatmap(createDifficultyBeatmap(convertedBeatmap, scorableHitObjects), targetRuleset.RulesetInfo)).Calculate(cancellationToken).StarRating;

            return convertedBeatmap;
        }

        protected override Beatmap<ManiaHitObject> CreateBeatmap()
        {
            var beatmap = new ManiaBeatmap(new StageDefinition(stageColumns));

            if (dualStage)
                beatmap.Stages.Add(new StageDefinition(stageColumns));

            return beatmap;
        }

        protected override IEnumerable<ManiaHitObject> ConvertHitObject(HitObject original, IBeatmap beatmap, CancellationToken cancellationToken)
        {
            switch (original)
            {
                case BmsHoldNote holdNote when holdNote.IsScratch:
                    foreach (var scratchSampleEvent in createScratchSampleHitObjects(holdNote))
                        yield return scratchSampleEvent;

                    break;

                case BmsHoldNote holdNote:
                    yield return new HoldNote
                    {
                        StartTime = holdNote.StartTime,
                        EndTime = holdNote.EndTime,
                        Column = getTargetColumn(holdNote.LaneIndex),
                        Samples = createSamples(holdNote.HeadKeysoundSample),
                        NodeSamples = new List<IList<HitSampleInfo>>
                        {
                            createSamples(holdNote.HeadKeysoundSample),
                            // K11: align with the BMS-side "LN tail silent" contract (P1-J #3a). mania plays the last
                            // node sample on tail release; carrying the tail keysound here re-introduces the head/tail
                            // double for LNTYPE1 charts whose tail object repeats the head WAV. Head still sounds via
                            // NodeSamples[0]; the tail keysound stays in the BMS model only to arm empty-strike lines.
                            new List<HitSampleInfo>(),
                        },
                    };
                    break;

                case BmsHitObject hitObject when hitObject.IsScratch:
                    var scratchSampleHitObject = createScratchSampleHitObject(hitObject.StartTime, hitObject.LaneIndex, hitObject.KeysoundSample, hitObject.KeysoundId);

                    if (scratchSampleHitObject != null)
                        yield return scratchSampleHitObject;

                    break;

                case BmsHitObject hitObject:
                    yield return new Note
                    {
                        StartTime = hitObject.StartTime,
                        Column = getTargetColumn(hitObject.LaneIndex),
                        Samples = createSamples(hitObject.KeysoundSample),
                    };
                    break;

                // K11: BGM (autoplay channel 0x01) carries the non-playable audio layer (drums / bass / backing /
                // vocals). Without this it is silently dropped and a pure-keysound BMS plays hollow in mania. Emitted
                // as a sample-only, ignore-judgement object so it never enters combo / statistics / star / autoplay.
                case BmsBgmEvent bgmEvent:
                    var bgmSample = createBgmSampleHitObject(bgmEvent);

                    if (bgmSample != null)
                        yield return bgmSample;

                    break;
            }
        }

        // Allocated once and reused so each mania-side conversion doesn't pay a fresh BmsRuleset() construction
        // (which sets up input bindings, mod state, etc.) just to walk the BMS->BMS conversion step below.
        private static readonly Ruleset cached_bms_ruleset_instance = new BmsRuleset();

        private BmsBeatmap createSourceBeatmap(CancellationToken cancellationToken)
        {
            // Reuse the K5 source-bound modless playable projection so a session that needs both BMS playable and
            // mania playable from the same BmsDecodedBeatmap doesn't run BmsBeatmapConverter twice. The wrapper's
            // mutable HitObjects are not the source of truth — BmsBeatmapConverter always re-derives them from
            // DecodedChart — so caching the converted BmsBeatmap is consistent with K9's modless source gate.
            if (sourceBeatmap.TryGetCachedModlessPlayableBeatmap(cached_bms_ruleset_instance.RulesetInfo, out var cached) && cached is BmsBeatmap cachedBmsBeatmap)
                return cachedBmsBeatmap;

            var converted = (BmsBeatmap)cached_bms_ruleset_instance.CreateBeatmapConverter(sourceBeatmap).Convert(cancellationToken);
            sourceBeatmap.CacheModlessPlayableBeatmap(cached_bms_ruleset_instance.RulesetInfo, converted);
            return converted;
        }

        private ManiaBeatmap createDifficultyBeatmap(ManiaBeatmap convertedBeatmap, IReadOnlyList<ManiaHitObject> scorableHitObjects)
        {
            var difficultyBeatmap = (ManiaBeatmap)CreateBeatmap();

            // DeepClone metadata so a future DifficultyCalculator implementation that decides to mutate it can't leak
            // back into the converted beatmap's BeatmapInfo. Calculators today only read metadata, but the contract
            // is shared with ScoreProcessor / PerformanceCalculator and isn't read-only.
            difficultyBeatmap.BeatmapInfo = new BeatmapInfo(targetRuleset.RulesetInfo.Clone(), convertedBeatmap.Difficulty.Clone(), convertedBeatmap.Metadata.DeepClone());
            difficultyBeatmap.ControlPointInfo = convertedBeatmap.ControlPointInfo;
            difficultyBeatmap.Breaks = convertedBeatmap.Breaks;
            difficultyBeatmap.HitObjects = scorableHitObjects.ToList();

            return difficultyBeatmap;
        }

        private static ControlPointInfo createConvertedControlPointInfo(ControlPointInfo sourceControlPointInfo)
        {
            var convertedControlPointInfo = new ControlPointInfo();
            TimingControlPoint? lastTimingPoint = null;

            foreach (var timingPoint in sourceControlPointInfo.TimingPoints)
            {
                if (isBmsStopFreezeTimingPoint(timingPoint))
                    continue;

                if (lastTimingPoint != null && areEquivalentTimingPoints(lastTimingPoint, timingPoint))
                    continue;

                lastTimingPoint = new TimingControlPoint
                {
                    BeatLength = timingPoint.BeatLength,
                    OmitFirstBarLine = timingPoint.OmitFirstBarLine,
                    TimeSignature = timingPoint.TimeSignature,
                };

                convertedControlPointInfo.Add(timingPoint.Time, lastTimingPoint);
            }

            return convertedControlPointInfo;
        }

        private static bool isBmsStopFreezeTimingPoint(TimingControlPoint timingPoint)
            => timingPoint is BmsStopFreezeTimingControlPoint;

        private static bool areEquivalentTimingPoints(TimingControlPoint left, TimingControlPoint right)
            => Math.Abs(left.BeatLength - right.BeatLength) <= control_point_epsilon
               && left.OmitFirstBarLine == right.OmitFirstBarLine
               && left.TimeSignature.Equals(right.TimeSignature);

        private IEnumerable<ManiaHitObject> createScratchSampleHitObjects(BmsHoldNote holdNote)
        {
            // Scratch long-note: only the head sounds. The tail keysound is intentionally dropped to match the
            // BMS-side "LN tail silent" contract (P1-J #3a) — the same alignment applied to non-scratch holds via the
            // empty NodeSamples[1]. LNTYPE1 tails commonly repeat the head WAV, so emitting a tail sample object here
            // re-introduces the head/tail double (e.g. GOODBOUNCE scratch vocal "stomp your fee feet"). K11.
            var head = createScratchSampleHitObject(holdNote.StartTime, holdNote.LaneIndex, holdNote.HeadKeysoundSample, holdNote.HeadKeysoundId);

            if (head != null)
                yield return head;
        }

        private BmsConvertedScratchSampleHitObject? createScratchSampleHitObject(double time, int laneIndex, BmsKeysoundSampleInfo? sample, int? keysoundId)
        {
            var samples = createSamples(sample);

            if (samples.Count == 0)
                return null;

            return new BmsConvertedScratchSampleHitObject
            {
                StartTime = time,
                Column = getScratchSampleColumn(laneIndex),
                Samples = samples,
                KeysoundSample = sample,
                KeysoundId = keysoundId,
            };
        }

        private BmsConvertedBgmSampleHitObject? createBgmSampleHitObject(BmsBgmEvent bgmEvent)
        {
            var samples = createSamples(bgmEvent.KeysoundSample);

            if (samples.Count == 0)
                return null;

            // BGM is not bound to a playable lane; it only needs a valid anchor column for the sample-only drawable,
            // so it pins to column 0 (always valid because CanConvert guarantees totalColumns > 0). Column carries no
            // judgement / stereo meaning here.
            return new BmsConvertedBgmSampleHitObject
            {
                StartTime = bgmEvent.StartTime,
                Column = 0,
                Samples = samples,
                KeysoundSample = bgmEvent.KeysoundSample,
                KeysoundId = bgmEvent.KeysoundId,
            };
        }

        private void initialiseLaneColumnMaps()
        {
            int currentColumn = 0;

            for (int laneIndex = 0; laneIndex < sourceLaneLayout.Lanes.Count; laneIndex++)
            {
                bool isScratch = sourceLaneLayout.Lanes[laneIndex].IsScratch;

                if (!isScratch)
                {
                    judgementColumnsByLane[laneIndex] = currentColumn++;
                    continue;
                }

                // Scratch sample anchors to the nearest judged column on the same side (immediately to the right of
                // a left-side scratch, or to the left of the right-side scratch). Non-scratch lanes are not queried
                // through getScratchSampleColumn so we only fill scratch entries here.
                scratchSampleColumnsByLane[laneIndex] = Math.Min(currentColumn, totalColumns - 1);
            }
        }

        private int getTargetColumn(int laneIndex)
        {
            validateLaneIndex(laneIndex);

            if (judgementColumnsByLane[laneIndex] is not int column)
                throw new InvalidOperationException($"BMS lane {laneIndex} is a scratch lane and does not map to a mania judgement column.");

            return column;
        }

        private int getScratchSampleColumn(int laneIndex)
        {
            validateLaneIndex(laneIndex);

            return scratchSampleColumnsByLane[laneIndex];
        }

        private void validateLaneIndex(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= sourceLaneLayout.Lanes.Count)
                throw new InvalidOperationException($"BMS lane {laneIndex} is outside the supported {sourceBeatmap.DecodedChart.BeatmapInfo.Keymode} layout.");
        }

        private static bool isScorableHitObject(ManiaHitObject hitObject)
            => hitObject is not (BmsConvertedScratchSampleHitObject or BmsConvertedBgmSampleHitObject);

        private static List<HitSampleInfo> createSamples(BmsKeysoundSampleInfo? sample)
            => sample == null
                ? new List<HitSampleInfo>()
                : new List<HitSampleInfo> { sample.With() };
    }

    public static class BmsToManiaBeatmapConverterFactory
    {
        public static bool CanCreate(IBeatmap beatmap) => beatmap is BmsDecodedBeatmap;

        public static IBeatmapConverter Create(IBeatmap beatmap, Ruleset ruleset)
            => new BmsToManiaBeatmapConverter(beatmap, ruleset);
    }
}
