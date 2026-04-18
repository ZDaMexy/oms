// Copyright (c) OMS contributors. Licensed under the MIT Licence.

using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Bms.Audio;
using osu.Game.Rulesets.Bms.Difficulty;
using osu.Game.Rulesets.Bms.Scoring;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Bms.Objects
{
    /// <summary>
    /// A playable BMS note carrying lane and keysound metadata.
    /// </summary>
    public class BmsHitObject : HitObject, IHasColumn, IHasXPosition
    {
        private HitObjectProperty<int> laneIndex;
        private HitObjectProperty<int?> keysoundId;
        private HitObjectProperty<BmsKeysoundSampleInfo?> keysoundSample;
        private HitObjectProperty<BmsKeymode?> keymode;
        private HitObjectProperty<bool> isScratch;
        private HitObjectProperty<bool> autoPlay;
        private HitObjectProperty<bool> countsForScore = new HitObjectProperty<bool>(true);

        public virtual int LaneIndex
        {
            get => laneIndex.Value;
            set => laneIndex.Value = value;
        }

        public virtual int Column
        {
            get => LaneIndex;
            set => LaneIndex = value;
        }

        public virtual int? KeysoundId
        {
            get => keysoundId.Value;
            set => keysoundId.Value = value;
        }

        public virtual BmsKeysoundSampleInfo? KeysoundSample
        {
            get => keysoundSample.Value;
            set => keysoundSample.Value = value;
        }

        public virtual BmsKeymode Keymode
        {
            get => keymode.Value ?? BmsKeymode.Key7K;
            set => keymode.Value = value;
        }

        public virtual bool IsScratch
        {
            get => isScratch.Value;
            set => isScratch.Value = value;
        }

        public virtual bool AutoPlay
        {
            get => autoPlay.Value;
            set => autoPlay.Value = value;
        }

        public virtual bool CountsForScore
        {
            get => countsForScore.Value;
            set => countsForScore.Value = value;
        }

        float IHasXPosition.X
        {
            get => Column;
            set => Column = (int)value;
        }

        protected override HitWindows CreateHitWindows() => new BmsTimingWindows();

        public override Judgement CreateJudgement() => new BmsHitObjectJudgement { CountsForScore = CountsForScore };
    }
}
