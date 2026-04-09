// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Bms.Objects;

namespace osu.Game.Rulesets.Bms.Scoring
{
    public enum BmsLongNoteMode
    {
        LN,
        CN,
        HCN,
    }

    public static class BmsLongNoteModeExtensions
    {
        public static string GetDisplayName(this BmsLongNoteMode longNoteMode)
            => longNoteMode switch
            {
                BmsLongNoteMode.LN => "LN",
                BmsLongNoteMode.CN => "CN",
                BmsLongNoteMode.HCN => "HCN",
                _ => longNoteMode.ToString(),
            };

        public static bool RequiresTailJudgement(this BmsLongNoteMode longNoteMode)
            => longNoteMode != BmsLongNoteMode.LN;

        public static bool RequiresBodyGaugeTicks(this BmsLongNoteMode longNoteMode)
            => longNoteMode == BmsLongNoteMode.HCN;

        public static void ApplyToBeatmap(this BmsLongNoteMode longNoteMode, IBeatmap beatmap)
        {
            bool tailCountsForScore = longNoteMode.RequiresTailJudgement();
            bool bodyCountsForGauge = longNoteMode.RequiresBodyGaugeTicks();

            foreach (var holdNote in beatmap.HitObjects.OfType<BmsHoldNote>())
            {
                if (holdNote.Tail?.Judgement is BmsHoldNoteTailJudgement tailJudgement)
                    tailJudgement.CountsForScore = tailCountsForScore;

                foreach (var bodyTick in holdNote.BodyTicks)
                    bodyTick.CountsForGauge = bodyCountsForGauge;
            }
        }
    }
}
