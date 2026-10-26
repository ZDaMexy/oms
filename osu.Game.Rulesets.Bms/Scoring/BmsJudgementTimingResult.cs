// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Skinning.Gameplay;

namespace osu.Game.Rulesets.Bms.Scoring
{
    /// <summary>
    /// Retains input provenance beside the existing BMS result without changing its scoring facts.
    /// </summary>
    internal sealed class BmsJudgementTimingResult : JudgementResult, IGameplaySkinJudgementTimingMetadata
    {
        private bool hasPlayerTiming;

        public bool HasPlayerTiming
        {
            get => HasResult && hasPlayerTiming;
            internal set => hasPlayerTiming = value;
        }

        public BmsJudgementTimingResult(HitObject hitObject, Judgement judgement)
            : base(hitObject, judgement)
        {
        }
    }
}
