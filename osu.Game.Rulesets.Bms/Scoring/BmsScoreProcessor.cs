// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Collections.Generic;
using System.Linq;
#if DEBUG
using osu.Framework.Logging;
#endif
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Mods;
using osu.Game.Rulesets.Bms.Objects;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public partial class BmsScoreProcessor : ScoreProcessor
    {
        public const BmsLongNoteMode DEFAULT_LONG_NOTE_MODE = BmsLongNoteMode.LN;

        private long currentExScore;

#if DEBUG
        private double beatmapEndTime;
        private bool hasLoggedCompletionDiagnostic;
        private int judgedNotes, judgedHeads, judgedTails, judgedTicks, judgedParents, judgedBgm, judgedOther;
#endif

        public long CurrentExScore => currentExScore;

        public long MaximumExScore => MaximumTotalScore;

        public BmsScoreProcessor()
            : base(new BmsRuleset())
        {
        }

        public override void ApplyBeatmap(IBeatmap beatmap)
        {
            GetLongNoteMode(Mods.Value).ApplyToBeatmap(beatmap);
            base.ApplyBeatmap(beatmap);
            TotalScoreWithoutMods.Value = currentExScore;
            TotalScore.Value = currentExScore;

#if DEBUG
            beatmapEndTime = beatmap.HitObjects.Any() ? beatmap.HitObjects.Max(h => h.GetEndTime()) : 0;
            hasLoggedCompletionDiagnostic = false;

            int countByType(System.Func<HitObject, bool> predicate) =>
                beatmap.HitObjects.SelectMany(h => h.NestedHitObjects.Prepend(h)).Count(predicate);

            Logger.Log(
                $"[BMS] ApplyBeatmap: MaxHits={MaxHits} EndTime={beatmapEndTime:F0}"
                + $" Notes={beatmap.HitObjects.OfType<BmsHitObject>().Count(h => h is not BmsHoldNote)}"
                + $" Holds={beatmap.HitObjects.OfType<BmsHoldNote>().Count()}"
                + $" Bgm={beatmap.HitObjects.OfType<BmsBgmEvent>().Count()}"
                + $" Heads={countByType(h => h is BmsHoldNoteHead)}"
                + $" Tails={countByType(h => h is BmsHoldNoteTailEvent)}"
                + $" Ticks={countByType(h => h is BmsHoldNoteBodyTick)}",
                LoggingTarget.Runtime);
#endif
        }

#if DEBUG
        protected override void Update()
        {
            base.Update();

            if (!hasLoggedCompletionDiagnostic && JudgedHits > 0
                && Clock?.CurrentTime > beatmapEndTime + 3000)
            {
                hasLoggedCompletionDiagnostic = true;

                if (HasCompleted.Value)
                {
                    Logger.Log(
                        $"[BMS] COMPLETED OK: JudgedHits={JudgedHits}/{MaxHits} Clock={Clock?.CurrentTime:F0}"
                        + $" Judged[Notes={judgedNotes} Heads={judgedHeads} Tails={judgedTails} Ticks={judgedTicks} Parents={judgedParents} Bgm={judgedBgm} Other={judgedOther}]",
                        LoggingTarget.Runtime);
                }
                else
                {
                    Logger.Log(
                        $"[BMS] COMPLETION STUCK: JudgedHits={JudgedHits}/{MaxHits} Clock={Clock?.CurrentTime:F0} EndTime={beatmapEndTime:F0}"
                        + $" Judged[Notes={judgedNotes} Heads={judgedHeads} Tails={judgedTails} Ticks={judgedTicks} Parents={judgedParents} Bgm={judgedBgm} Other={judgedOther}]",
                        LoggingTarget.Runtime);
                }
            }
        }
#endif

        protected override void ApplyScoreChange(JudgementResult result)
        {
            currentExScore += GetExScoreForResult(result.Type);

#if DEBUG
            switch (result.HitObject)
            {
                case BmsHoldNoteHead: judgedHeads++; break;
                case BmsHoldNoteTailEvent: judgedTails++; break;
                case BmsHoldNoteBodyTick: judgedTicks++; break;
                case BmsHoldNote: judgedParents++; break;
                case BmsHitObject: judgedNotes++; break;
                case BmsBgmEvent: judgedBgm++; break;
                default: judgedOther++; break;
            }
#endif
        }

        public override int GetBaseScoreForResult(HitResult result)
            => GetExScoreForResult(result);

        protected override double GetComboScoreChange(JudgementResult result) => 0;

        protected override void RemoveScoreChange(JudgementResult result)
            => currentExScore -= GetExScoreForResult(result.Type);

        protected override bool ResultIncreasesCombo(HitResult result)
            => result switch
            {
                HitResult.Perfect => true,
                HitResult.Great => true,
                HitResult.Good => true,
                _ => false,
            };

        protected override bool ResultBreaksCombo(HitResult result)
            => result switch
            {
                HitResult.ComboBreak => true,
                HitResult.Meh => true,
                HitResult.Miss => true,
                _ => false,
            };

        protected override double ComputeTotalScore(double comboProgress, double accuracyProgress, double bonusPortion)
            => currentExScore;

        protected override void Reset(bool storeResults)
        {
            base.Reset(storeResults);
            currentExScore = 0;
#if DEBUG
            judgedNotes = judgedHeads = judgedTails = judgedTicks = judgedParents = judgedBgm = judgedOther = 0;
#endif
        }

        protected override bool CountsResultTowardsJudgedHits(JudgementResult result)
            // Whitelist: only count objects that derive from BmsHitObject (single notes,
            // hold-note heads, hold-note tails) but NOT the parent BmsHoldNote container.
            // This automatically excludes BmsEmptyPoorHitObject, BmsBgmEvent and
            // BmsHoldNoteBodyTick which all inherit HitObject directly.
            => result.HitObject is BmsHitObject and not BmsHoldNote;

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

        public override void ResetFromReplayFrame(osu.Game.Rulesets.Replays.ReplayFrame frame)
        {
            base.ResetFromReplayFrame(frame);

            currentExScore = CalculateExScore(Statistics);
            TotalScoreWithoutMods.Value = currentExScore;
            TotalScore.Value = currentExScore;
        }

        public override ScoreRank RankFromScore(double accuracy, IReadOnlyDictionary<HitResult, int> results)
        {
            if (accuracy == 1)
                return ScoreRank.X;

            return BmsDjLevelCalculator.Calculate(accuracy) switch
            {
                BmsDjLevel.AAA => ScoreRank.S,
                BmsDjLevel.AA => ScoreRank.A,
                BmsDjLevel.A => ScoreRank.B,
                BmsDjLevel.B => ScoreRank.C,
                _ => ScoreRank.D,
            };
        }

        public static int GetExScoreForResult(HitResult result)
            => result switch
            {
                HitResult.Perfect => 2,
                HitResult.Great => 1,
                _ => 0,
            };

        public static BmsLongNoteMode GetLongNoteMode(IEnumerable<Mod>? mods)
            => mods?.OfType<BmsModLongNoteMode>().LastOrDefault()?.LongNoteMode ?? DEFAULT_LONG_NOTE_MODE;

        public static BmsLongNoteMode GetLongNoteMode(ScoreInfo score)
            => score.GetRulesetData<BmsScoreInfoData>()?.LongNoteMode ?? GetLongNoteMode(score.Mods);

        public static long CalculateExScore(IReadOnlyDictionary<HitResult, int> statistics)
            => statistics.GetValueOrDefault(HitResult.Perfect) * 2L
               + statistics.GetValueOrDefault(HitResult.Great);

        public static long CalculateMaxExScore(IReadOnlyDictionary<HitResult, int> maximumStatistics)
            => maximumStatistics.GetValueOrDefault(HitResult.Perfect) * 2L
               + maximumStatistics.GetValueOrDefault(HitResult.Great);

        public static int GetEmptyPoorCount(IReadOnlyDictionary<HitResult, int> statistics)
            => statistics.GetValueOrDefault(HitResult.ComboBreak);
    }
}
