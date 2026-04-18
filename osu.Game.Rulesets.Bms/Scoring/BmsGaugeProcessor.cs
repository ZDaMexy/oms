// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Beatmaps;
using osu.Game.Rulesets.Bms.Difficulty;
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

        private static readonly GaugeGutsStep[] no_guts = Array.Empty<GaugeGutsStep>();
        private static readonly GaugeGutsStep[] beatoraja_hard_guts =
        {
            new GaugeGutsStep(10, 0.4),
            new GaugeGutsStep(20, 0.5),
            new GaugeGutsStep(30, 0.6),
            new GaugeGutsStep(40, 0.7),
            new GaugeGutsStep(50, 0.8),
        };
        private static readonly GaugeGutsStep[] lr2_hard_guts =
        {
            new GaugeGutsStep(30, 0.6),
        };
        private static readonly GaugeGutsStep[] iidx_hard_guts =
        {
            new GaugeGutsStep(30, 0.5, inclusive: true),
        };

        private readonly Bindable<BmsGaugeType> gaugeType = new Bindable<BmsGaugeType>();
        private readonly Bindable<BmsGaugeRulesFamily> gaugeRulesFamily = new Bindable<BmsGaugeRulesFamily>();

        private GaugeSpecification currentGaugeSpecification;
        private BmsKeymode currentKeymode = BmsKeymode.Key7K;

        public IBindable<BmsGaugeType> GaugeTypeBindable => gaugeType;
        public IBindable<BmsGaugeRulesFamily> GaugeRulesFamilyBindable => gaugeRulesFamily;

        public virtual BmsGaugeType GaugeType
        {
            get => gaugeType.Value;
            protected set
            {
                if (gaugeType.Value == value)
                    return;

                gaugeType.Value = value;
                updateGaugeSpecification();
            }
        }

        public BmsGaugeRulesFamily GaugeRulesFamily => gaugeRulesFamily.Value;

        public virtual bool IsGaugeAutoShiftActive => false;

        protected virtual double StartingGaugeValue => currentGaugeSpecification.Initial / 100.0;

        protected virtual double FloorGaugeValue => currentGaugeSpecification.Minimum / 100.0;

        protected virtual double MaximumGaugeValue => currentGaugeSpecification.Maximum / 100.0;

        public double CurrentFloorGauge => FloorGaugeValue;

        public double CurrentMaximumGauge => MaximumGaugeValue;

        public double CurrentClearThreshold => currentGaugeSpecification.Border / 100.0;

        public double BaseRate { get; private set; }

        public double ChartTotal { get; private set; } = DEFAULT_TOTAL;

        public int TotalHittableObjects { get; private set; }

        public BmsKeymode Keymode => currentKeymode;

        public bool IsClear => MeetsClearCondition(GaugeType, GaugeRulesFamily, currentKeymode, Health.Value, HasFailed);

        public BmsGaugeProcessor(double drainStartTime, BmsGaugeType gaugeType = BmsGaugeType.Normal, BmsGaugeRulesFamily gaugeRulesFamily = BmsGaugeRulesFamily.Legacy)
        {
            GaugeType = gaugeType;
            setGaugeRulesFamilyInternal(gaugeRulesFamily);
        }

        public void SetGaugeRulesFamily(BmsGaugeRulesFamily gaugeRulesFamily)
        {
            if (GaugeRulesFamily == gaugeRulesFamily)
                return;

            setGaugeRulesFamilyInternal(gaugeRulesFamily);

            if (TotalHittableObjects <= 0)
                return;

            updateGaugeBounds();

            if (JudgedHits == 0)
                Health.Value = StartingGaugeValue;
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            ChartTotal = beatmap is BmsBeatmap bmsBeatmap ? bmsBeatmap.BmsInfo.Total : DEFAULT_TOTAL;
            TotalHittableObjects = countHittableObjects(beatmap);
            currentKeymode = ResolveKeymode(beatmap);
            BaseRate = CalculateBaseRate(ChartTotal, TotalHittableObjects);
            updateGaugeSpecification();

            base.ApplyBeatmap(beatmap);
        }

        public static BmsKeymode ResolveKeymode(IBeatmap beatmap)
        {
            if (beatmap is BmsBeatmap bmsBeatmap)
                return bmsBeatmap.BmsInfo.Keymode;

            int storedKeyCount = (int)Math.Round(beatmap.Difficulty.CircleSize);

            if (storedKeyCount > 0)
                return keyCountToKeymode(storedKeyCount);

            int laneCount = beatmap.HitObjects.OfType<BmsHitObject>()
                                  .Select(hitObject => hitObject.LaneIndex)
                                  .DefaultIfEmpty(-1)
                                  .Max() + 1;

            return keyCountToKeymode(laneCount switch
            {
                6 => 5,
                8 => 7,
                9 => 9,
                16 => 14,
                _ => 7,
            });
        }

        public static double CalculateBaseRate(double total, int totalHittableObjects)
        {
            if (totalHittableObjects <= 0)
                return 0;

            return total / (totalHittableObjects * 100.0);
        }

        public static BmsGaugeType GetGaugeType(IEnumerable<Mod>? mods)
            => GetGaugeAutoShift(mods)?.GetStartingGaugeType() ?? mods?.OfType<BmsModGauge>().LastOrDefault()?.GaugeType ?? BmsGaugeType.Normal;

        public static BmsGaugeRulesFamily GetGaugeRulesFamily(IEnumerable<Mod>? mods)
            => mods?.OfType<BmsModGaugeRules>().LastOrDefault()?.GaugeRulesFamily ?? BmsGaugeRulesFamily.Legacy;

        public static BmsGaugeType GetGaugeType(ScoreInfo score)
            => score.GetRulesetData<BmsScoreInfoData>()?.GaugeType ?? GetGaugeType(score.Mods);

        public static BmsGaugeRulesFamily GetGaugeRulesFamily(ScoreInfo score)
            => score.GetRulesetData<BmsScoreInfoData>()?.GaugeRulesFamily ?? GetGaugeRulesFamily(score.Mods);

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

        public static string GetGaugeRulesDisplayName(ScoreInfo score)
            => GetGaugeRulesFamily(score).GetDisplayName();

        public static BmsGaugeProcessor CreateForMods(double drainStartTime, IEnumerable<Mod>? mods)
        {
            var gaugeRulesFamily = GetGaugeRulesFamily(mods);

            return GetGaugeAutoShift(mods) is BmsModGaugeAutoShift gasMod
                ? new BmsGasGaugeProcessor(drainStartTime, gasMod.GetStartingGaugeType(), gasMod.GetFloorGaugeType(), gaugeRulesFamily)
                : new BmsGaugeProcessor(drainStartTime, GetGaugeType(mods), gaugeRulesFamily);
        }

        public static BmsGaugeProcessor CreateForScore(double drainStartTime, ScoreInfo score)
        {
            var scoreData = score.GetRulesetData<BmsScoreInfoData>();
            var gaugeRulesFamily = scoreData?.GaugeRulesFamily ?? GetGaugeRulesFamily(score.Mods);

            if (scoreData?.UsesGaugeAutoShift == true)
                return new BmsGasGaugeProcessor(drainStartTime, scoreData.StartingGaugeType, scoreData.FloorGaugeType, gaugeRulesFamily);

            if (scoreData == null)
                return CreateForMods(drainStartTime, score.Mods);

            return new BmsGaugeProcessor(drainStartTime, scoreData.GaugeType, gaugeRulesFamily);
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
            => GetStartingGauge(gaugeType, BmsGaugeRulesFamily.Legacy);

        public static double GetStartingGauge(BmsGaugeType gaugeType, BmsGaugeRulesFamily gaugeRulesFamily, BmsKeymode keymode = BmsKeymode.Key7K)
            => getGaugeSettings(gaugeRulesFamily, keymode, gaugeType).Initial / 100.0;

        public static double GetFloorGauge(BmsGaugeType gaugeType)
            => GetFloorGauge(gaugeType, BmsGaugeRulesFamily.Legacy);

        public static double GetFloorGauge(BmsGaugeType gaugeType, BmsGaugeRulesFamily gaugeRulesFamily, BmsKeymode keymode = BmsKeymode.Key7K)
            => getGaugeSettings(gaugeRulesFamily, keymode, gaugeType).Minimum / 100.0;

        public static bool UsesSurvivalClear(BmsGaugeType gaugeType)
            => gaugeType is BmsGaugeType.Hard or BmsGaugeType.ExHard or BmsGaugeType.Hazard;

        public static bool MeetsClearCondition(BmsGaugeType gaugeType, double finalGauge, bool hasFailed)
            => MeetsClearCondition(gaugeType, BmsGaugeRulesFamily.Legacy, BmsKeymode.Key7K, finalGauge, hasFailed);

        public static bool MeetsClearCondition(BmsGaugeType gaugeType, BmsGaugeRulesFamily gaugeRulesFamily, BmsKeymode keymode, double finalGauge, bool hasFailed)
        {
            if (UsesSurvivalClear(gaugeType))
                return !hasFailed && finalGauge > 0;

            return finalGauge >= getGaugeSettings(gaugeRulesFamily, keymode, gaugeType).Border / 100.0;
        }

        protected override double GetHealthIncreaseFor(JudgementResult result)
        {
            if (result.HitObject is BmsHoldNoteBodyTick { CountsForGauge: true })
            {
                switch (result.Type)
                {
                    case HitResult.IgnoreHit:
                        return applyGaugeDelta(currentGaugeSpecification.Perfect * BmsHoldNoteBodyTick.TICK_QUANTUM / 1000.0);

                    case HitResult.IgnoreMiss:
                        return applyGaugeDelta(currentGaugeSpecification.Bad * BmsHoldNoteBodyTick.TICK_QUANTUM / 1000.0);
                }
            }

            if (result.HitObject is BmsEmptyPoorHitObject && result.Type == HitResult.Ok)
                return applyGaugeDelta(currentGaugeSpecification.EmptyPoor);

            return result.Type switch
            {
                HitResult.Perfect => applyGaugeDelta(currentGaugeSpecification.Perfect),
                HitResult.Great => applyGaugeDelta(currentGaugeSpecification.Great),
                HitResult.Good => applyGaugeDelta(currentGaugeSpecification.Good),
                HitResult.Meh => applyGaugeDelta(currentGaugeSpecification.Bad),
                HitResult.Miss => applyGaugeDelta(currentGaugeSpecification.Poor),
                _ => 0,
            };
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
                if (result is HitResult.Ok or HitResult.ComboBreak)
                    continue;

                judgedHits += count;
            }

            return judgedHits;
        }

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);

            updateGaugeBounds();
            Health.Value = StartingGaugeValue;
        }

        private static BmsModGaugeAutoShift? GetGaugeAutoShift(IEnumerable<Mod>? mods)
            => mods?.OfType<BmsModGaugeAutoShift>().LastOrDefault();

        private static BmsKeymode keyCountToKeymode(int keyCount)
            => keyCount switch
            {
                5 => BmsKeymode.Key5K,
                9 => BmsKeymode.Key9K_Bms,
                14 => BmsKeymode.Key14K,
                _ => BmsKeymode.Key7K,
            };

        private double applyGaugeDelta(double rawPercent)
        {
            if (rawPercent == 0)
                return 0;

            double adjustedPercent = rawPercent;

            if (adjustedPercent < 0)
                adjustedPercent *= getGutsMultiplier();

            return adjustedPercent / 100.0;
        }

        private double getGutsMultiplier()
        {
            double gaugePercent = Health.Value * 100.0;

            foreach (var step in currentGaugeSpecification.Guts)
            {
                if (step.AppliesTo(gaugePercent))
                    return step.Multiplier;
            }

            return 1;
        }

        private void setGaugeRulesFamilyInternal(BmsGaugeRulesFamily rulesFamily)
        {
            gaugeRulesFamily.Value = rulesFamily;
            updateGaugeSpecification();
        }

        private void updateGaugeSpecification()
            => currentGaugeSpecification = createGaugeSpecification(GaugeRulesFamily, currentKeymode, GaugeType, ChartTotal, TotalHittableObjects);

        private void updateGaugeBounds()
        {
            Health.MinValue = FloorGaugeValue;
            Health.MaxValue = MaximumGaugeValue;
            Health.Value = Math.Clamp(Health.Value, Health.MinValue, Health.MaxValue);
        }

        private static GaugeSpecification createGaugeSpecification(BmsGaugeRulesFamily gaugeRulesFamily, BmsKeymode keymode, BmsGaugeType gaugeType, double total, int totalHittableObjects)
            => gaugeRulesFamily switch
            {
                BmsGaugeRulesFamily.Beatoraja => createBeatorajaGaugeSpecification(keymode, gaugeType, total, totalHittableObjects),
                BmsGaugeRulesFamily.LR2 => createLr2GaugeSpecification(gaugeType, total, totalHittableObjects),
                BmsGaugeRulesFamily.IIDX => createIidxGaugeSpecification(gaugeType, totalHittableObjects),
                _ => createLegacyGaugeSpecification(gaugeType, total, totalHittableObjects),
            };

        private static GaugeSpecification createLegacyGaugeSpecification(BmsGaugeType gaugeType, double total, int totalHittableObjects)
        {
            double basePercent = totalHittableObjects <= 0 ? 0 : total / totalHittableObjects;

            return gaugeType switch
            {
                BmsGaugeType.AssistEasy => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.None, basePercent * 1.6, basePercent * 1.6, basePercent * 0.8, -basePercent * 4.0, -basePercent * 6.0, -basePercent * 4.0),
                BmsGaugeType.Easy => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.None, basePercent * 1.2, basePercent * 1.2, basePercent * 0.6, -basePercent * 6.0, -basePercent * 9.0, -basePercent * 6.0),
                BmsGaugeType.Normal => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.None, basePercent * 0.8, basePercent * 0.8, basePercent * 0.4, -basePercent * 8.0, -basePercent * 12.0, -basePercent * 8.0),
                BmsGaugeType.Hard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, basePercent * 0.8, basePercent * 0.8, basePercent * 0.4, -basePercent * 25.0, -basePercent * 37.5, -basePercent * 25.0),
                BmsGaugeType.ExHard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, basePercent * 0.8, basePercent * 0.8, basePercent * 0.4, -basePercent * 50.0, -basePercent * 75.0, -basePercent * 50.0),
                BmsGaugeType.Hazard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, basePercent * 0.8, basePercent * 0.8, 0, -100, -100, -100),
                _ => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.None, basePercent * 0.8, basePercent * 0.8, basePercent * 0.4, -basePercent * 8.0, -basePercent * 12.0, -basePercent * 8.0),
            };
        }

        private static GaugeSpecification createBeatorajaGaugeSpecification(BmsKeymode keymode, BmsGaugeType gaugeType, double total, int totalHittableObjects)
        {
            switch (resolveGaugeProfile(keymode))
            {
                case GaugeProfile.FiveKeys:
                    return gaugeType switch
                    {
                        BmsGaugeType.AssistEasy => createGaugeSpecification(new GaugeSettings(2, 20, 50), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -1.5, -3.0, -0.5, total, totalHittableObjects),
                        BmsGaugeType.Easy => createGaugeSpecification(new GaugeSettings(2, 20, 75), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -1.5, -4.5, -1.0, total, totalHittableObjects),
                        BmsGaugeType.Normal => createGaugeSpecification(new GaugeSettings(2, 20, 75), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -3.0, -6.0, -2.0, total, totalHittableObjects),
                        BmsGaugeType.Hard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.LimitIncrement, 0, 0, 0, -5.0, -10.0, -5.0, total, totalHittableObjects),
                        BmsGaugeType.ExHard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.ModifyDamage, 0, 0, 0, -10.0, -20.0, -10.0, total, totalHittableObjects),
                        BmsGaugeType.Hazard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0, 0, 0, -100.0, -100.0, -100.0),
                        _ => createLegacyGaugeSpecification(gaugeType, total, totalHittableObjects),
                    };

                case GaugeProfile.Pms:
                    return gaugeType switch
                    {
                        BmsGaugeType.AssistEasy => createGaugeSpecification(new GaugeSettings(2, 30, 65, 120), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -1.0, -2.0, -2.0, total, totalHittableObjects),
                        BmsGaugeType.Easy => createGaugeSpecification(new GaugeSettings(2, 30, 85, 120), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -1.0, -3.0, -3.0, total, totalHittableObjects),
                        BmsGaugeType.Normal => createGaugeSpecification(new GaugeSettings(2, 30, 85, 120), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -2.0, -6.0, -6.0, total, totalHittableObjects),
                        BmsGaugeType.Hard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.LimitIncrement, 0.15, 0.12, 0.03, -5.0, -10.0, -10.0, total, totalHittableObjects, beatoraja_hard_guts),
                        BmsGaugeType.ExHard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.LimitIncrement, 0.15, 0.06, 0, -10.0, -15.0, -15.0, total, totalHittableObjects),
                        BmsGaugeType.Hazard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0.15, 0.06, 0, -100.0, -100.0, -100.0),
                        _ => createLegacyGaugeSpecification(gaugeType, total, totalHittableObjects),
                    };

                default:
                    return gaugeType switch
                    {
                        BmsGaugeType.AssistEasy => createGaugeSpecification(new GaugeSettings(2, 20, 60), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -1.5, -3.0, -0.5, total, totalHittableObjects),
                        BmsGaugeType.Easy => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -1.5, -4.5, -1.0, total, totalHittableObjects),
                        BmsGaugeType.Normal => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -3.0, -6.0, -2.0, total, totalHittableObjects),
                        BmsGaugeType.Hard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.LimitIncrement, 0.15, 0.12, 0.03, -5.0, -10.0, -5.0, total, totalHittableObjects, beatoraja_hard_guts),
                        BmsGaugeType.ExHard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.LimitIncrement, 0.15, 0.06, 0, -8.0, -16.0, -8.0, total, totalHittableObjects),
                        BmsGaugeType.Hazard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0.15, 0.06, 0, -100.0, -100.0, -10.0),
                        _ => createLegacyGaugeSpecification(gaugeType, total, totalHittableObjects),
                    };
            }
        }

        private static GaugeSpecification createLr2GaugeSpecification(BmsGaugeType gaugeType, double total, int totalHittableObjects)
            => gaugeType switch
            {
                BmsGaugeType.AssistEasy => createGaugeSpecification(new GaugeSettings(2, 20, 60), GaugeValueModifier.Total, 1.2, 1.2, 0.6, -3.2, -4.8, -1.6, total, totalHittableObjects),
                BmsGaugeType.Easy => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.Total, 1.2, 1.2, 0.6, -3.2, -4.8, -1.6, total, totalHittableObjects),
                BmsGaugeType.Normal => createGaugeSpecification(new GaugeSettings(2, 20, 80), GaugeValueModifier.Total, 1.0, 1.0, 0.5, -4.0, -6.0, -2.0, total, totalHittableObjects),
                BmsGaugeType.Hard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.ModifyDamage, 0.1, 0.1, 0.05, -6.0, -10.0, -2.0, total, totalHittableObjects, lr2_hard_guts),
                BmsGaugeType.ExHard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.ModifyDamage, 0.1, 0.1, 0.05, -12.0, -20.0, -2.0, total, totalHittableObjects),
                BmsGaugeType.Hazard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0.15, 0.06, 0, -100.0, -100.0, -10.0),
                _ => createLegacyGaugeSpecification(gaugeType, total, totalHittableObjects),
            };

        private static GaugeSpecification createIidxGaugeSpecification(BmsGaugeType gaugeType, int totalHittableObjects)
        {
            double aValue = calculateIidxAValue(totalHittableObjects);

            return gaugeType switch
            {
                BmsGaugeType.AssistEasy => createGaugeSpecification(new GaugeSettings(0, 22, 60), GaugeValueModifier.None, aValue, aValue, aValue * 0.5, -1.6, -4.8, -1.6),
                BmsGaugeType.Easy => createGaugeSpecification(new GaugeSettings(0, 22, 80), GaugeValueModifier.None, aValue, aValue, aValue * 0.5, -1.6, -4.8, -1.6),
                BmsGaugeType.Normal => createGaugeSpecification(new GaugeSettings(0, 22, 80), GaugeValueModifier.None, aValue, aValue, aValue * 0.5, -2.0, -6.0, -2.0),
                BmsGaugeType.Hard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0.16, 0.16, 0, -5.0, -9.0, -5.0, guts: iidx_hard_guts),
                BmsGaugeType.ExHard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0.16, 0.16, 0, -10.0, -18.0, -10.0),
                // Hazard is a BMS-only extension, so use IIDX EX-HARD recovery with instant-fail penalties.
                BmsGaugeType.Hazard => createGaugeSpecification(new GaugeSettings(0, 100, 0), GaugeValueModifier.None, 0.16, 0.16, 0, -100.0, -100.0, -100.0),
                _ => createLegacyGaugeSpecification(gaugeType, DEFAULT_TOTAL, totalHittableObjects),
            };
        }

        private static GaugeSpecification createGaugeSpecification(GaugeSettings settings, GaugeValueModifier modifier, double perfect, double great, double good, double bad, double poor, double emptyPoor, double total = DEFAULT_TOTAL, int totalHittableObjects = 0, GaugeGutsStep[]? guts = null)
            => new GaugeSpecification(
                settings.Minimum,
                settings.Maximum,
                settings.Initial,
                settings.Border,
                applyModifier(modifier, perfect, total, totalHittableObjects),
                applyModifier(modifier, great, total, totalHittableObjects),
                applyModifier(modifier, good, total, totalHittableObjects),
                applyModifier(modifier, bad, total, totalHittableObjects),
                applyModifier(modifier, poor, total, totalHittableObjects),
                applyModifier(modifier, emptyPoor, total, totalHittableObjects),
                guts ?? no_guts);

        private static GaugeSettings getGaugeSettings(BmsGaugeRulesFamily gaugeRulesFamily, BmsKeymode keymode, BmsGaugeType gaugeType)
        {
            if (gaugeRulesFamily == BmsGaugeRulesFamily.IIDX)
            {
                return gaugeType switch
                {
                    BmsGaugeType.AssistEasy => new GaugeSettings(0, 22, 60),
                    BmsGaugeType.Easy => new GaugeSettings(0, 22, 80),
                    BmsGaugeType.Normal => new GaugeSettings(0, 22, 80),
                    _ => new GaugeSettings(0, 100, 0),
                };
            }

            if (gaugeRulesFamily == BmsGaugeRulesFamily.LR2)
            {
                return gaugeType switch
                {
                    BmsGaugeType.AssistEasy => new GaugeSettings(2, 20, 60),
                    BmsGaugeType.Easy => new GaugeSettings(2, 20, 80),
                    BmsGaugeType.Normal => new GaugeSettings(2, 20, 80),
                    _ => new GaugeSettings(0, 100, 0),
                };
            }

            if (gaugeRulesFamily == BmsGaugeRulesFamily.Beatoraja)
            {
                switch (resolveGaugeProfile(keymode))
                {
                    case GaugeProfile.FiveKeys:
                        return gaugeType switch
                        {
                            BmsGaugeType.AssistEasy => new GaugeSettings(2, 20, 50),
                            BmsGaugeType.Easy => new GaugeSettings(2, 20, 75),
                            BmsGaugeType.Normal => new GaugeSettings(2, 20, 75),
                            _ => new GaugeSettings(0, 100, 0),
                        };

                    case GaugeProfile.Pms:
                        return gaugeType switch
                        {
                            BmsGaugeType.AssistEasy => new GaugeSettings(2, 30, 65, 120),
                            BmsGaugeType.Easy => new GaugeSettings(2, 30, 85, 120),
                            BmsGaugeType.Normal => new GaugeSettings(2, 30, 85, 120),
                            _ => new GaugeSettings(0, 100, 0),
                        };

                    default:
                        return gaugeType switch
                        {
                            BmsGaugeType.AssistEasy => new GaugeSettings(2, 20, 60),
                            BmsGaugeType.Easy => new GaugeSettings(2, 20, 80),
                            BmsGaugeType.Normal => new GaugeSettings(2, 20, 80),
                            _ => new GaugeSettings(0, 100, 0),
                        };
                }
            }

            return gaugeType switch
            {
                BmsGaugeType.AssistEasy => new GaugeSettings(2, 20, 80),
                BmsGaugeType.Easy => new GaugeSettings(2, 20, 80),
                BmsGaugeType.Normal => new GaugeSettings(2, 20, 80),
                _ => new GaugeSettings(0, 100, 0),
            };
        }

        private static GaugeProfile resolveGaugeProfile(BmsKeymode keymode)
            => keymode switch
            {
                BmsKeymode.Key5K => GaugeProfile.FiveKeys,
                BmsKeymode.Key9K_Pms => GaugeProfile.Pms,
                _ => GaugeProfile.SevenKeys,
            };

        private static double applyModifier(GaugeValueModifier modifier, double value, double total, int totalHittableObjects)
        {
            if (modifier == GaugeValueModifier.None)
                return value;

            if (totalHittableObjects <= 0)
                return 0;

            return modifier switch
            {
                GaugeValueModifier.Total when value > 0 => value * total / totalHittableObjects,
                GaugeValueModifier.LimitIncrement when value > 0 => value * calculateLimitIncrementScale(total, totalHittableObjects),
                GaugeValueModifier.ModifyDamage when value < 0 => value * calculateModifyDamageMultiplier(total, totalHittableObjects),
                _ => value,
            };
        }

        private static double calculateLimitIncrementScale(double total, int totalHittableObjects)
        {
            double pg = Math.Max(Math.Min(0.15, (2 * total - 320) / totalHittableObjects), 0);
            return pg / 0.15;
        }

        private static double calculateModifyDamageMultiplier(double total, int totalHittableObjects)
        {
            double fix2 = 1.0;
            double[] fix1Total = { 240.0, 230.0, 210.0, 200.0, 180.0, 160.0, 150.0, 130.0, 120.0, 0 };
            double[] fix1Table = { 1.0, 1.11, 1.25, 1.5, 1.666, 2.0, 2.5, 3.333, 5.0, 10.0 };
            int i = 0;

            for (; i < fix1Total.Length - 1 && total < fix1Total[i]; i++)
            {
            }

            int note = 1000;
            double mod = 0.002;

            while (note > totalHittableObjects || note > 1)
            {
                fix2 += mod * (note - Math.Max(totalHittableObjects, note / 2.0));
                note /= 2;
                mod *= 2.0;
            }

            return Math.Max(fix1Table[i], fix2);
        }

        private static double calculateIidxAValue(int totalHittableObjects)
        {
            if (totalHittableObjects <= 0)
                return 0;

            return totalHittableObjects <= 338
                ? 260.0 / totalHittableObjects
                : 760.5 / (totalHittableObjects + 650.0);
        }

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

        private readonly struct GaugeSettings
        {
            public readonly double Minimum;
            public readonly double Maximum;
            public readonly double Initial;
            public readonly double Border;

            public GaugeSettings(double minimum, double initial, double border, double maximum = 100)
            {
                Minimum = minimum;
                Maximum = maximum;
                Initial = initial;
                Border = border;
            }
        }

        private readonly struct GaugeSpecification
        {
            public readonly double Minimum;
            public readonly double Maximum;
            public readonly double Initial;
            public readonly double Border;
            public readonly double Perfect;
            public readonly double Great;
            public readonly double Good;
            public readonly double Bad;
            public readonly double Poor;
            public readonly double EmptyPoor;
            public readonly GaugeGutsStep[] Guts;

            public GaugeSpecification(double minimum, double maximum, double initial, double border, double perfect, double great, double good, double bad, double poor, double emptyPoor, GaugeGutsStep[] guts)
            {
                Minimum = minimum;
                Maximum = maximum;
                Initial = initial;
                Border = border;
                Perfect = perfect;
                Great = great;
                Good = good;
                Bad = bad;
                Poor = poor;
                EmptyPoor = emptyPoor;
                Guts = guts;
            }
        }

        private readonly struct GaugeGutsStep
        {
            public readonly double Threshold;
            public readonly double Multiplier;
            public readonly bool Inclusive;

            public GaugeGutsStep(double threshold, double multiplier, bool inclusive = false)
            {
                Threshold = threshold;
                Multiplier = multiplier;
                Inclusive = inclusive;
            }

            public bool AppliesTo(double gaugePercent)
                => Inclusive ? gaugePercent <= Threshold : gaugePercent < Threshold;
        }

        private enum GaugeValueModifier
        {
            None,
            Total,
            LimitIncrement,
            ModifyDamage,
        }

        private enum GaugeProfile
        {
            FiveKeys,
            SevenKeys,
            Pms,
        }
    }
}
