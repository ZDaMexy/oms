// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public partial class BmsGaugeProcessor : HealthProcessor
    {
        public const double DEFAULT_TOTAL = 200;
        public const double STARTING_GAUGE = 0.2;
        public const double SURVIVAL_FLOOR = 0.02;
        public const double CLEAR_THRESHOLD = 0.8;

        private readonly Bindable<BmsGaugeType> gaugeType = new Bindable<BmsGaugeType>();

        public IBindable<BmsGaugeType> GaugeTypeBindable => gaugeType;

        public virtual BmsGaugeType GaugeType
        {
            get => gaugeType.Value;
            protected set => gaugeType.Value = value;
        }

        public virtual bool IsGaugeAutoShiftActive => false;

        protected virtual double RecoveryMultiplier
            => GaugeType switch
            {
                BmsGaugeType.AssistEasy => 1.6,
                BmsGaugeType.Easy => 1.2,
                _ => 0.8,
            };

        protected virtual double DamageMultiplier
            => GaugeType switch
            {
                BmsGaugeType.AssistEasy => 4.0,
                BmsGaugeType.Easy => 6.0,
                BmsGaugeType.Normal => 8.0,
                BmsGaugeType.Hard => 25.0,
                BmsGaugeType.ExHard => 50.0,
                BmsGaugeType.Hazard => 8.0,
                _ => 8.0,
            };

        protected virtual double StartingGauge => GetStartingGauge(GaugeType);

        protected virtual double FloorGauge => GetFloorGauge(GaugeType);

        public double BaseRate { get; private set; }

        public double ChartTotal { get; private set; } = DEFAULT_TOTAL;

        public int TotalHittableObjects { get; private set; }

        public bool IsClear => MeetsClearCondition(GaugeType, Health.Value, HasFailed);

        public BmsGaugeProcessor(double drainStartTime, BmsGaugeType gaugeType = BmsGaugeType.Normal)
        {
            GaugeType = gaugeType;
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            ChartTotal = beatmap is BmsBeatmap bmsBeatmap ? bmsBeatmap.BmsInfo.Total : DEFAULT_TOTAL;
            TotalHittableObjects = countHittableObjects(beatmap);
            BaseRate = CalculateBaseRate(ChartTotal, TotalHittableObjects);

            base.ApplyBeatmap(beatmap);
        }

        public static double CalculateBaseRate(double total, int totalHittableObjects)
        {
            if (totalHittableObjects <= 0)
                return 0;

            return total / (totalHittableObjects * 100.0);
        }

        public static BmsGaugeType GetGaugeType(IEnumerable<Mod>? mods)
            => GetGaugeAutoShift(mods)?.GetStartingGaugeType() ?? mods?.OfType<BmsModGauge>().LastOrDefault()?.GaugeType ?? BmsGaugeType.Normal;

        public static BmsGaugeType GetGaugeType(ScoreInfo score)
            => score.GetRulesetData<BmsScoreInfoData>()?.GaugeType ?? GetGaugeType(score.Mods);

        public static BmsGaugeType GetStartingGaugeType(IEnumerable<Mod>? mods)
            => GetGaugeAutoShift(mods)?.GetStartingGaugeType() ?? GetGaugeType(mods);

        public static BmsGaugeType GetStartingGaugeType(ScoreInfo score)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            if (scoreData?.UsesGaugeAutoShift == true)
                return scoreData.StartingGaugeType;

            return scoreData?.GaugeType ?? GetStartingGaugeType(score.Mods);
        }

        public static BmsGaugeType GetFloorGaugeType(IEnumerable<Mod>? mods)
            => GetGaugeAutoShift(mods)?.GetFloorGaugeType() ?? GetGaugeType(mods);

        public static BmsGaugeType GetFloorGaugeType(ScoreInfo score)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            if (scoreData?.UsesGaugeAutoShift == true)
                return scoreData.FloorGaugeType;

            return scoreData?.GaugeType ?? GetFloorGaugeType(score.Mods);
        }

        public static bool UsesGaugeAutoShift(IEnumerable<Mod>? mods)
            => GetGaugeAutoShift(mods) != null;

        public static bool UsesGaugeAutoShift(ScoreInfo score)
            => score.GetRulesetData<BmsScoreInfoData>()?.UsesGaugeAutoShift ?? UsesGaugeAutoShift(score.Mods);

        public static string GetGaugeDisplayName(ScoreInfo score)
            => UsesGaugeAutoShift(score)
                ? $"GAS ({GetGaugeType(score).GetDisplayName()})"
                : GetGaugeType(score).GetDisplayName();

        public static BmsGaugeProcessor CreateForMods(double drainStartTime, IEnumerable<Mod>? mods)
            => GetGaugeAutoShift(mods) is BmsModGaugeAutoShift gasMod
                ? new BmsGasGaugeProcessor(drainStartTime, gasMod.GetStartingGaugeType(), gasMod.GetFloorGaugeType())
                : new BmsGaugeProcessor(drainStartTime, GetGaugeType(mods));

        public static BmsGaugeProcessor CreateForScore(double drainStartTime, ScoreInfo score)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>();

            if (scoreData?.UsesGaugeAutoShift == true)
                return new BmsGasGaugeProcessor(drainStartTime, scoreData.StartingGaugeType, scoreData.FloorGaugeType);

            if (scoreData == null)
                return CreateForMods(drainStartTime, score.Mods);

            return new BmsGaugeProcessor(drainStartTime, scoreData?.GaugeType ?? GetGaugeType(score.Mods));
        }

        public static BmsGaugeType GetLowerGaugeType(BmsGaugeType gaugeType)
            => gaugeType switch
            {
                BmsGaugeType.Hazard => BmsGaugeType.ExHard,
                BmsGaugeType.ExHard => BmsGaugeType.Hard,
                BmsGaugeType.Hard => BmsGaugeType.Normal,
                BmsGaugeType.Normal => BmsGaugeType.Easy,
                BmsGaugeType.Easy => BmsGaugeType.AssistEasy,
                _ => BmsGaugeType.AssistEasy,
            };

        public static double GetStartingGauge(BmsGaugeType gaugeType)
            => UsesSurvivalClear(gaugeType) ? 1.0 : STARTING_GAUGE;

        public static double GetFloorGauge(BmsGaugeType gaugeType)
            => UsesSurvivalClear(gaugeType) ? 0 : SURVIVAL_FLOOR;

        public static bool UsesSurvivalClear(BmsGaugeType gaugeType)
            => gaugeType is BmsGaugeType.Hard or BmsGaugeType.ExHard or BmsGaugeType.Hazard;

        public static bool MeetsClearCondition(BmsGaugeType gaugeType, double finalGauge, bool hasFailed)
            => UsesSurvivalClear(gaugeType)
                ? !hasFailed && finalGauge > 0
                : finalGauge >= CLEAR_THRESHOLD;

        protected override double GetHealthIncreaseFor(JudgementResult result)
        {
            if (result.HitObject is BmsHoldNoteBodyTick { CountsForGauge: true })
            {
                switch (result.Type)
                {
                    case HitResult.IgnoreHit:
                        return BaseRate * RecoveryMultiplier * BmsHoldNoteBodyTick.TICK_QUANTUM / 1000.0;

                    case HitResult.IgnoreMiss:
                        return -BaseRate * DamageMultiplier * BmsHoldNoteBodyTick.TICK_QUANTUM / 1000.0;
                }
            }

            if (result.HitObject is BmsEmptyPoorHitObject && result.Type == HitResult.ComboBreak)
                return getBadDamage();

            switch (result.Type)
            {
                case HitResult.Perfect:
                case HitResult.Great:
                    return BaseRate * RecoveryMultiplier;

                case HitResult.Good:
                    return GaugeType == BmsGaugeType.Hazard ? 0 : BaseRate * RecoveryMultiplier * 0.5;

                case HitResult.Meh:
                    return getBadDamage();

                case HitResult.Miss:
                    return getPoorDamage();

                default:
                    return 0;
            }
        }

        protected override bool CheckDefaultFailCondition(JudgementResult result)
            => UsesSurvivalClear(GaugeType) && Health.Value <= 0.000001;

        protected override bool CountsResultTowardsJudgedHits(JudgementResult result)
            => result.HitObject is not BmsEmptyPoorHitObject
               && result.HitObject is not BmsBgmEvent;

        protected override int GetJudgedHitCountFromReplayFrame(osu.Game.Rulesets.Replays.ReplayFrame frame)
        {
            if (frame.Header == null)
                return 0;

            int judgedHits = 0;

            foreach ((var result, int count) in frame.Header.Statistics)
            {
                if (result == HitResult.ComboBreak)
                    continue;

                judgedHits += count;
            }

            return judgedHits;
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);

            Health.MinValue = FloorGauge;
            Health.Value = StartingGauge;
        }

        private static BmsModGaugeAutoShift? GetGaugeAutoShift(IEnumerable<Mod>? mods)
            => mods?.OfType<BmsModGaugeAutoShift>().LastOrDefault();

        private double getBadDamage()
            => GaugeType == BmsGaugeType.Hazard ? -1 : -BaseRate * DamageMultiplier;

        private double getPoorDamage()
            => GaugeType == BmsGaugeType.Hazard ? -1 : -BaseRate * DamageMultiplier * 1.5;

        private static int countHittableObjects(IBeatmap beatmap)
            => beatmap.HitObjects
                      .SelectMany(hitObject => hitObject.NestedHitObjects.Prepend(hitObject))
                      .Count(hitObject => hitObject switch
                      {
                          BmsBgmEvent => false,
                          BmsHoldNote => false,
                          BmsHoldNoteTailEvent { Judgement: BmsHoldNoteTailJudgement { CountsForScore: false } } => false,
                          BmsHitObject bmsHitObject => !bmsHitObject.AutoPlay,
                          _ => false,
                      });
    }
}
